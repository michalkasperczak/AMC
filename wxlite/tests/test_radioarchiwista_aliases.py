"""Local Radio Archiwista names keep source identity and keyboard behavior."""

from __future__ import annotations

from types import SimpleNamespace

import pytest

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, i18n, menu_model, radioarchiwista_source as rarch
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId
from amc_wx_lite.shortcuts import Action, Chord, RADIOARCHIWISTA_SUPPORTED_ACTIONS, describe, resolve


def test_alias_persists_and_restores_source_titles_without_changing_ids(tmp_path) -> None:
    source = rarch.RadioarchiwistaSource(tmp_path / "profile")
    station_id = rarch.station_item_id("pr1")
    recording_id = "rarch:rec:pr1:2026-11-22T12-00-00+00-00"
    index = rarch.StationsIndex(
        (rarch.StationRecord("pr1", "Program 1", "Polskie Radio Program 1", 120),), 2,
    )
    original = rarch.station_rows(index)[0]
    recording = Row(recording_id, "13:00", "recording",
                    url="https://example.test/hour.mp3", service_id="pr1")

    assert source.set_alias(station_id, "Moje Jedynka")
    assert source.set_alias(recording_id, "Rozmowa poranna")
    reloaded = rarch.RadioarchiwistaSource(tmp_path / "profile")
    rows = reloaded.apply_aliases([original, recording])
    assert [(row.item_id, row.title) for row in rows] == [
        (station_id, "Moje Jedynka, 120 nagrań"),
        (recording_id, "Rozmowa poranna"),
    ]
    assert rows[0].service_id == "pr1" and rows[1].url == recording.url
    reloaded.stations = lambda force=False: (index, False)
    assert reloaded.search_station_rows()[0].title == "Moje Jedynka, 120 nagrań"
    source.toggle_station_favorite("pr1", "Polskie Radio Program 1")
    assert reloaded.search_rows()[0].title == "Moje Jedynka"
    archive_recording = rarch.ArchiveRecording(
        station_slug="pr1", station_name="Program 1", item_id=recording_id,
        title="13:00", audio_url=recording.url, duration_seconds=3600,
        local_date_label="22.11.2026", local_hour_label="13:00",
        local_label="22.11.2026 13:00",
        utc_started_at="2026-11-22T12:00:00+00:00", hour=13,
    )
    source.toggle_queue(archive_recording)
    assert reloaded.queue_rows()[0].title == "Rozmowa poranna"
    assert reloaded.queue_rows()[0].item_id == recording_id

    assert reloaded.set_alias(station_id, "")
    assert reloaded.set_alias(recording_id, "")
    restored = reloaded.apply_aliases(rows)
    assert [row.title for row in restored] == [original.title, recording.title]
    assert [row.item_id for row in restored] == [station_id, recording_id]
    with pytest.raises(rarch.RadioarchiwistaError):
        source.set_alias("rarch:days:pr1:2026-11-22", "Niedozwolone")


def test_alias_dialog_restores_focus_and_reopens_with_saved_name(tmp_path, monkeypatch) -> None:
    i18n.configure("pl")
    source = rarch.RadioarchiwistaSource(tmp_path / "profile")
    station = rarch.station_rows(rarch.StationsIndex(
        (rarch.StationRecord("pr1", "Program 1", "Program 1", 3),), 2,
    ))[0]
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    navigator.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_STATIONS, [station])
    spoken: list[str] = []
    focus: list[str] = []
    dialogs: list[tuple[str, str]] = []

    class Dialog:
        next_value = "Moja Jedynka"
        next_result = gui.wx.ID_OK

        def __init__(self, _parent, **options):
            dialogs.append((options["field_name"], options["value"]))

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):
            return self.next_result

        @property
        def value(self):
            return self.next_value

    monkeypatch.setattr(gui, "RenameItemDialog", Dialog)
    frame = SimpleNamespace(
        navigator=navigator,
        _radioarchiwista_source=lambda: source,
        _sync_views=lambda: None,
        _restore_focus_after_dialog=lambda: focus.append("returned"),
        announcer=SimpleNamespace(say=spoken.append),
    )
    gui.LiteFrame._rename_radioarchiwista_item(frame)
    assert dialogs[-1] == ("Nowa nazwa w AMC", "Program 1")
    assert navigator.session.model.selected_row.title == "Moja Jedynka, 3 nagrania"
    assert navigator.session.model.selected_id == station.item_id
    assert spoken[-1] == "Zapisano nazwę w AMC: Moja Jedynka"
    assert focus == ["returned"]

    Dialog.next_result = gui.wx.ID_CANCEL
    gui.LiteFrame._rename_radioarchiwista_item(frame)
    assert dialogs[-1][1] == "Moja Jedynka"
    assert focus == ["returned", "returned"]
    assert source.alias_for(station.item_id) == "Moja Jedynka"

    Dialog.next_result = gui.wx.ID_OK
    Dialog.next_value = ""
    gui.LiteFrame._rename_radioarchiwista_item(frame)
    assert navigator.session.model.selected_row.title == station.title
    assert spoken[-1] == "Przywrócono nazwę z katalogu"


def test_alias_command_has_one_target_rule_and_matching_surfaces() -> None:
    station = Row("rarch:station:pr1", "Program 1", "folder",
                  service_kind="rarchStation", service_id="pr1")
    recording = Row("rarch:rec:pr1:one", "13:00", "recording")
    day = Row("rarch:days:pr1:2026-11-22", "22.11", "folder",
              service_kind="rarchDay", service_id="pr1")
    assert menu_model.radioarchiwista_alias_target(station)
    assert menu_model.radioarchiwista_alias_target(recording)
    assert not menu_model.radioarchiwista_alias_target(day)
    assert not menu_model.radioarchiwista_alias_target(None)

    assert Action.RENAME_RADIOARCHIWISTA_ITEM in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert resolve(Chord("F2"), player_view=False, radio_session=False,
                   radioarchiwista_session=True) is Action.RENAME_RADIOARCHIWISTA_ITEM
    try:
        for language, fragment in (("pl", "stacji lub nagrania"),
                                   ("en", "station or recording")):
            i18n.configure(language)
            menu = next(item for group in menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
                        for item in group.items
                        if item.action is Action.RENAME_RADIOARCHIWISTA_ITEM)
            palette = next(item for item in entries_for_session(SessionId.RADIOARCHIWISTA)
                           if item.action is Action.RENAME_RADIOARCHIWISTA_ITEM)
            help_rows = describe("Radio Archiwista", set(RADIOARCHIWISTA_SUPPORTED_ACTIONS))
            assert fragment in menu.label and fragment in palette.name
            assert menu.shortcut == palette.shortcut == "F2"
            assert menu.needs_radioarchiwista_alias_target
            assert any(key == "F2" and fragment in text for key, text in help_rows)
    finally:
        i18n.configure("pl")
