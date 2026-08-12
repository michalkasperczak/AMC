# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-013`
- Tytuł zestawu: Nowa mapa skrótów i rozpoznanie problemu Ctrl+Z
- Wersja programu: `0.1.0-alpha.13`
- Utworzono: 2026-08-12 20:21:31, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-12_202131_0.1.0-alpha.13.md`

Najważniejsze są zadania 1–4 i dokładny opis zachowania `Ctrl+Z` w zadaniu 6. Wystarczy swobodny opis; nie trzeba uzupełniać każdego pola.

## AMC-013-01 — Widoki Ulubionych i Albumów

1. Ustaw fokus na liście multimediów.
2. Naciśnij `Ctrl+U`, a następnie `Ctrl+Shift+A`.
3. Wejdź do pola filtra przez `Ctrl+K` i ponownie sprawdź oba skróty.

Oczekiwany wynik: `Ctrl+U` otwiera Ulubione, `Ctrl+Shift+A` otwiera Albumy, także z pola filtra. `Ctrl+A` nie otwiera Albumów i zachowuje standardowe znaczenie kontrolki.

## AMC-013-02 — Filtr, wyszukiwanie i paleta poleceń

1. Naciśnij `Ctrl+K`, wpisz fragment tytułu i przejdź do wyników.
2. Naciśnij `Ctrl+F`.
3. Naciśnij `Ctrl+Shift+F`.
4. Naciśnij `Ctrl+Shift+K`.

Oczekiwany wynik: `Ctrl+K` przechodzi do istniejącego filtra bieżącej listy. `Ctrl+F` pokazuje demonstracyjny widok „Wyszukiwanie”, a `Ctrl+Shift+F` — „Wyszukiwanie we wszystkich usługach”. Paleta nie jest jeszcze zbudowana, więc `Ctrl+Shift+K` podaje jej jednoznaczny polski komunikat i nie zmienia fokusu przypadkowo.

## AMC-013-03 — Ulubione jako widok i czynność

1. Na liście naciśnij `Ctrl+Shift+U` dwa razy.
2. Po każdym użyciu sprawdź komunikat oraz położenie fokusu.
3. Otwórz menu kontekstowe elementu i sprawdź opis skrótu przy pozycji Ulubionych.

Oczekiwany wynik: pierwsze użycie dodaje albo usuwa element z Ulubionych, drugie odwraca zmianę. Fokus pozostaje na liście. Menu pokazuje `Ctrl+Shift+U`, a `Ctrl+U` służy wyłącznie do otwarcia widoku Ulubionych.

## AMC-013-04 — Nowa mapa po globalnym prefiksie

1. Naciśnij domyślny prefiks `Ctrl+Alt+Windows+F12`, a potem samą cyfrę `1`, `2` albo `3`.
2. Użyj kolejno: prefiks, `0`; prefiks, `U`; prefiks, `A`; prefiks, `K`; prefiks, `F`.
3. Sprawdź także prefiks, `Shift+U`; prefiks, `Shift+F`; prefiks, `Shift+K`.
4. Dodatkowo sprawdź prefiks, `D` oraz prefiks, `Shift+D`.

Oczekiwany wynik: po prefiksie cyfry i litery nie wymagają dodatkowego `Ctrl`. `U` otwiera Ulubione, `Shift+U` zmienia ich stan, `A` otwiera Albumy, `K` filtr, `F` wyszukiwanie bieżącej usługi. Warianty `Shift+F` i `Shift+K` wywołują odpowiednio wyszukiwanie globalne i komunikat palety. Oba skróty pobierania podają na razie komunikat o niedostępności.

Jeśli działa stara mapa, sprawdź w Ustawieniach, czy aktywny jest profil „Domyślny”. Własne profile celowo zachowują wcześniejsze przypisania.

## AMC-013-05 — Litery na liście bez prefiksu

1. Wróć na zwykłą listę multimediów.
2. Wpisuj szybko jedną albo kilka liter nazwy elementu, bez `Ctrl` i bez prefiksu.
3. Naciśnij samo `A` oraz `U`.

Oczekiwany wynik: litery przechodzą do pasującego elementu, jak wcześniej. Nie otwierają Albumów ani Ulubionych i nie wykonują czynności.

## AMC-013-06 — Dokładny objaw Ctrl+Z

1. Zmień stan Ulubionych klawiszami `Ctrl+Shift+U`, a następnie naciśnij `Ctrl+Z`.
2. Powtórz dla Biblioteki (`Ctrl+Shift+L`) oraz Kolejki (`Ctrl+Shift+Q`).
3. Naciskaj `Ctrl+Z` po wyczerpaniu historii.
4. Osobno wpisz tekst w filtrze i użyj tam `Ctrl+Z`.

Zapisz proszę możliwie dokładnie: co mówi NVDA, gdzie znajduje się fokus, czy zmiana faktycznie została cofnięta i czy problem występuje zawsze, czy tylko w jednym z powyższych przypadków. W `alpha.13` mechanizm `Ctrl+Z` nie został jeszcze zmieniony — to zadanie ma dać jednoznaczną podstawę do poprawki.
