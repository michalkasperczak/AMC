# Dostępny kontroler multimedialny — koncepcja projektu

Wersja dokumentu: 0.4, aktualny plan projektu

Data aktualizacji: 12 sierpnia 2026 r.

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

Filtr działa tylko na danych już znajdujących się w bieżącej liście i nie wysyła zapytania do usługi. Wyszukiwanie bieżące może odpytać aktualną usługę, a wyszukiwanie globalne scala wyniki ze wszystkich włączonych źródeł. Paleta poleceń jest dostępną, filtrowalną listą funkcji, także tych bez przypisanego skrótu.

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

Strzałki po prefiksie służą wyłącznie do sterowania globalnego. W aktywnym oknie listy zwykłe strzałki nawigują po elementach bez używania prefiksu.

## 7. Okno przeglądania

### 7.1. Lista zamiast drzewa

Podstawowym widokiem będzie zwykła dostępna lista lub tabela, nie drzewo rozwijane strzałkami.

Powody:

- Enter jest jednoznaczny i przewidywalny;
- czytnik ekranu nie musi ogłaszać wielu poziomów rozwinięcia;
- łatwiej zachować pozycję po przeładowaniu;
- ten sam komponent może pokazać wykonawców, albumy, utwory, playlisty i urządzenia.

Kolejność informacji w dostępnej etykiecie elementu jest konfigurowalna. Użytkownik może ustawić na przykład tytuł, wykonawcę, czas trwania i typ elementu albo wykonawcę przed tytułem. Pola bez wartości są pomijane. Ta sama kolejność obowiązuje na listach i w komunikatach o bieżącym elemencie; pozostaje niezależna od profilu klawiatury.

### 7.2. Nawigacja

| Klawisz w oknie | Działanie |
| --- | --- |
| `Strzałki w górę/dół` | poprzedni / następny element |
| `Home`, `End` | pierwszy / ostatni element |
| `Page Up`, `Page Down` | przewijanie listy stronami |
| wpisywanie liter | szybkie przejście do elementu zaczynającego się od wpisanego ciągu; kolejne szybko wpisane litery budują frazę, a powtarzanie jednej litery przechodzi między dopasowaniami |
| `Enter` | otwórz wykonawcę, album lub playlistę; na utworze wykonaj domyślną czynność |
| `Ctrl+Enter` | odtwórz zaznaczenie teraz |
| `Shift+Enter` | dodaj zaznaczenie do kolejki |
| `Ctrl+Shift+Enter` | odtwórz jako następne |
| `Backspace` lub `Delete` | usuń element z bieżącej playlisty, kolejki, ulubionych albo biblioteki, jeśli działanie jest jednoznaczne |
| `Ctrl+Z` | cofnij ostatnią zmianę przynależności; w polu tekstowym cofnij edycję tekstu |
| `Alt+Strzałka w lewo` | poprzedni widok |
| `Alt+Strzałka w prawo` | następny widok, jeśli istnieje |
| `Alt+Enter` | informacje o elemencie |
| `Ctrl+Shift+O` | otwórz element w oficjalnej aplikacji usługi |
| `Klawisz aplikacji` lub `Shift+F10` | menu kontekstowe |

Enter wykonuje działanie podstawowe zależne od rodzaju elementu: odtwarza utwór, stację lub preset, natomiast na albumie, playliście albo wykonawcy otwiera zawartość. `Ctrl+Enter` odtwarza natychmiast również cały album lub playlistę. `Alt+Enter`, zgodnie z typowym zachowaniem menedżerów plików, pozostaje informacją lub właściwościami elementu; nie służy do otwierania zewnętrznej aplikacji.

Zwykłe litery na liście nigdy nie wykonują poleceń AMC. Pozostają nawigacją po nazwach elementów. Polecenia jednoliterowe działają dopiero po prawidłowym aktywowaniu globalnej warstwy prefiksowej.

### 7.3. Przeładowywanie

- Stara lista pozostaje widoczna do czasu otrzymania nowych danych.
- Program ogłasza krótko „Ładowanie”, ale nie powtarza komunikatu dla każdej części danych.
- Po zakończeniu informuje np. „24 utwory”.
- Po otwarciu albumu lub playlisty podaje zwięzłe podsumowanie, np. „Album: Abbey Road, The Beatles. 17 utworów, 47 minut 23 sekundy”.
- Po powrocie do wcześniejszego widoku przywraca poprzednio zaznaczony element.
- Odświeżenie nie powinno bez potrzeby przenosić fokusu na początek listy.

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
| `Ctrl+D` | pobierz offline wewnątrz usługi, jeśli obsługiwane |
| `Ctrl+Shift+D` | pobierz do pliku lokalnego; funkcja eksperymentalna, domyślnie wyłączona |
| `Backspace` lub `Delete` | usuń z bieżącej playlisty, kolejki, ulubionych lub biblioteki; z potwierdzeniem albo możliwością cofnięcia |
| `Ctrl+Z` | cofnij ostatnią zmianę przynależności do Ulubionych, Biblioteki lub Kolejki albo stan „Odtwórz jako następne” |
| `Ctrl+Shift+O` | otwórz element w oficjalnej aplikacji usługi |
| `F2` | zmień nazwę playlisty, jeśli obsługiwane |
| `Ctrl+A` | zaznacz wszystkie elementy, jeśli widok pozwala |

Każdy skrót lokalny jest zmienny. Polecenia pobierania nie powinny być aktywne, dopóki odpowiedni moduł nie zostanie świadomie włączony.

`Ctrl+1–9` wybiera sesję, `Ctrl+0` otwiera listę sesji, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję.

## 10. Menu kontekstowe

Menu kontekstowe jest obowiązkowe. Powinno pokazywać tylko funkcje dostępne dla rodzaju elementu i bieżącej usługi, ale zachowywać stałą, przewidywalną kolejność:

1. Odtwórz teraz.
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

Domyślne komunikaty mają być krótkie. W pierwszej wersji nie wprowadzamy osobnych profili „krótki”, „normalny” i „szczegółowy”. Zamiast nich użytkownik może edytować szablon każdego komunikatu, wyłączyć go albo przywrócić ustawienie domyślne.

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
- „Dodano do ulubionych”.
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

Rozróżniamy:

- **źródła i katalogi** — TIDAL, Spotify, Apple Music, radio internetowe i biblioteka lokalna;
- **urządzenia i cele odtwarzania** — lokalny komputer, WiiM, Sonos, Bluesound/BluOS i Frontier Smart;
- **sesję** — aktualne połączenie źródła, konta, kolejki i celu odtwarzania, np. „TIDAL na WiiM w salonie”.

Pierwsze realne logowania otwierają systemową przeglądarkę i używają oficjalnych metod danej usługi. Rdzeń musi obsługiwać OAuth z PKCE, kod powrotu, odświeżanie tokenu, anulowanie, wylogowanie i utratę uprawnień. Integracje wymagające sekretu lub publicznego adresu zwrotnego mogą potrzebować małego, kontrolowanego zaplecza internetowego. Apple Music może mieć różne szczegóły integracji na Windows i macOS, ale przedstawia rdzeniowi ten sam zestaw możliwości.

Lokalne urządzenia mogą wymagać wykrywania w sieci przez mDNS, SSDP/UPnP albo HTTP. Wykrywanie, autoryzacja i sterowanie urządzeniem nie należą do interfejsu okna ani do wtyczki NVDA.

### 13.3. Lokalne multimedia i radio

Lokalny moduł odtwarzania obejmuje docelowo otwieranie plików i folderów, metadane, Bibliotekę, kolejkę, podstawowe popularne formaty, wybór urządzenia, odtwarzanie bez przerw i ReplayGain. Na Windows domyślne wyjście dźwięku powinno pracować w trybie współdzielonym, aby nie wyciszać NVDA i pozostałych dźwięków. Tryb wyłączny może pojawić się później jako funkcja zaawansowana z wyraźnym ostrzeżeniem.

Radio internetowe jest osobnym adapterem rdzenia i korzysta z tych samych sesji, Ulubionych, historii oraz poleceń transportowych. Powinno obsłużyć bezpośrednie strumienie, M3U/PLS, metadane stacji, ponawianie po zerwaniu i wyszukiwanie. Mechanizmy Free Radio można wykorzystać po analizie kodu i licencji, bez przenoszenia całego odtwarzania do procesu NVDA.

### 13.4. Testowanie i odpowiedzialność

- logika rdzenia ma testy jednostkowe bez uruchamiania okna;
- kontrakt klient–host ma testy zgodności i wersjonowania;
- adaptery mają testy kontraktowe na atrapach oraz oddzielne, świadomie uruchamiane testy prawdziwych kont i urządzeń;
- mapy skrótów są sprawdzane automatycznie pod kątem duplikatów, poleceń bez mapowania i kolizji z rezerwacjami standardowymi;
- WPF przechodzi testy klawiatury, UI Automation, fokusu i komunikatów z NVDA, JAWS-em i Narratorem;
- wersja macOS przechodzi osobne testy z VoiceOver i narzędziami dostępności Apple;
- awaria adaptera nie może zawiesić czytnika ekranu ani uszkodzić konfiguracji pozostałych usług.

Docelowa dystrybucja powinna być samowystarczalna i zawierać wymagane środowisko uruchomieniowe. Aktualizator działa dla bieżącego użytkownika bez uprawnień administratora, sprawdza aktualizacje w tle, pobiera wyłącznie podpisane pakiety, weryfikuje ich sumy SHA-256, instaluje atomowo i pozwala wrócić do poprzedniej wersji. Aktualizacja nie może nadpisywać profili, konfiguracji ani danych logowania, kraść fokusu czy przerywać odtwarzania. Użytkownik wybiera kanał stabilny albo beta oraz może wyłączyć automatyczne sprawdzanie, pobieranie lub instalację.

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
12. Interfejs systemu automatycznych aktualizacji, początkowo bez serwera dystrybucyjnego.
13. Oddzielenie rdzenia od WPF oraz przygotowanie kontraktu dla przyszłego AMC.Host.

Pierwszy prototyp i pierwsze działające wydanie dotyczą wyłącznie Windows. Wersja dla macOS, VoiceOver i ewentualna obsługa Siri są etapem późniejszym.

Planowana kolejność dalszych etapów:

1. Ustabilizowanie głównego okna, list, filtra, kolejki, fokusu i zatwierdzonej mapy klawiatury.
2. Wydzielenie AMC.Host i lokalnego kontraktu polecenie–zdarzenie z adapterem demonstracyjnym.
3. WiiM jako pierwszy realny test wykrywania, komend, głośności i presetów.
4. Pierwsze logowanie OAuth i adapter katalogowy: TIDAL albo Spotify.
5. Cienka wtyczka NVDA korzystająca wyłącznie z kontraktu hosta.
6. Radio internetowe i podstawowe lokalne multimedia.
7. Apple Music oraz kolejne urządzenia: Sonos, Bluesound/BluOS i Frontier Smart.
8. Natywny prototyp macOS w Swift/AppKit po ustabilizowaniu kontraktu i zachowania wersji Windows.

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
