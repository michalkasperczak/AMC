# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-012`
- Tytuł zestawu: Stabilny fokus, Escape i komunikat Ctrl+Z
- Wersja programu: `0.1.0-alpha.12`
- Utworzono: 2026-08-12 16:17:17, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-12_161717_0.1.0-alpha.12.md`

To krótki test poprawek zgłoszonych po `alpha.11`. Wystarczy swobodny opis; nie trzeba uzupełniać wszystkich pól.

## AMC-012-01 — Ctrl+Z bez dodatkowego „Undo”

1. Otwórz Bibliotekę przez `Ctrl+L`.
2. Usuń zaznaczony element i naciśnij `Ctrl+Z`.

Oczekiwany wynik: NVDA podaje tylko właściwy polski komunikat, na przykład „Przywrócono w bibliotece” wraz z tytułem. Nie powinien wcześniej mówić po angielsku „Undo”.

## AMC-012-02 — Pusta historia cofania

1. Naciskaj `Ctrl+Z`, aż program poda „Brak zmian do cofnięcia”.
2. Użyj strzałek w górę i w dół.

Oczekiwany wynik: fokus pozostaje na liście, a strzałki poruszają się po elementach. Nie przechodzi na przycisk Otwórz ani do menu Plik.

## AMC-012-03 — Zrozumiałe wejście do filtra

1. Na liście naciśnij `Ctrl+F`.
2. Posłuchaj instrukcji, wpisz fragment tytułu i naciśnij `Enter` albo strzałkę w dół.

Oczekiwany wynik: program krótko wyjaśnia działanie filtra, wpisywanie ogranicza wyniki, a Enter lub strzałka w dół przenosi na pierwszy pasujący element.

## AMC-012-04 — Jednoznaczny Escape

1. W filtrze wpisz tekst i naciśnij `Escape`.
2. Ponownie wejdź do filtra, pozostaw go pusty i naciśnij `Escape`.
3. Przejdź klawiszem Tab na dowolny główny przycisk i naciśnij `Escape`.
4. Otwórz menu z podmenu i naciskaj `Escape`.

Oczekiwany wynik: w filtrze i na przycisku wystarcza jedno naciśnięcie. Aktywny filtr zostaje wyczyszczony, a fokus wraca do listy. Menu działa standardowo — każde naciśnięcie Escape wychodzi o jeden poziom.
