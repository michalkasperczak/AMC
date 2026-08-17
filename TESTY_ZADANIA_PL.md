# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-041`
- Tytuł zestawu: Osobne komunikaty i pasek stanu
- Wersja programu: `0.1.0-alpha.41`
- Utworzono: 2026-08-17 18:44, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1844_0.1.0-alpha.41.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-041-01 — Procenty bez komunikatów strzałek

1. W Ustawieniach → Komunikaty włącz nadrzędne komunikaty odtwarzacza.
2. Zaznacz „Oznajmiaj skoki cyframi”, a odznacz „Oznajmiaj przewijanie strzałkami, Home i End”.
3. W odtwarzaczu naciśnij `2`, `5`, strzałkę w prawo, `Shift+lewo`, Home i End.

Oczekiwane:

- cyfry mówią `20%` i `50%` albo wariant wybrany na liście;
- strzałki, Home i End zmieniają pozycję bez automatycznej wypowiedzi;
- `Ctrl+Shift+E` nadal podaje aktualny czas.

## AMC-041-02 — Głośność oraz odtwarzanie i pauza

1. Osobno włącz i wyłącz „Oznajmiaj zmiany głośności”. Sprawdź górę/dół.
2. Osobno włącz i wyłącz „Oznajmiaj odtwarzanie i pauzę”. Sprawdź Spację.

Oczekiwane:

- każda grupa może mówić albo pozostać cicha niezależnie od procentów i strzałek;
- wyłączenie komunikatu nie blokuje zmiany głośności, odtwarzania ani pauzy.

## AMC-041-03 — Nadrzędne wyciszenie zachowuje wybór

1. Pozostaw komunikaty cyfr włączone, a strzałek wyłączone.
2. Naciśnij `Ctrl+Shift+G`, sprawdź cyfrę, a następnie naciśnij `Ctrl+Shift+G` ponownie.
3. Ponownie sprawdź cyfrę i strzałkę.

Oczekiwane:

- po pierwszym skrócie wszystkie cztery automatyczne kategorie są chwilowo ciche;
- po drugim cyfra znowu mówi, a strzałka nadal milczy;
- skrót nie zaznacza ponownie wyłączonych wcześniej kategorii.

## AMC-041-04 — Pasek stanu i NVDA+End

1. Pozostań w głównym oknie na sesji demonstracyjnej.
2. Naciśnij `NVDA+End`, następnie zmień pozycję lub głośność i ponownie naciśnij `NVDA+End`.

Oczekiwane:

- NVDA odczytuje usługę, stan, tytuł, pozycję i czas całkowity, głośność oraz przepływność;
- dla demonstracji przepływność może być „brak danych”;
- pasek aktualizuje wartości, ale nie mówi sam co sekundę.

## AMC-041-05 — Plik lokalny, przepływność i paleta

1. Otwórz lokalny plik audio, uruchom go i po chwili naciśnij `NVDA+End`.
2. Otwórz paletę `Ctrl+Shift+K` i wyszukaj kolejno „skoki cyframi”, „strzałkami”, „głośności” oraz „pauzy”.

Oczekiwane:

- pasek podaje czas pliku i „przepływność około … kb/s”; gdy system nie zna jeszcze czasu, uczciwie mówi „brak danych”;
- paleta pokazuje stan każdej kategorii i Enter ustawia fokus na odpowiednim checkboxie karty Komunikaty.

Uwaga: komunikaty błędów i niedostępności nie należą do czterech automatycznych kategorii i pozostają słyszalne.
