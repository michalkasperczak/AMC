# Radio YouTube — aktualizacja do najbliższej transmisji

## Zachowanie użytkowe

11.11.2026 dodano do kodu AMC Test polecenie **Zaktualizuj transmisję YouTube tej stacji**. W sesji Radio pojawia się ono w menu Radia, menu kontekstowym listy, pełnej pomocy i palecie poleceń tylko dla wybranej stacji z adresem YouTube i przy dostępnym koncie YouTube. Nie ma nowego skrótu. Zapisany link do pojedynczego filmu pozwala silnikowi odczytać jego kanał. AMC wybiera trwającą transmisję; jeżeli żadna nie trwa, najbliższą przyszłą według planowanego czasu. Wybrany publiczny adres filmu zastępuje adres stacji, a ten sam adres trafia do jej powiązanych prywatnych planów nagrywania. Stacja zachowuje nazwę, identyfikator, Ulubione i Bibliotekę. Gdy brak transmisji, nic nie zmienia. NVDA słyszy wynik lub jasny błąd; nie otrzymuje identyfikatorów ani danych protokołu.

## Uzasadnienie i ograniczenia

Stacja „Kramer” zapisana jako jeden URL `watch?v=...` nie przejdzie samoczynnie na kolejne wydarzenie. Dynamiczna stacja z adresem kanału `/live` już przy odtwarzaniu rozwiązuje bieżącą transmisję, ale polecenie pozwala świadomie zapisać konkretną trwającą albo najbliższą zaplanowaną pozycję do nagrania. Jeżeli kanał ma kilka przygotowanych wydarzeń, decyduje najwcześniejszy termin w przyszłości. Po tym wydarzeniu zapisany adres filmu będzie wymagał kolejnej aktualizacji. Źródło danych to istniejący klient YouTube w bezokiennym hoście; token nie opuszcza hosta. Stary adres filmu służy jedynie do ustalenia kanału przez yt-dlp. Funkcja wymaga późniejszej aktualizacji instalacji AMC Test i połączenia konta YouTube. Zachowano zastane, niezwiązane zmiany GUI i rozpoznawania utworów.

## Kod, testy i wydanie

Zmieniono model poleceń, menu, pomoc, paletę, klienta hosta, wybór transmisji i bezokienny host C#. Testy wyboru, menu, pomocy i aktualizacji powiązanych planów: 111 testów i 2 subtesty przeszły. `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj --no-restore -v:q` zakończył się bez ostrzeżeń i błędów. Nie przeprowadzono testu NVDA ani próby podczas rzeczywistej transmisji. Zmiany źródeł zapisano w commicie `5011eb78`. Nie instalowano ani nie publikowano nowego pakietu. Zmiana źródeł i udany build nie są dowodem działania w zainstalowanej wersji.


## Korekta językowa z 11.11.2026

Użytkownik przypomniał, że polecenie ma być spójne w polskim i angielskim menu kontekstowym, palecie poleceń oraz opisie klawiszy. Commit `3fd97a96` dopisuje polskie i angielskie komunikaty wykonania i błędów nowej funkcji oraz test wspólnej etykiety w menu i palecie, obecności w pełnej pomocy i braku fikcyjnego skrótu. Przed rozszerzeniem testu ukierunkowana kontrola menu, pomocy i palety przeszła: 12 testów i 2 subtesty. Po rozszerzeniu testu narzędzie uruchamiania na komputerze nie zwracało wyniku z kilku prób; te próby przerwano i nie są zaliczone. Kontrola NVDA oraz test w zainstalowanej wersji pozostają do wykonania po aktualizacji aplikacji.
