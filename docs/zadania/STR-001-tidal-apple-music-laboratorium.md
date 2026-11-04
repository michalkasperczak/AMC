# STR-001 — laboratorium TIDAL i Apple Music

- Status: `Wstrzymane — Apple Music ma bezsekretny prototyp serwerowy, ale
  prawdziwy test i wdrożenie wymagają własnego zespołu Apple Developer
  należącego do właściciela AMC`
- Utworzono: 18 października 2026
- Obszar: usługi strumieniowe i profil testowy
- Polecenie uruchamiające: `Uruchom zadanie STR-001`

## Cel

Zbadać oficjalne możliwości TIDAL i Apple Music bez obiecywania użytkownikom
funkcji, których dostawcy nie pozwalają niezależnemu klientowi wykonać.
Najpierw powstaje raport i mały eksperyment w profilu testowym; dopiero potem
może zapaść osobna decyzja o wdrożeniu.

## TIDAL

- Ponownie sprawdzić aktualne Web API, SDK oraz warunki deweloperskie.
- Pełne odtwarzanie wolno realizować wyłącznie przez oficjalny, niezmieniony
  moduł Player udostępniony przez TIDAL; katalog, Biblioteka i playlisty nie są
  automatycznie dowodem możliwości odtwarzania.
- Zachować istniejące ustalenia i nie powtarzać identycznej próby logowania ani
  pytania do TIDAL bez nowego dowodu.

## Apple Music

- Sprawdzić Apple Music API, MusicKit on the Web i ewentualny natywny klient
  macOS oddzielnie.
- Apple wymaga identyfikatora MusicKit i podpisanego tokenu deweloperskiego;
  dane Biblioteki wymagają także zgody użytkownika i Music User Token.
- Odtwarzanie na Windows należy badać przez oficjalne MusicKit on the Web, a
  nie przez odtwarzanie przechwyconych adresów multimediów. Natywny macOS może
  korzystać z MusicKit dla Swift, ale jest to osobny adapter platformy.

## Korekty użytkownika

- 2026-10-25: użytkownik uruchomił równoległą pracę nad Sonosem i Apple Music
  oraz dopuścił użycie tego samego publicznego serwera. Następnie ustalił, że
  posiadany przez znajomego klucz należy do obcego zespołu Apple Developer, a
  zakup własnego członkostwa bez dowodu działania byłby ryzykiem. Zastępuje to
  założenie, że sam istniejący token można po prostu podłączyć do publicznego
  AMC. Dalsze prawdziwe wdrożenie jest wstrzymane; dopuszczalny pozostaje
  laboratoryjny test pod świadomą kontrolą właściciela klucza.
- 2026-10-25, korekta późniejsza: po sprawdzeniu aktualnej umowy Apple Developer
  Program License Agreement wycofano także propozycję rzeczywistego testu na
  koncie znajomego. Zastępuje ona ostatnie zdanie poprzedniej korekty. Sama
  zgoda znajomego ani prywatny charakter próby nie czynią AMC jego aplikacją i
  nie spełniają warunku pracy wykonawcy wyłącznie w jego imieniu. Projekt nie
  poprosi znajomego o plik `.p8`, developer token, dostęp do konta ani
  uruchomienie brokera dla AMC. To ostrożna decyzja projektowa na podstawie
  bieżącego tekstu umowy, a nie porada prawna.

## Wynik badania Apple Music z 25.10.2026

Oficjalny kontrakt Apple potwierdza:

- każde żądanie Apple Music API wymaga developer tokenu podpisanego ES256;
- token zawiera Team ID jako `iss`, Key ID w nagłówku `kid`, czas wydania i
  wygaśnięcia; Apple dopuszcza najwyżej sześć miesięcy, ale laboratorium AMC
  ogranicza własne tokeny do 5–1440 minut, domyślnie jednej godziny;
- dla klienta webowego Apple zaleca claim `origin`; prototyp AMC wydaje token
  tylko dla jednego wcześniej dozwolonego originu HTTPS;
- MusicKit on the Web sam zarządza Music User Tokenem po zgodzie użytkownika.
  Prywatna Biblioteka, playlisty i historia wymagają tej zgody;
- MusicKit on the Web oficjalnie obsługuje odtwarzanie muzyki, teledysków i
  radia w przeglądarce. Nadal trzeba osobno potwierdzić zachowanie w WebView2,
  kontrolę z natywnego interfejsu AMC i pełną dostępność z NVDA;
- Apple Music API udostępnia katalog, wyszukiwanie, Bibliotekę, playlisty,
  ostatnio odtwarzane elementy, rekomendacje i oceny. Nie znaleziono
  oficjalnego kontraktu pobierania chronionych plików do własnego odtwarzacza;
  AMC nie będzie przechwytywał adresów mediów ani budował pobierania offline;
- członkostwo Apple Developer Program kosztuje obecnie 99 USD rocznie lub
  równowartość lokalną. Samo bezpłatne konto nie daje potrzebnego zestawu
  Media ID i klucza do tej publicznej integracji.

Źródła oficjalne, sprawdzone 25.10.2026:

- https://developer.apple.com/support/terms/
- https://developer.apple.com/fr/support/terms/apple-developer-program-license-agreement/
- https://developer.apple.com/documentation/applemusicapi/generating-developer-tokens
- https://developer.apple.com/documentation/applemusicapi/user-authentication-for-musickit
- https://developer.apple.com/musickit/
- https://developer.apple.com/help/account/capabilities/create-a-media-identifier-and-private-key
- https://developer.apple.com/help/account/membership/program-enrollment

## Prototyp bez sekretów

Powstała osobna usługa `AccessibleMediaController.AppleMusicTokenBroker`.
Czyta prywatny klucz wyłącznie z pliku na serwerze, sprawdza klucz EC P-256,
wydaje krótki token ES256, wymaga dokładnego originu HTTPS, stosuje wąski CORS,
limit osobno dla klienta, kontrolę zaufanego reverse proxy i bezpieczne
endpointy stanu. Nie zawiera prawdziwego Team ID, Key ID, Media ID, klucza,
developer tokenu ani Music User Tokenu.

Testy laboratoryjne zaliczyły 12/12 przypadków: konfigurację, origin, zakres
czasu, walidację klucza, podpis i claimy JWT, endpoint, CORS, HTTPS, zaufane
proxy, limit per klient oraz brak identyfikatorów w stanie usługi.

Prototyp nie został spakowany, wdrożony ani podłączony do AMC. Nie dowodzi
jeszcze logowania użytkownika, odtwarzania, zgodności terytorialnej ani NVDA.

## Najtańsza zgodna bramka decyzji

1. Nie używamy konta, klucza ani tokenu znajomego — ani publicznie, ani do
   rzeczywistej prywatnej próby AMC.
2. Bez konta Apple wolno kontynuować jedynie lokalne testy naszej architektury:
   konfiguracji, podpisu testowym kluczem, CORS, limitów i dostępnego interfejsu
   na danych pozorowanych. Nie nazywamy tego testem integracji z Apple Music.
3. Kasperczak albo formalny właściciel AMC, na przykład FIRR, ustala kto ma być
   właścicielem produktu i zespołu Apple Developer. Dopiero ten podmiot może
   rozważyć własne członkostwo i zaakceptować aktualne warunki Apple.
4. Dopiero na własnym zespole powstaje osobny Media ID i klucz. Pierwszym
   etapem jest niepubliczny pilot: logowanie użytkownika, pełny utwór, pauza,
   przewijanie, zmiana pozycji, odnowienie sesji i cały przebieg NVDA.
5. Publikacja następuje tylko po udanym pilocie, sprawdzeniu aktualnej umowy,
   wytycznych marki i ustaleniu modelu utrzymania. Sam prywatny zasięg nie
   pozwala użyć poświadczeń zespołu, do którego AMC nie należy.

## Organizacja pracy

- Badanie może zostać wykonane w osobnym zadaniu Codex albo przez Hermesa,
  kiedy użytkownik jawnie je uruchomi. Raport musi wrócić do tego pliku i
  zawierać źródła, datę, ograniczenia konta oraz rozdzielenie faktów od
  hipotez.
- Żadne zewnętrzne zadanie nie otrzymuje tokenów, danych kont ani prywatnego
  profilu AMC. Próby logowania i odtwarzania wykonuje się dopiero lokalnie, po
  osobnej zgodzie i w odizolowanym profilu testowym.
- TIDAL i Apple Music pozostają poza publicznym profilem do czasu osobnego
  odbioru prawnego, funkcjonalnego i dostępnościowego.

## Kryteria zakończenia badania

1. Macierz: katalog, wyszukiwanie, Biblioteka, playlisty, historia,
   odtwarzanie, sterowanie, pobieranie i ograniczenia terytorialne.
2. Opis oficjalnego logowania bez sekretu osadzonego w publicznym kliencie.
3. Prototyp nie zapisuje ani nie odtwarza chronionych adresów poza oficjalnym
   SDK.
4. Decyzja: odrzucić, pozostawić w laboratorium albo przygotować osobny plan
   wdrożenia testowego.

## Wynik wykonania

Do uzupełnienia po pracy:

- Data etapu: 25 października 2026
- Wersje API i warunków:
- Apple Music API i MusicKit on the Web v3 według dokumentacji dostępnej
  25.10.2026; członkostwo 99 USD/rok według strony Apple.
- Wynik TIDAL: nie był przedmiotem tego etapu.
- Wynik Apple Music: architektura i podpisywanie tokenu są wykonalne, lecz
  brakuje dowodu realnego MusicKit/WebView2/NVDA i stabilnego właściciela
  zespołu deweloperskiego.
- Rekomendacja: pozostawić w laboratorium bez połączenia z Apple. Nie używać
  zespołu znajomego nawet do rzeczywistego testu. Wrócić do pilota dopiero po
  wskazaniu właściciela AMC i założeniu przez niego własnego zespołu Apple
  Developer; przed kosztem można nadal oceniać makietę interfejsu i dostępność.

## Materiał do dokumentacji użytkownika

Apple Music nie jest jeszcze sesją AMC Test ani AMC Public. Żaden przycisk,
skrót ani ekran logowania nie został dodany. Użytkownik nie przekazuje AMC
hasła Apple ani prywatnego klucza deweloperskiego. Przyszłe logowanie będzie
odbywać się wyłącznie w oficjalnym MusicKit, a odtwarzanie na Windows pozostanie
w oficjalnym odtwarzaczu webowym Apple sterowanym przez dostępny interfejs AMC.

## Uzasadnienie i decyzje

- Ten sam fizyczny serwer może później utrzymywać Sonosa i Apple Music, lecz
  będą to osobne procesy, sekrety, limity, hosty i procedury wycofania.
- Obcy klucz i token nie są importowane, używane ani uruchamiane dla AMC.
- Brak realnej próby oznacza brak obietnicy publicznego Apple Music.

## Testy i wynik

- `dotnet run --project tests/AccessibleMediaController.AppleMusicTokenBroker.SmokeTests/AccessibleMediaController.AppleMusicTokenBroker.SmokeTests.csproj -c Release`
  — 12/12 zaliczone 25.10.2026.

## Znane ograniczenia

- brak prawdziwego klucza i realnego developer tokenu;
- brak próby Music User Token i konta subskrybenta;
- brak hosta HTTPS, strony odtwarzacza, WebView2 i sterowania z AMC;
- brak ręcznego testu NVDA;
- brak decyzji o docelowym właścicielu Apple Developer Team.

## Wersja i commit

Kod laboratoryjny powstał lokalnie 25.10.2026 w commicie
`8655c063353ac4ffe8803f3da9adb0e691885d40`. Późniejsza korekta tego samego
dnia wycofuje zawartą w nim propozycję testu na koncie znajomego. Nie wykonano
instalacji, publikacji ani prawdziwego połączenia z Apple.


## Badanie TIDAL — 4 listopada 2026 (uzupełnienie, tylko analiza)

### Materiał do dokumentacji użytkownika

W AMC Test sesja TIDAL pokazuje katalog i kolekcję przez oficjalne API, ale pełny dźwięk powstaje w oryginalnej aplikacji TIDAL. Enter na utworze przekazuje go do TIDALa; Ctrl+Enter uruchamia album albo playlistę. Spacja na liście przełącza odtwarzanie przez sesję multimedialną Windows po wcześniejszym udanym przekazaniu. Page Down i Page Up przechodzą po liście źródłowej AMC, jeśli ona istnieje, a w przeciwnym razie sterują kolejką TIDALa. Shift+Page Down i Shift+Page Up kierują następny/poprzedni bezpośrednio do TIDALa. Aktualnie AMC nie oferuje przewijania utworu TIDAL; odczyt czasu bywa niedostępny, gdy TIDAL nie wystawia pozycji przez Windows. Potwierdzone komunikaty z kodu to m.in. „Najpierw uruchom utwór w oryginalnym TIDALu”, „Oryginalny TIDAL nie odpowiada” oraz komunikat z tytułem po pomyślnym przejściu. Nie przeprowadzono w tym badaniu próby z NVDA ani żywej sesji TIDAL.

### Uzasadnienie i decyzje

- Bieżący tor: `wxlite/amc_wx_lite/gui.py` -> `host_client.py` -> `LiteTidalDesktopCoordinator` -> `TidalDesktopController` (uruchamia oryginalnego TIDALa z portem CDP, otwiera album/playlistę i klika przycisk w jego interfejsie). Po starcie transport używa `ExternalMediaController` i Windows `GlobalSystemMediaTransportControlsSession`: toggle, next, previous, state. To dwa różne kanały sterowania. Katalog i playlisty korzystają z oficjalnego API.
- Page Up/Down na liście mają osobną gałąź w `gui.py:14861-14878`, lecz tylko po udanym przekazaniu z AMC (`_tidal_desktop_has_playback`). Na liście źródłowej Page Up/Down mogą ponownie przekazać wybrany utwór przez CDP, a nie tylko wywołać systemowe skip. Shift+Page Up/Down idą wprost przez SMTC. Kod hosta dopuszcza jedynie `toggle`, `next`, `previous`, `state`; działania przewijania nie należą do `TIDAL_SUPPORTED_ACTIONS`.
- Pierwszy kandydat do pilota: odczytać `IsPlaybackPositionEnabled` i oś czasu sesji Windows, sprawdzić `TryChangePlaybackPositionAsync`, rzeczywistą zmianę pozycji oraz zachowanie podczas pauzy, końca utworu i przejścia między utworami. Wcześniejszy pomiar z 11.09.2026 podawał pozycję stale zero, więc samo istnienie API Windows nie dowodzi działania w TIDALu.
- Gdy SMTC odmówi, można zbadać kontrolkę paska postępu oryginalnego TIDALa przez UI Automation albo obecny CDP, tylko jako sterowanie jego własnym interfejsem. Wymaga odczytu aktualnej pozycji, pewnego potwierdzenia po skoku, testu aktualizacji TIDALa i NVDA. Kontrolki DOM/CDP są prywatną strukturą aplikacji i mogą się zmienić. Port debugowania daje szeroki dostęp do zalogowanej aplikacji; jego narażenie i stały numer 9222 trzeba ocenić przed rozbudową.
- `pywinauto`/UIA oraz Playwright/CDP są alternatywnymi narzędziami automatyzacji interfejsu, nie bibliotekami dającymi nowe prawa do pełnego audio. `tidalapi` jest nieoficjalne; przedstawiciel TIDAL określił jego użycie API jako nieautoryzowane. Oficjalne SDK Web jest już w starszej części repozytorium; zmiana języka albo opakowania nie podnosi poziomu dostępu do pełnego odtwarzania. Oficjalny Embed w teście z 09.09.2026 pozostał przy 30 sekundach po zwykłym logowaniu. TIDAL Connect oficjalnie obsługuje obecnie partnerów urządzeń, bez publicznego interfejsu kontrolera dla AMC.
- Rekomendacja: utrzymać odtwarzanie w oryginalnym TIDALu; priorytetem jest pilot możliwości przewijania przez Windows SMTC i dopiero potem bezpieczny wariant automatyzacji widocznej kontrolki. Nie wdrażać nieoficjalnej biblioteki do pobierania chronionych strumieni ani nie deklarować pełnego odtwarzania w samym AMC. To obserwacja i propozycja pilota, nie zatwierdzone wdrożenie.

### Testy i wynik

- 04.11.2026: odczyt aktualnych źródeł AMC Python, istniejącego testu trasowania klawiszy i dokumentacji projektu. Nie uruchamiano testów automatycznych, kompilacji, instalacji ani żywego TIDALa; nie zmieniono kodu aplikacji.
- Pytanie AMC do TIDAL, dyskusja #384, pozostaje bez odpowiedzi i bez komentarzy w dniu badania.

### Znane ograniczenia

- Brak żywego testu `IsPlaybackPositionEnabled` i `TryChangePlaybackPositionAsync` na aktualnej wersji TIDALa. Brak potwierdzenia przewijania i odporności DOM/UIA po aktualizacjach.
- Istniejący warunek `_tidal_desktop_has_playback` oznacza, że AMC nie przejmuje transportu TIDALa uruchomionego całkowicie poza AMC; wymaga to osobnego projektu stanu sesji.

### Źródła zewnętrzne sprawdzone 04.11.2026

- TIDAL Developer Guidelines: https://developer.tidal.com/documentation/guidelines/guidelines-developer-guidelines
- TIDAL Design Guidelines: https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines
- TIDAL Connect: https://developer.tidal.com/documentation/connect
- Oficjalny Embed, mostek komunikatów: https://github.com/tidal-music/embed-player/blob/main/src/client/js/message-bridge.js
- Oficjalne pytanie AMC, nadal bez odpowiedzi: https://github.com/orgs/tidal-music/discussions/384
- Stanowisko TIDAL o `tidalapi`: https://github.com/orgs/tidal-music/discussions/35
- Windows SMTC, możliwości: https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionplaybackcontrols
- Windows SMTC, skok do pozycji: https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssession.trychangeplaybackpositionasync
- pywinauto/UIA: https://github.com/pywinauto/pywinauto
- Playwright/CDP: https://playwright.dev/docs/api/class-browsertype#browser-type-connect-over-cdp
- Zagrożenia portu debugowania Chromium: https://developer.chrome.com/blog/remote-debugging-port

### Wersja i commit

Badanie dotyczy gałęzi `codex/konfigurowalne-skroty-20261026` w repozytorium AMC Python. Nie powstała nowa wersja aplikacji, instalacja ani publikacja. Ta sekcja dokumentuje wyłącznie analizę; commit notatki: nie wykonano.


## Korekta badania TIDAL — 4 listopada 2026: pełne utwory w odtwarzaczu AMC

### Powód korekty i zakres

Po uwadze użytkownika ponownie zbadano możliwość odtwarzania pełnych utworów we własnym odtwarzaczu AMC, a nie tylko sterowania oryginalną aplikacją TIDAL. Ta sekcja **zastępuje** wcześniejsze zbyt mocne wrażenie, że biblioteki alternatywne technicznie nie umożliwiają takiego odtwarzania. Zastępuje też wcześniejszą rekomendację ograniczoną do pilota przewijania oryginalnego TIDALa: pilot SMTC jest osobnym usprawnieniem obecnego toru, a nie odpowiedzią na potrzebę własnego odtwarzacza.

### Wynik i decyzja

- Pełne utwory we własnym silniku są technicznie możliwe: istnieją działające projekty, których źródła pobierają manifest/URL przez prywatne lub starsze wewnętrzne endpointy TIDAL i przekazują audio do systemowego odtwarzacza, mpv albo natywnej biblioteki. Nie potwierdzono ich działania na koncie użytkownika ani w AMC; deklaracje działania pochodzą z projektów. Wniosek nie oznacza oficjalnego uprawnienia AMC do takiego strumienia.
- Nie znaleziono biblioteki, którą można uczciwie nazwać stabilnym, oficjalnie dopuszczonym modułem pełnego odtwarzania TIDAL dla Python/wxPython z prawem przekazania strumienia do odtwarzacza AMC. Oficjalne wytyczne wymagają oficjalnego, niezmienionego modułu TIDAL Player i zakazują obchodzenia ograniczeń API. Starsze rozwiązania mogą przestać działać po zmianie endpointu, manifestu, DRM lub polityki konta.
- Oficjalny `@tidal-music/player` jest odrębną drogą: załadowany w aplikacji mógłby zapewnić własny interfejs sterowania, lecz TIDAL sam przydziela FULL/PREVIEW. Próba AMC we wrześniu zwróciła `FULL_REQUIRES_SUBSCRIPTION`, a próba Embed po zwykłym logowaniu nadal odtwarzała tylko 30 sekund. To nie dowodzi trwałej odmowy dla każdego konta ani nie rozstrzyga, czy istnieje wymagana zgoda lub inny sposób logowania. Pytanie AMC #384 nie dostało odpowiedzi.
- Druga legalna koncepcja techniczna to osadzony oryginalny odtwarzacz webowy TIDAL z zewnętrzną klawiaturową nakładką AMC. Nie jest to natywny silnik audio AMC. Na Windows nie ma potwierdzenia, że WebView2 odtworzy pełne utwory z DRM; `tidal-hifi`, opakowanie strony TIDAL w Electron, samo zgłasza typowy błąd DRM S6007 u użytkowników Windows. Wymaga osobnego testu logowania, pełnej długości, przewijania, DRM i NVDA przed obietnicą funkcji.
- Dla publicznego AMC nie włączać teraz prywatnych endpointów, przechwytywania URL ani odtwarzania poza oficjalnym Playerem. Jeśli użytkownik zechce rozważyć eksperyment nieoficjalny, osobno ustalić ryzyko warunków TIDAL, licencji zależności, awaryjność i granicę między AMC Test a wersją publiczną. Sama analiza nie autoryzuje takiego wdrożenia.

### Porównanie kodu z GitHuba

- `EbbLabs/python-tidal` (`tidalapi`, LGPL-3.0): rzeczywista biblioteka Python, wystawia adres strumienia. Pasuje językowo do AMC, ale używa nieoficjalnego API; przedstawiciel TIDAL nazwał użycie tego API nieautoryzowanym. AMC ma już oficjalne API dla katalogu, więc sam katalog nie uzasadnia tej zależności.
- `ArthurCarrenho/Tid3` (AGPL-3.0): młoda aplikacja Windows WinUI 3, kod pobiera URL przez wewnętrzny endpoint i odtwarza w `Windows.Media.Playback.MediaPlayer`/`AdaptiveMediaSource`; obejmuje kolejkę i seek. Dowód koncepcji natywnego Windows, nie gotowy moduł Python; bezpośrednie zapożyczenie kodu wymaga analizy AGPL.
- `fezzik-the-giant/riptide` (GPL-3.0): Rust TUI z mpv, kod korzysta ze starszego wewnętrznego endpointu i opisuje nowszy oficjalny manifest jako zaszyfrowany. Dowód architektury z własnym odtwarzaczem; zależność od prywatnego protokołu i GPL.
- `Kadajett/Tauri-Tidal` (brak odnalezionej licencji): wczesna aplikacja z Symphonia/cpal, również ścieżka wewnętrznego endpointu. Nie traktować kodu jako dostępnego do włączenia do AMC bez zgody autora; brak weryfikacji Windows i konta użytkownika.
- `Mastermindzh/tidal-hifi` (MIT): opakowuje oryginalny web player i steruje elementem audio/Media Session, ma seek i zewnętrzne API sterowania. Nie jest biblioteką strumieni dla natywnego odtwarzacza AMC. Projekt dokumentuje częsty błąd DRM S6007 na Windows, więc nie jest gotowym rozwiązaniem dla AMC.

### Zachowanie, skróty, dostępność, testy i ograniczenia

To badanie nie zmieniło żadnego polecenia AMC, skrótu, menu, pomocy, komunikatu NVDA ani kodu odtwarzacza. Nie wykonano próby logowania, odtwarzania, seek ani NVDA; ustalenia dotyczą repozytoriów i dokumentacji. Brak dowodu, że którykolwiek z projektów odtwarza dziś pełne utwory na koncie użytkownika lub spełnia wymagania dostępności AMC. Przed pilotem własnego interfejsu trzeba zdefiniować etykiety, fokus, komunikaty, kolejkę i parytet poleceń zgodnie z globalnymi zasadami dostępności. Nie powstał build, instalacja ani wydanie. Badanie na gałęzi `codex/konfigurowalne-skroty-20261026`; commit tej korekty nie wykonano.

### Źródła do ponownej weryfikacji

- https://developer.tidal.com/documentation/guidelines/guidelines-developer-guidelines
- https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines
- https://github.com/tidal-music/tidal-sdk-web/tree/main/packages/player
- https://github.com/orgs/tidal-music/discussions/384
- https://github.com/orgs/tidal-music/discussions/35
- https://github.com/EbbLabs/python-tidal
- https://github.com/ArthurCarrenho/Tid3
- https://github.com/fezzik-the-giant/riptide
- https://github.com/Kadajett/Tauri-Tidal
- https://github.com/Mastermindzh/tidal-hifi
- https://github.com/Mastermindzh/tidal-hifi/blob/master/docs/known-issues.md

### Doprecyzowanie po odczycie kodu oficjalnego Embed

Wytyczne TIDAL przewidują pełną długość dla subskrybenta **wewnątrz TIDAL Embed**; poza Embed ograniczają odtwarzanie do 30 sekund. Kod oficjalnego `embed-player` wystawia do strony nadrzędnej stan, czas i długość oraz przyjmuje żądania `play` i `pause`. W sprawdzonym `message-bridge.js` nie ma komend `seek`, `next` ani `previous`. Pełna długość w Embed nie oznacza więc automatycznie pełnej klawiaturowej obsługi z AMC. To doprecyzowuje opis oficjalnego `@tidal-music/player` powyżej; nie sprawdzono, czy istnieje inny publiczny interfejs Embed dla tych poleceń.

- https://github.com/tidal-music/embed-player/blob/main/src/client/js/message-bridge.js
