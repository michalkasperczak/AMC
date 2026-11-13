# AM-006 — projekt instrukcji i listy zmian

**Roboczy tekst do korekty użytkownika. Funkcja nie jest jeszcze zainstalowana ani wydana.**

## Instrukcja

W sesji Apple Music otwórz „Utwory” lub playlistę i zaznacz utwór. Naciśnij Enter albo z menu Biblioteka bądź menu kontekstowego wybierz „Otwórz utwór w oryginalnym Apple Music”. AMC przekazuje utwór do zainstalowanej aplikacji Apple Music. Po przełączeniu do niej sprawdź, czy rozpoczęła odtwarzanie. Gdy utwór nie ma identyfikatora katalogowego, AMC poinformuje, że nie może go otworzyć.

Polecenie jest dostępne tylko w AMC Test. Otwieranie utworu w oficjalnej aplikacji nie daje jeszcze AMC przycisków pauzy, przewijania, kolejki ani wyboru jej wyjścia audio.

## Lista zmian

- Dodano otwieranie utworu Apple Music przez Enter oraz równoważne polecenie menu i palety.
- Dla utworów Biblioteki bez gotowego łącza AMC tworzy bezpieczny publiczny adres z identyfikatora katalogowego.
- Komunikaty i dostępność polecenia uwzględniają rodzaj zaznaczenia i obecność oryginalnej aplikacji.

**Przed wydaniem:** sprawdzić, czy wskazany utwór rzeczywiście otwiera się i gra w aplikacji Apple, odsłuchać komunikaty NVDA i zbudować oraz przetestować pakiet.
