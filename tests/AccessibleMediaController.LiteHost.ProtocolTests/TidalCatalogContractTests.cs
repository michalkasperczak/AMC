using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class TidalCatalogContractTests
{
    public static void Run()
    {
        ReadsAContainerWithoutAcceptingPrivatePlaybackData();
        ReadsOnlyKnownArtistSections();
        RefusesMismatchedKindsAndExternalIds();
        ResultContainsOnlyIntentionalModelFields();
        ReadsExternalDesktopPlaybackWithoutPrivateData();
        ReadsOnlyKnownExternalTransportCommands();
        RefusesUnknownExternalTransportCommands();
    }

    private static void ReadsAContainerWithoutAcceptingPrivatePlaybackData()
    {
        using var document = JsonDocument.Parse("""
        {
          "itemId":"tidal:albums:123",
          "externalId":"albums:123",
          "title":"Album próby",
          "artist":"Wykonawca",
          "kind":"album",
          "publicUri":"https://tidal.com/browse/album/123",
          "source":"private-token-or-playback-handle"
        }
        """);
        var request = LiteTidalCatalogContract.ReadRequest(document.RootElement);
        Check(request.Container.Id == "tidal:albums:123", "zgubiono stabilne ID kontenera");
        Check(request.Container.ExternalId == "albums:123", "zgubiono ID katalogowe kontenera");
        Check(request.Container.Title == "Album próby", "zgubiono nazwę użytkową");
        Check(request.Container.Source is null, "prywatne źródło nie może wejść do kontraktu");
    }

    private static void ReadsOnlyKnownArtistSections()
    {
        foreach (var section in new[] { "albums", "tracks", "similarArtists" })
        {
            using var document = JsonDocument.Parse($$"""
            {"itemId":"tidal:artists:7","externalId":"artists:7","title":"Artysta","kind":"artist","artistSection":"{{section}}"}
            """);
            var request = LiteTidalCatalogContract.ReadRequest(document.RootElement);
            Check(request.ArtistSection is not null, $"nie odczytano sekcji {section}");
        }
    }

    private static void RefusesMismatchedKindsAndExternalIds()
    {
        using var wrong = JsonDocument.Parse("""
        {"itemId":"x","externalId":"tracks:7","title":"Nie kontener","kind":"album"}
        """);
        try
        {
            LiteTidalCatalogContract.ReadRequest(wrong.RootElement);
            throw new InvalidOperationException("przyjęto album z identyfikatorem utworu");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("tożsamość", StringComparison.Ordinal),
                "odmowa nie nazywa przyczyny");
        }
    }

    private static void ResultContainsOnlyIntentionalModelFields()
    {
        var container = new MediaItem
        {
            Id = "tidal:albums:123",
            ExternalId = "albums:123",
            Title = "Album próby",
            Kind = MediaItemKind.Album
        };
        var visible = new MediaItem
        {
            Id = "tidal:tracks:1",
            ExternalId = "tracks:1",
            Title = "Utwór",
            Artist = "Wykonawca",
            Kind = MediaItemKind.Track,
            Duration = TimeSpan.FromMinutes(3),
            Source = "private-playback-handle",
            PublicUri = "https://tidal.com/browse/track/1",
            RelatedAlbumExternalId = "albums:44",
            RelatedAlbumTitle = "Album próby",
            IsFavorite = true,
            IsAvailable = true
        };
        var hidden = new MediaItem
        {
            Id = "tidal:tracks:2",
            ExternalId = "tracks:2",
            Title = "Niedostępny",
            Kind = MediaItemKind.Track,
            IsAvailable = false
        };
        var result = LiteTidalCatalogContract.CreateResult(container, null, [visible, hidden]);
        var json = LiteJson.Serialize(result);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.GetProperty("heading").GetString() == "Album, Album próby",
            "nagłówek nie jest użytkowy");
        var items = root.GetProperty("items");
        Check(items.GetArrayLength() == 1, "niedostępny element wszedł do listy");
        var item = items[0];
        Check(item.GetProperty("title").GetString() == "Utwór", "brak nazwy");
        Check(item.GetProperty("kind").GetString() == "track", "rodzaj nie jest kanoniczny");
        Check(!item.TryGetProperty("source", out _), "prywatne źródło wyciekło do protokołu");
        Check(!json.Contains("private-playback-handle", StringComparison.Ordinal),
            "prywatny uchwyt wyciekł do JSON");
        Check(!json.Contains("token", StringComparison.OrdinalIgnoreCase),
            "odpowiedź zawiera pole tokenu");
        Check(item.GetProperty("relatedAlbumExternalId").GetString() == "albums:44",
            "brak relacji albumu potrzebnej oryginalnemu TIDALowi");
    }

    private static void ReadsExternalDesktopPlaybackWithoutPrivateData()
    {
        using var document = JsonDocument.Parse("""
        {
          "itemId":"tidal:tracks:1",
          "externalId":"tracks:1",
          "title":"Utwór próby",
          "relatedAlbumExternalId":"albums:44",
          "restartConsent":true,
          "token":"nie może wejść do planu",
          "source":"prywatny uchwyt"
        }
        """);
        var command = LiteTidalDesktopContract.ReadPlayRequest(document.RootElement);
        Check(command.ItemId == "tidal:tracks:1", "zgubiono stabilną tożsamość");
        Check(command.ExternalId == "tracks:1", "zgubiono tożsamość katalogową");
        Check(command.Request.TrackTitle == "Utwór próby", "zgubiono tytuł utworu");
        Check(command.Request.PageUri.EndsWith("/album/44", StringComparison.Ordinal),
            "plan nie otwiera właściwego albumu");
        Check(command.RestartConsent, "zgubiono świadomą zgodę na restart");
        Check(!command.Request.PageUri.Contains("token", StringComparison.OrdinalIgnoreCase),
            "prywatne pole wyciekło do planu");
    }

    private static void RefusesUnknownExternalTransportCommands()
    {
        using var document = JsonDocument.Parse("""{"command":"seek"}""");
        try
        {
            LiteTidalDesktopContract.ReadTransportCommand(document.RootElement);
            throw new InvalidOperationException("przyjęto przewijanie niedostępne w TIDALu");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("Nieznane polecenie", StringComparison.Ordinal),
                "odmowa nie nazywa przyczyny");
        }
    }

    private static void ReadsOnlyKnownExternalTransportCommands()
    {
        foreach (var command in new[] { "toggle", "next", "previous", "state" })
        {
            using var document = JsonDocument.Parse($$"""{"command":"{{command}}"}""");
            Check(LiteTidalDesktopContract.ReadTransportCommand(document.RootElement) == command,
                $"nie przyjeto znanego polecenia {command}");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
