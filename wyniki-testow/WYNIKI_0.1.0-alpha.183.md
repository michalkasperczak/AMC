# Wyniki testów AMC 0.1.0-alpha.183

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia i Windows: OK.
- Lista harmonogramów: OK; Spacja zmienia stan istniejącego wiersza bez
  przebudowania listy, więc zaznaczony obiekt i fokus mogą pozostać na miejscu.
- Dostępność: OK; po wyłączeniu etykieta zawiera „pole wyboru niezaznaczone”,
  a po włączeniu „pole wyboru zaznaczone”. Powiadomienie zawiera także nazwę
  stacji, termin i stan harmonogramu.
- Dotychczasowe testy Radia, harmonogramów, nagrywania, Shazam, ustawień
  dźwięku, odtwarzania lokalnego i dekoderów: OK.
- Publikacja samowystarczalnego programu dla `win-x64`: OK.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.183\AccessibleMediaController-0.1.0-alpha.183.exe`.
- Wersja produktu: `0.1.0-alpha.183`.
- Rozmiar programu: `165958048` bajtów.
- SHA-256: `9E09884B194680E8CEF02CA0442FAC65561B101F8C6A08CF45E697843D0B0158`.

## Test ręczny

### AMC-183-01 — Spacja na harmonogramie

Otwórz listę harmonogramów przez `Ctrl+Shift+H`, wybierz konkretny
harmonogram i naciśnij Spację kilka razy.

Oczekiwane po każdym naciśnięciu:

- fokus pozostaje na tym samym harmonogramie;
- lista ani edytor nie otwierają się ponownie;
- przy włączeniu NVDA podaje nazwę stacji i termin oraz „pole wyboru
  zaznaczone, harmonogram włączony”;
- przy wyłączeniu NVDA podaje nazwę stacji i termin oraz „pole wyboru
  niezaznaczone, harmonogram wyłączony”;
- NVDA nie ogranicza komunikatu do „Zaplanowane nagrania radia, lista”.

Uwagi:
