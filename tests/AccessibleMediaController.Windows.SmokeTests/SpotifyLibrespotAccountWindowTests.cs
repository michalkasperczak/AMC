using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using AccessibleMediaController.Windows;

internal static class SpotifyLibrespotAccountWindowTests
{
    internal static void Run() => OnSta(RunSta);

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try { action(); } catch (Exception exception) { failure = exception; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new Exception("Próba okna przekroczyła limit czasu.");
        if (failure is not null) throw new Exception("Okno parowania Librespot", failure);
    }

    private static void RunSta()
    {
        var type = typeof(MainWindow).Assembly.GetType(
            "AccessibleMediaController.Windows.SpotifyLibrespotAccountWindow")
            ?? throw new Exception("Brakuje osobnego dostępnego okna parowania Librespot.");
        var requests = 0;
        var opened = 0;
        var paired = false;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Action<string, Uri>, CancellationToken, Task> pair = async (ready, cancellation) =>
        {
            requests++;
            ready("TEST42", new Uri("https://www.spotify.com/pair?code=TEST42"));
            await done.Task.WaitAsync(cancellation);
            paired = true;
        };
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(info => info.GetParameters().Length == 4);
        var window = (Window)constructor.Invoke(new object[]
        { (Func<bool>)(() => paired), pair, (Action)(() => { }), (Action<Uri>)(_ => opened++) });
        try
        {
            if (requests != 0) throw new Exception("Samo otwarcie okna rozpoczyna parowanie.");
            foreach (var name in new[] { "AccountText", "PairingCodeBox", "PairingAddressBox" })
            {
                var box = window.FindName(name) as TextBox
                    ?? throw new Exception($"Brak pola z kursorem: {name}.");
                if (!box.IsReadOnly || string.IsNullOrWhiteSpace(AutomationProperties.GetName(box)))
                    throw new Exception($"Pole {name} nie ma dostępnej nazwy albo jest edytowalne.");
            }
            foreach (var name in new[] { "PairButton", "OpenBrowserButton", "CopyCodeButton", "DisconnectButton", "CatalogAccountButton", "CloseButton" })
                if (window.FindName(name) is not Button)
                    throw new Exception($"Brak przycisku {name}.");
            if (((Button)window.FindName("OpenBrowserButton")).IsEnabled)
                throw new Exception("Otwieranie strony jest aktywne bez bieżącego parowania.");
            Click(window, "PairButton");
            var operation = (Task)type.GetProperty("PendingPairing", BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
            if (operation.IsCompleted || requests != 1) throw new Exception("Parowanie nie oczekuje na potwierdzenie.");
            if (((TextBox)window.FindName("PairingCodeBox")).Text != "TEST42") throw new Exception("Kod nie trafił do pola z kursorem.");
            if (opened != 0) throw new Exception("Przeglądarka zabrała fokus bez żądania.");
            Click(window, "OpenBrowserButton");
            if (opened != 1) throw new Exception("Przycisk nie otworzył przygotowanego adresu.");
            done.SetResult();
            PumpUntil(() => operation.IsCompleted);
            operation.GetAwaiter().GetResult();
            if (!paired || ((TextBox)window.FindName("PairingCodeBox")).Text.Length != 0
                || !((TextBox)window.FindName("AccountText")).Text.Contains("jest zapisane"))
                throw new Exception("Po zakończeniu pozostał kod albo nie ma potwierdzenia zapisu.");
            if (!((Button)window.FindName("DisconnectButton")).IsEnabled)
                throw new Exception("Po sparowaniu nie można odłączyć sesji.");
        }
        finally { window.Close(); }

        var cancelled = false;
        using var signal = new CancellationTokenSource();
        var cancelWindow = new SpotifyLibrespotAccountWindow(() => false, async (ready, token) =>
        {
            ready("CANCEL", new Uri("https://spotify.com/pair?code=CANCEL"));
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled = true; throw; }
        }, () => { }, _ => { });
        Click(cancelWindow, "PairButton");
        cancelWindow.Close();
        PumpUntil(() => cancelWindow.PendingPairing.IsCompleted);
        if (!cancelled || cancelWindow.CompletionAnnouncement != "Anulowano parowanie Spotify — Librespot")
            throw new Exception("Zamknięcie nie anuluje oczekiwania z komunikatem.");
        Console.WriteLine("OK: okno parowania, pola z kursorem, świadome otwarcie strony, potwierdzenie i anulowanie; bez sieci i konta");
    }

    private static void Click(Window window, string name) => ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void PumpUntil(Func<bool> complete)
    {
        var frame = new DispatcherFrame();
        var started = DateTime.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => { if (complete() || DateTime.UtcNow - started > TimeSpan.FromSeconds(5)) frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        if (!complete()) throw new Exception("Okno nie zakończyło operacji w limicie czasu.");
    }

    internal static void ShowForNvda()
    {
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var window = new SpotifyLibrespotAccountWindow(() => false, async (ready, token) =>
            {
                ready("TEST42", new Uri("https://www.spotify.com/pair?code=TEST42"));
                await Task.Delay(Timeout.Infinite, token);
            }, () => { }, _ => { });
            window.Title += " — próba interfejsu, bez konta";
            window.ShowInTaskbar = true;
            window.ShowDialog();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
}
