using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// UZYTKOWY odtwarzacz Sonos mierzony na PRAWDZIWYM <see cref="MainWindow"/>:
/// RZECZYWISTE kontrolki widoku odtwarzacza, RZECZYWISTY przycisk i jego
/// dostepna nazwa oraz RZECZYWISTE polecenia czasu. Granica API jest
/// SYNTETYCZNA (<see cref="ISonosGroupSessionBackend"/>): zero sieci, konta,
/// DPAPI i audio.
///
/// Mierzone twierdzenia:
///   * D1: po wejsciu do odtwarzacza grupy WSZYSTKIE teksty odtwarzacza pochodza
///     z ODCZYTU Sonosa; zaden nie zostaje odziedziczony po poprzednim
///     odtwarzaczu innej sesji (sentinel w kontrolce musi zniknac),
///   * D2: RZECZYWISTY przycisk odtwarzania ma tresc i dostepna nazwe zgodna z
///     ODCZYTANYM stanem grupy, a zmiana Playing -> Paused ODCZYTEM aktualizuje
///     etykiete (nie zgadujemy z samego Accepted 200),
///   * D3: klikniecie RZECZYWISTEGO przycisku idzie do AKTYWNEJ GRUPY: dokladnie
///     JEDEN POST i potwierdzajacy GET, zadnej zmiany DemoMediaSession,
///   * D4: polecenia czasu (Ctrl+Shift+E/R/T) w sesji Sonos podaja ODCZYTANE
///     dane Sonosa, a nie pozycje 0 i dlugosc 0 z DemoMediaSession,
///   * D5: brak pozycji albo dlugosci to BRAK INFORMACJI, nie zero; radio bez
///     currentItem jest poprawnym stanem.
///
/// Czego to NIE dowodzi: nie ma tu odsluchu NVDA, prawdziwej mowy, klawiszy
/// systemowych, konta Sonos ani audio. Okno jest WLASNE i pokazywane.
/// </summary>
internal static class SonosPlayerUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                checks += MeasurePlayerTextsComeFromSonosRead();
                checks += MeasurePlayPauseButtonFollowsReadState();
                checks += MeasureRealButtonClickTargetsActiveGroup();
                checks += MeasureTimeCommandsUseSonosRead();
                checks += MeasureMissingTimeIsNotZero();
                checks += MeasureTimeOverOneDayDoesNotWrap();
                checks += MeasureSeekDialogsOpenForSonosGroup();
                checks += MeasureSeekConfirmSendsOneRelativeSeek();
                checks += MeasureSeekRefusalsAndAbandonmentSendNothing();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;

        Console.WriteLine(
            "OK: uzytkowy odtwarzacz Sonos - kontrolki, przycisk i polecenia czasu z odczytu grupy "
            + $"({checks} sprawdzeń, WLASNE pokazane okno)");
    }

    // ===== D1: teksty odtwarzacza z ODCZYTU, bez dziedziczenia =====

    private static int MeasurePlayerTextsComeFromSonosRead()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;

        // SLAD po odtwarzaczu INNEJ sesji: kontrolki maja tresc, ktorej Sonos
        // nigdy by nie wypisal. Widok grupy MUSI ja zastapic wlasnym odczytem,
        // bo inaczej niewidomy uzytkownik slyszy czas i stan cudzej sesji.
        harness.StampLeftoverPlayerTexts();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        foreach (var (name, text) in harness.PlayerTexts())
        {
            if (text.Contains(Harness.Leftover, StringComparison.Ordinal))
            {
                throw new Exception(
                    $"Kontrolka {name} odtwarzacza Sonos zostawila tekst poprzedniego odtwarzacza: \"{text}\".");
            }
        }

        var time = harness.Text("PlayerTimeText");
        if (!time.Contains("0:12", StringComparison.Ordinal))
        {
            throw new Exception($"Kontrolka czasu nie pokazuje ODCZYTANEJ pozycji Sonosa; jest \"{time}\".");
        }
        if (!time.Contains("3:00", StringComparison.Ordinal))
        {
            throw new Exception($"Kontrolka czasu nie pokazuje ODCZYTANEJ dlugosci Sonosa; jest \"{time}\".");
        }
        if (!harness.Text("PlayerTitleText").Contains("Preludium", StringComparison.Ordinal))
        {
            throw new Exception("Tytul w odtwarzaczu nie pochodzi z odczytanych metadanych grupy.");
        }
        if (!harness.Text("PlayerArtistText").Contains("Sonos Radio", StringComparison.Ordinal))
        {
            throw new Exception("Zrodlo w odtwarzaczu nie pochodzi z odczytanych metadanych grupy.");
        }
        if (!harness.Text("PlayerSessionText").Contains("Salon", StringComparison.Ordinal))
        {
            throw new Exception("Odtwarzacz nie nazywa uzywanej grupy Sonos.");
        }
        var state = harness.Text("PlayerStateText");
        if (!state.Contains("Wstrzymane", StringComparison.Ordinal)
            || !state.Contains("procent", StringComparison.Ordinal))
        {
            throw new Exception($"Stan i glosnosc nie pochodza z odczytu grupy; jest \"{state}\".");
        }
        return 7;
    }

    // ===== D2: przycisk i jego dostepna nazwa ida za ODCZYTANYM stanem =====

    private static int MeasurePlayPauseButtonFollowsReadState()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Playing;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        var playing = harness.ButtonContent();
        if (!string.Equals(playing, "Wstrzymaj", StringComparison.Ordinal))
        {
            throw new Exception(
                $"Grupa ODTWARZA, a rzeczywisty przycisk podaje \"{playing}\" - uzytkownik nie wie, co zrobi Enter.");
        }
        var playingName = harness.ButtonAccessibleName();
        if (!playingName.Contains("Preludium", StringComparison.Ordinal)
            || !playingName.Contains("Wstrzymaj", StringComparison.Ordinal))
        {
            throw new Exception($"Dostepna nazwa przycisku nie opisuje materialu i akcji Sonosa; jest \"{playingName}\".");
        }

        // ZMIANA stanu poznana ODCZYTEM (nie z Accepted 200): kolejny odczyt tla
        // widzi Paused, wiec etykieta MUSI sie przestawic.
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
        harness.RefreshByBackgroundRead();

        var paused = harness.ButtonContent();
        if (!string.Equals(paused, "Odtwórz", StringComparison.Ordinal))
        {
            throw new Exception(
                $"Po odczytaniu stanu Wstrzymane rzeczywisty przycisk nadal podaje \"{paused}\".");
        }
        if (!harness.ButtonAccessibleName().Contains("Odtwórz", StringComparison.Ordinal))
        {
            throw new Exception("Dostepna nazwa przycisku nie nadazyla za odczytanym stanem grupy.");
        }
        return 4;
    }

    // ===== D3: rzeczywisty przycisk -> aktywna grupa, jeden POST i GET =====

    private static int MeasureRealButtonClickTargetsActiveGroup()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Playing;
        harness.OpenPlayerForGroup("GRUPA-KUCHNIA");

        var demoPositionBefore = harness.DemoSessionPosition;
        var playbackReadsBefore = harness.Backend.PlaybackReads;
        harness.Backend.Commands.Clear();
        harness.Backend.CommandGroupIds.Clear();

        // PRAWDZIWY handler przycisku z XAML, nie helper sesji.
        harness.ClickPlayPauseButton();
        harness.PumpUntil(
            () => harness.Backend.Commands.Count > 0,
            "klikniecie rzeczywistego przycisku nie wyslalo polecenia do grupy Sonos");
        harness.PumpUntil(
            () => harness.Backend.PlaybackReads > playbackReadsBefore,
            "po poleceniu nie poszedl potwierdzajacy odczyt stanu grupy");

        if (harness.Backend.Commands.Count != 1)
        {
            throw new Exception(
                $"Rzeczywisty przycisk wyslal {harness.Backend.Commands.Count} polecen zamiast jednego.");
        }
        // Sonos ma WLASNE playback/togglePlayPause i SPEC dopuszcza "Przelacz"
        // (wiersz "Odtwórz / Pauza / Przełącz"). Sprawdzamy wiec, ze poszlo
        // polecenie TRANSPORTU tej grupy, a nie zgadujemy kierunku za Sonosa.
        if (harness.Backend.Commands[0] is not SonosGroupCommand.TogglePlayPause
            and not SonosGroupCommand.Pause)
        {
            throw new Exception(
                $"Rzeczywisty przycisk wyslal {harness.Backend.Commands[0]} zamiast polecenia odtwarzania/pauzy.");
        }
        if (harness.Backend.CommandGroupIds.Any(id => !string.Equals(id, "GRUPA-KUCHNIA", StringComparison.Ordinal)))
        {
            throw new Exception("Polecenie z przycisku poszlo do innej grupy niz aktywna.");
        }
        if (harness.DemoSessionPosition != demoPositionBefore)
        {
            throw new Exception("Przycisk ruszyl DemoMediaSession zamiast sterowac wylacznie grupa Sonos.");
        }
        return 5;
    }

    // ===== D4: polecenia czasu czytaja Sonosa, nie DemoMediaSession =====

    private static int MeasureTimeCommandsUseSonosRead()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        // Pozycja 0:12 z dlugosci 3:00 - kazda z trzech odpowiedzi jest INNA,
        // wiec zadnej nie da sie zaliczyc przypadkiem.
        var elapsed = harness.AnnounceFor(CommandIds.TimeElapsed);
        if (!elapsed.Contains("0:12", StringComparison.Ordinal))
        {
            throw new Exception($"Czas od poczatku nie pochodzi z odczytu Sonosa; powiedziano \"{elapsed}\".");
        }
        var remaining = harness.AnnounceFor(CommandIds.TimeRemaining);
        if (!remaining.Contains("2:48", StringComparison.Ordinal))
        {
            throw new Exception($"Czas pozostaly nie pochodzi z odczytu Sonosa; powiedziano \"{remaining}\".");
        }
        var total = harness.AnnounceFor(CommandIds.TimeTotal);
        if (!total.Contains("3:00", StringComparison.Ordinal))
        {
            throw new Exception($"Czas calkowity nie pochodzi z odczytu Sonosa; powiedziano \"{total}\".");
        }
        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception("Odczyt czasu wyslal polecenie do Sonosa.");
        }
        return 4;
    }

    // ===== D5: brak pozycji/dlugosci to brak informacji, nie zero =====

    private static int MeasureMissingTimeIsNotZero()
    {
        using var harness = Harness.Create();
        // RADIO: stacja bez currentItem, bez pozycji i bez dlugosci. To POPRAWNY
        // stan Sonosa, a nie blad - ale zero bylo by klamstwem.
        harness.Backend.RadioWithoutCurrentItem = true;
        harness.Backend.NextPlaybackState = SonosPlaybackState.Playing;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        foreach (var commandId in new[] { CommandIds.TimeElapsed, CommandIds.TimeRemaining, CommandIds.TimeTotal })
        {
            var said = harness.AnnounceFor(commandId);
            if (said.Contains("0:00", StringComparison.Ordinal))
            {
                throw new Exception(
                    $"Brak czasu w radiu Sonos zostal podany jako zero: \"{said}\" ({commandId}).");
            }
            if (!said.Contains("nieznan", StringComparison.OrdinalIgnoreCase)
                && !said.Contains("nie jest znan", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Brak czasu w radiu Sonos nie zostal nazwany brakiem informacji: \"{said}\" ({commandId}).");
            }
        }

        var time = harness.Text("PlayerTimeText");
        if (time.Contains("0:00", StringComparison.Ordinal))
        {
            throw new Exception($"Kontrolka czasu pokazuje zero mimo braku pozycji; jest \"{time}\".");
        }
        if (!harness.Text("PlayerTitleText").Contains("Radio Nowy Swiat", StringComparison.Ordinal))
        {
            throw new Exception("Stacja bez currentItem nie zostala uznana za poprawny tytul.");
        }
        return 5;
    }

    // ===== D6: czas DLUZSZY NIZ DOBA nie zawija sie do godziny 0-23 =====

    /// <summary>
    /// ZAWIJANIE po dobie. Wzorzec "h\:mm\:ss" bierze KOMPONENT godzin (0-23),
    /// wiec 25 h wracalo jako 1:00:00, a 24 h 30 min jako 0:30:00 - niewidomy
    /// uzytkownik slyszal dla dlugiej audycji czas KROTSZY od rzeczywistego,
    /// nie do odroznienia od poczatku materialu. Pozycja 24:30:00 z dlugosci
    /// 25:00:00 daje pozostale 30:00, wiec kazda z trzech odpowiedzi jest INNA
    /// i zadnej nie da sie zaliczyc przypadkiem.
    /// </summary>
    private static int MeasureTimeOverOneDayDoesNotWrap()
    {
        using var harness = Harness.Create();
        // PAUSED: pozycja ma zostac ODCZYTANA, nie ekstrapolowana zegarem.
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
        var position = TimeSpan.FromHours(24) + TimeSpan.FromMinutes(30);
        var duration = TimeSpan.FromHours(25);
        harness.Backend.PositionMillis = (int)position.TotalMilliseconds;
        harness.Backend.DurationMillis = (int)duration.TotalMilliseconds;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        var time = harness.Text("PlayerTimeText");
        if (!time.Contains("24:30:00", StringComparison.Ordinal))
        {
            throw new Exception(
                $"Kontrolka czasu zawija pozycje po dobie: zamiast 24:30:00 jest \"{time}\".");
        }
        if (!time.Contains("25:00:00", StringComparison.Ordinal))
        {
            throw new Exception(
                $"Kontrolka czasu zawija dlugosc po dobie: zamiast 25:00:00 jest \"{time}\".");
        }

        var elapsed = harness.AnnounceFor(CommandIds.TimeElapsed);
        if (!elapsed.Contains("24:30:00", StringComparison.Ordinal))
        {
            throw new Exception($"Czas od poczatku zawinal sie po dobie; powiedziano \"{elapsed}\".");
        }
        var total = harness.AnnounceFor(CommandIds.TimeTotal);
        if (!total.Contains("25:00:00", StringComparison.Ordinal))
        {
            throw new Exception($"Czas calkowity zawinal sie po dobie; powiedziano \"{total}\".");
        }
        var remaining = harness.AnnounceFor(CommandIds.TimeRemaining);
        if (!remaining.Contains("30:00", StringComparison.Ordinal))
        {
            throw new Exception($"Czas pozostaly nie wynika z czasow ponad dobe; powiedziano \"{remaining}\".");
        }
        return 5;
    }

    // ===== D7: RZECZYWISTE przyciski skoku otwieraja ISTNIEJACY dialog =====

    /// <summary>
    /// POTWIERDZONA wada, nie hipoteza: widoczne i wlaczone przyciski
    /// <c>PlayerSeekTimeButton</c> / <c>PlayerSeekPercentButton</c> ida droga
    /// XAML -> <c>SeekToTime_Click</c> -> <c>ExecuteCommand(SeekToTime)</c>, a te
    /// polecenia NIE sa w <see cref="SonosCommandGating.IsSeek"/>, wiec w sesji
    /// Sonos wpadaja w ogolny <c>ShowSeekPositionDialog</c> i czytaja
    /// <c>Duration</c> wiersza <c>MediaItemKind.Device</c>, ktory jest zerowy.
    /// Skutek: ZAWSZE odmowa "czas trwania jest nieznany", chociaz odczyt grupy
    /// zna dlugosc 3:00. Ten pomiar klika PRAWDZIWY przycisk (chroniony
    /// <c>Button.OnClick</c>, nie Spacja - Spacje przejmuje globalny PlayPause) i
    /// wymaga ISTNIEJACEGO okna <see cref="SeekPositionWindow"/>. Anulowanie ma
    /// dac ZERO POST.
    /// </summary>
    private static int MeasureSeekDialogsOpenForSonosGroup()
    {
        using var harness = Harness.Create();
        harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
        harness.OpenPlayerForGroup("GRUPA-SALON");

        var checks = 0;
        foreach (var (buttonName, expectedTitle) in new[]
                 {
                     ("PlayerSeekTimeButton", "Skocz do czasu"),
                     ("PlayerSeekPercentButton", "Skocz do procentu")
                 })
        {
            var button = (Button)harness.Window.FindName(buttonName)!;
            if (button.Visibility != Visibility.Visible || !button.IsEnabled)
            {
                throw new Exception(
                    $"Przycisk {buttonName} nie jest widoczny i wlaczony przy ODCZYTANEJ dlugosci 3:00.");
            }
            checks++;

            // AUTO-ANULOWANIE uzbrojone PRZED kliknieciem: modal nie ma prawa
            // zatrzymac pomiaru, a cleanup zamyka okno takze przy porazce.
            using var responder = SeekDialogResponder.ArmCancel();
            var saidBefore = harness.Announcements.Count;
            harness.ClickButton(button);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(120));

            if (responder.Seen is null)
            {
                var said = string.Join(" | ", harness.Announcements.Skip(saidBefore));
                throw new Exception(
                    $"Klikniecie rzeczywistego {buttonName} w sesji Sonos NIE otworzylo okna skoku; "
                    + $"powiedziano \"{said}\".");
            }
            if (!string.Equals(responder.Seen.Title, expectedTitle, StringComparison.Ordinal))
            {
                throw new Exception(
                    $"{buttonName} otworzylo okno \"{responder.Seen.Title}\" zamiast \"{expectedTitle}\".");
            }
            checks++;

            // Okno czasu musi dostac ODCZYTANA dlugosc grupy (3:00), nie zero z
            // wiersza Device i nie dlugosc DemoMediaSession.
            if (buttonName == "PlayerSeekTimeButton"
                && !responder.Instructions.Contains("3:00", StringComparison.Ordinal))
            {
                throw new Exception(
                    $"Okno skoku do czasu nie dostalo odczytanej dlugosci 3:00; instrukcja: \"{responder.Instructions}\".");
            }
            checks++;
        }

        if (harness.Backend.Commands.Count != 0)
        {
            throw new Exception(
                $"ANULOWANY skok wyslal {harness.Backend.Commands.Count} polecen do Sonosa zamiast zera.");
        }
        return checks + 1;
    }

    /// <summary>
    /// ZATWIERDZENIE obu okien: DOKLADNIE JEDNO zadanie skoku z poprawnym
    /// groupId, itemId i delta liczona od AKTUALNEGO odczytu, jawny GET po
    /// skoku, PlayerTimeText z tego odczytu i NIETKNIETY DemoMediaSession.
    /// Backend ma tylko SeekRelativeAsync, wiec pozycja docelowa musi zejsc do
    /// DELTY - sprawdzamy jej rzeczywista wartosc, nie sam fakt wywolania.
    /// </summary>
    private static int MeasureSeekConfirmSendsOneRelativeSeek()
    {
        var checks = 0;
        // Czas 2:30 z pozycji 12 s to +138 s; procent 50 z 3:00 to 1:30, czyli +78 s.
        foreach (var (commandId, typed, expectedDelta) in new[]
                 {
                     (CommandIds.SeekToTime, "2:30", 138_000),
                     (CommandIds.SeekToPercentage, "50", 78_000)
                 })
        {
            using var harness = Harness.Create();
            harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var demoBefore = harness.DemoSessionPosition;
            var readsBefore = harness.Backend.PlaybackReads;

            using var responder = SeekDialogResponder.ArmConfirm(typed);
            harness.RunSeekToPosition(commandId);

            if (harness.Backend.SeekCalls.Count != 1)
            {
                throw new Exception(
                    $"{commandId}: wyslano {harness.Backend.SeekCalls.Count} zadan skoku zamiast dokladnie jednego.");
            }
            var call = harness.Backend.SeekCalls[0];
            if (call.GroupId != "GRUPA-SALON" || call.ItemId != "UTWOR-1")
            {
                throw new Exception(
                    $"{commandId}: skok poszedl do grupy {call.GroupId} i materialu {call.ItemId}.");
            }
            checks += 2;

            if (call.DeltaMillis != expectedDelta)
            {
                throw new Exception(
                    $"{commandId}: delta {call.DeltaMillis} ms zamiast {expectedDelta} ms liczonych od odczytanej pozycji.");
            }
            checks++;

            // JAWNY odczyt PO skoku - inaczej Accepted bylby jedynym "dowodem".
            if (harness.Backend.PlaybackReads <= readsBefore + 1)
            {
                throw new Exception(
                    $"{commandId}: po skoku nie bylo jawnego odczytu ({readsBefore} -> {harness.Backend.PlaybackReads}).");
            }
            checks++;

            if (harness.DemoSessionPosition != demoBefore)
            {
                throw new Exception(
                    $"{commandId}: ruszono DemoMediaSession ({demoBefore} -> {harness.DemoSessionPosition}).");
            }
            checks++;

            // UI pokazuje ODCZYT (12 s), a nie zyczenie uzytkownika: fake nie
            // przesuwa pozycji, wiec udawanie trafionego miejsca byloby klamstwem.
            if (!harness.Text("PlayerTimeText").Contains("0:12", StringComparison.Ordinal))
            {
                throw new Exception(
                    $"{commandId}: PlayerTimeText \"{harness.Text("PlayerTimeText")}\" nie pochodzi z odczytu po skoku.");
            }
            checks++;
        }
        return checks;
    }

    /// <summary>
    /// ZERO POST tam, gdzie skoku byc nie moze: anulowanie okna, brak
    /// odczytanej dlugosci oraz PODMIANA materialu w czasie
    /// trwania modalu. Ostatni przypadek jest istotny, bo modal trwa dowolnie
    /// dlugo - skok policzony przed nim trafilby w cudzy material.
    /// </summary>
    private static int MeasureSeekRefusalsAndAbandonmentSendNothing()
    {
        var checks = 0;

        // 1. ANULOWANIE po wpisaniu wartosci: zero zadan.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmCancel();
            harness.RunSeekToPosition(CommandIds.SeekToTime);
            if (responder.Seen is null) throw new Exception("Anulowanie: okno skoku w ogole sie nie otworzylo.");
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception($"Anulowanie wyslalo {harness.Backend.SeekCalls.Count} zadan skoku.");
            }
            checks += 2;
        }

        // 2. BRAK odczytanej dlugosci: jawna odmowa, zero zadan, zero okna.
        using (var harness = Harness.Create())
        {
            harness.Backend.RadioWithoutCurrentItem = true;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmConfirm("1:00");
            var said = harness.Announcements.Count;
            harness.RunSeekToPosition(CommandIds.SeekToPercentage);
            if (responder.Seen is not null) throw new Exception("Bez znanej dlugosci otwarto okno skoku.");
            if (harness.Backend.SeekCalls.Count != 0) throw new Exception("Bez znanej dlugosci poszedl skok.");
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            if (!text.Contains("czasu trwania", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Bez znanej dlugosci nie powiedziano czego brakuje: \"{text}\".");
            }
            checks += 3;
        }

        // 3. PODMIANA materialu w trakcie modalu: zadanie NIE moze poleciec.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmConfirm("2:00");
            // Sonos przechodzi do NASTEPNEGO utworu, gdy okno jest juz otwarte.
            responder.BeforeConfirm = () => harness.Backend.CurrentItemId = "UTWOR-2";
            var said = harness.Announcements.Count;
            harness.RunSeekToPosition(CommandIds.SeekToTime);
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception(
                    $"Po zmianie materialu w trakcie modalu i tak wyslano skok: {harness.Backend.SeekCalls[0]}.");
            }
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            if (!text.Contains("materiał", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Porzucony skok nie zostal nazwany: \"{text}\".");
            }
            checks += 2;
        }
        return checks;
    }

    // ==================== aparatura ====================

    /// <summary>
    /// PRAWDZIWE okno, PRAWDZIWA droga wejscia (polecenie slotu + Enter) i
    /// SYNTETYCZNA granica API. Wzor aparatury jest przepisany z istniejacych
    /// pomiarow sesji Sonos, zeby nie budowac drugiej, innej maszynerii.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        internal const string Leftover = "SLAD-POPRZEDNIEGO-ODTWARZACZA";

        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

        private readonly string _directory;
        private readonly Dispatcher _dispatcher;

        private Harness(string directory, MainWindow window, FakeBackend backend, List<string> announcements)
        {
            _directory = directory;
            _dispatcher = Dispatcher.CurrentDispatcher;
            Window = window;
            Backend = backend;
            Announcements = announcements;
        }

        internal MainWindow Window { get; }

        internal FakeBackend Backend { get; }

        internal List<string> Announcements { get; }

        internal ListBox MediaList => (ListBox)Window.FindName("MediaList")!;

        internal Button PlayPauseButton => (Button)Window.FindName("PlayerPlayPauseButton")!;

        internal bool PlayerViewActive => (bool)Field("_playerViewActive")!;

        internal TimeSpan DemoSessionPosition
        {
            get
            {
                var sessions = Field("_sessions")!;
                var current = sessions.GetType().GetProperty("Current", Instance)!.GetValue(sessions)!;
                return (TimeSpan)current.GetType().GetProperty("Position", Instance)!.GetValue(current)!;
            }
        }

        internal static Harness Create()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));

            var directory = Path.Combine(Path.GetTempPath(), "amc-sonos-player-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var store = new ConfigurationStore(Path.Combine(directory, "settings.json"));
            var state = store.LoadOrCreate();
            state.Podcasts.Subscriptions.Clear();
            state.Podcasts.Episodes.Clear();
            state.WiiM.Devices.Clear();
            state.Radio.RecordingSchedules.Clear();
            state.Settings.Updates.CheckAutomatically = false;
            state.Settings.Updates.InstallOnExit = false;

            var backend = new FakeBackend();
            var announcements = new List<string>();
            var window = new MainWindow(state, store)
            {
                SuppressDesktopIntegrationForTests = true,
                SonosBackendOverride = backend,
                AnnouncementSinkForTests = announcements.Add
            };
            window.DenyApplicationUpdateStartForTests();
            return new Harness(directory, window, backend, announcements);
        }

        internal void OpenPlayerForGroup(string groupId)
        {
            ShowOwnWindow();
            ExecuteCommand(CommandIds.SessionSlot(8));
            PumpUntil(() => MediaList.Items.Count == 2, "pierwsze wejście nie wypełniło kontrolki listy");
            SelectRowByGroupId(groupId);
            PressKey(Key.Enter);
            PumpUntil(
                () => PlayerViewActive
                    && string.Equals(Window.SonosActiveGroup?.Id, groupId, StringComparison.Ordinal),
                "Enter na wierszu grupy nie otworzył odtwarzacza tej grupy");
            PumpUntil(
                () => Backend.MetadataReads > 0 && Backend.VolumeReads > 0,
                "wejście do odtwarzacza nie domknęło odczytu metadanych i głośności");
            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        /// <summary>
        /// Wpisuje SLAD poprzedniego odtwarzacza wprost do kontrolek. To nie jest
        /// podmiana logiki produkcyjnej, tylko stan, jaki zostaje po odtwarzaczu
        /// INNEJ sesji - widok Sonosa ma go nadpisac wlasnym odczytem.
        /// </summary>
        internal void StampLeftoverPlayerTexts()
        {
            foreach (var name in TextControlNames)
            {
                ((TextBlock)Window.FindName(name)!).Text = Leftover;
            }
            PlayPauseButton.Content = Leftover;
        }

        private static readonly string[] TextControlNames =
        [
            "PlayerTitleText", "PlayerArtistText", "PlayerSessionText",
            "PlayerStateText", "PlayerTimeText"
        ];

        internal IEnumerable<(string Name, string Text)> PlayerTexts() =>
            TextControlNames.Select(name => (name, Text(name)))
                .Append(("PlayerPlayPauseButton", ButtonContent()));

        internal string Text(string controlName) =>
            ((TextBlock)Window.FindName(controlName)!).Text ?? string.Empty;

        internal string ButtonContent() => PlayPauseButton.Content as string ?? string.Empty;

        internal string ButtonAccessibleName() =>
            AutomationProperties.GetName(PlayPauseButton) ?? string.Empty;

        /// <summary>PRAWDZIWY handler kliknięcia przycisku z XAML.</summary>
        internal void ClickPlayPauseButton()
        {
            var method = Window.GetType().GetMethod(
                "PlayerPlayPause_Click", Instance, binder: null,
                types: [typeof(object), typeof(RoutedEventArgs)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwego handlera PlayerPlayPause_Click.");
            Invoke(method, [PlayPauseButton, new RoutedEventArgs()]);
        }

        /// <summary>
        /// RZECZYWISTE klikniecie przycisku jego WLASNA droga zdarzenia Click z
        /// XAML - chroniony <c>Button.OnClick</c>, nie helper i nie Spacja.
        /// Fizyczna Spacja na tych przyciskach jest przejeta przez globalny
        /// PlayPause, wiec NIE jest kliknieciem skoku.
        /// </summary>
        internal void ClickButton(Button button)
        {
            var onClick = typeof(Button).GetMethod("OnClick", Instance, binder: null, types: [], modifiers: null)
                ?? throw new Exception("Nie ma chronionego Button.OnClick.");
            try
            {
                onClick.Invoke(button, []);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        /// <summary>
        /// ODCZYT tla PRAWDZIWA droga licznika odtwarzacza: to on ma przeniesc
        /// nowy stan grupy na kontrolki.
        /// </summary>
        internal void RefreshByBackgroundRead()
        {
            var playbackBefore = Backend.PlaybackReads;
            SetField("_sonosNextBackgroundReadUtc", DateTime.MinValue);
            var tick = Window.GetType().GetMethod(
                "PlayerUiTimer_Tick", Instance, binder: null,
                types: [typeof(object), typeof(EventArgs)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwego handlera PlayerUiTimer_Tick.");
            Invoke(tick, [null, EventArgs.Empty]);
            PumpUntil(
                () => Backend.PlaybackReads > playbackBefore
                    && Window.LastSonosBackgroundPollTaskForTests is { IsCompleted: true },
                "odczyt tła nie zakończył się nowym stanem grupy");
            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        /// <summary>Wykonuje polecenie i zwraca to, co RZECZYWIŚCIE powiedziano.</summary>
        internal string AnnounceFor(string commandId)
        {
            var before = Announcements.Count;
            ExecuteCommand(commandId);
            PumpUntil(() => Announcements.Count > before, $"polecenie {commandId} nic nie powiedziało");
            return string.Join(" | ", Announcements.Skip(before));
        }

        internal void SelectRowByGroupId(string groupId)
        {
            var list = MediaList;
            var index = Enumerable.Range(0, list.Items.Count).First(position =>
                string.Equals(RowId(list.Items[position]), groupId, StringComparison.Ordinal));
            list.SelectedIndex = index;
            list.UpdateLayout();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container) container.Focus();
            else list.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (list.SelectedIndex != index) throw new Exception("Nie udało się zaznaczyć wiersza grupy.");
        }

        private static string? RowId(object row) =>
            row.GetType().GetProperty("Item", Instance)?.GetValue(row) is AccessibleMediaController.Core.Sessions.MediaItem item
                ? item.Id
                : null;

        internal void ShowOwnWindow()
        {
            var rendered = (EventHandler)Delegate.CreateDelegate(
                typeof(EventHandler),
                Window,
                Window.GetType().GetMethod("Window_ContentRendered", Instance)!);
            Window.ContentRendered -= rendered;
            Window.ShowInTaskbar = false;
            Window.Show();
            PumpUntil(() => Window.IsLoaded && PresentationSource.FromVisual(Window) is not null,
                "własne okno się nie pokazało");
            MediaList.Focus();
            PumpQuietly(TimeSpan.FromMilliseconds(100));
        }

        internal void ExecuteCommand(string commandId)
        {
            var method = Window.GetType().GetMethod(
                "ExecuteCommand", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej metody ExecuteCommand(string).");
            Invoke(method, [commandId]);
        }

        /// <summary>
        /// PRAWDZIWA droga skoku do pozycji w sesji Sonos. Pompujemy zadanie do
        /// DEADLINE, nie stalej ciszy: modal konczy sie od naszego odpowiadacza,
        /// a po nim zostaja jeszcze odczyt i wyslanie skoku.
        /// </summary>
        internal void RunSeekToPosition(string commandId)
        {
            var method = Window.GetType().GetMethod(
                "SeekSonosToPositionForTests", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej drogi SeekSonosToPositionForTests(string).");
            Task task;
            try
            {
                task = (Task)method.Invoke(Window, [commandId])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
            PumpUntil(() => task.IsCompleted, "zadanie skoku do pozycji nie zakonczylo sie");
            if (task.IsFaulted) throw task.Exception!.InnerException!;
            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        internal void PressKey(Key key)
        {
            var target = Keyboard.FocusedElement as UIElement ?? MediaList;
            var source = PresentationSource.FromVisual(Window)
                ?? throw new Exception("Okno nie ma powierzchni prezentacji; pokaż je przed klawiszem.");
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            PumpQuietly(TimeSpan.FromMilliseconds(50));
        }

        private void Invoke(MethodInfo method, object?[] arguments)
        {
            try
            {
                method.Invoke(Window, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
        }

        internal void PumpUntil(Func<bool> condition, string what)
        {
            var deadline = DateTime.UtcNow + Limit;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: " + what + ".");
                DoEvents();
            }
        }

        internal void PumpQuietly(TimeSpan duration)
        {
            var deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline) DoEvents();
        }

        private void DoEvents()
        {
            var frame = new DispatcherFrame();
            _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }

        private object? Field(string name) =>
            Window.GetType().GetField(name, Instance)?.GetValue(Window)
            ?? throw new Exception("Nie ma pola " + name + " w prawdziwym MainWindow.");

        private void SetField(string name, object? value) =>
            (Window.GetType().GetField(name, Instance)
                ?? throw new Exception("Nie ma pola " + name + " w prawdziwym MainWindow."))
            .SetValue(Window, value);

        public void Dispose()
        {
            // ZWALNIAMY WSZYSTKIE wlasne bramki PRZED zamknieciem, inaczej okno
            // czekaloby na zadanie, ktorego nikt juz nie dokonczy.
            Backend.ReleaseEverything();
            Window.CancelSonosPendingWork();

            var closed = false;
            void OnClosed(object? sender, EventArgs e) => closed = true;
            Window.Closed += OnClosed;
            try
            {
                Window.Close();
            }
            catch (InvalidOperationException)
            {
                closed = true;
            }

            var deadline = DateTime.UtcNow + Limit;
            while (!closed)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Window.Closed -= OnClosed;
                    throw new Exception("Limit czasu: własne okno pomiaru się nie zamknęło.");
                }
                DoEvents();
            }
            Window.Closed -= OnClosed;

            PumpQuietly(TimeSpan.FromMilliseconds(200));
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// AUTO-ODPOWIEDZ na modalny <see cref="SeekPositionWindow"/>: przechwytuje
    /// KAZDE nowe okno tej klasy przez <see cref="EventManager"/> na zdarzeniu
    /// Loaded, zapisuje RZECZYWISTE okno i albo je anuluje, albo wpisuje wartosc
    /// i zatwierdza PRAWDZIWYM przyciskiem. Uzbrajamy to PRZED kliknieciem, a
    /// <see cref="Dispose"/> domyka okno takze przy porazce pomiaru - inaczej
    /// modal zatrzymalby caly watek STA.
    /// </summary>
    private sealed class SeekDialogResponder : IDisposable
    {
        // JEDNA rejestracja klasowa na cala aparature: EventManager NIE ma
        // odrejestrowania, wiec kolejne uzbrojenia tylko podmieniaja AKTYWNEGO
        // odpowiadajacego, a nie mnoza handlerow.
        private static bool _registered;
        private static SeekDialogResponder? _active;

        private readonly string? _value;

        private SeekDialogResponder(string? value)
        {
            _value = value;
            if (!_registered)
            {
                EventManager.RegisterClassHandler(
                    typeof(SeekPositionWindow),
                    FrameworkElement.LoadedEvent,
                    new RoutedEventHandler(static (sender, args) => _active?.OnLoaded(sender, args)));
                _registered = true;
            }
            if (_active is not null) throw new Exception("Poprzednia odpowiedz na okno skoku nie zostala zwolniona.");
            _active = this;
        }

        /// <summary>Uzbraja ANULOWANIE: zero POST po zamknieciu okna.</summary>
        internal static SeekDialogResponder ArmCancel() => new(null);

        /// <summary>
        /// Uzbraja WPISANIE wartosci i RZECZYWISTE zatwierdzenie PRAWDZIWYM
        /// przyciskiem "Skocz" z XAML okna - nie ustawiamy DialogResult sami,
        /// bo pominelibysmy walidacje i parsowanie wartosci.
        /// </summary>
        internal static SeekDialogResponder ArmConfirm(string value) => new(value);

        /// <summary>RZECZYWISCIE otwarte okno skoku albo null, gdy zadne nie wyszlo.</summary>
        internal SeekPositionWindow? Seen { get; private set; }

        /// <summary>Tekst instrukcji okna - stamtad wiemy, jaka DLUGOSC dostalo.</summary>
        internal string Instructions { get; private set; } = string.Empty;

        /// <summary>Co zrobic PO zaladowaniu okna, a PRZED zatwierdzeniem.</summary>
        internal Action? BeforeConfirm { get; set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not SeekPositionWindow dialog) return;
            Seen = dialog;
            Instructions = (dialog.FindName("InstructionsText") as TextBlock)?.Text ?? string.Empty;
            if (_value is null)
            {
                dialog.DialogResult = false;
                return;
            }

            BeforeConfirm?.Invoke();
            ((TextBox)dialog.FindName("ValueBox")!).Text = _value;
            var confirm = FindConfirmButton(dialog)
                ?? throw new Exception("Okno skoku nie ma przycisku zatwierdzenia.");
            var onClick = typeof(Button).GetMethod("OnClick", Instance, binder: null, types: [], modifiers: null)!;
            onClick.Invoke(confirm, []);
        }

        private static Button? FindConfirmButton(DependencyObject root)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                if (child is Button { IsDefault: true } button) return button;
                if (FindConfirmButton(child) is { } found) return found;
            }
            return null;
        }

        public void Dispose()
        {
            _active = null;
            if (Seen is { IsVisible: true } dialog)
            {
                try
                {
                    dialog.DialogResult = false;
                }
                catch (InvalidOperationException)
                {
                    dialog.Close();
                }
            }
        }
    }

    /// <summary>SYNTETYCZNA granica API: zero HttpClient, tokenu i magazynu.</summary>
    private sealed class FakeBackend : ISonosGroupSessionBackend
    {
        private readonly List<TaskCompletionSource> _gates = [];

        internal List<SonosGroupCommand> Commands { get; } = [];

        internal List<string?> CommandGroupIds { get; } = [];

        internal int PlaybackReads { get; private set; }

        internal int MetadataReads { get; private set; }

        internal int VolumeReads { get; private set; }

        internal SonosPlaybackState NextPlaybackState { get; set; } = SonosPlaybackState.Playing;

        /// <summary>Stacja bez currentItem: bez pozycji i bez dlugosci.</summary>
        internal bool RadioWithoutCurrentItem { get; set; }

        /// <summary>
        /// ODCZYTANA pozycja grupy. Domyslnie ta sama co dotad (0:12), zeby
        /// istniejace pomiary mierzyly dokladnie to samo co wczesniej.
        /// </summary>
        internal int PositionMillis { get; set; } = 12_000;

        /// <summary>ODCZYTANA dlugosc materialu; domyslnie ta sama co dotad (3:00).</summary>
        internal int DurationMillis { get; set; } = 180_000;

        /// <summary>ODCZYTANY identyfikator materialu - zmiana udaje przejscie utworu.</summary>
        internal string CurrentItemId { get; set; } = "UTWOR-1";

        private readonly SonosPlaybackActions _actions = new(
            canPlay: true, canSkip: true, canSkipBack: true, canSkipToPrevious: true,
            canSeek: true, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        internal void ReleaseEverything()
        {
            foreach (var gate in _gates) gate.TrySetResult();
        }

        public Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            PlaybackReads++;
            var status = new SonosGroupPlaybackStatus(
                NextPlaybackState, null, null,
                RadioWithoutCurrentItem ? null : CurrentItemId,
                RadioWithoutCurrentItem ? null : PositionMillis,
                null, null, null, _actions);
            return Task.FromResult(SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status));
        }

        public Task<SonosGroupReadResult<SonosGroupMetadata>> ReadGroupMetadataAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            MetadataReads++;
            var metadata = RadioWithoutCurrentItem
                ? new SonosGroupMetadata(
                    new SonosMetadataContainer(
                        "Radio Nowy Swiat", "station", new SonosMetadataService("Sonos Radio", "9")),
                    null, null, null, null)
                : new SonosGroupMetadata(
                    null,
                    new SonosQueueItem(CurrentItemId, new SonosTrackMetadata(
                        "track", "Preludium", "Chopin", "Nokturny", null,
                        new SonosMetadataService("Sonos Radio", "9"), DurationMillis), null),
                    null, null, null);
            return Task.FromResult(SonosGroupReadResult<SonosGroupMetadata>.Success(metadata));
        }

        public Task<SonosGroupReadResult<SonosGroupVolume>> ReadGroupVolumeAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            VolumeReads++;
            return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(
                new SonosGroupVolume(30, false, false)));
        }

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        /// <summary>RZECZYWISTE argumenty KAZDEGO skoku: delta i itemId celu.</summary>
        internal List<(string? GroupId, int DeltaMillis, string? ItemId)> SeekCalls { get; } = [];

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            SeekCalls.Add((groupId, deltaMillis, itemId));
            Commands.Add(SonosGroupCommand.SeekRelative);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            CommandGroupIds.Add(groupId);
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetMute));
        }

        public Task<SonosHouseholdsReadResult> ReadHouseholdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SonosHouseholdsReadResult.Success([new SonosHousehold("DOM-1", "Dom", null)]));

        public Task<SonosGroupsReadResult> ReadGroupsAsync(
            string householdId, CancellationToken cancellationToken) =>
            Task.FromResult(SonosGroupsReadResult.Success(new SonosHouseholdTopology(
                [
                    new SonosGroup("GRUPA-SALON", "Salon", "P1", ["P1"], SonosPlaybackState.Playing),
                    new SonosGroup("GRUPA-KUCHNIA", "Kuchnia", "P2", ["P2"], SonosPlaybackState.Playing)
                ],
                [
                    new SonosPlayer("P1", "Salon", null, null, null),
                    new SonosPlayer("P2", "Kuchnia", null, null, null)
                ],
                false)));
    }
}
