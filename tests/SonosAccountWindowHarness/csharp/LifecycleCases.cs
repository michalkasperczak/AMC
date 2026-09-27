using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AccessibleMediaController.Core.Sonos;
namespace SonosAccountWindowHarness;

// No Show, no browser, no native store and no announcements to NVDA.
internal static class LifecycleCases
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var coreCompleted in new[] { false, true })
        {
            using var s = Scenario.Restored();
            var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            s.Gateway.StartBarrier = gate.Task;
            s.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
            var task = s.Window.InvokeLoginAsync();
            if (coreCompleted)
            {
                gate.SetResult(true);
                // Test-only synchronisation: Core finishes on ThreadPool before
                // the queued UI continuation can run on this Dispatcher.
                check(SpinWait.SpinUntil(() => s.Coordinator.Snapshot.IsAwaitingBrowser, TimeSpan.FromSeconds(3)),
                    "B2: Core zainstalowal wynik przed zamknieciem");
                check(s.Window.AnnouncementCount == 0 && !task.IsCompleted,
                    "B2: kontynuacja okna jeszcze nie weszla");
            }
            s.Window.ShutdownOwnWork();
            if (!coreCompleted) gate.SetResult(true);
            Program.Pump(task);
            check(!s.Coordinator.Snapshot.IsAwaitingBrowser,
                "B2: zamkniecie nie zostawia proby, Core wczesniej=" + coreCompleted);
            check(s.Coordinator.Snapshot.HasCredentials && s.Store.DeleteCalls == 0 && s.Store.WriteCalls == 0,
                "B2: zachowano poprzednie konto, Core wczesniej=" + coreCompleted);
            check(s.Window.AnnouncementCount == 0 && s.Browser.OpenedUris.Count == 0,
                "B2: po Close brak mowy i przegladarki, Core wczesniej=" + coreCompleted);
        }
        using (var s = Scenario.Fresh())
        {
            s.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
            Program.Pump(s.Window.InvokeLoginAsync());
            s.Window.ShutdownOwnWork();
            check(!s.Coordinator.Snapshot.IsAwaitingBrowser, "B2: zwykly Start potem Close sprzata probe");
        }
        using (var s = Scenario.Fresh())
        {
            s.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
            Program.Pump(s.Coordinator.BeginLoginAsync(CancellationToken.None));
            s.Window.ShutdownOwnWork();
            check(s.Coordinator.Snapshot.IsAwaitingBrowser, "B2: okno nie rozpoczynalo proby, nie kasuje cudzej");
        }
        foreach (var name in new[] { "CancelLoginButton", "RetryPersistButton", "DisconnectButton" })
        {
            using var s = name == "DisconnectButton" ? Scenario.Restored() : Scenario.Fresh();
            s.Confirm.Answer = true;
            s.Gateway.StartResult = Outcomes.StartOk(Fakes.Session());
            if (name == "CancelLoginButton") Program.Pump(s.Window.InvokeLoginAsync());
            if (name == "RetryPersistButton")
            {
                s.Store.WriteResult = SonosCredentialWriteOutcome.Failure(SonosCredentialWriteStatus.WriteFailure);
                s.Gateway.FetchResult = Outcomes.FetchOk(Fakes.Tokens());
                Program.Pump(s.Window.InvokeLoginAsync());
                Program.Pump(s.Window.InvokeCheckLoginAsync());
            }
            var button = Program.Button(s.Window, name);
            check(button.Visibility == Visibility.Visible && button.IsEnabled, "B3: osiagniety czynny przycisk " + name);
            s.Coordinator.Dispose();
            var before = s.Window.AnnouncementCount;
            Exception? escaped = null;
            try
            {
                typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
            }
            catch (TargetInvocationException ex) { escaped = ex.InnerException; }
            catch (Exception ex) { escaped = ex; }
            check(escaped is null, "B3: OnClick nie wypuszcza wyjatku " + name);
            check(s.Window.AnnouncementCount == before + 1, "B3: jeden komunikat odmowy " + name);
            check(s.Window.LastAnnouncement == "Obsługa konta Sonos jest niedostępna. Zamknij to okno.",
                "B3: staly czytelny komunikat bez szczegolow wyjatku " + name);
        }
    }
}
