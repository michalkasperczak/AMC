# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-039`
- Tytuł zestawu: Skoki procentowe w odtwarzaczu
- Wersja programu: `0.1.0-alpha.39`
- Utworzono: 2026-08-17 15:43, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1543_0.1.0-alpha.39.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Otwórz plik o znanym czasie trwania i przejdź Enterem do odtwarzacza.

## AMC-039-01 — Cyfry górnego rzędu

1. Naciśnij kolejno `0`, `1`, `5` i `9`.
2. Po każdej cyfrze możesz użyć `Ctrl+Shift+E`, aby sprawdzić czas od początku.

Oczekiwane:

- `0` przechodzi na początek;
- `1` przechodzi do 10%, `5` do połowy, a `9` do 90% długości;
- przy włączonym odczycie pozycji program po każdej cyfrze podaje aktualny czas.

## AMC-039-02 — Blok numeryczny

1. Włącz Num Lock.
2. Naciśnij na bloku numerycznym `2`, `5` i `8`.

Oczekiwane:

- pozycja zmienia się odpowiednio na 20%, 50% i 80%;
- zachowanie i komunikaty są takie same jak dla cyfr górnego rzędu.

## AMC-039-03 — Ciche skoki i czas na żądanie

1. Wyłącz odczyt pozycji skrótem `Ctrl+Shift+G`.
2. Naciśnij `2`, `5` i `9`.
3. Naciśnij `Ctrl+Shift+E`.

Oczekiwane:

- cyfry nadal zmieniają pozycję, ale nie wypowiadają automatycznie czasu;
- `Ctrl+Shift+E` nadal podaje bieżącą wartość;
- `Ctrl+Shift+G` może ponownie włączyć odczyt.

## AMC-039-04 — Brak konfliktów poza odtwarzaczem

1. Wróć Escape na zwykłą listę.
2. Naciśnij cyfrę bez modyfikatora, a następnie `Ctrl+1` lub `Ctrl+2`.

Oczekiwane:

- sama cyfra nie przewija utworu i pozostaje zwykłym klawiszem nawigacji listy;
- `Ctrl+cyfra` nadal przełącza sesję;
- powrót F6 do odtwarzacza ponownie uaktywnia skoki procentowe.

## AMC-039-05 — Paleta poleceń

1. Otwórz paletę przez `Ctrl+Shift+K`.
2. Wyszukaj `50%` albo `przejdź 50`.

Oczekiwane:

- paleta pokazuje „Przejdź do 50% utworu, 5 (odtwarzacz)”;
- Enter wykonuje skok i zamyka paletę;
- analogiczne pozycje istnieją dla wszystkich wartości od 0% do 90%.

Uwaga: skok procentowy wymaga znanego czasu trwania. Dla transmisji na żywo albo źródła bez tej informacji AMC powinien podać, że funkcja jest niedostępna.
