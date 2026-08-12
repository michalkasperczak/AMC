# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-011`
- Tytuł zestawu: Cofanie zmian przez Ctrl+Z
- Wersja programu: `0.1.0-alpha.11`
- Utworzono: 2026-08-12 13:02:46, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-12_130246_0.1.0-alpha.11.md`

To krótki test przywracania elementów przypadkowo usuniętych z widoków. Wystarczy swobodny opis; nie trzeba uzupełniać wszystkich pól.

## AMC-011-01 — Przywrócenie elementu Biblioteki

1. Otwórz Bibliotekę przez `Ctrl+L`.
2. Usuń zaznaczony element klawiszem `Delete` albo `Backspace`.
3. Naciśnij `Ctrl+Z`.

Oczekiwany wynik: usunięty element wraca do Biblioteki, zostaje ponownie zaznaczony, fokus pozostaje na liście, a NVDA podaje „Przywrócono w bibliotece” wraz z tytułem.

## AMC-011-02 — Kilka operacji cofanych po kolei

1. Na dowolnym elemencie zmień kolejno stan Ulubionych, Biblioteki i Kolejki.
2. Trzykrotnie naciśnij `Ctrl+Z`.

Oczekiwany wynik: trzy zmiany są cofane w odwrotnej kolejności. Każde naciśnięcie podaje osobny, zrozumiały komunikat z tytułem elementu.

## AMC-011-03 — Całkowicie opróżniona Kolejka

1. Otwórz Kolejkę i usuń jej ostatni element.
2. Gdy lista jest pusta, naciśnij `Ctrl+Z`.

Oczekiwany wynik: element wraca, zostaje zaznaczony i przyjmuje wcześniejszy stan Kolejki oraz „Odtwórz jako następne”. Fokus nie przechodzi do filtra, przycisków ani menu.

## AMC-011-04 — Ctrl+Z w filtrze i menu Edycja

1. Przejdź do filtra przez `Ctrl+F`, wpisz kilka znaków i naciśnij `Ctrl+Z`.
2. Wróć do listy, usuń element z Biblioteki, a następnie wybierz `Edycja → Cofnij ostatnią zmianę`.
3. Gdy nie ma już zmian do cofnięcia, ponownie użyj polecenia.

Oczekiwany wynik: w filtrze `Ctrl+Z` cofa wpisywanie tekstu i nie zmienia przynależności elementów. Polecenie z menu przywraca element tak samo jak skrót. Pusta historia podaje „Brak zmian do cofnięcia”.
