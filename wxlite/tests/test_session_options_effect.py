"""Opcje sesji: SKUTEK, a nie tylko dialog i parser.

Ten plik mierzy wylacznie to, czego poprzedni przyrost NIE dowodzil. Kazdy
test wykonuje PRAWDZIWA metode ``LiteFrame`` (wyciagnieta z AST zrodla, tak
jak robi to pomiar rodzica), zeby "jest w zrodle" nie przechodzilo za
"dziala".

Pieciu brakow pilnuja grupy:

1. fokus po zamknieciu dialogu -- ``_active_list()``, nie nieistniejace
   ``list_ctrl``; i ustawiany PO ``Destroy``, bo przed nim wx oddaje fokus
   sam,
2. odmowa zapisu na dysk nie moze konczyc sie slowem "Zapisano" ani zostawiac
   nowego wyboru w RAM/silniku,
3. nowy proces musi dostac EFEKTYWNY payload (z wyborami sesji), inaczej zapis
   wraca z dysku bez skutku w silniku,
4. ``resolve_pause_on_player_exit`` musi miec PRODUKCYJNEGO wolacza i naprawde
   wstrzymywac po wyjsciu z odtwarzacza -- bez ruszania cudzej grajacej sesji,
5. dialog musi pozwolic WYBRAC konfigurowana sesje bez przelaczania odsluchu.
"""

from __future__ import annotations

import ast
import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import session_options
from amc_wx_lite.navigation import SessionId, View
from amc_wx_lite.session_options import SessionPlaybackOverrides
from amc_wx_lite.state_store import LiteState, Options

GUI_PATH = Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
GUI_SOURCE = GUI_PATH.read_text(encoding="utf-8")
_TREE = ast.parse(GUI_SOURCE)
_FRAME = next(
    node
    for node in _TREE.body
    if isinstance(node, ast.ClassDef) and node.name == "LiteFrame"
)


def real_method(name: str, env: dict | None = None):
    """Wyciagnij PRAWDZIWA metode ``LiteFrame`` i skompiluj ja osobno.

    Budowa calego okna wymaga pulpitu i zywego hosta, a te testy maja chodzic
    bez obu. Kompilujemy wiec sam ``FunctionDef`` z prawdziwego zrodla -- tresc
    jest ta sama, ktora wykona sie w aplikacji.
    """
    node = next(
        item
        for item in _FRAME.body
        if isinstance(item, ast.FunctionDef) and item.name == name
    )
    module = ast.Module(body=[node], type_ignores=[])
    namespace: dict = {
        "View": View,
        "SessionId": SessionId,
        "session_options": session_options,
        **(env or {}),
    }
    exec(compile(module, str(GUI_PATH), "exec"), namespace)
    return namespace[name]


class FakeClient:
    """Silnik-atrapa liczaca KAZDE wywolanie protokolu."""

    def __init__(self, *, fail_audio: bool = False, paused: bool = False) -> None:
        self.calls: list[tuple[str, dict]] = []
        self.fail_audio = fail_audio
        self._paused = paused

    def configure_audio(self, **options):
        self.calls.append(("audio.configure", dict(options)))
        if self.fail_audio:
            raise RuntimeError("Nieobsługiwana długość ciszy między utworami.")
        return {"appliesOnNextPlayback": True}

    def pause_resume(self):
        self.calls.append(("transport.pauseResume", {}))
        self._paused = not self._paused
        return {"paused": self._paused}

    def status(self):
        self.calls.append(("transport.status", {}))
        return {"paused": self._paused}

    def start(self) -> None:
        self.calls.append(("start", {}))

    def hello(self):
        self.calls.append(("hello", {}))
        return {}

    def close(self) -> None:
        self.calls.append(("close", {}))

    def call(self, op, args=None, **kwargs):
        self.calls.append((op, dict(args or {})))
        return {}

    @property
    def audio_payloads(self) -> list[dict]:
        return [payload for name, payload in self.calls if name == "audio.configure"]


class ImmediateRunner:
    """TaskRunner-atrapa: wykonuje w miejscu, ale TA SAMA droga co produkcja."""

    def __init__(self) -> None:
        self.submitted: list[str] = []

    def submit(self, name, work, done, failed):
        self.submitted.append(name)
        try:
            result = work()
        except Exception as error:  # noqa: BLE001 - atrapa odwzorowuje runner
            failed(error)
            return
        done(result)


# ------------------------------------------------- 1. fokus po dialogu


def test_fokus_po_dialogu_uzywa_istniejacej_listy_sesji():
    """``list_ctrl`` nie istnieje w ``LiteFrame`` -- fokus musi isc na _active_list().

    RED rodzica: ``AttributeError`` przy KAZDYM zamknieciu dialogu, wiec fokus
    gubil sie i po Anuluj, i po Zapisz.
    """
    focused: list[str] = []
    files_list = SimpleNamespace(SetFocus=lambda: focused.append("files"))
    radio_list = SimpleNamespace(SetFocus=lambda: focused.append("radio"))
    play_button = SimpleNamespace(SetFocus=lambda: focused.append("player"))

    for active, oczekiwane in ((SessionId.FILES, "files"), (SessionId.RADIO, "radio")):
        focused.clear()
        frame = SimpleNamespace(
            navigator=SimpleNamespace(
                active=active,
                session=SimpleNamespace(view=View.LIST),
            ),
            files_list=files_list,
            radio_list=radio_list,
            play_button=play_button,
        )
        frame._active_list = real_method("_active_list").__get__(frame, type(frame))
        real_method("_restore_focus_after_dialog")(frame)
        assert focused == [oczekiwane], f"sesja {active}: fokus na jej wlasna liste"


def test_fokus_po_dialogu_w_odtwarzaczu_wraca_na_przycisk():
    focused: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.FILES,
            session=SimpleNamespace(view=View.PLAYER),
        ),
        files_list=SimpleNamespace(SetFocus=lambda: focused.append("files")),
        radio_list=SimpleNamespace(SetFocus=lambda: focused.append("radio")),
        play_button=SimpleNamespace(SetFocus=lambda: focused.append("player")),
    )
    real_method("_restore_focus_after_dialog")(frame)
    assert focused == ["player"]


def test_zrodlo_nie_wola_nieistniejacego_list_ctrl():
    """Zabezpieczenie przed powrotem tej samej pomylki w innym miejscu."""
    assert "self.list_ctrl" not in GUI_SOURCE, (
        "LiteFrame nie ma pola list_ctrl -- jest _active_list()"
    )


def test_fokus_wraca_po_zniszczeniu_dialogu_nie_przed():
    """Fokus ustawiany PRZED ``Destroy`` oddaje go z powrotem oknu modalnemu.

    Mierzymy KOLEJNOSC zdarzen na prawdziwym handlerze dla drogi Anuluj.
    """
    kolejnosc: list[str] = []
    state = LiteState(options=Options())

    class Dialog:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def ShowModal(self):  # noqa: N802 - API wx
            return 0

        def Destroy(self) -> None:  # noqa: N802 - API wx
            kolejnosc.append("destroy")

        @property
        def session(self):
            return SessionId.FILES

        @property
        def overrides(self):
            return SessionPlaybackOverrides()

        @property
        def drafts(self):
            return {}

    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=FakeClient(),
        navigator=SimpleNamespace(active=SessionId.FILES, snapshot=lambda: {}),
        announcer=SimpleNamespace(say=lambda _t: None),
        runner=ImmediateRunner(),
        _save_state=lambda: True,
        _restore_focus_after_dialog=lambda: kolejnosc.append("focus"),
    )
    real_method(
        "_show_session_options",
        {"SessionOptionsDialog": Dialog, "wx": SimpleNamespace(ID_OK=5100)},
    )(frame)
    assert kolejnosc == ["destroy", "focus"], (
        "fokus wraca dopiero po zniszczeniu okna modalnego"
    )


# ------------------------------------------- 2. odmowa zapisu to nie sukces


def _handler_with_disk(refusal: bool, *, client=None, state=None):
    """Prawdziwy ``_show_session_options`` + prawdziwy ``_save_state``."""
    state = state if state is not None else LiteState(options=Options())
    client = client if client is not None else FakeClient()
    spoken: list[str] = []
    zapisy: list[str] = []

    class Store:
        def save(self, _state) -> None:
            zapisy.append("save")
            if refusal:
                raise OSError("synthetic write refusal")

    class Dialog:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def ShowModal(self):  # noqa: N802 - API wx
            return 5100

        def Destroy(self) -> None:  # noqa: N802 - API wx
            pass

        @property
        def session(self):
            return SessionId.FILES

        @property
        def overrides(self):
            return SessionPlaybackOverrides(loudness_normalization=True)

        @property
        def drafts(self):
            return {SessionId.FILES: SessionPlaybackOverrides(loudness_normalization=True)}

    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=client,
        store=Store(),
        navigator=SimpleNamespace(active=SessionId.FILES, snapshot=lambda: {}),
        announcer=SimpleNamespace(say=spoken.append),
        runner=ImmediateRunner(),
        _restore_focus_after_dialog=lambda: None,
    )
    frame._save_state = real_method("_save_state").__get__(frame, type(frame))
    frame._apply_session_option_drafts = real_method(
        "_apply_session_option_drafts"
    ).__get__(frame, type(frame))
    real_method(
        "_show_session_options",
        {"SessionOptionsDialog": Dialog, "wx": SimpleNamespace(ID_OK=5100)},
    )(frame)
    return {"spoken": spoken, "state": state, "client": client, "zapisy": zapisy}


def test_odmowa_zapisu_na_dysk_nie_mowi_zapisano():
    """RED rodzica: byl komunikat 'Nie moge zapisac' ORAZ 'Zapisano...'."""
    out = _handler_with_disk(True)
    assert out["zapisy"], "proba zapisu musi sie odbyc"
    polaczone = " | ".join(out["spoken"])
    assert not any(
        "zapisano" in text.lower() for text in out["spoken"]
    ), f"odmowa zapisu nie jest sukcesem: {polaczone}"


def test_odmowa_zapisu_mowi_dokladnie_jeden_komunikat():
    out = _handler_with_disk(True)
    assert len(out["spoken"]) == 1, (
        f"jeden prawdziwy komunikat, nie dwa sprzeczne: {out['spoken']}"
    )
    assert "zapis" in out["spoken"][0].lower()


def test_odmowa_zapisu_nie_zostawia_nowego_wyboru_w_ram():
    """Dysk odmowil -> RAM musi zostac przy STARYM trwalym stanie."""
    out = _handler_with_disk(True)
    assert out["state"].session_overrides.get("files") is None, (
        "wybor nieutrwalony nie moze zostac w pamieci jako obowiazujacy"
    )


def test_odmowa_zapisu_przywraca_silnik_do_stanu_trwalego():
    """Silnik nie moze grac wyborem, ktorego nie ma ani w RAM, ani na dysku."""
    client = FakeClient()
    out = _handler_with_disk(True, client=client)
    payloads = out["client"].audio_payloads
    assert payloads, "silnik byl wolany przy zmianie pol dzwieku"
    assert payloads[-1]["loudnessNormalization"] is False, (
        "po odmowie zapisu silnik wraca do ustawienia ogolnego, "
        f"a dostal {payloads[-1]}"
    )


def test_udany_zapis_nadal_mowi_zapisano_i_utrwala():
    out = _handler_with_disk(False)
    assert out["state"].session_overrides["files"].loudness_normalization is True
    assert any("apisano" in text for text in out["spoken"]), out["spoken"]


def test_komunikat_zapisu_mowi_od_kiedy_dziala_gdy_host_tak_zwraca():
    """Host zwraca ``appliesOnNextPlayback`` -- komunikat nie moze tego zjesc."""
    out = _handler_with_disk(False)
    polaczone = " ".join(out["spoken"]).lower()
    assert "uruchomieni" in polaczone or "otwarciu" in polaczone or "nastepn" in polaczone, (
        f"uzytkownik musi wiedziec, ze skutek jest od nastepnego odtwarzania: {out['spoken']}"
    )


def test_save_state_zwraca_informacje_o_powodzeniu():
    """Bez wyniku wolacz nie umie rozpoznac odmowy dysku."""
    for refusal, oczekiwane in ((False, True), (True, False)):
        spoken: list[str] = []

        class Store:
            def save(self, _state) -> None:
                if refusal:
                    raise OSError("synthetic write refusal")

        state = LiteState(options=Options())
        frame = SimpleNamespace(
            state=state,
            options=state.options,
            store=Store(),
            navigator=SimpleNamespace(snapshot=lambda: {}),
            announcer=SimpleNamespace(say=spoken.append),
        )
        wynik = real_method("_save_state")(frame)
        assert wynik is oczekiwane, f"odmowa={refusal} ma zwrocic {oczekiwane}"


# --------------------------------------- 3. nowy proces z EFEKTYWNYM payloadem


def test_nowy_proces_dostaje_wybory_sesji_a_nie_sam_global():
    """RED rodzica: zapis wracal z dysku, a host dostawal ustawienie ogolne."""
    state = LiteState(options=Options())
    state.options.loudness_normalization = False
    state.session_overrides["files"] = SessionPlaybackOverrides(
        loudness_normalization=True, inter_track_silence_ms=2000
    )
    client = FakeClient()
    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=None,
        runner=ImmediateRunner(),
        timer=SimpleNamespace(Start=lambda _ms: None),
        library=SimpleNamespace(use_collation=lambda _c: None),
        announcer=SimpleNamespace(say=lambda _t: None),
        _queue_persistence_arguments=lambda: {},
        _on_engine_event=lambda *a: None,
        _load_initial_content=lambda: None,
    )
    real_method(
        "_start_engine",
        {
            "LiteHostClient": lambda *a, **kw: client,
            "default_host_path": lambda: Path("synthetic-host"),
            "HostCollation": lambda _call: object(),
        },
    )(frame)
    payloads = client.audio_payloads
    assert payloads, "start musi skonfigurowac dzwiek"
    assert payloads[-1]["loudnessNormalization"] is True, (
        f"zapisany wybor sesji musi dojsc do nowego procesu: {payloads[-1]}"
    )
    assert payloads[-1]["interTrackSilenceMs"] == 2000


def test_zmiana_algorytmu_tempa_nie_ucina_wyborow_sesji():
    """Wspolny payload: menu Dzwiek nie moze zdmuchnac opcji sesji."""
    state = LiteState(options=Options())
    state.options.loudness_normalization = False
    state.session_overrides["files"] = SessionPlaybackOverrides(
        loudness_normalization=True
    )
    client = FakeClient()
    items = {
        0: SimpleNamespace(Check=lambda _v: None, Enable=lambda _v: None),
        1: SimpleNamespace(Check=lambda _v: None, Enable=lambda _v: None),
        2: SimpleNamespace(Check=lambda _v: None, Enable=lambda _v: None),
    }
    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=client,
        runner=ImmediateRunner(),
        tempo_items=items,
        announcer=SimpleNamespace(say=lambda _t: None),
        _save_state=lambda: True,
    )
    real_method(
        "_set_tempo_algorithm",
        {"TEMPO_LABELS": {0: "a", 1: "b", 2: "c"}},
    )(frame, 2)
    payloads = client.audio_payloads
    assert payloads, "zmiana algorytmu wola silnik"
    assert payloads[-1]["tempoAlgorithm"] == 2
    assert payloads[-1]["loudnessNormalization"] is True, (
        f"wybor sesji nie moze zniknac przy zmianie algorytmu: {payloads[-1]}"
    )


def test_efektywny_payload_liczy_jedna_wspolna_funkcja():
    """Dwie kopie regul rozjechalyby sie przy pierwszej zmianie."""
    assert GUI_SOURCE.count("session_options.engine_audio_payload") >= 2, (
        "start silnika i zmiana algorytmu musza uzywac TEJ SAMEJ funkcji"
    )
    # Reguly nie moga byc skopiowane obok wspolnej funkcji.
    assert "self.options.audio_payload()" not in GUI_SOURCE.replace(
        "``options.audio_payload()``", ""
    ), "surowy payload ogolny ucina wybory sesji"


# ----------------------------------------------- 4. pauza po wyjsciu z odtwarzacza


def test_polityka_pauzy_ma_produkcyjnego_wolacza():
    """RED rodzica: ``resolve_pause_on_player_exit`` nie mial ani jednego."""
    callers: list[str] = []
    folder = GUI_PATH.parent
    for path in sorted(folder.glob("*.py")):
        tree = ast.parse(path.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            func = node.func
            if isinstance(func, ast.Name) and func.id == "resolve_pause_on_player_exit":
                callers.append(path.name)
            elif (
                isinstance(func, ast.Attribute)
                and func.attr == "resolve_pause_on_player_exit"
            ):
                callers.append(path.name)
    assert "gui.py" in callers, f"kontrolka bez wolacza jest martwa, wolacze: {callers}"


def _pause_frame(*, overrides=None, global_pause=True, playing_session=SessionId.FILES,
                 active=SessionId.FILES, paused=False,
                 now_playing: str | None = "item-1"):
    state = LiteState(options=Options())
    state.options.pause_on_player_exit = global_pause
    if overrides is not None:
        state.session_overrides[active.value] = overrides
    client = FakeClient(paused=paused)
    session_state = SimpleNamespace(
        view=View.PLAYER,
        now_playing_id=now_playing,
    )
    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=client,
        runner=ImmediateRunner(),
        _playing_session=playing_session,
        _last_status={"paused": paused},
        navigator=SimpleNamespace(
            active=active,
            session=session_state,
            sessions={active: session_state},
        ),
        announcer=SimpleNamespace(say=lambda _t: None),
        _set_transport_label=lambda **kw: None,
    )
    return frame, client


def test_wyjscie_z_odtwarzacza_wstrzymuje_gdy_tak_ustawiono():
    frame, client = _pause_frame(
        overrides=SessionPlaybackOverrides(pause_on_player_exit=True),
        global_pause=False,
    )
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.FILES)
    assert ("transport.pauseResume", {}) in client.calls, (
        f"wybor sesji 'wstrzymuj' musi naprawde wstrzymac: {client.calls}"
    )


def test_wyjscie_z_odtwarzacza_nie_wstrzymuje_gdy_sesja_tak_wybrala():
    """Radio: Escape nie moze przerywac transmisji, gdy tak ustawiono."""
    frame, client = _pause_frame(
        overrides=SessionPlaybackOverrides(pause_on_player_exit=False),
        global_pause=True,
        playing_session=SessionId.RADIO,
        active=SessionId.RADIO,
    )
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.RADIO)
    assert ("transport.pauseResume", {}) not in client.calls, (
        f"wybor 'odtwarzaj dalej' musi byc respektowany: {client.calls}"
    )


def test_domyslnie_wstrzymuje_jak_oryginal():
    """``AppSettings.PausePlaybackWhenLeavingPlayer`` = true (AppSettings.cs:98).

    Sesja bez wlasnego wyboru dziedziczy ustawienie ogolne, ktorego domysl
    oryginal ma na ``true`` -- porownane ze zrodlem, nie zalozone.
    """
    assert Options().pause_on_player_exit is True
    frame, client = _pause_frame(overrides=None, global_pause=True)
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.FILES)
    assert ("transport.pauseResume", {}) in client.calls


def test_nie_rusza_cudzej_grajacej_sesji():
    """Wyjscie z odtwarzacza PLIKOW nie moze wstrzymac grajacego radia."""
    frame, client = _pause_frame(
        overrides=SessionPlaybackOverrides(pause_on_player_exit=True),
        playing_session=SessionId.RADIO,
        active=SessionId.FILES,
    )
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.FILES)
    assert ("transport.pauseResume", {}) not in client.calls, (
        f"host gra RADIO -- pauza plikow ruszylaby cudza sesje: {client.calls}"
    )


def test_zapis_podcastow_czeka_do_startu_i_nie_przestawia_grajacego_pliku():
    """Pliki i Podcasty dzielą wyjście, ale mają osobne ustawienia sesji."""
    state = LiteState(options=Options())
    client = FakeClient()

    result = session_options.apply_session_options(
        state,
        SessionId.PODCASTS,
        SessionPlaybackOverrides(loudness_normalization=True),
        client=client,
    )

    assert result.saved is True
    assert result.applies_on_next_playback is True
    assert client.calls == [], "zapis Podcastów nie rusza wspólnego wyjścia teraz"
    assert state.session_overrides["podcasts"].loudness_normalization is True


def test_wycofanie_opcji_podcastow_też_czeka_do_nastepnego_startu():
    state = LiteState(options=Options())
    previous = SessionPlaybackOverrides(loudness_normalization=True)
    state.session_overrides["podcasts"] = previous
    client = FakeClient()

    session_options.restore_session_options(
        state,
        SessionId.PODCASTS,
        SessionPlaybackOverrides(),
        client=client,
    )

    assert client.calls == []
    assert "podcasts" not in state.session_overrides


def test_nie_wstrzymuje_gdy_juz_wstrzymane():
    """Druga pauza wznowilaby odtwarzanie -- dokladnie odwrotnie niz opcja."""
    frame, client = _pause_frame(
        overrides=SessionPlaybackOverrides(pause_on_player_exit=True),
        paused=True,
    )
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.FILES)
    assert ("transport.pauseResume", {}) not in client.calls, (
        f"wstrzymane zostaje wstrzymane: {client.calls}"
    )


def test_nie_wstrzymuje_gdy_sesja_nic_nie_odtwarza():
    frame, client = _pause_frame(
        overrides=SessionPlaybackOverrides(pause_on_player_exit=True),
        now_playing=None,
    )
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.FILES)
    assert ("transport.pauseResume", {}) not in client.calls


def test_pauza_idzie_przez_runner_nie_na_watku_gui():
    frame, client = _pause_frame(
        overrides=SessionPlaybackOverrides(pause_on_player_exit=True)
    )
    real_method("_pause_on_player_exit_if_needed")(frame, SessionId.FILES)
    assert frame.runner.submitted, "operacja sieciowa idzie przez TaskRunner"


def test_wszystkie_obslugiwane_wyjscia_pauzuja():
    """Escape, F6 i menu Lista -- wszystkie prowadza przez ten sam guard.

    ``back_to_list`` jest jedynym przejsciem odtwarzacz -> lista w nawigatorze
    (Escape, F6 i Action.SHOW_LIST wszystkie koncza na nim), wiec guard musi
    stac dokladnie przy nim w ``gui.py``.
    """
    dispatch = next(
        item
        for item in _FRAME.body
        if isinstance(item, ast.FunctionDef) and item.name == "_dispatch"
    )
    wywolania = {
        node.func.attr
        for node in ast.walk(dispatch)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
    }
    assert "_pause_on_player_exit_if_needed" in wywolania or any(
        "_leave_player" in name for name in wywolania
    ), f"dyspozytor musi wolac guard pauzy, wola: {sorted(wywolania)}"


def test_polityka_pauzy_nie_czysci_filtra_ani_listy():
    """Zapisana opcja zastepuje regule pauzy, a nie reszte zachowania wyjscia."""
    node = next(
        item
        for item in _FRAME.body
        if isinstance(item, ast.FunctionDef)
        and item.name == "_pause_on_player_exit_if_needed"
    )
    tresc = ast.unparse(node)
    for zakazane in ("filter_box", "clear_filter", "DeleteAllItems", "select_id"):
        assert zakazane not in tresc, (
            f"guard pauzy nie moze dotykac {zakazane} -- to inna droga"
        )


# -------------------------------------- 5. wybor sesji bez zmiany odsluchu


def test_dialog_pozwala_wybrac_konfigurowana_sesje():
    """Sam tytul okna nie jest wyborem -- musi byc kontrolka wyboru sesji."""
    dialog = next(
        node
        for node in _TREE.body
        if isinstance(node, ast.ClassDef) and node.name == "SessionOptionsDialog"
    )
    tresc = ast.unparse(dialog)
    assert "session_choice" in tresc, (
        "dialog musi miec kontrolke wyboru konfigurowanej sesji"
    )
    init = next(
        item
        for item in dialog.body
        if isinstance(item, ast.FunctionDef) and item.name == "__init__"
    )
    assert "drafts" in ast.unparse(dialog), (
        "wybory kazdej sesji musza przetrwac przelaczenie w dialogu"
    )
    assert init is not None


def test_wybor_sesji_w_dialogu_nie_przelacza_odsluchu():
    """Zmiana konfigurowanej sesji NIE moze wolac ``_switch_session``."""
    dialog = next(
        node
        for node in _TREE.body
        if isinstance(node, ast.ClassDef) and node.name == "SessionOptionsDialog"
    )
    tresc = ast.unparse(dialog)
    for zakazane in ("_switch_session", "navigator.active =", "play_file", "play_station"):
        assert zakazane not in tresc, (
            f"dialog nie moze zmieniac odsluchu ({zakazane})"
        )


def test_handler_zapisuje_kazda_edytowana_sesje():
    """Kontrakt draftu: Zapisz utrwala wszystkie zmienione sesje, Anuluj zadnej."""
    state = LiteState(options=Options())
    client = FakeClient()
    spoken: list[str] = []

    class Dialog:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def ShowModal(self):  # noqa: N802 - API wx
            return 5100

        def Destroy(self) -> None:  # noqa: N802 - API wx
            pass

        @property
        def session(self):
            return SessionId.RADIO

        @property
        def drafts(self):
            return {
                SessionId.FILES: SessionPlaybackOverrides(loudness_normalization=True),
                SessionId.RADIO: SessionPlaybackOverrides(pause_on_player_exit=False),
            }

    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=client,
        navigator=SimpleNamespace(active=SessionId.RADIO, snapshot=lambda: {}),
        announcer=SimpleNamespace(say=spoken.append),
        runner=ImmediateRunner(),
        _save_state=lambda: True,
        _restore_focus_after_dialog=lambda: None,
    )
    frame._apply_session_option_drafts = real_method(
        "_apply_session_option_drafts"
    ).__get__(frame, type(frame))
    real_method(
        "_show_session_options",
        {"SessionOptionsDialog": Dialog, "wx": SimpleNamespace(ID_OK=5100)},
    )(frame)
    assert state.session_overrides["files"].loudness_normalization is True, (
        f"edycja drugiej sesji musi sie zapisac: {state.session_overrides}"
    )
    assert state.session_overrides["radio"].pause_on_player_exit is False


def test_anuluj_nie_zapisuje_zadnej_z_edytowanych_sesji():
    state = LiteState(options=Options())
    client = FakeClient()
    spoken: list[str] = []

    class Dialog:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def ShowModal(self):  # noqa: N802 - API wx
            return 0

        def Destroy(self) -> None:  # noqa: N802 - API wx
            pass

        @property
        def session(self):
            return SessionId.FILES

        @property
        def drafts(self):
            return {
                SessionId.FILES: SessionPlaybackOverrides(loudness_normalization=True),
                SessionId.RADIO: SessionPlaybackOverrides(pause_on_player_exit=False),
            }

    frame = SimpleNamespace(
        state=state,
        options=state.options,
        client=client,
        navigator=SimpleNamespace(active=SessionId.FILES, snapshot=lambda: {}),
        announcer=SimpleNamespace(say=spoken.append),
        runner=ImmediateRunner(),
        _save_state=lambda: True,
        _restore_focus_after_dialog=lambda: None,
    )
    real_method(
        "_show_session_options",
        {"SessionOptionsDialog": Dialog, "wx": SimpleNamespace(ID_OK=5100)},
    )(frame)
    assert state.session_overrides == {}, "Anuluj nie zapisuje nic"
    assert client.calls == [], "Anuluj nie dotyka silnika"
    assert spoken == [], "Anuluj nie oglasza zapisu"


def test_operacja_silnika_idzie_przez_runner_a_nie_synchronicznie():
    """``configure_audio`` na watku GUI zawiesza okno na czas call timeout."""
    node = next(
        item
        for item in _FRAME.body
        if isinstance(item, ast.FunctionDef)
        and item.name == "_apply_session_option_drafts"
    )
    tresc = ast.unparse(node)
    assert "runner.submit" in tresc, (
        "nowa operacja musi uzywac istniejacego TaskRunnera z bramka kontekstu"
    )
    # ...i NIE wolac hosta wprost z watku GUI.
    szkielet = ast.unparse(
        next(
            item
            for item in _FRAME.body
            if isinstance(item, ast.FunctionDef)
            and item.name == "_show_session_options"
        )
    )
    assert "configure_audio" not in szkielet, (
        "wejscie nie moze rozmawiac z hostem synchronicznie"
    )


def test_bez_nowych_opcji_w_dialogu():
    """Zakres przyrostu: zadnej nowej opcji odtwarzania poza uzgodnionymi."""
    assert set(session_options.SESSION_OPTION_IDS) == {
        "loudness_normalization",
        "smooth_track_transitions",
        "inter_track_silence_ms",
        "pause_on_player_exit",
    }
