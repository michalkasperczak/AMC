using System.Text.Json;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class TidalNativePlayRequestTests
{
    private const string Token = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef";
    private const string Url = "http://127.0.0.1:43117/audio/" + Token;

    public static void Run()
    {
        AcceptsOnlyKnownTidalProfiles();
        ParsesOneLocalTrackWithoutUpstreamData();
        RejectsUnsafeUrlsAndExtraData();
        RejectsBadIdentityTitleAndDuration();
        PreservesSourceRateAndLabelsPcmSeparately();
        Console.WriteLine("TidalNativePlayRequestTests: OK");
    }

    private static void AcceptsOnlyKnownTidalProfiles()
    {
        Assert.True(LiteTidalNativePlayRequest.IsEnabledForProfile("test"),
            "TIDAL native w AMC Test jest dostępny.");
        Assert.True(LiteTidalNativePlayRequest.IsEnabledForProfile("public"),
            "TIDAL native w AMC Public jest dostępny.");
        foreach (var profile in new string?[] { null, "", "Test", "Public", "unknown" })
            Assert.True(!LiteTidalNativePlayRequest.IsEnabledForProfile(profile),
                "Nieznany i brak profilu nie mogą uruchomić TIDAL native.");
        Assert.True(!LiteTidalNativePlayRequest.DisabledMessage.Contains("http", StringComparison.Ordinal),
            "Odmowa profilu nie ujawnia adresu proxy.");
    }

    private static void ParsesOneLocalTrackWithoutUpstreamData()
    {
        var parsed = Parse($$"""
            {"id":"518279266","title":"Przykładowy utwór","proxyUrl":"{{Url}}","durationSeconds":215.5}
            """);
        Assert.Equal("518279266", parsed.TrackId, "Id dostawcy pozostaje danymi modelu");
        Assert.Equal("tidal:518279266", parsed.MediaItemId, "stałe Id hosta");
        Assert.Equal("tidal:track:518279266", parsed.StableSource, "Source bez proxy URL");
        Assert.Equal("Przykładowy utwór", parsed.Title, "tytuł użytkowy");
        Assert.True(parsed.Duration == TimeSpan.FromSeconds(215.5), "pełny czas utworu");
        Assert.True(parsed.ProxyUri.Host == "127.0.0.1", "proxy tylko loopback");
        var max = Parse($$"""
            {"id":"518279266","title":"Utwór Max","proxyUrl":"{{Url}}.mpd","durationSeconds":215.5}
            """);
        Assert.True(max.ProxyUri.AbsolutePath.EndsWith(".mpd", StringComparison.Ordinal),
            "manifest Max pozostaje na lokalnym proxy");
        var withBitrate = Parse($$"""
            {"id":"518279266","title":"Utwór Max","proxyUrl":"{{Url}}.mpd","durationSeconds":215.5,"bitrateKbps":5000}
            """);
        Assert.True(withBitrate.BitrateKbps == 5000,
            "deklarowana przepływność skompresowanego strumienia");
    }

    private static void RejectsUnsafeUrlsAndExtraData()
    {
        var invalidUrls = new[]
        {
            "https://audio.tidal.com/audio/" + Token,
            "http://localhost:43117/audio/" + Token,
            "http://[::1]:43117/audio/" + Token,
            "http://127.0.0.1:43117/other/" + Token,
            "http://127.0.0.1:43117/audio/short",
            Url + "?token=secret",
            Url + ".mpd?token=secret",
            Url + "#fragment",
            "http://user:pass@127.0.0.1:43117/audio/" + Token,
            "http://127.0.0.1:0/audio/" + Token,
            "http://127.0.0.1:99999/audio/" + Token,
        };
        foreach (var url in invalidUrls)
        {
            var json = JsonSerializer.Serialize(new
            {
                id = "518279266", title = "Utwór", proxyUrl = url, durationSeconds = 215
            });
            ExpectRejected(json, "niedozwolony adres proxy");
        }
        ExpectRejected($$"""
            {"id":"518279266","title":"Utwór","proxyUrl":"{{Url}}","durationSeconds":215,"upstreamUrl":"secret"}
            """, "żądanie nie może przemycić adresu upstream");
    }

    private static void RejectsBadIdentityTitleAndDuration()
    {
        ExpectRejected($$"""
            {"id":518279266,"title":"Utwór","proxyUrl":"{{Url}}","durationSeconds":215}
            """, "Id musi być cyframi w stringu");
        ExpectRejected($$"""
            {"id":"tidal:518279266","title":"Utwór","proxyUrl":"{{Url}}","durationSeconds":215}
            """, "Id nie może mieć prefiksu");
        ExpectRejected($$"""
            {"id":"518279266","title":"Utwór\nsekret","proxyUrl":"{{Url}}","durationSeconds":215}
            """, "tytuł nie może zawierać znaków sterujących");
        ExpectRejected($$"""
            {"id":"518279266","title":"Utwór","proxyUrl":"{{Url}}","durationSeconds":0}
            """, "czas musi być dodatni");
        ExpectRejected($$"""
            {"id":"518279266","title":"Utwór","proxyUrl":"{{Url}}","durationSeconds":86401}
            """, "czas nie może przekroczyć doby");
        ExpectRejected($$"""
            {"id":"518279266","title":"Utwór","proxyUrl":"{{Url}}","durationSeconds":215,"bitrateKbps":48000}
            """, "częstotliwość 48 kHz nie może udawać bitrate");
    }

    private static LiteTidalNativePlayRequest Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return LiteTidalNativePlayRequest.Parse(document.RootElement);
    }

    private static void PreservesSourceRateAndLabelsPcmSeparately()
    {
        var parsed = Parse($$"""
            {"id":"42","title":"Max","proxyUrl":"{{Url}}.mpd","durationSeconds":8,
             "sampleRateHz":192000,"sourceQualityLabel":"Max, FLAC 24 bit, 192 kHz"}
            """);
        Assert.True(parsed.SampleRateHz == 192000, "częstotliwość źródła trafia do silnika");
        Assert.Equal("Max, FLAC 24 bit, 192 kHz", parsed.SourceQualityLabel!, "opis źródła");
        var text = LiteTidalNativePlayRequest.FormatPlaybackText(
            parsed.Title, parsed.SourceQualityLabel!, 5612, 192000);
        Assert.True(text.Contains("źródło Max, FLAC 24 bit, 192 kHz", StringComparison.Ordinal)
                    && text.Contains("5612 kb/s", StringComparison.Ordinal)
                    && text.Contains("PCM w silniku 192 kHz", StringComparison.Ordinal),
            "NVDA odróżnia format źródła i PCM");
        Assert.True(LiteTidalNativePlayRequest.FormatPlaybackText("AAC", "stratna, AAC", 321, 44100)
                .Contains("PCM w silniku 44,1 kHz", StringComparison.Ordinal), "polski zapis częstotliwości");
        foreach (var value in new[] { "0", "384001", "true", "44100.5", "\"192000\"" })
            ExpectRejected($$"""
                {"id":"42","title":"Max","proxyUrl":"{{Url}}","durationSeconds":8,"sampleRateHz":{{value}}}
                """, "nieprawidłowa częstotliwość");
        ExpectRejected($$"""
            {"id":"42","title":"Max","proxyUrl":"{{Url}}","durationSeconds":8,"sourceQualityLabel":"Max\nsekret"}
            """, "opis źródła nie może zawierać znaków sterujących");
    }

    private static void ExpectRejected(string json, string reason)
    {
        try
        {
            Parse(json);
        }
        catch (LiteRequestException error)
        {
            Assert.True(!error.Message.Contains("secret", StringComparison.Ordinal),
                "komunikat błędu nie ujawnia przesłanych danych");
            return;
        }
        throw new InvalidOperationException("Nie odrzucono: " + reason);
    }
}
