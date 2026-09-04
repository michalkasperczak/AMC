# AMC 0.1.0-alpha.262 — wyniki testów

Data: 4 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- fokus listy i wybór rozdziałów do odtwarzania są niezależne;
- zwykłe strzałki nie zmieniają zestawu wybranych rozdziałów;
- Spacja i `Ctrl+Spacja` przełączają pojedynczy wybór;
- Shift ze strzałkami dodaje zakres do wyboru;
- NVDA podaje „Wybrany do odtwarzania” albo „Niewybrany do odtwarzania”.

## Testy automatyczne

- kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- zmiana bieżącego wiersza bez zmiany funkcjonalnego wyboru: zaliczona;
- zaznaczanie pojedyncze i zakresowe: zaliczone;
- aktualizacja obu etykiet UI Automation: zaliczona;
- pełne testy Windows: zaliczone.

## Test ręczny NVDA

1. Otwórz `Ctrl+Alt+B` i przejdź zwykłymi strzałkami przez kilka pozycji. Nie
   powinny wszystkie mówić „Wybrany do odtwarzania”.
2. Wybierz dwie nieprzylegające pozycje Spacją. Odejście i powrót strzałkami
   powinno zachować oraz prawidłowo odczytać oba wybory.
3. Odznacz jedną pozycję ponowną Spacją. Powinna mówić „Niewybrany do
   odtwarzania”.
4. Dodaj kilka sąsiednich pozycji Shiftem ze strzałkami i naciśnij Enter.
   Odtwarzane mają być wyłącznie rozdziały pozostawione w wyborze.
