using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Core.Configuration;
using AccessibleMediaController.Windows;
using AccessibleMediaController.Windows.Services;

/// <summary>
/// Jawna aktualizacja AMC nie moze po cichu zamknac programu, gdy zapis stanu
/// zawiedzie. Mierzymy DWA momenty na rzeczywistym oknie glownym:
/// 1. ochronny zapis PRZED nieodwracalnym zamykaniem - trwala awaria zostawia
///    AMC w pelni dzialajace i pokazuje dostepne okno wyjasnienia,
/// 2. awarie KONCOWEGO zapisu, juz po zwolnieniu zasobow - wtedy program musi
///    pokazac modalny komunikat wymagajacy potwierdzenia, a nie ulotny
///    komunikat mowy, ktory przepada razem z zamykanym oknem.
/// Bez sieci, bez kont, bez instalatora: wlasny TEMP i pusta konfiguracja.
/// </summary>
internal static class ApplicationUpdateSaveFailureTests
{
    private sealed class DialogCapture
    {
        internal DispatcherTimer? Timer;
        internal bool Seen;
        internal string Title = string.Empty;
        internal string Text = string.Empty;
        internal bool WasModalOverOwner;
    }

    internal static void ShowForNvda(bool closing)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "amc-save-nvda-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Tidal.ClientId = string.Empty;
                state.Spotify.ClientId = string.Empty;
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.Updates.InstallOnExit = false;
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                window = new MainWindow(state, store, () =>
                {
                    if (!closing)
                    {
                        InvokePrivate(window!, "TryPrepareExplicitUpdateStateSave");
                        Console.WriteLine("UI_SAVE_PREFLIGHT_RETURNED|Closing=" + GetPrivateField(window!, "_isClosing"));
                    }
                    else
                    {
                        var flow = (AccessibleMediaController.Core.Updates.ApplicationUpdateInstallFlow)
                            GetPrivateField(window!, "_applicationUpdateInstallFlow")!;
                        flow.RequestClose(() => { window!.Close(); return true; });
                    }
                });
                InjectFailingPersistence(window, store);
                window.Title = "AMC — próba błędu zapisu; F11 pokazuje błąd";
                window.ShowDialog();
                Console.WriteLine("UI_SAVE_FIXTURE_CLOSED|LateFailure=" + closing);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                if (window is not null) ClearUpdateRequest(window);
                window?.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(10))) throw new Exception("Próba błędu zapisu z NVDA przekroczyła czas.");
        if (failure is not null) throw new Exception("Próba błędu zapisu z NVDA", failure);
    }

    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "amc-update-save-failure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            MainWindow? window = null;
            try
            {
                var state = new PersistedState();
                state.Tidal.ClientId = string.Empty;
                state.Spotify.ClientId = string.Empty;
                state.Settings.Updates.CheckAutomatically = false;
                state.Settings.Updates.InstallOnExit = false;
                var store = new ConfigurationStore(Path.Combine(root, "state.json"));
                window = new MainWindow(state, store);
                // AccessibleDialog ustawia Owner, a to wymaga POKAZANEGO okna -
                // inaczej spada na systemowy MessageBox, ktorego nie da sie
                // zamknac w tym tescie. Okno testowe stoi poza ekranem.
                window.Left = -32000;
                window.Top = -32000;
                window.ShowInTaskbar = false;
                window.Show();
                InjectFailingPersistence(window, store);
                var gaps = new List<string>();

                // 0. Zwykle zamkniecie BEZ jawnego zadania: zaden popup nie ma
                //    prawa sie pokazac.
                var startOverride = typeof(MainWindow).GetField(
                    "_applicationUpdateStartOverride",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (startOverride is null)
                {
                    gaps.Add("Brak instancyjnego przelacznika startu instalacji (_applicationUpdateStartOverride) — nie da sie zmierzyc odmowy startu bez produkcyjnego katalogu aktualizacji.");
                }
                else
                {
                    var cichyDialog = WatchForOwnDialog(window);
                    InvokePrivate(window, "LaunchUpdateAfterStateSave", true);
                    Stop(cichyDialog);
                    if (cichyDialog.Seen)
                        gaps.Add("Zwykle zamkniecie bez jawnego zadania aktualizacji pokazalo okno o porzuconej aktualizacji.");

                    // 0a. UDANY zapis + odmowa startu: jawne zadanie nie moze
                    //     zniknac po cichu, a komunikat NIE MOZE twierdzic, ze
                    //     zapis stanu zawiodl.
                    MarkExplicitRequest(window);
                    startOverride.SetValue(window, (Func<bool, bool>)(_ => false));
                    var odmowaDialog = WatchForOwnDialog(window);
                    InvokePrivate(window, "LaunchUpdateAfterStateSave", true);
                    Stop(odmowaDialog);
                    if (!odmowaDialog.Seen)
                        gaps.Add("Po UDANYM zapisie i nieudanym starcie instalacji jawna aktualizacja znika bez modalnego komunikatu.");
                    else
                    {
                        CollectMentions(gaps, odmowaDialog.Text, "odmowa startu po udanym zapisie", new[] { "aktualizacj", "zamkn" });
                        if (odmowaDialog.Text.Contains("nie udało się zapisać stanu", StringComparison.OrdinalIgnoreCase))
                            gaps.Add("Komunikat o nieudanym starcie twierdzi, ze zawiodl zapis stanu, mimo ze zapis byl poprawny: " + odmowaDialog.Text);
                        if (odmowaDialog.Text.Contains("paczka pozostaje", StringComparison.OrdinalIgnoreCase))
                            gaps.Add("Komunikat twierdzi, ze pobrana paczka pozostaje na dysku, choc jej brak jest przyczyna: " + odmowaDialog.Text);
                        if (odmowaDialog.Text.Contains("Nie potwierdzono", StringComparison.OrdinalIgnoreCase)
                            || odmowaDialog.Text.Contains("pobierz paczkę jeszcze raz", StringComparison.OrdinalIgnoreCase))
                            gaps.Add("Odmowa samego startu nie dowodzi niesprawdzonej paczki i nie wymaga ponownego pobrania: " + odmowaDialog.Text);
                    }

                    // 0b. WYJATEK startu - druga jawna odmowa, ten sam wymog.
                    MarkExplicitRequest(window);
                    startOverride.SetValue(window, (Func<bool, bool>)(_ =>
                        throw new IOException("Test: instalator nie wystartowal.")));
                    var wyjatekDialog = WatchForOwnDialog(window);
                    InvokePrivate(window, "LaunchUpdateAfterStateSave", true);
                    Stop(wyjatekDialog);
                    if (!wyjatekDialog.Seen)
                        gaps.Add("Wyjatek przy uruchamianiu instalatora zamyka program bez modalnego komunikatu.");
                    else
                        CollectMentions(gaps, wyjatekDialog.Text, "wyjatek startu instalatora", new[] { "aktualizacj", "błęd" });

                    startOverride.SetValue(window, null);
                    // Znacznik jawnego zadania zostaje po powyzszych probach -
                    // zdejmujemy go, zeby ponizszy RequestClose mial czysty stan.
                    ClearUpdateRequest(window);
                }

                // 1. Awaria KONCOWEGO zapisu, juz po zwolnieniu zasobow: jawna
                //    aktualizacja nie moze zniknac bez potwierdzonego komunikatu.
                var flow = GetPrivateField(window, "_applicationUpdateInstallFlow")!;
                var requestClose = flow.GetType().GetMethod("RequestClose")!;
                if (!(bool)requestClose.Invoke(flow, new object[] { (Func<bool>)(() => true) })!)
                    throw new Exception("Nie udalo sie oznaczyc jawnego zadania aktualizacji w tescie.");

                var exitDialog = WatchForOwnDialog(window);
                InvokePrivate(window, "LaunchUpdateAfterStateSave", false);
                Stop(exitDialog);
                if (!exitDialog.Seen)
                    gaps.Add("Po nieudanym koncowym zapisie jawna aktualizacja zamyka program bez modalnego komunikatu (tylko ulotny komunikat mowy).");
                else
                    CollectMentions(gaps, exitDialog.Text, "porzucona aktualizacja przy zamykaniu", new[] { "aktualizacj", "zapis", "zamkn" });

                // 2. Ochronny zapis PRZED zamykaniem: trwala awaria wstrzymuje
                //    aktualizacje i tlumaczy to w dostepnym oknie.
                var preflight = typeof(MainWindow).GetMethod(
                    "TryPrepareExplicitUpdateStateSave",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (preflight is null)
                {
                    gaps.Add("Brak ochronnego zapisu przed nieodwracalnym zamykaniem (metoda TryPrepareExplicitUpdateStateSave).");
                }
                else
                {
                    var preflightDialog = WatchForOwnDialog(window);
                    var prepared = (bool)preflight.Invoke(window, Array.Empty<object>())!;
                    Stop(preflightDialog);
                    if (prepared)
                        gaps.Add("Ochronny zapis zglosil sukces, mimo ze zapis stanu rzuca wyjatek.");
                    if (!preflightDialog.Seen)
                        gaps.Add("Wstrzymana aktualizacja nie pokazala okna wyjasnienia.");
                    else
                    {
                        CollectMentions(gaps, preflightDialog.Text, "wstrzymana aktualizacja", new[] { "aktualizacj", "zapis" });
                        if (preflightDialog.Text.Contains("żadne dane nie zostały utracone", StringComparison.OrdinalIgnoreCase))
                            gaps.Add("Po błędzie zapisu nie wolno gwarantować, że żadne dane nie zostały utracone.");
                        if (!preflightDialog.Text.Contains("pozostaje otwarte", StringComparison.OrdinalIgnoreCase))
                            gaps.Add("Okno wstrzymanej aktualizacji nie mowi, ze AMC pozostaje otwarte: " + preflightDialog.Text);
                    }
                    if ((bool)GetPrivateField(window, "_isClosing")!)
                        gaps.Add("Wstrzymana aktualizacja rozpoczela zamykanie programu.");
                }

                // Rzeczywiste Close uruchamia Window_Closing i Dispose,
                // dopiero po nich musi pozostac dostepne okno bledu zapisu.
                var closed = false;
                window.Closed += (_, _) => closed = true;
                var realClosingDialog = WatchForOwnDialog(window);
                window.Close();
                Stop(realClosingDialog);
                if (!closed || !realClosingDialog.Seen)
                    gaps.Add("Rzeczywiste zamkniecie nie pokazalo potwierdzanego bledu koncowego zapisu.");

                if (gaps.Count > 0)
                    throw new Exception(string.Join(" | ", gaps));
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                // Bez tego zamkniecie okna testowego wywolaloby modalny
                // komunikat o porzuconej aktualizacji i zawiesilo test.
                if (window is not null) ClearUpdateRequest(window);
                try { window?.Close(); } catch { /* okno testowe */ }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
                try { Directory.Delete(root, true); } catch { /* TEMP testu */ }
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(2)))
            throw new Exception("Test awarii zapisu przy aktualizacji przekroczyl czas.");
        if (failure is not null)
            throw new Exception("Awaria zapisu stanu przy jawnej aktualizacji AMC", failure);
        Console.WriteLine("OK: nieudany zapis stanu wstrzymuje aktualizacje i tlumaczy to w dostepnym oknie, nie po cichu");
    }

    /// <summary>Zapis stanu, ktory zawsze zawodzi - bez globalnej sciezki testowej.</summary>
    private static void InjectFailingPersistence(MainWindow window, ConfigurationStore store)
    {
        var queue = new StatePersistenceQueue(
            store.CloneState,
            _ => throw new IOException("Test: zapis stanu niedostepny."));
        var field = typeof(MainWindow).GetField("_statePersistence", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("Brak pola _statePersistence w MainWindow.");
        field.SetValue(window, queue);
    }

    /// <summary>
    /// Modalne okno blokuje watek, wiec czekamy na nie zegarem dyspozytora.
    /// Zamykamy WYLACZNIE wlasne okno tego testu.
    /// </summary>
    private static DialogCapture WatchForOwnDialog(Window owner)
    {
        var capture = new DialogCapture();
        var ticks = 0;
        var timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(80),
            DispatcherPriority.Normal,
            (_, _) =>
            {
                ticks++;
                foreach (var source in PresentationSource.CurrentSources.OfType<PresentationSource>().ToArray())
                {
                    if (source.RootVisual is not Window dialog) continue;
                    if (ReferenceEquals(dialog, owner) || !ReferenceEquals(dialog.Dispatcher, owner.Dispatcher)) continue;
                    if (dialog.Title is not ("Aktualizacja nie zostanie zainstalowana"
                        or "Aktualizacja wstrzymana — nieudany zapis stanu")) continue;
                    capture.Seen = true;
                    capture.Title = dialog.Title ?? string.Empty;
                    capture.Text = ReadDialogContent(dialog);
                    capture.WasModalOverOwner = ReferenceEquals(dialog.Owner, owner);
                    try { dialog.Close(); } catch { /* wlasne okno testu */ }
                    return;
                }
                if (ticks > 200) capture.Timer?.Stop();
            },
            owner.Dispatcher);
        capture.Timer = timer;
        timer.Start();
        return capture;
    }

    private static void Stop(DialogCapture capture) => capture.Timer?.Stop();

    /// <summary>Oznacz JAWNE zadanie aktualizacji bez zamykania okna testowego.</summary>
    private static void MarkExplicitRequest(MainWindow window)
    {
        var flow = GetPrivateField(window, "_applicationUpdateInstallFlow")!;
        flow.GetType().GetProperty("IsRequested")!.GetSetMethod(true)!
            .Invoke(flow, new object[] { true });
    }

    /// <summary>Zdejmij znacznik jawnego zadania, zeby zamkniecie okna testowego nie pokazalo okna.</summary>
    private static void ClearUpdateRequest(MainWindow window)
    {
        try
        {
            var flow = GetPrivateField(window, "_applicationUpdateInstallFlow");
            if (flow is null) return;
            var property = flow.GetType().GetProperty("IsRequested");
            property?.GetSetMethod(true)?.Invoke(flow, new object[] { false });
        }
        catch { /* sprzatanie testu */ }
    }

    private static string ReadDialogContent(Window dialog)
    {
        var box = FindTextBox(dialog.Content as DependencyObject);
        return box?.Text ?? string.Empty;
    }

    private static TextBox? FindTextBox(DependencyObject? node)
    {
        if (node is null) return null;
        if (node is TextBox box) return box;
        foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
        {
            var found = FindTextBox(child);
            if (found is not null) return found;
        }
        return null;
    }

    private static void CollectMentions(List<string> gaps, string text, string what, string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (!text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                gaps.Add($"Komunikat ({what}) nie mowi o \"{fragment}\": {text}");
        }
    }

    private static object? InvokePrivate(MainWindow window, string name, params object[] arguments)
    {
        var method = typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception($"Brak metody {name} w MainWindow.");
        return method.Invoke(window, arguments);
    }

    private static object? GetPrivateField(MainWindow window, string name)
    {
        var field = typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception($"Brak pola {name} w MainWindow.");
        return field.GetValue(window);
    }
}
