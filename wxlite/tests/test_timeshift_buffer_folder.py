"""Persistent Time Shift folder and host startup contract."""

from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.state_store import LiteState, Options, StateStore


def test_custom_timeshift_folder_survives_restart_and_reaches_host(tmp_path):
    folder = tmp_path / "Bufor AMC Time Shift"
    folder.mkdir()
    store = StateStore(tmp_path / "state")
    store.save(LiteState(options=Options(timeshift_minutes=360, timeshift_buffer_folder=str(folder))))

    options = store.load().options
    assert options.timeshift_minutes == 360
    assert options.timeshift_buffer_folder == str(folder)
    command = LiteHostClient("amc_lite_host.exe", timeshift_minutes=options.timeshift_minutes,
                             timeshift_buffer_folder=options.timeshift_buffer_folder)._command()
    assert command == ["amc_lite_host.exe", "--timeshift-minutes", "360",
                       "--timeshift-buffer-folder", str(folder)]


def test_default_timeshift_folder_keeps_existing_host_behavior(tmp_path):
    store = StateStore(tmp_path / "state")
    store.save(LiteState(options=Options(timeshift_buffer_folder=None)))
    options = store.load().options
    assert options.timeshift_buffer_folder is None
    assert LiteHostClient("amc_lite_host.exe", timeshift_buffer_folder=options.timeshift_buffer_folder)._command() == [
        "amc_lite_host.exe", "--timeshift-minutes", "30"
    ]


def test_invalid_saved_folder_value_cannot_become_host_argument():
    options = Options(timeshift_buffer_folder=123)  # type: ignore[arg-type]
    options.clamp()
    assert options.timeshift_buffer_folder is None
