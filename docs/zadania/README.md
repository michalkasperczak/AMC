# Rejestr zadań AMC

**Odtwarzanie po aktualizacji / 25.11.2026: zgłoszono samoczynny start wstrzymanej stacji.** Instalacja ma już manifest alpha.528 (`25.11.2026, 02:00`); źródła wznowienia pomijały stan pauzy. W źródłach dodano bramkę i zaliczono 133 testy celowane; nowe wydanie i próba rzeczywistego aktualizatora otwarte. [Przyczyna i test](PLAYBACK-STARTUP-20261125-pauza-po-aktualizacji.md).

**Dostępność i kolejka / 25.11.2026: trzy dalsze zadania.** [Konfigurowalne zapowiedzi utworów z innych sesji](ACCESS-ANNOUNCE-20261125-inne-sesje.md); [przywrócenie `Shift+Enter` dla „Odtwarzaj jako następne”](QUEUE-004-20261125-shift-enter-nastepne.md); [pełny przegląd menu kontekstowego i cyfr słyszanych przez NVDA](MENU-001-menu-kontekstowe-i-skroty.md). Na razie zapis wymagań, bez wdrożenia.

**Podcasty / 25.11.2026: nowe zgłoszenie po `Ctrl+D`.** Gdy użytkownik idzie dalej po liście w czasie pobierania odcinka, ukończenie pobierania cofa kursor na pierwotny odcinek. Przyczyna jest wskazana w kodzie; poprawka i testy pozostają do wykonania. [Opis i kryteria](PODCAST-DOWNLOAD-FOCUS-20261125.md).

**AMC Test alpha.528 / 25.11.2026: wydanie GitHub opublikowane i zainstalowane przez użytkownika.** GitHub Actions zbudował instalator ze źródeł `4ba3ea7d`; wydanie `test-2026.11.25.0200` ma zweryfikowany instalator, plik integralności i manifest aktualizacji. Odczyt lokalnego `bundle-manifest.json` potwierdził etykietę `25.11.2026, 02:00` i ten sam commit. Nie wykonano niezależnej kontroli żywego procesu ani odbioru NVDA. [Poprawki Podcastów](PODCAST-LIBRARY-FOCUS-20261125.md).

**Podcasty / 25.11.2026: poprawka zaznaczenia po `Ctrl+Shift+L` i potwierdzenia po `Delete` wydana w alpha.528.** Wyszukiwarka zachowuje dotychczasowy wybór po dodaniu źródła, a usunięcie wybiera sąsiada. 195 testów celowanych przeszło; instalacja i odbiór NVDA pozostają otwarte. TIDAL zachowuje strony po 150 wierszy, a `Ctrl+F` szuka w pełnej pobranej kolekcji i katalogu. [Zadanie i wyniki](PODCAST-LIBRARY-FOCUS-20261125.md).

**SCHED-004 / AMC Test alpha.527 (25.11.2026): wydane na GitHub, zainstalowane i uruchomione.** Wynik zapisu planu ma być wypowiedziany po przywróceniu fokusu listy harmonogramów. 142 testy celowane, kompilacja GitHub, SHA-256 wydania, 3658 plików instalacji i jedna instancja nowej wersji potwierdzone; rzeczywisty odbiór NVDA po Enter nadal otwarty. Konfigurowalny układ tytułu okna zachowano. [Zgłoszenie i przyczyna](SCHED-004-20261125-komunikat-po-zapisie-planu.md); [dowody wydania](RELEASE-ALPHA527-20261125.md).

**AMC Test alpha.526 (25.11.2026): historyczne wydanie GitHub, pominięte w instalacji lokalnej na rzecz alpha.527.** GitHub Actions zbudował instalator ze źródeł `c3cd2952`; zdalne zasoby i SHA-256 są zgodne. Wersja zawiera HEOS, Cast/AirPlay i listę `Shift+F6` ze Spacją. [Lista źródeł](NEXT-TEST-20261124-instrukcja-i-lista-zmian-szkic.md).

**Korekta 25.11.2026:** starsze wpisy poniżej, które opisują HEOS, CAST-004, PLAY-004 lub zwykłą kolejkę Radia jako niewydane, dokumentują etap sprzed alpha.527; bieżący stan jest w [zapisie wydania](RELEASE-ALPHA527-20261125.md).

**CAST-002 / alpha.522 (24.11.2026): spójne Test i Public wydane; Test automatycznie zainstalowany i uruchomiony.** Zachowano alpha.520 oraz Cast/AirPlay. Gotowe funkcje nagrań wspólne, Apple Music tylko w Test. GitHub, oba instalatory i SHA-256, pełna integralność instalacji Test oraz jedna instancja potwierdzone. Odsłuch nowych wariantów i NVDA pozostają otwarte. [Wyniki i baza następnych wydań](RELEASE-ALPHA522-20261124-Cast-instalacja.md).

**RELEASE-ALPHA520 (23.11.2026):** końcowy kandydat Test/Public po konflikcie równoległych wydań alpha.519; ten sam zweryfikowany zakres funkcji bez Google Cast i HEOS, z zachowanym odsłuchem nagrania tylko w Test. [Zapis wydania](RELEASE-ALPHA520-20261123.md).

**RELEASE-ALPHA519 (23.11.2026):** kandydat naprawczy Test/Public zachowuje odsłuch nagrania z Test alpha.516, rozdziela adresy odtwarzacza YouTube dla profili i dodaje wznawianie podcastów z aktualnej pozycji. Kod i testy celowane gotowe; pakiety, publikacja i instalacja do osobnego potwierdzenia. [Zapis zadania](RELEASE-ALPHA519-20261123.md).

**CAST-002 (23.11.2026): wydane w Test i Public alpha.519.** Shift+C i Shift+M obejmują Radio Archiwistę oraz obsługiwane źródła natywnego TIDAL. Zdalny commit, oba instalatory z SHA-256 i dostępność w aktualizatorach potwierdzone; użytkownik instaluje sam. Odsłuch nowych wariantów, NVDA oraz istniejące braki angielskich tłumaczeń pozostają otwarte. [Wynik wydania i ścieżki](RELEASE-ALPHA519-20261123-Cast-AirPlay.md); [zakres](CAST-002-20261123-zakres-sesji-i-AirPlay.md).

**RELEASE-ALPHA518 (23.11.2026):** połączone wydania Test i Public z wideo,
wyszukiwaniem, poprawką kolejki Spotify i informacjami TIDAL; Public bez
Apple Music, oba profile bez HEOS. [Zapis wydania](RELEASE-ALPHA518-20261123.md).

**VIDEO-001 (23.11.2026): kod i 10 testów celowanych gotowe; widoczny odbiór, build i wydanie otwarte.** Obraz lokalnego filmu w obecnym odtwarzaczu AMC, wybór samego audio i otwieranie strony filmu internetowego w zwykłej przeglądarce. [Zadanie](VIDEO-20261123-odtwarzanie-w-odtwarzaczu-AMC.md).

**HEOS-001 (24.11.2026): alpha.525 zainstalowana; fizyczny głośnik odrzucił uruchomienie stacji z Ulubionych przez `play_stream` kodem 4.** Głośność 0 nie wyjaśnia odmowy. W źródłach dodano osobne `play_preset` dla Ulubionych i stabilne presety AMC; 191 testów celowanych przeszło. Nowej komendy jeszcze nie sprawdzono na głośniku, nie zbudowano ani nie wydano nowej wersji. Porządek/sortowanie/wycinanie Ulubionych i odbiór NVDA są otwarte. [Dowód i zakres](HEOS-001-20261123-rekonesans-i-adapter-odczytowy.md).

**Interfejs odtwarzania i CAST-002 (24.11.2026): część źródeł gotowa do następnego wspólnego Test.** `Shift+C` oznacza teraz wybrany odbiornik, a odczyt bieżącego materiału przez most NVDA łączy tytuł z faktyczną trasą Cast; testy źródłowe przeszły, bez wydania i fizycznego NVDA. Strzałki listy mają jeszcze wskazywać pozycję aktualnie odtwarzaną. Skróty zmiany Cast/Mirror i docelowe miejsce AirPlay pozostają tematami projektowymi. [Kontrakt list](AM-003-sesja-i-dostepny-interfejs.md); [Cast i AirPlay](CAST-002-20261123-zakres-sesji-i-AirPlay.md).

[Szkic instrukcji i listy zmian następnego wspólnego Test](NEXT-TEST-20261124-instrukcja-i-lista-zmian-szkic.md) jest do korekty Michała; nie jest opublikowany.

**CAST-003 (24.11.2026): szybkie przełączanie głównego wyjścia Cast gotowe w źródłach, bez wydania.** `Ctrl+Shift+[` i `Ctrl+Shift+]` przechodzą między komputerem i dostępnymi odbiornikami, wypowiadając wybrany cel; lista `Shift+M` oznacza wybrany dodatkowy odsłuch. Mirror ma oznaczać dwa wyjścia równocześnie. Główne AirPlay i wariant z Alt pozostają do decyzji. [Zadanie i testy](CAST-003-20261124-szybka-zmiana-wyjscia.md).

**CAST-004 (24.11.2026): główne AirPlay wdrożone w źródłach następnego Test, bez wydania i odsłuchu sprzętowego.** `Shift+C` wybiera komputer, Cast lub AirPlay jako jedno główne wyjście; `Ctrl+Shift+[` / `Ctrl+Shift+]` używają tej samej listy i mówią cel. `Shift+A` pozostaje dla Windows, `Shift+M` dla dublowania. Osobny router AirPlay wycisza lokalny silnik, steruje RAOP i przywraca dźwięk przy powrocie lub błędzie. 223 celowane testy, 40 testów ukrytego wx, 4 testy językowe i 1 test izolowanego hosta zaliczone; kompilacja hosta bez błędów. Pełny inwentarz i18n ma 2 niezaliczone testy, fizyczny AirPlay/NVDA otwarte. [Kontrakt i wyniki](CAST-004-20261124-glowne-wyjscia-bezprzewodowe.md).

**PLAY-004 (24.11.2026): Shift+F6 pokazuje aktywne odtwarzanie w znanych sesjach i na wybranych urządzeniach, kod gotowy do wspólnego Test.** Enter przenosi do wybranego odtwarzacza, Escape z niego wraca do listy wybranej sesji, a Spacja pauzuje lub wznawia zaznaczoną pozycję. Pomysł dołączenia komputera jako Mirror do zewnętrznego odbiornika wymaga osobnego toru i próby sprzętowej; nie ma jeszcze skrótu. Bez wydania i odbioru NVDA. [Kontrakt, testy i ograniczenia](PLAY-004-20261124-lista-aktywnego-odtwarzania.md).

**QUEUE-003 (24.11.2026): osobne zadanie projektowe, bez implementacji.** Opcja wspólnej kolejki dla obsługiwanych plików, podcastów i innych materiałów, z zachowaniem osobnych kolejek jako dotychczasowego trybu. Zwykłe Radio na żywo nie należy do kolejki. [Kontrakt i następny krok](QUEUE-003-20261124-wspolna-kolejka.md).

**Jedna aktywna gałąź i przekazanie 22.11.2026:** [bieżący stan oraz kolejność pracy](AMC-HANDOFF-20261122-jedna-galaz.md).

**AUDIO-EDIT Tok 22.11.2026:** [odzyskano kopię nagrania i zabezpieczono edycję plików synchronizowanych](AUDIO-EDIT-20261122-Tok-odzysk-i-zabezpieczenie.md). Oryginalna nazwa zniknęła po cięciu; jest kopia na D. Zmiana źródeł jeszcze nie została wydana.

**AUDIO-EDIT 22.11.2026:** dwa kolejne cięcia sztucznych WAV/MP3 na lokalnym NTFS przeszły. Wcześniejszą diagnozę uzupełnia dzisiejszy incydent Tok. [Diagnoza lokalna](AUDIO-EDIT-20261122-dwa-ciecia-diagnoza.md).

**AM-008 22.11.2026:** poprawiono źródłowo widoczne logowanie i komunikaty wyszukiwania Apple Music oraz zatrzymanie niewłaściwego utworu. Żywe logowanie i dokładne odtwarzanie nadal do odbioru; bez wydania. [Zadanie](AM-008-20261122-wylogowanie-i-odtwarzanie.md).

**Zapis i wycinanie nagrań — nowe zgłoszenie Michała, 19.11.2026:**
potwierdzono na głównym PC brak obu pierwotnych plików Dwójki po cięciu,
pozostawione wyniki pod technicznymi nazwami oraz log sukcesu zapisu/usunięcia
kopii przed późniejszym FileNotFoundException. Jeden wynik Michał sam przemianował.
Odczyt zakończony, przyczyna jeszcze nieustalona; osobne następne zadanie na
prywatnych fixture. Nie zmieniać nagrań użytkownika i nie obwiniać iCloud bez
dowodu. Nie ma poprawki, instalacji ani odbioru GUI.
[Zlecenie, fakty, granice i kryteria](AUDIO-EDIT-20261119-znikajace-nagrania.md).
Michał wskazał DirectCut RS jako edytor audio do porównania. Odczyt kodu MIT
potwierdził przydatne wzorce zapisu/ramek/dostępnej edycji, nie gotową zamianę.
[Ocena źródeł i ograniczenia](AUDIO-EDIT-20261120-directcutrs-wzorzec.md).
Bez instalacji lub nowego wdrożenia; kolejność zadań pozostaje bez zmiany.

**Wyjścia audio — nowe zlecenie 19.11.2026:** podążanie za domyślnym Windows
we wszystkich odpowiednich sesjach, w tym TIDAL i Spotify; niezależnie sprawdzić
martwe wznowienie tej samej stacji po utracie zapamiętanego urządzenia.
Pauza ochronna zostaje; jawna Spacja/Enter ma odzyskać dostępne wyjście bez
zmiany stacji. Niezależny odbiór `4a791c2` potwierdził i naprawił ciszę przy
wyłączonej ochronie (RED/GREEN), dodał test hostowego wznowienia i odmowy Spotify.
Końcowy `3948aec` synchronizuje aparaturę: rodzic potwierdził 747 źródeł
bez różnic i Debug exit 0. Jednak świeży wspólny Release `aef4626` ponownie
zwrócił exit 1 na `transport.pauseResume`. Integrację cofnięto w `8d1da71`;
przyczyna została następnie potwierdzona w `6c70141`: spóźnione PlaybackStarted
resetuje ochronną pauzę w stanie hosta. Rodzic niezależnie odtworzył kontrolowany
RED z późnym startem po sprawdzeniu 749 źródeł stagingu.
Kandydat `7b0a60b` miał GREEN bezpośredniego fallbacku, lecz niepełny cykl kolejki.
Drugi, ostatni cykl zakończył się `8941e34`: naturalne A→B ma potwierdzony RED/GREEN,
nowe kontrakty Stop/next i pierwotny late-fallback przechodzą także u rodzica
(749 źródeł zgodnych, Release exit 0). Niezależny review `deleg_edbfee20` dał jednak
**NIE**: przy połączeniu naturalnego przejścia i utraty wyjścia B późny start może
nadpisać nowszą ochronną pauzę hosta. To konkretny przeplot z kodu i brak testu
łączonego, nie wykonany nowy RED ani dowód samoczynnego dźwięku.
**20.11 Michał ponownie zezwolił kontynuować przez Codex; wcześniejszy limit
dwóch prób nie jest limitem konta i punkt STOP został zastąpiony.**
`deleg_adce614a` bada teraz deterministycznie scenariusz łączony na osobnej
bazie `8941e34`; dopiero po RED minimalna korekta i odbiór. Nadal bez integracji,
GUI, sprzętu, instalacji, publikacji i płatnego API w tym kroku.
[Aktualny odbiór](AUDIO-RECOVERY-20261120-late-start-odbior.md).
Spotify `7a26f3a` ma potwierdzony przez rodzica świeży build i test Release
adaptera/polityki offline. Kandydat pozostaje osobno, bez wspólnej integracji.
Wpięcie natywnych callbacków przeczytano w kodzie, nie wykonano zdarzenia
fizycznego odłączenia. [Odbiór i granice](AUDIO-RECOVERY-20261119-spotify-parent-check.md).
Brak API sinka Soloist pozostaje jawnym ograniczeniem.
Bez GUI i zmian głównego PC.
[Kontrakt, zakres adapterów i kryteria](AUDIO-RECOVERY-20261119-kontrakt.md).

**Radio Archiwista — zwykła nawigacja listy i pełny odtwarzacz, 19.11.2026:**
Ctrl+F ponownie 10 testów PASS w źródłach, jeszcze bez nowego wydania.
Podany przez Michała Test 18.11, 03:37 jest najnowszym opublikowanym Testem.
Korekta: lista ma wybierać nazwy jak lista plików, literami i cyframi;
wycofano hipotezę specjalnego selektora/aliasu godzin 7 → 07.
[Aktualna diagnoza i granice dowodu](RARCH-HOURS-20261119-diagnoza.md).
Przyjęto tylko testy/dokumentację zwykłego routingu jako `a9b0e20`, bez zmian
produkcji; rodzic niezależnie 2 PASS. Natywnego wyboru Windows nie odtworzono,
więc zgłoszenia nie uznano za naprawione. Potrzebne uzgodnione okno GUI/NVDA.
Ograniczony kontrakt Ctrl+H, zakładek, skoków procentowych i wspólnego transportu
odebrano offline: nie wykryto funkcjonalnej luki produkcji. Uzupełniono atrapy
bez osłabiania asercji oraz dodano regresje rzeczywistych handlerów. Testy i
raport zintegrowano w `8ff468c`; wykonawca 73 testy Python, rodzic po integracji
41 w zmienionych modułach oraz 3 zestawy C# PASS. Nie sumować nakładających się
wyborów. Bez GUI/NVDA, instalacji i zmian głównego PC.
[Zakres odbioru i rzeczywiste wyniki](RARCH-PLAYER-20261119-kontrakt.md).

**Zgłoszenia Huberta — 19.11.2026, #25 dane/trwałość odebrane w kodzie:**
daty przed limitem, bezpieczne „nowe”, poprawny fallback bez RSS i wąski trwały
zapis po kanale są zintegrowane w `adf6e51`. Cztery celowane trasy Windows,
build LiteHost i niezależny przegląd zaliczone; brak instalacji/publikacji.
Harmonogram #25 również odebrano i zintegrowano w `d113fae`: catch-up do 30 s,
adaptacyjna porcja, kooperacyjny budżet 75 s, zgodne starsze API i anulowanie
przyjęcia próby przed commitem. Zapisane interwały i blokady nagrywania zachowane.
Niezależny review PRZYJĄĆ; końcowy test kontrolowanego timera i rzeczywisty
dispatcher PASS. [Odbiór i granice](HUBERT25-SCHEDULE-20261119-akceptacja.md).
#26 / PR #27 (rozdziały YouTube) odebrano i zintegrowano jako `36ba1fe`,
z poprawką `516e5b3`: potwierdzony RED/GREEN dziecka utrzymującego pipe'y po
wyjściu rodzica. Rodzic potwierdził dziewięć zmienionych źródeł finalnego stagingu,
zgodność całych `src/tests` po integracji oraz `--youtube-chapters-only` exit 0.
Niezmieniony zestaw koordynatora wcześniej niezależnie PASS. Dalej #28 / PR #29
(wznowienie bez zerowania toru), z uwzględnieniem odbioru wspólnego audio.
[Wykonawczy plan rozdziałów #26](HUBERT26-CHAPTERS-20261119-plan.md).
Starych gałęzi PR nie scalano; rozwiązanie #26 przeniesiono i odebrano na
bieżącej bazie, #28 wymaga osobnego odbioru. Chrome zostaje. Kod CDP już ponawia tempo przy nowym
starcie, ale GUI Spotify blokuje zmianę; potrzebny ograniczony eksperyment,
nie obietnica gotowej funkcji. Nagrywanie nadal ma pierwszeństwo; RSS-first,
cache zakresowy i mDNS pozostają osobnymi tematami. 46 testów Python PASS
(nie dowodzą CDP/audio/NVDA). Skrzynka profilu AMC poprawiona osobno do 0.2.1;
31 testów PASS i rzeczywista wysyłka/odczyt Telegram potwierdzone.
[Pełne rozstrzygnięcie, dowody i granice](HUBERT-20261119-triage-zgloszen.md).

**Spotify / Luna / fallback Chrome — 19.11.2026, analiza zakończona:**
Soloist jest już preferowanym osobnym silnikiem; Chrome pozostaje alternatywą.
Rekomendacja: nie usuwać teraz fallbacku, ponieważ pokrywa brak gotowego Soloist
oraz ma osobną obsługę nazwanych wyjść audio. Przygotować jawny kontrakt silnika
błędów i ograniczeń dopiero po decyzji użytkowej; niczego nie wdrożono.
24 testy Soloist/katalogu i 22 nawigacji zaliczone offline, bez audio i NVDA.
Hubert odpowiedział: używa Chrome w AMC; najważniejsze dla niego są tempo
podcastów i dostępne parowanie Soloist. Chrome zachowujemy; ocena zgłoszeń zakończona.
[Pełna ocena i warunki ewentualnego Soloist-only](SPOTIFY-LUNA-20261119-ocena-i-fallback-chrome.md).

**Porządek zgłoszeń GitHub — 19.11.2026, wykonano:** zamknięto #12 (test
Sygnalisty), #16 (trwałość kolejki), #18 (dzień tygodnia Radio Archiwisty)
i #20 (pomijanie archiwów transmisji przy polskiej lokalizacji). Każdy wpis
ma komentarz z dowodem i zakresem rozwiązania; odczyt API potwierdził stan
`closed/completed`. Pozostałe 13 zgłoszeń zachowano otwarte, w tym nowe #22–#25.
Nie utożsamiono wydanego kodu z nieodbytym odbiorem instalacji i NVDA.
[Decyzje per zgłoszenie i dowody](GH-TRIAGE-20261119-zamkniecie-wykonanych-zgloszen.md).

**Nowe zlecenie i doprecyzowanie — 19.11.2026, w realizacji:** pamięć pozycji
skrzynki `Ctrl+I`, wspólne oznaczanie faktycznie odtwarzanego materiału tekstem,
dźwiękiem albo oboma we wszystkich właściwych listach sesji/usług (również
kolejki i zawartość playlist) oraz parametry przed nazwą w `NVDA+End`.
Po zakończeniu i odbiorze wszystkich poprawek: nowe wydania **Public i Test
na GitHubie**, bez automatycznej instalacji na głównym PC. Nie ponawiać
starych buildów ani nie relabelować binariów z 18.11. Bramki bezpieczeństwa,
integralności i rzetelnego raportowania odbioru nadal obowiązują.
[Plan całości, decyzje i następny krok](RELEASE-20261119-odtwarzany-skrzynka-status.md).

**NVDA+End — 19.11.2026, źródła zintegrowane (`d4a298c`):** parametry audio
przed nazwą materiału, bez zmiany Radia i dodatku NVDA. Niezależny przegląd
bez blokera źródłowego; po integracji 28 testów statusu/Radia i jeden zestaw
C# TIDAL zaliczone. Pełny LiteHost i żywy NVDA nadal do odbioru; brak nowego
wydania. [Kontrakt i dowody](NVDA-END-20261119-parametry-przed-nazwa.md).

- `INBOX-PLAYING-20261119` — **Źródła po ograniczonym niezależnym przeglądzie zintegrowane; brak buildu, instalacji i publikacji.** Pamięć pozycji `Ctrl+I`, wspólne oznaczanie materiału i poprawiona kolejność tekstu w wyszukiwaniu. WiiM i własne strumienie Sonos wymagają potwierdzonego ID z hosta; pauza zachowuje tekst bez sygnału. Sonos ulubione/zawartość playlist, Frontier Smart i Apple Music nadal nie mają bezpiecznego mapowania materiału. Po wspólnej integracji 94 celowane testy Python i pięć zestawów C# zaliczone, bez żywego GUI/NVDA. Pełnego zakresu wszystkich usług nie uznano za ukończony. [Zakres, wynik i ograniczenia](INBOX-PLAYING-20261119-fokus-i-odtwarzany.md).

**Wyszukiwanie — 18.11.2026, zintegrowane w źródłach (`bb3fdcd`):** dodano
`Ctrl+F` Radio Archiwisty i wybór usług dla `Ctrl+Shift+F` w Ogólnych.
Aktywność sesji i zgoda na wyszukiwanie to osobne warunki; czasowo wyłączona
sesja jest pomijana, ale pamięta zaznaczenie na ponowne włączenie.
Żywy publiczny katalog potwierdzony; 124 celowane testy po poprawkach,
po integracji 14 testów zmienionej granicy. Bez instalacji, publikacji i NVDA.
[Pełny kontrakt i kryteria](SEARCH-SCOPE-20261118-radioarchiwista-i-uslugi.md).

**Ctrl+C w Podcastach — 18.11.2026, zintegrowane w źródłach (`d7627f2`):** pełny opis,
następnie strona odcinka, bezpośredni plik audio, na końcu strona podcastu.
`Ctrl+Shift+C` bez zmian; pozostałe sesje i YouTube bez przebudowy.
Celowane RED/GREEN, 128 testów i kontrola produkcyjnych dróg kopiowania, bez nowej
instalacji lub wydania. [Zakres i wynik](PODCAST-COPY-20261118-opis-i-kolejnosc-linkow.md).

**Nowe zlecenie Michała — 18.11.2026:** najpierw usunąć gubienie miejsca na
liście po `Ctrl+Shift+L` i sprawdzić pokrewne ścieżki Biblioteki, Ulubionych,
kolejek i playlist; zmieniać tylko potwierdzone błędy. Następnie zbadać dawną
próbę odtwarzania Apple Music, bibliotekę GitHub i błędne identyfikatory.
Źródła poprawki fokusu zintegrowane i niezależnie sprawdzone, także z filtrem;
13 celowanych przypadków headless zaliczonych, bez żywego NVDA i instalatora.
Rekonesans Apple zakończony; rzeczywista próba czeka na uzgodnione warunki
interaktywne. Bez zmian instalacji na głównym PC, nowego lokalnego buildu
Windows i bez zakupu konta. Raport zapisano i odczytano na `MICHAL` w notatkach.
[Zakres, kryteria i wyniki](AMC-20261118-fokus-kolekcji-i-apple-playback.md).

**Porządek materiałów na głównym PC — 18.11.2026:** zapisano podsumowanie
własnych zgłoszeń Michała i mapę folderów w istniejącym `AMC - notatki`.
Oryginał `Hubert zgłoszenia 2026-11-16.md` zachowany bez zmiany; dodano
skróty do notatek i poniedziałkowego pliku w `Hermes\AMC` oraz wejście
z głównego folderu projektów. Trzy dokumenty i trzy skróty odczytano ponownie.
Bez kasowania dawnych kopii/worktree, synchronizacji kodu i bez instalacji.
Test na głównym PC ma źródła nowego wydania; zainstalowany Public pozostaje
starszy. [Zakres, stan i dowody](DOC-20261118-porzadek-materialow-na-glownym-PC.md).

**Najnowsze polecenie 18.11.2026: „To instaluj w sensie na GH”.** Po informacji
o sukcesie nowych Public/Test, braku detekcji w nowym przebiegu oraz nadal
nierozstrzygniętym dawnym RCX i braku odbioru NVDA, Michał zlecił publikację
nowych instalatorów na GitHubie. Bez instalacji na głównym PC, kolejnego
buildu i lokalnych dużych pobrań. **Wykonano:** publisher `44292203528`
zakończył się sukcesem; opublikowano Public `public-2026.11.18.0329`
(release `449870446`) i Test `test-2026.11.18.0337` (release `449870638`).
Oba tagi wskazują source `1723786c4557ef68bb3922189a028099ce96db31`.
Świeży odczyt potwierdził sześć załączników, ich rozmiary i SHA oraz zgodność
z faktycznym walidatorem aktualizatora. Nie pobrano lokalnie EXE, nie wykonano
instalacji/żywego NVDA ani restartu. Historyczny RCX nie ma werdyktu Microsoftu.
Szczegóły i sumy EXE: REL-002. Poniższa chronologia zachowuje wcześniejsze stany.

Wcześniejsza dyspozycja po naprawie #21: wydać na GitHubie **Public i Test
z Apple Music**, bez nowych lokalnych Windows/VM/Sandboxów. „Testowy Windows”
w poprzednim meldunku oznaczał zwykły Windows komputera HERMES, nie nowy
zainstalowany system. Chroniony workflow obu profili z poprawką #21
uruchomiono na GitHubie po review: run `44221728197`, commit
`aca4bdb74348db574a8a9552dcb406196c1ece16`; ostatni odczyt wskazuje
`failure`: poprawna kontrola Public odrzuciła Apple Music skopiowane
z wejściowego runtime Test. Defender w tym przebiegu nie wykrył zagrożenia;
brak instalatorów i publikacji. Odczytano 8 raportów, hash ZIP zgodny z API.
Poprawkę rozdzielającą fizyczne kopiowanie Public/Test odebrano przez
niezależny RED/GREEN oraz sprawdzenie pięciu blobów #21. Nowy chroniony
run `44229055217` z commita `1723786c4557ef68bb3922189a028099ce96db31`
na dokładnej gałęzi r2 zakończył się **success**. Powstały oba kanoniczne
instalatory alpha.511 z #21; Public i Test mają osobne dowody ISCC exit 0.
Odczytano 13 raportów; hash małego ZIP-a zgodny z API. Końcowy Defender:
aktywna ochrona, brak wykluczeń, detekcji i zdarzeń 1116/1117.
Instalatory oraz sidecary są w Actions artifact `14395074212`; nie pobrano
ich lokalnie. Hash tego archiwum nie jest sumą konkretnego EXE.
**Po samej budowie, przed opisanym wyżej publisherem, nie było release; nadal brak odbioru instalacji/Windows/NVDA.** Dawny alert
RCX pozostaje nierozstrzygnięty; nowy poprawny przebieg sam go nie rozstrzyga.
Monitor zakończony, starego runu nie ponawiano; bez kolejnej budowy.
Apple Music: użytkownik potwierdza sprawne wyszukiwanie/dodawanie do Biblioteki,
bez odtwarzania; kolejne poprawki wyszukiwania pozostają poza zakresem.

Aktualne polecenie po doprecyzowaniu 18.11.2026: **naprawić najnowsze
zgłoszenie Huberta #21 — komunikaty Folderów Biblioteki i nierozróżnialne
nazwy folderów**. Następnie, bez oczekiwania na kolejne „kontynuuj”,
wrócić do **wydania instalatora na GitHubie**, nie instalowania na głównym PC.
To końcowe doprecyzowanie Michała zastępuje nieokreślone odłożenie wydania;
nie znosi bramek bezpieczeństwa, integralności i odbioru na Hermesie.
Poprzednia interpretacja „wdrażaj, poprawiaj i sprawdzaj” jako polecenia
odbioru instalatora jest nieaktualna. Niedobór miejsca Windows nie blokuje
rozwoju źródeł i nie zmienia pierwszeństwa #21.

- `LIB-021` — **Źródła naprawione; poprawka opublikowana 18.11 w Public i Test z source `1723786`, bez żywego odbioru NVDA.** Dialog ogłasza wyniki własnym announcerem, odmowa wskazuje ścieżkę konfliktu, a identyczne nazwy korzeni są rozróżniane dopiskiem rodzica. PL/EN i callbacki sprawdzone; 6 celowanych Python oraz wydzielony zestaw Core zaliczone. [Plan i dowody](LIB-021-foldery-biblioteki-komunikaty-i-nazwy.md). Publikacja na GitHubie wykonana po jawnej decyzji Michała o nowych artefaktach i ograniczeniach (REL-002); osobno pozostaje odbiór Windows/NVDA i analiza historycznego RCX. Nie jest to instalacja u użytkownika.

Poprzedni wynik diagnostyczny REL-002 (18.11.2026): **instalator diagnostyczny z chronionej
VM GitHuba powstał**, run `44197448182`, źródła `9739fe3`, SUCCESS.
Po osobnej zgodzie użytkownika włączono ochronę wyłącznie na efemerycznej VM;
Defender pozostał aktywny, wymagane skany i kanoniczna budowa przeszły,
końcowy audyt nie wykazał detekcji ani zdarzeń 1116/1117. Plik Test zachowano
jako Actions artifact `14380677377`, nie jako release; 10 raportów odczytano.
Job i cleanup zakończone; GitHub automatycznie likwiduje standardową VM.
Nie kompilowano ani nie instalowano lokalnie, nie zmieniano ochrony na Hermesie
ani głównym PC. Historyczne RCX nadal bez werdyktu; wydanie, instalacja
i odbiór Windows/NVDA nie są zaliczone. Nie ponawiać budowy zamiast odbioru.
Szczegóły w [REL-002](REL-002-kanoniczne-wydania-test-public-github.md).

- `YT-007` — **#20 zamknięte 19.11 jako wykonane w opublikowanych Public/Test z 18.11; #19 nadal otwarte.** Odczyt statusu archiwów bez lokalizacji i dopasowanie polskich tytułów po ID są w source `1723786`; Michał potwierdził widoczność wcześniej pomijanych transmisji. Import plików, typowanie OPML i ograniczona lista live/upcoming są w tych samych źródłach, ale pozostały odbiór użytkowy zachowuje osobny status. Nowe problemy scalania i odświeżania pozostają w #25. Bez nieoficjalnego logowania TV. [Plan, kryteria i wyniki](YT-007-zgloszenia-19-20-import-i-transmisje.md).

- `HUBERT-20261116` — **W trakcie; #17, bezpieczna część #10, #18 oraz komunikat niedostępnej playlisty zintegrowane lokalnie; MP3 i restart kolejki odebrane bezokiennie** — rzeczywisty tor MP3/limitera i seek zaliczone, kolejka przywrócona przez drugi proces hosta; aparat #14 wcześniej 46/46. Bundle Public/Test alpha.511 z 17e01e44 i ich prywatne instalacje na Hermesie przeszły integralność i start; **publikacja oraz dalsze uruchamianie wstrzymane**: Defender wykrył Worm:Win32/Mofksys!pz w trzech tymczasowych plikach kompilacji Test, a jego EXE pochodzi z kompilacji diagnostycznej po błędzie kanonicznego wrappera. Gotowe EXE przeskanowano bez nowego alertu, ale nie dowodzi to fałszywego alarmu. Odczytowa diagnoza zakończona: podpisy producenta czterech składników Inno zweryfikowano niezależnie, ale nie rozstrzygnięto alarmu. Użytkownik zgodził się na przekazanie próbek Microsoftowi bez profili i danych logowania; podniesienie uprawnień już działa, ale kwarantanna jest pusta i próby eksportu trzech RCX nie zwróciły plików. W MPLog odczytano hash jednego RCX; logowanie i dodatkową zgodę OAuth zatwierdzono po osobnym potwierdzeniu użytkownika. Portal pokazał rekord dokładnie tego hasha (4427264 B); jednokrotnie wywołano Rescan File, lecz nie ma werdyktu, potwierdzenia przyjęcia ani submission ID, a odczytana później historia ma 0 wpisów. Zwykły formularz wymaga pliku, więc oryginałów nie wysłano. Dowód: microsoft-portal-status-20261117.json w logach alpha.511. Porównanie z oryginalnymi najnowszymi instalatorami Public/Test z GitHuba zakończone w dostępnym zakresie: SHA potwierdzone, rodzina formatu Inno wspólna; starego payloadu nie wyodrębniono przez nieobsługiwany format, przyczyna detekcji nadal nierozstrzygnięta. Ochrony nie zmieniano. Korekta użytkownika z 17.11: blokada dotyczy kandydatów i wydania, nie rozwoju źródeł ani niezależnych testów; prace przez Codex są kontynuowane, bez obchodzenia Defendera. Zintegrowano poprawę technicznych podpisów menu bez zmiany przypisań (41 testów celowanych) oraz zapis dowodów uruchomienia kompilatora (rzeczywiste próby PowerShell 5.1); przyczyna exit 2 i alarm Defendera nadal nierozstrzygnięte, bez nowego instalatora i odbioru NVDA. Monitor AMC/Sygnalisty działa co 5 godzin, Soloist cotygodniowo. Pozostają rzeczywiste aktualizacje/instalatory, celowany NVDA, konta/odtwarzanie Spotify i publikacja Public/Test; zgłoszeń nie zamknięto. Aktualizacja głównego komputera ręczna; testowe instalacje wyłącznie na Hermesie. [Podział i kryteria odbioru](HUBERT-20261116-zgloszenia-i-wydanie.md).

Aktualny punkt wejścia dla Hermesa (16.11.2026): [pełne przekazanie](../../HERMES_AMC_PRZEKAZANIE_2026-11-16_PL.md) i [katalog wszystkich plików zadań](HERMES_AMC_KATALOG_ZADAN_2026-11-16_PL.md).

- `DOWNLOADS-003` — **15.11.2026: poprawki pobierania wydane na GitHubie
  dla AMC Test i AMC Public; uruchomiony Test alpha.510 jest na D; pobieranie czeka na odbiór**.
  Folder Radio Archiwisty i naprawa ścieżek Ctrl+J; Test zawiera Apple Music.
  [Opis i wyniki](DOWNLOADS-003-folder-radioarchiwisty-i-naprawa-historii.md).

- `AM-007` — **15.11.2026: alpha.509 wydana w kanale Test; lokalna instalacja
  alpha.507, bez odbioru NVDA** — Ctrl+F obejmuje cztery rodzaje wyników
  Biblioteki i katalogu, Enter otwiera wykonawcę, album i playlistę, prawa
  strzałka pokazuje powiązania, listy pobierają po 150 pozycji. Kandydat
  alpha.508 sprzed korekty jest nieaktualny; wydanie Test alpha.509 jest
  przygotowane do ręcznej aktualizacji i dalszego odbioru.
  [Opis i wyniki](AM-007-wyszukiwanie-i-listy-150.md).

- `POD-PERF-20261114` — **alpha.507 opublikowana i zainstalowana w Test i Public; użytkownik 16.11 zgłasza poprawę szybkości; pomiary i odbiór NVDA otwarte** — `0` wyłącza automatyczne sprawdzanie podcastów i YouTube, dodatnia liczba ustala takt; F5 i Ctrl+F5 działają na żądanie. Pozostaje koszt pełnego wczytania i przeliczenia około 92 tys. odcinków przy zapisie porcji odświeżenia; czasu natywnej listy i NVDA jeszcze nie zmierzono. [Pomiary, porównanie i ograniczenia](POD-PERF-20261114-odswiezanie-duzej-biblioteki.md).

- `QUEUE-002` — **alpha.504 wydane; korekta z 24.11 wyłącza kolejkę w zwykłym Radiu tylko w źródłach, bez nowego wydania** — przenoszenie i Delete w rzeczywistych kolejkach, czas trwania w `Ctrl+Q` i playlistach. Radio Archiwista zachowuje kolejkę nagrań. Odbiór NVDA otwarty. [Opis i wyniki](QUEUE-002-przeglad-kolejek-sesji-internetowych.md).

- `AM-006` — **jawne otwieranie w oryginalnym Apple Music jest w zainstalowanym AMC Test alpha.507; odbiór NVDA otwarty** — menu kontekstowe otwiera oryginalną aplikację; Enter na utworze wskazuje to polecenie bez uruchamiania ukrytego odtwarzacza. [Szczegóły](AM-006-przekazanie-utworu-do-apple-music-windows.md).
- `A11Y-COLLECTIONS-20261113` — **Ctrl+F Apple Music i dalsze strony po 100
  są w działającym AMC Test alpha.507; bez odbioru NVDA** — wyszukiwanie Biblioteki
  Apple poza wczytaną stroną i osobny kontekst filtra wyników.
  [Szczegóły](A11Y-COLLECTIONS-20261113-fokus-mowa-sortowanie.md).
- `PLAYER-20261113` — **zmiany w wydanym i zainstalowanym Test/Public alpha.507; odbiór NVDA otwarty** — Sonos
  mówi tylko potwierdzoną głośność, Spotify udostępnia suwak przez Tab, a
  `Ctrl+Shift+C` Apple Music kopiuje bezpośredni lub nazwany link wyszukiwania.
  Odtwarzanie i `Shift+A` Apple odłożono do jutrzejszej próby oficjalnej
  aplikacji. [Szczegóły](PLAYER-20261113-sonos-spotify-apple-link.md).

- `RECOG-HIST-20261111` — **W trakcie; kod, testy celowane i kompilacja hosta zaliczone, bez instalacji i odbioru NVDA** — bitrate w historii TIDAL, Biblioteka i Ulubione rozpoznanego utworu oraz jednoznaczne kopiowanie łącza. [Szczegóły](RECOG-HIST-20261111-tidal-bitrate-kolekcje.md).
- `RADIOBROWSER-001` — **W trakcie; źródła, celowane testy modelu i kompilacja hosta gotowe, bez instalacji i odbioru NVDA** — `Ctrl+F5` w Radiu otwiera Radio Browser, który prowadzi przez kontynenty, kraje i stronicowane stacje. TuneIn odłożono. [Szczegóły](RADIOBROWSER-001-przegladanie-katalogu.md).
- `RADIO-FAST-001` — **Zrobione — wycofane 10.11.2026** — zachowano dotychczasowe Page Up/Page Down i odczyt nazwy stacji; dodatkowe nazwy poleceń cofnięto, bez optymalizacji wczytywania. [Szczegóły](RADIO-FAST-001-pageup-pagedown-w-odtwarzaczu.md).
- `YT-006` — **Kod i testy celowane gotowe; instalacja i odbiór NVDA później** — wybór konkretnego wariantu pojedynczego filmu YouTube. [Szczegóły](YT-006-dokladny-wybor-wariantu-pobrania.md).
- `RADIO-TRACK-001` — **Ręczny zapis fragmentu gotowy w źródłach, dwa przejścia dźwięku zmierzone; bez instalacji i odbioru NVDA, słyszalne granice otwarte** — kandydaci zmiany sygnału poprzedzają tytuł ICY o różny czas. [Wdrożenie](RADIO-TRACK-001-implementacja-20261111.md). [Pomiary](RADIO-TRACK-001-zapis-rozpoznanego-utworu.md). [Przejścia](RADIO-TRACK-001-granice-sygnalu-20261111.md).

- `NVDA-DUAL-20261110` — **Wydanie opublikowane; 11.11 wykryto niezgodność dodatku 0.4.1, przygotowano automatyczną podmianę w cichej aktualizacji; instalacja i odbiór czekają na koniec nagrywania** — jeden dodatek 0.4.2 kieruje skróty do AMC Test lub AMC Public przez osobne potoki. [Wynik i dalszy odbiór](NVDA-DUAL-20261110-dwa-profile-jeden-dodatek.md).

- `RELEASE-20261111` — **Przygotowanie zbiorczej aktualizacji; wydanie i instalacja po zakończeniu bieżących zadań** — polecenie użytkownika z 11.11 obejmuje GitHub i lokalne AMC. [Stan i warunki](RELEASE-20261111-zbiorcza-aktualizacja.md).
- `RELEASE-20261109-alpha488` — **Wydane 9.11; Public zainstalowany, Test nieinstalowany w tamtym etapie** — historyczny rekord wydania alpha.488. [Plan i wyniki](RELEASE-20261109-alpha488.md).

- `HIST-REL-20261109` — **Kod i testy celowane gotowe; bez instalacji i odbioru NVDA** — nazwy albumu, wykonawcy, podcastu i kanału pod prawą strzałką w historii oraz kopiowanie nazw i dokładnych linków. [Szczegóły](HIST-REL-20261109-nazwy-i-linki.md).

- `HUBERT-20261109` — **W trakcie; kod i testy celowane gotowe, bez instalacji i odbioru NVDA** — przester MP3 oraz zapis zwykłej kolejki plików dla Test/Public. [Wynik](HUBERT-20261109-MP3-i-kolejka.md).

- `AMC-20261109-SOLOIST-LOSSLESS` — **Test i Public alpha.487 opublikowane; instalacja, parowanie i odsłuch NVDA czekają** — Soloist w obu profilach żąda poziomu Lossless dla muzyki Premium. [Stan i ograniczenia](AMC-20261109-Soloist-Lossless-i-wydanie.md).

- `RADIO-REC-20261109` — **W trakcie; kod i testy celowane zaliczone, bez instalacji i odbioru NVDA** — historia rozróżnia brak pliku, kopię do odzyskania i kolizję nazwy przy zapisie. [Szczegóły](RADIO-REC-20261109-status-i-kolizje.md).

- `SEARCH-MEMBERSHIP-20261109` — **Kod i testy celowane gotowe; bez instalacji i odbioru NVDA** — `Ctrl+Shift+L` i `Ctrl+Shift+U` w wynikach wyszukiwania przełączają potwierdzony stan Biblioteki i Ulubionych w obsługiwanych sesjach. [Szczegóły](SEARCH-MEMBERSHIP-20261109-wyniki-ulubione-biblioteka.md).

- `PLAYLIST-20261107` — **Public alpha.485 zainstalowany i uruchomiony; odbiór NVDA w toku** — Delete w playlistach i dodawanie wybranych utworów z otwartego albumu TIDAL; pełny Test później. [Szczegóły](PLAYLIST-20261107-delete-i-zaznaczenie.md).

- `RADIO-VIANNEY-20261107` — **Adres ulubionego poprawiony w profilu AMC Test; odsłuch w AMC i NVDA niepotwierdzony** — oficjalny kanał YouTube zastąpił niedostępny adres IP. [Szczegóły](RADIO-VIANNEY-20261107-poprawa-ulubionego.md).

- `PRESET-20261106` — **W trakcie; krótsze etykiety i 25 testów zaliczonych, budowa, instalacja i odbiór NVDA odłożone do zbiorczej aktualizacji** — wiersze presetów podają numer, nazwę i skrót. [Szczegóły](PRESET-20261106-krotszy-odczyt-miejsc.md).
- `AUTH-PUBLIC-20261106` — **Instrukcja Client ID Spotify dla Test i Public oraz przycisk panelu w źródłach; bez nowego buildu** — ten sam własny identyfikator może być wpisany do obu profili, z osobnym logowaniem. Zainstalowany Public alpha.486 pozostaje bez tej zmiany okna. [Szczegóły](AUTH-PUBLIC-20261106-logowanie-tidal-spotify.md).
- `AMC-TEST-ALPHA487-SOLOIST` — **Kandydat zastąpiony wspólnym wydaniem Test/Public 9.11.2026** — wcześniejsza budowa Test z Soloist, Sygnalistą i lokalnym Sonosem. [Stan historyczny](AMC-Test-alpha487-Soloist-20261109.md).

Korekta kandydata Public z 30.10.2026: [PUBLIC-20261030-uzupelnienie-poprawek-huberta.md](PUBLIC-20261030-uzupelnienie-poprawek-huberta.md). Obejmuje dodatkowe PR #7 i #8; zgłoszenie #5 ma osobne zadanie do następnej aktualizacji z Radioarchiwistą.

Ten katalog jest kanonicznym miejscem zapisywania osobnych zadań, które mają
przetrwać między rozmowami i być czytelne zarówno dla Codexa, jak i Hermesa.
Rejestr nie uruchamia pracy samodzielnie. Zadanie rozpoczyna się dopiero po
jawnym poleceniu użytkownika albo przez osobno uzgodniony harmonogram.

- `LIST-20261105-konfiguracja-odczytu` — **W trakcie; kolejność pól globalnie i dla sesji wdrożona w kodzie, bez odbioru NVDA** — osobny odczyt góra/dół i lewej strzałki, przesuwanie pola Alt+góra/dół; poprawa regresji szybkości nagrań i hosta. [Szczegóły](LIST-20261105-konfiguracja-odczytu.md).
- `TID-LIST-PLAYER-20261105` — **Alpha.473 opublikowana na GitHubie; nieinstalowana, czeka na zbiorczy pakiet** — Max FLAC, krótszy odczyt albumu, historia natywnego TIDAL, początek po Page Up/Down i audyt Spotify. [Szczegóły](TID-LIST-PLAYER-20261105-followup.md).
- `LIST-SEARCH-GLOBAL-20261105` — **Kod i 240 testów gotowe; budowa i instalacja odłożone na koniec trwających nagrań** — krótszy odczyt TIDAL i Spotify, wyniki wyszukiwania z rodzajem pozycji oraz wspólne listy z każdej sesji. [Szczegóły](LIST-SEARCH-GLOBAL-20261105.md).
- `RECOG-TIDAL-20261105` — **Kod i testy celowane gotowe; oczekuje na bezpieczną budowę** — rozpoznany album wraca do albumów wykonawcy; Enter uruchamia TIDAL w AMC, a przekazanie do oryginalnej aplikacji jest jawne. [Szczegóły](RECOG-TIDAL-20261105.md).
- `AUDIO-BITRATE-20261105` — **AMC Test alpha.480 zainstalowany; oczekuje na odsłuch NVDA** — odczyt bitrate/znanej jakości objęto TIDAL, Radio, pliki, Archiwistę, podcasty, Spotify Web i WiiM; pełny opis trafia także do komend NVDA. [Szczegóły](AUDIO-BITRATE-20261105.md).
- `LIST-20261105` — **Wydane i zainstalowane w Test alpha.472; odbiór NVDA otwarty** — czas TIDAL bez słowa „Czas” i jedno przeliczenie menu na ruch strzałką. [Szczegóły](LIST-20261105-czas-tidal-i-strzalki.md).
- `PLAYER-20261105` — **Wydane i zainstalowane w Test alpha.472; odbiór NVDA otwarty** — J skacze do wpisanego czasu, Shift+J do procentu; zakładki działają w natywnym TIDAL. [Szczegóły](PLAYER-20261105-skoki-i-zakladki.md).

## Historyczny punkt wznowienia z 7.11.2026

Najnowsza lokalna instalacja to **AMC Test — 07.11.2026, 13:54** (`0.1.0-alpha.482`), commit `ed9d61acff8c71e517b5e25b5c454012b314397a`. Pełna kontrola 4196 plików instalacji oraz start jednej instancji ze stałej lokalizacji przeszły. To kandydat do ręcznego odbioru NVDA, bez publikacji alpha.482. Szczegóły i ograniczenia Public: `AUTH-PUBLIC-20261106`.

Alpha.482 powstała na gałęzi `codex/konfigurowalne-skroty-20261026`, z czystego zarządzanego checkoutu commita `ed9d61ac`. Niezakończone zmiany presetów w kanonicznym checkoutcie pozostają poza tą paczką. Przy kolejnej budowie zachować kompletny runtime obecnej instalacji, w tym zależności Cast i urządzeń sieciowych; zależności TIDAL są dołączane osobno.

## Zasady prowadzenia

- Każde większe zadanie ma stały identyfikator i osobny plik o nazwie
  `<OBSZAR>-<NUMER>-<krotka-nazwa>.md`.
- Dozwolone statusy: `Oczekuje`, `W trakcie`, `Zablokowane`, `Zrobione`.
- Przed pracą przeczytaj ten rejestr, właściwy plik zadania, `AGENTS.md` oraz
  dokumenty wskazane przez zadanie.
- Nie rozpoczynaj pozostałych wpisów tylko dlatego, że znajdują się w rejestrze.
- Przy rozpoczęciu zmień status w pliku zadania i tutaj na `W trakcie`.
- Po zakończeniu zapisz w pliku zadania datę, wynik, zmienione pliki, commit,
  uruchomione testy z rezultatami i dokładną wersję instalacji, jeśli powstała.
  Dopiero po zweryfikowaniu kryteriów akceptacji ustaw status `Zrobione`.
- W każdym rozpoczętym zadaniu prowadź również sekcje `Materiał do dokumentacji
  użytkownika`, `Uzasadnienie i decyzje`, `Testy i wynik`, `Znane ograniczenia`
  oraz `Wersja i commit`. Tekst użytkowy ma być gotowy do automatycznego
  złożenia instrukcji i opisu wydania, ale pozostaje projektem do korekty i
  zatwierdzenia użytkownika.
- Korekty użytkownika zapisuj z datą i oznaczaj, które wcześniejsze ustalenie
  zastępują. Nie utrzymuj dwóch sprzecznych wariantów jako równoważnych.
- Jeżeli praca utknęła, ustaw `Zablokowane` i opisz konkretną przeszkodę oraz
  najkrótszy sposób wznowienia.
- Nie zapisuj tu haseł, tokenów, danych kont ani prywatnych logów użytkownika.
- Korekta użytkownika z 27.10.2026: domyślnie stosuj celowane testy bez
  widocznego interfejsu. Pełną regresję GUI, Sonosa lub WiiM, testy otwierające
  okna, przejmujące fokus albo schowek uruchamiaj dopiero na wyraźną prośbę;
  przed proponowanym testem wydaniowym najpierw opisz wpływ i uzyskaj zgodę.

## Zadania

- `UPDATE-20261101` — **W trakcie; alpha.464 opublikowana i zainstalowana, potrzebny odsłuch NVDA** — czytelny wynik i ochrona przed powtórnym sprawdzeniem podczas instalacji. [UPDATE-20261101-ponowna-aktualizacja.md](UPDATE-20261101-ponowna-aktualizacja.md).

- `RADIO-20261101` — **W trakcie; alpha.463 opublikowana i zainstalowana, potrzebny odsłuch NVDA** — bitrate i kodek bieżącej stacji w odczycie paska stanu NVDA+End oraz Alt+D. [RADIO-20261101-parametry-strumienia.md](RADIO-20261101-parametry-strumienia.md).

- `RARCH-003` — presety katalogów Radio Archiwisty, domyślnie Najnowsze nagrania i Program 1. [RARCH-003-presety-katalogow.md](RARCH-003-presety-katalogow.md).
- `RELEASE-20261031` — **Kandydat AMC Test alpha.461; testy zaliczone, instalacja i GitHub do weryfikacji** — wspólna aktualizacja tytułu, Radio Archiwisty i diagnostyki. [RELEASE-20261031-alpha461.md](RELEASE-20261031-alpha461.md).

- `MODAL-001` — **W trakcie** — zabezpieczenie historii rozpoznawania i innych okien przed pozostawieniem wyłączonego głównego okna AMC; osobny przegląd po zgłoszeniu z 31.10.2026. Szczegóły: [MODAL-001-okna-blokujace-glowny-interfejs.md](MODAL-001-okna-blokujace-glowny-interfejs.md).

- `PERF-001` — **W trakcie; PR #6 Huberta zastosowany lokalnie, 65/65 testów celowanych zaliczonych, bez commitu, builda, instalacji i publikacji** — pominięcie zbędnego przeliczania dużych list; osobny projekt dla przełączania widoku i Escape z odtwarzacza ze zgłoszenia #5. Szczegóły: [PERF-001-duze-listy-hubert.md](PERF-001-duze-listy-hubert.md).

- `PERF-002` — **W trakcie; izolowany kandydat źródłowy, testy bez okien i próba rzeczywistej kontrolki zaliczone; czeka na odbiór NVDA, bez instalacji i publikacji** — duża lista Plików lokalnych i powrót Escape ze zgłoszenia #5. Szczegóły: [PERF-002-duze-biblioteki-plikow.md](PERF-002-duze-biblioteki-plikow.md).

- `PODRAD-001` — **W trakcie; dwa domyślnie wyłączone przełączniki osobnych folderów Radia i Podcastów, celowane testy oraz build hosta zaliczone; bez instalacji i próby NVDA**. Szczegóły:
  [PODRAD-001-osobne-foldery-stacji-i-podcastow.md](PODRAD-001-osobne-foldery-stacji-i-podcastow.md).

- `RARCH-001` — **W trakcie; poprawki po pierwszym odbiorze przygotowane
  do osobnego wydania AMC Test 0.1.0-alpha.451; potrzebny odbiór NVDA;
  kod, testy i pull request
  [michalkasperczak/AMC#4](https://github.com/michalkasperczak/AMC/pull/4)
  (gałąź `radioarchiwista-sesja`, autor radioarchiwista) są gotowe;
  pozostaje odbiór właściciela i ręczny odbiór NVDA na AMC Test** —

- `REL-20261102` — **Zrobione: wspólne wydanie Test alpha.465 opublikowane, zainstalowane i uruchomione 02.11.2026 po 02:00**. [Wyniki](REL-20261102-nocna-aktualizacja-test.md).

- `TITLE-001` — **W trakcie; kod i 17 testów zaliczonych, bez instalacji** — stała data kompilacji w tytule AMC Test; odtwarzanie czytane z NVDA+End. [TITLE-001-data-w-tytule-AMC-Test.md](TITLE-001-data-w-tytule-AMC-Test.md).

- `A11Y-20261030` — **W trakcie; źródła, testy i kompilacja bez instalacji** — `NVDA+End` ma odczytywać aktualny pasek stanu na liście i w odtwarzaczu różnych sesji. Ręczna próba NVDA pozostaje do wykonania. [A11Y-20261030-nvda-end-pasek-stanu.md](A11Y-20261030-nvda-end-pasek-stanu.md).
- `A11Y-20261110` — **W trakcie; źródła i 15 testów dodatku oraz 35 testów statusu zaliczone, paczka NVDA 0.4.1 zbudowana; instalacja i żywy odsłuch czekają** — NVDA+End mówi „Lista” zamiast bieżącego materiału i parametrów w TIDAL, Spotify oraz prawdopodobnie Podcastach. [Wynik](A11Y-20261110-nvda-end-lista.md).

- `PERF-001` — **W trakcie; poprawka #6 opublikowana w AMC Public alpha.448; bez lokalnej instalacji i ręcznej próby NVDA** — pominięcie zbędnego przeliczania dużych list; osobny projekt dla przełączania widoku i Escape z odtwarzacza ze zgłoszenia #5. Szczegóły: [PERF-001-duze-listy-hubert.md](PERF-001-duze-listy-hubert.md).

- `RARCH-001` — **W trakcie; pierwsza wersja w AMC Test odebrana z uwagami,
  poprawki pobierania, Spacji, Shift+A i diagnostyki w źródłach po testach i kompilacji;
  czeka następna instalacja i ręczny odbiór NVDA** —
  sesja „Radio Archiwista” wyłącznie w profilu Test: przeglądanie
  publicznego katalogu nagrań (stacja → rok → miesiąc → dzień → godzina,
  „Najnowsze nagrania”), odtwarzanie wspólnym torem podcastów (tempo,
  wznowienie pozycji), pobieranie MP3 `Ctrl+D`, ulubione `Ctrl+U`/
  `Ctrl+Shift+U`, odświeżanie `F5`, globalne wyszukiwanie bez sieci.
  Bez sterowania nagrywaniem. Szczegóły:
  [RARCH-001-integracja-radioarchiwista.md](RARCH-001-integracja-radioarchiwista.md).
- [UNDO-001 — przywracanie pozycji kolekcji](UNDO-001-przywracanie-pozycji-kolekcji.md): przegląd Ctrl+Z, trwałych porządków Ulubionych i kolejek oraz kontrakt cofania.
- [RARCH-002 — pobieranie wycinka i diagnostyka awarii](RARCH-002-pobieranie-fragmentow.md): Ctrl+Shift+D w Radio Archiwiście, postęp dwóch etapów, analiza zrzutów wx, presety.
- `SPOTIFY-ACCESS-001` — **Zrobione jako pakiet do recenzji, bez wysyłki** — uczciwe wstępne zapytanie o publiczny dostęp Spotify Web API dla pełnej sesji AMC, polskie streszczenie, potwierdzone fakty i zakresy, lista danych właściciela, dowodów oraz plan późniejszego formalnego wniosku. Aktualny kanał kontaktu i status kwotowy Client ID wymagają potwierdzenia; nie kontaktowano się ze Spotify. [SPOTIFY-ACCESS-001-zapytanie-o-publiczne-web-api.md](SPOTIFY-ACCESS-001-zapytanie-o-publiczne-web-api.md).

- `SPOTIFY-OUTPUT-001` — **Zrobione jako badanie; brak potwierdzonego, wspieranego API do automatycznego wyboru fizycznego wyjścia oficjalnego Spotify** — `Shift+A` w AMC Test nadal rzeczywiście wybiera wyjście hosta Librespot (12 testów bez GUI zaliczonych). Oficjalna aplikacja może mieć wyjście ustawione ręcznie w Mikserze Windows; to nie jest wdrożenie `Shift+A`. [SPOTIFY-OUTPUT-001-wyjscie-oficjalnego-odtwarzacza.md](SPOTIFY-OUTPUT-001-wyjscie-oficjalnego-odtwarzacza.md).
- `SPOTIFY-ISSUES-009-011` — **Poprawki źródeł i testy bez interfejsu, bez instalacji** — dodano konfigurację i parowanie z #9, naprawiono pobranie i zapis kolekcji z #10 oraz komunikat Librespot z #11. Nowe okno wymaga próby NVDA i odbioru na koncie. [SPOTIFY-ISSUES-009-011-20261030.md](SPOTIFY-ISSUES-009-011-20261030.md).
- `SPOTIFY-LIBRESPOT-20261108` — **Wycofanie hosta z nowych paczek: kod i 61 testów zaliczone; bez instalacji i publikacji** — oba profile przestają kopiować stary host, a przyszły instalator usunie go z katalogu programu przy aktualizacji. [Szczegóły](SPOTIFY-LIBRESPOT-20261108-wycofanie-z-pakietu.md).

- `MIRROR-001` — **Shift+M wydany w AMC Test alpha.466; 222 testy celowane zaliczone, 1 pominięty, odbiór NVDA i pomiar opóźnienia otwarte**. Jeden dodatkowy odbiornik Google Cast; AirPlay i drugie lokalne wyjście pozostają osobnymi etapami. [Opis i wyniki](MIRROR-001-dublowanie-dzwieku-cast-airplay.md).
- `SESSION-003` — **poprawka Enter i natywnych presetów WiiM wydana w AMC Test alpha.466; ręczny odbiór NVDA otwarty**. [Opis i wyniki](SESSION-003-wiim-enter-i-presety.md).

- `CAST-001` — **Podstawowy zakres wydany i zainstalowany w Test alpha.465; lista odebrana z NVDA**. Shift+C wybiera Cast; Shift+A pozostaje torem sesji. [Zakres i ograniczenia](CAST-001-google-cast.md).

- `DOWNLOADS-001` — **AMC Test `alpha.455` opublikowany na GitHubie i
  zainstalowany; 2211 zaliczonych testów, pozostaje ręczny odbiór NVDA** —
  historia pobranych plików Radio Archiwisty, Podcastów i wideo pod `Ctrl+J`.
  Szczegóły: [DOWNLOADS-001-historia-pobierania.md](DOWNLOADS-001-historia-pobierania.md).

- `DOWNLOADS-002` — **AMC Test `alpha.456` opublikowany na GitHubie i
  zainstalowany; 2215 testów zaliczonych, odbiór NVDA oczekuje** — w Historii
  pobrań `Ctrl+C` kopiuje ścieżkę, a
  `Ctrl+Shift+C` plik do wklejenia. Szczegóły:
  [DOWNLOADS-002-schowek-historii-pobran.md](DOWNLOADS-002-schowek-historii-pobran.md).

- `RARCH-002` — **AMC Test `alpha.457` opublikowany i zainstalowany; 2220
  testów regresji zaliczonych, odbiór NVDA oczekuje** — wybór zakresu
  `MM:SS` i zapis fragmentu MP3 przez `Ctrl+Shift+D`; szczegóły i wyniki
  w [RARCH-002-pobieranie-fragmentow.md](RARCH-002-pobieranie-fragmentow.md).

- `TEST-20261031-SPOTIFY` — **zbiorcza aktualizacja AMC Test `alpha.458`
  w przygotowaniu** — gotowe poprawki konta i katalogu Spotify #9–#11
  połączone z podstawą `alpha.457`; pozostałe sprawdzone nowości były już
  w tej podstawie. [Wyniki i ograniczenia](TEST-20261031-spotify-alpha458.md).


- `EDIT-001` — **Zrobione w źródłach; 3 testy kontraktu, celowany test
  rzeczywistego wycinania oraz kompilacja LiteHosta są zaliczone; poprawka nie
  została jeszcze zainstalowana ani wydana** —
  wycinanie z odtwarzanego pliku czeka na faktyczne zwolnienie dekodera i
  blokuje ponowne otwarcie lokalnego pliku do końca edycji. Szczegóły:
  [EDIT-001-zwalnianie-pliku-przed-wycinaniem.md](EDIT-001-zwalnianie-pliku-przed-wycinaniem.md).

- `POD-004` — **Wydane w AMC Test `0.1.0-alpha.436`; poprawa polskiego
  wyszukiwania SoundCloud i parytet globalnego `Ctrl+Shift+F` są zapisane w
  commitach `11ba6271` i `a8263cc0`, lecz nie ma jeszcze nowej instalacji ani
  publikacji** —
  wyszukiwanie podcastów SoundCloud przez `Ctrl+F` i globalne `Ctrl+Shift+F`,
  lokalna subskrypcja przez `Ctrl+Shift+L`, adres profilu lub RSS w `Ctrl+N`
  oraz zwykłe pliki audio do odtwarzania i pobierania. Korekta zastępuje
  wcześniejszą webową integrację Mixcloud.
  Szczegóły:
  [POD-004-soundcloud-rss-wyszukiwanie-i-subskrypcje.md](POD-004-soundcloud-rss-wyszukiwanie-i-subskrypcje.md).

- `YT-005` — **Zaakceptowane przez użytkownika i wydane w AMC Test
  `0.1.0-alpha.436`; kod, testy, paczka, instalator, GitHub oraz ręczny odbiór
  głównych funkcji są zweryfikowane** — ponowne `Ctrl+I` w otwartej skrzynce
  doładowuje następne 150 pozycji, a kolekcje YouTube żądają polskich tytułów
  przy zachowaniu dat przybliżonych. Szczegóły:
  [YT-005-skrzynka-ctrl-i-i-tytuly-youtube.md](YT-005-skrzynka-ctrl-i-i-tytuly-youtube.md).

- `POD-20261105` — **Zainstalowany kandydat alpha.478; oczekuje na odbiór NVDA** — lekki odczyt skrzynki
  `Ctrl+I`, stabilne Enter/Escape i powrót do poprzedniej sesji; audyt czasu
  otwarcia pozostałych zbiorczych widoków. Szczegóły:
  [POD-20261105-wydajnosc-skrzynki-i-nawigacji.md](POD-20261105-wydajnosc-skrzynki-i-nawigacji.md).

- `POD-20261106` — **W trakcie; kod i testy bez okien gotowe, bez instalacji i odbioru NVDA** — wybór źródeł skrzynki `Ctrl+I` i kanałów transmisji na żywo obok siebie w menu Ustawienia; pozostałe źródła można sprawdzać ręcznie przez `F5`. [Szczegóły](POD-20261106-wybor-zrodel-skrzynki.md).

- `PLAY-20261105` — **Kod i testy gotowe; kandydat alpha.479** — naturalna
  kontynuacja albumu lub listy w lokalnych plikach, Spotify i wewnętrznym TIDAL.
  Szczegóły: [PLAY-20261105-kontynuacja-albumu.md](PLAY-20261105-kontynuacja-albumu.md).

- `KEY-001` — **Zrobione; kod, pełna regresja, czysta paczka, instalator,
  wydanie GitHub i rzeczywista aktualizacja instalacji są zweryfikowane, a
  użytkownik zatwierdził obsługę klawiatury i skróty 28.10.2026** — osobna
  karta **Klawiatura** z filtrem poleceń, podglądem zmiany przed zapisem,
  wykrywaniem konfliktów, usuwaniem i przywracaniem skrótów oraz jednym
  kontraktem menu, palety i pomocy. Szczegóły:
  [KEY-001-konfigurowalne-skroty-klawiaturowe.md](KEY-001-konfigurowalne-skroty-klawiaturowe.md).

- PODRAD-001 — **W trakcie odbioru NVDA; Test alpha.448 opublikowany i zainstalowany, Public alpha.448 opublikowany na GitHubie**. Dwa niezależne, domyślnie wyłączone przełączniki osobnych folderów: [PODRAD-001-osobne-foldery-stacji-i-podcastow.md](PODRAD-001-osobne-foldery-stacji-i-podcastow.md).
- TEST-20261030 — **Wydano i zainstalowano AMC Test 0.1.0-alpha.448; ręczny odbiór NVDA oczekuje**. Wyniki: [TEST-20261030-amc-test-alpha448.md](TEST-20261030-amc-test-alpha448.md). [Szkic instrukcji i listy zmian](TEST-20261030-instrukcja-i-lista-zmian-szkic.md).
- PUBLIC-20261030 — **Opublikowano AMC Public 0.1.0-alpha.448 na GitHubie; bez lokalnej instalacji i ręcznego odbioru NVDA**. Wyniki: [PUBLIC-20261030-wydanie-alpha448.md](PUBLIC-20261030-wydanie-alpha448.md).

- `SET-001` — **W trakcie; kod priorytetów 1–3, pełna regresja, czysta paczka,
  instalator, wydanie GitHub i rzeczywista aktualizacja instalacji są
  zweryfikowane; robocza korekta kolejności kart, sekcji i końcowego edytora
  własnych komunikatów oraz automatyczne powiadamianie o nowej wersji zostały
  wydane w Public i AMC Test `0.1.0-alpha.445`; lokalnej instalacji nowej
  wersji nie wykonano, a ręczny odbiór NVDA pola pozostaje** —
  naprawa osiągalności pól domyślnych folderów przez Tab, przegląd nazw
  kontrolek, dostępna karta integracji z Windows, nowa karta Aktualizacje oraz
  karta Dostępność i komunikaty. Szczegóły:
  [SET-001-audyt-ustawien-dostepnosc-integracja-windows.md](SET-001-audyt-ustawien-dostepnosc-integracja-windows.md).

- `I18N-001` — **Poprawka po odbiorze wydana w AMC Test `0.1.0-alpha.444`;
  wspólna warstwa językowa, polski, angielski, wybór zgodny z systemem,
  osobne szablony komunikatów, pełny katalog 713 statycznych napisów i 312
  komunikatów dynamicznych, parytet menu/pomocy/palety, natychmiastowa zmiana
  języka i zgodna z językiem nazwa listy są zweryfikowane. Wyjątek kontrolki
  tekstowej, który po zapisaniu języka przerywał przebudowę menu, jest
  naprawiony; 31 testów i dwie sondy wxPython są zaliczone. Paczka, instalator
  i wydanie GitHub są zweryfikowane; lokalnej instalacji nie wykonano** —
  przygotowanie interfejsu wxPython do obsługi wielu języków bez korzystania
  z dawnej aplikacji WPF. Szczegóły:
  [I18N-001-wielojezyczny-interfejs-wxpython.md](I18N-001-wielojezyczny-interfejs-wxpython.md).

- `REL-002` — **W trakcie; najnowszy Test `0.1.0-alpha.500` z 13.11.2026,
  19:52 jest opublikowany i zweryfikowany na GitHubie, lecz lokalna instalacja
  czeka na ręczną aktualizację użytkownika. Stabilny Public z 29.10.2026,
  19:24 jest opublikowany i odczytowo zweryfikowany. Public powstał
  z najnowszej bazy Test i ma WiiM oraz Frontier Smart, bez Sonosa, Spotify i
  TIDAL-a; 231/231 celowanych testów Pythona, 38/38 Launchera/aktualizatora,
  paczka `89473955`, cichy start, instalator, skan sekretów i zdalny SHA-256 są
  zaliczone. Najnowsze stabilne wydanie Public z 29.10.2026, 19:24
  automatycznie wykrywa nowe wydanie, informuje i pyta przed pobraniem;
  119 testów, paczka, instalator, bramka i publikacja GitHub są zaliczone;
  Hubert potwierdził działanie Public i zamykanie aplikacji. Test zachowuje
  TIDAL, Spotify i eksperymentalny, nadal niedziałający Sonos; 205 testów
  Pythona, 3 testy M3U/ICY, 38 testów aktualizatora, paczka, instalator i
  publikacja prerelease są zaliczone. Test alpha.498 zachowuje sesję Apple
  Music tylko do odczytu, naprawia jej `Ctrl+Shift+C` i `Ctrl+F`, stabilizuje
  fokus oraz mowę po zmianach kolekcji, udostępnia głośność Spotify, skraca
  komunikat głośności Sonosa i przenosi pobieranie Radio Archiwisty w tło;
  kandydat, instalacja na D, start oraz wydanie `test-2026.11.13.0217` są
  zweryfikowane** — jedna
  kanoniczna linia AMC Python,
  wydania AMC Test i AMC Public z tego samego commitu, dwa kanały aktualizacji,
  standardowa instalacja Windows oraz kontrolowane uporządkowanie starego C#,
  gałęzi lokalnych i GitHuba. Szczegóły:
  [REL-002-kanoniczne-wydania-test-public-github.md](REL-002-kanoniczne-wydania-test-public-github.md).

- `REL-003` — **W trakcie; instalacja WPF, stare instalatory, artefakty
  kompilacji, trzy worktree alpha 425, dwa stare kandydaty Frontier, 26
  zbędnych gałęzi lokalnych i dziesięć powiązanych worktree usunięte; dwie
  gałęzie robocze pozostały, a końcowy snapshot źródeł WPF zarchiwizowano na
  GitHubie bez zmiany `main`** — potwierdza
  niezależne od logowania Google odświeżanie już zapisanych kanałów YouTube i
  dokumentuje bezpieczny zakres dalszego porządkowania starego repozytorium.
  Szczegóły:
  [REL-003-usuniecie-wpf-i-stan-youtube.md](REL-003-usuniecie-wpf-i-stan-youtube.md).

- `RECOG-002` — **W trakcie; poprawki rozpoznawania są w zainstalowanym AMC Test 4.11.2026, 12:40; kod playlist TIDAL czeka na osobny odbiór** — poprawia kopiowanie linku z okna działań prawej strzałki, zapis całego otwartego albumu do Biblioteki Spotify lub TIDAL i powrót do listy usługi. Kod dodawania albumu do playlist konta TIDAL jest zapisany; jego odbiór użytkownik wydzielił 4.11.2026 do osobnego etapu. Szczegóły: [RECOG-002-album-z-historii-do-uslug.md](RECOG-002-album-z-historii-do-uslug.md).

- `ECOSYSTEM-001` — **Zrobione: porównanie źródeł, bez zmian w programie** — obserwacje z Blindradio o rozpoznawaniu, historii i komunikatach NVDA; jedna sugestia na przyszłe zadanie. [ECOSYSTEM-001-Blindradio-rozpoznawanie.md](ECOSYSTEM-001-Blindradio-rozpoznawanie.md).

- `RECOG-001` — **W trakcie; poprawki Test są w opublikowanym `alpha.438`, a
  korekta Public usuwająca wewnętrzne działania Spotify/TIDAL jest w
  stabilnym `alpha.440`. Poprawka odświeżania otwartej historii po nowym
  rozpoznaniu jest zainstalowana w AMC Test 30.10.2026. Korekta listy,
  filtra stacji i komunikatu kopiowania z 31.10 jest w źródłach; pozostaje
  ręczny odbiór NVDA. Korekta z 09.11 ustawia nazwany utwór usługi domyślnej
  przed drugą usługą i kopią linku Apple Music; również jest w źródłach** — prawa strzałka
  od razu fokusuje pierwszy element, obie listy potwierdzają kopiowanie przez
  lokalny region aktywny, a historia jest dostępna z listy i odtwarzacza
  Plików lokalnych oraz Radia, lecz nie z pozostałych sesji. Pełne podmenu
  zachowuje `Usługi, łącza i powiązania`. `Escape` zachowuje sesję, widok,
  zaznaczony element i wraca fokusem do miejsca wywołania.
  Szczegóły:
  [RECOG-001-interakcje-z-rozpoznanymi-utworami.md](RECOG-001-interakcje-z-rozpoznanymi-utworami.md).

- `RADIO-001` — **Kandydat AMC Public zbudowany i zweryfikowany lokalnie;
  instalator oczekuje na odbiór Huberta, bez instalacji i publikacji** — listy
  M3U z samymi adresami uzupełniają nazwy bez
  nadpisywania nazw użytkownika, a kompatybilne radio przekazuje bieżący tytuł
  do `NVDA+T`. Odrzucona pierwsza paczka ujawniła niepodpisane cache Pythona;
  końcowy builder i instalator blokują pliki spoza manifestu. Po odbiorze ten
  sam commit ma trafić do AMC Test. Szczegóły:
  [RADIO-001-import-m3u-icy-i-tytuly.md](RADIO-001-import-m3u-icy-i-tytuly.md).

- `REC-002` — **Zrobione i zainstalowane w AMC Test 26.10.2026, 13:32** —
  prawa strzałka w widoku `Nagrywane` i harmonogramach podaje format, nazwę
  pliku oraz folder bez powtarzania stacji i adresu. Szczegóły:
  [REC-002-prawa-strzalka-nagrywane-pliki.md](REC-002-prawa-strzalka-nagrywane-pliki.md).

- `SOCIAL-001` — **Wydane próbnie w AMC Test `0.1.0-alpha.444`,
  bez instalacji i odbioru NVDA** — pojedyncze publiczne filmy Facebooka i
  Instagrama oraz ręcznie odświeżane strony Facebooka; odtwarzanie dźwięku,
  pobieranie MP3 i wideo.
  Odczyt stron korzysta z nieudokumentowanego mechanizmu Meta i może być
  niepełny. Szczegóły:
  [SOCIAL-001-facebook-instagram-wideo.md](SOCIAL-001-facebook-instagram-wideo.md).

- `YT-004` — **W trakcie; kod, pełna regresja, build i instalacja AMC Test są
  gotowe; pozostaje ręczny odbiór prawdziwego pobrania i NVDA** — pobieranie
  filmów YouTube pod `Ctrl+Shift+D`, pojedyncze `Zapisz jako` pod `Ctrl+S` oraz
  domyślny wybór MP4 albo najlepszego oryginalnego formatu. Szczegóły:
  [YT-004-pobieranie-wideo-youtube.md](YT-004-pobieranie-wideo-youtube.md).

- `PLAY-001` — **W trakcie; kod, pełna regresja 2010/2010, instalacja i
  uruchomienie AMC Test 23.10.2026, 11:24 są gotowe; czeka na ręczny odbiór
  NVDA** — trwałe
  `Odtwarzaj losowo (R)` w odtwarzaczach Plików, Podcastów i Spotify, również
  w menu kontekstowym, palecie i pomocy; jawne kolejki zachowują własny porządek.
  Szczegóły:
  [PLAY-001-odtwarzanie-losowe-r.md](PLAY-001-odtwarzanie-losowe-r.md).

- `STAB-001` — **Zrobione; zniknięcie i powrót okna z 23.10.2026 były
  kontrolowaną promocją AMC Test, nie awarią Pythona** — korelacja manifestu,
  procesów, logu hosta i dzienników Windows oraz izolowane testy operacji
  asynchronicznych, launchera i kolejki hosta. Nie zmieniono źródeł ani
  instalacji. Szczegóły:
  [STAB-001-stabilnosc-okna-po-operacjach-sieciowych.md](STAB-001-stabilnosc-okna-po-operacjach-sieciowych.md).

- `SESSION-001` — **W trakcie; korekta po odbiorze NVDA ma 76/76 testów
  celowanych, pełną regresję Pythona 1983/1983 i została zainstalowana w
  zbiorczym AMC Test; migracja starej prędkości i kopii z 29.10 ma dodatkowo
  90 zaliczonych testów celowanych, lecz nie została jeszcze spakowana,
  zainstalowana ani wydana; pozostaje ponowny odbiór NVDA** —
  pełne, wykonywalne opcje sesji z historycznego AMC oraz nowa domyślna
  prędkość osobno dla Plików i Podcastów. Szczegóły:
  [SESSION-001-opcje-sesji-i-predkosc.md](SESSION-001-opcje-sesji-i-predkosc.md).

- `SESSION-002` — **Zrobione i opublikowane w AMC Test `0.1.0-alpha.441`;
  ustawienie pozostawania na liście
  działa per sesja także dla plików lokalnych i pozostałych wykonywalnych
  odtwarzaczy, a Menedżer ma osobny przycisk „Ustawienia sesji…”; testy
  celowane, paczka, instalator i GitHub są zaliczone, bez instalacji i ręcznego
  odbioru NVDA** —
  rozdzielenie ustawień od konfiguracji folderów, kont i urządzeń oraz wspólna
  polityka Entera, kolejek, zakładek i wyników katalogu. Szczegóły:
  [SESSION-002-ustawienia-sesji-i-zachowanie-odtwarzacza.md](SESSION-002-ustawienia-sesji-i-zachowanie-odtwarzacza.md).

- `SPOT-001` — **Zrobione w zakresie prędkości; użytkownik potwierdził
  działanie przyspieszania. Kod, natywny DSP, komunikat przygotowania, testy,
  build i bieżąca instalacja AMC Test są zaliczone** —
  prawdziwa prędkość `0,5–2,0x` w wewnętrznym odtwarzaczu Spotify, wspólne
  skróty `Shift+,`, `Shift+.` i `Ctrl+.`, menu, pomoc, paleta oraz dostępne
  etykiety wartości. Szczegóły:
  [SPOT-001-predkosc-odtwarzania.md](SPOT-001-predkosc-odtwarzania.md).
  Bieżąca instalacja:
  [AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md](AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md).

Roboczy indeks materiału z nocnych etapów do przyszłej instrukcji i opisu
zmian, wymagający korekty użytkownika:
[INDEKS-MATERIALU-INSTRUKCJA-I-ZMIANY-2026-10-19.md](INDEKS-MATERIALU-INSTRUKCJA-I-ZMIANY-2026-10-19.md).

- `BACKUP-001` — **Wdrożone i zweryfikowane w źródłach; czeka na instalację
  kandydata oraz ręczny odbiór NVDA** — bezpieczna pełna kopia bieżącego
  profilu oraz osobny eksport i import ustawień, Biblioteki, playlist AMC,
  Radia, harmonogramów, Podcastów i YouTube oraz urządzeń i usług. Szczegóły:
  [BACKUP-001-eksport-import-danych.md](BACKUP-001-eksport-import-danych.md).

- `REC-001` — **Zrobione; korekta zachowania istniejącego pliku po zerwaniu
  strumienia jest zainstalowana w AMC Test** — trwałość wykonanego
  harmonogramu nagrywania, zgodność listy `Alt+R` z blokadą zamknięcia oraz
  zachowanie niepustego pliku domkniętego przed awarią odbioru. Szczegóły:
  [REC-001-harmonogram-alt-r-zamykanie.md](REC-001-harmonogram-alt-r-zamykanie.md).
- `AMC02` — **Publiczny `alpha.431` zainstalowany i uruchomiony do odbioru
  NVDA; AMC Test czeka na bezpieczne zamknięcie** — `Alt+R`
  i `Alt+Shift+R` przy jednym wpisie podają widok, pozycję oraz nazwę pliku.
  `Ctrl+H` zapisuje od teraz potwierdzony start Radia, pliku i podcastu, a
  pojedynczy wpis Historii jest nazwany niezależnie od zdarzenia fokusu;
  wcześniejszych niezapisanych odtworzeń nie da się odtworzyć wstecz.
  Zachowano osobną pamięć zaznaczenia oraz bitrate, wielkość i folder pod lewą
  strzałką także w `Ctrl+H`. Bieżący zapis realizacji:
  [AMC02-listy-nagrywania-i-historii.md](AMC02-listy-nagrywania-i-historii.md).
  Zatwierdzona analiza historyczna:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC02_ANALIZA_2026-10-18.md`
  oraz `AMC02_REALIZACJA_ETAP_A_2026-10-18.md`. Właściwe wdrożenie wykonujemy
  w bieżącym AMC Test, a stary kod służy tylko jako wzorzec.
- `SCHED-001` — **Druga korekta po ręcznym odbiorze zainstalowana w AMC Test;
  czeka na ponowny odbiór NVDA** — dostępna przebudowa listy i edytora
  harmonogramów: informacje pod strzałkami, segmentowa data i czas oraz
  jednoznaczne wpisywanie czasu trwania. Szczegóły:
  [SCHED-001-edytor-i-klawisze-harmonogramow.md](SCHED-001-edytor-i-klawisze-harmonogramow.md).
- `NVDA-001` — **W trakcie; użytkownik potwierdził bardzo szybką reakcję
  `Ctrl+C` w AMC, pozostaje kontrola zwykłej aplikacji** — usunięcie opóźnienia `Ctrl+C` powodowanego przez
  NVDAExtensionGlobalPlugin bez zmiany zwykłego schowka w innych programach.
  Szczegóły:
  [NVDA-001-opoznienie-ctrl-c.md](NVDA-001-opoznienie-ctrl-c.md).
- `SEARCH-001` — **Wspólny kontrakt wyszukiwania dla wszystkich sesji oraz
  pustego profilu jest zainstalowany w obu profilach po pełnej regresji 1910
  testów; czeka na ręczny odbiór NVDA** — wyszukiwanie Plików lokalnych,
  YouTube, Radia,
  Spotify, TIDAL, WiiM i Sonosa:
  `Ctrl+Shift+L` pozostaje w oknie także dla zapisanego wyniku, wykonuje jedną
  właściwą operację i nie oddaje mowy głównemu oknu. `Ctrl+L` zamyka modal i
  przechodzi wspólnym poleceniem do Biblioteki. Szczegóły:
  [SEARCH-001-youtube-biblioteka-komunikaty.md](SEARCH-001-youtube-biblioteka-komunikaty.md).
- `POD-001` — **Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór
  NVDA** — trzy tryby sortowania
  zbiorczej Biblioteki podcastów, kanałów YouTube i „Media internetowe”,
  prywatna trwałość wyboru i zachowanie zaznaczenia po stabilnym ID. Szczegóły:
  [POD-001-sortowanie-biblioteki-zrodel.md](POD-001-sortowanie-biblioteki-zrodel.md).
- `POD-002` — **Zrobione i przyjęte przez użytkownika 23.10.2026 w AMC Test;
  odbiór obejmuje `Ctrl+N` w sesji `Podcasty i YouTube`. Poprawki szybkiego
  `Ctrl+V`, fokusu, blokady drugiego Entera i ukrywania procesów są scalone
  oraz znajdują się również w bieżącej instalacji 21:25** — formularz
  `Ctrl+N` pozostaje otwarty przez oczekiwanie, procesy pomocnicze nie tworzą
  okna, a zwykła mowa nie ujawnia „Windows 32”, terminala, ścieżek ani nazw
  technicznych. AMC Public, instalator i GitHub bez zmian. Szczegóły:
  [POD-002-import-youtube-ctrl-n.md](POD-002-import-youtube-ctrl-n.md).
- `POD-003` — **Zrobione, zainstalowane i uruchomione w AMC Test 23.10.2026,
  11:24; ręczny odbiór NVDA pozostaje** — naprawa niejawnego ucinania dużych kanałów
  RSS, uczciwy wynik pełny/częściowy/błędny oraz jawne stronicowanie `wczytano
  N z całości` z zachowaniem fokusu. Szczegóły:
  [POD-003-niezawodnosc-odswiezania-i-duze-kanaly.md](POD-003-niezawodnosc-odswiezania-i-duze-kanaly.md).
- `AMC04` — **Ręczny odbiór 23.10.2026 potwierdził, że odtwarzanie dużego
  podcastu Spotify działa, ale rozdziały w Spotify nadal nie działają.
  Obecny kod potrafi tworzyć rozdziały tylko ze znaczników czasu w opisie;
  nie należy przedstawiać natywnych rozdziałów Spotify jako wdrożonych.
  Zakładki i rozdziały zwykłych podcastów pozostają osobnym, wcześniej
  zaimplementowanym zakresem** — przeniesiony do głównego AMC
  Test/Python zatwierdzony model skrótów
  zakładek, nazwanych zakładek, rozdziałów, historii widoków i historii
  odtwarzanych elementów. Bieżący zapis realizacji:
  [AMC04-nawigacja-zakladki-rozdzialy-historia.md](AMC04-nawigacja-zakladki-rozdzialy-historia.md).
  Źródłowy audyt i rozpoczęta realizacja są zapisane w
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC03_AUDYT_SKROTOW_2026-10-18.md`
  oraz `AMC04_REALIZACJA_ETAP_A_2026-10-18.md`. Stare źródła WPF służą tylko
  jako wzorzec; właściwe wdrożenie wykonujemy w tym repozytorium.
- `MENU-001` — **W trakcie; naprawa `Shift+F10` harmonogramów i nazwa `Alt+D`
  oraz korekta pełnych ścieżek profilu hosta są zainstalowane w AMC Test i
  czekają na odbiór NVDA** —
  wspólne menu kontekstowe bez odczytu technicznych cyfr i przypadkowych
  liter dostępu, nazwy zależne od rodzaju elementu, rzeczywiste skróty oraz
  audyt dostępności poleceń i nawigacji między sesjami. Szczegóły:
  [MENU-001-menu-kontekstowe-i-skroty.md](MENU-001-menu-kontekstowe-i-skroty.md).
- `AMC05` — **Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór
  NVDA** — stan `Odtwarzane` lub `Wstrzymane` na właściwych listach bez utraty
  etykiety i bez przestawiania fokusa. Bieżący zapis realizacji:
  [AMC05-odtwarzany-element-na-listach.md](AMC05-odtwarzany-element-na-listach.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC05_ODTWARZANY_ELEMENT_NA_LISTACH_2026-10-18.md`.
- `AMC13` — **Rozszerzenie skoków procentowych zainstalowane w AMC Test;
  czeka na odbiór NVDA** — `End` w otwartym
  odtwarzaczu Time Shiftu wraca na żywo i do tempa normalnego, `NVDA+End`
  pozostaje poleceniem czytnika, a cyfry `0`–`9` przechodzą do 0–90%
  aktualnego bufora. Bieżący zapis realizacji:
  [AMC13-timeshift-end-na-zywo.md](AMC13-timeshift-end-na-zywo.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC13_TIMESHIFT_RADIA_2026-10-18.md`.
  Eksport fragmentów Time Shiftu pozostaje późniejszym rozszerzeniem.
- `TSH-001` — **Kod i testy gotowe; wspólna aktualizacja z TIDAL w przygotowaniu** —
  wybór folderu bufora Time Shift w ustawieniach Radia, z domyślnym
  katalogiem tymczasowym Windows. Wynik i ograniczenia:
  [TSH-001-folder-bufora-timeshift.md](TSH-001-folder-bufora-timeshift.md).
- `AMC08` — **Po odbiorze instalacji 11:24 znaleziono i naprawiono rzeczywistą
  przyczynę pustego Tyflo Podcast: 10 rekordów `null` w 2873 elementach
  przerywało parser. Odcinki programu są teraz ładowane narastająco po 150;
  prawdziwy odczyt zwrócił pierwszą porcję w 3,38 s. Pełna regresja, budowa i
  instalacja AMC Test 12:56 są zaliczone. Prawdziwe OAuth i dodawanie do playlist zostały potwierdzone
  przez użytkownika; korekta mowy `Ctrl+F5` i widoczności pod `Ctrl+P`
  playlist istniejących oraz nowych jest zainstalowana w AMC Test; również
  czyszczenie migawki po ponownym logowaniu jest zbudowane i zainstalowane,
  całość czeka na ręczny odbiór NVDA; korekta z 23.10.2026 naprawiająca
  jednorazowe pełne odświeżenie zapisanych podcastów i odcinków została
  zainstalowana i uruchomiona w AMC Test 23.10.2026, 11:24**
  — natywne playlisty
  Spotify w AMC Test: `Ctrl+P`, `Ctrl+Shift+P`, pobranie katalogu raz na
  uruchomienie, natychmiastowa widoczność nowej playlisty i zapamiętanie
  ostatnich celów. Ręczny test z odłączonym interfejsem Denon USB ujawnił, że
  wcześniejszy fallback następował za późno. Korekta z 23.10.2026 sprawdza
  nazwane wyjście przy konfiguracji sesji bez logowania, przełącza bieżący tor
  na urządzenie domyślne przed pierwszym Play i rozróżnia kod
  `trackUnavailable`; kod, testy, build Release oraz instalacja AMC Test 16:30
  są zaliczone, a ręczny odbiór dźwięku pozostaje do wykonania. Mechanizm
  ma partycje usług i kont, aby kolejne integracje
  używały tego samego kontraktu bez kopiowania kodu Spotify. Przebieg:
  `AMC08-playlisty-spotify.md`. Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC08_PLAYLISTY_SPOTIFY_2026-10-18.md`.
  Historyczne źródła są tylko wzorcem; kod powstaje w tym repozytorium.
- `AMC09` — **Menedżer, pierwsze rozszerzenie Konfiguruj, ujednolicone
  `Ctrl+F5`, precyzyjne nazwy dla każdej sesji oraz Foldery Biblioteki są
  zbudowane; po ręcznym wykryciu brakującego mapowania poprawiono całą rodzinę
  `F1`–`F24` i nawigację bloku numerycznego; najnowsza korekta jest
  zainstalowana i czeka na ponowny odbiór NVDA. Wydanie z 13.11.2026 dodaje
  dziesiątą sesję pod `Ctrl+0`, jawny wynik Spacji, F11 i Ctrl+F11; instalacja
  oraz prerelease GitHub są zweryfikowane** — dostępny
  Menedżer sesji pod `Ctrl+Shift+S`, włączanie Spacją, zwarta kolejność
  `Ctrl+1–9`, zmiana kolejności `Alt+strzałkami`, stan gotowości i stała nazwa
  `Radio`. Ma działać już w AMC Test; profil publiczny będzie później filtrował
  tę samą listę przez manifest możliwości. Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC09_MENEDZER_SESJI_I_PIERWSZE_LOGOWANIE_2026-10-18.md`.
  Automatyczne otwarcie na czystej instalacji pozostaje do odbioru AMC12 i nie
  może zmienić obecnego profilu testowego. W Plikach lokalnych `Ctrl+F5`
  otwiera teraz niedestrukcyjny menedżer dodawania, odświeżania i odłączania
  Folderów Biblioteki; pozostałe sesje zachowują własne znaczenie tego skrótu.
  Korekta z 20.10.2026 dodaje w Radiu rzeczywiste „Odśwież stacje Radia” pod
  `Ctrl+F5`, również z odtwarzacza, przy wspólnym opisie menu, pomocy i palety.
  Kod i testy kontraktu są potwierdzone. Ręczny odbiór ujawnił jednak wspólną
  przyczynę ciszy `F5` i `Ctrl+F5`: brak mapowania rzeczywistego kodu klawisza
  wxPython. Poprawka obejmuje już wszystkie klawisze funkcyjne, a nie tylko
  `F5`; testy zaczynają się od surowych zdarzeń klawiatury. Najnowsza wersja
  została zainstalowana 20.10.2026. Korekta z 21.10.2026 utrwala zasadę, że
  `Ctrl+F5` po skonfigurowaniu pokazuje stan i zapisane cele bez ponownego
  logowania lub skanowania. Audyt wykazał, że Spotify i TIDAL nadal wymagają
  domknięcia jawnego wylogowania oraz stanowych przycisków konta.
  Bieżący zapis realizacji:
  [AMC09-menedzer-sesji.md](AMC09-menedzer-sesji.md).
- `AMC12` — **Lokalny kandydat `alpha.433` przeszedł pełną regresję źródeł,
  został zbudowany i zainstalowany w `D:\Projekty Codex\AMC Public`, lecz
  decyzją użytkownika z 21.10.2026 jest wstrzymany do odbioru Smart Frontier.
  Nie uruchomiono go do odbioru, nie opublikowano i nie należy go jeszcze
  przekazywać. Dodaje ręczną kolejność Biblioteki i Ulubionych Radia:
  `Alt+strzałka góra/dół` oraz `Ctrl+X`, `Ctrl+V`, z nazwaniem stacji nad albo
  pod którą przeniesiono wybór.
  Publiczny AMC jest budowany z tego samego kodu co AMC Test; zgodnie z korektą
  użytkownika z 21.10.2026 wyłącza
  wyłącznie niedopracowane funkcje streamingowe, a wszystkie ukończone funkcje,
  w tym Sonosa, zachowuje. Obecnie TIDAL, Spotify i nieodebrana jeszcze
  integracja Frontier Smart pozostają tylko w AMC Test. Końcowa paczka
  `e6d4269d` ma 1075 plików; integralność i kontrolowany start paczki oraz
  integralność zainstalowanej kopii są zaliczone. Instalator usuwa także stare moduły niedostępnych usług
  pozostałe po wcześniejszej wersji. Najnowsza poprawka rozpoznaje ten sam plik
  w `Ctrl+O` i Bibliotece podczas `Ctrl+Shift+U`, nie przełącza folderu na pustą
  Bibliotekę oraz jasno wskazuje `Ctrl+F5`, gdy folder nie jest źródłem.
  Wcześniejsza poprawka zachowuje
  kolejność importowanej playlisty Radia, uruchamia `Alt+1–3` w Bibliotece i
  Ulubionych, wiąże `Ctrl+Shift+U` z Ulubionymi oraz `Ctrl+Shift+L` z
  członkostwem w Bibliotece. Ręczny odbiór w NVDA i decyzja o dołączeniu
  Frontier Smart pozostają do wykonania; niczego nie opublikowano** — jeden kod, jawny manifest możliwości, osobne
  katalogi, blokady, logi i kanały AMC Test oraz publicznego AMC. Bieżący zapis
  realizacji:
  [AMC12-profile-wydania-publicznego-i-testowego.md](AMC12-profile-wydania-publicznego-i-testowego.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC12_PROFILE_WYDANIA_PUBLICZNEGO_I_TESTOWEGO_2026-10-18.md`.
- `AMC10` — **W trakcie odbioru użytkownika; publiczny broker działa pod
  `https://sonos.stareaudycje.pl/`, callback jest zapisany w portalu Sonos, a
  paczka AMC Test `0423232e` z commita `8f42e319` została zbudowana, atomowo zainstalowana i
  uruchomiona z `D:\Projekty Codex\AMC Python`. Rozłączona sesja ma jeden
  wiersz logowania, Enter otwiera konto z jednym polem informacji, a
  `Rozpocznij logowanie` automatycznie otwiera oficjalną stronę i zamyka modal.
  Lokalny wariant AMC Test został odebrany na prawdziwym głośniku częściowo:
  wykrywa grupę oraz odtwarza Ulubione i Playlisty Sonos, ale 14.11 użytkownik
  potwierdził błąd `Moich stacji`, części transportu i wolniejszą listę.
  Następna korekta wyłącza odpytywanie UPnP na listach, pozwala spróbować
  wznowić materiał zatrzymany poza AMC i dodaje bezpośredni wariant URI po
  odrzuceniu radiowego. Testy celowane **56/56** są zaliczone; build, instalacja,
  publikacja i ponowny odbiór tej korekty pozostają otwarte. Zweryfikowane
  nocne wydanie `public-2026.11.14.0329` nadal jawnie wyklucza Sonosa.
  Ponowne otwarcie konta zachowuje w pamięci pierwotny adres tej samej próby i
  udostępnia „Otwórz stronę ponownie” oraz „Kopiuj adres logowania”.
  Po powrocie główne okno pozostaje dostępne, a jeden wiersz `Sprawdź logowanie
  do Sonos` prowadzi do następnego kroku. `Ctrl+Alt+Shift+R` zatrzymuje
  nagrania globalnie także z Sonosa i pozostałych sesji. Pierwsza wcześniejsza
  próba dotarła do oficjalnego wyboru
  gospodarstwa domowego, lecz Sonos zwrócił błąd przed callbackiem. Próba w
  oknie prywatnym wymaga powtórzenia z poprawnie skopiowanego adresu startowego.
  Pozostają ponowna próba na koncie przypisanym do systemu, udane logowanie, odmowa,
  odnowienie, sprzęt i NVDA. AMC Public pozostaje bez zmian** — działające
  sterowanie Sonosem i WiiM jest już przepisane i
  nie wymaga ponownej implementacji. `Ctrl+F5` wybiera dom i grupę, a przy braku
  połączenia prowadzi do konta zamiast kończyć się błędem. Okno udostępnia
  rozpoczęcie, jawne
  sprawdzenie, anulowanie i odświeżenie logowania bez przekazywania tokenów do
  Pythona. Dodano wcześniej serwerowy
  kontrakt logowania, PKCE, jednorazowe sesje, odnowienie, ograniczenie tempa,
  bezpieczne logowanie oraz ścisłą obsługę zakończenia TLS przez jawnie zaufane
  reverse proxy. AMC Public pozostaje bez zmian.
  Bieżący zapis realizacji:
  [AMC10-logowanie-sonos-i-publiczny-broker.md](AMC10-logowanie-sonos-i-publiczny-broker.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC10_SONOS_WIIM_URUCHOMIENIE_I_UJEDNOLICENIE_2026-10-18.md`.
  Bez obecności użytkownika nie logować konta i nie sterować głośnikami.
- `AMC14` — **W trakcie; folder zawierający, argument pliku, osobny kanał
  działającej instancji i idempotentne `/kolejka` są zainstalowane w AMC Test;
  opcjonalna i odwracalna rejestracja instalatora jest rozdzielona dla AMC
  oraz AMC Test, ma
  potwierdzoną składnię, lecz nie została zastosowana w systemie** —
  aplikacyjna integracja z powłoką Windows,
  przekazywanie plików do właściwej instancji i późniejsza rejestracja przez
  instalator. Bieżący zapis realizacji:
  [AMC14-foldery-i-integracja-powloki.md](AMC14-foldery-i-integracja-powloki.md).
  Zatwierdzona specyfikacja:
  `D:\Projekty Codex\Accessible Multimedia Controller\AMC14_FOLDERY_I_INTEGRACJA_POWLOKI_2026-10-18.md`.
- `YT-001` — **Wdrożone do ręcznego odbioru — logowanie, subskrypcje i opisy
  są odebrane; trwałe respektowanie usunięć, uczciwe ponawianie oraz obsługa
  kanałów bez standardowej karty Filmy są zbudowane, zainstalowane i działają
  na rzeczywistej bazie. Duplikatów kanałów ani filmów nie wykryto**
  — oficjalne logowanie do
  konta YouTube, synchronizacja subskrypcji i zwykłych playlist oraz bezpieczny
  zakres czynności konta. Etap rozpoczęto 21 października 2026 na nowe jawne
  polecenie użytkownika; sam start nadal nie loguje konta ani nie otwiera
  przeglądarki. Profil publiczny pozostaje wyłączony. Korekta z 27.10.2026:
  rzeczywiste ponowne logowanie przez zainstalowany LiteHost i systemową
  przeglądarkę zakończyło się wynikiem `Zalogowano do YouTube`, a kolejny odczyt
  potwierdził aktywne połączenie. Nie zmieniano Google Cloud, źródeł, instalacji
  ani GitHuba. Dalsza korekta z 27.10.2026: źródła profilu Public mają już
  osobny bezsekretowy tor przez broker tokenów, a projekty polityki
  prywatności, warunków i listy gotowości są zapisane, lecz nieopublikowane.
  Źródła Public cofają token w Google, usuwają oznaczone dane konta i
  automatycznie usuwają je po 30 dniach bez odświeżenia; publiczne kanały
  dodane ręcznie pozostają nienaruszone. Celowane testy, build i audyt
  zależności są zaliczone. Decyzja użytkownika z 27.10.2026 wyklucza wdrożenie
  na `stareaudycje.pl`: do czasu świadomego wyboru innej domeny moduł pozostaje
  przygotowany, ale AMC Public powstaje bez zasobów OAuth i bez martwych pozycji
  konta w interfejsie. Przed ewentualnym włączeniem pozostają: zgoda i dostęp do
  polityki w interfejsie, uzupełnienie danych administratora, własna
  zweryfikowana domena, wdrożenie brokera, konfiguracja produkcyjnego projektu
  Google oraz rzeczywisty odbiór zakresu `youtube.readonly` z NVDA.
  Szczegóły:
  [YT-001-konto-youtube-i-synchronizacja.md](YT-001-konto-youtube-i-synchronizacja.md).
  Bieżąca instalacja:
  [AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md](AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md).
- `YT-002` — **W trakcie — dotychczasowy zakres jednego kanału jest
  zainstalowany, a 24.10.2026 wdrożono źródła zbiorczej listy wybranych
  kanałów pod `Ctrl+Shift+I`; pełna regresja wxPython 2032/2032, build,
  integralność, atomowa instalacja i start AMC Test 24.10.2026, 13:33 są
  zaliczone. Rzeczywisty odbiór potwierdził listę i ujawnił nieczytelne
  oczekiwanie; commit `efb8c090` blokuje powtórne `Ctrl+Shift+I` i `F5`
  podczas trwającego skanu. Pełna regresja po poprawce: 2033 zaliczone,
  111 pominiętych i 13 podtestów; paczka `8af5940f` z commita `81a1e4c5`
  przeszła build, integralność i kontrolowany start, została atomowo
  zainstalowana i uruchomiona jako `AMC Test — 24.10.2026, 14:48`** — lista transmisji live/upcoming zapisanego kanału, Enter w
  wewnętrznym silniku, kopiowanie adresu, F5 i Backspace oraz dynamiczna stacja
  Radia oparta na stałym adresie kanału `/live`. Tego samego dnia rozpoczęto
  poprawkę zwykłej listy kanału: łączy ona kartę `Filmy` z publicznymi,
  zakończonymi transmisjami, bez osobnej historii i bez duplikatów. Test
  syntetyczny oraz rzeczywiste kanały Dominikanie Poznań, Wrocław, Freta i
  Szczecin są zaliczone; build źródeł i 52 celowane testy wxPython także są
  zaliczone. Użytkownik potwierdził również prawidłowe rozpoznanie i nagranie
  z harmonogramu transmisji Dominikanie Poznań i Teleexpress oraz przygotowanie
  następnych transmisji na 25.10.2026. Kod jest w commicie `921b784e`; nowa
  paczka AMC Test `5b3ac88c` została zbudowana, sprawdzona, atomowo
  zainstalowana i uruchomiona jako `AMC Test — 24.10.2026, 21:20`. Pełna
  integralność zainstalowanej kopii i kontrolowany start są zaliczone. AMC
  Public i GitHub pozostały bez zmian. Szczegóły:
  [YT-002-transmisje-na-zywo-i-dynamiczne-radio.md](YT-002-transmisje-na-zywo-i-dynamiczne-radio.md).
- `YT-003` — **Podstawowy zakres opisów jest zainstalowany i odebrany; korekta
  z 27.10.2026 pełnego automatycznego odczytu wszystkich akapitów ma commit
  `a4471493` i 88/88 samowystarczalnych testów, lecz czeka na następną paczkę,
  instalację i ręczny odsłuch NVDA** — `Alt+D` pokazuje opisy kanałów, playlist
  i materiałów YouTube, zapisuje nowe opisy z importu i synchronizacji oraz
  odczytuje na żądanie brakujący opis starszego publicznego wpisu bez logowania
  Google. Limit 500 znaków w automatycznej mowie został usunięty również dla
  RSS i Spotify; Radio nie skracało tej odpowiedzi i otrzymało test ochronny.
  GitHub pozostał bez zmian. Szczegóły:
  [YT-003-opisy-youtube-alt-d.md](YT-003-opisy-youtube-alt-d.md).
- `NAME-001` — **Kod, testy, paczka, instalacja i uruchomienie AMC Test gotowe;
  bez nowego instalatora AMC Public** —
  krótkie nazwy `AMC Test — data, godzina` i `AMC Public — data, godzina`,
  z technicznymi identyfikatorami pozostawionymi poza zwykłą nazwą.
  Szczegóły: [NAME-001-amc-test.md](NAME-001-amc-test.md).
- `FRONTIER-001` — **W trakcie — użytkownik potwierdził 25.10.2026 na bieżącym
  AMC Test, że `Ctrl+Alt+M` i wybór trybów znacznie przyspieszyły, presety
  działają, AMC widzi pełną listę 30 pozycji, a skróty obsługują pierwszych 12.
  Ustalono, że nie powstaną osobne skróty dla Spotify, USB, FM, DAB+ ani
  Bluetooth. Następny osobny etap ma rozważyć pod `Ctrl+L` dwie pozycje:
  `Tryby urządzenia` i `Presety urządzenia`, bez udawanych Ulubionych. Tego
  rozszerzenia jeszcze nie zaimplementowano** —
  `Ctrl+8`, `Ctrl+F5` i ręczny adres korzystają z jednego modelu stanów.
  AMC wymaga potwierdzonego opisu FSAPI/UNDOK i użytkowej nazwy urządzenia,
  zanim pokaże pole PIN. Brak wyniku i błąd nie prowadzą do PIN-u. Po zapisaniu
  urządzenia `Ctrl+F5` pokazuje listę bez ponownego skanowania. Funkcja nadal
  pozostaje wyłącznie w profilu AMC Test.
  Szczegóły:
  [FRONTIER-001-frontier-smart-fsapi.md](FRONTIER-001-frontier-smart-fsapi.md).
  Bieżąca instalacja:
  [AMC-TEST-2026-10-23-2241-frontier-pin-spotify-start.md](AMC-TEST-2026-10-23-2241-frontier-pin-spotify-start.md).
- `ARC-001` — **Oczekuje** — wspólny rdzeń poleceń dla GUI, palety, NVDA,
  przyszłego CLI i lokalnego sterowania AI. Szczegóły:
  [ARC-001-wspolny-rdzen-polecen-cli-ai.md](ARC-001-wspolny-rdzen-polecen-cli-ai.md).
- `MAC-001` — **Oczekuje, odłożone bez terminu** — ewentualna ocena portu AMC
  na macOS dopiero po wydaniu Windows i wyłącznie na nowe jawne polecenie.
  Szczegóły:
  [MAC-001-port-macos-wxpython-litehost.md](MAC-001-port-macos-wxpython-litehost.md).
- `LIC-001` — **Oczekuje** — Radio Browser, licencja BASS i bezpieczna decyzja
  o dobrowolnych wpłatach. Szczegóły:
  [LIC-001-radio-browser-bass-darowizny.md](LIC-001-radio-browser-bass-darowizny.md).
- `REL-001` — **W trakcie; testowy Worker ma sekret i aktywny endpoint w źródłach;
  zainstalowany AMC Test czeka na paczkę, próbne zgłoszenie i odbiór** — menu, F1 i
  paleta prowadzą do dostępnego formularza z pełnym
  podglądem, domyślnie wyłączonym logiem, redakcją danych oraz kopiowaniem lub
  atomowym zapisem w magazynie profilu. Po zweryfikowanym endpointcie ten sam
  formularz wysyła dokładnie widoczny raport w tle. Worker ma drugą redakcję,
  limit, idempotencję i rozdziela prywatną treść od publicznego issue; jest wdrożony
  testowo z sekretem, ale nie utworzył jeszcze
  próbnego zgłoszenia. Zależności
  Workera są przypięte, a skrypt wdrożenia wykonuje testy oraz dwa pakowania
  `--dry-run`. Jawny bootstrap przygotowuje migrację Durable Object bez sekretu,
  ustawienie tokenu i właściwe wdrożenie są osobnymi etapami, a skrypt nigdy sam
  nie rozpoczyna logowania. Sesja Cloudflare jest potwierdzona. Prywatne
  repozytorium intake i wymagane etykiety są już utworzone. Lokalna bramka
  publikacji instalatora sprawdza profil, rewizję, rozmiar, SHA-256 i wynik
  odczytany z GitHuba, ale bez jawnego `-Publish` niczego nie wysyła. Czysta
  publiczna linia źródeł powstaje jako lokalny snapshot identycznego drzewa,
  bez publikowania roboczej historii i bez przełączania bieżącej gałęzi.
  Szczegóły:
  [REL-001-github-aktualizacje-sygnalista.md](REL-001-github-aktualizacje-sygnalista.md).
- `TID-001` — **W trakcie; poprawka wznowienia po zmianie sesji jest w źródłach, test audio i odbiór NVDA oczekują; zainstalowany Test pozostaje bez zmiany** — sesja audio ma osobne logowanie i punkt powrotu do oryginalnego TIDALa. Pozostaje odbiór pełnego sterowania i NVDA oraz ustalenie źródła okresowego obciążenia hosta. Szczegóły: [TID-001-pelne-odtwarzanie-w-AMC-Test.md](TID-001-pelne-odtwarzanie-w-AMC-Test.md).
- `STR-001` — **W trakcie; 12.11 użytkownik uruchomił prywatny pilot bez konta
  deweloperskiego: apple-music-py dla Biblioteki oraz izolowane
  music.apple.com dla odtwarzania; logowanie i żywa Biblioteka są potwierdzone,
  DRM, odtwarzanie oraz NVDA jeszcze nietestowane**
  — Apple Music nie jest jeszcze sesją AMC Test ani AMC Public. Oficjalna droga
  z istniejącym brokerem tokenów nadal wymaga zespołu Apple Developer.
  Szczegóły:
  [STR-001-tidal-apple-music-laboratorium.md](STR-001-tidal-apple-music-laboratorium.md).
- `AM-001` — **W trakcie; po aktywacji subskrypcji żywe logowanie, DPAPI,
  cztery widoki Biblioteki, druga strona utworów i zawartość playlisty działają;
  stronicowanie ma 101/101 testów, ale nie jest jeszcze podłączone do GUI** —
  pierwszy etap nieoficjalnego katalogu i Biblioteki tylko w AMC Test.
  [Szczegóły](AM-001-nieoficjalne-konto-i-biblioteka.md).
- `AM-002` — **14.11: wycofanie ukrytego odtwarzacza w wydanym,
  zainstalowanym i uruchomionym AMC Test alpha.507; bez odbioru NVDA**
  — Enter na utworze wskazuje jawne otwarcie w oryginalnym Apple Music;
  Spacja steruje tylko bieżącym utworem w oryginalnej aplikacji.
  [Szczegóły](AM-002-osadzony-odtwarzacz-music-apple-com.md).
- `AM-003` — **Odczytowa sesja jest zainstalowana i opublikowana w AMC Test
  `0.1.0-alpha.497`; ręczny odbiór NVDA pozostaje otwarty** — `Ctrl+L` oznacza Bibliotekę,
  a `Ctrl+U` czteroczęściowe Ulubione Apple Music, nie tylko playlistę
  ulubionych utworów. Odtwarzanie i polecenia zapisu nadal oczekują na osobne
  potwierdzenie możliwości.
  [Szczegóły](AM-003-sesja-i-dostepny-interfejs.md).
- `AM-004` — **Oczekuje; bez zakupu konta przed wynikiem pilota** — oficjalny
  MusicKit, istniejący broker tokenów, właściciel zespołu Apple Developer i
  ewentualny profil publiczny.
  [Szczegóły](AM-004-oficjalny-musickit-i-konto-developer.md).
- `BLUOS-001` — **Oczekuje; opracowanie źródłowe jest gotowe, a pierwszy etap
  wymaga pomiaru na Playerze użytkownika** — osobna lokalna sesja BluOS do
  sterowania Playerami, grupami, kolejką, presetami i katalogiem udostępnionym
  przez urządzenie. Nie jest to bezpośrednie API Apple Music ani TIDAL-a.
  Szczegóły:
  [BLUOS-001-sesja-bluos.md](BLUOS-001-sesja-bluos.md).

## Krótkie polecenia użytkownika

- `RELEASE-20261114-PUBLIC-SONOS` — **Test i Public alpha.507 opublikowane, zainstalowane i sprawdzone; Test uruchomiony z D** — lokalny Sonos w Public oraz poprawki playlist, YouTube, kolejek i podcastów; Public bez Apple Music. [RELEASE-20261114-public-sonos-playlisty.md](RELEASE-20261114-public-sonos-playlisty.md).
- `PLAYLIST-BULK-20261114` — **w wydanym i zainstalowanym Test/Public alpha.507; odbiór NVDA otwarty** — wielokrotne zaznaczenie dla playlist w pięciu sesjach, potwierdzona liczba dodatków TIDAL oraz brak łącznego czasu dla playlist radia. [PLAYLIST-BULK-20261114-potwierdzenie-i-radio.md](PLAYLIST-BULK-20261114-potwierdzenie-i-radio.md).

Wystarczy powiedzieć na przykład: `Uruchom zadanie NVDA-001`. Agent ma wtedy
odczytać rejestr oraz właściwy plik i wykonać wyłącznie wskazane zadanie.

- `SPOTIFY-20261101` — **W trakcie; kod i testy bez instalacji i odsłuchu NVDA** — album `Ctrl+Enter`, prywatna kolejka `Ctrl+Q` / `Ctrl+Shift+Q`, ciszej przy Enter, otwarty odbiór `NVDA+End` i prawej strzałki w rozpoznanych utworach. [SPOTIFY-20261101-album-kolejka-komunikaty.md](SPOTIFY-20261101-album-kolejka-komunikaty.md).

- `UPDATE-20261109` — **Źródła i testy celowane gotowe, bez instalatora i instalacji** — zgłoszenie Huberta #14 (kod 5 po starcie AMC podczas cichej instalacji), blokada startu, pełna kontrola plików oraz `Ctrl+Alt+Z` dla Sygnalisty w Test/Public. [UPDATE-20261109-hubert-kod5-i-sygnalista.md](UPDATE-20261109-hubert-kod5-i-sygnalista.md).
