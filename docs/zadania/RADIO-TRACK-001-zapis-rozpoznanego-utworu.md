# RADIO-TRACK-001 — zapis rozpoznanego utworu z radia

Status: **Ręczny eksport fragmentu gotowy w źródłach; test NVDA, instalacja i słyszalne granice utworu nadal otwarte**. Decyzja użytkownika: 10.11.2026. Badanie, prototyp, pomiary i implementacja: 11.11.2026. Ten wynik zastępuje wcześniejszy status o otwartym eksporcie z 11.11.2026. Rekord wdrożenia: [RADIO-TRACK-001-implementacja-20261111.md](RADIO-TRACK-001-implementacja-20261111.md).

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

Badanie objęło kod AMC i dokumentację Icecast oraz wcześniejszy zapis `ECOSYSTEM-001` dotyczący BlindRadio. Nie analizowano ani nie kopiowano kodu BlindRadio w tej iteracji. Commit badania: `24535a81`. Wersja zainstalowanej aplikacji: bez zmiany.


## Etap prototypu 11.11.2026

- Każda próbka PCM skopiowana z pierścienia ma teraz identyfikator konkretnego bufora, początkową i końcową pozycję bajtową oraz oznaczenie osi odsłuchu albo odbioru. Pozycje powstają pod tą samą blokadą co kopia dźwięku. Dotychczasowy odbiorca próbki nadal dostaje te same bajty, format i długość; nie dodano polecenia użytkowego.
- Test headless wymusił nadpisanie części pierścienia, oddzielne pozycje odsłuchu i odbioru oraz nowy identyfikator po utworzeniu drugiego bufora. Smoke test zakończył się kodem 0. Build SmokeTests Release i LiteHost Release przeszedł bez błędów i ostrzeżeń. Nie wykonano instalacji, testu NVDA ani eksportu dźwięku. Commit prototypu: 56cbe93b.
- Po reconnect nowy strumień dostaje osobny identyfikator segmentu w tym samym buforze. Próbka rozpoznawania nie przekracza tej granicy; do czasu zebrania trzech sekund nowego dźwięku rozpoznanie zwraca brak wystarczającej próbki. Odsłuch starszego zakresu zachowuje poprzedni identyfikator segmentu. Test headless potwierdził oba przypadki, a ponowny build SmokeTests Release i LiteHost Release przeszedł bez ostrzeżeń i błędów. Commit granicy reconnect: `4763a34b`.
- **Ograniczenia na zakończenie prototypu (uzupełnione pomiarem poniżej):** kotwice nie są utrwalane w historii rozpoznania. Wtedy nie mierzono żywego ICY/HLS ani opóźnienia metadanych. Nie ma jeszcze wiarygodnego początku i końca piosenki, testu NVDA, eksportu ani instalacji. Zapis całego rozpoznanego utworu pozostaje niedostępny.

## Pierwszy pomiar żywego strumienia — 11.11.2026

Użytkownik doprecyzował, że można rozpocząć pomiar **próbek do rozpoznawania utworów**. Cichy próbnik `RadioLiveTimingProbe` uruchomiono jako osobny proces na publicznym strumieniu HLS z listy AMC, bez zatrzymywania zainstalowanego AMC Test i bez ingerowania w nagrania. Próbnik został zbudowany w profilu Release z kodu roboczego na bazie `2ffb4bfa`; kompilacja przeszła bez błędów i ostrzeżeń. Plik danych: `work/measurements/radio-samples-20261111-eska.jsonl`. Zawiera adres strumienia i zdarzenia diagnostyczne, więc pozostaje lokalnym materiałem roboczym, nie załącznikiem do wydania.

- Trzyminutowy odczyt: 36 z 36 zapytań o próbkę zwróciło PCM. Pierwsza próbka miała 9,6 s, późniejsze osiągały 12 s; wszystkie były w jednym segmencie. Bufor próbnika miał 60 s. To potwierdza pobieranie zakotwiczonych próbek z żywego HLS, nie poprawność granic piosenki.
- Próbnik zarejestrował trzy zdarzenia `NowPlayingChanged`, lecz żadne nie niosło tytułu utworu. Na tym strumieniu nie da się porównać granicy utworu z metadanymi. Zdarzenia początkowe i końcowe nie są dowodem zmiany piosenki.
- Jedno żądanie rozpoznania po pierwszych 30 s zwróciło błąd HTTP 429 po 951 ms. Nie uzyskano poprawnego wyniku ani opóźnienia udanego rozpoznania; żądania nie ponawiano.
- Następne pomiary: strumień ICY z tytułami, przesunięcie odsłuchu względem odbioru, tor prywatnego nagrania i zachowanie przy reconnect. Konieczny jest też odsłuch rzeczywistego przejścia utworów, bo sama próbka i tytuł nie wyznaczają pewnej granicy. Do czasu ustąpienia ograniczenia usługi należy mierzyć próbki lokalnie bez kolejnych żądań Shazam.

Kod próbnika zapisano w źródłach jako `0af00681`; nie zmieniono zainstalowanej wersji, nie wykonano próby NVDA ani zapisu fragmentu utworu. Ten pomiar nie zastępuje wcześniejszych zastrzeżeń o braku gwarancji pełnego utworu.


## Pomiary ICY i porównanie z HLS — 11.11.2026

Dalszy, zakończony pomiar obejmuje cztery oficjalne strumienie, dwa uchwycone przejścia tytułu ICY, kotwice PCM i działający HLS bez tytułu. Wyniki liczbowe, metoda, ograniczenia oraz lokalizacja surowych logów: [RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md](RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md). Poprzedni plan „następne pomiary: ICY” został tym etapem wykonany; ówczesne braki dotyczące Time Shift i toru nagrania uzupełnia sekcja poniżej. Nadal nie zmierzono słyszalnej granicy. Parser tytułu ICY zachowuje teraz apostrofy; źródło i test: `0af00681`. Instalacja i wydanie: bez zmian.

## Uzupełnienie: Time Shift i prywatny tor nagrania — 11.11.2026

[Metoda, testy i ograniczenia pomiaru](RADIO-TRACK-001-pomiar-timeshift-prywatny-20261111.md).

Po uwadze użytkownika, aby prowadzić pomiar bez pytań o każdy krok, próbniki działały dalej bez zatrzymywania zainstalowanego AMC Test. Rozszerzono `RadioLiveTimingProbe` o tryb bez kolejnych żądań Shazam po odpowiedzi 429, pomiar obu osi PCM, cofnięcie kursora o 20 s i powrót do transmisji na żywo. Log nowej sondy zapisuje domenę stacji zamiast pełnego adresu. `RadioPrivateRecordingProbe` sprawdza krótki, osobny, bezgłośny tor z rzeczywistym zapisem WAV i pobraniem próbki przez `RadioRecordingControl`.

- ICY Złote Przeboje: 36/36 dostępnych próbek w 180 s; pierwsza miała 8,1 s, pozostałe osiągały 12 s. Jedyny niepusty tekst ICY był stałą nazwą stacji, bez zmiany utworu. Po cofnięciu kursor odsłuchu był o 20 s za odbiorem; po skoku na żywo różnica wyniosła 0,25 s. Bez żądania Shazam. Dane: `work/measurements/radio-samples-20261111-icy.jsonl`.
- ICY SomaFM Indie Pop Rocks!: 60/60 próbek przez 300 s, każda o długości 12 s, jeden segment. Przy zmianie nadawca przesłał komunikat stacji, a po 3,008 s tytuł nowego utworu. Zdarzenia miały pozycje końca odebranego PCM 213,60 s i 216,35 s; kolejna 12-sekundowa próbka obejmowała 205,35–217,35 s. Po cofnięciu różnica osi wyniosła 20 s, po powrocie na żywo 0,25 s. Bez żądania Shazam. Dane: `work/measurements/radio-samples-20261111-somafm.jsonl`. Odstęp zdarzeń metadanych nie jest pomiarem słyszalnego przejścia w dźwięku.
- Prywatny tor nagrania: podczas odrębnego, bezgłośnego nagrania `RadioRecordingControl` zwrócił próbkę `Capture` o długości 12 s i 4 233 600 bajtach. Jej identyfikatory bufora i segmentu oraz zakres bajtowy były takie same jak w bezpośrednim odczycie odbioru z tego toru. Próbny WAV miał 5 512 546 bajtów. Raport i plik audio są w `work/measurements/private-recording-20261111/`, ignorowanym przez Git; to lokalny materiał diagnostyczny, nie wydanie.
- Dwie kompilacje SmokeTests Release po rozszerzeniu sondy przeszły bez ostrzeżeń i błędów. Wszystkie trzy procesy zainstalowanego AMC Test (launcher, Python, host) nadal działały po pomiarach. Nie instalowano ani nie restartowano AMC.

**Granica decyzji:** pomiar potwierdza tożsamość i długość próbek na żywo, różne osie Time Shift oraz poprawny tor próbki z nagrania. Nie potwierdza słyszalnego początku lub końca utworu. Bezpieczny eksport zakresu z pierścienia już jest w źródłach; przed funkcją „zapisz cały rozpoznany utwór” nadal potrzebny jest kontrolowany odsłuch przejścia i wiarygodne granice. Obecny interfejs obiecuje jedynie ręcznie sprawdzony fragment.
