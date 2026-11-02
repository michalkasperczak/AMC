# TITLE-001 — data i godzina kompilacji w tytule AMC Test

- Status: W trakcie; zmieniono źródła i wykonano testy, bez kompilacji i instalacji.
- Zgłoszenie i korekta użytkownika: 31.10.2026. Zastępuje decyzję z commitu
  f1c09348 z 29.10.2026, która w trakcie słuchania Radia umieszczała stację
  i utwór w tytule okna (NVDA+T). Użytkownik chce ponownie czytać tam nazwę
  AMC Test oraz datę i godzinę kompilacji; informacje o odtwarzaniu w NVDA+End.

## Zachowanie i decyzja

- Główne okno ma stały tytuł `AMC Test — DD.MM.RRRR, GG:MM`, na przykład
  `AMC Test — 31.10.2026, 20:21`. Jest to czas utworzenia zainstalowanej
  paczki, nie aktualny zegar. Brak poprawnej daty w manifeście daje
  istniejącą etykietę wersji.
- Utwór i stacja nie podmieniają tytułu podczas odtwarzania Radia, również
  wtedy, gdy użytkownik przechodzi do innej sesji. Wspólny pasek stanu
  używany przez NVDA+End otrzymuje z hosta opis bieżącej audycji i
  dostępne parametry audio; Alt+D w Radiu nadal odczytuje informacje
  o aktualnej audycji.
- W zainstalowanej paczce e0bf1a55 manifest zawiera `displayVersion`:
  `31.10.2026, 20:21`. Kod źródłowy korzysta z istniejącej funkcji
  `version_info.window_title()`.

## Zmiany, testy i wynik

- `wxlite/amc_wx_lite/gui.py`: usunięto nadpisywanie tytułu przez
  `radio_window_title`, przywrócono stały tytuł z metadanych paczki.
- `wxlite/tests/test_current_radio_broadcast.py`: test ponownego ustawienia
  tytułu podczas odtwarzania Radia, bez utraty daty. Istniejące testy
  sprawdzają przekazanie opisu audycji do NVDA+End.
- `wxlite/JAK_TESTOWAC.md`: instrukcja kontroli tytułu i paska stanu.
- 31.10.2026: 17 testów `test_current_radio_broadcast.py` i
  `test_version_info.py` zaliczonych w 1,39 s. Nie wykonano ręcznej
  próby NVDA, kompilacji, instalacji ani publikacji.

## Materiał do dokumentacji użytkownika

NVDA+T czyta nazwę AMC Test oraz datę i godzinę kompilacji aplikacji.
NVDA+End czyta pasek stanu z informacją o bieżącym odtwarzaniu i
dostępnych parametrach audio. W Radiu Alt+D podaje szczegóły audycji.

## Znane ograniczenia

Poprawka istnieje w roboczych źródłach. Zainstalowana paczka nadal
ma starsze zachowanie do czasu przygotowania i sprawdzenia kolejnej
wersji. Ręczna próba NVDA wymaga osobnego odbioru.

## Wersja i commit

- Źródła: gałąź `codex/konfigurowalne-skroty-20261026`, zmiany
  niezatwierdzone; brak nowego commitu.
- Zainstalowana: manifest e0bf1a55, 31.10.2026, 20:21;
  ta instalacja nie zawiera poprawki.
