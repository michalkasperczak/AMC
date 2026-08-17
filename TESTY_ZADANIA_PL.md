# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-045`
- Tytuł zestawu: Pasek NVDA i skróty skoku w odtwarzaczu
- Wersja programu: `0.1.0-alpha.45`
- Utworzono: 2026-08-17 22:06, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2206_0.1.0-alpha.45.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-045-01 — NVDA+End

1. Naciśnij `NVDA+End` w zwykłym głównym oknie.
2. Zmaksymalizuj okno i ponów próbę.
3. Przywróć rozmiar, przesuń okno i sprawdź jeszcze raz.

Oczekiwane:

- NVDA za każdym razem odnajduje pasek;
- podaje usługę, stan, tytuł, czas, głośność i przepływność;
- niewidoczny punkt zgodności nie otrzymuje fokusu ani nie pojawia się w Alt+Tab.

## AMC-045-02 — Ctrl+J i Ctrl+Shift+J w odtwarzaczu

1. Otwórz odtwarzacz klawiszem `F6` dla elementu o znanym czasie.
2. Naciśnij `Ctrl+J`, wpisz poprawny czas i zatwierdź.
3. Naciśnij `Ctrl+Shift+J`, wpisz `50` i zatwierdź.

Oczekiwane:

- `Ctrl+J` otwiera „Skocz do czasu”;
- `Ctrl+Shift+J` otwiera „Skocz do procentu”;
- oba skoki zmieniają pozycję bieżącego odtwarzania.

## AMC-045-03 — Skróty tylko w odtwarzaczu

1. Wyjdź z odtwarzacza Escape na zwykłą listę.
2. Naciśnij kolejno `Ctrl+J`, `Ctrl+Shift+J` i cyfrę `5`.
3. Z menu lub palety wybierz „Skocz do czasu”.

Oczekiwane:

- skróty i cyfra na liście nie przewijają utworu ani nie otwierają okna skoku;
- polecenie wybrane jawnie z menu lub palety mówi, że skok jest dostępny tylko w odtwarzaczu i wskazuje `F6`.

## AMC-045-04 — F6 jako wspólny odtwarzacz

1. Przełączaj sesje demonstracyjne `Ctrl+1`, `Ctrl+2` i `Ctrl+3`.
2. W każdej sesji naciśnij `F6`, a następnie Escape.
3. Powtórz próbę w sesji lokalnej, jeżeli jest załadowana.

Oczekiwane:

- `F6` zawsze otwiera odtwarzacz bieżącej sesji;
- Escape wraca na poprzednią listę i element;
- funkcja nie jest przywiązana wyłącznie do lokalnych plików.
