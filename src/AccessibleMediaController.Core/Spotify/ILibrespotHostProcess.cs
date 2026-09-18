using System.Diagnostics;
using System.Text;

namespace AccessibleMediaController.Core.Spotify;

/// <summary>
/// Uruchomiony proces hosta Librespot widziany przez transport. Osobny
/// interfejs istnieje po to, zeby testy mogly podstawic wlasny proces albo
/// atrape strumieni - bez WPF, bez konta i bez czekania na prawdziwy silnik.
/// </summary>
public interface ILibrespotHostProcess : IDisposable
{
    /// <summary>Wejscie hosta. Tylko tedy wolno przekazac token.</summary>
    TextWriter StandardInput { get; }

    /// <summary>Wyjscie hosta. Wylacznie linie JSON.</summary>
    TextReader StandardOutput { get; }

    bool HasExited { get; }

    /// <summary>Kod wyjscia albo null, gdy proces jeszcze zyje.</summary>
    int? ExitCode { get; }

    void Kill();

    Task WaitForExitAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Prawdziwy proces hosta Librespot. Trzymamy go w Core, bo nie potrzebuje
/// niczego z WPF: to zwykly <see cref="Process"/> z przekierowanymi strumieniami.
///
/// TOKEN NIE MOZE TU WEJSC. Argumenty i zmienne srodowiskowe sa sprawdzane
/// bezpiecznikiem, bo argv widzi kazdy uzytkownik systemu w liscie procesow.
/// </summary>
public sealed class LibrespotHostProcess : ILibrespotHostProcess
{
    private readonly Process process;
    private readonly StringBuilder standardError = new();
    private readonly object errorGate = new();
    private bool disposed;

    private LibrespotHostProcess(Process process)
    {
        this.process = process;
    }

    /// <summary>
    /// Uruchamia osobny plik wykonywalny hosta. UseShellExecute=false, bez okna,
    /// wszystkie trzy strumienie przekierowane.
    /// </summary>
    public static LibrespotHostProcess Start(
        string executablePath,
        IReadOnlyList<string>? arguments = null,
        string? workingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!File.Exists(executablePath))
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.StartFailed,
                "Nie znaleziono pliku hosta Librespot w instalacji AMC.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Path.GetDirectoryName(executablePath) ?? string.Empty
                : workingDirectory
        };
        foreach (var argument in arguments ?? [])
        {
            GuardAgainstSecret(argument);
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            var process = Process.Start(startInfo)
                ?? throw new LibrespotHostException(
                    LibrespotHostErrorCodes.StartFailed,
                    "Nie udało się uruchomić procesu hosta Librespot.");
            var host = new LibrespotHostProcess(process);
            // stderr czytamy, zeby nie zablokowac hosta pelnym buforem potoku.
            // Do dziennika nie idzie jego TRESC - tylko dlugosc i kod wyjscia.
            process.ErrorDataReceived += host.OnErrorDataReceived;
            process.BeginErrorReadLine();
            return host;
        }
        catch (Exception exception) when (exception is not LibrespotHostException)
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.StartFailed,
                "Nie udało się uruchomić procesu hosta Librespot.",
                exception);
        }
    }

    /// <summary>
    /// Bezpiecznik wycieku: cokolwiek wygladajacego na token konta nie moze
    /// trafic do argv, bo lista procesow jest widoczna dla innych programow.
    /// </summary>
    public static void GuardAgainstSecret(string argument)
    {
        var value = argument ?? string.Empty;
        var wygladaNaToken = value.Length >= 40
            && value.All(character => char.IsLetterOrDigit(character)
                || character is '-' or '_' or '.' or '~');
        if (wygladaNaToken
            || value.Contains("accessToken", StringComparison.OrdinalIgnoreCase)
            || value.Contains("access_token", StringComparison.OrdinalIgnoreCase))
        {
            throw new LibrespotHostException(
                LibrespotHostErrorCodes.TokenLeakGuard,
                "Odmówiono uruchomienia hosta: token konta nie może trafić do argumentów procesu.");
        }
    }

    public TextWriter StandardInput => process.StandardInput;
    public TextReader StandardOutput => process.StandardOutput;

    public bool HasExited
    {
        get
        {
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    public int? ExitCode
    {
        get
        {
            try
            {
                return process.HasExited ? process.ExitCode : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    /// <summary>Liczba znakow stderr - sama miara, bez tresci.</summary>
    public int StandardErrorLength
    {
        get { lock (errorGate) return standardError.Length; }
    }

    public void Kill()
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or NotSupportedException
            or System.ComponentModel.Win32Exception)
        {
        }
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        lock (errorGate)
        {
            // Bufor ograniczony: host nie moze zapchac pamieci AMC diagnostyka.
            if (standardError.Length > 8192) return;
            standardError.Append(e.Data.Length);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Kill();
        try
        {
            process.ErrorDataReceived -= OnErrorDataReceived;
        }
        catch (InvalidOperationException)
        {
        }
        process.Dispose();
    }
}
