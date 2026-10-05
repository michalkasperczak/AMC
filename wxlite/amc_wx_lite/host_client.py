"""Klient bezokiennego hosta AMC (``amc_lite_host.exe``).

Protokol: JSON-lines po stdin/stdout procesu potomnego.
  zadanie:    {"id": 1, "op": "files.play", "args": {...}}
  odpowiedz:  {"id": 1, "result": {...}}  albo  {"id": 1, "error": {"message": "..."}}
  zdarzenie:  {"event": "playback.started", "data": {...}}

Dlaczego proces potomny, a nie port: nikt z sieci nie moze sterowac
odtwarzaniem ani czytac sciezek. Host umiera razem z frontendem (EOF na stdin).

stdout to WYLACZNIE protokol; diagnostyka hosta idzie na stderr i trafia do
naszego logu, nie do parsera.
"""

from __future__ import annotations

import json
import os
import queue
import subprocess
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable


class HostError(RuntimeError):
    """Host odpowiedzial bledem (np. nie ma pliku). To NIE jest awaria procesu."""


class HostUnavailable(RuntimeError):
    """Hosta nie da sie uruchomic albo juz nie zyje."""


@dataclass(slots=True)
class Response:
    # Identyfikator jest NAPISEM, bo host C# zapisuje "id" jako string
    # (LiteJson.Serialize -> writer.WriteString("id", ...)). Trzymanie tu int
    # powodowalo, ze zadna odpowiedz nie trafiala do swojego zadania.
    request_id: str
    result: Any = None
    error: str | None = None


def decode_line(line: str) -> dict | None:
    """Jeden wiersz protokolu. Zly JSON POMIJAMY - nie wywracamy frontendu
    tylko dlatego, ze w strumieniu pojawil sie smiec."""
    line = line.strip()
    if not line:
        return None
    try:
        message = json.loads(line)
    except json.JSONDecodeError:
        return None
    return message if isinstance(message, dict) else None


def classify(message: dict) -> tuple[str, Any]:
    """Rozpoznaj rodzaj komunikatu: ``("response", Response)`` albo
    ``("event", (nazwa, dane))``, albo ``("ignore", None)``."""
    if "event" in message:
        return "event", (str(message["event"]), message.get("data") or {})
    raw_id = message.get("id")
    # Host zapisuje identyfikator jako napis; przyjmujemy tez liczbe, zeby
    # klient byl zgodny takze z prostszymi implementacjami protokolu.
    if isinstance(raw_id, (str, int)) and not isinstance(raw_id, bool):
        request_id = str(raw_id)
        error = message.get("error")
        if isinstance(error, dict):
            return "response", Response(request_id, error=str(error.get("message") or "Blad hosta"))
        if error is not None:
            return "response", Response(request_id, error=str(error))
        return "response", Response(request_id, result=message.get("result"))
    return "ignore", None


class LiteHostClient:
    """Rozmowa z hostem. Wywolania ``call`` sa synchroniczne z limitem czasu,
    zdarzenia ida do ``on_event`` z WATKU CZYTAJACEGO (okno przerzuca je
    do GUI przez wx.CallAfter)."""

    def __init__(
        self,
        executable: str | Path,
        *,
        timeshift_minutes: int = 30,
        profile_dir: str | Path | None = None,
        queue_write: bool = False,
        on_event: Callable[[str, dict], None] | None = None,
        on_stderr: Callable[[str], None] | None = None,
        spawn: Callable[..., Any] | None = None,
    ) -> None:
        self.executable = str(executable)
        self.timeshift_minutes = timeshift_minutes
        # TRWALOSC kolejki. Host zapisuje ja tylko wtedy, gdy dostanie JAWNA
        # sciezke wlasnej kopii profilu; ``queue_write`` jest osobna, swiadoma
        # zgoda na bycie jej pisarzem. Domyslnie oba sa puste, czyli host
        # zachowuje sie dokladnie jak dotad: kolejka zyje w pamieci procesu.
        self.profile_dir = str(profile_dir) if profile_dir else None
        # Samo ``--queue-write`` bez katalogu host i tak odrzuca
        # (``Program.cs`` -> ``LiteQueueStoreDenied``). Nie wysylamy polowy
        # kontraktu, zeby okno nie startowalo z gwarantowanym bledem.
        self.queue_write = bool(queue_write) and self.profile_dir is not None
        self._on_event = on_event
        self._on_stderr = on_stderr
        self._spawn = spawn or subprocess.Popen
        self._process: Any = None
        self._next_id = 0
        self._id_lock = threading.Lock()
        self._pending: dict[str, queue.Queue] = {}
        self._pending_lock = threading.Lock()
        self._reader: threading.Thread | None = None
        self._stderr_reader: threading.Thread | None = None
        self._closed = False
        self._lifecycle_lock = threading.RLock()

    # ------------------------------------------------------------ start/stop

    def _command(self) -> list[str]:
        """Pelne polecenie procesu hosta.

        Argumenty trwalosci dokladamy TYLKO wtedy, gdy wolajacy je podal.
        Zwykly start zostaje bajt w bajt taki jak dotad -- host bez
        ``--profile-dir`` nie dotyka zadnego profilu.
        """
        command = [self.executable, "--timeshift-minutes", str(self.timeshift_minutes)]
        if self.profile_dir is not None:
            command += ["--profile-dir", self.profile_dir]
            if self.queue_write:
                command.append("--queue-write")
        return command

    def start(self) -> None:
        with self._lifecycle_lock:
            if self._closed:
                raise HostUnavailable("Silnik został już zamknięty.")
            self._start_locked()

    def _start_locked(self) -> None:
        if self._process is not None:
            return
        if not Path(self.executable).exists():
            raise HostUnavailable(
                f"Nie znaleziono silnika: {self.executable}. "
                "Zbuduj go albo wskaz sciezke zmienna AMC_LITE_HOST."
            )
        creation = 0
        if os.name == "nt":
            # Bez tego Windows pokazuje czarne okno konsoli hosta.
            creation = getattr(subprocess, "CREATE_NO_WINDOW", 0)
        self._process = self._spawn(
            self._command(),
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            bufsize=1,
            creationflags=creation,
        )
        self._reader = threading.Thread(target=self._read_loop, daemon=True)
        self._reader.start()
        self._stderr_reader = threading.Thread(target=self._read_stderr, daemon=True)
        self._stderr_reader.start()

    def close(self) -> None:
        """Zamknij stdin: host konczy sie SAM na EOF (tak go napisalismy)."""
        with self._lifecycle_lock:
            self._closed = True
            process = self._process
        if process is None:
            return
        try:
            if process.stdin is not None:
                process.stdin.close()
        except OSError:
            pass
        try:
            process.wait(timeout=5)
        except Exception:
            try:
                process.kill()
            except Exception:
                pass
        # Obudz wszystkich czekajacych, zeby nie wisieli do konca limitu.
        with self._pending_lock:
            waiting = list(self._pending.values())
            self._pending.clear()
        for slot in waiting:
            slot.put(Response("", error="Silnik zostal zamkniety"))

    @property
    def alive(self) -> bool:
        return self._process is not None and self._process.poll() is None

    # ------------------------------------------------------------- czytanie

    def _read_loop(self) -> None:
        process = self._process
        if process is None or process.stdout is None:
            return
        for line in process.stdout:
            message = decode_line(line)
            if message is None:
                continue
            kind, payload = classify(message)
            if kind == "response":
                assert isinstance(payload, Response)
                with self._pending_lock:
                    slot = self._pending.pop(payload.request_id, None)
                if slot is not None:
                    slot.put(payload)
            elif kind == "event" and self._on_event is not None:
                name, data = payload
                try:
                    self._on_event(name, data)
                except Exception:
                    # Blad obslugi zdarzenia nie moze zabic watku czytajacego,
                    # bo stracilibysmy WSZYSTKIE kolejne odpowiedzi.
                    pass
        # stdout sie skonczyl: host odszedl. Odblokuj czekajacych.
        with self._pending_lock:
            waiting = list(self._pending.values())
            self._pending.clear()
        for slot in waiting:
            slot.put(Response("", error="Silnik przestal odpowiadac"))

    def _read_stderr(self) -> None:
        process = self._process
        if process is None or process.stderr is None:
            return
        for line in process.stderr:
            if self._on_stderr is not None:
                try:
                    self._on_stderr(line.rstrip())
                except Exception:
                    pass

    # ------------------------------------------------------------ wywolania

    def call(self, op: str, args: dict | None = None, *, timeout: float = 20.0) -> Any:
        """Wyslij zadanie i poczekaj na odpowiedz. ``HostError`` = blad tresci."""
        if self._closed or not self.alive:
            raise HostUnavailable("Silnik nie jest uruchomiony.")
        process = self._process
        if process is None or process.stdin is None:
            raise HostUnavailable("Silnik nie ma wejscia.")

        with self._id_lock:
            self._next_id += 1
            request_id = str(self._next_id)

        slot: queue.Queue = queue.Queue(maxsize=1)
        with self._pending_lock:
            self._pending[request_id] = slot

        payload = json.dumps(
            {"id": request_id, "op": op, "args": args or {}}, ensure_ascii=False
        )
        try:
            process.stdin.write(payload + "\n")
            process.stdin.flush()
        except (OSError, ValueError) as error:
            with self._pending_lock:
                self._pending.pop(request_id, None)
            raise HostUnavailable(f"Nie moge wyslac polecenia do silnika: {error}") from error

        try:
            response: Response = slot.get(timeout=timeout)
        except queue.Empty:
            with self._pending_lock:
                self._pending.pop(request_id, None)
            raise HostUnavailable(f"Silnik nie odpowiedzial w {timeout:.0f} s na {op}.") from None

        if response.error is not None:
            raise HostError(response.error)
        return response.result

    # ------------------------------------------------- wygodne skroty operacji

    def hello(self) -> Any:
        return self.call("host.hello")

    def list_folder(self, path: str) -> Any:
        return self.call("files.listFolder", {"path": path}, timeout=60.0)

    def play_file(
        self,
        path: str,
        *,
        volume: int,
        rate: float,
        title: str | None = None,
        position_seconds: float | None = None,
    ) -> Any:
        args: dict[str, Any] = {"path": path, "volume": volume, "rate": rate}
        if title:
            args["title"] = title
        if position_seconds:
            # Pozycja idzie TYM SAMYM wywolaniem. ``LiteEngineHandlers.cs:290``
            # czyta ``positionSeconds`` i podaje je do ``_files.Play``, a
            # rozruch strumienia zeruje czas (cs:309) -- osobny
            # ``transport.seek`` puszczony rownolegle zgubilby skok.
            args["positionSeconds"] = float(position_seconds)
        return self.call("files.play", args, timeout=60.0)

    def play_station(self, url: str, *, volume: int, item_id: str, title: str) -> Any:
        return self.call(
            "radio.play",
            {"url": url, "volume": volume, "id": item_id, "title": title},
            timeout=90.0,
        )

    def pause_resume(self) -> Any:
        return self.call("transport.pauseResume")

    def stop(self) -> Any:
        return self.call("transport.stop")

    def seek_by(self, seconds: float) -> Any:
        return self.call("transport.seek", {"deltaSeconds": seconds})

    def set_volume(self, volume: int) -> Any:
        return self.call("transport.setVolume", {"volume": volume})

    def set_rate(self, rate: float) -> Any:
        return self.call("transport.setRate", {"rate": rate})

    def status(self) -> Any:
        return self.call("transport.status", timeout=5.0)

    # ----------------------------------------------------------------- kolejka
    #
    # ZYWA kolejka hosta. Nastepstwo utworow liczy sesja Core po stronie C#;
    # Python tylko wczytuje kolejnosc i prosi o start/skok.

    def queue_set(
        self,
        items: list[dict[str, Any]],
        *,
        session_id: str = "local",
        order: list[str] | None = None,
    ) -> Any:
        """Wczytaj kolejnosc do kolejki hosta. NIC nie zaczyna grac.

        Duza kolejka moze przekroczyc limit zadania (64 KiB), dlatego wysylamy
        tylko pola, ktorych host potrzebuje do otwarcia materialu.

        ``order`` jest JAWNA kolejnoscia nastepstwa. Host przyjmuje jej brak
        (bierze wtedy kolejnosc ``items``), ale wolajacy, ktory zna kolejnosc
        widoku, podaje ja wprost -- inaczej kontrakt opiera sie na zbieznosci
        dwoch list zamiast na umowie.
        """
        payload: dict[str, Any] = {"sessionId": session_id, "items": items}
        if order is not None:
            payload["order"] = order
        return self.call("queue.set", payload, timeout=60.0)

    def queue_play_at(self, item_id: str, *, volume: int, rate: float) -> Any:
        """Zacznij kolejke od WSKAZANEJ pozycji (Enter na wierszu Ctrl+Q)."""
        return self.call(
            "queue.playAt",
            {"itemId": item_id, "volume": volume, "rate": rate},
            timeout=60.0,
        )

    def queue_next(self, *, volume: int, rate: float) -> Any:
        return self.call(
            "queue.next", {"volume": volume, "rate": rate}, timeout=60.0
        )

    def queue_previous(self, *, volume: int, rate: float) -> Any:
        return self.call(
            "queue.previous", {"volume": volume, "rate": rate}, timeout=60.0
        )

    def queue_status(self) -> Any:
        return self.call("queue.status", timeout=5.0)

    def import_playlist(self, path: str) -> Any:
        return self.call("radio.importPlaylist", {"path": path}, timeout=60.0)

    def configure_audio(self, **options: Any) -> Any:
        return self.call("audio.configure", options)


def default_host_path() -> Path:
    """Gdzie szukac silnika. Kolejnosc: zmienna srodowiskowa, obok programu,
    potem wynik buildu w drzewie zrodel."""
    override = os.environ.get("AMC_LITE_HOST")
    if override:
        return Path(override)

    here = Path(__file__).resolve()
    name = "amc_lite_host.exe" if os.name == "nt" else "amc_lite_host"
    candidates = [
        here.parent.parent / "host" / name,
        here.parent.parent.parent / "host" / name,
        here.parents[3]
        / "src"
        / "AccessibleMediaController.LiteHost"
        / "bin"
        / "Release"
        / "net8.0-windows10.0.19041.0"
        / "win-x64"
        / name,
    ]
    for candidate in candidates:
        if candidate.exists():
            return candidate
    return candidates[0]
