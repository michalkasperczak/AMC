"""PUSTA lista musi dac sie ZIDENTYFIKOWAC przez czytnik ekranu.

Co to za defekt (zmierzony, nie wywnioskowany)
----------------------------------------------
Na zywym NVDA wejscie do widoku bez wierszy dawalo ``name=''`` i ``role=0``
("nieznane"), mimo ze sama kontrolka ma ``accName`` i ``accRole=33``
(``kwit-ab/pusta-ab.json``: ``accRole=33`` nawet BEZ ``MediaListAccessible``).

A/B na tej samej ``MediaListCtrl`` (``kwit-ab/pusta-przyczyna.json``) pokazalo
roznice wobec kontrolki standardowej: nasza droga do zera to petla
``DeleteItem``, a kazde usuniecie wiersza PRZED kursorem przesuwa fokus
SysListView32 na nizszy indeks, wiec na kontrolce konczacej z 0 wierszami
poszly ``EVENT_OBJECT_FOCUS`` z ``idChild=2``, a potem ``idChild=1`` -- na
dzieci, ktorych po oproznieniu NIE MA (``get_accChild(1)`` zwraca
``0x80070057``). ``DeleteAllItems`` tej serii nie wysyla wcale: ostatnim
zdarzeniem zostaje wtedy fokus z czasow niepustej listy.

W obu drogach konczymy wiec BEZ zdarzenia opisujacego stan faktyczny: lista ma
fokus klawiatury (``GetFocus() == HWND``), a dziecka z fokusem nie ma
(``accFocus`` VT_EMPTY). Dlatego czytnik trzyma nieistniejace dziecko.

Naprawa: po zejsciu do ZERA wierszy wysylamy JEDNO ``EVENT_OBJECT_FOCUS`` na
``CHILDID_SELF`` -- zdarzenie odpowiadajace realnej zmianie dostepnego obiektu,
nie falszowanie fokusu. Fokus klawiatury juz tam jest; mowimy tylko prawde o
tym, co teraz jest dostepnym obiektem.

Ten plik mierzy KONTRAKT: kiedy zdarzenie leci, a kiedy NIE. Mowy nie dowodzi --
to robi zywy NVDA (kwit ``odbior-pusto-*.json``).
"""

from __future__ import annotations

from amc_wx_lite.list_model import ListModel, Row
from test_native_list_apply import FakePlainList, make_ctrl, seed, track


class NotifyingList(FakePlainList):
    """Atrapa, ktora ZAPISUJE zdarzenia MSAA wyslane przez nasz kod."""

    def __init__(self, model: ListModel) -> None:
        super().__init__(model)
        self.events: list[tuple[int, int, int, int]] = []
        self._focused_hwnd = True

    def GetHandle(self) -> int:  # noqa: N802 - API wx
        return 0x1234

    def HasFocus(self) -> bool:  # noqa: N802
        return self._focused_hwnd


def make_notifying(model: ListModel, selected: int = -1):
    ctrl = make_ctrl(model)
    ctrl.__class__ = type("FakeNotifyingList", (NotifyingList,), {})
    ctrl.events = []
    ctrl._focused_hwnd = True
    ctrl._selected = selected
    ctrl._focused = selected
    ctrl._notify = lambda event, hwnd, obj, child: ctrl.events.append(
        (event, hwnd, obj, child))
    return ctrl


def rows(count: int) -> list[Row]:
    return [track(str(index), "Poz %d" % index) for index in range(count)]


# ------------------------------------------- 1. zejscie do zera = JEDNO zdarzenie


def test_emptying_the_list_announces_the_list_itself_once() -> None:
    """Rdzen naprawy. Bez tego czytnik zostaje na usunietym dziecku."""
    from amc_wx_lite import gui

    model = ListModel()
    ctrl = make_notifying(model)
    seed(ctrl, model, rows(5))
    model.select_id("2")
    ctrl.sync_rows()
    ctrl.events.clear()

    model.replace([], preferred_id=None)
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 0
    assert ctrl.events == [
        (gui.EVENT_OBJECT_FOCUS, 0x1234, gui.OBJID_CLIENT, gui.CHILDID_SELF)
    ], "pusta lista musi oglosic SAMA SIEBIE dokladnie raz: %r" % (ctrl.events,)


def test_the_announcement_targets_child_self_not_a_removed_row() -> None:
    """Zmierzony objaw: zdarzenia szly na idChild=2, potem 1 -- na nieistniejace."""
    from amc_wx_lite import gui

    model = ListModel()
    ctrl = make_notifying(model)
    seed(ctrl, model, rows(3))
    ctrl.events.clear()

    model.replace([], preferred_id=None)
    ctrl.sync_rows()

    child_ids = [event[3] for event in ctrl.events]
    assert child_ids == [gui.CHILDID_SELF], (
        "zdarzenie o nieistniejacym wierszu: %r" % (child_ids,))


# ---------------------------------------------- 2. kiedy zdarzenia byc NIE MOZE


def test_a_non_empty_result_sends_no_extra_event() -> None:
    """Niepusta lista mowi przez wiersz. Dodatkowe zdarzenie = podwojny odczyt."""
    model = ListModel()
    ctrl = make_notifying(model)
    seed(ctrl, model, rows(5))
    ctrl.events.clear()

    model.replace(rows(2), preferred_id="0")
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 2
    assert ctrl.events == []


def test_staying_empty_does_not_repeat_the_announcement() -> None:
    """Tick statusu wola ta sama droge. Pusto -> pusto to ZERO zdarzen."""
    model = ListModel()
    ctrl = make_notifying(model)
    seed(ctrl, model, [])
    ctrl.events.clear()

    for _ in range(4):
        ctrl.sync_rows()

    assert ctrl.events == []


def test_first_fill_with_an_empty_view_announces_once() -> None:
    """Wejscie od razu w pusty widok tez musi byc zidentyfikowane."""
    from amc_wx_lite import gui

    model = ListModel()
    ctrl = make_notifying(model)
    model.replace([], preferred_id=None)
    ctrl.events.clear()

    ctrl.fill_initial()

    assert ctrl.events == [
        (gui.EVENT_OBJECT_FOCUS, 0x1234, gui.OBJID_CLIENT, gui.CHILDID_SELF)
    ]


def test_no_event_when_the_list_does_not_have_keyboard_focus() -> None:
    """Zapowiedz dotyczy TEGO, co uzytkownik ma pod reka.

    Lista w ukrytym panelu tez przechodzi przez ``sync_rows``; zdarzenie fokusu
    z kontrolki bez fokusu byloby zdarzeniem o czyms, czego nie ma.
    """
    model = ListModel()
    ctrl = make_notifying(model)
    seed(ctrl, model, rows(4))
    ctrl._focused_hwnd = False
    ctrl.events.clear()

    model.replace([], preferred_id=None)
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 0
    assert ctrl.events == []


# ------------------------------------------------- 3. awaria MSAA nie wywraca GUI


def test_a_failing_notify_does_not_break_the_update() -> None:
    """Zapowiedz jest DODATKIEM: lista musi sie opruznic nawet gdy MSAA padnie."""
    model = ListModel()
    ctrl = make_notifying(model)
    seed(ctrl, model, rows(3))

    def failing(*_args):
        raise OSError("NotifyWinEvent padlo")

    ctrl._notify = failing

    model.replace([], preferred_id=None)
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 0
    assert ctrl._shown == []
