using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Windows.Services;

internal sealed record YouTubeDownloadVariant(
    string Selector,
    string Signature,
    string Kind,
    string? Extension,
    string? VideoCodec,
    string? AudioCodec,
    string? Language,
    int? Width,
    int? Height,
    int? Fps,
    int? BitrateKbps,
    long? SizeBytes,
    bool SizeApproximate,
    bool NeedsMerge);

/// <summary>
/// A short lived format offer. Identifiers stay in the model; the UI creates
/// deliberate spoken labels from the descriptive fields.
/// </summary>
internal static class YouTubeDownloadVariantCatalog
{
    private const int MaximumJsonCharacters = 8_000_000;
    private static readonly Regex SafeFormatId = new(
        @"^[A-Za-z0-9_.-]{1,80}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static YouTubeDownloadVariant? Match(
        IReadOnlyList<YouTubeDownloadVariant> current,
        string selector,
        string signature) =>
        current.FirstOrDefault(item => item.Selector == selector
            && item.Signature == signature);

    internal static async Task<IReadOnlyList<YouTubeDownloadVariant>> ListAsync(
        string pageUrl,
        CancellationToken cancellationToken)
    {
        if (!YouTubeSourceResolver.IsYouTubeUrl(pageUrl))
            throw new ArgumentException("Wybór wariantu jest dostępny tylko dla filmu YouTube.", nameof(pageUrl));
        var executable = YouTubeSourceResolver.FindExecutable()
            ?? throw new NotSupportedException(
                "Lista wariantów wymaga składnika yt-dlp. Wybierz Pomoc, Sprawdź składniki: FFmpeg i yt-dlp.");
        var start = ExternalToolProcess.Create(executable);
        foreach (var argument in new[]
        {
            "--ignore-config", "--no-playlist", "--no-warnings", "--skip-download",
            "--dump-single-json", "--force-ipv4", "--socket-timeout", "20",
            "--extractor-retries", "2", "--extractor-args", "youtube:lang=pl",
            "--", pageUrl.Trim()
        }) start.ArgumentList.Add(argument);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        Process? process = null;
        try
        {
            process = Process.Start(start)
                ?? throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.");
            process.StandardInput.Close();
            using var termination = timeout.Token.Register(static state =>
            {
                try
                {
                    var running = (Process)state!;
                    if (!running.HasExited) running.Kill(entireProcessTree: true);
                }
                catch (Exception) { }
            }, process);
            var jsonTask = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            var json = await jsonTask.ConfigureAwait(false);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                DiagnosticLog.Warning("youtube-variant-list",
                    $"yt-dlp zakończył listowanie kodem {process.ExitCode}; komunikat: {TrimDiagnostic(error)}");
                throw new InvalidDataException(
                    "Nie udało się pobrać listy wariantów. Sprawdź dostępność filmu i aktualność yt-dlp.");
            }
            return Parse(json, pageUrl.Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Pobieranie listy wariantów trwało zbyt długo.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidDataException("Nie udało się uruchomić składnika yt-dlp.", exception);
        }
        finally
        {
            try { if (process is { HasExited: false }) process.Kill(entireProcessTree: true); }
            catch (Exception) { }
            process?.Dispose();
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[8192];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0) return result.ToString();
            if (result.Length + count > MaximumJsonCharacters)
                throw new InvalidDataException("Lista wariantów jest zbyt duża.");
            result.Append(buffer, 0, count);
        }
    }

    internal static IReadOnlyList<YouTubeDownloadVariant> Parse(
        string json, string sourceIdentity = "")
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("formats", out var formats)
                || formats.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Film nie udostępnia listy wariantów.");
            var sourceKey = sourceIdentity + "|" +
                (ReadString(document.RootElement, "id") ?? string.Empty);

            var raw = new List<RawFormat>();
            foreach (var item in formats.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || ReadBool(item, "has_drm")) continue;
                var id = ReadString(item, "format_id");
                var ext = ReadString(item, "ext");
                var video = ReadString(item, "vcodec");
                var audio = ReadString(item, "acodec");
                if (id is null || !SafeFormatId.IsMatch(id)
                    || id is "best" or "bestaudio" or "bestvideo" or "bv" or "ba" or "b"
                    || ext is null || !SafeFormatId.IsMatch(ext)
                    || ext.Equals("mhtml", StringComparison.OrdinalIgnoreCase)) continue;
                var hasVideo = video is not null && video != "none";
                var hasAudio = audio is not null && audio != "none";
                if (!hasVideo && !hasAudio) continue;
                var exactSize = ReadLong(item, "filesize");
                var size = exactSize ?? ReadLong(item, "filesize_approx");
                raw.Add(new RawFormat(id, ext, hasVideo ? video : null,
                    hasAudio ? audio : null, ReadString(item, "language"),
                    ReadInt(item, "width"), ReadInt(item, "height"),
                    ReadInt(item, "fps"), ReadInt(item, "tbr") ?? ReadInt(item, "abr"),
                    size, exactSize is null && size is not null));
            }
            var variants = new List<YouTubeDownloadVariant>();
            var bestAudio = raw.Where(item => item.AudioCodec is not null
                                             && item.VideoCodec is null)
                .OrderByDescending(item => item.BitrateKbps ?? 0)
                .FirstOrDefault();
            foreach (var item in raw)
            {
                variants.Add(Create(sourceKey, item.Id,
                    item.VideoCodec is null ? "audioOnly"
                    : item.AudioCodec is null ? "videoOnly" : "videoAudio",
                    item.Extension, item.VideoCodec, item.AudioCodec,
                    item.Language, item.Width, item.Height, item.Fps,
                    item.BitrateKbps, item.SizeBytes, item.SizeApproximate, false));
                if (item.VideoCodec is not null && item.AudioCodec is null
                    && bestAudio is not null && bestAudio.Id != item.Id)
                {
                    var size = item.SizeBytes is not null && bestAudio.SizeBytes is not null
                        && item.SizeBytes <= long.MaxValue - bestAudio.SizeBytes
                        ? item.SizeBytes + bestAudio.SizeBytes : null;
                    variants.Add(Create(sourceKey, item.Id + "+" + bestAudio.Id,
                        "videoAudio", "mkv", item.VideoCodec, bestAudio.AudioCodec,
                        bestAudio.Language, item.Width, item.Height, item.Fps,
                        item.BitrateKbps, size,
                        item.SizeApproximate || bestAudio.SizeApproximate, true));
                }
            }
            return variants
                .GroupBy(item => item.Selector, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.Kind == "videoAudio" ? 0
                    : item.Kind == "audioOnly" ? 1 : 2)
                .ThenByDescending(item => item.Height ?? 0)
                .ThenByDescending(item => item.BitrateKbps ?? 0)
                .Take(200)
                .ToArray();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Nie udało się odczytać listy wariantów filmu.", exception);
        }
    }

    private static YouTubeDownloadVariant Create(string sourceKey,
        string selector, string kind,
        string? extension, string? videoCodec, string? audioCodec, string? language,
        int? width, int? height, int? fps, int? bitrate, long? size,
        bool approximate, bool merge)
    {
        var identity = string.Join('|', sourceKey, selector, kind, extension, videoCodec,
            audioCodec, language, width, height, fps, merge);
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new YouTubeDownloadVariant(selector, signature, kind, extension,
            videoCodec, audioCodec, language, width, height, fps, bitrate,
            size, approximate, merge);
    }

    private static string? ReadString(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() : null;

    private static int? ReadInt(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number) && double.IsFinite(number)
            && number > 0 && number <= int.MaxValue
            ? (int)Math.Round(number) : null;

    private static long? ReadLong(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number) && number > 0 ? number : null;

    private static bool ReadBool(JsonElement item, string key) =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;

    private static string TrimDiagnostic(string value)
    {
        var normalized = string.Join(' ', value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private sealed record RawFormat(string Id, string Extension, string? VideoCodec,
        string? AudioCodec, string? Language, int? Width, int? Height, int? Fps,
        int? BitrateKbps, long? SizeBytes, bool SizeApproximate);
}
