using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AccessibleMediaController.Core.ShellIntegration;
using AccessibleMediaController.PythonLauncher;
using AccessibleMediaController.PythonUpdater;
using AccessibleMediaController.ReleaseProfiles;

namespace AccessibleMediaController.PythonLauncher.Tests;

internal static class Program
{
    private static readonly (string Name, Action Run)[] Tests =
    [
        ("poprawny pakiet", ValidBundleIsAccepted),
        ("poprawny publiczny pakiet", ValidPublicBundleIsAccepted),
        ("natywny TIDAL jest w Test i Public", NativeTidalCapabilityIsInBothProfiles),
        ("manifest nie może przedefiniować profilu", TamperedProfileIsRejected),
        ("uszkodzony plik", CorruptStartupFileIsRejected),
        ("pełna integralność obejmuje pliki niestartowe", FullVerificationCoversNonStartupFiles),
        ("pełna integralność sprawdza rozmiary", FullVerificationChecksSizes),
        ("niezgodny protokół", WrongProtocolIsRejected),
        ("wyjście poza katalog", TraversalIsRejected),
        ("prywatny runtime bez PYTHONPATH i bez alertów pythonw", LaunchPlanUsesOnlyPrivateRuntime),
        ("diagnostyka przez python.exe", DiagnosticUsesConsoleRuntime),
        ("przełącznik startu po aktualizacji jest wewnętrzny", PostUpdateActivationSwitchIsInternal),
        ("frontend odbiera polecenia powłoki przez stdin", FrontendReceivesShellRequestsOnStandardInput),
        ("argumenty powłoki zachowują znaki i tryb kolejki", ShellArgumentsPreservePathsAndQueueMode),
        ("polecenie powłoki ma jednoliniowy JSON", ShellHandoffUsesSingleLineJson),
        ("kanały powłoki profili są osobne", ReleaseProfilesHaveSeparateShellChannels),
        ("kanał przekazuje żądanie do działającej instancji", ShellChannelForwardsToExistingInstance),
        ("jedna instancja launchera", OnlyOneLauncherOwnsTheLease),
        ("osobne instancje launcherów profili", ReleaseProfilesHaveSeparateLauncherLeases),
        ("osobne instancje aktualizatorów profili", ReleaseProfilesHaveSeparateUpdaterLeases),
        ("launcher blokuje start podczas aktualizacji właściwego profilu", LauncherBlocksStartDuringProfileUpdate),
        ("kontrola instalatora omija blokadę tylko dla dwóch cichych prób", InstallerChecksAreNarrow),
        ("kompletna instalacja przechodzi pełne sprawdzenie", InstalledBundlePassesFullVerification),
        ("brak pliku po instalacji jest wykrywany", InstalledBundleDetectsMissingFile),
        ("uszkodzony plik niestartowy po instalacji jest wykrywany", InstalledBundleDetectsCorruptNonStartupFile),
        ("launcher rozpoznaje obecną i starszą nazwę okna", LauncherRecognizesCurrentAndLegacyTitles),
        ("ta sama kompilacja tylko pokazuje okno", SameBuildOnlyActivates),
        ("nowa kompilacja prosi o bezpieczną podmianę", NewBuildRequestsSafeReplacement),
        ("kanoniczna instalacja wygrywa z nieznaną kopią", PreferredInstallationWins),
        ("aktualizator wykrywa wszystkie procesy instalacji", UpdaterRecognizesInstalledComponents),
        ("aktualizator wykrywa żywy proces z instalacji", UpdaterFindsRunningInstalledProcess),
        ("aktualizator zamyka tylko właściwe okno AMC", UpdaterTargetsOnlyInstalledAmcWindow),
        ("aktualizator przyjmuje zgodny profil", UpdaterAcceptsMatchingProfile),
        ("aktualizator przyjmuje starszą nazwę AMC Public", UpdaterAcceptsLegacyPublicName),
        ("aktualizator rozszerza starszy AMC Public o nowe sesje", UpdaterAcceptsOlderPublicSubset),
        ("aktualizator migruje starszy AMC Public z Sonosa na Frontier Smart", UpdaterAcceptsLegacyPublicSonosProfile),
        ("aktualizator przyjmuje starszy zakres tego samego profilu", UpdaterAcceptsKnownProfileExpansion),
        ("aktualizator odrzuca nieznany zakres starego profilu", UpdaterRejectsUnknownInstalledCapability),
        ("aktualizator odrzuca obcy profil", UpdaterRejectsDifferentProfile),
        ("aktualizator przyjmuje pełny instalator właściwego profilu", UpdaterAcceptsMatchingInstaller),
        ("aktualizator odrzuca instalator innego profilu", UpdaterRejectsForeignInstaller),
        ("aktualizator odrzuca instalator o innej sumie", UpdaterRejectsInstallerWithWrongHash),
        ("aktualizator zachowuje dwie najnowsze kopie ratunkowe", UpdaterKeepsTwoNewestRescueTransactions),
        ("aktualizator chroni kopię bieżącej niepełnej transakcji", UpdaterProtectsCurrentRescueTransaction),
    ];

    public static int Main()
    {
        var failures = 0;
        foreach (var (name, run) in Tests)
        {
            try
            {
                run();
                Console.WriteLine("OK: " + name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("NIEPOWODZENIE: " + name + ": " + exception.Message);
            }
        }
        Console.WriteLine(failures == 0
            ? $"Wszystkie testy launchera przeszły ({Tests.Length})."
            : $"Niepowodzenia launchera: {failures} z {Tests.Length}.");
        return failures == 0 ? 0 : 1;
    }

    private static void ValidBundleIsAccepted()
    {
        using var fixture = BundleFixture.Create();
        var verified = BundleVerifier.Verify(fixture.Root);
        Equal(Path.Combine(fixture.Root, "host", "amc_lite_host.exe"), verified.HostExecutable);
        Equal("test", verified.Profile.ProfileId);
    }

    private static void ValidPublicBundleIsAccepted()
    {
        using var fixture = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        var verified = BundleVerifier.Verify(fixture.Root);
        Equal("public", verified.Profile.ProfileId);
        Equal("AMC Public", verified.Profile.DisplayName);
    }

    private static void NativeTidalCapabilityIsInBothProfiles()
    {
        Equal(true, ReleaseProfileContract.Test.Capabilities.Contains("tidal.native-playback"));
        Equal(true, ReleaseProfileContract.Public.Capabilities.Contains("tidal.native-playback"));
    }

    private static void TamperedProfileIsRejected()
    {
        using var fixture = BundleFixture.Create(
            profile: ReleaseProfileContract.Public,
            displayNameOverride: "AMC Test");
        Throws("nazwa wydania", () => BundleVerifier.Verify(fixture.Root));
    }

    private static void CorruptStartupFileIsRejected()
    {
        using var fixture = BundleFixture.Create();
        File.AppendAllText(Path.Combine(fixture.Root, "app", "amc_wx_lite", "__main__.py"), "uszkodzenie");
        Throws("uszkodzony", () => BundleVerifier.Verify(fixture.Root));
    }

    private static void FullVerificationCoversNonStartupFiles()
    {
        using var fixture = BundleFixture.Create(extraPackageFile: "runtime/library.zip");
        File.AppendAllText(Path.Combine(fixture.Root, "runtime", "library.zip"), "uszkodzenie");
        _ = BundleVerifier.Verify(fixture.Root);
        Throws("uszkodzony", () => BundleVerifier.Verify(fixture.Root, verifyAllFiles: true));
    }

    private static void FullVerificationChecksSizes()
    {
        using var fixture = BundleFixture.Create(extraPackageFile: "runtime/library.zip");
        var manifestPath = Path.Combine(fixture.Root, "bundle-manifest.json");
        var text = File.ReadAllText(manifestPath);
        var marker = "\"runtime/library.zip\":";
        var sizesStart = text.IndexOf("\"fileSizes\"", StringComparison.Ordinal);
        var index = text.IndexOf(marker, sizesStart, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException("Fixture nie zawiera rozmiaru pliku");
        var valueStart = index + marker.Length;
        var valueEnd = text.IndexOfAny([',', '}'], valueStart);
        var size = long.Parse(text[valueStart..valueEnd], System.Globalization.CultureInfo.InvariantCulture);
        text = text[..valueStart]
            + (size + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + text[valueEnd..];
        File.WriteAllText(manifestPath, text);
        Throws("rozmiar", () => BundleVerifier.Verify(fixture.Root, verifyAllFiles: true));
    }

    private static void WrongProtocolIsRejected()
    {
        using var fixture = BundleFixture.Create(hostProtocolVersion: 2);
        Throws("wersja silnika", () => BundleVerifier.Verify(fixture.Root));
    }

    private static void TraversalIsRejected()
    {
        using var fixture = BundleFixture.Create(
            extraStartupFile: "../poza-" + Guid.NewGuid().ToString("N") + ".txt");
        Throws("poza folder", () => BundleVerifier.Verify(fixture.Root));
    }

    private static void LaunchPlanUsesOnlyPrivateRuntime()
    {
        using var fixture = BundleFixture.Create();
        var verified = BundleVerifier.Verify(fixture.Root);
        var oldPythonPath = Environment.GetEnvironmentVariable("PYTHONPATH");
        var oldOverride = Environment.GetEnvironmentVariable("AMC_WX_LITE_PYTHON");
        try
        {
            Environment.SetEnvironmentVariable("PYTHONPATH", "C:\\obcy-python");
            Environment.SetEnvironmentVariable("AMC_WX_LITE_PYTHON", "C:\\obcy.exe");
            var plan = PythonLaunchPlan.Create(verified, []);
            Equal(Path.Combine(fixture.Root, "runtime", "python.exe"), plan.StartInfo.FileName);
            Equal(false, plan.StartInfo.UseShellExecute);
            Equal(true, plan.StartInfo.CreateNoWindow);
            Equal(ProcessWindowStyle.Hidden, plan.StartInfo.WindowStyle);
            Equal(true, plan.StartInfo.RedirectStandardOutput);
            Equal(true, plan.StartInfo.RedirectStandardError);
            Equal(false, plan.ShowCapturedOutput);
            Equal(false, plan.StartInfo.Environment.ContainsKey("PYTHONPATH"));
            Equal(false, plan.StartInfo.Environment.ContainsKey("AMC_WX_LITE_PYTHON"));
            Equal(verified.HostExecutable, plan.StartInfo.Environment["AMC_LITE_HOST"]);
            Equal("test", plan.StartInfo.Environment["AMC_RELEASE_PROFILE"]);
            Equal("1", plan.StartInfo.Environment["PYTHONDONTWRITEBYTECODE"]);
            Equal("-m", plan.StartInfo.ArgumentList[0]);
            Equal("amc_wx_lite", plan.StartInfo.ArgumentList[1]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PYTHONPATH", oldPythonPath);
            Environment.SetEnvironmentVariable("AMC_WX_LITE_PYTHON", oldOverride);
        }
    }

    private static void DiagnosticUsesConsoleRuntime()
    {
        using var fixture = BundleFixture.Create();
        var plan = PythonLaunchPlan.Create(
            BundleVerifier.Verify(fixture.Root),
            ["--sprawdz"]);
        Equal(Path.Combine(fixture.Root, "runtime", "python.exe"), plan.StartInfo.FileName);
        Equal(true, plan.ShowCapturedOutput);
        Equal(true, plan.StartInfo.RedirectStandardOutput);
        Equal(true, plan.StartInfo.RedirectStandardError);
        Equal("--sprawdz", plan.StartInfo.ArgumentList[2]);
    }

    private static void PostUpdateActivationSwitchIsInternal()
    {
        var parsed = PostUpdateLaunchContract.Parse(
            [PostUpdateLaunchContract.ActivateAfterUpdateSwitch, "plik.mp3"]);
        Equal(true, parsed.ActivateAfterStart);
        Equal(1, parsed.ForwardedArguments.Length);
        Equal("plik.mp3", parsed.ForwardedArguments[0]);

        using var fixture = BundleFixture.Create();
        var plan = PythonLaunchPlan.Create(
            BundleVerifier.Verify(fixture.Root),
            parsed.ForwardedArguments);
        Equal(false, plan.StartInfo.ArgumentList.Contains(
            PostUpdateLaunchContract.ActivateAfterUpdateSwitch));

        var ordinary = PostUpdateLaunchContract.Parse(["--sprawdz"]);
        Equal(false, ordinary.ActivateAfterStart);
        Equal("--sprawdz", ordinary.ForwardedArguments[0]);
    }

    private static void FrontendReceivesShellRequestsOnStandardInput()
    {
        using var fixture = BundleFixture.Create();
        var plan = PythonLaunchPlan.Create(BundleVerifier.Verify(fixture.Root), []);
        Equal(true, plan.StartInfo.RedirectStandardInput);
        Equal("1", plan.StartInfo.Environment["AMC_SHELL_REQUESTS"]);
    }

    private static void ShellArgumentsPreservePathsAndQueueMode()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "amc-shell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = Path.Combine(root, "Żółć, średnik; (1).mp3");
            var second = Path.Combine(root, "drugi plik.flac");
            File.WriteAllText(first, "pierwszy");
            File.WriteAllText(second, "drugi");
            var request = ShellLaunchRequestContract.ParseAndValidate(
                [second, ShellIntegrationRules.QueueSwitch, first]);
            Equal(true, request.AddToQueue);
            Equal(2, request.Files.Count);
            Equal(second, request.Files[0]);
            Equal(first, request.Files[1]);

            ThrowsShell(
                "typ pliku",
                () => ShellLaunchRequestContract.ParseAndValidate(
                    [Path.Combine(root, "nie-obslugiwany.xyz")],
                    _ => true));
            var unc = ShellLaunchRequestContract.ParseAndValidate(
                [@"\\serwer\udział z odstępem\plik.mp3"],
                _ => true);
            Equal(@"\\serwer\udział z odstępem\plik.mp3", unc.Files[0]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void ShellHandoffUsesSingleLineJson()
    {
        var original = new ShellLaunchRequest(
            [@"C:\Muzyka\Żółć, średnik; (1).mp3"],
            true);
        var encoded = ShellIntegrationRules.EncodeHandoff(original);
        Equal(false, encoded.Contains('\n'));
        var decoded = ShellIntegrationRules.DecodeHandoff(encoded);
        Equal(true, decoded.AddToQueue);
        Equal(original.Files[0], decoded.Files[0]);
    }

    private static void ReleaseProfilesHaveSeparateShellChannels()
    {
        Equal(false, string.Equals(
            ShellRequestChannel.PipeName(ReleaseProfileContract.Test),
            ShellRequestChannel.PipeName(ReleaseProfileContract.Public),
            StringComparison.Ordinal));
    }

    private static void ShellChannelForwardsToExistingInstance()
    {
        // Kanał użytkowego profilu może należeć do równolegle uruchomionego
        // AMC albo innego przebiegu testów. Unikalna tożsamość zachowuje ten
        // sam kontrakt potoku, ale nie pozwala testowi wejść do obcej instancji.
        var profile = ReleaseProfileContract.Test with
        {
            ProfileId = "test-" + Guid.NewGuid().ToString("N"),
        };
        var received = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = ShellRequestChannel.Start(
            profile,
            payload => received.TrySetResult(payload));
        var request = new ShellLaunchRequest([@"C:\Muzyka\plik.mp3"], false);
        Equal(true, ShellRequestChannel.TrySend(
            profile,
            request,
            5_000));
        var forwarded = received.Task.WaitAsync(TimeSpan.FromSeconds(5))
            .GetAwaiter().GetResult();
        var decoded = ShellIntegrationRules.DecodeHandoff(forwarded);
        Equal(false, decoded.AddToQueue);
        Equal(request.Files[0], decoded.Files[0]);
    }

    private static void OnlyOneLauncherOwnsTheLease()
    {
        var name = @"Local\AMC-Python-Launcher-Test-" + Guid.NewGuid().ToString("N");
        using var first = LauncherInstanceLease.TryAcquire(name);
        Equal(true, first.IsOwner);

        var secondOwns = Task.Run(() =>
        {
            using var second = LauncherInstanceLease.TryAcquire(name);
            return second.IsOwner;
        }).GetAwaiter().GetResult();
        Equal(false, secondOwns);

        first.Dispose();
        var thirdOwns = Task.Run(() =>
        {
            using var third = LauncherInstanceLease.TryAcquire(name);
            return third.IsOwner;
        }).GetAwaiter().GetResult();
        Equal(true, thirdOwns);
    }

    private static void ReleaseProfilesHaveSeparateLauncherLeases()
    {
        Equal(false, string.Equals(
            ReleaseProfileContract.Test.LauncherMutexName,
            ReleaseProfileContract.Public.LauncherMutexName,
            StringComparison.Ordinal));
        var suffix = ".Probe." + Guid.NewGuid().ToString("N");
        using var test = LauncherInstanceLease.TryAcquire(
            ReleaseProfileContract.Test.LauncherMutexName + suffix);
        using var publicRelease = LauncherInstanceLease.TryAcquire(
            ReleaseProfileContract.Public.LauncherMutexName + suffix);
        Equal(true, test.IsOwner);
        Equal(true, publicRelease.IsOwner);
    }

    private static void ReleaseProfilesHaveSeparateUpdaterLeases()
    {
        Equal(false, string.Equals(
            ReleaseProfileContract.Test.UpdaterMutexName,
            ReleaseProfileContract.Public.UpdaterMutexName,
            StringComparison.Ordinal));
        var suffix = ".Probe." + Guid.NewGuid().ToString("N");
        using var test = UpdaterInstanceLease.TryAcquire(
            ReleaseProfileContract.Test.UpdaterMutexName + suffix);
        using var publicRelease = UpdaterInstanceLease.TryAcquire(
            ReleaseProfileContract.Public.UpdaterMutexName + suffix);
        Equal(true, test.IsOwner);
        Equal(true, publicRelease.IsOwner);
    }

    private static void SameBuildOnlyActivates()
    {
        Equal(
            ExistingInstanceAction.Activate,
            ExistingInstancePolicy.Decide(
                "2026.10.14-1fe0f900-a1b2c3d4",
                "AMC Python — wersja 0.1.0, kompilacja 2026.10.14-1fe0f900-a1b2c3d4",
                @"D:\Projekty Codex\AMC Python",
                @"D:\Projekty Codex\AMC Python",
                "AMC Python"));
    }

    private static void LauncherRecognizesCurrentAndLegacyTitles()
    {
        Equal(true, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Test,
            "AMC Test — 18.10.2026, 13:30"));
        Equal(true, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Test,
            "AMC Python — 18.10.2026, 13:29"));
        Equal(false, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Test,
            "AMC — 18.10.2026, 13:29"));
        Equal(true, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Public,
            "AMC Public — 18.10.2026, 13:30"));
        Equal(true, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Public,
            "AMC — 18.10.2026, 13:29"));
        Equal(false, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Public,
            "AMC Test — 18.10.2026, 13:30"));
        Equal(false, ProductIdentity.IsMainWindowTitle(ReleaseProfileContract.Test,
            "Diagnostyka AMC Test"));
    }

    private static void NewBuildRequestsSafeReplacement()
    {
        Equal(
            ExistingInstanceAction.RequestSafeReplacement,
            ExistingInstancePolicy.Decide(
                "2026.10.14-1fe0f900-a1b2c3d4",
                "AMC Python — wersja 0.1.0, kompilacja 2026.10.14-1fe0f900-e5f6a7b8",
                @"D:\Projekty Codex\AMC Python",
                @"D:\Projekty Codex\AMC Python",
                "AMC Python"));
    }

    private static void PreferredInstallationWins()
    {
        Equal(
            ExistingInstanceAction.RequestSafeReplacement,
            ExistingInstancePolicy.Decide(
                "2026.10.13-6c48ebf",
                "AMC Python — wersja 0.1.0",
                @"D:\Projekty Codex\AMC Python",
                @"D:\Projekty Codex\wydanie testowe",
                "AMC Python"));
        Equal(
            ExistingInstanceAction.Activate,
            ExistingInstancePolicy.Decide(
                "RuntimeObject { Id = tajne }",
                "AMC Python — wersja 0.1.0, kompilacja RuntimeObject { Id = tajne }",
                @"D:\Projekty Codex\wydanie testowe",
                @"D:\Projekty Codex\AMC Python",
                "AMC Python"));
    }

    private static void UpdaterRecognizesInstalledComponents()
    {
        var root = Path.Combine("D:\\", "Projekty Codex", "AMC Python");
        Equal(true, InstalledProcessGuard.IsInsideInstallation(
            root, Path.Combine(root, "AMC.exe")));
        Equal(true, InstalledProcessGuard.IsInsideInstallation(
            root, Path.Combine(root, "runtime", "python.exe")));
        Equal(true, InstalledProcessGuard.IsInsideInstallation(
            root, Path.Combine(root, "host", "amc_lite_host.exe")));
        Equal(true, InstalledProcessGuard.IsInsideInstallation(
            root.ToUpperInvariant(), Path.Combine(root.ToLowerInvariant(), "AMC.exe")));
        Equal(false, InstalledProcessGuard.IsInsideInstallation(
            root, Path.Combine("D:\\", "Projekty Codex", "AMC Python stary", "AMC.exe")));
        Equal(false, InstalledProcessGuard.IsInsideInstallation(root, null));
    }

    private static void UpdaterFindsRunningInstalledProcess()
    {
        var root = Path.Combine(
            Path.GetTempPath(), "amc-updater-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var command = Environment.GetEnvironmentVariable("ComSpec")
            ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var installedCommand = Path.Combine(root, "runtime", "amc-test-process.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(installedCommand)!);
        File.Copy(command, installedCommand);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = installedCommand,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "/d", "/c", "ping -n 30 127.0.0.1 > nul" },
        }) ?? throw new InvalidOperationException("Nie uruchomiono procesu próbnego.");
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!InstalledProcessGuard.HasRunningComponent(root))
            {
                if (process.HasExited || DateTime.UtcNow >= deadline)
                    throw new InvalidOperationException(
                        "Nie wykryto żywego procesu uruchomionego z instalacji.");
                Thread.Sleep(50);
            }
            var blockers = InstalledProcessGuard.RunningComponents(root);
            Equal(1, blockers.Count);
            Equal(process.Id, blockers[0].ProcessId);
            Equal(true, InstalledProcessGuard.IsInsideInstallation(
                root,
                blockers[0].Executable));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5_000);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static void UpdaterTargetsOnlyInstalledAmcWindow()
    {
        var root = Path.Combine("D:\\", "Projekty Codex", "AMC Python");
        var runtime = Path.Combine(root, "runtime", "python.exe");
        Equal(true, SafeApplicationStop.IsCandidateWindow(
            root,
            ReleaseProfileContract.Test,
            runtime,
            "AMC Test — 18.10.2026, 13:30"));
        Equal(true, SafeApplicationStop.IsCandidateWindow(
            root,
            ReleaseProfileContract.Test,
            runtime,
            "AMC Python — wersja 0.1.0, kompilacja 2026.10.13-test"));
        Equal(true, SafeApplicationStop.IsCandidateWindow(
            root,
            ReleaseProfileContract.Test,
            runtime,
            "AMC Python — 14.10.2026, 18:52"));
        Equal(true, SafeApplicationStop.IsCandidateWindow(
            root, ReleaseProfileContract.Test, runtime, "AMC Python"));
        Equal(false, SafeApplicationStop.IsCandidateWindow(
            root, ReleaseProfileContract.Public, runtime, "AMC Test"));
        Equal(true, SafeApplicationStop.IsCandidateWindow(
            root, ReleaseProfileContract.Public, runtime, "AMC"));
        Equal(false, SafeApplicationStop.IsCandidateWindow(
            root,
            ReleaseProfileContract.Test,
            Path.Combine("D:\\", "Projekty Codex", "inna kopia", "runtime", "python.exe"),
            "AMC Python — wersja 0.1.0, kompilacja 2026.10.13-test"));
        Equal(false, SafeApplicationStop.IsCandidateWindow(
            root,
            ReleaseProfileContract.Test,
            runtime,
            "AMC Python - techniczne okno pomocnicze"));
    }

    private static void LauncherBlocksStartDuringProfileUpdate()
    {
        using var test = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        using var publicBundle = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        using var ready = new ManualResetEventSlim();
        using var finish = new ManualResetEventSlim();
        var updater = new Thread(() =>
        {
            using var lease = UpdaterInstanceLease.TryAcquire(
                ReleaseProfileContract.Test.UpdaterMutexName);
            ready.Set();
            finish.Wait();
        });
        updater.Start();
        try
        {
            if (!ready.Wait(TimeSpan.FromSeconds(5)))
                throw new InvalidOperationException("Mutex aktualizatora nie został zajęty");
            Equal("test", UpdateInProgressGate.ActiveProfile(test.Root)?.ProfileId);
            Equal(null, UpdateInProgressGate.ActiveProfile(publicBundle.Root));
        }
        finally
        {
            finish.Set();
            updater.Join(TimeSpan.FromSeconds(5));
        }
        Equal(null, UpdateInProgressGate.ActiveProfile(test.Root));
    }

    private static void InstallerChecksAreNarrow()
    {
        Equal(true, UpdateInProgressGate.IsInstallerCheck(
            [UpdateInProgressGate.InstallerFullCheck]));
        Equal(true, UpdateInProgressGate.IsInstallerCheck(
            [UpdateInProgressGate.InstallerStartCheck]));
        Equal(false, UpdateInProgressGate.IsInstallerCheck([]));
        Equal(false, UpdateInProgressGate.IsInstallerCheck(
            ["--sprawdz-pakiet-pelny-cicho"]));
        Equal(false, UpdateInProgressGate.IsInstallerCheck(
            [UpdateInProgressGate.InstallerFullCheck, "--activate-after-update"]));
        Equal(false, UpdateInProgressGate.IsInstallerCheck(["--sprawdz-start-cicho"]));
    }

    private static void InstalledBundlePassesFullVerification()
    {
        using var test = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        using var publicBundle = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        AccessibleMediaController.PythonUpdater.Program.VerifyInstalledBundle(
            test.Root, ReleaseProfileContract.Test);
        AccessibleMediaController.PythonUpdater.Program.VerifyInstalledBundle(
            publicBundle.Root, ReleaseProfileContract.Public);
    }

    private static void InstalledBundleDetectsMissingFile()
    {
        using var fixture = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        File.Delete(Path.Combine(fixture.Root, "runtime", "python.exe"));
        Throws("brak pliku", () => AccessibleMediaController.PythonUpdater.Program
            .VerifyInstalledBundle(fixture.Root, ReleaseProfileContract.Test));
    }

    private static void InstalledBundleDetectsCorruptNonStartupFile()
    {
        using var fixture = BundleFixture.Create(
            profile: ReleaseProfileContract.Public,
            extraPackageFile: "runtime/library.zip");
        File.AppendAllText(Path.Combine(fixture.Root, "runtime", "library.zip"), "błąd");
        Throws("rozmiar", () => AccessibleMediaController.PythonUpdater.Program
            .VerifyInstalledBundle(fixture.Root, ReleaseProfileContract.Public));
    }

    private static void UpdaterAcceptsMatchingProfile()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        using var target = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        var manifest = AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
            source.Root,
            target.Root,
            ReleaseProfileContract.Public);
        Equal("public", manifest.ReleaseProfile);
        Equal("stable", manifest.UpdateChannel);
    }

    private static void UpdaterAcceptsLegacyPublicName()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        using var target = BundleFixture.Create(
            profile: ReleaseProfileContract.Public,
            displayNameOverride: "AMC");
        var manifest = AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
            source.Root,
            target.Root,
            ReleaseProfileContract.Public);
        Equal("public", manifest.ReleaseProfile);
    }

    private static void UpdaterAcceptsOlderPublicSubset()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        using var target = BundleFixture.Create(
            profile: ReleaseProfileContract.Public,
            sessionsOverride: ReleaseProfileContract.Public.Sessions
                .Where(value => value is not ("spotify" or "radioarchiwista")).ToArray(),
            capabilitiesOverride: ReleaseProfileContract.Public.Capabilities
                .Where(value => value is not (
                    "session.spotify" or "spotify.playlists" or
                    "session.radioarchiwista" or "youtube.account.readonly")).ToArray());
        var manifest = AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
            source.Root,
            target.Root,
            ReleaseProfileContract.Public);
        Equal("public", manifest.ReleaseProfile);
    }

    private static void UpdaterAcceptsLegacyPublicSonosProfile()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        using var target = BundleFixture.Create(
            profile: ReleaseProfileContract.Public,
            sessionsOverride: ReleaseProfileContract.Public.Sessions
                .Where(value => value != "frontierSmart")
                .Append("sonos").ToArray(),
            capabilitiesOverride: ReleaseProfileContract.Public.Capabilities
                .Where(value => value != "session.frontier-smart")
                .Append("session.sonos").ToArray());
        var manifest = AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
            source.Root,
            target.Root,
            ReleaseProfileContract.Public);
        Equal("public", manifest.ReleaseProfile);
    }

    private static void UpdaterRejectsDifferentProfile()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Public);
        using var target = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        Throws(
            "innego wydania",
            () => AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
                source.Root,
                target.Root,
                ReleaseProfileContract.Public));
    }

    private static void UpdaterAcceptsKnownProfileExpansion()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        using var target = BundleFixture.Create(
            profile: ReleaseProfileContract.Test,
            sessionsOverride: ReleaseProfileContract.Test.Sessions
                .Where(value => value != "frontierSmart").Reverse().ToArray(),
            capabilitiesOverride: ReleaseProfileContract.Test.Capabilities
                .Where(value => value is not "session.frontier-smart" and not "tidal.native-playback")
                .Reverse().ToArray());
        var manifest = AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
            source.Root,
            target.Root,
            ReleaseProfileContract.Test);
        Equal("test", manifest.ReleaseProfile);
    }

    private static void UpdaterRejectsUnknownInstalledCapability()
    {
        using var source = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        using var target = BundleFixture.Create(
            profile: ReleaseProfileContract.Test,
            capabilitiesOverride: ReleaseProfileContract.Test.Capabilities
                .Append("session.unknown").ToArray());
        Throws(
            "niezgodne pole: lista możliwości",
            () => AccessibleMediaController.PythonUpdater.Program.LoadAndVerify(
                source.Root,
                target.Root,
                ReleaseProfileContract.Test));
    }

    private static void UpdaterAcceptsMatchingInstaller()
    {
        using var target = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        WithInstaller("AMC-Test-2026-10-27-0900.exe", (installer, sha256) =>
            AccessibleMediaController.PythonUpdater.Program.VerifyInstaller(
                installer,
                sha256,
                target.Root,
                ReleaseProfileContract.Test));
    }

    private static void UpdaterRejectsForeignInstaller()
    {
        using var target = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        WithInstaller("AMC-Public-2026-10-27-0900.exe", (installer, sha256) =>
            Throws(
                "innego wydania",
                () => AccessibleMediaController.PythonUpdater.Program.VerifyInstaller(
                    installer,
                    sha256,
                    target.Root,
                    ReleaseProfileContract.Test)));
    }

    private static void UpdaterRejectsInstallerWithWrongHash()
    {
        using var target = BundleFixture.Create(profile: ReleaseProfileContract.Test);
        WithInstaller("AMC-Test-2026-10-27-0900.exe", (installer, _) =>
            Throws(
                "niezgodna suma",
                () => AccessibleMediaController.PythonUpdater.Program.VerifyInstaller(
                    installer,
                    new string('0', 64),
                    target.Root,
                    ReleaseProfileContract.Test)));
    }

    private static void UpdaterKeepsTwoNewestRescueTransactions()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "amc-updater-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var transactions = Enumerable.Range(0, 4)
                .Select(_ => Path.Combine(
                    root,
                    ".aktualizacja-" + Guid.NewGuid().ToString("N")))
                .ToArray();
            for (var index = 0; index < transactions.Length; index++)
            {
                Directory.CreateDirectory(transactions[index]);
                File.WriteAllText(Path.Combine(transactions[index], "kopia.bin"), index.ToString());
                Directory.SetLastWriteTimeUtc(
                    transactions[index],
                    DateTime.UtcNow.AddHours(-transactions.Length + index));
            }
            var unrelated = Path.Combine(root, ".aktualizacja-nie-jest-identyfikatorem");
            Directory.CreateDirectory(unrelated);

            var removed = AccessibleMediaController.PythonUpdater.Program
                .PruneRescueTransactions(root);

            Equal(2, removed);
            Equal(false, Directory.Exists(transactions[0]));
            Equal(false, Directory.Exists(transactions[1]));
            Equal(true, Directory.Exists(transactions[2]));
            Equal(true, Directory.Exists(transactions[3]));
            Equal(true, Directory.Exists(unrelated));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void UpdaterProtectsCurrentRescueTransaction()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "amc-updater-protected-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var oldest = Path.Combine(
                root,
                ".aktualizacja-" + Guid.NewGuid().ToString("N"));
            var newest = Path.Combine(
                root,
                ".aktualizacja-" + Guid.NewGuid().ToString("N"));
            var current = Path.Combine(
                root,
                ".aktualizacja-" + Guid.NewGuid().ToString("N"));
            foreach (var path in new[] { oldest, newest, current })
                Directory.CreateDirectory(path);
            Directory.SetLastWriteTimeUtc(oldest, DateTime.UtcNow.AddHours(-3));
            Directory.SetLastWriteTimeUtc(newest, DateTime.UtcNow.AddHours(-1));
            Directory.SetLastWriteTimeUtc(current, DateTime.UtcNow.AddHours(-4));

            var removed = AccessibleMediaController.PythonUpdater.Program
                .PruneRescueTransactions(root, current);

            Equal(1, removed);
            Equal(false, Directory.Exists(oldest));
            Equal(true, Directory.Exists(newest));
            Equal(true, Directory.Exists(current));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WithInstaller(string fileName, Action<string, string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "amc-updater-installer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var installer = Path.Combine(directory, fileName);
            File.WriteAllText(installer, "syntetyczny instalator");
            var sha256 = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(installer)));
            action(installer, sha256);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Throws(string expected, Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException exception) when (
            exception.Message.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        throw new InvalidOperationException("Nie otrzymano oczekiwanej odmowy: " + expected);
    }

    private static void ThrowsShell(string expected, Action action)
    {
        try
        {
            action();
        }
        catch (ShellLaunchRequestException exception) when (
            exception.Message.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        throw new InvalidOperationException("Nie otrzymano oczekiwanej odmowy: " + expected);
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Oczekiwano {expected}, otrzymano {actual}");
    }

    private sealed class BundleFixture : IDisposable
    {
        public string Root { get; }

        private readonly string? _outsidePath;

        private BundleFixture(string root, string? outsidePath = null)
        {
            Root = root;
            _outsidePath = outsidePath;
        }

        public static BundleFixture Create(
            int hostProtocolVersion = 1,
            string? extraStartupFile = null,
            string? extraPackageFile = null,
            ReleaseProfileDescriptor? profile = null,
            string? displayNameOverride = null,
            IReadOnlyList<string>? sessionsOverride = null,
            IReadOnlyList<string>? capabilitiesOverride = null)
        {
            profile ??= ReleaseProfileContract.Test;
            var root = Path.Combine(Path.GetTempPath(), "amc-launcher-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string? outsidePath = null;
            var startupFiles = new[]
            {
                "AMC.exe",
                "runtime/python.exe",
                "runtime/pythonw.exe",
                "app/amc_wx_lite/__main__.py",
                "host/amc_lite_host.exe",
            }.ToList();
            var files = startupFiles.ToList();
            if (extraStartupFile is not null)
            {
                files.Add(extraStartupFile);
                startupFiles.Add(extraStartupFile);
                outsidePath = Path.GetFullPath(Path.Combine(root, extraStartupFile));
                File.WriteAllText(outsidePath, "poza");
            }
            if (extraPackageFile is not null) files.Add(extraPackageFile);
            foreach (var relative in files.Where(path => !path.StartsWith("..", StringComparison.Ordinal)))
            {
                var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, "plik:" + relative);
            }
            var hashes = files.ToDictionary(
                relative => relative,
                relative =>
                {
                    var path = Path.GetFullPath(Path.Combine(
                        root,
                        relative.Replace('/', Path.DirectorySeparatorChar)));
                    return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
                },
                StringComparer.OrdinalIgnoreCase);
            var sizes = files.ToDictionary(
                relative => relative,
                relative => new FileInfo(Path.GetFullPath(Path.Combine(
                    root,
                    relative.Replace('/', Path.DirectorySeparatorChar)))).Length,
                StringComparer.OrdinalIgnoreCase);
            var manifest = new
            {
                bundleFormatVersion = 1,
                interfaceVersion = 1,
                hostProtocolVersion,
                application = "AMC Python",
                buildLabel = "2026.10.13-6c48ebf",
                entryPoint = "AMC.exe",
                pythonModule = "amc_wx_lite",
                hostFolder = "host",
                releaseProfileSchema = 1,
                releaseProfile = profile.ProfileId,
                productId = profile.ProductId,
                displayName = displayNameOverride ?? profile.DisplayName,
                installId = profile.InstallId,
                updateChannel = profile.UpdateChannel,
                uiStateFolder = profile.UiStateFolder,
                roamingProfileFolder = profile.RoamingProfileFolder,
                localProfileFolder = profile.LocalProfileFolder,
                cacheFolder = profile.CacheFolder,
                logFolder = profile.LogFolder,
                credentialNamespace = profile.CredentialNamespace,
                sessions = sessionsOverride ?? profile.Sessions,
                capabilities = capabilitiesOverride ?? profile.Capabilities,
                files,
                startupFiles,
                fileHashes = hashes,
                fileSizes = sizes,
            };
            File.WriteAllText(
                Path.Combine(root, "bundle-manifest.json"),
                JsonSerializer.Serialize(manifest));
            return new BundleFixture(root, outsidePath);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
            if (_outsidePath is not null && File.Exists(_outsidePath))
                File.Delete(_outsidePath);
        }
    }
}
