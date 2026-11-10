# RADIO-FAST-001 — zmiana stacji w otwartym odtwarzaczu

Status: **Zrobione — wycofane decyzją użytkownika 10.11.2026**. Nie jest planowane dalsze przyspieszanie w ramach tego zadania.

## Korekta decyzji z 10.11.2026

Użytkownik wycofał pomysł dodatkowych nazw „Następna stacja” i „Poprzednia stacja” oraz samo zadanie optymalizacji. Powód: takie nazwy nie przyspieszą wczytania stacji, a obecne przełączanie jest już wystarczająco szybkie. AMC ma nadal wypowiadać po prostu nazwę wybranej stacji. Ta decyzja zastępuje wcześniejszy plan dalszego pomiaru i optymalizacji oraz wcześniejszą propozycję nazw poleceń.

## Zachowanie użytkowe

- W otwartym odtwarzaczu Radia dotychczasowe Page Up/Page Down wybierają stację z listy źródłowej. Po wyborze AMC przekazuje do odczytania nazwę stacji (`Announce(target.title)`).
- Na zwykłej liście Page Up/Page Down przewijają stronami; samo poruszanie się po liście nie uruchamia stacji.
- Dodatkowe pozycje menu, nazwy w palecie i pomocy, tłumaczenia oraz testy tych nazw z commitu `a635835b` zostały cofnięte w `7e36bdca`. Tor audio i sama nawigacja nie były zmieniane. Nie dodano przygotowania sąsiednich strumieni.

## Historia i uzasadnienie

Po porównaniu z BlindRadio utworzono zadanie w `49d1a85a`. Użytkownik wybrał je 10.11.2026 jako najlżejsze do rozpoczęcia. W `a635835b` dodano wyłącznie nazwy poleceń oraz testy; opisano to w `ae6eaae0`. Korekta użytkownika z 10.11.2026 wycofuje te zmiany, ponieważ nie realizują celu szybszego wczytywania stacji. W przyszłości nowa decyzja i pomiar rzeczywistej zwłoki wymagałyby osobnego zadania.

## Testy i wynik

- Po wycofaniu: `python -m pytest -q tests/test_player_source_navigation.py tests/test_gui_logic.py::test_page_keys_scroll_all_native_lists_and_skip_tracks_only_in_player` w `wxlite`: **6 zaliczonych, 0 błędów**. Obejmuje istniejącą zmianę stacji i rozdzielenie listy od odtwarzacza.
- Przed wycofaniem: 10 testów celowanych dla nowych nazw było zaliczonych; nie opisują już bieżącej funkcji. Wtedy szerszy zestaw dał 78 zaliczonych, 1 pominięty, 3 błędy dotyczące równoległych zmian katalogu Radia i tłumaczeń.
- Ręcznego odsłuchu NVDA po wycofaniu, pomiaru opóźnienia, buildu, instalacji ani publikacji nie wykonano. Testy źródeł nie dowodzą stanu bieżącej instalacji.

## Ograniczenia, migracja, wersja

Wczytanie stacji nadal zależy od strumienia i serwera. Brak zmiany konfiguracji, danych i migracji. Wycofanie źródeł: commit `7e36bdca` na gałęzi `codex/amc-test-public-20261109`; nie nadano nowej wersji ani wydania.
