# HEOS-001 — rozpoznanie urządzenia HEOS 200 i adapter odczytowy

**Status: W trakcie; próba odczytowa na urządzeniu wykonana 23.11.2026.**

## Cel i zakres

Wprowadzić HEOS do AMC Test według `D:\Projekty Codex\Hermes\Nowe\HEOS-20261122-integracja.md`. Pierwsza bramka to próba odczytowa na wskazanym przez Michała urządzeniu „HEOS 200” (dokładny model do potwierdzenia), a następnie sprawdzony adapter, sesja i dostępny interfejs. HEOS nie trafia jeszcze do AMC Public.

## Stan i wykonane pliki

- Aktywna gałąź: `codex/alpha512-update-candidate-20261122` w worktree `C:\Users\micha\.codex\worktrees\amc-live-radio-recording-20261122\AMC - integracja wxPython`, bazowy commit `d4ec91a4a880e7fbec46cd5cd07bd71419b8cbf6`. Główny checkout na D ma osobne niezatwierdzone dokumenty i nie został zmieniony.
- `wxlite/amc_wx_lite/heos_client.py`: wstępny, wyłącznie odczytowy klient CLI. Wymaga lokalnego adresu IP, ogranicza polecenia do odczytu, rozpoznaje zdarzenia pomiędzy odpowiedziami, odpowiedź pośrednią, fragmentację linii, błąd i limit czasu. Po błędzie zamyka połączenie, by spóźniona odpowiedź nie mogła trafić do kolejnego żądania. Stronicuje katalog i kolejkę bez deduplikowania tytułów.
- `wxlite/tests/test_heos_client.py`: izolowane testy protokołu z atrapą gniazda.
- Adapter nie jest jeszcze wpięty w sesję, interfejs, pakiet ani instalację.

## Próba sieciowa

Pierwsze próby SSDP 23.11.2026 nie przyniosły odpowiedzi, ponieważ środowisko odmówiło dostępu do lokalnej sieci. Późniejsza próba z dostępem sieciowym wykryła urządzenie i potwierdziła działanie CLI; szczegóły w korekcie poniżej. Nie wysłano komend zmieniających stan.

## Uzasadnienie i decyzje

- Próba odczytowa przed sterowaniem wynika z notatki integracyjnej. Najpierw sprawdzić model, firmware, graczy, grupy, konto, źródła, znaną kolekcję i kolejkę. Bez danych użytkownika w udostępnianych raportach.
- Wstępny klient nie przyjmuje komend zapisu i nie ma żadnego logowania do konta HEOS. Nie dodano zależności `pyheos`; zgodność biblioteki z runtime AMC i urządzeniem nie została sprawdzona.
- Brak wykrycia SSDP nie zamyka pracy: po uruchomieniu urządzenia można powtórzyć wykrycie albo użyć jego lokalnego adresu IP.

## Testy i wynik

- `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe -m pytest wxlite\tests\test_heos_client.py -q` z `PYTHONPATH=wxlite`: **8 passed in 0.17s**.
- To testy bezokienkowe na atrapach. Nie potwierdzają zgodności z Denon Home 200, dźwięku, interfejsu ani NVDA.
- Nie wykonano kompilacji, instalacji ani publikacji.

## Znane ograniczenia

- Odczyt CLI na Denon Home 200 działa; zgodność poleceń sterowania i zachowanie po utracie połączenia pozostają do próby.
- Brak sesji HEOS i czynności użytkowych w GUI. Odczytowy adapter jest etapem wewnętrznym.
- Paginacja i format odpowiedzi wymagają jeszcze sprawdzenia na obecnym firmware Denon Home 200.

## Wersja i commit

Baza `d4ec91a4`; kod i testy HEOS: commit `99e602ec6532`. Żadna wersja AMC z HEOS nie powstała.

## Materiał do dokumentacji użytkownika

HEOS nie jest jeszcze dostępny w AMC Test. Wykryto Denon Home 200 i odczytano jego stan bez wpływu na graną stację. Planowana sesja pozwoli wybrać urządzenie i sterować nim z klawiatury; dźwięk pozostanie na urządzeniu. Biblioteka pokaże tylko źródła naprawdę dostępne przez CLI. Komendy i komunikaty NVDA zostaną opisane po przygotowaniu interfejsu.

## Następny krok

Wpiąć HEOS do AMC Test na podstawie potwierdzonego odczytu: wybór celu, stan odtwarzacza, dostępne źródła, Biblioteka i kolejka. Rozróżnić źródło bez listy od pustej listy oraz odświeżać zmienną listę źródeł. Zaktualizować wspólny model poleceń, menu, pomoc, paletę, testy parytetu i dokumentację. Po przygotowaniu sprawdzalnego interfejsu osobno uzgodnić fizyczną próbę sterowania.

## Korekta i wynik próby sprzętowej — 23.11.2026

Michał uruchomił głośnik i wznowił zadanie tego samego dnia. Wcześniejsze „nie wykryto urządzenia” było wynikiem odmowy dostępu do sieci przez piaskownicę (Windows 10013), a nie dowodem uśpienia lub niezgodności głośnika. Ta korekta zastępuje wcześniejsze przypuszczenie o uśpieniu. Po uruchomieniu próby z dostępem do lokalnej sieci SSDP wykrył jedno urządzenie HEOS; CLI na porcie 1255 odpowiada.

Odczyt potwierdził model **Denon Home 200**, firmware **3.139.175**, jednego odtwarzacza i brak grup. `system/check_account` zwrócił `success`, co potwierdza wykonanie komendy, lecz samo nie dowodzi zalogowania konta. Odczyt stanu, bieżących metadanych, głośności i wyciszenia się powiódł; podczas próby grała stacja. Kolejka zwróciła zero pozycji. Nie zapisano tytułów, nazw kont, identyfikatorów urządzenia ani adresu sieciowego w tym raporcie.

Lista źródeł w kolejnych odczytach miała 5, a następnie 8 pozycji. Po ustabilizowaniu pojawiły się trzy źródła `music_service`, jedno `heos_server` i cztery `heos_service`. `Local Music` zwróciło pustą listę. Dwa serwisy muzyczne zwróciły katalogi, a Historia dwie pozycje typu kontener, z których jedna była oznaczona jako odtwarzalna. Trzy źródła zwróciły `success` bez pola `payload` i bez liczników `returned`/`count`; obecny klient zgłasza dla nich `HeosProtocolError`, co jest uczciwsze od uznania ich za puste. Nie stwierdzono obecności DLNA ani USB. Nie uruchomiono żadnej pozycji.

Próba była wyłącznie odczytowa. Kod klienta i 8 testów na atrapach pozostają w commitcie `99e602ec6532`. Nie wykonano testu sterowania, interfejsu, NVDA, kompilacji, instalacji ani publikacji. Następny krok: zaprojektować adapter źródeł z osobnym stanem źródła bez listy, odświeżaniem źródeł i rozróżnieniem stacji od kolejki; następnie wpiąć HEOS wyłącznie do AMC Test, z pełnym parytetem poleceń i dostępności. Fizyczną próbę zmieniającą odtwarzanie uzgodnić osobno po przygotowaniu interfejsu.
