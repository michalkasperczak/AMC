using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Przeglad wykonawcy Spotify na TYM SAMYM mechanizmie co TIDAL
/// (<see cref="ArtistBrowseSection"/>), bez drugiej kopii kategorii.
///
/// GRANICA DOSTAWCY, sprawdzona u zrodla przed napisaniem tego testu:
/// "Web API Changelog - February 2026" wymienia
/// [REMOVED] Get Artist's Top Tracks (GET /artists/{id}/top-tracks).
/// Changelog z marca 2026 cofa wylacznie pola external_ids, nie ten endpoint.
/// Dlatego kategoria "Utwory" NIE MOZE opierac sie na top-tracks. Jedyna
/// udokumentowana, dostepna droga to "Search for Item" z filtrem pola
/// artist: - z limitem 0-10 na rodzaj (obnizony w lutym 2026 z 50) i
/// stronicowaniem przez offset/"next". Wyszukiwarka zwraca tez utwory INNYCH
/// wykonawcow o podobnej nazwie, wiec dopasowanie musi isc po IDENTYFIKATORZE
/// wykonawcy z entry.artists, nie po tekscie.
///
/// Test mierzy pieciu rzeczy, bo zadna nie dowodzi pozostalych:
/// 1. warstwa API: adres, obowiazkowy rynek, limit w granicach dokumentacji,
///    stronicowanie adresem "next", dopasowanie po ID i brak falszywych flag
///    kolekcji,
/// 2. kategorie: sesja Spotify dostaje Albumy i Utwory (bez martwego wiersza
///    "Podobni wykonawcy" - related-artists tez jest niedostepne), a TIDAL
///    zachowuje swoje trzy,
/// 3. nazwy widokow sekcji, takze dla DRUGIEJ sesji Spotify (Librespot) -
///    bez tego druga sesja nadpisalaby widok pierwszej,
/// 4. klawiatura i powrot: fokus na kategorii, Enter/powrot po stabilnym ID,
/// 5. guard spoznionej odpowiedzi: zmiana widoku, sesji, zaznaczenia ORAZ
///    wejscie do odtwarzacza blokuja przestawienie listy.
/// </summary>
internal static class SpotifyArtistRowsTests
{
    internal static void Run()
    {
        Task.Run(SprawdzUtworyWykonawcyZKatalogu).GetAwaiter().GetResult();
        SprawdzKategorieINazwyWidokow();
        SprawdzGuardSpoznionejOdpowiedzi();
        SprawdzWierszDoladowaniaIOdswiezanie();
        SprawdzZyweUiDoladowania();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { SprawdzKlawiatureIPowrot(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(25)))
            throw new Exception("Spotify: przekroczono limit testu interfejsu kategorii wykonawcy.");
        if (failure is not null) throw new Exception("Spotify: kategorie wykonawcy w interfejsie", failure);
        Console.WriteLine("OK: przeglad wykonawcy Spotify - kategorie Albumy/Utwory, utwory z katalogu"
            + " (wyszukiwanie z dopasowaniem ID, partia + jawne doladowanie kolejnych stron),"
            + " brak nazwy wykonawcy dociagany po ID, nazwy widokow obu sesji, klawiatura, powrot,"
            + " wiersz \"Wczytaj wiecej\", prawdziwe komunikaty, F5 i guard spoznionej odpowiedzi");
    }

    // ---------- 1. Warstwa API ----------

    private static async Task SprawdzUtworyWykonawcyZKatalogu()
    {
        var metoda = typeof(SpotifyApiClient).GetMethod(
            "GetArtistTracksAsync", BindingFlags.Public | BindingFlags.Instance);
        Check(metoda is not null,
            "Brak SpotifyApiClient.GetArtistTracksAsync - kategoria \"Utwory\" nie ma skad wziac utworow. "
            + "Get Artist's Top Tracks zostalo usuniete w lutym 2026, wiec droga musi byc inna.");
        var parametry = metoda!.GetParameters().Select(p => p.Name).ToArray();
        Check(parametry.Length == 6,
            $"GetArtistTracksAsync ma {parametry.Length} parametrow zamiast szesciu "
            + "(token, rynek, identyfikator wykonawcy, nazwa wykonawcy, anulowanie, adres nastepnej strony). "
            + "Bez adresu kontynuacji kolejne strony nie maja skad wystartowac.");

        // Atrapa oddaje CZTERY strony, wiecej niz miesci sie w jednej partii.
        // Stare zachowanie (staly limit trzech stron, "next" wyrzucany) musi tu
        // polec: ostatnia strona nie moze zginac bez sladu.
        using var handler = new ArtistTracksHandler();
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);
        var partia = await Wywolaj(metoda, api, "token", "PL", "art1", "Budka Suflera");

        Check(partia is not null, "Utwory wykonawcy wrocily jako brak dostepu zamiast partii.");
        var utwory = Pozycje(partia!);
        Check(handler.Urls.Count == 3,
            $"Pierwsza partia wyslala {handler.Urls.Count} zapytan zamiast trzech stron.");
        var dalej = Next(partia!);
        Check(!string.IsNullOrEmpty(dalej),
            "Partia zgubila adres nastepnej strony. Spotify zwrocilo \"next\", a my go wyrzucilismy - "
            + "reszta katalogu staje sie NIEOSIAGALNA i wiersz \"Wczytaj wiecej\" nie mialby dokad pojsc.");

        var pierwsze = handler.Urls[0];
        Check(pierwsze.Contains("/search", StringComparison.Ordinal),
            $"Utwory wykonawcy nie ida przez udokumentowane wyszukiwanie katalogu: {pierwsze}");
        Check(!pierwsze.Contains("top-tracks", StringComparison.Ordinal),
            "Uzyto endpointu Get Artist's Top Tracks, usunietego przez Spotify w lutym 2026.");
        Check(!pierwsze.Contains("related-artists", StringComparison.Ordinal),
            "Uzyto endpointu related-artists, ktory rowniez nie jest dostepny.");
        Check(pierwsze.Contains("type=track", StringComparison.Ordinal),
            $"Zapytanie nie ogranicza sie do utworow: {pierwsze}");
        Check(pierwsze.Contains("market=PL", StringComparison.Ordinal),
            $"Brak obowiazkowego rynku - Spotify uzna tresc za niedostepna: {pierwsze}");
        var zapytanie = Uri.UnescapeDataString(Parametr(pierwsze, "q") ?? string.Empty);
        Check(zapytanie.Contains("artist:", StringComparison.OrdinalIgnoreCase)
            && zapytanie.Contains("Budka Suflera", StringComparison.Ordinal),
            $"Zapytanie nie filtruje po polu wykonawcy: {zapytanie}");
        foreach (var url in handler.Urls)
        {
            var limit = int.TryParse(Parametr(url, "limit"), out var wartosc) ? wartosc : (int?)null;
            Check(limit is >= 1 and <= 10,
                $"Limit poza granica wyszukiwania (dokumentacja luty 2026: 0-10): {url}");
        }
        Check(handler.Urls[1] == NextUrl(1),
            $"Druga strona nie poszla adresem \"next\" od Spotify: {handler.Urls[1]}");
        Check(handler.Urls[2] == NextUrl(2),
            $"Trzecia strona nie poszla adresem \"next\" od Spotify: {handler.Urls[2]}");
        Check(dalej == NextUrl(3),
            $"Adres kolejnej partii nie jest tym, ktory podalo Spotify: {dalej}");

        var lista = utwory;
        var tytuly = lista.Select(item => item.Title).ToArray();
        // Trzy strony po 3/2/2 pozycje, minus utwor innego wykonawcy odsiany po
        // identyfikatorze, minus powtorzone "t1" z trzeciej strony = 5 pozycji.
        Check(lista.Count == 5,
            $"Pierwsza partia ma {lista.Count} utworow zamiast pieciu: {string.Join(", ", tytuly)}");
        Check(lista.Count > 3,
            $"Pierwsza partia zmiescila sie w trzech pozycjach ({lista.Count}) - stare obciecie.");
        Check(!tytuly.Contains("Cudzy utwor"),
            "Wyszukiwarka zwrocila utwor INNEGO wykonawcy o podobnej nazwie, a lista go przyjela - "
            + "dopasowanie musi isc po identyfikatorze z entry.artists.");
        Check(lista.All(item => item.Kind == MediaItemKind.Track),
            "Pozycja kategorii \"Utwory\" nie jest utworem.");
        Check(lista.All(item => item.Source == $"spotify:track:{item.ExternalId}"),
            "Utwor wykonawcy nie ma adresu odtwarzania.");
        Check(lista.All(item => item.RelatedArtistExternalId == "art1"),
            "Utwor stracil powiazanie z wykonawca.");
        Check(lista.All(item => !item.IsInLibrary && !item.IsFavorite),
            "Utwor z katalogu udaje pozycje zapisana na koncie - Ctrl+L i Ctrl+U klamalyby.");
        Check(lista.Select(item => item.ExternalId).Distinct().Count() == lista.Count,
            "Ta sama pozycja katalogu wrocila kilka razy.");

        // ---- DRUGA PARTIA: klikniecie "Wczytaj wiecej" po trzech stronach ----
        // To jest dokladnie ten przypadek, ktory przekraczal oryginalny blad.
        using var handler2 = new ArtistTracksHandler();
        using var http2 = new HttpClient(handler2);
        using var api2 = new SpotifyApiClient(http2);
        var druga = await Wywolaj(metoda, api2, "token", "PL", "art1", "Budka Suflera", dalej);
        Check(handler2.Urls.Count is >= 1 and <= 3,
            $"Doladowanie wyslalo {handler2.Urls.Count} zapytan - partia ma byc ograniczona, "
            + "a nie niekontrolowanym pobieraniem calego katalogu.");
        Check(handler2.Urls[0] == dalej,
            $"Doladowanie nie poszlo adresem \"next\", tylko wlasnym: {handler2.Urls[0]}");
        var dalszeUtwory = Pozycje(druga!);
        Check(dalszeUtwory.Count > 0,
            "Czwarta strona nie oddala zadnego utworu - przy starym, stalym obcieciu do trzech "
            + "stron te pozycje byly NIEOSIAGALNE.");
        Check(dalszeUtwory.All(item => item.Title != "Jolka, Jolka"),
            "Doladowanie zaczelo katalog od poczatku zamiast isc adresem \"next\".");
        Check(!string.IsNullOrEmpty(Next(druga!)),
            "Doladowanie zgubilo adres nastepnej strony - trzecie uzycie wiersza byloby martwe.");
        Check(dalszeUtwory.All(item => item.RelatedArtistExternalId == "art1"),
            "Utwor z kolejnej strony stracil powiazanie z wykonawca.");
        Check(dalszeUtwory.All(item => !item.IsInLibrary && !item.IsFavorite),
            "Utwor z kolejnej strony udaje pozycje zapisana na koncie.");

        // ---- Strona 0 (obcy limit) i strona z wlasnym "next" ----
        using var obcyLimit = new ObcyLimitHandler();
        using var httpObcy = new HttpClient(obcyLimit);
        using var apiObcy = new SpotifyApiClient(httpObcy);
        var pustaStrona = await Wywolaj(
            metoda, apiObcy, "token", "PL", "art1", "Budka Suflera",
            "https://api.spotify.com/v1/search?q=x&type=track&market=PL&limit=0");
        Check(pustaStrona is not null && Pozycje(pustaStrona).Count == 0,
            "Strona bez pozycji musi wrocic jako pusta partia, nie jako blad.");
        Check(Next(pustaStrona!) == "https://api.spotify.com/v1/search?q=x&type=track&limit=10&offset=99",
            "Partia nie przeniosla adresu \"next\" podanego przez Spotify dla pustej strony. "
            + "Pusta strona z \"next\" NIE oznacza konca katalogu.");

        // ---- Brak nazwy wykonawcy: nie wolno szukac po surowym ID ----
        using var nazwaHandler = new NazwaWykonawcyHandler();
        using var httpNazwa = new HttpClient(nazwaHandler);
        using var apiNazwa = new SpotifyApiClient(httpNazwa);
        var poNazwie = await Wywolaj(metoda, apiNazwa, "token", "PL", "art1", "");
        Check(nazwaHandler.Urls.Count >= 1,
            "Przy pustej nazwie nie poszlo zadne zapytanie.");
        Check(nazwaHandler.Urls[0].Contains("/artists/art1", StringComparison.Ordinal),
            $"Przy pustej nazwie nie dociagnieto nazwy wykonawcy przez GET /artists/id: {nazwaHandler.Urls[0]}");
        var szukanie = nazwaHandler.Urls.FirstOrDefault(u => u.Contains("/search", StringComparison.Ordinal));
        Check(szukanie is not null, "Po ustaleniu nazwy nie poszlo wyszukiwanie katalogu.");
        var q = Uri.UnescapeDataString(Parametr(szukanie!, "q") ?? string.Empty);
        Check(!q.Contains("art1", StringComparison.Ordinal),
            $"Zapytanie szuka po SUROWYM identyfikatorze wykonawcy: {q}. To zwykle szukanie tekstu "
            + "i oddaje utwory bez zwiazku z wykonawca.");
        Check(q.Contains("artist:", StringComparison.OrdinalIgnoreCase)
            && q.Contains("Budka Suflera", StringComparison.Ordinal),
            $"Zapytanie nie uzylo dociagnietej nazwy wykonawcy: {q}");
        Check(Pozycje(poNazwie!).Count == 1, "Utwory po dociagnieciu nazwy nie wrocily.");

        // Gdy i nazwy nie da sie ustalic, musi wrocic TRAFNY powod, nie pustka.
        using var bezNazwy = new OdmowaHandler(HttpStatusCode.NotFound);
        using var httpBezNazwy = new HttpClient(bezNazwy);
        using var apiBezNazwy = new SpotifyApiClient(httpBezNazwy);
        var brak = await Wywolaj(metoda, apiBezNazwy, "token", "PL", "art1", "");
        var powod = Failure(brak!);
        Check(powod is not null && powod.ToString() == "UnknownArtistName",
            $"Brak nazwy wykonawcy nie zostal odroznony od odmowy Spotify: {powod?.ToString() ?? "brak"}. "
            + "Komunikat dla uzytkownika musi powiedziec, CZEGO brakuje.");

        // ---- 401/403/429 ----
        foreach (var kod in new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound })
        {
            using var odmowa = new OdmowaHandler(kod);
            using var httpOdmowa = new HttpClient(odmowa);
            using var apiOdmowa = new SpotifyApiClient(httpOdmowa);
            var wynik = await Wywolaj(metoda, apiOdmowa, "token", "PL", "art1", "Budka Suflera");
            Check(Failure(wynik!)?.ToString() == "Denied",
                $"Odmowa {(int)kod} musi wrocic jako brak danych, nie jako pusta lista udajaca brak utworow.");
            Check(Pozycje(wynik!).Count == 0 && string.IsNullOrEmpty(Next(wynik!)),
                $"Odmowa {(int)kod} oddala pozycje albo adres nastepnej strony.");
        }
        foreach (var kod in new[] { HttpStatusCode.Unauthorized, (HttpStatusCode)429 })
        {
            using var blad = new OdmowaHandler(kod);
            using var httpBlad = new HttpClient(blad);
            using var apiBlad = new SpotifyApiClient(httpBlad);
            Exception? wyjatek = null;
            try { await Wywolaj(metoda, apiBlad, "token", "PL", "art1", "Budka Suflera"); }
            catch (Exception exception) { wyjatek = exception; }
            Check(wyjatek is not null,
                $"Blad {(int)kod} przeszedl po cichu jako brak utworow. 401 i 429 to nie jest "
                + "\"wykonawca nie ma utworow\" - uzytkownik musi uslyszec prawdziwy powod.");
        }
    }

    private static async Task<object?> Wywolaj(
        MethodInfo metoda, SpotifyApiClient api, string token, string rynek, string id, string nazwa,
        string? next = null)
    {
        var task = (Task)metoda.Invoke(api, [token, rynek, id, nazwa, CancellationToken.None, next])!;
        await task.ConfigureAwait(false);
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static IReadOnlyList<MediaItem> Pozycje(object partia) =>
        (IReadOnlyList<MediaItem>)partia.GetType().GetProperty("Items")!.GetValue(partia)!;

    private static string? Next(object partia) =>
        (string?)partia.GetType().GetProperty("Next")!.GetValue(partia);

    private static object? Failure(object partia) =>
        partia.GetType().GetProperty("Failure")!.GetValue(partia);

    // ---------- 2 i 3. Kategorie i nazwy widokow ----------

    private static void SprawdzKategorieINazwyWidokow()
    {
        var sekcjeMetoda = typeof(MainWindow).GetMethod(
            "ArtistSectionsForSession", BindingFlags.NonPublic | BindingFlags.Static);
        Check(sekcjeMetoda is not null,
            "Brak wspolnego doboru kategorii per sesja (ArtistSectionsForSession) - "
            + "lista kategorii nie moze byc zaszyta w jednej usludze.");
        ArtistBrowseSection[] Sekcje(string sesja) =>
            ((IEnumerable<ArtistBrowseSection>)sekcjeMetoda!.Invoke(null, [sesja])!).ToArray();

        Check(Sekcje("spotify").SequenceEqual([ArtistBrowseSection.Albums, ArtistBrowseSection.Tracks]),
            "Sesja Spotify musi miec Albumy i Utwory. Podobnych wykonawcow Spotify nie udostepnia "
            + "(related-artists), wiec martwy wiersz nie moze sie pojawic.");
        Check(Sekcje("spotifyLibrespot").SequenceEqual([ArtistBrowseSection.Albums, ArtistBrowseSection.Tracks]),
            "Druga sesja Spotify (Librespot) dostala inne kategorie niz pierwsza.");
        Check(Sekcje("tidal").SequenceEqual(
                [ArtistBrowseSection.Albums, ArtistBrowseSection.Tracks, ArtistBrowseSection.SimilarArtists]),
            "TIDAL stracil ktoras ze swoich trzech kategorii.");

        var widokMetoda = typeof(MainWindow).GetMethod(
            "SpotifySectionViewName", BindingFlags.NonPublic | BindingFlags.Static);
        Check(widokMetoda is not null,
            "Brak nazwy widoku sekcji Spotify - bez sufiksu sekcji Albumy i Utwory dzielilyby jeden widok.");
        var artysta = new MediaItem { ExternalId = "art1", Title = "Budka Suflera", Kind = MediaItemKind.Artist };
        string Widok(string sesja, ArtistBrowseSection? sekcja) =>
            (string)widokMetoda!.Invoke(null, [sesja, artysta, sekcja])!;

        var przeglad = Widok("spotify", null);
        var utwory = Widok("spotify", ArtistBrowseSection.Tracks);
        var albumy = Widok("spotify", ArtistBrowseSection.Albums);
        Check(przeglad != utwory && utwory != albumy && przeglad != albumy,
            $"Widoki przegladu i sekcji sie pokrywaja: {przeglad}, {utwory}, {albumy}");
        Check(utwory.EndsWith(":section:Tracks", StringComparison.Ordinal),
            $"Sekcja nie uzywa wspolnego sufiksu widoku co TIDAL: {utwory}");
        var drugaSesja = Widok("spotifyLibrespot", ArtistBrowseSection.Tracks);
        Check(drugaSesja != utwory && drugaSesja.Contains("spotifyLibrespot", StringComparison.Ordinal)
            && drugaSesja.EndsWith(":section:Tracks", StringComparison.Ordinal),
            $"Druga sesja Spotify nadpisalaby widok pierwszej: {drugaSesja}");
    }

    // ---------- 5. Guard spoznionej odpowiedzi ----------

    private static void SprawdzGuardSpoznionejOdpowiedzi()
    {
        var typ = typeof(MainWindow).Assembly.GetType("AccessibleMediaController.Windows.ServiceInteractionContext");
        Check(typ is not null,
            "Brak wspolnego kontekstu odpowiedzi dla obu uslug (ServiceInteractionContext) - "
            + "Spotify nie moze miec wlasnej kopii guardu.");
        object Kontekst(long wersja, string sesja, string widok, string? element, bool odtwarzacz) =>
            Activator.CreateInstance(typ!, [wersja, sesja, widok, element, odtwarzacz])!;
        var canPresent = typ!.GetMethod("CanPresent", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typ.GetMethod("CanPresent", BindingFlags.Public | BindingFlags.Instance);
        Check(canPresent is not null, "Kontekst odpowiedzi nie ma metody CanPresent.");
        bool Wolno(object start, object teraz, bool oknoDostepne) =>
            (bool)canPresent!.Invoke(start, [teraz, oknoDostepne])!;

        var start = Kontekst(7, "spotify", "Spotify:art1", "item-1", false);
        Check(Wolno(start, Kontekst(7, "spotify", "Spotify:art1", "item-1", false), true),
            "Aktualna odpowiedz zostala odrzucona.");
        Check(!Wolno(start, Kontekst(8, "spotify", "Spotify:art1", "item-1", false), true),
            "Spozniona odpowiedz z poprzedniego zadania przestawila liste.");
        Check(!Wolno(start, Kontekst(7, "tidal", "Spotify:art1", "item-1", false), true),
            "Odpowiedz Spotify przestawila liste po zmianie sesji.");
        Check(!Wolno(start, Kontekst(7, "spotify", "Biblioteka", "item-1", false), true),
            "Odpowiedz przestawila liste po zmianie widoku.");
        Check(!Wolno(start, Kontekst(7, "spotify", "Spotify:art1", "item-2", false), true),
            "Odpowiedz przestawila liste po zmianie zaznaczenia.");
        Check(!Wolno(start, Kontekst(7, "spotify", "Spotify:art1", "item-1", true), true),
            "Odpowiedz przestawila liste, gdy uzytkownik wszedl do odtwarzacza.");
        Check(!Wolno(start, Kontekst(7, "spotify", "Spotify:art1", "item-1", false), false),
            "Odpowiedz przestawila liste przy niedostepnym oknie (otwarte menu albo okno potomne).");

        // Bramka musi byc WSPOLNA, nie kopiowana per usluga.
        var spotifyGuard = typeof(MainWindow).GetMethod(
            "CanPresentSpotifyResponse", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(spotifyGuard is not null && spotifyGuard.GetParameters().Length == 1
            && spotifyGuard.GetParameters()[0].ParameterType.Name == "ServiceInteractionContext",
            "Guard Spotify nie przyjmuje wspolnego kontekstu interakcji - to druga kopia regul.");
    }

    // ---------- 6. Wiersz \"Wczytaj wiecej\" i zachowanie F5 ----------

    private static void SprawdzWierszDoladowaniaIOdswiezanie()
    {
        // Wiersz doladowania MUSI istniec jako osobny mechanizm, a nie jako
        // podszycie sie pod wiersz podcastu (ten odslania odcinki pobrane
        // lokalnie; tu leci nowe zapytanie do Spotify).
        var wlasciwosc = typeof(MainWindow).Assembly
            .GetTypes().First(t => t.Name == "MediaItemRow")
            .GetProperty("LoadMoreSpotifyTracksViewName");
        Check(wlasciwosc is not null,
            "Wiersz listy nie ma roli \"doladuj utwory Spotify\". Bez niej albo nie ma czym "
            + "doladowac kolejnej strony, albo wiersz podszywalby sie pod podcastowy.");

        var fabryka = typeof(MainWindow).GetMethod(
            "AppendSpotifyTracksLoadMoreRow", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(fabryka is not null,
            "Brak fabryki wiersza \"Wczytaj wiecej utworow\" - kategoria Utwory nie ma jak "
            + "zaproponowac nastepnej strony.");

        var stanTyp = typeof(MainWindow).GetNestedType(
            "SpotifyContainerViewState", BindingFlags.NonPublic);
        Check(stanTyp is not null, "Brak stanu widoku kontenera Spotify.");
        Check(stanTyp!.GetProperty("NextTracksUrl") is not null,
            "Stan widoku kategorii Utwory nie pamieta adresu nastepnej strony - "
            + "po otwarciu widoku reszta katalogu bylaby NIEOSIAGALNA.");

        var loader = typeof(MainWindow).GetMethod(
            "LoadMoreSpotifyArtistTracksAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(loader is not null, "Brak sciezki doladowania kolejnych utworow wykonawcy.");

        // Anulowanie: dluga sciezka nie moze zostac wieczna praca w tle.
        Check(typeof(MainWindow).GetMethod(
                "BeginSpotifyWork", BindingFlags.NonPublic | BindingFlags.Instance) is not null
            && typeof(MainWindow).GetMethod(
                "CancelSpotifyWork", BindingFlags.NonPublic | BindingFlags.Instance) is not null,
            "Odczyt katalogu Spotify nie ma anulowania. Przy nowej nawigacji albo zamknieciu okna "
            + "zostalaby wiecznie pracujaca robota na sieci.");
        var loadMetoda = typeof(MainWindow).GetMethod(
            "LoadSpotifyContainerItemsAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(loadMetoda is not null, "Brak odczytu zawartosci kontenera Spotify.");
        Check(loadMetoda!.GetParameters().Any(p => p.ParameterType == typeof(CancellationToken)),
            "Odczyt kontenera Spotify nadal nie przyjmuje anulowania (bylo CancellationToken.None).");
        Check(loadMetoda.GetParameters().Any(p => p.Name == "continuationUrl"),
            "Odczyt kontenera nie przyjmuje adresu nastepnej strony.");

        // Transport HTTP wstrzykiwany: test UI nie ma prawa dotknac sieci ani konta.
        Check(typeof(MainWindow).GetProperty(
                "SpotifyHttpClientForTests",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public) is not null,
            "Nie da sie wstrzyknac transportu HTTP - test UI musialby wolac prawdziwe Spotify.");

        // KOMUNIKAT: mowi POBRANO/WYSWIETLONO N i wskazuje wiecej, a NIE przypisuje
        // naszego limitu partii Spotify ani nie udaje calej dyskografii.
        var komunikat = typeof(MainWindow).GetMethod(
            "SpotifyTracksBatchMessage", BindingFlags.NonPublic | BindingFlags.Static);
        Check(komunikat is not null, "Brak komunikatu partii utworow.");
        string Komunikat(int n, string? next) =>
            (string)komunikat!.Invoke(null, ["Budka Suflera", n, next])!;

        var zWiecej = Komunikat(30, "https://api.spotify.com/v1/search?offset=30");
        Check(zWiecej.Contains("30", StringComparison.Ordinal),
            $"Komunikat nie mowi, ile pobrano: {zWiecej}");
        Check(zWiecej.Contains("wiecej", StringComparison.OrdinalIgnoreCase)
            || zWiecej.Contains("więcej", StringComparison.OrdinalIgnoreCase),
            $"Komunikat nie wskazuje, ze w katalogu jest wiecej: {zWiecej}");
        Check(!zWiecej.Contains("Spotify udostępnia wybór", StringComparison.OrdinalIgnoreCase)
            && !zWiecej.Contains("Spotify udostepnia wybor", StringComparison.OrdinalIgnoreCase),
            $"Komunikat przypisuje Spotify NASZ limit partii: {zWiecej}. Obciecie jest nasze.");
        Check(!zWiecej.Contains("wszystkie utwory", StringComparison.OrdinalIgnoreCase),
            $"Komunikat przedstawia partie jako wszystkie utwory wykonawcy: {zWiecej}");

        var bezWiecej = Komunikat(12, null);
        Check(bezWiecej.Contains("12", StringComparison.Ordinal)
            && !bezWiecej.Contains("Wczytaj więcej", StringComparison.OrdinalIgnoreCase),
            $"Przy wyczerpanym katalogu komunikat nadal zapowiada doladowanie: {bezWiecej}");

        // F5 na PRZEGLADZIE nie robi odczytu z sieci; F5 na KATEGORII robi.
        var f5 = typeof(MainWindow).GetMethod(
            "TryHandleArtistSectionCommand", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(f5 is not null, "Brak obslugi F5 dla kategorii wykonawcy.");
        var zrodlo = ZrodloMetody("MainWindow.ArtistBrowse.cs");
        Check(zrodlo.Contains("IsArtistOverview", StringComparison.Ordinal)
            && zrodlo.Contains("SelectedArtistSection", StringComparison.Ordinal),
            "F5 na przegladzie kategorii nie rozpoznaje, ze nie ma czego odswiezac z sieci - "
            + "ponowne otwarcie przestawia fokus na pierwszy wiersz.");
    }

    private static string ZrodloMetody(string plik)
    {
        var katalog = new DirectoryInfo(AppContext.BaseDirectory);
        while (katalog is not null && !Directory.Exists(Path.Combine(katalog.FullName, "src")))
            katalog = katalog.Parent;
        Check(katalog is not null, "Nie znaleziono katalogu zrodel repozytorium.");
        var sciezka = Path.Combine(
            katalog!.FullName, "src", "AccessibleMediaController.Windows", plik);
        Check(File.Exists(sciezka), $"Brak pliku zrodlowego {plik}.");
        return File.ReadAllText(sciezka);
    }

    // ---------- 4. Klawiatura i powrot ----------

    private static void SprawdzKlawiatureIPowrot()
    {
        var artysta = new MediaItem { ExternalId = "art1", Title = "Budka Suflera", Kind = MediaItemKind.Artist };
        var sekcjeMetoda = typeof(MainWindow).GetMethod(
            "ArtistSectionsForSession", BindingFlags.NonPublic | BindingFlags.Static)!;
        var fabryka = typeof(MainWindow).GetMethod(
            "CreateArtistSectionRows", BindingFlags.NonPublic | BindingFlags.Static);
        Check(fabryka is not null,
            "Brak wspolnej fabryki wierszy kategorii (CreateArtistSectionRows) - "
            + "Spotify nie moze dostac drugiej kopii mechanizmu TIDAL.");
        var sekcje = sekcjeMetoda.Invoke(null, ["spotify"])!;
        object[] Wiersze() => ((IEnumerable)fabryka!.Invoke(null, [artysta, sekcje])!).Cast<object>().ToArray();

        var oczekiwane = new[] { "Albumy", "Utwory" };
        var wiersze = Wiersze();
        Check(wiersze.Select(row => row.ToString()).SequenceEqual(oczekiwane),
            $"Przeglad wykonawcy Spotify pokazuje: {string.Join(", ", wiersze.Select(r => r.ToString()))}");
        string Id(object row) => ((MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!).Id;
        Check(wiersze.Select(Id).Distinct().Count() == 2 && wiersze.Select(Id).SequenceEqual(Wiersze().Select(Id)),
            "Identyfikatory nawigacji kategorii nie sa stabilne - powrot na kategorie bylby losowy.");
        foreach (var row in wiersze)
        {
            var item = (MediaItem)row.GetType().GetProperty("Item")!.GetValue(row)!;
            Check(item.ExternalId is null && !item.IsFavorite && !item.IsInLibrary,
                "Kategoria udaje element zdalnej kolekcji.");
            Check(item.Kind == MediaItemKind.Folder, "Kategoria nie jest folderem nawigacji.");
            Check(row.GetType().GetProperty("ArtistSection")!.GetValue(row) is ArtistBrowseSection,
                "Wiersz kategorii nie ma roli nawigacyjnej wspolnego mechanizmu.");
        }

        var style = new Style(typeof(ListBoxItem));
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding("Label")));
        var list = new ListBox
        {
            ItemsSource = Wiersze(), DisplayMemberPath = "Label", ItemContainerStyle = style,
            IsTextSearchEnabled = false, SelectedIndex = 0
        };
        TextSearch.SetTextPath(list, "NavigationText");
        var window = new Window { Content = list, Width = 350, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show(); window.Activate(); Drain(window.Dispatcher);
            for (var i = 0; i < oczekiwane.Length; i++)
            {
                list.SelectedIndex = i; list.UpdateLayout();
                var container = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(i);
                container.Focus(); Keyboard.Focus(container);
                Check(UIElementAutomationPeer.CreatePeerForElement(container)!.GetName() == oczekiwane[i],
                    "UI Automation nie otrzymuje samej nazwy kategorii.");
                Check(container.IsKeyboardFocused, "Kategoria nie otrzymala fokusu.");
            }
            // Enter wchodzi w "Utwory", a powrot musi ustawic fokus na TEJ SAMEJ
            // kategorii, nie na pierwszym wierszu listy.
            var zapamietana = Id(list.SelectedItem);
            list.ItemsSource = new[] { "Utwor testowy" };
            list.ItemsSource = Wiersze();
            list.SelectedItem = list.Items.Cast<object>().Single(row => Id(row) == zapamietana);
            list.UpdateLayout();
            var przywrocony = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
            przywrocony.Focus(); Keyboard.Focus(przywrocony);
            Check(przywrocony.IsKeyboardFocused && list.SelectedIndex == 1,
                "Powrot nie wrocil na kategorie \"Utwory\".");
            AutomationProperties.SetName(przywrocony, "Wstecz, Utwory");
            przywrocony.ClearValue(AutomationProperties.NameProperty);
            Check(UIElementAutomationPeer.CreatePeerForElement(przywrocony)!.GetName() == oczekiwane[1],
                "Jednorazowy komunikat powrotu usunal podstawowa etykiete kategorii.");
        }
        finally { window.Close(); }
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
    }

    private static string? Parametr(string url, string nazwa)
    {
        foreach (var para in new Uri(url).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var podzial = para.Split('=', 2);
            if (podzial.Length == 2 && podzial[0] == nazwa) return podzial[1];
        }
        return null;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string NextUrl(int strona) =>
        "https://api.spotify.com/v1/search?q=artist%3A%22Budka%20Suflera%22&type=track&market=PL&limit=10"
        + $"&offset={strona * 10}";

    /// <summary>
    /// Atrapa wyszukiwania Spotify: oddaje CZTERY strony - wiecej, niz miesci
    /// sie w jednej partii. W pierwszej celowo podrzuca utwor innego wykonawcy
    /// o mylacej nazwie, a w trzeciej powtarza pozycje z pierwszej.
    /// </summary>
    private sealed class ArtistTracksHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Add(url);
            var limit = int.TryParse(Parametr(url, "limit"), out var wartosc) ? wartosc : (int?)null;
            if (limit is null or < 0 or > 10)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """{"error":{"status":400,"message":"Invalid limit"}}""",
                        Encoding.UTF8, "application/json")
                });
            }
            var offset = int.TryParse(Parametr(url, "offset"), out var przesuniecie) ? przesuniecie : 0;
            // Katalog CELOWO dluzszy niz dwie partie: po doladowaniu wiersz
            // "Wczytaj wiecej" ma nadal byc na liscie. Strona 20 powtarza "t1",
            // zeby zmierzyc deduplikacje przy sklejaniu partii.
            var strona = offset / 10;
            var koniec = strona >= 9;
            var pozycje = strona == 0
                ? new[] { ("t1", "Jolka, Jolka", "art1"), ("t2", "Takie tango", "art1"),
                          ("t9", "Cudzy utwor", "inny") }
                : strona == 2
                    ? [("t1", "Jolka, Jolka", "art1"), ($"u{strona}a", $"Utwor {strona} A", "art1")]
                    : [($"u{strona}a", $"Utwor {strona} A", "art1"),
                       ($"u{strona}b", $"Utwor {strona} B", "art1")];
            var body = Strona(pozycje, koniec ? null : NextUrl(strona + 1));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    // ---------- 7. ZYWE UI: doladowanie, F5, spozniona odpowiedz ----------

    private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>
    /// Test na PRAWDZIWYM oknie WPF z WSTRZYKNIETYM transportem HTTP (zero
    /// ruchu do Spotify i zero danych konta). Sprawdza to, czego refleksja po
    /// sygnaturach nie zlapie: ze wiersz doladowania faktycznie wchodzi na
    /// liste, ze klikniecie go dokleja kolejna strone, ze F5 na przegladzie
    /// utrzymuje fokus i ze spozniona odpowiedz nie przestawia widoku.
    /// </summary>
    private static void SprawdzZyweUiDoladowania()
    {
        Exception? blad = null;
        var watek = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var root = Path.Combine(Path.GetTempPath(), "amc-artist-load-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? okno = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.LastSessionId = "spotify";
                state.Settings.SpotifySessionUnificationVersion = SpotifySessionMigration.Version;
                okno = new MainWindow(state, new ConfigurationStore(Path.Combine(root, "state.json")));
                okno.Show();

                var sessions = (SessionManager)typeof(MainWindow)
                    .GetField("_sessions", Priv)!.GetValue(okno)!;
                sessions.SelectSession("spotify");

                // Transport atrapy: cztery strony katalogu, bez sieci i bez konta.
                var handler = new ArtistTracksHandler();
                typeof(MainWindow).GetProperty("SpotifyHttpClientForTests", Priv)!
                    .SetValue(okno, new HttpClient(handler));
                typeof(MainWindow).GetProperty("SpotifyAccessTokenForTests", Priv)!
                    .SetValue(okno, "token-testowy");

                var artysta = new MediaItem
                {
                    ExternalId = "art1",
                    Title = "Budka Suflera",
                    Kind = MediaItemKind.Artist
                };

                // --- Kategoria Utwory: PIERWSZA partia + wiersz doladowania ---
                Pompuj(okno, typeof(MainWindow)
                    .GetMethod("OpenSpotifyContainerAsync", Priv)!
                    .Invoke(okno, [artysta, ArtistBrowseSection.Tracks]) as Task);

                var lista = (ListBox)typeof(MainWindow).GetField("MediaList",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!
                    .GetValue(okno)!;
                var wiersze = Wiersze(lista);
                var poPierwszej = wiersze.Count;
                Check(poPierwszej > 3,
                    $"Kategoria Utwory pokazala {poPierwszej} wierszy. Stale obciecie do 3 "
                    + "przezylo poprawke - pierwsza partia ma byc rozsadna, nie trzy pozycje.");

                var loader = wiersze[^1];
                Check(LoadMoreNazwa(loader) is not null,
                    "Ostatni wiersz kategorii Utwory to nie wiersz doladowania. Skoro Spotify "
                    + "oddalo next, reszta katalogu jest NIEOSIAGALNA z klawiatury.");
                var etykieta = Tytul(loader);
                Check(etykieta.Contains("Wczytaj", StringComparison.OrdinalIgnoreCase)
                    && !etykieta.Contains("odcink", StringComparison.OrdinalIgnoreCase),
                    $"Wiersz doladowania podszywa sie pod podcast albo nie mowi, co robi: {etykieta}");

                var utworowPrzed = wiersze.Count(w => LoadMoreNazwa(w) is null);
                var zapytanPrzed = handler.Urls.Count;

                // --- Klik/Enter na wierszu doladowania: DRUGA partia ---
                lista.SelectedIndex = wiersze.Count - 1;
                Pompuj(okno, typeof(MainWindow)
                    .GetMethod("LoadMoreSpotifyArtistTracksAsync", Priv)!
                    .Invoke(okno, [LoadMoreNazwa(loader)]) as Task);

                var poDrugiej = Wiersze(lista);
                var utworowPo = poDrugiej.Count(w => LoadMoreNazwa(w) is null);
                Check(utworowPo > utworowPrzed,
                    $"Po uzyciu wiersza doladowania liczba utworow nie wzrosla ({utworowPrzed} -> "
                    + $"{utworowPo}). Doladowanie jest MARTWE.");
                Check(handler.Urls.Count > zapytanPrzed,
                    "Doladowanie nie wyslalo zadnego nowego zapytania - wiersz tylko udaje, ze dziala.");
                Check(handler.Urls.Skip(zapytanPrzed).All(u => u.Contains("offset=", StringComparison.Ordinal)),
                    "Doladowanie nie poszlo adresem nastepnej strony - zaczelo katalog od poczatku.");

                // Brak duplikatow po zlozeniu partii (trzecia strona powtarza "t1").
                var identy = poDrugiej.Where(w => LoadMoreNazwa(w) is null)
                    .Select(w => Element(w)?.ExternalId).Where(x => x is { Length: > 0 }).ToArray();
                Check(identy.Distinct(StringComparer.OrdinalIgnoreCase).Count() == identy.Length,
                    "Po doladowaniu lista ma powtorzone utwory - deduplikacja katalogu nie objela nowej partii.");

                // Wiersz doladowania NIE moze trafic do kolejki ani biblioteki.
                lista.SelectedIndex = poDrugiej.Count - 1;
                var zaznaczony = lista.SelectedItem!;
                Check(LoadMoreNazwa(zaznaczony) is not null,
                    "Zaznaczenie nie stanelo na wierszu doladowania - test nie mierzy tego, co mial. "
                    + $"Wierszy: {poDrugiej.Count}; zaznaczony: {Tytul(zaznaczony)}; "
                    + $"ostatni w ItemsSource: {Tytul(poDrugiej[^1])}; "
                    + $"loaderow: {poDrugiej.Count(w => LoadMoreNazwa(w) is not null)}.");
                Check(okno.ActionItem is null,
                    "Wiersz Wczytaj wiecej udaje element multimedialny (ActionItem = "
                    + $"{okno.ActionItem?.Title}) - wpadlby do kolejki, biblioteki, presetow "
                    + "albo zapisu playlisty.");
                Check(okno.ActionItems.All(i => i.Id != Element(zaznaczony)?.Id),
                    "Wiersz Wczytaj wiecej wchodzi do zbiorczej listy dzialan (ActionItems).");

                // Podwojny klik: drugie uzycie W TRAKCIE pracy nie mnozy zapytan.
                // Atrapa musi WSTRZYMAC pierwsza odpowiedz, inaczej oba wywolania
                // przebiegna po kolei i test nic nie zmierzy.
                var nazwaWidoku = LoadMoreNazwa(poDrugiej[^1]);
                if (nazwaWidoku is not null)
                {
                    var brama = new WolnaOdpowiedzHandler();
                    typeof(MainWindow).GetProperty("SpotifyHttpClientForTests", Priv)!
                        .SetValue(okno, new HttpClient(brama));
                    var metoda = typeof(MainWindow).GetMethod("LoadMoreSpotifyArtistTracksAsync", Priv)!;
                    var a = metoda.Invoke(okno, [nazwaWidoku]) as Task;
                    var b = metoda.Invoke(okno, [nazwaWidoku]) as Task;
                    Check(brama.Zapytan == 1,
                        $"Dwa klikniecia w wierszu doladowania ruszyly {brama.Zapytan} zapytania "
                        + "rownolegle - ten sam fragment katalogu leci dwa razy.");
                    brama.Zwolnij();
                    Pompuj(okno, a);
                    Pompuj(okno, b);
                    typeof(MainWindow).GetProperty("SpotifyHttpClientForTests", Priv)!
                        .SetValue(okno, new HttpClient(handler));
                }

                // --- F5 na PRZEGLADZIE kategorii: fokus i kategoria zostaja ---
                Pompuj(okno, typeof(MainWindow)
                    .GetMethod("OpenSpotifyContainerAsync", Priv)!
                    .Invoke(okno, [artysta, null]) as Task);
                var przeglad = Wiersze(lista);
                Check(przeglad.Count >= 2, "Przeglad wykonawcy nie pokazal kategorii.");
                lista.SelectedIndex = 1;                       // Utwory
                var widokPrzed = Widok(okno);
                var zaznaczonePrzed = Tytul(przeglad[1]);
                var zapytanPrzedF5 = handler.Urls.Count;

                typeof(MainWindow).GetMethod("TryHandleArtistSectionCommand", Priv)!
                    .Invoke(okno, [CommandIds.RefreshLocalLibrary]);
                Pompuj(okno, null);

                Check(Widok(okno) == widokPrzed,
                    $"F5 na przegladzie kategorii zmienilo widok ({widokPrzed} -> {Widok(okno)}).");
                var poF5 = Wiersze(lista);
                Check(lista.SelectedIndex == 1 && Tytul(poF5[1]) == zaznaczonePrzed,
                    "F5 na przegladzie kategorii przestawilo fokus na inny wiersz. Uzytkownik "
                    + "czytnika traci miejsce w liscie.");
                Check(handler.Urls.Count == zapytanPrzedF5,
                    "F5 na przegladzie kategorii poszlo do sieci, choc lista kategorii jest lokalna.");

                // --- Spozniona odpowiedz po Back: nie przestawia widoku ---
                Pompuj(okno, typeof(MainWindow)
                    .GetMethod("OpenSpotifyContainerAsync", Priv)!
                    .Invoke(okno, [artysta, ArtistBrowseSection.Tracks]) as Task);
                var wolnaAtrapa = new WolnaOdpowiedzHandler();
                typeof(MainWindow).GetProperty("SpotifyHttpClientForTests", Priv)!
                    .SetValue(okno, new HttpClient(wolnaAtrapa));
                var zadanie = typeof(MainWindow)
                    .GetMethod("LoadMoreSpotifyArtistTracksAsync", Priv)!
                    .Invoke(okno, [LoadMoreNazwa(Wiersze(lista)[^1])]) as Task;

                // Uzytkownik wraca (Backspace) jeszcze przed odpowiedzia.
                typeof(MainWindow).GetMethod("NavigateBack", Priv)?.Invoke(okno, null);
                var widokPoBack = Widok(okno);
                wolnaAtrapa.Zwolnij();
                Pompuj(okno, zadanie);
                Check(Widok(okno) == widokPoBack,
                    $"Spozniona odpowiedz przestawila widok po powrocie ({widokPoBack} -> {Widok(okno)}).");

                // --- Spozniona odpowiedz przy ZMIANIE KATEGORII (wiersz kategorii) ---
                // Tu przechodzil oryginalny blad: na wierszach kategorii ActionItem
                // jest null, wiec guard oparty na SelectedItem porownywal null z
                // null i przepuszczal odpowiedz po przejsciu na inna kategorie.
                Pompuj(okno, typeof(MainWindow)
                    .GetMethod("OpenSpotifyContainerAsync", Priv)!
                    .Invoke(okno, [artysta, null]) as Task);
                var kategorie = Wiersze(lista);
                lista.SelectedIndex = 1;
                var przechwyt = typeof(MainWindow).GetMethod(
                    "CaptureServiceInteractionContext", Priv, null, [typeof(long)], null);
                Check(przechwyt is not null,
                    "Brak wspolnego przechwytu kontekstu dla podanego licznika nawigacji.");
                var wersja = (long)typeof(MainWindow).GetField("_spotifyNavigationVersion", Priv)!
                    .GetValue(okno)!;
                var naUtworach = przechwyt!.Invoke(okno, [wersja])!;

                lista.SelectedIndex = 0;                       // uzytkownik przeszedl na Albumy
                var wolno = (bool)typeof(MainWindow)
                    .GetMethod("CanPresentSpotifyResponse", Priv)!
                    .Invoke(okno, [naUtworach])!;
                Check(!wolno,
                    "Spozniona odpowiedz dla kategorii Utwory wolno przedstawic, choc uzytkownik "
                    + "stoi juz na innym wierszu kategorii. Guard oparty na SelectedItem/ActionItem "
                    + "porownuje null z null i nic nie chroni - musi brac trwaly Item.Id wiersza.");
                lista.SelectedIndex = 1;                       // powrot na Utwory
                Check((bool)typeof(MainWindow).GetMethod("CanPresentSpotifyResponse", Priv)!
                        .Invoke(okno, [naUtworach])!,
                    "Guard odrzuca odpowiedz, choc fokus wrocil na ten sam wiersz kategorii.");

                // --- Odmowa uslugi: prawdziwy komunikat, brak martwego wiersza ---
                typeof(MainWindow).GetProperty("SpotifyHttpClientForTests", Priv)!
                    .SetValue(okno, new HttpClient(new OdmowaHandler(HttpStatusCode.TooManyRequests)));
                Pompuj(okno, typeof(MainWindow)
                    .GetMethod("OpenSpotifyContainerAsync", Priv)!
                    .Invoke(okno, [artysta, ArtistBrowseSection.Tracks]) as Task);
                Check(Wiersze(lista).All(w => LoadMoreNazwa(w) is null),
                    "Po odmowie Spotify (429) na liscie zostal wiersz doladowania, ktory nie ma "
                    + "czego wczytac - martwy wiersz dla czytnika.");
            }
            catch (Exception exception)
            {
                blad = exception;
            }
            finally
            {
                try { okno?.Close(); } catch { }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        watek.SetApartmentState(ApartmentState.STA);
        watek.Start();
        Check(watek.Join(TimeSpan.FromMinutes(3)), "Test zywego UI nie zakonczyl sie w czasie.");
        if (blad is not null) throw new Exception(blad.Message, blad);
    }

    private static List<object> Wiersze(ListBox lista) =>
        lista.ItemsSource is IEnumerable zrodlo ? zrodlo.Cast<object>().ToList() : [];

    private static string? LoadMoreNazwa(object wiersz) =>
        (string?)wiersz.GetType().GetProperty("LoadMoreSpotifyTracksViewName")?.GetValue(wiersz);

    private static MediaItem? Element(object wiersz) =>
        wiersz.GetType().GetProperty("Item")?.GetValue(wiersz) as MediaItem;

    private static string Tytul(object wiersz) =>
        (string?)wiersz.GetType().GetProperty("Title")?.GetValue(wiersz)
        ?? Element(wiersz)?.Title ?? string.Empty;

    private static string Widok(MainWindow okno) =>
        (string?)typeof(MainWindow).GetField("_currentView", Priv)!.GetValue(okno) ?? string.Empty;

    /// <summary>Pompuje petle komunikatow, az zadanie sie skonczy (UI zostaje na dispatcherze).</summary>
    private static void Pompuj(MainWindow okno, Task? zadanie)
    {
        var zegar = Stopwatch.StartNew();
        while (zegar.Elapsed < TimeSpan.FromSeconds(30))
        {
            okno.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            if (zadanie is null || zadanie.IsCompleted) { if (zadanie is null) return; break; }
            Thread.Sleep(15);
        }
        if (zadanie?.IsFaulted == true && zadanie.Exception is not null)
            throw zadanie.Exception.GetBaseException();
        // Domkniecie kontynuacji, ktore czekaja juz tylko na dispatcher.
        for (var i = 0; i < 40; i++)
            okno.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    /// <summary>Odpowiedz przychodzi DOPIERO po zwolnieniu - symuluje spozniona strone.</summary>
    private sealed class WolnaOdpowiedzHandler : HttpMessageHandler
    {
        private readonly SemaphoreSlim _brama = new(0);
        private int _zapytan;

        internal int Zapytan => Volatile.Read(ref _zapytan);

        internal void Zwolnij() => _brama.Release(16);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _zapytan);
            await _brama.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    Strona([("late1", "Spozniony utwor", "art1")], null),
                    Encoding.UTF8, "application/json")
            };
        }
    }

    private static string Strona((string Id, string Nazwa, string Wykonawca)[] pozycje, string? next)
    {
        var wpisy = string.Join(",", pozycje.Select(p => $$"""
            {"id":"{{p.Id}}","name":"{{p.Nazwa}}","type":"track","duration_ms":210000,
             "album":{"id":"alb1","name":"Album","type":"album"},
             "artists":[{"id":"{{p.Wykonawca}}","name":"Budka Suflera","type":"artist"}]}
            """));
        var nextJson = next is null ? "null" : $"\"{next}\"";
        return "{\"tracks\":{\"href\":\"x\",\"limit\":10,\"offset\":0,\"total\":5,\"next\":"
            + nextJson + ",\"previous\":null,\"items\":[" + wpisy + "]}}";
    }

    /// <summary>
    /// Strona bez pozycji, ale z adresem "next". Pusta strona NIE oznacza konca
    /// katalogu - Spotify tak odpowiada, gdy rynek odsiał całą stronę.
    /// </summary>
    private sealed class ObcyLimitHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"tracks\":{\"href\":\"x\",\"limit\":0,\"offset\":89,\"total\":300,"
                    + "\"next\":\"https://api.spotify.com/v1/search?q=x&type=track&limit=10&offset=99\","
                    + "\"previous\":null,\"items\":[]}}",
                    Encoding.UTF8, "application/json")
            });
    }

    /// <summary>
    /// Atrapa dla PUSTEJ nazwy wykonawcy: najpierw GET /artists/{id} (nazwa),
    /// potem wyszukiwanie katalogu. Rejestruje kolejnosc, zeby test udowodnil,
    /// ze q NIE zawiera surowego identyfikatora.
    /// </summary>
    private sealed class NazwaWykonawcyHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Urls.Add(url);
            var body = url.Contains("/artists/", StringComparison.Ordinal)
                ? "{\"id\":\"art1\",\"name\":\"Budka Suflera\",\"type\":\"artist\"}"
                : "{\"tracks\":{\"href\":\"x\",\"limit\":10,\"offset\":0,\"total\":1,\"next\":null,"
                    + "\"previous\":null,\"items\":[{\"id\":\"t1\",\"name\":\"Jolka, Jolka\",\"type\":\"track\","
                    + "\"duration_ms\":210000,\"album\":{\"id\":\"alb1\",\"name\":\"Album\",\"type\":\"album\"},"
                    + "\"artists\":[{\"id\":\"art1\",\"name\":\"Budka Suflera\",\"type\":\"artist\"}]}]}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class OdmowaHandler(HttpStatusCode kod) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(kod)
            {
                Content = new StringContent(
                    "{\"error\":{\"status\":" + (int)kod + ",\"message\":\"Forbidden\"}}",
                    Encoding.UTF8, "application/json")
            });
    }
}
