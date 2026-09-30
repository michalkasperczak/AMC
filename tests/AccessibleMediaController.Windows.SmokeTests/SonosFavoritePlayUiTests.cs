using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
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
                void Stage(string name) => Console.Error.WriteLine("ETAP: " + name);
                Stage("P1"); checks += MeasureReadOnlyWindowSwallowsEnter();
                Stage("P2"); checks += MeasureDuplicateNamesSendExactSelectedId();
                Stage("P3"); checks += MeasurePlayButtonTakesSamePath();
                Stage("P4"); checks += MeasureHeldAttemptRefusesSecondPost();
                Stage("P5"); checks += MeasureNoGroupAndEmptyListNeverPost();
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
