"""Komunikat schowka ma opisywac to, co NAPRAWDE trafilo do schowka.

Usterka zmierzona przez rodzica (gest B06): Ctrl+Shift+C na pliku mowi
"Skopiowano plik i pełną ścieżkę", a ``_to_clipboard`` ustawia WYLACZNIE
``wx.TextDataObject``. Tekst sciezki to nie jest ten sam format, co plik
upuszczony z Eksploratora (``CF_HDROP`` / ``wx.FileDataObject``): wklejenie
do folderu nie utworzy kopii pliku. Komunikat obiecywal wiec czynnosc,
ktorej program nie wykonal -- i to jest sam fasz, nie brak funkcji.

NAJMNIEJSZA POPRAWKA, swiadomie w tym zakresie
----------------------------------------------
Mowimy prawde o tekscie: "Skopiowano pełną ścieżkę". DANYCH W SCHOWKU NIE
ZMIENIAMY (dalej tekst), skrotu nie ruszamy, Ctrl+Shift+C zostaje.

Czego tu CELOWO nie ma: ``wx.FileDataObject`` / ``CF_HDROP``. Pelny parytet
``CopyActionItemLocation`` z AMC (``MainWindow.xaml.cs:24428``) to osobny
etap -- dolozenie formatu plikowego po cichu przy korekcie komunikatu
zamienioby ta poprawke w niezmierzona zmiane zachowania.

Stacja zostaje bez zmian: "Skopiowano bezpośredni adres"
(``MainWindow.xaml.cs:24400``) juz byla prawdziwa -- adres strumienia to
tekst i niczego wiecej nie obiecuje.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row, rows_from_stations

# Funkcja CZYSTA: dobor komunikatu nie dotyka wx, wiec testuje sie w WSL.
from amc_wx_lite.gui import copied_address_message

URL = "http://stream.example.com:8000/live/nadajnik.mp3"


def test_file_path_message_does_not_promise_a_copied_file() -> None:
    """Sedno usterki: nie obiecujemy pliku, gdy kopiujemy tekst."""
    row = Row(item_id="f", title="a.mp3", kind="track", path="D:\\muzyka\\a.mp3")
    message = copied_address_message(row)
    assert message == "Skopiowano pełną ścieżkę"
    assert "plik i" not in message, (
        "FileDrop nie jest ustawiany, wiec slowo o pliku bylo falszywym sukcesem"
    )


def test_folder_path_is_described_the_same_honest_way() -> None:
    row = Row(item_id="d", title="muzyka", kind="folder", path="D:\\muzyka")
    assert copied_address_message(row) == "Skopiowano pełną ścieżkę"


def test_station_message_is_unchanged_it_was_already_true() -> None:
    """Regresja-strazniczka: korekta pliku nie moze ruszyc stacji."""
    row = rows_from_stations([{"id": "s1", "name": "Radio", "url": URL}])[0]
    assert copied_address_message(row) == "Skopiowano bezpośredni adres"


def test_what_lands_in_the_clipboard_is_still_the_plain_text_address() -> None:
    """Dane bez zmian: poprawiamy OPIS, nie zawartosc schowka."""
    row = Row(item_id="f", title="a.mp3", kind="track", path="D:\\muzyka\\a.mp3")
    assert row.address == "D:\\muzyka\\a.mp3"
    station = rows_from_stations([{"id": "s1", "name": "Radio", "url": URL}])[0]
    assert station.address == URL


def test_copy_name_message_is_untouched() -> None:
    """Ctrl+C byl juz zmierzony jako poprawny -- nie wolno go przy okazji ruszyc."""
    from amc_wx_lite.gui import COPIED_NAME_MESSAGE

    assert COPIED_NAME_MESSAGE == "Skopiowano nazwę"
