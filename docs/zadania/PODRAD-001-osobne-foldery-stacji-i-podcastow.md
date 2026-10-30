# PODRAD-001 — osobne foldery stacji i podcastów

Status: **W trakcie**. Kod i testy celowane ukończone 30.10.2026. Instalacja i próba z NVDA pozostają do wykonania.

## Cel i wynik

Na prośbę użytkownika z 30.10.2026 dodano dwa niezależne przełączniki w oknie Ustawienia: Radio i Podcasty i YouTube. Oba są domyślnie wyłączone, co zachowuje dotychczasowy zapis do folderów bazowych. Włączenie opcji Radia zapisuje ręczne nagrania w podfolderze nazwanym stacją; włączenie opcji Podcastów umieszcza odcinki pobierane przez Ctrl+D w podfolderze nazwanym podcastem lub kanałem. Jedno zbiorcze Ctrl+D może utworzyć kilka podfolderów.

## Materiał do dokumentacji użytkownika

W **Ustawieniach, Radio**, po wyborze folderu nagrań jest pole wyboru **Twórz osobny folder dla każdej stacji radiowej**. W **Ustawieniach, Podcasty i YouTube**, po wyborze folderu pobierania jest pole **Twórz osobny folder dla każdego podcastu lub kanału**. NVDA powinien odczytać pełną nazwę każdego pola i stan „zaznaczone” albo „niezaznaczone”. Spacja zmienia stan, Enter zapisuje ustawienia, a Anuluj lub Escape odrzuca zmianę. Po zamknięciu dialogu fokus wraca do poprzedniego miejsca.

Gdy pole pozostaje niezaznaczone, pliki nadal trafiają bezpośrednio do wybranego folderu bazowego. Po zaznaczeniu Radio tworzy podfolder stacji podczas rozpoczęcia ręcznego nagrywania, a Podcasty tworzą podfolder źródła przy pobieraniu odcinka przez Ctrl+D. Ustawienia są niezależne także wtedy, gdy Radio i Podcasty używają wspólnego folderu bazowego. Istniejący własny folder przypisany pojedynczej stacji ma pierwszeństwo i nie dostaje dodatkowego podfolderu. Odcinki już pobrane nie są przenoszone; Ctrl+S zapisuje pod ręcznie wskazaną ścieżką.

W AMC Test i AMC Public reguła jest taka sama; funkcja nie wymaga nowego skrótu ani zmiany komendy Ctrl+D. Angielskie etykiety pól są uwzględnione w katalogu tłumaczeń.

## Uzasadnienie i decyzje

- 30.10.2026: zachowano wspólny folder jako zachowanie domyślne, zgodnie z wyraźną preferencją użytkownika. Opcje są osobne i wyłączone przy pierwszym uruchomieniu.
- Podfoldery otrzymują bezpieczną nazwę z nazwy stacji albo źródła. Nazwy zewnętrznych źródeł nie są używane jako surowa ścieżka. Gdy nazwa źródła jest pusta, używana jest nazwa zastępcza.
- Indywidualny folder stacji jest dokładniejszym ustawieniem i zachowuje pierwszeństwo. Zaplanowane nagrania mają własne ustawienia folderu; ten etap nie zmienia istniejących planów.

## Zmienne pliki

`wxlite/amc_wx_lite/{gui,host_client,i18n,radio_recording,state_store}.py`; `src/AccessibleMediaController.Core/{Configuration/PodcastLibraryDatabase.cs,Configuration/PodcastLibraryMutationStore.cs,Podcasts/PodcastDownloadNaming.cs}`; `src/AccessibleMediaController.LiteHost/{LitePodcastDownloadCoordinator.cs,LiteRadioRecordingCoordinator.cs}`; celowane testy `wxlite/tests/test_{audio_preferences,general_settings_dialog,host_client,podcast_download,podcast_download_host_e2e,radio_recording}.py` i `tests/AccessibleMediaController.Core.SmokeTests/Program.cs`.

## Testy i wynik

- Przypięte Python 3.14 i wxPython 4.3.1, pytest bez GUI: 96/96 testów preferencji, Radia, pobierania i klienta hosta zaliczonych.
- Test struktury okna ustawień: 17/17 zaliczonych.
- `dotnet build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj -c Release --no-restore --disable-build-servers -v:q`: sukces, 0 ostrzeżeń, 0 błędów.
- Rzeczywisty LiteHost i lokalny serwer HTTP: 1/1 test zaliczony; jeden odcinek zapisany do folderu bazowego przy wyłączonej opcji i do nazwanego podfolderu przy włączonej.
- `dotnet run --project tests\AccessibleMediaController.Core.SmokeTests\AccessibleMediaController.Core.SmokeTests.csproj -c Release --no-restore --no-launch-profile -- --podcast-download-naming`: sukces, kod wyjścia 0.
- Składnia Pythona sprawdzona przez `ast.parse`; `git diff --check` bez błędów.

## Znane ograniczenia

- Brak ręcznej próby NVDA i odbioru na zainstalowanym AMC. Testy automatyczne sprawdzają etykiety i kolejność pól, lecz nie potwierdzają rzeczywistej mowy czytnika.
- Opcja Radia obejmuje ręczne nagrania; harmonogramy zachowują indywidualnie zapisane foldery. Pliki istniejące wcześniej nie są przenoszone.
- Nie utworzono instalatora ani wydania, nie zaktualizowano stałej instalacji użytkownika. W repozytorium są także zastane, niezwiązane zmiany robocze.

## Wersja i commit

Gałąź `codex/konfigurowalne-skroty-20261026`, baza `89a0dcbf178f16f5c1624a1476134c8404dfeb95`. Zmiany tego zadania pozostają niezatwierdzone w Git; nie przypisano im numeru wersji ani nowego commitu.
