"""Statyczny kontrakt jednorazowej, skanowanej budowy diagnostycznej AMC Test."""
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = (ROOT / ".github" / "workflows" / "amc-diagnostic-build.yml").read_text(
    encoding="utf-8"
)
BUNDLE = (ROOT / "wxlite" / "tools" / "Build-AMCBundle.ps1").read_text(
    encoding="utf-8"
)
INSTALLER = (ROOT / "wxlite" / "tools" / "Build-AMCInstaller.ps1").read_text(
    encoding="utf-8"
)


def test_diagnostic_workflow_has_one_shot_read_only_build_path() -> None:
    for required in (
        "codex/amc-diagnostic-gh-protected-20261118-verified",
        "github.run_attempt == '1'",
        "github.event.before == '0000000000000000000000000000000000000000'",
        "github.ref == 'refs/heads/codex/amc-diagnostic-gh-protected-20261118-verified'",
        "runs-on: windows-2025",
        "contents: read",
        "persist-credentials: false",
        "test-2026.11.15.2036",
        "AMC-Test-2026-11-15-2036.exe",
        "078ce7cddb4461552071192f7d83bc4623d02bac0d2b55e587d5fde41d7f8591",
        "218531931",
        "innoextract670.zip",
        "79b69b9b1fcd98f42ccd4b245efdf6a03bcfb674ba6af482f5a46891c9ed4d14",
        "innosetup-6.7.3.exe",
        "BeforeCandidateExecution",
        "Build-AMCBundle.ps1 -Profile test",
        "Build-AMCInstaller.ps1 -Profile test",
        "AMC-NVDA-",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ):
        assert required in WORKFLOW
    assert "workflow_dispatch" not in WORKFLOW
    assert "self-hosted" not in WORKFLOW
    assert "actions/cache" not in WORKFLOW
    assert "Publish-AMCRelease" not in WORKFLOW
    assert "gh release" not in WORKFLOW


def test_defender_scans_are_custom_fail_closed_and_cover_job_lifetime() -> None:
    assert "Start-MpScan -ScanType CustomScan" in WORKFLOW
    assert WORKFLOW.count("Invoke-DefenderGate -Target") >= 5
    assert "-WarningAction Stop" not in WORKFLOW
    assert "-WarningAction Continue -WarningVariable +diagWarnings" in WORKFLOW
    assert "if ($diagWarnings.Count -gt 0)" in WORKFLOW
    assert "NoMatchingEventsFound" in WORKFLOW
    assert "$jobStarted" in WORKFLOW
    assert "Get-MpComputerStatus -ErrorAction Stop -WarningAction Continue" in WORKFLOW
    assert "Get-MpPreference -ErrorAction Stop -WarningAction Continue" in WORKFLOW
    assert "Get-MpThreat -ErrorAction Stop -WarningAction Continue" in WORKFLOW
    assert "Get-MpThreatDetection -ErrorAction Stop -WarningAction Continue" in WORKFLOW
    assert "exclusionPathCount" in WORKFLOW
    assert "Defender" in WORKFLOW


def test_bundle_hook_scans_components_and_candidate_before_execution() -> None:
    assert "[scriptblock]$BeforeCandidateExecution = $null" in BUNDLE
    assert "function Invoke-BeforeCandidateExecution" in BUNDLE
    assert "& $BeforeCandidateExecution -Path $Path -Stage $Stage" in BUNDLE
    assert BUNDLE.index("Invoke-BeforeCandidateExecution -Path $hostOutput") < BUNDLE.index(
        "Test-AppleMusicWebPipe.ps1"
    )
    candidate_hook = "Invoke-BeforeCandidateExecution -Path $candidate -Stage \"candidate-before-first-execution\""
    assert candidate_hook in BUNDLE
    assert BUNDLE.index(candidate_hook) < BUNDLE.index("Invoke-FullBundleCheck -LauncherPath")


def test_installer_hook_scans_bundle_before_canonical_checks() -> None:
    assert "[scriptblock]$BeforeCandidateExecution = $null" in INSTALLER
    assert "function Invoke-BeforeCandidateExecution" in INSTALLER
    bundle_hook = "Invoke-BeforeCandidateExecution -Path $bundle -Stage \"bundle-before-installer-checks\""
    assert bundle_hook in INSTALLER
    assert INSTALLER.index(bundle_hook) < INSTALLER.index("Start-Process -FilePath $launcher")
    assert INSTALLER.index(bundle_hook) < INSTALLER.index("& $startupSmokeTool -BundleRoot $bundle")


def test_nvda_output_and_sdk_follow_actual_repository_contracts() -> None:
    assert "GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName,$false) -cne 'Pyrsys B.V.'" in WORKFLOW
    assert "JRSoftware|Jordan Russell" not in WORKFLOW
    assert "actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9" in WORKFLOW
    assert "dotnet-version: 8.0.423" in WORKFLOW
    assert "Get-ChildItem -LiteralPath $env:GITHUB_WORKSPACE -Filter 'AMC-NVDA-*.nvda-addon'" in WORKFLOW
    assert '"Budowa AMC NVDA zakończyła się kodem $LASTEXITCODE"' not in WORKFLOW
    assert "final-defender-audit" in WORKFLOW
    for preference in ("$pref", "$preference"):
        for field in ("ExclusionPath", "ExclusionProcess", "ExclusionExtension"):
            assert f"@({preference}.{field}).Count" not in WORKFLOW
    assert WORKFLOW.index("final-defender-audit") < WORKFLOW.index("name: Zachowaj instalator")


def test_authorized_protection_setup_precedes_scan_and_build() -> None:
    setup = ROOT / 'wxlite/tools/Enable-AMCDiagnosticProtection.ps1'
    assert setup.exists()
    source = setup.read_text(encoding='utf-8')
    assert WORKFLOW.index('Enable-AMCDiagnosticProtection.ps1') < WORKFLOW.index('Defender source preflight')
    for token in ('RUNNER_ENVIRONMENT', 'github-hosted', 'RUNNER_OS', 'Windows',
                  'Set-MpPreference', 'Remove-MpPreference', 'Update-MpSignature',
                  'RealTimeProtectionEnabled', 'BehaviorMonitorEnabled', 'IoavProtectionEnabled',
                  'DisableRealtimeMonitoring=$false', 'DisableBehaviorMonitoring=$false',
                  'DisableIOAVProtection=$false', 'DisableScriptScanning=$false',
                  'DisableArchiveScanning=$false', 'protection-setup.json'):
        assert token in source
    assert source.index('github-hosted') < source.index('Set-MpPreference')
    assert 'DisableRealtimeMonitoring=$true' not in source
    assert 'Add-MpPreference' not in source
    assert "-WarningAction Stop" not in source


def test_workflow_is_not_an_intentional_unconditional_stop() -> None:
    forbidden = "throw 'Brak oczekiwanej bramki bezpieczeństwa w skrypcie kanonicznym; nie wolno kontynuować.'"
    assert forbidden not in WORKFLOW
    assert "Skompiluj jeden kontrolowany instalator diagnostyczny AMC Test" in WORKFLOW
