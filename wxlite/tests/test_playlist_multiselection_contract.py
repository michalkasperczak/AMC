"""Native multi-selection is the source for every playlist command."""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui, list_sync  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import SessionId, View  # noqa: E402


class NativeSelection:
    shown_item_id = gui.MediaListCtrl.shown_item_id
    selected_rows = gui.MediaListCtrl.selected_rows

    def __init__(self, model: ListModel) -> None:
        self.model = model
        self._shown = list_sync.model_row_texts(model)
        self._selected = [0, 2]

    def GetFirstSelected(self) -> int:  # noqa: N802
        return self._selected[0]

    def GetNextSelected(self, index: int) -> int:  # noqa: N802
        return next((value for value in self._selected if value > index), -1)


def test_playlist_commands_keep_native_multi_selection_in_every_supported_session() -> None:
    cases = (
        (SessionId.FILES, "track", None),
        (SessionId.RADIO, "station", None),
        (SessionId.PODCASTS, "episode", None),
        (SessionId.TIDAL, "track", "tracks"),
        (SessionId.SPOTIFY, "track", "spotify:track"),
    )
    for session_id, kind, service_prefix in cases:
        rows = [
            Row(
                f"{session_id.value}-{index}", f"Utwór {index}", kind,
                service_id=f"{service_prefix}:{index}" if service_prefix else None,
            )
            for index in range(3)
        ]
        model = ListModel()
        model.replace(rows)
        model.select_id(rows[2].item_id)
        control = NativeSelection(model)
        frame = SimpleNamespace(
            navigator=SimpleNamespace(
                active=session_id,
                session=SimpleNamespace(view=View.LIST, model=model),
            ),
            _active_list=lambda: control,
        )

        selected = gui.LiteFrame._selected_action_rows(frame)

        assert [row.item_id for row in selected] == [
            rows[0].item_id, rows[2].item_id
        ], session_id
