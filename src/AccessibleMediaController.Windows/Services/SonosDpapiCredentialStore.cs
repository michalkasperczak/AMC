using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AccessibleMediaController.Core.Sonos;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// TRWALY magazyn poswiadczen Sonos: JEDEN plik zaszyfrowany DPAPI BIEZACEGO
/// UZYTKOWNIKA Windows, poza state.json i poza jego kopiami.
///
/// Dlaczego plik, a nie Menedzer poswiadczen jak w TIDAL/Librespot: zestaw
/// tokenow Sonos moze byc duzy, a blob CredWrite ma scisly limit rozmiaru.
/// Zamiast go obchodzic, trzymamy caly rekord w jednym pliku i szyfrujemy go
/// natywnym DPAPI.
///
/// Granice, ktorych tu nie wolno przekroczyc:
///   * DPAPI CurrentUser, NIGDY LocalMachine; zawsze CRYPTPROTECT_UI_FORBIDDEN,
///     wiec zaden monit nie zablokuje procesu,
///   * stala entropia domeny (AMC/Sonos/wersja) dodatkowo wiaze plik z TYM
///     przeznaczeniem - blob z innego miejsca sie nie odszyfruje,
///   * szyfrujemy PRZED jakimkolwiek I/O; na dysk NIGDY nie trafia plaintext,
///     takze nie do pliku tymczasowego - nie ma zadnego plaintext fallbacku,
///   * zapis jest atomowy: unikalny temp w TYM SAMYM folderze, pelny flush,
///     potem File.Replace istniejacego albo File.Move pierwszego. Nigdy
///     Delete+Write, wiec nieudany zapis ZOSTAWIA poprzedni plik,
///   * odczyt i walidacja NIGDY nie kasuja zepsutego pliku - to swiadome
///     odejscie od wzorca TidalCredentialStore. Usuwa tylko jawne Delete(),
///   * bufory native zwalniane w finally, plaintextowe tablice bajtow zerowane
///     po uzyciu. Nie obiecujemy wymazania niemutowalnych stringow C#,
///   * zero PowerShella, CLI i zmiennych srodowiskowych z tokenami - DPAPI
///     wolane natywnie w procesie,
///   * zaden komunikat i zaden wyjatek nie cytuje tokenu ani sciezki.
///
/// Wlascicielem operacji bedzie JEDNA instancja przyszlego koordynatora; nie ma
/// tu blokad wieloprocesowych ani ogolnego frameworka plikow.
/// </summary>
internal sealed class SonosDpapiCredentialStore : ISonosCredentialStore
{
    /// <summary>
    /// Stala entropia domeny: AMC + Sonos + wersja formatu. Nie jest sekretem -
    /// wiaze blob z tym przeznaczeniem, a ochrona pochodzi z DPAPI uzytkownika.
    /// </summary>
    private static readonly byte[] DomainEntropy =
        Encoding.UTF8.GetBytes("AccessibleMediaController/Sonos/credentials/v1");

    private const uint CryptprotectUiForbidden = 0x1;

    private readonly SonosLoginBrokerConfiguration _broker;

    /// <summary>
    /// Konstruktor NIE czyta zapisanych kont i nie dotyka dysku - tylko ustala
    /// docelowa sciezke.
    /// </summary>
    public SonosDpapiCredentialStore(SonosLoginBrokerConfiguration broker, string filePath)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _broker = broker;
        FilePath = filePath;
    }

    public string FilePath { get; }

    /// <summary>
    /// Domyslna sciezka: osobny podkatalog credentials, celowo NIE obok
    /// state.json ani jego kopii zapasowych.
    /// </summary>
    public static string DefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AccessibleMediaController",
            "credentials",
            "sonos.bin");

    public SonosCredentialReadOutcome Read()
    {
        byte[] encrypted;
        try
        {
            using var stream = new FileStream(
                FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            // Kontrola rozmiaru PRZED alokacja bufora.
            var length = stream.Length;
            if (length <= 0 || length > SonosCredentialPolicy.MaxEncryptedFileBytes)
            {
                return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Invalid);
            }

            encrypted = new byte[(int)length];
            var offset = 0;
            while (offset < encrypted.Length)
            {
                var read = stream.Read(encrypted, offset, encrypted.Length - offset);
                if (read <= 0)
                {
                    return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.ReadFailure);
                }

                offset += read;
            }
        }
        catch (FileNotFoundException)
        {
            return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing);
        }
        catch (DirectoryNotFoundException)
        {
            return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Missing);
        }
        catch (IOException)
        {
            return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.ReadFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.ReadFailure);
        }

        byte[]? plaintext = null;
        try
        {
            if (!TryUnprotect(encrypted, out plaintext) || plaintext is null)
            {
                // Uszkodzony albo obcy blob. NIE kasujemy pliku.
                return SonosCredentialReadOutcome.Failure(SonosCredentialReadStatus.Invalid);
            }

            var status = SonosCredentialSerializer.TryDeserialize(plaintext, _broker, out var credentials);
            return status == SonosCredentialReadStatus.Success && credentials is not null
                ? SonosCredentialReadOutcome.Ok(credentials)
                : SonosCredentialReadOutcome.Failure(status);
        }
        finally
        {
            if (plaintext is not null)
            {
                Array.Clear(plaintext);
            }
        }
    }

    public SonosCredentialWriteOutcome Write(SonosStoredCredentials credentials)
    {
        if (credentials is null)
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.InvalidRecord);
        }

        var plaintext = SonosCredentialSerializer.TrySerialize(credentials);
        if (plaintext is null)
        {
            // Zle wejscie NIE nadpisuje poprzedniego zapisu.
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.InvalidRecord);
        }

        byte[]? encrypted;
        try
        {
            // Szyfrowanie PRZED I/O: na dysk nie ma jak trafic plaintext.
            if (!TryProtect(plaintext, out encrypted) || encrypted is null)
            {
                return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
            }
        }
        finally
        {
            Array.Clear(plaintext);
        }

        if (encrypted.Length > SonosCredentialPolicy.MaxEncryptedFileBytes)
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.InvalidRecord);
        }

        return WriteAtomically(encrypted);
    }

    private SonosCredentialWriteOutcome WriteAtomically(byte[] encrypted)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrEmpty(directory))
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        }

        string temporaryPath;
        try
        {
            Directory.CreateDirectory(directory);
            // Unikalny temp w TYM SAMYM folderze, zeby podmiana byla atomowa.
            temporaryPath = Path.Combine(
                directory,
                Path.GetFileName(FilePath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        }
        catch (IOException)
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        }

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(encrypted, 0, encrypted.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(FilePath))
            {
                // Atomowa podmiana BEZ pliku .bak i bez Delete+Write.
                File.Replace(temporaryPath, FilePath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, FilePath);
            }

            return SonosCredentialWriteOutcome.Ok();
        }
        catch (IOException)
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        }
        catch (UnauthorizedAccessException)
        {
            return SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
        }
        finally
        {
            // Nieudany zapis nie zostawia smiecia; poprzedni plik jest nietkniety.
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// JAWNE usuniecie zapisanych poswiadczen. Idempotentne: brak pliku to
    /// rowniez sukces. Jedyne miejsce w tej klasie, ktore kasuje dane.
    /// </summary>
    public bool Delete()
    {
        try
        {
            File.Delete(FilePath);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryProtect(byte[] plaintext, out byte[]? encrypted) =>
        TryTransform(plaintext, protect: true, out encrypted);

    private static bool TryUnprotect(byte[] encrypted, out byte[]? plaintext) =>
        TryTransform(encrypted, protect: false, out plaintext);

    /// <summary>
    /// Jedno wejscie do natywnego DPAPI dla obu kierunkow: te same flagi, ta
    /// sama entropia i to samo zwalnianie buforow w finally.
    /// </summary>
    private static bool TryTransform(byte[] input, bool protect, out byte[]? output)
    {
        output = null;
        var inputBlob = default(DataBlob);
        var entropyBlob = default(DataBlob);
        var outputBlob = default(DataBlob);
        try
        {
            inputBlob = Allocate(input);
            entropyBlob = Allocate(DomainEntropy);

            var ok = protect
                ? CryptProtectData(
                    ref inputBlob,
                    null,
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    ref outputBlob)
                : CryptUnprotectData(
                    ref inputBlob,
                    IntPtr.Zero,
                    ref entropyBlob,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptprotectUiForbidden,
                    ref outputBlob);

            if (!ok || outputBlob.DataPointer == IntPtr.Zero)
            {
                return false;
            }

            // Kontrola rozmiaru PRZED alokacja zarzadzanego bufora.
            var limit = protect
                ? SonosCredentialPolicy.MaxEncryptedFileBytes
                : SonosCredentialPolicy.MaxPlaintextBytes;
            if (outputBlob.DataLength == 0 || outputBlob.DataLength > limit)
            {
                return false;
            }

            var result = new byte[outputBlob.DataLength];
            Marshal.Copy(outputBlob.DataPointer, result, 0, result.Length);
            output = result;
            return true;
        }
        catch (OutOfMemoryException)
        {
            return false;
        }
        finally
        {
            FreeNative(ref inputBlob, zero: true);
            FreeNative(ref entropyBlob, zero: false);
            FreeLocal(ref outputBlob, zero: !protect);
        }
    }

    private static DataBlob Allocate(byte[] data)
    {
        var pointer = Marshal.AllocHGlobal(Math.Max(data.Length, 1));
        Marshal.Copy(data, 0, pointer, data.Length);
        return new DataBlob { DataLength = (uint)data.Length, DataPointer = pointer };
    }

    private static void FreeNative(ref DataBlob blob, bool zero)
    {
        if (blob.DataPointer == IntPtr.Zero)
        {
            return;
        }

        if (zero && blob.DataLength > 0)
        {
            Zero(blob.DataPointer, blob.DataLength);
        }

        Marshal.FreeHGlobal(blob.DataPointer);
        blob.DataPointer = IntPtr.Zero;
        blob.DataLength = 0;
    }

    private static void FreeLocal(ref DataBlob blob, bool zero)
    {
        if (blob.DataPointer == IntPtr.Zero)
        {
            return;
        }

        if (zero && blob.DataLength > 0)
        {
            Zero(blob.DataPointer, blob.DataLength);
        }

        // DPAPI oddaje bufor z LocalAlloc.
        LocalFree(blob.DataPointer);
        blob.DataPointer = IntPtr.Zero;
        blob.DataLength = 0;
    }

    private static void Zero(IntPtr pointer, uint length)
    {
        for (uint index = 0; index < length; index++)
        {
            Marshal.WriteByte(pointer, (int)index, 0);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public uint DataLength;
        public IntPtr DataPointer;
    }

    /// <summary>
    /// Brak flagi CRYPTPROTECT_LOCAL_MACHINE oznacza zakres BIEZACEGO
    /// UZYTKOWNIKA - dokladnie tego chcemy.
    /// </summary>
    [DllImport("crypt32.dll", EntryPoint = "CryptProtectData", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob input,
        string? description,
        ref DataBlob entropy,
        IntPtr reserved,
        IntPtr prompt,
        uint flags,
        ref DataBlob output);

    [DllImport("crypt32.dll", EntryPoint = "CryptUnprotectData", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob input,
        IntPtr description,
        ref DataBlob entropy,
        IntPtr reserved,
        IntPtr prompt,
        uint flags,
        ref DataBlob output);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);
}
