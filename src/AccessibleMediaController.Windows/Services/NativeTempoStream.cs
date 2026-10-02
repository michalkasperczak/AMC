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

    /// <summary>
    /// Obejscie przy tempie 1,0x. ZMIERZONE, nie zalozone: przy wlaczonym
    /// obejsciu przejscie 2x -> 1x -> 2x oddawalo material WSTECZ o 23801 ramek
    /// zrodla (T3), a ten sam przebieg z obejsciem wylaczonym (T3c) oraz
    /// przebieg omijajacy 1,0x, czyli 2x -> 1,5x -> 2x (T3b), byly czyste.
    /// Powod jest strukturalny: w obejsciu czytnik idzie dalej, a silnik tego
    /// nie widzi, wiec po powrocie do tempa innego niz 1,0 nie ma spojnego
    /// punktu zaczepienia. Dlatego domyslnie obejscia NIE MA - silnik dostaje
    /// material takze przy 1,0x. Granica: przy 1,0x placimy czas procesora
    /// silnika, ktorego tor SoundTouch nie placil.
    /// Pole zostaje publiczne dla testu, ktory rozstrzyga te hipoteze.
    /// </summary>
    internal static bool BypassAtUnitTempo;

    private readonly object _gate = new();
    private readonly WaveStream _reader;
    private readonly WaveFormat _format;
    private readonly int _channels;
    private readonly string _engineName;
    /// <summary>True dla zrodla 16-bit PCM: trzeba przeliczac w obie strony.</summary>
    private readonly bool _pcm16;
    private IntPtr _handle;
    private float[] _sourceBuffer = [];
    private float[] _engineBuffer = [];
    private byte[] _sourceBytes = [];
    /// <summary>Ogon oddany przez silnik przy zmianie tempa, jeszcze nie wydany.</summary>
    private byte[] _pendingTail = [];
    private int _pendingTailLength;
    private int _pendingTailOffset;
    private bool _needsHandoff;
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
        _pcm16 = format.Encoding == WaveFormatEncoding.Pcm;
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
        if (format.Encoding is not (WaveFormatEncoding.IeeeFloat or WaveFormatEncoding.Pcm))
        {
            reason = $"Nieobsługiwane kodowanie dźwięku: {format.Encoding}.";
            return null;
        }
        // 16-bitowy PCM jest zwyklym wyjsciem czesci dekoderow. Nie jest to
        // powod do cichego powrotu do SoundTouch: silniki pracuja na float,
        // wiec konwersja idzie TUTAJ, a format wyjscia etapu pozostaje taki
        // sam jak wejscia, zeby dalsze ogniwa toru nie zmienily sie wcale.
        if (format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample != 16)
        {
            reason = $"Natywne silniki tempa przyjmują z PCM tylko 16 bitów, nie {format.BitsPerSample}.";
            return null;
        }
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample != 32)
        {
            reason = $"Dźwięk zmiennoprzecinkowy musi mieć 32 bity, nie {format.BitsPerSample}.";
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
        var status = 0;
        try
        {
            handle = AmcTempoNativeLibrary.NativeMethods.AmcTempoCreate(
                engine,
                format.SampleRate,
                format.Channels,
                out status);
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
            reason = $"Natywny silnik tempa odmówił utworzenia strumienia (kod {status}).";
            return null;
        }

        var name = algorithm == PlaybackTempoAlgorithm.Speech
            ? "Speedy (mowa)"
            : "Signalsmith Stretch (muzyka)";
        var stream = new NativeTempoStream(reader, format, handle, name);
        if (status != 0)
        {
            // Nie przemilczamy ostrzezenia silnika, nawet gdy oddal uchwyt.
            reason = $"Silnik utworzony z ostrzeżeniem (kod {status}).";
        }
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
                if (Math.Abs(_tempo - value) < 1e-9d) return;
                // Zmiana tempa jest PRZEKAZANIEM, nie samym ustawieniem liczby:
                // material juz pochloniety ze zrodla, a jeszcze nie oddany,
                // musi wyjsc w starym tempie, zanim silnik dostanie nowe.
                // Bez tego 1x -> 2x oddawalo najpierw stary, nieoddany ogon
                // (zmierzone: 31003 ramki zrodla wstecz).
                _needsHandoff = true;
                _tempo = value;
                AmcTempoNativeLibrary.NativeMethods.AmcTempoSetTempo(_handle, (float)value);
                HandoffLocked();
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
                if (AmcTempoNativeLibrary.NativeMethods.AmcTempoSetPitch(_handle, (float)value) == 0)
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
        // Po przeskoku stary material jest NIEPRAWIDLOWY, nie opozniony:
        // bufor przekazania trzeba porzucic, a nie wydac.
        _pendingTailOffset = 0;
        _pendingTailLength = 0;
        _needsHandoff = false;
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

            // Bufor przekazania zawsze pierwszy: to material juz pochloniety ze
            // zrodla, oddany przez silnik w POPRZEDNIM tempie.
            var pending = TakePendingTailLocked(buffer, offset, count);
            if (pending > 0) return pending;

            // Obejscie przy 1,0x: zaden silnik nie dotyka dzwieku, tak jak
            // dotychczas robil to tor SoundTouch z tempem 1,0.
            if (BypassAtUnitTempo && Math.Abs(_tempo - 1d) < 0.001d)
            {
                return _reader.Read(buffer, offset, count);
            }

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
                switch (PumpSourceLocked())
                {
                    case SourcePumpResult.SourceEnded:
                        _sourceEnded = true;
                        break;
                    case SourcePumpResult.EngineFull:
                        // Silnik odmowil PRZYJECIA, ale material sie nie
                        // skonczyl. Zmierzone: wczesniej brano to za koniec
                        // zrodla i Signalsmith konczyl utwor na ~64180 ramce
                        // z 96000, czyli gubil jedna trzecia materialu.
                        // Odmowa przyjecia znaczy "oddaj najpierw wyjscie",
                        // wiec po prostu wracamy do petli czytania.
                        break;
                }
            }
            return produced * blockAlign;
        }
    }

    /// <summary>
    /// Przekazanie przy zmianie tempa. Material gotowy w silniku zostaje
    /// zdjety do bufora przekazania i wyjdzie PRZED nowym, a stan silnika jest
    /// zerowany, zeby jego wewnetrzna kolejka nie wrocila pozniej jako stary
    /// dzwiek. Dwie wlasnosci razem: nic nie przepada i nic sie nie powtarza.
    /// Limit bufora jest skonczony, bo opoznienie silnika jest skonczone.
    /// </summary>
    private void HandoffLocked()
    {
        if (!_needsHandoff) return;
        _needsHandoff = false;
        var blockAlign = Math.Max(1, _format.BlockAlign);
        const int MaxHandoffFrames = 1 << 16;
        EnsureEngineBuffer(MaxHandoffFrames);
        var carried = 0;
        while (carried < MaxHandoffFrames)
        {
            var got = AmcTempoNativeLibrary.NativeMethods.AmcTempoRead(
                _handle,
                _engineBuffer,
                Math.Min(SourceFrameChunk, MaxHandoffFrames - carried));
            if (got <= 0) break;
            var neededBytes = (_pendingTailLength - _pendingTailOffset) + (got * blockAlign);
            if (_pendingTail.Length < neededBytes)
            {
                var grown = new byte[Math.Max(neededBytes, _pendingTail.Length * 2)];
                Buffer.BlockCopy(
                    _pendingTail,
                    _pendingTailOffset,
                    grown,
                    0,
                    _pendingTailLength - _pendingTailOffset);
                _pendingTailLength -= _pendingTailOffset;
                _pendingTailOffset = 0;
                _pendingTail = grown;
            }
            CopyFramesToBytes(got, _pendingTail, _pendingTailLength);
            _pendingTailLength += got * blockAlign;
            carried += got;
        }
        // Zerowanie DOPIERO po zdjeciu gotowego materialu.
        AmcTempoNativeLibrary.NativeMethods.AmcTempoReset(_handle);
        var residual = 0;
        while (true)
        {
            var got = AmcTempoNativeLibrary.NativeMethods.AmcTempoRead(
                _handle, _engineBuffer, SourceFrameChunk);
            if (got <= 0) break;
            residual += got;
            if (residual > 1 << 20) break;
        }
        LastHandoffDiagnostics =
            $"przeniesione {carried}, resztka po zerowaniu {residual}, " +
            $"pochloniete {AmcTempoNativeLibrary.NativeMethods.AmcTempoConsumedInputFrames(_handle)}, " +
            $"oddane {AmcTempoNativeLibrary.NativeMethods.AmcTempoProducedOutputFrames(_handle)}, " +
            $"opoznienie {AmcTempoNativeLibrary.NativeMethods.AmcTempoOutputLatencyFrames(_handle)}, " +
            $"pozycja czytnika {_reader.Position / Math.Max(1, _format.BlockAlign)}";
        _sourceEnded = false;
        _flushed = false;
    }

    /// <summary>Tylko do pomiaru: co stalo sie przy ostatniej zmianie tempa.</summary>
    internal string? LastHandoffDiagnostics { get; private set; }

    /// <summary>Wydaje bufor przekazania. 0 = nic nie czeka.</summary>
    private int TakePendingTailLocked(byte[] buffer, int offset, int count)
    {
        var available = _pendingTailLength - _pendingTailOffset;
        if (available <= 0) return 0;
        var take = Math.Min(available, count);
        take -= take % Math.Max(1, _format.BlockAlign);
        if (take <= 0) return 0;
        Buffer.BlockCopy(_pendingTail, _pendingTailOffset, buffer, offset, take);
        _pendingTailOffset += take;
        if (_pendingTailOffset >= _pendingTailLength)
        {
            _pendingTailOffset = 0;
            _pendingTailLength = 0;
        }
        return take;
    }

    /// <summary>Wynik doklejania materialu ze zrodla.</summary>
    private enum SourcePumpResult
    {
        /// <summary>Material przyjety.</summary>
        Accepted,

        /// <summary>Silnik nie przyjal teraz; material NADAL jest.</summary>
        EngineFull,

        /// <summary>Zrodlo faktycznie sie skonczylo.</summary>
        SourceEnded
    }

    /// <summary>Dokłada materiał ze źródła.</summary>
    private SourcePumpResult PumpSourceLocked()
    {
        var blockAlign = Math.Max(1, _format.BlockAlign);
        var wantedBytes = SourceFrameChunk * blockAlign;
        if (_sourceBytes.Length < wantedBytes) _sourceBytes = new byte[wantedBytes];
        var readBytes = _reader.Read(_sourceBytes, 0, wantedBytes);
        if (readBytes <= 0) return SourcePumpResult.SourceEnded;
        readBytes -= readBytes % blockAlign;
        if (readBytes <= 0) return SourcePumpResult.SourceEnded;
        var frames = readBytes / blockAlign;
        var samples = frames * _channels;
        if (_sourceBuffer.Length < samples) _sourceBuffer = new float[samples];
        if (_pcm16)
        {
            // Silniki pracuja na float w zakresie (-1, 1). Konwersja jest tutaj,
            // zeby wybor 16-bitowego dekodera nie odbieral uzytkownikowi
            // wybranego algorytmu tempa.
            for (var i = 0; i < samples; i++)
            {
                _sourceBuffer[i] = BitConverter.ToInt16(_sourceBytes, i * sizeof(short)) / 32768f;
            }
        }
        else
        {
            Buffer.BlockCopy(_sourceBytes, 0, _sourceBuffer, 0, samples * sizeof(float));
        }
        var accepted = AmcTempoNativeLibrary.NativeMethods.AmcTempoWrite(
            _handle,
            _sourceBuffer,
            frames);
        return accepted > 0 ? SourcePumpResult.Accepted : SourcePumpResult.EngineFull;
    }

    private void EnsureEngineBuffer(int frames)
    {
        var samples = Math.Max(1, frames) * _channels;
        if (_engineBuffer.Length < samples) _engineBuffer = new float[samples];
    }

    private void CopyFramesToBytes(int frames, byte[] destination, int destinationOffset)
    {
        var samples = frames * _channels;
        if (!_pcm16)
        {
            Buffer.BlockCopy(_engineBuffer, 0, destination, destinationOffset, samples * sizeof(float));
            return;
        }
        // Powrot do 16-bitowego PCM: format wyjscia etapu jest taki sam jak
        // wejscia, wiec dalsze ogniwa toru nie wymagaja zadnej zmiany.
        for (var i = 0; i < samples; i++)
        {
            var clamped = Math.Clamp(_engineBuffer[i], -1f, 1f);
            var sample = (short)Math.Clamp(clamped * 32767f, short.MinValue, short.MaxValue);
            BitConverter.TryWriteBytes(
                destination.AsSpan(destinationOffset + (i * sizeof(short)), sizeof(short)),
                sample);
        }
    }

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
