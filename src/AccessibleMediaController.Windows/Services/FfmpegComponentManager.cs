using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Windows.Services;

internal sealed record FfmpegComponentStatus(
    bool Installed,
    string? Version,
    string? ExecutablePath,
    string? Sha256,
    DateTimeOffset? CheckedAtUtc)
{
    internal string UserFacingText => Installed
        ? $"FFmpeg {Version ?? "wersja nieznana"}, zainstalowany i zweryfikowany"
        : "FFmpeg nie jest jeszcze zainstalowany przez AMC";
}

internal sealed record FfmpegComponentUpdateResult(
    bool Success,
    bool Changed,
    FfmpegComponentStatus Status,
    string Message);

/// <summary>
/// Installs the stable win64 LGPL shared FFmpeg build into AMC local data.
/// The publisher is linked by ffmpeg.org. Every archive is matched against
/// the release checksum before extraction and is validated before activation.
/// </summary>
internal static class FfmpegComponentManager
{
    internal const string AssetName = "ffmpeg-n9.0-latest-win64-lgpl-shared-9.0.zip";
    internal static readonly Uri ArchiveUri = new(
        $"https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/{AssetName}");
    internal static readonly Uri ChecksumsUri = new(
        "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256");
    internal static readonly Uri ProviderUri = new("https://github.com/BtbN/FFmpeg-Builds");
    private const long MaximumArchiveBytes = 220L * 1024 * 1024;
    private const long MaximumExtractedBytes = 1_200L * 1024 * 1024;
    private const int MaximumArchiveEntries = 8_000;
    private static readonly SemaphoreSlim UpdateGate = new(1, 1);
    private static readonly HttpClient Client = CreateClient();

    private static string ComponentRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AccessibleMediaController",
        "components",
        "ffmpeg");

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
            DiagnosticLog.Warning("ffmpeg-component", $"Nie udało się odczytać aktywnego komponentu: {exception.Message}");
            return null;
        }
    }

    internal static FfmpegComponentStatus GetStatus()
    {
        try
        {
            var state = LoadState();
            var executable = FindInstalledExecutable();
            return state is not null && executable is not null
                ? new FfmpegComponentStatus(
                    true,
                    state.Version,
                    executable,
                    state.Sha256,
                    state.CheckedAtUtc)
                : new FfmpegComponentStatus(false, null, null, null, state?.CheckedAtUtc);
        }
        catch (Exception)
        {
            return new FfmpegComponentStatus(false, null, null, null, null);
        }
    }

    internal static async Task<FfmpegComponentUpdateResult> CheckAndUpdateAsync(
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
                ?? throw new InvalidDataException("Plik sum FFmpeg nie zawiera oczekiwanego pakietu Windows LGPL.");
            var existing = LoadState();
            var existingExecutable = FindInstalledExecutable();
            if (existingExecutable is not null
                && string.Equals(existing?.Sha256, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                existing!.CheckedAtUtc = DateTimeOffset.UtcNow;
                SaveState(existing);
                var current = GetStatus();
                progress?.Report(1d);
                return new FfmpegComponentUpdateResult(
                    true,
                    false,
                    current,
                    $"{current.UserFacingText}. Nie ma nowszej wersji.");
            }

            if (!installAvailable)
            {
                var status = GetStatus();
                return new FfmpegComponentUpdateResult(
                    true,
                    false,
                    status,
                    status.Installed
                        ? "Dostępna jest aktualizacja FFmpeg. Automatyczne pobieranie jest wyłączone."
                        : "FFmpeg jest dostępny do pobrania. Automatyczne pobieranie jest wyłączone.");
            }

            var stagingRoot = Path.Combine(ComponentRoot, $".staging-{Guid.NewGuid():N}");
            var archivePath = Path.Combine(stagingRoot, AssetName);
            var extractRoot = Path.Combine(stagingRoot, "extracted");
            try
            {
                Directory.CreateDirectory(stagingRoot);
                await DownloadArchiveAsync(ArchiveUri, archivePath, progress, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var actualHash = await ComputeSha256Async(archivePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "Pobrany pakiet FFmpeg ma inną sumę SHA-256 niż pakiet opublikowany przez dostawcę.");
                }

                progress?.Report(0.83d);
                ExtractSafely(archivePath, extractRoot, cancellationToken);
                var stagedExecutable = FindArchiveExecutable(extractRoot)
                    ?? throw new InvalidDataException("Zweryfikowany pakiet FFmpeg nie zawiera ffmpeg.exe.");
                var validation = await ValidateExecutableAsync(stagedExecutable, cancellationToken)
                    .ConfigureAwait(false);
                var releaseId = expectedHash[..16].ToLowerInvariant();
                var releasesRoot = Path.Combine(ComponentRoot, "releases");
                var releaseDirectory = Path.Combine(releasesRoot, releaseId);
                Directory.CreateDirectory(releasesRoot);
                if (Directory.Exists(releaseDirectory))
                {
                    Directory.Delete(releaseDirectory, recursive: true);
                }
                Directory.Move(extractRoot, releaseDirectory);
                var relativeExecutable = Path.GetRelativePath(
                    ComponentRoot,
                    Path.Combine(releaseDirectory, Path.GetRelativePath(extractRoot, stagedExecutable)));
                var state = new FfmpegComponentState
                {
                    AssetName = AssetName,
                    Version = validation.Version,
                    Sha256 = expectedHash.ToLowerInvariant(),
                    ExecutableRelativePath = relativeExecutable,
                    InstalledAtUtc = DateTimeOffset.UtcNow,
                    CheckedAtUtc = DateTimeOffset.UtcNow,
                    PackageUri = ArchiveUri.AbsoluteUri,
                    SourceUri = ProviderUri.AbsoluteUri,
                    License = "LGPL-2.1-or-later"
                };
                SaveState(state);
                progress?.Report(0.98d);
                CleanupOldReleases(releaseDirectory);
                var installed = GetStatus();
                if (!installed.Installed)
                    throw new InvalidDataException("Nowa wersja FFmpeg przeszła kontrolę, ale nie została uaktywniona.");
                progress?.Report(1d);
                DiagnosticLog.Info(
                    "ffmpeg-component",
                    $"Zainstalowano {installed.Version}; SHA-256 {expectedHash}; źródło {ArchiveUri}.");
                return new FfmpegComponentUpdateResult(
                    true,
                    true,
                    installed,
                    $"Zainstalowano i zweryfikowano FFmpeg {installed.Version}.");
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
            or System.ComponentModel.Win32Exception
            or TimeoutException)
        {
            DiagnosticLog.Error("ffmpeg-component", "Aktualizacja FFmpeg nie powiodła się.", exception);
            return new FfmpegComponentUpdateResult(
                false,
                false,
                GetStatus(),
                $"Nie udało się bezpiecznie zaktualizować FFmpeg: {exception.Message}");
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
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    internal static bool IsSafeArchiveDestination(string extractionRoot, string candidate)
    {
        var root = Path.GetFullPath(extractionRoot);
        var path = Path.GetFullPath(candidate);
        return IsWithin(path, root);
    }

    private static async Task<string> DownloadTextAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > 1_048_576)
            throw new InvalidDataException("Plik sum FFmpeg jest nieoczekiwanie duży.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(source, Encoding.UTF8, true, 8_192, leaveOpen: false);
        var value = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (value.Length > 1_048_576) throw new InvalidDataException("Plik sum FFmpeg jest nieoczekiwanie duży.");
        return value;
    }

    private static async Task DownloadArchiveAsync(
        Uri uri,
        string destination,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        if (length is > MaximumArchiveBytes)
            throw new InvalidDataException("Pakiet FFmpeg przekracza bezpieczny limit rozmiaru.");
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
            if (total > MaximumArchiveBytes)
                throw new InvalidDataException("Pakiet FFmpeg przekracza bezpieczny limit rozmiaru.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (length is > 0) progress?.Report(0.05d + 0.75d * total / length.Value);
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

    private static void ExtractSafely(string archivePath, string extractionRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(extractionRoot);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new InvalidDataException("Pakiet FFmpeg zawiera nieoczekiwanie dużo plików.");
        long extractedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Length < 0 || entry.Length > MaximumExtractedBytes - extractedBytes)
                throw new InvalidDataException("Rozpakowany FFmpeg przekracza bezpieczny limit rozmiaru.");
            extractedBytes += entry.Length;
            var destination = Path.GetFullPath(Path.Combine(extractionRoot, entry.FullName));
            if (!IsSafeArchiveDestination(extractionRoot, destination))
                throw new InvalidDataException("Pakiet FFmpeg zawiera niebezpieczną ścieżkę pliku.");
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destination);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: false);
        }
    }

    private static string? FindArchiveExecutable(string extractionRoot) =>
        Directory.EnumerateFiles(extractionRoot, "ffmpeg.exe", SearchOption.AllDirectories)
            .FirstOrDefault(path => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(path)),
                "bin",
                StringComparison.OrdinalIgnoreCase));

    private static async Task<(string Version, string Configuration)> ValidateExecutableAsync(
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
        start.ArgumentList.Add("-hide_banner");
        start.ArgumentList.Add("-version");
        using var process = Process.Start(start)
            ?? throw new InvalidDataException("Nie udało się uruchomić pobranego ffmpeg.exe.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
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
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        var combined = output + Environment.NewLine + error;
        if (process.ExitCode != 0 || !combined.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Pobrany program nie przeszedł kontroli uruchomienia FFmpeg.");
        if (combined.Contains("--enable-gpl", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("--enable-nonfree", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Pobrany FFmpeg nie jest oczekiwanym wariantem LGPL.");
        }
        var firstLine = combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase));
        var marker = firstLine.IndexOf("ffmpeg version", StringComparison.OrdinalIgnoreCase);
        var afterMarker = firstLine[(marker + "ffmpeg version".Length)..].Trim();
        var version = afterMarker.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? "wersja nieznana";
        return (version, combined);
    }

    private static FfmpegComponentState? LoadState()
    {
        if (!File.Exists(StatePath)) return null;
        return JsonSerializer.Deserialize<FfmpegComponentState>(File.ReadAllText(StatePath));
    }

    private static void SaveState(FfmpegComponentState state)
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
        {
            TryDeleteDirectory(directory);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            var root = Path.GetFullPath(ComponentRoot);
            var candidate = Path.GetFullPath(path);
            if (IsWithin(candidate, root) && Directory.Exists(candidate))
                Directory.Delete(candidate, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Warning("ffmpeg-component", $"Nie udało się jeszcze usunąć starego katalogu: {exception.Message}");
        }
    }

    private static bool IsWithin(string candidate, string root) =>
        candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 8
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(12)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMediaController/0.1 FFmpegComponentUpdater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
        return client;
    }

    private sealed class FfmpegComponentState
    {
        public string AssetName { get; set; } = string.Empty;
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
