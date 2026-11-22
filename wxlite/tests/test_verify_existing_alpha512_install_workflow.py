"""Statyczny kontrakt jednorazowego smoke instalacji istniejących AMC alpha.512."""
from __future__ import annotations

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github" / "workflows" / "amc-verify-existing-alpha512-install.yml"
VERIFIER = ROOT / "tools" / "verify_existing_alpha512_artifacts.py"
BRANCH = "codex/amc-verify-install-alpha512"
SOURCE_SHA = "d360cd89f622b90ae351f1ed876904b38856b625"


def _workflow() -> str:
    assert WORKFLOW.is_file(), "brak jednorazowego workflow smoke instalacji"
    return WORKFLOW.read_text(encoding="utf-8")


def test_one_shot_trigger_and_runtime_guard_share_exact_ref() -> None:
    workflow = _workflow()
    trigger = workflow.split("\npermissions:", 1)[0]
    assert re.findall(r"(?m)^      - (codex/amc-verify-install-alpha512)$", trigger) == [BRANCH]
    assert f"github.ref == 'refs/heads/{BRANCH}'" in workflow
    assert "$env:GITHUB_REF -cne 'refs/heads/codex/amc-verify-install-alpha512'" in workflow
    assert "github.event.before == '0000000000000000000000000000000000000000'" in workflow
    assert "github.run_attempt == '1'" in workflow


def test_workflow_is_hosted_read_only_and_uploads_reports_only() -> None:
    workflow = _workflow()
    for required in (
        "runs-on: windows-2025",
        "actions: read",
        "contents: read",
        "persist-credentials: false",
        "actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683",
        "actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02",
        "if: always()",
    ):
        assert required in workflow
    for forbidden in (
        "workflow_dispatch",
        "self-hosted",
        "actions/cache",
        "gh release",
        "Publish-AMCRelease",
        "permissions: write",
        "strategy:",
        "release create",
    ):
        assert forbidden not in workflow
    upload = workflow.split("actions/upload-artifact@", 1)[1]
    assert "amc-alpha512-install-evidence" in upload
    assert "*.exe" not in upload
    assert "*.zip" not in upload


def test_artifact_inputs_are_pinned_and_verified_before_safe_extraction() -> None:
    workflow = _workflow()
    for required in (
        "45016390370",
        "14752053921",
        "14752572396",
        "python tools/verify_existing_alpha512_artifacts.py",
        "Invoke-DefenderGate -Target $binaryZip",
        "Invoke-DefenderGate -Target $evidenceZip",
    ):
        assert required in workflow
    assert VERIFIER.is_file(), "brak wąskiego weryfikatora istniejących artefaktów"
    verifier = VERIFIER.read_text(encoding="utf-8")
    for required in (
        SOURCE_SHA,
        "f56a2b11c650f890bfe39571f99257ead3934fe25c0ebf29080dc1d8928a6f71",
        "347655437",
        "c438451b243408bdcad4e871545de24d158f3381449e5538ed52959c4aa34175",
        "6000",
        "AMC-Public-2026-11-22-1129.exe",
        "AMC-Test-2026-11-22-1137.exe",
        "ZipFile",
        "PurePosixPath",
        "sourceRevision",
        "installerSourceRevision",
        "secretScan",
        "AMC.Public.win-x64",
        "AMC.Test.win-x64",
        "org.accessible-media-controller.public",
        "org.accessible-media-controller.test",
    ):
        assert required in verifier
    assert "extractall" not in verifier


def test_smoke_uses_separate_clean_dirs_waits_and_checks_real_contracts() -> None:
    workflow = _workflow()
    for required in (
        "install-public",
        "install-test",
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "WaitForExit(600000)",
        "bundle-manifest.json",
        "VersionInfo.ProductVersion",
        "appleMusic",
        "AMC.exe",
        "AMC-Test.exe",
        "GITHUB_TOKEN",
        "ACTIONS_RUNTIME_TOKEN",
        "Invoke-DefenderGate -Target $smokeRoot",
    ):
        assert required in workflow
    assert "NVDA" not in workflow


def test_defender_enablement_scan_and_final_audit_fail_closed() -> None:
    workflow = _workflow()
    for required in (
        "Update-MpSignature",
        "Set-MpPreference",
        "Remove-MpPreference",
        "Get-MpComputerStatus",
        "Get-MpPreference",
        "Start-MpScan -ScanType CustomScan",
        "Get-MpThreatDetection",
        "Id = 1116,1117",
        "NoMatchingEventsFound",
        "final-defender-audit.json",
        "protection-setup.json",
    ):
        assert required in workflow


if __name__ == "__main__":
    import unittest

    checks = (
        test_one_shot_trigger_and_runtime_guard_share_exact_ref,
        test_workflow_is_hosted_read_only_and_uploads_reports_only,
        test_artifact_inputs_are_pinned_and_verified_before_safe_extraction,
        test_smoke_uses_separate_clean_dirs_waits_and_checks_real_contracts,
        test_defender_enablement_scan_and_final_audit_fail_closed,
    )
    result = unittest.TextTestRunner(verbosity=2).run(
        unittest.TestSuite(unittest.FunctionTestCase(check) for check in checks)
    )
    raise SystemExit(not result.wasSuccessful())
