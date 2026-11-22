# Delete przy filtrowaniu kolejek AMC

Stan: 22.11.2026. Kandydat kodu `5c249c68` w gałęzi `codex/alpha512-update-candidate-20261122`. Nie zainstalowano ani nie wydano.

## Zachowanie

Po `Delete` w otwartej kolejce Spotify, TIDAL, żywej Kolejce Plików lub zapisanej Kolejce Plików program wyznacza następny zaznaczony wiersz z pozycji widocznych przy obecnym filtrze. Przy końcu listy wybiera poprzedni. Bez filtra obowiązuje dotychczasowa kolejność. Model, identyfikatory i zapis usługi pozostają bez zmian.

Podcasty i zapisane filmy YouTube mają osobną poprawkę `76b00d66`, opisaną w `QUEUE-DELETE-20261122-podcast-youtube.md`. Radio Archiwista ma poprawkę `7da80d87`, opisaną w `RARCH-QUEUE-DELETE-20261122.md`.

## Dlaczego

Te cztery ścieżki liczyły sąsiada z pełnej kolejki, więc wybrany następca mógł być ukryty przez filtr. Natywna lista stosuje `list_filter.filter_rows`; teraz ten sam zbiór wierszy służy do wyboru po usunięciu.

## Testy i ograniczenia

- `test_queue_membership.py`, `test_queue_duration_and_editing.py`, `test_podcast_queue_delete.py`: **25 passed**. Dodano osobne scenariusze filtrowania Spotify, TIDAL, żywej i zapisanej Kolejki Plików.
- Nie wykonano jeszcze odbioru fokusu z NVDA w rzeczywistym oknie, ani kont Spotify/TIDAL, ani pełnego zestawu i builda dla tego commitu.
- W żywej Kolejce plików nadal potrzebna jest próba z rzeczywistym hostem po odświeżeniu asynchronicznym; test celowany potwierdza wyliczenie i przekazanie właściwego ID.

## Szkic instrukcji i listy zmian do korekty Michała

Po przefiltrowaniu Kolejki naciśnij `Delete` na wybranym wpisie. AMC pozostawi zaznaczenie na najbliższej widocznej pozycji, bez skoku na element ukryty przez filtr.

Lista zmian: poprawiono wybór pozycji po usunięciu w filtrowanych kolejkach Spotify, TIDAL i Plików.
