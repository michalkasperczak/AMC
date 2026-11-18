"""Fixture-only checks for the narrow existing-installer publisher."""

from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
import zipfile
from dataclasses import replace
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from tools.publish_existing_installers import (  # noqa: E402
    CONTRACT,
    VerificationError,
    verify_archives,
    verify_release_readback,
    write_release_inputs,
)


def _write_json(archive: zipfile.ZipFile, name: str, payload: object) -> None:
    archive.writestr(name, json.dumps(payload, sort_keys=True))


def _contract_for_fixture(binary_zip: Path, evidence_zip: Path):
    public = CONTRACT.profiles["public"]
    test = CONTRACT.profiles["test"]
    return replace(
        CONTRACT,
        binary_artifact_sha256=hashlib.sha256(binary_zip.read_bytes()).hexdigest(),
        binary_artifact_bytes=binary_zip.stat().st_size,
        evidence_artifact_sha256=hashlib.sha256(evidence_zip.read_bytes()).hexdigest(),
        evidence_artifact_bytes=evidence_zip.stat().st_size,
        profiles={
            "public": replace(public, installer_name="AMC-Public-2026-11-18-0329.exe"),
            "test": replace(test, installer_name="AMC-Test-2026-11-18-0337.exe"),
        },
    )


def _evidence(profile: str, stage: str, *, argv: list[str] | None = None) -> dict[str, object]:
    result: dict[str, object] = {
        "profile": profile,
        "stage": stage,
        "sourceSha": CONTRACT.source_revision,
        "sourceVersion": CONTRACT.version,
        "exitCode": 0,
    }
    if argv is not None:
        result.update({"state": "exited", "argumentList": argv, "exitCode": 0})
    return result


def _make_archives(tmp_path: Path) -> tuple[Path, Path]:
    binary_zip = tmp_path / "binary.zip"
    evidence_zip = tmp_path / "evidence.zip"
    installers = {
        "public": ("AMC-Public-2026-11-18-0329.exe", b"fixture public installer"),
        "test": ("AMC-Test-2026-11-18-0337.exe", b"fixture test installer"),
    }
    with zipfile.ZipFile(binary_zip, "w") as archive:
        for profile, (name, content) in installers.items():
            digest = hashlib.sha256(content).hexdigest()
            sidecar = {
                "schemaVersion": 1,
                "profile": profile,
                "version": CONTRACT.version,
                "displayVersion": CONTRACT.profiles[profile].display_version,
                "fileName": name,
                "bytes": len(content),
                "sha256": digest,
                "bundleId": f"fixture-{profile}",
                "sourceRevision": CONTRACT.source_revision,
                "installerSourceRevision": CONTRACT.source_revision,
                "secretScan": "passed",
                "nvdaAddon": {"fileName": "quick.nvda-addon", "bytes": 1, "sha256": "a" * 64},
                "amcNvdaAddon": {"fileName": "AMC-NVDA-0.4.2.nvda-addon", "bytes": 1, "sha256": "b" * 64},
            }
            root = f"amc-{profile}-installer"
            archive.writestr(f"{root}/{name}", content)
            _write_json(archive, f"{root}/{name}.integrity.json", sidecar)

    with zipfile.ZipFile(evidence_zip, "w") as archive:
        _write_json(
            archive,
            "source.json",
            {
                "sourceSha": CONTRACT.source_revision,
                "sourceVersion": CONTRACT.version,
                "sourceStatus": [],
            },
        )
        _write_json(
            archive,
            "defender-source-preflight.json",
            {
                "exitCode": 0,
                "status": {
                    "antivirusEnabled": True,
                    "realTimeProtectionEnabled": True,
                    "behaviorMonitorEnabled": True,
                    "ioavProtectionEnabled": True,
                    "onAccessProtectionEnabled": True,
                },
                "preference": {
                    "disableRealtimeMonitoring": False,
                    "disableBehaviorMonitoring": False,
                    "disableIOAVProtection": False,
                    "exclusionPathCount": 0,
                    "exclusionProcessCount": 0,
                    "exclusionExtensionCount": 0,
                },
                "threats": [],
                "events1116_1117": [],
            },
        )
        _write_json(archive, "protection-setup.json", {
            "stage": "authorized-protection-setup",
            "exitCode": 0,
            "after": {
                "antivirusEnabled": True,
                "realTimeProtectionEnabled": True,
                "behaviorMonitorEnabled": True,
                "ioavProtectionEnabled": True,
                "onAccessProtectionEnabled": True,
                "disableRealtimeMonitoring": False,
                "disableBehaviorMonitoring": False,
                "disableIOAVProtection": False,
                "disableArchiveScanning": False,
                "disableScriptScanning": False,
                "exclusionPaths": [],
                "exclusionProcessCount": 0,
                "exclusionExtensionCount": 0,
            },
        })
        _write_json(archive, "closed-inputs.json", {"exitCode": 0})
        _write_json(archive, "inno-provenance.json", {"exitCode": 0})
        _write_json(archive, "amc-nvda-addon.json", {"exitCode": 0})
        for profile in ("public", "test"):
            _write_json(archive, f"{profile}-bundle.json", _evidence(profile, "bundle"))
            _write_json(archive, f"{profile}-installer.json", _evidence(profile, "installer"))
            _write_json(
                archive,
                f"{profile}-installer-compile-evidence.json",
                _evidence(profile, "compile", argv=[f"/DProfil={profile}", "installer/AMC_Wx_Setup.iss"]),
            )
        _write_json(
            archive,
            "final-defender-audit.json",
            {
                "exitCode": 0,
                "status": {
                    "antivirusEnabled": True,
                    "realTimeProtectionEnabled": True,
                    "behaviorMonitorEnabled": True,
                    "ioavProtectionEnabled": True,
                    "onAccessProtectionEnabled": True,
                    "mode": "Normal",
                },
                "preference": {
                    "exclusionPathCount": 0,
                    "exclusionProcessCount": 0,
                    "exclusionExtensionCount": 0,
                },
                "detections": [],
                "events1116_1117": [],
            },
        )
    return binary_zip, evidence_zip


class PublishExistingInstallerTests(unittest.TestCase):
    def test_fixture_archives_create_profiled_release_inputs(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            binary_zip, evidence_zip = _make_archives(Path(temporary))
            result = verify_archives(binary_zip, evidence_zip, _contract_for_fixture(binary_zip, evidence_zip))

        self.assertEqual(result["public"].installer_bytes, len(b"fixture public installer"))
        self.assertEqual(
            result["test"].installer_sha256,
            hashlib.sha256(b"fixture test installer").hexdigest(),
        )
        self.assertIs(result["public"].update_manifest["prerelease"], False)
        self.assertIs(result["test"].update_manifest["prerelease"], True)
        self.assertEqual(result["public"].update_manifest["sourceRevision"], CONTRACT.source_revision)
        self.assertIn("brak odtwarzania", result["test"].notes.lower())
        self.assertIn("RCX", result["public"].notes)

    def test_fixture_archives_reject_extra_binary_payload(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            binary_zip, evidence_zip = _make_archives(Path(temporary))
            with zipfile.ZipFile(binary_zip, "a") as archive:
                archive.writestr("amc-public-installer/unexpected.dll", b"not allowed")

            with self.assertRaisesRegex(VerificationError, "dokładnie"):
                verify_archives(binary_zip, evidence_zip, _contract_for_fixture(binary_zip, evidence_zip))

    def test_release_readback_requires_all_uploaded_digests(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            binary_zip, evidence_zip = _make_archives(temporary_path)
            contract = _contract_for_fixture(binary_zip, evidence_zip)
            verified = write_release_inputs(binary_zip, evidence_zip, temporary_path / "prepared", contract)
            profile_dir = temporary_path / "prepared" / "public"
            installer = profile_dir / contract.profiles["public"].installer_name
            downloaded = profile_dir / f"{installer.name}.downloaded"
            downloaded.write_bytes(installer.read_bytes())
            assets = []
            for asset_path in (
                installer,
                profile_dir / f"{installer.name}.integrity.json",
                profile_dir / "amc-update-public.json",
            ):
                assets.append({
                    "name": asset_path.name,
                    "state": "uploaded",
                    "size": asset_path.stat().st_size,
                    "digest": "sha256:" + hashlib.sha256(asset_path.read_bytes()).hexdigest(),
                })
            release = {
                "tag_name": contract.profiles["public"].tag,
                "target_commitish": CONTRACT.source_revision,
                "draft": True,
                "prerelease": False,
                "assets": assets,
            }
            verify_release_readback(release, verified["public"], downloaded, draft=True)
            assets[-1]["digest"] = "sha256:" + "0" * 64
            with self.assertRaisesRegex(VerificationError, "Metadane assetu"):
                verify_release_readback(release, verified["public"], downloaded, draft=True)

    def test_github_request_field_types(self) -> None:
        # Exercise the real inline payload producer, not a copied JSON fixture.
        import contextlib
        import io
        import unittest.mock
        workflow = (Path(__file__).resolve().parents[2] / ".github/workflows/amc-publish-existing-installers.yml").read_text()
        marker = "          import json, sys\n          tag, profile, prerelease, latest, notes = sys.argv[1:]"
        code = workflow[workflow.index(marker):].split("\n          PY", 1)[0]
        import textwrap
        with tempfile.TemporaryDirectory() as temporary:
            notes = Path(temporary) / "notes.md"
            notes.write_text("fixture notes", encoding="utf-8")
            stdout = io.StringIO()
            with unittest.mock.patch.object(sys, "argv", ["-", CONTRACT.profiles["public"].tag, "public", "false", "true", str(notes)]), contextlib.redirect_stdout(stdout):
                exec(compile(textwrap.dedent(code), "workflow-create-payload", "exec"), {})
            payload = json.loads(stdout.getvalue())
        self.assertIs(payload["draft"], True)
        self.assertIs(payload["prerelease"], False)
        self.assertEqual(payload["make_latest"], "true")
        self.assertIsInstance(payload["make_latest"], str)
        self.assertEqual(payload["target_commitish"], CONTRACT.source_revision)
        self.assertIn("-F draft=false", workflow)
        self.assertIn('-f make_latest="$latest"', workflow)
        self.assertNotIn("--hostname uploads.github.com", workflow)

    def test_protection_setup_rejects_disabled_after_state(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            binary_zip, evidence_zip = _make_archives(root)
            replacement = root / "disabled-protection.zip"
            with zipfile.ZipFile(evidence_zip) as source, zipfile.ZipFile(replacement, "w") as target:
                for info in source.infolist():
                    payload = source.read(info.filename)
                    if info.filename == "protection-setup.json":
                        value = json.loads(payload)
                        value["after"]["realTimeProtectionEnabled"] = False
                        payload = json.dumps(value).encode()
                    target.writestr(info, payload)
            with self.assertRaisesRegex(VerificationError, "ochrona nie była aktywna"):
                verify_archives(binary_zip, replacement, _contract_for_fixture(binary_zip, replacement))

    def test_fixture_archives_reject_wrong_profile_compiler_argv(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            binary_zip, evidence_zip = _make_archives(temporary_path)
            replacement = temporary_path / "bad-evidence.zip"
            with zipfile.ZipFile(evidence_zip) as source, zipfile.ZipFile(replacement, "w") as target:
                for info in source.infolist():
                    payload = source.read(info.filename)
                    if info.filename == "test-installer-compile-evidence.json":
                        value = json.loads(payload)
                        value["argumentList"] = ["/DProfil=public", "installer/AMC_Wx_Setup.iss"]
                        payload = json.dumps(value).encode()
                    target.writestr(info, payload)

            with self.assertRaisesRegex(VerificationError, "argv"):
                verify_archives(binary_zip, replacement, _contract_for_fixture(binary_zip, replacement))


if __name__ == "__main__":
    unittest.main()
