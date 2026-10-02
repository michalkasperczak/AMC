"""Testy bramy wynikow w tle: przeterminowane wyniki i zycie okna."""

from __future__ import annotations

import threading

from amc_wx_lite.async_gate import BackgroundRunner, StaleResultGate


def test_only_newest_request_in_a_stream_is_accepted() -> None:
    # Uzytkownik wchodzi do folderu A, potem szybko do B. Wynik A przychodzi
    # pozniej i NIE MOZE podmienic listy folderu B.
    gate = StaleResultGate()
    token_a = gate.begin("folder")
    token_b = gate.begin("folder")

    assert not gate.accept("folder", token_a), "stary wynik odrzucony"
    assert gate.accept("folder", token_b)
    assert gate.dropped_stale == 1


def test_streams_are_independent() -> None:
    gate = StaleResultGate()
    folder = gate.begin("folder")
    gate.begin("stations")
    assert gate.accept("folder", folder), "zadanie stacji nie uniewaznia skanu folderu"


def test_dead_window_rejects_everything() -> None:
    alive = True
    gate = StaleResultGate(alive=lambda: alive)
    token = gate.begin("folder")
    alive = False
    assert not gate.accept("folder", token)
    assert gate.dropped_dead_window == 1


def test_cancel_all_invalidates_pending_work() -> None:
    gate = StaleResultGate()
    token = gate.begin("folder")
    gate.cancel_all()
    assert not gate.accept("folder", token)


def test_runner_delivers_result_to_gui_callback() -> None:
    gate = StaleResultGate()
    runner = BackgroundRunner(gate)  # to_gui i spawn wykonuja od razu
    received: list[str] = []
    runner.submit("folder", lambda: "lista", received.append)
    assert received == ["lista"]


def test_runner_drops_result_of_superseded_request() -> None:
    gate = StaleResultGate()
    pending: list = []
    runner = BackgroundRunner(gate, to_gui=pending.append, spawn=lambda work: work())

    received: list[str] = []
    runner.submit("folder", lambda: "A", received.append)
    runner.submit("folder", lambda: "B", received.append)

    # Oba zadania skonczyly prace; teraz GUI je odbiera w swojej kolejnosci.
    for delivery in pending:
        delivery()

    assert received == ["B"], "wynik pierwszego zadania zostal odrzucony jako przeterminowany"


def test_runner_reports_errors_but_only_for_current_request() -> None:
    gate = StaleResultGate()
    pending: list = []
    runner = BackgroundRunner(gate, to_gui=pending.append, spawn=lambda work: work())
    errors: list[str] = []

    def boom() -> str:
        raise OSError("brak dostepu do folderu")

    runner.submit("folder", boom, lambda _: None, lambda e: errors.append(str(e)))
    runner.submit("folder", lambda: "B", lambda _: None)
    for delivery in pending:
        delivery()

    assert errors == [], "blad starego zadania nie krzyczy do uzytkownika"

    pending.clear()
    runner.submit("folder", boom, lambda _: None, lambda e: errors.append(str(e)))
    for delivery in pending:
        delivery()
    assert errors == ["brak dostepu do folderu"], "blad aktualnego zadania jest zglaszany"


def test_runner_never_calls_back_after_window_closed() -> None:
    alive = True
    gate = StaleResultGate(alive=lambda: alive)
    pending: list = []
    runner = BackgroundRunner(gate, to_gui=pending.append, spawn=lambda work: work())
    received: list[str] = []
    runner.submit("folder", lambda: "wynik", received.append)

    alive = False  # uzytkownik zamknal okno, zanim wynik dotarl do GUI
    for delivery in pending:
        delivery()

    assert received == [], "nie siegamy do zniszczonego okna"


def test_real_threads_do_not_corrupt_the_counter() -> None:
    gate = StaleResultGate()
    tokens: list[int] = []
    lock = threading.Lock()

    def hammer() -> None:
        for _ in range(200):
            token = gate.begin("folder")
            with lock:
                tokens.append(token)

    threads = [threading.Thread(target=hammer) for _ in range(4)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert len(set(tokens)) == 800, "licznik generacji jest bezpieczny watkowo"
