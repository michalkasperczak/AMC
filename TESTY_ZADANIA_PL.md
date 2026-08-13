# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-014`
- Tytuł zestawu: Cofanie, wyszukiwanie i powtarzana nawigacja literami
- Wersja programu: `0.1.0-alpha.14`
- Utworzono: 2026-08-12 22:08:34, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-12_220834_0.1.0-alpha.14.md`

Najważniejsze są trzy pierwsze zadania. Globalnego prefiksu w tej wersji nie trzeba ponownie sprawdzać.

## AMC-014-01 — Ctrl+Z bez „Undo” i z rzeczywistym cofnięciem

1. Na liście zmień stan Ulubionych skrótem `Ctrl+Shift+U`.
2. Naciśnij `Ctrl+Z` i sprawdź stan elementu, fokus oraz cały komunikat NVDA.
3. Powtórz dla Biblioteki (`Ctrl+Shift+L`) i Kolejki (`Ctrl+Shift+Q`).
4. Po wyczerpaniu historii naciśnij `Ctrl+Z` jeszcze raz.
5. Wpisz kilka znaków w polu filtra i sprawdź tam `Ctrl+Z`.

Oczekiwany wynik: zmiana jest rzeczywiście cofnięta, NVDA podaje tylko właściwy polski komunikat, a fokus pozostaje na liście. W pustej historii słychać „Brak zmian do cofnięcia”. W polu filtra działa zwykłe cofanie edycji tekstu.

## AMC-014-02 — Wejście do wyszukiwania i Escape

1. Otwórz dowolny zwykły widok, na przykład Albumy albo Bibliotekę.
2. Naciśnij `Ctrl+F`, a potem `Escape`.
3. Powtórz używając `Ctrl+Shift+F`.

Oczekiwany wynik: wejście podaje nazwę odpowiedniego trybu wyszukiwania, ale nie czyta czasu trwania ani liczby elementów poprzedniego widoku. `Escape` wraca do wcześniejszej listy, ustawia fokus na jej elemencie i NVDA odczytuje ten element. Nie jest potrzebne drugie naciśnięcie `Escape`.

## AMC-014-03 — Kolejne sekwencje liter na liście

1. Na liście wpisz szybko kilka liter początku nazwy elementu.
2. Bez używania filtra rozpocznij inną sekwencję liter; sprawdź także nową sekwencję po krótkiej przerwie.
3. Naciskaj wielokrotnie tę samą literę, jeżeli kilka elementów zaczyna się od niej.
4. Powróć literami do elementu odnalezionego wcześniej.

Oczekiwany wynik: każda kolejna sekwencja działa, również po wcześniejszym wyszukaniu innego elementu. Gdy złożony ciąg przestaje pasować, ostatnia litera od razu rozpoczyna nowe wyszukiwanie. Powtarzanie jednej litery przechodzi po kolejnych pasujących elementach.

## AMC-014-04 — Regresja filtra i głównych widoków

1. Użyj `Ctrl+K`, wpisz fragment nazwy i przejdź strzałką w dół do wyników.
2. Naciśnij `Escape`.
3. Sprawdź `Ctrl+U`, `Ctrl+Shift+A`, `Ctrl+L` i `Ctrl+Q`.

Oczekiwany wynik: filtr nadal działa i po `Escape` wraca na listę. Skróty głównych widoków zachowują działanie z wersji alpha.13.

## AMC-014-05 — Skróty w menu kontekstowym

1. Ustaw fokus na elemencie listy multimediów i otwórz menu kontekstowe.
2. Przejdź strzałkami przez wszystkie jego pozycje.

Oczekiwany wynik: NVDA podaje przy każdej pozycji przypisany skrót, między innymi `Ctrl+Enter`, `Shift+Enter`, `Ctrl+Shift+U`, `Alt+Enter` i `Delete`.
