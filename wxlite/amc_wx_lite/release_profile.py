"""Jawna tozsamosc wydania i jego mozliwosci.

Profil wydania jest jednym zrodlem prawdy dla nazw produktu, izolacji danych,
kanalu aktualizacji oraz uslug, ktore wolno pokazac uzytkownikowi.  Modul nie
zna wx ani systemowego rejestru, dlatego mozna go sprawdzic bez uruchamiania
aplikacji i wykorzystac podczas budowania paczki.
"""

from __future__ import annotations

import json
import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path, PurePosixPath
from typing import Mapping


PROFILE_SCHEMA_VERSION = 1
TEST_PROFILE_ID = "test"
PUBLIC_PROFILE_ID = "public"


@dataclass(frozen=True, slots=True)
class ReleaseProfile:
    """Niezmienny, wersjonowany kontrakt jednego wariantu AMC."""

    profile_id: str
    product_id: str
    display_name: str
    install_id: str
    update_channel: str
    ui_state_folder: str
    roaming_profile_folder: str
    local_profile_folder: str
    cache_folder: str
    log_folder: str
    credential_namespace: str
    session_ids: tuple[str, ...]
    capabilities: tuple[str, ...]

    def manifest_fields(self) -> dict[str, object]:
        """Pola dopisywane do manifestu paczki bez wartosci technicznych w UI."""

        return {
            "releaseProfileSchema": PROFILE_SCHEMA_VERSION,
            "releaseProfile": self.profile_id,
            "productId": self.product_id,
            "displayName": self.display_name,
            "installId": self.install_id,
            "updateChannel": self.update_channel,
            "uiStateFolder": self.ui_state_folder,
            "roamingProfileFolder": self.roaming_profile_folder,
            "localProfileFolder": self.local_profile_folder,
            "cacheFolder": self.cache_folder,
            "logFolder": self.log_folder,
            "credentialNamespace": self.credential_namespace,
            "sessions": list(self.session_ids),
            "capabilities": list(self.capabilities),
        }


_COMMON_CAPABILITIES = (
    "session.files",
    "session.radio",
    "session.podcasts-youtube",
    "session.wiim",
    "session.sonos",
    "recording",
    "schedules",
    "bookmarks",
    "chapters",
    "command-palette",
    "keyboard-help",
    "problem-report-local",
    "experimental",
)

_PUBLIC_CAPABILITIES = tuple(
    capability
    for capability in _COMMON_CAPABILITIES
    if capability != "session.sonos"
) + ("session.frontier-smart",)

TEST_PROFILE = ReleaseProfile(
    profile_id=TEST_PROFILE_ID,
    product_id="org.accessible-media-controller.test",
    display_name="AMC Test",
    install_id="AMC.Test.win-x64",
    update_channel="test",
    # Zachowanie obecnej instalacji pozostaje bez migracji.
    ui_state_folder="AMC-wx-Lite",
    roaming_profile_folder="AccessibleMediaController",
    local_profile_folder="AccessibleMediaController",
    cache_folder="AMC-wx-Lite/cache",
    log_folder="AMC-wx-Lite/logs",
    credential_namespace="AccessibleMediaController.Test",
    session_ids=(
        "files",
        "radio",
        "podcasts",
        "tidal",
        "wiim",
        "sonos",
        "spotify",
        "frontierSmart",
        "radioarchiwista",
    ),
    capabilities=_COMMON_CAPABILITIES
    + (
        "session.spotify",
        "session.tidal",
        "session.frontier-smart",
        "session.radioarchiwista",
        "spotify.playlists",
        "youtube.account.readonly",
    ),
)

PUBLIC_PROFILE = ReleaseProfile(
    profile_id=PUBLIC_PROFILE_ID,
    product_id="org.accessible-media-controller.public",
    display_name="AMC Public",
    install_id="AMC.Public.win-x64",
    update_channel="stable",
    ui_state_folder="AMC/ui",
    roaming_profile_folder="AMC/profile",
    local_profile_folder="AMC/profile",
    cache_folder="AMC/cache",
    log_folder="AMC/logs",
    credential_namespace="AccessibleMediaController.Public",
    session_ids=("files", "radio", "podcasts", "wiim", "frontierSmart"),
    capabilities=_PUBLIC_CAPABILITIES,
)

PROFILES: dict[str, ReleaseProfile] = {
    TEST_PROFILE.profile_id: TEST_PROFILE,
    PUBLIC_PROFILE.profile_id: PUBLIC_PROFILE,
}


def profile_from_id(profile_id: object) -> ReleaseProfile:
    """Zwracaj tylko zatwierdzone profile; literowka nie moze zmienic danych."""

    if not isinstance(profile_id, str):
        raise ValueError("Nieznany profil wydania")
    try:
        return PROFILES[profile_id.strip().casefold()]
    except KeyError as error:
        raise ValueError(f"Nieznany profil wydania: {profile_id}") from error


@lru_cache(maxsize=8)
def _manifest_profile_for_root(root_text: str) -> str | None:
    root = Path(root_text)
    manifest = root / "bundle-manifest.json"
    try:
        if not manifest.is_file() or manifest.stat().st_size > 8 * 1024 * 1024:
            return None
        raw = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, ValueError, TypeError):
        return None
    if (
        not isinstance(raw, dict)
        or raw.get("releaseProfileSchema") != PROFILE_SCHEMA_VERSION
    ):
        return None
    value = raw.get("releaseProfile")
    return value if isinstance(value, str) else None


def _manifest_profile(package_root: Path | None = None) -> str | None:
    root = package_root or Path(__file__).resolve().parents[2]
    return _manifest_profile_for_root(str(root.resolve()))


def current_profile(
    environment: Mapping[str, str] | None = None,
    package_root: Path | None = None,
) -> ReleaseProfile:
    """Wybierz profil: jawne srodowisko, manifest, a na koncu zgodny AMC Test."""

    env = os.environ if environment is None else environment
    requested = env.get("AMC_RELEASE_PROFILE") or _manifest_profile(package_root)
    return profile_from_id(requested or TEST_PROFILE_ID)


def _safe_relative_folder(value: str) -> Path:
    """Nie pozwol manifestowi wyjsc poza bazowy katalog systemowy."""

    candidate = PurePosixPath(value.replace("\\", "/"))
    if candidate.is_absolute() or not candidate.parts or any(
        part in ("", ".", "..") for part in candidate.parts
    ):
        raise ValueError("Nieprawidlowy katalog profilu wydania")
    return Path(*candidate.parts)


def profile_directory(base: Path, relative_folder: str) -> Path:
    """Rozwiaz katalog profilu wylacznie ponizej jawnej bazy systemowej."""

    return Path(base) / _safe_relative_folder(relative_folder)
