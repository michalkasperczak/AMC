"""Local Spotify Soloist bootstrap, separate from Spotify catalog OAuth.

Soloist is downloaded by the user from Spotify. AMC never redistributes it or
puts its API key into the normal profile, logs, or diagnostic output.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path

from .release_profile import ReleaseProfile, current_profile
from .tidalapi_session_store import WindowsCredentialStore


class SoloistSetupError(Exception):
    """An error safe to show to the user without leaking subprocess output."""


@dataclass(frozen=True)
class SoloistSetup:
    executable: Path
    data_directory: Path
    key_saved: bool


def soloist_credential_target(profile: ReleaseProfile) -> str:
    return f"{profile.credential_namespace}.SpotifySoloistApiKey.v1"


class SoloistManager:
    """Own an optional Soloist process for one AMC release profile."""

    def __init__(
        self,
        ui_state_path: Path,
        *,
        profile: ReleaseProfile | None = None,
        credential_store: object | None = None,
        local_app_data: Path | None = None,
    ) -> None:
        self.profile = profile or current_profile()
        self.settings_path = (Path(ui_state_path).parent / "spotify-soloist.json").resolve()
        root = local_app_data or Path(os.environ.get("LOCALAPPDATA", ""))
        if not root.is_absolute():
            raise SoloistSetupError("Nie można ustalić lokalnego katalogu danych Windows.")
        self.data_directory = root / self.profile.local_profile_folder / "soloist"
        self.cache_directory = root / self.profile.cache_folder / "soloist"
        self._store = credential_store
        self._process: subprocess.Popen[bytes] | None = None

    def _credential_store(self) -> object:
        if self._store is None:
            self._store = WindowsCredentialStore(
                self.profile,
                target_override=soloist_credential_target(self.profile),
                user_name_override=f"{self.profile.display_name} Spotify Soloist",
            )
        return self._store

    def _read_key(self) -> str | None:
        try:
            data = self._credential_store().read()
            return data.decode("utf-8") if data else None
        except Exception:
            raise SoloistSetupError("Nie można odczytać klucza Soloist z Menedżera poświadczeń Windows.") from None

    def setup(self) -> SoloistSetup | None:
        try:
            if not self.settings_path.is_file():
                return None
            values = json.loads(self.settings_path.read_text(encoding="utf-8"))
            if values.get("version") != 1 or not isinstance(values.get("executable"), str):
                raise ValueError()
            executable = Path(values["executable"])
            if not executable.is_absolute():
                raise ValueError()
            stored_data_directory = values.get("data_directory")
            if stored_data_directory is not None and stored_data_directory != str(self.data_directory):
                raise ValueError()
            return SoloistSetup(executable, self.data_directory, bool(self._read_key()))
        except SoloistSetupError:
            raise
        except (OSError, ValueError, TypeError, AttributeError):
            raise SoloistSetupError("Nie można odczytać konfiguracji Soloist.") from None

    def save(self, executable: str | Path, key: str) -> SoloistSetup:
        path = Path(executable).expanduser()
        if not path.is_absolute() or path.name.casefold() != "soloist.exe" or not path.is_file():
            raise SoloistSetupError("Wskaż istniejący plik soloist.exe pobrany ze Spotify.")
        if not isinstance(key, str) or not key.strip() or len(key.encode("utf-8")) > 2048:
            raise SoloistSetupError("Wpisz klucz Soloist z panelu Spotify for Developers.")
        try:
            self._credential_store().write(key.strip().encode("utf-8"))
        except Exception:
            raise SoloistSetupError("Nie można zapisać klucza w Menedżerze poświadczeń Windows.") from None
        self.settings_path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.settings_path.with_suffix(".tmp")
        try:
            temporary.write_text(
                json.dumps({"version": 1, "executable": str(path),
                            "data_directory": str(self.data_directory)}), encoding="utf-8"
            )
            temporary.replace(self.settings_path)
        except OSError:
            raise SoloistSetupError("Nie można zapisać ścieżki do Soloist.") from None
        return SoloistSetup(path, self.data_directory, True)

    def start(self) -> None:
        setup = self.setup()
        if setup is None or not setup.executable.is_file():
            raise SoloistSetupError("Najpierw wskaż plik soloist.exe i zapisz klucz.")
        key = self._read_key()
        if not key:
            raise SoloistSetupError("Brakuje klucza Soloist w Menedżerze poświadczeń Windows.")
        self.data_directory.mkdir(parents=True, exist_ok=True)
        self.cache_directory.mkdir(parents=True, exist_ok=True)
        flags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
        try:
            self._process = subprocess.Popen(
                [str(setup.executable), "--device-name", self.profile.display_name,
                 "--api-key", key, "--data-dir", str(self.data_directory),
                 "--cache-dir", str(self.cache_directory),
                 "--ws", "127.0.0.1:0"],
                stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL, creationflags=flags,
            )
        except OSError:
            raise SoloistSetupError("Nie udało się uruchomić Soloist.") from None
        # Detect an expired build or immediate configuration failure without
        # making the caller interpret untrusted stderr containing account data.
        time.sleep(0.2)
        exit_code = self._process.poll()
        if exit_code == 10:
            self._process = None
            raise SoloistSetupError("Wersja Soloist wygasła. Pobierz nową ze Spotify.")
        if exit_code is not None:
            self._process = None
            raise SoloistSetupError("Soloist zakończył działanie podczas uruchamiania.")

    def status(self) -> str:
        setup = self.setup()
        if setup is None or not setup.executable.is_file():
            return "Soloist nie jest skonfigurowany."
        flags = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0
        def check(*arguments: str) -> subprocess.CompletedProcess[bytes]:
            return subprocess.run(
                [str(setup.executable), "ctl", "--data-dir", str(self.data_directory),
                 *arguments], capture_output=True, timeout=8, check=False,
                creationflags=flags,
            )
        try:
            result = check("status")
        except (OSError, subprocess.TimeoutExpired):
            return "Nie można teraz sprawdzić stanu Soloist."
        if result.returncode == 0:
            # ctl now succeeds only after Connect login. Never speak arbitrary
            # stdout or stderr, which may contain account data.
            try:
                current = check("now", "--json")
            except (OSError, subprocess.TimeoutExpired):
                current = None
            if current is not None and current.returncode == 0:
                return "Soloist jest połączony ze Spotify i gotowy do odtwarzania."
            return "Soloist działa. Wybierz urządzenie AMC w aplikacji Spotify."
        return "Soloist nie działa lub nie można się z nim połączyć."

    def close(self) -> None:
        process = self._process
        self._process = None
        if process is not None and process.poll() is None:
            process.terminate()
