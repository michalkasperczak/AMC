# AMC Test alpha.477 — szkic instrukcji i opisu zmian

Status: roboczy tekst do korekty użytkownika. Lokalny kandydat z 5 listopada 2026, godz. 21:20. Nie jest wydaniem GitHub.

## Instrukcja użytkownika

- Na liście `Ctrl+,` otwiera ustawienia ogólne. W karcie **Komunikaty dostępności** można osobno wybrać pola czytane strzałkami góra/dół oraz lewą strzałką. Nazwa pozycji pozostaje czytana zawsze.
- `Ctrl+Alt+Enter` otwiera **Ustawienia sesji**. W sekcji **Zasady odczytu listy** można dziedziczyć ustawienia ogólne albo użyć własnych. Lista pól lewej strzałki pokazuje tylko informacje możliwe w tej sesji. Brakujących danych program nie zapowiada.
- W lokalnych plikach i nagraniach lewa strzałka podaje wybrane informacje w kolejności: długość, bitrate, rozmiar, następnie dalsze dostępne parametry. Na albumach TIDAL nie czyta zbędnego słowa „Album”.
- `Alt+Shift+R` otwiera historię nagrywania. Strzałki góra/dół poruszają się po gotowej liście. Otwieranie tego widoku nie odczytuje ponownie całego profilu Radia.
- W odtwarzaczu plików lokalnych, Spotify i wewnętrznym TIDAL `J` otwiera skok do czasu utworu, a `Shift+J` skok do procentu. `B` dodaje zakładkę, `Ctrl+Shift+B` nazwaną zakładkę. Radio na żywo nie ma osi czasu utworu.
- `Ctrl+H` otwiera historię odtwarzania danej sesji, a Page Up/Page Down przechodzą między utworami, gdy sesja obsługuje te polecenia.

## Szkic listy zmian

- TIDAL żąda najwyższej dostępnej jakości do Max i odtwarza obsługiwany FLAC DASH wewnątrz AMC; dla nieobsługiwanego wariantu wraca do High. Faktyczna jakość zależy od utworu i konta.
- Lista nagrań używa bieżącego stanu Radia, sprawdza tylko pliki potrzebne do pokazania najnowszych pozycji i nie sprawdza tej samej ścieżki dwa razy podczas otwarcia.
- Listy formatują trzy kolumny wiersza w jednym przebiegu; ruch kursora nie pobiera danych na nowo.
- Automatyczna seria odświeżania podcastów wybiera źródła i liczy nowe odcinki bez wczytywania całej biblioteki. Źródła bez rozdziałów są zapisywane w jednej operacji dla całej porcji; źródła z rozdziałami zachowują osobny zapis zakładek.

## Stan odbioru

Testy kodu, kontrolowany start paczki, instalacja i pomiary obciążenia przeszły. Szybkość oraz komunikaty w żywym NVDA wymagają potwierdzenia użytkownika. Spotify Lossless przez wewnętrzny tor AMC pozostaje osobnym zadaniem; obecny odtwarzacz Spotify używa Web Playback SDK.
