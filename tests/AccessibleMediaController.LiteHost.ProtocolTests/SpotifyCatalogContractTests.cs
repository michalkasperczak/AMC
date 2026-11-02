using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.LiteHost.Protocol;

namespace AccessibleMediaController.LiteHost.ProtocolTests;

internal static class SpotifyCatalogContractTests
{
    public static void Run()
    {
        CapabilitiesDescribeOneReadOnlySession();
        LoginResultContainsOnlyUserFacingState();
        PlaybackRequestAcceptsOnlyTracksAndEpisodes();
        AccountStatusKeepsCatalogueAndPlaybackSeparate();
        MissingClientIdIsAnExplicitSafeState();
        ConfigureClientIdRejectsSecretsAndUpdatesBackend();
        PairingKeepsDeviceSecretOutOfProtocol();
        CollectionRootContainsOnlyUserFacingCategories();
        CollectionViewRequestUsesSafeCumulativePageSizes();
        CollectionViewsKeepUnavailableSavedItems();
        CollectionViewsPageAfterFilteringAndKeepSourceOrder();
        CollectionViewsSortBeforePaging();
        SearchRowsHaveExplicitAccessibleLabelsWithoutPrivateHandles();
        ContainerRequestAcceptsOnlyMatchingSpotifyIdentity();
        DescriptionRequestAcceptsOnlyPodcastMaterialsAndCleansText();
        MembershipRequestSeparatesFavoritesFromLibrary();
        PlaylistRequestsAcceptOnlyNamedTargetsAndPlayableItems();
        ContainerResultNeverUsesIdentifiersInItsHeading();
        ContainerKeepsRepeatedPlaylistOccurrencesDistinct();
        CoordinatorDoesNotAuthenticateForStatusOrCapabilities();
        CoordinatorRedactsCredentialProbeFailure();
        CoordinatorUsesCachedCollectionWithoutAConnection();
        CoordinatorUsesPopulatedCacheImmediatelyWhenConnected();
        CoordinatorRefreshesOnlyAlbumsToFillDurations();
        CoordinatorRefreshesPodcastsWithPopulatedOtherCacheExactlyOnce();
        CoordinatorClearsStalePodcastsAfterSuccessfulEmptyRefresh();
        CoordinatorKeepsPodcastCacheWhenOnlyPartOfRefreshSucceeds();
        CoordinatorLoginInvalidatesPreviousPodcastSnapshot();
        CoordinatorRedactsBackendFailures();
        CoordinatorSearchesExactlyOnceWithoutMutatingAnything();
        CoordinatorReadsOneDescriptionWithoutMutation();
        CoordinatorReadsEpisodeChaptersFromDescriptionWithoutMutation();
        CoordinatorShowsConfirmedCreatedPlaylistInCollectionImmediately();
        CoordinatorRefreshesPlaylistViewOnceWithoutFullCollectionSync();
        CoordinatorLoadsCollectionAfterOpeningPlaylistsFirst();
        CoordinatorLoginInvalidatesPreviousPlaylistSnapshot();
    }

    private static void CapabilitiesDescribeOneReadOnlySession()
    {
        var json = LiteJson.Serialize(LiteSpotifyCatalogContract.CreateCapabilities());
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.GetProperty("sessionId").GetString() == "spotify",
            "kontrakt nie wskazuje jednej kanonicznej sesji spotify");
        Check(root.GetProperty("readOnly").GetBoolean(),
            "pierwsza warstwa Spotify nie jest oznaczona jako tylko do odczytu");
        Check(root.GetProperty("oneSession").GetBoolean(),
            "kontrakt nie deklaruje jednej sesji");
        Check(!root.GetProperty("collectionWrite").GetBoolean()
            && !root.GetProperty("login").GetBoolean()
            && root.GetProperty("playback").GetBoolean(),
            "kontrakt nie rozdziela odtwarzania od mutacji i logowania");
        Check(!json.Contains("spotifyLibrespot", StringComparison.OrdinalIgnoreCase),
            "historyczna druga sesja wyciekła do kontraktu");
        var withLogin = LiteSpotifyCatalogContract.CreateCapabilities(login: true);
        Check(withLogin.Login, "adapter konta nie ujawnia dostępnego logowania");
    }

    private static void LoginResultContainsOnlyUserFacingState()
    {
        var result = LiteSpotifyCatalogContract.CreateLoginResult(
            "  Michał\r\nPróba  ",
            "premium");
        var json = LiteJson.Serialize(result);
        Check(result.Connected
            && result.Message.Contains("MichałPróba", StringComparison.Ordinal)
            && result.Message.Contains("Premium", StringComparison.Ordinal),
            "wynik logowania Spotify nie ma bezpiecznej informacji dla użytkownika");
        Check(!json.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("scope", StringComparison.OrdinalIgnoreCase),
            "wynik logowania Spotify ujawnia dane techniczne");
    }

    private static void PlaybackRequestAcceptsOnlyTracksAndEpisodes()
    {
        using var good = JsonDocument.Parse("""
        {
          "itemId":"spotify:track:abc123:occurrence:2",
          "externalId":"abc123",
          "title":"Utwór\r\npróby",
          "artist":"Wykonawca",
          "kind":"track",
          "volume":41,
          "positionSeconds":12.5,
          "playbackRate":1.25,
          "token":"nie może przejść"
        }
        """);
        var request = LiteSpotifyPlaybackContract.ReadPlay(good.RootElement);
        Check(request.Item.Kind == MediaItemKind.Track
            && request.Item.ExternalId == "abc123"
            && request.Item.Source == "spotify:track:abc123"
            && request.Item.Title == "Utwórpróby"
            && request.Volume == 41
            && request.Position == TimeSpan.FromSeconds(12.5)
            && Math.Abs(request.PlaybackRate - 1.25d) < 0.001d,
            "poprawny utwór nie został bezpiecznie przygotowany do Librespota");

        using var container = JsonDocument.Parse("""
        {"itemId":"album","externalId":"abc123","title":"Album","kind":"album"}
        """);
        try
        {
            LiteSpotifyPlaybackContract.ReadPlay(container.RootElement);
            throw new InvalidOperationException("kontener Spotify trafił do odtwarzania");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("utwór albo odcinek", StringComparison.Ordinal),
                "odmowa kontenera nie podpowiada właściwego działania");
        }
    }

    private static void MembershipRequestSeparatesFavoritesFromLibrary()
    {
        using var album = JsonDocument.Parse("""
        {
          "mode":"library",
          "items":[{
            "itemId":"spotify:album:album123",
            "externalId":"album123",
            "title":"Album",
            "kind":"album",
            "artist":"Wykonawca"
          }]
        }
        """);
        var request = LiteSpotifyCatalogContract.ReadMembershipRequest(
            album.RootElement);
        Check(request.Mode == "library"
            && request.Items.Single().Kind == MediaItemKind.Album
            && request.Items.Single().ExternalId == "album123",
            "album Spotify nie został przygotowany do zapisu Biblioteki");

        using var episode = JsonDocument.Parse("""
        {
          "mode":"library",
          "items":[{
            "itemId":"spotify:episode:episode123",
            "externalId":"episode123",
            "title":"Odcinek",
            "kind":"episode"
          }]
        }
        """);
        var episodeRequest = LiteSpotifyCatalogContract.ReadMembershipRequest(
            episode.RootElement);
        Check(episodeRequest.Mode == "library"
            && episodeRequest.Items.Single().Kind == MediaItemKind.Episode,
            "odcinek Spotify nie został przygotowany do zapisu Biblioteki");

        using var wrong = JsonDocument.Parse("""
        {
          "mode":"favorite",
          "items":[{
            "itemId":"spotify:album:album123",
            "externalId":"album123",
            "title":"Album",
            "kind":"album"
          }]
        }
        """);
        try
        {
            LiteSpotifyCatalogContract.ReadMembershipRequest(wrong.RootElement);
            throw new InvalidOperationException("album przyjęto do Ulubionych Spotify");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("utwór albo odcinek", StringComparison.Ordinal),
                "odmowa nie wyjaśnia rodzajów obsługiwanych przez Ulubione Spotify");
        }

        var writable = LiteSpotifyCatalogContract.CreateCapabilities(
            collectionWrite: true);
        Check(!writable.ReadOnly && writable.CollectionWrite,
            "host z pisarzem Spotify nadal zgłasza kolekcję tylko do odczytu");
    }

    private static void PlaylistRequestsAcceptOnlyNamedTargetsAndPlayableItems()
    {
        using var create = JsonDocument.Parse("""
        {"name":"  Wieczór\r\n  ","isPublic":true,"token":"nie przechodzi"}
        """);
        var createRequest = LiteSpotifyCatalogContract.ReadPlaylistCreateRequest(
            create.RootElement);
        Check(createRequest.Name == "Wieczór" && createRequest.IsPublic,
            "tworzenie playlisty nie czyści nazwy albo widoczności");

        using var add = JsonDocument.Parse("""
        {
          "playlistIds":["one","one","two"],
          "items":[
            {"itemId":"row1","externalId":"track1","title":"Utwór","kind":"track"},
            {"itemId":"row2","externalId":"episode2","title":"Odcinek","kind":"episode"}
          ]
        }
        """);
        var addRequest = LiteSpotifyCatalogContract.ReadPlaylistAddRequest(add.RootElement);
        Check(addRequest.PlaylistIds.SequenceEqual(["one", "two"])
            && addRequest.Items.Select(item => item.Kind).SequenceEqual(
                [MediaItemKind.Track, MediaItemKind.Episode]),
            "żądanie nie odduplikowało playlist albo pomyliło rodzaj elementu");

        using var wrong = JsonDocument.Parse("""
        {
          "playlistIds":["one"],
          "items":[{"itemId":"row","externalId":"album","title":"Album","kind":"album"}]
        }
        """);
        try
        {
            LiteSpotifyCatalogContract.ReadPlaylistAddRequest(wrong.RootElement);
            throw new InvalidOperationException("album przyjęto do playlisty jako odtwarzalny element");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("utwór albo odcinek", StringComparison.Ordinal),
                "odmowa albumu nie wyjaśnia obsługiwanych rodzajów");
        }

        var target = LiteSpotifyCatalogContract.ToPlaylistTarget(
            new SpotifyPlaylistTarget(
                "secret-id", "Nazwa", "owner-id", "Michał", false, false, true,
                "secret-snapshot"));
        Check(target.ToString() == target.AccessibleLabel
            && !target.AccessibleLabel.Contains("secret", StringComparison.Ordinal),
            "natywna lista mogłaby odczytać rekord albo identyfikator techniczny");
    }

    private static void AccountStatusKeepsCatalogueAndPlaybackSeparate()
    {
        var result = LiteSpotifyCatalogContract.CreateAccountStatus(
            new LiteSpotifyAccountProbe(
                true, true, "Konto\r\nPróby", "premium", false));
        var json = LiteJson.Serialize(result);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Check(root.GetProperty("catalog").GetProperty("state").GetString() == "connected",
            "połączony katalog nie ma właściwego stanu");
        Check(root.GetProperty("playback").GetProperty("state").GetString() == "disconnected",
            "osobny stan odtwarzania został utożsamiony z katalogiem");
        Check(root.GetProperty("catalog").GetProperty("accountPlan").GetString() == "Premium",
            "techniczna wartość planu nie została zamieniona na etykietę");
        Check(!json.Contains('\r') && !json.Contains('\n'),
            "znaki sterujące weszły do etykiety konta");
        Check(!json.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("refresh", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("spotifyLibrespot", StringComparison.OrdinalIgnoreCase),
            "status konta zawiera prywatne albo historyczne pole");
    }

    private static void MissingClientIdIsAnExplicitSafeState()
    {
        var result = LiteSpotifyCatalogContract.CreateAccountStatus(
            new LiteSpotifyAccountProbe(false, false, string.Empty, string.Empty, true));
        Check(result.Catalog.State == "notConfigured" && !result.Catalog.Configured,
            "brak ClientId nie daje jawnego stanu notConfigured");
        Check(result.Playback.State == "connected",
            "osobny zapis odtwarzania zniknął przy braku konfiguracji katalogu");
    }

    private static void CollectionRootContainsOnlyUserFacingCategories()
    {
        var result = LiteSpotifyCatalogContract.CreateCollectionRoot();
        Check(result.Views.Select(view => view.View).SequenceEqual(
            new[] { "liked", "albums", "artists", "playlists", "podcasts", "episodes" }),
            "korzeń biblioteki ma nieoczekiwaną kolejność albo zakres");
        Check(result.Views.All(view => !view.Label.Contains("spotify:", StringComparison.OrdinalIgnoreCase)),
            "identyfikator usługi trafił do etykiety kategorii");
    }

    private static void CollectionViewsKeepUnavailableSavedItems()
    {
        var unavailable = Item(
            MediaItemKind.Track,
            "track123",
            "Wycofany utwór",
            favorite: true,
            available: false);
        var album = Item(
            MediaItemKind.Album,
            "album123",
            "Album",
            library: true);
        var result = LiteSpotifyCatalogContract.CreateCollectionViewResult(
            "liked", [unavailable, album]);
        Check(result.Items.Count == 1,
            "widok polubionych zgubił niedostępny zapis albo przyjął album");
        Check(!result.Items[0].IsAvailable
            && result.Items[0].AccessibleLabel.EndsWith(", niedostępny", StringComparison.Ordinal),
            "niedostępna zapisana pozycja nie ma uczciwej etykiety");
    }

    private static void CollectionViewRequestUsesSafeCumulativePageSizes()
    {
        using var missing = JsonDocument.Parse("""{"view":"liked"}""");
        using var tooSmall = JsonDocument.Parse(
            """{"view":"liked","loadedCount":3}""");
        using var nextPage = JsonDocument.Parse(
            """{"view":"liked","loadedCount":300}""");

        Check(LiteSpotifyCatalogContract.ReadCollectionLoadedCount(missing.RootElement)
                == LiteSpotifyCatalogContract.CollectionPageSize
            && LiteSpotifyCatalogContract.ReadCollectionLoadedCount(tooSmall.RootElement)
                == LiteSpotifyCatalogContract.CollectionPageSize
            && LiteSpotifyCatalogContract.ReadCollectionLoadedCount(nextPage.RootElement) == 300,
            "rozmiar porcji kolekcji nie ma bezpiecznego domyślnego minimum");
        Check(LiteSpotifyCatalogContract.ReadCollectionSortMode(missing.RootElement)
                == "AddedNewest",
            "brak trybu sortowania nie wraca do bezpiecznego porządku domyślnego");
        using var alphabetical = JsonDocument.Parse(
            """{"view":"liked","sortMode":"Alphabetical"}""");
        using var invalidSort = JsonDocument.Parse(
            """{"view":"liked","sortMode":"TechnicalEnum { value = 1 }"}""");
        Check(LiteSpotifyCatalogContract.ReadCollectionSortMode(alphabetical.RootElement)
                == "Alphabetical"
            && LiteSpotifyCatalogContract.ReadCollectionSortMode(invalidSort.RootElement)
                == "AddedNewest",
            "tryb sortowania przyjął techniczną lub nieznaną wartość");
    }

    private static void CollectionViewsPageAfterFilteringAndKeepSourceOrder()
    {
        var items = Enumerable.Range(0, 152)
            .Select(index => Item(
                MediaItemKind.Track,
                $"track{index}",
                $"Utwór {index}",
                favorite: true))
            .ToList();
        items.Insert(1, Item(
            MediaItemKind.Album,
            "albumIgnored",
            "Album pomijany",
            library: true));
        items.Insert(2, Item(
            MediaItemKind.Track,
            "track0",
            "Duplikat pomijany",
            favorite: true));

        var first = LiteSpotifyCatalogContract.CreateCollectionViewResult(
            "liked", items, loadedCount: 150);
        var second = LiteSpotifyCatalogContract.CreateCollectionViewResult(
            "liked", items, loadedCount: 300);

        Check(first.Items.Count == 150 && first.HasMore,
            "pierwsza porcja kolekcji nie ma 150 elementów albo zgubiła hasMore");
        Check(first.Items[0].Title == "Utwór 0"
            && first.Items[149].Title == "Utwór 149",
            "filtrowanie lub usuwanie duplikatów zmieniło kolejność przed stronicowaniem");
        Check(second.Items.Count == 152 && !second.HasMore
            && second.Items[^1].Title == "Utwór 151",
            "druga porcja nie odsłoniła reszty w tej samej kolejności");
    }

    private static void CollectionViewsSortBeforePaging()
    {
        var items = Enumerable.Range(0, 151)
            .Select(index => Item(
                MediaItemKind.Track,
                $"track{index}",
                $"Z utwór {index:000}",
                favorite: true))
            .ToList();
        items.Add(Item(
            MediaItemKind.Track,
            "alphabeticalFirst",
            "A pierwszy",
            favorite: true));

        var alphabetical = LiteSpotifyCatalogContract.CreateCollectionViewResult(
            "liked",
            items,
            loadedCount: 150,
            sortMode: "Alphabetical");
        Check(alphabetical.Items.Count == 150
            && alphabetical.HasMore
            && alphabetical.Items[0].Title == "A pierwszy",
            "sortowanie alfabetyczne zostało wykonane dopiero po stronicowaniu");

        var oldest = Item(MediaItemKind.Track, "oldest", "Najstarszy", favorite: true);
        oldest.CollectionAddedUtcTicks = 10;
        var newest = Item(MediaItemKind.Track, "newest", "Najnowszy", favorite: true);
        newest.CollectionAddedUtcTicks = 30;
        var undated = Item(MediaItemKind.Track, "undated", "Bez daty", favorite: true);
        var added = LiteSpotifyCatalogContract.CreateCollectionViewResult(
            "liked",
            [oldest, undated, newest],
            sortMode: "AddedNewest");
        var custom = LiteSpotifyCatalogContract.CreateCollectionViewResult(
            "liked",
            [oldest, undated, newest],
            sortMode: "Custom");
        Check(added.Items.Select(item => item.Title).SequenceEqual(
                new[] { "Najnowszy", "Najstarszy", "Bez daty" }),
            "porządek według dodania nie jest stabilny albo źle układa brak daty");
        Check(custom.Items.Select(item => item.Title).SequenceEqual(
                new[] { "Najstarszy", "Bez daty", "Najnowszy" }),
            "kolejność katalogu nie zachowała źródłowego cache Spotify");
    }

    private static void SearchRowsHaveExplicitAccessibleLabelsWithoutPrivateHandles()
    {
        var item = Item(MediaItemKind.Track, "abc123", "Utwór", artist: "Wykonawca");
        item.Source = "Bearer sekret spotify:track:abc123";
        item.PublicUri = "https://open.spotify.com/track/abc123";
        item.RelatedAlbumExternalId = "album987";
        item.RelatedAlbumTitle = "Album";
        var result = LiteSpotifyCatalogContract.CreateSearchResult([item]);
        var json = LiteJson.Serialize(result);
        var row = result.Items.Single();
        Check(row.AccessibleLabel == "Utwór, Wykonawca, utwór",
            "wiersz nie ma jednoznacznej etykiety dostępnościowej");
        Check(!row.AccessibleLabel.Contains("abc123", StringComparison.Ordinal)
            && !row.AccessibleLabel.Contains("open.spotify.com", StringComparison.OrdinalIgnoreCase),
            "ID albo URI weszło do etykiety dostępnościowej");
        Check(!json.Contains("Bearer sekret", StringComparison.Ordinal)
            && !json.Contains("\"source\"", StringComparison.OrdinalIgnoreCase),
            "prywatny uchwyt odtwarzania wyciekł do DTO");
        Check(row.PublicUri == "https://open.spotify.com/track/abc123",
            "jawny publiczny adres nie pozostał oddzielnym polem modelu");
        Check(result.Warning.Contains("10 wyników każdego rodzaju", StringComparison.Ordinal),
            "wynik wyszukiwania nie ujawnia aktualnego limitu dostawcy");
    }

    private static void ContainerRequestAcceptsOnlyMatchingSpotifyIdentity()
    {
        using var good = JsonDocument.Parse("""
        {
          "externalId":"spotify:album:album123",
          "title":"Album próby",
          "artist":"Wykonawca",
          "kind":"album",
          "loadedCount":300,
          "token":"sekret",
          "source":"prywatny uchwyt"
        }
        """);
        var request = LiteSpotifyCatalogContract.ReadContainerRequest(good.RootElement);
        Check(request.ExternalId == "album123"
            && request.Kind == MediaItemKind.Album
            && request.LoadedCount == 300,
            "poprawny kontener nie został znormalizowany");

        using var wrong = JsonDocument.Parse("""
        {"externalId":"spotify:track:track123","title":"Nie album","kind":"album"}
        """);
        try
        {
            LiteSpotifyCatalogContract.ReadContainerRequest(wrong.RootElement);
            throw new InvalidOperationException("przyjęto album z identyfikatorem utworu");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("tożsamość", StringComparison.Ordinal),
                "odmowa nie ma użytkowego komunikatu");
        }
    }

    private static void ContainerResultNeverUsesIdentifiersInItsHeading()
    {
        var request = new LiteSpotifyContainerRequest(
            MediaItemKind.Artist, "artist123", "Wykonawca", string.Empty, "tracks");
        var result = LiteSpotifyCatalogContract.CreateContainerResult(
            request,
            [Item(MediaItemKind.Track, "track123", "Utwór")],
            available: true,
            hasMore: true);
        Check(result.Heading == "Utwory wykonawcy, Wykonawca",
            "nagłówek kontenera nie jest użytkowy");
        Check(!result.Heading.Contains("artist123", StringComparison.Ordinal)
            && result.HasMore,
            "ID weszło do nagłówka albo zgubiono informację o dalszych wynikach");
    }

    private static void DescriptionRequestAcceptsOnlyPodcastMaterialsAndCleansText()
    {
        using var good = JsonDocument.Parse(
            """{"externalId":"spotify:episode:episode123","kind":"episode"}""");
        var request = LiteSpotifyCatalogContract.ReadDescriptionRequest(
            good.RootElement);
        var result = LiteSpotifyCatalogContract.CreateDescriptionResult(
            " Pierwszy wiersz.\0\r\nDrugi wiersz. ");
        Check(request.Kind == MediaItemKind.Episode
            && request.ExternalId == "episode123"
            && result.Description == "Pierwszy wiersz.\r\nDrugi wiersz.",
            "opis odcinka nie został bezpiecznie znormalizowany");

        using var album = JsonDocument.Parse(
            """{"externalId":"album123","kind":"album"}""");
        try
        {
            LiteSpotifyCatalogContract.ReadDescriptionRequest(album.RootElement);
            throw new InvalidOperationException("przyjęto opis albumu jako podcast");
        }
        catch (LiteRequestException exception)
        {
            Check(exception.Message.Contains("podcastu albo odcinka", StringComparison.Ordinal),
                "odmowa opisu albumu nie ma użytkowej etykiety");
        }
    }

    private static void ContainerKeepsRepeatedPlaylistOccurrencesDistinct()
    {
        var request = new LiteSpotifyContainerRequest(
            MediaItemKind.Playlist, "playlist123", "Playlista", string.Empty, null);
        var first = Item(MediaItemKind.Track, "track123", "Powtórzony utwór");
        var second = Item(MediaItemKind.Track, "track123", "Powtórzony utwór");
        var result = LiteSpotifyCatalogContract.CreateContainerResult(
            request, [first, second], available: true);
        Check(result.Items.Count == 2 && result.Items[0].Id != result.Items[1].Id,
            "powtórne wystąpienie na playliście zostało zgubione albo ma ten sam klucz wiersza");
        Check(result.Items[0].ExternalId == result.Items[1].ExternalId,
            "tożsamość zasobu zmieniła się przy rozróżnianiu wystąpień");
    }

    private static void CoordinatorDoesNotAuthenticateForStatusOrCapabilities()
    {
        using var backend = new FakeBackend();
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        _ = coordinator.Capabilities();
        _ = coordinator.AccountStatus();
        _ = coordinator.CollectionRoot();
        Check(backend.RefreshCalls == 0
            && backend.AlbumRefreshCalls == 0
            && backend.SearchCalls == 0
            && backend.ContainerCalls == 0,
            "status albo możliwości uruchomiły operację katalogową");
    }

    private static void ConfigureClientIdRejectsSecretsAndUpdatesBackend()
    {
        using var backend = new WritableFakeBackend();
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var valid = JsonDocument.Parse(
            """{"clientId":"0123456789abcdef0123456789abcdef"}""");
        using var invalid = JsonDocument.Parse(
            """{"clientId":"a client secret and token"}""");
        try
        {
            _ = coordinator.Configure(invalid.RootElement);
            throw new InvalidOperationException("Nieprawidłowy Client ID został przyjęty.");
        }
        catch (LiteRequestException)
        {
        }
        var result = LiteJson.Serialize(coordinator.Configure(valid.RootElement));
        Check(backend.ClientId == "0123456789abcdef0123456789abcdef"
            && !result.Contains(backend.ClientId, StringComparison.Ordinal),
            "Zapis Client ID nie dotarł do backendu albo ujawnił go w odpowiedzi.");
    }

    private static void PairingKeepsDeviceSecretOutOfProtocol()
    {
        using var backend = new WritableFakeBackend();
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        var start = LiteJson.Serialize(coordinator.StartPairing());
        var status = (LiteSpotifyPairingStatusResult)coordinator.PairingStatus();
        var cancelled = (LiteSpotifyPairingStatusResult)coordinator.CancelPairing();
        Check(start.Contains("ABCD-EFGH", StringComparison.Ordinal)
            && start.Contains("https://www.spotify.com/pair", StringComparison.Ordinal)
            && !start.Contains("device-secret", StringComparison.Ordinal)
            && status.State == "pending"
            && cancelled.State == "cancelled",
            "Parowanie nie zwróciło wyłącznie kodu użytkownika i bezpiecznego stanu.");
    }

    private static void CoordinatorRedactsCredentialProbeFailure()
    {
        const string secret = "credential target and token must stay private";
        using var backend = new FakeBackend
        {
            AccountFailure = new InvalidOperationException(secret)
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        var status = (LiteSpotifyAccountStatusResult)coordinator.AccountStatus();
        var json = LiteJson.Serialize(status);
        Check(status.Catalog.State == "unavailable"
            && status.Playback.State == "unavailable",
            "awaria odczytu poświadczeń nie daje bezpiecznego stanu unavailable");
        Check(!json.Contains(secret, StringComparison.Ordinal),
            "szczegóły awarii poświadczeń wyciekły do statusu konta");
    }

    private static void CoordinatorUsesCachedCollectionWithoutAConnection()
    {
        using var backend = new FakeBackend
        {
            Account = new LiteSpotifyAccountProbe(
                true, false, string.Empty, string.Empty, false),
            Cached = [Item(MediaItemKind.Album, "album123", "Zapisany album", library: true)]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var args = JsonDocument.Parse("""{"view":"albums"}""");
        var result = (LiteSpotifyItemsResult)coordinator.CollectionView(args.RootElement);
        Check(result.Items.Count == 1 && backend.RefreshCalls == 0,
            "widok bez połączenia nie użył cache albo próbował wejść do sieci");
        Check(result.Warning.Contains("Pokazano ostatni zapis", StringComparison.Ordinal),
            "fallback cache nie jest jawny dla użytkownika");
    }

    private static void CoordinatorUsesPopulatedCacheImmediatelyWhenConnected()
    {
        using var backend = new FakeBackend
        {
            Cached = [Item(MediaItemKind.Album, "cached", "Album z zapisu", library: true)],
            RefreshedItems = [Item(MediaItemKind.Album, "network", "Album z sieci", library: true)]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var args = JsonDocument.Parse("""{"view":"albums"}""");

        var result = (LiteSpotifyItemsResult)coordinator.CollectionView(args.RootElement);

        Check(backend.RefreshCalls == 0,
            "otwarcie zapisanej kolekcji uruchomiło pełną synchronizację przed pokazaniem listy");
        Check(backend.AlbumRefreshCalls == 1,
            "widok Albumy nie wykonał wąskiego odświeżenia czasów albumów");
        Check(result.Items.Count == 1 && result.Items[0].Title == "Album z zapisu",
            "wąskie odświeżenie zmieniło istniejący zapis kolekcji");
    }

    private static void CoordinatorRefreshesOnlyAlbumsToFillDurations()
    {
        var cached = Item(
            MediaItemKind.Album, "album123", "Album", library: true);
        var refreshed = Item(
            MediaItemKind.Album, "album123", "Album", library: true);
        refreshed.Duration = TimeSpan.FromMinutes(38);
        using var backend = new FakeBackend
        {
            Cached = [cached],
            RefreshedAlbums = [refreshed]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var args = JsonDocument.Parse("""{"view":"albums"}""");

        var first = (LiteSpotifyItemsResult)coordinator.CollectionView(args.RootElement);
        var second = (LiteSpotifyItemsResult)coordinator.CollectionView(args.RootElement);

        Check(backend.RefreshCalls == 0 && backend.AlbumRefreshCalls == 1,
            "czas albumu uruchomił pełną synchronizację albo odświeżał się wielokrotnie");
        Check(first.Items.Single().DurationTicks == TimeSpan.FromMinutes(38).Ticks
            && second.Items.Single().DurationTicks == TimeSpan.FromMinutes(38).Ticks,
            "czas odświeżonego albumu nie trafił do wiersza listy");
    }

    private static void CoordinatorRefreshesPodcastsWithPopulatedOtherCacheExactlyOnce()
    {
        using var backend = new FakeBackend
        {
            Cached =
            [
                Item(MediaItemKind.Track, "track123", "Utwór", favorite: true),
                Item(MediaItemKind.Podcast, "old-show", "Nieaktualny podcast", library: true)
            ],
            RefreshedPodcasts =
            [
                Item(MediaItemKind.Podcast, "show123", "Podcast z konta", library: true),
                Item(MediaItemKind.Episode, "episode123", "Zapisany odcinek", favorite: true)
            ]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var podcasts = JsonDocument.Parse(
            """{"view":"podcasts","loadedCount":150}""");
        using var episodes = JsonDocument.Parse(
            """{"view":"episodes","loadedCount":150}""");

        var first = (LiteSpotifyItemsResult)coordinator.CollectionView(
            podcasts.RootElement);
        var second = (LiteSpotifyItemsResult)coordinator.CollectionView(
            episodes.RootElement);
        var reopened = (LiteSpotifyItemsResult)coordinator.CollectionView(
            podcasts.RootElement);

        Check(backend.PodcastRefreshCalls == 1 && backend.RefreshCalls == 0,
            "podcasty odświeżyły się wielokrotnie albo uruchomiły pełną synchronizację");
        Check(first.Items.Single().Title == "Podcast z konta"
              && second.Items.Single().Title == "Zapisany odcinek"
              && reopened.Items.Single().Title == "Podcast z konta",
            "wąskie odświeżenie nie zastąpiło starego podcastu albo nie objęło odcinków");
    }

    private static void CoordinatorKeepsPodcastCacheWhenOnlyPartOfRefreshSucceeds()
    {
        using var backend = new FakeBackend
        {
            Cached =
            [
                Item(MediaItemKind.Podcast, "oldshow123", "Podcast z zapisu", library: true),
                Item(MediaItemKind.Episode, "oldepisode123", "Odcinek z zapisu", favorite: true)
            ],
            RefreshedPodcasts =
            [
                Item(MediaItemKind.Episode, "newepisode123", "Nowy odcinek", favorite: true)
            ],
            PodcastRefreshWarnings = true
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var podcasts = JsonDocument.Parse("""{"view":"podcasts"}""");

        var result = (LiteSpotifyItemsResult)coordinator.CollectionView(
            podcasts.RootElement);

        Check(result.Items.Single().Title == "Podcast z zapisu",
            "częściowe odświeżenie usunęło ostatni poprawny zapis podcastów");
        Check(result.Warning.Contains("Nie wszystkie podcasty", StringComparison.Ordinal)
              && result.Warning.Contains("ostatni zapis", StringComparison.Ordinal),
            "częściowy wynik nie ma uczciwego komunikatu dla użytkownika");
    }

    private static void CoordinatorClearsStalePodcastsAfterSuccessfulEmptyRefresh()
    {
        using var backend = new FakeBackend
        {
            Cached =
            [
                Item(MediaItemKind.Track, "track123", "Utwór", favorite: true),
                Item(MediaItemKind.Podcast, "oldshow123", "Stary podcast", library: true),
                Item(MediaItemKind.Episode, "oldepisode123", "Stary odcinek", favorite: true)
            ],
            RefreshedPodcasts = []
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var podcasts = JsonDocument.Parse("""{"view":"podcasts"}""");
        using var episodes = JsonDocument.Parse("""{"view":"episodes"}""");

        var shows = (LiteSpotifyItemsResult)coordinator.CollectionView(
            podcasts.RootElement);
        var savedEpisodes = (LiteSpotifyItemsResult)coordinator.CollectionView(
            episodes.RootElement);

        Check(shows.Items.Count == 0 && savedEpisodes.Items.Count == 0,
            "pełny pusty wynik pozostawił nieaktualny podcast albo odcinek");
        Check(backend.PodcastRefreshCalls == 1
              && string.IsNullOrEmpty(shows.Warning),
            "pełny pusty wynik został błędnie uznany za awarię lub powtórzony");
    }

    private static void CoordinatorLoginInvalidatesPreviousPodcastSnapshot()
    {
        using var backend = new WritableFakeBackend
        {
            RefreshedPodcasts =
            [
                Item(MediaItemKind.Podcast, "oldshow123", "Podcast sprzed logowania", library: true)
            ]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var view = JsonDocument.Parse("""{"view":"podcasts"}""");
        var before = (LiteSpotifyItemsResult)coordinator.CollectionView(view.RootElement);

        backend.RefreshedPodcasts =
        [
            Item(MediaItemKind.Podcast, "newshow123", "Podcast po logowaniu", library: true)
        ];
        _ = coordinator.Login();
        var after = (LiteSpotifyItemsResult)coordinator.CollectionView(view.RootElement);

        Check(backend.LoginCalls == 1 && backend.PodcastRefreshCalls == 2,
            "ponowne logowanie nie wymusiło świeżego odczytu podcastów");
        Check(before.Items.Single().Title == "Podcast sprzed logowania"
              && after.Items.Single().Title == "Podcast po logowaniu",
            "widok podcastów zachował migawkę sprzed logowania");
    }

    private static void CoordinatorRedactsBackendFailures()
    {
        const string secret = "Bearer tajny-token spotify:track:sekret123";
        using var backend = new FakeBackend
        {
            SearchFailure = new InvalidOperationException(secret),
            RefreshFailure = new InvalidOperationException(secret)
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var searchArgs = JsonDocument.Parse("""{"query":"muzyka"}""");
        try
        {
            coordinator.Search(searchArgs.RootElement);
            throw new InvalidOperationException("awaria backendu nie została zgłoszona");
        }
        catch (LiteRequestException exception)
        {
            Check(!exception.Message.Contains(secret, StringComparison.Ordinal)
                && exception.Message == "Nie udało się wyszukać w Spotify.",
                "surowy błąd backendu wyciekł do protokołu");
        }

        using var viewArgs = JsonDocument.Parse("""{"view":"liked"}""");
        var view = (LiteSpotifyItemsResult)coordinator.CollectionView(viewArgs.RootElement);
        Check(!view.Warning.Contains(secret, StringComparison.Ordinal)
            && view.Warning.Contains("Pokazano ostatni zapis", StringComparison.Ordinal),
            "surowy błąd odświeżenia wyciekł do ostrzeżenia");
    }

    private static void CoordinatorSearchesExactlyOnceWithoutMutatingAnything()
    {
        using var backend = new FakeBackend
        {
            SearchItems = [Item(MediaItemKind.Artist, "artist123", "Wykonawca")]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var args = JsonDocument.Parse("""{"query":" Wykonawca ","add":true}""");
        var result = (LiteSpotifyItemsResult)coordinator.Search(args.RootElement);
        Check(backend.SearchCalls == 1 && backend.LastQuery == "Wykonawca",
            "wyszukiwanie nie wykonało dokładnie jednego oczyszczonego odczytu");
        Check(result.Items.Single().Kind == "artist" && backend.MutationCalls == 0,
            "wynik zmienił rodzaj albo wyszukiwanie wykonało mutację");
    }

    private static void CoordinatorReadsOneDescriptionWithoutMutation()
    {
        using var backend = new FakeBackend { Description = "Opis próby" };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var args = JsonDocument.Parse(
            """{"externalId":"show123","kind":"podcast"}""");
        var result = (LiteSpotifyDescriptionResult)coordinator.Description(
            args.RootElement);
        Check(result.Description == "Opis próby"
            && backend.DescriptionCalls == 1
            && backend.MutationCalls == 0,
            "Alt+D nie wykonało dokładnie jednego bezpiecznego odczytu opisu");
    }

    private static void CoordinatorReadsEpisodeChaptersFromDescriptionWithoutMutation()
    {
        using var backend = new FakeBackend
        {
            Description = "00:00 Wstęp\n05:30 Temat główny"
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        var episode = Item(MediaItemKind.Episode, "episode123", "Odcinek");

        var chapters = coordinator.ReadEpisodeChapterPoints(
            episode,
            TimeSpan.FromMinutes(20));

        Check(chapters.Count == 2
            && chapters[0].Name == "Wstęp"
            && chapters[1].Start == TimeSpan.FromMinutes(5.5)
            && backend.DescriptionCalls == 1
            && backend.MutationCalls == 0,
            "rozdziały odcinka Spotify nie powstały z jednego bezpiecznego odczytu opisu");
    }

    private static void CoordinatorShowsConfirmedCreatedPlaylistInCollectionImmediately()
    {
        using var backend = new WritableFakeBackend();
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var create = JsonDocument.Parse(
            """{"name":"Nowa playlista","isPublic":true}""");
        using var view = JsonDocument.Parse(
            """{"view":"playlists","loadedCount":150}""");

        _ = coordinator.CreatePlaylist(create.RootElement);
        var result = (LiteSpotifyItemsResult)coordinator.CollectionView(
            view.RootElement);

        Check(backend.CreateCalls == 1 && backend.RefreshCalls == 0,
            "utworzenie playlisty nie wykonało jednej mutacji albo wymusiło pełne odświeżenie");
        Check(result.Items.Count == 1
            && result.Items[0].Title == "Nowa playlista"
            && result.Items[0].Kind == "playlist"
            && result.Items[0].ExternalId == "created123",
            "potwierdzona playlista nie pojawiła się od razu pod Ctrl+P");
        Check(!result.Items[0].AccessibleLabel.Contains(
                "created123", StringComparison.Ordinal),
            "techniczny identyfikator nowej playlisty wszedł do etykiety dostępnościowej");
    }

    private static void CoordinatorRefreshesPlaylistViewOnceWithoutFullCollectionSync()
    {
        using var backend = new WritableFakeBackend
        {
            PlaylistTargets =
            [
                new SpotifyPlaylistTarget(
                    "existing123", "Playlista z konta", "owner", "Michał",
                    false, false, true, "snapshot-existing")
            ]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var view = JsonDocument.Parse(
            """{"view":"playlists","loadedCount":150}""");

        var first = (LiteSpotifyItemsResult)coordinator.CollectionView(
            view.RootElement);
        var second = (LiteSpotifyItemsResult)coordinator.CollectionView(
            view.RootElement);

        Check(backend.PlaylistTargetCalls == 1 && backend.RefreshCalls == 0,
            "Ctrl+P pobrał playlisty wielokrotnie albo zsynchronizował całą kolekcję");
        Check(first.Items.Single().Title == "Playlista z konta"
            && second.Items.Single().Title == "Playlista z konta",
            "playlista utworzona wcześniej na koncie nie pojawiła się pod Ctrl+P");
    }

    private static void CoordinatorLoadsCollectionAfterOpeningPlaylistsFirst()
    {
        using var backend = new WritableFakeBackend
        {
            PlaylistTargets =
            [
                new SpotifyPlaylistTarget(
                    "playlist123", "Playlista", "owner", "Michał",
                    false, false, true, "snapshot")
            ],
            RefreshedItems =
            [
                Item(MediaItemKind.Album, "album123", "Album z konta", library: true)
            ]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var playlists = JsonDocument.Parse("""{"view":"playlists","loadedCount":150}""");
        using var albums = JsonDocument.Parse("""{"view":"albums","loadedCount":150}""");

        var playlistView = (LiteSpotifyItemsResult)coordinator.CollectionView(playlists.RootElement);
        var albumView = (LiteSpotifyItemsResult)coordinator.CollectionView(albums.RootElement);
        _ = coordinator.CollectionView(albums.RootElement);

        Check(playlistView.Items.Count == 1
            && backend.PlaylistTargetCalls == 1
            && backend.RefreshCalls == 1
            && albumView.Items.Count == 1
            && albumView.Items[0].Title == "Album z konta",
            $"Otwarcie playlist jako pierwszych zablokowało pobranie albumów albo powtórzyło synchronizację: playlisty {playlistView.Items.Count}, albumy {albumView.Items.Count}, odświeżenia {backend.RefreshCalls}.");
    }

    private static void CoordinatorLoginInvalidatesPreviousPlaylistSnapshot()
    {
        using var backend = new WritableFakeBackend
        {
            PlaylistTargets =
            [
                new SpotifyPlaylistTarget(
                    "old123", "Playlista sprzed logowania", "owner", "Michał",
                    false, false, true, "snapshot-old")
            ]
        };
        using var coordinator = new LiteSpotifyCatalogCoordinator(backend);
        using var view = JsonDocument.Parse(
            """{"view":"playlists","loadedCount":150}""");
        var before = (LiteSpotifyItemsResult)coordinator.CollectionView(view.RootElement);

        backend.PlaylistTargets =
        [
            new SpotifyPlaylistTarget(
                "new123", "Playlista po logowaniu", "owner", "Michał",
                false, false, true, "snapshot-new")
        ];
        _ = coordinator.Login();
        var after = (LiteSpotifyItemsResult)coordinator.CollectionView(view.RootElement);

        Check(backend.LoginCalls == 1 && backend.PlaylistTargetCalls == 2,
            "Ponowne logowanie nie wymusiło jednego świeżego odczytu playlist.");
        Check(before.Items.Single().Title == "Playlista sprzed logowania"
              && after.Items.Count == 1
              && after.Items[0].Title == "Playlista po logowaniu",
            "Ctrl+P zachował playlistę z migawki sprzed logowania.");
    }

    private static MediaItem Item(
        MediaItemKind kind,
        string externalId,
        string title,
        string artist = "",
        bool favorite = false,
        bool library = false,
        bool available = true) => new()
    {
        Kind = kind,
        ExternalId = externalId,
        Title = title,
        Artist = artist,
        IsFavorite = favorite,
        IsInLibrary = library,
        IsAvailable = available
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeBackend : ILiteSpotifyReadOnlyBackend
    {
        public LiteSpotifyAccountProbe Account { get; set; } = new(
            true, true, "Konto", "premium", false);
        public IReadOnlyList<MediaItem> Cached { get; set; } = [];
        public IReadOnlyList<MediaItem> SearchItems { get; set; } = [];
        public IReadOnlyList<MediaItem> RefreshedItems { get; set; } = [];
        public IReadOnlyList<MediaItem>? RefreshedAlbums { get; set; }
        public IReadOnlyList<MediaItem> RefreshedPodcasts { get; set; } = [];
        public bool PodcastRefreshWarnings { get; set; }
        public IReadOnlyList<MediaItem> ContainerItems { get; set; } = [];
        public string? Description { get; set; }
        public Exception? SearchFailure { get; set; }
        public Exception? RefreshFailure { get; set; }
        public Exception? AlbumRefreshFailure { get; set; }
        public Exception? ContainerFailure { get; set; }
        public Exception? AccountFailure { get; set; }
        public int RefreshCalls { get; private set; }
        public int AlbumRefreshCalls { get; private set; }
        public int PodcastRefreshCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public int ContainerCalls { get; private set; }
        public int DescriptionCalls { get; private set; }
        public int MutationCalls => 0;
        public string LastQuery { get; private set; } = string.Empty;

        public IReadOnlyList<MediaItem> CachedCollection => Cached;

        public LiteSpotifyAccountProbe ReadAccountStatus()
        {
            if (AccountFailure is not null) throw AccountFailure;
            return Account;
        }

        public Task<LiteSpotifyCollectionRefresh> RefreshCollectionAsync(
            CancellationToken cancellationToken)
        {
            RefreshCalls++;
            if (RefreshFailure is not null) throw RefreshFailure;
            return Task.FromResult(new LiteSpotifyCollectionRefresh(RefreshedItems, false));
        }

        public Task<LiteSpotifyCollectionRefresh> RefreshAlbumsAsync(
            CancellationToken cancellationToken)
        {
            AlbumRefreshCalls++;
            if (AlbumRefreshFailure is not null) throw AlbumRefreshFailure;
            return Task.FromResult(new LiteSpotifyCollectionRefresh(
                RefreshedAlbums ?? Cached.Where(item =>
                    item.Kind == MediaItemKind.Album).ToArray(),
                false));
        }

        public Task<LiteSpotifyCollectionRefresh> RefreshPodcastsAsync(
            CancellationToken cancellationToken)
        {
            PodcastRefreshCalls++;
            return Task.FromResult(new LiteSpotifyCollectionRefresh(
                RefreshedPodcasts,
                PodcastRefreshWarnings));
        }

        public Task<IReadOnlyList<MediaItem>> SearchAsync(
            string query,
            CancellationToken cancellationToken)
        {
            SearchCalls++;
            LastQuery = query;
            if (SearchFailure is not null) throw SearchFailure;
            return Task.FromResult(SearchItems);
        }

        public Task<string?> GetDescriptionAsync(
            LiteSpotifyDescriptionRequest request,
            CancellationToken cancellationToken)
        {
            DescriptionCalls++;
            return Task.FromResult(Description);
        }

        public Task<LiteSpotifyContainerBackendResult> GetContainerItemsAsync(
            LiteSpotifyContainerRequest request,
            CancellationToken cancellationToken)
        {
            ContainerCalls++;
            if (ContainerFailure is not null) throw ContainerFailure;
            return Task.FromResult(
                new LiteSpotifyContainerBackendResult(ContainerItems, true));
        }

        public void Dispose()
        {
        }
    }

    private sealed class WritableFakeBackend : ILiteSpotifyWriteBackend, ILiteSpotifyAccountBackend
    {
        public int CreateCalls { get; private set; }
        public int RefreshCalls { get; private set; }
        public int PlaylistTargetCalls { get; private set; }
        public int LoginCalls { get; private set; }
        public int PodcastRefreshCalls { get; private set; }
        public string ClientId { get; private set; } = string.Empty;
        public bool PairingCancelled { get; private set; }
        public IReadOnlyList<SpotifyPlaylistTarget> PlaylistTargets { get; set; } = [];
        public IReadOnlyList<MediaItem> RefreshedItems { get; set; } = [];
        public IReadOnlyList<MediaItem> RefreshedPodcasts { get; set; } = [];

        public IReadOnlyList<MediaItem> CachedCollection => [];

        public LiteSpotifyAccountProbe ReadAccountStatus() => new(
            true, true, "Konto", "premium", true);

        public Task<LiteSpotifyCollectionRefresh> RefreshCollectionAsync(
            CancellationToken cancellationToken)
        {
            RefreshCalls++;
            return Task.FromResult(new LiteSpotifyCollectionRefresh(RefreshedItems, false));
        }

        public Task<LiteSpotifyCollectionRefresh> RefreshAlbumsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new LiteSpotifyCollectionRefresh(
                RefreshedItems.Where(item => item.Kind == MediaItemKind.Album).ToArray(),
                false));

        public Task<LiteSpotifyCollectionRefresh> RefreshPodcastsAsync(
            CancellationToken cancellationToken)
        {
            PodcastRefreshCalls++;
            return Task.FromResult(new LiteSpotifyCollectionRefresh(
                RefreshedPodcasts,
                false));
        }

        public Task<IReadOnlyList<MediaItem>> SearchAsync(
            string query,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MediaItem>>([]);

        public Task<string?> GetDescriptionAsync(
            LiteSpotifyDescriptionRequest request,
            CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<LiteSpotifyContainerBackendResult> GetContainerItemsAsync(
            LiteSpotifyContainerRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LiteSpotifyContainerBackendResult([], true));

        public Task<LiteSpotifyMembershipChange> ChangeMembershipAsync(
            IReadOnlyList<MediaItem> items,
            bool? requestedAddition,
            CancellationToken cancellationToken) =>
            Task.FromResult(new LiteSpotifyMembershipChange(
                requestedAddition ?? true, [], 0));

        public Task<IReadOnlyList<SpotifyPlaylistTarget>> GetPlaylistTargetsAsync(
            CancellationToken cancellationToken)
        {
            PlaylistTargetCalls++;
            return Task.FromResult(PlaylistTargets);
        }

        public Task<SpotifyPlaylistTarget> CreatePlaylistAsync(
            string name,
            bool isPublic,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            return Task.FromResult(new SpotifyPlaylistTarget(
                "created123", name, "owner", "Michał", isPublic,
                false, true, "snapshot123"));
        }

        public Task<SpotifyPlaylistWriteResult> AddPlaylistItemsAsync(
            IReadOnlyList<string> playlistIds,
            IReadOnlyList<MediaItem> items,
            CancellationToken cancellationToken) =>
            Task.FromResult(new SpotifyPlaylistWriteResult(items.Count, []));

        public Task<LiteSpotifyLoginOutcome> LoginAsync(
            CancellationToken cancellationToken)
        {
            LoginCalls++;
            return Task.FromResult(new LiteSpotifyLoginOutcome("Michał", "premium"));
        }

        public void ConfigureClientId(string clientId) => ClientId = clientId;

        public Task<LiteSpotifyPairingRequest> StartPairingAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(new LiteSpotifyPairingRequest(
                "ABCD-EFGH", "https://www.spotify.com/pair"));

        public LiteSpotifyPairingStatusResult PairingStatus() => PairingCancelled
            ? new("cancelled", "Anulowano")
            : new("pending", "Czekam");

        public void CancelPairing() => PairingCancelled = true;

        public void Dispose()
        {
        }
    }
}
