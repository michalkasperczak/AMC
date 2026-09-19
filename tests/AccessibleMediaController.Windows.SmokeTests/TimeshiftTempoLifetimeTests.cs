using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using AccessibleMediaController.Windows.Services;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.SmokeTests;

/// <summary>
/// Niezalezny pomiar TRZECH luk wskazanych w przegladzie d963d1d2 (przeglad
/// czytal starszy stan 7221c96; tutaj mierzone jest to, co jest w kodzie teraz).
/// Testy nie zmieniaja silnika - tylko go obserwuja.
///
/// 1. WSPOLBIEZNOSC: zablokowany Read kontra Dispose i SetTempo, z barierami,
///    bez czekania "na chybil trafil". Dowodem jest to, ze operacja druga NIE
///    konczy sie, dopoki Read siedzi w buforze, a po zwolnieniu bariery konczy
///    sie BEZ wyjatku. Bez wspolnego zamka Dispose zwalnia SoundTouch pod
///    trwajacym Read (ryzyko przerwania odczytu).
/// 2. POWROT DO 1,0x: po recznym ustawieniu 1,0x odczyt faktycznego tempa musi
///    byc prawdziwy OD RAZU, bez kolejnego Read, a dzwiek musi isc dalej bez
///    przerwy i bez ciszy. Osobno mierzone jest, ze 1,0x zostalo faktycznie
///    zastosowane do procesora (konsumpcja materialu wraca do 1,0 s/s), a nie
///    tylko zapisane w polu.
/// 3. DLUGIE OKNO: po rozgrzewce co najmniej 20 s wyjscia, float32 i PCM16.
///    Roznica konsumpcji miedzy 1,5x i 1,0x w TYM SAMYM oknie po rozgrzewce
///    wyklucza tlumaczenie "to tylko bufor startowy SoundTouch". Dodatkowo
///    czestotliwosc tonu nie moze sie zmieniac - to ma byc tempo, nie wysokosc.
///
/// Wszystko na danych probnych (sinusoida 440 Hz), bez sieci, bez kont, bez
/// urzadzenia audio i bez realnej stacji.
/// </summary>
public static class TimeshiftTempoLifetimeTests
{
    private const double ToneHertz = 440d;

    public static void Run()
    {
        var failures = new List<string>();

        Check(failures, "1a SetTempo czeka na aktywny Read", () => BlockedReadVersus(failures, disposeInstead: false));
        Check(failures, "1b Dispose czeka na aktywny Read", () => BlockedReadVersus(failures, disposeInstead: true));
        Check(failures, "1c stan po Dispose", () => DisposedContract(failures));
        Check(failures, "2 powrot 1,5x -> 1,0x bez kolejnego Read", () => ManualReturnToNormal(failures));
        Check(failures, "3 dlugie okno tempa po rozgrzewce", () => LongWindow(failures));

        Console.WriteLine();
        foreach (var failure in failures) Console.WriteLine("BLAD: " + failure);
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"TimeshiftTempoLifetimeTests: {failures.Count} niepowodzen (szczegoly wyzej).");
        }
        Console.WriteLine("TimeshiftTempoLifetimeTests: OK");
    }

    private static void Check(List<string> failures, string name, Action test)
    {
        Console.WriteLine();
        Console.WriteLine("== " + name);
        try { test(); }
        catch (Exception exception)
        {
            failures.Add($"{name}: wyjatek {exception.GetType().Name}: {exception.Message}");
        }
    }

    // ---------------------------------------------------------------- 1 ------

    /// <summary>
    /// Deterministyczny wyscig z barierami. Watek czytajacy zostaje ZATRZYMANY
    /// wewnatrz Read (w buforze zrodlowym, czyli pod zamkiem etapu). Dopiero
    /// wtedy startuje druga operacja. Gdy Read/Dispose/SetTempo sa serializowane,
    /// druga operacja musi CZEKAC, a po zwolnieniu bariery zakonczyc sie czysto.
    /// </summary>
    private static void BlockedReadVersus(List<string> failures, bool disposeInstead)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var buffer = new GatedSineBuffer(format) { Behind = TimeSpan.FromSeconds(600) };
        using var stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.Behind)
            ?? throw new InvalidOperationException("Etap tempa nie powstal dla float32.");

        var chunk = new byte[stage.WaveFormat.AverageBytesPerSecond / 5];
        if (disposeInstead)
        {
            // Dispose ma sie zderzyc z Read IDACYM PRZEZ SoundTouch - to ten
            // przypadek grozi zwolnieniem procesora DSP pod czytaniem.
            stage.SetTempo(1.5d);
            stage.Read(chunk, 0, chunk.Length);
            Console.WriteLine("  rozgrzewka: tempo 1,50x wpiete, SoundTouch aktywny");
        }
        else
        {
            stage.Read(chunk, 0, chunk.Length);
            Console.WriteLine("  rozgrzewka: tor 1x na wprost");
        }

        buffer.Block = true;
        Exception? readError = null;
        var readResult = -1;
        var reader = new Thread(() =>
        {
            try { readResult = stage.Read(chunk, 0, chunk.Length); }
            catch (Exception exception) { readError = exception; }
        }) { IsBackground = true, Name = "read" };
        reader.Start();

        if (!buffer.Entered.Wait(TimeSpan.FromSeconds(10)))
        {
            failures.Add("Read nie wszedl do bufora - bariera testu nie zadzialala, pomiar niewazny.");
            buffer.Release.Set();
            reader.Join(TimeSpan.FromSeconds(5));
            return;
        }

        Exception? secondError = null;
        var secondDone = new ManualResetEventSlim(false);
        var watch = Stopwatch.StartNew();
        var second = new Thread(() =>
        {
            try
            {
                if (disposeInstead) stage.Dispose();
                else stage.SetTempo(1.5d);
            }
            catch (Exception exception) { secondError = exception; }
            finally { secondDone.Set(); }
        }) { IsBackground = true, Name = disposeInstead ? "dispose" : "settempo" };
        second.Start();

        var finishedWhileReadBlocked = secondDone.Wait(TimeSpan.FromMilliseconds(750));
        var label = disposeInstead ? "Dispose" : "SetTempo";
        Console.WriteLine(finishedWhileReadBlocked
            ? $"  {label} SKONCZYL sie w trakcie zablokowanego Read (po {watch.ElapsedMilliseconds} ms)"
            : $"  {label} czeka - po 750 ms nadal nie skonczony (Read trzyma zamek)");
        if (finishedWhileReadBlocked)
        {
            failures.Add(
                $"{label} przeszedl RAZEM z aktywnym Read - operacje nie sa serializowane, "
                + "czyli SoundTouch moze byc ruszany/zwalniany pod trwajacym czytaniem.");
        }

        buffer.Release.Set();
        var secondFinished = secondDone.Wait(TimeSpan.FromSeconds(10));
        var readFinished = reader.Join(TimeSpan.FromSeconds(10));
        watch.Stop();
        Console.WriteLine(
            $"  po zwolnieniu bariery: {label} zakonczony={secondFinished}, Read zakonczony={readFinished}, "
            + $"lacznie {watch.ElapsedMilliseconds} ms, Read oddal {readResult} B");

        if (!secondFinished) failures.Add($"{label} nie zakonczyl sie po zwolnieniu Read - zakleszczenie.");
        if (!readFinished) failures.Add("Read nie zakonczyl sie po zwolnieniu bariery.");
        if (secondError is not null) failures.Add($"{label} rzucil {secondError.GetType().Name}: {secondError.Message}");
        if (readError is not null) failures.Add($"Read rzucil {readError.GetType().Name}: {readError.Message}");
        if (readResult <= 0 && !disposeInstead)
        {
            failures.Add($"Read po zwolnieniu bariery oddal {readResult} B zamiast dzwieku.");
        }
        if (!disposeInstead && Math.Abs(stage.EffectiveTempo - 1.5d) > 0.001d)
        {
            failures.Add($"Po SetTempo(1,5) odczyt podaje {stage.EffectiveTempo:0.000} - readback nieprawdziwy.");
        }
    }

    /// <summary>Po Dispose etap nie moze wybuchac ani udawac przyspieszenia.</summary>
    private static void DisposedContract(List<string> failures)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var buffer = new GatedSineBuffer(format) { Behind = TimeSpan.FromSeconds(600) };
        var stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.Behind)
            ?? throw new InvalidOperationException("Etap tempa nie powstal dla float32.");
        stage.SetTempo(1.5d);
        var chunk = new byte[stage.WaveFormat.AverageBytesPerSecond / 10];
        stage.Read(chunk, 0, chunk.Length);
        stage.Dispose();
        stage.Dispose();

        var afterRead = stage.Read(chunk, 0, chunk.Length);
        var rate = stage.EffectiveTempo;
        var threw = false;
        try { stage.SetTempo(1.25d); }
        catch (ObjectDisposedException) { threw = true; }
        Console.WriteLine(
            $"  po Dispose: Read={afterRead} B, EffectiveTempo={rate:0.000}, "
            + $"SetTempo zglasza ObjectDisposedException={threw}");
        if (afterRead != 0) failures.Add($"Read po Dispose oddal {afterRead} B zamiast 0.");
        if (Math.Abs(rate - 1d) > 0.001d) failures.Add($"Po Dispose odczyt tempa podaje {rate:0.000}.");
        if (!threw) failures.Add("SetTempo po Dispose nie zglasza ObjectDisposedException.");
    }

    // ---------------------------------------------------------------- 2 ------

    /// <summary>
    /// 1,5x -> RECZNE 1,0x. Odczyt musi byc prawdziwy natychmiast (bez kolejnego
    /// Read), dzwiek musi isc dalej bez ciszy i bez urwanego odczytu, a samo
    /// 1,0x musi byc FAKTYCZNIE w procesorze - dowodem jest konsumpcja materialu
    /// wracajaca do 1,0 s na sekunde wyjscia.
    /// </summary>
    private static void ManualReturnToNormal(List<string> failures)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var buffer = new GatedSineBuffer(format) { Behind = TimeSpan.FromSeconds(600) };
        using var stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.Behind)
            ?? throw new InvalidOperationException("Etap tempa nie powstal dla float32.");

        stage.SetTempo(1.5d);
        var fast = ReadWindow(stage, buffer, seconds: 3d);
        Console.WriteLine(
            $"  1,50x: wyjscie {fast.OutputSeconds:0.000}s, pobrano {fast.ConsumedSeconds:0.000}s, "
            + $"wspolczynnik {fast.Ratio:0.000}, RMS {fast.Rms:0.0000}, ton {fast.Hertz:0.0} Hz, "
            + $"EffectiveTempo={stage.EffectiveTempo:0.000}");
        if (fast.Ratio < 1.4d) failures.Add($"Kontrola dodatnia: 1,5x zjada tylko {fast.Ratio:0.000} s/s.");

        // Tu nie ma zadnego Read. Odczyt musi juz mowic prawde.
        stage.SetTempo(1.0d);
        var readbackEffective = stage.EffectiveTempo;
        var readbackRequested = stage.RequestedTempo;
        Console.WriteLine(
            $"  natychmiast po SetTempo(1,00), BEZ kolejnego Read: EffectiveTempo={readbackEffective:0.000}, "
            + $"RequestedTempo={readbackRequested:0.000}");
        if (Math.Abs(readbackEffective - 1d) > 0.001d)
        {
            failures.Add(
                $"Po recznym 1,0x odczyt faktycznego tempa podaje {readbackEffective:0.000} bez kolejnego Read - "
                + "uzytkownik i NVDA dostaja nieprawde do nastepnej porcji dzwieku.");
        }
        if (Math.Abs(readbackRequested - 1d) > 0.001d)
        {
            failures.Add($"Po recznym 1,0x zadane tempo podaje {readbackRequested:0.000}.");
        }

        var normal = ReadWindow(stage, buffer, seconds: 4d);
        Console.WriteLine(
            $"  po 1,00x: wyjscie {normal.OutputSeconds:0.000}s, pobrano {normal.ConsumedSeconds:0.000}s, "
            + $"wspolczynnik {normal.Ratio:0.000}, cisza {normal.SilentPercent:0.00}%, RMS {normal.Rms:0.0000}, "
            + $"ton {normal.Hertz:0.0} Hz, pierwsza porcja: cisza {normal.FirstChunkSilentPercent:0.00}%, "
            + $"RMS {normal.FirstChunkRms:0.0000}, krotkich odczytow {normal.ShortReads}");

        if (Math.Abs(normal.Ratio - 1d) > 0.06d)
        {
            failures.Add(
                $"Po recznym 1,0x etap pobiera {normal.Ratio:0.000} s materialu na sekunde wyjscia - "
                + "1,0x nie zostalo zastosowane do procesora, tylko zapisane w polu.");
        }
        if (normal.OutputSeconds < 3.9d) failures.Add($"Zywe wyjscie urwalo sie na {normal.OutputSeconds:0.000}s.");
        if (normal.ShortReads > 0) failures.Add($"{normal.ShortReads} odczytow oddalo mniej, niz proszono - przerwa w dzwieku.");
        if (normal.SilentPercent > 1d) failures.Add($"Po powrocie do 1,0x na wyjsciu jest {normal.SilentPercent:0.00}% ciszy.");
        if (normal.FirstChunkSilentPercent > 1d)
        {
            failures.Add($"Pierwsza porcja po zmianie tempa ma {normal.FirstChunkSilentPercent:0.00}% ciszy - slyszalna dziura.");
        }
        if (normal.FirstChunkRms < fast.Rms * 0.5d || normal.FirstChunkRms > fast.Rms * 1.5d)
        {
            failures.Add(
                $"Poziom dzwieku zaraz po zmianie tempa ({normal.FirstChunkRms:0.0000}) odstaje od poziomu "
                + $"przed zmiana ({fast.Rms:0.0000}) - wyjscie nie jest ciagle.");
        }
        if (Math.Abs(normal.Hertz - ToneHertz) > 20d)
        {
            failures.Add($"Po powrocie do 1,0x ton ma {normal.Hertz:0.0} Hz zamiast {ToneHertz:0} Hz.");
        }

        // Ponowne przyspieszenie po recznym 1,0x musi dzialac (1x nie jest
        // stanem koncowym ustawionym na stale).
        stage.SetTempo(1.5d);
        var again = ReadWindow(stage, buffer, seconds: 2d);
        Console.WriteLine($"  ponowne 1,50x po recznym 1,00x: wspolczynnik {again.Ratio:0.000}");
        if (again.Ratio < 1.4d) failures.Add($"Po recznym 1,0x ponowne przyspieszenie daje {again.Ratio:0.000} s/s.");
    }

    // ---------------------------------------------------------------- 3 ------

    /// <summary>
    /// Dlugie okno po rozgrzewce. Bufor startowy SoundTouch jest jednorazowy,
    /// wiec na 20 s wyjscia rozcienczylby sie do szumu pomiarowego; roznica
    /// konsumpcji miedzy 1,5x i 1,0x w tym samym oknie po rozgrzewce nie da sie
    /// nim wytlumaczyc.
    /// </summary>
    private static void LongWindow(List<string> failures)
    {
        const double window = 20d;
        const double warmup = 3d;
        foreach (var (name, format) in new (string, WaveFormat)[]
        {
            ("float32", WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)),
            ("PCM16", new WaveFormat(44100, 16, 2))
        })
        {
            var results = new Dictionary<double, Window>();
            foreach (var tempo in new[] { 1.0d, 1.25d, 1.5d, 0.75d })
            {
                var buffer = new GatedSineBuffer(format) { Behind = TimeSpan.FromSeconds(3600) };
                using var stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.Behind)
                    ?? throw new InvalidOperationException($"Etap tempa nie powstal dla {format}.");
                stage.SetTempo(tempo);
                ReadWindow(stage, buffer, warmup);              // rozgrzewka - poza pomiarem
                var measured = ReadWindow(stage, buffer, window); // dopiero to sie liczy
                results[tempo] = measured;
                Console.WriteLine(
                    $"  {name} tempo={tempo:0.00} (po {warmup:0}s rozgrzewki): wyjscie {measured.OutputSeconds:0.000}s, "
                    + $"pobrano {measured.ConsumedSeconds:0.000}s, wspolczynnik {measured.Ratio:0.000}, "
                    + $"cisza {measured.SilentPercent:0.00}%, RMS {measured.Rms:0.0000}, ton {measured.Hertz:0.0} Hz, "
                    + $"EffectiveTempo={stage.EffectiveTempo:0.000}");

                if (measured.OutputSeconds < window - 0.05d)
                {
                    failures.Add($"{name} tempo {tempo:0.00}: okno pomiaru ma tylko {measured.OutputSeconds:0.000}s.");
                }
                if (Math.Abs(measured.Ratio - tempo) > 0.04d)
                {
                    failures.Add(
                        $"{name} tempo {tempo:0.00}: w dlugim oknie po rozgrzewce konsumpcja to "
                        + $"{measured.Ratio:0.000} s/s - tempo nie utrzymuje sie w czasie.");
                }
                if (measured.SilentPercent > 1d)
                {
                    failures.Add($"{name} tempo {tempo:0.00}: {measured.SilentPercent:0.00}% ciszy w dlugim oknie.");
                }
                if (Math.Abs(measured.Hertz - ToneHertz) > 20d)
                {
                    failures.Add(
                        $"{name} tempo {tempo:0.00}: ton ma {measured.Hertz:0.0} Hz zamiast {ToneHertz:0} Hz - "
                        + "zmieniona jest wysokosc, nie samo tempo.");
                }
                if (measured.ShortReads > 0)
                {
                    failures.Add($"{name} tempo {tempo:0.00}: {measured.ShortReads} krotkich odczytow w dlugim oknie.");
                }
            }

            // Roznica konsumpcji w IDENTYCZNYM oknie po rozgrzewce.
            var fastMinusNormal = results[1.5d].ConsumedSeconds - results[1.0d].ConsumedSeconds;
            var normalMinusSlow = results[1.0d].ConsumedSeconds - results[0.75d].ConsumedSeconds;
            Console.WriteLine(
                $"  {name} roznica konsumpcji w tym samym oknie {window:0}s: 1,5x-1,0x = {fastMinusNormal:0.000}s "
                + $"(oczekiwane ~{window * 0.5:0.0}s), 1,0x-0,75x = {normalMinusSlow:0.000}s "
                + $"(oczekiwane ~{window * 0.25:0.0}s)");
            if (fastMinusNormal < window * 0.45d)
            {
                failures.Add(
                    $"{name}: 1,5x zjada tylko {fastMinusNormal:0.000}s materialu wiecej niz 1,0x na {window:0}s "
                    + $"wyjscia (potrzebne ~{window * 0.5:0.0}s) - roznica mieszcząca sie w buforze startowym.");
            }
            if (normalMinusSlow < window * 0.2d)
            {
                failures.Add(
                    $"{name}: 0,75x oszczedza tylko {normalMinusSlow:0.000}s materialu na {window:0}s wyjscia.");
            }
        }
    }

    // ------------------------------------------------------------ pomiar -----

    /// <summary>
    /// Czyta zadany czas WYJSCIA i liczy przy tym: ile materialu zniknelo z
    /// bufora, ile ciszy, jaki poziom, jaka czestotliwosc i czy ktorys odczyt
    /// byl krotszy niz zamowiony (dziura w zywym dzwieku).
    /// </summary>
    private static Window ReadWindow(TimeshiftTempoStage stage, GatedSineBuffer buffer, double seconds)
    {
        var outputFormat = stage.WaveFormat;
        var wanted = (long)(outputFormat.AverageBytesPerSecond * seconds);
        wanted -= wanted % outputFormat.BlockAlign;
        var chunkSize = outputFormat.AverageBytesPerSecond / 10;
        chunkSize -= chunkSize % outputFormat.BlockAlign;
        var chunk = new byte[chunkSize];

        var consumedBefore = buffer.ReadPosition;
        long produced = 0;
        long samples = 0;
        long silent = 0;
        double squareSum = 0;
        var shortReads = 0;
        var crossings = 0;
        long frames = 0;
        float previous = 0;
        var havePrevious = false;
        double firstChunkSquareSum = 0;
        long firstChunkSamples = 0;
        long firstChunkSilent = 0;

        while (produced < wanted)
        {
            var ask = (int)Math.Min(chunk.Length, wanted - produced);
            var read = stage.Read(chunk, 0, ask);
            if (read <= 0) break;
            if (read < ask) shortReads++;
            var isFirst = produced == 0;
            for (var index = 0; index + 4 <= read; index += 4)
            {
                var value = BitConverter.ToSingle(chunk, index);
                squareSum += (double)value * value;
                samples++;
                if (Math.Abs(value) < 1e-6) silent++;
                if (isFirst)
                {
                    firstChunkSquareSum += (double)value * value;
                    firstChunkSamples++;
                    if (Math.Abs(value) < 1e-6) firstChunkSilent++;
                }
                if (index % outputFormat.BlockAlign != 0) continue; // tylko kanal 0
                if (havePrevious && ((previous < 0 && value >= 0) || (previous >= 0 && value < 0))) crossings++;
                previous = value;
                havePrevious = true;
                frames++;
            }
            produced += read;
        }

        var consumed = (double)(buffer.ReadPosition - consumedBefore) / buffer.WaveFormat.AverageBytesPerSecond;
        return new Window(
            (double)produced / outputFormat.AverageBytesPerSecond,
            consumed,
            samples == 0 ? 0 : Math.Sqrt(squareSum / samples),
            samples == 0 ? 100 : 100d * silent / samples,
            frames < 2 ? 0 : crossings * outputFormat.SampleRate / (2d * (frames - 1)),
            shortReads,
            firstChunkSamples == 0 ? 0 : Math.Sqrt(firstChunkSquareSum / firstChunkSamples),
            firstChunkSamples == 0 ? 100 : 100d * firstChunkSilent / firstChunkSamples);
    }

    private readonly record struct Window(
        double OutputSeconds,
        double ConsumedSeconds,
        double Rms,
        double SilentPercent,
        double Hertz,
        int ShortReads,
        double FirstChunkRms,
        double FirstChunkSilentPercent)
    {
        public double Ratio => OutputSeconds > 0 ? ConsumedSeconds / OutputSeconds : 0;
    }

    /// <summary>
    /// Bufor probny: niewyczerpalna sinusoida 440 Hz o ciaglej fazie (sklejenia
    /// nie udaja ciszy ani trzaskow), z opcjonalna BARIERA w Read. Bariera jest
    /// calym sednem testu 1: pozwala zatrzymac czytanie DOKLADNIE w chwili, gdy
    /// etap trzyma swoj zamek.
    /// </summary>
    private sealed class GatedSineBuffer : IWaveProvider
    {
        private readonly int _blockAlign;
        private long _frame;

        public GatedSineBuffer(WaveFormat format)
        {
            WaveFormat = format;
            _blockAlign = Math.Max(1, format.BlockAlign);
        }

        public WaveFormat WaveFormat { get; }

        /// <summary>Ile materialu zostalo do czola transmisji (sterowane w tescie).</summary>
        public TimeSpan Behind { get; set; } = TimeSpan.FromSeconds(600);

        public long ReadPosition { get; private set; }

        public bool Block { get; set; }

        public ManualResetEventSlim Entered { get; } = new(false);

        /// <summary>
        /// MUSI startowac NIEZAPALONE. Zapalone zdarzenie przepuszczaloby Read
        /// od razu, bariera by nie trzymala zamka i caly test 1 mierzylby
        /// nieblokowany przebieg - czyli pokazywalby "brak serializacji" takze
        /// dla poprawnego kodu.
        /// </summary>
        public ManualResetEventSlim Release { get; } = new(false);

        public int Read(byte[] destination, int offset, int count)
        {
            count -= count % _blockAlign;
            if (count <= 0) return 0;
            if (Block)
            {
                Entered.Set();
                Release.Wait();
            }
            var frames = count / _blockAlign;
            var bytesPerSample = WaveFormat.BitsPerSample / 8;
            for (var frame = 0; frame < frames; frame++, _frame++)
            {
                var value = (float)(0.5 * Math.Sin(2 * Math.PI * ToneHertz * _frame / WaveFormat.SampleRate));
                for (var channel = 0; channel < WaveFormat.Channels; channel++)
                {
                    var at = offset + (frame * _blockAlign) + (channel * bytesPerSample);
                    if (WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                    {
                        BitConverter.TryWriteBytes(destination.AsSpan(at, 4), value);
                    }
                    else
                    {
                        BitConverter.TryWriteBytes(destination.AsSpan(at, 2), (short)(value * short.MaxValue));
                    }
                }
            }
            ReadPosition += count;
            return count;
        }
    }
}
