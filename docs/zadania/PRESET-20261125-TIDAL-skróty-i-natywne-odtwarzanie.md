# PRESET — TIDAL: skróty cyfr i natywne odtwarzanie

Zgłoszenie: 25.11.2026. Priorytet: pilny. Status: błąd potwierdzony w kodzie, naprawa i odbiór otwarte. Bieżąca aplikacja to wxPython w `D:\Projekty Codex\AMC - integracja wxPython`; dawny WPF nie należy do zadania. Baza badania: `d8d561db`, czysta gałąź `codex/heos-next-test-20261123`.

## Decyzja użytkownika zastępująca projekt z 22.11.2026

Michał zgłosił, że w TIDAL nie działa bezpośrednie przypisanie zaznaczonego celu przez `Ctrl+Alt+Shift+1…9, 0`, mimo że `Ctrl+Alt+Shift+P` pozwala przypisywać presety. Skróty i działanie mają być spójne między sesjami wszędzie, gdzie preset jest dostępny. Jego wypowiedź zawierała przejęzyczenie „Ctrl Alt Ctrl Shift”; zapisane skróty z aktualnego kontraktu to `Ctrl+Alt+Shift+cyfra` oraz `Ctrl+Alt+Shift+P`. Sprawdzić też faktyczną nazwę skrótów w działającym wydaniu i odczyt NVDA.

Zapisane presety albumów i playlist TIDAL nie mogą uruchamiać oryginalnej aplikacji TIDAL. Powinny używać natywnego odtwarzania AMC. Oryginalny TIDAL ma pozostać jedynie jawną opcją w menu kontekstowym. Ta decyzja z 25.11.2026 **zastępuje** opis w [PRESET-20261122](PRESET-20261122-tidal-i-bezposrednie-przypisanie.md), który świadomie kierował album i playlistę do oryginalnego TIDAL. Nie utrzymywać obu wariantów jako równorzędnych.

## Potwierdzone ścieżki kodu

- `shortcuts.py`: `ASSIGN_PRESET_SHORTCUTS` mapuje `Ctrl+Alt+Shift+1…0` na akcje przypisania, a `LIST_VIEW` i `PLAYER_VIEW` zawierają tę mapę. `gui.py` kieruje te akcje przez `assign_preset_slot` do `_assign_preset(direct_slot)`.
- `TIDAL_SUPPORTED_ACTIONS` w `shortcuts.py` zawiera `ASSIGN_PRESET` i akcje uruchamiania `PRESET_1…12`, ale **nie zawiera** `ASSIGN_PRESET_1…10`. Bramka w `gui.py` odrzuca te akcje przed wykonaniem i używa przestarzałego komunikatu o oryginalnym TIDAL. To bezpośrednia, źródłowo potwierdzona przyczyna braku działania skrótów cyfr w TIDAL. Dotychczasowe testy sprawdzały m.in. `resolve(...)`, lecz nie pełną bramkę wykonania TIDAL, więc jej nie wykryły.
- `_activate_preset` w `gui.py` uruchamia zapisany utwór TIDAL przez `navigator.open_tidal_search_result`; album i playlistę przekazuje jako `PlayTidalContainer`. `_play_tidal_container` wywołuje `client.tidal_desktop_play_container`, czyli stary zewnętrzny tor. Wymaga zastąpienia natywnym pobraniem kolejki albumu/playlisty i uruchomieniem w AMC z właściwą pozycją, metadanymi oraz obsługą błędu; nie wystarczy zmienić etykiety.

## Wymagany rezultat i weryfikacja

- `Ctrl+Alt+Shift+cyfra` w TIDAL zapisuje prawidłowy zaznaczony utwór, album, playlistę lub wykonawcę zgodnie z tym samym kontraktem zajętego miejsca, anulowania i potwierdzenia co inne sesje. Sprawdzić listę i odtwarzacz, wyszukiwanie, kolekcję oraz wiersz bez identyfikatora; w każdym wypadku NVDA ma dostać czytelną informację.
- Preset utworu, albumu i playlisty uruchamia natywny TIDAL w AMC. Preset wykonawcy otwiera właściwy katalog. Uruchomienie presetu nigdy nie wywołuje `tidal_desktop_play_container` ani nie otwiera zewnętrznej aplikacji. Jawna opcja kontekstowa „Otwórz w oryginalnym TIDAL” zachowuje osobne znaczenie.
- Wspólny model poleceń ma utrzymywać zgodność realnego wykonania, dostępności, menu Presety, `Ctrl+F1`, pełnej pomocy, palety `Ctrl+Shift+K` i dokumentacji w reprezentatywnych sesjach. Dodać test przechodzący przez faktyczną bramkę TIDAL, nie tylko mapę skrótów; testy natywnego uruchomienia kontenerów i błędów oraz ręczny odbiór NVDA.

Stan w chwili zapisu: analiza źródeł i karta zadania, **bez zmiany kodu**, testu nowej poprawki, kompilacji, instalacji i publikacji. Poprzedni `PRESET-20261122` opisywał testy i wydanie tamtej wersji; nie stanowią odbioru tej korekty. Ostatni odczyt limitu Codex: 98% wykorzystane, 2% pozostało; szeroka naprawa i pakowanie wymagają nowego przydziału. Commit dokumentacji do uzupełnienia w historii gałęzi.
