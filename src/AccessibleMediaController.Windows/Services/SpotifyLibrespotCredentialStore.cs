using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Magazyn logowania NATYWNEJ sesji Spotify (Librespot) w Menedzerze
/// poswiadczen Windows.
///
/// ODDZIELNY klucz docelowy od logowania Web API/SDK
/// ("AccessibleMediaController/Spotify"). Powod zmierzony 18.09.2026: token
/// wydany dla wlasnego ClientID aplikacji NIE loguje sie do Librespota -
/// warstwa AP inicjalizuje sie, a nastepnie Login konczy sie
/// INVALID_CREDENTIALS. Dziala wylacznie osobne, standardowe OAuth Device
/// Authorization z ClientID klienta desktop uzywanym przez upstream librespot.
/// Dlatego oba logowania sa dwoma roznymi kontami technicznymi i odlaczenie
/// jednego NIE MOZE dotknac drugiego.
///
/// Tokeny nigdy nie trafiaja do state.json, kopii zapasowych, dziennika
/// diagnostycznego, argumentow procesu, zmiennych srodowiskowych ani
/// repozytorium.
/// </summary>
internal interface ISpotifyLibrespotCredentialStore
{
    bool TryRead(out SpotifyTokenSet? tokens);

    void Write(SpotifyTokenSet tokens);

    void Delete();
}

internal sealed class SpotifyLibrespotCredentialStore : ISpotifyLibrespotCredentialStore
{
    /// <summary>Klucz produkcyjny natywnej sesji. NIGDY klucz SDK.</summary>
    internal const string NativeTargetName = "AccessibleMediaController/SpotifyLibrespot";

    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    private readonly string _targetName;

    public SpotifyLibrespotCredentialStore()
        : this(NativeTargetName)
    {
    }

    /// <summary>
    /// Nazwa docelowa jest parametrem WYLACZNIE dlatego, zeby test mogl uzyc
    /// wlasnego, losowego klucza tymczasowego. Produkcja uzywa konstruktora
    /// bezparametrowego, wiec test nigdy nie dotyka konta uzytkownika.
    /// </summary>
    internal SpotifyLibrespotCredentialStore(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            throw new ArgumentException("Nazwa docelowa poswiadczenia nie moze byc pusta.", nameof(targetName));
        _targetName = targetName;
    }

    internal string TargetName => _targetName;

    public bool TryRead(out SpotifyTokenSet? tokens)
    {
        tokens = null;
        if (!CredRead(_targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound) return false;
            throw new Win32Exception(
                error,
                "Nie udalo sie odczytac logowania natywnej sesji Spotify z Menedzera poswiadczen Windows.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return false;
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            tokens = JsonSerializer.Deserialize<SpotifyTokenSet>(Encoding.UTF8.GetString(bytes));
            return tokens is not null
                   && !string.IsNullOrWhiteSpace(tokens.AccessToken)
                   && !string.IsNullOrWhiteSpace(tokens.RefreshToken)
                   && !string.IsNullOrWhiteSpace(tokens.ClientId);
        }
        catch (JsonException)
        {
            // Uszkodzony wpis nie moze udawac zalogowanego konta. Kasujemy TYLKO
            // klucz natywny; logowanie Web API zostaje nietknięte.
            Delete();
            return false;
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Write(SpotifyTokenSet tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens));
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = _targetName,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = "Spotify Librespot"
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Nie udalo sie bezpiecznie zapisac logowania natywnej sesji Spotify.");
            }
        }
        finally
        {
            // Bufor z tokenem zawsze zwalniany, takze po bledzie zapisu.
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public void Delete()
    {
        if (CredDelete(_targetName, CredentialTypeGeneric, 0)) return;
        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
        {
            throw new Win32Exception(error, "Nie udalo sie usunac logowania natywnej sesji Spotify.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}
