using System.Net;
using System.Net.Http.Headers;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class RemoteMediaBitrateTests
{
    internal static void Run()
    {
        var headCalls = 0;
        var head = new FakeHandler(request =>
        {
            headCalls++;
            Assert.True(request.Method == HttpMethod.Head, "Znana długość wymaga tylko HEAD.");
            return AudioResponse(HttpStatusCode.OK, 64_000);
        });
        var measured = LiteRemoteMediaBitrate.TryReadAsync(
            "https://example.test/nagranie.mp3", TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(2), head).GetAwaiter().GetResult();
        Assert.True(measured == 128 && headCalls == 1,
            "Zdalny MP3 powinien dać średnie 128 kb/s bez pobrania treści.");

        var rangeCalls = 0;
        var range = new FakeHandler(request =>
        {
            rangeCalls++;
            if (request.Method == HttpMethod.Head)
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed)
                { Content = new ByteArrayContent([]) };
            Assert.True(request.Headers.Range?.ToString() == "bytes=0-0",
                "Awaryjny odczyt ma żądać tylko pierwszego bajtu.");
            var response = AudioResponse(HttpStatusCode.PartialContent, 1);
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, 64_000);
            return response;
        });
        measured = LiteRemoteMediaBitrate.TryReadAsync(
            "https://example.test/nagranie.mp3", TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(2), range).GetAwaiter().GetResult();
        Assert.True(measured == 128 && rangeCalls == 2,
            "Serwer bez HEAD powinien ujawnić rozmiar przez Content-Range.");

        var html = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[64_000])
            {
                Headers = { ContentType = new MediaTypeHeaderValue("text/html") }
            }
        });
        Assert.True(LiteRemoteMediaBitrate.TryReadAsync(
            "https://example.test/nagranie.mp3", TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(2), html).GetAwaiter().GetResult() is null,
            "Rozmiar strony HTML nie może udawać bitrate nagrania.");
        Assert.True(LiteRemoteMediaBitrate.Estimate(0, TimeSpan.FromSeconds(4)) is null,
            "Brak rozmiaru nie daje fikcyjnej wartości.");
        Console.WriteLine("RemoteMediaBitrateTests: OK");
    }

    private static HttpResponseMessage AudioResponse(HttpStatusCode status, long length)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        response.Content.Headers.ContentLength = length;
        return response;
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
