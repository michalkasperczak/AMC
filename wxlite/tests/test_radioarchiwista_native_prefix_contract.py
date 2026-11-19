"""Kontrakt zwykłego wyszukiwania przyrostowego listy Radio Archiwista.

Nie emulujemy SysListView32: to nalezy do Windows i wymaga uzgodnionej proby
GUI. Mierzymy granice, za które odpowiada aplikacja: realny producent wierszy,
realny Navigator oraz prawdziwy hook listy musza zostawic literę i cyfrę
natywnej kontrolce, a pierwszy tekst kolumny musi zostac surową nazwą.
"""

from __future__ import annotations

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, list_reading, list_sync  # noqa: E402
from amc_wx_lite.playback_activity import activity_rows  # noqa: E402
from amc_wx_lite.radioarchiwista_source import ArchiveRecording, recording_rows  # noqa: E402
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId  # noqa: E402
from amc_wx_lite.state_store import Options  # noqa: E402


class _LiteralKeyEvent:
    """Minimalne EVT_CHAR_HOOK dla zwykłego znaku bez modyfikatora."""

    def __init__(self, character: str) -> None:
        self._code = ord(character)
        self.skipped = False

    def GetKeyCode(self) -> int:  # noqa: N802 - wx API
        return self._code

    def GetRawKeyCode(self) -> int:  # noqa: N802 - wx API
        return self._code

    def ControlDown(self) -> bool:  # noqa: N802 - wx API
        return False

    def ShiftDown(self) -> bool:  # noqa: N802 - wx API
        return False

    def AltDown(self) -> bool:  # noqa: N802 - wx API
        return False

    def Skip(self) -> None:  # noqa: N802 - wx API
        self.skipped = True


def _recording(hour: int, *, favorite: bool = False) -> ArchiveRecording:
    return ArchiveRecording(
        station_slug="pr1",
        station_name="Program 1",
        item_id=f"rarch:rec:pr1:{hour:02d}",
        title=f"Godzina {hour}",
        audio_url=f"https://archive.invalid/pr1/{hour:02d}.mp3",
        duration_seconds=3600.0,
        local_date_label="19.11.2026",
        local_hour_label=f"{hour:02d}:00",
        local_label=f"19.11.2026 {hour:02d}:00",
        utc_started_at=f"2026-11-19T{hour:02d}:00:00+00:00",
        hour=hour,
    )


def test_hours_use_the_same_raw_first_column_as_a_normal_file_list() -> None:
    """Dekoracje statusu nie mogą zmienić dosłownego prefiksu nazwy."""

    recordings = (_recording(0), _recording(7), _recording(17))
    rows = recording_rows(
        recordings,
        favorite_ids={recordings[1].item_id},
    )
    rendered = activity_rows(
        rows,
        current_item_id=recordings[1].item_id,
        playing=True,
        paused=False,
        session_id=SessionId.RADIOARCHIWISTA.value,
    )

    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_HOURS,
        rendered,
        slug="pr1",
        year=2026,
        month=11,
        day=19,
    )
    state = navigator.sessions[SessionId.RADIOARCHIWISTA]
    fields = list_reading.session_fields(
        Options(), None, "updown", session=SessionId.RADIOARCHIWISTA.value,
    )
    shown = list_sync.model_row_texts(state.model, reading_fields=fields)

    assert state.library_view is LibraryView.RADIOARCHIWISTA_HOURS
    assert [entry.texts[0] for entry in shown] == ["00:00", "07:00", "17:00"]
    assert shown[1].texts[2].strip().endswith("Odtwarzane, ★")


def test_actual_list_hook_leaves_literal_letters_and_digits_to_syslistview32() -> None:
    """AMC nie może zamienić ani odtworzyć zwykłego prefiksu listy."""

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame._on_key = lambda event: (_ for _ in ()).throw(
        AssertionError("zwykły znak nie może trafić do poleceń AMC")
    )

    for character in ("a", "7", "0"):
        event = _LiteralKeyEvent(character)
        frame._on_list_shortcut_hook(event)
        assert event.skipped is True, character
