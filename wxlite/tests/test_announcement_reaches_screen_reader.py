"""Dlaczego krotkie potwierdzenia BYLY CICHE, mimo poprawnego wywolania wx.

MINIMALNA PRZYCZYNA, ODCZYTANA ZE ZRODEL NVDA (nie zgadnieta z objawu):

1. ``EVENT_SYSTEM_ALERT`` (0x2) NVDA tlumaczy na zdarzenie ``"alert"``:
   ``source/IAccessibleHandler/internalWinEventHandler.py:37``
       ``winUser.EVENT_SYSTEM_ALERT: "alert"``

2. Obsluga tego zdarzenia ODRZUCA obiekt, ktory nie jest alertem:
   ``source/NVDAObjects/IAccessible/__init__.py:2025-2028``
       ``def event_alert(self):``
       ``    if self.role != controlTypes.Role.ALERT:``
       ``        # Ignore alert events on objects that aren't alerts.``
       ``        return``

3. Zwykly ``wx.StaticText`` to okno klasy ``Static`` z rola MSAA
   ``ROLE_SYSTEM_STATICTEXT``, ktore NVDA mapuje na nakladke ``StaticText``:
   ``source/NVDAObjects/IAccessible/__init__.py:2842``
       ``("Static", oleacc.ROLE_SYSTEM_STATICTEXT): "StaticText"``
   a ``ROLE_SYSTEM_STATICTEXT`` to ``Role.STATICTEXT``, nie ``Role.ALERT``
   (``source/IAccessibleHandler/__init__.py:119`` mapuje na ALERT WYLACZNIE
   ``oleacc.ROLE_SYSTEM_ALERT``).

=> Wywolanie bylo poprawne, obiekt byl zly. NVDA wracal z ``event_alert``
w pierwszej linii i NIC nie mowil. Dlatego "schowek rzeczywiscie poprawny,
a mowy nie bylo": kopiowanie dzialalo, zapowiedz nie miala jak dojsc.

DROGA, KTORA DZIALA (tez ze zrodel NVDA, bez dodatku i bez restartu):

   ``source/IAccessibleHandler/internalWinEventHandler.py:58``
       ``winUser.EVENT_OBJECT_LIVEREGIONCHANGED: "liveRegionChange"``
   ``source/NVDAObjects/__init__.py:1238-1254``
       ``def event_liveRegionChange(self):``
       ``    name = self.name``
       ``    if name:``
       ``        ... ui.message(name, ...)``

``event_liveRegionChange`` NIE sprawdza roli -- wymaga tylko NIEPUSTEJ nazwy.
Nazwa zwyklego ``wx.StaticText`` to jego tekst, ktory ``Announcer.say`` juz
ustawia. Zadna nakladka NVDA tego nie przeslania (``event_liveRegionChange``
istnieje wylacznie w klasie bazowej ``NVDAObject``), wiec ``ui.message``
wymawia DOKLADNIE podany tekst -- bez slowa "alert", bez nazwy regionu.

To ta sama intencja, co w zwyklym AMC: ``AccessibleStatusTextBlock.Announce``
wysyla notyfikacje UIA z TRESCIA komunikatu, a peer bierze nazwe z tekstu
(``Controls/AccessibleStatusTextBlock.cs:26-37, 44-48``). WPF ma
``RaiseNotificationEvent``; natywne okno Win32 ma ``NotifyWinEvent``.

CZEGO TU NIE MA: ``sleep``, restartu NVDA, dodatku, appModule, wymuszania
oczekiwanego tekstu przez sonde ani drugiego rownoleglego zdarzenia (dwa
zdarzenia na jeden komunikat to wlasnie podwojna zapowiedz).
"""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import wx  # noqa: E402

from amc_wx_lite.gui import (  # noqa: E402
    CHILDID_SELF,
    EVENT_OBJECT_LIVEREGIONCHANGED,
    EVENT_SYSTEM_ALERT,
    OBJID_CLIENT,
    Announcer,
)

STATUS_HWND = 123456


class Status:
    """Atrapa ``wx.StaticText``: tekst, nazwa i uchwyt okna."""

    def __init__(self) -> None:
        self.label = ""
        self.name = ""
        self.updates = 0

    def SetLabel(self, text: str) -> None:  # noqa: N802 - API wx
        self.label = text
        self.updates += 1

    def GetLabel(self) -> str:  # noqa: N802 - API wx
        return self.label

    def SetName(self, text: str) -> None:  # noqa: N802 - API wx
        self.name = text

    def GetHandle(self) -> int:  # noqa: N802 - API wx
        return STATUS_HWND


def capture(active: bool = True):
    """Announcer z PODSTAWIONYM powiadamianiem. Nie udaje mowy NVDA --
    sprawdza tylko, KTORE zdarzenie MSAA i na jakim uchwycie poszlo."""
    sent: list[tuple[int, int, int, int]] = []
    wx.GetTopLevelParent = lambda _control: SimpleNamespace(IsActive=lambda: active)
    status = Status()
    announcer = Announcer(
        status,
        notify=lambda event, hwnd, obj, child: sent.append((event, hwnd, obj, child)),
    )
    return announcer, status, sent


def test_announcement_uses_event_nvda_actually_speaks() -> None:
    """Live region zamiast alertu: NVDA mowi nazwe, bez warunku na role."""
    announcer, status, sent = capture()
    announcer.say("Skopiowano nazwę")
    assert status.label == "Skopiowano nazwę"
    assert status.name == "Skopiowano nazwę", "NVDA czyta NAZWE obiektu, nie sam tekst"
    assert len(sent) == 1, "Jeden komunikat to JEDNO zdarzenie, inaczej mowa sie dubluje"
    event, hwnd, object_id, child_id = sent[0]
    assert event == EVENT_OBJECT_LIVEREGIONCHANGED
    assert hwnd == STATUS_HWND
    assert (object_id, child_id) == (OBJID_CLIENT, CHILDID_SELF)


def test_alert_event_is_not_used_because_static_text_is_not_an_alert() -> None:
    """Regresja wprost: 0x2 na StaticText NVDA odrzuca w event_alert."""
    announcer, _status, sent = capture()
    announcer.say("Pauza")
    assert all(event != EVENT_SYSTEM_ALERT for event, *_ in sent), (
        "NVDAObjects/IAccessible/__init__.py:2026 odrzuca alert na roli STATICTEXT"
    )


def test_repeated_explicit_query_speaks_again_without_rewriting_text() -> None:
    """Ctrl+E dwa razy ma odpowiedziec dwa razy; tekst zmieniamy raz."""
    announcer, status, sent = capture()
    announcer.say("10 sekund")
    announcer.say("10 sekund")
    assert len(sent) == 2
    assert status.updates == 1


def test_background_window_updates_text_but_does_not_interrupt_other_app() -> None:
    announcer, status, sent = capture(active=False)
    announcer.say("Próba")
    assert status.label == "Próba" and not sent


def test_empty_message_is_not_an_announcement() -> None:
    announcer, _status, sent = capture()
    announcer.say("")
    announcer.say("   ")
    assert not sent


def test_failing_notification_does_not_break_the_action() -> None:
    """Zapowiedz jest DODATKIEM: jej awaria nie moze wywrocic kopiowania."""
    status = Status()
    wx.GetTopLevelParent = lambda _control: SimpleNamespace(IsActive=lambda: True)

    def explode(*_args):
        raise OSError("NotifyWinEvent niedostepne")

    Announcer(status, notify=explode).say("Skopiowano nazwę")
    assert status.label == "Skopiowano nazwę"
