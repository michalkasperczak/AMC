# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-028`
- Tytuł zestawu: Trwała historia wyszukiwania
- Wersja programu: `0.1.0-alpha.28`
- Utworzono: 2026-08-16 13:08:43, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-16_130843_0.1.0-alpha.28.md`

W pliku wyników po dwukropku wpisz `OK`, jeżeli zadanie działa, albo krótko opisz problem. Nie trzeba powtarzać wariantów odpowiedzi przy każdym zadaniu.

Alpha.28 dodaje trwałą historię zapytań. Historia jest osobna dla każdej usługi i dla wyszukiwania globalnego, przechowuje do 20 unikatowych pozycji i zapisuje również zapytania bez wyników.

## AMC-028-01 — Przeglądanie historii strzałkami

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Otwórz wyszukiwanie przez `Ctrl+F`, wpisz `brzeg ciszy` i naciśnij Enter.
3. Zamknij wyniki klawiszem Escape i ponownie otwórz `Ctrl+F`.
4. W pustym polu naciśnij strzałkę w dół.
5. Naciśnij strzałkę w dół jeszcze raz, potem strzałkę w górę i ponownie strzałkę w górę.

Oczekiwane wyniki:

- pierwsza strzałka w dół wybiera najnowsze zapytanie „brzeg ciszy”;
- kolejne naciśnięcie przechodzi do starszego wpisu, jeżeli taki istnieje, bez przenoszenia fokusu na listę wyników;
- strzałka w górę wraca w stronę nowszych wpisów;
- strzałka w górę użyta na najnowszym wpisie przywraca puste pole;
- odczyty nie nakładają się i nie pojawia się osobny fragment „Stan programu”.

## AMC-028-02 — Osobne historie usług i zakresu globalnego

1. W TIDAL wykonaj `Ctrl+F` i wyszukaj `pierwszy utwór`, następnie zamknij okno.
2. Przełącz się do Apple Music przez `Ctrl+2`, wykonaj `Ctrl+F` i wyszukaj `zielony horyzont`, następnie zamknij okno.
3. Otwórz wyszukiwanie globalne przez `Ctrl+Shift+F`, wyszukaj `album demonstracyjny` i zamknij okno.
4. Otwórz ponownie wyszukiwanie globalne i w pustym polu naciśnij strzałkę w dół.
5. Zamknij je, przejdź do TIDAL, otwórz `Ctrl+F` i naciśnij strzałkę w dół.
6. Powtórz sprawdzenie w Apple Music.

Oczekiwane wyniki:

- historia globalna zaczyna się od „album demonstracyjny”;
- historia TIDAL zaczyna się od „pierwszy utwór”;
- historia Apple Music zaczyna się od „zielony horyzont”;
- wpisy nie przechodzą między zakresami.

## AMC-028-03 — Zapytanie bez wyniku i ponowne uruchomienie

1. W TIDAL otwórz `Ctrl+F`, wpisz `zapytanie bez wyniku alpha 28` i naciśnij Enter.
2. Po komunikacie o braku wyników zamknij wyszukiwanie, otwórz je ponownie i naciśnij strzałkę w dół.
3. Zamknij całą aplikację AMC i uruchom ponownie alpha.28.
4. Naciśnij `Ctrl+1`, potem `Ctrl+F` i w pustym polu strzałkę w dół.

Oczekiwane wyniki:

- brak wyników pozostawia fokus w polu wyszukiwania;
- zapytanie bez wyniku jest najnowszą pozycją historii TIDAL;
- po ponownym uruchomieniu aplikacji ta sama pozycja nadal jest dostępna;
- konfiguracja z alpha.27 uruchamia się bez błędu migracji.

## AMC-028-04 — Regresja zwykłego wyszukiwania

1. Otwórz `Ctrl+F`, wpisz `brzeg` i naciśnij Enter.
2. Na wyniku sprawdź zwykły Enter.
3. Ponownie otwórz wyszukiwanie i sprawdź `Ctrl+Enter` albo inne znane działanie bezpośrednie.
4. Zamknij wyszukiwanie przez Escape.

Oczekiwane wyniki:

- pierwszy Enter wykonuje wyszukiwanie i ustawia fokus na wyniku;
- zwykły Enter otwiera wynik i wraca do głównej listy;
- działanie bezpośrednie pozostawia okno wyników otwarte;
- Escape wraca do głównego okna, a dotychczasowe komunikaty i fokus działają jak w alpha.27.

## Następny etap

Po zatwierdzeniu historii wyszukiwania kolejnym małym etapem będzie dostępna paleta poleceń pod `Ctrl+Shift+K`. Osobną decyzją ustawień listy pozostaje możliwość całkowitego wyłączenia pola czasu trwania.
