using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;
using NLayer.NAudioSupport;

const string FixtureBase64 = """
T2dnUwACAAAAAAAAAAC43PvDAAAAAHCxeIUBHgF2b3JiaXMAAAAAAUAfAAAAAAAAgFcAAAAAAACZAU9nZ1MAAAAAAAAAAAAAuNz7wwEAAABjSiSUCz////////////+1A3ZvcmJpcwwAAABMYXZmNjIuMy4xMDABAAAAHwAAAGVuY29kZXI9TGF2YzYyLjExLjEwMCBsaWJ2b3JiaXMBBXZvcmJpcxJCQ1YBAAABAAxSFCElGVNKYwiVUlIpBR1jUFtHHWPUOUYhZBBTiEkZpXtPKpVYSsgRUlgpRR1TTFNJlVKWKUUdYxRTSCFT1jFloXMUS4ZJCSVsTa50FkvomWOWMUYdY85aSp1j1jFFHWNSUkmhcxg6ZiVkFDpGxehifDA6laJCKL7H3lLpLYWKW4q91xpT6y2EGEtpwQhhc+211dxKasUYY4wxxsXiUyiC0JBVAAABAABABAFCQ1YBAAoAAMJQDEVRgNCQVQBABgCAABRFcRTHcRxHkiTLAkJDVgEAQAAAAgAAKI7hKJIjSZJkWZZlWZameZaouaov+64u667t6roOhIasBADIAAAYhiGH3knMkFOQSSYpVcw5CKH1DjnlFGTSUsaYYoxRzpBTDDEFMYbQKYUQ1E45pQwiCENInWTOIEs96OBi5zgQGrIiAIgCAACMQYwhxpBzDEoGIXKOScggRM45KZ2UTEoorbSWSQktldYi55yUTkompbQWUsuklNZCKwUAAAQ4AAAEWAiFhqwIAKIAABCDkFJIKcSUYk4xh5RSjinHkFLMOcWYcowx6CBUzDHIHIRIKcUYc0455iBkDCrmHIQMMgEAAAEOAAABFkKhISsCgDgBAIMkaZqlaaJoaZooeqaoqqIoqqrleabpmaaqeqKpqqaquq6pqq5seZ5peqaoqp4pqqqpqq5rqqrriqpqy6ar2rbpqrbsyrJuu7Ks256qyrapurJuqq5tu7Js664s27rkearqmabreqbpuqrr2rLqurLtmabriqor26bryrLryratyrKua6bpuqKr2q6purLtyq5tu7Ks+6br6rbqyrquyrLu27au+7KtC7vourauyq6uq7Ks67It67Zs20LJ81TVM03X9UzTdVXXtW3VdW1bM03XNV1XlkXVdWXVlXVddWVb90zTdU1XlWXTVWVZlWXddmVXl0XXtW1Vln1ddWVfl23d92VZ133TdXVblWXbV2VZ92Vd94VZt33dU1VbN11X103X1X1b131htm3fF11X11XZ1oVVlnXf1n1lmHWdMLqurqu27OuqLOu+ruvGMOu6MKy6bfyurQvDq+vGseu+rty+j2rbvvDqtjG8um4cu7Abv+37xrGpqm2brqvrpivrumzrvm/runGMrqvrqiz7uurKvm/ruvDrvi8Mo+vquirLurDasq/Lui4Mu64bw2rbwu7aunDMsi4Mt+8rx68LQ9W2heHVdaOr28ZvC8PSN3a+AACAAQcAgAATykChISsCgDgBAAYhCBVjECrGIIQQUgohpFQxBiFjDkrGHJQQSkkhlNIqxiBkjknIHJMQSmiplNBKKKWlUEpLoZTWUmotptRaDKG0FEpprZTSWmopttRSbBVjEDLnpGSOSSiltFZKaSlzTErGoKQOQiqlpNJKSa1lzknJoKPSOUippNJSSam1UEproZTWSkqxpdJKba3FGkppLaTSWkmptdRSba21WiPGIGSMQcmck1JKSamU0lrmnJQOOiqZg5JKKamVklKsmJPSQSglg4xKSaW1kkoroZTWSkqxhVJaa63VmFJLNZSSWkmpxVBKa621GlMrNYVQUgultBZKaa21VmtqLbZQQmuhpBZLKjG1FmNtrcUYSmmtpBJbKanFFluNrbVYU0s1lpJibK3V2EotOdZaa0ot1tJSjK21mFtMucVYaw0ltBZKaa2U0lpKrcXWWq2hlNZKKrGVklpsrdXYWow1lNJiKSm1kEpsrbVYW2w1ppZibLHVWFKLMcZYc0u11ZRai621WEsrNcYYa2415VIAAMCAAwBAgAlloNCQlQBAFAAAYAxjjEFoFHLMOSmNUs45JyVzDkIIKWXOQQghpc45CKW01DkHoZSUQikppRRbKCWl1losAACgwAEAIMAGTYnFAQoNWQkARAEAIMYoxRiExiClGIPQGKMUYxAqpRhzDkKlFGPOQcgYc85BKRljzkEnJYQQQimlhBBCKKWUAgAAChwAAAJs0JRYHKDQkBUBQBQAAGAMYgwxhiB0UjopEYRMSielkRJaCylllkqKJcbMWomtxNhICa2F1jJrJcbSYkatxFhiKgAA7MABAOzAQig0ZCUAkAcAQBijFGPOOWcQYsw5CCE0CDHmHIQQKsaccw5CCBVjzjkHIYTOOecghBBC55xzEEIIoYMQQgillNJBCCGEUkrpIIQQQimldBBCCKGUUgoAACpwAAAIsFFkc4KRoEJDVgIAeQAAgDFKOSclpUYpxiCkFFujFGMQUmqtYgxCSq3FWDEGIaXWYuwgpNRajLV2EFJqLcZaQ0qtxVhrziGl1mKsNdfUWoy15tx7ai3GWnPOuQAA3AUHALADG0U2JxgJKjRkJQCQBwBAIKQUY4w5h5RijDHnnENKMcaYc84pxhhzzjnnFGOMOeecc4wx55xzzjnGmHPOOeecc84556CDkDnnnHPQQeicc845CCF0zjnnHIQQCgAAKnAAAAiwUWRzgpGgQkNWAgDhAACAMZRSSimllFJKqKOUUkoppZRSAiGllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimllFJKKaWUUkoppZRSSimVUkoppZRSSimllFJKKaUAIN8KBwD/BxtnWEk6KxwNLjRkJQAQDgAAGMMYhIw5JyWlhjEIpXROSkklNYxBKKVzElJKKYPQWmqlpNJSShmElGILIZWUWgqltFZrKam1lFIoKcUaS0qppdYy5ySkklpLrbaYOQelpNZaaq3FEEJKsbXWUmuxdVJSSa211lptLaSUWmstxtZibCWlllprqcXWWkyptRZbSy3G1mJLrcXYYosxxhoLAOBucACASLBxhpWks8LR4EJDVgIAIQEABDJKOeecgxBCCCFSijHnoIMQQgghREox5pyDEEIIIYSMMecghBBCCKGUkDHmHIQQQgghhFI65yCEUEoJpZRSSucchBBCCKWUUkoJIYQQQiillFJKKSGEEEoppZRSSiklhBBCKKWUUkoppYQQQiillFJKKaWUEEIopZRSSimllBJCCKGUUkoppZRSQgillFJKKaWUUkooIYRSSimllFJKCSWUUkoppZRSSikhlFJKKaWUUkoppQAAgAMHAIAAI+gko8oibDThwgMQAAAAAgACTACBAYKCUQgChBEIAAAAAAAIAPgAAEgKgIiIaOYMDhASFBYYGhweICIkAAAAAAAAAAAAAAAABE9nZ1MABMADAAAAAAAAuNz7wwIAAAA/BbY+BTgUEhQjipUZ81O9AoBfTIZAZUAKGamKVqd3pxvYDz80TdNaaw3cSDfSdV3XdV3XdV2VNVjDYY7oiI7o7wSSlpndjVcA8FQBAAAAhBRyaT6lAJaWmX0brwBAVQAAAICQQioDNJKWmd2NVwDwWQUAAABirIh35xcKhstQWc0rAPSdGQByAGSIhp6Gv12MkxDV2lKimaT3PfLd2wU=
""";

const long LiveStreamSampleOffset = 2_256_060_119_296;
var path = Path.Combine(Path.GetTempPath(), $"amc-offset-vorbis-{Guid.NewGuid():N}.ogg");

try
{
    var bytes = Convert.FromBase64String(FixtureBase64);
    AddGranuleOffset(bytes, LiveStreamSampleOffset);
    File.WriteAllBytes(path, bytes);

    using var reader = new NormalizedVorbisWaveReader(path);
    Assert(reader.HasNormalizedTimeline, "Nie rozpoznano osi czasu fragmentu transmisji.");
    Assert(
        Math.Abs(reader.SampleOrigin - LiveStreamSampleOffset) < reader.WaveFormat.SampleRate * 2L,
        $"Nie odjęto początkowego numeru próbki: {reader.SampleOrigin}.");
    Assert(reader.TotalTime > TimeSpan.Zero && reader.TotalTime < TimeSpan.FromSeconds(1),
        $"Nieprawidłowy czas fragmentu: {reader.TotalTime}.");

    reader.CurrentTime = TimeSpan.FromTicks(reader.TotalTime.Ticks / 2);
    var buffer = new byte[16_384];
    Assert(reader.Read(buffer, 0, buffer.Length) > 0, "Przewinięty fragment nie zwrócił dźwięku.");

    reader.Position = 0;
    long totalRead = 0;
    int read;
    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
    {
        totalRead += read;
        Assert(totalRead <= reader.Length, "Czytnik przekroczył rzeczywisty koniec fragmentu.");
    }
    Assert(totalRead == reader.Length, "Czytnik nie zatrzymał się dokładnie na końcu fragmentu.");

    Console.WriteLine("OK: normalizacja osi czasu fragmentu OGG/Vorbis");

    TestGuardDoesNotBlockPositionReads();
    TestCompleteOutputChainMonitor();
    TestInvalidSamplesAreSilenced();
    TestGuardRejectsAbsurdDuration();
    TestManagedMp3Fallback();
    TestWaveMetadataAndDamagedContainers();
    TestRadioBrowserSearchMapping();
    TestRadioMp3Recording();
    foreach (var mediaPath in args)
    {
        TestFormatMetadata(mediaPath);
    }
    return 0;
}

catch (Exception exception)
{
    Console.Error.WriteLine($"BŁĄD: testy dekoderów Windows: {exception}");
    return 1;
}
finally
{
    if (File.Exists(path)) File.Delete(path);
}

static void TestGuardDoesNotBlockPositionReads()
{
    using var inner = new BlockingWaveStream();
    using var guarded = new GuardedWaveStream(inner, "blokujacy-test.wav");
    var readTask = Task.Run(() => guarded.Read(new byte[4_096], 0, 4_096));
    Assert(inner.ReadStarted.Wait(TimeSpan.FromSeconds(1)), "Testowy dekoder nie rozpoczął odczytu.");

    var stopwatch = Stopwatch.StartNew();
    _ = guarded.Position;
    stopwatch.Stop();
    Assert(stopwatch.Elapsed < TimeSpan.FromMilliseconds(100),
        "Odczyt pozycji czekał na zablokowany dekoder.");
    Assert(guarded.IsReadStalled(TimeSpan.Zero), "Nie wykryto zatrzymanego odczytu dekodera.");

    inner.AllowReadToFinish.Set();
    Assert(readTask.Wait(TimeSpan.FromSeconds(1)), "Nie zakończono testowego odczytu.");
    Console.WriteLine("OK: nadzór dekodera nie blokuje odczytu pozycji");
}

static void TestGuardRejectsAbsurdDuration()
{
    using var inner = new FixedDurationWaveStream(TimeSpan.FromDays(366));
    try
    {
        using var _ = new GuardedWaveStream(inner, "nieprawidlowa-dlugosc.wav");
        throw new InvalidOperationException("Zaakceptowano absurdalny czas trwania pliku.");
    }
    catch (InvalidDataException)
    {
        Console.WriteLine("OK: odrzucono absurdalny czas trwania dla wspólnej ścieżki formatów");
    }
}

static void TestManagedMp3Fallback()
{
    const int frameLength = 417;
    const int frameCount = 12;
    var path = Path.Combine(
        Path.GetTempPath(),
        $"amc-managed-fallback-{Guid.NewGuid():N}.mp3");
    try
    {
        byte[] frameHeader = [0xFF, 0xFB, 0x90, 0x00];
        var data = new byte[frameLength * frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            frameHeader.CopyTo(data, frame * frameLength);
        }
        File.WriteAllBytes(path, data);

        var builder = new Mp3FileReader.FrameDecompressorBuilder(
            waveFormat => new Mp3FrameDecompressor(waveFormat));
        using var decoder = new Mp3FileReaderBase(path, builder);
        using var reader = new WaveChannel32(decoder) { PadWithZeroes = false };
        using var guarded = new GuardedWaveStream(reader, path);
        Assert(
            guarded.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat
                && guarded.WaveFormat.BitsPerSample == 32,
            "Awaryjny dekoder nie został znormalizowany do bezpiecznych próbek float.");
        Assert(guarded.WaveFormat.SampleRate == 44_100, "Awaryjny dekoder podał złą częstotliwość.");
        Assert(guarded.TotalTime > TimeSpan.Zero, "Awaryjny dekoder nie podał czasu MP3.");
        var buffer = new byte[Math.Min(guarded.WaveFormat.AverageBytesPerSecond, 16_384)];
        Assert(guarded.Read(buffer, 0, buffer.Length) > 0, "Awaryjny dekoder nie zwrócił próbek.");

        var sanitizedPath = Path.Combine(
            Path.GetTempPath(),
            $"amc-sanitized-mp3-{Guid.NewGuid():N}.mp3");
        try
        {
            var unusual = new byte[37 + data.Length];
            Array.Fill<byte>(unusual, 0x55, 0, 37);
            data.CopyTo(unusual, 37);
            File.WriteAllBytes(sanitizedPath, unusual);
            var result = WindowsMediaOutput.TryReadMetadataAsync(
                    sanitizedPath,
                    TimeSpan.FromSeconds(3))
                .GetAwaiter()
                .GetResult();
            Assert(result.Success && !result.TimedOut, "Oczyszczony strumień MP3 nie został zdekodowany.");
        }
        finally
        {
            if (File.Exists(sanitizedPath)) File.Delete(sanitizedPath);
        }
        Console.WriteLine("OK: zarządzany dekoder awaryjny MP3");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void TestWaveMetadataAndDamagedContainers()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-format-resilience-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var wavePath = Path.Combine(directory, "valid.wav");
        using (var writer = new WaveFileWriter(wavePath, new WaveFormat(44_100, 16, 2)))
        {
            writer.Write(new byte[44_100 * 2 * 2 / 10]);
        }
        Assert(
            WindowsMediaOutput.TryReadMetadata(wavePath, out var duration, out var sampleRate),
            "Nie odczytano prawidłowego WAV.");
        Assert(duration > TimeSpan.Zero && sampleRate == 44_100, "WAV podał błędne parametry.");

        var damagedFiles = new Dictionary<string, byte[]>
        {
            ["cut.wav"] = "RIFF\0\0\0\0WAVE"u8.ToArray(),
            ["cut.flac"] = "fLaC\0\0\0\0"u8.ToArray(),
            ["cut.ogg"] = "OggS\0\0\0\0"u8.ToArray()
        };
        foreach (var (name, bytes) in damagedFiles)
        {
            var path = Path.Combine(directory, name);
            File.WriteAllBytes(path, bytes);
            var stopwatch = Stopwatch.StartNew();
            var result = WindowsMediaOutput.TryReadMetadataAsync(path, TimeSpan.FromSeconds(2))
                .GetAwaiter()
                .GetResult();
            stopwatch.Stop();
            Assert(!result.Success, $"Zaakceptowano ucięty kontener: {name}.");
            Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Ucięty kontener zablokował test: {name}.");
        }
        Console.WriteLine("OK: prawidłowy WAV i bezpieczne odrzucanie uciętych kontenerów");
    }
    finally
    {
        Directory.Delete(directory, true);
    }
}

static void TestRadioBrowserSearchMapping()
{
    const string response = """
        [{
          "stationuuid":"abc-123",
          "name":"Radio Testowe",
          "url":"http://example.test/original",
          "url_resolved":"https://example.test/live.aac",
          "homepage":"https://example.test",
          "country":"Polska",
          "language":"polski",
          "tags":"informacje,kultura",
          "codec":"AAC",
          "bitrate":192,
          "votes":42,
          "lastcheckok":1
        }]
        """;
    using var client = new HttpClient(new FixedJsonHandler(response));
    using var catalog = new RadioBrowserClient(client);
    var stations = catalog.SearchAsync("test").GetAwaiter().GetResult();
    Assert(stations.Count == 1, "Nie scalono powtarzającego się wyniku katalogu radia.");
    var station = stations[0];
    Assert(station.Title == "Radio Testowe" && station.Kind == AccessibleMediaController.Core.Sessions.MediaItemKind.Station,
        "Nie odwzorowano nazwy lub rodzaju stacji.");
    Assert(station.Source == "https://example.test/live.aac" && station.BitrateKbps == 192,
        "Nie wybrano rozwiązanego strumienia lub bitrate stacji.");
    Assert(station.Country == "Polska" && station.Language == "polski" && station.Codec == "AAC",
        "Nie zachowano informacji katalogowych stacji.");
    Assert(!station.IsInLibrary && !station.IsFavorite,
        "Wynik wyszukiwania nie może samoczynnie trafić do Biblioteki lub Ulubionych.");
    Console.WriteLine("OK: wyszukiwanie i mapowanie katalogu Radio Browser");
}

static void TestRadioMp3Recording()
{
    var directory = Path.Combine(
        Path.GetTempPath(),
        $"amc-radio-recording-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var mp3Path = Path.Combine(directory, "radio-test.mp3");
    try
    {
        var format = new WaveFormat(44_100, 16, 2);
        using var recorder = RadioMp3Recorder.Start(mp3Path, format);
        var pcm = new byte[format.AverageBytesPerSecond];
        for (var frame = 0; frame < format.SampleRate; frame++)
        {
            var sample = (short)(Math.Sin(2 * Math.PI * 440 * frame / format.SampleRate) * short.MaxValue * 0.1);
            var offset = frame * format.BlockAlign;
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset, 2), sample);
            BinaryPrimitives.WriteInt16LittleEndian(pcm.AsSpan(offset + 2, 2), sample);
        }
        recorder.Write(pcm, 0, pcm.Length);
        var result = recorder.Stop();

        Assert(result == mp3Path && File.Exists(mp3Path), "Nie opublikowano gotowego nagrania MP3.");
        Assert(!File.Exists(mp3Path + ".amc-partial"), "Pozostał tymczasowy plik nagrania.");
        using var reader = new MediaFoundationReader(mp3Path);
        Assert(reader.TotalTime > TimeSpan.FromMilliseconds(500), "Nagranie MP3 ma nieprawidłowy czas.");
        Assert(reader.WaveFormat.SampleRate == 44_100, "Nagranie MP3 ma nieprawidłową częstotliwość.");
        using var frameStream = File.OpenRead(mp3Path);
        var firstFrame = Mp3Frame.LoadFromStream(frameStream);
        Assert(firstFrame?.BitRate == RadioMp3Recorder.DesiredBitRate,
            $"Nagranie MP3 ma bitrate {firstFrame?.BitRate ?? 0} zamiast {RadioMp3Recorder.DesiredBitRate}.");

        var shutdownPath = Path.Combine(directory, "zamkniecie.mp3");
        using (var shutdownRecorder = RadioMp3Recorder.Start(shutdownPath, format))
        {
            shutdownRecorder.Write(pcm, 0, pcm.Length / 2);
        }
        Assert(File.Exists(shutdownPath) && !File.Exists(shutdownPath + ".amc-partial"),
            "Zamykanie programu nie zakończyło nagrania MP3.");

        var abortedPath = Path.Combine(directory, "przerwane.mp3");
        using (var abortedRecorder = RadioMp3Recorder.Start(abortedPath, format))
        {
            abortedRecorder.Write(pcm, 0, pcm.Length / 4);
            abortedRecorder.Abort();
        }
        Assert(!File.Exists(abortedPath) && !File.Exists(abortedPath + ".amc-partial"),
            "Przerwane kodowanie pozostawiło plik udający gotowe nagranie.");
        Console.WriteLine("OK: nagrywanie radia do prawidłowo zakończonego pliku MP3");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static void TestCompleteOutputChainMonitor()
{
    var inner = new BlockingSampleProvider();
    var monitor = new DecoderReadMonitorSampleProvider(inner);
    var readTask = Task.Run(() => monitor.Read(new float[1_024], 0, 1_024));
    Assert(inner.ReadStarted.Wait(TimeSpan.FromSeconds(1)), "Końcowy tor dźwięku nie rozpoczął odczytu.");
    Assert(monitor.IsReadStalled(TimeSpan.Zero), "Nie wykryto zatrzymania pełnego toru dźwięku.");
    inner.AllowReadToFinish.Set();
    Assert(readTask.Wait(TimeSpan.FromSeconds(1)), "Nie zakończono testu pełnego toru dźwięku.");
    inner.Dispose();
    Console.WriteLine("OK: nadzór obejmuje pełny tor dekodera i zmiany prędkości");
}

static void TestInvalidSamplesAreSilenced()
{
    var monitor = new DecoderReadMonitorSampleProvider(new InvalidSampleProvider());
    var buffer = new float[8];
    var read = monitor.Read(buffer, 0, buffer.Length);
    Assert(read == buffer.Length, "Nie odczytano testowych próbek.");
    Assert(buffer.All(float.IsFinite), "Nieprawidłowa próbka dotarła do urządzenia audio.");
    Assert(buffer[1] == 0f && buffer[2] == 0f, "Nie zastąpiono NaN i nieskończoności ciszą.");
    Console.WriteLine("OK: nieprawidłowe próbki są bezpiecznie zastępowane ciszą");
}

static void TestFormatMetadata(string mediaPath)
{
    Assert(File.Exists(mediaPath), $"Nie istnieje plik testowy: {mediaPath}");
    var result = WindowsMediaOutput.TryReadMetadataAsync(
            mediaPath,
            TimeSpan.FromSeconds(5))
        .GetAwaiter()
        .GetResult();
    Assert(!result.TimedOut, $"Odczyt formatu przekroczył limit: {mediaPath}");
    Assert(result.Success, $"Nie odczytano formatu: {mediaPath}");
    Assert(result.Duration > TimeSpan.Zero, $"Format nie podał czasu: {mediaPath}");
    Assert(result.SampleRateHz > 0, $"Format nie podał częstotliwości: {mediaPath}");
    Console.WriteLine($"OK: {Path.GetExtension(mediaPath).ToUpperInvariant()}, {result.Duration}, {result.SampleRateHz} Hz");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AddGranuleOffset(byte[] data, long sampleOffset)
{
    var pageOffset = 0;
    while (pageOffset <= data.Length - 27)
    {
        if (data[pageOffset] != (byte)'O'
            || data[pageOffset + 1] != (byte)'g'
            || data[pageOffset + 2] != (byte)'g'
            || data[pageOffset + 3] != (byte)'S')
        {
            throw new InvalidDataException("Nieprawidłowy nagłówek strony OGG w pliku testowym.");
        }

        var segmentCount = data[pageOffset + 26];
        var segmentTableEnd = checked(pageOffset + 27 + segmentCount);
        if (segmentTableEnd > data.Length) throw new InvalidDataException("Niepełna tablica segmentów OGG.");

        var bodyLength = 0;
        for (var index = 0; index < segmentCount; index++)
        {
            bodyLength += data[pageOffset + 27 + index];
        }
        var pageLength = checked(27 + segmentCount + bodyLength);
        if (pageOffset + pageLength > data.Length) throw new InvalidDataException("Niepełna strona OGG.");

        var granule = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(pageOffset + 6, sizeof(long)));
        if (granule > 0)
        {
            BinaryPrimitives.WriteInt64LittleEndian(
                data.AsSpan(pageOffset + 6, sizeof(long)),
                checked(granule + sampleOffset));
            data.AsSpan(pageOffset + 22, sizeof(uint)).Clear();
            BinaryPrimitives.WriteUInt32LittleEndian(
                data.AsSpan(pageOffset + 22, sizeof(uint)),
                CalculateOggChecksum(data.AsSpan(pageOffset, pageLength)));
        }

        pageOffset += pageLength;
    }

    if (pageOffset != data.Length) throw new InvalidDataException("Dodatkowe dane za ostatnią stroną OGG.");
}

static uint CalculateOggChecksum(ReadOnlySpan<byte> page)
{
    const uint polynomial = 0x04C11DB7;
    uint checksum = 0;
    foreach (var value in page)
    {
        checksum ^= (uint)value << 24;
        for (var bit = 0; bit < 8; bit++)
        {
            checksum = (checksum & 0x80000000) != 0
                ? (checksum << 1) ^ polynomial
                : checksum << 1;
        }
    }
    return checksum;
}

sealed class BlockingWaveStream : WaveStream
{
    private readonly WaveFormat _format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    private long _position;

    public ManualResetEventSlim ReadStarted { get; } = new(false);
    public ManualResetEventSlim AllowReadToFinish { get; } = new(false);
    public override WaveFormat WaveFormat => _format;
    public override long Length => _format.AverageBytesPerSecond * 60L;
    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, Length);
    }
    public override void Flush()
    {
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        ReadStarted.Set();
        if (!AllowReadToFinish.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Testowy odczyt nie został zwolniony.");
        return 0;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AllowReadToFinish.Set();
            ReadStarted.Dispose();
            AllowReadToFinish.Dispose();
        }
        base.Dispose(disposing);
    }
}

sealed class FixedJsonHandler(string json) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        RequestMessage = request
    });
}

sealed class FixedDurationWaveStream(TimeSpan duration) : WaveStream
{
    private readonly WaveFormat _format = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    private long _position;
    public override WaveFormat WaveFormat => _format;
    public override long Length => checked((long)(duration.TotalSeconds * _format.AverageBytesPerSecond));
    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, Length);
    }
    public override void Flush()
    {
    }
    public override int Read(byte[] buffer, int offset, int count) => 0;
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

sealed class BlockingSampleProvider : ISampleProvider, IDisposable
{
    public ManualResetEventSlim ReadStarted { get; } = new(false);
    public ManualResetEventSlim AllowReadToFinish { get; } = new(false);
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
    public int Read(float[] buffer, int offset, int count)
    {
        ReadStarted.Set();
        if (!AllowReadToFinish.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Testowy tor dźwięku nie został zwolniony.");
        return 0;
    }
    public void Dispose()
    {
        AllowReadToFinish.Set();
        ReadStarted.Dispose();
        AllowReadToFinish.Dispose();
    }
}

sealed class InvalidSampleProvider : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Fill(buffer, 0.25f, offset, count);
        if (count > 1) buffer[offset + 1] = float.NaN;
        if (count > 2) buffer[offset + 2] = float.PositiveInfinity;
        return count;
    }
}
