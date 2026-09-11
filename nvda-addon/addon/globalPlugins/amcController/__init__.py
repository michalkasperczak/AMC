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

    @script(description="Przełącz AMC na poprzednią sesję", gesture="kb:control+windows+shift+tab")
    def script_sessionPrevious(self, gesture):
        self._send("sessionPrevious")

    @script(description="Przełącz AMC na następną sesję", gesture="kb:control+windows+tab")
    def script_sessionNext(self, gesture):
        self._send("sessionNext")

    @script(description="Odczytaj listę używaną przez następny i poprzedni element", gesture="kb:control+windows+alt+i")
    def script_context(self, gesture):
        self._send("context")

    @script(description="Wycisz lub przywróć dźwięk wszystkich sesji AMC", gesture="kb:control+windows+shift+m")
    def script_muteAll(self, gesture):
        self._send("muteAll")

    @script(description="Cofnij odtwarzanie o 30 sekund", gesture="kb:control+windows+shift+j")
    def script_seekBack30(self, gesture):
        self._send("seekBack30")

    @script(description="Przewiń odtwarzanie o 30 sekund", gesture="kb:control+windows+shift+k")
    def script_seekForward30(self, gesture):
        self._send("seekForward30")

    @script(description="Cofnij odtwarzanie o minutę", gesture="kb:control+windows+alt+j")
    def script_seekBack60(self, gesture):
        self._send("seekBack60")

    @script(description="Przewiń odtwarzanie o minutę", gesture="kb:control+windows+alt+k")
    def script_seekForward60(self, gesture):
        self._send("seekForward60")

    @script(description="Zmniejsz prędkość odtwarzania bez zmiany wysokości dźwięku", gesture="kb:control+windows+,")
    def script_rateDown(self, gesture):
        self._send("rateDown")

    @script(description="Zwiększ prędkość odtwarzania bez zmiany wysokości dźwięku", gesture="kb:control+windows+.")
    def script_rateUp(self, gesture):
        self._send("rateUp")

    @script(description="Przywróć normalną prędkość odtwarzania", gesture="kb:control+windows+shift+.")
    def script_rateReset(self, gesture):
        self._send("rateReset")

    @script(description="Przejdź do początku nagrania lub bufora radia", gesture="kb:control+windows+home")
    def script_trackStart(self, gesture):
        self._send("trackStart")

    @script(description="Przejdź do końca nagrania lub radia na żywo", gesture="kb:control+windows+end")
    def script_trackEnd(self, gesture):
        self._send("trackEnd")

    @script(description="Dodaj zakładkę w bieżącym nagraniu", gesture="kb:control+windows+b")
    def script_addBookmark(self, gesture):
        self._send("addBookmark")

    @script(description="Przejdź do poprzedniej zakładki bieżącego nagrania", gesture="kb:control+windows+shift+pageUp")
    def script_previousBookmark(self, gesture):
        self._send("previousBookmark")

    @script(description="Przejdź do następnej zakładki bieżącego nagrania", gesture="kb:control+windows+shift+pageDown")
    def script_nextBookmark(self, gesture):
        self._send("nextBookmark")

    @script(description="Przejdź do poprzedniego rozdziału bieżącego nagrania", gesture="kb:control+windows+alt+shift+pageUp")
    def script_previousChapter(self, gesture):
        self._send("previousChapter")

    @script(description="Przejdź do następnego rozdziału bieżącego nagrania", gesture="kb:control+windows+alt+shift+pageDown")
    def script_nextChapter(self, gesture):
        self._send("nextChapter")

    @script(description="Odtwórz poprzedni preset; w WiiM preset lub zapisany strumień", gesture="kb:control+windows+alt+pageUp")
    def script_presetPrevious(self, gesture):
        self._send("presetPrevious")

    @script(description="Odtwórz następny preset; w WiiM preset lub zapisany strumień", gesture="kb:control+windows+alt+pageDown")
    def script_presetNext(self, gesture):
        self._send("presetNext")

    @script(description="Dodaj lub usuń bieżące nagranie z ulubionych", gesture="kb:control+windows+shift+u")
    def script_favorite(self, gesture):
        self._send("favorite")

    @script(description="Dodaj lub usuń bieżące nagranie z kolejki", gesture="kb:control+windows+shift+q")
    def script_queue(self, gesture):
        self._send("queue")

    @script(description="Rozpocznij lub zakończ nagrywanie bieżącej stacji", gesture="kb:control+windows+alt+r")
    def script_recordToggle(self, gesture):
        self._send("recordToggle")

    @script(description="Wstrzymaj lub wznów nagrywanie bieżącej stacji bez zmiany odsłuchu", gesture="kb:control+windows+shift+r")
    def script_recordPause(self, gesture):
        self._send("recordPause")

    @script(description="Kontynuuj nagrywanie bieżącej stacji w nowym pliku", gesture="kb:control+windows+shift+t")
    def script_recordSplit(self, gesture):
        self._send("recordSplit")

    @script(description="Otwórz okno AMC z bieżącym odtwarzaczem", gesture="kb:control+windows+f6")
    def script_showPlayer(self, gesture):
        self._send("showPlayer")

    @script(description="Otwórz bibliotekę bieżącej sesji w oknie AMC", gesture="kb:control+windows+l")
    def script_showLibrary(self, gesture):
        self._send("showLibrary")

    @script(description="Otwórz ulubione bieżącej sesji w oknie AMC", gesture="kb:control+windows+u")
    def script_showFavorites(self, gesture):
        self._send("showFavorites")

    @script(description="Otwórz kolejkę bieżącej sesji w oknie AMC", gesture="kb:control+windows+alt+q")
    def script_showQueue(self, gesture):
        self._send("showQueue")

    @script(description="Otwórz playlisty bieżącej sesji w oknie AMC", gesture="kb:control+windows+shift+p")
    def script_showPlaylists(self, gesture):
        self._send("showPlaylists")

    @script(description="Otwórz historię odtwarzania bieżącej sesji w oknie AMC", gesture="kb:control+windows+h")
    def script_showHistory(self, gesture):
        self._send("showHistory")

    @script(description="Otwórz presety bieżącej sesji w oknie AMC", gesture="kb:control+windows+alt+p")
    def script_showPresets(self, gesture):
        self._send("showPresets")

    @script(description="Otwórz listę zakładek w oknie AMC", gesture="kb:control+windows+alt+b")
    def script_showBookmarks(self, gesture):
        self._send("showBookmarks")

    @script(description="Otwórz listę rozdziałów w oknie AMC", gesture="kb:control+windows+alt+c")
    def script_showChapters(self, gesture):
        self._send("showChapters")

    @script(description="Otwórz wybór sesji w oknie AMC", gesture="kb:control+windows+shift+s")
    def script_showSessions(self, gesture):
        self._send("showSessions")

    @script(description="Otwórz wybór urządzenia audio bieżącej sesji w oknie AMC", gesture="kb:control+windows+a")
    def script_showAudioOutput(self, gesture):
        self._send("showAudioOutput")

    @script(description="Otwórz wyszukiwanie bieżącej sesji w oknie AMC", gesture="kb:control+windows+alt+f")
    def script_showSearch(self, gesture):
        self._send("showSearch")

    @script(description="Otwórz paletę poleceń w oknie AMC", gesture="kb:control+windows+f2")
    def script_showCommands(self, gesture):
        self._send("showCommands")

    @script(description="Otwórz listę nagrywanych stacji w oknie AMC", gesture="kb:control+windows+alt+h")
    def script_showRecordings(self, gesture):
        self._send("showRecordings")

    @script(description="Otwórz harmonogramy nagrywania w oknie AMC", gesture="kb:control+windows+shift+h")
    def script_showSchedules(self, gesture):
        self._send("showSchedules")

    @script(description="Otwórz listę rozpoznanych utworów w oknie AMC", gesture="kb:control+windows+alt+s")
    def script_showRecognitions(self, gesture):
        self._send("showRecognitions")

    @script(description="Uruchom preset 1 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+1")
    def script_preset1(self, gesture):
        self._send("preset1")

    @script(description="Uruchom preset 2 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+2")
    def script_preset2(self, gesture):
        self._send("preset2")

    @script(description="Uruchom preset 3 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+3")
    def script_preset3(self, gesture):
        self._send("preset3")

    @script(description="Uruchom preset 4 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+4")
    def script_preset4(self, gesture):
        self._send("preset4")

    @script(description="Uruchom preset 5 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+5")
    def script_preset5(self, gesture):
        self._send("preset5")

    @script(description="Uruchom preset 6 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+6")
    def script_preset6(self, gesture):
        self._send("preset6")

    @script(description="Uruchom preset 7 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+7")
    def script_preset7(self, gesture):
        self._send("preset7")

    @script(description="Uruchom preset 8 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+8")
    def script_preset8(self, gesture):
        self._send("preset8")

    @script(description="Uruchom preset 9 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+9")
    def script_preset9(self, gesture):
        self._send("preset9")

    @script(description="Uruchom preset 10 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+0")
    def script_preset10(self, gesture):
        self._send("preset10")

    @script(description="Uruchom preset 11 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+-")
    def script_preset11(self, gesture):
        self._send("preset11")

    @script(description="Uruchom preset 12 bieżącej sesji; nie zmienia przypisania", gesture="kb:control+windows+alt+=")
    def script_preset12(self, gesture):
        self._send("preset12")
