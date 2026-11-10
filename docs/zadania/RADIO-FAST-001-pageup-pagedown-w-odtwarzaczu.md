# RADIO-FAST-001 — zmiana stacji w otwartym odtwarzaczu

Status: **W trakcie: poprawka źródeł i testy celowane gotowe; pomiar dźwięku oraz odbiór NVDA czekają**. Decyzja użytkownika: 10.11.2026. Prace rozpoczęto 10.11.2026.

## Ustalenie użytkownika

Korekta z 10.11.2026: użytkownik wskazał to jako najlżejsze z trzech zadań do rozpoczęcia od razu, zastępując wcześniejszy plan startu 11.11 oraz przejęzyczenie „najdłuższe”.

Page Up i Page Down w otwartym odtwarzaczu radia mają wybierać poprzednią i następną stację. Przełączanie AMC już jest dość szybkie; ewentualne dalsze przyspieszenie warto rozważyć po pomiarze. Zwykła lista ma nadal przewijać się stronami i nie uruchamiać stacji przy samym ruchu zaznaczenia.

## Stan i zakres pracy

- W obecnych źródłach `step_playback_source` obsługuje stacje z listy źródłowej, a `_queue_step` wywołuje tę ścieżkę z odtwarzacza. Zadanie zaczyna się od sprawdzenia działania w rzeczywistym AMC Test, nie od dodania drugiego skrótu.
- Sprawdzić bibliotekę, ulubione, wyniki wyszukiwania i presety: kolejność, granice listy, zachowanie fokusu, komunikat o rzeczywiście uruchomionej stacji oraz szybkie powtarzanie klawisza.
- Zmierzyć czas od naciśnięcia klawisza do słyszalnego dźwięku na kilku różnych strumieniach. Dopiero gdy pomiar i odsłuch wykażą istotną zwłokę, zaprojektować ograniczone przygotowanie sąsiedniej stacji. Określić limit połączeń, anulowanie nieaktualnych prób i wpływ na nagrywanie oraz zużycie sieci.
- Jeżeli zmieni się polecenie lub jego dostępność, uaktualnić wspólny model, menu, paletę, pomoc kontekstową i pełną oraz dokumentację. Zachować podział listy i odtwarzacza z `NAV-20261106`.

## Uzasadnienie i decyzje

Inspiracja: [BlindRadio](https://projects.serrebiradio.com/apps/blindradio/) utrzymuje sąsiednie stacje gotowe do szybkiego przełączenia. Nie przejmujemy automatycznego uruchamiania stacji podczas chodzenia po liście ani kodu zewnętrznej aplikacji. Priorytet optymalizacji jest niski, dopóki własny pomiar AMC nie wskaże potrzeby.

## Materiał do dokumentacji użytkownika

Projekt do przyszłej korekty: „W otwartym odtwarzaczu Radia Page Up wybiera poprzednią, a Page Down następną stację z listy źródłowej. Na zwykłej liście oba klawisze przewijają listę o stronę. Polecenia są także w menu Odtwarzanie, menu kontekstowym, palecie Ctrl+Shift+K, pomocy Ctrl+F1 i pełnej pomocy. Na granicy listy AMC informuje, że to pierwsza lub ostatnia pozycja.” W kodzie są już etykiety „Poprzednia stacja” i „Następna stacja”; ich rzeczywisty odczyt NVDA w nowej instalacji pozostaje do potwierdzenia.

## Testy i wynik

W źródłach potwierdzono istniejącą ścieżkę Page Up/Page Down przez `step_playback_source` i `_queue_step`. Dodano radiowe nazwy poleceń w menu Odtwarzanie, menu kontekstowym, palecie i obu rodzajach pomocy; polski i angielski odczyt korzystają z tych samych nazw. Nawigacja i tor audio nie zostały zmienione.

- Końcowe celowane `python -m pytest -q` z katalogu `wxlite`: 10 zaliczonych, 0 błędów. Obejmują oba kierunki, granicę listy, rozdzielenie listy od odtwarzacza, menu/paletę/pomoc po polsku i angielsku oraz etykiety menu kontekstowego. Są to testy kodu i atrap kontrolek, bez odsłuchu NVDA.
- Wcześniejszy szerszy zestaw czterech modułów: 78 zaliczonych, 1 pominięty, 3 niezaliczone. Jedna niezgodność `Ctrl+F5` Radia i dwie inwentaryzacje tłumaczeń dotyczą równolegle edytowanych funkcji katalogu oraz innych napisów; nie zaliczono ich na rzecz tego zadania i nie włączono ich zmian do commitu. Pierwsza próba przez pomocniczy `run_tests.py` dała 122 zaliczone i 4 błędy runnera, który nie obsługuje fixture `monkeypatch`; następnie użyto właściwego pytest.
- Build, paczka, instalacja, publikacja, pomiar czasu do słyszalnego dźwięku i ręczny odbiór NVDA: **nie wykonano**. W repozytorium trwały też inne, niezwiązane zmiany robocze; pozostawiono je nietknięte.

## Znane ograniczenia

Szybkość zależy od serwera i formatu stacji. Przygotowanie sąsiadów może wymagać dodatkowych połączeń; nie jest jeszcze przyjętą zmianą. Bez żywego pomiaru i odsłuchu nie ma podstaw do zmiany toru audio. Szybkie powtarzanie klawiszy oraz lista z wyników wyszukiwania i presetów wymagają ręcznego odbioru na bieżącej instalacji.

## Wersja i commit

Kod źródłowy i testy: commit `a635835b` na gałęzi `codex/amc-test-public-20261109`. Dotychczasowa instalacja użytkowa nie zawiera tej poprawki; numeru nowej wersji, paczki ani wydania nie nadano.
