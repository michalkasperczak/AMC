# Ctrl+I: pamięć pozycji i oznaczanie odtwarzanego elementu

Status: rozpoczęte; źródłowe zlecenie Michała z 19.11.2026.

## Cel i zakres

Po Ctrl+I, wyborze odcinka, przejściu Ctrl+L do Biblioteki i ponownym Ctrl+I fokus ma wrócić na ostatnio wskazany odcinek skrzynki, nie początek. Istniejąca opcja pamięci pozycji powinna obejmować skrzynkę, z osobną pamięcią od Biblioteki. Zachować istniejące doładowanie kolejnej porcji przez Ctrl+I, gdy skrzynka jest już otwarta.

Druga część: przekazać informację o faktycznie odtwarzanym materiale (tekst, sygnał albo oba zgodnie z istniejącym wyborem) przez jeden resolver tożsamości, renderer i drogę cue. Dotyczy to wszystkich właściwych list tego samego materiału: głównych list Plików, Podcastów, YouTube, Spotify i TIDAL, kolejek, zawartości playlist, historii, wyników wyszukiwania, zakładek oraz odtwarzanych strumieni WiiM/Sonos. Nie używać tytułu, zaznaczenia ani samego wysłania play; zawsze wymagać potwierdzonego identyfikatora materiału i sesji. Kontenery (album, playlista, folder, urządzenie) nie są materiałem odtwarzanym. Zachować działające Radio, bez zmiany silników ani kont.

## Kryteria odbioru źródeł

- Test RED/GREEN przez produkcyjne handlery przejścia Ctrl+I → wybór dalszego odcinka → Ctrl+L → Ctrl+I, również po zmianie kolejności i wczytaniu kolejnej porcji.
- Pamięć według stabilnego ID, rozdzielona dla skrzynki i Biblioteki; usunięty/ukryty element ma bezpieczny fallback; brak kradzieży fokusu przez spóźniony wynik. Wyłączona opcja pamięci ma zachować świadome zachowanie użytkownika.
- Tekst/sygnał/wyłączenie respektują dotychczasowy wybór. Stan A→B, pauza/wznowienie, stop/błąd usuwa stare oznaczenie. Wiersze kontenerów nie są utworami. Ten sam tytuł/ID w innej sesji nie daje fałszywego oznaczenia.
- Matryca obejmuje główne listy, kolejki, playlisty/zawartość, historię, wyszukiwanie i zakładki; wszystkie muszą prowadzić do prawdziwego `material_id`, a nie własnego ID wiersza. Parametryzowane scenariusze adapterów nie wykonują dodatkowego I/O podczas ruchu po liście.
- Stan WiiM/Sonos/Frontier i Apple Music wolno oznaczyć wyłącznie po potwierdzonym odwzorowaniu aktualnego materiału przez status. Brak świeżego ID oznacza brak oznaczenia, nigdy domysł.
- Aktualizacja tylko niezbędnych tekstów/wierszy, bez przebudowy, zmian zaznaczenia i focusem niezależnym od grającej pozycji.
- Zachować parytet menu, palety, Ctrl+F1, pełnej pomocy oraz metadanych; uaktualnić instrukcję i rejestr.
- Niezależny przegląd kandydata przed integracją. Testy tylko celowane headless; test natywnego GUI/NVDA jest osobną bramką.

## Stan zastany i materiały

Repo główne /home/michal/projekty/AMC, gałąź codex/amc-test-public-20261109, baza 0b522c000335c7476e8e2cd6b4b35c2121e65d22. Drzewo czyste. Po fetch zdalna gałąź jest przodkiem (27 lokalnych commitów, zero zdalnych unikatowych); nie ma nowych zmian do integracji. Izolowany worktree codex/amc-inbox-playing-20261119.

Przeczytano AMC05-odtwarzany-element-na-listach.md i POD-20261105-wydajnosc-skrzynki-i-nawigacji.md. Stary AMC05 świadomie pomijał TIDAL — bieżące zlecenie rozszerza ten zakres, ale nie pozwala zgadywać stanu. Przed produkcyjną zmianą odtworzyć dokładne symptomy w małych testach.

## Materiał do dokumentacji użytkownika

Po powrocie Ctrl+I skrzynka pamięta ostatni odcinek niezależnie od Biblioteki. Ponowne Ctrl+I w otwartej skrzynce nadal doładowuje kolejne odcinki. Dotychczasowe ustawienia oznaczenia odtwarzania obejmują tekst przed/po nazwie lub brak tekstu oraz niezależny sygnał dźwiękowy. Wyszukiwanie respektuje tę samą kolejność. Pauza pozostawia informację o wstrzymaniu, bez sygnału odtwarzania. Dla WiiM i własnych strumieni Sonos wymagane jest potwierdzenie materiału przez urządzenie; brak identyfikatora nie może oznaczyć zaznaczonego ani ostatnio żądanego elementu. Znane ograniczenia usług są wymienione poniżej. To opis źródeł kandydata, nie potwierdzenie instalacji ani pomiaru NVDA.

### Końcowa kontrola rodzica — pauza na rzeczywistej liście

Rodzic ponownie uruchomił cztery zestawy C# i eksport dziesięciu rzeczywistych wyników mapperów (pięć WiiM, pięć Sonos), następnie dziewięć celowanych testów korekty: PASS. Rozszerzenie tego samego testu kontraktu o produkcyjne `_apply_streamer_activity_status` ujawniło brak etykiety dla hostowego `pauza` (RED `wiim-paused`), a osobny test wiersza natywnego ujawnił flagę sygnału podczas pauzy (RED `True is False`). Minimalna korekta wspólnych dekoratorów zachowuje tekst pauzy i ustawia flagę sygnału tylko podczas odtwarzania; nie zmienia silników, fokusu ani Radia. Końcowy, celowany wybór `test_playing_review_fixes`, `test_playback_activity`, `test_streamer_playback_history`, `test_radio_activity_presentation`: **51 passed in 0.50s**, z prawdziwym eksportem C# i bez pominięcia kontraktu. Wyników nakładających się wyborów nie sumować.

## Uzasadnienie i decyzje

Jeden autor wspólnego `gui.py`; wspólny resolver `row_matches_material()` oddziela `Row.item_id` od `Row.material_id` oraz wymaga zgodnej sesji. Loader zakładek zapisuje oba pola, więc `bookmark:<id>` nie może zostać omyłkowo uznany za utwór. Ten sam resolver obsługuje natywne statusy, wyniki wyszukiwania i historyczne listy streamerów; cue reaguje na wspólną flagę aktywności, nie na rodzaj widoku. Codex przez bieżącego dostawcę openai-codex; lokalny CLI codex nieobecny. Zakaz osobno płatnego API. Bez uruchamiania widocznej aplikacji, instalatora, lokalnego buildu Windows, instalacji na głównym komputerze, restartów i publikacji w tej fazie.

## Testy i wynik

- RED/GREEN (review 17e): nowy test rzeczywistego `SearchDialog.show_results()` najpierw odtworzył bezwarunkowy prefix etykiety dla pozycji `after`/`off`; testy `Navigator.activate_selected()` → rzeczywiste handlery z `client=None`/brakiem aktywnej grupy odtworzyły przedwczesne ustawienie potwierdzonego ID; test dokładnego prefiksu odtworzył niezgodność `sonos-ownStream:*` wobec hostowego `sonos-stream:*`.
- GREEN: `AMC_OBSERVED_MATERIAL_FIXTURE=/home/michal/.hermes/profiles/amc/cache/scratch/amc-observed-material-contract-20261119.json PYTHONPATH=wxlite:wxlite/tests uv run --offline --python 3.11 --with pytest python -m pytest -q --tb=short wxlite/tests/test_playing_review_fixes.py wxlite/tests/test_streamer_playback_history.py wxlite/tests/test_sonos_session.py wxlite/tests/test_wiim_session.py wxlite/tests/test_search_gui_navigation.py` — **199 passed in 0.72s**. Testy są headless i nie uruchamiają aplikacji ani pulpitu.
- Kontrakt C#→Python: test czyta wyłącznie `AMC_OBSERVED_MATERIAL_FIXTURE`, o root `{"cases":[...]}`. Iteruje po dowolnych nazwach i liczbie przypadków, uruchamia produkcyjne `network_stream_rows`/`material_rows` oraz parser snapshotu, potem rzeczywiste zastosowanie GUI, historię i render wyników wyszukiwania. Bez zmiennej środowiskowej test uczciwie się pomija; nie utworzono danych zastępczych.
- Kontrakt streamerów: whitelisty statusu przekazują wyłącznie `observedMaterialId` i `identityProvenance`, bez URL. WiiM przyjmuje wyłącznie `wiim-stream:*` + `uriMatch` dla aktywnego urządzenia; Sonos wyłącznie hostowe `sonos-stream:*` + `playbackItemId` dla aktywnej grupy. `buffering` nie jest odtwarzaniem potwierdzonym i nie daje etykiety, cue ani historii.

## Znane ograniczenia

Brak żywego NVDA i odsłuchu; headless nie dowodzi ich odbioru. Cztery jawne granice kontraktu:

1. **WiiM:** oznaczenie i historia działają dopiero, gdy host zwróci dokładnie unikalne znormalizowane URI jako `wiim-stream:*` z pochodzeniem `uriMatch`; odpowiedź bez tych pól czyści oznaczenie. Python nie odtwarza mapowania URI ani nie korzysta z zapamiętanego wyboru.
2. **Sonos:** tylko zapisany własny strumień (`sonos-stream:*`, `playbackItemId`) w bieżącej grupie i gospodarstwie może być oznaczony lub wejść do historii. Ulubione, kontener playlisty i dziecko playlisty pozostają celowo nieoznaczone — nie ma ich wiarygodnej tożsamości odtwarzanego materiału.
3. **Frontier Smart:** status urządzenia nie daje potwierdzonego mapowania materiału do istniejącego wiersza; nie dodano sztucznego `material_id` ani historii.
4. **Apple Music:** nie ma w tym torze statusowego material-ID i sesji; wynik/zakładka nie może otrzymać oznaczenia przez tytuł lub intencję.

To konkretne luki kontraktowe, nie wyłączenie zabezpieczenia: wymagają rozszerzenia hosta. Nie kopiować poświadczeń, nie zmieniać AV, nie wykorzystywać profili użytkownika.

## Wersja i commit

Baza funkcjonalna: `17e677aec46affe4868af0b732c7aaf1b9a8374b`; zachowano jej korektę prezentacji i ustawień. Kod/testy/dokumentacja: lokalny commit na gałęzi `codex/amc-inbox-playing-20261119`; jego SHA należy odczytać z logu przed integracją. Pakiet/instalacja/publikacja: brak; publikacja Test/Public należy do parenta po integracji i review.

## Następny krok

Review i integracja przez parenta; przed publikacją osobny odbiór GUI/NVDA oraz uzupełnienie kontraktów hosta Frontier/Apple Music. Kontrakt statusu WiiM/Sonos po stronie hosta jest osobnym zadaniem; Python pozostaje kompatybilny z jego brakiem i wtedy uczciwie nie oznacza materiału.
