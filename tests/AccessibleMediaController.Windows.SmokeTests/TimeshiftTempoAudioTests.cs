using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.Windows.SmokeTests;

/// <summary>
/// Test ZACHOWANIA DZWIEKU regulacji tempa w buforze transmisji (TimeShift).
///
/// Poprzednie testy tej funkcji sprawdzaly TEKST pliku zrodlowego
/// ("SupportsPlaybackRate => true", "SoundTouchWaveStream") - przechodzily
/// takze wtedy, gdy tempo nie dzialalo ani na jednej stacji PCM 16-bit.
/// Tutaj mierzy sie RZECZYWISTA KONSUMPCJE MATERIALU z bufora wzgledem
/// wyprodukowanego dzwieku, na danych probnych (syntetyczna sinusoida),
/// bez sieci, bez kont i bez urzadzenia audio.
///
/// Kryterium: przy tempie 1,5x etap musi pobrac z bufora ~1,5 s materialu na
/// kazda sekunde wyjscia; przy 0,75x ~0,75 s. Samo pole Tempo nie jest dowodem.
/// </summary>
public static class TimeshiftTempoAudioTests
{
    private const double SampleRate = 44100;

    public static int Run()
    {
        var failures = new List<string>();
        foreach (var (name, format) in new (string, WaveFormat)[]
        {
            ("float32", WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)),
            ("PCM16", new WaveFormat(44100, 16, 2))
        })
        {
            Check(failures, name, format);
        }
        CheckNeutralPathDoesNotTouchSoundTouch(failures);
        CheckSmallReserveFallsBackToLive(failures);
        CheckUnsupportedFormatDoesNotBlockPlayback(failures);
        ShowOldChainForReference();

        foreach (var failure in failures) Console.WriteLine("BLAD: " + failure);
        Console.WriteLine(failures.Count == 0
            ? "TimeshiftTempoAudioTests: OK"
            : $"TimeshiftTempoAudioTests: {failures.Count} niepowodzen");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Check(List<string> failures, string name, WaveFormat format)
    {
        // PCM 16-bit z dekoderow systemowych MUSI dostac regulacje tempa - to
        // wlasnie ta grupa stacji jej wczesniej nie miala.
        using (var probe = TimeshiftTempoStage.TryCreate(
            new FakeTimeshiftBuffer(format, 5),
            () => TimeSpan.Zero))
        {
            if (probe is null)
            {
                failures.Add(
                    $"{name}: etap tempa w ogole nie powstal dla {format} - "
                    + "ta grupa stacji nie ma regulacji predkosci.");
                Console.WriteLine($"{name}: etap tempa NIE POWSTAL dla {format}.");
                return;
            }
        }

        // Duzy zapas w buforze = sluchanie z opoznieniem, jest co przyspieszac.
        foreach (var tempo in new[] { 1.00d, 1.25d, 1.50d, 0.75d })
        {
            var measured = Measure(format, reserveSeconds: 60, tempo: tempo, outputSeconds: 4);
            Console.WriteLine(
                $"{name} tempo={tempo:0.00}: wyjscie {measured.OutputSeconds:0.000}s, "
                + $"pobrano z bufora {measured.ConsumedSeconds:0.000}s, "
                + $"wspolczynnik {measured.Ratio:0.000}, cisza {measured.SilentPercent:0.0}%, "
                + $"RMS {measured.Rms:0.0000}, EffectiveTempo={measured.EffectiveTempo:0.00}");

            if (Math.Abs(measured.Ratio - tempo) > 0.12d)
            {
                failures.Add(
                    $"{name} tempo {tempo:0.00}: bufor oddal {measured.Ratio:0.000} s materialu "
                    + "na sekunde dzwieku - regulacja tempa nie dziala na dzwieku.");
            }
            if (measured.SilentPercent > 5d)
            {
                failures.Add($"{name} tempo {tempo:0.00}: {measured.SilentPercent:0.0}% ciszy na wyjsciu.");
            }
            if (Math.Abs(measured.EffectiveTempo - tempo) > 0.01d)
            {
                failures.Add(
                    $"{name} tempo {tempo:0.00}: odczyt faktycznego tempa podaje "
                    + $"{measured.EffectiveTempo:0.00}.");
            }
        }
    }

    /// <summary>
    /// Zwykle sluchanie 1x nie moze zalezec od dodatku: dopoki uzytkownik nie
    /// ruszyl tempa, dzwiek musi wychodzic bajt w bajt taki jak w buforze.
    /// </summary>
    private static void CheckNeutralPathDoesNotTouchSoundTouch(List<string> failures)
    {
        foreach (var format in new[] { WaveFormat.CreateIeeeFloatWaveFormat(44100, 2), new WaveFormat(44100, 16, 2) })
        {
            var buffer = new FakeTimeshiftBuffer(format, 10);
            var control = new FakeTimeshiftBuffer(format, 10);
            var source = Sine(format, 5);
            buffer.Write(source, 0, source.Length);
            control.Write(source, 0, source.Length);
            using var stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.BehindLive)
                ?? throw new InvalidOperationException("Etap tempa nie powstal dla obslugiwanego formatu.");
            var output = new byte[stage.WaveFormat.AverageBytesPerSecond * 2];
            var read = ReadFully(stage, output, output.Length);
            var expected = new float[output.Length / sizeof(float)];
            var expectedCount = control.ToSampleProvider().Read(expected, 0, expected.Length);
            var expectedBytes = new byte[expected.Length * sizeof(float)];
            Buffer.BlockCopy(expected, 0, expectedBytes, 0, expectedBytes.Length);
            var identical = read == output.Length && expectedCount == expected.Length
                && output.AsSpan(0, read).SequenceEqual(expectedBytes.AsSpan(0, read));
            Console.WriteLine($"1x {format.Encoding}/{format.BitsPerSample}: {(identical ? "probki identyczne z torem bez tempa" : "dzwiek ZMIENIONY")}");
            if (!identical) failures.Add("Zwykle 1x zmienia probki wzgledem starej konwersji NAudio: " + format);
        }
    }

    /// <summary>
    /// Maly zapas: przyspieszenie zjada wiecej, niz bufor ma. Etap ma wtedy
    /// wrocic do 1,0x zamiast produkowac cisze - tak dochodzi sie do live.
    /// </summary>
    private static void CheckSmallReserveFallsBackToLive(List<string> failures)
    {
        // Zapas 1,0 s, wyjscie 0,8 s: material WYSTARCZA na normalne tempo,
        // ale nie na 1,5x. Przy zadaniu 2 s wyjscia cisza bylaby nieunikniona
        // niezaleznie od poprawki - to nie byloby rozstrzygajace kryterium.
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var measured = Measure(format, reserveSeconds: 1.0, tempo: 1.50, outputSeconds: 0.8);
        Console.WriteLine(
            $"maly zapas 1,0s przy zadanym 1,50x: wspolczynnik {measured.Ratio:0.000}, "
            + $"cisza {measured.SilentPercent:0.0}%, EffectiveTempo={measured.EffectiveTempo:0.00}");
        if (measured.SilentPercent > 5d)
        {
            failures.Add(
                $"Przy malym zapasie przyspieszenie produkuje {measured.SilentPercent:0.0}% ciszy "
                + "zamiast wrocic do normalnego tempa.");
        }
        if (measured.EffectiveTempo > 1.01d)
        {
            failures.Add("Po wyczerpaniu zapasu faktyczne tempo nadal deklaruje przyspieszenie.");
        }
    }

    /// <summary>Regresja 375: brak wsparcia formatu NIE MOZE blokowac sluchania.</summary>
    private static void CheckUnsupportedFormatDoesNotBlockPlayback(List<string> failures)
    {
        var format = new WaveFormat(44100, 8, 1);
        var buffer = new FakeTimeshiftBuffer(format, 30);
        TimeshiftTempoStage? stage = null;
        try
        {
            stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.BehindLive);
        }
        catch (Exception exception)
        {
            failures.Add($"Nieobslugiwany format rzucil {exception.GetType().Name} zamiast zwrocic null.");
        }
        Console.WriteLine($"PCM 8-bit: etap tempa = {(stage is null ? "null (stacja gra bez tempa)" : "utworzony")}");
        if (stage is not null)
        {
            failures.Add("Nieobslugiwany format dostal etap tempa - grozi to regresja 375.");
            stage.Dispose();
        }
    }

    /// <summary>
    /// RED w tym samym przebiegu: tak zachowywal sie tor SPRZED poprawki
    /// (SoundTouchWaveStream wpiety wprost na bufor). Nie jest asercja - sluzy
    /// za punkt odniesienia w wyniku testu.
    /// </summary>
    private static void ShowOldChainForReference()
    {
        var pcm16 = new WaveFormat(44100, 16, 2);
        try
        {
            var buffer = new FakeTimeshiftBuffer(pcm16, 30);
            using var legacy = new SoundTouch.Net.NAudioSupport.SoundTouchWaveStream(
                new PassThroughWaveStream(buffer));
            Console.WriteLine("ODNIESIENIE (stary tor) PCM16: SoundTouch przyjal format - nieoczekiwane.");
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"ODNIESIENIE (stary tor) PCM16: {exception.GetType().Name}: {exception.Message} "
                + "-> przed poprawka stacje PCM16 nie mialy regulacji tempa.");
        }

        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var reserve = new FakeTimeshiftBuffer(format, 180);
        var material = Sine(format, 1.0);
        reserve.Write(material, 0, material.Length);
        using var old = new SoundTouch.Net.NAudioSupport.SoundTouchWaveStream(
            new PassThroughWaveStream(reserve))
        {
            Tempo = 1.5d
        };
        var wanted = (int)(format.AverageBytesPerSecond * 0.8);
        wanted -= wanted % format.BlockAlign;
        var chunk = new byte[wanted];
        var produced = ReadFully(old, chunk, wanted);
        long silent = 0;
        long samples = 0;
        for (var index = 0; index + 4 <= produced; index += 4)
        {
            if (Math.Abs(BitConverter.ToSingle(chunk, index)) < 1e-6) silent++;
            samples++;
        }
        Console.WriteLine(
            $"ODNIESIENIE (stary tor) float32, zapas 1,0s, tempo 1,50x: cisza "
            + $"{(samples == 0 ? 100 : 100d * silent / samples):0.0}%, pobrano "
            + $"{(double)reserve.ReadPosition / format.AverageBytesPerSecond:0.000}s.");
    }

    private sealed class PassThroughWaveStream(IWaveProvider source) : WaveStream
    {
        public override WaveFormat WaveFormat => source.WaveFormat;
        public override long Length => long.MaxValue;
        public override long Position { get; set; }
        public override int Read(byte[] destination, int offset, int count)
        {
            var read = source.Read(destination, offset, count);
            Position += read;
            return read;
        }
    }

    private static Measurement Measure(
        WaveFormat format,
        double reserveSeconds,
        double tempo,
        double outputSeconds)
    {
        var buffer = new FakeTimeshiftBuffer(format, 180);
        var material = Sine(format, Math.Max(reserveSeconds, 0.001));
        if (reserveSeconds > 0) buffer.Write(material, 0, material.Length);
        using var stage = TimeshiftTempoStage.TryCreate(buffer, () => buffer.BehindLive)
            ?? throw new InvalidOperationException($"Etap tempa nie powstal dla {format}.");
        stage.SetTempo(tempo);

        var outputFormat = stage.WaveFormat;
        var wanted = (long)(outputFormat.AverageBytesPerSecond * outputSeconds);
        wanted -= wanted % outputFormat.BlockAlign;
        var chunk = new byte[outputFormat.AverageBytesPerSecond / 10];
        long produced = 0;
        double squareSum = 0;
        long samples = 0;
        long silent = 0;
        while (produced < wanted)
        {
            var read = stage.Read(chunk, 0, (int)Math.Min(chunk.Length, wanted - produced));
            if (read <= 0) break;
            for (var index = 0; index + 4 <= read; index += 4)
            {
                var value = BitConverter.ToSingle(chunk, index);
                squareSum += value * value;
                samples++;
                if (Math.Abs(value) < 1e-6) silent++;
            }
            produced += read;
        }
        return new Measurement(
            (double)produced / outputFormat.AverageBytesPerSecond,
            (double)buffer.ReadPosition / format.AverageBytesPerSecond,
            samples == 0 ? 0 : Math.Sqrt(squareSum / samples),
            samples == 0 ? 100 : 100d * silent / samples,
            stage.EffectiveTempo);
    }

    private static int ReadFully(IWaveProvider provider, byte[] destination, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = provider.Read(destination, total, count - total);
            if (read <= 0) break;
            total += read;
        }
        return total;
    }

    private static byte[] Sine(WaveFormat format, double seconds, double hertz = 440)
    {
        var frames = (int)(format.SampleRate * seconds);
        var bytes = new byte[frames * format.BlockAlign];
        var bytesPerSample = format.BitsPerSample / 8;
        for (var frame = 0; frame < frames; frame++)
        {
            var value = (float)(0.5 * Math.Sin(2 * Math.PI * hertz * frame / format.SampleRate));
            for (var channel = 0; channel < format.Channels; channel++)
            {
                var offset = (frame * format.BlockAlign) + (channel * bytesPerSample);
                if (format.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    BitConverter.TryWriteBytes(bytes.AsSpan(offset, 4), value);
                }
                else if (bytesPerSample == 2)
                {
                    BitConverter.TryWriteBytes(bytes.AsSpan(offset, 2), (short)(value * short.MaxValue));
                }
                else
                {
                    bytes[offset] = (byte)(128 + (value * 120));
                }
            }
        }
        return bytes;
    }

    private readonly record struct Measurement(
        double OutputSeconds,
        double ConsumedSeconds,
        double Rms,
        double SilentPercent,
        double EffectiveTempo)
    {
        public double Ratio => OutputSeconds > 0 ? ConsumedSeconds / OutputSeconds : 0;
    }

    /// <summary>
    /// Ta sama semantyka co RadioTimeshiftWaveProvider: Read ZAWSZE zwraca
    /// zadana liczbe bajtow, brak materialu dopelnia cisza. To wlasnie dlatego
    /// przyspieszanie bez zapasu produkowalo cisze zamiast dzwieku.
    /// </summary>
    private sealed class FakeTimeshiftBuffer : IWaveProvider
    {
        private readonly byte[] _ring;
        private readonly int _blockAlign;
        private long _written;
        private long _read;

        public FakeTimeshiftBuffer(WaveFormat format, int seconds)
        {
            WaveFormat = format;
            _blockAlign = Math.Max(1, format.BlockAlign);
            var capacity = (long)format.AverageBytesPerSecond * seconds;
            capacity -= capacity % _blockAlign;
            _ring = new byte[capacity];
        }

        public WaveFormat WaveFormat { get; }

        public long ReadPosition => _read;

        public TimeSpan BehindLive => TimeSpan.FromSeconds(
            (double)Math.Max(0, _written - _read) / WaveFormat.AverageBytesPerSecond);

        public void Write(byte[] source, int offset, int count)
        {
            count -= count % _blockAlign;
            if (count <= 0) return;
            var index = _written % _ring.Length;
            var first = (int)Math.Min(count, _ring.Length - index);
            Buffer.BlockCopy(source, offset, _ring, (int)index, first);
            if (first < count) Buffer.BlockCopy(source, offset + first, _ring, 0, count - first);
            _written += count;
        }

        public int Read(byte[] destination, int offset, int count)
        {
            count -= count % _blockAlign;
            if (count <= 0) return 0;
            var available = (int)Math.Min(count, Math.Max(0, _written - _read));
            available -= available % _blockAlign;
            if (available > 0)
            {
                var index = _read % _ring.Length;
                var first = (int)Math.Min(available, _ring.Length - index);
                Buffer.BlockCopy(_ring, (int)index, destination, offset, first);
                if (first < available)
                {
                    Buffer.BlockCopy(_ring, 0, destination, offset + first, available - first);
                }
                _read += available;
            }
            if (available < count) Array.Clear(destination, offset + available, count - available);
            return count;
        }
    }
}
