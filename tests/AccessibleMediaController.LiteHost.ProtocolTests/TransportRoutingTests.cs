using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class TransportRoutingTests
{
    public static void Run()
    {
        ExplicitSessionsChooseTheirOwnEngine();
        LegacyCallsKeepTheLastEngine();
        UnknownSessionIsRejected();
        Console.WriteLine("TransportRoutingTests: OK");
    }

    private static void ExplicitSessionsChooseTheirOwnEngine()
    {
        var radio = LiteTransportRouting.Resolve("radio", "files", false);
        var files = LiteTransportRouting.Resolve("files", "radio", true);
        var podcasts = LiteTransportRouting.Resolve("podcasts", "radio", false);
        var spotify = LiteTransportRouting.Resolve("spotify", "files", false);
        Assert.True(radio == new LiteTransportTarget("radio", "radio"),
            "Radio ma własny tor niezależnie od ostatniego polecenia.");
        Assert.True(files == new LiteTransportTarget("files", "files"),
            "Pliki mają własny tor niezależnie od Radia.");
        Assert.True(podcasts == new LiteTransportTarget("podcasts", "files"),
            "Podcasty używają lokalnego toru plikowego, ale zachowują nazwę sesji.");
        Assert.True(spotify == new LiteTransportTarget("spotify", "spotify"),
            "Spotify ma własny tor Librespot i nie może trafić do plików.");
        var radioarchiwista = LiteTransportRouting.Resolve("radioarchiwista", "radio", false);
        Assert.True(radioarchiwista == new LiteTransportTarget("radioarchiwista", "files"),
            "Radio Archiwista używa lokalnego toru plikowego i zachowuje nazwę sesji.");
    }

    private static void LegacyCallsKeepTheLastEngine()
    {
        Assert.True(
            LiteTransportRouting.Resolve(null, "radio", false)
                == new LiteTransportTarget("radio", "radio"),
            "Stary klient bez sessionId zachowuje ostatnie Radio.");
        Assert.True(
            LiteTransportRouting.Resolve(null, "files", true)
                == new LiteTransportTarget("podcasts", "files"),
            "Stary klient rozpoznaje odcinek na torze plikowym.");
        Assert.True(
            LiteTransportRouting.Resolve(null, "spotify", false)
                == new LiteTransportTarget("spotify", "spotify"),
            "Stare polecenie bez sessionId zachowuje ostatni tor Spotify.");
    }

    private static void UnknownSessionIsRejected()
    {
        try
        {
            LiteTransportRouting.Resolve("tidal", "files", false);
            throw new InvalidOperationException("Nieznana sesja nie została odrzucona.");
        }
        catch (LiteRequestException)
        {
        }
    }
}
