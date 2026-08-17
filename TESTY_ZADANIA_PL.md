# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-040`
- Tytuł zestawu: Komunikaty czasu, procentu i głośności
- Wersja programu: `0.1.0-alpha.40`
- Utworzono: 2026-08-17 16:03, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1603_0.1.0-alpha.40.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Otwórz plik o znanym czasie trwania i przejdź Enterem do odtwarzacza.

## AMC-040-01 — Domyślny komunikat procentowy

1. Upewnij się, że automatyczne komunikaty są włączone skrótem `Ctrl+Shift+G`.
2. Naciśnij kolejno `2`, `5` i `9`.

Oczekiwane:

- program mówi tylko `20%`, `50%` i `90%`;
- nie dopowiada automatycznie czasu;
- `Ctrl+Shift+E` nadal podaje dokładną bieżącą pozycję.

## AMC-040-02 — Trzy warianty w Ustawieniach

1. Otwórz Ustawienia, kartę „Komunikaty”.
2. Znajdź listę „Po skoku cyfrą oznajmiaj”.
3. Sprawdź kolejno „Tylko czas” oraz „Procent i czas”, zapisując ustawienia i używając cyfry w odtwarzaczu.

Oczekiwane:

- „Tylko czas” podaje np. `2:10` bez procentu;
- „Procent i czas” podaje np. `50%, 2:10`;
- wybrany wariant zostaje zapamiętany po ponownym uruchomieniu programu.

## AMC-040-03 — Wspólne wyciszenie czasu i głośności

1. Naciśnij `Ctrl+Shift+G`, aby wyłączyć automatyczne komunikaty.
2. Użyj cyfr, strzałek lewo/prawo oraz góra/dół i Shift+góra/dół.
3. Sprawdź pozycję przez `Ctrl+Shift+E`.

Oczekiwane:

- pozycja i głośność nadal się zmieniają;
- program nie wypowiada automatycznie czasu, procentu skoku ani wartości głośności;
- `Ctrl+Shift+E` nadal odpowiada.

## AMC-040-04 — Komunikaty pozostające aktywne

1. Pozostaw automatyczne komunikaty wyłączone.
2. Użyj Spacji dwa razy.

Oczekiwane:

- nadal słychać „Pauza” oraz „Odtwarzanie” z tytułem;
- wyciszenie czasu i głośności nie wyłącza ważnych komunikatów stanu.

## AMC-040-05 — Paleta i fokus ustawienia

1. Otwórz paletę przez `Ctrl+Shift+K`.
2. Wyszukaj „czas głośność” i sprawdź przełącznik.
3. Ponownie otwórz paletę, wyszukaj „skoku cyfrą” i naciśnij Enter.

Oczekiwane:

- przełącznik podaje bieżący stan oraz `Ctrl+Shift+G`;
- polecenie skoku cyfrą podaje wybrany wariant;
- Enter otwiera kartę „Komunikaty” z fokusem na liście trzech wariantów.
