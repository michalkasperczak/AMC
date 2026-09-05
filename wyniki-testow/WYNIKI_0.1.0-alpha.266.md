# AMC 0.1.0-alpha.266 — wyniki testów

Data: 5 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- wybrane rozdziały pozostają filtrem automatycznego przechodzenia;
- ręczne przewijanie pozwala wejść w dowolne miejsce materiału;
- poprzedni i następny rozdział przechodzą po pełnym spisie;
- niewybrany rozdział uruchomiony ręcznie gra do końca;
- na jego granicy AMC wraca do najbliższego późniejszego wybranego rozdziału;
- brak późniejszego wybranego rozdziału kończy zestaw.

## Testy automatyczne

- kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- ręczny skok do wybranego rozdziału: zaliczony;
- ręczny skok do niewybranego rozdziału bez wymuszonej zmiany pozycji: zaliczony;
- znalezienie następnego wybranego rozdziału po ręcznym fragmencie: zaliczone;
- pełne testy rdzenia i Windows: zaliczone;
- natychmiastowe anulowanie oczekującego połączenia BASS: zaliczone;
- samowystarczalny pakiet Windows x64: utworzony.

## Test ręczny NVDA

1. Wybierz dwa nieprzylegające rozdziały i uruchom zestaw.
2. Przejdź ręcznie do rozdziału pomiędzy nimi i sprawdź, czy odtwarzanie nie
   wykonuje natychmiastowego skoku.
3. Pozwól niewybranemu rozdziałowi dobiec do końca; AMC powinien przejść do
   następnego wybranego.
4. Użyj Home, End, cyfr, skoku do czasu/procentu i nawigacji rozdziałami;
   wszystkie polecenia powinny obejmować pełny materiał.
5. Otwórz ponownie listę i sprawdź, czy wcześniejsze wybory nadal mówią
   „Wybrany”.
