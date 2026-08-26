# Dostępny kontroler multimedialny — koncepcja projektu

Wersja dokumentu: 0.6, aktualny plan projektu

Data aktualizacji: 17 sierpnia 2026 r.

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
| Właściwości i informacje o bieżącym lub zaznaczonym elemencie | `Alt+Enter` | brak stałego przypisania |

Filtr działa tylko na danych już znajdujących się w bieżącej liście i nie wysyła zapytania do usługi. Wyszukiwanie bieżące może odpytać aktualną usługę, a wyszukiwanie globalne odpytuje wszystkie włączone źródła, które zezwalają na wspólną prezentację. Adapter może wymagać osobnego widoku wyników; dotyczy to między innymi prawdziwego adaptera TIDAL, którego treści nie wolno mieszać na jednej liście z treściami podobnych usług. Paleta poleceń jest dostępną, filtrowalną listą funkcji, także tych bez przypisanego skrótu.

`Shift+U` działa jako przełącznik tylko wtedy, gdy adapter potrafi pewnie odczytać aktualny stan. Program mówi odpowiednio „Dodano do ulubionych” albo „Usunięto z ulubionych”. Jeśli stan jest nieznany, aplikacja nie może zgadywać i powinna otworzyć menu z jednoznacznymi czynnościami.

Albumy są ważnym i często używanym widokiem, dlatego otrzymują skrót. Jest to świadomy wyjątek od pełnej symetrii: po prefiksie używamy prostego `A`, a w oknie `Ctrl+Shift+A`, ponieważ `Ctrl+A` bezwzględnie zachowuje standardowe „Zaznacz wszystko”. Nie używamy `Ctrl+Alt+A`, gdyż `Ctrl+Alt` może odpowiadać AltGr i kolidować z wpisywaniem polskiego znaku „ą”. `L` pozostaje Biblioteką, a `B` rezerwą dla możliwych przyszłych Zakładek/Bookmarks. `Shift+A` po prefiksie pozostaje na razie nieprzypisane.

Pozostałe zatwierdzone wcześniej polecenia warstwy zachowują litery `R` — Radio, `M` — Miksy, `H` — Historia, `N` — Teraz odtwarzane i `O` — Wyjścia. Trzy wcześniejsze polecenia informacji zostały połączone w jedno **Właściwości i informacje** pod `Alt+Enter`. Nie ma ono domyślnego przypisania po prefiksie. Otwiera dostępną listę tylko do odczytu, zaczynającą się od tytułu i zawierającą dalej sekcje Odtwarzanie, Techniczne i Źródło. Strzałki czytają wiersze, Ctrl+A zaznacza wszystkie, Ctrl+C kopiuje zaznaczone wiersze, a osobny przycisk kopiuje całość. Escape albo Enter zamyka okno i przywraca fokus. Każdy konflikt rozstrzyga edytor mapy, a polecenie może pozostać bez skrótu i być dostępne z menu oraz palety.

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

Odtwarzacz jest widokiem wewnątrz głównego okna, a nie osobnym oknem modalnym. Enter na utworze lub stacji zapewnia odtwarzanie elementu i otwiera ten widok; jeżeli element już gra, nie przełącza go na pauzę. `Ctrl+Enter` zachowuje działanie bezpośrednie na liście i nie otwiera odtwarzacza. `F6`, polecenie „Teraz odtwarzane” albo `N` po prefiksie pokazuje odtwarzacz bez uruchamiania nowego zaznaczenia. Escape wraca do listy i elementu, z których **ostatnio** otwarto odtwarzacz. Jeżeli użytkownik opuścił odtwarzacz, przeszedł do Ulubionych i użył F6, następny Escape wraca właśnie do Ulubionych. Każda sesja pamięta osobno bieżący widok, fokus w każdym widoku, filtr i to, czy na wierzchu był odtwarzacz; przełączenie sesji odtwarza jej własną powierzchnię. Na listach bieżący utwór otrzymuje dostępny początek „Odtwarzany” albo „Wstrzymany”, dzięki czemu jego stan można rozpoznać bez opuszczania przeglądanej pozycji.

Odtwarzacz jest powierzchnią jednego elementu. `Ctrl+K`, `Ctrl+F` i `Ctrl+Shift+F` nie otwierają z niego filtra ani wyszukiwania, lecz krótko informują, że funkcje te są dostępne na listach. Skróty przejścia do Ulubionych, Playlist, Biblioteki, Albumów i Kolejki świadomie zamykają widok odtwarzacza i otwierają żądaną listę. Działania dotyczące bieżącego elementu pozostają dostępne: Ulubione, Biblioteka, kolejka, „odtwarzaj jako następne”, playlisty oraz `Alt+Enter`.

W odtwarzaczu lewo/prawo przewija o 10 sekund, Shift+lewo/prawo o 30 sekund, Ctrl+lewo/prawo o minutę, góra/dół zmienia głośność o 5%, Shift+góra/dół o 1%, Home przechodzi na początek, a End w pobliże końca. Cyfry `0–9` przechodzą odpowiednio do `0%, 10%, …, 90%` czasu trwania; obejmuje to blok numeryczny przy włączonym Num Lock. Domyślnym komunikatem skoku jest sam procent. Użytkownik może zamiast niego wybrać czas albo procent i czas. Skrót działa wyłącznie w odtwarzaczu, więc cyfry na liście nadal służą jej natywnej nawigacji, a `Ctrl+cyfra` wybiera sesję. Przy nieznanym czasie trwania skok procentowy jest niedostępny. `Alt+lewo` z odtwarzacza wraca do ostatniej listy, natomiast na powierzchni przeglądania `Alt+lewo/prawo` przechodzi przez eksperymentalną historię widoków. Ctrl+Shift+strzałki pozostają wolne do czasu ustalenia potrzeb. Tab przechodzi przez rzeczywiste przyciski odtwarzania, przewijania, głośności i powrotu. Funkcje zależne od możliwości sesji, np. nagrywanie radia, pojawią się później jako warunkowe kontrolki i polecenia; nie otrzymują jeszcze stałego skrótu. Docelowo przypisania odtwarzacza będą konfigurowalne obok profilu prefiksu.

`Ctrl+J` otwiera osobne okno „Skocz do czasu”. Sama liczba oznacza minuty, dwie części — minuty i sekundy, a trzy — godziny, minuty i sekundy. `Ctrl+Shift+J` otwiera „Skocz do procentu” i przyjmuje wartość `0–100`. Oba skróty działają wyłącznie w odtwarzaczu, podobnie jak szybkie skoki cyframi. Wybranie tych poleceń z menu lub palety poza odtwarzaczem podaje wskazówkę użycia `F6` i nie zmienia pozycji. Rozdzielenie zapobiega zgadywaniu, czy `35` oznacza minuty, czy procent. Ewentualne odpowiedniki po prefiksie pozostają do sprawdzenia razem z całą warstwą.

Regulacja tempa korzysta z mapy zgodnej z YouTube: `Shift+,` zwalnia, `Shift+.` przyspiesza, a `Ctrl+.` przywraca normalne 1,00×. Pierwszy zakres to 0,50–2,00× co 0,25. Zmiana dotyczy tempa, nie wysokości dźwięku. Wartość należy do sesji odtwarzania i pozostaje aktywna przy zmianie utworu; adapter, który nie zapewnia tej funkcji, ma ją jawnie zgłosić jako niedostępną. Skróty okienne działają w odtwarzaczu, a ewentualne przypisania po prefiksie pozostają osobną decyzją mapy.

### 6.2. Pozycje wznowienia, Zakładki i zasoby globalne

Od `alpha.57` lokalne pozycje są zapisywane osobno dla każdego pliku razem z odciskiem obejmującym rozmiar i datę modyfikacji. Stan jest utrwalany nie częściej niż co 15 sekund oraz przy prawidłowym zamknięciu, bez modyfikowania samego pliku. Ponowne wybranie pliku rozpoczyna go od zapamiętanej pozycji, ale start programu nigdy sam nie uruchamia dźwięku. Późniejsze ustawienie określi, czy krótkie utwory muzyczne mają wznawiać się tak samo jak długie nagrania i podcasty. Streaming zachowuje własną pozycję tylko wtedy, gdy oficjalny adapter usługi ją udostępnia; AMC nie będzie tworzyć ukrytej, pozornej synchronizacji.

Zakładka jest lokalnym rekordem AMC wskazującym stabilny identyfikator sesji, element, pozycję i opcjonalną nazwę. Może więc wskazywać plik lokalny albo pozycję w materiale streamingowym, jeśli adapter potrafi ponownie otworzyć ten sam element i przewinąć go. Zakładki są z natury globalnym indeksem aplikacji, ale nie kopiują ani nie przejmują treści usługi. Od `alpha.68` w odtwarzaczu `B` tworzy szybką zakładkę, `Shift+Page Up/Down` przechodzi po zakładkach bieżącego materiału, a `Ctrl+B` otwiera globalną listę. Od `alpha.70` `Ctrl+Shift+B` tworzy zakładkę nazwaną. Enter na pozycji listy przełącza sesję, otwiera element i ustawia czas. `Delete` usuwa rekord zakładki; `Shift+Delete` nie może z tego widoku usunąć źródłowego pliku. Zwykłe `B` na innych listach pozostaje nawigacją literową.

Ulubione i playlisty pozostają własnością konkretnej usługi albo lokalnej biblioteki, ponieważ ich stan, uprawnienia i identyfikatory pochodzą z danego adaptera. AMC może udostępnić globalny widok „Wszystkie ulubione” jako agregację odsyłaczy oraz własne „Kolekcje AMC” mieszające odsyłacze z wielu usług, lecz nie będzie przedstawiać ich jako jednej zsynchronizowanej listy ulubionych w serwisach. Kolejka jest związana z aktywnym celem odtwarzania; globalne dodawanie ma sens dopiero wtedy, gdy host potrafi niezawodnie przekazać kolejny element między usługami lub urządzeniami.

Na Windows domyślnym wyjściem pozostaje współdzielone WASAPI, aby AMC współistniał z NVDA. Tryb wyłączny, bit-perfect i natywne DSD nie wchodzą do podstawowego toru. Ewentualny późniejszy tryb zaawansowany musi być jawny, odwracalny i nie może po cichu odbierać dźwięku czytnikowi ekranu. foobar2000 może kiedyś działać jako zewnętrzny adapter; jego komponentów nie traktujemy jako bibliotek możliwych do bezpośredniego wbudowania bez osobno sprawdzonych źródeł i licencji.

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
| `Delete` | usuń element z bieżącej playlisty, kolejki, ulubionych albo biblioteki, jeśli działanie jest jednoznaczne |
| `Backspace` | przejdź o poziom wyżej; nigdy nie usuwaj elementu |
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

### 7.6. Lokalna Biblioteka i prawdziwe foldery

Lokalna Biblioteka rozdziela **katalog AMC** od fizycznego miejsca przechowywania. Domyślny tryb jest referencyjny: AMC zapisuje stabilny rekord i kanoniczną ścieżkę, ale nie kopiuje pliku, nie zmienia jego nazwy i nie tworzy ukrytej własnej kopii. `Ctrl+O` dodaje wskazane pliki w ten sposób. `Ctrl+Shift+O` rejestruje folder jako trwałe **źródło Biblioteki** i włącza do niej wszystkie rozpoznane pliki z tego folderu oraz podfolderów. Źródła są synchronizowane przy starcie, po zdarzeniach systemu plików i jawnie przez `F5`. Brakujący plik jest oznaczany jako niedostępny, nie kasowany z katalogu, dzięki czemu historia, zakładki i pozycja wznowienia pozostają zachowane.

Użytkownik może zarejestrować wiele źródeł: zwykły katalog lokalny, dysk zewnętrzny, udział sieciowy albo folder zarządzany przez iCloud Drive lub OneDrive. AMC nie implementuje własnej synchronizacji takiego źródła. Dostawca chmurowy odpowiada za przesyłanie, a adapter lokalny rozpoznaje dostępność pliku i placeholder Cloud Files. Samo indeksowanie nie powinno pobierać całej kolekcji z chmury; odtworzenie może zażądać pobrania konkretnego pliku i musi oznajmić oczekiwanie albo błąd. Punkty ponownej analizy są kontrolowane, aby skan nie wchodził w pętle.

Widok **Foldery** odzwierciedla rzeczywistą hierarchię bez używania problematycznego wielopoziomowego TreeView. Jest zwykłą listą bieżącego poziomu: foldery i pliki są normalnymi wierszami, Enter wchodzi do folderu albo otwiera plik, Backspace lub jawne polecenie „Folder nadrzędny” wraca o poziom, a litery przeszukują wyłącznie widoczny poziom. Płaska **Biblioteka** pozostaje równoległym widokiem wszystkich rekordów. Albumy, Wykonawcy i Gatunki powstają z dostępnych tagów, lecz nie zastępują Folderów i nie ukrywają plików bez kompletnych metadanych.

Usunięcie źródła Biblioteki usuwa z AMC jedynie odwołania należące wyłącznie do tego źródła i nigdy nie kasuje katalogu na dysku. `Delete` wyklucza rekord z aktywnej Biblioteki i zapisuje to wykluczenie, natomiast `Shift+Delete` po potwierdzeniu używa systemowego Kosza. Przywrócenie pliku pod tą samą ścieżką przywraca dostępność tego samego rekordu. Zmiana nazwy lub przeniesienie obserwowane podczas działania AMC zachowuje tożsamość rekordu; dopasowanie ruchu wykonanego przy zamkniętej aplikacji na podstawie odcisku pliku pozostaje dalszym etapem. Opcjonalna **zarządzana Biblioteka AMC**, kopiująca lub przenosząca importowane pliki do jednego wskazanego katalogu, może powstać później jako świadomie włączany tryb. Jej katalog również może znajdować się w chmurze, ale synchronizację nadal wykonuje dostawca, nie AMC.

Baza indeksu, aktywny stan i pliki robocze pozostają lokalne w AppData i nie są otwierane równocześnie przez synchronizator chmurowy. Do chmury można zapisywać atomowe eksporty ustawień, playlist, zakładek oraz pełne kopie AMC. Repozytorium źródłowe, `.git`, `obj`, `bin` i bieżący katalog publikacji także pozostają poza folderami synchronizowanymi; historię kodu zapewnia GitHub.

### 7.7. Kontrakt spójności list, wyszukiwania i adapterów

Jedna semantyka interfejsu obowiązuje lokalne pliki, radio, podcasty, urządzenia i usługi streamingowe. Adapter dostarcza dane oraz deklaruje możliwości; nie tworzy własnych skrótów, kolejności odczytu ani odmiennego zachowania fokusu. Wspólna warstwa prezentacji buduje zwykłe listy, wyniki wyszukiwania, menu kontekstowe, paletę poleceń i komunikaty.

Kontrakt obejmuje co najmniej:

- te same znaczenia Enter, `Ctrl+Enter`, Spacji, Escape, menu kontekstowego, kopiowania, Kolejki, Ulubionych, Biblioteki i playlist wszędzie, gdzie adapter deklaruje daną możliwość;
- tę samą główną nazwę semantyczną, nawigację literową, zaznaczanie wielokrotne, przywracanie fokusu i porcjowanie zarówno na liście głównej, jak i w wynikach;
- tę samą konfigurowalną kolejność pól; brakująca wartość jest pomijana, a nie zastępowana zgadywaną wartością;
- strzałkę w lewo jako wspólną krótką informację, `Alt+Enter` jako pełne właściwości oraz `Ctrl+C` i `Ctrl+Shift+C` jako odpowiednio nazwę i publiczną lokalizację albo prawdziwy plik lokalny;
- działania bezpośrednie w wyszukiwaniu bez zamykania okna, jeśli ta sama czynność jest dostępna na liście głównej i nie wymaga następnego okna modalnego; wybór playlist zamyka wyszukiwanie i otwiera właściwy menedżer;
- nazwę usługi w wynikach mieszanych i globalnych, ale bez jej zbędnego powtarzania na jednorodnej liście jednej sesji;
- wspólne komunikaty ładowania, braku danych, niedostępnej możliwości, błędu, częściowego sukcesu i końca listy.

Spójność nie oznacza udawania identycznych możliwości. Jeśli usługa nie zwraca bitrate, nie pozwala przewijać, nie ma kolejki albo wymaga izolowanych wyników, adapter jawnie deklaruje brak lub ograniczenie. Interfejs zachowuje ten sam skrót i odpowiada „Niedostępne w tej usłudze” albo ukrywa nieosiągalną czynność zgodnie z ustawieniem użytkownika; nie wykonuje innego polecenia pod tym samym klawiszem. Każdy prawdziwy adapter musi przejść wspólne testy kontraktowe dla listy, wyszukiwania, fokusu, komunikatów, błędów, stronicowania i wszystkich zadeklarowanych działań przed włączeniem do stabilnego wydania.

## 8. Wybór playlisty

`Shift+P` w warstwie albo lokalne `Ctrl+Shift+P` otwiera niewielkie modalne okno:

- pole filtrowania;
- lista wszystkich playlist bieżącej usługi z informacją „zawiera” albo „nie zawiera”;
- Spacja przełącza przynależność elementu do wskazanej playlisty;
- Enter zatwierdza wszystkie zmiany;
- Insert lub `Ctrl+N` tworzy nową playlistę, F2 zmienia jej nazwę, a Delete usuwa playlistę bez usuwania multimediów;
- `Ctrl+K` przechodzi do filtra playlist; Escape najpierw czyści filtr, a przy pustym filtrze anuluje całe okno;
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
| `Ctrl+Shift+C` | kopiuj pełną ścieżkę lokalną albo publiczne łącze do elementu w usłudze |
| `Ctrl+Shift+U` | dodaj do Ulubionych albo usuń z Ulubionych |
| `Ctrl+Shift+P` | otwórz wybór playlist i zmień przynależność |
| `Ctrl+Shift+Q` | dodaj do kolejki |
| `Ctrl+Shift+L` | dodaj do biblioteki |
| `Ctrl+O` | otwórz jeden lub wiele lokalnych plików audio |
| `Ctrl+Shift+O` | otwórz folder z plikami audio wraz z podfolderami |
| `Alt+1` na liście lokalnej | pokaż Foldery Biblioteki |
| `Alt+2` na liście lokalnej | pokaż Wszystkie pliki alfabetycznie |
| `Alt+3` na liście lokalnej | pokaż trwałą Kolejność własną |
| `Alt+Strzałka w górę/w dół` w Kolejności własnej | przesuń plik albo ciągły zaznaczony blok o jedną pozycję |
| `F5` w sesji lokalnej | ponownie przeskanuj wszystkie dostępne źródła |
| `Ctrl+F5` | otwórz Menedżera Biblioteki lokalnej |
| `F2` na lokalnej liście | zmień tylko trwałą nazwę wyświetlaną w AMC |
| `Shift+F2` na lokalnej liście | zmień rzeczywistą nazwę pliku na dysku, zachowując rozszerzenie i dane AMC |
| `F6` | otwórz widok odtwarzacza |
| `Strzałka w lewo/prawo` w odtwarzaczu | cofnij albo przewiń o 10 sekund |
| `Shift+Strzałka w lewo/prawo` w odtwarzaczu | cofnij albo przewiń o 30 sekund |
| `Ctrl+Strzałka w lewo/prawo` w odtwarzaczu | cofnij albo przewiń o minutę |
| `Strzałka w górę/dół` w odtwarzaczu | zmień głośność o 5% |
| `Shift+Strzałka w górę/dół` w odtwarzaczu | zmień głośność o 1% |
| `Home` w odtwarzaczu | przejdź na początek utworu |
| `End` w odtwarzaczu | przejdź 10 sekund przed końcem utworu |
| `0–9` w odtwarzaczu | przejdź do 0–90% czasu trwania co 10% |
| `Ctrl+J` w odtwarzaczu | wpisz dokładny czas i przejdź do niego |
| `Ctrl+Shift+J` w odtwarzaczu | wpisz procent od 0 do 100 i przejdź do niego |
| `Ctrl+Shift+E`, `Ctrl+Shift+R`, `Ctrl+Shift+T` | podaj czas od początku, pozostały albo całkowity |
| `Ctrl+Shift+G` | chwilowo włącz lub wyłącz wszystkie automatyczne komunikaty odtwarzacza |
| `Ctrl+D` | pobierz offline wewnątrz usługi, jeśli obsługiwane |
| `Ctrl+Shift+D` | pobierz do pliku lokalnego; funkcja eksperymentalna, domyślnie wyłączona |
| `Delete` | usuń z bieżącej playlisty, kolejki, ulubionych lub biblioteki; z potwierdzeniem albo możliwością cofnięcia |
| `Backspace` | przejdź do poziomu nadrzędnego; w polu tekstowym usuń znak |
| `Ctrl+Z` | cofnij ostatnią zmianę przynależności do Ulubionych, Biblioteki lub Kolejki albo stan „Odtwórz jako następne” |
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

Domyślne komunikaty mają być krótkie. W pierwszej wersji nie wprowadzamy osobnych profili „krótki”, „normalny” i „szczegółowy”. Zamiast nich użytkownik może edytować szablon każdego komunikatu, wyłączyć go albo przywrócić ustawienie domyślne. Oddzielna globalna opcja szczegółowych podpowiedzi klawiatury obejmuje filtr oraz wyszukiwanie bieżące i globalne; jest domyślnie wyłączona, nie zmienia treści komunikatów zdarzeń, a na wynikach wymienia tylko strzałki, Enter i Escape. Automatyczne komunikaty odtwarzacza mają nadrzędny przełącznik pod `Ctrl+Shift+G` oraz cztery zachowywane niezależnie kategorie: skoki cyframi, przewijanie strzałkami, głośność i odtwarzanie/pauzę. Nadrzędne wyciszenie nie zmienia zaznaczenia kategorii. Nie obejmuje informacji o czasie wywołanych na żądanie ani komunikatów o błędzie lub niedostępności. Dla skoku cyfrą można dodatkowo wybrać sam procent, sam czas albo obie wartości.

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

- **WiiM**: publiczne lokalne API HTTPS zapewnia informacje o urządzeniu, stan i metadane odtwarzania, transport, przewijanie, głośność, wyciszenie, tryby powtarzania, EQ, alarmy, wejścia, wyjścia i 12 presetów. Rozpoznaje Spotify Connect i TIDAL Connect jako aktywne tryby, ale nie dokumentuje przeglądania katalogów usług ani uniwersalnego wyszukiwania znanego z WiiM Home. Pierwszy adapter WiiM jest więc adapterem urządzenia i presetów, a nie zastępczym API TIDAL. Aplikacja WiiM Home przedstawia ponadto zbiorczy ekran z presetami, ostatnio odtwarzanymi treściami i Ulubionymi rozdzielonymi według usług, lecz AMC nie zakłada, że wszystkie te listy należą do urządzenia albo są dostępne przez lokalne API. [HTTP API for WiiM Products](https://www.wiimhome.com/pdf/HTTP%20API%20for%20WiiM%20Products.pdf), [WiiM Home App User Guide](https://wiimhome.com/pdf/WiiM%20Home%20App%20User%20Guide.pdf).
- **BluOS/Bluesound**: lokalne API HTTP/XML pozwala dodatkowo przeglądać i wyszukiwać źródła skonfigurowane na odtwarzaczu, w tym TIDAL, stronicować wyniki, pobierać menu kontekstowe, wykonywać działania Ulubionych i kolejki, zarządzać kolejką, presetami i grupami. Jest to pierwszy kandydat do sesji, w której urządzenie może pośredniczyć zarówno w katalogu, jak i odtwarzaniu. Presety dodaje i usuwa się w oficjalnym kontrolerze BluOS; AMC może je listować i uruchamiać. [BluOS Custom Integration API 1.7](https://bluos.io/wp-content/uploads/2025/06/BluOS-Custom-Integration-API_v1.7.pdf).
- **Spotify**: Web API obejmuje wyszukiwanie, bibliotekę, playlisty, kolejkę, bieżące odtwarzanie i urządzenia Spotify Connect oraz pozwala przenosić odtwarzanie i sterować transportem. Funkcje odtwarzacza wymagają Premium; urządzenie może być oznaczone jako ograniczone i wtedy nie przyjmuje poleceń. Pierwszy prawdziwy OAuth powinien wykorzystać Authorization Code z PKCE, obsłużyć limity trybu deweloperskiego oraz ponowną autoryzację po wygaśnięciu tokenu odświeżania. [Spotify Web API](https://developer.spotify.com/documentation/web-api), [Spotify scopes](https://developer.spotify.com/documentation/web-api/concepts/scopes), [Spotify quota modes](https://developer.spotify.com/documentation/web-api/concepts/quota-modes).
- **Apple Music**: Apple Music API udostępnia katalog i osobistą bibliotekę, wyszukiwanie, albumy, utwory, wykonawców, playlisty, teledyski, stacje, oceny i Ulubione, rekomendacje oraz historię. Na macOS odtwarzanie korzysta natywnie z MusicKit dla Swift; na Windows należy osobno zweryfikować dostępność i zgodność dostępnościową MusicKit on the Web. [Apple Music API](https://developer.apple.com/documentation/applemusicapi), [MusicKit](https://developer.apple.com/musickit/).
- **TIDAL**: API i OAuth 2.1 mogą dostarczyć katalog oraz zasoby użytkownika w granicach przyznanych zakresów, ale odtwarzanie musi używać oficjalnego modułu TIDAL Player. Publiczne TIDAL Connect jest przeznaczone dla partnerów sprzętowych. Adapter pozostaje ważnym, osobnym modułem AMC, lecz wymaga prezentacji treści TIDAL w izolowanym widoku, oznaczenia marki, przycisku otwarcia w TIDAL, minimalnego przechowywania danych i formalnego sprawdzenia trybu produkcyjnego. Nie łączymy treści TIDAL z podobnymi usługami w jednej liście i nie udostępniamy nagrywania ani eksportu strumienia. [TIDAL authorization](https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization), [TIDAL Developer Terms](https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms), [TIDAL Design Guidelines](https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines), [TIDAL Connect](https://developer.tidal.com/documentation/connect).
Warstwa przypominająca dostępnego klienta WhatsApp, która opakowuje TIDAL Web i naprawia samą nawigację klawiaturą, jest technicznie możliwa tylko jako ostrożny eksperyment. Nie może automatycznie wydobywać katalogu, playlist ani historii z DOM, ponieważ oficjalne zasady zabraniają scrapingu i automatycznego indeksowania TIDAL. Dopuszczalnym awaryjnym wariantem AMC pozostaje otwarcie oficjalnego odtwarzacza WWW i zapewnienie użytkownikowi przejścia do niego; właściwy adapter korzysta z oficjalnego API i modułu Player. Eksperymentalnej nakładki dostępnościowej nie traktujemy jako podstawowego adaptera bez pisemnego potwierdzenia TIDAL.

#### 13.2.2. Ulubione i historia zależne od usługi

AMC ujednolica polecenia i sposób nawigacji, ale nie ujednolica na siłę znaczenia danych dostawców. Polecenia `favorites.open` i `favorites.toggle` trafiają do adaptera bieżącej usługi, który zachowuje jej natywną semantykę oraz identyfikatory. Ewentualny widok zbiorczy jest wyłącznie prezentacją elementów z wyraźnym wskazaniem źródła; nie tworzy wspólnego stanu Ulubionych i nie kopiuje automatycznie elementów między kontami.

- **TIDAL** przedstawia Ulubione jako typowane kolekcje użytkownika: osobno utwory, albumy, wykonawcy, playlisty i obsługiwane materiały wideo. Dodanie lub usunięcie elementu modyfikuje odpowiednią kolekcję konta TIDAL. Semantycznie jest to bliższe zapisanej kolekcji lub Bibliotece Apple Music niż dodatkowemu znacznikowi „lubię”. [TIDAL API Reference](https://tidal-music.github.io/tidal-api-reference/).
- **Apple Music** rozdziela Bibliotekę i stan Ulubionych. Element może znajdować się w Bibliotece i nie być Ulubiony. Oznaczenie utworu jako Ulubionego dodaje go także do systemowej playlisty Favorite Songs, natomiast albumy i playlisty mogą być filtrowane według stanu Ulubionych. Nie utożsamiamy Ulubionych z ocenami ani z samym dodaniem do Biblioteki. [Add Resource to Favorites](https://developer.apple.com/documentation/applemusicapi/add-resource-to-favorites), [Library Albums attributes](https://developer.apple.com/documentation/applemusicapi/libraryalbums/attributes-data.dictionary).
- **Pliki lokalne** zachowują osobne Ulubione AMC. Nie są one wysyłane do TIDAL ani Apple Music.

Historia ma dwa jawnie rozdzielone źródła:

1. **Odtwarzane przez AMC** — trwała lokalna historia prowadzona osobno dla każdej sesji i usługi. Obejmuje tylko odtwarzanie rozpoczęte albo zaobserwowane przez AMC i nie jest wysyłana do dostawcy.
2. **Historia usługi** — dodatkowe, tylko wtedy dostępne widoki pochodzące z oficjalnego API. Apple Music rozdziela ostatnio odtwarzane utwory, inne zasoby, stacje i heavy rotation; „ostatnio dodane” jest aktywnością Biblioteki, a nie historią odtwarzania. [Apple Music History](https://developer.apple.com/documentation/applemusicapi/history), [Recently Played Tracks](https://developer.apple.com/documentation/applemusicapi/get-v1-me-recent-played-tracks). Dla TIDAL nie zakładamy istnienia równoważnej pełnej historii konta, dopóki oficjalne API i przyznane zakresy nie zostaną potwierdzone w działającym adapterze.

Widoki nie mieszają tych historii bez oznaczenia pochodzenia i nie zastępują jednej drugą. Brak historii usługi nie blokuje lokalnej historii AMC.

Widok **Ulubione TIDAL** nie jest jedną płaską listą. Pierwszy poziom zawiera kategorie **Utwory**, **Albumy**, **Wykonawcy**, **Playlisty** i, jeśli konto oraz API je udostępniają, **Wideo**. Enter otwiera kategorię, Escape wraca o jeden poziom, a nawigacja literami i filtrowanie dotyczą wyłącznie widocznego poziomu. `favorites.toggle` sam wybiera właściwą kolekcję na podstawie rodzaju zaznaczonego zasobu. Własne playlisty użytkownika i playlisty tylko dodane do Ulubionych zachowują informację o właścicielu i nie stają się tym samym stanem.

Wspólny model rdzenia rozdziela **zapisaną kolekcję**, **przynależność do Biblioteki** i **znacznik Ulubionego**. TIDAL może odwzorować zapisaną kolekcję i Ulubione na tę samą natywną operację, natomiast Apple Music zachowuje Bibliotekę i Ulubione oddzielnie. `library.open` nie tworzy w TIDAL drugiej, fikcyjnej kopii tych samych danych: po teście prawdziwego konta otworzy natywny nadrzędny widok kolekcji albo jednoznacznie skieruje do Ulubionych TIDAL.

Kolejność najbliższych rzeczywistych integracji po ustabilizowaniu lokalnego interfejsu to: **WiiM** jako adapter urządzenia i presetów, następnie **TIDAL**, a potem **Apple Music** jako adaptery katalogów i kont. **BluOS/Bluesound** pozostaje w planie, lecz jego wdrożenie odkładamy do czasu dostępu do prawdziwego urządzenia, na którym można przeprowadzić testy. WiiM nie zastępuje API TIDAL ani Apple Music; sesja może później połączyć źródło z celem odtwarzania tylko przez oficjalnie dostępną drogę.

- **YouTube**: pierwszy adapter działa na żądanie i nie synchronizuje konta. Publiczne wyszukiwanie filmów, transmisji i playlist korzysta z YouTube Data API oraz klucza projektu. Lokalna Biblioteka YouTube w AMC nie jest kopią serwisu: zawiera pozycje świadomie dodane do Ulubionych, własne lokalne playlisty oraz lokalną historię odtwarzania. Te dane wystarczają do podstawowej pracy bez logowania. OAuth pozostaje opcjonalnym późniejszym rozszerzeniem, uruchamianym tylko wtedy, gdy użytkownik świadomie zechce pobrać subskrypcje, playlisty lub polubienia z konta; brak logowania nie ogranicza podstawowego adaptera. Odtwarzanie i sterowanie transportem korzysta z widocznego oficjalnego IFrame Player API. AMC przedstawia metadane, wyniki i polecenia w dostępnym interfejsie przypominającym podcasty, ale nie ukrywa odtwarzacza w tle. Zgodnie z zasadami API oficjalny adapter nie pobiera materiałów, nie oddziela dźwięku i nie nagrywa fragmentów filmów ani transmisji. Nagrywanie pozostaje funkcją źródeł, które jawnie na to pozwalają, np. bezpośredniego strumienia radiowego lub własnego pliku. [YouTube Data API](https://developers.google.com/youtube/v3/getting-started), [YouTube IFrame Player API](https://developers.google.com/youtube/iframe_api_reference), [YouTube API Services Developer Policies](https://developers.google.com/youtube/terms/developer-policies).

  Narzędzia ekstrakcyjne, takie jak yt-dlp, nie stają się oficjalnym adapterem YouTube ani obowiązkową zależnością AMC. Ich obsługa YouTube wymaga częstych poprawek wskutek zmian odtwarzacza, JavaScriptu i tokenów PO, może tracić formaty bez ostrzeżenia i przy użyciu ciasteczek konta niesie ryzyko ograniczenia konta. Możemy w przyszłości badać osobny, domyślnie wyłączony moduł „Narzędzia multimedialne” dla legalnie zapisywalnych źródeł. Taki moduł działa poza procesem głównym, aktualizuje się niezależnie, nie otrzymuje tokenów OAuth ani ciasteczek oficjalnych adapterów, a jego awaria nie może zakłócić wyszukiwania lub odtwarzania. [Kanały wydań yt-dlp](https://github.com/yt-dlp/yt-dlp/blob/master/README.md#update-channels), [uwagi o ekstraktorze YouTube](https://github.com/yt-dlp/yt-dlp/wiki/Extractors).

- **Sonos**: chmurowe Control API z OAuth pozwala odkrywać gospodarstwa domowe, grupy i odtwarzacze, odczytywać stan, sterować transportem, przewijaniem i głośnością oraz uruchamiać Sonos Favorites i playlisty Sonos. Nie zastępuje katalogowego API istniejących usług muzycznych, wymaga publicznego zwrotnego adresu HTTPS i ma większy koszt integracyjny. Sonos pozostaje w planie, ale po WiiM, Spotify, TIDAL, BluOS, Apple Music, radiu i multimediach lokalnych. [Sonos Control API](https://docs.sonos.com/reference/about-control-api), [Sonos authorization](https://docs.sonos.com/docs/authorize).
- **Frontier Smart**: producent potwierdza NetRemote API, SDK i możliwość budowania własnych aplikacji przez partnerów sprzętowych, ale nie publikuje kompletnej wspieranej dokumentacji konsumenckiej. Stabilny adapter wymaga dostępu partnerskiego; ewentualny adapter społecznościowy musi być osobno oznaczony jako eksperymentalny i nie może być podstawą pierwszego wydania. [Frontier AURIA](https://www.frontiersmart.com/product/auria/), [Frontier customer area](https://www.frontiersmart.com/customer-area/).

Adapter deklaruje osobno co najmniej: zakres wyszukiwania, politykę prezentacji wyników, odczyt i zapis Ulubionych, Bibliotekę, playlisty, możliwość odtworzenia, możliwości kolejki, cele odtwarzania, transport, przewijanie, głośność, wejścia, presety, grupy, pobieranie offline wewnątrz usługi oraz legalny eksport. Interfejs nie zgaduje brakujących możliwości.

### 13.3. Lokalne multimedia i radio

Lokalny moduł odtwarzania obejmuje docelowo otwieranie plików i folderów, metadane, Bibliotekę, kolejkę, podstawowe popularne formaty, wybór urządzenia, odtwarzanie bez przerw i ReplayGain. Na Windows domyślne wyjście dźwięku powinno pracować w trybie współdzielonym, aby nie wyciszać NVDA i pozostałych dźwięków. Tryb wyłączny może pojawić się później jako funkcja zaawansowana z wyraźnym ostrzeżeniem.

Pierwszy krok wdrożony w `alpha.33` rozdziela neutralny interfejs wyjścia dźwięku w rdzeniu od implementacji Windows. `Ctrl+O` ładuje pliki do nietrwałej sesji lokalnej, a systemowy odtwarzacz Windows realizuje odtwarzanie, pauzę, pozycję i głośność w trybie współdzielonym. `Alpha.34` dodaje rekursywne otwieranie folderu przez `Ctrl+Shift+O`, naturalne porządkowanie nazw, pomijanie duplikatów oraz lokalne `Ctrl+E`, `Ctrl+R` i `Ctrl+T`. Nie jest to jeszcze pełna Biblioteka: zapis listy, odtwarzanie bez przerw, ReplayGain, wybór urządzenia i opcjonalne kodeki pozostają późniejszymi etapami.

Radio internetowe jest osobnym adapterem rdzenia i korzysta z tych samych sesji, Ulubionych, historii oraz poleceń transportowych. Powinno obsłużyć bezpośrednie strumienie, M3U/M3U8, PLS i XSPF, metadane stacji, ponawianie po zerwaniu i wyszukiwanie. Import M3U8 musi odróżniać zwykłą listę stacji od manifestu HLS zawierającego znaczniki `#EXT-X-`. Mechanizmy Free Radio można wykorzystać po analizie kodu i licencji, bez przenoszenia całego odtwarzania do procesu NVDA. Nagrywanie radia może być świadomie uruchamianą funkcją lokalną do prywatnego użytku: zapisuje dostępny bezpośredni strumień bez obchodzenia DRM, nie uruchamia się automatycznie, nie dotyczy TIDAL, Spotify ani Apple Music i pozostawia użytkownikowi odpowiedzialność za zgodność z prawem właściwym dla miejsca użycia.

WiiM Home oferuje także usługę **Open Network Stream**: pojedyncze bezpośrednie adresy radia lub podcastu, import list M3U, M3U8 i PLS, edycję, sortowanie, przypisywanie do presetów oraz eksport M3U. Publiczna dokumentacja opisuje tę funkcję jako element aplikacji WiiM Home, lecz nie ustanawia wspieranego lokalnego API do dwukierunkowej synchronizacji całej listy. Dlatego AMC utrzymuje własną przenośną listę strumieni, może wysłać wybrany URL bezpośrednio do odtwarzacza WiiM i może wymieniać listy z WiiM Home przez M3U. Nie odczytuje prywatnego magazynu aplikacji i nie obiecuje automatycznej synchronizacji, dopóki producent nie udostępni odpowiedniego API. [Using the Open Network Stream Service](https://faq.wiimhome.com/en/support/solutions/articles/72000636011-tutorial-using-the-open-network-stream-service).

W adapterze WiiM rozdzielamy zatem cztery powierzchnie: **Presety urządzenia**, **Ostatnio odtwarzane WiiM** dostępne tylko w zakresie potwierdzonym przez API, **Ulubione usług** należące do odpowiednich kont oraz **Strumienie sieciowe AMC**. Jeśli „Ostatnio odtwarzane” WiiM Home nie jest dostępne programistycznie, AMC pokazuje własną historię tego, co uruchomiło na WiiM, bez przedstawiania jej jako kompletnej historii aplikacji producenta.

Obowiązuje zasada **źródło jest nadrzędne**. Jeżeli oficjalne API urządzenia albo usługi pozwala odczytać i zmienić presety, historię, kolejkę, Ulubione, playlisty, strumienie lub konfigurację, AMC pracuje bezpośrednio na tych danych zamiast tworzyć równoległą kolekcję wymagającą ponownego ustawiania w telefonie i komputerze. Dane urządzenia są nadrzędne dla ustawień i zasobów urządzenia, a konto usługi dla katalogu użytkownika. AMC przechowuje tylko bezpieczny cache oraz własne dane, których źródło nie udostępnia. Cache zachowuje natywne identyfikatory i nie nadpisuje źródła po odzyskaniu połączenia bez sprawdzenia aktualnego stanu.

Adapter deklaruje osobno odczyt i zapis każdej kategorii. Przy dostępie tylko do odczytu AMC pokazuje stan źródła bez udawania synchronizacji. Przy pełnym dostępie zmiana w AMC trafia do urządzenia lub usługi i staje się widoczna również w oficjalnej aplikacji. Dopiero brak wspieranego API uruchamia jednoznacznie oznaczoną warstwę lokalną oraz import i eksport jako rozwiązanie przenośne. Jeżeli WiiM udostępni kiedyś listę Open Network Stream, stanie się ona nadrzędna, a obecny model M3U pozostanie sposobem wymiany i kopii zapasowej.

Format eksportu Ulubionych aplikacji VRadio jest przewidziany jako kolejny adapter importu i eksportu radia. Adapter mapuje stacje, ich stabilne identyfikatory, alternatywne strumienie oraz grupy Ulubionych do neutralnego modelu AMC. Najpierw stosuje ścisły parser JSON, a kontrolowane odzyskiwanie danych uruchamia wyłącznie dla rozpoznanych, możliwych do bezpiecznego naprawienia błędów. Podejrzane adresy i uszkodzone rekordy są pomijane z raportem, a tokeny, podpisane adresy i inne dane prywatne nigdy nie trafiają do logu. Eksport odtwarza strukturę akceptowaną przez VRadio bez ujawniania danych spoza wybranych stacji. Prywatny plik użyty do analizy nie jest częścią repozytorium ani danych testowych.

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

Stan `alpha.39`: cyfry `0–9` w widoku odtwarzacza przechodzą do `0–90%` czasu trwania. Działają także cyfry numeryczne przy włączonym Num Lock, nie zmieniając znaczenia cyfr na listach ani `Ctrl+cyfra` dla sesji. Skok respektuje ustawienie automatycznego odczytu pozycji, a brak znanego czasu trwania kończy się jednoznacznym komunikatem. Paleta poleceń pokazuje wszystkie dziesięć pozycji procentowych. W planie adapterów zapisano oficjalną integrację YouTube bez synchronizacji konta w pierwszym etapie i bez pobierania, ekstrakcji dźwięku lub nagrywania treści YouTube.

Stan `alpha.40`: domyślnym komunikatem po skoku cyfrą jest sam procent. Karta Komunikaty pozwala wybrać sam procent, sam czas albo procent i czas, a paleta prowadzi bezpośrednio do tej kontrolki. `Ctrl+Shift+G` i wspólny checkbox wyciszają zarówno automatyczne wartości czasu, jak i wartości głośności po zmianie. Odtwarzanie, pauza, komunikaty błędów i polecenia czasu na żądanie pozostają słyszalne.

Stan `alpha.41`: nadrzędne `Ctrl+Shift+G` zachowuje osobne wybory komunikatów skoków cyframi, przewijania strzałkami, głośności i odtwarzania/pauzy. Każda kategoria ma własny checkbox i wejście w palecie. Na dole głównego okna działa pasek stanu bez automatycznej mowy, odczytywany przez `NVDA+End`: usługa, stan, tytuł, pozycja z czasem całkowitym, głośność i przepływność. Lokalna przepływność jest oznaczonym przybliżeniem; brak metadanych nie jest zastępowany wymyśloną wartością.

Stan `alpha.42`: pasek stanu został przeniesiony z wewnętrznego panelu z marginesem na rzeczywistą dolną krawędź okna, zgodnie ze sposobem lokalizowania go przez `NVDA+End`. Bezpośredni element paska udostępnia aktualizowany tekst przez UI Automation. `Ctrl+G` otwiera skok do wpisanego czasu, a osobne polecenie przyjmuje procent `0–100`; oba są obecne w menu Odtwarzanie, odtwarzaczu i palecie.

Stan `alpha.43`: po negatywnym teście NVDA pasek WPF został zastąpiony natywnym paskiem stanu Windows osadzonym przy dolnej krawędzi. Dokładny skok do czasu i procentu jest poleceniem globalnym bieżącej sesji odtwarzania, dostępnym także podczas pracy na liście; cyfry `0–9` pozostają lokalne dla odtwarzacza. Walidacja okna skoku wywołuje aktywne oznajmienie błędu, zaznacza wadliwą wartość i nie przenosi fokusu z pola.

Stan `alpha.44`: analiza kodu NVDA wykazała, że bez modułu aplikacji `NVDA+End` bada wyłącznie obiekt w lewym dolnym pikselu granic okna. Natywny pasek osadzony wewnątrz WPF nadal nie obejmował ramki. Warstwa Windows tworzy więc nieaktywujący, niemal przezroczysty obiekt `msctls_statusbar32` obejmujący możliwe dolne lewe granice Win32 i DWM. Obiekt ma bieżący tekst paska, pozostaje poza nawigacją i jest przemieszczany razem z oknem.

Stan `alpha.45`: ostateczna lokalna mapa odtwarzacza używa `Ctrl+J` dla wpisanego czasu i `Ctrl+Shift+J` dla wpisanego procentu. Oba polecenia oraz skoki cyframi działają tylko w odtwarzaczu. Na liście skróty nie są przechwytywane, a wybranie polecenia skoku z menu lub palety podaje, że `F6` otwiera odtwarzacz. `F6` jest wspólnym wejściem „Teraz odtwarzane” dla multimediów lokalnych, streamingu, radia i sesji urządzeń; skoki zależne od długości pozostają niedostępne dla transmisji bez znanego czasu.

Stan `alpha.46`: osobne, prawie niewidoczne okno zgodności z `alpha.44–45` zostaje całkowicie usunięte, ponieważ w ręcznym teście przejmowało kontekst NVDA przy starcie i blokowało odczyt oraz klawiaturę do czasu zamknięcia przez `Alt+F4`. Nie wolno realizować paska stanu dodatkowym oknem nadrzędnym ani pomocniczym. Główne okno zgłasza automatyzacji granice własnego obszaru klienta, a rzeczywisty pasek pozostaje jego wewnętrzną kontrolką bez aktywnych komunikatów. `NVDA+End` pozostaje testem zgodności, nie warunkiem używalności: menu Odtwarzanie i paleta zawierają zawsze dostępne polecenie „Odczytaj stan odtwarzania”, które mówi usługę, stan, tytuł, czas, głośność i przepływność bez przesuwania fokusu.

Stan `alpha.47`: kontener paska nie powiela już pełnej nazwy dostępnościowej swojej etykiety. Jeden komunikat ma kolejność zoptymalizowaną pod szybkie pytanie o parametry: przepływność, stan, pozycja z czasem całkowitym, głośność, tytuł i na końcu usługa. To samo formatowanie stosuje jawne polecenie „Odczytaj stan odtwarzania”.

Stan `alpha.48`: odczyt paska pomija głośność i zachowuje kolejność: przepływność, stan, pozycja z czasem całkowitym, tytuł, usługa. Jawne polecenie pełnego stanu nadal może podać głośność. Wspólna kontrolka komunikatów używa jednego zdarzenia powiadomienia UI Automation z tekstem komunikatu; usunięto równoległe `LiveRegionChanged`, które mogło zamiast treści sporadycznie wywołać statyczną nazwę „Stan programu”. Nazwa automatyzacji tej kontrolki jest teraz jej aktualnym tekstem, więc także ręczne badanie obiektu nie ujawnia technicznej etykiety.

Stan `alpha.49`: lokalny tor został przeniesiony na NAudio 2.2.1, współdzielone WASAPI i SoundTouch.Net 2.3.2. `Shift+,` oraz `Shift+.` wybierają 0,50–2,00× co 0,25, a `Ctrl+.` przywraca normalną wartość. SoundTouch zmienia tempo niezależnie od wysokości. Stan jest własnością sesji i przetrwa zmianę utworu oraz przebudowę rdzenia po zapisaniu ustawień. Sesje bez wspieranego wyjścia zgłaszają niedostępność. Ze względu na LGPL biblioteki SoundTouch pozostają wymiennymi plikami obok EXE, razem z pełnym tekstem licencji i wskazaniem źródeł; NAudio jest objęte licencją MIT. Publikacja jest od tej wersji jednoznacznie nazwanym folderem, nie pojedynczym plikiem. Podstawa techniczna: [NAudio](https://github.com/naudio/NAudio), [SoundTouch.Net](https://github.com/owoudenberg/soundtouch.net), [skróty YouTube](https://support.google.com/youtube/answer/7631406).

Stan `alpha.50`: pasek stanu rozpoczyna się bez zbędnego słowa „przepływność”, np. „około 192 kb/s, 48 kHz”. Bitrate pozostaje oznaczonym przybliżeniem, gdy wynika z wielkości pliku i czasu, natomiast częstotliwość próbkowania pochodzi bezpośrednio z formatu źródłowego NAudio. `Ctrl+I` i prefiksowe `I` otwierają informacje o elemencie. `Ctrl+Shift+I` i prefiksowe `Shift+I` odczytują pełny stan odtwarzania. Rozszerzone informacje techniczne pozostają w menu i palecie bez stałego skrótu.

Stan `alpha.51`: pasek usuwa słowo „około” i pomija całą część parametrów, jeśli źródło ich nie udostępnia. `Ctrl+I`, `Ctrl+Shift+I`, prefiksowe `I` i `Shift+I` oraz trzy stare polecenia znikają. Zastępuje je jedno `Alt+Enter` — **Właściwości i informacje** — z dostępnym tekstem i bez ujawniania tokenów lub podpisanych adresów. Odtwarzacz nie otwiera filtra ani wyszukiwania, lecz zachowuje działania na bieżącym elemencie. Widok, filtr, zaznaczenie i aktywność odtwarzacza są pamiętane osobno dla każdej sesji. Escape wraca do miejsca, z którego ostatnio wywołano F6 lub otwarto odtwarzacz. Plan radia obejmuje adaptery M3U/M3U8, PLS, XSPF oraz import i eksport formatu Ulubionych VRadio.

Stan `alpha.52`: Kolejka jest logiczną sumą stanów „w kolejce” i „odtwarzaj jako następne”, ale każde polecenie usunięcia czyści oba, więc element nie może pozostawać w widoku z niezgodnym komunikatem. Zwykła lista ma rozszerzone zaznaczanie `Shift+strzałka`; działania przynależności, playlist i Delete obejmują cały wybór, a historia zapisuje je jako jedną odwracalną operację. Naturalny koniec lokalnego pliku wybiera w kolejności: „odtwarzaj jako następne”, Kolejkę, następny element załadowanej listy; ostatni element nie zapętla listy. Regulacja głośności jest wzmocnieniem programowym w strumieniu AMC przed współdzielonym WASAPI i nie steruje sesją audio NVDA. Zmiana sesji podaje również przywrócony widok. Tytuł głównego okna nadal zaczyna się od elementu aktualnie odtwarzanego, a nie od zaznaczenia przeglądanej listy; nazwa widoku w dalszej części tytułu odróżnia Kolejkę, Ulubione i inne powierzchnie.

Stan `alpha.53`: informacje `Alt+Enter` są listą wierszy zamiast wielowierszowego pola edycji, ponieważ ręczny test NVDA nie pozwalał niezawodnie czytać poprzedniej kontrolki. Pierwszy wiersz to od razu tytuł; nagłówek „Podstawowe informacje” został usunięty jako zbędny i mylący. `Ctrl+Shift+C` rozróżnia pełną ścieżkę lokalną od kanonicznego publicznego łącza usługi. Model danych przechowuje publiczne łącze osobno od źródła odtwarzania, które może być prywatne lub krótkotrwałe. Historia widoków `Alt+lewo/prawo` działa w całym głównym widoku i jednoznacznie mówi „Wstecz” albo „Naprzód” wraz z nazwą celu; nadal jest eksperymentalna i może zostać usunięta, jeśli ponowny test potwierdzi, że komplikuje interfejs bardziej, niż pomaga.

Stan `alpha.54`: po ręcznym teście lista wierszy została zastąpiona natywnym `RichTextBox` Windows tylko do odczytu. Kontrolka udostępnia czytnikowi tekst, kursor i zakres zaznaczenia, więc można czytać znaki, słowa i wiersze oraz kopiować dowolny fragment. Jest to samodzielny odpowiednik idei okna przeglądalnego używanej przez dodatki NVDA: AMC nie wywołuje `ui.browseableMessage`, ponieważ ta funkcja należy do procesu dodatku NVDA i uzależniłaby zwykłe okno programu od jednego czytnika. Podstawą decyzji są zakresy tekstowe opisane w [architekturze NVDA](https://github.com/nvaccess/nvda/blob/master/projectDocs/design/technicalDesignOverview.md). Enter na „Kopiuj wszystko” nie jest już przechwytywany jako zamknięcie: operacja mówi o powodzeniu i pozostawia okno otwarte. Lokalna ścieżka znajduje się w pierwszym bloku informacji. Odtwarzacz otrzymuje menu kontekstowe działań bieżącego elementu, a menu listy zostaje uzupełnione o Bibliotekę i obie operacje kopiowania; stanowe etykiety mówią „Dodaj” albo „Usuń”.

Stan `alpha.55`: wspólne właściwości są uporządkowane jako: identyfikacja i źródło, sekcja **W aplikacji** oraz sekcja **Techniczne**. Pierwsza zawiera tytuł, wykonawcę, rodzaj, usługę i lokalną ścieżkę; druga bieżące odtwarzanie i wszystkie stany przynależności; trzecia czas, format, rozmiar, bitrate i częstotliwość próbkowania. Menu kontekstowe nie polega już na osobnym atrybucie akceleratora, którego NVDA nie powtarzał przy ponownym odczycie fokusu. Skrót jest częścią dostępnościowej nazwy pozycji, a wizualny `InputGestureText` pozostaje bez zmian. Historia widoków jest z definicji lokalna dla sesji: każda usługa ma osobne stosy Wstecz i Naprzód, a zmiana sesji odbywa się wyłącznie przeznaczonymi do tego poleceniami. Komunikaty historii zawierają nazwę sesji, aby ta granica była słyszalna.

Stan `alpha.56`: prefiks fokusu po historii ma kolejność **kierunek → docelowy widok → sesja**, np. „Wstecz, Kolejka, Lokalne multimedia”. `MessageSettings.HistoryMessages` steruje wyłącznie dodatkowymi komunikatami Wstecz/Naprzód i komunikatem braku historii; nie zmienia stosów ani działania skrótów. Opcja ma checkbox na karcie Komunikaty, własny cel ustawień i wpis palety poleceń, dlatego podlega także eksportowi konfiguracji. Nadrzędne `Messages.Enabled` ma pierwszeństwo. Nie łączymy jej z `SeekMessages`, ponieważ ten przełącznik jest nadrzędny wyłącznie dla automatycznych komunikatów transportu odtwarzacza. Pełny odczyt obiektu odtwarzacza przez polecenie przeglądu NVDA pozostaje celowo bogaty i nie jest automatycznym komunikatem aplikacji.

Stan `alpha.57`: po wyłączeniu `HistoryMessages` historia nadal podaje **docelowy widok → sesję → element**, lecz pomija tylko słowa Wstecz/Naprzód; wyłączenie nadrzędnego `Messages.Enabled` usuwa cały dodatkowy kontekst. Tytuł głównego okna ma stabilną kolejność **moduł → aktualnie odtwarzany lub wstrzymany element → sesja → aplikacja i wersja**. Lokalna biblioteka, przynależności, bieżący plik, głośność, tempo i pozycje wszystkich plików są trwałą częścią stanu i pełnej kopii zapasowej. Odcisk pliku zapobiega wznowieniu podmienionej treści, a przywrócenie stanu nie rozpoczyna automatycznie odtwarzania. Dane pozostają w AppData niezależnie od przenośnego folderu programu; MSIX/App Installer jest nadal osobnym etapem dystrybucji. Próbka surowego AAC/ADTS 48 kHz została poprawnie rozpoznana i zdekodowana przez aktualny tor NAudio–SoundTouch; ręczny test aplikacji rozstrzygnie, czy zgłoszony brak dźwięku dotyczył konkretnego pliku albo działania interfejsu.

Stan `alpha.58`: po ręcznym teście tytuł ma kolejność **aktualnie odtwarzany lub wstrzymany element → moduł → sesja → aplikacja i wersja**, co ułatwia rozpoznanie treści w przełączniku zadań. Drugi start AMC nie tworzy osobnego procesu, lecz przywołuje najgłębsze widoczne okno pierwszej instancji; zapobiega to równoległemu nadpisywaniu `state.json`. Dialogi modalne zachowujemy tylko tam, gdzie użytkownik kończy krótkie zadanie, a odtwarzacz pozostaje widokiem. Opcjonalny zasobnik będzie domyślnie wyłączony i nie zmieni znaczenia `Alt+F4`. `Page Up` uruchamia poprzedni, a `Page Down` następny element bez zapętlenia; `Shift+Page Up/Down` pozostaje zarezerwowane dla zakładek w bieżącym pliku. Wszystkie zakładki będą równocześnie widoczne w globalnym indeksie Zakładek, lecz nawigacja w odtwarzaczu nie przeskoczy samoczynnie do innego pliku. Escape wraca z odtwarzacza do listy bez zatrzymywania dźwięku, ponieważ jest poleceniem nawigacji, a nie transportu. OGG/Vorbis otrzymuje jawny dekoder NAudio.Vorbis/NVorbis. Pozycja lokalna jest zapisywana również na granicach działań transportu i przy odejściu z okna. Struktura folderów, bezpieczne usuwanie z biblioteki i przenoszenie pliku do Kosza są następnym odrębnym etapem.

Stan `alpha.59`: widoczna nazwa sesji zostaje skrócona do **Pliki lokalne**. Wewnętrzna nazwa domyślnego widoku może pozostać stabilnym identyfikatorem „Multimedia”, lecz nie jest dołączana do tytułu ani kontekstu NVDA, gdy powtarza oczywistą informację. Boczne strzałki na głównej liście tej sesji: lewa oznajmia z pamięci format, wykonawcę, czas, parametry audio i rozmiar, a prawa otwiera zwykłe menu kontekstowe z działaniami „Otwórz” i „Otwórz w…”. Odczyt nie skanuje synchronicznie zawartości pliku. Lokalny `Ctrl+Shift+C` zapisuje jednocześnie `UnicodeText` i `FileDrop`, również dla wielokrotnego zaznaczenia; serwisy nadal kopiują wyłącznie kanoniczny publiczny URL. `Delete` w domyślnym lokalnym katalogu usuwa rekord z AMC bez operacji na systemie plików. Operacja jest częścią wspólnej chronologii `Ctrl+Z`, razem ze zmianami Ulubionych, Biblioteki i Kolejki. Można usunąć również ostatni plik: pusta sesja zostaje odłączona bez pozostawiania sztucznego elementu, a cofnięcie przywraca jej kolejność i zapamiętane pozycje.

Stan `alpha.60`: każda sesja ma zapisywaną na dysku historię odtwarzania o maksymalnie 500 unikatowych elementach. Ponowne odtworzenie przenosi element na początek zamiast tworzyć duplikat. `Ctrl+H` otwiera jej zwykły widok, uporządkowany od najnowszego. Ostatnio odtwarzany element jest oznaczany na listach, lecz przywrócenie aplikacji nie uruchamia go i nie zmienia zaznaczenia. W odtwarzaczu `Alt+dół` przechodzi do starszego, a `Alt+góra` do nowszego wpisu; każdy zaczyna w swojej zapamiętanej pozycji. Migawka historii na czas tej nawigacji pozostaje stabilna, mimo że uruchomiony element staje się najnowszy. `Page Up/Down` nadal porusza się względem listy źródłowej. Historia widoków `Alt+lewo/prawo` pozostaje osobnym, nietrwałym stosem każdej sesji. Fizyczne `Shift+Delete` działa tylko dla lokalnych źródeł, wymaga potwierdzenia, zwalnia otwarty plik, przenosi udane elementy do systemowego Kosza i usuwa ich rekordy z historii; nie zapisuje fałszywej operacji `Ctrl+Z`. Zwykły `Delete` pozostaje odwracalnym usunięciem katalogowym. Boczne strzałki działają we wszystkich lokalnych widokach, polecenia czasu są dostępne także w odtwarzaczu, `Ctrl+C` obejmuje wszystkie zaznaczone nazwy, a po przełączeniu sesji komunikat rozpoczyna się od numeru i nazwy sesji, następnie podaje przywrócony moduł.

Stan `alpha.61`: prawa strzałka jest bezpośrednim skrótem systemowego `openas`, a nie zamiennikiem menu kontekstowego AMC. Lewa nadal podaje krótkie informacje. `Shift+Delete` jest dostępne na liście i w odtwarzaczu; w drugim przypadku działa na faktycznie otwartym elemencie, potwierdza zamiar, zatrzymuje tor i zwalnia uchwyt przed operacją Kosza. Usuwanie czyści trwałą listę identyfikatorów oraz migawkę `Alt+góra/dół`. Stan lokalny zeruje `CurrentItemId`, gdy sesja przestaje istnieć, a normalizacja historii pozostawia tylko identyfikatory obecne w lokalnym katalogu. Opóźnienie samego Kosza może wynikać z synchronicznej powłoki Windows i synchronizacji iCloud; nie przenosimy destrukcyjnej operacji na wątek tła bez osobnego projektu anulowania, blokady ponownego polecenia i obsługi komunikatów powłoki.

Stan `alpha.62`: szybka informacja pod lewą strzałką uzupełnia brakujące metadane wyłącznie zaznaczonego pliku. Tor używa tego samego wyboru czytnika NAudio/Vorbis co odtwarzanie, lecz nie uruchamia dźwięku. Średni bitrate jest szacowany jako rozmiar w bitach podzielony przez czas, więc dla VBR opisuje średnią, a nie chwilową wartość. Czas, częstotliwość i `kb/s` są zapisywane w stanie lokalnym. Niepowodzenie próby nie blokuje odczytu rozszerzenia, wykonawcy i rozmiaru. Główne okno jawnie przechwytuje `Alt+F4` przed warstwą odtwarzacza i wykonuje `Close`; widok odtwarzacza nie jest osobnym oknem, dlatego nie ma dwuetapowego zamykania. Dialog modalny nadal zachowuje standard Windows i jego własne `Alt+F4` zamyka najpierw ten dialog.

Stan `alpha.63`: zapamiętana pozycja lokalnego pliku jest stanem sesji także wtedy, gdy po ponownym uruchomieniu dekoder nie otworzył jeszcze żadnego źródła. Odczyt czasu, F6 i pierwsze wznowienie korzystają więc z zapisanej pozycji zamiast z technicznego zera pustego wyjścia audio. Prawa strzałka wywołuje bezpośrednio udokumentowane systemowe okno `SHOpenWithDialog`, niezależne od istniejącego skojarzenia rozszerzenia. W Windows 10 i nowszym okno służy do jednorazowego otwarcia pliku; zmianą aplikacji domyślnej zarządzają Ustawienia systemu. Fizyczne `Shift+Delete` pozostaje wyłącznie na listach. W lokalnym odtwarzaczu `Delete` usuwa bieżący rekord z AMC, pozostawia plik na dysku, wstrzymuje go i podaje następny element albo docelową sesję. Operację można cofnąć przez `Ctrl+Z`.

Stan `alpha.64`: ręczny test NVDA wykazał, że systemowe `SHOpenWithDialog` odbiera aktywację AMC, lecz nie ustanawia czytelnego fokusu w oknie wyboru aplikacji. Użytkownik odzyskiwał fokus dopiero przez `Alt+Tab`. Funkcja „Otwórz w…” oraz jej prawa strzałka zostały więc całkowicie wycofane. Prawa strzałka zachowuje zwykłą semantykę kontrolki listy, a menu nadal oferuje „Otwórz w domyślnej aplikacji”, gdy plik ma działające skojarzenie. Nie budujemy własnego wyboru aplikacji, ponieważ wymagałby utrzymywania równoległego, niepełnego modelu programów Windows.

Stan `alpha.65`: po doprecyzowaniu, że warto zachować użyteczną funkcję mimo pojedynczego problemu z fokusem NVDA, „Otwórz w…” wraca jako kontrolowany eksperyment. Systemowe okno nie jest już wywoływane wewnątrz stosu `PreviewKeyDown` ani zdarzenia kliknięcia menu. AMC najpierw kończy obsługę klawisza i zmianę fokusu WPF, a dopiero przy bezczynności dyspozytora otwiera natywne okno. Prawa strzałka i pozycja menu korzystają z tej samej odroczonej ścieżki. Test obu wejść rozstrzygnie, czy funkcję zachować w całości, tylko w menu, czy poszukać innego mechanizmu systemowego.

Stan `alpha.66`: test `alpha.65` wykazał ten sam brak czytelnego fokusu zarówno z prawej strzałki, jak i z menu, więc problem nie należał do wejścia klawiaturowego. Ostatni wariant eksperymentalny uruchamia systemowe `OpenAs_RunDLL` przez osobny proces `rundll32.exe`. AMC nie jest właścicielem okna wyboru i nie blokuje własnego wątku WPF; Windows może nadać procesowi powłoki zwykły fokus pierwszoplanowy. Jeśli test NVDA nadal będzie negatywny, „Otwórz w…” zostanie usunięte bez kolejnych obejść, a pozostanie stabilne otwieranie w aplikacji domyślnej.

Stan `alpha.67`: osobny proces powłoki `alpha.66` również nie otworzył dostępnego okna — po prawej strzałce nie następowała żadna widoczna ani słyszalna operacja. Po trzech sprawdzonych implementacjach „Otwórz w…” zostaje definitywnie usunięte z prawej strzałki oraz z menu listy i odtwarzacza. Prawa strzałka zachowuje standardową semantykę kontrolki listy. Stabilne „Otwórz w domyślnej aplikacji” pozostaje w menu lokalnego pliku; wybór lub zmiana skojarzenia odbywa się poza AMC w systemie Windows.

Stan `alpha.68`: trwały indeks Zakładek zapisuje sesję, element, tytuł, czas oraz datę utworzenia. `B` w odtwarzaczu dodaje szybką zakładkę bez okna, a próba ponownego dodania w tej samej sekundzie nie tworzy duplikatu. `Shift+Page Up/Down` porusza się tylko w obrębie aktualnego materiału i nie przeskakuje samoczynnie do innego pliku. `Ctrl+B` otwiera wspólny dostępny widok wszystkich sesji, w którym działa filtrowanie, nawigacja literowa, Enter i bezpieczne usuwanie. Pełna kopia danych obejmuje Zakładki; eksport samych ustawień celowo ich nie zawiera.

Stan `alpha.69`: nawigacja po zakładkach ma krótkotrwałą kotwicę ostatnio osiągniętej pozycji, dlatego kolejne `Shift+Page Up/Down` nie wybiera ponownie tej samej zakładki wskutek upływu czasu odtwarzania. Każde inne polecenie zrywa tę kotwicę i następny skok jest ponownie obliczany względem faktycznej pozycji. Osobna opcja komunikatów wycisza pomyślne skoki po zakładkach, ale zachowuje ostrzeżenie o dojściu do pierwszej lub ostatniej. Projekt osobnego, przenośnego eksportu opisuje `PROJEKT_IMPORTU_EKSPORTU_ZAKLADEK.md`; w tej wersji działa nadal bezpieczna pełna kopia.

Stan `alpha.70`: `Ctrl+Shift+B` otwiera jedno modalne, dostępne pole nazwy dla bieżącej pozycji odtwarzacza. Nazwa ma do 200 znaków, jest normalizowana do pojedynczych odstępów i zapisywana razem z zakładką. Wpis w tej samej sekundzie aktualizuje nazwę istniejącej zakładki, zamiast ją dublować. Globalna lista przedstawia kolejno **nazwę zakładki → tytuł materiału → czas → sesję**; zakładki szybkie bez nazwy zachowują dotychczasowy format.

Stan `alpha.71`: porządek globalnej listy nie używa daty utworzenia jako porządku odsłuchu. Zakładki aktualnie wybranego materiału znajdują się na początku i są uporządkowane rosnąco według pozycji. Pozostałe rekordy są grupowane stabilnie według sesji oraz tytułu, a następnie czasu. Nie sortujemy wszystkich materiałów wyłącznie po wartości czasu, ponieważ mieszałoby to niepowiązane pliki. Przejście Enterem zachowuje identyfikator wiersza powrotnego, pokazuje odtwarzacz i ustawia fokus na jego głównym przycisku; Escape odbudowuje listę, wybiera zapisany identyfikator i przywraca fokus.

Stan `alpha.72`: data utworzenia jest informacją prezentowaną, ale nadal nie zmienia porządku odsłuchu. Globalny rekord ma kolejność: tytuł pliku lub materiału, lokalna data utworzenia zakładki, pozycja w materiale, opcjonalna nazwa, sesja i rodzaj „zakładka”. W otwartym odtwarzaczu `Shift+Page Up/Down` podaje wyłącznie pozycję albo nazwę i pozycję. Kopiowanie z globalnej listy zachowuje pełny, widoczny opis każdego zaznaczonego rekordu.

Schowek Windows w `alpha.72`: `Ctrl+C` oznacza tekstową nazwę, `Ctrl+Shift+C` — fizyczne pliki i tekstowe pełne ścieżki dla lokalnych źródeł albo łącze dla usługi. Obie operacje są dostępne także na wynikach wyszukiwania bez zamykania jego okna. `Ctrl+X` jest prawdziwą systemową operacją przygotowania lokalnych plików do przeniesienia i działa na zwykłych listach oraz lokalnych wynikach wyszukiwania, ale nie w Zakładkach ani dla strumieni. AMC nie usuwa wpisów przy samym wycięciu; po faktycznym `Ctrl+V` i odzyskaniu aktywności usuwa tylko rekordy wskazujące nieistniejące już ścieżki.

Stan `alpha.73`: wyniki wyszukiwania pozwalają zaznaczać wiele rekordów i kopiować zbiorczo nazwy albo fizyczne pliki, ścieżki i łącza. Wycinanie i wklejanie są w wynikach jawnie blokowane; nie dotyczy to pola tekstowego, w którym standardowy schowek tekstowy działa normalnie. Na zwykłych listach lokalnych `Ctrl+V` importuje pliki ze schowka jako istniejące źródła bez kopiowania danych do katalogu programu. Dozwolone cele to Multimedia, Biblioteka, Kolejka i Ulubione; dwa ostatnie ustawiają odpowiednią przynależność. Historia odtwarzania i Zakładki nie są celami importu. Wewnętrzne wklejenie zmienia systemową operację wycięcia na zwykłą kopię w schowku, dzięki czemu plik nie zostanie później nieumyślnie przeniesiony.

Stan `alpha.74`: globalne Zakładki są widokiem przejściowym, a nie trwałym miejscem startowym sesji. Jawne `Ctrl+B` zapamiętuje sesję, widok i element źródłowy. Sekwencja Enter → odtwarzacz → Escape wraca do wybranego rekordu Zakładek, a kolejny Escape do zapamiętanego kontekstu źródłowego. Aktywny filtr ma pierwszeństwo: pierwszy Escape tylko go czyści. Stan kończący poprzednie uruchomienie w samej liście Zakładek jest przy następnym starcie normalizowany do Multimedia; stan otwartego odtwarzacza może pozostać przywracany, lecz wyjście z niego prowadzi do zwykłej listy. `Shift+Page Up/Down` jest obsługiwane jako nawigacja po zakładkach wyłącznie wtedy, gdy fokus znajduje się w odtwarzaczu. Na listach pozostaje standardowym rozszerzonym zaznaczaniem WPF i nie może zmieniać widoku.

Stan `alpha.75`: szybka informacja pod strzałką w lewo jest wspólną funkcją zwykłych list i wyników wyszukiwania we wszystkich sesjach. Układ obejmuje dostępne metadane w kolejności: tytuł, lokalny format, wykonawca, czas, zwarty zestaw parametrów audio oraz lokalny rozmiar. Dla pliku AMC może uzupełnić metadane z nagłówka i obliczyć przybliżony bitrate z rozmiaru oraz czasu. Dla streamingu wolno odczytać wyłącznie wartości przekazane przez adapter; brak wartości nie może zostać zastąpiony założeniem o jakości usługi. Skrót nie otwiera okna informacji i nie zmienia fokusu.

Stan `alpha.76`: fizyczne usuwanie `Shift+Delete` korzysta z COM `IFileOperation` i jawnych flag Kosza, ponieważ starszy mechanizm nie obsługiwał niezawodnie punktów ponownej analizy Cloud Files, między innymi istniejących placeholderów iCloud Drive. AMC najpierw zamyka tor odtwarzania, jeśli obejmuje on zaznaczony plik, następnie przekazuje każdy plik do Kosza. Dopiero potwierdzone powodzenie usuwa rekord z katalogu, historii i widoków. Błąd lub anulowanie pozostawia rekord oraz plik bez zmian, nie kończy procesu aplikacji i podaje komunikat wraz z kodem HRESULT. Częściowe powodzenie wielokrotnego wyboru jest dozwolone i raportowane.

Stan `alpha.77`: `Ctrl+Shift+O` rejestruje wybrany katalog jako trwałe źródło i otwiera dostępny widok **Foldery** oparty na zwykłej liście, nie na wielopoziomowym TreeView. Lista pokazuje tylko jeden poziom naraz, najpierw podfoldery, potem pliki; Enter schodzi do folderu lub otwiera plik, Backspace wraca wyżej, wpisywanie liter oraz `Ctrl+K` dotyczą bieżącego poziomu, a `Ctrl+F` całej sesji lokalnej. Ścieżka bieżącego poziomu i źródła są zapisywane w stanie. Płaska Biblioteka pozostaje równoległa. Kolejność sesji jest edytowana w Ustawieniach Ogólnych i stanowi jedno źródło prawdy dla `Ctrl+1–9`, listy sesji oraz `Ctrl+Page Up/Page Down`; domyślnie: Pliki lokalne, WiiM, TIDAL, Apple Music. Ustawienia kolejności pól list zostały przeniesione do sekcji Odczytywanie elementów list na karcie Komunikaty, bez zmiany modelu danych, eksportu ani celów palety poleceń.

Korekta `alpha.78`: logiczna sesja Pliki lokalne jest tworzona już przy starcie, również bez elementów. Skrót przypisany do tej sesji jest więc zawsze prawidłowy, a polecenia wymagające materiału zwracają jednoznaczny komunikat o pustej sesji. Wybranie źródła przez `Ctrl+Shift+O` najpierw przełącza interfejs na lokalny widok Foldery i zapisuje źródło, a następnie asynchronicznie odkrywa pliki; lista demonstracyjna innej usługi nie pozostaje na ekranie podczas operacji.

Korekta `alpha.79`: Foldery i Biblioteka nie są dwoma katalogami danych. Zarejestrowany folder jest źródłem Biblioteki, Foldery pokazują hierarchię fizyczną, a `Ctrl+L` pokazuje płaski, alfabetyczny zestaw tych samych rekordów. Ponowne `Ctrl+Shift+O` przywraca do Biblioteki znane pliki wcześniej z niej usunięte. `Delete` na pliku w Folderach usuwa tylko przynależność do Biblioteki i pozostawia plik na dysku; `Ctrl+Z` ją przywraca. `Delete` na wierszu folderu nie usuwa ani źródła, ani katalogu. Zarządzanie źródłami otrzyma osobne, jednoznaczne polecenie, a fizyczny Kosz pozostaje wyłącznie pod `Shift+Delete` z potwierdzeniem.

Korekta `alpha.80`: `Delete` zapisuje trwałe wykluczenie, więc obserwator, `F5` i ponowne uruchomienie nie dodają elementu samoczynnie; bezpośrednie `Ctrl+Z` usuwa wykluczenie. Źródła są skanowane przy starcie i obserwowane podczas działania. Niedostępne źródło nie jest traktowane jak pusty folder, a brakujące pliki zachowują dane i wracają po pojawieniu się pod tą samą ścieżką. `Alt+1` wybiera Foldery Biblioteki, `Alt+2` płaskie Wszystkie pliki, `Ctrl+L` otwiera ostatni lokalny układ, a `F5` wymusza pełne skanowanie. Shift z cyfrą nie jest skrótem widoku: znak wynikający z układu klawiatury uczestniczy w nawigacji literowej.

Korekta `alpha.81`: znacznik `ReparsePoint` nie oznacza automatycznie dowiązania. Pliki i katalogi-placeholdery Cloud Files bez celu dowiązania są indeksowane po nazwach i atrybutach bez otwierania treści; prawdziwe dowiązania symboliczne i junctiony są nadal pomijane. Migracja naprawia źródło, w którym alfa 80 omyłkowo zamieniła wszystkie starsze rekordy w wykluczenia. Przejście z płaskiego widoku `Alt+2` do Folderów przez `Alt+1` ustawia poziom na katalog nadrzędny zaznaczonego pliku i zachowuje jego trwałe ID. Kolejka pozostaje stanem aktywnej sesji: otwieranie lokalnego źródła przełącza do Plików lokalnych, lecz nie powinno czyścić kolejki innej sesji.

Korekta `alpha.82`: klasyfikacja katalogu najpierw sprawdza prawdziwy cel dowiązania, a dopiero przy błędzie dostawcy korzysta z atrybutów `Offline`, `RecallOnOpen`, `RecallOnDataAccess`, `Pinned` i `Unpinned`. Zabezpiecza to OneDrive i inne implementacje Cloud Files bez wpuszczania AMC w zwykłe junctiony. Google Drive w trybie Mirror jest zwykłym źródłem lokalnym. W trybie Stream wirtualny dysk musi być dostępny, ale jego czasowe zniknięcie jest błędem skanu źródła, nie sygnałem usunięcia wszystkich plików. Odkrywanie ogranicza się do nazw, atrybutów i rozszerzeń; treść może zostać pobrana dopiero na jawne odtworzenie albo odczyt metadanych wybranego elementu. Podstawą są dokumentacje [Microsoft Cloud Files](https://learn.microsoft.com/windows/win32/cfapi/cloud-files-functions) i [Google Drive: Stream or mirror files](https://support.google.com/drive/answer/13401938).

Korekta `alpha.83`: źródła folderowe mają osobny, dostępny menedżer wywoływany z menu Plik albo palety poleceń. Stan każdego źródła obejmuje osiągalność folderu, aktywne, niedostępne i trwale wykluczone rekordy oraz pełną ścieżkę. Rejestracja odrzuca źródła identyczne, zagnieżdżone i wzajemnie obejmujące się, aby jeden plik nie miał niejednoznacznego źródła nadrzędnego. Bezpieczne odłączenie usuwa wyłącznie wpis automatycznej synchronizacji; rekordy katalogu i wszystkie ich relacje pozostają, a pliki na dysku nie są dotykane. Pełna kopia `.amcbackup.json` jest pojedynczym formatem zabezpieczenia: zawiera katalog lokalny, źródła, wykluczenia, Ulubione, kolejki, historię, zakładki, pozycje i ustawienia, bez poświadczeń. Nie tworzymy drugiego formatu kopii katalogu, który mógłby rozjechać się z pełnym stanem.

Korekta `alpha.84`: widok odtwarzacza jest granicą sterowania transportem. Domyślnie `Escape`, `Shift+F6`, przycisk powrotu oraz bezpośrednia nawigacja do widoku wstrzymują audio i odsłaniają zwykłą listę; ustawienie ogólne pozwala świadomie pozostawić odtwarzanie w tle. Wstrzymanie nie oznacza wyzerowania. Osobna zasada określa trwałe wznowienie lokalnych plików: ustawienie ogólne jest domyślnie włączone, a każde źródło folderowe może je odziedziczyć albo wymusić pamiętanie lub start od początku. Pliki dodane pojedynczo dziedziczą zasadę ogólną. Lista nie przejmuje skrótów głośności ani przewijania.

Korekta `alpha.85`: każdy model danych używany bezpośrednio jako element dostępnej listy musi zwracać przyjazną etykietę także przez `ToString()`, ponieważ WPF UI Automation może pominąć `DisplayMemberPath`. Menedżer źródeł nie ujawnia już NVDA nazwy klasy, identyfikatora ani nazw właściwości rekordu.

Rozstrzygnięcie `alpha.86`: `F5` odświeża lokalne źródła, a `Ctrl+F5` otwiera Menedżera Biblioteki. `F2` na lokalnej liście ustawia trwały alias katalogowy i nie zmienia ścieżki; wpisanie ponownie nazwy pliku bez rozszerzenia usuwa rozróżnienie aliasu. `Shift+F2` wykonuje rzeczywistą zmianę nazwy na dysku, zawsze zachowując rozszerzenie i stabilny identyfikator rekordu. Aktualizacja ścieżki zachowuje stany przynależności, Historię, Zakładki i pozycję wznowienia. Operacja nie nadpisuje istniejącego celu i zwalnia wcześniej załadowany plik przez bezpieczne zatrzymanie wyjścia audio.

Rozstrzygnięcie `alpha.87`: Biblioteka lokalna rozdziela układ danych od sortowania. `Alt+1` pokazuje rzeczywiste Foldery, `Alt+2` zawsze wylicza widok Wszystkie pliki alfabetycznie, a `Alt+3` pokazuje zapisaną Kolejność własną. `Alt+strzałka w górę/w dół` działa wyłącznie w trzecim widoku i przenosi pojedynczy element albo ciągły blok; przy aktywnym filtrze jest blokowane, aby ukryte rekordy nie zmieniły pozycji w sposób nieprzewidywalny. Pierwsza Kolejność własna startuje alfabetycznie, później zachowuje ręczne zmiany, a nowe rekordy dopisuje na końcu. Jest metadanym AMC i nie modyfikuje systemu plików. W przyszłych adapterach to samo polecenie może zmienić porządek po stronie usługi tylko wtedy, gdy jej oficjalne API jawnie wspiera taką operację; w pozostałych widokach nie udajemy zapisu zdalnego.

Filtr `Ctrl+K` jest krótkotrwałym zawężeniem bieżącego kontekstu, nie sposobem sortowania ani zapytaniem do usługi. Escape usuwa go i jednym krokiem przywraca fokus listy. Przejście do innego widoku, folderu albo sesji również czyści filtr, a AMC nie przywraca go po restarcie. Wyniki wyszukiwania pozostają osobnym oknem i nie oferują ręcznego przestawiania rekordów.

Implementacja `alpha.88`: lokalne `Ctrl+Shift+A` wylicza Albumy z bezpiecznej heurystyki folderów. Kandydat ma co najmniej dwa bezpośrednie pliki audio, przynajmniej dwa różne numery ścieżek oraz numerację w co najmniej połowie plików. Akceptowane są jedno- lub dwucyfrowe początki oddzielone końcem nazwy, spacją, myślnikiem, podkreśleniem, kropką albo nawiasem. Trzy- i czterocyfrowe początki, między innymi lata w nazwach nagrań, są odrzucane. Wiersz albumu jest kontenerem, a nie fałszywym plikiem: Enter otwiera utwory, Escape wraca na album, a menu nie oferuje plikowych działań wobec samego kontenera. Operacje na utworach pozostają zwykłymi operacjami Biblioteki. Pierwszy wariant nie otwiera treści plików i nie pobiera placeholderów chmurowych; analizuje wyłącznie zapisane ścieżki. Odczyt tagów i ręczne nadpisanie klasyfikacji pozostają późniejszym etapem.

Rozstrzygnięcie `alpha.89`: kolejność widoku i kolejność odtwarzania są odrębnymi, jawnymi pojęciami. Uruchomienie elementu z Ulubionych, Kolejki, otwartego Albumu, bieżącego Folderu, Wszystkich plików albo Kolejności własnej zapisuje w sesji identyfikatory całej nieprzefiltrowanej listy jako **kontekst odtwarzania**. `Page Up`, `Page Down` i zdarzenie końca pliku korzystają z tego samego kontekstu. Samo przejście do innego widoku go nie zmienia; zmienia go dopiero uruchomienie elementu z nowej listy. Historia, Zakładki i wyniki wyszukiwania pozostają lokalizatorami: przechodzą do zasobu, lecz nie tworzą ukrytej playlisty wyników. Jawne „Odtwórz jako następne” i Kolejka mają pierwszeństwo, po czym odtwarzanie wraca do pozycji następującej w zapamiętanym kontekście.

`Alt+strzałka w górę/w dół` ma znaczenie wyłącznie na listach o porządku użytkownika: Kolejności własnej, Ulubionych, otwartej edytowalnej Playliście oraz Kolejce. Od `alpha.98` działają wszystkie cztery warianty. Foldery zachowują hierarchię dysku, Wszystkie pliki — porządek alfabetyczny, Album — numer ścieżki, a Historia i wyszukiwanie — porządek wynikający z ich znaczenia. Przenoszenie nigdy nie zmienia pliku na dysku. Adapter usługi zapisuje zdalny porządek tylko wtedy, gdy oficjalne API to wspiera; w innym wypadku kolejność AMC jest wyraźnie lokalną metadaną.

Opcje elementu są oddzielone od informacji. `Alt+Enter` pozostaje tekstem tylko do odczytu, natomiast `Alt+Shift+Enter` otwiera edytowalne **Opcje odtwarzania elementu**. Lokalna reguła wznowienia ma hierarchię: ustawienie ogólne, nadpisanie źródła folderowego, nadpisanie pojedynczego elementu. Prędkość ma regułę sesji oraz opcjonalne nadpisanie elementu; przejście do kolejnego elementu przywraca jego własną wartość albo wartość sesji. Wybór urządzenia elementu i EQ mają zarezerwowane miejsce w tym samym modelu, lecz nie są uaktywniane, dopóki warstwa wyjść nie potrafi bezpiecznie wyliczyć urządzeń i przełączyć współdzielonego WASAPI bez utraty dźwięku NVDA.

Rozszerzenie `alpha.90` nadaje ustawieniom odtwarzania hierarchię `plik > najbliższy folder > źródło Biblioteki > ustawienie ogólne`. Nadpisanie folderu jest osobną metadaną AMC identyfikowaną znormalizowaną pełną ścieżką; nie tworzy nowego źródła, nie zmienia struktury dysku i obejmuje również przyszłe pliki wykryte w tym poddrzewie. Wartość `dziedzicz` pomija dany poziom, dzięki czemu ustawienie prędkości folderu nie blokuje dziedziczenia reguły pozycji z jego rodzica. Dopasowanie rekordu pliku odbywa się najpierw po stabilnym identyfikatorze, a awaryjnie po znormalizowanej ścieżce bez rozróżniania wielkości liter.

Operacje schowka `alpha.91` przechodzą przez jedną warstwę Windows STA. Zapis tekstu, `UnicodeText`, `FileDrop` i `Preferred DropEffect` ma ograniczone czasowo ponowienia dla `CLIPBRD_E_CANT_OPEN` oraz odpowiadających mu wyjątków WPF. Warstwa nie tworzy drugiej kolejki schowka i nie zmienia semantyki danych: `Ctrl+C` pozostaje tekstem, `Ctrl+Shift+C` przekazuje tekst oraz pliki, a `Ctrl+X` dodatkowo ustawia efekt przeniesienia. Komunikat sukcesu powstaje dopiero po udanym `SetDataObject(..., copy: true)`; trwała blokada jest raportowana, a zaznaczenie i fokus pozostają bez zmian.

Obiekty wyboru w oknie opcji muszą mieć stabilną reprezentację tekstową równą widocznej etykiecie. Samo WPF `DisplayMemberPath` nie wystarcza dla wszystkich ścieżek UI Automation: dlatego `alpha.92` ustawia również tekst wyszukiwania oraz jawny wynik `ToString()` dla reguł pozycji i prędkości. Modelowa wartość enum lub liczba pozostaje oddzielona od komunikatu dostępnościowego.

Warstwa trwałych danych `alpha.93` używa osadzonego SQLite. Tabele rozdzielają rekordy lokalne, źródła i reguły folderów, wykluczenia, własną kolejność, stan lokalnej sesji, Zakładki, Historię, porządek Ulubionych, od `alpha.97` Playlisty i ich uporządkowane elementy, a od `alpha.98` także porządek Kolejki każdej sesji; indeksy obejmują między innymi tytuł, ścieżkę oraz przynależność do widoków. Ustawienia interfejsu, profile klawiatury, historia zapytań i nawigacja sesji pozostają w małym pliku JSON. Migracja z wcześniejszego stanu przebiega w jednej transakcji, weryfikuje liczbę elementów i zachowuje kopię wejściową. Pełny eksport pozostaje niezależny od formatu bazy i nadal może zostać zaimportowany na innym komputerze.

Hydratacja Cloud Files jest operacją odtwarzania, nigdy indeksowania. Skaner odczytuje wyłącznie nazwy, rozszerzenia i atrybuty; widoki oraz informacje nie otwierają zawartości placeholdera. Jawne odtworzenie jednego rekordu uruchamia przygotowanie dekodera na wątku roboczym, oznajmia stan pobierania i ma identyfikator żądania: anulowanie, zmiana utworu lub limit czasu unieważniają wynik, dzięki czemu spóźniony plik nie zacznie grać. Log procesu zapisuje etapy otwierania, błędy urządzenia, wyjątki nieobsłużone i wykryte okresy braku odpowiedzi interfejsu, ale nie jest synchronizowany do chmury.

Krytyczne skróty okna mogą otrzymać dodatkową obsługę na granicy komunikatów Win32, jeżeli WPF, kontrolka hybrydowa albo kolejność zdarzeń czytnika ekranu okazuje się niestabilna. `alpha.94` obejmuje tą ścieżką `Ctrl+Shift+C` na listach i w odtwarzaczu, zachowując zwykłą edycję w polach tekstowych. Operacja schowka rejestruje w lokalnym logu samo nadejście polecenia, typy zapisywanych formatów i wynik ograniczonych ponowień, co pozwala rozróżnić konflikt skrótu od blokady schowka bez zapisywania kopiowanej treści.

Historia cofania pełnego usunięcia rekordu lokalnego zawiera również pozycje jego identyfikatorów w Kolejności własnej. `alpha.95` odtwarza te pozycje po ponownym wstawieniu rekordów, zamiast pozwolić normalizacji potraktować je jak nowe pliki. Pozycje wielu elementów są przywracane rosnąco, dzięki czemu zachowują wzajemny układ. Ta reguła nie zmienia dopisywania rzeczywiście nowych rekordów na końcu.

Historia zmian przynależności do kolekcji przechowuje również migawkę pozycji elementów. Od `alpha.96` dotyczy to Ulubionych każdej sesji oraz lokalnej Kolejności własnej. Cofnięcie najpierw przywraca przynależność, następnie dokładne pozycje, a dopiero potem odświeża widok. Zapobiega to utracie pozycji przez normalizację listy po `Delete` i późniejsze dopisanie przywróconego elementu na końcu.

Implementacja `alpha.97` definiuje playlistę jako nazwaną, uporządkowaną listę stabilnych identyfikatorów elementów należącą do jednej sesji. `Ctrl+P` pokazuje kontenery playlist; Enter otwiera zawartość, a Escape lub Backspace wraca do listy playlist. Uruchomienie utworu zapisuje całą nieprzefiltrowaną zawartość jako kontekst odtwarzania. Delete na poziomie głównym usuwa playlistę po potwierdzeniu, a wewnątrz usuwa wyłącznie wskazania na elementy. Menedżer `Ctrl+Shift+P` obsługuje jedno- i wielokrotne zaznaczenie, stan mieszany, tworzenie, zmianę nazwy, kasowanie i filtrowanie bez ujawniania technicznych reprezentacji obiektów w UI Automation. Wszystkie mutacje mają wspólną historię `Ctrl+Z`, a pełna kopia przenosi playlisty niezależnie od formatu bazy.

Implementacja `alpha.98` przechowuje osobny porządek Kolejki dla każdej sesji jako listę stabilnych identyfikatorów. Nowy element dopisywany jest na koniec odpowiedniej kolejki, brakujące odwołanie jest usuwane podczas normalizacji, a kolejność nie wynika już przypadkowo z katalogu Biblioteki. „Odtwórz jako następne” pozostaje warstwą priorytetową nad zwykłą kolejką: obie grupy mają własną kolejność względną, dlatego zaznaczonego bloku mieszanego nie można przesuwać. Ten sam model zasila widok, automatyczną kontynuację, `Alt+góra/dół`, `Ctrl+Z`, restart i pełny eksport; odświeżenie lokalnego źródła ponownie synchronizuje sesję z zapisem bez zmiany pozycji.

Relacje „Przejdź do albumu” i „Przejdź do wykonawcy” są poleceniami nawigacyjnymi, a nie wyszukiwaniem tekstowym. Dla lokalnego pliku `alpha.89` wykorzystuje rozpoznany folder albumu i jego nadrzędny folder wykonawcy. Alias tytułu ustawiony przez `F2` nie zmienia pliku ani klucza sortowania albumu: etykieta w AMC może nie zawierać `01`, ale kolejność nadal wynika z numeru rzeczywistej nazwy na dysku. Adapter streamingowy ma później dostarczyć stabilne identyfikatory powiązanego albumu i wykonawcy. Dopasowanie lokalnego pliku do katalogu usługi pozostaje osobną, kosztowniejszą funkcją na żądanie.

Semantyka `Backspace` jest hierarchiczna i niezależna od usuwania. `Delete` usuwa z bieżącej kolekcji, a `Shift+Delete` wykonuje odrębne, potwierdzane działanie na fizycznym pliku. `Backspace` przechodzi do rodzica: folderu nadrzędnego, listy albumów z zawartości albumu, listy źródłowej z odtwarzacza albo poziomu nadrzędnego przyszłego kontenera usługi. Na poziomie głównym niczego nie zmienia. Pola edycyjne zachowują systemowe kasowanie znaku. Historia widoków `Alt+lewo/prawo` może prowadzić inną drogą niż rodzic i pozostaje osobnym mechanizmem.

Priorytet lokalnej Biblioteki: podstawowym widokiem będzie rzeczywista hierarchia **Folderów**, ponieważ kolekcja użytkownika nie musi mieć kompletnych tagów. Płaska Biblioteka pozostaje równoległym zestawieniem wszystkich zaimportowanych plików. Widoki Wykonawców, Albumów i Gatunków mogą później powstać z metadanych, lecz nie są warunkiem używalności. Ulubione, Kolejka, Historia, Playlisty i Zakładki wskazują te same rekordy niezależnie od widoku źródłowego.

Lokalny widok **Albumy** nie może zależeć wyłącznie od kompletnych tagów. Zgodny tag albumu ma pierwszeństwo, lecz jego brak uruchamia konserwatywne rozpoznawanie struktury folderów. Folder zawierający co najmniej dwa bezpośrednie pliki audio z różnymi, rozpoznawalnymi numerami ścieżek, np. `01`, `02`, `1 -` albo `2.`, jest albumem; jego nazwa staje się nazwą albumu. Jeżeli układ ma postać `źródło\wykonawca\album\utwory`, nazwę bezpośredniego folderu nadrzędnego można przedstawić jako wykonawcę. Pliki są porządkowane najpierw według numeru ścieżki z tagu, następnie numeru z nazwy i na końcu naturalnie według nazwy. Brakujący tag tytułu pozostawia nazwę pliku bez rozszerzenia; AMC nie przepisuje automatycznie nazw ani tagów. Sprzeczne tagi nie mogą łączyć dwóch albumów, a luźny katalog nagrań bez numeracji nie jest samoczynnie uznawany za album. Albumy jednoplikowe, wielopłytowe i nietypowe katalogi otrzymają później jawne nadpisanie „Traktuj folder jako album”. Adaptery streamingowe korzystają z albumów zwróconych przez usługę i nie stosują heurystyki folderowej.

Prosty montaż audio jest etapem późniejszym po Zakładkach i Folderach. Pierwszy zakres obejmie niedestrukcyjne punkty A–B, odsłuch zaznaczenia i zapis fragmentu jako nowego pliku. Następnie lista fragmentów pozwoli utworzyć nowy plik z kilku źródeł. Oryginały nie będą nadpisywane. Bezstratne cięcie i łączenie będzie używać dojrzałych narzędzi właściwych dla formatu; ponowne kodowanie musi być jawne, a operacja zapisywana przez plik tymczasowy i atomowe ukończenie.

Opcjonalny pilot NVDA inspirowany Free Radio będzie cienką wtyczką wysyłającą wspólne polecenia do rdzenia AMC. Nie skopiuje pełnej, zagnieżdżonej Biblioteki: obsłuży transport, głośność, sesje, presety radiowe i płaskie widoki Kolejki, Ulubionych oraz Historii, a złożone Foldery i wyszukiwanie otworzy w głównym oknie na odpowiednim elemencie. Bezpieczny profil prefiksowy pozostaje podstawą; bezpośredni profil `Ctrl+Windows` będzie opcjonalny i będzie mógł przejmować systemowe gesty tylko po świadomym włączeniu.

Katalog lokalny i kolejność: Biblioteka nie jest playlistą ani kopią jednego folderu, lecz katalogiem źródeł z trwałą tożsamością, ścieżką i widokami. Porządki wyliczane, takie jak tytuł, wykonawca, album, folder, data dodania albo ostatnie odtworzenie, pozostają deterministycznymi sposobami sortowania. Osobna Kolejność własna jest zapisem użytkownika i nie zmienia kolejności plików na dysku. Te same klawisze nie udają ręcznego sortowania w widokach wykonawców, albumów ani wyników wyszukiwania.

Planowana kolejność dalszych etapów:

1. Ustabilizowanie głównego okna, list, filtra, kolejki, fokusu i zatwierdzonej mapy klawiatury.
2. Eksperyment dystrybucji MSIX/App Installer, migracja do .NET 10 LTS oraz prototyp podpisanego manifestu komponentów i powrotu po błędzie.
3. Wydzielenie AMC.Host i lokalnego kontraktu polecenie–zdarzenie z adapterem demonstracyjnym.
4. WiiM jako pierwszy realny test wykrywania, komend, głośności, wejść i presetów.
5. Spotify jako pierwsze logowanie OAuth, katalog i test przekazania odtwarzania do urządzenia Connect.
6. TIDAL jako osobny adapter katalogowy z izolowanym widokiem wyników oraz oficjalnym modułem odtwarzania.
7. Cienka wtyczka NVDA korzystająca wyłącznie z kontraktu hosta.
8. Radio internetowe, świadome nagrywanie bezpośrednich strumieni i podstawowe lokalne multimedia.
9. YouTube jako oficjalny adapter publicznego wyszukiwania i widocznego odtwarzacza z lokalnymi Ulubionymi, playlistami i historią; synchronizacja konta pozostaje opcjonalnym późniejszym rozszerzeniem.
10. BluOS/Bluesound jako rozbudowany adapter urządzenia i źródeł skonfigurowanych na odtwarzaczu.
11. Apple Music i natywna ścieżka MusicKit dla macOS.
12. Natywny prototyp macOS w Swift/AppKit po ustabilizowaniu kontraktu i zachowania wersji Windows.
13. Frontier Smart po uzyskaniu wspieranego API; Sonos jako późniejsza, osobna integracja chmurowa.

## 15. Otwarte decyzje

1. Czy domyślnym prefiksem ma być `Ctrl+Numeryczny Enter`, czy sam `Numeryczny Enter`, oraz jaki ma być czas wygaśnięcia warstwy.
2. Jak prezentować wybór urządzenia i EQ dla elementu po wdrożeniu modułu wyjść audio; nadpisanie wznowienia i prędkości pojedynczego pliku działa od `alpha.89`.
3. Czy istnieje od początku playlista „Do odsłuchu”.
4. Które komunikaty mają być mówione, a które sygnalizowane dźwiękiem.
5. Edycja nazw istniejących zakładek; tworzenie nazwanych zakładek działa od `alpha.70`, a podstawowy globalny widok i eksport pełnej kopii od `alpha.68`.
6. Domyślny odstęp polecenia „w pobliże końca”; roboczo 10 sekund.
7. Ostateczna nazwa aplikacji i identyfikatory pakietów na poszczególnych platformach.

## 16. Zasada dalszej pracy

Dokument jest projektem, a nie zamkniętą specyfikacją. Każda zatwierdzona zmiana powinna być równolegle naniesiona do wersji polskiej i angielskiej. Kod, ustawienia i dokumentacja mają używać stabilnych identyfikatorów poleceń niezależnych od wyświetlanego języka i wybranych skrótów.
