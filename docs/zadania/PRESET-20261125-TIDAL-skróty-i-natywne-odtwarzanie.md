# PRESET — TIDAL: skróty cyfr i natywne odtwarzanie

Zgłoszenie i decyzja użytkownika: 25.11.2026. Status: poprawka kodu i testów zapisana; kompilacja, instalacja i odbiór pozostają otwarte. Bieżąca aplikacja: wxPython w D:\Projekty Codex\AMC - integracja wxPython. Dawny WPF nie należy do zadania.

## Decyzja zastępująca opis z 22.11.2026

Ctrl+Alt+Shift+1…9,0 ma przypisywać zaznaczony utwór, album, playlistę lub wykonawcę TIDAL do presetu, tak jak w pozostałych sesjach. Ctrl+Alt+Shift+P nadal otwiera istniejące przypisywanie. Album lub playlista uruchomiona z presetu ma grać natywnie w AMC. Oryginalny TIDAL pozostaje jedynie jawną opcją menu kontekstowego dla utworu. Ta decyzja zastępuje dawny projekt PRESET-20261122, który kierował albumy i playlisty do oryginalnego TIDAL.

## Przyczyna i poprawka

Bramka TIDAL_SUPPORTED_ACTIONS nie zawierała akcji ASSIGN_PRESET_1…10, choć mapy skrótów i metoda zapisu już istniały. Dodano te akcje w commicie 95c3bd8. Bramka jest wspólna dla wykonania i powierzchni poleceń.

Presety albumów i playlist oraz Ctrl+Enter przechodziły przez PlayTidalContainer do oryginalnego TIDAL. W commicie 61eac6b9 połączono je z jedną natywną trasą. Pobiera ona pełny kontener przez hosta, wybiera pierwszy odtwarzalny utwór i uruchamia go wraz z listą źródłową AMC do przechodzenia między utworami. Brak natywnego adaptera i pusty kontener dają czytelny komunikat bez przekazania do starej aplikacji. Z menu głównego i palety usunięto przekazanie utworu do oryginalnego TIDAL; jawne polecenie pozostało w menu kontekstowym. W kodzie aplikacji nie ma już wywołania tidal_desktop_play_container, choć niewywoływana metoda klienta hosta pozostała.

## Sprawdzenie i odbiór

Celowane testy: 64 zaliczone. Obejmują bramkę skrótów TIDAL, zapis i uruchomienie presetów albumu i playlisty, brak automatycznego powrotu do starego TIDAL, Ctrl+Enter, intencje nawigacji i źródłową listę natywnego odtwarzania. Szerszy przebieg test_tidal_native_gui.py przed tą zmianą miał 60 zaliczonych i 5 błędów niepełnych atrap SimpleNamespace w nietkniętych ścieżkach GUI; nie jest to zaliczona pełna regresja.

Nie zbudowano, nie zainstalowano i nie opublikowano nowego wydania. Ręczny test NVDA i prawdziwego konta TIDAL pozostaje do zrobienia: początkowy fokus, strzałki, powrót po zapisaniu i anulowaniu, ponowne otwarcie, dźwięk, pełna kolejność playlisty, następny utwór i zachowanie przy błędzie pobierania.

Historia: karta 45ce36ff, skróty 95c3bd8, częściowy zapis e9e0f440, natywne odtwarzanie 61eac6b9. W chwili ostatniego odczytu limitu Codex wykorzystano 98%, pozostało 2%, reset 27.11.2026 o 22:53 CET. Użytkownik wyraźnie pozwolił kontynuować poniżej progu 15%.

## Stan wydania 25.11.2026

Następcza wersja alpha.532 została skompilowana na GitHub Actions z commitu b830c954 i opublikowana w obu profilach. Tagi: test-2026.11.25.2256 oraz public-2026.11.25.2256. Profil Public nie zawiera Apple Music ani HEOS. Szczegóły kompilacji, SHA-256 i ograniczenia odbioru: RELEASE-ALPHA532-20261125-Test-Public.md. Wcześniejszy zapis o braku kompilacji dotyczył stanu sprzed tego wydania; ręczny odbiór NVDA i konta TIDAL nadal pozostaje otwarty.
