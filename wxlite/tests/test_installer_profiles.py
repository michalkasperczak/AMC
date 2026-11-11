"""Strukturalny kontrakt dwóch instalatorów; bez uruchamiania Inno Setup."""

from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ISS = (ROOT / "installer" / "AMC_Wx_Setup.iss").read_text(encoding="utf-8")
BUILD = (ROOT / "wxlite" / "tools" / "Build-AMCInstaller.ps1").read_text(
    encoding="utf-8"
)
BUNDLE_BUILD = (ROOT / "wxlite" / "tools" / "Build-AMCBundle.ps1").read_text(
    encoding="utf-8"
)
PROMOTE_TEST = (
    ROOT / "wxlite" / "tools" / "Promote-AMCPythonTestBuild.ps1"
).read_text(encoding="utf-8")
LAUNCHER = (
    ROOT / "src" / "AccessibleMediaController.PythonLauncher" / "Program.cs"
).read_text(encoding="utf-8")
PYTHON_MAIN = (ROOT / "wxlite" / "amc_wx_lite" / "__main__.py").read_text(
    encoding="utf-8"
)
GUI = (ROOT / "wxlite" / "amc_wx_lite" / "gui.py").read_text(encoding="utf-8")
UPDATER = (
    ROOT / "src" / "AccessibleMediaController.PythonUpdater" / "Program.cs"
).read_text(encoding="utf-8")


def test_installer_profiles_have_separate_ids_names_folders_and_mutexes() -> None:
    for required in (
        'Produkt "AMC Public"',
        'Produkt "AMC Test"',
        'NazwaWyjscia "AMC-Public-" + EtykietaPliku',
        'NazwaWyjscia "AMC-Test-" + EtykietaPliku',
        "1E437448-30B4-4E4A-BCA1-CDA114B096A9",
        "0B807571-BDC5-4287-BD7C-4F4A7C71438F",
        "PythonLauncher.Public.SingleInstance",
        "PythonLauncher.SingleInstance",
        "FolderProgramu \"AMC Public\"",
        "FolderProgramu \"AMC Test\"",
    ):
        assert required in ISS


def test_shell_integration_is_available_and_isolated_for_both_profiles() -> None:
    """AMC Public i AMC Test mogą współistnieć w powłoce bez wspólnych wpisów."""
    for required in (
        'ShellProfileKey "Public"',
        'ShellProfileKey "Test"',
        'ShellRegisteredApplication "Accessible Multimedia Controller"',
        'ShellRegisteredApplication "Accessible Multimedia Controller — Test"',
        'ShellDisplayName "AMC Public"',
        'ShellDisplayName "AMC Test"',
        'ShellOpenLabel "Otwórz w AMC Public"',
        'ShellOpenLabel "Otwórz w AMC Test"',
        'ShellQueueLabel "Dodaj do kolejki AMC Public"',
        'ShellQueueLabel "Dodaj do kolejki AMC Test"',
        "ShellCapabilitiesRoot = 'Software\\AccessibleMultimediaController\\{#ShellProfileKey}\\Capabilities'",
        "ShellClassPrefix = 'AccessibleMediaController.{#ShellProfileKey}.'",
        "ShellVerbPrefix = 'AccessibleMediaController.{#ShellProfileKey}.'",
    ):
        assert required in ISS

    assert 'RegDeleteKeyIncludingSubkeys(HKA, ShellCapabilitiesRoot)' in ISS
    assert "AssociationRoot + 'open'" in ISS
    assert "AssociationRoot + 'queue'" in ISS
    assert "ShellClassesRoot + ClassName" in ISS
    assert "UserChoice" not in ISS


def test_installer_never_forces_running_amc_to_close() -> None:
    assert "CloseApplications=no" in ISS
    assert "RestartApplications=no" in ISS
    assert "CloseApplications=force" not in ISS
    assert "AppMutex={#LauncherMutex}" in ISS


def test_both_installer_profiles_remove_retired_librespot_files() -> None:
    shared_install_delete = ISS[ISS.index("[InstallDelete]"):ISS.index('#if Profil == "public"', ISS.index("[InstallDelete]"))]
    assert 'Type: filesandordirs; Name: "{app}\\host\\LibrespotHost"' in shared_install_delete
    assert 'Type: filesandordirs; Name: "{app}\\host\\licenses\\librespot-dependencies"' in shared_install_delete
    assert 'Type: files; Name: "{app}\\host\\licenses\\librespot-MIT.txt"' in shared_install_delete


def test_installer_shortcuts_are_optional_and_noicons_is_effective() -> None:
    assert "DisableProgramGroupPage=auto" in ISS
    assert "AllowNoIcons=yes" in ISS
    assert 'Name: "desktopicon"' in ISS
    assert 'Flags: unchecked' in ISS
    assert ISS.count("Check: not WizardNoIcons") == 3
    assert "Tasks: desktopicon" in ISS


def test_test_installer_offers_verified_quick_app_switcher_without_silent_install() -> None:
    for required in (
        "#ifdef NvdaAddonPath",
        'Source: "{#NvdaAddonPath}"; DestDir: "{tmp}"',
        'Filename: "{tmp}\\{#NvdaAddonFileName}"',
        "Uruchom instalację dodatku NVDA Quick App Switcher {#NvdaAddonVersion}",
        "Flags: shellexec postinstall skipifsilent",
        "Check: ShouldOfferNvdaAddon",
        "InstalledQuickAppSwitcherVersion",
        "CompareDottedVersions",
        "{userappdata}\\nvda\\addons\\quickAppSwitcher\\manifest.ini",
    ):
        assert required in ISS

    run_section = ISS[ISS.index("[Run]") : ISS.index("[Code]")]
    addon_run = run_section.index('Filename: "{tmp}\\{#NvdaAddonFileName}"')
    app_run = run_section.index('Filename: "{app}\\AMC.exe"')
    assert addon_run < app_run
    assert "restartreplace" not in run_section[addon_run:app_run]


def test_amc_nvda_addon_is_verified_and_offered_for_both_profiles() -> None:
    for required in (
        '[string]$AmcNvdaAddonPath = ""',
        '"amcController"',
        '"AMC-NVDA-$amcAddonVersion.nvda-addon"',
        'amcNvdaAddon = $amcNvdaAddon',
        '/DAmcNvdaAddonPath=',
        '/DAmcNvdaAddonVersion=',
    ):
        assert required in BUILD

    for required in (
        '#ifdef AmcNvdaAddonPath',
        'Source: "{#AmcNvdaAddonPath}"; DestDir: "{tmp}"',
        'Filename: "{tmp}\\{#AmcNvdaAddonFileName}"',
        'Check: ShouldOfferAmcNvdaAddon',
        'amcController\\manifest.ini',
        "if Value = '' then",
    ):
        assert required in ISS


def test_silent_update_installs_older_amc_nvda_addon_automatically() -> None:
    prepare = ISS[ISS.index("function PrepareToInstall"):]
    assert "WizardSilent" in prepare
    assert "ShouldOfferAmcNvdaAddon" in prepare
    assert "CheckForMutexes(" in prepare
    assert "Zakończ nagrywanie i ponów aktualizację" in prepare
    assert "amc-nvda-update.py" in prepare
    assert "runtime\\python.exe" in prepare
    assert "DirExists(ExpandConstant('{userappdata}\\nvda\\addons'))" in prepare
    assert "/DAmcNvdaUpdateHelperPath=" in BUILD
    assert "Source: \"{#AmcNvdaUpdateHelperPath}\"" in ISS
    assert "ewWaitUntilTerminated" in prepare
    assert "skipifsilent" in ISS[ISS.index("[Run]"):ISS.index("[Code]")]


def test_installer_performs_full_post_install_integrity_check() -> None:
    assert "--sprawdz-pakiet-instalatora-pelny-cicho" in ISS
    assert "--sprawdz-start-instalatora-cicho" in ISS
    assert "ewWaitUntilTerminated" in ISS
    assert "ResultCode <> 0" in ISS
    assert "RaiseException" in ISS
    assert ISS.index("--sprawdz-start-instalatora-cicho") < ISS.index("RegisterShellIntegration;", ISS.index("procedure CurStepChanged"))


def test_launcher_is_copied_before_runtime_during_silent_update() -> None:
    files_section = ISS.split("[Files]", 1)[1].split("[InstallDelete]", 1)[0]
    assert files_section.index('Source: "AMC.exe"') < files_section.index('Source: "*"')
    assert 'Excludes: "AMC.exe"' in files_section


def test_installer_refuses_to_overlay_another_or_historical_product() -> None:
    assert 'ExpectedInstallId "AMC.Public.win-x64"' in ISS
    assert 'ExpectedInstallId "AMC.Test.win-x64"' in ISS
    assert "DirectoryHasEntries" in ISS
    assert "ExistingDirectoryBelongsToThisProfile" in ISS
    assert 'bundle-manifest.json' in ISS
    assert '"installId": "{#ExpectedInstallId}"' in ISS
    assert "function PrepareToInstall" in ISS
    assert "Instalator nie nadpisze jej plików" in ISS


def test_public_update_removes_modules_disabled_for_that_profile() -> None:
    section_start = ISS.index("[InstallDelete]")
    install_delete = ISS[section_start : ISS.index("#endif", section_start)]

    for stale_name in (
        "sonos_source.py",
        "spotify_playlist_dialog.py",
        "spotify_source.py",
        "tidal_source.py",
    ):
        assert f'Name: "{{app}}\\app\\amc_wx_lite\\{stale_name}"' in install_delete
        assert stale_name.removesuffix(".py") + ".*.pyc" in install_delete

    assert "frontier_smart_source" not in install_delete


def test_installer_refuses_local_oauth_secrets() -> None:
    assert "forbiddenBundleFiles" in BUILD
    assert "youtube-oauth-client-secret.txt" in BUILD
    assert "sonos_client_secret.txt" in BUILD
    assert "Instalator nie może zawierać sekretu" in BUILD
    assert 'secretScan = "passed"' in BUILD


def test_build_helper_verifies_profile_clean_source_and_full_bundle() -> None:
    assert 'ValidateSet("public", "test")' in BUILD
    assert "manifest.releaseProfile" in BUILD
    assert "manifest.sourceDirty" in BUILD
    assert "git status --porcelain --untracked-files=no" in BUILD
    assert "installerSourceRevision" in BUILD
    assert "Paczka i instalator muszą pochodzić z tego samego commitu" in BUILD
    assert ".VersionInfo.ProductVersion" in BUILD
    assert "$launcherVersion -ne $Version" in BUILD
    assert "Numer instalatora $Version nie zgadza się" in BUILD
    assert "--sprawdz-pakiet-pelny-cicho" in BUILD
    assert "Test-AMCBundleStartup.ps1" in BUILD
    assert "& $startupSmokeTool -BundleRoot $bundle" in BUILD
    assert "Get-FileHash" in BUILD
    assert "sha256" in BUILD
    assert "bytes" in BUILD
    assert "manifest.displayVersion" in BUILD
    assert '"AMC-Public-$fileTimestamp.exe"' in BUILD
    assert '"AMC-Test-$fileTimestamp.exe"' in BUILD
    assert "CreateFromDirectory" not in BUILD
    assert "updatePackageSha256" not in BUILD
    assert "/DEtykietaUzytkowa" in BUILD
    assert "AppVerName={#KrotkaNazwa} — {#EtykietaUzytkowa}" in ISS
    assert '$env:LOCALAPPDATA "Programs\\Inno Setup 6\\ISCC.exe"' in BUILD


def test_test_installer_requires_and_validates_quick_app_switcher_package() -> None:
    for required in (
        '[string]$NvdaAddonPath = ""',
        '$Profile -eq "test"',
        "Instalator AMC Test wymaga paczki Quick App Switcher",
        '[IO.Compression.ZipFile]::OpenRead($resolvedAddon)',
        '"manifest.ini"',
        '"quickAppSwitcher"',
        '"quickAppSwitcher-$addonVersion.nvda-addon"',
        "Get-FileHash -LiteralPath $resolvedAddon -Algorithm SHA256",
        "/DNvdaAddonPath=",
        "/DNvdaAddonFileName=",
        "/DNvdaAddonVersion=",
        "nvdaAddon = $nvdaAddon",
    ):
        assert required in BUILD


def test_test_build_promotion_runs_launcher_and_updater_contracts() -> None:
    assert "AccessibleMediaController.PythonLauncher.Tests.csproj" in PROMOTE_TEST
    assert '"Testy launchera i aktualizatora"' in PROMOTE_TEST


def test_application_updater_can_remove_only_verified_staging_after_success() -> None:
    assert 'case "--cleanup-source":' in UPDATER
    assert "ApplyPackage(options.Source, options.Target, manifest!.Files);" in UPDATER
    assert "CleanupSourceBestEffort(options.Source, options.Target);" in UPDATER
    assert UPDATER.index("ApplyPackage(options.Source") < UPDATER.index(
        "CleanupSourceBestEffort(options.Source"
    )
    assert "Directory.Delete(resolvedSource, recursive: true)" in UPDATER
    assert "IsInside(resolvedSource, resolvedTarget)" in UPDATER
    assert "IsInside(resolvedTarget, resolvedSource)" in UPDATER


def test_application_updater_runs_the_verified_full_installer() -> None:
    for required in (
        'case "--installer"',
        'case "--installer-sha256"',
        'case "--cleanup-installer":',
        '"/VERYSILENT"',
        '"/SUPPRESSMSGBOXES"',
        '"/NORESTART"',
        '"/DIR=" + target',
        "VerifyInstaller(",
        "RunInstaller(options.Installer!",
    ):
        assert required in UPDATER
    assert UPDATER.index("VerifyInstaller(") < UPDATER.index(
        "SafeApplicationStop.RequestAndWait"
    )
    assert UPDATER.index("RunInstaller(options.Installer!") < UPDATER.index(
        "CleanupInstallerBestEffort(options.Installer!"
    )
    assert "File.Delete(resolvedInstaller)" in UPDATER
    assert "Directory.Delete(parent)" in UPDATER
    assert "recursive: true" not in UPDATER[
        UPDATER.index("private static void CleanupInstallerBestEffort") :
        UPDATER.index("private static bool RelaunchOutsideInstallation")
    ]
    assert "UsePreviousAppDir=yes" in ISS
    assert "UsePreviousTasks=yes" in ISS


def test_successful_update_restarts_without_confirmation_and_requests_foreground() -> None:
    assert "Aktualizacja {profile.DisplayName} została zainstalowana" not in UPDATER
    assert "PostUpdateLaunchContract.ActivateAfterUpdateSwitch" in UPDATER
    assert "AllowForegroundBestEffort(restarted.Id)" in UPDATER
    assert "PostUpdateLaunchContract.Parse(args)" in LAUNCHER
    assert "AllowForegroundBestEffort(process.Id)" in LAUNCHER
    assert "WaitForMainWindow(" in LAUNCHER


def test_bundle_builder_embeds_dotnet_and_private_python_runtime() -> None:
    assert "$hostOutput" in BUNDLE_BUILD
    assert "$host =" not in BUNDLE_BUILD
    assert BUNDLE_BUILD.count('"--self-contained", "true"') == 3
    assert BUNDLE_BUILD.count('"--artifacts-path"') == 3
    assert '"--profile", $Profile' in BUNDLE_BUILD
    assert '"--runtime", $runtime' in BUNDLE_BUILD
    assert "--sprawdz-pakiet-pelny-cicho" in BUNDLE_BUILD
    assert "Test-AMCBundleStartup.ps1" in BUNDLE_BUILD
    assert "& $startupSmokeTool -BundleRoot $candidate" in BUNDLE_BUILD
    assert "Assert-ManifestCoversAllFiles -BundleRoot $candidate" in BUNDLE_BUILD
    assert BUNDLE_BUILD.count("Invoke-FullBundleCheck -LauncherPath") == 2
    assert "Start-Process -FilePath $LauncherPath" in BUNDLE_BUILD
    assert "-Wait -PassThru -WindowStyle Hidden" in BUNDLE_BUILD
    assert "Folder docelowy już istnieje" in BUNDLE_BUILD
    assert "Move-Item -LiteralPath $candidate" in BUNDLE_BUILD


def test_bundle_builder_cleans_only_its_unique_project_temporary_folder() -> None:
    assert 'Join-Path $repositoryRoot ".test-tmp"' in BUNDLE_BUILD
    assert '"bundle-build-" + [guid]::NewGuid()' in BUNDLE_BUILD
    assert "Remove-Item -LiteralPath $operationRoot -Recurse -Force" in BUNDLE_BUILD


def test_bundle_and_installer_reject_files_outside_integrity_manifest() -> None:
    assert "Paczka zawiera pliki nieobjęte manifestem" in BUNDLE_BUILD
    assert "Instalator nie może zawierać plików nieobjętych manifestem" in BUILD
    assert 'relative -ne "bundle-manifest.json"' in BUNDLE_BUILD
    assert 'relative -ne "bundle-manifest.json"' in BUILD


def test_test_profile_promotion_smokes_candidate_and_installed_copy_before_launch() -> None:
    assert "Test-AMCBundleStartup.ps1" in PROMOTE_TEST
    candidate_gate = "& $StartupSmokeTool -BundleRoot $candidateRoot"
    installed_gate = "& $StartupSmokeTool -BundleRoot $InstallRoot"
    visible_launch = "Start-Process -FilePath $InstalledLauncher"
    assert candidate_gate in PROMOTE_TEST
    assert installed_gate in PROMOTE_TEST
    assert PROMOTE_TEST.index(candidate_gate) < PROMOTE_TEST.index(
        '"--source", $candidateRoot'
    )
    assert PROMOTE_TEST.index(installed_gate) < PROMOTE_TEST.index(visible_launch)


def test_startup_smoke_requires_ready_marker_timeout_and_no_leftovers() -> None:
    smoke = (ROOT / "wxlite" / "tools" / "Test-AMCBundleStartup.ps1").read_text(
        encoding="utf-8"
    )
    for required in (
        "--sprawdz-start-cicho",
        "AMC_STARTUP_SMOKE_RESULT",
        'result -ne "READY"',
        "WaitForExit($TimeoutSeconds * 1000)",
        "Get-Process -ErrorAction SilentlyContinue",
        "Kontrolowany start pozostawił procesy",
        '"startup-smoke-" + [guid]::NewGuid()',
    ):
        assert required in smoke


def test_startup_smoke_is_isolated_and_cannot_activate_an_existing_instance() -> None:
    assert '"--sprawdz-start-cicho"' in LAUNCHER
    assert "suppressFailureDialog: true" in LAUNCHER
    assert "if (!suppressFailureDialog)\n            AppendProcessLog" in LAUNCHER
    assert LAUNCHER.index("if (silentStartupCheck)") < LAUNCHER.index(
        "LauncherInstanceLease.TryAcquire"
    )
    for required in (
        "AMC_STARTUP_SMOKE",
        "AMC_WX_FIXTURE",
        "TemporaryDirectory",
        "APPDATA",
        "LOCALAPPDATA",
    ):
        assert required in PYTHON_MAIN
    assert 'frame.Hide()' in GUI
    assert 'AMC_STARTUP_SMOKE_READY' in GUI
    assert 'self._startup_smoke_ready = True' in GUI
    assert "wx.CallAfter(self.ExitMainLoop)" in GUI
    assert GUI.index("frame.Close(force=True)") < GUI.index(
        "wx.CallAfter(self.ExitMainLoop)"
    )
    assert 'if not _startup_smoke_mode()' in GUI
    activation_handler = GUI[GUI.index("def _on_window_activate"):]
    activation_handler = activation_handler[: activation_handler.index("def ", 4)]
    assert "if _startup_smoke_mode():" in activation_handler
    assert activation_handler.index("if _startup_smoke_mode():") < activation_handler.index(
        "self._windows_preset_10_hotkey.register()"
    )


def test_empty_library_folders_dialog_explains_the_next_action() -> None:
    """Puste Ctrl+F5 ma prowadzić do Dodaj folder, a nie mówić tylko „Gotowe”."""

    dialog = GUI[GUI.index("class LibraryFoldersDialog") :]
    dialog = dialog[: dialog.index("class FrontierSmartDevicesDialog")]
    message = (
        "Brak dodanych folderów. Wybierz Dodaj folder, aby utworzyć Bibliotekę."
    )
    assert message in dialog
    assert "elif not self._sources:" in dialog
    assert "self.add_button.SetFocus()" in dialog
