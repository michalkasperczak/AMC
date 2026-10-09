"""Mowa przy ZMIANIE widoku: bez wlasnego dublu wiersza w zapowiedzi.

Zmierzone na zywym NVDA (kwit rodzica
``library-gui-after422/parent-acceptance/odbior-widokow.json``, gesty A02/A04/B01).
Byly trzy OSOBNE objawy, kazdy z wlasna przyczyna w kodzie:

1. ``A02-Alt2`` / ``B01-Alt2`` -- pierwsza wypowiedz to STARY wiersz
   poprzedniego widoku, a nawet stara NAZWA z NOWA dlugoscia:
   "Biskup; Rodzaj: playlista ... **1 z 2475**".

2. ``A04-CtrlU`` -- nowy wiersz wymowiony DWA razy ("Emu; ... 1 z 9"
   dwukrotnie), bo dawna ``sync_selection`` wolala ``Select(index)`` ORAZ
   ``Focus(index)``: dwa wywolania API, dwa zdarzenia MSAA, dwa odczyty.

3. Wszystkie gesty zmiany widoku -- nasza wlasna zapowiedz zawierala tytul
   pierwszego wiersza, ktory natywna lista i tak wymawia zaraz po niej. To ta
   "wlasna nadmiarowa zapowiedz tego samego elementu": element czytany dwa razy
   roznymi slowami.

GDZIE SA TERAZ OBJAWY 1 i 2. Ten plik sprawdza JUZ TYLKO objaw 3 -- zapowiedzi
z ``navigation``. Objawy 1 i 2 lezaly w WIRTUALNEJ kontrolce i razem z nia
zniknely:

* Objaw 1 bral sie z ``SetItemCount`` bez ``RefreshItems``: licznik wirtualnej
  listy szedl do przodu, a tekst wierszy zostawal w cache kontrolki. Zwykla
  lista nie ma takiego cache -- tekst siedzi w kontrolce i zmienia sie
  dokladnie wtedy, gdy go nadpiszemy. Testy tamtej pary wywolan zostaly
  USUNIETE, bo utrwalaly wymaganie, ktorego JUZ NIE MA (wirtualizacji).
  Swiadomie nie przepisano ich tak, zeby dalej swiecily na zielono pod
  ``LC_VIRTUAL``: zmienilo sie wymaganie, wiec zmienil sie test.
* Objaw 2 (jedno przejscie stanu zamiast ``Select`` + ``Focus``) obowiazuje
  DALEJ i jest sprawdzany tam, gdzie teraz mieszka kod kursora:
  ``test_native_list_apply.py::test_cursor_is_set_once_as_one_state_transition``
  i ``...::test_cursor_in_place_is_not_touched``.

Czego te testy NIE wymagaja: absolutnego zera standardowych zdarzen czytnika.
Lista ma dalej mowic, na czym stoi kursor -- ale raz i aktualnie.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import Announce, LibraryView, Navigator, SessionId


def track(item_id: str, title: str) -> Row:
    return Row(item_id=item_id, title=title, kind="track", path=f"C:\\m\\{title}.mp3")


# --------------------------------- bez wlasnej zapowiedzi tego samego wiersza


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


def test_escape_from_player_leaves_the_focused_row_to_the_native_list() -> None:
    """Bez własnego „Lista, nazwa”; SysListView32 przeczyta wiersz raz."""
    nav = Navigator()
    rows = [track("1", "Alfa"), track("2", "Beta")]
    nav.apply_folder("D:\\test", rows, preferred_id="2")
    nav.activate_selected()

    events = nav.back_to_list()

    assert events == []
    assert nav.sessions[SessionId.FILES].model.selected_id == "2"


def test_escape_from_player_still_reports_an_empty_list_when_rows_vanished() -> None:
    """Pusta kontrolka nie ma elementu, który NVDA mógłby przeczytać sama."""
    nav = Navigator()
    nav.apply_folder("D:\\test", [track("1", "Alfa")])
    nav.activate_selected()
    nav.sessions[SessionId.FILES].model.replace([])

    events = nav.back_to_list()

    assert [event.text for event in events if isinstance(event, Announce)] == [
        "Lista, zero elementów"
    ]
