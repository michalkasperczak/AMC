# PRESET — Podcasty i YouTube / Podcasts and YouTube

Data zgłoszenia i korekty: 28.11.2026. Dotyczy bieżącego AMC wxPython. Ta decyzja rozszerza wcześniejszy kontrakt presetów z 25.11.2026 na sesję Podcasty i YouTube. Stan: kod i testy w osobnym worktree; wynik kompilacji, instalacji, publikacji i ręcznego odbioru dopisuje integrator. Commit: zapisany w historii Git wraz z tą kartą.

## Zachowanie i skróty (PL)

- Ctrl+Alt+Shift+1…9,0 przypisuje zaznaczony element sesji do miejsca 1…10. Ctrl+Alt+Shift+P otwiera dotychczasowy wybór miejsca.
- Ctrl+Shift+1…9,0,-,= uruchamia miejsca 1…12. Działanie jest również w menu Presety i palecie poleceń; opisują je pełna pomoc i pomoc kontekstowa Ctrl+F1.
- Preset podcastu, kanału YouTube lub playlisty podcastów otwiera listę odcinków albo filmów. Preset pojedynczego odcinka lub filmu rozpoczyna jego odtwarzanie w AMC, bez przesuwania zaznaczenia listy.
- Odcinek jest zapisywany po trwałym identyfikatorze. Przy uruchomieniu program odczytuje jego bieżący adres z biblioteki. Jeśli element zniknął, mówi: „Odcinek lub film z presetu N nie jest już dostępny w bibliotece”. Brak źródła odtwarzania i błąd odczytu mają osobne komunikaty, bez identyfikatorów technicznych.
- Przypisanie niemożliwego elementu podaje rodzaje obsługiwane w tej sesji. Stara playlista podcastów zapisana jako „amcPlaylist” uruchamia listę podcastów, nie sesję Pliki.
- Presety TIDAL i Spotify oraz dotychczasowy sposób odtwarzania albumów i playlist pozostają bez zmian. Apple Music nie udostępnia tych komend. Profil Public nie zawiera Apple Music ani HEOS; profil Test obejmuje HEOS.

## Behavior and shortcuts (EN draft)

- Ctrl+Alt+Shift+1…9,0 saves the selected item in slots 1…10. Ctrl+Alt+Shift+P opens the existing slot picker.
- Ctrl+Shift+1…9,0,-,= runs slots 1…12. The Presets menu, command palette, full keyboard help, and contextual Ctrl+F1 help expose the same commands.
- A saved podcast, YouTube channel, or podcast playlist opens its item list. A saved episode or video starts playback in AMC without moving the list selection.
- Episodes are stored by stable ID and resolved again from the library when opened. Missing, unplayable, and unreadable episodes have spoken messages that do not expose internal IDs.
- Existing TIDAL and Spotify preset behavior is preserved. The Public profile excludes Apple Music and HEOS.

## Przyczyna, sprawdzenie i ograniczenia / Cause, checks, limits

Wybór celu presetu nie rozpoznawał wierszy „podcast” i „episode”; playlistę z tej sesji kierował do Plików. Uruchomienie pojedynczego odcinka musi pobrać bieżący rekord, aby nie utrwalić nieaktualnego URL. Ponadto aktywacja Ctrl+Shift+cyfra była routowana z klawiatury, ale nie miała pozycji w menu i palecie. Są to źródłowe przyczyny poprawki.

Celowane testy obejmują mapowanie celu, prawdziwy zapis SQLite filmu YouTube, otwarcie kontenerów, odtwarzanie filmu, zachowanie fokusu, komunikat brakującego celu, trasę starej playlisty, zgodność skrótów/menu/palety/pomocy oraz profile i katalogi PL/EN. Wynik: 73 testy zaliczone; git diff --check bez błędów. Test ręczny z NVDA oraz usługą YouTube/podcastów pozostaje do odbioru: pierwszy fokus, strzałki, zapis i anulowanie, powrót fokusu, ponowne otwarcie i słyszalny dźwięk. Nie ma nowej opcji ustawień dla zachowania albumów ani folderów; ta decyzja wymaga osobnego projektu.
