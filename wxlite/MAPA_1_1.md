# AMC Python — zakres względem pełnego AMC

Stan kodu po192aa07 i rzeczywistym odbiorze Windows/NVDA. **To nadal rozwijany pełny interfejs równoległy, nie ukończony odpowiednik1:1.** Nazwa katalogów `wxlite` jest pozostałością wcześniejszego prototypu.

## Działa i zostało odebrane

- Naturalny start wxPython z rzeczywistym hostemC#, nie ręczne wypełnienie okna.
- Biblioteka czytana z bieżącego SQLite w trybie `mode=ro`, z WAL. Przy błędzie nie przechodzi samowolnie na stary obraz `immutable`.
- Foldery, wejście i powrót z zachowaniem wyboru, identyfikatory jako napisy.
- Wszystkie pliki alfabetycznie: Alt+2.
- Lokalne Ulubione: Ctrl+U, kolejność według dodania lub własna w warstwie danych. Nie deklarujemy jeszcze alfabetyki Ulubionych.
- Katalog playlist: Ctrl+P; Enter otwiera zawartość, Backspace wraca do zaznaczonej playlisty.
- Historia odtwarzania: Ctrl+H (zgodnie z `CommandIds.ViewHistory`, `MainWindow.xaml:481`). Etykieta mówi „Historia odtwarzania" i jest to ODCZYT zapisanej historii AMC, nie historia własnego odtwarzania tego okna. Wiersze filtrowane jak w AMC do `ActiveLocalItems`.
- Kolejka Ctrl+Q: początkowo odczyt zapisu profilu, po inicjalizacji stan hosta, także po wyczerpaniu. Enter w zapisanej kolejce przesyła flagi członkostwa i kolejność, Enter w żywej wykonuje tylko `queue.playAt`. Naturalne B→A→C zmierzono w poprzednim kandydacie `bb4c917`; końcowy odbiór całej integracji nadal otwarty (transport C# oraz poprawki rodzica: pusta kolejka bez powrotu zapisanych utworów, aktualizacja już otwartej listy i kontekstu Plików podczas oglądania Radia).
- Dwa ODDZIELNE widoki zakładek, dwa skróty, dwie akcje:
  - **Ctrl+B** = `Action.VIEW_ALL_BOOKMARKS` → `all_bookmark_rows` (`BookmarkIndex.GetForDisplay`): WSZYSTKIE zakładki z bieżącym materiałem sesji na początku. Tak jak `CommandIds.ViewBookmarks` w C#.
  - **Ctrl+Shift+B** = `Action.VIEW_ITEM_BOOKMARKS` → `bookmark_rows` (`GetForItem`): zakładki JEDNEGO wybranego pliku. Ten skrót jest nasz, bo w C# ten węższy zakres nie ma własnej komendy.

  Wcześniej Ctrl+B był świadomie wolny — podpięcie pod niego węższego `GetForItem` byłoby podstawieniem węższego zakresu pod istniejącą szerszą komendę. Po dołożeniu `all_bookmark_rows` ten powód zniknął: Ctrl+B dostał zakres, który ma w AMC.
- Kontekst widoku zbiorczego to **`CurrentSession.CurrentItem`**, nie zaznaczony wiersz. Materiał w PAUZIE nadal jest bieżący — `CurrentItem` nie jest tym samym co `IsPlaying` (zmierzone: `status_text="Wstrzymano"`, a Ctrl+B dalej stawia zakładkę tego pliku u góry).
- Tożsamość materiału: dane mają profilowy `Id` (`local-...`), host potrafi zwracać `file:<path>`, a wiersz zakładki ma `bookmark:<id>`. Żaden z tych prefiksów nie zastępuje profilowego `Id` — kontekst bierzemy po POTWIERDZONYM starcie, nie po samym zaznaczeniu ani nieudanej próbie.
- Enter na zakładce przechodzi FIZYCZNIE do materiału i pozycji: `play.file` dostaje `positionSeconds` w jednym wywołaniu, bez osobnego seeka po starcie. `Row.item_id` zakładki (`bookmark:<id>`) nigdy nie trafia do backendu jako plik — ścieżka i pozycja idą z mapy celów, a `BookmarkRow.item_id` jest ID pliku.
- Pozycja liczona jako `position_ticks / 10_000_000` z zachowaną częścią ułamkową (bez `//`).
- Backspace z widoku zakładek wraca na TEN plik (zmierzone: wiersz 2309 z 2476, to samo ID), nie na wiersz pierwszy.
- Foldery Biblioteki są w menu (Alt+1), a „Powrót na listę” wywołuje istniejące SHOW_LIST. Prawdziwy PLAYER→LIST przez menu został odebrany w `all-bookmarks-gui-after422/parent-acceptance.json`; wcześniejszy niepełny P13 nie jest dowodem tego przejścia.
- Pusta lista jest odebrana: `MediaListAccessible` udostępnia nazwę i rolę, a `_announce_empty_list` zgłasza samą kontrolkę po zniknięciu ostatniego dziecka. Żywy NVDA czyta listę zamiast „nieznane”; Backspace wraca na właściwy plik. Kwity `native-empty-final-after422/` i końcowy `native-w02-valid-retest-after422/parent-final/`.

## Mowa listy — odebrany zakres i pozostałe ograniczenia

- Wszystkie istniejące listy korzystają ze wspólnego LC_REPORT i `list_sync`: brak zmian = zero operacji, zmiana pola nie przepisuje reszty. Pełna podmiana danych jest bramkowana zmianą widoku, bez rekreacji HWND. Końcowy żywy odbiór potwierdził pojedynczy odczyt i prawidłową nazwę po Foldery/Playlisty → Wszystkie pliki, strzałki, rzeczywiste puste zakładki i powrót. Dane nie są obcinane ani stronicowane. To wynik pomiaru, nie wniosek z samej zmiany klasy kontrolki.
- Radio czytane z aktualnego `radio.stations` profilu. Brak/awaria odczytu nie jest już traktowana jak prawdziwie pusta lista. Wspólny profil nadal nie jest edytowany przez Python.
- Ctrl+C kopiuje nazwę; Ctrl+Shift+C kopiuje pełną ścieżkę jako TEKST i tak ją nazywa: „Skopiowano pełną ścieżkę". Oba potwierdzenia odczytane na żywymNVDA. Obsługi formatuFileDrop (przeciąganie pliku do innej aplikacji) nadal **nie ma** — komunikat już jej nie udaje.
- Ctrl+O otwiera natywny dialog folderu. Odtwarzanie z normalnej listy, pauza i wznowienie Spacją: potwierdzony czas i sygnał wyjściowy.
- Przycisk transportu nazywa czynność: Odtwórz albo Wstrzymaj. Zmiany i krótkie komunikaty zostały odczytane w Podglądzie mowyNVDA.
- Nieznana długość jest pokazywana jako brak („łączny czas nieznany"), nie jako `0:00`. Wzorzec zC# `MediaItemFormatter.FieldValue`: przy `Duration <= TimeSpan.Zero` pole jest pomijane. Prawdziwe czasy (np. `5:03`) zostają.
- Pasek menu wxPython udostępnia działające już funkcje: sesje, otwieranie pliku/folderu, widoki Biblioteki, odtwarzacz/lista, transport, kopiowanie i pomoc. Każda pozycja woła ten sam `MainWindow._dispatch`, co skrót — bez drugiej implementacji. Zachowane menu Dźwięk/ustawienia. Pozycje niedostępne w kontekście (np. lokalne Ulubione w Radiu) są wyszarzane, nie ukrywane.

## Zakres pomiaru

Pełna zachowana kopia danych zawiera11200rekordów lokalnych, z których2475spełnia filtr aktywności. W próbie były9Ulubionych,1playlista/54pozycje i165stacji. To liczby badanej kopii, nie dzisiejszy odczyt głównego komputera ani stałe w kodzie.

Końcowy zachowany runner:**444zdanych,0padniętych,0pominiętych**. Odbiór rodzica:23gesty z prawidłową bramką,19niepustych przyrostów mowy; pomocniczeTab i ruch na jedynymwierszu nie muszą zmieniać odczytu. Na nowych widokach wykonano rzeczywisteAlt+2/Ctrl+U/Ctrl+P/Enter/Backspace, nie wywołania handlera.

Widoki aktywności (historia/kolejka/zakładki) zmierzone na żywymNVDA w `amc_pomoc/wx-full-profile-after421/activity-gui-after422/`: Ctrl+H→119wierszy, Ctrl+Q→6, Ctrl+Shift+B na wybranym pliku→1, na innym pliku→„Zakładki, pusto". Menu Alt+B+H/K/Z dało te same widoki ze stanu różnego od wyniku. Skok zakładki dowiedziony pozycją z ŻYWEGO hosta: start na≈83,4s przy długości120,06s (odczyty91,2→100,2s w kolejnych sekundach). Materiał jest SYNTETYCZNY — wygenerowany plik i wstrzyknięty rekord w OSOBNEJ prywatnej kopii pełnej bazy, nie plik użytkownika.

Próba mowy i menu po tej zmianie: `amc_pomoc/wx-full-profile-after421/menu-and-speech-after422/proba-koncowa.json`. Menu obchodzono rzeczywistymAlt/strzałkami/literami naWindows zNVDA; schowek czytano zWindows i przywrócono do stanu sprzed próby.

Dźwięk zmierzono wcześniej na jawnym syntetycznymWAV: sygnał występuje podczas grania, zanika w pauzie i wraca po wznowieniu. To pomiar wyjścia, nie ludzka ocena jakości brzmienia. Narrator nie został odebrany w tych przebiegach.

Dowody robocze pozaGit: `amc_pomoc/wx-full-profile-after421/integrated-after422/parent-closure/` oraz `library-gui-after422/parent-acceptance/`.

## Sortowanie

Foldery zachowują dotychczasoweAMC_PL (`IgnoreCase|IgnoreNonSpace`). Wszystkiepliki korzystają z osobnego trybu tytułów bez ignorowania akcentów i natywnego klucza ścieżki OrdinalIgnoreCase. Cache trybów są oddzielne, wsady mieszczą się w limicie64KiB. Klient wykrywa stary host nieobsługujący nowego trybu.

Rogié/e orazß/ss były odtworzone jakoRED i poprawione. LiteHost został następnie zbudowany naWindows; trzy tryby wywołano w rzeczywistym procesie. Pomiary dotycząpl-PL; taką kulturę odczytano też na stanowiskuWindows. Nie rozciągamy wyników na niezmierzone kultury.

## Zbiorczy widok zakładek w interfejsie — zmierzony odbiór

Kwity: `amc_pomoc/wx-full-profile-after421/all-bookmarks-gui-after422/`
(`odbior-zakladek.json`, `odbior-zakladek-bcd2.json`, `odbior-zakladek-d.json`,
`REPORT.md`). Żywe okno, własny profil `profile-bookmark`, żywy host,
odczyt mowyNVDA przez podgląd.

Zdane:

- **Ctrl+B**: wejście fizyczne z Wszystkich plików (2476 wierszy) → `LibraryView.ALL_BOOKMARKS`, `rows=30` == 30 zakładek odczytanych z bazy, rola 15, mowa „Biblioteka — Wszystkie zakładki, 30 pozycji, kolejność zastępcza".
- **Bieżący materiał sesji, także w pauzie**: zwykły Enter → PLAYER (`files.play` BEZ `positionSeconds`), Spacja → „Wstrzymano", Ctrl+B z pauzy → zakładka tego pliku u góry, zgodnie z wynikiem `all_bookmark_rows(current_item_id=...)`.
- **Lokalny skok**: dokładnie **jeden** `files.play` z `positionSeconds=83.456` (== `position_seconds` z danych), **zero** wywołań `*seek*`. Dziennik wywołań hosta w `host-calls.jsonl`.
- **Odmowa sesji nielokalnej**: Enter na wierszu Spotify → brak `files.play`, zostajemy na wierszu, mowa „Ta zakładka należy do sesji Spotify. Ten program odtwarza tylko pliki lokalne."
- **Powrót PLAYER→LIST menu**: `alt` (pasek menu, rola 11) → Widok → „Powrót na listę" → `session_view=View.LIST`, `player_shown=false`, wiersz czytany. Menu „Lista→Lista" nie jest dowodem tego przejścia — do jego pokazania obserwator dostał pole `session_view` (`library_view` nie rozróżnia odtwarzacza od listy).

### Historyczne obserwacje list — zamknięte przed obecną kolejką

Puste listy, podwójny odczyt i stare nazwy zostały odebrane po zmianie wspólnego mechanizmu list w `14bcc285`; dawne kwity powyżej nie otwierają ich ponownie. `library_view=None` oznacza poprawny widok Folderów, nie brak implementacji. Aktualne dowody wskazują początkowe sekcje tej mapy oraz `native-w02-valid-retest-after422/parent-final/`.

## Najbliższe braki

- Mechanizm wszystkich dotychczasowych list jest odebrany; nie powtarzamy zamkniętego audytu W02. Pozostaje wdrażanie kolejnych funkcji przez ten sam mechanizm.
- Dalsze widoki Biblioteki i wyszukiwanie/filtry. Historia i oba zakresy zakładek są odczytem. Żywa kolejka jest odebrana na pełnej kopii przez rodzica (`parent-speech-full-profile/`): naturalne B→A→C, C wybrane PRZED przejściem i zachowane, samoaktualizacja otwartej listy, prawdziwa mowa NVDA oraz 0 po wyczerpaniu i ponownym Ctrl+Q. Trwała kolejność jest odebrana na prywatnej kopii (sekcja niżej); nadal brak czasu wznowienia, mutacji zakładek i bezpiecznego wspólnego pisarza ze starym WPF.
- ObsługaFileDrop dla Ctrl+Shift+C (parytet przeciągania pliku) jako osobny etap.
- Zapis Ulubionych, playlist, kolejności i pozostałego stanu przez jednego właścicielaC#.
- Pozostałe sesje i ich pełna obsługa, nagrywanie/harmonogramy, pozostałe ustawienia, presety oraz redakcja materiałów.

Nie policzono rzetelnie procentu zgodności całego programu. Dawne237pozycji/196skrótów to historyczny spisWPF z421, nie aktualny mianownik pokrycia i nie dowód dostępności każdej funkcji.

## Trwała kolejność na prywatnej kopii — odebrana

Zwykłe GUI samo uruchamia hosta z argumentami prywatnego profilu i pisarza. Kolejność, członkostwo i świadomie pusty stan przeżywają restart. Odmowa zapisu jest czytana przez NVDA również po ostatnim utworze w odtwarzaczu, bez powtarzania tej samej przyczyny. Rodzic wykonał pełny cykl GUI na kopii11203/5000; `queue-persistence-after-live/gui-integration-parent/`. Czas wznowienia i bieżące ID nie są jeszcze utrwalane. Domyślny wspólny profil nadal pozostaje tylko do odczytu.

## Własność danych — zasada już przyjęta

Python czyta wspólny profil i trzyma własne ustawienia interfejsu osobno. Zapis danych wspólnych ma należeć do jednego właścicielaC#, z ochroną przed dwiema instancjami/harmonogramami. To przyjęta decyzja techniczna, a nie pytanie do użytkownika o wybór wariantu. Operacje zapisu nie są jeszcze przez to automatycznie zaimplementowane.

Pomiary są na kopii. Nie migruj ani nie nadpisuj profilu użytkownika przy testach. Techniczne plikiSQLiteWAL/SHM nie są podstawą do użycia `immutable=1` na żywych danych.
