using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AccessibleMediaController.LiteHost.Protocol;

/// <summary>
/// Read-only experiment against the legacy playback-info service. The response
/// deliberately contains neither a token nor any part of a playback manifest.
/// A full manifest is evidence of an entitlement response, not proof that AMC
/// can decode or play the audio.
/// </summary>
internal static class LiteTidalPlaybackProbe
{
    internal const string Operation = "tidal.playbackProbe";
    private const int MaximumResponseBytes = 64 * 1024;

    internal static string ReadTrackId(JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty("trackId", out var value)
            || value.ValueKind != JsonValueKind.String)
            throw new LiteRequestException("Wybierz utwór TIDAL do sprawdzenia.");

        var id = value.GetString() ?? string.Empty;
        if (id.Length is < 1 or > 18 || !id.All(char.IsAsciiDigit))
            throw new LiteRequestException("Identyfikator utworu TIDAL jest nieprawidłowy.");
        return id;
    }

    internal static async Task<LiteTidalPlaybackProbeResult> RunAsync(
        HttpClient client,
        string trackId,
        string accessToken,
        string countryCode,
        CancellationToken cancellationToken)
    {
        if (trackId.Length is < 1 or > 18 || !trackId.All(char.IsAsciiDigit))
            return Result("unavailable", "invalid_track");
        if (string.IsNullOrWhiteSpace(accessToken))
            return Result("unavailable", "missing_login");
        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetter))
            return Result("unavailable", "invalid_country");

        var endpoint = new Uri(
            $"https://api.tidal.com/v1/tracks/{trackId}/playbackinfopostpaywall"
            + $"?countryCode={countryCode.ToUpperInvariant()}"
            + "&audioquality=HIGH&playbackmode=STREAM&assetpresentation=FULL");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            var httpStatus = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
                return Result("denied", ErrorFor(response.StatusCode), httpStatus);

            if (response.Content.Headers.ContentLength is > MaximumResponseBytes)
                return Result("unavailable", "invalid_response", httpStatus);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var buffer = new byte[MaximumResponseBytes + 1];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer.AsMemory(length, buffer.Length - length), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                length += read;
            }
            if (length == 0 || length > MaximumResponseBytes)
                return Result("unavailable", "invalid_response", httpStatus);

            using var document = JsonDocument.Parse(buffer.AsMemory(0, length));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Result("unavailable", "invalid_response", httpStatus);
            if (!root.TryGetProperty("assetPresentation", out var presentation)
                || presentation.ValueKind != JsonValueKind.String)
                return Result("unavailable", "invalid_response", httpStatus);
            if (presentation.GetString() == "PREVIEW")
                return Result("preview", "preview_only", httpStatus);
            if (presentation.GetString() != "FULL"
                || !root.TryGetProperty("manifestMimeType", out var mime)
                || mime.ValueKind != JsonValueKind.String
                || mime.GetString() is not ("application/vnd.tidal.bts" or "application/dash+xml")
                || !root.TryGetProperty("manifest", out var manifest)
                || manifest.ValueKind != JsonValueKind.String
                || !IsValidManifestEncoding(manifest.GetString()))
                return Result("unavailable", "invalid_response", httpStatus);

            return Result("full_manifest", "none", httpStatus, true);
        }
        catch (JsonException)
        {
            return Result("unavailable", "invalid_response", 200);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result("unavailable", "cancelled");
        }
        catch (Exception)
        {
            // Never forward an exception message: HTTP libraries may include
            // authorization headers or private response content in it.
            return Result("unavailable", "request_failed");
        }
    }

    private static string ErrorFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "login_rejected",
        HttpStatusCode.Forbidden => "access_denied",
        HttpStatusCode.NotFound => "not_found",
        HttpStatusCode.TooManyRequests => "rate_limited",
        _ => "server_rejected"
    };

    private static bool IsValidManifestEncoding(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var bytes = new byte[value.Length];
        try
        {
            return Convert.TryFromBase64String(value, bytes, out var written)
                && written > 0;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static LiteTidalPlaybackProbeResult Result(
        string status, string code, int httpStatus = 0, bool fullManifestAvailable = false) =>
        new(status, code, httpStatus, fullManifestAvailable);
}

internal sealed record LiteTidalPlaybackProbeResult(
    string Status,
    string ErrorCode,
    int HttpStatus,
    bool FullManifestAvailable);
