"""Brak danych o czasie to BRAK, nie zerowa dlugosc.

Zmierzone: w widokach "Wszystkie pliki" i zawartosci playlisty czytnik mowil
``Szczegoly: 0:00`` dla 2475 i 54 pozycji (kwit rodzica, gesty A02/A03/A09/B01).
To nie jest utwor dlugosci zero sekund -- to rekord bez zmierzonego czasu.

WZORZEC C#, przepisany, nie zgadniety
-------------------------------------
``src/AccessibleMediaController.Core/Presentation/MediaItemFormatter.cs:75-82``::

    private static string? FieldValue(MediaItem item, MediaItemField field) => field switch
    {
        ...
        MediaItemField.Duration when item.Duration > TimeSpan.Zero => FormatDuration(item.Duration),
        ...
        _ => null
    };

Warunek ``when item.Duration > TimeSpan.Zero`` jest w oryginale CZESCIA
wzorca: gdy czas nie jest dodatni, ``FieldValue`` zwraca ``null``, a
``Format`` (54) pomija wartosci puste (``IsNullOrWhiteSpace``). Oryginal
nigdy nie wypisuje wiec ``"0:00"`` -- po prostu nie mowi o czasie.

``FormatDuration`` (69-73) zostaje bez zmian dla czasow PRAWDZIWYCH: nasz
format ``"m:ss"`` / ``"h:mm:ss"`` jest tym samym, co w C#.

Zakres: maly RED/GREEN na formatterze wiersza. Zadnego audytu bazy.
"""

from __future__ import annotations

from amc_wx_lite.library_db import LibraryItem, _format_detail

SECOND = 10_000_000


def item(ticks: int, *, favorite: bool = False, recording: bool = False) -> LibraryItem:
    return LibraryItem(
        id="1",
        title="utwor",
        path="C:\\m\\utwor.mp3",
        duration_ticks=ticks,
        is_favorite=favorite,
        is_available=True,
        is_in_library=True,
        is_radio_recording=recording,
    )


# ----------------------------------------------------------- brak jako brak


def test_missing_duration_is_not_spoken_as_zero_length() -> None:
    """Rekord bez zmierzonego czasu: ``Szczegoly`` zostaje PUSTE.

    Tak robi ``FieldValue``: ``null`` dla ``Duration <= TimeSpan.Zero``, a
    ``Format`` pomija puste. Nie wymyslamy wlasnego slowa "nieznany" w
    kolumnie, bo oryginal takiego slowa tu nie ma.
    """
    assert _format_detail(item(0)) == ""


def test_negative_ticks_are_missing_data_too() -> None:
    """``> TimeSpan.Zero`` odrzuca tez wartosci ujemne, nie tylko zero."""
    assert _format_detail(item(-SECOND)) == ""


def test_missing_duration_still_reports_the_marks_it_does_know() -> None:
    """Brak czasu nie moze zjesc "ulubione" -- to osobna, znana informacja."""
    assert _format_detail(item(0, favorite=True)) == "ulubione"
    assert _format_detail(item(0, recording=True)) == "nagranie radia"
    assert _format_detail(item(0, favorite=True, recording=True)) == (
        "ulubione, nagranie radia"
    )


# -------------------------------------------- prawdziwe czasy bez regresji


def test_real_durations_keep_the_csharp_format() -> None:
    """``FormatDuration`` z C#: ``m:ss`` ponizej godziny, ``h:mm:ss`` wyzej."""
    assert _format_detail(item(303 * SECOND)) == "5:03"
    assert _format_detail(item(172 * SECOND)) == "2:52"
    assert _format_detail(item(3725 * SECOND)) == "1:02:05"


def test_one_second_is_a_real_duration_not_missing_data() -> None:
    """Granica warunku: 1 tick ponad zero to JUZ znany czas."""
    assert _format_detail(item(SECOND)) == "0:01"
    assert _format_detail(item(1)) == "0:00", (
        "ticki ponizej sekundy to czas ZNANY, tylko krotki -- "
        "oryginal formatuje go, bo Duration > TimeSpan.Zero"
    )


def test_real_duration_keeps_the_marks_after_it() -> None:
    assert _format_detail(item(303 * SECOND, favorite=True)) == "5:03, ulubione"
