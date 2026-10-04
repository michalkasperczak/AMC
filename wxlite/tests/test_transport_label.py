"""Etykieta przycisku transportu ma mowic, CO ZROBI naciśniecie.

Przycisk nazywal sie "Pauza/wznow" -- czyli wymienial oba warianty naraz,
zamiast podac czynnosc zalezna od stanu. Czytnik odczytywal wiec zawsze to
samo i uzytkownik nie wiedzial, czy wlasnie wstrzyma, czy wznowi.

Wzorzec bierzemy ze ZWYKLEGO AMC, nie wymyslamy nowego slownictwa:

    ``MainWindow.xaml.cs:2560``
    ``PlayerPlayPauseButton.Content =``
    ``    preparing ? "Anuluj" : session.IsPlaying ? "Wstrzymaj" : "Odtwórz";``

    ``MainWindow.xaml.cs:2565``
    ``var action = preparing ? "Anuluj otwieranie"``
    ``    : session.IsPlaying ? "Wstrzymaj" : "Odtwórz";``

Czyli: gra -> "Wstrzymaj", nie gra -> "Odtwórz". Skrot zostaje Spacja
(``MainWindow.xaml:720-721`` "Odtwórz lub wstrzymaj, Spacja").
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

from amc_wx_lite.gui import transport_button_label, transport_confirmation  # noqa: E402


def test_label_tells_what_pressing_will_do() -> None:
    """Odwrotnie niz stan: gra -> wstrzymasz, stoi -> odtworzysz."""
    assert transport_button_label(playing=True) == "&Wstrzymaj"
    assert transport_button_label(playing=False) == "&Odtwórz"


def test_label_never_lists_both_actions_at_once() -> None:
    """Regresja wprost na dawne "Pauza/wznow"."""
    for playing in (True, False):
        label = transport_button_label(playing=playing)
        assert "/" not in label
        assert not ("strzymaj" in label and "dtwórz" in label)


def test_preparing_offers_cancelling_like_the_regular_app() -> None:
    """MainWindow.xaml.cs:2560 -- w trakcie otwierania przycisk anuluje."""
    assert transport_button_label(playing=False, preparing=True) == "&Anuluj"
    assert transport_button_label(playing=True, preparing=True) == "&Anuluj"


def test_confirmation_reports_the_state_that_was_reached() -> None:
    """Etykieta mowi o PRZYSZLOSCI, potwierdzenie o TERAZ -- to nie to samo."""
    assert transport_confirmation(paused=True) == "Wstrzymano"
    assert transport_confirmation(paused=False) == "Odtwarzanie"


def test_confirmation_is_short_enough_to_not_annoy() -> None:
    for paused in (True, False):
        assert len(transport_confirmation(paused=paused).split()) == 1
