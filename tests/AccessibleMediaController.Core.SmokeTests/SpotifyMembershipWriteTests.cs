using System.Net;
using System.Net.Http;
using System.Text.Json;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;

/// <summary>
/// Zapis do biblioteki Spotify na PRAWDZIWYM API, nie na lokalnych flagach.
/// Atrapa HTTP jest jedynym sposobem sprawdzenia tego bez dotykania konta
/// Michała - żaden test tutaj nie wysyła niczego do Spotify.
/// </summary>
internal static class SpotifyMembershipWriteTests
{
    public static void Run()
    {
        TestSemantykaRodzajow();
        TestAdresUri();
        TestZakresyZapisu();
        TestDodanieZPotwierdzeniem();
        TestUsuniecieZPotwierdzeniem();
        TestOdwracanieStanuCzytaAPI();
        TestBrakZakresuNieWysylaZapytania();
        TestOdmowaTrzySeteNieZmieniaStanu();
        TestBrakPotwierdzeniaToNieSukces();
        TestCzesciowaAwariaZachowujePotwierdzone();
        TestAwariaTransportuNieGubiCzesciowegoWyniku();
        TestAwariaPotwierdzeniaNieGubiCzesciowegoWyniku();
        TestPlaylistaPozaZakresem();
        TestSerializacjaStanuPoZapisie();
    }

    private static void TestSemantykaRodzajow()
    {
        // Utwór i odcinek to Ulubione; album, wykonawca, playlista, podcast to Biblioteka.
        Assert(SpotifyCollectionSemantics.UsesFavorites(MediaItemKind.Track), "utwór to Ulubione");
        Assert(SpotifyCollectionSemantics.UsesFavorites(MediaItemKind.Episode), "odcinek to Ulubione");
        Assert(SpotifyCollectionSemantics.UsesLibrary(MediaItemKind.Album), "album to Biblioteka");
        Assert(SpotifyCollectionSemantics.UsesLibrary(MediaItemKind.Artist), "wykonawca to Biblioteka");
        Assert(SpotifyCollectionSemantics.UsesLibrary(MediaItemKind.Podcast), "podcast to Biblioteka");
        Assert(!SpotifyCollectionSemantics.IsCollectionKind(MediaItemKind.Station), "stacja radiowa poza kolekcją");

        var utwor = new MediaItem { Kind = MediaItemKind.Track, ExternalId = "abc" };
        SpotifyCollectionSemantics.ApplyMembership(utwor, true);
        Assert(utwor.IsFavorite && !utwor.IsInLibrary, "utwór dostaje tylko flagę Ulubionych");
        var album = new MediaItem { Kind = MediaItemKind.Album, ExternalId = "def" };
        SpotifyCollectionSemantics.ApplyMembership(album, true);
        Assert(album.IsInLibrary && !album.IsFavorite, "album dostaje tylko flagę Biblioteki");
        SpotifyCollectionSemantics.ApplyMembership(album, false);
        Assert(!album.IsInLibrary, "usunięcie czyści flagę Biblioteki");
    }

    private static void TestAdresUri()
    {
        Assert(
            SpotifyCollectionSemantics.TryBuildUri(
                new MediaItem { Kind = MediaItemKind.Track, ExternalId = "7ouMYWpwJ422jRcDASZB7P" },
                out var utwor) && utwor == "spotify:track:7ouMYWpwJ422jRcDASZB7P",
            "adres utworu");
        Assert(
            SpotifyCollectionSemantics.TryBuildUri(
                new MediaItem { Kind = MediaItemKind.Podcast, ExternalId = "5CfCWKI5pZ29U0uOzXkDHe" },
                out var podcast) && podcast == "spotify:show:5CfCWKI5pZ29U0uOzXkDHe",
            "podcast idzie jako show");
        // Pełny adres w polu nie może dostać drugiego prefiksu.
        Assert(
            SpotifyCollectionSemantics.TryBuildUri(
                new MediaItem { Kind = MediaItemKind.Album, ExternalId = "spotify:album:xyz" },
                out var gotowy) && gotowy == "spotify:album:xyz",
            "gotowy adres bez zdublowanego prefiksu");
        Assert(
            !SpotifyCollectionSemantics.TryBuildUri(
                new MediaItem { Kind = MediaItemKind.Track, ExternalId = null },
                out _),
            "brak identyfikatora to brak adresu");
        Assert(
            !SpotifyCollectionSemantics.TryBuildUri(
                new MediaItem { Kind = MediaItemKind.Station, ExternalId = "abc" },
                out _),
            "stacja radiowa nie ma adresu biblioteki Spotify");
        Assert(
            !SpotifyCollectionSemantics.TryBuildUri(
                new MediaItem { Kind = MediaItemKind.Track, ExternalId = @"D:\muzyka\plik.mp3" },
                out _),
            "ścieżka pliku lokalnego nie jest identyfikatorem Spotify");
    }

    private static void TestZakresyZapisu()
    {
        Assert(
            SpotifyScopes.Requested.Contains("user-library-modify", StringComparison.Ordinal),
            "zakres zapisu biblioteki w prośbie o logowanie");
        Assert(
            SpotifyScopes.Requested.Contains("user-follow-modify", StringComparison.Ordinal),
            "zakres zapisu obserwowanych w prośbie o logowanie");
        // Playlisty poza zakresem tej zmiany: nie prosimy o zgodę bez funkcji.
        Assert(
            !SpotifyScopes.Requested.Contains("playlist-modify", StringComparison.Ordinal),
            "brak zakresów zmiany playlist");
        Assert(
            SpotifyScopes.WriteScopeForUriType("track") == "user-library-modify",
            "utwór wymaga user-library-modify");
        Assert(
            SpotifyScopes.WriteScopeForUriType("artist") == "user-follow-modify",
            "wykonawca wymaga user-follow-modify");
        Assert(
            SpotifyScopes.WriteScopeForUriType("playlist") is null,
            "playlista nie ma jeszcze zakresu zapisu w AMC");
        var stary = "streaming user-library-read user-follow-read";
        Assert(
            SpotifyScopes.MissingMembershipScopes(stary).Count == 2,
            "stare logowanie nie ma żadnego zakresu zapisu");
        Assert(
            SpotifyScopes.MissingMembershipScopes(SpotifyScopes.Requested).Count == 0,
            "pełne logowanie nie wymaga ponowienia");
    }

    private static void TestDodanieZPotwierdzeniem()
    {
        var atrapa = new SpotifyLibraryHttpStub();
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wynik = client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            ["spotify:track:aaa"],
            requestedAddition: null,
            CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.Added, "pozycja nienależąca do biblioteki zostaje dodana");
        Assert(wynik.ConfirmedUris.Count == 1, "potwierdzona jedna pozycja");
        Assert(wynik.FailedUris.Count == 0, "brak nieudanych");
        Assert(atrapa.Writes.Count == 1 && atrapa.Writes[0].Method == HttpMethod.Put, "wysłano PUT /me/library");
        Assert(
            atrapa.Writes[0].Url.Contains("/me/library?uris=", StringComparison.Ordinal),
            "użyto aktualnego punktu /me/library");
        Assert(
            atrapa.Reads.Count >= 2,
            "stan czytany przed zapisem i potwierdzany po zapisie");
    }

    private static void TestUsuniecieZPotwierdzeniem()
    {
        var atrapa = new SpotifyLibraryHttpStub();
        atrapa.Membership["spotify:album:bbb"] = true;
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wynik = client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            ["spotify:album:bbb"],
            requestedAddition: false,
            CancellationToken.None).GetAwaiter().GetResult();
        Assert(!wynik.Added, "jawne usunięcie");
        Assert(atrapa.Writes[0].Method == HttpMethod.Delete, "wysłano DELETE /me/library");
        Assert(!atrapa.Membership["spotify:album:bbb"], "konto nie ma już pozycji");
    }

    private static void TestOdwracanieStanuCzytaAPI()
    {
        // Lokalna flaga mówi "nie należy", ale API wie, że należy. Jedno
        // naciśnięcie skrótu ma USUNĄĆ, nie dodać po raz drugi.
        var atrapa = new SpotifyLibraryHttpStub();
        atrapa.Membership["spotify:track:ccc"] = true;
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wynik = client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            ["spotify:track:ccc"],
            requestedAddition: null,
            CancellationToken.None).GetAwaiter().GetResult();
        Assert(!wynik.Added, "stan odwrócony wobec stanu z API, nie wobec flagi lokalnej");
    }

    private static void TestBrakZakresuNieWysylaZapytania()
    {
        var atrapa = new SpotifyLibraryHttpStub();
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wyjatek = Throws(() => client.ChangeMembershipAsync(
            "token",
            "streaming user-library-read",
            ["spotify:track:ddd"],
            requestedAddition: true,
            CancellationToken.None).GetAwaiter().GetResult());
        Assert(wyjatek is SpotifyWriteBlockedException, "brak zakresu blokuje zapis");
        Assert(
            ((SpotifyWriteBlockedException)wyjatek!).Reason == SpotifyWriteBlockReason.MissingScope,
            "przyczyna: brak zakresu");
        Assert(
            wyjatek.Message.Contains("Ctrl+F5", StringComparison.Ordinal),
            "komunikat kieruje do okna konta, a nie otwiera przeglądarki sam");
        Assert(
            wyjatek.Message.Contains("Otwórz Ctrl+F5 i wybierz Zaloguj w przeglądarce", StringComparison.Ordinal)
                && !wyjatek.Message.Contains("Konto katalogu i biblioteka", StringComparison.Ordinal),
            "brak zgody prowadzi bezpośrednio do logowania w pierwszym oknie konta");
        Assert(
            wyjatek.Message.Contains("Parowanie odtwarzacza nie zmienia tej zgody", StringComparison.Ordinal),
            "komunikat odróżnia zgodę biblioteki od parowania odtwarzacza");
        Assert(atrapa.Writes.Count == 0 && atrapa.Reads.Count == 0, "nie poszło żadne zapytanie");
    }

    private static void TestOdmowaTrzySeteNieZmieniaStanu()
    {
        var atrapa = new SpotifyLibraryHttpStub { WriteStatus = HttpStatusCode.Forbidden };
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var utwor = new MediaItem { Kind = MediaItemKind.Track, ExternalId = "eee" };
        var wyjatek = Throws(() => client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            ["spotify:track:eee"],
            requestedAddition: true,
            CancellationToken.None).GetAwaiter().GetResult());
        Assert(
            wyjatek is SpotifyWriteBlockedException { Reason: SpotifyWriteBlockReason.Denied },
            "403 to odmowa, nie sukces");
        Assert(!utwor.IsFavorite, "flaga lokalna nietknięta po odmowie");
        Assert(!atrapa.Membership.ContainsKey("spotify:track:eee"), "konto bez zmian");
    }

    private static void TestBrakPotwierdzeniaToNieSukces()
    {
        // Spotify odpowiada 200, ale stan się nie zmienia (widziane przy
        // odebranej zgodzie po stronie konta). Bez odczytu po zapisie AMC
        // ogłaszałoby sukces i kłamało.
        var atrapa = new SpotifyLibraryHttpStub { IgnoreWrites = true };
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wyjatek = Throws(() => client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            ["spotify:track:fff"],
            requestedAddition: true,
            CancellationToken.None).GetAwaiter().GetResult());
        Assert(wyjatek is SpotifyWriteBlockedException, "brak potwierdzenia to porażka");
        Assert(
            wyjatek!.Message.Contains("nie potwierdził", StringComparison.OrdinalIgnoreCase),
            "komunikat mówi o braku potwierdzenia");
    }

    private static void TestCzesciowaAwariaZachowujePotwierdzone()
    {
        // Dwie partie po 40 adresów. Druga pada; pierwsza MUSI zostać
        // potwierdzona, a nie zniknąć razem z nieudaną.
        var uris = Enumerable.Range(0, 45).Select(i => $"spotify:track:t{i:D3}").ToArray();
        var atrapa = new SpotifyLibraryHttpStub { FailWriteAfterBatches = 1 };
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wynik = client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            uris,
            requestedAddition: true,
            CancellationToken.None).GetAwaiter().GetResult();
        Assert(wynik.ConfirmedUris.Count == 40, $"pierwsza partia potwierdzona, było {wynik.ConfirmedUris.Count}");
        Assert(wynik.FailedUris.Count == 5, $"nieudane zgłoszone osobno, było {wynik.FailedUris.Count}");
        Assert(wynik.Warnings.Count > 0, "użytkownik dostaje ostrzeżenie o częściowej awarii");
        Assert(
            atrapa.Membership.Count(para => para.Value) == 40,
            "w koncie zostaje to, co Spotify przyjął");
    }

    private static void TestAwariaTransportuNieGubiCzesciowegoWyniku()
    {
        foreach (var timeout in new[] { false, true })
        {
            var uris = Enumerable.Range(0, 45).Select(i => $"spotify:track:t{i:D3}").ToArray();
            var stub = new SpotifyLibraryHttpStub { FailWriteAfterBatches = 1,
                WriteFailure = timeout ? new TaskCanceledException("timeout") : new HttpRequestException("network") };
            using var client = new SpotifyLibraryWriteClient(new HttpClient(stub));
            var result = client.ChangeMembershipAsync("token", SpotifyScopes.Requested, uris, true, CancellationToken.None).GetAwaiter().GetResult();
            Assert(result.ConfirmedUris.Count == 40 && result.FailedUris.Count == 5, "transport nie może zgubić pierwszej potwierdzonej partii");
        }
    }

    private static void TestAwariaPotwierdzeniaNieGubiCzesciowegoWyniku()
    {
        var uris = Enumerable.Range(0, 45).Select(i => $"spotify:track:t{i:D3}").ToArray();
        var stub = new SpotifyLibraryHttpStub { FailConfirmationAfterBatches = 1 };
        using var client = new SpotifyLibraryWriteClient(new HttpClient(stub));
        var result = client.ChangeMembershipAsync("token", SpotifyScopes.Requested, uris, true, CancellationToken.None).GetAwaiter().GetResult();
        Assert(result.ConfirmedUris.Count == 40 && result.FailedUris.Count == 5, "timeout potwierdzenia nie może zgubić wcześniejszej potwierdzonej partii");
        Assert(stub.Membership.Count == 45, "test odróżnia stan konta od niepełnego potwierdzenia");
    }

    private static void TestPlaylistaPozaZakresem()
    {
        // Playlisty poza zakresem tej zmiany: brak funkcji i brak zakresu.
        // Nie wolno udawać, że się udało.
        var atrapa = new SpotifyLibraryHttpStub();
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var wyjatek = Throws(() => client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            ["spotify:playlist:ggg"],
            requestedAddition: true,
            CancellationToken.None).GetAwaiter().GetResult());
        Assert(
            wyjatek is SpotifyWriteBlockedException { Reason: SpotifyWriteBlockReason.UnsupportedKind },
            "playlista zgłoszona jako rodzaj bez obsługi zapisu");
        Assert(atrapa.Writes.Count == 0, "nie wysłano żądania dla playlisty");
    }

    private static void TestSerializacjaStanuPoZapisie()
    {
        // Potwierdzona zmiana musi przetrwać zapis i odczyt cache, inaczej
        // po restarcie AMC pokaże stary stan.
        var atrapa = new SpotifyLibraryHttpStub();
        using var client = new SpotifyLibraryWriteClient(new HttpClient(atrapa));
        var album = new MediaItem { Kind = MediaItemKind.Album, Title = "Płyta", ExternalId = "hhh" };
        Assert(SpotifyCollectionSemantics.TryBuildUri(album, out var uri), "adres albumu");
        var wynik = client.ChangeMembershipAsync(
            "token",
            SpotifyScopes.Requested,
            [uri],
            requestedAddition: true,
            CancellationToken.None).GetAwaiter().GetResult();
        var potwierdzone = wynik.ConfirmedUris.ToHashSet(StringComparer.Ordinal);
        if (potwierdzone.Contains(uri))
            SpotifyCollectionSemantics.ApplyMembership(album, wynik.Added);
        Assert(album.IsInLibrary, "flaga ustawiona tylko po potwierdzeniu");

        var json = JsonSerializer.Serialize(new
        {
            album.Title,
            album.ExternalId,
            Kind = album.Kind.ToString(),
            album.IsInLibrary,
            album.IsFavorite
        });
        using var dokument = JsonDocument.Parse(json);
        Assert(
            dokument.RootElement.GetProperty("IsInLibrary").GetBoolean(),
            "stan Biblioteki przetrwał serializację");
        Assert(
            !dokument.RootElement.GetProperty("IsFavorite").GetBoolean(),
            "album nie udaje Ulubionego");
        Assert(
            !json.Contains("token", StringComparison.OrdinalIgnoreCase),
            "żaden token nie trafia do zapisanego stanu");
    }

    private static void Assert(bool warunek, string opis)
    {
        if (!warunek) throw new InvalidOperationException($"Zapis biblioteki Spotify: {opis}");
    }

    private static Exception? Throws(Action akcja)
    {
        try
        {
            akcja();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Atrapa /me/library i /me/library/contains. Trzyma stan konta w pamięci,
    /// żeby test mógł sprawdzić SKUTEK, nie tylko wysłane żądanie.
    /// </summary>
    private sealed class SpotifyLibraryHttpStub : HttpMessageHandler
    {
        public Dictionary<string, bool> Membership { get; } = new(StringComparer.Ordinal);
        public List<(HttpMethod Method, string Url)> Writes { get; } = [];
        public List<string> Reads { get; } = [];
        public HttpStatusCode WriteStatus { get; set; } = HttpStatusCode.OK;
        /// <summary>200, ale stan konta się nie zmienia.</summary>
        public bool IgnoreWrites { get; set; }
        /// <summary>Po ilu udanych partiach zapisu zacząć odrzucać.</summary>
        public int? FailWriteAfterBatches { get; set; }
        public Exception? WriteFailure { get; set; }
        public int? FailConfirmationAfterBatches { get; set; }
        private int confirmations;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var uris = ParseUris(url);
            if (request.Method == HttpMethod.Get)
            {
                Reads.Add(url);
                if (Writes.Count > 0 && FailConfirmationAfterBatches is { } count && confirmations++ >= count)
                    throw new TaskCanceledException("confirmation timeout");
                var tablica = uris.Select(uri => Membership.GetValueOrDefault(uri) ? "true" : "false");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($"[{string.Join(',', tablica)}]")
                });
            }

            Writes.Add((request.Method, url));
            if (WriteStatus != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(WriteStatus));
            if (FailWriteAfterBatches is { } limit && Writes.Count > limit)
            {
                if (WriteFailure is not null) throw WriteFailure;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            }
            if (!IgnoreWrites)
            {
                var add = request.Method == HttpMethod.Put;
                foreach (var uri in uris) Membership[uri] = add;
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        private static string[] ParseUris(string url)
        {
            var index = url.IndexOf("uris=", StringComparison.Ordinal);
            if (index < 0) return [];
            return url[(index + 5)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.UnescapeDataString)
                .ToArray();
        }
    }
}
