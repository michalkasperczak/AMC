# Wyniki testów AMC 0.1.0-alpha.188

## Zakres wersji

- Automatyczne rozpoznawanie może dotyczyć stacji słuchanej, stacji nagrywanych
  w tle albo obu rodzajów źródeł.
- `Shift+S` przełącza obserwowanie dla zapisanego zakresu, a ręczne `S` nadal
  rozpoznaje tylko słuchaną stację.
- Rozpoznawanie nagrania korzysta z jego istniejącego bufora i nie otwiera
  dodatkowego połączenia ze stacją.
- Historia pozostaje wspólna i otrzymuje dostępny filtr według stacji.

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Testy rdzenia: OK. Sprawdzono trwałość trzech zakresów, ich reguły oraz
  wejście do ustawienia z palety poleceń.
- Testy Windows związane z wersją: OK. Sprawdzono użytkowe etykiety pola
  zakresu, brak technicznych nazw, filtr wspólnej historii, politykę
  oznajmiania i udostępnienie bufora prywatnego nagrania bez drugiego
  połączenia. Pełny dalszy zestaw zatrzymał się dopiero na niezwiązanym teście
  katalogu roboczego nagrań w AppData; nie podnoszono dostępu podczas aktywnego
  nagrywania użytkownika.
- Publikacja samowystarczalnego programu dla `win-x64`: OK, bez uruchamiania
  aplikacji.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.188\AccessibleMediaController-0.1.0-alpha.188.exe`.
- Wersja produktu: `0.1.0-alpha.188`.
- Rozmiar programu: `165999520` bajtów.
- SHA-256: `5FAA95EC7FBBF8B0C5D8D197D13175052990C0E5B07D5BEE55E33BBDE85BBB6E`.

## Test ręczny

### AMC-188-01 — tylko słuchana stacja

Uwagi:

### AMC-188-02 — tylko stacje nagrywane w tle

Uwagi:

### AMC-188-03 — słuchana i wszystkie nagrywane stacje

Uwagi:

### AMC-188-04 — pauza nagrania i trwałość

Uwagi:

### AMC-188-05 — filtr wspólnej historii

Uwagi:

### AMC-188-06 — rozpoznawanie bez oznajmiania

Uwagi:
