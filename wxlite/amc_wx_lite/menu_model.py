"""Opis menu jako DANE -- bez zaleznosci od wx.

Menu AMC-wx-Lite nie ma wlasnej logiki. Kazda pozycja niesie ``Action``,
ktora juz istnieje i ktora wywoluje ten sam ``MainWindow._dispatch``, co
skrot klawiszowy. GUI tylko zamienia ten opis na ``wx.MenuBar``.

Dlaczego osobny modul, a nie od razu ``wx.Menu``:
  * da sie go sprawdzic testem bez okna i bez czytnika ekranu,
  * widac na jednej stronie, czy skrot w menu zgadza sie z tablica skrotow,
  * nie da sie przypadkiem dopisac pozycji z wlasna logika obok dispatchera.

Nazwy pozycji pochodza z pelnego AMC (MainWindow.xaml), nie sa wymyslone:
  458  "_Wszystkie pliki alfabetycznie"   Alt+2
  426  "_Ulubione"                        Ctrl+U
  427  "_Playlisty"                       Ctrl+P

Czego tu NIE MA i dlaczego:
  * 237 martwych pozycji z pelnego AMC -- nie kopiujemy menu, ktore nic nie
    robi w tej aplikacji,
  * pozycji "niedostepne" jako zapowiedzi -- menu wymienia tylko to, co
    naprawde dziala dzisiaj,
  * zapisu do wspolnego profilu -- jest czytany tylko do odczytu, wiec
    pozycje zmieniajace stacje sa UCZCIWIE wylaczane poza sesja radiowa.
"""

from __future__ import annotations

from dataclasses import dataclass

from .shortcuts import Action


@dataclass(frozen=True, slots=True)
class MenuItem:
    """Jedna pozycja. ``action`` prowadzi do dispatchera, ``builtin`` do okna.

    ``shortcut`` jest tylko PODPISEM -- klawisz obsluguje ``shortcuts.resolve``.
    Test pilnuje, zeby podpis zgadzal sie z prawda.
    """

    label: str = ""
    action: Action | None = None
    builtin: str | None = None
    shortcut: str | None = None
    #: Czy ``shortcut`` wolno oddac wx jako AKCELERATOR (tekst po ``\t``).
    #:
    #: Domyslnie tak. Dla GOLYCH klawiszy edycji (Backspace, Spacja, Delete)
    #: musi byc ``False``: wx robi z takiego podpisu akcelerator na poziomie
    #: OKNA, ktory ma pierwszenstwo przed kontrolka z fokusem i POLYKA klawisz
    #: w polu filtra. Zmierzone na zywym GUI (final-recovery, plan D):
    #: Backspace nie kasowal znaku, tylko wynosil uzytkownika o poziom wyzej.
    #: Sam skrot dziala dalej -- obsluguja go tablice w ``shortcuts``.
    accelerator: bool = True
    #: Wylaczana poza sesja radiowa, zamiast udawac, ze zadziala.
    needs_radio_session: bool = False
    #: Pozycja PRZELACZNIKA: wx ma ja wstawic jako ``AppendCheckItem``.
    #:
    #: Nie jest to kosmetyka. Zwykla pozycja menu nie niesie stanu, wiec
    #: czytnik ekranu powiedzialby tylko nazwe i uzytkownik nie wiedzialby, czy
    #: opcja jest wlaczona, dopoki jej nie przestawi. Pozycja zaznaczalna ma
    #: rolę "pole wyboru" i stan "zaznaczone"/"niezaznaczone" w nazwie
    #: dostepnosciowej, wiec stan slychac BEZ zmieniania go.
    checkable: bool = False
    #: Wymaga czegos grajacego (transport, pytanie o czas).
    needs_playback: bool = False
    #: Wymaga zaznaczonego wiersza na liscie (kopiowanie, otwarcie).
    needs_selection: bool = False
    is_separator: bool = False


@dataclass(frozen=True, slots=True)
class Menu:
    title: str
    items: tuple[MenuItem, ...]


SEPARATOR = MenuItem(is_separator=True)


def build_menus() -> tuple[Menu, ...]:
    """Pelny opis paska menu. Kolejnosc jak w pelnym AMC: Pliki, Radio, Widok."""
    files = Menu(
        "&Pliki",
        (
            # Kolejnosc i gesty z oryginalu (MainWindow.xaml:42-49): pliki pod
            # Ctrl+O, folder pod Ctrl+Shift+O.
            MenuItem("Otwórz &plik", Action.OPEN_FILE_DIALOG, shortcut="Ctrl+O"),
            MenuItem("Otwórz &folder", Action.OPEN_FOLDER_DIALOG, shortcut="Ctrl+Shift+O"),
            SEPARATOR,
            # Backspace dzialal od dawna, ale wylacznie z klawiatury.
            #
            # BEZ ``needs_selection``, i to jest WARUNEK dostepnosci, nie
            # kosmetyka. wxWidgets zaklada akcelerator okna z tekstu etykiety
            # PO TABULATORZE (``_build_menu`` dokleja tam podpis skrotu).
            # Akcelerator wyprzedza ``EVT_KEY_DOWN`` kontrolki, a gdy pozycja
            # jest WYLACZONA, Windows dopasowuje go i NIE wysyla komendy
            # nikomu -- klawisz zostaje POLKNIETY. Na pustym widoku (brak
            # wiersza => pozycja wyszarzona) Backspace przestawal wiec
            # istniec i z pustych Zakladek nie bylo wyjscia klawiatura.
            # Zmierzone: ``wx-keys-seen.jsonl`` nie zawiera ANI JEDNEGO
            # Backspace, przy ``wx_lista_ma_fokus=true``.
            #
            # Wyjscie z widoku nie potrzebuje zaznaczenia: ``go_to_parent``
            # w nazwanym widoku woli ``_leave_library_view`` jeszcze przed
            # szukaniem wiersza rodzica.
            MenuItem("Folder &nadrzędny", Action.PARENT_FOLDER, shortcut="Back",
                     accelerator=False),
            SEPARATOR,
            MenuItem("&Zakończ", builtin="quit"),
        ),
    )

    library = Menu(
        "&Biblioteka",
        (
            # Foldery Biblioteki dzialaly, ale wejscia w menu nie bylo.
            # MainWindow.xaml:446 "Foldery _Biblioteki" Alt+1.
            # Powrot do Biblioteki z nazwanego widoku. MainWindow.xaml:427
            # "_Biblioteka" Ctrl+L -- pozycji tej w porcie brakowalo, wiec
            # z Ulubionych i Historii nie bylo jak wrocic jednym gestem.
            MenuItem("&Biblioteka", Action.VIEW_LIBRARY, shortcut="Ctrl+L"),
            MenuItem("&Foldery Biblioteki", Action.VIEW_FOLDERS, shortcut="Alt+1"),
            MenuItem("&Wszystkie pliki alfabetycznie", Action.VIEW_ALL_FILES,
                     shortcut="Alt+2"),
            MenuItem("&Ulubione", Action.VIEW_FAVORITES, shortcut="Ctrl+U"),
            MenuItem("&Playlisty", Action.VIEW_PLAYLISTS, shortcut="Ctrl+P"),
            SEPARATOR,
            # Trzy odczyty aktywnosci. Skroty ODCZYTANE z kodu:
            #   MainWindow.xaml:488 "_Historia odtwarzania" Ctrl+H
            #   MainWindow.xaml:487 "_Kolejka"              Ctrl+Q
            MenuItem("&Historia odtwarzania", Action.VIEW_HISTORY, shortcut="Ctrl+H"),
            MenuItem("&Kolejka", Action.VIEW_SAVED_QUEUE, shortcut="Ctrl+Q"),
            # NIE "Zakładki" jak MainWindow.xaml:489. Tamta pozycja (Ctrl+B,
            # ViewBookmarks -> GetForDisplay) pokazuje WSZYSTKIE zakladki; my
            # mamy port GetForItem, czyli zakladki JEDNEGO zaznaczonego pliku.
            # Etykieta i gest musza ten wezszy zakres pokazac, a nie udawac
            # szerszej komendy oryginalu.
            MenuItem("&Zakładki zaznaczonego", Action.VIEW_ITEM_BOOKMARKS,
                     shortcut="Ctrl+Shift+B", needs_selection=True),
            # Pozycja oryginalu (MainWindow.xaml:489, Ctrl+B). Bez
            # ``needs_selection``: zbiorczy widok nie zalezy od zaznaczenia --
            # kontekst bierze z SESJI, a sesja moze nic nie odtwarzac.
            # Klawisz dostepu "i": "w" zajmuje "Wszystkie pliki
            # alfabetycznie", a "k" -- "Kolejka", obie w TYM SAMYM menu. Dwie
            # pozycje na jednej literze kazalyby uzytkownikowi czytnika
            # zgadywac, ktora sie wybierze.
            MenuItem("Wszystk&ie zakładki", Action.VIEW_ALL_BOOKMARKS,
                     shortcut="Ctrl+B"),
            SEPARATOR,
            MenuItem("&Otwórz zaznaczone", Action.ACTIVATE, shortcut="Return",
                     needs_selection=True),
            SEPARATOR,
            MenuItem("&Skopiuj nazwę", Action.COPY_NAME, shortcut="Ctrl+C",
                     needs_selection=True),
            MenuItem("W&ytnij plik", Action.CUT_FILE, shortcut="Ctrl+X",
                     accelerator=False),
            MenuItem("Skopiuj &adres", Action.COPY_ADDRESS, shortcut="Ctrl+Shift+C",
                     needs_selection=True),
            SEPARATOR,
            # Oryginal ma te pozycje w TYM SAMYM menu co widoki Kolejka/
            # Historia/Zakladki, po separatorze (MainWindow.xaml:490-491):
            #   <MenuItem Header="_Filtruj listę" InputGestureText="Ctrl+K" />
            # BEZ ``needs_selection``: filtr dziala takze na liscie, z ktorej
            # nic nie jest zaznaczone, a wylaczona pozycja menu polykalaby
            # akcelerator (czytnik mowilby wtedy "niedostepne" bez powodu).
            #
            # Sasiednich pozycji oryginalu "Szukaj w bieżącej usłudze" (Ctrl+F)
            # i "Szukaj globalnie" (Ctrl+Shift+F) tu NIE MA: to zapytania do
            # ZDALNEJ uslugi, ktorych ten port jeszcze nie ma. Martwa pozycja
            # menu byla by obietnica bez pokrycia.
            MenuItem("Fi&ltruj listę", Action.FOCUS_FILTER, shortcut="Ctrl+K"),
        ),
    )

    radio = Menu(
        "&Radio",
        (
            MenuItem("&Dodaj stację", Action.STATION_ADD, shortcut="Ctrl+N",
                     needs_radio_session=True),
            MenuItem("&Zmień stację", Action.STATION_EDIT, shortcut="F2",
                     needs_radio_session=True),
            MenuItem("&Usuń stację", Action.STATION_DELETE, shortcut="Delete",
                     accelerator=False, needs_radio_session=True),
            SEPARATOR,
            MenuItem("&Importuj M3U/PLS", Action.STATION_IMPORT, shortcut="Ctrl+I",
                     needs_radio_session=True),
            SEPARATOR,
            MenuItem("&Nagrywaj albo zatrzymaj wybraną stację",
                     Action.RECORD_TOGGLE, shortcut="Ctrl+R",
                     needs_radio_session=True),
            # Shift+Spacja i gola litera T nie moga zostac akceleratorami
            # okna: w polu filtra sa zwyklymi klawiszami edycji. Obsluguje je
            # resolver tylko wtedy, gdy fokus nie jest w polu tekstowym.
            MenuItem("&Wstrzymaj albo wznów nagrywanie",
                     Action.RECORD_PAUSE, shortcut="Shift+Space",
                     accelerator=False, needs_radio_session=True),
            MenuItem("Podziel &bieżące nagranie",
                     Action.RECORD_SPLIT, shortcut="T",
                     accelerator=False, needs_radio_session=True),
            MenuItem("Zatrzymaj w&szystkie nagrania",
                     Action.RECORD_STOP_ALL, shortcut="Ctrl+Alt+Shift+R",
                     accelerator=False, needs_radio_session=True),
            MenuItem("Pokaż n&agrywane", Action.VIEW_ACTIVE_RECORDINGS,
                     shortcut="Alt+R"),
            MenuItem("&Historia nagrywania", Action.VIEW_RECORDED_RADIO_FILES,
                     shortcut="Alt+Shift+R"),
            MenuItem("Harmonogra&m nagrywania", Action.MANAGE_RADIO_SCHEDULES,
                     shortcut="Ctrl+Shift+H", needs_radio_session=True),
            SEPARATOR,
            # Przelacznik licznika "3 z 37" na liscie stacji. Ustawienie
            # PRYWATNE portu wx (``state.radio_announce_position``) -- pelne
            # AMC nie dostaje tu nowego wariantu.
            #
            # Samo odznaczenie tej pozycji NIE uciszy licznika: mowi go NVDA z
            # natywnego ``positionInfo``, wiec ukrycie wykonuje nakladka w
            # dodatku AMC (wersja 0.4.0+). Przy starszym albo braku dodatku
            # ustawienie zapisze sie, a licznik zostanie -- i tak to wtedy
            # nazywamy w komunikacie, zamiast obiecywac skutek.
            MenuItem("Odczyt &pozycji stacji na liście",
                     Action.TOGGLE_RADIO_POSITION,
                     checkable=True, needs_radio_session=True),
        ),
    )

    playback = Menu(
        "&Odtwarzanie",
        (
            MenuItem("&Pauza albo wznowienie", Action.PLAY_PAUSE, shortcut="Space",
                     accelerator=False, needs_playback=True),
            SEPARATOR,
            # Przewijanie. CZTERY kroki oryginalu (MainWindow.xaml.cs:21583-21590):
            # 10 s goly, 30 s z Shift, 60 s z Ctrl, czas z ustawien z Ctrl+Alt.
            # Dotad zadnego z nich nie bylo w menu, wiec uzytkownik, ktory nie
            # zna skrotu, nie mial jak przewinac inaczej niz suwakiem.
            #
            # GOLE Left/Right: ``accelerator=False`` z TEGO SAMEGO powodu, co
            # Backspace i Spacja powyzej. Zmierzone na zywym GUI/NVDA po
            # scaleniu transportu z filtrem (kwit-kolizja-1, plan KOL-POLE):
            # w polu filtra z tekstem "kaz" trzy strzalki w poziomie NIE
            # ruszyly karetki (3 -> 3 -> 3 -> 3), a NVDA powiedzialo trzy razy
            # "pusta" zamiast przeczytac znak. Akcelerator na poziomie OKNA ma
            # pierwszenstwo przed kontrolka z fokusem i polyka klawisz TAKZE
            # wtedy, gdy pozycja menu jest wyszarzona (nic nie gra) -- wiec
            # klawisz nie robil ani przewijania, ani edycji.
            #
            # Dlaczego tylko gole: Ctrl+Left/Right, Shift+Left/Right i
            # Ctrl+Alt+Left/Right ZMIERZONO w tym samym przebiegu (plany
            # KOL-POLE2 i KOL-POLE3) i one karetke/zaznaczenie przepuszczaja,
            # wiec nie ma powodu odbierac im podpisu-akceleratora.
            MenuItem("Przewiń &wstecz 10 sekund", Action.SEEK_BACK_10,
                     shortcut="Left", accelerator=False, needs_playback=True),
            MenuItem("Przewiń w przó&d 10 sekund", Action.SEEK_FORWARD_10,
                     shortcut="Right", accelerator=False, needs_playback=True),
            MenuItem("Przewiń wstecz &30 sekund", Action.SEEK_BACK_30,
                     shortcut="Shift+Left", needs_playback=True),
            MenuItem("Przewiń w przód 3&0 sekund", Action.SEEK_FORWARD_30,
                     shortcut="Shift+Right", needs_playback=True),
            MenuItem("Przewiń wstecz &minutę", Action.SEEK_BACK_60,
                     shortcut="Ctrl+Left", needs_playback=True),
            MenuItem("Przewiń w przód m&inutę", Action.SEEK_FORWARD_60,
                     shortcut="Ctrl+Right", needs_playback=True),
            MenuItem("Przewiń wstecz o czas z &ustawień AMC",
                     Action.SEEK_BACK_CUSTOM, shortcut="Ctrl+Alt+Left",
                     needs_playback=True),
            MenuItem("Przewiń w przód o czas z ustawień &AMC",
                     Action.SEEK_FORWARD_CUSTOM, shortcut="Ctrl+Alt+Right",
                     needs_playback=True),
            SEPARATOR,
            # Predkosc odtwarzania. Naglowki i akceleratory doslownie z
            # MainWindow.xaml:382-393 ("_Wolniej" Shift+, / "_Szybciej" Shift+.
            # / "Prędkość _normalna" Ctrl+.). Sam suwak "Tempo" w oknie NIE
            # jest odbiorem tej funkcji: uzytkownik czytnika szuka polecenia w
            # menu i w pomocy, a Michal zglosil wprost, ze predkosci nie
            # znalazl.
            #
            # Shift+, i Shift+. to na klawiaturze ZNAKI "<" i ">", czyli
            # zwykle wpisywanie. Jako akcelerator okna nie docieraly do pola
            # filtra: zmierzone (kwit-kolizja-1, plan KOL-ZNAKI) -- po obu
            # gestach tekst pola pozostal pusty, a dopiero zwykla litera
            # cokolwiek wpisala. Dlatego ``accelerator=False``; samo polecenie
            # dziala dalej przez tablice w ``shortcuts`` i przez menu.
            # Ctrl+. zmierzone osobno jako nieszkodliwe i zostaje z podpisem.
            MenuItem("Wol&niej", Action.RATE_DOWN, shortcut="Shift+,",
                     accelerator=False, needs_playback=True),
            MenuItem("&Szybciej", Action.RATE_UP, shortcut="Shift+.",
                     accelerator=False, needs_playback=True),
            MenuItem("Prędkość no&rmalna", Action.RATE_RESET, shortcut="Ctrl+.",
                     needs_playback=True),
            SEPARATOR,
            # Skroty poprawione na te z oryginalu: Ctrl+SHIFT+E/R/T
            # (MainWindow.xaml.cs:21674-21676). Menu obiecywalo stare Ctrl+E/R/T,
            # czyli etykiete, ktora po poprawce tablicy byla by nieprawda.
            MenuItem("Czas minion&y", Action.TIME_ELAPSED, shortcut="Ctrl+Shift+E",
                     needs_playback=True),
            MenuItem("Czas p&ozostały", Action.TIME_REMAINING, shortcut="Ctrl+Shift+R",
                     needs_playback=True),
            MenuItem("Czas &całkowity", Action.TIME_TOTAL, shortcut="Ctrl+Shift+T",
                     needs_playback=True),
            SEPARATOR,
            # Przelacznik automatycznych komunikatow (cs:21670). We wspolnym
            # profilu wlascicielem state.json jest host C#, wiec akcja mowi,
            # gdzie te opcje zmienic -- zamiast udawac zapis.
            MenuItem("Automatyczne &komunikaty odtwarzacza",
                     Action.TOGGLE_SEEK_MESSAGES, shortcut="Ctrl+Shift+G"),
        ),
    )

    view = Menu(
        "&Widok",
        (
            MenuItem("Sesja: &Pliki lokalne", Action.SESSION_FILES, shortcut="Ctrl+1"),
            MenuItem("Sesja: &Radio", Action.SESSION_RADIO, shortcut="Ctrl+2"),
            SEPARATOR,
            # ``accelerator=False``: ZMIERZONE na zywym GUI (statusclip-1).
            # Z akceleratorem pozycja byla WYLACZONA na liscie (nic nie gralo),
            # a wylaczony akcelerator okna POLYKA swoj klawisz -- F6 dochodzil
            # do okna (``code 345``), ale nie trafial ani do komendy, ani do
            # ``_on_key``, wiec widok zostawal na liscie. Skrot dziala dalej:
            # obsluguje go ``shortcuts.LIST_VIEW["F6"]``, bezwarunkowo.
            #
            # ``needs_playback`` ZOSTAJE: pozycja ma byc wyszarzona, gdy nie ma
            # czego pokazywac. Zdejmujemy tylko akcelerator.
            MenuItem("Widok &odtwarzacza", Action.SHOW_PLAYER, shortcut="F6",
                     accelerator=False, needs_playback=True),
            # Powrot na liste istnial tylko z klawiatury (Escape/Shift+F6).
            # Akcja SHOW_LIST byla juz obslugiwana w _dispatch -- brakowalo
            # samego wejscia w menu, wiec nie jest to martwa pozycja.
            #
            # BEZ akceleratora, z tego samego powodu co Backspace wyzej: wx
            # zrobilby z podpisu akcelerator OKNA, ktory wyprzedza kontrolke z
            # fokusem. Escape ma trzy rozne znaczenia zaleznie od miejsca
            # (pole filtra czysci filtr, lista wychodzi o poziom wyzej,
            # odtwarzacz wraca na liste) -- jeden akcelerator okna splaszczylby
            # je do jednego i polknal klawisz w polu filtra.
            MenuItem("Powrót na &listę", Action.SHOW_LIST, shortcut="Escape",
                     accelerator=False),
        ),
    )

    # Menu Dzwiek juz istnialo i dziala -- model tylko oznacza miejsce, w
    # ktorym GUI dokleja swoje radio-itemy algorytmow (stan Check per pozycja).
    audio = Menu(
        "&Dźwięk",
        (
            MenuItem("&Algorytm przyspieszania", builtin="tempo-submenu"),
            SEPARATOR,
            # Opcje dotyczą sesji, także na pustej liście. Ctrl+Alt+Enter
            # jest skrótem okna, więc rejestrujemy natywny akcelerator;
            # sama obsługa KEY_DOWN listy nie uruchamiała dialogu w próbie wx.
            MenuItem(
                "&Opcje sesji…",
                Action.SESSION_OPTIONS,
                shortcut="Ctrl+Alt+Return",
                accelerator=True,
            ),
        ),
    )

    help_menu = Menu("Pomo&c", (MenuItem("&Skróty klawiszowe", Action.HELP, shortcut="F1"),))

    return (files, library, radio, playback, view, audio, help_menu)
