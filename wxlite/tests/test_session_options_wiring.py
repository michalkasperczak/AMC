"""Opcje sesji PODLACZONE do okna: wejscie, dyspozytor, zapis i SKUTEK.

Warstwa logiki (``session_options``) jest odebrana w ``test_session_options``
i tutaj jej nie powtarzamy. Sprawdzamy to, czego ta warstwa nie dowodzi:

* czy da sie Opcje sesji otworzyc Z MENU i Z KLAWIATURY (Ctrl+Alt+Enter),
* czy gest nie zabiera cudzego znaczenia w zadnym widoku,
* czy akcja jest NAPRAWDE obslugiwana w ``gui._dispatch``,
* czy Zapisz wysyla do silnika payload sesji, a Anuluj NIE wysyla nic,
* czy odmowa silnika nie konczy sie slowem "zapisano",
* czy wybor sesji w dialogu nie przestawia TEGO, co aktualnie gra.
"""

from __future__ import annotations

import ast
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import menu_model, session_options
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.session_options import (
    SessionPlaybackOverrides,
    apply_session_options,
    capabilities_for,
    resolve_audio_payload,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import LiteState, Options, StateStore

GUI_SOURCE = (Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py").read_text(
    encoding="utf-8"
)


# ----------------------------------------------------------- 1. wejscie


def test_ctrl_alt_enter_otwiera_opcje_sesji_w_obu_widokach():
    """Jedno WSPOLNE wejscie: ten sam gest na liscie i w odtwarzaczu."""
    chord = Chord("Return", ctrl=True, alt=True)
    for player_view in (False, True):
        for radio in (False, True):
            assert (
                resolve(chord, player_view=player_view, radio_session=radio)
                is Action.SESSION_OPTIONS
            ), f"brak gestu w widoku player={player_view}, radio={radio}"


def test_ctrl_alt_enter_nie_zabiera_cudzego_znaczenia():
    """Samo Enter zostaje aktywacja, Ctrl+Alt+Left/Right zostaja przewijaniem."""
    assert resolve(Chord("Return"), player_view=False, radio_session=False) is Action.ACTIVATE
    assert (
        resolve(Chord("Left", ctrl=True, alt=True), player_view=True, radio_session=False)
        is Action.SEEK_BACK_CUSTOM
    )
    assert (
        resolve(Chord("Right", ctrl=True, alt=True), player_view=True, radio_session=False)
        is Action.SEEK_FORWARD_CUSTOM
    )


def test_menu_ma_dokladnie_jedna_pozycje_opcji_sesji_z_prawdziwym_podpisem():
    items = [
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is Action.SESSION_OPTIONS
    ]
    assert len(items) == 1, "jedno wejscie w menu, nie dwa"
    item = items[0]
    # Nazwa klawisza jest TA SAMA co w tablicach skrotow; na "Enter" tlumaczy
    # ja ``_menu_shortcut_text`` przy budowie menu.
    assert item.shortcut == "Ctrl+Alt+Return", "podpis w menu musi byc prawda"
    # Opcje dotycza SESJI, nie zaznaczonego wiersza: wyszarzenie bez wyboru
    # wiersza zabralo by dostep do nich na pustej liscie.
    assert not item.needs_selection
    assert not item.needs_radio_session, "obie sesje maja swoje opcje"


def test_session_options_has_native_window_accelerator():
    """Ctrl+Alt+Enter needs the native accelerator, not list KEY_DOWN."""
    item = next(
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is Action.SESSION_OPTIONS
    )
    assert item.accelerator, "Ctrl+Alt+Enter must be registered as a native accelerator"


def test_filter_ctrl_alt_enter_opens_options_not_results():
    from types import SimpleNamespace
    from amc_wx_lite import gui

    called = []
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame._dispatch = lambda action: called.append(action)
    frame._focus_filter_results = lambda: called.append("filter-results")
    event = SimpleNamespace(
        GetKeyCode=lambda: gui.wx.WXK_RETURN,
        ControlDown=lambda: True,
        AltDown=lambda: True,
        ShiftDown=lambda: False,
        Skip=lambda: called.append("skip"),
    )
    gui.LiteFrame._on_filter_key(frame, event)
    assert called == [Action.SESSION_OPTIONS], called


def test_pozycja_menu_ma_wlasna_nazwe_a_nie_nazwe_ustawien_ogolnych():
    item = next(
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is Action.SESSION_OPTIONS
    )
    assert "sesji" in item.label.lower(), "nazwa musi mowic o ZAKRESIE sesji"


def test_pomoc_f1_wymienia_nowy_gest():
    from amc_wx_lite.shortcuts import describe

    opisy = dict(describe())
    assert "Ctrl+Alt+Return" in opisy or "Ctrl+Alt+Enter" in opisy, (
        "gest bez wpisu w pomocy jest dla uzytkownika niewidzialny"
    )


# ----------------------------------------------------------- 2. dyspozytor


def test_akcja_jest_obslugiwana_w_oknie():
    """Bez galezi w ``_dispatch`` menu i gest byly by martwe."""
    tree = ast.parse(GUI_SOURCE)
    used = {
        node.attr
        for node in ast.walk(tree)
        if isinstance(node, ast.Attribute)
        and isinstance(node.value, ast.Name)
        and node.value.id == "Action"
    }
    assert Action.SESSION_OPTIONS.name in used, "akcja nieobsluzona w gui.py"


def test_okno_ma_handler_opcji_sesji_i_uzywa_wspolnej_logiki():
    assert "_show_session_options" in GUI_SOURCE
    assert "apply_session_options" in GUI_SOURCE, (
        "okno musi uzywac wspolnej logiki, nie wlasnej kopii regul"
    )


# --------------------------------------- 3. Zapisz / Anuluj i skutek


class FakeClient:
    """Silnik-atrapa. Zapisuje KAZDE wywolanie, zeby dalo sie policzyc skutek."""

    def __init__(self, *, fail: bool = False) -> None:
        self.calls: list[tuple[str, dict]] = []
        self.fail = fail

    def configure_audio(self, **options):
        self.calls.append(("audio.configure", dict(options)))
        if self.fail:
            raise RuntimeError("Nieobsługiwana długość ciszy między utworami.")
        return {"appliesOnNextPlayback": True}


def test_zapisz_wysyla_payload_sesji_do_silnika():
    options = Options()
    client = FakeClient()
    state = LiteState(options=options)
    wynik = apply_session_options(
        state,
        SessionId.FILES,
        SessionPlaybackOverrides(loudness_normalization=True, inter_track_silence_ms=1000),
        client=client,
    )
    assert wynik.saved is True
    assert len(client.calls) == 1, "dokladnie jedno wywolanie silnika"
    name, payload = client.calls[0]
    assert name == "audio.configure"
    assert payload["loudnessNormalization"] is True
    assert payload["interTrackSilenceMs"] == 1000
    assert state.session_overrides["files"].loudness_normalization is True


def _run_handler(modal_result, *, session=SessionId.FILES, state=None, client=None):
    """Wykonaj PRAWDZIWY ``LiteFrame._show_session_options`` na atrapach.

    Grep po zrodle ("czy jest tam ID_OK") przechodzi takze wtedy, gdy warunek
    jest odwrocony albo martwy. Tutaj handler naprawde sie wykonuje, wiec
    "Anuluj mimo wszystko zapisuje" wychodzi jako czerwony test.
    """
    import test_gui_logic

    test_gui_logic.install_wx_stub()
    import wx

    from amc_wx_lite import gui

    state = state if state is not None else LiteState(options=Options())
    client = client if client is not None else FakeClient()
    saved: list[bool] = []
    spoken: list[str] = []

    class FakeDialog:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def ShowModal(self):  # noqa: N802 - API wx
            return modal_result

        def Destroy(self) -> None:  # noqa: N802 - API wx
            pass

        @property
        def drafts(self):
            # Dialog oddaje wybory per SESJA: wymaganiem bylo ustawienie
            # drugiej sesji bez przelaczania odsluchu.
            return {session: SessionPlaybackOverrides(loudness_normalization=True)}

    class FakeNavigator:
        active = session

    class ImmediateRunner:
        """TaskRunner bez watku: ``work`` i ``done`` w tej samej kolejnosci."""

        def submit(self, _name, work, done, failed):
            try:
                done(work())
            except Exception as error:  # noqa: BLE001 - jak prawdziwy runner
                failed(error)

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = FakeNavigator()
    frame.state = state
    frame.options = state.options
    frame.client = client
    frame.runner = ImmediateRunner()
    frame.announcer = type("A", (), {"say": lambda _s, text: spoken.append(text)})()
    frame._save_state = lambda: saved.append(True) or True
    frame._restore_focus_after_dialog = lambda: None

    original = gui.SessionOptionsDialog
    gui.SessionOptionsDialog = lambda *a, **k: FakeDialog()
    try:
        frame._show_session_options()
    finally:
        gui.SessionOptionsDialog = original
    return {"saved": saved, "spoken": spoken, "client": client, "state": state,
            "ID_OK": wx.ID_OK}


def test_anuluj_nie_zapisuje_i_nie_rusza_silnika():
    """ESC, krzyzyk i Anuluj: zaden z nich nie dotyka stanu ani silnika.

    Handler jest WYKONYWANY z wynikiem modalnym innym niz ``ID_OK``.
    """
    import wx

    for result in (wx.ID_CANCEL, 0, -1):
        out = _run_handler(result)
        assert out["client"].calls == [], f"wynik {result}: silnik nietkniety"
        assert out["state"].session_overrides == {}, f"wynik {result}: brak zapisu"
        assert out["saved"] == [], f"wynik {result}: profil nie zapisany"
        assert out["spoken"] == [], f"wynik {result}: brak slowa 'zapisano'"


def test_zapisz_wykonuje_pelny_pion_az_do_profilu():
    """Zatwierdzenie: silnik + utrwalenie + komunikat. Przeciwienstwo Anuluj."""
    import wx

    out = _run_handler(wx.ID_OK)
    assert len(out["client"].calls) == 1, "silnik wywolany dokladnie raz"
    assert out["state"].session_overrides["files"].loudness_normalization is True
    assert out["saved"] == [True], "zapis profilu nastepuje po zgodzie silnika"
    assert out["spoken"] and "apisano" in out["spoken"][0]
    assert "Przetwarzanie dźwięku" in out["spoken"][0], (
        "Odroczony skutek dotyczy audio, nie od razu działającej polityki pauzy"
    )


def test_odmowa_silnika_w_oknie_nie_utrwala_profilu():
    """Silnik odmawia -> brak 'zapisano' i brak zapisu na dysk."""
    import wx

    out = _run_handler(wx.ID_OK, client=FakeClient(fail=True))
    assert out["saved"] == [], "odmowa silnika nie zapisuje profilu"
    assert out["spoken"] and "zapisano" not in out["spoken"][0].lower()
    assert out["state"].session_overrides == {}


def test_odmowa_silnika_nie_daje_slowa_zapisano_ani_nie_utrwala():
    options = Options()
    client = FakeClient(fail=True)
    state = LiteState(options=options)
    wynik = apply_session_options(
        state,
        SessionId.FILES,
        SessionPlaybackOverrides(loudness_normalization=True),
        client=client,
    )
    assert wynik.saved is False, "odmowa silnika to nie zapis"
    assert "zapisano" not in wynik.message.lower()
    assert wynik.error is not None
    assert state.session_overrides == {}, "odrzucony wybor nie moze zostac w stanie"


def test_wybor_sesji_radio_nie_przestawia_przetwarzania_plikow():
    """Dialog wybiera KONFIGUROWANA sesje, nie przelacza odsluchu.

    Radio nie ma przetwarzania dzwieku, wiec jego Opcje sesji nie moga wyslac
    pol, ktore konfiguruja wyjscie plikow lokalnych.
    """
    options = Options()
    client = FakeClient()
    state = LiteState(options=options)
    wynik = apply_session_options(
        state,
        SessionId.RADIO,
        # Nawet gdy ktos poda pole spoza zdolnosci Radia -- ma byc odrzucone.
        SessionPlaybackOverrides(loudness_normalization=True, pause_on_player_exit=False),
        client=client,
    )
    assert wynik.saved is True
    zapisane = state.session_overrides["radio"]
    assert zapisane.pause_on_player_exit is False
    assert zapisane.loudness_normalization is None, (
        "Radio nie ma normalizacji -- wybor nie moze zostac utrwalony"
    )
    # Wstrzymanie po wyjsciu dziala w OKNIE, nie w silniku: bez pol dzwieku
    # nie ma po co ruszac silnika i przestawiac grajacych plikow.
    assert client.calls == [], "opcja okna nie konfiguruje silnika"


def test_zapis_radia_nie_zmienia_globalnego_algorytmu_tempa():
    options = Options()
    options.tempo_algorithm = 2
    payload = resolve_audio_payload(
        options, SessionPlaybackOverrides(loudness_normalization=True)
    )
    assert payload["tempoAlgorithm"] == 2


def test_zapis_trafia_na_dysk_i_wraca_po_ponownym_wczytaniu():
    """Realny Save + reload, nie tylko stan w pamieci."""
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        state = LiteState()
        client = FakeClient()
        apply_session_options(
            state,
            SessionId.FILES,
            SessionPlaybackOverrides(smooth_track_transitions=True),
            client=client,
        )
        store.save(state)
        wczytany = store.load()
        assert wczytany.session_overrides["files"].smooth_track_transitions is True


def test_powrot_do_dziedziczenia_usuwa_wpis_sesji():
    options = Options()
    client = FakeClient()
    state = LiteState(options=options)
    apply_session_options(
        state, SessionId.FILES, SessionPlaybackOverrides(loudness_normalization=True),
        client=client,
    )
    assert "files" in state.session_overrides
    apply_session_options(
        state, SessionId.FILES, SessionPlaybackOverrides(), client=client
    )
    assert "files" not in state.session_overrides, (
        "brak wyboru musi WRACAC do dziedziczenia, nie zostawac falszem"
    )


def test_brak_silnika_nie_przewraca_zapisu_opcji_okna():
    """Bez hosta Opcje sesji, ktore dzialaja w oknie, nadal maja sie zapisac."""
    state = LiteState()
    wynik = apply_session_options(
        state, SessionId.RADIO, SessionPlaybackOverrides(pause_on_player_exit=False),
        client=None,
    )
    assert wynik.saved is True
    assert state.session_overrides["radio"].pause_on_player_exit is False


def test_brak_silnika_odmawia_uczciwie_opcjom_ktore_go_wymagaja():
    state = LiteState()
    wynik = apply_session_options(
        state, SessionId.FILES, SessionPlaybackOverrides(loudness_normalization=True),
        client=None,
    )
    assert wynik.saved is False
    assert "zapisano" not in wynik.message.lower()


# ----------------------------------------------------------- 4. mowa


def test_potwierdzenie_zapisu_jest_krotkie_i_mowi_zakres():
    options = Options()
    client = FakeClient()
    state = LiteState(options=options)
    wynik = apply_session_options(
        state, SessionId.FILES, SessionPlaybackOverrides(loudness_normalization=True),
        client=client,
    )
    assert wynik.message
    assert len(wynik.message) < 200, "komunikat czytnika ma byc krotki"
    assert "sesj" in wynik.message.lower()


def test_tytul_dialogu_nazywa_konfigurowana_sesje():
    for session, nazwa in ((SessionId.FILES, "Pliki lokalne"), (SessionId.RADIO, "Radio")):
        tytul = session_options.dialog_title(session)
        assert "Opcje sesji" in tytul
        assert nazwa.split()[0] in tytul


def test_kazda_sesja_portu_ma_co_pokazac():
    for session in (SessionId.FILES, SessionId.RADIO):
        assert capabilities_for(session).has_options is True
