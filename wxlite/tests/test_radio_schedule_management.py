"""Prywatne zarzadzanie planami: model, czas i izolacja profilu AMC."""

from __future__ import annotations

import json
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite.radio_schedule_settings import (
    DAY_NAMES,
    ScheduleValidationError,
    clone_schedules,
    duplicate_schedule,
    effective_schedules,
    local_start_from_utc_ticks,
    sanitize_schedule,
    schedules_from_host_status,
    utc_ticks_from_local,
    validate_file_name_template,
)
from amc_wx_lite.state_store import LiteState, StateStore


def sample_schedule(**changes) -> dict:
    value = {
        "id": "plan-1",
        "name": "Poranna audycja",
        "stationId": "radio-1",
        "stationName": "Radio Jeden",
        "streamUrl": "https://example.invalid/live.mp3",
        "nextStartUtcTicks": 639_271_800_000_000_000,
        "timeZoneId": "Central European Standard Time",
        "durationMinutes": 90,
        "segmentMinutes": 30,
        "recurrence": "Daily",
        "activeDays": [],
        "outputFolder": "",
        "fileNameTemplate": "{stacja} - {data} {czas}",
        "recordingFormat": "Mp3",
        "recordingBitrateKbps": 192,
        "wakeComputer": None,
        "enabled": True,
    }
    value.update(changes)
    return value


def test_profile_is_mirrored_until_first_private_change() -> None:
    profile = sample_schedule(nowePoleHosta="zachowaj")
    assert effective_schedules([profile], None) == [profile]
    assert effective_schedules([profile], []) == []


def test_private_schedule_keeps_only_known_safe_fields() -> None:
    schedule = sanitize_schedule(
        sample_schedule(
            internalObject={"nie": "pokazuj"},
            stationName={"repr": "nie"},
        )
    )
    assert schedule is None, "plan bez tekstowej nazwy stacji jest odrzucany"

    schedule = sanitize_schedule(sample_schedule(internalObject={"nie": "zapisuj"}))
    assert schedule is not None
    assert "internalObject" not in schedule
    assert schedule["stationName"] == "Radio Jeden"


def test_duplicate_is_separate_disabled_plan_without_old_failure() -> None:
    original = sample_schedule(
        lastFailureUtcTicks=123,
        lastFailureMessage="stary błąd",
        lastFailureAcknowledged=False,
    )
    copy = duplicate_schedule(
        original, [original, sample_schedule(id="plan-2", name="Poranna audycja (2)")]
    )
    assert copy["id"] not in ("plan-1", "plan-2")
    assert copy["name"] == "Poranna audycja (3)"
    assert copy["enabled"] is False
    assert copy["lastFailureUtcTicks"] is None
    assert copy["lastFailureMessage"] == ""
    assert copy["lastFailureAcknowledged"] is True


def test_past_once_is_rejected_but_daily_moves_to_future() -> None:
    local_past = datetime.now().astimezone().replace(
        tzinfo=None, second=0, microsecond=0
    ) - timedelta(days=1)
    now = datetime.now(timezone.utc)
    try:
        utc_ticks_from_local(local_past, "Once", [], now_utc=now)
    except ScheduleValidationError as error:
        assert "przyszłości" in str(error)
    else:
        raise AssertionError("jednorazowy termin z przeszłości powinien być odrzucony")

    ticks = utc_ticks_from_local(local_past, "Daily", [], now_utc=now)
    assert local_start_from_utc_ticks(ticks).astimezone(timezone.utc) > now


def test_selected_days_uses_only_checked_weekdays() -> None:
    local_now = (
        datetime.now().astimezone().replace(tzinfo=None, second=0, microsecond=0)
    )
    wanted = DAY_NAMES[(local_now + timedelta(days=3)).weekday()]
    ticks = utc_ticks_from_local(
        local_now - timedelta(days=1),
        "SelectedDays",
        [wanted],
        now_utc=datetime.now(timezone.utc),
    )
    assert DAY_NAMES[local_start_from_utc_ticks(ticks).weekday()] == wanted


def test_file_name_tokens_are_validated_without_accepting_unknown_names() -> None:
    assert validate_file_name_template("{stacja} - {data}") == "{stacja} - {data}"
    try:
        validate_file_name_template("{wewnętrzne-id}")
    except ScheduleValidationError as error:
        assert "Nieznany token" in str(error)
    else:
        raise AssertionError("nieznany token powinien być odrzucony")


def test_private_schedule_state_distinguishes_inheritance_from_empty_list() -> None:
    with tempfile.TemporaryDirectory(prefix="amc-wx-schedules-") as directory:
        store = StateStore(directory)
        store.save(LiteState())
        raw = json.loads(
            (Path(directory) / StateStore.FILE_NAME).read_text(encoding="utf-8")
        )
        assert "radio_schedule_overrides" not in raw
        assert store.load().radio_schedule_overrides is None

        store.save(
            LiteState(
                radio_schedule_overrides=[],
                radio_schedule_wake_override=False,
            )
        )
        loaded = store.load()
        assert loaded.radio_schedule_overrides == []
        assert loaded.radio_schedule_wake_override is False


def test_private_schedule_round_trip_sanitizes_data() -> None:
    with tempfile.TemporaryDirectory(prefix="amc-wx-schedules-") as directory:
        store = StateStore(directory)
        store.save(
            LiteState(
                radio_schedule_overrides=[
                    sample_schedule(unknown={"object": "not stored"}),
                    {"id": "broken"},
                ]
            )
        )
        loaded = store.load()
        assert loaded.radio_schedule_overrides is not None
        assert (
            clone_schedules(loaded.radio_schedule_overrides)
            == loaded.radio_schedule_overrides
        )
        assert len(loaded.radio_schedule_overrides) == 1
        assert "unknown" not in loaded.radio_schedule_overrides[0]


def test_effective_payload_prefers_private_schedule_and_wake_setting() -> None:
    # Import po instalacji atrapy wx z istniejacego testu GUI.
    import sys

    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from test_gui_logic import install_wx_stub

    install_wx_stub()
    from amc_wx_lite.gui import LiteFrame

    profile = sample_schedule()
    private = sample_schedule(id="plan-prywatny", name="Plan prywatny")
    snapshot = SimpleNamespace(
        recording_schedules=(profile,),
        wake_scheduled_recordings=True,
        recording=SimpleNamespace(
            default_folder=r"D:\Nagrania",
            folder_preset="radio",
            station_folders={},
            format="Mp3",
            bitrate_kbps=192,
        ),
    )
    state = LiteState(
        radio_schedule_overrides=[private],
        radio_schedule_wake_override=False,
    )
    payload = LiteFrame._radio_schedule_sync_payload(snapshot, state)
    assert [item["id"] for item in payload["schedules"]] == ["plan-prywatny"]
    assert payload["wakeScheduledRecordings"] is False


def test_host_status_can_advance_the_private_effective_term() -> None:
    source = sample_schedule(nextStartUtcTicks=100)
    advanced = sample_schedule(nextStartUtcTicks=200, enabled=False)
    payload = {
        "schedules": [
            {
                **advanced,
                "label": "Poranna audycja, wyłączone",
                "navigationText": "Poranna audycja",
            }
        ]
    }
    assert schedules_from_host_status(payload, [source]) == [
        sanitize_schedule(advanced)
    ]


def test_old_host_status_without_model_fields_keeps_safe_fallback() -> None:
    source = sample_schedule()
    payload = {
        "schedules": [
            {
                "id": "plan-1",
                "label": "Poranna audycja, włączone",
                "navigationText": "Poranna audycja",
            }
        ]
    }
    assert schedules_from_host_status(payload, [source]) == clone_schedules([source])
