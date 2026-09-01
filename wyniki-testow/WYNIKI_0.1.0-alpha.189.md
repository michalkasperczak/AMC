# Wyniki testów AMC 0.1.0-alpha.189

## Przyczyna poprawki

Log po awarii wykazał `OverflowException` w oknie zmiany skrótu. Hak okna
otrzymywał wszystkie komunikaty Windows i przed sprawdzeniem ich rodzaju
próbował zamienić parametr wskaźnikowy na 32-bitowy kod klawisza. Komunikat
fokusu po przechwyceniu Plusa numerycznego miał większą wartość i zakończył
wątek interfejsu. Działający w tym samym procesie globalny hak klawiatury mógł
wtedy zatrzymać przekazywanie wejścia do NVDA.

## Weryfikacja automatyczna

- Kompilacja Release: OK, 0 ostrzeżeń i 0 błędów.
- Test regresji komunikatu 64-bitowego oraz Plusa numerycznego: OK.
- Testy rdzenia: OK.
- Testy Windows do testu publikacji nagrania: OK, w tym cały nowy test
  przechwytywania. Dalszy zestaw zatrzymał się na istniejącym ograniczeniu
  dostępu środowiska testowego do katalogu `recording-staging` w AppData; nie
  jest to błąd tej poprawki.
- Aplikacji nie uruchamiano automatycznie po kompilacji.

## Pakiet

- Program: `publish\AccessibleMediaController-0.1.0-alpha.189\AccessibleMediaController-0.1.0-alpha.189.exe`.
- Wersja produktu: `0.1.0-alpha.189`.
- Rozmiar programu: `165999520` bajtów.
- SHA-256: `517D9D46A4FD3E259CF110A66D451CE8FF719A715A6C654AD671E37DA0AE52ED`.

## Test ręczny

### AMC-189-01 — przechwycenie Plusa numerycznego

Uwagi:

### AMC-189-02 — zapis i użycie Plusa

Uwagi:

### AMC-189-03 — powtarzanie i anulowanie

Uwagi:
