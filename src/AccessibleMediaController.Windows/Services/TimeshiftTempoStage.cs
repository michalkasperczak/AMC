using System;
using NAudio.Wave;
using SoundTouch.Net.NAudioSupport;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Etap regulacji tempa dla bufora transmisji (TimeShift). Stoi ZA buforem i
/// PRZED regulacja glosnosci, wiec nie dotyka dekodera ani odbioru strumienia.
///
/// Trzy rzeczy, ktorych nie robil poprzedni uklad (zmierzone sonda syntetyczna
/// 19.09.2026, patrz TimeshiftTempoAudioTests):
/// 1. PCM 16-bit w ogole nie dostawal regulacji tempa - SoundTouch przyjmuje
///    WYLACZNIE 32-bit IEEE float i rzuca ArgumentException juz w konstruktorze.
///    Tutaj PCM jest zamieniany na float ZA buforem, wiec dekoder i zawartosc
///    bufora zostaja nietkniete, a tempo dziala dla obslugiwanych formatow float32 i PCM16.
/// 2. Przy malym zapasie bufor oddaje CISZE (Read zawsze zwraca count), wiec
///    przyspieszenie "konsumowalo" cisze zamiast materialu: zmierzone 82,8%
///    ciszy na wyjsciu przy tempie 1,5x i sekundzie zapasu. Etap pilnuje zapasu
///    i sam wraca do 1,0x, gdy material sie konczy - tak dochodzi sie do live.
/// 3. Dopoki uzytkownik nie ruszyl tempa, dzwiek idzie NA WPROST, z pominieciem
///    SoundTouch. Zwykle sluchanie 1x nie zalezy wiec od tego dodatku.
///
/// Nieobslugiwany format albo rozpoznany blad tworzenia etapu powoduje zwrot
/// null z TryCreate; wywolujacy zachowuje wtedy oryginalny tor bez tempa.
/// </summary>
public sealed class TimeshiftTempoStage : IWaveProvider, IDisposable
{
    /// <summary>Zapas materialu, ponizej ktorego przyspieszenie sie wycofuje.</summary>
    public const double LiveReserveSeconds = 1d;

    public const double MinimumTempo = 0.50d;
    public const double MaximumTempo = 2.00d;

    private readonly object _gate = new();
    private readonly FloatConversionWaveStream _float;
    private readonly SoundTouchWaveStream _soundTouch;
    private readonly Func<TimeSpan> _behindLive;
    private double _requestedTempo = 1d;
    private double _effectiveTempo = 1d;
    private bool _engaged;
    private bool _disposed;

    private TimeshiftTempoStage(
        FloatConversionWaveStream floatStream,
        SoundTouchWaveStream soundTouch,
        Func<TimeSpan> behindLive)
    {
        _float = floatStream;
        _soundTouch = soundTouch;
        _behindLive = behindLive;
    }

    /// <summary>
    /// Buduje etap nad buforem transmisji. Zwraca null, gdy formatu nie da sie
    /// obsluzyc - wtedy stacja gra bez regulacji tempa, ale gra.
    /// </summary>
    /// <param name="bufferedSource">Bufor transmisji (juz za dekoderem).</param>
    /// <param name="behindLive">Ile materialu zostalo do czola transmisji.</param>
    public static TimeshiftTempoStage? TryCreate(
        IWaveProvider bufferedSource,
        Func<TimeSpan> behindLive)
    {
        ArgumentNullException.ThrowIfNull(bufferedSource);
        ArgumentNullException.ThrowIfNull(behindLive);
        FloatConversionWaveStream? floatStream = null;
        try
        {
            floatStream = FloatConversionWaveStream.TryCreate(bufferedSource);
            if (floatStream is null) return null;
            var soundTouch = new SoundTouchWaveStream(floatStream)
            {
                Tempo = 1d,
                Pitch = 1d,
                Rate = 1d
            };
            return new TimeshiftTempoStage(floatStream, soundTouch, behindLive);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or InvalidOperationException)
        {
            floatStream?.Dispose();
            return null;
        }
    }

    public WaveFormat WaveFormat => _float.WaveFormat;

    public event EventHandler? NormalTempoResumed;

    /// <summary>Tempo, o ktore poprosil uzytkownik.</summary>
    public double RequestedTempo
    {
        get { lock (_gate) return _requestedTempo; }
    }

    /// <summary>
    /// Tempo FAKTYCZNIE stosowane do dzwieku. Rozni sie od zadanego, gdy zapas
    /// bufora sie wyczerpal i etap sam wrocil do 1,0x.
    /// </summary>
    public double EffectiveTempo
    {
        get { lock (_gate) return _effectiveTempo; }
    }

    public void SetTempo(double tempo)
    {
        var resolved = Math.Clamp(tempo, MinimumTempo, MaximumTempo);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ApplyTempoLocked(ResolveEffectiveTempo(resolved, 0));
        }
    }

    private void ApplyTempoLocked(double tempo)
    {
        // Apply to the processor before exposing the accepted setting. This
        // also works while paused, without waiting for the next audio Read.
        _soundTouch.Tempo = tempo;
        _requestedTempo = tempo;
        _effectiveTempo = tempo;
        if (Math.Abs(tempo - 1d) > 0.001d) _engaged = true;
    }

    /// <summary>
    /// Move the underlying buffer and discard DSP samples from its previous
    /// position as one operation relative to Read. End also restores 1x.
    /// </summary>
    internal void Reposition(Action moveBuffer, bool returnToLive)
    {
        ArgumentNullException.ThrowIfNull(moveBuffer);
        lock (_gate)
        {
            if (_disposed) return;
            moveBuffer();
            // SoundTouchWaveStream.Flush clears its processor's queued audio
            // (verified against the pinned SoundTouch.Net 2.3.2 source).
            _soundTouch.Flush();
            ApplyTempoLocked(returnToLive ? 1d : ResolveEffectiveTempo(_requestedTempo, 0));
            _engaged = Math.Abs(_effectiveTempo - 1d) > 0.001d;
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        int read;
        var resumedNormalTempo = false;
        lock (_gate)
        {
            if (_disposed) return 0;
            var effective = ResolveEffectiveTempo(_requestedTempo, count);
            // Reaching live is a completed return to normal, not a temporary
            // pause in acceleration. Only another user command can speed up.
            if (Math.Abs(effective - _requestedTempo) > 0.001d)
            {
                resumedNormalTempo = Math.Abs(effective - 1d) < 0.001d;
                ApplyTempoLocked(effective);
            }
            read = !_engaged ? _float.Read(buffer, offset, count) : _soundTouch.Read(buffer, offset, count);
        }
        // Notifications cannot hold the audio lock or interrupt playback.
        if (resumedNormalTempo)
        {
            try { NormalTempoResumed?.Invoke(this, EventArgs.Empty); }
            catch (Exception exception)
            {
                DiagnosticLog.Error("radio-playback", "Nie udało się przekazać informacji o powrocie do normalnego tempa.", exception);
            }
        }
        return read;
    }

    /// <summary>
    /// Przyspieszenie zjada wiecej materialu, niz oddaje dzwieku. Gdy zapasu
    /// brakuje, bufor oddaje CISZE zamiast dzwieku - dlatego zamiast udawac
    /// szybsze odtwarzanie wracamy do 1,0x i zostajemy przy czole transmisji.
    /// Zwalnianie (ponizej 1,0x) buduje zapas, ale tez zaczyna sie w buforze.
    /// </summary>
    private double ResolveEffectiveTempo(double requested, int count)
    {
        if (Math.Abs(requested - 1d) < 0.001d) return 1d;
        TimeSpan behind;
        try { behind = _behindLive(); }
        catch (Exception exception) when (exception is InvalidOperationException
            or ObjectDisposedException)
        {
            return 1d;
        }
        if (behind.TotalSeconds < LiveReserveSeconds) return 1d;
        if (requested < 1d) return requested;
        var bytesPerSecond = WaveFormat.AverageBytesPerSecond;
        if (bytesPerSecond <= 0) return 1d;
        var outputSeconds = (double)Math.Max(0, count) / bytesPerSecond;
        var neededSeconds = (outputSeconds * requested) + LiveReserveSeconds;
        return behind.TotalSeconds >= neededSeconds ? requested : 1d;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _effectiveTempo = 1d;
            _requestedTempo = 1d;
            _soundTouch.Dispose();
            _float.Dispose();
        }
    }

    /// <summary>
    /// Podaje bufor transmisji jako 32-bitowy float WaveStream. Dzwiek 16-bit
    /// PCM jest przeliczany TUTAJ, czyli za buforem - w buforze, w nagrywaniu i
    /// w dekoderze nic sie nie zmienia. Strumien jest nieskonczony, wiec dlugosc
    /// jest umowna, a przewijanie robi sam bufor.
    /// </summary>
    internal sealed class FloatConversionWaveStream : WaveStream
    {
        private readonly IWaveProvider _source;
        private readonly bool _convertFromPcm16;
        private byte[] _scratch = [];

        private FloatConversionWaveStream(IWaveProvider source, WaveFormat outputFormat, bool convert)
        {
            _source = source;
            WaveFormat = outputFormat;
            _convertFromPcm16 = convert;
        }

        public static FloatConversionWaveStream? TryCreate(IWaveProvider source)
        {
            var format = source.WaveFormat;
            if (format is null || format.Channels <= 0 || format.SampleRate <= 0) return null;
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                return new FloatConversionWaveStream(source, format, convert: false);
            }
            if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample == 16)
            {
                var output = WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, format.Channels);
                return new FloatConversionWaveStream(source, output, convert: true);
            }
            return null;
        }

        public override WaveFormat WaveFormat { get; }

        public override long Length => long.MaxValue;

        public override long Position { get; set; }

        public override int Read(byte[] destination, int offset, int count)
        {
            count -= count % WaveFormat.BlockAlign;
            if (count <= 0) return 0;
            if (!_convertFromPcm16)
            {
                var direct = _source.Read(destination, offset, count);
                Position += direct;
                return direct;
            }

            var sourceBytes = count / 2;
            sourceBytes -= sourceBytes % Math.Max(1, _source.WaveFormat.BlockAlign);
            if (sourceBytes <= 0) return 0;
            if (_scratch.Length < sourceBytes) _scratch = new byte[sourceBytes];
            var read = _source.Read(_scratch, 0, sourceBytes);
            if (read <= 0) return 0;
            read -= read % 2;
            var samples = read / 2;
            for (var index = 0; index < samples; index++)
            {
                var value = BitConverter.ToInt16(_scratch, index * 2) / 32768f;
                BitConverter.TryWriteBytes(destination.AsSpan(offset + (index * 4), 4), value);
            }
            var produced = samples * 4;
            Position += produced;
            return produced;
        }
    }
}
