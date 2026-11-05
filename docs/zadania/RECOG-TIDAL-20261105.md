# RECOG-TIDAL-20261105 — kontekst albumu i domyślne Enter TIDAL

Status: kod i testy celowane gotowe 5 listopada 2026; budowa, instalacja i ręczny test NVDA oczekują na koniec nagrań.

## Korekta użytkownika

5 listopada 2026 użytkownik zauważył, że album otwarty z rozpoznanego utworu przez Ctrl+Alt+S wraca po Backspace do globalnego widoku albumów, pomijając albumy rozpoznanego wykonawcy. Chce łatwo wrócić do właściwego albumu i zapisać go. Ta korekta zastępuje wcześniejsze uznanie bezpośredniego otwarcia albumu za kompletną ścieżkę nawigacji. Tego samego dnia użytkownik zmienił decyzję o Enter w TIDAL: domyślnie ma grać w AMC, a przekazanie do oryginalnego programu ma być osobną akcją.

## Materiał do dokumentacji użytkownika

- W rozpoznanych utworach Ctrl+Alt+S wybierz „Pokaż album w AMC” dla Spotify lub TIDAL. Gdy widzisz utwory albumu, Ctrl+Shift+L zapisuje właśnie otwarty album do Biblioteki. Jeden Backspace wraca do albumów rozpoznanego wykonawcy i wskazuje właściwy album; Ctrl+Shift+L działa także na tej pozycji. Następny Backspace wraca do wcześniejszego widoku.
- Enter na utworze TIDAL uruchamia wewnętrzny odtwarzacz AMC Test z najwyższą dostępną jakością, do Max. Dotyczy również utworu wybranego z wyników wyszukiwania i zwykłej listy. Odtwarzanie kolejnego utworu korzysta z bieżącej załadowanej listy.
- W menu Biblioteka i menu kontekstowym utworu pozostaje jawne polecenie „Przekaż utwór do oryginalnego TIDALa”, bez skrótu. Działa wtedy, gdy serwis podał album utworu. Polecenia „Odtwórz utwór TIDAL w AMC” można nadal użyć jawnie; Enter daje ten sam rezultat.
- Menu, paleta Ctrl+Shift+K, pomoc Ctrl+F1 i pełna pomoc nazywają obie drogi. AMC Public nie zawiera sesji TIDAL.

## Uzasadnienie i decyzje

Dotychczasowa ścieżka rozpoznania otwierała album bezpośrednio i zapamiętywała w historii niepowiązany korzeń sesji. Teraz najpierw ładowana jest sekcja albumów wykonawcy, po czym konkretny album. Jeśli rozpoznany album nie leży na pierwszej stronie sekcji, jest dodawany do tej migawki i zaznaczany, aby Backspace nadal prowadził do miejsca zapisu. Gdy usługa nie poda identyfikatora wykonawcy, program otwiera album bez tego dodatkowego kontekstu. Ta ścieżka pobiera jeden dodatkowy kontener przy wejściu, nigdy przy poruszaniu się strzałkami.

Wewnętrzny tor TIDAL jest już dostępny w AMC Test; zatem domyślne Enter nie musi wymagać identyfikatora albumu, potrzebnego tylko oryginalnej aplikacji. Stara droga pozostaje jawna i odrębna. Rzeczywisty start nadal wymaga potwierdzenia odtwarzacza; błąd przywraca poprzedni stan.

## Testy i wynik

Testy nawigacji rozpoznanego albumu Spotify i TIDAL, powrotu o jeden i dwa poziomy, wskazania albumu spoza pierwszej strony, celu zapisu Ctrl+Shift+L, Enter bez albumu, zachowania źródła podczas skoku, jawnego przekazania do TIDAL, menu, palety, pomocy i braku kolizji klawiszy dostępu: 231 zaliczonych. Dodatkowe 74 testy rozpoznawania, natywnego TIDAL i przewodów GUI Spotify po dopięciu ścieżki awaryjnej również zaliczone. Próba uruchomienia całego katalogu testów w systemowym Pythonie została zatrzymana po wykryciu błędów atrap środowiska; pierwszy odtworzony błąd dotyczy `wx.GetTopLevelWindows`, którego atrapa `test_gui_logic` nie udostępnia. W tym przebiegu do pierwszego błędu przeszło 107 testów. Nie jest to dowód pełnego zaliczenia ani regresji omawianych funkcji. Odbiór NVDA pozostaje do wykonania. Prawdziwego konta TIDAL nie uruchamiano podczas nagrań.

## Znane ograniczenia

Jeżeli wykonawca nie ma identyfikatora katalogowego albo lista jego albumów jest niedostępna, program otwiera sam album bez kontekstowego rodzica. Pierwsze wejście z rozpoznania może wykonać dwa żądania sieciowe: listy albumów wykonawcy i samego albumu. Przy wolnym łączu opóźnienie początkowe może być odczuwalne; strzałki po wczytaniu korzystają z gotowych danych. Ręczne NVDA musi sprawdzić fokus, Enter, Backspace i ponowne otwarcie.

## Wersja i commit

Planowany wspólny kandydat AMC Test 0.1.0-alpha.480. Commit kodu i testów: f5a709ec. Budowa: niewykonana. Instalacja: niewykonana. Publikacja: niewykonana.
