"""Czytelna tozsamosc uruchomionej paczki AMC Test albo AMC Public.

Numer kompilacji pochodzi z podpisanego sumami manifestu paczki.  Nie
pokazujemy sciezek, obiektow ani surowych bledow parsera w tytule okna.
"""

from __future__ import annotations

import json
import re
from datetime import datetime
from pathlib import Path

from . import __version__
from .release_profile import current_profile


_PROFILE = current_profile()
PRODUCT_NAME = _PROFILE.display_name
LEGACY_PRODUCT_NAMES = (
    ("AMC Python",) if _PROFILE.profile_id == "test" else ("AMC",)
)


_BUILD_LABEL = re.compile(
    r"^\d{4}\.\d{2}\.\d{2}"
    r"(?:(?:-[0-9a-f]{7,12}){0,2}|-[0-9a-f]{40}-[0-9a-f]{8})"
    r"(?:-robocza)?$",
    re.IGNORECASE,
)
_DISPLAY_VERSION = re.compile(
    r"^(?:0[1-9]|[12]\d|3[01])\."
    r"(?:0[1-9]|1[0-2])\.\d{4}, "
    r"(?:[01]\d|2[0-3]):[0-5]\d$"
)


def package_root(module_file: str | Path | None = None) -> Path:
    """Folder paczki zawierajacy ``AMC.exe`` i manifest."""

    source = Path(module_file) if module_file is not None else Path(__file__)
    return source.resolve().parents[2]


def read_build_label(root: str | Path | None = None) -> str:
    """Zwroc bezpieczna etykiete kompilacji albo pusty tekst."""

    base = Path(root) if root is not None else package_root()
    manifest = base / "bundle-manifest.json"
    try:
        if manifest.stat().st_size > 8 * 1024 * 1024:
            return ""
        payload = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return ""
    label = payload.get("buildLabel") if isinstance(payload, dict) else None
    if not isinstance(label, str):
        return ""
    label = label.strip()
    return label if _BUILD_LABEL.fullmatch(label) else ""


def read_build_time_label(root: str | Path | None = None) -> str:
    """Zwróć stałą datę i godzinę paczki bez szczegółów technicznych."""

    base = Path(root) if root is not None else package_root()
    manifest = base / "bundle-manifest.json"
    try:
        if manifest.stat().st_size > 8 * 1024 * 1024:
            return ""
        payload = json.loads(manifest.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, json.JSONDecodeError):
        return ""
    if not isinstance(payload, dict):
        return ""
    display_version = payload.get("displayVersion")
    if isinstance(display_version, str):
        display_version = display_version.strip()
        if _DISPLAY_VERSION.fullmatch(display_version):
            return display_version

    # Zgodność ze starszymi paczkami: przed wprowadzeniem stałej etykiety
    # użytkowej manifest zawierał wyłącznie czas UTC.
    created = payload.get("created")
    if not isinstance(created, str):
        return ""
    try:
        timestamp = datetime.fromisoformat(created.strip())
    except ValueError:
        return ""
    if timestamp.tzinfo is None:
        return ""
    return timestamp.astimezone().strftime("%d.%m.%Y, %H:%M")


def window_title(root: str | Path | None = None) -> str:
    """Tytul widoczny i czytany przez NVDA."""

    build_time = read_build_time_label(root)
    return (
        f"{PRODUCT_NAME} — {build_time}"
        if build_time
        else f"{PRODUCT_NAME} — wersja {__version__}"
    )


def window_title_with_playback(base: str, playing_title: str, position: str) -> str:
    """Compose a short NVDA+T label without changing the stable app identity."""

    title = " ".join(playing_title.split())[:160].strip()
    if not title or position == "off":
        return base
    if position == "before":
        return f"{title} — {base}"
    return f"{base} — {title}"


def is_main_window_title(title: str) -> bool:
    """Rozpoznaj obecne i przejściowo starsze główne okno aplikacji."""

    return any(
        title == name or title.startswith(name + " — ") or title.endswith(" — " + name)
        or (" — " + name + " — ") in title
        for name in (PRODUCT_NAME, *LEGACY_PRODUCT_NAMES)
    )
