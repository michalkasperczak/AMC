"""Testy tablicy skrotow: zgodnosc z pelnym AMC i pierwszenstwo natywnej listy."""

from __future__ import annotations

from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    describe,
    describe_action,
    keyboard_help_announcement,
    resolve,
)


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


def test_item_information_shortcuts_work_in_list_and_player() -> None:
    for player in (False, True):
        assert resolve(
            Chord("T", alt=True),
            player_view=player,
            radio_session=False,
        ) is Action.QUICK_INFORMATION
        assert resolve(
            Chord("Return", alt=True),
            player_view=player,
            radio_session=False,
        ) is Action.ITEM_PROPERTIES

    help_entries = dict(describe())
    assert "Alt+T" in help_entries
    assert "Alt+Enter" in help_entries


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
    assert resolve(Chord("M", ctrl=True), player_view=False, radio_session=False) is Action.TOGGLE_MUTE
    assert resolve(Chord("M", ctrl=True), player_view=True, radio_session=True) is Action.TOGGLE_MUTE


def test_ctrl_space_belongs_to_native_multiselect_list() -> None:
    # Windowsowy Ctrl+Spacja przelacza element pod fokusem bez kasowania
    # pozostalych zaznaczen. AMC nie moze przejac tego gestu jako polecenia.
    for radio in (False, True):
        assert resolve(
            Chord("Space", ctrl=True), player_view=False, radio_session=radio
        ) is None


def test_search_shortcuts_match_full_amc_in_both_views() -> None:
    current = Chord("F", ctrl=True)
    global_search = Chord("F", ctrl=True, shift=True)
    for player in (False, True):
        assert resolve(
            current, player_view=player, radio_session=False
        ) is Action.SEARCH_CURRENT
        assert resolve(
            global_search, player_view=player, radio_session=False
        ) is Action.SEARCH_ALL


def test_f6_and_escape_match_the_full_amc_behaviour() -> None:
    # MainWindow.xaml.cs:20773-20792
    assert resolve(Chord("F6"), player_view=False, radio_session=False) is Action.SHOW_PLAYER
    assert resolve(Chord("F6", shift=True), player_view=True, radio_session=False) is Action.SHOW_LIST
    assert resolve(Chord("Escape"), player_view=True, radio_session=False) is Action.SHOW_LIST
    # Escape na liscie zamyka podglad, ale nie jest drugim Backspace.
    assert resolve(Chord("Escape"), player_view=False, radio_session=False) is Action.CLOSE_LIST_PREVIEW


def test_ctrl_digits_switch_sessions() -> None:
    # MainWindow.xaml.cs:21449 Ctrl + cyfra => SessionSlot
    assert resolve(Chord("1", ctrl=True), player_view=False, radio_session=False) is Action.SESSION_FILES
    assert resolve(Chord("2", ctrl=True), player_view=True, radio_session=False) is Action.SESSION_RADIO
    assert resolve(Chord("0", ctrl=True), player_view=False, radio_session=False) is None
    assert resolve(Chord("S", ctrl=True, shift=True), player_view=False, radio_session=False) is Action.SESSION_LIST
    assert resolve(Chord("S", ctrl=True, shift=True), player_view=True, radio_session=False) is Action.SESSION_LIST
    assert resolve(Chord("0", ctrl=True), player_view=True, radio_session=False) is None


def test_backspace_goes_to_parent_folder_only_on_the_list() -> None:
    # MainWindow.xaml.cs:20832
    assert resolve(Chord("Back"), player_view=False, radio_session=False) is Action.PARENT_FOLDER
    assert resolve(Chord("Back"), player_view=True, radio_session=False) is None


def test_station_management_only_in_the_radio_session() -> None:
    for key, expected in (("Ctrl+N", Action.STATION_ADD), ("F2", Action.STATION_EDIT)):
        chord = Chord(key.split("+")[-1], ctrl=key.startswith("Ctrl"))
        assert resolve(chord, player_view=False, radio_session=True) is expected
        assert resolve(chord, player_view=False, radio_session=False) is not expected


def test_ctrl_i_and_ctrl_shift_i_are_global_and_radio_import_uses_ctrl_o() -> None:
    ctrl_i = Chord("I", ctrl=True)
    ctrl_shift_i = Chord("I", ctrl=True, shift=True)
    for player in (False, True):
        for radio in (False, True):
            assert resolve(
                ctrl_i, player_view=player, radio_session=radio
            ) is Action.VIEW_PODCAST_INBOX
            assert resolve(
                ctrl_shift_i,
                player_view=player,
                radio_session=radio,
                youtube_account_available=True,
            ) is Action.VIEW_YOUTUBE_LIVE_AGGREGATE
            assert resolve(
                ctrl_shift_i,
                player_view=player,
                radio_session=radio,
                youtube_account_available=False,
            ) is None
    assert resolve(
        Chord("O", ctrl=True), player_view=False, radio_session=True
    ) is Action.STATION_IMPORT
    assert resolve(
        Chord("O", ctrl=True), player_view=True, radio_session=True
    ) is Action.STATION_IMPORT


def test_ctrl_e_exports_radio_favorites_in_both_radio_views_only() -> None:
    for player in (False, True):
        assert resolve(
            Chord("E", ctrl=True), player_view=player, radio_session=True
        ) is Action.STATION_EXPORT_FAVORITES
        assert resolve(
            Chord("E", ctrl=True), player_view=player, radio_session=False
        ) is not Action.STATION_EXPORT_FAVORITES


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


def test_alt_digits_sort_tidal_only_at_collection_roots() -> None:
    expected = {
        "1": Action.SORT_TIDAL_COLLECTION_ADDED,
        "2": Action.SORT_TIDAL_COLLECTION_ALPHABETICAL,
        "3": Action.SORT_TIDAL_COLLECTION_CUSTOM,
    }
    for key, action in expected.items():
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            tidal_session=True,
            tidal_collection=True,
        ) is action
        # W albumie/wykonawcy/kolejce nie wolno uruchomić ani sortowania,
        # ani lokalnego znaczenia „Foldery/Wszystkie pliki”.
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            tidal_session=True,
            tidal_collection=False,
        ) is None


def test_alt_digits_sort_spotify_only_at_collection_roots() -> None:
    expected = {
        "1": Action.SORT_SPOTIFY_COLLECTION_ADDED,
        "2": Action.SORT_SPOTIFY_COLLECTION_ALPHABETICAL,
        "3": Action.SORT_SPOTIFY_COLLECTION_CATALOG,
    }
    for key, action in expected.items():
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            spotify_session=True,
            spotify_collection=True,
        ) is action
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=False,
            spotify_session=True,
            spotify_collection=False,
        ) is None


def test_spotify_membership_shortcuts_work_in_list_and_player() -> None:
    expected = {
        "L": Action.SPOTIFY_TOGGLE_LIBRARY,
        "U": Action.SPOTIFY_TOGGLE_FAVORITE,
    }
    for key, action in expected.items():
        for player in (False, True):
            assert resolve(
                Chord(key, ctrl=True, shift=True),
                player_view=player,
                radio_session=False,
                spotify_session=True,
            ) is action


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


def test_ctrl_f5_opens_account_or_device_configuration_by_session() -> None:
    chord = Chord("F5", ctrl=True)
    assert resolve(
        Chord("F5"),
        player_view=False,
        radio_session=False,
        files_session=True,
    ) is Action.REFRESH_LOCAL_LIBRARY
    assert resolve(
        chord,
        player_view=False,
        radio_session=True,
    ) is Action.SESSION_CONFIGURE_CURRENT
    assert resolve(
        chord,
        player_view=True,
        radio_session=True,
    ) is Action.SESSION_CONFIGURE_CURRENT
    assert resolve(
        Chord("F5"), player_view=False, radio_session=True,
    ) is Action.REFRESH_RADIO_STATIONS
    assert resolve(
        chord,
        player_view=False,
        radio_session=False,
        files_session=True,
    ) is Action.MANAGE_LIBRARY_FOLDERS
    for flag in ("tidal_session", "spotify_session", "wiim_session"):
        arguments = {
            "player_view": False,
            "radio_session": False,
            flag: True,
        }
        assert resolve(chord, **arguments) is Action.SESSION_CONFIGURE_CURRENT

    # Sonos zachowuje odrębny wybór celu, a Podcasty pełne odświeżenie.
    assert resolve(
        chord,
        player_view=False,
        radio_session=False,
        sonos_session=True,
    ) is Action.SELECT_SONOS_TARGET
    assert resolve(
        chord,
        player_view=False,
        radio_session=False,
        podcast_session=True,
    ) is Action.REFRESH_PODCAST_LIBRARY


def test_alt_shift_enter_opens_podcast_item_options_in_list_and_player() -> None:
    chord = Chord("Return", alt=True, shift=True)
    for player in (False, True):
        assert resolve(
            chord,
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.PODCAST_PLAYBACK_OPTIONS
        assert resolve(
            chord,
            player_view=player,
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


def test_ctrl_s_saves_one_podcast_copy_but_keeps_file_clip_export() -> None:
    for player in (False, True):
        assert resolve(
            Chord("S", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.SAVE_PODCAST_EPISODE_AS
    assert resolve(
        Chord("S", ctrl=True),
        player_view=True,
        radio_session=False,
        podcast_session=False,
    ) is Action.CLIP_EXPORT


def test_alt_d_opens_description_only_in_the_podcast_session() -> None:
    for player in (False, True):
        assert resolve(
            Chord("D", alt=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.SHOW_PODCAST_DESCRIPTION
    assert resolve(
        Chord("D", alt=True),
        player_view=False,
        radio_session=False,
        podcast_session=False,
    ) is None


def test_ctrl_shift_u_toggles_podcast_favorites_in_list_and_player() -> None:
    for player in (False, True):
        assert resolve(
            Chord("U", ctrl=True, shift=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.TOGGLE_PODCAST_FAVORITE
    assert resolve(
        Chord("U", ctrl=True, shift=True),
        player_view=False,
        radio_session=False,
        podcast_session=False,
    ) is Action.TOGGLE_FAVORITE


def test_ctrl_shift_u_toggles_local_and_radio_favorites_in_list_and_player() -> None:
    for player in (False, True):
        for radio in (False, True):
            assert resolve(
                Chord("U", ctrl=True, shift=True),
                player_view=player,
                radio_session=radio,
            ) is Action.TOGGLE_FAVORITE


def test_radio_collection_shortcuts_have_one_contextual_meaning() -> None:
    expected = {
        "1": Action.SORT_RADIO_COLLECTION_ADDED,
        "2": Action.SORT_RADIO_COLLECTION_ALPHABETICAL,
        "3": Action.SORT_RADIO_COLLECTION_CUSTOM,
    }
    for key, action in expected.items():
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=True,
            radio_collection=True,
        ) is action
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=True,
            radio_collection=False,
        ) is None
    manual_order = {
        Chord("X", ctrl=True): Action.CUT_FILE,
        Chord("V", ctrl=True): Action.PASTE_OR_MOVE,
        Chord("Up", alt=True): Action.MOVE_LIST_ITEM_UP,
        Chord("Down", alt=True): Action.MOVE_LIST_ITEM_DOWN,
    }
    for chord, action in manual_order.items():
        assert resolve(
            chord,
            player_view=False,
            radio_session=True,
            radio_collection=True,
        ) is action
        assert resolve(
            chord,
            player_view=True,
            radio_session=True,
            radio_collection=False,
        ) is None
        assert resolve(
            chord,
            player_view=False,
            radio_session=True,
            radio_collection=False,
        ) is None
    for player in (False, True):
        assert resolve(
            Chord("L", ctrl=True, shift=True),
            player_view=player,
            radio_session=True,
            radio_collection=not player,
        ) is Action.TOGGLE_RADIO_LIBRARY
        assert resolve(
            Chord("U", ctrl=True, shift=True),
            player_view=player,
            radio_session=True,
            radio_collection=not player,
        ) is Action.TOGGLE_FAVORITE


def test_radio_playlist_contents_reuse_shared_manual_order_shortcuts() -> None:
    manual_order = {
        Chord("X", ctrl=True): Action.CUT_FILE,
        Chord("V", ctrl=True): Action.PASTE_OR_MOVE,
        Chord("Up", alt=True): Action.MOVE_LIST_ITEM_UP,
        Chord("Down", alt=True): Action.MOVE_LIST_ITEM_DOWN,
    }
    for chord, action in manual_order.items():
        assert resolve(
            chord,
            player_view=False,
            radio_session=True,
            radio_playlist_contents=True,
        ) is action

    # Sortowanie calej Biblioteki/Ulubionych nie moze przestawiac zawartosci
    # pojedynczej playlisty ani reklamowac w niej obcego trybu widoku.
    for key in "123":
        assert resolve(
            Chord(key, alt=True),
            player_view=False,
            radio_session=True,
            radio_playlist_contents=True,
        ) is None


def test_ctrl_shift_p_opens_playlist_membership_in_list_and_player() -> None:
    for player in (False, True):
        for radio in (False, True):
            assert resolve(
                Chord("P", ctrl=True, shift=True),
                player_view=player,
                radio_session=radio,
            ) is Action.MANAGE_PLAYLIST_MEMBERSHIP
        assert resolve(
            Chord("P", ctrl=True, shift=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.MANAGE_PLAYLIST_MEMBERSHIP


def test_playlist_manual_order_shortcuts_stay_on_the_list() -> None:
    expected = {
        Chord("X", ctrl=True): Action.CUT_FILE,
        Chord("V", ctrl=True): Action.PASTE_OR_MOVE,
        Chord("Up", alt=True): Action.MOVE_LIST_ITEM_UP,
        Chord("Down", alt=True): Action.MOVE_LIST_ITEM_DOWN,
    }
    for chord, action in expected.items():
        assert resolve(
            chord,
            player_view=False,
            radio_session=False,
        ) is action
    assert resolve(
        Chord("V", ctrl=True),
        player_view=True,
        radio_session=False,
    ) is None


def test_ctrl_u_opens_podcast_favorites_from_list_and_player() -> None:
    for player in (False, True):
        assert resolve(
            Chord("U", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.VIEW_FAVORITES


def test_ctrl_h_opens_podcast_history_from_list_and_player() -> None:
    for player in (False, True):
        assert resolve(
            Chord("H", ctrl=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.VIEW_HISTORY


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


def test_queue_shortcuts_are_distinct_and_shift_enter_stays_free() -> None:
    """Zwykla kolejka i play-next nie moga byc dwiema nazwami tej samej akcji."""
    for player in (False, True):
        assert resolve(
            Chord("Q", ctrl=True, shift=True),
            player_view=player,
            radio_session=False,
        ) is Action.ADD_TO_QUEUE
        assert resolve(
            Chord("Return", ctrl=True, shift=True),
            player_view=player,
            radio_session=False,
        ) is Action.TOGGLE_PLAY_NEXT
        assert resolve(
            Chord("Return", shift=True),
            player_view=player,
            radio_session=False,
        ) is None


def test_podcast_library_and_queue_shortcuts_keep_their_full_amc_meaning() -> None:
    for player in (False, True):
        assert resolve(
            Chord("L", ctrl=True, shift=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.TOGGLE_PODCAST_LIBRARY
        assert resolve(
            Chord("Q", ctrl=True, shift=True),
            player_view=player,
            radio_session=False,
            podcast_session=True,
        ) is Action.ADD_TO_QUEUE


def test_help_listing_is_not_empty_and_has_no_duplicates() -> None:
    entries = describe()
    assert len(entries) > 15
    actions = [label for _, label in entries]
    assert len(actions) == len(set(actions)), "kazda akcja opisana raz"


def test_ctrl_f1_opens_keyboard_help_in_list_and_player() -> None:
    for player in (False, True):
        assert resolve(
            Chord("F1", ctrl=True),
            player_view=player,
            radio_session=False,
        ) is Action.KEYBOARD_HELP


def test_tidal_right_arrow_has_a_precise_keyboard_help_action() -> None:
    action = resolve(
        Chord("Right"),
        player_view=False,
        radio_session=False,
        tidal_session=True,
    )
    assert action is Action.TIDAL_RELATIONS
    announcement = keyboard_help_announcement(Chord("Right"), action, "TIDAL")
    assert "albumu lub wykonawcy TIDAL" in announcement
    assert "Brak polecenia" not in announcement


def test_spotify_right_arrow_help_names_destinations_not_relations_menu() -> None:
    action = resolve(
        Chord("Right"),
        player_view=False,
        radio_session=False,
        spotify_session=True,
    )
    assert action is Action.SPOTIFY_RELATIONS
    announcement = keyboard_help_announcement(Chord("Right"), action, "Spotify")
    assert "albumu, wykonawcy lub podcastu Spotify" in announcement
    assert "menu" not in announcement.casefold()
    assert "powiąza" not in announcement.casefold()


def test_keyboard_help_uses_only_user_facing_labels() -> None:
    message = keyboard_help_announcement(
        Chord("F", ctrl=True),
        Action.SEARCH_CURRENT,
        "Pliki lokalne",
    )
    assert message == "Ctrl+F. Szukaj w bieżącej usłudze. Pliki lokalne"
    assert Action.SEARCH_CURRENT.value not in message
    assert describe_action(Action.KEYBOARD_HELP) == (
        "Włącz lub wyłącz Pomoc klawiatury"
    )


def test_keyboard_help_says_when_a_key_has_no_command() -> None:
    assert keyboard_help_announcement(
        Chord("G"), None, "Radio"
    ) == "G. Brak polecenia w sesji Radio"
