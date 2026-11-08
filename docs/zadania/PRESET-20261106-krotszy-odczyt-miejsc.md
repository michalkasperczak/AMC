# PRESET-20261106 — krótszy odczyt miejsc presetów

Stan: W trakcie — poprawka źródła i test celowany zaliczone; użytkownik odłożył budowę, instalację i odbiór NVDA do zbiorczej aktualizacji z równoległymi zadaniami.

## Materiał do dokumentacji użytkownika

W oknie presetów (`Ctrl+Alt+P`) i oknie przypisywania (`Ctrl+Alt+Shift+P`) każdy wiersz podaje numer miejsca, nazwę zapisanego elementu i skrót. Przykład: „8, nazwa stacji, Ctrl+Shift+8”. Wolne miejsce podaje „pusty”. Dziesiąte miejsce jest oznaczone 0, a dwa ostatnie odpowiednio minusem i znakiem równości. Dotyczy wspólnej listy miejsc w sesjach, które oferują takie presety; nie zmienia przypisań ani działania skrótów.

## Uzasadnienie i decyzje

Korekta użytkownika z 6.11.2026 zastępuje wcześniejszy, rozwlekły wzór „Preset numer 8, skrót Ctrl+Shift+8 — nazwa stacji”. Wiersz ma podawać tylko numer, nazwę i skrót, w tej kolejności. Nazwy i dane celu pozostają oddzielone od identyfikatorów wewnętrznych.

Późniejsza decyzja użytkownika z 6.11.2026: nie budować, nie instalować i nie testować teraz widocznego okna. Dołączyć poprawkę do zbiorczej aktualizacji z równoległymi zadaniami; odbiór NVDA wykonać po tej aktualizacji.

## Testy i wynik

- Bezokienkowy `wxlite/tests/test_profile_presets.py`: 25 zaliczonych, 0 niepowodzeń (6.11.2026). Sprawdzono wiersze zajęte i puste oraz miejsca 10–12.
- Ręczny odsłuch NVDA: odłożony przez użytkownika do zbiorczej aktualizacji. Przejście strzałkami, fokus po zapisie i anulowaniu oraz ponowne otwarcie nie zostały jeszcze potwierdzone.

## Znane ograniczenia

Kod w repozytorium nie oznacza jeszcze nowej kompilacji ani zmiany działającej instalacji.

## Wersja i commit

Źródła zmienione na gałęzi `codex/konfigurowalne-skroty-20261026`; nowego commitu, kompilacji, instalacji ani publikacji jeszcze nie ma.

## Zmienione pliki

- `wxlite/amc_wx_lite/profile_presets.py` — wspólna etykieta listy.
- `wxlite/tests/test_profile_presets.py` — oczekiwane brzmienie 12 miejsc.
- `docs/zadania/README.md` — wpis rejestru.
