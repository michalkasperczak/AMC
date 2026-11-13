# AM-006 — projekt instrukcji i listy zmian

**Roboczy tekst do korekty użytkownika. Funkcja nie jest jeszcze zainstalowana ani wydana.**

## Instrukcja

W sesji Apple Music otwórz „Utwory” lub playlistę i zaznacz utwór. Pierwsza pozycja menu kontekstowego „Otwórz utwór w oryginalnym Apple Music” przekazuje link do zainstalowanej aplikacji. Apple Music może wtedy pokazać album bez uruchomienia zaznaczonego utworu. Obecne działanie Entera jest takie samo; wymaga jeszcze zmiany, aby spełniało oczekiwanie odtwarzania w tle. Gdy utwór nie ma identyfikatora katalogowego, AMC poinformuje, że nie może go otworzyć.

Jeśli w oryginalnym Apple Music gra już muzyka, Spacja w AMC wstrzymuje lub wznawia odtwarzanie, Shift+Page Down przechodzi do następnego utworu, a Shift+Page Up do poprzedniego. Odczyt stanu podaje tytuł zgłoszony przez oryginalną aplikację. Przewijanie do wskazanego czasu nie jest dostępne. Funkcja jest przeznaczona tylko dla AMC Test i pozostaje niezainstalowana.

## Lista zmian

- Polecenie otwierania utworu Apple Music jest pierwszą pozycją menu kontekstowego; Enter obecnie korzysta z tego samego przekazania linku.
- Dodano sterowanie bieżącą sesją oficjalnej aplikacji przez Spację i Shift+Page Down/Up.
- Odczyt „Teraz odtwarzane” pochodzi z rzeczywistego stanu Windows, a nie z zaznaczonego wiersza AMC.
- Dla utworów Biblioteki bez gotowego łącza AMC tworzy bezpieczny publiczny adres z identyfikatora katalogowego.
- Komunikaty i dostępność polecenia uwzględniają rodzaj zaznaczenia i obecność oryginalnej aplikacji.

**Przed wydaniem jako odtwarzania Apple Music:** uruchomić dokładnie wskazany utwór bez przełączania okna, sprawdzić pełną długość i komunikaty NVDA, a następnie zbudować i przetestować pakiet. Dotychczasowe przekazanie linku nie zalicza tej bramki.
