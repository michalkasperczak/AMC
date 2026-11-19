# Analiza zgłoszeń Huberta #11, #25, #26 i #28 — 19.11.2026

## Nowe #30 / PR #31 — odczyt i opis, bez wdrożenia

Na nowe polecenie Michała sprawdzono zgłoszenie o pozostawaniu dźwięku na
głośnikach po podłączeniu słuchawek oraz opóźnionym odzyskaniu YouTube po
odłączeniu. PR #31 wymienia samo wyjście audio i śledzi domyślne urządzenie.
Świeży odczyt: OPEN, nie scalony, brak check-runs; pomiary Huberta nie są
niezależnym odbiorem. Nie dodano automatycznie do realizacji #25/#26/#28.
[Opis, zakres, kod i granice dowodu](HUBERT30-OUTPUT-20261119-ocena.md).

## Decyzja wykonawcza Michała — 19.11.2026, po analizie

**Status bieżący: W REALIZACJI.** Michał polecił „po kolei wdrażaj, poprawiaj”,
a następnie jednoznacznie potwierdził „Tak, zgadzam się na realizację. Oczywiście”.
**#25 zakończony w źródłach:** dane/trwałość `adf6e51`, harmonogram `d113fae`.
Niezależny przegląd korekty i końcowy kontrolowany test anulowania zaliczone.
Teraz #26/PR #27; następnie #28/PR #29. Bez instalacji i publikacji.
[Odbiór harmonogramu i ograniczenia](HUBERT25-SCHEDULE-20261119-akceptacja.md).
Poniższa analiza pozostaje materiałem wejściowym, a jej dawne ograniczenie do
odczytu nie oznacza już braku zgody na realizację zaakceptowanych poprawek.

Kolejność:
1. #25: daty RSS przed limitem listy, brak wypierania nowych zwykłych filmów,
   bezpieczne oznaczanie „nowe” bez masowego czyszczenia danych użytkownika,
   trwały zapis wyniku każdego kanału. Regresje >100 elementów i przerwania
   po pierwszym z kilku kanałów; rzeczywisty RED/GREEN i ponowne otwarcie bazy.
2. #25: ograniczony catch-up i dopasowany harmonogram, bez zmiany istniejących
   interwałów użytkownika i bez odblokowania ciężkiej pracy podczas nagrywania.
3. #26: review PR #27, rzeczywiste testy i integracja rozdziałów YouTube.
4. #28: review PR #29, rzeczywiste testy i integracja wznowienia bez zerowania toru.
5. Niezależny odbiór i wspólny kandydat. Nie mylić źródeł/testów/builda z
   instalacją, odsłuchem, NVDA i publikacją. Odrębne nierozstrzygnięte bramki
   wspólnego planu RELEASE-20261119 pozostają w mocy; nie relabelować artefaktów.

Chrome zostaje. Tempo Spotify nie jest zgodą na globalne odblokowanie GUI ani
logowanie kont; ewentualny eksperyment musi pozostać izolowany. RSS-first,
range/cache, automatyczne mDNS i parowanie pozostają poza tym wdrożeniem.
Nie instalować, zamykać ani restartować AMC na głównym komputerze Michała.
Zachować AV, konta i nagrania. Wykonanie przez Codex w ramach konta,
bez osobno płatnego API. Rodzic prowadzi wspólne rejestry; wykonawcy mają
izolowane worktree i nie nadpisują pięciu wcześniejszych zmian dokumentacji.

## Etap #25 P0 — kandydat i kontrola przed integracją

Wykonawca dostarczył `6f55a14b53703201ecf30da793e23e39e2e9c28b`
(gałąź `codex/amc-hubert25-data-20261119`, baza `80c2576`). Zmiany: bezpieczna
flaga „nowe” przy braku wspólnych ID, daty RSS przed limitem scalonej listy,
zapis ukończonego kanału przed pobieraniem następnego. Trzy regresje przeszły
rzeczywiste RED → GREEN na Windows; rzeczywisty LiteHost zbudowany bez błędów
ani ostrzeżeń. Nie wykonano masowego czyszczenia starych flag użytkownika.

Rodzic odczytał wszystkie trzy zmiany produkcji, diff testów i surowe kwity
RED/GREEN. Niezależnie uruchomił `--podcast-progress` na stagingu Windows:
`Wszystkie zestawy przeszly (1).`, exit 0. Cztery pliki źródłowe stagingu
porównano bajtowo z zamrożonym `6f55a14b`; hash użytej DLL protokołu:
`d8ac22e0f056694f53e4506abb0d9cb48b5087e740a06277daf6f356ffad1571`.
To nie jest wynik GUI, żywego YouTube ani instalacji.

**Jeszcze nie scalać:** kontrola sąsiedniej granicy wykazała, że przy pustym
RSS `CompleteFromChannelFeed` wraca przed końcowym sortowaniem/limitem.
Po nowym scaleniu bez wczesnego obcięcia może zwrócić 200 zamiast 100
kandydatów. Zlecono wąski test RED/GREEN i poprawkę w osobnym worktree
`codex/amc-hubert25-rss-fallback-20261119`, baza `6f55a14b`.
Przygotowana osobno korekta RSS `9700a6133b7157053cd44be5a36856134ef3644f`
została odebrana: końcowe sortowanie i limit 100 działają także bez RSS,
a playlisty nie są normalizowane jak kanały. Wykonawca uzyskał RED → GREEN,
a rodzic przeczytał pełny delta-diff i niezależnie wykonał Windows Smoke
`--youtube-collection-normalization`: exit 0. Źródła stagingu helpera i testu
porównano bajtowo z tym SHA. Build LiteHost wykonawcy: 0 błędów i ostrzeżeń.
Korekta jest nadal w izolowanym worktree, nie w wydaniu.
Niezależny przegląd bazowego P0 pozostał **BLOCKED** z drugiego powodu:
`ApplyRefresh` mimo jednego ID wczytuje i skanuje całą bazę
przez `ApplyRefreshBatch` → `LoadInto`/`SaveCore`. Nie ma masowego UPDATE,
ale N kanałów powoduje N pełnych materializacji i fingerprintowania.
Nie zmierzono konkretnego spowolnienia. Koryguje to wcześniejsze zapewnienie
o krótkiej operacji zapisu tylko jednego kanału. Zlecono wąski RED/GREEN
ograniczonej operacji DB na `codex/amc-hubert25-target-store-20261119`,
baza `6f55a14b`; trwałość pierwszego sukcesu przed następnym fetchem ma zostać.
[Werdykt, dowody i warunki odbioru](HUBERT25-DATA-20261119-review.md).

Korekta DB dostarczona jako `9611e8513c90b751bbe458320e588889650afa38`:
odczyt i zapis ograniczony do celu; zachowane ordinale istniejących wierszy,
postęp i stan innych kanałów. Wykonawca uzyskał deterministyczny RED/GREEN
na globalnej deserializacji, testy obu callerów i build rzeczywistego hosta.
Rodzic przygotował osobno wspólny kandydat `ec34b3b` (trzy zmiany), uruchomił
cztery celowane trasy Windows oraz build LiteHost Release: wszystkie exit 0,
build bez błędów i ostrzeżeń. [Kwity i granice odbioru](HUBERT25-P0-20261119-akceptacja.md).
**#25 P0 ODEBRANO i zintegrowano:** niezależny delta-review `9611e851`
uzyskał PRZYJĄĆ bez blockera; rodzic przeczytał pełny raport i potwierdził
niezmienność odebranych źródeł. Merge kanoniczny: `adf6e51c6880a03335fc082e6881e0363273c824`.
Wszystkie 12 przeniesionych plików jest bajtowo zgodnych z przetestowanym
`ec34b3b`. Nie publikowano ani nie instalowano.
Po odbiorze P0 nastąpił ograniczony catch-up i harmonogram #25, odebrany poniżej.
Nie rozpoczynano tych wdrożeń przed odbiorem danych/trwałości.

## Etap #25 harmonogram — ODEBRANY; niżej historia odbioru

Końcowy kandydat `dd01bcb` zintegrowano w `d113fae` po niezależnym
PRZYJĄĆ korekty `5f0242f` i GREEN kontrolowanego timera. Rodzic odczytał
pełne dwa raporty i diff aparatury, potwierdził brak zmiany produkcji od
przejrzanego `5f0242f`, 3049 plików stagingu i bajtową zgodność 13 plików kodu.
Niezależny końcowy Windows Protocol `--podcast-progress`: exit 0, 1 zestaw PASS.
Wszystkie 16 plików merge jest identyczne z kandydatem; zachowano dokumentację.
Ograniczenie testu wejścia do SQLite pozostaje jawne, nie blokuje kodu.
[Pełne dowody i granice](HUBERT25-SCHEDULE-20261119-akceptacja.md).

Poniższe wstrzymania i zlecenia są chronologią, nie aktualną blokadą #25.

Wykonawca dostarczył czysty `c3e140401aad1eb08bfee8a597cc073abcb50ef8`,
baza `ac203b6`, w izolowanym `codex/amc-hubert25-schedule-20261119`.
Kandydat dodaje jeden startowy termin nadrabiania do 30 s, dobór porcji
z kadencji i interwałów (z ustawioną liczbą źródeł jako twardym maksimum),
75-sekundowy budżet anulowania pobrania i telemetrię odroczonych źródeł.
Token jest przekazywany rzeczywistym klientom RSS/YouTube/Facebook;
rodzic sprawdził również połączenie tokenu z zakończeniem własnego procesu
yt-dlp. Nie oznacza to pomiaru obciążenia ani czasu na bibliotece użytkownika.
Zachowano zapisane interwały, wyłączenie przez 0, blokady nagrywania oraz
brak równoległego automatycznego/ręcznego odświeżania.

Wykonawca podał 62 testy Python PASS, Protocol `--podcast-progress` PASS,
Core `--youtube-subscription-sync-apply` PASS i build LiteHost bez błędów
oraz ostrzeżeń. Szczegółowy raport pozostaje w worktree kandydata:
`docs/zadania/HUBERT25-SCHEDULE-20261119-wdrozenie.md`.

Rodzic odczytał pełny diff wszystkich 11 plików oraz raport. Bajtowo
porównał 10 zmienionych plików źródeł/testów stagingu z `c3e1404`,
a następnie niezależnie uruchomił Windows Protocol `--podcast-progress`:
exit 0, „Wszystkie zestawy przeszly (1).” Hash DLL:
`ce263be483cca3c9143d143e1bf93f5df21734245e5ef3b4fbb792d0b643e44b`.
Celowana kontrola Python `test_podcast_refresh.py -k 'startup_catch_up or recording'`:
5 PASS, 13 poza wyborem. Obejmuje zmieniony termin i sąsiednie blokady nagrywania,
nie odbiór żywego GUI/NVDA.

**Odbiór wstrzymany:** niezależny przegląd `c3e1404` zakończony wskazaniem
niezgodności no-arg API: wrapper i dyspozytor hosta przypisują brakowi kadencji
nowe 60 s, choć stara ścieżka bez argumentu oznaczała pełny zapisany limit.
Adaptacyjna kadencja ma obowiązywać po jawnym przekazaniu jej przez GUI;
wywołanie bez argumentów ma zachować zgodność. Rodzic potwierdził ten przepływ
w niezmiennym kodzie. Recenzent uruchomił jedynie celowany test jawnej kadencji,
nie odtworzył jeszcze funkcjonalnego RED dla ścieżki no-arg.

Druga uwaga dotyczy wyścigu między sprawdzeniem budżetu a zapisem próby.
To na razie statyczna hipoteza, nie zmierzony błąd ani dowód utraty danych.
Trzeba rozróżnić przyjęcie źródła przed terminem od przyjęcia po terminie;
samo dodanie kolejnego sprawdzenia tokenu nie dowodzi usunięcia wyścigu.
Zlecono wąski RED/GREEN no-arg API oraz deterministyczną próbę tej granicy
w osobnym `codex/amc-hubert25-schedule-correction-20261119`, baza `c3e1404`.
Naprawiać drugą ścieżkę tylko po potwierdzeniu problemu; bez nowego globalnego
potoku lub zarządzania anulowaniem. Frozen `c3e1404` i kanoniczna produkcja
pozostają nietknięte. Po odbiorze korekty zakończyć #25; #26 nadal następne.

### Korekta dostarczona — `5f0242f`, jeszcze bez integracji

Wykonawca dostarczył czysty `5f0242ff5e20c0c51999438349c4688407f9bf6d`
na bazie `c3e1404`. No-arg wrapper i rzeczywisty dyspozytor zachowują stary
pełny limit; jawna kadencja jest adaptacyjna, również jawne 0 nadal podlega
walidacji do co najmniej 1 s. Nowy fixture referencjonuje rzeczywisty LiteHost
z kontrolowanym transportem, a nie zastępczy dyspozytor.

Zapis znacznika próby jest teraz transakcją `TryMarkRefreshAttempt` z kontrolą
tokenu po dostępie do SQLite i przed commitem. Wykonawca odtworzył RED przy
starej ścieżce, następnie GREEN: po anulowaniu pod blokadą `BEGIN EXCLUSIVE`
nie zostaje utrwalona próba odroczonego źródła. Dokładna granica admission
pozostaje opisana w `HUBERT25-SCHEDULE-CORRECTION-20261119-wdrozenie.md`:
to nie wspólna atomowa operacja SQLite/token ani gwarancja natychmiastowego
przerwania oczekiwania na SQLite. Schemat bazy nadal 4.

Rodzic odczytał pełny diff 12 plików korekty i raport, uzupełnił w stagingu
brakujący końcowy raport (nie kod), potwierdził obecność 3048 śledzonych plików
oraz bajtową zgodność wszystkich 13 zmienionych plików kodu/testów od `ac203b6`.
Uruchomiony przez rodzica rzeczywisty HandlerTests: exit 0,
„LiteHost automatic refresh dispatcher: OK” (no-arg 4, jawne 60/0 po 1).
Hash DLL `fe2f64ed235007beb37dd382ede23b968c063385e6746e351ddec82dd5507a92`.
Wybrany pytest kontraktu wrappera legacy/jawna kadencja/settings: 1 PASS.
Kwity: staging `hubert25-schedule-correction-20261119/.amc-parent-acceptance/handler-parent.json`.

Wykonawca zgłosił 62 Python PASS, Core 9 PASS, prawdziwy LiteHost build 0/0
oraz dwa końcowe Protocol PASS. **Nie oznacza to zamknięcia odbioru:** podczas
wcześniejszego uruchomienia test 25 ms limitu automatu raz upadł. Przyczyna
nie może zostać uznana za niepowiązaną na podstawie dwóch późniejszych PASS.
Równolegle trwają: ograniczony niezależny review delty `5f0242f` oraz wyłącznie
naprawa aparatury tego testu w `codex/amc-hubert25-deadline-test-20261119`
(baza `5f0242f`). Oczekiwany kontrolowany timer ma sprawdzać anulowanie po
przyjęciu pierwszego źródła, bez zależności od zimnego JIT/tempa CPU i bez
zmiany produkcji. Nie zlecono ponownej szerokiej suite ani budowy produktu.

## Przygotowawczy przegląd kolejnych etapów — bez zmiany kolejności

Po zamknięciu #25 rozpoczynamy [wykonanie #26](HUBERT26-CHAPTERS-20261119-plan.md).
Świeży odczyt GitHub potwierdza niezmienione głowy PR #27 `d46e4c8` i #29
`aedb5f0`: OPEN, mergedAt=null, brak checków (nie zielone CI). Przeczytano
ponownie pełny opis i komentarz #26 oraz pełny opis PR #27.

Przegląd #27/#29 zakończony: przed integracją rozdziałów trzeba odróżnić awarię
yt-dlp/uszkodzony JSON od prawidłowego braku rozdziałów i ograniczyć stdout
podczas odczytu. Przy wznowieniu trzeba funkcjonalnie sprawdzić tożsamość
zachowywanego pipeline; dopasowanie tylko po Source jest dziedziczone sprzed
PR, nie dowiedzioną nową regresją. [Odrębny przegląd i granice dowodu](HUBERT-20261119-pr27-pr29-review.md).
To nie blokuje pierwszego etapu #25 i nie oznacza gotowego wdrożenia kolejnych.

## Zakres i poziom dowodu

Analiza jest wyłącznie odczytowa. Przeczytano pełne body i wszystkie komentarze Issues #11, #25, #26, #28 oraz opisy i pliki PR #27/#29. Nie pobierano załączników ani Gistów Huberta, nie otwierano GUI, nie logowano do Spotify/YouTube, nie zmieniano kodu AMC, zgłoszeń ani wydań.

**Rozróżnienie źródeł:**

- **Relacja Huberta** jest wartościowym pomiarem na jego koncie/komputerze, ale nie jest samodzielnym dowodem zachowania aktualnego kodu AMC.
- **Potwierdzenie w kodzie** dotyczy checkoutu `/home/michal/projekty/AMC`, lokalny `HEAD` `90d766a`; trzy cudze, niezacommitowane pliki dokumentacji pozostawiono bez zmian.
- **Wydanie** oznacza opublikowany tag/release, nie automatycznie test na komputerze Michała ani test NVDA.

Świeżo odczytane wydania GitHub są opublikowane i mają ten sam `targetCommitish` `1723786c4557ef68bb3922189a028099ce96db31`:

| Wydanie | Status | Dowód |
|---|---|---|
| `test-2026.11.18.0337` / AMC Test 18.11.2026 03:37 | opublikowane prerelease, 3 assety | GitHub Release metadata, odczyt 19.11 |
| `public-2026.11.18.0329` / AMC Public 18.11.2026 03:29 | opublikowane stable, 3 assety | GitHub Release metadata, odczyt 19.11 |

Kod `LiteSpotifyChromeOutput.cs` i `LiteSpotifySoloistOutput.cs` jest identyczny między tym committem wydania a analizowanym checkoutem. PR #27 (`d46e4c8`) i #29 (`aedb5f0`) są nadal **OPEN, nie merged**, oba mają jako rodzica `1723786c`; ich commitów nie zawiera ani checkout, ani opublikowane wydania. Nie wolno więc opisywać #26 ani #28 jako wdrożonych.

## Decyzje w skrócie

| Temat | Stan | Decyzja |
|---|---|---|
| #11: odrzucenie klucza audio Librespot | problem upstreamowy nieusuwalny samą aktualizacją Librespot; istnieje opublikowany fallback Chrome oraz opcjonalny Soloist | **pozostawić Librespot jako stary fallback, nie próbować go „naprawić”; dopracować komunikat tylko jeśli nadal myli** |
| #11: Spotify — tempo podcastów | CDP ustawia i ponawia `playbackRate`, lecz zwykłe GUI celowo blokuje zmianę tempa Spotify; Soloist jawnie jej nie oferuje | **nie włączać funkcji użytkowej bez ograniczonego eksperymentu i testu automatycznego** |
| #11: Soloist, mDNS/parowanie | konfiguracja, nazwa urządzenia, wygasanie i stan są częściowo obsłużone; brak mechanizmu naprawy/ogłaszania mDNS i brak parowania w AMC | **nie implementować mDNS ani automatycznego parowania teraz; poprawić instrukcję/status tylko po potwierdzeniu potrzeby** |
| #25: YouTube refresh i zalew „nowych” | kod potwierdza zasadnicze mechanizmy zgłoszenia; dotyczy integralności biblioteki | **najwyższy priorytet naprawczy** |
| #26: rozdziały YouTube | trafna przyczyna; gotowy, niezmieniony PR #27 ma wąski zakres | **zrecenzować i scalić po odtworzeniu testów na Windows; potem wydać** |
| #28: przerwy po pauzie przy 2x | trafna, wąska poprawka jest w PR #29, ale nie jest w produkcie | **zrecenzować i scalić po testach Windows; osobno rozważyć bufor/range** |

## #11 — Spotify, Librespot, Chrome i Soloist

### Co zostało już wdrożone i wydane

1. **Tor Chrome/Web Playback SDK jest rzeczywistym fallbackiem**, a nie wyłącznie planem. `LiteSpotifyPlaybackCoordinator.EnsureOutputLocked` wybiera gotowy Soloist, a w przeciwnym razie `LiteSpotifyChromeOutput` (`LiteSpotifyPlaybackCoordinator.cs:417–432`). To odpowiada raportowi Huberta z 08.11, że katalog i odtwarzanie Chrome działały na jego koncie, ale nie zastępuje testu bieżącego wydania na innym koncie.
2. **Wybór fizycznego wyjścia Chrome przez CDP jest w kodzie.** `ApplyOutputAsync` wyszukuje `audiooutput`, wywołuje `setSinkId` na znalezionych `HTMLMediaElement` i sprawdza wynik (`LiteSpotifyChromeOutput.cs:374–405`). To jest dowód implementacji, nie dowód aktualnego odsłuchu na Denonie.
3. **Soloist jest opcjonalnym, lokalnym adapterem.** Python uruchamia wybrany przez użytkownika `soloist.exe` z `--device-name` równym nazwie profilu i z lokalnym WebSocketem; klucz pozostaje w Windows Credential Manager (`wxlite/amc_wx_lite/spotify_soloist.py:155–188`). Profil Test nadaje nazwę `AMC Test`, Public — `AMC Public` (`release_profile.py:79–124`). Host wybiera adapter tylko, gdy plik konfiguracji, ścieżka i `ctl now --json` są poprawne (`LiteSpotifySoloistOutput.cs:40–109`).
4. **Wygasanie binarki jest obsłużone na starcie.** Kod wyjścia `10` daje komunikat „Wersja Soloist wygasła. Pobierz nową ze Spotify.” (`spotify_soloist.py:179–188`). Aplikacja nie redystrybuuje pliku.
5. **Nazwa kontroli dla NVDA po stronie AMC jest obecna.** Przycisk konfiguracji ma `SetName("Konfiguruj odtwarzacz Spotify Soloist")`; dialog i jego pola/przyciski mają własne nazwy dostępności (`gui.py:4945–4946`, `spotify_soloist_dialog.py:21–105`). To nie może naprawić nienazwanego przycisku w oficjalnym UI Spotify opisanym przez Huberta; AMC może tylko uprzedzić użytkownika w instrukcji.

### Co nadal nie jest rozwiązane

- **Librespot:** #11 poprawnie rozdziela „utwór niedostępny” od zaobserwowanego przez Huberta `audio key error`. W tej analizie nie potwierdzono w aktywnej ścieżce Librespot kodu `audioKeyRefused`; nie należy twierdzić, że upstreamowa odmowa klucza została naprawiona. Chrome/Soloist są obejściami dla kont, nie naprawą Librespot.
- **Parowanie/mDNS na publicznym Wi‑Fi:** kod Soloist nie uruchamia `--pair`, nie zawiera `mDNS`, `zeroconf` ani ogłaszacza zastępczego. Opisana przez Huberta przeszkoda (sieć publiczna + WSL/adresy dodatkowe) pozostaje relacją środowiskową, nie przetestowaną właściwością AMC. Najbezpieczniej pozostawić ręczne parowanie w oficjalnej aplikacji Spotify i dodać krótką instrukcję diagnostyczną dopiero po zatwierdzeniu UX.
- **Wyjście audio Soloist:** adapter zwraca pustą listę urządzeń, a `TrySetOutputDeviceAsync` przyjmuje wyłącznie ustawienie domyślne (`LiteSpotifySoloistOutput.cs:207–211`). `Shift+A` nie ma więc parytetu z Chrome dla Soloist. Raport Huberta o systemowym przypisaniu procesu do endpointu jest hipotezą integracyjną, nie funkcją AMC. Nie dodawać nieudokumentowanego AudioPolicyConfig ani restartów procesu bez osobnego, mierzalnego zadania.
- **Jakość:** kod prosi Soloist o najwyższą warstwę w `prefs`, ale nie odczytuje faktycznego kodeka/FLAC. Nie wolno obiecywać Lossless/FLAC użytkownikowi na podstawie tej prośby ani pomiaru jednego konta Huberta.

### Spotify: tempo podcastów — odpowiedź dokładna

**Czy `playbackRate` jest wykonywane przez CDP? Tak, w Chrome.** `SetMediaRateAsync` ustawia `e.playbackRate` dla znalezionych przez `Runtime.queryObjects(HTMLMediaElement.prototype)` elementów w ramce SDK (`LiteSpotifyChromeOutput.cs:324–328, 408–429`). Wartość jest ograniczana do 0,5–2,0.

**Czy jest ponawiane po zmianie utworu? Tak, pod konkretnym warunkiem.** Po otrzymaniu stanu, w którym odtwarzanie zostało pierwszy raz potwierdzone, `HandleEvent` emituje `PlaybackStarted` i uruchamia `ReapplyOutputAsync`; ta metoda po ewentualnym `setSinkId` wywołuje ponownie `SetMediaRateAsync`, gdy `rate != 1` (`LiteSpotifyChromeOutput.cs:716–735`). To pokrywa normalny nowy start odtwarzania, a nie daje dowodu dla każdego możliwego rekonektu/przeładowania ramki lub zmiany elementu bez nowego zdarzenia `PlaybackStarted`.

**Czy wywołuje to normalne GUI? Nie.** Protokół hosta ma `transport.setRate` → `LiteSpotifyPlaybackCoordinator.SetRate`, lecz GUI przy aktywnej sesji Spotify kończy `_set_rate` komunikatem „Spotify nie udostępnia trwałej zmiany prędkości w AMC” i nie wysyła żądania (`gui.py:35703–35727`). Test `test_spotify_rate_command_does_not_claim_transient_change` potwierdza brak wywołania klienta. Kontrolka tempa jest też wyłączona dla Spotify (`test_gui_wires_activity_views.py:453–465`). Zatem obecne CDP jest kodem wewnętrznym/protokolarnym, nie udostępnioną obsługą tempa podcastów.

**Soloist nie obsługuje tempa.** `SetPlaybackRate` odrzuca wszystko poza `1.0` z jawnym komunikatem (`LiteSpotifySoloistOutput.cs:200–205`), a koordynator blokuje żądanie zanim zmieni stan (`LiteSpotifyPlaybackCoordinator.cs:219–240`). Obserwacja Huberta dotycząca `options.playback_speed`/`position.speed` nie jest wystarczającym kontraktem, aby ominąć tę granicę.

### Granice dowodu i ograniczony eksperyment

Nie ma w aktualnych testach bezpośredniego testu `LiteSpotifyChromeOutput` ani sztucznego CDP potwierdzającego ustawienie tempa po odtworzeniu elementu. Przejście 46 testów Python nie dowodzi tego zachowania. Nie uruchamiano Chrome, Spotify, CDP ani NVDA.

**Zalecany eksperyment — osobne, małe zadanie, bez włączania funkcji w GUI:**

Samo `return true` skryptu `SetMediaRateAsync` nie potwierdza znalezienia elementu ani zastosowania tempa. Kryterium eksperymentu musi sprawdzić znalezione elementy i rzeczywistą wartość oraz zachowanie dźwięku, nie tylko brak wyjątku CDP.

1. Najpierw dodać test z fałszywą warstwą CDP dla trzech przypadków: żądanie 1,25×, potwierdzony nowy start z nowym elementem i ponowne połączenie/utrata elementu. Test ma sprawdzić wywołaną wartość, liczbę ponowień i brak próby dla `1,0×`.
2. Dopiero po tej bramce, za jawną zgodą właściciela konta, wykonać pojedynczą próbę na **podcaście** i izolowanym profilu Chrome: 1,25× → zmiana odcinka → symulowany reconnect. Kryteria: odczyt `HTMLMediaElement.playbackRate`, rosnąca pozycja i odsłuch, a po zmianie utworu ponowne ustawienie. Nie logować tokenu ani danych konta.
3. Jeśli trzy przypadki przejdą, podjąć osobną decyzję produktową: tempo pokazywać tylko dla aktywnego backendu Chrome, wprost jako eksperymentalne i nietrwałe; Soloist nadal pozostaje wyłączony. W przeciwnym razie zachować obecną blokadę GUI.

## #25 — opóźnione kanały, utrata wyników, zalew „nowych”

To jest **najwyższy priorytet**: dotyczy regularności odświeżania i ryzyka błędnego stanu biblioteki, a nie pojedynczej funkcji UI.

### Co potwierdza aktualny kod

- Rozmiar automatycznej porcji jest stałą konfiguracją `automatic_refresh_batch_size`; wybierane są najstarsze należne źródła, maksymalnie `batchSize` (`PodcastLibraryDatabase.cs:515–565`). Nie ma doboru porcji od liczby kanałów, czasu przebiegu ani interwału kanału.
- Pierwsze automatyczne odświeżenie po uruchomieniu jest planowane na `teraz + cały interwał` (`gui.py:11019–11024`), nie jako szybki catch-up.
- Timer wprost nie uruchamia pracy, gdy trwa nagrywanie; także tuż przed wywołaniem hosta ponownie sprawdza nagrywanie (`gui.py:35975–36047`). To potwierdza ważną część relacji Huberta.
- `MarkRefreshAttempt` jest wykonywane **przed** `_fetch` dla każdego celu automatycznego (`LitePodcastRefreshCoordinator.cs:137–148`), a `GetDueRefreshTargets` uwzględnia ten zapis jako ostatnią aktywność (`PodcastLibraryDatabase.cs:542–565`). Tym samym nieudane/przerwane źródło nie wraca od razu; istnieją testy sprawiedliwości i trwałości tej polityki (`YouTubeSubscriptionSyncApplyTests.cs:220–262`, `PodcastProgressStoreTests.cs:461–523`).
- Dla dokumentów bez rozdziałów wyniki są odkładane do `pendingBatch` i zapisywane dopiero po zakończeniu pętli (`LitePodcastRefreshCoordinator.cs:116–191`). Przerwanie procesu między pobraniem a `ApplyRefreshBatch` rzeczywiście może zgubić już pobrane wyniki, podczas gdy próba była oznaczona. Nie każdy feed ma ten sam przebieg: dokument z rozdziałami jest zapisany od razu.
- `YouTubeCollectionClient` scala strumienie z filmami, sortuje po obecności/dacie publikacji i robi `.Take(MaximumItems)` **przed** `CompleteFromChannelFeed`, które dopiero nakłada daty RSS (`YouTubeCollectionClient.cs:96–130, 495–548, 550–588`). `MaximumItems` wynosi 100. To potwierdza mechanizm opisany w punktach 4–5 #25.

Nie potwierdzono tu konkretnych liczb Huberta (122 kanały, 2 330 pozycji, 7,8 s); są to jego pomiary, nie reprodukcja w tym środowisku. Aktualne testy kolekcji sprawdzają normalizację, scalanie i proste uzupełnienie dat, ale nie przypadek ponad 100 pozycji, w którym zwykłe filmy bez dat wypadają przed uzupełnieniem RSS (`Windows.SmokeTests/Program.cs:2863–3162`).

### Co naprawiać — kolejność

1. **P0: integralność listy i flaga „nowe”.** Najpierw zmienić kolejność tak, aby daty RSS były nałożone przed sortowaniem/obcięciem, a obcięcie nie wypierało zwykłych filmów przez zakończone transmisje. Dodać regresję: >100 pozycji, część początkowo bez dat, RSS zawiera daty zwykłych filmów i transmisje; sprawdzić obecność wszystkich oczekiwanych nowych filmów oraz brak fałszywego „nowe”. Przed jakąkolwiek migracją zalewu przygotować odwracalne, mierzalne kryterium (nie masowo wyczyścić flag bez historii źródła).
2. **P0: trwały zapis per źródło.** Zmienić atomowość: pobrany feed zapisać po sukcesie każdego kanału albo oznaczać próbę dopiero po bezpiecznym zakończeniu. Zachować ochronę przed gorącą pętlą nieudanych źródeł, lecz nie pozwalać, by zamknięcie aplikacji zniszczyło wynik i opóźniło następne podejście. Dodać test przerwania dokładnie po sukcesie kanału 1 z N i ponownego otwarcia bazy.
3. **P1: harmonogram.** Dodać szybki, ograniczony catch-up po starcie oraz wyliczanie docelowego budżetu porcji względem liczby kanałów/interwałów, z limitem czasu jednego przebiegu i telemetrią liczby pozostałych należnych źródeł. Nie zmieniać automatycznie istniejących interwałów źródeł bez świadomej migracji ustawień.
4. **P1: nagrywanie.** Na razie **pozostawić blokadę pełnego yt-dlp w trakcie nagrania** — chroni to priorytet nagrywania i zasoby. Po P0/P1 można ocenić osobno lekki RSS-only refresh z limitem połączeń; nie mieszać tego z korektą danych.
5. **P2: RSS-first.** To rozsądny kierunek wydajnościowy, ale osobny projekt: cache/ETag, kolejność RSS→yt-dlp, obsługa Shorts, fallback i pomiary przy dużej bibliotece. Nie powinien blokować P0.
6. **Pozostawić poza zakresem:** usługa działająca po zamknięciu AMC i nieoficjalne logowanie/kod „jak BlindRSS”. Pierwsze wymaga osobnego produktu/usługi; drugie nie usuwa przyczyn P0 i zmienia model danych/kont.

## #26 — rozdziały YouTube

**Ocena: naprawiać teraz przez PR #27, po weryfikacji kompilacji Windows.** Przyczyna zgłoszenia jest zgodna z architekturą: lista kanału używa płaskiej playlisty, a istniejący parser strony nie wyciąga opisu trzymanego przez YouTube w skrypcie.

PR #27 dodaje wąski parser wyniku `yt-dlp --print "%(chapters)j"`, ograniczenia rozmiaru/czasu oraz kieruje tylko adresy YouTube do tej ścieżki (`d46e4c8`, diff od `1723786c`). Dla braku rozdziałów zwracana jest pusta lista; błędy procesu i timeout mają pozostać odwracalne i nie być zapamiętane jako „brak” (`YouTubeSourceResolver.ReadChaptersAsync`, PR diff; `LitePodcastChapterCoordinator` rozróżnia `TimeoutException`). Zakres nie obejmuje Facebooka/Instagrama.

**Co jest dowodem, a co nie:** autor PR deklaruje build LiteHost/Windows, 37/37 ProtocolTests i próbę żywą 25 rozdziałów. W tym środowisku brak `dotnet` na PATH, więc tych deklaracji nie powtórzono; PR nadal nie jest merged ani wydany. Przed scaleniem należy uruchomić wskazane testy na Windows i dodać/utrzymać test: kanał YouTube bez opisu → yt-dlp, strona watch nie jest pobierana, `NA`, uszkodzony JSON, timeout i pojedynczy film Ctrl+N.

## #28 — pauza/wznowienie YouTube przy 2×

**Ocena: naprawiać przez PR #29; nie rozszerzać od razu o pobieranie range.** Mechanizm z #28 jest spójny z istniejącym torem: poprzednie wznowienie bezwarunkowo wywołuje `SeekPipeline`, które ustawia czas i czyści procesor tempa. PR #29 ogranicza zmianę do wznowienia tego samego, wstrzymanego pipeline'u:

- rezygnuje z `SeekPipeline` tylko, gdy pozycja jest w ±250 ms od pauzy, wyjście jest `Paused` i dla źródła sieciowego pauza trwa najwyżej 2 minuty;
- w pozostałych sytuacjach, zwłaszcza po długiej pauzie sieciowej lub przy rzeczywistym seek, pozostawia dawną ścieżkę (`ResumesInPlace`, PR #29 diff `WindowsMediaOutput.cs`).

To jest ostrożny zakres; nie zmienia zachowania kolejki/świadomego przewijania. Granica 2 minut jest założeniem autora, nie zmierzoną trwałością URL/połączenia YouTube. Autor deklaruje selektywny `Windows.SmokeTests --resume-in-place-only` i ProtocolTests, lecz testów nie zweryfikowano lokalnie z powodu braku `dotnet`; PR nie jest wydany. Po scaleniu potrzebny jest jeden test Windows na krótką pauzę i jeden na pauzę >2 min, plus odsłuch przy 2×. Lokalny proxy/range/download do cache należy zostawić jako odrębny projekt wydajnościowy po potwierdzeniu tej poprawki.

## Wykonane weryfikacje

- Odczytano pełne treści i komentarze: #11 (11 komentarzy), #25 (brak komentarzy), #26 (1), #28 (1), oraz metadane/pełne opisy PR #27/#29 i obu wydań.
- Sprawdzono stan PR i ancestry: #27/#29 nie są w `HEAD` ani w commitcie opublikowanych wydań.
- Uruchomiono bez sieci:

```text
PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest \
  wxlite/tests/test_spotify_soloist.py \
  wxlite/tests/test_spotify_source.py \
  wxlite/tests/test_spotify_navigation.py -q \
  --basetemp …/hubert-triage-pytest-verify -p no:cacheprovider

46 passed in 0.32s
```

Ten wynik obejmuje konfigurację/stan Soloist i część źródła/nawigacji Spotify. Nie jest dowodem Chrome/CDP, mDNS, fizycznego wyjścia, kodeka, aktualności Soloist ani NVDA na żywo.

- Próba uruchomienia celowanych testów .NET bez sieci nie wystartowała, bo w WSL `dotnet` nie jest zainstalowany/nie jest na PATH (`dotnet: command not found`). Niczego nie instalowano.

## Rekomendowana kolejność prac

1. **#25 P0**: korekta kolejności RSS/sort/limit i idempotentny zapis wyników per kanał, wraz z testami przerwania i >100 pozycji.
2. **#26 PR #27**: review + odtworzenie testów Windows + merge/release.
3. **#28 PR #29**: review + selektywne testy Windows + merge/release; osobna akceptacja odsłuchowa 2×.
4. **#11 komunikaty/Soloist**: małe zadanie UX tylko dla braków potwierdzonych po wydaniu; nie implementować mDNS/AudioPolicyConfig ani obietnic Lossless.
5. **Spotify tempo podcastów**: wyłącznie ograniczony eksperyment opisany wyżej; żadnej zmiany dostępnej dla użytkownika przed pomiarem.
6. **RSS-first/range cache**: osobne inicjatywy po zakończeniu napraw P0.

## Kontrola i wynik końcowy rodzica

Rodzic odczytał pełny raport i konkretne wyniki w transkrypcie delegacji: 46 testów PASS, próby C# niewykonane z powodu braku polecenia dotnet w WSL. Nie uznał tego za brak możliwości użycia istniejącego środowiska Windows ani funkcjonalną porażkę testów. Niezależnie odczytał aktualny stan PR #27/#29: OPEN, mergedAt=null, pełne #25/#26/#28 i ich komentarze; potwierdził kod `_set_rate`, `SetMediaRateAsync`, `HandleEvent`, `ReapplyOutputAsync` oraz buforowanie odświeżenia po oznaczeniu próby. Nie uruchamiał dodatkowych niezmienionych testów. Plan wdrożenia jest rekomendacją, nie automatycznym poleceniem scalania lub wydawania. Nie zmieniono kodu produktu AMC, nie zamknięto zgłoszeń, nie instalowano i nie logowano kont.

Oddzielna naprawa skrzynki w profilu AMC: plugin 0.2.1 aktywny bez restartu, krótki nagłówek „Hubert:”, globalny limit pozwalający na przełączanie tras Telegrama; 31 testów PASS i rzeczywista wysyłka do właściciela z odczytem dokładnej treści przez forwardMessage. To nie jest nowy przychodzący test z konta Huberta. Po całkowitej awarii nie ma automatycznego retry; stan failed jest jawny. Dowód w profilu: state/hubert-inbox-live-check.json.
