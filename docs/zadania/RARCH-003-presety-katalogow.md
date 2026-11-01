# RARCH-003 — presety katalogów Radio Archiwisty

Stan: źródło i testy zaliczone; budowa, instalacja i publikacja wymagają osobnego potwierdzenia wyników. Wersja docelowa: AMC Test 0.1.0-alpha.462.

## Zachowanie

- W sesji Radio Archiwista Ctrl+Shift+1 otwiera „Najnowsze nagrania”, a Ctrl+Shift+2 folder Programu 1 (lata). To ustawienia początkowe. Miejsca 3–12 są puste.
- Na liście stacji lub wierszu „Najnowsze nagrania” Ctrl+Alt+Shift+P otwiera dialog przypisania do dowolnego z 12 miejsc. Ctrl+Alt+P pokazuje listę presetów. Zmiany są zapisywane tylko w prywatnych ustawieniach AMC Test. Usunięcie wszystkich miejsc pozostaje zapamiętane i nie przywraca domyślnych przypisań.
- Wybranie pojedynczego nagrania, roku, miesiąca lub dnia nie pozwala przypisać go jako preset. Komunikat NVDA prosi wtedy o wybranie elementu sesji do przypisania. Po zapisaniu czytnik podaje „Zapisano preset N: [stacja]”; po usunięciu „Usunięto preset N”. Lista presetów pokazuje przyjazne nazwy i skróty, bez identyfikatorów katalogu.
- Pozostałe sesje zachowują własne presety. Ctrl+Shift+D w Radio Archiwiście nadal pobiera wycinek; w Podcastach/YouTube zachowuje dotychczasowe znaczenie.

## Decyzja i korekta

1.11.2026 użytkownik doprecyzował, że preset ma wskazywać stację lub folder, nie nagranie z określonej godziny. Ta decyzja zastępuje wariant pojedynczego godzinnego nagrania zapisany w `stan-aktualizacji.md` z 1.11.2026 17:12.

Wykorzystano istniejący dialog przypisania Ctrl+Alt+Shift+P i wspólny model poleceń. Bezpośredni gest Ctrl+Shift+Alt+cyfra z wcześniejszej propozycji użytkownika nie jest jeszcze osobnym poleceniem; zmiana miejsca działa w dostępnym dialogu.

## Weryfikacja

- Testy jednostkowe: domyślne cele bez starego profilu, trwałe nadpisanie i pusta lista, odmowa dla pliku godzinnego, skróty/menu/pomoc, wywołanie nawigacji do „Najnowszych” i lat Programu 1. Skupiony zestaw: 29 zaliczonych (1.11.2026).
- Manualny odsłuch NVDA, start aplikacji z nową paczką, instalacja i GitHub Release są odrębnymi etapami; nie są potwierdzone samymi testami.

## Szkic instrukcji i listy zmian do akceptacji użytkownika

W sesji Radio Archiwista naciśnij Ctrl+Shift+1, aby otworzyć najnowsze nagrania, lub Ctrl+Shift+2, aby otworzyć Program 1. Aby przypisać inny katalog, zaznacz stację na liście i naciśnij Ctrl+Alt+Shift+P. Wybierz numer, zapisz, a potem otwieraj go skrótem Ctrl+Shift+cyfra. Listę wszystkich miejsc otworzysz Ctrl+Alt+P.

Lista zmian: Radio Archiwista otrzymał 12 miejsc presetów katalogowych, dwa początkowe przypisania i możliwość ich zmiany.
