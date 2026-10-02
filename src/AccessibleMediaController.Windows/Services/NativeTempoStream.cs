using System;
using NAudio.Wave;
using AccessibleMediaController.Core.Configuration;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Etap tempa oparty na natywnych silnikach AMC: Speedy/Sonic dla mowy i
/// Signalsmith Stretch dla muzyki. Stoi dokladnie tam, gdzie dotychczas stal
/// SoundTouch - za dekoderem, przed normalizacja glosnosci. Dekodery,
/// protokoly i bufor transmisji zostaja nietkniete.
///
/// Mapowanie czasu - rzecz wazna i celowo prosta:
/// Speedy przyspiesza NIELINIOWO (ciszej/wolniej w trudnych miejscach), wiec
/// czasu w materiale NIE WOLNO liczyc jako wyjscie * tempo. Dlatego pozycja i
/// dlugosc sa brane WPROST z czytnika zrodla (ile materialu faktycznie
/// pochlonelismy), a nie wyliczane z liczby oddanych probek. Granica tego
/// podejscia: pozycja wyprzedza to, co uzytkownik slyszy, o material
/// zakolejkowany w silniku (rzad setek milisekund). Przy przewijaniu kolejka
/// jest czyszczona, wiec blad nie kumuluje sie miedzy przeskokami.
/// </summary>
internal sealed class NativeTempoStream : PlaybackTempoStream
{
    /// <summary>Ile ramek czytamy ze zrodla w jednym podejsciu.</summary>
    private const int SourceFrameChunk = 4096;

    /// <summary>Sila nieliniowosci Speedy. 0 = czysty Sonic (bez Speedy).</summary>
    private const float SpeechNonlinearStrength = 1.0f;

    private readonly object _gate = new();
    private readonly WaveStream _reader;
    private readonly WaveFormat _format;
    private readonly int _channels;
    private readonly string _engineName;
    private IntPtr _handle;
    private float[] _sourceBuffer = [];
    private float[] _engineBuffer = [];
    private byte[] _sourceBytes = [];
    private double _tempo = 1d;
    private double _pitch = 1d;
    private double _rate = 1d;
    private bool _sourceEnded;
    private bool _flushed;
    private bool _disposed;

    private NativeTempoStream(
        WaveStream reader,
        WaveFormat format,
        IntPtr handle,
        string engineName)
    {
        _reader = reader;
        _format = format;
        _channels = format.Channels;
        _handle = handle;
        _engineName = engineName;
    }

    /// <summary>
    /// Tworzy etap albo zwraca null z czytelnym powodem. Nie rzuca wyjatkiem w
    /// gore, zeby wywolujacy mogl spokojnie wrocic do SoundTouch.
    /// </summary>
    internal static NativeTempoStream? TryCreate(
        WaveStream reader,
        PlaybackTempoAlgorithm algorithm,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reason = null;
        var format = reader.WaveFormat;
        if (format is null)
        {
            reason = "Czytnik nie podał formatu dźwięku.";
            return null;
        }
        if (format.Encoding != WaveFormatEncoding.IeeeFloat || format.BitsPerSample != 32)
        {
            reason = "Natywne silniki tempa przyjmują tylko 32-bitowy dźwięk zmiennoprzecinkowy.";
            return null;
        }
        if (format.Channels is < 1 or > 2)
        {
            reason = $"Nieobsługiwana liczba kanałów: {format.Channels}.";
            return null;
        }

        var engine = algorithm switch
        {
            PlaybackTempoAlgorithm.Speech => AmcTempoNativeLibrary.EngineSpeech,
            PlaybackTempoAlgorithm.Music => AmcTempoNativeLibrary.EngineMusic,
            _ => 0
        };
        if (engine == 0)
        {
            reason = $"Algorytm {algorithm} nie ma natywnego silnika.";
            return null;
        }

        IntPtr handle;
        try
        {
            handle = AmcTempoNativeLibrary.NativeMethods.AmcTempoCreate(
                engine,
                format.SampleRate,
                format.Channels);
        }
        catch (Exception exception) when (exception is DllNotFoundException
            or BadImageFormatException
            or EntryPointNotFoundException)
        {
            reason = $"Nie udało się wczytać biblioteki silników tempa: {exception.Message}";
            return null;
        }
        if (handle == IntPtr.Zero)
        {
            reason = "Natywny silnik tempa odmówił utworzenia strumienia.";
            return null;
        }

        var name = algorithm == PlaybackTempoAlgorithm.Speech
            ? "Speedy (mowa)"
            : "Signalsmith Stretch (muzyka)";
        var stream = new NativeTempoStream(reader, format, handle, name);
        if (algorithm == PlaybackTempoAlgorithm.Speech)
        {
            // Nieliniowosc wlaczona wprost - zerowa sila znaczy "tylko Sonic".
            AmcTempoNativeLibrary.NativeMethods.AmcTempoSetNonlinearStrength(
                handle,
                SpeechNonlinearStrength);
        }
        return stream;
    }

    internal override string EngineName => _engineName;

    public override WaveFormat WaveFormat => _format;

    /// <summary>Dlugosc liczona w materiale zrodlowym, nie w wyjsciu.</summary>
    public override long Length => _reader.Length;

    /// <summary>
    /// Pozycja w materiale zrodlowym. Patrz uwaga o mapowaniu czasu w opisie
    /// klasy: dla silnika nieliniowego to jedyna rzetelna miara.
    /// </summary>
    public override long Position
    {
        get => _reader.Position;
        set
        {
            lock (_gate)
            {
                _reader.Position = value;
                ResetEngineLocked();
            }
        }
    }

    public override TimeSpan CurrentTime
    {
        get => _reader.CurrentTime;
        set
        {
            lock (_gate)
            {
                _reader.CurrentTime = value;
                ResetEngineLocked();
            }
        }
    }

    public override TimeSpan TotalTime => _reader.TotalTime;

    internal override double Tempo
    {
        get { lock (_gate) return _tempo; }
        set
        {
            lock (_gate)
            {
                if (_disposed) return;
                _tempo = value;
                AmcTempoNativeLibrary.NativeMethods.AmcTempoSetTempo(_handle, value);
            }
        }
    }

    /// <summary>
    /// Wysokosc dzwieku. Nowe silniki trzymaja ja bez zmian przy regulacji
    /// tempa; wartosci inne niz 1,0 nie sa obslugiwane i sa ignorowane, zeby
    /// nie udawac dzialania.
    /// </summary>
    internal override double Pitch
    {
        get { lock (_gate) return _pitch; }
        set
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (AmcTempoNativeLibrary.NativeMethods.AmcTempoSetPitch(_handle, value) == 0)
                {
                    _pitch = value;
                }
            }
        }
    }

    /// <summary>
    /// Zachowane dla zgodnosci z dotychczasowym torem: tor plikow ustawia 1,0.
    /// Inne wartosci nie sa obslugiwane przez nowe silniki.
    /// </summary>
    internal override double Rate
    {
        get { lock (_gate) return _rate; }
        set { lock (_gate) if (Math.Abs(value - 1d) < 0.0001d) _rate = value; }
    }

    internal override void FlushProcessor()
    {
        lock (_gate) ResetEngineLocked();
    }

    private void ResetEngineLocked()
    {
        if (_disposed) return;
        AmcTempoNativeLibrary.NativeMethods.AmcTempoReset(_handle);
        _sourceEnded = false;
        _flushed = false;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        lock (_gate)
        {
            if (_disposed) return 0;
            var blockAlign = Math.Max(1, _format.BlockAlign);
            count -= count % blockAlign;
            if (count <= 0) return 0;

            // Obejscie przy 1,0x: zaden silnik nie dotyka dzwieku, tak jak
            // dotychczas robil to tor SoundTouch z tempem 1,0.
            if (Math.Abs(_tempo - 1d) < 0.001d) return _reader.Read(buffer, offset, count);

            var framesWanted = count / blockAlign;
            EnsureEngineBuffer(framesWanted);
            var produced = 0;
            while (produced < framesWanted)
            {
                var got = AmcTempoNativeLibrary.NativeMethods.AmcTempoRead(
                    _handle,
                    _engineBuffer,
                    framesWanted - produced);
                if (got > 0)
                {
                    CopyFramesToBytes(got, buffer, offset + (produced * blockAlign));
                    produced += got;
                    continue;
                }
                if (_sourceEnded)
                {
                    // Domkniecie konca: raz oddajemy silnikowi polecenie
                    // wypchniecia ogona, potem konczymy.
                    if (_flushed) break;
                    AmcTempoNativeLibrary.NativeMethods.AmcTempoFlush(_handle);
                    _flushed = true;
                    continue;
                }
                if (!PumpSourceLocked()) _sourceEnded = true;
            }
            return produced * blockAlign;
        }
    }

    /// <summary>Dokłada materiał ze źródła. False = źródło się skończyło.</summary>
    private bool PumpSourceLocked()
    {
        var blockAlign = Math.Max(1, _format.BlockAlign);
        var wantedBytes = SourceFrameChunk * blockAlign;
        if (_sourceBytes.Length < wantedBytes) _sourceBytes = new byte[wantedBytes];
        var readBytes = _reader.Read(_sourceBytes, 0, wantedBytes);
        if (readBytes <= 0) return false;
        readBytes -= readBytes % blockAlign;
        if (readBytes <= 0) return false;
        var frames = readBytes / blockAlign;
        var samples = frames * _channels;
        if (_sourceBuffer.Length < samples) _sourceBuffer = new float[samples];
        Buffer.BlockCopy(_sourceBytes, 0, _sourceBuffer, 0, samples * sizeof(float));
        var accepted = AmcTempoNativeLibrary.NativeMethods.AmcTempoWrite(
            _handle,
            _sourceBuffer,
            frames);
        return accepted > 0;
    }

    private void EnsureEngineBuffer(int frames)
    {
        var samples = Math.Max(1, frames) * _channels;
        if (_engineBuffer.Length < samples) _engineBuffer = new float[samples];
    }

    private void CopyFramesToBytes(int frames, byte[] destination, int destinationOffset) =>
        Buffer.BlockCopy(
            _engineBuffer,
            0,
            destination,
            destinationOffset,
            frames * _channels * sizeof(float));

    protected override void Dispose(bool disposing)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_handle != IntPtr.Zero)
                {
                    AmcTempoNativeLibrary.NativeMethods.AmcTempoDestroy(_handle);
                    _handle = IntPtr.Zero;
                }
                // Tak jak SoundTouchWaveStream: etap jest wlascicielem czytnika.
                if (disposing) _reader.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
