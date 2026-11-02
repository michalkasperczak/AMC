# RELEASE-20261031 — AMC Test alpha.461

Status: kandydat nocnej aktualizacji. Instalacja i GitHub wymagają osobnej
weryfikacji wynikowej.

## Zachowanie użytkowe

- Tytuł okna (NVDA+T) stale pokazuje AMC Test i datę oraz godzinę
  kompilacji. Utwór i dostępne parametry odtwarzania są w pasku stanu
  odczytywanym przez NVDA+End. W Radiu Alt+D nadal podaje audycję.
- Ctrl+D w Radio Archiwiście pobiera całe nagranie. Ctrl+Shift+D pobiera
  wycinek; w Podcastach i YouTube ten skrót nadal pobiera wideo.
  Okna postępu mają przycisk Anuluj z procentem albo informacją o
  nieznanym rozmiarze, po zakończeniu oddają fokus i podają pełną ścieżkę.
  Historia pobrań jest zachowana.
- Po nieoczekiwanym zakończeniu interfejsu diagnostyka podaje kod,
  lokalizację dziennika i fakt zamknięcia AMC. Przycisk OK zamyka
  jedynie diagnostykę.

## Źródło i decyzja

Kandydat powstał w osobnym worktree od rewizji e0bf1a55 aktualnie
zainstalowanego Test alpha.460. Dodano tylko sprawdzone poprawki
tytułu, pobierania Radio Archiwisty, anulowania i launchera.
Zachowano nowszą Historię pobrań z tej bazy. Kanoniczna gałąź robocza
z pozostałymi zmianami nie została przełączona ani nadpisana.

## Testy i ograniczenia

31.10.2026: 58 testów logiki, 3 testy natywnych okien wxPython,
128 testów skrótów, menu, palety, języków i Historii pobrań oraz
38 testów launchera zaliczonych; 1 test pominięty warunkowo.
Ręczny odsłuch NVDA i rzeczywisty transfer
wycinka pozostają do odbioru. Kompilacja, instalacja i publikacja
muszą zostać zapisane po wykonaniu; nie wynikają z tych testów.
Awaria naruszenia pamięci mogła mieć więcej niż jedną przyczynę.

## Wersja i commit

0.1.0-alpha.461. Commit kandydata zostanie uzupełniony po zapisaniu
spójnych źródeł. GitHub i zainstalowana wersja pozostają alpha.460
do czasu osobnej weryfikacji.
