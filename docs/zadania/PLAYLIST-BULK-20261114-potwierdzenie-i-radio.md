# PLAYLIST-BULK — wielokrotne zaznaczenie i potwierdzenie TIDAL

Stan z 14.11.2026: poprawka w źródłach AMC Test, testy celowane zaliczone. Wersja źródeł `0.1.0-alpha.506`; identyfikator commitu dopisać po zapisie. Instalacja i publikacja nie są tym zapisem potwierdzone.

## Zachowanie użytkownika

- Na liście plików, stacji radiowych, odcinków podcastów, utworów TIDAL i utworów lub odcinków Spotify można zaznaczyć kilka pozycji przez Shift/Ctrl lub Ctrl+A i użyć `Ctrl+Shift+P`. Okno otrzymuje wszystkie zaznaczone pozycje, a nie tylko wiersz z fokusem. Spacja wybiera kilka playlist docelowych, Enter zatwierdza. Escape anuluje i przywraca fokus do listy.
- Pliki, radio i podcasty zapisują członkostwo w lokalnych playlistach danego rodzaju. TIDAL i Spotify używają playlist konta usługi. W TIDAL wybranie samego albumu na liście albumów oznacza album; w jego otwartej zawartości polecenie bierze wskazane utwory.
- Po zapisie TIDAL komunikat NVDA podaje `Dodane pozycje: N. Zmienione playlisty TIDAL: M.` i osobno wymienia pozycje pominięte przez usługę lub bez potwierdzenia. Nie mówi już, że dodano wszystkie, gdy odpowiedź TIDAL zawiera `meta.skipped`. Gdy zapis się nie udał, mówi `Nie dodano pozycji do playlist TIDAL.` oraz przyczynę.
- Otwarcie playlisty stacji radiowych podaje jej nazwę, bez łącznego czasu trwania. Enter na stacji nadal uruchamia ją; Backspace wraca do listy playlist radia. Dla playlist utworów w innych sesjach czas może być podawany.

## Korekty wcześniejszych ustaleń

- Zgłoszenie użytkownika z 14.11.2026 zastępuje nieprecyzyjny opis z `RECOG-002` i starego szkicu TIDAL, jakoby samo otwarcie albumu wybierało całą jego zawartość do playlisty. Obowiązuje faktyczne zaznaczenie. Szkic instrukcji TIDAL poprawiono z datą.
- Dopowiedzenie użytkownika z 14.11.2026 wyłącza łączny czas trwania dla playlist radia, nawet jeśli zwykłe playlisty utworów taki czas zapowiadają.

## Implementacja i weryfikacja

- Wspólny `MediaListCtrl.selected_rows()` przekłada natywne wielokrotne zaznaczenie na identyfikatory widocznych wierszy; `_selected_action_rows()` jest wspólnym wejściem polecenia w pięciu sesjach. Test mostu sprawdza wszystkie pięć bez zastępowania tej metody atrapą.
- TIDAL OpenAPI zwraca listę `data` dodanych zasobów i `meta.skipped` dla pominiętych. Klient API porównuje identyfikatory odpowiedzi z żądaniem i host liczy wyłącznie potwierdzone dodatki. Lista celów zapamiętanych po zapisie zawiera tylko playlisty ze zmianą. Odczyt playlisty po zapisie może się nie udać bez unieważnienia potwierdzenia samego zapisu.
- `python -m pytest -p no:cacheprovider -q` dla pięciu plików testów playlist i nawigacji: **34 passed**. `dotnet run` Core.SmokeTests z `--tidal-playlist-add`: **kod 0**. `dotnet build` LiteHost `--no-restore -v:q`: **0 ostrzeżeń, 0 błędów**. Izolowana próba natywnej kontrolki wx na zainstalowanym runtime zwróciła dwa wskazane wiersze. Osobny szeroki Core.SmokeTests przeszedł przypadek TIDAL, ale zakończył się na niezwiązanym asercie instalatora dotyczącym samowystarczalnych procesów .NET.

## Granice potwierdzenia

Nie wykonano zapisu na żywym koncie TIDAL lub Spotify ani odbioru NVDA w nowej zainstalowanej wersji. Uruchomiona kopia AMC Test jest starsza od tych źródeł. Testy, kompilacja, pakiet, instalacja i wydanie są oddzielnymi etapami. Przed wydaniem sprawdzić czysty zestaw źródeł, ponieważ w tym samym drzewie trwają równoległe zmiany Sonos i wcześniej odłożono budowę alpha.506.

## Szkic listy zmian do zatwierdzenia

Wybór kilku pozycji do playlist jest sprawdzony w plikach, radiu, podcastach, TIDAL i Spotify. TIDAL odczytuje potwierdzenie usługi i zgłasza pominięte pozycje. Playlista radia nie zapowiada łącznego czasu trwania; wybrane stacje nadal można odtwarzać.
