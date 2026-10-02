using System;
using NAudio.Wave;
using SoundTouch.Net.NAudioSupport;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Wspolny kontrakt etapu tempa dla toru plikow. Istnieje, zeby ten sam kod
/// odtwarzania obslugiwal zarowno dotychczasowy SoundTouch, jak i nowe silniki
/// natywne (Speedy dla mowy, Signalsmith dla muzyki) bez zmian w dekoderach
/// ani w protokolach.
///
/// Zachowane wprost: tempo/pitch/rate, pozycja i dlugosc (przewijanie),
/// czyszczenie po przeskoku oraz obejscie przy tempie 1,0x.
/// </summary>
internal abstract class PlaybackTempoStream : WaveStream
{
    /// <summary>Tempo 1,0 = bez zmiany. Zakres zgodny z dotychczasowym.</summary>
    internal abstract double Tempo { get; set; }

    internal abstract double Pitch { get; set; }

    internal abstract double Rate { get; set; }

    /// <summary>Wyrzuca dzwiek zakolejkowany w silniku (po przeskoku).</summary>
    internal abstract void FlushProcessor();

    /// <summary>Nazwa silnika do logu i diagnostyki.</summary>
    internal abstract string EngineName { get; }

    /// <summary>
    /// Buduje etap tempa dla wskazanego algorytmu. Gdy wybrany silnik natywny
    /// nie jest dostepny, zwraca etap SoundTouch i podaje powod w
    /// <paramref name="fallbackReason"/> - zadnego udawania, ze nowy algorytm
    /// dziala.
    /// </summary>
    internal static PlaybackTempoStream Create(
        WaveStream reader,
        PlaybackTempoAlgorithm algorithm,
        out string? fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(reader);
        fallbackReason = null;
        if (algorithm == PlaybackTempoAlgorithm.SoundTouch)
        {
            return new SoundTouchTempoStream(reader);
        }

        if (!AmcTempoNativeLibrary.IsAvailable)
        {
            fallbackReason = AmcTempoNativeLibrary.UnavailableReason
                ?? "Natywne silniki tempa nie są dostępne.";
            return new SoundTouchTempoStream(reader);
        }

        var native = NativeTempoStream.TryCreate(reader, algorithm, out var reason);
        if (native is not null) return native;
        fallbackReason = reason ?? "Nie udało się uruchomić natywnego silnika tempa.";
        return new SoundTouchTempoStream(reader);
    }
}

/// <summary>Dotychczasowy etap tempa: SoundTouch. Zachowanie bez zmian.</summary>
internal sealed class SoundTouchTempoStream : PlaybackTempoStream
{
    private readonly SoundTouchWaveStream _inner;

    internal SoundTouchTempoStream(WaveStream reader)
    {
        _inner = new SoundTouchWaveStream(reader)
        {
            Tempo = 1d,
            Pitch = 1d,
            Rate = 1d
        };
    }

    internal override string EngineName => "SoundTouch";

    internal override double Tempo
    {
        get => _inner.Tempo;
        set => _inner.Tempo = value;
    }

    internal override double Pitch
    {
        get => _inner.Pitch;
        set => _inner.Pitch = value;
    }

    internal override double Rate
    {
        get => _inner.Rate;
        set => _inner.Rate = value;
    }

    internal override void FlushProcessor() => _inner.Flush();

    public override WaveFormat WaveFormat => _inner.WaveFormat;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override TimeSpan CurrentTime
    {
        get => _inner.CurrentTime;
        set => _inner.CurrentTime = value;
    }

    public override TimeSpan TotalTime => _inner.TotalTime;

    public override int Read(byte[] buffer, int offset, int count) =>
        _inner.Read(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        // SoundTouchWaveStream jest wlascicielem czytnika i sam go zwalnia -
        // tak jak dotychczas.
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
