# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-055`
- Tytuł zestawu: Kolejność właściwości, pełny odczyt menu i historia sesji
- Wersja programu: `0.1.0-alpha.55`
- Utworzono: 2026-08-19 14:07, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-19_1407_0.1.0-alpha.55.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-055-01 — Trzy części właściwości

1. Na lokalnym pliku otwórz `Alt+Enter`.
2. Przeczytaj cały tekst strzałkami.
3. Powtórz dla elementu demonstracyjnej usługi.

Oczekiwane:

- początek zawiera tytuł, wykonawcę, rodzaj, usługę i — lokalnie — pełną ścieżkę;
- dalej występuje nagłówek „W aplikacji”, a pod nim odtwarzanie, Ulubione, Biblioteka, Kolejka i „Odtwórz jako następne”;
- ostatnia sekcja „Techniczne” zaczyna się od czasu, następnie podaje dostępny format, rozmiar, bitrate i częstotliwość;
- nie ma powtórzonej ścieżki ani zbędnego nagłówka „Podstawowe informacje”.

## AMC-055-02 — Skrót przy pierwszym i ponownym odczycie menu

1. Na liście otwórz menu kontekstowe.
2. Przechodź po pozycjach strzałkami i zwróć uwagę, czy nazwa wraz ze skrótem jest wypowiadana tylko raz.
3. Na każdej z kilku pozycji użyj polecenia NVDA odczytującego ponownie bieżący fokus lub wiersz.
4. Powtórz w menu kontekstowym odtwarzacza.
5. Sprawdź pozycje zmieniające nazwę, np. „Dodaj do ulubionych” i po wykonaniu „Usuń z ulubionych”.

Oczekiwane:

- zwykłe wejście strzałką mówi opcję i skrót jeden raz;
- ponowny odczyt fokusu również mówi opcję i skrót;
- dynamiczna nazwa „Dodaj” albo „Usuń” zawsze ma właściwy skrót;
- pozycja bez skrótu, np. otwarcie w oficjalnej aplikacji, nie otrzymuje sztucznego skrótu.

## AMC-055-03 — Historia osobna dla sesji

1. W TIDAL przejdź kolejno do Biblioteki, Ulubionych i Kolejki.
2. Przełącz się do Apple Music i przejdź do Albumów oraz Biblioteki.
3. W Apple Music użyj `Alt+strzałka w lewo`, potem `Alt+strzałka w prawo`.
4. Wróć do TIDAL i ponownie użyj obu kierunków historii.
5. W sesji bez wcześniejszego widoku spróbuj przejść wstecz.

Oczekiwane:

- historia Apple Music porusza się wyłącznie między widokami Apple Music;
- po powrocie do TIDAL zachowana jest osobna historia TIDAL;
- komunikaty zawierają „Wstecz” albo „Naprzód”, nazwę sesji i nazwę widoku;
- brak historii jest zgłaszany wraz z nazwą bieżącej sesji;
- historia nigdy nie zmienia usługi.

## AMC-055-04 — Krótka regresja właściwości i menu

1. W `Alt+Enter` zaznacz fragment tekstu znakami i słowami, skopiuj go, użyj „Kopiuj wszystko” i wyjdź Escape’em.
2. W menu listy zaznacz dwa elementy i zmień ich stan kolejki lub Ulubionych.
3. W odtwarzaczu otwórz właściwości i skopiuj nazwę oraz pełną ścieżkę.

Oczekiwane:

- tekstowe zaznaczanie i oba rodzaje kopiowania z `alpha.54` nadal działają;
- działania listy zachowują wybór wieloelementowy;
- działania odtwarzacza dotyczą faktycznie odtwarzanego elementu;
- po zamknięciu okna albo menu fokus wraca do właściwego miejsca.
