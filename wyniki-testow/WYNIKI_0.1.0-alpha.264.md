# AMC 0.1.0-alpha.264 — wyniki testów

Data: 5 września 2026 r.

Wpisz u góry zauważone zachowanie. Nie trzeba dopisywać „OK” albo „błąd” przed
każdym punktem. Po dwukropku wpisuj spację.

## Zakres poprawki

- brak prefiksu przy niewybranym rozdziale;
- krótkie „Wybrany” wyłącznie przy pozycjach należących do zestawu;
- komunikat z bieżącym i następnym wybranym rozdziałem;
- dokładne logowanie początku, przejścia, końca i przerwania zestawu;
- udokumentowana zasada odtwarzania całego przedziału rozdziału przed skokiem.

## Testy automatyczne

- kompilacja Release: zaliczona, 0 błędów, 0 ostrzeżeń;
- niewybrany wiersz bez prefiksu stanu: zaliczony;
- wybrany wiersz z krótkim prefiksem „Wybrany”: zaliczony;
- wybór funkcjonalny nadal niezależny od fokusa listy: zaliczony;
- pełne testy rdzenia i Windows: zaliczone;
- analiza logu ręcznego testu alpha 263: Intro było odtwarzane około 1:12 z
  przedziału trwającego do 3:32, dlatego automatyczne przejście nie powinno było
  jeszcze nastąpić;
- samowystarczalny pakiet Windows x64: utworzony.

## Test ręczny NVDA

1. Przejdź po liście bez wybierania. Nie powinno być słychać „Niewybrany”.
2. Wybierz Spacją dwie pozycje. Tylko one mają mówić „Wybrany”.
3. Naciśnij Enter. Posłuchaj pierwszego rozdziału aż do jego rzeczywistego
   końca; AMC ma wtedy przeskoczyć do drugiego wybranego rozdziału.
4. Powtórz test, ale przed końcem użyj ręcznej nawigacji po rozdziałach. Stary
   zestaw ma zostać anulowany.
