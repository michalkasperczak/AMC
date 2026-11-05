"""Regresje publicznego odbioru: folder, Kosz i zakładki odtwarzacza."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui  # noqa: E402
from amc_wx_lite.state_store import LiteState, Options
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import Navigator, SessionId, View  # noqa: E402
from amc_wx_lite.profile_layout import read_only_mirror  # noqa: E402
from amc_wx_lite.quick_info import QuickInfoMemoryCache  # noqa: E402
from amc_wx_lite.shortcuts import Action  # noqa: E402


class ImmediateRunner:
    def submit(self, _slot, work, done, failed):  # noqa: ANN001
        try:
            done(work())
        except Exception as error:  # pragma: no cover - ścieżka diagnostyczna
            failed(error)


class DeferredRunner:
    def __init__(self) -> None:
        self.jobs = []

    def submit(self, _slot, work, done, failed):  # noqa: ANN001
        self.jobs.append((work, done, failed))

    def finish(self) -> None:
        work, done, failed = self.jobs.pop(0)
        try:
            done(work())
        except Exception as error:  # pragma: no cover - ścieżka diagnostyczna
            failed(error)


def test_shift_delete_keeps_open_folder_and_removes_only_confirmed_paths() -> None:
    folder = r"D:\Nagrania"
    first = rf"{folder}\pierwszy.mp3"
    second = rf"{folder}\drugi.mp3"
    third = rf"{folder}\trzeci.mp3"
    fourth = rf"{folder}\czwarty.mp3"
    parent = Row("dir:parent", "..", "parent", path="D:\\")
    rows = [
        parent,
        Row(f"file:{first}", "pierwszy.mp3", "track", path=first),
        Row(f"file:{second}", "drugi.mp3", "track", path=second),
        Row(f"file:{third}", "trzeci.mp3", "track", path=third),
        Row(f"file:{fourth}", "czwarty.mp3", "track", path=fourth),
    ]
    navigator = Navigator()
    navigator.apply_folder(folder, rows)
    navigator.session.model.select_id(f"file:{third}")
    selected = rows[2:4]
    calls: list[tuple[list[str], list[str] | None]] = []
    spoken: list[str] = []
    synced: list[bool] = []
    refreshed: list[bool] = []

    class Client:
        def recycle_local_files(self, item_ids, *, paths=None):  # noqa: ANN001
            calls.append((item_ids, paths))
            return {
                "removed": [
                    {"id": "", "title": "drugi.mp3", "path": second},
                    {"id": "", "title": "trzeci.mp3", "path": third},
                ],
                "failures": [],
            }

    frame = SimpleNamespace(
        navigator=navigator,
        _selected_action_rows=lambda: selected,
        _profile_edit_client=lambda: Client(),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _forget_private_recording_history_paths=lambda _paths: None,
        _refresh_profile_view=lambda **_kwargs: refreshed.append(True),
        _sync_views=lambda: synced.append(True),
    )
    original = gui.wx.MessageBox
    gui.wx.MessageBox = lambda *_args, **_kwargs: gui.wx.YES
    try:
        gui.LiteFrame._recycle_selected_files(frame)
    finally:
        gui.wx.MessageBox = original

    assert calls == [
        ([f"file:{second}", f"file:{third}"], [second, third])
    ]
    assert navigator.active is SessionId.FILES
    assert navigator.view is View.LIST
    assert navigator.session.folder_path == folder
    assert [row.item_id for row in navigator.session.model.rows] == [
        "dir:parent",
        f"file:{first}",
        f"file:{fourth}",
    ]
    assert navigator.session.model.selected_id == f"file:{fourth}"
    assert synced == [True]
    assert refreshed == []
    assert spoken == [
        "Przeniesiono do Kosza: 2 pliki. "
        "Liczba pozycji pozostałych w folderze: 2"
    ]


def test_b_adds_bookmark_for_file_opened_directly_from_folder() -> None:
    path = r"D:\Nagrania\audycja.mp3"
    item_id = f"file:{path}"
    navigator = Navigator()
    navigator.session.view = View.PLAYER
    navigator.session.now_playing_id = item_id
    navigator.session.now_playing_title = "audycja.mp3"
    calls: list[dict[str, object]] = []
    spoken: list[str] = []

    class Client:
        def add_bookmark(self, **kwargs):  # noqa: ANN003
            calls.append(kwargs)
            return {"added": True, "positionSeconds": 65.0}

    frame = SimpleNamespace(
        navigator=navigator,
        client=Client(),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
    )

    gui.LiteFrame._add_bookmark(frame)

    assert calls == [{
        "item_id": item_id,
        "item_title": "audycja.mp3",
        "session_id": "files",
        "name": None,
    }]
    assert spoken == ["Dodano zakładkę: 1:05"]


def test_late_recycle_result_never_navigates_away_from_new_session() -> None:
    path = r"D:\Nagrania\audycja.mp3"
    row = Row(f"file:{path}", "audycja.mp3", "track", path=path)
    navigator = Navigator()
    navigator.apply_folder(r"D:\Nagrania", [row])
    runner = DeferredRunner()
    spoken: list[str] = []
    synced: list[bool] = []
    refreshed: list[bool] = []

    class Client:
        def recycle_local_files(self, _item_ids, *, paths=None):  # noqa: ANN001
            assert paths == [path]
            return {
                "removed": [{"id": "", "title": "audycja.mp3", "path": path}],
                "failures": [],
            }

    frame = SimpleNamespace(
        navigator=navigator,
        _selected_action_rows=lambda: [row],
        _profile_edit_client=lambda: Client(),
        runner=runner,
        announcer=SimpleNamespace(say=spoken.append),
        _forget_private_recording_history_paths=lambda _paths: None,
        _refresh_profile_view=lambda **_kwargs: refreshed.append(True),
        _sync_views=lambda: synced.append(True),
    )
    original = gui.wx.MessageBox
    gui.wx.MessageBox = lambda *_args, **_kwargs: gui.wx.YES
    try:
        gui.LiteFrame._recycle_selected_files(frame)
    finally:
        gui.wx.MessageBox = original

    navigator.active = SessionId.RADIO
    runner.finish()

    assert navigator.active is SessionId.RADIO
    assert navigator.sessions[SessionId.FILES].model.rows == []
    assert synced == []
    assert refreshed == []
    assert spoken == [
        "Przeniesiono do Kosza: audycja.mp3. "
        "Liczba pozycji pozostałych w folderze: 0"
    ]


def test_dispatch_routes_shared_bookmark_action_to_the_same_handler() -> None:
    navigator = Navigator()
    navigator.session.view = View.PLAYER
    calls: list[bool] = []
    frame = SimpleNamespace(
        navigator=navigator,
        announcer=SimpleNamespace(say=lambda _text: None),
        _add_bookmark=lambda **_kwargs: calls.append(True),
    )

    gui.LiteFrame._dispatch(frame, Action.ADD_BOOKMARK)

    assert calls == [True]


def test_repeated_left_arrow_reuses_measured_result(tmp_path) -> None:  # noqa: ANN001
    path = r"D:\Nagrania\audycja.mp3"
    row = Row(f"file:{path}", "audycja.mp3", "track", path=path)
    navigator = Navigator()
    navigator.session.model.replace([row])
    calls: list[tuple[str, dict, float]] = []
    spoken: list[str] = []

    class Client:
        def call(self, operation, request, *, timeout):  # noqa: ANN001
            calls.append((operation, request, timeout))
            return {"text": "MP3, 320 kb/s"}

    frame = SimpleNamespace(
        navigator=navigator,
        state=LiteState(), options=Options(),
        client=Client(),
        layout=read_only_mirror(tmp_path, tmp_path),
        runner=ImmediateRunner(),
        announcer=SimpleNamespace(say=spoken.append),
        _window_alive=lambda: True,
        _selected_item_id=lambda: row.item_id,
        _quick_info_cache=QuickInfoMemoryCache(),
    )

    gui.LiteFrame._announce_quick_information(frame)
    gui.LiteFrame._announce_quick_information(frame)

    assert len(calls) == 1
    assert spoken == ["MP3, 320 kb/s", "MP3, 320 kb/s"]
