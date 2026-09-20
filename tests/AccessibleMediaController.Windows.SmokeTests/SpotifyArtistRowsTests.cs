using System.Collections;
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
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;
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
            + " (wyszukiwanie z dopasowaniem ID i stronicowaniem), nazwy widokow obu sesji,"
            + " klawiatura, powrot i guard spoznionej odpowiedzi");
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
        Check(parametry.Length == 5,
            $"GetArtistTracksAsync ma {parametry.Length} parametrow zamiast pieciu "
            + "(token, rynek, identyfikator wykonawcy, nazwa wykonawcy, anulowanie).");

        using var handler = new ArtistTracksHandler();
        using var http = new HttpClient(handler);
        using var api = new SpotifyApiClient(http);
        var utwory = await Wywolaj(metoda, api, "token", "PL", "art1", "Budka Suflera");

        Check(utwory is not null, "Utwory wykonawcy wrocily jako brak dostepu zamiast listy.");
        Check(handler.Urls.Count == 2,
            $"Stronicowanie wyslalo {handler.Urls.Count} zapytan zamiast dwoch (pierwsza strona + next).");

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
        Check(handler.Urls[1] == NextUrl,
            $"Druga strona nie poszla adresem \"next\" od Spotify: {handler.Urls[1]}");

        var lista = utwory!;
        var tytuly = lista.Select(item => item.Title).ToArray();
        Check(lista.Count == 4,
            $"Pobrano {lista.Count} utworow zamiast czterech: {string.Join(", ", tytuly)}");
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

        using var odmowa = new OdmowaHandler(HttpStatusCode.Forbidden);
        using var http403 = new HttpClient(odmowa);
        using var api403 = new SpotifyApiClient(http403);
        Check(await Wywolaj(metoda, api403, "token", "PL", "art1", "Budka Suflera") is null,
            "Odmowa Spotify musi wrocic jako brak danych, nie jako pusta lista udajaca brak utworow.");
    }

    private static async Task<IReadOnlyList<MediaItem>?> Wywolaj(
        MethodInfo metoda, SpotifyApiClient api, string token, string rynek, string id, string nazwa)
    {
        var task = (Task)metoda.Invoke(api, [token, rynek, id, nazwa, CancellationToken.None])!;
        await task.ConfigureAwait(false);
        return (IReadOnlyList<MediaItem>?)task.GetType().GetProperty("Result")!.GetValue(task);
    }

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

    private const string NextUrl =
        "https://api.spotify.com/v1/search?q=artist%3A%22Budka%20Suflera%22&type=track&market=PL&limit=10&offset=10";

    /// <summary>
    /// Atrapa wyszukiwania Spotify: oddaje DWIE strony, a w pierwszej celowo
    /// podrzuca utwor innego wykonawcy o mylacej nazwie.
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
            var pierwsza = Urls.Count == 1;
            var body = pierwsza
                ? Strona(
                    [("t1", "Jolka, Jolka", "art1"), ("t2", "Takie tango", "art1"), ("t9", "Cudzy utwor", "inny")],
                    NextUrl)
                : Strona([("t3", "Sen o dolinie", "art1"), ("t4", "Nocny kochanek", "art1")], null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
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
