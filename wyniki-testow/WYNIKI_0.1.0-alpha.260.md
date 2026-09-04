# AMC 0.1.0-alpha.260 — wyniki testów

Data: 4 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- pierwsza nawigacja po rozdziałach sama wykrywa ich spis;
- poprzedni rozdział pozwala kolejno cofać się przez wcześniejsze punkty;
- Spacja i `Ctrl+Spacja` przełączają pojedyncze zaznaczenie przed domyślną
  obsługą listy WPF;
- zmiana zaznaczenia ma jawny komunikat dostępnościowy dla NVDA;
- Shift ze strzałkami nadal zaznacza ciągły zakres;
- Enter odtwarza wyłącznie wybrane rozdziały i wraca do odtwarzacza.

## Testy automatyczne

- pełna kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- testy rdzenia, trwałości oraz nawigacji rozdziałów: zaliczone;
- testy okna Windows i użytkowych etykiet UI Automation: zaliczone;
- testy pozostałych sesji, odtwarzania i nagrywania: zaliczone.

## Test ręczny NVDA

1. Uruchom odcinek z rozdziałami i bez wcześniejszego otwierania listy użyj
   `Ctrl+Shift+strzałki w prawo`. AMC powinien sam znaleźć spis i przejść dalej.
2. Przejdź co najmniej do trzeciego rozdziału, po czym szybko kilka razy użyj
   `Ctrl+Shift+strzałki w lewo`. Powinno być możliwe przejście do poprzedniego
   i jeszcze wcześniejszego rozdziału.
3. Otwórz listę `Ctrl+Alt+B`. Spacją zaznacz bieżący rozdział, przejdź
   `Ctrl+strzałką` do innego i użyj `Ctrl+Spacji`. NVDA powinien podać stan i
   liczbę wybranych.
4. Odznacz jeden rozdział, zaznacz zakres Shiftem ze strzałkami i naciśnij
   Enter. Program powinien odtworzyć tylko pozycje, które pozostały wybrane.
5. Otwórz listę ponownie i anuluj Escape. Fokus powinien wrócić dokładnie do
   odtwarzacza bez potrzeby dodatkowego Escape.
