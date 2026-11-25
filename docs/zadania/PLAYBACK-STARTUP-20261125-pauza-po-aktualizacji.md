# Odtwarzanie po aktualizacji: zachowanie pauzy

Stan: 25.11.2026 — poprawka źródłowa i 133 testy celowane gotowe; AMC Test alpha.529 wydane na GitHubie; instalacja i odbiór po aktualizacji otwarte.

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

Bieżąca instalacja użytkownika ma etykietę `25.11.2026, 02:00` i manifest źródeł `4ba3ea7dbcfcb359415b4384411d4ce61a263c78` (AMC Test alpha.528). Poprawka nie należy do alpha.528. Źródła alpha.529: commit `06a3f3c9767cce1cffc0fb599cd1f768da983ef6`. GitHub Actions run `45525484120` zakończył się powodzeniem dla tego commitu. Wydanie `test-2026.11.25.0226` zawiera instalator `AMC-Test-2026-11-25-0226.exe`, 214672890 bajtów, SHA-256 `768f31bc4bb16bff87d82bf17f18d6698d213ba22c5525c11410a8ef27e2cec9`; kontrola sekretów przeszła, a plik integralności i manifest aktualizacji są opublikowane. Wydania jeszcze nie zainstalowano lokalnie i nie sprawdzono z prawdziwie wstrzymaną stacją. Następny krok: odbiór po aktualizacji bez naruszania nagrań użytkownika.
