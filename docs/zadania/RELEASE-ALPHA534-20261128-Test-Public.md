# AMC Test i Public alpha.534 — wydanie GitHub / GitHub release

Data: 28.11.2026. Źródło: `3f8e51cbe78060aafbe01e32d79117fd9b827e33`, gałąź `codex/heos-next-test-20261123`, wersja `0.1.0-alpha.534`. GitHub Actions `46087861794`: oba zadania zakończone sukcesem. Test jest prerelease, Public jest stabilnym Latest. Zdalnie sprawdzono profil, wersję, commit, komplet czterech zasobów każdego wydania, ich rozmiary i SHA-256. Oba manifesty aktualizacji przeszły walidator.

- Test: `test-2026.11.28.0128`, `AMC-Test-2026-11-28-0128.exe`, 214662737 bajtów, SHA-256 `c20b8db555d2f3bc3e1fad09d97e7d18cae7a6ec9ecc6428e3de4d00fa7635f4`.
- Public: `public-2026.11.28.0128`, `AMC-Public-2026-11-28-0128.exe`, 134099151 bajtów, SHA-256 `a59aebaf874be4a9dd1b4ff03c17710f6e3781b638bce7a2cd0712f8ae80506d`.

Zmiany: presety Podcastów i YouTube, Ctrl+Z Ulubionych, kolejka i Shift+Enter, fokus po Ctrl+Shift+L/Ctrl+D, potwierdzenie rozpoczęcia nagrania, bitrate TIDAL w NVDA+End, dziedziczenie komunikatów sesji przez Ctrl+Alt+Enter. Public nie zawiera Apple Music ani HEOS; Test je zawiera. Testy źródeł przeszły: 44, 54, 27, 90 i 77 testów Pythona w zestawach celowanych oraz 2 zestawy C# protokołu/kolejki; lokalny build hosta Release zakończył się bez błędów i ostrzeżeń. Rzeczywisty odsłuch NVDA, TIDAL i urządzeń pozostaje do odbioru. AirPlay odłożono do próby za dnia z użytkownikiem. Szkic instrukcji PL/EN: `ALPHA534-instrukcja-i-lista-zmian-szkic.md`, bez automatycznej publikacji jako zatwierdzona instrukcja.

## Korekta i stan instalacji / Correction and installation

28.11.2026: próba aktualizacji zainstalowanego AMC Public alpha.507 wykryła, że aktualizator alpha.534 odrzucał starszą, zgodną listę możliwości profilu jeszcze przed uruchomieniem instalatora (exit 4). Poprawka `1a28b0e3` i jej 49 testów są w następnym wydaniu alpha.535; to późniejsze ustalenie zastępuje założenie, że sama walidacja instalatora dowodzi przejścia ze starszej wersji. Po zbudowaniu poprawionego aktualizatora ze źródła alpha.535 rzeczywista instalacja Public alpha.507 → alpha.534 zakończyła się kodem 0. Lokalny manifest Public wskazuje profil `public`, wersję `0.1.0-alpha.534` i commit powyżej. Instalacja Test alpha.534 nie była wykonywana; Test otrzyma wydanie korekcyjne alpha.535.

Również 28.11.2026 potwierdzono, że Ctrl+Shift+Enter nadal było czynne w alpha.534 mimo wcześniejszej decyzji użytkownika o jego wycofaniu. Korekta kontraktu należy do alpha.535. GitHub alpha.534 pozostaje historycznym, opublikowanym wydaniem, które alpha.535 ma zastąpić jako najnowsze.

English: Both profiles were built and published from the exact source commit. Public alpha.507 upgraded successfully to alpha.534 only with the subsequently fixed updater. The released alpha.534 updater rejected older valid Public capability lists, and Ctrl+Shift+Enter remained active contrary to the user's earlier decision. Both corrections are assigned to alpha.535. Live NVDA, TIDAL, and device acceptance remains open.

Archiwum pobranych pakietów i dowodów: `D:\Projekty Codex\AMC - integracja wxPython\work\release-alpha534-20261128`. Zachować do odbioru użytkownika.