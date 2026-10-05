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
    #: Wylaczana poza sesja radiowa, zamiast udawac, ze zadziala.
    needs_radio_session: bool = False
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
            MenuItem("Otwórz &folder", Action.OPEN_FOLDER_DIALOG, shortcut="Ctrl+O"),
            MenuItem("Otwórz &plik", Action.OPEN_FILE_DIALOG, shortcut="Ctrl+Shift+O"),
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
            MenuItem("Folder &nadrzędny", Action.PARENT_FOLDER, shortcut="Back"),
            SEPARATOR,
            MenuItem("&Zakończ", builtin="quit"),
        ),
    )

    library = Menu(
        "&Biblioteka",
        (
            # Foldery Biblioteki dzialaly, ale wejscia w menu nie bylo.
            # MainWindow.xaml:446 "Foldery _Biblioteki" Alt+1.
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
                     needs_radio_session=True),
            SEPARATOR,
            MenuItem("&Importuj M3U/PLS", Action.STATION_IMPORT, shortcut="Ctrl+I",
                     needs_radio_session=True),
        ),
    )

    playback = Menu(
        "&Odtwarzanie",
        (
            MenuItem("&Pauza albo wznowienie", Action.PLAY_PAUSE, shortcut="Space",
                     needs_playback=True),
            SEPARATOR,
            MenuItem("Czas &miniony", Action.TIME_ELAPSED, shortcut="Ctrl+E",
                     needs_playback=True),
            MenuItem("Czas p&ozostały", Action.TIME_REMAINING, shortcut="Ctrl+R",
                     needs_playback=True),
            MenuItem("Czas &całkowity", Action.TIME_TOTAL, shortcut="Ctrl+T",
                     needs_playback=True),
        ),
    )

    view = Menu(
        "&Widok",
        (
            MenuItem("Sesja: &Pliki lokalne", Action.SESSION_FILES, shortcut="Ctrl+1"),
            MenuItem("Sesja: &Radio", Action.SESSION_RADIO, shortcut="Ctrl+2"),
            SEPARATOR,
            MenuItem("Widok &odtwarzacza", Action.SHOW_PLAYER, shortcut="F6",
                     needs_playback=True),
            # Powrot na liste istnial tylko z klawiatury (Escape/Shift+F6).
            # Akcja SHOW_LIST byla juz obslugiwana w _dispatch -- brakowalo
            # samego wejscia w menu, wiec nie jest to martwa pozycja.
            MenuItem("Powrót na &listę", Action.SHOW_LIST, shortcut="Escape"),
        ),
    )

    # Menu Dzwiek juz istnialo i dziala -- model tylko oznacza miejsce, w
    # ktorym GUI dokleja swoje radio-itemy algorytmow (stan Check per pozycja).
    audio = Menu("&Dźwięk", (MenuItem("&Algorytm przyspieszania", builtin="tempo-submenu"),))

    help_menu = Menu("Pomo&c", (MenuItem("&Skróty klawiszowe", Action.HELP, shortcut="F1"),))

    return (files, library, radio, playback, view, audio, help_menu)
