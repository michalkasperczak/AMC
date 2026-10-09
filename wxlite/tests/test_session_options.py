"""Opcje sesji: zdolnosci, dziedziczenie i PRYWATNA trwalosc -- bez okna.

Mierzymy to, co da sie zmierzyc bez pulpitu: ktore opcje sesja NAPRAWDE ma
(bo ma je kto wykonac w silniku), jak dziedziczy ustawienie ogolne i czy
zapisany wybor wraca po ponownym wczytaniu profilu.

ZRODLO ZDOLNOSCI (odczytane, nie zgadniete):
  src/AccessibleMediaController.Windows/SessionPlaybackOptionsEditor.cs:24-31
      SupportsAudioProcessing: local || podcasts     -> RADIO: NIE
      SupportsPlaybackRate:    false                 -> sesja nie zapisuje tempa
      SupportsPlayerExitPause: known && id != wiim   -> oba: TAK
  src/AccessibleMediaController.Windows/Services/RadioMediaOutput.cs:1047
      RadioMediaOutput ma WYLACZNIE ConfigureTempoAlgorithm -- nie ma
      normalizacji, przejsc ani ciszy miedzy utworami, wiec te trzy opcje
      w sesji Radio nie mialyby wykonawcy.
  src/AccessibleMediaController.Core/Playback/PlayerExitPausePolicy.cs:34-39
      ShouldPause = wybor SESJI, inaczej ustawienie OGOLNE.
"""

from __future__ import annotations

import json
import tempfile
from pathlib import Path

from amc_wx_lite.navigation import SessionId
from amc_wx_lite.session_options import (
    SESSION_OPTION_IDS,
    SessionCapabilities,
    SessionPlaybackOverrides,
    INTER_TRACK_SILENCE_CHOICES,
    capabilities_for,
    inter_track_silence_label,
    describe_session_options,
    resolve_audio_payload,
    resolve_pause_on_player_exit,
    scope_for_session,
)
from amc_wx_lite.state_store import LiteState, Options, StateStore


# ------------------------------------------------------- zdolnosci sesji


def test_radio_nie_dostaje_opcji_bez_wykonawcy_w_silniku():
    """RadioMediaOutput nie ma normalizacji/przejsc/ciszy -- wiec sesja tez nie."""
    radio = capabilities_for(SessionId.RADIO)
    assert radio.supports_audio_processing is False, (
        "RadioMediaOutput.cs ma tylko ConfigureTempoAlgorithm; "
        "normalizacja/przejscia/cisza nie mialyby kto wykonac"
    )
    assert radio.supports_player_exit_pause is True, (
        "SessionPlaybackOptionsEditor.cs:29 -- kazda znana sesja poza wiim"
    )
    assert radio.has_options is True, "Radio ma co najmniej opcje opuszczenia odtwarzacza"


def test_pliki_lokalne_dostaja_przetwarzanie_dzwieku_ktore_silnik_umie():
    files = capabilities_for(SessionId.FILES)
    assert files.supports_audio_processing is True
    assert files.supports_player_exit_pause is True
    assert files.has_options is True


def test_podcasty_dostaja_wlasne_przetwarzanie_dzwieku_przed_startem():
    podcasts = capabilities_for(SessionId.PODCASTS)
    assert podcasts.supports_audio_processing is True
    assert podcasts.supports_player_exit_pause is True
    assert podcasts.has_options is True


def test_sesja_nie_zapisuje_algorytmu_tempa_ani_predkosci():
    """SessionPlaybackOptionsEditor.cs:28 -- SupportsPlaybackRate: false.

    Algorytm tempa zostaje OSOBNA funkcja (menu Dzwiek, globalnie). Gdyby
    sesja go zapisywala, dwa miejsca ustawialyby to samo pole silnika.
    """
    for session in (SessionId.FILES, SessionId.RADIO, SessionId.PODCASTS):
        caps = capabilities_for(session)
        assert caps.supports_playback_rate is False
    assert "tempo_algorithm" not in SESSION_OPTION_IDS
    assert not hasattr(SessionPlaybackOverrides(), "tempo_algorithm")


def test_nie_ma_martwych_pol_wyszukiwania_ani_automatycznego_dodawania():
    """Port wyszukiwania i bezpieczny zapis czlonkostwa jeszcze nie istnieja."""
    overrides = SessionPlaybackOverrides()
    for dead in ("auto_add", "search_enter", "resume_mode", "output_device"):
        assert not hasattr(overrides, dead), f"pole bez wykonawcy: {dead}"


# ------------------------------------------------------- dziedziczenie


def test_brak_wyboru_sesji_dziedziczy_ustawienie_ogolne():
    options = Options()
    options.loudness_normalization = True
    options.smooth_track_transitions = True
    options.inter_track_silence_ms = 2000
    payload = resolve_audio_payload(options, SessionPlaybackOverrides())
    assert payload["loudnessNormalization"] is True
    assert payload["smoothTrackTransitions"] is True
    assert payload["interTrackSilenceMs"] == 2000


def test_wybor_sesji_ma_pierwszenstwo_nad_ogolnym():
    options = Options()
    options.loudness_normalization = True
    options.inter_track_silence_ms = 2000
    overrides = SessionPlaybackOverrides(
        loudness_normalization=False, inter_track_silence_ms=500
    )
    payload = resolve_audio_payload(options, overrides)
    assert payload["loudnessNormalization"] is False, "sesja nadpisuje ogolne"
    assert payload["interTrackSilenceMs"] == 500
    # Pole bez wyboru sesji nadal dziedziczy.
    assert payload["smoothTrackTransitions"] is False


def test_algorytm_tempa_w_payloadzie_zostaje_globalny():
    options = Options()
    options.tempo_algorithm = 2
    payload = resolve_audio_payload(options, SessionPlaybackOverrides(loudness_normalization=True))
    assert payload["tempoAlgorithm"] == 2, "sesja nie przestawia globalnego algorytmu"


def test_wstrzymanie_po_wyjsciu_dziedziczy_potem_nadpisuje():
    """PlayerExitPausePolicy.cs:34-39 -- sesja, inaczej ogolne."""
    options = Options()
    assert options.pause_on_player_exit is True, (
        "AppSettings.cs:98 PausePlaybackWhenLeavingPlayer = true"
    )
    assert resolve_pause_on_player_exit(options, SessionPlaybackOverrides()) is True
    assert resolve_pause_on_player_exit(
        options, SessionPlaybackOverrides(pause_on_player_exit=False)
    ) is False
    options.pause_on_player_exit = False
    assert resolve_pause_on_player_exit(options, SessionPlaybackOverrides()) is False
    assert resolve_pause_on_player_exit(
        options, SessionPlaybackOverrides(pause_on_player_exit=True)
    ) is True


# ------------------------------------------------------- zakres silnika


def test_zakres_sesji_nie_rusza_obcego_silnika():
    """Jedno wywolanie audio.configure konfigurowalo OBA silniki naraz.

    LiteEngineHandlers.cs:770-771 wola i _files, i _radio. Bez zakresu wybor
    sesji Pliki lokalne przestawialby grajace radio.
    """
    assert scope_for_session(SessionId.FILES) == "files"
    assert scope_for_session(SessionId.RADIO) == "radio"
    assert scope_for_session(SessionId.PODCASTS) == "podcasts"


# ------------------------------------------------------- trwalosc prywatna


def test_zapisany_wybor_sesji_wraca_po_ponownym_wczytaniu():
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        state = LiteState()
        state.session_overrides["files"] = SessionPlaybackOverrides(
            loudness_normalization=True, inter_track_silence_ms=1000
        )
        state.session_overrides["radio"] = SessionPlaybackOverrides(
            pause_on_player_exit=False
        )
        store.save(state)

        wczytany = store.load()
        files = wczytany.session_overrides["files"]
        assert files.loudness_normalization is True
        assert files.inter_track_silence_ms == 1000
        assert files.smooth_track_transitions is None, "brak wyboru zostaje brakiem"
        assert wczytany.session_overrides["radio"].pause_on_player_exit is False


def test_puste_odstepstwo_nie_zostaje_w_zapisanym_profilu():
    """SessionPlaybackOptionsEditor.cs:93 -- puste wpisy sa USUWANE.

    Inaczej w pliku zostawalyby wartosci nieodroznialne od braku decyzji.
    """
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        state = LiteState()
        state.session_overrides["files"] = SessionPlaybackOverrides()
        store.save(state)
        raw = json.loads(Path(store.path).read_text(encoding="utf-8"))
        assert raw.get("session_overrides", {}) == {}
        assert store.load().session_overrides == {}


def test_uszkodzony_wpis_sesji_nie_blokuje_startu_ani_pozostalych():
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        Path(store.path).parent.mkdir(parents=True, exist_ok=True)
        Path(store.path).write_text(
            json.dumps({
                "session_overrides": {
                    "files": {"loudness_normalization": "tak", "inter_track_silence_ms": 777},
                    "radio": {"pause_on_player_exit": False},
                    "obca-sesja": {"loudness_normalization": True},
                    "zly-ksztalt": ["lista"],
                }
            }),
            encoding="utf-8",
        )
        state = store.load()
        # Napis "tak" jest prawdziwy po rzutowaniu -- STRICT odrzuca go do braku wyboru.
        assert state.session_overrides.get("files", SessionPlaybackOverrides()).loudness_normalization is None
        # 777 ms nie jest wartoscia z PlaybackAudioSettingsRules.
        assert state.session_overrides.get("files", SessionPlaybackOverrides()).inter_track_silence_ms is None
        assert state.session_overrides["radio"].pause_on_player_exit is False
        assert "obca-sesja" not in state.session_overrides, "tylko sesje, ktore port ma"
        assert "zly-ksztalt" not in state.session_overrides


def test_odczyt_starego_profilu_bez_opcji_sesji_dziala():
    """Profil zapisany przed tym przyrostem nie moze przewrocic wczytania."""
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        Path(store.path).parent.mkdir(parents=True, exist_ok=True)
        Path(store.path).write_text(
            json.dumps({"version": 1, "options": {"volume": 40}, "stations": []}),
            encoding="utf-8",
        )
        state = store.load()
        assert state.session_overrides == {}
        assert state.options.volume == 40
        assert state.options.pause_on_player_exit is True


def test_obca_wartosc_wstrzymania_w_ogolnych_wraca_do_domyslu():
    for invalid in ("tak", "false", 1, 0, None, []):
        options = Options()
        options.pause_on_player_exit = invalid
        assert options.clamp().pause_on_player_exit is True


# ------------------------------------------------------- mowa dla czytnika


def test_opis_sesji_wymienia_tylko_wykonalne_opcje():
    options = Options()
    radio = describe_session_options(
        SessionId.RADIO, options, SessionPlaybackOverrides(pause_on_player_exit=False)
    )
    assert "normaliz" not in radio.lower(), "Radio nie ma normalizacji -- nie mow o niej"
    assert "cisz" not in radio.lower()
    assert "dalej" in radio.lower()

    files = describe_session_options(
        SessionId.FILES, options, SessionPlaybackOverrides(loudness_normalization=True)
    )
    assert "normaliz" in files.lower()
    assert "ogóln" in files.lower(), "pola dziedziczone mowia, skad biora wartosc"


def test_opis_jest_krotki_i_bez_powtorzen():
    options = Options()
    for session in (SessionId.FILES, SessionId.RADIO):
        text = describe_session_options(session, options, SessionPlaybackOverrides())
        assert text and len(text) < 320, "komunikat czytnika ma byc krotki"
        assert "  " not in text


def test_etykiety_ciszy_slowo_w_slowo_jak_w_oryginale():
    """Parytet z ``PlaybackAudioSettingsRules.GetInterTrackSilenceLabel``.

    Uzytkownik zna te aplikacje ze slyszenia. Gdy port mowi "2 s" tam, gdzie
    oryginal mowil "2 sekundy", to ta sama opcja brzmi jak inna.
    """
    assert inter_track_silence_label(0) == "bez dodatkowej ciszy"
    assert inter_track_silence_label(500) == "pół sekundy"
    assert inter_track_silence_label(1000) == "1 sekunda"
    assert inter_track_silence_label(2000) == "2 sekundy"
    assert inter_track_silence_label(3000) == "3 sekundy"
    assert inter_track_silence_label(5000) == "5 sekund"


def test_dlugosci_ciszy_zgodne_z_regulami_silnika():
    """Zbior dlugosci = ``SupportedInterTrackSilenceMilliseconds`` (AppSettings.cs:387).

    Dialog nie moze zaproponowac dlugosci, ktora ``LiteAudioSettings.Read``
    odrzuci wyjatkiem -- uzytkownik dostalby odmowe po Zapisz.
    """
    assert INTER_TRACK_SILENCE_CHOICES == (0, 500, 1000, 2000, 3000, 5000)


def test_zdolnosci_sa_niezmienne_i_opisane():
    caps = capabilities_for(SessionId.FILES)
    assert isinstance(caps, SessionCapabilities)
    try:
        caps.supports_audio_processing = False  # type: ignore[misc]
    except (AttributeError, TypeError):
        pass
    else:
        raise AssertionError("zdolnosci sesji nie moga byc przestawiane w locie")
