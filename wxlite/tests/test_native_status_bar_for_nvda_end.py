"""NVDA+End ma czytac PASEK STANU, a nie ostatnie slowo tytulu okna.

ZGLOSZENIE (zmierzone zywym NVDA przez rodzica)
-----------------------------------------------
``NVDA+Up`` czytal poprawnie element fokusu, ale ``NVDA+End`` zamiast statusu
mowil ostatnie slowo tytulu okna, np. "wieczorne" z "Uspokojenie wieczorne".
To samo w widoku odtwarzacza po ``F6``.

DLACZEGO TAK BYLO
-----------------
``NVDA+End`` czyta PASEK STANU okna. NVDA szuka go natywnie: albo bierze
ostatnie dziecko okna, albo -- gdy nie znajdzie kontrolki paska stanu --
sieka tytul okna i oddaje jego koniec. Nasze okno NIE MIALO paska stanu:
status byl zwyklym ``wx.StaticText`` z nazwa "Komunikaty" wewnatrz panelu
(``gui.py``, ``_build_ui``). ``wx.StaticText`` to okno klasy ``Static``
z rola ``ROLE_SYSTEM_STATICTEXT`` -- nie jest paskiem stanu, wiec NVDA
spadal na tytul okna. Komunikat byl widoczny i mowiony przy zmianie
(live region), ale NIE BYL do odczytania NA ZADANIE.

ORYGINAL C# MA DWA KANALY -- I MY TEZ MAMY MIEC DWA
---------------------------------------------------
Zwykle AMC trzyma je rozdzielnie, nie zamiast siebie:

  ``Controls/AccessibleStatusTextBlock.cs``
      WPF ``TextBlock`` z notyfikacja o TRESCI -- kanal MOWY przy zdarzeniu.
      To odpowiednik naszego ``Announcer`` + ``status_field``.

  ``Controls/AccessiblePlaybackStatusStrip.cs`` + ``MainWindow.xaml:20-22``
      NATYWNY ``StatusStrip`` (WinForms) w ``WindowsFormsHost``,
      ``DockPanel.Dock="Bottom"``, ``Focusable="False"``,
      ``KeyboardNavigation.IsTabStop="False"``, ``TabStop = false``,
      ``SetStyle(ControlStyles.Selectable, false)``.
      To kanal ODCZYTU NA ZADANIE -- wprost "It must remain available to
      NVDA+End, but it must never become the keyboard target".

Stad zakres tej poprawki: DOKLADAMY natywny pasek stanu obok istniejacego
kanalu mowy. Nie usuwamy ``Announcer``, nie zmieniamy jego komunikatow ani
czasow i NIE przejmujemy zadnego gestu czytnika -- pasek stanu jest zwykla
natywna kontrolka, ktora NVDA umie znalezc sam.

BEZ SPAMU -- to jawna regula oryginalu, nie nasza ostroznosc
-----------------------------------------------------------
``AccessiblePlaybackStatusStrip.SpokenText`` celowo NIE wysyla zdarzenia
zmiany nazwy przy kazdej aktualizacji:

    "Do not emit a NameChange event every second: for a native control
     hosted inside WPF that event can move NVDA's navigator away from the
     player even though WPF still reports logical focus there."

Pasek stanu ma wiec tylko PRZECHOWYWAC aktualny tekst. Mowa zostaje tam,
gdzie byla zmierzona jako dzialajaca: w ``Announcer`` (live region).

Pulpitu tu nie ma (WSL), wiec sprawdzamy to, co da sie sprawdzic maszynowo:
kontrakt ``NativeStatusBar``, jego podlaczenie do ``Announcer`` oraz to, ze
okno buduje PRAWDZIWY pasek stanu ramki, a nie kolejna etykiete. Zywy odczyt
``NVDA+End`` i ``F6`` zostaje dla rodzica.
"""

from __future__ import annotations

import ast
from pathlib import Path

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui  # noqa: E402  (zastepnik wx musi byc pierwszy)

GUI_SOURCE = Path(gui.__file__).read_text(encoding="utf-8")


class FakeStatusField:
    """Atrapa ``wx.StaticText``: tekst, nazwa i uchwyt okna."""

    def __init__(self) -> None:
        self.label = ""
        self.name = ""

    def GetLabel(self) -> str:  # noqa: N802 - API wx
        return self.label

    def SetLabel(self, text: str) -> None:  # noqa: N802
        self.label = text

    def SetName(self, text: str) -> None:  # noqa: N802
        self.name = text

    def GetHandle(self) -> int:  # noqa: N802
        return 4242


class FakeNativeBar:
    """Atrapa ``wx.StatusBar``: liczymy KAZDE wywolanie ``SetStatusText``."""

    def __init__(self) -> None:
        self.text = ""
        self.writes: list[tuple[str, int]] = []

    def SetStatusText(self, text: str, number: int = 0) -> None:  # noqa: N802
        self.text = text
        self.writes.append((text, number))

    def GetStatusText(self, number: int = 0) -> str:  # noqa: N802
        return self.text


def make_announcer(*, active: bool = True):
    field = FakeStatusField()
    bar = FakeNativeBar()
    events: list[tuple] = []

    # ``Announcer`` pyta o okno nadrzedne przez ``wx.GetTopLevelParent``.
    import wx

    wx.GetTopLevelParent = lambda _window: type("W", (), {"IsActive": lambda _self: active})()

    announcer = gui.Announcer(
        field,
        notify=lambda *args: events.append(args),
        status_bar=gui.NativeStatusBar(bar),
    )
    return announcer, field, bar, events


# ------------------------------------------- 1. kontrakt samego paska stanu


def test_native_status_bar_keeps_the_current_text_for_on_demand_reading() -> None:
    bar = FakeNativeBar()
    native = gui.NativeStatusBar(bar)
    native.show("Skopiowano nazwę")
    assert bar.GetStatusText(0) == "Skopiowano nazwę"


def test_native_status_bar_writes_only_when_the_text_really_changed() -> None:
    """Bez tego kazde tykniecie zegara pisalo by do paska bez potrzeby."""
    bar = FakeNativeBar()
    native = gui.NativeStatusBar(bar)
    native.show("pauza, 0 s")
    native.show("pauza, 0 s")
    native.show("pauza, 0 s")
    assert bar.writes == [("pauza, 0 s", 0)], (
        "powtorzony ten sam tekst nie jest zmiana statusu"
    )


def test_native_status_bar_never_raises_a_screen_reader_event() -> None:
    """Pasek stanu PRZECHOWUJE tekst. Mowa idzie kanalem ``Announcer``.

    Regula wprost z ``AccessiblePlaybackStatusStrip.cs``: zdarzenie zmiany
    nazwy co sekunde potrafi przestawic nawigator czytnika.
    """
    source = ast.parse(GUI_SOURCE)
    klass = next(
        node
        for node in source.body
        if isinstance(node, ast.ClassDef) and node.name == "NativeStatusBar"
    )
    called = {
        node.func.id
        for node in ast.walk(klass)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
    }
    assert "_notify_win_event" not in called
    body = ast.get_source_segment(GUI_SOURCE, klass) or ""
    assert "LIVEREGION" not in body.upper()
    assert "NotifyWinEvent" not in body


def test_missing_native_bar_does_not_break_announcements() -> None:
    """Brak paska (atrapy w testach, stare wywolania) nie moze wywalic mowy."""
    field = FakeStatusField()
    import wx

    wx.GetTopLevelParent = lambda _window: type("W", (), {"IsActive": lambda _self: True})()
    events: list[tuple] = []
    gui.Announcer(field, notify=lambda *a: events.append(a)).say("Gotowe")
    assert field.label == "Gotowe"
    assert len(events) == 1


# ------------------------------------- 2. podlaczenie do istniejacej bramy


def test_announced_message_also_lands_in_the_native_status_bar() -> None:
    """Tresc z dolu okna musi byc do ODCZYTANIA, nie tylko do uslyszenia."""
    announcer, field, bar, events = make_announcer()
    announcer.say("Skopiowano plik i pełną ścieżkę")
    assert field.label == "Skopiowano plik i pełną ścieżkę"
    assert bar.GetStatusText(0) == "Skopiowano plik i pełną ścieżkę"


def test_announcer_still_speaks_exactly_once_per_message() -> None:
    """Dolozenie paska nie moze zdublowac zapowiedzi."""
    announcer, _field, _bar, events = make_announcer()
    announcer.say("Skopiowano nazwę")
    assert len(events) == 1
    assert events[0][0] == gui.EVENT_OBJECT_LIVEREGIONCHANGED


def test_repeated_question_is_answered_again_in_speech() -> None:
    """Ctrl+Shift+E dwa razy = dwie odpowiedzi. Zachowane bez zmian."""
    announcer, _field, bar, events = make_announcer()
    announcer.say("3:51")
    announcer.say("3:51")
    assert len(events) == 2, "powtorzone pytanie ma dostac odpowiedz dwa razy"
    assert bar.writes == [("3:51", 0)], "ale pasek stanu nie zmienil tresci"


def test_empty_message_changes_nothing() -> None:
    announcer, field, bar, events = make_announcer()
    announcer.say("   ")
    assert field.label == ""
    assert bar.writes == []
    assert events == []


def test_background_window_updates_the_bar_but_stays_silent() -> None:
    """Okno w tle nie wchodzi w slowo obcej aplikacji -- regula bez zmian."""
    announcer, field, bar, events = make_announcer(active=False)
    announcer.say("Nic nie jest odtwarzane")
    assert field.label == "Nic nie jest odtwarzane"
    assert bar.GetStatusText(0) == "Nic nie jest odtwarzane"
    assert events == [], "w tle nie mowimy"


# ----------------------------------- 3. okno buduje PRAWDZIWY pasek ramki


def _build_ui_source() -> str:
    tree = ast.parse(GUI_SOURCE)
    frame = next(
        node
        for node in ast.walk(tree)
        if isinstance(node, ast.ClassDef) and node.name == "LiteFrame"
    )
    build = next(
        node
        for node in frame.body
        if isinstance(node, ast.FunctionDef) and node.name == "_build_ui"
    )
    return ast.get_source_segment(GUI_SOURCE, build) or ""


def test_window_creates_a_real_frame_status_bar_not_another_label() -> None:
    """``CreateStatusBar`` daje natywny ``msctls_statusbar32`` ramki.

    Dowolna kolejna ``wx.StaticText`` nie naprawilaby ``NVDA+End``, bo to nie
    jest kontrolka paska stanu.
    """
    build = _build_ui_source()
    assert "self.CreateStatusBar(" in build, (
        "bez natywnego paska stanu NVDA+End dalej sieka tytul okna"
    )
    assert "NativeStatusBar(" in build
    assert "status_bar=" in build, "Announcer musi dostac pasek, inaczej nic go nie pisze"


def test_status_bar_is_not_a_keyboard_target() -> None:
    """Oryginal: "it must never become the keyboard target"."""
    build = _build_ui_source()
    assert "wx.STB_DEFAULT_STYLE" in build or "CreateStatusBar(1" in build
    # Pasek stanu ramki wx nie jest w kolejnosci tabulacji ani nie przyjmuje
    # fokusu; sprawdzamy, ze NIE probujemy go do niej wpisac.
    assert "self.status_bar.SetFocus" not in build
    assert "SetCanFocus(True)" not in build


def test_existing_message_field_and_announcer_survive() -> None:
    """Kanal mowy zostaje: nie wymieniamy dzialajacego na nowy."""
    build = _build_ui_source()
    assert "self.status_field = wx.StaticText(" in build
    assert 'self.status_field.SetName("Komunikaty")' in build
    assert "self.announcer = Announcer(" in build
