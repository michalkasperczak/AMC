using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// WASKI POMIAR POZOSTALEGO TRANSPORTU I PRESETOW SONOSA PO 4.1.7.
///
/// CZEGO NIE OBEJMOWAL ISTNIEJACY POMIAR (SonosAfter417ThreePartTests, czesc 2 i 3):
///   * dotykal WYLACZNIE Ulubionych - Playlisty i Moje stacje przechodza przez TEN
///     SAM router <c>SonosSublistSessionSwitch.TryHandleTransportAndPresets</c> bez
///     zadnego pomiaru;
///   * liczyl "dowolny POST ze slowem playback" i "dowolny POST po Enterze" - czyli
///     nie rozpoznawalby ani zlego polecenia, ani zlego identyfikatora, ani
///     drugiego zadania;
///   * preset sprawdzal po FRAGMENCIE adresu, bez ciala i bez liczby zadan;
///     w nowym pomiarze wymagamy identyfikatora innego niz zaznaczony wiersz,
///   * Spacja NA PRZYCISKU nie byla mierzona w ogole, a <c>TryHandleTransportAndPresets</c>
///     oszczedza tylko <c>TextBoxBase</c>/<c>PasswordBox</c>.
///
/// CO TU JEST MIERZONE (na KAZDEJ z TRZECH prawdziwych podlist modalnych):
///   T1  Spacja na LISCIE: DOKLADNIE JEDEN POST /groups/{grupa}/playback/togglePlayPause,
///       po nim JAWNY GET /groups/{grupa}/playback i zgodny stan; ZERO POST-ow
///       ladowania materialu. Druga Spacja nad PAUSED/canPlay znow idzie
///       transportem (PLAY), a NIE ponownym ladowaniem wiersza.
///   T2  Enter: KONKRETNY identyfikator wybranego wiersza w ciele, DOKLADNA liczba
///       zadan wedlug kontraktu danej listy, zaznaczenie i fokus bez zmian,
///       ZERO POST transportu (Enter nie jest toggle).
///   T3  Spacja NA PRZYCISKU: router NIE MA prawa zabrac gestu (Handled=false) i NIE
///       MA prawa wyslac transportu; natywne aktywowanie przycisku Zamknij zwija
///       liste przy ZEROWYM POST transportu.
///   T4  Ctrl+Shift+3 / 0 / minus / equals WEDLUG <c>RadioPresetKeyMap</c>: DOKLADNIE
///       JEDEN POST pod adres i z cialem WLASCIWEGO MATERIALU PRESETU (INNEGO niz
///       zaznaczony wiersz), bez zmiany wiersza i fokusu; dwa gesty pod rzad oraz
///       okno BEZ wlasciciela nie daja drugiej wysylki.
///
/// UCZCIWOSC APARATURY:
///   * To SYNTETYCZNE WPF: <c>PreviewKeyDown</c> na realnym zrodle prezentacji z
///     PRAWDZIWIE wcisnietymi modyfikatorami. WPF NIE wykonuje z tego ani
///     <c>Button.Click</c> (to robi KeyUp/przestrzen klawisza), ani autorepetycji -
///     dlatego T3 rozdziela WYNIK ROUTERA od RZECZYWISTEGO aktywowania przycisku
///     (<c>ButtonAutomationPeer.Invoke</c>), a T4 NIE udaje <c>e.IsRepeat</c>, tylko
///     mierzy DWA osobne gesty. Fizyczna klawiatura i odczyt NVDA ida osobno.
///   * Zadnej zmiany produktu: bramka polecen jest SPELNIANA (odczyt stanu z
///     canPause/canPlay), nie omijana.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    /// <summary>
    /// KTORE czesci mierzyc. Zawezenie sluzy ODTWORZENIU kazdego punktu OSOBNO -
    /// pierwszy czerwony nie ma przykryc pozostalych.
    /// </summary>
    internal static string[] After417TransportPartsToMeasure { get; set; } = ["T1", "T2", "T3", "T4"];

    internal static void RunAfter417TransportPresets()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { MeasureAfter417TransportPresets(); }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(900)))
        {
            throw new Exception("Limit wąskiego pomiaru transportu i presetów po 4.1.7");
        }

        if (failure is not null) throw failure;

        // PODSUMOWANIE MOWI, CO ZMIERZONO - nie wszystkie punkty, tylko wybrane.
        var measured = string.Join(", ", After417TransportPartsToMeasure);
        Console.WriteLine($"({After417TransportPartsToMeasure.Length} zmierzonych części: {measured};"
            + $" {TransportSublistsMeasuredForReport} przebiegów podlist)");
        foreach (var part in After417TransportPartsToMeasure)
        {
            Console.WriteLine("OK: " + part + ": " + DescribeTransportPart(part));
        }
    }

    /// <summary>Ile PRZEBIEGOW PODLIST naprawde poszlo - liczone z listy, nie z glowy.</summary>
    private static int TransportSublistsMeasuredForReport;

    private static string DescribeTransportPart(string part) => part switch
    {
        "T1" => "Spacja na liście = 1 POST playback/togglePlayPause + GET playback, "
            + "druga Spacja nad PAUSED/canPlay znów transportem, zero POST ładowania "
            + "(Ulubione, Playlisty, Moje stacje)",
        "T2" => "Enter = dokładne ID wybranego wiersza i dokładna liczba żądań wg kontraktu listy, "
            + "fokus i zaznaczenie bez zmian, zero POST transportu",
        "T3" => "Spacja na przycisku Zamknij: router nie przechwytuje (Handled=false) i nie wysyła "
            + "transportu; natywne aktywowanie przycisku zwija listę przy zerowym POST transportu",
        "T4" => "Ctrl+Shift+3/0/minus/equals wg RadioPresetKeyMap = 1 POST materiału presetu "
            + "(INNEGO niż zaznaczony wiersz) z właściwym ID w ciele, bez zmiany wiersza i fokusu; "
            + "dwa gesty i okno bez właściciela nie dają drugiej wysyłki",
        _ => throw new Exception("Nieznana część wąskiego pomiaru: " + part)
    };

    private static void MeasureAfter417TransportPresets()
    {
        TransportSublistsMeasuredForReport = 0;
        foreach (var part in After417TransportPartsToMeasure)
        {
            switch (part)
            {
                case "T1": MeasureSpaceTransportOnAllThreeLists(); break;
                case "T2": MeasureEnterStartsExactRowOnAllThreeLists(); break;
                case "T3": MeasureSpaceOnButtonDoesNotStealActivation(); break;
                case "T4": MeasurePresetShortcutsOnAllThreeLists(); break;
                default: throw new Exception("Nieznana część wąskiego pomiaru: " + part);
            }

            Console.Error.WriteLine("CZESC " + part + ": zmierzona - OK");
        }
    }

    // ==================== OPIS JEDNEJ PODLISTY DLA WASKIEGO POMIARU ====================

    /// <summary>
    /// JEDNA podlista opisana KONTRAKTEM, ktory ma byc zmierzony. Nie ma tu
    /// zadnej nowej architektury: <c>Run</c> to ISTNIEJACE uruchomienie modalu
    /// (<c>RunFavoritesModal</c>, <c>RunPlaylistsModal</c>, <c>RunOwnStreamsModal</c>).
    /// </summary>
    private sealed record TransportSublist(
        string Label,
        string ListName,
        Action<RealHarness, Action<Window>> Run,
        Func<Window, string?> SelectedRowId,
        string SecondRowId,
        /// <summary>DOKLADNE adresy POST, jakich wymaga Enter na tej liscie - w kolejnosci.</summary>
        string[] ExpectedEnterPostPaths,
        /// <summary>Pole ciala pierwszego POST Entera i oczekiwana wartosc.</summary>
        string EnterBodyField,
        /// <summary>Material PRESETU: INNY niz zaznaczony wiersz, zeby pomiar nie byl pusty.</summary>
        string PresetKind,
        string PresetTargetId,
        string PresetPostPathSuffix,
        string PresetBodyField);

    private static TransportSublist[] DescribeTransportSublists() =>
    [
        new(
            "Ulubione",
            "FavoritesList",
            (h, steps) => h.RunFavoritesModal(dialog => steps(dialog)),
            dialog => ((SonosFavoritesWindow)dialog).SelectedFavoriteForTests?.Id,
            "ULU-DRUGI",
            ["/groups/" + GroupId + "/favorites"],
            "favoriteId",
            // PRESET to PLAYLISTA, a zaznaczony wiersz to ULUBIONE - gdyby gest
            // uruchomil po prostu wiersz, adres i cialo BYLYBY INNE.
            SonosPresetKinds.Playlist,
            "LISTA-PIERWSZA",
            "/groups/" + GroupId + "/playlists",
            "playlistId"),
        new(
            "Playlisty",
            "PlaylistsList",
            (h, steps) => h.RunPlaylistsModal(dialog => steps(dialog)),
            dialog => ((SonosPlaylistsWindow)dialog).HighlightedPlaylistIdForTests,
            "LISTA-DRUGA",
            ["/groups/" + GroupId + "/playlists"],
            "playlistId",
            // PRESET to ULUBIONE, a wiersz to PLAYLISTA - znow INNY material.
            SonosPresetKinds.Favorite,
            "ULU-PIERWSZY",
            "/groups/" + GroupId + "/favorites",
            "favoriteId"),
        new(
            "Moje stacje",
            "StationsList",
            (h, steps) => h.RunOwnStreamsModal(dialog => steps(dialog)),
            dialog => ((SonosOwnStreamsWindow)dialog).HighlightedStationIdForTests,
            "station-two",
            // WLASNA STACJA to DWA zadania wedlug kontraktu Control API: utworzenie
            // sesji odtwarzania i dopiero potem adres strumienia. Liczba 2 jest
            // CZESCIA kontraktu tej listy, nie wyjatkiem dla wygody pomiaru.
            [
                "/groups/" + GroupId + "/playbackSession",
                "/playbackSessions/" + TransportSessionId + "/playbackSession/loadStreamUrl"
            ],
            "streamUrl",
            SonosPresetKinds.Favorite,
            "ULU-PIERWSZY",
            "/groups/" + GroupId + "/favorites",
            "favoriteId")
    ];

    private const string TransportSessionId = "SESJA-TRANSPORTU-1";
    private const string SecondStationUrl = "https://stacja.example.invalid/druga";

    /// <summary>Adres POST transportu Spacji - JEDEN konkretny, nie "cokolwiek z playback".</summary>
    private static string TogglePath => "/groups/" + GroupId + "/playback/togglePlayPause";

    private static string PlaybackReadPath => "/groups/" + GroupId + "/playback";

    // ==================== T1: SPACJA NA LISCIE ====================

    /// <summary>
    /// SPACJA NA LISCIE kazdej z TRZECH podlist: DOKLADNIE JEDEN POST
    /// <c>playback/togglePlayPause</c> dla AKTYWNEJ grupy, po nim JAWNY odczyt
    /// stanu, ZERO POST-ow ladowania materialu, a stan po odczycie ZGODNY z
    /// odpowiedzia chmury. Druga Spacja nad PAUSED/canPlay znowu idzie
    /// TRANSPORTEM - nie wolno jej zamienic w ponowne ladowanie wiersza.
    /// </summary>
    private static void MeasureSpaceTransportOnAllThreeLists()
    {
        foreach (var sublist in DescribeTransportSublists())
        {
            using var h = CreateTransportHarness(out var cloud);
            h.Enter();
            PrimeTransportState(h, cloud);

            var firstToggles = Array.Empty<string>();
            var firstReads = 0;
            var loadPosts = Array.Empty<string>();
            var secondToggles = Array.Empty<string>();
            var secondLoadPosts = Array.Empty<string>();
            var stateAfterFirst = SonosPlaybackState.Unknown;
            var rowKept = false;
            var listAlive = false;
            var focusInList = false;

            sublist.Run(h, dialog =>
            {
                var list = SelectSecondRowForTransport(h, dialog, sublist);
                var rowBefore = sublist.SelectedRowId(dialog);

                // --- PIERWSZA SPACJA: PAUZA GRAJACEGO MATERIALU ---
                var requestsBefore = h.Handler.Requests.Count;
                var postsBefore = h.Handler.Posts.Count;
                cloud.NextStateAfterToggle = TransportCloudState.PausedCanPlay;
                SendKeyWithModifiers(dialog, Key.Space, ModifierKeys.None);
                h.PumpQuietly(TimeSpan.FromMilliseconds(900));

                var newPosts = h.Handler.Posts.Skip(postsBefore).ToArray();
                firstToggles = newPosts
                    .Where(p => p.Uri.AbsolutePath.EndsWith(TogglePath, StringComparison.Ordinal))
                    .Select(p => p.Uri.AbsolutePath)
                    .ToArray();
                loadPosts = newPosts
                    .Where(p => !p.Uri.AbsolutePath.EndsWith(TogglePath, StringComparison.Ordinal))
                    .Select(p => p.Uri.AbsolutePath)
                    .ToArray();
                // POTWIERDZAJACY ODCZYT: "Accepted" nie znaczy "zagralo".
                firstReads = h.Handler.Requests.Skip(requestsBefore)
                    .Count(w => w.Method == "GET"
                        && w.Uri.AbsolutePath.EndsWith(PlaybackReadPath, StringComparison.Ordinal));
                stateAfterFirst = h.Window.SonosPlaybackForTests?.PlaybackState ?? SonosPlaybackState.Unknown;

                rowKept = string.Equals(sublist.SelectedRowId(dialog), rowBefore, StringComparison.Ordinal);
                listAlive = dialog.IsVisible;
                focusInList = list.IsKeyboardFocusWithin;

                // --- DRUGA SPACJA: WZNOWIENIE, A NIE PONOWNE LADOWANIE ---
                var postsBeforeSecond = h.Handler.Posts.Count;
                cloud.NextStateAfterToggle = TransportCloudState.PlayingCanPause;
                SendKeyWithModifiers(dialog, Key.Space, ModifierKeys.None);
                h.PumpQuietly(TimeSpan.FromMilliseconds(900));
                var second = h.Handler.Posts.Skip(postsBeforeSecond).ToArray();
                secondToggles = second
                    .Where(p => p.Uri.AbsolutePath.EndsWith(TogglePath, StringComparison.Ordinal))
                    .Select(p => p.Uri.AbsolutePath)
                    .ToArray();
                secondLoadPosts = second
                    .Where(p => !p.Uri.AbsolutePath.EndsWith(TogglePath, StringComparison.Ordinal))
                    .Select(p => p.Uri.AbsolutePath)
                    .ToArray();
            });

            TransportSublistsMeasuredForReport++;
            var where = "[" + sublist.Label + "] ";

            if (firstToggles.Length == 0)
            {
                throw new Exception(where + "ZGLOSZONY BLAD ODTWORZONY: Spacja na liście NIE wysłała "
                    + "polecenia transportu (zero POST " + TogglePath + "). Zapowiedzi: "
                    + string.Join(" | ", h.Announcements.TakeLast(5)));
            }

            if (firstToggles.Length != 1)
            {
                throw new Exception(where + $"Spacja wysłała {firstToggles.Length} POST transportu "
                    + "zamiast dokładnie jednego: " + string.Join(" | ", firstToggles));
            }

            if (loadPosts.Length != 0)
            {
                throw new Exception(where + "Spacja wysłała POST ładowania materiału - transport "
                    + "bieżącego materiału nie ma prawa niczego wczytywać: "
                    + string.Join(" | ", loadPosts));
            }

            if (firstReads == 0)
            {
                throw new Exception(where + "Po POST transportu NIE poszedł jawny GET "
                    + PlaybackReadPath + " - bez odczytu nie ma potwierdzenia skutku.");
            }

            if (stateAfterFirst != SonosPlaybackState.Paused)
            {
                throw new Exception(where + "Stan po Spacji to " + stateAfterFirst
                    + ", a chmura odpowiedziała PLAYBACK_STATE_PAUSED - odczyt nie trafił do stanu okna.");
            }

            if (!rowKept) throw new Exception(where + "Spacja zmieniła zaznaczony wiersz podlisty.");
            if (!listAlive) throw new Exception(where + "Spacja zamknęła podlistę.");
            if (!focusInList) throw new Exception(where + "Spacja zabrała fokus z listy.");

            if (secondToggles.Length != 1)
            {
                throw new Exception(where + $"Druga Spacja nad PAUSED/canPlay dała {secondToggles.Length} "
                    + "POST transportu zamiast dokładnie jednego (wznowienie). Zapowiedzi: "
                    + string.Join(" | ", h.Announcements.TakeLast(5)));
            }

            if (secondLoadPosts.Length != 0)
            {
                throw new Exception(where + "Druga Spacja zamieniła się w PONOWNE ŁADOWANIE materiału: "
                    + string.Join(" | ", secondLoadPosts));
            }
        }
    }

    // ==================== T2: ENTER URUCHAMIA KONKRETNY WIERSZ ====================

    /// <summary>
    /// ENTER na kazdej z TRZECH list: DOKLADNA liczba zadan wedlug kontraktu tej
    /// listy, KONKRETNY identyfikator wybranego wiersza w ciele, ZERO POST
    /// transportu (Enter nie staje sie toggle) i zachowane zaznaczenie z fokusem.
    /// </summary>
    private static void MeasureEnterStartsExactRowOnAllThreeLists()
    {
        foreach (var sublist in DescribeTransportSublists())
        {
            using var h = CreateTransportHarness(out var cloud);
            h.Enter();
            PrimeTransportState(h, cloud);

            var paths = Array.Empty<string>();
            var bodies = Array.Empty<string>();
            var rowKept = false;
            var focusInList = false;

            sublist.Run(h, dialog =>
            {
                var list = SelectSecondRowForTransport(h, dialog, sublist);
                var rowBefore = sublist.SelectedRowId(dialog);

                var postsBefore = h.Handler.Posts.Count;
                SendKeyWithModifiers(dialog, Key.Enter, ModifierKeys.None);
                h.PumpQuietly(TimeSpan.FromMilliseconds(200));
                if (LastPlayTaskOf(dialog) is { } play) h.Pump(play);
                h.PumpQuietly(TimeSpan.FromMilliseconds(400));

                var newPosts = h.Handler.Posts.Skip(postsBefore).ToArray();
                paths = newPosts.Select(p => p.Uri.AbsolutePath).ToArray();
                bodies = newPosts.Select(p => p.Body).ToArray();
                rowKept = string.Equals(sublist.SelectedRowId(dialog), rowBefore, StringComparison.Ordinal);
                focusInList = list.IsKeyboardFocusWithin;
            });

            TransportSublistsMeasuredForReport++;
            var where = "[" + sublist.Label + "] ";

            if (paths.Length != sublist.ExpectedEnterPostPaths.Length)
            {
                throw new Exception(where + $"Enter wysłał {paths.Length} POST zamiast "
                    + $"{sublist.ExpectedEnterPostPaths.Length} wymaganych kontraktem tej listy. "
                    + "Adresy: " + string.Join(" | ", paths)
                    + "; oczekiwano: " + string.Join(" | ", sublist.ExpectedEnterPostPaths)
                    + ". Zapowiedzi: " + string.Join(" | ", h.Announcements.TakeLast(5)));
            }

            for (var i = 0; i < paths.Length; i++)
            {
                if (!paths[i].EndsWith(sublist.ExpectedEnterPostPaths[i], StringComparison.Ordinal))
                {
                    throw new Exception(where + $"POST numer {i + 1} poszedł na \"{paths[i]}\" "
                        + $"zamiast \"{sublist.ExpectedEnterPostPaths[i]}\".");
                }
            }

            // IDENTYFIKATOR WYBRANEGO WIERSZA - nie "jakikolwiek POST".
            var expectedValue = sublist.EnterBodyField == "streamUrl"
                ? SecondStationUrl
                : sublist.SecondRowId;
            var bodyIndex = sublist.EnterBodyField == "streamUrl" ? 1 : 0;
            using var bodyJson = System.Text.Json.JsonDocument.Parse(bodies[bodyIndex]);
            if (!bodyJson.RootElement.TryGetProperty(sublist.EnterBodyField, out var actualValue)
                || actualValue.GetString() != expectedValue)
            {
                throw new Exception(where + $"Ciało POST numer {bodyIndex + 1} nie poniosło "
                    + sublist.EnterBodyField + "=\"" + expectedValue + "\": " + bodies[bodyIndex]);
            }

            if (paths.Any(p => p.EndsWith(TogglePath, StringComparison.Ordinal)))
            {
                throw new Exception(where + "Enter wysłał POST transportu - Enter nie ma prawa "
                    + "zamienić się w pauzę/wznowienie: " + string.Join(" | ", paths));
            }

            if (!rowKept) throw new Exception(where + "Enter zmienił zaznaczony wiersz podlisty.");
            if (!focusInList) throw new Exception(where + "Enter zabrał fokus z listy.");
        }
    }

    // ==================== T3: SPACJA NA PRZYCISKU ====================

    /// <summary>
    /// SPACJA NA PRZYCISKU ma zostac NATYWNYM AKTYWOWANIEM PRZYCISKU, nie
    /// dodatkowym transportem. <c>SharedSpace</c> routera chroni WYLACZNIE
    /// <c>TextBoxBase</c>/<c>PasswordBox</c>, wiec przycisk Zamknij/Odtworz/Dodaj
    /// jest w zasiegu przechwycenia.
    ///
    /// DWIE ROZDZIELONE RZECZY, bo syntetyczny <c>PreviewKeyDown</c> sam z siebie
    /// NIE wykonuje <c>Button.Click</c> (to robi dopiero przestrzen klawisza/KeyUp):
    ///   (a) WYNIK ROUTERA: <c>e.Handled</c> po przejsciu przez prawdziwy handler
    ///       okna MUSI byc false i NIE MOZE pojsc zaden POST transportu - inaczej
    ///       router kradnie klawisz przyciskowi;
    ///   (b) RZECZYWISTE AKTYWOWANIE: natywny <c>ButtonAutomationPeer.Invoke</c> na
    ///       przycisku Zamknij zwija liste i NIE wysyla transportu.
    /// Fizyczny gest i odczyt NVDA ida osobno - ten pomiar tego NIE UDAJE.
    /// </summary>
    private static void MeasureSpaceOnButtonDoesNotStealActivation()
    {
        foreach (var sublist in DescribeTransportSublists())
        {
            using var h = CreateTransportHarness(out var cloud);
            h.Enter();
            PrimeTransportState(h, cloud);

            var handledByRouter = true;
            var transportPosts = Array.Empty<string>();
            var closedAfterInvoke = false;
            var transportAfterInvoke = Array.Empty<string>();
            var buttonHadFocus = false;

            sublist.Run(h, dialog =>
            {
                SelectSecondRowForTransport(h, dialog, sublist);
                var close = (Button)dialog.FindName("CloseButton")!;
                close.Focus();
                Keyboard.Focus(close);
                h.PumpQuietly(TimeSpan.FromMilliseconds(120));
                buttonHadFocus = close.IsKeyboardFocused;

                // (a) WYNIK ROUTERA, mierzony na PRAWDZIWYM handlerze okna.
                var postsBefore = h.Handler.Posts.Count;
                handledByRouter = SendKeyAndReportHandled(dialog, Key.Space, ModifierKeys.None);
                h.PumpQuietly(TimeSpan.FromMilliseconds(700));
                transportPosts = h.Handler.Posts.Skip(postsBefore)
                    .Select(p => p.Uri.AbsolutePath)
                    .Where(p => p.Contains("/playback", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                // (b) NATYWNE AKTYWOWANIE PRZYCISKU: to, czego uzytkownik oczekuje
                // od Spacji na przycisku Zamknij.
                var postsBeforeInvoke = h.Handler.Posts.Count;
                var peer = new ButtonAutomationPeer(close);
                ((System.Windows.Automation.Provider.IInvokeProvider)peer.GetPattern(
                    PatternInterface.Invoke)!).Invoke();
                h.PumpQuietly(TimeSpan.FromMilliseconds(400));
                closedAfterInvoke = !dialog.IsVisible;
                transportAfterInvoke = h.Handler.Posts.Skip(postsBeforeInvoke)
                    .Select(p => p.Uri.AbsolutePath)
                    .Where(p => p.Contains("/playback", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            });

            TransportSublistsMeasuredForReport++;
            var where = "[" + sublist.Label + "] ";

            if (!buttonHadFocus)
            {
                throw new Exception(where + "Aparatura nie ustawiła fokusu klawiatury na przycisku "
                    + "Zamknij - pomiar mierzyłby Spację na liście.");
            }

            if (handledByRouter)
            {
                throw new Exception(where + "ZGLOSZONA REGRESJA ODTWORZONA: router podlisty "
                    + "PRZECHWYCIŁ Spację z fokusem na PRZYCISKU (Handled=true) - przycisk nie "
                    + "dostaje swojego klawisza. Zapowiedzi: "
                    + string.Join(" | ", h.Announcements.TakeLast(4)));
            }

            if (transportPosts.Length != 0)
            {
                throw new Exception(where + "Spacja na przycisku wysłała POST transportu: "
                    + string.Join(" | ", transportPosts));
            }

            if (!closedAfterInvoke)
            {
                throw new Exception(where + "Natywne aktywowanie przycisku Zamknij NIE zwinęło podlisty.");
            }

            if (transportAfterInvoke.Length != 0)
            {
                throw new Exception(where + "Aktywowanie przycisku Zamknij wysłało POST transportu: "
                    + string.Join(" | ", transportAfterInvoke));
            }
        }
    }

    // ==================== T4: PRESETY CTRL+SHIFT+CYFRA ====================

    /// <summary>
    /// CTRL+SHIFT+3 / 0 / minus / equals wedlug <c>RadioPresetKeyMap</c> (slot 3,
    /// 10, 11, 12) na KAZDEJ z TRZECH list: DOKLADNIE JEDEN POST pod adres
    /// MATERIALU PRESETU i z jego identyfikatorem w ciele.
    ///
    /// MATERIAL PRESETU JEST INNY NIZ ZAZNACZONY WIERSZ - gdyby gest uruchamial po
    /// prostu wiersz, adres albo cialo by sie nie zgodzily. Bez tego pomiar nie
    /// rozdzielalby presetu od Entera.
    /// </summary>
    private static void MeasurePresetShortcutsOnAllThreeLists()
    {
        // MAPA JEST ZRODLEM: slot liczymy TYM SAMYM routerem, co produkcja, a nie
        // wpisana z glowy liczba. Niezgodnosc to TWARDY blad pomiaru.
        var gestures = new[]
        {
            (Key: Key.D3, Label: "Ctrl+Shift+3"),
            (Key: Key.D0, Label: "Ctrl+Shift+0"),
            (Key: Key.OemMinus, Label: "Ctrl+Shift+minus"),
            (Key: Key.OemPlus, Label: "Ctrl+Shift+equals")
        };

        foreach (var sublist in DescribeTransportSublists())
        {
            foreach (var (key, label) in gestures)
            {
                if (!RadioPresetKeyMap.TryGetSlot(key, out var slot))
                {
                    throw new Exception("Mapa presetów nie zna klawisza " + label
                        + " - pomiar nie ma czego mierzyć.");
                }

                using var h = CreateTransportHarness(out var cloud);
                h.Enter();
                PrimeTransportState(h, cloud);
                AssignTransportPresetToSlot(h, slot, sublist);

                var paths = Array.Empty<string>();
                var bodies = Array.Empty<string>();
                var rowKept = false;
                var focusInList = false;
                var listAlive = false;
                var afterSecondGesture = 0;
                var orphanHandled = true;
                var orphanPosts = 0;

                sublist.Run(h, dialog =>
                {
                    var list = SelectSecondRowForTransport(h, dialog, sublist);
                    var rowBefore = sublist.SelectedRowId(dialog);

                    var postsBefore = h.Handler.Posts.Count;
                    SendKeyWithModifiers(dialog, key, ModifierKeys.Control | ModifierKeys.Shift);
                    h.PumpQuietly(TimeSpan.FromMilliseconds(900));

                    var newPosts = h.Handler.Posts.Skip(postsBefore).ToArray();
                    paths = newPosts.Select(p => p.Uri.AbsolutePath).ToArray();
                    bodies = newPosts.Select(p => p.Body).ToArray();
                    rowKept = string.Equals(sublist.SelectedRowId(dialog), rowBefore, StringComparison.Ordinal);
                    focusInList = list.IsKeyboardFocusWithin;
                    listAlive = dialog.IsVisible;

                    // DRUGI GEST POD RZAD. To NIE jest pomiar e.IsRepeat - WPF nie
                    // daje syntetycznie autorepetycji, a dwa zdarzenia to dwa
                    // zdarzenia. Mierzymy, ze ISTNIEJACA bramka jednego polecenia
                    // nie wypuszcza drugiej wysylki tego samego materialu.
                    var beforeSecond = h.Handler.Posts.Count;
                    SendKeyWithModifiers(dialog, key, ModifierKeys.Control | ModifierKeys.Shift);
                    SendKeyWithModifiers(dialog, key, ModifierKeys.Control | ModifierKeys.Shift);
                    h.PumpQuietly(TimeSpan.FromMilliseconds(900));
                    afterSecondGesture = h.Handler.Posts.Count - beforeSecond;
                });

                // OBCY KONTEKST: okno BEZ wlasciciela nie ma prawa uruchomic presetu
                // - router szuka okna glownego w lancuchu wlascicieli i ma odmowic.
                var orphan = new Window { ShowInTaskbar = false, Width = 10, Height = 10 };
                // Exercise the actual shared router; a plain Window without this
                // handler would always pass without testing the owner guard.
                orphan.PreviewKeyDown += (_, args) =>
                    SonosSublistSessionSwitch.TryHandleTransportAndPresets(orphan, args);
                try
                {
                    orphan.Show();
                    h.PumpUntil(() => PresentationSource.FromVisual(orphan) is not null,
                        "okno bez właściciela się nie pokazało");
                    orphan.Activate();
                    orphan.Focus();
                    Keyboard.Focus(orphan);
                    h.PumpUntil(() => ReferenceEquals(Keyboard.FocusedElement, orphan),
                        "fokus nie wszedł do okna bez właściciela");
                    var postsBeforeOrphan = h.Handler.Posts.Count;
                    orphanHandled = SendKeyAndReportHandled(
                        orphan, key, ModifierKeys.Control | ModifierKeys.Shift);
                    h.PumpQuietly(TimeSpan.FromMilliseconds(500));
                    orphanPosts = h.Handler.Posts.Count - postsBeforeOrphan;
                }
                finally { orphan.Close(); }

                TransportSublistsMeasuredForReport++;
                var where = "[" + sublist.Label + " / " + label + " / slot " + slot + "] ";

                if (paths.Length == 0)
                {
                    throw new Exception(where + "ZGLOSZONY BLAD ODTWORZONY: gest presetu w podliście "
                        + "NIE wykonał żadnego żądania. Zapowiedzi: "
                        + string.Join(" | ", h.Announcements.TakeLast(5)));
                }

                if (paths.Length != 1)
                {
                    throw new Exception(where + $"Preset wysłał {paths.Length} POST zamiast dokładnie "
                        + "jednego: " + string.Join(" | ", paths));
                }

                if (!paths[0].EndsWith(sublist.PresetPostPathSuffix, StringComparison.Ordinal))
                {
                    throw new Exception(where + "POST presetu poszedł na \"" + paths[0]
                        + "\" zamiast \"" + sublist.PresetPostPathSuffix
                        + "\" - to adres materiału presetu, INNEGO niż zaznaczony wiersz.");
                }

                if (!bodies[0].Contains(
                    "\"" + sublist.PresetBodyField + "\":\"" + sublist.PresetTargetId + "\"",
                    StringComparison.Ordinal))
                {
                    throw new Exception(where + "Ciało POST presetu nie poniosło "
                        + sublist.PresetBodyField + "=\"" + sublist.PresetTargetId + "\": " + bodies[0]);
                }

                if (bodies[0].Contains(sublist.SecondRowId, StringComparison.Ordinal))
                {
                    throw new Exception(where + "Ciało POST presetu zawiera identyfikator ZAZNACZONEGO "
                        + "WIERSZA (" + sublist.SecondRowId + ") - gest uruchomił wiersz, nie preset: "
                        + bodies[0]);
                }

                if (!rowKept) throw new Exception(where + "Preset zmienił zaznaczony wiersz.");
                if (!focusInList) throw new Exception(where + "Preset zabrał fokus z listy.");
                if (!listAlive) throw new Exception(where + "Preset zamknął podlistę.");

                if (afterSecondGesture > 1)
                {
                    throw new Exception(where + $"Dwa kolejne gesty dały {afterSecondGesture} POST - "
                        + "bramka jednego polecenia wypuściła powtórzenie tego samego materiału.");
                }

                if (orphanHandled || orphanPosts != 0)
                {
                    throw new Exception(where + "Okno BEZ właściciela przechwyciło gest presetu "
                        + $"(Handled={orphanHandled}, POST={orphanPosts}) - router nie ma prawa działać "
                        + "poza łańcuchem właścicieli okna głównego.");
                }
            }
        }
    }

    // ==================== APARATURA WASKIEGO POMIARU ====================

    /// <summary>
    /// JEDYNY BRAKUJACY HELPER ISTNIEJACEJ APARATURY: uruchomienie PRAWDZIWEGO
    /// modalu PLAYLIST. Ulubione i Moje stacje maja swoje
    /// (<c>RunFavoritesModal</c>, <c>RunOwnStreamsModal</c>) w oryginalnym pliku -
    /// playlisty nie mialy, bo zaden dotychczasowy pomiar nie wchodzil do ich
    /// modalu ta droga. Ksztalt jest DOKLADNIE taki sam: produkcyjne polecenie
    /// tworzy okno, podstawiamy TYLKO pokazanie, kroki jada z timera w petli
    /// modalu. Zadnego drugiego harnessu.
    /// </summary>
    private sealed partial class RealHarness
    {
        internal void RunPlaylistsModal(Action<SonosPlaylistsWindow> steps)
        {
            Exception? inside = null;
            Window.PresentSonosPlaylistsOverrideForTests = dialog =>
            {
                dialog.ShowInTaskbar = false;
                var timer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(25)
                };
                timer.Tick += (_, _) =>
                {
                    if (!dialog.IsLoaded || !dialog.IsVisible) return;
                    timer.Stop();
                    try { steps(dialog); }
                    catch (Exception exception) { inside = exception; }
                    finally
                    {
                        if (dialog.IsVisible)
                        {
                            try { dialog.Close(); } catch (InvalidOperationException) { }
                        }
                    }
                };
                timer.Start();
                try { dialog.ShowDialog(); }
                finally { timer.Stop(); }
            };
            try
            {
                ExecuteCommand(CommandIds.ViewPlaylists);
                Pump(Window.LastSonosPlaylistsTaskForTests
                    ?? throw new Exception("Polecenie nie rozpoczęło odczytu playlist."));
            }
            finally
            {
                Window.PresentSonosPlaylistsOverrideForTests = null;
            }

            if (inside is not null) throw inside;
        }
    }

    /// <summary>STAN SYNTETYCZNEJ CHMURY dla transportu - CZYTELNY, nie "jakis".</summary>
    private enum TransportCloudState
    {
        PlayingCanPause,
        PausedCanPlay
    }

    /// <summary>
    /// SYNTETYCZNA CHMURA Z PAMIECIA STANU. Bramka polecen Sonosa wymaga ODCZYTU
    /// uprawnien (canPause / canPlay), wiec stan MUSI sie zmieniac po POST -
    /// inaczej druga Spacja mierzylaby odmowe uprawnienia, a nie zgloszony blad.
    /// Produkt nie jest tu w niczym oslabiany.
    /// </summary>
    private sealed class TransportCloud
    {
        internal TransportCloudState State { get; set; } = TransportCloudState.PlayingCanPause;

        /// <summary>Stan, ktory chmura przyjmie po NAJBLIZSZYM POST transportu.</summary>
        internal TransportCloudState? NextStateAfterToggle { get; set; }

        internal string PlaybackBody => State == TransportCloudState.PlayingCanPause
            ? "{\"playbackState\":\"PLAYBACK_STATE_PLAYING\",\"itemId\":\"POZYCJA-1\","
                + "\"positionMillis\":12000,\"availablePlaybackActions\":{\"canPause\":true,"
                + "\"canStop\":false,\"canSkip\":true,\"canSkipBack\":true,\"canSeek\":true,"
                + "\"canCrossfade\":false}}"
            : "{\"playbackState\":\"PLAYBACK_STATE_PAUSED\",\"itemId\":\"POZYCJA-1\","
                + "\"positionMillis\":12000,\"availablePlaybackActions\":{\"canPause\":false,"
                + "\"canStop\":false,\"canPlay\":true,\"canSkip\":true,\"canSkipBack\":true,"
                + "\"canSeek\":true,\"canCrossfade\":false}}";
    }

    /// <summary>
    /// HARNESS dla wszystkich TRZECH list: playlisty z chmury, wlasne stacje z
    /// konfiguracji (nie pochodza z chmury), odpowiedzi POST dla transportu,
    /// playlist i sesji odtwarzania. Kazda trasa jest JAWNA - nieznana zostaje
    /// twardym bledem pomiaru w istniejacym <c>ReplyByRoute</c>.
    /// </summary>
    private static RealHarness CreateTransportHarness(out TransportCloud cloud)
    {
        var state = new TransportCloud();
        cloud = state;
        var h = RealHarness.Create();
        h.Handler.RouteOverride = (request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get)
            {
                if (path.EndsWith("/playlists", StringComparison.Ordinal))
                    return Json(TransportPlaylistsBody);
                if (path.EndsWith("/playback", StringComparison.Ordinal))
                    return Json(state.PlaybackBody);
                return null;
            }

            if (request.Method != HttpMethod.Post) return null;

            if (path.EndsWith("/playback/togglePlayPause", StringComparison.Ordinal))
            {
                // STAN CHMURY ZMIENIA SIE PO POLECENIU - potwierdzajacy odczyt ma
                // zobaczyc SKUTEK, a nie wiecznie ten sam stan.
                if (state.NextStateAfterToggle is { } next) state.State = next;
                state.NextStateAfterToggle = null;
                return Json("{}");
            }

            if (path.EndsWith("/playlists", StringComparison.Ordinal)) return Json("{}");
            if (path.EndsWith("/playbackSession", StringComparison.Ordinal))
                return Json("{\"sessionId\":\"" + TransportSessionId + "\"}");
            if (path.EndsWith("/loadStreamUrl", StringComparison.Ordinal)) return Json("{}");
            return null;
        };

        var streams = h.Window.StateForTests.Sonos.OwnStreams;
        streams.Clear();
        streams.Add(new SonosOwnStreamSettings
        {
            Id = "station-one",
            Name = "Pierwsza stacja",
            StreamUrl = "https://stacja.example.invalid/pierwsza"
        });
        streams.Add(new SonosOwnStreamSettings
        {
            Id = "station-two",
            Name = "Druga stacja",
            StreamUrl = SecondStationUrl
        });
        return h;
    }

    /// <summary>
    /// CIALO PLAYLIST: DWA wiersze, bo "drugi wiersz" musi istnieć, a preset
    /// pierwszej playlisty musi byc materialem INNYM niz zaznaczona druga.
    /// </summary>
    private const string TransportPlaylistsBody =
        "{\"version\":\"PL1\",\"playlists\":["
        + "{\"id\":\"LISTA-PIERWSZA\",\"name\":\"Pierwsza playlista\",\"type\":\"playlist\",\"trackCount\":3},"
        + "{\"id\":\"LISTA-DRUGA\",\"name\":\"Druga playlista\",\"type\":\"playlist\",\"trackCount\":5}]}";

    /// <summary>
    /// STAN ODCZYTANY PRODUKCYJNA DROGA. Bez tego bramka polecen odmowilaby
    /// "Stan Sonos nie został odczytany" i pomiar nie dotykalby zgloszonego bledu.
    /// Uprawnienie przychodzi z ODPOWIEDZI chmury, nie z oslabienia bramki.
    /// </summary>
    private static void PrimeTransportState(RealHarness h, TransportCloud cloud)
    {
        cloud.State = TransportCloudState.PlayingCanPause;
        var method = typeof(MainWindow).GetMethod("ReadSonosGroupStateAsync", Instance)
            ?? throw new Exception("Nie ma prawdziwej metody ReadSonosGroupStateAsync.");
        h.Pump((Task)method.Invoke(h.Window, null)!);
        if (h.Window.SonosPlaybackForTests is not { } playback)
        {
            throw new Exception("Odczyt stanu grupy nie dostarczył stanu odtwarzania - "
                + "bramka poleceń odmówiłaby z innego powodu niż zgłoszony błąd.");
        }

        if (playback.AvailablePlaybackActions is not { CanPause: true })
        {
            throw new Exception("Syntetyczna chmura nie zgłosiła canPause - pomiar Spacji "
                + "mierzyłby odmowę uprawnienia, a nie zgłoszony błąd.");
        }
    }

    /// <summary>
    /// PRESET W SLOCIE Z MATERIALEM INNYM NIZ ZAZNACZONY WIERSZ. Slot jest
    /// NIEPUSTY, czyli gest ma realny skutek, a rozny material rozdziela preset
    /// od uruchomienia wiersza.
    /// </summary>
    private static void AssignTransportPresetToSlot(RealHarness h, int slot, TransportSublist sublist)
    {
        var sessionId = h.Window.SessionsForTests.Current.Id;
        if (!MainWindow.IsSonosSession(sessionId))
        {
            throw new Exception("Preset przypisywany poza sesją Sonos: " + sessionId);
        }

        if (string.Equals(sublist.PresetTargetId, sublist.SecondRowId, StringComparison.Ordinal))
        {
            throw new Exception("Materiał presetu jest TEN SAM co zaznaczony wiersz - pomiar "
                + "nie rozdzieliłby presetu od uruchomienia wiersza.");
        }

        var entries = h.Window.StateForTests.SessionPresets.EntriesBySession
            .TryGetValue(sessionId, out var existing) ? existing : [];
        entries.RemoveAll(entry => entry.Slot == slot);
        entries.Add(new SessionPresetEntry
        {
            Slot = slot,
            TargetId = sublist.PresetTargetId,
            TargetKind = sublist.PresetKind,
            TargetTitle = "Materiał presetu slotu " + slot,
            SonosHouseholdId = HouseholdId
        });
        h.Window.StateForTests.SessionPresets.EntriesBySession[sessionId] = entries;
    }

    /// <summary>
    /// DRUGI WIERSZ z fokusem klawiatury NA LISCIE - pierwszy wiersz nie
    /// odroznilby "zachowano wybor" od "wrocono na gore".
    /// </summary>
    private static ListBox SelectSecondRowForTransport(
        RealHarness h, Window dialog, TransportSublist sublist)
    {
        var list = (ListBox)dialog.FindName(sublist.ListName)!;
        h.PumpUntil(() => list.Items.Count >= 2,
            "podlista " + sublist.Label + " nie wczytała dwóch wierszy");
        list.SelectedIndex = 1;
        list.UpdateLayout();
        list.Focus();
        Keyboard.Focus(list);
        h.PumpQuietly(TimeSpan.FromMilliseconds(120));
        if (!string.Equals(sublist.SelectedRowId(dialog), sublist.SecondRowId, StringComparison.Ordinal))
        {
            throw new Exception("Nie udało się stanąć na drugim wierszu podlisty "
                + sublist.Label + ": " + sublist.SelectedRowId(dialog));
        }

        if (!list.IsKeyboardFocusWithin)
        {
            throw new Exception("Fokus klawiatury nie wszedł na listę " + sublist.Label
                + " - pomiar mierzyłby gest spod przycisków.");
        }

        return list;
    }

    /// <summary>ZADANIE URUCHOMIENIA wystawione przez okno danej podlisty.</summary>
    private static Task? LastPlayTaskOf(Window dialog) => dialog switch
    {
        SonosFavoritesWindow favorites => favorites.LastPlayTaskForTests,
        SonosPlaylistsWindow playlists => playlists.LastPlayTaskForTests,
        SonosOwnStreamsWindow streams => streams.LastPlayTaskForTests,
        _ => throw new Exception("Nieznane okno podlisty: " + dialog.GetType().Name)
    };

    /// <summary>
    /// KLAWISZ do PRAWDZIWEGO handlera okna, ZWRACAJACY wynik routera
    /// (<c>e.Handled</c>). Potrzebny tam, gdzie mierzona jest sama DECYZJA
    /// przechwycenia, a nie jej skutek sieciowy - syntetyczny
    /// <c>PreviewKeyDown</c> nie wykonuje za WPF ani kliku przycisku, ani KeyUp.
    /// </summary>
    private static bool SendKeyAndReportHandled(Window window, Key key, ModifierKeys modifiers)
    {
        var source = PresentationSource.FromVisual(window)
            ?? throw new Exception("Okno nie ma powierzchni prezentacji.");
        var target = Keyboard.FocusedElement as UIElement ?? window;
        var previous = new byte[256];
        if (!GetKeyboardState(previous)) throw new Exception("Nie można odczytać stanu klawiatury.");
        var pressed = new byte[256];
        if ((modifiers & ModifierKeys.Control) != 0) pressed[0x11] = pressed[0xA2] = 0x80;
        if ((modifiers & ModifierKeys.Shift) != 0) pressed[0x10] = pressed[0xA0] = 0x80;
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        try
        {
            if (!SetKeyboardState(pressed)) throw new Exception("Nie można ustawić stanu klawiatury wątku.");
            if (Keyboard.Modifiers != modifiers)
            {
                throw new Exception($"Aparatura nie dała modyfikatorów {modifiers} "
                    + $"(jest {Keyboard.Modifiers}) - pomiar mierzyłby inny gest.");
            }

            target.RaiseEvent(args);
        }
        finally { SetKeyboardState(previous); }

        return args.Handled;
    }
}
