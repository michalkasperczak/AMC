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
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import (
    FILES_SESSION_VIEW,
    FRONTIER_SMART_SESSION_VIEW,
    RADIOARCHIWISTA_SESSION_VIEW,
    LIST_VIEW,
    PLAYER_VIEW,
    PODCAST_INBOX_LIST_VIEW,
    PODCAST_LIBRARY_LIST_VIEW,
    PODCAST_SESSION_VIEW,
    YOUTUBE_CHANNEL_LIST_VIEW,
    YOUTUBE_BROADCASTS_LIST_VIEW,
    RADIO_COLLECTION_LIST_VIEW,
    RADIO_LIST_VIEW,
    RADIO_PLAYER_VIEW,
    SPOTIFY_COLLECTION_LIST_VIEW,
    SPOTIFY_SESSION_VIEW,
    TIDAL_COLLECTION_LIST_VIEW,
    TIDAL_SESSION_VIEW,
    WIIM_PLAYER_VIEW,
    WIIM_SESSION_VIEW,
    SONOS_SESSION_VIEW,
    SONOS_OWN_STREAMS_VIEW,
    Action,
    Chord,
    resolve,
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
        FILES_SESSION_VIEW,
        FRONTIER_SMART_SESSION_VIEW,
        RADIOARCHIWISTA_SESSION_VIEW,
        PODCAST_LIBRARY_LIST_VIEW,
        PODCAST_INBOX_LIST_VIEW,
        PODCAST_SESSION_VIEW,
        YOUTUBE_CHANNEL_LIST_VIEW,
        YOUTUBE_BROADCASTS_LIST_VIEW,
        LIST_VIEW,
        RADIO_COLLECTION_LIST_VIEW,
        RADIO_LIST_VIEW,
        RADIO_PLAYER_VIEW,
        SPOTIFY_COLLECTION_LIST_VIEW,
        SPOTIFY_SESSION_VIEW,
        TIDAL_COLLECTION_LIST_VIEW,
        TIDAL_SESSION_VIEW,
        WIIM_SESSION_VIEW,
        WIIM_PLAYER_VIEW,
        SONOS_SESSION_VIEW,
        SONOS_OWN_STREAMS_VIEW,
        PLAYER_VIEW,
    ):
        for chord, action in table.items():
            real.setdefault(chord, set()).add(action)

    for item in all_items():
        if not item.shortcut or item.action is None:
            continue
        assert item.shortcut in real, f"{item.label}: skrot {item.shortcut} nie istnieje"
        if item.action is Action.SAVE_YOUTUBE_VIDEO_AS:
            assert resolve(
                Chord("S", ctrl=True),
                player_view=False,
                radio_session=False,
                podcast_session=True,
                youtube_video_selection=True,
            ) is item.action
            continue
        if item.action is Action.RECORDING_OUTPUT_INFORMATION:
            assert resolve(
                Chord("Right"),
                player_view=False,
                radio_session=True,
                radio_recording_context=True,
            ) is item.action
            continue
        assert item.action in real[item.shortcut], (
            f"{item.label}: {item.shortcut} nie robi nigdzie {item.action}, "
            f"tylko {real[item.shortcut]}"
        )


def test_corrected_menu_labels_name_the_real_action() -> None:
    search = next(
        item for item in all_items()
        if item.action is Action.SEARCH_CURRENT
    )
    station = next(
        item for item in all_items()
        if item.action is Action.STATION_EDIT
    )
    assert search.label.replace("&", "") == "Szukaj w bieżącej usłudze"
    assert station.label.replace("&", "") == "Zmień nazwę stacji"


def test_account_or_device_menu_uses_ctrl_f5_only_in_matching_sessions() -> None:
    expected = {
        SessionId.SPOTIFY: "Konto Spotify",
        SessionId.TIDAL: "Konto i synchronizacja TIDAL",
        SessionId.WIIM: "Urządzenia WiiM",
    }
    for session, label in expected.items():
        entries = [
            item
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if item.action is Action.SESSION_CONFIGURE_CURRENT
        ]
        assert len(entries) == 1
        assert entries[0].label.replace("&", "") == label
        assert entries[0].shortcut == "Ctrl+F5"
        assert not entries[0].accelerator


def test_no_duplicate_labels_or_shortcuts() -> None:
    """Powtórzenia są tylko celowymi skrótami kontekstowymi AMC."""
    items = all_items()
    labels = [item.label for item in items]
    assert len(labels) == len(set(labels)), "powtorzona nazwa pozycji"
    by_shortcut: dict[str, list[menu_model.MenuItem]] = {}
    for item in items:
        if item.shortcut:
            by_shortcut.setdefault(item.shortcut, []).append(item)
    duplicates = {key: values for key, values in by_shortcut.items() if len(values) > 1}
    # F2/Delete są teraz tak samo kontekstowe jak w pełnym AMC: ogólna
    # pozycja Biblioteki obsługuje pliki i podcasty, a menu Radio ma bardziej
    # szczegółową nazwę tej samej intencji dla stacji. Alt+D również jest
    # kontekstowe: w Radiu czyta informacje o bieżącym odtwarzaniu, a w
    # Podcastach pełny opis.
    assert set(duplicates) <= {
        "Ctrl+O", "Ctrl+Shift+O", "Ctrl+N", "Ctrl+E", "Ctrl+D", "Ctrl+S",
        "Alt+1", "Alt+2", "Alt+3", "Alt+D", "F5", "Ctrl+F5", "Ctrl+Shift+L",
            "Ctrl+Shift+U", "Ctrl+Shift+P",
        "B", "Shift+B", "Ctrl+Shift+B", "Ctrl+X", "Ctrl+V",
        "Alt+Up", "Alt+Down", "F2", "Delete", "Right",
        "Ctrl+U", "Ctrl+P", "I", "O", "R", "S", "T"
    }, duplicates
    for chord in ("Ctrl+U", "Ctrl+P"):
        for item in duplicates.get(chord, []):
            if item.action in {
                Action.SPOTIFY_VIEW_LIKED,
                Action.SPOTIFY_VIEW_PLAYLISTS,
            }:
                assert not item.accelerator, (
                    f"kontekstowe {chord} Spotify musi rozstrzygnąć aktywna sesja"
                )
    for item in duplicates.get("Ctrl+O", []):
        assert not item.accelerator, (
            "kontekstowe Ctrl+O musi dojsc do resolvera sesji, a nie do "
            f"pierwszego akceleratora menu: {item.label}"
        )
    for chord in ("Alt+1", "Alt+2", "Alt+3"):
        contextual = duplicates.get(chord, [])
        assert len(contextual) == (8 if chord in ("Alt+1", "Alt+2") else 7)
        assert all(not item.accelerator for item in contextual), (
            f"kontekstowe {chord} nie moze byc akceleratorem okna"
        )
    for item in duplicates.get("Ctrl+D", []):
        assert not item.accelerator, (
            "kontekstowe Ctrl+D musi rozstrzygnąć sesja podcastów albo plików"
        )
    for item in duplicates.get("Ctrl+S", []):
        assert not item.accelerator, (
            "kontekstowe Ctrl+S musi rozstrzygnąć zapis odcinka albo fragmentu"
        )
    for item in duplicates.get("Ctrl+N", []):
        assert not item.accelerator, (
            "kontekstowe Ctrl+N musi rozstrzygnąć sesja podcastów albo radia"
        )
    for item in duplicates.get("Ctrl+X", []):
        assert not item.accelerator, (
            "kontekstowe Ctrl+X musi rozstrzygnąć pliki, WiiM albo Sonosa"
        )
    for chord in ("Ctrl+V", "Alt+Up", "Alt+Down"):
        for item in duplicates.get(chord, []):
            assert not item.accelerator, (
                f"kontekstowe {chord} musi rozstrzygnąć aktywny widok"
            )
    for chord in ("Ctrl+Shift+O", "Ctrl+E"):
        for item in duplicates.get(chord, []):
            if item.action in {
                Action.IMPORT_WIIM_NETWORK_STREAMS,
                Action.EXPORT_WIIM_NETWORK_STREAMS,
            }:
                assert not item.accelerator, (
                    f"WiiM {chord} musi rozstrzygnąć aktywna sesja"
                )
    for item in duplicates.get("Ctrl+Shift+B", []):
        assert not item.accelerator, (
            "Ctrl+Shift+B musi rozstrzygnąć lista albo odtwarzacz"
        )
    for item in duplicates.get("Ctrl+Shift+L", []):
        assert not item.accelerator, (
            "Ctrl+Shift+L musi rozstrzygnąć Podcasty albo TIDAL"
        )
    for chord in ("B", "Shift+B"):
        for item in duplicates.get(chord, []):
            assert not item.accelerator, (
                f"{chord} musi rozstrzygnąć plik albo nagrywanie radia"
            )


def test_radio_recording_bookmark_menu_is_precisely_gated() -> None:
    radio = next(menu for menu in menu_model.build_menus() if "Radio" in menu.title)
    items = [
        item for item in radio.items
        if item.action in (Action.ADD_BOOKMARK, Action.ADD_NAMED_BOOKMARK)
    ]
    assert {item.shortcut for item in items} == {"B", "Ctrl+Shift+B"}
    assert all(item.needs_radio_session for item in items)
    assert all(item.needs_radio_recording for item in items)
    assert all(not item.accelerator for item in items)


def test_recording_pause_is_a_checkable_state_not_an_ambiguous_command() -> None:
    item = next(
        item for item in all_items() if item.action is Action.RECORD_PAUSE
    )
    assert item.checkable
    assert item.needs_radio_session
    assert item.needs_radio_recording
    assert item.shortcut == "Shift+Space"


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
        assert label_part.casefold() in item.label.replace("&", "").casefold()


def test_tidal_sort_menu_is_contextual_checkable_and_user_facing() -> None:
    expected = {
        Action.SORT_TIDAL_COLLECTION_ADDED: ("Alt+1", "najnowsze"),
        Action.SORT_TIDAL_COLLECTION_ALPHABETICAL: ("Alt+2", "alfabetycznie"),
        Action.SORT_TIDAL_COLLECTION_CUSTOM: ("Alt+3", "własna"),
    }
    found = {item.action: item for item in all_items() if item.action in expected}
    assert set(found) == set(expected)
    for action, (shortcut, label_part) in expected.items():
        item = found[action]
        assert item.shortcut == shortcut
        assert item.needs_tidal_collection and item.checkable
        assert not item.accelerator
        assert label_part.casefold() in item.label.replace("&", "").casefold()


def test_spotify_sort_menu_is_contextual_checkable_and_user_facing() -> None:
    expected = {
        Action.SORT_SPOTIFY_COLLECTION_ADDED: ("Alt+1", "najnowsze"),
        Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL: ("Alt+2", "alfabetycznie"),
        Action.SORT_SPOTIFY_COLLECTION_CATALOG: ("Alt+3", "katalogu"),
    }
    found = {item.action: item for item in all_items() if item.action in expected}
    assert set(found) == set(expected)
    for action, (shortcut, label_part) in expected.items():
        item = found[action]
        assert item.shortcut == shortcut
        assert item.needs_spotify_collection and item.checkable
        assert not item.accelerator
        assert label_part.casefold() in item.label.replace("&", "").casefold()


def test_podcast_refresh_menu_is_contextual_and_does_not_steal_f5() -> None:
    expected = {
        Action.REFRESH_PODCAST: "F5",
        Action.REFRESH_PODCAST_LIBRARY: "Ctrl+F5",
    }
    found = {item.action: item for item in all_items() if item.action in expected}
    assert set(found) == set(expected)
    for action, shortcut in expected.items():
        item = found[action]
        assert item.shortcut == shortcut
        assert item.needs_podcast_session
        assert not item.accelerator


def test_podcast_download_menu_is_contextual_and_does_not_steal_ctrl_d() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.DOWNLOAD_PODCAST_EPISODES
    )
    assert item.shortcut == "Ctrl+D"
    assert item.needs_podcast_session and item.needs_selection
    assert not item.accelerator
    assert "pobierz" in item.label.replace("&", "").casefold()


def test_podcast_save_as_menu_is_contextual_and_does_not_steal_ctrl_s() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.SAVE_PODCAST_EPISODE_AS
    )
    assert item.shortcut == "Ctrl+S"
    assert item.needs_podcast_session and item.needs_selection
    assert not item.accelerator
    assert "zapisz" in item.label.replace("&", "").casefold()


def test_podcast_description_and_go_to_parent_have_precise_gates() -> None:
    descriptions = [
        entry for entry in all_items()
        if entry.action is Action.SHOW_PODCAST_DESCRIPTION
    ]
    description = next(
        entry for entry in descriptions
        if entry.visible_sessions == (SessionId.PODCASTS,)
    )
    spotify_description = next(
        entry for entry in descriptions
        if entry.visible_sessions == (SessionId.SPOTIFY,)
    )
    go_to = next(
        entry for entry in all_items()
        if entry.action is Action.GO_TO_RELATED_PODCAST
    )
    assert description.shortcut == "Alt+D"
    assert description.needs_podcast_session
    assert description.needs_podcast_item
    assert not description.accelerator
    assert spotify_description.shortcut == "Alt+D"
    assert spotify_description.needs_description_item
    assert not spotify_description.accelerator
    assert go_to.shortcut is None
    assert go_to.needs_podcast_session
    assert go_to.needs_podcast_episode
    assert "podcast" in go_to.label.replace("&", "").casefold()


def test_favorite_menu_is_contextual_and_does_not_steal_shortcut() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.TOGGLE_FAVORITE
    )
    assert item.shortcut == "Ctrl+Shift+U"
    assert item.needs_favorite_item
    assert not item.needs_podcast_session
    assert not item.needs_podcast_item
    assert not item.accelerator
    assert "ulubion" in item.label.replace("&", "").casefold()


def test_radio_library_and_sort_commands_are_discoverable_and_contextual() -> None:
    membership = next(
        entry for entry in all_items()
        if entry.action is Action.TOGGLE_RADIO_LIBRARY
    )
    assert membership.shortcut == "Ctrl+Shift+L"
    assert membership.visible_sessions == (SessionId.RADIO,)
    assert membership.needs_radio_station
    assert not membership.accelerator

    sort_actions = {
        Action.SORT_RADIO_COLLECTION_ADDED,
        Action.SORT_RADIO_COLLECTION_ALPHABETICAL,
        Action.SORT_RADIO_COLLECTION_CUSTOM,
    }
    entries = [entry for entry in all_items() if entry.action in sort_actions]
    assert {entry.shortcut for entry in entries} == {"Alt+1", "Alt+2", "Alt+3"}
    assert all(entry.checkable and entry.needs_radio_collection for entry in entries)
    assert all(entry.visible_sessions == (SessionId.RADIO,) for entry in entries)
    assert all(not entry.accelerator for entry in entries)


def test_playlist_membership_menu_is_contextual_and_does_not_steal_shortcut() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.MANAGE_PLAYLIST_MEMBERSHIP
    )
    assert item.shortcut == "Ctrl+Shift+P"
    assert item.needs_playlist_item
    assert not item.accelerator
    assert "playlist" in item.label.replace("&", "").casefold()


def test_spotify_delete_menu_is_limited_to_removable_saved_rows() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.REMOVE_SELECTED
        and entry.visible_sessions == (menu_model.SessionId.SPOTIFY,)
    )
    assert item.shortcut == "Delete"
    assert item.needs_selection and item.needs_spotify_saved_item
    assert not item.accelerator
    assert "spotify" in item.label.replace("&", "").casefold()


def test_library_membership_menu_is_specific_to_podcasts_and_tidal() -> None:
    podcast = next(
        entry for entry in all_items()
        if entry.action is Action.TOGGLE_PODCAST_LIBRARY
    )
    tidal = next(
        entry for entry in all_items()
        if entry.action is Action.TIDAL_TOGGLE_LIBRARY
    )
    assert podcast.shortcut == tidal.shortcut == "Ctrl+Shift+L"
    assert podcast.visible_sessions == (menu_model.SessionId.PODCASTS,)
    assert podcast.needs_podcast_library_item
    assert tidal.visible_sessions == (menu_model.SessionId.TIDAL,)
    assert tidal.needs_tidal_library_item
    assert not podcast.accelerator and not tidal.accelerator
    assert "bibliotek" in podcast.label.replace("&", "").casefold()
    assert "bibliotek" in tidal.label.replace("&", "").casefold()


def test_queue_membership_menu_matches_supported_sessions() -> None:
    queue = next(
        entry for entry in all_items() if entry.action is Action.ADD_TO_QUEUE
    )
    play_next = next(
        entry for entry in all_items()
        if entry.action is Action.TOGGLE_PLAY_NEXT
    )
    assert queue.shortcut == "Ctrl+Shift+Q"
    assert queue.needs_selection and queue.needs_queue_item
    assert set(queue.visible_sessions) == {
        menu_model.SessionId.FILES,
        menu_model.SessionId.PODCASTS,
        menu_model.SessionId.TIDAL,
    }
    assert play_next.shortcut == "Ctrl+Shift+Return"
    assert play_next.needs_selection and play_next.needs_play_next_item
    assert set(play_next.visible_sessions) == {
        menu_model.SessionId.FILES,
        menu_model.SessionId.PODCASTS,
    }
    assert not queue.accelerator and not play_next.accelerator

    actions_by_session = {
        session: {
            item.action
            for menu in menu_model.menus_for_session(session)
            for item in menu.items
            if not item.is_separator
        }
        for session in menu_model.SessionId
    }
    assert Action.ADD_TO_QUEUE in actions_by_session[menu_model.SessionId.FILES]
    assert Action.ADD_TO_QUEUE in actions_by_session[menu_model.SessionId.PODCASTS]
    assert Action.ADD_TO_QUEUE in actions_by_session[menu_model.SessionId.TIDAL]
    assert Action.TOGGLE_PLAY_NEXT in actions_by_session[menu_model.SessionId.FILES]
    assert Action.TOGGLE_PLAY_NEXT in actions_by_session[menu_model.SessionId.PODCASTS]
    assert Action.TOGGLE_PLAY_NEXT not in actions_by_session[menu_model.SessionId.TIDAL]
    for session in (
        menu_model.SessionId.RADIO,
        menu_model.SessionId.WIIM,
        menu_model.SessionId.SONOS,
    ):
        assert Action.ADD_TO_QUEUE not in actions_by_session[session]
        assert Action.TOGGLE_PLAY_NEXT not in actions_by_session[session]


def test_manual_order_menu_is_limited_to_playlist_or_radio_collection() -> None:
    expected = {
        Action.MOVE_LIST_ITEM_UP,
        Action.MOVE_LIST_ITEM_DOWN,
        Action.PASTE_OR_MOVE,
    }
    items = [entry for entry in all_items() if entry.action in expected]
    assert {entry.action for entry in items} == expected
    assert all(entry.needs_manual_order_context for entry in items)
    assert all(entry.needs_selection for entry in items)
    assert all(not entry.accelerator for entry in items)


def test_podcast_add_menu_is_contextual_and_does_not_steal_ctrl_n() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.ADD_PODCAST_SOURCE
    )
    assert item.shortcut == "Ctrl+N"
    assert item.needs_podcast_session
    assert not item.accelerator
    assert "dodaj" in item.label.replace("&", "").casefold()


def test_podcast_opml_menu_is_contextual_and_keeps_ctrl_o_in_resolver() -> None:
    imported = next(
        entry for entry in all_items()
        if entry.action is Action.IMPORT_PODCAST_OPML
    )
    exported = next(
        entry for entry in all_items()
        if entry.action is Action.EXPORT_PODCAST_OPML
    )
    assert imported.shortcut == "Ctrl+O"
    assert imported.needs_podcast_session and not imported.accelerator
    assert exported.shortcut is None and exported.needs_podcast_session
    assert "opml" in imported.label.replace("&", "").casefold()
    assert "opml" in exported.label.replace("&", "").casefold()


def test_youtube_export_menu_is_contextual_and_has_no_fake_shortcut() -> None:
    item = next(
        entry for entry in all_items()
        if entry.action is Action.EXPORT_YOUTUBE_SUBSCRIPTIONS
    )
    assert item.shortcut is None
    assert item.needs_podcast_session
    assert "youtube" in item.label.replace("&", "").casefold()


def test_every_item_is_keyboard_reachable() -> None:
    """Kazda pozycja ma znacznik '&' -- inaczej nie ma dostepu z klawiatury."""
    for item in all_items():
        assert "&" in item.label, f"{item.label} bez klawisza dostepu"
    for menu in menu_model.build_menus():
        assert "&" in menu.title, f"menu {menu.title} bez klawisza dostepu"


def test_access_keys_within_one_menu_do_not_collide() -> None:
    """Dwie pozycje na tej samej literze psuja obsluge z klawiatury."""
    # Pasek jest przebudowywany przy zmianie sesji. Sprawdzamy więc każdy
    # rzeczywiście widoczny wariant, a nie sztuczną sumę wzajemnie wykluczających
    # się poleceń Plików, Radia, TIDAL-a, WiiM i Sonosa.
    for session in menu_model.SessionId:
        for menu in menu_model.menus_for_session(session):
            keys = []
            for item in menu.items:
                if item.is_separator:
                    continue
                letter = item.label.split("&", 1)[1][:1].lower()
                keys.append(letter)
            assert len(keys) == len(set(keys)), (
                f"kolizja klawiszy dostepu w {menu.title}, {session}: {keys}"
            )


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
        Action.STATION_EXPORT_FAVORITES,
    }
    for menu in menu_model.build_menus():
        if "Radio" in menu.title:
            continue
        assert not {i.action for i in menu.items} & station, menu.title


def test_radio_items_declare_they_need_the_radio_session() -> None:
    """Polecenia stacji sa radiowe; jawne podglady zachowuja swoj zakres."""
    radio = next(m for m in menu_model.build_menus() if "Radio" in m.title)
    global_previews = {
        Action.RECORD_STOP_ALL,
        Action.VIEW_ACTIVE_RECORDINGS,
        Action.VIEW_RECORDED_RADIO_FILES,
        Action.MANAGE_RADIO_SCHEDULES,
    }
    local_and_radio = {Action.VIEW_RADIO_RECOGNITION_HISTORY}
    for item in radio.items:
        if item.is_separator or item.action in global_previews | local_and_radio:
            continue
        assert item.needs_radio_session, item.label
    for item in radio.items:
        if item.action in global_previews:
            assert not item.needs_radio_session, item.label
        if item.action in local_and_radio:
            assert not item.needs_radio_session, item.label
            assert item.visible_sessions == (
                SessionId.FILES,
                SessionId.RADIO,
            )


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


def test_ctrl_c_is_not_a_second_native_menu_accelerator() -> None:
    """NVDA ma uslyszec jedno potwierdzenie, bez angielskiego ``copy``.

    Kopiowanie obsluguje ``shortcuts.resolve`` i ``LiteFrame._on_key``.
    Akcelerator wx byl druga, zbedna droga tego samego gestu i wywolywal
    dodatkowa systemowa zapowiedz przed polskim komunikatem aplikacji.
    """
    copy_name = next(
        item for item in all_items() if item.action is Action.COPY_NAME
    )
    assert copy_name.shortcut == "Ctrl+C"
    assert not copy_name.accelerator


def test_existing_menus_are_preserved() -> None:
    """Dzwiek, tempo i ustawienia juz byly -- nie wolno ich zgubic."""
    titles = [m.title for m in menu_model.build_menus()]
    for expected in ("&Pliki", "&Radio", "&Widok", "Dźwię&k", "Pomo&c"):
        assert expected in titles, f"zgubione menu {expected}"


def test_podcast_aggregate_views_are_discoverable_and_contextual() -> None:
    view = next(menu for menu in menu_model.build_menus() if menu.title == "&Widok")
    by_action = {item.action: item for item in view.items if item.action is not None}
    assert Action.VIEW_PODCAST_INBOX in by_action
    assert Action.VIEW_PODCAST_DOWNLOADS in by_action
    assert by_action[Action.VIEW_PODCAST_INBOX].visible_sessions == (
        menu_model.SessionId.PODCASTS,
    )
    assert by_action[Action.VIEW_PODCAST_DOWNLOADS].needs_podcast_session


def test_playback_and_view_menus_do_not_mix_foreign_sessions() -> None:
    def actions(session, title):
        menu = next(
            menu for menu in menu_model.menus_for_session(session)
            if menu.title == title
        )
        return {item.action for item in menu.items if item.action is not None}

    podcast_view = actions(menu_model.SessionId.PODCASTS, "&Widok")
    assert Action.VIEW_PODCAST_INBOX in podcast_view
    assert Action.SORT_TIDAL_COLLECTION_ADDED not in podcast_view

    tidal_view = actions(menu_model.SessionId.TIDAL, "&Widok")
    assert Action.SORT_TIDAL_COLLECTION_ADDED in tidal_view
    assert Action.SORT_SPOTIFY_COLLECTION_ADDED not in tidal_view
    assert Action.VIEW_PODCAST_INBOX not in tidal_view
    assert Action.SHOW_PLAYER not in tidal_view

    spotify_view = actions(menu_model.SessionId.SPOTIFY, "&Widok")
    assert Action.SORT_SPOTIFY_COLLECTION_ADDED in spotify_view
    assert Action.SORT_TIDAL_COLLECTION_ADDED not in spotify_view

    radio_playback = actions(menu_model.SessionId.RADIO, "&Odtwarzanie")
    assert Action.TOGGLE_MUTE in radio_playback
    assert Action.SEEK_BACK_10 in radio_playback
    assert Action.CLIP_MARK_START not in radio_playback
    assert Action.RATE_UP not in radio_playback

    files_playback = actions(menu_model.SessionId.FILES, "&Odtwarzanie")
    assert Action.CLIP_MARK_START in files_playback
    assert Action.RATE_UP in files_playback

    remote_playback = actions(menu_model.SessionId.SONOS, "&Odtwarzanie")
    assert remote_playback == {Action.PLAY_PAUSE}

    sonos_library = actions(menu_model.SessionId.SONOS, "&Biblioteka")
    assert {
        Action.VIEW_LIBRARY,
        Action.VIEW_FAVORITES,
        Action.VIEW_PLAYLISTS,
        Action.VIEW_HISTORY,
    } <= sonos_library
    assert {
        Action.VIEW_FOLDERS,
        Action.VIEW_ALL_FILES,
        Action.VIEW_SAVED_QUEUE,
        Action.MANAGE_PLAYLIST_MEMBERSHIP,
    }.isdisjoint(sonos_library)

    sonos_audio = actions(menu_model.SessionId.SONOS, "Dźwię&k")
    assert sonos_audio == {Action.SESSION_OPTIONS}


def test_every_session_has_the_accessible_session_list_command() -> None:
    for session in menu_model.SessionId:
        view = next(
            menu for menu in menu_model.menus_for_session(session)
            if menu.title == "&Widok"
        )
        item = next(entry for entry in view.items if entry.action is Action.SESSION_LIST)
        assert item.label == "Otwórz &Menedżer sesji…"
        assert item.shortcut == "Ctrl+Shift+S"
        assert not item.accelerator


def test_current_session_mute_is_an_audio_checkable_playback_command() -> None:
    playback = next(
        menu for menu in menu_model.build_menus()
        if menu.title == "&Odtwarzanie"
    )
    item = next(
        entry for entry in playback.items
        if entry.action is Action.TOGGLE_MUTE
    )

    assert item.shortcut == "Ctrl+M"
    assert item.checkable
    assert item.needs_playback
    assert item.visible_sessions == menu_model.MUTE_SESSIONS
    assert not item.accelerator


def test_spotify_gets_embedded_player_commands_and_real_tempo_controls() -> None:
    actions = {
        item.action
        for menu in menu_model.menus_for_session(menu_model.SessionId.SPOTIFY)
        for item in menu.items
        if item.action is not None
    }

    assert {
        Action.SHOW_PLAYER,
        Action.SHOW_LIST,
        Action.PLAY_PAUSE,
        Action.TOGGLE_MUTE,
        Action.SEEK_BACK_10,
        Action.SEEK_FORWARD_10,
        Action.TIME_ELAPSED,
        Action.TIME_REMAINING,
        Action.TIME_TOTAL,
        Action.SELECT_AUDIO_OUTPUT,
        Action.RATE_DOWN,
        Action.RATE_UP,
        Action.RATE_RESET,
    } <= actions


def test_file_menu_uses_the_active_session_instead_of_mixing_sources() -> None:
    def actions(session):
        menu = next(
            menu for menu in menu_model.menus_for_session(session)
            if menu.title == "&Pliki"
        )
        return {item.action for item in menu.items if item.action is not None}

    files = actions(menu_model.SessionId.FILES)
    assert Action.OPEN_FILE_DIALOG in files
    assert Action.IMPORT_PODCAST_OPML not in files
    assert Action.VIEW_WIIM_NETWORK_STREAMS not in files

    podcasts = actions(menu_model.SessionId.PODCASTS)
    assert Action.IMPORT_PODCAST_OPML in podcasts
    assert Action.OPEN_FILE_DIALOG not in podcasts
    assert Action.VIEW_WIIM_NETWORK_STREAMS not in podcasts

    wiim = actions(menu_model.SessionId.WIIM)
    assert Action.IMPORT_WIIM_NETWORK_STREAMS in wiim
    assert Action.VIEW_WIIM_NETWORK_STREAMS not in wiim
    assert Action.OPEN_FILE_DIALOG not in wiim
    assert Action.IMPORT_PODCAST_OPML not in wiim


def test_audio_menu_keeps_its_tempo_submenu_marker() -> None:
    """Podmenu algorytmow buduje GUI (radio-itemy) -- model ma je zapowiedziec."""
    audio = next(
        m for m in menu_model.build_menus()
        if m.title.replace("&", "") == "Dźwięk"
    )
    assert any(item.builtin == "tempo-submenu" for item in audio.items)


def test_quit_and_help_use_builtin_ids_not_invented_actions() -> None:
    """Zakoncz i Pomoc to polecenia okna, nie akcje listy."""
    builtins = {item.builtin for item in all_items() if item.builtin}
    assert "quit" in builtins
    actions = {item.action for item in all_items()}
    assert Action.HELP in actions
