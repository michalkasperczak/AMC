using System.IO;
using System.Security.Cryptography;
using System.Text;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

/// <summary>
/// Real-file tests of appending a selected range after the whole content of an
/// existing audio file. Everything is measured on generated tone files with a
/// decoder: durations, byte order of the content, the untouched source and a
/// byte-exact backup of the replaced target.
/// </summary>
internal static class AudioClipAppendTests
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(30);

    internal static void Run()
    {
        var cases = new (string Name, Action<string> Body)[]
        {
            ("Zaznaczenie ląduje po całym dotychczasowym pliku", AppendsAfterWholeTarget),
            ("Anulowanie przed zapisem nie zmienia żadnego pliku", CancellationLeavesBothFilesUntouched),
            ("Ten sam plik jako źródło i cel jest odrzucany", RejectsSameSourceAndTarget),
            ("Brakujący plik docelowy jest odrzucany", RejectsMissingTarget),
            ("Zmiana pliku docelowego w trakcie pracy nie jest nadpisywana", DetectsTargetChangedBeforeCommit),
            ("Dwa równoległe dołączenia do jednego pliku nie gubią dźwięku", SerializesConcurrentAppends),
            ("FLAC zachowuje format i kolejność dźwięku", r=>AppendsToOtherContainerFormat(r,".flac","fLaC")),
            ("MP3 zachowuje format i kolejność dźwięku", r=>AppendsToOtherContainerFormat(r,".mp3",string.Empty)),
            ("M4A zachowuje format i kolejność dźwięku", r=>AppendsToOtherContainerFormat(r,".m4a","ftyp")),
            ("Ogg zachowuje format i kolejność dźwięku", r=>AppendsToOtherContainerFormat(r,".ogg","OggS")),
            ("Podmiana tej samej wielkości i daty nie nadpisuje nowej treści", DetectsSameMetadataReplacement),
            ("Plik tylko do odczytu pozostaje nietknięty", RejectsReadOnlyTarget),
            ("Zajęty plik pozostaje nietknięty", RejectsLockedTarget),
            ("Nieobsługiwany lub wideo cel nie jest przyjmowany", RejectsUnsupportedTarget)
        };

        var failed = 0;
        foreach (var (name, body) in cases)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-clip-append-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                body(root);
                Console.WriteLine("OK: " + name);
            }
            catch (Exception error)
            {
                failed++;
                Console.Error.WriteLine("BLAD: " + name + ": " + error);
            }
            finally
            {
                TryDeleteDirectory(root);
            }
        }

        Console.WriteLine(
            $"DOLACZANIE FRAGMENTU: {cases.Length - failed} OK / {failed} BLAD / razem {cases.Length}");
        if (failed != 0)
            throw new InvalidOperationException("Dołączanie zaznaczenia: nie przeszły wszystkie przypadki.");
    }

    private static void RejectsUnsupportedTarget(string root)
    {
        var source=Path.Combine(root,"źródło.wav");WriteTone(source,TimeSpan.FromSeconds(3),440);
        foreach(var extension in new[]{".mp4",".txt",".wma"})
        {
            var target=Path.Combine(root,"cel"+extension);File.Copy(source,target);
            Check(!AudioClipAppender.SupportsTarget(target),"Deklarowana obsługa niebezpiecznego lub nieobsługiwanego celu "+extension);
            var before=Hash(File.ReadAllBytes(target));var refused=false;
            try {Wait(AudioClipAppender.AppendAsync(new(source,target,TimeSpan.Zero,TimeSpan.FromSeconds(1)),null,CancellationToken.None));}
            catch(NotSupportedException){refused=true;}
            Check(refused && Hash(File.ReadAllBytes(target))==before,"Nie odrzucono celu bez zmiany jego zawartości");
        }
    }

    private sealed class ImmediateContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) => callback(state);
    }

    private static void DetectsSameMetadataReplacement(string root)
    {
        var target=Path.Combine(root,"cel.wav");var source=Path.Combine(root,"źródło.wav");
        WriteTone(target,TimeSpan.FromSeconds(2),220);WriteTone(source,TimeSpan.FromSeconds(20),1200);
        var length=new FileInfo(target).Length;var stamp=File.GetLastWriteTimeUtc(target);
        var changed=false;string? newerHash=null;var oldContext=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new ImmediateContext());
        try
        {
            var progress=new Progress<double>(_=>{
                if(changed)return;
                WriteTone(target,TimeSpan.FromSeconds(2),660);File.SetLastWriteTimeUtc(target,stamp);
                Check(new FileInfo(target).Length==length,"Kontrolka jednakowej długości pliku");
                newerHash=Hash(File.ReadAllBytes(target));changed=true;
            });
            var refused=false;
            try {Wait(AudioClipAppender.AppendAsync(new(source,target,TimeSpan.Zero,TimeSpan.FromSeconds(10)),progress,CancellationToken.None));}
            catch(IOException) {refused=true;}
            Check(changed,"Nie wykonano podmiany podczas przygotowania");
            Check(refused,"Zgodność długości i daty ukryła zmianę treści pliku docelowego");
            Check(Hash(File.ReadAllBytes(target))==newerHash,"Nadpisano nową treść pliku");
        }
        finally {SynchronizationContext.SetSynchronizationContext(oldContext);}
    }

    private static void RejectsReadOnlyTarget(string root)
    {
        var target=Path.Combine(root,"cel.wav");var source=Path.Combine(root,"źródło.wav");
        WriteTone(target,TimeSpan.FromSeconds(2),220);WriteTone(source,TimeSpan.FromSeconds(4),1200);
        var before=Hash(File.ReadAllBytes(target));File.SetAttributes(target,FileAttributes.ReadOnly);
        var refused=false;
        try {Wait(AudioClipAppender.AppendAsync(new(source,target,TimeSpan.Zero,TimeSpan.FromSeconds(1)),null,CancellationToken.None));}
        catch(UnauthorizedAccessException) {refused=true;}
        finally {File.SetAttributes(target,FileAttributes.Normal);}
        Check(refused && Hash(File.ReadAllBytes(target))==before,"Cel tylko do odczytu nie został bezpiecznie odrzucony");
        Check(Directory.GetFiles(root).Length==2,"Przy odmowie powstały dodatkowe pliki");
    }

    private static void RejectsLockedTarget(string root)
    {
        var target=Path.Combine(root,"cel.wav");var source=Path.Combine(root,"źródło.wav");
        WriteTone(target,TimeSpan.FromSeconds(2),220);WriteTone(source,TimeSpan.FromSeconds(4),1200);
        var before=Hash(File.ReadAllBytes(target));var refused=false;
        using(var locked=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            try {Wait(AudioClipAppender.AppendAsync(new(source,target,TimeSpan.Zero,TimeSpan.FromSeconds(1)),null,CancellationToken.None));}
            catch(IOException e) {refused=e.Message.Contains("używany",StringComparison.Ordinal);}
        }
        Check(refused && Hash(File.ReadAllBytes(target))==before,"Cel używany przez inny proces nie został bezpiecznie odrzucony");
        Check(Directory.GetFiles(root).Length==2,"Przy odmowie powstały dodatkowe pliki");
    }

    private static void AppendsAfterWholeTarget(string root)
    {
        var target = Path.Combine(root, "cel próby.wav");
        var source = Path.Combine(root, "źródło próby.wav");
        WriteTone(target, TimeSpan.FromSeconds(2), 220);
        WriteSelectedTone(source);
        var targetBefore = File.ReadAllBytes(target);
        var targetHashBefore = Hash(targetBefore);
        var sourceHashBefore = Hash(File.ReadAllBytes(source));
        var targetPcmBefore = ReadPcm(target);
        var start = TimeSpan.FromSeconds(1);
        var end = TimeSpan.FromSeconds(3.5);
        // Dlugosc oczekiwanego ogona liczona w formacie pliku DOCELOWEGO:
        // dekoder eksportu zwraca 32-bitowe probki zmiennoprzecinkowe, wiec
        // porownanie bajt w bajt z jego wynikiem mierzylo tylko rozny format.
        var clipDuration = ReferenceClipDuration(root, source, start, end);
        var expectedTailBytes = FrameBytes(target, clipDuration);

        var result = Wait(AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(source, target, start, end),
            null,
            CancellationToken.None));

        Check(
            Hash(File.ReadAllBytes(source)) == sourceHashBefore,
            "Plik źródłowy został zmieniony przy dołączaniu.");
        Check(File.Exists(result.BackupPath), "Nie powstała kopia zapasowa poprzedniego pliku docelowego.");
        Check(
            Hash(File.ReadAllBytes(result.BackupPath)) == targetHashBefore,
            "Kopia zapasowa nie jest wierną kopią poprzedniego pliku docelowego.");
        Check(!result.TargetWasReencoded, "Dołączenie do pliku WAV nie powinno wymagać ponownego kodowania.");

        var resultPcm = ReadPcm(target);
        Check(
            Math.Abs(resultPcm.Length - (targetPcmBefore.Length + expectedTailBytes)) <= 4 * 441,
            $"Wynik ma {resultPcm.Length} bajtów dźwięku, a oczekiwano około "
            + $"{targetPcmBefore.Length + expectedTailBytes}.");
        Check(
            resultPcm.AsSpan(0, targetPcmBefore.Length).SequenceEqual(targetPcmBefore),
            "Początek wyniku nie jest dotychczasowym plikiem docelowym bajt w bajt.");
        // Ogon musi brzmiec jak ZRODLO (1200 Hz), nie jak dotychczasowy cel (220 Hz):
        // to rozstrzyga o kolejnosci, nie tylko o dlugosci.
        var tailFrequency = DominantFrequency(target, resultPcm, targetPcmBefore.Length);
        Check(
            Math.Abs(tailFrequency - 1200) < 60,
            $"Dołączony ogon brzmi jak {tailFrequency:0} Hz, a zaznaczone źródło ma 1200 Hz.");

        using var reader = new WaveFileReader(target);
        var expectedDuration = result.TargetDurationBefore + result.AppendedDuration;
        Check(
            (reader.TotalTime - expectedDuration).Duration() < TimeSpan.FromMilliseconds(30),
            $"Dekoder widzi {reader.TotalTime:c}, a suma wynosi {expectedDuration:c}.");
        Check(
            (result.TargetDurationAfter - reader.TotalTime).Duration() < TimeSpan.FromMilliseconds(30),
            "Zgłoszona długość po dołączeniu nie zgadza się z odczytem dekodera.");
        Check(
            result.AppendedDuration > TimeSpan.FromSeconds(2.4)
            && result.AppendedDuration < TimeSpan.FromSeconds(2.6),
            $"Dołączono {result.AppendedDuration:c} zamiast zaznaczonych 2,5 s.");
    }

    private static void CancellationLeavesBothFilesUntouched(string root)
    {
        var target = Path.Combine(root, "cel.wav");
        var source = Path.Combine(root, "źródło.wav");
        WriteTone(target, TimeSpan.FromSeconds(2), 220);
        WriteTone(source, TimeSpan.FromSeconds(20), 1200);
        var targetHash = Hash(File.ReadAllBytes(target));
        var sourceHash = Hash(File.ReadAllBytes(source));

        using var cancellation = new CancellationTokenSource();
        var progress = new Progress<double>(_ => cancellation.Cancel());
        var canceled = false;
        try
        {
            Wait(AudioClipAppender.AppendAsync(
                new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(18)),
                progress,
                cancellation.Token));
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        Check(canceled, "Anulowanie nie przerwało dołączania.");
        Check(Hash(File.ReadAllBytes(target)) == targetHash, "Anulowanie zmieniło plik docelowy.");
        Check(Hash(File.ReadAllBytes(source)) == sourceHash, "Anulowanie zmieniło plik źródłowy.");
        var leftovers = Directory
            .GetFiles(root)
            .Where(path => !string.Equals(path, target, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, source, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Check(
            leftovers.Length == 0,
            "Po anulowaniu zostały pliki techniczne: " + string.Join(", ", leftovers.Select(Path.GetFileName)));
    }

    private static void RejectsSameSourceAndTarget(string root)
    {
        var path = Path.Combine(root, "jeden.wav");
        WriteTone(path, TimeSpan.FromSeconds(4), 440);
        var hash = Hash(File.ReadAllBytes(path));
        var rejected = false;
        try
        {
            Wait(AudioClipAppender.AppendAsync(
                new AudioClipAppendRequest(path, path, TimeSpan.Zero, TimeSpan.FromSeconds(2)),
                null,
                CancellationToken.None));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Check(rejected, "Ten sam plik jako źródło i cel nie został odrzucony.");
        Check(Hash(File.ReadAllBytes(path)) == hash, "Odrzucone żądanie zmieniło plik.");
        Check(Directory.GetFiles(root).Length == 1, "Odrzucone żądanie utworzyło dodatkowe pliki.");
    }

    private static void RejectsMissingTarget(string root)
    {
        var source = Path.Combine(root, "źródło.wav");
        WriteTone(source, TimeSpan.FromSeconds(4), 440);
        var rejected = false;
        try
        {
            Wait(AudioClipAppender.AppendAsync(
                new AudioClipAppendRequest(source, Path.Combine(root, "nie ma.wav"), TimeSpan.Zero, TimeSpan.FromSeconds(2)),
                null,
                CancellationToken.None));
        }
        catch (FileNotFoundException)
        {
            rejected = true;
        }

        Check(rejected, "Dołączanie do nieistniejącego pliku nie zostało odrzucone.");
        Check(Directory.GetFiles(root).Length == 1, "Odrzucone żądanie utworzyło dodatkowe pliki.");
    }

    private static void DetectsTargetChangedBeforeCommit(string root)
    {
        var target = Path.Combine(root, "cel.wav");
        var source = Path.Combine(root, "źródło.wav");
        WriteTone(target, TimeSpan.FromSeconds(2), 220);
        WriteTone(source, TimeSpan.FromSeconds(20), 1200);
        var replaced = 0;
        var progress = new Progress<double>(_ =>
        {
            if (Interlocked.Exchange(ref replaced, 1) != 0) return;
            WriteTone(target, TimeSpan.FromSeconds(3), 660);
        });

        var refused = false;
        try
        {
            Wait(AudioClipAppender.AppendAsync(
                new AudioClipAppendRequest(source, target, TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                progress,
                CancellationToken.None));
        }
        catch (IOException)
        {
            refused = true;
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Check(replaced == 1, "Test nie zdążył podmienić pliku docelowego w trakcie pracy.");
        Check(refused, "Dołączanie nie wykryło, że plik docelowy zmienił się w trakcie pracy.");
        var newer = ReadWhenFree(target);
        Check(
            (newer - TimeSpan.FromSeconds(3)).Duration() < TimeSpan.FromMilliseconds(30),
            $"Nowsza treść pliku docelowego została nadpisana starszym wynikiem ({newer:c}).");
    }

    private static TimeSpan ReadWhenFree(string path)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using var reader = new WaveFileReader(path);
                return reader.TotalTime;
            }
            catch (IOException error)
            {
                last = error;
                Thread.Sleep(100);
            }
        }
        throw last ?? new IOException("Nie można odczytać pliku " + path);
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    private static void SerializesConcurrentAppends(string root)
    {
        var target = Path.Combine(root, "cel.wav");
        var first = Path.Combine(root, "pierwsze.wav");
        var second = Path.Combine(root, "drugie.wav");
        WriteTone(target, TimeSpan.FromSeconds(2), 220);
        WriteTone(first, TimeSpan.FromSeconds(4), 800);
        WriteTone(second, TimeSpan.FromSeconds(4), 1500);
        var targetPcmLength = ReadPcm(target).Length;
        var clipLength = FrameBytes(
            target,
            ReferenceClipDuration(root, first, TimeSpan.Zero, TimeSpan.FromSeconds(1)));

        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        var firstTask = Task.Run(() => AudioClipAppender.AppendAsync(
            new AudioClipAppendRequest(first, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
            new InlineProgress(value =>
            {
                firstEntered.Set();
                if (!releaseFirst.Wait(TimeSpan.FromSeconds(15)))
                    throw new TimeoutException("Próba nie zwolniła pierwszego zapisu.");
            }), CancellationToken.None));
        Task<AudioClipAppendResult>? secondTask = null;
        var overlapped = false;
        try
        {
            Check(firstEntered.Wait(TimeSpan.FromSeconds(10)), "Pierwszy zapis nie rozpoczął eksportu.");
            secondTask = Task.Run(() =>
            {
                secondStarted.Set();
                return AudioClipAppender.AppendAsync(
                    new AudioClipAppendRequest(second, target, TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                    new InlineProgress(value => secondEntered.Set()), CancellationToken.None);
            });
            Check(secondStarted.Wait(TimeSpan.FromSeconds(10)), "Drugie wywołanie nie wystartowało.");
            overlapped = secondEntered.Wait(TimeSpan.FromMilliseconds(500));
        }
        finally { releaseFirst.Set(); }
        Exception? failure = null;
        try { Wait(Task.WhenAll(firstTask, secondTask ?? firstTask)); }
        catch (Exception error) { failure = error; }
        Check(!overlapped, "Drugi zapis wszedł do tego samego celu przed zakończeniem pierwszego.");
        if (failure is not null) throw failure;
        var firstResult = firstTask.Result;
        var secondResult = secondTask!.Result;
        Check(firstResult.BackupPath != secondResult.BackupPath,
            "Dwa zapisy użyły tej samej kopii zapasowej.");
        Check(ReadPcm(firstResult.BackupPath).Length == targetPcmLength,
            "Pierwsza kopia nie zachowała pierwotnego celu.");
        Check(Math.Abs(ReadPcm(secondResult.BackupPath).Length - (targetPcmLength + clipLength)) <= 8 * 441,
            "Druga kopia nie zachowała wyniku pierwszego dopisania.");
        var resultLength = ReadPcm(target).Length;
        Check(
            Math.Abs(resultLength - (targetPcmLength + 2 * clipLength)) <= 8 * 441,
            $"Po dwóch równoległych dołączeniach jest {resultLength} bajtów dźwięku, "
            + $"a oczekiwano {targetPcmLength + 2 * clipLength}.");
    }

    private static void AppendsToOtherContainerFormat(string root, string extension, string signature)
    {
        if (!AudioClipExporter.IsFfmpegAvailable)
        {
            throw new InvalidOperationException("Próba formatów wymaga FFmpeg; bez niego wynik nie jest zaliczony.");
        }

        var source = Path.Combine(root, "źródło.wav");
        WriteSelectedTone(source);
        {
            var target = Path.Combine(root, "cel" + extension);
            EncodeTone(target, TimeSpan.FromSeconds(3), 220);
            var before = Duration(target);
            var result = Wait(AudioClipAppender.AppendAsync(
                new AudioClipAppendRequest(source, target, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)),
                null,
                CancellationToken.None));
            var head = Encoding.ASCII.GetString(File.ReadAllBytes(target).Take(12).ToArray());
            Check(
                !head.StartsWith("RIFF", StringComparison.Ordinal),
                $"Pod rozszerzeniem {extension} zapisano plik WAV.");
            if (signature.Length != 0)
            {
                Check(
                    head.Contains(signature, StringComparison.Ordinal),
                    $"Plik {extension} stracił swój format (nagłówek: {head.Trim()}).");
            }

            var after = Duration(target);
            var expected = before + TimeSpan.FromSeconds(2);
            Check(
                (after - expected).Duration() < TimeSpan.FromMilliseconds(400),
                $"Plik {extension} ma po dołączeniu {after:c}, a oczekiwano {expected:c}.");
            Check(File.Exists(result.BackupPath), $"Brak kopii zapasowej dla {extension}.");
            var firstHz=DecodedFrequency(target,0.5);var tailHz=DecodedFrequency(target,3.5);
            Console.WriteLine($"AUDIO {extension}: początek {firstHz:0.0} Hz; koniec {tailHz:0.0} Hz");
            Check(Math.Abs(firstHz-220)<20,$"{extension}: utracono dźwięk pierwotnego celu ({firstHz} Hz)");
            Check(Math.Abs(tailHz-1200)<60,$"{extension}: na końcu jest inna część źródła niż zaznaczona ({tailHz} Hz)");
            if (extension is ".mp3" or ".m4a" or ".ogg")
            {
                Check(
                    result.TargetWasReencoded && !string.IsNullOrWhiteSpace(result.ReencodeWarning),
                    $"Dołączenie do formatu {extension} nie zgłosiło ponownego kodowania.");
            }

            Console.WriteLine($"  {extension}: {before:c} + 2 s = {after:c}, ponowne kodowanie: {result.TargetWasReencoded}");
        }
    }

    /// <summary>Real duration of the clip that the shared exporter produces.</summary>
    private static TimeSpan ReferenceClipDuration(string root, string source, TimeSpan start, TimeSpan end)
    {
        var clip = Path.Combine(root, "wzorzec-" + Guid.NewGuid().ToString("N") + ".wav");
        Wait(AudioClipExporter.ExportAsync(
            new AudioClipExportRequest(source, clip, start, end, AudioClipExportFormat.Wav),
            null,
            CancellationToken.None));
        try
        {
            using var reader = new WaveFileReader(clip);
            return reader.TotalTime;
        }
        finally
        {
            File.Delete(clip);
        }
    }

    private static int FrameBytes(string formatOwner, TimeSpan duration)
    {
        using var reader = new WaveFileReader(formatOwner);
        var format = reader.WaveFormat;
        var frames = (int)Math.Round(duration.TotalSeconds * format.SampleRate);
        return frames * format.BlockAlign;
    }

    /// <summary>Zero-crossing estimate of the tone in one part of the file.</summary>
    private static double DominantFrequency(string formatOwner, byte[] pcm, int offset)
    {
        using var reader = new WaveFileReader(formatOwner);
        var format = reader.WaveFormat;
        Check(format.BitsPerSample == 16, "Test zakłada 16-bitowy plik docelowy.");
        var crossings = 0;
        var frames = 0;
        var previous = 0d;
        for (var index = offset; index + format.BlockAlign <= pcm.Length; index += format.BlockAlign)
        {
            var value = BitConverter.ToInt16(pcm, index) / 32768d;
            if (frames > 0 && ((previous < 0 && value >= 0) || (previous >= 0 && value < 0))) crossings++;
            previous = value;
            frames++;
        }
        Check(frames > format.SampleRate / 4, "Dołączony ogon jest zbyt krótki, by zmierzyć ton.");
        return crossings / 2d / (frames / (double)format.SampleRate);
    }

    private static void WriteSelectedTone(string path)
    {
        var format=new WaveFormat(44100,16,2);
        using var writer=new WaveFileWriter(path,format);
        for(var frame=0;frame<format.SampleRate*6;frame++)
        {
            var time=frame/(double)format.SampleRate;
            var hz=time<1 ? 450 : time<3.5 ? 1200 : 1900;
            var sample=(float)(0.36*Math.Sin(2*Math.PI*hz*time));
            writer.WriteSample(sample);writer.WriteSample(sample);
        }
    }

    private static double DecodedFrequency(string path, double seconds)
    {
        using var reader=WindowsMediaOutput.OpenReaderForExport(path);
        var samples=reader.ToSampleProvider();
        var toSkip=(int)Math.Round(seconds*samples.WaveFormat.SampleRate*samples.WaveFormat.Channels);
        var discard=new float[8192];
        while(toSkip>0)
        {
            var read=samples.Read(discard,0,Math.Min(discard.Length,toSkip));
            Check(read>0,"Dekoder skończył przed badanym oknem dźwięku");toSkip-=read;
        }
        var buffer=new float[samples.WaveFormat.SampleRate*samples.WaveFormat.Channels/2];
        var count=samples.Read(buffer,0,buffer.Length);
        var crossings=0;var frames=0;float previous=0;
        for(var i=0;i<count;i+=samples.WaveFormat.Channels)
        {
            var value=buffer[i];
            if(frames>0 && ((previous<0 && value>=0)||(previous>=0 && value<0)))crossings++;
            previous=value;frames++;
        }
        Check(frames>samples.WaveFormat.SampleRate/4,"Za mało zdekodowanych próbek do sprawdzenia kolejności");
        return crossings/2d/(frames/(double)samples.WaveFormat.SampleRate);
    }

    private static void WriteTone(string path, TimeSpan duration, double frequencyHz)
    {
        // Stereo 44,1 kHz 16 bit: dokładnie taki kształt zwraca dekoder eksportu,
        // więc porównanie bajt w bajt mierzy kolejność treści, a nie przeliczanie.
        var format = new WaveFormat(44100, 16, 2);
        var frames = (int)(duration.TotalSeconds * format.SampleRate);
        using var writer = new WaveFileWriter(path, format);
        for (var index = 0; index < frames; index++)
        {
            var value = (float)(0.36 * Math.Sin(2 * Math.PI * frequencyHz * index / format.SampleRate));
            writer.WriteSample(value);
            writer.WriteSample(value);
        }
    }

    private static void EncodeTone(string path, TimeSpan duration, double frequencyHz)
    {
        var wav = Path.Combine(
            Path.GetDirectoryName(path)!,
            "ton-" + Guid.NewGuid().ToString("N") + ".wav");
        WriteTone(wav, duration, frequencyHz);
        try
        {
            Wait(AudioClipAppender.EncodeRangeAsync(wav, path, TimeSpan.Zero, duration, CancellationToken.None));
        }
        finally
        {
            File.Delete(wav);
        }
    }

    private static TimeSpan Duration(string path)
    {
        var metadata = Wait(WindowsMediaOutput.TryReadMetadataAsync(path, MetadataTimeout));
        Check(metadata.Success && metadata.Duration > TimeSpan.Zero, "Nie można odczytać długości pliku " + path);
        return metadata.Duration;
    }

    private static byte[] ReadPcm(string path)
    {
        using var reader = new WaveFileReader(path);
        using var memory = new MemoryStream();
        reader.CopyTo(memory);
        return memory.ToArray();
    }

    private static string Hash(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static T Wait<T>(Task<T> task)
    {
        try
        {
            return task.GetAwaiter().GetResult();
        }
        catch (AggregateException error) when (error.InnerException is not null)
        {
            throw error.InnerException;
        }
    }

    private static void Wait(Task task)
    {
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (AggregateException error) when (error.InnerException is not null)
        {
            throw error.InnerException;
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private static void TryDeleteDirectory(string root)
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
