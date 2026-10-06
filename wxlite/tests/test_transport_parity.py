"""Parytet TRANSPORTU wxPython z oryginalnym AMC -- gesty, kroki, komunikaty.

Dlaczego ten plik istnieje
--------------------------
Michal uruchomil wariant wxPython na prawdziwym profilu i zglosil, ze transport
NIE jest tym transportem, ktory zna z AMC:

  1. ``Ctrl+Shift+E/R/T`` nie dzialaja, a dzialaja ``Ctrl+E/R/T``.
  2. Przy strzalkach slychac stale "Minelo ...", czyli zapowiedz bezwarunkowa
     i w obcym formacie.
  3. ``Ctrl+Left`` / ``Ctrl+Right`` nie robia nic.
  4. Regulacji predkosci nie da sie znalezc pod klawiszami oryginalu.

Kwit RED rodzica (wszystkie piec gestow => ``None``):
``/home/michal/projekty/amc_pomoc/wx-transport-parity-20261005/delivered-shortcuts-repro.json``

WZORZEC C#, ODCZYTANY, NIE ZGADNIETY
------------------------------------
``src/AccessibleMediaController.Windows/MainWindow.xaml.cs``

  21674-21676 i 22190-22192  (dwie drogi tego samego okna)::

      (ModifierKeys.Control | ModifierKeys.Shift, Key.E) => CommandIds.TimeElapsed,
      (ModifierKeys.Control | ModifierKeys.Shift, Key.R) => CommandIds.TimeRemaining,
      (ModifierKeys.Control | ModifierKeys.Shift, Key.T) => CommandIds.TimeTotal,

  21673 i 22188::

      (ModifierKeys.Control | ModifierKeys.Shift, Key.G) => CommandIds.SettingsToggleSeekMessages,

  21583-21590 i 22389-22394  (przewijanie w odtwarzaczu)::

      (None,        Left/Right) => SeekBackward10 / SeekForward10
      (Shift,       Left/Right) => SeekBackward30 / SeekForward30
      (Control,     Left/Right) => SeekBackward60 / SeekForward60
      (Control|Alt, Left/Right) => SeekBackwardCustom / SeekForwardCustom

  21595-21597 i 22395-22397  (predkosc w odtwarzaczu)::

      (Shift,   OemComma)  => PlaybackRateDown
      (Shift,   OemPeriod) => PlaybackRateUp
      (Control, OemPeriod) => PlaybackRateReset

  21556-21559  (skok procentowy w odtwarzaczu)::

      if (modifiers == ModifierKeys.None && TryGetDigitKey(key, out var digit))
          commandId = CommandIds.SeekPercent(digit * 10);

  20778-20785  (F6 i Shift+F6)::

      if (e.Key == Key.F6 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)
          if (_playerViewActive && Keyboard.Modifiers == ModifierKeys.Shift)
              ReturnFromPlayerToList();
          else
              ShowPlayerView();

``src/AccessibleMediaController.Core/Input/KeyboardProfile.cs:70-72`` wiaze
``Ctrl+E/R/T`` z tymi samymi komendami, ale to profil PO AKORDZIE PREFIKSU --
inna warstwa, nie klawisze okna. W oknie ``Ctrl+E`` nalezy do eksportu
(``MainWindow.xaml.cs:21662`` ExportRadioFavorites), wiec zostawienie go jako
aliasu czasu bylo BLEDEM, nie wygoda.

``src/AccessibleMediaController.Core/Commands/CommandRouter.cs``

  325-335  czas pytany recznie idzie przez ``AnnounceTemplate`` z domyslnym
  szablonem ``{elapsed}`` / ``{remaining}`` / ``{total}``, czyli SAM CZAS bez
  slowa "Minelo"/"Pozostalo"/"Calosc",
  534-539  ``Seek`` mowi TYLKO gdy ``Messages.SeekMessages`` **i**
  ``Messages.ArrowSeekMessages``,
  543-562  ``SeekPercent`` pyta ``PercentageSeekMessages`` i
  ``PercentageSeekAnnouncement`` (osobna rodzina -- NIE dotyczy strzalek),
  566-575  ``Volume`` pyta ``SeekMessages`` i ``VolumeMessages``,
  600-604  ``FormatPlaybackRate``: "Predkosc normalna" albo
  "Predkosc {0.##} razy".

``src/AccessibleMediaController.Core/Presentation/MediaItemFormatter.cs:69-73``
``FormatDuration``: ``h:mm:ss`` od godziny, inaczej ``m:ss``.

``src/AccessibleMediaController.Core/Sessions/DemoMediaSession.cs:11``
drabina predkosci: ``[0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00]``.

``src/AccessibleMediaController.Core/Configuration/AppSettings.cs:280-295``
``CustomSeekSeconds``: domyslnie 300 s, zakres 5..1800 s.

Zakres tego pliku: SAMA tablica klawiszy i SAMA polityka komunikatow. Bez GUI,
bez audio, bez czytnika ekranu. Zywy NVDA i rzeczywisty ruch czasu to osobna,
waska proba rodzica -- tu pilnujemy kontraktu, ktory da sie zmierzyc bez okna.
"""

from __future__ import annotations

import json
import tempfile
from pathlib import Path

from amc_wx_lite import menu_model
from amc_wx_lite.profile_layout import private_sandbox, read_only_mirror
from amc_wx_lite.shortcuts import (
    LIST_VIEW,
    PLAYER_VIEW,
    RADIO_LIST_VIEW,
    Action,
    Chord,
    describe,
    resolve,
)
from amc_wx_lite.transport_parity import (
    CUSTOM_SEEK_DEFAULT_SECONDS,
    CUSTOM_SEEK_MAX_SECONDS,
    CUSTOM_SEEK_MIN_SECONDS,
    PLAYBACK_RATES,
    MessagePolicy,
    PercentAnnouncement,
    format_clock,
    format_playback_rate,
    load_message_policy,
    next_playback_rate,
    normalize_custom_seek_seconds,
    seek_percent_value,
    seek_step_seconds,
    time_announcement,
)


def player(key: str, **mods: bool) -> Action | None:
    return resolve(Chord(key, **mods), player_view=True, radio_session=False)


def listing(key: str, **mods: bool) -> Action | None:
    return resolve(Chord(key, **mods), player_view=False, radio_session=False)


# ===================================================== 1. Ctrl+Shift+E/R/T


def test_ctrl_shift_ert_are_the_time_questions_in_both_views() -> None:
    """``MainWindow.xaml.cs:21674-21676`` i ``22190-22192``.

    To dwie drogi TEGO SAMEGO okna (pomoc klawiatury i skrot przy fokusie),
    wiec gest musi dzialac i na liscie, i w odtwarzaczu.
    """
    for ask, expected in (
        ("E", Action.TIME_ELAPSED),
        ("R", Action.TIME_REMAINING),
        ("T", Action.TIME_TOTAL),
    ):
        assert player(ask, ctrl=True, shift=True) is expected, f"Ctrl+Shift+{ask}"
        assert listing(ask, ctrl=True, shift=True) is expected, f"Ctrl+Shift+{ask}"


def test_ctrl_ert_without_shift_is_not_an_alias_for_time() -> None:
    """``Ctrl+E/R/T`` naleza do PROFILU PO PREFIKSIE, nie do okna.

    W samym oknie ``Ctrl+E`` to eksport (``MainWindow.xaml.cs:21662``).
    Zostawienie aliasu uczyloby gestu, ktorego oryginal nie ma -- a Michal
    wlasnie na to trafil: dzialalo ``Ctrl+E``, nie dzialalo ``Ctrl+Shift+E``.
    """
    for ask in ("E", "R", "T"):
        assert player(ask, ctrl=True) is None, f"Ctrl+{ask} nie jest czasem"
        assert listing(ask, ctrl=True) is None, f"Ctrl+{ask} nie jest czasem"


def test_ctrl_shift_g_toggles_player_messages() -> None:
    """``MainWindow.xaml.cs:21673``/``22188`` SettingsToggleSeekMessages."""
    assert player("G", ctrl=True, shift=True) is Action.TOGGLE_SEEK_MESSAGES
    assert listing("G", ctrl=True, shift=True) is Action.TOGGLE_SEEK_MESSAGES


# ======================================================== 2. kroki przewijania


def test_every_seek_step_from_the_original_exists() -> None:
    """``MainWindow.xaml.cs:21583-21590`` oraz ``22389-22394``.

    Cztery pary, nie dwie. ``Shift`` to 30 s (NIE 60), ``Ctrl`` to 60 s,
    ``Ctrl+Alt`` to czas ustawiony przez uzytkownika.
    """
    assert player("Left") is Action.SEEK_BACK_10
    assert player("Right") is Action.SEEK_FORWARD_10
    assert player("Left", shift=True) is Action.SEEK_BACK_30
    assert player("Right", shift=True) is Action.SEEK_FORWARD_30
    assert player("Left", ctrl=True) is Action.SEEK_BACK_60
    assert player("Right", ctrl=True) is Action.SEEK_FORWARD_60
    assert player("Left", ctrl=True, alt=True) is Action.SEEK_BACK_CUSTOM
    assert player("Right", ctrl=True, alt=True) is Action.SEEK_FORWARD_CUSTOM


def test_seek_step_seconds_match_the_command_names() -> None:
    """Krok w sekundach bierzemy z NAZWY komendy oryginalu, nie z pamieci."""
    assert seek_step_seconds(Action.SEEK_BACK_10, custom_seconds=300) == -10
    assert seek_step_seconds(Action.SEEK_FORWARD_10, custom_seconds=300) == 10
    assert seek_step_seconds(Action.SEEK_BACK_30, custom_seconds=300) == -30
    assert seek_step_seconds(Action.SEEK_FORWARD_30, custom_seconds=300) == 30
    assert seek_step_seconds(Action.SEEK_BACK_60, custom_seconds=300) == -60
    assert seek_step_seconds(Action.SEEK_FORWARD_60, custom_seconds=300) == 60
    assert seek_step_seconds(Action.SEEK_BACK_CUSTOM, custom_seconds=300) == -300
    assert seek_step_seconds(Action.SEEK_FORWARD_CUSTOM, custom_seconds=300) == 300


def test_shift_arrow_is_no_longer_silently_sixty_seconds() -> None:
    """Regresja, ktora wlasnie naprawiamy.

    Dostarczony ``shortcuts.py`` wiazal ``Shift+Left/Right`` z 60 s. Oryginal
    daje tam 30 s, a 60 s siedzi pod ``Ctrl``. Dwa gesty robily to samo, a
    jeden nie robil nic.
    """
    assert seek_step_seconds(player("Left", shift=True), custom_seconds=300) == -30
    assert seek_step_seconds(player("Left", ctrl=True), custom_seconds=300) == -60


def test_custom_seek_seconds_respect_the_original_range() -> None:
    """``AppSettings.cs:280-295``: domyslnie 300, zakres 5..1800."""
    assert CUSTOM_SEEK_DEFAULT_SECONDS == 300
    assert CUSTOM_SEEK_MIN_SECONDS == 5
    assert CUSTOM_SEEK_MAX_SECONDS == 1800
    assert normalize_custom_seek_seconds(0) == 5
    assert normalize_custom_seek_seconds(4) == 5
    assert normalize_custom_seek_seconds(300) == 300
    assert normalize_custom_seek_seconds(9999) == 1800


def test_arrows_on_the_list_still_belong_to_the_native_control() -> None:
    """Nowe gesty NIE moga zabrac strzalek liscie -- to warunek dostepnosci.

    Jedyny wyjatek jest WZIETY Z ORYGINALU, nie wymyslony tutaj: LEWA bez
    modyfikatora czyta krotka informacje uzupelniajaca
    (``MainWindow.xaml.cs:23206-23216``). Pod Shift/Ctrl/Alt i w kazdym innym
    kierunku strzalka nadal nalezy do kontrolki.
    """
    for mods in ({}, {"shift": True}, {"ctrl": True}, {"ctrl": True, "alt": True}):
        for key in ("Left", "Right", "Up", "Down"):
            if key == "Left" and not mods:
                assert listing(key) is Action.QUICK_INFORMATION
                continue
            assert listing(key, **mods) is None, f"{key} {mods} na liscie"


# ============================================== 3. format i polityka komunikatow


def test_clock_format_is_the_csharp_format_duration() -> None:
    """``MediaItemFormatter.cs:69-73``. Release .383 mowi wprost "3:51"."""
    assert format_clock(0) == "0:00"
    assert format_clock(5) == "0:05"
    assert format_clock(65) == "1:05"
    assert format_clock(231) == "3:51"
    assert format_clock(3725) == "1:02:05"
    assert format_clock(None) == "nieznany"
    assert format_clock(-1) == "nieznany"


def test_time_questions_say_the_bare_time_without_added_words() -> None:
    """``CommandRouter.cs:325-335``: szablony to ``{elapsed}``/``{remaining}``/
    ``{total}``, czyli SAM CZAS. Dostarczony ``gui.py:1897-1905`` doklejal
    "Minelo"/"Pozostalo"/"Calosc" -- tego w oryginale nie ma.
    """
    policy = MessagePolicy()
    assert time_announcement(Action.TIME_ELAPSED, policy, position=231, duration=300) == "3:51"
    assert time_announcement(Action.TIME_TOTAL, policy, position=231, duration=300) == "5:00"
    assert time_announcement(Action.TIME_REMAINING, policy, position=231, duration=300) == "1:09"


def test_remaining_time_is_never_negative() -> None:
    """``CommandRouter.cs:331`` owija roznice w ``Max(TimeSpan.Zero, ...)``."""
    policy = MessagePolicy()
    assert time_announcement(Action.TIME_REMAINING, policy, position=400, duration=300) == "0:00"


def test_time_questions_are_answered_even_with_messages_off() -> None:
    """Pytanie zadane RECZNIE musi dostac odpowiedz.

    ``CommandRouter.cs:325-335`` nie pyta tu o zaden przelacznik -- bramka
    ``SeekMessages``/``ArrowSeekMessages`` dotyczy zapowiedzi AUTOMATYCZNYCH
    przy przewijaniu, nie odpowiedzi na nacisniety klawisz.
    """
    quiet = MessagePolicy(seek_messages=False, arrow_seek_messages=False)
    assert time_announcement(Action.TIME_ELAPSED, quiet, position=231, duration=300) == "3:51"


def test_master_switch_silences_the_spoken_answer_but_keeps_the_text() -> None:
    """``MainWindow.xaml.cs:659-663``: gdy ``Messages.Enabled`` jest wylaczone,
    tekst idzie do pola statusu, ale NIE jest wymawiany."""
    off = MessagePolicy(enabled=False)
    assert off.speaks_routine is False
    assert time_announcement(Action.TIME_ELAPSED, off, position=231, duration=300) == "3:51"


def test_arrow_seek_announcement_obeys_both_switches() -> None:
    """``CommandRouter.cs:534-539``: ``SeekMessages`` **i** ``ArrowSeekMessages``."""
    assert MessagePolicy().announces_arrow_seek is True
    assert MessagePolicy(arrow_seek_messages=False).announces_arrow_seek is False
    assert MessagePolicy(seek_messages=False).announces_arrow_seek is False
    # Wylaczone ArrowSeekMessages NIE moze wyciszyc rodziny procentowej.
    assert MessagePolicy(arrow_seek_messages=False).announces_percent_seek is True


def test_percent_family_is_separate_from_the_arrow_family() -> None:
    """``CommandRouter.cs:543-562``: procent ma WLASNE przelaczniki i WLASNY
    tryb zapowiedzi. Przeniesienie ich na strzalki byloby zmiana semantyki.
    """
    assert MessagePolicy().percent_announcement is PercentAnnouncement.PERCENT
    assert MessagePolicy(percentage_seek_messages=False).announces_percent_seek is False
    assert MessagePolicy(seek_messages=False).announces_percent_seek is False


def test_percent_announcement_has_all_three_modes_of_the_original() -> None:
    """``CommandRouter.cs:556-561`` -- Percent, Time, PercentAndTime."""
    base = MessagePolicy()
    assert base.percent_text(30, position_seconds=231) == "30%"
    timed = MessagePolicy(percent_announcement=PercentAnnouncement.TIME)
    assert timed.percent_text(30, position_seconds=231) == "3:51"
    both = MessagePolicy(percent_announcement=PercentAnnouncement.PERCENT_AND_TIME)
    assert both.percent_text(30, position_seconds=231) == "30%, 3:51"


def test_volume_announcement_obeys_its_own_switch() -> None:
    """``CommandRouter.cs:566-575``: ``SeekMessages`` **i** ``VolumeMessages``,
    a format to ``{value}%`` -- nie wlasne slowo "Glosnosc"."""
    assert MessagePolicy().announces_volume is True
    assert MessagePolicy(volume_messages=False).announces_volume is False
    assert MessagePolicy().volume_text(35) == "35%"
    assert MessagePolicy().volume_text(35, muted=True) == "35%, wyciszono"


def test_templates_from_the_profile_win_over_the_fallback() -> None:
    """``CommandRouter.cs:678-687``: ``Messages.Templates`` ma pierwszenstwo.

    Uzytkownik, ktory w AMC wpisal wlasny szablon, musi uslyszec swoj tekst
    takze tutaj -- inaczej "te same ustawienia" byly by nieprawda.
    """
    policy = MessagePolicy(templates={"time.elapsed": "Minelo {elapsed}"})
    assert time_announcement(Action.TIME_ELAPSED, policy, position=231, duration=300) == "Minelo 3:51"


def test_empty_template_means_silence_not_a_fallback() -> None:
    """``CommandRouter.cs:681``: ``if (string.IsNullOrEmpty(template)) return;``"""
    policy = MessagePolicy(templates={"time.total": ""})
    assert time_announcement(Action.TIME_TOTAL, policy, position=231, duration=300) is None


def test_unknown_duration_does_not_pretend_to_know_the_remainder() -> None:
    policy = MessagePolicy()
    assert time_announcement(Action.TIME_REMAINING, policy, position=10, duration=None) is None
    assert time_announcement(Action.TIME_TOTAL, policy, position=10, duration=None) == "nieznany"


# ================================================== 4. polityka z PRAWDZIWEGO profilu


def test_message_policy_is_read_from_the_shared_amc_profile() -> None:
    """Jedne ustawienia, nie dwa zbiory.

    ``ConfigurationStore.cs:30-35`` zapisuje ``state.json`` w ``camelCase``,
    wiec czytamy ``settings.messages`` oraz ``settings.customSeekSeconds``
    doslownie tak, jak je zapisal host. Zapisu NIE robimy.
    """
    with tempfile.TemporaryDirectory() as raw:
        base = Path(raw)
        profile = base / "profile"
        profile.mkdir()
        (profile / "state.json").write_text(
            json.dumps(
                {
                    "schemaVersion": 54,
                    "settings": {
                        "customSeekSeconds": 90,
                        "messages": {
                            "enabled": True,
                            "seekMessages": True,
                            "arrowSeekMessages": False,
                            "percentageSeekMessages": True,
                            "volumeMessages": False,
                            "percentageSeekAnnouncement": "PercentAndTime",
                            "templates": {"time.elapsed": "{elapsed} minelo"},
                        },
                    },
                },
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        layout = read_only_mirror(local_dir=base / "local", profile_dir=profile)
        policy = load_message_policy(layout)

        assert policy.announces_arrow_seek is False, "wylaczone w profilu AMC"
        assert policy.announces_percent_seek is True
        assert policy.announces_volume is False
        assert policy.percent_announcement is PercentAnnouncement.PERCENT_AND_TIME
        assert policy.custom_seek_seconds == 90
        assert (
            time_announcement(Action.TIME_ELAPSED, policy, position=231, duration=300)
            == "3:51 minelo"
        )


def test_missing_or_broken_profile_falls_back_to_original_defaults() -> None:
    """Brak pliku nie moze uciszyc transportu ani wywrocic okna.

    Domyslne wartosci sa TE SAME, co ``MessageSettings`` w
    ``AppSettings.cs:539-555``.
    """
    with tempfile.TemporaryDirectory() as raw:
        base = Path(raw)
        layout = read_only_mirror(local_dir=base / "l", profile_dir=base / "p")
        policy = load_message_policy(layout)
        assert policy.announces_arrow_seek is True
        assert policy.custom_seek_seconds == CUSTOM_SEEK_DEFAULT_SECONDS

        broken = base / "broken"
        broken.mkdir()
        (broken / "state.json").write_text("{ to nie jest json", encoding="utf-8")
        policy = load_message_policy(read_only_mirror(local_dir=base / "l", profile_dir=broken))
        assert policy.announces_arrow_seek is True


def test_out_of_range_custom_seek_from_the_profile_is_clamped() -> None:
    with tempfile.TemporaryDirectory() as raw:
        base = Path(raw)
        profile = base / "p"
        profile.mkdir()
        (profile / "state.json").write_text(
            json.dumps({"settings": {"customSeekSeconds": 99999}}), encoding="utf-8"
        )
        policy = load_message_policy(read_only_mirror(local_dir=base / "l", profile_dir=profile))
        assert policy.custom_seek_seconds == CUSTOM_SEEK_MAX_SECONDS


def test_toggling_messages_never_writes_to_the_shared_profile() -> None:
    """Wlascicielem ``state.json`` zostaje host C#.

    ``Ctrl+Shift+G`` w trybie wspolnego profilu ma powiedziec PRAWDE
    ("zmien w AMC"), a nie udac zapisu ani po cichu nic nie zrobic.
    """
    with tempfile.TemporaryDirectory() as raw:
        base = Path(raw)
        profile = base / "p"
        profile.mkdir()
        state = profile / "state.json"
        state.write_text(json.dumps({"settings": {"messages": {"seekMessages": True}}}), encoding="utf-8")
        before = state.read_text(encoding="utf-8")

        layout = read_only_mirror(local_dir=base / "l", profile_dir=profile)
        policy = load_message_policy(layout)
        assert policy.may_persist is False
        result = policy.toggle_seek_messages()
        assert result.changed is False
        assert "AMC" in result.message
        assert state.read_text(encoding="utf-8") == before, "profil AMC nietkniety"


def test_toggling_messages_works_in_the_private_sandbox() -> None:
    """Opcja, ktorej nie da sie wykonac, byla by martwym polem.

    We wlasnej piaskownicy zapis jest dozwolony, wiec przelacznik DZIALA i
    mowi to samo zdanie, co ``MainWindow.xaml.cs:5903-5905``.
    """
    with tempfile.TemporaryDirectory() as raw:
        policy = load_message_policy(private_sandbox(raw))
        assert policy.may_persist is True
        assert policy.announces_arrow_seek is True

        off = policy.toggle_seek_messages()
        assert off.changed is True
        assert off.message == "Automatyczne komunikaty odtwarzacza wyłączone"
        assert policy.announces_arrow_seek is False

        on = policy.toggle_seek_messages()
        assert on.changed is True
        assert on.message == "Automatyczne komunikaty odtwarzacza włączone"
        assert policy.announces_arrow_seek is True


# ===================================================== 5. predkosc odtwarzania


def test_playback_rate_keys_are_the_keys_of_the_original() -> None:
    """``MainWindow.xaml.cs:21595-21597`` i ``22395-22397``.

    Michal "nie znalazl predkosci" dokladnie dlatego, ze siedziala pod
    ``Ctrl+strzalki``, czyli pod gestem, ktorego oryginal nie ma.
    """
    assert player(",", shift=True) is Action.RATE_DOWN
    assert player(".", shift=True) is Action.RATE_UP
    assert player(".", ctrl=True) is Action.RATE_RESET


def test_invented_rate_keys_are_gone() -> None:
    """``Ctrl+Up/Down`` i ``Ctrl+0`` nie istnieja w oryginale.

    ``Ctrl+0`` to w AMC lista sesji (``MainWindow.xaml.cs:21456``), wiec alias
    nie tylko byl obcy -- zabieral cudzy gest.
    """
    assert player("Up", ctrl=True) is None
    assert player("Down", ctrl=True) is None
    assert player("0", ctrl=True) is not Action.RATE_RESET


def test_playback_rate_is_the_seven_step_ladder_of_the_original() -> None:
    """``DemoMediaSession.cs:11`` -- nie wlasna skala 50..200 co 10."""
    assert PLAYBACK_RATES == (0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00)


def test_rate_steps_move_along_the_ladder_and_stop_at_its_ends() -> None:
    """``DemoMediaSession.cs:364-376``: indeks +-1 i ``Math.Clamp``."""
    assert next_playback_rate(1.00, 1) == 1.25
    assert next_playback_rate(1.00, -1) == 0.75
    assert next_playback_rate(2.00, 1) == 2.00
    assert next_playback_rate(0.50, -1) == 0.50
    # Wartosc z boku drabiny (np. z cudzego zapisu) wchodzi na nia, nie gubi sie.
    # C# bierze ``FindLastIndex(rate < PlaybackRate)``, czyli dla 1,10 indeks
    # wartosci 1,00 -- i DOPIERO potem dodaje kierunek. Stad w dol wychodzi
    # 0,75, a nie 1,00. Przepisujemy to zachowanie, nie "rozsadniejsze".
    assert next_playback_rate(1.10, 1) == 1.25
    assert next_playback_rate(1.10, -1) == 0.75


def test_rate_announcement_matches_format_playback_rate() -> None:
    """``CommandRouter.cs:600-604``, kultura polska: przecinek dziesietny."""
    assert format_playback_rate(1.0) == "Prędkość normalna"
    assert format_playback_rate(1.25) == "Prędkość 1,25 razy"
    assert format_playback_rate(0.5) == "Prędkość 0,5 razy"
    assert format_playback_rate(2.0) == "Prędkość 2 razy"


def test_rate_is_reachable_from_the_menu_and_from_the_help() -> None:
    """Sam suwak w oknie to NIE odbior funkcji.

    Uzytkownik czytnika ekranu znajduje polecenie przez menu i przez liste
    skrotow (F1). Jesli predkosci nie ma w zadnym z tych dwoch miejsc, to
    "jest w kodzie" nie znaczy "da sie uzyc".
    """
    actions = {
        item.action
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is not None
    }
    for action in (Action.RATE_UP, Action.RATE_DOWN, Action.RATE_RESET):
        assert action in actions, f"{action} nie ma wejscia w menu"

    described = {label for _, label in describe()}
    assert any("rędkoś" in label or "redkos" in label for label in described), (
        "pomoc F1 nie wymienia regulacji predkosci"
    )


# ========================================================= 6. skok procentowy


def test_digits_in_the_player_jump_by_percent_like_the_original() -> None:
    """``MainWindow.xaml.cs:21556-21559``: cyfra bez modyfikatora -> ``digit*10``."""
    for digit in range(10):
        action = player(str(digit))
        assert action is not None, f"cyfra {digit} w odtwarzaczu"
        assert seek_percent_value(action) == digit * 10


def test_digits_on_the_list_are_left_alone() -> None:
    """Na liscie cyfry naleza do wyszukiwania wierszy w kontrolce."""
    for digit in range(10):
        assert listing(str(digit)) is None


def test_percent_seek_range_matches_command_ids() -> None:
    """``CommandIds.cs:259-266``: 0..90, wielokrotnosci 10."""
    values = sorted(
        seek_percent_value(player(str(digit))) for digit in range(10)
    )
    assert values == [0, 10, 20, 30, 40, 50, 60, 70, 80, 90]


# ================================================================= 7. F6 cykl


def test_f6_and_shift_f6_both_cross_between_list_and_player() -> None:
    """``MainWindow.xaml.cs:20778-20785``.

    Na liscie ``_playerViewActive`` jest falszem, wiec OBIE galezie (z Shift i
    bez) prowadza do ``ShowPlayerView``. W odtwarzaczu oba gesty wracaja na
    liste. Dostarczony kod mial ``Shift+F6`` tylko w jedna strone.
    """
    assert listing("F6") is Action.SHOW_PLAYER
    assert listing("F6", shift=True) is Action.SHOW_PLAYER
    assert player("F6") is Action.SHOW_LIST
    assert player("F6", shift=True) is Action.SHOW_LIST


# ============================================ 8. spojnosc tablic i opisow


def test_no_table_keeps_a_chord_that_the_original_gives_to_someone_else() -> None:
    """Bezpiecznik na aliasy: lista zakazanych gestow z POWODEM.

    Kazdy wpis to gest, ktory w oryginale nalezy do innej komendy. Alias w
    naszej tablicy uczylby uzytkownika zlego klawisza i zabieral wlasciwy.
    """
    forbidden = {
        "Ctrl+E": "ExportRadioFavorites (MainWindow.xaml.cs:21662)",
        "Ctrl+R": "profil po prefiksie, nie okno (KeyboardProfile.cs:71)",
        "Ctrl+T": "profil po prefiksie, nie okno (KeyboardProfile.cs:72)",
        "Ctrl+Up": "oryginal nie ma takiego gestu",
        "Ctrl+Down": "oryginal nie ma takiego gestu",
        "Ctrl+0": "SessionList (MainWindow.xaml.cs:21456)",
    }
    for table_name, table in (("LIST_VIEW", LIST_VIEW), ("PLAYER_VIEW", PLAYER_VIEW)):
        for chord, why in forbidden.items():
            assert chord not in table, f"{table_name}: {chord} nalezy do {why}"


def test_help_lists_every_new_transport_gesture() -> None:
    """Gest bez wiersza w pomocy jest dla uzytkownika czytnika niewidoczny."""
    labels = dict(describe())
    for chord in (
        "Ctrl+Shift+E",
        "Ctrl+Shift+R",
        "Ctrl+Shift+T",
        "Ctrl+Shift+G",
        "Shift+Left",
        "Ctrl+Left",
        "Ctrl+Alt+Left",
        "Shift+,",
        "Shift+.",
        "Ctrl+.",
    ):
        assert chord in labels, f"pomoc nie wymienia {chord}"
        assert labels[chord].strip(), f"{chord} bez opisu"


def test_menu_shortcut_labels_still_tell_the_truth() -> None:
    """Menu nie moze obiecywac starego ``Ctrl+E``, gdy dziala Ctrl+Shift+E.

    Dopuszczamy klawisz, ktory w roznych widokach robi roznie (Escape, F6) --
    liczy sie, czy akcja z menu jest W OGOLE pod tym klawiszem.
    """
    real: dict[str, set[Action]] = {}
    for table in (LIST_VIEW, RADIO_LIST_VIEW, PLAYER_VIEW):
        for chord, action in table.items():
            real.setdefault(chord, set()).add(action)
    for menu in menu_model.build_menus():
        for item in menu.items:
            if item.is_separator or not item.shortcut or item.action is None:
                continue
            assert item.shortcut in real, f"{item.label}: {item.shortcut} nie istnieje"
            assert item.action in real[item.shortcut], (
                f"{item.label}: {item.shortcut} nie robi nigdzie {item.action}, "
                f"tylko {real[item.shortcut]}"
            )
