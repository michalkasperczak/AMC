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
            # Host ustawia UTF-8 jawnie. ``replace`` jest ostatnia bariera na
            # wypadek, gdy obca biblioteka zapisze surowy bajt na stderr:
            # diagnostyka moze stracic jeden znak, ale watek czytajacy i cala
            # komunikacja z silnikiem nie moga przez to zginac.
            errors="replace",
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

    def pause_resume(self) -> Any:
        return self.call("transport.pauseResume")

    def stop(self) -> Any:
        return self.call("transport.stop")

    def seek_by(self, seconds: float) -> Any:
        return self.call("transport.seek", {"deltaSeconds": seconds})

    def seek_to_position(self, seconds: float) -> Any:
        """Skok na BEZWZGLEDNA pozycje.

        Host juz to umie: LiteEngineHandlers.cs:655 czyta ``positionSeconds``
        obok ``deltaSeconds``, wiec skok procentowy nie wymaga nowej komendy
        protokolu -- procent przeliczamy na sekundy po stronie klienta.
        """
        return self.call("transport.seek", {"positionSeconds": max(0.0, float(seconds))})

    def set_volume(self, volume: int) -> Any:
        return self.call("transport.setVolume", {"volume": volume})

    def set_rate(self, rate: float) -> Any:
        return self.call("transport.setRate", {"rate": rate})

    def status(self) -> Any:
        return self.call("transport.status", timeout=5.0)

    def checkpoint_podcast(self) -> Any:
        """Zapisz żywą pozycję odcinka przez wąską operację hosta C#."""
        return self.call("podcast.checkpoint", timeout=15.0)

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

    def refresh_podcasts(self, subscription_id: str | None = None) -> Any:
        """Odśwież jedno źródło lub całą bibliotekę przez właściciela C#."""
        args = {"subscriptionId": subscription_id} if subscription_id else {}
        # Duża biblioteka YouTube wykonuje yt-dlp kolejno dla wielu źródeł.
        # Wywołanie biegnie poza kolejką transportu, ale odpowiedź może zająć
        # znacznie dłużej niż zwykłe polecenie odtwarzania.
        return self.call("podcast.refresh", args, timeout=3600.0)

    def download_podcast_episodes(self, episode_ids: list[str]) -> Any:
        """Pobierz zaznaczone odcinki przez wspólny downloader AMC."""
        return self.call(
            "podcast.download",
            {"episodeIds": list(dict.fromkeys(episode_ids))},
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

    def add_podcast_source(self, address: str, title: str = "") -> Any:
        """Sprawdź i dodaj RSS, kolekcję YouTube albo publiczne medium."""
        return self.call(
            "podcast.add",
            {"address": address, "title": title},
            timeout=24 * 3600.0,
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

    def rename_local_file(self, item_id: str, name: str) -> Any:
        return self.call(
            "library.renameFile", {"itemId": item_id, "name": name}, timeout=30.0
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

    def remove_profile_items(
        self, session_id: str, view: str, item_ids: list[str]
    ) -> Any:
        return self.call(
            "library.remove",
            {"sessionId": session_id, "view": view, "itemIds": item_ids},
            timeout=30.0,
        )

    def recycle_local_files(self, item_ids: list[str]) -> Any:
        return self.call(
            "library.recycle", {"itemIds": item_ids}, timeout=120.0
        )

    def add_bookmark(self, *, item_id: str, item_title: str) -> Any:
        return self.call(
            "bookmark.add",
            {"itemId": item_id, "itemTitle": item_title},
            timeout=15.0,
        )

    def remove_bookmarks(self, bookmark_ids: list[str]) -> Any:
        """Usuń zaznaczone zakładki bez usuwania ich materiałów."""
        return self.call(
            "bookmark.remove",
            {"itemIds": list(dict.fromkeys(bookmark_ids))},
            timeout=20.0,
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

    def configure_audio(self, **options: Any) -> Any:
        return self.call("audio.configure", options)

    def audio_outputs(self, selected_device_id: str | None = None) -> Any:
        """Lista jawnie nazwanych wyjsc, wraz z niedostepnym zapisanym wyborem."""
        return self.call(
            "audio.outputs",
            {"selectedDeviceId": selected_device_id},
            timeout=5.0,
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
