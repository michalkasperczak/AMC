# AM-002 — izolowany odtwarzacz music.apple.com

- Status: `Oczekuje — architektura Sidry sprawdzona; brak uruchomionego pilota DRM`
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
- Nie uruchamiano Sidry, Chromium, Apple Music, DRM ani audio; brak testu NVDA.

## Znane ograniczenia

- Nie wiadomo jeszcze, który runtime da pełne DRM w wydaniu możliwym do
  utrzymywania przez AMC.
- Ukrycie okna nie może zatrzymywać odtwarzania ani usypiać zdarzeń MusicKit.
- Aktualizacja strony Apple może zmienić singleton, kolejkę lub zdarzenia.

## Wersja i commit

Brak kodu produkcyjnego, buildu, instalacji i wydania. Commit dokumentacji
pilota: `5e37190abd3cf76229447544b0bcef6190f7a66d`.
