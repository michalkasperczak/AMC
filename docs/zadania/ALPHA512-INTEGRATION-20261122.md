# Kandydat aktualizacji AMC po alpha.512 — integracja

Stan 22.11.2026: gałąź `codex/alpha512-update-candidate-20261122`, HEAD `563aa802`, w osobnym worktree. Kod nie jest zainstalowany na głównym komputerze ani Hermesie i nie jest opublikowany jako wydanie GitHub.

## Zintegrowane przyrosty

1. `RADIO-LIVE-REC-20261122`: świeży adres wybranej stacji dla Ctrl+R, synchronizacja planów, komunikat startu dopiero po otwarciu pliku; commity `f21fb18d`, `e593a024`.
2. Hubert #32: wyszukiwanie Biblioteki powyżej 50 000 elementów; commit integracyjny `2579acaa` z `bf8992b4`.
3. Hubert #25: RSS przed pełnym odczytem kanałów YouTube; commit integracyjny `72a44502` z `616a4c93`.
4. Radio Archiwista: fokus kolejki po Delete; commit integracyjny `7da80d87` z `095fe4fd`.
5. Odświeżanie stacji YouTube i objaśnienie Ctrl+R w czasie planu; commit `cadc8ead`. Raport `YOUTUBE-RADIO-HERMANICE-20261122.md`.
6. Presety TIDAL oraz szybkie przypisanie `Ctrl+Alt+Shift+cyfra` w obsługiwanych sesjach; commity integracyjne `a01fbfe2` i `1d5e68b2` z istniejących `5f1297a7` i `a12aafb7`. Pełny kontrakt, etykiety i ograniczenia są w `PRESET-20261122-tidal-i-bezposrednie-przypisanie.md`. Kanoniczne źródła i niezależne zmiany robocze na D: pozostawiono bez zmian.
7. Radio Archiwista `RARCH-004`, etap 1: ulubione całych stacji obok nagrań; commit integracyjny `563aa802` z `218e25a8`. F2 z lokalnymi nazwami pozostaje otwarte. Raport `RARCH-004-ulubione-stacji-i-nazwy-lokalne.md`.

## Weryfikacja

- Po połączeniu pierwszych czterech przyrostów celowane testy Pythona: **235 passed**; dwa zestawy Protocol Tests PASS; LiteHost Release zbudowany z **0 ostrzeżeń, 0 błędów**.
- Pełne `PYTHONPATH=wxlite python -m pytest wxlite/tests -q`: **2880 passed, 148 skipped, 30 failed, 34 subtests passed**. Dokładnie te same 30 niepowodzeń wystąpiło na wcześniejszej gałęzi odniesienia bez zintegrowanych poprawek; porównano 14 odpowiadających plików, wynik **212 passed, 1 skipped, 30 failed**. Integracja nie wprowadziła nowych niepowodzeń w tym porównaniu, ale pełny zestaw nie przechodzi. Wśród niepowodzeń są m.in. kopie zapasowe, i18n, instalator, menu, GUI Radio Archiwisty, opcje sesji, Sonos/Spotify/TIDAL oraz opis pomocy timeshift. Część może dotyczyć atrap testowych, ale nie uznawano ich zbiorczo za nieistotne.
- Po commicie `cadc8ead` celowany zestaw nagrywania i YouTube: **67 passed, 2 subtests passed**; protokół publiczny **4 scenariusze PASS**, protokół konta YouTube **9 scenariuszy PASS**; LiteHost Release **0 ostrzeżeń, 0 błędów**.
- Po dołączeniu presetów: testy `test_radio_recording.py`, `test_profile_presets.py`, `test_preset_gui_wiring.py`, `test_shortcuts.py`, `test_tidal_navigation.py`, `test_menu_exposes_existing_actions.py`, `test_command_palette.py`, `test_i18n.py`: **228 passed, 1 skipped, 2 failed**. Dwa niepowodzenia dotyczą starszych testów pełnej inwentaryzacji angielskich tekstów i statycznych tekstów okna ustawień; raport presetów z kanonicznej gałęzi wskazywał je przed integracją. Testy presetów i ich poleceń w tym przebiegu przeszły. Nie wykonano jeszcze builda ani pełnego zestawu po dołączeniu presetów; wcześniejsze wyniki builda nie są dowodem dla nowego HEAD.
- Po dołączeniu ulubionych stacji: połączony zestaw nagrywania, Radio Archiwisty, presetów, menu, palety i skrótów: **242 passed**. Nie wykonano jeszcze builda, pełnego zestawu ani widocznego GUI dla tego HEAD.
- Nie powtarzano pełnego zestawu po małej zmianie YouTube i Ctrl+R. Nie wykonano prób instalacji, NVDA ani prawdziwego nagrania nowym kodem. Pełny test w poprzedniej próbie dopisał wpisy testowe do lokalnego dziennika hosta między 16:46 a 16:48; nie traktować ich jako logu incydentu użytkownika. Kolejne testy wymagające hosta należy izolować od bieżącego profilu.

## Ograniczenia i kolejność

Przed wydaniem należy rozstrzygnąć 30 niepowodzeń pełnego zestawu, wykonać próbę na odrębnym profilu i sprawdzić działanie z NVDA. Instalację planować dopiero po potwierdzeniu braku aktywnego nagrania. Nie przenosić tej gałęzi na działającą instalację tylko na podstawie kompilacji. Po wydaniu poinformować Huberta w zgłoszeniach dotyczących faktycznie wydanych poprawek. Kolejny otwarty przyrost kodu: `RARCH-004` etap 2, lokalne nazwy F2; osobno foldery Biblioteki #22 i pozostałe kolejki. Nagrania Dwójki i Apple Music pozostają na później zgodnie z decyzją Michała.
