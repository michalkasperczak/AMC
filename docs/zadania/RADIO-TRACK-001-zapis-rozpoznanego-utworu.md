# RADIO-TRACK-001 — zapis rozpoznanego utworu z radia

Status: **Badanie kodu zakończone; pomiar żywego strumienia i prototyp osi czasu wymagane przed wdrożeniem**. Decyzja użytkownika: 10.11.2026. Badanie: 11.11.2026. Zastępuje status „Oczekuje” z 10.11.2026.

## Ustalenie użytkownika

Zbadać zapis rozpoznanego utworu do osobnego pliku, zainspirowany BlindRadio. Rozpoznanie może nadejść po początku piosenki, więc AMC musi najpierw sprawdzić jej granice w swoim buforze.

## Wynik przeglądu bieżącego AMC

- `LiteRadioRecognitionCoordinator` bierze do 12 sekund PCM z odtwarzanej stacji albo z prywatnego toru aktywnego nagrania. Do historii przekazuje stację, artystę, tytuł i `recognizedUtcTicks` zapisane **po** odpowiedzi usługi. `ShazamTrackRecognitionService` zwraca tożsamość nagrania, a nie pozycje początku i końca piosenki.
- `RadioTimeshiftWaveProvider` już ma licznik `_totalWritten`, pozycję odczytu `_readPosition`, długość pierścienia i format PCM. Utrzymuje zakres od `max(0, _totalWritten - _ring.Length)` do `_totalWritten`. Duży bufor leży w pliku odwzorowanym w pamięci, z limitem pojemności; przy błędzie dysku może się skrócić do 64 MB. Próbka rozpoznawania zawiera tylko bajty, format i długość — **traci początkową i końcową pozycję w pierścieniu**.
- Aktualne `TryGetRecentPlaybackAudio` kończy próbkę na pozycji **odsłuchu**, podczas gdy `TryGetRecentCapturedAudio` kończy ją na pozycji **odbioru**. W trybie przesunięcia czasu to dwie różne osie; nie wolno przyklejać rozpoznania odsłuchu do bieżących metadanych na żywo.
- Zmiana tytułu strumienia dochodzi przez `RadioPipeline.StreamTitleChanged` i jest publikowana w `NowPlayingChanged`. Obecnie nie ma przy niej pozycji bajtowej bufora ani czasu źródłowego. Serwer Icecast pozwala aktualizować tytuł niezależnym żądaniem administracyjnym, dlatego sama zmiana napisu nie jest dowodem dokładnej granicy w dźwięku. Źródło: [dokumentacja Icecast](https://www.icecast.org/docs/icecast-latest/admin_interface/).
- Prywatny tor nagrywania używa `RadioRecordingControl.TryGetRecentAudio`; rozpoznanie może pochodzić z tego toru, podczas gdy główny odtwarzacz ma inny bufor lub inną stację. Nie ma obecnie wspólnego, trwałego identyfikatora osi czasu tych dwóch torów. Istniejące `Zapisz zaznaczony fragment` dotyczy skończonego pliku audio z punktami I/O, nie wycina odcinka z żywego radiowego pierścienia.

**Wniosek:** technicznie można dodać eksport zakresu PCM z bufora aktywnego toru, ale obecny wynik Shazama i model bufora nie pozwalają uczciwie obiecać „zapisz cały rozpoznany utwór”. Potrzebne są pozycje próbek, wiarygodność granic i ręczna korekta. To wniosek z kodu, nie wynik pomiaru stacji.

## Proponowany kontrakt osi czasu

1. Nadać każdemu uruchomieniu toru odbioru niepowtarzalny identyfikator epoki i stacji. Wszystkie pozycje są wyrównanymi do ramki, rosnącymi licznikami **bajtów PCM** w tej epoce, niezależnymi od zegara ściennego. Reconnect z wymianą czytnika trzeba jawnie oznaczyć; nie scalać dźwięku przez przerwę bez komunikatu.
2. `TryGetRecent...Audio` powinno wraz z kopią próbki zwracać `epoch`, `startByte`, `endByte` i informację „odsłuch” albo „odbiór”. Wynik rozpoznania przechowuje tę kotwicę, a `recognizedUtcTicks` pozostaje wyłącznie czasem historii. Gdy rozpoznanie dojdzie po nadpisaniu zakresu, zgłosić utratę danych.
3. Zmiany ICY/HLS zapisywać z pozycją **odbioru**, tytułem i informacją o źródle; sprawdzić, czy dany dekoder rzeczywiście udostępnia metadane i z jakim opóźnieniem. Metadane są kandydatem na granicę, nie prawdą absolutną. HLS bez znaczników tytułu albo stacje z reklamami/źle ustawionym tytułem wymagają odsłuchu ręcznego. Nie wolno wyznaczać granicy przez cofnięcie o arbitralne 12 sekund od czasu rozpoznania.
4. Dla utworu z kotwicą próbki szukać poprzedniego i następnego **zweryfikowanego** przejścia tytułu w tej samej epoce, ewentualnie proponować zakres na podstawie analizy zmian sygnału. Propozycja ma stan `potwierdzona metadanymi`, `szacowana` albo `granice nieznane`. „Potwierdzona metadanymi” nadal wymaga odsłuchu przy zapisie jako cały utwór. Początek musi być starszy niż próbka, koniec późniejszy; oba w dostępnym zakresie bufora.
5. Eksport czyta spod blokady spójny zakres bez zmiany kursora odtwarzania ani stanu nagrania. Jeśli pierścień może nadpisać fragment podczas zapisu, najpierw zabezpieczyć go osobną kopią tymczasową albo zastosować ograniczone pinowanie. Weryfikować `epoch` i `oldest <= start < end <= totalWritten` bezpośrednio przed kopią i po niej. Unikać blokowania toru audio przez długi zapis na dysk.

## Proponowana interakcja, jeśli prototyp potwierdzi wykonalność

- Na rozpoznaniu w historii: `Sprawdź i zapisz fragment utworu…`. Okno mówi stację, tytuł, źródło rozpoznania, dostępny czas bufora, wybrany początek i koniec oraz poziom pewności. Gdy brak początku lub końca, nie używa nazwy „cały utwór”.
- Osobne kontrolki `Początek`, `Koniec`, `Odtwórz od początku`, `Odtwórz koniec`, `Zapisz fragment…`, `Anuluj`. Klawiatura pozwala przesuwać granice małym/dużym krokiem i odsłuchać okolice obu punktów. Każda zmiana czyta pozycję i długość po polsku/angielsku; fokus początkowy na pierwszej decyzji, po zamknięciu wraca do rozpoznania. Lista wariantów/formatów musi mieć celowe etykiety NVDA i stabilną reprezentację tekstową.
- Zapis po jawnym potwierdzeniu zakresu przez użytkownika, do wskazanego folderu i nazwy `Artysta – Tytuł` po oczyszczeniu znaków. Domyślny pierwszy format: bezstratny WAV lub FLAC z bufora PCM; „oryginalny format strumienia” nie wynika z tego bufora i wymagałby osobnego archiwum skompresowanych bajtów. MP3 byłoby jawną konwersją. Kolizja nazwy daje wybór nowej nazwy albo anulowanie; plik częściowy nie staje się gotowym wynikiem.
- Gdy próbka lub granice wypadły z bufora, tor się zmienił albo końca jeszcze nie ma, okno mówi dokładnie co jest dostępne. Można zaproponować zapis **dostępnego fragmentu** po ręcznym wyborze, bez etykiety „cały utwór”. Aktywne nagranie trwa nadal; eksport nie może go zatrzymać ani przełączyć stacji.

## Bramka przed implementacją polecenia

1. Dodać diagnostyczny prototyp kotwic próbek i metadanych, bez zmiany zachowania użytkowego. Na kontrolowanych strumieniach ICY i HLS zmierzyć różnicę między zmianą metadanych, słyszalną zmianą utworu, końcem 12-sekundowej próbki i otrzymaniem wyniku. Sprawdzić live, timeshift oraz prywatny tor nagrania.
2. Sprawdzić nadpisanie pierścienia, bufor na dysku i awaryjny 64 MB, reconnect, pauzę odsłuchu/nagrania, równoległe nagrywanie, utratę metadanych i stację z reklamami. Zmierzyć koszt bezpiecznej kopii segmentu.
3. Po wynikach wybrać, czy domyślnie oferować tylko ręczny zapis fragmentu czy również proponowane granice. Dopiero wtedy dodać komendę do wspólnego modelu, menu, palety, `Ctrl+F1`, pełnej pomocy i instrukcji oraz testy parytetu i NVDA.

## Materiał do instrukcji i zmian — szkic

„Po rozpoznaniu utworu AMC może pokazać dostępny fragment radia z bufora. Sprawdź odsłuchem początek i koniec, popraw je i wybierz miejsce zapisu. Jeśli bufor nie zawiera całego utworu, program powie o tym przed zapisaniem.” To szkic funkcji przyszłej, nie opis bieżącej aplikacji.

## Wynik, ograniczenia i identyfikacja

Badanie objęło kod AMC i dokumentację Icecast oraz wcześniejszy zapis `ECOSYSTEM-001` dotyczący BlindRadio. Nie analizowano ani nie kopiowano kodu BlindRadio w tej iteracji. Nie wykonano pomiaru żywego ICY/HLS, testu NVDA, testu eksportu, buildu, instalacji ani publikacji w ramach RADIO-TRACK-001. Wersja aplikacji: bez zmiany. Commit badania: `24535a81`.
