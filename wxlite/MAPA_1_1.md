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
- Kolejka: Ctrl+Q (`CommandIds.ViewQueue`, `MainWindow.xaml:482`). Etykieta brzmi **„Kolejka (zapisana)"** — czytamy zapis `queue_order`/`regular`/`play_next` z profilu, a to NIE jest kolejka żywego silnika. Enter odtwarza wybraną pozycję zwykłą drogą pojedynczego pliku; nie deklarujemy uruchomionej kolejki ani naturalnego przechodzenia po niej.
- Zakładki zaznaczonego pliku: **Ctrl+Shift+B**, świadomie INNY skrót niż Ctrl+B w C#. `CommandIds.ViewBookmarks` (Ctrl+B) woła `BookmarkIndex.GetForDisplay`, czyli WSZYSTKIE zakładki z bieżącym elementem na początku; nasza funkcja to węższe `GetForItem` dla jednego pliku. Podpięcie jej pod Ctrl+B byłoby podstawieniem węższego zakresu pod istniejącą szerszą komendę.
- Enter na zakładce przechodzi FIZYCZNIE do materiału i pozycji: `play.file` dostaje `positionSeconds` w jednym wywołaniu, bez osobnego seeka po starcie. `Row.item_id` zakładki (`bookmark:<id>`) nigdy nie trafia do backendu jako plik — ścieżka i pozycja idą z mapy celów, a `BookmarkRow.item_id` jest ID pliku.
- Pozycja liczona jako `position_ticks / 10_000_000` z zachowaną częścią ułamkową (bez `//`).
- Backspace z widoku zakładek wraca na TEN plik (zmierzone: wiersz 2309 z 2476, to samo ID), nie na wiersz pierwszy.
- Foldery Biblioteki mają wreszcie wejście w menu (Alt+1, jak `MainWindow.xaml:446`), a menu Widok — brakujący „Powrót na listę" (istniejąca akcja `SHOW_LIST`). W `list-speech-after422/odbior-listy-FINAL.json` P01 wywołał ponowne wczytanie folderów. P14+P15 wykonał pozycję menu, ale P13 nie dowodzi wcześniejszego wejścia do odtwarzacza (fokus nadal na wierszu, brak odtwarzania). Przejście PLAYER→LIST pozostaje do zmierzenia w tym menu.
- `MediaListAccessible` nadaje nazwę i rolę samej kontrolce listy. Odczyt MSAA na pustym modelu potwierdzony (`list-speech-after422/zdarzenia15.jsonl`); usunięcie „nieznane” w rzeczywistym przejściu klawiaturą do pustych zakładek NIE zostało jeszcze odebrane. FINAL P11 miał nadal 1 wiersz, nie 0.

## Mowa listy — co NADAL jest zepsute

- **Poprzednia nazwa z nowym licznikiem** przy zmianie zbioru oraz **wielokrotny odczyt tego samego wiersza** NIE są naprawione. Przyczyna jest zmierzona (`SetItemCount` na kontrolce z fokusem sam zgłasza fokus wiersza 0, a czytnik trzyma obiekty pod kluczem `(HWND, childID)`), ale jedyna skuteczna na to droga — rekreacja kontrolki — kosztowała całkowitą ciszę na wybranym wierszu przy 2476 pozycjach i została wycofana. Osiem odrzuconych prób ratowania i pełny bilans: `list-speech-after422/REPORT.md`.
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

## Najbliższe braki

- Odczyt przy zmianie widoku jest spokojniejszy, ale **nie całkiem cichy**. Zmierzone na żywymNVDA: zniknęła własna nadmiarowa zapowiedź wiersza, a na Ulubionych ubył jeden z powtórzonych odczytów. Zostaje: pojedyncze powtórzenie bieżącego elementu po ogłoszeniu widoku, a przy dużym skoku długości (1→2475) jedno odczytanie poprzedniej nazwy z nowym licznikiem. Próba odświeżania nachodzących wierszy przed `SetItemCount` **nie dała zmiany w mowie** i została wycofana — przyczyna leży głębiej niż kolejność tych dwóch wywołań. Nowe widoki aktywności tego objawu **nie usunęły i nie pogorszyły**: w kwicie `activity-gui-after422` Ctrl+Q powtarza pierwszy wiersz kolejki trzy razy, a Ctrl+U dodatkowo wypowiada „Emu … 2 z 10" przed właściwym „SYNTEZA … 1 z 10". Nie przeorganizowano kontrolki pod ten objaw, więc zostaje on jawnie OTWARTY.
- Dalsze widoki Biblioteki, wyszukiwanie/filtry. Historia, kolejka (zapisana) i zakładki zaznaczonego pliku są już ODCZYTEM podłączonym do interfejsu; brakuje natomiast zbiorczego widoku wszystkich zakładek (Ctrl+B / `GetForDisplay`), prawdziwej kolejki żywego silnika i zapisu/usuwania zakładek.
- ObsługaFileDrop dla Ctrl+Shift+C (parytet przeciągania pliku) jako osobny etap.
- Zapis Ulubionych, playlist, kolejności i pozostałego stanu przez jednego właścicielaC#.
- Pozostałe sesje i ich pełna obsługa, nagrywanie/harmonogramy, pozostałe ustawienia, presety oraz redakcja materiałów.

Nie policzono rzetelnie procentu zgodności całego programu. Dawne237pozycji/196skrótów to historyczny spisWPF z421, nie aktualny mianownik pokrycia i nie dowód dostępności każdej funkcji.

## Własność danych — zasada już przyjęta

Python czyta wspólny profil i trzyma własne ustawienia interfejsu osobno. Zapis danych wspólnych ma należeć do jednego właścicielaC#, z ochroną przed dwiema instancjami/harmonogramami. To przyjęta decyzja techniczna, a nie pytanie do użytkownika o wybór wariantu. Operacje zapisu nie są jeszcze przez to automatycznie zaimplementowane.

Pomiary są na kopii. Nie migruj ani nie nadpisuj profilu użytkownika przy testach. Techniczne plikiSQLiteWAL/SHM nie są podstawą do użycia `immutable=1` na żywych danych.
