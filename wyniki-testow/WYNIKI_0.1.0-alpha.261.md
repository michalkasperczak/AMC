# AMC 0.1.0-alpha.261 — wyniki testów

Data: 4 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- „Zaznaczony” albo „Niezaznaczony” jest czytane przed treścią każdego
  rozdziału;
- etykieta aktualizuje się po Spacji, `Ctrl+Spacji` i zaznaczeniu zakresu;
- `Ctrl+strzałki` pozwalają sprawdzić wybór bez jego zmiany;
- mechanika nawigacji i wybiórczego odtwarzania z `alpha.260` pozostaje bez
  zmian.

## Testy automatyczne

- pełna kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- test zaznaczonego i niezaznaczonego kontenera UI Automation: zaliczony;
- test aktualizacji etykiety po zmianie wyboru: zaliczony;
- pełne testy rdzenia i Windows: zaliczone.

## Test ręczny NVDA

1. Otwórz listę rozdziałów przez `Ctrl+Alt+B`.
2. Zaznacz kilka nieprzylegających pozycji Spacją lub `Ctrl+Spacją`.
3. Przechodź po całej liście `Ctrl+strzałką w górę/dół`. Każdy wiersz powinien
   rozpoczynać się od „Zaznaczony” albo „Niezaznaczony”.
4. Odznacz jedną pozycję, odejdź i wróć. Powinna już mówić „Niezaznaczony”.
5. Naciśnij Enter i sprawdź, czy odtwarzane są wyłącznie pozycje, które
   pozostały zaznaczone.
