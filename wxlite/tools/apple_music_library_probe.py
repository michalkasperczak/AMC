#!/usr/bin/env python3
"""Explicit laboratory probe for AMC Test Apple Music account setup.

Without ``--login`` the probe performs a local status check only.  It never
prints credentials, provider dictionaries, paths or exception details.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import sys


WXLITE = Path(__file__).resolve().parent.parent
if str(WXLITE) not in sys.path:
    sys.path.insert(0, str(WXLITE))

from amc_wx_lite.apple_music_account import (  # noqa: E402
    AppleMusicAccountError,
    connect_account,
    stored_account_status,
)
from amc_wx_lite.profile_layout import resolve_layout  # noqa: E402


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Bezpieczna sonda konta Apple Music wyłącznie dla AMC Test."
    )
    parser.add_argument(
        "--login",
        action="store_true",
        help="jawnie zezwól na otwarcie widocznego okna logowania Apple",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    reconfigure = getattr(sys.stdout, "reconfigure", None)
    if callable(reconfigure):
        reconfigure(encoding="utf-8")
    options = build_parser().parse_args(argv)
    layout = resolve_layout()
    try:
        status = (
            connect_account(layout)
            if options.login
            else stored_account_status(layout)
        )
    except AppleMusicAccountError as error:
        print(str(error))
        return 1
    print(status.message)
    if not options.login:
        print("Sonda lokalna nie otworzyła przeglądarki ani połączenia z Apple.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
