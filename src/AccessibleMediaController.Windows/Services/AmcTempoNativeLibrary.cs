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

        [DllImport(LibraryName, EntryPoint = "AmcTempoCreate", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr AmcTempoCreate(int engine, int sampleRate, int channels);

        [DllImport(LibraryName, EntryPoint = "AmcTempoDestroy", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void AmcTempoDestroy(IntPtr stream);

        [DllImport(LibraryName, EntryPoint = "AmcTempoSetTempo", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoSetTempo(IntPtr stream, double tempo);

        [DllImport(LibraryName, EntryPoint = "AmcTempoSetPitch", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int AmcTempoSetPitch(IntPtr stream, double pitch);

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
    }
}
