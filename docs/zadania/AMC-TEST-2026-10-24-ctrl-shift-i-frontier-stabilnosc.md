# AMC Test — zbiorcze transmisje YouTube i stabilność Frontier Smart

- Data: 24 października 2026
- Status: zbudowane, atomowo zainstalowane i uruchomione; automatyczne bramki
  zaliczone, czeka na ręczny odbiór NVDA, YouTube i Teufela
- Gałąź: `integration/amc-test-20261023-late`
- Baza przed zmianą: `044c9a5ce5146145c9429eb9755ccaf7bed6dd5f`
- AMC Public i GitHub: bez zmian

## Zachowanie użytkowe

`Ctrl+Shift+I` otwiera zbiorczą listę transmisji trwających i zaplanowanych z
wybranych kanałów YouTube. Pierwsze użycie bez wyboru otwiera dostępne okno
`Kanały zbiorczej listy transmisji`. Użytkownik może wybrać do 20 kanałów.
Polecenie występuje w tym samym modelu wykonania, menu Widok, menu
kontekstowym, palecie `Ctrl+Shift+K`, pomocy `Ctrl+F1` i pełnej pomocy.

Na liście działają Enter, `Ctrl+Shift+C`, `F5`, `Ctrl+R`, `Shift+R`,
Backspace i Escape. Nagranie bieżącej transmisji używa jej adresu, a
harmonogram stabilnego adresu kanału `/live`. Lista nie działa w tle i nie
skanuje wszystkich subskrypcji.

Frontier Smart scala szybką serię regulacji głośności. Wysyła bieżącą wartość
i najwyżej jedną, najnowszą wartość oczekującą. Timer odczytu stanu nie nakłada
się na trwające polecenie. Zmiana powstała po rzeczywistym odbiorze, w którym
radio wykonywało dwuminutową kolejkę starych żądań i stało się chwilowo
niesterowalne.

## Komunikaty i dostępność

- `Najpierw wybierz kanały zbiorczej listy transmisji` prowadzi do selektora.
- Okno wyboru mówi wyłącznie nazwy kanałów, liczbę wybranych i limit 20.
- Wynik podaje liczbę transmisji na żywo, zaplanowanych oraz ewentualnych
  kanałów, których nie udało się sprawdzić.
- Próba `Ctrl+R` na transmisji przyszłej mówi, że nie jest jeszcze na żywo.
- Nakładające się polecenie Frontier Smart mówi:
  `Poprzednie polecenie Frontier Smart jeszcze trwa`.
- Po błędzie głośności suwak wraca do ostatniej potwierdzonej wartości.

## Różnice trybów i ograniczenia

- Zbiorcza lista wymaga skonfigurowanego konta YouTube w profilu AMC Test.
- Wybrane kanały są prywatnym ustawieniem AMC Test i częścią pełnej kopii
  danych; token Google pozostaje w hoście C#.
- Limit 20 i jawne uruchomienie chronią dzienny limit oficjalnego API.
- Trwałe cotygodniowe nagranie nadal najlepiej planować na dynamicznej stacji
  Radia; `Shift+R` z listy służy także pojedynczym zaplanowanym wydarzeniom.
- Stabilność Frontier wymaga ponownego testu na prawdziwym Teufelu; test
  automatyczny potwierdza algorytm, ale nie zachowanie firmware.

## Korekta po rzeczywistym odbiorze NVDA — 24.10.2026

Wcześniejszy opis zakładał, że po pierwszym `Ctrl+Shift+I`, wybraniu
kanałów i zatwierdzeniu selektora wynik będzie od razu jednoznacznie
widoczny. Rzeczywisty odbiór użytkownika wykazał, że lista pojawiła się
dopiero po dłuższym oczekiwaniu i sprawiała wrażenie uruchomionej dopiero
ponownym `Ctrl+Shift+I`. Kod rozpoczyna skan automatycznie po zapisie, ale
sprawdza kanały kolejno i po zamknięciu selektora nie przekazuje dostatecznie
czytelnego, trwałego stanu oczekiwania. Ponowne naciśnięcie skrótu nie jest
warunkiem pobrania i może rozpocząć drugi skan.

Do dalszej poprawy pozostaje natychmiastowy, dostępny komunikat lub wiersz
`Trwa sprawdzanie kanałów`, końcowy komunikat po zapełnieniu listy oraz
ochrona przed ponownym uruchomieniem tego samego skanu. Wybór kanałów można
już zmieniać przez `Pliki` → `Wybierz kanały zbiorczej listy transmisji…`
albo pojedynczo na kanale poleceniem `Uwzględniaj kanał na zbiorczej liście
transmisji`.

Potwierdzono także zapis jednorazowego harmonogramu dla `TVP Info — YouTube na
żywo` na 24.10.2026, godz. 17:00 czasu polskiego, przez 30 minut. Harmonogram
przechowuje dynamiczny adres kanału `/live`, a nie adres konkretnego filmu.
Chroni to przed wygaśnięciem zaplanowanego linku, lecz przy kilku równoległych
transmisjach kanału YouTube może wybrać inną transmisję aktywną w chwili
startu. Wynik tego przypadku wymaga odbioru po wykonaniu harmonogramu.

## Blokada nakładających się skanów — 24.10.2026

Na kolejne polecenie użytkownika dodano jedną bramę trwającego skanu.
Jeżeli sprawdzanie wybranych kanałów już trwa, ponowne `Ctrl+Shift+I` ani
`F5` nie rozpoczyna drugiej serii zapytań do YouTube i nie unieważnia wyniku
pierwszej. NVDA otrzymuje komunikat:
`Sprawdzanie transmisji wybranych kanałów YouTube już trwa`.

Blokada jest zwalniana po powodzeniu, po błędzie i wtedy, gdy użytkownik
opuści widok przed nadejściem wyniku. Nazwa, skrót, widoczność i wykonanie
polecenia nadal pochodzą z dotychczasowego wspólnego katalogu poleceń; nie
powstała osobna droga klawiaturowa.

Diagnoza zakończonych transmisji: zbiorcza lista wykonuje wyłącznie oficjalne
zapytania o stany `live` i `upcoming`. Zwykły zsynchronizowany kanał AMC czyta
kartę `/videos`, a nie kartę archiwalnych transmisji `/streams`. Po końcu
wydarzenie wypada więc ze zbiorczej listy. Jeżeli nadawca zachowa publiczną
powtórkę wyłącznie na karcie transmisji, bieżący zwykły widok kanału AMC jej
nie zaimportuje. Materiał ukryty, niepubliczny albo usunięty przez nadawcę nie
jest dostępny żadną z tych dróg. Ewentualny widok zakończonych transmisji jest
osobnym przyszłym zakresem, a nie częścią tej blokady.

## Testy i rzeczywiste wyniki

- celowany zestaw po synchronizacji: **273 zaliczone testy i 2 podtesty**;
- pełna regresja `wxlite`: **2032 zaliczone, 111 pominiętych, 13 podtestów
  zaliczonych**, zero błędów, 153,04 s;
- po dodaniu blokady: testy powiązane **78 zaliczonych i 2 podtesty**, a pełna
  regresja **2033 zaliczone, 111 pominiętych i 13 podtestów**, zero błędów,
  204,07 s;
- oficjalna bramka promocji powtórzyła **30/30** testów launchera i
  aktualizatora, pełną regresję **2033 zaliczone, 111 pominiętych i 13
  podtestów** w 163,56 s oraz trzy buildy .NET bez ostrzeżeń i błędów;
- `git diff --check`: bez błędów;
- test Frontier potwierdza, że seria `25, 30, 35` wysyła tylko `25` i `35`;
- testy parytetu potwierdzają wspólne nazwy, skróty i widoczność w menu,
  kontekście, palecie i pomocy;
- ręczny odbiór NVDA, prawdziwego konta YouTube, nagrywania i Teufela:
  oczekuje po instalacji.

## Zmienione miejsca

- `wxlite/amc_wx_lite/gui.py` — selektor, zbiorczy widok, nagrywanie,
  harmonogram i scalanie poleceń Frontier;
- `shortcuts.py`, `menu_model.py`, `navigation.py` — jeden kontrakt poleceń;
- `youtube_live.py`, `list_model.py`, `state_store.py`, `full_backup.py` —
  dane, sortowanie, trwałość i kopia;
- `radio_schedule_dialogs.py`, `transient_preview_policy.py` — termin
  początkowy i bezpieczny powrót;
- sześć modułów testowych — widok, trwałość, parytet i Frontier.

## Wersja, commit i instalacja

- Blokada ponownego skanu: commit `efb8c090` —
  `fix(youtube): blokuj nakladajace sie skany live`.
- Zbudowana i zainstalowana paczka pochodzi z dokumentacyjnego commita
  `81a1e4c507000dee2fcf1ba60d93b059adcad0be`, ma identyfikator `8af5940f`
  i etykietę
  `2026.10.24-81a1e4c507000dee2fcf1ba60d93b059adcad0be-8af5940f`.
- Pełna integralność zainstalowanej kopii i jej odizolowany start są
  zaliczone. Potwierdzone okno użytkowe: `AMC Test — 24.10.2026, 14:48` z
  `D:\Projekty Codex\AMC Python\AMC.exe`; frontend i host odpowiadają.
- Pierwsza próba instalacji bezpiecznie zatrzymała się, gdy działające AMC
  zapisywało nagranie Poznań rozpoczęte o 14:43. Plik roboczy urósł z 0 do
  około 3,7 MB, a następnie został prawidłowo domknięty jako
  `D:\iCloudDrive\iCloud~co~supertop~castro\Sideloads\YouTubeAudio\Poznań - 2026-10-24 14-43-28.mp3`
  o rozmiarze 6 341 039 bajtów. Dopiero po zniknięciu procesów i pliku
  częściowego wykonano atomową podmianę.

- Commit źródłowy: `fe281d31af2c5328f1ada1c3668a587feefbcf80` —
  `feat: dodaj zbiorcze transmisje i ustabilizuj Frontier`.
- Pakiet: `8bce7f95`; etykieta:
  `2026.10.24-fe281d31af2c5328f1ada1c3668a587feefbcf80-8bce7f95`.
- Instalacja: `D:\Projekty Codex\AMC Python`; 1674 pliki, wszystkie sumy
  SHA-256 zgodne z manifestem kandydata.
- Bramka promocji: 30/30 testów launchera i aktualizatora, pełna regresja
  2032 zaliczone, 111 pominiętych, 13 podtestów zaliczonych oraz buildy .NET
  bez ostrzeżeń i błędów.
- Kandydat i stała instalacja zaliczyły odizolowany start interfejsu i hosta.
- Potwierdzone widoczne okno: `AMC Test — 24.10.2026, 13:33`, PID interfejsu
  7068, właściwy host PID 34048 i launcher PID 44992 w chwili weryfikacji.
- Pierwsza próba podmiany bezpiecznie odmówiła, ponieważ stary host nie
  odpowiadał po rzeczywistym przeciążeniu kolejką Frontier. Przed dalszym
  działaniem sprawdzono dwukrotnie foldery nagrań i pobrań, brak aktywnych
  `.amc-partial` i brak procesów kodera. Zatrzymano wyłącznie dokładny proces
  hosta AMC Test, po czym interfejs zamknął się zwykłą ścieżką z zapisem stanu.
  NVDA zdążył ponownie uruchomić starą instalację, dlatego końcową podmianę
  wykonano aktualizatorem oczekującym na rzeczywisty kod końcowy. Kod 0 i
  manifest nowej instalacji zostały potwierdzone przed uruchomieniem.
- AMC Public, instalator publiczny i GitHub: bez zmian.

## Materiał do instrukcji i listy zmian — projekt

> `Ctrl+Shift+I` pokazuje transmisje na żywo i zaplanowane z wybranych kanałów
> YouTube. Przy pierwszym użyciu wybierz kanały. Enter odtwarza, F5 odświeża,
> Ctrl+R nagrywa trwającą transmisję, a Shift+R otwiera harmonogram. Gdy
> sprawdzanie już trwa, kolejne Ctrl+Shift+I lub F5 nie zużywa następnych
> zapytań i informuje, że skan jest w toku.

> Regulacja głośności Frontier Smart nie wysyła już długiej kolejki zmian do
> radia. AMC zachowuje tylko ostatnią wartość wybraną podczas oczekiwania na
> odpowiedź urządzenia.

## Korekta lokalizacji polecenia — 6 listopada 2026

Na prośbę użytkownika wybór kanałów do sprawdzania transmisji na żywo przeniesiono z menu **Pliki** do menu **Ustawienia**, obok wyboru źródeł skrzynki podcastów. Nowa nazwa polecenia brzmi **Wybierz kanały do sprawdzania transmisji na żywo…**. Ta decyzja zastępuje opis wcześniejszej lokalizacji w sekcji powyżej. Działanie `Ctrl+Shift+I` i wybranych kanałów nie uległo zmianie; kod przeniesienia pozostaje w roboczych źródłach zadania `POD-20261106`, bez nowej instalacji i odbioru NVDA.
