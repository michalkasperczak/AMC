"""Etykiety wyboru OPML są wyłącznie tekstem użytkowym dla NVDA."""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

from amc_wx_lite.gui import PodcastOpmlImportDialog  # noqa: E402


def _dialog_without_window() -> PodcastOpmlImportDialog:
    dialog = object.__new__(PodcastOpmlImportDialog)
    dialog._entries = [
        {"label": "Podcast Alfa, alfa.example", "feedUrl": "https://alfa.example/rss"},
        {"label": "Podcast Beta, beta.example", "feedUrl": "https://beta.example/rss"},
    ]
    dialog._included = [True, False]
    return dialog


def test_opml_choice_labels_explain_state_without_leaking_model_objects() -> None:
    dialog = _dialog_without_window()
    assert dialog._label(0) == "Podcast Alfa, alfa.example, zaznaczony"
    assert dialog._label(1) == "Podcast Beta, beta.example, niezaznaczony"
    combined = dialog._label(0) + dialog._label(1)
    assert "{" not in combined and "feedUrl" not in combined


def test_opml_selected_values_remain_separate_from_spoken_labels() -> None:
    dialog = _dialog_without_window()
    assert dialog.selected_feed_urls == ["https://alfa.example/rss"]
