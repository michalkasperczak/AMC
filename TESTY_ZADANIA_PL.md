# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-026`
- Tytuł zestawu: Jeden komunikat przy zmianie widoku
- Wersja programu: `0.1.0-alpha.26`
- Utworzono: 2026-08-15 23:18:48, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-15_231848_0.1.0-alpha.26.md`

W pliku wyników w polu „Status” można wpisać `OK`, `Błąd` albo `Pominięto`. W pozostałych polach dopisz wynik lub uwagę po pozostawionej spacji. Nie usuwaj numeru ani tytułu zadania.

Alpha.26 usuwa osobne podsumowanie widoku z obszaru „Stan programu”. Nazwa widoku i pierwszy element mają zostać odczytane jako jedna wypowiedź wynikająca z ustawienia fokusu.

## AMC-026-01 — Jedna wypowiedź po skrócie widoku

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Naciśnij `Ctrl+Shift+A` i zapisz pełną pierwszą wypowiedź.
3. Naciśnij kolejno `Ctrl+P`, `Ctrl+U`, `Ctrl+L` i `Ctrl+Q`; po każdym skrócie zapisz pełną pierwszą wypowiedź.

Oczekiwane wyniki:

- każda wypowiedź zaczyna się od nazwy otwartego widoku, a następnie bez przerwania podaje wybrany element;
- Albumy nie powtarzają rodzaju „album”, a Playlisty nie powtarzają rodzaju „playlista”;
- Ulubione, Biblioteka i Kolejka zachowują rodzaj zasobu;
- nie występuje osobny komunikat o liczbie i łącznym czasie;
- nie występuje zwrot „Stan programu” ani jego urwany fragment;
- NVDA może standardowo podać rodzaj kontrolki i pozycję „1 z N”.

## AMC-026-02 — Prefiks widoku jest jednorazowy

1. Otwórz Albumy skrótem `Ctrl+Shift+A`.
2. Zapisz pierwszą wypowiedź, a następnie naciśnij strzałkę w dół albo w górę i zapisz wypowiedź elementu.
3. Powtórz sprawdzenie w Ulubionych skrótem `Ctrl+U`.

Oczekiwane wyniki:

- pierwszy element po skrócie zaczyna się od „Albumy” albo „Ulubione”;
- następny element nie powtarza nazwy widoku;
- żadna wypowiedź nie jest przerwana innym komunikatem AMC.

## AMC-026-03 — Historia widoków

1. Otwórz kolejno Albumy i Playlisty.
2. Na liście naciśnij `Alt+Strzałka w lewo`.
3. Zapisz pełną pierwszą wypowiedź.
4. Naciśnij `Alt+Strzałka w prawo` i ponownie zapisz wypowiedź.

Oczekiwane wyniki:

- powrót do Albumów daje jedną wypowiedź „Albumy, element”;
- przejście naprzód do Playlist daje jedną wypowiedź „Playlisty, element”;
- nie pojawia się osobne podsumowanie liczby ani czasu;
- fokus pozostaje na liście multimediów.

## Następne funkcje po tym zestawie

Po zatwierdzeniu komunikatów widoków następnym małym etapem będzie lokalna historia wyszukiwania: osobna dla każdej usługi i zakresu globalnego, do 20 unikatowych zapytań, wybierana strzałką w dół przy pustym polu. Potem powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. W dalszym planie pozostają niskopoziomowy konfigurowalny prefiks, instalator i bezpieczne aktualizacje, AMC.Host, pierwszy adapter WiiM oraz pierwsze logowanie OAuth do Spotify. Prawdziwy TIDAL pozostaje ważnym osobnym modułem z izolowaną listą wyników; Sonos jest etapem późniejszym.
