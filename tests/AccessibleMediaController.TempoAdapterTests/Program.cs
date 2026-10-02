using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.Wave;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows.Services;

namespace AccessibleMediaController.TempoAdapterTests;

/// <summary>
/// Pomiar FAKTYCZNEGO toru C# -> natywne silniki tempa na WSL/Linuksie.
/// Fixture jest syntetyczny i jawny (rampa numerow ramek), zeby kazda probka
/// na wyjsciu dala sie przypisac do miejsca w materiale zrodlowym. Nie ma tu
/// odsluchu ani oceny jakosci - tylko dlugosc, ciaglosc i jawny status.
/// </summary>
internal static class Program
{
    private const int SampleRate = 48000;
    private static int _checks;
    private static readonly List<string> Failures = [];

    private static int Main()
    {
        ResolveNativeLibrary();
        Console.WriteLine($"ABI natywny dostepny: {AmcTempoNativeLibrary.IsAvailable}; " +
                          $"powod: {AmcTempoNativeLibrary.UnavailableReason ?? "(brak)"}");

        RunTest("T1 tempo 2x faktycznie skraca material (float32, mowa)", Test1_TempoActuallyChangesLength);
        RunTest("T2 zrodlo PCM16 trafia do natywnego silnika, nie w cichy fallback", Test2_Pcm16NotSilentFallback);
        RunTest("T3 przelaczenia 2x -> 1x -> 2x bez starego bufora i bez gubienia materialu", Test3_TempoSwitchNoStaleNoLoss);

        Console.WriteLine();
        Console.WriteLine($"Sprawdzen: {_checks}, niezgodnosci: {Failures.Count}");
        foreach (var failure in Failures) Console.WriteLine("  NIEZGODNOSC: " + failure);
        return Failures.Count == 0 ? 0 : 1;
    }

    // ---------- T1 ----------

    private static void Test1_TempoActuallyChangesLength()
    {
        const int frames = SampleRate * 4;
        var reader = new RampWaveStream(frames, 1, pcm16: false);
        var stream = PlaybackTempoStream.Create(reader, PlaybackTempoAlgorithm.Speech, out var fallbackReason);
        using var disposable = stream;
        Check("silnik jest natywny, nie SoundTouch",
            stream.EngineName.Contains("Speedy", StringComparison.Ordinal),
            $"EngineName={stream.EngineName}, fallbackReason={fallbackReason ?? "(brak)"}");

        stream.Tempo = 2d;
        var produced = DrainFrames(stream, out _);
        var ratio = (double)frames / Math.Max(1, produced);
        Check("tempo 2,0x daje ~polowe ramek wyjscia",
            ratio is > 1.6 and < 2.5,
            $"wejscie {frames} ramek, wyjscie {produced} ramek, iloraz {ratio:F3}");
    }

    // ---------- T2 ----------

    private static void Test2_Pcm16NotSilentFallback()
    {
        const int frames = SampleRate * 2;
        var reader = new RampWaveStream(frames, 2, pcm16: true);
        var stream = PlaybackTempoStream.Create(reader, PlaybackTempoAlgorithm.Music, out var fallbackReason);
        using var disposable = stream;
        Check("zrodlo 16-bit PCM nie wymusza powrotu do SoundTouch",
            !stream.EngineName.Equals("SoundTouch", StringComparison.Ordinal),
            $"EngineName={stream.EngineName}, fallbackReason={fallbackReason ?? "(brak)"}");
        Check("przy udanym wyborze nie ma powodu powrotu",
            stream.EngineName.Equals("SoundTouch", StringComparison.Ordinal) || fallbackReason is null,
            $"fallbackReason={fallbackReason ?? "(brak)"}");

        stream.Tempo = 1.5d;
        var produced = DrainFrames(stream, out var maxSourceIndex);
        Check("etap oddal jakikolwiek dzwiek z PCM16", produced > 0, $"wyjscie {produced} ramek");
        Check("material siega konca zrodla",
            maxSourceIndex > frames * 0.8,
            $"najdalsza rozpoznana ramka zrodla {maxSourceIndex:F0} z {frames}");
    }

    // ---------- T3 ----------

    private static void Test3_TempoSwitchNoStaleNoLoss()
    {
        const int frames = SampleRate * 6;
        var reader = new RampWaveStream(frames, 1, pcm16: false);
        var stream = PlaybackTempoStream.Create(reader, PlaybackTempoAlgorithm.Speech, out _);
        using var disposable = stream;

        // Rampa rosnie monotonicznie, wiec kazda probka wyjscia wskazuje swoje
        // miejsce w materiale. Stary bufor = wartosc NIZSZA od ostatnio oddanej.
        stream.Tempo = 2d;
        var a = ReadWindow(stream, SampleRate);          // ~2 s materialu
        stream.Tempo = 1d;
        var b = ReadWindow(stream, SampleRate / 2);
        stream.Tempo = 2d;
        var c = ReadWindow(stream, SampleRate / 2);

        Check("kazde okno oddalo dzwiek",
            a.Count > 0 && b.Count > 0 && c.Count > 0,
            $"okna: {a.Count}, {b.Count}, {c.Count} ramek");

        var afterA = a.Count > 0 ? a[^1] : 0d;
        var firstB = b.Count > 0 ? b[0] : 0d;
        Check("po przejsciu 2x -> 1x nie wraca stary material",
            firstB >= afterA - 2d,
            $"ostatnia ramka przy 2x = {afterA:F0}, pierwsza przy 1x = {firstB:F0} (ujemna roznica = stary bufor)");
        Check("po przejsciu 2x -> 1x nie przepada duzy kawalek materialu",
            firstB - afterA < SampleRate * 0.5,
            $"luka {firstB - afterA:F0} ramek zrodla (> 0,5 s = zgubiony material)");

        var afterB = b.Count > 0 ? b[^1] : 0d;
        var firstC = c.Count > 0 ? c[0] : 0d;
        Check("po przejsciu 1x -> 2x nie wraca stary material",
            firstC >= afterB - 2d,
            $"ostatnia ramka przy 1x = {afterB:F0}, pierwsza przy 2x = {firstC:F0}");
        Check("po przejsciu 1x -> 2x nie przepada duzy kawalek materialu",
            firstC - afterB < SampleRate * 0.5,
            $"luka {firstC - afterB:F0} ramek zrodla");

        Check("okno 1x jest wolniejsze niz okno 2x (tempo faktycznie zmienione)",
            SourceSpan(b) < SourceSpan(a) / Math.Max(1d, a.Count / (double)b.Count) * 1.6,
            $"rozpietosc zrodla: 2x {SourceSpan(a):F0} na {a.Count} ramek, 1x {SourceSpan(b):F0} na {b.Count} ramek");
    }

    private static double SourceSpan(List<double> window) =>
        window.Count < 2 ? 0d : window[^1] - window[0];

    // ---------- aparatura ----------

    /// <summary>Czyta caly strumien; zwraca liczbe ramek i najdalszy rozpoznany indeks zrodla.</summary>
    private static int DrainFrames(PlaybackTempoStream stream, out double maxSourceIndex)
    {
        var blockAlign = stream.WaveFormat.BlockAlign;
        var buffer = new byte[blockAlign * 4096];
        var frames = 0;
        maxSourceIndex = 0d;
        for (var guard = 0; guard < 100000; guard++)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;
            frames += read / blockAlign;
            maxSourceIndex = Math.Max(maxSourceIndex, DecodeLastIndex(stream.WaveFormat, buffer, read));
        }
        return frames;
    }

    /// <summary>Czyta okolo <paramref name="wantedFrames"/> ramek i oddaje indeksy zrodla.</summary>
    private static List<double> ReadWindow(PlaybackTempoStream stream, int wantedFrames)
    {
        var format = stream.WaveFormat;
        var blockAlign = format.BlockAlign;
        var buffer = new byte[blockAlign * 1024];
        var indices = new List<double>();
        while (indices.Count < wantedFrames)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;
            for (var offset = 0; offset + blockAlign <= read; offset += blockAlign)
            {
                indices.Add(DecodeIndex(format, buffer, offset));
            }
        }
        return indices;
    }

    private static double DecodeLastIndex(WaveFormat format, byte[] buffer, int read)
    {
        var blockAlign = format.BlockAlign;
        var last = ((read / blockAlign) - 1) * blockAlign;
        return last < 0 ? 0d : DecodeIndex(format, buffer, last);
    }

    /// <summary>Odwraca kodowanie rampy: wartosc probki -> numer ramki zrodla.</summary>
    private static double DecodeIndex(WaveFormat format, byte[] buffer, int offset)
    {
        var value = format.Encoding == WaveFormatEncoding.IeeeFloat
            ? BitConverter.ToSingle(buffer, offset)
            : BitConverter.ToInt16(buffer, offset) / 32767f;
        return value * RampWaveStream.IndexScale;
    }

    private static void RunTest(string name, Action body)
    {
        Console.WriteLine();
        Console.WriteLine("== " + name);
        try
        {
            body();
        }
        catch (Exception exception)
        {
            Failures.Add($"{name}: wyjatek {exception.GetType().Name}: {exception.Message}");
            Console.WriteLine("  WYJATEK " + exception);
        }
    }

    private static void Check(string what, bool ok, string detail)
    {
        _checks++;
        Console.WriteLine($"  [{(ok ? "OK  " : "BLAD")}] {what} -- {detail}");
        if (!ok) Failures.Add($"{what} -- {detail}");
    }

    /// <summary>
    /// Na Linuksie plik nazywa sie AmcTempoEngines.so (bez przedrostka lib),
    /// bo tak samo jak na Windows nazwa jest ustalona w CMake.
    /// </summary>
    private static void ResolveNativeLibrary()
    {
        var baseDirectory = AppContext.BaseDirectory;
        NativeLibrary.SetDllImportResolver(
            typeof(AmcTempoNativeLibrary).Assembly,
            (name, assembly, path) =>
            {
                if (!name.Equals("AmcTempoEngines", StringComparison.Ordinal)) return IntPtr.Zero;
                foreach (var candidate in new[]
                         {
                             Path.Combine(baseDirectory, "AmcTempoEngines.so"),
                             Path.Combine(baseDirectory, "libAmcTempoEngines.so"),
                             Environment.GetEnvironmentVariable("AMC_TEMPO_NATIVE_PATH") ?? string.Empty
                         })
                {
                    if (candidate.Length > 0 && File.Exists(candidate)) return NativeLibrary.Load(candidate);
                }
                return IntPtr.Zero;
            });
    }
}

/// <summary>
/// Syntetyczny fixture: rampa, w ktorej wartosc probki koduje numer ramki.
/// Dzieki temu kazda probka na wyjsciu mowi, SKAD w materiale pochodzi, wiec
/// stary bufor i zgubiony fragment sa widoczne jako liczby, nie jako wrazenie.
/// </summary>
internal sealed class RampWaveStream : WaveStream
{
    internal const float IndexScale = 1_000_000f;

    private readonly WaveFormat _format;
    private readonly long _frames;
    private readonly int _channels;
    private readonly bool _pcm16;
    private long _frame;

    internal RampWaveStream(long frames, int channels, bool pcm16)
    {
        _frames = frames;
        _channels = channels;
        _pcm16 = pcm16;
        _format = pcm16
            ? new WaveFormat(48000, 16, channels)
            : WaveFormat.CreateIeeeFloatWaveFormat(48000, channels);
    }

    public override WaveFormat WaveFormat => _format;

    public override long Length => _frames * _format.BlockAlign;

    public override long Position
    {
        get => _frame * _format.BlockAlign;
        set => _frame = Math.Clamp(value, 0, Length) / _format.BlockAlign;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var blockAlign = _format.BlockAlign;
        count -= count % blockAlign;
        var framesWanted = (int)Math.Min(count / blockAlign, _frames - _frame);
        if (framesWanted <= 0) return 0;
        for (var i = 0; i < framesWanted; i++)
        {
            var value = (float)((_frame + i) / (double)IndexScale);
            for (var channel = 0; channel < _channels; channel++)
            {
                var at = offset + ((i * _channels) + channel) * (_pcm16 ? 2 : 4);
                if (_pcm16)
                {
                    var sample = (short)Math.Clamp(value * 32767f, short.MinValue, short.MaxValue);
                    BitConverter.TryWriteBytes(buffer.AsSpan(at, 2), sample);
                }
                else
                {
                    BitConverter.TryWriteBytes(buffer.AsSpan(at, 4), value);
                }
            }
        }
        _frame += framesWanted;
        return framesWanted * blockAlign;
    }
}
