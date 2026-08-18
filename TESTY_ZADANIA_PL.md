# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-052`
- Tytuł zestawu: Kolejka, ciągłe odtwarzanie, wiele elementów i izolacja głośności
- Wersja programu: `0.1.0-alpha.52`
- Utworzono: 2026-08-18 21:30, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-18_2130_0.1.0-alpha.52.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-052-01 — Jednoznaczne usuwanie z Kolejki

1. Na liście Multimedia zaznacz plik i naciśnij `Ctrl+Shift+Enter`, aby ustawić „Odtwórz jako następne”.
2. Otwórz Kolejkę przez `Ctrl+Q`, ustaw fokus na tym pliku i naciśnij `Shift+Enter`.
3. Wróć do Multimedia, dodaj plik zwykłym `Shift+Enter`, ponownie otwórz Kolejkę i usuń go klawiszem Delete.
4. Przechodź między ostatnimi widokami przez `Alt+strzałka w lewo/prawo` i powtórz dodanie oraz usunięcie.

Oczekiwane:

- element ustawiony jako następny daje się usunąć z Kolejki od razu;
- program mówi „Usunięto z kolejki”, a nie „Dodano do kolejki”;
- po usunięciu element nie wraca przez historię widoków;
- menu kontekstowe pokazuje czynność zgodną z rzeczywistym stanem.

## AMC-052-02 — Zaznaczanie wielu elementów

1. Na liście co najmniej trzech plików ustaw fokus na pierwszym.
2. Przytrzymaj Shift i dwa razy naciśnij strzałkę w dół, następnie raz strzałkę w górę.
3. Na dwóch zaznaczonych elementach użyj `Shift+Enter`, `Ctrl+Shift+U` oraz `Ctrl+Shift+L`.
4. Sprawdź odpowiednio Kolejkę, Ulubione i Bibliotekę.

Oczekiwane:

- Shift+strzałki rozszerzają i zmniejszają zaznaczenie zgodnie ze standardową listą Windows;
- każde polecenie obejmuje wszystkie zaznaczone elementy i podaje ich liczbę;
- żaden niezaznaczony element nie zmienia stanu;
- zwykłe strzałki wracają do pojedynczego zaznaczenia.

## AMC-052-03 — Zbiorowe usuwanie i jedno cofnięcie

1. Dodaj co najmniej trzy pliki do Kolejki.
2. W Kolejce zaznacz dwa sąsiednie przez Shift+strzałkę i naciśnij Delete.
3. Naciśnij raz `Ctrl+Z`.
4. Powtórz zbiorcze usunięcie i cofnięcie w Ulubionych albo Bibliotece.

Oczekiwane:

- Delete usuwa oba zaznaczone elementy;
- jedno `Ctrl+Z` przywraca całą operację zbiorową, nie tylko jeden element;
- przywrócone elementy są ponownie zaznaczone, jeśli występują w bieżącym widoku;
- fokus pozostaje na liście również wtedy, gdy usunięcie chwilowo ją opróżni.

## AMC-052-04 — Izolacja głośności od NVDA i systemu

1. Uruchom dłuższy lokalny plik i otwórz odtwarzacz.
2. Zanotuj głośność NVDA oraz systemową głośność wyjścia.
3. Zmień głośność AMC strzałkami w górę/dół oraz Shift+strzałkami od minimum do wyraźnie wyższej wartości.
4. W trakcie zmian wywołuj mowę NVDA i ponownie sprawdź jego oraz systemową głośność.

Oczekiwane:

- zmienia się wyłącznie głośność odtwarzanego pliku w AMC;
- NVDA mówi cały czas z tą samą głośnością;
- suwak główny systemu i głośność innych aplikacji nie zmieniają się;
- przewijanie i regulacja prędkości nadal działają.

## AMC-052-05 — Automatyczna kontynuacja listy

1. Otwórz folder zawierający co najmniej trzy krótkie pliki w znanej kolejności.
2. Uruchom pierwszy i pozwól mu zakończyć się naturalnie.
3. Powtórz, wcześniej ustawiając inny plik jako „Odtwórz jako następne”.
4. Powtórz z elementem dodanym tylko do zwykłej Kolejki.
5. Pozwól zakończyć się ostatniemu elementowi bez dalszej kolejki.

Oczekiwane:

- bez kolejki zaczyna się następny plik załadowanej listy;
- „Odtwórz jako następne” ma pierwszeństwo, a zwykła Kolejka drugie;
- wykorzystany wpis znika z Kolejki w odpowiednim momencie;
- ostatni plik nie zapętla listy i program oznajmia jego koniec.

## AMC-052-06 — Zmiana sesji podaje przywrócony widok

1. Ustaw TIDAL w Bibliotece, Apple Music w Kolejce, a Lokalne multimedia w Ulubionych.
2. W każdej sesji pozostaw inne zaznaczenie.
3. Przełączaj `Ctrl+1`, `Ctrl+2` i numer lokalnej sesji, a następnie `Ctrl+Page Up/Page Down`.
4. W jednej sesji pozostaw odtwarzacz i wróć do niej po przełączeniu.

Oczekiwane:

- NVDA podaje numer lub nazwę sesji, usługę, przywrócony widok i zaznaczony element w jednej wypowiedzi;
- przykładowo słychać „TIDAL, Biblioteka” albo „Apple Music, Kolejka”, nie samą usługę;
- sesja pozostawiona w odtwarzaczu jest oznajmiana jako Odtwarzacz;
- każdy widok i fokus pozostają niezależne.

## AMC-052-07 — Właściwości bez „element element”

1. Z listy otwórz `Alt+Enter` i czytaj od początku strzałkami.
2. Użyj „Kopiuj wszystko” i porównaj skopiowany tekst z odczytem.
3. Zamknij okno, uruchom odtwarzacz i powtórz.

Oczekiwane:

- pierwsza linia brzmi „Podstawowe informacje”, a nie „element element”;
- dalsze sekcje i wartości są czytane po kolei bez technicznych nazw kontrolek;
- kopiowanie nadal zwraca pełne, prawidłowe dane;
- fokus wraca do poprzedniej listy albo odtwarzacza.

## AMC-052-08 — Tytuł okna i regresja

1. Odtwórz jeden plik, a na liście zaznacz inny.
2. Przejdź kolejno do Ulubionych, Kolejki i Biblioteki i odczytaj tytuł okna.
3. Wstrzymaj oraz zmień odtwarzany plik, ponownie odczytując tytuł.
4. Sprawdź pasek `NVDA+End`, czas, skoki, prędkość, wyszukiwanie, paletę, menu i `Alt+F4`.

Oczekiwane:

- początek tytułu jest nazwą aktualnie odtwarzanego lub wstrzymanego elementu, a nie bieżącego zaznaczenia;
- dalsza część zawsze odpowiada usłudze i rzeczywistemu widokowi: Ulubione, Kolejka albo Biblioteka;
- zmiana bieżącego utworu aktualizuje początek tytułu;
- nie wracają podwójne komunikaty, techniczne identyfikatory, problemy z fokusem ani wcześniejsze regresje.
