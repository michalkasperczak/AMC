# AMC — fałszywy błąd integralności podczas automatycznej aktualizacji

## Objaw i ustalenie

10.11.2026 o 02:17 aktualizator AMC Test zamknął działającą instancję alpha.488 kodem 0, uruchomił `AMC-Test-2026-11-10-0107.exe` w trybie cichym i ponownie wystartował Test alpha.489 o 02:18. Log Inno Setup zawiera `Installation process succeeded`, a następnie wyjątek `Pełna kontrola integralności zainstalowanego pakietu nie powiodła się`. Użytkownik widział niespodziewaną instalację i ponowne uruchomienie.

Wszystkie 3456 plików zainstalowanego Test miały po aktualizacji rozmiary i SHA-256 zgodne z manifestem; `AMC.exe --sprawdz-pakiet-pelny-cicho` zwrócił 0. Test alpha.489 odpowiadał i miał uruchomiony host. Nie jest to dowód poprawnego odtwarzania ani wyjaśnienie wcześniejszego zawieszenia alpha.488.

## Przyczyna w kodzie

`AMC-Python-Updater.exe` utrzymuje mutex profilu przez `RunInstaller`. W `ssPostInstall` instalator uruchamiał `AMC.exe --sprawdz-pakiet-pelny-cicho`, a następnie `--sprawdz-start-cicho`. Launcher przed weryfikacją sprawdza ten sam mutex i zwraca kod 4 (`UpdateInProgress`), więc pierwsza kontrola instalatora deterministycznie kończyła się fałszywym błędem. Aktualizator później sam sprawdza pliki, ale wcześniej zgłoszony błąd instalatora jest mylący i pomija jego kontrolowany start. Ścieżka dotyczy obu profili, Test i Public.

## Poprawka źródłowa

Instalator używa teraz dwóch osobnych, cichych poleceń samokontroli. Launcher dopuszcza je podczas zajętej blokady aktualizatora; zwykły start i publiczne polecenia diagnostyczne nadal czekają na koniec aktualizacji. Cichy start wewnątrz instalatora przekazuje frontendowi jego dotychczasowy argument. Nie zmienia to danych profilu ani ścieżki ręcznego uruchomienia.

## Sprawdzenia i granice wyniku

- 44 testy `AccessibleMediaController.PythonLauncher.Tests` przeszły, w tym nowy test ograniczenia wyjątku do dwóch poleceń instalatora.
- Test `AccessibleMediaController.Core.SmokeTests --installer-offline` przeszedł dla kontraktu instalatora.
- Weryfikacja zainstalowanego Test alpha.489: wszystkie 3456 plików zgodne z manifestem oraz kod 0 pełnej kontroli launchera.
- Build nowych paczek, instalacja, test z prawdziwym zajętym mutexem, GitHub i żywa próba NVDA: do odnotowania po wykonaniu. Tych wyników nie wolno utożsamiać z powyższymi testami źródłowymi.

## Wersja i dalszy krok

Źródła na gałęzi `codex/amc-test-public-20261109`; przed zmianą HEAD `aa98351a`. Po commicie uzupełnić jego identyfikator, wersję, wyniki budowy Test/Public i linki do GitHub. Nie instalować nowej paczki Test podczas nagrywania. Odrębnie badać wcześniejsze zawieszenie z `AMC-TEST-ZAWIESZENIE-20261110.md`.
