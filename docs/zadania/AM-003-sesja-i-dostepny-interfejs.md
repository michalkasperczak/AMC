# AM-003 — sesja Apple Music i dostępny interfejs AMC

- Status: `Projekt interfejsu gotowy do zatwierdzenia; odczyt Biblioteki AM-001 działa, odtwarzanie AM-002 nadal oczekuje`
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

## Przełączanie rodzaju kolekcji — decyzja i brakujący gest

Model ma dwie niezależne osie:

- zakres: `Ctrl+L` — Biblioteka, `Ctrl+U` — Ulubione;
- rodzaj materiału w bieżącym zakresie:
  - `Ctrl+Shift+Y` — Utwory; propozycja, ostatnia litera słowa „utwory”,
  - `Ctrl+Shift+A` — Albumy; decyzja użytkownika z 12 listopada 2026,
  - `Ctrl+Shift+W` — Wykonawcy; decyzja użytkownika z 12 listopada 2026,
  - `Ctrl+P` — Playlisty; istniejący wspólny gest AMC.

Przykład: `Ctrl+U`, następnie `Ctrl+Shift+A` otwiera ulubione albumy.
`Ctrl+L`, następnie `Ctrl+Shift+A` otwiera albumy Biblioteki. Zmiana rodzaju
nie przełącza samoczynnie Biblioteki na Ulubione ani odwrotnie.

Wcześniejsza propozycja `Alt+4–7` została odrzucona po decyzji użytkownika,
aby wykorzystać gesty, które już działają w Spotify. `Alt+U` i `Alt+W`
również odpadają, ponieważ otwierają obecnie odpowiednio menu Ustawienia i
Widok; `Alt+P` prowadzi do menu Pliki. Skróty z `Ctrl+Alt` nie są dobrym
zamiennikiem na polskiej klawiaturze, ponieważ ta kombinacja może być
traktowana jak AltGr.

Późniejsza propozycja użytkownika `Ctrl+Alt+U` została sprawdzona 12 listopada
2026 na aktywnym układzie `00000415`, polski programisty. Sonda Windows
`ToUnicodeEx` zwróciła jeden znak: `€`. Gest kolidowałby więc z wpisywaniem
znaku euro i nie może być poleceniem widoku działającym także w filtrze lub
wyszukiwaniu. Wynik ten zastępuje ogólne przypuszczenie konkretnym pomiarem.

`Alt+1–3` nie ma dziś jednego znaczenia we wszystkich sesjach: w kolekcjach
Spotify i TIDAL wybiera sposoby sortowania, w Podcastach inne porządki, a w
głównej Bibliotece lokalnej część tych gestów otwiera nazwane widoki. Przed
ujednoliceniem sortowania potrzebny jest osobny audyt wszystkich sesji.
Ta rozbieżność nie blokuje osobnych, sesyjnych gestów rodzaju kolekcji Apple.

Audyt bieżącego wxPython wykazał także rozbieżność dokumentacji z kodem:
`Ctrl+Shift+A` działa obecnie jako Albumy Spotify, a `Ctrl+Shift+W` jako
Wykonawcy Spotify. Starszy opis ogólny nadal nazywa `Ctrl+Shift+A` lokalnym
widokiem Albumów, lecz taki wspólny lokalny rozkaz nie jest obecnie podłączony
w kodzie wxPython. Wspólne polecenia Albumów i Wykonawców należy teraz
zdefiniować w modelu możliwości, a nie kopiować dwie kolejne tabele skrótów.

W sesji Apple `Ctrl+P` zachowuje bieżący zakres: po `Ctrl+L` pokazuje playlisty
Biblioteki, a po `Ctrl+U` tylko ulubione playlisty. Użyty z centrum sesji,
gdzie nie wybrano jeszcze zakresu, domyślnie otwiera playlisty Biblioteki.

`Ctrl+Shift+N` został odrzucony przez użytkownika 12 listopada 2026, ponieważ
jednoznacznie kojarzy się z poleceniem „Nowy”, na przykład nowym folderem albo
playlistą. Ta decyzja zastępuje wcześniejszą propozycję „N jak nagrania”.
`Ctrl+Shift+T` byłby naturalny od angielskiego „tracks”, ale w AMC podaje czas
całkowity. `Ctrl+Shift+U` zmienia stan Ulubionych, a `Ctrl+Shift+O` otwiera
folder lub importuje strumienie. Audyt bieżącego wxPython nie wykazał żadnego
przypisania `Ctrl+Shift+Y`; pozostaje propozycją dla Utworów do potwierdzenia.

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

## Testy i wynik

Brak nowego kodu GUI, testów GUI, buildu, instalacji i NVDA w AM-003. Odczyt
AM-001 ma osobne zaliczone testy i żywe wyniki, ale nie dowodzi działania
sesji. Kryteria AM-003 obejmą początkowy fokus, strzałki, Enter, Escape,
anulowanie, powrót fokusu i ponowne otwarcie, a także zgodność nazw,
widoczności, stanu enabled i wykonania na wszystkich powierzchniach poleceń.

## Znane ograniczenia

Zakres zapisów do Biblioteki, Ulubionych i playlist wymaga osobnych,
destrukcyjnych testów pilota i potwierdzenia użytkownika. Transport i kolejka
zależą od AM-002. AMC Public jest poza tym zadaniem. Pierwsza sesja nie będzie
udawać dostępności nieukończonych funkcji.

## Wersja i commit

Brak zmian kodu i wersji AMC. Pierwotny commit dokumentacji planu:
`5e37190abd3cf76229447544b0bcef6190f7a66d`. Bieżący kontrakt oczekuje na
zatwierdzenie i osobny commit dokumentacji.
