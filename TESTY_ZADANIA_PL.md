# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-035`
- Tytuł zestawu: Transport lokalny bez prefiksu
- Wersja programu: `0.1.0-alpha.35`
- Utworzono: 2026-08-17 13:35, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1335_0.1.0-alpha.35.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Otwórz przez `Ctrl+O` plik trwający co najmniej dwie minuty i uruchom go Enterem. Wszystkie skróty w tym zestawie naciskaj z fokusem na głównej liście, bez globalnego prefiksu.

## AMC-035-01 — Informacje o czasie

1. Naciśnij `Ctrl+E`, `Ctrl+R` i `Ctrl+T`.
2. Po kilku sekundach ponownie naciśnij `Ctrl+E`.

Oczekiwane:

- NVDA podaje czas od początku, pozostały i całkowity;
- drugi czas od początku jest późniejszy od pierwszego;
- żaden skrót nie zatrzymuje dźwięku ani nie przenosi fokusu.

## AMC-035-02 — Przewijanie strzałkami

1. Naciśnij strzałkę w prawo i sprawdź `Ctrl+E`.
2. Naciśnij strzałkę w lewo i ponownie sprawdź czas.
3. Naciśnij `Shift+strzałka w prawo`, a następnie `Shift+strzałka w lewo`.

Oczekiwane:

- zwykłe strzałki zmieniają pozycję o około 10 sekund;
- warianty z Shiftem zmieniają pozycję o około minutę;
- program podaje nową pozycję, a zaznaczenie pozostaje na tym samym pliku.

## AMC-035-03 — Lista i głośność

1. Jeśli lista zawiera kilka plików, użyj zwykłych strzałek w górę i w dół.
2. Naciśnij `Ctrl+strzałka w górę`, a następnie `Ctrl+strzałka w dół`.
3. Sprawdź także `Ctrl+Shift+strzałka w górę` i `Ctrl+Shift+strzałka w dół`.

Oczekiwane:

- góra/dół bez modyfikatorów nadal zmieniają zaznaczony plik;
- warianty z Ctrl zmieniają głośność o 5%, a z Ctrl+Shift o 1%;
- głośność NVDA nie jest zmieniana.

## AMC-035-04 — Początek, okolice końca i paleta

1. Naciśnij `Ctrl+Home`, a potem `Ctrl+E`.
2. Naciśnij `Ctrl+End`, a potem ponownie `Ctrl+E`.
3. W palecie `Ctrl+Shift+K` wyszukaj polecenia czasu, przewijania oraz głośności.

Oczekiwane:

- `Ctrl+Home` przechodzi do `0:00`;
- `Ctrl+End` przechodzi około 10 sekund przed końcem, nie uruchamiając od razu następnego pliku;
- paleta podaje działające skróty okna;
- „Otwórz w oficjalnej aplikacji” nie podaje już zajętego `Ctrl+Shift+O`.

Uwaga: „Skocz do miejsca” oraz osobne wyciszanie komunikatów transportowych są zapisane jako następne funkcje. Najpierw ten test ma potwierdzić niezawodność podstawowych poleceń czasu i przewijania.
