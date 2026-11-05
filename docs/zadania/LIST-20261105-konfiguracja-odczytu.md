# LIST-20261105 — konfigurowalny odczyt list

Status: **W trakcie**. Data: 5 listopada 2026. Użytkownik potwierdził, że po alpha.472 listy działają wyraźnie szybciej. Nowe ustawienia nie mogą przywrócić sieci ani bazy do obsługi każdego ruchu kursora.

## Zakres i korekty użytkownika

Pierwsza decyzja z 5 listopada: lewa strzałka na lokalnych plikach, także nagraniach, ma podawać najpierw długość, następnie bitrate i rozmiar. Góra/dół oraz lewa strzałka mają osobne wybory odczytywanych pól globalnie w karcie komunikatów i w opcjach sesji `Ctrl+Alt+Enter`, z możliwością dziedziczenia ustawień ogólnych. Użytkownik doprecyzował tego samego dnia, że mechanizm musi obejmować także Sonos, WiiM i Frontier Smart oraz dawać prostą drogę dodawania kolejnych sesji. Kolejna korekta z 5 listopada zastępuje wcześniejszy zamiar mówienia w TIDAL o niedostępnym formacie: nieobecnych parametrów nie wymieniać osobno. Dla dodanych strumieni Sonos i WiiM dopuszczony jest pomiar bitrate i kodeka na żądanie lewą strzałką. Ostatnie doprecyzowanie z 5 listopada zastępuje wcześniejszą decyzję o ogólnym komunikacie „Brak wybranych/dodatkowych informacji”: jeżeli wybranych danych nie ma, lewa strzałka zachowuje ciszę; nie powtarza komunikatu o braku parametru.

## Materiał do dokumentacji użytkownika

- `Ctrl+,` → karta **Komunikaty dostępności** → sekcje **Odczyt listy: strzałki góra i dół** i **Odczyt listy: strzałka w lewo**. Każde pole ma osobny przełącznik; to samo pole wolno włączyć w obu sekcjach. Nazwa pozycji jest zawsze czytana na liście.
- `Ctrl+Alt+Enter` → **Ustawienia sesji** → **Zasady odczytu listy**: „Jak ustawienie ogólne” albo „Własne ustawienia tej sesji”. Po wyborze własnych ustawień dostępne są dwie grupy pól. Okno pokazuje tylko pola lewej strzałki możliwe dla tej sesji. Zapis działa od razu; Anuluj nie zapisuje. Fokus wraca do poprzedniego widoku.
- Góra/dół czyta wyłącznie dane już obecne w wierszu. Przełączniki obejmują rodzaj, wykonawcę, długość, inne szczegóły i stan. Nie są wykonywane dodatkowe zapytania do sieci ani bazy przy każdym ruchu kursora.
- Lewa strzałka czyta wybrane dane na żądanie. W lokalnym pliku kolejność to długość, bitrate, rozmiar, format, częstotliwość i pozostałe dostępne informacje. Na TIDAL katalog podaje długość i wykonawcę; brak formatu nie jest oznajmiany. W Spotify podawane są dostępne czas, wykonawca i inne szczegóły. Radio Archiwista podaje długość nagrania bez słowa „Czas”. Dla własnego strumienia WiiM lub Sonos aplikacja może zmierzyć bitrate i kodek; gdy ich brak, pomija je. Frontier Smart pokazuje wyłącznie opis udostępniony na liście urządzeń.
- Gdy żadne pole nie jest wybrane albo wybranych parametrów nie ma, lewa strzałka nie dodaje komunikatu. Jeśli dostępna jest tylko część parametrów, czyta wyłącznie tę część.

## Uzasadnienie i decyzje

Wspólny katalog pól i możliwości każdej sesji mieszka w `wxlite/amc_wx_lite/list_reading.py`. Nowa sesja dopisuje swój zakres w tym jednym miejscu i używa tych samych ustawień globalnych oraz sesyjnych. Model listy składa wypowiedź z gotowego `Row`; `MediaListCtrl` aktualizuje tekst przy zmianie wiersza lub preferencji, a nie przy każdej strzałce. Host C# formatuje lewą strzałkę i otwiera metadane tylko dla wybranych brakujących pól. Dawny formatter Core zachowuje semantykę historycznego AMC; nowa ścieżka wybranych pól dotyczy bieżącego interfejsu wxPython.

Zmiany obejmują `list_reading.py`, `list_model.py`, `list_sync.py`, `gui.py`, `state_store.py`, lokalne źródła listy, `radio_recording.py`, `speaker_content.py`, `spotify_source.py`, `quick_info.py`, formatter Core i host Lite. Opisy `Ctrl+,`, `Ctrl+Alt+Enter` i lewej strzałki pochodzą ze wspólnego katalogu `shortcuts.py`, używanego przez menu, pomoc i paletę.

## Testy i wynik

Regresja Python zmienionych modułów: 438 testów zaliczonych przed ostatnim doprecyzowaniem. Po zmianie pomijania pustych informacji: 114 testów Python i dwa zestawy C# `--quickinfo --transport-routing` zaliczone; zestaw przewodu hosta wymagał aktualizacji oczekiwania na pustą wypowiedź i ponownego uruchomienia. LiteHost Release wcześniej zbudowany bez ostrzeżeń i błędów. Rzeczywiste okno wxPython utworzono i zamknięto dla ośmiu sesji: wysokość 650 px, dla Plików obszar wirtualny 740 px i widoczny 564 px, więc sekcja rzeczywiście się przewija. Nie uruchamiano w tej próbie normalnej instancji AMC ani nagrywania. Paczka, instalacja, wydanie GitHub i żywy odbiór NVDA wymagają osobnych wyników i zostaną dopisane po wykonaniu.

## Znane ograniczenia

Góra/dół nie odczyta bitrate ani rozmiaru, gdy nie ma ich w gotowym wierszu; dostępne są na żądanie pod lewą strzałką. Część urządzeń i katalogów nie podaje parametrów audio. Nowych kontrolek nie sprawdzono jeszcze na żywym NVDA; testy atrap i kodu nie są dowodem mowy czytnika. Spotify Lossless przez wewnętrzny tor nie jest częścią tej zmiany i pozostaje osobnym zadaniem.

## Wersja i commit

Kandydat `0.1.0-alpha.474` zainstalowano 5 listopada o 15:35 z commitu `085d5b804bb348f87b64a48f5328150d5adef162`. Użytkownik zgłosił tego dnia regresję szybkości lewo/prawo w odtwarzaczu i wejścia do historii nagrań `Alt+Shift+R`; ten odbiór zastępuje wcześniejszą ocenę, że nawigacja jest wystarczająco szybka. Alpha.474 nie opublikowano.

## Korekta wydajności po odbiorze alpha.474 — 5 listopada 2026

### Materiał do dokumentacji użytkownika

`Alt+Shift+R` otwiera historię nagrywania z bieżącego hosta i zapamiętanych wpisów. Lista nadal pokazuje do 100 najnowszych dostępnych nagrań; otwarcie nie odczytuje ponownie całego profilu Radia, a sprawdzanie plików kończy się po znalezieniu 100 wierszy. Lewa i prawa strzałka w odtwarzaczu nadal przewijają utwór o dotychczasowy krok. Automatyczne odświeżanie podcastów nadal działa, lecz jego odczyty liczby nowych odcinków i wybór źródeł nie wczytują całej biblioteki odcinków.

### Uzasadnienie i decyzje

Profil działającej alpha.474 wskazał niemal pełne wykorzystanie rdzenia przez LiteHost, kiedy co minutę odświeżał 5 źródeł podcastów z biblioteki 90 546 odcinków. Wybranie źródeł i odczyt licznika ładowały całą bazę; zastąpiono je wąskimi zapytaniami SQLite. Automatyczne odświeżanie ma niższy priorytet wątku. `Alt+Shift+R` wcześniej ponownie odczytywało profil Radia około 12,6 MB oraz sprawdzało więcej ścieżek niż wyświetla; korzysta z bieżącego stanu i pamięci wyników sprawdzeń. Formatowanie trzech kolumn listy wykonuje się raz na wiersz zamiast trzy razy.

### Testy i wynik

Przed zbudowaniem nowej paczki: 50 testów Python zmienionych list i nagrań zaliczonych; testy podcastów Core i LiteHost oraz kompilacja LiteHost Release zaliczone. Syntetyczne formatowanie 50 tys. wierszy skróciło się z 0,766 s do 0,339 s. Rzeczywisty pomiar odczuć z NVDA i odsłuchu po instalacji wymagają odbioru użytkownika.

### Znane ograniczenia

Sam zapis odświeżonego źródła podcastów nadal używa pełnej operacji biblioteki, aby zachować kolejność i stan odcinków. Niższy priorytet ogranicza wpływ na interfejs, ale dużą bibliotekę trzeba sprawdzić w działającej instalacji. Poprawka nie zmienia sieciowego czasu otwarcia nowego widoku TIDAL lub Spotify.

### Wersja i commit

`0.1.0-alpha.475` z commitu `09c526479c567b3343b2cdb8485c5f68727aed05` zbudowano i zainstalowano 5 listopada o 20:40. Instalator: `publish/installers/AMC-Test-2026-11-05-2040.exe`, SHA-256 `133a1d84facf8ae85aeacab1cb9c2df5bd5acab7548d155c262bd454494bbdb9`. Kontrolowany start paczki i jej integralność przeszły, instalator zwrócił kod 0, tytuł działającego okna i manifest wskazują alpha.475 oraz ten commit. Dodatkowa pełna kontrola zainstalowanych plików utknęła bez zużycia CPU i została zakończona jako osobny proces diagnostyczny; nie należy jej mylić z zaliczonym sprawdzeniem paczki. Lokalny test otwarcia `Alt+Shift+R` po ponownym wywołaniu trwał około sekundy, a lista była wypełniona. W 5-sekundowym oknie automatycznego odświeżania host nadal zużył 3,05 s CPU. Dlatego alpha.475 pozostaje kandydatem i nie jest publikowana.

Kolejny kandydat `0.1.0-alpha.476` ogranicza pełną kontrolę integralności bazy podcastów do pierwszego odczytu i odstępów co najmniej 10 minut; na bieżącej bibliotece samo `quick_check` zajmuje 0,517 s, więc powtarzanie go dla każdego z pięciu źródeł było zbędnym kosztem. Wynik testów, commit, budowę i instalację alpha.476 trzeba zapisać osobno.
