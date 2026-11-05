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

Kandydat `0.1.0-alpha.474` po zbiorczej regresji. Commit, build, instalację i publikację zapisać osobno po ich weryfikacji.
