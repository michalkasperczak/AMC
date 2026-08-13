# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-016`
- Tytuł zestawu: Prawdziwe wyszukiwanie, rozdzielenie filtra i stabilna lista
- Wersja programu: `0.1.0-alpha.16`
- Utworzono: 2026-08-13 23:21:52, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-13_232152_0.1.0-alpha.16.md`

Najważniejsze są pierwsze trzy zadania. Do testu nawigacji literami używaj widoku **Teraz odtwarzane**, ponieważ zawiera 17 pozycji. Biblioteka celowo zawiera tylko dwa należące do niej utwory.

## AMC-016-01 — Wyszukiwanie w bieżącej usłudze

1. Na głównej liście TIDAL naciśnij `Ctrl+F`.
2. Sprawdź tytuł okna i początkowy fokus.
3. Wpisz `Brzeg` i naciśnij Enter.
4. Sprawdź odczyt liczby wyników i fokus na liście wyników.
5. Naciśnij Enter na „Brzeg ciszy”.

Oczekiwany wynik: otwiera się osobne okno „Szukaj w usłudze TIDAL”, a fokus trafia do pola „Wyszukiwany tekst”. Pierwszy Enter wykonuje wyszukiwanie i przechodzi na wynik. Drugi Enter zamyka okno, wraca do głównej listy „Teraz odtwarzane” i zaznacza „Brzeg ciszy”.

## AMC-016-02 — Anulowanie i brak wyników

1. Naciśnij `Ctrl+F`, wpisz tekst, którego nie ma, i naciśnij Enter.
2. Sprawdź komunikat i położenie fokusu.
3. Naciśnij Escape.
4. Otwórz wyszukiwanie ponownie i od razu naciśnij Escape.

Oczekiwany wynik: przy braku dopasowania słychać „Brak wyników. Zmień wyszukiwany tekst”, a tekst pozostaje zaznaczony do poprawy. Escape zamyka okno jednym krokiem i przywraca fokus głównej liście bez zmiany widoku.

## AMC-016-03 — Wyszukiwanie globalne i zmiana sesji

1. Będąc w TIDAL-u, naciśnij `Ctrl+Shift+F`.
2. Wpisz `Zielony horyzont` i naciśnij Enter.
3. Przejrzyj wyniki strzałkami; powinny zawierać nazwę usługi.
4. Wybierz wynik z Apple Music i naciśnij Enter.

Oczekiwany wynik: okno nazywa się „Szukaj we wszystkich usługach”. Lista zawiera osobne wyniki TIDAL, Apple Music i WiiM. Otwarcie wyniku Apple Music przełącza główną sesję na Apple Music i zaznacza „Zielony horyzont”.

## AMC-016-04 — Różnica między filtrem a wyszukiwaniem

1. Naciśnij `Ctrl+K`, wpisz `B` i sprawdź zachowanie.
2. Naciśnij Escape.
3. Naciśnij `Ctrl+F`, wpisz `B` i naciśnij Enter.

Oczekiwany wynik: `Ctrl+K` nie otwiera nowego okna i od razu zawęża wyłącznie bieżącą, załadowaną listę. `Ctrl+F` otwiera osobne okno i wykonuje zapytanie dopiero po Enterze. W prototypie wyszukiwanie korzysta jeszcze z katalogu demonstracyjnego; później ten sam interfejs odpytuje prawdziwą usługę.

## AMC-016-05 — Nawigacja literami we właściwym widoku

1. Z menu **Widok** otwórz **Teraz odtwarzane** i sprawdź, że lista ma 17 pozycji.
2. Naciśnij szybko `Z`, `I`, `E`.
3. Po krótkiej przerwie naciskaj pojedyncze `B`, za każdym razem czekając na odczyt elementu.
4. Powtórz z literą `C`.

Oczekiwany wynik: `ZIE` przechodzi do „Zielonego horyzontu”. Kolejne pojedyncze `B` przechodzą między „Brzegiem ciszy” i „Błękitną godziną”, a `C` — między „Ciepłym deszczem” i „Ciszą o świcie”. Elementy nie znikają; przejście do Biblioteki pokazuje tylko jej dwa elementy i nie zmienia katalogu „Teraz odtwarzane”.

## Następne funkcje po tym zestawie

Po ustabilizowaniu wyszukiwania kolejny mały etap obejmie dostępną paletę poleceń pod `Ctrl+Shift+K`. Równolegle w planie pozostają niskopoziomowy, konfigurowalny prefiks z testem `Ctrl+Numeryczny Enter`, porządek instalacji i aktualizacji, wydzielenie AMC.Host, WiiM jako pierwszy realny adapter oraz późniejsze logowanie OAuth do usług. Tych tematów nie dokładamy naraz do testu alpha.16, ale pozostają zapisane w `MEDIA_CONTROLLER_PL.md`.
