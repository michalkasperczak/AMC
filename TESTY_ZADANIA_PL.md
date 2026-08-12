# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-010`
- Tytuł zestawu: Pusta lista, Biblioteka i proste szablony
- Wersja programu: `0.1.0-alpha.10`
- Utworzono: 2026-08-11 16:05:05, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-11_160505_0.1.0-alpha.10.md`

To krótki test czterech miejsc przebudowanych po wynikach alpha.9. Wystarczy swobodny opis; nie trzeba uzupełniać wszystkich pól.

## AMC-010-01 — Szablony bez tabeli

1. Otwórz Ustawienia przez `Ctrl+,`, a następnie kartę Komunikaty.
2. Przejdź klawiszem `Tab` po nowych kontrolkach.
3. Na liście zdarzeń użyj strzałek, wybierz dowolne zdarzenie i przejdź do pola „Tekst szablonu”.

Oczekiwany wynik: zamiast tabeli jest zwykła lista nazw zdarzeń oraz osobne pole edycji. Pierwsza pozycja to „Zmiana sesji”. Nie występuje pusty pierwszy wiersz ani zagnieżdżona nawigacja po komórkach.

## AMC-010-02 — Biblioteka z listy i z filtra

1. W głównym oknie naciśnij `Ctrl+L`.
2. Sprawdź zawartość demonstracyjnej Biblioteki.
3. Naciśnij `Ctrl+F`, wpisz dowolny tekst i — pozostając w filtrze — ponownie naciśnij `Ctrl+L`.
4. Powtórz próbę z `Ctrl+P` oraz `Ctrl+Q`.

Oczekiwany wynik: świeżo uruchomiona Biblioteka zawiera dwa utwory demonstracyjne. `Ctrl+L`, `Ctrl+P` i `Ctrl+Q` działają również z pola filtra i przenoszą fokus do listy wybranego widoku.

## AMC-010-03 — Strzałki na pustej Kolejce

1. Otwórz Kolejkę przez `Ctrl+Q` i usuń wszystkie jej elementy.
2. Naciśnij kilka razy strzałki w górę i w dół, a także `Home`, `End`, `Page Up` i `Page Down`.
3. Dopiero na końcu naciśnij `Tab`.

Oczekiwany wynik: wszystkie klawisze nawigacyjne pozostają na pustej liście i podają „Kolejka: lista jest pusta”. Nie przechodzą do filtra, przycisku „Odtwórz” ani menu. Dopiero `Tab` świadomie opuszcza listę.

## AMC-010-04 — Usunięcie z niepustej Kolejki

1. Dodaj trzy elementy do kolejki.
2. W widoku Kolejka usuń środkowy element przez `Shift+Enter`.
3. Usuń kolejny element przez menu kontekstowe.
4. Sprawdź działanie strzałek po obu operacjach.

Oczekiwany wynik: po każdym usunięciu fokus pozostaje na najbliższym elemencie listy, a końcowy komunikat zawiera właściwy tytuł. Strzałki nie opuszczają Kolejki.
