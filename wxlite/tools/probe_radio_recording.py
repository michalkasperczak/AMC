"""End-to-end probe of the wxPython radio-recording host contract.

The probe creates a synthetic local MP3 stream, serves it over loopback,
starts the real LiteHost, records a short fragment, stops it and verifies the
status, terminal event, history and the resulting file.  It never touches the
user's AMC profile or recording folders.
"""

from __future__ import annotations

import argparse
import http.server
import json
import subprocess
import sys
import tempfile
import threading
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path


WXLITE_ROOT = Path(__file__).resolve().parents[1]
if str(WXLITE_ROOT) not in sys.path:
    sys.path.insert(0, str(WXLITE_ROOT))

from amc_wx_lite.host_client import LiteHostClient  # noqa: E402
from amc_wx_lite.radio_source import SCOPE_LIBRARY, stations_from_amc_state  # noqa: E402


DOTNET_UNIX_EPOCH_TICKS = 621_355_968_000_000_000


class _StreamingHandler(http.server.BaseHTTPRequestHandler):
    source: bytes = b""

    def log_message(self, _format: str, *_args: object) -> None:
        return

    def do_GET(self) -> None:  # noqa: N802 - nazwa narzucona przez stdlib
        if self.path != "/probe.mp3":
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", "audio/mpeg")
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Connection", "close")
        self.send_header("icy-name", "Lokalna próba nagrywania")
        self.end_headers()
        # Brak Content-Length jest zamierzony: host ma zobaczyc radio na zywo,
        # a nie skonczony plik, ktory niektore dekodery najpierw pobieraja caly.
        try:
            while True:
                for offset in range(0, len(self.source), 4096):
                    self.wfile.write(self.source[offset : offset + 4096])
                    self.wfile.flush()
                    time.sleep(0.15)
        except (BrokenPipeError, ConnectionResetError, OSError):
            return


def _wait_for_event(
    condition: threading.Condition,
    events: list[tuple[str, dict]],
    names: set[str],
    timeout: float,
) -> tuple[str, dict]:
    deadline = time.monotonic() + timeout
    with condition:
        while True:
            for name, data in events:
                if name in names:
                    return name, data
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                seen = ", ".join(name for name, _data in events) or "brak"
                raise RuntimeError(
                    f"Nie otrzymano zdarzenia {sorted(names)}; odebrane: {seen}."
                )
            condition.wait(remaining)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", required=True, type=Path)
    parser.add_argument("--ffmpeg", type=Path)
    parser.add_argument("--state-json", type=Path)
    parser.add_argument("--work-dir", required=True, type=Path)
    parser.add_argument(
        "--format",
        default="Mp3",
        choices=("Mp3", "Aac", "Flac", "Wav", "Original"),
    )
    parser.add_argument(
        "--schedule",
        action="store_true",
        help="sprawdź wykonanie planu zamiast ręcznego Ctrl+R",
    )
    args = parser.parse_args()

    host = args.host.resolve()
    work_dir = args.work_dir.resolve()
    if not host.is_file():
        raise FileNotFoundError(f"Nie znaleziono hosta: {host}")
    if args.state_json is None and args.ffmpeg is None:
        parser.error("podaj --state-json albo --ffmpeg")
    ffmpeg = args.ffmpeg.resolve() if args.ffmpeg else None
    if ffmpeg is not None and not ffmpeg.is_file():
        raise FileNotFoundError(f"Nie znaleziono FFmpeg: {ffmpeg}")
    work_dir.mkdir(parents=True, exist_ok=True)

    events: list[tuple[str, dict]] = []
    condition = threading.Condition()

    def on_event(name: str, data: dict) -> None:
        with condition:
            events.append((name, data))
            condition.notify_all()

    with tempfile.TemporaryDirectory(prefix="radio-recording-", dir=work_dir) as raw:
        root = Path(raw)
        output_dir = root / "output"
        output_dir.mkdir()
        server: http.server.ThreadingHTTPServer | None = None
        server_thread: threading.Thread | None = None
        if args.state_json is not None:
            state_path = args.state_json.resolve()
            raw = json.loads(state_path.read_text(encoding="utf-8"))
            stations, _current = stations_from_amc_state(raw, scope=SCOPE_LIBRARY)
            station = next(
                (
                    item
                    for item in stations
                    if item.url.startswith(("http://", "https://"))
                    and "youtube.com" not in item.url.casefold()
                    and "youtu.be" not in item.url.casefold()
                ),
                None,
            )
            if station is None:
                raise RuntimeError("Profil nie zawiera zwykłej stacji HTTP do próby.")
            station_id = station.id
            station_name = station.name
            url = station.url
        else:
            assert ffmpeg is not None
            source = root / "probe.mp3"
            subprocess.run(
                [
                    str(ffmpeg),
                    "-hide_banner",
                    "-loglevel",
                    "error",
                    "-f",
                    "lavfi",
                    "-i",
                    "sine=frequency=440:sample_rate=44100:duration=30",
                    "-codec:a",
                    "libmp3lame",
                    "-b:a",
                    "192k",
                    "-y",
                    str(source),
                ],
                check=True,
            )
            _StreamingHandler.source = source.read_bytes()
            server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), _StreamingHandler)
            server_thread = threading.Thread(target=server.serve_forever, daemon=True)
            server_thread.start()
            station_id = "lokalna-proba-nagrywania"
            station_name = "Lokalna próba nagrywania"
            url = f"http://127.0.0.1:{server.server_port}/{source.name}"

        client = LiteHostClient(host, on_event=on_event)
        try:
            client.start()
            recording_request = {
                "stationId": station_id,
                "stationName": station_name,
                "url": url,
                "format": args.format,
                "bitrateKbps": 192,
                "folder": str(output_dir),
            }
            if args.schedule:
                start_utc = datetime.now(timezone.utc) + timedelta(seconds=3)
                start_ticks = (
                    int(start_utc.timestamp() * 10_000_000)
                    + DOTNET_UNIX_EPOCH_TICKS
                )
                schedule_id = "proba-harmonogramu"
                synced = client.sync_radio_schedules({
                    "schedules": [{
                        "id": schedule_id,
                        "name": "Próba harmonogramu",
                        "stationId": station_id,
                        "stationName": station_name,
                        "streamUrl": url,
                        "nextStartUtcTicks": start_ticks,
                        "timeZoneId": "",
                        "durationMinutes": 1,
                        "segmentMinutes": 0,
                        "recurrence": "Once",
                        "activeDays": [],
                        "outputFolder": str(output_dir),
                        "fileNameTemplate": "Próba harmonogramu - {data} {czas}",
                        "recordingFormat": args.format,
                        "recordingBitrateKbps": 192,
                        "wakeComputer": False,
                        "enabled": True,
                    }],
                    "defaultFolder": str(output_dir),
                    "folderPreset": "radio",
                    "stationFolders": {},
                    "recordingFormat": args.format,
                    "recordingBitrateKbps": 192,
                    "wakeScheduledRecordings": False,
                })
                schedules = synced.get("schedules") or []
                if not schedules or schedules[0].get("id") != schedule_id:
                    raise RuntimeError(f"Host nie przyjął harmonogramu: {synced!r}")
                scheduled_name, _scheduled_event = _wait_for_event(
                    condition,
                    events,
                    {"radio.recordingScheduled", "radio.recordingFailed"},
                    15.0,
                )
                if scheduled_name == "radio.recordingFailed":
                    raise RuntimeError("Plan nie rozpoczął wykonania.")
            else:
                started = client.toggle_radio_recording(recording_request)
                if started.get("action") != "starting":
                    raise RuntimeError(f"Host nie przyjął startu: {started!r}")

            name, start_event = _wait_for_event(
                condition,
                events,
                {"radio.recordingStarted", "radio.recordingFailed"},
                45.0,
            )
            if name == "radio.recordingFailed":
                raise RuntimeError(
                    "Host odrzucił nagranie: "
                    + str(start_event.get("error") or "brak opisu")
                )
            status = client.radio_recording_status()
            rows = status.get("recordings") or []
            if not rows or rows[0].get("state") not in ("recording", "paused"):
                raise RuntimeError(f"Nieprawidłowy stan aktywnego nagrania: {status!r}")

            time.sleep(2.0)
            if args.schedule:
                stopping = client.stop_all_radio_recordings()
                if stopping.get("stopping") != 1:
                    raise RuntimeError(
                        f"Host nie przyjął zatrzymania planu: {stopping!r}"
                    )
            else:
                stopping = client.toggle_radio_recording(recording_request)
                if stopping.get("action") != "stopping":
                    raise RuntimeError(f"Host nie przyjął zatrzymania: {stopping!r}")

            _terminal_name, terminal = _wait_for_event(
                condition,
                events,
                {
                    "radio.recordingStopped",
                    "radio.recordingFinished",
                    "radio.recordingFailed",
                },
                45.0,
            )
            if _terminal_name == "radio.recordingFailed":
                raise RuntimeError(
                    "Nagranie zakończyło się błędem: "
                    + str(terminal.get("error") or "brak opisu")
                )
            recording_path = Path(str(terminal.get("path") or start_event.get("path") or ""))
            if not recording_path.is_file() or recording_path.stat().st_size <= 0:
                raise RuntimeError(f"Nie utworzono poprawnego pliku: {recording_path}")

            final_status = client.radio_recording_status()
            if final_status.get("count") != 0:
                raise RuntimeError(f"Nagranie pozostało aktywne: {final_status!r}")
            history = client.radio_recording_history()
            history_rows = history.get("recordings") or []
            if not history_rows or history_rows[0].get("stationName") != station_name:
                raise RuntimeError(f"Brak wyniku w historii: {history!r}")
            if args.schedule:
                schedule_status = client.radio_schedule_status()
                schedule_rows = schedule_status.get("schedules") or []
                if not schedule_rows or schedule_rows[0].get("enabled") is not False:
                    raise RuntimeError(
                        "Jednorazowy plan nie został wyłączony po wykonaniu: "
                        f"{schedule_status!r}"
                    )

            print(
                "OK: start, stan aktywny, zatrzymanie, historia i plik; "
                f"tryb={'harmonogram' if args.schedule else 'ręczny'}, "
                f"format={args.format}, bajty={recording_path.stat().st_size}"
            )
        finally:
            client.close()
            if server is not None:
                server.shutdown()
                server.server_close()
            if server_thread is not None:
                server_thread.join(timeout=2.0)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
