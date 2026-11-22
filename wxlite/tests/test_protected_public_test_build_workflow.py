"""Statyczny kontrakt jednorazowej chronionej budowy AMC Public i Test."""
import re
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW_PATH = ROOT / ".github" / "workflows" / "amc-protected-public-test-build.yml"
GUARD_PATH = ROOT / "wxlite" / "tools" / "Enable-AMCDiagnosticProtection.ps1"
PROPS_PATH = ROOT / "Directory.Build.props"
AUTHORIZED_BRANCH = "codex/amc-protected-public-test-20261119-playback-r1"
AUTHORIZED_VERSION = "0.1.0-alpha.512"
CURRENT_VERSION = "0.1.0-alpha.514"


def _workflow() -> str:
    assert WORKFLOW_PATH.is_file(), "Brak workflow chronionej budowy Public/Test."
    return WORKFLOW_PATH.read_text(encoding="utf-8")


def _one_shot_contract(workflow: str, guard: str, props: str) -> dict[str, list[str]]:
    """Wyodrębnia ścisłe identyfikatory trzech bramek i obu źródeł wersji."""
    trigger = workflow.split("\npermissions:", 1)[0]
    return {
        "workflow_trigger": re.findall(r"(?m)^      - (codex/amc-protected-public-test-[^\r\n]+)$", trigger),
        "workflow_job": re.findall(r"github\.ref == 'refs/heads/([^']+)'", workflow),
        "powershell_guard": re.findall(r"\$env:GITHUB_REF -cne 'refs/heads/([^']+)'", guard),
        "props_version": re.findall(r"<Version>([^<]+)</Version>", props),
        "workflow_version": re.findall(r"\$sourceVersion -ne '([^']+)'", workflow),
    }


def _assert_authorized_one_shot_contract(workflow: str, guard: str, props: str) -> None:
    contract = _one_shot_contract(workflow, guard, props)
    assert contract["workflow_trigger"] == [AUTHORIZED_BRANCH]
    assert contract["workflow_job"] == [AUTHORIZED_BRANCH]
    assert contract["powershell_guard"] == [AUTHORIZED_BRANCH]
    assert contract["props_version"] == [CURRENT_VERSION]
    assert contract["workflow_version"] == [AUTHORIZED_VERSION]


def test_historical_one_shot_workflow_and_current_version_remain_explicit() -> None:
    guard = GUARD_PATH.read_text(encoding="utf-8-sig")
    props = PROPS_PATH.read_text(encoding="utf-8")
    _assert_authorized_one_shot_contract(_workflow(), guard, props)


@pytest.mark.parametrize(
    ("component", "expected", "replacement"),
    (
        ("trigger", AUTHORIZED_BRANCH, "codex/amc-protected-public-test-mismatched-trigger"),
        ("job", f"refs/heads/{AUTHORIZED_BRANCH}", "refs/heads/codex/amc-protected-public-test-mismatched-job"),
        ("guard", f"refs/heads/{AUTHORIZED_BRANCH}", "refs/heads/codex/amc-protected-public-test-mismatched-guard"),
        ("props", CURRENT_VERSION, "0.1.0-alpha.999"),
        ("workflow_version", AUTHORIZED_VERSION, "0.1.0-alpha.999"),
    ),
)
def test_one_shot_contract_rejects_each_ref_or_version_mismatch(component: str, expected: str, replacement: str) -> None:
    workflow = _workflow()
    guard = GUARD_PATH.read_text(encoding="utf-8-sig")
    props = PROPS_PATH.read_text(encoding="utf-8")
    if component in {"trigger", "job", "workflow_version"}:
        workflow = workflow.replace(expected, replacement, 1)
    elif component == "guard":
        guard = guard.replace(expected, replacement, 1)
    else:
        props = props.replace(expected, replacement, 1)
    with pytest.raises(AssertionError):
        _assert_authorized_one_shot_contract(workflow, guard, props)


def test_workflow_is_one_shot_read_only() -> None:
    workflow = _workflow()
    for required in (
        "github.run_attempt == '1'",
        "github.event.before == '0000000000000000000000000000000000000000'",
        "runs-on: windows-2025",
        "contents: read",
        "persist-credentials: false",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
    ):
        assert required in workflow
    for forbidden in (
        "workflow_dispatch",
        "self-hosted",
        "actions/cache",
        "Publish-AMCRelease",
        "gh release",
        "permissions: write",
        "strategy:",
    ):
        assert forbidden not in workflow


def test_protection_setup_preserves_host_and_runner_guards() -> None:
    guard = GUARD_PATH.read_text(encoding="utf-8-sig").split("$diagWarnings=@()", 1)[0]
    for required in ("$env:GITHUB_ACTIONS -cne 'true'", "$env:RUNNER_ENVIRONMENT -cne 'github-hosted'", "$env:RUNNER_OS -cne 'Windows'"):
        assert required in guard


def test_workflow_builds_public_then_test_from_one_source_revision() -> None:
    workflow = _workflow()
    public_bundle = "Build-AMCBundle.ps1 -Profile public"
    test_bundle = "Build-AMCBundle.ps1 -Profile test"
    public_installer = "Build-AMCInstaller.ps1 -Profile public"
    test_installer = "Build-AMCInstaller.ps1 -Profile test"
    for required in (
        "sourceSha = (git rev-parse HEAD).Trim()",
        "sourceVersion",
        "0.1.0-alpha.512",
        public_bundle,
        test_bundle,
        public_installer,
        test_installer,
        "-AppleMusicSitePackages $env:AMC_SITE_PACKAGES",
        "-Profile public",
        "-Profile test",
    ):
        assert required in workflow
    assert workflow.index(public_bundle) < workflow.index(test_bundle)
    assert workflow.index(public_installer) < workflow.index(test_installer)
    assert "-AppleMusicSitePackages $env:AMC_SITE_PACKAGES" in workflow[workflow.index(test_bundle):]
    assert "-AppleMusicSitePackages $env:AMC_SITE_PACKAGES" not in workflow[:workflow.index(test_bundle)]


def test_workflow_reuses_fail_closed_defender_gates_and_separate_profile_evidence() -> None:
    workflow = _workflow()
    for required in (
        "Enable-AMCDiagnosticProtection.ps1",
        "Start-MpScan -ScanType CustomScan",
        "-WarningAction Continue -WarningVariable +diagWarnings",
        "NoMatchingEventsFound",
        "$jobStarted",
        "Get-MpThreatDetection",
        "public-bundle.json",
        "public-installer.json",
        "test-bundle.json",
        "test-installer.json",
        "final-defender-audit.json",
        "if: success()",
        "if: always()",
    ):
        assert required in workflow
    assert workflow.count("Invoke-DefenderGate -Target") >= 10


def test_canonical_builders_scan_before_each_candidate_execution() -> None:
    bundle = (ROOT / "wxlite" / "tools" / "Build-AMCBundle.ps1").read_text(encoding="utf-8-sig")
    installer = (ROOT / "wxlite" / "tools" / "Build-AMCInstaller.ps1").read_text(encoding="utf-8-sig")
    for source in (bundle, installer):
        assert "[scriptblock]$BeforeCandidateExecution = $null" in source
        assert "function Invoke-BeforeCandidateExecution" in source
    assert bundle.index("components-before-apple-music-web-pipe") < bundle.index("Test-AppleMusicWebPipe.ps1")
    assert bundle.index("candidate-before-first-execution") < bundle.index("Invoke-FullBundleCheck -LauncherPath")
    assert installer.index("bundle-before-installer-checks") < installer.index("Start-Process -FilePath $launcher")


def test_final_audit_rejects_exclusions_and_retains_failed_compiler_evidence() -> None:
    audit = _workflow().split("- name: Odczytaj końcowe zdarzenia ochrony", 1)[1].split(
        "- name: Zachowaj oba instalatory", 1
    )[0]
    assert "if: always()" in audit
    for kind in ("Path", "Process", "Extension"):
        assert f"$report.preference.exclusion{kind}Count -gt 0" in audit
    finalizer = audit.split("finally {", 1)[1]
    assert "foreach ($profile in @('public','test'))" in finalizer
    assert "installer-compile-evidence.json" in finalizer
    assert "Test-Path -LiteralPath $compile -PathType Leaf" in finalizer
    assert '"$profile-installer-compile-evidence.json"' in finalizer
