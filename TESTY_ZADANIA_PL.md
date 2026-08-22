# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-084`
- Tytuł zestawu: Wyjście z odtwarzacza i pamiętanie pozycji
- Wersja programu: `0.1.0-alpha.84`
- Utworzono: 2026-08-22, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_0.1.0-alpha.84.md`

Możesz testować całkowicie opisowo. Nie trzeba wypełniać pliku ani wybierać przed każdym zadaniem wariantu „OK” lub „błąd”. Najważniejsze jest podanie użytego skrótu, miejsca w programie i tego, co powiedział NVDA.

## Nowości alpha 84

### AMC-084-01 — Domyślny Escape

Odtwórz lokalny plik, przejdź do odtwarzacza, odczekaj kilkanaście sekund i naciśnij `Escape`.

Oczekiwane: dźwięk zostaje wstrzymany, a fokus wraca na listę do logicznie tego samego elementu. Zwykłe strzałki listy nadal służą do nawigacji i nie sterują czasem ani głośnością.

### AMC-084-02 — Wznowienie zapamiętywanego pliku

Przy domyślnie zaznaczonej opcji **Pamiętaj pozycje lokalnych plików** odtwórz dłuższe nagranie, wyjdź `Escape`, wróć przez `F6` i uruchom odtwarzanie. Powtórz po ponownym uruchomieniu AMC.

Oczekiwane: `Escape` zatrzymuje dźwięk, lecz nie zeruje zapamiętanego miejsca. Wznowienie zaczyna się od ostatniej pozycji także po restarcie.

### AMC-084-03 — Folder muzyczny zawsze od początku

Otwórz **Plik → Zarządzaj źródłami Biblioteki**, wybierz bezpieczny folder z muzyką, ustaw **Pamiętanie pozycji w wybranym źródle** na **Zawsze od początku** i zapisz. Odtwórz plik z tego folderu, wyjdź `Escape`, wróć do niego i uruchom ponownie.

Oczekiwane: menedżer i jego lista czytelnie podają nową zasadę. Po `Escape` plik z tego źródła rozpoczyna się od `0:00`; stara pozycja nie wraca także po restarcie.

### AMC-084-04 — Nadpisanie ustawienia ogólnego

Wyłącz w Ustawieniach ogólnych **Domyślnie pamiętaj pozycje lokalnych plików**. Dla jednego folderu ustaw jednak **Pamiętaj pozycję**, a dla drugiego pozostaw **Używaj ustawienia ogólnego**. Sprawdź po jednym pliku z każdego folderu.

Oczekiwane: pierwszy folder wznawia miejsce mimo wyłączonej zasady ogólnej, drugi zaczyna od początku. Plik dodany pojedynczo przez `Ctrl+O` korzysta z zasady ogólnej.

### AMC-084-05 — Opcjonalne granie po wyjściu

Odznacz **Wstrzymuj odtwarzanie po wyjściu z odtwarzacza**, rozpocznij odtwarzanie i naciśnij `Escape`. Potem ponownie włącz tę opcję.

Oczekiwane: przy wyłączonej opcji lista się pojawia, ale dźwięk trwa. Po ponownym włączeniu `Escape`, `Shift+F6`, przycisk **Wróć do listy** oraz bezpośrednie przejście do widoku wstrzymują dźwięk.

### AMC-084-06 — Paleta, fokus i regresja

W palecie `Ctrl+Shift+K` wyszukaj obie nowe opcje ustawień. Sprawdź fokus po Zapisz i Anuluj, przełączanie sesji z otwartego odtwarzacza, `Page Up/Down`, przewijanie i głośność w odtwarzaczu oraz zwykłą nawigację listy.

Oczekiwane: paleta otwiera Ustawienia na właściwym polu wyboru. Opuszczana sesja zostaje wstrzymana zgodnie z opcją, NVDA nie traci fokusu, a dotychczasowe skróty listy i odtwarzacza nie mają regresji.

## Poprzedni zestaw regresyjny alpha 83

## Nowości alpha 83

Do prób odłączania wybierz źródło, które można później bezpiecznie dodać ponownie. **Odłącz źródło** nie może usuwać żadnego pliku z dysku.

### AMC-083-01 — Fokus i zawartość menedżera

Otwórz **Plik → Zarządzaj źródłami Biblioteki**. Sprawdź pierwszą kontrolkę, nawigację strzałkami i Tabem oraz Escape.

Oczekiwane: fokus zaczyna na zwykłej liście źródeł. Każdy wpis podaje nazwę, dostępność, liczby aktywnych, niedostępnych i wykluczonych plików oraz ścieżkę. Tab prowadzi do przycisków, Escape zamyka tylko menedżer, a fokus wraca do głównej listy AMC.

### AMC-083-02 — Odśwież jedno i wszystkie źródła

W menedżerze wybierz źródło i użyj **Odśwież wybrane**, następnie **Odśwież wszystkie**.

Oczekiwane: operacje kończą się czytelnym komunikatem, lista zachowuje zaznaczenie, NVDA nie traci fokusu, a niedostępne źródło nie powoduje skasowania zapisanych rekordów.

### AMC-083-03 — Ochrona przed nakładającymi się folderami

Mając zarejestrowane źródło, spróbuj dodać ponownie tę samą ścieżkę, jej podfolder, a następnie folder nadrzędny obejmujący istniejące źródło.

Oczekiwane: ta sama ścieżka jest jedynie ponownie skanowana. Podfolder i folder nadrzędny nie tworzą drugiego źródła; AMC jednoznacznie podaje, z którym istniejącym źródłem wystąpił konflikt.

### AMC-083-04 — Bezpieczne odłączenie

Zanotuj liczbę plików i stan przykładowego źródła, wybierz **Odłącz źródło**, przeczytaj całe pytanie i zatwierdź. Sprawdź dysk, `Alt+1`, `Alt+2`, Ulubione, Kolejkę, Historię oraz Zakładki. Uruchom AMC ponownie.

Oczekiwane: źródło znika tylko z listy automatycznej synchronizacji. Żaden plik na dysku nie zostaje usunięty. Rekordy i ich relacje pozostają dostępne jako pliki dodane pojedynczo także po restarcie. Jeśli bieżący poziom Folderów należał do odłączonego źródła, AMC bezpiecznie wraca na główny poziom.

### AMC-083-05 — Pełna kopia AMC

W menedżerze wybierz **Eksportuj pełną kopię AMC**. Zapisz plik `.amcbackup.json` w bezpiecznym miejscu. Sprawdź też opis w **Ustawienia → Import i eksport**.

Oczekiwane: komunikat potwierdza eksport katalogu Biblioteki, źródeł, wykluczeń, Ulubionych, kolejek, historii, zakładek, pozycji i ustawień. Opis w Ustawieniach wymienia te dane i zaznacza brak haseł oraz tokenów. Sam eksport niczego nie zmienia w Bibliotece.

### AMC-083-06 — Paleta i regresja chmur

Otwórz `Ctrl+Shift+K`, wyszukaj „źródła biblioteki” i uruchom polecenie. Potem wykonaj `F5`, `Alt+1`, `Alt+2` oraz wyrywkowy test źródła iCloud, OneDrive albo Google Drive.

Oczekiwane: paleta otwiera ten sam menedżer. Dotychczasowa nawigacja, bezpieczne skanowanie chmury, kopiowanie, odtwarzanie i kolejki działają bez regresji.

## Poprzedni zestaw regresyjny alpha 82

## Poprawki alpha 82

### AMC-082-01 — OneDrive bez masowego pobierania

Przez `Ctrl+Shift+O` dodaj niewielki folder OneDrive zawierający kilka plików audio dostępnych lokalnie i, jeśli masz takie dane testowe, kilka pozycji „tylko online”.

Oczekiwane: AMC pokazuje nazwy obu rodzajów plików, nie pomija folderu z atrybutem Cloud Files i nie rozpoczyna samoczynnego odtwarzania ani pobierania całej zawartości.

### AMC-082-02 — Google Drive Mirror

Jeżeli używasz trybu lustrzanego Google Drive, dodaj niewielki folder z plikami audio i wykonaj `F5`.

Oczekiwane: źródło zachowuje się jak zwykły folder lokalny, bez duplikatów i bez specjalnych komunikatów chmurowych.

### AMC-082-03 — Google Drive Stream albo niedostępny dysk

Jeżeli używasz trybu strumieniowanego, dodaj folder z wirtualnego dysku Google Drive, zamknij AMC, zatrzymaj Google Drive for desktop, uruchom AMC i użyj `F5`. Następnie ponownie uruchom Google Drive i odśwież źródła. Jeśli nie masz trybu Stream, test można pominąć.

Oczekiwane: podczas niedostępności AMC nie usuwa rekordów, historii, zakładek ani pozycji. Informuje o niedostępnym źródle. Po powrocie dysku te same rekordy stają się dostępne bez duplikatów.

### AMC-082-04 — Regresja iCloud i Alt+1

Powtórz test `Sideloads` oraz przejście z zaznaczonego pliku w `Alt+2` do `Alt+1`.

Oczekiwane: `Sideloads` nadal pokazuje 317 zapisanych plików audio, a `Alt+1` prowadzi do folderu zaznaczonego elementu.

## Poprzedni zestaw regresyjny alpha 81

## Poprawki alpha 81

### AMC-081-01 — Naprawa źródła Sideloads

Uruchom alfę 81 na dotychczasowym stanie i otwórz Foldery Biblioteki przez `Alt+1`. Wejdź do źródła `Sideloads`.

Oczekiwane: jednorazowa migracja usuwa błędne zbiorowe wykluczenie z alfy 80, źródło nie jest puste i pokazuje rozpoznane pliki oraz podfoldery. Samo indeksowanie nie powinno rozpoczynać odtwarzania ani pobierania wszystkich nagrań z iCloud.

### AMC-081-02 — Alt+2 do Alt+1

W `Alt+2` zaznacz plik znajdujący się kilka poziomów pod jednym ze źródeł, a następnie naciśnij `Alt+1`.

Oczekiwane: AMC otwiera folder nadrzędny tego pliku, pozostawia fokus na tym samym pliku i odczytuje położenie w Folderach. Dla pliku dodanego pojedynczo przez `Ctrl+O`, który nie leży w żadnym źródle, `Alt+1` przechodzi do głównego poziomu Folderów i zachowuje zaznaczenie na samodzielnym pliku.

### AMC-081-03 — Placeholder iCloud i F5

W źródle iCloud sprawdź plik dostępny tylko jako placeholder, następnie naciśnij `F5`.

Oczekiwane: plik pozostaje w Bibliotece, skanowanie nie uznaje `ReparsePoint` za dowiązanie i nie uruchamia pobierania treści. Prawdziwe dowiązania katalogów nie są przeszukiwane.

### AMC-081-04 — Kolejki poszczególnych sesji

Dodaj element do Kolejki w WiiM lub innej demonstracyjnej sesji. Otwórz lokalny folder przez `Ctrl+Shift+O`, sprawdź lokalną Kolejkę, a potem wróć do poprzedniej sesji i ponownie otwórz jej Kolejkę.

Oczekiwane: po otwarciu folderu aktywna jest sesja Pliki lokalne, której Kolejka może być pusta. Powrót do poprzedniej sesji pokazuje jej wcześniejszą Kolejkę; samo skanowanie lokalnego źródła jej nie czyści.

## Poprzedni zestaw regresyjny alpha 80

## Nowości alpha 80

Do prób z kasowaniem i zmianą nazwy przygotuj osobny folder oraz niepotrzebne kopie plików. Nie wykonuj `Shift+Delete` na jedynym egzemplarzu nagrania.

### AMC-080-01 — Kilka źródeł i dwa układy Biblioteki

Dodaj przez `Ctrl+Shift+O` co najmniej dwa różne foldery, najlepiej z podfolderami. Użyj `Alt+1`, `Alt+2`, a potem `Ctrl+L`.

Oczekiwane: `Alt+1` pokazuje **Foldery Biblioteki** z osobnymi źródłami, `Alt+2` jedną alfabetyczną listę **Wszystkie pliki**, a `Ctrl+L` wraca do ostatnio wybranego układu. Są to dwa widoki tych samych rekordów, bez duplikatów.

### AMC-080-02 — Automatyczne dodanie pliku

Przy uruchomionym AMC skopiuj z zewnątrz nowy rozpoznawany plik audio do jednego ze źródeł i odczekaj około dwóch sekund. Sprawdź oba układy.

Oczekiwane: plik pojawia się bez ponownego wybierania folderu i bez przejęcia fokusu. Jeśli obserwator danego nośnika nie działa, `F5` dodaje plik po pełnym skanowaniu.

### AMC-080-03 — Brak pliku i jego powrót

Przenieś testowy plik poza źródło bez używania AMC. Sprawdź listę, a następnie umieść go z powrotem dokładnie pod tą samą ścieżką i naciśnij `F5`, jeśli zmiana nie pojawi się samoczynnie.

Oczekiwane: brakujący plik znika z aktywnych list, lecz AMC nie usuwa jego historii, zakładek ani zapamiętanej pozycji. Po powrocie pojawia się jako ten sam rekord.

### AMC-080-04 — Trwałe wykluczenie przez Delete

Zaznacz testowy plik i naciśnij zwykły `Delete`. Potem użyj `F5`, przełącz `Alt+1` i `Alt+2`, zakończ AMC i uruchom je ponownie.

Oczekiwane: plik pozostaje na dysku, ale nie wraca do Biblioteki po skanowaniu ani restarcie. W osobnej próbie wykonanej bezpośrednio po Delete `Ctrl+Z` przywraca go od razu.

### AMC-080-05 — Niedostępne całe źródło

Jeśli masz bezpieczny testowy folder na nośniku odłączanym, zarejestruj go, zamknij AMC, odłącz nośnik i uruchom program. Alternatywnie tymczasowo zmień nazwę folderu przy zamkniętym AMC. Użyj `F5`.

Oczekiwane: AMC zgłasza jedno niedostępne źródło, ale nie uznaje go za pusty folder i nie kasuje jego danych. Po ponownym udostępnieniu źródła rekordy wracają.

### AMC-080-06 — Zmiana nazwy podczas działania

Przy uruchomionym AMC zmień poza programem nazwę testowego pliku, a potem nazwę jego folderu. Wcześniej można dodać zakładkę lub zapamiętać pozycję odtwarzania.

Oczekiwane: lista aktualizuje nazwę i ścieżkę, nie tworzy duplikatu, a stan rekordu pozostaje. Ruch wykonany przy zamkniętym AMC nie jest jeszcze gwarantowany jako ten sam rekord i stanowi znane ograniczenie tej wersji.

### AMC-080-07 — Plik aktualnie odtwarzany staje się niedostępny

Odtwórz niepotrzebną kopię, a następnie przenieś ją poza źródło w Eksploratorze lub innym menedżerze plików.

Oczekiwane: AMC zatrzymuje własny tor, informuje, który plik stał się niedostępny, nie blokuje NVDA i pozostawia pozostałe elementy aktywne.

### AMC-080-08 — Nawigacja po znakach specjalnych

Umieść w jednym widocznym poziomie kopie o nazwach zaczynających się np. od `!`, `@`, `#` albo cyfry. Sprawdź wpisywanie znaków oraz `Shift+cyfrę` właściwą dla polskiego układu klawiatury, a następnie `Alt+1` i `Alt+2`.

Oczekiwane: znaki specjalne uczestniczą w nawigacji po nazwach; `Alt+1/2` przełącza układ i nie przechwytuje `Shift+cyfr`.

### AMC-080-09 — Wyszukiwanie po synchronizacji

Wyklucz jeden plik Delete, przenieś drugi poza źródło, dodaj trzeci i wykonaj `Ctrl+F` dla ich nazw.

Oczekiwane: wyszukiwanie lokalne znajduje nowy aktywny plik, ale nie zwraca wykluczonego ani niedostępnego.

### AMC-080-10 — Regresja działań i fokusu

W obu układach sprawdź Enter, Escape, `Ctrl+C`, `Ctrl+Shift+C`, Ulubione, Kolejkę, zakładki, `Alt+Enter`, zaznaczanie Shiftem oraz menu kontekstowe. Uruchom też pełne `F5` z fokusem wewnątrz listy.

Oczekiwane: stan jest wspólny w obu układach, fokus pozostaje na logicznie tym samym elemencie, a odświeżenie nie otwiera filtra ani nie przenosi fokusu na pasek stanu.

## Poprzedni zestaw regresyjny alpha 79

## Poprawki alpha 79

Do testu fizycznego kasowania używaj wyłącznie niepotrzebnych kopii plików. Zwykły `Delete` nie powinien w tym zestawie usuwać niczego z dysku.

### AMC-079-01 — Folder jako źródło Biblioteki

W pustej albo dotychczasowej sesji Pliki lokalne naciśnij `Ctrl+Shift+O` i wybierz folder zawierający rozpoznane pliki w katalogu głównym oraz podfolderach. Po zakończeniu skanowania naciśnij `Ctrl+L`.

Oczekiwane: Foldery pokazują rzeczywistą hierarchię, natomiast Biblioteka pokazuje płaską alfabetyczną listę wszystkich wczytanych plików. Nie pojawia się dawna, niezależna biblioteka ani duplikaty.

### AMC-079-02 — Ponowne włączenie znanego pliku

W Bibliotece usuń zwykłym `Delete` jeden plik, upewnij się, że zniknął z `Ctrl+L`, a następnie ponownie wybierz jego źródło przez `Ctrl+Shift+O` i wróć do Biblioteki.

Oczekiwane: znany plik wraca do Biblioteki bez powstania drugiego rekordu. Komunikat rozróżnia przywrócenie od dodania nowego pliku.

### AMC-079-03 — Delete na pliku w Folderach

W widoku Foldery zaznacz plik należący do Biblioteki i naciśnij `Delete`. Następnie sprawdź `Ctrl+L`, wróć do Folderów i użyj `Ctrl+Z`.

Oczekiwane: program mówi, że usunął plik z Biblioteki i pozostawił go w folderze. Plik znika z płaskiej Biblioteki, ale nadal istnieje na dysku i pozostaje widoczny w swoim fizycznym folderze. `Ctrl+Z` przywraca jego przynależność do Biblioteki.

### AMC-079-04 — Delete na wierszu folderu

Zaznacz podfolder albo wiersz źródła i naciśnij `Delete`.

Oczekiwane: nic nie zostaje usunięte. AMC wyjaśnia, że Delete nie usuwa folderu ani źródła, Enter otwiera folder, a zarządzanie źródłami będzie osobnym poleceniem.

### AMC-079-05 — Rozdzielenie Delete i Shift+Delete

Na niepotrzebnej kopii pliku porównaj `Delete` oraz `Shift+Delete` z potwierdzeniem.

Oczekiwane: `Delete` zmienia tylko przynależność w AMC. Dopiero `Shift+Delete` pyta o potwierdzenie i przenosi fizyczny plik do systemowego Kosza.

## Poprzedni zestaw regresyjny alpha 78

## Poprawki alpha 78

### AMC-078-01 — Pusta sesja lokalna

Uruchom AMC bez dodawania plików i naciśnij `Ctrl+1`. Następnie spróbuj F6 albo polecenia odtwarzania.

Oczekiwane: `Ctrl+1` wybiera „Pliki lokalne” i pustą listę, a nie sesję nieprzypisaną ani listę demonstracyjną. Polecenie wymagające pliku mówi „Brak elementów w sesji Pliki lokalne” i nie otwiera fikcyjnego odtwarzacza.

### AMC-078-02 — Wczytywanie folderu z innej sesji

Przejdź do TIDAL, Apple Music albo WiiM, naciśnij `Ctrl+Shift+O` i wybierz folder zawierający pliki audio, najlepiej także podfoldery.

Oczekiwane: po zatwierdzeniu wyboru program natychmiast przechodzi do „Pliki lokalne — Foldery” i mówi nazwę wczytywanego folderu. Podczas skanowania nie pokazuje demonstracyjnych utworów poprzedniej usługi. Po zakończeniu wyświetla prawdziwe podfoldery i pliki.

### AMC-078-03 — Pusty folder i trwałość źródła

Wybierz `Ctrl+Shift+O` pusty katalog, a potem zakończ i ponownie uruchom AMC.

Oczekiwane: program informuje, że nie znaleziono obsługiwanych plików, ale pozostaje w lokalnym widoku i nie wraca do danych demonstracyjnych. Źródło folderu i przypisanie `Ctrl+1` pozostają zapisane.

## Poprzedni zestaw regresyjny alpha 77

## Nowości alpha 77

Do testu Folderów najlepiej wybrać katalog zawierający co najmniej dwa pliki audio w katalogu głównym i dwa różne podfoldery. Zwykłe otwieranie folderu niczego fizycznie nie przenosi ani nie usuwa.

### AMC-077-01 — Domyślne miejsca sesji

Po pierwszym uruchomieniu tej wersji sprawdź `Ctrl+1`, `Ctrl+2`, `Ctrl+3` i `Ctrl+4`.

Oczekiwane w bieżącej wersji: `Ctrl+1` zawsze wybiera Pliki lokalne, także z pustą listą; dalej są WiiM, TIDAL i Apple Music. Program podaje numer, usługę oraz zapamiętany widok tej sesji.

### AMC-077-02 — Zmiana kolejności sesji

Otwórz `Ctrl+,`, kartę Ogólne i listę „Kolejność sesji i skrótów Ctrl+1–9”. Przesuń wybraną sesję `Alt+strzałka w górę` albo przyciskiem, zapisz ustawienia i sprawdź odpowiednie `Ctrl+cyfra`.

Oczekiwane: po każdym przesunięciu NVDA podaje nowy numer. Po zapisaniu skrót wybiera właściwą sesję, lista `Ctrl+0` ma tę samą kolejność, a `Ctrl+Page Up/Page Down` przechodzi według niej.

### AMC-077-03 — Trwałość kolejności

Zakończ AMC po zmianie kolejności i uruchom je ponownie.

Oczekiwane: własna kolejność oraz znaczenie `Ctrl+1–9` pozostają zachowane. Przycisk „Przywróć domyślną” odtwarza kolejność: Pliki lokalne, WiiM, TIDAL, Apple Music.

### AMC-077-04 — Połączona karta Komunikaty

W Ustawieniach odszukaj kartę Komunikaty i sekcję „Odczytywanie elementów list”. Zmień kolejność pól `Alt+strzałka w górę/w dół`, zapisz i sprawdź zwykłą listę.

Oczekiwane: nie ma osobnej, dublującej karty „Listy i odczyt”. Kolejność pól, podgląd, przyciski oraz wszystkie dotychczasowe ustawienia komunikatów są dostępne i działają. Paleta poleceń nadal potrafi ustawić fokus bezpośrednio na kolejności odczytu i na kolejności sesji.

### AMC-077-05 — Rejestracja źródła folderu

Naciśnij `Ctrl+Shift+O` i wybierz przygotowany katalog.

Oczekiwane: program niczego automatycznie nie odtwarza, rejestruje źródło i pokazuje widok Foldery. Na jednym poziomie podfoldery są przed plikami, a NVDA czyta każdy podfolder jako „folder”.

### AMC-077-06 — Nawigacja po poziomach

W widoku Foldery otwórz podfolder Enterem, uruchom plik Enterem, wróć Escape z odtwarzacza, a następnie użyj Backspace.

Oczekiwane: Enter na folderze schodzi dokładnie o poziom, Enter na pliku otwiera wspólny odtwarzacz, Escape wraca do tego samego poziomu i elementu, a Backspace wraca do folderu nadrzędnego. Na liście źródeł Backspace jedynie informuje, że wyżej przejść nie można.

### AMC-077-07 — Litery, filtr i wyszukiwanie

W podfolderze użyj kilku liter, następnie `Ctrl+K`; na końcu wykonaj `Ctrl+F` dla nazwy pliku znajdującego się w innym podfolderze.

Oczekiwane: litery i filtr dotyczą tylko widocznego poziomu. Wyszukiwanie obejmuje całą sesję Pliki lokalne i może znaleźć rekord spoza bieżącego folderu. Strzałka w lewo na pliku nadal podaje dostępne parametry techniczne.

### AMC-077-08 — Pamięć Folderów i Biblioteki

Pozostaw fokus wewnątrz podfolderu, zakończ AMC i uruchom ponownie. Następnie otwórz płaską Bibliotekę przez `Ctrl+L` i wróć do Folderów z menu Widok albo palety.

Oczekiwane: źródło oraz poziom folderu są pamiętane. Biblioteka nadal zawiera wszystkie zaimportowane pliki w płaskim zestawieniu, a powrót do Folderów nie tworzy duplikatów.

### AMC-077-09 — Regresja lokalnych działań

Na pliku dostępnym przez Foldery sprawdź odtwarzanie, zakładkę, `Ctrl+C`, `Ctrl+Shift+C`, Ulubione, Kolejkę i informacje `Alt+Enter`.

Oczekiwane: rekord jest tym samym elementem co w Bibliotece, więc odtwarzanie, pozycja, zakładki i stany przynależności są wspólne. Kopiowanie nazwy i fizycznego pliku działa jak dotychczas.

## Poprzedni zestaw regresyjny alpha 76

## Nowości alpha 76

Do tych prób użyj wyłącznie niepotrzebnych kopii plików, które można później odzyskać z systemowego Kosza.

### AMC-076-01 — Rezygnacja z potwierdzenia

Zaznacz testowy plik lokalny, naciśnij `Shift+Delete`, a w pytaniu wybierz „Nie”.

Oczekiwane: plik pozostaje na dysku i w AMC, a fokus wraca do listy.

### AMC-076-02 — Plik iCloud

Zaznacz niepotrzebną kopię pliku znajdującą się w iCloud Drive i potwierdź `Shift+Delete`.

Oczekiwane: plik trafia do systemowego Kosza i znika z katalogu AMC. Nie pojawia się okno „Nieoczekiwany błąd”, a aplikacja pozostaje uruchomiona.

### AMC-076-03 — Plik aktualnie odtwarzany

Uruchom testowy plik, wróć do listy, zaznacz go i wykonaj `Shift+Delete`.

Oczekiwane: AMC najpierw zamyka odtwarzanie i zwalnia plik, a następnie przenosi go do Kosza. Lista przechodzi do następnego dostępnego elementu i podaje wynik.

### AMC-076-04 — Kilka plików

Zaznacz Shiftem dwie lub trzy niepotrzebne kopie, także z iCloud Drive, i potwierdź `Shift+Delete`.

Oczekiwane: wszystkie przeniesione pliki znikają z AMC i można je znaleźć w Koszu. Program nie zawiesza się podczas odświeżania listy.

### AMC-076-05 — Kontrolowany błąd

Jeśli któryś plik jest zablokowany albo niedostępny, spróbuj go przenieść do Kosza.

Oczekiwane: AMC pozostaje uruchomiony, nie usuwa błędnego rekordu i podaje nazwę pliku, opis oraz kod `0x...`. Inne prawidłowo przetworzone elementy mogą zostać usunięte.

### AMC-076-06 — Delete pozostawia plik

Na osobnej kopii użyj zwykłego `Delete` albo Backspace.

Oczekiwane: znika tylko rekord AMC, natomiast fizyczny plik nadal istnieje w swoim folderze. Znaczenie zwykłego Delete nie zmieniło się.

## Poprzedni zestaw regresyjny alpha 75

## Nowości alpha 75

### AMC-075-01 — Lokalny wynik wyszukiwania

W sesji Pliki lokalne wykonaj `Ctrl+F`, wybierz znaleziony plik i naciśnij strzałkę w lewo.

Oczekiwane: NVDA podaje krótką informację o wyniku, zawierającą wszystkie dostępne dane, w tym format, czas, bitrate w kb/s, częstotliwość w kHz i rozmiar. Fokus pozostaje na tym samym wyniku, a okno wyszukiwania nie zamyka się.

### AMC-075-02 — Spójność ze zwykłą listą

Otwórz ten sam plik na zwykłej liście i ponownie naciśnij strzałkę w lewo.

Oczekiwane: zakres i kolejność informacji są takie same jak w wyszukiwaniu. Dopuszczalne jest uzupełnienie wcześniej nieznanego parametru po pierwszym odczytaniu metadanych.

### AMC-075-03 — Wyszukiwanie globalne

Użyj `Ctrl+Shift+F`, znajdź plik lokalny i naciśnij strzałkę w lewo.

Oczekiwane: szybka informacja działa również w globalnych wynikach i nie przełącza sesji ani widoku.

### AMC-075-04 — Sesja streamingowa

Na zwykłej liście i w wynikach wyszukiwania sesji demonstracyjnej naciśnij strzałkę w lewo.

Oczekiwane: AMC podaje te dane, które posiada, w tym bitrate i kHz, jeśli adapter je zwrócił. Nie szacuje nieznanych parametrów streamingu. Jeśli nie ma żadnych danych technicznych, mówi o ich braku.

### AMC-075-05 — Regresja schowka

Na lokalnym pliku sprawdź `Ctrl+C` w edytorze tekstowym oraz `Ctrl+Shift+C` przez wklejenie do folderu i do edytora.

Oczekiwane: pierwszy skrót kopiuje nazwę. Drugi przekazuje prawdziwy plik oraz pełną ścieżkę tekstową.

## Poprzedni zestaw regresyjny alpha 74

## Nowości alpha 74

### AMC-074-01 — Shift+Page na zwykłej liście

Na liście Multimedia, Biblioteka albo Ulubione naciśnij `Shift+Page Down`, a następnie `Shift+Page Up`.

Oczekiwane: jest to standardowe rozszerzone zaznaczanie większego zakresu listy. Widok nie zmienia się na Zakładki, nie pojawia się lista zakładek i tytuł okna nie zawiera słowa „Zakładki”.

### AMC-074-02 — Jawne otwarcie Zakładek

Zapamiętaj sesję, widok i zaznaczony element, a następnie naciśnij `Ctrl+B`.

Oczekiwane: dopiero ten skrót otwiera globalny widok Zakładek. NVDA podaje kontekst Zakładek i aktywnej sesji.

### AMC-074-03 — Dwustopniowy powrót

Na liście Zakładek otwórz wybraną zakładkę Enterem. W odtwarzaczu naciśnij Escape, a potem ponownie Escape.

Oczekiwane: pierwszy Escape wraca do tej samej zakładki na liście Zakładek. Drugi wraca do zapamiętanej sesji, poprzedniego widoku i możliwie tego samego zaznaczonego elementu.

### AMC-074-04 — Powrót między sesjami

Otwórz `Ctrl+B` z sesji Pliki lokalne, ale wybierz zakładkę należącą do innej sesji. Po jej otwarciu wróć dwukrotnie przez Escape.

Oczekiwane: pierwszy powrót prowadzi do rekordu Zakładek, drugi do pierwotnej sesji Pliki lokalne i miejsca sprzed `Ctrl+B`.

### AMC-074-05 — Escape i filtr Zakładek

W widoku Zakładek wpisz tekst do filtra. Naciśnij Escape dwa razy.

Oczekiwane: pierwszy Escape jedynie czyści filtr i pozostawia Zakładki otwarte. Drugi zamyka przejściowy widok i przywraca listę źródłową.

### AMC-074-06 — Nawigacja zakładek w odtwarzaczu

Otwórz materiał zawierający kilka zakładek i użyj `Shift+Page Up/Down` w odtwarzaczu.

Oczekiwane: skróty przechodzą wyłącznie po zakładkach tego materiału. Nie otwierają globalnej listy Zakładek.

### AMC-074-07 — Ponowne uruchomienie

Zakończ AMC, będąc na globalnej liście Zakładek, i uruchom program ponownie. Osobno wykonaj próbę, kończąc pracę w otwartym odtwarzaczu.

Oczekiwane: sama lista Zakładek nie jest po starcie przywracana bez ostrzeżenia — pojawia się podstawowa lista sesji. Odtwarzacz może zostać przywrócony; Escape prowadzi wtedy do zwykłej listy, a nie do niespodziewanego widoku Zakładek.

## Poprzedni zestaw regresyjny alpha 73

## Nowości alpha 73

### AMC-073-01 — Zaznaczanie wyników wyszukiwania

Wykonaj `Ctrl+F` albo `Ctrl+Shift+F`, a na wynikach użyj `Shift+strzałka w dół` kilka razy.

Oczekiwane: lista zachowuje się jak standardowa lista wielokrotnego wyboru i zaznacza kolejne wyniki. Nawigacja bez Shifta nadal wybiera pojedynczy wynik.

### AMC-073-02 — Zbiorcze kopiowanie nazw

Zaznacz kilka wyników i naciśnij `Ctrl+C`, następnie wklej do edytora tekstu.

Oczekiwane: każda nazwa znajduje się w osobnym wierszu i zachowana jest kolejność widoczna na liście.

### AMC-073-03 — Zbiorcze kopiowanie plików

W wyszukiwaniu sesji Pliki lokalne zaznacz kilka wyników, naciśnij `Ctrl+Shift+C` i wklej w pustym folderze testowym.

Oczekiwane: system wkleja wszystkie zaznaczone prawdziwe pliki. W schowku tekstowym dostępne są również ich pełne ścieżki.

### AMC-073-04 — Wyniki z różnych usług

Wyszukaj we wszystkich usługach, zaznacz wyniki lokalne i usługowe, po czym użyj `Ctrl+Shift+C` i wklej tekst do edytora.

Oczekiwane: lokalne pozycje mają pełne ścieżki, a usługowe — łącza. Jeśli wklejasz do folderu, system przekazuje tylko rzeczywiste pliki lokalne.

### AMC-073-05 — Blokada wycinania w wyszukiwaniu

Na wynikach naciśnij `Ctrl+X`, a potem `Ctrl+V`.

Oczekiwane: program jednoznacznie informuje, że wycinanie i wklejanie nie działają na liście wyników. W polu wyszukiwania `Ctrl+X`, `Ctrl+C` i `Ctrl+V` nadal standardowo edytują tekst.

### AMC-073-06 — Wklejanie do lokalnej biblioteki

Skopiuj w Eksploratorze jeden lub kilka obsługiwanych plików audio. W AMC przejdź do sesji Pliki lokalne, do widoku Multimedia lub Biblioteka, i naciśnij `Ctrl+V`.

Oczekiwane: pliki pojawiają się w AMC i są zaznaczone. Fizycznie pozostają w swoich dotychczasowych folderach. Ponowne wklejenie nie tworzy duplikatów.

### AMC-073-07 — Wklejanie do Kolejki i Ulubionych

Skopiuj plik spoza katalogu AMC. Wklej go najpierw w widoku Kolejka, a inny w widoku Ulubione.

Oczekiwane: pierwszy zostaje dodany do katalogu i kolejki, drugi do katalogu i ulubionych. Oba są dostępne również w lokalnych Multimediach.

### AMC-073-08 — Niedozwolone cele

Spróbuj wkleić plik w Historii odtwarzania, Zakładkach, odtwarzaczu i sesji demonstracyjnej.

Oczekiwane: żaden plik nie jest dodawany, a komunikat wskazuje dozwolone lokalne widoki.

### AMC-073-09 — Wytnij, a następnie wklej wewnątrz AMC

Na zwykłej lokalnej liście naciśnij `Ctrl+X`, następnie przejdź do Kolejki lub Ulubionych i naciśnij `Ctrl+V`. Potem spróbuj wkleić ten sam schowek do folderu w Eksploratorze.

Oczekiwane: AMC dodaje przynależność, ale nie przenosi pliku. Późniejsze wklejenie w Eksploratorze działa jak kopiowanie, a nie przenoszenie źródła.

## Poprzedni zestaw regresyjny alpha 72

## Nowości alpha 72

### AMC-072-01 — Globalna kolejność informacji

Otwórz `Ctrl+B` i przejdź po kilku zakładkach należących do jednego oraz do różnych plików.

Oczekiwane: każdy wiersz zaczyna się od nazwy pliku lub materiału, następnie podaje datę utworzenia zakładki, pozycję w materiale, opcjonalną nazwę, sesję i słowo „zakładka”. Kilka zakładek tego samego pliku pozostaje ułożonych według pozycji w nagraniu, a nie według daty utworzenia.

### AMC-072-02 — Krótka nawigacja w odtwarzaczu

W otwartym materiale użyj `Shift+Page Up/Down` najpierw dla zwykłej, a potem nazwanej zakładki.

Oczekiwane: zwykła zakładka podaje tylko czas. Nazwana podaje nazwę i czas. Nie powtarza tytułu pliku ani daty utworzenia.

### AMC-072-03 — Kopiowanie jednej zakładki

Na liście `Ctrl+B` wybierz zakładkę i naciśnij `Ctrl+C`, po czym wklej zawartość do edytora tekstu.

Oczekiwane: skopiowany jest pełny opis widocznego wiersza — razem z datą i czasem — a nie sam tytuł pliku źródłowego.

### AMC-072-04 — Kopiowanie kilku zakładek

Zaznacz kilka sąsiednich zakładek przez `Shift+strzałka`, naciśnij `Ctrl+C` i wklej wynik.

Oczekiwane: każda zaznaczona zakładka znajduje się w osobnym wierszu, w tej samej kolejności co na liście.

### AMC-072-05 — Powrót i fokus

Z listy Zakładek otwórz rekord Enterem, a następnie wróć przez Escape.

Oczekiwane: fokus wraca do tego samego rekordu i strzałki od razu działają. Nie pojawia się dodatkowy widok ani filtr.

### AMC-072-06 — Dwa sposoby kopiowania w wyszukiwaniu

W `Ctrl+F` i `Ctrl+Shift+F` wybierz lokalny wynik. Naciśnij `Ctrl+C`, wklej do edytora, następnie naciśnij `Ctrl+Shift+C` i wklej w folderze testowym albo menedżerze plików.

Oczekiwane: `Ctrl+C` kopiuje samą nazwę. `Ctrl+Shift+C` kopiuje prawdziwy plik oraz pełną ścieżkę. Okno wyszukiwania pozostaje otwarte, a fokus wraca na ten sam wynik. Dla wyniku usługowego `Ctrl+Shift+C` kopiuje łącze, nie fikcyjny plik.

### AMC-072-07 — Wycinanie lokalnego pliku

Na dowolnej zwykłej liście sesji Pliki lokalne wybierz niepotrzebny plik testowy, naciśnij `Ctrl+X`, przejdź do pustego folderu i naciśnij tam `Ctrl+V`.

Oczekiwane: samo `Ctrl+X` niczego nie usuwa. `Ctrl+V` przenosi prawdziwy plik. Po powrocie do AMC program wykrywa brak starej ścieżki, usuwa nieaktualny wpis i podaje jednoznaczny komunikat.

### AMC-072-08 — Anulowane wycinanie

Na lokalnej liście naciśnij `Ctrl+X`, ale nie wklejaj pliku. Wróć do AMC i dalej nawiguj.

Oczekiwane: plik pozostaje na dysku i na liście. `Ctrl+X` bez późniejszego `Ctrl+V` nie powoduje utraty danych.

### AMC-072-09 — Wycinanie z wyszukiwania

Wyszukaj lokalny plik przez `Ctrl+F` albo `Ctrl+Shift+F`, naciśnij `Ctrl+X`, wklej go do folderu testowego, wróć do wyszukiwania i zamknij je Escape.

Oczekiwane: plik jest przeniesiony przez system. Po zamknięciu wyszukiwania AMC porządkuje swój katalog. `Ctrl+X` dla wyniku TIDAL-a, Apple Music lub WiiM nie tworzy fikcyjnego pliku i zgłasza, że funkcja dotyczy tylko plików lokalnych.

## Poprzedni zestaw regresyjny alpha 71

## Nowości alpha 71

### AMC-071-01 — Kolejność niezależna od dodawania

W jednym pliku dodaj zakładki nie po kolei, na przykład najpierw w `10:00`, potem w `2:00`, a na końcu w `6:00`. Otwórz `Ctrl+B`.

Oczekiwane: zakładki tego pliku występują jako `2:00`, `6:00`, `10:00`, niezależnie od kolejności ich utworzenia.

### AMC-071-02 — Kilka materiałów i sesji

Utwórz zakładki w dwóch plikach albo sesjach, wróć do jednego z nich i otwórz `Ctrl+B`.

Oczekiwane: bieżący materiał znajduje się pierwszy i ma rosnący czas. Pozostałe zakładki są pogrupowane według sesji i tytułu; czasy różnych plików nie są przemieszane w jedną wspólną oś.

### AMC-071-03 — Enter z listy do odtwarzacza

Na liście `Ctrl+B` wybierz zakładkę i naciśnij Enter. Od razu użyj NVDA+strzałka w górę albo Spacji.

Oczekiwane: fokus jest na głównym przycisku odtwarzacza. NVDA czyta obiekt odtwarzacza, a Spacja wstrzymuje lub wznawia; nic nie pozostaje zablokowane na ukrytej liście.

### AMC-071-04 — Escape do tej samej zakładki

Po otwarciu zakładki Enterem naciśnij Escape, a następnie strzałkę w dół i w górę.

Oczekiwane: fokus wraca do tej samej zakładki na liście `Ctrl+B`, a obie strzałki natychmiast czytają sąsiednie rekordy.

### AMC-071-05 — Krótka regresja nazw

Dodaj szybką zakładkę przez `B`, nazwaną przez `Ctrl+Shift+B`, przejdź po nich `Shift+Page Up/Down` i sprawdź wyciszenie komunikatów.

Oczekiwane: funkcje alpha.69–70 działają bez zmian, nazwy są trwałe i nie powstają duplikaty w tej samej sekundzie.

## Poprzedni zestaw regresyjny alpha 70

## Nowości alpha 70

### AMC-070-01 — Dodanie nazwanej zakładki

Otwórz plik w odtwarzaczu, przejdź do wybranego miejsca i naciśnij `Ctrl+Shift+B`. Wpisz nazwę, na przykład „Początek rozmowy”, i zatwierdź Enterem.

Oczekiwane: fokus trafia bezpośrednio do pola „Nazwa zakładki”. Enter zapisuje, Escape anuluje, a po zatwierdzeniu AMC podaje nazwę i czas bez zatrzymywania odtwarzania.

### AMC-070-02 — Nazwanie istniejącej szybkiej zakładki

Dodaj szybką zakładkę klawiszem `B`. Bez przewijania naciśnij `Ctrl+Shift+B`, wpisz nazwę i zatwierdź.

Oczekiwane: istniejąca zakładka otrzymuje nazwę. Na liście nie pojawia się drugi wpis z tym samym czasem.

### AMC-070-03 — Globalna lista nazw

Dodaj kilka nazwanych i kilka szybkich zakładek, po czym otwórz `Ctrl+B`. Nawiguj strzałkami, użyj pierwszej litery nazwy i filtra `Ctrl+K`.

Oczekiwane: nazwana pozycja jest czytana w kolejności: nazwa zakładki, tytuł materiału, czas, sesja, zakładka. Wpis bez nazwy zachowuje krótszy dotychczasowy format. Nazwa działa w nawigacji literowej i filtrze.

### AMC-070-04 — Nawigacja i wyciszenie

Przejdź po nazwanych zakładkach przez `Shift+Page Up/Down`, potem wyłącz „Oznajmiaj nawigację po zakładkach” i powtórz test.

Oczekiwane: przy włączonej opcji AMC podaje nazwę oraz czas osiągniętej zakładki. Przy wyłączonej przechodzi prawidłowo, lecz bez automatycznego komunikatu. `Ctrl+Shift+E` nadal podaje czas na żądanie.

### AMC-070-05 — Trwałość i pełna kopia

Zamknij i uruchom AMC ponownie, sprawdź nazwy przez `Ctrl+B`. Opcjonalnie wykonaj pełny eksport i import na danych testowych.

Oczekiwane: nazwy, czasy i powiązania z materiałami pozostają zachowane. Eksport samych ustawień nadal nie zawiera zakładek.

## Poprzedni zestaw regresyjny alpha 69

## Nowości alpha 69

### AMC-069-01 — Kilka poprzednich zakładek podczas odtwarzania

W jednym dłuższym pliku utwórz co najmniej cztery zakładki. Odtwarzaj plik za ostatnią z nich i kilka razy dość szybko naciśnij `Shift+Page Up`.

Oczekiwane: każde naciśnięcie przechodzi do wcześniejszej zakładki. Odtwarzanie nie powoduje ponownego wyboru tej samej pozycji i nie następuje przejście do innego pliku.

### AMC-069-02 — Kilka następnych zakładek

Po dojściu do pierwszej zakładki kilka razy naciśnij `Shift+Page Down`.

Oczekiwane: każde naciśnięcie przechodzi do następnej zakładki w prawidłowej kolejności. Za ostatnią słychać komunikat o braku następnej zakładki.

### AMC-069-03 — Powrót do rzeczywistej pozycji

Przejdź do zakładki, następnie użyj zwykłego przewijania, skoku cyfrą albo `Ctrl+J`, po czym ponownie naciśnij `Shift+Page Up` lub `Shift+Page Down`.

Oczekiwane: po innym poleceniu AMC wybiera zakładkę względem nowej, rzeczywistej pozycji, a nie względem starej sekwencji.

### AMC-069-04 — Cicha nawigacja

Otwórz Ustawienia → Komunikaty i wyłącz „Oznajmiaj nawigację po zakładkach”. Wróć do odtwarzacza i użyj `Shift+Page Up/Down`.

Oczekiwane: po udanym skoku nie pojawia się automatyczny komunikat „Zakładka” z czasem, ale pozycja naprawdę się zmienia. `Ctrl+Shift+E` nadal odczytuje czas na żądanie. Na krańcu pozostaje komunikat o braku dalszej zakładki.

### AMC-069-05 — Paleta i trwałość ustawienia

W palecie `Ctrl+Shift+K` wyszukaj „komunikaty nawigacji po zakładkach”, naciśnij Enter i sprawdź, czy fokus trafia na właściwy checkbox. Zapisz ustawienia i ponownie uruchom AMC.

Oczekiwane: paleta podaje bieżący stan opcji, ustawienie jest zachowane po restarcie, a `B` nadal dodaje zakładkę i `Ctrl+B` otwiera ich listę.

## Poprzedni zestaw regresyjny alpha 68

## Nowości alpha 68

### AMC-068-01 — Dodawanie szybkich zakładek

Otwórz dłuższy plik w odtwarzaczu. Przejdź mniej więcej do 2 minut i naciśnij `B`, potem przejdź do innego miejsca i ponownie naciśnij `B`.

Oczekiwane: program krótko mówi „Dodano zakładkę” oraz czas. Nie otwiera się żadne dodatkowe okno, odtwarzanie i fokus pozostają w odtwarzaczu.

### AMC-068-02 — Duplikat w tym samym miejscu

Bez zmiany pozycji naciśnij `B` ponownie.

Oczekiwane: program mówi, że zakładka już istnieje; lista nie otrzymuje drugiego wpisu w tej samej sekundzie.

### AMC-068-03 — Nawigacja wewnątrz jednego materiału

W tym samym pliku użyj kilka razy `Shift+Page Up` i `Shift+Page Down`, także przed pierwszą i za ostatnią zakładką.

Oczekiwane: skróty ustawiają dokładne zapisane miejsca i czytają czas. Nie otwierają innego pliku. Na krańcach pojawia się jednoznaczny komunikat o braku poprzedniej albo następnej zakładki.

### AMC-068-04 — Globalna lista Ctrl+B

Dodaj zakładki w co najmniej dwóch sesjach, następnie naciśnij `Ctrl+B`. Nawiguj strzałkami, wpisz początkową literę tytułu i sprawdź filtr `Ctrl+K`.

Oczekiwane: zwykła dostępna lista zawiera zakładki ze wszystkich sesji. Każdy wiersz podaje kolejno tytuł, czas, usługę i słowo „zakładka”. Nawigacja literowa oraz filtr działają jak na innych listach.

### AMC-068-05 — Otwarcie zakładki z innej sesji

Na globalnej liście wybierz zakładkę należącą do innej sesji i naciśnij Enter. Potem naciśnij `Escape`.

Oczekiwane: AMC przełącza właściwą sesję, otwiera materiał i ustawia zapisany czas. `Escape` wraca do globalnej listy na tej samej zakładce.

### AMC-068-06 — Bezpieczne usuwanie

Na liście Zakładek naciśnij najpierw `Shift+Delete`, a następnie zwykły `Delete`. Sprawdź, czy plik nadal istnieje na dysku.

Oczekiwane: `Shift+Delete` wyjaśnia, że z tego widoku nie usuwa pliku. `Delete` usuwa tylko wybraną zakładkę i ustawia fokus na sąsiednim wpisie. Źródłowy plik pozostaje bez zmian.

### AMC-068-07 — Zapis po ponownym uruchomieniu

Pozostaw kilka zakładek, zamknij AMC przez `Alt+F4`, uruchom ponownie i naciśnij `Ctrl+B`.

Oczekiwane: wszystkie pozostawione zakładki, ich czasy i sesje są zachowane. Program nie rozpoczyna odtwarzania samoczynnie.

### AMC-068-08 — B na zwykłej liście i krótka regresja

Wróć do zwykłej listy mediów i naciśnij `B`. Sprawdź też lewą strzałkę, wznowienie pozycji, `Page Up/Down`, `Alt+góra/dół`, OGG i `Delete` w odtwarzaczu.

Oczekiwane: na zwykłej liście `B` nadal przechodzi do tytułu zaczynającego się na B, zamiast tworzyć zakładkę. Pozostałe potwierdzone funkcje nie zmieniają się. `Ctrl+Shift+B` jest na razie celowo wolne i zarezerwowane dla zakładki nazwanej.

## Poprzedni zestaw regresyjny alpha 67

## Nowości alpha 67

### AMC-067-01 — Prawa strzałka i fokus listy

Na lokalnym pliku naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie otwiera się żadne okno systemowe, fokus pozostaje na liście, a NVDA nadal czyta kolejne elementy.

### AMC-067-02 — Menu lokalnego pliku

Otwórz menu kontekstowe na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. Pozostaje „Otwórz w domyślnej aplikacji” oraz właściwe działania AMC.

### AMC-067-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu i `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje działają bez zmian.

## Poprzedni zestaw regresyjny alpha 66

## Nowości alpha 66

### AMC-066-01 — Osobny proces z menu

Na pliku lokalnym otwórz menu kontekstowe i wybierz „Otwórz w…”.

Oczekiwane: systemowy wybór aplikacji staje się aktywnym oknem i NVDA odczytuje jego kontrolki. Po `Escape` fokus wraca do AMC.

### AMC-066-02 — Osobny proces prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę.

Oczekiwane: działanie i fokus są takie same jak z menu. Jeżeli ponownie wystąpi cisza lub konieczność użycia `Alt+Tab`, uznajemy mechanizm za niezgodny i usuwamy funkcję.

### AMC-066-03 — Regresja

Sprawdź lewą strzałkę, wznowienie pozycji po restarcie, `Delete` w odtwarzaczu oraz `Shift+Delete` na liście.

Oczekiwane: pozostałe funkcje nie zmieniają się.

## Poprzedni zestaw regresyjny alpha 65

## Nowości alpha 65

### AMC-065-01 — Otwórz w przez menu

Na pliku lokalnym otwórz menu kontekstowe, wybierz „Otwórz w…” i odczekaj chwilę.

Oczekiwane: systemowy wybór aplikacji otrzymuje fokus, NVDA odczytuje jego kontrolki i można wskazać program albo anulować. Po zamknięciu fokus wraca do tego samego pliku w AMC.

### AMC-065-02 — Otwórz w prawą strzałką

Na tym samym pliku naciśnij prawą strzałkę i odczekaj chwilę.

Oczekiwane: wynik jest taki sam jak z menu. Jeżeli działa menu, ale nie strzałka, zapisz dokładnie tę różnicę — wtedy zachowamy funkcję tylko w menu.

### AMC-065-03 — Anulowanie i regresja

Anuluj systemowe okno przez `Escape`, sprawdź nawigację listy, lewą strzałkę, pamiętanie pozycji i `Delete` w odtwarzaczu.

Oczekiwane: `Escape` nie zamyka AMC, fokus wraca na listę, a potwierdzone funkcje `alpha.63–64` pozostają bez zmian.

## Poprzedni zestaw regresyjny alpha 64

## Nowości alpha 64

### AMC-064-01 — Prawa strzałka nie gubi fokusu

Na pliku lokalnym naciśnij prawą strzałkę, potem strzałkę w dół i w górę.

Oczekiwane: nie pojawia się systemowe okno ani cisza spowodowana utratą fokusu. NVDA pozostaje na liście, a zwykła nawigacja nadal czyta elementy.

### AMC-064-02 — Menu pliku lokalnego

Otwórz menu kontekstowe pliku na liście i w odtwarzaczu.

Oczekiwane: nie ma pozycji „Otwórz w…”. „Otwórz w domyślnej aplikacji” pozostaje dostępne. `Delete` w odtwarzaczu nadal usuwa tylko wpis AMC, a `Shift+Delete` jest dostępne wyłącznie na liście.

### AMC-064-03 — Krótka regresja alpha 63

Sprawdź wznowienie pozycji po restarcie, lewą strzałkę z `kb/s`, `Delete` w odtwarzaczu i `Ctrl+Z`.

Oczekiwane: wszystkie potwierdzone funkcje `alpha.63` działają bez zmian.

## Poprzedni zestaw regresyjny alpha 63

## Nowości alpha 63

### AMC-063-01 — Pozycja ostatniego pliku po ponownym uruchomieniu

Odtwórz dłuższy plik, przejdź co najmniej minutę od początku, wstrzymaj i zamknij AMC przez `Alt+F4`. Uruchom ponownie, użyj `F6`, a następnie `Ctrl+Shift+E`. Wznów odtwarzanie.

Oczekiwane: AMC nie uruchamia dźwięku samoczynnie, ale od razu pokazuje i podaje zapisaną pozycję. Pierwsze wznowienie zaczyna się z tego miejsca, a nie od `0:00`.

### AMC-063-02 — Prawa strzałka bez skojarzenia pliku — historyczne, wycofane w alpha 64

Na lokalnym pliku naciśnij prawą strzałkę. Najlepiej sprawdzić także rozszerzenie, dla którego Windows nie ma poprawnej aplikacji domyślnej.

Ręczny test wykazał utratę czytelnego fokusu NVDA. Aktualne wymaganie opisuje `AMC-064-01`.

### AMC-063-03 — Delete w lokalnym odtwarzaczu

Otwórz kopię pliku testowego w odtwarzaczu i naciśnij `Delete`. Sprawdź dysk, wypowiedź NVDA i następny element. Następnie naciśnij `Ctrl+Z`.

Oczekiwane: plik pozostaje na dysku. AMC usuwa tylko swój wpis, wstrzymuje usuwany element, podaje następny element albo sesję, a `Ctrl+Z` przywraca wpis.

### AMC-063-04 — Fizyczne usuwanie tylko na liście

W odtwarzaczu naciśnij `Shift+Delete`, a potem otwórz jego menu kontekstowe. Następnie wróć na listę i użyj `Shift+Delete` na kopii pliku testowego.

Oczekiwane: w odtwarzaczu skrót niczego fizycznie nie usuwa i nie ma pozycji przenoszenia do Kosza. Na liście pozostaje dotychczasowe pytanie potwierdzające i systemowy Kosz.

### AMC-063-05 — Krótka regresja

Sprawdź lewą strzałkę z `kb/s`, `Alt+F4` z odtwarzacza, `Ctrl+Shift+E/R/T`, OGG i zmianę prędkości.

Oczekiwane: zachowanie `alpha.62` pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 62

## Nowości alpha 62

### AMC-062-01 — Bitrate pliku wcześniej odtwarzanego

Na liście zaznacz odtwarzany wcześniej plik i naciśnij lewą strzałkę.

Oczekiwane: komunikat zawiera rozszerzenie, czas, wartość `kb/s`, dostępne `kHz` i rozmiar.

### AMC-062-02 — Bitrate pliku jeszcze nieodtwarzanego

Dodaj nowy plik, nie uruchamiaj go i od razu naciśnij lewą strzałkę. Powtórz ją drugi raz.

Oczekiwane: AMC odczytuje metadane tylko tego pliku i podaje średni bitrate w `kb/s`. Druga próba korzysta z zapisanego wyniku. Nie rozpoczyna się odtwarzanie ani skanowanie całej listy.

### AMC-062-03 — Alt+F4 z odtwarzacza

Otwórz odtwarzacz przez `F6` i naciśnij `Alt+F4`.

Oczekiwane: cała aplikacja zamyka się od razu i zapisuje stan; nie następuje najpierw powrót do listy. Po ponownym uruchomieniu `Escape` i `Shift+F6` nadal wracają tylko do listy.

## Poprzedni zestaw regresyjny alpha 61

## Nowości alpha 61

### AMC-061-01 — Prawa strzałka i systemowe Otwórz w

Na pliku lokalnym naciśnij prawą strzałkę w głównej liście, Bibliotece i Historii odtwarzania.

Oczekiwane: bez otwierania menu AMC pojawia się bezpośrednio systemowy wybór aplikacji. Można jednorazowo otworzyć plik np. w foobar2000; ewentualna opcja zmiany aplikacji domyślnej zależy od wersji Windows.

### AMC-061-02 — Shift+Delete w odtwarzaczu — historyczne, wycofane w alpha 63

Odtwórz kopię pliku testowego, naciśnij `Shift+Delete`, najpierw wybierz Nie, a przy drugiej próbie Tak.

To zachowanie było testowane w `alpha.61–62`, lecz zostało świadomie wycofane. Aktualne wymaganie opisuje `AMC-063-04`.

### AMC-061-03 — Usunięty plik a Historia odtwarzania

Odtwórz plik, usuń go przez `Shift+Delete`, otwórz `Ctrl+H`, uruchom AMC ponownie i sprawdź historię ponownie.

Oczekiwane: usunięty element nie jest widoczny i nie wraca po restarcie. `Alt+góra/dół` również go pomija.

### AMC-061-04 — Krótka regresja

Sprawdź lewą strzałkę, `Ctrl+Shift+E/R/T`, zwykły `Delete` z `Ctrl+Z`, kopiowanie i naturalne przejście do następnego pliku.

Oczekiwane: zachowanie alpha 60 pozostaje bez zmian.

## Poprzedni zestaw regresyjny alpha 60

## AMC-060-01 — Start, ostatnio odtwarzany i F6

Uruchom AMC po wcześniejszym odtworzeniu pliku, lecz bez jego ponownego włączania. Sprawdź listę i `F6`.

Oczekiwane:

- program nie zaczyna sam odtwarzać i nie przesuwa zaznaczenia;
- zapisany element ma początek „Wstrzymany” albo „Ostatnio odtwarzany”;
- `F6` otwiera ten element w odtwarzaczu.

## AMC-060-02 — Jawne pytania o czas

Sprawdź `Ctrl+Shift+E`, `Ctrl+Shift+R` i `Ctrl+Shift+T` najpierw na liście, potem po `F6`.

Oczekiwane:

- w obu miejscach słychać odpowiednio czas od początku, pozostały i całkowity;
- działanie nie zależy od wyciszenia automatycznych komunikatów transportu.

## AMC-060-03 — Dwie niezależne historie

Na liście otwórz kolejno Bibliotekę, Kolejkę i Ulubione, po czym użyj `Alt+lewo` oraz `Alt+prawo`. Następnie odtwórz trzy różne pliki A, B i C, otwórz odtwarzacz i użyj `Alt+dół`, `Alt+dół`, `Alt+góra`.

Oczekiwane:

- boczny Alt cofa i ponawia widoki bieżącej sesji;
- w odtwarzaczu `Alt+dół` wybiera B, potem A, a `Alt+góra` wraca do B;
- każdy plik zachowuje własną pozycję;
- po ponownym uruchomieniu historia odtwarzania pozostaje, natomiast stos Wstecz/Naprzód może być pusty.

## AMC-060-04 — Widok Historia i lista źródłowa

Naciśnij `Ctrl+H`, sprawdź kolejność wpisów, następnie wróć do odtwarzacza i użyj `Page Up/Down`.

Oczekiwane:

- Historia jest uporządkowana od najnowszego wpisu i nie zawiera powtórzeń;
- `Page Up/Down` wybiera sąsiada odtwarzanego pliku na liście źródłowej, nie sąsiada z historii.

## AMC-060-05 — Strzałki lokalne we wszystkich widokach

Na pliku lokalnym użyj lewej i prawej strzałki w głównym katalogu, Bibliotece, Kolejce i Ulubionych.

Oczekiwane:

- lewa podaje krótkie informacje, a prawa otwiera menu działań i ustawia w nim fokus w każdym z lokalnych widoków;
- w sesjach nielokalnych strzałki zachowują zwykłą semantykę listy.

## AMC-060-06 — Delete i Shift+Delete

Na kopiach testowych plików sprawdź zwykły `Delete`, `Ctrl+Z`, a następnie `Shift+Delete`: najpierw odpowiedź Nie, potem Tak. Powtórz z wielokrotnym zaznaczeniem i z aktualnie otwartym plikiem.

Oczekiwane:

- `Delete` usuwa wpis z katalogu AMC i daje się cofnąć bez dotykania dysku;
- `Shift+Delete` wymaga potwierdzenia, po odpowiedzi Nie niczego nie zmienia, a po Tak zwalnia plik, przenosi udane pliki do systemowego Kosza i usuwa je z AMC;
- `Ctrl+Z` nie przywraca fizycznie usuniętych plików.

## AMC-060-07 — Kopiowanie wielokrotnego zaznaczenia

Zaznacz kilka plików. Wklej wynik `Ctrl+C` do edytora, a wynik `Ctrl+Shift+C` do edytora i do pustego folderu w Total Commanderze lub Eksploratorze.

Oczekiwane:

- `Ctrl+C` daje wszystkie nazwy, po jednej w wierszu;
- `Ctrl+Shift+C` daje pełne ścieżki w tekście i pozwala wkleić wszystkie fizyczne pliki.

## AMC-060-08 — Przełączanie sesji i regresja

Przejdź w każdej sesji do innego widoku, po czym przełączaj ją przez `Ctrl+1–9`. Sprawdź wyrywkowo OGG, prędkość, pasek, `Alt+Enter`, Kolejkę i naturalne przejście do następnego pliku.

Oczekiwane: komunikat zaczyna się od numeru i nazwy sesji, potem podaje przywrócony widok i element, np. „4, Pliki lokalne, Biblioteka…”. Pozostałe funkcje nie mają regresji.
