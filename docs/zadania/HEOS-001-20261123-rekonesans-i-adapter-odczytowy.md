# HEOS-001 — rozpoznanie urządzenia HEOS 200 i adapter odczytowy

**Status: W trakcie; adapter LAN, model źródeł i nawigacji gotowe, sesja AMC Test jeszcze niepodłączona.**

## Cel i zakres

Wprowadzić HEOS do AMC Test według `D:\Projekty Codex\Hermes\Nowe\HEOS-20261122-integracja.md`. Pierwsza bramka to próba odczytowa na wskazanym przez Michała urządzeniu „HEOS 200” (dokładny model do potwierdzenia), a następnie sprawdzony adapter, sesja i dostępny interfejs. HEOS nie trafia jeszcze do AMC Public.

## Stan i wykonane pliki

- Izolowana gałąź HEOS: `codex/heos-next-test-20261123`, oparta na commicie `eabaa0e8` trwającej aktualizacji, w worktree `D:\Projekty Codex\AMC - integracja wxPython\work\HEOS-001-next-test`. Główny checkout na D i worktree bieżącego wydania mają odrębne niezapisane zmiany; nie zostały naruszone.
- `wxlite/amc_wx_lite/heos_client.py`: klient CLI, wykrywanie SSDP i ręczny adres IP, bezpieczny odczyt, stronicowanie, jawne polecenia transportu/głośności/wyciszenia. Odpowiedź spóźniona po limicie czasu nie trafia do następnego żądania; mutacji nie ponawia automatycznie. Klasa odczytowa nadal odrzuca polecenia zapisu.
- `wxlite/tests/test_heos_client.py`: izolowane testy protokołu, wykrywania, stronicowania i braku ponowienia mutacji.
- `wxlite/amc_wx_lite/heos_source.py` i `wxlite/tests/test_heos_source.py`: osobne tożsamości i etykiety urządzeń, grup, źródeł, pozycji katalogu i kolejki oraz stanu; testy duplikatów tytułów, zagnieżdżonego źródła i braku wycieku identyfikatorów do mowy.
- `wxlite/amc_wx_lite/heos_navigation.py` i `wxlite/tests/test_heos_navigation.py`: stan poziomów katalogu, powrót do zaznaczenia oraz odrzucanie spóźnionych list po zmianie widoku lub urządzenia.
- `wxlite/amc_wx_lite/heos_service.py` i `wxlite/tests/test_heos_service.py`: operacje wykonywane przez przyszły wątek roboczy; odczyt celu, katalogu, kolejki i stanu oraz osobne wywołania sterowania. Stacja korzysta z polecenia strumienia, utwór z kolejki z identyfikatorem katalogu nadrzędnego, a album z własnym kontenerem.
- Adapter nie jest jeszcze wpięty w sesję, interfejs, pakiet ani instalację.

## Próba sieciowa

Pierwsze próby SSDP 23.11.2026 nie przyniosły odpowiedzi, ponieważ środowisko odmówiło dostępu do lokalnej sieci. Późniejsza próba z dostępem sieciowym wykryła urządzenie i potwierdziła działanie CLI; szczegóły w korekcie poniżej. Nie wysłano komend zmieniających stan.

## Uzasadnienie i decyzje

- Próba odczytowa przed sterowaniem wynika z notatki integracyjnej. Najpierw sprawdzić model, firmware, graczy, grupy, konto, źródła, znaną kolekcję i kolejkę. Bez danych użytkownika w udostępnianych raportach.
- Klient odczytowy nie przyjmuje komend zapisu; osobny klient sterowania ma jawne metody, lecz nie uruchomiono ich na urządzeniu. Nie ma logowania do konta HEOS w AMC ani zależności `pyheos`: własny adapter działa na Denon Home 200 bez dodatkowej biblioteki.
- Brak wykrycia SSDP nie zamyka pracy: po uruchomieniu urządzenia można powtórzyć wykrycie albo użyć jego lokalnego adresu IP.

## Testy i wynik

- `D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe -m pytest wxlite\tests\test_heos_client.py wxlite\tests\test_heos_source.py wxlite\tests\test_heos_navigation.py wxlite\tests\test_heos_service.py -q` z `PYTHONPATH=wxlite`: **41 passed in 0.97s** (ostatnia próba 23.11.2026).
- Testy automatyczne są bezokienkowe i używają atrap. Osobna próba odczytowa na Denon Home 200 potwierdziła wykrywanie, model celu, strukturę źródeł, stronicowanie i model stanu. Nie potwierdza sterowania, dźwięku, interfejsu ani NVDA.
- Nowa warstwa operacji przeszła 23.11.2026 odczyt na Denon Home 200: jeden cel, siedem źródeł, znana głośność i tytuł statusu, pusta kolejka; pięć źródeł zwróciło stronę katalogu, dwa nie udostępniły listy. To stan tej próby, a nie stała liczba źródeł. Nie wysłano żadnej komendy sterowania.
- Nie wykonano kompilacji, instalacji ani publikacji.

## Znane ograniczenia

- Odczyt CLI na Denon Home 200 działa; zgodność poleceń sterowania i zachowanie po utracie połączenia pozostają do próby.
- Brak sesji HEOS i czynności użytkowych w GUI. Odczytowy adapter jest etapem wewnętrznym.
- Stronicowanie katalogu i format bieżącej odpowiedzi sprawdzono odczytowo na Denon Home 200; dalsza zgodność z innymi źródłami i firmware wymaga prób.

## Wersja i commit

Baza aktualizacji `eabaa0e8`; wcześniejszy klient odczytowy `99e602ec6532`, adapter i model `2c7cc6f086f8`, komendy kolejki `00b23968`, nawigacja `996a074e`, warstwa operacji `b6f42b1b`. Żadna wersja AMC z HEOS nie powstała.

## Materiał do dokumentacji użytkownika

HEOS nie jest jeszcze dostępny w AMC Test. Wykryto Denon Home 200 i odczytano jego stan bez wpływu na graną stację. Planowana sesja pozwoli wybrać urządzenie i sterować nim z klawiatury; dźwięk pozostanie na urządzeniu. Biblioteka pokaże tylko źródła naprawdę dostępne przez CLI. Komendy i komunikaty NVDA zostaną opisane po przygotowaniu interfejsu.

## Następny krok

Wpiąć HEOS do AMC Test na podstawie potwierdzonego odczytu: wybór celu, stan odtwarzacza, dostępne źródła, Biblioteka i kolejka. Rozróżnić źródło bez listy od pustej listy oraz odświeżać zmienną listę źródeł. Zaktualizować wspólny model poleceń, menu, pomoc, paletę, testy parytetu i dokumentację. Po przygotowaniu sprawdzalnego interfejsu osobno uzgodnić fizyczną próbę sterowania.

## Korekta i wynik próby sprzętowej — 23.11.2026

Michał uruchomił głośnik i wznowił zadanie tego samego dnia. Wcześniejsze „nie wykryto urządzenia” było wynikiem odmowy dostępu do sieci przez piaskownicę (Windows 10013), a nie dowodem uśpienia lub niezgodności głośnika. Ta korekta zastępuje wcześniejsze przypuszczenie o uśpieniu. Po uruchomieniu próby z dostępem do lokalnej sieci SSDP wykrył jedno urządzenie HEOS; CLI na porcie 1255 odpowiada.

Odczyt potwierdził model **Denon Home 200**, firmware **3.139.175**, jednego odtwarzacza i brak grup. `system/check_account` zwrócił `success`, co potwierdza wykonanie komendy, lecz samo nie dowodzi zalogowania konta. Odczyt stanu, bieżących metadanych, głośności i wyciszenia się powiódł; podczas próby grała stacja. Kolejka zwróciła zero pozycji. Nie zapisano tytułów, nazw kont, identyfikatorów urządzenia ani adresu sieciowego w tym raporcie.

Lista źródeł w kolejnych odczytach miała 5, a następnie 8 pozycji. Po ustabilizowaniu pojawiły się trzy źródła `music_service`, jedno `heos_server` i cztery `heos_service`. `Local Music` zwróciło pustą listę. Dwa serwisy muzyczne zwróciły katalogi, a Historia dwie pozycje typu kontener, z których jedna była oznaczona jako odtwarzalna. Trzy źródła zwróciły `success` bez pola `payload` i bez liczników `returned`/`count`; obecny klient zgłasza dla nich `HeosProtocolError`, co jest uczciwsze od uznania ich za puste. Nie stwierdzono obecności DLNA ani USB. Nie uruchomiono żadnej pozycji.

Próba była wyłącznie odczytowa. Kod klienta i 8 testów na atrapach pozostają w commitcie `99e602ec6532`. Nie wykonano testu sterowania, interfejsu, NVDA, kompilacji, instalacji ani publikacji. Następny krok: zaprojektować adapter źródeł z osobnym stanem źródła bez listy, odświeżaniem źródeł i rozróżnieniem stacji od kolejki; następnie wpiąć HEOS wyłącznie do AMC Test, z pełnym parytetem poleceń i dostępności. Fizyczną próbę zmieniającą odtwarzanie uzgodnić osobno po przygotowaniu interfejsu.

## Decyzja Michała z 23.11.2026 — następna wersja testowa

Michał zezwolił rozwijać HEOS równolegle do właśnie składanej aktualizacji AMC. HEOS ma trafić do testów dopiero w **następnej** wersji AMC Test, po zakończeniu bieżącego wydania. Ta decyzja zastępuje wcześniejsze prowadzenie HEOS na gałęzi kandydata do bieżącej aktualizacji. Prace Apple Music mogą zostać dołączone do następnego Test po osobnej weryfikacji; nie są częścią zadania HEOS. Kod HEOS jest izolowany na własnej gałęzi. Nie skompilowano ani nie opublikowano wersji z HEOS na GitHubie.

Nowy adapter wykrył Denon Home 200 i odczytał jednego gracza. Model źródeł odczytał siedem dostępnych pozycji; źródło zagnieżdżone występujące na urządzeniu ma własny `sid` bez `cid` i otrzymało osobną tożsamość w modelu do przyszłej nawigacji. Źródła zwracające sukces bez listy pozostają stanem niedostępnej listy, a nie pustą kolekcją. W działającym radiu model stanu rozpoznał typ stacji oraz znaną głośność i stan wyciszenia. Nie wysłano mutacji do urządzenia.

## Uzupełnienie z 23.11.2026 — kolejka i nawigacja

Wcześniejsze zdanie, że brak `payload` kończy się `HeosProtocolError`, opisuje stan z pierwszej próby i zostało zastąpione: aktualny klient zgłasza `HeosListUnavailable`, odrębne od pustej listy. Odczyt stronicowany i model stanu sprawdzono też na rzeczywistym Denon Home 200. Nie odtworzono żadnej pozycji.

Dodano jawne polecenia dodania do kolejki w czterech różnych trybach; zastąpienie kolejki wymaga osobnego potwierdzenia wywołującego. Uruchomienie pozycji o przesuwającym się `qid` wymaga ponownego odczytu i zgodności odcisku pełnej kolejki. Radio korzysta z odrębnego polecenia stacji. Testy tych poleceń korzystały wyłącznie z atrap, więc nie dowodzą zgodności sterowania z fizycznym głośnikiem. Nowy model nawigacji odrzuca wyniki poprzedniej listy lub poprzedniego celu oraz przywraca identyfikator zaznaczonego kontenera po Backspace. Tekst dla NVDA pozostaje nazwą użytkową, bez technicznych identyfikatorów.

Nie wykonano testów GUI/NVDA, kompilacji, instalacji ani publikacji. Następnym etapem jest podłączenie sesji AMC Test wraz ze wspólnym routingiem, menu, pomocą i paletą poleceń. Dopiero po gotowym interfejsie trzeba uzgodnić fizyczne próby zmieniające odtwarzanie i kolejkę.
