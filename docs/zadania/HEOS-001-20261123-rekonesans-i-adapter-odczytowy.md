# HEOS-001 — rozpoznanie urządzenia HEOS 200 i adapter odczytowy

**Status: W trakcie; przerwane na prośbę Michała 23.11.2026, powrót następnego dnia.**

## Cel i zakres

Wprowadzić HEOS do AMC Test według `D:\Projekty Codex\Hermes\Nowe\HEOS-20261122-integracja.md`. Pierwsza bramka to próba odczytowa na wskazanym przez Michała urządzeniu „HEOS 200” (dokładny model do potwierdzenia), a następnie sprawdzony adapter, sesja i dostępny interfejs. HEOS nie trafia jeszcze do AMC Public.

## Stan i wykonane pliki

- Aktywna gałąź: `codex/alpha512-update-candidate-20261122` w worktree `C:\Users\micha\.codex\worktrees\amc-live-radio-recording-20261122\AMC - integracja wxPython`, bazowy commit `d4ec91a4a880e7fbec46cd5cd07bd71419b8cbf6`. Główny checkout na D ma osobne niezatwierdzone dokumenty i nie został zmieniony.
- `wxlite/amc_wx_lite/heos_client.py`: wstępny, wyłącznie odczytowy klient CLI. Wymaga lokalnego adresu IP, ogranicza polecenia do odczytu, rozpoznaje zdarzenia pomiędzy odpowiedziami, odpowiedź pośrednią, fragmentację linii, błąd i limit czasu. Po błędzie zamyka połączenie, by spóźniona odpowiedź nie mogła trafić do kolejnego żądania. Stronicuje katalog i kolejkę bez deduplikowania tytułów.
- `wxlite/tests/test_heos_client.py`: izolowane testy protokołu z atrapą gniazda.
- Adapter nie jest jeszcze wpięty w sesję, interfejs, pakiet ani instalację.

## Próba sieciowa

23.11.2026 z komputera w lokalnej sieci wysłano odczytowe M-SEARCH SSDP z oficjalnym typem `urn:schemas-denon-com:device:ACT-Denon:1` oraz zapytania `ssdp:all` i `MediaRenderer:1`. Nie było odpowiedzi HEOS. Nie próbowano zgadywać adresu, łączyć się z portem 1255 ani wysyłać komend zmieniających stan. Michał wskazał urządzenie „HEOS 200” i możliwość jego uśpienia; zdecydował wrócić do próby następnego dnia. Brak odpowiedzi SSDP nie dowodzi niezgodności urządzenia. Ręczny adres IP pozostaje poprawną drogą.

## Uzasadnienie i decyzje

- Próba odczytowa przed sterowaniem wynika z notatki integracyjnej. Najpierw sprawdzić model, firmware, graczy, grupy, konto, źródła, znaną kolekcję i kolejkę. Bez danych użytkownika w udostępnianych raportach.
- Wstępny klient nie przyjmuje komend zapisu i nie ma żadnego logowania do konta HEOS. Nie dodano zależności `pyheos`; zgodność biblioteki z runtime AMC i urządzeniem nie została sprawdzona.
- Brak wykrycia SSDP nie zamyka pracy: po uruchomieniu urządzenia można powtórzyć wykrycie albo użyć jego lokalnego adresu IP.

## Testy i wynik

- `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe -m pytest wxlite\tests\test_heos_client.py -q` z `PYTHONPATH=wxlite`: **8 passed in 0.17s**.
- To testy bezokienkowe na atrapach. Nie potwierdzają zgodności z Denon Home 200, dźwięku, interfejsu ani NVDA.
- Nie wykonano kompilacji, instalacji ani publikacji.

## Znane ograniczenia

- Brak odpowiedzi urządzenia i brak zweryfikowanego IP. Nie wykonano próby prawdziwego CLI. Dokładny model wymaga potwierdzenia.
- Brak sesji HEOS i czynności użytkowych w GUI. Odczytowy adapter jest etapem wewnętrznym.
- Paginacja i format odpowiedzi wymagają jeszcze sprawdzenia na obecnym firmware Denon Home 200.

## Wersja i commit

Baza `d4ec91a4`; kod i testy HEOS: commit `99e602ec6532`. Żadna wersja AMC z HEOS nie powstała.

## Materiał do dokumentacji użytkownika

HEOS nie jest jeszcze dostępny w AMC Test. Planowana sesja będzie sterować wybranym urządzeniem Denon lub Marantz, a dźwięk pozostanie na urządzeniu. Nie trzeba dziś wybudzać sprzętu ani zmieniać jego ustawień. Po wznowieniu sprawdzimy odczytowo urządzenie „HEOS 200”. Jeżeli wyszukiwanie go nie pokaże, można podać jego adres IP z sieci lokalnej.

## Następny krok

Gdy urządzenie „HEOS 200” będzie dostępne, powtórzyć SSDP lub użyć podanego przez Michała adresu IP. Wykonać wyłącznie odczyt `get_players`, `get_groups`, `check_account`, stanu, `get_music_sources`, znanej kolekcji i kolejki. Zapisać dokładny model, firmware i faktyczne możliwości bez prywatnych nazw/ID. Dopiero po tej bramce dodać sesję AMC Test i osobno uzgodnić próbę zmieniającą odtwarzanie.
