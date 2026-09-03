# Dostępny kontroler multimedialny — prototyp dla Windows

Wersja `alpha.227` wzmacnia ochronę fokusa podczas odtwarzania. AMC sprawdza
teraz nie tylko logiczny fokus WPF, lecz także rzeczywisty fokus natywnego
okna Windows. Dzięki temu osadzony pasek stanu nie może pozornie pozostawić
fokusa na odtwarzaczu, a faktycznie odebrać klawiatury i NVDA. Pasek nadal
działa z `NVDA+End`, ale nie wysyła co sekundę zbędnego zdarzenia zmiany nazwy,
które mogło przenosić obiekt nawigatora NVDA. Wykryta utrata fokusa jest
naprawiana i zapisywana w logu diagnostycznym.

Wersja `alpha.226` naprawia przejście o poziom wyżej w Bibliotece Podcastów.
`Ctrl+L` nadal może zgodnie z zapamiętanym miejscem otworzyć odcinki ostatnio
przeglądanej audycji, lecz Escape albo Backspace z tej listy zawsze prowadzi
bezpośrednio do nadrzędnej listy podcastów i ustawia fokus na właściwej
audycji. Powrót nie zależy już od przypadkowej historii wcześniejszych widoków.
Po takim świadomym wyjściu kolejne `Ctrl+L` pozostaje na nadrzędnej liście.

Wersja `alpha.225` dodaje trzy trwałe sposoby uporządkowania skrzynki **Nowe
odcinki**: `Alt+1` — najnowsze odcinki najpierw, `Alt+2` — alfabetycznie według
tytułu odcinka, `Alt+3` — grupami według nazwy podcastu, a wewnątrz grupy od
najnowszego. Trzeci wariant nie jest kolejnością własną i nie pozwala ręcznie
przesuwać automatycznej skrzynki. `Ctrl+C` w Podcastach kopiuje nazwę, pełny
opis i publiczną stronę każdego odcinka, natomiast `Ctrl+Shift+C` kopiuje
wyłącznie bezpośrednie adresy audio. Oba skróty obejmują cały ciągły zakres
zaznaczony Shiftem i działają również w wynikach wyszukiwania.

Wersja `alpha.224` naprawia przejście z wyników wyszukiwania Podcastów.
Znaleziony nagłówek audycji prowadzi do nadrzędnej Biblioteki, a odcinek — do
listy właściwej audycji z fokusem na tym odcinku. Wewnętrzny, płaski agregat
wszystkich audycji i odcinków nie może już pojawić się jako lista użytkownika;
starszy zapis takiego widoku jest bezpiecznie zastępowany ostatnim miejscem w
Bibliotece Podcastów. Wyniki jawnie odróżniają podcast w Bibliotece, podcast
spoza Biblioteki, wynik katalogu Apple Podcasts oraz odcinek wraz z audycją
nadrzędną. Zwykły Enter nadal tylko przechodzi z okna wyszukiwania na właściwą
listę — następny Enter otwiera audycję albo uruchamia odcinek.

Wersja `alpha.223` naprawia niepełną inicjalizację **Nowych odcinków** po
imporcie wykonanym we wcześniejszych wersjach Podcastów. Dla każdej
obserwowanej audycji bez nowej pozycji AMC dodaje do skrzynki tylko jej
najnowszy nierozpoczęty odcinek. Nie przywraca odsłuchanych materiałów i nie
zalewa skrzynki całym archiwum. `Ctrl+I` szybko otwiera zapisany stan skrzynki,
a `F5` pobiera aktualizacje wszystkich obserwowanych kanałów. Komunikat po
odświeżeniu rozróżnia liczbę odcinków dodanych teraz od łącznej liczby pozycji
w skrzynce, a log wskazuje nazwę kanału, którego nie udało się odświeżyć.

Wersja `alpha.222` zachowuje pozycję po wyjściu Escape z odtwarzacza do
widoku **Nowe odcinki**. Jeżeli odcinek nadal jest nowy, fokus wraca dokładnie
na niego. Jeżeli podczas słuchania przestał być nowy i zniknął ze skrzynki,
AMC wybiera najbliższy odcinek w jego dawnej pozycji zamiast pierwszego wiersza
listy. Ta sama reguła bezpiecznego powrotu obejmuje inne listy tymczasowe i
wiersze reprezentujące odtwarzany element pośrednio.

Wersja `alpha.221` chroni fokus podczas odtwarzania. Po asynchronicznym
otwarciu albo zmianie pliku, podcastu lub stacji AMC przywraca fokus do
odtwarzacza albo bieżącej listy, jeżeli WPF pozostawił go na niewidocznym
elemencie. Zamknięcie menu kontekstowego odtwarzacza również jawnie wraca do
odtwarzacza. Ochrona nie przejmuje fokusa podczas Alt+Tab, w otwartym menu ani
w oknie dialogowym. Każde rzeczywiste automatyczne odzyskanie jest zapisywane
w logu jako `focus-recovery`, ale nie dodaje komunikatu NVDA.

Wersja `alpha.220` naprawia odzyskiwanie dźwięku po odłączeniu wcześniej
wybranego urządzenia audio. AMC rozpoznaje, że zapamiętane wyjście zniknęło,
zachowuje ostatnią bezpieczną pozycję i po ręcznym wybraniu dostępnego
urządzenia ponownie uruchamia bieżący element. Świadomie zatrzymana albo
wstrzymana sesja nadal nie uruchamia się tylko z powodu zwykłej zmiany
urządzenia. Zmiana i wynik odzyskiwania są zapisywane w dzienniku bez
ujawniania technicznego identyfikatora urządzenia.

Wersja `alpha.219` dodaje polecenie **Przejdź do podcastu** dla odcinka
widocznego w Nowych odcinkach, Ulubionych, Kolejce, Historii, playliście,
wynikach wyszukiwania albo otwartym odtwarzaczu. Polecenie otwiera audycję
nadrzędną, ustawia fokus na tym samym odcinku i zapamiętuje to miejsce jako
punkt późniejszego powrotu przez `Ctrl+L`. Jest dostępne w odpowiednim menu
kontekstowym i palecie poleceń; nie zajmuje nowego domyślnego skrótu.

Wersja `alpha.218` zachowuje dokładne miejsce wewnątrz Biblioteki Podcastów.
Jeżeli użytkownik otworzy audycję, zaznaczy odcinek, przejdzie do Kolejki,
Ulubionych albo innego widoku i wróci przez `Ctrl+L`, AMC ponownie otwiera tę
samą audycję i zaznacza wcześniejszy odcinek. Backspace i Escape nadal
przechodzą świadomie o poziom wyżej. Zapamiętane miejsce jest sprawdzane przy
starcie; jeżeli audycję usunięto z Biblioteki, program bezpiecznie pokazuje
nadrzędną listę podcastów.

Wersja `alpha.217` uruchamia publiczne wyszukiwanie podcastów przez katalog
Apple Podcasts. `Ctrl+F` w sesji Podcasty szuka obserwowanych audycji,
zapisanych odcinków i wyników katalogu; Enter na wyniku katalogowym sprawdza
publiczny kanał RSS lub Atom, dodaje audycję do Biblioteki i otwiera jej
odcinki. Nie wymaga konta Apple i nie synchronizuje prywatnej biblioteki.
`Alt+D` na audycji albo odcinku otwiera pełny opis jako zwykły tekst tylko do
odczytu, po którym można poruszać się znakami i słowami, zaznaczać fragmenty,
kopiować oraz otwierać wykryte łącza. Opis nie jest automatycznie wypowiadany
podczas nawigacji po liście.

`Ctrl+I` przed zbudowaniem widoku przeładowuje bieżący stan Podcastów, dlatego
nie pokazuje pustej listy z powodu nieaktualnej kopii danych. Przy pierwszym
dodaniu audycji tylko jej najnowszy odcinek trafia do skrzynki; całe starsze
archiwum pozostaje dostępne wewnątrz audycji. Kolejne odświeżenia oznaczają
wszystkie rzeczywiście nowo odnalezione odcinki. `F5` w skrzynce i pozycja menu
odświeżają wszystkie obserwowane podcasty.

Wersja `alpha.216` ujednolica sortowanie trwałych kolekcji. W Bibliotece i
Ulubionych `Alt+1` wybiera kolejność dodania z najnowszymi pozycjami na
początku, `Alt+2` — alfabet, a `Alt+3` — trwałą kolejność własną. Wybrany
sposób jest pamiętany osobno dla widoku i sesji. Dopiero w kolejności własnej
`Alt+strzałka w górę/w dół` przenosi pozycje. Kolejki i playlisty zachowują
kolejność odtwarzania, albumy kolejność ścieżek, a wyszukiwanie, Historia,
Nowe odcinki i Nagrywane nie przejmują tych skrótów. Lokalna Biblioteka
zachowuje wcześniejszy wyjątek: `Alt+1` to Foldery, `Alt+2` płaska lista
alfabetyczna, `Alt+3` Kolejność własna; w lokalnych Ulubionych obowiązuje już
wspólna reguła kolekcji. Zwykłe cyfry nie mogą zostać potraktowane jako
`Alt+cyfra` po szybkim przejściu między widokami.

Reguła przeznaczona również do przyszłego podręcznika jest stała: Biblioteka i
Ulubione używają `Alt+1` dla kolejności dodania od najnowszych, `Alt+2` dla
alfabetu i `Alt+3` dla kolejności własnej. Ręczne przesuwanie
`Alt+strzałka w górę/w dół` działa tylko w kolejności własnej. Kolejka i
playlista zachowują kolejność odtwarzania, album — numery ścieżek, a Historia,
wyniki wyszukiwania, Nowe odcinki i Nagrywane własny porządek wyliczany.
Lokalna Biblioteka jest jedynym wyjątkiem: `Alt+1` oznacza Foldery,
`Alt+2` Wszystkie pliki alfabetycznie, a `Alt+3` Kolejność własną. Filtr
`Ctrl+K` jedynie zawęża widoczną listę i nigdy nie zmienia ani nie zapisuje jej
porządku.

Wersja `alpha.213` utrzymuje fokus na liście po Escape użytym na najwyższym
poziomie widoku; klawisz nie przenosi już użytkownika do pola filtra. Pusta
skrzynka Podcastów mówi „Brak nowych odcinków”, a `F5` użyte w skrzynce
odświeża wszystkie podcasty z Biblioteki. Nowy rozpoczęty odcinek pozostaje w
skrzynce do ukończenia, dzięki czemu rozpoczęcie odtwarzania nie usuwa go bez
możliwości powrotu.

Wersja `alpha.212` nie powtarza na liście identycznej nazwy zapisanej w kilku
polach metadanych. Jeśli kanał podcastu podaje ten sam tekst jako tytuł i autora,
NVDA czyta go tylko raz; pozostałe różne informacje nadal są zachowane.

Wersja `alpha.211` usuwa z początku użytkowej nazwy autora podcastu techniczne
oznaczenia praw `℗`, `©`, `®` i `™`. Przykładowe `℗&© Polskie Radio PiK` jest
czytane przez NVDA jako „Polskie Radio PiK”; oryginalna wartość kanału RSS
pozostaje zachowana. Na listach zagnieżdżonych Escape działa teraz jak
Backspace i wraca o jeden poziom, z zachowaniem osobnych reguł odtwarzacza,
filtra, pól edycyjnych, menu i okien dialogowych.

Wersja `alpha.210` zachowuje zmienioną prędkość Podcastów po wyjściu z
odtwarzacza klawiszem Escape i ponownym otwarciu odcinka. Wybrana wartość staje
się bieżącą prędkością sesji Podcasty, jest od razu stosowana przy kolejnym
odtworzeniu i nadal pozostaje zapisana po ponownym uruchomieniu AMC.

Wersja `alpha.209` dodaje w sesji Radio skrót `Ctrl+Alt+Shift+S`, który
włącza lub wyłącza wyłącznie automatyczne oznajmianie rozpoznanych utworów.
Rozpoznawanie uruchomione przez `Shift+S` nadal pracuje w tle, a wyniki nadal
trafiają do historii `Ctrl+Alt+S`. Ręczne rozpoznanie klawiszem `S` pozostaje
odpowiedzią na żądanie. Bieżący stan jest zapisywany, widoczny w menu
Odtwarzanie, menu kontekstowym odtwarzacza, Ustawieniach, palecie poleceń i
spisie skrótów. Wyłączenie mowy jest potwierdzane jednoznacznym komunikatem.

Wersja `alpha.208` naprawia przewijanie sieciowych odcinków Podcastów,
pozostawia bieżący odcinek w Kolejce do zakończenia lub przejścia dalej,
ujednolica kolejność tytułu i autora oraz zapisuje własną nazwę podcastu z `F2`.

Wersja `alpha.207` naprawia odtwarzanie sieciowych odcinków Podcastów. Dekoder
normalizuje MP3, MP4 i inne materiały HTTP/HTTPS do formatu wymaganego przez
wspólny tor regulacji prędkości. Poprawkę sprawdzono na rzeczywistych odcinkach
MP3 44,1 kHz i MP4 48 kHz, które w `alpha.206` kończyły się błędem przed
uruchomieniem wyjścia audio.

Wersja `alpha.206` uruchomiła pierwszy kompletny przepływ subskrypcji i
odtwarzania Podcastów.
W sesji Podcasty `Ctrl+N` sprawdza i dodaje bezpośredni kanał RSS/Atom,
`Ctrl+O` importuje wybrane kanały z OPML, `F5` odświeża bieżącą audycję, a
`Ctrl+F5` całą Bibliotekę. Enter na audycji otwiera jej odcinki, Backspace
wraca, Delete wypisuje, `Ctrl+C` kopiuje nazwę i publiczną stronę, jeśli kanał
ją podaje, a `Ctrl+Shift+C` nazwę i bezpośredni adres. Klient ogranicza czas, przekierowania i rozmiar odpowiedzi,
nie przyjmuje niebezpiecznego XML i podczas odświeżania nie pobiera audio.
Enter na odcinku odtwarza skończony materiał HTTP/HTTPS we wspólnym
odtwarzaczu, który zachowuje pozycję, prędkość, Historię i Zakładki.

W poprawionym imporcie OPML strzałki wyłącznie nawigują, Spacja niezależnie
zaznacza lub odznacza bieżący podcast, a `Ctrl+A` zaznacza wszystkie. Lista
harmonogramów nagrywania zaczyna każdy wiersz od nazwy stacji i po tej nazwie
obsługuje szybkie przechodzenie literami; stan włączony albo wyłączony jest
czytany bezpośrednio po nazwie.

Menu przekazuje teraz każdy znany skrót do NVDA również przez pole skrótu UI
Automation, zamiast polegać wyłącznie na tekście widocznym. Dla lokalnego lub
pobranego pliku pozycja **Pokaż plik w folderze** zaznacza oryginał w
Eksploratorze Windows. Dla zdalnego podcastu i odcinka osobna pozycja otwiera
jego publiczną stronę w przeglądarce; AMC nie używa już niejednoznacznego
polecenia „Otwórz w domyślnej aplikacji”. `Ctrl+I` otwiera **Nowe odcinki**.

Wersja `alpha.201` zabezpiecza ręczny podział nagrania radia przed szybkim podwójnym naciśnięciem `T`. Pierwsze `T` finalizuje bieżącą część i natychmiast kontynuuje zapis w nowym pliku; ponowne `T` w ciągu pierwszych pięciu sekund nowej części jest bezpiecznie pomijane zamiast zatrzymywać nagranie. Każda zamknięta część jest od razu dodawana do lokalnej Biblioteki, choć kolejna część nadal się nagrywa. `Shift+T` zachowuje dotychczasowe znaczenie i nie jest drugim poleceniem podziału.

Wersja `alpha.200` dodaje świadome usuwanie zaznaczonego przedziału z oryginalnego pliku audio przez `Ctrl+X` w odtwarzaczu. Operacja wymaga potwierdzenia domyślnie ustawionego na „Nie”, zatrzymuje odtwarzanie, nie kompresuje dźwięku ponownie, sprawdza gotowy wynik i dopiero wtedy podmienia źródło. Pełna, bitowo identyczna kopia otrzymuje końcówkę `.amc-backup`; błąd pozostawia oryginał bez zmian. Pliki wymagające pobrania z chmury i pliki wideo są bezpiecznie odrzucane.

Wersja `alpha.199` zapamiętuje osobne zaznaczenie fragmentu dla każdego lokalnego pliku w bazie Biblioteki SQLite. Początek i koniec wracają po zmianie pliku oraz ponownym uruchomieniu AMC. `Shift+X` usuwa wyłącznie zaznaczenie bieżącego pliku; plik źródłowy pozostaje nietknięty.

Wersja `alpha.198` dodaje kierunkową nawigację po granicach zaznaczonego fragmentu: `Alt+Page Up` przechodzi do poprzedniego, a `Alt+Page Down` do następnego punktu cięcia. Skróty nie zapętlają granic; `Shift+I` i `Shift+O` pozostają bezpośrednimi skokami do początku i końca.

Wersja `alpha.197` instaluje i aktualizuje własny, oddzielny składnik FFmpeg 9 dla Windows x64. AMC korzysta ze stabilnego wariantu LGPL shared od dostawcy wskazanego na oficjalnej stronie FFmpeg, porównuje pobrane archiwum z sumą SHA-256 wydania i przed uaktywnieniem sprawdza program, wersję oraz wariant licencji. Aktualizacja odbywa się w tle, wersjami i bez usuwania działającej kopii po błędzie. `Shift+I` i `Shift+O` przechodzą odpowiednio do zaznaczonego początku i końca fragmentu.

Wersja `alpha.196` dodaje pierwsze bezpieczne narzędzie do wycinania nagrań. W odtwarzaczu Plików lokalnych `I` oznacza początek, `O` koniec, `X` otwiera zapis fragmentu, a `Shift+X` usuwa zaznaczenie. Operacja zawsze tworzy nowy plik i nie modyfikuje źródła. Dokładny WAV działa bez dodatkowych składników; zapis bez konwersji oraz dokładny FLAC są dostępne po wykryciu właściwego FFmpeg.

Korekta `alpha.195` rozdziela dwa sposoby korzystania z Historii odtwarzania. Świadome otwarcie elementu z listy `Ctrl+H` ustawia widoczną Historię jako kontekst `Page Up/Page Down`, tak samo jak Ulubione, folder, playlista lub kolejka ustawiają własny kontekst. Natomiast chwilowe przejścia `Alt+strzałka w górę/w dół` wykonywane już w odtwarzaczu nadal nie zastępują wcześniejszego kontekstu listy.

Korekta `alpha.194` oddziela pozycję odsłuchu radia od zapisu na żywo. Spacja może teraz pauzować i wznawiać odsłuch z timeshiftu także podczas nagrywania tej samej stacji, podczas gdy rejestrator nadal zapisuje transmisję na żywo; `End` pozostaje jawnym powrotem na żywo. Przygotowanie podpisu Shazam zostało przeniesione poza wątek okna, a bufory FFT są ponownie używane zamiast tworzenia tysięcy dużych tablic przy każdym rozpoznaniu. Ogranicza to krótkie przerwy odsłuchu i przycięcia interfejsu podczas automatycznego rozpoznawania.

To pierwszy demonstracyjny prototyp aplikacji sterowanej globalnym prefiksem. Sprawdza architekturę klawiatury, sesji, list, komunikatów dostępności, profili oraz importu i eksportu. Nie łączy się jeszcze z prawdziwymi kontami TIDAL, Apple Music ani WiiM.

Ten README opisuje zachowanie bieżącego prototypu. Wspólny numer wersji jest zapisany w `Directory.Build.props`, dzięki czemu rdzeń, okno i publikowany program zawsze otrzymują ten sam numer. Zatwierdzony kierunek dalszego rozwoju, docelowa architektura oraz pełna mapa skrótów znajdują się w [`MEDIA_CONTROLLER_PL.md`](MEDIA_CONTROLLER_PL.md), a następny moduł opisuje [`PROJEKT_PODCASTOW_PL.md`](PROJEKT_PODCASTOW_PL.md). Trwałe reguły następstwa po zniknięciu pliku i komunikatów ręcznego przenoszenia są dodatkowo zebrane jako niezmienniki w punkcie 7.8 tej specyfikacji; przyszłe adaptery i przebudowy interfejsu nie mogą ich omijać.

Aktywne repozytorium robocze powinno znajdować się na zwykłym lokalnym woluminie NTFS, poza iCloud Drive, Google Drive, OneDrive i innymi katalogami synchronizowanymi. GitHub przechowuje historię kodu, natomiast atomowe kopie danych użytkownika mogą być eksportowane do chmury. Na głównym komputerze testowym stałą ścieżką projektu jest `D:\Projekty Codex\Accessible Multimedia Controller`.

Od `alpha.165` menu przekazuje NVDA nazwę polecenia i skrót jako dwa odrębne pola UI Automation. Nazwa dostępności nie zawiera już drugiej kopii `Ctrl+…`, a wewnętrzne litery dostępu pozycji nie są odczytywane jako dodatkowe pojedyncze klawisze. Zmiana obejmuje menu główne, menu kontekstowe listy i odtwarzacza oraz wyniki wyszukiwania; litery dostępu głównych kategorii menu pozostają dostępne przez Alt.

Od `alpha.166` formularz `Shift+R` zapisuje osobny format i bitrate dla każdego jednorazowego nagrania oraz każdego harmonogramu; dawne plany bez tych pól dziedziczą ustawienie ogólne. Lista harmonogramów ma semantykę pól wyboru: strzałki wybierają plan, a Spacja go włącza lub wyłącza. `Alt+2` pokazuje nagrywane stacje, `Shift+Spacja` wstrzymuje wybrane nagranie, a `Ctrl+Alt+R` kończy nagrywanie wybranej stacji, także z harmonogramu. `Ctrl+Alt+Shift+R` zatrzymuje wszystkie nagrania; `Ctrl+Shift+H` pozostaje jedynym skrótem listy harmonogramów. Spacja pauzuje wyłącznie słyszalny odsłuch również podczas nagrywania: zapis na żywo trwa dalej, a wznowienie wraca do zatrzymanego punktu timeshiftu. `End` przechodzi jednoznacznie na żywo. Każda stacja pamięta własną głośność odsłuchu. Historia rozpoznawania `Ctrl+Alt+S` ma również menu kontekstowe do otwierania wyniku, kopiowania, eksportu i usuwania.

Od `alpha.167` opcja **Ustawienia > Komunikaty > Oznajmiaj automatycznie rozpoznane utwory** oddziela obserwowanie Shazam od mowy. Po jej wyłączeniu `Shift+S` nadal rozpoznaje i zapisuje wyniki w historii, lecz nie przerywa pracy komunikatem. Po włączeniu automatyczny wynik jest wypowiadany wyłącznie wtedy, gdy okno AMC jest aktywne. Ręczne `S` nadal odpowiada w aktywnym oknie; jeśli użytkownik przełączy się do innego programu przed zakończeniem zapytania, wynik pozostaje w historii bez komunikatu poza AMC. Stan opcji jest widoczny również w palecie poleceń.

Od `alpha.168` w **Ustawienia > Ogólne > Odtwarzanie** są trzy domyślnie wyłączone opcje dla lokalnych plików. Normalizacja wyrównuje odczuwaną głośność w locie, nie zmieniając pliku ani ustawionej głośności, i ogranicza szczyty przed przesterowaniem. Łagodne przejścia przez półtorej sekundy wygaszają naturalny koniec i wprowadzają następny utwór; ręczna zmiana może w tym czasie krótko nałożyć oba tory. Dodatkowa cisza po naturalnym końcu ma jawne wartości od braku ciszy do pięciu sekund i nie dotyczy pauzy, ręcznej zmiany ani Radia. Paleta poleceń pokazuje bieżący stan każdej opcji i otwiera właściwą kontrolkę bez ujawniania technicznych identyfikatorów NVDA.

Od `alpha.169` te same opcje są dostępne bez otwierania Ustawień: w menu **Odtwarzanie** oraz w menu kontekstowym odtwarzacza sesji Pliki lokalne. Normalizacja i łagodne przejścia są polami zaznaczanymi z aktualnym stanem w nazwie, a podmenu ciszy pozwala wybrać dokładną wartość. Domyślny profil globalnego prefiksu dodaje `Shift+N` dla normalizacji, `T` dla przejść oraz `C` do cyklicznego wyboru ciszy; własne profile zachowują swoją mapę i mogą przypisać te polecenia w Ustawieniach. Skrót i etykieta pozostają osobnymi informacjami UI Automation, żeby NVDA nie powtarzał ich w menu.

Od `alpha.170` pobrany i przypięty plik iCloud jest odróżniany od pliku dostępnego tylko online. Taki lokalny plik nie otrzymuje już pięciominutowych limitów chmurowych i może skorzystać z awaryjnego dekodera MP3. Jeśli wznowienie istniejącego toru Media Foundation po zamknięciu pomocy, pauzie albo powrocie z listy nie powiedzie się, AMC odrzuca uszkodzony tor i automatycznie otwiera plik ponownie w zapamiętanej pozycji. Nadzór wykrywa też tor oznaczony jako odtwarzający, lecz nieprzesuwający pozycji; przy samym końcu rozpoznaje brakujące zdarzenie końca, a wcześniej bezpiecznie uruchamia odzyskiwanie lub zatrzymuje dekoder z komunikatem.

`Alt+Shift+Enter` dla lokalnego pliku albo Folderu Biblioteki zawiera teraz normalizację, łagodne przejścia i ciszę po utworze. Każde pole może dziedziczyć wartość z najbliższego folderu i ustawień globalnych albo otrzymać własną wartość. Ustawienia globalne pozostają w karcie **Ustawienia > Ogólne > Odtwarzanie — ustawienia globalne** oraz w menu Odtwarzanie, którego etykiety jawnie mówią teraz „globalne”. Lista prędkości podaje `1,00 razy — normalna prędkość`, a wszystkie nowe wybory mają osobne użytkowe etykiety dla NVDA.

Od `alpha.171` aktywny odtwarzacz Plików lokalnych ma trzy bezpośrednie skróty bez globalnego prefiksu: `Shift+N` przełącza globalną normalizację głośności, `Shift+T` przełącza globalne łagodne przejścia, a `Shift+C` wybiera kolejną globalną długość ciszy. Skróty działają tylko wtedy, gdy fokus jest w lokalnym odtwarzaczu; nie przejmują tych klawiszy na liście ani w Radiu. Menu kontekstowe odtwarzacza pokazuje warianty bez prefiksu, podczas gdy menu główne **Odtwarzanie** i pomoc nadal podają także dotychczasowe skróty globalnego prefiksu.

Od `alpha.172` dostępność normalizacji, przejść i ciszy wynika z możliwości toru odtwarzania, a nie z nazwy sesji. Pliki lokalne obsługują cały zestaw. Przyszły adapter usługi lub urządzenia może zgłosić każdą funkcję osobno, jeżeli wykonuje ją w lokalnym torze AMC albo przez oficjalne API; wtedy pozycja pojawi się także w menu **Odtwarzanie**, menu kontekstowym jego odtwarzacza i pod `Shift+N`, `Shift+T` lub `Shift+C`. Adapter bez takiej możliwości nie pokazuje martwego polecenia. `Ctrl+Shift+S` jest teraz głównym skrótem listy sesji, `Ctrl+0` pozostaje aliasem, a `Ctrl+Shift+0` nadal oznacza preset 0. Historia Shazam pozostaje pod `Ctrl+Alt+S`.

Od `alpha.173` menu **Odtwarzanie** i menu kontekstowe lokalnego odtwarzacza zawierają osobną pozycję **Zmień opcje bieżącego utworu**. Jej nazwa podaje efektywną normalizację, przejścia i ciszę oraz źródło każdej wartości: ustawienie pliku, folderu albo globalne. Globalne przełączniki nadal jawnie pokazują wyłącznie poziom globalny, więc ustawienie folderu nie jest już mylone z brakiem zapisu. Potwierdzenie `Alt+Shift+Enter` odczytuje zapisane wartości, a w razie błędu zapisu nie mówi już nieprawdziwego „Zapisano”. Próby automatyczne obejmują trwałość ustawień pliku i folderu w SQLite oraz rozpoznanie źródła każdej wartości.

Od `alpha.174` bezpośrednie `Shift+N`, `Shift+T` i `Shift+C` są przejmowane na granicy komunikatów Windows, zanim WPF lub czytnik ekranu może zgubić stan Shifta i potraktować literę jak klawisz dostępu przycisku. Polecenie wykonuje się raz, komunikat podaje nowy stan globalny, a fokus pozostaje na dotychczasowym elemencie odtwarzacza. Przyciski odtwarzacza nie mają już ukrytych jednoliterowych mnemoników, więc niewspierana kombinacja również nie przenosi fokusu na „Cofnij” albo „Następny”. Skróty nadal działają wyłącznie w odtwarzaczu toru zgłaszającego odpowiednią możliwość; plik z własnym ustawieniem lub ustawieniem folderu może nadal przesłaniać zmienioną wartość globalną.

Od `alpha.175` numerowane widoki Radia tworzą spójną parę: `Alt+1` otwiera **Wszystkie stacje**, czyli Bibliotekę zapisanych stacji dostępną również przez `Ctrl+L`, a `Alt+2` otwiera **Nagrywane**. Widok Nagrywane jest tymczasowy: `Escape` lub Backspace wraca dokładnie do wcześniejszego widoku Radia, na przykład do Ulubionych, wraz z zapamiętanym zaznaczeniem; `Alt+strzałka w lewo/prawo` nadal obsługuje pełną historię. `Alt+3` pozostaje świadomie wolne, dopóki prawdziwy adapter wielu urządzeń lub usług Connect nie dostarczy listy równoczesnych odtworzeń. Dla pojedynczego bieżącego odtwarzacza właściwym skrótem nadal jest `F6`.

Od `alpha.176` formularz `Shift+R` zawiera edytowalny **Szablon nazwy pliku nagrania**, podgląd wyniku oraz dostępne z klawiatury menu **Wstaw token lub wybierz gotowy szablon**. Domyślna nazwa `{stacja} - {data} {czas}` daje na przykład `Radio Łódź - 2026-08-31 20-15.mp3`; rozszerzenie jest zawsze dodawane automatycznie według formatu planu. Gotowe szablony można dalej swobodnie edytować. Tokeny obejmują stację, trzy formaty daty, rok, miesiąc, dzień miesiąca, polską nazwę dnia tygodnia, czas, godzinę, minutę i dwucyfrowy numer części. Nazwa jest zapisywana osobno z każdym harmonogramem, działa także dla planów cyklicznych oraz dzielonych, a znaki niedozwolone przez Windows są bezpiecznie zastępowane. Starsze plany otrzymują szablon domyślny i zachowują dotychczasowy sposób nazywania.

Od `alpha.177` Spacja na liście `Ctrl+Shift+H` rzeczywiście przełącza wybrany harmonogram bez otwierania edytora. Obsługa klawisza należy bezpośrednio do listy, a po odświeżeniu fokus wraca na ten sam wiersz zamiast na ogólny kontener „Zaplanowane nagrania radia”. NVDA otrzymuje jawne powiadomienie zawierające stację, termin oraz stan „włączony” albo „wyłączony”; wiersz równocześnie zmienia stan pola wyboru. Zmiana nadal wymaga przycisku **Zapisz**, dzięki czemu **Anuluj** może ją bezpiecznie odrzucić.

Od `alpha.178` edytowalne pola tekstowe AMC mają wspólną regułę zastępowania wartości. Po wejściu klawiaturą cała dotychczasowa zawartość jest zaznaczona, więc pierwsza wpisana litera albo cyfra ją zastępuje; strzałka przed pisaniem pozwala zamiast tego poprawić fragment. Kliknięcie myszą nadal ustawia kursor w wybranym miejscu, a pola tylko do odczytu nie są zmieniane. Ta sama reguła obejmuje natywne pola liczbowe długości całego nagrania i długości części w `Shift+R`, dzięki czemu wpisanie `5` po wejściu na wcześniejsze `60` daje `5`, nie `605`. Pola daty i godziny zachowują wybór części: wpisywanie zastępuje aktualny dzień, miesiąc, rok, godzinę lub minutę.

Od `alpha.179` globalnego prefiksu nie wpisuje się już ręcznie w zwykłym polu. Przycisk **Zmień prefiks…** otwiera dostępne okno przechwytywania, w którym pierwsza naciśnięta kombinacja zastępuje całą poprzednią wartość; osobny przycisk przywraca `Ctrl+Alt+Windows+F12`. AMC rozróżnia zwykły Enter od Entera numerycznego, dlatego można ustawić na przykład `Ctrl+Enter numeryczny`. Przy zapisie nowy prefiks jest sprawdzany przed porzuceniem starego. Jeżeli kombinacja jest zajęta przez Windows, NVDA albo inny program, ustawienia pozostają otwarte, komunikat podaje przyczynę, a poprzedni prefiks nadal działa. Okno przechwytywania i komunikaty pokazują wyłącznie nazwy użytkowe, bez identyfikatorów poleceń.

Od `alpha.180` obserwowanie Shazam jest ustawieniem trwałym. Pole **Ustawienia > Radio i nagrywanie > Automatycznie obserwuj i rozpoznawaj utwory podczas odtwarzania radia** oraz `Shift+S` sterują tym samym stanem i zapisują go od razu; po ponownym uruchomieniu AMC obserwowanie pozostaje takie, jakie użytkownik zostawił. Włączenie jest możliwe również przed uruchomieniem stacji. Pierwsza próba następuje około sześciu sekund po rozpoczęciu odbioru, a brak wyniku powoduje ponowną próbę po 15 sekundach zamiast oczekiwania kolejnej pełnej minuty. Po udanym rozpoznaniu zwykły interwał pozostaje minutowy. Niezależna opcja na karcie **Komunikaty** nadal decyduje tylko o wypowiadaniu automatycznych wyników.

Od `alpha.188` karta **Radio i nagrywanie** pozwala wybrać zakres automatycznego rozpoznawania: tylko aktualnie słuchana stacja, tylko stacje nagrywane w tle albo oba rodzaje źródeł. `Shift+S` przełącza obserwowanie dla zapisanego zakresu, natomiast ręczne `S` zawsze dotyczy wyłącznie słuchanej stacji. Nagrania w tle są rozpoznawane z ich istniejącego, prywatnego bufora dekodera, bez drugiego połączenia ze stacją i bez zmiany pliku nagrania. Ta sama stacja odbierana równocześnie przez odtwarzacz i nagrywanie jest sprawdzana tylko raz w cyklu. `Ctrl+Alt+S` nadal otwiera jedną historię, lecz nowe pole **Pokaż wpisy** filtruje ją według stacji. „Wszystkie źródła” oznaczają wyłącznie źródła faktycznie odbierane przez AMC, nigdy cały katalog Radia.

Korekta `alpha.189` usuwa awarię okna zmiany prefiksu na 64-bitowym Windows. Plus numeryczny i pozostałe jednoznaczne klawisze korzystają ze zwykłej ścieżki WPF; natywny hak pozostaje tylko dla Entera i klawiszy, których nie da się inaczej odróżnić od osobnego bloku nawigacyjnego. Hak sprawdza rodzaj komunikatu przed odczytaniem kodu klawisza, dlatego komunikaty fokusu i UI Automation zawierające wartości wskaźnikowe nie są mylone z klawiaturą. Zarówno okno przechwytywania, jak i globalny hak prefiksu mają dodatkową granicę bezpieczeństwa: wyjątek jest zapisywany w logu, warstwa zostaje anulowana, a klawisz przekazany dalej. Błąd AMC nie może przez to pozostawić zablokowanego łańcucha klawiatury ani uciszyć NVDA.

Od `alpha.190` tymczasową listę **Nagrywane** otwiera w Radiu `Alt+R`, nie `Alt+2`. `Escape` lub Backspace zamyka ją i wraca dokładnie do widoku, z którego została wywołana, na przykład do Ulubionych wraz z wcześniejszym zaznaczeniem. `Alt+1` zachowuje jedno stałe znaczenie: **Wszystkie stacje**. Zwykła Spacja pauzuje wyłącznie słyszalny odsłuch; odbiór, bufor i niezależne nagranie pracują dalej. Wznowienie wraca do zatrzymanego punktu timeshiftu także podczas nagrywania, natomiast `End` przechodzi na żywo. `Shift+Spacja` pozostaje osobnym poleceniem pauzy samego nagrania i pomija w zapisywanym pliku czas tej pauzy.

Korekta `alpha.191` przywraca niezawodny zapis i ponowną rejestrację globalnego prefiksu. Starsze ustawienia mogły zawierać zapis `CTRL-Alt-Win-F12`, podczas gdy bieżący mechanizm oczekiwał separatorów `+`; po wyłączeniu Pomocy klawiszy taki prefiks nie dawał się ponownie uruchomić. AMC automatycznie normalizuje starszy zapis do `Ctrl+Alt+Windows+F12` zarówno przy wczytywaniu, jak i zapisie. Nieprawidłowa wartość w importowanej konfiguracji nie blokuje uruchomienia ani pozostałych danych — bezpiecznie wraca do prefiksu domyślnego. Zajęta nowa kombinacja nadal pozostawia aktywny poprzedni prefiks.

Korekta `alpha.192` skraca nawigację po historii odtwarzania w lokalnym odtwarzaczu: udane `Alt+strzałka w górę/w dół` mówi wyłącznie nazwę wybranego pliku, bez powtarzania słowa „Historia”. Komunikaty o pustej historii oraz jej początku lub końcu pozostają jednoznaczne. Pomoc Radia rozdziela trzy działania: Spacja pauzuje sam odsłuch, `Ctrl+M` wycisza słyszalne wyjście bez pauzy, a `Shift+Spacja` pauzuje wyłącznie nagranie.

Korekta `alpha.193` naprawia globalny prefiks złożony z samego klawisza bloku numerycznego. Windows mógł przyjąć rejestrację Plusa numerycznego, lecz fizyczne naciśnięcie przejmował wcześniej hak klawiatury NVDA. Wszystkie numeryczne cyfry i operatory globalnego prefiksu korzystają teraz z istniejącego, zabezpieczonego haka AMC, natomiast dostępne okno wyboru nadal przechwytuje jednoznaczny Plus zwykłą ścieżką WPF. Klawisz `Pause` otrzymał pełne mapowanie i może być wybrany tak samo jak inne obsługiwane klawisze. Log podaje, czy prefiks został zarejestrowany przez hak, czy mechanizm skrótów Windows, a błąd inicjalizacji nie pozostaje już bez śladu.

Od `alpha.181` automatyczny zapis pozycji, historii, ustawień Radia i stanu
pozostałych sesji nie wykonuje pełnej transakcji SQLite na wątku interfejsu.
Jedna kolejka zapisuje migawki w tle i łączy kilka szybkich zmian, zachowując
najnowszy stan; przy zamknięciu programu końcowa migawka jest opróżniana przed
wyjściem. Chroni to fokus, strzałki i odtwarzacz również wtedy, gdy zapis
lokalnego katalogu opóźni dysk, filtr antywirusowy albo dostawca chmury.
Wspólna klasyfikacja rozróżnia plik lokalny, plik zdalny i strumień sieciowy,
więc reguła pracy poza interfejsem obejmuje iCloud, OneDrive, Dysk Google,
Dropbox, udziały sieciowe oraz przyszłe adaptery sesji, streamingu i pobierania.
Segmentowe pole daty harmonogramu pozostaje obsługiwane strzałkami: lewo i
prawo wybiera część daty, a góra i dół zmienia jej wartość.

Od `alpha.187` globalnym prefiksem może być każdy standardowy klawisz bloku
numerycznego, także bez Control, Alt, Shift ani Windows. Obejmuje to cyfry,
Enter, Plus, Minus, Gwiazdkę, Ukośnik, kropkę, separator i Num Lock. Przy
wyłączonym Num Lock AMC rozróżnia również numeryczne Insert, Delete, Home,
End, Page Up, Page Down oraz strzałki od odpowiadających im klawiszy osobnego
bloku nawigacyjnego. Ustawienia i okno przechwytywania podają NVDA wyłącznie
użytkowe nazwy, na przykład „Plus numeryczny” albo „Insert numeryczny”.

Od `alpha.115` sesja **Radio internetowe** pod domyślnym `Ctrl+5` jest pierwszym prawdziwym adapterem sieciowym AMC. `Ctrl+F` wyszukuje stacje w publicznym katalogu Radio Browser; wynik można odtworzyć oraz dodać do lokalnej Biblioteki lub Ulubionych radia. Kolejka i „Odtwórz jako następne” nie należą do modelu radia, natomiast playlisty mogą grupować stacje. `Insert` w Bibliotece dodaje własną stację, a `F2` otwiera dwa niezależne pola: nazwę i adres strumienia. Odtwarzacz ma ograniczony pamięcią timeshift, Home przechodzi do początku dostępnego bufora, End wraca na żywo, a `R` rozpoczyna lub kończy świadome nagrywanie bieżącej stacji w niezależnym tle. `Ctrl+Alt+R` robi to samo na liście, `Alt+2` pokazuje wszystkie aktualnie nagrywane stacje, a `Shift+R` otwiera nagranie czasowe lub harmonogram. Obsługiwane są bezpośrednie adresy HTTP/HTTPS oraz listy M3U, M3U8, PLS i XSPF; manifest HLS pozostaje manifestem dla dekodera. Zakładki, skok procentowy i regulacja prędkości są w radiu ukryte, ponieważ nie mają trwałego znaczenia dla transmisji na żywo.

Od `alpha.116` nagranie radia jest zapisywane jako MP3 zamiast WAV; docelowa jakość to 192 kb/s, a dla nietypowej częstotliwości próbkowania system wybiera najbliższy obsługiwany bitrate. AMC koduje dekodowany dźwięk przez systemowy Windows Media Foundation, więc działa tak samo dla źródłowego MP3, AAC, OGG i innych strumieni obsługiwanych przez odtwarzacz, bez instalowania FFmpeg lub globalnego pakietu kodeków. Podczas nagrywania powstaje ukryty plik roboczy; nazwa `.mp3` pojawia się dopiero po prawidłowym zakończeniu. Zatrzymanie, zmiana stacji i zamknięcie programu finalizują nagranie, natomiast awaria kodera usuwa niedokończone dane i nie zatrzymuje odtwarzania radia.

Od `alpha.117` starsze serwery Shoutcast i Icecast zwracające odpowiedź `ICY 200 OK` mają bezpieczną ścieżkę zgodności MP3. AMC najpierw zachowuje zwykły dekoder systemowy, a po odrzuceniu takiego strumienia ponawia połączenie własnym ograniczonym klientem i zarządzanym dekoderem NLayer. Rozwiązanie nie jest wyjątkiem dla jednej stacji; obejmuje między innymi starsze bezpośrednie adresy anten Polskiego Radia, nie obniża kontroli certyfikatów HTTPS i usuwa metadane ICY z toru audio, jeśli serwer je mimo wszystko przesyła.

Od `alpha.118` menu **Plik** odpowiada bieżącej sesji. W **Plikach lokalnych** pokazuje otwieranie plików, folderów i Foldery Biblioteki, a w **Radiu internetowym** — **Importuj stacje z playlisty…** oraz **Dodaj stację radiową…**. `Ctrl+O` otwiera pliki w sesji lokalnej, lecz w Radiu importuje wiele stacji z lokalnego M3U/M3U8, PLS, XSPF albo eksportu VRadio JSON. Import odrzuca wpisy niebędące prawidłowymi adresami HTTP/HTTPS, duplikaty i nadmiernie duże pola, a do logu zapisuje tylko liczby — nie treść wadliwych wpisów. Szybkie przełączenie stacji anuluje poprzednią próbę połączenia. Awaryjny dekoder ICY MP3 sprawdza MIME i nie próbuje już odtwarzać AAC/AAC+ lub OGG jako fałszywego MP3.

Od `alpha.119` bezpośrednie transmisje OGG/Vorbis, w tym Radio Emaus, mają osobny sekwencyjny dekoder radiowy. Starszy strumień ICY MP3 jest wyrównywany dopiero do dwóch kolejnych zgodnych ramek, dzięki czemu przypadkowe dane ze środka pierwszej ramki nie są uznawane za początek dźwięku. Dla znanych starych adresów AAC/HLS Programu 1, Programu 2, Czwórki i PR24 oraz nieaktualnego wpisu Eski AMC najpierw próbuje zgodnego wariantu MP3; testy sieciowe obejmują Program 2, Czwórkę, PR24, Eskę i Radio Emaus. Nieznane HLS i ICY AAC bez zgodnego wariantu nadal kończą się kontrolowanym komunikatem i wymagają planowanego dodatkowego komponentu dekodera — nie są zgadywane ani otwierane ścieżką, która mogłaby zawiesić aplikację.

Od `alpha.120` dekoder starszego radia MP3 prawidłowo scala częściowe odczyty TCP. Granica pakietu sieciowego może wypaść w środku ramki MP3 i nie oznacza końca stacji. Test kontrolny wysyła ramki celowo podzielone na małe fragmenty, a testy na żywo wymagają wielu kolejnych odczytów Jedynki, Programu 2, Czwórki i PR24. Zapobiega to sytuacji, w której AMC ogłasza rozpoczęcie odbioru, po czym po kilkudziesięciu milisekundach uznaje normalną transmisję za zakończoną.

Od `alpha.121` wpis AAC+ Radia Chopin na porcie 8960 ma sprawdzony wariant MP3 192 kb/s na porcie 8910. Trójka na porcie AAC+ 8954 ma odpowiadający wariant MP3 8904, lecz 28 sierpnia 2026 oba stare wejścia nadawcy zwracały `ICY 401 Service Unavailable`; AMC próbuje oba i kończy działanie bez zawieszenia, ale nie może odtworzyć niedostępnego źródła. Uruchomienie stacji jest ogłaszane dopiero po odebraniu pierwszej dekodowanej porcji audio, a nie po samym otwarciu adresu.

Od `alpha.122` zamknięcie anulowanego strumienia podczas szybkiej zmiany stacji jest traktowane jako oczekiwane zakończenie zadania odbioru. Wyjątek powstały przez równoczesne zwalnianie bufora nie trafia już jako niezaobserwowany błąd zadania i nie może wpłynąć na następną stację ani zamykanie programu.

Od `alpha.123` chwilowe zerwanie już działającej transmisji nie kończy od razu odtwarzacza. AMC dwukrotnie próbuje ponownie otworzyć tę samą stację, za każdym razem sprawdzając także jej zapisany zgodny wariant, i uznaje powrót dopiero po odebraniu prawdziwego dźwięku. Timeshift, urządzenie wyjściowe i trwające nagranie pozostają na tym samym torze, jeżeli format PCM po powrocie jest zgodny; niekontrolowana zmiana częstotliwości, liczby kanałów lub kodowania kończy się bezpiecznie zamiast przekazać błędne próbki. Po 20 sekundach stabilnego odbioru licznik ponowień zeruje się.

Od `alpha.125` bezpośrednie problematyczne strumienie radia otrzymują osobny dekoder BASS 2.4, który dobrze obsługuje między innymi starsze ICY MP3 Polskiego Radia i OGG Radia Emaus. Dla zwykłych stacji zachowany jest szybszy dekoder systemowy, a BASS staje się jego dodatkowym fallbackiem; HLS pozostaje na dotychczasowej ścieżce. Błąd BASS lub brak jego pliku automatycznie uruchamia zarządzane ścieżki zgodności. BASS nie naprawi źródła niedostępnego po stronie serwera. AMC pozostaje darmowym, niekomercyjnym projektem bez reklam, płatnych funkcji i darowizn; kod AMC jest otwarty, natomiast oddzielny `bass.dll` zachowuje własnościową licencję Un4seen i wymaga osobnej licencji przy wykorzystaniu komercyjnym.

Od `alpha.126` `Alt+1`, `Alt+2`, `Alt+3`, `F5` i `Ctrl+F5` są ściśle lokalne. W Radiu ani w innej sesji nie mogą przełączyć programu do Plików lokalnych, a lokalne pozycje Widoku są tam ukryte. Radiowych znaczeń `Alt+1–3` na razie nie przypisano; pozostają zarezerwowane do czasu ustalenia prawdziwych widoków katalogu i kolejności stacji. Ponowny import playlisty włącza do Biblioteki znalezioną wcześniej stację katalogową zamiast odrzucać ją jako duplikat, zachowując jej pełną nazwę, kodek i bitrate. Dla transmisji HLS zawierających obraz i AAC dostępna jest izolowana ścieżka audio przez `ffmpeg` odnaleziony obok programu, w `FFMPEG_PATH` albo `PATH`; sprawdzono na żywo transmisje Jarosławia i św. Jana Vianneya. Brak opcjonalnego komponentu kończy się kontrolowanym błędem. Docelowy instalator dostarczy osobno aktualizowany, zgodny licencyjnie składnik, zamiast instalować globalny pakiet kodeków. Pasek zachowuje systemową rolę dla `NVDA+End`, a jego wewnętrzna etykieta nadal zawiera tylko jeden aktualny komunikat. Odtwarzacz zapamiętuje wykryty przez dekoder bitrate i częstotliwość; lewa strzałka może na żądanie wykonać ograniczoną próbę metadanych, lecz nie wymyśla wartości, której strumień nie ujawnia.

Ta sama wersja utrwala wielokrotne zaznaczanie wyników wyszukiwania: `Shift+strzałka w górę/dół` jawnie rozszerza albo zmniejsza ciągłe zaznaczenie, a fokus pozostaje na ruchomym końcu zakresu. `Ctrl+C` kopiuje wszystkie zaznaczone nazwy w kolejności listy. `Ctrl+Shift+C` dla plików zachowuje pełne ścieżki i `FileDrop`, natomiast dla radia i innych usług kopiuje powtarzane pary **nazwa, publiczne łącze**; zaznaczenie wielu stacji daje więc nazwę i adres każdej z nich, a nie tylko pierwszego wyniku.

Od `alpha.127` zbiorcze działanie z wyników wyszukiwania korzysta z jednej, niezmiennej migawki zaznaczenia. `Ctrl+Shift+U`, Kolejka, „Odtwarzaj jako następne” i Biblioteka obejmują dokładnie zaznaczone wyniki, a nigdy pozostawione zaznaczenie głównej listy znajdującej się pod oknem wyszukiwania. Powtórzone wiersze tej samej stacji są liczone raz według jej identyfikatora. Zaznaczenie obejmujące różne usługi jest odrzucane czytelnym komunikatem, ponieważ Ulubione, Biblioteka i Kolejka należą do konkretnych sesji. Log zapisuje tylko liczby wierszy i elementów, bez nazw oraz adresów stacji.

Od `alpha.128` BASS odczytuje bitrate przez właściwy atrybut `BASS_ATTRIB_BITRATE`, a nie atrybut częstotliwości. Wartości zapisane wcześniej jako niemożliwe `44100 kb/s` są przy uruchomieniu odrzucane i mogą zostać ponownie wykryte na żądanie; 44,1 kHz pozostaje osobną częstotliwością próbkowania. Import playlisty przy zgodnym adresie tylko włącza istniejący rekord katalogowy do Biblioteki — nie zmienia jego nazwy ani bogatszych metadanych — i mówi o zachowaniu dotychczasowych nazw. Osobne rekordy tej samej rozgłośni o różnych adresach nadal pozostają osobne, aby nie połączyć przypadkiem innych anten lub wariantów jakości.

Od `alpha.129` rzeczywisty manifest HLS jest podstawowym adresem odtwarzania, a znany starszy wariant MP3 wyłącznie awaryjnym. W szczególności sprawdzony adres Trójki `https://stream13.polskieradio.pl/pr3/pr3.sdp/playlist.m3u8` nie jest już wyprzedzany przez niestabilny port 8904. Menu główne, kontekstowe, odtwarzacza, wyników wyszukiwania i paleta poleceń ukrywają w Radiu Kolejkę, „Odtwórz jako następne”, playlisty, Albumy, Zakładki i inne nieadekwatne działania; odpowiadające skróty są też blokowane. Jednorodna lista radia nie powtarza słowa „stacja” przed systemową pozycją `8 z 48`. W Ulubionych radia `Ctrl+X` zaznacza jedną stację albo grupę do przeniesienia, a `Ctrl+V` umieszcza cały blok przed aktualnie wybraną stacją jednym odświeżeniem; `Alt+góra/dół` pozostaje do pojedynczych korekt.

Od `alpha.130` zwykłe Radio ma dwanaście lokalnych presetów AMC: `1–0`, `-` i `=`. Lista służy wyłącznie do uruchamiania: Enter i Spacja włączają zajętą pozycję i nigdy jej nie nadpisują. Tryb przypisania przyjmuje klawisz miejsca, Enter zapisuje, a Escape anuluje. Zajęte miejsce wymaga ponownego wskazania tej samej cyfry lub znaku i dopiero Entera; Delete, a następnie Enter usuwa samo przypisanie. `Ctrl+Shift+1–0/-/=` uruchamia zajęty preset bezpośrednio. Od `alpha.135` listę otwiera wyłącznie `Ctrl+Alt+P`, a przypisanie `Ctrl+Alt+Shift+P`, ponieważ `Ctrl+P` i `Ctrl+Shift+P` należą do playlist. Zapis presetu zachowuje stację w Bibliotece radia; usunięcie presetu nie usuwa jej z Biblioteki ani Ulubionych. Presety urządzeń WiiM i przyszłych adapterów pozostają odrębnymi kolekcjami źródłowymi.

Ta sama wersja porządkuje `Alt+Enter` w Radiu: nie pokazuje Kolejki ani „Odtwarzaj jako następne”, lecz dane stacji, stan w Bibliotece i Ulubionych, parametry dźwięku oraz łącza. Tekst nadal pozwala nawigować po znakach i słowach, a osobna dostępna lista łączy otwiera Enterem adres strumienia lub stronę stacji w zewnętrznej aplikacji. Lewa strzałka wykonuje ograniczoną próbę ramek MP3/AAC i manifestu HLS; deklarowane pasmo wariantu HLS jest odczytywane bez pomylenia go z bitrate obrazu. Gdy chroniony serwer nie udostępnia parametrów osobnemu zapytaniu, znany profil 357 lub Trójki daje oznaczoną słowem „około” wartość awaryjną zamiast udawać pomiar.

Od `alpha.131` lista presetów ma rozszerzone zaznaczanie. `Ctrl+C` kopiuje nazwy zajętych stacji, a `Ctrl+Shift+C` każdą nazwę wraz z adresem strumienia; puste miejsca są pomijane i żadne kopiowanie nie zmienia przypisań. Lokalne MP4, M4V, MOV, MKV, WebM, AVI, WMV, MPEG, M2TS i inne rozpoznane kontenery wideo trafiają do Biblioteki jak pozostałe multimedia i są odtwarzane wyłącznie jako dźwięk, bez otwierania obrazu. Podstawowy MP4 z AAC jest objęty próbą automatyczną; mniej typowy kodek nadal wymaga obsługi przez Windows Media Foundation. Rozmiar całego pliku wideo nie jest przedstawiany jako bitrate jego ścieżki audio. Wartość 22050 Hz zgłoszona przez dekoder jest czytana precyzyjnie jako `22,05 kHz`.

Ta wersja poprawia również stan startowy: lista sesji ustawia fokus na ostatnio używanej sesji, a ustawienie „Ostatnia używana sesja” otwiera bezpośrednio jej zapamiętany widok. `Alt+strzałka w górę/w dół` działa z fokusem na liście kolejności sesji w Ustawieniach, a jej wiersze mają jawne nazwy dostępnościowe.

Od `alpha.132` pasek stanu nie ma już dodatkowej nazwy „Pasek stanu odtwarzania”. `NVDA+End` nadal odczytuje jego aktualną treść, lecz bez powtarzania nazwy kontenera. Tytuł głównego okna Radia zaczyna się od nazwy stacji i, gdy aktywny strumień ICY/BASS rzeczywiście przekazuje metadane, dopisuje bieżący utwór lub audycję. Tekst nie jest zapisywany jako nazwa stacji, jest ograniczony i oczyszczany ze znaków sterujących. Zmiana, zatrzymanie lub błąd stacji natychmiast usuwa poprzedni tytuł, więc opóźnione dane anulowanego strumienia nie mogą pojawić się przy nowej antenie. Strumień albo użyty dekoder, który nie ujawnia takich metadanych, pozostawia samą nazwę stacji.

Od `alpha.133` miejsca presetów są zawsze nazywane numerami od 1 do 12. Lista dodatkowo podaje rzeczywisty skrót, dlatego trzy ostatnie pozycje brzmią jednoznacznie: „Preset 10, skrót Ctrl+Shift+0”, „Preset 11, skrót Ctrl+Shift+minus” i „Preset 12, skrót Ctrl+Shift+znak równości”. Uruchamianie radia przez Enter, preset oraz Page Up lub Page Down podaje zwięźle samą nazwę wybranej stacji, bez powtarzania słowa „Łączenie” albo „Odtwarzanie”. Błędy i przekroczenie czasu połączenia nadal są oznajmiane.

Od `alpha.134` natywny pasek stanu udostępnia NVDA jedną bieżącą treść jako jeden obiekt o roli paska stanu. Dzięki temu `NVDA+End` ponownie go odnajduje, ale nie powtarza tekstu ani dodatkowej nazwy kontenera. W `Ustawienia > Ogólne > Odtwarzanie` działa opcja „Po wyjściu z odtwarzacza ustaw fokus na aktualnie odtwarzanym elemencie”. Domyślnie jest włączona: po zmianie elementu przez Page Up lub Page Down i wyjściu Escape, Shift+F6 albo przyciskiem Wróć do listy fokus podąża za odtwarzaniem, o ile element należy do wyświetlanego widoku. Radio obsługuje dodatkowo uniwersalne aliasy `Ctrl+Alt+P` dla listy presetów oraz `Ctrl+Alt+Shift+P` dla utworzenia lub przypisania presetu; dotychczasowe `Ctrl+P` i `Ctrl+Shift+P` pozostają bez zmian.

Od `alpha.135` Radio internetowe korzysta ze zwykłych, trwałych playlist AMC tak samo jak pozostałe sesje. `Ctrl+P` otwiera playlisty stacji, a `Ctrl+Shift+P` zmienia przynależność jednej lub wielu stacji, również z wyników wyszukiwania i odtwarzacza. Playlistę można tworzyć, nazywać, usuwać bez usuwania stacji oraz porządkować ręcznie. Presety są od tej funkcji całkowicie oddzielone: `Ctrl+Alt+P` otwiera ich listę, a `Ctrl+Alt+Shift+P` tworzy lub przypisuje preset. Krótsze skróty nie są już aliasami presetów.

Od `alpha.136` lista presetów rozróżnia numer miejsca od fizycznego klawisza. Dziesiąta pozycja brzmi „Preset numer 10, klawisz 0”, jedenasta wskazuje klawisz minus, a dwunasta znak równości. Pasek stanu ponownie zachowuje standardowe drzewo dostępności Windows: ma rolę paska stanu i dokładnie jedno tekstowe dziecko z aktualną treścią, którego oczekuje skrypt `NVDA+End`. Kontener nie otrzymuje dodatkowej nazwy, więc treść nie powinna być dublowana.

Od `alpha.137` presety AMC działają również w sesji **Pliki lokalne**. `Ctrl+Alt+P` otwiera dwanaście miejsc bieżącej sesji na liście folderów, w płaskiej Bibliotece, Ulubionych oraz w odtwarzaczu. `Ctrl+Alt+Shift+P` przypisuje zaznaczony plik, aktualnie odtwarzany plik albo wskazany folder Biblioteki. `Ctrl+Shift+1–0/-/=` wywołuje miejsce bez przełączania sesji: plik jest od razu odtwarzany, natomiast folder otwiera się jako bieżący poziom widoku Foldery. Presety lokalne są trwałe i wchodzą do pełnej kopii ustawień; usunięcie lub odłączenie celu nie usuwa po cichu przypisania, lecz daje czytelny komunikat o niedostępności.

Od `alpha.138` na liście presetów pojedyncze klawisze `1–0`, minus i znak równości przenoszą fokus bez uruchamiania ani nadpisywania miejsca. Bezpośrednie skróty `Ctrl+Shift+1–0/-/=` są dodatkowo przechwytywane na granicy komunikatów klawiatury, aby framework albo czytnik ekranu nie zgubił klawisza `0`. Puste miejsce wywołane skrótem nazywa użyty klawisz, dlatego `Ctrl+Shift+0` mówi „Preset 0 pusty”; lista nadal jednoznacznie przedstawia to samo miejsce jako „Preset numer 10, klawisz 0”.

Od `alpha.139` presety są wspólną funkcją wszystkich sesji AMC. Każda sesja ma własne dwanaście miejsc, więc preset TIDAL, Apple Music, WiiM, Radia albo Plików lokalnych nie nadpisuje miejsca innej usługi i nigdy nie przełącza sesji po cichu. Można przypisać zwykły element, folder, lokalny album albo playlistę AMC; element odtwarzalny uruchamia się w kontekście innych zajętych presetów tej sesji, a kontener otwiera swoją zawartość. Mechanizm działa również z pojedynczego wyniku wyszukiwania. Stare presety radia są jednorazowo, bezkolizyjnie przenoszone do wspólnego magazynu. Reguły bezpiecznego aktualizowania FFmpeg, bibliotek i dodatków zapisano w [`AKTUALIZACJE_KOMPONENTOW.md`](AKTUALIZACJE_KOMPONENTOW.md).

## Najprostsze uruchomienie gotowej wersji

1. Otwórz folder `publish`.
2. Otwórz folder `AccessibleMediaController-<wersja>` z najwyższym numerem wersji.
3. Uruchom znajdujący się w nim plik `AccessibleMediaController-<wersja>.exe`.

Publikowany program jest samowystarczalny dla środowiska .NET i podstawowego odtwarzania. Dwóch bibliotek SoundTouch oraz pliku BASS znajdujących się obok EXE nie należy przenosić ani usuwać: pozostają osobnymi, wymiennymi składnikami zgodnie z ich licencjami. Transmisja HLS z obrazem wymaga w obecnej wersji testowej dostępnego `ffmpeg`; zwykłe radio i pliki lokalne go nie wymagają. Użytkownik testujący gotową wersję nie musi instalować SDK ani budować projektu.

## Wymagania do zbudowania

- Windows 10 lub Windows 11;
- .NET 8 SDK zainstalowany przez osobę budującą projekt;
- PowerShell 5.1 lub nowszy;
- do testów dostępności: NVDA, JAWS albo Narrator.

Projekt jest budowany na Windows i sprawdzany automatycznymi testami rdzenia. Do zwykłego testowania służy gotowy plik EXE z folderu `publish`.

## Budowanie i uruchamianie

Poniższe polecenia są przeznaczone dla osób rozwijających projekt lub przygotowujących nową publikację.

W PowerShellu, w katalogu projektu:

```powershell
dotnet build AccessibleMediaController.sln
dotnet run --project src/AccessibleMediaController.Windows
```

Testy rdzenia bez dodatkowych bibliotek:

```powershell
dotnet run --project tests/AccessibleMediaController.Core.SmokeTests
```

Po każdej większej zmianie wykonaj także testy ręczne opisane w
[`TESTY_ZADANIA_PL.md`](TESTY_ZADANIA_PL.md). Ten stały plik jest aktualizowany dla bieżącej wersji i zawiera ponumerowane zadania z tytułami. Wyniki wpisuj do nowego, niepowtarzalnego pliku w folderze `wyniki-testow`; jego nazwa zawiera datę, godzinę oraz wersję programu.

Opcjonalny, bezpieczny klient smoke testu NVDA znajduje się w
[`tests/accessibility/nvda`](tests/accessibility/nvda). Nie instaluje dodatku i akceptuje wyłącznie utwardzony, lokalny profil mostu tylko do odczytu. Nie zastępuje testów ręcznych ani UI Automation.

Samowystarczalna wersja dla Windows x64, zawierająca środowisko .NET:

```powershell
.\build.ps1 -Publish
```

Wynik znajdzie się w folderze `publish\AccessibleMediaController-<wersja>`. Folder zawiera EXE, dwie wymienne biblioteki SoundTouch oraz informacje licencyjne.

## Domyślne działanie prototypu

Domyślny globalny prefiks to `Ctrl+Alt+Windows+F12`. Zastąpił wcześniejsze kombinacje kolidujące z NVDA albo systemowym skrótem Narratora. Warstwa globalna pozostaje eksperymentalna, a w `alpha.13` otrzymała uzgodnioną mapę. Po prefiksie:

- `1` — Pliki lokalne, gdy sesja jest dostępna;
- `2` — WiiM;
- `3` — TIDAL;
- `4` — Apple Music;
- `5–9` — następne przypisane sesje, jeśli istnieją;
- `0` — lista sesji;
- `Page Up` i `Page Down` — poprzednia i następna sesja;
- strzałki w lewo i w prawo — 10 sekund wstecz lub naprzód;
- strzałki w górę i w dół — głośność o 5%;
- `Ctrl+E`, `Ctrl+R`, `Ctrl+T` — odpowiednio czas upłynięty, pozostały i całkowity;
- `U` i `Shift+U` — Ulubione i zmiana stanu Ulubionych;
- `L` i `Shift+L` — Biblioteka i zmiana przynależności;
- `P` i `Shift+P` — Playlisty i zmiana przynależności;
- `Q` i `Shift+Q` — Kolejka i dodanie albo usunięcie elementu;
- `A` — Albumy; `Shift+A` po prefiksie pozostaje wolne (bez prefiksu wybiera urządzenie audio sesji);
- `K` — filtr bieżącej listy, `Shift+K` — paleta poleceń;
- `F` — wyszukiwanie w bieżącej usłudze, `Shift+F` — wyszukiwanie globalne;
- `D` — pobieranie wewnątrz usługi, `Shift+D` — eksperymentalne pobieranie na dysk.

Gdy okno AMC jest aktywne, `Ctrl+1–9` przełącza sesję bez globalnego prefiksu, `Ctrl+Shift+S` otwiera listę sesji, `Ctrl+0` pozostaje jej aliasem, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję. Lokalne skróty widoków to `Ctrl+U` — Ulubione, `Ctrl+P` — Playlisty, `Ctrl+L` — Biblioteka, `Ctrl+Q` — Kolejka oraz `Ctrl+Shift+A` — Albumy. Wszystkie strzałki na zwykłej liście zachowują działanie właściwe dla listy. Enter na utworze lub stacji uruchamia element i przechodzi do odtwarzacza; `Ctrl+Enter` przełącza odtwarzanie zaznaczenia bez opuszczania listy. `F6`, polecenie „Teraz odtwarzane” albo `N` po prefiksie otwiera odtwarzacz bez zmiany zaznaczenia.

W odtwarzaczu lewo/prawo przewija o 10 sekund, Shift+lewo/prawo o 30 sekund, Ctrl+lewo/prawo o minutę, góra/dół zmienia głośność o 5%, a Shift+góra/dół o 1%. `Shift+,` zmniejsza prędkość, `Shift+.` ją zwiększa, a `Ctrl+.` przywraca 1,00×. Dostępne wartości to 0,50–2,00× co 0,25; tempo zmienia się bez zmiany wysokości dźwięku. Home przechodzi na początek, End — 10 sekund przed końcem, a cyfry `0–9` przechodzą do `0–90%` czasu trwania co 10%. `Ctrl+J` otwiera „Skocz do czasu”: sama liczba oznacza minuty, `minuty:sekundy` podaje dokładniejszą pozycję, a trzy części oznaczają `godziny:minuty:sekundy`. `Ctrl+Shift+J` otwiera osobne „Skocz do procentu” i przyjmuje `0–100`. Oba skróty oraz cyfry działają wyłącznie w odtwarzaczu. `Ctrl+Shift+E/R/T` podaje czas, a Escape wraca dokładnie do wcześniejszej listy i pozycji. Odtwarzany element ma na liście początek „Odtwarzany”, a zatrzymany w połowie — „Wstrzymany”. Skróty widoków działają również wtedy, gdy fokus znajduje się w filtrze. `Ctrl+K` przechodzi do filtra już załadowanej listy. `Ctrl+F` otwiera okno nazwane krótko „Szukaj w TIDAL” lub odpowiednio dla bieżącej usługi, a `Ctrl+Shift+F` — „Szukaj we wszystkich usługach”. W polu wyszukiwania Enter wykonuje zapytanie, a kolejny Enter otwiera wybrany wynik bez automatycznego uruchamiania jedynego dopasowania. Na wyniku działają także `Ctrl+Enter` — odtwórz lub wstrzymaj zaznaczenie, `Shift+Enter` — kolejka, `Ctrl+Shift+Enter` — odtwórz jako następne, `Ctrl+Shift+U` — Ulubione i `Alt+Enter` — informacje. Te działania bezpośrednie pozostawiają okno wyników otwarte i fokus na wyniku; ich komunikat zawsze kończy się nazwą usługi. Po naciśnięciu zwykłego Enter wynik zostaje otwarty, a fokus przechodzi do głównej listy. Dostępnościowa nazwa tego jednego elementu zaczyna się chwilowo od nazwy usługi, dlatego NVDA czyta usługę i element w jednej nieprzerwanej wypowiedzi. To samo dzieje się po zamknięciu wyszukiwania globalnego Escape, jeśli wcześniej wykonano działanie bezpośrednie — również wtedy, gdy wybrana usługa była już aktywna. Po przejściu na inny element dodatkowy początek znika. Lista sama odczytuje wynik oraz jego pozycję, bez prefiksu „Wyniki wyszukiwania” i bez dodatkowego komunikatu na żywo o liczbie wyników. Escape zamyka okno. `Ctrl+Shift+K` otwiera dostępną paletę wszystkich poleceń AMC. Wpisywanie od razu filtruje listę, strzałka w dół przechodzi do wyników, Enter wykonuje zaznaczone polecenie, a Escape zamyka paletę. Nazwy można wpisywać bez polskich znaków, a wiersz podaje skrót działający w oknie oraz aktywny skrót działający po prefiksie.

W sesji **Pliki lokalne** `Ctrl+O` albo menu **Plik → Otwórz pliki audio** otwiera jeden lub wiele lokalnych plików. `Ctrl+Shift+O` albo **Plik → Otwórz folder z plikami audio** rejestruje trwałe źródło, wczytuje rozpoznane pliki również z podfolderów i otwiera standardowy widok **Foldery**. Enter wchodzi do zaznaczonego folderu albo otwiera plik, Backspace wraca o poziom wyżej, wpisywanie liter działa w bieżącym poziomie, `Ctrl+K` go filtruje, a `Ctrl+F` nadal przeszukuje całą lokalną sesję. Płaska **Biblioteka** pozostaje dostępna równolegle. Żadne z tych poleceń nie uruchamia dźwięku automatycznie, a ponowne wczytanie tej samej ścieżki nie tworzy duplikatu.

Od `alpha.77` kolejność sesji można zmieniać w **Ustawienia → Ogólne** przyciskami albo `Alt+strzałka w górę/w dół`. Pozycja określa jednocześnie `Ctrl+1–9`, listę sesji oraz kolejność `Ctrl+Page Up/Page Down`. Od `alpha.115` domyślnie jest to: Pliki lokalne, WiiM, TIDAL, Apple Music, Radio internetowe. Dawna karta **Listy i odczyt** została połączona z kartą **Komunikaty** jako sekcja **Odczytywanie elementów list**; wszystkie ustawienia i wejścia z palety poleceń pozostały dostępne.

Od `alpha.78` sesja **Pliki lokalne** istnieje od uruchomienia także wtedy, gdy biblioteka jest pusta, dlatego `Ctrl+1` nigdy nie prowadzi już do „nieprzypisanej” sesji. Po wybraniu katalogu przez `Ctrl+Shift+O` AMC natychmiast przechodzi do lokalnego widoku Foldery i pozostaje w nim podczas skanowania, zamiast pokazywać demonstracyjną listę innej usługi.

Od `alpha.79` zarejestrowany folder jest jednoznacznie **źródłem Biblioteki**. `Ctrl+Shift+O` włącza do płaskiej Biblioteki wszystkie rozpoznane pliki z wybranego folderu i podfolderów, także rekordy wcześniej z niej usunięte; widok **Foldery** jest tylko hierarchicznym sposobem oglądania tych samych rekordów. `Delete` na pliku w Folderach usuwa jego przynależność do Biblioteki, lecz pozostawia plik na dysku i w widoku jego rzeczywistego folderu. `Delete` na wierszu folderu niczego nie usuwa, a fizyczne przeniesienie pliku do Kosza nadal wymaga `Shift+Delete` i potwierdzenia.

Od `alpha.80` źródła są synchronizowane przy starcie, po zmianach zauważonych w systemie plików oraz ręcznie przez `F5`. Nowe pliki są dopisywane, brakujące stają się niedostępne bez utraty historii, zakładek i pozycji, a plik przywrócony pod tą samą ścieżką wraca do aktywnej Biblioteki. `Delete` tworzy trwałe wykluczenie, dlatego zwykłe ponowne skanowanie ani restart nie dodają pliku z powrotem; natychmiastowe `Ctrl+Z` cofa wykluczenie. `Alt+1` otwiera **Foldery Biblioteki**, `Alt+2` — płaskie **Wszystkie pliki**, a `Ctrl+L` wraca do ostatnio używanego układu Biblioteki. `Shift+cyfry` pozostaje dostępne dla nawigacji po nazwach zaczynających się od znaków specjalnych.

Od `alpha.81` skaner rozróżnia prawdziwe dowiązania katalogów od plików i folderów-placeholderów Cloud Files. Dzięki temu źródła iCloud są indeksowane bez otwierania i pobierania treści każdego nagrania, a dowiązania oraz junctiony nadal nie mogą utworzyć pętli. Jednorazowa migracja naprawia źródło, którego wszystkie starsze rekordy zostały omyłkowo wykluczone przez alfę 80. Przy przejściu z `Alt+2` do `Alt+1` AMC otwiera teraz rzeczywisty folder zaznaczonego pliku i zachowuje jego zaznaczenie; dla pliku dodanego pojedynczo, poza źródłami, wybierany jest główny poziom Folderów.

Od `alpha.82` awaryjne rozpoznawanie znaczników Cloud Files obejmuje także OneDrive, gdy dostawca odmawia odczytu celu punktu ponownej analizy. Google Drive w trybie lustrzanym działa jak zwykły folder, a w trybie strumieniowanym jak źródło na wirtualnym dysku: AMC indeksuje nazwy bez otwierania treści, nie wymusza masowego pobierania i zachowuje rekordy, gdy aplikacja Google Drive albo jej dysk są chwilowo niedostępne. Obserwator zmian jest dodatkiem; na systemie plików, który go nie obsługuje, pozostają skan przy starcie i `F5`.

Od `alpha.83` polecenie **Plik → Foldery Biblioteki** oraz paleta poleceń otwierają dostępne okno folderów. Lista podaje stan folderu, liczbę aktywnych, niedostępnych i wykluczonych plików oraz pełną ścieżkę. Można dodać folder, odświeżyć jeden albo wszystkie foldery, bezpiecznie odłączyć folder i wyeksportować pełną kopię AMC. Odłączenie wyłącza tylko dalszą automatyczną synchronizację: nie usuwa plików z dysku ani rekordów, Ulubionych, kolejki, historii, zakładek i pozycji. Nowy Folder Biblioteki nie może powtarzać ani obejmować innego Folderu Biblioteki i nie może być jego podfolderem. Pełna kopia `.amcbackup.json` obejmuje katalog Biblioteki, foldery, wykluczenia i wszystkie wymienione dane użytkownika; nadal nie zawiera haseł ani tokenów.

Zwykły Enter wykonuje działanie podstawowe: na utworze lub stacji zapewnia odtwarzanie zaznaczenia i otwiera odtwarzacz, a na albumie, playliście lub wykonawcy otwiera zawartość. Jeżeli bieżący element już gra, Enter tylko pokazuje odtwarzacz i go nie wstrzymuje. `Ctrl+Enter` wykonuje polecenie **Odtwórz lub wstrzymaj** bez otwierania elementu; nowy element zaczyna grać, bieżący zostaje wstrzymany, a wstrzymany — wznowiony. W wynikach wyszukiwania okno pozostaje otwarte. `Spacja` steruje wyłącznie tym, co faktycznie jest odtwarzane, niezależnie od bieżącego zaznaczenia. Sesja ma jeden tor odtwarzania, więc nowy utwór zastępuje poprzedni; dźwięki nie nakładają się. Tytuł głównego okna zaczyna się od aktualnie odtwarzanego lub wstrzymanego elementu, potem podaje moduł, sesję oraz pełny numer wersji AMC, np. „Audycja — Kolejka — Pliki lokalne — AMC…”. Na domyślnej liście zbędny moduł „Multimedia” jest pomijany. Nie jest to nazwa elementu tylko zaznaczonego kursorem. Samo otwarcie wyniku wyszukiwania bez uruchamiania odtwarzania nie zmienia tytułu bieżącego utworu. Tytuł okna wyszukiwania opisuje jego zakres, np. „Szukaj w TIDAL — AMC”. Widoki jednorodne nie powtarzają rodzaju zasobu: Albumy nie mówią „album”, a Playlisty — „playlista”. Biblioteka i Ulubione zachowują rodzaj, ponieważ mogą mieszać zasoby. Ulubione należą do bieżącej usługi lub lokalnej biblioteki, a Kolejka do aktywnej sesji odtwarzania; dlatego ich zwykłe wiersze nie powtarzają nazwy usługi. Od `alpha.26` przejście do widoku nie uruchamia osobnego komunikatu obszaru „Stan programu”. Pierwszy element otrzymuje jednorazowy prefiks widoku, np. „Albumy, Dziwne”, a pusta lista — nazwę „Ulubione, lista pusta”. Dzięki temu NVDA ma do odczytania jedno zdarzenie fokusu zamiast konkurujących komunikatów podsumowania i listy.

Od `alpha.27` ta sama zasada obejmuje zmianę sesji wykonywaną z listy przez `Ctrl+1–9` oraz `Ctrl+Page Up/Page Down`. Numer sesji i usługa są jednorazowym początkiem nazwy zaznaczonego elementu, np. „2, Apple Music, Zielony horyzont”, zamiast osobnego komunikatu „Stan programu”. Czas występujący w tej etykiecie jest czasem zaznaczonego utworu, albumu albo playlisty. Nie jest podsumowaniem łącznego czasu bieżącej listy.

Od `alpha.28` wykonane zapytania trafiają do trwałej historii: osobnej dla każdej usługi i osobnej dla wyszukiwania globalnego. Każdy zakres przechowuje do 20 unikatowych pozycji, z najnowszą na początku; ponowne użycie zapytania przenosi je na początek zamiast tworzyć duplikat. Przy pustym polu wyszukiwania strzałka w dół wybiera najnowszy wpis, kolejne naciśnięcia przechodzą do starszych, a strzałka w górę wraca do nowszych i następnie do pustego pola. Zapytanie zostaje zapisane także wtedy, gdy nie daje wyników.

Od `alpha.29` paleta poleceń pod `Ctrl+Shift+K` jest działającym interfejsem, a nie tylko zarezerwowanym skrótem. Obejmuje również polecenia bez własnego skrótu, filtruje po wielu fragmentach nazwy i skrótu oraz ignoruje wielkość liter i polskie znaki diakrytyczne.

Od `alpha.30` polecenie „Odtwórz lub wstrzymaj” ma tę samą przełączającą logikę pod `Ctrl+Enter`, na przycisku i w menu kontekstowym. `Spacja` niezależnie steruje aktualnym odtwarzaniem. Paleta pokazuje zarówno skróty okna, jak i skróty po prefiksie, a błędne dopisanie znaków nie pozostawia użytkownika w pustej liście bez możliwości szybkiego rozpoczęcia nowego wyszukiwania.

Od `alpha.31` paleta obejmuje wszystkie działające miejsca i operacje okna Ustawienia. Wpisy dotyczące profilu, prefiksu, kolejności list, importu, eksportu, szablonów i planowanych aktualizacji tylko otwierają właściwą kartę oraz ustawiają fokus na odpowiedniej kontrolce. Bezpośrednio przełączać można wyłącznie dwie bezpieczne opcje: komunikaty dostępności i szczegółowe podpowiedzi klawiatury. Ich nazwy zawsze podają aktualny stan oraz działanie Entera, a krótki komunikat potwierdza zmianę nawet wtedy, gdy właśnie wyłączono zwykłe komunikaty.

Od `alpha.32` lista zdarzeń na karcie **Komunikaty** podaje wyłącznie zrozumiałe nazwy, np. „Zmiana sesji”. Techniczne znaczniki, takie jak `{slot}` i `{service}`, pozostają dostępne dopiero w osobnym polu edycji szablonu, gdzie są potrzebne do jego modyfikowania.

Od `alpha.33` lokalna sesja korzysta z rzeczywistego wyjścia dźwięku Windows. Enter i `Ctrl+Enter` uruchamiają lub wstrzymują wybrany plik, a Spacja, przewijanie, głośność i polecenia czasu sterują tym samym torem. `Alpha.34` wprowadziła lokalne przypisania czasu, lecz test NVDA wykazał, że nie docierały niezawodnie do aplikacji. W `alpha.35` `Ctrl+E`, `Ctrl+R` i `Ctrl+T` zostały przechwycone na granicy komunikatów okna. W `alpha.36` eksperymentalny transport strzałkami przeniesiono z głównej listy do odtwarzacza, aby wszystkie strzałki na zwykłych listach zachowały naturalne działanie. Implementacja używa kodeków dostępnych w Windows i wyjścia współdzielonego, nie instaluje globalnych pakietów kodeków i nie wycisza czytnika ekranu. Lista lokalna jest na razie przechowywana tylko do zamknięcia programu.

W `alpha.37` Escape z odtwarzacza nadal wraca do miejsca ostatnio przeglądanego, nie do utworu bieżącego. Stan bieżącego utworu jest widoczny i dostępny bez zmieniania zaznaczenia. Skróty czasu w aktywnym oknie przeniesiono na `Ctrl+Shift+E/R/T`; przypisania `Ctrl+E/R/T` po globalnym prefiksie pozostają bez zmian.

W `alpha.38` karta **Komunikaty** zawiera opcję **Oznajmiaj pozycję po przewijaniu**. Jej wyłączenie ucisza automatyczny odczyt czasu po strzałkach oraz Home i End, ale nie wyłącza celowych pytań `Ctrl+Shift+E/R/T`. `Ctrl+Shift+G` przełącza tę opcję w każdej chwili, a paleta poleceń pokazuje jej aktualny stan i skutek Entera. Ustawienie jest zapisywane po zamknięciu programu.

W `alpha.39` cyfry `0–9` w odtwarzaczu przechodzą odpowiednio do `0%, 10%, …, 90%` długości utworu. Działają także klawisze bloku numerycznego przy włączonym Num Lock. Na zwykłej liście cyfry zachowują nawigację po elementach, a `Ctrl+cyfra` nadal wybiera sesję. Skoki procentowe respektują ustawienie odczytu pozycji; przy nieznanym czasie trwania program zgłasza niedostępność zamiast zgadywać.

W `alpha.40` skok cyfrą domyślnie oznajmia tylko procent, np. „50%”; dokładny czas pozostaje dostępny przez `Ctrl+Shift+E`. W Ustawieniach na karcie **Komunikaty** można wybrać: **Tylko procent**, **Tylko czas** albo **Procent i czas**. Dotychczasowy przełącznik i `Ctrl+Shift+G` obejmują teraz automatyczne komunikaty czasu oraz głośności. Ich wyłączenie nie ucisza odtwarzania, pauzy, błędów ani jawnych poleceń czasu.

W `alpha.41` `Ctrl+Shift+G` jest nadrzędnym, odwracalnym wyciszeniem automatycznych komunikatów odtwarzacza. Nie niszczy indywidualnego wyboru czterech kategorii: skoków cyframi, przewijania strzałkami, głośności oraz odtwarzania/pauzy. Można więc pozostawić same procenty, a wyłączyć pozostałe trzy grupy. Błędy i jawne pytania o czas pozostają słyszalne. Prawdziwy pasek stanu na dole okna udostępnia przez `NVDA+End` usługę, stan, tytuł, pozycję i czas całkowity, głośność oraz przepływność. Dla lokalnego pliku przepływność jest przybliżeniem z rozmiaru i czasu; dla źródła bez danych pojawia się „brak danych”. Pasek aktualizuje się bez automatycznego przerywania mowy.

W `alpha.42` pasek został przeniesiony na rzeczywistą dolną krawędź okna, ponieważ `NVDA+End` lokalizuje pasek stanu właśnie w tym miejscu. Dynamiczny tekst znajduje się w bezpośrednim elemencie paska UI Automation. Wersja dodaje również `Ctrl+G` — „Skocz do czasu” — oraz osobne „Skocz do procentu”. Oba polecenia są dostępne z menu Odtwarzanie, przycisków odtwarzacza i palety poleceń.

W `alpha.43` pasek WPF, którego NVDA nadal nie odnajdywał w ręcznym teście, został zastąpiony natywną kontrolką paska stanu Windows. Dokładny skok do czasu przez `Ctrl+G` oraz skok do procentu z menu lub palety działają w całym głównym oknie i dotyczą aktualnie odtwarzanego elementu. Cyfry `0–9` pozostają wyłącznie skrótem odtwarzacza, aby na zwykłej liście nie przejmować nawigacji po numerach. Błędna wartość czasu lub procentu jest teraz zgłaszana aktywnym zdarzeniem dostępności, a fokus pozostaje w zaznaczonym polu.

W `alpha.44` dodano punkt zgodności z algorytmem `NVDA+End`. NVDA nie przeszukuje całego drzewa kontrolek, lecz sprawdza obiekt w skrajnym lewym dolnym punkcie okna. WPF pozostawiał tam ramkę zamiast paska. Warstwa Windows utrzymuje teraz w tym miejscu prawie niewidoczny natywny obiekt klasy `msctls_statusbar32`, aktualizowany tą samą treścią co widoczny pasek. Punkt nie przyjmuje fokusu, nie trafia do Tab ani Alt+Tab i podąża za przesunięciem, zmianą rozmiaru oraz maksymalizacją okna.

W `alpha.45` `Ctrl+J` oznacza „Skocz do czasu”, a `Ctrl+Shift+J` — „Skocz do procentu”. Oba skróty i cyfry `0–9` działają wyłącznie w odtwarzaczu. `F6` otwiera wspólny widok odtwarzacza w każdej sesji: lokalnej, streamingowej, radiowej i urządzenia. Źródło bez znanej długości nadal może korzystać z odtwarzania i innych dostępnych funkcji, lecz nie z przewijania do czasu lub procentu.

W `alpha.46` wycofano niemal niewidoczny pomocniczy pasek z `alpha.44–45`. Ręczny test wykazał, że jako osobne okno potrafił przejąć kontekst NVDA przy uruchomieniu, zablokować odczyt i skróty, a zniknąć dopiero po `Alt+F4`. W bieżącej wersji nie istnieje drugie okno paska ani obiekt mogący otrzymać fokus. Główne okno udostępnia automatyzacji granice własnego obszaru klienta, aby próba `NVDA+End` trafiała do rzeczywistego paska osadzonego na dole. Niezależnie od wyniku tego eksperymentu stan można zawsze bezpiecznie odczytać poleceniem **Odtwarzanie → Odczytaj stan odtwarzania** lub z palety poleceń; nie zmienia ono fokusu.

W `alpha.47` pasek udostępnia pełny komunikat tylko na swojej wewnętrznej etykiecie. Sam kontener nie powtarza już tej samej nazwy, dlatego `NVDA+End` powinien odczytać treść jeden raz. Kolejność została skrócona pod kątem szybkiego odsłuchu: przepływność, stan, pozycja i czas całkowity, głośność, tytuł, usługa.

W `alpha.48` pasek nie podaje głośności; jego kolejność to przepływność, stan, pozycja i czas całkowity, tytuł oraz usługa. Głośność pozostaje w celowo wywołanym poleceniu „Odczytaj stan odtwarzania”. Pomocnicze komunikaty wysyłają jedno zdarzenie UI Automation z właściwą treścią. Nie wysyłają równolegle zdarzenia regionu aktywnego, które w niektórych przejściach powodowało sporadyczny odczyt technicznej nazwy „Stan programu”.

W `alpha.49` lokalne odtwarzanie korzysta z NAudio, współdzielonego WASAPI i SoundTouch. `Shift+,` oraz `Shift+.` zmieniają tempo bez zmiany wysokości dźwięku, a `Ctrl+.` przywraca normalną prędkość. Wartość należy do lokalnej sesji i pozostaje aktywna przy zmianie utworu. Usługi demonstracyjne bez własnego toru audio jednoznacznie zgłaszają brak obsługi. Wyjście pozostaje współdzielone, aby nie odbierać dźwięku NVDA. SoundTouch jest publikowany jako dwie wymienne biblioteki obok EXE; informacje oraz pełne teksty licencji znajdują się w tym samym folderze.

W `alpha.50` pasek stanu rozpoczyna się bezpośrednio od parametrów, np. „około 192 kb/s, 48 kHz”, bez zbędnego słowa „przepływność”. `Ctrl+I` otwiera informacje o elemencie, a `Ctrl+Shift+I` odczytuje pełny stan odtwarzania. Po prefiksie odpowiadają im `I` oraz `Shift+I`. Rozszerzone informacje techniczne pozostają w menu i palecie bez stałego skrótu.

W `alpha.51` bieżący pasek nie mówi „około” i pomija parametry, których źródło nie podało. Stare polecenia `Ctrl+I`, `Ctrl+Shift+I`, prefiksowe `I` i `Shift+I` zostały usunięte. Jedno `Alt+Enter` otwiera dostępne okno **Właściwości i informacje**; jego tekst można zaznaczać i kopiować. Każda sesja pamięta osobno widok, filtr, zaznaczenie i aktywny odtwarzacz. Filtr i wyszukiwanie nie otwierają się na powierzchni odtwarzacza, natomiast skróty Ulubionych, Biblioteki, kolejki i playlist działają na faktycznie odtwarzanym elemencie. Escape wraca do listy, z której ostatnio otwarto odtwarzacz przez F6. Plan importu i eksportu radia obejmuje M3U/M3U8, PLS, XSPF oraz format Ulubionych VRadio; prywatne pliki źródłowe nie trafiają do repozytorium.

W `alpha.52` widok Kolejki jednoznacznie traktuje zarówno zwykły wpis kolejki, jak i „Odtwórz jako następne”: ponowne `Shift+Enter`, usunięcie albo odpowiednie menu usuwa oba znaczniki zamiast przypadkowo dodawać element ponownie. `Shift+strzałka w górę/dół` rozszerza zaznaczenie listy, a działania Ulubionych, Biblioteki, Kolejki, „Odtwórz jako następne”, playlist i Delete obejmują cały wybór; jedno `Ctrl+Z` cofa całą operację zbiorową. Po naturalnym końcu pliku odtwarzanie wybiera najpierw „Odtwórz jako następne”, następnie Kolejkę, a potem kolejny element załadowanej listy, bez zapętlania ostatniego. Głośność jest regulowana programowo wyłącznie w strumieniu AMC i nie powinna zmieniać głośności NVDA ani systemu. Po zmianie sesji komunikat fokusu zawiera także przywrócony widok, np. „1, TIDAL, Biblioteka…”.

W `alpha.53` okno `Alt+Enter` używa zwykłej dostępnej listy zamiast pola tylko do odczytu. Strzałki przechodzą po osobnych wierszach, pierwszy wiersz zaczyna się bezpośrednio od „Tytuł”, `Ctrl+C` kopiuje zaznaczone wiersze, `Ctrl+A` zaznacza wszystkie, a przycisk nadal kopiuje całość z podziałem na sekcje. `Ctrl+Shift+C` kopiuje pełną ścieżkę pliku lokalnego; w adapterze usługi kopiuje wyłącznie kanoniczne publiczne łącze, nigdy prywatny lub tymczasowo podpisany adres odtwarzania. `Alt+strzałka w lewo/prawo` działa w całym głównym widoku, nie tylko przy fokusie na liście, i oznajmia kierunek oraz docelowy widok. Historia pozostaje na razie funkcją eksperymentalną do ponownego testu.

W `alpha.54` okno `Alt+Enter` jest natywnym tekstem Windows tylko do odczytu. Zwykłe strzałki przesuwają kursor po znakach i wierszach, `Ctrl+strzałki` po słowach, a warianty z `Shift` zaznaczają dowolny fragment; działają standardowe `Ctrl+A` i `Ctrl+C`. „Kopiuj wszystko” oznajmia powodzenie i pozostawia dialog otwarty, a Escape lub przycisk „Zamknij” kończy pracę. Pełna lokalna ścieżka znajduje się bezpośrednio po nazwie usługi. Menu kontekstowe listy i odtwarzacza udostępniają te same adekwatne działania na elemencie: kolejkę, „Odtwórz jako następne”, Ulubione, Bibliotekę, playlisty, informacje oraz kopiowanie nazwy i ścieżki lub publicznego łącza. Nazwy działań przełączanych pokazują bieżący stan.

W `alpha.55` właściwości mają trzy stałe części: identyfikację elementu i źródła, stan **W aplikacji**, a następnie dane **Techniczne**. Czas został przeniesiony do danych technicznych obok formatu, rozmiaru, bitrate i częstotliwości. W menu kontekstowych nazwa dostępnościowa każdej pozycji zawiera skrót, dlatego NVDA powinien podawać go zarówno podczas poruszania się po menu, jak i przy ponownym odczycie aktualnego fokusu, bez zdublowania. Historia `Alt+lewo/prawo` jest świadomie prowadzona osobno w każdej sesji i w komunikacie podaje nazwę bieżącej usługi; nie służy do zmiany sesji.

W `alpha.56` komunikat historii ma kolejność: kierunek, docelowy widok, sesja — na przykład „Wstecz, Kolejka, Lokalne multimedia”. W Ustawieniach, na karcie Komunikaty, można wyłączyć samo oznajmianie kierunku historii bez wyłączania `Alt+lewo/prawo`; docelowy widok i sesja nadal poprzedzają element, np. „Kolejka, Lokalne multimedia, Audycja”. Opcja jest również dostępna z palety poleceń. Nadrzędne wyłączenie wszystkich komunikatów dostępności usuwa także ten kontekst. Ustawienie komunikatów odtwarzacza pozostaje niezależne, ponieważ dotyczy czasu, przewijania, głośności i odtwarzania, a nie nawigacji po widokach. Pełny odczyt obiektu odtwarzacza przez NVDA nadal zawiera tytuł, rodzaj, usługę, stan, prędkość, przycisk oraz krótką instrukcję — jest to zamierzony przegląd powierzchni odtwarzacza.

W `alpha.57` lokalna biblioteka staje się trwała. AMC zapisuje w `%AppData%\AccessibleMediaController\state.json` listę plików, ich stany Ulubionych, Biblioteki i Kolejki, bieżący plik, głośność, prędkość oraz osobną pozycję wznowienia każdego pliku. Pozycja jest utrwalana co najwyżej raz na 15 sekund i przy prawidłowym zamknięciu; wznowienie nie uruchamia dźwięku samoczynnie. Rozmiar i data modyfikacji chronią przed zastosowaniem starej pozycji do podmienionego pliku. Pakiet programu pozostaje na razie przenośny, ale aktualizacja lub podmiana jego folderu nie usuwa danych użytkownika z AppData. Pierwsze uruchomienie `alpha.57` wymaga jednorazowego ponownego otwarcia plików załadowanych w starszej wersji, która ich jeszcze nie zapisywała.

W `alpha.58` AMC działa jako jedna instancja: ponowne uruchomienie przywołuje aktualnie otwarte okno albo najgłębszy dialog zamiast tworzyć drugi proces zapisujący ten sam stan. Okna wyszukiwania, ustawień, właściwości i palety pozostają modalnymi dialogami bez osobnych przycisków na pasku zadań, ponieważ zapewniają przewidywalną granicę fokusu NVDA; odtwarzacz jest widokiem głównego okna, a nie kolejnym oknem. Zasobnik systemowy jest planowany wyłącznie jako opcja domyślnie wyłączona: minimalizacja może wtedy ukrywać okno, lecz `Alt+F4` nadal zakończy aplikację. Tytuł okna zaczyna się od aktualnego elementu, a następnie podaje moduł i sesję. `Page Up` oraz `Page Down` w odtwarzaczu uruchamiają poprzedni lub następny element bez zapętlania krańców listy. OGG/Vorbis korzysta z dekodera NAudio.Vorbis/NVorbis, a lokalna pozycja jest dodatkowo zapisywana przy pauzie, zmianie utworu, wpisanym skoku i utracie aktywności okna.

W `alpha.59` sesja nazywa się zwięźle **Pliki lokalne**, a ogólny moduł „Multimedia” jest pomijany w tytule i dodatkowym kontekście, gdy nie wnosi informacji. Na głównej lokalnej liście `Strzałka w lewo` podaje krótki zestaw już zapisanych danych technicznych bez otwierania okna, a `Strzałka w prawo` otwiera menu działań zawierające także „Otwórz w domyślnej aplikacji” i systemowe „Otwórz w…”. `Ctrl+Shift+C` umieszcza w schowku jednocześnie tekst pełnej ścieżki oraz standardową listę plików Windows; dzięki temu ten sam skrót wkleja ścieżkę do edytora albo fizyczny plik do Eksploratora i Total Commandera. Zaznaczenie wielu plików tworzy wieloelementową listę. `Delete` na głównej lokalnej liście usuwa wpisy wyłącznie z AMC, nigdy z dysku, a `Ctrl+Z` je przywraca. Usunięcie całej zawartości bezpiecznie odłącza pustą sesję; dodanie nowych plików albo cofnięcie tworzy ją ponownie.

W `alpha.60` lewa i prawa strzałka udostępniają szybkie informacje oraz menu działań we wszystkich widokach **Plików lokalnych**, nie tylko w domyślnym katalogu. `Ctrl+Shift+E/R/T` działa zarówno na liście, jak i w odtwarzaczu. Każda sesja ma trwałą Historię odtwarzania dostępną przez `Ctrl+H`; lista jest uporządkowana od najnowszego wpisu, nie zawiera duplikatów i oznacza ostatni lub wstrzymany element bez samoczynnej zmiany fokusu po starcie. W odtwarzaczu `Alt+strzałka w dół` wybiera starszy odtwarzany element, a `Alt+strzałka w górę` nowszy, przywracając zapamiętaną pozycję. `Page Up/Down` nadal przechodzi po liście źródłowej, zaś `Alt+lewo/prawo` pozostaje odrębną, nietrwałą historią widoków. `Shift+Delete` po jednoznacznym potwierdzeniu przenosi zaznaczone pliki lokalne do systemowego Kosza i usuwa je z AMC; tej operacji nie cofa `Ctrl+Z`. Zwykły `Delete` nadal nie dotyka dysku. `Ctrl+C` kopiuje nazwy wszystkich zaznaczonych elementów w osobnych wierszach, a `Ctrl+Shift+C` zachowuje ścieżki i dane `FileDrop`. Po zmianie sesji kontekst ma kolejność **numer i sesja → przywrócony widok → element**.

W `alpha.61` prawa strzałka na lokalnej liście nie otwiera już menu kontekstowego AMC. Wywołuje bezpośrednio systemowe **Otwórz w…**, aby jednorazowo wybrać np. foobar2000 albo zmienić aplikację domyślną, jeśli pozwala na to bieżąca wersja Windows. `Shift+Delete` działa również w otwartym odtwarzaczu: po potwierdzeniu zatrzymuje i zwalnia bieżący plik przed przekazaniem go do Kosza. Usunięte identyfikatory są równocześnie usuwane z trwałej historii oraz z jej aktywnej migawki. Zapis nie zachowuje już osieroconego identyfikatora ostatniego pliku po usunięciu całej lokalnej sesji, a normalizacja czyści starsze takie wpisy. Samo przeniesienie do Kosza pozostaje synchroniczną operacją powłoki Windows i może chwilę trwać na dysku iCloud.

W `alpha.62` lewa strzałka zawsze próbuje podać średni bitrate lokalnego pliku. Jeśli dane nie zostały jeszcze zapisane podczas odtwarzania, AMC otwiera tylko zaznaczony plik w trybie odczytu metadanych, ustala czas i częstotliwość próbkowania, wylicza `kb/s` z rozmiaru i czasu, a wynik zachowuje na przyszłość. Nie skanuje całej biblioteki. Dla niedostępnego albo nierozpoznawalnego pliku pozostałe informacje nadal są odczytywane. `Alt+F4` ma jednoznaczne znaczenie systemowe: w głównym oknie zamyka całą aplikację także wtedy, gdy widoczny jest odtwarzacz. `Escape` i `Shift+F6` pozostają poleceniami powrotu do listy.

W `alpha.63` przywrócona pozycja jest widoczna i używana jeszcze przed pierwszym otwarciem pliku przez silnik audio. Prawa strzałka uruchamia bezpośrednio systemowy wybór aplikacji także dla rozszerzenia bez poprawnego skojarzenia. `Shift+Delete` fizycznie przenosi do Kosza tylko pliki zaznaczone na liście. W lokalnym odtwarzaczu `Delete` usuwa bieżący wpis z AMC, pozostawia plik na dysku, oznajmia następny element i daje się cofnąć przez `Ctrl+Z`.

W `alpha.64` wycofano systemowe „Otwórz w…” i przypisaną do niego prawą strzałkę, ponieważ ręczny test NVDA wykazał utratę czytelnego fokusu. Prawa strzałka znów zachowuje standardowe działanie listy. Dostępne pozostaje „Otwórz w domyślnej aplikacji” dla poprawnie skojarzonych plików. Pozostałe poprawki `alpha.63`, w tym zapamiętywanie pozycji oraz bezpieczne `Delete` w odtwarzaczu, nie zmieniają się.

W `alpha.65` „Otwórz w…” wraca do testów po poprawieniu kolejności zdarzeń fokusu. AMC kończy obsługę prawej strzałki albo menu, a dopiero potem otwiera systemowy wybór aplikacji. Oddzielny test prawej strzałki i menu pozwoli zachować przynajmniej działające wejście, jeżeli problem okaże się związany tylko ze skrótem.

W `alpha.66`, po negatywnym teście obu wejść `alpha.65`, „Otwórz w…” jest uruchamiane przez osobny proces powłoki Windows. Ma to pozwolić systemowi ustanowić zwykły fokus pierwszoplanowy poza wątkiem WPF. Jest to ostatni wariant testowy; jeśli NVDA nadal nie odczyta okna, funkcja zostanie usunięta. W planie lokalnym Foldery stają się głównym widokiem, Zakładki poprzedzają prosty niedestrukcyjny montaż A–B, a późniejszy pilot NVDA pozostaje cienką warstwą nad wspólnym rdzeniem.

W `alpha.67` „Otwórz w…” zostaje definitywnie usunięte po negatywnych testach wszystkich trzech wariantów. Prawa strzałka zachowuje zwykłe działanie listy, a menu lokalnego pliku nadal zawiera stabilne „Otwórz w domyślnej aplikacji”. Zmianą skojarzeń zarządza Windows poza AMC. Pozostałe funkcje i plan rozwoju `alpha.66` pozostają bez zmian.

W `alpha.68` działają trwałe Zakładki. W otwartym odtwarzaczu `B` zapisuje bieżące miejsce, `Shift+Page Up` i `Shift+Page Down` przechodzą po zakładkach tego samego materiału, a `Ctrl+B` otwiera wspólną listę zakładek ze wszystkich sesji. Każdy wiersz podaje tytuł, czas i usługę; Enter wybiera właściwą sesję, rozpoczyna materiał i ustawia zapisaną pozycję. `Delete` usuwa wyłącznie zakładkę, a `Shift+Delete` jest w tym widoku blokowany, aby nie skasować pliku. Zakładki są zapisywane w `state.json` i w pełnej kopii `*.amcbackup.json`. Na zwykłych listach pojedyncze `B` nadal służy nawigacji literowej.

W `alpha.69` szybkie, kolejne naciśnięcia `Shift+Page Up` i `Shift+Page Down` poruszają się sekwencyjnie względem ostatnio osiągniętej zakładki, nawet gdy odtwarzanie zdążyło ruszyć dalej. Kategoria „Oznajmiaj nawigację po zakładkach” w Ustawieniach → Komunikaty pozwala wyciszyć automatyczną wypowiedź czasu bez wyłączania skrótów. Informacja o braku dalszej zakładki pozostaje słyszalna jako potrzebny komunikat graniczny. Nawigacja celowo nie przechodzi do innego pliku.

W `alpha.70` `Ctrl+Shift+B` otwiera w odtwarzaczu dostępne pole nazwy i zapisuje nazwaną zakładkę. Jeśli szybka zakładka już istnieje w tej samej sekundzie, otrzymuje podaną nazwę zamiast tworzenia duplikatu. Na liście `Ctrl+B` nazwa jest czytana jako pierwsza, przed tytułem materiału, czasem i usługą; działa również w filtrze i nawigacji literowej. Nazwa jest trwała i wchodzi do pełnej kopii danych.

W `alpha.71` globalna lista pokazuje najpierw zakładki bieżącego materiału w kolejności czasu od początku do końca. Pozostałe wpisy są grupowane według sesji i tytułu materiału, a wewnątrz każdego materiału również według czasu. Enter na zakładce ustawia fokus bezpośrednio na głównym przycisku odtwarzacza; Escape wraca na ten sam rekord listy. Ustawienie fokusu odbywa się z priorytetem załadowanego widoku, aby nie zatrzymywało się na ukrytej liście ani nagłówku.

W `alpha.72` każdy rekord globalnej listy Zakładek zaczyna się od tytułu pliku lub materiału, następnie podaje lokalną datę utworzenia zakładki i pozycję w materiale. Opcjonalna nazwa zakładki pozostaje częścią rekordu. Nawigacja `Shift+Page Up/Down` wewnątrz otwartego materiału jest krótsza: podaje sam czas, a dla nazwanej zakładki nazwę i czas. `Ctrl+C` na liście Zakładek kopiuje widoczne opisy zaznaczonych zakładek zamiast samego tytułu pliku źródłowego.

Ta sama wersja ujednolica schowek w obu oknach wyszukiwania: `Ctrl+C` kopiuje nazwę, a `Ctrl+Shift+C` kopiuje prawdziwy plik i jego pełną ścieżkę albo łącze do wyniku usługowego. `Ctrl+X` na zwykłych listach lokalnych oraz na lokalnym wyniku wyszukiwania przekazuje prawdziwe pliki do systemowego schowka z operacją przeniesienia. Sam skrót nie usuwa danych; przeniesienie wykonuje dopiero `Ctrl+V` w folderze docelowym. Po powrocie AMC usuwa wpisy, których stare ścieżki rzeczywiście przestały istnieć. Wycinanie nie działa na liście Zakładek ani dla elementów strumieniowych.

W `alpha.73` lista wyników wyszukiwania używa zaznaczania rozszerzonego. `Shift+strzałka` może zaznaczyć kilka wyników, `Ctrl+C` kopiuje ich nazwy w oddzielnych wierszach, a `Ctrl+Shift+C` przekazuje lokalne wyniki jako prawdziwe pliki oraz pełne ścieżki; dla wyników usługowych kopiuje łącza. Historia wpisanych zapytań pozostaje zwykłym tekstem. `Ctrl+X` i `Ctrl+V` są blokowane na wynikach wyszukiwania.

`Ctrl+V` na listach lokalnych pełni rolę dostępnego odpowiednika przeciągania i upuszczania. W widokach Multimedia i Biblioteka importuje obsługiwane pliki audio do katalogu AMC, w Kolejce dodatkowo dodaje je do kolejki, a w Ulubionych oznacza jako ulubione. Pliki pozostają w dotychczasowych folderach — AMC zapisuje ich rzeczywiste ścieżki i nie tworzy ukrytej kopii. Wklejanie jest niedostępne w Historii odtwarzania, Zakładkach, odtwarzaczu i sesjach streamingowych. Wewnętrzne wklejenie po `Ctrl+X` anuluje systemowy zamiar przeniesienia, aby późniejsze przypadkowe wklejenie poza AMC nie przesunęło pliku.

W `alpha.74` Zakładki są przejściowym, otwieranym jawnie widokiem. `Ctrl+B` zapamiętuje sesję, poprzedni widok i zaznaczony element, a Escape wraca dokładnie do tego miejsca. Po otwarciu zakładki Enterem pierwszy Escape wraca do tego samego rekordu Zakładek, a drugi — do listy, z której wywołano `Ctrl+B`. Jeśli filtr Zakładek zawiera tekst, pierwszy Escape czyści filtr i pozostawia widok otwarty. Program nie przywraca już po uruchomieniu samej listy Zakładek bez kontekstu; wcześniejszy taki stan jest normalizowany do podstawowej listy sesji. `Shift+Page Up/Down` zachowuje dwa jednoznaczne znaczenia: w odtwarzaczu przechodzi po zakładkach bieżącego materiału, natomiast na każdej zwykłej liście wykonuje standardowe rozszerzone zaznaczanie stronami i nigdy nie otwiera widoku Zakładek.

W `alpha.75` strzałka w lewo podaje krótkie informacje o elemencie zarówno na zwykłej liście, jak i na liście wyników wyszukiwania. Dla pliku lokalnego obejmują dostępne wartości: format, wykonawcę, czas, bitrate, częstotliwość próbkowania i rozmiar. W sesji streamingowej używany jest ten sam układ, ale AMC odczytuje wyłącznie metadane faktycznie zwrócone przez adapter usługi i nie wymyśla ani nie szacuje parametrów strumienia. Fokus pozostaje na wybranym rekordzie.

W `alpha.76` `Shift+Delete` używa nowoczesnego interfejsu `IFileOperation` powłoki Windows zamiast starszego mechanizmu kasowania. Jest to istotne dla plików-placeholderów zarządzanych przez iCloud Drive i innych dostawców chmurowych. Po potwierdzeniu operacja nadal przenosi wyłącznie do systemowego Kosza; nie wykonuje trwałego usunięcia. Błąd pojedynczego pliku nie zamyka AMC, nie usuwa jego rekordu z katalogu i jest oznajmiany wraz z kodem systemowym pomocnym w diagnozie. Pozostałe poprawnie przeniesione pozycje są bezpiecznie usuwane z katalogu AMC.

W `alpha.84` domyślne, jawne wyjście `Escape`, `Shift+F6` albo przyciskiem **Wróć do listy** wstrzymuje dźwięk. Można wyłączyć tę zasadę w Ustawieniach ogólnych. Od poprawki `alpha.205` przejście bezpośrednio do Kolejki, Biblioteki lub innego widoku nie jest traktowane jako jawne wyjście i zachowuje odtwarzanie. Tak samo `Ctrl+cyfra` oraz `Ctrl+Page Up/Page Down` przełączają sesję bez zmiany jej dźwięku. Zasada jest niezależna od pamiętania pozycji: domyślnie lokalne pliki zachowują miejsce, natomiast każdy Folder Biblioteki może wymusić **Pamiętaj pozycję odtwarzania**, **Zawsze od początku** albo wybrać **Zgodnie z ustawieniem globalnym**. Pliki dodane pojedynczo przez `Ctrl+O` używają zasady globalnej. Zwykła lista nadal zachowuje standardowe działanie strzałek; przewijanie i regulacja głośności pozostają w odtwarzaczu.

Od `alpha.215` powrót z odtwarzacza nie ustawia już najpierw fokusu na samym kontenerze listy. NVDA otrzymuje jedną, celowo uporządkowaną nazwę: **element i jego stan → lista**, na przykład „Radio 24, wstrzymany, lista”. Fokus nadal trafia bezpośrednio na właściwy element i nie może spaść do pola filtrowania podczas odświeżenia.

W `alpha.85` lista w menedżerze źródeł udostępnia NVDA wyłącznie czytelną etykietę źródła. Techniczny zapis rekordu z identyfikatorem i nazwami pól nie jest już przekazywany przez UI Automation.

W `alpha.86` `Ctrl+F5` otwiera **Foldery Biblioteki**, a `F5` pozostaje ich ręcznym odświeżeniem. Na lokalnej liście `F2` zmienia wyłącznie trwałą nazwę wyświetlaną w AMC, bez dotykania pliku. `Shift+F2` zmienia rzeczywistą nazwę pliku na dysku, zachowuje rozszerzenie, stabilny identyfikator, Ulubione, Kolejkę, Historię, Zakładki i pozycję wznowienia. Istniejący cel, nazwy niedozwolone i nazwy zarezerwowane Windows są odrzucane bez nadpisania. Jeśli zmieniany plik był załadowany, AMC zatrzymuje go i zwalnia przed operacją, zachowując miejsce do późniejszego wznowienia.

W `alpha.87` lokalna Biblioteka ma trzy jawne układy: `Alt+1` — **Foldery Biblioteki**, `Alt+2` — **Wszystkie pliki alfabetycznie** i `Alt+3` — **Kolejność własna**. Tylko w Kolejności własnej `Alt+strzałka w górę/w dół` przesuwa jeden plik albo ciągły blok zaznaczony Shiftem. Porządek jest trwały, nowe pliki trafiają na koniec, a operacja nie zmienia folderów, nazw ani położenia plików na dysku. Aktywny filtr blokuje przesuwanie. `Ctrl+K` filtruje tylko bieżącą listę; Escape czyści filtr i wraca do listy, natomiast przejście do innego widoku, folderu albo sesji również automatycznie usuwa filtr. Filtr nie jest przywracany po restarcie programu.

W `alpha.88` `Ctrl+Shift+A` tworzy prawdziwy lokalny widok **Albumy** także dla kolekcji bez kompletnych tagów. Folder mający co najmniej dwa bezpośrednie pliki audio z różnymi numerami na początku nazwy, np. `01`, `02`, `1 -` albo `2.`, staje się albumem. Jego nazwa jest tytułem albumu, a bezpośredni folder nadrzędny jest wykonawcą, jeśli układ znajduje się poniżej zarejestrowanego źródła. Lista albumów podaje liczbę utworów. Enter otwiera ich naturalną kolejność numerów, a Escape wraca na ten sam album. Daty rozpoczynające się czterocyfrowym rokiem, folder jednego pliku i przypadkowy katalog bez numeracji nie są klasyfikowane jako album. Odczyt osadzonych tagów albumu i ręczne „Traktuj folder jako album” pozostają następnymi rozszerzeniami; alpha.88 wdraża bezpieczny wariant folderowy bez modyfikowania plików.

W `alpha.89` lista, z której uruchomiono element, staje się trwałym **kontekstem odtwarzania** tej sesji. `Page Up`, `Page Down` i automatyczna kontynuacja pozostają więc w Ulubionych, otwartym albumie, bieżącym folderze, Kolejce albo Kolejności własnej. Późniejsze przeglądanie innego widoku nie zmienia kontekstu, dopóki użytkownik nie uruchomi z niego nowego elementu. Historia i Zakładki są odsyłaczami, nie osobnymi kolejkami. Filtr `Ctrl+K` nie ogranicza kontekstu do chwilowo widocznych wyników. `Alt+strzałka w górę/w dół` ustawia trwałą kolejność również w Ulubionych; nadal nie działa w Folderach, Wszystkich plikach alfabetycznie, Albumach, Historii ani wynikach wyszukiwania.

`Alt+Shift+Enter` otwiera dostępne **Opcje odtwarzania elementu**. Dla pliku lokalnego można nadpisać regułę pamiętania pozycji oraz prędkość; brak nadpisania dziedziczy ustawienie folderu lub ogólne i prędkość sesji. Urządzenie wyjściowe wybiera się obecnie dla całej sesji w menu Odtwarzanie; nadpisanie tylko dla jednego pliku lub folderu pozostaje nieaktywne, a w tym samym miejscu później może pojawić się EQ. `Alt+Enter` pozostaje oknem informacji tylko do odczytu i pokazuje skuteczne reguły. Menu pliku rozpoznanego jako ścieżka albumu oferuje także **Przejdź do albumu** i **Przejdź do wykonawcy**. Alias ustawiony przez `F2` zmienia wyłącznie tytuł widoczny w AMC; kolejność albumu nadal wynika z numerów fizycznych nazw plików, więc własne czyste tytuły nie naruszają kolejności ścieżek.

Od `alpha.90` `Alt+Shift+Enter` działa również na folderze i albumie. Ustawienia folderu dotyczą wszystkich plików poniżej niego, a folder zagnieżdżony może mieć własne, bardziej szczegółowe nadpisanie. Pojedynczy plik ma zawsze pierwszeństwo, następnie obowiązuje najbliższy folder, zarejestrowane źródło Biblioteki i ustawienie ogólne. Opcje są zapisywane według pełnej ścieżki, ale nie przenoszą, nie zmieniają ani nie otwierają plików. Dopasowanie ustawień pojedynczego pliku używa także jego ścieżki, dzięki czemu odświeżenie identyfikatora Biblioteki nie powoduje komunikatu o braku ustawień.

W `alpha.91` wszystkie zapisy do schowka mają wspólną obsługę chwilowej blokady Windows. `Ctrl+C`, `Ctrl+Shift+C`, kopiowanie z wyszukiwania i właściwości oraz `Ctrl+X` wykonują kilka krótkich ponowień, gdy NVDA, Total Commander, Ditto albo inny menedżer schowka właśnie odczytuje jego zawartość. Sukces jest oznajmiany dopiero po rzeczywistym zapisaniu danych. Jeżeli blokada nie ustąpi, AMC pozostaje responsywny i podaje jednoznaczny komunikat z kodem systemowym zamiast milczeć albo zgubić kolejne skróty.

W `alpha.92` pola wyboru w **Opcjach odtwarzania elementu lub folderu** przekazują do UI Automation wyłącznie etykiety przeznaczone dla użytkownika. NVDA nie powinien już odczytywać nazw klas ani zapisów takich jak `ResumeChoice { Value = ... }`; dotyczy to zarówno reguł pamiętania pozycji, jak i prędkości.

W `alpha.93` katalog Biblioteki, źródła folderowe, wykluczenia, kolejności, Historia i Zakładki zostały przeniesione z dużego pliku JSON do lokalnej bazy **SQLite**. Pierwsze uruchomienie wykonuje jednorazową migrację transakcyjną, sprawdza liczbę zapisanych rekordów i pozostawia plik `state.pre-sqlite-migration.json` jako kopię sprzed migracji. Baza znajduje się w `%LocalAppData%\AccessibleMediaController\library.db`; ustawienia i profile nadal są w `%AppData%\AccessibleMediaController\state.json`, a pełny eksport `.amcbackup.json` zachowuje dotychczasowy format przenośny.

Otwieranie dekodera i zamykanie poprzedniego toru audio odbywa się teraz poza wątkiem interfejsu. Skanowanie, Foldery, Wszystkie pliki, Albumy, szybka informacja pod lewą strzałką i `Alt+Enter` nie otwierają treści placeholdera chmurowego, więc nie pobierają całego folderu ani nawet wybranego pliku. Dopiero jawne odtworzenie konkretnego pliku może zlecić iCloud Drive, OneDrive albo Google Drive jego pobranie. AMC oznajmia wtedy „Pobieranie z chmury”, pozostawia działające menu, fokus i skróty, pozwala anulować operację i po dwóch minutach zgłasza przekroczenie czasu zamiast blokować okno. Rotacyjne logi diagnostyczne znajdują się w `%LocalAppData%\AccessibleMediaController\logs`; przechowywanych jest najwyżej pięć plików po około 5 MB.

W `alpha.94` lokalne `Ctrl+Shift+C` jest dodatkowo przechwytywane na poziomie komunikatu okna, podobnie jak zabezpieczone wcześniej `Ctrl+Z` i skróty czasu. Dotyczy to listy oraz odtwarzacza, ale nie pól tekstowych. Log diagnostyczny rozróżnia dotarcie skrótu do AMC, liczbę zaznaczonych elementów, powodzenie zapisu, ponowienia po chwilowej blokadzie oraz ostateczny błąd schowka; zapisuje nazwy formatów, nie zawartość schowka.

W `alpha.95` cofnięcie usunięcia lokalnego rekordu odtwarza również jego dokładną pozycję w **Kolejności własnej**. Działa to dla jednego pliku i zaznaczonego bloku. Foldery oraz Wszystkie pliki nadal wyliczają pozycję odpowiednio z hierarchii i nazwy, natomiast rzeczywiście nowy plik — nie przywracany przez `Ctrl+Z` — trafia na koniec Kolejności własnej.

`alpha.96` rozszerza tę samą gwarancję na usuwanie tylko z kolekcji. Przed zmianą AMC zapisuje pozycję elementu w **Ulubionych** albo lokalnej **Kolejności własnej**, a `Ctrl+Z` odtwarza członkostwo i dokładne miejsce. Element ze środka listy nie jest już traktowany jak nowy i dopisywany na końcu.

W `alpha.97` Playlisty są już trwałymi kolekcjami każdej sesji, a nie pozycjami demonstracyjnymi. `Ctrl+P` otwiera ich listę; Insert tworzy playlistę, F2 zmienia nazwę, Delete usuwa samą playlistę, a Enter otwiera jej elementy. W otwartej playliście Delete usuwa tylko odwołanie, `Alt+strzałka w górę/w dół` ustala własną kolejność, a Page Up, Page Down i automatyczna kontynuacja pozostają w tej playliście. `Ctrl+Shift+P` na jednej pozycji albo zaznaczonym bloku otwiera dostępny menedżer przynależności; Spacja przełącza stan, stan mieszany oznacza częściowe członkostwo, `Ctrl+K` przechodzi do filtra, Enter zapisuje, a Escape anuluje. Polecenie działa również z wyników wyszukiwania. Utworzenie, zmiana nazwy, usunięcie, członkostwo i kolejność podlegają `Ctrl+Z`, zapisują się w SQLite i wchodzą do pełnej kopii `.amcbackup.json`; żadne z tych działań nie usuwa pliku multimedialnego z dysku.

W `alpha.98` Kolejka ma trwałą kolejność użytkownika w każdej sesji. W widoku Kolejka `Alt+strzałka w górę/w dół` przenosi jedną pozycję albo zaznaczony blok; `Ctrl+Z` przywraca dokładne poprzednie miejsce, a restart zachowuje układ w SQLite i pełnej kopii AMC. Pozycje oznaczone „Odtwórz jako następne” są zawsze wyświetlane i odtwarzane przed zwykłą kolejką. Można zmieniać kolejność wewnątrz każdej z tych dwóch grup, lecz nie mieszać ich jednym ruchem. Naturalny koniec utworu zużywa elementy dokładnie w widocznej kolejności: najpierw „Odtwórz jako następne”, potem zwykłą Kolejkę, a następnie wraca do wcześniejszego kontekstu odtwarzania.

W `alpha.99` `Ctrl+Q` pozostawia jedną wspólną Kolejkę, ale jej priorytetowa część jest jednoznaczna dla NVDA: wejście podaje liczbę pozycji „jako następne” i pozostałych, a każdy priorytetowy wiersz zaczyna się od słowa „Następny”. Od korekty `alpha.208` bieżący element pozostaje widoczny w Kolejce przez cały czas odtwarzania; znika dopiero po zakończeniu albo ręcznym przejściu dalej. W odtwarzaczu Page Up i Page Down poruszają się po zapamiętanej kolejności tej Kolejki także wtedy, gdy AMC wszedł do niej automatycznie po zakończeniu pliku z Biblioteki. Page Up może wrócić do wcześniej odtworzonej pozycji, a ręczne przechodzenie nie powoduje późniejszego powtarzania zużytych elementów. Po wyczerpaniu Kolejki uruchomionej z innego widoku AMC wraca do dalszej części wcześniejszego kontekstu; Kolejka uruchomiona bezpośrednio kończy się bez ponownego odtwarzania własnych pozycji.

W `alpha.100` wycięcie, przeniesienie albo usunięcie bieżącego pliku nie może już przestawić odtwarzacza na pierwszy element całej Biblioteki. AMC pamięta dokładny kontekst, z którego uruchomiono materiał: Kolejkę, folder, album, Ulubione, playlistę, Bibliotekę, wyniki wyszukiwania albo dowolny inny obecny lub przyszły widok. Wybiera następny dostępny element wyłącznie z tego kontekstu. Ta sama reguła obowiązuje przy zmianie wykrytej przez monitoring folderu, wycięciu do schowka AMC, Delete oraz Shift+Delete. Odtwarzanie pozostaje zatrzymane do naciśnięcia Spacji. Gdy w źródłowym widoku nie ma następcy, AMC nie wybiera pliku zastępczego i mówi o braku następnego elementu.

Po ręcznym przeniesieniu pozycji `Alt+strzałką w górę/w dół` NVDA otrzymuje w `alpha.100` komunikat relacyjny, na przykład „Przeniesiono w górę, nad [tytuł]” albo „Przeniesiono w dół, pod [tytuł]”. Dla zaznaczonego bloku komunikat podaje także liczbę przeniesionych elementów. Nazwa wskazuje sąsiada wypartego przez operację, dzięki czemu samo nowe miejsce jest zrozumiałe bez ponownego przeglądania listy.

W `alpha.101` użytkowa nazwa technicznych źródeł folderowych została ujednolicona do **Folderów Biblioteki**. `Ctrl+F5` otwiera okno o tej nazwie, a `F5` odświeża Foldery Biblioteki. Pole **Dla wybranego folderu** zawiera trzy jednoznaczne warianty: **Pamiętaj pozycję odtwarzania**, **Zawsze od początku** i **Zgodnie z ustawieniem globalnym**. Ustawienia ogólne oraz `Alt+Shift+Enter` używają tych samych dwóch nazw decyzji; wariant dziedziczenia elementu lub folderu dodatkowo wskazuje rzeczywisty poziom nadrzędny.

W `alpha.102` `Ctrl+F5` ustawia fokus bezpośrednio na pierwszym zaznaczonym Folderze Biblioteki, dzięki czemu NVDA odczytuje od razu cały wiersz zamiast samej nazwy listy. „Folder dostępny” oznacza, że korzeń folderu jest obecnie osiągalny. **Aktywne** to należące do Biblioteki pliki widoczne w systemie — również chmurowe placeholdery, których treść nie została jeszcze pobrana. **Niedostępne** to wcześniej zapamiętane pliki, których ścieżka jest obecnie nieosiągalna, na przykład po przeniesieniu albo odłączeniu dostawcy chmury. **Wykluczone** to ścieżki świadomie usunięte z Biblioteki zwykłym `Delete`; plik pozostaje na dysku, ale `F5` ani restart nie dodają go ponownie.

W `alpha.103` **Foldery Biblioteki** pozwalają przejrzeć niedostępne rekordy wybranego folderu i ręcznie wybrać jeden albo wiele z nich do operacji **Zapomnij w AMC**. Program nigdy nie wykonuje jej automatycznie. Ostrzeżenie wyjaśnia, że z AMC znikną rekord Biblioteki i jego powiązania z Ulubionymi, Kolejką, playlistami, Historią, Zakładkami oraz pozycją wznowienia, ale żaden plik na dysku nie zostanie zmieniony. Chmurowe placeholdery widoczne w systemie nie są uznawane za niedostępne. Ponownie pojawiający się plik zapomniany wcześniej jest indeksowany jako nowy rekord.

W `alpha.104` trwała Historia odtwarzania pod `Ctrl+H` działa jak zwykła lista ułożona od najnowszego wpisu. Można zaznaczyć wiele pozycji, skopiować je, zmienić ich Kolejkę lub Ulubione oraz przez `Ctrl+Shift+P` utworzyć z nich playlistę albo uzupełnić istniejącą. `Delete` usuwa wyłącznie zaznaczone wpisy z Historii: nie zmienia Biblioteki, playlist, Kolejki, Ulubionych ani plików na dysku. `Shift+Delete` pozostaje osobną, potwierdzaną operacją fizycznego przeniesienia lokalnego pliku do Kosza. Historii nie można ręcznie przestawiać, ponieważ jej porządek wynika z czasu odtwarzania.

W `alpha.105` klawisz `F1` otwiera dostępny, przeszukiwalny spis skrótów podzielony na sekcje. Każdy wiersz ma wyłącznie nazwę użytkową, aktualny skrót i kontekst; nie pokazuje technicznych identyfikatorów. Wykonywalne pozycje można uruchomić Enterem przez ten sam bezpieczny router co paletę poleceń. `Ctrl+F1` włącza Pomoc klawiatury: kolejne kombinacje są opisywane, ale nie wykonywane, aż do ponownego `Ctrl+F1` albo `Escape`. Znak `?` otwiera spis tylko poza polami tekstowymi.

W `alpha.106` lokalny dekoder OGG/Vorbis normalizuje oś czasu fragmentów wyciętych z ciągłych transmisji. Taki plik może zachować bardzo duży początkowy numer próbki, którego NVorbis nie może traktować jako długości samodzielnego nagrania. AMC odejmuje początek strumienia bez konwersji i bez modyfikowania pliku, podaje rzeczywisty czas, przewija względem początku fragmentu i kończy odczyt na jego fizycznym końcu. Dodatkowy test Windows tworzy taki przypadek niezależnie od kolekcji użytkownika i chroni przed powrotem pętli obciążającej procesor.

W `alpha.107` Pomoc klawiatury `Ctrl+F1` używa zwięzłego kontekstu będącego wyłącznie nazwą aktywnej sesji, np. `Pliki lokalne`, `WiiM` albo `TIDAL`. Nie powtarza już widoku w rodzaju `Foldery` lub `Kolejka`. Opis lewej strzałki jest konkretny: w Plikach lokalnych mówi o oznajmianiu wielkości i bitrate pliku, a w innych sesjach o bitrate i pozostałych dostępnych parametrach elementu.

W `alpha.108` wspólna osłona wszystkich lokalnych dekoderów sprawdza parametry i wiarygodność czasu przed odtwarzaniem. Pasek stanu i NVDA nie pytają już bezpośrednio zajętego dekodera o pozycję. Osiem sekund bez postępu zatrzymuje tylko wadliwy element, pozostawia AMC responsywne i zapisuje `decoder-watchdog` z nazwą źródła. Lewa strzałka odczytuje brakujące metadane w tle z limitem pięciu sekund, więc uszkodzony plik ani wolna chmura nie blokują głównego okna. Plik nie jest zmieniany ani usuwany. Wpis `watchdog.waitForFreezeRecovery` w logu NVDA jest informacją o wykryciu cudzego zawieszenia, a nie jego przyczyną.

W `alpha.109` wiersz folderu nigdy nie otrzymuje fałszywego stanu Ulubionych, Kolejki ani „odtwarzaj jako następne”. Enter nadal otwiera folder. `Shift+Enter`, `Ctrl+Shift+Enter`, `Ctrl+Shift+U` oraz zarządzanie playlistami działają na wszystkich aktualnie dostępnych i zaindeksowanych plikach audio w folderze i jego podfolderach. Powtórzenie przełączanego polecenia usuwa tę samą zawartość z kolekcji, komunikat wymienia folder i liczbę plików, a `Ctrl+Z` cofa całą zmianę jako jedną operację. Pusty folder niczego nie zmienia. Operacja korzysta wyłącznie z katalogu AMC: nie otwiera plików, nie odczytuje metadanych i nie pobiera placeholderów z chmury. `Ctrl+Shift+L` nie działa na kontenerze, ponieważ zarejestrowany folder już definiuje zawartość Biblioteki; przynależność pojedynczego pliku można zmienić po wejściu do folderu.

Od `alpha.110` częściowo wykorzystany folder ma jednoznaczną semantykę przełącznika. Jeżeli choć jeden jego plik pozostaje w Kolejce, grupie „odtwarzaj jako następne” albo Ulubionych, polecenie folderu i menu kontekstowe proponują usunięcie i czyszczą pozostałą zawartość folderu z tej kolekcji. Dopiero gdy żaden plik folderu już do niej nie należy, następne polecenie dodaje całą zawartość. Odtworzenie albo ręczne usunięcie pojedynczego pliku nie może więc przypadkowo zmienić działania folderu na ponowne dodawanie wszystkiego.

Od `alpha.111` przewijanie wolnego albo wielogodzinnego pliku odbywa się poza wątkiem okna. Dotyczy to zwłaszcza strumieniowanego Dysku Google, który może dociągać dane dopiero po skoku o kilka godzin, mimo braku zwykłego znacznika placeholdera Windows. AMC rozpoznaje typowe lokalizacje Dysku Google, iCloud, OneDrive i Dropbox, pozostawia fokus oraz skróty dostępne podczas oczekiwania i łączy serię szybkich skoków w ostatnią żądaną pozycję. Odczyt paska w tym czasie pokazuje cel, a nie starą pozycję dekodera. Wolne przewinięcie jest mierzone w logu; po dwóch minutach bez zakończenia nadzór bezpiecznie zatrzymuje element. Zwykły lokalny plik nadal przewija się bez zauważalnej zwłoki.

Od `alpha.112` ta ochrona jest adaptacyjna. Pliki z iCloud, OneDrive, Google Drive, Dropbox, Box Drive, pCloud, MEGA, Proton Drive, Nextcloud, ownCloud, Sync oraz udziałów sieciowych otrzymują dłuższe limity doczytywania i po chwilowym braku sieci nie są blokowane do restartu. Lokalne MP3 są przed odtwarzaniem sprawdzane przez odczyt najwyżej 1 MiB okolicy początku danych; zadeklarowany rozmiar ID3 nigdy nie steruje wielkością alokacji. Jeżeli systemowy dekoder odrzuci albo zatrzyma prawidłowo rozpoznany lokalny MP3 do 512 MiB, AMC jednokrotnie przechodzi na zarządzany dekoder NLayer i zachowuje pozycję, głośność oraz prędkość. Większe i zdalne pliki nadal korzystają z dekodera strumieniowego, więc fallback nie wywołuje pełnego skanowania ani masowego pobrania. Limit wiarygodnego czasu zwiększono z 30 do 365 dni; nieprawidłowy albo nieobsługiwany plik kończy się krótkim komunikatem użytkowym, a szczegóły pozostają w logu.

Od `alpha.113` każdy jawnie otwierany lokalny lub chmurowy plik otrzymuje dodatkowy, ograniczony do 64 KiB preflight kontenera. Rozpoznawane są nagłówki RIFF/RF64/BW64 WAVE, AIFF/AIFC, FLAC, OGG Vorbis i Opus, MP4/M4A/MOV/3GP, WMA/ASF, Matroska/WebM, AVI oraz ADTS/ADIF AAC. Preflight nie przechodzi po wszystkich blokach, nie ufa ich rozmiarom i nie zastępuje dekodera; wykrywa oczywiste ucięcie albo niezgodność rozszerzenia i zapisuje ją wyłącznie w logu. OGG jest kierowany do wbudowanego czytnika Vorbis tylko wtedy, gdy naprawdę zawiera Vorbis, a Ogg Opus pozostaje dla dekodera systemowego. Awaria dowolnego formatu odłącza wadliwy tor zamiast pozostawiać go jako pozornie gotowy do wznowienia. Końcowy tor kontroluje pełne ramki kanałowe oraz zastępuje wartości NaN i nieskończone ciszą przed przekazaniem do urządzenia audio. Skan Biblioteki rozpoznaje ponadto typowe kontenery obsługiwane przez Media Foundation; faktyczna dostępność AC-3, Opus i mniej typowych kodeków nadal zależy od wersji Windows i zainstalowanych składników.

Od `alpha.114` stan pliku chmurowego jest ustalany także przez systemowe Cloud Files API na podstawie atrybutów i znacznika punktu ponownej analizy, bez otwierania danych. Dzięki temu zabezpieczenie obejmuje zgodnego dostawcę niezależnie od nazwy folderu; rozpoznawanie ścieżek i wirtualnych dysków pozostaje uzupełnieniem dla Google Drive i starszych klientów. Kolejne awarie jednego zdalnego źródła otrzymują narastającą przerwę od 10 sekund do 5 minut, co zapobiega tworzeniu wielu zablokowanych dekoderów, ale po odzyskaniu połączenia plik nie jest trwale blokowany. MP3 z ogromnym ID3, błędną wersją tagu, dodatkowymi bajtami lub ramkami free-format jest sprawdzany ograniczonym odczytem. Dekoder może dostać strumień zaczynający się dokładnie od potwierdzonej ramki, bez kopiowania i bez limitu wielkości pliku; NLayer pozostaje awaryjną ścieżką wyłącznie do 512 MiB, ponieważ może skanować całość. Wstępna walidacja porównuje z fizyczną długością także pierwsze deklaracje RIFF/WAVE/AVI, AIFF, FLAC, OGG, MP4 oraz ADTS AAC. Ostrzeżenia nadal trafiają tylko do logu, a ostateczną decyzję podejmuje dekoder.

Od `alpha.89` `Delete` jest jedynym klawiszem usuwania. `Backspace` nigdy nie usuwa elementu: w Folderach otwiera folder nadrzędny, wewnątrz albumu lub innego kontenera wraca o jeden poziom, a w odtwarzaczu wraca do listy z zastosowaniem zwykłej reguły pauzy. W polach tekstowych zachowuje standardowe kasowanie znaku, a na najwyższym poziomie listy podaje, że nie ma poziomu nadrzędnego. `Alt+strzałka w lewo/prawo` pozostaje historią odwiedzonych widoków i nie zastępuje semantyki rodzica.

Obecny katalog demonstracyjny może pokazywać wspólne wyniki testowe. Prawdziwy adapter TIDAL będzie modułem izolowanym: `Ctrl+Shift+F` może uruchomić jego zapytanie, ale treści TIDAL nie zostaną wymieszane na jednej liście z treściami podobnych usług. AMC otworzy osobny, oznaczony widok wyników TIDAL i zachowa działanie wszystkich wspólnych skrótów.

Planowany moduł YouTube zacznie od publicznego wyszukiwania i oficjalnego, widocznego odtwarzacza bez synchronizacji konta. Jego lokalna Biblioteka obejmie wyłącznie materiały świadomie dodane do Ulubionych, własne playlisty AMC i lokalną historię odtwarzania; nie kopiujemy całego konta ani pełnego interfejsu YouTube. Logowanie OAuth pozostaje nieobowiązkowym późniejszym rozszerzeniem, jeżeli pojawi się realna potrzeba subskrypcji, playlist lub polubień z konta. Oficjalny adapter nie będzie pobierać, wyodrębniać dźwięku ani nagrywać materiałów odtwarzanych z YouTube. Ewentualne eksperymentalne narzędzia zapisu pozostaną osobnym, izolowanym i niezależnie aktualizowanym modułem dla źródeł, które na zapis pozwalają; nie staną się częścią rdzenia ani warunkiem działania YouTube. Interfejs może być podobny do podcastów, lecz źródłem pozostaje oficjalny odtwarzacz YouTube.

`Ctrl+Z` cofa kolejno zmiany przynależności do Ulubionych, Biblioteki i Kolejki oraz stan „Odtwórz jako następne”; przywrócony element jest ponownie zaznaczany, jeśli znajduje się w bieżącym widoku. W polu filtra `Ctrl+Z` zachowuje standardowe znaczenie cofania edycji tekstu. Polecenie jest także dostępne w menu **Edycja**. Dodatkowe angielskie „Undo” zostało przypisane funkcji **Clipboard command announcement** dodatku NVDA Global Commands Extension, a nie mechanizmowi AMC. `Ctrl+N` i `Ctrl+A` pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko. Wpisywanie pojedynczych i kolejnych liter bez modyfikatorów na liście przechodzi do głównej nazwy pasującego elementu i nie uruchamia poleceń. Dla utworu jest nią tytuł, dla wykonawcy jego nazwa, dla albumu tytuł albumu, a dla playlisty, stacji lub urządzenia ich nazwa. Kryterium pozostaje niezależne od skonfigurowanej kolejności odczytu pól. Nowa sekwencja zaczyna się po krótkiej przerwie albo od razu wtedy, gdy dotychczasowy ciąg z następną literą nie daje dopasowania; ponawianie jednej litery przechodzi przez kolejne pasujące pozycje.

Pusta lista zatrzymuje klawisze strzałek i podaje komunikat „lista jest pusta”, zamiast przenosić fokus do przycisków lub menu. Widok „Teraz odtwarzane” ma 17 stałych elementów przeznaczonych do testowania nawigacji literami. Demonstracyjna Biblioteka celowo zawiera tylko dwa utwory należące do biblioteki.

Opcjonalna szczegółowa pomoc wyniku wyszukiwania jest celowo krótka: wymienia tylko strzałki, Enter i Escape. Pozostałe działania są dostępne w menu kontekstowym i dokumentacji, ale nie wydłużają każdego odczytu wyniku.

Po otwarciu Ustawień fokus zawsze trafia na zaznaczoną kartę „Ogólne”. Strzałki w lewo i w prawo zmieniają kategorię, a Tab przechodzi do kontrolek wybranej karty. Zarówno Zapisz, jak i Anuluj przywracają fokus do zaznaczonego elementu głównej listy; komunikat o zapisaniu jest podawany dopiero po przywróceniu listy.

Prefiks, czas oczekiwania i wszystkie polecenia można zmienić w ustawieniach. Można też wybrać, czy po uruchomieniu program ma otwierać listę multimediów, czy listę sesji. Chroniony profil wbudowany jest odświeżany wraz z wersją programu; edytowalne profile użytkownika zachowują własne przypisania.

## Kolejność odczytu list

Na karcie **Listy i odczyt** można ustawić kolejność informacji odczytywanych dla elementów listy: tytułu, wykonawcy, czasu trwania i typu elementu. `Alt+Strzałka w górę/dół` przenosi zaznaczone pole, pozostawia fokus na liście i ogłasza jego nowe położenie. Podgląd aktualnej kolejności znajduje się przed przyciskami zmiany. Domyślna kolejność to tytuł, wykonawca, czas trwania, typ. Pola bez wartości są pomijane. Ustawienie obejmuje listy oraz komunikaty o bieżącym elemencie.

Polecenia **Dodaj do kolejki**, **Odtwórz jako następne** i **Ulubione** działają w prototypie jak przełączniki. Ponowne wykonanie usuwa element z odpowiedniego miejsca, a nazwa w menu kontekstowym odzwierciedla bieżący stan. Domyślne komunikaty zawierają nazwę zmienianego elementu, a własne szablony użytkownika nie są zastępowane podczas aktualizacji. Przed usunięciem fokus jest kotwiczony na kontrolce listy, a po zakończeniu układu WPF przywracany na najbliższy element; jeśli lista stała się pusta, pozostaje na pustej liście. W głównym oknie aktywny filtr jest po `Escape` czyszczony, a fokus wraca do ostatnio zaznaczonego elementu listy. Bez aktywnego filtra `Escape` również wraca z pola lub głównego przycisku do listy. W demonstracyjnych widokach wyszukiwania `Escape` wraca do poprzedniego widoku i ponownie ustawia fokus na jego liście. `Alt+F4` zamyka aktywne okno: w wyszukiwaniu wraca do okna głównego, a użyty w oknie głównym kończy aplikację. Samo wejście do wyszukiwania podaje nazwę trybu bez czasu i liczby elementów poprzedniej listy. Menu zachowuje standardowe działanie: każde naciśnięcie `Escape` wychodzi o jeden poziom.

Na karcie **Komunikaty** opcja **Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach** steruje rozbudowanymi opisami filtra oraz obu rodzajów wyszukiwania. Jest domyślnie wyłączona, dzięki czemu NVDA nie powtarza przy każdym wyniku instrukcji o Enterze i Escape.

## Import i eksport

Program rozróżnia trzy rodzaje plików:

- `*.amckeys.json` — pojedyncza mapa klawiszy;
- `*.amcsettings.json` — konfiguracja bez map klawiatury;
- `*.amcbackup.json` — pełna kopia: ustawienia, profile klawiatury, sesje, zakładki, playlisty i szablony komunikatów.

Żaden eksport nie zawiera haseł, tokenów ani danych logowania.

Ustawienia robocze i profile programu są przechowywane w `%AppData%\AccessibleMediaController\state.json`. Lokalny katalog Biblioteki i jego relacje znajdują się w `%LocalAppData%\AccessibleMediaController\library.db`. Pełna kopia `.amcbackup.json` nadal łączy oba zbiory w jeden przenośny plik.

## Aktualizacje

Projekt ma oddzielony interfejs systemu aktualizacji oraz ustawienia kanału, pobierania w tle i instalacji przy zamknięciu. Serwer aktualizacji nie jest jeszcze skonfigurowany.

Wersje `alpha` pozostają na razie przenośne. Przed publiczną betą powstanie instalowane wydanie dla bieżącego użytkownika jako podpisany MSIX z App Installerem oraz małym nadzorcą zdrowia i powrotu. Obowiązkowy kod, biblioteki i środowisko .NET będą aktualizowane jako jeden zgodny zestaw; aktualizacje nie przerwą odtwarzania, nie podmienią działającej wtyczki NVDA i nie zmienią danych użytkownika. Nieudane pobranie, weryfikacja lub migracja pozostawi bieżącą wersję i kopię danych w stanie używalnym.

Plan menu **Pomoc** obejmuje: dostępny hierarchiczny spis skrótów pod `F1` i opcjonalnym `?`, tryb poznawania klawiatury `Ctrl+F1`, ręczne sprawdzanie aktualizacji, informacje o pełnej wersji oraz przyszłe publiczne repozytorium. Spis będzie generowany z aktywnego profilu klawiatury i wspólnego katalogu poleceń; aktywowanie pozycji wykona ją tą samą bezpieczną drogą co paleta. `Ctrl+F1` tylko opisze przechwycony klawisz i nie wykona jego polecenia. Puste odsyłacze nie pojawią się w wersjach roboczych.

Docelowy mechanizm powinien:

- publikować samowystarczalny program niewymagający ręcznego instalowania .NET;
- działać dla bieżącego użytkownika bez uprawnień administratora;
- aktualizować aplikację i adaptery usług;
- weryfikować podpis oraz SHA-256 każdego pakietu;
- instalować atomowo i umożliwiać powrót do poprzedniej wersji;
- nie zmieniać profili, konfiguracji ani danych logowania;
- nie kraść fokusu i nie przerywać odtwarzania komunikatami.

## Playlisty i presety w alpha 140

Wiersz playlisty skończonych materiałów podaje liczbę elementów i **łączny czas**. Gdy część czasów nie jest jeszcze znana, etykieta mówi o znanym czasie i brakujących danych. Playlista radia nie udaje materiału o skończonej długości i jest oznaczona jako **transmisje na żywo**.

Enter otwiera zawartość playlisty. `Ctrl+Enter` odtwarza jej pierwszy dostępny element i ustawia całą playlistę jako kontekst Page Up/Page Down. Menu kontekstowe playlisty rozróżnia otwieranie od odtwarzania oraz jawnie nazywa działania zbiorcze na jej zawartości: Kolejkę, „Odtwórz jako następne”, Ulubione i Bibliotekę. W Radiu Kolejka oraz „Odtwórz jako następne” pozostają niedostępne. `Alt+Enter` pokazuje właściwości samej playlisty, a nie przypadkowo bieżącego utworu. W lokalnej sesji `Alt+1/2/3` działa również z wnętrza playlisty i przechodzi odpowiednio do Folderów, Wszystkich plików albo Kolejności własnej.

Bezpośrednie presety `Ctrl+Shift+1–0`, minus i znak równości są przechwytywane na granicy komunikatów Win32. Stan modyfikatorów jest odczytywany bezpośrednio z systemu, co usuwa szczególny przypadek, w którym polski układ klawiatury lub czytnik ekranu mógł zgubić Shift dla klawisza `0`.

Lokalne kontenery wideo, w tym MP4, MKV, WebM, MOV, AVI i MPEG, są już obsługiwane jako źródła dźwięku.

## Preset 0 i harmonogram Radia w alpha 141

`Ctrl+0` i `Ctrl+Shift+0` mają rozłączne trasy poleceń. Pierwszy skrót nadal otwiera listę sesji, natomiast drugi uruchamia dziesiąte miejsce presetu, nazywane w komunikacie bezpośrednim „Preset 0”. AMC śledzi stan modyfikatorów już od ich surowych komunikatów Windows i nie pozwala, aby chwilowe pominięcie Shift przez WPF zdegradowało skrót do `Ctrl+0`. Regresję obejmuje test całej decyzji routingu, a nie tylko mapowania klawisza `0` na numer miejsca.

W sesji **Radio internetowe** `Ctrl+Shift+H` otwiera dostępny **Harmonogram nagrywania radia**. Insert dodaje plan, Enter go edytuje, Spacja włącza lub wyłącza, a Delete usuwa; każda pozycja podaje stację, najbliższy termin, długość, podział na pliki, sposób powtarzania i stan. Plan może być jednorazowy, codzienny albo ograniczony do wybranych dni, ma długość od minuty do tygodnia, zapis do jednego pliku albo części o określonej długości, własny format i bitrate, opcjonalny folder oraz trzywartościową regułę wybudzania. Nagrywanie działa w osobnym, niesłyszalnym torze i nie zmienia stacji odtwarzanej przez użytkownika; kilka planów może działać równolegle.

Plan zachowuje stabilny identyfikator stacji i migawkę jej adresu. Zmiana nazwy lub adresu własnej stacji aktualizuje powiązane plany. Gdy AMC uruchomi się wewnątrz trwającego przedziału, zapisuje tylko pozostałą część; całkowicie pominięty termin jednorazowy jest usuwany, a cykliczny przechodzi do następnego właściwego dnia. Niedostępny folder planu powoduje bezpieczny zapis w folderze ogólnym. Aktywne nagranie blokuje automatyczne uśpienie, a najbliższy plan wymagający wybudzenia korzysta z jednego czasomierza Windows. Wybudzenie działa, gdy AMC pozostaje uruchomiony podczas uśpienia; zamknięty program nie pozostawia ukrytego zadania systemowego.

## Dostępne nagrywanie Radia i preset 0 w alpha 142

Gdy okno AMC jest na pierwszym planie, `Ctrl+Shift+0` ma dodatkową, niskopoziomową drogę klawiatury. Dzięki temu dziesiąty preset powinien zadziałać również wtedy, gdy NVDA albo jego dodatek nie przekazuje tej kombinacji do zwykłej kolejki komunikatów WPF. Przechwytywanie dotyczy wyłącznie dokładnego `Ctrl+Shift+0` i aktywnego okna AMC; `Ctrl+0` nadal otwiera listę sesji, a skróty w innych programach nie są przejmowane.

W otwartym odtwarzaczu Radia `R` rozpoczyna i kończy nagrywanie aktualnie słyszanej stacji. Od `alpha.153` jest to osobny, niesłyszalny tor: Page Up, Page Down, Enter i presety mogą zmienić odsłuch bez przerywania zapisu. `T` podczas ręcznego nagrywania finalizuje bieżącą część i od razu rozpoczyna następny plik; działa również na wybranej stacji w widoku **Nagrywane**. `Ctrl+Alt+R` uruchamia lub zatrzymuje nagranie wybranej stacji bez otwierania odtwarzacza, a kilka różnych stacji może być nagrywanych równolegle. `Alt+2` otwiera widok **Nagrywane**, obejmujący aktywne nagrania ręczne i harmonogramy; `R` w tym widoku zatrzymuje wybrane nagranie ręczne. `Shift+R` na stacji w liście albo w odtwarzaczu otwiera **Nagrywanie czasowe i harmonogram radia**. Pełną listę planów otwiera `Ctrl+Shift+H`; osobny `Shift+T` nie jest potrzebny.

## Ustawienia nagrywania Radia w alpha 143

Ustawienia mają osobną kartę **Radio i nagrywanie**. Można w niej wybrać domyślny folder systemowy albo własny folder, format `MP3`, `M4A (AAC)` lub `WAV`, bitrate dla formatów stratnych oraz domyślne wybudzanie przed planem. MP3 pozostaje ustawieniem początkowym 192 kb/s. WAV zapisuje zdekodowany PCM bez kompresji i dlatego tworzy znacznie większe pliki.

Każdy wpis harmonogramu jawnie wybiera **ogólny folder z ustawień programu** albo **inny folder dla tego planu**. Wybudzanie pozostaje trzywartościowe: zgodnie z ustawieniem ogólnym, zawsze wybudzaj lub nigdy nie wybudzaj; etykieta dziedziczenia podaje również bieżącą wartość ogólną. Gdy własny folder planu albo własny folder ogólny jest niedostępny, AMC wykonuje bezpieczną próbę zapisu i przechodzi kolejno do folderu ogólnego oraz systemowego zamiast utracić nagranie.

Nagrywanie ręczne i zaplanowane korzysta z tego samego zdekodowanego toru co odsłuch. Obejmuje to MP3, AAC, OGG oraz HLS, jeżeli dany strumień daje się odtworzyć przez dostępny dekoder AMC. Dla HLS wymagającego FFmpeg ten komponent musi być dostępny również podczas planu. AMC nie oferuje pozornie „oryginalnego formatu” HLS, ponieważ transmisja jest zbiorem zmiennych segmentów, a nie jednym stabilnym plikiem. Plik roboczy `.amc-partial` otrzymuje docelową nazwę dopiero po prawidłowym zamknięciu kodera.

Wybudzanie jest opcjonalne i wymaga, aby AMC pozostawał uruchomiony podczas uśpienia. Ostateczna skuteczność zależy od sprzętu, planu zasilania Windows i ustawień wybudzania, szczególnie na zasilaniu bateryjnym.

## Zakres stacji i stan nagrywania w alpha 144

`Shift+R` otwarty na liście nie korzysta już z całego wewnętrznego katalogu sesji. Pole stacji zawiera wyłącznie stacje z bieżącego widoku — na przykład aktualnych Ulubionych, Historii albo otwartej playlisty — w tej samej kolejności, z podświetloną stacją wybraną na początku. Dawne, niewidoczne wyniki wyszukiwania Radio Browser nie pojawiają się samoczynnie. Pełny menedżer harmonogramu zachowuje dodatkowo stacje Biblioteki i istniejących planów.

Aktywnie zapisywana stacja otrzymuje na każdej liście krótki stan **nagrywanie** podawany po nazwie stacji. Jeżeli jest równocześnie odtwarzana, NVDA mówi najpierw nazwę, a potem stany **nagrywanie, odtwarzany**. `Alt+Enter` zawiera osobną sekcję Nagrywanie: rodzaj ręczny lub planowy, godzinę rozpoczęcia, ręczne albo planowane zakończenie, format i folder lub plik. Dzięki temu zwykła nawigacja pozostaje krótka, a dokładne dane są dostępne na żądanie. `Alt+Shift+Enter` nie został zajęty nową funkcją i zachowuje rolę opcji elementu w usługach, które je udostępniają.

## Widok po uruchomieniu presetu w alpha 145

Preset odtwarzalnego elementu domyślnie rozpoczyna odtwarzanie w tle i nie odbiera fokusu z bieżącej listy. Dotyczy to wszystkich sesji oraz uruchamiania z listy presetów i bezpośrednimi skrótami `Ctrl+Shift+1–0/-/=`. Jeśli odtwarzacz był już otwarty, pozostaje otwarty i pokazuje nowy element.

W `Ustawienia > Ogólne > Odtwarzanie` znajduje się opcja **Po uruchomieniu presetu otwieraj odtwarzacz**. Po jej włączeniu preset przechodzi do odtwarzacza. Escape wraca wtedy do widoku i pozycji, z których wywołano preset; opcja podążania fokusu po wyjściu z odtwarzacza nadal działa niezależnie. Folder, album lub playlista przypisane do presetu pozostają kontenerami i otwierają swoją zawartość zamiast udawać odtwarzanie w tle.

## FLAC i oryginalny strumień w alpha 146

Format nagrywania Radia obejmuje teraz również **FLAC, bezstratny** oraz **Oryginalny strumień, bez konwersji**. FLAC otrzymuje zdekodowany dźwięk PCM używany przez AMC i koduje go bezstratnie. Format oryginalny otwiera osobne, niesłyszalne połączenie i kopiuje skompresowane pakiety bez ponownego kodowania. Bezpośredni MP3 pozostaje MP3, AAC trafia do AAC, OGG lub Opus do OGG, a FLAC do FLAC. Nieznany kodek otrzymuje bezpieczny kontener audio Matroska.

HLS nie jest jednym gotowym plikiem, dlatego AMC nie zapisuje manifestu M3U8. Segmenty audio są łączone i bez transkodowania przepakowywane do kontenera transportowego TS. Jest to zapis bez zmiany kodeka, ale nie kopia bajt po bajcie manifestu i segmentów. Metadane ICY nie są wstawiane pomiędzy ramki dźwięku. FLAC i format oryginalny wymagają FFmpeg; brak komponentu daje czytelny błąd i nie pozostawia pliku udającego gotowe nagranie. Bitrate można wybierać tylko dla MP3 i AAC.

## Foldery nagrań w alpha 147

Karta `Ustawienia > Radio i nagrywanie` ma jeden, jednoznaczny **Domyślny folder nagrywania**. Pole pokazuje pełną zapamiętaną ścieżkę, a przycisk **Wybierz…** otwiera standardowe okno wyboru folderu. Nie trzeba wcześniej przełączać przycisku opcji. Przy pierwszym uruchomieniu dawna pusta wartość jest przedstawiana jako rzeczywista ścieżka `Muzyka\AMC — Nagrania radia`; zapis ustawień utrwala ją jako jawny wybór użytkownika.

Każdy harmonogram ma osobne pole wyboru z dwiema wartościami: **Domyślny folder nagrywania** i **Inny folder**. Druga wartość udostępnia pole ścieżki oraz przycisk **Wybierz…** tylko dla tego planu. Pusta wartość zapisana w starszym planie nadal oznacza dziedziczenie ustawienia ogólnego, a istniejący własny folder nie jest tracony. Zmiana folderu domyślnego wpływa na plany dziedziczące, lecz nie zmienia planów z własnym folderem.

## Dostępna edycja harmonogramu w alpha 148

Siedem osobnych pól wyboru dni zastąpiła jedna lista. Strzałki przechodzą od poniedziałku do niedzieli, a Spacja zaznacza lub odznacza bieżący dzień i podaje jego nowy stan. Lista jest aktywna tylko przy powtarzaniu **W wybrane dni tygodnia**. Każdy wiersz ma jawną nazwę w rodzaju „Wtorek, zaznaczony”, więc UI Automation nie udostępnia technicznej reprezentacji obiektu.

Lista harmonogramów pozwala przełączać zaznaczony plan Spacją albo przyciskiem **Włącz lub wyłącz**. Wyłączony plan pozostaje zapisany, ale nie uruchamia nagrań ani wybudzania. Zmiana zaczyna obowiązywać po wybraniu **Zapisz**; anulowanie okna pozostawia poprzedni stan. Aktywne nagranie wyłączone na liście zostanie zatrzymane przy zapisaniu zmian.

Opcja szybkiego `Shift+R` brzmi teraz **Rozpocznij pierwsze nagranie od razu po zapisaniu**. Gdy jest zaznaczona, data i godzina pierwszego startu są pomijane, a plan zostaje włączony. Plan jednorazowy wykona się raz; cykliczny po pierwszym natychmiastowym zapisie wyznaczy kolejne wystąpienia o godzinie rzeczywistego pierwszego startu. Po odznaczeniu pierwsze nagranie korzysta z wpisanej daty i godziny. Pole folderu planu zawiera wartości **Domyślny folder nagrywania** oraz **Folder użytkownika**; tylko druga udostępnia przycisk wyboru ścieżki.

## Termin i fokus harmonogramu w alpha 149

Nowy plan ma jednoznaczne pole **Pierwsze nagranie** z wartościami **Od razu po zapisaniu** oraz **W wybranym terminie**. Druga wartość udostępnia natywne pola daty i godziny Windows. W dacie lewa i prawa strzałka wybierają dzień, miesiąc albo rok, a góra i dół zmieniają wskazaną część. Godzina działa analogicznie dla godziny i minut. Długość nagrania jest polem liczbowym od 1 do 10080 minut, więc nieprawidłowy tekst nie może zostać zapisany jako termin.

Tab przechodzi po formularzu w kolejności użytkowej. Escape działa również wewnątrz natywnych pól i anuluje edycję, a Enter zapisuje plan. Nowy plan rozpoczyna fokus od stacji, edycja istniejącego od daty, a po dodaniu, zmianie lub usunięciu fokus wraca na listę harmonogramów.

Pełna kopia `*.amcbackup.json` już obejmuje Bibliotekę i Ulubione Radia oraz harmonogramy. Plan osobnego, bezpiecznego eksportu do przesyłania innym osobom opisuje [`PROJEKT_IMPORTU_EKSPORTU_RADIA.md`](PROJEKT_IMPORTU_EKSPORTU_RADIA.md); osobne przyciski importu i eksportu nie są jeszcze częścią alpha 149.

## Nazwy rozpoczęcia i wybudzania w alpha 150

Pole nowego harmonogramu brzmi teraz zwięźle **Nagrywaj** i zawiera wartości **Natychmiast** oraz **Później**. „Natychmiast” oznacza rozpoczęcie po wybraniu Zapisz; „Później” uaktywnia datę i godzinę. Znika techniczne sformułowanie „po zapisaniu” z samej wartości pola. Pole wybudzania ma konsekwentną nazwę **Wybudzanie komputera dla tego harmonogramu**.

## Oznajmianie segmentów daty i godziny w alpha 151

Osadzone pola daty i godziny jawnie wysyłają do NVDA wybraną część oraz jej bieżącą wartość. Lewo i prawo podaje odpowiednio dzień, miesiąc, rok albo godzinę i minuty. Góra i dół po zmianie podaje nową wartość tej samej części, na przykład „Miesiąc: 8, sierpień” lub „Minuty: 35”. Nie trzeba używać `NVDA+strzałka w górę`, a data i godzina nadal zajmują po jednym punkcie Tab.

## Krótkie wartości daty i godziny w alpha 152

Lewo i prawo nadal określa wybraną część, na przykład „Minuty: 45”. Podczas właściwej zmiany górą lub dołem AMC mówi już tylko nową wartość: „46”, „47” — bez powtarzania słowa „Minuty”, „Dzień”, „Miesiąc”, „Rok” albo „Godzina”.

## Niezależne nagrania i widok Nagrywane w alpha 153

Ręczne nagranie Radia korzysta z osobnego, niesłyszalnego połączenia. `R` steruje bieżącą stacją w odtwarzaczu, `Ctrl+Alt+R` działa na zaznaczonej stacji listy, a Page Up, Page Down, Enter i presety zmieniają odsłuch bez kończenia trwającego zapisu. Różne stacje mogą być nagrywane równolegle. `Alt+2` w sesji Radia pokazuje widok **Nagrywane** z ręcznymi nagraniami i aktywnymi harmonogramami; `R` zatrzymuje tam wybrane nagranie ręczne. Powrót Escape z odtwarzacza jawnie kotwiczy fokus na liście. Tryb Oryginalny pozwala FFmpeg samodzielnie wybrać ścieżkę audio, dzięki czemu bezpośredni strumień ICY bez wczesnego indeksu `0:a:0` nie jest odrzucany.

Od `alpha.202` bezstratne usuwanie fragmentu przez `Ctrl+X` sprawdza rzeczywistą oś czasu pakietów FFmpeg zarówno przed operacją, jak i po niej. Jest to istotne dla długich nagrań MP3 bez wiarygodnego nagłówka Xing: czas szacowany przez Windows może być krótszy od zawartości o kilkanaście sekund. AMC zachowuje cały koniec pliku, kontroluje wynik i dopiero potem atomowo podmienia oryginał, nadal pozostawiając pełną kopię `.amc-backup`.

## Przewidywalny bitrate niskich częstotliwości w alpha 154

Stacje dekodowane jako 22,05 albo 24 kHz nie są już po cichu zapisywane przez systemowy koder jako MP3 80 kb/s mimo wybrania 128 kb/s. Tylko przed kodowaniem stratnym MP3 lub AAC AMC normalizuje taki sygnał wysokiej jakości resamplerem do odpowiednio 44,1 albo 48 kHz, dzięki czemu wynik zachowuje wybrany bitrate. Nie dodaje to szczegółów nieobecnych w źródle. FLAC, WAV i format Oryginalny nadal zachowują częstotliwość źródłową. W ustawieniach format Oryginalny jawnie informuje, że HLS tworzy plik `.ts`.

## Odporny odczyt TS i odzyskiwanie fragmentów w alpha 155

Lokalne `.ts`, `.mts` i `.m2ts` korzystają z odpornego dekodera FFmpeg, który pobiera wyłącznie ścieżkę audio. Dzięki temu AMC odtwarza również transmisje zapisane od środka segmentu obrazu, które Media Foundation odrzuca. Przewijanie pozostaje dostępne. `Ctrl+O` ma osobny filtr **Niedokończone nagrania do odzyskania** dla `.part`, `.partial` i `.amc-partial`; AMC próbuje odtworzyć dostępną część i kończy na jej rzeczywistym końcu. Takie pliki nie są automatycznie indeksowane z folderów, ponieważ mogą być nadal zapisywane lub niekompletne.

## Bezpieczne kończenie nagrań w alpha 156

Od `alpha.166` `Ctrl+Alt+Shift+R` zatrzymuje wszystkie aktualne nagrania ręczne i harmonogramowe niezależnie od otwartej sesji. Dla jednego nagrania działa od razu; przy kilku wymaga jawnego potwierdzenia z domyślną odpowiedzią Nie. Każdy odebrany fragment jest finalizowany, a harmonogram cykliczny zachowuje dopiero następny termin. Zamknięcie AMC podczas nagrywania również podaje liczbę aktywnych zapisów i wymaga potwierdzenia. Nagrania ręczne nie są wznawiane po kolejnym uruchomieniu. Jeśli jednak nadal trwa czas bieżącego wystąpienia harmonogramu, AMC rozpoczyna nowy plik i nagrywa jego pozostałą część; zakończonego okna nie odtwarza po czasie.

Od `alpha.157` `Shift+Spacja` wstrzymuje lub wznawia nagranie stacji wybranej na liście albo otwartej w odtwarzaczu. Zwykła Spacja pozostaje pauzą odsłuchu. MP3, M4A/AAC, FLAC i WAV pomijają dźwięk odebrany podczas pauzy, a każde wstrzymanie tworzy zakładkę AMC w ukończonym nagraniu. Tryb Oryginalny nie udostępnia pauzy, ponieważ zachowanie niezmienionych pakietów wymagałoby dzielenia i ponownego łączenia kontenerów. Importowany PLS, M3U lub XSPF jest przed oryginalnym zapisem rozwiązywany do bezpośredniego strumienia; prawdziwy HLS pozostaje manifestem.

Od `alpha.158` rozwiązywanie list sieciowych obejmuje także zagnieżdżone PLS, M3U, M3U8 i XSPF, adresy względne oraz przekierowanie z adresu playlisty bezpośrednio do audio. Pętla lub więcej niż cztery poziomy list są bezpiecznie odrzucane. Strumień rozpoznany przez typ odpowiedzi jako audio nie jest pobierany i błędnie analizowany jak tekst, a prawdziwy manifest HLS nadal trafia bezpośrednio do dekodera. Obejmuje to zapis Oryginalny Tyflo Podcastu z adresu `listen.pls` i inne stacje korzystające z takich samych opakowań.

Od `alpha.159` `Ctrl+M` wycisza albo przywraca odsłuch bieżącej sesji, a `Ctrl+Shift+M` wszystkie sesje AMC, również te grające w tle lub uruchomione później. Są to dwie niezależne warstwy: wyłączenie wyciszenia globalnego nie przywraca sesji wyciszonej wcześniej osobno. Regulacja głośności zdejmuje wyciszenie indywidualne, ale nie globalne. Skróty nie zmieniają głośności Windows, NVDA ani innych aplikacji i nie przerywają nagrywania. Wyciszenie nie jest zapisywane między uruchomieniami, natomiast ustawione wartości głośności pozostają zachowane.

Od `alpha.160` `Ctrl+Shift+H` otwiera pełną listę harmonogramów nagrywania w Radiu. Jest to podstawowy skrót pokazywany w menu, palecie i pomocy. Od `alpha.166` dawny alias `Ctrl+Alt+Shift+R` oznacza zatrzymanie wszystkich nagrań, dlatego harmonogram otwiera wyłącznie `Ctrl+Shift+H`. Wywołanie skrótu poza Radiem nie przełącza sesji, lecz podaje krótki komunikat o dostępności funkcji.

Od `alpha.161` nazwa stacji jest pierwszą informacją odczytywaną na listach i pasku stanu Radia. Dopiero po niej AMC podaje stany, na przykład „Radio 357, nagrywanie”, „Radio 357, nagrywanie wstrzymane, odtwarzany” albo „Radio 357, wstrzymany”. Dzięki temu użytkownik najpierw rozpoznaje stację, której dotyczą kolejne informacje.

Od `alpha.162` klawisz `T` w odtwarzaczu Radia lub widoku **Nagrywane** zamyka bieżący plik ręcznego nagrania i kontynuuje zapis tej samej stacji w nowym pliku. `Shift+R` ma pole **Sposób zapisu** z wartościami **Jeden plik** i **Dziel na części**; druga wartość udostępnia liczbę minut jednej części. Całkowita długość planu pozostaje wspólnym terminem końcowym, więc dwugodzinny plan dzielony co 15 minut tworzy osiem części, a spóźnione wznowienie nagrywa wyłącznie pozostały czas.

Od `alpha.163` HLS rozpoczyna się przy krawędzi transmisji i jest dekodowany w tempie czasu rzeczywistego, więc krótki zapis nie wciąga wcześniejszych segmentów manifestu. MP3, AAC, FLAC, WAV i Oryginalny powstają najpierw w lokalnym folderze roboczym poza iCloud, OneDrive i Dyskiem Google. Dopiero zamknięty plik jest kopiowany pod nazwą `.amc-publishing` i atomowo otrzymuje nazwę końcową; nieudana publikacja zachowuje lokalną kopię odzyskiwania. Żądanie zatrzymania ma pierwszeństwo przed równoczesnym podziałem i nie może uruchomić pustej kolejnej części.

Ta wersja dodaje opcjonalne rozpoznawanie muzyki w Radiu. `S` w odtwarzaczu rozpoznaje aktualnie słyszany fragment z bufora timeshift, bez drugiego połączenia ze stacją. `Shift+S` włącza lub wyłącza obserwowanie, a `Ctrl+Alt+S` otwiera trwałą historię od najnowszego. Powtórzenie tego samego utworu na tej samej stacji w ciągu 30 minut nie tworzy kolejnego wpisu. Historia przechowuje stację, tytuł, wykonawcę, album, datę wydania i czas rozpoznania; można ją zaznaczać, kopiować, usuwać i eksportować do JSON albo CSV. Eksport zawiera wyszukiwania Apple Music, Spotify i Tidal, lecz dokładne dopasowanie identyfikatorów będzie należało do przyszłych oficjalnych adapterów tych usług. Do dostawcy rozpoznawania wysyłany jest podpis akustyczny, nie nagranie ani adres stacji. Od `alpha.180` stan obserwowania jest trwały i ma również pole wyboru w ustawieniach Radia.

Od `alpha.164` bogate kopiowanie i eksport rozpoznanych utworów zawierają także YouTube Music oraz katalogowe wyszukiwania Discogs i MusicBrainz. Są to jawne adresy wyszukiwania, a nie automatyczne twierdzenie, że znaleziono właściwe wydanie. Projekt późniejszego, kontrolowanego dopasowania albumów i autorów znajduje się w [`PROJEKT_METADANYCH_I_AUTOROW_PL.md`](PROJEKT_METADANYCH_I_AUTOROW_PL.md).

## Podcasty: subskrypcje alpha 205 i odtwarzanie alpha 206–208

Podcasty są od tej wersji prawdziwą, szóstą sesją AMC i nie zawierają danych
demonstracyjnych. Sesja ma osobną Bibliotekę oraz dostępne z menu Widok puste
kontenery **Nowe odcinki** i **Pobrane**. Jej miejsce można zmienić razem z
pozostałymi sesjami w Ustawieniach, a ostatni widok jest zapamiętywany.

Rdzeń przechowuje już subskrypcje, odcinki, źródłowy identyfikator, adres
materiału i strony, datę, czas, stan nowy/odsłuchany, lokalne pobranie, pozycję,
prędkość, Ulubione i Kolejkę. Parser przyjmuje RSS 2.0 i Atom, obsługuje
`enclosure`, względne adresy oraz czas iTunes, pomija wpisy bez audio i tworzy
stabilne identyfikatory. XML jest traktowany jako niezaufany: DTD i encje
zewnętrzne są zablokowane, a rozmiar dokumentu i liczba odcinków mają granice.
`alpha.205` dodaje `Ctrl+N` dla bezpośredniego RSS/Atom, `Ctrl+O` dla importu
OPML, ograniczone odświeżanie HTTP/HTTPS przez `F5` i `Ctrl+F5` oraz wejście z
audycji do odcinków. Aktualizacja pobiera wyłącznie metadane i nigdy nie pobiera
automatycznie plików audio. `alpha.206` odtwarza odcinki z bezpośrednich
adresów HTTP/HTTPS, zapisuje pozycję i prędkość per odcinek oraz łączy je ze
wspólną Historią, Zakładkami, Kolejką i Playlistami AMC. `Ctrl+F` w następnym etapie katalogowym najpierw
wyszuka audycje, a Enter pokaże ich odcinki bez automatycznego subskrybowania.
Zakres wejściowy obejmie następnie publiczne wyszukiwanie Apple Podcasts,
odkrywanie RSS na zwykłej stronie, osadzone audio i osobne adaptery wydawców,
w tym Polskiego Radia. Wykorzystamy reguły przygotowane wcześniej w rozszerzeniu
Chrome i dodatku NVDA do konwersji, ale bez uzależnienia rdzenia AMC od ich
interfejsów. `Ctrl+I` otworzy automatyczną skrzynkę **Nowe odcinki**, `Ctrl+D`
pobierze świadomie wybrane odcinki, a Playlisty pozostaną ręcznymi zestawami i
nie będą się same zmieniać po odświeżeniu kanału. Odtwarzanie odcinków przez
HTTP działa od `alpha.206`. Korekta `alpha.208` umożliwia bezpieczne przewijanie
sieciowego odcinka strzałkami, skok wpisanym czasem i cyframi procentowymi bez
wywoływania dekodera Media Foundation z niewłaściwego wątku. Bieżący odcinek
pozostaje widoczny w Kolejce przez cały czas odtwarzania i znika dopiero po
zakończeniu albo ręcznym przejściu dalej. Wiersze Podcastów zawsze zaczynają się
od tytułu audycji lub odcinka, niezależnie od ogólnej kolejności pól dla muzyki.
`F2` na audycji w Bibliotece ustawia trwałą nazwę własną AMC, której odświeżenie
RSS nie nadpisuje. Pełny podział etapów znajduje
się w [`PROJEKT_PODCASTOW_PL.md`](PROJEKT_PODCASTOW_PL.md).

## Urządzenie audio osobno dla sesji w alpha 204

Klawisz **Shift+A**, menu **Odtwarzanie > Wybierz urządzenie audio dla bieżącej
sesji…** oraz menu kontekstowe odtwarzacza pokazują rzeczywiste, aktywne wyjścia
Windows. Wybór
jest zapisywany niezależnie dla Plików lokalnych, Radia internetowego i
Podcastów. Po zmianie aktywny odsłuch zostaje uruchomiony ponownie na nowym
wyjściu, bez zmiany Kolejki, Historii ani bieżącego elementu. W Radiu osobne
nagrania działające w tle pozostają niezależne i nie są zatrzymywane.

AMC nadal używa współdzielonego WASAPI, aby NVDA i inne aplikacje zachowały
dźwięk. Można wybrać **Domyślne urządzenie systemowe**, które podąża za zmianą
ustawienia Windows, albo zapamiętać konkretne urządzenie dla sesji. Gdy
zapamiętane wyjście jest chwilowo odłączone, AMC bezpiecznie korzysta z
urządzenia domyślnego, nie usuwa preferencji i ponownie użyje wybranego
urządzenia po jego powrocie. Dostępne etykiety zawierają tylko nazwy użytkowe;
identyfikatory techniczne urządzeń nie są przekazywane do NVDA. `Alt+Enter`
podaje skuteczne wyjście sesji. Indywidualne urządzenie dla pojedynczego pliku,
folderu lub stacji pozostaje etapem późniejszym.

Poprawka `alpha.205` zachowuje także ostatnie żądanie Spacji podczas
asynchronicznego przełączania toru. Jeżeli wolniejsze urządzenie zewnętrzne
jeszcze się uruchamia, polecenie wstrzymania nie ginie i nowy tor pozostaje
wstrzymany. Ta sama ochrona obejmuje Radio, Podcasty i Pliki lokalne.

`Shift+A` dotyczy bieżącej sesji, a nie wszystkich torów dźwięku naraz. Jest
dostępne w każdym adapterze, który rzeczywiście odtwarza dźwięk przez AMC.
Obecnie są to Pliki lokalne, Radio internetowe i Podcasty. W przyszłej sesji
urządzenia, takiej jak WiiM, ten sam skrót otworzy wybór celu odtwarzania
udostępniony przez adapter, zamiast pozorować wybór karty dźwiękowej Windows.

## Zakres i ograniczenia

- TIDAL, Apple Music i WiiM są obecnie sesjami demonstracyjnymi. Pliki lokalne odtwarzają prawdziwe multimedia i trwale zapisują katalog, a Radio internetowe wyszukuje oraz odtwarza prawdziwe publiczne strumienie i trwale zapisuje własną Bibliotekę oraz Ulubione.
- Publiczne strony stacji, podcastów i odcinków można otwierać w przeglądarce;
  integracje z oficjalnymi aplikacjami kontowymi pozostają etapem późniejszym.
- Pobieranie muzyki i obsługa DRM nie są jeszcze zaimplementowane; skróty `D` i `Shift+D` tylko podają komunikaty.
- Aktualizator nie pobiera jeszcze pakietów.
- Pierwszym celem jest Windows. macOS, VoiceOver i Siri pozostają etapem późniejszym.
- `Ctrl+Alt+Windows+F12` jest prefiksem prototypu; został pomyślnie zarejestrowany na komputerze testowym, ale kombinacje z Windows należy sprawdzać na każdym docelowym komputerze.

## Struktura

- `src/AccessibleMediaController.Core` — polecenia, profile, konfiguracja, sesje i interfejs aktualizacji;
- `src/AccessibleMediaController.Windows` — WPF, UI Automation, globalny prefiks i dostępne okna;
- `tests/AccessibleMediaController.Core.SmokeTests` — proste testy logiki bez zewnętrznych pakietów;
- `tests/accessibility/nvda` — opcjonalny, chroniony klient smoke testu NVDA;
- `MEDIA_CONTROLLER_PL.md` i `MEDIA_CONTROLLER_EN.md` — pełna specyfikacja koncepcji.
