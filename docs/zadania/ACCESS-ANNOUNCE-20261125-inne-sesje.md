# ACCESS-ANNOUNCE — zapowiedzi nowych utworów

Stan 25.11.2026: kod i testy celowane gotowe do odbioru; bez kompilacji, wydania, instalacji i odbioru NVDA. Ta zmiana **nie jest** częścią wydanego AMC Test alpha.529.

## Zgłoszenie i korekta decyzji

TIDAL mówi tytuł kolejnego utworu, gdy użytkownik pracuje w innej sesji, np. w lokalnej kolejce. Podobnie mogą przerywać odczyt metadane Radia i automatyczne przejścia innych źródeł. Użytkownik chce decydować osobno dla każdej sesji, z prostym domyślnym ustawieniem globalnym. Wypowiedź z 25.11.2026 zastępuje wcześniejszy pomysł **wyłącznie globalnego** przełącznika. Otwieranie odtwarzacza po Enter, automatyczna mowa i zawartość tytułu okna są odrębnymi decyzjami.

## Dostępny interfejs i zapis

- **Ustawienia programu → Dostępność i komunikaty → Automatyczne zapowiedzi nowych utworów**: „Zawsze oznajmiaj nowy utwór”, „Oznajmiaj tylko w bieżącej sesji”, „Nie oznajmiaj automatycznie”. Domyślnie „Zawsze”, aby nie zmienić istniejących profili bez wyboru użytkownika.
- **Ctrl+Alt+Enter → Ustawienia sesji → Automatyczne zapowiedzi nowych utworów tej sesji**: te same trzy warianty i „Jak ustawienie ogólne: [bieżący wariant]”. Nowa sesja dziedziczy ustawienie globalne, dopóki jej zdolności nie udostępnią tego pola i użytkownik nie zapisze własnego wyboru.
- W tym samym oknie osobne pole **Po uruchomieniu materiału Enterem** decyduje o przejściu do odtwarzacza. W Podcastach Alt+Shift+Enter pozostaje opcjami **wybranego podcastu lub odcinka**. Nie przejmujemy tego skrótu dla ustawień całej sesji.
- **Ustawienia programu → Ogólne → Tytuł odtwarzanego utworu w tytule okna** już pozwalają wybrać „Nie pokazuj w tytule okna”. To ustawienie jest globalne; wyłącza tytuł także dla metadanych Radia. Mowa automatyczna go nie przestawia. Odczyt na żądanie i błędy pozostają dostępne.

## Mapa zdarzeń do odbioru

| Sesja | Samoczynna zmiana dziś | Wykonawca preferencji | Odbiór do wykonania |
| --- | --- | --- | --- |
| Radio | radio.nowPlaying, metadane strumienia | bramka przed wypowiedzią; pasek i tytuł okna nadal się aktualizują | zmiana metadanych przy oglądaniu Radia i innej sesji; NVDA+T i NVDA+End |
| TIDAL natywny | kolejny utwór adaptera | bramka przed wypowiedzią tytułu | trzy tryby, widok listy/odtwarzacza, fokus poza TIDAL |
| Spotify | naturalny następny utwór oznaczony w zamiarze AMC | wypowiedź tytułu po potwierdzonym starcie zależna od preferencji; ręczne potwierdzenie pozostaje | naturalny następny utwór i ręczny Enter, także poza sesją |
| Pliki | queue.advanced i przejście po końcu źródła | stan kolejki zmienia się zawsze; tylko wypowiedź jest filtrowana | kolejka i źródło, pauza, stan bieżącego elementu |
| Podcasty | przejście do następnego odcinka w źródle | wypowiedź jest filtrowana | odtwarzacz otwarty i w tle; ręczne uruchomienie |
| Radio Archiwista | przejście do kolejnego nagrania | wypowiedź tytułu jest filtrowana | ciąg nagrań, odtwarzacz i lista |
| Sonos, WiiM, HEOS, Frontier Smart | odczyt stanu urządzenia, bez potwierdzonej analogicznej wypowiedzi automatycznej | brak nowej kontrolki, dopóki nie ma wykonawcy | osobny odbiór statusu i tytułu okna; HEOS z fizycznym głośnikiem jutro |
| Apple Music | odłożone na prośbę użytkownika 25.11.2026 | brak nowej kontrolki | po wznowieniu prac nad sesją |

Zapowiedź nie może przenosić fokusu ani zaznaczenia do innej sesji. Komunikaty o błędach i nagrywaniu pozostają niezależne od tego wyboru. Ctrl+Alt+Enter jest już w menu, palecie i pomocy; opis polecenia uzupełniono o zapowiedzi.

## Testy i ograniczenia

Celowany zestaw logiki ustawień, zapisu profilu, dialogu, Radia, TIDAL, Spotify, lokalnej kolejki, skrótów, menu i palety: **280 passed** w środowisku wxPython 4.3.1; dodatkowe dwa testy naturalnego przejścia Plików: **2 passed**. `py_compile` i `git diff --check` przeszły. Osobny szerszy zestaw wykazał dwa istniejące błędy testów startu hosta w test_session_options_effect.py oraz dwa istniejące braki w pełnym katalogu tłumaczeń; nie uznajemy tych testów za zaliczone. Nie wykonano jeszcze ręcznej nawigacji NVDA po nowym polu, odbioru wszystkich zdarzeń na żywo ani instalacji. Spotify wymaga potwierdzenia na rzeczywistym odtwarzaniu.

Przed wydaniem trzeba sprawdzić pierwszy wybór, wszystkie pozycje strzałkami, zapis, anulowanie, powrót fokusu i ponowne otwarcie w każdym z sześciu okien sesji; przy źródle zewnętrznym sprawdzić działanie bez przejmowania klawiatury. Tytuł okna ma dziś wybór globalny, nie osobny dla sesji. Przegląd strzałek, Ulubionych, playlist, kolejek oraz Ctrl+X jest odrębnym kontraktem dostępnego interfejsu i pozostaje otwarty.

## Szkic instrukcji użytkownika

„W Ustawieniach programu, na karcie Dostępność i komunikaty, wybierz domyślną zapowiedź nowych utworów. W konkretnej sesji naciśnij Ctrl+Alt+Enter, aby ją zmienić tylko dla tego źródła. Otwieranie odtwarzacza po Enter ustawiasz osobno w tym samym oknie. Jeśli chcesz ukryć utwór w tytule okna, wybierz odpowiednią pozycję na karcie Ogólne.” Szkic wymaga korekty użytkownika po odbiorze NVDA; nie publikować automatycznie.

Commit źródeł i testów: `6e96acc258a8b189b9e62b07dd9e8d4fdcd51ce1`. Wersja wydania: nieprzypisana; ta zmiana czeka na wspólne następne wydanie Test.
