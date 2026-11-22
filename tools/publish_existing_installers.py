#!/usr/bin/env python3
"""Fail-closed publisher for exactly two existing AMC Actions artifacts.

It only reads ZIPs and writes release inputs.  It never builds, installs, or
executes an installer.  The GitHub workflow uses this module on an ephemeral
Linux runner and is deliberately tied to one source revision and two artifact
IDs.
"""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import importlib
import json
import shutil
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Any, Mapping, NoReturn

REPOSITORY = "michalkasperczak/AMC"
SOURCE_REVISION = "d360cd89f622b90ae351f1ed876904b38856b625"
VERSION = "0.1.0-alpha.512"
RUN_ID = 45016390370
RUN_ATTEMPT = 1
BINARY_ARTIFACT_ID = 14752053921
BINARY_ARTIFACT_NAME = "amc-protected-public-test-installers-d360cd89f622b90ae351f1ed876904b38856b625"
BINARY_ARTIFACT_BYTES = 347655437
BINARY_ARTIFACT_SHA256 = "f56a2b11c650f890bfe39571f99257ead3934fe25c0ebf29080dc1d8928a6f71"
EVIDENCE_ARTIFACT_ID = 14752572396
EVIDENCE_ARTIFACT_NAME = "amc-protected-public-test-evidence-d360cd89f622b90ae351f1ed876904b38856b625"
EVIDENCE_ARTIFACT_BYTES = 6000
EVIDENCE_ARTIFACT_SHA256 = "c438451b243408bdcad4e871545de24d158f3381449e5538ed52959c4aa34175"


class VerificationError(RuntimeError):
    """An immutable input or release contract does not match."""


@dataclass(frozen=True)
class ProfileContract:
    profile: str
    display_name: str
    display_version: str
    tag: str
    prerelease: bool
    update_channel: str
    product_id: str
    install_id: str
    installer_name: str


@dataclass(frozen=True)
class VerificationContract:
    source_revision: str
    version: str
    binary_artifact_sha256: str
    binary_artifact_bytes: int
    evidence_artifact_sha256: str
    evidence_artifact_bytes: int
    profiles: Mapping[str, ProfileContract]


@dataclass(frozen=True)
class VerifiedProfile:
    contract: ProfileContract
    installer_bytes: int
    installer_sha256: str
    integrity: Mapping[str, Any]
    update_manifest: Mapping[str, Any]
    notes: str


CONTRACT = VerificationContract(
    source_revision=SOURCE_REVISION,
    version=VERSION,
    binary_artifact_sha256=BINARY_ARTIFACT_SHA256,
    binary_artifact_bytes=BINARY_ARTIFACT_BYTES,
    evidence_artifact_sha256=EVIDENCE_ARTIFACT_SHA256,
    evidence_artifact_bytes=EVIDENCE_ARTIFACT_BYTES,
    profiles={
        "public": ProfileContract(
            profile="public",
            display_name="AMC Public",
            display_version="22.11.2026, 11:29",
            tag="public-2026.11.22.1129",
            prerelease=False,
            update_channel="stable",
            product_id="org.accessible-media-controller.public",
            install_id="AMC.Public.win-x64",
            installer_name="AMC-Public-2026-11-22-1129.exe",
        ),
        "test": ProfileContract(
            profile="test",
            display_name="AMC Test",
            display_version="22.11.2026, 11:37",
            tag="test-2026.11.22.1137",
            prerelease=True,
            update_channel="test",
            product_id="org.accessible-media-controller.test",
            install_id="AMC.Test.win-x64",
            installer_name="AMC-Test-2026-11-22-1137.exe",
        ),
    },
)


def _fail(message: str) -> NoReturn:
    raise VerificationError(message)


def _sha256_path(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _safe_members(archive: zipfile.ZipFile) -> dict[str, zipfile.ZipInfo]:
    members: dict[str, zipfile.ZipInfo] = {}
    for info in archive.infolist():
        name = info.filename.replace("\\", "/")
        path = PurePosixPath(name)
        if (
            not name
            or name.startswith("/")
            or "\\" in info.filename
            or any(part in ("", ".", "..") for part in path.parts)
        ):
            _fail("Archiwum zawiera niebezpieczną ścieżkę")
        if name.endswith("/"):
            continue
        if name.casefold() in {member.casefold() for member in members}:
            _fail("Archiwum zawiera zduplikowaną ścieżkę")
        members[name] = info
    return members


def _read_json(archive: zipfile.ZipFile, member: str) -> dict[str, Any]:
    try:
        value = json.loads(archive.read(member).decode("utf-8-sig"))
    except (KeyError, UnicodeError, json.JSONDecodeError) as error:
        raise VerificationError(f"Nieprawidłowy JSON: {member}") from error
    if not isinstance(value, dict):
        _fail(f"JSON nie jest obiektem: {member}")
    return value


def _nonempty_text(payload: Mapping[str, Any], key: str) -> str:
    value = payload.get(key)
    if not isinstance(value, str) or not value.strip():
        _fail(f"Brak prawidłowego pola {key}")
    return value.strip()


def _sha256(value: object, context: str) -> str:
    if not isinstance(value, str) or len(value) != 64 or any(ch not in "0123456789abcdef" for ch in value.lower()):
        _fail(f"Nieprawidłowy SHA-256: {context}")
    return value.lower()


def _assert_hash_and_size(path: Path, expected_size: int, expected_hash: str, label: str) -> None:
    if path.stat().st_size != expected_size:
        _fail(f"Niewłaściwy rozmiar {label}")
    if _sha256_path(path) != expected_hash:
        _fail(f"Niewłaściwy SHA-256 {label}")


def _validate_sidecar(payload: Mapping[str, Any], profile: ProfileContract, executable: bytes, contract: VerificationContract) -> None:
    required = {
        "schemaVersion",
        "profile",
        "version",
        "displayVersion",
        "fileName",
        "bytes",
        "sha256",
        "bundleId",
        "sourceRevision",
        "installerSourceRevision",
        "secretScan",
        "nvdaAddon",
        "amcNvdaAddon",
    }
    if not required.issubset(payload):
        _fail("Sidecar integralności nie ma bieżącego schematu")
    if payload["schemaVersion"] != 1 or payload["profile"] != profile.profile:
        _fail("Sidecar ma niewłaściwy profil lub schemat")
    if payload["version"] != contract.version or payload["displayVersion"] != profile.display_version:
        _fail("Sidecar ma niewłaściwą wersję")
    if payload["fileName"] != profile.installer_name:
        _fail("Sidecar wskazuje inną nazwę instalatora")
    if isinstance(payload["bytes"], bool) or payload["bytes"] != len(executable):
        _fail("Rozmiar instalatora nie zgadza się z sidecarem")
    if _sha256(payload["sha256"], "sidecar") != hashlib.sha256(executable).hexdigest():
        _fail("SHA-256 instalatora nie zgadza się z sidecarem")
    if payload["sourceRevision"] != contract.source_revision or payload["installerSourceRevision"] != contract.source_revision:
        _fail("Sidecar nie wskazuje zamrożonego commitu źródeł")
    if payload["secretScan"] != "passed" or not _nonempty_text(payload, "bundleId"):
        _fail("Sidecar nie potwierdza kompletnej bramki paczki")
    for name in ("nvdaAddon", "amcNvdaAddon"):
        addon = payload[name]
        if not isinstance(addon, dict):
            _fail(f"Sidecar nie ma metadanych {name}")
        for key in ("fileName", "bytes", "sha256"):
            if key not in addon:
                _fail(f"Sidecar ma niepełne metadane {name}")
        if not isinstance(addon["fileName"], str) or not addon["fileName"]:
            _fail(f"Sidecar ma nieprawidłową nazwę {name}")
        if isinstance(addon["bytes"], bool) or not isinstance(addon["bytes"], int) or addon["bytes"] <= 0:
            _fail(f"Sidecar ma nieprawidłowy rozmiar {name}")
        _sha256(addon["sha256"], name)


def _validate_defender(payload: Mapping[str, Any], *, final: bool) -> None:
    if payload.get("exitCode") != 0:
        _fail("Brama Defendera nie zakończyła się powodzeniem")
    status = payload.get("status")
    preference = payload.get("preference")
    if not isinstance(status, dict) or not isinstance(preference, dict):
        _fail("Brakuje raportu Defendera")
    enabled = ("antivirusEnabled", "realTimeProtectionEnabled", "behaviorMonitorEnabled", "ioavProtectionEnabled", "onAccessProtectionEnabled")
    if any(status.get(name) is not True for name in enabled):
        _fail("Defender nie był aktywny we wszystkich wymaganych trybach")
    if final and status.get("mode") != "Normal":
        _fail("Końcowy raport Defendera nie ma trybu Normal")
    disabled = ("disableRealtimeMonitoring", "disableBehaviorMonitoring", "disableIOAVProtection")
    if not final and any(preference.get(name) is not False for name in disabled):
        _fail("Raport Defendera dopuszcza wyłączenie ochrony")
    for name in ("exclusionPathCount", "exclusionProcessCount", "exclusionExtensionCount"):
        if preference.get(name) != 0:
            _fail("Raport Defendera zawiera wykluczenia")
    for name in (("detections", "events1116_1117") if final else ("threats", "events1116_1117")):
        if payload.get(name) != []:
            _fail("Raport Defendera zawiera wykrycie lub zdarzenie")


def _validate_evidence(archive: zipfile.ZipFile, members: Mapping[str, zipfile.ZipInfo], contract: VerificationContract) -> None:
    expected = {
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
    if set(members) != expected:
        _fail("Archiwum dowodów nie ma dokładnie oczekiwanego zestawu plików")
    source = _read_json(archive, "source.json")
    if source.get("sourceSha") != contract.source_revision or source.get("sourceVersion") != contract.version or source.get("sourceStatus") != []:
        _fail("Dowód źródeł nie odpowiada zamrożonemu buildowi")
    protection = _read_json(archive, "protection-setup.json")
    after = protection.get("after")
    if protection.get("stage") != "authorized-protection-setup" or protection.get("exitCode") != 0 or not isinstance(after, dict):
        _fail("Brakuje poprawnego dowodu włączenia ochrony")
    if any(after.get(name) is not True for name in ("antivirusEnabled", "realTimeProtectionEnabled", "behaviorMonitorEnabled", "ioavProtectionEnabled", "onAccessProtectionEnabled")):
        _fail("Po przygotowaniu runnera ochrona nie była aktywna")
    if any(after.get(name) is not False for name in ("disableRealtimeMonitoring", "disableBehaviorMonitoring", "disableIOAVProtection", "disableArchiveScanning", "disableScriptScanning")):
        _fail("Po przygotowaniu runnera pozostała wyłączona ochrona")
    if after.get("exclusionPaths") != [] or any(after.get(name) != 0 for name in ("exclusionProcessCount", "exclusionExtensionCount")):
        _fail("Po przygotowaniu runnera pozostały wykluczenia")
    _validate_defender(_read_json(archive, "defender-source-preflight.json"), final=False)
    _validate_defender(_read_json(archive, "final-defender-audit.json"), final=True)
    for name in ("closed-inputs.json", "inno-provenance.json", "amc-nvda-addon.json"):
        if _read_json(archive, name).get("exitCode") != 0:
            _fail(f"Etap {name} nie zakończył się powodzeniem")
    for profile in contract.profiles:
        for stage in ("bundle", "installer"):
            payload = _read_json(archive, f"{profile}-{stage}.json")
            if payload.get("profile") != profile or payload.get("stage") != stage or payload.get("sourceSha") != contract.source_revision or payload.get("sourceVersion") != contract.version or payload.get("exitCode") != 0:
                _fail(f"Dowód {profile}/{stage} jest niezgodny")
        compile_payload = _read_json(archive, f"{profile}-installer-compile-evidence.json")
        argument_list = compile_payload.get("argumentList")
        if compile_payload.get("state") != "exited" or compile_payload.get("exitCode") != 0 or not isinstance(argument_list, list) or f"/DProfil={profile}" not in argument_list:
            _fail(f"Dowód argv kompilatora {profile} jest niezgodny")


def _notes(profile: ProfileContract, installer_sha256: str, installer_bytes: int, contract: VerificationContract) -> str:
    lines = [
        f"# {profile.display_name} — {profile.display_version}",
        "",
        "## Co obejmuje to wydanie",
        "",
        "- Radio Archiwista: wyszukiwanie materiałów.",
        "- Podcasty: pełny opis jest kopiowany do schowka.",
        "- Skrzynka: po powrocie do listy pozostaje zaznaczony uprzednio wybrany odcinek, rozpoznany po stabilnej tożsamości; nie jest to pamięć pozycji odtwarzania. Ctrl+L otwiera Bibliotekę, a Ctrl+I wraca do Skrzynki.",
        "- NVDA + End: parametry są odczytywane przed nazwą materiału.",
        "- Nowe oznaczenie zidentyfikowanego materiału korzysta tylko z wiarygodnej tożsamości: dokładnego URI WiiM albo tożsamości własnego strumienia Sonos wyliczonej z jego ID i URL. Nie usuwa ani nie zmienia wcześniej zapisanych oznaczeń w innych sesjach; nie rozszerza rozpoznania na Ulubione, playlisty, Frontier ani Apple.",
        "- W tym wydaniu objęto tylko odebrane odświeżanie oraz rozdziały YouTube; notatka nie potwierdza szerszego zakresu odświeżania ani rozdziałów.",
        "- Poprawka Delete kolejki i odzyskiwanie audio nie są włączone w tym wydaniu.",
    ]
    if profile.profile == "test":
        lines.extend([
            "- AMC Test zawiera eksperymentalne Apple Music: zachowane są Biblioteka, wyszukiwanie i dodawanie bez odtwarzania ani naprawy logowania.",
        ])
    else:
        lines.extend(["- AMC Public nie zawiera Apple Music."])
    lines.extend([
        "",
        "## Sprawdzenie i ograniczenia",
        "",
        "- Instalator pochodzi z chronionej budowy GitHub: raport wskazuje aktywną ochronę Defendera, zero wykluczeń oraz brak wykryć i zdarzeń 1116/1117. Nie jest to odbiór instalacji ani żywy odbiór NVDA.",
        "- Status odbioru instalacji: nieprzeprowadzony. Status żywego odbioru NVDA: nieprzeprowadzony; wymagane pozostają osobne bramki i zgoda.",
        "- Wcześniejszy lokalny alert RCX pozostaje bez analizy historycznych próbek przez Microsoft. Nie jest to werdykt o tym wydaniu.",

        "",
        "## Instalator",
        "",
        f"Plik: `{profile.installer_name}`  ",
        f"Rozmiar: `{installer_bytes}` bajtów  ",
        f"SHA-256: `{installer_sha256}`  ",
        f"Źródła: `{contract.source_revision}`",
    ])
    return "\n".join(lines) + "\n"


def _update_manifest(profile: ProfileContract, sha256: str, bytes_count: int, contract: VerificationContract, summary: str) -> dict[str, Any]:
    return {
        "schemaVersion": 1,
        "repository": REPOSITORY,
        "tag": profile.tag,
        "profile": profile.profile,
        "updateChannel": profile.update_channel,
        "displayName": profile.display_name,
        "productId": profile.product_id,
        "installId": profile.install_id,
        "prerelease": profile.prerelease,
        "displayVersion": profile.display_version,
        "sourceRevision": contract.source_revision,
        "summary": summary,
        "installer": {"fileName": profile.installer_name, "bytes": bytes_count, "sha256": sha256},
    }


def verify_archives(binary_zip: Path, evidence_zip: Path, contract: VerificationContract = CONTRACT) -> dict[str, VerifiedProfile]:
    _assert_hash_and_size(binary_zip, contract.binary_artifact_bytes, contract.binary_artifact_sha256, "archiwum binarne")
    _assert_hash_and_size(evidence_zip, contract.evidence_artifact_bytes, contract.evidence_artifact_sha256, "archiwum dowodów")
    try:
        with zipfile.ZipFile(binary_zip) as binaries, zipfile.ZipFile(evidence_zip) as evidence:
            binary_members = _safe_members(binaries)
            evidence_members = _safe_members(evidence)
            expected_binary = {
                f"amc-{profile}-installer/{definition.installer_name}"
                for profile, definition in contract.profiles.items()
            } | {
                f"amc-{profile}-installer/{definition.installer_name}.integrity.json"
                for profile, definition in contract.profiles.items()
            }
            if set(binary_members) != expected_binary:
                _fail("Archiwum binarne nie ma dokładnie oczekiwanego układu czterech plików")
            _validate_evidence(evidence, evidence_members, contract)
            verified: dict[str, VerifiedProfile] = {}
            for profile, definition in contract.profiles.items():
                root = f"amc-{profile}-installer/{definition.installer_name}"
                executable = binaries.read(root)
                sidecar = _read_json(binaries, f"{root}.integrity.json")
                _validate_sidecar(sidecar, definition, executable, contract)
                digest = hashlib.sha256(executable).hexdigest()
                summary = "Foldery Biblioteki: pojedyncze komunikaty, rozróżnianie nazw i wskazanie istniejącej ścieżki przy konflikcie."
                if profile == "test":
                    summary += " Apple Music: wyszukiwanie i dodawanie do Biblioteki; brak odtwarzania."
                manifest = _update_manifest(definition, digest, len(executable), contract, summary)
                verified[profile] = VerifiedProfile(definition, len(executable), digest, sidecar, manifest, _notes(definition, digest, len(executable), contract))
            return verified
    except zipfile.BadZipFile as error:
        raise VerificationError("Archiwum Actions nie jest prawidłowym ZIP") from error


def write_release_inputs(binary_zip: Path, evidence_zip: Path, output: Path, contract: VerificationContract = CONTRACT) -> dict[str, VerifiedProfile]:
    verified = verify_archives(binary_zip, evidence_zip, contract)
    if output.exists():
        _fail("Katalog wyjściowy musi nie istnieć")
    output.mkdir(parents=True)
    with zipfile.ZipFile(binary_zip) as archive:
        for profile, item in verified.items():
            target = output / profile
            target.mkdir()
            member = f"amc-{profile}-installer/{item.contract.installer_name}"
            with archive.open(member) as source, (target / item.contract.installer_name).open("xb") as destination:
                shutil.copyfileobj(source, destination)
            with archive.open(f"{member}.integrity.json") as source, (target / f"{item.contract.installer_name}.integrity.json").open("xb") as destination:
                shutil.copyfileobj(source, destination)
            (target / f"amc-update-{profile}.json").write_text(json.dumps(item.update_manifest, indent=2) + "\n", encoding="utf-8")
            (target / "release-notes.md").write_text(item.notes, encoding="utf-8")
            (target / "verified-release-input.json").write_text(
                json.dumps({"tag": item.contract.tag, "sourceRevision": contract.source_revision, "installer": item.update_manifest["installer"]}, indent=2) + "\n",
                encoding="utf-8",
            )
    return verified


def _load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise VerificationError(f"Nie można odczytać JSON: {path}") from error
    if not isinstance(value, dict):
        _fail("JSON wydania nie jest obiektem")
    return value


def _updater_consumer() -> Any:
    """Load the updater parser without copying its channel-selection rules."""

    wxlite_root = Path(__file__).resolve().parents[1] / "wxlite"
    if str(wxlite_root) not in sys.path:
        sys.path.insert(0, str(wxlite_root))
    try:
        application_update = importlib.import_module("amc_wx_lite.application_update")
        release_profile = importlib.import_module("amc_wx_lite.release_profile")
    except ImportError as error:
        raise VerificationError(
            f"Nie można załadować rzeczywistego konsumenta aktualizacji: {error}"
        ) from error
    return application_update, release_profile.profile_from_id


def verify_updater_release(
    releases: object,
    manifest_path: Path,
    profile_id: str,
    expected_tag: str,
) -> Any:
    """Apply the real updater selection and validation to release JSON.

    The local manifest was generated only after ``verify_archives``.  Require
    GitHub's asset metadata to have the same bytes, length, and SHA-256 digest
    before feeding that local manifest to the production consumer.
    """

    updater, profile_from_id = _updater_consumer()
    if not isinstance(releases, list):
        _fail("Lista wydań GitHub nie jest tablicą JSON")
    try:
        profile = profile_from_id(profile_id)
    except ValueError as error:
        raise VerificationError("Nieznany profil aktualizacji") from error
    selected = updater._latest_channel_release(releases, profile)
    if selected is None:
        _fail("Konsument aktualizacji nie wybrał wydania kanału")
    tag, assets = selected
    if tag != expected_tag:
        _fail("Konsument aktualizacji wybrał inny tag")
    manifest_name = f"amc-update-{profile.profile_id}.json"
    manifest_asset = assets.get(manifest_name.casefold())
    if manifest_asset is None:
        _fail("Konsument aktualizacji nie widzi manifestu")
    try:
        manifest_bytes = manifest_path.read_bytes()
    except OSError as error:
        raise VerificationError("Nie można odczytać lokalnego manifestu aktualizacji") from error
    if len(manifest_bytes) != manifest_asset.size:
        _fail("GitHub wskazuje inny rozmiar manifestu")
    digest = hashlib.sha256(manifest_bytes).hexdigest()
    matching_assets: list[dict[str, object]] = []
    for candidate in releases:
        if not isinstance(candidate, dict) or candidate.get("tag_name") != tag:
            continue
        raw_assets = candidate.get("assets")
        if not isinstance(raw_assets, list):
            continue
        for asset in raw_assets:
            if (
                isinstance(asset, dict)
                and asset.get("name") == manifest_asset.name
                and asset.get("browser_download_url") == manifest_asset.download_url
            ):
                matching_assets.append(asset)
    if len(matching_assets) != 1 or matching_assets[0].get("digest") != f"sha256:{digest}":
        _fail("GitHub wskazuje inny SHA-256 manifestu")
    try:
        manifest = updater._json_object(manifest_bytes, description="manifestu aktualizacji")
        return updater._validated_release(
            manifest,
            tag=tag,
            assets=assets,
            profile=profile,
        )
    except updater.ApplicationUpdateError as error:
        raise VerificationError(f"Konsument aktualizacji odrzucił manifest: {error}") from error


def verify_release_readback(
    release: Mapping[str, Any],
    item: VerifiedProfile,
    downloaded_installer: Path,
    *,
    draft: bool,
    expected_release_id: int,
) -> None:
    if isinstance(expected_release_id, bool) or expected_release_id <= 0 or release.get("id") != expected_release_id:
        _fail("Release nie odpowiada utworzonemu ID")
    if release.get("tag_name") != item.contract.tag or release.get("target_commitish") != SOURCE_REVISION:
        _fail("Release nie wskazuje dozwolonego tagu i commitu źródeł")
    if release.get("draft") is not draft or release.get("prerelease") is not item.contract.prerelease:
        _fail("Release ma niewłaściwy stan kanału")
    assets = release.get("assets")
    if not isinstance(assets, list):
        _fail("Release nie ma listy assetów")
    prepared = downloaded_installer.parent
    expected_files = (
        item.contract.installer_name,
        f"{item.contract.installer_name}.integrity.json",
        f"amc-update-{item.contract.profile}.json",
    )
    expected = {
        name: (
            (prepared / name).stat().st_size,
            _sha256_path(prepared / name),
        )
        for name in expected_files
    }
    if {asset.get("name") for asset in assets if isinstance(asset, dict)} != set(expected):
        _fail("Release nie ma dokładnie trzech wymaganych assetów")
    for asset in assets:
        if not isinstance(asset, dict):
            _fail("Release ma nieprawidłowy asset")
        name = asset.get("name")
        if asset.get("state") != "uploaded" or name not in expected:
            _fail("Asset nie został przesłany")
        expected_size, expected_digest = expected[name]
        if asset.get("size") != expected_size or asset.get("digest") != f"sha256:{expected_digest}":
            _fail("Metadane assetu na GitHubie są niezgodne")
    if _sha256_path(downloaded_installer) != item.installer_sha256 or downloaded_installer.stat().st_size != item.installer_bytes:
        _fail("Instalator pobrany z release ma inne bajty")


def _emit(value: object) -> None:
    print(json.dumps(value, ensure_ascii=False, indent=2, default=dataclasses.asdict))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    prepare = subparsers.add_parser("prepare")
    prepare.add_argument("--binary-zip", type=Path, required=True)
    prepare.add_argument("--evidence-zip", type=Path, required=True)
    prepare.add_argument("--output", type=Path, required=True)
    readback = subparsers.add_parser("verify-release-readback")
    readback.add_argument("--release-json", type=Path, required=True)
    readback.add_argument("--profile", choices=("public", "test"), required=True)
    readback.add_argument("--downloaded-installer", type=Path, required=True)
    readback.add_argument("--draft", choices=("true", "false"), required=True)
    readback.add_argument("--release-id", type=int, required=True)
    updater_check = subparsers.add_parser("verify-updater-release")
    updater_check.add_argument("--releases-json", type=Path, required=True)
    updater_check.add_argument("--profile", choices=("public", "test"), required=True)
    updater_check.add_argument("--manifest", type=Path, required=True)
    updater_check.add_argument("--expected-tag", required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "prepare":
            verified = write_release_inputs(args.binary_zip, args.evidence_zip, args.output)
            _emit({profile: {"tag": value.contract.tag, "bytes": value.installer_bytes, "sha256": value.installer_sha256} for profile, value in verified.items()})
        elif args.command == "verify-release-readback":
            profile = CONTRACT.profiles[args.profile]
            prepared = args.downloaded_installer.parent / "verified-release-input.json"
            manifest = _load_json(prepared)
            item = VerifiedProfile(profile, manifest["installer"]["bytes"], manifest["installer"]["sha256"], {}, {}, "")
            verify_release_readback(
                _load_json(args.release_json),
                item,
                args.downloaded_installer,
                draft=args.draft == "true",
                expected_release_id=args.release_id,
            )
        else:
            try:
                releases = json.loads(args.releases_json.read_text(encoding="utf-8"))
            except (OSError, UnicodeError, json.JSONDecodeError) as error:
                raise VerificationError("Nie można odczytać listy wydań GitHub") from error
            verify_updater_release(releases, args.manifest, args.profile, args.expected_tag)
    except VerificationError as error:
        print(f"FAIL-CLOSED: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
