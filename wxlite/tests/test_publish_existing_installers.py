"""Fixture-only checks for the narrow existing-installer publisher."""

from __future__ import annotations

import hashlib
import json
import os
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
    verify_updater_release,
    write_release_inputs,
)


def _write_json(archive: zipfile.ZipFile, name: str, payload: object) -> None:
    archive.writestr(name, json.dumps(payload, sort_keys=True))


def _contract_for_fixture(binary_zip: Path, evidence_zip: Path):
    return replace(
        CONTRACT,
        binary_artifact_sha256=hashlib.sha256(binary_zip.read_bytes()).hexdigest(),
        binary_artifact_bytes=binary_zip.stat().st_size,
        evidence_artifact_sha256=hashlib.sha256(evidence_zip.read_bytes()).hexdigest(),
        evidence_artifact_bytes=evidence_zip.stat().st_size,
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
        "public": (CONTRACT.profiles["public"].installer_name, b"fixture public installer"),
        "test": (CONTRACT.profiles["test"].installer_name, b"fixture test installer"),
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
    def test_updater_consumer_accepts_the_github_shape_for_both_channels(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            binary_zip, evidence_zip = _make_archives(root)
            verified = write_release_inputs(
                binary_zip, evidence_zip, root / "prepared", _contract_for_fixture(binary_zip, evidence_zip)
            )
            releases: list[dict[str, object]] = []
            for profile in ("public", "test"):
                item = verified[profile]
                manifest_path = root / "prepared" / profile / f"amc-update-{profile}.json"
                manifest_bytes = manifest_path.read_bytes()
                installer_path = root / "prepared" / profile / item.contract.installer_name
                releases.append({
                    "tag_name": item.contract.tag,
                    "draft": False,
                    "prerelease": item.contract.prerelease,
                    "assets": [
                        {
                            "name": installer_path.name,
                            "size": installer_path.stat().st_size,
                            "browser_download_url": (
                                "https://github.com/michalkasperczak/AMC/releases/download/"
                                f"{item.contract.tag}/{installer_path.name}"
                            ),
                        },
                        {
                            "name": manifest_path.name,
                            "size": len(manifest_bytes),
                            "digest": "sha256:" + hashlib.sha256(manifest_bytes).hexdigest(),
                            "browser_download_url": (
                                f"https://github.com/michalkasperczak/AMC/releases/download/"
                                f"{item.contract.tag}/{manifest_path.name}"
                            ),
                        },
                    ],
                })

            for profile in ("public", "test"):
                release = verify_updater_release(
                    releases,
                    root / "prepared" / profile / f"amc-update-{profile}.json",
                    profile,
                    CONTRACT.profiles[profile].tag,
                )
                self.assertEqual(release.tag, CONTRACT.profiles[profile].tag)
                self.assertEqual(release.profile_id, profile)

    def test_updater_consumer_rejects_manifest_asset_with_swapped_size(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            binary_zip, evidence_zip = _make_archives(root)
            verified = write_release_inputs(
                binary_zip, evidence_zip, root / "prepared", _contract_for_fixture(binary_zip, evidence_zip)
            )
            item = verified["public"]
            manifest_path = root / "prepared/public/amc-update-public.json"
            installer_path = root / "prepared/public" / item.contract.installer_name
            manifest_bytes = manifest_path.read_bytes()
            releases = [{
                "tag_name": item.contract.tag,
                "draft": False,
                "prerelease": False,
                "assets": [
                    {
                        "name": installer_path.name,
                        "size": installer_path.stat().st_size,
                        "browser_download_url": (
                            f"https://github.com/michalkasperczak/AMC/releases/download/"
                            f"{item.contract.tag}/{installer_path.name}"
                        ),
                    },
                    {
                        "name": manifest_path.name,
                        "size": len(manifest_bytes) + 1,
                        "digest": "sha256:" + hashlib.sha256(manifest_bytes).hexdigest(),
                        "browser_download_url": (
                            f"https://github.com/michalkasperczak/AMC/releases/download/"
                            f"{item.contract.tag}/{manifest_path.name}"
                        ),
                    },
                ],
            }]
            with self.assertRaisesRegex(VerificationError, "rozmiar manifestu"):
                verify_updater_release(releases, manifest_path, "public", item.contract.tag)

    def test_alpha512_contract_is_pinned_to_authorized_inputs_and_wrapper_names(self) -> None:
        self.assertEqual(CONTRACT.source_revision, "d360cd89f622b90ae351f1ed876904b38856b625")
        self.assertEqual(CONTRACT.version, "0.1.0-alpha.512")
        self.assertEqual(CONTRACT.binary_artifact_sha256, "f56a2b11c650f890bfe39571f99257ead3934fe25c0ebf29080dc1d8928a6f71")
        self.assertEqual(CONTRACT.evidence_artifact_sha256, "c438451b243408bdcad4e871545de24d158f3381449e5538ed52959c4aa34175")
        self.assertEqual(CONTRACT.profiles["public"].display_version, "22.11.2026, 11:29")
        self.assertEqual(CONTRACT.profiles["public"].tag, "public-2026.11.22.1129")
        self.assertEqual(CONTRACT.profiles["public"].installer_name, "AMC-Public-2026-11-22-1129.exe")
        self.assertEqual(CONTRACT.profiles["test"].display_version, "22.11.2026, 11:37")
        self.assertEqual(CONTRACT.profiles["test"].tag, "test-2026.11.22.1137")
        self.assertEqual(CONTRACT.profiles["test"].installer_name, "AMC-Test-2026-11-22-1137.exe")

    def test_release_notes_preserve_alpha512_scope_and_unperformed_acceptance(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            binary_zip, evidence_zip = _make_archives(Path(temporary))
            notes = verify_archives(binary_zip, evidence_zip, _contract_for_fixture(binary_zip, evidence_zip))

        public_notes = notes["public"].notes
        test_notes = notes["test"].notes
        for expected in (
            "Radio Archiwista",
            "pełny opis jest kopiowany do schowka",
            "Skrzynka: po powrocie do listy",
            "stabilnej tożsamości",
            "nie jest to pamięć pozycji odtwarzania",
            "Ctrl+L otwiera Bibliotekę, a Ctrl+I wraca do Skrzynki",
            "dokładnego URI WiiM",
            "tożsamości własnego strumienia Sonos wyliczonej z jego ID i URL",
            "Nie usuwa ani nie zmienia wcześniej zapisanych oznaczeń w innych sesjach",
            "Ulubione, playlisty, Frontier ani Apple",
            "tylko odebrane odświeżanie oraz rozdziały YouTube",
            "nie potwierdza szerszego zakresu",
            "Delete kolejki i odzyskiwanie audio nie są włączone",
            "Oba gotowe instalatory przeszły próbę instalacji na Windowsie w GitHub Actions",
            "Nie przeprowadzono odbioru z żywym NVDA",
            "Defender nie odnotował wykryć ani zdarzeń 1116/1117",
        ):
            self.assertIn(expected, public_notes)
        self.assertIn("AMC Public nie zawiera Apple Music", public_notes)
        self.assertIn("Biblioteka, wyszukiwanie i dodawanie bez odtwarzania ani naprawy logowania", test_notes)

    @unittest.skipUnless(os.environ.get("AMC_REAL_EVIDENCE_ZIP"), "requires AMC_REAL_EVIDENCE_ZIP")
    def test_real_alpha512_evidence_archive_and_tampering_fail_closed(self) -> None:
        evidence_zip = Path(os.environ["AMC_REAL_EVIDENCE_ZIP"])
        self.assertTrue(evidence_zip.is_file())
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            binary_zip, _ = _make_archives(root)
            self.assertEqual(evidence_zip.stat().st_size, CONTRACT.evidence_artifact_bytes)
            self.assertEqual(hashlib.sha256(evidence_zip.read_bytes()).hexdigest(), CONTRACT.evidence_artifact_sha256)
            contract = replace(
                CONTRACT,
                binary_artifact_sha256=hashlib.sha256(binary_zip.read_bytes()).hexdigest(),
                binary_artifact_bytes=binary_zip.stat().st_size,
            )
            result = verify_archives(binary_zip, evidence_zip, contract)
            self.assertEqual(set(result), {"public", "test"})

            tampered = root / "tampered-evidence.zip"
            with zipfile.ZipFile(evidence_zip) as source, zipfile.ZipFile(tampered, "w") as target:
                for info in source.infolist():
                    payload = source.read(info.filename)
                    if info.filename == "source.json":
                        value = json.loads(payload)
                        value["sourceVersion"] = "0.1.0-alpha.511"
                        payload = json.dumps(value).encode("utf-8")
                    target.writestr(info, payload)
            tampered_contract = replace(
                contract,
                evidence_artifact_sha256=hashlib.sha256(tampered.read_bytes()).hexdigest(),
                evidence_artifact_bytes=tampered.stat().st_size,
            )
            with self.assertRaisesRegex(VerificationError, "Dowód źródeł"):
                verify_archives(binary_zip, tampered, tampered_contract)

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
        self.assertIn("bez odtwarzania", result["test"].notes.lower())
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
                "id": 6001,
                "tag_name": contract.profiles["public"].tag,
                "target_commitish": CONTRACT.source_revision,
                "draft": True,
                "prerelease": False,
                "assets": assets,
            }
            verify_release_readback(release, verified["public"], downloaded, draft=True, expected_release_id=6001)
            assets[-1]["digest"] = "sha256:" + "0" * 64
            with self.assertRaisesRegex(VerificationError, "Metadane assetu"):
                verify_release_readback(release, verified["public"], downloaded, draft=True, expected_release_id=6001)

    def test_release_readback_requires_the_created_release_id(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            temporary_path = Path(temporary)
            binary_zip, evidence_zip = _make_archives(temporary_path)
            contract = _contract_for_fixture(binary_zip, evidence_zip)
            verified = write_release_inputs(binary_zip, evidence_zip, temporary_path / "prepared", contract)
            profile_dir = temporary_path / "prepared" / "public"
            installer = profile_dir / contract.profiles["public"].installer_name
            downloaded = profile_dir / f"{installer.name}.downloaded"
            downloaded.write_bytes(installer.read_bytes())
            assets = [
                {
                    "name": asset_path.name,
                    "state": "uploaded",
                    "size": asset_path.stat().st_size,
                    "digest": "sha256:" + hashlib.sha256(asset_path.read_bytes()).hexdigest(),
                }
                for asset_path in (
                    installer,
                    profile_dir / f"{installer.name}.integrity.json",
                    profile_dir / "amc-update-public.json",
                )
            ]
            release = {
                "id": 7001,
                "tag_name": contract.profiles["public"].tag,
                "target_commitish": CONTRACT.source_revision,
                "draft": True,
                "prerelease": False,
                "assets": assets,
            }
            verify_release_readback(release, verified["public"], downloaded, draft=True, expected_release_id=7001)
            release["id"] = 7002
            with self.assertRaisesRegex(VerificationError, "ID"):
                verify_release_readback(release, verified["public"], downloaded, draft=True, expected_release_id=7001)

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
        self.assertIn("verify-updater-release", workflow)
        self.assertIn("--manifest prepared/public/amc-update-public.json", workflow)
        self.assertIn("--manifest prepared/test/amc-update-test.json", workflow)
        self.assertNotIn("import json, re", workflow)

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
