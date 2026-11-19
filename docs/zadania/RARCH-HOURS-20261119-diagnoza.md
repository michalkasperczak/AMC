# Radio Archiwista — ponowne zgłoszenie Ctrl+F i wybieranie godziny cyframi

**Status: DIAGNOZA ŹRÓDEŁ POTWIERDZONA; korekta godzin w realizacji przez Codex.**

## Zlecenie i zakres

Michał ponownie zgłasza, że Ctrl+F w sesji Radio Archiwista podaje komunikat o nieobsługiwanej funkcji. Dodatkowo w liście godzin wybranego dnia i stacji chce wybierać pozycję przez wpisanie cyfry: 7 ma wskazać siódmą rano. Wybranie pozycji nie ma jej automatycznie odtwarzać.

Sprawdzić obie drogi oddzielnie. Nie zmieniać bez reprodukcji już zaakceptowanego wyszukiwania, nie dodawać wyszukiwania w transkrypcjach ani pobierania całego archiwum. Nie zmieniać instalacji, nie uruchamiać GUI ani audio, nie pobierać profili, kont, cookies ani poświadczeń. Wybrana poprawka jest niezależna od prac #26/#28 i nie zastępuje ich kolejności.

## Ctrl+F — sprawdzony stan

W aktualnych źródłach istnieje poprawka `bb3fdcd3284aa94847b6b45e6dc6215aa89d29e7`, obecna również w kanonicznej bazie `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e`. `git merge-base --is-ancestor` zakończył się kodem 0. Odczytano pełny [wcześniejszy plan i odbiór](SEARCH-SCOPE-20261118-radioarchiwista-i-uslugi.md), a nie tylko nagłówek pamięci.

Droga produkcyjna: hook listy przechwytuje Ctrl+F, resolver wybiera SEARCH_CURRENT, zbiór możliwości Radio Archiwisty dopuszcza wyszukiwanie, dispatcher otwiera zwykłe okno wyszukiwania. Rzeczywiste źródło na zimnym profilu pobiera tylko indeks stacji. Wynik Enterem wskazuje stację w zwykłej liście; dalej wybiera się rok, miesiąc, dzień i godzinę.

Powtórzono tylko wybraną granicę: `PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest wxlite/tests/test_radioarchiwista_search_scope.py -q --basetemp /home/michal/.hermes/profiles/amc/cache/scratch/radioarchiwista-followup-parent-search -p no:cacheprovider` — **10 passed in 0.94s**, exit 0. To testy bez GUI, nie próba żywego NVDA.

Poprzedni odbiór wprost zapisał brak nowego pakietu, instalacji i publikacji. Objaw użytkownika może pochodzić z wersji bez tej poprawki, ale nie sprawdzono wersji uruchomionej na głównym komputerze. Nie deklarować pewnego rozpoznania przyczyny na tamtym komputerze. Na obecnej bazie nie odtworzono komunikatu o nieobsługiwanym Ctrl+F; nie ma podstaw do kolejnej zmiany tej funkcji.

## Godziny — konkretna różnica względem oczekiwania

Producent `radioarchiwista_source.recording_rows` tworzy tytuły `00:00`, `07:00`, `17:00` itp. Radio Archiwista używa zwykłej natywnej listy, a drukowalne klawisze bez przypisanej akcji są oddawane kontrolce. Nie ma mapowania liczby 7 na godzinę 07:00. Początek tekstu 07:00 nie jest początkiem 7.

Rodzic wykonał offline produkcyjny `recording_rows` dla jawnych fikcyjnych danych godzin 0/7/17; otrzymał `['00:00', '07:00', '17:00']`. Istniejący `ListModel.find_prefix('7')` zwrócił -1, a `find_prefix('07')` 1. Obie obserwacje sprawdzono asercjami, exit 0. Ten helper ma obecnie tylko testowych callerów — jego poprawienie bez podłączenia do handlera niczego nie naprawi.

To wyjaśnia lukę kontraktu wybierania godzin i jest dowodem źródłowym, nie pomiarem naciskania klawiszy na Windows. Osobno sprawdzić dekoracje Ulubionych/odtwarzania i widoczny filtr, aby wybór nie przeskakiwał na ukryty wiersz. Nie wyciągać wniosku, że wszystkie litery we wszystkich listach są uszkodzone.

## Wykonanie i kryteria odbioru

Codex pracuje w osobnym czystym worktree `amc-radioarchiwista-hours-20261119`, gałąź `codex/amc-radioarchiwista-hours-20261119`, baza `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e`. Delegacja `deleg_d9b61458`, wykonawca `sa-0-eb5bbe09`.

Wymagane pionowe RED/GREEN na rzeczywistym produkcyjnym handlerze: 7 wskazuje 07:00; zachowane wpisywanie 07 i 17; brak wskazanej godziny nie uruchamia innej audycji. Wybór dotyczy tylko widocznych pozycji aktualnego dnia i stacji, nie gracza, pola filtra, dialogu lub innej sesji. Nie przejmować Ctrl/Alt/Shift, skrótów czytnika, presetów ani literowego wybierania stacji i miesięcy. Uwzględnić brak podwójnej reakcji hook/KEY_DOWN, normalne native focus events i brak automatycznego odtwarzania.

Testy mają korzystać z rzeczywistych producentów wierszy i handlerów, nie samego idealnego helpera. Proporcjonalny zestaw sąsiednich regresji, nie pełna bateria i nie budowa C#, jeśli zmienia się tylko Python. Zaktualizować użytkowy opis istniejącego kontraktu nawigacji, bez dokładania niezamówionych modalnych poleceń/menu. Wykonawca zapisze faktyczny RED/GREEN, pliki i commit w `RARCH-HOURS-20261119-wdrozenie.md`; rodzic wykona ograniczony przegląd i odbiór granicy przed integracją.

## Materiał do dokumentacji użytkownika

Ctrl+F w Radio Archiwiście wyszukuje stacje, nie treść audycji. W widoku godzin wybranego dnia cyfra 7 ma zaznaczać 07:00; Enter pozostaje oddzielnym uruchomieniem nagrania. Jest to kontrakt wdrażanej poprawki, nie informacja o już dostępnym wydaniu. Odczyt i fokus NVDA wymagają później osobno uzgodnionej próby na Windows.

## Wersja i następny krok

Kanoniczna baza źródeł: `4b5a4a4a07968eae69ba7cbc602d30bbaa07f07e`; brak nowego artefaktu i zmian głównego komputera. Następny krok: odebrać czysty commit oraz rzeczywiste wyniki Codexa, sprawdzić zakres i wykonać ograniczoną kontrolę krytycznej granicy. Nie uznawać samej diagnozy ani wysłania zlecenia za gotową poprawkę.
