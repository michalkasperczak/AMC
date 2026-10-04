"""Krotka mowa listy: kolumny maja ROZNE znaczenia, adres nie leci dwa razy.

Zglaszany objaw (zmierzony zywym NVDA na stacji radiowej): czytnik wymawial
pelny adres strumienia DWUKROTNIE -- raz jako "Rodzaj", raz jako "Szczegoly".
Przyczyna w kodzie: ``ListModel.text_for`` oddawalo ``row.detail`` dla KAZDEJ
kolumny innej niz 0, a ``MediaListCtrl`` ma trzy kolumny.

Wzorzec poprawnego odczytu bierzemy ze ZWYKLEGO AMC, nie z wlasnego pomyslu:

  src/AccessibleMediaController.Windows/MainWindow.xaml:531-534
      <ListBox x:Name="MediaList" DisplayMemberPath="Label" ...>
      => lista glowna AMC ma JEDNA czytana informacje na wiersz.

  src/AccessibleMediaController.Core/Sessions/MediaItem.cs:69-77
      KindLabel => MediaItemKind.Station => "stacja", Track => "utwor",
      Folder => "folder"
      => "Rodzaj" to SLOWO rodzaju, nigdy adres.

  src/AccessibleMediaController.Core/Presentation/MediaItemFormatter.cs:47-58
      Format(...) pomija wartosci PUSTE i POWTORZONE (``spokenValues.Add``)
      => ta sama tresc nie moze zabrzmiec dwa razy w jednym wierszu.

  src/AccessibleMediaController.Windows/MainWindow.xaml.cs:17920-17924
      homogeneousView: w sesji radio stacje NIE dostaja czlonu rodzaju
      => na liscie samych stacji "stacja" przy kazdym wierszu to szum.

  src/AccessibleMediaController.Windows/MainWindow.xaml.cs:20717-20732
      Ctrl+C -> CopyActionItemName(), Ctrl+Shift+C -> CopyActionItemLocation()
      => adres jest DOSTEPNY NA ZADANIE, a nie czytany domyslnie.
"""

from __future__ import annotations

from amc_wx_lite.list_model import (
    ListModel,
    Row,
    rows_from_folder_payload,
    rows_from_stations,
)

LONG_URL = "http://stream.example.com:8000/live/nadajnik-glowny-128kbps.mp3?token=abc123"


def _station_model() -> ListModel:
    model = ListModel()
    model.replace(rows_from_stations([{"id": "st-1", "name": "Radio Nowy Swiat", "url": LONG_URL}]))
    return model


def test_station_row_does_not_speak_the_address_at_all_by_default() -> None:
    # Sedno zgloszenia. Nie "nie dwa razy" -- domyslnie ani razu, bo wielki
    # adres jest nieczytelny dla czytnika ekranu i nie identyfikuje stacji.
    model = _station_model()
    spoken = [model.text_for(0, column) for column in range(3)]
    assert LONG_URL not in spoken, f"adres nie moze byc czytany domyslnie: {spoken}"


def test_station_columns_are_not_the_same_text_twice() -> None:
    model = _station_model()
    nonempty = [text for text in (model.text_for(0, c) for c in range(3)) if text]
    assert len(nonempty) == len(set(nonempty)), f"ta sama tresc dwa razy: {nonempty}"


def test_station_name_is_the_first_column() -> None:
    model = _station_model()
    assert model.text_for(0, 0) == "Radio Nowy Swiat"


def test_kind_column_says_the_kind_word_not_an_address() -> None:
    # KindLabel z MediaItem.cs:69-77 -- slowa, nie adresy ani sciezki.
    rows = [
        Row(item_id="dir:/m/x", title="Album", kind="folder", path="/m/x"),
        Row(item_id="file:/m/a.mp3", title="a", kind="track", path="/m/a.mp3"),
        Row(item_id="st-1", title="Radio", kind="station", url=LONG_URL),
    ]
    model = ListModel()
    model.replace(rows)
    kinds = [model.text_for(index, 1) for index in range(3)]
    assert kinds == ["folder", "utwór", "stacja"], kinds


def test_parent_row_kind_column_is_empty_not_a_fake_kind() -> None:
    # ".." nie jest rodzajem medium; falszywy rodzaj bylby klamstwem.
    model = ListModel()
    model.replace([Row(item_id="parent:/m", title="..", kind="parent", path="/m")])
    assert model.text_for(0, 1) == ""


def test_track_detail_column_is_not_a_full_path() -> None:
    # Szczegoly maja byc krotka informacja, nie powtorzeniem sciezki pliku.
    model = ListModel()
    model.replace([
        Row(item_id="file:D:/m/a.mp3", title="a.mp3", kind="track", path="D:\\m\\a.mp3")
    ])
    assert "D:\\m\\a.mp3" not in model.text_for(0, 2)


def test_folder_payload_no_longer_puts_the_word_folder_into_details() -> None:
    # Wczesniej 'folder' szlo do ``detail``, wiec kolumny Rodzaj i Szczegoly
    # mowily to samo slowo -- znow podwojny odczyt, tylko krotszy.
    payload = {
        "path": "/m",
        "parent": None,
        "items": [{"kind": "folder", "id": "dir:/m/x", "title": "x", "path": "/m/x"}],
    }
    model = ListModel()
    model.replace(rows_from_folder_payload(payload))
    assert model.text_for(0, 1) == "folder"
    assert model.text_for(0, 2) != "folder"


def test_address_stays_available_on_request() -> None:
    # Adres NIE znika z danych: Ctrl+Shift+C ma go skad wziac.
    model = _station_model()
    row = model.row_at(0)
    assert row is not None
    assert row.url == LONG_URL


def test_out_of_range_and_unknown_column_still_empty_not_error() -> None:
    model = _station_model()
    assert model.text_for(99, 0) == ""
    assert model.text_for(0, 7) == ""
