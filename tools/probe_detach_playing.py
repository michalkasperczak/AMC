#!/usr/bin/env python3
"""PUNKT B: czy queue.status klamie ``playing=true`` po odlaczeniu kolejki.

Kolejka prowadzi odtwarzanie tylko dopoki ``_leading``. Po ``files.play``
(bezposredni plik) albo po radiu kolejka JEST odlaczona -- ale ``BuildStatus``
oddaje ``session.IsPlaying`` bez sprawdzenia, czy kolejka wciaz prowadzi.
Frontend czyta ``playing`` z ``queue.status`` i na tej podstawie ustawia
etykiete przycisku: klamstwo tu znaczy przycisk "Wstrzymaj" dla kolejki, ktora
nie gra.

Mierzymy STAN, nie wiersze: wiersze i pozycja maja zostac nietkniete.
"""
from __future__ import annotations

import json
import os
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from probe_transport_requests import Host, make_wav  # noqa: E402

HOST = sys.argv[1] if len(sys.argv) > 1 else ""
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("detach-playing-receipt.json")


def main() -> int:
    if not HOST or not Path(HOST).exists():
        print(f"BRAK HOSTA: {HOST}")
        return 3

    folder = Path(os.environ.get("AMC_PROBE_DIR", os.environ.get("TEMP", "/tmp")))
    folder.mkdir(parents=True, exist_ok=True)
    win_prefix = os.environ.get("AMC_PROBE_WINDIR", str(folder)).rstrip("\\")
    names = ["B.wav", "A.wav", "C.wav", "DIRECT.wav"]
    for name in names:
        make_wav(folder / name, seconds=6.0)

    def win(name: str) -> str:
        return win_prefix + "\\" + name

    items = [
        {"id": "file:" + win(n), "title": n[:-4], "path": win(n), "isInQueue": True}
        for n in names[:3]
    ]
    order = [i["id"] for i in items]
    checks: list[dict] = []

    def check(name: str, ok: bool, detail: str) -> None:
        checks.append({"point": "B", "name": name, "ok": bool(ok), "detail": detail})
        print(("OK   " if ok else "BLAD ") + f"[B] {name}: {detail}")

    host = Host(HOST, str(folder))
    try:
        host.call("host.hello")
        host.call("queue.set", {"sessionId": "detach", "items": items, "order": order})
        host.call("queue.playAt", {"itemId": order[0], "volume": 0, "rate": 1.0})
        playing = host.call("queue.status")
        check(
            "kolejka prowadzaca odtwarzanie mowi playing=true",
            playing.get("playing") is True,
            f"playing={playing.get('playing')!r} current={playing.get('currentId')!r}",
        )
        rows_before = [r.get("id") for r in (playing.get("rows") or [])]

        # ---- odlaczenie przez BEZPOSREDNI plik (files.play) --------------
        host.call("files.play", {"path": win("DIRECT.wav"), "volume": 0, "rate": 1.0})
        after_direct = host.call("queue.status")
        check(
            "po files.play kolejka NIE twierdzi, ze gra",
            after_direct.get("playing") is False,
            f"playing={after_direct.get('playing')!r}",
        )
        rows_after = [r.get("id") for r in (after_direct.get("rows") or [])]
        check(
            "odlaczenie NIE skasowalo wierszy kolejki",
            rows_after == rows_before,
            f"przed={rows_before!r} po={rows_after!r}",
        )
        check(
            "odlaczenie NIE skasowalo biezacej pozycji",
            after_direct.get("currentId") == playing.get("currentId"),
            f"currentId={after_direct.get('currentId')!r}",
        )
        check(
            "odlaczenie NIE zgubilo stanu wczytania",
            after_direct.get("initialized") is True,
            f"initialized={after_direct.get('initialized')!r}",
        )

        failures = [c for c in checks if not c["ok"]]
        OUT.parent.mkdir(parents=True, exist_ok=True)
        OUT.write_text(
            json.dumps(
                {"host": HOST, "checks": checks, "failed": len(failures), "total": len(checks)},
                indent=2,
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        print(f"\nKWIT={OUT}")
        print(f"WYNIK: {len(checks) - len(failures)}/{len(checks)} zdanych")
        return 1 if failures else 0
    finally:
        host.close()


if __name__ == "__main__":
    sys.exit(main())
