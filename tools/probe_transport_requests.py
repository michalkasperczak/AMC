#!/usr/bin/env python3
"""Mierzy ZADANIA PROTOKOLU na PRAWDZIWYM hoscie amc_lite_host.exe.

Bez GUI i bez NVDA: host jest bezokienny, rozmawiamy z nim przez stdin/stdout
dokladnie tak, jak robi to frontend wxPython.

Pliki wejsciowe to ZYWE WAV-y z cisza (audio zero): dekoder je naprawde otwiera
i naprawde konczy, wiec przejscia miedzy utworami sa prawdziwe. Niczego nie
trzeba odsluchiwac -- werdykt bierzemy z pol odpowiedzi, nie z ucha.

Mierzone punkty zgloszenia:
  1. volume/rate z queue.playAt docieraja do transportu i trwaja
  2. transport.stop faktycznie zatrzymuje, elementy zostaja w kolejce
  3. po files.play / radio.play kolejka nie wraca; swiadomy queue.playAt wraca
  4. transport.status.durationSeconds po queue.playAt i po przejsciu
  6. queue.status.initialized: false bez kolejki, true po Set takze gdy rows=[]
"""
from __future__ import annotations

import json
import os
import struct
import subprocess
import sys
import time
import wave
from pathlib import Path

HOST = sys.argv[1] if len(sys.argv) > 1 else ""
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("transport-host-receipt.json")

# Dlugosc tak dobrana, zeby utwor faktycznie sie skonczyl w czasie testu,
# ale nie przeciagal przebiegu.
SECONDS = 2.0
RATE = 44100


def make_wav(path: Path, seconds: float = SECONDS) -> None:
    """Zywy, poprawny WAV z cisza. Audio zero, ale struktura prawdziwa."""
    frames = int(RATE * seconds)
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(2)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(struct.pack("<" + "h" * (frames * 2), *([0] * (frames * 2))))


class Host:
    def __init__(self, exe: str, workdir: str):
        self.proc = subprocess.Popen(
            [exe],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            cwd=workdir,
            text=False,
        )
        self.seq = 0
        self.events: list[dict] = []

    def call(self, op: str, args: dict | None = None, timeout: float = 30.0) -> dict:
        self.seq += 1
        rid = str(self.seq)
        line = json.dumps({"id": rid, "op": op, "args": args or {}}, ensure_ascii=False)
        assert self.proc.stdin is not None and self.proc.stdout is not None
        self.proc.stdin.write((line + "\n").encode("utf-8"))
        self.proc.stdin.flush()
        deadline = time.time() + timeout
        while time.time() < deadline:
            raw = self.proc.stdout.readline()
            if not raw:
                raise RuntimeError(f"host zamknal stdout przy {op}")
            msg = json.loads(raw.decode("utf-8"))
            # Zdarzenia (playback.*) nie sa odpowiedziami -- zbieramy je osobno.
            if msg.get("id") != rid:
                self.events.append(msg)
                continue
            # Protokol nie ma pola "ok": sukces to obecnosc "result",
            # a blad to obiekt "error" z kodem i komunikatem.
            if "error" in msg:
                err = msg["error"]
                raise RuntimeError(f"{op} -> {err.get('code')}: {err.get('message')}")
            return msg.get("result") or {}
        raise TimeoutError(f"brak odpowiedzi na {op}")

    def close(self) -> None:
        try:
            if self.proc.stdin:
                self.proc.stdin.close()
            self.proc.wait(timeout=10)
        except Exception:
            self.proc.kill()
            self.proc.wait(timeout=10)


def main() -> int:
    if not HOST or not Path(HOST).exists():
        print(f"BRAK HOSTA: {HOST}")
        return 3

    folder = Path(os.environ.get("AMC_PROBE_DIR", os.environ.get("TEMP", "/tmp")))
    folder.mkdir(parents=True, exist_ok=True)
    # Host jest procesem WINDOWS: sciezki w zadaniach musza byc windowsowe,
    # nawet gdy sonda dziala w WSL i zapisuje pliki przez /mnt/c.
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
        hello = host.call("host.hello")

        # ---- PUNKT 6: brak kolejki = initialized false --------------------
        st = host.call("queue.status")
        check("6", "bez kolejki initialized=false",
              st.get("initialized") is False,
              f"initialized={st.get('initialized')!r} rows={len(st.get('rows') or [])}")

        # ---- PUNKT 1: volume/rate z zadania ------------------------------
        host.call("queue.set", {"sessionId": "probe", "items": items, "order": order})
        st = host.call("queue.status")
        check("6", "po queue.set initialized=true",
              st.get("initialized") is True, f"initialized={st.get('initialized')!r}")

        host.call("queue.playAt", {"itemId": order[0], "volume": 0, "rate": 1.5})
        time.sleep(0.6)
        ts = host.call("transport.status")
        check("1", "volume 0 doszlo do transportu", ts.get("volume") == 0,
              f"volume={ts.get('volume')!r}")
        check("1", "rate 1.5 doszlo do transportu",
              abs(float(ts.get("rate") or 0) - 1.5) < 0.01, f"rate={ts.get('rate')!r}")

        # ---- PUNKT 4: duration po queue.playAt ---------------------------
        check("4", "durationSeconds po queue.playAt",
              float(ts.get("durationSeconds") or 0) > 0.5,
              f"durationSeconds={ts.get('durationSeconds')!r}")
        check("4", "biezacy utwor to ten zadany", ts.get("id") == order[0],
              f"id={ts.get('id')!r}")

        # ---- PUNKT 1: zmiana w trakcie i jej trwalosc --------------------
        host.call("transport.setVolume", {"volume": 70})
        host.call("transport.setRate", {"rate": 0.75})
        time.sleep(0.3)
        ts = host.call("transport.status")
        check("1", "zmiana glosnosci w trakcie", ts.get("volume") == 70,
              f"volume={ts.get('volume')!r}")
        check("1", "zmiana tempa w trakcie",
              abs(float(ts.get("rate") or 0) - 0.75) < 0.01, f"rate={ts.get('rate')!r}")

        # ---- PUNKT 1+4: naturalny koniec utworu --------------------------
        # Czekamy na PRAWDZIWY koniec 2-sekundowego WAV-a.
        deadline = time.time() + 20
        moved_to = None
        while time.time() < deadline:
            time.sleep(0.4)
            ts = host.call("transport.status")
            if ts.get("id") and ts.get("id") != order[0]:
                moved_to = ts.get("id")
                break
        check("1", "naturalne przejscie nastapilo", moved_to == order[1],
              f"id={moved_to!r} (oczekiwano {order[1]!r})")
        if moved_to:
            check("1", "glosnosc 70 przetrwala przejscie", ts.get("volume") == 70,
                  f"volume={ts.get('volume')!r}")
            check("1", "tempo 0.75 przetrwalo przejscie",
                  abs(float(ts.get("rate") or 0) - 0.75) < 0.01, f"rate={ts.get('rate')!r}")
            # Dlugosc podaje DEKODER zdarzeniem po otwarciu strumienia, wiec po
            # samym przejsciu moze jej jeszcze nie byc. Czekamy OGRANICZONY czas
            # i mierzymy, ile zajela -- brak po tym limicie to prawdziwy blad,
            # a nie niecierpliwosc sondy.
            start = time.time()
            dur = 0.0
            dur_id = None
            while time.time() - start < 6.0:
                ts2 = host.call("transport.status")
                dur = float(ts2.get("durationSeconds") or 0)
                dur_id = ts2.get("id")
                if dur > 0.5 and dur_id == moved_to:
                    break
                time.sleep(0.25)
            waited = round(time.time() - start, 2)
            check("4", "durationSeconds po naturalnym przejsciu",
                  dur > 0.5 and dur_id == moved_to,
                  f"durationSeconds={dur!r} id={dur_id!r} czekano={waited}s")

        # ---- PUNKT 2: stop naprawde zatrzymuje ---------------------------
        host.call("queue.playAt", {"itemId": order[0], "volume": 40, "rate": 1.0})
        time.sleep(0.5)
        # Ile wierszy JEST tuz przed zatrzymaniem: czesc utworow zuzyl juz
        # naturalny koniec wyzej, wiec 3 byloby falszywym zalozeniem.
        rows_before_stop = len(host.call("queue.status").get("rows") or [])
        stopped = host.call("transport.stop")
        time.sleep(0.3)
        ts = host.call("transport.status")
        qs = host.call("queue.status")
        # transport.status nie ma pola playing -- dowodem zatrzymania jest
        # brak wczytanego materialu w silniku (loadedId) i cisza w kolejce.
        check("2", "po stop silnik nie ma wczytanego materialu",
              ts.get("loadedId") in (None, ""), f"loadedId={ts.get('loadedId')!r}")
        check("2", "po stop kolejka nie twierdzi ze gra", qs.get("playing") is not True,
              f"queue.playing={qs.get('playing')!r}")
        check("2", "zatrzymanie nie usuwa elementow",
              len(qs.get("rows") or []) == rows_before_stop,
              f"rows przed={rows_before_stop} po={len(qs.get('rows') or [])}")
        check("2", "kolejka nadal wczytana", qs.get("initialized") is True,
              f"initialized={qs.get('initialized')!r}")

        # ---- PUNKT 3: files.play odcina kolejke --------------------------
        host.call("queue.playAt", {"itemId": order[0], "volume": 30, "rate": 1.0})
        time.sleep(0.4)
        # files.play TEGO SAMEGO Id: najtrudniejszy przypadek, bo Id sie zgadza.
        host.call("files.play", {"itemId": order[0], "path": items[0]["path"],
                                 "title": "B", "volume": 30})
        time.sleep(0.4)
        before = host.call("transport.status")
        # Czekamy przez caly czas trwania utworu: kolejka NIE MA prawa ruszyc.
        time.sleep(4.0)
        after = host.call("transport.status")
        check("3", "po files.play tego samego Id kolejka nie przeskakuje",
              after.get("id") in (None, order[0]),
              f"id przed={before.get('id')!r} po={after.get('id')!r}")

        # radio.play tez musi odciac (dotad nie odcinalo)
        host.call("queue.playAt", {"itemId": order[0], "volume": 30, "rate": 1.0})
        time.sleep(0.4)
        radio_detached = None
        try:
            host.call("radio.play", {"stationId": "probe", "title": "cisza",
                                     "url": "http://127.0.0.1:9/nic", "volume": 10})
        except RuntimeError as exc:
            # Stacja nie istnieje -- to normalne. Liczy sie, czy kolejka zostala
            # odcieta, a nie czy radio zagralo.
            radio_detached = str(exc)[:80]
        time.sleep(4.0)
        ts = host.call("transport.status")
        check("3", "po radio.play kolejka nie prowadzi dalej",
              ts.get("id") != order[1],
              f"id={ts.get('id')!r} radio={radio_detached!r}")

        # swiadomy queue.playAt PRZYWRACA prowadzenie
        host.call("queue.playAt", {"itemId": order[0], "volume": 30, "rate": 1.0})
        time.sleep(0.5)
        deadline = time.time() + 20
        resumed = None
        while time.time() < deadline:
            time.sleep(0.4)
            ts = host.call("transport.status")
            if ts.get("id") and ts.get("id") != order[0]:
                resumed = ts.get("id")
                break
        check("3", "swiadomy queue.playAt przywraca prowadzenie", resumed == order[1],
              f"id={resumed!r}")

        host.call("transport.stop")

        # ---- PUNKT 6: initialized po zuzyciu kolejki --------------------
        qs = host.call("queue.status")
        check("6", "initialized trwa po uzyciu kolejki", qs.get("initialized") is True,
              f"initialized={qs.get('initialized')!r} rows={len(qs.get('rows') or [])}")

        # ODRZUCONY Set nie ma prawa ustawic initialized. Pusta tablica items jest
        # POPRAWNYM zadaniem (pusta kolejka to tez kolejka), wiec zeby zmierzyc
        # odmowe, wysylamy wsad NIEPOPRAWNY: pozycje bez wymaganego "id".
        fresh = Host(HOST, str(folder))
        try:
            fresh.call("host.hello")
            failed = None
            try:
                fresh.call("queue.set", {"sessionId": "x",
                                         "items": [{"title": "bez id"}], "order": []})
            except RuntimeError as exc:
                failed = str(exc)[:100]
            qs2 = fresh.call("queue.status")
            check("6", "odrzucony queue.set nie ustawia initialized",
                  failed is not None and qs2.get("initialized") is False,
                  f"initialized={qs2.get('initialized')!r} odmowa={failed!r}")

            # A poprawny PUSTY wsad wczytuje kolejke: to nie to samo.
            fresh.call("queue.set", {"sessionId": "x", "items": [], "order": []})
            qs3 = fresh.call("queue.status")
            check("6", "poprawny pusty queue.set ustawia initialized",
                  qs3.get("initialized") is True and len(qs3.get("rows") or []) == 0,
                  f"initialized={qs3.get('initialized')!r} rows={len(qs3.get('rows') or [])}")
        finally:
            fresh.close()

        failures = [c for c in checks if not c["ok"]]
        receipt = {
            "host": HOST,
            "hello": hello,
            "wav": {"seconds": SECONDS, "rate": RATE, "channels": 2,
                    "note": "audio zero (cisza), struktura prawdziwa"},
            "checks": checks,
            "failed": len(failures),
            "total": len(checks),
        }
        OUT.write_text(json.dumps(receipt, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"\nKWIT={OUT}")
        print(f"WYNIK: {len(checks) - len(failures)}/{len(checks)} zdanych")
        return 1 if failures else 0
    finally:
        host.close()


if __name__ == "__main__":
    sys.exit(main())
