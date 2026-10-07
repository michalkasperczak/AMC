using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;

/// <summary>
/// WPIECIE IMPORTU PLAYLISTY W "MOJE STACJE SONOSA" - pomiar na PRAWDZIWYM
/// MainWindow, PRAWDZIWYM pliku na dysku i PRAWDZIWYM zapisie stanu.
///
/// Mierzymy wylacznie to, czego Core nie moze zmierzyc: ze menu Plik POKAZUJE
/// pozycje w sesji Sonos (i chowa poza nia), ze droga plik -> handler -> zapis
/// -> ODCZYT NOWYM store faktycznie przenosi stacje, ze anulowanie i blad pliku
/// NIE RUSZAJA listy oraz ze cala rzecz nie wysyla ANI JEDNEGO polecenia HTTP.
///
/// Scalanie, ID, duplikaty i odrzucone adresy sa mierzone w Core
/// (SonosOwnStreamsImportTests) i NIE sa tu powtarzane.
/// </summary>
internal static partial class SonosFavoritePlayRealOwnerTests
{
    internal static void RunOwnStreamsImport()
    {
        // ARYTMETYKA KOMUNIKATU bez okna: liczy sie, ze suma pominiec obejmuje
        // OBA zrodla. Ten przypadek jest tani i nie potrzebuje pulpitu, wiec
        // idzie przed pomiarami WPF.
        MeasureImportMessageArithmetic();
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                MeasureImportMenuVisibility();
                MeasureImportAppendsAndPersists();
                MeasureImportCancelled();
                MeasureImportBrokenFile();
                MeasureImportInsideOwnStreamsWindow();
            }
            catch (Exception e) { failure = e; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(180)))
            throw new Exception("Limit pomiaru importu playlisty do Moich stacji Sonosa");
        if (failure is not null) throw failure;
        Console.WriteLine("OK: import playlisty do Moich stacji Sonosa - widoczność w menu Plik, "
            + "prawdziwy plik/zapis/odczyt, anulowanie, błąd pliku, przycisk w oknie (5 przypadków)");
    }

    /// <summary>
    /// SUMA POMINIEC: komunikat musi doliczyc odrzucenia PARSERA i odrzucenia
    /// SCALANIA. Policzenie tylko jednego zrodla zostawia uzytkownika z
    /// nieprawdziwa arytmetyka ("dodano 1 z 4" przy 3 pominietych).
    /// </summary>
    private static void MeasureImportMessageArithmetic()
    {
        Console.Error.WriteLine("IMPORT SONOSA: arytmetyka komunikatu");
        var all = MainWindow.DescribeSonosOwnStreamsImport(
            added: 2, skippedByParser: 3, skippedDuplicates: 4, skippedInvalidAddresses: 5);
        if (!all.Contains("Pominięto 12", StringComparison.Ordinal))
            throw new Exception("Suma pominięć nie obejmuje wszystkich źródeł: " + all);
        foreach (var fragment in new[] { "już zapisane 4", "adresy nieobsługiwane 5",
            "bez adresu lub powtórzone w pliku 3" })
        {
            if (!all.Contains(fragment, StringComparison.Ordinal))
                throw new Exception($"Komunikat gubi rozbicie „{fragment}”: {all}");
        }

        // SAM PARSER pominął - scalanie nic nie odrzuciło. Źródło po stronie
        // pliku nie może zniknąć z komunikatu.
        var parserOnly = MainWindow.DescribeSonosOwnStreamsImport(1, 2, 0, 0);
        if (!parserOnly.Contains("Pominięto 2", StringComparison.Ordinal))
            throw new Exception("Pominięcia parsera wypadły z komunikatu: " + parserOnly);

        // NIC NIE PASOWAŁO: uczciwe „nie dodano”, bez liczby dodanych stacji.
        var nothing = MainWindow.DescribeSonosOwnStreamsImport(0, 0, 1, 0);
        if (!nothing.Contains("Nie dodano żadnej nowej stacji", StringComparison.Ordinal)
            || !nothing.Contains("już zapisane 1", StringComparison.Ordinal))
            throw new Exception("Brak dodanych stacji opisany nieuczciwie: " + nothing);

        // CZYSTY IMPORT: żadnego „Pominięto 0”.
        var clean = MainWindow.DescribeSonosOwnStreamsImport(3, 0, 0, 0);
        if (clean.Contains("Pominięto", StringComparison.Ordinal))
            throw new Exception("Czysty import mówi o pominięciach: " + clean);
    }

    /// <summary>
    /// MENU PLIK: pozycja importu Sonosa jest widoczna W SESJI SONOS i schowana
    /// poza nia, a pozycje Radia i WiiM zostaja tam, gdzie byly. Czytamy
    /// RZECZYWISTA widocznosc elementow po produkcyjnym przelaczeniu sesji.
    /// </summary>
    private static void MeasureImportMenuVisibility()
    {
        Console.Error.WriteLine("IMPORT SONOSA: widoczność w menu Plik");
        using var h = RealHarness.Create();
        h.Enter();
        MenuItem Item(string name) => (MenuItem)(h.Window.FindName(name)
            ?? throw new Exception("Menu Plik nie ma elementu " + name));
        var sonosImport = Item("ImportSonosOwnStreamsMenuItem");
        if (sonosImport.Visibility != Visibility.Visible)
            throw new Exception("Sesja Sonos nie pokazuje importu do Moich stacji");
        if (Item("ImportRadioPlaylistMenuItem").Visibility == Visibility.Visible)
            throw new Exception("Sesja Sonos pokazuje import Radia");

        // POZA SONOSEM: pozycja Sonosa znika, a import Radia wraca na swoje
        // miejsce - czyli nie podmieniliśmy cudzej pozycji, tylko dodali swoją.
        var radioSlot = h.Window.SessionsForTests.FindSlot("radio")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji radio.");
        h.ExecuteCommand(CommandIds.SessionSlot(radioSlot));
        if (sonosImport.Visibility == Visibility.Visible)
            throw new Exception("Import Sonosa został widoczny w sesji Radia");
        if (Item("ImportRadioPlaylistMenuItem").Visibility != Visibility.Visible)
            throw new Exception("Przełączenie sesji zepsuło import Radia");

        var wiimSlot = h.Window.SessionsForTests.FindSlot("wiim")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji wiim.");
        h.ExecuteCommand(CommandIds.SessionSlot(wiimSlot));
        if (sonosImport.Visibility == Visibility.Visible)
            throw new Exception("Import Sonosa został widoczny w sesji WiiM");
        if (Item("ImportWiiMNetworkStreamsMenuItem").Visibility != Visibility.Visible)
            throw new Exception("Przełączenie sesji zepsuło import WiiM");

        // POWROT DO SONOSA: pozycja wraca, a nie zostaje schowana na stałe.
        var sonosSlot = h.Window.SessionsForTests.FindSlot("sonos")
            ?? throw new Exception("Konfiguracja nie ma slotu sesji sonos.");
        h.ExecuteCommand(CommandIds.SessionSlot(sonosSlot));
        if (sonosImport.Visibility != Visibility.Visible)
            throw new Exception("Powrót do Sonosa nie przywrócił importu");
        if (h.Handler.Posts.Count != 0)
            throw new Exception("Przełączanie sesji wysłało POST");
    }

    /// <summary>
    /// PRAWDZIWY PLIK -> HANDLER -> ZAPIS -> ODCZYT NOWYM STORE.
    ///
    /// Stara stacja musi przezyc import NIETKNIETA (to samo ID, nazwa i adres),
    /// nowa musi dojsc, a komunikat musi doliczyc pominiecia z OBU zrodel:
    /// wiersz bez adresu odrzucony przez parser i stacje juz zapisana odrzucona
    /// przez scalanie.
    /// </summary>
    private static void MeasureImportAppendsAndPersists()
    {
        Console.Error.WriteLine("IMPORT SONOSA: prawdziwy plik, zapis i ponowny odczyt");
        using var h = RealHarness.Create();
        h.Enter();
        const string keptUrl = "https://radio.example.invalid/live?Key=AbC%2Fz&b=2";
        h.Window.StateForTests.Sonos.OwnStreams.Add(new SonosOwnStreamSettings
            { Id = "station-kept", Name = "Nazwa nadana ręcznie", StreamUrl = keptUrl });
        h.SaveStateForTests();

        // Playlista z czterema wpisami, po JEDNYM na kazde zrodlo pominiecia.
        //
        // DOBOR WPISOW jest wymuszony przez to, gdzie wpis ODPADA naprawde:
        // pusty wiersz po #EXTINF w ogole nie staje sie wpisem (parser go nie
        // odda), a "file:" odpada JUZ W PARSERZE radia - wiec zaden z nich nie
        // dochodzi do scalania i nie podbija SkippedInvalidAddresses.
        // Dlatego:
        // - "nie-adres" to NIEPUSTY wiersz, ktory parser liczy jako pominiety,
        // - adres http dluzszy niz limit 1024 znakow z loadStreamUrl (ale
        //   krotszy niz 4096, zeby nie odpadl wczesniej) przechodzi parser i
        //   zostaje odrzucony dopiero przez SonosStreamUrlPolicy w scalaniu.
        // Rozklad: parser 1 + duplikat 1 + zly adres 1 = 3 pominiecia.
        var tooLongUrl = "https://long.example.invalid/" + new string('x', 1100);
        var playlist = h.WriteTempFile("lista.m3u", string.Join('\n',
            "#EXTM3U",
            "#EXTINF:-1,Nowa stacja",
            "https://nowa.example.invalid/stream",
            "#EXTINF:-1,Nazwa z pliku",
            keptUrl,
            "#EXTINF:-1,Wpis bez adresu",
            "nie-adres",
            "#EXTINF:-1,Adres ponad limit Sonosa",
            tooLongUrl));
        if (tooLongUrl.Length is <= 1024 or >= 4096)
            throw new Exception("Fikstura zgubiła przedział długości adresu: " + tooLongUrl.Length);

        var outcome = h.ImportWithPath(playlist);
        if (outcome.Stations is null) throw new Exception("Udany import nie zwrócił listy: " + outcome.Message);
        if (outcome.Stations.Count != 2)
            throw new Exception("Wynik nie jest pełną listą, stacji: " + outcome.Stations.Count);
        var kept = outcome.Stations.SingleOrDefault(s => s.Id == "station-kept")
            ?? throw new Exception("Import zgubił zapisaną stację");
        if (kept.Name != "Nazwa nadana ręcznie" || kept.StreamUrl != keptUrl)
            throw new Exception("Import nadpisał zapisaną stację: " + kept.Name + " / " + kept.StreamUrl);
        var added = outcome.Stations.SingleOrDefault(s => s.Id != "station-kept")
            ?? throw new Exception("Import nie dodał nowej stacji");
        if (added.StreamUrl != "https://nowa.example.invalid/stream")
            throw new Exception("Dodana stacja ma inny adres: " + added.StreamUrl);
        if (outcome.FirstAddedId != added.Id)
            throw new Exception("Zaznaczenie nie wskazuje dodanej stacji");

        // KOMUNIKAT: 1 dodana, 3 pominięte - już zapisana 1, adres nieobsługiwany
        // 1, wpis bez adresu 1. Suma musi się zgadzać z PEŁNĄ liczbą pominięć.
        foreach (var fragment in new[] { "Dodano stacje", "1", "Pominięto 3",
            "już zapisane 1", "adresy nieobsługiwane 1", "bez adresu lub powtórzone w pliku 1" })
        {
            if (!outcome.Message.Contains(fragment, StringComparison.Ordinal))
                throw new Exception($"Komunikat nie zawiera „{fragment}”: {outcome.Message}");
        }

        // TRWALOSC: nowy store z TEGO SAMEGO pliku. Import tylko ZAKOLEJKOWAL
        // zapis (QueueStateSave), wiec czekamy na rzeczywiste domkniecie TEJ
        // kolejki - bez wlasnego Save, ktory zamaskowalby brak podpiecia.
        h.WaitForQueuedStateSave();
        var reloaded = h.ReloadStateForTests();
        if (reloaded.Sonos.OwnStreams.Count != 2)
            throw new Exception("Plik stanu ma stacji: " + reloaded.Sonos.OwnStreams.Count);
        var persistedKept = reloaded.Sonos.OwnStreams.SingleOrDefault(s => s.Id == "station-kept")
            ?? throw new Exception("Zapis zgubił starą stację");
        if (persistedKept.Name != "Nazwa nadana ręcznie" || persistedKept.StreamUrl != keptUrl)
            throw new Exception("Zapis uszkodził starą stację");
        if (reloaded.Sonos.OwnStreams.All(s => s.StreamUrl != "https://nowa.example.invalid/stream"))
            throw new Exception("Zapis nie utrwalił dodanej stacji");

        // ZERO SIECI: import jest lokalny - żadnego POST i żadnego pobrania
        // adresu stacji.
        if (h.Handler.Posts.Count != 0) throw new Exception("Import wysłał POST: " + h.Handler.Posts.Count);
        if (h.Handler.Requests.Any(w => w.Uri.Host.EndsWith("example.invalid", StringComparison.Ordinal)))
            throw new Exception("Import pobrał adres stacji z sieci");
    }

    /// <summary>ANULOWANIE okna wyboru pliku: lista i plik stanu bez zmian.</summary>
    private static void MeasureImportCancelled()
    {
        Console.Error.WriteLine("IMPORT SONOSA: anulowanie");
        using var h = RealHarness.Create();
        h.Enter();
        h.Window.StateForTests.Sonos.OwnStreams.Add(new SonosOwnStreamSettings
            { Id = "station-kept", Name = "Stacja próbna", StreamUrl = "https://a.example.invalid/s" });
        h.SaveStateForTests();
        // PRZED NEGATYWEM: doczekujemy zapisow PRZYGOTOWANIA. Inaczej pozniejszy
        // zapis z kolejki wpadlby w porownanie bajt w bajt i zostalby mylnie
        // przypisany anulowaniu.
        h.WaitForQueuedStateSave();
        var before = File.ReadAllText(h.SettingsPathForTests);

        var outcome = h.ImportWithPath(null);
        if (outcome.Stations is not null) throw new Exception("Anulowanie zmieniło listę");
        if (h.Window.StateForTests.Sonos.OwnStreams.Count != 1)
            throw new Exception("Anulowanie ruszyło stacje w pamięci");
        h.WaitForQueuedStateSave();
        if (File.ReadAllText(h.SettingsPathForTests) != before)
            throw new Exception("Anulowanie zapisało plik stanu");
        if (h.Handler.Posts.Count != 0) throw new Exception("Anulowanie wysłało POST");
    }

    /// <summary>
    /// BLAD PLIKU i MANIFEST HLS: komunikat, ale ANI JEDNA stacja nie znika i
    /// plik stanu zostaje bajt w bajt taki sam.
    /// </summary>
    private static void MeasureImportBrokenFile()
    {
        Console.Error.WriteLine("IMPORT SONOSA: błąd pliku i HLS");
        using var h = RealHarness.Create();
        h.Enter();
        h.Window.StateForTests.Sonos.OwnStreams.Add(new SonosOwnStreamSettings
            { Id = "station-kept", Name = "Stacja próbna", StreamUrl = "https://a.example.invalid/s" });
        h.SaveStateForTests();
        h.WaitForQueuedStateSave();
        var before = File.ReadAllText(h.SettingsPathForTests);

        // NIE MA takiego pliku.
        var missing = h.ImportWithPath(Path.Combine(Path.GetTempPath(),
            "amc-nie-ma-" + Guid.NewGuid().ToString("N") + ".m3u"));
        if (missing.Stations is not null) throw new Exception("Brak pliku zmienił listę");
        if (string.IsNullOrWhiteSpace(missing.Message)) throw new Exception("Brak pliku nie dał komunikatu");

        // MANIFEST HLS: to playlista, ale segmentów nie da się zapisać jako
        // stacji. Wolno odmówić - nie wolno wyczyścić listy.
        var hls = h.WriteTempFile("manifest.m3u8", string.Join('\n',
            "#EXTM3U", "#EXT-X-TARGETDURATION:10", "#EXTINF:10.0,", "segment0.ts", "#EXT-X-ENDLIST"));
        var hlsOutcome = h.ImportWithPath(hls);
        if (hlsOutcome.Stations is not null)
            throw new Exception("Manifest HLS zmienił listę stacji");
        if (string.IsNullOrWhiteSpace(hlsOutcome.Message)) throw new Exception("HLS nie dał komunikatu");

        if (h.Window.StateForTests.Sonos.OwnStreams.Count != 1)
            throw new Exception("Błąd importu ruszył stacje w pamięci");
        h.WaitForQueuedStateSave();
        if (File.ReadAllText(h.SettingsPathForTests) != before)
            throw new Exception("Błąd importu zapisał plik stanu");
        if (h.Handler.Posts.Count != 0) throw new Exception("Błąd importu wysłał POST");
    }

    /// <summary>
    /// PRZYCISK W OTWARTYCH MOICH STACJACH: ta sama akcja, co w menu Plik.
    /// Po imporcie lista w oknie ma PELNY wynik i zaznaczenie na dodanej stacji -
    /// czyli odswiezenie nie zgubilo ani wierszy, ani fokusu.
    /// </summary>
    private static void MeasureImportInsideOwnStreamsWindow()
    {
        Console.Error.WriteLine("IMPORT SONOSA: przycisk w oknie Moje stacje");
        using var h = RealHarness.Create();
        h.Enter();
        h.Window.StateForTests.Sonos.OwnStreams.Add(new SonosOwnStreamSettings
            { Id = "station-kept", Name = "Stacja próbna", StreamUrl = "https://a.example.invalid/s" });
        var playlist = h.WriteTempFile("jedna.m3u",
            "#EXTM3U\n#EXTINF:-1,Druga stacja\nhttps://b.example.invalid/s\n");
        h.Window.SonosOwnStreamsImportPathOverrideForTests = () => playlist;

        string? status = null;
        var rows = -1;
        string? selectedId = null;
        var enabled = false;
        h.RunOwnStreamsModal(dialog =>
        {
            var list = (ListBox)dialog.FindName("StationsList")!;
            var button = (Button)dialog.FindName("ImportButton")!;
            enabled = button.IsEnabled;
            list.SelectedIndex = 0;
            // PRODUKCYJNA droga przycisku - zdarzenie Click, nie wewnętrzna metoda.
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            rows = list.Items.Count;
            selectedId = (list.SelectedItem as SonosOwnStreamSettings)?.Id;
            status = dialog.StatusForTests;
        });

        if (!enabled) throw new Exception("Przycisk importu był wyłączony w otwartych Moich stacjach");
        if (rows != 2) throw new Exception("Lista w oknie nie odświeżyła się pełnym wynikiem, wierszy: " + rows);
        if (selectedId is null || selectedId == "station-kept")
            throw new Exception("Po imporcie zaznaczenie nie stanęło na dodanej stacji: " + selectedId);
        if (string.IsNullOrWhiteSpace(status) || !status!.Contains("Dodano stacje", StringComparison.Ordinal))
            throw new Exception("Okno nie powiedziało wyniku importu: " + status);

        h.WaitForQueuedStateSave();
        var reloaded = h.ReloadStateForTests();
        if (reloaded.Sonos.OwnStreams.Count != 2)
            throw new Exception("Import z okna nie utrwalił listy, stacji: " + reloaded.Sonos.OwnStreams.Count);
        if (h.Handler.Posts.Count != 0) throw new Exception("Import z okna wysłał POST");
    }
}
