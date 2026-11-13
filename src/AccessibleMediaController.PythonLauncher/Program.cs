using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using AccessibleMediaController.Core.ShellIntegration;
using AccessibleMediaController.ReleaseProfiles;

namespace AccessibleMediaController.PythonLauncher;

internal static class Program
{
    private const int InvalidBundle = 2;
    private const int StartFailed = 3;
    private const int UpdateInProgress = 4;
    private const int ExistingWindowWaitMilliseconds = 5_000;
    private const int PostUpdateWindowWaitMilliseconds = 15_000;
    private const int SafeReplacementWaitMilliseconds = 10_000;

    [STAThread]
    private static int Main(string[] args)
    {
        ReleaseProfileDescriptor? profile = null;
        var postUpdateLaunch = PostUpdateLaunchContract.Parse(args);
        args = postUpdateLaunch.ForwardedArguments;
        var installerFullCheck = UpdateInProgressGate.IsInstallerFullCheck(args);
        var installerStartCheck = UpdateInProgressGate.IsInstallerStartCheck(args);
        var fullBundleCheck = installerFullCheck || args.Any(argument =>
            string.Equals(argument, "--sprawdz-pakiet-pelny", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "--sprawdz-pakiet-pelny-cicho", StringComparison.OrdinalIgnoreCase));
        var silentBundleCheck = installerFullCheck || args.Any(argument =>
            string.Equals(argument, "--sprawdz-pakiet-cicho", StringComparison.OrdinalIgnoreCase)
            || string.Equals(argument, "--sprawdz-pakiet-pelny-cicho", StringComparison.OrdinalIgnoreCase));
        var silentStartupCheck = installerStartCheck || args.Any(argument =>
            string.Equals(argument, "--sprawdz-start-cicho", StringComparison.OrdinalIgnoreCase));
        try
        {
            // Sprawdzamy przed odczytem pakietu: instalator może w tej
            // chwili podmieniać nawet manifest i pliki startowe.
            if (!UpdateInProgressGate.IsInstallerCheck(args)
                && UpdateInProgressGate.ActiveProfile(AppContext.BaseDirectory) is { } updating)
            {
                if (!silentBundleCheck && !silentStartupCheck)
                    Show(updating, $"Trwa aktualizacja {updating.DisplayName}. "
                        + "Zaczekaj na zakończenie instalacji. "
                        + "Program uruchomi się ponownie automatycznie.", false);
                return UpdateInProgress;
            }
            var bundle = BundleVerifier.Verify(
                AppContext.BaseDirectory,
                verifyAllFiles: fullBundleCheck);
            profile = bundle.Profile;
            if (silentBundleCheck)
                return 0;
            if (fullBundleCheck || args.Any(argument => string.Equals(
                    argument,
                    "--sprawdz-pakiet",
                    StringComparison.OrdinalIgnoreCase)))
            {
                Show(
                    profile,
                    fullBundleCheck
                        ? $"Pełna integralność pakietu {profile.DisplayName} jest potwierdzona."
                        : $"Pakiet {profile.DisplayName} jest kompletny i zgodny.",
                    false);
                return 0;
            }

            // Bramka wydania uruchamia prawdziwy frontend i host na
            // odizolowanym profilu. Nie może aktywować działającego AMC ani
            // uznać jego okna za wynik testu nowej paczki, dlatego świadomie
            // omija zwykłą dzierżawę pojedynczej instancji.
            if (silentStartupCheck)
            {
                if (!installerStartCheck
                    && UpdateInProgressGate.ActiveProfile(bundle.Root) is not null)
                    return UpdateInProgress;
                return RunFrontend(
                    bundle,
                    installerStartCheck ? ["--sprawdz-start-cicho"] : args,
                    new ShellLaunchRequest([], false),
                    suppressFailureDialog: true,
                    activateAfterStart: false);
            }

            var shellRequest = ShellLaunchRequestContract.ParseAndValidate(args);

            LauncherInstanceLease? instance = LauncherInstanceLease.TryAcquire(
                profile.LauncherMutexName);
            try
            {
                if (!instance.IsOwner)
                {
                    var existing = WaitForMainWindow(
                        profile,
                        ExistingWindowWaitMilliseconds);
                    if (shellRequest.HasFiles)
                    {
                        var forwarded = ShellRequestChannel.TrySend(
                            profile,
                            shellRequest,
                            ExistingWindowWaitMilliseconds);
                        if (existing is not null)
                            ActivateWindow(existing.Value.Handle);
                        if (!forwarded)
                        {
                            Show(
                                profile,
                                "Nie udało się przekazać pliku do działającego "
                                + $"programu {profile.DisplayName}. Program nie został zamknięty.",
                                true);
                            return StartFailed;
                        }
                        return 0;
                    }
                    if (existing is null)
                        return 0;

                    if (ExistingInstancePolicy.Decide(
                            bundle.Manifest.BuildLabel,
                            existing.Value.Title,
                            bundle.Root,
                            existing.Value.PackageRoot,
                            profile.PreferredInstallationFolder)
                        == ExistingInstanceAction.Activate)
                    {
                        ActivateWindow(existing.Value.Handle);
                        return 0;
                    }

                    // A newer package asks the old GUI to close normally.  Its
                    // EVT_CLOSE handler checks the live recording owner and
                    // vetoes this request whenever recording is active or the
                    // state cannot be confirmed.  We never kill either process.
                    RequestSafeClose(existing.Value.Handle);
                    instance.Dispose();
                    instance = null;
                    instance = WaitForInstanceLease(
                        profile,
                        SafeReplacementWaitMilliseconds);
                    if (instance is null)
                    {
                        var stillRunning = WaitForMainWindow(profile, 500);
                        if (stillRunning is not null)
                            ActivateWindow(stillRunning.Value.Handle);
                        return 0;
                    }
                }

                if (UpdateInProgressGate.ActiveProfile(bundle.Root) is { } installing)
                {
                    Show(installing, $"Trwa aktualizacja {installing.DisplayName}. "
                        + "Zaczekaj na zakończenie instalacji. "
                        + "Program uruchomi się ponownie automatycznie.", false);
                    return UpdateInProgress;
                }
                return RunFrontend(
                    bundle,
                    args,
                    shellRequest,
                    activateAfterStart: postUpdateLaunch.ActivateAfterStart);
            }
            finally
            {
                instance?.Dispose();
            }
        }
        catch (ShellLaunchRequestException exception)
        {
            if (!silentBundleCheck && !silentStartupCheck)
                Show(profile, exception.Message, true);
            return StartFailed;
        }
        catch (InvalidDataException exception)
        {
            if (!silentBundleCheck && !silentStartupCheck)
            {
                Show(
                    profile,
                    $"Nie można uruchomić {profile?.DisplayName ?? "AMC"}, ponieważ pakiet jest "
                    + "niekompletny lub niezgodny: " + exception.Message + ".\n\n"
                    + "Uruchom ponownie instalator albo zastosuj pełną aktualizację.",
                    true);
            }
            return InvalidBundle;
        }
        catch
        {
            if (!silentBundleCheck && !silentStartupCheck)
            {
                Show(
                    profile,
                    $"Nie udało się uruchomić {profile?.DisplayName ?? "AMC"}. Pakiet nie został zmieniony. "
                    + "Uruchom ponownie instalator albo zastosuj pełną aktualizację.",
                    true);
            }
            return StartFailed;
        }
    }

    private static int RunFrontend(
        VerifiedBundle bundle,
        string[] args,
        ShellLaunchRequest shellRequest,
        bool suppressFailureDialog = false,
        bool activateAfterStart = false)
    {
        var plan = PythonLaunchPlan.Create(bundle, args);
        if (activateAfterStart)
            plan.StartInfo.Environment["AMC_POST_UPDATE_RESUME"] = "1";
        using var process = Process.Start(plan.StartInfo)
            ?? throw new InvalidOperationException("nie uruchomiono procesu interfejsu");
        if (activateAfterStart)
            AllowForegroundBestEffort(process.Id);

        var inputGate = new object();
        void ForwardToFrontend(string payload)
        {
            lock (inputGate)
            {
                if (process.HasExited)
                    throw new IOException("interfejs został zamknięty");
                process.StandardInput.WriteLine(payload);
                process.StandardInput.Flush();
            }
        }
        using var shellChannel = ShellRequestChannel.Start(
            bundle.Profile,
            ForwardToFrontend);
        if (shellRequest.HasFiles)
            ForwardToFrontend(ShellIntegrationRules.EncodeHandoff(shellRequest));

        // Strumienie czytamy od razu także podczas zwykłej pracy. Bez
        // tego dłuższy komunikat mógłby zapełnić bufor i zatrzymać proces.
        // Przechwycenie stderr zapobiega modalnym alertom pythonw.
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (activateAfterStart)
        {
            var window = WaitForMainWindow(
                bundle.Profile,
                PostUpdateWindowWaitMilliseconds);
            if (window is not null)
                ActivateWindow(window.Value.Handle);
        }
        process.WaitForExit();
        Task.WaitAll(output, error);

        var outputText = output.Result.Trim();
        var errorText = error.Result.Trim();
        // Kontrolowany start działa na profilu tymczasowym i nie może
        // dopisywać technicznego wyniku do dziennika prawdziwego profilu.
        if (!suppressFailureDialog)
            AppendProcessLog(bundle.Profile, outputText, errorText, process.ExitCode);

        if (plan.ShowCapturedOutput)
        {
            var details = string.Join(
                Environment.NewLine,
                new[] { outputText, errorText }
                    .Where(text => !string.IsNullOrWhiteSpace(text)));
            Show(
                bundle.Profile,
                string.IsNullOrWhiteSpace(details)
                    ? $"Sprawdzenie {bundle.Profile.DisplayName} nie zwróciło informacji."
                    : details,
                process.ExitCode != 0);
        }
        else if (process.ExitCode != 0 && !suppressFailureDialog)
        {
            Show(
                bundle.Profile,
                $"{bundle.Profile.DisplayName} zakończył się nieoczekiwanie "
                + $"(kod 0x{unchecked((uint)process.ExitCode):X8}). "
                + "AMC jest już zamknięty. Przycisk OK zamknie tylko tę diagnostykę. "
                + "Kod zakończenia zapisano w dzienniku: " + ProcessLogPath(bundle.Profile)
                + ". Po ponownym uruchomieniu AMC.exe można kontynuować pracę. "
                + "Jeśli problem wróci, przekaż dziennik diagnostyczny do analizy.",
                true);
        }
        return process.ExitCode;
    }

    private static LauncherInstanceLease? WaitForInstanceLease(
        ReleaseProfileDescriptor profile,
        int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + Math.Max(0, timeoutMilliseconds);
        do
        {
            var candidate = LauncherInstanceLease.TryAcquire(profile.LauncherMutexName);
            if (candidate.IsOwner)
                return candidate;
            candidate.Dispose();
            if (Environment.TickCount64 >= deadline)
                break;
            Thread.Sleep(100);
        } while (true);
        return null;
    }

    private static ExistingWindow? WaitForMainWindow(
        ReleaseProfileDescriptor profile,
        int timeoutMilliseconds)
    {
        var deadline = Environment.TickCount64 + Math.Max(0, timeoutMilliseconds);
        do
        {
            var window = FindMainWindow(profile);
            if (window is not null)
                return window;
            if (Environment.TickCount64 >= deadline)
                break;
            Thread.Sleep(100);
        } while (true);
        return null;
    }

    private static void RequestSafeClose(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            PostMessage(handle, 0x0010u, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
    }

    private static void ActivateWindow(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;
        ShowWindow(handle, 3); // SW_MAXIMIZE
        BringWindowToTop(handle);
        SetForegroundWindow(handle);
    }

    private static void AllowForegroundBestEffort(int processId)
    {
        try
        {
            AllowSetForegroundWindow((uint)processId);
        }
        catch
        {
            // Główny frontend nadal wykonuje własne Show/Raise/SetForegroundWindow.
        }
    }

    private static ExistingWindow? FindMainWindow(ReleaseProfileDescriptor profile)
    {
        ExistingWindow? found = null;
        EnumWindows((handle, _) =>
        {
            var length = GetWindowTextLength(handle);
            if (length <= 0)
                return true;
            var text = new StringBuilder(length + 1);
            GetWindowText(handle, text, text.Capacity);
            var title = text.ToString();
            if (!IsMainWindowTitle(profile, title))
                return true;
            found = new ExistingWindow(handle, title, PackageRootForWindow(handle));
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static string? PackageRootForWindow(IntPtr handle)
    {
        GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
            return null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var executable = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executable))
                return null;
            var executableFolder = Path.GetDirectoryName(executable);
            if (string.IsNullOrWhiteSpace(executableFolder))
                return null;
            var folder = new DirectoryInfo(executableFolder);
            return string.Equals(folder.Name, "runtime", StringComparison.OrdinalIgnoreCase)
                ? folder.Parent?.FullName
                : folder.FullName;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or NotSupportedException
                or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private readonly record struct ExistingWindow(
        IntPtr Handle,
        string Title,
        string? PackageRoot);

    private static string ProcessLogPath(ReleaseProfileDescriptor profile) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            profile.LogFolder.Replace('/', Path.DirectorySeparatorChar),
            "launcher.log");

    private static void AppendProcessLog(
        ReleaseProfileDescriptor profile,
        string output,
        string error,
        int exitCode)
    {
        if (string.IsNullOrWhiteSpace(output)
            && string.IsNullOrWhiteSpace(error)
            && exitCode == 0)
            return;
        try
        {
            var logPath = ProcessLogPath(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            var text = string.Join(
                Environment.NewLine,
                new[]
                {
                    $"{DateTimeOffset.Now:O} Kod zakończenia interfejsu: {exitCode}",
                    string.IsNullOrWhiteSpace(output) ? null : "Wyjście:\n" + Limit(output),
                    string.IsNullOrWhiteSpace(error) ? null : "Błędy:\n" + Limit(error),
                    "",
                }.Where(value => value is not null));
            File.AppendAllText(logPath, text);
        }
        catch
        {
            // Dziennik nie może przeszkodzić w uruchomieniu ani zamknięciu AMC.
        }
    }

    private static string Limit(string text)
    {
        const int maximum = 16_384;
        return text.Length <= maximum ? text : text[^maximum..];
    }

    private static void Show(
        ReleaseProfileDescriptor? profile,
        string text,
        bool error)
    {
        MessageBox(
            IntPtr.Zero,
            text,
            "Diagnostyka " + (profile?.DisplayName ?? "AMC"),
            (error ? 0x10u : 0x40u) | 0x00010000u | 0x00040000u); // foreground, topmost
    }

    internal static bool IsMainWindowTitle(
        ReleaseProfileDescriptor profile,
        string title) =>
        ProductIdentity.IsMainWindowTitle(profile, title);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(
        IntPtr owner,
        string text,
        string caption,
        uint type);

    private delegate bool EnumWindowsProc(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(
        IntPtr windowHandle,
        StringBuilder text,
        int maximumCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr windowHandle,
        out uint processId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wordParameter,
        IntPtr longParameter);
}
