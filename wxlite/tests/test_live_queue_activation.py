"""Enter w widoku Zapisanej kolejki uruchamia ZYWA kolejke hosta.

Dotychczas widok Ctrl+Q byl tylko do odczytu: Enter skladal zwykle
``files.play`` i po koncu utworu nie dzialo sie nic. Te testy pilnuja, ze z
tego widoku powstaje zlecenie ``PlayFromQueue`` -- czyli ze nastepstwo liczy
kolejka hosta, a nie pojedyncze odtworzenie pliku.

Czego te testy NIE mierza: samego przejscia miedzy utworami. To rozstrzyga
sesja Core po stronie hosta i sprawdza zestaw ``--queue`` testow protokolu.
Tutaj chodzi o droge GUI: ktore zlecenie powstaje i czy wybor zostaje tam,
gdzie uzytkownik stoi.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    PlayFromQueue,
    PlayTrack,
    View,
)


def _rows() -> list[Row]:
    """Trzy pozycje kolejki. Kolejnosc B, A, C NIE jest alfabetyczna."""
    return [
        Row(item_id="q1", title="B utwor", kind="track", path=r"C:\muzyka\B.wav"),
        Row(item_id="q2", title="A utwor", kind="track", path=r"C:\muzyka\A.wav"),
        Row(item_id="q3", title="C utwor", kind="track", path=r"C:\muzyka\C.wav"),
    ]


def _navigator_in_queue_view() -> Navigator:
    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.SAVED_QUEUE
    state.model.replace(_rows())
    return navigator


def test_enter_w_kolejce_daje_zlecenie_kolejki_nie_pojedynczy_plik() -> None:
    """Enter z Ctrl+Q uruchamia KOLEJKE, nie samotne odtworzenie pliku."""
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(2)  # wiersz C, nie pierwszy

    effects = navigator.activate_selected()

    orders = [effect for effect in effects if isinstance(effect, PlayFromQueue)]
    assert len(orders) == 1, "powstaje dokladnie jedno zlecenie kolejki"
    order = orders[0]
    assert order.item_id == "q3", "kolejka startuje od WYBRANEGO wiersza"
    # Pozycje idą w kolejnosci WIDOKU, bo taka jest kolejnosc zapisanej kolejki.
    assert [row.item_id for row in order.rows] == ["q1", "q2", "q3"]
    assert not any(isinstance(effect, PlayTrack) for effect in effects), (
        "z widoku kolejki NIE wychodzi zwykle files.play: to zerwaloby nastepstwo"
    )


def test_enter_w_kolejce_przechodzi_do_odtwarzacza_i_zapamietuje_wiersz() -> None:
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(1)

    navigator.activate_selected()
    state = navigator.session

    assert state.view is View.PLAYER, "Enter przechodzi do odtwarzacza jak dotad"
    assert state.now_playing_id == "q2", "biezacy material to wybrany wiersz"
    assert state.list_anchor_id == "q2", (
        "powrot Escape ma wrocic na TEN wiersz, nie na poczatek listy"
    )


def test_naturalne_przejscie_aktualizuje_biezacy_material() -> None:
    """``queue.advanced`` z hosta zmienia BIEZACY material, nie zaznaczenie.

    Host policzyl przejscie i NAPRAWDE juz gra nastepna pozycje. Okno ma to
    odwzorowac, ale kursor uzytkownika na liscie zostaje tam, gdzie byl --
    inaczej fokus uciekalby sam z siebie w trakcie sluchania.
    """
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    navigator.activate_selected()
    anchor_before = navigator.session.list_anchor_id

    effects = navigator.note_queue_advanced("q2", "A utwor")
    state = navigator.session

    assert state.now_playing_id == "q2", "biezacy material to TO, co host zaczal grac"
    assert state.now_playing_title == "A utwor"
    assert state.list_anchor_id == anchor_before, (
        "zaznaczenie na liscie NIE idzie za audio: fokus nie moze uciekac sam"
    )
    assert any(isinstance(effect, Announce) for effect in effects), (
        "zmiane utworu trzeba powiedziec, inaczej uzytkownik jej nie zauwazy"
    )


def test_przejscie_bez_identyfikatora_nie_psuje_stanu() -> None:
    """Zdarzenie bez ``id`` nie moze wyczyscic biezacego materialu."""
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    navigator.activate_selected()

    navigator.note_queue_advanced("", "")

    assert navigator.session.now_playing_id == "q1", "stan zostaje nietkniety"


def test_przejscie_nie_przerzuca_widoku_gdy_uzytkownik_wrocil_na_liste() -> None:
    """Slucham kolejki, ale przegladam liste: przejscie NIE ma mnie porywac."""
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    navigator.activate_selected()
    navigator.back_to_list()

    navigator.note_queue_advanced("q2", "A utwor")

    assert navigator.session.view is View.LIST, (
        "przejscie utworu nie przerzuca widoku pod rekami uzytkownika"
    )


def _recording_frame():
    """Prawdziwa ``LiteFrame`` z podstawionym klientem, BEZ budowania okna.

    ``wx`` nie da sie tu zaimportowac, a i bez pulpitu okna nie ma. Metody
    wysylajace do hosta sa jednak zwyklymi metodami: wolamy je na obiekcie
    utworzonym przez ``__new__`` i wstawiamy tylko te atrybuty, ktorych dana
    metoda naprawde uzywa. Dzieki temu mierzymy PAYLOAD PRODUKCJI, a nie
    wlasna kopie slownika ani obecnosc napisu w zrodle.
    """
    from types import SimpleNamespace

    from amc_wx_lite import gui

    sent: list[dict] = []

    class Client:
        def configure_audio(self, **_settings):
            # Przed odtworzeniem produkcja przełącza bazowy tor sesji Pliki.
            # Ten test mierzy wyłącznie kontrakt kolejki, więc konfiguracja
            # dźwięku jest celowo wykonana, ale nie trafia do listy payloadów.
            return {"ok": True}

        def queue_set(self, items, *, session_id="local", order=None):
            sent.append({"items": items, "sessionId": session_id, "order": order})
            return {"ok": True}

        def queue_play_at(self, item_id, *, volume=None, rate=None):
            sent.append({"playAt": item_id, "volume": volume, "rate": rate})
            return {"ok": True}

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.client = Client()
    frame.options = SimpleNamespace(
        volume=35,
        rate=1.0,
        session="local",
        audio_payload=lambda: {
            "loudnessNormalization": False,
            "smoothTrackTransitions": False,
            "interTrackSilenceMs": 0,
            "tempoAlgorithm": 0,
        },
    )
    frame.state = SimpleNamespace(session_overrides={})
    frame.announcer = SimpleNamespace(say=lambda *a, **k: None)
    # Runner wykonuje zadanie NATYCHMIAST i w tym samym watku: chcemy zmierzyc
    # payload, a nie polityke watkow (ta ma swoje testy).
    frame.runner = SimpleNamespace(
        submit=lambda slot, work, done, failed: done(work())
    )
    frame._refresh_status = lambda *a, **k: None
    frame._run = lambda effects: None
    return frame, sent


def test_kolejka_wyslana_do_hosta_ma_flage_przynaleznosci() -> None:
    """Pozycja bez ``isInQueue`` zagra, ale NIE poprowadzi dalej.

    Host uznaje za kolejke tylko pozycje z ta flaga
    (``LiteQueueCoordinator.Set`` -> ``SynchronizeQueueOrder``). Brak flagi
    dawal w zywym oknie dokladnie jeden utwor bez naturalnego przejscia -- i
    zaden test tego nie widzial.

    Mierzymy FAKTYCZNY payload ``LiteFrame._play_from_queue``, nie zrodlo.
    """
    navigator = _navigator_in_queue_view()
    navigator.session.model.select_index(0)
    effects = navigator.activate_selected()
    order = next(e for e in effects if isinstance(e, PlayFromQueue))

    frame, sent = _recording_frame()
    frame._play_from_queue(order)

    assert len(sent) == 2, "queue.set wczytuje kolejnosc, queue.playAt startuje"
    assert sent[1] == {"playAt": "q1", "volume": 35, "rate": 1.0}, (
        "start od WYBRANEGO wiersza, nie od pierwszego"
    )
    assert sent[0]["order"] == ["q1", "q2", "q3"], "jawna kolejnosc widoku"
    items = sent[0]["items"]
    assert [i["id"] for i in items] == ["q1", "q2", "q3"], (
        "kolejnosc widoku (B, A, C) musi dojsc do hosta nieprzesortowana"
    )
    assert all(i["isInQueue"] for i in items), (
        "kazda pozycja musi byc oznaczona jako nalezaca do kolejki, "
        "inaczej host nie poprowadzi naturalnego przejscia"
    )
    assert all("path" in i and i["path"] for i in items), "host potrzebuje sciezki"


def test_flagi_pozycji_ida_z_odczytanego_wiersza_a_nie_z_wymuszenia() -> None:
    """``isPlayNext`` ma oddawac PRAWDE z ``QueueRow``, nie staly ``True``.

    Gole wymuszenie wszystkiego na ``True`` zrobiloby z kazdej pozycji
    "odtworz jako nastepna" i skasowalo rozroznienie, ktore host zna
    (``QueueRow.IsPlayNext``). Dlatego zlecenie wiezie flagi per wiersz.
    """
    navigator = _navigator_in_queue_view()
    state = navigator.session
    # Tak jak po odczycie z warstwy danych: q2 jest "zagraj jako nastepne".
    state.queue_flags = {"q1": (True, False), "q2": (True, True), "q3": (True, False)}
    state.model.select_index(0)
    effects = navigator.activate_selected()
    order = next(e for e in effects if isinstance(e, PlayFromQueue))

    frame, sent = _recording_frame()
    frame._play_from_queue(order)

    flags = {i["id"]: i["isPlayNext"] for i in sent[0]["items"]}
    assert flags == {"q1": False, "q2": True, "q3": False}, (
        "flagi musza pochodzic z odczytanego wiersza, nie z wymuszenia"
    )


def test_enter_w_zywej_kolejce_nie_wymaga_sciezki_w_payloadzie() -> None:
    from amc_wx_lite.navigation import PlayQueueAt
    from amc_wx_lite.list_model import rows_from_queue_status

    navigator = Navigator()
    rows = rows_from_queue_status({"rows": [{"id": "q-live", "title": "Utwór"}]})
    navigator.apply_live_queue(rows, current_id="q-live")
    effects = navigator.activate_selected()
    assert any(isinstance(x, PlayQueueAt) and x.item_id == "q-live" for x in effects), effects
    assert navigator.session.view is View.PLAYER


def test_zywy_widok_nie_odbudowuje_kolejki_hosta() -> None:
    """Enter w ZYWYM widoku startuje w miejscu, bez ``queue.set``.

    Ponowna wysylka wierszy ekranu nadpisalaby to, co sesja Core wie o
    pozycjach juz odegranych -- czyli zgubilaby postep kolejki.
    """
    from amc_wx_lite.navigation import PlayQueueAt

    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.LIVE_QUEUE
    state.model.replace(_rows())
    state.model.select_index(1)

    effects = navigator.activate_selected()
    intent = next(e for e in effects if isinstance(e, PlayQueueAt))
    assert intent.item_id == "q2"
    assert not any(isinstance(e, PlayFromQueue) for e in effects), (
        "zywy widok NIE wysyla kolejki ponownie"
    )

    frame, sent = _recording_frame()
    frame._play_queue_at(intent)
    assert sent == [{"playAt": "q2", "volume": 35, "rate": 1.0}]


def test_ctrl_q_czyta_zywa_kolejke_hosta() -> None:
    """Ctrl+Q w trakcie kolejki pokazuje stan HOSTA, nie zapisany porzadek.

    To jest brakujace kryterium: po ``next`` host ma juz inna liste, a widok
    musi ja oddac -- razem z etykieta bez slowa "zapisana".
    """
    from types import SimpleNamespace

    from amc_wx_lite import gui
    from amc_wx_lite.list_model import rows_from_queue_status

    payload = {
        "currentId": "q2",
        "rows": [
            {"id": "q2", "title": "A utwor", "current": True},
            {"id": "q3", "title": "C utwor", "playNext": False},
        ],
    }

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.client = SimpleNamespace(queue_status=lambda: payload)
    frame.runner = SimpleNamespace(submit=lambda slot, work, done, failed: done(work()))
    frame.announcer = SimpleNamespace(say=lambda *a, **k: None)
    navigator = Navigator()
    frame.navigator = navigator
    applied: list[list] = []
    frame._run = lambda effects: applied.append(effects)

    frame._open_queue_view()

    state = navigator.session
    assert state.library_view is LibraryView.LIVE_QUEUE, (
        "Ctrl+Q w trakcie kolejki NIE moze wracac do widoku zapisanego"
    )
    assert [r.item_id for r in state.model.rows] == ["q2", "q3"], (
        "skonsumowana pozycja q1 nie wraca do widoku"
    )
    said = " ".join(
        e.text for eff in applied for e in eff if isinstance(e, Announce)
    )
    assert "zapisana" not in said.lower(), f"etykieta klamie o zapisie: {said}"
    assert rows_from_queue_status(payload)[0].item_id == "q2"


def test_skrot_kolejki_pyta_host_a_nie_otwiera_zapisu_wprost() -> None:
    """Ctrl+Q musi iSC przez ``open_queue_view``, nie prosto w zapis.

    Bez tego testu cala zywa sciezka wisiala na metodzie, ktorej skrot wcale
    nie wola: zmierzone mutantem -- podmiana galezi ``VIEW_SAVED_QUEUE`` na
    ``open_library_view(SAVED_QUEUE)`` NIE zapalala zadnego testu.

    Czytamy galaz ``_dispatch`` ze zrodla, bo samego ``_dispatch`` nie da sie
    wywolac bez okna wx. To pomiar WIAZANIA skrotu, nie obecnosci napisu:
    sprawdzamy, ktora metoda nawigatora stoi pod ta konkretna akcja.
    """
    import ast
    import inspect
    from pathlib import Path

    from amc_wx_lite import gui

    source = Path(inspect.getsourcefile(gui)).read_text(encoding="utf-8")
    tree = ast.parse(source)

    found: list[str] = []
    for node in ast.walk(tree):
        # Szukamy galezi "action is Action.VIEW_SAVED_QUEUE" i tego, co w niej jest.
        if not isinstance(node, ast.If):
            continue
        test = node.test
        if not (
            isinstance(test, ast.Compare)
            and isinstance(test.comparators[0], ast.Attribute)
            and test.comparators[0].attr == "VIEW_SAVED_QUEUE"
        ):
            continue
        for call in ast.walk(ast.Module(body=node.body, type_ignores=[])):
            if isinstance(call, ast.Call) and isinstance(call.func, ast.Attribute):
                found.append(call.func.attr)

    assert found, "nie znalazlem galezi VIEW_SAVED_QUEUE w gui.py"
    assert "open_queue_view" in found, (
        f"Ctrl+Q nie pyta hosta o kolejke, wola: {found}"
    )
    assert "open_library_view" not in found, (
        "Ctrl+Q wciaz otwiera zapisany porzadek na skrot, bez pytania hosta"
    )


def test_kolejka_niewczytana_pozwala_odczytac_zapis() -> None:
    from amc_wx_lite.navigation import OpenLibraryView

    frame, captured = _frame_for_queue_status(
        {"initialized": False, "rows": [], "currentId": None}
    )
    frame._open_queue_view()
    opened = [e for e in captured if isinstance(e, OpenLibraryView)]
    assert opened and opened[0].view is LibraryView.SAVED_QUEUE


def _frame_for_queue_status(payload: dict):
    from types import SimpleNamespace
    from amc_wx_lite import gui

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.client = SimpleNamespace(queue_status=lambda: payload)
    frame.runner = SimpleNamespace(submit=lambda slot, work, done, failed: done(work()))
    frame.announcer = SimpleNamespace(say=lambda *a, **k: None)
    frame.navigator = Navigator()
    captured: list = []
    frame._run = lambda effects: captured.extend(effects)
    return frame, captured


def test_zuzyta_zywa_kolejka_nie_przywraca_starych_utworow_z_profilu() -> None:
    from amc_wx_lite.navigation import OpenLibraryView

    frame, captured = _frame_for_queue_status(
        {"initialized": True, "rows": [], "currentId": "q3"}
    )
    frame._open_queue_view()
    assert not any(isinstance(e, OpenLibraryView) for e in captured), (
        "pusta ZYWA kolejka nie moze przywracac skonsumowanych pozycji z zapisu"
    )
    assert frame.navigator.session.library_view is LibraryView.LIVE_QUEUE
    assert frame.navigator.session.model.rows == []
    assert any(isinstance(e, Announce) and "pusto" in e.text for e in captured)


def test_otwarta_zywa_lista_usuwa_zuzyte_wiersze_bez_ponownego_ctrl_q() -> None:
    frame, _ = _frame_for_queue_status({
        "initialized": True, "currentId": "q2",
        "rows": [{"id": "q2", "title": "A"}, {"id": "q3", "title": "C"}],
    })
    frame.navigator.apply_live_queue(_rows(), current_id="q3")
    frame._window_alive = lambda: True
    frame._sync_views = lambda: None

    frame._handle_engine_event("queue.advanced", {"id": "q2", "title": "A"})

    state = frame.navigator.session
    assert [r.item_id for r in state.model.rows] == ["q2", "q3"], (
        "widoczna kolejka nie moze zostawic zuzytego wiersza do nastepnego Ctrl+Q"
    )
    assert state.model.selected_id == "q3"
    assert state.view is View.LIST


def test_koniec_utworu_oproznia_otwarta_zywa_liste() -> None:
    frame, _ = _frame_for_queue_status({
        "initialized": True, "currentId": "q3", "rows": [],
    })
    frame.navigator.apply_live_queue(_rows(), current_id="q3")
    frame._window_alive = lambda: True
    frame._sync_views = lambda: None

    frame._handle_engine_event("playback.ended", {
        "engine": "files", "id": "q3", "queueContinues": False,
    })

    assert frame.navigator.session.model.rows == []
    assert frame.navigator.session.library_view is LibraryView.LIVE_QUEUE


def test_kolejka_aktualizuje_kontekst_pliku_takze_gdy_ogladam_radio() -> None:
    from amc_wx_lite.navigation import SessionId

    navigator = _navigator_in_queue_view()
    navigator.activate_selected()
    navigator.note_playback_started()
    navigator.switch_session(SessionId.RADIO)
    radio = navigator.session
    radio.now_playing_id = "station-x"

    navigator.note_queue_advanced("q2", "A")

    files = navigator.sessions[SessionId.FILES]
    assert files.now_playing_id == "q2"
    assert files.current_material_id == "q2", "zakladki maja kontekst faktycznie grajacego pliku"
    assert radio.now_playing_id == "station-x", "zdarzenie pliku nie podmienia stacji"
    assert navigator.active is SessionId.RADIO


def test_odswiezenie_kolejki_nie_nadpisuje_innego_widoku() -> None:
    from types import SimpleNamespace

    frame, _ = _frame_for_queue_status({"initialized": True, "rows": []})
    frame.navigator.apply_live_queue(_rows(), current_id="q3")
    callbacks = []
    frame.runner = SimpleNamespace(
        submit=lambda slot, work, done, failed: callbacks.append((work, done))
    )
    frame._sync_views = lambda: None
    frame._refresh_live_queue()
    frame.navigator.apply_folder("example", _rows())
    work, done = callbacks.pop()
    done(work())
    assert frame.navigator.session.library_view is None
    assert [r.item_id for r in frame.navigator.session.model.rows] == ["q1", "q2", "q3"]


def test_identyczna_kolejka_nie_wywoluje_synchronizacji_gui() -> None:
    from amc_wx_lite.list_model import rows_from_queue_status

    payload = {"initialized": True, "rows": [{"id": "q2", "title": "A"}]}
    frame, _ = _frame_for_queue_status(payload)
    frame.navigator.apply_live_queue(rows_from_queue_status(payload), current_id="q2")
    syncs = []
    frame._sync_views = lambda: syncs.append(True)
    frame._refresh_live_queue()
    assert syncs == []


def test_poza_kolejka_enter_dziala_jak_dotad() -> None:
    """Zwykly folder NIE zmienia zachowania: dalej pojedynczy files.play."""
    navigator = Navigator()
    navigator.session.model.replace(_rows())  # library_view pozostaje None

    effects = navigator.activate_selected()

    assert any(isinstance(effect, PlayTrack) for effect in effects)
    assert not any(isinstance(effect, PlayFromQueue) for effect in effects)


def test_wiersz_bez_sciezki_w_kolejce_nie_udaje_odtwarzania() -> None:
    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.SAVED_QUEUE
    state.model.replace([Row(item_id="q1", title="bez pliku", kind="track", path="")])

    effects = navigator.activate_selected()

    assert not any(isinstance(effect, PlayFromQueue) for effect in effects)
    assert state.view is not View.PLAYER, "odmowa zostawia uzytkownika na liscie"


def test_pozycje_bez_sciezki_nie_wchodza_do_zlecenia_kolejki() -> None:
    """Wiersz bez pliku nie moze trafic do kolejki hosta jako material."""
    navigator = Navigator()
    state = navigator.session
    state.library_view = LibraryView.SAVED_QUEUE
    state.model.replace(
        [
            Row(item_id="q1", title="B", kind="track", path=r"C:\muzyka\B.wav"),
            Row(item_id="q2", title="bez pliku", kind="track", path=""),
            Row(item_id="q3", title="C", kind="track", path=r"C:\muzyka\C.wav"),
        ]
    )
    state.model.select_index(0)

    effects = navigator.activate_selected()
    order = next(effect for effect in effects if isinstance(effect, PlayFromQueue))

    assert [row.item_id for row in order.rows] == ["q1", "q3"], (
        "pozycja bez sciezki wypada z kolejki, reszta zachowuje kolejnosc"
    )
