using System;
using System.Runtime.InteropServices;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Wejscie do natywnej biblioteki AmcTempoEngines.dll: Speedy/Sonic dla mowy,
/// Signalsmith Stretch dla muzyki. Pochodzenie i licencje zrodel opisuje
/// native/AmcTempoEngines/vendor/PROVENANCE.md.
///
/// Brak biblioteki NIE jest udawany: <see cref="IsAvailable"/> zwraca false,
/// a warstwa wyzej zostaje przy SoundTouch i mowi o tym wprost.
/// </summary>
internal static class AmcTempoNativeLibrary
{
    internal const string LibraryName = "AmcTempoEngines";

    /// <summary>Wersja ABI, ktorej oczekuje ten kod C#.</summary>
    internal const int ExpectedAbiVersion = 1;

    internal const int EngineSpeech = 1;
    internal const int EngineMusic = 2;

    private static readonly object Gate = new();
    private static bool _probed;
    private static bool _available;
    private static string? _unavailableReason;

    /// <summary>
    /// Czy natywne silniki da sie realnie wywolac. Sprawdzane raz, przez
    /// faktyczne wywolanie funkcji wersji - nie przez samo istnienie pliku.
    /// </summary>
    internal static bool IsAvailable
    {
        get
        {
            Probe();
            return _available;
        }
    }

    /// <summary>Czytelny powod niedostepnosci albo null, gdy jest dostepna.</summary>
    internal static string? UnavailableReason
    {
        get
        {
            Probe();
            return _unavailableReason;
        }
    }

    private static void Probe()
    {
        lock (Gate)
        {
            if (_probed) return;
            _probed = true;
            try
            {
                var abi = NativeMethods.AmcTempoAbiVersion();
                if (abi != ExpectedAbiVersion)
                {
                    _available = false;
                    _unavailableReason =
                        $"Biblioteka {LibraryName} ma wersję ABI {abi}, a program oczekuje {ExpectedAbiVersion}.";
                    return;
                }
                _available = true;
                _unavailableReason = null;
            }
            catch (Exception exception) when (exception is DllNotFoundException
                or BadImageFormatException
                or EntryPointNotFoundException)
            {
                _available = false;
                _unavailableReason =
                    $"Nie udało się wczytać biblioteki {LibraryName}.dll: {exception.Message}";
            }
        }
    }

    internal static class NativeMethods
    {
        [DllImport(LibraryName, EntryPoint = "AmcTempoAbiVersion", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoAbiVersion();

        // UWAGA ABI: ksztalt KAZDEJ z tych funkcji musi byc dokladnie taki, jak
        // w native/AmcTempoEngines/src/amc_tempo_engines.h, a nie taki, jak
        // wygodniej wolac z C#. Zmierzone na HEAD e84b635: brakujacy czwarty
        // parametr AmcTempoCreate oraz double zamiast float w SetTempo/SetPitch
        // dawaly naruszenie ochrony pamieci (kod 139) przy pierwszym
        // rzeczywistym wywolaniu. Kod kompilowal sie bez ostrzezenia, bo
        // DllImport nie weryfikuje sygnatur. Zrodlem prawdy jest naglowek.
        [DllImport(LibraryName, EntryPoint = "AmcTempoCreate", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr AmcTempoCreate(
            int engine,
            int sampleRate,
            int channels,
            out int status);

        [DllImport(LibraryName, EntryPoint = "AmcTempoDestroy", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void AmcTempoDestroy(IntPtr stream);

        [DllImport(LibraryName, EntryPoint = "AmcTempoSetTempo", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoSetTempo(IntPtr stream, float tempo);

        [DllImport(LibraryName, EntryPoint = "AmcTempoSetPitch", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoSetPitch(IntPtr stream, float pitch);

        [DllImport(LibraryName, EntryPoint = "AmcTempoSetNonlinearStrength", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoSetNonlinearStrength(IntPtr stream, float strength);

        [DllImport(LibraryName, EntryPoint = "AmcTempoWrite", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoWrite(IntPtr stream, float[] input, int frameCount);

        [DllImport(LibraryName, EntryPoint = "AmcTempoRead", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoRead(IntPtr stream, float[] output, int frameCapacity);

        [DllImport(LibraryName, EntryPoint = "AmcTempoFlush", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoFlush(IntPtr stream);

        [DllImport(LibraryName, EntryPoint = "AmcTempoReset", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoReset(IntPtr stream);

        // Te trzy byly w naglowku od poczatku, ale nie mialy odpowiednika w C#,
        // mimo ze komentarz naglowka mowi, ze strona zarzadzana liczy z nich
        // pozycje. Sluza do pomiaru i diagnostyki mapowania czasu.
        [DllImport(LibraryName, EntryPoint = "AmcTempoConsumedInputFrames", CallingConvention = CallingConvention.Cdecl)]
        internal static extern long AmcTempoConsumedInputFrames(IntPtr stream);

        [DllImport(LibraryName, EntryPoint = "AmcTempoProducedOutputFrames", CallingConvention = CallingConvention.Cdecl)]
        internal static extern long AmcTempoProducedOutputFrames(IntPtr stream);

        [DllImport(LibraryName, EntryPoint = "AmcTempoOutputLatencyFrames", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoOutputLatencyFrames(IntPtr stream);
    }
}
