# Integracja TIDAL w AMC

Instrukcja pierwszego połączenia prawdziwego konta i testu z NVDA znajduje się
w pliku [`INSTRUKCJA_LOGOWANIA_TIDAL_PL.md`](INSTRUKCJA_LOGOWANIA_TIDAL_PL.md).

## Cel i granice

### Wynik testu Embed i logowania (2026-09-09, po przygotowaniu 335)

Pełne odtwarzanie **nadal nie jest potwierdzone w AMC**. W oficjalnym
odtwarzaczu na tidal.com po zwykłym logowaniu ten sam utwór przekroczył
granicę próbki (pauza przy 1:09 z 3:35). Embed po odświeżeniu nadal udostępniał
0:30 i ponownie pokazywał link logowania. Nie powtarzać logowania w pętli.

Odczyt publicznego kodu Embed wskazuje ważną różnicę: zwykła inicjalizacja
korzysta z dostawcy bez tokenu użytkownika; osobne uwierzytelnienie użytkownika
dotyczy Nostr. Link po zakończeniu próbki otwiera stronę tidal.com w nowej
karcie, bez widocznego w tym przepływie powrotu z poświadczeniem do Embed.
To wyjaśnienie zgodne z testem, a nie dowód identyczności wdrożonego kodu
ani obietnica pełnego odtwarzania po dodaniu Nostr.

Źródła przypięte do commita, wyniki, granice jakości i warunki dalszego testu:
[`TIDAL_WERYFIKACJA_PELNEGO_ODTWARZANIA_2026-09-09.md`](TIDAL_WERYFIKACJA_PELNEGO_ODTWARZANIA_2026-09-09.md).
Po osobnej zgodzie użytkownika pytanie opublikowano 9 września 2026 r. na
oficjalnym forum deweloperskim TIDAL: [dyskusja nr 384](https://github.com/orgs/tidal-music/discussions/384).
Publikacja nie oznacza uzyskania zgody ani rozwiązania problemu. Zapis treści:
[`TIDAL_PYTANIE_DO_DOSTAWCY_EN.md`](TIDAL_PYTANIE_DO_DOSTAWCY_EN.md).

### Pełne utwory — ustalenia i następny test (2026-09-09)

Priorytet użytkownika to pełne odtwarzanie. Nie uzyskano go w AMC; nie należy
traktować działających próbek jako zakończenia integracji.

- W zainstalowanym SDK `@tidal-music/player` 0.20.1 żądanie manifestu ma
  `usage: PLAYBACK`. AMC przekazuje poświadczenie użytkownika, jego ID oraz
  własny Client ID; nie wymusza trybu próbki. SDK odczytuje `trackPresentation`
  i `previewReason` z odpowiedzi serwera. Sama podmiana etykiety PREVIEW na
  FULL nie dostarczy pełnego materiału.
- Dokumentacja rozróżnia publiczny Player SDK od gotowego TIDAL Embed Player.
  Warunki platformy opisują pełne utwory dla abonentów przez Embed, a poza nim
  próbki. Działająca autoryzacja kolekcji nie potwierdza praw do pełnego audio.
  [Warunki platformy](https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms),
  [wytyczne odtwarzania](https://developer.tidal.com/documentation/guidelines/guidelines-design-guidelines).
- Jest to podstawa do sprawdzenia Embed, nie dowód, że zadziała w desktopowym
  WebView2 ani że udostępnia API do sterowania z natywnego interfejsu AMC.
  Nie potwierdzono też możliwości uzyskania szerszego dostępu dla klienta AMC.
- Logi z wersji 334 nie zawierają szczegółowej przyczyny próbek. Dopiero
  uruchomienie 335 i próba odtworzenia pozwolą sprawdzić `tidal-player-access`.
  Nie przypisywać kontu konkretnego powodu bez odpowiedzi SDK.

Kolejność dalszej weryfikacji:

1. Odczytać rzeczywisty powód ograniczenia w 335; rozróżnić uprawnienia
   aplikacji od abonamentu. Nie kierować użytkownika do logowania w pętli.
2. Sprawdzić oficjalny Embed na jednym utworze i prawdziwym koncie abonenta.
   Logowanie odbywa się wyłącznie po stronie TIDAL; AMC nie przejmuje haseł
   ani sesji Chrome. Początkowo test blokowały uprawnienia narzędzia; po
   uzyskaniu zgody użytkownika wykonano porównanie opisane powyżej. Embed
   pozostał przy próbce mimo skutecznego logowania do głównej strony.
3. Jeśli pełny utwór zadziała, osobno zweryfikować zgodność osadzenia w AMC,
   dostępne sterowanie, trwałość logowania, NVDA, Escape/Spację, brak kradzieży
   fokusa i błędnych przejść Kolejki. Nie zastępować obecnego silnika przed
   tym testem; interfejs Embed może wymagać widocznego okna.
4. Jeśli potrzebny jest dostęp partnerski lub nieudokumentowany interfejs,
   przygotować pytanie do TIDAL o wspieraną integrację dostępnej aplikacji
   desktopowej. Wysłanie pytania wymaga osobnej zgody, nie zgody na publikację
   AMC. Nie używać identyfikatorów cudzych aplikacji ani wydobywania audio.

### Alpha.335 — przełączanie, diagnostyka i TIDAL Connect (2026-09-09)

Użytkownik potwierdził w 334 słyszalne próbki około 30 sekund, przewijanie
i powrót do początku. Zmiana utworów subiektywnie trwa długo. Nie ma jeszcze
porównawczego pomiaru ani potwierdzenia pełnych utworów.

- Oficjalne `load()` w zainstalowanym SDK 0.20.1 samo rozpoczyna reset
  równolegle z pobraniem informacji o odtwarzaniu. AMC nie wykonuje już
  dodatkowego, wcześniej oczekiwanego `reset()` przed każdym `load()`.
  Jawne Stop nadal wywołuje reset. Identyfikatory prób, tłumienie błędnych
  zakończeń i zachowanie Kolejki po próbce pozostają obowiązujące.
- Poświadczenia są przygotowywane równolegle z inicjalizacją WebView2;
  obie operacje kończą się przed poleceniem odtwarzania. Bez dodatkowego
  logowania, otwierania okien lub pobierania kolejnych utworów na zapas.
- Log `tidal-player-timing` rozdziela czas przygotowania hosta/poświadczeń,
  oczekiwania w kolejce poleceń JS, `load()`, `play()` i potwierdzenia startu.
  Są to czasy operacji, nie czysty pomiar sieci TIDAL ani gwarancja odsłuchu.
  Nie ma okresowych komunikatów głosowych z tymi pomiarami.
- `tidal-player-access` zapisuje rzeczywiste `PREVIEW`/`FULL`, czas materiału
  oraz rozpoznany `previewReason`: wyższy poziom dostępu aplikacji,
  subskrypcja albo zakup. Nieznane/brakujące wartości mają osobny stan;
  dowolny tekst, adres ani token nie trafiają do tego pola logu.
  Sam ogólny kod błędu `S3016` nie jest dowodem ograniczenia dostępu.

**TIDAL Connect: blokada integracyjna, nie ukończona funkcja.**
[Oficjalna dokumentacja Connect](https://developer.tidal.com/documentation/connect)
podaje, że wspierane są integracje partnerów sprzętowych. Nie znaleziono
publicznego interfejsu kontrolera pozwalającego AMC wyświetlić urządzenia
Connect i przekazać na nie wskazany utwór. Abonament, synchronizacja kolekcji,
wykrycie WiiM ani API `getOutputDevices` odtwarzacza nie są potwierdzeniem
takiego dostępu. Nie dodajemy pozornego wyboru urządzenia, który w rzeczywistości
zmienia tylko głośniki Windows lub wysyła samą próbkę jako pełny utwór.

Docelowy podział przy integracji urządzeń:

1. Wyjście Windows: głośniki/interfejs USB obsługiwane przez dany silnik.
2. Urządzenie sieciowe i jego sterowanie: osobny adapter, np. obecny WiiM.
3. TIDAL Connect: wybór celu, rozpoczęcie wskazanego materiału i potwierdzony
   stan urządzenia dopiero po uzyskaniu dokumentacji/uprawnień kontrolera.
   Przy braku dostępu nie zmieniać sesji ani urządzenia bez wiedzy użytkownika.
   Odtwarzanie ma być przypisane do urządzenia/konta, nie do ostatniej
   lokalnej próbki; nie wolno używać lokalnego końca próbki do sterowania
   kolejką Connect. Odłączenie urządzenia nie może samoczynnie uruchamiać
   odsłuchu na głośnikach ani znosić wyciszenia sesji.

Obecna droga do sprawdzenia: uruchomić Connect na WiiM z oryginalnej aplikacji
TIDAL, a następnie sprawdzić podstawowe sterowanie i metadane w sesji WiiM
AMC. To nie oznacza uruchomienia dowolnego utworu z biblioteki AMC na WiiM.
[Instrukcja producenta WiiM](https://faq.wiimhome.com/en/support/solutions/articles/72000608942-how-to-stream-music-to-your-wiim-device-via-tidal-connect).
Nie przetestowano tej drogi na urządzeniu użytkownika podczas prac nad 335.

[Semantyka SDK load()](https://github.com/tidal-music/tidal-sdk-web/blob/main/packages/player/src/internal/handlers/load.ts).

### Diagnostyka alpha.333 i poprawka alpha.334 (2026-09-09)

Zdiagnozowano dwa konkretne błędy AMC, niezależne od synchronizacji katalogu:

- Log `tidal-player-detail` zawierał `NotAllowedError` oraz informację,
  że użytkownik nie wykonał interakcji z dokumentem. Klawiaturę obsługuje WPF,
  a `PostWebMessageAsJson` nie przenosi gestu użytkownika do strony SDK.
  Dedykowane środowisko WebView2 otrzymuje teraz
  `--autoplay-policy=no-user-gesture-required`. Nie modyfikujemy rejestru,
  profilu Chrome, globalnych preferencji przeglądarki ani zasad DRM/CORS.
- W oficjalnym SDK 0.20.1 `EndedEvent` zawiera `mediaProduct` i `reason`:
  `completed`, `error`, `skip`. Obsługiwanie każdego `ended` jako ukończonego
  utworu powodowało przejście do następnego także po odmowie odtwarzania.
  Mostek i część C# wymagają teraz `completed`, potwierdzonego startu oraz
  aktualnego identyfikatora i numeru próby. Odrzucają stare zakończenia,
  podwójne zakończenia, błędy starych zadań oraz zdarzenia po Stop/Pauza.

Koniec próbki `PREVIEW` zatrzymuje próbę bez usuwania pełnego utworu z Kolejki.
Ogólny błąd nie jest powodem ponownego logowania. Komunikat o blokadzie gestu
wskazuje integrację AMC; ograniczenia konta, materiału i aplikacji są osobne.
Surowe adresy, Bearer i pola poświadczeń są redagowane przed zapisem logu.

Weryfikacja obejmuje siedem wykonywalnych testów JS z kontrolowanym SDK,
testy rzeczywistego mostka C# i sesji/Kolejki oraz izolowany test WebView2:
`PostWebMessage` bez poprawki kończy się `NotAllowedError`, a z poprawką
odtwarza wygenerowany WAV. Test nie loguje się do TIDAL, nie zastępuje testu
pełnego utworu, DRM ani uprawnień konkretnego Client ID. Przygotowanie 334
nie zamyka uruchomionego AMC ani trwającego nagrywania użytkownika.

Źródła i granice:

- [Flagi WebView2 — Microsoft](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/webview-features-flags)
- [Polityka autoplay — Chromium](https://developer.chrome.com/blog/autoplay/)
- [Typ EndedEvent w oficjalnym SDK TIDAL](https://github.com/tidal-music/tidal-sdk-web/blob/main/packages/player/src/api/event/ended.ts)
- [Publiczny quick start TIDAL demonstruje próbki](https://developer.tidal.com/documentation/api-sdk/api-sdk-quick-start)
- [Warunki platformy TIDAL](https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms)

Synchronizacja kolekcji i abonament użytkownika nie stanowią potwierdzenia
prawa aplikacji do pełnego odtwarzania. Publiczna dokumentacja opisuje SDK
próbek, a warunki pełnego odsłuchu wskazują Embed Player dla abonentów.
Pełnego odtwarzania we własnym interfejsie nie należy obiecywać bez potwierdzenia
przez TIDAL uprawnień tego klienta. Nie wydobywamy chronionych adresów ani nie
podszywamy się pod inną autoryzowaną aplikację. Dalsze warianty (Embed,
zewnętrzny odtwarzacz lub dostęp partnerski) wymagają osobnej decyzji.

### Zakres funkcji

TIDAL jest osobną sesją AMC. Demonstracyjne elementy pozostają dostępne do
chwili pierwszej udanej synchronizacji, po odłączeniu konta oraz wtedy, gdy
integracja nie została jeszcze skonfigurowana. Dane demonstracyjne nigdy nie
mogą zostać zapisane na koncie TIDAL.

Aktualny etap prawdziwej integracji obejmuje:

- logowanie użytkownika przez oficjalny OAuth 2.1 Authorization Code z PKCE;
- odczyt osobnych kolekcji ulubionych utworów, albumów, wykonawców, playlist
  i materiałów wideo;
- rozdzielenie interfejsu: `Ctrl+U` pokazuje pojedyncze utwory i materiały
  wideo, a `Ctrl+L` albumy, wykonawców i playlisty;
- wyszukiwanie katalogu TIDAL przez `Ctrl+F` w sesji TIDAL;
- otwieranie albumów, playlist i wykonawców do ich właściwej zawartości;
- jednoznaczne relacje zależne od typu: utwór lub wideo prowadzi do albumu i
  wykonawcy, album do wykonawcy, a wykonawca pokazuje własne albumy;
- osobny widok playlist TIDAL pod `Ctrl+P` oraz sortowanie otwartych
  kontenerów przez `Alt+1` i `Alt+2`;
- dodawanie utworów, materiałów wideo oraz zawartości albumu do jednej lub
  kilku playlist konta przez `Ctrl+Shift+P`, z możliwością utworzenia nowej
  playlisty bez opuszczania tego okna;
- usuwanie wskazanych wystąpień z otwartej playlisty przez `Delete`;
- dodawanie i usuwanie obsługiwanych elementów z kolekcji TIDAL;
- zmianę kolejności elementów własnej playlisty TIDAL przez `Alt+3`,
  `Alt+strzałki` albo `Ctrl+X` i `Ctrl+V`;
- automatyczne odświeżenie po uruchomieniu i ręczne odświeżenie w oknie
  **Konto i synchronizacja TIDAL** pod `Ctrl+F5`;
- odtwarzanie utworów i materiałów wideo przez oficjalny TIDAL Player,
  sterowane z dostępnego odtwarzacza AMC: pauza, wznowienie, przewijanie,
  głośność, przejście do następnej pozycji i obsługa Kolejki;
- odczyt czasu, formatu, częstotliwości próbkowania i bitrate'u dopiero po
  otwarciu rzeczywistego materiału przez silnik TIDAL.

Lewa strzałka działa także na listach TIDAL, lecz nie przypisuje katalogowym
rekordom fikcyjnego rozszerzenia, bitrate'u ani rozmiaru. Dopóki oficjalny
odtwarzacz nie otworzy utworu, komunikat mówi wprost, że format nie został
udostępniony przez katalog, a następnie podaje tylko czas, wykonawcę i inne
znane metadane.

Aktualny etap używa oficjalnego, niezmodyfikowanego modułu TIDAL Player 0.20.1
w izolowanym, niedostępnym dla fokusa komponencie WebView2. AMC przekazuje do
niego wyłącznie identyfikator katalogowy oraz krótkotrwałe poświadczenie z
Menedżera poświadczeń Windows. Poświadczenie Playera zawiera token i
identyfikator użytkownika zwrócony przez `users/me`; sam token wystarcza do
odczytu katalogu, ale oficjalny SDK nie uznaje go bez `userId` za zalogowaną
sesję użytkownika. AMC nie wydobywa adresów strumieni, nie zapisuje chronionych
nagrań i nie przekazuje tokenu do logów, stanu ani schowka.

Błąd pojedynczego materiału unieważnia tylko bieżącą próbę odtwarzania. Późne
zdarzenie `ended` z Playera nie może wtedy przejść do następnego utworu z
albumu, playlisty ani Kolejki. Ponowne logowanie jest proponowane wyłącznie po
rozpoznanym błędzie autoryzacji; błąd dostępności materiału, regionu, planu lub
sieci ma osobny komunikat. Log zachowuje tylko ograniczoną długością treść oraz
kod błędu SDK, nigdy token ani chroniony adres odtwarzania.

Zakres odtwarzania zależy od poziomu dostępu przyznanego konkretnej aplikacji
w portalu TIDAL. Publiczny poziom może zwrócić tylko próbkę mimo konta z
abonamentem. AMC wykrywa stan `PREVIEW` i mówi o nim wprost; nie zapętla
logowania i nie przedstawia próbki jako pełnego utworu. Ograniczenia usługi
nie wolno obchodzić własnym resolverem chronionych adresów.

## Konfiguracja deweloperska

Do testu potrzebna jest aplikacja utworzona w panelu TIDAL Developer. Należy
zarejestrować dokładny adres powrotu:

`http://127.0.0.1:43821/tidal/callback/`

oraz włączyć minimalne zakresy:

- `user.read`;
- `collection.read`;
- `collection.write`;
- `playlists.read`;
- `playlists.write`.

Do AMC wprowadza się tylko Client ID. Client Secret nie jest wymagany w
przepływie użytkownika z PKCE i nie wolno wpisywać go do programu, dokumentacji,
repozytorium ani rozmowy. Hasło jest wpisywane wyłącznie na stronie TIDAL
otwartej w domyślnej przeglądarce. Lokalny odbiornik adresu powrotu działa
tylko przez czas logowania i nie wymaga uprawnień administratora.

Token dostępu i token odświeżania są przechowywane w Menedżerze poświadczeń
Windows pod nazwą `AccessibleMediaController/TIDAL`. Nie trafiają do
`state.json`, logów, eksportu konfiguracji ani pełnej kopii AMC. Odłączenie
konta usuwa je z Menedżera poświadczeń.

Wersja deweloperska może korzystać z Client ID właściciela projektu. Przed
publicznym wydaniem AMC trzeba zarejestrować oddzielną aplikację produkcyjną i
sprawdzić jej poziom dostępu; wymaganie, aby każdy użytkownik sam zakładał
aplikację deweloperską, nie jest docelowym interfejsem instalatora.

## Własność danych i synchronizacja

TIDAL jest źródłem prawdy dla:

- polubionych utworów;
- polubionych albumów;
- polubionych wykonawców;
- polubionych playlist i zawartości playlist TIDAL;
- polubionych materiałów wideo;
- tytułów, wykonawców, czasu trwania, dostępności regionalnej oraz
  identyfikatorów katalogowych.

AMC jest źródłem prawdy dla:

- kolejki bieżącej sesji i pozycji **Odtwórz jako następne**;
- historii odtwarzania i wyszukiwania;
- zakładek, własnych rozdziałów i pozycji wznowienia;
- lokalnych presetów oraz własnej kolejności i nazw pomocniczych;
- głośności, wybranego wyjścia audio i pozostałych ustawień odtwarzania.

Te dane lokalne nie są wysyłane do TIDAL. Polubienie albumu nie oznacza
automatycznego polubienia wszystkich jego utworów ani wykonawcy. Kolekcje
poszczególnych typów muszą więc pozostać rozdzielone także wtedy, gdy są pokazywane we
wspólnym widoku Biblioteki.

Synchronizacja wykonuje żądania kolejno, ponieważ publiczne API ma niski limit
wywołań. Każda kolekcja jest stronicowana przez nieprzezroczysty kursor z
`links.next`. Odpowiedź `429` respektuje `Retry-After` i jest ponawiana z
ograniczeniem. Udany odczyt zastępuje tylko odpowiadającą mu kategorię. Jeżeli
jedna kategoria nie zostanie pobrana, AMC zachowuje jej ostatni poprawny stan;
nie wolno wyczyścić całej sesji częściowym wynikiem. Gdy nie powiedzie się
żadna kategoria, poprzednia zawartość pozostaje bez zmian i synchronizacja nie
jest oznaczana jako udana.

Adres `links.next` zwracany przez TIDAL może zaczynać się od
`/userCollection...`, mimo że endpoint API znajduje się pod prefiksem `/v2`.
AMC musi traktować taki adres jako względny wobec podstawy API `/v2/`, a nie
wobec korzenia domeny. Utrata prefiksu daje błąd 404 dopiero od drugiej strony,
więc test regresji obowiązkowo obejmuje kolekcję liczącą ponad 20 elementów.

Aktualne endpointy relacji kolekcji użytkownika nie przyjmują parametru
`countryCode`; serwer odrzuca parametry, których dany endpoint nie dokumentuje.
Kod kraju pozostaje wymagany dla wyszukiwania katalogu. Wyszukiwanie korzysta z
`GET /searchResults?filter[query]=...`, a nie ze starszego wariantu z tekstem
zapytania umieszczonym w ścieżce.

AMC nie tworzy trwałego, nieograniczonego lustra katalogu TIDAL. W pamięci
przechowuje bieżący wynik potrzebny do działania sesji, a po następnym
uruchomieniu odświeża go z serwera. Ogranicza to ryzyko pokazywania usuniętych,
zastąpionych albo regionalnie niedostępnych nagrań oraz jest zgodne z zasadami
platformy TIDAL.

## Zapis do konta

AMC używa `collection.write` wyłącznie do dodawania i usuwania obsługiwanych
elementów z kolekcji. Zakres `playlists.write` służy do dodawania, usuwania i
zmiany kolejności elementów playlisty, którą dane konto może edytować. Album
zawsze zachowuje kolejność wydania, a porządek alfabetyczny jest jedynie
sposobem prezentacji.
Każda obecna zmiana konta jest:

1. wykonywana na stabilnym identyfikatorze TIDAL;
2. wysyłana z osobnym `Idempotency-Key`;
3. oznajmiana jako zakończona dopiero po odpowiedzi sukcesu;
4. natychmiast odzwierciedlana w pamięci i bieżącym widoku AMC bez kosztownego
   ponownego pobierania całej kolekcji;
5. kontrolowana przy ręcznej, okresowej i startowej pełnej synchronizacji;
6. cofana wizualnie po błędzie zamiast pozostawiania fałszywego stanu.

Taki zapis jest celowy: odpowiedź sukcesu z endpointu zapisu potwierdza
przyjęcie zmiany, a pobieranie kilkuset elementów po każdym pojedynczym
`Delete` albo `Ctrl+Shift+U` powodowałoby wielosekundową ciszę. Jeżeli stan
zmieni się równolegle w innej aplikacji, następna synchronizacja z TIDAL
pozostaje rozstrzygająca.

Relacja pozycji playlisty ma własny nieprzezroczysty identyfikator wystąpienia.
AMC zachowuje go wyłącznie wewnętrznie, dzięki czemu poprawnie rozróżnia dwa
wystąpienia tego samego utworu i nigdy nie wypowiada identyfikatora przez NVDA.
API przyjmuje najwyżej 20 przenoszonych pozycji na jedno żądanie; AMC egzekwuje
ten sam limit i wysyła `Idempotency-Key`. Po zmianie playlista jest pobierana
ponownie z TIDAL, zanim program ogłosi sukces.

`Ctrl+P` w sesji TIDAL nigdy nie tworzy ani nie pokazuje lokalnych playlist
AMC. Pokazuje prawdziwe playlisty konta. `Ctrl+Shift+P` otwiera dostępny wybór
jednej albo kilku playlist docelowych; album jest przed zapisem rozwijany do
utworów w kolejności wydania. Domyślnie zaznaczona jest ostatnia playlista, do
której AMC skutecznie coś dodał. Przycisk **Nowa…**, `Ctrl+N` lub `Insert`
pozwala utworzyć prawdziwą playlistę TIDAL i od razu dodać do niej wybrane
elementy. Utworzenie korzysta z oficjalnego zapisu JSON:API i klucza
idempotencji, więc ponowienie po ograniczeniu liczby zapytań nie może utworzyć
drugiej kopii playlisty. Na koncie bez playlist fokus trafia od razu na
**Nowa…**, zamiast pozostawać na pustej liście.

`Delete` wewnątrz playlisty usuwa wskazane wystąpienie relacji, dzięki czemu
dwie kopie tego samego utworu nie są mylone. Zmiana nazwy i kasowanie całego
zdalnego kontenera nie są jeszcze udostępnione w AMC i należy je wykonywać w
TIDAL.

Widoczność poleceń **Przejdź do albumu**, **Pokaż albumy wykonawcy** i
**Przejdź do wykonawcy** wynika z relacji zwróconych dla konkretnego elementu,
a nie z chwilowego fokusu lub poprzedniego widoku. Prawa strzałka pokazuje
krótkie menu tych relacji. Menu kontekstowe i menu prawej strzałki przekazują
NVDA pozycję liczoną wyłącznie wśród widocznych poleceń; ukryte działania innej
sesji nie zwiększają wartości „z ilu”.

Nawigacja literowa podąża za pierwszym czytanym polem identyfikującym element:
tytułem albo wykonawcą. Nie korzysta z rodzaju, usługi, czasu, stanu
ani numeru pozycji. Ta sama reguła obowiązuje w Bibliotece, Ulubionych,
playlistach, otwartych kontenerach i wynikach wyszukiwania. Sortowanie
alfabetyczne `Alt+2` używa tego samego klucza, więc sposób czytania, szukania
literą i porządek widoku nie przeczą sobie.

Wyjątkiem celowo dopasowanym do hierarchii jest otwarty album TIDAL. Album
zachowuje kolejność wydania zwróconą przez usługę, ale każdy utwór jest czytany
od tytułu, potem od wykonawcy, niezależnie od ogólnej kolejności pól. Litera
szuka wtedy po tytule. Na liście albumów jednego wykonawcy pierwszym polem i
kluczem literowym również jest tytuł albumu, ponieważ nazwa wykonawcy byłaby
identyczna w każdym wierszu.

Prawa strzałka i menu kontekstowe pokazują wyłącznie relacje mające sens dla
bieżącego rodzaju. W wynikach wyszukiwania wybór relacji musi zamknąć okno
wyszukiwania przed nawigacją; pozostawienie dialogu otwartego powodowałoby
pozorne wykonanie polecenia bez wejścia do albumu lub wykonawcy.

Prawa strzałka na zwykłej liście TIDAL i w wynikach wyszukiwania otwiera małe
menu relacji. Zależnie od danych elementu zawiera **Przejdź do albumu**,
**Przejdź do wykonawcy** albo obie pozycje. Polecenia pozostają również w menu
kontekstowym i palecie, lecz tylko wtedy, gdy odpowiedź API zawiera właściwą
relację. Otwarty album uzupełnia relację albumu dla swoich utworów, a otwarty
wykonawca relację wykonawcy dla swoich albumów; techniczne identyfikatory nie
mogą pojawić się w mowie NVDA. Nie przypisujemy osobnego skrótu z literą `G`,
ponieważ takie kombinacje kolidują z narzędziami systemowymi użytkownika.

Kolejka pozostaje lokalnym widokiem sesji AMC. Jej nagłówek podaje liczbę
elementów oraz sumę znanych czasów. Zwrot **jako następne** jest dodawany tylko
wtedy, gdy przynajmniej jedna pozycja ma tę flagę; zwykłe elementy Kolejki nie
są tak oznaczane. Zwykła Kolejka i warstwa **Odtwórz jako następne** mają osobne
trwałe porządki. Są zapisywane według stabilnego identyfikatora zasobu TIDAL,
a nie tymczasowego identyfikatora obiektu lub wystąpienia na playliście.
Synchronizacja katalogu, ponowne otwarcie kontenera i ponowne uruchomienie AMC
odtwarzają te flagi po utworzeniu nowych obiektów; brak flag w świeżo pobranym
rekordzie nie jest poleceniem wyczyszczenia lokalnej Kolejki.

Przy konflikcie członkostwo i kolejność zdalnej kolekcji wygrywają po stronie
TIDAL, a lokalne zakładki, presety, historia i ustawienia pozostają po stronie
AMC. `Ctrl+Z` nie może obiecywać cofnięcia operacji serwerowej, dopóki API nie
potwierdzi bezpiecznej operacji odwrotnej.

## Kontrakt przyszłych adapterów usług

Do Spotify i Apple Music należy przenieść mechanizm, a nie nazwy kolekcji
TIDAL. Każdy adapter musi zachować następujące zasady:

- stabilny identyfikator zasobu usługi jest oddzielony od identyfikatora jego
  wystąpienia w kontenerze, wiersza interfejsu i obiektu cache;
- Kolejka AMC, **Odtwórz jako następne**, Historia, zakładki, fokus, filtr i
  własna kolejność nie mogą zniknąć przy odświeżeniu katalogu;
- zapis zdalny jest ogłaszany dopiero po potwierdzeniu serwera, następnie
  aktualizuje lokalny cache, a pełna synchronizacja służy do późniejszego
  uzgodnienia stanu;
- niepełna odpowiedź odświeża tylko poprawnie pobrane kategorie; błąd jednej
  kategorii nie zeruje pozostałych ani danych lokalnych;
- semantyka Biblioteki i Ulubionych pochodzi z danej usługi. Apple Music i
  Spotify nie mogą dostać sztucznego podziału tylko dlatego, że tak prezentuje
  się TIDAL;
- każda nowa lista i menu relacji otrzymują jawne etykiety dostępnościowe i
  test NVDA bez reprezentacji klas, enumów ani identyfikatorów API.

## Odtwarzanie i urządzenia

Chronione nagrania są odtwarzane wyłącznie przez oficjalny moduł TIDAL Player.
Web API nadal służy do metadanych i operacji na koncie; nie dostarcza AMC
ścieżki do własnego wydobywania adresów audio. Adapter jest osadzony w WebView2
o rozmiarze jednego piksela, bez tabulatora, obsługi kliknięć i nazwy
dostępnościowej. Dla NVDA jedynym interfejsem pozostaje natywny odtwarzacz AMC.

Jakość żądana przez AMC to `HI_RES_LOSSLESS` z adaptacyjnym doborem strumienia;
rzeczywisty format zależy od materiału, konta i poziomu dostępu. Oficjalny
moduł nie udostępnia obecnie zmiany prędkości, dlatego w sesji TIDAL te
przyciski i polecenia są ukryte, zamiast pozornie działać. Pierwszy etap używa
domyślnego urządzenia Windows dla WebView2. Osobny wybór urządzenia TIDAL
wymaga dalszego testu API urządzeń oficjalnego Playera.

TIDAL Connect nie jest publicznym API sterowania dowolnym urządzeniem. TIDAL
udostępnia jego integrację partnerom sprzętowym. AMC może nadal sterować WiiM
przez adapter WiiM, gdy na urządzeniu gra TIDAL Connect, ale nie może
przedstawiać tego jako własnej implementacji TIDAL Connect.

## Znane ryzyka do testu z prawdziwym kontem

- dokładna zgodność adresu powrotu skonfigurowanego w panelu;
- poziom dostępu przypisany aplikacji deweloperskiej i odpowiedzi `401`/`403`;
- zakresy faktycznie zwrócone w tokenie;
- odświeżenie tokenu po wygaśnięciu oraz cofnięcie zgody w TIDAL;
- różne identyfikatory i zamienniki tego samego wydania;
- element niedostępny w kraju `PL` lub usunięty między stronami wyników;
- bardzo duża kolekcja, niski limit zapytań, `429` i niepełna synchronizacja;
- pusta kolekcja będąca poprawnym wynikiem, a nie błędem;
- kolejność i zawartość playlist oraz playlisty zawierające materiały wideo;
- brak sieci podczas logowania, synchronizacji i wyszukiwania;
- powrót fokusa NVDA z przeglądarki do okna konta;
- brak tokenów i technicznych obiektów w logach, eksporcie oraz mowie NVDA.

## Odświeżanie poświadczenia i lokalny katalog awaryjny

Implementacja musi odpowiadać oficjalnemu modułowi `@tidal-music/auth`.
Żądanie `refresh_token` zawiera `client_id`, `grant_type`, `refresh_token` oraz
skonfigurowany `scope`. Odpowiedź może nie zawierać nowego tokenu
odświeżającego; w takim przypadku poprzedni pozostaje ważny. Gdy serwer go
obraca, nowa wartość zastępuje starą w Menedżerze poświadczeń Windows.

Katalog awaryjny przechowuje wyłącznie publiczne metadane kolekcji i jej
znaczniki członkostwa oraz kolejności. Nie wolno w nim zapisywać tokenów ani
chronionych adresów odtwarzania. Adapter podczas startu zasila tym katalogiem
zarówno widok, jak i cache synchronizacji poszczególnych kategorii, dzięki
czemu częściowa odpowiedź nie usuwa albumów, playlist, wykonawców lub utworów,
których akurat nie udało się pobrać. Ta zasada jest obowiązkowym wzorcem dla
przyszłych adapterów Spotify i Apple Music.

## Oficjalne źródła

- https://developer.tidal.com/documentation/api-sdk/api-sdk-authorization
- https://developer.tidal.com/documentation/api-sdk/api-sdk-manage-apps
- https://tidal-music.github.io/tidal-api-reference/
- https://github.com/tidal-music/tidal-sdk-web
- https://github.com/tidal-music/tidal-sdk-web/blob/main/packages/player/README.md
- https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms
- https://developer.tidal.com/documentation/guidelines/guidelines-developer-guidelines
- https://developer.tidal.com/documentation/connect
