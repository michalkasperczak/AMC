"""Wyjscie z PUSTEGO widoku Biblioteki klawiatura + brak podwojnego odczytu.

Dwa osobne defekty, oba zmierzone na zywym NVDA (kwity: native-lists-after422
oraz native-list-gaps-after422). Tu sa zapisane jako testy, zeby nie wrocily.

------------------------------------------------------------------ DEFEKT 1
Objaw (kwit ``odbior-diag-pusto.json``): w pustych ITEM_BOOKMARKS Backspace
NIE wychodzil z widoku, a ``wx-keys-seen.jsonl`` pokazal, ze klawisz w ogole
NIE DOTARL do ``LiteFrame._on_key``. Jednoczesnie ``wx_lista_ma_fokus=true``,
wiec teza "pusta lista nie trzyma fokusu" byla FALSZYWA.

Mechanizm (nie domysl -- wynika z kwitu i z API wx): ``_build_menu`` dokleja
skrot do etykiety po tabulatorze (``"Folder &nadrzędny\tBackspace"``).
wxWidgets parsuje tekst PO TABULATORZE i SAM zaklada akcelerator na poziomie
okna -- mimo komentarza w kodzie, ze "nie rejestrujemy tu akceleratorow".
Akcelerator ma pierwszenstwo przed ``EVT_KEY_DOWN`` kontrolki, dlatego w
kwicie widac TYLKO same modyfikatory (``#306``/``#307``/``#308``) i ``Home``
-- a nigdy ``2``, ``b`` czy ``Backspace``: Alt+2 i Ctrl+Shift+B dzialaly
WLASNIE przez akcelerator, nie przez ``_on_key``.

Gdy pozycja menu jest WYLACZONA (``needs_selection`` + brak wiersza, czyli
dokladnie pusta lista), Windows nadal dopasowuje akcelerator, ale komendy nie
wysyla NIKOMU -- klawisz jest POLKNIETY. Stad "Backspace nie dociera".

Dlatego ``Action.PARENT_FOLDER`` nie moze deklarowac ``needs_selection``:
wyjscie z widoku NIE wymaga zaznaczonego wiersza (``go_to_parent`` w nazwanym
widoku woli ``_leave_library_view`` jeszcze przed szukaniem wiersza rodzica).

------------------------------------------------------------------ DEFEKT 2
Objaw (kwit ``odbior-widoki.json``, gest W02 Alt+2): pierwszy wiersz
"Wszystkie pliki" czytany DWA RAZY po naglowku widoku. W03 (Ulubione) czyta
raz, wiec nie jest to wlasciwosc samej nowej kontrolki.

UWAGA -- ZAKRES TYCH TESTOW. Ponizsze testy pilnuja JEDNEJ, zmierzonej
wlasciwosci: ``sync_rows``/``_move_cursor`` nie wysyla ZBEDNYCH przejsc stanu
(kazde ``SetItemState`` to kolejne ``EVENT_OBJECT_FOCUS`` dla czytnika).
To jest warte pilnowania samo w sobie i jest tu udowodnione na fake.

Czego te testy NIE dowodza: ze objaw W02 zniknal. Zmierzony stan na zywym
NVDA po tej zmianie (kwit ``odbior-powt-w02-*.json``) jest taki, ze pierwszy
wiersz NADAL czytany jest dwa razy, POWTARZALNIE 3/3. Hipoteza "to powtorny
bit FOCUSED" zostala wiec FALSYFIKOWANA pomiarem -- zmiana ``_move_cursor``
redukuje liczbe operacji, ale nie usuwa tego objawu.

Co dodatkowo wiemy z pomiaru (``list-trace.jsonl``): pod obserwatorem ze
sladem wywolan objaw NIE wystepuje, a sam slad pokazuje, ze po ``_apply_ops``
fokus kontrolki stoi na NIEAKTUALNYM indeksie (1090), dopiero potem
``_move_cursor`` przesuwa go na 0. Objaw jest wiec zalezny od czasu, a jego
przyczyna pozostaje OTWARTA -- patrz raport ``RAPORT.md``, sekcja "otwarte".
Nie zgadujemy tu kolejnego wariantu fokusu.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import menu_model
from amc_wx_lite.list_model import ListModel
from amc_wx_lite.navigation import LibraryView, Navigator, OpenLibraryView
from amc_wx_lite.shortcuts import Action

from test_native_list_apply import make_ctrl, seed, track


# ----------------------------------------------------------------- DEFEKT 1


def _item(action: Action) -> menu_model.MenuItem:
    for menu in menu_model.build_menus():
        for entry in menu.items:
            if not entry.is_separator and entry.action is action:
                return entry
    raise AssertionError(f"brak pozycji menu dla {action}")


def test_parent_folder_menu_item_is_never_greyed_out() -> None:
    """Wylaczona pozycja = POLKNIETY Backspace, nie "nieaktywna komenda".

    To jest sedno defektu 1: dopoki ta pozycja moze byc wylaczona, akcelerator
    zjada Backspace na pustej liscie i uzytkownik nie ma jak wyjsc.
    """
    entry = _item(Action.PARENT_FOLDER)
    assert entry.shortcut == "Back", "podpis w menu musi zostac prawda"
    assert not entry.needs_selection, (
        "Backspace musi dzialac takze bez zaznaczenia -- inaczej akcelerator "
        "wylaczonej pozycji polyka klawisz na PUSTEJ liscie"
    )


def test_no_menu_shortcut_that_must_work_without_a_row_is_selection_gated() -> None:
    """Ogolna zasada, nie tylko jeden przypadek.

    Kazdy skrot, ktory ma dzialac na PUSTEJ liscie, musi byc wolny od
    ``needs_selection`` -- bo wx zaklada akcelerator z etykiety, a akcelerator
    wylaczonej pozycji nie przepuszcza klawisza do kontrolki.
    """
    must_work_on_empty = {Action.PARENT_FOLDER}
    for menu in menu_model.build_menus():
        for entry in menu.items:
            if entry.is_separator or entry.action not in must_work_on_empty:
                continue
            assert not entry.needs_selection, entry.label


def test_backspace_leaves_an_empty_bookmarks_view() -> None:
    """Sama nawigacja: pusty widok zakladek MUSI dac sie opuscic.

    Model nie ma zadnego wiersza (``rows=0``), a mimo to Backspace ma wrocic
    do ALL_FILES i stanac na pliku, ktorego zakladki ogladalismy.
    """
    nav = Navigator()
    state = nav.session
    state.library_view = LibraryView.ITEM_BOOKMARKS
    state.library_item_id = "file:C:\\m\\utwor.mp3"
    state.model.replace([])
    assert state.model.selected_row is None, "warunek testu: widok jest PUSTY"

    effects = nav.go_to_parent()

    assert len(effects) == 1, effects
    effect = effects[0]
    assert isinstance(effect, OpenLibraryView), effect
    assert effect.view is LibraryView.ALL_FILES
    assert effect.preferred_id == "file:C:\\m\\utwor.mp3", (
        "powrot musi zachowac IDENTYFIKATOR pliku, nie tylko widok"
    )


# ----------------------------------------------------------------- DEFEKT 2


def _states(ctrl) -> list[tuple]:
    return [c for c in ctrl.calls if c[0] == "SetItemState"]


def test_already_focused_row_is_not_focused_again() -> None:
    """Sedno defektu 2: zero zbednych zdarzen fokusu = zero drugiego odczytu.

    Kontrolka ma JUZ wiersz 0 skupiony i zaznaczony, model chce wiersza 0.
    Zadne ``SetItemState`` nie moze polecieci -- kazde z nich to kolejne
    ``EVENT_OBJECT_FOCUS`` dla czytnika.
    """
    model = ListModel()
    ctrl = make_ctrl(model, selected=0)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])

    ctrl.sync_cursor()

    assert _states(ctrl) == [], f"zbedne zdarzenie fokusu: {_states(ctrl)}"


def test_row_focused_by_the_control_itself_only_gets_the_missing_bit() -> None:
    """Dokladny uklad z gestu W02 Alt+2.

    Po wstawieniu wierszy SysListView32 sam ustawia FOKUS na wiersz 0, ale
    zaznaczenia nie ma. Brakuje WYLACZNIE bitu SELECTED -- i tylko on ma byc
    ustawiony, bo powtorny FOCUSED to drugi odczyt tego samego wiersza.
    """
    import wx

    model = ListModel()
    ctrl = make_ctrl(model, selected=-1, focused=0)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])
    ctrl._selected, ctrl._focused = -1, 0

    ctrl.sync_cursor()

    assert len(_states(ctrl)) == 1, _states(ctrl)
    _, index, state, mask = _states(ctrl)[0]
    assert index == 0
    assert state == wx.LIST_STATE_SELECTED, (
        "ustawiony ma byc TYLKO brakujacy bit zaznaczenia"
    )
    assert mask == wx.LIST_STATE_SELECTED, "maska nie moze ruszac bitu FOCUSED"


def test_row_selected_but_not_focused_only_gets_the_focus_bit() -> None:
    """Przypadek odwrotny -- po usunieciu wiersza kontrolka gubi fokus."""
    import wx

    model = ListModel()
    ctrl = make_ctrl(model, selected=0, focused=-1)
    seed(ctrl, model, [track("1", "Alfa"), track("2", "Beta")])
    ctrl._selected, ctrl._focused = 0, -1

    ctrl.sync_cursor()

    assert len(_states(ctrl)) == 1, _states(ctrl)
    _, index, state, mask = _states(ctrl)[0]
    assert index == 0
    assert state == wx.LIST_STATE_FOCUSED
    assert mask == wx.LIST_STATE_FOCUSED


def test_cursor_far_from_target_still_sets_both_bits() -> None:
    """Zwykly ruch kursora nie moze sie zepsuc: oba bity, jedno przejscie."""
    import wx

    model = ListModel()
    ctrl = make_ctrl(model, selected=5)
    seed(ctrl, model, [track(str(i), f"Poz {i}") for i in range(10)])
    ctrl._selected, ctrl._focused = 5, 5
    model.replace([track(str(i), f"Poz {i}") for i in range(10)], preferred_id="2")

    ctrl.sync_cursor()

    both = wx.LIST_STATE_SELECTED | wx.LIST_STATE_FOCUSED
    assert _states(ctrl) == [("SetItemState", 2, both, both)], _states(ctrl)


def test_big_view_change_moves_the_cursor_exactly_once() -> None:
    """Zmiana 10 -> 2475 wierszy: JEDNO przejscie stanu, nie dwa.

    Odwzorowanie gestu W02 w skali, ktora da sie policzyc w tescie.
    """
    model = ListModel()
    ctrl = make_ctrl(model, selected=0)
    seed(ctrl, model, [track(f"f{i}", f"Folder {i}") for i in range(10)])
    # Kontrolka po wstawieniu sama trzyma fokus na wierszu 0.
    ctrl._selected, ctrl._focused = -1, 0

    model.replace([track(f"t{i}", f"Utwor {i:04d}") for i in range(600)])
    ctrl.sync_rows()

    assert len(_states(ctrl)) <= 1, (
        f"kazde dodatkowe zdarzenie fokusu to kolejny odczyt: {_states(ctrl)}"
    )
