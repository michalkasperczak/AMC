# Aktualizator starszej instalacji Public / Older Public updater compatibility

## PL — zapis zadania, 28 listopada 2026

- Źródło: commit `1a28b0e31117797a00a9b0b7541b0429808488be`; poprawka przeznaczona do wydania po `alpha.534`.
- Zachowanie: instalator właściwego profilu może zaktualizować starszą instalację AMC Public z mniejszą listą znanych możliwości. Tożsamość produktu, identyfikator instalacji, kanał, katalogi danych i przestrzeń poświadczeń nadal muszą być zgodne. Obcy profil i nieznane możliwości są odrzucane.
- Wejście: `AMC-Python-Updater.exe --installer PLIK.EXE --installer-sha256 SUMA --target KATALOG --silent`; nie ma nowego skrótu, menu ani ustawienia. AMC Test i AMC Public mają osobne profile i blokady aktualizacji.
- Dostępność: brak nowej kontrolki i komunikatu NVDA. Wcześniejsza odmowa pakietu przed uruchomieniem instalatora powinna zniknąć tylko dla zgodnych starszych instalacji. Błędy nadal zatrzymują aktualizację przed podmianą plików.
- Powód: w trybie instalatora punkt wejścia wymagał od *już zainstalowanego* manifestu dokładnej obecnej listy możliwości, choć istniejący kontrakt zgodności dopuszcza starszy podzbiór. Dla paczki źródłowej zostawiono ścisłą walidację obecnego profilu.
- Sprawdzenie: `dotnet run --project tests/AccessibleMediaController.PythonLauncher.Tests/AccessibleMediaController.PythonLauncher.Tests.csproj -c Release --no-launch-profile` — 49/49 testów bez interfejsu zaliczonych, w tym starszy Public bez `local-video-preview`, obcy `installId` i podszycie pod profil. Test runner został zbudowany podczas tego polecenia. Nie wykonano w tej części instalacji ani próby NVDA; te wyniki trzeba zapisać osobno po wydaniu.
- Migracja: nie zmienia danych użytkownika ani ustawień. Instalacja z poprzedniej wersji może wymagać uruchomienia nowego instalatora, ponieważ stary aktualizator może odrzucić nowszą paczkę przed jej uruchomieniem.

## EN — task record, 28 November 2026

- Source: commit `1a28b0e31117797a00a9b0b7541b0429808488be`; intended for the release after `alpha.534`.
- Behavior: the matching installer accepts an older AMC Public installation whose known capability list is a subset of the current one. Product identity, installation ID, channel, data folders, and credential namespace must still match. Foreign profiles and unknown capabilities are rejected.
- Entry point: `AMC-Python-Updater.exe --installer FILE.EXE --installer-sha256 HASH --target DIRECTORY --silent`; no new shortcut, menu item, or setting. Test and Public retain separate profiles and update locks.
- Accessibility: no new control or NVDA message. The premature package rejection should disappear only for compatible older installations. Invalid packages still fail before file replacement.
- Rationale: installer mode required the *installed* manifest to have the exact current capability list, even though the existing compatibility contract permits an older subset. Strict validation remains in place for the incoming source package.
- Verification: the launcher/updater test command above passed all 49 headless tests, including older Public without `local-video-preview`, foreign installation ID, and profile impersonation. The command built the test runner. Installation and live NVDA acceptance were outside this code task and must be recorded separately.
- Migration: user data and settings are unchanged. Updating from an older version may require running the new installer directly because the old updater can reject a newer package before launching it.
