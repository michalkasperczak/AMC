# UPDATE-20261109 — kod 5 instalatora Huberta i Sygnalista

Stan 9.11.2026: źródła i testy celowane gotowe w kanonicznym repozytorium AMC. Bez nowego instalatora, lokalnej instalacji i wydania GitHub. Bazowy HEAD `19d08fd60da97736daa933a6cfc0d2ae901827a0`; zmiany pozostają niezatwierdzone obok wcześniejszej pracy.

## Zgłoszenie i przyczyna

Hubert opublikował [Issue #14](https://github.com/michalkasperczak/AMC/issues/14) 9.11.2026. Aktualizacja AMC Test zamknęła program i uruchomiła instalator bez widocznego postępu. Po około 50 sekundach Hubert uruchomił AMC z menu Start. Nowy proces zablokował podmianę pliku wxPython, instalator zakończył się kodem 5, a wycofanie zostawiło brakujące pliki. Hubert zamknął AMC i naprawił instalację ręcznym ponowieniem tego samego instalatora. Jest to wynik Huberta, bez reprodukcji na tym komputerze.

## Zachowanie po poprawce źródłowej

- Instalator kopiuje nowy `AMC.exe` przed runtime. Launcher wykrywa mutex aktualizatora właściwego profilu przed odczytem pakietu i nie uruchamia GUI podczas podmiany. Pokazuje: „Trwa aktualizacja AMC Test/Public. Zaczekaj na zakończenie instalacji. Program uruchomi się ponownie automatycznie.” Test i Public mają osobne mutexy.
- Komunikat NVDA przed zamknięciem AMC mówi, że instalacja i sprawdzenie plików mogą potrwać kilka minut i że nie należy uruchamiać programu ręcznie.
- Po cichej instalacji aktualizator sprawdza wszystkie pliki z manifestu: obecność, rozmiar i SHA-256. Brak lub uszkodzenie zatrzymuje automatyczny restart i zachowuje pobrany instalator do naprawy. Komunikat podaje, że instalacja jest niekompletna oraz że po zamknięciu AMC trzeba uruchomić ponownie ten sam instalator. Nie ma automatycznego ponowienia instalacji. Gdy AMC zostało już zamknięte, niepowodzenie cichej aktualizacji otwiera dostępny komunikat błędu; nie zostaje wyłącznie w stderr pomocnika.
- `Ctrl+Alt+Z` otwiera formularz „Zgłoś błąd lub sugestię” z listy i odtwarzacza w każdej dostępnej sesji. Menu Pomoc, F1, Ctrl+F1 i paleta Ctrl+Shift+K korzystają ze wspólnego opisu. W obu profilach są lokalny podgląd, kopiowanie i zapis raportu. Internetowa wysyłka wymaga osobno skonfigurowanego, zweryfikowanego endpointu danego profilu. Test ma zweryfikowany tor testowy; Public pozostaje lokalny do odrębnej aktywacji.

## Weryfikacja i granice

- 43/43 testów C# launchera i aktualizatora. Obejmują oddzielne profile, blokadę startu podczas aktualizacji, wykrycie brakującego pliku i uszkodzenia pliku poza listą startową.
- Rzeczywiste projekty launchera i aktualizatora skompilowane w Debug bez restore: zero ostrzeżeń i błędów.
- Testy aktualizacji, Sygnalisty, menu, skrótów, palety i pakowania: 161/161 w przypiętym Pythonie 3.14 z wxPython 4.3.1. W tej liczbie jest test parytetu menu, palety i skrótu w obu profilach i dostępnych sesjach.
- `git diff --check` bez błędów białych znaków; ostrzeżenia dotyczą tylko konwersji LF/CRLF przy przyszłym zapisie.
- Nie wykonano jeszcze prawdziwej instalacji z wywołanym kodem 5, próby NVDA, pakowania Inno ani instalacji Test/Public. Nowy instalator kopiuje launcher jako pierwszy; bardzo krótki czas przed jego podmianą należy sprawdzić w próbie instalacyjnej.

## Materiał do przewodnika i listy zmian — szkic

Podczas aktualizacji poczekaj na automatyczne ponowne uruchomienie AMC. Jeśli w trakcie instalacji wybierzesz skrót do programu, launcher poinformuje, że instalacja trwa, i nie otworzy częściowo podmienionego programu. „Zgłoś błąd lub sugestię” otwiera `Ctrl+Alt+Z` albo pozycja w menu Pomoc. Raport można przejrzeć przed kopiowaniem, zapisem lub wysyłką dostępną w danym profilu. Szkic wymaga odbioru użytkownika przed publikacją jako przewodnik.
