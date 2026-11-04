using System.Net;
using System.Text;
using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class TidalPlaybackProbeTests
{
    private const string SyntheticToken = "private-test-token-must-not-leak";
    private const string SyntheticUrl = "https://private.example/audio?signature=must-not-leak";

    public static void Run()
    {
        AcceptsOnlyNumericTrackId();
        ReportsFullManifestWithoutPrivateData();
        ReportsPreviewWithoutClaimingFullPlayback();
        ReportsDenialWithoutServerBody();
        RejectsMalformedOrOversizedResponses();
        RejectsInvalidDirectTrackId();
        HidesNetworkExceptionDetails();
    }

    private static void AcceptsOnlyNumericTrackId()
    {
        using var valid = JsonDocument.Parse("{\"trackId\":\"123456\"}");
        Check(LiteTidalPlaybackProbe.ReadTrackId(valid.RootElement) == "123456",
            "poprawny identyfikator utworu został odrzucony");
        using var invalid = JsonDocument.Parse("{\"trackId\":\"123/../private\"}");
        try
        {
            LiteTidalPlaybackProbe.ReadTrackId(invalid.RootElement);
            throw new Exception("nieprawidłowy identyfikator został przyjęty");
        }
        catch (LiteRequestException)
        {
            // Expected fixed validation error, no input echoed.
        }
    }

    private static void ReportsFullManifestWithoutPrivateData()
    {
        var sentToken = string.Empty;
        using var client = Client(request =>
        {
            sentToken = request.Headers.Authorization?.Parameter ?? string.Empty;
            return Json(HttpStatusCode.OK,
                "{\"assetPresentation\":\"FULL\","
                + "\"manifestMimeType\":\"application/vnd.tidal.bts\","
                + "\"manifest\":\""
                + Convert.ToBase64String(Encoding.UTF8.GetBytes(SyntheticUrl))
                + "\"}");
        });
        var result = Probe(client);
        var wire = LiteJson.Serialize(result);
        Check(sentToken == SyntheticToken, "test nie użył przekazanego tokenu");
        Check(result.Status == "full_manifest" && result.FullManifestAvailable
            && result.HttpStatus == 200 && result.ErrorCode == "none",
            "pełny manifest nie został wykryty");
        Check(!wire.Contains(SyntheticToken, StringComparison.Ordinal)
            && !wire.Contains(SyntheticUrl, StringComparison.Ordinal)
            && !wire.Contains(Convert.ToBase64String(Encoding.UTF8.GetBytes(SyntheticUrl)),
                StringComparison.Ordinal), "wynik ujawnia prywatne dane");
    }

    private static void ReportsPreviewWithoutClaimingFullPlayback()
    {
        using var client = Client(_ => Json(HttpStatusCode.OK,
            "{\"assetPresentation\":\"PREVIEW\",\"manifest\":\"c2VjcmV0\"}"));
        var result = Probe(client);
        Check(result.Status == "preview" && !result.FullManifestAvailable
            && result.ErrorCode == "preview_only", "próbka została uznana za pełny utwór");
    }

    private static void ReportsDenialWithoutServerBody()
    {
        using var client = Client(_ => Json(HttpStatusCode.Forbidden,
            "{\"message\":\"" + SyntheticUrl + SyntheticToken + "\"}"));
        var result = Probe(client);
        var wire = LiteJson.Serialize(result);
        Check(result.Status == "denied" && result.ErrorCode == "access_denied"
            && result.HttpStatus == 403 && !result.FullManifestAvailable,
            "odmowa nie ma bezpiecznego wyniku");
        Check(!wire.Contains(SyntheticToken, StringComparison.Ordinal)
            && !wire.Contains(SyntheticUrl, StringComparison.Ordinal),
            "treść odmowy wyciekła do protokołu");
    }

    private static void RejectsMalformedOrOversizedResponses()
    {
        using var badJson = Client(_ => Json(HttpStatusCode.OK, "{not-json"));
        var badJsonResult = Probe(badJson);
        Check(badJsonResult.ErrorCode == "invalid_response"
            && badJsonResult.HttpStatus == 200
            && !badJsonResult.FullManifestAvailable,
            "uszkodzony JSON nie został odrzucony bezpiecznym kodem");
        using var malformed = Client(_ => Json(HttpStatusCode.OK,
            "{\"assetPresentation\":\"FULL\",\"manifestMimeType\":\"application/vnd.tidal.bts\",\"manifest\":\"not base64\"}"));
        var result = Probe(malformed);
        Check(result.ErrorCode == "invalid_response"
            && !result.FullManifestAvailable,
            "uszkodzony manifest został zaakceptowany");
        using var oversized = Client(_ => Json(HttpStatusCode.OK,
            new string('x', 70_000)));
        Check(Probe(oversized).ErrorCode == "invalid_response",
            "nadmierna odpowiedź została zaakceptowana");
    }

    private static void RejectsInvalidDirectTrackId()
    {
        var requests = 0;
        using var client = Client(_ =>
        {
            requests++;
            return Json(HttpStatusCode.OK, "{}");
        });
        var result = LiteTidalPlaybackProbe.RunAsync(
                client, "123/../private", SyntheticToken, "PL", CancellationToken.None)
            .GetAwaiter().GetResult();
        Check(result.ErrorCode == "invalid_track" && requests == 0,
            "bezpośrednie wywołanie przyjęło nieprawidłowy identyfikator");
    }

    private static void HidesNetworkExceptionDetails()
    {
        using var client = new HttpClient(new Handler(_ =>
            throw new HttpRequestException(SyntheticUrl + SyntheticToken)));
        var result = Probe(client);
        var wire = LiteJson.Serialize(result);
        Check(result.ErrorCode == "request_failed" && !result.FullManifestAvailable
            && !wire.Contains(SyntheticToken, StringComparison.Ordinal)
            && !wire.Contains(SyntheticUrl, StringComparison.Ordinal),
            "szczegóły wyjątku wyciekły do protokołu");
    }

    private static LiteTidalPlaybackProbeResult Probe(HttpClient client) =>
        LiteTidalPlaybackProbe.RunAsync(
            client, "123456", SyntheticToken, "PL", CancellationToken.None)
            .GetAwaiter().GetResult();

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
        new(new Handler(reply));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(reply(request));
    }
}
