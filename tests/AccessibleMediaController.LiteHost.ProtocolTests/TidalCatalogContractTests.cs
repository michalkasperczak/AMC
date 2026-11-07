using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Tidal;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class TidalCatalogContractTests
{
    public static void Run()
    {
        AccountStatusAndLoginUseOnlyUserFacingLabels();
        ReadsClientIdWithoutSecret();
        ReadsSearchWithoutCredentials();
        SearchResultContainsOnlyIntentionalModelFields();
        ReadsAContainerWithoutAcceptingPrivatePlaybackData();
        ReadsOnlyKnownArtistSections();
        RefusesMismatchedKindsAndExternalIds();
        ResultContainsOnlyIntentionalModelFields();
        ReadsExternalDesktopPlaybackWithoutPrivateData();
        ReadsExternalDesktopContainerPlayback();
        ReadsOnlyKnownExternalTransportCommands();
        RefusesUnknownExternalTransportCommands();
        ReadsCollectionMembershipWithoutCredentials();
        RefusesWrongCollectionShortcutForKind();
        CollectionViewContainsOnlyIntentionalFields();
        ReadsPlaylistWriteWithoutPrivateData();
    }

    private static void ReadsClientIdWithoutSecret()
    {
        using var valid = JsonDocument.Parse("""{"clientId":" public-id_123 "} """);
        Check(LiteTidalCatalogContract.ReadClientId(valid.RootElement) == "public-id_123",
            "konfiguracja TIDAL nie odczytuje identyfikatora aplikacji");
        using var invalid = JsonDocument.Parse("""{"clientId":"wrong id"} """);
        try
        {
            LiteTidalCatalogContract.ReadClientId(invalid.RootElement);
            throw new InvalidOperationException("przyjęto niepoprawny identyfikator TIDAL");
        }
        catch (LiteRequestException exception)
        {
            Check(!exception.Message.Contains("wrong id", StringComparison.Ordinal),
                "komunikat konfiguracji TIDAL ujawnia dane wejściowe");
        }
    }

    private static void AccountStatusAndLoginUseOnlyUserFacingLabels()
    {
        var status = LiteTidalCatalogContract.CreateAccountStatus(
            configured: true,
            connected: true,
            accountDisplayName: "  Konto   próby  ");
        var login = LiteTidalCatalogContract.CreateLoginResult("Konto próby");
        var disconnect = LiteTidalCatalogContract.CreateDisconnectResult();
        var json = LiteJson.Serialize(new { status, login, disconnect });
        Check(status.Configured && status.Connected
            && status.Label == "Konto TIDAL jest połączone: Konto próby.",
            "stan konta TIDAL nie ma jednoznacznej etykiety");
        Check(login.Connected
            && login.Message == "Zalogowano konto TIDAL: Konto próby.",
            "wynik logowania TIDAL nie ma użytkowego komunikatu");
        Check(!disconnect.Connected
            && disconnect.Message.Contains("Połączenie odtwarzacza pozostaje osobne.", StringComparison.Ordinal),
            "odłączenie katalogu TIDAL nie wyjaśnia granicy odtwarzania");
        Check(!json.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("credential", StringComparison.OrdinalIgnoreCase),
            "kontrakt konta TIDAL ujawnia poświadczenia");
    }

    private static void ReadsSearchWithoutCredentials()
    {
        using var document = JsonDocument.Parse("""
        {"query":"  Artysta próby  ","token":"nie może wejść do wyszukiwania"}
        """);
        Check(LiteTidalCatalogContract.ReadSearchQuery(document.RootElement)
                == "Artysta próby",
            "zapytanie TIDAL nie zostało oczyszczone");
    }

    private static void SearchResultContainsOnlyIntentionalModelFields()
    {
        var artist = new MediaItem
        {
            Id = "tidal:artists:7",
            ExternalId = "artists:7",
            Title = "Artysta",
            Kind = MediaItemKind.Artist,
            IsAvailable = true,
            Source = "private-token-or-playback-handle"
        };
        var result = LiteTidalCatalogContract.CreateSearchResult([artist]);
        var json = LiteJson.Serialize(result);
        Check(json.Contains("Wyniki wyszukiwania TIDAL", StringComparison.Ordinal),
            "wyszukiwanie nie ma użytkowego nagłówka");
        Check(json.Contains("\"kind\":\"artist\"", StringComparison.Ordinal),
            "wynik zgubił rodzaj wykonawcy");
        Check(!json.Contains("private-token-or-playback-handle", StringComparison.Ordinal),
            "prywatne źródło wyciekło do wyszukiwania");
        Check(!json.Contains("\"source\"", StringComparison.OrdinalIgnoreCase),
            "wyszukiwanie zawiera pole źródła");
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

    private static void ReadsExternalDesktopContainerPlayback()
    {
        using var document = JsonDocument.Parse("""
        {
          "itemId":"tidal:playlists:7",
          "externalId":"playlists:7",
          "title":"Playlista próby",
          "kind":"playlist",
          "restartConsent":false,
          "token":"nie może wejść do planu"
        }
        """);
        var command = LiteTidalDesktopContract.ReadPlayRequest(document.RootElement);
        Check(command.Request.Kind == TidalDesktopPlayKind.Container,
            "playlista nie zostala kontenerem");
        Check(command.Request.PageUri.EndsWith("/playlist/7", StringComparison.Ordinal),
            "plan nie otwiera wlasciwej playlisty");
        Check(!command.Request.PageUri.Contains("token", StringComparison.OrdinalIgnoreCase),
            "prywatne pole wycieklo do planu kontenera");
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

    private static void ReadsCollectionMembershipWithoutCredentials()
    {
        using var document = JsonDocument.Parse("""
        {
          "mode":"favorite",
          "items":[{
            "itemId":"tidal:tracks:1",
            "externalId":"tracks:1",
            "title":"Utwór",
            "kind":"track",
            "artist":"Wykonawca",
            "token":"sekret",
            "source":"uchwyt"
          }]
        }
        """);
        var request = LiteTidalCatalogContract.ReadMembershipRequest(document.RootElement);
        Check(request.Mode == "favorite" && request.Items.Count == 1,
            "nie odczytano zmiany Ulubionych");
        Check(request.Items[0].Source is null, "prywatne źródło weszło do zmiany kolekcji");
    }

    private static void RefusesWrongCollectionShortcutForKind()
    {
        using var document = JsonDocument.Parse("""
        {"mode":"library","items":[{
          "itemId":"tidal:tracks:1","externalId":"tracks:1",
          "title":"Utwór","kind":"track"
        }]}
        """);
        try
        {
            LiteTidalCatalogContract.ReadMembershipRequest(document.RootElement);
            throw new InvalidOperationException("Ctrl+Shift+L przyjął utwór zamiast kontenera");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("Biblioteki TIDAL", StringComparison.Ordinal),
                "odmowa nie nazywa właściwej kolekcji");
        }
    }

    private static void CollectionViewContainsOnlyIntentionalFields()
    {
        var item = new MediaItem
        {
            Id = "tidal:tracks:1",
            ExternalId = "tracks:1",
            Title = "Utwór",
            Kind = MediaItemKind.Track,
            IsFavorite = true,
            IsAvailable = true,
            CollectionAddedUtcTicks = 123456,
            Source = "private-token"
        };
        var result = LiteTidalCatalogContract.CreateCollectionViewResult(
            "favorites", [item]);
        var json = LiteJson.Serialize(result);
        Check(json.Contains("Ulubione TIDAL", StringComparison.Ordinal),
            "brak użytkowego nagłówka");
        Check(!json.Contains("private-token", StringComparison.Ordinal),
            "źródło odtwarzania wyciekło do widoku");
        Check(!json.Contains("\"source\"", StringComparison.OrdinalIgnoreCase),
            "widok zawiera pole źródła");
        Check(json.Contains("\"collectionAddedUtcTicks\":123456", StringComparison.Ordinal),
            "widok nie przekazuje czasu dodania potrzebnego do sortowania");
    }

    private static void ReadsPlaylistWriteWithoutPrivateData()
    {
        using var document = JsonDocument.Parse("""
        {"playlistExternalIds":["playlists:42"],"items":[{
          "itemId":"tidal:albums:7","externalId":"albums:7",
          "title":"Album próby","kind":"album","token":"sekret"
        }]}
        """);
        var request = LiteTidalCatalogContract.ReadPlaylistAddRequest(document.RootElement);
        Check(request.PlaylistExternalIds.SequenceEqual(["playlists:42"])
            && request.Items.Count == 1
            && request.Items[0].Kind == MediaItemKind.Album,
            "nie odczytano albumu i docelowej playlisty TIDAL");
        Check(request.Items[0].Source is null,
            "prywatne źródło weszło do zapisu playlisty");

        using var wrong = JsonDocument.Parse("""
        {"playlistExternalIds":["tracks:42"],"items":[{
          "itemId":"tidal:tracks:7","externalId":"tracks:7",
          "title":"Utwór","kind":"track"
        }]}
        """);
        try
        {
            LiteTidalCatalogContract.ReadPlaylistAddRequest(wrong.RootElement);
            throw new InvalidOperationException("Przyjęto utwór jako playlistę TIDAL");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("playlist", StringComparison.OrdinalIgnoreCase),
                "odmowa ma nieczytelny komunikat");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
