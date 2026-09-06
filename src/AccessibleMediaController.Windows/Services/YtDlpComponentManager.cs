using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Windows.Services;

internal sealed record YtDlpComponentStatus(
    bool Installed,
    string? Version,
    string? ExecutablePath,
    string? Sha256,
    DateTimeOffset? CheckedAtUtc)
{
    internal string UserFacingText => Installed
        ? $"yt-dlp {Version ?? "wersja nieznana"}, zainstalowany i zweryfikowany"
        : "yt-dlp nie jest jeszcze zainstalowany przez AMC";
}

internal sealed record YtDlpComponentUpdateResult(
    bool Success,
    bool Changed,
    YtDlpComponentStatus Status,
    string Message);

/// <summary>
/// Installs the official Windows yt-dlp executable as an isolated, replaceable
/// AMC component. The executable is activated only after its release checksum
/// and a bounded version probe have both succeeded.
/// </summary>
internal static class YtDlpComponentManager
{
    internal const string AssetName = "yt-dlp.exe";
    internal static readonly Uri ExecutableUri = new(
        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");
    internal static readonly Uri ChecksumsUri = new(
        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS");
    internal static readonly Uri ProviderUri = new("https://github.com/yt-dlp/yt-dlp");
    private const long MaximumExecutableBytes = 80L * 1024 * 1024;
    private static readonly SemaphoreSlim UpdateGate = new(1, 1);
    private static readonly HttpClient Client = CreateClient();

    private static string ComponentRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AccessibleMediaController",
        "components",
        "yt-dlp");

    private static string StatePath => Path.Combine(ComponentRoot, "current.json");

    internal static string? FindInstalledExecutable()
    {
        try
        {
            var state = LoadState();
            if (state is null || string.IsNullOrWhiteSpace(state.ExecutableRelativePath)) return null;
            var root = Path.GetFullPath(ComponentRoot);
            var candidate = Path.GetFullPath(Path.Combine(root, state.ExecutableRelativePath));
            if (!IsWithin(candidate, root) || !File.Exists(candidate)) return null;
            return candidate;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or NotSupportedException)
        {
            DiagnosticLog.Warning(
                "yt-dlp-component",
                $"Nie udało się odczytać aktywnego komponentu: {exception.Message}");
            return null;
        }
    }

    internal static YtDlpComponentStatus GetStatus()
    {
        try
        {
            var state = LoadState();
            var executable = FindInstalledExecutable();
            return state is not null && executable is not null
                ? new YtDlpComponentStatus(
                    true,
                    state.Version,
                    executable,
                    state.Sha256,
                    state.CheckedAtUtc)
                : new YtDlpComponentStatus(false, null, null, null, state?.CheckedAtUtc);
        }
        catch (Exception)
        {
            return new YtDlpComponentStatus(false, null, null, null, null);
        }
    }

    internal static async Task<YtDlpComponentUpdateResult> CheckAndUpdateAsync(
        bool installAvailable,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await UpdateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(ComponentRoot);
            CleanupAbandonedStagingDirectories();
            progress?.Report(0.01d);
            var checksumText = await DownloadTextAsync(ChecksumsUri, cancellationToken).ConfigureAwait(false);
            var expectedHash = ParseChecksum(checksumText, AssetName)
                ?? throw new InvalidDataException("Plik sum yt-dlp nie zawiera programu dla Windows.");
            var existing = LoadState();
            var existingExecutable = FindInstalledExecutable();
            if (existingExecutable is not null
                && string.Equals(existing?.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                existing!.CheckedAtUtc = DateTimeOffset.UtcNow;
                SaveState(existing);
                var current = GetStatus();
                progress?.Report(1d);
                return new YtDlpComponentUpdateResult(
                    true,
                    false,
                    current,
                    $"{current.UserFacingText}. Nie ma nowszej wersji.");
            }

            if (!installAvailable)
            {
                var status = GetStatus();
                return new YtDlpComponentUpdateResult(
                    true,
                    false,
                    status,
                    status.Installed
                        ? "Dostępna jest aktualizacja yt-dlp. Automatyczne pobieranie jest wyłączone."
                        : "yt-dlp jest dostępny do pobrania. Automatyczne pobieranie jest wyłączone.");
            }

            var stagingRoot = Path.Combine(ComponentRoot, $".staging-{Guid.NewGuid():N}");
            var stagedExecutable = Path.Combine(stagingRoot, AssetName);
            try
            {
                Directory.CreateDirectory(stagingRoot);
                await DownloadExecutableAsync(
                        ExecutableUri,
                        stagedExecutable,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
                var actualHash = await ComputeSha256Async(stagedExecutable, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Pobrany yt-dlp ma inną sumę SHA-256 niż plik opublikowany przez dostawcę.");
                }

                progress?.Report(0.9d);
                var version = await ValidateExecutableAsync(stagedExecutable, cancellationToken)
                    .ConfigureAwait(false);
                var releaseId = expectedHash[..16].ToLowerInvariant();
                var releasesRoot = Path.Combine(ComponentRoot, "releases");
                var releaseDirectory = Path.Combine(releasesRoot, releaseId);
                Directory.CreateDirectory(releasesRoot);
                if (Directory.Exists(releaseDirectory)) Directory.Delete(releaseDirectory, recursive: true);
                Directory.CreateDirectory(releaseDirectory);
                var activeExecutable = Path.Combine(releaseDirectory, AssetName);
                File.Move(stagedExecutable, activeExecutable);
                var state = new YtDlpComponentState
                {
                    Version = version,
                    Sha256 = expectedHash.ToLowerInvariant(),
                    ExecutableRelativePath = Path.GetRelativePath(ComponentRoot, activeExecutable),
                    InstalledAtUtc = DateTimeOffset.UtcNow,
                    CheckedAtUtc = DateTimeOffset.UtcNow,
                    PackageUri = ExecutableUri.AbsoluteUri,
                    SourceUri = ProviderUri.AbsoluteUri,
                    License = "Unlicense"
                };
                SaveState(state);
                CleanupOldReleases(releaseDirectory);
                var installed = GetStatus();
                if (!installed.Installed)
                    throw new InvalidDataException("Nowa wersja yt-dlp przeszła kontrolę, ale nie została uaktywniona.");
                progress?.Report(1d);
                DiagnosticLog.Info(
                    "yt-dlp-component",
                    $"Zainstalowano {installed.Version}; SHA-256 {expectedHash}; źródło {ProviderUri}.");
                return new YtDlpComponentUpdateResult(
                    true,
                    true,
                    installed,
                    $"Zainstalowano i zweryfikowano yt-dlp {installed.Version}.");
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or InvalidOperationException
            or JsonException
            or Win32Exception
            or TimeoutException)
        {
            DiagnosticLog.Error("yt-dlp-component", "Aktualizacja yt-dlp nie powiodła się.", exception);
            return new YtDlpComponentUpdateResult(
                false,
                false,
                GetStatus(),
                $"Nie udało się bezpiecznie zaktualizować yt-dlp: {exception.Message}");
        }
        finally
        {
            UpdateGate.Release();
        }
    }

    internal static string? ParseChecksum(string contents, string assetName)
    {
        foreach (var line in contents.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            var name = parts[^1].TrimStart('*');
            if (!string.Equals(name, assetName, StringComparison.Ordinal)) continue;
            var hash = parts[0];
            return hash.Length == 64 && hash.All(Uri.IsHexDigit)
                ? hash.ToLowerInvariant()
                : null;
        }
        return null;
    }

    private static async Task<string> DownloadTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 1_048_576)
            throw new InvalidDataException("Plik sum yt-dlp jest nieoczekiwanie duży.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(source, Encoding.UTF8, true, 8_192, leaveOpen: false);
        var value = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (value.Length > 1_048_576)
            throw new InvalidDataException("Plik sum yt-dlp jest nieoczekiwanie duży.");
        return value;
    }

    private static async Task DownloadExecutableAsync(
        Uri uri,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        if (length is > MaximumExecutableBytes)
            throw new InvalidDataException("Pakiet yt-dlp przekracza bezpieczny limit rozmiaru.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[128 * 1024];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > MaximumExecutableBytes)
                throw new InvalidDataException("Pakiet yt-dlp przekracza bezpieczny limit rozmiaru.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (length is > 0) progress?.Report(0.05d + 0.8d * total / length.Value);
        }
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<string> ValidateExecutableAsync(
        string executable,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("--ignore-config");
        start.ArgumentList.Add("--version");
        using var process = Process.Start(start)
            ?? throw new InvalidDataException("Nie udało się uruchomić pobranego yt-dlp.exe.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var termination = timeout.Token.Register(
            static state =>
            {
                try
                {
                    var running = (Process)state!;
                    if (!running.HasExited) running.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                }
            },
            process);
        var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        var output = (await outputTask.ConfigureAwait(false)).Trim();
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0
            || output.Length is < 6 or > 80
            || output.Any(character => !(char.IsDigit(character) || character is '.' or '-')))
        {
            throw new InvalidDataException(
                string.IsNullOrWhiteSpace(error)
                    ? "Pobrany program nie przeszedł kontroli uruchomienia yt-dlp."
                    : "Pobrany program nie przeszedł kontroli uruchomienia yt-dlp.");
        }
        return output;
    }

    private static YtDlpComponentState? LoadState()
    {
        if (!File.Exists(StatePath)) return null;
        return JsonSerializer.Deserialize<YtDlpComponentState>(File.ReadAllText(StatePath));
    }

    private static void SaveState(YtDlpComponentState state)
    {
        Directory.CreateDirectory(ComponentRoot);
        var temporary = Path.Combine(ComponentRoot, $".current-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
            File.Move(temporary, StatePath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { }
        }
    }

    private static void CleanupOldReleases(string activeRelease)
    {
        var releasesRoot = Path.Combine(ComponentRoot, "releases");
        if (!Directory.Exists(releasesRoot)) return;
        foreach (var directory in Directory.EnumerateDirectories(releasesRoot))
        {
            if (string.Equals(
                    Path.GetFullPath(directory),
                    Path.GetFullPath(activeRelease),
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            TryDeleteDirectory(directory);
        }
    }

    private static void CleanupAbandonedStagingDirectories()
    {
        if (!Directory.Exists(ComponentRoot)) return;
        foreach (var directory in Directory.EnumerateDirectories(ComponentRoot, ".staging-*"))
            TryDeleteDirectory(directory);
    }

    private static bool IsWithin(string candidate, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning(
                "yt-dlp-component",
                $"Nie udało się jeszcze usunąć starego katalogu: {exception.Message}");
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMediaController/0.1 YtDlpComponentUpdater");
        return client;
    }

    private sealed class YtDlpComponentState
    {
        public string Version { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string ExecutableRelativePath { get; set; } = string.Empty;
        public DateTimeOffset InstalledAtUtc { get; set; }
        public DateTimeOffset CheckedAtUtc { get; set; }
        public string PackageUri { get; set; } = string.Empty;
        public string SourceUri { get; set; } = string.Empty;
        public string License { get; set; } = string.Empty;
    }
}
