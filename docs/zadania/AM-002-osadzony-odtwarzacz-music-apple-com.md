# AM-002 — izolowany odtwarzacz music.apple.com

- Status: `W trakcie — kod integracji Enter i sterowania w AMC Test jest kandydatem źródłowym; bez instalacji i żywego odbioru`
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
- Doprecyzowanie z 13 listopada 2026: użytkownik chce kontynuacji prac w tle,
  a kolejne próby z widocznym oknem dopiero wtedy, gdy sam wskaże, że nie
  korzysta z komputera. To zastępuje wcześniejszy plan natychmiastowego
  wznowienia pilota po logowaniu. Testy źródeł nie otwierają okien.

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
- 13 listopada 2026: dodano osobny proces WinForms/WebView2
  `src/AccessibleMediaController.AppleMusicWebHost/` z profilem tylko dla AMC
  Test i prywatnym protokołem JSON-lines. Przyjmuje numeryczny katalogowy
  identyfikator utworu oraz stałą listę poleceń. Nie przyjmuje kodu strony,
  adresów mediów, ciasteczek ani danych logowania. Startuje zminimalizowany;
  widoczne okno można wywołać wyłącznie jawnym poleceniem menu „Pokaż okno
  odtwarzacza Apple Music”.
- W `wxlite/amc_wx_lite/apple_music_web.py`, `navigation.py`, `gui.py`,
  `shortcuts.py`, `menu_model.py` i `i18n.py` Enter na utworze kieruje
  polecenie odtwarzania do izolowanego procesu; Spacja i Shift+Page Down/Up
  sterują aktywnym webowym odtwarzaczem. Pierwsza pozycja menu kontekstowego
  nadal jawnie otwiera utwór w oryginalnym Apple Music. Przełączenie do niej
  zamyka aktywny proces webowy, aby nie grały równolegle dwa źródła Apple.
  Źródła i pomoc opisują tę różnicę. Nie ma zmiany zainstalowanego AMC.
- `wxlite/tools/Build-AMCBundle.ps1`, `Promote-AMCPythonTestBuild.ps1` i
  `build_bundle.py` uwzględniają proces webowy tylko dla profilu Test;
  moduły i pliki hosta są filtrowane z Public. Testy Python z 13 listopada:
  **67 zaliczonych** w obszarze Apple Music i układu pakietu. Test kontraktu
  C# `--apple-music-web`: **1 zestaw zaliczony**. Kompilacja procesu Release:
  **0 błędów, 1 ostrzeżenie** o konfliktujących referencjach WindowsBase
  dodanych przez pakiet WebView2. `git diff --check` bez błędów białych znaków.
- Kontrola katalogu tłumaczeń `test_i18n.py`: **12 zaliczonych, 1 pominięty,
  2 niezaliczone**. Oba niezaliczenia dotyczą zastanego braku pełnego katalogu
  angielskiego w innych obszarach; nowe etykiety Apple dodano do słownika.
  Ten wynik nie jest pełnym zaliczeniem testów.

## Znane ograniczenia

- WebView2 uruchomił wskazany utwór z licznikiem ponad minutę; nadal trzeba
  potwierdzić słyszalny dźwięk i utrzymywalne wydanie zintegrowane z AMC.
- Ukrycie okna nie może zatrzymywać odtwarzania ani usypiać zdarzeń MusicKit.
- Minimalizacja okna została sprawdzona; całkowite ukrycie, praca w tle
  podczas korzystania z AMC i brak przejmowania fokusu wymagają osobnego testu.
- Profil logowania prototypu jest osobny i nie jest częścią repozytorium ani
  zainstalowanego AMC. Nie wolno kopiować go do pakietu lub na GitHub.
- Aktualizacja strony Apple może zmienić singleton, kolejkę lub zdarzenia.
- Nowy proces nie był jeszcze uruchomiony z AMC ani z pakietu. Nie potwierdzono
  działania Enter, słyszalnego dźwięku, pauzy, wznowienia, fokusu NVDA ani
  logowania w jego osobnym profilu. Prototyp ma inny profil niż nowy proces,
  więc pierwsze logowanie może być potrzebne ponownie. Nie skopiowano profilu.
- Wywołanie `setQueue` i kontrola tytułu/identyfikatora w MusicKit są
  kandydatem implementacji; tylko żywa próba potwierdzi, czy strona Apple
  przyjmuje te polecenia w zminimalizowanym WebView2. Przewijanie, stop,
  głośność, repeat i shuffle nie mają jeszcze pełnego sterowania w AMC.
- Apple Music na Windows nie udostępnił w sprawdzonym interfejsie systemowym
  polecenia wyboru utworu. Warianty `/url`, `/play` i `music://` nie spełniły
  kryterium zgodności tytułu. Osadzony odtwarzacz webowy wymaga osobnej próby
  zgodnej z preferencją użytkownika dotyczącą przeglądarek i logowania.

## Następny krok

Zaczekać na sygnał użytkownika, że komputer jest wolny. Wtedy przygotować
oddzielny pakiet próbny Test, uprzedzić o dokładnym początku próby, sprawdzić
logowanie w osobnym profilu, słyszalny dźwięk, Enter, pauzę, wznowienie,
minimalizację i fokus NVDA. Do tego momentu nie instalować ani nie uruchamiać
nowego procesu na pulpicie użytkownika.

## Wersja i commit

Kandydat źródłowy i kompilacja procesu istnieją; brak instalacji i wydania.
Commit źródeł, testów i pakowania: `094188aa` z 13 listopada 2026.
Commit dokumentacji pilota: `5e37190abd3cf76229447544b0bcef6190f7a66d`.

Korekta 13 listopada 2026: kod transportu Windows i kompilacja hosta istnieją
w aktualnych źródłach w commicie `7c3b9a26`; nie ma zainstalowanego ani
wydanego odtwarzacza Apple Music.
