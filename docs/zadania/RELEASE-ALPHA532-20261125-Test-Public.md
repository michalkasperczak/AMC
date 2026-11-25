# AMC Test i Public alpha.532 — przebieg wydania GitHub

Data: 25.11.2026. Status: przygotowanie źródeł i workflow; build i publikacja jeszcze niepotwierdzone.

Wersja: 0.1.0-alpha.532. Gałąź: codex/heos-next-test-20261123. Profile: Test i Public z tego samego commitu źródeł. Public wyklucza Apple Music i HEOS; te sesje pozostają w Test.

Zakres względem alpha.531: poprawka stanu Google Cast po załadowaniu, bezpośrednie skróty przypisania presetów TIDAL i natywne uruchamianie albumu/playlisty z presetu oraz Ctrl+Enter. AirPlay na Sonos „Biuro” pozostaje niesprawny w próbie użytkownika; nie obiecywać poprawy. Zmiana lokalnej zapory nie jest częścią pakietu.

Weryfikacja przed pushem: 87/87 testów celowanych, obejmujących profile wydania, presety i natywne TIDAL, nawigację i protokół Cast. Git diff --check bez błędów. GitHub Actions ma wykonać build instalatorów i potwierdzenie integralności na Windows. Po runie trzeba sprawdzić dokładny commit, profil, wersję, manifest, rozmiar, SHA-256 i kontrolę sekretów oraz odczyt opublikowanych zasobów. Dopiero potem zmienić status tego rekordu na opublikowany.

Ręczny odbiór NVDA, TIDAL i AirPlay oraz instalacja lokalna nie są częścią tej publikacji. Szkic instrukcji: ALPHA532-instrukcja-i-lista-zmian-szkic.md.
