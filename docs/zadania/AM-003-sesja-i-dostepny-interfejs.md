# AM-003 — sesja Apple Music i dostępny interfejs AMC

- Status: `Kod sesji i wspólnych kategorii gotowy, testy celowane przechodzą; odtwarzanie AM-002 nadal oczekuje`
- Utworzono: 12 listopada 2026
- Ostatnia korekta: 12 listopada 2026 — zastępuje wcześniejsze założenie, że
  całe zadanie musi czekać na odtwarzanie AM-002. Czytelną sesję Biblioteki
  można wdrożyć wcześniej, bez pokazywania niedziałającego Enter.
- Obszar: sesje, nawigacja, klawiatura, menu, pomoc i NVDA

## Cel

Dodać Apple Music jako sesję AMC Test zgodną z obecnym, lepszym modelem TIDAL:
Biblioteka, wykonawcy, albumy, playlisty, wyszukiwanie, relacje pod prawą
strzałką, Enter do odtwarzania, transport, kolejka, historia i jawne operacje
Biblioteki, ulubionych oraz playlist.

## Uzasadnienie i decyzje

- Jedno źródło prawdy definiuje nazwę, opis, skrót, widoczność, enabled i
  wykonanie. Z niego korzystają klawiatura, menu, kontekst, `Ctrl+F1`, pełna
  pomoc i `Ctrl+Shift+K`.
- Sesja nie pojawia się, dopóki rzeczywiste adaptery nie zgłoszą swoich
  zdolności. Brak odtwarzacza nie może udawać działającego Enter, a brak prawa
  zapisu nie może pokazywać aktywnego usuwania lub tworzenia playlist.
- `Ctrl+F5` zarządza kontem w niemodalnym oknie. Powrót z logowania skupia
  „Sprawdź logowanie”; ponowne otwarcie nie rozpoczyna OAuth automatycznie.
- Listy przyjmują tylko jawne etykiety użytkowe. ID Apple, URL, obiekty Python,
  nazwy pól i rekordy API nigdy nie są mową NVDA.
- Pamięć playlist i celów jest dzielona według profilu, dostawcy i konta.
  Ponowne otwarcie używa migawki, lecz przed zapisem adapter ponownie sprawdza
  uprawnienie i możliwość edycji.

## Znaczenie Biblioteki i Ulubionych

AMC zachowuje jeden model pojęciowy we wszystkich sesjach, ale nie udaje, że
każdy dostawca przechowuje identyczne typy elementów.

| Polecenie | Stałe znaczenie w AMC | Apple Music |
|---|---|---|
| `Ctrl+L` | Otwórz zapamiętany widok Biblioteki bieżącej sesji | Biblioteka konta: Utwory, Albumy, Wykonawcy i Playlisty |
| `Ctrl+U` | Otwórz Ulubione bieżącej sesji | Centrum Ulubionych: Ulubione utwory, albumy, wykonawcy i playlisty |
| `Ctrl+Shift+L` | Dodaj albo usuń zaznaczenie z Biblioteki | Polecenie kontekstowe, dostępne wyłącznie dla typów i kierunków potwierdzonych przez adapter |
| `Ctrl+Shift+U` | Dodaj albo usuń oznaczenie Ulubione | Polecenie kontekstowe, dostępne dopiero po potwierdzeniu odczytu i obu kierunków zapisu |

W Apple Music Ulubione nie są drugą niezależną biblioteką. Są oznaczeniem
elementów Biblioteki i wpływają także na rekomendacje. Ulubiony utwór trafia
dodatkowo do automatycznej playlisty „Ulubione utwory”, ale ta playlista nie
zawiera ulubionych albumów, wykonawców ani playlist. Dlatego `Ctrl+U` nie może
otwierać wyłącznie tej jednej playlisty.

Dodanie do Ulubionych może również dodać element do Biblioteki Apple. Dla
utworów zależy to od ustawienia konta Apple, natomiast album, wykonawca i
playlista są dodawane do Biblioteki. AMC po zapisie ma więc odświeżyć oba
widoki i ogłosić rzeczywisty wynik, a nie zakładać, że Biblioteka i Ulubione są
całkowicie niezależne.

## Struktura pierwszej sesji Apple Music

Pierwszy ekran sesji jest krótkim centrum nawigacji, a nie jedną ogromną listą:

1. Biblioteka
2. Ulubione
3. Playlisty
4. Wyszukaj w Apple Music
5. Konto Apple Music

`Ctrl+L` otwiera ostatnio używaną kategorię Biblioteki, a przy pierwszym
wejściu centrum: Utwory, Albumy, Wykonawcy, Playlisty. `Ctrl+U` analogicznie
otwiera centrum czterech kategorii Ulubionych. `Ctrl+P` pozostaje bezpośrednim
przejściem do playlist. Powrót zachowuje zaznaczenie i numer strony.

Każda kategoria jest stronicowana. Wiersz listy ma tylko użytkową nazwę, rodzaj
i potrzebne szczegóły. Identyfikatory Apple, adresy API i reprezentacje
obiektów pozostają wyłącznie w modelu.

## Przełączanie rodzaju kolekcji — decyzja końcowa

> **Decyzja końcowa użytkownika z 12.11.2026 — zastępuje wszystkie propozycje
> skrótów w tej sekcji:** `Ctrl+Alt+1` oznacza Utwory, `Ctrl+Alt+2` Albumy,
> `Ctrl+Alt+3` Wykonawcy, a `Ctrl+P` Playlisty. Są to wspólne polecenia AMC,
> rozpoznawane we wszystkich sesjach. Pliki lokalne, Spotify, TIDAL i Apple
> Music mają rzeczywiste wykonanie; pozostałe sesje odpowiadają jednoznacznie,
> że dana kategoria nie jest dostępna, dopóki ich adapter jej nie udostępni.
>
> **Korekta użytkownika z 12.11.2026:** wcześniejszą decyzję o zachowaniu
> aliasów zgodności zastępuje ich usunięcie. `Ctrl+Shift+A` i `Ctrl+Shift+W`
> nie są już skrótami kategorii Spotify ani wspólnymi skrótami AMC.

Model ma dwie niezależne osie: `Ctrl+L` i `Ctrl+U` wybierają zakres kolekcji,
a `Ctrl+Alt+1–3` wybierają rodzaj materiału. `Ctrl+P` otwiera Playlisty.
Wspólna akcja i opis są używane przez klawiaturę, menu, pomoc kontekstową,
pełną pomoc i paletę poleceń; adapter sesji odpowiada tylko za wykonanie.

Późniejsza propozycja użytkownika `Ctrl+Alt+U` została sprawdzona 12 listopada
2026 na aktywnym układzie `00000415`, polski programisty. Sonda Windows
`ToUnicodeEx` zwróciła jeden znak: `€`. Gest kolidowałby więc z wpisywaniem
znaku euro i nie może być poleceniem widoku działającym także w filtrze lub
wyszukiwaniu. Wynik ten zastępuje ogólne przypuszczenie konkretnym pomiarem.

`Alt+1–3` (bez `Ctrl`) nie ma dziś jednego znaczenia we wszystkich sesjach: w kolekcjach
Spotify i TIDAL wybiera sposoby sortowania, w Podcastach inne porządki, a w
głównej Bibliotece lokalnej część tych gestów otwiera nazwane widoki. Przed
ujednoliceniem sortowania potrzebny jest osobny audyt wszystkich sesji.
Ta rozbieżność nie dotyczy wspólnych gestów `Ctrl+Alt+1–3`.

W sesji Apple `Ctrl+P` zachowuje bieżący zakres: po `Ctrl+L` pokazuje playlisty
Biblioteki, a po `Ctrl+U` tylko ulubione playlisty. Użyty z centrum sesji,
gdzie nie wybrano jeszcze zakresu, domyślnie otwiera playlisty Biblioteki.

Odrzucone propozycje (`Ctrl+Shift+N`, `Ctrl+Shift+Y`, `Ctrl+Shift+A/W`) są
zachowane wyłącznie jako historia decyzji; nie wolno ich reklamować ani
obsługiwać w bieżącym interfejsie.

## Presety Apple Music — osobny etap

Presety nie są rodzajem Biblioteki ani playlist. Bieżący model AMC zapisuje
osobno sesję, rodzaj celu, stabilną tożsamość i użytkową nazwę, dlatego nie
potrzebuje oddzielnego „modułu presetów playlist”. Już teraz rozróżnia w
Spotify między innymi utwór, album, playlistę i wykonawcę.

Dla Apple Music w pierwszej kolejności sens mają grywalne cele: utwór, album
i playlista. Preset wykonawcy powinien pojawić się dopiero po ustaleniu, czy
Enter ma otwierać stronę wykonawcy, uruchamiać jego utwory, czy radio
wykonawcy. Presety Apple pozostają wyłączone do czasu działającego AM-002,
potwierdzenia stabilnych identyfikatorów po ponownym logowaniu i testu
uruchomienia. Istniejące skróty presetów `Ctrl+Shift+1–0/-/=` nie kolidują z
gestami rodzaju kolekcji.

## Rzeczywiste możliwości obecnego adaptera

- Potwierdzony odczyt z konta: stronicowane Utwory, Albumy, Wykonawcy,
  Playlisty oraz utwory wybranej playlisty.
- Biblioteka `apple-music-py` potrafi dodawać utwory, albumy, wykonawców i
  teledyski. Usuwanie udostępnia obecnie tylko dla utworów i albumów.
- Biblioteka potrafi tworzyć, aktualizować i usuwać playlisty oraz dodawać i
  usuwać ich utwory.
- Używana rewizja `apple-music-py` nie udostępnia operacji Ulubionych ani
  ocen. Oficjalne Apple Music rozróżnia te możliwości i wspiera wiele typów
  ulubionych, lecz nie wolno wyprowadzać z tego niepotwierdzonej zdolności
  naszego nieoficjalnego adaptera.

Z tego powodu pierwsze wdrożenie GUI ma być odczytowe. `Ctrl+Shift+L` i
`Ctrl+Shift+U` nie mogą być aktywne ani reklamowane w pomocy, dopóki osobne
testy zapisu nie potwierdzą świeżego stanu po operacji. Asymetryczne
„dodajemy, ale nie umiemy niezawodnie usunąć” nie spełnia znaczenia słowa
„przełącz”.

## Etapy wdrożenia

1. Sesja Apple Music w AMC Test: konto, centrum Biblioteki, cztery kategorie,
   stronicowanie, cofanie i zachowanie fokusu. Bez aktywnego Enter i bez
   poleceń zapisu.
2. Wyszukiwanie katalogu oraz relacje album–wykonawca–utwory i podobni
   wykonawcy, tylko tam, gdzie odpowiedź dostawcy je potwierdza.
3. Odczyt Ulubionych jako czterech filtrów Biblioteki. Do tego czasu `Ctrl+U`
   jest niewidoczny w sesji Apple, a nie martwy.
4. AM-002: Enter i sterowanie ukrytym odtwarzaczem webowym Apple.
5. Operacje Biblioteki, Ulubionych i playlist dopiero po testach obu kierunków,
   odświeżenia stanu i komunikatów NVDA.

## Plan pozostałych poleceń do potwierdzenia po działającym pilocie

- Enter: odtwórz wybrany utwór w webowym silniku Apple.
- Prawa strzałka: albumy/utwory/podobni wykonawcy oraz relacje utworu.
- `Ctrl+Shift+L`: dodaj lub usuń z Biblioteki, jeżeli typ i kierunek są
  obsługiwane.
- `Ctrl+Shift+U`: dodaj lub usuń z Ulubionych, jeżeli API pilota potwierdzi
  odczyt oraz oba kierunki zmiany dla danego typu.
- Zarządzanie playlistą: wspólny mechanizm wyboru celu, bez surowych rekordów.
- Transport, kolejka, historia, wyszukiwanie i odczyt stanu: te same kontrakty
  co równoważne sesje, ale tylko dla faktycznie dostępnych możliwości Apple.

## Źródła znaczenia Apple Music

- Apple Support, „How to add and find favorites in Apple Music”: utwory,
  albumy, wykonawcy i playlisty mogą być Ulubione; Bibliotekę można filtrować
  według Ulubionych:
  https://support.apple.com/en-us/111118
- Apple Support, „Mark items as favorites in Apple Music on Windows”:
  https://support.apple.com/en-mide/guide/music-windows/musa407e11b8/windows
- Apple Music API: osobne zasoby Biblioteki, playlist, ocen i dodawania do
  Ulubionych. Oficjalna możliwość nie jest dowodem, że nieoficjalny adapter
  AMC już ją obsługuje:
  https://developer.apple.com/documentation/applemusicapi

## Materiał do dokumentacji użytkownika

To jest projekt interfejsu, nie działająca funkcja w zainstalowanym AMC Test.
Ostateczne nazwy i skróty zostaną pokazane dopiero razem z działającym
zapleczem i testami parytetu. Ulubione Apple Music obejmują więcej niż utwory,
dlatego AMC pokaże je według rodzaju materiału.

**Aktualizacja 12.11.2026:** pierwsza sesja Apple Music jest już podłączona w
źródłach AMC Test. `Ctrl+L` otwiera centrum Utwory, Albumy, Wykonawcy,
Playlisty; `Ctrl+Alt+1–3` przełącza trzy pierwsze kategorie, a `Ctrl+P`
playlisty. Listy są stronicowane, Enter na playliście otwiera jej utwory,
Backspace wraca z zachowaniem zaznaczenia. Enter na materiale uczciwie
informuje, że odtwarzanie należy do następnego etapu.

W sesji lokalnej te same trzy skróty pokazują wszystkie utwory, albumy
wywnioskowane z numerowanych plików w folderach oraz wykonawców wywnioskowanych
z katalogu nadrzędnego albumu. Enter otwiera wykonawcę, album i utwory albumu;
Backspace wraca dokładnie do poprzedniego poziomu.

**Korekta 12.11.2026 — TIDAL i usunięcie starych aliasów:** w sesji TIDAL
`Ctrl+Alt+1` pokazuje wyłącznie ulubione utwory, `Ctrl+Alt+2` albumy zapisane w
Bibliotece, a `Ctrl+Alt+3` zapisanych wykonawców. Widoki korzystają z tego samego
stronicowania, filtrowania, sortowania `Alt+1–3`, pamięci fokusu i zapisu
kolekcji co dotychczasowe główne widoki TIDAL. `Ctrl+Shift+A` i
`Ctrl+Shift+W` zostały usunięte z aktywnej mapy Spotify oraz z automatycznie
generowanej pomocy i palety. Polecenia Spotify nadal istnieją pod wspólnymi
skrótami i w menu.

## Testy i wynik

- Źródła: sesja Apple Music, lokalne kategorie muzyczne, wspólne polecenia,
  menu, pomoc, paleta i izolacja profilu Public są zaimplementowane.
- Celowany zestaw regresji sesji, poleceń, menu, profili, paczki i lokalnych
  kategorii: 154/154 testy zaliczone, bez błędów i pominięć.
- Dwa dodatkowe testy ponownego użycia kompletnego runtime TIDAL/Sonos:
  2/2 zaliczone. Ogólny uruchamiacz użyty z prywatnym runtime zgłosił osobno
  brak zewnętrznego pakietu `pytest` w 21 modułach; nie były to nieudane
  wykonania testów tej zmiany.
- Korekta TIDAL i skrótów: 109/109 celowanych testów Python zaliczonych. Zestaw
  obejmował źródło i nawigację TIDAL, rzeczywisty dyspozytor wspólnych poleceń,
  brak aliasów `Ctrl+Shift+A/W`, Spotify, lokalne kategorie, menu, stan sortowania
  i etykiety skrótów. Wszystkie 32 zestawy kontraktu LiteHost również przeszły.
  Projekt testowy hosta wymagał uzupełnienia starej atrapy backendu i izolowanej
  atrapy resolvera YouTube po wcześniejszym rozszerzeniu interfejsu; nie zmienia
  to kodu wykonywanego przez aplikację.
- Build `test-apple-music-c6efcbda-20261112-v2`: 4225 plików, dwie pełne
  kontrole integralności i kontrolowany start interfejsu oraz hosta — OK.
- Instalacja nie jest jeszcze ukończona. Aktualizator dwukrotnie bezpiecznie
  odmówił podmiany działającego AMC Test, a kontrola UI podała dokładny powód:
  „Nie zamykam AMC Test, ponieważ trwa nagrywanie”. Stary manifest
  `2026.11.11-a51d90b4…` pozostał kompletny.
- Nie wykonano jeszcze ręcznego testu NVDA na zainstalowanej wersji.

## Znane ograniczenia

Zakres zapisów do Biblioteki, Ulubionych i playlist wymaga osobnych,
destrukcyjnych testów pilota i potwierdzenia użytkownika. Transport i kolejka
zależą od AM-002. AMC Public jest poza tym zadaniem. Pierwsza sesja nie będzie
udawać dostępności nieukończonych funkcji.

Paczka zawiera bibliotekę Playwright, ale nie zawiera około 740 MiB binariów
Chromium z laboratorium AM-001. Zapisana sesja DPAPI wystarcza do bieżącego
odczytu Biblioteki. Ponowne logowanie z `Ctrl+F5` w samowystarczalnej
instalacji wymaga osobnego kontraktu dystrybucji przeglądarki i nie jest jeszcze
zweryfikowaną funkcją wydania.

## Wersja i commit

- Kod sesji, wspólnych poleceń i lokalnych kategorii:
  `df5e482997237bf811495616ae48bf3320c7713b`.
- Idempotentny pełny build z już kompletnym runtime TIDAL/Sonos:
  `c6efcbdae1686ac3d22e39fa20efa9a1d954f5e3`.
- Etykieta zweryfikowanej paczki:
  `2026.11.12-c6efcbdae1686ac3d22e39fa20efa9a1d954f5e3-a0608fbd`.
- Kandydat lokalny:
  `D:\Projekty Codex\AMC - integracja wxPython\publish\bundles\test-apple-music-c6efcbda-20261112-v2`.
- Profil Public nie był budowany, instalowany ani publikowany.
