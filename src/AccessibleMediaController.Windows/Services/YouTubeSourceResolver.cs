using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Windows.Services;

internal sealed record ResolvedYouTubeAudioSource(
    string PageUrl,
    string StreamUrl,
    string Title,
    bool IsLive,
    bool IsHls,
    string? Codec,
    int? BitrateKbps);

/// <summary>
/// Resolves a stable public YouTube page address to one temporary audio URL.
/// The signed URL is never persisted and no browser cookies are read.
/// </summary>
internal static class YouTubeSourceResolver
{
    private const int MaximumJsonCharacters = 8 * 1024 * 1024;
    private static readonly string[] YouTubeHosts =
    [
        "youtube.com",
        "youtu.be",
        "youtube-nocookie.com"
    ];

    internal static bool IsYouTubeUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        return YouTubeHosts.Any(candidate =>
            host.Equals(candidate, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith('.' + candidate, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<ResolvedYouTubeAudioSource> ResolveLiveAudioAsync(
        string pageUrl,
        CancellationToken cancellationToken)
    {
        if (!IsYouTubeUrl(pageUrl))
            throw new InvalidDataException("Adres nie prowadzi do YouTube.");

        var executable = FindExecutable();
        if (executable is null)
        {
            throw new NotSupportedException(
                "Odtwarzanie transmisji YouTube wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź aktualizacje i składniki.");
        }

        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var argument in new[]
        {
            "--ignore-config",
            "--no-playlist",
            "--no-warnings",
            "--no-live-from-start",
            "--force-ipv4",
            "--socket-timeout", "20",
            "--retries", "3",
            "--fragment-retries", "3",
            "--extractor-retries", "2",
            "--format", "bestaudio[ext=m4a]/bestaudio/best[acodec!=none]",
            "--dump-single-json",
            "--",
            pageUrl.Trim()
        })
        {
            start.ArgumentList.Add(argument);
        }

        Process? process = null;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.");
            process.StandardInput.Close();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(75));
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
            _ = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new InvalidDataException("YouTube nie udostępnił obecnie publicznego strumienia audio.");
            if (output.Length == 0 || output.Length > MaximumJsonCharacters)
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane źródła.");
            return ParseResult(pageUrl.Trim(), output, requireLive: true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("YouTube nie odpowiedział w bezpiecznym czasie.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            process?.Dispose();
        }
    }

    internal static ResolvedYouTubeAudioSource ParseResult(
        string pageUrl,
        string json,
        bool requireLive)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                MaxDepth = 64
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane źródła.");
            var root = document.RootElement;
            var liveStatus = ReadString(root, "live_status");
            var isLive = ReadBoolean(root, "is_live")
                         || liveStatus.Equals("is_live", StringComparison.OrdinalIgnoreCase);
            if (requireLive && !isLive)
            {
                throw new InvalidDataException(
                    "Ten adres YouTube nie jest obecnie transmisją na żywo. Zwykłe filmy będą obsługiwane w module Media internetowe.");
            }

            var selected = SelectAudio(root);
            if (selected is null)
                throw new InvalidDataException("YouTube nie udostępnił obsługiwanego strumienia audio.");
            var streamUrl = ReadString(selected.Value, "url");
            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out var streamUri)
                || streamUri.Scheme is not ("http" or "https")
                || !string.IsNullOrWhiteSpace(streamUri.UserInfo))
            {
                throw new InvalidDataException("YouTube zwrócił nieprawidłowy adres strumienia audio.");
            }

            var protocol = ReadString(selected.Value, "protocol");
            var extension = ReadString(selected.Value, "ext");
            var isHls = protocol.Contains("m3u8", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals("m3u8", StringComparison.OrdinalIgnoreCase)
                        || streamUri.Host.Equals("manifest.googlevideo.com", StringComparison.OrdinalIgnoreCase)
                        || streamUri.AbsolutePath.Contains("/manifest/hls_", StringComparison.OrdinalIgnoreCase);
            var title = ReadString(root, "title").Trim();
            if (title.Length == 0) title = "YouTube na żywo";
            if (title.Length > 500) title = title[..500].Trim();
            var codec = ReadString(selected.Value, "acodec");
            if (codec.Equals("none", StringComparison.OrdinalIgnoreCase)) codec = string.Empty;
            var bitrate = ReadNumber(selected.Value, "abr");
            var normalizedBitrate = bitrate is > 0 and < 10_000
                ? (int?)Math.Round(bitrate.Value)
                : null;
            return new ResolvedYouTubeAudioSource(
                pageUrl,
                streamUri.AbsoluteUri,
                title,
                isLive,
                isHls,
                codec.Length == 0 ? null : codec,
                normalizedBitrate);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("YouTube zwrócił nieprawidłowe dane źródła.", exception);
        }
    }

    private static JsonElement? SelectAudio(JsonElement root)
    {
        foreach (var property in new[] { "requested_downloads", "requested_formats" })
        {
            if (!root.TryGetProperty(property, out var container)
                || container.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var candidate in container.EnumerateArray())
            {
                if (IsUsableAudio(candidate)) return candidate;
            }
        }
        return IsUsableAudio(root) ? root : null;
    }

    private static bool IsUsableAudio(JsonElement candidate)
    {
        if (candidate.ValueKind != JsonValueKind.Object
            || string.IsNullOrWhiteSpace(ReadString(candidate, "url")))
        {
            return false;
        }
        var audioCodec = ReadString(candidate, "acodec");
        if (audioCodec.Equals("none", StringComparison.OrdinalIgnoreCase)) return false;
        var videoCodec = ReadString(candidate, "vcodec");
        return videoCodec.Length == 0 || videoCodec.Equals("none", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True;

    private static string ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static double? ReadNumber(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
            ? number
            : null;

    internal static string? FindExecutable()
    {
        var managed = YtDlpComponentManager.FindInstalledExecutable();
        if (managed is not null) return managed;
        var bundled = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        if (File.Exists(bundled)) return bundled;
        var configured = Environment.GetEnvironmentVariable("YTDLP_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory, "yt-dlp.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception exception) when (exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
            {
            }
        }
        return null;
    }
}
