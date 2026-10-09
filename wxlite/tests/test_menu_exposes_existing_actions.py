"""Menu udostepnia JUZ ISTNIEJACE akcje -- ta sama droga co klawisz.

Menu to drugi sposob dotarcia do funkcji, nie druga implementacja. Kazda
pozycja musi konczyc sie wywolaniem ``MainWindow._dispatch(Action...)``,
czyli dokladnie tego, co robi skrot klawiszowy. Dzieki temu nie ma szansy
na rozjazd "menu robi co innego niz Ctrl+U".

Co te testy pilnuja:
  * opis menu jest DANYMI (lista pozycji), wiec da sie go sprawdzic bez GUI,
  * kazda pozycja niesie Action, ktora juz istnieje w ``shortcuts.Action``,
  * skrot pokazany w menu jest TYM SAMYM skrotem, ktory dziala naprawde,
  * nazwy i skroty sie nie dubluja,
  * Radio nie trafia tam, gdzie go nie ma (lokalne Ulubione to nie stacje),
  * nie dokladamy pozycji-atrap "niedostepne".
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import menu_model
from amc_wx_lite.shortcuts import (
    LIST_VIEW,
    PLAYER_VIEW,
    PODCAST_INBOX_LIST_VIEW,
    RADIO_LIST_VIEW,
    RADIO_PLAYER_VIEW,
    Action,
)


def all_items() -> list[menu_model.MenuItem]:
    out: list[menu_model.MenuItem] = []
    for menu in menu_model.build_menus():
        out.extend(item for item in menu.items if not item.is_separator)
    return out


def test_every_command_item_carries_an_action_that_already_exists() -> None:
    """Zadna pozycja nie wymysla wlasnej logiki -- kazda ma istniejaca Action."""
    commands = [item for item in all_items() if item.action is not None]
    assert commands, "menu bez polecen byloby bezuzyteczne"
    for item in commands:
        assert isinstance(item.action, Action), item.label


def test_pozycje_menu_NIE_moga_miec_golego_akceleratora_klawisza_edycji() -> None:
    """Regresja ZMIERZONA na zywym GUI (final-recovery, plany D i exp-accel).

    Menu mialo "Folder &nadrzędny\\tBackspace". wx sklada z tekstu po
    tabulatorze AKCELERATOR NA POZIOMIE OKNA, ktory ma pierwszenstwo przed
    kontrolka z fokusem. Skutek zmierzony sonda i NVDA: Backspace w polu
    filtra NIE usuwal znaku (tekst zostal "kaz"), a czytnik mowil "To jest
    folder najwyzszego poziomu" -- poprawianie zapytania wynosilo
    niewidomego uzytkownika z widoku.

    Kontrdowod (plan exp-accel): po zdjeciu TEGO JEDNEGO akceleratora
    Backspace w polu kasuje znak ("kaz" -> "ka", wyniki 1 -> 2, sonda loguje
    ``key=8`` DWA razy), a plan H pokazal, ze Backspace NA LISCIE nadal
    wychodzi do folderu nadrzednego -- bo nawigacja idzie z tablic
    ``shortcuts.LIST_VIEW``, nie z akceleratora menu.

    Dlatego pozycja menu, ktorej skrot jest GOLYM klawiszem uzywanym do
    EDYCJI TEKSTU, nie moze pokazywac tego skrotu jako akceleratora. Sam
    skrot dziala dalej -- obsluguja go tablice skrotow.
    """
    # Klawisze, ktore w polu tekstowym SA edycja, a nie poleceniem.
    edycja = {"Back", "Space", "Delete"}
    winne = [
        item.label for item in all_items()
        if item.shortcut and item.shortcut in edycja and item.accelerator
    ]
    assert not winne, (
        "te pozycje menu tworza goly akcelerator na klawiszu edycji i "
        f"polykaja go w polu filtra: {winne}"
    )


def test_skrot_ktory_ma_dzialac_bez_odtwarzania_nie_moze_byc_bramkowanym_akceleratorem() -> None:
    """Regresja ZMIERZONA na zywym GUI (statusclip-1, plan PASEK-ODTWARZACZ).

    Pozycja "Widok &odtwarzacza\\tF6" byla ``accelerator=True`` ORAZ
    ``needs_playback=True``. Sonda zmierzyla na liscie:
    ``menu.state`` -> ``{"shortcut": "F6", "accel": true, "enabled": false}``,
    keylog ``hook.key`` -> ``code 345`` (F6) DOCHODZI do okna, a mimo to
    ``dispatch`` NIE pojawia sie wcale i widok zostaje ``View.LIST``.

    To jest polkniecie przez akcelerator WYLACZONEJ pozycji: Windows dopasowuje
    akcelerator okna, ale komendy nie wysyla nikomu, wiec klawisz nie dochodzi
    tez do ``_on_key``. Objaw udaje utrate fokusu (sonda pokazuje
    ``list_has_focus: true``), a nia nie jest.

    ``shortcuts.LIST_VIEW["F6"] == Action.SHOW_PLAYER`` jest BEZWARUNKOWE --
    F6 ma przechodzic do odtwarzacza takze gdy nic nie gra. Skrot bramkowany
    stanem nie moze wiec wisiec jako akcelerator na pozycji menu.

    ZAKRES TEJ REGULY jest wezszy niz "kazda bramkowana pozycja". Pozycje typu
    "Skopiuj adres" (``Ctrl+Shift+C``) wymagaja ZAZNACZENIA i gdy nic nie jest
    zaznaczone, polkniecie klawisza nie zabiera uzytkownikowi niczego -- nie ma
    czego kopiowac. Natomiast PRZELACZANIE WIDOKU musi dzialac zawsze, bo to
    jedyna droga wyjscia; dlatego mierzymy wlasnie je.
    """
    from amc_wx_lite import shortcuts

    przelaczanie_widoku = {
        Action.SHOW_PLAYER,
        Action.SHOW_LIST,
        Action.VIEW_PODCAST_IN_PROGRESS,
    }
    winne = [
        (item.label, item.shortcut)
        for item in all_items()
        if item.action in przelaczanie_widoku
        and item.shortcut
        and item.accelerator
        and (item.needs_playback or item.needs_selection or item.needs_radio_session)
    ]
    assert not winne, (
        "przelaczanie widoku wisi jako akcelerator bramkowany stanem -- gdy "
        f"pozycja jest wylaczona, polyka swoj klawisz i widok sie nie zmienia: {winne}"
    )
    # Kontrdowod, ze regula mierzy to, co trzeba: tablica skrotow listy
    # oferuje F6 BEZ zadnego warunku odtwarzania.
    assert shortcuts.LIST_VIEW["F6"] is Action.SHOW_PLAYER


def test_shortcut_shown_in_menu_is_the_shortcut_that_really_works() -> None:
    """Menu nie moze obiecywac skrotu, ktorego nie ma w tablicach skrotow.

    To najwazniejszy test tego pliku: chroni przed menu, ktore "uczy"
    uzytkownika niedzialajacego skrotu.

    Sprawdzamy przynaleznosc do KTOREJKOLWIEK tablicy widoku, a nie pierwsza
    znaleziona: ten sam klawisz ma w oryginale rozne znaczenie zaleznie od
    widoku (F6 to na liscie odtwarzacz, a w odtwarzaczu powrot; Escape to na
    liscie poziom wyzej, a w odtwarzaczu powrot). Splaszczenie przez
    ``setdefault`` przepuszczalo F6 tylko dzieki kolejnosci tablic.
    """
    real: dict[str, set[Action]] = {}
    for table in (
        PODCAST_INBOX_LIST_VIEW,
        LIST_VIEW,
        RADIO_LIST_VIEW,
        RADIO_PLAYER_VIEW,
        PLAYER_VIEW,
    ):
        for chord, action in table.items():
            real.setdefault(chord, set()).add(action)

    for item in all_items():
        if not item.shortcut or item.action is None:
            continue
        assert item.shortcut in real, f"{item.label}: skrot {item.shortcut} nie istnieje"
        assert item.action in real[item.shortcut], (
            f"{item.label}: {item.shortcut} nie robi nigdzie {item.action}, "
            f"tylko {real[item.shortcut]}"
        )


def test_no_duplicate_labels_or_shortcuts() -> None:
    """Only AMC's intentional contextual Ctrl+O may be repeated."""
    items = all_items()
    labels = [item.label for item in items]
    assert len(labels) == len(set(labels)), "powtorzona nazwa pozycji"
    by_shortcut: dict[str, list[menu_model.MenuItem]] = {}
    for item in items:
        if item.shortcut:
            by_shortcut.setdefault(item.shortcut, []).append(item)
    duplicates = {key: values for key, values in by_shortcut.items() if len(values) > 1}
    assert set(duplicates) <= {"Ctrl+O", "Alt+1", "Alt+2"}, duplicates
    for item in duplicates.get("Ctrl+O", []):
        assert not item.accelerator, (
            "kontekstowe Ctrl+O musi dojsc do resolvera sesji, a nie do "
            f"pierwszego akceleratora menu: {item.label}"
        )
    for chord in ("Alt+1", "Alt+2"):
        contextual = duplicates.get(chord, [])
        assert len(contextual) == 2
        assert all(not item.accelerator for item in contextual), (
            f"kontekstowe {chord} nie moze byc akceleratorem okna"
        )


def test_podcast_inbox_sort_menu_is_contextual_checkable_and_user_facing() -> None:
    expected = {
        Action.SORT_PODCAST_INBOX_ADDED: ("Alt+1", "dodania"),
        Action.SORT_PODCAST_INBOX_ALPHABETICAL: ("Alt+2", "Alfabetycznie"),
        Action.SORT_PODCAST_INBOX_BY_PODCAST: ("Alt+3", "podcast"),
    }
    found = {item.action: item for item in all_items() if item.action in expected}
    assert set(found) == set(expected)
    for action, (shortcut, label_part) in expected.items():
        item = found[action]
        assert item.shortcut == shortcut
        assert item.needs_podcast_inbox and item.checkable
        assert not item.accelerator
        assert label_part.casefold() in item.label.casefold()


def test_every_item_is_keyboard_reachable() -> None:
    """Kazda pozycja ma znacznik '&' -- inaczej nie ma dostepu z klawiatury."""
    for item in all_items():
        assert "&" in item.label, f"{item.label} bez klawisza dostepu"
    for menu in menu_model.build_menus():
        assert "&" in menu.title, f"menu {menu.title} bez klawisza dostepu"


def test_access_keys_within_one_menu_do_not_collide() -> None:
    """Dwie pozycje na tej samej literze psuja obsluge z klawiatury."""
    for menu in menu_model.build_menus():
        keys = []
        for item in menu.items:
            if item.is_separator:
                continue
            letter = item.label.split("&", 1)[1][:1].lower()
            keys.append(letter)
        assert len(keys) == len(set(keys)), f"kolizja klawiszy dostepu w {menu.title}: {keys}"


def test_library_views_are_not_offered_in_the_radio_menu() -> None:
    """Kontekst: Ulubione i Playlisty to BIBLIOTEKA LOKALNA, nie stacje radiowe."""
    radio = next(m for m in menu_model.build_menus() if "Radio" in m.title)
    library = {Action.VIEW_ALL_FILES, Action.VIEW_FAVORITES, Action.VIEW_PLAYLISTS}
    assert not {i.action for i in radio.items} & library


def test_radio_station_management_stays_in_the_radio_menu() -> None:
    """I odwrotnie: zarzadzanie stacjami nie wycieka do menu biblioteki."""
    station = {
        Action.STATION_ADD,
        Action.STATION_EDIT,
        Action.STATION_DELETE,
        Action.STATION_IMPORT,
    }
    for menu in menu_model.build_menus():
        if "Radio" in menu.title:
            continue
        assert not {i.action for i in menu.items} & station, menu.title


def test_radio_items_declare_they_need_the_radio_session() -> None:
    """Polecenia stacji sa radiowe; wspolne podglady sa globalne jak w AMC."""
    radio = next(m for m in menu_model.build_menus() if "Radio" in m.title)
    global_previews = {
        Action.VIEW_ACTIVE_RECORDINGS,
        Action.VIEW_RECORDED_RADIO_FILES,
        Action.MANAGE_RADIO_SCHEDULES,
    }
    for item in radio.items:
        if item.is_separator or item.action in global_previews:
            continue
        assert item.needs_radio_session, item.label
    for item in radio.items:
        if item.action in global_previews:
            assert not item.needs_radio_session, item.label


def test_no_placeholder_items_for_things_we_do_not_have() -> None:
    """Zero atrap typu 'niedostepne' -- menu wymienia tylko dzialajace funkcje."""
    for item in all_items():
        lowered = item.label.lower()
        for bad in ("niedost", "wkrótce", "wkrotce", "w przygotowaniu", "(brak)"):
            assert bad not in lowered, item.label
        assert item.action is not None or item.builtin is not None, (
            f"{item.label}: pozycja bez akcji byłaby atrapa"
        )


def test_the_four_new_operations_are_discoverable_in_the_menu() -> None:
    """Funkcje, ktore dzialaja fizycznie, ale byly ukryte za skrotem."""
    actions = {item.action for item in all_items()}
    for action in (
        Action.VIEW_ALL_FILES,
        Action.VIEW_FAVORITES,
        Action.VIEW_PLAYLISTS,
        Action.PARENT_FOLDER,
    ):
        assert action in actions, action


def test_transport_and_clipboard_reached_the_menu_too() -> None:
    """Transport i kopiowanie tez byly dostepne wylacznie z klawiatury."""
    actions = {item.action for item in all_items()}
    for action in (
        Action.PLAY_PAUSE,
        Action.TIME_ELAPSED,
        Action.TIME_REMAINING,
        Action.TIME_TOTAL,
        Action.COPY_NAME,
        Action.COPY_ADDRESS,
    ):
        assert action in actions, action


def test_existing_menus_are_preserved() -> None:
    """Dzwiek, tempo i ustawienia juz byly -- nie wolno ich zgubic."""
    titles = [m.title for m in menu_model.build_menus()]
    for expected in ("&Pliki", "&Radio", "&Widok", "&Dźwięk", "Pomo&c"):
        assert expected in titles, f"zgubione menu {expected}"


def test_podcast_aggregate_views_are_discoverable_and_contextual() -> None:
    view = next(menu for menu in menu_model.build_menus() if menu.title == "&Widok")
    by_action = {item.action: item for item in view.items if item.action is not None}
    assert Action.VIEW_PODCAST_INBOX in by_action
    assert Action.VIEW_PODCAST_IN_PROGRESS in by_action
    assert Action.VIEW_PODCAST_DOWNLOADS in by_action
    assert not by_action[Action.VIEW_PODCAST_INBOX].needs_podcast_session
    assert by_action[Action.VIEW_PODCAST_IN_PROGRESS].needs_podcast_session
    assert by_action[Action.VIEW_PODCAST_DOWNLOADS].needs_podcast_session


def test_audio_menu_keeps_its_tempo_submenu_marker() -> None:
    """Podmenu algorytmow buduje GUI (radio-itemy) -- model ma je zapowiedziec."""
    audio = next(m for m in menu_model.build_menus() if "Dźwięk" in m.title)
    assert any(item.builtin == "tempo-submenu" for item in audio.items)


def test_quit_and_help_use_builtin_ids_not_invented_actions() -> None:
    """Zakoncz i Pomoc to polecenia okna, nie akcje listy."""
    builtins = {item.builtin for item in all_items() if item.builtin}
    assert "quit" in builtins
    actions = {item.action for item in all_items()}
    assert Action.HELP in actions
