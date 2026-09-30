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
                // CZTERY uwagi odbioru mierzone RAZEM: kazda ma pokazac swoj
                // WLASNY wynik, a nie schowac sie za pierwsza porazka. Sumaryczny
                // wyjatek trzyma wszystkie tresci, wiec RED jest kompletny.
                var pending = new List<string>();
                foreach (var (name, measure) in new (string, Func<int>)[]
                         {
                             ("S1 swieza CanSeek", MeasureFreshCanSeekFalseStopsSeek),
                             ("S2 bramka w czasie przedskokowego GET", MeasureBusyGateCoversPreSeekRead),
                             ("#3 komunikat przy bledzie odczytu", MeasurePreSeekReadFaultIsAnnounced),
                             ("#2 powrot fokusu", MeasureFocusReturnsToOpeningControl),
                             ("K1 cisza po porzuceniu celu", MeasureAbandonedTargetSaysNothing),
                             ("K2 proba bez potwierdzenia", MeasureUnsentSeekIsNotCalledSent),
                             ("B4-1 przewijanie custom z konfiguracji", MeasureCustomSeekUsesConfiguredLength),
                             ("B4-2 cyfry 0-9 od swiezego odczytu", MeasureDigitSeekUsesFreshRead),
                             ("B4-3 odmowy przewijania cyfrowego", MeasureDigitSeekRefusalsSendNothing),
                             ("B4-4 skip do grupy i bramki", MeasureSkipTargetsGroupAndRespectsGates),
                             ("B4-5 glosnosc z odczytu", MeasureVolumeStepsUseReadValue),
                             ("B4-6 mute tylko znanego bool", MeasureMuteInvertsKnownReadOnly)
                         })
                {
                    // POSTEP na stdout: gdyby ktorys przypadek zawisl na modalu,
                    // log MUSI pokazac, ktory - inaczej mamy tylko pusty plik.
                    Console.WriteLine("... mierze: " + name);
                    Console.Out.Flush();
                    try
                    {
                        checks += measure();
                    }
                    catch (Exception exception)
                    {
                        pending.Add(name + ": " + exception.Message);
                    }
                }
                if (pending.Count > 0) throw new Exception(string.Join(Environment.NewLine, pending));
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

    /// <summary>
    /// S1 (uwaga odbioru): CanSeek utracony W TRAKCIE modalu. Bramka przed
    /// oknem widzi CanSeek=true, ale SWIEZY odczyt przed wyslaniem zglasza
    /// CanSeek=false dla TEGO SAMEGO materialu - skok NIE ma prawa polecieć, a
    /// odmowa musi byc nazwana. Mierzymy takze, ze swiezy odczyt RZECZYWISCIE
    /// byl (inaczej "brak POST" moglby wynikac z czegos innego) i ze bramka
    /// polecen jest po odmowie ZWOLNIONA.
    /// </summary>
    private static int MeasureFreshCanSeekFalseStopsSeek()
    {
        var checks = 0;
        foreach (var commandId in new[] { CommandIds.SeekToTime, CommandIds.SeekToPercentage })
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var readsBefore = harness.Backend.PlaybackReads;
            var said = harness.Announcements.Count;

            using var responder = SeekDialogResponder.ArmConfirm(
                commandId == CommandIds.SeekToTime ? "2:30" : "50");
            // Sonos przestaje zglaszac przewijanie, gdy okno jest juz otwarte.
            responder.BeforeConfirm = () => harness.Backend.CanSeekFlag = false;
            harness.RunSeekToPosition(commandId);

            if (harness.Backend.PlaybackReads <= readsBefore)
            {
                throw new Exception(
                    $"{commandId}: nie bylo swiezego odczytu przed wyslaniem ({readsBefore} -> {harness.Backend.PlaybackReads}).");
            }
            checks++;
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception(
                    $"{commandId}: przy CanSeek=false ze SWIEZEGO odczytu wyslano skok {harness.Backend.SeekCalls[0]}.");
            }
            checks++;
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            if (!text.Contains("przewijan", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"{commandId}: utracony CanSeek nie zostal nazwany: \"{text}\".");
            }
            checks++;
            if (harness.CommandInFlight)
            {
                throw new Exception($"{commandId}: po odmowie bramka polecen Sonos zostala zamknieta.");
            }
            checks++;
        }
        return checks;
    }

    /// <summary>
    /// S2 (uwaga odbioru): rezerwacja bramki MUSI obowiazywac najpozniej PRZED
    /// pierwszym await, czyli takze w czasie przedskokowego GET-a. Konkurent
    /// (PRODUKCYJNA droga glosnosci) startuje z PRAWDZIWEGO, POZNIEJSZEGO
    /// callbacka kolejki Dispatchera, gdy GET wisi na barierze - to nie jest
    /// rekurencyjne klikniecie w atrapie. Wymagamy: brak nakladania sie POST-ow,
    /// bramka zwolniona PO probie i brak zwolnienia cudzego biletu.
    /// </summary>
    private static int MeasureBusyGateCoversPreSeekRead()
    {
        var checks = 0;
        using var harness = Harness.Create();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        using var responder = SeekDialogResponder.ArmConfirm("2:30");
        // BARIERA dokladnie na PRZEDSKOKOWYM GET: uzbrajamy ja w chwili
        // zatwierdzenia okna, wiec zatrzymany odczyt to ten PO modalu, a nie
        // przypadkowy odczyt tla, ktory moglby wypasc wczesniej.
        responder.BeforeConfirm = () => harness.Backend.HoldNextPlaybackRead = true;
        var seek = harness.StartSeekToPosition(CommandIds.SeekToTime);
        harness.PumpUntil(
            () => harness.Backend.HeldPlaybackRead is not null,
            "przedskokowy odczyt nie zatrzymal sie na barierze");
        if (harness.Backend.SeekCalls.Count != 0)
        {
            throw new Exception("Bariera zatrzymala odczyt PO wyslaniu skoku, a nie przedskokowy.");
        }
        checks += 2;
        Console.WriteLine($"   S2: wstrzymany przedskokowy GET, busy={harness.CommandInFlight}");

        if (!harness.CommandInFlight)
        {
            throw new Exception(
                "W czasie przedskokowego GET-a bramka polecen Sonos jest otwarta: rezerwacja nie obowiazuje przed pierwszym await.");
        }
        checks++;

        // PRAWDZIWA kolejka Dispatchera: konkurent jest osobnym callbackiem.
        Task? competitor = null;
        var saidBefore = harness.Announcements.Count;
        harness.PostToDispatcher(() => competitor = harness.Window.ExecuteSonosCommandForTests(CommandIds.VolumeUp1));
        harness.PumpUntil(() => competitor is { IsCompleted: true }, "konkurencyjne polecenie nie zakonczylo sie");
        if (harness.Backend.Commands.Contains(SonosGroupCommand.SetVolume))
        {
            throw new Exception(
                "Rownolegle polecenie glosnosci przeszlo do backendu w czasie przedskokowego GET-a skoku (nakladanie POST-ow).");
        }
        var refusal = string.Join(" | ", harness.Announcements.Skip(saidBefore));
        if (!refusal.Contains("jeszcze się nie zakończyło", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Konkurent nie dostal jawnej odmowy zajetosci: \"{refusal}\".");
        }
        checks += 2;

        harness.Backend.ReleaseHeldPlaybackRead();
        harness.PumpUntil(() => seek.IsCompleted, "zadanie skoku nie zakonczylo sie po zwolnieniu bariery");
        if (seek.IsFaulted) throw seek.Exception!.InnerException!;
        if (harness.Backend.SeekCalls.Count != 1)
        {
            throw new Exception(
                $"Po zwolnieniu bariery wyslano {harness.Backend.SeekCalls.Count} zadan skoku zamiast jednego.");
        }
        checks++;

        // Bramka MUSI byc zwolniona po probie - inaczej odtwarzacz zostaje gluchy.
        if (harness.CommandInFlight) throw new Exception("Po skoku bramka polecen Sonos zostala zamknieta.");
        harness.PumpQuietly(TimeSpan.FromMilliseconds(30));
        var again = harness.Window.ExecuteSonosCommandForTests(CommandIds.VolumeUp1);
        harness.PumpUntil(() => again.IsCompleted, "polecenie po skoku nie zakonczylo sie");
        if (!harness.Backend.Commands.Contains(SonosGroupCommand.SetVolume))
        {
            throw new Exception("Po zakonczonym skoku kolejne polecenie Sonos nie doszlo do backendu.");
        }
        checks += 2;
        return checks;
    }

    /// <summary>
    /// Uwaga odbioru #3: WYJATEK transportu w przedskokowym GET nie moze byc
    /// cisza. Mierzymy OBSERWOWALNE zadanie skoku: zero POST, RZECZYWISTE
    /// ujscie Announce z jawnym "nie wyslano", brak porzuconego fault i brak
    /// ujawnienia tresci wyjatku (token/adres). ODDZIELNIE mierzymy wyjatek
    /// odczytu PO wyslaniu: tam nie wolno twierdzic, ze skok nie poszedl.
    /// </summary>
    private static int MeasurePreSeekReadFaultIsAnnounced()
    {
        var checks = 0;

        // 1. PRZED wyslaniem: skok nie poszedl i tak trzeba to powiedziec.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmConfirm("2:30");
            // WYJATEK dokladnie w PRZEDSKOKOWYM odczycie: uzbrojony w chwili
            // zatwierdzenia okna, wiec nie trafia w odczyt tla.
            responder.BeforeConfirm = () => harness.Backend.FaultNextPlaybackRead = true;
            var said = harness.Announcements.Count;
            var task = harness.StartSeekToPosition(CommandIds.SeekToTime);
            harness.PumpUntil(() => task.IsCompleted, "zadanie skoku nie zakonczylo sie po wyjatku odczytu");

            if (task.IsFaulted)
            {
                throw new Exception(
                    "Wyjatek przedskokowego odczytu UCIEKA z zadania skoku (porzucony fault): "
                    + task.Exception!.InnerException!.GetType().Name + ".");
            }
            checks++;
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception("Po wyjatku przedskokowego odczytu i tak wyslano skok.");
            }
            checks++;
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            if (!text.Contains("nie udało się odczytać", StringComparison.OrdinalIgnoreCase)
                || !text.Contains("nie został wysłany", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Po wyjatku przedskokowego odczytu nie powiedziano, ze odczyt padl i skok nie poszedl: \"{text}\".");
            }
            checks++;
            if (text.Contains(FakeBackend.Secret, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Komunikat ujawnil tresc wyjatku transportu: \"{text}\".");
            }
            checks++;
        }

        // 2. PO wyslaniu: utracona odpowiedz NIE jest dowodem, ze nic nie poszlo.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmConfirm("2:30");
            harness.Backend.FaultReadAfterSeek = true;
            var said = harness.Announcements.Count;
            var task = harness.StartSeekToPosition(CommandIds.SeekToTime);
            harness.PumpUntil(() => task.IsCompleted, "zadanie skoku nie zakonczylo sie po wyjatku odczytu po skoku");
            if (task.IsFaulted)
            {
                throw new Exception(
                    "Wyjatek odczytu PO skoku UCIEKA z zadania skoku: "
                    + task.Exception!.InnerException!.GetType().Name + ".");
            }
            if (harness.Backend.SeekCalls.Count != 1)
            {
                throw new Exception(
                    $"Przypadek odczytu po skoku wyslal {harness.Backend.SeekCalls.Count} zadan zamiast jednego.");
            }
            checks += 2;
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            if (text.Contains("nie został wysłany", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Po utracie odpowiedzi PO wyslaniu powiedziano, ze skok nie poszedl: \"{text}\".");
            }
            if (text.Length == 0)
            {
                throw new Exception("Po wyjatku odczytu PO skoku nie powiedziano nic.");
            }
            checks += 2;
            if (text.Contains(FakeBackend.Secret, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Komunikat po skoku ujawnil tresc wyjatku: \"{text}\".");
            }
            checks++;
        }
        return checks;
    }

    /// <summary>
    /// Uwaga odbioru #2: fokus po zamknieciu okna wraca do MIEJSCA, z ktorego
    /// skok wyszedl, a nie zawsze do przycisku czasu. Mierzymy OBA tryby, OBA
    /// rozstrzygniecia (zatwierdzenie i anulowanie) oraz droge skrotu z fokusem
    /// na Odtwarzaj/Pauza - tam fokus nie ma prawa przeskoczyc na przycisk skoku.
    /// </summary>
    private static int MeasureFocusReturnsToOpeningControl()
    {
        var checks = 0;
        foreach (var buttonName in new[] { "PlayerSeekTimeButton", "PlayerSeekPercentButton" })
        {
            // WARTOSC musi byc poprawna W DANYM TRYBIE: odrzucona przez
            // walidacje nie zamyka okna, a modal zawiesilby caly pomiar.
            var value = buttonName == "PlayerSeekTimeButton" ? "2:30" : "50";
            foreach (var typed in new string?[] { null, value })
            {
                using var harness = Harness.Create();
                harness.OpenPlayerForGroup("GRUPA-SALON");
                var button = (Button)harness.Window.FindName(buttonName)!;
                // DROGA KLAWIATURY: uzytkownik ma fokus na przycisku i go
                // uruchamia. To RZECZYWISTY fokus przed modalem, ktory ma wrocic.
                button.Focus();
                harness.PumpQuietly(TimeSpan.FromMilliseconds(50));
                if (!ReferenceEquals(Keyboard.FocusedElement, button))
                {
                    throw new Exception($"Nie udalo sie ustawic fokusu na {buttonName} przed uruchomieniem.");
                }
                using var responder = typed is null
                    ? SeekDialogResponder.ArmCancel()
                    : SeekDialogResponder.ArmConfirm(typed);
                harness.ClickButton(button);
                harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
                if (responder.Seen is null) throw new Exception($"{buttonName}: okno skoku sie nie otworzylo.");
                Console.WriteLine($"   #2: {buttonName} {(typed is null ? "anulowanie" : "zatwierdzenie")} -> {harness.FocusedElementName()}");
                if (!ReferenceEquals(Keyboard.FocusedElement, button))
                {
                    throw new Exception(
                        $"{buttonName} ({(typed is null ? "anulowanie" : "zatwierdzenie")}): fokus wrocil na "
                        + $"\"{harness.FocusedElementName()}\" zamiast na przycisk, z ktorego skok wyszedl.");
                }
                checks++;
            }
        }

        // DROGA SKROTU: fokus byl na Odtwarzaj/Pauza (Spacja to istniejacy
        // PlayPause, wiec skrot skoku jest jedyna droga z tego miejsca).
        foreach (var commandId in new[] { CommandIds.SeekToTime, CommandIds.SeekToPercentage })
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            harness.PlayPauseButton.Focus();
            harness.PumpQuietly(TimeSpan.FromMilliseconds(50));
            if (!ReferenceEquals(Keyboard.FocusedElement, harness.PlayPauseButton))
            {
                throw new Exception("Nie udalo sie ustawic fokusu na Odtwarzaj/Pauza przed skrotem.");
            }
            using var responder = SeekDialogResponder.ArmCancel();
            harness.ExecuteCommand(commandId);
            harness.PumpQuietly(TimeSpan.FromMilliseconds(150));
            if (responder.Seen is null) throw new Exception($"{commandId}: skrot nie otworzyl okna skoku.");
            Console.WriteLine($"   #2: skrot {commandId} z PlayPause -> {harness.FocusedElementName()}");
            if (!ReferenceEquals(Keyboard.FocusedElement, harness.PlayPauseButton))
            {
                throw new Exception(
                    $"{commandId}: po anulowaniu fokus przeskoczyl z Odtwarzaj/Pauza na "
                    + $"\"{harness.FocusedElementName()}\".");
            }
            checks++;
        }
        return checks;
    }

    /// <summary>
    /// K1: po PORZUCENIU celu w czasie przedskokowego GET-a skok MILCZY.
    /// Przelot wisi na barierze odczytu, a uzytkownik PRAWDZIWA droga polecen
    /// przechodzi do innej sesji (slot). Wymagamy: zadanie sie konczy, ZERO
    /// POST-ow skoku i ZERO nowych komunikatow po opuszczeniu sesji Sonos -
    /// odmowa skoku Sonosa nie ma prawa odezwac sie w cudzym widoku.
    /// </summary>
    private static int MeasureAbandonedTargetSaysNothing()
    {
        var checks = 0;
        using var harness = Harness.Create();
        harness.OpenPlayerForGroup("GRUPA-SALON");

        using var responder = SeekDialogResponder.ArmConfirm("2:30");
        responder.BeforeConfirm = () => harness.Backend.HoldNextPlaybackRead = true;
        var seek = harness.StartSeekToPosition(CommandIds.SeekToTime);
        harness.PumpUntil(
            () => harness.Backend.HeldPlaybackRead is not null,
            "przedskokowy odczyt nie zatrzymal sie na barierze");
        if (harness.Backend.SeekCalls.Count != 0)
        {
            throw new Exception("Bariera zatrzymala odczyt PO wyslaniu skoku, a nie przedskokowy.");
        }
        checks++;

        // PORZUCENIE celu PRAWDZIWA droga: POZNIEJSZY callback kolejki
        // Dispatchera, nie wnetrze atrapy.
        var ticketBefore = harness.Window.SonosTargetTicket;
        harness.PostToDispatcher(() => harness.ExecuteCommand(CommandIds.SessionSlot(3)));
        harness.PumpUntil(
            () => harness.Window.SonosTargetTicket != ticketBefore,
            "przejscie do innej sesji nie uniewaznilo celu Sonos");
        checks++;

        // DOPIERO TERAZ liczymy mowe: przelaczenie sesji samo w sobie mowi.
        harness.PumpQuietly(TimeSpan.FromMilliseconds(60));
        var saidAfterLeaving = harness.Announcements.Count;

        harness.Backend.ReleaseHeldPlaybackRead();
        harness.PumpUntil(() => seek.IsCompleted, "zadanie skoku nie zakonczylo sie po zwolnieniu bariery");
        if (seek.IsFaulted) throw seek.Exception!.InnerException!;
        harness.PumpQuietly(TimeSpan.FromMilliseconds(60));
        checks++;

        if (harness.Backend.SeekCalls.Count != 0)
        {
            throw new Exception(
                $"Po porzuceniu celu i tak wyslano {harness.Backend.SeekCalls.Count} zadan skoku.");
        }
        checks++;
        var late = string.Join(" | ", harness.Announcements.Skip(saidAfterLeaving));
        if (late.Length != 0)
        {
            throw new Exception($"Porzucony skok odezwal sie po wyjsciu z sesji Sonos: \"{late}\".");
        }
        checks++;
        return checks;
    }

    /// <summary>
    /// K2: komunikat po utraconym odczycie NIE moze udawac wyslania. Bierzemy
    /// LEGALNY wynik kontraktu <see cref="SonosGroupCommandResult"/> z
    /// RequestSent=false (Attempted z Outcome.Sent=false, tak jak w wyniku
    /// walidacji transportu) i wymagamy, by przelot powiedzial, ze skok NIE poszedl.
    /// KONTROLA DODATNIA: przy RequestSent=true wolno powiedziec najwyzej o
    /// PODJETEJ PROBIE bez potwierdzenia - bez obietnicy, ze zadanie opuscilo
    /// maszyne albo dotarlo do glosnika.
    ///
    /// GRANICA POMIARU: wynik jest SYNTETYCZNY, zbudowany na granicy API. Nie
    /// mierzymy tu konta, HTTP ani transportu - tylko TEKST, ktory przelot
    /// wybiera dla danego RequestSent.
    /// </summary>
    private static int MeasureUnsentSeekIsNotCalledSent()
    {
        var checks = 0;

        // 1. RequestSent=false: ZERO prob wyslania - i tak trzeba to powiedziec.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmConfirm("2:30");
            // LEGALNY ksztalt kontraktu: proba PODJETA przez koordynatora
            // (Attempted), ale transport NIE wyslal nic - zla konfiguracja.
            // RequestSent=false znaczy w kontrakcie ZERO prob wyslania.
            harness.Backend.NextSeekResult = new SonosGroupCommandResult(
                SonosGroupOperationStatus.Attempted,
                SonosGroupCommand.SeekRelative,
                SonosGroupCommandOutcome.FromStatus(
                    SonosGroupCommand.SeekRelative, SonosControlApiStatus.InvalidConfiguration, false),
                requestSent: false,
                renewed: false,
                SonosAccountSnapshots.Empty);
            harness.Backend.FaultReadAfterSeek = true;
            var said = harness.Announcements.Count;
            var task = harness.StartSeekToPosition(CommandIds.SeekToTime);
            harness.PumpUntil(() => task.IsCompleted, "zadanie skoku nie zakonczylo sie przy RequestSent=false");
            if (task.IsFaulted) throw task.Exception!.InnerException!;
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            Console.WriteLine($"   K2 false: \"{text}\"");
            if (!text.Contains("nie został wysłany", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Przy RequestSent=false nie powiedziano, ze skok nie zostal wyslany: \"{text}\".");
            }
            checks++;
            if (!text.Contains("odczyt", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Przy RequestSent=false nie nazwano nieudanego odczytu: \"{text}\".");
            }
            checks++;
            if (text.Contains(FakeBackend.Secret, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Komunikat ujawnil tresc wyjatku transportu: \"{text}\".");
            }
            checks++;
        }

        // 2. KONTROLA DODATNIA - RequestSent=true: tylko PROBA, bez obietnicy.
        using (var harness = Harness.Create())
        {
            harness.OpenPlayerForGroup("GRUPA-SALON");
            using var responder = SeekDialogResponder.ArmConfirm("2:30");
            // Unreachable, NIE Success: samo Accepted/200 dowodzi PRZYJECIA, wiec
            // nie nadaje sie na dowod, ze brak wyslania zostal nazwany uczciwie.
            // Tu proba BYLA (RequestSent=true i Outcome.Sent=true), ale brak odpowiedzi
            // nie pozwala rozstrzygnac, czy zadanie dotarlo do uslugi.
            harness.Backend.NextSeekResult = new SonosGroupCommandResult(
                SonosGroupOperationStatus.Attempted,
                SonosGroupCommand.SeekRelative,
                SonosGroupCommandOutcome.FromStatus(
                    SonosGroupCommand.SeekRelative, SonosControlApiStatus.Unreachable, true),
                requestSent: true,
                renewed: false,
                SonosAccountSnapshots.Empty);
            harness.Backend.FaultReadAfterSeek = true;
            var said = harness.Announcements.Count;
            var task = harness.StartSeekToPosition(CommandIds.SeekToTime);
            harness.PumpUntil(() => task.IsCompleted, "zadanie skoku nie zakonczylo sie przy RequestSent=true");
            if (task.IsFaulted) throw task.Exception!.InnerException!;
            var text = string.Join(" | ", harness.Announcements.Skip(said));
            Console.WriteLine($"   K2 true: \"{text}\"");
            if (harness.Backend.SeekCalls.Count != 1)
            {
                throw new Exception(
                    $"Kontrola dodatnia wyslala {harness.Backend.SeekCalls.Count} zadan zamiast jednego.");
            }
            checks++;
            if (!text.Contains("prób", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Przy RequestSent=true nie nazwano tego PROBA: \"{text}\".");
            }
            checks++;
            if (!text.Contains("potwierdz", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Przy RequestSent=true nie nazwano braku potwierdzenia: \"{text}\".");
            }
            checks++;
            // Slowa FALSZYWIE przyrzekajace: "wyslany"/"dostarczony"/"dotarl"
            // twierdza o losie zadania poza maszyna, a tego nikt nie zmierzyl.
            foreach (var promise in new[] { "został wysłany", "dostarcz", "dotar" })
            {
                if (text.Contains(promise, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(
                        $"Przy samej PROBIE komunikat obiecuje \"{promise}\": \"{text}\".");
                }
                checks++;
            }
        }
        return checks;
    }

    // ===== B4: przewijanie CUSTOM i CYFROWE istniejaca droga polecen =====

    /// <summary>
    /// B4-1: Alt+Ctrl+strzalka (<see cref="CommandIds.SeekBackwardCustom"/> /
    /// <see cref="CommandIds.SeekForwardCustom"/>) ma w sesji Sonos wysylac
    /// DOKLADNIE JEDEN skok relatywny o dlugosc Z KONFIGURACJI, znormalizowana
    /// istniejaca regula <see cref="PlaybackSeekRules.NormalizeCustomSeekSeconds"/>.
    /// Zmiana ustawienia MUSI zmienic delte w backendzie - inaczej opcja klamie.
    /// Zadnych nowych klawiszy i zadnego nowego endpointu: idzie ta sama
    /// <c>SeekRelativeAsync</c> z ODCZYTANYM itemId aktywnej grupy.
    /// </summary>
    private static int MeasureCustomSeekUsesConfiguredLength()
    {
        var checks = 0;
        // Trzecia para jest granica reguly: 99999 s normalizuje sie do 1800 s.
        foreach (var (configured, expectedSeconds) in new[] { (120, 120), (45, 45), (99_999, 1800) })
        {
            foreach (var (commandId, sign) in new[]
                     {
                         (CommandIds.SeekForwardCustom, 1),
                         (CommandIds.SeekBackwardCustom, -1)
                     })
            {
                using var harness = Harness.Create();
                harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
                harness.Window.StateForTests.Settings.CustomSeekSeconds = configured;
                harness.OpenPlayerForGroup("GRUPA-SALON");
                var demoBefore = harness.DemoSessionPosition;
                var readsBefore = harness.Backend.PlaybackReads;

                var said = harness.AnnounceFor(commandId);

                if (harness.Backend.SeekCalls.Count != 1)
                {
                    throw new Exception(
                        $"{commandId} przy ustawieniu {configured} s: wyslano "
                        + $"{harness.Backend.SeekCalls.Count} zadan skoku zamiast dokladnie jednego. "
                        + $"Powiedziano: \"{said}\".");
                }
                checks++;

                var call = harness.Backend.SeekCalls[0];
                var expectedDelta = sign * expectedSeconds * 1000;
                if (call.DeltaMillis != expectedDelta)
                {
                    throw new Exception(
                        $"{commandId} przy ustawieniu {configured} s: delta {call.DeltaMillis} ms "
                        + $"zamiast {expectedDelta} ms z konfiguracji.");
                }
                checks++;

                if (call.GroupId != "GRUPA-SALON" || call.ItemId != "UTWOR-1")
                {
                    throw new Exception(
                        $"{commandId}: skok poszedl do grupy {call.GroupId} i materialu {call.ItemId}.");
                }
                checks++;

                // JAWNY odczyt po poleceniu: Accepted nie jest dowodem skutku.
                if (harness.Backend.PlaybackReads <= readsBefore)
                {
                    throw new Exception(
                        $"{commandId}: po skoku nie bylo jawnego odczytu stanu grupy.");
                }
                checks++;

                if (harness.DemoSessionPosition != demoBefore)
                {
                    throw new Exception(
                        $"{commandId}: ruszono DemoMediaSession ({demoBefore} -> {harness.DemoSessionPosition}).");
                }
                checks++;
            }
        }
        return checks;
    }

    /// <summary>
    /// B4-2: cyfry 0-9 (<see cref="CommandIds.SeekPercent"/>, czyli 0-90 co 10)
    /// maja w sesji Sonos trafiac w BEZWZGLEDNY cel policzony z ODCZYTANEJ
    /// dlugosci, przelozony na DELTE od SWIEZEGO odczytu pozycji TEJ SAMEJ
    /// grupy i TEGO SAMEGO materialu. Bez modalu i bez nowego endpointu.
    /// Fake czyta pozycje 12 s i dlugosc 3:00, wiec cyfra p daje
    /// (p% * 180 000) - 12 000 ms.
    /// </summary>
    private static int MeasureDigitSeekUsesFreshRead()
    {
        var checks = 0;
        for (var digit = 0; digit <= 9; digit++)
        {
            var percent = digit * 10;
            var commandId = CommandIds.SeekPercent(percent);
            using var harness = Harness.Create();
            harness.Backend.NextPlaybackState = SonosPlaybackState.Paused;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var demoBefore = harness.DemoSessionPosition;
            var readsBefore = harness.Backend.PlaybackReads;

            var said = harness.AnnounceFor(commandId);

            if (harness.Backend.SeekCalls.Count != 1)
            {
                throw new Exception(
                    $"Cyfra {digit} ({commandId}): wyslano {harness.Backend.SeekCalls.Count} zadan "
                    + $"skoku zamiast dokladnie jednego. Powiedziano: \"{said}\".");
            }
            checks++;

            var call = harness.Backend.SeekCalls[0];
            var expectedDelta = percent * 1_800 - 12_000;
            if (call.DeltaMillis != expectedDelta)
            {
                throw new Exception(
                    $"Cyfra {digit}: delta {call.DeltaMillis} ms zamiast {expectedDelta} ms "
                    + "liczonych od swiezo odczytanej pozycji.");
            }
            checks++;

            if (call.GroupId != "GRUPA-SALON" || call.ItemId != "UTWOR-1")
            {
                throw new Exception(
                    $"Cyfra {digit}: skok poszedl do grupy {call.GroupId} i materialu {call.ItemId}.");
            }
            checks++;

            // SWIEZY odczyt PRZED skokiem i JAWNY odczyt PO nim: dwa odczyty
            // ponad stan z wejscia do odtwarzacza.
            if (harness.Backend.PlaybackReads < readsBefore + 2)
            {
                throw new Exception(
                    $"Cyfra {digit}: byl tylko {harness.Backend.PlaybackReads - readsBefore} odczyt "
                    + "stanu, a potrzebny jest swiezy przed skokiem i jawny po nim.");
            }
            checks++;

            if (harness.DemoSessionPosition != demoBefore)
            {
                throw new Exception(
                    $"Cyfra {digit}: ruszono DemoMediaSession ({demoBefore} -> {harness.DemoSessionPosition}).");
            }
            checks++;
        }
        return checks;
    }

    /// <summary>
    /// B4-3: ODMOWY przewijania cyfrowego i custom sa CZYTELNE i wysylaja ZERO
    /// zadan: brak pozycji/dlugosci (radio bez currentItem) oraz CanSeek=false.
    /// Nieprawidlowy identyfikator procentu (np. 35, poza 0-90 co 10) zostaje
    /// odmowa nieobslugiwanego polecenia - wspolny parser sie nie rozjezdza.
    /// </summary>
    private static int MeasureDigitSeekRefusalsSendNothing()
    {
        var checks = 0;

        // 1. RADIO bez currentItem: nie ma ani pozycji, ani dlugosci, wiec
        // BEZWZGLEDNEGO celu procentowego nie da sie policzyc. To dotyczy TYLKO
        // cyfr: przewijanie WZGLEDNE (stale kroki i custom) nie potrzebuje
        // pozycji, bo delte liczy sam Sonos.
        foreach (var commandId in new[] { CommandIds.SeekPercent(50), CommandIds.SeekPercent(0) })
        {
            using var harness = Harness.Create();
            harness.Backend.RadioWithoutCurrentItem = true;
            harness.OpenPlayerForGroup("GRUPA-SALON");

            var said = harness.AnnounceFor(commandId);
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception(
                    $"{commandId} bez odczytanej pozycji wyslal {harness.Backend.SeekCalls.Count} zadan skoku.");
            }
            checks++;
            if (said.Length == 0 || said.Contains("0:00", StringComparison.Ordinal))
            {
                throw new Exception($"{commandId} bez pozycji odpowiedzial zerem z demo: \"{said}\".");
            }
            checks++;
        }

        // 2. CanSeek=false z ODCZYTU: zero zadan, czytelna odmowa.
        foreach (var commandId in new[] { CommandIds.SeekPercent(30), CommandIds.SeekBackwardCustom })
        {
            using var harness = Harness.Create();
            harness.Backend.CanSeekFlag = false;
            harness.OpenPlayerForGroup("GRUPA-SALON");

            var said = harness.AnnounceFor(commandId);
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception(
                    $"{commandId} przy CanSeek=false wyslal {harness.Backend.SeekCalls.Count} zadan skoku.");
            }
            checks++;
            if (!said.Contains("przewijan", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"{commandId} przy CanSeek=false nie nazwal przewijania: \"{said}\".");
            }
            checks++;
        }

        // 3. NIEPRAWIDLOWY identyfikator procentu: odmowa zostaje odmowa.
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var said = harness.AnnounceFor("transport.seekPercent.35");
            if (harness.Backend.SeekCalls.Count != 0)
            {
                throw new Exception(
                    $"Nieprawidlowy procent 35 wyslal {harness.Backend.SeekCalls.Count} zadan skoku.");
            }
            checks++;
            if (!said.Contains("nie jest obsługiwane", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Nieprawidlowy procent 35 nie zostal odmowiony: \"{said}\".");
            }
            checks++;
        }
        return checks;
    }

    /// <summary>
    /// B4-4: Next/Previous ida do SkipToNextTrack / SkipToPreviousTrack AKTYWNEJ
    /// GRUPY: dokladnie JEDEN POST + JAWNY GET stanu, DemoMediaSession nietkniety.
    /// Accepted BEZ nowego (albo bez porownywalnego) itemId NIE jest
    /// potwierdzeniem zmiany - mierzymy oba warianty. Bramki CanSkip i
    /// SkipToPreviousAllowed: false i null daja ZERO POST.
    /// </summary>
    private static int MeasureSkipTargetsGroupAndRespectsGates()
    {
        var checks = 0;

        // 1. Accepted BEZ zmiany materialu: jeden POST, jawny GET, brak obietnicy.
        foreach (var (commandId, expected) in new[]
                 {
                     (CommandIds.Next, SonosGroupCommand.SkipToNextTrack),
                     (CommandIds.Previous, SonosGroupCommand.SkipToPreviousTrack)
                 })
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var demoBefore = harness.DemoSessionPosition;
            var readsBefore = harness.Backend.PlaybackReads;
            var commandsBefore = harness.Backend.Commands.Count;

            var said = harness.AnnounceFor(commandId);
            var sent = harness.Backend.Commands.Skip(commandsBefore).ToList();
            if (sent.Count != 1 || sent[0] != expected)
            {
                throw new Exception(
                    $"{commandId}: wyslano [{string.Join(", ", sent)}] zamiast dokladnie jednego {expected}.");
            }
            checks++;
            if (harness.Backend.CommandGroupIds[^1] != "GRUPA-SALON")
            {
                throw new Exception(
                    $"{commandId}: polecenie poszlo do grupy {harness.Backend.CommandGroupIds[^1]}.");
            }
            checks++;
            if (harness.Backend.PlaybackReads <= readsBefore)
            {
                throw new Exception($"{commandId}: nie bylo jawnego GET-u stanu po poleceniu.");
            }
            checks++;
            if (harness.DemoSessionPosition != demoBefore)
            {
                throw new Exception($"{commandId}: ruszono DemoMediaSession.");
            }
            checks++;
            // ITEM sie NIE zmienil, wiec zmiana pozycji jest NIEPOTWIERDZONA.
            if (!said.Contains("niepotwierdzon", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"{commandId}: Accepted bez nowego materialu ogloszono jako skutek: \"{said}\".");
            }
            checks++;
        }

        // 2. ITEM RZECZYWISCIE sie zmienil w odczycie po POST: dopiero to jest
        // potwierdzeniem. Podmieniamy odczytywany identyfikator z wnetrza
        // samego POST-u, bo liczenie odczytow z gory jest zawodne.
        foreach (var commandId in new[] { CommandIds.Next, CommandIds.Previous })
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            harness.Backend.NextItemIdAfterCommand = "UTWOR-2";

            var said = harness.AnnounceFor(commandId);
            if (harness.Backend.CommandGroupIds[^1] != "GRUPA-SALON")
            {
                throw new Exception($"{commandId}: polecenie poszlo do cudzej grupy.");
            }
            checks++;
            if (!said.Contains("potwierdzona odczytem", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"{commandId}: zmiana materialu UTWOR-1 -> UTWOR-2 nie zostala potwierdzona: \"{said}\".");
            }
            checks++;
        }

        // 3. BRAMKI: false i null to ZERO POST i czytelna odmowa.
        foreach (var (commandId, flag) in new[]
                 {
                     (CommandIds.Next, false), (CommandIds.Previous, false)
                 })
        {
            foreach (var value in new bool?[] { flag, null })
            {
                using var harness = Harness.Create();
                if (commandId == CommandIds.Next) harness.Backend.CanSkipFlag = value;
                else harness.Backend.CanSkipToPreviousFlag = value;
                harness.OpenPlayerForGroup("GRUPA-SALON");
                var commandsBefore = harness.Backend.Commands.Count;

                var said = harness.AnnounceFor(commandId);
                if (harness.Backend.Commands.Count != commandsBefore)
                {
                    throw new Exception(
                        $"{commandId} przy uprawnieniu {Describe(value)} i tak wyslal polecenie.");
                }
                checks++;
                if (!said.Contains("nie zgłasza możliwości", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception(
                        $"{commandId} przy uprawnieniu {Describe(value)} nie odmowil czytelnie: \"{said}\".");
                }
                checks++;
            }
        }
        return checks;
    }

    private static string Describe(bool? value) =>
        value switch { true => "true", false => "false", _ => "null" };

    /// <summary>
    /// B4-5: Glosnosc +-1 / +-5 idzie przez SetGroupVolumeAsync z ODCZYTANEJ
    /// liczby, z clampem 0..100, JEDEN POST + JAWNY GET glosnosci. Mowa i UI
    /// pochodza z ODCZYTU: Accepted bez zmiany odczytu nie jest sukcesem, a
    /// odczyt, ktory RZECZYWISCIE zwraca nowa wartosc - jest.
    /// Sprawdzamy WSZYSTKIE cztery polecenia.
    /// </summary>
    private static int MeasureVolumeStepsUseReadValue()
    {
        var checks = 0;
        foreach (var (commandId, delta) in new[]
                 {
                     (CommandIds.VolumeUp1, 1), (CommandIds.VolumeDown1, -1),
                     (CommandIds.VolumeUp5, 5), (CommandIds.VolumeDown5, -5)
                 })
        {
            // ODCZYT jest zrodlem liczby: startujemy z 30, nie z zera demo.
            using var harness = Harness.Create();
            harness.Backend.ApplyVolumeWrites = true;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var demoBefore = harness.DemoSessionPosition;
            var volumeReadsBefore = harness.Backend.VolumeReads;

            var said = harness.AnnounceFor(commandId);
            if (harness.Backend.VolumeSets.Count != 1)
            {
                throw new Exception(
                    $"{commandId}: wyslano {harness.Backend.VolumeSets.Count} ustawien poziomu "
                    + $"zamiast dokladnie jednego. Powiedziano: \"{said}\".");
            }
            checks++;
            var set = harness.Backend.VolumeSets[0];
            if (set.GroupId != "GRUPA-SALON" || set.Volume != 30 + delta)
            {
                throw new Exception(
                    $"{commandId}: ustawiono {set.Volume} w grupie {set.GroupId}, "
                    + $"a z odczytu 30 wynika {30 + delta} w GRUPA-SALON.");
            }
            checks++;
            if (harness.Backend.VolumeReads <= volumeReadsBefore)
            {
                throw new Exception($"{commandId}: nie bylo jawnego GET-u glosnosci po poleceniu.");
            }
            checks++;
            if (!said.Contains("potwierdzona odczytem", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"{commandId}: odczyt zwrocil zmieniona wartosc, a mowa jej nie potwierdzila: \"{said}\".");
            }
            checks++;
            if (harness.DemoSessionPosition != demoBefore)
            {
                throw new Exception($"{commandId}: ruszono DemoMediaSession.");
            }
            checks++;
        }

        // CLAMP 0..100 z ODCZYTANEJ wartosci, oba konce.
        foreach (var (start, commandId, expected) in new[]
                 {
                     (98, CommandIds.VolumeUp5, 100),
                     (2, CommandIds.VolumeDown5, 0)
                 })
        {
            using var harness = Harness.Create();
            harness.Backend.VolumeValue = start;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            harness.AnnounceFor(commandId);
            if (harness.Backend.VolumeSets.Count != 1 || harness.Backend.VolumeSets[0].Volume != expected)
            {
                throw new Exception(
                    $"{commandId} od {start}: ustawiono "
                    + $"{string.Join(",", harness.Backend.VolumeSets.Select(v => v.Volume))} zamiast {expected}.");
            }
            checks++;
        }

        // ACCEPTED BEZ ZMIANY ODCZYTU nie jest sukcesem (fake nie zapisuje).
        {
            using var harness = Harness.Create();
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var said = harness.AnnounceFor(CommandIds.VolumeUp5);
            if (said.Contains("potwierdzona odczytem", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(
                    $"Accepted bez zmiany odczytanej glosnosci ogloszono jako sukces: \"{said}\".");
            }
            checks++;
        }

        // BRAK ODCZYTU glosnosci i volume.fixed=true: ZERO POST poziomu.
        foreach (var (name, arm) in new (string, Action<FakeBackend>)[]
                 {
                     ("brak odczytu glosnosci", backend => backend.VolumeUnavailable = true),
                     ("volume.fixed=true", backend => backend.FixedVolumeFlag = true)
                 })
        {
            foreach (var commandId in new[]
                     {
                         CommandIds.VolumeUp1, CommandIds.VolumeDown1,
                         CommandIds.VolumeUp5, CommandIds.VolumeDown5
                     })
            {
                using var harness = Harness.Create();
                arm(harness.Backend);
                harness.OpenPlayerForGroup("GRUPA-SALON");
                var said = harness.AnnounceFor(commandId);
                if (harness.Backend.VolumeSets.Count != 0)
                {
                    throw new Exception(
                        $"{commandId} przy {name} wyslal {harness.Backend.VolumeSets.Count} ustawien poziomu.");
                }
                checks++;
                if (said.Length == 0)
                {
                    throw new Exception($"{commandId} przy {name} nic nie powiedzial.");
                }
                checks++;
            }
        }
        return checks;
    }

    /// <summary>
    /// B4-6: MUTE odwraca WYLACZNIE ZNANY bool z odczytu. false -> true,
    /// true -> false, a NIEZNANE wyciszenie to ODMOWA i ZERO POST - zgadniety
    /// bool prowadzilby do odwrotnego skutku w pokoju. volume.fixed=true blokuje
    /// POZIOM, NIE mute: wyciszenie przy stalym poziomie ma dojsc do backendu.
    /// Nie ruszamy globalnego wyciszenia ani ustawien innej sesji.
    /// </summary>
    private static int MeasureMuteInvertsKnownReadOnly()
    {
        var checks = 0;

        foreach (var before in new[] { false, true })
        {
            using var harness = Harness.Create();
            harness.Backend.MutedFlag = before;
            harness.Backend.ApplyVolumeWrites = true;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var volumeReadsBefore = harness.Backend.VolumeReads;

            var said = harness.AnnounceFor(CommandIds.ToggleMuteCurrentSession);
            if (harness.Backend.MuteSets.Count != 1)
            {
                throw new Exception(
                    $"Mute przy odczycie {before}: wyslano {harness.Backend.MuteSets.Count} ustawien "
                    + $"zamiast dokladnie jednego. Powiedziano: \"{said}\".");
            }
            checks++;
            var set = harness.Backend.MuteSets[0];
            if (set.GroupId != "GRUPA-SALON" || set.Muted == before)
            {
                throw new Exception(
                    $"Mute przy odczycie {before}: wyslano {set.Muted} do grupy {set.GroupId} "
                    + "zamiast odwrotnosci odczytu do GRUPA-SALON.");
            }
            checks++;
            if (harness.Backend.VolumeReads <= volumeReadsBefore)
            {
                throw new Exception("Mute: nie bylo jawnego GET-u glosnosci po poleceniu.");
            }
            checks++;
            if (!said.Contains("Wyciszenie ustawione", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception($"Mute przy odczycie {before}: odczyt zmienil sie, a mowa nie: \"{said}\".");
            }
            checks++;
            if (harness.Backend.VolumeSets.Count != 0)
            {
                throw new Exception("Mute ruszyl POZIOM glosnosci, a nie tylko wyciszenie.");
            }
            checks++;
        }

        // NIEZNANE wyciszenie (brak pola muted w odczycie): ODMOWA, zero POST.
        foreach (var arm in new (string Name, Action<FakeBackend> Apply)[]
                 {
                     ("muted=null", backend => backend.MutedFlag = null),
                     ("brak odczytu glosnosci", backend => backend.VolumeUnavailable = true)
                 })
        {
            using var harness = Harness.Create();
            arm.Apply(harness.Backend);
            harness.OpenPlayerForGroup("GRUPA-SALON");
            var said = harness.AnnounceFor(CommandIds.ToggleMuteCurrentSession);
            if (harness.Backend.MuteSets.Count != 0)
            {
                throw new Exception($"Mute przy {arm.Name} wyslal {harness.Backend.MuteSets.Count} ustawien.");
            }
            checks++;
            if (said.Length == 0)
            {
                throw new Exception($"Mute przy {arm.Name} nic nie powiedzial.");
            }
            checks++;
        }

        // volume.fixed=true BLOKUJE POZIOM, NIE wyciszenie.
        {
            using var harness = Harness.Create();
            harness.Backend.FixedVolumeFlag = true;
            harness.Backend.MutedFlag = false;
            harness.OpenPlayerForGroup("GRUPA-SALON");
            harness.AnnounceFor(CommandIds.ToggleMuteCurrentSession);
            if (harness.Backend.MuteSets.Count != 1 || !harness.Backend.MuteSets[0].Muted)
            {
                throw new Exception(
                    "volume.fixed=true zablokowalo WYCISZENIE, a dotyczy tylko poziomu: "
                    + $"{harness.Backend.MuteSets.Count} ustawien.");
            }
            checks++;
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

        /// <summary>
        /// RZECZYWISTY stan bramki polecen Sonos w produkcyjnym oknie - nie
        /// wlasna kopia warunku.
        /// </summary>
        internal bool CommandInFlight => (bool)Field("_sonosCommandInFlight")!;

        /// <summary>Dostepna nazwa elementu, ktory RZECZYWISCIE ma fokus klawiatury.</summary>
        internal string FocusedElementName()
        {
            if (Keyboard.FocusedElement is not DependencyObject focused) return "(brak fokusu)";
            var name = focused is FrameworkElement element ? element.Name : string.Empty;
            var accessible = AutomationProperties.GetName(focused) ?? string.Empty;
            return $"{focused.GetType().Name} {name} \"{accessible}\"";
        }

        /// <summary>
        /// PRAWDZIWY, POZNIEJSZY callback kolejki Dispatchera. Konkurencyjne
        /// polecenie musi wyjsc z osobnego przelotu petli komunikatow, nie z
        /// wnetrza atrapy backendu - inaczej mierzylibysmy rekurencje.
        /// </summary>
        internal void PostToDispatcher(Action action) =>
            _dispatcher.BeginInvoke(DispatcherPriority.Background, action);

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

        /// <summary>
        /// To samo PRODUKCYJNE zadanie skoku, ale ZWROCONE bez czekania: pomiar
        /// bariery i pomiar wyjatku musza obserwowac je w trakcie, a fault nie
        /// moze byc podniesiony za nas.
        /// </summary>
        internal Task StartSeekToPosition(string commandId)
        {
            var method = Window.GetType().GetMethod(
                "SeekSonosToPositionForTests", Instance, binder: null, types: [typeof(string)], modifiers: null)
                ?? throw new Exception("Nie ma prawdziwej drogi SeekSonosToPositionForTests(string).");
            try
            {
                return (Task)method.Invoke(Window, [commandId])!;
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw exception.InnerException;
            }
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

        /// <summary>
        /// ODCZYTANA flaga przewijania. Sonos moze ja odebrac w trakcie
        /// otwartego okna dla TEGO SAMEGO materialu, wiec musi byc zmienna -
        /// wczesniej byla stalym polem i tego przypadku nie dalo sie zmierzyc.
        /// </summary>
        internal bool CanSeekFlag { get; set; } = true;

        /// <summary>
        /// NUMER odczytu stanu, ktory ma ZAWISNAC na barierze (1 = pierwszy).
        /// Sluzy do zmierzenia, czy bramka obowiazuje w czasie GET-a.
        /// </summary>
        internal int? HoldPlaybackReadNumber { get; set; }

        /// <summary>
        /// Zatrzymaj NAJBLIZSZY odczyt stanu. Numerowanie z gory bylo zawodne:
        /// odczyt tla odtwarzacza moze wypasc przed tym, ktory chcemy zmierzyc.
        /// </summary>
        internal bool HoldNextPlaybackRead { get; set; }

        /// <summary>Zatrzymaj NAJBLIZSZY odczyt wyjatkiem transportu.</summary>
        internal bool FaultNextPlaybackRead { get; set; }

        /// <summary>Bariera RZECZYWISCIE trzymajacego odczytu albo null.</summary>
        internal TaskCompletionSource? HeldPlaybackRead { get; private set; }

        /// <summary>NUMER odczytu stanu, ktory ma RZUCIC wyjatek transportu.</summary>
        internal int? FaultPlaybackReadNumber { get; set; }

        /// <summary>
        /// TRESC wyjatku transportu. Komunikat dla uzytkownika NIE ma prawa jej
        /// powtorzyc - to miejsce, w ktorym w prawdziwym kliencie siedzi adres
        /// i naglowek autoryzacji.
        /// </summary>
        internal const string Secret = "token=TAJNE-SONOS-XYZ";

        internal void ReleaseHeldPlaybackRead()
        {
            var held = HeldPlaybackRead;
            HeldPlaybackRead = null;
            held?.TrySetResult();
        }

        private SonosPlaybackActions CurrentActions() => new(
            canPlay: true, canSkip: CanSkipFlag, canSkipBack: null, canSkipToPrevious: CanSkipToPreviousFlag,
            canSeek: CanSeekFlag, canPause: true, canStop: null, canRepeat: null, canRepeatOne: null,
            canCrossfade: null, canShuffle: null);

        /// <summary>
        /// ODCZYTANE availablePlaybackActions.canSkip. Wczesniej bylo stale true;
        /// bramka Next musi dac sie zmierzyc rowniez przy false i null.
        /// </summary>
        internal bool? CanSkipFlag { get; set; } = true;

        /// <summary>ODCZYTANE canSkipToPrevious (SkipToPreviousAllowed).</summary>
        internal bool? CanSkipToPreviousFlag { get; set; } = true;

        internal void ReleaseEverything()
        {
            foreach (var gate in _gates) gate.TrySetResult();
            ReleaseHeldPlaybackRead();
        }

        public async Task<SonosGroupReadResult<SonosGroupPlaybackStatus>> ReadGroupPlaybackAsync(
            string? groupId, CancellationToken cancellationToken)
        {
            var number = ++PlaybackReads;
            if (HoldNextPlaybackRead || HoldPlaybackReadNumber == number)
            {
                HoldNextPlaybackRead = false;
                HoldPlaybackReadNumber = null;
                var gate = new TaskCompletionSource();
                _gates.Add(gate);
                HeldPlaybackRead = gate;
                await gate.Task.ConfigureAwait(true);
            }
            if (FaultNextPlaybackRead || FaultPlaybackReadNumber == number)
            {
                FaultNextPlaybackRead = false;
                FaultPlaybackReadNumber = null;
                // WYJATEK transportu, taki jak przy timeoucie HTTP: tresc zawiera
                // dane, ktorych komunikat dla uzytkownika nie moze powtorzyc.
                throw new TimeoutException("Sonos Control API nie odpowiedzial: " + Secret);
            }
            var status = new SonosGroupPlaybackStatus(
                NextPlaybackState, null, null,
                RadioWithoutCurrentItem ? null : CurrentItemId,
                RadioWithoutCurrentItem ? null : PositionMillis,
                null, null, null, CurrentActions());
            return SonosGroupReadResult<SonosGroupPlaybackStatus>.Success(status);
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
            if (VolumeUnavailable)
            {
                return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Failure(
                    SonosDeviceReadStatus.ServiceError));
            }

            return Task.FromResult(SonosGroupReadResult<SonosGroupVolume>.Success(
                new SonosGroupVolume(VolumeValue, MutedFlag, FixedVolumeFlag)));
        }

        /// <summary>ODCZYTANY poziom; domyslnie ten sam co dotad (30).</summary>
        internal int VolumeValue { get; set; } = 30;

        /// <summary>
        /// ODCZYTANE wyciszenie. null udaje odpowiedz BEZ pola muted - wtedy
        /// przelacznik nie ma czego odwrocic i musi odmowic.
        /// </summary>
        internal bool? MutedFlag { get; set; } = false;

        /// <summary>volume.fixed z odczytu: blokuje POZIOM, nie wyciszenie.</summary>
        internal bool? FixedVolumeFlag { get; set; } = false;

        /// <summary>Odczyt glosnosci sie NIE udaje - brak danych, nie zero.</summary>
        internal bool VolumeUnavailable { get; set; }

        /// <summary>
        /// Gdy true, POST glosnosci/wyciszenia RZECZYWISCIE zmienia odczytywana
        /// wartosc - tylko wtedy werdykt moze byc potwierdzony. Domyslnie false,
        /// wiec istniejace pomiary mierza dokladnie to samo co wczesniej i mamy
        /// przypadek "Accepted bez zmiany odczytu".
        /// </summary>
        internal bool ApplyVolumeWrites { get; set; }

        public Task<SonosGroupCommandResult> SendGroupCommandAsync(
            string? groupId, SonosGroupCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            CommandGroupIds.Add(groupId);
            ApplyItemChangeAfterCommand();
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(command));
        }

        /// <summary>
        /// Gdy ustawione, PIERWSZE polecenie zmienia ODCZYTYWANY identyfikator
        /// materialu - tylko wtedy nastepny GET moze potwierdzic skip. Zapis z
        /// wnetrza POST-u, bo liczenie odczytow z gory jest zawodne (odczyt tla).
        /// </summary>
        internal string? NextItemIdAfterCommand { get; set; }

        private void ApplyItemChangeAfterCommand()
        {
            if (NextItemIdAfterCommand is not { } next) return;
            NextItemIdAfterCommand = null;
            CurrentItemId = next;
        }

        /// <summary>RZECZYWISTE argumenty KAZDEGO skoku: delta i itemId celu.</summary>
        internal List<(string? GroupId, int DeltaMillis, string? ItemId)> SeekCalls { get; } = [];

        /// <summary>
        /// Wyjatek transportu ma trafic w odczyt PO skoku. Uzbrajamy go z wnetrza
        /// samego POST-u, bo liczenie odczytow z gory bylo zawodne (odczyt tla).
        /// </summary>
        internal bool FaultReadAfterSeek { get; set; }

        /// <summary>
        /// WYNIK NASTEPNEGO skoku, gdy pomiar potrzebuje INNEGO niz przyjety:
        /// np. LEGALNEGO wyniku kontraktu z RequestSent=false. Null znaczy
        /// dotychczasowe zachowanie, wiec istniejace pomiary mierza to samo.
        /// </summary>
        internal SonosGroupCommandResult? NextSeekResult { get; set; }

        public Task<SonosGroupCommandResult> SeekRelativeAsync(
            string? groupId, int deltaMillis, string? itemId, CancellationToken cancellationToken)
        {
            SeekCalls.Add((groupId, deltaMillis, itemId));
            if (FaultReadAfterSeek)
            {
                FaultReadAfterSeek = false;
                FaultNextPlaybackRead = true;
            }
            Commands.Add(SonosGroupCommand.SeekRelative);
            CommandGroupIds.Add(groupId);
            var forced = NextSeekResult;
            NextSeekResult = null;
            return Task.FromResult(forced
                ?? SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SeekRelative));
        }

        public Task<SonosGroupCommandResult> SetGroupVolumeAsync(
            string? groupId, int volume, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetVolume);
            CommandGroupIds.Add(groupId);
            VolumeSets.Add((groupId, volume));
            if (ApplyVolumeWrites) VolumeValue = volume;
            return Task.FromResult(SonosGroupCommandResult.CreateAcceptedForMeasurement(SonosGroupCommand.SetVolume));
        }

        /// <summary>RZECZYWISTE argumenty KAZDEGO ustawienia poziomu.</summary>
        internal List<(string? GroupId, int Volume)> VolumeSets { get; } = [];

        /// <summary>RZECZYWISTE argumenty KAZDEGO ustawienia wyciszenia.</summary>
        internal List<(string? GroupId, bool Muted)> MuteSets { get; } = [];

        public Task<SonosGroupCommandResult> SetGroupMuteAsync(
            string? groupId, bool muted, CancellationToken cancellationToken)
        {
            Commands.Add(SonosGroupCommand.SetMute);
            CommandGroupIds.Add(groupId);
            MuteSets.Add((groupId, muted));
            if (ApplyVolumeWrites) MutedFlag = muted;
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
