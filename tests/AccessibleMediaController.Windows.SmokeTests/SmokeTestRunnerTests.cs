using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Threading;

internal static class SmokeTestRunnerTests
{
    internal static void Run()
    {
        using var error = new StringWriter();
        if (SmokeTestRunner.Run(() => 0, error) != 0 || error.ToString().Length != 0)
            throw new Exception("Poprawny test został zgłoszony jako błąd.");
        if (SmokeTestRunner.Run(() => 7, error) != 7)
            throw new Exception("Zmieniono jawny kod zakończenia testu.");

        // Real child processes verify that CLR exceptions become exit code 1,
        // rather than an unhandled-exception exit and a Windows error dialog.
        foreach (var kind in new[] { "main", "probe", "sta", "dispatcher", "cleanup" })
        {
            var executable = Environment.ProcessPath ?? throw new Exception("Brak ścieżki procesu testowego.");
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardErrorEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8
            };
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(SmokeTestRunnerTests).Assembly.Location);
            start.ArgumentList.Add($"--smoke-runner-failure={kind}");
            using var child = Process.Start(start) ?? throw new Exception("Nie uruchomiono testu obsługi błędu.");
            var stderr = child.StandardError.ReadToEndAsync();
            var stdout = child.StandardOutput.ReadToEndAsync();
            if (!child.WaitForExit(15_000))
            {
                // Only the child just created here; never search for or kill AMC.
                child.Kill(entireProcessTree: true);
                throw new TimeoutException($"Proces kontrolny {kind} nie zakończył się w terminie.");
            }
            var message = stderr.GetAwaiter().GetResult();
            if (child.ExitCode != 1 || !message.Contains("BŁĄD TESTU AMC", StringComparison.Ordinal)
                || !message.Contains($"controlled-{kind}", StringComparison.Ordinal)
                || stdout.GetAwaiter().GetResult().Contains("OK:", StringComparison.Ordinal))
                throw new Exception($"Nieprawidłowe zakończenie symulacji {kind}: kod {child.ExitCode}; {message}");
        }
        Console.WriteLine("OK: niepowodzenia main/probe/STA/dispatcher/cleanup kończą proces testowy kodem 1, bez fałszywego sukcesu.");
    }

    internal static void SimulateFailure(string kind)
    {
        var failure = new InvalidOperationException($"controlled-{kind}");
        switch (kind)
        {
            case "main": throw failure;
            case "probe":
                TidalWebViewSmokeTests.RunOnSta(async () => { await Task.Yield(); throw failure; });
                break;
            case "sta":
                TidalWebViewSmokeTests.RunOnSta(() => Task.CompletedTask, () => throw failure);
                break;
            case "dispatcher":
                TidalWebViewSmokeTests.RunOnSta(() =>
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => throw failure));
                    return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
                });
                break;
            case "cleanup":
                try { _ = DateTime.UtcNow; }
                finally { throw failure; }
            default: throw new ArgumentException("Nieznana symulacja błędu testów.", nameof(kind));
        }
    }
}
