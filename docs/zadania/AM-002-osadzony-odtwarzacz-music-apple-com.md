# AM-002 — izolowany odtwarzacz music.apple.com

- Status: `W trakcie — izolowany pilot WebView2 uruchomił wskazany utwór ponad minutę; odsłuch niepotwierdzony, brak integracji z AMC`
- Utworzono: 12 listopada 2026
- Obszar: AMC Test, odtwarzanie, DRM, osobny proces
- Zależność: bezpieczny profil i logowanie z AM-001 mogą być współdzielone tylko przez jawny kontrakt

## Cel

Uruchomić prawdziwe `music.apple.com` w izolowanym procesie przeglądarkowym,
pozostawić MusicKit właścicielem chronionego audio i dać AMC wąski mostek do
play, pause, stop, seek, next, previous, głośności, kolejki, repeat i shuffle.
Po logowaniu okno może być ukryte; logowania i 2FA nie wolno ukrywać ani
automatyzować.

## Uzasadnienie i decyzje

- Sidra potwierdza działający wzorzec na Windows, ale jej wydania korzystają z
  CastLabs Electron i produkcyjnego podpisu Widevine VMP. Zwykły WebView2 lub
  Playwright Chromium nie jest automatycznie równoważny.
- WinZapp potwierdza dobry podział dla wxPython: dostępny klient komunikuje się
  z osobnym procesem prowadzącym Chromium. Przenosimy architekturę, nie kod ani
  licencję GPL.
- Mostek ma jawne allowlisty poleceń i zdarzeń. Nie przekazuje adresów mediów,
  zawartości cookies, kodu strony ani dowolnego `executeJavaScript` z GUI.
- Pierwsza bramka sukcesu to słyszalny pełny utwór dłuższy niż 30 sekund,
  prawdziwy czas, pauza, wznowienie i seek. Sam stan „playing” nie wystarcza.
- Korekta celu użytkownika z 13 listopada 2026: Enter w AMC ma rozpocząć
  odtwarzanie dokładnie wybranego utworu w tle, sterowane z AMC. Etap przekazania
  linku z AM-006 nie realizuje tego celu. Oficjalna aplikacja Apple Music
  odtwarza dźwięk na komputerze użytkownika, ale jej interfejs Windows SMTC
  pozwala sterować tylko bieżącą sesją; nie ustawia katalogowego utworu.
- Korekta użytkownika z 13 listopada 2026, po pilocie: okno próbne przejęło
  sterowanie pulpitem i utrudniło korzystanie z AMC. Przed każdym kolejnym
  widocznym testem trzeba uprzedzić użytkownika; podczas zwykłej pracy okno
  odtwarzacza nie może zasłaniać ani blokować AMC. Ta korekta zastępuje
  założenie, że samo widoczne okno pilota jest akceptowalnym stanem pracy.

## Plan pilota

1. Porównać dostępny na Windows runtime: podpisane wydanie Sidry, CastLabs
   Electron oraz zwykły WebView2/Chromium — bez instalacji do AMC.
2. Utworzyć osobny profil Test i wąski lokalny IPC z losową zdolnością sesji.
3. Pokazać okno logowania; potem pozwolić jawnie je ukryć i przywrócić.
4. Uruchomić jeden katalogowy identyfikator dostarczony przez AM-001 i zebrać
   wyłącznie bezpieczny stan odtwarzacza.
5. Dopiero po żywym DRM projektować trwały proces, aktualizacje i pakiet.

## Materiał do dokumentacji użytkownika

Docelowo Enter w sesji Apple Music ma odtwarzać bez przełączania do osobnego
programu. Dźwięk pozostanie jednak w oficjalnym webowym silniku Apple ukrytym
po zalogowaniu, a nie w dekoderze PCM AMC takim jak obecny TIDAL.

## Testy i wynik

- Odczyt źródeł Sidry, commit `76de8f3ca93b2d4b747510855ee5269c2f795b85`.
- Potwierdzona allowlista komend i zdarzeń MusicKit oraz izolacja kontekstu.
- Pierwotnie nie uruchamiano Sidry, Chromium, Apple Music, DRM ani audio; brak
  testu NVDA. Poniższe próby z 13 listopada 2026 zastępują ten stan.
- Korekta z 13 listopada 2026 zastępuje powyższy stan: uruchomiono oryginalne
  Apple Music i odczytano sesję Windows. Faktyczne przełączenie pauza–gra–pauza
  zadziałało przez `TryTogglePlayPauseAsync`; tytuł z sesji nie odpowiadał
  wskazanemu przez AMC utworowi. Odczyt po otwarciu oficjalnego linku katalogowego
  nie potwierdził zmiany tytułu na wskazany utwór. Użytkownik słyszał automatycznie
  uruchamiane utwory w sekwencji prób, ale nie wiadomo, która akcja je wywołała.
  `IsPlaybackPositionEnabled`
  było `false`. Na tym etapie nie wykonano testu pełnego utworu ani pilota DRM.
- Zbudowano bezokienny host z wąskim transportem Apple Music; test kontraktu
  zaliczony. Zmiany nie zostały zainstalowane ani opublikowane.
- 13 listopada 2026: w odrębnym prototypie WinForms/WebView2 1.0.4191.47
  otwarto katalogowy utwór Bruce Springsteen „A Prayer By The River
  (Instrumental)”, ID `1804690076`. Użytkownik samodzielnie zalogował się w
  widocznym oknie. Po zamknięciu ekranu powitalnego wybrano dokładnie ten
  utwór; licznik przeszedł przez 42 s, 53 s, 1:00 i 1:28, bez oznaczenia
  „PODGLĄD”. Pauza zatrzymała licznik na 53 s, wznowienie go uruchomiło.
  Po minimalizacji licznik nadal rósł, utwór doszedł do końca i kolejny zaczął
  grać. Prototyp pozwolił odczytać wyłącznie strukturę publicznego wiersza
  wskazanego utworu; nie odczytywano cookies ani danych logowania.
  To potwierdza wybór utworu i licznik wykraczający poza próbkę w WebView2
  na tym komputerze. Nie potwierdzono osobno słyszalnego dźwięku, ponieważ
  użytkownik korzystał równolegle z AMC. Test **nie** potwierdza działania
  Enter, sterowania ani instalacji AMC.
- Po uwadze użytkownika potwierdzono odczytowo: proces prototypu był już
  zamknięty, proces AMC Test nadal działał. Nie uruchamiano kolejnej próby.

## Znane ograniczenia

- WebView2 uruchomił wskazany utwór z licznikiem ponad minutę; nadal trzeba
  potwierdzić słyszalny dźwięk i utrzymywalne wydanie zintegrowane z AMC.
- Ukrycie okna nie może zatrzymywać odtwarzania ani usypiać zdarzeń MusicKit.
- Minimalizacja okna została sprawdzona; całkowite ukrycie, praca w tle
  podczas korzystania z AMC i brak przejmowania fokusu wymagają osobnego testu.
- Profil logowania prototypu jest osobny i nie jest częścią repozytorium ani
  zainstalowanego AMC. Nie wolno kopiować go do pakietu lub na GitHub.
- Aktualizacja strony Apple może zmienić singleton, kolejkę lub zdarzenia.
- Apple Music na Windows nie udostępnił w sprawdzonym interfejsie systemowym
  polecenia wyboru utworu. Warianty `/url`, `/play` i `music://` nie spełniły
  kryterium zgodności tytułu. Osadzony odtwarzacz webowy wymaga osobnej próby
  zgodnej z preferencją użytkownika dotyczącą przeglądarek i logowania.

## Następny krok

Połączyć wąskie polecenie wyboru utworu z prototypem WebView2 w profilu AMC Test
bez przejmowania fokusu AMC. Przed pierwszą widoczną próbą uprzedzić użytkownika;
sprawdzić słyszalny dźwięk, ukrycie okna, Enter i sterowanie z AMC z NVDA.

## Wersja i commit

Brak kodu produkcyjnego, buildu, instalacji i wydania. Commit dokumentacji
pilota: `5e37190abd3cf76229447544b0bcef6190f7a66d`.

Korekta 13 listopada 2026: kod transportu Windows i kompilacja hosta istnieją
w aktualnych źródłach w commicie `7c3b9a26`; nie ma zainstalowanego ani
wydanego odtwarzacza Apple Music.
