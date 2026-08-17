# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-036`
- Tytuł zestawu: Dostępny odtwarzacz w głównym oknie
- Wersja programu: `0.1.0-alpha.36`
- Utworzono: 2026-08-17 13:49, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_1349_0.1.0-alpha.36.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

Do prób transportu otwórz przez `Ctrl+O` plik trwający co najmniej dwie minuty. Jeżeli to możliwe, dodaj także drugi plik, aby sprawdzić zwykłą nawigację po liście.

## AMC-036-01 — Numer wersji i otwarcie odtwarzacza

1. Odczytaj tytuł głównego okna.
2. Na wybranym pliku naciśnij Enter.

Oczekiwane:

- tytuł zawiera pełny numer `AMC 0.1.0-alpha.36`;
- dźwięk zostaje uruchomiony, a w tym samym oknie pojawia się odtwarzacz;
- NVDA podaje odtwarzacz, tytuł, sesję i stan bez otwierania drugiego okna.

## AMC-036-02 — Sterowanie w odtwarzaczu

1. Naciśnij prawo, lewo, `Shift+prawo` i `Shift+lewo`.
2. Sprawdź głośność przez góra/dół oraz `Shift+góra/dół`.
3. Sprawdź Home, End oraz `Ctrl+E`, `Ctrl+R` i `Ctrl+T`.

Oczekiwane:

- lewo/prawo przewija o około 10 sekund, a wariant z Shiftem o około minutę;
- góra/dół zmienia głośność o 5%, a wariant z Shiftem o 1%;
- Home przechodzi na początek, a End około 10 sekund przed końcem;
- polecenia czasu podają właściwe wartości, a upływ każdej sekundy nie jest automatycznie wypowiadany.

## AMC-036-03 — Przyciski i odtwarzanie

1. Przejdź Tabem przez przyciski odtwarzacza.
2. Użyj przycisku „Odtwórz lub wstrzymaj”, jednego przycisku przewijania i jednego przycisku głośności.

Oczekiwane:

- każdy przycisk ma krótką, jednoznaczną nazwę;
- działania przycisków i odpowiadających im skrótów dotyczą tego samego odtwarzania;
- fokus pozostaje w odtwarzaczu.

## AMC-036-04 — Escape i zwykła lista

1. W odtwarzaczu naciśnij Escape.
2. Sprawdź zaznaczenie oraz dźwięk.
3. Użyj wszystkich strzałek na zwykłej liście.

Oczekiwane:

- Escape wraca dokładnie do wcześniejszego pliku i widoku, a odtwarzanie trwa dalej;
- strzałki na liście zachowują naturalne działanie listy i nie przewijają nagrania ani nie zmieniają głośności.

## AMC-036-05 — Ctrl+Enter i F6

1. Na liście naciśnij `Ctrl+Enter` i sprawdź, czy fokus pozostał na liście.
2. Naciśnij F6, a następnie Escape.
3. Opcjonalnie otwórz odtwarzacz poleceniem „Teraz odtwarzane” z palety albo klawiszem `N` po prefiksie.

Oczekiwane:

- `Ctrl+Enter` przełącza odtwarzanie zaznaczenia bez otwierania odtwarzacza;
- F6 otwiera odtwarzacz bez zmiany zaznaczonego elementu;
- Escape ponownie przywraca tę samą listę i pozycję.

## AMC-036-06 — Powrót przyciskiem

1. Otwórz odtwarzacz F6.
2. Tabem przejdź do przycisku „Wróć do listy” i go użyj.

Oczekiwane:

- przycisk działa tak jak Escape;
- fokus wraca do zapamiętanego elementu;
- tytuł okna nadal zawiera numer wersji.

Uwaga: odtwarzacz jest częścią głównego okna, nie osobnym oknem modalnym. Dodatkowe funkcje zależne od rodzaju źródła, na przykład nagrywanie radia, pozostają na późniejszy etap.
