# AUTH-PUBLIC-20261106 — logowanie TIDAL i Spotify dla odbiorcy Public

Status na 7.11.2026 przed budową alpha.482. Pełne odtwarzanie i rzeczywista jakość źródeł są potwierdzone. Źródła dokończonej poprawki zachowują częstotliwość także wtedy, gdy TIDAL nie podaje jej w metadanych. Cicha próba „Sara” przez rzeczywisty host potwierdziła PCM 44,1 kHz i etykietę „źródło AAC, jakość stratna”. Przeszły testy dekodera, transportu i 122 celowane testy Pythona. Zainstalowany AMC Test pozostaje alpha.481, a uruchomiona paczka próbna v7 zawiera wcześniejszy wariant. Instalator alpha.482, odbiór NVDA i zewnętrzne logowanie Public pozostają otwarte.

## Materiał do dokumentacji użytkownika

Szkic krok po kroku znajduje się w `docs/public/LOGOWANIE_TIDAL_SPOTIFY_SZKIC_2026-11-06.md`. Zainstalowany AMC Public nie zawiera sesji TIDAL ani Spotify. W źródłach nowego kandydata Public włączono TIDAL, jego pełny odtwarzacz i okno konta z polem Client ID. Użytkownik naciska `Ctrl+F5`, wpisuje Client ID tylko wtedy, gdy AMC nie dostarczy wspólnego identyfikatora, a potem wybiera „Zaloguj w przeglądarce”. Hasło wpisuje wyłącznie na stronie TIDAL. Pełny odtwarzacz wymaga osobnego kodu urządzenia; jego poświadczenia w Test i Public mają różne nazwy w Menedżerze poświadczeń Windows. Własny Client ID pozostaje wariantem osobistej, ograniczonej konfiguracji. Spotify w Public nie został włączony.

## Uzasadnienie i decyzje

Oficjalny OAuth katalogu TIDAL nie zapewnia uprawnienia do pełnego odtwarzania w wewnętrznym odtwarzaczu. Obecny pełny tor AMC Test używa `tidalapi`, a wytyczne TIDAL wymagają oficjalnego Player SDK i ograniczają odtwarzanie poza Embed do próbki. Spotify Development Mode dopuszcza pięciu użytkowników na aplikację; szerszy dostęp wymaga Extended Quota Mode. Własne identyfikatory użytkowników mogą być przydatne dla ich osobistych prób, ale nie stanowią potwierdzenia gotowości publicznego wydania.

**Korekta użytkownika z 6.11.2026:** pełne audio TIDAL ma pozostać w planie aktualizacji Public. Użytkownicy mają logować się na własne, płatne konta; właściciel AMC akceptuje nieoficjalny charakter obecnego toru i jego ryzyko. Ta decyzja zastępuje wcześniejszą decyzję z tego samego dnia o wykluczeniu `tidalapi` z planu Public do czasu pisemnej zgody TIDAL. Nie jest jeszcze poleceniem publikacji obecnej paczki Test jako Public. Przed wydaniem trzeba przygotować profil Public, przeprowadzić odbiór na świeżym profilu zewnętrznej osoby i jasno opisać granice integracji. Brak odpowiedzi TIDAL na wcześniejsze pytanie nie wymaga ponownego identycznego zgłoszenia.

## Testy i wynik

Przeczytano kod i bieżący rejestr AMC Test oraz oficjalne dokumenty dostawców. Testy źródeł: 14/14 profilu i pakowania, następnie 53/53 profilu, menu, palety i natywnego GUI, 15/15 okna konta/klienta hosta, 1/1 kontraktu katalogu TIDAL, 1/1 kontraktu natywnego żądania, 39/39 testów launchera, 2/2 izolacji natywnych poświadczeń oraz 13/13 wyszukiwania po przeniesieniu importu Spotify. Kompilacja hosta po izolacji poświadczeń: 0 błędów, 0 ostrzeżeń. Wcześniejsze próby paczki: runtime Public bez współdzielonych zależności; runtime testowy z już dołączonym TIDAL; pierwszy pełny kandydat z kodem 2. Diagnostyka wykazała brak modułu `spotify_source` w Public przy przedwczesnym imporcie przez wyszukiwanie. Po przeniesieniu importu do funkcji Spotify, końcowy build `public-tidal-isolated-v2-20261106` złożył 3143 pliki, przeszedł pełną integralność i kontrolowany start interfejsu oraz hosta na jednorazowym profilu. Potwierdzono brak procesów pozostawionych przez paczkę, obecność TIDAL, brak modułu Spotify i zgodność sześciu kluczowych plików Pythona z bieżącymi źródłami. Rozmiar paczki około 448,7 MB. Nie wykonano OAuth z kontem zewnętrznym, pełnego odsłuchu, NVDA, instalacji ani publikacji.

**Próba użytkownika 6.11.2026:** uruchomiono paczkę obok AMC Test i otwarto sesję TIDAL przez `Ctrl+Shift+S` oraz konto przez `Ctrl+F5`. Nowy profil Public pokazał brak Client ID katalogu. Sprawdzono, że profil Test ma Client ID, a Public nie. Przez okno konta zapisano w Public wyłącznie istniejący Client ID z Test, bez przenoszenia tokenów; komunikat okna i odczyt `state.json` potwierdziły zapis. Nie uruchamiano jeszcze logowania OAuth ani pełnego odtwarzania. Użytkownik zgłosił nieoczekiwany dźwięk po starcie; jego źródło nie jest jeszcze ustalone. W dostępnych komunikatach okna występuje mieszanie polskiego i angielskiego (`Account`, `Saved`), do poprawienia przed wydaniem.

**Korekta próby z 6.11.2026, zastępuje zdanie o niewykonanym OAuth powyżej:** użytkownik wybrał „Zaloguj w przeglądarce” i potwierdził logowanie na stronie TIDAL. Okno AMC Public podało „Zalogowano konto TIDAL.”, więc callback dotarł do hosta. Nie wykonano jeszcze osobnego logowania odtwarzacza ani odsłuchu. Public wyświetlał pusty widok TIDAL po OAuth, ponieważ frontend nie otwierał ponownie Biblioteki. W oknie pozostał przycisk „Zaloguj w przeglądarce” i brakowało odłączania konta. Użytkownik zauważył też klucze dostępu na stronie TIDAL; to interfejs dostawcy, a nie zmiana logowania w AMC.

**Poprawka źródeł 6.11.2026:** po udanym logowaniu katalogu TIDAL AMC ponownie ładuje bieżący widok Biblioteki/Ulubionych/Playlist. Okno pokazuje „Zaloguj ponownie w przeglądarce” oraz „Odłącz konto katalogu TIDAL” tylko dla połączonego katalogu. Odłączenie wymaga potwierdzenia, usuwa token katalogu z bieżącego profilu, czyści stan kolekcji i nie zmienia osobnej sesji odtwarzacza. Po odłączeniu widok TIDAL jest odświeżany. Wyjaśnienie w oknie mówi wprost o własnej kolekcji i kodzie odtwarzacza. Opis `Ctrl+F5` w pomocy/palecie oraz tłumaczenia angielskie okna uzupełniono. Testy: 35/35 celowanych testów Python, 31/31 zestawów kontraktów hosta; sprawdzono tłumaczenie nowych etykiet i wariantu statusu z nazwą użytkownika. Pełny zestaw i18n ma trzy niepowodzenia dotyczące wcześniej nieuzupełnionego szablonu i innych tekstów, w tym TIDAL poza tym oknem. Nowa paczka `public-tidal-login-refresh-v2-20261106` przeszła pełną integralność i kontrolowany start hosta oraz GUI. Jej cztery zmienione pliki w `app` są identyczne ze źródłami, a paczka nie zawiera `state.json`. Odbiór na koncie użytkownika i odsłuch nie są jeszcze potwierdzone.

## Znane ograniczenia

Nie znamy przyznanego statusu produkcyjnego rejestracji AMC u TIDAL ani Spotify. Oficjalna strona TIDAL dotycząca zarządzania aplikacjami nie udostępniła czytelnej treści w narzędziu badawczym; jej szczegóły panelu trzeba ponownie sprawdzić przed instrukcją końcową. Obecna ścieżka pełnego TIDAL jest nieoficjalna i może przestać działać po zmianach usługi. Opłacona subskrypcja użytkownika nie zmienia sama w sobie zasad TIDAL dotyczących integracji zewnętrznej. Wcześniejszy diagnostyczny start bez jawnej izolacji został odrzucony przez automatyczny przegląd i nie został wykonany. Użytkownik 6.11.2026 zezwolił tylko na izolowaną diagnostykę. Tryb kontrolowanego startu ma teraz zablokowany dostęp do poświadczeń TIDAL i własny katalog danych tymczasowych; jego wynik jest dodatni. Nadal brak odbioru rzeczywistego logowania i odtwarzania.

## Wersja i commit

Zmiany źródłowe są niezatwierdzone commitem. Powstała odizolowana, zweryfikowana technicznie paczka próbna w `.test-output/public-tidal-isolated-v2-20261106`; nie jest to zainstalowany build ani wydanie Public. Równoległe zmiany presetów profilu należą do innego zadania i nie są częścią tego zapisu.

Najnowsza paczka do próby po zalogowaniu: `D:\Projekty Codex\AMC - integracja wxPython\.test-output\public-tidal-login-refresh-v2-20261106\AMC.exe`. Stara paczka może być nadal uruchomiona; przed próbą nowej należy ją zamknąć. Nowa paczka użyje istniejącego stanu profilu Public, ale nie została zainstalowana ani opublikowana.

## Szkic changelogu do korekty przed wydaniem

- Profil Public otrzymuje sesję TIDAL i pełny odtwarzacz w AMC.
- Okno konta TIDAL pozwala wpisać Client ID katalogu, skopiować adres powrotu i rozpocząć logowanie w przeglądarce.
- Poświadczenia katalogu i pełnego odtwarzacza są rozdzielone między Test i Public.
- Polecenia TIDAL w menu, pomocy i palecie mają wspólne nazwy dla obu profili.
- Spotify nie jest częścią tej paczki próbnej. Treść changelogu wymaga odbioru użytkownika i testu na zewnętrznym koncie; nie publikować automatycznie.

## Dalszy odbiór 6.11.2026

Po uruchomieniu `public-tidal-login-refresh-v2-20261106` odczyt interfejsu potwierdził „Konto TIDAL jest połączone”, przyciski „Zaloguj ponownie w przeglądarce” i „Odłącz konto katalogu TIDAL” oraz wczytaną Bibliotekę TIDAL: 810 pozycji, 150 pokazanych na pierwszej stronie. Jest to wynik działania na koncie użytkownika, a nie tylko test źródłowy. Odtwarzania pełnego utworu nadal nie potwierdzono.

Źródła poprawki lokalizacji dialogu złożono w `public-tidal-login-refresh-v3-20261106`; integralność i kontrolowany start przeszły, ale v3 nie była jeszcze otwarta jako główne okno użytkownika. Uruchamiacz AMC dopuszcza jedną instancję na profil. Ponieważ v2 była otwarta, próba startu v3 tylko aktywowała v2. Równolegle otwarte AMC Test pozostawiono bez zmian. Katalogi `.test-output` i `.test-tmp` to zachowane pliki próbne, nie procesy ani skrypty działające w tle. Konsolowy odczyt procesów w ograniczonym środowisku błędnie wskazał brak aplikacji; poprawny odczyt okien wykazał otwarte v2 i AMC Test. Nie wykonano instalacji, odłączenia konta, czyszczenia plików ani publikacji.

## Błąd odtwarzania i diagnostyka 7.11.2026

Użytkownik zgłosił po Enter na utworze w albumie „Real Love” komunikat „Nie udało się odtworzyć utworu TIDAL.” Ta obserwacja zastępuje wcześniejsze stwierdzenie o niewykonanym odsłuchu: próbę odtwarzania wykonano, lecz audio nie wystartowało. Konto katalogu pozostaje połączone, a Biblioteka i lista utworów albumu są widoczne. Puste pole Client ID po zapisaniu jest zamierzonym zachowaniem okna i nie oznacza wylogowania.

Izolowana próba na zapisanej sesji odtwarzacza Public, bez wyjścia audio i bez ujawniania tokenów, wykazała: przywrócenie poświadczenia, pobranie obu wersji „Change” z albumu, manifest HTTP 200 o pełnej prezentacji, przygotowanie lokalnego proxy, dekodowanie 1 sekundy przez FFmpeg oraz przekazanie utworu przez rzeczywisty adapter do atrapy silnika. Obie wersje przeszły. To **nie** potwierdza rzeczywistego odtwarzania w działającym LiteHost; przyczyna błędu w oknie użytkownika pozostaje nieustalona. Dzienniki działającego hosta nie zawierały wpisu wskazującego etap tej konkretnej nieudanej próby.

Źródło `tidal_native_playback.py` rozróżnia teraz przy błędzie etapy: zapisane logowanie odtwarzacza, logowanie kodem, dane utworu, manifest, lokalne proxy, przyjęcie strumienia przez host oraz uruchomienie audio przez host. Komunikaty dla NVDA są wyłącznie tekstem użytkowym, bez identyfikatorów, URL ani treści wyjątków. Testy atrap 12/12, w tym odrzucenie przez host, awaria manifestu zawierająca sekret i asynchroniczny błąd audio, przeszły. Zmiana nie dodaje polecenia ani skrótu; zestaw pomocy i paleta pozostają zgodne z dotychczasowym zachowaniem.

Paczka `public-tidal-playback-stage-v4-20261107` w `.test-output` została zbudowana z bieżących źródeł Public. Manifest obejmuje 3143 pliki, pełna kontrola integralności oraz kontrolowany start GUI i hosta przeszły. To jest wyłącznie build diagnostyczny; v2 nadal była otwarta podczas budowy. v4 nie została zainstalowana, opublikowana ani sprawdzona na rzeczywistym audio. Przed wydaniem należy odtworzyć błąd w v4, zanotować dokładny nowy komunikat, usunąć przyczynę i potwierdzić pełny odsłuch oraz NVDA.

**Korekta z 7.11.2026:** po zamknięciu v2 uruchomiono dokładny `AMC.exe` paczki v4; sprawdzono procesy launchera, Pythona i hosta z tego samego folderu oraz zgodność SHA-256 adaptera w paczce ze źródłem. Nie instalowano paczki. Użytkownik zauważył pusty widok TIDAL po starcie v4. Dziennik hosta potwierdza synchronizację 946 elementów, więc to nie jest dowód utraty konta ani braku danych. W `_load_initial_content` brakowało gałęzi otwierającej przywróconą aktywną sesję TIDAL, podczas gdy inne sesje ją mają. W źródłach dodano otwarcie zapamiętanego głównego widoku TIDAL, z powrotem do Biblioteki dla widoku albumu; celowany test startu przeszedł (1/1; siedem starszych testów tego modułu pominięto z powodu niedostępnej opcjonalnej bazy fixture). Ta ostatnia poprawka **nie znajduje się jeszcze w działającej v4**. W v4 obejściem jest `Ctrl+L`. Wynik pełnego odtworzenia nadal oczekuje na próbę użytkownika.

## Korekta identyfikatorów utworów, 7.11.2026

Paczka `public-tidal-playback-and-library-v5-20261107` zawiera poprawkę przywracania Biblioteki przy starcie. Sprawdzono w działającym oknie 810 pozycji (150 na pierwszej stronie). Użytkownik spróbował Enter na „Change” i usłyszał „Nie udało się pobrać danych utworu TIDAL.” Wcześniejsze stwierdzenie, że przyczyna tej próby jest nieustalona, zastępuje poniższa diagnoza.

W izolowanej diagnostyce pobrano album i utwory z oficjalnego katalogu przez LiteHost, przywrócono zapisaną sesję pełnego odtwarzacza Public, bez wyjścia audio. Oba identyfikatory „Change” były poprawnymi identyfikatorami numerycznymi. `tidalapi.Session.track()` dla obu zwróciło `ObjectNotFound`, natomiast żądanie manifestu `playbackinfopostpaywall` dla obu zwróciło HTTP 200. Stary adapter niepotrzebnie wymagał dodatkowego pobrania tytułu i czasu trwania przez `Session.track()` przed manifestem. To rozbieżność między dostępnym w katalogu utworem a starszym punktem metadanych używanym przez `tidalapi`.

Źródła `gui.py` i `tidal_native_playback.py` przekazują teraz do adaptera tytuł i dodatni, ograniczony czas trwania z wiersza katalogu. Adapter pomija stare zapytanie metadanych, gdy te dane są dostępne, a bez czasu trwania zachowuje dotychczasową ścieżkę. Nie zmieniono sposobu logowania, manifestu, proxy, poleceń, skrótów ani komunikatów NVDA. Testy adaptera 13/13 i GUI 24/24 przeszły. Izolowana próba na rzeczywistym utworze katalogu potwierdziła przekazanie manifestu i czasu trwania przez poprawiony adapter do atrapy silnika bez błędu; nie włączała audio.

Paczka `D:\Projekty Codex\AMC - integracja wxPython\.test-output\public-tidal-playback-catalog-v6-20261107\AMC.exe` została zbudowana jako Public: 3143 pliki, kontrolowany start GUI i hosta OK, pełna kontrola integralności z kodem 0. SHA-256 `gui.py` i `tidal_native_playback.py` w paczce jest zgodny ze źródłami. Łagodnie zamknięto v5 i uruchomiono wyłącznie v6; potwierdzono procesy launchera, Pythona i LiteHost z katalogu v6. W oknie v6 `Ctrl+L` pokazało Bibliotekę TIDAL: 810 pozycji, 150 na pierwszej stronie, z albumem „Real Love”. Pełnego odtworzenia w v6 i obserwacji NVDA jeszcze nie potwierdzono. Nie instalowano ani nie publikowano paczki. Zmiany nie mają commita.

## Odbiór odtwarzania i jakość, 7.11.2026

**Korekta obserwacji użytkownika:** użytkownik potwierdził, że pełny utwór gra w paczce v6. Zastępuje to wcześniejsze „nie potwierdzono pełnego odtworzenia”; nie oznacza jeszcze testu NVDA wszystkich kontrolek, instalacji ani wydania. Użytkownik zauważył niejasny komunikat „Jakość TIDAL” i odczyt około 321 kb/s, 48 kHz dla „All Woman”.

Izolowany odczyt manifestu dla obu wersji „Change” potwierdził HTTP 200, `audioQuality=HIGH` i AAC, mimo żądania `HI_RES_LOSSLESS`; osobne żądanie `LOSSLESS` również zwróciło `HIGH`. Trzy utwory z innych albumów także zwróciły `HIGH`. Według enum w używanej wersji `tidalapi`, `HIGH` oznacza stratne AAC do około 320 kb/s; obecne nazewnictwo TIDAL klasyfikuje AAC do 320 kb/s jako Low, bezstratny FLAC 16/44,1 jako High, a HiRes FLAC jako Max. Wynik dotyczy tych utworów. Wcześniejszy wniosek o braku Max w całej sesji Public został wycofany po porównaniu poniżej.

W źródle adaptera poprawiono etykietę jakości: zwrócone AAC/MP3 zostaje nazwane „Jakość stratna”, a Max/High podaje się tylko dla odpowiednio oznaczonego FLAC. Celowany test dowodzi, że odpowiedź `HIGH` z AAC nie jest ogłaszana jako Max; cały zestaw adaptera 14/14 przeszedł. Ta zmiana jest **tylko w źródłach**, nie w działającej v6. Przed kolejną paczką trzeba sprawdzić możliwą drogę do FLAC/Max; użytkownik może nadal słuchać v6. Nie zmieniono sesji, poświadczeń ani instalacji.

## Porównanie Test i Public po uwadze użytkownika, 7.11.2026

Użytkownik wskazał, że przed uproszczeniem logowania w profilu Developer słyszał Max, i polecił ustalić, czy był rzeczywisty. Ta korekta zastępuje przypuszczenie o ograniczeniu całej sesji Public na podstawie kilku utworów AAC. Historyczny rekord `TID-LIST-PLAYER-20261105-followup.md` opisuje rzeczywisty FLAC 24/192 i dekodowanie PCM 5.11.2026. Wcześniejszy komunikat Max w zainstalowanym adapterze pochodził z rozwiązanego manifestu DASH, a nie z samego żądania jakości.

Porównano zainstalowany AMC Test (manifest źródła `c872d0dee094ebc4896996222d07a4c9723bd29e`) z Public v6. SHA-256 plików `tidalapi/session.py`, `tidalapi/request.py` oraz `tidal_dash_audio.py` jest identyczny. Bieżące repozytorium i gałąź potwierdzono przez Git. Działające procesy należały do zainstalowanego AMC Test. Próby porównawcze użyły osobnego procesu, bez hosta i wyjścia audio, bez ponownego logowania i zapisu poświadczeń.

`probe_profile_quality.py` przywrócił oba zapisane poświadczenia. Konta i kraje są takie same, tokeny sesji różne. Dla tego samego historycznego utworu „The Golden Age” Becka z „Sea Change” oba żądania zwróciły HTTP 200, zgodny identyfikator utworu, `FULL`, `HI_RES_LOSSLESS`, FLAC 24 bit/192000 Hz. Dla tego samego „Change”, znalezionego przez starszy katalog, obie sesje zwróciły `FULL`, `HIGH` i AAC. Uproszczone logowanie Public zachowało dostęp do Max dla zweryfikowanego utworu. Sam odczyt 321 kb/s dla „All Woman” pozostaje prawidłową obserwacją jakości tego strumienia.

`probe_public_max_decode.py` uruchomiono dokładnym runtime paczki Public v6 z jej `app` na początku ścieżki importu. Jej parser i lokalne proxy otrzymały strumień zapisanej sesji Public. FFprobe niezależnie potwierdził rzeczywisty kodek FLAC, 192000 Hz, 24 bity. FFmpeg odczytał 1 sekundę i oddał 384000 bajtów PCM f32 stereo po konwersji do 48 kHz, kod 0. Dane audio i adresy pozostawały w pamięci. To potwierdza dekodowanie, nie jest odsłuchem NVDA utworu Max w GUI.

Skrypty diagnostyczne pozostają w `C:\Users\micha\Documents\Codex\2026-11-06\t\work\tidal_public`. Wynik ujęto w szkicu instrukcji i rejestrze zadań. Nie powstał nowy build, instalacja ani publikacja. Zmiany jakości AAC i dokumentacji pozostają bez commita. Do odbioru słuchowego można użyć „The Golden Age” → Beck → „Sea Change”; oczekiwany komunikat startu obejmuje „Jakość Max, FLAC 24 bit, 192 kHz”.

## Rzeczywisty odtwarzacz AMC Test i konwersja PCM, 7.11.2026

Na kolejną uwagę użytkownika sprawdzono działające okno zainstalowanego AMC Test — 06.11.2026, 01:44. W sesji TIDAL odtwarzacz pokazywał „Sara, 321 kb/s, 48 kHz”. W jego zwykłym wyszukiwaniu (`Ctrl+L`, `Ctrl+F`, zapytanie „The Golden Age Beck”, wynik utworu Becka, Enter) uruchomiono „The Golden Age”. Komunikat startu widoczny przez UI Automation brzmiał „The Golden Age: Odtwarzanie TIDAL rozpoczęte. Jakość Max, FLAC 24 bit, 192 kHz.”. Pasek stanu tego samego działającego odtwarzania podał „The Golden Age, 5612 kb/s, 48 kHz”. To jest test z normalnego interfejsu zainstalowanej aplikacji, nie tylko osobny próbnik. Nie zarejestrowano osobno mowy NVDA.

Próbnik `probe_test_history_quality.py` użył dokładnych identyfikatorów zapisanych przez tę instalację w historii. Dla „Sara” zwrócono AAC, dla „The Golden Age” FLAC 24 bit/192000 Hz, w obu przypadkach odpowiedź dotyczyła żądanego utworu. Zatem AAC słyszane przez użytkownika odpowiada rzeczywistemu źródłu, natomiast komunikat Max dla utworu kontrolnego również ma potwierdzenie w danych źródłowych.

**Nowy problem, korygujący wcześniejsze wrażenie pełnego zachowania 192 kHz w odtwarzaczu:** `FfmpegLocalAudioWaveStream` definiuje `OutputSampleRate = 48_000` i zawsze przekazuje FFmpeg `-ar 48000`. `WindowsMediaOutput.OpenTransientFlacReader` używa tej ścieżki również dla TIDAL. Odczyt działającego procesu potomnego LiteHost potwierdził lokalne wejście proxy, format f32le i argument `-ar 48000`. Odtwarzacz rzeczywiście konwertuje źródło 192 kHz do PCM 48 kHz. `AudioOutputDeviceCatalog` korzysta ponadto z WASAPI Shared. Wcześniejsza diagnostyka dowodziła formatu źródła i możliwości dekodowania; nie sprawdziła zachowania częstotliwości w rzeczywistym torze aplikacji.

Do poprawy pozostaje przekazywanie jakości źródła i częstotliwości PCM jako jawnie opisanych danych oraz usunięcie stałej konwersji dla toru HiRes, z uwzględnieniem urządzenia i silnika tempa. Samą etykietą nie można naprawić konwersji. W tym etapie wykonano badanie i korektę dokumentacji. Nie zmieniono kodu silnika, nie zbudowano ani nie zainstalowano nowej paczki. AMC Test pozostawiono z uruchomionym utworem kontrolnym. Przed ewentualną instalacją trzeba ponownie sprawdzić stan nagrywania.

## Poprawka znanej częstotliwości i próba v7, 7.11.2026

Po poleceniu kontynuacji zmieniono adapter Pythona, kontrakt natywnego żądania, LiteHost oraz `WindowsMediaOutput` i `FfmpegLocalAudioWaveStream`. Żądanie przekazuje opcjonalne `sampleRateHz` i gotowy, walidowany opis `sourceQualityLabel`. Znana częstotliwość trafia do FFmpeg i formatu PCM, długość i seek korzystają z odpowiadającej jej liczby bajtów. Przechowywane źródło dekodera zachowuje tę wartość podczas przewijania i odbudowy. Stare żądania oraz odpowiedzi TIDAL bez częstotliwości nadal używają domyślnych 48 kHz. Nie dodano nowego polecenia ani skrótu.

Pasek stanu i most NVDA otrzymują jeden tekst hosta, np. „The Golden Age, źródło Max, FLAC 24 bit, 192 kHz, 5612 kb/s, PCM w silniku 192 kHz”. AAC dostaje opis jakości stratnej. Dotyczy to formatu wewnątrz silnika; wyjście WASAPI pozostaje współdzielone i Windows może konwertować dźwięk do formatu urządzenia. Nie potwierdzono wyjścia bit-perfect.

Testy przed budową: adapter 14/14; `dotnet run` ProtocolTests `--tidal-native` 1/1; Windows.SmokeTests `--transient-flac-output` 1/1 z rzeczywistym dekodowaniem i seek dla 44100, 96000 i 192000 Hz. Artefakty testów powstały osobno w `.test-tmp/tidal-hires-protocol-20261107` i `.test-tmp/tidal-hires-decoder-20261107`. Pierwszy build zatrzymał kontrolowany zakaz podwójnego dołączenia zależności TIDAL. Utworzono kopię kompletnego runtime Test z pominięciem sześciu modułów TIDAL dołączanych osobno, pozostawiając zainstalowany runtime bez zmian.

`Build-AMCBundle.ps1 -Profile test` z tą kopią runtime oraz przypiętymi zależnościami z instalacji zakończył build `D:\Projekty Codex\AMC - integracja wxPython\.test-output\test-tidal-hires-pcm-v7-20261107`. Paczka ma 4196 plików, przeszła pełną integralność i kontrolowany start GUI/hosta. Test rzeczywistego LiteHost z tej paczki `wxlite/tests/test_tidal_native_host_transport.py`, wskazany przez `AMC_LITE_HOST_EXE`, przeszedł 1/1. Używa cichego syntetycznego FLAC 192 kHz i osobnego stanu; sprawdza odczyt 192 kHz po starcie, seek, pauzie/wznowieniu i odbudowie wyjścia. To test silnika, nie odsłuch NVDA.

Po odpowiedzi użytkownika o braku nagrywania łagodnie zamknięto zainstalowany AMC Test i uruchomiono dokładny `AMC.exe` v7. Potwierdzono procesy launchera, Pythona i hosta z katalogu v7 oraz zgodność adaptera z bieżącymi źródłami. Okno ma nazwę „AMC Test — 07.11.2026, 13:08”. Próba sterowania GUI została przerwana fizycznym Escape. Użytkownik następnie podał odczyt NVDA „Sara, źródło stratna, AAC, 321 kb/s, PCM w silniku 48 kHz”. Nowa paczka jest uruchomiona próbnie, bez instalacji i publikacji. Zawiera bieżące źródła z wcześniejszymi zmianami roboczymi; nie utworzono commita ani wydania.

Niezależny `probe_sara_bitstream.py` odczytał dokładny identyfikator „Sara” z historii Test i poprosił o `HI_RES_LOSSLESS`. Odpowiedź dotyczyła tego samego pełnego utworu. FFprobe zbadał rzeczywiste dane przez lokalne proxy i wykazał AAC, 44100 Hz, stereo, kod 0. Wyjścia audio nie używano. Odczyt NVDA w v7 jest więc zgodny z rozróżnieniem źródła stratnego i PCM w silniku, ale zachowanie źródłowej częstotliwości nadal wymaga obsługi przypadków, w których serwer nie podaje jej w metadanych. To ogranicza wcześniejsze ogólne zdanie „tor zachowuje częstotliwość źródła” do źródeł o przekazanej częstotliwości. Dla „Sara” zachodzi konwersja 44,1 → 48 kHz, bez podniesienia jakości AAC.

## Weryfikacja ośmiu prób użytkownika, 7.11.2026

Na prośbę użytkownika porównano faktyczne strumienie utworów Lisy Stansfield, Omegi, Misi Furtak i Nosowskiej z odczytem jakości w AMC/NVDA. `probe_attempted_albums.py` użył dokładnych identyfikatorów ośmiu zapisanych prób, żądania `HI_RES_LOSSLESS`, resolverów i proxy paczki v7. FFprobe odczytał kodek, częstotliwość, kanały i dla FLAC głębię bitową z rzeczywistych danych, niezależnie od etykiety. Wszystkie osiem odpowiedzi miało pełną prezentację. Próba zakończyła się bez błędów, bez wyjścia audio i bez zapisania mediów, tokenów lub adresów strumieni.

| Album wybrany w AMC | Zbadane utwory | Faktyczny strumień otrzymany od TIDAL | Zgodność z raportem NVDA |
|---|---|---|---|
| Lisa Stansfield — Real Love | I Will Be Waiting; Real Love | AAC stereo 44,1 kHz | „stratna” jest zgodne |
| Omega — 10000 lépés | Gyöngyhajú lány | AAC stereo 44,1 kHz | „stratna” jest zgodne |
| Misia Furtak — Otuchy | Białe kwadraty; Cała naprzód; Przed siebie; Nie bój się | FLAC stereo 24 bit/96 kHz | „Max” jest zgodne |
| Nosowska — NIGREDO | SZKODA BRAW | FLAC stereo 24 bit/44,1 kHz | „Max” jest zgodne |

Wyniki dotyczą wskazanych prób, nie całych albumów. Częstotliwość PCM v7 wynosi 96 kHz dla zbadanych utworów Misi Furtak i 44,1 kHz dla Nosowskiej. Dla AAC przy brakujących metadanych nadal wynosi 48 kHz. Potwierdzone odczyty użytkownika poprawnie odróżniają jakość stratną i Max. Format końcowego urządzenia nie był w tej próbie mierzony.

**Dodatkowe ustalenie o Lisie Stansfield:** w obu odpowiedziach identyfikator utworu różnił się od zapisanego w bibliotece. `probe_lisa_replacements.py` pobrał metadane zwróconych wersji. Tytuły i wykonawca zgadzają się, ale oba utwory należą do „The Boxset Collection”. To automatyczne zastąpienie po stronie usługi; sama ta próba nie dowodzi, że każda edycja „Real Love” w TIDAL występuje wyłącznie w AAC. Do porównania z oryginalnym odtwarzaczem trzeba uwzględnić tę różnicę edycji. Nie zmieniono kodu ani paczki podczas tego pomiaru.

## Dokończenie brakujących parametrów i kandydat alpha.482, 7.11.2026

### Materiał do dokumentacji użytkownika

Po rozpoczęciu utworu AMC odczytuje jakość rzeczywiście zwróconego źródła. Pasek stanu i NVDA+End podają opis źródła oraz osobno PCM w silniku. Przykład dla „Sara” po poprawce: „Sara, źródło AAC, jakość stratna, 321 kb/s, PCM w silniku 44,1 kHz”. Utwory Max zachowują częstotliwość podaną w manifeście, np. 96 lub 192 kHz. Gdy częstotliwości brakuje, AMC odczytuje ją ze strumienia przed startem. Taka analiza może wydłużyć rozpoczęcie odtwarzania. Współdzielone wyjście Windows nadal może przeliczać dźwięk do ustawień urządzenia. Skróty i dostępne polecenia pozostają zgodne z istniejącym modelem.

### Uzasadnienie i decyzje

Zastępuje ograniczenie v7, w którym brakujące metadane kierowały AAC 44,1 kHz do PCM 48 kHz. Tor chwilowych źródeł TIDAL analizuje częstotliwość istniejącym FFmpeg. Ten sam ograniczony czasowo mechanizm analizy jest używany przy określaniu czasu plików. Wynik diagnostyczny pozostaje w pamięci, a błąd analizy nie ujawnia adresu strumienia. Pozostałe rodzaje źródeł zachowują swój dotychczasowy wybór częstotliwości. Opis „źródło stratna, AAC” poprawiono na „źródło AAC, jakość stratna”. Status PCM pojawia się po potwierdzeniu parametrów przez dekoder.

### Testy i wynik

Test Windows `--transient-flac-output` przeszedł dla FLAC 44,1/96/192 kHz zarówno ze znaną częstotliwością, jak i bez niej, oraz dla AAC 44,1 kHz bez metadanych, w tym seek. Zbudowany osobno host `.test-output/tidal-hires-host-v8-20261107` przeszedł dwa warianty rzeczywistego transportu: 192 kHz z metadanymi i 44,1 kHz bez metadanych. Każdy sprawdza start, seek, pauzę, wznowienie i odbudowę wyjścia.

`verify_sara_host_v8.py` użył dokładnego utworu z historii, adaptera, lokalnego proxy i nowego hosta na jednorazowym stanie z głośnością zero. Poświadczenie skopiowano wyłącznie do pamięci i nie zapisywano zmian. Wynik: start potwierdzony, brak błędu, PCM 44,1 kHz, nowa etykieta AAC, głośność zero. Pierwsza próba diagnostyki błędnie ustawiała głośność w sesji TIDAL przed jej uruchomieniem; poprawiono skrypt, ustawiając głośność lokalnego toru przed startem.

Zestawy Pythona: 69 testów adaptera, GUI, poświadczeń, palety i klienta hosta, 6 testów okna konta oraz 47 testów profili/pakowania — razem 122 zaliczone. Pierwsze zbiorcze wywołanie miało ograniczenia katalogu tymczasowego i konflikt kolejności importu rzeczywistego wx z atrapą. Po uruchomieniu we właściwym środowisku pozostały cztery stare oczekiwania wykluczające TIDAL z Public. Uaktualniono je do zatwierdzonego profilu i ponowiono wszystkie objęte zestawy z wynikiem dodatnim. Źródła, testy i diagnostyka nie są jeszcze instalacją alpha.482.

### Wersja i commit

Planowana aktualizacja Test alpha.482. Przygotowanie instalatora wymaga czystego commita zgodnego z paczką. Zmiany TIDAL są oddzielane od niezakończonego zadania presetów, które pozostaje w bieżącym checkoutcie. Commit i wyniki instalatora, instalacji oraz ewentualnej publikacji należy dopisać po rzeczywistym wykonaniu.
