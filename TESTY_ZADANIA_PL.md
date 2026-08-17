# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-038`
- Tytuł zestawu: Wyciszanie informacji po przewijaniu
- Wersja programu: `0.1.0-alpha.38`
- Utworzono: 2026-08-17 15:27, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1527_0.1.0-alpha.38.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Otwórz plik trwający ponad dwie minuty i przejdź Enterem do odtwarzacza.

## AMC-038-01 — Szybkie wyłączenie

1. Naciśnij `Ctrl+Shift+G`.
2. Użyj kilka razy lewo/prawo, `Shift+lewo/prawo` i `Ctrl+lewo/prawo`.
3. Sprawdź także Home i End.

Oczekiwane:

- skrót mówi raz „Odczyt pozycji po przewijaniu wyłączony”;
- wszystkie polecenia nadal zmieniają pozycję;
- po każdym przewinięciu program nie wypowiada liczby sekund ani czasu.

## AMC-038-02 — Czas na żądanie

1. Przy wyłączonym odczycie przewijaj strzałkami.
2. Naciśnij `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T`.

Oczekiwane:

- automatyczne informacje pozostają ciche;
- każde z trzech jawnych poleceń czasu nadal podaje aktualną wartość.

## AMC-038-03 — Ponowne włączenie

1. Naciśnij ponownie `Ctrl+Shift+G`.
2. Użyj strzałki w prawo.

Oczekiwane:

- skrót potwierdza włączenie odczytu;
- po przewinięciu program znowu podaje nową pozycję.

## AMC-038-04 — Ustawienia i paleta

1. Otwórz Ustawienia i kartę „Komunikaty”.
2. Sprawdź checkbox „Oznajmiaj pozycję po przewijaniu”.
3. W palecie `Ctrl+Shift+K` wyszukaj „pozycja przewijanie”.

Oczekiwane:

- checkbox istnieje, ma zrozumiałą pomoc i odpowiada stanowi skrótu;
- paleta podaje aktualny stan, działanie Entera oraz `Ctrl+Shift+G`;
- Enter w palecie przełącza opcję i potwierdza zmianę.

## AMC-038-05 — Zapamiętanie ustawienia

1. Pozostaw odczyt pozycji wyłączony i zamknij AMC.
2. Uruchom ponownie alpha.38, otwórz plik i odtwarzacz, a następnie użyj strzałki.

Oczekiwane:

- po ponownym uruchomieniu przewijanie nadal jest ciche;
- `Ctrl+Shift+E/R/T` nadal odczytuje czas;
- `Ctrl+Shift+G` może od razu przywrócić automatyczny odczyt.

Uwaga: opcja nie wycisza komunikatów odtwarzania, pauzy, błędów ani głośności. Dotyczy wyłącznie czasu automatycznie podawanego po zmianie pozycji.
