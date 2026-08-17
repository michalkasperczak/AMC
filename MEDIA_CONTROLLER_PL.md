# Dostępny kontroler multimedialny — koncepcja projektu

Wersja dokumentu: 0.5, aktualny plan projektu

Data aktualizacji: 13 sierpnia 2026 r.

## 1. Cel projektu

Projekt zakłada stworzenie dostępnego kontrolera multimediów z jednym wspólnym rdzeniem poleceń, sesji i usług oraz kilkoma równorzędnymi sposobami obsługi. Program ma działać zarówno przez klasyczne okno, jak i przez konfigurowalny prefiks klawiaturowy używany bez przechodzenia do okna.

Pierwsze planowane integracje:

- TIDAL;
- Apple Music;
- WiiM.

W dalszej kolejności planowane są Spotify, Sonos, Bluesound/BluOS oraz urządzenia korzystające z platformy Frontier Smart. Architektura ma również przewidywać radio internetowe i lokalne multimedia. Standardowe odtwarzanie lokalne nie będzie wymagać foobar2000; później może powstać jego opcjonalny adapter dla wyspecjalizowanych formatów, DSP lub urządzeń. Rozwiązania Free Radio mogą posłużyć jako materiał i mechanizm odniesienia po sprawdzeniu kodu oraz licencji.

Program nie będzie zależny od NVDA. Pierwsza wersja okna powstaje dla Windows w WPF i ma działać przez UI Automation z NVDA, JAWS-em oraz Narratorem. Opcjonalna wtyczka NVDA zostanie zbudowana później jako mały klient, a nie jako miejsce wykonywania logiki usług.

Docelowa wersja dla macOS otrzyma natywny interfejs Swift/AppKit korzystający z NSAccessibility i VoiceOver. Wspólna logika pozostanie w wieloplatformowym rdzeniu .NET, udostępnianym interfejsom przez stabilną granicę poleceń i zdarzeń. Skróty i globalny prefiks będą miały osobną implementację właściwą dla danego systemu.

## 2. Model hybrydowy

Aplikacja będzie połączeniem trzech sposobów obsługi:

1. **Warstwa prefiksowa** do szybkich operacji wykonywanych w tle.
2. **Proste dostępne okna** do przeglądania wyników wyszukiwania, albumów, playlist, biblioteki i ustawień.
3. **Opcjonalne cienkie integracje**, przede wszystkim wtyczka NVDA, korzystające z tego samego uruchomionego rdzenia.

Nie planujemy na początku rozbudowanego, stale otwartego interfejsu. Złożone dane muszą jednak być prezentowane w normalnym oknie, ponieważ nie da się wygodnie przejrzeć kilkudziesięciu albumów lub playlist samymi komunikatami głosowymi.

Każda operacja aplikacji będzie wewnętrznym poleceniem, które można wywołać:

- w warstwie prefiksowej;
- lokalnym skrótem w oknie;
- z menu kontekstowego;
- w przyszłości przez opcjonalną wtyczkę NVDA, natywny interfejs macOS lub zewnętrzny interfejs sterowania.

Wtyczka NVDA nie zawiera adapterów usług, bibliotek odtwarzania ani tokenów. Wysyła krótkie polecenia do lokalnego procesu AMC i odbiera zdarzenia oraz tekst komunikatów. Nie jest to wywoływanie terminala. Na Windows komunikacja będzie odbywać się przez lokalny named pipe, a na macOS przez lokalny mechanizm właściwy dla systemu, np. Unix domain socket lub natywny most.

Rdzeń działa tylko wtedy, gdy jest potrzebny. Może uruchomić się wraz z oknem albo na żądanie wtyczki, działać bez widocznego okna w zasobniku i zakończyć się na wyraźne polecenie użytkownika. Automatyczny start z systemem pozostaje ustawieniem opcjonalnym.

Na macOS normalne, dostępne okno pozostaje obowiązkowe, a prefiks jest jedynie dodatkową drogą sterowania. Zewnętrzne narzędzia automatyzacji, np. Keyboard Maestro, mogą w przyszłości korzystać z publicznego polecenia lub adresu `amc://`, ale nie są wymagane do działania programu.

## 3. Prefiks aplikacji

### 3.1. Zasada działania

1. Użytkownik naciska globalny prefiks.
2. Aplikacja przechodzi na krótko do warstwy poleceń.
3. Następny klawisz lub kombinacja zostaje zinterpretowana przez aplikację.
4. `Escape` anuluje warstwę.
5. Warstwa wygasa po konfigurowalnym czasie, proponowane domyślnie 3 sekundy.

Ponowne naciśnięcie samego prefiksu może informować o bieżącej sesji, np. „TIDAL”. To zachowanie również powinno być konfigurowalne.

### 3.2. Wybór prefiksu

Prefiks pozostaje konfigurowalny. Obecny prototyp używa `Ctrl+Alt+Windows+F12`, ale nie jest to wybór docelowy. Implementacja docelowa dla Windows ma wykrywać prefiks przez niskopoziomowe, fizyczne przechwycenie klawiatury, a nie oferować użytkownikowi wybór między kilkoma technicznymi trybami rejestracji. Pozwala to rozróżnić zwykły `Enter` od `Numerycznego Entera` na podstawie kodu skanowania i znacznika klawisza rozszerzonego oraz jednakowo obsługiwać stan Num Lock.

Użytkownik może przypisać dowolną obsługiwaną kombinację fizycznych klawiszy. Pusta wartość wyłącza globalny prefiks bez wyłączania skrótów działających w aktywnym oknie. Po wykryciu skonfigurowanej kombinacji AMC zatrzymuje jej zdarzenia, aby nie uruchamiała równocześnie polecenia w programie znajdującym się pod fokusem. Program musi jednak wykrywać i jasno zgłaszać przypadki, w których system, program działający z wyższymi uprawnieniami albo czytnik ekranu przejął kombinację wcześniej.

Głównymi kandydatami na przyszłą wartość domyślną są `Ctrl+Numeryczny Enter` oraz sam `Numeryczny Enter`. Pierwszy wariant mniej ingeruje w zwykłe zatwierdzanie w innych programach; drugi jest szybszy, lecz globalnie odbiera numerycznemu Enterowi jego standardowe znaczenie. Ostateczny wybór wymaga testów z NVDA, JAWS-em, Ditto i popularnymi menedżerami schowka. Rozważane możliwości:

| Kandydat | Zalety | Ryzyko |
| --- | --- | --- |
| `Ctrl+Numeryczny Enter` | krótki, fizycznie charakterystyczny i mniej inwazyjny | wymaga niskopoziomowego rozróżnienia obu Enterów |
| `Numeryczny Enter` | bardzo szybki i wygodny jedną ręką | przejmuje standardowe zatwierdzanie tym klawiszem we wszystkich programach |
| `Ctrl+Alt+Windows+F12` | rozpoznawalny i bez konfliktu na komputerze testowym | cztery klawisze; wymaga testu rejestracji |
| `Ctrl+Alt+Spacja` | stosunkowo krótki | możliwy konflikt z innymi aplikacjami lub metodami wprowadzania |
| `Ctrl+Shift+Windows+Spacja` | wyraźnie odróżnia aplikację od Free Radia | długi; kombinacje z Windows mogą być zarezerwowane przez system |
| `Ctrl+Windows+\` | krótki i charakterystyczny | kombinacje z Windows wymagają testu rejestracji |
| `Ctrl+Shift+Windows+P` | łatwe skojarzenie z prefiksem | cztery klawisze |
| `F13–F24` | bardzo małe ryzyko konfliktu | wymaga klawiatury programowalnej lub mapowania dodatkowego klawisza |

Ustawienia powinny zawierać funkcję „Sprawdź prefiks”. Program zapisze kombinację dopiero po udanym teście fizycznego rozpoznania i ostrzeże, jeżeli skrót jest już zajęty lub nie dociera do AMC.

Microsoft zastrzega, że skróty zawierające klawisz Windows są przeznaczone dla systemu operacyjnego, dlatego nie wolno zakładać, że każda taka kombinacja będzie dostępna. Program musi sprawdzać ją na konkretnym komputerze: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey>.

## 4. Sesje

### 4.1. Wybieranie sesji

Po prefiksie same cyfry będą bezpośrednio przełączać sesję. W aktywnym oknie odpowiadają im skróty `Ctrl+cyfra`. Kolejność sesji jest konfigurowalna. Cyfry nie zależą od języka i nie zajmują liter potrzebnych do poleceń.

Proponowane ustawienia domyślne:

| Polecenie po prefiksie | Sesja lub działanie |
| --- | --- |
| `1` | TIDAL |
| `2` | Apple Music |
| `3` | WiiM |
| `4–9` | kolejne usługi lub urządzenia |
| `0` | lista wszystkich sesji |
| `Page Up` | poprzednia dostępna sesja |
| `Page Down` | następna dostępna sesja |

Po zmianie aplikacja przekazuje krótki komunikat, np. „3, WiiM”. Jeśli miejsce nie zostało przypisane, mówi „Sesja 4 nieprzypisana”. W aktywnym oknie `Ctrl+1–9` wybiera sesję, `Ctrl+0` otwiera ich listę, a `Ctrl+Page Up` i `Ctrl+Page Down` przechodzą do poprzedniej i następnej sesji.

### 4.2. Zapamiętywanie sesji

- Wybrana sesja pozostaje aktywna dla następnych poleceń.
- Nie trzeba wskazywać usługi za każdym razem.
- Po przełączeniu sesji warstwa może pozostać aktywna jeszcze przez około 2 sekundy, aby można było od razu wykonać funkcję.

Przykłady:

- prefiks, `1` — zmień sesję na TIDAL;
- prefiks, `F` — wyszukaj w aktualnej sesji, czyli w TIDAL-u;
- prefiks, `3`, `Spacja` — zmień sesję na WiiM i włącz albo zatrzymaj odtwarzanie;
- prefiks, `2`, `P` — zmień sesję na Apple Music i otwórz playlisty.

Do rozstrzygnięcia pozostaje, czy ostatnia sesja ma być pamiętana po ponownym uruchomieniu programu. Bezpieczniejszy wariant to przywrócenie sesji i ogłoszenie jej przy pierwszym użyciu prefiksu.

## 5. Polecenia literowe

### 5.1. Ogólna reguła

- w aktywnym oknie podstawowym skrótem polecenia jest zwykle `Ctrl+litera`;
- po prefiksie odpowiada mu ta sama litera bez `Ctrl`;
- `Shift` oznacza działanie powiązane, rozszerzony zakres albo czynność dotyczącą aktualnego elementu;
- nie każda litera musi od razu mieć wersję z Shiftem;
- wszystkie przypisania można zmienić w ustawieniach.

Wersja z Shiftem nie powinna wykonywać nieodwracalnej operacji bez potwierdzenia.

Prefiks zastępuje więc `Ctrl`, zamiast wymagać po sobie kolejnej kombinacji z `Ctrl`. Pozostawia to kombinacje takie jak `Ctrl+E`, `Ctrl+R` i `Ctrl+T` wewnątrz warstwy dla dodatkowych poleceń informacyjnych. Obsługa okna i prefiksu wywołuje te same identyfikatory poleceń w rdzeniu, więc nie powiela logiki funkcji.

Program może zmieniać język interfejsu i komunikatów, ale nie powinien automatycznie zmieniać przypisań klawiszy. Domyślny zestaw pozostaje stabilny, a użytkownik może zbudować własny profil. Dopuszczamy czytelne polskie skojarzenie `U` — Ulubione obok utrwalonego `L` — Library/Biblioteka; ważniejsze od językowej czystości są brak konfliktów i pamięć mięśniowa.

### 5.2. Zatwierdzona mapa podstawowa

| Funkcja | Skrót w oknie | Klawisz po prefiksie |
| --- | --- | --- |
| Biblioteka | `Ctrl+L` | `L` |
| dodaj do biblioteki albo usuń z niej | `Ctrl+Shift+L` | `Shift+L` |
| Ulubione | `Ctrl+U` | `U` |
| dodaj do Ulubionych albo usuń z nich | `Ctrl+Shift+U` | `Shift+U` |
| Playlisty | `Ctrl+P` | `P` |
| wybór playlist i zmiana przynależności | `Ctrl+Shift+P` | `Shift+P` |
| Kolejka | `Ctrl+Q` | `Q` |
| dodaj do kolejki | `Ctrl+Shift+Q` | `Shift+Q` |
| filtr aktualnie załadowanej listy | `Ctrl+K` | `K` |
| paleta wszystkich poleceń AMC | `Ctrl+Shift+K` | `Shift+K` |
| wyszukiwanie w bieżącej usłudze lub źródle | `Ctrl+F` | `F` |
| wyszukiwanie we wszystkich włączonych usługach i źródłach | `Ctrl+Shift+F` | `Shift+F` |
| pobierz lub zachowaj wewnątrz usługi | `Ctrl+D` | `D` |
| pobierz na dysk, jeśli zezwala na to usługa | `Ctrl+Shift+D` | `Shift+D` |
| Albumy | `Ctrl+Shift+A` | `A` |

Filtr działa tylko na danych już znajdujących się w bieżącej liście i nie wysyła zapytania do usługi. Wyszukiwanie bieżące może odpytać aktualną usługę, a wyszukiwanie globalne odpytuje wszystkie włączone źródła, które zezwalają na wspólną prezentację. Adapter może wymagać osobnego widoku wyników; dotyczy to między innymi prawdziwego adaptera TIDAL, którego treści nie wolno mieszać na jednej liście z treściami podobnych usług. Paleta poleceń jest dostępną, filtrowalną listą funkcji, także tych bez przypisanego skrótu.

`Shift+U` działa jako przełącznik tylko wtedy, gdy adapter potrafi pewnie odczytać aktualny stan. Program mówi odpowiednio „Dodano do ulubionych” albo „Usunięto z ulubionych”. Jeśli stan jest nieznany, aplikacja nie może zgadywać i powinna otworzyć menu z jednoznacznymi czynnościami.

Albumy są ważnym i często używanym widokiem, dlatego otrzymują skrót. Jest to świadomy wyjątek od pełnej symetrii: po prefiksie używamy prostego `A`, a w oknie `Ctrl+Shift+A`, ponieważ `Ctrl+A` bezwzględnie zachowuje standardowe „Zaznacz wszystko”. Nie używamy `Ctrl+Alt+A`, gdyż `Ctrl+Alt` może odpowiadać AltGr i kolidować z wpisywaniem polskiego znaku „ą”. `L` pozostaje Biblioteką, a `B` rezerwą dla możliwych przyszłych Zakładek/Bookmarks. `Shift+A` po prefiksie pozostaje na razie nieprzypisane.

Pozostałe zatwierdzone wcześniej polecenia warstwy zachowują litery `R` — Radio, `M` — Miksy, `H` — Historia, `N` — Teraz odtwarzane, `I` — Informacje i `O` — Wyjścia. Ich odpowiedniki okienne mają docelowo używać `Ctrl` oraz tej samej litery, o ile nie narusza to standardowego działania pola tekstowego lub systemu. Każdy konflikt rozstrzyga edytor mapy, a polecenie może pozostać bez skrótu i być dostępne z menu oraz palety.

Funkcje pobierania nie należą do podstawowej wersji. Są aktywowane osobno dla każdego adaptera dopiero po sprawdzeniu oficjalnych możliwości, licencji i zasad danej usługi. Pobieranie na dysk jest eksperymentalne i domyślnie wyłączone.

### 5.3. Informacje o czasie

Informacje o czasie pozostają trzema osobnymi poleceniami, żeby użytkownik nie musiał słuchać niepotrzebnych danych:

| Polecenie po prefiksie | Informacja domyślna |
| --- | --- |
| `Ctrl+E` | czas, który upłynął, np. „1:23” |
| `Ctrl+R` | czas pozostały, np. „2:57” |
| `Ctrl+T` | całkowity czas elementu, np. „4:20” |

Są to polecenia informacyjne, dlatego używają Ctrl i nie kolidują z literą `R` uruchamiającą widok radia. Domyślnie wypowiadana jest tylko wartość. Każde ponowne wywołanie podaje wartość aktualną; program nie powinien co sekundę przerywać mową. Monitor brajlowski i dostępne pole stanu mogą pokazywać aktualizowany czas bez automatycznej wypowiedzi.

## 6. Polecenia transportowe i strzałki

Propozycja dla warstwy prefiksowej:

| Polecenie po prefiksie | Działanie |
| --- | --- |
| `Spacja` | odtwórz / pauza |
| `Strzałka w lewo` | cofnij o 10 sekund |
| `Strzałka w prawo` | przewiń o 10 sekund |
| `Strzałka w górę` | zwiększ głośność, domyślnie o 5% |
| `Strzałka w dół` | zmniejsz głośność, domyślnie o 5% |
| `Ctrl+Strzałka w lewo` | poprzedni utwór lub element |
| `Ctrl+Strzałka w prawo` | następny utwór lub element |
| `Shift+Strzałka w lewo` | cofnij o 60 sekund |
| `Shift+Strzałka w prawo` | przewiń o 60 sekund |
| `Shift+Strzałka w górę` | zwiększ głośność o 1% |
| `Shift+Strzałka w dół` | zmniejsz głośność o 1% |
| `Ctrl+Home` | początek utworu |
| `Ctrl+End` | przejdź w pobliże końca utworu, domyślnie 10 sekund przed końcem |
| `Page Up` | poprzednia sesja |
| `Page Down` | następna sesja |

Polecenia nieobsługiwane przez daną sesję nie mogą być po cichu ignorowane. Program powinien powiedzieć np. „Przewijanie niedostępne dla WiiM”.

Odstęp używany przez polecenie przejścia w pobliże końca jest konfigurowalny. Program nie ustawia pozycji na dokładnym końcu, ponieważ mogłoby to natychmiast przełączyć utwór.

Po prefiksie wszystkie strzałki służą sterowaniu globalnemu. Na zwykłej liście wszystkie strzałki zachowują natywne działanie listy i pozostają dostępne dla przyszłego zaznaczania oraz pracy z playlistami. Bez prefiksu strzałki transportowe działają dopiero w widoku odtwarzacza.

### 6.1. Widok odtwarzacza

Odtwarzacz jest widokiem wewnątrz głównego okna, a nie osobnym oknem modalnym. Enter na utworze lub stacji zapewnia odtwarzanie elementu i otwiera ten widok; jeżeli element już gra, nie przełącza go na pauzę. `Ctrl+Enter` zachowuje działanie bezpośrednie na liście i nie otwiera odtwarzacza. `F6`, polecenie „Teraz odtwarzane” albo `N` po prefiksie pokazuje odtwarzacz bez uruchamiania nowego zaznaczenia. Escape wraca dokładnie do wcześniejszego widoku i elementu, nie zatrzymując dźwięku ani nie przenosząc zaznaczenia do bieżącego utworu. Na listach bieżący utwór otrzymuje dostępny początek „Odtwarzany” albo „Wstrzymany”, dzięki czemu jego stan można rozpoznać bez opuszczania przeglądanej pozycji.

W odtwarzaczu lewo/prawo przewija o 10 sekund, Shift+lewo/prawo o 30 sekund, Ctrl+lewo/prawo o minutę, góra/dół zmienia głośność o 5%, Shift+góra/dół o 1%, Home przechodzi na początek, a End w pobliże końca. Alt+strzałki oraz Ctrl+Shift+strzałki pozostają wolne do czasu ustalenia potrzeb. Tab przechodzi przez rzeczywiste przyciski odtwarzania, przewijania, głośności i powrotu. Funkcje zależne od możliwości sesji, np. nagrywanie radia, pojawią się później jako warunkowe kontrolki i polecenia; nie otrzymują jeszcze stałego skrótu. Docelowo przypisania odtwarzacza będą konfigurowalne obok profilu prefiksu.

Planowane jest osobne polecenie „Skocz do miejsca”, przyjmujące czas bezwzględny, oraz wariant przejścia do procentu długości. Docelowe skróty, w tym możliwe `J` i `Shift+J` po prefiksie, pozostają do sprawdzenia razem z całą warstwą prefiksową. Polecenia nie będą wiązane na stałe przed testem konfliktów.

## 7. Okno przeglądania

### 7.1. Lista zamiast drzewa

Podstawowym widokiem będzie zwykła dostępna lista lub tabela, nie drzewo rozwijane strzałkami.

Powody:

- Enter jest jednoznaczny i przewidywalny;
- czytnik ekranu nie musi ogłaszać wielu poziomów rozwinięcia;
- łatwiej zachować pozycję po przeładowaniu;
- ten sam komponent może pokazać wykonawców, albumy, utwory, playlisty i urządzenia.

Kolejność informacji w dostępnej etykiecie elementu jest konfigurowalna. Użytkownik może ustawić na przykład tytuł, wykonawcę, czas trwania i typ elementu albo wykonawcę przed tytułem. Pola bez wartości są pomijane. Ta sama kolejność obowiązuje na listach i w komunikatach o bieżącym elemencie; pozostaje niezależna od profilu klawiatury.

### 7.1.1. Tożsamość, główna nazwa i klucz nawigacji

Każdy wiersz rozdziela trzy informacje, których nie wolno utożsamiać:

1. **Tożsamość techniczna** służy do wykonania działania na właściwym zasobie.
2. **Główna nazwa semantyczna** służy domyślnie do sortowania i nawigacji wpisywanymi literami.
3. **Dostępna etykieta** zawiera pola odczytywane przez NVDA w kolejności wybranej przez użytkownika.

Żaden tytuł, wykonawca ani inny tekst prezentacyjny nie jest gwarantowanie unikatowy. Klucz zasobu musi być złożony co najmniej z identyfikatora adaptera, przestrzeni konta lub biblioteki, rodzaju zasobu i natywnego identyfikatora usługi. Osobny klucz wystąpienia zawiera kontekst listy oraz identyfikator albo pozycję wystąpienia, ponieważ ten sam utwór może wystąpić kilka razy w kolejce lub playliście. Identyczne tytuły z TIDAL-u, Apple Music i Spotify pozostają trzema wynikami; nazwa usługi i typ są czytane jako informacje rozróżniające, ale nie zastępują tożsamości technicznej.

Każdy widok otrzymuje jawny opis prezentacji: dopuszczalne rodzaje elementów, główną nazwę, dostępne pola sortowania, aktualne sortowanie, klucz nawigacji literowej i pola rozróżniające duplikaty. Domyślne reguły są następujące:

| Widok lub rodzaj elementu | Domyślny klucz nawigacji literowej |
| --- | --- |
| utwory, kolejka, historia i „Teraz odtwarzane” | tytuł utworu |
| wykonawcy | nazwa wykonawcy |
| albumy i wydania | tytuł albumu; wykonawca i rok rozróżniają duplikaty |
| playlisty i miksy | nazwa playlisty lub miksu |
| stacje radiowe i presety | nazwa stacji albo presetu |
| audycje i podcasty | nazwa audycji |
| odcinki, rozdziały i teledyski | tytuł odcinka, rozdziału albo teledysku |
| audiobooki | tytuł audiobooka |
| urządzenia, pomieszczenia i grupy | nazwa nadana przez użytkownika |
| wejścia i wyjścia audio | dostępna nazwa wejścia albo wyjścia |
| wyniki jednego rodzaju | główna nazwa właściwa dla tego rodzaju |
| wyniki mieszane lub globalne | główna nazwa każdego wyniku; typ i usługa rozróżniają pozycje |

Opis prezentacji określa również, czy odczytywać rodzaj i usługę. Lista jednorodna nie powtarza rodzaju przy każdym wierszu: wewnątrz albumu nie mówi „utwór”, a w widoku Albumy nie mówi „album”. Lista mieszana zachowuje rodzaj. Lista jednej sesji nie powtarza usługi; wynik wspólnego wyszukiwania usług zezwalających na agregację podaje ją na końcu po danych elementu. Adapter wymagający izolacji, w szczególności TIDAL, otwiera osobną listę wyników. Po naciśnięciu zwykłego Enter albo po zamknięciu wyszukiwania globalnego Escape nazwa docelowej usługi jest jednorazowym prefiksem pierwszej etykiety głównej listy, np. „TIDAL, Anna Kowalska, Brzeg ciszy”. Dalsza nawigacja nie powtarza usługi.

Ulubione należą do konkretnego adaptera, konta lub lokalnej biblioteki bieżącej sesji. `Ctrl+U` pokazuje Ulubione tej sesji; usługa nie jest powtarzana w wierszach. Pierwszy etap nie tworzy odrębnych „globalnych Ulubionych AMC”. Model zachowuje jednak pełny klucz źródła, aby przyszły opcjonalny widok mógł agregować natywne Ulubione wielu usług bez kopiowania ich między kontami; taki widok będzie podawał usługę przy każdym wyniku.

Kolejka należy do aktywnej sesji odtwarzania lub celu, np. konkretnego komputera, urządzenia albo strefy. Może mieszać źródła tylko wtedy, gdy pozwalają na to adaptery i cel odtwarzania. `Ctrl+Q` pokazuje kolejkę bieżącej sesji, więc nazwa usługi nie jest powtarzana w każdym wierszu. Nie istnieje jedna kolejka łącząca równolegle działające urządzenia.

Kolejność pól odczytu nigdy nie zmienia klucza nawigacji. Zmiana sortowania według pola tekstowego domyślnie zmienia również klucz nawigacji, na przykład „Sortowanie: wykonawca. Nawigacja literowa: wykonawca”. Przy sortowaniu liczbowym lub czasowym, takim jak czas trwania, numer ścieżki albo data dodania, nawigacja pozostaje przy głównej nazwie, chyba że użytkownik jawnie wybierze inaczej. Brak pola w danym wyniku powoduje powrót do głównej nazwy, a nie pominięcie elementu.

Zapytanie wyszukiwania może sprawdzać wiele pól i aliasów, natomiast nawigacja po już otrzymanych wynikach używa jednego przewidywalnego klucza opisanego powyżej. Adapter usługi przekazuje dostępne pola i możliwości; interfejs nie zakłada, że każda usługa oferuje identyczne typy albo metadane.

### 7.2. Nawigacja

| Klawisz w oknie | Działanie |
| --- | --- |
| `Strzałki w górę/dół` | poprzedni / następny element |
| `Home`, `End` | pierwszy / ostatni element |
| `Page Up`, `Page Down` | przewijanie listy stronami |
| wpisywanie liter | szybkie przejście do elementu zaczynającego się od wpisanego ciągu; kolejne szybko wpisane litery budują frazę, a powtarzanie jednej litery przechodzi między dopasowaniami |
| `Enter` | otwórz wykonawcę, album lub playlistę; na utworze wykonaj domyślną czynność |
| `Ctrl+Enter` | odtwórz lub wstrzymaj zaznaczenie bez otwierania |
| `Spacja` | wstrzymaj lub wznów element faktycznie odtwarzany, niezależnie od zaznaczenia |
| `Shift+Enter` | dodaj zaznaczenie do kolejki |
| `Ctrl+Shift+Enter` | odtwórz jako następne |
| `Backspace` lub `Delete` | usuń element z bieżącej playlisty, kolejki, ulubionych albo biblioteki, jeśli działanie jest jednoznaczne |
| `Ctrl+Z` | cofnij ostatnią zmianę przynależności; w polu tekstowym cofnij edycję tekstu |
| `Alt+Strzałka w lewo` | poprzedni widok |
| `Alt+Strzałka w prawo` | następny widok, jeśli istnieje |
| `Alt+Enter` | informacje o elemencie |
| brak domyślnego skrótu | otwórz element w oficjalnej aplikacji usługi; polecenie pozostaje w menu kontekstowym i palecie do czasu uporządkowania prefiksu |
| `Klawisz aplikacji` lub `Shift+F10` | menu kontekstowe |

Enter wykonuje działanie podstawowe zależne od rodzaju elementu: na utworze, stacji lub presecie przełącza odtwarzanie zaznaczenia, natomiast na albumie, playliście albo wykonawcy otwiera zawartość. `Ctrl+Enter` wykonuje „Odtwórz lub wstrzymaj” bez otwierania: uruchamia nowe zaznaczenie, wstrzymuje bieżące albo je wznawia. Docelowo ta sama reguła uruchomi cały album lub playlistę bez wchodzenia do środka. `Spacja` steruje tym, co faktycznie gra, i nie zależy od położenia zaznaczenia. `Alt+Enter`, zgodnie z typowym zachowaniem menedżerów plików, pozostaje informacją lub właściwościami elementu; nie służy do otwierania zewnętrznej aplikacji.

Zwykłe litery na liście nigdy nie wykonują poleceń AMC. Pozostają nawigacją według jawnego klucza bieżącego widoku. Polecenia jednoliterowe działają dopiero po prawidłowym aktywowaniu globalnej warstwy prefiksowej.

### 7.3. Przeładowywanie

- Stara lista pozostaje widoczna do czasu otrzymania nowych danych.
- Program ogłasza krótko „Ładowanie”, ale nie powtarza komunikatu dla każdej części danych.
- Po zakończeniu informuje np. „24 utwory”.
- Po otwarciu albumu lub playlisty podaje zwięzłe podsumowanie, np. „Album: Abbey Road, The Beatles. 17 utworów, 47 minut 23 sekundy”.
- Po powrocie do wcześniejszego widoku przywraca poprzednio zaznaczony element.
- Odświeżenie nie powinno bez potrzeby przenosić fokusu na początek listy.

### 7.4. Wyszukiwanie i historia zapytań

`Ctrl+F` otwiera wyszukiwanie w bieżącej usłudze, a `Ctrl+Shift+F` — wyszukiwanie globalne. Nazwa okna i nazwa dostępnościowa pola edycyjnego krótko wskazują zakres: „Szukaj w TIDAL” albo „Szukaj we wszystkich usługach”. Enter w polu wykonuje zapytanie i przenosi fokus na pierwszy wynik; kolejny Enter otwiera wynik. Działania bezpośrednie pozostawiają wyniki otwarte, a ich komunikaty zawsze kończą się nazwą usługi. Po naciśnięciu zwykłego Enter fokus głównej listy odczytuje usługę i wybrany element jako jedną wypowiedź. Po działaniu bezpośrednim i zamknięciu wyszukiwania globalnego Escape obowiązuje ta sama reguła, nawet jeśli wybrana usługa była już aktywna. Nazwa usługi jest chwilowym początkiem dostępnościowej nazwy tego elementu i znika po zmianie zaznaczenia. Wyszukiwanie globalne jest operacją orkiestrującą, nie obietnicą jednej wymieszanej listy: adapter deklaruje `mixed-results`, `isolated-results` albo brak wyszukiwania. Wyniki TIDAL są prezentowane osobno i z wymaganym oznaczeniem usługi.

Po poprawnym zapytaniu natywna lista podaje etykietę wyniku i jego pozycję, np. „1 z 3”, bez dodawanego prefiksu „Wyniki wyszukiwania”. Widoczna liczba wyników nie jest osobnym komunikatem na żywo. Przy włączonych szczegółowych podpowiedziach krótka instrukcja o strzałkach, Enterze i Escape jest pomocą wybranego elementu, dzięki czemu następuje po nazwie wyniku, a nie przed nią. Działania bezpośrednie pozostają w menu kontekstowym i dokumentacji. Brak wyników pozostaje jawnym komunikatem.

Historia wyszukiwania będzie lokalna i rozdzielona według zakresu: osobno dla każdej usługi oraz dla wyszukiwania globalnego. Przy pustym polu strzałka w dół otworzy do 20 ostatnich unikatowych zapytań, od najnowszego. Strzałki tylko wybiorą pozycję, a Enter dopiero wykona zapytanie; samo zaznaczenie nigdy nie uruchomi wyszukiwania. Powtórzone zapytanie wróci na początek zamiast tworzyć duplikat. Użytkownik będzie mógł wyczyścić historię i wyłączyć jej zapisywanie. Historia nie zawiera tokenów, nie jest synchronizowana i nie trafia do eksportu bez osobnej, świadomej zgody.

Po rozszerzeniu wspólnego modelu metadanych zwykłe strzałki w lewo i w prawo na listach wyników i pozostałych listach będą przechodziły po dostępnych polach bieżącego zasobu, np. tytule, wykonawcy, albumie, kompozytorze, roku i usłudze. Adapter deklaruje dostępne pola, a interfejs pomija brakujące wartości. Mechanizm ma być wspólny dla wszystkich widoków, a nie implementowany osobno dla każdej usługi.

### 7.5. Długie listy i porcjowanie danych

Nawigacja literami zachowuje obecną regułę semantyczną: przeszukuje główną nazwę właściwą dla bieżącego widoku, np. tytuł utworu, nazwę wykonawcy albo tytuł albumu, niezależnie od kolejności pól odczytywanych przez czytnik ekranu. Litery działają tylko wśród elementów już załadowanych. Pełne przeszukiwanie katalogu usługi pozostaje zadaniem `Ctrl+F`, dzięki czemu wpisanie litery nie wywołuje serii nieprzewidywalnych zapytań sieciowych.

Długie katalogi i wyniki są pobierane porcjami, roboczo po 100–200 elementów. Adapter tłumaczy mechanizm konkretnej usługi na wspólny wynik strony: elementy, nieprzezroczysty znacznik kontynuacji, informację, czy istnieje następna strona, oraz opcjonalną liczbę wszystkich wyników. Interfejs nie zakłada, że usługa zna sumę ani że obsługuje numery stron.

Na ostatnim załadowanym elemencie zwykła strzałka w dół albo Page Down rozpoczyna pobieranie następnej porcji. W trakcie fokus pozostaje na tym samym elemencie identyfikowanym trwałym ID. Program mówi krótko „Ładowanie kolejnych elementów”, a po sukcesie np. „Załadowano 100 kolejnych, 200 łącznie”; następna strzałka przechodzi już do pierwszego nowego elementu. Nieudane pobranie nie zmienia zaznaczenia i kończy się dostępnym komunikatem z możliwością ponowienia.

Po liście może znajdować się prawdziwy przycisk **Załaduj więcej** jako alternatywa dostępna Tabem. Nie jest on udawanym elementem multimedialnym wewnątrz listy, nie uczestniczy w nawigacji literami i znika albo staje się niedostępny, gdy nie ma dalszych danych. Po użyciu przycisku fokus przechodzi na pierwszy nowo dodany element.

Doładowanie dopisuje elementy do istniejącej kolekcji zamiast bez potrzeby zastępować całe źródło listy. Aktualizacje są grupowane, a czytnik ekranu otrzymuje tylko końcowy stan. Jeżeli elementy nie zmieniły kolejności, aktualizowane są wyłącznie ich etykiety. Fokus i pozycja przewijania są przywracane według trwałego ID, nie surowego numeru wiersza. Gdy całkowita liczba jest znana, komunikat może brzmieć „200 z 1843 załadowanych”; w przeciwnym razie „200 załadowanych, więcej dostępnych”.

Inspiracją dla stabilności fokusu, porcjowania i grupowania zdarzeń jest [WinZapp_Python](https://github.com/gabrielhhaber/WinZapp_Python). Zachowanie implementujemy niezależnie w .NET i WPF; nie kopiujemy kodu projektu objętego GPL-3.0 ani nie zmieniamy z tego powodu technologii AMC.

## 8. Wybór playlisty

`Shift+P` w warstwie albo lokalne polecenie zarządzania playlistami otwiera niewielkie modalne okno:

- pole filtrowania;
- lista wszystkich playlist bieżącej usługi z informacją „zawiera” albo „nie zawiera”;
- ostatnio używane playlisty na początku, opcjonalnie;
- Spacja przełącza przynależność elementu do wskazanej playlisty;
- Enter zatwierdza wszystkie zmiany;
- `Ctrl+N` tworzy nową playlistę;
- Escape anuluje;
- po wykonaniu operacji fokus wraca dokładnie do wcześniejszego elementu.

W ustawieniach można później wskazać playlistę domyślną, np. „Do odsłuchu”. Osobne polecenie może dodawać do niej bez otwierania okna. Zwykłe `Shift+P` powinno jednak domyślnie dawać wybór.

## 9. Skróty lokalne w oknie

Skróty lokalne są niezależne od warstwy prefiksowej, ale wywołują te same wewnętrzne polecenia.

Zatwierdzone przypisania podstawowe:

| Skrót lokalny | Działanie |
| --- | --- |
| `Ctrl+K` | filtruj tylko aktualnie załadowaną listę |
| `Ctrl+F` | wyszukaj w bieżącej usłudze lub źródle |
| `Ctrl+Shift+F` | wyszukaj we wszystkich włączonych usługach i źródłach |
| `Ctrl+Shift+K` | otwórz paletę poleceń AMC |
| `Ctrl+L` | otwórz Bibliotekę |
| `Ctrl+U` | otwórz Ulubione |
| `Ctrl+P` | otwórz Playlisty |
| `Ctrl+Q` | otwórz Kolejkę |
| `Ctrl+Shift+A` | otwórz Albumy |
| `Ctrl+C` | kopiuj nazwę wybranego elementu |
| `Ctrl+Shift+C` | kopiuj łącze do elementu w usłudze |
| `Ctrl+Shift+U` | dodaj do Ulubionych albo usuń z Ulubionych |
| `Ctrl+Shift+P` | otwórz wybór playlist i zmień przynależność |
| `Ctrl+Shift+Q` | dodaj do kolejki |
| `Ctrl+Shift+L` | dodaj do biblioteki |
| `Ctrl+O` | otwórz jeden lub wiele lokalnych plików audio |
| `Ctrl+Shift+O` | otwórz folder z plikami audio wraz z podfolderami |
| `F6` | otwórz widok odtwarzacza |
| `Strzałka w lewo/prawo` w odtwarzaczu | cofnij albo przewiń o 10 sekund |
| `Shift+Strzałka w lewo/prawo` w odtwarzaczu | cofnij albo przewiń o 30 sekund |
| `Ctrl+Strzałka w lewo/prawo` w odtwarzaczu | cofnij albo przewiń o minutę |
| `Strzałka w górę/dół` w odtwarzaczu | zmień głośność o 5% |
| `Shift+Strzałka w górę/dół` w odtwarzaczu | zmień głośność o 1% |
| `Home` w odtwarzaczu | przejdź na początek utworu |
| `End` w odtwarzaczu | przejdź 10 sekund przed końcem utworu |
| `Ctrl+Shift+E`, `Ctrl+Shift+R`, `Ctrl+Shift+T` | podaj czas od początku, pozostały albo całkowity |
| `Ctrl+Shift+G` | włącz lub wyłącz automatyczny odczyt pozycji po przewijaniu |
| `Ctrl+D` | pobierz offline wewnątrz usługi, jeśli obsługiwane |
| `Ctrl+Shift+D` | pobierz do pliku lokalnego; funkcja eksperymentalna, domyślnie wyłączona |
| `Backspace` lub `Delete` | usuń z bieżącej playlisty, kolejki, ulubionych lub biblioteki; z potwierdzeniem albo możliwością cofnięcia |
| `Ctrl+Z` | cofnij ostatnią zmianę przynależności do Ulubionych, Biblioteki lub Kolejki albo stan „Odtwórz jako następne” |
| `F2` | zmień nazwę playlisty, jeśli obsługiwane |
| `Ctrl+A` | zaznacz wszystkie elementy, jeśli widok pozwala |

Każdy skrót lokalny jest zmienny. Polecenia pobierania nie powinny być aktywne, dopóki odpowiedni moduł nie zostanie świadomie włączony.

`Ctrl+1–9` wybiera sesję, `Ctrl+0` otwiera listę sesji, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję.

## 10. Menu kontekstowe

Menu kontekstowe jest obowiązkowe. Powinno pokazywać tylko funkcje dostępne dla rodzaju elementu i bieżącej usługi, ale zachowywać stałą, przewidywalną kolejność:

1. Odtwórz lub wstrzymaj.
2. Odtwórz jako następne.
3. Dodaj do kolejki.
4. Pokaż zawartość albumu, playlisty albo wykonawcy — jeśli dotyczy.
5. Dodaj do ulubionych albo usuń z ulubionych.
6. Dodaj do playlisty.
7. Dodaj do biblioteki.
8. Udostępnij lub kopiuj łącze.
9. Pobierz — tylko jeśli dostępne i włączone.
10. Usuń z bieżącego widoku — jeśli dotyczy.
11. Informacje.
12. Otwórz w oficjalnej aplikacji usługi.

Przy każdej pozycji menu powinien być wyświetlony jej aktualny skrót. Czynności destrukcyjne należy oddzielić separatorem.

## 11. Ustawienia klawiatury

Ustawienia muszą umożliwiać zmianę wszystkiego:

- globalnego prefiksu;
- czasu aktywności warstwy;
- skrótów wyboru sesji;
- liter i par `litera` / `Shift+litera`;
- poleceń transportowych;
- skrótów lokalnych w oknie;
- poleceń bez przypisanego skrótu;
- zachowania klawisza Enter dla każdego typu elementu;
- domyślnej playlisty;
- pamiętania ostatniej sesji.

Domyślna mapa zachowuje symetrię: `Ctrl+klawisz` w aktywnym oknie odpowiada `klawiszowi` po prefiksie, a `Ctrl+Shift+klawisz` odpowiada `Shift+klawiszowi`. Jawne wyjątki to Albumy — `Ctrl+Shift+A` w oknie i `A` po prefiksie ze względu na standardowe `Ctrl+A` — oraz informacyjne `Ctrl+E`, `Ctrl+R` i `Ctrl+T` wewnątrz warstwy. Wszystkie wyjątki są opisane i sprawdzane pod kątem konfliktów.

Język interfejsu jest ustawieniem niezależnym od profilu skrótów. Zmiana języka nie może samodzielnie przemeblować klawiatury. W pierwszej wersji powstaje wyłącznie profil Windows; profil macOS zostanie zaprojektowany razem z późniejszym wydaniem dla tej platformy.

Program może przechowywać wiele profili klawiatury. Użytkownik wybiera profil aktywny, tworzy edytowalną kopię profilu domyślnego, nadaje jej nazwę i przełącza profile w ustawieniach. Profil wbudowany pozostaje chroniony, aby zawsze istniał pewny punkt powrotu.

Wymagane funkcje edytora skrótów:

- przechwycenie nowej kombinacji;
- wykrywanie duplikatów wewnątrz programu;
- próba wykrycia konfliktu globalnego;
- możliwość całkowitego wyłączenia polecenia;
- przywracanie ustawień domyślnych dla pojedynczego polecenia lub całej sekcji;
- osobny import i eksport pojedynczej mapy klawiszy;
- osobny import i eksport konfiguracji bez map klawiatury;
- import i eksport pełnej kopii zawierającej ustawienia, wszystkie profile, kolejność sesji i szablony komunikatów.

Pełna kopia ani pozostałe eksporty nie mogą zawierać haseł, tokenów i danych logowania. Proponowane rozszerzenia to `.amckeys.json`, `.amcsettings.json` oraz `.amcbackup.json`.

Okno ustawień powinno zawierać kategorie: Ogólne, Język, Prefiks i polecenia, Sesje i usługi, Odtwarzanie, Listy, Komunikaty i dostępność oraz Zaawansowane. `Ctrl+F` wyszukuje ustawienie po nazwie.

## 12. Komunikaty, mowa i brajl

Każdy komunikat powinien być jednocześnie:

- zapisany w widocznym, dostępnym polu stanu;
- wysłany jako systemowe powiadomienie dostępności;
- dostępny dla mowy i monitora brajlowskiego za pośrednictwem czytnika ekranu.

Podstawą pierwszej wersji jest UI Automation w Windows. Program nie może wymagać NVDA do działania. Accessibility API w macOS zostanie opracowane dopiero przy rozpoczęciu prac nad wydaniem dla Maca.

Domyślne komunikaty mają być krótkie. W pierwszej wersji nie wprowadzamy osobnych profili „krótki”, „normalny” i „szczegółowy”. Zamiast nich użytkownik może edytować szablon każdego komunikatu, wyłączyć go albo przywrócić ustawienie domyślne. Oddzielna globalna opcja szczegółowych podpowiedzi klawiatury obejmuje filtr oraz wyszukiwanie bieżące i globalne; jest domyślnie wyłączona, nie zmienia treści komunikatów zdarzeń, a na wynikach wymienia tylko strzałki, Enter i Escape. Niezależna opcja odczytu pozycji po przewijaniu pozwala seryjnie używać strzałek bez wypowiadania każdej wartości; nie wycisza informacji o czasie wywołanych na żądanie.

Ustawienia komunikatów:

- osobne włączanie komunikatów o sesji, odtwarzaniu, głośności, ładowaniu i błędach;
- edycja tekstu z polami takimi jak `{service}`, `{title}`, `{elapsed}`, `{remaining}` i `{total}`;
- przycisk odsłuchania przykładu i przywrócenia krótkiego szablonu domyślnego;
- opcjonalne dźwięki zamiast niektórych wypowiedzi;
- ograniczanie powtarzających się komunikatów;
- możliwość ponownego odczytania ostatniego komunikatu;
- opcja używania bezpośredniego modułu NVDA w przyszłości, ale tylko jako dodatku.

Przykłady komunikatów krótkich:

- „TIDAL”.
- „Dodano do ulubionych: Brzeg ciszy”.
- „Już w ulubionych”.
- „Dodano do: Do odsłuchu”.
- „Głośność WiiM: 35%”.
- „Ładowanie albumu”.
- „24 utwory”.
- „Polecenie niedostępne dla tej sesji”.
- dla skrótów czasu domyślnie tylko „1:23”, „2:57” albo „4:20”.

Przy operacjach globalnych warto podawać usługę, jeżeli istnieje ryzyko pomyłki, np. „Apple Music: dodano do playlisty”.

## 13. Architektura funkcjonalna

### 13.1. Jeden rdzeń, wiele klientów

Docelowy podział rozwiązania:

- **AMC.Core (.NET)** — niezależne od interfejsu modele, stabilne identyfikatory poleceń, sesje, kolejka, Ulubione, Biblioteka, odtwarzanie, konfiguracja, historia cofania i komunikaty domenowe;
- **AMC.Host (.NET)** — działający proces utrzymujący stan, połączenia, adaptery, autoryzację, pamięć podręczną i lokalny interfejs komunikacyjny;
- **AMC.Windows (WPF)** — natywne okno Windows, UI Automation, menu, listy, fokus, globalny prefiks i zasobnik;
- **AMC.NVDA (Python)** — opcjonalna cienka wtyczka rejestrująca gesty NVDA, wysyłająca polecenia do AMC.Host i prezentująca odpowiedzi; bez adapterów, dużych bibliotek i danych logowania;
- **AMC.macOS (Swift/AppKit)** — przyszłe natywne okno, NSAccessibility, VoiceOver, menu i właściwa dla macOS implementacja skrótów;
- **AMC.Adapters.*** — niezależne moduły usług muzycznych, urządzeń, radia i lokalnego odtwarzania;
- **AMC.Update** — aktualizacja aplikacji i zgodnych adapterów.

WPF pozostaje warstwą tylko dla Windows. Wspólny rdzeń .NET nie odwołuje się do WPF, NVDA ani API dostępności konkretnego systemu. Natywny klient macOS nie musi bezpośrednio ładować klas .NET; komunikuje się z hostem przez wersjonowany kontrakt poleceń i zdarzeń. Jeżeli później pomiary uzasadnią bezpośredni most binarny, można go dodać bez zmiany modelu poleceń.

FastSMRW jest wzorcem architektonicznym: jeden przenośny rdzeń, cienkie natywne interfejsy i opcjonalna warstwa sterowania bez przechodzenia do okna. AMC wykorzystuje tę zasadę, lecz zachowuje własną technologię, kontrakt i mapę poleceń.

Interfejsy przekazują polecenia, np. `favorites.toggle`, `session.next` albo `search.current`, a host publikuje zdarzenia stanu i gotowe dane do przedstawienia. Nie wykonuje się poleceń terminala. Lokalna komunikacja ma być asynchroniczna, wersjonowana, ograniczona do bieżącego użytkownika i odporna na rozłączenie klienta.

Opóźnienie lokalnego IPC nie jest elementem krytycznym wobec czasu zapytań sieciowych i odpowiedzi urządzeń. Interfejs może przechowywać wyłącznie mały, niesekretny obraz ostatniego stanu potrzebny do natychmiastowego odczytu. Tokeny i sekrety pozostają w hoście, w systemowym magazynie poświadczeń: Windows Credential Manager lub macOS Keychain.

### 13.2. Adaptery i możliwości

Każdy adapter deklaruje możliwości zamiast udawać identyczność usług. Przykładowe możliwości to wyszukiwanie, Ulubione, Biblioteka, playlisty, kolejka, pobieranie wewnątrz usługi, legalny eksport na dysk, przewijanie, głośność, presety, grupowanie urządzeń i zdarzenia czasu rzeczywistego. Niedostępne polecenie jest wyłączone albo kończy się jednoznacznym komunikatem.

Katalog rodzajów zasobów pozostaje rozszerzalny. Oprócz obecnych utworu, albumu, wykonawcy, playlisty, stacji i urządzenia przewiduje audycję lub podcast, odcinek, audiobook, rozdział, teledysk, miks, preset, wejście, wyjście, pomieszczenie i grupę urządzeń. Wynika to z rzeczywistych różnic usług: Spotify udostępnia między innymi audycje, odcinki i audiobooki, Apple Music także teledyski i stacje, TIDAL własne zasoby katalogowe utworów, albumów, wykonawców i playlist, a WiiM urządzenia, grupy, wejścia, kolejki i presety. Nieznany przyszły rodzaj zachowuje natywny identyfikator oraz główną nazwę i może być pokazany jako ogólny element bez utraty tożsamości.

Rozróżniamy:

- **źródła i katalogi** — TIDAL, Spotify, Apple Music, radio internetowe i biblioteka lokalna;
- **urządzenia i cele odtwarzania** — lokalny komputer, WiiM, Sonos, Bluesound/BluOS i Frontier Smart;
- **sesję** — aktualne połączenie źródła, konta, kolejki i celu odtwarzania, np. „TIDAL na WiiM w salonie”.

Pierwsze realne logowania otwierają systemową przeglądarkę i używają oficjalnych metod danej usługi. Rdzeń musi obsługiwać OAuth z PKCE, kod powrotu, odświeżanie tokenu, anulowanie, wylogowanie i utratę uprawnień. Integracje wymagające sekretu lub publicznego adresu zwrotnego mogą potrzebować małego, kontrolowanego zaplecza internetowego. Apple Music może mieć różne szczegóły integracji na Windows i macOS, ale przedstawia rdzeniowi ten sam zestaw możliwości.

Lokalne urządzenia mogą wymagać wykrywania w sieci przez mDNS, SSDP/UPnP albo HTTP. Wykrywanie, autoryzacja i sterowanie urządzeniem nie należą do interfejsu okna ani do wtyczki NVDA.

#### 13.2.1. Zakres oficjalnych integracji

Źródło katalogu i cel odtwarzania są niezależnymi adapterami. Przełączenie do WiiM, BluOS albo Sonos pokazuje urządzenia, wejścia, presety i stan odtwarzania udostępniany przez dany ekosystem; nie oznacza automatycznie uzyskania całego katalogu każdej usługi widocznej w aplikacji producenta. Sesja może je połączyć dopiero wtedy, gdy istnieje oficjalna droga przekazania odtwarzania, np. „Spotify na WiiM w salonie”.

- **WiiM**: publiczne lokalne API HTTPS zapewnia informacje o urządzeniu, stan i metadane odtwarzania, transport, przewijanie, głośność, wyciszenie, tryby powtarzania, EQ, alarmy, wejścia, wyjścia i 12 presetów. Rozpoznaje Spotify Connect i TIDAL Connect jako aktywne tryby, ale nie dokumentuje przeglądania katalogów usług ani uniwersalnego wyszukiwania znanego z WiiM Home. Pierwszy adapter WiiM jest więc adapterem urządzenia i presetów, a nie zastępczym API TIDAL. [HTTP API for WiiM Products](https://www.wiimhome.com/pdf/HTTP%20API%20for%20WiiM%20Products.pdf), [WiiM Home App User Guide](https://wiimhome.com/pdf/WiiM%20Home%20App%20User%20Guide.pdf).
- **BluOS/Bluesound**: lokalne API HTTP/XML pozwala dodatkowo przeglądać i wyszukiwać źródła skonfigurowane na odtwarzaczu, w tym TIDAL, stronicować wyniki, pobierać menu kontekstowe, wykonywać działania Ulubionych i kolejki, zarządzać kolejką, presetami i grupami. Jest to pierwszy kandydat do sesji, w której urządzenie może pośredniczyć zarówno w katalogu, jak i odtwarzaniu. Presety dodaje i usuwa się w oficjalnym kontrolerze BluOS; AMC może je listować i uruchamiać. [BluOS Custom Integration API 1.7](https://bluos.io/wp-content/uploads/2025/06/BluOS-Custom-Integration-API_v1.7.pdf).
- **Spotify**: Web API obejmuje wyszukiwanie, bibliotekę, playlisty, kolejkę, bieżące odtwarzanie i urządzenia Spotify Connect oraz pozwala przenosić odtwarzanie i sterować transportem. Funkcje odtwarzacza wymagają Premium; urządzenie może być oznaczone jako ograniczone i wtedy nie przyjmuje poleceń. Pierwszy prawdziwy OAuth powinien wykorzystać Authorization Code z PKCE, obsłużyć limity trybu deweloperskiego oraz ponowną autoryzację po wygaśnięciu tokenu odświeżania. [Spotify Web API](https://developer.spotify.com/documentation/web-api), [Spotify scopes](https://developer.spotify.com/documentation/web-api/concepts/scopes), [Spotify quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes).
- **Apple Music**: Apple Music API udostępnia katalog i osobistą bibliotekę, wyszukiwanie, albumy, utwory, wykonawców, playlisty, teledyski, stacje, oceny i Ulubione, rekomendacje oraz historię. Na macOS odtwarzanie korzysta natywnie z MusicKit dla Swift; na Windows należy osobno zweryfikować dostępność i zgodność dostępnościową MusicKit on the Web. [Apple Music API](https://developer.apple.com/documentation/applemusicapi), [MusicKit](https://developer.apple.com/musickit/).
- **TIDAL**: API i OAuth 2.1 mogą dostarczyć katalog oraz zasoby użytkownika w granicach przyznanych zakresów, ale odtwarzanie musi używać oficjalnego modułu TIDAL Player. Publiczne TIDAL Connect jest przeznaczone dla partnerów sprzętowych. Adapter pozostaje ważnym, osobnym modułem AMC, lecz wymaga prezentacji treści TIDAL w izolowanym widoku, oznaczenia marki, przycisku otwarcia w TIDAL, minimalnego przechowywania danych i formalnego sprawdzenia trybu produkcyjnego. Nie łączymy treści TIDAL z podobnymi usługami w jednej liście i nie udostępniamy nagrywania ani eksportu strumienia. [TIDAL authorization](https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization), [TIDAL Developer Terms](https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms), [TIDAL Design Guidelines](https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines), [TIDAL Connect](https://developer.tidal.com/documentation/connect).
Warstwa przypominająca dostępnego klienta WhatsApp, która opakowuje TIDAL Web i naprawia samą nawigację klawiaturą, jest technicznie możliwa tylko jako ostrożny eksperyment. Nie może automatycznie wydobywać katalogu, playlist ani historii z DOM, ponieważ oficjalne zasady zabraniają scrapingu i automatycznego indeksowania TIDAL. Dopuszczalnym awaryjnym wariantem AMC pozostaje otwarcie oficjalnego odtwarzacza WWW i zapewnienie użytkownikowi przejścia do niego; właściwy adapter korzysta z oficjalnego API i modułu Player. Eksperymentalnej nakładki dostępnościowej nie traktujemy jako podstawowego adaptera bez pisemnego potwierdzenia TIDAL.

- **Sonos**: chmurowe Control API z OAuth pozwala odkrywać gospodarstwa domowe, grupy i odtwarzacze, odczytywać stan, sterować transportem, przewijaniem i głośnością oraz uruchamiać Sonos Favorites i playlisty Sonos. Nie zastępuje katalogowego API istniejących usług muzycznych, wymaga publicznego zwrotnego adresu HTTPS i ma większy koszt integracyjny. Sonos pozostaje w planie, ale po WiiM, Spotify, TIDAL, BluOS, Apple Music, radiu i multimediach lokalnych. [Sonos Control API](https://docs.sonos.com/reference/about-control-api), [Sonos authorization](https://docs.sonos.com/docs/authorize).
- **Frontier Smart**: producent potwierdza NetRemote API, SDK i możliwość budowania własnych aplikacji przez partnerów sprzętowych, ale nie publikuje kompletnej wspieranej dokumentacji konsumenckiej. Stabilny adapter wymaga dostępu partnerskiego; ewentualny adapter społecznościowy musi być osobno oznaczony jako eksperymentalny i nie może być podstawą pierwszego wydania. [Frontier AURIA](https://www.frontiersmart.com/product/auria/), [Frontier customer area](https://www.frontiersmart.com/customer-area/).

Adapter deklaruje osobno co najmniej: zakres wyszukiwania, politykę prezentacji wyników, odczyt i zapis Ulubionych, Bibliotekę, playlisty, możliwość odtworzenia, możliwości kolejki, cele odtwarzania, transport, przewijanie, głośność, wejścia, presety, grupy, pobieranie offline wewnątrz usługi oraz legalny eksport. Interfejs nie zgaduje brakujących możliwości.

### 13.3. Lokalne multimedia i radio

Lokalny moduł odtwarzania obejmuje docelowo otwieranie plików i folderów, metadane, Bibliotekę, kolejkę, podstawowe popularne formaty, wybór urządzenia, odtwarzanie bez przerw i ReplayGain. Na Windows domyślne wyjście dźwięku powinno pracować w trybie współdzielonym, aby nie wyciszać NVDA i pozostałych dźwięków. Tryb wyłączny może pojawić się później jako funkcja zaawansowana z wyraźnym ostrzeżeniem.

Pierwszy krok wdrożony w `alpha.33` rozdziela neutralny interfejs wyjścia dźwięku w rdzeniu od implementacji Windows. `Ctrl+O` ładuje pliki do nietrwałej sesji lokalnej, a systemowy odtwarzacz Windows realizuje odtwarzanie, pauzę, pozycję i głośność w trybie współdzielonym. `Alpha.34` dodaje rekursywne otwieranie folderu przez `Ctrl+Shift+O`, naturalne porządkowanie nazw, pomijanie duplikatów oraz lokalne `Ctrl+E`, `Ctrl+R` i `Ctrl+T`. Nie jest to jeszcze pełna Biblioteka: zapis listy, odtwarzanie bez przerw, ReplayGain, wybór urządzenia i opcjonalne kodeki pozostają późniejszymi etapami.

Radio internetowe jest osobnym adapterem rdzenia i korzysta z tych samych sesji, Ulubionych, historii oraz poleceń transportowych. Powinno obsłużyć bezpośrednie strumienie, M3U/PLS, metadane stacji, ponawianie po zerwaniu i wyszukiwanie. Mechanizmy Free Radio można wykorzystać po analizie kodu i licencji, bez przenoszenia całego odtwarzania do procesu NVDA. Nagrywanie radia może być świadomie uruchamianą funkcją lokalną do prywatnego użytku: zapisuje dostępny bezpośredni strumień bez obchodzenia DRM, nie uruchamia się automatycznie, nie dotyczy TIDAL, Spotify ani Apple Music i pozostawia użytkownikowi odpowiedzialność za zgodność z prawem właściwym dla miejsca użycia.

### 13.4. Testowanie i odpowiedzialność

- logika rdzenia ma testy jednostkowe bez uruchamiania okna;
- kontrakt klient–host ma testy zgodności i wersjonowania;
- adaptery mają testy kontraktowe na atrapach oraz oddzielne, świadomie uruchamiane testy prawdziwych kont i urządzeń;
- mapy skrótów są sprawdzane automatycznie pod kątem duplikatów, poleceń bez mapowania i kolizji z rezerwacjami standardowymi;
- WPF przechodzi testy klawiatury, UI Automation, fokusu i komunikatów z NVDA, JAWS-em i Narratorem;
- wersja macOS przechodzi osobne testy z VoiceOver i narzędziami dostępności Apple;
- awaria adaptera nie może zawiesić czytnika ekranu ani uszkodzić konfiguracji pozostałych usług.

Ręczny arkusz wyników zawiera jedną instrukcję u góry, że w polu „Status” można wpisać `OK`, `Błąd` albo `Pominięto`; wariantów tych nie powtarzamy przy każdym zadaniu. Każde puste pole przeznaczone do uzupełnienia, także „Status”, kończy się dwukropkiem i dokładnie jedną zwykłą spacją, aby po przejściu na koniec wiersza można było od razu pisać. Opis wyniku nazywa czynność użytkownika wprost, np. „wypowiedź po naciśnięciu Enter”; słowa „powrót” nie używamy jako polskiego odpowiednika klawisza Enter.

Most NVDA może być używany wyłącznie jako oddzielne, opcjonalne narzędzie deweloperskie. Nie jest składnikiem AMC ani wydania dla użytkownika. Profil testowy jest domyślnie wyłączony, nasłuchuje tylko na `127.0.0.1`, wymaga losowego tokenu bez wartości domyślnej i udostępnia jedynie odczyt tytułu okna, fokusu oraz obiektu nawigatora. Nie wolno mu przesuwać fokusu, mówić, wyświetlać komunikatów, czytać logu, przeładowywać dodatków ani restartować NVDA. Raport nie utrwala wartości i opisów pól, bieżącego wiersza ani logu czytnika.

Taki most daje pojedynczy zrzut informacji rozpoznawanych przez NVDA; nie przechwytuje wypowiedzi i nie dowodzi poprawności kolejności Tab, skrótów, trybu przeglądania lub brajla. Dlatego pozostaje uzupełnieniem testów UI Automation i ręcznej macierzy NVDA, JAWS oraz Narrator. Publiczny projekt `nvda-mcp-bridge` 0.2.0 jest tylko punktem odniesienia: przed ewentualnym użyciem wymaga utwardzenia, a jego kod GPL-2.0 pozostaje poza kodem i dystrybucją AMC.

### 13.5. Dystrybucja, biblioteki, komponenty i aktualizacje

Obecny pojedynczy plik EXE ma około 162 MB przede wszystkim dlatego, że jest publikacją samowystarczalną i zawiera środowisko .NET. Nie zawiera jeszcze przyszłych usług ani pełnego zestawu kodeków. Rozdzielenie go na wiele plików może zmniejszyć sam plik startowy, ale nie musi zmniejszyć całego miejsca zajętego przez instalację. Priorytetem jest niezawodne uruchomienie bez ręcznego instalowania bibliotek, a oszczędność transferu uzyskujemy przez aktualizacje różnicowe i opcjonalne komponenty.

Przyjmujemy następujący model dla Windows:

1. Podstawowym wydaniem instalowanym jest podpisany pakiet MSIX z małym plikiem App Installer. Instalacja odbywa się dla bieżącego użytkownika, bez ręcznego kopiowania bibliotek. App Installer sprawdza aktualizacje przy uruchomieniu i w tle, a MSIX pobiera tylko zmienione bloki pakietu. Osobny eksperyment techniczny musi potwierdzić działanie WPF, globalnego przechwytywania klawiatury, AMC.Host, IPC, OAuth i zasobnika po zapakowaniu.
2. Paczka przenośna pozostaje wariantem dodatkowym dla zaawansowanych użytkowników. Jest samowystarczalna, ale nie instaluje po cichu aktualizacji systemowych i wyraźnie informuje, kto odpowiada za jej aktualność.
3. Wydanie publiczne przechodzi na bieżącą wersję LTS .NET. Według stanu na datę tego dokumentu wsparcie .NET 8 kończy się 10 listopada 2026 r., a .NET 10 LTS trwa do 14 listopada 2028 r., dlatego migracja do .NET 10 następuje przed pierwszym wydaniem publicznym. Samowystarczalny pakiet otrzymuje poprawki środowiska wraz z aktualizacją AMC.
4. Kanały Stabilny i Beta mają osobne tożsamości oraz metadane. Przejście między kanałami jest świadomą czynnością użytkownika, a nie przypadkową zmianą wersji.

Pakiet aplikacji zawiera zgodny zestaw: AMC.Windows, AMC.Host, AMC.Core, wbudowane adaptery podstawowe oraz właściwe środowisko .NET. Niezależnie aktualizowane komponenty to adaptery usług i urządzeń, opcjonalny silnik lokalnego odtwarzania, opcjonalne kodeki, dane katalogowe niewymagające sekretów oraz cienka wtyczka NVDA. Każdy komponent ma manifest zawierający co najmniej:

- stabilny identyfikator i wersję;
- platformę i architekturę;
- minimalną i maksymalną zgodną wersję API hosta;
- zależności oraz informację, czy komponent jest wymagany;
- rozmiar, sumę SHA-256 i podpisane metadane;
- licencję, źródło kodu i listę składników zewnętrznych;
- kanał wydania i informację o krytyczności aktualizacji.

Aplikacja użytkownika nigdy nie uruchamia `dotnet restore`, NuGet, skryptu instalacyjnego ani komendy pobranej z Internetu. Biblioteki NuGet są wybierane podczas budowania wydania, mają przypięte wersje i pliki `packages.lock.json`, a CI używa trybu zablokowanego, audytu podatności, inwentarza licencji i SBOM. Wydanie powstaje wyłącznie z przejrzanego, powtarzalnego zestawu zależności.

Repozytorium komponentów używa dojrzałej implementacji modelu TUF albo rozwiązania o równoważnych własnościach, zamiast własnego protokołu kryptograficznego. Klucz zaufania jest wbudowany w podpisaną aplikację, klucze główne pozostają offline, a klucze wydawnicze można odwołać i wymienić. HTTPS jest obowiązkowy, lecz nie zastępuje podpisu. Same sumy SHA-256 wykrywają uszkodzenie, ale dopiero podpisane i terminowe metadane chronią również przed podstawieniem, cofnięciem, zamrożeniem oraz pomieszaniem wersji komponentów.

Aktualizacja przebiega następująco:

1. sprawdzenie podpisanych metadanych w tle, bez komunikatu mówionego;
2. wybranie całego zgodnego zestawu dla platformy, kanału i wersji API;
3. pobranie do katalogu tymczasowego z limitem rozmiaru, wznowieniem i poszanowaniem połączenia taryfowego;
4. weryfikacja podpisów, wersji, rozmiarów, SHA-256, zależności i licencji przed udostępnieniem plików;
5. przygotowanie nowej wersji obok aktywnej, bez nadpisywania działających bibliotek;
6. aktywacja po zamknięciu aplikacji albo w wybranym przez użytkownika terminie; nigdy w trakcie odtwarzania;
7. test zdrowia AMC.Host po uruchomieniu i automatyczny powrót do poprzedniej wersji, jeśli nowa nie wystartuje lub nie odpowie;
8. zachowanie co najmniej jednej poprzedniej działającej wersji i uporządkowanie starszych plików dopiero po pomyślnym starcie.

Konfiguracja, biblioteka użytkownika, pamięć podręczna i poświadczenia są oddzielone od plików programu. Aktualizacja nie może ich usuwać ani zastępować. Konfiguracja ma wersjonowany schemat i migrację jednokierunkową z kopią bezpieczeństwa. Wtyczka NVDA jest przygotowywana osobno i aktywowana dopiero przy bezpiecznym ponownym uruchomieniu NVDA; aktualizator nie podmienia plików wewnątrz działającego czytnika ekranu.

W zakresie kodeków na Windows najpierw wykrywamy i wykorzystujemy możliwości Media Foundation oraz kodeki legalnie zainstalowane w systemie. AMC nie instaluje globalnych „codec packów” i nie zastępuje systemowych bibliotek. Brakujący format może otrzymać opcjonalny, izolowany komponent AMC. Jeżeli wybierzemy FFmpeg, będzie to jawny pakiet DLL z dokładnie określoną konfiguracją LGPL, bez części GPL i `nonfree`, z wymaganymi informacjami licencyjnymi, odpowiadającym kodem źródłowym i niezależną aktualizacją. Własny silnik lub kodek użytkownika może być funkcją zaawansowaną, uruchamianą poza procesem głównym i wyraźnie oznaczoną jako składnik niezarządzany przez AMC.

Aktualizacje domyślnie sprawdzają się i pobierają w tle, ale instalują przy bezpiecznym zamknięciu. Nie kradną fokusu, nie przerywają mowy ani odtwarzania i nie wyświetlają powtarzających się okien. Użytkownik może wyłączyć automatyczne pobieranie, wybrać kanał, odroczyć instalację i sprawdzić dostępny dziennik: wersja, rozmiar, składniki, wynik weryfikacji i powód ewentualnego cofnięcia. Krytyczne wydanie bezpieczeństwa może wymagać aktualizacji, lecz zawsze komunikuje to jednoznacznie w dostępnym oknie.

Na macOS klient, host i składniki platformowe otrzymają osobno podpisaną i notaryzowaną dystrybucję zgodną z mechanizmami Apple. Wspólny pozostaje format manifestu komponentów i reguły zgodności, natomiast instalacja i podpis platformowy są natywne dla systemu.

Podstawy techniczne tej decyzji: [tryby publikowania .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/), [cykl wsparcia .NET](https://dotnet.microsoft.com/en-us/platform/support/policy), [MSIX i aktualizacje różnicowe](https://learn.microsoft.com/en-us/windows/msix/overview), [automatyczne aktualizacje App Installer](https://learn.microsoft.com/pl-pl/windows/msix/app-installer/auto-update-and-repair--overview), [specyfikacja TUF](https://theupdateframework.github.io/specification/latest/), [blokowanie zależności NuGet](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files), [kodeki obsługiwane przez Windows](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/supported-codecs) oraz [wymagania licencyjne FFmpeg](https://ffmpeg.org/legal.html).

## 14. Zakres pierwszej wersji

Obecny prototyp Windows powinien najpierw ustabilizować:

1. Rejestrowany i zmienny prefiks.
2. Warstwę poleceń z timeoutem i anulowaniem.
3. Trzy przykładowe sesje, początkowo nawet jako moduły demonstracyjne.
4. Przełączanie sesji po prefiksie przez `1–9`, listę sesji pod `0` i zmianę kolejną przez `Page Up` i `Page Down`, z lustrzanymi skrótami `Ctrl` w aktywnym oknie.
5. Konfigurowalne mapowanie kilku poleceń literowych.
6. Komunikaty przez systemową dostępność i widoczne pole stanu.
7. Jedno wspólne okno listy z Enterem, powrotem i menu kontekstowym.
8. Edytor skrótów, wykrywanie konfliktów i dostępną paletę poleceń.
9. Krótkie, edytowalne szablony komunikatów, w tym osobne polecenia czasu upłyniętego, pozostałego i całkowitego.
10. Zmienne profile klawiatury z chronionym profilem domyślnym.
11. Trzy rodzaje importu i eksportu: mapa klawiszy, konfiguracja oraz pełna kopia.
12. Interfejs systemu automatycznych aktualizacji, podpisany manifest demonstracyjny i test atomowego powrotu, początkowo bez publicznego serwera dystrybucyjnego.
13. Oddzielenie rdzenia od WPF oraz przygotowanie kontraktu dla przyszłego AMC.Host.

Pierwszy prototyp i pierwsze działające wydanie dotyczą wyłącznie Windows. Wersja dla macOS, VoiceOver i ewentualna obsługa Siri są etapem późniejszym.

Stan `alpha.25`: filtr i wyszukiwanie są rozdzielone — `Ctrl+K` zawęża bieżącą listę, a `Ctrl+F` i `Ctrl+Shift+F` otwierają osobne okna zapytania oraz wyników. Lokalne pole i tytuł mówią krótko „Szukaj w TIDAL” albo odpowiednią nazwę usługi; wariant globalny mówi „Szukaj we wszystkich usługach”. Po naciśnięciu zwykłego Enter nazwa usługi staje się chwilowym początkiem dostępnościowej nazwy elementu na głównej liście, dzięki czemu cały kontekst jest czytany w jednej wypowiedzi fokusowej. Po działaniu bezpośrednim wyszukiwanie zapamiętuje konkretny ostatni wynik; po Escape stosuje tę samą jednorazową etykietę również wtedy, gdy usługa nie zmieniła się względem chwili otwarcia wyszukiwania. Po zmianie elementu prefiks znika. Wyniki globalne nadal podają usługę po danych elementu. Widoki Albumy i Playlisty są jednorodne, filtrują właściwy rodzaj zasobu i nie powtarzają słów „album” ani „playlista”; Biblioteka, Ulubione i pozostałe widoki mieszane zachowują rodzaj. Ulubione należą do bieżącej usługi lub biblioteki lokalnej, a Kolejka do aktywnej sesji odtwarzania, dlatego zwykłe wiersze tych widoków nie powtarzają usługi. Wynik jest czytany bez prefiksu „Wyniki wyszukiwania” i bez powtórzonego komunikatu o liczbie pozycji; opcjonalna pomoc elementu wymienia tylko strzałki, Enter i Escape. Tytuł głównego okna zaczyna się od aktualnie odtwarzanego elementu, usługi i widoku; samo otwarcie innego wyniku bez odtwarzania nie zmienia tego tytułu. Ustawienia otwierają się z fokusem na karcie „Ogólne”; Zapisz i Anuluj przywracają fokus do zaznaczonego elementu głównej listy. Enter na bieżącym utworze przełącza odtwarzanie i pauzę, a `Ctrl+Enter` zawsze oznacza „odtwórz teraz”; test rdzenia sprawdza, że ponowne polecenie Play nie przełącza na pauzę. Nawigacja wpisywanymi literami korzysta z głównej nazwy semantycznej elementu, a nie z pierwszego pola dostępnej etykiety. Rdzeń i interfejs pobierają jeden numer wersji z `Directory.Build.props`, a publikacja przenośna powstaje jako pojedynczy, jednoznacznie nazwany plik EXE. Do zamknięcia pierwszego etapu pozostają przede wszystkim lokalna historia wyszukiwania, dostępna paleta poleceń, niskopoziomowe przechwycenie konfigurowalnego prefiksu i test kandydatów z NVDA, JAWS-em oraz menedżerami schowka. Obecne wyszukiwanie korzysta z katalogu demonstracyjnego; prawdziwe zapytania sieciowe pojawią się dopiero z adapterami usług.

Stan `alpha.26`: przejście do Albumów, Playlist, Ulubionych, Biblioteki i Kolejki oraz użycie historii widoków nie wysyła już osobnego podsumowania przez obszar „Stan programu”. Pierwszy element otrzymuje jednorazowy prefiks nazwy widoku, np. „Albumy, Dziwne”, a pusta lista — nazwę „Ulubione, lista pusta”. Po przejściu strzałką prefiks znika. Decyzje integracyjne rozdzielają źródło od celu odtwarzania, traktują prawdziwy TIDAL jako ważny moduł z izolowanymi wynikami, rozpoczynają prawdziwe adaptery od WiiM i Spotify, wykorzystują szersze możliwości BluOS oraz przesuwają kosztowniejszą integrację Sonos na późniejszy etap. Radio przewiduje świadomie uruchamiane nagrywanie bezpośrednich strumieni do prywatnego użytku.

Stan `alpha.27`: zmiana sesji z listy przez `Ctrl+1–9` albo `Ctrl+Page Up/Page Down` nie ściga się już z odczytem fokusu. Numer sesji i nazwa usługi stają się jednorazowym prefiksem zaznaczonego elementu, a po przejściu strzałką znikają. Czas podany w etykiecie pozostaje czasem samego elementu; osobne podsumowanie liczby i łącznego czasu listy nie jest wysyłane. Ewentualne ukrywanie czasu elementu będzie decyzją ustawień pól listy, a nie częścią mechanizmu komunikatów o widoku.

Stan `alpha.28`: wyszukiwanie ma trwałą lokalną historię oddzielną dla każdej usługi i zakresu globalnego. Każdy zakres przechowuje maksymalnie 20 unikatowych zapytań w kolejności od najnowszego; ponowienie istniejącego zapytania przenosi je na początek. Strzałka w dół przy pustym polu rozpoczyna przeglądanie historii, dalsze naciśnięcia przechodzą do starszych wpisów, a strzałka w górę wraca przez nowsze do pustego pola. Historia jest częścią pełnej kopii stanu, ale nie zwykłego eksportu konfiguracji. Następnym małym etapem interfejsu pozostaje dostępna paleta poleceń pod `Ctrl+Shift+K`.

Stan `alpha.29`: `Ctrl+Shift+K` oraz `Shift+K` po prefiksie otwierają dostępną paletę poleceń. Pole tekstowe filtruje podczas pisania, strzałka w dół przechodzi do natywnej listy, Enter wykonuje zaznaczone polecenie, a Escape wraca do głównej listy. Paleta obejmuje także polecenia bez skrótu, pokazuje skróty aktywnego profilu po prefiksie i pozwala wyszukiwać wieloma fragmentami bez wpisywania polskich znaków. Zasada odtwarzania pozostaje jednoznaczna: Enter na bieżącym, już odtwarzanym utworze przełącza pauzę, natomiast `Ctrl+Enter` zawsze wymusza odtwarzanie.

Stan `alpha.30`: polecenie „Odtwórz lub wstrzymaj” zastępuje wcześniejsze „Odtwórz teraz”. `Ctrl+Enter`, przycisk i pierwsza pozycja menu kontekstowego stosują jedną regułę przełączającą do zaznaczenia, a `Spacja` steruje niezależnie aktualnym odtwarzaniem. Paleta podaje skrót okna i skrót po prefiksie. Podczas pisania z fokusem na liście próbuje najpierw przedłużyć frazę, potem rozpocząć nową od wpisanego znaku, a jeśli żaden wariant nie pasuje — czyści filtr.

Stan `alpha.31`: paleta udostępnia wszystkie działające cele okna Ustawienia, włącznie z dokładnymi kontrolkami profili klawiatury, przypisań, list, importu, eksportu, komunikatów i planowanych aktualizacji. Domyślną zasadą bezpieczeństwa jest nawigacja bez zmiany wartości. Wyjątkiem są dwa świadomie dopuszczone przełączniki: komunikaty dostępności oraz szczegółowe podpowiedzi klawiatury. Dynamiczna nazwa podaje stan i skutek Entera, wartość jest od razu zapisywana, a wymuszone potwierdzenie zmiany omija globalne wyłączenie zwykłych komunikatów.

Stan `alpha.32`: lista zdarzeń komunikatów udostępnia przez UI Automation wyłącznie przyjazne nazwy zdarzeń. Surowy tekst i znaczniki szablonu są prezentowane dopiero w osobnym polu edycji, dzięki czemu NVDA nie dopisuje `{slot}` ani `{service}` do nazwy elementu listy, a możliwość pełnej edycji pozostaje zachowana.

Stan `alpha.33`: `Ctrl+O` i menu Plik otwierają wiele lokalnych plików audio w tymczasowej sesji, przypisywanej do pierwszego wolnego miejsca od 4. Sam wybór nie uruchamia dźwięku. Enter, `Ctrl+Enter`, Spacja, przewijanie, głośność i informacje o czasie sterują rzeczywistym wyjściem Windows. Granica `IMediaOutput` pozostaje w rdzeniu, a implementacja `WindowsMediaOutput` w warstwie systemowej, dzięki czemu późniejsze wydzielenie AMC.Host nie wymaga przenoszenia logiki do WPF.

Stan `alpha.34`: `Ctrl+Shift+O` otwiera folder z plikami audio wraz z dostępnymi podfolderami bez samoczynnego odtwarzania. Odkrywanie odbywa się poza wątkiem interfejsu, pomija niedostępne katalogi i łącza mogące tworzyć pętle, filtruje rozpoznane rozszerzenia i zachowuje naturalną kolejność numerowanych nazw. Wersja wprowadziła też lokalne `Ctrl+E`, `Ctrl+R` i `Ctrl+T`, ale ręczny test NVDA wykazał, że standardowa obsługa WPF nie odbiera ich niezawodnie; naprawa przechodzi do `alpha.35`. Skrót oficjalnej aplikacji usługi pozostaje celowo nieustalony do czasu przeglądu całego prefiksu. Wynik testu `alpha.33` potwierdził potrzebę osobnego wyciszania komunikatów transportowych; możliwość ta pozostaje zaplanowaną kategorią ustawień komunikatów.

Stan `alpha.35`: po nieudanym teście standardowej obsługi WPF polecenia `Ctrl+E`, `Ctrl+R` i `Ctrl+T` są przechwytywane wcześniej, na granicy komunikatów okna, z zachowaniem zwykłych poleceń edycji w polach tekstowych. Główna lista obsługuje bez prefiksu przewijanie lewo/prawo, minutowe skoki z Shiftem, głośność pod Ctrl+góra/dół oraz początek i okolice końca. Paleta pokazuje te działające skróty. Zwykłe góra/dół pozostają nawigacją po liście, a „Otwórz w oficjalnej aplikacji” nie ogłasza już zajętego `Ctrl+Shift+O` w menu kontekstowym.

Stan `alpha.36`: eksperymentalne skróty transportowe `alpha.35` zostały wycofane ze zwykłej listy i przeniesione do pierwszego dostępnego widoku odtwarzacza w tym samym oknie. Enter otwiera odtwarzacz, `Ctrl+Enter` działa na liście, F6 pokazuje bieżące odtwarzanie, a Escape przywraca wcześniejszy element. Odtwarzacz udostępnia prawdziwe przyciski oraz okresowo aktualizowane dane tytułu, wykonawcy, sesji, stanu i czasu bez automatycznego wypowiadania każdej sekundy. Tytuł i dostępna nazwa głównego okna zawierają pełny numer wersji.

Stan `alpha.37`: Escape z odtwarzacza zachowuje pozycję ostatnio przeglądaną, a bieżący element jest oznaczany na liście jako „Odtwarzany” lub „Wstrzymany”. Zmiana sesji nie aktualizuje już ukrytego obszaru „Stan programu” przed scalonym komunikatem fokusowym. Lokalne informacje o czasie przechodzą na `Ctrl+Shift+E/R/T`; prefiksowe `Ctrl+E/R/T` pozostają bez zmian. Odtwarzacz przewija o 10 sekund bez modyfikatora, o 30 sekund z Shiftem i o minutę z Ctrl. Zwykła lista nadal zachowuje wszystkie strzałki, a Spacja, `Ctrl+Enter` i prefiks umożliwiają sterowanie bez otwierania odtwarzacza.

Stan `alpha.38`: opcja „Oznajmiaj pozycję po przewijaniu” na karcie Komunikaty oddziela automatyczną informację po przewijaniu od jawnych poleceń czasu. Wyłączenie obejmuje strzałki transportowe, Home i End, ale `Ctrl+Shift+E/R/T` nadal odpowiada. Stan jest trwały, dostępny jako bezpieczny przełącznik palety i przełączany bezpośrednio przez `Ctrl+Shift+G` z wymuszonym krótkim potwierdzeniem.

Planowana kolejność dalszych etapów:

1. Ustabilizowanie głównego okna, list, filtra, kolejki, fokusu i zatwierdzonej mapy klawiatury.
2. Eksperyment dystrybucji MSIX/App Installer, migracja do .NET 10 LTS oraz prototyp podpisanego manifestu komponentów i powrotu po błędzie.
3. Wydzielenie AMC.Host i lokalnego kontraktu polecenie–zdarzenie z adapterem demonstracyjnym.
4. WiiM jako pierwszy realny test wykrywania, komend, głośności, wejść i presetów.
5. Spotify jako pierwsze logowanie OAuth, katalog i test przekazania odtwarzania do urządzenia Connect.
6. TIDAL jako osobny adapter katalogowy z izolowanym widokiem wyników oraz oficjalnym modułem odtwarzania.
7. Cienka wtyczka NVDA korzystająca wyłącznie z kontraktu hosta.
8. Radio internetowe, świadome nagrywanie bezpośrednich strumieni i podstawowe lokalne multimedia.
9. BluOS/Bluesound jako rozbudowany adapter urządzenia i źródeł skonfigurowanych na odtwarzaczu.
10. Apple Music i natywna ścieżka MusicKit dla macOS.
11. Natywny prototyp macOS w Swift/AppKit po ustabilizowaniu kontraktu i zachowania wersji Windows.
12. Frontier Smart po uzyskaniu wspieranego API; Sonos jako późniejsza, osobna integracja chmurowa.

## 15. Otwarte decyzje

1. Czy domyślnym prefiksem ma być `Ctrl+Numeryczny Enter`, czy sam `Numeryczny Enter`, oraz jaki ma być czas wygaśnięcia warstwy.
2. Czy aplikacja pamięta sesję po ponownym uruchomieniu.
3. Czy istnieje od początku playlista „Do odsłuchu”.
4. Które komunikaty mają być mówione, a które sygnalizowane dźwiękiem.
5. Dokładny zakres lokalnego odtwarzania, radia i opcjonalnej integracji z foobar2000.
6. Domyślny odstęp polecenia „w pobliże końca”; roboczo 10 sekund.
7. Ostateczna nazwa aplikacji i identyfikatory pakietów na poszczególnych platformach.

## 16. Zasada dalszej pracy

Dokument jest projektem, a nie zamkniętą specyfikacją. Każda zatwierdzona zmiana powinna być równolegle naniesiona do wersji polskiej i angielskiej. Kod, ustawienia i dokumentacja mają używać stabilnych identyfikatorów poleceń niezależnych od wyświetlanego języka i wybranych skrótów.
