"""A single bounded worker, never NVDA's speech / keyboard thread."""
import queue
import threading
import time

# Polecenia, ktore uzytkownik trzyma wcisniete i ktore SUMUJA sie w skutku.
# Nie wolno ich gubic (5 x glosniej to +25%, nie +5%), ale nie wolno tez
# zapychac nimi kolejki: przy autopowtarzaniu klawisza czterowpisowa kolejka
# pekala po ulamku sekundy i zamiast stanu glosnosci uzytkownik slyszal
# "poczekaj na poprzednie polecenie".  Dlatego kolejne wystapienie TEGO SAMEGO
# polecenia zwieksza krotnosc wpisu, ktory jeszcze czeka, a worker wykonuje je
# po kolei i wypowiada tylko OSTATNIA odpowiedz.
REPEATABLE = frozenset((
    "volumeUp", "volumeDown",
    "seekBack", "seekForward",
    "seekBack30", "seekForward30",
    "seekBack60", "seekForward60",
    "rateDown", "rateUp",
))

# Ile powtorzen jednego polecenia wolno zebrac.  Bez gornej granicy trzymany
# klawisz zbudowalby kolejke na kilkaset wymian i AMC odpowiadalby jeszcze
# dlugo po puszczeniu klawisza.
MAX_REPEAT = 24


class _Pending:
    __slots__ = ("command", "created", "count", "cancelled")

    def __init__(self, command, created):
        self.command = command
        self.created = created
        self.count = 1
        self.cancelled = False


class CommandWorker:
    def __init__(self, exchange, deliver):
        self._exchange = exchange
        self._deliver = deliver
        self._queue = queue.Queue(maxsize=4)
        self._closed = threading.Event()
        self._gate = threading.Lock()
        self._repeatable = {}
        self._thread = threading.Thread(target=self._run, name="AMC controller", daemon=True)
        self._thread.start()

    def submit(self, command):
        if self._closed.is_set():
            return False
        now = time.monotonic()
        if command in REPEATABLE:
            with self._gate:
                waiting = self._repeatable.get(command)
                if waiting is not None and not waiting.cancelled and waiting.count < MAX_REPEAT:
                    # Krotnosc rosnie, kolejka sie NIE wydluza.
                    waiting.count += 1
                    waiting.created = now
                    return True
        entry = _Pending(command, now)
        try:
            self._queue.put_nowait(entry)
        except queue.Full:
            return False
        if command in REPEATABLE:
            with self._gate:
                self._repeatable[command] = entry
        return True

    def close(self):
        # Reloading plugins or exiting NVDA must never wait for AMC.
        self._closed.set()

    def _take(self, entry):
        """Odbierz wpis z kolejki i domknij go dla scalania."""
        with self._gate:
            if self._repeatable.get(entry.command) is entry:
                del self._repeatable[entry.command]
            entry.cancelled = True
            return entry.count

    def _run(self):
        while not self._closed.is_set():
            try:
                entry = self._queue.get(timeout=0.1)
            except queue.Empty:
                continue
            if self._closed.is_set():
                break
            stale = time.monotonic() - entry.created > 1.5
            repeats = self._take(entry)
            if stale:
                self._deliver("Pominięto spóźnione polecenie AMC.")
                continue
            message = None
            for _ in range(repeats):
                if self._closed.is_set():
                    return
                try:
                    response = self._exchange(entry.command)
                    message = response["message"]
                except Exception as error:
                    # Only intentional BridgeError labels may be spoken by the adapter.
                    message = error
                    break
            if not self._closed.is_set():
                # Powtorzenia wypowiadamy RAZ - koncowym stanem, nie kazdym krokiem.
                self._deliver(message)
