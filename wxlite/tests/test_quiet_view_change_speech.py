"""Mowa przy ZMIANIE widoku: bez starej nazwy i bez wlasnego dublu wiersza.

Zmierzone na zywym NVDA (kwit rodzica
``library-gui-after422/parent-acceptance/odbior-widokow.json``, gesty A02/A04/B01).
Trzy OSOBNE objawy, kazdy z wlasna przyczyna w kodzie:

1. ``A02-Alt2``  -- pierwsza wypowiedz to STARY wiersz poprzedniego widoku
   ("Kazania Dominikanie Grobla ... 1 z 10"), dopiero potem nowy widok.
   ``B01-Alt2`` pokazuje te sama przyczyne jeszcze wyrazniej: stara NAZWA z
   NOWA dlugoscia -- "Biskup; Rodzaj: playlista ... **1 z 2475**". Licznik byl
   juz nowy, a tekst wiersza stary, czyli ``SetItemCount`` przestawil dlugosc
   listy, ale NIE uniewaznil cache tekstu wierszy wirtualnej kontrolki.
   wxWidgets odswieza ``OnGetItemText`` dopiero na ``RefreshItems``.

2. ``A04-CtrlU`` -- nowy wiersz wymowiony DWA razy
   ("Emu; ... 1 z 9" dwukrotnie). ``sync_selection`` wolalo ``Select(index)``
   ORAZ ``Focus(index)``: dwa osobne wywolania API, dwa zdarzenia MSAA, dwa
   odczyty tego samego elementu. Natywne chodzenie strzalkami tego nie robi,
   bo kontrolka zmienia zaznaczenie i fokus JEDNYM przejsciem stanu.

3. Wszystkie gesty zmiany widoku -- nasza wlasna zapowiedz zawiera tytul
   pierwszego wiersza, ktory natywna lista i tak wymawia zaraz po niej. To
   jest ta "wlasna nadmiarowa zapowiedz tego samego elementu": czytamy
   element dwa razy roznymi slowami.

Czego te testy NIE wymagaja: absolutnego zera standardowych zdarzen czytnika.
Lista ma dalej mowic, na czym stoi kursor -- ale raz i aktualnie.
"""

from __future__ import annotations

from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.navigation import Announce, LibraryView, Navigator, SessionId


def track(item_id: str, title: str) -> Row:
    return Row(item_id=item_id, title=title, kind="track", path=f"C:\\m\\{title}.mp3")


# ----------------------------------------------------- 1. stara nazwa z cache


class FakeVirtualList:
    """Atrapa wirtualnego ``wx.ListCtrl`` o interfejsie, ktorego uzywamy.

    Nie udaje wxWidgets: zapisuje kolejnosc wywolan, zeby dalo sie sprawdzic,
    czy po zmianie dlugosci listy uniewazniamy tekst wierszy, i czy stan
    zaznaczenia przestawiamy JEDNYM przejsciem.
    """

    def __init__(self, model: ListModel) -> None:
        self.model = model
        self.calls: list[tuple] = []
        self._selected = -1

    # --- API wx, ktore woła nasz kod
    def SetItemCount(self, count: int) -> None:  # noqa: N802 - API wx
        self.calls.append(("SetItemCount", count))

    def RefreshItems(self, first: int, last: int) -> None:  # noqa: N802
        self.calls.append(("RefreshItems", first, last))

    def RefreshItem(self, index: int) -> None:  # noqa: N802
        self.calls.append(("RefreshItem", index))

    def GetFirstSelected(self) -> int:  # noqa: N802
        return self._selected

    def Select(self, index: int, on: int = 1) -> None:  # noqa: N802
        self.calls.append(("Select", index))
        self._selected = index

    def Focus(self, index: int) -> None:  # noqa: N802
        self.calls.append(("Focus", index))

    def SetItemState(self, index: int, state: int, mask: int) -> None:  # noqa: N802
        self.calls.append(("SetItemState", index, state, mask))
        self._selected = index

    def EnsureVisible(self, index: int) -> None:  # noqa: N802
        self.calls.append(("EnsureVisible", index))


def make_ctrl(model: ListModel, selected: int = -1):
    """Zbuduj realny ``MediaListCtrl`` z podmieniona baza wx (bez pulpitu)."""
    from test_gui_logic import install_wx_stub

    install_wx_stub()
    from amc_wx_lite.gui import MediaListCtrl

    ctrl = FakeVirtualList(model)
    ctrl._selected = selected
    # Metody klasy produkcyjnej na atrapie kontrolki: testujemy NASZ kod,
    # nie wxWidgets.
    ctrl.sync_length = MediaListCtrl.sync_length.__get__(ctrl, FakeVirtualList)
    ctrl.sync_selection = MediaListCtrl.sync_selection.__get__(ctrl, FakeVirtualList)
    return ctrl


def test_new_length_also_invalidates_the_old_row_text() -> None:
    """Objaw 1: "Biskup ... 1 z 2475" -- nowy licznik, stara nazwa.

    ``SetItemCount`` nie kaze kontrolce zapytac modelu o tekst ponownie, wiec
    czytnik dostaje wiersz POPRZEDNIEGO widoku. Po zmianie dlugosci MUSI pojsc
    uniewaznienie zakresu.
    """
    model = ListModel()
    model.replace([track("1", "Alfa"), track("2", "Beta")])
    ctrl = make_ctrl(model)

    ctrl.sync_length()

    names = [c[0] for c in ctrl.calls]
    assert "SetItemCount" in names
    assert "RefreshItems" in names, (
        "bez RefreshItems kontrolka oddaje czytnikowi tekst z cache, "
        "czyli wiersz poprzedniego widoku"
    )
    assert names.index("SetItemCount") < names.index("RefreshItems")
    assert ("RefreshItems", 0, 1) in ctrl.calls


def test_empty_list_is_not_refreshed_out_of_range() -> None:
    """Pusty widok nie moze wolac ``RefreshItems(0, -1)``."""
    ctrl = make_ctrl(ListModel())
    ctrl.sync_length()
    assert [c for c in ctrl.calls if c[0] == "RefreshItems"] == []


# ------------------------------------------- 2. jeden element, jeden odczyt


def test_selection_change_is_one_state_transition_not_two_events() -> None:
    """Objaw 2: "Emu ... 1 z 9" dwa razy.

    ``Select`` + ``Focus`` to dwa wywolania i dwa zdarzenia MSAA. Zaznaczenie
    i fokus maja sie przestawic JEDNYM przejsciem stanu, tak jak przy
    natywnym chodzeniu strzalkami.
    """
    model = ListModel()
    model.replace([track("1", "Emu"), track("2", "Piosenka")])
    # Kursor stal na pozycji 1 poprzedniego widoku -- dokladnie sytuacja A04.
    ctrl = make_ctrl(model, selected=1)

    ctrl.sync_selection()

    moves = [c for c in ctrl.calls if c[0] in ("Select", "Focus", "SetItemState")]
    assert len(moves) == 1, f"jeden element = jedno zdarzenie, a bylo: {moves}"
    assert moves[0][0] == "SetItemState"
    assert moves[0][1] == 0
    # Oba bity w JEDNYM wywolaniu: wartosci z ``include/wx/listbase.h``.
    import wx

    both = wx.LIST_STATE_SELECTED | wx.LIST_STATE_FOCUSED
    assert moves[0][2] == both and moves[0][3] == both
    # Przewijanie z dawnego ``Focus()`` MUSI zostac (``wx/core.py:2902``).
    assert ("EnsureVisible", 0) in ctrl.calls


def test_selection_already_in_place_touches_nothing() -> None:
    """Gdy kursor juz stoi tam, gdzie model -- zadnego zdarzenia."""
    model = ListModel()
    model.replace([track("1", "Alfa")])
    ctrl = make_ctrl(model, selected=0)
    ctrl.sync_selection()
    assert ctrl.calls == []


# --------------------------------- 3. bez wlasnej zapowiedzi tego samego wiersza


def test_view_announcement_does_not_repeat_the_row_the_list_will_speak() -> None:
    """Objaw 3: naglowek + liczba zostaja, tytul wiersza NIE.

    Natywna kontrolka wymawia wiersz pod kursorem sama (i robi to pelniej:
    z kolumnami i pozycja "1 z N"). Nasza zapowiedz ma dodac to, czego
    kontrolka nie powie -- nazwe widoku i rozmiar listy.
    """
    nav = Navigator()
    rows = [track("1", "Alfa"), track("2", "Beta")]
    tasks = nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", rows)
    said = [t.text for t in tasks if isinstance(t, Announce)]
    assert len(said) == 1
    message = said[0]
    assert "Wszystkie pliki" in message
    assert "2" in message
    assert "Alfa" not in message, (
        "tytul wiersza wymawia natywna lista; powtorzenie go to drugi odczyt "
        "tego samego elementu"
    )
    # Stan modelu bez zmian: wybor nadal po ID.
    assert nav.sessions[SessionId.FILES].model.selected_id == "1"


def test_empty_view_still_says_it_is_empty() -> None:
    """Pusta lista NIE wymowi nic sama, wiec tu zapowiedz jest konieczna."""
    nav = Navigator()
    tasks = nav.apply_library_view(LibraryView.FAVORITES, "Ulubione", [])
    said = [t.text for t in tasks if isinstance(t, Announce)][0]
    assert "Ulubione" in said and "pusto" in said.lower()


def test_folder_announcement_does_not_repeat_the_row_either() -> None:
    """Ctrl+O i wejscie w folder mialy ten sam dubel (gest C00d)."""
    nav = Navigator()
    rows = [track("f1", "ton-440hz-180s")]
    tasks = nav.apply_folder("D:\\test", rows)
    said = [t.text for t in tasks if isinstance(t, Announce)][0]
    assert "test" in said
    assert "ton-440hz-180s" not in said


def test_empty_folder_says_it_is_empty() -> None:
    nav = Navigator()
    said = [t.text for t in nav.apply_folder("D:\\puste", []) if isinstance(t, Announce)][0]
    assert "pust" in said.lower()
