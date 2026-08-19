# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-059`
- Tytuł zestawu: Pliki lokalne, schowek plikowy i bezpieczne usuwanie
- Wersja programu: `0.1.0-alpha.59`
- Utworzono: 2026-08-19, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.59.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-059-01 — Nazwa sesji i tytuł domyślnej listy

1. Otwórz kilka plików lokalnych i wróć na ich główną listę.
2. Odczytaj bieżący element, tytuł okna i komunikat po przełączeniu sesji.
3. Przejdź do Kolejki lub Ulubionych i powtórz odczyt.

Oczekiwane:

- program mówi „Pliki lokalne”, bez powtórzenia „multimedia, lokalne multimedia”;
- na głównej liście tytuł ma postać „nazwa — Pliki lokalne — AMC 0.1.0-alpha.59”;
- w konkretnym module pozostaje kolejność „nazwa — Kolejka lub Ulubione — Pliki lokalne — AMC”.

## AMC-059-02 — Krótkie informacje pod lewą strzałką

1. Na głównej liście Plików lokalnych zaznacz MP3, OGG i inny dostępny format.
2. Na każdym naciśnij `Strzałkę w lewo`.
3. Powtórz próbę w Kolejce albo Ulubionych.

Oczekiwane:

- na głównej liście NVDA podaje zwięźle dostępne dane, np. format, czas, kb/s, kHz i rozmiar;
- brakująca wartość jest pomijana, a program nie zawiesza się na odczytywaniu pliku;
- w innych widokach boczne strzałki nie wykonują tych lokalnych działań.

## AMC-059-03 — Menu pod prawą strzałką

1. Na głównej liście Plików lokalnych naciśnij `Strzałkę w prawo`.
2. Przejrzyj menu strzałkami.
3. Sprawdź „Otwórz w domyślnej aplikacji” i „Otwórz w…”.

Oczekiwane:

- otwiera się menu działań zaznaczonego elementu i fokus trafia do menu;
- pozycje otwierania są dostępne tylko dla lokalnego pliku;
- „Otwórz w…” pokazuje systemowy wybór aplikacji, a po zamknięciu menu fokus wraca na listę.

## AMC-059-04 — Plik i ścieżka w jednym schowku

1. Zaznacz plik i naciśnij `Ctrl+Shift+C`.
2. Wklej do zwykłego edytora tekstu.
3. W Total Commanderze albo Eksploratorze przejdź do innego folderu i naciśnij `Ctrl+V`.

Oczekiwane:

- edytor otrzymuje pełną ścieżkę tekstową;
- menedżer plików kopiuje fizyczny plik tak, jak po standardowym `Ctrl+C` w Eksploratorze;
- AMC mówi „Skopiowano plik i pełną ścieżkę”.

## AMC-059-05 — Wiele plików w schowku

Zaznacz co najmniej dwa pliki, naciśnij `Ctrl+Shift+C`, a następnie wklej do edytora i do pustego folderu.

Oczekiwane:

- edytor otrzymuje osobne ścieżki w osobnych wierszach;
- menedżer plików kopiuje wszystkie zaznaczone pliki;
- `Ctrl+C` nadal kopiuje wyłącznie nazwę elementu.

## AMC-059-06 — Delete i Ctrl+Z

1. Zaznacz plik na głównej liście i naciśnij `Delete`.
2. Sprawdź plik w Eksploratorze lub Total Commanderze.
3. Naciśnij `Ctrl+Z`, a potem zamknij i uruchom AMC ponownie.

Oczekiwane:

- wpis znika tylko z AMC, a plik pozostaje na dysku;
- `Ctrl+Z` przywraca wpis w jego miejscu;
- przywrócenie pozostaje zapisane po ponownym uruchomieniu.

## AMC-059-07 — Usunięcie całej sesji i bieżącego pliku

1. Uruchom jeden z dwóch testowych plików, wróć do listy i zaznacz oba.
2. Naciśnij `Delete`, a następnie `Ctrl+Z`.

Oczekiwane:

- bieżące odtwarzanie zostaje wstrzymane, a fizyczne pliki pozostają;
- pusta sesja znika bez błędu, a AMC przechodzi do innej sesji;
- `Ctrl+Z` przywraca Pliki lokalne, kolejność i zapamiętane pozycje.

## AMC-059-08 — Chronologia cofania i regresja

Zmień Ulubione, usuń wpis z AMC i dwa razy użyj `Ctrl+Z`. Następnie sprawdź wyrywkowo OGG, `Page Up/Down`, pamięć pozycji, Bibliotekę, Kolejkę, `Alt+Enter`, pasek i naturalne przejście do następnego pliku.

Oczekiwane: cofnięcia działają od najnowszej operacji niezależnie od jej rodzaju, a funkcje wersji 58 nadal działają.
