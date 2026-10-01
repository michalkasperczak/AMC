using System.Net;
using System.Net.Http;
using System.Windows.Threading;
using AccessibleMediaController.Core.Sonos;

/// <summary>
/// POWTORZENIE ULUBIONEGO przez PRESET, mierzone na CALEJ drodze: prawdziwe
/// <c>MainWindow</c> -&gt; produkcyjny backend sesji -&gt; prawdziwy
/// <c>SonosAccountOwner</c>/<c>SonosAccountCoordinator</c> -&gt; prawdziwy
/// <c>SonosControlApiClient</c> -&gt; syntetyczny handler HTTP.
///
/// Rozbudowujemy ISTNIEJACA aparature <c>RealHarness</c> o trzy trasy fixture
/// (katalog ulubionych z <c>resource.id</c>, <c>playbackMetadata</c> z
/// <c>container.id</c>, przestawiany stan odtwarzania) - zadnej drugiej
/// aparatury i zadnego drugiego klienta HTTP.
///
/// Zadnego prawdziwego konta, sieci, Sonosa ani audio. ID i accountId sa
/// SYNTETYCZNE; to pomiar DECYZJI (ile POST, jaki POST, co powiedziano), a nie
/// obietnica, ze kazda usluga Sonosa zwraca pelna trojke.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    /// <summary>
    /// ZESTAW POWTORZENIA ULUBIONEGO. Osobne wejscie, zeby maly krok nie wymagal
    /// przebiegu calego pionu presetow.
    /// </summary>
    internal static void RunFavoriteRepeat()
    {
        List<Verdict> verdicts = [];
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { verdicts = RunAllFavoriteRepeat(); }
            catch (Exception exception) { failure = exception; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(240)))
            throw new Exception("Limit czasu pomiaru powtórzenia ulubionego.");
        if (failure is not null) throw failure;

        foreach (var verdict in verdicts)
        {
            Console.WriteLine(
                (verdict.Passed ? "REAL-POTWIERDZONY " : "REAL-ODRZUCONY ")
                + verdict.Case + ": " + verdict.Hypothesis + " -> " + verdict.Detail);
        }

        var rejected = verdicts.Where(verdict => !verdict.Passed).ToArray();
        Console.WriteLine($"OK: powtórzenie ulubionego przez realną drogę ({verdicts.Count} sprawdzeń)");
        if (rejected.Length != 0)
        {
            throw new Exception(
                "Powtórzenie ulubionego: ODRZUCONO " + rejected.Length + " z " + verdicts.Count + ": "
                + string.Join(" || ", rejected.Select(verdict => verdict.Case + " " + verdict.Detail)));
        }
    }

    private static List<Verdict> RunAllFavoriteRepeat() =>
    [
        Measure("F1", "Ulubiony JUZ GRA (currentItem to INNY utwor, niezerowy postep): "
            + "ZERO POST i SAMA nazwa", () => MeasureFavoriteRepeat("gra")),
        Measure("F2", "SPAUZOWANY ten sam kontener: DOKLADNIE jeden Play, "
            + "zadnego load/seek", () => MeasureFavoriteRepeat("pauza")),
        Measure("F3", "RADIO IDLE z zaladowanym zgodnym kontenerem: DOKLADNIE jeden Play",
            () => MeasureFavoriteRepeat("radio-idle")),
        Measure("F4", "INNE KONTO USLUGI w trojce mimo zgodnych serviceId/objectId: "
            + "ZWYKLY load, nie wznowienie", () => MeasureFavoriteRepeat("inne-konto")),
        Measure("F5", "POTWIERDZONY pusty glosnik (brak kontenera i pozycji, IDLE): zwykly load",
            () => MeasureFavoriteRepeat("pusty-glosnik")),
        Measure("F6", "ZMIANA tozsamosci MIEDZY odczytami przed wznowieniem: "
            + "ZERO POST, uczciwa odmowa", () => MeasureFavoriteRepeat("zmiana-w-trakcie")),
        Measure("F7", "BLAD GET katalogu: ZERO POST, zadnego load w ciemno",
            () => MeasureFavoriteRepeat("katalog-blad")),
        Measure("F8", "NIEPELNA trojka ulubionego przy istniejacym materiale: ZERO POST",
            () => MeasureFavoriteRepeat("niepelna-tozsamosc")),
        Measure("F9", "ZMIANA KONTA podczas wstrzymanego GET katalogu: ZERO POST "
            + "i zaden stary komunikat w nowej sesji", MeasureFavoriteRepeatAccountSwapDuringRead),
        Measure("F10", "DOMYSLNA trasa zestawu (znany INNY material): zwykly load",
            MeasureDefaultRouteStillLoads),
    ];

    /// <summary>
    /// WSPOLNA aparatura zestawu bez zadnych nadpisan tras: grupa gra ZNANY INNY
    /// material, wiec preset ulubionego ma normalnie zaladowac. To pilnuje, zeby
    /// nowa bramka nie wywrocila POZOSTALYCH pomiarow pionu.
    /// </summary>
    private static string MeasureDefaultRouteStillLoads()
    {
        using var h = RealHarness.Create();
        h.Enter();
        var before = h.Handler.Posts.Count;
        FirePreset(h, FavoritePreset());
        var posts = h.Handler.Posts.Skip(before).ToArray();
        var paths = string.Join(" | ", posts.Select(post => post.Uri.AbsolutePath));
        if (posts.Length != 1 || !posts[0].Uri.AbsolutePath.EndsWith("/favorites", StringComparison.Ordinal))
        {
            throw new Exception("Domyślna trasa: oczekiwano JEDNEGO load, jest " + posts.Length
                + " [" + paths + "], mowa=" + LastAnnouncement(h));
        }

        return "domyślna trasa: 1 load [" + paths + "], mowa=" + LastAnnouncement(h);
    }

    private const string FavoriteServiceId = "38";
    private const string FavoriteObjectId = "OBIEKT-DRUGI";
    private const string FavoriteAccountId = "KONTO-SYNTETYCZNE-9";

    /// <summary>
    /// KATALOG z tozsamoscia MATERIALU. Dwie jednakowe nazwy, rozne
    /// <c>resource.id</c> - zgodnosc nie moze wyjsc z nazwy.
    /// </summary>
    private static string FavoritesWithResourceBody(bool completeIdentity) =>
        "{\"version\":\"W2\",\"items\":["
        + "{\"id\":\"ULU-PIERWSZY\",\"name\":\"Nokturny\",\"resource\":{\"id\":{"
        + "\"serviceId\":\"" + FavoriteServiceId + "\",\"objectId\":\"OBIEKT-PIERWSZY\","
        + "\"accountId\":\"" + FavoriteAccountId + "\"}}},"
        + "{\"id\":\"ULU-DRUGI\",\"name\":\"Nokturny\",\"resource\":{\"id\":{"
        + (completeIdentity
            ? "\"serviceId\":\"" + FavoriteServiceId + "\",\"objectId\":\"" + FavoriteObjectId
                + "\",\"accountId\":\"" + FavoriteAccountId + "\""
            // NIEPELNA trojka jest LEGALNA w odpowiedzi i NIE wystarcza do rozpoznania.
            : "\"objectId\":\"" + FavoriteObjectId + "\"")
        + "}}}]}";

    /// <summary>
    /// METADANE grupy. <c>currentItem</c> jest INNYM utworem niz pierwszy -
    /// album na dalszym utworze to NADAL ten sam kontener, wiec rozpoznanie nie
    /// ma prawa opierac sie na <c>currentItem.track</c>.
    /// </summary>
    private static string MetadataBody(string? objectId, string? accountId) =>
        objectId is null
            // POTWIERDZONY pusty gloshnik: bez kontenera i bez pozycji.
            ? "{}"
            : "{\"container\":{\"name\":\"Nokturny\",\"type\":\"album\",\"id\":{"
                + "\"serviceId\":\"" + FavoriteServiceId + "\",\"objectId\":\"" + objectId + "\""
                + (accountId is null ? string.Empty : ",\"accountId\":\"" + accountId + "\"")
                + "}},\"currentItem\":{\"id\":\"POZYCJA-7\",\"track\":{\"type\":\"track\","
                + "\"name\":\"Utwór piąty\"}}}";

    private static string PlaybackBody(string state) =>
        "{\"playbackState\":\"" + state + "\",\"itemId\":\"POZYCJA-7\",\"positionMillis\":93000}";

    /// <summary>
    /// JEDEN pomiar z przestawialnym trybem fixture. Tryby odpowiadaja dokladnie
    /// gataunkom ryzyka, nie wariantom tekstu.
    /// </summary>
    private static string MeasureFavoriteRepeat(string mode)
    {
        using var h = RealHarness.Create();
        h.Enter();

        var catalogueReads = 0;
        var metadataReads = 0;
        var playbackReads = 0;
        h.Handler.RouteOverride = (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/favorites", StringComparison.Ordinal))
            {
                catalogueReads++;
                if (mode == "katalog-blad") return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                return Json(FavoritesWithResourceBody(mode != "niepelna-tozsamosc"));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/playbackMetadata", StringComparison.Ordinal))
            {
                metadataReads++;
                if (mode == "pusty-glosnik") return Json(MetadataBody(null, null));
                // INNE KONTO USLUGI przy zgodnym serviceId/objectId to INNY material.
                if (mode == "inne-konto") return Json(MetadataBody(FavoriteObjectId, "KONTO-INNEGO-UZYTKOWNIKA"));
                // ZMIANA W TRAKCIE: pierwszy odczyt zgodny, potwierdzajacy JUZ NIE.
                if (mode == "zmiana-w-trakcie" && metadataReads > 1)
                    return Json(MetadataBody("OBIEKT-PIERWSZY", FavoriteAccountId));
                return Json(MetadataBody(FavoriteObjectId, FavoriteAccountId));
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/playback", StringComparison.Ordinal))
            {
                playbackReads++;
                return Json(PlaybackBody(mode switch
                {
                    "pauza" or "zmiana-w-trakcie" => "PLAYBACK_STATE_PAUSED",
                    "radio-idle" or "pusty-glosnik" => "PLAYBACK_STATE_IDLE",
                    _ => "PLAYBACK_STATE_PLAYING"
                }));
            }

            // PRZYJECIE wznowienia. Sonos oddaje pusty obiekt - 200 nie dowodzi,
            // ze material zagral, i niczego tu nie uczymy z odpowiedzi.
            if (request.Method == HttpMethod.Post && path.EndsWith("/playback/play", StringComparison.Ordinal))
                return Json("{}");

            return null;
        };

        var before = h.Handler.Posts.Count;
        FirePreset(h, FavoritePreset());
        var posts = h.Handler.Posts.Skip(before).ToArray();
        var spoken = LastAnnouncement(h);
        var paths = string.Join(" | ", posts.Select(post => post.Uri.AbsolutePath));

        // ZADNEGO seek i ZADNEJ zmiany kolejki w ZADNYM trybie powtorzenia.
        if (posts.Any(post => post.Uri.AbsolutePath.Contains("/playback/seek", StringComparison.Ordinal)))
            throw new Exception("Powtórzenie ulubionego wysłało seek: " + paths);

        switch (mode)
        {
            case "gra":
                if (posts.Length != 0) throw new Exception("Już gra, a poszło " + posts.Length + " POST: " + paths);
                if (spoken != "Nokturny") throw new Exception("Już gra: wymagana SAMA nazwa, powiedziano: " + spoken);
                if (catalogueReads == 0 || metadataReads == 0)
                    throw new Exception("Decyzja bez świeżego odczytu katalogu/metadanych.");
                break;
            case "pauza":
            case "radio-idle":
                if (posts.Length != 1 || !posts[0].Uri.AbsolutePath.EndsWith("/playback/play", StringComparison.Ordinal))
                    throw new Exception("Wznowienie wymaga DOKŁADNIE jednego Play: " + posts.Length + " -> " + paths);
                if (posts.Any(post => post.Uri.AbsolutePath.EndsWith("/favorites", StringComparison.Ordinal)))
                    throw new Exception("Wznowienie przeładowało ulubiony: " + paths);
                if (metadataReads < 2)
                    throw new Exception("Brak POWTÓRNEGO potwierdzenia tożsamości przed Play.");
                if (spoken != "Nokturny") throw new Exception("Po wznowieniu wymagana SAMA nazwa: " + spoken);
                break;
            case "inne-konto":
                if (posts.Length != 1 || !posts[0].Uri.AbsolutePath.EndsWith("/favorites", StringComparison.Ordinal))
                    throw new Exception("Inne konto usługi wymaga ZWYKŁEGO load: " + paths);
                if (!posts[0].Body.Contains("INSERT", StringComparison.Ordinal)
                    || !posts[0].Body.Contains("\"playOnCompletion\":true", StringComparison.Ordinal))
                    throw new Exception("Load nie użył istniejącej akcji INSERT/true: " + posts[0].Body);
                break;
            case "pusty-glosnik":
                if (posts.Length != 1 || !posts[0].Uri.AbsolutePath.EndsWith("/favorites", StringComparison.Ordinal))
                    throw new Exception("Potwierdzony pusty głośnik ma zwyczajnie załadować: " + paths);
                break;
            case "zmiana-w-trakcie":
            case "katalog-blad":
            case "niepelna-tozsamosc":
                if (posts.Length != 0) throw new Exception(mode + ": poszło " + posts.Length + " POST: " + paths);
                if (!spoken.Contains("Nie udało się potwierdzić", StringComparison.OrdinalIgnoreCase))
                    throw new Exception(mode + ": brak uczciwej odmowy, powiedziano: " + spoken);
                break;
        }

        return mode + ": POST=" + posts.Length + " [" + paths + "], katalogGET=" + catalogueReads
            + ", metadataGET=" + metadataReads + ", playbackGET=" + playbackReads + ", mowa=" + spoken;
    }

    /// <summary>
    /// ZMIANA KONTA W TRAKCIE wstrzymanego GET katalogu. Stary kontekst NIE MA
    /// prawa ani wyslac POST, ani powiedziec czegokolwiek w NOWEJ sesji konta.
    /// </summary>
    private static string MeasureFavoriteRepeatAccountSwapDuringRead()
    {
        using var h = RealHarness.Create();
        h.Enter();
        h.Handler.RouteOverride = (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.EndsWith("/favorites", StringComparison.Ordinal))
                return Json(FavoritesWithResourceBody(completeIdentity: true));
            if (request.Method == HttpMethod.Get && path.EndsWith("/playbackMetadata", StringComparison.Ordinal))
                return Json(MetadataBody(FavoriteObjectId, FavoriteAccountId));
            if (request.Method == HttpMethod.Get && path.EndsWith("/playback", StringComparison.Ordinal))
                return Json(PlaybackBody("PLAYBACK_STATE_PAUSED"));
            return null;
        };

        var before = h.Handler.Posts.Count;
        var spokenBefore = h.Announcements.Count;
        var held = h.Handler.HoldNextGet("/favorites");
        var task = h.Window.ActivateSonosPresetForTests(FavoritePreset(), "1");
        h.PumpUntil(() => held.Arrived, TimeSpan.FromSeconds(5), "GET katalogu nie dotarł do transportu");

        // NOWSZY ZAMIAR: zmiana konta PRODUKCYJNA droga koordynatora.
        h.SwapAccount("KONTO-ULUBIONE-B");
        held.Release();
        h.PumpUntil(() => task.IsCompleted, TimeSpan.FromSeconds(15), "próba powtórzenia się nie rozliczyła");
        task.GetAwaiter().GetResult();
        h.PumpQuietly(TimeSpan.FromMilliseconds(200));

        var posts = h.Handler.Posts.Skip(before).ToArray();
        if (posts.Length != 0)
            throw new Exception("Po zmianie konta w trakcie odczytu poszło " + posts.Length + " POST.");
        var after = h.Announcements.Skip(spokenBefore).ToArray();
        if (after.Any(message => message == "Nokturny"))
            throw new Exception("Stary kontekst ogłosił sukces w nowej sesji konta.");

        return "zmiana konta podczas wstrzymanego GET katalogu: 0 POST, bez starego komunikatu "
            + "(po zmianie powiedziano: " + (after.Length == 0 ? "nic" : string.Join(" / ", after)) + ")";
    }
}
