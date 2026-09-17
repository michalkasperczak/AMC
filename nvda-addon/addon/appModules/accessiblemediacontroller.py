"""Modul aplikacji dla AMC: gesty dzialajace TYLKO w okien AMC.

Insert plus strzalka w gore ma czytac zrodlo i utwor - w sesji Radio
internetowe tak samo, jak robi to sesja urzadzenia WiiM. To jest gest samego
NVDA (czytanie biezacej linii), wiec NIE WOLNO go
przypisywac w globalPlugin: nadpisalby czytnik w kazdym programie. Modul
aplikacji obowiazuje wylacznie wtedy, gdy fokus jest w AMC; wszedzie indziej
NVDA zachowuje sie bez zmian.

Skrot jest tez wystawiony jako zwykle zdarzenie wejscia w kategorii AMC,
wiec uzytkownik moze go zmienic w Ustawieniach NVDA.
"""
import threading

import appModuleHandler
import ui
import wx
from scriptHandler import script

try:
    # Normalna sciezka: NVDA laczy katalog globalPlugins dodatku
    # z pakietem globalPlugins czytnika.
    from globalPlugins.amcController.transport import BridgeError, exchange
except ImportError:  # pragma: no cover - zalezne od ukladu instalacji dodatku
    # Zapas: wczytaj transport wprost z pliku obok, zeby skrot nie zniknal
    # po cichu, gdyby uklad pakietow w danej wersji NVDA byl inny.
    import importlib.util
    import os

    _sciezka = os.path.join(
        os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        "globalPlugins", "amcController", "transport.py")
    _spec = importlib.util.spec_from_file_location("amcControllerTransport", _sciezka)
    _modul = importlib.util.module_from_spec(_spec)
    _spec.loader.exec_module(_modul)
    BridgeError = _modul.BridgeError
    exchange = _modul.exchange


class AppModule(appModuleHandler.AppModule):
    scriptCategory = "AMC"

    def _powiedz(self, tekst):
        if tekst:
            wx.CallAfter(ui.message, tekst)

    def _zapytaj(self):
        # Nigdy nie blokuj watku klawiatury NVDA na nazwanym potoku.
        try:
            odpowiedz = exchange("nowPlaying")
            self._powiedz(odpowiedz.get("message", ""))
        except BridgeError as blad:
            self._powiedz(str(blad))
        except Exception:
            self._powiedz("Dodatek nie mógł połączyć się z AMC.")

    @script(
        description="Odczytaj źródło i utwór, który teraz leci",
        gesture="kb:insert+upArrow",
    )
    def script_amcNowPlaying(self, gesture):
        threading.Thread(target=self._zapytaj, name="AMC now playing", daemon=True).start()
