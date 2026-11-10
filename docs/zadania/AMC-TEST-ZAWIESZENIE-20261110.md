# AMC Test — zawieszenie interfejsu po długiej pracy (10.11.2026)

## Zgłoszenie i stan

Użytkownik zgłosił, że po uruchomieniu AMC Test widzi prawdopodobnie sesję TIDAL, a okno nie odpowiada. Wieczorem 9 listopada nagrywanie i odtwarzanie działały. Nie potwierdzono, że aktywną kartą w chwili zawieszenia był TIDAL; tytuł nieodpowiadającego okna zawierał wcześniejszy utwór „Komary”. Nie ustalono jeszcze przyczyny zawieszenia ani chwili, w której zniknął silnik.

## Sprawdzone fakty

- Instalacja Test: `0.1.0-alpha.488`, manifest źródła `23a1490ef423325f01cf726cc09dbb1c4c1d5496`. Zmiany źródłowe alpha.489, opublikowane wcześniej dla obu profili, były zainstalowane tylko w Public. Zawieszenia Test nie można przypisać bezpośrednio kodowi alpha.489.
- Zawieszona instancja nie była nowym startem: launcher `AMC.exe` PID 41324 działał od 9.11, 22:49:59, a GUI `runtime\python.exe` PID 38484 od 22:50:00. Ponowne kliknięcie uruchomienia przywoływało tę instancję.
- GUI miało `Responding=False`, stały licznik CPU, wszystkie sprawdzone wątki w oczekiwaniu. Proces `amc_lite_host.exe` tej instancji już nie działał. Brak procesu `ffmpeg.exe` lub innego procesu nagrywania.
- Ostatni zapis `frontend.log`: 10.11, 01:04:16. Ostatnie wpisy dotyczyły nieudanych odświeżeń trzech źródeł podcastów YouTube, bez zarejestrowanej awarii TIDAL lub końca hosta. Wspólny `amc.log` zawiera również wpisy z krótkich testów startu innych buildów; nie wolno przypisywać ich bez ustalenia PID instancji Test.
- Windows Application nie zgłosił pasującej awarii `python.exe`, `amc_lite_host.exe` lub AMC w sprawdzonych 5 godzinach. Brak zdarzenia wyczerpania zasobów ID 2004 w System; w chwili sprawdzenia wolne było ok. 12 GB RAM z 32 GB.
- Pliki `*.amc-partial` w `recording-staging` miały ostatni zapis 9.11, 20:26:13 lub wcześniej, przed startem tej instancji. Pozostawiono je bez zmian.
- Po próbie zwykłego zamknięcia nieodpowiadającego GUI zatrzymano **tylko PID 38484**. Launcher zapisał kod `-1`, co jest skutkiem wymuszonego zatrzymania, a nie pierwotnym kodem awarii. Pokazał modalną „Diagnostykę AMC Test”, którą następnie zamknięto zwykłą drogą.
- O 10.11, 02:14 uruchomiono ponownie zainstalowany Test alpha.488. Nowe GUI PID 10972 i host PID 43912 wystartowały; GUI `Responding=True`. O 02:17 launcher zapisał zwykły kod zakończenia interfejsu `0`. Późniejszy odczyt logu instalatora wykazał, że aktualizator zamknął tę instancję, zainstalował alpha.489 i uruchomił Test ponownie o 02:18. Nie przeprowadzono próby odtwarzania ani nagrywania.

## Dodatkowy błąd wykryty w diagnostyce

`launcher.log` po zatrzymaniu GUI zawiera wielokrotne `Unrecognized accel key 'strzałka w prawo', accel string ignored.`. W `gui.py` `_menu_shortcut_text` tłumaczy `Right` na `strzałka w prawo`, a `_menu_native_label` używa tego tekstu po tabulatorze również dla pozycji z akceleratorem, gdzie wx oczekuje technicznej nazwy klawisza. To może wyłączyć skróty tych pozycji. Nie ma dowodu, że wywołuje wielogodzinne zawieszenie. Wymaga osobnej poprawki i testu parytetu menu/pomocy/palety/NVDA.

## Dalsza analiza

1. Dodać diagnostykę następnego zawieszenia, która zapisze stosy wątków Pythona poza UI i rozróżni normalny koniec hosta, jego błąd oraz zewnętrzne zakończenie. Nie gromadzić zrzutu pamięci zawierającego dane kont bez potrzeby.
2. Przejrzeć wywołania synchroniczne GUI i sekwencję kończenia `LiteHostClient` oraz miejsca, gdzie zdarzenie hosta trafia do `wx.CallAfter`. Sprawdzić, czy utrata hosta może pozostawić zamek lub oczekiwanie bez limitu.
3. Odtworzyć długą pracę i przełączanie TIDAL/innych sesji na profilu izolowanym. Nie uruchamiać równoległego procesu na profilu użytkownika ani nie naruszać nagrań.
4. Naprawić nieprawidłowy podpis akceleratora, zachowując polski tekst NVDA oraz techniczny klawisz wx. Sprawdzić menu, skrót, pełną pomoc, `Ctrl+F1`, paletę i oba profile.
5. Po ustaleniu przyczyny uruchomić testy celowane, build i osobno zweryfikować instalację. Do tej chwili nie przedstawiać ponownego uruchomienia jako naprawy.

## Stan repozytorium i limit

Kanoniczne repo: `D:\Projekty Codex\AMC - integracja wxPython`, gałąź `codex/amc-test-public-20261109`, HEAD `aa98351a`, drzewo czyste przed tym zapisem. W chwili diagnostyki limit Codex: 82% wykorzystane, 18% pozostało; przewidywane odnowienie 11.11.2026 o 07:45 czasu Warszawy, dostępne dwa kredyty pełnego resetu, niewykorzystane. Przy rezerwie poniżej 20% nie rozpoczynać opcjonalnej szerokiej regresji bez nowego odczytu limitu.

## Status dostarczenia

Analiza potwierdziła, że Test alpha.488 uruchomił się ponownie i odpowiadał, a następnie zakończył się z kodem 0 w ramach automatycznej aktualizacji do alpha.489. Samą aktualizację i jej osobny błąd opisuje `AMC-INSTALLER-UPDATE-GATE-20261110.md`. Pierwotna przyczyna wielogodzinnego zawieszenia alpha.488 pozostaje nieustalona. Użytkownik 10.11 poinformował, że zamierza wyłączyć komputer, a do głębszej analizy wrócimy następnego dnia.
