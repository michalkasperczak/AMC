# Kandydat aktualizacji AMC po alpha.512 — integracja

Stan 22.11.2026: gałąź `codex/alpha512-update-candidate-20261122`, commit kodu `5c249c68`, w osobnym worktree. Kod nie jest zainstalowany na głównym komputerze ani Hermesie i nie jest opublikowany jako wydanie GitHub.

## Zintegrowane przyrosty

1. `RADIO-LIVE-REC-20261122`: świeży adres wybranej stacji dla Ctrl+R, synchronizacja planów, komunikat startu dopiero po otwarciu pliku; commity `f21fb18d`, `e593a024`.
2. Hubert #32: wyszukiwanie Biblioteki powyżej 50 000 elementów; commit integracyjny `2579acaa` z `bf8992b4`.
3. Hubert #25: RSS przed pełnym odczytem kanałów YouTube; commit integracyjny `72a44502` z `616a4c93`.
4. Radio Archiwista: fokus kolejki po Delete; commit integracyjny `7da80d87` z `095fe4fd`.
5. Odświeżanie stacji YouTube i objaśnienie Ctrl+R w czasie planu; commit `cadc8ead`. Raport `YOUTUBE-RADIO-HERMANICE-20261122.md`.
6. Presety TIDAL oraz szybkie przypisanie `Ctrl+Alt+Shift+cyfra` w obsługiwanych sesjach; commity integracyjne `a01fbfe2` i `1d5e68b2` z istniejących `5f1297a7` i `a12aafb7`. Pełny kontrakt, etykiety i ograniczenia są w `PRESET-20261122-tidal-i-bezposrednie-przypisanie.md`. Kanoniczne źródła i niezależne zmiany robocze na D: pozostawiono bez zmian.
7. Radio Archiwista `RARCH-004`: ulubione całych stacji obok nagrań i lokalne nazwy F2; commity integracyjne `563aa802` i `25fe2834` z `218e25a8` i `818520da`. Raport `RARCH-004-ulubione-stacji-i-nazwy-lokalne.md`.
8. Hubert #22: Foldery Biblioteki bez zbędnego sortowania tytułów w SQL, przy zachowaniu `rowid` dla remisów; commit `12dd0c50`. Raport `HUBERT-22-20261122-foldery.md`.
9. Kolejka Podcastów i zapisanych filmów YouTube: Delete wybiera widocznego sąsiada, pojedynczo potwierdza wynik i ignoruje spóźnioną odpowiedź po zmianie widoku; commit `76b00d66`. Raport `QUEUE-DELETE-20261122-podcast-youtube.md`.
10. TIDAL: zachowanie znanej długości utworu w prywatnej Kolejce, ustawienie Entera listy/odtwarzacza, mowa tylko tytułu przy automatycznym przejściu i ignorowanie spóźnionego końca po nowym Enterze; commity `b6cc7c0b`, `0ed160af`. Raport `TIDAL-QUEUE-ENTER-20261122.md`.
11. Kolejki Spotify, TIDAL i Plików: Delete przy aktywnym filtrze wybiera widocznego sąsiada; commit `5c249c68`. Raport `QUEUE-FILTER-DELETE-20261122.md`.

## Weryfikacja

- Po połączeniu pierwszych czterech przyrostów celowane testy Pythona: **235 passed**; dwa zestawy Protocol Tests PASS; LiteHost Release zbudowany z **0 ostrzeżeń, 0 błędów**.
- Pełne `PYTHONPATH=wxlite python -m pytest wxlite/tests -q`: **2880 passed, 148 skipped, 30 failed, 34 subtests passed**. Dokładnie te same 30 niepowodzeń wystąpiło na wcześniejszej gałęzi odniesienia bez zintegrowanych poprawek; porównano 14 odpowiadających plików, wynik **212 passed, 1 skipped, 30 failed**. Integracja nie wprowadziła nowych niepowodzeń w tym porównaniu, ale pełny zestaw nie przechodzi. Wśród niepowodzeń są m.in. kopie zapasowe, i18n, instalator, menu, GUI Radio Archiwisty, opcje sesji, Sonos/Spotify/TIDAL oraz opis pomocy timeshift. Część może dotyczyć atrap testowych, ale nie uznawano ich zbiorczo za nieistotne.
- Po commicie `cadc8ead` celowany zestaw nagrywania i YouTube: **67 passed, 2 subtests passed**; protokół publiczny **4 scenariusze PASS**, protokół konta YouTube **9 scenariuszy PASS**; LiteHost Release **0 ostrzeżeń, 0 błędów**.
- Po dołączeniu presetów: testy `test_radio_recording.py`, `test_profile_presets.py`, `test_preset_gui_wiring.py`, `test_shortcuts.py`, `test_tidal_navigation.py`, `test_menu_exposes_existing_actions.py`, `test_command_palette.py`, `test_i18n.py`: **228 passed, 1 skipped, 2 failed**. Dwa niepowodzenia dotyczą starszych testów pełnej inwentaryzacji angielskich tekstów i statycznych tekstów okna ustawień; raport presetów z kanonicznej gałęzi wskazywał je przed integracją. Testy presetów i ich poleceń w tym przebiegu przeszły. Nie wykonano jeszcze builda ani pełnego zestawu po dołączeniu presetów; wcześniejsze wyniki builda nie są dowodem dla nowego HEAD.
- Po dołączeniu ulubionych stacji: połączony zestaw nagrywania, Radio Archiwisty, presetów, menu, palety i skrótów: **242 passed**. Nie wykonano jeszcze builda, pełnego zestawu ani widocznego GUI dla tego HEAD.
- Po dołączeniu lokalnych nazw F2: połączony zestaw nagrywania, YouTube, Radio Archiwisty, presetów, menu, palety i skrótów: **267 passed, 2 subtests passed**. Nadal bez widocznego GUI, NVDA, instalacji, pełnego zestawu i builda po tym commicie.
- Po poprawce Folderów #22: celowane testy #22/#21/Biblioteki **8 passed, 14 skipped**; szerszy zestaw dziewięciu plików **114 passed, 49 skipped, 2 subtests passed**. Na syntetycznej bazie 60 000 plików korzeń spadł z 4,986 s do 0,249 s, a folder z 8,766 s do 0,544 s; skróty wyniku przed i po były zgodne. Pełny zestaw, build i NVDA nadal niewykonane dla tego HEAD.
- Po poprawce Delete Podcastów/YouTube: **6 nowych testów passed** oraz **26 testów nawigacji i członkostwa kolejki passed**. Szerszy zestaw z prawdziwym hostem nie zakończył się po 32 początkowych testach i został przerwany; nie zaliczono go. Pełny zestaw, build i NVDA nadal niewykonane dla obecnego HEAD.
- Po poprawce TIDAL: **138 passed** w zestawie czasu Kolejki; **115 passed, 1 deselected** w zestawie Entera i mowy. Wyłączony test pomocy kontekstowej pada identycznie na niezmienionej gałęzi kanonicznej z powodu niepełnej atrapy GUI. Nadal bez próby na koncie, NVDA, pełnego zestawu i builda dla obecnego HEAD.
- Po poprawce filtrów kolejek: **25 passed** w zestawie członkostwa, czasów i Delete. Odbiór rzeczywistego hosta i NVDA pozostaje otwarty; nadal bez pełnego zestawu i builda dla obecnego HEAD.
- Nie powtarzano pełnego zestawu po małej zmianie YouTube i Ctrl+R. Nie wykonano prób instalacji, NVDA ani prawdziwego nagrania nowym kodem. Pełny test w poprzedniej próbie dopisał wpisy testowe do lokalnego dziennika hosta między 16:46 a 16:48; nie traktować ich jako logu incydentu użytkownika. Kolejne testy wymagające hosta należy izolować od bieżącego profilu.

## Ograniczenia i kolejność

Przed wydaniem należy rozstrzygnąć 30 niepowodzeń pełnego zestawu, wykonać próbę na odrębnym profilu i sprawdzić działanie z NVDA. Instalację planować dopiero po potwierdzeniu braku aktywnego nagrania. Nie przenosić tej gałęzi na działającą instalację tylko na podstawie kompilacji. Po wydaniu poinformować Huberta w zgłoszeniach dotyczących faktycznie wydanych poprawek. Następne przyrosty: pozostałe kolejki i wyjścia audio. Nagrania Dwójki i Apple Music pozostają na później zgodnie z decyzją Michała.
