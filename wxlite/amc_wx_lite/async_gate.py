"""Praca w tle bez zamrozenia okna i bez PRZETERMINOWANYCH wynikow.

Problem, ktory to rozwiazuje: skan folderu albo odpowiedz hosta przychodzi po
sekundzie. W tym czasie uzytkownik moze wejsc do innego folderu, zmienic sesje
albo zamknac okno. Spozniony wynik NIE MOZE wtedy podmienic listy ani siegnac
do zniszczonego okna.

Mechanizm: kazde zadanie dostaje numer generacji. Wynik przyjmujemy tylko wtedy,
gdy jego generacja jest nadal aktualna, a okno zyje.
"""

from __future__ import annotations

import threading
from dataclasses import dataclass, field
from typing import Any, Callable


@dataclass(slots=True)
class Generation:
    """Licznik generacji dla JEDNEGO strumienia zadan (np. skan folderu).

    ``bump`` uniewaznia wszystko, co jest w drodze.
    """

    value: int = 0
    _lock: threading.Lock = field(default_factory=threading.Lock, repr=False)

    def bump(self) -> int:
        with self._lock:
            self.value += 1
            return self.value

    def current(self) -> int:
        with self._lock:
            return self.value

    def is_current(self, token: int) -> bool:
        with self._lock:
            return token == self.value


class StaleResultGate:
    """Brama wynikow pracy w tle.

    ``alive`` to funkcja mowiaca, czy okno nadal istnieje (w wx:
    ``lambda: bool(self) and not self.IsBeingDeleted()``). Tutaj jest wstrzykiwana,
    zeby cala brama dala sie przetestowac bez pulpitu.
    """

    def __init__(self, alive: Callable[[], bool] | None = None) -> None:
        self._generations: dict[str, Generation] = {}
        self._alive = alive or (lambda: True)
        self._lock = threading.Lock()
        self.dropped_stale = 0
        self.dropped_dead_window = 0

    def _generation(self, stream: str) -> Generation:
        with self._lock:
            generation = self._generations.get(stream)
            if generation is None:
                generation = Generation()
                self._generations[stream] = generation
            return generation

    def begin(self, stream: str) -> int:
        """Zglos NOWE zadanie w strumieniu. Zwraca token do oddania przy wyniku."""
        return self._generation(stream).bump()

    def accept(self, stream: str, token: int) -> bool:
        """Czy wynik z tym tokenem wolno jeszcze zastosowac."""
        if not self._alive():
            self.dropped_dead_window += 1
            return False
        if not self._generation(stream).is_current(token):
            self.dropped_stale += 1
            return False
        return True

    def cancel_all(self) -> None:
        """Zamykanie okna: uniewaznij wszystko, co jest w drodze."""
        with self._lock:
            streams = list(self._generations.values())
        for generation in streams:
            generation.bump()


class BackgroundRunner:
    """Uruchamia prace w WATKU, a skutek oddaje do watku GUI.

    ``to_gui`` w aplikacji to ``wx.CallAfter``. W testach podajemy funkcje
    wykonujaca od razu, wiec mierzymy LOGIKE, nie petle zdarzen wx.
    """

    def __init__(
        self,
        gate: StaleResultGate,
        to_gui: Callable[[Callable[..., Any]], None] | None = None,
        spawn: Callable[[Callable[[], None]], None] | None = None,
    ) -> None:
        self._gate = gate
        self._to_gui = to_gui or (lambda fn: fn())
        self._spawn = spawn or self._default_spawn

    @staticmethod
    def _default_spawn(work: Callable[[], None]) -> None:
        thread = threading.Thread(target=work, daemon=True)
        thread.start()

    def submit(
        self,
        stream: str,
        work: Callable[[], Any],
        on_success: Callable[[Any], None],
        on_error: Callable[[Exception], None] | None = None,
    ) -> int:
        """Wykonaj ``work`` poza GUI; ``on_success`` dostanie wynik W GUI."""
        token = self._gate.begin(stream)

        def run() -> None:
            try:
                result = work()
            except Exception as caught:  # noqa: BLE001 - blad ma dojsc do okna
                handler = on_error
                if handler is None:
                    return
                # Python USUWA zmienna wyjatku na koncu bloku except, wiec
                # lambda odpalona pozniej w watku GUI nie znalazlaby jej.
                # Zapisujemy ja do wlasnej zmiennej, zanim blok sie skonczy.
                failure = caught
                self._to_gui(lambda: self._deliver_error(stream, token, failure, handler))
                return
            self._to_gui(lambda: self._deliver(stream, token, result, on_success))

        self._spawn(run)
        return token

    def _deliver(self, stream: str, token: int, result: Any, on_success: Callable[[Any], None]) -> None:
        if self._gate.accept(stream, token):
            on_success(result)

    def _deliver_error(
        self,
        stream: str,
        token: int,
        error: Exception,
        on_error: Callable[[Exception], None],
    ) -> None:
        if self._gate.accept(stream, token):
            on_error(error)
