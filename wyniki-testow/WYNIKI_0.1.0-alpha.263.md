# AMC 0.1.0-alpha.263 — wyniki testów

Data: 5 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- skrócone stany rozdziałów: „Wybrany” i „Niewybrany”;
- brak automatycznego wyboru początkowego rozdziału;
- Enter bez ręcznego wyboru uruchamia wiersz pod fokusem;
- Enter po użyciu Spacji uruchamia jawnie wybrany zestaw;
- zatwierdzone rozdziały są zapisywane w logu diagnostycznym.

## Testy automatyczne

- kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- początkowy fokus bez automatycznego wyboru: zaliczony;
- Enter bez jawnego zestawu wybiera rozdział pod fokusem: zaliczony;
- wybór pojedynczy i zakresowy pozostaje niezależny od fokusa: zaliczony;
- krótkie etykiety obu stanów UI Automation: zaliczone;
- pełne testy rdzenia i Windows: zaliczone;
- samowystarczalny pakiet Windows x64: utworzony.

## Test ręczny NVDA

1. Otwórz listę i przejdź strzałkami przez kilka rozdziałów. Komunikaty mają
   zaczynać się od krótkiego „Niewybrany”.
2. Bez użycia Spacji ustaw fokus na wybranym rozdziale i naciśnij Enter. AMC ma
   przejść dokładnie do tego rozdziału.
3. Ponownie otwórz listę, wybierz dwa rozdziały Spacją i naciśnij Enter. AMC ma
   odtworzyć tylko wybrany zestaw.
4. Ponownie otwórz listę po skoku i upewnij się, że fokus odpowiada bieżącemu
   czasowi, ale żaden wiersz nie jest automatycznie oznaczony jako „Wybrany”.
