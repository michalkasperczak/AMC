# NVDA-DUAL-20261110 — AMC Test i AMC Public z jednym dodatkiem

Stan 10.11.2026 po wydaniu: kod, testy celowane, pełne paczki i instalatory obu profili zaliczone. Wydania Test i Public opublikowano na GitHub. AMC Public zainstalowano lokalnie i sprawdzono cicho jego integralność. AMC Test i dodatek w działającym NVDA pozostawiono do aktualizacji użytkownika; ręczny odbiór z żywym NVDA nie został wykonany.

## Korekta użytkownika i wcześniejsza decyzja

10.11.2026 użytkownik polecił naprawić równoczesną pracę AMC Test i AMC Public z jednym dodatkiem NVDA, bez zmiany gestów. Ta decyzja zastępuje wcześniejsze założenie, że potok `AMC.NVDA.v1.<SessionId>` wystarczy dla wszystkich profili. Log z 10.11.2026, 14:35–14:36, zawiera „Wszystkie wystąpienia potoku są zajęte”. Dwa hosty mogły konkurować o jedną nazwę, a skrót trafić do niewłaściwego profilu.

## Materiał do dokumentacji użytkownika

- Jeden dodatek NVDA 0.4.2 obsługuje aktualne AMC Test i AMC Public. Gdy pracujesz w oknie AMC Test, istniejące globalne skróty `Ctrl+Windows` sterują Testem; w oknie AMC Public sterują Public. Ta sama zasada dotyczy okien dialogowych należących do danego programu.
- Po przejściu do innej aplikacji skróty sterują ostatnio używanym profilem AMC. Dodatek pamięta ten wybór po restarcie NVDA. Polecenie już oczekujące w kolejce zachowuje profil z chwili naciśnięcia skrótu.
- Jeśli oba AMC działają i nie było jeszcze wyboru, NVDA mówi: „Działają AMC Test i AMC Public. Wybierz okno właściwego programu, a potem ponów skrót.” Jeśli ostatnio używany profil jest zamknięty, NVDA podaje jego nazwę i prosi o wybranie okna. Dodatek nie przełącza wtedy samoczynnie polecenia na drugi profil.
- Gesty nie zmieniły się. Przykładowo `Ctrl+Windows+I` odczytuje stan wybranego programu, `Ctrl+Windows+P` steruje odtwarzaniem, a `Ctrl+Windows+Alt+R` steruje nagrywaniem bieżącej stacji. Pełna mapa pozostaje w `NVDA > Ustawienia > Zdarzenia wejścia > AMC` oraz w pomocy dodatku.
- Do równoczesnej pracy trzeba zaktualizować oba używane profile AMC oraz dodatek. Starszy host ze wspólnym potokiem nie odpowie dodatku 0.4.2. Błąd połączenia wymienia wybrany profil.
- Dane, ustawienia, kolekcje, nagrania i poświadczenia Test/Public pozostają w dotychczasowych oddzielnych magazynach. Poprawka zmienia jedynie kanał poleceń NVDA i jego wybór.

## Uzasadnienie i decyzje

- Host wxPython otrzymuje profil `test` albo `public` przez istniejący argument `--release-profile`. Jego serwer NVDA tworzy teraz tylko potok `AMC.NVDA.v1.<SessionId>.test` albo `.public`. Nie otwiera równolegle starego wspólnego potoku. Nieznany profil jest odrzucany.
- Dodatek rozpoznaje profil po oknie głównym należącym do procesu aktywnego okna. Działa to również wtedy, gdy fokus ma dialog tego procesu. W innych aplikacjach korzysta z zapamiętanego wyboru; gdy go nie ma, wybiera jedyny działający profil albo podaje jasny komunikat o braku jednoznacznego celu.
- Wybór profilu jest utrwalany w konfiguracji NVDA. Nie zawiera ścieżek zbiorów ani danych kont. Kolejka przechowuje wybrany profil razem z poleceniem; powtarzane polecenia głośności i przewijania łączą się tylko wewnątrz tego samego profilu.
- Katalog poleceń, gesty aplikacji, `Ctrl+F1`, pełna pomoc klawiatury, paleta `Ctrl+Shift+K` i menu AMC nie zmieniły się, ponieważ nie dodano funkcji ani skrótu. Test istniejącej mapy gestów pozostał zaliczony. Pomoc samego dodatku oraz jego opis wersji zostały uaktualnione.

## Testy i wynik

- `python -m pytest nvda-addon/tests/test_controller.py nvda-addon/tests/test_profile_routing.py nvda-addon/tests/test_gesture_scope.py -q -p no:cacheprovider -k 'not python_dotnet_interop'`: **16 zaliczonych, 1 wyłączony do osobnego przebiegu, 68 podtestów zaliczonych**. Obejmują mapę gestów, wybór aktywnego okna, pracę poza AMC, brak celu, pamięć po restarcie i kolejkę.
- `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj -c Debug --no-restore`: **kod 0, 0 ostrzeżeń, 0 błędów**.
- `dotnet build tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj -c Release --no-restore`: **kod 0, 0 ostrzeżeń, 0 błędów**.
- `dotnet .../AccessibleMediaController.Windows.SmokeTests.dll --nvda-bridge-smoke`: **kod 0**. Dwa serwery na osobnych syntetycznych nazwach nie mieszają poleceń ani odpowiedzi; przechodzą też walidacja protokołu, limit czasu i zamknięcie.
- `python -m pytest nvda-addon/tests/test_controller.py -q -p no:cacheprovider -k python_dotnet_interop`: **1 zaliczony**; zgodność transportu Python/.NET.
- `AMC_DUAL_HOST_EXE=<świeżo zbudowany amc_lite_host.exe> python -m pytest wxlite/tests/test_nvda_dual_host.py -q -p no:cacheprovider`: **1 zaliczony**. Dwa rzeczywiste procesy hosta, bez konfiguracji audio i magazynów użytkownika, przyjęły polecenia przez własne potoki i oddały odpowiedź wyłącznie właściwemu profilowi. Test pomija wykonanie, gdy potok profilu zajmuje już użytkowy AMC.
- Końcowa regresja tych testów dodatku, trasowania, mapy gestów, mostka wxPython i instalatora po poprawce sygnału gotowości: **50 zaliczonych, 68 podtestów zaliczonych**. Jedno wcześniejsze uruchomienie całego zestawu ujawniło wyścig wyłącznie w testowym sygnale „READY”; pojedynczy test przeszedł, a po zmianie serwer ogłasza gotowość dopiero po rozpoczęciu nasłuchu. Powtórne kompilacje obu projektów: **0 ostrzeżeń, 0 błędów**. Test dwóch prawdziwych hostów po tej zmianie: **1 zaliczony**.
- Próba API `foreground_windows()` na rzeczywistym pulpicie zwróciła proces i jego okna. Nie wykonano jeszcze nawigacji i odsłuchu na żywym NVDA z dwoma oknami AMC.

## Znane ograniczenia i następny odbiór

- Zainstalowany AMC Test nadal korzysta ze starszego wspólnego potoku; AMC Public jest już w wersji alpha.492. Dodatek NVDA pozostaje w dotychczasowej instalacji. Aktualizację Test i dodatku trzeba skoordynować, gdy można bezpiecznie zatrzymać nagrania i zrestartować NVDA. Po aktualizacji sprawdzić start dwóch profili, skróty z każdego okna i spoza AMC, dialogi, powrót fokusu, ponowne otwarcie, restart NVDA oraz mowę/brajl. Nie wykonywać poleceń przełączających nagrywanie w teście na rzeczywistych nagraniach.
- 10.11.2026 podczas pracy źródłowej działały AMC Test, host i NVDA. W oknie AMC Test widać było Radio Gdańsk. Późniejsze odczytanie żywego okna przez dostępność systemową ujawniło komunikat „Rozpoczynam nagrywanie w tle: Kolor”. To zastępuje wcześniejsze niejednoznaczne rozpoznanie na podstawie plików `recording-staging`: nagranie w tle było aktywne. Procesów nie zamykano i niczego nie instalowano. Przed budową pełnych paczek, instalacją i restartem należy ponownie ustalić stan nagrywania.
- Część starszych instrukcji w katalogu głównym dotyczy historycznego AMC; aktualna pomoc dodatku jest w `nvda-addon/addon/doc/pl/readme.html`. Nie użyto starych instrukcji jako wzorca obecnego wydania.

## Wersja i commit

- Stan źródła przed zmianą: `4c89848245887397c2ef2c99055eb490b67783bf`, gałąź `codex/amc-test-public-20261109`; AMC `alpha.491`, dodatek `0.4.1`.
- Dodatek `0.4.2`: lokalny pakiet `AMC-NVDA-0.4.2.nvda-addon` ma 15 528 bajtów, poprawną strukturę ZIP i SHA-256 `22e211db6030c0c5c7e8b343066f2568647c72cf5bca8e66b3398418047a1ae9`. Nie został zainstalowany w działającym NVDA. AMC ma numer `0.1.0-alpha.492` w `Directory.Build.props`; oba instalatory tej wersji powstały i zostały wydane, jak zapisano niżej.
- Commit implementacji i testów: `c7f7eb11` na gałęzi `codex/amc-test-public-20261109`. Niniejszy wpis z numerem commitu jest osobnym zapisem dokumentacyjnym.

## Szkic opisu zmian do korekty użytkownika

Dodatek NVDA rozróżnia teraz AMC Test i AMC Public uruchomione jednocześnie. Skróty działają w aktywnym profilu, a poza AMC w ostatnio używanym. Wybór pozostaje po restarcie NVDA. Polecenia nie przeskakują do drugiego profilu po zmianie okna. Gesty pozostały takie same. Aktualizacja wymaga nowych wersji obu hostów AMC i dodatku; instalacja oraz odbiór z żywym NVDA czekają.

Osobne szkice do korekty użytkownika: `NVDA-DUAL-20261110-instrukcja-szkic.md` i `NVDA-DUAL-20261110-zmiany-szkic.md`. Nie zostały opublikowane jako gotowa instrukcja ani lista zmian.

## Wydanie i instalacja — 10.11.2026

Użytkownik polecił opublikować wydania GitHub i samodzielnie zaktualizować AMC Test; wyraźnie zezwolił na lokalną instalację AMC Public. To zastępuje wcześniejszą decyzję o wstrzymaniu całego wydania do zakończenia nagrania. Działającego AMC Test ani NVDA nie zamykano. Odczyt okna przez Computer Use odrzuciła automatyczna kontrola po wcześniejszym zatrzymaniu tego narzędzia klawiszem Escape; nie ponawiano go ani nie wykonywano odbioru na żywo.

- Źródło obu wydań: `f36c8a7ca8c40129283e4ff2bea421d3978ea5b2`, gałąź `codex/amc-test-public-20261109`, wysłana i odczytana na GitHub. Drzewo śledzonych plików było czyste przy tworzeniu instalatorów i publikacji.
- `Build-AMCBundle.ps1` z osobnym runtime alpha.491 i przypiętymi zależnościami TIDAL oraz, dla Test, Sonos: Test 3456 plików, Public 3149. Obie paczki: `sourceDirty=false`, właściwy profil i wersja alpha.492, pełna kontrola integralności oraz kontrolowany start GUI i hosta zaliczone. Są w `publish/bundles/test-alpha492-20261110` i `publish/bundles/public-alpha492-20261110`.
- `Build-AMCInstaller.ps1`: kontrolowany start paczek, kontrola sekretów `passed`, zgodność commitu i kompresja zaliczone. Instalatory zawierają opcjonalny dodatek AMC NVDA 0.4.2; Test dodatkowo Quick App Switcher 0.1.6.
- Test: `publish/test-alpha492-20261110/AMC-Test-2026-11-10-1734.exe`, 136 499 695 bajtów, SHA-256 `e12847a24d137fb6e65455cf3528ce52c04b3e832a16e859ab2d0b2fda8fd55d`. GitHub `test-2026.11.10.1734`: wydanie wstępne, trzy zasoby ze stanem `uploaded`; zdalny digest instalatora i rozmiar zgodne z lokalnymi.
- Public: `publish/public-alpha492-20261110/AMC-Public-2026-11-10-1737.exe`, 133 908 722 bajty, SHA-256 `6bfae7bda58e30b53d655898169a4e8c915d2135e06dabf8c736163c2f84028b`. GitHub `public-2026.11.10.1737`: zwykłe wydanie oznaczone `Latest`, trzy zasoby `uploaded`; zdalny digest instalatora i rozmiar zgodne z lokalnymi. Oba wydania wskazują commit źródłowy powyżej.
- Instalator Public uruchomiono z `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`; kod wyjścia 0, log `work/NVDA-DUAL-20261110-install-public.log` zawiera `Installation process succeeded` i `Need to restart Windows? No`. Tryb cichy pominął opcjonalny instalator dodatku NVDA i start GUI. Zainstalowany `C:\Users\micha\AppData\Local\Programs\AMC Public\AMC.exe` ma wersję produktu alpha.492; jego manifest wskazuje `public` i commit `f36c8a7c`, a `--sprawdz-pakiet-pelny-cicho` zwrócił 0. Procesy AMC Test, jego hosta i NVDA po instalacji pozostały te same.
- Nie instalowano AMC Test ani dodatku NVDA 0.4.2; Public nie został uruchomiony użytkowo. Wyniki budowy, publikacji i instalacji nie są dowodem ręcznej pracy dwóch okien z żywym NVDA. Lokalne paczki i instalatory zachowano do odbioru; nie czyszczono ich.

## Zgłoszenie skrótu presetów — 11.11.2026

Użytkownik zgłosił komunikat „Brak połączenia z AMC. Uruchom wersję alpha 342 lub nowszą; jeśli działa, spróbuj za chwilę” po skrócie z dodatku NVDA. Następnie potwierdził, że presety otworzyły się po użyciu skrótu z Alt. Mapa gestów w źródłach i zainstalowanym dodatku 0.4.1 pozostaje jednoznaczna: `Ctrl+Windows+Shift+P` otwiera playlisty, `Ctrl+Windows+Alt+P` otwiera presety. W samym AMC `Ctrl+Alt+P` otwiera presety, a `Ctrl+Alt+Shift+P` przypisuje bieżący element. Te funkcje nie wymagają zmiany gestów.

Rzeczywista przyczyna komunikatu o połączeniu: działający AMC Test ma wersję `0.1.0-alpha.494` i wystawia potok `AMC.NVDA.v1.1.test`, podczas gdy zainstalowany dodatek NVDA `0.4.1` próbuje otworzyć dawny `AMC.NVDA.v1.1`. Dokładny tekst błędu znajduje się w jego zainstalowanym `transport.py`. Przygotowany pakiet dodatku `AMC-NVDA-0.4.2.nvda-addon` używa potoku profilu Test/Public; jego SHA-256 pozostaje `22e211db6030c0c5c7e8b343066f2568647c72cf5bca8e66b3398418047a1ae9`. Nie stwierdzono usterki w mapie poleceń źródeł.

Testy bez widocznego interfejsu 11.11.2026: `test_profile_routing.py`, `test_gesture_scope.py`, `test_controller.py`, `test_nvda_bridge.py`, `test_nvda_bridge_gui.py` — **31 zaliczonych, 68 podtestów zaliczonych, 1 test interoperacyjności wyłączony**. Dodatkowe testy presetów Radia, Radio Archiwisty, WiiM, Sonosa i Frontier Smart — **32 zaliczone, 79 innych pominięto filtrem**. Pierwsze uruchomienie wspólnego zestawu nie zebrało dwóch modułów z powodu braku `wxlite` w `PYTHONPATH`; poprawiony przebieg przeszedł. Nie jest to dowód odsłuchu z żywym NVDA.

Odczyt listy „Nagrywane” w bieżącym AMC Test pokazał **jedno trwające nagranie**. Nie zatrzymywano AMC, nie restartowano NVDA i nie instalowano dodatku. Po zakończeniu nagrywania trzeba zainstalować przygotowany dodatek 0.4.2, uruchomić NVDA ponownie i sprawdzić połączenie oraz skróty w aktywnych profilach i sesjach. Źródła, kompilacja i opublikowane wydanie z 10.11.2026 pozostają bez zmian; to zadanie dotyczy niedokończonej lokalnej instalacji i odbioru.

## Korekta cichej aktualizacji — 11.11.2026

Użytkownik potwierdził, że `Ctrl+Windows+P` również nie działa, i polecił, aby cicha aktualizacja AMC sama podmieniała starszy dodatek NVDA. Ta decyzja zastępuje wcześniejszy pomysł zatrzymywania cichego instalatora i kierowania użytkownika do ręcznej instalacji dodatku. Nie zmienia mapy gestów.

### Materiał do dokumentacji użytkownika

- Podczas następnej cichej aktualizacji AMC instalator porówna wersję dołączonego dodatku AMC z wersją w NVDA. Gdy dodatek jest starszy lub nieobecny, zainstaluje dołączoną wersję bez osobnego pytania i uruchomi ponownie NVDA. Gdy wersja jest aktualna lub nowsza, nie podmieni jej.
- Aktualizacja zaczeka, jeśli nadal działa AMC Test albo AMC Public. Najpierw trzeba bezpiecznie zakończyć nagrywanie i zamknąć program. Po udanej aktualizacji `Ctrl+Windows+P` steruje odtwarzaniem, `Ctrl+Windows+Alt+P` otwiera presety, a `Ctrl+Windows+Shift+P` otwiera playlisty.
- Poprzedni katalog dodatku jest zachowany w folderze konfiguracji NVDA jako kopia do odzyskania. Ustawienia NVDA, inne dodatki i dane AMC nie są podmieniane.

### Uzasadnienie i decyzje

- Dotychczasowy wpis `[Run]` miał `skipifsilent`, więc aktualizator instalował nowe AMC z pominięciem dołączonego dodatku. Nowa ścieżka cicha wywołuje pomocnika po kontrolowanym starcie i kontroli integralności AMC. Pomocnik sprawdza nazwę i wersję manifestu, sumy CRC, ścieżki w ZIP, limit rozmiaru oraz obecność zadań instalacyjnych. Podmienia wyłącznie `amcController` z kopią poprzedniego katalogu i cofa podmianę, jeśli nie uda się uruchomić restartu NVDA.
- Przed kopiowaniem instalator sprawdza muteksy obu profili AMC. Pomocnik ponawia ten warunek bezpośrednio przed podmianą. Gdy drugi profil działa, instalacja zostaje wstrzymana przed zmianą programu. Cicha instalacja bez konfiguracji dodatków NVDA pomija ten etap.
- Widoczny instalator nadal oferuje standardową instalację dodatku przez NVDA. Automatyczna podmiana dotyczy tylko cichego trybu aktualizacji i wyłącznie własnego dodatku AMC; Quick App Switcher pozostaje osobną, opcjonalną propozycją.

### Testy i wynik

- `python -m pytest wxlite/tests/test_silent_nvda_addon_update.py wxlite/tests/test_installer_profiles.py -q -p no:cacheprovider`: **32 zaliczone**. Testy obejmują aktualizację, zachowanie kopii, pominięcie aktualnej wersji, instalację brakującego dodatku, cofnięcie przy błędzie restartu, działający AMC i odrzucenie ścieżki wychodzącej poza ZIP.
- Testowa kompilacja `ISCC.exe` instalatora AMC Test z minimalną paczką i dołączonym dodatkiem 0.4.2: **kod 0**; sprawdza składnię Inno i dołączenie pomocnika. Testowy instalator nie był uruchamiany.
- Rzeczywista paczka `AMC-NVDA-0.4.2.nvda-addon` przeszła walidację nowego pomocnika: 7 wpisów. Odczytowe sprawdzenie na komputerze użytkownika wykryło uruchomione AMC, uruchomione NVDA i właściwą ścieżkę `C:\Program Files\NVDA\nvda.exe`.
- Po sprawdzeniu składni usunięto wyłącznie testowy katalog `C:\Users\micha\Documents\Codex\2026-11-06\a\iscc-smoke-nvda` (około 2,1 MB). Zawierał odtwarzalną minimalną paczkę i testowy instalator; nie zawierał wydania ani danych użytkownika.
- **Nie zbudowano pełnej paczki AMC i nie zainstalowano tej zmiany.** Żywe NVDA i AMC nie były restartowane, ponieważ nadal mogło trwać nagranie. Potrzebny odbiór cichej aktualizacji na bezpiecznej sesji oraz kontrola skrótów z obu profili.

### Znane ograniczenia

- Automatyczna ścieżka korzysta ze standardowego katalogu dodatków zainstalowanego NVDA w profilu bieżącego użytkownika. Instalacja przenośna NVDA z własnym katalogiem konfiguracji wymaga osobnego sprawdzenia.
- Jeśli NVDA nie uruchomi się po wystartowaniu polecenia restartu, pomocnik nie może tego niezawodnie rozpoznać; pozostaje kopia starego dodatku do odzyskania. Po pierwszym wydaniu konieczny jest odbiór na żywym NVDA.

### Wersja i commit

- Zmiany w źródłach: `installer/AMC_Wx_Setup.iss`, `installer/update_amc_nvda_addon.py`, `wxlite/tools/Build-AMCInstaller.ps1`, testy instalatora, rejestr zadań oraz szkice instrukcji i listy zmian. Commit, pełna paczka, instalacja i publikacja: **jeszcze nie wykonane**.

### Korekta po próbie instalacji — 11.11.2026

Wersja `alpha.495` z commitu `c8bed6af` ujawniła błąd cichej aktualizacji: Windows odmówił uruchomienia `C:\Program Files\NVDA\nvda.exe -r` przez `CreateProcess` (WinError 740). Pomocnik prawidłowo przywrócił dodatek 0.4.1, lecz Inno zwróciło kod 0 po wyjątku w `CurStepChanged`, a aktualizator uruchomił AMC Test ze starym dodatkiem. Ta instalacja nie stanowi odbioru wydania.

Zastąpiono restart wywołaniem ShellExecute i dodano `GetCustomSetupExitCode`, który zwraca 10 po błędzie kontroli instalacji albo aktualizacji dodatku. Testy celowane: 32 zaliczone. Ręczne uruchomienie poprawionego pomocnika po zwykłym zamknięciu AMC Test zainstalowało 0.4.2 i uruchomiło NVDA ponownie; stary dodatek 0.4.1 pozostawiono jako kopię w katalogu konfiguracji NVDA. Zmiany są przygotowane dla `alpha.496`; pełna instalacja i odbiór skrótów pozostają do wykonania.
