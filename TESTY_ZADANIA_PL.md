# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-048`
- Tytuł zestawu: Pasek bez głośności i komunikaty bez nazwy technicznej
- Wersja programu: `0.1.0-alpha.48`
- Utworzono: 2026-08-17 23:36, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2336_0.1.0-alpha.48.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-048-01 — Pasek bez głośności

1. Odtwórz lokalny plik i naciśnij `NVDA+End`.
2. Powtórz podczas pauzy oraz po zmianie pozycji.

Oczekiwane:

- pasek mówi jeden raz: przepływność, stan, pozycję z czasem całkowitym, tytuł i usługę;
- pasek nie podaje głośności;
- fokus pozostaje na wcześniejszej kontrolce.

## AMC-048-02 — Regulacja głośności bez „Stan programu”

1. W odtwarzaczu kilka razy zmień głośność strzałkami góra i dół.
2. Powtórz z wyłączonymi automatycznymi komunikatami przez `Ctrl+Shift+G`.
3. Włącz komunikaty ponownie.

Oczekiwane:

- przy włączonych komunikatach NVDA mówi właściwą wartość głośności tylko raz;
- przy wyłączonych komunikatach zmiana jest cicha;
- w żadnym wariancie nie pojawia się „Stan programu”.

## AMC-048-03 — Powrót z okna skoku i palety

1. W odtwarzaczu otwórz `Ctrl+J`, wpisz poprawny czas i zatwierdź.
2. Otwórz `Ctrl+Shift+J`, wpisz nieprawidłowy procent, a następnie popraw go i zatwierdź.
3. Otwórz paletę `Ctrl+Shift+K`, wyszukaj polecenie skoku i wykonaj je.
4. Zamknij kolejne okno Escape i kontynuuj nawigację.

Oczekiwane:

- odczytywany jest właściwy czas, procent albo konkretny błąd;
- po powrocie fokus trafia do oczekiwanego miejsca;
- nie pojawia się sam komunikat „Stan programu”.

## AMC-048-04 — Jawny pełny odczyt stanu

1. Z menu Odtwarzanie wybierz „Odczytaj stan odtwarzania”.
2. Powtórz polecenie z palety.

Oczekiwane:

- jawne polecenie podaje pełny stan, łącznie z głośnością;
- komunikat jest jeden i zawiera rzeczywiste dane;
- fokus nie zmienia miejsca.
