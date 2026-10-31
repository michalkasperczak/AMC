# Spotify w AMC Test: oficjalny odtwarzacz w ukrytym Chrome

Data: 31.10.2026. Status: źródła i próba hosta; publikacja i instalacja są osobnymi etapami.

## Decyzja i korekty

31.10.2026 użytkownik zatwierdził trwałe zastąpienie toru LibreSpot w AMC Test po odsłuchowej próbie Huberta i lokalnej próbie przejścia z Realteka na Denona USB. To zastępuje decyzję z 30.10, by nie wdrażać ukrytego Chrome bez potwierdzonego fizycznego wyjścia. W drugiej lokalnej próbie użytkownik słyszał pierwsze około 10 sekund na Realteku, potem utwór przeszedł na Denona bez restartu. Najwyższa jakość pozostaje celem użytkownika, lecz nie ma potwierdzenia Lossless w Web Playback SDK.

## Zachowanie użytkowe

- Jeden Client ID i jedno logowanie Spotify obsługują katalog oraz odtwarzanie; token odświeża wspólny `SpotifyIntegrationService`. Nie trzeba parować LibreSpot ani podawać klucza Soloist. Konto Premium, zakresy `streaming` i `user-modify-playback-state` oraz dopisanie konta do aplikacji deweloperskiej Spotify pozostają wymagane.
- `Ctrl+F5`, menu i paleta poleceń otwierają okno konta. Zbędne przyciski parowania nie pojawiają się. Stan konta wyjaśnia wspólne logowanie i wymóg Chrome. Działanie konta w AMC Public pozostaje wyłączone przez profil.
- Odtwarzanie utworu lub odcinka Spotify uruchamia Web Playback SDK w systemowym Google Chrome z osobnym profilem i bez widocznego okna. AMC steruje pauzą, wznowieniem, pozycją, głośnością i prędkością przez SDK i istniejący kontrakt hosta.
- `Shift+A` pokazuje aktywne wyjścia Windows z nazwami użytkowymi; wybranie Denona USB przełącza element odtwarzacza przez `setSinkId`, bez ponownego uruchamiania utworu. `Ctrl+[` i `Ctrl+]` zachowują dotychczasowy wybór sąsiedniego wyjścia. Zapisane wcześniej wyjście LibreSpot jest rozpoznawane po nazwie, jeśli istnieje; nowy wybór zapisuje identyfikator endpointu Windows.
- Wcześniejsze funkcje katalogu i zapisana kompletna biblioteka z #9 i #10 pozostają w wydaniu. Ten tor rozwiązuje zaobserwowany przypadek `audio key 0 1` z #11, w którym ten sam utwór grał przez SDK.

## Dostępność i różnice trybów

Lista `Shift+A` korzysta z istniejącego dialogu oraz jawnych nazw wyjść, a identyfikatory pozostają w modelu. Komunikat wyboru ma postać „Urządzenie audio: [nazwa]”. Przy nieudanym przełączeniu AMC nie potwierdza sukcesu. Okno konta zachowuje etykiety i fokus po zamknięciu. AMC Test udostępnia Spotify, AMC Public nie udostępnia tej sesji.

## Realizacja i bezpieczeństwo

Chrome korzysta z osobnego profilu w lokalnych danych AMC Test. Strona SDK działa tylko na pętli zwrotnej `127.0.0.1`; token dostaje przez lokalne żądanie z losowym identyfikatorem i nie trafia do adresu, argumentów procesu ani zdarzeń protokołu AMC. `Browser.setPermission` nadaje `speaker-selection` wyłącznie osadzonej ramce `sdk.scdn.co`. AMC nie włącza automatycznej zgody na mikrofon ani nie wyłącza izolacji witryn. Przełączenie wybiera aktywny `HTMLMediaElement` z ramki przez Chrome DevTools Protocol i sprawdza jego `sinkId`. Proces Chrome uruchomiony przez AMC jest zamykany przy końcu hosta.

## Testy i rzeczywiste wyniki

- `dotnet build` hosta w izolowanym worktree: 0 błędów, 0 ostrzeżeń.
- 42 testy Python okna Spotify i preferencji audio: zaliczone.
- Izolowany host z minimalną kopią ustawień Spotify, głośność 0: `ready`, odtwarzanie rozpoczęte, pozycja rosła. Przełączenie Denon → Realtek → Denon podczas tego samego utworu zwróciło `available=true`, pozycja nadal rosła. Pauza i wznowienie działały. Osobna próba: pauza zadana natychmiast po `Play` zatrzymała przygotowanie (`playing=false`, `paused=true`); wznowienie uruchomiło utwór. Host i Chrome zakończyły się bez pozostawionych procesów.
- Wynik odsłuchu Denona pochodzi z wcześniejszej prywatnej próby użytkownika; nowe próby hosta były bezgłośne. Nie wykonano jeszcze ręcznego odsłuchu NVDA w zainstalowanym AMC Test ani pomiaru jakości.

## Ograniczenia i migracja

Wymagany jest systemowy Google Chrome z Widevine. Web Playback SDK i wewnętrzny element audio ramki mogą się zmienić po aktualizacji Spotify lub Chrome; po takiej zmianie należy ponowić próbę wyjścia. Nie deklarujemy jakości bezstratnej ani trybu wyłącznego. Spotify Connect na inne urządzenia sieciowe pozostaje osobnym zagadnieniem; `Shift+A` wybiera fizyczne wyjścia komputera. Stary kod i binarium LibreSpot pozostają w pakiecie przejściowo, lecz bieżący tor Spotify ich nie uruchamia. Poprzednio zapamiętana nazwa wyjścia jest przyjmowana zgodnościowo.

## Wersja i commit

AMC Test `0.1.0-alpha.459`, gałąź `codex/amc-test-spotify-chrome-alpha459`, podstawa poprzedniego wydania `acbe251960fce7fe15253f08d9a6fb0747999940`. Commit źródłowy paczki: `4217efff92e6cf685e9ab6ed569d4bae8f58e164`.

- Pełna regresja wxPython: 2213 zaliczonych, 150 pominiętych, 3 niepowodzenia z powodu brakującego standardowego hosta Release i brakującego wxPython w procesie potomnym testu. Po zbudowaniu hosta Release dwa testy integracyjne przeszły, a sonda okna Sonos uruchomiona bezpośrednio z właściwym wxPython zwróciła `MODELLESS_OK`. Protokół hosta: 28 zestawów zaliczonych.
- Samowystarczalny bundle Test: `sourceDirty=false`, profil `test`, commit jak wyżej, `bundleId=21899544`, 2141 plików. Kontrola pełnej integralności i kontrolowany start interfejsu oraz hosta: zaliczone.
- Instalator `AMC-Test-2026-10-31-1729.exe`: 133667591 bajtów, SHA-256 `558b308a467300c535ad819c22f7efc1df38760096d83593ae97909262258dd2`. Skan sekretów: zaliczony. GitHub prerelease `test-2026.10.31.1729` wskazuje dokładny commit i zawiera instalator, plik integralności oraz manifest aktualizacji. GitHub odczytowo potwierdził nazwę, rozmiar i taki sam digest SHA-256 instalatora.
- Lokalna instalacja: nie wykonana. Użytkownik sam wykona aktualizację po zakończeniu nagrywania; nie zamykano bieżącego AMC.

## Późniejsza korekta jakości — 31.10.2026

Po publikacji źródeł i instalatora odczytano nowe [pomiary Huberta w #11](https://github.com/michalkasperczak/AMC/issues/11#issuecomment-6543596541). To zastępuje powyższe „brak potwierdzenia Lossless” bardziej konkretnym wynikiem: na jego koncie Premium i utworze `Refuse / Resist` ramka Web Playback SDK raportowała dekoder AAC, 44,1 kHz i wejście 16-bitowe. Bitrate nie był podany. W sprawdzonym torze Chrome gra więc **stratnie**, bez Lossless. Soloist 1.3.10 odtwarzał FLAC po wpisaniu dwóch nieudokumentowanych wartości do `prefs`; domyślnie grał Ogg Vorbis 160 kb/s. [Dodatkowa próba Huberta](https://github.com/michalkasperczak/AMC/issues/11#issuecomment-6543632323) wskazuje możliwość uniknięcia przeliczenia 44,1→48 kHz przy odpowiednim formacie wyjścia, ale dotyczy tylko jego urządzenia i utworu. Nie zmieniano toru AMC na Soloist w tym wydaniu. Opis GitHub i lokalny opis wydania zostały skorygowane, a [komentarz w #11](https://github.com/michalkasperczak/AMC/issues/11#issuecomment-6546199862) odczytowo potwierdzono; sam instalator nadal odpowiada commitowi `4217efff`.
