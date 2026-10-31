# AMC Python — uruchamianie i sprawdzanie bieżącego przyrostu

To rozwijany równoległy interfejs pełnego AMC. Nie jest jeszcze zamiennikiem wszystkich funkcji programu. Katalog i nazwa uruchamiacza `wxlite` są historyczne; nie oznaczają decyzji o ograniczeniu docelowego zakresu do dwóch sesji.

## Zasada nieuciążliwego testowania

Korekta użytkownika z 27 października 2026: podczas zwykłego rozwoju domyślnie
uruchamiamy wyłącznie celowane testy bez widocznego interfejsu, związane ze
zmienianym obszarem. Pełna regresja GUI, testy otwierające okna, przejmujące
fokus lub schowek oraz szerokie testy interfejsu Sonosa i WiiM nie są rutynowym
krokiem po małej zmianie.

Taką regresję wykonujemy na wyraźną prośbę użytkownika. Przed istotnym wydaniem
albo zmianą wysokiego ryzyka można ją zaproponować, ale najpierw trzeba
uprzedzić o możliwym przeszkadzaniu w pracy i uzyskać zgodę. Build, testy
celowane, pełna regresja bez interfejsu, testy GUI, instalacja i publikacja są
osobnymi wynikami i należy je raportować oddzielnie.

## Start aplikacji i jedna instancja

`AMC-wx-Lite.cmd` jest wyłącznie uruchamiaczem deweloperskim w repozytorium.
Nie trafia do paczki użytkownika i nie jest sposobem startu ani aktualizacji
programu. Gotowy pakiet uruchamia się wyłącznie przez `AMC.exe`, które zawsze
korzysta z runtime'u i LiteHosta dołączonych do tej samej, zweryfikowanej
paczki. `AMC.exe --sprawdz-pakiet` sprawdza zgodność wersji oraz sumy plików
potrzebnych przy starcie; `AMC.exe --sprawdz` uruchamia diagnostykę interfejsu.

Odbiór docelowego launchera:

Stała, automatycznie aktualizowana i weryfikowana paczka użytkowa znajduje się w:

`D:\Projekty Codex\AMC Python\AMC.exe`

Dokładną tożsamość bieżącego programu podaje pozycja **Pomoc, Wersja programu**.
Etykieta zawiera lokalną datę, rewizję źródeł i skrót zawartości paczki. Jeżeli
kompilacja nie jest taka sama jak w ostatnim komunikacie narzędzia wydania,
zatrzymaj odbiór zamiast szukać kolejnego `AMC.exe` w `.test-output`. Jedyną
ścieżką użytkową pozostaje powyższy stały plik.

Przygotowane przyrosty trafiają najpierw do osobnych katalogów testowych.
Stała paczka jest podmieniana dopiero po potwierdzeniu, że AMC Python nie
nagrywa. Nie przełączaj jej ręcznie w czasie trwającego nagrywania. Pierwszy
żywy odbiór wykonaj kolejno według punktów poniżej.

1. Uruchom `AMC.exe` bez otwierania terminala i bez wybierania interpretera.
   Okno ma od razu być zmaksymalizowane, aktywne i na pierwszym planie.
2. Bez trwającego nagrywania uruchom ten sam plik ponownie. Jeśli numer
   kompilacji jest ten sam, nie może powstać drugi proces aplikacji ani drugi
   interfejs. Istniejące okno ma wrócić na pierwszy plan, pozostać
   zmaksymalizowane i zachować stan.
3. Po podmianie plików tej samej stałej instalacji uruchom `AMC.exe` ponownie.
   Inny numer kompilacji ma wysłać staremu oknu zwykłe żądanie zamknięcia,
   zaczekać na pełne zwolnienie jednej instancji i dopiero wtedy uruchomić
   nową kompilację. Nie wolno zabijać procesu. Przypadkowe uruchomienie starego
   `AMC.exe` z `.test-output` nie może zamknąć ani zastąpić stałej instalacji.
4. Podczas rzeczywistego nagrywania powtórz próbę podmiany kompilacji.
   Stare okno ma odmówić zamknięcia na podstawie żywego stanu właściciela
   nagrania, wrócić na pierwszy plan i zachować nagranie. To samo obowiązuje,
   gdy stanu nagrywania nie da się potwierdzić. Aktualizator może podmienić
   pakiet dopiero po bezpiecznym zamknięciu wszystkich jego procesów.
5. Usuń z procesu testowego systemowy Python i zmienne `PYTHONPATH` oraz
   `AMC_WX_LITE_PYTHON`. `AMC.exe` nadal ma uruchomić dołączony runtime.
6. Podstaw celowo niezgodne wersje interfejsu i LiteHosta albo uszkodzony
   manifest. Program ma odmówić startu z krótkim, dostępnym komunikatem, bez
   uruchamiania przypadkowej starszej kopii.
7. Zastosuj poprawną aktualizację przez `AMC-Python-Updater.exe`. Po
   weryfikacji i podmianie `AMC.exe` ma uruchomić nową wersję; błąd podmiany
   ma przywrócić poprzednią, działającą całość.

Zwykły odbiór deweloperski wykonuje jedno polecenie:

`wxlite\tools\Promote-AMCPythonTestBuild.ps1`

Skrypt odmawia pracy przy niezatwierdzonych zmianach śledzonych przez Git,
uruchamia testy, buduje wszystkie składniki, tworzy pełną paczkę, sprawdza jej
sumy, bezpiecznie stosuje aktualizację, ponownie sprawdza stałą instalację i
dopiero wtedy ją uruchamia. Jeżeli stary AMC nie zgodzi się zamknąć, instalacja
albo końcowa tożsamość nie pasuje, stara wersja nie jest uruchamiana jako rzekomo
nowa. Ostatni jednoznaczny kwit zapisuje się w `.test-output\LAST_TEST_BUILD.txt`
oraz `.test-output\LAST_TEST_BUILD.json`.

Odbiór równoległego Radia i Multimediów wykonaj dopiero wtedy, gdy można
bezpiecznie przerwać bieżący odsłuch. Uruchom Radio, przejdź do Plików i
uruchom plik. Oba źródła mają nadal być słyszalne. Przełączaj się między tymi
sesjami: Spacja ma wstrzymywać i wznawiać wyłącznie aktywną sesję. W każdej
sesji wybierz przez `Shift+A` inne urządzenie; zmiana wyjścia jednego toru nie
może zatrzymać drugiego. Na końcu zatrzymaj Pliki i potwierdź, że Radio nadal
gra, po czym zatrzymaj Radio. Powtórz `Ctrl+M` w każdej sesji: pierwsze
naciśnięcie ma wyciszyć tylko jej odsłuch, a drugie przywrócić poprzednią
głośność bez zatrzymania drugiego toru ani nagrywania.

Biblioteka, Ulubione, Playlisty i Kolejka są równorzędnymi widokami sesji.
Przełącza się je odpowiednio przez `Ctrl+L`, `Ctrl+U`, `Ctrl+P` i `Ctrl+Q`.
Na każdym z tych głównych widoków `Escape` nie może samoczynnie przenosić do
Biblioteki ani niczego ogłaszać. W drzewie folderów wyłącznie `Backspace`
przechodzi do folderu nadrzędnego; `Escape` nie zmienia poziomu. `Escape` nadal
czyści aktywny filtr oraz zamyka rzeczywisty podgląd lub widok podrzędny, na
przykład listę trwających nagrań, historię nagrywania albo harmonogramy.
Sprawdź tę zasadę osobno w każdej sesji, która udostępnia dany widok.

W każdym z sześciu widoków naciśnij `Ctrl+Shift+S`. Powinna otworzyć się natywna
lista „Sesje”, a NVDA ma czytać wyłącznie kolejno: „Ctrl+1, Pliki lokalne”,
„Ctrl+2, Radio internetowe”, „Ctrl+3, Podcasty i YouTube”, „Ctrl+4, TIDAL”,
„Ctrl+5, WiiM” i „Ctrl+6, Sonos”. Bieżąca sesja jest wybrana przy pierwszym
otwarciu i przy każdym ponownym otwarciu. Sprawdź strzałki, Enter, Escape oraz
powrót fokusu po obu sposobach zamknięcia. Powtórz poleceniem „Lista sesji” z
dodatku NVDA; w mowie nie może pojawić się `SessionId`, nazwa klasy, rekord ani
identyfikator. Poprzedni i następny preset z dodatku mają pomijać puste miejsca
i zawijać listę, nie otwierając po drodze okna presetów.

`Ctrl+0` nie ma już żadnej funkcji. W Ustawieniach, na karcie Ogólne, zmień
„Kolejność sesji i skrótów Ctrl+1–9” przyciskami albo przez Alt+strzałkę w górę
i w dół. Po zapisaniu menu Widok, lista sesji, paleta poleceń oraz bezpośrednie
skróty Ctrl+1–6 muszą od razu pokazywać i wykonywać tę samą kolejność.
`Ctrl+Page Up` i `Ctrl+Page Down` przechodzą odpowiednio do poprzedniej i
następnej sesji według tej kolejności, z zawijaniem na końcach.

Na tej samej karcie zmień „Wyświetlaj po uruchomieniu” na „Lista sesji”,
zapisz i uruchom AMC Python ponownie w bezpiecznym momencie. Po gotowości
silnika ma otworzyć się zwykłe okno „Wybór sesji” z fokusem na ostatniej
sesji. Enter przechodzi do zaznaczonej sesji, Escape lub Anuluj pozostawia
ostatnią sesję. Wróć do wariantu „Ostatnia używana sesja” i sprawdź, że po
następnym starcie nie pojawia się dodatkowy dialog. NVDA nie może odczytać
`MediaList`, `SessionList`, nazwy klasy ani rekordu modelu.

Naciśnij `Ctrl+Shift+K` oraz osobno „Otwórz paletę poleceń” z dodatku NVDA.
Fokus ma trafić do pola „Filtr poleceń”. Wpisz bez polskich znaków dwa słowa,
na przykład „paleta polecen”; liczba wyników ma się zmienić, a strzałka w dół
ma przejść do natywnej listy „Polecenia”. Lista może zawierać tylko nazwy i
skróty użytkowe, nigdy `Action`, identyfikator polecenia ani rekord modelu.
Enter wykonuje zaznaczoną czynność, Escape niczego nie wykonuje, a po obu
wyjściach fokus wraca do listy albo odtwarzacza aktywnej sesji. Powtórz w
Radiu i TIDAL-u: paleta TIDAL-u nie może oferować poleceń nagrywania Radia.

W odtwarzaczu Plików `I` ustawia początek, a `O` koniec zaznaczonego
fragmentu. Sprawdź także nagranie otwarte z `Alt+Shift+R`, gdy Radio nadal
działa w tle: oba klawisze muszą korzystać z toru Plików, nie z ostatniego
odpytywanego stanu Radia. Plik iCloud lub OneDrive oznaczony „zawsze zachowuj
na tym urządzeniu” ma dać się zaznaczyć i edytować bez fałszywego żądania
pobrania. `Alt+Page Up` i `Alt+Page Down` przechodzą po granicach fragmentu,
natomiast `Shift+Page Up` i `Shift+Page Down` przechodzą po zapisanych
zakładkach. `B` dodaje szybką zakładkę, a `Ctrl+B` otwiera zbiorczą listę
wszystkich zakładek. `Ctrl+Shift+B` jest zależne od miejsca: na liście otwiera
zakładki zaznaczonego pliku, a w odtwarzaczu otwiera pole „Nazwa zakładki”.
Wpisz nazwę, zatwierdź Enterem i sprawdź trzy komunikaty: dodanie nowej,
nadanie nazwy istniejącej szybkiej zakładce oraz informację o istniejącej już
nazwanej zakładce. Anuluj dialog klawiszem Escape i potwierdź powrót fokusu do
odtwarzacza bez zapisu.

Plik `AMC-Python-Updater.exe` służy obecnie do bezpiecznego zastosowania
przygotowanej paczki z pełnym manifestem SHA-256. Nie uruchamiaj go ręcznie na
produkcyjnym katalogu bez przygotowanej paczki aktualizacji. Automatyczne
sprawdzanie i pobieranie nowych wersji zostanie podłączone do docelowego
kanału wydań AMC Python w kolejnym etapie.

Aktualizator najpierw sprawdza cały manifest i sumy plików, a dopiero potem
prosi właściwe okno AMC Python z aktualizowanego katalogu o zwykłe zamknięcie.
Nie kończy procesu wymuszenie. Gdy trwa nagrywanie albo jego stanu nie da się
potwierdzić, interfejs odmawia zamknięcia, aktualizacja nie dotyka żadnego
pliku i zwraca kod 3. Po udanej podmianie parametr `--launch AMC.exe` uruchamia
już nową wersję z tego samego, stałego katalogu.

## Starszy pakiet testowy — Podcasty i Shift+R

Do czasu podania nowej ścieżki pakietu z `AMC.exe` starszy przyrost uruchamiał
się dokładnie tak:

`D:\Projekty Codex\AMC Python - test opcje podcastow 2026-10-09\AMC-wx-Lite.cmd`

Po ponownym uruchomieniu skrypt wybiera jedyną kanoniczną kopię silnika z
folderu `host`. Foldery `host-next*` nie są częścią bieżącego pakietu, żeby
restart nie mógł przypadkowo połączyć nowego interfejsu ze starym silnikiem.
Sprawdź kolejno:

1. W Podcastach lewa strzałka czyta krótką informację o zaznaczonym źródle
   albo odcinku; nie może czytać nazwy klasy, rekordu ani identyfikatora.
2. `Ctrl+Shift+U` przełącza Ulubione podcastu lub odcinka.
3. `Ctrl+Shift+L` na podcaście zmienia jego Bibliotekę. Na odcinku ma zmienić
   podcast nadrzędny; po usunięciu otwartego podcastu program wraca do głównej
   listy zamiast zostawiać odłączony widok.
4. `Ctrl+Shift+Q` na odcinku dodaje go do kolejki podcastów albo usuwa.
   `Ctrl+Q` ma pokazać zgodny stan także po ponownym uruchomieniu.
   `Ctrl+Shift+Enter` osobno przełącza „Odtwarzaj jako następne”.
5. W Radiu zaznacz stację i naciśnij `Shift+R`. Powinien od razu otworzyć się
   formularz nowego planu z wybraną tą stacją. `Ctrl+Shift+H` nadal otwiera
   listę wszystkich harmonogramów.

Nie uruchamiaj równolegle starego głównego AMC podczas testu zapisów profilu.
Sonos zostaje do osobnego odbioru na obudzonych urządzeniach.

## Dodawanie i import stacji radiowych — odbiór 13 października

Na czas sprawdzania zapisu zamknij stare główne AMC; jego równoległe działanie
ma celowo zablokować zmianę wspólnego profilu.

1. W sesji Radio naciśnij `Ctrl+N`, wpisz nazwę oraz pełny adres `http://` lub
   `https://` i zatwierdź. Program ma otworzyć Bibliotekę, ustawić fokus na
   nowej stacji i powiedzieć „Dodano stację do Biblioteki” z jej nazwą.
2. Powtórz `Ctrl+N` z tym samym adresem, ale inną nazwą. Nie może powstać
   duplikat; istniejący wiersz ma otrzymać nową nazwę, a NVDA ma powiedzieć
   „Zaktualizowano stację”.
3. W Radiu naciśnij `Ctrl+O` i wybierz playlistę M3U, M3U8, PLS, XSPF albo
   JSON. Nowe stacje mają zostać dopisane, istniejąca stacja katalogowa spoza
   Biblioteki ma wrócić bez utraty dotychczasowej nazwy, a duplikaty mają być
   policzone jako pominięte.
4. Po ponownym uruchomieniu sprawdź Bibliotekę w kolejności własnej oraz według
   dodania. Nowe i przywrócone stacje muszą pozostać na listach, bez nazw klas,
   rekordów i identyfikatorów technicznych w mowie NVDA.
5. Dodaj co najmniej dwie stacje do Ulubionych i ustaw ich kolejność przez
   `Alt+1`, `Alt+2` oraz `Alt+3`. Naciśnij `Ctrl+E` najpierw na liście, a potem
   w odtwarzaczu. Zapisz osobno M3U oraz „Ulubione VRadio” JSON. Po każdym
   zapisie NVDA ma powiedzieć wyłącznie „Wyeksportowano ulubione stacje” i
   liczbę. Pliki mają zachować wybraną kolejność, dać się ponownie zaimportować
   przez `Ctrl+O` i nie mogą zawierać identyfikatorów technicznych AMC.
6. Usuń wszystkie stacje z Ulubionych i ponów `Ctrl+E`. Program ma powiedzieć:
   „Ulubione nie zawierają stacji, które można wyeksportować” i nie tworzyć
   pustego pliku.

## TIDAL, WiiM i Sonos — odbiór 10 października

TIDAL:

1. `Ctrl+4` otwiera sesję TIDAL.
2. Na albumie, wykonawcy lub playliście `Ctrl+Shift+L` ma zmienić stan
   Biblioteki i podać jeden końcowy komunikat.
3. Na utworze lub materiale wideo `Ctrl+Shift+U` ma zmienić stan Ulubionych.
4. Zaznacz kilka utworów, także nieprzylegających przez `Ctrl+Spacja`.
   `Ctrl+Shift+Q` ma dodać lub usunąć całe zaznaczenie z prywatnej Kolejki.
   `Ctrl+Q` ma pokazać tę samą kolejność także po ponownym uruchomieniu.
5. Enter na utworze lub albumie ma przekazać odtwarzanie do oficjalnej
   aplikacji TIDAL. Nie oczekuj od tej drogi własnego pełnego odtwarzacza AMC.
6. Otwórz albumy dodane w różnych terminach. `Alt+1` ustawia porządek według
   dodania z najnowszym albumem na początku, `Alt+2` porządek alfabetyczny, a
   `Alt+3` kolejność własną. Wybór ma pozostać po wyjściu, ponownym otwarciu i
   restarcie. Powtórz dla Biblioteki, Ulubionych i Playlist; każdy widok ma
   pamiętać swój tryb osobno. W albumie, wykonawcy i Kolejce te skróty nie mogą
   przełączyć widoku Plików ani udawać sortowania kolekcji.
7. Na albumie naciśnij prawą strzałkę: przy jednym celu aplikacja ma od razu
   przejść do wykonawcy, bez zbędnej zapowiedzi „menu kontekstowe”. Tak samo
   wykonawca z jednym dostępnym celem od razu pokazuje jego albumy. Dopiero na
   utworze z kompletnymi dwiema relacjami pojawia się natywne menu z pozycjami
   „Przejdź do albumu” i „Przejdź do wykonawcy”. Enter na samym wykonawcy
   nadal otwiera trzy kategorie: Albumy, Utwory i Podobni wykonawcy. Po
   anulowaniu rzeczywistego menu fokus wraca na ten sam wiersz; Backspace z
   otwartej relacji wraca we właściwe miejsce. NVDA nie może czytać
   identyfikatorów TIDAL.

WiiM:

1. Obudź urządzenie i naciśnij `Ctrl+5`.
2. Sprawdź wykrycie, aktywację, Enter, Spację, następny/poprzedni, głośność,
   przewijanie, szybkie informacje i powrót fokusu po zmianie sesji.
3. Sprawdź dotychczasowe przełączanie `F6` między odtwarzaczem i listą
   urządzeń. `Ctrl+Alt+P` ma otworzyć 12 natywnych miejsc urządzenia. Cyfry
   wybierają konkretne miejsce, Page Up i Page Down przechodzą wyłącznie po
   zajętych miejscach, a Enter lub Spacja uruchamia zajęty preset. Puste
   miejsce nie zamyka okna. Preset TIDAL/Spotify Connect bez adresu ma
   wyjaśnić, że dźwięk trzeba wysłać z aplikacji, zamiast udawać sukces.
4. Po udanym uruchomieniu presetu ma pojawić się odtwarzacz WiiM, krótki
   komunikat z numerem i nazwą presetu oraz aktualny stan urządzenia. Po
   anulowaniu listy fokus ma wrócić do listy urządzeń albo odtwarzacza.
5. Pełne AMC nie udaje Biblioteki ani Ulubionych konta WiiM, których lokalne
   API urządzenia nie udostępnia. `Ctrl+L` otwiera uczciwą **Bibliotekę źródeł
   WiiM** z dokładnie dwiema pozycjami: „Presety urządzenia” i „Strumienie
   zapisane w AMC”. NVDA ma czytać tylko te etykiety i rodzaj „źródło”, bez
   identyfikatorów. Enter na pierwszej pozycji otwiera natywne presety, a na
   drugiej zapisane strumienie.
6. W Bibliotece źródeł wybierz **Strumienie zapisane w AMC**. NVDA
   ma czytać nazwę i rodzaj „strumień sieciowy”, nigdy adres URL ani
   identyfikator. Enter uruchamia strumień na tym urządzeniu i pokazuje jego
   odtwarzacz. Backspace ze strumieni wraca do Biblioteki źródeł, a kolejny
   Backspace dokładnie na wcześniejsze urządzenie.
7. Jeżeli lista Strumieni WiiM jest pusta, program mówi „Strumienie WiiM,
   zero elementów”. `Ctrl+N` dodaje nazwę i adres, `F2` edytuje jeden wybrany
   wpis, a Delete usuwa zaznaczone wpisy wyłącznie z profilu AMC. Usuwanie ma
   jawnie powiedzieć, że WiiM Home pozostaje bez zmian. Po każdej operacji
   lista wraca na zmieniony wpis; adres wolno pokazać tylko w jawnym oknie
   edycji F2, nigdy w zwykłej nazwie dostępnościowej. Na liście urządzeń F2
   i Delete nie są przejmowane przez obsługę strumieni. `Ctrl+O` otwiera dialog
   playlisty lub pliku ze strumieniami i importuje M3U/M3U8/PLS istniejącym
   importerem AMC, bez dublowania adresów. Starsze `Ctrl+Shift+O` pozostaje
   zgodnym aliasem, ale nie jest pokazywane w menu.
   `Ctrl+E` zapisuje M3U do WiiM Home w dokładnie widocznej kolejności i bez
   technicznych identyfikatorów. `Alt+1`, `Alt+2`, `Alt+3` wybierają kolejno:
   najnowsze, alfabetycznie i kolejność własną; aktualny wybór jest polem
   zaznaczonym w menu i przeżywa restart. Skróty `Ctrl+Shift+1–0/-/=` nadal
   uruchamiają mapowane presety AMC.

Wynik odbioru 10 października: urządzenie i stan są prawidłowo widziane,
stację można uruchomić i zatrzymać. Natywne presety pod `Ctrl+Alt+P` i
odtwarzanie zapisanych Strumieni WiiM z Biblioteki źródeł są podłączone technicznie
i czekają na powyższy odbiór na obudzonym urządzeniu. Do przeniesienia
pozostaje odbiór zarządzania listą Strumieni na żywym urządzeniu oraz
pozostałe opcje sprzętowe WiiM.

Sonos:

Odbiór logowania z publicznym brokerem w AMC Test:

1. Sprawdź, że tytuł okna to `AMC Test — 25.10.2026, 19:30`, a następnie
   naciśnij `Ctrl+6` przy rozłączonym koncie.
2. Lista ma zawierać jeden wiersz „Rozpocznij logowanie do Sonos”. NVDA ma
   przeczytać wyłącznie tę nazwę i użytkowy opis „Enter otwiera konto Sonos”,
   bez nazwy klasy, identyfikatora lub rekordu. Enter otwiera okno konta.
3. Przed rozpoczęciem logowania okno ma udostępniać „Rozpocznij logowanie” i
   „Zamknij”. Nie może pokazywać „Wyloguj konto”. Instrukcja i stan pozostają
   polami tylko do odczytu w kolejności Tab.
4. Po wybraniu „Rozpocznij logowanie” fokus ma trafić na „Otwórz stronę
   logowania”. Dalej dostępne są: otwarcie, kopiowanie adresu, sprawdzenie,
   anulowanie i zamknięcie. Nie może być przycisku wylogowania.
5. Po anulowaniu i ponownym otwarciu sprawdź ten sam stan początkowy oraz
   prawidłowy powrót fokusu. Zwykły błąd sieci nie może zmienić listy w
   komunikat o brakującym koncie.
6. Na oficjalnej stronie użyj tego samego zwykłego konta, które w aplikacji
   Sonos widzi system. Nie zapisuj loginu ani hasła w raporcie. Jeżeli Sonos
   ponownie pokaże błąd po „Kontynuuj” na wyborze gospodarstwa domowego, zapisz
   tylko etap i tekst użytkowy; nie traktuj tego jako dowodu wywołania callbacku.
7. Po powodzeniu wróć do AMC i wybierz „Sprawdź logowanie”. Dopiero po
   połączeniu mają pojawić się „Wyloguj konto” oraz „Głośniki i grupy Sonos”.

Odbiór sterowania po połączeniu konta:

1. Obudź głośniki i naciśnij `Ctrl+6`.
2. Lista ma czytać wyłącznie nazwy grup i stan użytkowy. Enter wybiera grupę,
   pozostaje na liście i po odczycie pokazuje Bibliotekę Sonos. Sam wybór grupy
   nie może uruchomić ostatniej stacji ani otworzyć odtwarzacza.
3. W Bibliotece, Ulubionych, Playlistach Sonos i Moich stacjach sprawdź
   Spację. Ma wstrzymywać lub wznawiać aktywną grupę bez przechodzenia do
   odtwarzacza. `Ctrl+Spacja` nadal zaznacza nieprzylegający wiersz i nie może
   sterować odtwarzaniem.
4. Przy włączonej wspólnej opcji „Pozostawaj na liście po uruchomieniu stacji
   lub materiału Enterem” uruchom materiał Sonos. Fokus ma pozostać na liście.
   `F6` jawnie pokazuje odtwarzacz i rozpoczyna odświeżanie stanu wybranej
   grupy; ponowne `F6` wraca do listy.
5. Sprawdź następny/poprzedni, głośność, przewijanie, szybkie informacje oraz
   zachowanie po zmianie sesji.
6. Nazwy klas, identyfikatory i dane uwierzytelniające nie mogą pojawić się w
   mowie NVDA, schowku ani komunikatach.

Wynik odbioru 10 października: wykrywanie grupy i odczyt stanu działają, ale
sterowanie jest zablokowane komunikatem „Trwa odczyt informacji. Powtórz skrót
za chwilę”. Ponowienie po chwili również nie wykonywało polecenia. Poprawka
techniczna jest już w paczce: odczyt i transport nie nakładają się, a polecenie
naciśnięte podczas odczytu czeka w kolejce. Chwilowe przeciążenie hosta jest
ponawiane w tle bez tego komunikatu.

Odbiór poprawki na żywo:

1. Naciśnij Enter na grupie i od razu, jeszcze podczas pierwszego odczytu,
   naciśnij Spację. Po zakończeniu odczytu Spacja ma się wykonać bez ponownego
   naciskania i bez komunikatu „Trwa odczyt informacji”, a program ma pokazać
   Bibliotekę Sonos zamiast odtwarzacza.
2. Powtórz próbę z następnym/poprzednim, zmianą głośności i wyciszeniem.
3. Otwórz odtwarzacz klawiszem `F6` i pozostaw go co najmniej kilka cykli
   odświeżania. Polecenia nadal mają działać, a automatyczny odczyt nie może
   ich blokować.
4. Wróć do listy i szybko wybierz inną grupę. Ma obowiązywać ostatni świadomy
   wybór, bez spóźnionego powrotu do poprzedniej grupy.

Trzeba też odebrać pełną Bibliotekę, Ulubione, wybór materiałów i presety wraz
ze skrótami z głównego AMC. Widoczne Radio Opole nie zaczęło faktycznie grać
mimo wcześniejszej próby jego uruchomienia.

Brak urządzeń lub uśpione urządzenia nie jest zaliczeniem ani błędem kodu.
Zapisz wtedy wynik jako „nieprzetestowane na żywo”. Spotify nie należy do tego
etapu. Moduł aktualizacji będzie sprawdzany osobno po weryfikacji „Sygnalisty”:
`https://github.com/michaldziwisz/sygnalista`.

## Wyszukiwanie i odczyt nagranych plików

`Ctrl+F` pyta bieżącą sesję lub usługę, a `Ctrl+Shift+F` przeszukuje wszystkie
obsługiwane zapisane katalogi. Oba polecenia otwierają osobne natywne okno;
`Ctrl+K` nadal filtruje tylko bieżącą listę.

Odbiór wyszukiwania z NVDA:

1. Na liście Plików naciśnij `Ctrl+F`. Fokus ma stanąć w polu „Szukaj w Pliki
   lokalne”. Wpisz fragment nazwy bez polskich znaków odpowiadający nazwie z
   ogonkiem i naciśnij Enter. Lista ma znaleźć wynik niezależnie od wielkości
   liter i znaków diakrytycznych.
   Po ponownym otwarciu pustego pola strzałka w dół ma wybrać najnowsze
   zapytanie z historii, kolejne naciśnięcia — starsze, a strzałka w górę ma
   wracać aż do pustego pola. Historia zakresu globalnego jest osobna.
2. Strzałki mają czytać nazwę, rodzaj i szczegóły bez nazw klas, identyfikatorów
   i `nieznane`. Lewa strzałka czyta krótkie informacje. `Ctrl+C` kopiuje same
   nazwy, a `Ctrl+Shift+C` adresy. Przy wyłączonym zewnętrznym ogłaszaniu
   poleceń schowka pozostaje jeden celowy polski komunikat AMC.
3. Zaznacz zakres Shiftem i nieprzylegające wiersze przez `Ctrl+Spacja`.
   Sprawdź `Ctrl+Shift+Q`, `Ctrl+Shift+Enter`, `Ctrl+Shift+U`, `Ctrl+Shift+L`,
   `Ctrl+Shift+P`, `Ctrl+Alt+Shift+P`, `Alt+Enter`, `Ctrl+D` i `Ctrl+S` w
   pasujących sesjach. Wyniki z różnych usług nie mogą zostać potraktowane
   jako jedna operacja.
4. Enter ma zamknąć okno i ustawić fokus na dokładnie tym elemencie w jego
   prawdziwej liście. Escape ma wrócić bez zmiany bieżącej sesji i zaznaczenia.
   Puste zapytanie mówi „Wpisz tekst do wyszukania”; brak wyniku mówi „Brak
   wyników. Zmień wyszukiwany tekst” i zostawia fokus w zaznaczonym polu.
5. `Ctrl+Shift+F` ma w szczegółach podawać użytkową nazwę sesji. Sprawdź wynik
   z Plików, Radia, Podcastów i TIDAL. WiiM i Sonos nie udają katalogu muzyki:
   wyszukiwanie globalne je pomija, a bieżące podaje jasną informację o braku
   katalogu.

Na tym etapie przeszukiwany jest kompletny lokalny cache. Zdalne katalogi na
żywo pozostają do domknięcia. Relacje TIDAL pod prawą strzałką są podłączone,
ale ich odbiór z kontem i NVDA nie może być zaliczony samym testem lokalnym.

Uruchom jedno nagranie i naciśnij `Alt+R`. Po ukończeniu aktualizacji natywnej
listy ostatnim pełnym komunikatem ma być „Nagrywane, 1 z 1, nazwa pliku”. Nie
może skończyć się na samym „Nagrywane, 1 z 1”, ujawnić identyfikatora ani
przemilczeć nazwy jedynego wiersza. Strzałki na gotowej liście nadal czytają
krótko od nazwy pliku, bez przedrostka „Nagrywane”. Powtórz próbę po Escape i
ponownym `Alt+R`, aby sprawdzić przywrócenie zaznaczenia.

W `Alt+Shift+R` zwykły ukończony plik nie powinien zaczynać się od słowa
„Nagrane”, ponieważ taki jest już kontekst listy. Odczyt ma zaczynać się od
nazwy stacji lub pliku. Osobno sprawdź wpisy przerwane, nieudane, zatrzymane i
z brakującym plikiem — ich istotny stan nie może zniknąć.

Wyjaśnienie dodatkowego „Copy”: na komputerze testowym wypowiada je globalna
funkcja **Clipboard command announcement** dodatku **NVDA Extension Global
Plugin**, nie lista wxPython. W pliku `NVDAExtensionGlobalPluginAddon.ini`
odpowiada jej włączone `FakeClipboardAnnouncement = 1`. Funkcja przechwytuje
`Ctrl+C`, przekazuje je do AMC i po zmianie schowka sama mówi „Copy”, dlatego
aplikacja nie może usunąć tego komunikatu własnym `EVT_CHAR_HOOK`. Do próby
samej mowy AMC wyłącz tę funkcję w ustawieniach dodatku NVDA i uruchom NVDA
ponownie. Nie wyłączaj całego dodatku i nie zmieniaj tego ustawienia wyłącznie
na podstawie testu automatycznego. Przy funkcji włączonej sprawdzamy zawartość
schowka oraz polski komunikat AMC, traktując dodatkowe „Copy” jako świadomie
skonfigurowane zachowanie NVDA.

## Nagrywanie radia — potwierdzenie po Ctrl+R

Na liście `Ctrl+Shift+H` ustaw fokus na włączonym planie i naciśnij Spację.
Plan ma stać się wyłączony, NVDA ma raz podać jego nazwę i nowy stan, a fokus
ma zostać na tym samym wierszu. Ponowna Spacja ma go włączyć. Zamknij i otwórz
okno, następnie uruchom AMC ponownie: stan musi pozostać zapisany. Powtórz dla
planu jednorazowego i cyklicznego oraz podczas aktywnego nagrania, zgodnie z
zasadami głównego AMC. Przycisk „Włącz lub wyłącz” i Spacja muszą wywoływać tę
samą operację. Test wywołujący bezpośrednio metodę `_toggle` nie jest dowodem,
że fizyczna Spacja dochodzi z natywnej listy wx przy działającym NVDA.

`Ctrl+R` na stacji od razu mówi jedno krótkie zdanie:
„Rozpoczynam nagrywanie w tle: nazwa stacji”. Ponowne zdarzenie faktycznego
otwarcia pliku nie czyta już nazwy pliku i nie zagłusza nawigacji NVDA.
Ponowne `Ctrl+R` mówi „Zatrzymuję nagrywanie: nazwa stacji”. Stan wiersza
stacji oraz widok `Alt+R` nadal są odświeżane z rzeczywistego stanu hosta.
Gdy nadchodzi termin planu, interfejs mówi jedno zdanie „Rozpoczynam
zaplanowane nagrywanie: nazwa stacji”. Późniejsze otwarcie pliku tylko
odświeża stan listy i nie powtarza komunikatu.

Podczas trwającego nagrania `B` dodaje szybką zakładkę, a `Shift+B` otwiera
pole „Nazwa zakładki”. Skróty działają w odtwarzaczu Radia oraz na wybranej
stacji w widoku `Alt+R`; na zwykłej liście Radia sama litera `B` nadal służy
natywnemu wyszukiwaniu przyrostowemu. Po zatrzymaniu nagrania każda część z
zakładkami ma zostać automatycznie dodana do Biblioteki Plików jako nagranie
radia, a zakładki mają być dostępne pod `Ctrl+Shift+B`. Powtórne dodanie w tej
samej sekundzie nie tworzy duplikatu; `Shift+B` może nadać nazwę istniejącej
szybkiej zakładce. Zapis profilu testuj przy zamkniętym starym głównym AMC:
równoległa starsza wersja jest celowo blokowana, aby nie nadpisała zmian.

Tor został sprawdzony końcowo na rzeczywistym LiteHost i jednej zapisanej
stacji, bez zmiany profilu: start MP3, aktywny stan, zatrzymanie, historia oraz
niepusty plik w katalogu tymczasowym. Powtarzalna próba znajduje się w
`tools/probe_radio_recording.py`. Testy modelu i komunikatów:

```powershell
python wxlite\run_tests.py radio_recording
# oczekiwane: 23 zdane, 0 błędów
```

Ten sam próbnik z parametrem `--schedule` sprawdza pełną drogę planu:
przyjęcie terminu, samoczynny start, aktywny stan, zapis MP3, historię oraz
wyłączenie wykonanego planu jednorazowego. Próba z rzeczywistym LiteHost i
zapisaną stacją została zaliczona 9 października 2026 r.

## Radio — bieżąca audycja lub utwór pod Alt+D

W sesji Radia `Alt+D` działa zarówno na liście, jak i w odtwarzaczu. Host C#
składa komunikat w tej samej kolejności co główne AMC: nazwa stacji, dostępne
parametry audio, a następnie nazwa audycji lub utworu przekazana przez
strumień. Gdy strumień nie podaje nazwy, komunikat kończy się jednoznacznym
„brak nazwy bieżącej audycji lub utworu”. W Podcastach ten sam skrót nadal
otwiera pełny opis podcastu albo odcinka.

Sprawdzenie ręczne:

1. Uruchom stację, która podaje tytuły ICY, i naciśnij `Alt+D` na liście.
2. Przejdź do odtwarzacza klawiszem `F6` i ponów `Alt+D`.
3. W obu miejscach komunikat powinien zaczynać się nazwą stacji, nie
   identyfikatorem ani adresem. Jeżeli są znane, parametry mają brzmieć np.
   „192 kb/s, 44,1 kHz”, a na końcu ma być bieżący tytuł.
4. Przejdź do Podcastów i sprawdź, że `Alt+D` nadal pokazuje pełny opis.

## Podcasty i YouTube — odczyt, odtwarzanie i trwały postęp

Skrót `Ctrl+3` przełącza na trzecią sesję „Podcasty i YouTube”. Lista czyta
pozycje należące do Biblioteki z tego samego `podcasts.db`, którego używa pełne
AMC, ale otwiera bazę wyłącznie przez SQLite `mode=ro`. Nazwy kontrolek i
wierszy zawierają tylko tekst przeznaczony dla użytkownika; identyfikatory,
rekordy JSON i nazwy typów nie trafiają do mowy NVDA.

1. `Enter` na podcaście, kanale lub playliście otwiera jego odcinki.
2. `Enter` na odcinku uruchamia istniejący silnik multimediów C# AMC. Jeżeli
   baza ma zapamiętaną pozycję, odtwarzanie zaczyna się od niej.
3. `Backspace` wraca na dokładnie to samo źródło, a `Ctrl+L` wraca do głównej
   Biblioteki podcastów.
4. Widok ładuje po 150 odcinków. Wiersz „Załaduj więcej odcinków” dokłada
   następną stronę i przenosi wybór na pierwszy nowo dołożony odcinek.
5. `Page Up` i `Page Down` w odtwarzaczu przechodzą po odtwarzalnych odcinkach
   bieżącej listy. `Ctrl+Shift+C` na odcinku kopiuje bezpośredni adres medium.
6. Pozycja jest zapisywana przez host C# co 15 sekund, przed zmianą materiału
   i przy zamknięciu. Po minucie odcinek dostaje stan „w trakcie”. Naturalny
   koniec ustawia „odtworzony” i zeruje punkt wznowienia, tak jak pełne AMC.
   Python nadal otwiera `podcasts.db` wyłącznie w `mode=ro`.
7. `Ctrl+I` z dowolnej sesji otwiera chwilowy widok „Nowe odcinki i
   materiały”. Są w nim tylko nowe, nieodtworzone materiały ze źródeł nadal
   należących do Biblioteki. `Escape` wraca do dokładnej poprzedniej sesji,
   listy, filtra i zaznaczenia bez komunikatu „Powrót”.
8. Zbiorcze widoki ładują po 150 pozycji. Każdy wiersz ma nazwę źródła, a
   materiały z kanałów i playlist YouTube mają jawne określenie „materiał
   YouTube”. Identyfikatory bazy i rekordy modelu nie trafiają do mowy NVDA.
9. `Ctrl+O` pozostaje kontekstowe jak w głównym AMC: w Plikach wybiera plik,
    w Radiu importuje M3U/PLS, a w Podcastach otwiera import OPML. Import OPML
    pokazuje natywną listę: wszystkie źródła są początkowo zaznaczone, Spacja
    przełącza bieżące, a `Ctrl+A` zaznacza wszystkie. `Ctrl+I` nie jest już
    błędnie zajęte przez import stacji.
10. Pozycja Widok → „Pobrane” jest dostępna w sesji Podcasty i YouTube.
    Pokazuje tylko odcinki, których zapisany plik nadal istnieje na dysku,
    także gdy źródło zostało później usunięte z Biblioteki. Najnowsze są na
    początku; Enter odtwarza lokalny plik zwykłym torem silnika C#.
    W AMC Test od `alpha.455` `Ctrl+J` lub Widok → „Historia pobrań” otwiera
    wspólną listę plików z Podcastów i Radio Archiwisty. Pobierz audio przez
    `Ctrl+D`, wideo przez `Ctrl+Shift+D` i nagranie archiwum przez `Ctrl+D`.
    Sprawdź przy użyciu NVDA: pierwszą pozycję, strzałki góra/dół, lewo
    (źródło, data, rozmiar, folder), Enter, `F6`, `F5`, Escape i ponowne
    otwarcie z każdej sesji. Usuń lub przenieś kopię testowego pliku i sprawdź,
    czy „Brak pliku” jest czytelne i Enter nie próbuje odtwarzać. Testuj bez
    naruszania własnych nagrań użytkownika.
11. W „Nowych odcinkach” `Alt+1` ustawia najnowsze według dodania,
    `Alt+2` porządek alfabetyczny, a `Alt+3` grupowanie według podcastu.
    Wybrany tryb jest zaznaczony w menu Widok, zachowuje wybrany odcinek
    według jego ID i przeżywa restart wxPython. Zapis trafia wyłącznie do
    prywatnego stanu `AMC-wx-Lite`; wspólny `state.json` pozostaje nietknięty.
12. `Ctrl+N` w sesji Podcasty i YouTube otwiera natywny formularz dodawania.
    Przyjmuje RSS/Atom, kanał lub playlistę YouTube oraz pojedynczy publiczny
    materiał YouTube. Adres sprawdza ten sam klient C# co w głównym AMC;
    tymczasowy podpisany adres audio z YouTube nigdy nie trafia do bazy.
13. `F2` na głównej liście zmienia własną nazwę podcastu lub kanału,
    `Delete` usuwa źródło z Biblioteki, `F5` odświeża bieżące źródło,
    `Ctrl+F5` wszystkie źródła, a `Ctrl+D` pobiera jeden lub wiele zaznaczonych
    odcinków. Zaznaczenie wielokrotne działa także przez `Ctrl+Spacja`.
14. Pliki → „Eksportuj bibliotekę podcastów do OPML” zapisuje wyłącznie
    podcasty RSS należące do Biblioteki. Parser i eksporter pochodzą ze
    wspólnego Core AMC; Python nie interpretuje XML-u i nie zapisuje bazy.
15. Pliki → „Eksportuj subskrypcje YouTube” zapisuje kanały jako CSV zgodny
    z Google Takeout i importem NewPipe oraz FreeTube albo kanały wraz
    z playlistami jako OPML do czytników RSS. Pole „Typ pliku” opisuje
    zastosowanie obu formatów i zawsze nadaje właściwe rozszerzenie, również
    po przełączeniu z CSV na OPML. CSV nie udaje obsługi playlist: komunikat
    jawnie podaje liczbę pominiętych.
16. `Ctrl+S` na jednym odcinku otwiera natywny dialog „Zapisz jako” z nazwą
    wygenerowaną przez wspólny `PodcastDownloadNaming`. Powstaje niezależna
    kopia; pole pobrania w bibliotece pozostaje niezmienione. W sesji plików
    ten sam skrót nadal eksportuje zaznaczony fragment audio.
17. `Ctrl+Shift+U` zmienia Ulubione dla wspólnego zaznaczenia podcastów i
    odcinków. `Ctrl+U` pokazuje oba rodzaje w jednym widoku również z
    odtwarzacza. `Delete` w tym widoku wyłącza stan Ulubionych, ale nie usuwa
    źródła z Biblioteki ani pobranego pliku. Enter na podcaście otwiera jego
    odcinki, a Enter na odcinku odtwarza go zwykłym torem silnika.
18. `Ctrl+H` w sesji Podcasty i YouTube pokazuje zapisaną Historię
    odtwarzania tej sesji, również z odtwarzacza. `Delete` usuwa zaznaczenie
    tylko z Historii; źródło, odcinek i pobrany plik pozostają bez zmian.
19. `Alt+Shift+Enter` na podcaście albo odcinku otwiera jego natywne opcje
    odtwarzania. Ten sam skrót działa w odtwarzaczu dla bieżącego odcinka.
    Odcinek może dziedziczyć po podcaście. Dźwięk i prędkość przechodzą dalej
    do sesji lub ustawienia ogólnego, a domyślna zasada pozycji brzmi jawnie
    „pamiętaj pozycję”. Etykieta `1,00 razy — normalna prędkość` odróżnia wartość
    neutralną od wolniejszych i szybszych. Dla całego podcastu można ponadto
    ustawić częstotliwość odświeżania oraz własny folder pobierania.
    Sprawdź pierwszy fokus, rozwinięcie każdego wyboru, strzałki, Anuluj,
    Zapisz, powrót fokusu i ponowne otwarcie z zapisanymi wartościami. Zapis
    nadal wykonuje wyłącznie host C# i jest odmawiany przy drugim pisarzu.

Próba zgodności została wykonana na kopii prawdziwej bazy bez wypisywania
tytułów ani adresów: 305 źródeł w Bibliotece, pierwsze 20 źródeł zwróciło 2487
wierszy z poprawną paginacją. Osobno rzeczywisty opublikowany LiteHost otworzył
syntetyczny plik przez nowe polecenie `media.play` i zgłosił
`playback.duration` oraz `playback.started`.

Zapis postępu jest wąską transakcją jednego odcinka. Dodawanie, zmiana nazwy,
usuwanie, odświeżanie, pobieranie i import OPML także przechodzą przez jednego właściciela
C# i nigdy nie otwierają bazy do zapisu z Pythona. Host odmawia drugiemu oknu
wxPython prawa pisarza i odmawia zapisu, gdy wykryje uruchomione główne AMC,
które mogłoby później nadpisać całą migawkę. Widoki „Nowe odcinki i materiały”
wraz z trzema trybami sortowania oraz „Pobrane” są już przeniesione. Widok
„W trakcie słuchania” został świadomie usunięty: rozpoczęty materiał można
odnaleźć w Historii, Ulubionych albo Kolejce. Import i eksport OPML oraz
eksport kanałów i playlist YouTube są przeniesione; nadal czekają pozostałe
widoki specjalne.

Test bez danych użytkownika:

```powershell
dotnet tests\AccessibleMediaController.LiteHost.ProtocolTests\bin\Release\net8.0\amc_lite_protocol_tests.dll --podcast-progress
python wxlite\run_tests.py podcast_progress podcast_source podcast_navigation shortcuts
```

Zestaw C# sprawdza na osobnej bazie: 30 sekund bez zmiany statusu, 75 sekund
ze stanem „w trakcie”, naturalny koniec oraz nienaruszenie obcej tabeli.
Żywy odbiór wznowienia na profilu użytkownika wymaga bezpiecznego restartu
pakietu; nie wolno zamykać działającego okna podczas nagrywania tylko po to,
żeby podmienić zablokowany plik hosta.

## Opcje sesji — jak to sprawdzić (ten przyrost)

Korekta 2026-10-23 zastępuje wcześniejszy scenariusz zbiorczego wyboru sesji.
Przejdź do wybranej sesji w głównym oknie i naciśnij `Ctrl+Alt+Enter`.
Tytuł i pola mają dotyczyć wyłącznie tej aktywnej sesji; w dialogu nie może
być pola „Konfigurowana sesja”. Po zmianie ustawienia i zapisaniu sprawdź, że
ustawienia pozostałych sesji nie zmieniły się. Dla domyślnej prędkości wariant
dziedziczony ma być czytany dokładnie „Jak ustawienie ogólne”, bez dopowiedzenia
na przykład „2,00 razy — szybciej”.

Aktualizacja 2026-10-22 zastąpiła wcześniejszy scenariusz czterech pól.
W Plikach lokalnych i Podcastach i YouTube oczekiwane jest sześć pól:
pozycja, domyślna prędkość, normalizacja, przejścia, cisza oraz zachowanie po
wyjściu. Radio ma wyłącznie zachowanie po wyjściu. `1,00 razy` musi być
odczytane jako normalna prędkość, wartości niższe jako wolniejsze, a wyższe
jako szybsze. Po Zapisz zamknij i otwórz dialog ponownie, sprawdź zachowanie
strzałek/Home/End oraz powrót fokusu po Zapisz i Anuluj. Następny materiał ma
ruszyć z domyślną prędkością sesji, chyba że plik, podcast lub odcinek ma
własne ustawienie. Opcja „Zawsze od początku” nie może unieważnić jawnego
ustawienia bardziej szczegółowego.

Wynik automatyczny bieżących źródeł po korekcie z 2026-10-23: pełny zestaw
Pythona `1983 passed, 111 skipped, 11 subtests passed`; celowane testy opcji
sesji `76 passed`. Wcześniejszy etap zaliczył 27 zestawów protokołu C#, przyjął
378 payloadów Pythona w parserze C# i zbudował Debug bez ostrzeżeń i błędów;
korekta z 2026-10-23 nie zmienia kodu C#. Jest to dowód źródeł i kompilacji,
nie nowej instalacji AMC Test ani ponownego odbioru żywego NVDA.

Testy bezokienne (wszystko, co zmierzone):

```bash
cd /home/test/projekty/amc-wx-session-options-20261007/wxlite
python3 run_tests.py          # 968 PASS / 0 FAIL / 0 SKIP
```

Zgodność z silnikiem — payloady portu czytane PRAWDZIWYM parserem C#.
Sam build i `dotnet run` nie dowodzą portu; potrzebny jest krok Pythona:

```bash
cd wxlite && python3 tools/dump_session_option_payloads.py /tmp/p.json
cd .. && AMC_SESSION_OPTIONS_PAYLOADS=/tmp/p.json \
  ~/dotnet/dotnet run --project \
  tests/AccessibleMediaController.LiteHost.ProtocolTests/AccessibleMediaController.LiteHost.ProtocolTests.csproj
# oczekiwane: "(payloady z portu Python przyjete parserem: 378)" + 8 zestawów OK
```

Bez zmiennej `AMC_SESSION_OPTIONS_PAYLOADS` ten zestaw **wypisuje, że
pominął** sprawdzenie — nie traktuj takiego przebiegu jako dowodu portu.

Kalibracja (wykonana): podmiana zdolności Radia na „umie przetwarzanie”
czerwieni 4 testy, a odwrócenie warunku `ID_OK` czerwieni test Anuluj.
Dołożenie payloadu z nieobsługiwaną ciszą czerwieni zestaw C#. Testy więc
rozróżniają, a nie tylko świecą zielono.

Dialog opcji odtwarzania podcastu został dodatkowo otwarty w prawdziwym,
ukrytym oknie wxPython na Windows. Sprawdzono pierwszy fokus, celową nazwę
kontrolki, etykietę prędkości normalnej, zmianę wyboru i ponowne otwarcie z
tym wyborem. To nie zastępuje odbioru żywym NVDA: mowa, pełna kolejność Tab
oraz fokus po Zapisz i Anuluj wymagają sprawdzenia przez użytkownika.

## Biblioteka radia i lokalna Historia — odbiór kolejnego przyrostu

Kod `4fbc49b0` ma zakończony wąski odbiór na Hermesie: filtr członkostwa
Biblioteki radia, nazwa stacji bez powtarzania rodzaju oraz odtworzenie z
Ctrl+H pliku niebędącego członkiem Biblioteki. Raport z gestów i mowy:
`amc_pomoc/wx-library-compare-20261006/live/ACCEPTANCE.md`;
kwit rodzica: `live/PARENT-VERIFIED.json`. To nadal przyrost przed dostawą,
nie funkcje już obecne w zainstalowanej próbnej paczce `5e1f2d4e`.

Przy ponowieniu używaj prywatnej pełnej kopii i właściwego trybu profilu:
`AMC_WX_FIXTURE` wybiera prywatną listę stacji z lite-home, a nie czytnik
`radio.stations`. Dla próby READ_ONLY_MIRROR ustaw APPDATA i LOCALAPPDATA
na prywatną kopię i nie ustawiaj AMC_WX_FIXTURE. Potwierdź efektywne ścieżki
oraz rzeczywiste wiersze przed pomiarem. Puste Radio nie zalicza filtra.

## Poprawki plików — odbiór zakończony

Na kodzie `5e1f2d4ed07503f7b9399f89aaf7143f5e5a1b71` wykonano poniższe
próby z żywym NVDA. Lista pozostaje scenariuszem regresji, nie otwartą
prośbą o ponowne sprawdzanie całego zakresu:


1. Ctrl+O otwiera dialog pliku, Ctrl+Shift+O dialog folderu; Escape anuluje.
2. Escape: pole filtra, lista wyników z filtrem, lista bez filtra i pusta
   lista folderu, następnie odtwarzacz. Pierwszy Escape z wyników czyści filtr,
   dopiero następny wychodzi wyżej. Brak rutynowej zapowiedzi wczytywania.
3. Zaznaczyć dalszy plik w folderze, Ctrl+U, Ctrl+L: ten folder i plik.
   Powtórzyć z Ctrl+H. Sprawdzić oba powroty również po Alt+2.
4. Home i End: rzeczywista pozycja hosta 0 i Max(0, duration-10s).
   Osobno NVDA+End: odczyt paska i zero poleceń przewinięcia.

Końcowy wynik Pythona: **799/0/0**. Raport z faktycznych gestów, mowy,
stanów GUI i hosta: `amc_pomoc/wx-file-keys-parity-20261006/parent-final-live/ACCEPTANCE.md`.
Rodzic uzupełnił tylko brakujące pomiary: prawdziwe oba modale, Escape
z potwierdzonego PLAYER oraz NVDA+End na obu potwierdzonych widokach.
Zastępczy ShowModal zwracający ID_CANCEL nie zalicza próby dialogu.
W folderze nawet przy braku wyników może zostać wiersz nadrzędny „..”;
faktyczne zero wierszy zmierzono osobno w filtrowanym korzeniu.

Dane próbne muszą być prywatną kopią, nagrywanie i rezerwacja pulpitu
sprawdzone świeżo przy kolejnej próbie. Dostawa tej paczki została już
potwierdzona oddzielnym odczytem569plików oraz działającej jednej instancji
na głównym komputerze; raport `wx-file-keys-parity-20261006/delivery/PARENT-DELIVERY.md`.
Nie mylić jej z nadal przygotowywanymi zmianami Biblioteki i Opcji sesji.

## Co zostało wykonane

Bieżący odbiór mechanizmu wszystkich list: 562 testy, 0 błędów, 0 pominięć. Zwykłe listy Windows, przyrostowe zmiany w obrębie widoku, zachowanie wyboru po ID, puste zakładki i pojedynczy odczyt po zmianie dużego widoku sprawdzono w prywatnym stagingu z żywym NVDA. Końcowe kwity: `native-w02-valid-retest-after422/parent-final/`. To nie jest odbiór wszystkich funkcji pełnego AMC ani innego czytnika.

Odbiór KOŃCOWY żywej kolejki (po scaleniu): **588 testów Pythona, 0 błędów, 0 pominięć** i **6/6 zestawów C#**. Z żywym NVDA zmierzono na nowym hoście (DLL `6FFF6BD0…`): Ctrl+Q na starcie oddaje zapis, Enter uruchamia kolejkę w porządku **B→A→C (nie alfabet)**, a pozostawiona OTWARTA lista sama usuwa zużyty wiersz po naturalnym przejściu i nie kradnie wyboru grającej pozycji. Po ostatnim utworze lista ma 0 pozycji (potwierdzone też kontrolką Win32 `LVM_GETITEMCOUNT=0`), czytnik mówi „Kolejka odtwarzania, pusto", a ponowny Ctrl+Q NIE wraca do starego zapisu. Transport na przebudowanym hoście: 22/22. Kwity: `live-queue-after-native-lists/final-integrated/`. Trwały pisarz profilu i dalszy pełny port pozostają poza tym etapem.

## Cisza podczas wpisywania filtra — korekta po próbie użytkownika

Wpisywanie i usuwanie znaków nadal zawęża listę, ale aplikacja nie mówi
„Wyniki filtrowania” ani liczby po każdym znaku, również przy zerowym wyniku.
Zostaje zwykłe echo klawiszy NVDA. Jawne wejście na pustą listę nadal wyjaśnia
brak wyników; Escape w polu nadal czyści filtr i wraca na listę.

Dwa testy kodujące dawną automatyczną zapowiedź odwrócono: najpierw26PASS/2FAIL,
po zmianie74PASS/0FAIL/0SKIP w filtrze i jego bezpośrednich zależnościach.
Odbiór żywej mowy do wykonania na scalonym kandydacie. Historia fraz, nowe
zachowanie Tab/Down i zmiana pamiętania filtra pozostają propozycjami.

## Filtr listy Ctrl+K: jak to sprawdzić na żywo

Pełny Python po tym odbiorze: **649 testów, 0 błędów, 0 pominięć**
(`search-filter-after-resume/final-recovery/full-python-final.log`).

Staging i narzędzia (nie publikacja, nie instalacja):

1. Skopiuj `wxlite/amc_wx_lite` do prywatnego stagingu i SPRAWDŹ `sha256sum` oraz
   `gui.__file__` z kwitu, zanim cokolwiek orzekniesz — stary staging potrafi mieć
   inny kod niż repo.
2. Uruchom `launch-final.ps1 <nazwa-kwitu>`; kwit ląduje w `kwit-<nazwa>/`
   (`probe.jsonl`, `snapshot.json`, `boot-stderr.txt`).
3. Gesty: `gestures-final.ps1 <pid> <plan>`. Plany: `A` pisanie+Enter, `B` Escape,
   `C` brak wyników, `D` Backspace w polu, `E` Down, `F` Radio, `H` Backspace NA LIŚCIE,
   `I` filtr per widok, `J` dwa niezależne zapytania.
4. Mowę czytaj z Podglądu mowy NVDA (`harness-nvda/read-speech-viewer.ps1`), zachowując
   bufor PRZED gestami i porównując różnicę. `Announcer.say` z sondy NIE jest dowodem mowy.

Czego wymagać od aparatury (sprawdzone tu na własnych błędach):

- Bramka pierwszego planu musi zwracać TYLKO `bool` i logować osobno; `Write-Output`
  przed `return` zwraca tablicę i `if (-not ...)` przepuszcza gest mimo braku fokusu.
- `$pid` jest w PowerShellu tylko do odczytu — użyj innej nazwy (`$fgPid`).
- Owijka sondy MUSI przekazywać `*args/**kwargs`, inaczej zgubi nowy parametr metody
  i zdarzenie w ogóle się nie zaloguje.
- Nie owijaj tej samej metody dwa razy w jednej instalacji sondy — drugie owinięcie
  nadpisuje pierwsze i zdarzenia milkną.

Ograniczenie zmierzone, nie naprawione: przy szybkim wpisywaniu przez `SendKeys`
pierwszy znak serii bywa gubiony przez samą aparaturę (plan J: wysłane `bo`,
dotarło `o`). To artefakt wysyłki gestów, nie filtra — wynik czytaj z `filter.text`
w kwicie, a nie z zamierzonego napisu.

## Domknięcie żywej kolejki

Nadrzędny kwit `live-queue-after-native-lists/parent-speech-full-profile/`: pełna kopia 11200→11203 pliki, 5000 zakładek zachowanych, naturalne B→A→C z C zaznaczonym jeszcze podczas B. Natywne/model 3→2→1→0, stały fokus, ponowne Ctrl+Q nadal puste. Mowa z rzeczywistego Podglądu mowy; `Announcer.say` sam w sobie jej nie dowodzi. Kod wykonawczy 38d931e, host DLL 6fff6bd0…, bez kolejnych zmian produkcyjnych w tym odbiorze.

## Trwałość kolejki: próbny profil i rzeczywisty restart

Aktualny kwit `queue-persistence-after-live/gui-integration-parent/REPORT.md`: pełny Python603/0/0, GUI odmowa zapisu ostatniego utworu z prawdziwą mową NVDA, ponowne uruchomienie niepustej kolejki, udany zapis do zera i kolejny restart z0wierszy. Własny profil `profile-persist-parent-final` zawiera pełne11203 rekordy; nie zastępuj go małą próbką.

Host: `C:\Users\test\AppData\Local\Temp\amc-wx-queue-persist-parent\complete-host-8abc180\amc_lite_host.exe`, DLL13f38747…. Przed użyciem sprawdź komplet wymaganych bibliotek z deps.json i manifest41plików w kwicie. Sam SHA własnejDLL NIE wykrywa brakujących NAudio/SoundTouch; poprzedni katalog był niekompletny mimo zgodnego SHA. Starych kwitów/profili nie nadpisuj, użyj następnego backupuSQLite. Zapisy są dozwolone tylko na prywatnej kopii, a późniejszy odbiór czasu opisano poniżej.

## Czas wznowienia — odbiór z rzeczywistym NVDA

Kod70c833d nad984c660/4ad9696. Aktualny host: `C:\Users\test\AppData\Local\Temp\amc-wx-resume-parent-final\host-policy-completion\amc_lite_host.exe`, DLL9cedd23b…,41plików,22wymagane DLL z deps.json i0braków. Kwity: `queue-resume-after-persistence/parent-final/REPORT.md`, `accepted-gui-cycles.json`, pełny `python-final-full.log` (608/0/0).

Na NOWEJ pełnej kopii, nie na zachowanych kwitach: Ctrl+Q, wybór niepierwszego utworu, Enter, Right+10s. Sprawdź rzeczywisty `loadedId` i rosnącą pozycję; `transport.status` nie ma pola `playing`. Zamknij własne okno podczas grania. Nowy GUI/host nie może grać sam: ma odtworzyć porządek, wybrać bieżące ID, a świadomy Enter wznowić od zapisanego czasu. Dla czytnika wstrzymaj materiał, poczekaj na aktualizację stanu, wywołaj Ctrl+E i odczytaj NOWY tekst Podglądu mowy przed zamknięciem okna. Sam `Announcer.say` nie dowodzi mowy. Wykonana próba:12,63s zapisane →12,75s start, NVDA „Minelo 12 s”.

Następnie wyłącz pamięć dla pozycji dziedziczącej ustawienie, pozostawiając stary niezerowy checkpoint. Nowy proces: `resumeSeconds=0`, start od początku (zmierzono0,97s, NVDA „Minelo 1 s”), checkpoint po zamknięciu0. Jawny Remember pozycji jest osobnym priorytetem. Dziedziczenie źródeł folderów sprawdzaj zgodnie z oryginałem: najbliższe Inherit prowadzi do sesji, nie do dalszego źródła.

C#7 zielonych zestawów, jedna pominięta próba ścieżki produkcyjnej naWSL. Adapter WPF skompilowany naWindows bez błędów/ostrzeżeń. To nie jest odbiór pełnego portu ani zgoda na współdzielony pisarz z działającym starym WPF.

## Przygotowane stanowisko prywatne

Odebrany układ naHermesie: `C:\Users\test\AppData\Local\Temp\amc-wx-425` z podkatalogami `app`, `runtime`, `host` i prywatną kopią `profile`. Jest to staging, nie numer publicznego wydania.

Uruchomienie bez obserwatora, z tym samym prawdziwym punktem wejścia:

```powershell
$stage = 'C:\Users\test\AppData\Local\Temp\amc-wx-425'
$env:APPDATA = "$stage\profile"
$env:LOCALAPPDATA = "$stage\profile"
$env:AMC_LITE_HOST = "$stage\host\amc_lite_host.exe"
Remove-Item Env:AMC_WX_FIXTURE -ErrorAction SilentlyContinue
& "$stage\runtime\python.exe" -m amc_wx_lite
```

Prywatny runtime ma `python314._pth` wskazujący `../app`. Embeddable Python może ignorować `PYTHONPATH`: zawsze sprawdzaj faktyczne `gui.__file__` i zgodność kopiowanych modułów. Stary moduł kandydata w innym katalogu nie testuje nowego kodu.

Bez przekierowania `APPDATA`/`LOCALAPPDATA` aplikacja domyślnie czyta profil właściwego użytkownikaWindows. Testy wykonuj na kopii. Zmienna `AMC_WX_FIXTURE` wybiera inny tryb prywatnej piaskownicy, dlatego nie zastępuje automatycznie prób lustrzanego, pełnego profilu.

## Odbiór klawiatury i NVDA

1. Ctrl+1: Biblioteka. Foldery mają pochodzić z bazy, nawet gdy zapisane dyski nie istnieją na stanowisku. Enter i Backspace zachowują logiczny powrót.
2. Alt+2: wszystkie lokalne pliki alfabetycznie. Porównaj liczność ze źródłem, nie z wpisaną na stałe liczbą.
3. Ctrl+Shift+U na pliku albo stacji radiowej dodaje zaznaczenie do Ulubionych
   lub je usuwa. Zaznacz kilka sąsiednich pozycji Shiftem i kilka rozłącznych
   przez Ctrl+Spację: jeżeli choć jedna nie była ulubiona, stan ma zostać
   włączony wszystkim; dopiero ponowienie na zbiorze samych Ulubionych usuwa
   go wszystkim. W odtwarzaczu polecenie nie może przerywać dźwięku ani wracać
   samoczynnie na listę. Ctrl+U pokazuje zgodny stan, kolejność i wybór także
   po ponownym uruchomieniu.
4. Ctrl+Shift+P na pliku, stacji albo odcinku otwiera „Przynależność do
   playlist”. Strzałki mają czytać wyłącznie nazwę, stan „zaznaczona”,
   „niezaznaczona” lub „stan mieszany” oraz liczbę elementów — nigdy nazwę
   klasy ani identyfikator. Spacja zmienia stan całego zaznaczenia. Insert i
   Ctrl+N tworzą playlistę, F2 zmienia jej nazwę, Delete usuwa tylko playlistę,
   Enter zapisuje, a Escape anuluje wszystkie zmiany robocze. Po Zapisz i po
   Anuluj fokus wraca na właściwą listę albo przycisk odtwarzacza. Ctrl+P
   pokazuje playlisty bieżącej sesji; Enter otwiera zawartość, Backspace wraca
   na tę samą playlistę. Powtórz cały odczyt i powrót osobno w Plikach,
   Radiu oraz Podcastach i YouTube. Wiersz playlisty ma podawać nazwę i liczbę
   dostępnych elementów, nigdy identyfikator profilu. W otwartej lokalnej,
   radiowej albo podcastowej playliście zaznacz ciągły blok Shiftem:
   `Alt+Up` i `Alt+Down` przesuwają go o jedno miejsce, zachowując wszystkie
   zaznaczenia i fokus według elementu, nie numeru wiersza. Następnie zaznacz
   nieprzylegające pozycje przez `Ctrl+Spację`, naciśnij `Ctrl+X`, ustaw fokus
   na niezaznaczonym celu i naciśnij `Ctrl+V`: cały blok ma znaleźć się przed
   celem w swojej wcześniejszej kolejności. Cel należący do zaznaczenia ma
   dać krótką odmowę. Przy niepustym filtrze obie drogi mają zażądać Escape i
   niczego nie zapisać. Po ponownym uruchomieniu sprawdź tę samą kolejność.
   W playliście podcastów `Delete` ma usunąć odcinek tylko z otwartej
   playlisty, bez usuwania subskrypcji, pliku ani wpisu z innej playlisty.
5. Ctrl+C: nazwa lub nazwy zaznaczonych pozycji, każda w osobnym wierszu.
   Ctrl+Shift+C na istniejących plikach ustawia jednocześnie tekst pełnych
   ścieżek oraz Windows `CF_HDROP`, więc pliki można wkleić do folderu. Dla
   kilku zaznaczeń kolejność odpowiada kolejności listy. Dla pozycji
   internetowej skrót kopiuje adres jako tekst. Sprawdź rzeczywistą zawartość
   schowka, nie sam komunikat.
6. Ctrl+Shift+O: zwykły dialog folderu, nie Biblioteka (Ctrl+O to dialog PLIKU — do 06.10.2026 port miał te dwa gesty odwrotnie). W zmierzonym dialogu pierwszyEnter wybiera wpisany folder, drugi zatwierdza; kontroluj rzeczywisty fokus i zamknięcie okna zamiast wysyłać gesty w ciemno.
7. Na dostępnym pliku:Enter, Spacja, Spacja. Stan przycisku Odtwórz/Wstrzymaj ma zgadzać się z działaniem; cisza czytnika i sam tekst statusu nie dowodzą komunikatuNVDA.
8. Ctrl+2: Radio z `radio.stations` wspólnego profilu. W trybie tylko do odczytu dodawanie/zmiana/usuwanie/import nadal mają odmówić uczciwie. Prywatna lista piaskownicy to odrębny tryb, nie zapis doAMC.

9. Menu (Alt): pasek ma być w całości klawiaturowy — Alt otwiera, strzałki chodzą, litery wybierają, Escape zamyka. Każda pozycja musi wykonać TO SAMO co jej skrót (ten sam `_dispatch`). W Radiu pozycje lokalnych Ulubionych/playlist mają być wyszarzane, nie ukryte. Nie dodawaj pozycji dla funkcji, których nie ma.

10. Widoki aktywności. Ctrl+H historia, Ctrl+Q kolejka, Ctrl+Shift+B zakładki ZAZNACZONEGO pliku. Trzy rzeczy do sprawdzenia poza samą licznością:
    - Etykieta kolejki odpowiada stanowi hosta. Przed pierwszą inicjalizacją jest to „Kolejka (zapisana)”; potem „Kolejka odtwarzania”, także „pusto” po ostatnim utworze. Pozostaw Ctrl+Q otwarte przy B→A→C: wiersze mają ubywać bez ponownego skrótu, a po ponownym wejściu nie mogą wrócić z zapisu. Wybrany nadal istniejący wiersz i widok pozostają; przejście pliku nie zmienia bieżącej stacji w oglądanej sesji Radia.
    - Zaznacz kilka plików Shiftem, potem kilka nieprzyległych przez Ctrl+Spację. Ctrl+Shift+Q dodaje albo usuwa całe zaznaczenie ze zwykłej kolejki. Ctrl+Shift+Enter ustawia albo zdejmuje „odtwórz następne”. Shift+Enter niczego nie przechwytuje. Jeśli plik już gra, żadna z tych operacji nie może przerwać dźwięku ani wyzerować czasu.
    - W kolejce zawierającej bieżący B, zwykły A i oznaczony jako następny C widok ma pokazać B, C, A, a naturalny koniec B ma rzeczywiście uruchomić C.
    - Ctrl+Shift+B, nie Ctrl+B. Ctrl+B to teraz OSOBNY, szerszy widok zbiorczy (`GetForDisplay` / `all_bookmark_rows`); Ctrl+Shift+B zostaje przy węższym `GetForItem` dla jednego pliku. Dwa skróty, dwa widoki — sprawdzaj, że się nie podmieniają.
    - W obu widokach zakładek zaznacz kilka wpisów, także nieprzylegających
      przez Ctrl+Spację, i naciśnij Delete. Mają zniknąć wszystkie zaznaczone
      zakładki, fokus ma przejść na najbliższy pozostały wpis, a pliki
      źródłowe muszą pozostać nietknięte. Wpis będący jednocześnie zakładką
      i rozdziałem pozostaje w bazie jako rozdział.
    - Backspace z zakładek ma wrócić na TEN plik. Sprawdzaj ID zaznaczenia, nie numer wiersza — po powrocie do Wszystkich plików numer jest inny (zmierzono 2309 z 2476).
11. Fizyczny skok zakładki mierz pozycją z ŻYWEGO hosta (`transport.status`), nie oczekiwanym payloadem. `play.file` dostaje `positionSeconds` w jednym wywołaniu — nie wysyłaj seeka obok play, bo pozycja ginie przy starcie nowego pliku. Materiał do próby zrób SYNTETYCZNY: wygenerowany plik plus wstrzyknięty rekord w OSOBNEJ kopii pełnej bazy (nie w małej próbce, nie na pliku użytkownika). Przy wstrzykiwaniu zarejestruj zastępczą kolację `AMC_PL`, inaczej `INSERT` padnie na indeksie. Pozycję daj z częścią ułamkową (próba:83,456s) — kontrakt to dzielenie `/ 10_000_000`, nie `//`.

Przed audio/GUI sprawdź rezerwację pulpitu, obce procesy i ŻYWY stan nagrywania. Nie wyprowadzaj braku nagrywania z pustych pólJSON. Nie restartujNVDA ani nie wysyłaj klawiszy w obce okno. Podgląd mowy musi być otwarty podczas gestów. Zachowaj surowy przyrost i osobno skutek działania, nie tylko informację„niepusta mowa”.

Próba mowy/menu dla tego przyrostu: `amc_pomoc/wx-full-profile-after421/menu-and-speech-after422/proba_koncowa.py` (kwit `proba-koncowa.json`). Mierzy tylko to, co zmienione: mowę po zmianie widoku, oba kopiowania z odczytem schowka i obejście menu. Po próbie zamyka własne okno, sprawdza procesy i przywraca schowek.

Mowa natywnej listy: odebrano pojedynczy odczyt po Foldery → Wszystkie pliki oraz Playlisty → Wszystkie pliki, bez poprzedniej nazwy; strzałki czytają właściwy wiersz. Puste zakładki zgłaszają „pusto” i dostępną listę zamiast „nieznane”, a Backspace wraca po ID. Sama migracja na LC_REPORT tego nie dowodziła — końcowe dowody są w `native-w02-valid-retest-after422/parent-final/`. Pełna podmiana zawartości jest ograniczona do zmiany widoku; nie odtwarza HWND.

Próba widoków aktywności: `amc_pomoc/wx-full-profile-after421/activity-gui-after422/proba_koncowa_activity.py` (etapy A–C) oraz `dopiecie_de.py` (etapy D–F na tym samym żywym oknie). Nazwa gestu Backspace w mostku NVDA to `backspace`; `back` zwraca HTTP 500 i pierwszy przebieg na tym padł.

12. Zbiorczy widok zakładek (Ctrl+B) i powrót z odtwarzacza. Aparatura:
    `amc_pomoc/wx-full-profile-after421/all-bookmarks-gui-after422/`
    (`odbior_zakladek.py` = Ctrl+B / odmowa / pusto, `dopiecie_bcd2.py` =
    start i pauza i skok, `dopiecie_d.py` = powrót menu). Pułapki, na które
    już wpadnięto — nie powtarzaj ich:
    - **Ścieżka hosta w stagingu to `host\amc_lite_host.exe`**, nie
      `AccessibleMediaController.LiteHost.exe`. Przy złej nazwie okno
      pokazuje widok odtwarzacza przy `engine_pid=null` i `HostUnavailable` —
      wygląda jak zaliczony skok, a nic nie grało. Zawsze sprawdź
      `runtime.json` i dziennik wywołań, nie sam widok.
    - **Klasa klienta to `LiteHostClient`**, nie `HostClient`. Podgląd
      wywołań po złej nazwie milczy; podglądu bez pokrycia nie wolno czytać
      jako „zero wywołań".
    - **Pasek menu otwiera gołe `alt`.** `alt+w` wysłane z fokusem na
      przycisku odtwarzacza trafia w przycisk (NVDA zgłasza rolę 9
      „Odtwórz"), nie w menu. Potwierdź rolę 11 po `alt`, dopiero potem
      wysyłaj literę.
    - **Nie dosięgniesz pliku 40 strzałkami w 2476 wierszach.** Materiał
      próbny ma `is_favorite=1` — wejdź przez Ulubione (Ctrl+U, ~10 wierszy).
      Start i tak jest zwykłym Enterem z listy, charakter dowodu bez zmian.
    - **Kontekst bierz z sesji, nie z zaznaczenia**, i sprawdź go RÓWNIEŻ w
      pauzie. `status_text="Wstrzymano"` nie znaczy „nic nie jest bieżące".
    - **Powrót PLAYER→LIST wymaga `session_view` z obserwatora.**
      `library_view` nazywa tylko widok Biblioteki i nie rozróżnia
      odtwarzacza od listy, więc „Lista→Lista" nie jest dowodem powrotu.
    - **Pusty widok rób na POTWIERDZONYM innym pliku.** Najpierw zmień
      wybrany plik, potwierdź jego rzeczywiste ID i 0 zakładek tą samą
      funkcją, której używa widok, dopiero potem Ctrl+Shift+B i wymagaj
      `rows==0`. Nazwanie czegoś „pusto" przy `rows=1` nie jest dowodem.

## Dane i silnik

- SQLite: `mode=ro`, dziennikWAL uwzględniany. Błąd odczytu nie przechodzi automatycznie na `immutable=1`.
- ID są napisami. Dostępność i członkostwo w Bibliotece pochodzą z danychAMC, nie z `Path.exists` na innym komputerze.
- Jeden wykonawca C# prowadzi zegar i nagrywanie; Python nie uruchamia drugiego
  harmonogramu. `Ctrl+Shift+H` otwiera natywne zarządzanie planami wxPython.
  Zmiany są zapisywane w prywatnym `AMC-wx-Lite\state.json`, a nie we wspólnym
  profilu WPF, i od razu synchronizowane z działającym hostem. `Insert` dodaje,
  `Ctrl+D` powiela jako wyłączoną kopię, `Enter` edytuje, `Spacja` przełącza,
  a `Delete` usuwa. Po wykonaniu host zwraca przesunięty lub wyłączony plan,
  który wxPython utrwala w swojej kopii.
- Foldery:AMC_PL. Kolekcje alfabetyczne: właściwy osobny tryb tytułu iOrdinalIgnoreCase ścieżki. Zgoda jednego korpusu nie dowodzi równoważności dwóch komparatorów.
- ProtokółJSON-lines, identyfikatorynapisy, limit64KiB po stronie żądania, wsady dzielone po bajtach. Nowe tryby kluczy wymagają nowegoLiteHost; klient odmawia niezgodnego trybu starego hosta.
- Host wykorzystuje istniejące silnikiAMC, nie własny dekoderPython. Sprawdzenie Roslynem lub kluczy w osobnej sondzie nie zastępuje kompilacji i uruchomienia rzeczywistego hostaWindows.

## Testy kodu

```bash
cd /home/test/projekty/amc-wx-full-profile/wxlite
python3 run_tests.py
```

Wąskie przebiegi wybiera się fragmentem nazwy modułu, np. `library_view_navigation`, `library_views_through_source`, `announcement_reaches_screen_reader`. Testy protokołu z rzeczywistymC# wymagają zbudowanych `AccessibleMediaController.LiteHost.ProtocolTests`; pominięć nie przedstawiaj jako zaliczenia.

Przed publikacją potrzebny jest osobny, pełny odbiór niezmienianej paczki. Ten dokument nie potwierdza publicznego wydania ani instalacji na komputerze Michała.

### Kolejka podcastów i YouTube

- Przejdź do sesji „Podcasty i YouTube” i naciśnij `Ctrl+Q`. Widok ma pokazać
  odcinki w kolejności zapisanej przez główne AMC; pozycje „Odtwórz następny”
  są przed zwykłą częścią kolejki.
- Pusta kolejka ma powiedzieć „Kolejka, zero elementów” bez identyfikatorów
  bazy ani technicznych nazw rekordów.
- `Enter` odtwarza zaznaczony odcinek. `Page Up` i `Page Down` przechodzą po
  widocznej kolejności źródłowej.
- `Delete` usuwa zaznaczony odcinek tylko z kolejki podcastów. Nie usuwa
  subskrypcji, pobranego pliku ani historii. Po ponownym `Ctrl+Q` usunięta
  pozycja nie może wrócić.
- Stronicowanie zachowuje próg 150 pozycji i wiersz „Wczytaj więcej”.

## Znane ograniczenia

Wymienione wyżej przypadki mowy list są odebrane. Nie rozszerzaj tego na wszystkie możliwe scenariusze i czytniki. Całość nie ma jeszcze wszystkich usług i ustawień oryginału. Kolejka żywego silnika DZIAŁA (`Ctrl+Q` pokazuje stan, `Ctrl+Shift+Q` zmienia zwykłą kolejkę, a `Ctrl+Shift+Enter` blok „Odtwórz następny”); naturalne przejścia i trwały zapis prywatnej kolejki zostały zmierzone. Tempo i wybór silników mają osobne wcześniejsze kwity — odbiór list ich nie powtarza.

Pełne dowody robocze: `amc_pomoc/wx-full-profile-after421/library-gui-after422/REPORT.md` i `parent-acceptance/`. Nie kopiuj prywatnych tytułów/profilu do repo ani paczki.

## TIDAL — kolekcje i nawigacja katalogu

- `Ctrl+4` z listy i odtwarzacza przełącza do sesji TIDAL. Powrót do innej
  sesji i ponowne `Ctrl+4` ma zachować poprzedni widok oraz zaznaczony element.
- `Ctrl+L`, `Ctrl+U` i `Ctrl+P` otwierają Bibliotekę, Ulubione i Playlisty.
  Strzałki mają być obsługiwane przez zwykłą listę wx i NVDA ma czytać
  wyłącznie celowe nazwy, rodzaje oraz szczegóły — nigdy ID usługi, prywatnego
  uchwytu odtwarzania ani reprezentacji obiektu.
- `Ctrl+C` kopiuje nazwę, a `Ctrl+Shift+C` publiczny adres, jeżeli był zapisany.
- Prawa strzałka przy jednym dostępnym celu przechodzi do niego bez
  jednoelementowego menu. Przy dwóch celach otwiera natywne menu z wyłącznie
  dostępnymi działaniami. Anulowanie menu przywraca fokus listy i nie zmienia
  zaznaczenia.
- Lewa strzałka czyta tylko dostępne informacje katalogowe i uczciwie mówi, że
  format audio nie jest znany z zapisanej migawki.
- Enter na albumie i playliście otwiera ich zawartość przez tę samą oficjalną
  integrację TIDAL i ten sam magazyn poświadczeń Windows co główne AMC. Python
  nie otrzymuje tokenu ani prywatnego uchwytu odtwarzania. Enter na wykonawcy
  pokazuje trzy kategorie: Albumy, Utwory i Podobni wykonawcy; Enter na kategorii
  pobiera właściwą listę katalogu.
- Backspace z zawartości wraca do dokładnej poprzedniej listy i zaznaczonego
  kontenera. Backspace na głównym widoku TIDAL niczego nie oznajmia i nie
  zmienia sesji. Przełączenie do innej sesji i powrót zachowuje także wnętrze
  otwartego kontenera.
- Enter na utworze lub wideo przekazuje materiał do oficjalnej aplikacji
  TIDAL. `Ctrl+Enter` przekazuje cały album albo playlistę. AMC Python nie
  udaje własnego chronionego odtwarzacza TIDAL ani zmiany konta; słyszalny
  start, pauzę i następny/poprzedni trzeba odebrać na żywym koncie. Nie należy
  uznawać testów protokołu za odbiór żywego konta i NVDA.

## Zanik wybranego urządzenia audio

Podczas grającego radia odłącz urządzenie wybrane przez `Shift+A`. Sprawdź oba
warianty nowego ustawienia. W trybie kontynuacji program ma raz podać przejście
na urządzenie domyślne i dalej odtwarzać tę samą stację. W trybie pauzy ma
przejść na urządzenie domyślne, lecz wstrzymać odsłuch do świadomego naciśnięcia
`Spacji`. W obu trybach aktywne nagrywanie i timeshift mają działać dalej.
Materiał wstrzymany przed odłączeniem nie może sam się wznowić.

Podczas odtwarzania naciskaj `Ctrl+]` oraz `Ctrl+[`. Pierwszy skrót ma wybierać
następne, a drugi poprzednie dostępne wyjście bieżącej sesji. Oba zawijają
listę, podają jedną użytkową nazwę i zapamiętują wybór. Nie mogą zmienić
urządzenia ani stanu innej sesji. Nie odłączaj urządzenia używanego do pracy z
czytnikiem ekranu bez zapewnionego innego słyszalnego wyjścia.

## Niezależne odtwarzanie sesji

1. Uruchom radio na jednym urządzeniu audio.
2. Przejdź do Multimedia, wybierz inne wyjście i uruchom plik. Radio i plik
   mają nadal grać równolegle.
3. W Multimedia użyj `Spacji` i `Ctrl+M`. Zmienia się wyłącznie plik.
4. Wróć do Radia i użyj tych samych skrótów. Zmienia się wyłącznie radio.
5. Zmień sesję kilka razy bez polecenia transportu. Samo przejście nie może
   zatrzymać, wznowić, wyciszyć ani przełączyć urządzenia żadnego toru.

## Ustawienia i pełny audyt interfejsu

`Ctrl+,` otwiera obecnie cztery działające karty: Ogólne, Radio i nagrywanie,
Podcasty i YouTube oraz Komunikaty. Odbierz
`Ctrl+Tab` oraz `Ctrl+Shift+Tab`, pierwsze pole każdej karty, wszystkie listy
wyboru oraz powrót fokusu po Zapisz i Anuluj. Enter na zmienionej wartości ma
zapisać ją i podać raz „Zapisano ustawienia”; ponowne otwarcie ma pokazać tę
samą wartość bez technicznych nazw. Szczególnie sprawdź etykiety SoundTouch,
Speedy i Signalsmith, wartości ciszy oraz oba warianty utraty urządzenia.

Na karcie Komunikaty przełącz „Pokazuj szczegółowe podpowiedzi klawiatury
przy polach i listach”. Po wyłączeniu `Ctrl+K` ma powiedzieć tylko „Filtr
listy”. Po włączeniu ma podać także instrukcję Enter/strzałka w dół/Escape.
Otwórz `Ctrl+F` i `Ctrl+Shift+F`: nazwa pola pozostaje krótka („Szukaj w…”),
a jego opis ma wyjaśniać Enter, historię pod strzałką w dół i Escape. Lista
„Wyniki wyszukiwania” zachowuje tę krótką nazwę, a osobny opis wyjaśnia
strzałki, Enter i Escape. Po Zapisz zmiana ma działać od razu i wrócić po
ponownym otwarciu Ustawień oraz restarcie programu. Wyłączona opcja nie może
pozostawić starych tekstów pomocy.

Na karcie Radio i nagrywanie sprawdź kolejno:

1. Długość TimeShift od 10 minut do 12 godzin; NVDA ma czytać czas i miejsce
   buforowania, a nie samą liczbę ani wartość modelu.
2. Wybór osobnego folderu oraz przycisk przywracający domyślny folder AMC.
   Pole ma podawać ścieżkę albo tekst „Domyślny folder nagrywania AMC”.
   Wybierz też „Ten sam folder co pobieranie Podcastów”, zapisz, ponownie
   otwórz Ustawienia i sprawdź, że pole osobnego folderu jest nieaktywne.
   Powrót do osobnego folderu ma przywrócić poprzednią ścieżkę Radia.
3. Format MP3, AAC, FLAC, oryginalny i WAV. Bitrate ma być dostępny tylko dla
   MP3 i AAC, a każda wartość ma zawierać jednostkę „kilobitów na sekundę”.
4. Preferowanie folderu stacji w nowym harmonogramie i domyślne wybudzanie.
   Po Zapisz otwórz ponownie Ustawienia i sprawdź zachowanie obu pól.
5. Gdy nic nie jest nagrywane, wykonaj krótką próbę `Ctrl+R` i otwórz nowy
   plan `Shift+R`: ręczne nagranie i formularz planu mają użyć zapisanego
   folderu, formatu i bitrate'u. Nie przerywaj istniejącego nagrania tylko na
   potrzeby tego odbioru. Zmiana długości TimeShift działa od kolejnego startu
   programu.
6. W odtwarzaczu Radia odczekaj co najmniej 12 sekund od uruchomienia stacji
   i naciśnij `S`. Ma paść „Rozpoznaję utwór”, a potem wynik albo uczciwa
   odmowa. `Shift+S` przełącza automatyczne obserwowanie. W Ustawieniach
   sprawdź trzy jawnie nazwane zakresy: odtwarzana stacja, nagrywane stacje
   oraz oba rodzaje źródeł. Brak odpowiedniego źródła ma uśpić automat, nie
   powodować prób sieciowych co kilkanaście sekund.
7. `Ctrl+Alt+S` otwiera trwałą historię. Strzałki mają czytać tytuł,
   wykonawcę, stację i czas bez nazw klas ani identyfikatorów. `Ctrl+C` kopiuje
   opis, `Ctrl+Shift+C` opis i łącza, Delete usuwa zaznaczone wpisy, a filtr
   stacji pozostaje zwykłym, dostępnym wyborem. Usunięty wpis przejęty kiedyś
   z pełnego AMC nie może wrócić po restarcie.
8. `Ctrl+Alt+Shift+S` przełącza samo oznajmianie. Po wyłączeniu automat nadal
   zapisuje wyniki do historii. Do Shazam wychodzi wyłącznie lokalnie
   obliczony podpis akustyczny; adres stacji i próbka PCM nie opuszczają hosta.

Na karcie Podcasty i YouTube sprawdź domyślny folder pobierania, odstęp
sprawdzania podcastów, osobny odstęp dla kanałów YouTube oraz liczbę źródeł
w jednym przebiegu. Zero przy odstępie ma być czytane jako wyłączenie
automatycznego sprawdzania. Zapisane wartości mają wrócić po ponownym
otwarciu i restarcie. Automat sprawdza tylko należne źródła, w partiach o
wybranym rozmiarze; brak nowości nie powinien przerywać NVDA komunikatem.

Na karcie Komunikaty wyłącz „Włącz komunikaty dostępności”, zapisz Enterem i
sprawdź, że rutynowe przewijanie, zmiana głośności oraz odtwarzanie/pauza nie
przerywają bieżącej mowy NVDA. Tekst stanu ma nadal trafiać do paska, a błędy
i odpowiedzi na wyraźne polecenia użytkownika nie mogą zostać ukryte. Otwórz
Ustawienia ponownie: przełącznik ma zachować wartość, mieć tę samą celową nazwę
i odzyskać zwykły fokus klawiatury. Po ponownym włączeniu komunikaty mają
wrócić bez restartu programu. Wykonaj ten odbiór również po ponownym
uruchomieniu AMC Python.

Pełny odbiór Ustawień pozostaje otwarty. Trzeba jeszcze przepisać całe karty
Profile klawiatury, Import i eksport oraz Aktualizacje, a następnie porównać
wszystkie pola, wartości domyślne, zależności, walidację i miejsca zapisu z
głównym AMC.

Na karcie Komunikaty sprawdź listę „Zdarzenia komunikatów”. NVDA ma czytać
wyłącznie polskie nazwy zdarzeń, nigdy klucze z kropką ani reprezentację
obiektu. Zmień „Dodanie do kolejki” na `W kolejce: {item}`, zapisz Enterem i
dodaj plik do kolejki: ma paść nowy tekst. Następnie ustaw pusty tekst,
zapisz, ponów operację i potwierdź brak tego jednego komunikatu. Ponowne
otwarcie Ustawień ma zachować oba wybory. Po wyłączeniu „Włącz komunikaty
dostępności” zwykły komunikat nie jest mówiony automatycznie, lecz jego tekst
pozostaje dostępny w polu statusu; błąd wykonania nadal musi zostać oznajmiony.

Porównaj z głównym AMC każdą sesję i każdy widok: menu, skróty, strukturę
Biblioteki, Enter, Escape/Backspace, lewą i prawą strzałkę oraz wszystkie
sortowania `Alt+cyfra`. Wynik audytu zapisz jako macierz widok–skrót–działanie–
powrót. Nie uznawaj jednego działającego sortowania ani jednej listy za dowód
zgodności całego interfejsu.

### Zwykła natywna lista: jak sprawdzać aktualizacje (po migracji z `LC_VIRTUAL`)

Lista jest zwykłą `wx.ListCtrl` (`LC_REPORT`) i trzyma teksty u siebie. Kontrolka
jest TRWAŁA — nie wolno jej niszczyć i odtwarzać przy zmianie danych (ta droga
była mierzona i odrzucona: zabierała mowę wybranego wiersza przy 2476 pozycjach).

Co sprawdzać:

- **Brak zbędnych aktualizacji.** Po samej strzałce, Ctrl+C, zmianie statusu czy
  ogłoszeniu widoku liczba operacji na liście ma być **0**. Mierz licznikiem
  operacji (`observer_native.py` owija `_apply_ops`/`sync_rows`), nie na oko.
- **Ten sam ID z nową treścią MA się odświeżyć.** Zmieniona nazwa, długość albo
  flaga Ulubione przy niezmienionym ID musi zaktualizować swoje pole. ID/kolejność
  i treść/stan porównywane są osobno — testuj oba warunki, nie jeden.
- **Jedna zmiana nie przepisuje listy.** Dodanie, usunięcie albo przeniesienie
  jednego wiersza daje jedną operację, nie czyszczenie całości. Pełna podmiana
  jest dopuszczalna tylko przy rzeczywistej zmianie całego zbioru.
- **Fokus i wybór to OSOBNE własności.** Sprawdzaj oba (`GetFirstSelected` nie
  wystarcza) i zachowanie świadomego ruchu użytkownika.
- **Programowa selekcja nie może zmienić modelu.** W trakcie podmiany wiersze
  wysyłają `EVT_LIST_ITEM_SELECTED`; bramka `updating` pilnuje, żeby przejściowy
  indeks nie wszedł jako nowy wybór. Prawdziwy ruch użytkownika musi nadal dojść.
- **Strzałki natywne.** Góra/dół działają same, bez naszej obsługi.
- **Pierwsze wypełnienie na prawdziwym wxMSW.** Model wybiera wiersz 0 przed
  wstawieniem go do kontrolki. Nie wolno wywołać `GetItemState(0)`, dopóki
  `GetItemCount()` wynosi 0; wx zgłasza wtedy asercję i przerywa synchronizację
  przed `InsertItem`. Sam działający Enter nie dowodzi, że kontrolka ma wiersze,
  ponieważ aktywacja korzysta z osobnego modelu.
- **Pusta lista i przejścia mały↔duży** (np. 1 → 2475 → 9) bez degradacji mowy.

Pomiar czasu planu na pełnej skali: `python3 tools/measure_list_sync.py 2476`.
Odbiór 10 widoków i kwity: `amc_pomoc/wx-full-profile-after421/native-lists-after422/`.

Pusta lista jest odebrana: nazwa „Pliki lokalne”, rola NVDA 14 (MSAA LIST ma
inny numer: 33), właściwy komunikat i wyjście Backspace. Dawna para A/B z
`native-lists-after422/diag_pustej.py` była nadpisana i nie dowodziła wersji
źródłowej usterki. Przy ponawianiu prób używaj `observer_w02v.py`, pełnego
przekazywania argumentów wrapperów, PID/runID producenta, zgodności modelu
z `GetItemCount` i czystego stdout; sam licznik modelu nie dowodzi wypełnienia GUI.

## Lewa strzałka i widoki Radia — jak mierzyć na żywo

Scena, kwit i gesty: `amc_pomoc/wx-integrated-20261007/live/`. Reuzyj tej
aparatury, nie buduj nowej.

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File launch-integ.ps1 integ-live
powershell.exe -NoProfile -ExecutionPolicy Bypass -File integ-gestures.ps1 <PID> 'end,left' <HWND>
powershell.exe -NoProfile -ExecutionPolicy Bypass -File read-speech-viewer.ps1
python3 mk_receipt.py <kwit> <speech.txt> receipt.json
```

- **`AMC_WX_FIXTURE` MUSI być puste.** Fixture daje `PRIVATE_SANDBOX` i Radio z
  zerem stacji, więc na nim filtry `isInLibrary`/`isFavorite` i cały
  `READ_ONLY_MIRROR` są nietestowalne. Zamiast fixture przekieruj `APPDATA`
  i `LOCALAPPDATA` na **własną pełną kopię** profilu (oba, nie jeden).
- **Mowa to Podgląd mowy NVDA, nie nasz log.** `read-speech-viewer.ps1` czyta
  okno przez UIA. Nasz własny `announcer.say` dowodzi tylko, że wywołaliśmy
  funkcję — nie że użytkownik to usłyszał.
- **Sonda musi łapać `say`, nie zwrotkę.** `quick_info_reply`/`quick_info_failure`
  zwracają `None`; treść idzie wyłącznie przez `say`. Owijaj `say`, bo inaczej
  zapiszesz `None` i uznasz to za „brak mowy”.
- **Podmieniaj nazwy TAKŻE w `gui.py`.** `gui.py` robi `from .quick_info import
  quick_info_plan`, więc podstawienie w samym module `quick_info` nie dociera do
  okna.
- **Folder, plik i stacja to trzy osobne drogi.** Folder kończy się na
  `plan.message` bez żądania do hosta; plik i stacja idą przez silnik. Zmierz
  wszystkie trzy — jedna przechodząca nie dowodzi pozostałych.
- **Pole filtra to kryterium braku regresji.** Wejdź w filtr (`Shift+Tab` z
  listy; `F6` tam NIE wchodzi), wpisz tekst, naciśnij lewą strzałkę i wymagaj
  **zera** wywołań quick-info w tym oknie czasu.
- **W menu wx `End` nie skacze na koniec.** Nawiguj strzałkami w dół; `End`
  zostawia kursor na pierwszej pozycji i cicho zmierzysz nie tę pozycję, co
  chciałeś.
- **Listy mierzymy bez nakładek.** Biblioteka, Radio, Podcasty, harmonogramy i
  presety mają korzystać z natywnych obiektów wx/Windows. Nie wolno dołączać
  `wx.Accessible`, znacznika HWND ani `chooseNVDAObjectOverlayClasses` do
  zmiany nazw wierszy lub informacji o ich pozycji.
- **Nie licz pierwszego snapshotu.** Pierwsze ujęcie pada przed załadowaniem
  wierszy; jego `model_count=0` opisuje sondę, nie profil. Bierz maksimum z
  przebiegu.
## Frontier Smart: tryby i presety

1. Włącz sesję Frontier Smart i wejdź Enterem na zapisane urządzenie, aby
   otworzyć odtwarzacz. `Ctrl+Alt+M` ma otworzyć listę trybów tego urządzenia.
   Ten sam skrót musi być podany przy `Odtwarzanie → Wybierz tryb Frontier
   Smart…`, w `Ctrl+F1` i w palecie `Ctrl+Shift+K`.
2. Na liście trybów sprawdź początkowe zaznaczenie, strzałki, Enter, Escape,
   powrót fokusu oraz ponowne otwarcie. NVDA ma czytać wyłącznie nazwy trybów,
   bez klucza liczbowego, identyfikatora i reprezentacji obiektu.
3. `Ctrl+Alt+P` ma w razie potrzeby włączyć nawigację urządzenia, poczekać na
   jej gotowość i otworzyć listę rzeczywiście dostępnych presetów. Pierwsza
   próba po przełączeniu trybu nie może kończyć się samą techniczną blokadą
   węzła. Brak presetów w trybie, który naprawdę ich nie obsługuje, nadal jest
   poprawnym wynikiem.
4. Na liście presetów sprawdź początkowe zaznaczenie, odczyt nazwy i numeru,
   strzałki, Enter, Escape, powrót fokusu oraz ponowne otwarcie. Uruchomienie
   presetu ma przełączyć radio i zaktualizować odtwarzacz bez ujawniania PIN-u,
   adresu IP ani nazw węzłów FSAPI.

## Sonos: przekazanie do przeglądarki i powrót bez niedostępnego okna

1. W rozłączonej sesji Sonos sprawdź, że zwykła lista zawiera jeden wiersz
   `Rozpocznij logowanie do Sonos`. Enter otwiera `Konto Sonos`, w którym jest
   jedno wielowierszowe pole `Informacje o logowaniu Sonos`; nie mogą być dwa
   pola powtarzające tę samą instrukcję.
2. `Rozpocznij logowanie` ma przygotować próbę i automatycznie otworzyć
   oficjalną stronę w domyślnej przeglądarce. Po udanym otwarciu modal znika.
   Po Alt+Tab do AMC okno główne musi być dostępne, a lista zawierać jeden
   wiersz `Sprawdź logowanie do Sonos`. `Ctrl+1`, `Ctrl+2` i pozostałe globalne
   polecenia nie mogą być blokowane przez ukryty modal.
3. Enter na `Sprawdź logowanie do Sonos` ponownie otwiera konto i ustawia fokus
   na `Sprawdź logowanie`. Sprawdź też `Otwórz stronę ponownie`, kopiowanie
   adresu, `Anuluj logowanie`, Escape, ponowne otwarcie i przewidywalny powrót
   fokusu. AMC nie może automatycznie rozpoczynać drugiej próby OAuth.
4. Zasymulowany błąd otwarcia przeglądarki pozostawia modal otwarty, podaje
   użytkowy komunikat i ustawia fokus w czytelnym polu informacji. Nie może
   ogłosić sukcesu ani pozostawić wyłączonego okna bez drogi powrotu.
5. W Sonosie, WiiM, TIDAL-u, Spotify i Frontier Smart naciśnij
   `Ctrl+Alt+Shift+R`. Polecenie ma wykonać globalne zatrzymanie nagrań Radia
   albo uczciwie powiedzieć, że nic nie jest nagrywane; nie może wymagać
   wcześniejszego przełączenia do sesji Radia. Ta sama nazwa i skrót muszą być
   widoczne w menu, `Ctrl+F1`, pełnej pomocy i palecie `Ctrl+Shift+K`.
