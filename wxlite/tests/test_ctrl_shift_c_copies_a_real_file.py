"""Ctrl+Shift+C ma oddac PRAWDZIWY PLIK, nie sam tekst sciezki.

ZGLOSZENIE
----------
Ctrl+Shift+C oglaszalo, ze kopiuje plik i sciezke, a wklejenie w Total
Commanderze nic nie robilo. Poprzedni etap naprawil sam KOMUNIKAT (mowil
odtad prawde o tekscie: "Skopiowano pełną ścieżkę") i jawnie odlozyl dane:

    test_clipboard_message_matches_data.py
    "Czego tu CELOWO nie ma: ``wx.FileDataObject`` / ``CF_HDROP``. Pelny
     parytet ``CopyActionItemLocation`` z AMC (``MainWindow.xaml.cs:24428``)
     to osobny etap."

To jest ten etap. Odwracamy kierunek: dane doganiaja oryginal, a komunikat
wraca do slow oryginalu DOPIERO dlatego, ze staja sie prawdziwe.

PELNY KONTRAKT ORYGINALU -- odczytany, nie odtworzony z komunikatu
------------------------------------------------------------------
``MainWindow.xaml.cs:24744-24814`` (``CopyItemLocations``):

  1. Sesja "podcasts" -> SAM TEKST bezposrednich adresow; brak adresu to
     "Dla zaznaczonego podcastu lub odcinka nie zapisano bezpośredniego adresu".
  2. Dla kazdego elementu: ``TryGetLocalPath(item.Source)`` decyduje,
     czy to sciezka lokalna; jak nie -- ``GetShareableLocation``.
  3. WSZYSTKIE elementy lokalne (``localPaths.Length == items.Count``):
         data.SetData(DataFormats.UnicodeText, string.Join(NewLine, localPaths));
         data.SetFileDropList(fileDropList);
     czyli DWA FORMATY NARAZ: file drop ORAZ tekst sciezki.
     Komunikat: "Skopiowano plik i pełną ścieżkę".
  4. Mieszanka lokalne + zdalne -> tez oba formaty, tekst zawiera takze URL-e.
  5. Same zdalne -> sam tekst, komunikat "Skopiowano łącze".
  6. Blad schowka (``ClipboardRetry``, ``MainWindow.xaml.cs:24783``)
     ZWRACA KOMUNIKAT BLEDU zamiast komunikatu sukcesu.

``TryGetLocalPath`` (``MainWindow.xaml.cs:5378-5390``) -- reguly dosLOWNE:

    if (string.IsNullOrWhiteSpace(source)
        || Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
        return false;
    if (!Path.IsPathFullyQualified(source)) return false;

czyli: pusty -> nie; absolutny URI ktory NIE jest ``file:`` -> nie;
sciezka niepelna (wzgledna) -> nie. Zwrocmy uwage, czego tu NIE MA:
``TryGetLocalPath`` SAM NIE SPRAWDZA ISTNIENIA pliku.

ISTNIENIE -- dwie rozne reguly, obie w oryginale
------------------------------------------------
``CopySearchResultLocations`` (``MainWindow.xaml.cs:24835-24838``) dokłada
``&& File.Exists(path)``. ``RadioPresetsWindow.xaml.cs:156-158`` sprawdza
``File.Exists(path) || Directory.Exists(path)`` -- bo preset moze wskazywac
FOLDER. Nasza lista pokazuje i pliki, i foldery, wiec bierzemy regule
szersza (plik ALBO folder): to jedyna z trzech, ktora nie klamie o folderze.
Nieistniejace zrodlo nie trafia do file dropu -- Windows i tak odmowilby
wklejenia, a my nie wolno nam ogłosic sukcesu kopiowania pliku.

CZEGO TU NIE MA, swiadomie
--------------------------
Nie dodajemy zaznaczenia wielokrotnego (lista jest ``LC_SINGLE_SEL``),
nie ruszamy ``Ctrl+C`` (``COPIED_NAME_MESSAGE``), nie dotykamy podcastow
(ta sesja jeszcze nie istnieje w wxlite) i nie wchodzimy na zywy schowek
Windows -- ``wx.TheClipboard`` jest tu wstrzykiwany jako atrapa.
"""

from __future__ import annotations

import os
import tempfile
from pathlib import Path

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui  # noqa: E402  (zastepnik wx musi byc pierwszy)
from amc_wx_lite.list_model import Row, rows_from_stations  # noqa: E402

URL = "http://stream.example.com:8000/live/nadajnik.mp3"


# --------------------------------------------- 1. TryGetLocalPath -> parytet


def test_absolute_windows_path_is_local() -> None:
    assert gui.is_local_path("D:\\muzyka\\a.mp3") is True


def test_absolute_posix_path_is_local() -> None:
    """Testy chodza w WSL; sciezka POSIX tez jest w pelni kwalifikowana."""
    assert gui.is_local_path("/home/michal/a.mp3") is True


def test_unc_path_is_local_it_is_fully_qualified() -> None:
    assert gui.is_local_path("\\\\serwer\\udzial\\a.mp3") is True


def test_http_url_is_not_a_local_path() -> None:
    """``Uri.TryCreate`` + ``!uri.IsFile`` -- MainWindow.xaml.cs:5382."""
    assert gui.is_local_path(URL) is False


def test_relative_path_is_not_local() -> None:
    """``!Path.IsPathFullyQualified`` -- MainWindow.xaml.cs:5387."""
    assert gui.is_local_path("muzyka\\a.mp3") is False
    assert gui.is_local_path("a.mp3") is False


def test_blank_source_is_not_local() -> None:
    assert gui.is_local_path("") is False
    assert gui.is_local_path("   ") is False
    assert gui.is_local_path(None) is False


def test_file_uri_is_local_because_IsFile_is_true() -> None:
    """Oryginal odrzuca URI tylko wtedy, gdy NIE jest ``file:``."""
    assert gui.is_local_path("file:///D:/muzyka/a.mp3") is True


# -------------------------------------------------- 2. co trafia do schowka


class FakeClipboard:
    """Atrapa ``wx.TheClipboard``. Zapamietuje OBIEKT, nie sam tekst."""

    def __init__(self, *, opens: bool = True, fails: bool = False) -> None:
        self._opens = opens
        self._fails = fails
        self.data = None
        self.flushed = False
        self.closed = 0

    def Open(self) -> bool:  # noqa: N802
        return self._opens

    def SetData(self, data) -> bool:  # noqa: N802, ANN001
        if self._fails:
            raise RuntimeError("schowek zajety")
        self.data = data
        return True

    def Flush(self) -> bool:  # noqa: N802
        self.flushed = True
        return True

    def Close(self) -> None:  # noqa: N802
        self.closed += 1


class FakeTextData:
    def __init__(self, text: str) -> None:
        self.text = text


class FakeFileData:
    def __init__(self) -> None:
        self.files: list[str] = []

    def AddFile(self, path: str) -> None:  # noqa: N802
        self.files.append(path)


class FakeComposite:
    def __init__(self) -> None:
        self.parts: list[tuple[object, bool]] = []

    def Add(self, data, preferred: bool = False) -> None:  # noqa: N802, ANN001
        self.parts.append((data, preferred))


def install_clipboard_stubs(clipboard: FakeClipboard) -> None:
    import wx

    wx.TheClipboard = clipboard
    wx.TextDataObject = FakeTextData
    wx.FileDataObject = FakeFileData
    wx.DataObjectComposite = FakeComposite


def make_frame(clipboard: FakeClipboard):
    """Atrapa okna: tylko to, czego dotyka droga kopiowania."""
    from types import SimpleNamespace

    install_clipboard_stubs(clipboard)
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    spoken: list[str] = []
    frame.announcer = SimpleNamespace(say=spoken.append)
    return frame, spoken


def set_selected(frame, row: Row | None) -> None:
    from types import SimpleNamespace

    frame.navigator = SimpleNamespace(
        session=SimpleNamespace(model=SimpleNamespace(selected_row=row))
    )


def test_existing_local_file_goes_to_the_clipboard_as_a_real_file() -> None:
    """SEDNO ZGLOSZENIA: wklejenie w menedzerze plikow ma utworzyc kopie."""
    with tempfile.TemporaryDirectory() as folder:
        path = os.path.join(folder, "a.mp3")
        Path(path).write_bytes(b"x")
        clipboard = FakeClipboard()
        frame, spoken = make_frame(clipboard)
        set_selected(frame, Row(item_id="f", title="a.mp3", kind="track", path=path))

        frame._copy_address()

        assert isinstance(clipboard.data, FakeComposite), (
            "sam TextDataObject to byl wlasnie zglaszany brak CF_HDROP"
        )
        kinds = {type(part) for part, _ in clipboard.data.parts}
        assert FakeFileData in kinds, "brak file dropu = Total Commander nie wklei"
        file_part = next(p for p, _ in clipboard.data.parts if isinstance(p, FakeFileData))
        assert file_part.files == [path]


def test_text_path_is_kept_next_to_the_file_as_in_the_original() -> None:
    """``data.SetData(UnicodeText, ...)`` obok ``SetFileDropList`` -- :24781."""
    with tempfile.TemporaryDirectory() as folder:
        path = os.path.join(folder, "a.mp3")
        Path(path).write_bytes(b"x")
        clipboard = FakeClipboard()
        frame, _spoken = make_frame(clipboard)
        set_selected(frame, Row(item_id="f", title="a.mp3", kind="track", path=path))

        frame._copy_address()

        text_part = next(
            p for p, _ in clipboard.data.parts if isinstance(p, FakeTextData)
        )
        assert text_part.text == path


def test_message_may_promise_a_file_only_now_that_a_file_is_copied() -> None:
    """Slowa wracaja do oryginalu (:24785), bo dane w koncu im odpowiadaja."""
    with tempfile.TemporaryDirectory() as folder:
        path = os.path.join(folder, "a.mp3")
        Path(path).write_bytes(b"x")
        clipboard = FakeClipboard()
        frame, spoken = make_frame(clipboard)
        set_selected(frame, Row(item_id="f", title="a.mp3", kind="track", path=path))

        frame._copy_address()

        assert spoken == ["Skopiowano plik i pełną ścieżkę"]


def test_existing_folder_is_copied_as_a_file_drop_too() -> None:
    """``Directory.Exists`` -- RadioPresetsWindow.xaml.cs:157."""
    with tempfile.TemporaryDirectory() as folder:
        clipboard = FakeClipboard()
        frame, spoken = make_frame(clipboard)
        set_selected(frame, Row(item_id="d", title="muzyka", kind="folder", path=folder))

        frame._copy_address()

        file_part = next(
            p for p, _ in clipboard.data.parts if isinstance(p, FakeFileData)
        )
        assert file_part.files == [folder]
        assert spoken == ["Skopiowano plik i pełną ścieżkę"]


# ------------------------------------------------ 3. przypadki NEGATYWNE


def test_missing_source_copies_text_only_and_says_so() -> None:
    """NAJPROSTSZY przypadek negatywny: sciezki nie ma na dysku.

    Nie wolno ogłosic skopiowanego PLIKU, bo file drop wskazywalby w pustke.
    Tekst sciezki nadal ma sens (mozna go wkleic do edytora), wiec go dajemy
    i mowimy o nim prawde.
    """
    clipboard = FakeClipboard()
    frame, spoken = make_frame(clipboard)
    set_selected(
        frame, Row(item_id="f", title="brak.mp3", kind="track", path="/nie/ma/brak.mp3")
    )

    frame._copy_address()

    assert isinstance(clipboard.data, FakeTextData), "zadnego file dropu w pustke"
    assert clipboard.data.text == "/nie/ma/brak.mp3"
    assert spoken == ["Skopiowano pełną ścieżkę"]


def test_station_url_is_still_text_only_and_keeps_its_old_message() -> None:
    """Regresja-strazniczka: stacja byla poprawna i nie wolno jej ruszyc."""
    clipboard = FakeClipboard()
    frame, spoken = make_frame(clipboard)
    set_selected(frame, rows_from_stations([{"id": "s1", "name": "Radio", "url": URL}])[0])

    frame._copy_address()

    assert isinstance(clipboard.data, FakeTextData)
    assert clipboard.data.text == URL
    assert spoken == ["Skopiowano bezpośredni adres"]


def test_row_without_any_address_is_refused() -> None:
    clipboard = FakeClipboard()
    frame, spoken = make_frame(clipboard)
    set_selected(frame, Row(item_id="p", title="..", kind="parent", path=None))

    frame._copy_address()

    assert clipboard.data is None
    assert spoken == ["Ten element nie ma zapisanego adresu"]


def test_busy_clipboard_never_claims_success() -> None:
    """``ClipboardRetry`` zwraca BLAD zamiast komunikatu sukcesu -- :24783."""
    with tempfile.TemporaryDirectory() as folder:
        path = os.path.join(folder, "a.mp3")
        Path(path).write_bytes(b"x")
        clipboard = FakeClipboard(opens=False)
        frame, spoken = make_frame(clipboard)
        set_selected(frame, Row(item_id="f", title="a.mp3", kind="track", path=path))

        frame._copy_address()

        assert spoken == ["Schowek jest zajęty, spróbuj ponownie"]
        assert not any("Skopiowano" in message for message in spoken)


def test_clipboard_write_failure_never_claims_success() -> None:
    with tempfile.TemporaryDirectory() as folder:
        path = os.path.join(folder, "a.mp3")
        Path(path).write_bytes(b"x")
        clipboard = FakeClipboard(fails=True)
        frame, spoken = make_frame(clipboard)
        set_selected(frame, Row(item_id="f", title="a.mp3", kind="track", path=path))

        frame._copy_address()

        assert len(spoken) == 1
        assert spoken[0].startswith("Nie udało się skopiować")
        assert "Skopiowano" not in spoken[0]
        assert clipboard.closed == 1, "schowek musi zostac zamkniety mimo bledu"


# --------------------------------------------------- 4. Ctrl+C bez zmian


def test_ctrl_c_still_copies_plain_text_name() -> None:
    """Ctrl+C byl zmierzony jako poprawny -- nie wolno go przy okazji ruszyc."""
    clipboard = FakeClipboard()
    frame, spoken = make_frame(clipboard)
    set_selected(frame, Row(item_id="f", title="a.mp3", kind="track", path="/x/a.mp3"))

    frame._copy_name()

    assert isinstance(clipboard.data, FakeTextData), "nazwa to TEKST, nie plik"
    assert clipboard.data.text == "a.mp3"
    assert spoken == ["Skopiowano nazwę"]


def test_copy_name_message_constant_is_untouched() -> None:
    assert gui.COPIED_NAME_MESSAGE == "Skopiowano nazwę"
