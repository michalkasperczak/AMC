using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Podcasts;

namespace AccessibleMediaController.Windows.Services;

/// <summary>
/// Reads ID3 CHAP/CTOC and MP4/M4A chapter metadata through the verified
/// FFprobe shipped in the same managed FFmpeg component as ffmpeg.exe.
/// </summary>
internal static class EmbeddedMediaChapterReader
{
    private const int MaximumOutputCharacters = 2 * 1024 * 1024;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(15);

    internal static bool IsAvailable => FindFfprobe() is not null;

    internal static string? GetFileSignature(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}"
                : null;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return null;
        }
    }

    internal static async Task<IReadOnlyList<ProviderChapterPoint>> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var ffprobe = FindFfprobe();
        if (ffprobe is null || !File.Exists(path)) return [];
        var start = new ProcessStartInfo
        {
            FileName = ffprobe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[]
        {
            "-v", "error",
            "-print_format", "json",
            "-show_chapters",
            path
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Nie udało się uruchomić analizy rozdziałów.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);
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
        var outputTask = ReadBoundedAsync(process.StandardOutput, MaximumOutputCharacters, timeout.Token);
        var errorTask = ReadBoundedAsync(process.StandardError, 16 * 1024, timeout.Token);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidDataException(string.IsNullOrWhiteSpace(error)
                ? "Nie udało się odczytać rozdziałów z pliku."
                : $"Nie udało się odczytać rozdziałów z pliku: {error.Trim()}");
        return Parse(output);
    }

    internal static IReadOnlyList<ProviderChapterPoint> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("chapters", out var chapters)
            || chapters.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<ProviderChapterPoint>();
        var number = 0;
        foreach (var chapter in chapters.EnumerateArray())
        {
            if (result.Count >= ChapterIndex.MaximumProviderChaptersPerItem) break;
            number++;
            if (!TryGetStartSeconds(chapter, out var seconds)
                || !double.IsFinite(seconds)
                || seconds < 0
                || seconds > TimeSpan.MaxValue.TotalSeconds)
            {
                continue;
            }
            var title = string.Empty;
            if (chapter.TryGetProperty("tags", out var tags)
                && tags.ValueKind == JsonValueKind.Object
                && tags.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String)
            {
                title = PodcastJsonChapterParser.NormalizeTitle(titleElement.GetString());
            }
            if (title.Length == 0) title = $"Rozdział {number}";
            result.Add(new ProviderChapterPoint(
                $"ffprobe:{number}:{seconds.ToString("R", CultureInfo.InvariantCulture)}:{title}",
                title,
                TimeSpan.FromSeconds(seconds)));
        }
        return PodcastJsonChapterParser.Normalize(result);
    }

    private static bool TryGetStartSeconds(JsonElement chapter, out double seconds)
    {
        seconds = 0;
        if (chapter.ValueKind != JsonValueKind.Object
            || !chapter.TryGetProperty("start_time", out var start))
        {
            return false;
        }
        return start.ValueKind switch
        {
            JsonValueKind.String => double.TryParse(
                start.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out seconds),
            JsonValueKind.Number => start.TryGetDouble(out seconds),
            _ => false
        };
    }

    private static string? FindFfprobe()
    {
        var ffmpeg = FfmpegRadioWaveProvider.FindExecutable();
        if (string.IsNullOrWhiteSpace(ffmpeg)) return null;
        var directory = Path.GetDirectoryName(ffmpeg);
        if (string.IsNullOrWhiteSpace(directory)) return null;
        var name = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
        var candidate = Path.Combine(directory, name);
        return File.Exists(candidate) ? candidate : null;
    }

    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        var buffer = new char[8 * 1024];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) return result.ToString();
            if (result.Length > maximumCharacters - read)
                throw new InvalidDataException("Wynik analizy rozdziałów jest zbyt duży.");
            result.Append(buffer, 0, read);
        }
    }
}
