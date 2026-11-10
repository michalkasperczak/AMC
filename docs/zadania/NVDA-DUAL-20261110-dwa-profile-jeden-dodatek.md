# NVDA-DUAL-20261110 — AMC Test i AMC Public z jednym dodatkiem

Stan 10.11.2026: kod, testy celowane oraz kompilacje hosta i programu testowego zaliczone. Instalacja obu profili, aktualizacja dodatku w działającym NVDA, ręczny odsłuch oraz publikacja nie zostały wykonane. Bieżący AMC Test i NVDA pozostały uruchomione. AMC Public nie był uruchomiony użytkowo.

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

- Zainstalowany AMC Test nadal korzysta ze starszego wspólnego potoku; nowy kod został tylko skompilowany w repozytorium. Public i dodatek NVDA pozostają w dotychczasowych instalacjach. Aktualizację obu profili i dodatku trzeba skoordynować, gdy można bezpiecznie zatrzymać programy. Po instalacji sprawdzić start dwóch profili, skróty z każdego okna i spoza AMC, dialogi, powrót fokusu, ponowne otwarcie, restart NVDA oraz mowę/brajl. Nie wykonywać poleceń przełączających nagrywanie w teście na rzeczywistych nagraniach.
- 10.11.2026 podczas pracy źródłowej działały AMC Test, host i NVDA. W oknie AMC Test widać było Radio Gdańsk, lecz samo okno nie potwierdzało jednoznacznie stanu wszystkich nagrań w tle. W katalogu `recording-staging` nie było pliku zmienionego w ostatnich 10 minutach przed testem. Procesów nie zamykano i niczego nie instalowano.
- Część starszych instrukcji w katalogu głównym dotyczy historycznego AMC; aktualna pomoc dodatku jest w `nvda-addon/addon/doc/pl/readme.html`. Nie użyto starych instrukcji jako wzorca obecnego wydania.

## Wersja i commit

- Stan źródła przed zmianą: `4c89848245887397c2ef2c99055eb490b67783bf`, gałąź `codex/amc-test-public-20261109`; AMC `alpha.491`, dodatek `0.4.1`.
- Kandydat źródłowy dodatku: `0.4.2`. Nie ma nowego pakietu `.nvda-addon` ani instalatora AMC. Numer AMC pozostaje `alpha.491` do osobnego, zweryfikowanego wydania.
- Commit implementacji i testów: `c7f7eb11` na gałęzi `codex/amc-test-public-20261109`. Niniejszy wpis z numerem commitu jest osobnym zapisem dokumentacyjnym.

## Szkic opisu zmian do korekty użytkownika

Dodatek NVDA rozróżnia teraz AMC Test i AMC Public uruchomione jednocześnie. Skróty działają w aktywnym profilu, a poza AMC w ostatnio używanym. Wybór pozostaje po restarcie NVDA. Polecenia nie przeskakują do drugiego profilu po zmianie okna. Gesty pozostały takie same. Aktualizacja wymaga nowych wersji obu hostów AMC i dodatku; instalacja oraz odbiór z żywym NVDA czekają.
