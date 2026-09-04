# AMC 0.1.0-alpha.265 — wyniki testów

Data: 5 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- przewijanie nie anuluje aktywnego zestawu rozdziałów;
- trafienie w pominięty fragment wraca do wybranego przedziału;
- poprzedni i następny rozdział poruszają się po wybranym zestawie;
- ponownie otwarta lista pokazuje aktywne wybory;
- zmiana materiału nadal bezpiecznie kończy plan.

## Testy automatyczne

- kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- przewijanie względne, czasowe i procentowe nie przerywa planu: zaliczone;
- poprzedni i następny rozdział nie przerywa planu: zaliczone;
- zmiana materiału nadal przerywa plan: zaliczona;
- ponowne otwarcie listy odtwarza funkcjonalny wybór i etykiety UI Automation:
  zaliczone;
- pełne testy rdzenia i Windows: zaliczone;
- samowystarczalny pakiet Windows x64: utworzony.

## Test ręczny NVDA

1. Wybierz dwa odległe rozdziały i uruchom zestaw.
2. Przewijaj pierwszy rozdział strzałkami, nie otwierając innego materiału.
3. Sprawdź, czy po jego końcu AMC przechodzi do drugiego wybranego rozdziału.
4. Użyj `Ctrl+Shift+lewo/prawo`; skróty mają przechodzić tylko po wyborze.
5. Ponownie otwórz listę. Te same rozdziały powinny mówić „Wybrany”.
