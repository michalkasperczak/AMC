"""Dialog Opcji sesji WYKONANY na atrapach -- nie czytany jako tekst.

Testy czytajace zrodlo (``"wx.Choice(" in build``) przechodza takze wtedy,
gdy kod nigdy sie nie uruchamia. Tu dialog jest NAPRAWDE konstruowany, a
atrapy zapisuja, jakie kontrolki powstaly i z jakimi wariantami. Dzieki temu
"martwa kontrolka" (utworzona dla sesji, ktora jej nie wykona) wychodzi jako
czerwony test.

GRANICA: to nadal nie jest wxWidgets ani czytnik ekranu. Dowodzi doboru
kontrolek i odczytu wyborow, NIE tego, ze NVDA je przeczyta.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from test_gui_logic import install_wx_stub

install_wx_stub()

import wx  # noqa: E402  (zastepnik wx musi byc pierwszy)

from amc_wx_lite import session_options  # noqa: E402
from amc_wx_lite.navigation import SessionId  # noqa: E402
from amc_wx_lite.state_store import Options  # noqa: E402


# --------------------------------------------------------------- atrapy wx


class FakeChoice:
    """Atrapa ``wx.Choice``: pamieta warianty, nazwe i wybor."""

    def __init__(self, parent, choices=None, **kwargs) -> None:
        self.choices = list(choices or [])
        self.name = ""
        self._selection = -1
        self.focused = False

    def SetName(self, name: str) -> None:  # noqa: N802 - API wx
        self.name = name

    def GetName(self) -> str:  # noqa: N802 - API wx
        return self.name

    def SetSelection(self, index: int) -> None:  # noqa: N802 - API wx
        self._selection = index

    def GetSelection(self) -> int:  # noqa: N802 - API wx
        return self._selection

    def SetFocus(self) -> None:  # noqa: N802 - API wx
        self.focused = True


class FakeStaticText:
    def __init__(self, parent, label: str = "", **kwargs) -> None:
        self.label = label


class FakeSizer:
    def __init__(self, *args, **kwargs) -> None:
        self.items: list = []

    def Add(self, item, *args, **kwargs) -> None:  # noqa: N802 - API wx
        self.items.append(item)

    def AddMany(self, entries) -> None:  # noqa: N802 - API wx
        for entry in entries:
            self.items.append(entry[0])

    def AddGrowableCol(self, *args, **kwargs) -> None:  # noqa: N802 - API wx
        pass


class FakePanel:
    def __init__(self, parent, **kwargs) -> None:
        pass

    def SetSizer(self, sizer) -> None:  # noqa: N802 - API wx
        pass


def _build(session: SessionId, options: Options, overrides) -> object:
    """Zbuduj dialog na atrapach i zwroc gotowy obiekt."""
    saved = {name: getattr(wx, name, None) for name in
             ("Choice", "StaticText", "FlexGridSizer", "BoxSizer", "Panel", "Dialog")}
    wx.Choice = FakeChoice
    wx.StaticText = FakeStaticText
    wx.FlexGridSizer = FakeSizer
    wx.BoxSizer = FakeSizer
    wx.Panel = FakePanel

    class FakeDialog:
        """Baza ``wx.Dialog``: przechwytuje tytul i uklad, nie tworzy okna."""

        def __init__(self, parent=None, title: str = "", **kwargs) -> None:
            self.title = title

        def CreateStdDialogButtonSizer(self, flags):  # noqa: N802 - API wx
            # Zapisujemy flagi: dialog MUSI miec oba przyciski, bo inaczej
            # nie da sie ani zapisac, ani wycofac zmian.
            self.button_flags = flags
            return FakeSizer()

        def SetSizer(self, sizer) -> None:  # noqa: N802 - API wx
            pass

        def Fit(self) -> None:  # noqa: N802 - API wx
            pass

    wx.Dialog = FakeDialog
    # Import PO podstawieniu atrap: klasa dziedziczy po FakeDialog.
    import importlib

    from amc_wx_lite import gui

    importlib.reload(gui)
    try:
        return gui.SessionOptionsDialog(None, session, options, overrides)
    finally:
        for name, value in saved.items():
            if value is not None:
                setattr(wx, name, value)


def _controls(dialog) -> dict:
    return {
        "loudness": dialog.loudness,
        "transitions": dialog.transitions,
        "silence": dialog.silence,
        "pause": dialog.pause,
    }


# ------------------------------------------------- dobor kontrolek per sesja


def test_pliki_lokalne_dostaja_wszystkie_wykonalne_kontrolki():
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    controls = _controls(dialog)
    for name in ("loudness", "transitions", "silence", "pause"):
        assert controls[name] is not None, f"Pliki lokalne wykonuja {name}"


def test_radio_nie_dostaje_kontrolek_ktorych_nie_wykona():
    """Brak kontrolki, nie kontrolka ukryta.

    Ukryta kontrolka nadal siedzi w kolejnosci Tab i czytnik o niej mowi --
    uzytkownik dostalby opcje, ktora nic nie robi.
    """
    dialog = _build(
        SessionId.RADIO, Options(), session_options.SessionPlaybackOverrides()
    )
    controls = _controls(dialog)
    assert controls["loudness"] is None, "Radio nie ma normalizacji"
    assert controls["transitions"] is None, "Radio nie ma przejsc miedzy utworami"
    assert controls["silence"] is None, "Radio nie ma ciszy miedzy utworami"
    assert controls["pause"] is not None, "wyjscie z odtwarzacza dotyczy obu sesji"


def test_liczba_kontrolek_zgadza_sie_ze_zdolnosciami():
    for session in (SessionId.FILES, SessionId.RADIO):
        dialog = _build(
            session, Options(), session_options.SessionPlaybackOverrides()
        )
        caps = session_options.capabilities_for(session)
        created = sum(1 for value in _controls(dialog).values() if value is not None)
        expected = (3 if caps.supports_audio_processing else 0) + (
            1 if caps.supports_player_exit_pause else 0
        )
        assert created == expected, f"{session.value}: {created} != {expected}"


# ------------------------------------------------------- warianty i wybory


def test_kazde_pole_pozwala_wrocic_do_ustawienia_ogolnego():
    """Bez wariantu dziedziczonego wybor bylby nieodwracalny."""
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    for name, control in _controls(dialog).items():
        if control is None:
            continue
        assert control.choices, f"{name} ma warianty"
        assert "ogólne" in control.choices[0], (
            f"{name}: pierwszy wariant wraca do ustawienia ogólnego"
        )
        assert control._amc_values[0] is None


def test_cisza_oferuje_tylko_dlugosci_przyjmowane_przez_silnik():
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    values = [v for v in dialog.silence._amc_values if v is not None]
    assert tuple(values) == session_options.INTER_TRACK_SILENCE_CHOICES
    labels = dialog.silence.choices[1:]
    assert labels == [
        session_options.inter_track_silence_label(v)
        for v in session_options.INTER_TRACK_SILENCE_CHOICES
    ], "etykiety ciszy brzmia jak w oryginale"


def test_otwarcie_pokazuje_ZAPISANY_wybor_a_nie_domysl():
    """Dialog otwarty po zapisie musi pokazac to, co uzytkownik wybral."""
    overrides = session_options.SessionPlaybackOverrides(
        loudness_normalization=True,
        smooth_track_transitions=False,
        inter_track_silence_ms=3000,
        pause_on_player_exit=True,
    )
    dialog = _build(SessionId.FILES, Options(), overrides)
    assert dialog.overrides == overrides, "wyjscie dialogu = wejscie bez zmian"


def test_wybor_uzytkownika_wraca_z_dialogu():
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    dialog.loudness.SetSelection(dialog.loudness._amc_values.index(True))
    dialog.silence.SetSelection(dialog.silence._amc_values.index(2000))
    result = dialog.overrides
    assert result.loudness_normalization is True
    assert result.inter_track_silence_ms == 2000
    assert result.smooth_track_transitions is None, "nietkniete pole zostaje dziedziczone"


def test_radio_nie_zwraca_wyborow_ktorych_nie_ma():
    dialog = _build(
        SessionId.RADIO, Options(), session_options.SessionPlaybackOverrides()
    )
    result = dialog.overrides
    assert result.loudness_normalization is None
    assert result.smooth_track_transitions is None
    assert result.inter_track_silence_ms is None


# --------------------------------------------------------- mowa i obsluga


def test_tytul_mowi_ktora_sesje_konfigurujemy():
    """Uzytkownik moze otworzyc dialog nie patrzac na liste -- tytul to jedyny
    pewny sygnal, ktorej sesji dotkna zmiany."""
    files = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    radio = _build(
        SessionId.RADIO, Options(), session_options.SessionPlaybackOverrides()
    )
    assert files.title != radio.title
    assert "Pliki lokalne" in files.title and "Opcje sesji" in files.title
    assert "Radio" in radio.title


def test_kontrolki_maja_nazwy_bez_znakow_akceleratora():
    """Czytnik czyta nazwe kontrolki. ``&`` i dwukropek zostaja w etykiecie."""
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    for control in _controls(dialog).values():
        if control is None:
            continue
        assert control.name, "kazda kontrolka ma nazwe dostepnosciowa"
        assert "&" not in control.name
        assert not control.name.endswith(":")


def test_dialog_ma_zapisz_i_anuluj():
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    flags = dialog.button_flags
    assert flags == (wx.OK | wx.CANCEL), (
        "standardowy uklad przyciskow daje Enter=Zapisz i Escape=Anuluj"
    )


def test_fokus_startuje_na_pierwszej_kontrolce():
    dialog = _build(
        SessionId.FILES, Options(), session_options.SessionPlaybackOverrides()
    )
    focused = [c for c in _controls(dialog).values() if c is not None and c.focused]
    assert len(focused) == 1, "dokladnie jedna kontrolka dostaje fokus na start"
    assert focused[0] is dialog.loudness


def test_wariant_dziedziczony_mowi_co_z_niego_wynika():
    """Port ``PlayerExitPausePolicy.DescribeSessionMode``.

    "Jak ustawienie ogólne" bez skutku zmusza do sprawdzania ustawien
    osobnym gestem, zeby wiedziec, co sie stanie.
    """
    options = Options()
    options.pause_on_player_exit = True
    dialog = _build(
        SessionId.RADIO, options, session_options.SessionPlaybackOverrides()
    )
    assert "wstrzymuj" in dialog.pause.choices[0].lower()

    options.pause_on_player_exit = False
    dialog = _build(
        SessionId.RADIO, options, session_options.SessionPlaybackOverrides()
    )
    assert "dalej" in dialog.pause.choices[0].lower()
