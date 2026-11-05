# Szkic instrukcji i listy zmian AMC Test alpha.473

Do korekty i akceptacji użytkownika; nie publikować automatycznie jako gotowej instrukcji.

## Instrukcja użytkownika

W TIDAL wybierz utwór i z menu kontekstowego wybierz „Odtwórz utwór TIDAL w AMC Test”. Program żąda najlepszej dostępnej jakości do Max. Po starcie odczytuje tytuł i potwierdzoną przez TIDAL jakość, np. „Jakość Max, FLAC 24 bit, 192 kHz”. Jeśli utwór nie jest dostępny w Max albo jego manifest jest nieobsługiwany, może zostać odtworzony w High; komunikat podaje uzyskany format. Enter na zwykłej liście nadal otwiera oryginalny TIDAL.

W odtwarzaczu Plików, Spotify i TIDAL naciśnij `J`, aby wpisać czas `mm:ss` lub `hh:mm:ss`, albo `Shift+J`, aby wpisać procent. `B` dodaje zakładkę, `Ctrl+Shift+B` zakładkę nazwaną. `Page Up` i `Page Down` przechodzą do poprzedniego i następnego utworu z listy źródłowej. `Ctrl+H` otwiera historię bieżącej sesji; Escape wraca do poprzedniego miejsca. Na liście album TIDAL powinien być czytany jako nazwa, wykonawca i czas, bez osobnego słowa „album”.

## Lista zmian

- TIDAL Max FLAC przez lokalny manifest DASH z przewijaniem i komunikatem jakości.
- Powrót do jakości High, gdy wariant Max nie może być obsłużony.
- Krótszy odczyt albumu na liście TIDAL.
- Natywne odtworzenia TIDAL trafiają do `Ctrl+H` po udanym starcie.
- Początek następnego i poprzedniego utworu TIDAL bez czterosekundowego narastania głośności.

Spotify nadal korzysta z oficjalnego odtwarzacza przeglądarkowego, którego jakość nie odpowiada trybowi Lossless desktopowej aplikacji Spotify.
