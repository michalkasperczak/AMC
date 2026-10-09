"""Testy tablicy skrotow: zgodnosc z pelnym AMC i pierwszenstwo natywnej listy."""

from __future__ import annotations

from amc_wx_lite.shortcuts import Action, Chord, describe, resolve


def test_arrows_on_the_list_belong_to_the_native_control() -> None:
    # To jest WARUNEK dostepnosci: strzalki na liscie musza chodzic po
    # wierszach, zeby czytnik ekranu i Narrator dzialaly bez dodatku.
    #
    # WYJATEK, ktory ma sam oryginal: LEWA strzalka bez modyfikatora czyta
    # krotka informacje uzupelniajaca (MainWindow.xaml.cs:23206-23216), i to
    # tam, nie w kontrolce, bo lista jest JEDNOKOLUMNOWA -- w lewo nie ma
    # gdzie przejsc. Ten test kodowal dawne wymaganie "zadna strzalka"; zostaje
    # odwrocony na wymaganie oryginalu, a nie usuniety.
    for key in ("Up", "Down", "Right", "Home", "End", "Prior", "Next"):
        action = resolve(Chord(key), player_view=False, radio_session=False)
        assert action is None, f"{key} na liscie nie moze byc przejety"
    assert (
        resolve(Chord("Left"), player_view=False, radio_session=False)
        is Action.QUICK_INFORMATION
    )


def test_tab_is_never_intercepted() -> None:
    for view in (True, False):
        assert resolve(Chord("Tab"), player_view=view, radio_session=False) is None
        assert resolve(Chord("Tab", shift=True), player_view=view, radio_session=False) is None


def test_arrows_in_player_view_control_seek_and_volume() -> None:
    # W widoku odtwarzacza nie ma po czym chodzic, wiec strzalki przejmuja
    # role z profilu globalnego AMC (KeyboardProfile.cs:57-66).
    #
    # POPRAWKA PARYTETU: wiersz Shift = 60 s pochodzil z KeyboardProfile.cs:63-66,
    # czyli z profilu czytanego PO AKORDZIE PREFIKSU. Warstwa OKNA ma inne
    # kroki (MainWindow.xaml.cs:21585-21590, 22389-22394): Shift = 30 s,
    # Ctrl = 60 s, Ctrl+Alt = czas z ustawien. Ten test kodowal dawne,
    # nieprawdziwe wymaganie, wiec zostaje odwrocony na wymaganie oryginalu,
    # a nie usuniety. Pelny zestaw par sprawdza test_transport_parity.
    cases = {
        ("Left", False): Action.SEEK_BACK_10,
        ("Right", False): Action.SEEK_FORWARD_10,
        ("Left", True): Action.SEEK_BACK_30,
        ("Right", True): Action.SEEK_FORWARD_30,
        ("Up", False): Action.VOLUME_UP_5,
        ("Down", False): Action.VOLUME_DOWN_5,
        ("Up", True): Action.VOLUME_UP_1,
        ("Down", True): Action.VOLUME_DOWN_1,
    }
    for (key, shift), expected in cases.items():
        assert resolve(Chord(key, shift=shift), player_view=True, radio_session=False) is expected
    # 60 s nie znika z programu -- przenosi sie pod Ctrl, zgodnie z oryginalem.
    assert resolve(Chord("Left", ctrl=True), player_view=True, radio_session=False) is Action.SEEK_BACK_60
    assert resolve(Chord("Right", ctrl=True), player_view=True, radio_session=False) is Action.SEEK_FORWARD_60


def test_space_is_play_pause_in_both_views() -> None:
    # MainWindow.xaml.cs:21687 (ModifierKeys.None, Key.Space) => PlayPause
    assert resolve(Chord("Space"), player_view=False, radio_session=False) is Action.PLAY_PAUSE
    assert resolve(Chord("Space"), player_view=True, radio_session=False) is Action.PLAY_PAUSE


def test_ctrl_space_belongs_to_native_multiselect_list() -> None:
    # Windowsowy Ctrl+Spacja przelacza element pod fokusem bez kasowania
    # pozostalych zaznaczen. AMC nie moze przejac tego gestu jako polecenia.
    for radio in (False, True):
        assert resolve(
            Chord("Space", ctrl=True), player_view=False, radio_session=radio
        ) is None


def test_f6_and_escape_match_the_full_amc_behaviour() -> None:
    # MainWindow.xaml.cs:20773-20792
    assert resolve(Chord("F6"), player_view=False, radio_session=False) is Action.SHOW_PLAYER
    assert resolve(Chord("F6", shift=True), player_view=True, radio_session=False) is Action.SHOW_LIST
    assert resolve(Chord("Escape"), player_view=True, radio_session=False) is Action.SHOW_LIST
    # Escape NA LISCIE wychodzi o poziom wyzej: cs:20827 wola
    # ``ReturnToMediaListFromEscape``, a ta przy pustym filtrze (cs:22562)
    # wola ``NavigateToParentLevel()``. Wczesniej ten test wymagal ``None``
    # i dlatego utrwalal martwy klawisz, ktory Michal zglosil.
    assert resolve(Chord("Escape"), player_view=False, radio_session=False) is Action.PARENT_FOLDER


def test_ctrl_digits_switch_sessions() -> None:
    # MainWindow.xaml.cs:21449 Ctrl + cyfra => SessionSlot
    assert resolve(Chord("1", ctrl=True), player_view=False, radio_session=False) is Action.SESSION_FILES
    assert resolve(Chord("2", ctrl=True), player_view=True, radio_session=False) is Action.SESSION_RADIO


def test_backspace_goes_to_parent_folder_only_on_the_list() -> None:
    # MainWindow.xaml.cs:20832
    assert resolve(Chord("Back"), player_view=False, radio_session=False) is Action.PARENT_FOLDER
    assert resolve(Chord("Back"), player_view=True, radio_session=False) is None


def test_station_management_only_in_the_radio_session() -> None:
    for key, expected in (("Ctrl+N", Action.STATION_ADD), ("F2", Action.STATION_EDIT)):
        chord = Chord(key.split("+")[-1], ctrl=key.startswith("Ctrl"))
        assert resolve(chord, player_view=False, radio_session=True) is expected
        assert resolve(chord, player_view=False, radio_session=False) is not expected


def test_ctrl_i_is_global_podcast_inbox_and_radio_import_uses_ctrl_o() -> None:
    ctrl_i = Chord("I", ctrl=True)
    ctrl_shift_i = Chord("I", ctrl=True, shift=True)
    for player in (False, True):
        for radio in (False, True):
            assert resolve(
                ctrl_i, player_view=player, radio_session=radio
            ) is Action.VIEW_PODCAST_INBOX
            assert resolve(
                ctrl_shift_i, player_view=player, radio_session=radio
            ) is Action.VIEW_PODCAST_IN_PROGRESS
    assert resolve(
        Chord("O", ctrl=True), player_view=False, radio_session=True
    ) is Action.STATION_IMPORT
    assert resolve(
        Chord("O", ctrl=True), player_view=True, radio_session=True
    ) is Action.STATION_IMPORT


def test_alt_digits_sort_only_inside_the_podcast_inbox() -> None:
    expected = {
        "1": Action.SORT_PODCAST_INBOX_ADDED,
        "2": Action.SORT_PODCAST_INBOX_ALPHABETICAL,
        "3": Action.SORT_PODCAST_INBOX_BY_PODCAST,
    }
    for key, action in expected.items():
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            podcast_inbox=True,
        ) is action

    # Poza tym widokiem dotychczasowe znaczenia lokalne nie moga zniknac.
    assert resolve(
        Chord("1", alt=True), player_view=False, radio_session=False
    ) is Action.VIEW_FOLDERS
    assert resolve(
        Chord("2", alt=True), player_view=False, radio_session=False
    ) is Action.VIEW_ALL_FILES
    assert resolve(
        Chord("3", alt=True), player_view=False, radio_session=False
    ) is None
    assert resolve(
        Chord("1", alt=True),
        player_view=True,
        radio_session=False,
        podcast_inbox=True,
    ) is None
    assert resolve(
        Chord("O", ctrl=True), player_view=False, radio_session=False
    ) is Action.OPEN_FILE_DIALOG


def test_podcast_refresh_matches_the_full_amc_context() -> None:
    # Ctrl+F5 zawsze odswieza cala Biblioteke podcastow. F5 odswieza biezace
    # zrodlo, z jednym swiadomym wyjatkiem: w globalnej skrzynce odswieza
    # wszystkie zrodla, bo wiersze moga pochodzic z wielu podcastow.
    for player in (False, True):
        assert resolve(
            Chord("F5", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.REFRESH_PODCAST_LIBRARY
        assert resolve(
            Chord("F5"),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.REFRESH_PODCAST

    assert resolve(
        Chord("F5"),
        player_view=False,
        radio_session=False,
        podcast_session=True,
        podcast_inbox=True,
    ) is Action.REFRESH_PODCAST_LIBRARY
    assert resolve(
        Chord("F5"),
        player_view=False,
        radio_session=False,
        podcast_session=False,
    ) is None


def test_ctrl_n_adds_the_kind_used_by_the_current_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("N", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.ADD_PODCAST_SOURCE
    assert resolve(
        Chord("N", ctrl=True),
        player_view=False,
        radio_session=True,
    ) is Action.STATION_ADD


def test_ctrl_o_imports_opml_only_in_the_podcast_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("O", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.IMPORT_PODCAST_OPML
    assert resolve(
        Chord("O", ctrl=True),
        player_view=False,
        radio_session=False,
        podcast_session=False,
    ) is Action.OPEN_FILE_DIALOG
    assert resolve(
        Chord("O", ctrl=True),
        player_view=False,
        radio_session=True,
        podcast_session=False,
    ) is Action.STATION_IMPORT


def test_ctrl_d_downloads_podcasts_but_keeps_file_clip_append() -> None:
    for player in (False, True):
        assert resolve(
            Chord("D", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.DOWNLOAD_PODCAST_EPISODES

    assert resolve(
        Chord("D", ctrl=True),
        player_view=True,
        radio_session=False,
        podcast_session=False,
    ) is Action.CLIP_APPEND


def test_f2_and_delete_match_contextual_library_editing() -> None:
    # Delete usuwa tylko z bieżącego widoku; dopiero Shift+Delete prowadzi
    # przez potwierdzenie do systemowego Kosza. F2 nie zmienia pliku na dysku,
    # a Shift+F2 ma osobną, jawną akcję plikową.
    assert resolve(Chord("F2"), player_view=False, radio_session=False) is Action.RENAME_LIBRARY_ITEM
    assert resolve(Chord("F2", shift=True), player_view=False, radio_session=False) is Action.RENAME_LOCAL_FILE
    assert resolve(Chord("Delete"), player_view=False, radio_session=False) is Action.REMOVE_SELECTED
    assert resolve(Chord("Delete", shift=True), player_view=False, radio_session=False) is Action.RECYCLE_SELECTED
    # W Radiu F2 i Delete zachowują wcześniejsze, dokładniejsze akcje stacji.
    assert resolve(Chord("Delete"), player_view=False, radio_session=True) is Action.STATION_DELETE
    assert resolve(Chord("F2"), player_view=False, radio_session=True) is Action.STATION_EDIT


def test_help_listing_is_not_empty_and_has_no_duplicates() -> None:
    entries = describe()
    assert len(entries) > 15
    actions = [label for _, label in entries]
    assert len(actions) == len(set(actions)), "kazda akcja opisana raz"
