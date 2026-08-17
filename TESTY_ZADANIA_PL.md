# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-047`
- Tytuł zestawu: Pojedynczy i uporządkowany odczyt paska
- Wersja programu: `0.1.0-alpha.47`
- Utworzono: 2026-08-17 23:29, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2329_0.1.0-alpha.47.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-047-01 — Pojedynczy odczyt NVDA+End

1. Uruchom program i naciśnij `NVDA+End`.
2. Powtórz próbę podczas pauzy i odtwarzania.
3. Zmaksymalizuj okno i sprawdź jeszcze raz.

Oczekiwane:

- pełny tekst paska jest czytany dokładnie jeden raz;
- NVDA nie powtarza tego samego zestawu danych dla kontenera i etykiety;
- odczyt nie przenosi fokusu i nie blokuje programu.

## AMC-047-02 — Kolejność danych paska

1. Odtwórz lokalny plik o znanym czasie.
2. Przejdź w dowolne miejsce utworu i naciśnij `NVDA+End`.

Oczekiwane:

- kolejność brzmi: „Przepływność…, odtwarzanie albo pauza, pozycja z czasem całkowitym, głośność, tytuł, usługa”;
- długi tytuł nie opóźnia informacji o przepływności, czasie i głośności;
- przy braku danych początek brzmi „Przepływność brak danych”.

## AMC-047-03 — Polecenie odczytu stanu

1. Z menu Odtwarzanie wybierz „Odczytaj stan odtwarzania”.
2. Wykonaj to samo polecenie z palety `Ctrl+Shift+K`.

Oczekiwane:

- oba sposoby podają te same dane w tej samej kolejności co pasek;
- komunikat występuje tylko raz;
- fokus pozostaje na wcześniejszej kontrolce.

## AMC-047-04 — Fokus i skróty po odczycie

1. Po `NVDA+End` przejdź strzałką po liście.
2. Otwórz odtwarzacz przez `F6` i sprawdź `Ctrl+J` oraz `Ctrl+Shift+J`.
3. Wróć Escape na listę.

Oczekiwane:

- lista i wszystkie skróty działają bez dodatkowego `Alt+F4`;
- odtwarzacz zachowuje prawidłowe skoki;
- Escape wraca do wcześniejszego elementu listy.
