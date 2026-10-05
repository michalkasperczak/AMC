#!/usr/bin/env python3
"""PUNKT A: kolejnosc zatwierdzenia kolejki wobec zdarzenia konca utworu.

Mierzy PRAWDZIWA droge frontendu, nie obecnosc napisu w zrodle: frontend
(``gui.py`` -> ``_refresh_live_queue``) odpytuje ``queue.status`` NATYCHMIAST po
zdarzeniu ``playback.ended``. Jesli host oglasza koniec PRZED zatwierdzeniem
przejscia w sesji Core, odpowiedz niesie STARE wiersze -- i przy OSTATNIM
utworze kolejki nie przychodzi juz zadne ``queue.advanced``, wiec otwarta lista
zostaje z zuzytym wierszem na zawsze.

Dlatego mierzymy dwa razy:
  A1  koniec utworu W SRODKU kolejki: rows odczytane po ``playback.ended``
      nie moga zawierac zakonczonego Id,
  A2  koniec OSTATNIEGO utworu: rows odczytane po ``playback.ended`` musza byc
      puste (0), bo zadne dalsze zdarzenie listy juz nie odswiezy.

Uzywa klasy Host z istniejacej sondy transportu -- bez nowego harnessu.
"""
from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from probe_transport_requests import Host, make_wav  # noqa: E402

HOST = sys.argv[1] if len(sys.argv) > 1 else ""
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("end-order-receipt.json")


def wait_event(host: Host, name: str, timeout: float = 25.0) -> dict | None:
    """Czeka na ZDARZENIE hosta, nie na odpowiedz. Zwraca jego payload."""
    # Zdarzenia zebrane mimochodem przez wczesniejsze call() tez sie licza.
    for message in list(host.events):
        if message.get("event") == name:
            host.events.remove(message)
            return message
    deadline = time.time() + timeout
    assert host.proc.stdout is not None
    while time.time() < deadline:
        raw = host.proc.stdout.readline()
        if not raw:
            return None
        message = json.loads(raw.decode("utf-8"))
        if message.get("event") == name:
            return message
        host.events.append(message)
    return None


def main() -> int:
    if not HOST or not Path(HOST).exists():
        print(f"BRAK HOSTA: {HOST}")
        return 3

    folder = Path(os.environ.get("AMC_PROBE_DIR", os.environ.get("TEMP", "/tmp")))
    folder.mkdir(parents=True, exist_ok=True)
    win_prefix = os.environ.get("AMC_PROBE_WINDIR", str(folder))
    names = ["B.wav", "A.wav", "C.wav"]
    for name in names:
        make_wav(folder / name)

    def item(name: str) -> dict:
        p = win_prefix.rstrip("\\") + "\\" + name
        return {"id": "file:" + p, "title": name[:-4], "path": p, "isInQueue": True}

    items = [item(n) for n in names]
    order = [i["id"] for i in items]
    checks: list[dict] = []

    def check(point: str, name: str, ok: bool, detail: str) -> None:
        checks.append({"point": point, "name": name, "ok": bool(ok), "detail": detail})
        print(("OK   " if ok else "BLAD ") + f"[{point}] {name}: {detail}")

    host = Host(HOST, str(folder))
    try:
        host.call("host.hello")
        host.call("queue.set", {"sessionId": "endorder", "items": items, "order": order})
        host.call("queue.playAt", {"itemId": order[0], "volume": 0, "rate": 1.0})

        # ---- A1: koniec W SRODKU kolejki ---------------------------------
        ended = wait_event(host, "playback.ended")
        rows_mid = host.call("queue.status").get("rows") or []
        ids_mid = [r.get("id") for r in rows_mid]
        check(
            "A1",
            "po playback.ended zuzyty wiersz juz NIE jest w queue.status",
            ended is not None and order[0] not in ids_mid,
            f"ended={(ended or {}).get('data', {}).get('id')!r} rows={ids_mid!r}",
        )
        check(
            "A1",
            "zdarzenie zapowiada dalsze prowadzenie kolejki",
            bool((ended or {}).get("data", {}).get("queueContinues")),
            f"queueContinues={(ended or {}).get('data', {}).get('queueContinues')!r}",
        )

        # ---- A1b: drugi naturalny koniec (A), kolejka wchodzi na C -------
        # Zadnego queue.playAt: przejscie prowadzi SAM host, tak jak w GUI.
        ended_a = wait_event(host, "playback.ended")
        rows_a = host.call("queue.status").get("rows") or []
        ids_a = [r.get("id") for r in rows_a]
        check(
            "A1b",
            "naturalne B->A->C: po koncu A zostaje tylko C",
            ended_a is not None and ids_a == [order[2]],
            f"ended={(ended_a or {}).get('data', {}).get('id')!r} rows={ids_a!r}",
        )

        # ---- A2: koniec OSTATNIEGO utworu (C) ----------------------------
        ended_last = wait_event(host, "playback.ended")
        rows_last = host.call("queue.status").get("rows") or []
        check(
            "A2",
            "po koncu OSTATNIEGO utworu queue.status oddaje 0 wierszy",
            ended_last is not None and len(rows_last) == 0,
            f"rows={[r.get('id') for r in rows_last]!r}",
        )
        check(
            "A2",
            "koniec kolejki NIE zapowiada przejscia (okno ma powiedziec 'Koniec utworu')",
            (ended_last or {}).get("data", {}).get("queueContinues") is False,
            f"queueContinues={(ended_last or {}).get('data', {}).get('queueContinues')!r}",
        )
        status_last = host.call("queue.status")
        check(
            "A2",
            "pusta kolejka nadal jest WCZYTANA (nie wroci zapis profilu)",
            status_last.get("initialized") is True,
            f"initialized={status_last.get('initialized')!r}",
        )

        failures = [c for c in checks if not c["ok"]]
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
