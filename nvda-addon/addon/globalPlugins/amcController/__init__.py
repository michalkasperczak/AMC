import globalPluginHandler
import globalVars
from scriptHandler import script
from winAPI.sessionTracking import isLockScreenModeActive
import ui
import wx

from .transport import BridgeError, exchange
from .worker import CommandWorker


class GlobalPlugin(globalPluginHandler.GlobalPlugin):
    scriptCategory = "AMC"

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._closed = False
        self._worker = CommandWorker(self._exchange, self._deliver)

    def _blocked(self):
        return self._closed or globalVars.appArgs.secure or isLockScreenModeActive()

    def _exchange(self, command):
        if self._blocked():
            return {"message": ""}
        return exchange(command)

    def _deliver(self, message):
        if isinstance(message, BridgeError):
            message = str(message)
        elif isinstance(message, Exception):
            message = "Dodatek nie mógł połączyć się z AMC."
        if message:
            wx.CallAfter(self._say, message)

    def _say(self, message):
        if not self._blocked():
            ui.message(message)

    def _send(self, command):
        if not self._blocked() and not self._worker.submit(command):
            ui.message("AMC: poczekaj na poprzednie polecenie.")

    def terminate(self):
        self._closed = True
        self._worker.close()
        super().terminate()

    # User-approved Ctrl+Win profile; do not rewrite NVDA's user gesture overrides.
    # Free Radio must be disabled or use different gestures if still installed.
    @script(description="Odczytaj bieżące nagranie, sesję i stan odtwarzania", gesture="kb:control+windows+i")
    def script_status(self, gesture):
        self._send("status")

    @script(description="Odtwórz lub wstrzymaj bieżące nagranie", gesture="kb:control+windows+p")
    def script_playPause(self, gesture):
        self._send("playPause")

    @script(description="Odtwórz poprzedni element w kontekście odtwarzania", gesture="kb:control+windows+leftArrow")
    def script_previous(self, gesture):
        self._send("previous")

    @script(description="Odtwórz następny element w kontekście odtwarzania", gesture="kb:control+windows+rightArrow")
    def script_next(self, gesture):
        self._send("next")

    @script(description="Zwiększ głośność bieżącej sesji o 5 procent", gesture="kb:control+windows+upArrow")
    def script_volumeUp(self, gesture):
        self._send("volumeUp")

    @script(description="Zmniejsz głośność bieżącej sesji o 5 procent", gesture="kb:control+windows+downArrow")
    def script_volumeDown(self, gesture):
        self._send("volumeDown")

    @script(description="Wycisz lub przywróć dźwięk bieżącej sesji", gesture="kb:control+windows+m")
    def script_mute(self, gesture):
        self._send("mute")

    @script(description="Cofnij odtwarzanie o 10 sekund", gesture="kb:control+windows+j")
    def script_seekBack(self, gesture):
        self._send("seekBack")

    @script(description="Przewiń odtwarzanie do przodu o 10 sekund", gesture="kb:control+windows+k")
    def script_seekForward(self, gesture):
        self._send("seekForward")

    @script(description="Odczytaj czas od początku", gesture="kb:control+windows+e")
    def script_elapsed(self, gesture):
        self._send("elapsed")

    @script(description="Odczytaj czas pozostały", gesture="kb:control+windows+r")
    def script_remaining(self, gesture):
        self._send("remaining")

    @script(description="Odczytaj całkowity czas nagrania", gesture="kb:control+windows+t")
    def script_total(self, gesture):
        self._send("total")

    @script(description="Przełącz AMC na poprzednią sesję", gesture="kb:control+windows+pageUp")
    def script_sessionPrevious(self, gesture):
        self._send("sessionPrevious")

    @script(description="Przełącz AMC na następną sesję", gesture="kb:control+windows+pageDown")
    def script_sessionNext(self, gesture):
        self._send("sessionNext")
