"""Planer PRZYROSTOWYCH aktualizacji zwyklej (natywnej) listy Windows.

DLACZEGO TEN PLIK ISTNIEJE. Do tej pory ``MediaListCtrl`` byla kontrolka
``LC_VIRTUAL`` i KAZDE odswiezenie widoku szlo ta sama droga: ``SetItemCount``
plus ``RefreshItems(0, n-1)`` na calym zakresie -- rowniez wtedy, gdy dane sie
nie zmienily (sam ``Announce``, Ctrl+C, tick statusu). Uzytkownik zatwierdzil
zmiane na ZWYKLE natywne listy z aktualizacja tylko faktycznie zmienionych
wierszy i pol.

Ten planer jest JEDNYM wspolnym mechanizmem tej zmiany. Jest czystym Pythonem
(bez ``wx``), wiec jego kontrakt da sie zmierzyc w WSL bez pulpitu; samo
nalozenie planu na kontrolke sprawdza ``test_native_list_apply.py``.

Czego te testy NIE dowodza: mowy czytnika ekranu. Mowe mierzy zywy NVDA, kwity
osobno.
"""

from __future__ import annotations

from amc_wx_lite.list_sync import (
    DeleteRow,
    InsertRow,
    RowText,
    SetField,
    model_row_texts,
    plan_cursor,
    plan_row_updates,
)
from amc_wx_lite.list_model import ListModel, Row


def rt(item_id: str, name: str, kind: str = "utwór", detail: str = "") -> RowText:
    return RowText(item_id=item_id, texts=(name, kind, detail))


def ids_of(rows) -> list[str]:
    return [row.item_id for row in rows]


def apply_plan(current: list[RowText], ops) -> list[RowText]:
    """Referencyjne nalozenie planu -- tak samo, jak zrobi to kontrolka.

    Dzieki temu kazdy test sprawdza nie tylko LICZBE operacji, ale i to, ze
    po ich wykonaniu lista naprawde ma zadana tresc.
    """
    rows = list(current)
    for op in ops:
        if isinstance(op, DeleteRow):
            del rows[op.index]
        elif isinstance(op, InsertRow):
            rows.insert(op.index, RowText(item_id=op.item_id, texts=op.texts))
        elif isinstance(op, SetField):
            row = rows[op.index]
            texts = list(row.texts)
            texts[op.column] = op.text
            rows[op.index] = RowText(item_id=row.item_id, texts=tuple(texts))
        else:  # pragma: no cover - planer nie emituje innych operacji
            raise AssertionError(f"nieznana operacja: {op}")
    return rows


# --------------------------------------------------------------- 1. brak zmian


def test_identical_content_produces_zero_operations() -> None:
    """To JEST zatwierdzone wymaganie: bez zmiany danych zero operacji.

    Stara droga wolala tu ``SetItemCount`` + ``RefreshItems`` na calej liscie
    nawet przy samym komunikacie. Zmierzone na zywym NVDA jako powtorzony
    odczyt biezacego wiersza.
    """
    rows = [rt("1", "Alfa"), rt("2", "Beta"), rt("3", "Gamma")]

    assert plan_row_updates(rows, list(rows)) == []


def test_empty_stays_empty_without_operations() -> None:
    assert plan_row_updates([], []) == []


# ------------------------------------------- 2. to samo ID, zmieniona tresc


def test_same_id_with_a_new_name_updates_just_that_field() -> None:
    """Identyczne ID NIE pozwala pominac zmiany tekstu (regula z WNIOSKI.md).

    Zmiana nazwy, dlugosci albo Ulubionych przy tym samym ID musi sie
    odswiezyc -- i wylacznie ona.
    """
    current = [rt("1", "Alfa"), rt("2", "Beta"), rt("3", "Gamma")]
    desired = [rt("1", "Alfa"), rt("2", "Beta po zmianie nazwy"), rt("3", "Gamma")]

    ops = plan_row_updates(current, desired)

    assert ops == [SetField(index=1, column=0, text="Beta po zmianie nazwy")]
    assert apply_plan(current, ops) == desired


def test_same_id_with_new_detail_updates_only_the_detail_column() -> None:
    """Dlugosc/\"ulubione\" siedza w kolumnie Szczegoly -- tylko ona sie zmienia."""
    current = [rt("1", "Alfa", detail="5:03")]
    desired = [rt("1", "Alfa", detail="5:03, ulubione")]

    ops = plan_row_updates(current, desired)

    assert ops == [SetField(index=0, column=2, text="5:03, ulubione")]


def test_two_changed_fields_in_one_row_are_two_field_updates_not_a_rewrite() -> None:
    current = [rt("1", "Alfa", kind="utwór", detail="5:03"), rt("2", "Beta")]
    desired = [rt("1", "Alfa", kind="playlista", detail="54 elementy"), rt("2", "Beta")]

    ops = plan_row_updates(current, desired)

    assert ops == [
        SetField(index=0, column=1, text="playlista"),
        SetField(index=0, column=2, text="54 elementy"),
    ]
    assert all(not isinstance(op, (InsertRow, DeleteRow)) for op in ops), (
        "zmiana tekstu nie moze przebudowywac wiersza"
    )


# --------------------------------- 3. jedno dodanie/usuniecie/przeniesienie


def test_one_insertion_does_not_touch_unrelated_rows() -> None:
    current = [rt("1", "Alfa"), rt("3", "Gamma")]
    desired = [rt("1", "Alfa"), rt("2", "Beta"), rt("3", "Gamma")]

    ops = plan_row_updates(current, desired)

    assert len(ops) == 1
    assert isinstance(ops[0], InsertRow)
    assert ops[0].index == 1 and ops[0].item_id == "2"
    assert apply_plan(current, ops) == desired


def test_one_removal_does_not_touch_unrelated_rows() -> None:
    current = [rt("1", "Alfa"), rt("2", "Beta"), rt("3", "Gamma")]
    desired = [rt("1", "Alfa"), rt("3", "Gamma")]

    ops = plan_row_updates(current, desired)

    assert ops == [DeleteRow(index=1)]
    assert apply_plan(current, ops) == desired


def test_appending_to_the_end_of_a_long_list_is_one_insert() -> None:
    """Przypadek 500 -> 501 (helper WinZappa dawal 0 delete / 1 insert)."""
    current = [rt(str(i), f"Poz {i}") for i in range(500)]
    desired = current + [rt("500", "Poz 500")]

    ops = plan_row_updates(current, desired)

    assert ops == [InsertRow(index=500, item_id="500", texts=("Poz 500", "utwór", ""))]


def test_moving_one_row_leaves_the_rest_alone() -> None:
    """Alt+Up/Ctrl+X w liscie: przenosimy JEDEN wiersz, nie przepisujemy listy."""
    current = [rt("1", "Alfa"), rt("2", "Beta"), rt("3", "Gamma"), rt("4", "Delta")]
    desired = [rt("3", "Gamma"), rt("1", "Alfa"), rt("2", "Beta"), rt("4", "Delta")]

    ops = plan_row_updates(current, desired)

    assert apply_plan(current, ops) == desired
    touched = {op.index for op in ops if isinstance(op, SetField)}
    assert touched == set(), "przeniesienie to struktura, nie przepisywanie tekstow"
    # Jeden ruch = jedno usuniecie i jedno wstawienie, nie cztery.
    assert len([o for o in ops if isinstance(o, DeleteRow)]) == 1
    assert len([o for o in ops if isinstance(o, InsertRow)]) == 1
    assert len([o for o in ops if isinstance(o, SetField)]) == 0
    assert all(
        not isinstance(op, (InsertRow, DeleteRow)) or op.index in (0, 2)
        for op in ops
    )


def test_unrelated_rows_are_not_rewritten_when_one_row_is_inserted_in_the_middle() -> None:
    current = [rt(str(i), f"Poz {i}") for i in range(200)]
    desired = current[:100] + [rt("nowy", "Nowy")] + current[100:]

    ops = plan_row_updates(current, desired)

    assert len(ops) == 1, f"jedno wstawienie, a bylo {len(ops)} operacji"
    assert apply_plan(current, ops) == desired


# ------------------------------------------- 4. prawdziwa zmiana calego zbioru


def test_a_genuinely_different_set_is_replaced_whole() -> None:
    """Pelna podmiana jest DOPUSZCZALNA, gdy zbior naprawde sie zmienil
    (Alt+1 Foldery <- Wszystkie pliki). Nie jest domyslnym odswiezeniem."""
    current = [rt(f"old{i}", f"Stary {i}") for i in range(5)]
    desired = [rt(f"new{i}", f"Nowy {i}") for i in range(3)]

    ops = plan_row_updates(current, desired)

    assert apply_plan(current, ops) == desired
    assert len([o for o in ops if isinstance(o, DeleteRow)]) == 5
    assert len([o for o in ops if isinstance(o, InsertRow)]) == 3


def test_becoming_empty_deletes_every_row_and_nothing_else() -> None:
    current = [rt("1", "Alfa"), rt("2", "Beta")]

    ops = plan_row_updates(current, [])

    assert apply_plan(current, ops) == []
    assert all(isinstance(op, DeleteRow) for op in ops)
    assert len(ops) == 2


def test_filling_an_empty_list_only_inserts() -> None:
    desired = [rt("1", "Alfa"), rt("2", "Beta")]

    ops = plan_row_updates([], desired)

    assert [type(op) for op in ops] == [InsertRow, InsertRow]
    assert [op.index for op in ops] == [0, 1]
    assert apply_plan([], ops) == desired


def test_shrinking_from_a_big_view_to_a_small_one_keeps_shared_rows() -> None:
    """2476 -> 11 (Alt+1): wspolne wiersze zostaja, reszta znika."""
    current = [rt(str(i), f"Poz {i}") for i in range(2476)]
    desired = [rt(str(i), f"Poz {i}") for i in range(11)]

    ops = plan_row_updates(current, desired)

    assert apply_plan(current, ops) == desired
    assert all(isinstance(op, DeleteRow) for op in ops), "nic nie trzeba wstawiac"


# ---------------------------------------------------- 5. fokus i zaznaczenie


def test_cursor_is_not_moved_when_it_already_matches_the_model() -> None:
    """Brak zmiany kursora = brak operacji. Inaczej kazde odswiezenie
    generowalo wlasne zdarzenie MSAA i czytnik powtarzal wiersz."""
    assert plan_cursor(wanted=3, selected=3, focused=3) is None


def test_cursor_moves_when_the_model_points_elsewhere() -> None:
    assert plan_cursor(wanted=0, selected=1, focused=1) == 0


def test_focus_and_selection_are_checked_separately() -> None:
    """Reguła 2 z WNIOSKI.md: zaznaczenie to NIE to samo co skupiony wiersz.

    Stara ``sync_selection`` pytala wylacznie ``GetFirstSelected``. Gdy fokus
    rozjechal sie z zaznaczeniem, kursor zostawal na zlym wierszu.
    """
    # Zaznaczenie zgadza sie z modelem, ale fokus stoi gdzie indziej.
    assert plan_cursor(wanted=5, selected=5, focused=2) == 5
    # I odwrotnie.
    assert plan_cursor(wanted=5, selected=2, focused=5) == 5


def test_no_cursor_when_the_model_has_no_selection() -> None:
    """Pusta lista: nie ma czego zaznaczyc i nie wymyslamy wiersza."""
    assert plan_cursor(wanted=-1, selected=-1, focused=-1) is None
    assert plan_cursor(wanted=-1, selected=0, focused=0) is None


# ------------------------------------------- 6. tresc wierszy z naszego modelu


def test_row_texts_come_from_the_existing_model_without_new_columns() -> None:
    """Zachowujemy ISTNIEJACY model, ID i trzy kolumny -- nic nie wymyslamy."""
    model = ListModel()
    model.replace(
        [
            Row(item_id="dir:/m/album", title="album", kind="folder", path="/m/album"),
            Row(
                item_id="st-1",
                title="Radio",
                kind="station",
                url="http://x/s",
                detail="128 kbps",
            ),
        ]
    )

    rows = model_row_texts(model)

    assert ids_of(rows) == ["dir:/m/album", "st-1"]
    # Dokladnie to, co oddawal ``OnGetItemText`` -- ta sama funkcja modelu.
    assert rows[0].texts == (model.text_for(0, 0), model.text_for(0, 1), model.text_for(0, 2))
    assert rows[1].texts == (model.text_for(1, 0), model.text_for(1, 1), model.text_for(1, 2))
    assert rows[0].texts == ("album", "folder", "")
    assert len(rows[0].texts) == 3, "trzy kolumny, bez nowych"


def test_row_texts_of_an_empty_model_are_empty() -> None:
    assert model_row_texts(ListModel()) == []


def test_favourite_mark_change_is_seen_through_the_model() -> None:
    """Scenariusz z wymagan: to samo ID, nowy znacznik Ulubionych."""
    model = ListModel()
    model.replace([Row(item_id="file:/m/a.mp3", title="a", kind="track", detail="5:03")])
    before = model_row_texts(model)

    model.replace(
        [Row(item_id="file:/m/a.mp3", title="a", kind="track", detail="5:03, ulubione")]
    )
    after = model_row_texts(model)

    ops = plan_row_updates(before, after)
    assert ops == [SetField(index=0, column=2, text="5:03, ulubione")]


# -------------------------------------------------------- 7. uczciwosc planu


def test_plan_indices_are_valid_while_the_plan_is_being_applied() -> None:
    """Operacje maja sens w KOLEJNOSCI nakladania, nie tylko na papierze.

    Mieszane usuwanie i wstawianie w kilku miejscach to najlatwiejszy sposob
    na plan, ktory wyglada dobrze, a przy nakladaniu trafia poza zakres.
    """
    current = [rt(str(i), f"Poz {i}") for i in range(20)]
    desired = (
        [rt("a", "Nowy A")]
        + current[1:5]
        + [rt("b", "Nowy B")]
        + current[8:15]
        + [rt("c", "Nowy C")]
    )

    ops = plan_row_updates(current, desired)

    rows = list(current)
    for op in ops:
        if isinstance(op, DeleteRow):
            assert 0 <= op.index < len(rows), f"usuniecie poza zakresem: {op}"
            del rows[op.index]
        elif isinstance(op, InsertRow):
            assert 0 <= op.index <= len(rows), f"wstawienie poza zakresem: {op}"
            rows.insert(op.index, RowText(item_id=op.item_id, texts=op.texts))
        else:
            assert 0 <= op.index < len(rows), f"pole poza zakresem: {op}"
            row = rows[op.index]
            texts = list(row.texts)
            texts[op.column] = op.text
            rows[op.index] = RowText(item_id=row.item_id, texts=tuple(texts))
    assert rows == desired


def test_duplicate_identifiers_do_not_confuse_field_updates() -> None:
    """Zakladki tego samego pliku moga powtarzac ID; plan nie moze ich zlepic."""
    current = [rt("dup", "Pierwsza"), rt("dup", "Druga"), rt("x", "Inna")]
    desired = [rt("dup", "Pierwsza"), rt("dup", "Druga zmieniona"), rt("x", "Inna")]

    ops = plan_row_updates(current, desired)

    assert apply_plan(current, ops) == desired


def test_plan_does_not_mutate_its_inputs() -> None:
    current = [rt("1", "Alfa")]
    desired = [rt("2", "Beta")]
    snapshot_current = list(current)
    snapshot_desired = list(desired)

    plan_row_updates(current, desired)

    assert current == snapshot_current and desired == snapshot_desired
