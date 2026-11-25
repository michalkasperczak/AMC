# Podcasty: wybór po zmianie Biblioteki i komunikat po odświeżeniu

Stan: 25.11.2026 — kod i testy celowane gotowe; wydanie GitHub alpha.528 opublikowane; instalacja i odbiór NVDA otwarte.

## Cel i korekta użytkownika

Po `Ctrl+Shift+L` w Podcastach usunięcie źródła z otwartej listy jego odcinków przenosiło użytkownika na początek głównej Biblioteki. Dodanie źródła z okna wyszukiwania mogło podobnie przestawić zaznaczenie na pozycję nowego źródła, które sortowało się wyżej. Przy `Delete` na odcinku potwierdzenie po odświeżeniu bywało pomijane. Użytkownik 25.11.2026 zgłosił, że stan po poprzedniej poprawce wciąż jest nieprawidłowy; to zastępuje wcześniejsze założenie, że wybór nowo dodanego wpisu z wyszukiwarki jest zawsze pożądany.

## Zachowanie i decyzje

- `Ctrl+Shift+L` na podcaście lub jego odcinku w głównej Bibliotece usuwa źródło i ustawia sąsiedni widoczny podcast: następny, a na końcu poprzedni.
- `Ctrl+Shift+L` na odcinku w otwartym podcaście po usunięciu źródła wraca do głównej Biblioteki i wybiera sąsiednie źródło według ostatnio wczytanej kolejności Biblioteki.
- Dodanie z `Ctrl+F` lub `Ctrl+Shift+F` odświeża Bibliotekę pod oknem wyszukiwania, zachowując wcześniej wybrany wiersz. Usunięcie wyniku z wyszukiwarki wybiera sąsiada, jeśli usunięto aktualnie wskazany podcast.
- Operacja wyniku katalogowego mówi teraz „Zmienianie Biblioteki” lub „Zmienianie Ulubionych”, a następnie wynik „Dodano…”/„Usunięto…”. Nie nazywa zmiany Biblioteki przygotowywaniem wyniku katalogu.
- `Delete` na odcinku przekazuje potwierdzenie do odpowiedniego widoku Podcastów dopiero po odświeżeniu natywnej listy. Podczas tego odświeżenia nie czyta ponownie nagłówka widoku.
- TIDAL nadal pokazuje po 150 pozycji na stronę. `Ctrl+F` przeszukuje pełną pobraną kolekcję oraz katalog TIDAL; `Ctrl+K` filtruje tylko widoczną listę. Nie zmieniono stronicowania.

## Materiał do dokumentacji użytkownika

W Podcastach naciśnij `Ctrl+Shift+L`, aby dodać lub usunąć podcast z Biblioteki. Po usunięciu źródła AMC wskaże sąsiedni podcast. Po dodaniu z okna wyszukiwania zaznaczenie dotychczasowego wiersza nie zmieni się. Gdy usuniesz odcinek klawiszem `Delete`, potwierdzenie usunięcia zostanie wypowiedziane po odświeżeniu listy. W TIDAL użyj `Ctrl+F`, aby znaleźć album w pełnej pobranej kolekcji i katalogu, bez przechodzenia po wszystkich stronach listy.

## Testy i wynik

- `pytest -q tests/test_podcast_library_queue.py tests/test_search_gui_navigation.py`: 119 passed.
- `pytest -q tests/test_podcast_navigation.py tests/test_podcast_favorite.py tests/test_podcast_queue_delete.py tests/test_profile_mutations.py tests/test_search_large_library.py`: 34 passed.
- `pytest -q tests/test_release_profile.py tests/test_installer_profiles.py`: 42 passed.
- `py_compile amc_wx_lite/gui.py`: exit 0.
- Runtime: przypięte `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe`.

## Znane ograniczenia

- Nie wykonano jeszcze rzeczywistej próby NVDA i fizycznego fokusu w zainstalowanym AMC Test.
- Jeśli podcast otwarto bez wcześniejszego odczytu głównej Biblioteki, brak zapamiętanej kolejności źródeł; po usunięciu całego źródła wybór może spaść na pierwszy pozostały wiersz.
- Nie wykonano pełnego zestawu testów ani lokalnej instalacji. Przed instalacją sterowanie komputerem zostało przerwane klawiszem Escape; nie potwierdzono stanu nagrywania.

## Wersja i commit

Źródła: commit `4ba3ea7dbcfcb359415b4384411d4ce61a263c78` na gałęzi `codex/heos-next-test-20261123`. GitHub Actions run `45522175254` zakończył się powodzeniem. Wydanie testowe `test-2026.11.25.0200` zawiera instalator `AMC-Test-2026-11-25-0200.exe`, 214638869 bajtów, SHA-256 `7cf5652e2da765ac713a336e98c11e978995fb845e5715a672e2720097c6bc9b`; plik integralności i manifest aktualizacji są opublikowane. Bieżąca instalacja lokalna pozostaje AMC Test alpha.527 (`test-2026.11.25.0046`).

Następny krok: po bezpiecznym odczycie stanu nagrywania zainstalować wydanie alpha.528 i wykonać odbiór `Ctrl+Shift+L`, `Delete` oraz fokusu NVDA na rzeczywistym widoku Podcastów.
