# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-046`
- Tytuł zestawu: Regresja fokusu NVDA i bezpieczny odczyt stanu
- Wersja programu: `0.1.0-alpha.46`
- Utworzono: 2026-08-17 23:05, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2305_0.1.0-alpha.46.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-046-01 — Uruchomienie i fokus NVDA

1. Uruchom program i niczego nie naciskaj przez kilka sekund.
2. Odczytaj zaznaczony element i przejdź po liście strzałkami.
3. Wypróbuj `Ctrl+K`, `F6` i Escape.
4. Sprawdź Alt+Tab, ale nie naciskaj jeszcze `Alt+F4`.

Oczekiwane:

- fokus znajduje się na liście, a NVDA nie pozostaje na samym komunikacie „pasek stanu”;
- odczyt, strzałki i skróty działają od pierwszej chwili;
- w Alt+Tab istnieje tylko jedno okno AMC i nie ma pomocniczego okna paska.

## AMC-046-02 — NVDA+End bez blokowania aplikacji

1. Naciśnij `NVDA+End` w zwykłym rozmiarze okna.
2. Zmaksymalizuj okno i powtórz próbę.
3. Przywróć rozmiar, przesuń okno i sprawdź jeszcze raz.
4. Po każdej próbie użyj strzałki oraz jednego skrótu AMC.

Oczekiwane:

- NVDA próbuje odczytać wewnętrzny pasek i, jeśli go odnajdzie, podaje usługę, stan, tytuł, czas, głośność i przepływność;
- niezależnie od wyniku `NVDA+End` aplikacja nie blokuje się, fokus się nie przenosi, a skróty nadal działają;
- zapisz dokładnie, co mówi NVDA, jeżeli paska nadal nie odnajduje.

## AMC-046-03 — Odczytaj stan odtwarzania

1. Z menu Odtwarzanie wybierz „Odczytaj stan odtwarzania”.
2. Otwórz `Ctrl+Shift+K`, wyszukaj „stan odtwarzania” i wykonaj polecenie.
3. Powtórz podczas odtwarzania lokalnego pliku, jeśli jest załadowany.

Oczekiwane:

- polecenie podaje usługę, odtwarzanie albo pauzę, tytuł, czas, głośność i przepływność;
- fokus pozostaje w dotychczasowym miejscu;
- polecenie działa także wtedy, gdy zwykłe automatyczne komunikaty odtwarzacza są wyciszone.

## AMC-046-04 — Brak regresji skoków

1. Otwórz odtwarzacz klawiszem `F6` dla elementu o znanym czasie.
2. Sprawdź `Ctrl+J`, `Ctrl+Shift+J` oraz cyfrę `5`.
3. Wpisz wartość czasu większą niż czas trwania.

Oczekiwane:

- oba okna skoku i skok cyfrą działają wyłącznie w odtwarzaczu;
- błędna wartość jest oznajmiona, a fokus pozostaje w polu;
- usunięcie pomocniczego paska nie zmienia działania odtwarzacza.
