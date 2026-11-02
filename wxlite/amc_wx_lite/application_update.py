"""Bezpieczny klient aktualizacji calego AMC z przypisanego kanalu GitHub.

Profil wydania jest jedynym zrodlem kanalu.  Interfejs nie przyjmuje nazwy
kanalu ani adresu repozytorium od uzytkownika, wiec AMC Test nie moze pobrac
pakietu Public i odwrotnie.  Modul nie zalezy od wx; siec, porownanie wersji,
weryfikacje i przygotowanie instalatora mozna sprawdzic w izolowanych testach.
"""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Callable

from .release_profile import ReleaseProfile, current_profile


REPOSITORY = "michalkasperczak/AMC"
RELEASES_API = f"https://api.github.com/repos/{REPOSITORY}/releases?per_page=30"
UPDATE_MANIFEST_SCHEMA = 1
MAX_JSON_BYTES = 512 * 1024
MAX_INSTALLER_BYTES = 2 * 1024 * 1024 * 1024
USER_AGENT = "AMC-Application-Updater/1"


class ApplicationUpdateError(RuntimeError):
    """Blad, ktory interfejs zamienia na krotki tekst uzytkowy."""


class ApplicationUpdateNetworkError(ApplicationUpdateError):
    """GitHub albo polaczenie nie odpowiedzialy prawidlowo."""


class ApplicationUpdateIntegrityError(ApplicationUpdateError):
    """Metadane albo paczka nie spelnily kontraktu wydania."""


class ApplicationUpdateRecordingError(ApplicationUpdateError):
    """Aktywne nagrywanie musi się zakończyć przed podmianą programu."""


class ApplicationUpdateRecordingStatusError(ApplicationUpdateError):
    """Stan nagrywania jest nieznany, więc zamknięcie AMC byłoby ryzykowne."""


@dataclass(frozen=True, slots=True)
class ReleaseAsset:
    name: str
    size: int
    download_url: str


@dataclass(frozen=True, slots=True)
class ApplicationUpdateRelease:
    tag: str
    profile_id: str
    channel: str
    display_name: str
    display_version: str
    source_revision: str
    summary: str
    installer: ReleaseAsset
    installer_sha256: str


@dataclass(frozen=True, slots=True)
class ApplicationUpdateCheck:
    profile: ReleaseProfile
    current_display_version: str
    available: bool
    release: ApplicationUpdateRelease | None

    @property
    def information_text(self) -> str:
        if self.available and self.release is not None:
            return (
                f"Dostępna wersja: {self.release.display_name} — "
                f"{self.release.display_version}.\n\n"
                f"Co nowego:\n{self.release.summary}"
            )
        return (
            f"Masz najnowszą wersję {self.profile.display_name} dla tego "
            f"kanału.\n\nBieżąca wersja: {self.current_display_version}."
        )


@dataclass(frozen=True, slots=True)
class PreparedApplicationUpdate:
    release: ApplicationUpdateRelease
    installer_path: Path
    target_root: Path
    updater_path: Path


FetchBytes = Callable[[str, int], bytes]
DownloadFile = Callable[[str, Path, int], tuple[int, str]]


def package_root() -> Path:
    """Korzen uruchomionej paczki, niezaleznie od katalogu roboczego."""

    return Path(__file__).resolve().parents[2]


def _request(url: str) -> urllib.request.Request:
    return urllib.request.Request(
        url,
        headers={
            "Accept": "application/vnd.github+json",
            "User-Agent": USER_AGENT,
            "X-GitHub-Api-Version": "2022-11-28",
        },
        method="GET",
    )


def _read_bounded(response: object, maximum: int) -> bytes:
    length_text = getattr(response, "headers", {}).get("Content-Length", "")
    try:
        declared = int(length_text) if length_text else 0
    except (TypeError, ValueError):
        declared = 0
    if declared < 0 or declared > maximum:
        raise ApplicationUpdateIntegrityError("Zbyt duża odpowiedź serwera")
    data = response.read(maximum + 1)
    if len(data) > maximum:
        raise ApplicationUpdateIntegrityError("Zbyt duża odpowiedź serwera")
    return data


def _fetch_bytes(url: str, maximum: int) -> bytes:
    try:
        with urllib.request.urlopen(_request(url), timeout=30) as response:
            return _read_bounded(response, maximum)
    except ApplicationUpdateIntegrityError:
        raise
    except (OSError, TimeoutError, urllib.error.URLError) as error:
        raise ApplicationUpdateNetworkError("Nie udało się odczytać wydania") from error


def _download_file(url: str, destination: Path, maximum: int) -> tuple[int, str]:
    digest = hashlib.sha256()
    total = 0
    try:
        with urllib.request.urlopen(_request(url), timeout=60) as response:
            length_text = response.headers.get("Content-Length", "")
            try:
                declared = int(length_text) if length_text else 0
            except (TypeError, ValueError):
                declared = 0
            if declared < 0 or declared > maximum:
                raise ApplicationUpdateIntegrityError("Instalator ma nieprawidłowy rozmiar")
            with destination.open("xb") as output:
                while True:
                    chunk = response.read(1024 * 1024)
                    if not chunk:
                        break
                    total += len(chunk)
                    if total > maximum:
                        raise ApplicationUpdateIntegrityError(
                            "Instalator przekracza dozwolony rozmiar"
                        )
                    output.write(chunk)
                    digest.update(chunk)
    except ApplicationUpdateError:
        destination.unlink(missing_ok=True)
        raise
    except (OSError, TimeoutError, urllib.error.URLError) as error:
        destination.unlink(missing_ok=True)
        raise ApplicationUpdateNetworkError("Nie udało się pobrać instalatora") from error
    return total, digest.hexdigest()


def _json_object(data: bytes, *, description: str) -> dict[str, object]:
    try:
        value = json.loads(data.decode("utf-8-sig"))
    except (UnicodeError, ValueError, TypeError) as error:
        raise ApplicationUpdateIntegrityError(
            f"Nieprawidłowy format {description}"
        ) from error
    if not isinstance(value, dict):
        raise ApplicationUpdateIntegrityError(f"Nieprawidłowa treść {description}")
    return value


def _json_list(data: bytes, *, description: str) -> list[object]:
    try:
        value = json.loads(data.decode("utf-8-sig"))
    except (UnicodeError, ValueError, TypeError) as error:
        raise ApplicationUpdateIntegrityError(
            f"Nieprawidłowy format {description}"
        ) from error
    if not isinstance(value, list):
        raise ApplicationUpdateIntegrityError(f"Nieprawidłowa treść {description}")
    return value


def _required_text(payload: dict[str, object], name: str, maximum: int = 512) -> str:
    value = payload.get(name)
    if not isinstance(value, str):
        raise ApplicationUpdateIntegrityError("Manifest aktualizacji jest niepełny")
    text = value.strip()
    if not text or len(text) > maximum or "\x00" in text:
        raise ApplicationUpdateIntegrityError("Manifest aktualizacji jest nieprawidłowy")
    return text


def _required_int(payload: dict[str, object], name: str) -> int:
    value = payload.get(name)
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise ApplicationUpdateIntegrityError("Manifest aktualizacji jest nieprawidłowy")
    return value


def _display_datetime(value: str) -> datetime:
    try:
        return datetime.strptime(value, "%d.%m.%Y, %H:%M")
    except ValueError as error:
        raise ApplicationUpdateIntegrityError(
            "Wydanie ma nieprawidłową datę wersji"
        ) from error


def _sha256(value: str) -> str:
    normalized = value.strip().lower()
    if len(normalized) != 64 or any(character not in "0123456789abcdef" for character in normalized):
        raise ApplicationUpdateIntegrityError("Wydanie ma nieprawidłową sumę kontrolną")
    return normalized


def _source_revision(value: str) -> str:
    normalized = value.strip().lower()
    if len(normalized) != 40 or any(character not in "0123456789abcdef" for character in normalized):
        raise ApplicationUpdateIntegrityError("Wydanie ma nieprawidłową rewizję źródeł")
    return normalized


def _release_asset(payload: object) -> ReleaseAsset | None:
    if not isinstance(payload, dict):
        return None
    name = payload.get("name")
    size = payload.get("size")
    url = payload.get("browser_download_url")
    if (
        not isinstance(name, str)
        or not name
        or Path(name).name != name
        or isinstance(size, bool)
        or not isinstance(size, int)
        or size < 0
        or not isinstance(url, str)
        or not url.startswith(
            f"https://github.com/{REPOSITORY}/releases/download/"
        )
    ):
        return None
    return ReleaseAsset(name=name, size=size, download_url=url)


def _latest_channel_release(
    releases: list[object], profile: ReleaseProfile
) -> tuple[str, dict[str, ReleaseAsset]] | None:
    prefix = "test-" if profile.update_channel == "test" else "public-"
    expected_prerelease = profile.update_channel == "test"
    for candidate in releases:
        if not isinstance(candidate, dict):
            continue
        tag = candidate.get("tag_name")
        if (
            candidate.get("draft") is True
            or candidate.get("prerelease") is not expected_prerelease
            or not isinstance(tag, str)
            or re.fullmatch(re.escape(prefix) + r"\d{4}\.\d{2}\.\d{2}\.\d{4}", tag)
            is None
        ):
            continue
        assets: dict[str, ReleaseAsset] = {}
        raw_assets = candidate.get("assets")
        if not isinstance(raw_assets, list):
            continue
        duplicate = False
        for raw_asset in raw_assets:
            asset = _release_asset(raw_asset)
            if asset is None:
                continue
            folded = asset.name.casefold()
            if folded in assets:
                duplicate = True
                break
            assets[folded] = asset
        if not duplicate:
            return tag, assets
    return None


def _installed_manifest(root: Path, profile: ReleaseProfile) -> dict[str, object]:
    path = root / "bundle-manifest.json"
    try:
        if path.stat().st_size > 8 * 1024 * 1024:
            raise ApplicationUpdateIntegrityError("Manifest instalacji jest zbyt duży")
        payload = _json_object(path.read_bytes(), description="manifestu instalacji")
    except FileNotFoundError as error:
        raise ApplicationUpdateIntegrityError("Brak manifestu bieżącej instalacji") from error
    except OSError as error:
        raise ApplicationUpdateIntegrityError(
            "Nie można odczytać manifestu bieżącej instalacji"
        ) from error
    for name, expected in (
        ("releaseProfile", profile.profile_id),
        ("productId", profile.product_id),
        ("installId", profile.install_id),
        ("updateChannel", profile.update_channel),
    ):
        if payload.get(name) != expected:
            raise ApplicationUpdateIntegrityError("Bieżąca instalacja ma inny profil")
    return payload


def _validated_release(
    manifest: dict[str, object],
    *,
    tag: str,
    assets: dict[str, ReleaseAsset],
    profile: ReleaseProfile,
) -> ApplicationUpdateRelease:
    if manifest.get("schemaVersion") != UPDATE_MANIFEST_SCHEMA:
        raise ApplicationUpdateIntegrityError("Nieobsługiwana wersja manifestu aktualizacji")
    expected = {
        "repository": REPOSITORY,
        "tag": tag,
        "profile": profile.profile_id,
        "updateChannel": profile.update_channel,
        "displayName": profile.display_name,
        "productId": profile.product_id,
        "installId": profile.install_id,
        "prerelease": profile.update_channel == "test",
    }
    if any(manifest.get(name) != value for name, value in expected.items()):
        raise ApplicationUpdateIntegrityError("Wydanie jest przeznaczone dla innego AMC")
    display_version = _required_text(manifest, "displayVersion", 64)
    _display_datetime(display_version)
    source_revision = _source_revision(_required_text(manifest, "sourceRevision", 64))
    summary = _required_text(manifest, "summary", 8_000)
    installer_payload = manifest.get("installer")
    if not isinstance(installer_payload, dict):
        raise ApplicationUpdateIntegrityError("Manifest nie wskazuje instalatora")
    installer_name = _required_text(installer_payload, "fileName", 260)
    expected_prefix = "AMC-Test-" if profile.profile_id == "test" else "AMC-Public-"
    if (
        Path(installer_name).name != installer_name
        or not installer_name.lower().endswith(".exe")
        or not installer_name.startswith(expected_prefix)
    ):
        raise ApplicationUpdateIntegrityError("Manifest wskazuje nieprawidłowy instalator")
    installer_size = _required_int(installer_payload, "bytes")
    if installer_size <= 0 or installer_size > MAX_INSTALLER_BYTES:
        raise ApplicationUpdateIntegrityError("Instalator ma nieprawidłowy rozmiar")
    installer_sha256 = _sha256(_required_text(installer_payload, "sha256", 64))
    asset = assets.get(installer_name.casefold())
    if asset is None or asset.size != installer_size:
        raise ApplicationUpdateIntegrityError("Wydanie nie zawiera oczekiwanego instalatora")
    return ApplicationUpdateRelease(
        tag=tag,
        profile_id=profile.profile_id,
        channel=profile.update_channel,
        display_name=profile.display_name,
        display_version=display_version,
        source_revision=source_revision,
        summary=summary,
        installer=asset,
        installer_sha256=installer_sha256,
    )


def check_for_application_update(
    *,
    root: Path | None = None,
    fetch_bytes: FetchBytes = _fetch_bytes,
) -> ApplicationUpdateCheck:
    """Sprawdz najnowsze wydanie stale przypisanego kanalu profilu."""

    install_root = (root or package_root()).resolve()
    profile = current_profile(package_root=install_root)
    installed = _installed_manifest(install_root, profile)
    current_display = _required_text(installed, "displayVersion", 64)
    current_time = _display_datetime(current_display)
    current_revision = _source_revision(_required_text(installed, "sourceRevision", 64))

    releases = _json_list(
        fetch_bytes(RELEASES_API, MAX_JSON_BYTES),
        description="listy wydań",
    )
    selected = _latest_channel_release(releases, profile)
    if selected is None:
        return ApplicationUpdateCheck(profile, current_display, False, None)
    tag, assets = selected
    manifest_name = f"amc-update-{profile.profile_id}.json"
    manifest_asset = assets.get(manifest_name.casefold())
    if manifest_asset is None or manifest_asset.size <= 0 or manifest_asset.size > MAX_JSON_BYTES:
        raise ApplicationUpdateIntegrityError("Wydanie nie zawiera manifestu aktualizacji")
    manifest_bytes = fetch_bytes(manifest_asset.download_url, MAX_JSON_BYTES)
    if len(manifest_bytes) != manifest_asset.size:
        raise ApplicationUpdateIntegrityError("Manifest ma inny rozmiar niż wydanie")
    manifest = _json_object(
        manifest_bytes,
        description="manifestu aktualizacji",
    )
    release = _validated_release(
        manifest,
        tag=tag,
        assets=assets,
        profile=profile,
    )
    release_time = _display_datetime(release.display_version)
    available = release_time > current_time or (
        release_time == current_time and release.source_revision != current_revision
    )
    return ApplicationUpdateCheck(profile, current_display, available, release)


def _default_update_cache(profile: ReleaseProfile) -> Path:
    local = os.environ.get("LOCALAPPDATA")
    if not local:
        raise ApplicationUpdateIntegrityError("Brak katalogu danych lokalnych Windows")
    return Path(local) / "AMC-Application-Updates" / profile.profile_id


def prepare_application_update(
    check: ApplicationUpdateCheck,
    *,
    root: Path | None = None,
    cache_root: Path | None = None,
    download_file: DownloadFile = _download_file,
) -> PreparedApplicationUpdate:
    """Pobierz i sprawdz pelny instalator, ale jeszcze nie zamykaj AMC."""

    if not check.available or check.release is None:
        raise ApplicationUpdateIntegrityError("Nie ma aktualizacji do pobrania")
    release = check.release
    target = (root or package_root()).resolve()
    cache = (cache_root or _default_update_cache(check.profile)).resolve()
    cache.mkdir(parents=True, exist_ok=True)
    operation = Path(tempfile.mkdtemp(prefix="aktualizacja-", dir=cache))
    installer_path = operation / release.installer.name
    try:
        actual_size, actual_hash = download_file(
            release.installer.download_url,
            installer_path,
            MAX_INSTALLER_BYTES,
        )
        if (
            actual_size != release.installer.size
            or actual_hash.lower() != release.installer_sha256
        ):
            raise ApplicationUpdateIntegrityError(
                "Pobrany instalator ma inną sumę kontrolną"
            )
        updater = target / "AMC-Python-Updater.exe"
        if not updater.is_file():
            raise ApplicationUpdateIntegrityError("Bieżąca instalacja nie ma aktualizatora")
        return PreparedApplicationUpdate(release, installer_path, target, updater)
    except Exception:
        installer_path.unlink(missing_ok=True)
        try:
            operation.rmdir()
        except OSError:
            pass
        raise


def launch_prepared_application_update(
    prepared: PreparedApplicationUpdate,
) -> subprocess.Popen[bytes]:
    """Uruchom pomocnika poza instalacją, aby móc odczytać jego rzeczywisty wynik."""

    helper = prepared.installer_path.parent / "AMC-Python-Updater.exe"
    try:
        shutil.copy2(prepared.updater_path, helper)
    except OSError as error:
        raise ApplicationUpdateError("Nie udało się przygotować aktualizatora") from error

    arguments = [
        str(helper),
        "--installer",
        str(prepared.installer_path),
        "--installer-sha256",
        prepared.release.installer_sha256,
        "--target",
        str(prepared.target_root),
        "--wait-pid",
        str(os.getpid()),
        "--launch",
        "AMC.exe",
        "--cleanup-installer",
        "--silent",
    ]
    try:
        return subprocess.Popen(  # noqa: S603 - jawny, zweryfikowany plik instalacji
            arguments,
            cwd=prepared.target_root,
            close_fds=True,
        )
    except OSError as error:
        raise ApplicationUpdateError("Nie udało się uruchomić aktualizatora") from error


def recording_blocks_application_update(status: object) -> bool:
    """Użyj świeżej migawki hosta, nie pamięci podręcznej interfejsu."""

    if not isinstance(status, dict):
        return True
    try:
        if int(status.get("count") or 0) > 0:
            return True
    except (TypeError, ValueError):
        pass
    rows = status.get("recordings")
    if not isinstance(rows, list):
        return "count" not in status
    return any(
        isinstance(row, dict) and str(row.get("state") or "").lower()
        not in ("finished", "stopped", "failed")
        for row in rows
    )


def schedule_blocks_application_update(
    status: object, *, now_utc_ticks: int | None = None
) -> bool:
    """Chron również przerwy między plikami w bieżącym oknie harmonogramu."""

    if not isinstance(status, dict):
        return True
    active_ids = status.get("activeIds")
    schedules = status.get("schedules")
    if not isinstance(active_ids, list) or not isinstance(schedules, list):
        return True
    if active_ids:
        return True
    now = now_utc_ticks
    if now is None:
        now = time.time_ns() // 100 + 621355968000000000
    try:
        protected_until = int(status.get("updateProtectedUntilUtcTicks") or 0)
    except (TypeError, ValueError):
        return True
    if protected_until > now:
        return True
    minute = 60 * 10_000_000
    for schedule in schedules:
        if not isinstance(schedule, dict):
            return True
        if not schedule.get("enabled"):
            continue
        try:
            start = int(schedule["nextStartUtcTicks"])
            duration = int(schedule["durationMinutes"])
        except (KeyError, TypeError, ValueError):
            return True
        if duration <= 0 or start <= 0:
            return True
        if start - 5 * minute <= now <= start + (duration + 2) * minute:
            return True
    return False


def user_facing_update_error(error: Exception) -> str:
    """Nie ujawniaj URL-i, sciezek ani typow wyjatkow w zwyklej mowie NVDA."""

    if isinstance(error, ApplicationUpdateNetworkError):
        return "Nie udało się połączyć z serwerem aktualizacji. Spróbuj ponownie później."
    if isinstance(error, ApplicationUpdateRecordingError):
        return "Trwa nagrywanie lub jego zaplanowane okno. Aktualizacja nie została uruchomiona. Spróbuj ponownie po zakończeniu."
    if isinstance(error, ApplicationUpdateRecordingStatusError):
        return "Nie można sprawdzić stanu nagrywania. Aktualizacja nie została uruchomiona. Spróbuj ponownie później."
    if isinstance(error, ApplicationUpdateIntegrityError):
        return "Dane aktualizacji nie przeszły kontroli bezpieczeństwa. Nic nie zostało zainstalowane."
    return "Nie udało się przygotować aktualizacji. Nic nie zostało zainstalowane."
