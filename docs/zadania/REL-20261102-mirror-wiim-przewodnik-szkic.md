# Szkic instrukcji i listy zmian — AMC Test alpha.466

Projekt do korekty użytkownika; nie jest opublikowaną instrukcją.

## Jak używać dublowania

1. W sesji Pliki, Radio lub Podcasty uruchom materiał lokalnie.
2. Naciśnij `Shift+M`. Na liście wybierz odbiornik Google Cast i zatwierdź Enterem lub przyciskiem **Zastosuj**.
3. Aby zakończyć dublowanie, ponownie naciśnij `Shift+M` i wybierz **Wyłącz dublowanie**. Dźwięk lokalny będzie nadal grał.

`Shift+C` wybiera samo urządzenie Cast, a `Shift+A` wybiera lokalne wyjście sesji. Przy plikach i podcastach AMC próbuje wyrównać pozycję po buforowaniu Cast; w jednym pokoju może nadal być słychać opóźnienie. AirPlay i dwa lokalne wyjścia nie są w tej wersji. Słyszalne komunikaty NVDA i fokus okna wymagają ręcznego sprawdzenia po instalacji.

## Jak ustawić Enter i presety WiiM

W sesji WiiM otwórz `Ctrl+Alt+Enter`. **Po uruchomieniu materiału Enterem** dotyczy zapisanych strumieni, a **Po uruchomieniu presetu** dotyczy presetów urządzenia, także wywołanych skrótem `Ctrl+Shift+cyfra`. Dla każdego pola wybierz **Jak ustawienie ogólne**, **Otwórz odtwarzacz** albo **Pozostań na liście**. `F6` otwiera odtwarzacz później, kiedy go potrzebujesz.

## Jak powiązać audio i wideo z AMC Test w Windows

1. W AMC Test naciśnij `Ctrl+,` i przejdź do karty **Integracja z Windows**.
2. Włącz **Pokaż AMC Test na liście Otwórz za pomocą i w ustawieniach aplikacji domyślnych Windows**. Zaznacz potrzebne grupy typów plików, na przykład typowe audio i wideo. Możesz też włączyć polecenia **Otwórz w AMC Test** i **Dodaj do kolejki AMC Test** w menu kontekstowym.
3. Zapisz ustawienia. Użyj przycisku **Otwórz ustawienia aplikacji domyślnych Windows** i przypisz AMC Test do wybranych rozszerzeń, na przykład `.mp3` i `.mp4`.

AMC udostępnia się systemowi, ale nie odbiera automatycznie domyślnego programu innym aplikacjom. Otwieranie pliku z Eksploratora przekazuje go do działającego AMC Test. Wybór programu domyślnego jest osobny dla każdego rozszerzenia.

## Szkic listy zmian

- Dodano `Shift+M`: lokalne audio AMC plus jeden odbiornik Google Cast, z ograniczoną korektą pozycji plików i podcastów po buforowaniu.
- Poprawiono wykrywanie Cast na komputerze z wieloma interfejsami sieciowymi.
- Poprawiono Enter na zapisanych strumieniach WiiM i dodano opcję widoku po uruchomieniu natywnego presetu WiiM.
- Budowa AMC Test alpha.466 powstała i została sprawdzona na GitHub Actions. Nie instalowano jej automatycznie na komputerze użytkownika.
