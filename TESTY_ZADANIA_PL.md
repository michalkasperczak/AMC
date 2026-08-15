# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-027`
- Tytuł zestawu: Jedna wypowiedź przy zmianie sesji
- Wersja programu: `0.1.0-alpha.27`
- Utworzono: 2026-08-16 00:06:12, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-16_000612_0.1.0-alpha.27.md`

W pliku wyników w polu „Status” można wpisać `OK`, `Błąd` albo `Pominięto`. W pozostałych polach dopisz wynik lub uwagę po pozostawionej spacji. Nie usuwaj numeru ani tytułu zadania.

Alpha.27 obejmuje tylko potwierdzony problem zmiany sesji. Czas znajdujący się w etykiecie elementu pozostaje świadomie włączony: oznacza czas tego utworu, albumu albo playlisty. Usunięte wcześniej podsumowanie dotyczyło liczby oraz łącznego czasu całej listy.

## AMC-027-01 — Sesja i element jako jedna wypowiedź

1. Ustaw fokus na liście multimediów.
2. Naciśnij kolejno `Ctrl+1`, `Ctrl+2` i `Ctrl+3`. Po każdym skrócie zapisz pełną pierwszą wypowiedź.

Oczekiwane wyniki:

- każda wypowiedź zaczyna się od numeru sesji i nazwy usługi, a następnie bez przerwania podaje zaznaczony element;
- nie pojawia się osobny komunikat „Stan programu” ani jego urwany fragment;
- NVDA może standardowo podać rodzaj kontrolki i pozycję „1 z N”.

## AMC-027-02 — Poprzednia i następna sesja

1. Pozostając na liście, naciśnij `Ctrl+Page Down`.
2. Zapisz pełną pierwszą wypowiedź.
3. Naciśnij `Ctrl+Page Up` i ponownie zapisz wypowiedź.

Oczekiwane wyniki:

- każda zmiana daje jedną wypowiedź: numer sesji, usługa i element;
- fokus pozostaje na liście;
- nie występuje osobny komunikat z obszaru „Stan programu”.

## AMC-027-03 — Prefiks sesji jest jednorazowy

1. Przełącz się skrótem `Ctrl+2` do Apple Music.
2. Po pierwszej wypowiedzi naciśnij strzałkę w dół i zapisz wypowiedź następnego elementu.
3. Otwórz Albumy skrótem `Ctrl+Shift+A` i zapisz wypowiedź pierwszego albumu.

Oczekiwane wyniki:

- pierwszy element po `Ctrl+2` zaczyna się od numeru i nazwy usługi;
- następny element nie powtarza numeru ani usługi;
- Albumy zaczynają się jednorazowo od nazwy widoku;
- czas przy albumie jest czasem albumu, a nie osobnym podsumowaniem listy;
- nie jest wypowiadana liczba elementów ani łączny czas całej listy jako dodatkowy komunikat.

## Następne funkcje po tym zestawie

Po zatwierdzeniu komunikatów sesji następnym małym etapem będzie lokalna historia wyszukiwania: osobna dla każdej usługi i zakresu globalnego, do 20 unikatowych zapytań, wybierana strzałką w dół przy pustym polu. Potem powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. Osobną decyzją ustawień listy pozostaje możliwość całkowitego wyłączenia pola czasu trwania.
