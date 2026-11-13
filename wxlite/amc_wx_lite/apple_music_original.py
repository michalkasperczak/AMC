"""Hand a public Apple Music track link to the installed Windows app."""

from __future__ import annotations

import re
import shutil
import subprocess

from .apple_music_source import AppleMusicRecord


_CATALOG_ID = re.compile(r"[0-9]{1,20}\Z")


class AppleMusicOriginalUnavailable(RuntimeError):
    """A safe, user-facing failure without a technical identifier."""

    def __str__(self) -> str:
        return "Nie można otworzyć utworu w oryginalnym Apple Music."


def track_url(record: AppleMusicRecord) -> str | None:
    """Prefer an Apple-provided URL; library records usually omit it."""

    if record.row.kind != "track" or not record.target.can_play:
        return None
    if record.row.url:
        return record.row.url
    catalog_id = record.target.catalog_id or ""
    if not _CATALOG_ID.fullmatch(catalog_id):
        return None
    return f"https://music.apple.com/pl/song/{catalog_id}"


def available(record: AppleMusicRecord | None) -> bool:
    """One availability rule for menus, help, palette and execution."""

    return bool(
        record is not None
        and track_url(record)
        and shutil.which("AppleMusic.exe")
    )


def open_in_original(
    record: AppleMusicRecord,
    *,
    find_executable=shutil.which,
    start=subprocess.Popen,
) -> None:
    """Use Apple's registered execution alias, never the browser default."""

    address = track_url(record)
    executable = find_executable("AppleMusic.exe")
    if not address or not executable:
        raise AppleMusicOriginalUnavailable()
    try:
        start([executable, "/url", address], close_fds=True)
    except OSError:
        raise AppleMusicOriginalUnavailable() from None
