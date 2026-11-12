#!/usr/bin/env python3
"""Read-only Apple Music diagnostics with deliberately bounded output."""

from __future__ import annotations

import argparse
from collections.abc import Callable
from pathlib import Path
import sys


WXLITE = Path(__file__).resolve().parent.parent
if str(WXLITE) not in sys.path:
    sys.path.insert(0, str(WXLITE))

from amc_wx_lite.apple_music_credentials import (  # noqa: E402
    AppleMusicCredentialStore,
)
from amc_wx_lite.profile_layout import resolve_layout  # noqa: E402


Output = Callable[[str], None]


def diagnose_client(client: object, *, output: Output = print) -> bool:
    """Exercise only approved reads and never echo provider exceptions."""

    operations = (
        ("Storefront", "current_user_storefront"),
        ("Playlisty", "current_user_playlists"),
        ("Utwory", "current_user_saved_tracks"),
    )
    all_ok = True
    for label, method_name in operations:
        try:
            value = getattr(client, method_name)()
            count = len(value) if isinstance(value, list) else 1
            output(f"{label}: OK, elementów {count}")
        except Exception as error:
            all_ok = False
            status = getattr(error, "status_code", None)
            if isinstance(status, int) and 0 <= status <= 999:
                output(f"{label}: HTTP {status}")
            else:
                output(f"{label}: błąd bez szczegółów")
    return all_ok


def diagnose_library_parameters(client: object, *, output: Output = print) -> bool:
    """Identify a rejected query parameter without exposing response bodies."""

    variants = (
        ("bez parametrów", None),
        ("limit 25", {"limit": 25}),
        ("język en-US i limit 25", {"l": "en-US", "limit": 25}),
        ("język pl-PL i limit 25", {"l": "pl-PL", "limit": 25}),
    )
    any_ok = False
    request = getattr(client, "_request")
    for label, params in variants:
        try:
            response = request("GET", "/v1/me/library/songs", params=params)
            envelope = response.json()
            data = envelope.get("data", []) if isinstance(envelope, dict) else []
            count = len(data) if isinstance(data, list) else 0
            output(f"Utwory, {label}: OK, elementów na stronie {count}")
            any_ok = True
        except Exception as error:
            status = getattr(error, "status_code", None)
            if isinstance(status, int) and 0 <= status <= 999:
                output(f"Utwory, {label}: HTTP {status}")
            else:
                output(f"Utwory, {label}: błąd bez szczegółów")
    return any_ok


def diagnose_first_pages(client: object, *, output: Output = print) -> bool:
    """Read one bounded page from each core collection, never paginate."""

    operations = (
        ("Playlisty", "/v1/me/library/playlists"),
        ("Utwory", "/v1/me/library/songs"),
        ("Albumy", "/v1/me/library/albums"),
        ("Wykonawcy", "/v1/me/library/artists"),
    )
    all_ok = True
    request = getattr(client, "_request")
    for label, path in operations:
        try:
            response = request(
                "GET",
                path,
                params={"l": "pl-PL", "limit": 25},
            )
            envelope = response.json()
            data = envelope.get("data", []) if isinstance(envelope, dict) else []
            count = len(data) if isinstance(data, list) else 0
            more = bool(envelope.get("next")) if isinstance(envelope, dict) else False
            output(
                f"{label}: pierwsza strona {count}, "
                + ("są dalsze strony" if more else "to cała lista")
            )
        except Exception as error:
            all_ok = False
            status = getattr(error, "status_code", None)
            if isinstance(status, int) and 0 <= status <= 999:
                output(f"{label}: HTTP {status}")
            else:
                output(f"{label}: błąd bez szczegółów")
    return all_ok


def main() -> int:
    reconfigure = getattr(sys.stdout, "reconfigure", None)
    if callable(reconfigure):
        reconfigure(encoding="utf-8", line_buffering=True)
    parser = argparse.ArgumentParser(
        description="Bezpieczna diagnostyka odczytu Apple Music."
    )
    parser.add_argument(
        "--first-page",
        action="store_true",
        help="odczytaj najwyżej pierwsze 25 elementów bez paginacji",
    )
    options = parser.parse_args()
    try:
        from applemusic import AppleMusic, AppleMusicCredentials

        saved = AppleMusicCredentialStore(resolve_layout()).read()
        if saved is None:
            print("Apple Music: brak zapisanej sesji.")
            return 1
        client = AppleMusic(
            AppleMusicCredentials(
                saved.developer_token,
                saved.media_user_token,
                saved.cookie_header,
            ),
            language="pl-PL",
            max_retries=0,
        )
        try:
            if options.first_page:
                return 0 if diagnose_first_pages(client) else 1
            regular_ok = diagnose_client(client)
            if regular_ok:
                return 0
            return 0 if diagnose_library_parameters(client) else 1
        finally:
            client.close()
    except Exception:
        print("Diagnostyka Apple Music nie mogła zostać uruchomiona.")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
