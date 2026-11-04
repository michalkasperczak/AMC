# TID-001 — Pełne odtwarzanie TIDAL w odtwarzaczu AMC Test

## Status i decyzja użytkownika

W trakcie, od 4 listopada 2026. Użytkownik prosi o odwracalny eksperyment z alternatywnymi bibliotekami i nieoficjalnym torem odtwarzania, nawet jeśli nie jest autoryzowany przez TIDAL. Chce zachować bieżący dostęp do katalogu i Biblioteki oraz istniejące przekazanie do oryginalnej aplikacji jako punkt powrotu. Ta decyzja zastępuje dla **prywatnego laboratorium AMC Test** wcześniejszą decyzję STR-001, która wykluczała próby odtwarzania poza oficjalnym Playerem. Nie upoważnia do publikacji niezweryfikowanego rozwiązania w AMC Public ani do obchodzenia DRM.

## Cel i granice

Pełny utwór subskrybenta powinien grać w istniejącym odtwarzaczu AMC Test: pauza, wznowienie, przewijanie, następny/poprzedni utwór, kolejka i czytelny stan NVDA. Katalog, Biblioteka, playlisty i ich obecne logowanie pozostają osobnym torem. Funkcja eksperymentalna ma być wyłączona domyślnie i możliwa do wycofania bez utraty danych konta, list lub ustawień. Nie przechowywać tokenów ani podpisanych adresów audio w logach, protokole do GUI, historii, kolejkach i dokumentacji.

## Etapy i bramki

1. Zabezpieczyć punkt powrotu: obecna gałąź `codex/konfigurowalne-skroty-20261026`, HEAD `5c666b31`; zidentyfikować istniejące zmiany, instalację i procesy. Nie zastępować instalacji podczas prób źródłowych.
2. Dodać izolowaną diagnostykę jednego utworu: czy **obecny** token AMC ma prawo do pełnego strumienia. Odpowiedź ujawnia tylko status i bezpieczny kod wyniku. Testy bez konta przez fałszywy serwer HTTP.
3. Jeżeli token nie ma uprawnień, rozważyć osobne logowanie odtwarzania w odizolowanym magazynie poświadczeń; nie rozszerzać obecnych zakresów OAuth, ponieważ próba `r_usr` z 11.09.2026 zrywała całe logowanie. Żadne drugie logowanie nie może popsuć katalogu.
4. Po pozytywnym wyniku pełnego, niezaszyfrowanego strumienia podłączyć osobny resolver do istniejącego `WindowsMediaOutput`; zachować tylko ID utworu w kolejce i pobierać krótko ważny adres na moment odtwarzania. Dla DRM lub odmowy zgłaszać błąd i zachować tor oryginalnej aplikacji, bez prób odszyfrowania.
5. Dodać przełącznik w AMC Test, wspólny model poleceń, menu, Ctrl+F1, pełną pomoc, Ctrl+Shift+K, instrukcję i testy parytetu. Następnie test klawiatury i NVDA: fokus, pauza, przewijanie, kolejka, koniec utworu, ponowne otwarcie. AMC Public pozostaje bez eksperymentu do osobnego odbioru.
6. Dopiero po realnym teście pełnego utworu, braku wycieków, regresji i odbiorze NVDA zdecydować o budowie, instalacji i ewentualnym wydaniu. W razie porażki wyłączyć adapter i wrócić do oryginalnego TIDALa.

## Materiał do dokumentacji użytkownika

Na obecnym etapie sposób odtwarzania TIDAL w AMC Test się nie zmienił. Analizujemy możliwość pełnego odtwarzania w odtwarzaczu AMC; nie ma jeszcze nowego skrótu, menu ani komunikatu NVDA. Dotychczasowe przekazanie do oryginalnego TIDALa pozostaje dostępne.

## Uzasadnienie i decyzje

- `tidalapi` jest biblioteką Python, ale jej wewnętrzne API prawdopodobnie wymaga innego logowania niż katalog AMC. Nie dodawać zależności, dopóki nie ma potwierdzonego pełnego strumienia.
- Tid3 i Riptide dowodzą koncepcji natywnego odtwarzania przez starsze prywatne API. Ich licencje AGPL/GPL nie pozwalają traktować kodu jako dowolnego fragmentu do skopiowania do AMC. Projektować własny, mały adapter.
- Oficjalny Player SDK ma potrzebne operacje, ale test AMC we wrześniu dał próbkę i `FULL_REQUIRES_SUBSCRIPTION`; nie utożsamiać tego z błędem dostępu raportowanym przez innych. Embed ma ograniczony publiczny mostek poleceń. TIDAL Hi-Fi na Windows raportuje problem DRM.
- `WindowsMediaOutput` ma już obsługę zdalnego HTTP i seek, ale loguje źródło; ochronę diagnostyki wykonać przed rzeczywistym audio TIDAL.

## Testy i wynik

Na początku etapu: przegląd źródeł AMC oraz projektów GitHub. Brak próby z kontem, odtwarzania, buildu, instalacji, publikacji i testu NVDA. Wyniki kodu i testów dopisać po ich zakończeniu.

## Znane ograniczenia

Działanie obecnego tokenu w starszym API jest niepotwierdzone. Możliwe są drugie logowanie, zmiany protokołu, wygasanie adresów i odmowa/DRM. Nie twierdzić, że test z fałszywym serwerem dowodzi pełnego odtwarzania TIDAL.

## Wersja i commit

Brak nowej wersji AMC, instalacji i wydania. Commit eksperymentu: do uzupełnienia.

## Wynik etapu 1 — 4 listopada 2026

### Korekta użytkownika z 4 listopada 2026

W dawnym eksperymencie TIDAL program sygnalizował odtwarzanie, lecz nie było słychać dźwięku; później wprowadzono przekazanie do oryginalnej aplikacji. Ta informacja **zastępuje** każde wcześniejsze kryterium, które uznawałoby status `PLAYING`, manifest lub ruch licznika za dowód sukcesu. Warunek odbioru przyszłego backendu: użytkownik rzeczywiście słyszy pełny utwór przez wybrane wyjście audio w odtwarzaczu AMC, a następnie potwierdza pauzę, wznowienie, seek, następny/poprzedni i komunikaty NVDA. Bez tego eksperyment pozostaje niedziałający.

### Materiał do dokumentacji użytkownika

Na obecnym etapie nie zmieniono sposobu odtwarzania ani skrótów AMC. Dodano tylko niewidoczną w GUI operację diagnostyczną. Konto do katalogu pozostaje dotychczasowe, a testowy login audio ma osobny, jednorazowy kod. Oryginalny TIDAL pozostaje działającym sposobem odtwarzania.

### Uzasadnienie i decyzje

- Próbnik `tidal.playbackProbe` użył istniejącego logowania AMC i dla jednego utworu otrzymał HTTP 403. To nie ustala dokładnej przyczyny odmowy i nie dowodzi, że konto użytkownika nie ma subskrypcji. Nie zmieniono obecnego OAuth ani jego zakresów.
- Izolowany test .NET 8/HTTP z syntetycznym FLAC: `WindowsMediaOutput` i jego MediaFoundationReader nie odczytały sieciowego FLAC (COM E_ABORT), chociaż lokalny FLAC i kontrolne HTTP MP3 działały. Istniejący `FfmpegLocalAudioWaveStream.TryOpenNetwork` odczytał ten sam sieciowy FLAC i przewinął do około 6,043 s. Przy późniejszym pilocie rzeczywistego audio FLAC trzeba wybrać ten tor z potwierdzonym czasem trwania; samo istniejące `media.play` nie wystarcza.
- `tidalapi==0.8.11` zainstalowano wyłącznie w `work/TID-001-venv` (Python 3.14.2), bez zmiany globalnego Pythona, instalacji AMC i pliku zależności aplikacji. Narzędzie jednorazowego logowania nie zapisuje sesji. Próba na koncie oczekuje na działanie użytkownika lub wynik kodu.

### Testy i wynik

- `dotnet run --no-restore --project tests/AccessibleMediaController.LiteHost.ProtocolTests -c Release -- --tidal-playback-probe`: 1/1 celowany zestaw zaliczony.
- `dotnet build src/AccessibleMediaController.LiteHost -c Release --no-restore`: powodzenie, 0 błędów i ostrzeżeń (przed końcowym przeglądem).
- `dotnet run --no-restore --project tests/AccessibleMediaController.Windows.SmokeTests -c Release -- --diagnostic-redaction`: powodzenie. Test obejmuje URL w ścieżce/query/fragmencie oraz treść wyjątku, z zachowaniem lokalnej ścieżki.
- `python -m unittest wxlite.tests.test_tidalapi_device_probe -v`: 8/8 testów bez sieci zaliczonych na Pythonie 3.14.2.
- Test FLAC opisany wyżej używał syntetycznego pliku i wyłącznie 127.0.0.1; nie jest dowodem odtwarzania TIDAL ani odsłuchu z wyjścia audio.
- `git diff --check` pierwszego etapu: bez błędów. Kod próbnika i redakcji zapisany lokalnie w commicie `a2fd6ca7`. Testowego narzędzia Python jeszcze nie commitowano.

### Znane ograniczenia

Nie ma jeszcze pełnego strumienia TIDAL w AMC, rzeczywistego odsłuchu, testu NVDA ani nowej instalacji. HTTP 403 na obecnym tokenie wymaga osobnej diagnostyki logowania audio. `tidalapi` korzysta z nieoficjalnego interfejsu, którego działanie i uprawnienia mogą się zmienić.

### Dwie pierwsze próby logowania 4 listopada 2026

Dwa jednorazowe kody urządzenia wygasły przed zatwierdzeniem. Użytkownik wyjaśnił, że najpierw musi przejść logowanie TIDAL przez Google. W tych dwóch próbach nie otrzymano tokenu z `tidalapi`, nie sprawdzono nim manifestu i nie wykonano odtwarzania. Kolejny kod należało wygenerować dopiero, gdy użytkownik będzie przy polu kodu. Żadnego kodu jednorazowego ani danych konta nie zapisano w tym pliku. Ta informacja doprecyzowuje wcześniejszy zapis o próbie oczekującej.

### Wynik osobnego logowania 4 listopada 2026

Po przejściu logowania TIDAL przez Google i zatwierdzeniu kodu urządzenia `tidalapi==0.8.11` otrzymało odpowiedź HTTP 200 z `assetPresentation=FULL` i poprawnie zakodowanym manifestem jednego utworu. Narzędzie zwróciło wyłącznie `full_manifest`, kod HTTP i wartość logiczną; token, treść manifestu oraz adres audio nie trafiły do odpowiedzi ani dokumentacji. Poprzedni HTTP 403 dotyczył **istniejącego tokenu katalogu AMC**, a nie konta abonenta jako takiego. Ten wynik potwierdza osobną ścieżkę uprawnienia do pełnego manifestu, lecz nie potwierdza jeszcze pobrania danych audio, ich dekodowania, słyszalności w AMC ani przewijania. Sesja testowa była tylko w pamięci i zniknęła po zakończeniu procesu; kolejna próba audio będzie wymagała nowego kodu, dopóki nie powstanie bezpieczny magazyn tej osobnej sesji.
