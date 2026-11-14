# POD-PERF-20261114 — obciążenie odświeżania dużej biblioteki podcastów

Status: W trakcie. Zmiany źródeł i celowane testy przygotowane 14.11.2026. Zbudowano i zweryfikowano odizolowaną paczkę oraz instalator AMC Test 0.1.0-alpha.505. Działająca instalacja alpha.504 pozostaje nienaruszona: bezpośrednio przed planowanym zamknięciem pojawiły się dwa procesy `yt-dlp.exe`, więc zgodnie z warunkiem użytkownika nie przerywano aktywnej pracy. Użytkownik planuje nagrywanie od 07:00 czasu polskiego; po tej godzinie nie instalować ani nie restartować AMC.

## Cel i zakres

Zmniejszyć okresowe zużycie CPU i dysku przez automatyczne odświeżanie 507 zapisanych źródeł (436 z włączonym odświeżaniem) oraz przyspieszyć otwarcie dużej listy odcinków. Zachować źródła, odcinki, kolejność list, licznik pozycji, ręczne F5 i Ctrl+F5, fokus oraz działające nagrywanie. Nie zmieniać głośności ani urządzeń audio. Zadanie dotyczy bieżącego wxPython AMC Test i kanonicznej gałęzi `codex/amc-test-public-20261109`.

## Diagnoza i pomiary przed zmianą

- Prawdziwa baza AMC Test na C: miała 225 947 648 bajtów, 91 892 odcinki i 692 rekordy źródeł, z czego 507 w Bibliotece. Automatyczne odświeżanie było włączone dla 436 źródeł: 221 RSS, 212 kanałów YouTube i 3 SoundCloud. Odczytano bazę wyłącznie w SQLite `mode=ro`, bez `immutable=1`.
- W przedziale 02:37–02:39 host zapisał znaczniki odświeżenia dla 5 źródeł o 02:37:13, 5 o 02:38:18 oraz 5 o 02:39:02; odpowiadają im próbki monitoringu z logicznym zapisem hosta około 43,25–43,32 MB/s o 02:37:17, 02:38:24 i 02:39:07. To silny związek czasowy, lecz licznik zapisu obejmuje cały proces hosta, więc nie jest dokładną liczbą bajtów samej bazy.
- Inna 20-sekundowa próbka działającego hosta AMC Test, PID 37056, wykazała 0,719 s CPU, 280,423 MB odczytu i 13,837 MB zapisu procesu; w pobliskim odczycie bazy było 5 prób odświeżenia w ostatniej minucie. Osobne 10 sekund bez przebiegu dało 0 s CPU. Obciążenie występuje więc skokowo. Same logiczne liczniki procesu nie dowodzą identycznej ilości fizycznego I/O na C:.
- Wcześniejszy monitoring wykazał 80–88% jednego rdzenia dla `python.exe` przez kilka próbek 02:38:30–02:38:48. Log aplikacji pokazuje dwa starty hosta AMC o 02:38:10 i 02:38:15 oraz zamknięcia silnika o 02:38:14 i 02:38:15. Skrypt monitorujący nie zapisał PID `python.exe` ani otwartego widoku, dlatego tego skoku nie można przypisać przebudowie listy. Kod automatu odświeża widoczną listę tylko po dodaniu odcinków; przy braku nowych nic nie przebudowuje. Wcześniejszy pomiar skrzynki z 28 709 pozycji wynosił 0,124 s dla pierwszych 150.
- Odczyt 91 892 pełnych `payload_json` z SQLite trwał 1,7324 s, a skan identyfikatorów, pozycji i skrótów 0,4201 s. `ApplyRefreshBatch` w obecnym hostcie nadal wczytuje pełną bibliotekę i przelicza zapisywane rekordy przy odświeżeniu porcji, więc regularność prób mnoży ten koszt. Nie zmieniono tej semantyki zapisu w tym etapie.
- Dla największego źródła RSS (3551 odcinków) stare zapytanie strony z `COUNT(*) OVER()` trwało 0,0546 s w pomiarze SQLite i budowało tymczasową strukturę do sortowania wszystkich wierszy. Osobne `COUNT(*)` i pobranie 151 wierszy trwały razem około 0,0019 s w tym samym ciepłym odczycie. To pomiar zapytania, nie całego okna.

## Uzasadnienie i decyzje

- Automat zaczyna po pięciu minutach od startu i potem uruchamia najwyżej jeden przebieg co pięć minut, zamiast co minutę. Zachowuje wybraną przez użytkownika liczbę źródeł w przebiegu (obecnie 5), dotychczasową kolejkę należnych źródeł i trwałe znaczniki prób. Przy tych ustawieniach teoretyczny górny pułap spada z 300 do 60 prób na godzinę, o 80%. Odstęp 20/60 minut dla źródła pozostaje warunkiem najwcześniejszego sprawdzenia, nie gwarancją dokładnego terminu; pełny obrót 436 źródeł może trwać ponad 7 godzin. F5 i Ctrl+F5 działają niezależnie od automatu.
- Przebieg automatyczny czeka podczas aktywnego nagrywania i gdy status nagrywania nie został jeszcze potwierdzony. Wykorzystuje istniejącą wspólną ocenę stanu nagrywania, a przed zleceniem sieciowej porcji odczytuje stan ponownie bezpośrednio od hosta. Gdy ponowny odczyt się nie powiedzie, odkłada porcję bez rutynowego komunikatu. Po ustaniu nagrywania zaległy przebieg rusza przy następnym takcie timera. Rozpoczętego już pobierania nie przerywa w połowie.
- Lista odcinków pobiera licznik i stronę w jednej migawce SQLite, lecz dwoma zapytaniami. `LIMIT` może teraz ograniczyć pobierane dane. Nie zmienia się wybór pierwszych 150, sortowanie ani komunikat „Załaduj więcej odcinków”. Nie ma migracji schematu ani usuwania danych.
- Korekta z 14.11.2026 zastępuje wcześniejszą decyzję z 11.11.2026 o automatycznej kontroli co minutę. Dotychczasowe reguły cichego odświeżenia widoku pozostają w mocy.

## Materiał do dokumentacji użytkownika

W Ustawieniach ogólnych, w sekcji **Podcasty i YouTube → Automatyczne odświeżanie**, można nadal wybrać odstęp dla podcastów i kanałów YouTube oraz liczbę źródeł sprawdzanych w jednym przebiegu. AMC Test rozpoczyna taki przebieg co pięć minut i odkłada go, jeśli trwa nagrywanie. Przy dużej bibliotece kanał może być sprawdzony później niż wpisany dla niego odstęp. Jego miejsce w Bibliotece i wszystkie zapisane odcinki pozostają bez zmian. W sesji **Podcasty i YouTube** F5 odświeża wybrany podcast lub kanał, a Ctrl+F5 wszystkie źródła na żądanie. Automat nie podaje rutynowych komunikatów NVDA i nie wyrywa fokusu z odtwarzacza; gdy pojawią się nowe odcinki, pasek stanu podaje ich liczbę. Lista kanału pokazuje pierwsze 150 odcinków i pełną liczbę pozycji; Enter na **Załaduj więcej odcinków** dobiera następną porcję. Nie ma różnicy w tym zachowaniu między sesjami Test/Public tam, gdzie Podcasty i YouTube są dostępne.

## Testy i wynik

- Pierwsze `python -m pytest` z katalogu głównego nie zebrało testów z powodu brakującego `PYTHONPATH`; to błąd uruchomienia, nie regresja. Ponowiono z katalogu `wxlite`, zgodnie z położeniem pakietu.
- `python -m pytest -q tests/test_podcast_source.py tests/test_podcast_refresh.py tests/test_general_settings_dialog.py::test_podcast_settings_reach_the_database_owner_and_automatic_refresh` uruchomione z `wxlite` po końcowej poprawce: **41 zaliczonych**, 4,32 s.
- Poszerzony przebieg całego `test_general_settings_dialog.py`: 54 zaliczone, 1 niepowodzenie w niepowiązanym `test_local_resume_setting_reaches_start_save_and_background_checkpoint`. Test oczekuje 7 wystąpień `session_options.configure_resume_preferences(`, podczas gdy wyjściowy commit `ea5a8bd0` już zawierał 8; potwierdzono `git show HEAD`. Nie zmieniano wznowienia ani tego testu w tym zadaniu.
- Prawdziwa baza odczytowo, po zmianie: największa lista RSS 3551 odcinków — mediana trzech otwarć 0,0126 s, 150 widocznych pozycji; największa lista YouTube 558 odcinków — 0,0170 s, także 150 pozycji. Te czasy obejmują metodę Pythona, nie synchronizację wx/NVDA.
- Cichy test startu odizolowanej paczki potwierdził interfejs i host bez otwierania drugiej widocznej instancji. Pełna kontrola integralności paczki zakończyła się pomyślnie. Nie wykonano pełnej regresji ani odbioru NVDA nowej wersji.
- Odczyt uruchomionej alpha.504 przez `Alt+R` pokazał zero trwających nagrań; `Escape` przywrócił poprzednią listę plików lokalnych i fokus. Pasek stanu wskazywał brak odtwarzania. Zapisany prywatny harmonogram miał najbliższy włączony termin 14.11.2026 o 07:13 czasu polskiego; odczyt pliku nie zastępuje żywego statusu hosta.
- Tuż przed planowanym zamknięciem, o 04:32 czasu polskiego, odczyt procesów wykazał dwa `yt-dlp.exe` z katalogu składników AMC. Procesy były krótkotrwałe i zniknęły przy następnym odczycie, ale w chwili decyzji oznaczały aktywną pracę. Nie zamykano AMC, nie instalowano i nie restartowano NVDA. Dla pobierania odcinków bez `yt-dlp` nie ma w tym przebiegu bezpośredniego odczytu flagi w pamięci interfejsu.
- W zaobserwowanej alpha.504 po powrocie z pustego podglądu nagrań lista plików lokalnych i fokus wróciły, lecz tekst pomocniczy listy nadal mówił „zero elementów” mimo sześciu pozycji. To osobna obserwacja dostępności starej instalacji, bez przypisania do tej zmiany.

## Zmienione pliki

- `wxlite/amc_wx_lite/gui.py` — pięciominutowy takt, wstrzymanie automatu przy nagrywaniu i świeży odczyt stanu przed porcją.
- `wxlite/amc_wx_lite/podcast_source.py` — licznik oraz pierwsza strona w jednej migawce, bez okna SQL liczącego każdy wiersz.
- `wxlite/tests/test_podcast_refresh.py` — terminy, aktywne nagrywanie i nieznany status.
- `README_PL.md` — zachowanie automatu i opóźnienia przy dużej bibliotece.
- `docs/zadania/README.md` oraz ten rekord — status, pomiary i decyzje.

## Znane ograniczenia i następny krok

Najważniejsze ograniczenie: źródła są teraz sprawdzane rzadziej. Zysk kosztu pełnego zapisu bazy jest prognozą na podstawie ograniczenia liczby przebiegów; rzeczywisty wynik po instalacji wymaga nowego pomiaru. Skok CPU Pythona z 02:38 nie ma potwierdzonej przyczyny. Rozpoczęty przebieg automatu może jeszcze trwać po rozpoczęciu nowego nagrania. Następny krok: w następnym bezpiecznym oknie ponownie potwierdzić brak nagrywania, pobierania i bliskich harmonogramów, a dopiero potem zainstalować już przygotowany instalator. Po instalacji porównać liczbę prób, I/O hosta, czas listy i fokus NVDA; decyzję o wydaniu podjąć na tych wynikach.

## Wersja i commit

Źródła: gałąź `codex/amc-test-public-20261109`, wyjściowy commit `ea5a8bd0f4dcafee107e395dd2d618d954a348ca`, zmiany kodu `7b4b0f132374984ffce8a998bfb87be08b38f479` i `348023552cb50a49cb17292d1af17efe628d4914`, numer alpha.505 w `c54089fe110c19fab1fe77a8afa6a7ada95b48c9`. Testy: 41 celowanych zaliczonych; dodatkowy niepowiązany test w szerszym zestawie pozostaje niezgodny już na wyjściowym commicie. Build: paczka `publish/bundles/test-podcast-alpha505-20261114` z czystego commita `c54089fe`, identyfikator zawartości `adcd0c55`, 4699 plików, pełna kontrola i cichy test startu zaliczone. Instalator: `publish/test-podcast-alpha505-20261114/AMC-Test-2026-11-14-0420.exe`, 218 448 094 bajty; plik `.integrity.json` potwierdza wersję, profil, commit, identyfikator paczki, skan sekretów i oba dodatki NVDA. Ponowny SHA-256 i rozmiar instalatora zgadzały się z metadanymi. Instalacja: nie wykonano, działające AMC Test z `D:\Projekty Codex\AMC Python` pozostawiono bez zmian z powodu aktywnego `yt-dlp`. Publikacja: nie wykonano.

## Szkic instrukcji i listy zmian do zatwierdzenia

Instrukcja: W Ustawieniach ogólnych otwórz Podcasty i YouTube, aby ustawić odstęp oraz wielkość przebiegu. AMC sprawdza należne źródła co pięć minut, a podczas nagrywania czeka. F5 i Ctrl+F5 pozostają ręcznymi sposobami odświeżenia. Na liście odcinków Enter na „Załaduj więcej odcinków” doładowuje kolejne pozycje.

Lista zmian: Ograniczono częstotliwość automatycznego odświeżania dużej biblioteki i wstrzymano je podczas nagrywania. Pierwsza strona odcinków otwiera się bez liczenia pełnej listy w tym samym zapytaniu. Ten szkic wymaga korekty i zatwierdzenia użytkownika przed publikacją.
