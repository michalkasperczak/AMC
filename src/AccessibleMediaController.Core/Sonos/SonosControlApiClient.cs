using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>Odczyt Control API. Token tylko na czas wywolania, bez zapisu i logowania.</summary>
public sealed partial class SonosControlApiClient : IDisposable
{
    public const int MaxResponseBytes = 256 * 1024;
    private readonly SonosControlApiConfiguration _configuration;
    private readonly HttpClient _http;
    private readonly TimeSpan _timeout;

    // Wstrzykniety handler jest zaufanym szwem testowym, nie adresem alternatywnej uslugi.
    public SonosControlApiClient(SonosControlApiConfiguration configuration,
        HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _configuration = configuration;
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        // Wlasnosc handlera: wlasny HttpClientHandler zwalniamy razem z klientem,
        // wstrzyknietego handlera NIE przejmujemy - zostaje wlasnoscia wolajacego.
        _http = new HttpClient(handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        }, disposeHandler: handler is null) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<SonosHouseholdsOutcome> GetHouseholdsAsync(string? accessToken, CancellationToken cancellationToken)
    {
        var reply = await ReadAsync(_configuration.HouseholdsUri, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success) return SonosHouseholdsOutcome.Failure(reply.Status);
        try
        {
            using var document = Parse(reply.Body!);
            var root = document.RootElement;
            var households = new List<SonosHousehold>();
            foreach (var item in Array(root, "households", 4))
                households.Add(new SonosHousehold(Text(item, "id", 64, true)!,
                    Text(item, "name", 1024), Text(item, "swVersion", 64)));
            return SonosHouseholdsOutcome.Ok(households);
        }
        catch (Exception ex) when (IsInvalidData(ex))
        { return SonosHouseholdsOutcome.Failure(SonosControlApiStatus.InvalidResponse); }
    }

    public async Task<SonosGroupsOutcome> GetGroupsAsync(string? accessToken, string? householdId, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return SonosGroupsOutcome.Failure(SonosControlApiStatus.Canceled);
        if (!SonosHouseholdIdPolicy.TryEncode(householdId, out var encoded))
            return SonosGroupsOutcome.Failure(SonosControlApiStatus.InvalidConfiguration);
        var uri = new Uri(_configuration.Origin, "households/" + encoded + "/groups");
        var reply = await ReadAsync(uri, accessToken, cancellationToken).ConfigureAwait(false);
        if (reply.Status != SonosControlApiStatus.Success) return SonosGroupsOutcome.Failure(reply.Status);
        try
        {
            using var document = Parse(reply.Body!);
            var root = document.RootElement;
            var groups = new List<SonosGroup>();
            foreach (var item in Array(root, "groups", 32))
            {
                var ids = new List<string>();
                foreach (var id in Array(item, "playerIds", 32, true))
                {
                    if (id.ValueKind != JsonValueKind.String) throw new JsonException();
                    var value = id.GetString();
                    if (string.IsNullOrEmpty(value) || value.Length > 24) throw new JsonException();
                    ids.Add(value);
                }
                SonosPlaybackStates.TryParse(Text(item, "playbackState", 128), out var state);
                groups.Add(new SonosGroup(Text(item, "id", 35, true)!, Text(item, "name", 69, true)!,
                    Text(item, "coordinatorId", 24, true)!, ids, state));
            }
            var players = new List<SonosPlayer>();
            foreach (var item in Array(root, "players", 32))
                players.Add(new SonosPlayer(Text(item, "id", 24, true)!, Text(item, "name", 64, true)!,
                    Text(item, "softwareVersion", 64), Text(item, "apiVersion", 64), Text(item, "minApiVersion", 16)));
            var partial = false;
            if (root.TryGetProperty("partial", out var valuePartial) && valuePartial.ValueKind != JsonValueKind.Null)
                partial = valuePartial.GetBoolean();
            return SonosGroupsOutcome.Ok(new SonosHouseholdTopology(groups, players, partial));
        }
        catch (Exception ex) when (IsInvalidData(ex))
        { return SonosGroupsOutcome.Failure(SonosControlApiStatus.InvalidResponse); }
    }

    private async Task<(SonosControlApiStatus Status, string? Body)> ReadAsync(Uri uri, string? token, CancellationToken caller)
    {
        if (caller.IsCancellationRequested) return (SonosControlApiStatus.Canceled, null);
        if (!IsHeaderValue(token) || !IsHeaderValue(_configuration.ApiKey))
            return (SonosControlApiStatus.InvalidConfiguration, null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(_timeout);
        var ct = deadline.Token;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("X-Sonos-Api-Key", _configuration.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            // Kazda odpowiedz musi wskazywac dokladnie wyslany adres.
            // Szew testowy odwzorowuje te metadane natywnego transportu.
            if (response.RequestMessage?.RequestUri != uri)
                return (SonosControlApiStatus.RedirectRefused, null);
            var status = MapStatus((int)response.StatusCode);
            if (status != SonosControlApiStatus.Success) return (status, null);
            var contentType = response.Content.Headers.ContentType;
            if (contentType is null || !string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(contentType.CharSet)
                    && !string.Equals(contentType.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))
                || response.Content.Headers.ContentLength > MaxResponseBytes)
                return (SonosControlApiStatus.InvalidResponse, null);
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var count = await stream.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
                if (count == 0) break;
                if (output.Length + count > MaxResponseBytes) return (SonosControlApiStatus.InvalidResponse, null);
                output.Write(buffer, 0, count);
            }
            return (SonosControlApiStatus.Success, new UTF8Encoding(false, true).GetString(output.ToArray()));
        }
        catch (OperationCanceledException)
        { return (caller.IsCancellationRequested ? SonosControlApiStatus.Canceled : SonosControlApiStatus.Unreachable, null); }
        catch (HttpRequestException) { return (SonosControlApiStatus.Unreachable, null); }
        catch (IOException) { return (SonosControlApiStatus.Unreachable, null); }
        catch (FormatException) { return (SonosControlApiStatus.InvalidConfiguration, null); }
        catch (DecoderFallbackException) { return (SonosControlApiStatus.InvalidResponse, null); }
    }

    private static IEnumerable<JsonElement> Array(JsonElement parent, string name, int max, bool required = false)
    {
        if (parent.ValueKind != JsonValueKind.Object) throw new JsonException();
        if (!parent.TryGetProperty(name, out var array) || array.ValueKind == JsonValueKind.Null)
        {
            if (required) throw new JsonException();
            yield break;
        }
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > max) throw new JsonException();
        foreach (var item in array.EnumerateArray()) yield return item;
    }

    private static string? Text(JsonElement parent, string name, int max, bool required = false)
    {
        if (parent.ValueKind != JsonValueKind.Object) throw new JsonException();
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required) throw new JsonException();
            return null;
        }
        if (value.ValueKind != JsonValueKind.String) throw new JsonException();
        var text = value.GetString()!;
        if (text.Length > max || (required && text.Length == 0)) throw new JsonException();
        return text;
    }

    // Polityka transportu naglowkow, nie walidacja semantyki tokenu Sonos.
    // Zachowujemy wartosc dokladnie; nie naprawiamy trimem ani kodowaniem.
    private static bool IsHeaderValue(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 65536) return false;
        foreach (var c in value) if (c < '!' || c > '~') return false;
        return true;
    }

    private static SonosControlApiStatus MapStatus(int code) => code switch
    {
        200 => SonosControlApiStatus.Success,
        >= 300 and < 400 => SonosControlApiStatus.RedirectRefused,
        400 => SonosControlApiStatus.RequestRejected,
        401 => SonosControlApiStatus.Unauthorized,
        403 => SonosControlApiStatus.Forbidden,
        404 => SonosControlApiStatus.NotFound,
        429 => SonosControlApiStatus.RateLimited,
        499 => SonosControlApiStatus.CommandFailed,
        >= 500 and < 600 => SonosControlApiStatus.ServiceError,
        _ => SonosControlApiStatus.InvalidResponse
    };

    private static JsonDocument Parse(string body)
    {
        var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        try { RejectDuplicateProperties(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }

    private static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException();
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }

    private static bool IsInvalidData(Exception ex) => ex is JsonException or InvalidOperationException or ArgumentException;
    public void Dispose() => _http.Dispose();
}
