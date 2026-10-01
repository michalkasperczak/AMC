using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using AccessibleMediaController.Core.Commands;
using AccessibleMediaController.Core.Sonos;
using AccessibleMediaController.Windows;

/// <summary>
/// F3c TRIAGE: MALY pomiar NIEZMIENIONEGO szkicu uruchamiania ulubionych Sonos.
///
/// To NIE jest zestaw docelowy ani dowod TDD: kod produkcyjny F3c napisano PRZED
/// tym plikiem (brak RED przed produkcja - autor szkicu padl przed jakimkolwiek
/// uruchomieniem testu). Ten plik ma JEDNO zadanie: rozstrzygnac HIPOTEZY o
/// zachowaniu szkicu pomiarem, zanim ktokolwiek cokolwiek naprawi.
///
/// Zero HTTP, zero tokenu, zero konta, zero audio, zero prawdziwego Sonosa.
/// Kazdy przypadek jedzie PRAWDZIWA droga WPF: pokazane okno, prawdziwy
/// PreviewKeyDown z zywym urzadzeniem klawiatury, prawdziwe klikniecie przycisku,
/// prawdziwa pompa dispatchera i oczekiwanie na DOKLADNIE to zadanie.
/// </summary>
internal static class SonosFavoritePlayUiTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    internal static void Run()
    {
        var checks = 0;
        Exception? failure = null;
        List<SonosFavoritePlayLateAnswerTests.Verdict> lateVerdicts = [];
        var thread = new Thread(() =>
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
                void Stage(string name) => Console.Error.WriteLine("ETAP: " + name);
                Stage("P1"); checks += MeasureReadOnlyWindowSwallowsEnter();
                Stage("P2"); checks += MeasureDuplicateNamesSendExactSelectedId();
                Stage("P3"); checks += MeasurePlayButtonTakesSamePath();
                Stage("P4"); checks += MeasureHeldAttemptRefusesSecondPost();
                Stage("P5"); checks += MeasureNoGroupAndEmptyListNeverPost();
                Stage("N1"); checks += MeasureFocusedPlayButtonKeepsFocus();
                Stage("N1-choice"); checks += MeasureDeliberateFocusIsNotRestored();
                Stage("N2"); checks += MeasureImmediateFeedbackHasNoPending();
                Stage("N3"); checks += MeasureNotSentInstructionNamesRealRecoveryPath();
                Stage("KONIEC-OKNO");
                // ETAP B: SPOZNIONY WYNIK na PRODUKCYJNEJ drodze okna glownego.
                // Werdykty zbieramy WSZYSTKIE - triage ma rozstrzygnac kazda
                // hipoteze, nie zatrzymac sie na pierwszej odrzuconej.
                lateVerdicts = SonosFavoritePlayLateAnswerTests.RunAll();
                Stage("KONIEC-SPOZNIONY");
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
        if (!thread.Join(TimeSpan.FromSeconds(120)))
        {
            throw new Exception("Limit czasu pomiaru F3c: patrz ostatni ETAP na stderr.");
        }

        if (failure is not null) throw failure;

        // RAPORT HIPOTEZ: kazdy werdykt JAWNIE, POTWIERDZONA/ODRZUCONA, zeby
        // triage dal sie odczytac z loga bez zagladania do kodu.
        foreach (var verdict in lateVerdicts)
        {
            Console.WriteLine(
                (verdict.Passed ? "HIPOTEZA-POTWIERDZONA " : "HIPOTEZA-ODRZUCONA ")
                + verdict.Case + ": " + verdict.Hypothesis + " -> " + verdict.Detail);
        }

        checks += lateVerdicts.Count;
        var rejected = lateVerdicts.Where(verdict => !verdict.Passed).ToArray();
        Console.WriteLine(
            "OK: F3c triage - uruchamianie ulubionych Sonos z okna, prawdziwy Enter i spóźniony wynik "
            + $"({checks} sprawdzeń)");
        if (rejected.Length != 0)
        {
            throw new Exception(
                "F3c: pomiar ODRZUCIL " + rejected.Length + " z " + lateVerdicts.Count
                + " hipotez spóźnionego wyniku: "
                + string.Join(" || ", rejected.Select(verdict => verdict.Case + " " + verdict.Detail)));
        }
    }

    // ===== P1: wariant TYLKO PODGLAD - Enter POCHLONIETY, ZERO callbacka =====

    private static int MeasureReadOnlyWindowSwallowsEnter()
    {
        using var ui = Fixture.ShowReadOnly(Favorites(("ULU-1", "Nokturny", null, null)));
        if (ui.Window.OffersPlaybackForTests)
        {
            throw new Exception("Wariant tylko do odczytu deklaruje drogę odtwarzania.");
        }
        if (ui.Window.PlayVisibleForTests)
        {
            throw new Exception("Wariant tylko do odczytu pokazuje przycisk Odtwórz.");
        }

        ui.PressEnterOnList();
        if (ui.Window.LoadCallsForTests != 0)
        {
            throw new Exception($"Enter w podglądzie wywołał {ui.Window.LoadCallsForTests} razy uruchomienie.");
        }
        if (ui.Window.LastPlayTaskForTests is not null)
        {
            throw new Exception("Enter w podglądzie rozpoczął zadanie uruchomienia.");
        }
        if (!ui.Window.IsVisible)
        {
            throw new Exception("Enter zamknął okno podglądu.");
        }
        if (!ui.Window.StatusForTests.Contains("nie udostępnia uruchamiania", StringComparison.Ordinal))
        {
            throw new Exception("Podgląd nie wyjaśnił braku uruchamiania: \"" + ui.Window.StatusForTests + "\".");
        }

        return 5;
    }

    // ===== P2: DWIE TE SAME NAZWY, ROZNE ID - jedzie ID Z WIERSZA, nie z nazwy =====

    private static int MeasureDuplicateNamesSendExactSelectedId()
    {
        var sent = new List<string?>();
        using var ui = Fixture.ShowWithPlay(
            Favorites(
                ("ULU-1", "Radio Nowy Świat", null, null),
                ("ULU-7", "Radio Nowy Świat", null, null),
                ("ULU-9", "Radio Nowy Świat", null, null)),
            "Salon",
            favorite =>
            {
                sent.Add(favorite.Id);
                return Task.CompletedTask;
            });

        if (!ui.Window.OffersPlaybackForTests || !ui.Window.PlayVisibleForTests || !ui.Window.PlayEnabledForTests)
        {
            throw new Exception("Wariant z uruchamianiem nie udostępnia przycisku Odtwórz.");
        }

        var labels = ui.Window.RowLabelsForTests;
        if (labels.Count != 3 || labels.Distinct(StringComparer.Ordinal).Count() != 1)
        {
            throw new Exception("Trzy różne identyfikatory nie dały trzech wierszy o tej samej nazwie: "
                + string.Join(" | ", labels));
        }

        // ZAZNACZENIE drugiej pozycji o TEJ SAMEJ nazwie: wybor idzie po WIERSZU.
        ui.SelectIndex(1);
        ui.PressEnterOnList();
        ui.AwaitPlay();

        if (sent.Count != 1) throw new Exception($"Enter wysłał {sent.Count} zamiast 1 uruchomienia.");
        if (sent[0] != "ULU-7")
        {
            throw new Exception("Enter wysłał \"" + sent[0] + "\" zamiast identyfikatora zaznaczonego wiersza ULU-7.");
        }
        if (ui.Window.SelectedIndexForTests != 1)
        {
            throw new Exception($"Po próbie zaznaczenie stoi na {ui.Window.SelectedIndexForTests} zamiast 1.");
        }
        if (!ui.Window.IsVisible) throw new Exception("Modal ulubionych zamknął się po próbie uruchomienia.");
        if (!ui.Window.ListHasFocusForTests)
        {
            throw new Exception("Po próbie fokus nie wrócił na listę ulubionych.");
        }
        if (!ui.Window.PlayEnabledForTests)
        {
            throw new Exception("Po zakończonej próbie przycisk Odtwórz został wyłączony.");
        }

        return 8;
    }

    // ===== P3: PRZYCISK Odtworz - TA SAMA droga co Enter =====

    private static int MeasurePlayButtonTakesSamePath()
    {
        var sent = new List<string?>();
        using var ui = Fixture.ShowWithPlay(
            Favorites(("ULU-1", "Nokturny", null, null), ("ULU-2", "Poranek", null, null)),
            "Salon",
            favorite =>
            {
                sent.Add(favorite.Id);
                return Task.CompletedTask;
            });

        ui.SelectIndex(1);
        ui.ClickPlay();
        ui.AwaitPlay();

        if (sent.Count != 1 || sent[0] != "ULU-2")
        {
            throw new Exception("Przycisk Odtwórz wysłał " + sent.Count + " x \"" + sent.FirstOrDefault()
                + "\" zamiast jednego ULU-2.");
        }
        if (ui.Window.LoadCallsForTests != 1)
        {
            throw new Exception($"Okno policzyło {ui.Window.LoadCallsForTests} wywołań zamiast 1.");
        }
        if (!ui.Window.IsVisible) throw new Exception("Przycisk Odtwórz zamknął okno.");

        return 4;
    }

    // ===== P4: TRWAJACA proba - drugie Enter i drugi klik ZERO drugiego POST =====

    private static int MeasureHeldAttemptRefusesSecondPost()
    {
        var held = new TaskCompletionSource();
        var calls = 0;
        using var ui = Fixture.ShowWithPlay(
            Favorites(("ULU-1", "Nokturny", null, null)),
            "Salon",
            _ =>
            {
                calls++;
                return held.Task;
            });
        try
        {
            ui.PressEnterOnList();
            ui.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (calls != 1) throw new Exception($"Pierwszy Enter dał {calls} wywołań zamiast 1.");
            if (ui.Window.PlayEnabledForTests)
            {
                throw new Exception("Podczas trwającej próby przycisk Odtwórz został włączony.");
            }

            ui.PressEnterOnList();
            ui.PressEnterOnList();
            ui.ClickPlay();
            ui.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (calls != 1)
            {
                throw new Exception($"Powtórzone Enter/klik dały {calls} wywołań zamiast 1 (drugi POST).");
            }
            if (!ui.Window.StatusForTests.Contains("jeszcze się nie zakończyło", StringComparison.Ordinal))
            {
                throw new Exception("Powtórka nie dostała wyjaśnienia: \"" + ui.Window.StatusForTests + "\".");
            }
        }
        finally
        {
            held.TrySetResult();
            ui.AwaitPlay();
        }

        if (calls != 1) throw new Exception($"Po zwolnieniu liczba wywołań to {calls} zamiast 1.");
        if (!ui.Window.PlayEnabledForTests)
        {
            throw new Exception("Po zakończeniu próby przycisk Odtwórz nie wrócił do użycia.");
        }

        return 6;
    }

    // ===== P5: BRAK GRUPY i PUSTA LISTA - zero callbacka, uczciwe wyjasnienie =====

    private static int MeasureNoGroupAndEmptyListNeverPost()
    {
        var checks = 0;
        var calls = 0;
        Task Load(SonosFavorite _)
        {
            calls++;
            return Task.CompletedTask;
        }

        using (var ui = Fixture.ShowWithPlay(Favorites(("ULU-1", "Nokturny", null, null)), null, Load))
        {
            if (!ui.Window.PlayVisibleForTests || ui.Window.PlayEnabledForTests)
            {
                throw new Exception("Bez aktywnej grupy przycisk Odtwórz nie jest widoczny i wyłączony.");
            }

            ui.PressEnterOnList();
            ui.ClickPlay();
            ui.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (calls != 0) throw new Exception($"Bez grupy poszło {calls} uruchomień.");
            if (!ui.Window.StatusForTests.Contains("wymaga aktywnej grupy", StringComparison.Ordinal))
            {
                throw new Exception("Brak grupy bez wyjaśnienia: \"" + ui.Window.StatusForTests + "\".");
            }
            if (!ui.Window.IsVisible) throw new Exception("Odmowa zamknęła okno.");
            checks += 4;
        }

        using (var ui = Fixture.ShowWithPlay(Favorites(), "Salon", Load))
        {
            if (ui.Window.RowCountForTests != 0) throw new Exception("Pusta lista ma wiersze.");
            if (ui.Window.PlayEnabledForTests) throw new Exception("Pusta lista zostawia Odtwórz włączone.");
            ui.PressEnterOnList();
            ui.ClickPlay();
            ui.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (calls != 0) throw new Exception($"Pusta lista wysłała {calls} uruchomień.");
            if (!ui.Window.IntroductionForTests.Contains("nie ma zapisanych ulubionych", StringComparison.Ordinal))
            {
                throw new Exception("Pusty stan nie jest powiedziany: \"" + ui.Window.IntroductionForTests + "\".");
            }
            checks += 4;
        }

        return checks;
    }

    // ===== N3: INSTRUKCJA POWROTU wskazuje DROGE, ktora naprawde dziala =====

    /// <summary>
    /// ZMIERZONE POD NVDA (parent 905): po PUBLICZNEJ zmianie konta lista glowna
    /// jest PUSTA (skasowany dom), wiec "otworz liste" NIE naprawia wyboru grupy -
    /// ani Ctrl+L, ani Ctrl+F5 (to tylko podglad). FIZYCZNIE zmierzona droga to
    /// akcja "Wybierz dom Sonos" (paleta Ctrl+Shift+K albo menu Plik), potem
    /// wybor grupy. Test PRZYPINA tekst do ISTNIEJACEJ akcji o tej nazwie, zeby
    /// instrukcja nie mogla znow wskazywac drogi bez skutku.
    /// </summary>
    private static int MeasureNotSentInstructionNamesRealRecoveryPath()
    {
        var checks = 0;

        // Akcja o tej nazwie MUSI ISTNIEC w produkcji - inaczej instrukcja klamie.
        // Pytamy PRODUKCYJNY katalog polecen, nie kopie nazwy w tescie.
        var displayed = CommandCatalog.GetDisplayName(CommandIds.ChooseSonosHousehold);
        if (displayed != SonosChooseHouseholdActionName)
        {
            throw new Exception("Polecenie wyboru domu nazywa się w programie \"" + displayed
                + "\", a instrukcja mówi \"" + SonosChooseHouseholdActionName + "\".");
        }

        checks++;

        foreach (var (name, text) in new[]
        {
            ("PlayNotSentAccountChanged", MainWindow.PlayNotSentAccountChanged),
            ("PlayNotSentTargetChanged", MainWindow.PlayNotSentTargetChanged)
        })
        {
            if (!text.Contains(SonosChooseHouseholdActionName, StringComparison.Ordinal))
            {
                throw new Exception(name + " nie wskazuje zmierzonej drogi odzyskania \""
                    + SonosChooseHouseholdActionName + "\": \"" + text + "\".");
            }

            checks++;

            // ZMIERZONE: po zmianie konta lista glowna jest PUSTA, wiec samo
            // "otwórz listę" NIE naprawia wyboru grupy. Ponowne otwarcie listy
            // jest dopuszczalne WYLACZNIE jako KROK KONCOWY - po wyborze domu i
            // grupy. Sprawdzamy wiec KOLEJNOSC, nie samo wystapienie.
            var openList = text.IndexOf("otwórz listę", StringComparison.OrdinalIgnoreCase);
            if (openList >= 0)
            {
                var chooseHousehold = text.IndexOf(SonosChooseHouseholdActionName, StringComparison.Ordinal);
                if (chooseHousehold > openList)
                {
                    throw new Exception(name + " każe otworzyć listę PRZED wyborem domu, a to nie "
                        + "naprawia wyboru grupy: \"" + text + "\".");
                }
            }

            checks++;

            // Po wyborze domu MUSI byc wybor grupy - bez niego POST nie ma celu.
            if (!text.Contains("grup", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(name + " nie mówi o wyborze grupy: \"" + text + "\".");
            }

            checks++;

            // KROTKO i NIEOSOBOWO: zadnych obietnic, zadnego tlumaczenia sie.
            if (text.Contains("obiecuj", StringComparison.OrdinalIgnoreCase)
                || text.Contains("niestety", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception(name + " obiecuje albo tłumaczy się zamiast podać drogę: \""
                    + text + "\".");
            }

            checks++;

            // PELNE polskie znaki - czytnik ma przeczytac slowa, nie kalki.
            if (!text.Contains('ó') && !text.Contains('ę') && !text.Contains('ł')
                && !text.Contains('ą') && !text.Contains('ś'))
            {
                throw new Exception(name + " nie ma pełnych polskich znaków: \"" + text + "\".");
            }

            checks++;
        }

        return checks;
    }

    /// <summary>NAZWA akcji wyboru domu, odczytana ze ZRODLA produkcji.</summary>
    private const string SonosChooseHouseholdActionName = "Wybierz dom Sonos";

    // ===== N1: FOKUS na przycisku Odtworz ZOSTAJE na przycisku =====

    /// <summary>
    /// ZMIERZONE POD NVDA (parent 905, before-button.txt/after-button.txt): fizyczna
    /// Spacja na SKUPIONYM Odtworz przy WSTRZYMANYM POST przenosila fokus czytnika
    /// na CALY dialog (rola 4) i powodowala POWTORNE przeczytanie wstepu okna
    /// ("Ulubione Sonos dialog" + "Lista ulubionych Sonos. Odtwórz albo Enter...").
    /// Fokus zostawal na dialogu TAKZE po zakonczeniu dokladnego zadania - dopiero
    /// Shift+Tab wracal na liste.
    ///
    /// PRZYCZYNA W ZRODLE: RunPlayAsync wylacza PlayButton (IsEnabled=false), a WPF
    /// zabiera fokus wylaczonej kontrolce i oddaje go oknu. finally zapamietalo
    /// TYLKO focusWasInList, wiec przycisku nikt nie przywracal.
    ///
    /// WYMAGANIE: fokus MA ZOSTAC na przycisku Odtworz - podczas proby i po niej.
    /// Zaden samoczynny przeskok na liste "dla wygody": uzytkownik stoi tam, gdzie
    /// nacisnal. ZERO drugiego POST musi sie utrzymac.
    /// </summary>
    private static int MeasureFocusedPlayButtonKeepsFocus()
    {
        var held = new TaskCompletionSource();
        var calls = 0;
        using var ui = Fixture.ShowWithPlay(
            Favorites(("ULU-1", "Nokturny", null, null), ("ULU-2", "Poranek", null, null)),
            "Salon",
            _ =>
            {
                calls++;
                return held.Task;
            });

        string duringFocus;
        try
        {
            ui.FocusPlay();
            if (!ui.Window.PlayHasFocusForTests)
            {
                throw new Exception("Aparatura nie postawiła fokusu na Odtwórz: \""
                    + ui.Window.FocusedElementNameForTests + "\".");
            }

            // PRAWDZIWA Spacja na skupionym przycisku - ta sama droga, ktora
            // zmierzono fizycznie pod NVDA.
            ui.PressSpaceOnPlay();
            ui.PumpQuietly(TimeSpan.FromMilliseconds(120));
            if (calls != 1) throw new Exception($"Spacja na Odtwórz dała {calls} wywołań zamiast 1.");

            duringFocus = ui.Window.FocusedElementNameForTests;
            if (!ui.Window.PlayHasFocusForTests)
            {
                throw new Exception(
                    "PODCZAS trwającej próby fokus zszedł z przycisku Odtwórz na \"" + duringFocus
                    + "\" - to jest regresja zmierzona pod NVDA (czytnik wraca na cały dialog "
                    + "i powtarza wstęp okna).");
            }

            // POWTORKA przy skupionym przycisku nadal NIE MA prawa wyslac drugiego
            // POST: fokus zostaje, ale bramka _playInFlight dziala.
            ui.PressSpaceOnPlay();
            ui.ClickPlay();
            ui.PumpQuietly(TimeSpan.FromMilliseconds(80));
            if (calls != 1)
            {
                throw new Exception($"Powtórka na skupionym Odtwórz dała {calls} wywołań zamiast 1.");
            }
            if (!ui.Window.StatusForTests.Contains("jeszcze się nie zakończyło", StringComparison.Ordinal))
            {
                throw new Exception("Powtórka na Odtwórz bez wyjaśnienia: \""
                    + ui.Window.StatusForTests + "\".");
            }
        }
        finally
        {
            held.TrySetResult();
            ui.AwaitPlay();
        }

        // PO DOKLADNYM ZADANIU (nie po Close): fokus NADAL na przycisku. Parent
        // zmierzyl, ze tu zostawal dialog.
        var afterFocus = ui.Window.FocusedElementNameForTests;
        if (!ui.Window.PlayHasFocusForTests)
        {
            throw new Exception(
                "PO zakończeniu dokładnego zadania fokus stoi na \"" + afterFocus
                + "\" zamiast na przycisku Odtwórz (podczas próby był \"" + duringFocus
                + "\") - użytkownik musi wracać Shift+Tab.");
        }

        if (!ui.Window.PlayEnabledForTests)
        {
            throw new Exception("Po zakończonej próbie Odtwórz nie wrócił do użycia.");
        }
        if (calls != 1) throw new Exception($"Po zwolnieniu liczba wywołań to {calls} zamiast 1.");
        if (!ui.Window.IsVisible) throw new Exception("Próba z przycisku zamknęła okno.");

        return 8;
    }

    private static int MeasureDeliberateFocusIsNotRestored()
    {
        foreach (var fromButton in new[] { true, false })
        {
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var ui = Fixture.ShowWithPlay(Favorites(("ULU-1", "Nokturny", null, null)),
                "Salon", _ => held.Task);
            try
            {
                if (fromButton) { ui.FocusPlay(); ui.PressSpaceOnPlay(); }
                else ui.PressEnterOnList();
                var close = (Button)ui.Window.FindName("CloseButton")!;
                close.Focus(); Keyboard.Focus(close);
                if (!close.IsKeyboardFocused) throw new Exception("Aparatura nie przeszła na Zamknij.");
                held.TrySetResult();
                ui.AwaitPlay();
                if (!close.IsKeyboardFocused)
                    throw new Exception("N1-choice: koniec zadania odebrał świadomie wybrany fokus Zamknij.");
            }
            finally { held.TrySetResult(); ui.AwaitPlay(); }
        }
        return 4;
    }

    private static int MeasureImmediateFeedbackHasNoPending()
    {
        foreach (var readStillPending in new[] { false, true })
        {
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            SonosFavoritesWindow? origin = null;
            using var ui = Fixture.ShowWithPlay(Favorites(("ULU-1", "Nokturny", null, null)),
                "Salon", _ =>
                {
                    origin!.AnnounceForOwner("Wynik polecenia znany przed powrotem callbacka.");
                    return readStillPending ? held.Task : Task.CompletedTask;
                });
            origin = ui.Window;
            try
            {
                ui.PressEnterOnList();
                if (origin.AnnouncementsForTests != 1
                    || origin.StatusForTests != "Wynik polecenia znany przed powrotem callbacka.")
                    throw new Exception("N2: znany wynik konkuruje z niepotrzebnym Czekaj.");
            }
            finally { held.TrySetResult(); ui.AwaitPlay(); }
        }
        return 4;
    }

    // ==================== aparatura ====================

    private static IReadOnlyList<SonosFavorite> Favorites(
        params (string Id, string Name, string? Service, string? Description)[] items) =>
        items.Select(item => new SonosFavorite(
                item.Id,
                item.Name,
                item.Description,
                item.Service is null ? null : new SonosFavoriteService(item.Service, "SVC-9")))
            .ToArray();

    /// <summary>
    /// POKAZANE okno ulubionych z prawdziwa pompa dispatchera. Okno idzie przez
    /// <c>Show</c>, wiec watek pomiaru moze wysylac klawisze; zamkniecie jest
    /// jawne w <c>Dispose</c>, a fokus mierzymy WYLACZNIE przy widocznym oknie.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

        private Fixture(SonosFavoritesWindow window) => Window = window;

        internal SonosFavoritesWindow Window { get; }

        internal static Fixture ShowReadOnly(IReadOnlyList<SonosFavorite> favorites) =>
            Show(new SonosFavoritesWindow(favorites));

        /// <summary>
        /// WARIANT Z URUCHAMIANIEM. Okno oddaje callbackowi NIEZMIENNE zlecenie
        /// (<c>PlayRequest</c>) z TOZSAMOSCIA zlecajacej instancji; te przypadki
        /// mierza zachowanie SAMEGO okna, wiec aparatura rozpakowuje ulubiony,
        /// ale SPRAWDZA, ze zlecajacym jest dokladnie to okno.
        /// </summary>
        internal static Fixture ShowWithPlay(
            IReadOnlyList<SonosFavorite> favorites, string? groupName, Func<SonosFavorite, Task> load)
        {
            SonosFavoritesWindow? created = null;
            var window = new SonosFavoritesWindow(favorites, groupName, request =>
            {
                if (!ReferenceEquals(request.Origin, created))
                {
                    throw new Exception("Zlecenie uruchomienia nie niesie tożsamości okna, które je zleciło.");
                }
                if (request.Lifetime.IsCancellationRequested)
                {
                    throw new Exception("Token życia otwartego okna jest już anulowany.");
                }

                return load(request.Favorite);
            });
            created = window;
            return Show(window);
        }

        private static Fixture Show(SonosFavoritesWindow window)
        {
            var fixture = new Fixture(window);
            window.ShowInTaskbar = false;
            window.Show();
            fixture.PumpUntil(
                () => window.IsLoaded && PresentationSource.FromVisual(window) is not null,
                "okno ulubionych się nie pokazało");
            window.Activate();
            fixture.PumpQuietly(TimeSpan.FromMilliseconds(120));
            return fixture;
        }

        internal void SelectIndex(int index)
        {
            var list = (ListBox)Window.FindName("FavoritesList")!;
            list.SelectedIndex = index;
            list.UpdateLayout();
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
            {
                item.Focus();
                Keyboard.Focus(item);
            }

            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        /// <summary>
        /// PRAWDZIWY Enter: zywe urzadzenie klawiatury, zrodlo prezentacji tego
        /// okna i tunelujacy <c>PreviewKeyDown</c> - dokladnie to zdarzenie, ktore
        /// obsluguje produkcja. Zaden bezposredni wywolanie metody prywatnej.
        /// </summary>
        internal void PressEnterOnList()
        {
            var list = (ListBox)Window.FindName("FavoritesList")!;
            if (!list.IsKeyboardFocusWithin)
            {
                list.Focus();
                Keyboard.Focus(list);
                PumpQuietly(TimeSpan.FromMilliseconds(40));
            }

            var target = Keyboard.FocusedElement as UIElement ?? list;
            var source = PresentationSource.FromVisual(Window)
                ?? throw new Exception("Okno ulubionych nie ma powierzchni prezentacji.");
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Enter)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            PumpQuietly(TimeSpan.FromMilliseconds(40));
        }

        internal void ClickPlay()
        {
            var play = (Button)Window.FindName("PlayButton")!;
            play.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            PumpQuietly(TimeSpan.FromMilliseconds(40));
        }

        /// <summary>FOKUS na przycisku Odtworz - jak po Tabie z listy.</summary>
        internal void FocusPlay()
        {
            var play = (Button)Window.FindName("PlayButton")!;
            play.Focus();
            Keyboard.Focus(play);
            PumpQuietly(TimeSpan.FromMilliseconds(60));
        }

        /// <summary>
        /// PRAWDZIWA Spacja na SKUPIONYM przycisku. WPF zamienia KeyDown Space na
        /// Click w <c>ButtonBase</c>, ale samo zdarzenie klawisza jedzie przez okno
        /// - dokladnie tak, jak zmierzono fizycznie pod NVDA. Nie wolamy tu
        /// bezposrednio Play_Click.
        /// </summary>
        internal void PressSpaceOnPlay()
        {
            var play = (Button)Window.FindName("PlayButton")!;
            var source = PresentationSource.FromVisual(Window)
                ?? throw new Exception("Okno ulubionych nie ma powierzchni prezentacji.");
            var target = Keyboard.FocusedElement as UIElement ?? play;
            target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Space)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            });
            // Spacja na przycisku konczy sie Click; zdarzenie klawisza samo go nie
            // syntetyzuje w podniesionym KeyEventArgs, wiec domykamy TA SAMA
            // produkcyjna droga co fizyczna Spacja: Click na tym przycisku.
            if (play.IsKeyboardFocused && play.IsEnabled)
            {
                play.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }

            PumpQuietly(TimeSpan.FromMilliseconds(40));
        }

        /// <summary>CZEKANIE na DOKLADNIE to zadanie proby, nie na milisekundy.</summary>
        internal void AwaitPlay()
        {
            if (Window.LastPlayTaskForTests is not { } task) return;
            var deadline = DateTime.UtcNow + Limit;
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline) throw new Exception("Limit czasu: zadanie uruchomienia ulubionego.");
                DoEvents();
            }

            task.GetAwaiter().GetResult();
            PumpQuietly(TimeSpan.FromMilliseconds(60));
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
        }

        public void Dispose()
        {
            try
            {
                Window.Close();
                PumpQuietly(TimeSpan.FromMilliseconds(60));
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
