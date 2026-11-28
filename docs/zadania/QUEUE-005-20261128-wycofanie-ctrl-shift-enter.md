# QUEUE-005 — wycofanie Ctrl+Shift+Enter / withdrawal of Ctrl+Shift+Enter

Data: 28.11.2026. Kandydat do AMC Test i Public `0.1.0-alpha.535`.

## Decyzja i zachowanie / Decision and behavior

Późniejsza decyzja użytkownika z 25.11.2026 („wycofujemy Ctrl+Shift+Enter”) zastępuje wcześniejszy kontrakt, w którym ten gest przełączał blok „Odtwarzaj jako następne”. Opublikowany szkic alpha.534 nadal błędnie reklamował skrót; jego tekst skorygowano 28.11.2026. W obu profilach i w Plikach, Podcastach/YouTube oraz Radiu Archiwiście `Shift+Enter` dopisuje zaznaczenie do odtworzenia po bieżącym materiale; ponowne użycie nie usuwa pozycji. `Ctrl+Shift+Q` nadal przełącza zwykłą kolejkę. Polecenie „Dodaj lub usuń z następnych” pozostaje dostępne w menu i palecie bez skrótu oraz w pełnej pomocy jako „Bez skrótu”. `Ctrl+Shift+Enter` nie jest obsługiwany również w wynikach wyszukiwania. Stary zapis nadpisania tego gestu dla polecenia przełączania nie przechodzi migracji ustawień klawiatury.

The user's later 25 November 2026 decision to withdraw `Ctrl+Shift+Enter` supersedes the earlier toggle shortcut. `Shift+Enter` appends the selected item to Play Next in Files, Podcasts/YouTube and Radio Archiwista; a repeat does not remove it. `Ctrl+Shift+Q` still toggles the ordinary queue. The Add or remove from Play Next menu/palette command remains available without a shortcut, and the full keyboard help labels it “No shortcut”. Search results also leave `Ctrl+Shift+Enter` unbound. A saved override for the removed default action is dropped when loading keyboard settings.

## Dostępność, komunikaty i miejsca / Accessibility and surfaces

Wszystkie skróty, menu, menu kontekstowe pochodzące z modelu, paleta `Ctrl+Shift+K`, pełna pomoc i pomoc kontekstowa `Ctrl+F1` korzystają z tego samego modelu poleceń. `Shift+Enter` potwierdza „Odtwarzaj jako następne: {nazwa/liczba}”; usunięcie przez menu potwierdza „Usunięto z następnych: {nazwa/liczba}”. W menu Presety poprawiono przy okazji unikatowe klawisze dostępu uruchamiania i przypisywania, bez dodawania technicznych lub samotnych liter do nazw dla NVDA.

The same command model drives routing, menu, palette and help. `Shift+Enter` announces “Set to play next: {item}” in English; menu removal announces “Removed from play next: {item}”. Preset menu access keys are unique and do not add stray letters to the spoken item name.

## Weryfikacja / Verification

Testy Pythona: 113 zaliczonych w `test_shift_enter_release.py`, `test_menu_exposes_existing_actions.py`, `test_radioarchiwista_followup.py`, `test_shortcuts.py`, z katalogiem źródłowym przed zainstalowaną wersją w `PYTHONPATH`. Obejmują brak gestu w trzech rodzajach widoku i wynikach wyszukiwania, zgodność menu/palety/pomocy, odrzucenie starego nadpisania klawiatury oraz unikatowe klawisze dostępu menu. Trzy stare atrapy testowe Radio Archiwista uzupełniono o jawnie nieaktywny stan odtwarzania YouTube. `git diff --check` bez błędów. To są testy źródła; kompilację GitHub, instalację i rzeczywisty odbiór NVDA zapisuje osobny rekord wydania.

Python verification: 113 passed in the four targeted suites above. GitHub build, installation and live NVDA acceptance are separate release stages.

## Ograniczenia / Limits

Ręczna próba NVDA i naturalnego przejścia kolejki po instalacji pozostaje do odbioru. Czynności na działającej aplikacji nie były częścią tego testu źródła. Zmiana nie usuwa wewnętrznego protokołu przełączania bloku „następne”, ponieważ korzysta z niego polecenie menu.
