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
            MenuItem("Folder &nadrzędny", Action.PARENT_FOLDER, shortcut="Back",
                     needs_selection=True),
            SEPARATOR,
            MenuItem("&Zakończ", builtin="quit"),
        ),
    )

    library = Menu(
        "&Biblioteka",
        (
            MenuItem("&Wszystkie pliki alfabetycznie", Action.VIEW_ALL_FILES,
                     shortcut="Alt+2"),
            MenuItem("&Ulubione", Action.VIEW_FAVORITES, shortcut="Ctrl+U"),
            MenuItem("&Playlisty", Action.VIEW_PLAYLISTS, shortcut="Ctrl+P"),
            SEPARATOR,
            MenuItem("&Otwórz zaznaczone", Action.ACTIVATE, shortcut="Return",
                     needs_selection=True),
            SEPARATOR,
            MenuItem("&Skopiuj nazwę", Action.COPY_NAME, shortcut="Ctrl+C",
                     needs_selection=True),
            MenuItem("Skopiuj &adres", Action.COPY_ADDRESS, shortcut="Ctrl+Shift+C",
                     needs_selection=True),
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
        ),
    )

    # Menu Dzwiek juz istnialo i dziala -- model tylko oznacza miejsce, w
    # ktorym GUI dokleja swoje radio-itemy algorytmow (stan Check per pozycja).
    audio = Menu("&Dźwięk", (MenuItem("&Algorytm przyspieszania", builtin="tempo-submenu"),))

    help_menu = Menu("Pomo&c", (MenuItem("&Skróty klawiszowe", Action.HELP, shortcut="F1"),))

    return (files, library, radio, playback, view, audio, help_menu)
