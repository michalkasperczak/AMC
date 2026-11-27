# AMC Test alpha.533 — wydanie 27.11.2026

Status: opublikowany prerelease Test; żywa instalacja i odbiór NVDA otwarte.

- Źródła i tag: `7e8afda585a2b9dbf4a8050a59e80640bab49638`, gałąź `codex/heos-next-test-20261123`, tag `test-2026.11.27.2342`.
- GitHub Actions: run `46078107196`, artifact `15235003886`; wszystkie etapy zakończone sukcesem. Profil `test`, wersja `0.1.0-alpha.533`, `secretScan=passed`.
- Lokalny instalator: `D:\Projekty Codex\AMC - integracja wxPython\wydania\AMC-Test-alpha533-20261127\AMC-Test-2026-11-27-2342.exe`, 214670480 B, SHA-256 `f41cd873e167d79cb9de2107e427fa656c0009b2e938f922e0efa20fb9c79147`.
- Wydanie GitHub: `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.27.2342`. `draft=false`, `prerelease=true`. Tag wskazuje commit źródeł, a nie późniejszy commit dokumentacji.
- Cztery załączniki: EXE, sidecar integralności, manifest kanału Test i dowód kompilacji. Zdalny digest i rozmiar EXE zgadzają się z lokalną kopią. Trzy JSON-y pobrano z wydania i porównano bajt po bajcie. Manifest przeszedł walidator aktualizatora AMC.

Testy przed budową: 202 testy Python dla komend, kolejek, wyszukiwarki i szablonu językowego; celowany test C# `QueueCoordinatorTests`; `dotnet build` hosta bez ostrzeżeń i błędów. `git diff --check` czysty. Pełne starsze `test_gui_logic.py`, `test_podcast_source.py` i `test_i18n.py` nie są zaliczone; szczegóły w [QUEUE-004](QUEUE-004-20261125-shift-enter-nastepne.md).

Instalatora nie uruchamiano ani nie instalowano. W trakcie pracy na głównym komputerze działało AMC odtwarzające Radio oraz NVDA. Nie przełączano ani nie zamykano tej sesji. Użytkownik instaluje sam; po instalacji sprawdzić klawiaturą i NVDA: początkowy wiersz, kolejne wybory, duplikat, kilka zaznaczeń, menu, `Ctrl+F1`, pełną pomoc, paletę, powrót fokusu i ponowne otwarcie wyników wyszukiwania. HEOS wymaga późniejszej próby na fizycznym głośniku i nie ma tego gestu w alpha.533.

Szkic instrukcji i listy zmian: [ALPHA533-Test-instrukcja-i-lista-zmian-szkic.md](ALPHA533-Test-instrukcja-i-lista-zmian-szkic.md), do korekty i zatwierdzenia użytkownika; bez automatycznej publikacji.
