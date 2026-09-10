"""A single bounded worker, never NVDA's speech / keyboard thread."""
import queue
import threading
import time


class CommandWorker:
    def __init__(self, exchange, deliver):
        self._exchange = exchange
        self._deliver = deliver
        self._queue = queue.Queue(maxsize=4)
        self._closed = threading.Event()
        self._thread = threading.Thread(target=self._run, name="AMC controller", daemon=True)
        self._thread.start()

    def submit(self, command):
        if self._closed.is_set():
            return False
        try:
            self._queue.put_nowait((command, time.monotonic()))
            return True
        except queue.Full:
            return False

    def close(self):
        # Reloading plugins or exiting NVDA must never wait for AMC.
        self._closed.set()

    def _run(self):
        while not self._closed.is_set():
            try:
                command, created = self._queue.get(timeout=0.1)
            except queue.Empty:
                continue
            if self._closed.is_set():
                break
            if time.monotonic() - created > 1.5:
                self._deliver("Pominięto spóźnione polecenie AMC.")
                continue
            try:
                response = self._exchange(command)
                message = response["message"]
            except Exception as error:
                # Only intentional BridgeError labels may be spoken by the adapter.
                message = error
            if not self._closed.is_set():
                self._deliver(message)
