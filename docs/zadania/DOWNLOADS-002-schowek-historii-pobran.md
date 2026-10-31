# DOWNLOADS-002 — schowek Historii pobrań

- Korekta użytkownika: 31.10.2026, po wydaniu `alpha.455`. W Historii pobrań
  `Ctrl+C` ma kopiować ścieżkę, a `Ctrl+Shift+C` plik do wklejenia. Zastępuje
  wcześniejsze, domyślne działanie `Ctrl+C` kopiujące nazwę na tej liście.
- Wersja: AMC Test `0.1.0-alpha.456`.
- Status: implementacja i pełna regresja gotowe; wydanie, instalacja oraz
  odbiór NVDA do uzupełnienia.

## Zachowanie

- Globalne `Ctrl+J` i Widok → Historia pobrań nadal otwierają listę z każdej
  sesji i odtwarzacza. Strzałki, Enter, `F5` i Escape pozostają zgodne z
  zadaniem `DOWNLOADS-001`.
- Na liście `Ctrl+C` zapisuje pełną ścieżkę jako tekst. Obejmuje też ostatnio
  zapamiętaną ścieżkę brakującego pliku.
- `Ctrl+Shift+C` umieszcza istniejący plik w schowku Windows jako plik do
  wklejenia oraz tekst ścieżki. Brakujący plik nie jest pozorowany jako
  kopiowalny; użytkownik dostaje komunikat „Plik niedostępny. Ctrl+C skopiuje
  zapisaną ścieżkę”.
- Menu, menu kontekstowe, paleta `Ctrl+Shift+K`, pełna pomoc i `Ctrl+F1`
  używają nazw odpowiednich dla Historii pobrań. Po opuszczeniu tej listy
  wracają nazwy zwykłe. Inne widoki nadal kopiują nazwę przez `Ctrl+C`.
- Różnica wobec biblioteki stacji: lista jest lokalnym indeksem pobrań i nie
  zmienia serwisu Radio Archiwisty. Spotify pozostaje osobnym zadaniem.

## Testy i wydanie

- Testy celowane schowka, istniejącego i brakującego pliku, globalnego
  skrótu i opisów pomocy: 41 zaliczonych 31.10.2026.
- Pełna regresja 31.10.2026: **2215 zaliczonych, 143 pominięte,
  13 podtestów zaliczonych, 0 błędów**. Pierwszy przebieg wykrył brak dwóch
  tłumaczeń i sprzężenie atrapy wx z kolejnością testów. Oba problemy
  poprawiono przed przebiegiem końcowym.
- Build, instalator, GitHub i lokalna instalacja: do uzupełnienia.
- Ręczny odbiór z NVDA: oczekuje na próbę użytkownika po instalacji.

## Projekt instrukcji i opisu zmian

Instrukcja: „Otwórz Historię pobrań przez Ctrl+J. Ctrl+C kopiuje pełną
ścieżkę, a Ctrl+Shift+C kopiuje istniejący plik do wklejenia w innym folderze.
Enter odtwarza, lewa strzałka podaje szczegóły, Escape wraca do poprzedniego
miejsca.”

Opis zmian: „AMC Test dopasowuje schowek Historii pobrań do pracy na plikach:
kopiuje ścieżkę albo sam plik, także przy zaznaczeniu wielu pozycji.”
