# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-058`
- Tytuł zestawu: Jedna instancja, OGG i sterowanie odtwarzaczem
- Wersja programu: `0.1.0-alpha.58`
- Utworzono: 2026-08-19, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.58.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-058-01 — Ponowne uruchomienie AMC

1. Uruchom AMC i pozostaw otwarte główne okno.
2. Uruchom ten sam plik EXE drugi raz.
3. Otwórz Ustawienia, pozostaw je otwarte i jeszcze raz uruchom EXE.

Oczekiwane:

- nie powstaje drugie niezależne okno AMC;
- przy pierwszej próbie wraca istniejące główne okno;
- przy drugiej próbie wraca istniejące okno Ustawień, a fokus nie przechodzi pod dialog;
- odtwarzanie i zapisany stan nie są przerywane.

## AMC-058-02 — Tytuł: element, moduł, sesja

1. Przejdź w Lokalnych multimediach do Biblioteki, Ulubionych, Kolejki i odtwarzacza.
2. W każdym miejscu odczytaj tytuł głównego okna.
3. Przesuń fokus na inny plik bez rozpoczynania jego odtwarzania.

Oczekiwane:

- tytuł ma kolejność „aktualny element, moduł, sesja, AMC i wersja”;
- przesuwanie fokusu po liście nie podmienia aktualnego elementu w tytule.

## AMC-058-03 — OGG/Vorbis

1. Otwórz plik `Nextfest pr3_2026-04-18_17-00.ogg`.
2. Uruchom go, przewiń, zmień prędkość i odczytaj pasek stanu.

Oczekiwane:

- plik odtwarza się i ma czas około `1:00:10`;
- pasek podaje `48 kHz`, przewijanie i tempo działają;
- nie pojawia się komunikat o nieobsługiwanym strumieniu Media Foundation.

## AMC-058-04 — Poprzedni i następny utwór

1. Otwórz odtwarzacz na środkowym pliku lokalnej listy.
2. Naciśnij `Page Down`, a następnie `Page Up`.
3. Spróbuj `Page Up` na pierwszym i `Page Down` na ostatnim elemencie.
4. Sprawdź przyciski oraz menu kontekstowe odtwarzacza.

Oczekiwane:

- `Page Up` uruchamia poprzedni, a `Page Down` następny plik;
- początek i koniec listy nie zapętlają się;
- przyciski i menu mają te same działania oraz czytelne skróty.

## AMC-058-05 — Niezdefiniowane kombinacje odtwarzacza

W odtwarzaczu sprawdź `Ctrl+Down`, `Ctrl+W`, `Ctrl+Shift+Left`, `Ctrl+Shift+Right` oraz `Shift+Page Up/Down`.

Oczekiwane:

- fokus nie przeskakuje do „Wróć do listy”, nagłówka „Odtwarzacz” ani przycisku przewijania;
- te jeszcze nieprzypisane kombinacje nie wykonują przypadkowego działania i nie ogłaszają mylącego przycisku.

## AMC-058-06 — Trwałość pozycji

1. Uruchom `Tyfloprzegląd 20260811 — kopia.mp3` i skocz do wyraźnie rozpoznawalnego czasu.
2. Wstrzymaj, przełącz się do innego okna, zamknij AMC i uruchom je ponownie.
3. Ponownie uruchom ten sam plik.

Oczekiwane:

- plik wznawia się od zapisanego miejsca;
- AMC nie zaczyna odtwarzania samoczynnie przy starcie;
- zapis działa także wtedy, gdy od pauzy do zamknięcia minęło mniej niż 15 sekund.

## AMC-058-07 — Ustawienia i liczba okien

1. Otwórz kolejno Wyszukiwanie, Paletę poleceń, Właściwości i Ustawienia.
2. Sprawdź przełączanie `Alt+Tab` i zachowanie Escape lub Anuluj.
3. W Ustawieniach przejdź Tabem na checkbox i włącz lub wyłącz go spacją; następnie zapisz Enterem.

Oczekiwane:

- tylko główne AMC ma osobny przycisk na pasku zadań;
- każdy dialog zatrzymuje fokus do zakończenia krótkiego zadania i wraca do właściwego miejsca;
- Tab podaje stan checkboxa, spacja go zmienia, Enter zapisuje cały dialog z dowolnej kontrolki, a Escape anuluje.

## AMC-058-08 — Podstawowa regresja

Sprawdź wyrywkowo listy, zaznaczanie wielu elementów, Ulubione, Bibliotekę, Kolejkę, filtr, wyszukiwanie, `Alt+Enter`, kopiowanie ścieżki, pasek stanu, zmianę głośności i naturalne przejście do kolejnego pliku.

Oczekiwane: funkcje wersji 57 nadal działają, a głośność AMC nie zmienia głośności NVDA ani systemu.
