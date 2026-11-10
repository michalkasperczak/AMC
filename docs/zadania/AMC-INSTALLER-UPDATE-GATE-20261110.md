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
- Paczki alpha.490 z czystego commitu `ac606f04d5a1f12ef297b10c7dc225aa94f5c7d6`: Public 3149 plików, Test 3456 plików. Obie przeszły pełną kontrolę manifestu oraz kontrolowany start GUI i hosta w izolacji. Dwie wcześniejsze próby builda zostały poprawnie odrzucone przez skrypt z powodu niewłaściwego runtime (z już obecnym TIDAL oraz ze zbyt starymi bibliotekami wspólnymi); z nich nie powstało wydanie.
- Próba na rzeczywistych paczkach przy celowo zajętym mutexie aktualizatora: w obu profilach zwykła pełna kontrola zwróciła 4, wewnętrzna pełna kontrola instalatora 0, a wewnętrzny cichy start 0. To bezpośrednio odtwarza wcześniej wadliwy warunek. Nie przeprowadzono ponownej automatycznej aktualizacji całej działającej instalacji przez sam aktualizator.
- Instalator Public `publish/public-alpha490-20261110/AMC-Public-2026-11-10-0235.exe`: 133906176 B, SHA-256 `b35fe4e598b9250bdfdfa781385bb4036e23501f52943004e96e3f379275660d`. Instalator Test `publish/test-alpha490-20261110/AMC-Test-2026-11-10-0239.exe`: 136468438 B, SHA-256 `9c8da7b73ce13d55dcd86000b07bfd5e571742ac36a05edff4822482bdff5712`. Kontrola sekretów obu artefaktów zaliczona; Test ma ten sam dodatek Quick App Switcher 0.1.6, co poprzednie wydanie (zgodna SHA-256).
- Na wyraźną zgodę użytkownika zainstalowano obie paczki. Public: kod instalatora 0, log Inno `Setup Log 2026-11-10 #002.txt` bez wyjątku końcowej kontroli, manifest `public`, wersja alpha.490 i pełna kontrola instalacji 0. Test: po potwierdzeniu braku nagrywania poprzednia instancja zamknęła się zwykłą drogą; instalator kod 0, log `Setup Log 2026-11-10 #003.txt` bez wyjątku, manifest `test`, wersja alpha.490 i pełna kontrola instalacji 0. Nowy Test uruchomiono z katalogu instalacji, GUI i host odpowiadały. Public ma potwierdzony cichy start w buildzie i instalatorze, lecz nie otwierano jego okna użytkowego.
- Źródła wypchnięto na GitHub. Wydanie [Public alpha.490](https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.10.0235) oraz [Test alpha.490](https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.10.0239) opublikowano; skrypt odczytowo potwierdził nazwę, rozmiar i stan `uploaded` trzech zasobów każdego wydania.
- Nie testowano żywego odczytu NVDA ani rzeczywistego odtwarzania/nagrywania po instalacji. Wcześniejsze wielogodzinne zawieszenie Test alpha.488 nadal nie ma ustalonej przyczyny.

## Wersja i dalszy krok

Źródła na gałęzi `codex/amc-test-public-20261109`, poprawka `642caafc`, wersja i paczki z commitu `ac606f04d5a1f12ef297b10c7dc225aa94f5c7d6`. Przy końcowym odczycie limit Codex: 84% wykorzystane, 16% pozostało; odnowienie przewidywane 11.11.2026 o 07:45 w Warszawie, dwa kredyty pełnego resetu niewykorzystane. Dalsza analiza wcześniejszego zawieszenia jest zapisana w `AMC-TEST-ZAWIESZENIE-20261110.md` i nie powinna być ogłaszana jako naprawiona.

## Roboczy wpis do instrukcji i changelogu

- Instrukcja aktualizacji, szkic do korekty: Po pobraniu aktualizacji AMC bezpiecznie zamknie działające okno, sprawdzi pliki i cichy start, a następnie uruchomi program. Podczas nagrywania aplikacja może odmówić zamknięcia; wtedy aktualizacja czeka na zakończenie pracy.
- Changelog, szkic do korekty: Usunięto fałszywy komunikat o nieudanej kontroli integralności podczas automatycznej aktualizacji Test i Public. Potwierdzono poprawne działanie kontroli przy zajętej blokadzie aktualizatora. Przyczyna osobnego zawieszenia Test pozostaje w badaniu.
