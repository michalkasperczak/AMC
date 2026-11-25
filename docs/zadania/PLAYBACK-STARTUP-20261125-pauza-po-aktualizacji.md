# Odtwarzanie po aktualizacji: zachowanie pauzy

Stan: 25.11.2026 — poprawka źródłowa i test celowany gotowe; nowe wydanie oraz odbiór po aktualizacji otwarte.

## Zgłoszenie i decyzja

Po aktualizacji i uruchomieniu AMC Test użytkownik nie słuchał niczego, ale program sam uruchomił ostatnią stację. Użytkownik wyjaśnił, że transmisja była wstrzymana. Po aktualizacji wznowienie dotyczy wyłącznie materiału, który faktycznie grał przed zamknięciem. Wstrzymana stacja nie może rozpocząć odtwarzania bez jawnego polecenia użytkownika. Ostatnia stacja może nadal pozostawać wskazana na liście.

## Przyczyna i zmiana

Aktualizator uruchamia AMC z `AMC_POST_UPDATE_RESUME=1`. Przed zamknięciem frontend pyta host o status sesji, a `post_update_playback.normalize_snapshot` kwalifikował wpis, gdy `playing=True`, nie sprawdzając `paused=True`. Host Radia może zgłaszać oba pola jednocześnie dla otwartej, lecz wstrzymanej stacji. Nowy proces odtwarzał taki wpis przez `play_station`, uruchamiając dźwięk. Normalizacja pomija teraz wstrzymane wpisy. Ta sama zasada obejmuje inne kwalifikujące się sesje, jeśli ich status ma `paused=True`.

## Materiał do dokumentacji użytkownika

Jeśli przed aktualizacją wstrzymasz odtwarzanie, AMC Test nie uruchomi go sam po ponownym starcie. Materiał, który rzeczywiście grał w chwili zamknięcia przez aktualizator, nadal może zostać przywrócony. Odtwarzanie wstrzymanej stacji wznowisz świadomie zwykłym poleceniem odtwarzania.

## Testy i wynik

- `pytest -q tests/test_post_update_playback.py`: 7 passed w przypiętym środowisku wxPython 4.3.1 / Python 3.14. Nowy test przeprowadza wstrzymaną stację przez odczyt statusu przed zamknięciem i próbę wznowienia po aktualizacji; potwierdza brak wywołania `play_station`.
- `pytest -q tests/test_post_update_playback.py tests/test_state_store.py tests/test_application_updates.py`: 91 passed.
- `pytest -q tests/test_release_profile.py tests/test_installer_profiles.py`: 42 passed. `py_compile` i `git diff --check` zakończyły się kodem 0.
- Nie uruchamiano lokalnego AMC ani rzeczywistego aktualizatora w ramach tego testu. Test źródłowy nie zastępuje próby po wydaniu.

## Znane ograniczenia, wersja i commit

Bieżąca instalacja użytkownika ma etykietę `25.11.2026, 02:00` i manifest źródeł `4ba3ea7dbcfcb359415b4384411d4ce61a263c78` (AMC Test alpha.528). Poprawka nie należy do alpha.528. Kandydat następnego wydania to AMC Test alpha.529; commit do uzupełnienia po zatwierdzeniu. Następny krok: zbudować i opublikować poprawkę na GitHubie, a potem sprawdzić aktualizator na wstrzymanej stacji bez naruszania nagrań.
