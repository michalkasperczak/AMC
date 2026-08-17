# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-042`
- Tytuł zestawu: Klasyczny pasek stanu i dokładne skoki
- Wersja programu: `0.1.0-alpha.42`
- Utworzono: 2026-08-17 19:49, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1949_0.1.0-alpha.42.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-042-01 — Pasek stanu w sesji demonstracyjnej

1. Pozostań w głównym oknie na sesji TIDAL, Apple Music albo WiiM.
2. Naciśnij `NVDA+End`.
3. Zmień pozycję lub głośność i ponownie naciśnij `NVDA+End`.

Oczekiwane:

- NVDA odnajduje klasyczny pasek stanu;
- czyta usługę, stan, tytuł, pozycję i czas całkowity, głośność oraz „przepływność brak danych”;
- pasek nie mówi sam podczas zwykłej pracy.

## AMC-042-02 — Pasek stanu pliku lokalnego

1. Otwórz lokalny plik audio i zacznij go odtwarzać.
2. Poczekaj kilka sekund na odczytanie czasu trwania.
3. Naciśnij `NVDA+End`.

Oczekiwane:

- NVDA czyta „Lokalne multimedia”, tytuł, bieżący i całkowity czas oraz głośność;
- przepływność jest podana jako wartość przybliżona w kb/s;
- ponowne `NVDA+End` podaje zaktualizowaną pozycję.

## AMC-042-03 — Skocz do czasu przez Ctrl+G

1. Na elemencie z czasem trwania naciśnij `Ctrl+G`.
2. Sprawdź kolejno wartości `1:35`, `0` oraz — dla odpowiednio długiego pliku — `35`.
3. Po każdej wartości zatwierdź Enterem i sprawdź czas przez `Ctrl+Shift+E`.

Oczekiwane:

- fokus od razu znajduje się w polu „Czas docelowy”;
- `1:35` oznacza minutę i 35 sekund;
- `0` oznacza początek;
- sama liczba `35` oznacza 35 minut, a nie 35 sekund ani 35 procent;
- po zatwierdzeniu program krótko podaje osiągnięty czas.

## AMC-042-04 — Błędy czasu i powrót fokusu

1. Otwórz `Ctrl+G` i wpisz `1:60` albo czas dłuższy od całego pliku.
2. Zatwierdź Enterem.
3. Popraw wartość i zatwierdź ponownie; innym razem zamknij okno Escape.

Oczekiwane:

- błąd jest jednoznacznie oznajmiony, a fokus pozostaje w zaznaczonym polu tekstowym;
- błędna wartość nie zmienia pozycji;
- poprawna wartość zamyka okno;
- Escape anuluje bez przewijania i przywraca poprzedni fokus.

## AMC-042-05 — Skocz do procentu

1. Wybierz **Odtwarzanie → Skocz do procentu** albo wyszukaj to polecenie w palecie `Ctrl+Shift+K`.
2. Sprawdź wartości `35`, `100%` oraz błędną `101`.

Oczekiwane:

- `35` przechodzi dokładnie do 35% czasu i podaje procent oraz obliczony czas;
- opcjonalny znak `%` jest przyjmowany;
- `100%` przechodzi na koniec;
- `101` pozostawia okno otwarte, oznajmia zakres `0–100` i nie przewija.

Uwaga: przy nieznanym czasie trwania oba dokładne skoki są niedostępne i program podaje przyczynę zamiast zgadywać.
