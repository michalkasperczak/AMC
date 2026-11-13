"""Narrow, private pipe to AMC Test's separate Apple Music WebView2 process.

The helper owns Apple authorization and media.  This adapter passes catalog
song IDs and fixed transport commands only; it never reads cookies or URLs.
"""

from __future__ import annotations

import json
import queue
import re
import subprocess
import threading
from pathlib import Path
from typing import Any, Callable

from .apple_music_source import AppleMusicRecord


_SONG_ID = re.compile(r"[0-9]{1,20}\Z")
_TRANSPORT = frozenset({"toggle", "stop", "next", "previous", "state", "show"})


def catalog_song_id(record: AppleMusicRecord | None) -> str | None:
    """Allow only playable catalog tracks, never private library IDs."""

    if record is None or record.row.kind != "track" or not record.target.can_play:
        return None
    candidate = record.target.catalog_id or ""
    return candidate if _SONG_ID.fullmatch(candidate) else None


class AppleMusicWebUnavailable(RuntimeError):
    """A safe, user-facing failure without private or technical data."""

    def __str__(self) -> str:
        return "Osadzony odtwarzacz Apple Music nie odpowiada."


def helper_for_host(host_executable: str | Path) -> Path:
    """The web helper ships only in the Test host's private component folder."""

    return Path(host_executable).resolve().parent / "apple-music-web" / (
        "amc_apple_music_web_host.exe"
    )


class AppleMusicWebClient:
    def __init__(
        self,
        executable: str | Path,
        *,
        spawn: Callable[..., Any] = subprocess.Popen,
    ) -> None:
        self.executable = Path(executable)
        self._spawn = spawn
        self._process: Any = None
        self._responses: queue.Queue[dict | None] = queue.Queue()
        self._lock = threading.RLock()
        self._next_id = 0

    @property
    def is_running(self) -> bool:
        return self._process is not None and self._process.poll() is None

    def play(self, song_id: str) -> dict:
        if not _SONG_ID.fullmatch(song_id):
            raise AppleMusicWebUnavailable()
        return self._call("play", songId=song_id)

    def transport(self, command: str) -> dict:
        if command not in _TRANSPORT:
            raise AppleMusicWebUnavailable()
        if command != "show" and not self.is_running:
            return {"hasSession": False} if command == "state" else {
                "handled": False, "message": "Apple Music nie odtwarza w AMC."
            }
        return self._call(command)

    def seek(self, seconds: float) -> dict:
        if not isinstance(seconds, (int, float)) or not 0 <= seconds <= 86400:
            raise AppleMusicWebUnavailable()
        if not self.is_running:
            return {"handled": False, "message": "Apple Music nie odtwarza w AMC."}
        return self._call("seek", seconds=float(seconds))

    def close(self) -> None:
        with self._lock:
            process = self._process
            self._process = None
            if process is None:
                return
            try:
                process.stdin.close()
                process.wait(timeout=2)
            except (OSError, ValueError, subprocess.TimeoutExpired):
                try:
                    process.terminate()
                    process.wait(timeout=2)
                except (OSError, subprocess.TimeoutExpired):
                    try:
                        process.kill()
                    except OSError:
                        pass

    def _start(self) -> None:
        if self.is_running:
            return
        if not self.executable.is_file():
            raise AppleMusicWebUnavailable()
        self._responses = queue.Queue()
        try:
            self._process = self._spawn(
                [str(self.executable)],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=subprocess.DEVNULL,
                text=True,
                encoding="utf-8",
                bufsize=1,
                close_fds=True,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
        except OSError:
            raise AppleMusicWebUnavailable() from None
        threading.Thread(
            target=self._read_responses,
            args=(self._process, self._responses),
            name="amc-apple-music-web-reader",
            daemon=True,
        ).start()

    @staticmethod
    def _read_responses(process: Any, responses: queue.Queue[dict | None]) -> None:
        try:
            for line in process.stdout:
                try:
                    response = json.loads(line)
                except (ValueError, TypeError):
                    response = None
                responses.put(response if isinstance(response, dict) else None)
        except OSError:
            pass
        responses.put(None)

    def _call(self, command: str, **arguments: object) -> dict:
        with self._lock:
            self._start()
            self._next_id += 1
            request_id = self._next_id
            try:
                self._process.stdin.write(json.dumps({
                    "id": request_id, "command": command, **arguments,
                }, ensure_ascii=False) + "\n")
                self._process.stdin.flush()
                response = self._responses.get(timeout=35)
                if response is None or response.get("id") != request_id:
                    raise AppleMusicWebUnavailable()
                result = response.get("result")
                if not isinstance(result, dict):
                    raise AppleMusicWebUnavailable()
                return result
            except (OSError, ValueError, queue.Empty, AppleMusicWebUnavailable):
                self.close()
                raise AppleMusicWebUnavailable() from None
