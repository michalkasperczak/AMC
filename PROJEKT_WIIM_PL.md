# Adapter WiiM — zakres i dalsze etapy

## Zasada architektoniczna

WiiM jest w AMC przede wszystkim **urządzeniem i celem odtwarzania**, a nie
zamiennikiem katalogów TIDAL, Spotify, Apple Music lub innych usług. Adapter
urządzenia steruje tym, co rzeczywiście udostępnia odtwarzacz. Katalogi,
logowanie, Ulubione i playlisty konta należą do osobnych adapterów usług.

AMC korzysta najpierw z udokumentowanego lokalnego API. Funkcje potwierdzone
tylko przez projekty społecznościowe mogą wejść wyłącznie jako jawnie
eksperymentalne, po sprawdzeniu możliwości konkretnego modelu i z bezpiecznym
wycofaniem operacji. Polecenia administracyjne, konfiguracja sieci, restart i
przywracanie ustawień fabrycznych nie należą do adaptera multimedialnego.

## Stan po alpha 283

- wykrywanie UPnP/SSDP i ręczne dodanie lokalnego adresu IP;
- wybór i zapamiętanie aktywnego urządzenia;
- stan odtwarzania i dostępne metadane;
- odtwarzanie, pauza, następny, poprzedni, stop, przewijanie, głośność i
  wyciszenie;
- wybór wejścia i fizycznego wyjścia, korektor graficzny, powtarzanie,
  losowanie i timer uśpienia;
- odczyt oraz uruchamianie natywnych presetów urządzenia;
- regulacja głośności urządzenia strzałkami góra/dół o 5% oraz
  Shift+strzałkami o 1% w otwartym odtwarzaczu, odporna na chwilową utratę
  fokusa i szybkie kolejne naciśnięcia; kombinacje z klawiszem NVDA pozostają
  przeznaczone dla czytnika ekranu;
- skróty `Ctrl+Shift+1–0/-/=` do sprzętowych miejsc 1–12. Strumienie AMC nie
  przejmują tych samych klawiszy. `Alt+Page Up/Alt+Page Down` przechodzi po
  strumieniach AMC, jeśli taki strumień uruchomiono ostatnio, albo po sąsiednich
  zajętych presetach, jeśli źródłem był preset;
- po ponownym uruchomieniu AMC ustala bieżący preset przez zgodność adresu
  strumienia, a gdy urządzenie nie podaje adresu — przez ostatni preset
  uruchomiony w AMC. Aktywne zapisane urządzenie jest bezgłośnie odświeżane po
  uruchomieniu programu i przy każdym wejściu do sesji WiiM. Pierwsze
  `Alt+Page Up/Alt+Page Down` czeka na ten odczyt zamiast wymagać uprzedniego
  otwarcia listy presetów. Gdy firmware nie udostępnia adresu, zapamiętany
  preset może być punktem nawigacji również dla sieciowych trybów usług
  Connect. Wejścia fizyczne, AirPlay, DLNA i odtwarzanie grupowe nigdy nie
  dziedziczą numeru presetu.
- stan odtwarzacza, `getMetaInfo` i UPnP `GetInfoEx` są łączone w jeden zestaw
  bieżących metadanych. Tytuł audycji lub utworu trafia do odtwarzacza, tytułu
  okna i paska stanu, a `Alt+D` odczytuje go na żądanie bez dialogu;
- nazwy `unknown`, `unknow` oraz techniczne nazwy playlist M3U/PLS nie są
  przedstawiane jako tytuł audycji. Zgodny adres może zostać powiązany z
  użytkową nazwą stacji w Bibliotece Radia AMC.
- migracja `alpha.283` naprawia również kolejność istniejącej listy Strumieni
  sieciowych, zapisując ją w autorytatywnym magazynie SQLite. Jeżeli URL
  uruchomionego strumienia jest identyczny z adresem presetu, jawnie uruchomiony
  strumień nadal steruje `Alt+Page Up/Down`; dopiero jawne uruchomienie presetu
  przełącza nawigację na presety.
- `Ctrl+Alt+Shift+P` nie próbuje zapisywać presetu w urządzeniu. Pozwala
  przypisać jeden z gotowych presetów WiiM do dwunastu lokalnych skrótów AMC
  `Ctrl+Shift+1–0/-/=`. Mapowanie jest zapisywane osobno dla każdego urządzenia,
  a jego usunięcie nie usuwa ani nie zmienia presetu sprzętowego.
- **Otwórz w WiiM** w menu kontekstowym, menu Odtwarzanie i palecie poleceń
  wysyła do aktywnego urządzenia publiczny adres stacji, odcinka podcastu,
  zdalnego utworu albo publicznej playlisty. Enter lub `Ctrl+Alt+W` jest
  świadomym poleceniem, dlatego nie otwiera dodatkowego pytania; po powodzeniu
  przechodzi do odtwarzacza urządzenia. Lista M3U albo PLS jest
  przekazywana poleceniem playlisty, natomiast M3U8 pozostaje bezpośrednim
  strumieniem HLS.
- AMC ma własną, przenośną listę **Strumienie sieciowe** dla sesji WiiM.
  `Ctrl+L` otwiera listę, `Ctrl+N` zapisuje nazwę i adres, a `Ctrl+O` importuje
  bezpiecznie M3U, M3U8 lub PLS. `Ctrl+Shift+O` eksportuje całą listę AMC do
  rozszerzonego M3U. Enter albo `Ctrl+Alt+W` wysyła wskazany strumień do
  aktywnego urządzenia, `F2` edytuje nazwę i adres, a Delete usuwa wpis wyłącznie
  z AMC. Eksport zapisuje dokładnie bieżącą kolejność widoczną w AMC. Test na
  rzeczywistym urządzeniu wykazał, że dodatkowe odwracanie pliku odwracało listę
  ponownie. Lista jest zachowywana po zamknięciu programu i nie
  modyfikuje danych aplikacji WiiM Home poza świadomym importem pliku przez
  użytkownika.
- Strumienie korzystają ze wspólnych reguł porządkowania Biblioteki:
  `Alt+1` pokazuje najnowsze wpisy najpierw, `Alt+2` porządek alfabetyczny,
  a `Alt+3` kolejność własną. W kolejności własnej `Alt+strzałka w górę/w dół`
  przenosi pojedynczy wpis albo ciągły zaznaczony blok. Eksport do WiiM Home
  oraz `Alt+Page Up/Alt+Page Down` respektują aktualnie wybrany porządek,
  zamiast wracać do technicznej kolejności zapisu. `Ctrl+C` kopiuje nazwy
  wszystkich zaznaczonych strumieni, a `Ctrl+Shift+C` każdą nazwę wraz z
  adresem. Po `F2`, anulowaniu okna i przeniesieniu fokus wraca do właściwego
  wpisu.
- Import M3U/PLS zachowuje kolejność pozycji wewnątrz importowanej partii w
  widoku `Alt+1`. Po uruchomieniu strumienia AMC zapamiętuje ten wybór także
  wtedy, gdy firmware zwraca adres przekierowany, wewnętrzny lub chwilowo
  jeszcze poprzedni. Dzięki temu `Alt+Page Up/Down` nie przełącza się bez
  ostrzeżenia na presety. Rozpoznanie rzeczywistego presetu sprzętowego nadal
  ma pierwszeństwo i przełącza nawigację na presety.
- Delete usuwa wyłącznie lokalny wpis z AMC. Nie istnieje wspierane publiczne
  API do usuwania pozycji z listy Open Network Stream w WiiM Home, więc program
  nie może zgłaszać ani sugerować takiej operacji.

Lokalne API nie udostępnia zapisu ani zmiany kolejności natywnych presetów.
AMC nie zgłasza więc pozornego powodzenia; takie ustawienie nadal wykonuje się
w WiiM Home. Uniwersalne presety AMC są osobną funkcją i mogą później wskazywać
adres, album, playlistę lub inną treść niezależnie od sprzętowych miejsc WiiM.

## Kolejność dalszej integracji

1. **Synchronizacja zapisanych strumieni.** Lokalna lista, import M3U, M3U8 i
   PLS oraz eksport M3U działają od `alpha.279`. Publiczne API nadal nie
   udostępnia odczytu ani zapisu całej listy „Open Network Stream” z WiiM Home,
   dlatego wymiana pozostaje jawną operacją plikową. Automatyczna synchronizacja
   może powstać dopiero po udostępnieniu wspieranego interfejsu przez producenta.
2. **Aktualizacja stanu przez UPnP z odpytywaniem awaryjnym.** Zdarzenia UPnP
   mogą szybciej aktualizować tytuł, pozycję i głośność. Okresowe HTTP pozostaje
   mechanizmem awaryjnym, ponieważ zdarzenia bywają blokowane przez router,
   zaporę lub firmware.
3. **Grupy i multiroom.** Najpierw tylko odczyt ról oraz członków. Dołączanie i
   opuszczanie grupy dopiero po testach na co najmniej dwóch prawdziwych
   urządzeniach, z osobnym sterowaniem głośnością każdego pokoju. Nie wolno
   opierać stanu grupy na jednym polu, którego znaczenie różni się między
   generacjami firmware.
4. **Kolejka urządzenia.** Dostępna tylko, jeśli konkretne urządzenie ogłosi i
   potwierdzi obsługę. Przeglądanie kolejki jest ograniczone do części modeli i
   źródeł, dlatego nie zastąpi kolejki AMC ani kolejki Spotify/TIDAL.
5. **Korektor parametryczny i korekcja pomieszczenia.** PEQ może być
   eksperymentalny po wykryciu możliwości modelu. Korekcja pomieszczenia
   pozostaje tylko do odczytu, dopóki zapis nie zostanie szeroko potwierdzony na
   sprzęcie.
6. **Spotify Connect i TIDAL Connect.** WiiM pokazuje, że dana usługa gra, i
   pozwala na podstawowy transport, ale nie udostępnia jej pełnego katalogu.
   Wyszukiwanie, biblioteka i wybór albumu wymagają adaptera danej usługi.
   Dopiero taki adapter może przekazać odtwarzanie na wybrany WiiM oficjalną
   drogą Connect.

## Reguły zgodności i bezpieczeństwa

- Każda funkcja ma osobną flagę możliwości wykrytą na urządzeniu; odpowiedź
  „unknown command” wyłącza tylko tę funkcję.
- Awaria jednego odczytu nie usuwa urządzenia ani zapisanych ustawień.
- Żądania mają ograniczony czas, rozmiar odpowiedzi i są kierowane wyłącznie do
  lokalnych adresów IP bez przekierowań.
- Do urządzenia można wysłać wyłącznie bezwzględny adres HTTP albo HTTPS bez
  danych logowania. Fragment adresu jest usuwany. Ścieżki lokalne, adresy
  `file:`, nagłówki podcastów prowadzące do RSS oraz prywatne uchwyty usług nie
  są kwalifikowane do tej funkcji.
- Zapis stanu urządzenia nie może przejmować fokusu ani generować okresowych
  komunikatów NVDA.
- Nazwy usług, trybów i elementów są etykietami użytkowymi. Surowe kody,
  rekordy i obiekty API nie trafiają do UI Automation.
- Rozszerzenia społecznościowe wymagają testu na rzeczywistych modelach WiiM
  Pro Plus i WiiM Sound przed uznaniem ich za stabilne.

## Źródła techniczne

- Oficjalne: `HTTP API for WiiM Products`, dokumentacja pomocy WiiM oraz
  instrukcja WiiM Home.
- Społecznościowe do porównania zachowania: `mjcumming/pywiim`, integracja
  Home Assistant `mjcumming/wiim` oraz `gthibo/wiim-universal-remote`.
  Projekty te są źródłem testów i rozpoznania różnic firmware, nie powodem do
  automatycznego włączenia nieudokumentowanych poleceń.
