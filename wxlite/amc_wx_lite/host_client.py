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

    def __init__(self, message: str, *, code: str | None = None) -> None:
        super().__init__(message)
        self.code = code


class HostUnavailable(RuntimeError):
    """Hosta nie da sie uruchomic albo juz nie zyje."""


def _hidden_process_options(
    platform_name: str | None = None,
    process_module: Any = subprocess,
) -> dict[str, Any]:
    """Opcje Windows, które nie pozwalają hostowi przejąć fokusu.

    ``CREATE_NO_WINDOW`` nie przydziela konsoli, a ``STARTUPINFO`` z
    ``SW_HIDE`` jest drugą, niezależną barierą dla krótkiego okna startowego.
    Zależności są parametrami wyłącznie po to, aby kontrakt dało się sprawdzić
    deterministycznie także poza Windows bez uruchamiania prawdziwego AMC.
    """

    if (platform_name or os.name) != "nt":
        return {}
    startup_info = process_module.STARTUPINFO()
    startup_info.dwFlags |= process_module.STARTF_USESHOWWINDOW
    startup_info.wShowWindow = process_module.SW_HIDE
    return {
        "creationflags": process_module.CREATE_NO_WINDOW,
        "startupinfo": startup_info,
    }


@dataclass(slots=True)
class Response:
    # Identyfikator jest NAPISEM, bo host C# zapisuje "id" jako string
    # (LiteJson.Serialize -> writer.WriteString("id", ...)). Trzymanie tu int
    # powodowalo, ze zadna odpowiedz nie trafiala do swojego zadania.
    request_id: str
    result: Any = None
    error: str | None = None
    error_code: str | None = None


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
            return "response", Response(
                request_id, error=str(error.get("message") or "Blad hosta"),
                error_code=error.get("code") if isinstance(error.get("code"), str) else None,
            )
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
        library_db: str | Path | None = None,
        podcasts_db: str | Path | None = None,
        state_json: str | Path | None = None,
        release_profile: str | None = None,
        youtube_client_id: str | None = None,
        youtube_client_secret_file: str | Path | None = None,
        youtube_token_broker: str | None = None,
        common_data_snapshot: str | Path | None = None,
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
        self.library_db = str(library_db) if library_db else None
        self.podcasts_db = str(podcasts_db) if podcasts_db else None
        self.state_json = str(state_json) if state_json else None
        self.release_profile = str(release_profile).strip() if release_profile else None
        self.youtube_client_id = (
            str(youtube_client_id).strip() if youtube_client_id else None
        )
        self.youtube_client_secret_file = (
            str(youtube_client_secret_file) if youtube_client_secret_file else None
        )
        self.youtube_token_broker = (
            str(youtube_token_broker).strip() if youtube_token_broker else None
        )
        self.common_data_snapshot = (
            str(common_data_snapshot) if common_data_snapshot else None
        )
        self._on_event = on_event
        self._on_stderr = on_stderr
        self._spawn = spawn or subprocess.Popen
        self._process: Any = None
        self._next_id = 0
        self._id_lock = threading.Lock()
        # Eksport/edycja dzialaja wspolbieznie ze statusem i anulowaniem.
        # Jeden zapis JSON-lines musi pozostac jednym calym wierszem; bez
        # zamka dwa watki moglyby przeplec write/flush i uszkodzic protokol.
        self._write_lock = threading.Lock()
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
        if self.library_db is not None:
            command += ["--library-db", self.library_db]
        if self.podcasts_db is not None:
            command += ["--podcasts-db", self.podcasts_db]
        if self.state_json is not None:
            command += ["--state-json", self.state_json]
        if self.release_profile is not None:
            command += ["--release-profile", self.release_profile]
        if self.youtube_client_id is not None:
            command += ["--youtube-client-id", self.youtube_client_id]
        if self.youtube_client_secret_file is not None:
            command += [
                "--youtube-client-secret-file",
                self.youtube_client_secret_file,
            ]
        if self.youtube_token_broker is not None:
            command += ["--youtube-token-broker", self.youtube_token_broker]
        if self.common_data_snapshot is not None:
            command += ["--common-data-snapshot", self.common_data_snapshot]
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
        self._process = self._spawn(
            self._command(),
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            # Host ustawia UTF-8 jawnie. ``replace`` jest ostatnia bariera na
            # wypadek, gdy obca biblioteka zapisze surowy bajt na stderr:
            # diagnostyka moze stracic jeden znak, ale watek czytajacy i cala
            # komunikacja z silnikiem nie moga przez to zginac.
            errors="replace",
            bufsize=1,
            **_hidden_process_options(),
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
        # Proces juz zakonczyl zapis do potokow. Poczekaj, az oba czytniki
        # odbiora EOF, zanim interpreter albo okno zwolni obiekty strumieni;
        # inaczej szybkie zamkniecie po anulowaniu potrafilo wypisac surowe
        # ``Exception in thread`` mimo poprawnego wyniku operacji.
        current_thread = threading.current_thread()
        for reader in (self._reader, self._stderr_reader):
            if reader is not None and reader is not current_thread:
                reader.join(timeout=1.0)
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
            with self._write_lock:
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
            raise HostError(response.error, code=response.error_code)
        return response.result

    # ------------------------------------------------- wygodne skroty operacji

    def hello(self) -> Any:
        return self.call("host.hello")

    def configure_nvda_bridge(self, window_handle: int) -> Any:
        """Zarejestruj wyłącznie uchwyt głównego okna dla jawnych poleceń
        dodatku NVDA otwierających widok. Potok i jego ACL pozostają w C#."""
        return self.call("nvda.configure", {"windowHandle": int(window_handle)})

    def reply_to_nvda(
        self, request_id: str, *, ok: bool, message: str
    ) -> Any:
        """Odpowiedz na jedno zweryfikowane żądanie lokalnego dodatku."""
        return self.call(
            "nvda.reply",
            {
                "requestId": str(request_id),
                "ok": bool(ok),
                "message": str(message)[:2000],
            },
            timeout=2.0,
        )

    def list_folder(self, path: str) -> Any:
        return self.call("files.listFolder", {"path": path}, timeout=60.0)

    def local_folder_sources(self) -> Any:
        return self.call("library.folderSources", timeout=15.0)

    def add_local_folder_source(self, path: str) -> Any:
        return self.call(
            "library.folderSource.add",
            {"path": path},
            timeout=3600.0,
        )

    def refresh_local_folder_sources(
        self,
        source_ids: list[str] | None = None,
    ) -> Any:
        return self.call(
            "library.folderSource.refresh",
            {"sourceIds": list(source_ids or [])},
            timeout=3600.0,
        )

    def detach_local_folder_source(self, source_id: str) -> Any:
        return self.call(
            "library.folderSource.detach",
            {"sourceId": source_id},
            timeout=30.0,
        )

    def play_file(
        self,
        path: str,
        *,
        item_id: str | None = None,
        volume: int,
        rate: float,
        title: str | None = None,
        position_seconds: float | None = None,
    ) -> Any:
        args: dict[str, Any] = {
            "path": path,
            "id": item_id or f"file:{path}",
            "volume": volume,
            "rate": rate,
        }
        if title:
            args["title"] = title
        if position_seconds is not None:
            # Pozycja idzie TYM SAMYM wywolaniem. ``LiteEngineHandlers.cs:290``
            # czyta ``positionSeconds`` i podaje je do ``_files.Play``, a
            # rozruch strumienia zeruje czas (cs:309) -- osobny
            # ``transport.seek`` puszczony rownolegle zgubilby skok.
            args["positionSeconds"] = float(position_seconds)
        return self.call("files.play", args, timeout=60.0)

    def configure_local_resume(
        self,
        remember: bool,
        *,
        local_resume_mode: int = 0,
        podcast_resume_mode: int = 0,
    ) -> Any:
        """Ustaw globalna i sesyjne warstwy polityki wznowienia."""
        return self.call(
            "local.resumeConfigure",
            {
                "remember": bool(remember),
                "localResumeMode": int(local_resume_mode),
                "podcastResumeMode": int(podcast_resume_mode),
            },
        )

    def checkpoint_local(self) -> Any:
        """Utrwal pozycje bezposrednio granego lokalnego pliku."""
        return self.call("local.checkpoint", timeout=10.0)

    def play_media(
        self,
        source: str,
        *,
        item_id: str,
        title: str,
        volume: int,
        rate: float,
        position_seconds: float = 0.0,
    ) -> Any:
        """Play a podcast/YouTube source while retaining its stable AMC Id."""
        return self.call(
            "media.play",
            {
                "source": source,
                "id": item_id,
                "title": title,
                "volume": volume,
                "rate": rate,
                "positionSeconds": max(0.0, float(position_seconds)),
            },
            timeout=120.0,
        )

    def play_station(self, url: str, *, volume: int, item_id: str, title: str) -> Any:
        return self.call(
            "radio.play",
            {"url": url, "volume": volume, "id": item_id, "title": title},
            timeout=90.0,
        )

    @staticmethod
    def _transport_args(session_id: str | None, **values: Any) -> dict[str, Any]:
        args = dict(values)
        if session_id:
            args["sessionId"] = session_id
        return args

    def pause_resume(self, session_id: str | None = None) -> Any:
        return self.call("transport.pauseResume", self._transport_args(session_id))

    def toggle_mute(self, session_id: str | None = None) -> Any:
        return self.call("transport.toggleMute", self._transport_args(session_id))

    def stop(self, session_id: str | None = None) -> Any:
        return self.call("transport.stop", self._transport_args(session_id))

    def seek_by(self, seconds: float, session_id: str | None = None) -> Any:
        return self.call(
            "transport.seek",
            self._transport_args(session_id, deltaSeconds=seconds),
        )

    def seek_to_position(self, seconds: float, session_id: str | None = None) -> Any:
        """Skok na BEZWZGLEDNA pozycje.

        Host juz to umie: LiteEngineHandlers.cs:655 czyta ``positionSeconds``
        obok ``deltaSeconds``, wiec skok procentowy nie wymaga nowej komendy
        protokolu -- procent przeliczamy na sekundy po stronie klienta.
        """
        return self.call(
            "transport.seek",
            self._transport_args(
                session_id,
                positionSeconds=max(0.0, float(seconds)),
            ),
        )

    def radio_jump_live(self) -> Any:
        """Wróć z bufora Time Shift do żywej transmisji i normalnego tempa."""

        return self.call("radio.jumpLive", {"sessionId": "radio"})

    def radio_seek_percent(self, percentage: int) -> Any:
        """Przejdź do procentu aktualnego okna bufora Time Shift."""

        return self.call(
            "radio.seekPercent",
            {"sessionId": "radio", "percentage": max(0, min(100, int(percentage)))},
        )

    def set_volume(self, volume: int, session_id: str | None = None) -> Any:
        return self.call(
            "transport.setVolume",
            self._transport_args(session_id, volume=volume),
        )

    def set_rate(self, rate: float, session_id: str | None = None) -> Any:
        return self.call(
            "transport.setRate",
            self._transport_args(session_id, rate=rate),
        )

    def status(self, session_id: str | None = None) -> Any:
        return self.call(
            "transport.status",
            self._transport_args(session_id),
            timeout=5.0,
        )

    def checkpoint_podcast(self) -> Any:
        """Zapisz żywą pozycję odcinka przez wąską operację hosta C#."""
        return self.call("podcast.checkpoint", timeout=15.0)

    def spotify_capabilities(self) -> Any:
        """Read the supported Spotify surface without starting authentication."""
        return self.call("spotify.capabilities", timeout=15.0)

    def youtube_account_capabilities(self) -> Any:
        """Sprawdź dostępność bez uruchamiania logowania ani sieci."""
        return self.call("youtube.accountCapabilities", timeout=15.0)

    def youtube_account_status(self) -> Any:
        """Odczytaj wyłącznie użytkowy stan zapisanej sesji YouTube."""
        return self.call("youtube.accountStatus", timeout=15.0)

    def youtube_account_login(self) -> Any:
        """Uruchom OAuth dopiero po jawnym poleceniu w oknie konta."""
        # Host czeka do 15 minut, aby dostępny przepływ Google nie kończył
        # lokalnego odbiornika, gdy użytkownik potrzebuje więcej czasu.
        return self.call("youtube.accountLogin", timeout=960.0)

    def youtube_account_cancel_login(self) -> Any:
        """Przerwij lokalne oczekiwanie na wynik otwartej strony Google."""
        return self.call("youtube.accountCancelLogin", timeout=15.0)

    def youtube_account_logout(self) -> Any:
        """Usuń zapisane połączenie wyłącznie z bieżącego profilu AMC."""
        return self.call("youtube.accountLogout", timeout=30.0)

    def youtube_subscription_sync_preview(
        self, *, restore_suppressed: bool = False
    ) -> Any:
        return self.call(
            "youtube.subscriptionSyncPreview",
            {"restoreSuppressed": bool(restore_suppressed)},
            timeout=180.0,
        )

    def youtube_subscription_sync_apply(
        self, *, restore_suppressed: bool = False
    ) -> Any:
        return self.call(
            "youtube.subscriptionSyncApply",
            {"restoreSuppressed": bool(restore_suppressed)},
            timeout=90.0,
        )

    def youtube_playlist_sync_preview(
        self, *, restore_suppressed: bool = False
    ) -> Any:
        return self.call(
            "youtube.playlistSyncPreview",
            {"restoreSuppressed": bool(restore_suppressed)},
            timeout=300.0,
        )

    def youtube_playlist_sync_apply(
        self, *, restore_suppressed: bool = False
    ) -> Any:
        return self.call(
            "youtube.playlistSyncApply",
            {"restoreSuppressed": bool(restore_suppressed)},
            timeout=120.0,
        )

    def youtube_channel_broadcasts(
        self,
        subscription_id: str,
        *,
        refresh: bool = False,
    ) -> Any:
        """Odczytaj publiczne transmisje kanału; token zostaje w hoście C#."""
        return self.call(
            "youtube.channelBroadcasts",
            {
                "subscriptionId": subscription_id,
                "refresh": bool(refresh),
            },
            timeout=120.0,
        )

    def spotify_account_status(self) -> Any:
        """Read the separate catalogue and playback connection states."""
        return self.call("spotify.accountStatus", timeout=15.0)

    def spotify_login(self) -> Any:
        """Start OAuth only after an explicit user action in the account dialog."""
        return self.call("spotify.login", timeout=360.0)

    def spotify_configure(self, client_id: str) -> Any:
        """Store the user's public Spotify application identifier."""
        return self.call("spotify.configure", {"clientId": client_id}, timeout=15.0)

    def spotify_pairing_start(self) -> Any:
        return self.call("spotify.pairingStart", timeout=30.0)

    def spotify_pairing_status(self) -> Any:
        return self.call("spotify.pairingStatus", timeout=15.0)

    def spotify_pairing_cancel(self) -> Any:
        return self.call("spotify.pairingCancel", timeout=15.0)

    def spotify_collection_root(self) -> Any:
        """Read the user-facing Spotify collection categories."""
        return self.call("spotify.collectionRoot", timeout=15.0)

    def spotify_collection_view(
        self,
        view: str,
        *,
        loaded_count: int = 150,
        sort_mode: str = "AddedNewest",
    ) -> Any:
        """Read one Spotify collection view through the C# host."""
        return self.call(
            "spotify.collectionView",
            {
                "view": view,
                "loadedCount": max(150, int(loaded_count)),
                "sortMode": sort_mode,
            },
            timeout=180.0,
        )

    def spotify_container_items(
        self,
        *,
        external_id: str,
        title: str,
        kind: str,
        artist: str = "",
        artist_section: str | None = None,
        loaded_count: int = 150,
    ) -> Any:
        """Read a Spotify container without exposing credentials or sources."""
        args: dict[str, Any] = {
            "externalId": external_id,
            "title": title,
            "kind": kind,
            "loadedCount": max(150, int(loaded_count)),
        }
        if artist:
            args["artist"] = artist
        if artist_section:
            args["artistSection"] = artist_section
        return self.call("spotify.containerItems", args, timeout=120.0)

    def spotify_search(self, query: str) -> Any:
        """Search Spotify without changing the account collection."""
        return self.call("spotify.search", {"query": query}, timeout=120.0)

    def spotify_description(self, *, external_id: str, kind: str) -> Any:
        """Read one podcast or episode description without browsing its list."""
        return self.call(
            "spotify.description",
            {"externalId": external_id, "kind": kind},
            timeout=30.0,
        )

    def youtube_description(self, *, address: str, kind: str) -> Any:
        """Read one public YouTube description without starting Google login."""
        return self.call(
            "youtube.description",
            {"address": address, "kind": kind},
            timeout=55.0,
        )

    def spotify_collection_membership(
        self,
        items: list[dict[str, Any]],
        *,
        mode: str,
        add: bool | None = None,
    ) -> Any:
        """Change confirmed account membership without exposing credentials."""

        args: dict[str, Any] = {"mode": mode, "items": items}
        if add is not None:
            args["add"] = bool(add)
        return self.call(
            "spotify.collectionMembership",
            args,
            timeout=120.0,
        )

    def spotify_playlist_targets(self) -> Any:
        """Read editable native playlists without starting authentication."""
        return self.call("spotify.playlistTargets", timeout=120.0)

    def spotify_playlist_create(self, name: str, *, is_public: bool = False) -> Any:
        """Create one native Spotify playlist with explicit visibility."""
        return self.call(
            "spotify.playlistCreate",
            {"name": name, "isPublic": bool(is_public)},
            timeout=120.0,
        )

    def spotify_playlist_add_items(
        self,
        playlist_ids: list[str],
        items: list[dict[str, Any]],
    ) -> Any:
        """Add tracks or episodes to native Spotify playlists."""
        return self.call(
            "spotify.playlistAddItems",
            {"playlistIds": playlist_ids, "items": items},
            timeout=180.0,
        )

    def spotify_play(
        self,
        *,
        item_id: str,
        title: str,
        external_id: str,
        kind: str,
        volume: int,
        artist: str = "",
        position_seconds: float = 0.0,
        playback_rate: float = 1.0,
    ) -> Any:
        """Play one catalogue item without moving credentials through Python."""
        return self.call(
            "spotify.play",
            {
                "itemId": item_id,
                "title": title,
                "externalId": external_id,
                "kind": kind,
                "artist": artist,
                "volume": volume,
                "positionSeconds": max(0.0, float(position_seconds)),
                "playbackRate": min(2.0, max(0.5, float(playback_rate))),
            },
            timeout=60.0,
        )

    def tidal_account_status(self) -> Any:
        """Read the stored TIDAL connection state without opening a browser."""
        return self.call("tidal.accountStatus", timeout=15.0)

    def tidal_login(self) -> Any:
        """Start OAuth only after an explicit user action in the account dialog."""
        return self.call("tidal.login", timeout=360.0)

    def tidal_search(self, query: str) -> Any:
        """Search the official catalogue without changing the collection."""
        return self.call("tidal.search", {"query": query}, timeout=120.0)

    def tidal_container_items(
        self,
        *,
        item_id: str,
        external_id: str,
        title: str,
        kind: str,
        artist: str = "",
        public_uri: str | None = None,
        artist_section: str | None = None,
    ) -> Any:
        """Read a TIDAL container without moving credentials through Python."""
        args: dict[str, Any] = {
            "itemId": item_id,
            "externalId": external_id,
            "title": title,
            "kind": kind,
        }
        if artist:
            args["artist"] = artist
        if public_uri:
            args["publicUri"] = public_uri
        if artist_section:
            args["artistSection"] = artist_section
        return self.call("tidal.containerItems", args, timeout=120.0)

    def tidal_collection_view(self, view: str) -> Any:
        """Synchronize and read one account collection view in the C# host."""
        return self.call("tidal.collectionView", {"view": view}, timeout=180.0)

    def tidal_collection_membership(
        self,
        items: list[dict[str, Any]],
        *,
        mode: str,
        add: bool | None = None,
    ) -> Any:
        """Change account collection through C# without exposing credentials."""
        args: dict[str, Any] = {
            "mode": mode,
            "items": items,
        }
        if add is not None:
            args["add"] = bool(add)
        return self.call("tidal.collectionMembership", args, timeout=120.0)

    def tidal_desktop_play(
        self,
        *,
        item_id: str,
        external_id: str,
        title: str,
        related_album_external_id: str,
        restart_consent: bool = False,
    ) -> Any:
        """Point the original TIDAL desktop app at one exact track.

        The opaque IDs are transport data only.  Credentials and playback
        sources stay inside the shared C# implementation.
        """
        return self.call(
            "tidal.desktopPlay",
            {
                "itemId": item_id,
                "externalId": external_id,
                "title": title,
                "relatedAlbumExternalId": related_album_external_id,
                "restartConsent": bool(restart_consent),
            },
            timeout=90.0,
        )

    def tidal_desktop_play_container(
        self,
        *,
        item_id: str,
        external_id: str,
        title: str,
        kind: str,
        restart_consent: bool = False,
    ) -> Any:
        """Play a complete TIDAL album or playlist in original TIDAL."""
        return self.call(
            "tidal.desktopPlay",
            {
                "itemId": item_id,
                "externalId": external_id,
                "title": title,
                "kind": kind,
                "restartConsent": bool(restart_consent),
            },
            timeout=90.0,
        )

    def tidal_external_transport(self, command: str) -> Any:
        """Control only the system media session exposed by original TIDAL."""
        return self.call(
            "tidal.externalTransport",
            {"command": command},
            timeout=15.0,
        )

    def tidal_external_state(self) -> Any:
        """Read the deliberately partial SMTC state exposed by TIDAL."""
        return self.call(
            "tidal.externalTransport",
            {"command": "state"},
            timeout=15.0,
        )

    def wiim_devices(self) -> Any:
        """List configured WiiM devices without exposing their IP addresses."""
        return self.call("wiim.devices", timeout=15.0)

    def frontier_smart_devices(self) -> Any:
        return self.call("frontierSmart.devices", timeout=15.0)

    def frontier_smart_discover(self) -> Any:
        return self.call("frontierSmart.discover", timeout=15.0)

    def frontier_smart_identify(self, address: str) -> Any:
        return self.call(
            "frontierSmart.identify", {"address": address}, timeout=15.0
        )

    def frontier_smart_add(self, address: str, pin: str) -> Any:
        return self.call(
            "frontierSmart.add",
            {"address": address, "pin": pin},
            timeout=30.0,
        )

    def frontier_smart_remove(self, device_id: str) -> Any:
        return self.call(
            "frontierSmart.remove", {"deviceId": device_id}, timeout=15.0
        )

    def frontier_smart_select(self, device_id: str) -> Any:
        return self.call(
            "frontierSmart.select", {"deviceId": device_id}, timeout=15.0
        )

    def frontier_smart_snapshot(self, device_id: str) -> Any:
        return self.call(
            "frontierSmart.snapshot", {"deviceId": device_id}, timeout=30.0
        )

    def frontier_smart_transport(
        self,
        device_id: str,
        command: str,
        *,
        volume: int | None = None,
    ) -> Any:
        args: dict[str, Any] = {"deviceId": device_id, "command": command}
        if volume is not None:
            args["volume"] = int(volume)
        return self.call("frontierSmart.transport", args, timeout=30.0)

    def frontier_smart_modes(self, device_id: str) -> Any:
        return self.call(
            "frontierSmart.modes", {"deviceId": device_id}, timeout=30.0
        )

    def frontier_smart_set_mode(self, device_id: str, mode_key: int) -> Any:
        return self.call(
            "frontierSmart.modeSet",
            {"deviceId": device_id, "modeKey": int(mode_key)},
            timeout=30.0,
        )

    def frontier_smart_presets(self, device_id: str) -> Any:
        return self.call(
            "frontierSmart.presets", {"deviceId": device_id}, timeout=30.0
        )

    def frontier_smart_activate_preset(
        self, device_id: str, preset_key: int
    ) -> Any:
        return self.call(
            "frontierSmart.presetActivate",
            {"deviceId": device_id, "presetKey": int(preset_key)},
            timeout=30.0,
        )

    def wiim_snapshot(self, device_id: str) -> Any:
        """Read the current state of one configured WiiM device."""
        return self.call(
            "wiim.snapshot", {"deviceId": device_id}, timeout=30.0
        )

    def wiim_transport(
        self, device_id: str, command: str, *, volume: int | None = None
    ) -> Any:
        """Send one named transport command to the selected WiiM."""
        args: dict[str, object] = {
            "deviceId": device_id,
            "command": command,
        }
        if volume is not None:
            args["volume"] = max(0, min(100, int(volume)))
        return self.call(
            "wiim.transport",
            args,
            timeout=30.0,
        )

    def wiim_presets(self, device_id: str) -> Any:
        """Read twelve native preset slots without exposing stream URLs."""
        return self.call(
            "wiim.presets", {"deviceId": device_id}, timeout=30.0
        )

    def wiim_activate_preset(self, device_id: str, preset_number: int) -> Any:
        """Activate one validated native device preset."""
        return self.call(
            "wiim.presetActivate",
            {
                "deviceId": device_id,
                "presetNumber": max(1, min(12, int(preset_number))),
            },
            timeout=30.0,
        )

    def wiim_options(self, device_id: str, option: str) -> Any:
        """Read intentional labels for one option of the selected WiiM."""
        return self.call(
            "wiim.options",
            {"deviceId": device_id, "option": option},
            timeout=30.0,
        )

    def wiim_set_option(self, device_id: str, option: str, value: str) -> Any:
        """Apply a value previously returned by :meth:`wiim_options`."""
        return self.call(
            "wiim.optionSet",
            {"deviceId": device_id, "option": option, "value": value},
            timeout=30.0,
        )

    def wiim_toggle_shuffle(self, device_id: str) -> Any:
        return self.call(
            "wiim.shuffleToggle",
            {"deviceId": device_id},
            timeout=30.0,
        )

    def wiim_navigate_source(self, device_id: str, direction: int) -> Any:
        """Move through saved streams or occupied playable device presets."""
        return self.call(
            "wiim.sourceNavigate",
            {
                "deviceId": device_id,
                "direction": 1 if direction > 0 else -1,
            },
            timeout=30.0,
        )

    def wiim_network_streams(self) -> Any:
        """List saved WiiM streams without returning their network URLs."""
        return self.call("wiim.networkStreams", timeout=15.0)

    def wiim_play_network_stream(self, device_id: str, stream_id: str) -> Any:
        """Open one saved stream on a selected WiiM by stable identifiers."""
        return self.call(
            "wiim.networkStreamPlay",
            {"deviceId": device_id, "streamId": stream_id},
            timeout=30.0,
        )

    def wiim_add_network_stream(self, name: str, url: str) -> Any:
        return self.call(
            "wiim.networkStreamAdd",
            {"name": name, "url": url},
            timeout=30.0,
        )

    def wiim_network_stream_edit_info(self, stream_id: str) -> Any:
        """Read an address only for the explicit F2 edit dialog."""
        return self.call(
            "wiim.networkStreamEditInfo",
            {"streamId": stream_id},
            timeout=15.0,
        )

    def wiim_edit_network_stream(
        self, stream_id: str, name: str, url: str
    ) -> Any:
        return self.call(
            "wiim.networkStreamEdit",
            {"streamId": stream_id, "name": name, "url": url},
            timeout=30.0,
        )

    def wiim_remove_network_streams(self, stream_ids: list[str]) -> Any:
        return self.call(
            "wiim.networkStreamRemove",
            {"itemIds": list(stream_ids)},
            timeout=30.0,
        )

    def wiim_import_network_streams(self, path: str) -> Any:
        return self.call(
            "wiim.networkStreamImport",
            {"path": path},
            timeout=60.0,
        )

    def wiim_export_network_streams(self, path: str) -> Any:
        return self.call(
            "wiim.networkStreamExport",
            {"path": path},
            timeout=60.0,
        )

    def wiim_sort_network_streams(self, mode: str) -> Any:
        return self.call(
            "wiim.networkStreamSort",
            {"mode": mode},
            timeout=30.0,
        )

    def wiim_move_network_streams(
        self, stream_ids: list[str], direction: int
    ) -> Any:
        return self.call(
            "wiim.networkStreamMove",
            {
                "itemIds": list(stream_ids),
                "direction": int(direction),
            },
            timeout=30.0,
        )

    def wiim_place_network_streams(
        self, stream_ids: list[str], target_id: str
    ) -> Any:
        return self.call(
            "wiim.networkStreamPlace",
            {"itemIds": list(stream_ids), "targetId": target_id},
            timeout=30.0,
        )

    def sonos_targets(self) -> Any:
        """List Sonos groups; account tokens never leave the C# host."""
        return self.call("sonos.targets", timeout=60.0)

    def sonos_account_status(self) -> Any:
        """Read only user-facing Sonos account state; never return tokens."""
        return self.call("sonos.accountStatus", timeout=15.0)

    def sonos_account_login_start(self) -> Any:
        """Create one broker login attempt and return its trusted authorize URL."""
        return self.call("sonos.accountLoginStart", timeout=60.0)

    def sonos_account_login_check(self) -> Any:
        """Check one pending login exactly once; no polling is hidden here."""
        return self.call("sonos.accountLoginCheck", timeout=60.0)

    def sonos_account_login_cancel(self) -> Any:
        return self.call("sonos.accountLoginCancel", timeout=15.0)

    def sonos_account_refresh(self) -> Any:
        return self.call("sonos.accountRefresh", timeout=60.0)

    def sonos_account_persist_retry(self) -> Any:
        return self.call("sonos.accountPersistRetry", timeout=15.0)

    def sonos_account_disconnect(self) -> Any:
        return self.call("sonos.accountDisconnect", timeout=15.0)

    def sonos_snapshot(self, household_id: str, group_id: str) -> Any:
        """Read the current state of one group from the shared Sonos core."""
        return self.call(
            "sonos.snapshot",
            {"householdId": household_id, "groupId": group_id},
            timeout=45.0,
        )

    def sonos_transport(
        self,
        household_id: str,
        group_id: str,
        command: str,
        *,
        volume: int | None = None,
        seconds: float | None = None,
    ) -> Any:
        """Send one allowlisted command to a group selected from fresh topology."""
        args: dict[str, object] = {
            "householdId": household_id,
            "groupId": group_id,
            "command": command,
        }
        if volume is not None:
            args["volume"] = max(0, min(100, int(volume)))
        if seconds is not None:
            args["seconds"] = float(seconds)
        return self.call("sonos.transport", args, timeout=45.0)

    def sonos_library(self) -> Any:
        """List user-facing Sonos content categories."""
        return self.call("sonos.library", timeout=10.0)

    def sonos_materials(self, household_id: str, kind: str) -> Any:
        """Read favorites, playlists or saved streams for one Sonos household."""
        return self.call(
            "sonos.materials",
            {"householdId": household_id, "kind": kind},
            timeout=60.0,
        )

    def sonos_load_material(
        self,
        household_id: str,
        group_id: str,
        kind: str,
        material_id: str,
    ) -> Any:
        """Start one selected favorite, playlist or saved stream in the group."""
        return self.call(
            "sonos.loadMaterial",
            {
                "householdId": household_id,
                "groupId": group_id,
                "kind": kind,
                "materialId": material_id,
            },
            timeout=60.0,
        )

    def sonos_add_own_stream(self, name: str, url: str) -> Any:
        return self.call(
            "sonos.ownStreamAdd",
            {"name": name, "url": url},
            timeout=30.0,
        )

    def sonos_own_stream_edit_info(self, stream_id: str) -> Any:
        """Read the URL only for an explicit edit dialog."""
        return self.call(
            "sonos.ownStreamEditInfo",
            {"streamId": stream_id},
            timeout=15.0,
        )

    def sonos_edit_own_stream(
        self, stream_id: str, name: str, url: str
    ) -> Any:
        return self.call(
            "sonos.ownStreamEdit",
            {"streamId": stream_id, "name": name, "url": url},
            timeout=30.0,
        )

    def sonos_remove_own_streams(self, stream_ids: list[str]) -> Any:
        return self.call(
            "sonos.ownStreamRemove",
            {"itemIds": list(stream_ids)},
            timeout=30.0,
        )

    def sonos_import_own_streams(self, path: str) -> Any:
        return self.call(
            "sonos.ownStreamImport",
            {"path": path},
            timeout=60.0,
        )

    def sonos_sort_own_streams(self, mode: str) -> Any:
        return self.call(
            "sonos.ownStreamSort",
            {"mode": mode},
            timeout=30.0,
        )

    def sonos_move_own_streams(
        self, stream_ids: list[str], direction: int
    ) -> Any:
        return self.call(
            "sonos.ownStreamMove",
            {
                "itemIds": list(stream_ids),
                "direction": -1 if direction < 0 else 1,
            },
            timeout=30.0,
        )

    def sonos_place_own_streams(
        self, stream_ids: list[str], target_id: str
    ) -> Any:
        return self.call(
            "sonos.ownStreamPlace",
            {"itemIds": list(stream_ids), "targetId": target_id},
            timeout=30.0,
        )

    def sonos_export_own_streams(self, path: str) -> Any:
        return self.call(
            "sonos.ownStreamExport",
            {"path": path},
            timeout=60.0,
        )

    def refresh_podcasts(self, subscription_id: str | None = None) -> Any:
        """Odśwież jedno źródło lub całą bibliotekę przez właściciela C#."""
        args = {"subscriptionId": subscription_id} if subscription_id else {}
        # Duża biblioteka YouTube wykonuje yt-dlp kolejno dla wielu źródeł.
        # Wywołanie biegnie poza kolejką transportu, ale odpowiedź może zająć
        # znacznie dłużej niż zwykłe polecenie odtwarzania.
        return self.call("podcast.refresh", args, timeout=3600.0)

    def refresh_podcasts_automatically(self) -> Any:
        """Odśwież tylko należne źródła, z limitem zapisanym w ustawieniach."""
        return self.call("podcast.refreshAutomatic", timeout=3600.0)

    def podcast_global_settings(self) -> Any:
        """Odczytaj cztery globalne ustawienia bez surowych rekordów bazy."""
        return self.call("podcast.settingsGet", timeout=10.0)

    def configure_podcast_global_settings(
        self,
        *,
        downloads_folder: str,
        rss_refresh_interval_minutes: int,
        youtube_refresh_interval_minutes: int,
        automatic_refresh_batch_size: int,
    ) -> Any:
        """Zapisz ustawienia przez jedynego właściciela ``podcasts.db``."""
        return self.call(
            "podcast.settingsSet",
            {
                "downloadsFolder": downloads_folder,
                "rssRefreshIntervalMinutes": rss_refresh_interval_minutes,
                "youTubeRefreshIntervalMinutes": youtube_refresh_interval_minutes,
                "automaticRefreshBatchSize": automatic_refresh_batch_size,
            },
            timeout=10.0,
        )

    def download_podcast_episodes(
        self, episode_ids: list[str], *, separate_source_folders: bool = False
    ) -> Any:
        """Pobierz zaznaczone odcinki przez wspólny downloader AMC."""
        return self.call(
            "podcast.download",
            {
                "episodeIds": list(dict.fromkeys(episode_ids)),
                "separateSourceFolders": separate_source_folders,
            },
            timeout=24 * 3600.0,
        )

    def podcast_save_as_info(self, episode_id: str) -> Any:
        """Pobierz wspólną z AMC propozycję nazwy i folderu dialogu."""
        return self.call(
            "podcast.downloadSaveAsInfo",
            {"episodeId": episode_id},
            timeout=20.0,
        )

    def save_podcast_episode_as(self, episode_id: str, path: str) -> Any:
        """Zapisz jedną kopię pod wskazaną nazwą bez zmiany stanu pobrania."""
        return self.call(
            "podcast.downloadSaveAs",
            {"episodeId": episode_id, "path": path},
            timeout=24 * 3600.0,
        )

    def download_youtube_videos(
        self,
        episode_ids: list[str],
        video_format: str,
    ) -> Any:
        """Pobierz publiczne materiały YouTube jako wideo."""
        return self.call(
            "podcast.videoDownload",
            {
                "episodeIds": list(dict.fromkeys(episode_ids)),
                "format": video_format,
            },
            timeout=24 * 3600.0,
        )

    def youtube_video_save_as_info(self, episode_id: str) -> Any:
        return self.call(
            "podcast.videoSaveAsInfo",
            {"episodeId": episode_id},
            timeout=20.0,
        )

    def save_youtube_video_as(
        self,
        episode_id: str,
        path: str,
        video_format: str,
    ) -> Any:
        return self.call(
            "podcast.videoSaveAs",
            {"episodeId": episode_id, "path": path, "format": video_format},
            timeout=24 * 3600.0,
        )

    def add_podcast_source(self, address: str, title: str = "") -> Any:
        """Sprawdź i dodaj RSS, kolekcję YouTube albo publiczne medium."""
        return self.call(
            "podcast.add",
            {"address": address, "title": title},
            timeout=24 * 3600.0,
        )

    def search_podcast_directories(self, query: str) -> Any:
        """Przeszukaj publiczne katalogi bez danych konta użytkownika."""
        return self.call(
            "podcast.searchDirectories",
            {"query": query},
            timeout=45.0,
        )

    def search_radio_directory(self, query: str) -> Any:
        """Przeszukaj publiczny Radio Browser wspólnym klientem AMC."""
        return self.call(
            "radio.searchDirectory",
            {"query": query},
            timeout=45.0,
        )

    def inspect_podcast_opml(self, path: str) -> Any:
        """Odczytaj bezpieczne etykiety źródeł przez wspólny parser AMC."""
        return self.call("podcast.opml.inspect", {"path": path}, timeout=30.0)

    def import_podcast_opml(self, path: str, feed_urls: list[str]) -> Any:
        """Pobierz i dodaj wybrane RSS z uprzednio sprawdzonego OPML."""
        return self.call(
            "podcast.opml.import",
            {"path": path, "feedUrls": list(dict.fromkeys(feed_urls))},
            timeout=24 * 3600.0,
        )

    def export_podcast_opml(self, path: str) -> Any:
        """Zapisz RSS z Biblioteki przez wspólny eksporter AMC."""
        return self.call("podcast.opml.export", {"path": path}, timeout=30.0)

    def export_youtube_subscriptions(self, path: str) -> Any:
        """Zapisz kanały jako Takeout CSV albo kanały i playlisty jako OPML."""
        return self.call("podcast.youtube.export", {"path": path}, timeout=30.0)

    def rename_library_item(self, item_id: str, title: str) -> Any:
        return self.call(
            "library.renameTitle", {"itemId": item_id, "title": title}, timeout=20.0
        )

    def rename_local_file(
        self, item_id: str, name: str, *, path: str | None = None
    ) -> Any:
        payload = {"itemId": item_id, "name": name}
        if path:
            payload["path"] = path
        return self.call(
            "library.renameFile", payload, timeout=30.0
        )

    def rename_podcast_subscription(self, subscription_id: str, title: str) -> Any:
        return self.call(
            "podcast.renameSubscription",
            {"subscriptionId": subscription_id, "title": title},
            timeout=20.0,
        )

    def toggle_podcast_favorites(
        self,
        subscription_ids: list[str],
        episode_ids: list[str],
    ) -> Any:
        """Set the whole selection to favorite, or clear it when all are set."""
        return self.call(
            "podcast.toggleFavorite",
            {
                "subscriptionIds": list(dict.fromkeys(subscription_ids)),
                "episodeIds": list(dict.fromkeys(episode_ids)),
            },
            timeout=30.0,
        )

    def toggle_podcast_library(self, subscription_ids: list[str]) -> Any:
        """Zmień Bibliotekę dla podcastów wskazanych wprost albo przez odcinki."""
        return self.call(
            "podcast.toggleLibrary",
            {"subscriptionIds": list(dict.fromkeys(subscription_ids))},
            timeout=30.0,
        )

    def toggle_podcast_queue(
        self,
        episode_ids: list[str],
        *,
        play_next: bool = False,
    ) -> Any:
        """Zmień trwałą kolejkę odcinków bez mieszania jej z kolejką plików."""
        return self.call(
            "podcast.toggleQueue",
            {
                "episodeIds": list(dict.fromkeys(episode_ids)),
                "playNext": bool(play_next),
            },
            timeout=30.0,
        )

    def podcast_playback_options(self, item_id: str, target: str) -> Any:
        """Odczytaj opcje podcastu albo odcinka bez ujawniania ich w UI."""
        return self.call(
            "podcast.playbackOptions",
            {"itemId": item_id, "target": target},
            timeout=20.0,
        )

    def set_podcast_playback_options(
        self,
        item_id: str,
        target: str,
        values: dict[str, Any],
    ) -> Any:
        """Zapisz kompletny wybor przez waska brame hosta C#."""
        return self.call(
            "podcast.playbackOptions.set",
            {"itemId": item_id, "target": target, **values},
            timeout=30.0,
        )

    def edit_radio_station(self, station_id: str, name: str, url: str) -> Any:
        return self.call(
            "radio.editStation",
            {"stationId": station_id, "name": name, "url": url},
            timeout=20.0,
        )

    def add_radio_station(self, name: str, url: str) -> Any:
        """Dodaj albo zaktualizuj stację przez jedynego właściciela profilu."""
        return self.call(
            "radio.addStation", {"name": name, "url": url}, timeout=20.0
        )

    def remove_profile_items(
        self,
        session_id: str,
        view: str,
        item_ids: list[str],
        *,
        playlist_id: str | None = None,
    ) -> Any:
        payload: dict[str, Any] = {
            "sessionId": session_id,
            "view": view,
            "itemIds": item_ids,
        }
        if playlist_id:
            payload["playlistId"] = playlist_id
        return self.call(
            "library.remove",
            payload,
            timeout=30.0,
        )

    def toggle_profile_favorites(
        self,
        session_id: str,
        item_ids: list[str],
        *,
        paths: list[str] | None = None,
    ) -> Any:
        args: dict[str, Any] = {
            "sessionId": session_id,
            "itemIds": item_ids,
        }
        if paths:
            # Ctrl+O pokazuje wiersze dysku z identyfikatorem ``file:<path>``,
            # podczas gdy ten sam plik w Bibliotece ma trwałe ``local-*``.
            # Host rozwiązuje zweryfikowaną ścieżkę do istniejącego rekordu;
            # frontend nadal nie zapisuje bazy i nie tworzy własnych ID.
            args["paths"] = paths
        return self.call(
            "library.toggleFavorite",
            args,
            timeout=30.0,
        )

    def profile_playlists(self, session_id: str) -> Any:
        """Pobierz pełną roboczą migawkę playlist jednej sesji."""
        return self.call(
            "library.playlists",
            {"sessionId": session_id},
            timeout=30.0,
        )

    def export_common_data_snapshot(
        self,
        search_history: dict[str, list[str]] | None = None,
    ) -> Any:
        """AMC Test: zapisz ostatnia kompletna migawke dla publicznego AMC."""
        return self.call(
            "profile.commonDataExport",
            {"searchHistory": search_history or {}},
            timeout=120.0,
        )

    def common_data_snapshot_status(self) -> Any:
        """Stan migawki bez modyfikowania zadnego profilu."""
        return self.call("profile.commonDataStatus", timeout=60.0)

    def component_update_status(self) -> Any:
        """Odczytaj stan składników i odziedziczoną politykę aktualizacji."""
        return self.call("components.status", timeout=20.0)

    def update_media_components(
        self,
        *,
        automatic: bool = False,
        check_automatically: bool | None = None,
        download_automatically: bool | None = None,
    ) -> Any:
        """Sprawdź wspólne, zweryfikowane składniki FFmpeg i yt-dlp."""
        args: dict[str, bool] = {}
        if check_automatically is not None:
            args["checkAutomatically"] = bool(check_automatically)
        if download_automatically is not None:
            args["downloadAutomatically"] = bool(download_automatically)
        return self.call(
            "components.updateAutomatic" if automatic else "components.update",
            args,
            timeout=1_200.0,
        )

    def shell_integration_status(self) -> Any:
        """Odczytaj faktyczny stan integracji bieżącego profilu z Windows."""
        return self.call("shellIntegration.status", timeout=20.0)

    def general_settings_defaults(self) -> Any:
        """Odczytaj pełne systemowe ścieżki pokazywane w Ustawieniach."""
        return self.call("settings.defaults", timeout=20.0)

    def apply_shell_integration(
        self,
        *,
        context_menu_enabled: bool,
        open_with_enabled: bool,
        group_ids: list[str],
    ) -> Any:
        """Zastosuj wyłącznie znane hostowi grupy typów plików."""
        return self.call(
            "shellIntegration.apply",
            {
                "contextMenuEnabled": bool(context_menu_enabled),
                "openWithEnabled": bool(open_with_enabled),
                "groupIds": list(dict.fromkeys(str(value) for value in group_ids)),
            },
            timeout=30.0,
        )

    def open_windows_default_apps(self) -> Any:
        """Otwórz systemową stronę legalnego wyboru aplikacji domyślnych."""
        return self.call("shellIntegration.openDefaultApps", timeout=20.0)

    def import_common_data_snapshot(self, *, accept_test_data: bool = False) -> Any:
        """Publiczne AMC: odswiez dane; konflikt wymaga jawnej decyzji."""
        return self.call(
            "profile.commonDataImport",
            {"acceptTestData": bool(accept_test_data)},
            timeout=180.0,
        )

    def export_backup(self, path: str | Path, sections: list[str]) -> Any:
        """Eksportuj wskazane działy profilu przez jedynego pisarza C#."""
        return self.call(
            "profile.backupExport",
            {"path": str(path), "sections": list(sections)},
            timeout=180.0,
        )

    def inspect_backup(self, path: str | Path) -> Any:
        """Zweryfikuj manifest i dane bez zmiany profilu."""
        return self.call(
            "profile.backupInspect",
            {"path": str(path)},
            timeout=180.0,
        )

    def import_backup(
        self,
        path: str | Path,
        sections: list[str] | None = None,
    ) -> Any:
        """Odtwórz wyłącznie działy obecne w kopii i jawnie wybrane."""
        args: dict[str, Any] = {"path": str(path)}
        if sections is not None:
            args["sections"] = list(sections)
        return self.call("profile.backupImport", args, timeout=180.0)

    def replace_profile_playlists(
        self, session_id: str, playlists: list[dict[str, Any]]
    ) -> Any:
        """Atomowo zapisz wynik okna playlist przez jedynego pisarza C#."""
        return self.call(
            "library.playlists.replace",
            {"sessionId": session_id, "playlists": playlists},
            timeout=60.0,
        )

    def move_profile_playlist_items(
        self,
        session_id: str,
        playlist_id: str,
        selected_item_ids: list[str],
        visible_item_ids: list[str],
        direction: int,
    ) -> Any:
        """Atomowo przesuń widoczny, ciągły blok o jedną pozycję."""
        return self.call(
            "library.playlist.move",
            {
                "sessionId": session_id,
                "playlistId": playlist_id,
                "selectedItemIds": selected_item_ids,
                "visibleItemIds": visible_item_ids,
                "direction": direction,
            },
            timeout=30.0,
        )

    def place_profile_playlist_items_before(
        self,
        session_id: str,
        playlist_id: str,
        selected_item_ids: list[str],
        target_item_id: str,
    ) -> Any:
        """Atomowo wstaw wcześniej wybrane pozycje przed wskazanym celem."""
        return self.call(
            "library.playlist.placeBefore",
            {
                "sessionId": session_id,
                "playlistId": playlist_id,
                "selectedItemIds": selected_item_ids,
                "targetItemId": target_item_id,
            },
            timeout=30.0,
        )

    def recycle_local_files(
        self,
        item_ids: list[str],
        *,
        paths: list[str] | None = None,
    ) -> Any:
        args: dict[str, Any] = {"itemIds": item_ids}
        if paths:
            # Historia nagrywania ma celowo własne identyfikatory wierszy.
            # Zweryfikowana ścieżka pozwala hostowi znaleźć także świeże
            # nagranie, którego skaner Biblioteki jeszcze nie zindeksował.
            args["paths"] = paths
        return self.call(
            "library.recycle", args, timeout=120.0
        )

    def forget_recording_history_paths(self, paths: list[str]) -> Any:
        """Usuń ślad tylko po potwierdzonym usunięciu lub przeniesieniu pliku."""
        return self.call(
            "radio.recordingHistoryForgetPaths",
            {"paths": paths},
            timeout=20.0,
        )

    def add_bookmark(
        self,
        *,
        item_id: str,
        item_title: str,
        session_id: str = "files",
        name: str | None = None,
    ) -> Any:
        args = {
            "sessionId": session_id,
            "itemId": item_id,
            "itemTitle": item_title,
        }
        if name is not None:
            args["name"] = name
        return self.call(
            "bookmark.add",
            args,
            timeout=15.0,
        )

    def remove_bookmarks(self, bookmark_ids: list[str]) -> Any:
        """Usuń zaznaczone zakładki bez usuwania ich materiałów."""
        return self.call(
            "bookmark.remove",
            {"itemIds": list(dict.fromkeys(bookmark_ids))},
            timeout=20.0,
        )

    def navigate_bookmark(
        self,
        direction: int,
        *,
        session_id: str = "files",
        named_only: bool = False,
    ) -> Any:
        """Przejdź po wszystkich albo tylko nazwanych zakładkach materiału."""
        return self.call(
            "bookmark.navigate",
            {
                "sessionId": session_id,
                "direction": -1 if int(direction) < 0 else 1,
                "namedOnly": bool(named_only),
            },
            timeout=15.0,
        )

    def add_chapter(self, name: str, *, session_id: str) -> Any:
        return self.call(
            "chapter.add",
            {"sessionId": session_id, "name": name},
            timeout=20.0,
        )

    def prepare_chapters(self, *, session_id: str) -> Any:
        return self.call(
            "chapter.prepare",
            {"sessionId": session_id},
            timeout=40.0,
        )

    def list_chapters(self, *, session_id: str) -> Any:
        return self.call(
            "chapter.list",
            {"sessionId": session_id},
            timeout=15.0,
        )

    def navigate_chapter(self, direction: int, *, session_id: str) -> Any:
        return self.call(
            "chapter.navigate",
            {
                "sessionId": session_id,
                "direction": -1 if int(direction) < 0 else 1,
            },
            timeout=15.0,
        )

    def audio_clip_capabilities(self, source_path: str) -> Any:
        """Dostepne sposoby zapisu i rozszerzenia liczone przez silnik AMC."""
        return self.call(
            "audio.clipCapabilities",
            {"sourcePath": source_path},
            timeout=10.0,
        )

    def export_audio_clip(
        self,
        *,
        source_path: str,
        destination_path: str,
        start_seconds: float,
        end_seconds: float,
        format_value: str,
        operation_id: str | None = None,
    ) -> Any:
        """Zapisz zaznaczenie wspolnym ``AudioClipExporter`` z pelnego AMC."""
        args = {
            "sourcePath": source_path,
            "destinationPath": destination_path,
            "startSeconds": float(start_seconds),
            "endSeconds": float(end_seconds),
            "format": format_value,
        }
        if operation_id:
            args["operationId"] = operation_id
        return self.call(
            "audio.clipExport",
            args,
            # Duzy material lub konwersja do WAV/FLAC moze trwac wiele minut.
            timeout=3_600.0,
        )

    def audio_clip_removal_capabilities(self, source_path: str) -> Any:
        """Sprawdz FFmpeg, lokalnosc, typ i zapis pliku przed ostrzezeniem."""
        return self.call(
            "audio.clipRemoveCapabilities",
            {"sourcePath": source_path},
            timeout=15.0,
        )

    def audio_clip_append_capabilities(
        self,
        source_path: str,
        target_path: str,
    ) -> Any:
        """Sprawdz oba pliki i format celu przed rozpoczeciem dopisywania."""
        return self.call(
            "audio.clipAppendCapabilities",
            {"sourcePath": source_path, "targetPath": target_path},
            timeout=15.0,
        )

    def append_audio_clip(
        self,
        *,
        source_path: str,
        target_path: str,
        start_seconds: float,
        end_seconds: float,
        keep_backup: bool,
        operation_id: str,
    ) -> Any:
        """Dopisz zaznaczenie wspolnym ``AudioClipAppender`` pelnego AMC."""
        return self.call(
            "audio.clipAppend",
            {
                "sourcePath": source_path,
                "targetPath": target_path,
                "startSeconds": float(start_seconds),
                "endSeconds": float(end_seconds),
                "keepBackup": bool(keep_backup),
                "operationId": operation_id,
            },
            timeout=3_600.0,
        )

    def remove_audio_clip(
        self,
        *,
        source_path: str,
        start_seconds: float,
        end_seconds: float,
        source_duration_seconds: float,
        keep_backup: bool,
        operation_id: str,
    ) -> Any:
        """Usun zaznaczenie wspolnym edytorem pelnego AMC."""
        return self.call(
            "audio.clipRemoveOriginal",
            {
                "sourcePath": source_path,
                "startSeconds": float(start_seconds),
                "endSeconds": float(end_seconds),
                "sourceDurationSeconds": float(source_duration_seconds),
                "keepBackup": bool(keep_backup),
                "operationId": operation_id,
            },
            timeout=3_600.0,
        )

    def cancel_audio_clip(self, operation_id: str) -> Any:
        """Popros host o przerwanie dokladnie jednej operacji fragmentu."""
        return self.call(
            "audio.clipCancel",
            {"operationId": operation_id},
            timeout=5.0,
        )

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

    def queue_toggle_membership(
        self,
        items: list[dict[str, Any]],
        *,
        session_id: str = "local",
    ) -> Any:
        """Dodaj/usuń zaznaczenie ze zwykłej kolejki bez jej przebudowy."""
        return self.call(
            "queue.toggleMembership",
            {"sessionId": session_id, "items": items},
            timeout=60.0,
        )

    def queue_add_membership(
        self,
        items: list[dict[str, Any]],
        *,
        session_id: str = "local",
    ) -> Any:
        """Dodaj do kolejki idempotentnie; istniejącej pozycji nie usuwaj."""
        return self.call(
            "queue.addMembership",
            {"sessionId": session_id, "items": items},
            timeout=60.0,
        )

    def queue_toggle_play_next(
        self,
        items: list[dict[str, Any]],
        *,
        session_id: str = "local",
    ) -> Any:
        """Ustaw/usuń „odtwórz następne” dla całego zaznaczenia."""
        return self.call(
            "queue.togglePlayNext",
            {"sessionId": session_id, "items": items},
            timeout=60.0,
        )

    def import_playlist(self, path: str) -> Any:
        return self.call("radio.importPlaylist", {"path": path}, timeout=60.0)

    def import_playlist_to_library(self, path: str) -> Any:
        """Scal playlistę ze wspólną Biblioteką radia i zapisz profil."""
        return self.call(
            "radio.importPlaylistToLibrary", {"path": path}, timeout=60.0
        )

    def toggle_radio_library(self, item_ids: list[str]) -> Any:
        """Dodaj albo usuń stacje ze wspólnej Biblioteki Radia."""
        return self.call(
            "radio.toggleLibrary", {"itemIds": item_ids}, timeout=60.0
        )

    def set_radio_collection_sort(self, view: str, mode: str) -> Any:
        """Zapisz tryb sortowania Biblioteki albo Ulubionych Radia."""
        return self.call(
            "radio.collectionSort",
            {"view": view, "mode": mode},
            timeout=60.0,
        )

    def move_radio_collection_items(
        self,
        view: str,
        selected_item_ids: list[str],
        visible_item_ids: list[str],
        direction: int,
    ) -> Any:
        """Atomowo przesuń widoczny blok Biblioteki albo Ulubionych Radia."""
        return self.call(
            "radio.collectionMove",
            {
                "view": view,
                "selectedItemIds": selected_item_ids,
                "visibleItemIds": visible_item_ids,
                "direction": direction,
            },
            timeout=30.0,
        )

    def place_radio_collection_items_before(
        self,
        view: str,
        selected_item_ids: list[str],
        visible_item_ids: list[str],
        target_item_id: str,
    ) -> Any:
        """Wstaw wybrane stacje przed wskazaną stacją w kolejności własnej."""
        return self.call(
            "radio.collectionPlaceBefore",
            {
                "view": view,
                "selectedItemIds": selected_item_ids,
                "visibleItemIds": visible_item_ids,
                "targetItemId": target_item_id,
            },
            timeout=30.0,
        )

    def export_radio_favorites(self, path: str) -> Any:
        """Eksportuj Ulubione wspólnego profilu bez kopiowania danych do Pythona."""
        return self.call("radio.exportFavorites", {"path": path}, timeout=60.0)

    # ------------------------------------------------------ nagrywanie radia

    def toggle_radio_recording(self, payload: dict[str, Any]) -> Any:
        """Uruchom lub zatrzymaj prywatny tor nagrania wskazanej stacji."""
        return self.call("radio.recordingToggle", payload, timeout=10.0)

    def toggle_radio_recording_pause(self, station_id: str, url: str) -> Any:
        return self.call(
            "radio.recordingPauseToggle",
            {"stationId": station_id, "url": url},
            timeout=10.0,
        )

    def split_radio_recording(self, station_id: str, url: str) -> Any:
        return self.call(
            "radio.recordingSplit",
            {"stationId": station_id, "url": url},
            timeout=30.0,
        )

    def add_radio_recording_bookmark(
        self,
        station_id: str,
        url: str,
        name: str | None = None,
    ) -> Any:
        args = {"stationId": station_id, "url": url}
        if name is not None:
            args["name"] = name
        return self.call("radio.recordingBookmark", args, timeout=10.0)

    def stop_all_radio_recordings(self) -> Any:
        return self.call("radio.recordingStopAll", timeout=10.0)

    def radio_recording_status(self) -> Any:
        return self.call("radio.recordingStatus", timeout=5.0)

    def current_radio_broadcast_information(self) -> Any:
        """Tekst Alt+D zlozony wspolnym formatterem hosta C# AMC."""
        return self.call("radio.currentBroadcastInformation", timeout=5.0)

    def radio_recording_history(self) -> Any:
        """Historia biezacego procesu hosta; profil WPF pozostaje read-only."""
        return self.call("radio.recordingHistory", timeout=5.0)

    def radio_schedule_labels(self, schedules: list[dict]) -> Any:
        """Etykiety planow liczone wspolnym formatterem C# AMC."""
        return self.call(
            "radio.scheduleLabels",
            {"schedules": schedules, "activeIds": []},
            timeout=5.0,
        )

    def sync_radio_schedules(self, payload: dict[str, Any]) -> Any:
        """Przekaz plany hostowi C#, ktory liczy czas i wykonuje nagrania."""
        return self.call("radio.scheduleSync", payload, timeout=10.0)

    def radio_schedule_status(self) -> Any:
        return self.call("radio.scheduleStatus", timeout=5.0)

    def recognize_radio_tracks(self, scope: str = "CurrentStation") -> Any:
        """Rozpoznaj dzwiek z bufora; probka nie trafia do Pythona."""
        return self.call(
            "radio.recognition.run",
            {"scope": scope},
            timeout=45.0,
        )

    def configure_audio(self, **options: Any) -> Any:
        return self.call("audio.configure", options)

    def audio_outputs(
        self,
        selected_device_id: str | None = None,
        session_id: str | None = None,
    ) -> Any:
        """Lista jawnie nazwanych wyjsc, wraz z niedostepnym zapisanym wyborem."""
        args: dict[str, Any] = {"selectedDeviceId": selected_device_id}
        if session_id:
            args["sessionId"] = session_id
        return self.call(
            "audio.outputs",
            args,
            timeout=15.0,
        )

    def select_audio_output(
        self,
        session_id: str,
        device_id: str | None,
        *,
        restart: bool = True,
    ) -> Any:
        """Zapamietaj wyjscie sesji i, gdy gra, odtworz jej tor audio."""
        return self.call(
            "audio.selectOutput",
            {
                "sessionId": session_id,
                "deviceId": device_id,
                "restart": restart,
            },
            timeout=10.0,
        )


def default_host_path() -> Path:
    """Gdzie szukac silnika. Kolejnosc: zmienna srodowiskowa, obok programu,
    potem wynik buildu w drzewie zrodel."""
    override = os.environ.get("AMC_LITE_HOST")
    if override:
        return Path(override)

    here = Path(__file__).resolve()
    name = "amc_lite_host.exe" if os.name == "nt" else "amc_lite_host"
    candidates = [
        here.parent.parent / "host-next-2" / name,
        here.parent.parent.parent / "host-next-2" / name,
        here.parent.parent / "host-next" / name,
        here.parent.parent.parent / "host-next" / name,
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
