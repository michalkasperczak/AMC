# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-025`
- Tytuł zestawu: Zwięzłe etykiety zależne od kontekstu
- Wersja programu: `0.1.0-alpha.25`
- Utworzono: 2026-08-15 19:06:28, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-15_190628_0.1.0-alpha.25.md`

Alpha.24 potwierdziła brak nakładania komunikatów. Alpha.25 sprawdza przyjętą regułę: lista mieszana podaje rodzaj i, jeśli jest globalna, usługę; lista jednorodna nie powtarza rodzaju; po przejściu z wyszukiwania globalnego usługa występuje jednorazowo przed elementem.

## AMC-025-01 — Krótkie nazwy wyszukiwania

1. Przełącz się do TIDAL skrótem `Ctrl+1` i naciśnij `Ctrl+F`.
2. Zapisz wypowiedź NVDA po ustawieniu fokusu w polu, a następnie zamknij okno Escape.
3. Przełącz się do Apple Music skrótem `Ctrl+2`, ponownie naciśnij `Ctrl+F` i zapisz wypowiedź pola.
4. Zamknij okno, naciśnij `Ctrl+Shift+F` i zapisz wypowiedź pola wyszukiwania globalnego.

Oczekiwane wyniki:

- lokalne pole nazywa się odpowiednio „Szukaj w TIDAL” i „Szukaj w Apple Music”;
- globalne pole nazywa się „Szukaj we wszystkich usługach”;
- AMC nie dodaje zwrotów „w usłudze” ani „wyszukiwany tekst”; czytnik może standardowo podać rolę pola edycji.

## AMC-025-02 — Usługa przed elementem po Enter i Escape

1. W Apple Music otwórz wyszukiwanie globalne, wyszukaj `Zielony horyzont`, wybierz wynik Apple Music i naciśnij zwykły Enter.
2. Zapisz pełną pierwszą wypowiedź głównej listy.
3. Ponownie otwórz wyszukiwanie globalne, wyszukaj `Brzeg ciszy`, wybierz wynik TIDAL i naciśnij `Ctrl+Enter`.
4. Naciśnij Escape i zapisz pełną pierwszą wypowiedź głównej listy.
5. Przejdź strzałką na inny element.

Oczekiwane wyniki:

- po zwykłym Enter jedna nieprzerwana wypowiedź zaczyna się od „Apple Music”, a następnie podaje dane „Zielonego horyzontu”;
- po Escape jedna nieprzerwana wypowiedź zaczyna się od „TIDAL”, a następnie podaje dane „Brzegu ciszy”;
- w samych wynikach globalnych kolejność pozostaje odwrotna: najpierw dane elementu, a usługa na końcu;
- następny element głównej listy nie powtarza nazwy usługi.

## AMC-025-03 — Rodzaj tylko tam, gdzie rozróżnia elementy

1. Przełącz się do TIDAL skrótem `Ctrl+1`.
2. Otwórz Albumy skrótem `Ctrl+Shift+A` i zapisz etykietę pierwszego elementu.
3. Otwórz Playlisty skrótem `Ctrl+P` i zapisz etykietę pierwszego elementu.
4. Otwórz Ulubione skrótem `Ctrl+U` i sprawdź etykiety kilku elementów.
5. Otwórz Kolejkę skrótem `Ctrl+Q` i zapisz etykietę elementu.

Oczekiwane wyniki:

- Albumy zawierają tylko albumy, ale etykieta nie powtarza słowa „album”;
- Playlisty zawierają tylko playlisty, ale etykieta nie powtarza słowa „playlista”;
- Ulubione pozostają listą bieżącej usługi, nie powtarzają TIDAL, ale zachowują rodzaj, ponieważ docelowo mogą mieszać utwory, albumy i playlisty;
- Kolejka nie powtarza TIDAL, ponieważ należy do aktywnej sesji odtwarzania; zachowuje rodzaj, ponieważ docelowo może mieszać obsługiwane zasoby.

## Następne funkcje po tym zestawie

Po zatwierdzeniu etykiet następnym małym etapem będzie lokalna historia wyszukiwania: osobna dla każdej usługi i zakresu globalnego, do 20 unikatowych zapytań, wybierana strzałką w dół przy pustym polu. Następnie powstanie dostępna paleta poleceń pod `Ctrl+Shift+K`. Nadal pozostają niskopoziomowy konfigurowalny prefiks, instalator i bezpieczne aktualizacje, AMC.Host, WiiM oraz pierwsze logowanie OAuth.
