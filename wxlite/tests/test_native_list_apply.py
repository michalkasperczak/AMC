"""Nalozenie planu na ZWYKLA natywna liste: ile i jakich wywolan wx.

Planer (``test_list_sync_plan.py``) mowi CO sie zmienilo. Ten plik sprawdza, co
z tego naprawde trafia do kontrolki -- bo to tam byl objaw: ``SetItemCount`` +
``RefreshItems(0, n-1)`` na kazde odswiezenie.

Atrapa NIE udaje wxWidgets. Zapisuje wywolania, ktorych uzywa nasz kod, i
pilnuje dwoch rzeczy:

1. przy braku zmian NIE MA ANI JEDNEJ operacji na liscie,
2. zmieniony wiersz dostaje ``SetItem``, a nie przebudowe calej listy.

Mowy czytnika to nie dowodzi -- mowe mierzy zywy NVDA (kwity osobno).
"""

from __future__ import annotations

from amc_wx_lite.list_model import ListModel, Row


def track(item_id: str, title: str, detail: str = "") -> Row:
    return Row(
        item_id=item_id, title=title, kind="track", path=f"C:\\m\\{title}.mp3", detail=detail
    )


class FakePlainList:
    """Atrapa zwyklej ``wx.ListCtrl`` (LC_REPORT, BEZ LC_VIRTUAL).

    Trzyma rzeczywista tresc wierszy, zeby test mogl sprawdzic nie tylko LICZBE
    wywolan, ale i to, ze lista po aktualizacji ma wlasciwy tekst.
    """

    def __init__(self, model: ListModel) -> None:
        self.model = model
        self.calls: list[tuple] = []
        self.rows: list[list[str]] = []
        self._selected = -1
        self._focused = -1
        self.frozen = 0

    # --- wywolania, ktorych uzywa nasz kod
    def InsertItem(self, index: int, text: str) -> int:  # noqa: N802 - API wx
        self.calls.append(("InsertItem", index, text))
        self.rows.insert(index, [text, "", ""])
        return index

    def SetItem(self, index: int, column: int, text: str) -> None:  # noqa: N802
        self.calls.append(("SetItem", index, column, text))
        self.rows[index][column] = text

    def DeleteItem(self, index: int) -> None:  # noqa: N802
        self.calls.append(("DeleteItem", index))
        del self.rows[index]

    def DeleteAllItems(self) -> None:  # noqa: N802
        self.calls.append(("DeleteAllItems",))
        self.rows = []

    def GetItemCount(self) -> int:  # noqa: N802
        return len(self.rows)

    def GetFirstSelected(self) -> int:  # noqa: N802
        return self._selected

    def GetFocusedItem(self) -> int:  # noqa: N802
        return self._focused

    def SetItemState(self, index: int, state: int, mask: int) -> None:  # noqa: N802
        self.calls.append(("SetItemState", index, state, mask))
        self._selected = index
        self._focused = index

    def EnsureVisible(self, index: int) -> None:  # noqa: N802
        self.calls.append(("EnsureVisible", index))

    def Freeze(self) -> None:  # noqa: N802
        self.frozen += 1
        self.calls.append(("Freeze",))

    def Thaw(self) -> None:  # noqa: N802
        self.frozen -= 1
        self.calls.append(("Thaw",))

    # --- wygodne skroty dla testow
    @property
    def texts(self) -> list[tuple[str, str, str]]:
        return [tuple(row) for row in self.rows]

    def list_ops(self) -> list[tuple]:
        """Operacje DOTYKAJACE listy. Freeze/Thaw to przemalowanie, nie dane."""
        return [c for c in self.calls if c[0] not in ("Freeze", "Thaw")]


def make_ctrl(model: ListModel, selected: int = -1, focused: int | None = None):
    """Prawdziwe metody ``MediaListCtrl`` na atrapie kontrolki (bez pulpitu)."""
    from test_gui_logic import install_wx_stub

    install_wx_stub()
    from amc_wx_lite.gui import MediaListCtrl

    ctrl = FakePlainList(model)
    ctrl._selected = selected
    ctrl._focused = selected if focused is None else focused
    # Te dwa pola zaklada ``MediaListCtrl.__init__``, ktorego tu nie wolamy (nie
    # ma pulpitu). Trzymamy je zgodne z produkcja, zeby test nie sprawdzal
    # innego kontraktu niz dziala w aplikacji.
    ctrl._shown = []
    ctrl.updating = False
    for name in ("sync_rows", "sync_cursor", "_apply_ops", "fill_initial"):
        setattr(ctrl, name, getattr(MediaListCtrl, name).__get__(ctrl, FakePlainList))
    # ``_cursor_target``/``_move_cursor`` tez z produkcji -- inaczej testowalibysmy
    # wlasna atrape kursora.
    for name in ("_cursor_target", "_move_cursor"):
        setattr(ctrl, name, getattr(MediaListCtrl, name).__get__(ctrl, FakePlainList))
    return ctrl


def seed(ctrl, model: ListModel, rows: list[Row]) -> None:
    """Pierwsze wypelnienie listy przez TEN SAM kod produkcyjny."""
    model.replace(rows)
    ctrl.sync_rows()
    ctrl.calls.clear()


# ---------------------------------------------------- 1. brak zmian = brak operacji


def test_second_sync_without_data_change_touches_the_list_zero_times() -> None:
    """Zatwierdzone wymaganie. Dawniej szlo tu SetItemCount + RefreshItems."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])

    ctrl.sync_rows()

    assert ctrl.list_ops() == [], f"zbedne operacje: {ctrl.list_ops()}"


def test_repeating_sync_many_times_still_does_nothing() -> None:
    """Tick statusu i Ctrl+C wolaja te sama droge co zmiana widoku."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track(str(i), f"Poz {i}") for i in range(300)])

    for _ in range(5):
        ctrl.sync_rows()

    assert ctrl.list_ops() == []


def test_empty_list_sync_is_also_silent() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [])

    ctrl.sync_rows()

    assert ctrl.list_ops() == []


# --------------------------------------------- 2. to samo ID, nowa tresc


def test_renamed_row_with_the_same_id_is_updated_in_place() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])

    model.replace([track("1", "Alfa"), track("2", "Beta nowa")])
    ctrl.sync_rows()

    sets = [c for c in ctrl.calls if c[0] == "SetItem"]
    assert sets == [("SetItem", 1, 0, "Beta nowa")]
    assert not [c for c in ctrl.calls if c[0] in ("DeleteAllItems", "DeleteItem", "InsertItem")]
    assert ctrl.texts[1][0] == "Beta nowa"


def test_changed_detail_updates_only_the_detail_cell() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa", detail="5:03")])

    model.replace([track("1", "Alfa", detail="5:03, ulubione")])
    ctrl.sync_rows()

    assert [c for c in ctrl.calls if c[0] == "SetItem"] == [
        ("SetItem", 0, 2, "5:03, ulubione")
    ]


# ----------------------------------- 3. jedno dodanie/usuniecie w duzej liscie


def test_one_new_row_in_a_big_list_is_one_insert_not_a_rebuild() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    rows = [track(str(i), f"Poz {i}") for i in range(500)]
    seed(ctrl, model, rows)

    model.replace(rows + [track("500", "Poz 500")])
    ctrl.sync_rows()

    inserts = [c for c in ctrl.calls if c[0] == "InsertItem"]
    assert len(inserts) == 1 and inserts[0][1] == 500
    assert not [c for c in ctrl.calls if c[0] == "DeleteAllItems"]
    assert ctrl.GetItemCount() == 501


def test_one_removed_row_in_a_big_list_is_one_delete() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    rows = [track(str(i), f"Poz {i}") for i in range(500)]
    seed(ctrl, model, rows)

    model.replace(rows[:100] + rows[101:])
    ctrl.sync_rows()

    deletes = [c for c in ctrl.calls if c[0] == "DeleteItem"]
    assert deletes == [("DeleteItem", 100)]
    assert ctrl.GetItemCount() == 499


# ------------------------------------------- 4. prawdziwa zmiana calego zbioru


def test_a_whole_new_set_is_replaced_and_the_texts_are_right() -> None:
    """Przejscie 2476 -> 11 (Alt+1 Foldery): tresc MUSI byc nowa.

    To byl objaw \"stara nazwa, nowy licznik\": lista mowila wiersz poprzedniego
    widoku. Zwykla kontrolka trzyma tekst u siebie, wiec sprawdzamy tekst.
    """
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track(f"old{i}", f"Stary {i}") for i in range(2476)])

    model.replace([track(f"new{i}", f"Nowy {i}") for i in range(11)])
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 11
    assert [row[0] for row in ctrl.rows] == [f"Nowy {i}" for i in range(11)]
    assert all("Stary" not in row[0] for row in ctrl.rows), "stara nazwa nie moze zostac"


def test_going_empty_leaves_a_really_empty_control() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])

    model.replace([])
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 0
    assert ctrl.texts == []


def test_small_to_big_transition_fills_every_row() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])

    model.replace([track(f"b{i}", f"Duzy {i}") for i in range(2476)])
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 2476
    assert ctrl.rows[0][0] == "Duzy 0"
    assert ctrl.rows[-1][0] == "Duzy 2475"


# ---------------------------------------------------- 5. fokus i zaznaczenie


def test_cursor_is_set_once_as_one_state_transition() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])
    ctrl._selected = 0
    ctrl._focused = 0

    model.select_id("2")
    ctrl.sync_rows()

    moves = [c for c in ctrl.calls if c[0] == "SetItemState"]
    assert len(moves) == 1, f"jeden kursor = jedno zdarzenie: {moves}"
    assert moves[0][1] == 1
    import wx

    both = wx.LIST_STATE_SELECTED | wx.LIST_STATE_FOCUSED
    assert moves[0][2] == both and moves[0][3] == both


def test_cursor_in_place_is_not_touched() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])
    ctrl._selected = 0
    ctrl._focused = 0

    ctrl.sync_rows()

    assert [c for c in ctrl.calls if c[0] == "SetItemState"] == []


def test_focus_out_of_sync_with_selection_is_repaired() -> None:
    """Fokus i zaznaczenie to OSOBNE wlasciwosci -- sprawdzamy oba."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta"), track("3", "Gamma")])
    model.select_id("2")
    # Zaznaczenie zgadza sie z modelem, ale fokus stoi na innym wierszu.
    ctrl._selected = 1
    ctrl._focused = 2

    ctrl.sync_rows()

    moves = [c for c in ctrl.calls if c[0] == "SetItemState"]
    assert moves and moves[0][1] == 1, (
        "sam GetFirstSelected nie wykrylby rozjazdu fokusu"
    )


def test_selected_id_survives_a_row_inserted_above_it() -> None:
    """Zaznaczony ID zostaje zaznaczony, choc zmienil numer wiersza."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("b", "Beta"), track("c", "Gamma")])
    model.select_id("c")
    ctrl.sync_rows()
    ctrl.calls.clear()

    model.replace([track("a", "Alfa"), track("b", "Beta"), track("c", "Gamma")])
    ctrl.sync_rows()

    assert model.selected_id == "c"
    moves = [c for c in ctrl.calls if c[0] == "SetItemState"]
    assert moves and moves[-1][1] == 2, "kursor idzie za ID, nie za numerem"
    assert ctrl.GetFirstSelected() == 2 and ctrl.GetFocusedItem() == 2


def test_empty_model_does_not_invent_a_cursor() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])

    model.replace([])
    ctrl.sync_rows()

    assert [c for c in ctrl.calls if c[0] == "SetItemState"] == []


# ---------------------------------------------------------- 6. przemalowanie


def test_structural_change_is_wrapped_in_one_freeze_thaw() -> None:
    """Jedno przemalowanie na podmiane, i to TYLKO gdy cos sie zmienia."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])

    model.replace([track(f"n{i}", f"Nowy {i}") for i in range(50)])
    ctrl.sync_rows()

    names = [c[0] for c in ctrl.calls]
    assert names[0] == "Freeze" and names[-1] == "Thaw"
    assert names.count("Freeze") == 1 and names.count("Thaw") == 1
    assert ctrl.frozen == 0, "Thaw musi wrocic nawet przy wyjatku"


def test_nothing_to_do_means_no_freeze_at_all() -> None:
    """Zamrazanie przy braku zmian to tez zbedna operacja na kontrolce."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])

    ctrl.sync_rows()

    assert ctrl.calls == [], f"nic sie nie zmienilo, a bylo: {ctrl.calls}"


# ------------------------- 7. zdarzenia WYBORU w trakcie naszej podmiany
#
# Wstawianie wierszy i ``SetItemState`` wysylaja ``EVT_LIST_ITEM_SELECTED``
# DOKLADNIE TAK SAMO jak ruch uzytkownika. Gdyby handler wpisal przejsciowy
# indeks do modelu, swiadomy wybor uzytkownika przepadlby przy kazdym
# odswiezeniu. Bramka to ``MediaListCtrl.updating``.


def test_the_control_marks_itself_as_updating_during_a_change() -> None:
    """Handler MUSI miec po czym poznac, ze to nasza podmiana, nie uzytkownik."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])
    widziane: list[bool] = []
    # Podgladamy flage DOKLADNIE w chwili operacji na liscie.
    oryginal = ctrl.InsertItem

    def spy(index: int, text: str) -> int:
        widziane.append(ctrl.updating)
        return oryginal(index, text)

    ctrl.InsertItem = spy  # type: ignore[method-assign]

    model.replace([track("1", "Alfa"), track("2", "Beta")])
    ctrl.sync_rows()

    assert widziane and all(widziane), "w trakcie podmiany flaga musi byc wlaczona"
    assert ctrl.updating is False, "po podmianie flaga MUSI zgasnac"


def test_updating_flag_is_cleared_even_when_applying_fails() -> None:
    """Zawieszona flaga wyciszylaby PRAWDZIWE wybory uzytkownika na stale."""
    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("1", "Alfa")])

    def wybuch(index: int, text: str) -> int:
        raise RuntimeError("awaria w trakcie wstawiania")

    ctrl.InsertItem = wybuch  # type: ignore[method-assign]
    model.replace([track("1", "Alfa"), track("2", "Beta")])

    try:
        ctrl.sync_rows()
    except RuntimeError:
        pass

    assert ctrl.updating is False
    assert ctrl.frozen == 0, "Thaw tez musi wrocic"


def test_a_transient_index_during_replacement_does_not_become_the_selection() -> None:
    """Pelny lancuch: zdarzenie z NASZEJ podmiany nie rusza wyboru modelu.

    Odtwarzamy to, co robi wx: przy kazdym ``InsertItem``/``SetItemState``
    wolamy ten sam handler okna, ktory w produkcji siedzi na
    ``EVT_LIST_ITEM_SELECTED``.
    """
    from test_gui_logic import install_wx_stub

    install_wx_stub()
    from amc_wx_lite.gui import LiteFrame, MediaListCtrl

    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("a", "Alfa"), track("b", "Beta"), track("c", "Gamma")])
    model.select_id("c")
    ctrl.sync_rows()
    ctrl.calls.clear()

    class FakeEvent:
        def __init__(self, index: int, source) -> None:
            self._index = index
            self._source = source

        def GetIndex(self) -> int:  # noqa: N802 - API wx
            return self._index

        def GetEventObject(self):  # noqa: N802 - API wx
            return self._source

        def Skip(self) -> None:  # noqa: N802 - API wx
            pass

    class FakeFrame:
        """Tylko to, czego dotyka handler wyboru."""

        def __init__(self, nav) -> None:
            self.navigator = nav

    class FakeNav:
        def __init__(self, session) -> None:
            self.session = session

    class FakeSession:
        def __init__(self, model) -> None:
            self.model = model

    frame = FakeFrame(FakeNav(FakeSession(model)))
    handler = LiteFrame._on_item_selected.__get__(frame, FakeFrame)

    # Handler jest wolany z KAZDEJ operacji podmiany, tak jak robi to wx.
    oryginal_insert = ctrl.InsertItem
    oryginal_state = ctrl.SetItemState

    def insert_z_zdarzeniem(index: int, text: str) -> int:
        wynik = oryginal_insert(index, text)
        handler(FakeEvent(index, ctrl))
        return wynik

    def state_z_zdarzeniem(index: int, state: int, mask: int) -> None:
        oryginal_state(index, state, mask)
        handler(FakeEvent(index, ctrl))

    ctrl.InsertItem = insert_z_zdarzeniem  # type: ignore[method-assign]
    ctrl.SetItemState = state_z_zdarzeniem  # type: ignore[method-assign]
    # Atrapa musi byc widziana jako nasza kontrolka -- handler rozpoznaje typ
    # przez ``isinstance``. Dziedziczymy po OBU, zeby nie zgubic metod atrapy.
    ctrl.__class__ = type("FakeMediaList", (FakePlainList, MediaListCtrl), {})

    # Nowy, wiekszy zbior: wiersz "c" ma inny numer, wiec przez podmiane
    # przechodzi wiele przejsciowych indeksow (0, 1, 2, ...).
    model.replace(
        [track("x", "Nowy"), track("a", "Alfa"), track("b", "Beta"), track("c", "Gamma")]
    )
    ctrl.sync_rows()

    assert model.selected_id == "c", (
        "przejsciowy indeks z naszej podmiany nadpisal wybor uzytkownika"
    )
    assert ctrl.GetFirstSelected() == 3 and ctrl.GetFocusedItem() == 3


def test_a_real_user_selection_still_reaches_the_model() -> None:
    """Bramka nie moze wyciszyc PRAWDZIWEGO ruchu uzytkownika."""
    from test_gui_logic import install_wx_stub

    install_wx_stub()
    from amc_wx_lite.gui import LiteFrame, MediaListCtrl

    model = ListModel()
    ctrl = make_ctrl(model)
    seed(ctrl, model, [track("a", "Alfa"), track("b", "Beta")])
    ctrl.__class__ = type("FakeMediaList", (FakePlainList, MediaListCtrl), {})
    ctrl.updating = False  # poza podmiana

    class FakeEvent:
        def GetIndex(self) -> int:  # noqa: N802
            return 1

        def GetEventObject(self):  # noqa: N802
            return ctrl

        def Skip(self) -> None:  # noqa: N802
            pass

    class FakeFrame:
        def __init__(self, nav) -> None:
            self.navigator = nav

    class FakeNav:
        def __init__(self, session) -> None:
            self.session = session

    class FakeSession:
        def __init__(self, model) -> None:
            self.model = model

    frame = FakeFrame(FakeNav(FakeSession(model)))
    LiteFrame._on_item_selected.__get__(frame, FakeFrame)(FakeEvent())

    assert model.selected_id == "b", "strzalka uzytkownika MUSI zmienic wybor"


# ------------------------------- 8. zmiana danych W CZASIE odczytu wiersza
#
# Kontrolowana zmiana z WLASNEGO modelu w tescie -- zaden zapis do profilu
# uzytkownika. Chodzi o to, czy po podmianie w locie kursor i tresc nadal
# wskazuja TEN SAM element.


def test_a_change_while_the_reader_is_on_a_row_keeps_that_row_current() -> None:
    model = ListModel()
    ctrl = make_ctrl(model)
    rows = [track(str(i), f"Poz {i}") for i in range(200)]
    seed(ctrl, model, rows)
    model.select_id("120")
    ctrl.sync_rows()
    ctrl.calls.clear()

    # W tej samej chwili: dochodzi wiersz PRZED kursorem i zmienia sie nazwa
    # wiersza, na ktorym stoi czytnik.
    zmienione = list(rows)
    zmienione[120] = track("120", "Poz 120 po zmianie nazwy")
    model.replace([track("nowy", "Dopisany")] + zmienione)
    ctrl.sync_rows()

    assert model.selected_id == "120"
    # Kursor idzie za ID: wiersz 120 jest teraz 121.
    assert ctrl.GetFirstSelected() == 121 and ctrl.GetFocusedItem() == 121
    # I ma NOWY tekst, nie stary.
    assert ctrl.rows[121][0] == "Poz 120 po zmianie nazwy"
    # Reszta listy nie zostala przepisana.
    assert len([c for c in ctrl.calls if c[0] == "InsertItem"]) == 1
