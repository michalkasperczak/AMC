"""Regresje #21: mowa operacji i jednoznaczne korzenie Biblioteki."""

from __future__ import annotations

from pathlib import Path
import sys
from types import SimpleNamespace
from typing import cast

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui
from amc_wx_lite.gui import Announcer, LibraryFoldersDialog
from amc_wx_lite.library_db import LibraryDatabase, FolderSource, disambiguate_folder_source_names, folder_rows


def _source(identifier: str, path: str, display_name: str) -> FolderSource:
    return FolderSource(
        id=identifier,
        ordinal=0,
        path=path,
        display_name=display_name,
        resume_mode=0,
    )


def test_same_folder_display_names_get_case_insensitive_parent_labels() -> None:
    sources = [
        _source("one", r"X:\a\cd", "cd"),
        _source("two", r"X:\b\CD\\", "CD"),
        _source("three", r"X:\muzyka", "Muzyka"),
    ]

    assert disambiguate_folder_source_names(sources) == {
        "one": r"cd (X:\a)",
        "two": r"CD (X:\b)",
        "three": "Muzyka",
    }


def test_root_folder_rows_use_disambiguated_source_labels() -> None:
    sources = [
        FolderSource(id="one", ordinal=0, path=r"X:\a\cd", display_name="cd", resume_mode=0),
        FolderSource(id="two", ordinal=1, path=r"X:\b\CD\\", display_name="CD", resume_mode=0),
    ]
    database = SimpleNamespace(folder_sources=lambda: sources, active_items_under=lambda _root, **_kwargs: [])

    assert [row.title for row in folder_rows(cast(LibraryDatabase, database), None)] == ["cd (X:\\a)", "CD (X:\\b)"]


class _Control:
    def __init__(self) -> None:
        self.focus_count = 0
        self.enabled: list[bool] = []

    def SetFocus(self) -> None:  # noqa: N802 - wx API
        self.focus_count += 1

    def Enable(self, enabled: bool) -> None:  # noqa: N802 - wx API
        self.enabled.append(enabled)


class _SourcesControl(_Control):
    def __init__(self) -> None:
        super().__init__()
        self.items: list[str] = []
        self.selection = -1

    def GetSelection(self) -> int:  # noqa: N802 - wx API
        return self.selection

    def Set(self, items: list[str]) -> None:  # noqa: N802 - wx API
        self.items = list(items)
        self.selection = -1

    def SetSelection(self, selection: int) -> None:  # noqa: N802 - wx API
        self.selection = selection


class _Status:
    def __init__(self) -> None:
        self.label = ""
        self.name = ""

    def SetLabel(self, value: str) -> None:  # noqa: N802 - wx API
        self.label = value

    def SetName(self, value: str) -> None:  # noqa: N802 - wx API
        self.name = value

    def GetLabel(self) -> str:  # noqa: N802 - wx API
        return self.label

    def GetHandle(self) -> int:  # noqa: N802 - wx API
        return 42


class _Owner:
    def __init__(self, active: bool = True) -> None:
        self.active = active

    def IsActive(self) -> bool:  # noqa: N802 - wx API
        return self.active


class _DialogHarness:
    """Headless controls, but every exercised dialog method is production code."""

    selected_source_id = LibraryFoldersDialog.selected_source_id
    apply_payload = LibraryFoldersDialog.apply_payload
    finish = LibraryFoldersDialog.finish
    fail = LibraryFoldersDialog.fail
    set_status = LibraryFoldersDialog.set_status
    _update_buttons = LibraryFoldersDialog._update_buttons

    def __init__(self, announcer: Announcer, status: _Status) -> None:
        self._busy = True
        self._sources: list[dict] = []
        self.sources = _SourcesControl()
        self.add_button = _Control()
        self.refresh_button = _Control()
        self.refresh_all_button = _Control()
        self.detach_button = _Control()
        self.close_button = _Control()
        self.status = status
        self.announcer = announcer
        self.layout_count = 0

    def Layout(self) -> None:  # noqa: N802 - wx API
        self.layout_count += 1


def test_library_folder_finish_and_fail_use_real_payload_path_and_active_dialog_owner() -> None:
    notifications: list[tuple[int, int, int, int]] = []
    status = _Status()
    owner = _Owner()
    original_parent = getattr(gui.wx, "GetTopLevelParent", None)
    gui.wx.GetTopLevelParent = lambda _status: owner
    try:
        announcer = Announcer(status, notify=lambda *args: notifications.append(args))
        dialog = _DialogHarness(announcer, status)

        dialog.finish({
            "message": "Dodano Folder Biblioteki",
            "sources": [{"id": "source-add", "label": "Dodany folder"}],
        })
        assert len(notifications) == 1
        assert dialog.selected_source_id == "source-add"
        assert dialog.sources.focus_count == 1

        dialog.finish(
            {
                "message": "Odświeżono Folder Biblioteki",
                "sources": [
                    {"id": "source-add", "label": "Dodany folder"},
                    {"id": "source-refresh", "label": "Odświeżony folder"},
                ],
            },
            preferred_id="source-refresh",
        )
        assert len(notifications) == 2
        assert dialog.selected_source_id == "source-refresh"
        assert dialog.sources.focus_count == 2

        dialog.fail("Nie dodano folderu")
        assert len(notifications) == 3
        assert status.label == "Nie dodano folderu"
        assert status.name == "Nie dodano folderu"
        assert dialog.sources.focus_count == 3

        dialog.finish({"message": "Odłączono Folder Biblioteki", "sources": []})
        assert len(notifications) == 4
        assert dialog.add_button.focus_count == 1

        owner.active = False
        dialog.fail("Cichy w nieaktywnym ownerze")
        assert len(notifications) == 4
        assert status.label == "Cichy w nieaktywnym ownerze"
    finally:
        if original_parent is None:
            delattr(gui.wx, "GetTopLevelParent")
        else:
            gui.wx.GetTopLevelParent = original_parent


def test_library_folder_runner_callback_reaches_real_finish() -> None:
    notifications: list[tuple[int, int, int, int]] = []
    owner = _Owner()

    class Client:
        def local_folder_sources(self) -> object:
            return {"sources": []}

        def add_local_folder_source(self, path: str) -> object:
            assert path == r"X:\muzyka"
            return {
                "message": "Dodano Folder Biblioteki",
                "sources": [{"id": "source-add", "label": "Muzyka"}],
            }

    class Runner:
        def __init__(self) -> None:
            self.tasks: list[str] = []

        def submit(self, task_name, work, done, failed) -> None:
            self.tasks.append(task_name)
            done(work())

    class RunnerDialog(_DialogHarness):
        instance: "RunnerDialog | None" = None

        def __init__(self, parent, payload, *, add_folder, refresh_folders, detach_folder) -> None:
            status = _Status()
            super().__init__(Announcer(status, notify=lambda *args: notifications.append(args)), status)
            self._add_folder = add_folder
            RunnerDialog.instance = self

        def __enter__(self):
            return self

        def __exit__(self, exc_type, exc_value, traceback) -> None:
            return None

        def IsBeingDeleted(self) -> bool:  # noqa: N802 - wx API
            return False

        def ShowModal(self) -> None:  # noqa: N802 - wx API
            self._add_folder(r"X:\muzyka", self)

    client = Client()
    runner = Runner()
    frame = SimpleNamespace(
        _profile_edit_client=lambda: client,
        runner=runner,
        navigator=SimpleNamespace(active=None),
        _refresh_profile_view=lambda: None,
        _restore_focus_after_dialog=lambda: None,
    )
    original_parent = getattr(gui.wx, "GetTopLevelParent", None)
    original_dialog = gui.LibraryFoldersDialog
    gui.wx.GetTopLevelParent = lambda _status: owner
    gui.LibraryFoldersDialog = RunnerDialog
    try:
        gui.LiteFrame._show_library_folders(cast(gui.LiteFrame, frame))
        assert runner.tasks == ["library-folder-add"]
        assert RunnerDialog.instance is not None
        assert RunnerDialog.instance.selected_source_id == "source-add"
        assert len(notifications) == 1
    finally:
        gui.LibraryFoldersDialog = original_dialog
        if original_parent is None:
            delattr(gui.wx, "GetTopLevelParent")
        else:
            gui.wx.GetTopLevelParent = original_parent


def test_disambiguation_keeps_unc_and_drive_roots_without_trailing_separators() -> None:
    sources = [
        _source("unc", "\\\\serwer\\udział\\", "Root"),
        _source("drive", "X:\\", "root"),
    ]

    assert disambiguate_folder_source_names(sources) == {
        "unc": "Root (\\\\serwer\\udział)",
        "drive": "root (X:)",
    }
