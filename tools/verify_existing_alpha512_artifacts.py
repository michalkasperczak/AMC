#!/usr/bin/env python3
"""Fail-closed reader for the two immutable AMC alpha.512 Actions artifacts.

This is intentionally narrower than the release publisher: it verifies and
safely extracts exactly the two installers and their integrity sidecars.  It
never builds, publishes, installs, or executes any downloaded file.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
import zipfile
from pathlib import Path, PurePosixPath
from typing import Any

SOURCE_REVISION = "d360cd89f622b90ae351f1ed876904b38856b625"
VERSION = "0.1.0-alpha.512"
BINARY_ZIP_BYTES = 347655437
BINARY_ZIP_SHA256 = "f56a2b11c650f890bfe39571f99257ead3934fe25c0ebf29080dc1d8928a6f71"
EVIDENCE_ZIP_BYTES = 6000
EVIDENCE_ZIP_SHA256 = "c438451b243408bdcad4e871545de24d158f3381449e5538ed52959c4aa34175"
PROFILES = {
    "public": {
        "installer": "AMC-Public-2026-11-22-1129.exe",
        "display_version": "22.11.2026, 11:29",
        "product_id": "org.accessible-media-controller.public",
        "install_id": "AMC.Public.win-x64",
        "channel": "stable",
        "apple_music": False,
    },
    "test": {
        "installer": "AMC-Test-2026-11-22-1137.exe",
        "display_version": "22.11.2026, 11:37",
        "product_id": "org.accessible-media-controller.test",
        "install_id": "AMC.Test.win-x64",
        "channel": "test",
        "apple_music": True,
    },
}
EXPECTED_EVIDENCE = {
    "source.json",
    "protection-setup.json",
    "defender-source-preflight.json",
    "closed-inputs.json",
    "inno-provenance.json",
    "amc-nvda-addon.json",
    "public-bundle.json",
    "public-installer.json",
    "public-installer-compile-evidence.json",
    "test-bundle.json",
    "test-installer.json",
    "test-installer-compile-evidence.json",
    "final-defender-audit.json",
}


class VerificationError(RuntimeError):
    pass


def sha256_path(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def require_hash_size(path: Path, expected_size: int, expected_sha256: str, label: str) -> None:
    if not path.is_file() or path.stat().st_size != expected_size:
        raise VerificationError(f"Nieprawidłowy rozmiar: {label}")
    if sha256_path(path) != expected_sha256:
        raise VerificationError(f"Nieprawidłowy SHA-256: {label}")


def safe_members(archive: zipfile.ZipFile) -> dict[str, zipfile.ZipInfo]:
    """Reject path traversal, case collisions and UNIX symlinks before reads."""
    members: dict[str, zipfile.ZipInfo] = {}
    seen_casefold: set[str] = set()
    for info in archive.infolist():
        name = info.filename
        normalized = name.replace("\\", "/")
        path = PurePosixPath(normalized)
        is_directory = name.endswith("/")
        mode = (info.external_attr >> 16) & 0o170000
        if (
            not name
            or name.startswith(("/", "\\"))
            or ":" in path.parts[0]
            or "\\" in name
            or any(part in ("", ".", "..") for part in path.parts)
            or mode == 0o120000
        ):
            raise VerificationError("Archiwum zawiera niebezpieczną ścieżkę")
        if is_directory:
            continue
        folded = normalized.casefold()
        if folded in seen_casefold:
            raise VerificationError("Archiwum zawiera zduplikowaną ścieżkę")
        seen_casefold.add(folded)
        members[normalized] = info
    return members


def json_member(archive: zipfile.ZipFile, member: str) -> dict[str, Any]:
    try:
        value = json.loads(archive.read(member).decode("utf-8-sig"))
    except (KeyError, UnicodeError, json.JSONDecodeError) as error:
        raise VerificationError(f"Nieprawidłowy JSON: {member}") from error
    if not isinstance(value, dict):
        raise VerificationError(f"JSON nie jest obiektem: {member}")
    return value


def ensure_clean_defender(payload: dict[str, Any], *, final: bool) -> None:
    if payload.get("exitCode") != 0:
        raise VerificationError("Brama Defendera nie zakończyła się powodzeniem")
    status = payload.get("status")
    preference = payload.get("preference")
    if not isinstance(status, dict) or not isinstance(preference, dict):
        raise VerificationError("Brakuje raportu Defendera")
    enabled = (
        "antivirusEnabled",
        "realTimeProtectionEnabled",
        "behaviorMonitorEnabled",
        "ioavProtectionEnabled",
        "onAccessProtectionEnabled",
    )
    if any(status.get(item) is not True for item in enabled):
        raise VerificationError("Defender nie był aktywny we wszystkich wymaganych trybach")
    if final and status.get("mode") != "Normal":
        raise VerificationError("Końcowy raport Defendera nie ma trybu Normal")
    if not final and any(
        preference.get(item) is not False
        for item in ("disableRealtimeMonitoring", "disableBehaviorMonitoring", "disableIOAVProtection")
    ):
        raise VerificationError("Raport Defendera dopuszcza wyłączenie ochrony")
    if any(preference.get(item) != 0 for item in ("exclusionPathCount", "exclusionProcessCount", "exclusionExtensionCount")):
        raise VerificationError("Raport Defendera zawiera wykluczenia")
    expected_detections = ("detections", "events1116_1117") if final else ("threats", "events1116_1117")
    if any(payload.get(item) != [] for item in expected_detections):
        raise VerificationError("Raport Defendera zawiera wykrycie lub zdarzenie")


def verify_evidence(archive: zipfile.ZipFile, members: dict[str, zipfile.ZipInfo]) -> None:
    if set(members) != EXPECTED_EVIDENCE:
        raise VerificationError("Archiwum dowodów nie ma dokładnie oczekiwanego zestawu plików")
    source = json_member(archive, "source.json")
    if source.get("sourceSha") != SOURCE_REVISION or source.get("sourceVersion") != VERSION or source.get("sourceStatus") != []:
        raise VerificationError("Dowód źródeł nie odpowiada zamrożonemu buildowi")
    protection = json_member(archive, "protection-setup.json")
    after = protection.get("after")
    if protection.get("stage") != "authorized-protection-setup" or protection.get("exitCode") != 0 or not isinstance(after, dict):
        raise VerificationError("Brakuje poprawnego dowodu włączenia ochrony")
    if any(after.get(item) is not True for item in ("antivirusEnabled", "realTimeProtectionEnabled", "behaviorMonitorEnabled", "ioavProtectionEnabled", "onAccessProtectionEnabled")):
        raise VerificationError("Po przygotowaniu runnera ochrona nie była aktywna")
    if any(after.get(item) is not False for item in ("disableRealtimeMonitoring", "disableBehaviorMonitoring", "disableIOAVProtection", "disableArchiveScanning", "disableScriptScanning")):
        raise VerificationError("Po przygotowaniu runnera pozostała wyłączona ochrona")
    if after.get("exclusionPaths") != [] or any(after.get(item) != 0 for item in ("exclusionProcessCount", "exclusionExtensionCount")):
        raise VerificationError("Po przygotowaniu runnera pozostały wykluczenia")
    ensure_clean_defender(json_member(archive, "defender-source-preflight.json"), final=False)
    ensure_clean_defender(json_member(archive, "final-defender-audit.json"), final=True)
    for item in ("closed-inputs.json", "inno-provenance.json", "amc-nvda-addon.json"):
        if json_member(archive, item).get("exitCode") != 0:
            raise VerificationError(f"Etap {item} nie zakończył się powodzeniem")
    for profile in PROFILES:
        for stage in ("bundle", "installer"):
            item = json_member(archive, f"{profile}-{stage}.json")
            if item.get("profile") != profile or item.get("stage") != stage or item.get("sourceSha") != SOURCE_REVISION or item.get("sourceVersion") != VERSION or item.get("exitCode") != 0:
                raise VerificationError(f"Dowód {profile}/{stage} jest niezgodny")
        compiler = json_member(archive, f"{profile}-installer-compile-evidence.json")
        argv = compiler.get("argumentList")
        if compiler.get("state") != "exited" or compiler.get("exitCode") != 0 or not isinstance(argv, list) or f"/DProfil={profile}" not in argv:
            raise VerificationError(f"Dowód argv kompilatora {profile} jest niezgodny")


def validate_sidecar(sidecar: dict[str, Any], profile: str, executable: bytes) -> None:
    contract = PROFILES[profile]
    expected_name = contract["installer"]
    required = {"schemaVersion", "profile", "version", "displayVersion", "fileName", "bytes", "sha256", "bundleId", "sourceRevision", "installerSourceRevision", "secretScan", "nvdaAddon", "amcNvdaAddon"}
    if not required.issubset(sidecar):
        raise VerificationError("Sidecar integralności nie ma bieżącego schematu")
    if sidecar.get("schemaVersion") != 1 or sidecar.get("profile") != profile:
        raise VerificationError("Sidecar ma niewłaściwy profil lub schemat")
    if sidecar.get("version") != VERSION or sidecar.get("displayVersion") != contract["display_version"] or sidecar.get("fileName") != expected_name:
        raise VerificationError("Sidecar ma niewłaściwą wersję lub nazwę")
    if isinstance(sidecar.get("bytes"), bool) or sidecar.get("bytes") != len(executable):
        raise VerificationError("Rozmiar instalatora nie zgadza się z sidecarem")
    digest = hashlib.sha256(executable).hexdigest()
    if sidecar.get("sha256") != digest:
        raise VerificationError("SHA-256 instalatora nie zgadza się z sidecarem")
    if sidecar.get("sourceRevision") != SOURCE_REVISION or sidecar.get("installerSourceRevision") != SOURCE_REVISION or sidecar.get("secretScan") != "passed" or not isinstance(sidecar.get("bundleId"), str) or not sidecar["bundleId"]:
        raise VerificationError("Sidecar nie potwierdza źródła lub bramki paczki")
    for addon_name in ("nvdaAddon", "amcNvdaAddon"):
        addon = sidecar.get(addon_name)
        if not isinstance(addon, dict) or not isinstance(addon.get("fileName"), str) or not addon["fileName"] or isinstance(addon.get("bytes"), bool) or not isinstance(addon.get("bytes"), int) or addon["bytes"] <= 0 or not isinstance(addon.get("sha256"), str) or len(addon["sha256"]) != 64:
            raise VerificationError(f"Sidecar ma niepełne metadane {addon_name}")


def copy_member(archive: zipfile.ZipFile, info: zipfile.ZipInfo, destination: Path) -> None:
    if not destination.parent.is_dir():
        raise VerificationError("Brak bezpiecznie utworzonego katalogu docelowego")
    with archive.open(info, "r") as source, destination.open("xb") as target:
        shutil.copyfileobj(source, target)
    if destination.stat().st_size != info.file_size:
        raise VerificationError("Bezpieczne rozpakowanie zmieniło rozmiar pliku")


def verify_and_extract(binary_zip: Path, evidence_zip: Path, destination: Path) -> dict[str, Any]:
    require_hash_size(binary_zip, BINARY_ZIP_BYTES, BINARY_ZIP_SHA256, "archiwum binarne")
    require_hash_size(evidence_zip, EVIDENCE_ZIP_BYTES, EVIDENCE_ZIP_SHA256, "archiwum dowodów")
    if destination.exists():
        raise VerificationError("Katalog docelowy weryfikacji musi nie istnieć")
    try:
        with zipfile.ZipFile(binary_zip) as binaries, zipfile.ZipFile(evidence_zip) as evidence:
            binary_members = safe_members(binaries)
            evidence_members = safe_members(evidence)
            expected_binary = {
                f"amc-{profile}-installer/{data['installer']}"
                for profile, data in PROFILES.items()
            } | {
                f"amc-{profile}-installer/{data['installer']}.integrity.json"
                for profile, data in PROFILES.items()
            }
            if set(binary_members) != expected_binary:
                raise VerificationError("Archiwum binarne nie ma dokładnie oczekiwanego układu czterech plików")
            verify_evidence(evidence, evidence_members)
            result: dict[str, Any] = {"sourceRevision": SOURCE_REVISION, "version": VERSION, "profiles": {}}
            for profile, data in PROFILES.items():
                name = data["installer"]
                member = f"amc-{profile}-installer/{name}"
                executable = binaries.read(member)
                sidecar = json_member(binaries, f"{member}.integrity.json")
                validate_sidecar(sidecar, profile, executable)
                target = destination / profile
                target.mkdir(parents=True, exist_ok=False)
                copy_member(binaries, binary_members[member], target / name)
                copy_member(binaries, binary_members[f"{member}.integrity.json"], target / f"{name}.integrity.json")
                result["profiles"][profile] = {
                    "installer": name,
                    "bytes": len(executable),
                    "sha256": hashlib.sha256(executable).hexdigest(),
                    "displayVersion": data["display_version"],
                    "productId": data["product_id"],
                    "installId": data["install_id"],
                    "updateChannel": data["channel"],
                    "appleMusicExpected": data["apple_music"],
                }
            return result
    except zipfile.BadZipFile as error:
        raise VerificationError("Archiwum Actions nie jest prawidłowym ZIP") from error


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary-zip", type=Path, required=True)
    parser.add_argument("--evidence-zip", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        report = verify_and_extract(args.binary_zip, args.evidence_zip, args.output_directory)
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    except (OSError, VerificationError) as error:
        print(f"FAIL-CLOSED: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
