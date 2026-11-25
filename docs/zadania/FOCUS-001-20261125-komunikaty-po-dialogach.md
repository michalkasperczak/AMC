# FOCUS-001 — wynik zapisu po powrocie fokusu

Stan 25.11.2026: zmiana w źródłach AMC Test, bez kompilacji, wydania GitHub, instalacji i odbioru NVDA. Wersja zainstalowana: alpha.527. Zmiana ma wejść do wspólnego następnego wydania testowego.

## Powód i zachowanie

Po zamknięciu okna zapisu lub eksportu niektóre drogi wypowiadały wynik przed ustawieniem fokusu z powrotem na listę albo odtwarzacz. NVDA może wtedy zastąpić wynik odczytem fokusu lub tytułu głównego okna. To ta sama klasa ryzyka, którą Michał zgłosił przy harmonogramach; nie ma potwierdzenia, że każda z niżej wymienionych dróg faktycznie była zagłuszana w jego zainstalowanym AMC.

Wynik po udanym lub nieudanym zapisie przechodzi teraz przez wspólną metodę: następny obrót pętli wx przywraca fokus, kolejny wypowiada wynik, o ile okno nadal istnieje. Przy odmowie eksportu WiiM i Sonos fokus również wraca po oknie wyboru pliku. Nie zmieniono danych, ścieżek eksportu ani sposobu odtwarzania.

| Droga | Polecenie i wynik dla NVDA |
| --- | --- |
| Sesja WiiM, eksport strumieni | `Ctrl+E`; „Wyeksportowano strumienie WiiM: {liczba}” lub „Nie można zapisać playlisty: {powód}”. |
| Sesja Sonos, Moje stacje, eksport | `Ctrl+E`; „Wyeksportowano stacje Sonos: {liczba}” lub „Nie można zapisać playlisty: {powód}”. |
| Sesja Podcasty, Ustawienia programu → Wybierz źródła skrzynki podcastów | „Zapisano źródła skrzynki podcastów: {liczba}” lub „Nie zapisano źródeł skrzynki podcastów: {powód}”. Gdy otwarta jest Skrzynka, jej odświeżenie zachowuje dotychczasową osobną drogę. |
| Sesja Podcasty, `Alt+Shift+Enter`, Opcje odtwarzania elementu | „Zapisano opcje podcastu/odcinka: {tytuł}” lub „Nie zapisano opcji odtwarzania: {powód}”. Dodatkowa informacja o ustawieniach obowiązujących od następnego otwarcia pozostaje. |

Żadna z tych dróg nie dodaje nowego skrótu. Dostępność w menu, palecie `Ctrl+Shift+K` i pomocy `Ctrl+F1` pozostaje taka sama. Przy ponownym otwarciu modalnego okna jego fokus i etykiety są bez zmian. Jeśli okno główne zniknie przed opóźnioną wypowiedzią, komunikat nie jest wysyłany do nieistniejącej kontrolki.

## Sprawdzenie i ograniczenia

`test_podcast_playback_options.py` i `test_radio_gui_wiring.py`: 22 testy zaliczone na atrapie wx. Test sprawdza kolejność fokus → wypowiedź oraz brak spóźnionej wypowiedzi po zamknięciu okna. Nie jest to test prawdziwej mowy NVDA. Nie uruchamiano eksportu na urządzeniach ani nie tworzono plików eksportowych. Przed wydaniem potrzebny jest celowany test w rzeczywistym wx/NVDA oraz zwykła bramka kompilacji wydania.

## Szkic instrukcji i listy zmian do korekty Michała

Po zapisaniu opcji odtwarzania lub źródeł skrzynki oraz po eksporcie stacji AMC przywraca fokus i odczytuje wynik operacji. Przy błędzie odczytuje przyczynę; nie potwierdza zapisu, którego nie było. Ten tekst jest szkicem do przyszłej wspólnej aktualizacji, nie opisem obecnej instalacji.

## Migracja, wersja i commit

Migracja ustawień i danych nie jest potrzebna. Commit implementacji: `259be2ff46ee05d2b088cc34ab98b130c5661714`; brak numeru nowego wydania.
