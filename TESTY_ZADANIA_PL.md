# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-044`
- Tytuł zestawu: Punkt zgodności NVDA+End
- Wersja programu: `0.1.0-alpha.44`
- Utworzono: 2026-08-17 22:00, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2200_0.1.0-alpha.44.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-044-01 — NVDA+End w różnych rozmiarach okna

1. W głównym oknie naciśnij `NVDA+End` przy zwykłym rozmiarze okna.
2. Zmaksymalizuj okno i ponownie naciśnij `NVDA+End`.
3. Przywróć rozmiar, przesuń okno i wykonaj próbę po raz trzeci.

Oczekiwane:

- w każdym położeniu NVDA odnajduje pasek stanu;
- odczytuje usługę, stan, tytuł, czas, głośność i przepływność;
- przesuwanie i maksymalizacja nie powodują dodatkowych komunikatów ani utraty fokusu.

## AMC-044-02 — Aktualizacja i brak ingerencji punktu zgodności

1. Otwórz lokalny plik, rozpocznij odtwarzanie i naciśnij `NVDA+End`.
2. Odczekaj kilka sekund, zmień głośność i ponownie odczytaj pasek.
3. Przejdź Tab i Shift+Tab przez główne kontrolki oraz użyj Alt+Tab.

Oczekiwane:

- ponowny odczyt zawiera nowszy czas i głośność;
- punkt zgodności nie otrzymuje fokusu i nie pojawia się w kolejności Tab;
- Alt+Tab pokazuje tylko główne okno AMC, bez osobnego okna paska.

## AMC-044-03 — Oznajmienie błędu skoku

1. Naciśnij `Ctrl+G`, wpisz `1:60` i zatwierdź.
2. Popraw zaznaczoną wartość i zatwierdź ponownie.

Oczekiwane:

- NVDA od razu mówi przyczynę błędu;
- fokus pozostaje w polu, a błędna wartość jest zaznaczona;
- poprawna wartość wykonuje skok.
