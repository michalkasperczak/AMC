# AMC Test i Public alpha.532 — wydanie GitHub

Data: 25.11.2026. Stan: oba wydania opublikowane i zdalnie zweryfikowane. Wersja 0.1.0-alpha.532. Commit źródeł b830c954fb38070e6b98422827b4d4e058b9feb9. Gałąź codex/heos-next-test-20261123.

## Zakres

Wspólne dla Test i Public: przypisanie presetu TIDAL przez Ctrl+Alt+Shift+cyfra; natywne odtwarzanie albumów i playlist z presetu i przez Ctrl+Enter; jawny stary TIDAL tylko w menu kontekstowym utworu. Google Cast sprawdza stan odbiornika po załadowaniu. Apple Music i HEOS są dostępne w Test, wyłączone w Public. Lokalna poprawka zapory AirPlay nie jest składnikiem instalatora.

## Weryfikacja źródeł i kompilacji

Przed wysłaniem: 87/87 testów celowanych, obejmujących profile, presety, nawigację TIDAL i Cast. Po odkryciu wymogu manifestu kanału: 23/23 testów aktualizatora. Git diff --check bez błędów. Skan dodanych wierszy nie ujawnił wartości poświadczeń; jedyne trafienie to standardowe odwołanie do github.token w workflow.

GitHub Actions run 45708527108 ukończył oba zadania sukcesem z dokładnego commitu b830c954fb38070e6b98422827b4d4e058b9feb9. Zdalne artefakty pobrano na D i sprawdzono z plikami integralności oraz dowodem zakończenia kompilatora Inno Setup (exitCode 0). Oba manifesty potwierdzają profil, wersję, tę samą rewizję i secretScan passed.

AMC Test: tag test-2026.11.25.2256, prerelease, AMC-Test-2026-11-25-2256.exe, 214662369 bajtów, SHA-256 8b2a86a64775d31cd7241aab475b7ff342bc893a2d6fb2fe019d8610d6d33d35.

AMC Public: tag public-2026.11.25.2256, najnowsze stabilne, AMC-Public-2026-11-25-2256.exe, 134078522 bajty, SHA-256 2f3ae0bfbb8a6b4d7047c1b8fb79a9dc64b73f20de019460c7b6a21111690052.

Każde wydanie zawiera instalator, plik integralności, dowód kompilatora i amc-update-profil.json. Przed publikacją porównano wszystkie cztery nazwy, rozmiary i SHA-256 z odczytem zasobów GitHub; potem odczyt potwierdził cztery zasoby uploaded w każdym wydaniu i właściwy commit. Manifest Test ma kanał test, manifest Public kanał stable. Poprzedni manifest Public alpha.531 miał wartość public, niezgodną z aktualnym profilem stable; alpha.532 koryguje metadane kanału. Endpoint releases/latest wskazuje public-2026.11.25.2256; lista wydań aktualizatora wybiera nowe tagi obu profili.

## Granice odbioru

Nie instalowano ani nie uruchamiano nowych paczek na komputerze użytkownika. Nie wykonano ręcznego testu NVDA, rzeczywistego odtwarzania pełnej playlisty TIDAL ani odsłuchu nagrania w tej paczce. Sonos „Biuro” w próbie AirPlay nadal milczał; tej usterki nie należy uznawać za rozwiązaną. Szerszy zestaw test_tidal_native_gui.py miał przed wydaniem pięć błędów niepełnych atrap GUI, więc 87/87 jest wynikiem zestawu celowanego, a nie pełnej regresji.

Pobrane artefakty i notatki pozostały w D:\Projekty Codex\AMC - integracja wxPython\work\release-alpha532-20261125 do odbioru. Szkic instrukcji i listy zmian: ALPHA532-instrukcja-i-lista-zmian-szkic.md; nie opublikowano go jako zatwierdzonej instrukcji.
