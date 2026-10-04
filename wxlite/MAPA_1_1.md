# AMC Python — zakres względem pełnego AMC

Stan kodu po192aa07 i rzeczywistym odbiorze Windows/NVDA. **To nadal rozwijany pełny interfejs równoległy, nie ukończony odpowiednik1:1.** Nazwa katalogów `wxlite` jest pozostałością wcześniejszego prototypu.

## Działa i zostało odebrane

- Naturalny start wxPython z rzeczywistym hostemC#, nie ręczne wypełnienie okna.
- Biblioteka czytana z bieżącego SQLite w trybie `mode=ro`, z WAL. Przy błędzie nie przechodzi samowolnie na stary obraz `immutable`.
- Foldery, wejście i powrót z zachowaniem wyboru, identyfikatory jako napisy.
- Wszystkie pliki alfabetycznie: Alt+2.
- Lokalne Ulubione: Ctrl+U, kolejność według dodania lub własna w warstwie danych. Nie deklarujemy jeszcze alfabetyki Ulubionych.
- Katalog playlist: Ctrl+P; Enter otwiera zawartość, Backspace wraca do zaznaczonej playlisty.
- Radio czytane z aktualnego `radio.stations` profilu. Brak/awaria odczytu nie jest już traktowana jak prawdziwie pusta lista. Wspólny profil nadal nie jest edytowany przez Python.
- Ctrl+C kopiuje nazwę; Ctrl+Shift+C kopiuje tekst adresu/ścieżki. Potwierdzenia docierają do żywegoNVDA. Obecny komunikat dla ścieżki mówi zbyt wiele o skopiowaniu pliku; nie jest to jeszcze obsługa formatuFileDrop.
- Ctrl+O otwiera natywny dialog folderu. Odtwarzanie z normalnej listy, pauza i wznowienie Spacją: potwierdzony czas i sygnał wyjściowy.
- Przycisk transportu nazywa czynność: Odtwórz albo Wstrzymaj. Zmiany i krótkie komunikaty zostały odczytane w Podglądzie mowyNVDA.

## Zakres pomiaru

Pełna zachowana kopia danych zawiera11200rekordów lokalnych, z których2475spełnia filtr aktywności. W próbie były9Ulubionych,1playlista/54pozycje i165stacji. To liczby badanej kopii, nie dzisiejszy odczyt głównego komputera ani stałe w kodzie.

Końcowy zachowany runner:326zdanych,0padniętych,0pominiętych. Odbiór rodzica:23gesty z prawidłową bramką,19niepustych przyrostów mowy; pomocniczeTab i ruch na jedynymwierszu nie muszą zmieniać odczytu. Na nowych widokach wykonano rzeczywisteAlt+2/Ctrl+U/Ctrl+P/Enter/Backspace, nie wywołania handlera.

Dźwięk zmierzono wcześniej na jawnym syntetycznymWAV: sygnał występuje podczas grania, zanika w pauzie i wraca po wznowieniu. To pomiar wyjścia, nie ludzka ocena jakości brzmienia. Narrator nie został odebrany w tych przebiegach.

Dowody robocze pozaGit: `amc_pomoc/wx-full-profile-after421/integrated-after422/parent-closure/` oraz `library-gui-after422/parent-acceptance/`.

## Sortowanie

Foldery zachowują dotychczasoweAMC_PL (`IgnoreCase|IgnoreNonSpace`). Wszystkiepliki korzystają z osobnego trybu tytułów bez ignorowania akcentów i natywnego klucza ścieżki OrdinalIgnoreCase. Cache trybów są oddzielne, wsady mieszczą się w limicie64KiB. Klient wykrywa stary host nieobsługujący nowego trybu.

Rogié/e orazß/ss były odtworzone jakoRED i poprawione. LiteHost został następnie zbudowany naWindows; trzy tryby wywołano w rzeczywistym procesie. Pomiary dotycząpl-PL; taką kulturę odczytano też na stanowiskuWindows. Nie rozciągamy wyników na niezmierzone kultury.

## Najbliższe braki

- Uspokojenie odczytu przy zmianie listy: obecnie pojawia się poprzednia nazwa i powtarzanie wybranej pozycji.
- Uczciwa treść potwierdzenia kopiowania ścieżki i prezentacja nieznanej długości zamiast0:00.
- Dostępne menu dla funkcji już wdrożonych, dalsze widoki Biblioteki, wyszukiwanie/filtry, historia, kolejka i zakładki.
- Zapis Ulubionych, playlist, kolejności i pozostałego stanu przez jednego właścicielaC#.
- Pozostałe sesje i ich pełna obsługa, nagrywanie/harmonogramy, pozostałe ustawienia, presety oraz redakcja materiałów.

Nie policzono rzetelnie procentu zgodności całego programu. Dawne237pozycji/196skrótów to historyczny spisWPF z421, nie aktualny mianownik pokrycia i nie dowód dostępności każdej funkcji.

## Własność danych — zasada już przyjęta

Python czyta wspólny profil i trzyma własne ustawienia interfejsu osobno. Zapis danych wspólnych ma należeć do jednego właścicielaC#, z ochroną przed dwiema instancjami/harmonogramami. To przyjęta decyzja techniczna, a nie pytanie do użytkownika o wybór wariantu. Operacje zapisu nie są jeszcze przez to automatycznie zaimplementowane.

Pomiary są na kopii. Nie migruj ani nie nadpisuj profilu użytkownika przy testach. Techniczne plikiSQLiteWAL/SHM nie są podstawą do użycia `immutable=1` na żywych danych.
