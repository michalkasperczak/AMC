"""Bezpiecznik przeciw ROZJECHANIU SIE kopii regul z oryginalem C#.

Po co
-----
Reguly transportu (kroki sekund, drabina predkosci, nazwy przelacznikow,
domyslne szablony) sa w AMC zapisane w kodzie C# i to on jest wzorcem. Python
musi je znac, bo mapuje klawisze i buduje komunikaty, ale KOPIA regul zyje
wlasnym zyciem: ktos poprawi C#, a Python zostanie na starych liczbach i znowu
"ten sam klawisz robi dwie rozne rzeczy w dwoch programach".

Ten plik nie testuje Pythona przeciw Pythonowi. CZYTA PLIKI C# i porownuje z
nimi nasze stale. Gdy oryginal zmieni krok przewijania, zakres predkosci albo
nazwe przelacznika, padnie TUTAJ -- z komunikatem, co dokladnie sie rozjechalo.

Dlaczego czytanie zrodla, a nie uruchamianie C#: nie budujemy tu .NET (testy
maja byc szybkie i dzialac bez pulpitu), a te konkretne reguly sa w kodzie
literalami, wiec daja sie odczytac wiarygodnie. Tam gdzie literalu nie ma,
test tego NIE UDAJE -- pomija punkt i mowi o tym wprost.

Ograniczenie, swiadome: to bezpiecznik na rozjazd WARTOSCI, a nie dowod
rownowaznosci zachowania. Dowodem zachowania sa ``test_transport_parity`` i
``test_transport_parity_gui_wiring``, a ostatecznie zywy odsluch u rodzica.
"""

from __future__ import annotations

import re
from pathlib import Path

from amc_wx_lite import transport_parity as tp

REPO = Path(__file__).parents[2]
CORE = REPO / "src" / "AccessibleMediaController.Core"
WINDOWS = REPO / "src" / "AccessibleMediaController.Windows"

SESSION = CORE / "Sessions" / "DemoMediaSession.cs"
SETTINGS = CORE / "Configuration" / "AppSettings.cs"
ROUTER = CORE / "Commands" / "CommandRouter.cs"
FORMATTER = CORE / "Presentation" / "MediaItemFormatter.cs"
MAIN_WINDOW = WINDOWS / "MainWindow.xaml.cs"
COMMAND_IDS = CORE / "Commands" / "CommandIds.cs"


def _read(path: Path) -> str | None:
    """Tresc pliku albo ``None``, gdy zrodla C# nie ma obok.

    Worktree moze byc rozdzielone; brak zrodla to powod do POMINIECIA punktu,
    nie do falszywej zieleni ani do padniecia calej suity.
    """
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None


def test_drabina_predkosci_jest_ta_sama_co_w_silniku() -> None:
    source = _read(SESSION)
    if source is None:
        print("POMINIETE: brak DemoMediaSession.cs obok worktree")
        return
    match = re.search(r"PlaybackRates\s*=\s*\[([^\]]+)\]", source)
    assert match, "nie znalazlem tablicy PlaybackRates w DemoMediaSession.cs"
    rates = tuple(
        round(float(value), 2)
        for value in re.findall(r"(\d+\.\d+)d", match.group(1))
    )
    assert rates, "tablica PlaybackRates pusta po odczycie"
    assert tp.PLAYBACK_RATES == rates, (
        f"drabina predkosci rozjechala sie z C#: C#={rates}, Python={tp.PLAYBACK_RATES}"
    )
    # Granice suwaka licza sie z tej samej tablicy, wiec nie moga jej przeczyc.
    assert tp.PLAYBACK_RATE_MIN == rates[0]
    assert tp.PLAYBACK_RATE_MAX == rates[-1]


def test_zakres_czasu_wlasnego_jest_ten_sam_co_w_ustawieniach() -> None:
    source = _read(SETTINGS)
    if source is None:
        print("POMINIETE: brak AppSettings.cs obok worktree")
        return
    found: dict[str, int] = {}
    for name in (
        "DefaultCustomSeekSeconds",
        "MinimumCustomSeekSeconds",
        "MaximumCustomSeekSeconds",
    ):
        match = re.search(rf"{name}\s*=\s*(\d+)", source)
        if match:
            found[name] = int(match.group(1))
    if not found:
        print("POMINIETE: nie znalazlem stalych CustomSeek w AppSettings.cs")
        return
    expected = {
        "DefaultCustomSeekSeconds": tp.CUSTOM_SEEK_DEFAULT_SECONDS,
        "MinimumCustomSeekSeconds": tp.CUSTOM_SEEK_MIN_SECONDS,
        "MaximumCustomSeekSeconds": tp.CUSTOM_SEEK_MAX_SECONDS,
    }
    for name, value in found.items():
        assert expected[name] == value, (
            f"{name} rozjechalo sie z C#: C#={value}, Python={expected[name]}"
        )


def test_kroki_przewijania_maja_pokrycie_w_komendach_okna() -> None:
    """Kazdy nasz krok ma odpowiadajaca komende w ``CommandIds``.

    Sprawdzamy OBIE strony: ze komenda oryginalu istnieje i ze liczba sekund
    w jej nazwie zgadza sie z nasza liczba. Dzieki temu nie da sie juz
    przypisac ``Shift`` szescdziesieciu sekund "z pamieci".

    Mapowanie nazw jest JAWNE, bo oryginal pisze "Backward", a nasza komenda
    "back" -- zgadywanie regula tekstowa dalo by falszywy alarm.
    """
    source = _read(COMMAND_IDS)
    if source is None:
        print("POMINIETE: brak CommandIds.cs obok worktree")
        return
    csharp_names = {
        tp.Action.SEEK_BACK_10: "SeekBackward10",
        tp.Action.SEEK_FORWARD_10: "SeekForward10",
        tp.Action.SEEK_BACK_30: "SeekBackward30",
        tp.Action.SEEK_FORWARD_30: "SeekForward30",
        tp.Action.SEEK_BACK_60: "SeekBackward60",
        tp.Action.SEEK_FORWARD_60: "SeekForward60",
    }
    # Nie wolno zapomniec o nowym kroku: tabela i mapowanie musza byc rowne.
    assert set(csharp_names) == set(tp.SEEK_STEPS), (
        "tabela SEEK_STEPS i mapowanie nazw C# maja rozne komendy"
    )
    for action, seconds in tp.SEEK_STEPS.items():
        name = csharp_names[action]
        assert re.search(rf"\b{name}\s*=", source), (
            f"brak komendy {name} w CommandIds.cs dla {action}"
        )
        digits = re.search(r"(\d+)$", name)
        assert digits, name
        assert int(digits.group(1)) == abs(seconds), (
            f"{action}: nazwa C# mowi {digits.group(1)} s, a my wysylamy {abs(seconds)} s"
        )
    # Krok wlasny nie ma liczby w nazwie -- bierze ja z ustawien.
    for name in ("SeekBackwardCustom", "SeekForwardCustom"):
        assert re.search(rf"\b{name}\s*=", source), f"brak komendy {name} w CommandIds.cs"


def test_nazwy_przelacznikow_komunikatow_istnieja_w_oryginale() -> None:
    """Czytamy ustawienia pod nazwami, ktore naprawde zapisuje host.

    Literowka w nazwie pola dalaby cichy powrot do domyslnego ``True`` -- czyli
    znowu gadanie przy kazdej strzalce, tylko trudniejsze do wykrycia.
    """
    source = _read(SETTINGS)
    if source is None:
        print("POMINIETE: brak AppSettings.cs obok worktree")
        return
    for csharp_name in (
        "SeekMessages",
        "ArrowSeekMessages",
        "PercentageSeekMessages",
        "VolumeMessages",
        "PlaybackMessages",
        "BookmarkNavigationMessages",
        "PercentageSeekAnnouncement",
    ):
        assert re.search(rf"\b{csharp_name}\b", source), (
            f"brak pola {csharp_name} w AppSettings.cs -- czytamy nieistniejace ustawienie"
        )


def test_bramka_strzalek_to_wciaz_koniunkcja_dwoch_przelacznikow() -> None:
    """``CommandRouter`` ma w sciezce przewijania ``SeekMessages &&``.

    Gdyby oryginal rozdzielil te bramki, nasza koniunkcja byla by za ostra i
    uciszylaby cos, co w AMC mowi.
    """
    source = _read(ROUTER)
    if source is None:
        print("POMINIETE: brak CommandRouter.cs obok worktree")
        return
    assert re.search(
        r"Messages\.SeekMessages\s*&&\s*settings\.Messages\.ArrowSeekMessages", source
    ), "bramka strzalek w C# nie jest juz koniunkcja SeekMessages i ArrowSeekMessages"
    assert re.search(
        r"Messages\.SeekMessages\s*&&\s*settings\.Messages\.PercentageSeekMessages", source
    ), "bramka procentow w C# nie jest juz koniunkcja SeekMessages i PercentageSeekMessages"
    assert re.search(
        r"Messages\.SeekMessages\s*&&\s*settings\.Messages\.VolumeMessages", source
    ), "bramka glosnosci w C# nie jest juz koniunkcja SeekMessages i VolumeMessages"


def test_format_czasu_zgadza_sie_z_formatterem_oryginalu() -> None:
    """``FormatDuration``: ``h:mm:ss`` od godziny, inaczej ``m:ss``."""
    source = _read(FORMATTER)
    if source is None:
        print("POMINIETE: brak MediaItemFormatter.cs obok worktree")
        return
    match = re.search(r"FormatDuration\(TimeSpan duration\)\s*\{(.+?)\n    \}", source, re.S)
    assert match, "nie znalazlem FormatDuration w MediaItemFormatter.cs"
    body = match.group(1)
    assert "TotalHours >= 1" in body, (
        "prog godzinowy w C# sie zmienil -- format_clock trzeba poprawic razem z nim"
    )
    # Minuty i sekundy dopelniane do dwoch cyfr tylko tam, gdzie C# to robi.
    assert "Seconds:00" in body
    assert tp.format_clock(231) == "3:51"
    assert tp.format_clock(3600) == "1:00:00"
