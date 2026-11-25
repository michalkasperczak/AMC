# „Odtwarzaj jako następne” pod Shift+Enter

Stan: wymaganie zapisane 25.11.2026; bez implementacji nowego skrótu, testu, kompilacji i wydania.

## Zgłoszenie i obecny stan

Użytkownik chce przywrócić `Shift+Enter` dla „Odtwarzaj jako następne”. Wybrany utwór lub odcinek ma wejść po aktualnie grającym materiale, przed dawną następną pozycją kolejki. Obecny model ma `Ctrl+Shift+Enter` jako przełącznik bloku „Odtwarzaj jako następne”; w `shortcuts.py` `Shift+Enter` jest celowo pozostawiony wolny. Nie wolno zatem dodawać skrótu tylko do jednej listy bez uzgodnienia jego znaczenia we wszystkich kwalifikujących się sesjach.

## Projekt zachowania

- `Shift+Enter` na odtwarzalnym wierszu ma dopisać go za bieżącą pozycją bez przerywania bieżącego utworu. Później ma wrócić wcześniejsza następna pozycja, o ile nadal istnieje.
- Dla kilku kolejnych dodań proponowany jest porządek wykonywania gestów: bieżący → pierwszy dodany → drugi dodany → dawna następna pozycja. Wielokrotny wybór zachowuje kolejność widoczną na liście. Decyzję o ponownym wskazaniu pozycji już oczekującej trzeba zapisać przed kodowaniem, aby gest nie usunął jej przypadkiem.
- Jawnie rozdzielić jednorazowe „dodaj jako następne” od obecnego przełącznika `Ctrl+Shift+Enter`, który może usuwać członkostwo. Zmianę wykonać w jednym modelu komend z menu, `Ctrl+F1`, pełną pomocą, paletą i skrótami.
- W zwykłym Radiu na żywo funkcja pozostaje niedostępna. Kolejki TIDAL, Spotify, HEOS i urządzeń mają własnych właścicieli; pokazać skrót tylko tam, gdzie adapter potrafi przewidywalnie wstawić materiał.

## Zależność od wspólnej kolejki

To polecenie może działać z obecnymi kolejkami sesji przed wprowadzeniem opcjonalnej wspólnej kolejki `QUEUE-003`. Po jej wdrożeniu musi wybierać właściwy kontener według jawnego trybu, bez dublowania wpisów i bez mieszania kolejki urządzenia z lokalną.

## Testy i wynik

Odczyt `shortcuts.py` potwierdza `Ctrl+Shift+Enter` i wolny `Shift+Enter`; nie wykonano zmian ani prób na odtwarzaczu. Przyszłe testy: wstawienie X po grającym B w kolejce A–B–C, potem Y; weryfikacja B–X–Y–C, koniec kolejki, filtr, kilka zaznaczonych elementów, duplikat, źródło nieobsługiwane, brak przeskoku fokusu oraz parytet menu/pomocy/palety.

## Materiał do dokumentacji użytkownika

Szkic przyszłej instrukcji: „Zaznacz utwór lub odcinek i naciśnij `Shift+Enter`, aby zagrał po obecnym materiale. Bieżący utwór będzie grał dalej”. Skrót nie jest jeszcze dostępny w tej wersji.

## Znane ograniczenia, wersja i commit

Brak implementacji i commitu funkcji. Zgłoszenie powstało po AMC Test alpha.528. Następny krok: określić semantykę duplikatu i adaptery sesji, a następnie dodać komendę i testy wykonania.
