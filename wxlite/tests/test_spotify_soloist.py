from __future__ import annotations

import json
import subprocess
from pathlib import Path

import pytest

from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.spotify_soloist import (
    SoloistManager,
    SoloistSetupError,
    request_lossless_quality,
    soloist_credential_target,
)


class FakeCredentialStore:
    def __init__(self) -> None:
        self.value: bytes | None = None

    def read(self) -> bytes | None:
        return self.value

    def write(self, value: bytes) -> None:
        self.value = value


def test_lossless_request_updates_global_and_paired_user_prefs(tmp_path: Path) -> None:
    settings = tmp_path / "settings"
    global_prefs = settings / "prefs"
    user_prefs = settings / "Users" / "account" / "prefs"
    user_prefs.parent.mkdir(parents=True)
    global_prefs.write_text(
        "other.setting=keep\naudio.play_bitrate_enumeration=3\n", encoding="utf-8"
    )
    user_prefs.write_text(
        "audio.play_bitrate_non_metered_enumeration=3\n", encoding="utf-8"
    )

    request_lossless_quality(tmp_path)
    for prefs in (global_prefs, user_prefs):
        content = prefs.read_text(encoding="utf-8")
        assert content.count("audio.play_bitrate_enumeration=5") == 1
        assert content.count("audio.play_bitrate_non_metered_enumeration=5") == 1
        assert content.count("audio.play_bitrate_non_metered_migrated=true") == 1
        assert "\n=3" not in content
    assert "other.setting=keep" in global_prefs.read_text(encoding="utf-8")


def test_soloist_key_is_only_in_credential_store(tmp_path: Path) -> None:
    executable = tmp_path / "soloist.exe"
    executable.write_bytes(b"test binary")
    credentials = FakeCredentialStore()
    manager = SoloistManager(
        tmp_path / "ui" / "state.json", profile=TEST_PROFILE,
        credential_store=credentials, local_app_data=tmp_path / "local",
    )
    setup = manager.save(executable, "secret-key")
    assert setup.key_saved
    assert credentials.value == b"secret-key"
    raw = manager.settings_path.read_text(encoding="utf-8")
    assert "secret-key" not in raw
    assert json.loads(raw)["executable"] == str(executable)
    assert json.loads(raw)["data_directory"] == str(manager.data_directory)
    assert manager.setup() == setup


def test_soloist_profiles_have_separate_credentials_and_data(tmp_path: Path) -> None:
    assert soloist_credential_target(TEST_PROFILE) != soloist_credential_target(PUBLIC_PROFILE)
    test = SoloistManager(
        tmp_path / "test" / "state.json", profile=TEST_PROFILE,
        credential_store=FakeCredentialStore(), local_app_data=tmp_path,
    )
    public = SoloistManager(
        tmp_path / "public" / "state.json", profile=PUBLIC_PROFILE,
        credential_store=FakeCredentialStore(), local_app_data=tmp_path,
    )
    assert test.data_directory != public.data_directory
    assert test.cache_directory != public.cache_directory
    assert test.settings_path != public.settings_path


def test_soloist_rejects_missing_executable_without_storing_key(tmp_path: Path) -> None:
    credentials = FakeCredentialStore()
    manager = SoloistManager(
        tmp_path / "ui" / "state.json", credential_store=credentials,
        local_app_data=tmp_path,
    )
    with pytest.raises(SoloistSetupError, match="soloist.exe"):
        manager.save(tmp_path / "missing" / "soloist.exe", "secret-key")
    assert credentials.value is None
    assert not manager.settings_path.exists()


def test_start_uses_loopback_and_hides_process_output(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
) -> None:
    executable = tmp_path / "soloist.exe"
    executable.write_bytes(b"test binary")
    manager = SoloistManager(
        tmp_path / "ui" / "state.json", credential_store=FakeCredentialStore(),
        local_app_data=tmp_path,
    )
    manager.save(executable, "secret-key")
    seen: dict[str, object] = {}

    class FakeProcess:
        def poll(self) -> None:
            return None

    def fake_popen(args: list[str], **options: object) -> FakeProcess:
        seen["args"] = args
        seen["options"] = options
        seen["starts"] = int(seen.get("starts", 0)) + 1
        return FakeProcess()

    monkeypatch.setattr("amc_wx_lite.spotify_soloist.subprocess.Popen", fake_popen)
    monkeypatch.setattr("amc_wx_lite.spotify_soloist.time.sleep", lambda _seconds: None)
    manager.start()
    manager.start()
    assert seen["starts"] == 1
    assert seen["args"][-2:] == ["--ws", "127.0.0.1:0"]
    assert seen["options"]["stdout"] is subprocess.DEVNULL
    assert seen["options"]["stderr"] is subprocess.DEVNULL
    assert "audio.play_bitrate_enumeration=5" in (
        manager.data_directory / "settings" / "prefs"
    ).read_text(encoding="utf-8")


def test_status_distinguishes_running_from_paired(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
) -> None:
    executable = tmp_path / "soloist.exe"
    executable.write_bytes(b"test binary")
    manager = SoloistManager(
        tmp_path / "ui" / "state.json", credential_store=FakeCredentialStore(),
        local_app_data=tmp_path,
    )
    manager.save(executable, "secret-key")
    current_code = 3

    def fake_run(args: list[str], **_options: object) -> subprocess.CompletedProcess[bytes]:
        code = 0 if args[-1] == "status" else current_code
        return subprocess.CompletedProcess(args, code, b"", b"private data")

    monkeypatch.setattr("amc_wx_lite.spotify_soloist.subprocess.run", fake_run)
    assert "Wybierz urządzenie" in manager.status()
    current_code = 0
    assert "gotowy do odtwarzania" in manager.status()
