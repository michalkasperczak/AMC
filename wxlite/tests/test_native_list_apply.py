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
        # Pola zakladane przez ``MediaListCtrl.__init__``, ktorego tu nie wolamy.
        self._shown: list = []
        #: Sentinel jak w produkcji: "jeszcze nic nie pokazano".
        from amc_wx_lite.gui import _BRAK_KONTEKSTU

        self._shown_context: object = _BRAK_KONTEKSTU
        #: Brak sesji = brak wiedzy o widoku. Testy bramki podstawiaja tu
        #: ``FakeState`` i wtedy ``_view_context`` zwraca prawdziwa tozsamosc.
        self.state = None
        self.updating = False
        #: Filtr listy. Pusty = atrapa zachowuje sie tak jak PRZED dodaniem
        #: filtrowania, wiec wszystkie dotychczasowe pomiary mechanizmu list
        #: mierza dokladnie to samo co wczesniej.
        self.filter_query: str = ""

    def HasFocus(self) -> bool:  # noqa: N802 - API wx
        """Domyslnie BEZ fokusu: zapowiedz pustki dotyczy tylko listy pod reka."""
        return False

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
        # ZMIERZONE na zywej kontrolce (laboratorium W02, tryb ``deleteall``):
        # czyszczenie calej listy zostawia ``fokus=-1 wybor=-1``. Atrapa, ktora
        # trzymalaby stary indeks, pozwolilaby przejsc kodowi liczacemu kursor
        # po nieistniejacym wierszu.
        self._selected = -1
        self._focused = -1

    def GetItemCount(self) -> int:  # noqa: N802
        return len(self.rows)

    def GetFirstSelected(self) -> int:  # noqa: N802
        return self._selected

    def GetFocusedItem(self) -> int:  # noqa: N802
        return self._focused

    def GetItemState(self, index: int, _mask: int) -> int:  # noqa: N802
        """Jak wxMSW: pytanie o nieistniejacy wiersz jest bledem testu."""
        assert 0 <= index < len(self.rows), "GetItemState poza lista"
        import wx

        return wx.LIST_STATE_SELECTED if index == self._selected else 0

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
    for name in ("sync_rows", "sync_cursor", "_apply_ops", "fill_initial",
                 "_plan_usuwa_fokus_i_wstawia", "_wymienia_caly_widok",
                 "_view_context"):
        setattr(ctrl, name, getattr(MediaListCtrl, name).__get__(ctrl, FakePlainList))
    # ``_cursor_target``/``_move_cursor`` tez z produkcji -- inaczej testowalibysmy
    # wlasna atrape kursora.
    for name in ("_cursor_target", "_move_cursor", "_wanted_visible_index",
                 "_is_index_selected",
                 "shown_item_id", "visible_count"):
        setattr(ctrl, name, getattr(MediaListCtrl, name).__get__(ctrl, FakePlainList))
    return ctrl


def seed(ctrl, model: ListModel, rows: list[Row]) -> None:
    """Pierwsze wypelnienie listy przez TEN SAM kod produkcyjny."""
    model.replace(rows)
    ctrl.sync_rows()
    ctrl.calls.clear()


# ---------------------------------------------------- 1. brak zmian = brak operacji


def test_initial_fill_never_queries_state_of_a_row_not_yet_inserted() -> None:
    """Regresja z zywego wxMSW: ``GetItemState(0)`` na pustej kontrolce rzuca.

    Model wybiera pierwszy element przed wypelnieniem widoku. Plan kursora ma
    najpierw uznac go za jeszcze niezaznaczony, potem wstawic wiersz i dopiero
    wtedy zapytac/ustawic jego stan.
    """
    model = ListModel()
    model.replace([track("1", "Alfa")])
    ctrl = make_ctrl(model)

    ctrl.sync_rows()

    assert ctrl.texts == [("Alfa", "utwór", "")]
    assert ctrl.GetFirstSelected() == 0


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
            self.menu_refreshes = 0

        def _refresh_menu_state(self) -> None:
            # Prawdziwe okno przelicza bramki ``needs_selection`` po KAZDYM
            # zaznaczeniu (gui.py:2011). Atrapa musi miec ta metode, inaczej
            # test mowi o AttributeError, a nie o tym, co bada.
            self.menu_refreshes += 1

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

# -------------------- 9. BRAMKA ZAKRESU: pelna podmiana tylko na zmianie widoku
#
# Pelna podmiana (``DeleteAllItems`` + wstawienie calego ``desired``) jest
# DOZWOLONA przy rzeczywistej zmianie calego widoku (Foldery -> Wszystkie pliki,
# wejscie w folder, zawartosc playlisty). NIE jest dozwolona na zwykle usuniecie
# jednego elementu i wstawienie w TYM SAMYM widoku -- tam obowiazuje diff.
#
# Te testy pilnuja wlasnie tej granicy, bo sama heurystyka "plan usuwa wiersz z
# fokusem i cos wstawia" jej NIE odrozniala.


class FakeState:
    """Minimalny odpowiednik ``navigation.SessionState`` dla bramki widoku.

    Tylko pola, ktore czyta ``navigation.view_context``. ``library_view=None``
    jest POPRAWNYM kontraktem widoku Folderow, nie brakiem danych.
    """

    def __init__(self, session_id: str = "files", library_view=None,
                 library_playlist_id=None, library_item_id=None,
                 folder_path: str = "C:\\m") -> None:
        self.session_id = session_id
        self.library_view = library_view
        self.library_playlist_id = library_playlist_id
        self.library_item_id = library_item_id
        self.folder_path = folder_path


def make_ctrl_ze_stanem(model: ListModel, state: FakeState, selected: int = -1):
    ctrl = make_ctrl(model, selected=selected)
    ctrl.state = state
    return ctrl


def test_usuniecie_wiersza_z_fokusem_w_tym_samym_widoku_zostaje_punktowe() -> None:
    """Zwykle usuniecie + wstawienie BEZ zmiany widoku: zadnego DeleteAllItems.

    To jest jawny zakres uzytkownika. Gdyby bramka opierala sie tylko na
    ukladzie planu (usuwa wiersz z fokusem i wstawia), ten przypadek
    przebudowywalby cala liste -- i ten test byl by czerwony.
    """
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    rows = [track(str(i), f"Poz {i}") for i in range(40)]
    seed(ctrl, model, rows)
    model.select_id("10")
    ctrl.sync_rows()
    ctrl.calls.clear()

    # Usuwamy wiersz pod kursorem i jednoczesnie dopisujemy nowy. WIDOK TEN SAM.
    zmienione = [r for r in rows if r.item_id != "10"] + [track("nowy", "Dopisany")]
    model.replace(zmienione)
    ctrl.sync_rows()

    rodzaje = [c[0] for c in ctrl.list_ops()]
    assert "DeleteAllItems" not in rodzaje, (
        f"pelna podmiana w TYM SAMYM widoku jest zabroniona; operacje: {rodzaje}")
    # I nadal wszystkie wiersze sa na liscie, z wlasciwa trescia.
    assert ctrl.GetItemCount() == len(zmienione)
    assert ctrl.rows[0][0] == "Poz 0"


def test_rzeczywista_zmiana_widoku_wymienia_cala_liste_jednym_czyszczeniem() -> None:
    """Foldery -> Wszystkie pliki: wolno jedno DeleteAllItems + pelne wstawienie.

    Zmierzony powod: ``DeleteItem`` na sfokusowanym wierszu emituje przejsciowy
    FOCUS na dziecko, ktorego juz nie bedzie (NVDA czytal wiersz dwa razy);
    ``DeleteAllItems`` nie emituje nic.
    """
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    seed(ctrl, model, [track(str(i), f"Folder {i}") for i in range(30)])
    model.select_id("5")
    ctrl.sync_rows()
    ctrl.calls.clear()

    # RZECZYWISTA zmiana widoku: inna tozsamosc w sesji + inne dane.
    state.library_view = "all_files"
    nowe = [track(f"t{i}", f"Utwor {i}") for i in range(50)]
    model.replace(nowe)
    ctrl.sync_rows()

    rodzaje = [c[0] for c in ctrl.list_ops()]
    assert rodzaje.count("DeleteAllItems") == 1, f"operacje: {rodzaje}"
    assert "DeleteItem" not in rodzaje, "po pelnym czyszczeniu nie ma co usuwac pojedynczo"
    # WSZYSTKIE wiersze i WSZYSTKIE kolumny -- bez paginacji i obcinania.
    assert ctrl.GetItemCount() == 50
    assert ctrl.rows[49][0] == "Utwor 49"
    assert len([c for c in ctrl.calls if c[0] == "InsertItem"]) == 50


def test_zmiana_widoku_bez_usuwania_fokusu_nie_siega_po_pelna_podmiane() -> None:
    """Zmiana widoku, ale plan nie usuwa wiersza z fokusem: zostaje przyrostowo.

    Bez przejsciowego zdarzenia fokusu pelna podmiana nic nie naprawia, a
    kosztuje przebudowe calej listy.
    """
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    rows = [track(str(i), f"Poz {i}") for i in range(20)]
    seed(ctrl, model, rows)
    # Fokus na wierszu, ktory PRZETRWA zmiane.
    model.select_id("0")
    ctrl.sync_rows()
    ctrl.calls.clear()

    state.folder_path = "C:\\m\\inny"
    model.replace(rows + [track("nowy", "Dopisany")])
    ctrl.sync_rows()

    rodzaje = [c[0] for c in ctrl.list_ops()]
    assert "DeleteAllItems" not in rodzaje, f"operacje: {rodzaje}"
    assert rodzaje.count("InsertItem") == 1


def test_zmiana_widoku_bez_zmiany_danych_to_nadal_zero_operacji() -> None:
    """Pusta sync w nowym widoku o identycznych wierszach: ANI JEDNEJ operacji.

    Pelna podmiana nie moze stac sie wymowka do przemalowania listy, gdy plan
    jest pusty. Dotyczy to TAKZE syncow, ktore loader odpala po zmianie
    ``library_view``, ale PRZED oddaniem danych.
    """
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])
    ctrl.calls.clear()

    state.library_view = "all_files"
    ctrl.sync_rows()

    assert ctrl.list_ops() == [], f"zbedne operacje: {ctrl.list_ops()}"


def test_po_pelnej_podmianie_kursor_wraca_na_wybrane_ID() -> None:
    """Po wymianie widoku wybor idzie za ID, a nie za indeksem."""
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    seed(ctrl, model, [track(str(i), f"Folder {i}") for i in range(10)])
    model.select_id("3")
    ctrl.sync_rows()
    ctrl.calls.clear()

    state.library_view = "all_files"
    nowe = [track(f"t{i}", f"Utwor {i}") for i in range(10)] + [track("3", "Stary trzy")]
    model.replace(nowe)
    ctrl.sync_rows()

    # ID "3" nadal istnieje -- i to ono ma byc wybrane, na nowej pozycji 10.
    assert model.selected_id == "3"
    assert ctrl.GetFirstSelected() == 10 and ctrl.GetFocusedItem() == 10
    assert ctrl.rows[10][0] == "Stary trzy"


def test_bez_wiedzy_o_widoku_nigdy_nie_ma_pelnej_podmiany() -> None:
    """``state=None`` to brak wiedzy o widoku, a nie dowod jego zmiany."""
    model = ListModel()
    ctrl = make_ctrl(model)  # bez stanu sesji
    rows = [track(str(i), f"Poz {i}") for i in range(20)]
    seed(ctrl, model, rows)
    model.select_id("7")
    ctrl.sync_rows()
    ctrl.calls.clear()

    model.replace([r for r in rows if r.item_id != "7"] + [track("nowy", "Dopisany")])
    ctrl.sync_rows()

    assert "DeleteAllItems" not in [c[0] for c in ctrl.list_ops()]


def test_pusty_widok_po_zmianie_kontekstu_nie_laduje_w_pelnej_podmianie() -> None:
    """Przejscie do PUSTEGO widoku: nie ma czego wstawiac, zapowiedz pustki dziala.

    Naprawa pustej listy ma zostac nietknieta.
    """
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])
    model.select_id("1")
    ctrl.sync_rows()
    ctrl.calls.clear()

    state.library_view = "playlists"
    state.library_playlist_id = "p1"
    model.replace([])
    ctrl.sync_rows()

    assert ctrl.GetItemCount() == 0
    # Bez wstawien: pelna podmiana wymaga czegos do wstawienia.
    assert not [c for c in ctrl.calls if c[0] == "InsertItem"]

def test_sync_przed_oddaniem_danych_nie_zuzywa_zmiany_widoku() -> None:
    """ASYNCHRONICZNY LOADER: kontekst nalezy do danych, nie do zamiaru.

    ZMIERZONA kolejnosc po Alt+2 (kwit ``odbior-w02valid``): najpierw leci sync
    z ZEROWYM planem, bo sesja ma juz ``library_view=ALL_FILES``, a loader
    jeszcze nie oddal wierszy (11 == 11). Dopiero nastepny sync wnosi dane
    (11 -> 2476).

    Gdyby ten pierwszy, bezczynny przebieg zapisal nowy kontekst jako
    "pokazany", prawdziwa aktualizacja danych wygladalaby na te sama tozsamosc
    widoku i poszla diffem -- czyli ``DeleteItem`` na wierszu z fokusem, a to
    jest wlasnie przyczyna podwojnego odczytu NVDA.
    """
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    seed(ctrl, model, [track(str(i), f"Folder {i}") for i in range(11)])
    model.select_id("0")
    ctrl.sync_rows()
    ctrl.calls.clear()

    # 1. Zamiar: widok juz przestawiony, danych jeszcze nie ma.
    state.library_view = "all_files"
    ctrl.sync_rows()
    assert ctrl.list_ops() == [], "sync bez zmiany danych nie dotyka listy"

    # 2. Loader oddaje dane TEGO NOWEGO widoku.
    model.replace([track(f"t{i}", f"Utwor {i}") for i in range(2476)])
    ctrl.sync_rows()

    rodzaje = [c[0] for c in ctrl.list_ops()]
    assert rodzaje.count("DeleteAllItems") == 1, (
        "pelna podmiana MUSI zadzialac na przebiegu, ktory wnosi dane nowego "
        f"widoku; operacje: {set(rodzaje)}")
    assert "DeleteItem" not in rodzaje
    assert ctrl.GetItemCount() == 2476


def test_identical_loaded_rows_commit_view_before_next_incremental_change() -> None:
    """Gotowy wynik loadera moze miec identyczne dane jak poprzedni widok."""
    model = ListModel()
    state = FakeState()
    ctrl = make_ctrl_ze_stanem(model, state)
    rows = [track(str(i), f"Poz {i}") for i in range(3)]
    seed(ctrl, model, rows)

    state.library_view = "all_files"
    ctrl.sync_rows()  # Sam zamiar, loader jeszcze nie zakonczony.
    model.replace(rows)  # Zakonczony loader: ten sam wynik, nowy zbior danych.
    ctrl.calls.clear()
    ctrl.sync_rows()
    assert ctrl.list_ops() == [], "identyczne dane nie wymagaja operacji kontrolki"

    model.replace([track("new", "Nowy")] + rows[1:])
    ctrl.calls.clear()
    ctrl.sync_rows()
    ops = ctrl.list_ops()
    assert not any(op[0] == "DeleteAllItems" for op in ops), (
        "gotowy widok nie moze przy kolejnej punktowej zmianie udawac zmiany widoku"
    )
    assert sum(op[0] == "DeleteItem" for op in ops) == 1
    assert sum(op[0] == "InsertItem" for op in ops) == 1
    assert ctrl.GetItemCount() == 3
