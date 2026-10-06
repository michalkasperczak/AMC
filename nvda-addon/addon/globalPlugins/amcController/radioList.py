"""Nakladka: ukrywa POZYCJE wiersza wylacznie w oznaczonej liscie AMC.

DLACZEGO TO JEST JEDYNE MIEJSCE, W KTORYM TO DZIALA
---------------------------------------------------
Licznik "1 z 37" liczy sam NVDA, w swoim kodzie:

    # NVDAObjects/IAccessible/sysListView32.py:463-466
    def _get_positionInfo(self):
        index = self.IAccessibleChildID
        totalCount = watchdog.cancellableSendMessage(self.windowHandle, LVM_GETITEMCOUNT, 0, 0)
        return dict(indexInGroup=index, similarItemsInGroup=totalCount)

a mowa bierze z tego slownika klucze ``indexInGroup`` i
``similarItemsInGroup`` TYLKO gdy sa obecne (``speech/speech.py:756-762``).
Pusty slownik jest w tym API legalna odpowiedzia -- tak wyglada BAZOWA
implementacja ``NVDAObject._get_positionInfo`` (``NVDAObjects/__init__.py``
1108-1112). Dlatego ukrycie pozycji to zwrot ``{}``, a nie przechwyt mowy.

Nakladke dobiera ISTNIEJACY globalPlugin: ``NVDAObjects/__init__.py:125-131``
wola ``chooseNVDAObjectOverlayClasses`` kazdego uruchomionego pluginu.
appModule NIE jest do tego potrzebny i nadal go nie ma.

ZAKRES: dokladnie wiersz listy, ktorej HWND nosi NASZ znacznik. Bez
znacznika -- albo gdy cokolwiek sie nie udaje -- oddajemy natywne ``super()``.
Pliki, inne widoki AMC i wszystkie obce aplikacje zostaja nietkniete.

CZEGO TU NIE MA: zadnej zmiany ``config.conf`` ani ustawien mowy, zadnego
gestu, zadnego ``ui.message``. Ten modul tylko odpowiada na pytanie, ktore
NVDA samo zadaje.
"""

#: Te same nazwy, co w ``wxlite/amc_wx_lite/radio_position.py``.
#: Dwa znaczniki, bo nakladke NVDA dobiera RAZ (przy tworzeniu obiektu), a
#: tryb musi dac sie przelaczyc bez ponownego ustawiania fokusu:
#:   * PRESENCE_PROP -- "to lista radiowa AMC"; po nim nakladka sie dolacza,
#:   * MODE_PROP     -- "teraz ukrywaj"; czytany przy KAZDYM odczycie pozycji.
PRESENCE_PROP = "AMC.wx.RadioList.v1"
MODE_PROP = "AMC.wx.RadioList.HidePosition.v1"

#: Klasa okna zwyklej natywnej listy Windows. Lista AMC jest ``LC_REPORT``
#: bez ``LC_VIRTUAL``, wiec to jest jej klasa. Drugi warunek obok znacznika:
#: nie chcemy dotykac niczego innego, nawet gdyby znacznik gdzies wyciekl.
WINDOW_CLASS = "SysListView32"


def _realPropReader():
    """``GetPropW`` z ``user32``. Wstrzykiwalne, zeby testy szly bez Windows."""
    import ctypes
    from ctypes import wintypes

    user = ctypes.WinDLL("user32", use_last_error=True)
    user.GetPropW.argtypes = [wintypes.HWND, wintypes.LPCWSTR]
    user.GetPropW.restype = wintypes.HANDLE

    def read(hwnd, name):
        return user.GetPropW(hwnd, name) or 0

    return read


_readProp = None


def setPropReader(reader):
    """Podmien czytnik wlasciwosci okna (testy). ``None`` wraca do ``user32``."""
    global _readProp
    _readProp = reader


def _prop(hwnd, name):
    global _readProp
    if _readProp is None:
        _readProp = _realPropReader()
    return _readProp(hwnd, name)


def isMarkedRadioList(hwnd):
    """Czy TO okno jest oznaczona lista radiowa AMC."""
    return bool(_prop(hwnd, PRESENCE_PROP))


def hidesPosition(hwnd):
    """Czy to okno jest TERAZ w trybie ukrywania pozycji.

    Oba znaczniki musza byc obecne. Sam tryb bez obecnosci nie wystarcza --
    inaczej przypadkowa zbieznosc nazwy w obcym oknie wyciszalaby licznik
    tam, gdzie uzytkownik go oczekuje.
    """
    return bool(_prop(hwnd, PRESENCE_PROP)) and bool(_prop(hwnd, MODE_PROP))


class AmcRadioListItem:
    """Jedyna zmiana: pusty ``positionInfo`` w trybie ukrywania.

    Tryb czytamy przy KAZDYM odczycie, bo NVDA dobiera nakladke tylko raz,
    przy tworzeniu obiektu, a opcja w AMC zmienia sie bez ponownego wejscia
    na liste (obiekt moze siedziec w cache czytnika).
    """

    def _get_positionInfo(self):
        try:
            hide = hidesPosition(self.windowHandle)
        except Exception:
            # Awaria odczytu nie moze uciszyc listy w nieznanym stanie --
            # natywne zachowanie jest bezpiecznym domyslem.
            hide = False
        if hide:
            # Tak samo jak bazowe NVDAObject._get_positionInfo: brak informacji
            # o pozycji, a nie podmieniona informacja.
            return {}
        return super()._get_positionInfo()


def chooseOverlay(obj, clsList):
    """Dolacz nakladke WYLACZNIE do wiersza oznaczonej listy AMC.

    Zapewnienie jest waskie z trzech stron naraz: nasz znacznik na HWND,
    klasa okna natywnej listy i to, ze obiekt jest WIERSZEM (``IAccessible``
    childID wiersza jest nie-zerowe; sama kontrolka listy ma 0).
    """
    try:
        hwnd = obj.windowHandle
        childId = obj.IAccessibleChildID
        windowClass = obj.windowClassName
    except AttributeError:
        return
    if not hwnd or not childId:
        return
    if windowClass != WINDOW_CLASS:
        return
    try:
        if not isMarkedRadioList(hwnd):
            return
    except Exception:
        return
    if AmcRadioListItem not in clsList:
        # Na POCZATEK listy: nasze ``_get_positionInfo`` musi wyprzedzic
        # natywne ``sysListView32.ListItem``, zeby ``super()`` w nakladce
        # trafilo wlasnie w nie.
        clsList.insert(0, AmcRadioListItem)
