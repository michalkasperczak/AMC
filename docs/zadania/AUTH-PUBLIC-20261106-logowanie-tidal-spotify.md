# AUTH-PUBLIC-20261106 — logowanie TIDAL i Spotify dla odbiorcy Public

Bieżący punkt wznowienia: odbiór Ctrl+Shift+P w zainstalowanym Public alpha.483 nie przeszedł — brak okna, potem blokada „już trwa”. Potwierdzono NameError zależności okna importowanej tylko dla Spotify. Poprawka alpha.484 przenosi UI do wspólnego modułu i zwalnia blokadę po błędzie. Test świeżego, odfiltrowanego Public i prawdziwy konstruktor wx przeszły. Przygotowywany jest nowy instalator. Nie kasować logowania i nie zamykać Test; instalacja i ponowny odbiór otwarte.

Status na 7.11.2026 po instalacji alpha.482. Instalator z czystego commita `ed9d61acff8c71e517b5e25b5c454012b314397a` zainstalowano po aktualnym potwierdzeniu użytkownika o braku nagrywania i zaplanowanych nagrań. Pełna integralność 4196 plików instalacji przeszła (kod 0). Uruchomiono jedną instancję ze stałego `C:\Users\micha\AppData\Local\Programs\AMC Test\AMC.exe`; tytuł „AMC Test — 07.11.2026, 13:54”. Cicha próba „Sara” wcześniej potwierdziła PCM 44,1 kHz i etykietę „źródło AAC, jakość stratna”. Przeszły testy dekodera, transportu i 122 celowane testy Pythona. Użytkownik potwierdził odczyt NVDA dla „Sara”: AAC, jakość stratna, 321 kb/s, PCM w silniku 44,1 kHz. Odbiór AAC i Max przez użytkownika jest dodatni: „Cała naprzód” Misi Furtak odczytano jako FLAC 24 bit/96 kHz, 2287 kb/s, PCM w silniku 96 kHz. Zewnętrzne logowanie Public i publikacja pozostają otwarte. Nie opublikowano alpha.482.

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

## Gotowy instalator alpha.482, 7.11.2026

Zmiany TIDAL zapisano na bieżącej gałęzi jako `ed9d61acff8c71e517b5e25b5c454012b314397a`. Pozostawiono poza commitem kod i testy presetów oraz ich wpis w rejestrze. Do pakowania utworzono zarządzany checkout tego samego commita `C:\Users\micha\.codex\worktrees\amc-tidal-alpha482\AMC - integracja wxPython`; źródła w nim pozostały czyste. Build powstał z kompletnego runtime Test i osobno przypiętych modułów TIDAL.

Paczka `.test-output/amc-test-alpha482-20261107` obejmuje 4196 plików. Manifest potwierdza `sourceDirty=false`, właściwy commit i datę użytkową 07.11.2026, 13:54. Pełna integralność oraz kontrolowany start hosta i GUI przeszły. Test transportu wykonany ponownie na hostcie finalnej paczki z czystego checkoutu przeszedł 2/2.

`Build-AMCInstaller.ps1 -Profile test -Version 0.1.0-alpha.482` z czystego checkoutu przygotował `D:\Projekty Codex\AMC - integracja wxPython\publish\alpha482-20261107\AMC-Test-2026-11-07-1354.exe` o wielkości 143857991 bajtów. Plik `.exe.integrity.json` potwierdza profil Test, wersję, commit i `secretScan=passed`; SHA-256 instalatora zgadza się z manifestem integralności. Instalator zawiera standardowo opcjonalną paczkę Quick App Switcher 0.1.6. Nie instalowano ani nie restartowano NVDA.

`Publish-AMCRelease.ps1` wykonano w trybie przygotowania, bez `-Publish`. Powstały lokalne `amc-update-test.json`, `release-ready-test-0.1.0-alpha.482.json` i szkic opisu wydania. Instrukcja `AMC-Test-alpha482-instrukcja-szkic.md` i lista zmian `AMC-Test-alpha482-zmiany-szkic.md` znajdują się w tym samym folderze instalatora i wymagają odbioru użytkownika. Nie opublikowano wydania.

Na etapie przygotowania instalacja oczekiwała na aktualne potwierdzenie braku nagrywania; uruchomiona była paczka próbna v7. Ten stan został zastąpiony wynikiem instalacji opisanym poniżej, po odpowiedzi użytkownika „Nadal nic nie nagrywa i nic nie jest zaplanowane”.

Przed Public nadal trzeba dostarczyć produkcyjny Client ID aplikacji AMC i sprawdzić zwykłego użytkownika od świeżego logowania. Obecny Client ID w Test i zapisane sesje nie stanowią takiego testu. To ograniczenie przygotowania publicznego wydania pozostaje jawne w szkicu instrukcji.

## Instalacja alpha.482 i punkt wznowienia, 7.11.2026

### Materiał do dokumentacji użytkownika

Użytkownik korzysta teraz ze stałej instalacji `C:\Users\micha\AppData\Local\Programs\AMC Test\AMC.exe`, z nazwą okna „AMC Test — 07.11.2026, 13:54”. Nie musi uruchamiać paczki z `.test-output`. Do odbioru: odtworzyć „Sara” i nacisnąć NVDA+End. Oczekiwany odczyt zawiera „źródło AAC, jakość stratna” oraz „PCM w silniku 44,1 kHz”. Następnie można sprawdzić znany utwór Max. Wynik silnika nadal nie określa formatu końcowego urządzenia Windows.

### Uzasadnienie i decyzje

Aktualna odpowiedź użytkownika potwierdziła brak nagrywania i zaplanowanych nagrań. Łagodnie zamknięto wyłącznie próbny proces v7 (Python PID 13724), a następnie uruchomiono dokładny zweryfikowany instalator. Zachowano dane i poświadczenia użytkownika. Cicha instalacja nie instalowała dodatku ani nie restartowała NVDA. Instalacja do ręcznego odbioru pozostaje kandydatem; nie uruchamiano publikacji.

### Testy i wynik

Log `C:\Users\micha\Documents\Codex\2026-11-06\t\work\tidal_hires\install-alpha482.log` potwierdza „Installation process succeeded”, brak potrzeby restartu Windows i zakończenie o 14:48:21. Następnie `Start-Process` dokładnego zainstalowanego `AMC.exe` z argumentem `--sprawdz-pakiet-pelny-cicho`, `-WindowStyle Hidden -Wait -PassThru` zakończył kontrolę kodem 0. Manifest: 4196 plików, profil `test`, `sourceDirty=false`, commit `ed9d61acff8c71e517b5e25b5c454012b314397a`, data użytkowa 07.11.2026, 13:54. Wersja produktu `AMC.exe`: `0.1.0-alpha.482`.

Start stabilnego launchera o 14:51:00 potwierdzono przez ścieżki i drzewo procesów: `AMC.exe` PID 39680 → `runtime\python.exe` PID 836 → `host\amc_lite_host.exe` PID 27008, wszystkie z katalogu instalacji AMC Test. Jedno okno: „AMC Test — 07.11.2026, 13:54”. Nie wykryto pozostawionej próbnej instancji. Rzeczywistej mowy NVDA po instalacji jeszcze nie potwierdzono; wysłano prośbę o odczyt „Sara”.

### Znane ograniczenia

Odbiór NVDA i odsłuch użytkownika pozostają otwarte. Użycie zapisanej sesji Test nie sprawdza logowania od zera. Produkcyjny Client ID AMC oraz świeże konto zewnętrzne Public wymagają osobnego sprawdzenia. Spotify nie włączono do Public. Nie publikowano źródeł ani instalatora alpha.482. Zachowano kandydat, szkice dokumentacji i wcześniejsze artefakty do czasu potwierdzenia zastępstwa.

### Wersja i commit

Zbudowany, zainstalowany i uruchomiony AMC Test `0.1.0-alpha.482`, nazwa użytkowa „AMC Test — 07.11.2026, 13:54”, commit źródeł `ed9d61acff8c71e517b5e25b5c454012b314397a`. Późniejszy zapis wyniku instalacji jest zmianą dokumentacji poza tym artefaktem. Stan limitu po tej bramce: 47% wykorzystane, 53% pozostało; reset 11.11.2026 o 07:45 czasu polskiego.

## Odbiór NVDA dla „Sara”, 7.11.2026, 14:54

Użytkownik po uruchomieniu zainstalowanej alpha.482 podał dokładny komunikat: „Sara, źródło AAC, jakość stratna, 321 kb/s, PCM w silniku 44,1 kHz”. Zastępuje to wcześniejszy status oczekiwania na odczyt tego utworu. Odczyt potwierdza rozpoznanie stratnego źródła i zachowanie częstotliwości PCM 44,1 kHz w silniku. Jest zgodny z niezależnym badaniem danych AAC oraz cichą próbą nowego hosta. Sama informacja o PCM nie potwierdza formatu na urządzeniu wyjściowym. Użytkownik nie podał osobnego wyniku odsłuchu ani nowego odczytu utworu Max w tej instalacji.

Nie zmieniono kodu, wersji ani działających procesów. Nie ponawiano testów dla zmiany dokumentacji. Commit źródeł instalacji pozostaje `ed9d61acff8c71e517b5e25b5c454012b314397a`. Publikacja alpha.482 i świeże logowanie zewnętrzne Public nadal nie są wykonane. Następna weryfikacja jakości może użyć znanego utworu Max; po zamknięciu odbioru można dokończyć wydanie i uporządkować zbędne paczki z zachowaniem instalatora, źródeł, dokumentacji i danych użytkownika.
## Odbiór NVDA dla Max — Misia Furtak, 7.11.2026

Użytkownik podał odczyt: „Cała naprzód, źródło Max, FLAC 24 bit, 96 kHz, 2287 kb/s, PCM w silniku 96 kHz” i potwierdził „czyli tak jak miało być”. Jest to dodatni odbiór przypadku Max w zainstalowanej alpha.482, uzupełniający odebrany wcześniej przypadek AAC „Sara”. Zastępuje wcześniejsze oczekiwanie na nowy odczyt Max w tej instalacji. Format źródła i PCM silnika zgadzają się z niezależnym wcześniejszym pomiarem strumienia tego utworu. Format urządzenia Windows nadal nie był mierzony.

Nie zmieniono kodu, wersji ani procesów; testów nie ponawiano dla zapisu dokumentacji. Commit instalacji pozostaje `ed9d61acff8c71e517b5e25b5c454012b314397a`. Odbiór jakości AAC i Max jest zamknięty dodatnio. Całe zadanie AUTH-PUBLIC pozostaje w trakcie: świeże logowanie zewnętrznego użytkownika i produkcyjna konfiguracja Client ID Public nadal wymagają sprawdzenia. Publikacja alpha.482 pozostaje osobną, niewykonaną czynnością.

## Przygotowanie nowego logowania po nagraniu, 7.11.2026

### Materiał do dokumentacji użytkownika

Użytkownik po potwierdzeniu jakości AAC i Max polecił zabezpieczyć stary profil i przygotować aktualizację do próby nowego logowania na własnym koncie Google. Jednocześnie podał, że teraz nagrywa i aktualizację wykona po 16:00. To aktualna informacja zastępująca wcześniejsze potwierdzenia braku nagrań; nie wolno użyć tych starszych odpowiedzi do restartu albo instalacji. Samo osiągnięcie 16:00 nie potwierdza zakończenia nagrania.

Przygotowano osobny kandydat Public „AMC Public — 07.11.2026, 15:27” z tym samym sprawdzonym silnikiem alpha.482. Zadano opcjonalne pytanie, czy użytkownik zamierza testować osobny Public czy wyczyścić Test; do przygotowania przyjęto podaną użytkownikowi wersję Public. Test pozostaje uruchomiony bez zmian. Dokładna instrukcja `START-TUTAJ-po-16.txt` i szkic zmian `ZMIANY-Public-szkic.txt` są w `publish/public-login-alpha482-20261107`.

Próba po zakończeniu nagrania: zainstalować Public, odświeżyć kopię przed resetem, zamknąć dokładnie jego instancję i wyczyścić wyłącznie katalogowe i odtwarzaczowe logowanie TIDAL Public oraz zapamiętaną kolekcję. Zachować Client ID i pozostałe ustawienia. Następnie Ctrl+Shift+S → TIDAL, Ctrl+F5 → logowanie, Google na stronie dostawcy, powrót do AMC i Ctrl+L → Biblioteka. Otworzyć album i Enter na utworze; jeśli będzie wymagany kod odtwarzacza, zatwierdzić go osobno na tym samym koncie. Sprawdzić pełny utwór, transport, NVDA+End i ponowne uruchomienie Public. Nie kasować sesji Google w przeglądarce ani nie odwoływać globalnie autoryzacji konta.

### Uzasadnienie i decyzje

Nie ma potrzeby ryzykować utraty działającego Test ani usuwać całego profilu z innymi funkcjami. Zabezpieczono pełne ustawienia i bazy nieaktywnego Public oraz dokładnie cztery wskazane wpisy Menedżera poświadczeń TIDAL Test/Public. Dodatkowo zapisano wstępną migawkę samego state.json Test; jego aktywnych baz nie kopiowano. Kopia jest zaszyfrowana Windows DPAPI dla bieżącego użytkownika i leży poza repozytorium oraz folderem publikacji:

`D:\Projekty Codex\AMC - kopie prywatne\TIDAL-20261107\profil-przed-logowaniem-od-zera.dpapi`

Rozmiar kopii 46534310 bajtów, 9 pozycji. Sprawdzono odszyfrowanie, zgodność bajtów przed/po szyfrowaniu oraz SHA-256 każdej zapisanej pozycji. Przy kopii zapisano narzędzie i sumę zaszyfrowanego pliku. Nie ujawniano tokenów, identyfikatorów kont ani podpisanych adresów. Nie wykonywano wylogowania, kasowania poświadczeń, resetu, instalacji ani restartu.

### Testy i wynik

Migawki baz Public utworzono przez SQLite backup z połączenia read-only do bazy w pamięci; obejmują zatwierdzone dane WAL. Pierwsza kontrola quick_check wymagała kolacji AMC_PL z hosta. Nie zastępowano jej inną kolejnością sortowania: usunięto tę nieodpowiednią dla narzędzia bramkę i potwierdzono odczyt stron, wykonanie kopii oraz sumy danych. Nie jest to test poprawności indeksów aplikacji. Późniejsze odczytanie zaszyfrowanego pliku i ponowna weryfikacja wszystkich pozycji przeszły.

Public złożono przez `prepare_public_alpha482.py`, używając projektowego `build_bundle.py --profile public` w czystym zarządzanym checkoutcie commita `ed9d61ac`. Każdy ponownie użyty plik hosta, launcher i aktualizator sprawdzono według sum finalnej paczki Test. Host Spotify Librespot i jego osobne licencje pominięto w wejściu Public. Użyto istniejącego runtime Public i przypiętych zależności TIDAL. Nie uruchamiano ponownej kompilacji ani szerokiej regresji. Powstały 3143 pliki manifestu; `sourceDirty=false`, właściwy commit, profil Public, TIDAL z pełnym odtwarzaczem, bez sesji Spotify. Pakowanie i kompresja miały obniżony priorytet. Pełna kontrola pakietu i kontrolowany start GUI/hosta z izolowanym stanem i zablokowanymi poświadczeniami przeszły w bramce Build-AMCInstaller.

### Znane ograniczenia i punkt wznowienia

Przed instalacją lub zmianą logowania uzyskać aktualne potwierdzenie zakończenia nagrania i braku bliskiego harmonogramu. Odświeżyć kopię bezpośrednio przed resetem; obecna jest migawką wstępną. Przywracanie po ewentualnej nieudanej próbie ma objąć tylko sekcję TIDAL Public i jego dwa wpisy logowania, bez nadpisywania późniejszych ustawień i danych Test.

Obecny Public ma już skonfigurowany Client ID, ale jego prawo do obsługi innych osób nie jest potwierdzone. Próba na własnym koncie przez Google zbada nowe zatwierdzenie logowania i bibliotekę. Nie dowodzi logowania z całkowicie pustej instalacji ani dostępu produkcyjnego na koncie innej osoby. Wspólny Client ID AMC pozostaje osobnym warunkiem przygotowania dystrybucji. Nie dodano identyfikatora z profilu użytkownika do instalatora. Spotify pozostaje wyłączony w Public. Pełny tor TIDAL jest nieoficjalny. Nie publikowano kandydata ani kopii profilu. Porządki starych paczek odłożono do odbioru zastępstwa i zakończenia nagrywania.

### Wersja i commit

Public `0.1.0-alpha.482`, data użytkowa 07.11.2026, 15:27, źródła `ed9d61acff8c71e517b5e25b5c454012b314397a`. Zmiany presetów pozostają poza tym kandydatem. Instalacja Test ma datę użytkową 13:54 i nadal działa z tym samym drzewem procesów (launcher 39680, Python 836, host 27008). Wynik końcowego pakowania instalatora dopisano osobno po jego zakończeniu.

### Gotowy instalator i stan końcowy przygotowania

`Build-AMCInstaller.ps1 -Profile public -BundleRoot "D:\Projekty Codex\AMC - integracja wxPython\.test-output\amc-public-alpha482-login-20261107" -Version 0.1.0-alpha.482 -OutputDirectory "D:\Projekty Codex\AMC - integracja wxPython\publish\public-login-alpha482-20261107"` zakończył się powodzeniem. Instalator `AMC-Public-2026-11-07-1527.exe`: 133855367 bajtów. Obliczony SHA-256 zgadza się z plikiem `.exe.integrity.json`, który potwierdza profil Public, właściwy commit, wersję i `secretScan=passed`. Nie przygotowano publicznego kanału aktualizacji ani publikacji. Kandydat jest gotowy do lokalnej instalacji po zakończeniu nagrania.

Końcowa kontrola procesów potwierdziła niezmienioną instancję Test (39680 → 836 → 27008) ze stałej instalacji i brak pozostawionych procesów nowej paczki Public. Nie wykonywano instalacji, resetu profilu, usunięcia poświadczeń ani otwarcia przeglądarki. Próba logowania przez Google i ponowne pobranie Biblioteki pozostają do wykonania z użytkownikiem. Użytkownik w odpowiedzi dopuścił próbę bezpośrednio w Public, chociaż początkowo zakładał nową próbę w Test; poprosił o wyjaśnienie pełnego zakresu kandydata, w tym Cast i AirPlay. Przygotowanie Public nie zmieniło żadnego z profili. Limit po przygotowaniu: 47% wykorzystane, 53% pozostało, reset 11.11.2026 o 07:45 Europe/Warsaw.
## Wyjaśnienie zakresu kandydata Public, 7.11.2026

Użytkownik zapytał, czy paczka zawiera wszystkie udoskonalenia Test, w tym Google Cast i AirPlay. Dopuścił bezpośrednią próbę TIDAL w Public zamiast pierwotnie rozważanego ponownego logowania w Test. Nie polecił włączenia do Public wszystkich usług Test ani instalacji w czasie nagrania.

Sprawdzono rzeczywisty manifest, model możliwości i wykonanie poleceń. Public ma Pliki, Radio, Podcasty i YouTube bez konta, TIDAL z pełnym odtwarzaniem, WiiM i Frontier Smart, nagrywanie i harmonogramy, zakładki, rozdziały, pomoc i paletę. Zawiera kod Google Cast, AirPlay i pyatv. Polecenia Cast oraz drugiego wyjścia są dostępne dla Plików, Radia i Podcastów; nie obiecywać przesyłania TIDAL przez te polecenia. W tej paczce nie powtarzano fizycznego testu odbiorników Cast/AirPlay.

To wspólna baza źródeł Test alpha.482 z ograniczeniami profilu Public, nie pełny profil Test. Spotify, Sonos, Radioarchiwista i logowanie do konta YouTube pozostają wyłączone. Porównano treść 86 pakowanych modułów Pythona z bieżącym checkoutem, normalizując końce wierszy po wykryciu różnic CRLF/LF. Jedyna różnica treści to profile_presets.py: osobna, jeszcze niezatwierdzona poprawka krótszych etykiet miejsc presetów nie jest w kandydacie ani w instalacji Test alpha.482. Pozostałe pakowane moduły mają bieżącą treść. Nie zmieniano kodu ani nie wykonywano nowego buildu.

Materiał użytkowy dopisano do instrukcji przy instalatorze. Kandydat pozostaje lokalny, nieinstalowany i nieopublikowany; nagranie w Test nie zostało przerwane. Kolejny etap po aktualnym potwierdzeniu zakończenia nagrania: zainstalować Public i przejść nowe logowanie Google, bibliotekę oraz pełne odtwarzanie. Zakres szerszego wydania Public i oddzielna poprawka presetów nie są automatycznie uznane za odebrane przez tę próbę.
## Decyzja użytkownika o Spotify, 7.11.2026

Użytkownik przypomniał dopracowany w Test wariant logowania i działający u niego tor Spotify (nazwa przekazana niejednoznacznie jako „soloist albo ten drugi”). Decyzję o przeniesieniu Spotify do Public wyraźnie odłożył na później. Brak Spotify w przygotowanym kandydacie Public jest ograniczeniem profilu, nie stwierdzeniem, że działający tor Test został uznany za niesprawny. Nie rozstrzygano teraz, który backend użytkownik miał na myśli, ani nie przypisywano jego odbioru do konkretnej implementacji bez sprawdzenia. Nie rozpoczynano nowego audytu, buildu ani włączania Spotify. Bieżąca próba Public nadal dotyczy TIDAL po zakończeniu nagrania. To ustalenie zastępuje ewentualną interpretację wyjaśnienia zakresu paczki jako decyzji o trwałym wykluczeniu Spotify z Public.
## Zainstalowany Public i przygotowane logowanie od zera, 7.11.2026, 16:23

### Materiał do dokumentacji użytkownika

Po ponownej dyskusji Test/Public użytkownik wyraźnie polecił „Dobrze to instaluj. OK”. Zainstalowano i uruchomiono dokładny `C:\Users\micha\AppData\Local\Programs\AMC Public\AMC.exe`. Okno ma tytuł „AMC Public — 07.11.2026, 15:27”. Użytkownik wybiera to okno przez Alt+Tab, następnie Ctrl+Shift+S → TIDAL i Ctrl+F5 → „Zaloguj w przeglądarce”. Hasło i Google pozostają na stronie dostawcy. Po sukcesie Escape i Ctrl+L otwierają Bibliotekę. Pełny odtwarzacz ma również wyczyszczone lokalne poświadczenie i przy pierwszym utworze może wymagać osobnego kodu. Nie otwierano przeglądarki za użytkownika.

### Uzasadnienie i decyzje

Nowa zgoda po 16:00 zastępuje wcześniejsze odroczenie instalacji Public. Public przed instalacją nie był uruchomiony, zatem nie zamykano żadnej aplikacji. Działający Test pozostawiono bez zmian, niezależnie od jego aktualnej aktywności. Zweryfikowany instalator ma osobny identyfikator produktu, katalog, mutex i wpisy rejestru; nie używa Restart Managera do zamykania aplikacji.

Bezpośrednio przed instalacją wykonano ponownie kopię, 46534294 bajty, 9 pozycji, z potwierdzonym odszyfrowaniem i sumami:
`D:\Projekty Codex\AMC - kopie prywatne\TIDAL-20261107\profil-bezposrednio-przed-instalacja.dpapi`
Poprzednia kopia została zachowana. Nie zapisano jawnych tokenów ani danych kont w logach lub dokumentacji.

### Testy i rzeczywisty wynik

Pierwsze wywołanie Start-Process miało błędne cytowanie argumentu /DIR ze spacją i utworzyło świeży katalog `C:\Users\micha\AppData\Local\Programs\AMC`. Błąd wykryto w logu przed zwykłym uruchomieniem. Po zakończeniu instalacji sprawdzono manifest, profil, commit i brak aktywnego procesu z tego katalogu. Standardowy deinstalator tej konkretnej nowej kopii zakończył się kodem 0. Następnie ten sam instalator wywołano z poprawnym argumentem `/DIR="C:\Users\micha\AppData\Local\Programs\AMC Public"`. Log potwierdził właściwy katalog, powodzenie instalacji i brak restartu Windows. Końcowy kod 0. Nie ma już AMC.exe w omyłkowym katalogu. Dane profilu i instalacja Test nie były celem korekty.

Log poprawnej instalacji: `C:\Users\micha\Documents\Codex\2026-11-06\t\work\tidal_public\install-public-alpha482-corrected.log`. Pełna kontrola dokładnego zainstalowanego AMC.exe z `--sprawdz-pakiet-pelny-cicho` zakończyła się kodem 0. Manifest: Public, 3143 pliki, czyste źródła, commit `ed9d61acff8c71e517b5e25b5c454012b314397a`; wersja produktu 0.1.0-alpha.482.

`reset_public_tidal.py` najpierw przeszedł tryb bez zapisu. Sprawdził odszyfrowaną kopię, zgodność bieżącego state.json i dwóch wpisów logowania z kopią oraz brak mutexa działającego Public. Pierwszy start narzędzia wymagał jawnego dodania jego katalogu do sys.path z powodu izolowanego runtime; przed tą korektą nie zmieniono profilu. W trybie --apply zapis atomowy wyczyścił tylko nazwę konta, ostatnią playlistę, czas synchronizacji i kolekcję w sekcji TIDAL oraz usunął dokładnie dwa lokalne wpisy TIDAL Public. Zachowano Client ID i całą resztę JSON. Narzędzie ma cofnięcie zmian na błąd, którego nie trzeba było używać. Wynik: reset=True, tidalCredentialsAbsent=True, clientIdPreserved=True, otherSettingsPreserved=True, testTidalCredentialsUnchanged=True. Nie odwoływano uprawnień po stronie serwisu ani sesji Google.

O 16:23 uruchomiono stabilny launcher Public PID 29012 → runtime\python.exe PID 47640 → host\amc_lite_host.exe PID 35204. Wszystkie ścieżki należą do właściwej instalacji AMC Public. Potwierdzono jedno okno Public o oczekiwanej nazwie. Istniejący Test pozostał z PID 39680 → 836 → 27008 i tytułem „AMC Test — 07.11.2026, 13:54”.

### Znane ograniczenia

Faktyczne nowe logowanie przez Google, pobranie Biblioteki, osobny kod pełnego odtwarzacza, odsłuch i ponowne uruchomienie Public pozostają do odbioru użytkownika. Test zachował swoje poświadczenia, więc nowe logowanie w Public nie jest migracją sesji między produktami. To próba własnego konta i już skonfigurowanego Client ID; dostęp produkcyjny dla innej osoby nadal nie jest dowiedziony. Spotify i pozostałe różnice profilu bez zmian. Instalacja lokalna nie oznacza publikacji; wydania i kanału aktualizacji Public nie opublikowano.

### Wersja i commit

Zainstalowany i uruchomiony Public 0.1.0-alpha.482, nazwa użytkowa „07.11.2026, 15:27”, źródła ed9d61acff8c71e517b5e25b5c454012b314397a. Aktualizacje dokumentacji i narzędzia jednorazowe nie zmieniają zawartości tego artefaktu. Limit po tej bramce: 48% wykorzystane, 52% pozostało; reset 11.11.2026 o 07:45 Europe/Warsaw.
## Odbiór świeżego logowania i uwagi użytkownika, 7.11.2026

Użytkownik potwierdził nowe logowanie katalogu przez Google, osobny kod odtwarzacza i pełne odtwarzanie w zainstalowanym Public. Zastępuje to oczekiwanie na wynik świeżego logowania na jego własnym koncie. Nie potwierdza konta innej osoby ani uprawnień produkcyjnych wspólnego Client ID.

Do poprawienia w ramach bieżącego zadania: jawny przycisk otwierania zwykłej przeglądarki z kodem przy zachowaniu kopiowania; opis dwóch osobnych zatwierdzeń; sporadyczne odrzucenie Enter po Escape jako „zajęty”; angielski interfejs przy języku systemowym; okno Ctrl+Shift+P z nazwami playlist bez przedrostka „Playlista TIDAL”, literami, Enter i pamięcią potwierdzonego wyboru. Użytkownik zgłosił brak łącznego czasu My Mix jako drobne ograniczenie do wyjaśnienia.

Odczyt CUA potwierdził angielskie menu i nagłówki w Public oraz bieżący utwór Max FLAC 24/192 z PCM 192 kHz. Próbę otwarcia Ctrl+Shift+P obserwowano jako „Pobieram playlisty TIDAL”; nie uzyskano czytelnego drzewa właściwego okna, nie zatwierdzano i nie zmieniano playlist na koncie. Nie jest to zaliczenie ręcznego testu tego okna. Skupiono się na kodzie i izolowanych testach.

Izolowany proces z tym samym runtime wykazał locale.getlocale()[0] = Polish_Poland przed wx.App oraz None po utworzeniu wx.App; Windows UI LCID = 1045. To reprodukcja przyczyny języka, a nie zmiana ustawień użytkownika. Trwa poprawka wyboru języka na podstawie języka interfejsu Windows.
## Poprawki interfejsu po odbiorze Public, kandydat alpha.483, 7.11.2026

### Materiał do dokumentacji użytkownika

Pełna instrukcja i szkic changelogu: `docs/public/TIDAL_LOGOWANIE_PLAYLISTY_2026-11-07_SZKIC.md` oraz `docs/public/TIDAL_ALPHA483_ZMIANY_SZKIC.md`. Pierwsze logowanie w bieżącej architekturze ma osobne upoważnienia katalogu i pełnego odtwarzacza. Nowy przycisk „Otwórz przeglądarkę z kodem” jest domyślny; zachowano „Kopiuj adres”, „Kopiuj kod” i anulowanie. Okno jest modeless, główny AMC pozostaje dostępny, a automatyczne zamknięcie po sukcesie nie przywraca fokusu po wcześniejszym przejściu do przeglądarki. Błąd uruchomienia pozostawia adres do ręcznego skopiowania.

Ctrl+Shift+P: TIDAL ma nazwy bez poprzedzającego „Playlista TIDAL”, co umożliwia natywne przechodzenie literami po nazwach. Enter na liście dodaje do wskazanego celu; po świadomym użyciu Spacji zatwierdza zestaw zaznaczeń. Bez ręcznego zaznaczania Enter wybiera bieżący wiersz zamiast dawnych zapamiętanych zaznaczeń. Zapis potwierdzony przez host nadal zapamiętuje cele, a ponowne otwarcie wybiera pierwszy potwierdzony cel. Anulowanie niczego nie zapamiętuje. Wspólne okno Spotify ma ten sam kontrakt. Dostosowano centralne opisy poleceń i instrukcje okna oraz szablon języków.

Enter po Escape na tym samym grającym utworze otwiera odtwarzacz bez ponownego startu. W trakcie przygotowania identyczne żądanie jest rozpoznawane i ogłaszane jako trwające. Polski język systemowy jest ustalany według Windows UI, niezależnie od resetu locale przez wx.App. Jawny wybór angielskiego jest zachowany.

### Uzasadnienie i decyzje

Powtarzany przedrostek wierszy blokował wyszukiwanie literą nazwy. Natywny domyślny przycisk nie zapewniał jawnej obsługi Enter na liście, a brak potwierdzonego zapisu nie mógł zapamiętać celu. Naprawiono zachowanie w warstwie dialogu, bez obchodzenia kontroli uprawnień serwisu. Powtórne odtwarzanie tego samego strumienia było odrzucane przez adapter; GUI rozpoznaje istniejący strumień i przygotowanie zamiast tworzyć duplikat. Nie zmieniano formatu audio ani logowania kont użytkownika.

### Testy i wynik

66 celowanych testów obejmujących nowy zestaw, TIDAL/Spotify playlisty, GUI natywnego TIDAL i adapter przeszło. Początkowe próby wymagały użycia istniejącego interpretera z pytest (Programs\Python\Python314, nie pythoncore) i uzupełnienia atrap o MOD_NONE oraz GetLabel. Były to braki środowiska/atrap, nie zmiany produkcyjnego wx.

Pełny test i18n ujawnił znane braki katalogu angielskiego i nieaktualny szablon. Szablon odtworzono projektowym export_translation_template.py, obejmując nowe klucze i wcześniejsze teksty TIDAL. Bramka języka i nowych zachowań: 32 zaliczone, 1 pominięty test wymagający prawdziwego wx w procesie z atrapą, 2 jawnie wyłączone wcześniejsze testy kompletności pełnego katalogu angielskiego. Te dwa braki pozostają ograniczeniem całego katalogu; nie podawać, że pełny i18n jest zielony. Osobny proces z prawdziwym runtime Public i wx.App potwierdził locale=None oraz nowy wynik wybranego języka pl.

Dodatkowa próba prawdziwego okna z trzema fikcyjnymi playlistami (bez hosta i konta) pokazała w CUA celowe etykiety „Poranek, niezaznaczona”, „Ulubione, niezaznaczona”, „Wieczór, zaznaczona”. Narzędzie następnie zwracało niedostępny stan/elementy i obrazy spod okna; klawiatura oraz NVDA nie zostały odebrane. Zakończono wyłącznie dokładny proces tej jednorazowej próby PID 39320. Public i Test pozostały uruchomione. Następnie skrócono etykietę samej listy i zawinięto instrukcje, aby długi tekst nie stawał się jej automatyczną nazwą ani nie rozszerzał okna.

### Znane ograniczenia

Rzeczywisty nowy przycisk w przeglądarce, klawisz litery, Enter/Spacja, zapamiętanie celu, anulowanie i odczyt NVDA po aktualizacji wymagają odbioru użytkownika. Nie kasować ponownie działających sesji tylko dla testu playlist lub języka. Nowy przycisk można odebrać przy następnym rzeczywistym wymaganiu kodu.

Kod katalogu wyświetla dodatni duration; brak lub nieobsługiwany format pozostawia czas nieznany. W bieżącej próbie nie pobrano odpowiedzi konkretnych My Mix użytkownika (trwały cachedCollectionItems Public jest pusty). Nie uznawać przypuszczenia o brakujących danych za dowiedzioną przyczynę tych miksów. Nie dodano sumowania częściowo pobranej playlisty ani fałszywego zera.

### Wersja i commit

Kandydat alpha.483. Przed instalacją należy przygotować i sprawdzić paczkę oraz ustalić aktualny stan nagrywania w zamykanym Public. Test nie wymaga zamykania. Źródła tej poprawki będą zapisane osobno od niezakończonej poprawki presetów. Wyniki builda, instalatora, instalacji i publikacji dopisać po wykonaniu. Limit przed budową: 49% wykorzystane, 51% pozostało, reset 11.11.2026 o 07:45 Europe/Warsaw.
## Gotowa paczka interfejsu alpha.483, 7.11.2026

Commit `94e71bb126a45a03666cec527a5997f254082668` zapisano na bieżącej gałęzi bez niezakończonych zmian presetów i ich testów. Do budowy ponownie użyto zarządzanego checkoutu w `C:\Users\micha\.codex\worktrees\amc-tidal-alpha482\AMC - integracja wxPython`, ustawionego na ten nowy commit; nazwa katalogu nie wyznacza wersji. Build-AMCBundle.ps1 z profilem public, istniejącym runtime Public i osobno przypiętymi zależnościami TIDAL przygotował `.test-output/amc-public-alpha483-20261107`. Manifest: 3143 pliki, sourceDirty=false, właściwy commit, data użytkowa 07.11.2026, 17:01. Launcher ma wersję produktu 0.1.0-alpha.483. Pełna integralność i kontrolowany start GUI/hosta na izolowanym stanie z zablokowanymi poświadczeniami przeszły.

Build-AMCInstaller.ps1 przygotował dokładny plik:
`D:\Projekty Codex\AMC - integracja wxPython\publish\public-tidal-polish-alpha483-20261107\AMC-Public-2026-11-07-1701.exe`
Rozmiar 133872509 bajtów. Manifest `.exe.integrity.json` potwierdza profil, wersję, commit i secretScan=passed. Obliczony SHA-256 zgadza się z manifestem. Instrukcja i szkic zmian znajdują się obok instalatora; zapisano lokalne GOTOWOSC-DO-ODBIORU.json bez publikowania kanału aktualizacji. Nie instalowano ani nie uruchamiano tej paczki na rzeczywistych danych użytkownika.

Przed podmianą wysłano pytanie o aktualne nagrywanie i bliski harmonogram tylko w Public. Do końca przygotowania odpowiedź nie nadeszła. Instalacja pozostaje odłożona; nie wolno traktować wcześniejszych odpowiedzi z czasu alpha.482 jako aktualnego stanu. Po potwierdzeniu bezpieczeństwa wykonać świeżą zaszyfrowaną kopię obecnych danych i poświadczeń (po udanym logowaniu), zamknąć łagodnie tylko Public, uruchomić zweryfikowany instalator z właściwie cytowaną ścieżką AMC Public, sprawdzić integralność i uruchomić stabilny launcher. Zachować oba zaakceptowane logowania TIDAL; nie resetować ich ponownie. Test pozostaje uruchomiony.

Ręczny odbiór po instalacji: polski interfejs przy języku systemowym, Ctrl+Shift+P bez przedrostków wierszy, U → Ulubione, Enter bez dodatkowej Spacji, kilka celów przez Spację, ponowne otwarcie na potwierdzonym celu, Escape bez zmiany zapamiętanego wyboru i powrót fokusu. Nowy przycisk przeglądarki można sprawdzić przy następnym wymaganym kodzie; nie wymuszać wylogowania dla samego testu. My Mix i dostęp produkcyjny innej osoby pozostają jawnie niepotwierdzone. Stare paczki zachowano do odbioru zastępstwa, bez usuwania kopii profili.
## Instalacja poprawki Public alpha.483, 7.11.2026, 19:12–19:13

### Materiał do dokumentacji użytkownika

Po aktualnej odpowiedzi użytkownika „Tak możesz aktualizować wersję publiczną…” zaktualizowano wyłącznie Public. Uruchomiony program: `C:\Users\micha\AppData\Local\Programs\AMC Public\AMC.exe`, okno „AMC Public — 07.11.2026, 17:01”. CUA potwierdziło polskie menu „Pliki”, „Biblioteka”, „Odtwarzanie”, „Ustawienia”, „Pomoc” oraz nagłówki listy. TIDAL ma zachowane oba połączenia, więc nie należy ponownie przechodzić logowania tylko w celu sprawdzenia playlist lub języka. Test ma nadal okno „07.11.2026, 13:54” i nie był zamykany.

Do odbioru użytkownika: w Public wybrać utwór, Ctrl+Shift+P, przejść U do „Ulubione”, sprawdzić nazwy bez przedrostka, Enter bez wcześniejszej Spacji, kilka celów przez Spację i ponowne otwarcie na potwierdzonym celu. Escape ma przywrócić poprzedni widok i fokus. Nowy przycisk przeglądarki pozostaje do sprawdzenia przy rzeczywistym wymaganiu kodu, bez wymuszania wylogowania.

### Uzasadnienie i ochrona nagrań

Nie zamieniono odpowiedzi użytkownika na nieudzielone dosłowne oświadczenie o braku nagrań. Sprawdzono bieżące procesy, ochronę normalnego zamknięcia i harmonogram zapisany w Public. Istniejący RadioScheduleCalculator z zainstalowanej biblioteki Core obliczył 0 niesupresowanych bieżących okien planu, 0 startów w ciągu 5 minut i najbliższy przyszły start 7.11 o 23:00 Europe/Warsaw. Kalkulator nie uruchamia ani nie zapisuje nagrań.

Przez CUA wysłano Alt+F4 wyłącznie do starego Public (okno 5837368). _on_close aplikacji pobiera bieżący stan od właściciela nagrywania i odmawia zamknięcia przy aktywnych nagraniach lub braku odpowiedzi. Public zakończył się normalnie; nie wymuszano zakończenia procesu. Potwierdzono zniknięcie okna i brak procesów instalacji Public przed podmianą. Test pozostał uruchomiony.

### Kopia, instalacja i wynik weryfikacji

Po zamknięciu wykonano nową spójną kopię bieżącego profilu oraz poświadczeń po odebranym logowaniu:
`D:\Projekty Codex\AMC - kopie prywatne\TIDAL-20261107\profil-po-logowaniu-przed-alpha483.dpapi`
Kopia DPAPI ma 46515510 bajtów i 9 pozycji; odszyfrowanie i sumy pozycji przeszły. Testowi skopiowano tylko wstępną migawkę state.json i wskazane wpisy TIDAL, bez jego aktywnych baz. Nie ujawniano tokenów ani nazw kont.

Dokładny instalator alpha.483 z folderu `publish/public-tidal-polish-alpha483-20261107` uruchomiono z /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- oraz poprawnie cytowanym /DIR="C:\Users\micha\AppData\Local\Programs\AMC Public". Wynik kod 0. Log `C:\Users\micha\Documents\Codex\2026-11-06\t\work\tidal_polish\install-alpha483.log` potwierdza sukces o 19:11:45 i zakończenie o 19:11:59, bez restartu Windows.

Pełna kontrola dokładnego zainstalowanego launchera --sprawdz-pakiet-pelny-cicho zakończyła się kodem 0. Manifest: profil public, 3143 pliki, sourceDirty=false, sourceRevision=94e71bb126a45a03666cec527a5997f254082668, data 07.11.2026, 17:01; wersja produktu 0.1.0-alpha.483.

Przed zwykłym startem verify_preserved_login.py porównał aktualny profil z zaszyfrowaną kopią: publicTidalCredentialsUnchanged=True, publicTidalConnectionsPresent=2, publicStateUnchanged=True. Nie wykonywano resetu, migracji ani odwołania sesji TIDAL.

O 19:12:19 uruchomiono stabilny Public: AMC.exe PID 28528 → runtime\python.exe PID 41476 → host\amc_lite_host.exe PID 23300, wszystkie ścieżki z właściwej instalacji. Jedno okno nowego Public (39126818). Test zachował PID 39680 → 836 → 27008 i czas startu 14:51. Nie pozostała stara instancja Public. Po starcie CUA potwierdziło poprawiony polski interfejs. Rzeczywisty odczyt playlist przez NVDA i nowy przycisk przeglądarki nie są jeszcze odebrane.

### Dodatkowe rzeczywiste ustalenie, poza zmianą TIDAL

Przed zamknięciem Public jego komunikat zawierał błąd zapisu gotowego nagrania „Tarnobrzeg” i adres zachowanego pliku:
`C:\Users\micha\AppData\Local\AccessibleMediaController\recording-staging\0cc3141ec8f540dcbafbcb274f35c0cf.mp3.amc-partial`
Odczyt filesystem potwierdził istnienie pliku, 12856367 bajtów, ostatni zapis 7.11 o 18:28. Nie odtwarzano go, nie przenoszono ani nie usuwano i nie oceniono kompletności nagrania. Nie ustalono przyczyny odmowy zapisu ani nie zmieniano harmonogramów i folderów. To osobny rzeczywisty problem do ewentualnego podjęcia; instalacja programu nie obejmuje tego katalogu. Nie mylić tego historycznego komunikatu z trwającym nagrywaniem lub skutkiem aktualizacji.

### Znane ograniczenia, wersja i commit

Zainstalowany, uruchomiony i sprawdzony technicznie Public 0.1.0-alpha.483, źródła 94e71bb126a45a03666cec527a5997f254082668. Odbiór NVDA opisanych zmian pozostaje otwarty, podobnie My Mix i produkcyjny dostęp innej osoby. Nie publikowano źródeł ani wydania Public. Nie usuwano kopii danych, pliku nagrania ani jedynego poprzedniego instalatora. Porządki zbędnych paczek pozostają po odbiorze zastępstwa. Limit po tej bramce: 50% wykorzystane, 50% pozostało, reset 11.11.2026 o 07:45 Europe/Warsaw.
## Korekta po nieudanym odbiorze playlist alpha.483, 7.11.2026

### Materiał do dokumentacji użytkownika

Użytkownik zgłosił: po Ctrl+Shift+P na utworze nie było okna ani zrozumiałego wyniku, a drugi skrót podał „Zmiana playlist TIDAL już trwa”. Zastępuje to oczekiwanie, że alpha.483 jest odebraną poprawką tego okna. Pełne audio, zachowane logowanie i polski interfejs pozostają osobnymi sprawdzonymi wynikami. Szkic alpha.483 oznaczono jako nieodebrany w zakresie playlist; nowa korekta jest w szkicu alpha.484 i aktualnej instrukcji.

Po poprawce nieudane otwarcie ma zwolnić blokadę i przywrócić fokus. Użytkownik usłyszy „Nie udało się otworzyć okna playlist TIDAL. Spróbuj ponownie.” Ponowne wywołanie może użyć już pobranej listy, bez restartu. Zwykłe Ctrl+Shift+P ma otworzyć dotychczasowe wspólne okno z obsługą Enter, Spacji, liter i potwierdzonego wyboru. Wiersz nowej playlisty TIDAL również zaczyna się od nazwy.

### Uzasadnienie i przyczyna

Odczyt launcher.log Public wykazał NameError: SpotifyPlaylistChoice przy tworzeniu choices w callbacku loaded. GUI importowało wspólne klasy pod warunkiem session.spotify, a moduł spotify_playlist_dialog.py był celowo wykluczony z pakietu Public. TIDAL używał tych klas mimo wyłączenia Spotify. Ślad zapisany przy zamknięciu starego Public o 19:09 dotyczy wcześniejszej wersji; warunek importu pozostał również w alpha.483.

Poprzednie testy ładowały GUI jako Test, a późniejsza zmiana zmiennej profilu nie usuwała wcześniej zaimportowanych nazw. Atrapy runnera wywoływały callback synchronicznie i mogły kierować wyjątek UI do on_error, podczas gdy prawdziwy BackgroundRunner dostarcza on_success osobno w wątku GUI. To luka dotychczasowej weryfikacji — 66 zielonych testów nie dowodziło działającego okna w faktycznie odfiltrowanym Public.

Przeniesiono implementację UI do service_playlist_dialog.py, dołączanego niezależnie od usług. GUI importuje wspólne klasy bez warunku Spotify. Stary moduł Spotify pozostaje cienkim eksportem zgodności, nadal wykluczonym z Public, tak jak spotify_source.py. Nie włączono Spotify w Public. Lokalna osłona wywołania okna czyści _playlist_membership_pending, przywraca fokus i podaje bezpieczny komunikat po błędzie konstrukcji/otwarcia. Do prywatnego dziennika trafia tylko typ błędu, nie surowe dane usługi. Nie zmieniano kont, poświadczeń ani faktycznych playlist podczas diagnostyki.

### Testy i wynik

Nowy test tworzy copy_python_code(profile=public), sprawdza fizyczną nieobecność modułów Spotify i uruchamia nowy proces z tym pakietem jako pierwszym na ścieżce. W tym procesie profil jest Public już przed importem GUI. Rzeczywisty przepływ adaptera z atrapą providera przechodzi pierwsze otwarcie, tworzenie, dodanie, pamięć wyboru i ponowne otwarcie. Osobny test prawdziwego BackgroundRunner z kolejką dostarczania odtwarza wyjątek konstruktora, potwierdza zwolnienie blokady i udaną anulowaną ponowną próbę z cache (jedno pobranie).

Zestaw 89 testów: początkowo 88 zaliczonych i jeden stary test layoutu z założeniem „GUI Public nie ma SpotifyPlaylistDialog”. To założenie wykluczało właśnie współdzielone UI potrzebne TIDAL. Zastąpiono je sprawdzeniem pochodzenia klasy ze wspólnego modułu i dalszej nieobecności prywatnych modułów Spotify. Powtórzony poprawiony przypadek przeszedł 1/1; pozostałych 88 nie zmieniano.

Osobny proces z prawdziwym runtime Public, wx.App i faktycznie odfiltrowanym katalogiem app skonstruował natywne okno bez pokazywania go i bez konta: filteredPublicImportsReady=True, nativeDialogConstructed=True, rememberedSelectionIndex=1, namesStartWithTitles=True. To test konstruktora i wyboru w kontrolce, nie ręczny odbiór NVDA ani zewnętrzny zapis do playlisty. Testy używają wyłącznie fikcyjnych nazw/identyfikatorów.

### Znane ograniczenia, wersja i commit

Kandydat alpha.484 wymaga jeszcze builda i instalacji. Rzeczywisty odbiór okna przez użytkownika pozostaje otwarty. Nie resetować działającego logowania; przy podmianie zachować normalną kontrolę nagrywania i harmonogramu Public oraz nie zamykać Test. Wcześniejsze dwa braki pełnego katalogu angielskiego pozostają poza tym zakresem. Nie publikowano kandydata. Nowy commit ma zawierać wyłącznie tę korektę i dokumentację, bez niezależnych presetów. Limit przed budową: 50% wykorzystane, 50% pozostało, reset 11.11.2026 o 07:45 Europe/Warsaw.
## Dodatkowe polecenie użytkownika — zachować zmiany i sprawdzić Test, 7.11.2026

W trakcie przygotowywania alpha.484 użytkownik polecił sprawdzić ten sam błąd w Test i po potwierdzeniu działania TIDAL w Public przyjąć wszystkie poprawki TIDAL do pełnej wersji Test. To rozszerzenie bieżącej pracy; nie zastępuje naprawy Public.

Sprawdzono zainstalowany Test alpha.482 w osobnym procesie dokładnego runtime `C:\Users\micha\AppData\Local\Programs\AMC Test\runtime\python.exe`, importując GUI wyłącznie z jego zainstalowanego app. Profil Test był wybrany przed importem. Obie klasy okna są dostępne i prawdziwy konstruktor wx okna TIDAL z fikcyjną playlistą przeszedł: installedProfile=test, tidalPickerImportsPresent=True, nativeTidalPickerConstructed=True. Nie uruchamiano hosta, logowania ani zapisu do konta; działający Test i jego procesy nie były zmieniane. Konkretny NameError brakującego importu dotyczy więc Public, w którym Spotify jest wyłączony. Ochronę przed innym błędem otwarcia otrzyma także Test po aktualizacji.

Test odfiltrowanej paczki rozszerzono o dwa profile, każdy w świeżym procesie przed importem GUI. Wersja Test zachowuje prywatne moduły, Public ich nie ma, a oba korzystają ze wspólnego okna TIDAL. Cały aktualny plik test_tidal_picker_profile.py przeszedł 4/4. To późniejsze rozszerzenie testu nie zmienia kodu programu w zbudowanej paczce z dd8e6c03.

Po odbiorze poprawionego Public należy z tych samych naprawionych źródeł zbudować pełny profil Test, zachowując Spotify, Sonosa, Radioarchiwistę i pozostałe możliwości Test oraz wszystkie dotychczasowe poprawki audio i logowania TIDAL. Nie kopiować ograniczonej paczki Public na instalację Test i nie przenosić sesji logowania. Przed instalacją Test uzyskać jego aktualny stan nagrań/harmonogramu. Niezakończone zmiany presetów nadal są zachowane w kanonicznym checkoutcie i należy je uwzględnić zgodnie z osobnymi ustaleniami zbiorczej aktualizacji, a nie nadpisywać przy budowie z czystego commita.
## Instalacja alpha.484 i odbiór użytkownika, 7.11.2026

Public alpha.484 zainstalowano ze źródeł dd8e6c031b62912664b9813ea9a45e796195f278. Instalator publish/public-tidal-picker-alpha484-20261107/AMC-Public-2026-11-07-1954.exe ma 133889558 bajtów; manifest profilu Public, sourceDirty=false i kontrola sekretów zaliczone. Instalacja zakończona kodem 0, log install-alpha484.log potwierdza sukces o 21:21:35, bez restartu. Pełna integralność 3144 plików instalacji: kod 0.

Uruchomiono stabilny C:\Users\micha\AppData\Local\Programs\AMC Public\AMC.exe; CUA potwierdziło okno „AMC Public — 07.11.2026, 19:54”, polskie menu i bibliotekę TIDAL (818 pozycji, pierwsze 150). Przed normalnym startem porównano stan i poświadczenia ze świeżą kopią DPAPI: publicTidalCredentialsUnchanged=true, publicTidalConnectionsPresent=2, publicStateUnchanged=true. Kopia D:\Projekty Codex\AMC - kopie prywatne\TIDAL-20261107\profil-przed-alpha484.dpapi, 46524054 bajty, 9 wpisów, zweryfikowane odszyfrowanie i sumy. Test pozostał uruchomiony w alpha.482.

Późniejszy odbiór użytkownika potwierdza: „sam mechanizm działa, okno i tak dalej bardzo dobrze”. Zastępuje to wcześniejszy otwarty status samego otwarcia okna playlist. Nie dowodzi poprawnego zakresu zaznaczenia: użytkownik zgłosił dodanie całego albumu po wyborze jednego utworu oraz niedziałający Delete. Na jego prośbę dalsze naprawy i audyt wszystkich sesji otrzymują osobne zadanie PLAYLIST-20261107-delete-i-zaznaczenie.md. Nadal przygotować pełny Test ze wspólnej poprawionej bazy po uzgodnionym odbiorze. Nie opublikowano alpha.484. Odbiór przycisku przeglądarki, czasu My Mix i konta innej osoby pozostaje odrębnie otwarty.


## Korekta użytkownika — Spotify Client ID w Public, 9.11.2026

### Materiał do dokumentacji użytkownika

Public i Test trzymają Client ID i sesję osobno. Użytkownik może przepisać do Public ten sam Client ID swojej aplikacji Spotify z Test; adres powrotu obu profili jest identyczny. Następnie zapisuje identyfikator i loguje konto w Public oddzielnie. Osoba bez własnej aplikacji znajdzie w `Ctrl+F5` przycisk „Otwórz panel Spotify, aby utworzyć aplikację” w przyszłej wersji; wersja zainstalowana 8.11 pokazuje tylko adres do skopiowania. Instrukcja krok po kroku: `docs/public/SPOTIFY_CLIENT_ID_TEST_PUBLIC_2026-11-09.md`. Gdy Client ID jest puste, przyszłe okno skieruje fokus na to pole i poda wymagany krok zamiast zapowiadać logowanie w przeglądarce.

### Uzasadnienie i decyzje

Użytkownik poprosił o bezpośredni odnośnik do utworzenia aplikacji Spotify i dokładną instrukcję. Własny testowy identyfikator można wykorzystać w obu profilach, lecz nie udostępniać go nieograniczonej grupie odbiorców Public: Spotify ogranicza tryb deweloperski. Soloist nie zastępuje Web API katalogu. Oficjalny panel Spotify jest stabilnym adresem wejściowym; nie zakładamy działania nieudokumentowanego głębokiego adresu formularza.

### Testy i wynik

Weryfikacja składni `gui.py` przeszła. Testy okna konta: 8/8; testy okna konta, skrótów i palety łącznie wymagają ponowienia po aktualizacji oczekiwanego opisu `Ctrl+F5`. Zmiana źródeł nie jest instalacją ani wydaniem.

### Znane ograniczenia

Rzeczywisty odbiór przycisku w NVDA, logowanie na świeżym profilu i dostęp innej osoby pozostają otwarte. Starsze opisy Public bez Spotify są historyczne i nie dotyczą zainstalowanej alpha.486.

### Wersja i commit

Zainstalowany Public alpha.486 z 8.11.2026 pozostaje bez zmian. Nowa poprawka źródeł i instrukcji nie ma jeszcze nowego buildu, commitu ani instalacji.

### Uzupełnienie weryfikacji, 9.11.2026

Po aktualizacji testu zgodności opisu polecenia Ctrl+F5 pełny celowany zestaw okna konta, skrótów i palety przeszedł: 69/69. git diff --check dla zmienionych plików nie wykazał błędów. Nie uruchamiano rzeczywistej przeglądarki ani logowania; nie zbudowano, nie zainstalowano i nie wydano nowego Public.
Szablon językowy po dodaniu pięciu nowych etykiet przeszedł test zgodności 1/1. Pełny test tłumaczeń ma nadal dwa niezaliczone przypadki inwentaryzacji innych, równoległych zmian interfejsu (m.in. Time Shift i podgląd transmisji); nie są wynikiem tej poprawki Spotify. Angielskie odpowiedniki nowych etykiet konta dodano do katalogu zgodności.
9.11.2026 użytkownik zgłosił, że „wszystko działa”, bez wskazania profilu. Odczyt bez ujawniania wartości potwierdził prawidłowy format zapisanego Client ID w Test, natomiast bieżący plik stanu Public nadal nie zawiera Client ID ani nazwy konta i nie był aktualizowany od 7.11. Nie oznaczać logowania Public jako odebranego do czasu wskazania, który profil został sprawdzony. Nie zmieniano profili ani poświadczeń.

## Korekta okien kont — 09.11.2026

Po potwierdzeniu odsłuchu Spotify w Public poprawiono źródła wspólnego okna
konta. Stan Spotify bierze teraz rzeczywisty wynik katalogu, zapisany Client
ID można skopiować, a po zalogowaniu przycisk „Zaloguj” ustępuje
„Wyloguj ze Spotify w AMC”. Wylogowanie usuwa token bieżącego profilu,
zatrzymuje odtwarzanie Spotify i czyści pamięć katalogu, lecz zachowuje
Client ID. TIDAL pokazuje po zalogowaniu „Wyloguj z katalogu TIDAL w AMC”;
jego osobne poświadczenie natywnego odtwarzacza pozostaje odrębne. Sonos
już wcześniej pokazywał „Wyloguj konto” tylko w stanie połączenia; nie
zmieniano jego logowania, odbiór na sprzęcie czeka na czwartek 12.11.2026.

Zmiany źródłowe i testy celowane: host C# zbudowany bez ostrzeżeń, 1 zestaw
kontraktu Spotify i 86 testów Python kont/tytułu zaliczone. Test Sonos bez
rzeczywistego wx: 9 zaliczonych; jedna sonda GUI nie uruchomiła się z powodu
brakującego modułu wx w globalnym Pythonie. Nie wykonano wylogowania na
rzeczywistym koncie, instalacji, odbioru NVDA ani wydania. Korekta zastępuje
wcześniejszy opis stałego przycisku ponownego logowania w tym zadaniu.
