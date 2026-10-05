"""Testy tablicy skrotow: zgodnosc z pelnym AMC i pierwszenstwo natywnej listy."""

from __future__ import annotations

from amc_wx_lite.shortcuts import Action, Chord, describe, resolve


def test_arrows_on_the_list_belong_to_the_native_control() -> None:
    # To jest WARUNEK dostepnosci: strzalki na liscie musza chodzic po
    # wierszach, zeby czytnik ekranu i Narrator dzialaly bez dodatku.
    for key in ("Up", "Down", "Left", "Right", "Home", "End", "Prior", "Next"):
        action = resolve(Chord(key), player_view=False, radio_session=False)
        assert action is None, f"{key} na liscie nie moze byc przejety"


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


def test_delete_does_nothing_in_the_files_session() -> None:
    # Zabezpieczenie: Delete nie moze dotykac plikow uzytkownika.
    assert resolve(Chord("Delete"), player_view=False, radio_session=False) is None
    assert resolve(Chord("Delete"), player_view=False, radio_session=True) is Action.STATION_DELETE


def test_help_listing_is_not_empty_and_has_no_duplicates() -> None:
    entries = describe()
    assert len(entries) > 15
    actions = [label for _, label in entries]
    assert len(actions) == len(set(actions)), "kazda akcja opisana raz"
