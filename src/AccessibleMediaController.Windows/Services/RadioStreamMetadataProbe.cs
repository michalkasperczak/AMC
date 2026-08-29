using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using AccessibleMediaController.Core.LocalMedia;
using NAudio.Wave;

namespace AccessibleMediaController.Windows.Services;

internal static partial class RadioStreamMetadataProbe
{
    private const int MaximumManifestCharacters = 512 * 1024;
    private const int MaximumAudioProbeBytes = 64 * 1024;

    internal static async Task<RadioAudioMetadata?> TryReadAsync(
        string source,
        TimeSpan timeout)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var sourceUri)
            || sourceUri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        using var cancellation = new CancellationTokenSource(timeout);
        using var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            ConnectTimeout = timeout
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AccessibleMediaController/0.1");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Icy-MetaData", "0");
        var uriMetadata = MetadataFromUri(sourceUri);

        try
        {
            var detected = RadioStreamResolver.IsHlsSource(source)
                ? await ReadHlsAsync(client, sourceUri, cancellation.Token).ConfigureAwait(false)
                : await ReadHeadersAsync(client, sourceUri, cancellation.Token).ConfigureAwait(false);
            return Merge(detected, uriMetadata);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or IOException
            or OperationCanceledException
            or InvalidDataException)
        {
            return uriMetadata;
        }
    }

    private static RadioAudioMetadata? MetadataFromUri(Uri source)
    {
        var declared = BitrateFromUri(source);
        if (declared is not null)
        {
            return new RadioAudioMetadata(declared, null, CodecFromUri(source) ?? "AAC");
        }

        var host = source.Host.ToLowerInvariant();
        var path = source.AbsolutePath.ToLowerInvariant();
        if (host is "stream.radio357.pl" or "live.r357.eu")
        {
            return new RadioAudioMetadata(128, null, "AAC", true);
        }
        if (host.EndsWith(".rcs.revma.com", StringComparison.Ordinal)
            || host.Equals("stream.rcs.revma.com", StringComparison.Ordinal))
        {
            if (path.Contains("an1ugyygzk8uv", StringComparison.Ordinal))
                return new RadioAudioMetadata(128, null, "MP3", true);
            if (path.Contains("ye5kghkgcm0uv", StringComparison.Ordinal))
                return new RadioAudioMetadata(128, null, "AAC", true);
        }
        if (host.Equals("stream13.polskieradio.pl", StringComparison.Ordinal)
            && path.Contains("/pr3/", StringComparison.Ordinal))
        {
            return new RadioAudioMetadata(192, null, "AAC", true);
        }
        return null;
    }

    private static RadioAudioMetadata? Merge(
        RadioAudioMetadata? detected,
        RadioAudioMetadata? fallback)
    {
        if (detected is null) return fallback;
        if (fallback is null) return detected;
        var bitrateFromFallback = detected.BitrateKbps is null && fallback.BitrateKbps is not null;
        return detected with
        {
            BitrateKbps = detected.BitrateKbps ?? fallback.BitrateKbps,
            SampleRateHz = detected.SampleRateHz ?? fallback.SampleRateHz,
            Codec = detected.Codec ?? fallback.Codec,
            IsBitrateEstimated = bitrateFromFallback
                ? fallback.IsBitrateEstimated
                : detected.IsBitrateEstimated
        };
    }

    private static async Task<RadioAudioMetadata?> ReadHeadersAsync(
        HttpClient client,
        Uri source,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, source);
        using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bitrate = HeaderBitrate(response);
        var codec = CodecFromContentType(response.Content.Headers.ContentType?.MediaType);
        int? sampleRate = null;
        if (bitrate is null || sampleRate is null || codec is null)
        {
            var sample = await ReadAudioSampleAsync(response.Content, cancellationToken).ConfigureAwait(false);
            var detected = DetectAudioSample(sample);
            bitrate ??= detected?.BitrateKbps;
            sampleRate ??= detected?.SampleRateHz;
            codec ??= detected?.Codec;
        }
        return bitrate is null && sampleRate is null && codec is null
            ? null
            : new RadioAudioMetadata(bitrate, sampleRate, codec, false);
    }

    private static async Task<RadioAudioMetadata?> ReadHlsAsync(
        HttpClient client,
        Uri source,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        var buffer = new char[16 * 1024];
        var manifest = new System.Text.StringBuilder();
        while (manifest.Length < MaximumManifestCharacters)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, MaximumManifestCharacters - manifest.Length)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0) break;
            manifest.Append(buffer, 0, read);
        }

        var parsed = ParseHlsManifest(manifest.ToString());
        if (parsed.BitrateKbps is not null || parsed.Codec is not null) return parsed;
        var declaredBitrate = BitrateFromUri(source);
        if (declaredBitrate is not null)
        {
            return new RadioAudioMetadata(
                declaredBitrate,
                null,
                CodecFromUri(source) ?? "AAC",
                false);
        }
        if (parsed.SegmentDurationSeconds is not > 0 || string.IsNullOrWhiteSpace(parsed.SegmentUri)) return null;

        var segmentUri = new Uri(source, parsed.SegmentUri);
        using var segmentRequest = new HttpRequestMessage(HttpMethod.Get, segmentUri);
        using var segmentResponse = await client.SendAsync(
                segmentRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        segmentResponse.EnsureSuccessStatusCode();
        var contentType = segmentResponse.Content.Headers.ContentType?.MediaType;
        var safeAudioSegment = IsAudioOnlySegment(segmentUri, contentType);
        var length = segmentResponse.Content.Headers.ContentLength;
        var estimatedBitrate = safeAudioSegment && length is > 0
            ? RadioAudioMetadataRules.NormalizeBitrateKbps((int)Math.Round(
                length.Value * 8d / parsed.SegmentDurationSeconds.Value / 1000d))
            : null;
        var codec = CodecFromContentType(contentType) ?? CodecFromExtension(segmentUri.AbsolutePath);
        int? sampleRate = null;
        if (estimatedBitrate is null || codec is null)
        {
            var sample = await ReadAudioSampleAsync(segmentResponse.Content, cancellationToken).ConfigureAwait(false);
            var detected = DetectAudioSample(sample);
            estimatedBitrate ??= detected?.BitrateKbps;
            sampleRate = detected?.SampleRateHz;
            codec ??= detected?.Codec;
        }
        return estimatedBitrate is null && sampleRate is null && codec is null
            ? null
            : new RadioAudioMetadata(estimatedBitrate, sampleRate, codec, estimatedBitrate is not null);
    }

    internal static RadioAudioMetadata ParseHlsManifest(string manifest)
    {
        var lines = manifest.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        int? bestAudioBandwidth = null;
        string? bestCodec = null;
        double? firstSegmentDuration = null;
        string? firstSegmentUri = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase))
            {
                var attributes = line[(line.IndexOf(':') + 1)..];
                var codecs = AttributeValue(attributes, "CODECS");
                var hasVideo = codecs is not null && VideoCodecRegex().IsMatch(codecs);
                var bandwidthText = AttributeValue(attributes, "AVERAGE-BANDWIDTH")
                    ?? AttributeValue(attributes, "BANDWIDTH");
                if (!hasVideo
                    && long.TryParse(bandwidthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bandwidth)
                    && RadioAudioMetadataRules.NormalizeBitrateKbps((int)Math.Round(bandwidth / 1000d)) is int normalized
                    && (bestAudioBandwidth is null || normalized > bestAudioBandwidth))
                {
                    bestAudioBandwidth = normalized;
                    bestCodec = CodecFromCodecs(codecs);
                }
                else if (bestCodec is null)
                {
                    bestCodec = CodecFromCodecs(codecs);
                }
                continue;
            }

            if (firstSegmentDuration is null
                && line.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
            {
                var durationText = line[(line.IndexOf(':') + 1)..].Split(',')[0];
                if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration)
                    || duration <= 0)
                {
                    continue;
                }
                for (var next = index + 1; next < lines.Length; next++)
                {
                    if (lines[next].StartsWith('#')) continue;
                    firstSegmentDuration = duration;
                    firstSegmentUri = lines[next];
                    break;
                }
            }
        }

        return new RadioAudioMetadata(
            bestAudioBandwidth,
            null,
            bestCodec,
            false,
            firstSegmentDuration,
            firstSegmentUri);
    }

    private static int? HeaderBitrate(HttpResponseMessage response)
    {
        foreach (var name in new[] { "icy-br", "x-audiocast-bitrate", "ice-bitrate" })
        {
            if (!response.Headers.TryGetValues(name, out var values)) continue;
            var value = values.FirstOrDefault();
            if (value is null) continue;
            var numeric = new string(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
            if (double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var bitrate))
            {
                return RadioAudioMetadataRules.NormalizeBitrateKbps((int)Math.Round(bitrate));
            }
        }
        return null;
    }

    private static async Task<byte[]> ReadAudioSampleAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var sample = new MemoryStream(MaximumAudioProbeBytes);
        var buffer = new byte[8 * 1024];
        while (sample.Length < MaximumAudioProbeBytes)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(
                        buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaximumAudioProbeBytes - sample.Length)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (sample.Length > 0)
            {
                break;
            }
            if (read <= 0) break;
            sample.Write(buffer, 0, read);
            if (sample.Length >= 4 * 1024 && DetectAudioSample(sample.GetBuffer().AsSpan(0, (int)sample.Length)) is not null)
            {
                break;
            }
        }
        return sample.ToArray();
    }

    internal static RadioAudioMetadata? DetectAudioSample(ReadOnlySpan<byte> sample)
    {
        if (Mp3StructureProbe.TryFindConsecutiveFrameOffset(sample, out var mp3Offset))
        {
            try
            {
                using var stream = new MemoryStream(sample[mp3Offset..].ToArray(), writable: false);
                var frame = Mp3Frame.LoadFromStream(stream);
                if (frame is not null)
                {
                    return new RadioAudioMetadata(
                        RadioAudioMetadataRules.NormalizeBitrateKbps(frame.BitRate / 1000),
                        frame.SampleRate > 0 ? frame.SampleRate : null,
                        "MP3");
                }
            }
            catch (Exception exception) when (exception is InvalidDataException
                or EndOfStreamException
                or ArgumentException)
            {
                // Continue with the bounded AAC probe.
            }
        }

        return TryReadAdtsMetadata(sample, out var aacBitrate, out var aacSampleRate)
            ? new RadioAudioMetadata(aacBitrate, aacSampleRate, "AAC")
            : null;
    }

    private static bool TryReadAdtsMetadata(
        ReadOnlySpan<byte> sample,
        out int bitrateKbps,
        out int sampleRateHz)
    {
        bitrateKbps = 0;
        sampleRateHz = 0;
        ReadOnlySpan<int> sampleRates = [96_000, 88_200, 64_000, 48_000, 44_100, 32_000, 24_000, 22_050, 16_000, 12_000, 11_025, 8_000, 7_350];
        for (var offset = 0; offset <= sample.Length - 7; offset++)
        {
            if (sample[offset] != 0xFF || (sample[offset + 1] & 0xF6) != 0xF0) continue;
            var rateIndex = (sample[offset + 2] >> 2) & 0x0F;
            if (rateIndex >= sampleRates.Length) continue;
            var rate = sampleRates[rateIndex];
            var position = offset;
            long totalBytes = 0;
            long totalSamples = 0;
            var frames = 0;
            while (position <= sample.Length - 7 && frames < 16)
            {
                if (sample[position] != 0xFF || (sample[position + 1] & 0xF6) != 0xF0
                    || ((sample[position + 2] >> 2) & 0x0F) != rateIndex)
                {
                    break;
                }
                var frameLength = ((sample[position + 3] & 0x03) << 11)
                    | (sample[position + 4] << 3)
                    | ((sample[position + 5] & 0xE0) >> 5);
                if (frameLength < 7 || position + frameLength > sample.Length) break;
                totalBytes += frameLength;
                totalSamples += 1024L * ((sample[position + 6] & 0x03) + 1);
                frames++;
                position += frameLength;
            }
            if (frames < 3 || totalSamples <= 0) continue;
            var calculated = (int)Math.Round(totalBytes * 8d * rate / totalSamples / 1000d);
            if (RadioAudioMetadataRules.NormalizeBitrateKbps(calculated) is not int normalized) continue;
            bitrateKbps = normalized;
            sampleRateHz = rate;
            return true;
        }
        return false;
    }

    internal static int? BitrateFromUri(Uri uri)
    {
        var match = AudioBitrateInUriRegex().Match(Uri.UnescapeDataString(uri.AbsoluteUri));
        if (!match.Success
            || !long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitsPerSecond))
        {
            return null;
        }
        var kbps = bitsPerSecond >= 10_000 ? (int)Math.Round(bitsPerSecond / 1000d) : (int)bitsPerSecond;
        return RadioAudioMetadataRules.NormalizeBitrateKbps(kbps);
    }

    private static string? CodecFromUri(Uri uri)
    {
        if (AudioBitrateInUriRegex().IsMatch(Uri.UnescapeDataString(uri.AbsoluteUri))) return "AAC";
        return CodecFromExtension(uri.AbsolutePath);
    }

    private static string? AttributeValue(string attributes, string name)
    {
        var match = Regex.Match(
            attributes,
            $"(?:^|,){Regex.Escape(name)}=(?:\"(?<quoted>[^\"]*)\"|(?<plain>[^,]*))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!match.Success) return null;
        return match.Groups["quoted"].Success
            ? match.Groups["quoted"].Value
            : match.Groups["plain"].Value.Trim();
    }

    private static bool IsAudioOnlySegment(Uri segment, string? contentType) =>
        contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true
        || Path.GetExtension(segment.AbsolutePath).ToLowerInvariant() is ".aac" or ".mp3" or ".m4a" or ".ogg" or ".opus";

    private static string? CodecFromContentType(string? contentType) => contentType?.ToLowerInvariant() switch
    {
        "audio/mpeg" or "audio/mp3" => "MP3",
        "audio/aac" or "audio/aacp" => "AAC",
        "audio/ogg" => "OGG",
        "audio/opus" => "Opus",
        "audio/flac" => "FLAC",
        _ => null
    };

    private static string? CodecFromExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".aac" or ".m4a" => "AAC",
        ".mp3" => "MP3",
        ".ogg" => "OGG",
        ".opus" => "Opus",
        ".flac" => "FLAC",
        _ => null
    };

    private static string? CodecFromCodecs(string? codecs)
    {
        if (string.IsNullOrWhiteSpace(codecs)) return null;
        if (codecs.Contains("mp4a", StringComparison.OrdinalIgnoreCase)) return "AAC";
        if (codecs.Contains("opus", StringComparison.OrdinalIgnoreCase)) return "Opus";
        if (codecs.Contains("vorbis", StringComparison.OrdinalIgnoreCase)) return "Vorbis";
        if (codecs.Contains("mp3", StringComparison.OrdinalIgnoreCase)) return "MP3";
        return null;
    }

    [GeneratedRegex("(?:avc|h26[45]|hevc|vp0?[89]|av01)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VideoCodecRegex();

    [GeneratedRegex("audio=(\\d{4,8})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AudioBitrateInUriRegex();
}
