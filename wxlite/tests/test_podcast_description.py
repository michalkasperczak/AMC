"""Pełny opis i przejście do podcastu zachowują tekst oraz fokus."""

from __future__ import annotations

import ast
import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite.list_model import ListModel, Row  # noqa: E402
from amc_wx_lite.navigation import SessionId  # noqa: E402
from amc_wx_lite.podcast_source import (  # noqa: E402
    PodcastDescription,
    PodcastSubscription,
)
from amc_wx_lite.host_client import HostUnavailable, LiteHostClient  # noqa: E402


def _sync_submit(_name, work, done, failed):
    try:
        done(work())
    except Exception as error:
        failed(error)


def _state(row: Row):
    model = ListModel()
    model.replace([row])
    return SimpleNamespace(model=model, library_return_id=None)


def test_alt_d_opens_native_description_and_copies_only_user_text() -> None:
    row = Row("private-episode-id", "Odcinek próbny", "episode")
    state = _state(row)
    spoken: list[str] = []
    copied: list[str] = []
    shown: list[PodcastDescription] = []
    information = PodcastDescription(
        window_title="Opis odcinka",
        text="Treść opisu.\n\nOdcinek podcastu\nTytuł: Odcinek próbny",
        initial_focus_name="Treść opisu.",
    )

    class Dialog:
        def __init__(self, _parent, received, copy_text):
            shown.append(received)
            self._copy_text = copy_text

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):  # noqa: N802 - wx API
            assert self._copy_text(information.text)
            return gui.wx.ID_CANCEL

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(
            description=lambda item_id, item_kind: information
            if (item_id, item_kind) == ("private-episode-id", "episode")
            else None
        ),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _to_clipboard=lambda text: copied.append(text) is None,
    )
    original = gui.PodcastDescriptionDialog
    gui.PodcastDescriptionDialog = Dialog
    try:
        gui.LiteFrame._show_podcast_description(frame)
    finally:
        gui.PodcastDescriptionDialog = original

    assert shown == [information]
    assert copied == [information.text]
    assert spoken == ["Skopiowano całą treść"]
    exposed = " ".join(spoken + copied)
    assert "private-episode-id" not in exposed


def test_alt_d_reports_an_empty_episode_description_in_plain_language() -> None:
    row = Row("private-episode-id", "Bez opisu", "episode")
    state = _state(row)
    spoken: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(description=lambda *_args: None),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=spoken.append),
    )

    gui.LiteFrame._show_podcast_description(frame)

    assert spoken == ["Ten odcinek nie zawiera opisu"]


def test_alt_d_fetches_missing_public_youtube_description_without_login() -> None:
    row = Row("private-youtube-id", "Materiał YouTube", "episode")
    state = _state(row)
    shown: list[PodcastDescription] = []
    calls: list[tuple[str, str]] = []
    information = PodcastDescription(
        window_title="Opis materiału YouTube",
        text="YouTube nie udostępnia opisu tego materiału.\n\nMateriał YouTube",
        initial_focus_name="YouTube nie udostępnia opisu tego materiału.",
        metadata_text="Materiał YouTube\nTytuł: Materiał YouTube",
        youtube_address="https://www.youtube.com/watch?v=PublicVideo1",
        youtube_kind="video",
        youtube_fallback="YouTube nie udostępnia opisu tego materiału.",
    )

    class Dialog:
        def __init__(self, _parent, received, _copy_text):
            shown.append(received)

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):  # noqa: N802 - wx API
            return gui.wx.ID_CANCEL

    client = SimpleNamespace(
        youtube_description=lambda *, address, kind: (
            calls.append((address, kind))
            or {"description": "Pełny opis z YouTube.\nDrugi akapit."}
        )
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(description=lambda *_args: information),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=lambda _message: None),
        client=client,
        _to_clipboard=lambda _text: True,
    )
    original = gui.PodcastDescriptionDialog
    gui.PodcastDescriptionDialog = Dialog
    try:
        gui.LiteFrame._show_podcast_description(frame)
    finally:
        gui.PodcastDescriptionDialog = original

    assert calls == [("https://www.youtube.com/watch?v=PublicVideo1", "video")]
    assert shown[0].window_title == "Opis materiału YouTube"
    assert shown[0].text.startswith(
        "Pełny opis z YouTube.\nDrugi akapit.\n\nMateriał YouTube"
    )
    assert shown[0].initial_focus_name == "Pełny opis z YouTube. Drugi akapit."
    assert "private-youtube-id" not in shown[0].text


def test_youtube_description_client_uses_public_read_without_google_login() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {"description": "Opis"}
    )

    result = client.youtube_description(
        address="https://www.youtube.com/watch?v=PublicVideo1",
        kind="video",
    )

    assert result == {"description": "Opis"}
    assert calls == [(
        "youtube.description",
        {
            "address": "https://www.youtube.com/watch?v=PublicVideo1",
            "kind": "video",
        },
        55.0,
    )]
    serialized = repr(calls).casefold()
    assert "login" not in serialized and "token" not in serialized


def test_alt_d_hides_technical_operation_when_youtube_host_is_unavailable() -> None:
    row = Row("private-youtube-id", "Materiał YouTube", "episode")
    state = _state(row)
    shown: list[PodcastDescription] = []
    information = PodcastDescription(
        window_title="Opis materiału YouTube",
        text="YouTube nie udostępnia opisu tego materiału.",
        initial_focus_name="YouTube nie udostępnia opisu tego materiału.",
        metadata_text="Materiał YouTube\nTytuł: Materiał YouTube",
        youtube_address="https://www.youtube.com/watch?v=PublicVideo1",
        youtube_kind="video",
        youtube_fallback="YouTube nie udostępnia opisu tego materiału.",
    )

    class Dialog:
        def __init__(self, _parent, received, _copy_text):
            shown.append(received)

        def __enter__(self):
            return self

        def __exit__(self, *_args):
            return False

        def ShowModal(self):  # noqa: N802 - wx API
            return gui.wx.ID_CANCEL

    def unavailable(**_kwargs):
        raise HostUnavailable(
            "Silnik nie odpowiedział w 55 s na youtube.description."
        )

    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(description=lambda *_args: information),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=lambda _message: None),
        client=SimpleNamespace(youtube_description=unavailable),
        _to_clipboard=lambda _text: True,
    )
    original = gui.PodcastDescriptionDialog
    gui.PodcastDescriptionDialog = Dialog
    try:
        gui.LiteFrame._show_podcast_description(frame)
    finally:
        gui.PodcastDescriptionDialog = original

    exposed = shown[0].text.casefold()
    assert "silnik amc nie odpowiedział" in exposed
    assert "youtube.description" not in exposed
    assert "private-youtube-id" not in exposed


def test_okno_alt_d_uzywa_poczatku_opisu_zamiast_ogolnego_tytulu() -> None:
    source = Path(gui.__file__).read_text(encoding="utf-8")
    tree = ast.parse(source)
    dialog = next(
        node for node in tree.body
        if isinstance(node, ast.ClassDef)
        and node.name == "PodcastDescriptionDialog"
    )
    init = next(
        node for node in dialog.body
        if isinstance(node, ast.FunctionDef) and node.name == "__init__"
    )
    rendered = ast.unparse(init)
    assert "accessible_title = information.initial_focus_name or information.window_title" in rendered
    assert "super().__init__(parent, title=accessible_title)" in rendered
    assert "self.content.SetName(accessible_title)" in rendered


def test_go_to_related_podcast_waits_for_enter_and_reselects_episode() -> None:
    row = Row("private-episode-id", "Wybrany odcinek", "episode")
    state = _state(row)
    opened = []
    choices = []
    spoken: list[str] = []
    subscription = PodcastSubscription(
        "private-parent-id", "Audycja tygodnia", 0
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: state},
        ),
        podcasts=SimpleNamespace(related_podcast=lambda _item_id: subscription),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _open_podcast_view=opened.append,
        _show_relation_destination_window=lambda **kwargs: choices.append(kwargs),
    )

    gui.LiteFrame._go_to_related_podcast(frame)

    assert spoken == []
    assert len(choices) == 1
    assert choices[0]["labels"] == ("Audycja tygodnia, podcast",)
    assert choices[0]["names"] == ("Audycja tygodnia",)
    assert choices[0]["links"] == ("",)
    assert opened == []
    choices[0]["activate"](0)
    assert state.library_return_id == "private-parent-id"
    assert len(opened) == 1
    assert opened[0].subscription_id == "private-parent-id"
    assert opened[0].subscription_title == "Audycja tygodnia"
    assert opened[0].preferred_id == "private-episode-id"


def test_history_episode_names_its_youtube_channel_and_keeps_exact_feed_link() -> None:
    row = Row("episode-id", "Odcinek", "episode")
    subscription = PodcastSubscription(
        "channel-id", "Kanał Ani", 2,
        feed_url="https://www.youtube.com/feeds/videos.xml?channel_id=abc",
    )
    shown = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.PODCASTS,
            sessions={SessionId.PODCASTS: _state(row)},
        ),
        podcasts=SimpleNamespace(related_podcast=lambda _id: subscription),
        runner=SimpleNamespace(submit=_sync_submit),
        announcer=SimpleNamespace(say=lambda _message: None),
        _show_relation_destination_window=lambda **kwargs: shown.append(kwargs),
    )

    gui.LiteFrame._go_to_related_podcast(frame)

    assert shown[0]["labels"] == ("Kanał Ani, kanał YouTube",)
    assert shown[0]["names"] == ("Kanał Ani",)
    assert shown[0]["links"] == (subscription.feed_url,)

