using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Playback;
using AccessibleMediaController.Core.Sessions;
using AccessibleMediaController.Core.Spotify;
using AccessibleMediaController.Windows;

/// <summary>
/// ZGLOSZENIE Michala (20.09.2026): Ctrl+Shift+5 z presetem przypisanym do
/// ALBUMU Spotify ("Nieklikalnosc", Maja Kleszcz) nie odtwarzalo albumu.
/// Zamiast tego przenosilo zaznaczenie do albumu w glownym zbiorze i czytnik
/// czytal "9664 z 10776". Preset ma ZAGRAC album i NIE RUSZAC listy.
///
/// Test idzie PRODUKCYJNA droga: prawdziwe okno glowne, prawdziwy router
/// polecen (ExecuteCommand -> CommandIds.RadioPreset -> ActivatePreset),
/// prawdziwy SpotifyApiClient i prawdziwe parsowanie odpowiedzi. Podstawione
/// sa tylko dwa SZWY, ktorych nie da sie miec na maszynie testowej:
/// transport HTTP (atrapa Web API) i tor dzwieku (IMediaOutput). Metoda
/// mierzona NIE jest podstawiona.
///
/// Mierzone jest szesc rzeczy, bo zadna nie dowodzi pozostalych:
/// 1. preset albumu ODTWARZA pierwszy utwor albumu,
/// 2. widok, zaznaczenie listy i pozycja NIE zmieniaja sie,
/// 3. kontekst "nastepny utwor" to utwory albumu w KOLEJNOSCI albumu,
/// 4. dwa presety pod rzad: spozniona odpowiedz pierwszego NIE gra,
/// 5. pusty album i odmowa Spotify nie przerywaja tego, co gra,
/// 6. preset UTWORU i preset PLAYLISTY zachowuja stare zachowanie.
/// </summary>
internal static class SpotifyAlbumPresetTests
{
    private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;

    private const string AlbumId = "5NieklikalnoscAlbum";
    private const string AlbumItemId = "spotify:album:" + AlbumId;

    internal static void Run()
    {
        PresetAlbumuGraBezRuszaniaListy();
        MowaToSamaNazwaAlbumu();
        NiegrywalneUtworyNieWchodzaDoKontekstu();
        AlbumBezGrywalnychUtworowNieNiszczyOdtwarzania();
        PowtorzonyPresetNieRestartujeAlbumu();
        PowtorzonyPresetZPauzyWznawiaBiezacyUtwor();
        InnyKontekstNiePolykaNowegoAlbumu();
        DwaPresetyPodRzadGrajaTylkoNowszy();
        CzekajacyAlbumNiePrzebijaPresetuUtworu();
        CzekajacyAlbumNiePrzebijaPowtorzonegoAlbumu();
        PustyAlbumIOdmowaNiePrzerywajaOdtwarzania();
        PresetUtworuIPlaylistyBezZmian();
        Console.WriteLine(
            "OK: preset albumu Spotify - odtwarzanie pierwszego GRYWALNEGO utworu, sama nazwa albumu w mowie,"
            + " nienaruszony widok/zaznaczenie, kolejnosc albumu w kontekscie, powtorzenie bez restartu i bez sieci,"
            + " wznowienie z pauzy, inny kontekst gra normalnie, spozniona odpowiedz nie przebija presetu utworu"
            + " ani powtorzonego albumu, pusty album i odmowa, niezmienione presety utworu i playlisty");
    }

    // ---------- Mowa: sama nazwa albumu ----------

    /// <summary>
    /// Po udanym wlaczeniu czytnik slyszy SAMA NAZWE ALBUMU. Bez numeru presetu
    /// i bez doklejonego tytulu utworu - tego Michal wprost nie chce.
    /// </summary>
    private static void MowaToSamaNazwaAlbumu()
    {
        WOknie((window, session, output) =>
        {
            Preset(window, session, new AlbumTracksHandler(), slot: 5);
            Check(output.Played.Count == 1, "Preset nie zagral, wiec mowy nie ma co sprawdzac.");
            var powiedziane = Powiedziane(window);
            Check(powiedziane == "Nieklikalność",
                $"Po wlaczeniu albumu czytnik slyszy \"{powiedziane}\", a ma slyszec sama nazwe \"Nieklikalność\".");

            // Powtorzenie przy grajacym albumie: tez sama nazwa.
            Wykonaj(window, 5);
            Drain(window.Dispatcher);
            Thread.Sleep(200);
            Drain(window.Dispatcher);
            var poPowtorzeniu = Powiedziane(window);
            Check(poPowtorzeniu == "Nieklikalność",
                $"Powtorzony preset mowi \"{poPowtorzeniu}\" zamiast samej nazwy albumu.");

            // Wznowienie z pauzy: tez sama nazwa.
            session.TogglePlayback();
            Drain(window.Dispatcher);
            Wykonaj(window, 5);
            Drain(window.Dispatcher);
            Thread.Sleep(200);
            Drain(window.Dispatcher);
            var poWznowieniu = Powiedziane(window);
            Check(poWznowieniu == "Nieklikalność",
                $"Wznowienie z pauzy mowi \"{poWznowieniu}\" zamiast samej nazwy albumu.");
        });
    }

    // ---------- Grywalnosc utworow ----------

    /// <summary>
    /// Niegrywalny PIERWSZY utwor (Spotify: <c>is_playable: false</c>) nie moze
    /// wejsc do kontekstu ani zostac zagrany - tor dzwieku i tak by go odrzucil.
    /// Preset ma zagrac pierwszy GRYWALNY utwor.
    /// </summary>
    private static void NiegrywalneUtworyNieWchodzaDoKontekstu()
    {
        WOknie((window, session, output) =>
        {
            Preset(window, session, new AlbumTracksHandler { Unplayable = [1] }, slot: 5);

            Check(output.Played.Count == 1,
                $"Album z niegrywalnym pierwszym utworem dal {output.Played.Count} wywolan odtwarzania zamiast jednego.");
            Check(output.Played[0].ExternalId == "T2",
                $"Zagral \"{output.Played[0].ExternalId}\" zamiast pierwszego GRYWALNEGO utworu T2.");
            var kontekst = session.PlaybackContextItemIds
                .Select(id => session.Items.FirstOrDefault(item => item.Id == id)?.ExternalId ?? id)
                .ToArray();
            Check(!kontekst.Contains("T1"),
                $"Niegrywalny utwor T1 wszedl do kontekstu odtwarzania: {string.Join(",", kontekst)}.");
            Check(kontekst.SequenceEqual(["T2", "T3"]),
                $"Kontekst to {string.Join(",", kontekst)}, a ma byc T2,T3 w kolejnosci albumu.");
        });
    }

    /// <summary>
    /// Album, w ktorym NIC nie jest grywalne, nie wolno postawic na miejscu
    /// dzialajacego kontekstu: to, co gra, ma grac dalej, a uzytkownik slyszy
    /// powod (z numerem presetu - to diagnostyka).
    /// </summary>
    private static void AlbumBezGrywalnychUtworowNieNiszczyOdtwarzania()
    {
        WOknie((window, session, output) =>
        {
            Preset(window, session, new AlbumTracksHandler(), slot: 5);
            var granyPrzed = output.Played[^1].ExternalId;
            var kontekstPrzed = string.Join(",", session.PlaybackContextItemIds);
            var zagranychPrzed = output.Played.Count;

            Przypisz(window, session, slot: 7, albumId: "7MartwyAlbum", itemId: "spotify:album:7MartwyAlbum");
            Szew(window, new AlbumTracksHandler { Unplayable = [1, 2, 3], TrackPrefix = "MARTWY" });
            Wykonaj(window, 7);
            Drain(window.Dispatcher);
            for (var i = 0; i < 60; i++) { Thread.Sleep(20); Drain(window.Dispatcher); }

            Check(output.Played.Count == zagranychPrzed,
                $"Album bez grywalnych utworow ruszyl odtwarzanie ({output.Played.Count - zagranychPrzed} razy).");
            Check(session.CurrentItem.ExternalId == granyPrzed,
                $"Odtwarzanie przeskoczylo z \"{granyPrzed}\" na \"{session.CurrentItem.ExternalId}\".");
            Check(string.Join(",", session.PlaybackContextItemIds) == kontekstPrzed,
                "Album bez grywalnych utworow zastapil dzialajacy kontekst odtwarzania.");
            Check(Powiedziane(window).Contains("nie zawiera utworów", StringComparison.Ordinal),
                $"Brak grywalnych utworow nie zostal wytlumaczony: \"{Powiedziane(window)}\".");
        });
    }

    // ---------- Powtorzenie tego samego presetu ----------

    /// <summary>
    /// Ten sam preset wcisniety znowu, gdy album GRA - takze gdy zeszlo na
    /// dalszy utwor. Ma powiedziec nazwe i nie ruszyc odtwarzania: zadnego
    /// restartu na pierwszy utwor, zadnego zapytania do Spotify.
    /// </summary>
    private static void PowtorzonyPresetNieRestartujeAlbumu()
    {
        foreach (var naDalszymUtworze in new[] { false, true })
        {
            WOknie((window, session, output) =>
            {
                var handler = new AlbumTracksHandler();
                Preset(window, session, handler, slot: 5);
                Check(output.Played.Count == 1, "Pierwsze uruchomienie presetu nie zagralo.");

                if (naDalszymUtworze)
                {
                    var drugi = session.Items.First(item => item.ExternalId == "T2");
                    session.Play(drugi);
                    Drain(window.Dispatcher);
                }
                var granyPrzed = output.Played[^1].ExternalId;
                var zagranychPrzed = output.Played.Count;
                var zapytanPrzed = handler.Urls.Count;
                var kontekstPrzed = string.Join(",", session.PlaybackContextItemIds);
                var listaPrzed = StanListy(window);

                Wykonaj(window, 5);
                Drain(window.Dispatcher);
                Thread.Sleep(250);
                Drain(window.Dispatcher);

                Check(output.Played.Count == zagranychPrzed,
                    $"Powtorzony preset przestawil odtwarzanie ({output.Played.Count - zagranychPrzed} razy) "
                    + $"- gralo \"{granyPrzed}\", a ma grac dalej bez restartu.");
                Check(session.CurrentItem.ExternalId == granyPrzed,
                    $"Powtorzony preset przeskoczyl z \"{granyPrzed}\" na \"{session.CurrentItem.ExternalId}\".");
                Check(session.IsPlaying, "Powtorzony preset zatrzymal odtwarzanie.");
                Check(handler.Urls.Count == zapytanPrzed,
                    $"Powtorzony preset odpytal Spotify {handler.Urls.Count - zapytanPrzed} raz(y) bez potrzeby.");
                Check(string.Join(",", session.PlaybackContextItemIds) == kontekstPrzed,
                    "Powtorzony preset przebudowal kolejke odtwarzania.");
                Check(StanListy(window) == listaPrzed,
                    $"Powtorzony preset ruszyl liste: przed \"{listaPrzed}\", po \"{StanListy(window)}\".");
            });
        }
    }

    /// <summary>
    /// Ten sam preset przy PAUZIE w tym samym albumie: wznawia BIEZACY utwor od
    /// jego pozycji, a nie pierwszy utwor albumu.
    /// </summary>
    private static void PowtorzonyPresetZPauzyWznawiaBiezacyUtwor()
    {
        WOknie((window, session, output) =>
        {
            var handler = new AlbumTracksHandler();
            Preset(window, session, handler, slot: 5);
            var trzeci = session.Items.First(item => item.ExternalId == "T3");
            session.Play(trzeci);
            session.TogglePlayback();
            Drain(window.Dispatcher);
            Check(session.IsPaused, "Przygotowanie: sesja nie jest na pauzie.");
            var zagranychPrzed = output.Played.Count;
            var zapytanPrzed = handler.Urls.Count;

            Wykonaj(window, 5);
            Drain(window.Dispatcher);
            Thread.Sleep(250);
            Drain(window.Dispatcher);

            Check(output.Played.Count == zagranychPrzed + 1,
                $"Wznowienie z pauzy dalo {output.Played.Count - zagranychPrzed} wywolan odtwarzania zamiast jednego.");
            Check(output.Played[^1].ExternalId == "T3",
                $"Wznowienie zagralo \"{output.Played[^1].ExternalId}\" zamiast BIEZACEGO utworu T3.");
            Check(session.IsPlaying && !session.IsPaused, "Po wznowieniu sesja nie gra.");
            Check(handler.Urls.Count == zapytanPrzed,
                "Wznowienie z pauzy odpytalo Spotify bez potrzeby.");
        });
    }

    /// <summary>
    /// Straz powtorzenia NIE MOZE polknac zamierzonego wlaczenia innego albumu.
    /// Drugi preset (inny album) po zmianie kontekstu musi zagrac normalnie.
    /// </summary>
    private static void InnyKontekstNiePolykaNowegoAlbumu()
    {
        WOknie((window, session, output) =>
        {
            Preset(window, session, new AlbumTracksHandler(), slot: 5);
            Check(output.Played.Count == 1, "Pierwszy album nie zagral.");

            Przypisz(window, session, slot: 6, albumId: "6InnyAlbum", itemId: "spotify:album:6InnyAlbum");
            var inny = new AlbumTracksHandler { TrackPrefix = "INNY" };
            Szew(window, inny);
            Wykonaj(window, 6);
            Drain(window.Dispatcher);
            for (var i = 0; i < 60; i++) { Thread.Sleep(20); Drain(window.Dispatcher); }

            Check(output.Played.Count == 2,
                $"Inny album nie zagral ({output.Played.Count} wywolan) - straz powtorzenia polknela nowy preset.");
            Check(output.Played[^1].ExternalId == "INNY1",
                $"Zagral \"{output.Played[^1].ExternalId}\" zamiast pierwszego utworu INNEGO albumu.");
        });
    }

    // ---------- 1-3. Gra album, lista zostaje, kolejnosc albumu ----------

    private static void PresetAlbumuGraBezRuszaniaListy()
    {
        WOknie((window, session, output) =>
        {
            var listaPrzed = StanListy(window);

            var handler = new AlbumTracksHandler();
            Preset(window, session, handler, slot: 5);

            Check(output.Played.Count == 1,
                $"Preset albumu zagral {output.Played.Count} razy zamiast raz - album ma zagrac, nie otworzyc sie.");
            var zagrany = output.Played[0];
            Check(zagrany.ExternalId == "T1",
                $"Preset zagral \"{zagrany.ExternalId}\" ({zagrany.Title}) zamiast PIERWSZEGO utworu albumu (T1).");
            Check(session.IsPlaying,
                "Sesja nie jest w stanie odtwarzania po presecie albumu.");

            var listaPo = StanListy(window);
            Check(listaPo == listaPrzed,
                $"Preset albumu przestawil liste: przed \"{listaPrzed}\", po \"{listaPo}\". "
                + "Czytnik przeczytalby pozycje w glownym zbiorze (zgloszenie \"9664 z 10776\").");
            Check(Widok(window) == "Multimedia",
                $"Preset albumu zmienil widok na \"{Widok(window)}\" zamiast zostac w widoku uzytkownika.");

            var kontekst = session.PlaybackContextItemIds.ToArray();
            Check(kontekst.Length == 3,
                $"Kontekst odtwarzania ma {kontekst.Length} pozycji zamiast trzech utworow albumu.");
            var kolejnosc = kontekst
                .Select(id => session.Items.FirstOrDefault(item =>
                    string.Equals(item.Id, id, StringComparison.Ordinal))?.ExternalId ?? "<brak>")
                .ToArray();
            Check(kolejnosc is ["T1", "T2", "T3"],
                "Kontekst nie zachowal kolejnosci albumu: " + string.Join(", ", kolejnosc));

            var wSesji = session.Items
                .Where(item => item.ExternalId is "T1" or "T2" or "T3")
                .ToArray();
            Check(wSesji.Length == 3,
                $"Utwory albumu nie weszly do sesji ({wSesji.Length} z 3) - nastepny utwor nie mialby czego grac.");
            Check(wSesji.All(item => !item.IsInLibrary && !item.IsFavorite),
                "Utwory albumu z katalogu udaja pozycje zapisane na koncie - Ctrl+L i Ctrl+U klamalyby.");
            Check(wSesji.All(item => item.RelatedAlbumExternalId == AlbumId),
                "Utwor albumu stracil powiazanie z albumem.");
            Check(handler.Urls.Any(url => url.Contains($"/albums/{AlbumId}/tracks", StringComparison.Ordinal)),
                "Utwory albumu nie poszly udokumentowanym endpointem albumu: "
                + string.Join(", ", handler.Urls));
        });
    }

    // ---------- 4. Dwa presety pod rzad ----------

    private static void DwaPresetyPodRzadGrajaTylkoNowszy()
    {
        WOknie((window, session, output) =>
        {
            // Pierwszy preset czeka na siec; drugi wchodzi natychmiast.
            var wolny = new AlbumTracksHandler { Gate = new TaskCompletionSource(), TrackPrefix = "STARY" };
            var szybki = new AlbumTracksHandler { TrackPrefix = "NOWY" };

            Przypisz(window, session, slot: 5);
            Przypisz(window, session, slot: 6, albumId: "6DrugiAlbum", itemId: "spotify:album:6DrugiAlbum");

            Szew(window, wolny);
            Wykonaj(window, 5);
            Szew(window, szybki);
            Wykonaj(window, 6);
            Drain(window.Dispatcher);

            wolny.Gate!.SetResult();
            for (var i = 0; i < 40 && output.Played.Count < 1; i++)
            {
                Drain(window.Dispatcher);
                Thread.Sleep(25);
            }
            Drain(window.Dispatcher);
            Thread.Sleep(150);
            Drain(window.Dispatcher);

            Check(output.Played.Count == 1,
                $"Dwa presety pod rzad zagraly {output.Played.Count} razy - spozniona odpowiedz musi zostac odrzucona.");
            Check(output.Played[0].ExternalId?.StartsWith("NOWY", StringComparison.Ordinal) == true,
                $"Zagral album ze SPOZNIONEJ odpowiedzi ({output.Played[0].ExternalId}) zamiast z nowszego presetu.");
        });
    }

    /// <summary>
    /// Album A czeka na siec, a uzytkownik wciska preset UTWORU. Spozniona
    /// odpowiedz albumu nie moze przebic utworu: <c>_spotifyNavigationVersion</c>
    /// liczy tylko albumy i nawigacje, wiec bez wlasnego licznika presetu
    /// album wygralby z nowsza decyzja uzytkownika.
    /// </summary>
    private static void CzekajacyAlbumNiePrzebijaPresetuUtworu()
    {
        WOknie((window, session, output) =>
        {
            var wolny = new AlbumTracksHandler { Gate = new TaskCompletionSource(), TrackPrefix = "STARY" };
            Przypisz(window, session, slot: 5);

            // Preset utworu: zwykla pozycja z kolekcji, stara droga ActivatePreset.
            var utwor = session.Items.First(item => item is { Kind: MediaItemKind.Track, IsAvailable: true });
            var entries = Entries(window, session.Id);
            entries.RemoveAll(entry => entry.Slot == 8);
            entries.Add(new SessionPresetEntry
            {
                Slot = 8, TargetId = utwor.Id, TargetKind = "track", TargetTitle = utwor.Title
            });

            Szew(window, wolny);
            Wykonaj(window, 5);
            Drain(window.Dispatcher);
            Wykonaj(window, 8);
            Drain(window.Dispatcher);
            Check(output.Played.Count == 1 && output.Played[0].Id == utwor.Id,
                $"Przygotowanie: preset utworu nie zagral ({output.Played.Count} wywolan).");
            var kontekstPoUtworze = string.Join(",", session.PlaybackContextItemIds);

            wolny.Gate!.SetResult();
            for (var i = 0; i < 60; i++) { Thread.Sleep(25); Drain(window.Dispatcher); }

            Check(output.Played.Count == 1,
                $"Spozniona odpowiedz albumu przebila preset UTWORU ({output.Played.Count} wywolan odtwarzania).");
            Check(session.CurrentItem.Id == utwor.Id,
                $"Po spoznionej odpowiedzi gra \"{session.CurrentItem.ExternalId}\" zamiast utworu z presetu.");
            Check(string.Join(",", session.PlaybackContextItemIds) == kontekstPoUtworze,
                "Spozniona odpowiedz albumu przebudowala kontekst ustawiony presetem utworu.");
        });
    }

    /// <summary>
    /// Album B czeka na siec, a uzytkownik wciska POWTORZONY preset grajacego
    /// albumu A. Powtorzenie wraca wczesnie, wiec licznik trzeba podbic PRZED
    /// tym wyjsciem - inaczej odpowiedz B przestawi grajacy album A.
    /// </summary>
    private static void CzekajacyAlbumNiePrzebijaPowtorzonegoAlbumu()
    {
        WOknie((window, session, output) =>
        {
            // A gra.
            Preset(window, session, new AlbumTracksHandler(), slot: 5);
            Check(output.Played.Count == 1, "Przygotowanie: album A nie zagral.");
            var granyPrzed = output.Played[0].ExternalId;
            var kontekstPrzed = string.Join(",", session.PlaybackContextItemIds);

            // B rusza i wisi na sieci.
            var wolny = new AlbumTracksHandler { Gate = new TaskCompletionSource(), TrackPrefix = "POZNY" };
            Przypisz(window, session, slot: 6, albumId: "6DrugiAlbum", itemId: "spotify:album:6DrugiAlbum");
            Szew(window, wolny);
            Wykonaj(window, 6);
            Drain(window.Dispatcher);

            // Powtorzony preset grajacego albumu A - wczesne wyjscie.
            Wykonaj(window, 5);
            Drain(window.Dispatcher);

            wolny.Gate!.SetResult();
            for (var i = 0; i < 60; i++) { Thread.Sleep(25); Drain(window.Dispatcher); }

            Check(output.Played.Count == 1,
                $"Czekajacy album B przebil POWTORZONY album A ({output.Played.Count} wywolan odtwarzania).");
            Check(session.CurrentItem.ExternalId == granyPrzed,
                $"Po odpowiedzi B gra \"{session.CurrentItem.ExternalId}\" zamiast dalej albumu A ({granyPrzed}).");
            Check(string.Join(",", session.PlaybackContextItemIds) == kontekstPrzed,
                "Czekajacy album B przebudowal kontekst grajacego albumu A.");
        });
    }

    // ---------- 5. Pusty album i odmowa ----------

    private static void PustyAlbumIOdmowaNiePrzerywajaOdtwarzania()
    {
        foreach (var pusty in new[] { true, false })
        {
            WOknie((window, session, output) =>
            {
                // Cos JUZ gra: preset, ktory nie ma czego zagrac, nie moze tego przerwac.
                var grajacy = session.Items.First(item => item.Kind == MediaItemKind.Track);
                session.Play(grajacy);
                var zagranychPrzed = output.Played.Count;
                var zatrzymanPrzed = output.Stopped;

                Preset(
                    window,
                    session,
                    pusty ? new AlbumTracksHandler { Empty = true } : new OdmowaHandler(),
                    slot: 5);

                Check(output.Played.Count == zagranychPrzed,
                    (pusty ? "Pusty album" : "Odmowa Spotify")
                    + $" uruchomila odtwarzanie ({output.Played.Count - zagranychPrzed} razy).");
                Check(output.Stopped == zatrzymanPrzed,
                    (pusty ? "Pusty album" : "Odmowa Spotify") + " przerwala to, co gralo.");
                Check(session.CurrentItem.Id == grajacy.Id,
                    "Nieudany preset albumu przestawil biezaca pozycje sesji.");
            });
        }
    }

    // ---------- 6. Utwor i playlista bez zmian ----------

    private static void PresetUtworuIPlaylistyBezZmian()
    {
        WOknie((window, session, output) =>
        {
            var utwor = session.Items.First(item => item.Kind == MediaItemKind.Track);
            Entries(window, session.Id).Clear();
            Entries(window, session.Id).Add(new SessionPresetEntry
            {
                Slot = 4, TargetId = utwor.Id, TargetKind = "track", TargetTitle = utwor.Title
            });
            Wykonaj(window, 4);
            Drain(window.Dispatcher);
            Check(output.Played.Count == 1 && output.Played[0].Id == utwor.Id,
                "Preset UTWORU Spotify przestal grac tak jak dotad.");
        });

        WOknie((window, session, _) =>
        {
            var playlista = new MediaItem
            {
                Id = "spotify:playlist:P1", ExternalId = "P1", Kind = MediaItemKind.Playlist,
                Title = "Playlista testowa", Source = "spotify:playlist:P1"
            };
            session.AddItemsById([playlista]);
            Entries(window, session.Id).Clear();
            Entries(window, session.Id).Add(new SessionPresetEntry
            {
                Slot = 4, TargetId = playlista.Id, TargetKind = "playlist", TargetTitle = playlista.Title
            });
            var widokPrzed = Widok(window);
            Wykonaj(window, 4);
            Drain(window.Dispatcher);
            // Playlista zostaje na starej, NAWIGACYJNEJ drodze (SelectSessionBrowserItem
            // + NavigateTo na tytul): tego zgloszenie nie dotyczylo, wiec zmiana
            // bylaby niezamowiona. Mierzymy, ze droga jest ta SAMA co dotad.
            Check(Widok(window) == playlista.Title,
                $"Preset playlisty przestal otwierac playliste: widok \"{Widok(window)}\", byl \"{widokPrzed}\".");
        });
    }

    // ---------- Narzedzia ----------

    private static void Preset(
        MainWindow window, DemoMediaSession session, HttpMessageHandler handler, int slot)
    {
        Przypisz(window, session, slot);
        Szew(window, handler);
        Wykonaj(window, slot);
        Drain(window.Dispatcher);
        for (var i = 0; i < 60; i++)
        {
            Thread.Sleep(20);
            Drain(window.Dispatcher);
        }
    }

    private static void Przypisz(
        MainWindow window,
        DemoMediaSession session,
        int slot,
        string albumId = AlbumId,
        string itemId = AlbumItemId)
    {
        var album = new MediaItem
        {
            Id = itemId, ExternalId = albumId, Kind = MediaItemKind.Album,
            Title = "Nieklikalność", Artist = "Maja Kleszcz",
            Source = $"spotify:album:{albumId}"
        };
        session.AddItemsById([album]);
        var entries = Entries(window, session.Id);
        entries.RemoveAll(entry => entry.Slot == slot);
        entries.Add(new SessionPresetEntry
        {
            Slot = slot, TargetId = album.Id, TargetKind = "album", TargetTitle = album.Title
        });
    }

    private static List<SessionPresetEntry> Entries(MainWindow window, string sessionId) =>
        (List<SessionPresetEntry>)typeof(MainWindow)
            .GetMethod("SessionPresetEntries", Flags)!
            .Invoke(window, [sessionId])!;

    /// <summary>
    /// Podstawia transport HTTP i token. Klient Spotify, parsowanie i cala droga
    /// pobierania zostaja produkcyjne.
    /// </summary>
    private static void Szew(MainWindow window, HttpMessageHandler handler)
    {
        window.SpotifyApiHttpClientFactoryForTests =
            () => new HttpClient(handler, disposeHandler: false);
        window.SpotifyAccessTokenForTests = () => "token-testowy";
    }

    /// <summary>
    /// PRAWDZIWA droga skrotu Ctrl+Shift+cyfra: router polecen aplikacji.
    /// </summary>
    private static void Wykonaj(MainWindow window, int slot) =>
        typeof(MainWindow).GetMethod("ExecuteCommand", Flags, null, [typeof(string)], null)!
            .Invoke(window, [AccessibleMediaController.Core.Commands.CommandIds.RadioPreset(slot)]);

    private static string Widok(MainWindow window) =>
        (string)typeof(MainWindow).GetField("_currentView", Flags)!.GetValue(window)!;

    /// <summary>
    /// Ostatni komunikat dla czytnika. Mowa jest wylaczona, wiec produkcyjny
    /// <c>Announce</c> wpisuje go do paska stanu.
    /// </summary>
    private static string Powiedziane(MainWindow window) =>
        ((TextBlock)typeof(MainWindow)
            .GetField("StatusText", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(window)!).Text;

    /// <summary>
    /// Odcisk stanu listy: widok, indeks, zaznaczona pozycja i liczba wierszy.
    /// </summary>
    private static string StanListy(MainWindow window)
    {
        var list = (ListBox)typeof(MainWindow)
            .GetField("MediaList", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!
            .GetValue(window)!;
        return $"{Widok(window)}|{list.SelectedIndex}|{window.SelectedItem?.Id ?? "<brak>"}|{list.Items.Count}";
    }

    private static void Drain(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(() => frame.Continue = false, DispatcherPriority.ApplicationIdle);
        Dispatcher.PushFrame(frame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    /// <summary>
    /// Prawdziwe okno glowne w watku STA z podstawionym torem dzwieku dla sesji
    /// Spotify. Bez ShowDialog, bez konta, bez audio.
    /// </summary>
    private static void WOknie(Action<MainWindow, DemoMediaSession, FakeOutput> sprawdz)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-spotify-preset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Settings.Updates.CheckAutomatically = false;
                // Mowa wylaczona kieruje komunikat do paska stanu, wiec test
                // czyta DOKLADNIE to, co uslyszalby czytnik ekranu.
                state.Settings.Messages.Enabled = false;
                state.Settings.SpotifyEngine = SpotifyPlaybackEngine.Librespot;
                state.Settings.SpotifySessionUnificationVersion = 1;
                state.Settings.LastSessionId = "spotify";
                foreach (var numer in new[] { 1, 2 })
                {
                    state.Spotify.CachedCollectionItems.Add(
                        TidalCachedCollectionItemSettings.FromMediaItem(new MediaItem
                        {
                            Id = $"spotify:track:ZBIOR{numer}", ExternalId = $"ZBIOR{numer}",
                            Kind = MediaItemKind.Track, Title = $"Utwór zbioru {numer}",
                            Source = $"spotify:track:ZBIOR{numer}", IsInLibrary = true
                        }));
                }
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                store.Save(state);
                state = store.LoadOrCreate();
                window = new MainWindow(state, store);
                var sessions = (SessionManager)typeof(MainWindow)
                    .GetField("_sessions", Flags)!.GetValue(window)!;
                var session = sessions.FindSession("spotify")
                    ?? throw new Exception("Brak kanonicznej sesji Spotify.");
                sessions.SelectSession("spotify");
                var output = new FakeOutput();
                typeof(DemoMediaSession)
                    .GetField("_output", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(session, output);
                Drain(window.Dispatcher);
                sprawdz(window, session, output);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                try { window?.Close(); } catch (Exception) { }
                // Ten watek STA ma wlasny dyspozytor. Bez jawnego zamkniecia
                // zostaje po nim ustawiony kontekst synchronizacji WPF, a
                // nastepny test w tym samym procesie (WinForms) probuje przez
                // niego wolac BeginInvoke na oknie bez uchwytu i sypie sie
                // przy sprzataniu. Zamykamy wiec dyspozytor tego watku.
                try { Dispatcher.CurrentDispatcher.InvokeShutdown(); } catch (Exception) { }
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(90)))
            throw new Exception("Preset albumu Spotify: test nie zakonczyl sie w 90 s.");
        if (failure is not null) throw new Exception("Preset albumu Spotify.", failure);
    }

    /// <summary>Tor dzwieku bez dzwieku: zapisuje, co dostal do zagrania.</summary>
    private sealed class FakeOutput : IMediaOutput
    {
        internal List<MediaItem> Played { get; } = [];
        internal int Stopped { get; private set; }

        public string? LoadedItemId { get; private set; }
        public TimeSpan Position => TimeSpan.Zero;
        public bool SupportsPlaybackRate => false;

        public void Play(MediaItem item, TimeSpan position, int volume, double playbackRate)
        {
            Played.Add(item);
            LoadedItemId = item.Id;
        }

        public void Pause() { }
        public void Stop() => Stopped++;
        public void Seek(TimeSpan position) { }
        public void SetVolume(int volume) { }
        public void SetPlaybackRate(double playbackRate) { }
    }

    /// <summary>
    /// Atrapa Web API Spotify: naglowek albumu i JEDNA strona utworow w
    /// kolejnosci albumu.
    /// </summary>
    private sealed class AlbumTracksHandler : HttpMessageHandler
    {
        internal List<string> Urls { get; } = [];
        internal bool Empty { get; init; }
        internal string TrackPrefix { get; init; } = "T";
        /// <summary>
        /// Brama sieci. UWAGA: pobranie albumu to DWA zapytania (nagłowek i
        /// strona utworow), wiec brama musi przepuscic OBA - semafor z jednym
        /// pozwoleniem zawiesilby odpowiedz na zawsze i test przechodzilby z
        /// niewlasciwego powodu.
        /// </summary>
        internal TaskCompletionSource? Gate { get; init; }
        /// <summary>Numery utworow, ktore Spotify oznacza jako niegrywalne.</summary>
        internal int[] Unplayable { get; init; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            lock (Urls) Urls.Add(url);
            if (Gate is not null)
                await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var json = url.Contains("/tracks", StringComparison.Ordinal)
                ? Empty
                    ? "{\"items\":[],\"next\":null}"
                    : "{\"items\":[" + string.Join(",", new[] { 1, 2, 3 }.Select(numer =>
                        "{\"id\":\"" + TrackPrefix + numer + "\",\"name\":\"Utwór " + numer
                        + "\",\"duration_ms\":180000,\"track_number\":" + numer
                        + (Unplayable.Contains(numer) ? ",\"is_playable\":false" : string.Empty)
                        + ",\"artists\":[{\"id\":\"A1\",\"name\":\"Maja Kleszcz\"}]}"))
                        + "],\"next\":null}"
                : "{\"id\":\"" + AlbumId + "\",\"name\":\"Nieklikalność\","
                    + "\"artists\":[{\"id\":\"A1\",\"name\":\"Maja Kleszcz\"}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>Spotify odmawia zawartosci albumu (np. rynek/prawa).</summary>
    private sealed class OdmowaHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":{\"status\":404}}", Encoding.UTF8, "application/json")
            });
    }
}
