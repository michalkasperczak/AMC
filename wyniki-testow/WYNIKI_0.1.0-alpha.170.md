# Wyniki testów AMC 0.1.0-alpha.170

## Weryfikacja automatyczna

- Kompilacja Release: zakończona, 0 błędów i 0 ostrzeżeń.
- Testy rdzenia: wszystkie zakończone powodzeniem, w tym dziedziczenie ustawień
  dźwięku, trwałość nowych pól w SQLite oraz rozróżnienie pobranego pliku
  iCloud od pliku tylko online.
- Testy Windows: wszystkie zakończone powodzeniem, w tym nadzór dekodera,
  dekoder awaryjny MP3, normalizacja, przejścia, nagrywanie i dostępne etykiety.

## Uwagi z diagnozy logu 31 sierpnia 2026

- Po zamknięciu pomocy klawiatury Media Foundation trzykrotnie odrzucił
  ustawienie pozycji przy wznowieniu Gdańska. Ten przypadek ma teraz
  automatyczne odrzucenie uszkodzonego toru i ponowne otwarcie w zachowanej
  pozycji.
- Łódź została w badanym przebiegu zmieniona ręcznie przed końcem, więc log nie
  potwierdza jej błędu końca.
- Rzeszów nie zgłosił ani końca, ani błędu przez prawie pięć minut. Nowy nadzór
  sprawdza również brak przesuwania pozycji, nie tylko zablokowane wywołanie
  odczytu.
- Istniejące pliki Gdańska i PIK były przypięte i pobrane lokalnie, mimo że
  poprzednia wersja oznaczała całą ścieżkę iCloud jako zdalną.

## Test ręczny

Wpisz tutaj zauważone zachowanie dla zadań AMC-170-01–AMC-170-06. Nie trzeba
przy każdym zadaniu dopisywać osobno „OK” albo „błąd”. Po dwukropku wpisuj
spację.

### AMC-170-01

Uwagi:

### AMC-170-02

Uwagi:

### AMC-170-03

Uwagi:

### AMC-170-04

Uwagi:

### AMC-170-05

Uwagi:

### AMC-170-06

Uwagi:
