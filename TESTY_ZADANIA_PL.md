# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-043`
- Tytuł zestawu: Natywny pasek stanu i globalne skoki
- Wersja programu: `0.1.0-alpha.43`
- Utworzono: 2026-08-17 21:48, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2148_0.1.0-alpha.43.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-043-01 — Natywny pasek stanu

1. W głównym oknie pozostaw zaznaczenie na zwykłej liście.
2. Naciśnij `NVDA+End` w sesji demonstracyjnej.
3. Otwórz lokalny plik, zacznij odtwarzanie i ponownie naciśnij `NVDA+End`.

Oczekiwane:

- NVDA odnajduje pasek stanu w obu przypadkach;
- czyta usługę, stan, tytuł, pozycję, czas całkowity, głośność i przepływność;
- lokalny pasek aktualizuje czas, a dla źródła bez danych mówi „przepływność brak danych”.

## AMC-043-02 — Błąd wpisanego czasu i procentu

1. Naciśnij `Ctrl+G`, wpisz `1:60` i zatwierdź.
2. Otwórz „Skocz do procentu”, wpisz `101` i zatwierdź.

Oczekiwane:

- każda przyczyna błędu jest od razu oznajmiona;
- fokus pozostaje w polu, a błędna wartość jest zaznaczona do zastąpienia;
- po wpisaniu poprawnej wartości skok działa.

## AMC-043-03 — Skoki podczas pracy na liście

1. Uruchom lokalny plik i wróć Escape na listę.
2. Nie otwierając odtwarzacza, użyj `Ctrl+G` i wpisz poprawny czas.
3. Nadal na liście otwórz paletę `Ctrl+Shift+K`, wyszukaj „Skocz do procentu” i wpisz `50`.
4. Naciśnij na liście zwykłą cyfrę bez modyfikatora.

Oczekiwane:

- oba dokładne skoki sterują aktualnie odtwarzanym plikiem bez otwierania odtwarzacza;
- cyfra na zwykłej liście nie przewija utworu;
- po wejściu do odtwarzacza cyfry `0–9` nadal wykonują szybkie skoki `0–90%`.
