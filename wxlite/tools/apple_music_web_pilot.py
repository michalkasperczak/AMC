#!/usr/bin/env python3
"""Manual, visible AMC Test WebView2 playback probe with guaranteed teardown.

The operator sends one fixed command per line. No credentials, page scripts,
cookies, URLs or personal library responses cross the helper pipe.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys


WXLITE = Path(__file__).resolve().parent.parent
if str(WXLITE) not in sys.path:
    sys.path.insert(0, str(WXLITE))

from amc_wx_lite.apple_music_web import (  # noqa: E402
    AppleMusicWebClient,
    AppleMusicWebUnavailable,
)


VISIBLE_FIELDS = (
    "handled", "message", "loginRequired", "hasSession", "title", "artist",
    "songId", "isPlaying", "positionSeconds", "durationSeconds",
)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Widoczna próba WebView2 AMC Test")
    parser.add_argument("--helper", required=True, type=Path)
    args = parser.parse_args(argv)
    client = AppleMusicWebClient(args.helper)
    print("Próba gotowa: show, play NUMERYCZNE_ID, state, toggle, stop, quit.",
          flush=True)
    try:
        for raw in sys.stdin:
            words = raw.strip().split()
            if not words:
                continue
            command = words[0].lower()
            if command == "quit":
                break
            try:
                if command == "play" and len(words) == 2:
                    result = client.play(words[1])
                elif command in {"show", "state", "toggle", "stop"} and len(words) == 1:
                    result = client.transport(command)
                else:
                    print("Nieznane polecenie próby.", flush=True)
                    continue
                safe = {key: result[key] for key in VISIBLE_FIELDS if key in result}
                print(json.dumps(safe, ensure_ascii=False), flush=True)
            except AppleMusicWebUnavailable as error:
                print(str(error), flush=True)
    finally:
        if client.is_running:
            try:
                client.transport("stop")
            except AppleMusicWebUnavailable:
                pass
        client.close()
        print("Zamknięto pomocnika WebView2.", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
