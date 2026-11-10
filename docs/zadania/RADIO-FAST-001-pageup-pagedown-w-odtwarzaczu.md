# RADIO-FAST-001 — zmiana stacji w otwartym odtwarzaczu

Status: **Oczekuje**. Decyzja użytkownika: 10.11.2026. Plan rozpoczęcia: 11.11.2026.

## Ustalenie użytkownika

Page Up i Page Down w otwartym odtwarzaczu radia mają wybierać poprzednią i następną stację. Przełączanie AMC już jest dość szybkie; ewentualne dalsze przyspieszenie warto rozważyć po pomiarze. Zwykła lista ma nadal przewijać się stronami i nie uruchamiać stacji przy samym ruchu zaznaczenia.

## Stan i zakres pracy

- W obecnych źródłach `step_playback_source` obsługuje stacje z listy źródłowej, a `_queue_step` wywołuje tę ścieżkę z odtwarzacza. Zadanie zaczyna się od sprawdzenia działania w rzeczywistym AMC Test, nie od dodania drugiego skrótu.
- Sprawdzić bibliotekę, ulubione, wyniki wyszukiwania i presety: kolejność, granice listy, zachowanie fokusu, komunikat o rzeczywiście uruchomionej stacji oraz szybkie powtarzanie klawisza.
- Zmierzyć czas od naciśnięcia klawisza do słyszalnego dźwięku na kilku różnych strumieniach. Dopiero gdy pomiar i odsłuch wykażą istotną zwłokę, zaprojektować ograniczone przygotowanie sąsiedniej stacji. Określić limit połączeń, anulowanie nieaktualnych prób i wpływ na nagrywanie oraz zużycie sieci.
- Jeżeli zmieni się polecenie lub jego dostępność, uaktualnić wspólny model, menu, paletę, pomoc kontekstową i pełną oraz dokumentację. Zachować podział listy i odtwarzacza z `NAV-20261106`.

## Uzasadnienie i decyzje

Inspiracja: [BlindRadio](https://projects.serrebiradio.com/apps/blindradio/) utrzymuje sąsiednie stacje gotowe do szybkiego przełączenia. Nie przejmujemy automatycznego uruchamiania stacji podczas chodzenia po liście ani kodu zewnętrznej aplikacji. Priorytet optymalizacji jest niski, dopóki własny pomiar AMC nie wskaże potrzeby.

## Materiał do dokumentacji użytkownika

Projekt do przyszłej korekty: „W odtwarzaczu Radia Page Up włącza poprzednią, a Page Down następną stację z listy, z której uruchomiono radio. Na zwykłej liście oba klawisze przewijają listę o stronę.” Ten opis jest warunkowy: przed publikacją trzeba potwierdzić działanie w instalacji i odsłuchu NVDA.

## Testy i wynik

Jeszcze nie uruchamiano testów ani pomiarów w ramach tego zadania. Zaplanować testy kontraktu listy źródłowej, szybkich kolejnych żądań, menu/palety/pomocy oraz ręczny odsłuch NVDA i realnych stacji. Źródła, build, instalacja i publikacja: bez zmian.

## Znane ograniczenia

Szybkość zależy od serwera i formatu stacji. Przygotowanie sąsiadów może wymagać dodatkowych połączeń; nie jest jeszcze przyjętą zmianą.

## Wersja i commit

Nie dotyczy przed rozpoczęciem implementacji. Zadanie dokumentacyjne, bez zmiany programu.
