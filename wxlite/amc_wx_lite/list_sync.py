"""Przyrostowa aktualizacja ZWYKLEJ natywnej listy Windows.

JEDEN wspolny mechanizm dla WSZYSTKICH list AMC-wx-Lite. Zadnej wirtualizacji:
kontrolka jest zwyklym ``wx.ListCtrl(LC_REPORT)`` i trzyma teksty u siebie, tak
jak listy SARY i WinZappa, ktore uzytkownik wskazal jako dzialajace dobrze.

CO TEN MODUL ROBI I CZEGO NIE ROBI

* Robi: porownuje stan POKAZANY z stanem ZADANYM i oddaje najmniejszy plan
  operacji (``DeleteRow`` / ``InsertRow`` / ``SetField``). Gdy nic sie nie
  zmienilo, plan jest PUSTY -- i to jest cel zmiany: dawniej kazde wywolanie
  ``_sync_views`` (takze po samym komunikacie, Ctrl+C czy ticku statusu)
  przestawialo licznik wirtualnej listy i odswiezalo CALY zakres wierszy.
* Nie robi: niczego z czytnikiem ekranu. Nie wycisza, nie usypia, nie udaje
  fokusu. Mniej zbednych operacji na kontrolce to mniej zbednych zdarzen
  a11y -- ale dowodem mowy jest zywy NVDA, nie ten plik.

DWIE OSOBNE RZECZY DO PORONANIA (regula z ``ui-inspiration-sara-winzapp``)

1. TOZSAMOSC i KOLEJNOSC -- ciag ``item_id``. Stad dodania, usuniecia i
   przeniesienia wierszy.
2. TRESC -- teksty kolumn wiersza. Identyczne ``item_id`` NIE pozwala pominac
   zmiany: nowa nazwa, nowa dlugosc albo swiezy znacznik "ulubione" siedza przy
   tym samym ID i musza sie odswiezyc.

Modul jest czystym Pythonem (bez ``wx``), wiec jego kontrakt mierzymy w WSL.
"""

from __future__ import annotations

import difflib
from dataclasses import dataclass
from typing import Sequence

from .list_model import ListModel

#: Liczba kolumn listy. ZACHOWANA bez zmian: "Nazwa", "Rodzaj", "Szczegoly".
COLUMN_COUNT = 3


@dataclass(frozen=True, slots=True)
class RowText:
    """Jeden wiersz tak, jak go WIDZI kontrolka: tozsamosc + teksty kolumn.

    ``item_id`` jest tym samym trwalym identyfikatorem co w ``Row``/``ListModel``
    -- nie wprowadzamy drugiego systemu ID.
    """

    item_id: str
    texts: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class DeleteRow:
    """Usun wiersz ``index``. Indeks dotyczy stanu W MOMENCIE wykonania."""

    index: int


@dataclass(frozen=True, slots=True)
class InsertRow:
    """Wstaw nowy wiersz na pozycji ``index`` z gotowymi tekstami."""

    index: int
    item_id: str
    texts: tuple[str, ...]


@dataclass(frozen=True, slots=True)
class SetField:
    """Nadpisz JEDNA komorke. Nie wiersz, nie liste -- komorke."""

    index: int
    column: int
    text: str


#: Operacje planu. Celowo trzy proste rodzaje, bez frameworka.
ListOp = DeleteRow | InsertRow | SetField


def model_row_texts(model: ListModel) -> list[RowText]:
    """Zadany stan listy wyliczony z ISTNIEJACEGO modelu.

    Teksty bierzemy z ``ListModel.text_for`` -- dokladnie tej funkcji, ktora
    wczesniej obslugiwala ``OnGetItemText``. Semantyka kolumn, pomijanie
    powtorzen i slowa rodzaju zostaja bez zmian; zmienia sie tylko to, KIEDY
    tekst trafia do kontrolki (raz przy zmianie, nie przy kazdym malowaniu).
    """
    return [
        RowText(
            item_id=row.item_id,
            texts=tuple(model.text_for(index, column) for column in range(COLUMN_COUNT)),
        )
        for index, row in enumerate(model.rows)
    ]


def plan_row_updates(
    current: Sequence[RowText], desired: Sequence[RowText]
) -> list[ListOp]:
    """Najmniejszy plan przejscia z ``current`` do ``desired``.

    Kolejnosc wyniku jest KOLEJNOSCIA WYKONANIA:

    1. usuniecia od konca (indeksy nizszych wierszy sie nie przesuwaja),
    2. wstawienia od poczatku (prefiks listy zgadza sie wtedy z celem),
    3. aktualizacje pol na indeksach JUZ docelowych.

    Dzieki temu kazdy indeks jest poprawny w chwili uzycia -- plan, ktory
    "wyglada dobrze", a przy nakladaniu wychodzi poza zakres, byl by cichym
    bledem na liscie 2476 wierszy.

    Rozpoznawaniem wspolnych fragmentow zajmuje sie ``difflib`` ze standardowej
    biblioteki. Wlasnej macierzy LCS nie piszemy: na dwoch listach po 2476
    wierszy to miliony komorek w Pythonie, a ``SequenceMatcher`` szuka
    najdluzszych wspolnych blokow i na realnych danych (dopisany wiersz, zmiana
    nazwy, przeniesienie) konczy od razu.

    ``autojunk=False`` jest KONIECZNE: domyslna heurystyka uznaje element
    powtarzajacy sie w ponad 1% dlugiej listy za "smiec" i przestaje go
    dopasowywac. Przy tysiacach wierszy dalo by to nadmiarowe usuniecia i
    wstawienia zamiast punktowej zmiany.
    """
    current_ids = [row.item_id for row in current]
    desired_ids = [row.item_id for row in desired]

    deletes: list[DeleteRow] = []
    inserts: list[InsertRow] = []
    #: Pary (indeks w ``current``, indeks w ``desired``) wierszy, ktore ZOSTAJA.
    kept: list[tuple[int, int]] = []

    if current_ids == desired_ids:
        # Najczestszy przypadek w AMC: te same wiersze, byc moze inna tresc.
        # Omijamy dopasowywanie, zeby tick statusu nie liczyl diffa listy.
        kept = [(index, index) for index in range(len(current_ids))]
    else:
        matcher = difflib.SequenceMatcher(None, current_ids, desired_ids, autojunk=False)
        for tag, i1, i2, j1, j2 in matcher.get_opcodes():
            if tag == "equal":
                kept.extend((i1 + offset, j1 + offset) for offset in range(i2 - i1))
                continue
            if tag in ("delete", "replace"):
                deletes.extend(DeleteRow(index=index) for index in range(i1, i2))
            if tag in ("insert", "replace"):
                inserts.extend(
                    InsertRow(
                        index=index,
                        item_id=desired[index].item_id,
                        texts=tuple(desired[index].texts),
                    )
                    for index in range(j1, j2)
                )

    ops: list[ListOp] = []
    # Od konca: usuniecie wiersza 7 nie przesuwa wiersza 3.
    ops.extend(sorted(deletes, key=lambda op: op.index, reverse=True))
    # Od poczatku: po wstawieniu na pozycji 3 prefiks listy rowna sie celowi.
    ops.extend(sorted(inserts, key=lambda op: op.index))

    # Tresc porownujemy OSOBNO i tylko dla wierszy, ktore zostaly. Wiersze
    # wstawione maja juz wlasciwe teksty, wiec nie dotykamy ich drugi raz.
    for source, target in kept:
        before = current[source].texts
        after = desired[target].texts
        if before == after:
            continue
        for column in range(len(after)):
            old = before[column] if column < len(before) else ""
            if old != after[column]:
                ops.append(SetField(index=target, column=column, text=after[column]))
    return ops


def plan_cursor(*, wanted: int, selected: int, focused: int) -> int | None:
    """Gdzie przestawic kursor listy, albo ``None`` gdy nie ma co robic.

    ZAZNACZENIE i SKUPIONY WIERSZ to DWIE osobne wlasciwosci kontrolki.
    Dotychczasowy kod pytal wylacznie ``GetFirstSelected``, wiec rozjazd fokusu
    (po usunieciu wiersza kontrolka sama przesuwa fokus) zostawal nienaprawiony,
    a czytnik czytal inny wiersz niz ten z modelu. Sprawdzamy OBA.

    ``wanted < 0`` znaczy "model nie ma wyboru" -- na pustej liscie nie
    wymyslamy wiersza, zeby ukryc pustke.
    """
    if wanted < 0:
        return None
    if selected == wanted and focused == wanted:
        return None
    return wanted
