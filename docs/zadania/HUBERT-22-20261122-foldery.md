# Hubert #22 — szybkość Folderów Biblioteki

Stan 22.11.2026: poprawka źródeł i testy w kandydacie `codex/alpha512-update-candidate-20261122`, commit `12dd0c50`. Nie instalowano, nie uruchamiano widocznego AMC z NVDA i nie wydano aktualizacji.

Zgłoszenie Huberta: [#22 — Foldery Biblioteki otwierają się po kilka sekund](https://github.com/michalkasperczak/AMC/issues/22)

https://github.com/michalkasperczak/AMC/issues/22

## Przyczyna i zmiana

`LibraryDatabase.active_items_under()` sortował wszystkie aktywne rekordy po `title COLLATE AMC_PL`. Kolacja w Pythonie kosztuje wiele porównań przy dużej Bibliotece. Widok Foldery sam sortuje podfoldery i pliki po utworzeniu wierszy, więc SQL wykonywał pracę, której wynik był później zastępowany. Usunięcie całego `ORDER BY` zmieniłoby kolejność przy indeksie członkostwa i mogłoby zmienić pisownię podfolderu oraz kolejność remisów.

Dodano opcjonalny parametr `by_title`, domyślnie `True`. Wyłącznie dwa wywołania widoku Foldery przekazują `False` i czytają rekordy z `ORDER BY rowid`. Pozostali wywołujący zachowują sortowanie tytułów. Nie zmienia się schemat bazy, zapis profilu, foldery na dysku ani kolejność użytkowa widoku. Test obejmuje remisy `abc`/`ABC`, pierwszą pisownię podfolderu `CD` oraz zapytanie korzenia z osieroconym plikiem. Fałszywa baza testu #21 przyjmuje nowy argument nazwany.

## Pomiary i testy

Odizolowany skrypt `benchmark_hubert22.py` w katalogu roboczym Codexa zbudował syntetyczną bazę 60 000 plików. Mierzono samo `folder_rows`, bez tworzenia bazy, na tym samym komputerze i tych samych danych; odczyt nie dotykał prawdziwej Biblioteki użytkownika.

| Widok | Przed | Po | Liczba wierszy | Skrót wyniku przed i po |
|---|---:|---:|---:|---|
| Korzeń Folderów | 4,986 s | 0,249 s | 1 | `95dbb4b3b86a1e6c` |
| Folder `X:\Music` | 8,766 s | 0,544 s | 54 018 | `11f550db173f4b54` |

To jeden pomiar syntetyczny, nie gwarantowany czas na dysku Michała. Zgadza się kierunek z wcześniejszym pomiarem Huberta na jego kopii bazy; jego liczby pozostają w zgłoszeniu #22. Skrót SHA-256 policzono z identyfikatorów, nazw i rodzajów zwracanych wierszy, więc wynik potwierdza zgodność tych pól w tej próbie. Nie obejmuje odsłuchu ani zachowania NVDA.

- `PYTHONPATH=wxlite python -m pytest wxlite/tests/test_library_folder_rows_order.py wxlite/tests/test_library_folder_issue21.py wxlite/tests/test_library_db.py -q`: **8 passed, 14 skipped**.
- Szerszy zestaw dziewięciu plików testów Biblioteki, widoków, nawigacji i aktywności: **114 passed, 49 skipped, 2 subtests passed**. Pominięcia to brak opcjonalnej kopii prawdziwej bazy w środowisku testów.
- Źródła i testy: tak. Build nowego kandydata: nie wykonano po tym commicie. Instalacja: nie. Ręczny odbiór NVDA: nie. Publikacja GitHub: nie.

## Materiał do instrukcji i dalszy krok

Widok Foldery Biblioteki powinien otwierać duże katalogi szybciej. Skróty i nazwy poleceń nie zmieniają się. Po bezpiecznej instalacji przetestować otwarcie kilku dużych folderów w rzeczywistym profilu, przejście do rodzica, kolejność wierszy oraz odczyt NVDA. Pomysł Huberta na osobne filtrowanie osieroconych plików w SQL pozostaje odrębną optymalizacją, bez zmiany w tym commicie. Po faktycznym wydaniu tej poprawki przekazać Hubertowi wynik przez zgłoszenie #22.
