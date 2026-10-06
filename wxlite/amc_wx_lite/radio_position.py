"""Znacznik HWND listy radiowej dla nakladki NVDA. Bez wx, bez czytnika.

PO CO TO ISTNIEJE
-----------------
Licznik "1 z 37" na liscie stacji nie pochodzi od nas. Mowi go NVDA z
WLASNEGO zrodla: ``NVDAObjects/IAccessible/sysListView32.py:463-466`` liczy
``similarItemsInGroup`` przez ``LVM_GETITEMCOUNT``, a ``speech/speech.py``
(756-762, 2174-2182) zamienia to w wypowiedz. Dlatego wyciszenie NASZYCH
komunikatow nie zmienilo by niczego -- zmiana musi dojsc do tego slownika.

Zmienia go nakladka obiektu NVDA (``nvda-addon/.../radioList.py``). Zadaniem
TEGO modulu jest tylko jedno: oznaczyc nasza liste radiowa tak, zeby
nakladka trafila WYLACZNIE w nia.

CZEMU NAZWANA WLASCIWOSC OKNA, A NIE COS INNEGO
-----------------------------------------------
Lista wlasciwosci okna wisi przy HWND w ``user32``, nie w pamieci procesu,
wiec ``GetPropW`` z procesu NVDA widzi to, co ``SetPropW`` zapisal z naszego
(dokumentacja SetPropW: UIPI przepuszcza zapis do procesu o rownym lub
nizszym poziomie integralnosci; odmowa to GetLastError 5, ktora tu po prostu
oznacza "nie udalo sie" -- bez wyjatku na zewnatrz).

Czego NIE robimy: zadnego appModule, zadnej zmiany globalnej konfiguracji
NVDA ani ustawien mowy, zadnego przechwytywania klawiszy, zadnego serwera
ani handshake'u. Oznaczenie jest jednostronne i pasywne.

DWA ZNACZNIKI, NIE JEDEN
------------------------
``NVDAObjects/__init__.py:110-131`` dobiera nakladke RAZ -- przy tworzeniu
obiektu. Gdyby jeden znacznik mowil naraz "to lista radia" i "ukrywaj", to
przy wlaczonej opcji nakladka wcale by sie nie dolaczyla, a po pozniejszym
wylaczeniu opcji nie byloby czego przelaczac bez ponownego wejscia na liste.
Dlatego:

* ``PRESENCE_PROP`` -- "to jest lista radiowa AMC". Wisi, dopoki lista jest
  listą radiową, niezaleznie od opcji. Po nim nakladka sie DOLACZA.
* ``MODE_PROP`` -- "teraz ukrywaj pozycje". Nakladka czyta go przy KAZDYM
  odczycie pozycji, wiec przelacznik dziala bez ponownego ustawiania fokusu.
"""

from __future__ import annotations

import os
from collections.abc import Callable
from pathlib import Path

#: Prefiks naszych wlasnych nazw. Wlasciwosci okna sa wspolna przestrzenia
#: nazw dla calego HWND (subclassing Windows trzyma tam ``UxSubclassInfo``),
#: wiec nazwa musi byc jednoznacznie nasza.
PRESENCE_PROP = "AMC.wx.RadioList.v1"
MODE_PROP = "AMC.wx.RadioList.HidePosition.v1"

#: ``GetPropW`` oddaje 0 dla braku wpisu, wiec wartosc obecnosci musi byc
#: rozna od zera. Sama wartosc nie niesie tresci -- liczy sie obecnosc.
FLAG_ON = 1

#: Plik nakladki w dodatku NVDA. Jego brak = dodatek nie umie ukrywac.
OVERLAY_MODULE = "radioList.py"


SetProp = Callable[[int, str, int], bool]
RemoveProp = Callable[[int, str], bool]


def _default_user32() -> tuple[SetProp, RemoveProp]:
    """Prawdziwe ``user32`` na Windows; na innym systemie jawna odmowa.

    Brak Windows nie jest bledem do zgloszenia uzytkownikowi -- port dziala
    na Windows, a testy maja isc wszedzie. Dlatego zwracamy funkcje, ktore
    uczciwie mowia ``False``, zamiast udawac sukces.
    """
    try:
        import ctypes
        from ctypes import wintypes
    except ImportError:  # pragma: no cover - ctypes jest w stdlib
        return (lambda *_a: False, lambda *_a: False)
    if os.name != "nt":
        return (lambda *_a: False, lambda *_a: False)

    user = ctypes.WinDLL("user32", use_last_error=True)  # type: ignore[attr-defined]
    user.SetPropW.argtypes = [wintypes.HWND, wintypes.LPCWSTR, wintypes.HANDLE]
    user.SetPropW.restype = wintypes.BOOL
    user.RemovePropW.argtypes = [wintypes.HWND, wintypes.LPCWSTR]
    user.RemovePropW.restype = wintypes.HANDLE

    def set_prop(hwnd: int, name: str, value: int) -> bool:
        return bool(user.SetPropW(hwnd, name, value))

    def remove_prop(hwnd: int, name: str) -> bool:
        user.RemovePropW(hwnd, name)
        return True

    return set_prop, remove_prop


class WindowMarkers:
    """Trzyma znaczniki w zgodzie ze stanem widoku. Brak zmian = zero wywolan.

    ``apply`` wola wspolny przyrostowy sync po KAZDYM przebiegu, takze po
    samym ticku statusu. Dlatego pamietamy stan zalozony i nie powtarzamy
    wywolan ``user32`` bez powodu -- ta sama zasada, co w ``sync_rows``.
    """

    def __init__(
        self,
        set_prop: SetProp | None = None,
        remove_prop: RemoveProp | None = None,
    ) -> None:
        if set_prop is None or remove_prop is None:
            default_set, default_remove = _default_user32()
            set_prop = set_prop or default_set
            remove_prop = remove_prop or default_remove
        self._set = set_prop
        self._remove = remove_prop
        #: Co NAPRAWDE udalo sie zalozyc, per HWND: zbior nazw wlasciwosci.
        #: Nieudany zapis tu nie trafia, wiec bramka idempotencji nie zablokuje
        #: ponownej proby.
        self._applied: dict[int, set[str]] = {}

    def apply(self, hwnd: int, *, is_radio_list: bool, hide_position: bool) -> bool:
        """Doprowadz znaczniki okna do zadanego stanu.

        Zwraca ``True``, gdy stan okna zgadza sie z zadanym (takze gdy nic
        nie trzeba bylo robic). ``False`` znaczy "nie udalo sie" -- nigdy nie
        udaje sukcesu i nigdy nie wypuszcza wyjatku do okna.
        """
        if not hwnd:
            return False
        wanted: set[str] = set()
        if is_radio_list:
            wanted.add(PRESENCE_PROP)
            if hide_position:
                wanted.add(MODE_PROP)
        have = self._applied.get(hwnd, set())
        if have == wanted:
            return True
        ok = True
        for name in sorted(have - wanted):
            if self._drop(hwnd, name):
                have.discard(name)
            else:
                ok = False
        for name in sorted(wanted - have):
            try:
                written = bool(self._set(hwnd, name, FLAG_ON))
            except (OSError, ValueError, TypeError):
                written = False
            if written:
                have.add(name)
            else:
                ok = False
        if have:
            self._applied[hwnd] = have
        else:
            self._applied.pop(hwnd, None)
        return ok and have == wanted

    def forget(self, hwnd: int) -> None:
        """Zdejmij WSZYSTKIE nasze wpisy. Dla zamkniecia okna.

        Dokumentacja ``SetPropW`` zada, by aplikacja usunela swoje wpisy
        przed zniszczeniem okna (przed powrotem z ``WM_NCDESTROY``).
        Wolamy to bezwarunkowo dla obu nazw: po restarcie procesu pamiec
        ``_applied`` jest pusta, a wpis przy HWND moglby zostac.
        """
        if not hwnd:
            return
        for name in (PRESENCE_PROP, MODE_PROP):
            self._drop(hwnd, name)
        self._applied.pop(hwnd, None)

    def _drop(self, hwnd: int, name: str) -> bool:
        try:
            self._remove(hwnd, name)
        except (OSError, ValueError, TypeError):
            return False
        return True


# ------------------------------------------- uczciwosc wobec braku dodatku


def default_addons_root() -> Path | None:
    """Katalog dodatkow NVDA biezacego uzytkownika, jesli da sie ustalic."""
    appdata = os.environ.get("APPDATA")
    return Path(appdata) / "nvda" if appdata else None


def overlay_addon_installed(root: Path | None = None) -> bool:
    """Czy zainstalowany dodatek AMC MA plik nakladki pozycji.

    Samo "dodatek jest" nie wystarcza: dodatek w wersji bez ``radioList.py``
    nie potrafi ukryc licznika, a przelacznik udajacy skutek bylby gorszy od
    braku przelacznika. Sprawdzamy ISTNIEJACA mozliwosc wykrycia -- obecnosc
    pliku na dysku. Zadnego pytania do czytnika, zadnego handshake'u.

    To wykrycie jest POSZLAKA, nie dowodem: plik moze lezec, a dodatek byc
    wylaczony w NVDA. Dlatego komunikat ostrzega, ale nigdy nie zapewnia.
    """
    if root is None:
        root = default_addons_root()
    if root is None:
        return False
    try:
        return any(
            (candidate / OVERLAY_MODULE).is_file()
            for candidate in Path(root).glob("addons/*/globalPlugins/amcController")
        )
    except OSError:
        return False


def announcement(*, hide_position: bool, overlay_available: bool) -> str:
    """Zapowiedz zmiany przelacznika -- bez obietnicy, ktorej nie dowiezie.

    Wlaczony licznik jest NATYWNY (NVDA liczy go sam), wiec tam dodatek nie
    jest do niczego potrzebny i ostrzezenie byloby nieprawda.
    """
    if not hide_position:
        return "Odczyt pozycji stacji włączony."
    if overlay_available:
        return "Odczyt pozycji stacji wyłączony."
    return (
        "Odczyt pozycji stacji wyłączony w AMC, ale nie znalazłem dodatku NVDA, "
        "który to wykonuje. Licznik może być nadal czytany."
    )
