# Audio: potwierdzony wyścig spóźnionego startu i dalsza minimalna poprawka

## Wznowienie na nowe polecenie Michała — 20.11.2026

Michał wyjaśnił, że można kontynuować pracę przez Codex niemal do końca
pozostałego limitu konta. To nowa zgoda na dalsze wykonanie po wcześniejszym
punkcie STOP, nie dowód odczytania stanu limitu konta. Pierwotny limit dwóch
prób był limitem procesu naprawy, nie potwierdzonym wyczerpaniem Codexa.
Nie używać osobno płatnego API ani samoczynnego przełączenia dostawcy.

Kontynuacja dotyczy tej samej granicy lokalnego audio: najpierw kontrolowany
scenariusz naturalnego A→B z utratą wyjścia B przed jego późnym Started(B).
Wymusić rzeczywistą kolejność bez Sleep/retry i odczytać publiczny transport.status
oraz skuteczność jawnego wznowienia tej samej pozycji. Najpierw rozstrzygnąć,
czy zarzut review jest odtwarzalny; bez RED nie dodawać poprawki na zapas.
Jeżeli RED potwierdzi defekt, minimalna zmiana właściwej ważności/porządku stanu,
GREEN wraz z sąsiednimi już istniejącymi testami i proporcjonalny niezależny review.
Nie budować kolejnych wyjątków po samym ID, jeśli nie odróżniają nowszej pauzy.

Wykonanie w osobnym worktree `amc-output-queue-fallback-20261120`, baza dokładnie
`8941e341ee23d65045032daead99cb7b35a65d0c`. Zamrożony poprzedni kandydat i jego
raporty pozostają nietknięte. Bez GUI/NVDA, sprzętu, kont, instalacji lub publikacji
w tym kroku. Integracja do canonical dopiero po odbiorze; Spotify i nagrania osobno.
Poniższy werdykt NIE nadal obowiązuje dla starego kandydata, lecz jego zakaz
kontynuowania został zastąpiony tą nową decyzją użytkownika.

## Werdykt rodzica — 20.11.2026, po zakończeniu drugiego cyklu

**NIE do integracji `8941e341ee23d65045032daead99cb7b35a65d0c`.
Drugi i ostatni uzgodniony cykl zakończony. STOP dalszych poprawek w tym limicie.**

### Zakończony RED/GREEN i własny odbiór rodzica

Codex `deleg_c4e52a18` zakończył cykl na bazie `7b0a60b`. Dodał testy
publicznych handlerów dla naturalnego A→B, Stop z późnym startem A i next po
pauzie z jawnym resume/odmową końca. Zapisany RED produktu dotyczy **naturalnego
A→B** (`transport.status` błędne playing; build exit 0, test exit 1), nie osobno
wszystkich nowych scenariuszy. Wcześniejszy błąd koperty payload/data był
aparaturowy, nie RED produktu. Nie osłabiono pierwotnej asercji late-fallback.

Minimalna delta poprawiła konkretne braki pierwszego latch: start nowej B przed
synchronizacją hosta, odblokowanie po naturalnym przejściu oraz blokadę queue Stop.
LiteHost i HandlerTests Release: buildy exit 0, 0 ostrzeżeń i 0 błędów; zwykły
oraz `--late-initial-playback-started` exit 0. To zachowane wyniki wykonawcy.

Rodzic porównał **749 plików źródłowych/projektowych** stagingu z dokładnym
`8941e34`: zero braków i różnic. Sprawdził hasze surowych logów RED/GREEN
wykonawcy i DLL `053762311c2680acf28795208f28f27584c2fdb0ae19c562a4e325cc6c346da2`.
Sam uruchomił tę DLL z `--late-initial-playback-started`, obejmując także trzy
nowe scenariusze kolejki: **exit 0 po 2,147 s**, komunikat OK, stderr pusty.
Nie powtarzano całej macierzy ani kompilacji niezmienionych konsumentów.
Początkowy odczyt logu RED weryfikował niedostępny w CP852 znak strzałki;
naprawiono tylko porównanie aparatury przed uruchomieniem testu produktu.

Dowody rodzica: scratch profilu AMC `amc-output-late-start-parent-20261120/`,
`verify_c2.py`, `c2-source-verification.json`, `c2-parent-result.json`, raw stdout/stderr.
Dowody wykonawcy: `amc-output-late-start-c2-evidence-20261120/evidence-c2/`,
`red-product-result.json`, `green-result.json` i powiązane logi bajtowe.

### Niezależny review: niezamknięta granica kolejka × fallback

`deleg_edbfee20`, `sa-0-cfeb8f01`, zwrócił **NIE**. Wskazał konkretny
**statyczny przeplot**, bez nowego wykonanego RED:

1. Po końcu A Core rozpoczyna B; kolejka może już mieć B, a host nadal A.
2. Fallback B nakłada nowszą ochronną pauzę i ustawia latch hosta.
3. Późny Started(B) rozpoznaje `switchedToQueueCurrent` i pomija tę blokadę,
   ustawiając playing=true, paused=false i zdejmując latch bez jawnego resume.

Rodzic odczytał hostowe callbacki i końcowe odblokowanie w PlaybackEnded,
`Protocol/LiteQueueCoordinator.HandlePlaybackEnded/OwnsCurrent`,
`DemoMediaSession.ContinueAfterPlaybackEnded` i niezależne publikacje zdarzeń
WindowsMediaOutput. Sam identyfikator pozycji nie dowodzi pierwszeństwa startu
względem nowszej pauzy. Testy badają kolejkę i direct fallback oddzielnie;
**brakuje kontrolowanego scenariusza łączonego A→B + fallback B przed późnym
Started(B)**. Bez niego nie przyjmować całości na podstawie istniejącego GREEN.

To dowód braku bezpiecznej granicy stanu w odczycie kodu, **nie pomiar
automatycznego dźwięku na głośnikach**. WindowsMediaOutput ma osobny
`_pauseRequested` i guard; błędny stan hosta nie dowodzi, że fizyczne wyjście
samoczynnie zagrało. Może natomiast błędnie opisywać pauzę i decyzję następnej
Spacji. Nie nazywać tego uruchomionym reproduktorem ani zmierzonym wyciekiem audio.

### Stan i punkt zatrzymania

Kandydat zachowany osobno, worktree czysty; niczego nie scalono ani nie pushowano.
Kanoniczne `src/tests` nadal zgodne z `8dd7278` po rewercie `8d1da71`, #26
zachowany, Spotify `7a26f3a` osobno. Nie było GUI/NVDA, fizycznego audio,
kont, Chrome/Spotify, instalacji i publikacji. Limit dwóch cykli został
wykorzystany; nie rozpoczynać trzeciego ani nowego refaktoru pod inną nazwą.
Ewentualne wznowienie wymaga osobnej decyzji co do małego testu łączonego i
kontrolowanego rozróżnienia aktualnego startu od nowszej ochrony. Nagrania mają
odrębny plan; inspiracja DirectCut RS to zapisany scenariusz na później.

## Historia pierwszego review i zlecenia drugiego cyklu

Poniższy opis jest stanem sprzed zakończenia `deleg_c4e52a18`; nowszy werdykt
powyżej go zastępuje. `7b0a60b` już wtedy miał NIE do integracji.

`deleg_9a50915b`, `sa-0-09aa1278`, zakończył niezależny review zamrożonego `7b0a60b`.
Prześledził host, LiteQueueCoordinator, DemoMediaSession i WindowsMediaOutput.
Rodzic sprawdził wskazane bloki kodu i brak resetów flagi. Ustalenia statyczne:

- Naturalny `PlaybackEnded(A)` blokuje flagę i gasi `_filesPlaying`, a następnie
  `HandlePlaybackEnded → ContinueAfterPlaybackEnded → Play(B)` uruchamia B.
  Host po przejściu nie odblokowuje flagi; `PlaybackStarted(B)` nie ustawia
  playing=true. Sam zielony bezpośredni `files.play` tego nie sprawdza.
- `StopFilesPlayback` w gałęzi kolejki zatrzymuje silnik i gasi playing, lecz
  nie blokuje późnego PlaybackStarted. Bezpośredni Stop miał tę blokadę.
- `QueueRelative` odblokowuje flagę po PlayRelative i SyncCurrentFromQueue.
  To za późna granica dla startu B dostarczonego przed powrotem PlayRelative,
  gdy poprzedni stan był zablokowany. Odmowa ruchu nie może przypadkiem
  odblokować starego materiału.

**Nowych scenariuszy RED jeszcze nie wykonano.** Reviewer ponownie uruchomił
tylko istniejący bezpośredni late-start GREEN. Powyższe są konkretnymi brakami
call graph/stanu, nie twierdzeniem o zmierzonym dźwięku lub wykonanym nowym RED.

Codex `deleg_c4e52a18`, `sa-0-b81cfb83`, pracuje w izolowanym worktree
`amc-output-late-start-c2-20261120`, gałąź
`codex/amc-output-late-start-c2-20261120`, baza `7b0a60b`. To **drugi i ostatni
cykl**, nie wyzerowanie poprzedniego limitu. Najpierw kontrolowane testy
publicznych handlerów queue A→B i queue Stop + późny Started; dopiero po RED
minimalna korekta. Zachować pierwotny late-fallback GREEN i zdrowe starty/ruch
kolejki, bez osłabiania asercji. Jeśli ten cykl nie wystarczy, zakończyć blokadą.
Kanonicznych źródeł, Spotify, sprzętu, GUI i instalacji nadal nie ruszano.

## Wynik pierwszej minimalnej poprawki — historia kandydata `7b0a60b`

Codex `deleg_de2324a4` zakończył jeden celowany cykl commitem
`7b0a60b8566fc68673969505e955658dd499c0cf`. Produkcyjnie zmienił tylko
`LiteEngineHandlers.cs`: nowa flaga `_filesStartStateLocked` blokuje zmianę
stanu przez spóźniony start po nowszej decyzji pauzy, fallbacku, końca lub Stop;
start i wznowienie mają ją odblokować. Nie zmieniono czerwonej sondy/asercji.

Rodzic przeczytał diff, raport i pełny `result.json` wykonawcy. Ten zapis
potwierdza exit 0 dla restore/build Release hosta, normalnego i późnego trybu
HandlerTests, buildów Windows i smoke oraz `--protected-output-recovery-only`.
Buildy mają 0 ostrzeżeń i 0 błędów. Sąsiednie testy ochrony/pauzy/Stop dotyczą
**silnika WindowsMediaOutput**, nie są pełnym pokryciem nowej flagi hosta.

**Własny odbiór krytycznego scenariusza rodzica, 20.11 około 00:22 CET:**
porównanie 749 źródeł/projektów stagingu z dokładnym `7b0a60b`, zero różnic;
sprawdzenie SHA DLL
`dc8edf8270b1ae20c3190f55446a5250663d0fee9d7a09247467d17e7bc14ce7`;
faktyczny run `--late-initial-playback-started` — **exit 0 po 1,847 s**,
`LiteHost automatic refresh dispatcher and protected output transport: OK`,
stderr pusty. Jest para RED/GREEN dla niezmienionego krytycznego scenariusza.
Nie powtarzano mechanicznie wszystkich testów wykonawcy.

Dowody rodzica w `amc-output-late-start-parent-20261120/`:
`green-source-verification.json`, `verify_green.py`, `green.json`, raw stdout/stderr.
Dowody wykonawcy: `amc-output-late-start-fix-20261120-evidence-c1/result.json`.

Niezależny review `deleg_9a50915b`, `sa-0-09aa1278`, bada tylko granice nowej
flagi: wszystkie starty hosta, automatyczny koniec/przejście kolejki, pauzę
użytkownika i Stop/nowy Play. Szczególnie czy blokada po końcu jest zdejmowana
przed zdrowym następnym startem; to **hipoteza do sprawdzenia**, nie wykryta
regresja. Bez nowych poprawek, szerokiego audytu ani powtarzania całej macierzy.
Przed werdyktem nie scalać kandydata do canonical. #26 i cofnięta wcześniejsza
integracja pozostają nietknięte; Spotify nadal osobno.

## Zakończona diagnoza przed poprawką

Codex `deleg_12ac386e` zakończył etap diagnostyczny commitem
`6c701411dfdcf923e64c68ff827104a0581c4641`. Zmieniono wyłącznie test hosta i
raport. Produkcji nie zmieniano. Rodzic przeczytał deltę, raport, zapisane
wyniki kontrolowanej sondy i odpowiednie produkcyjne callbacki.

## Przyczyna i granica wcześniejszego testu

Początkowy `IWavePlayer.Play` wykonuje się przed publikacją asynchronicznego
`WindowsMediaOutput.PlaybackStarted`. Poprzedni test czekał na wejście do Play,
nie na obsłużenie pełnego potwierdzenia startu przez host.

Kontrolowany przeplot:

1. Początkowy Play wchodzi w bramę testu, przed publikacją PlaybackStarted.
2. Utrata wyjścia tworzy fallback; host publikuje `audio.outputFallback`
   i ustawia `_filesPaused=true`. Nowe wyjście nie gra bez jawnego wznowienia.
3. Zwolniony początkowy Play publikuje spóźnione `playback.started`.
   Handler startu dla tego samego materiału bezwarunkowo ustawia
   `_filesPlaying=true`, `_filesPaused=false`.
4. Drugie publiczne `transport.pauseResume` odczytuje false i ponownie
   pauzuje zamiast wznowić tę samą pozycję na gotowym fallbacku.

To wada produkcyjnego kontraktu zdarzeń/stanu, nie wyłącznie testowego licznika
outputów. Samo oczekiwanie dłużej w teście ukryłoby ten przeplot zamiast go
naprawić. Sam filtr ID materiału nie odróżnia spóźnionego startu tej samej pozycji.

## Dowody wykonania

Wykonawca przeprowadził małą macierz źródeł `3948aec`/`aef4626` i konfiguracji
Debug/Release: wszystkie cztery zwykłe przebiegi przeszły. Nie unieważnia to
wcześniejszego RED rodzica. Wyklucza stałe przypisanie defektu kompilacji Release
lub integracji #26; sam PASS nie był tu dowodem naprawy.

Następnie ta sama nowa DLL sondy przeszła normalną kolejność (exit 0), a
wymuszona późna kolejność dała exit 1 na asercji wznowienia, bez Sleep i retry.
Rodzic przeczytał `probe.json`, porównał 749 plików `.cs/.csproj/.props/.targets`
stagingu z `6c70141` — zero braków i różnic bajtowych — i zweryfikował SHA DLL:

`ac9601725d720772820a394cb233efc7121d0ea6640481d998fee902f0e0f119`.

**Własne wykonanie rodzica, 20.11 około 00:06 CET:** rzeczywisty Windows
HandlerTests z `--late-initial-playback-started` zakończył się exit 1 po 1,753 s.
Dokładnie zachowana asercja:
`transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku`,
`Program.cs:142`. To oczekiwany, niezależnie potwierdzony RED przed poprawką.
Nie wykonywano ponownie całej macierzy ani szeregu losowych powtórzeń.

Dowody rodzica: profil AMC, scratch `amc-output-late-start-parent-20261120/`,
`red.json`, raw stdout/stderr i `reproduce.py`.

Faktyczne położenie dowodów wykonawcy po przeniesieniu:
`cache/scratch/amc-output-investigation2-evidence-20261119/`, podkatalogi
`diagnostic-evidence-output-matrix-20261119/` i
`diagnostic-evidence-late-start-probe-20261119/`. Starszy raport w zamrożonym
commicie wskazuje dawne położenie wewnątrz worktree; nie używać go jako
aktualnego adresu plików dowodowych.

## Zakres pierwszej minimalnej poprawki — implementacja zakończona, review NIE

Codex `deleg_de2324a4`, `sa-0-fcabf63e`, zakończył implementację w osobnym worktree
`amc-output-late-start-fix-20261120`, gałąź
`codex/amc-output-late-start-fix-20261120`, baza `6c70141`.

Zachować czerwony scenariusz i naprawić właściwy produkcyjny porządek/ważność
zdarzenia lub stan, nie wyciąć test. Zielony wynik wymagany dla zwykłej i późnej
kolejności w świeżym Release; publiczne wznowienie ma uruchomić ten sam materiał
bez trzeciego outputu/dekodera. Sąsiednio zachować pauzę użytkownika, ochronę
wyłączoną, Stop/nowy Play i spóźnione callbacki. Build rzeczywistych konsumentów
LiteHost i Windows. Maksymalnie dwa celowane cykle, bez retry-until-green,
architektonicznego refaktoru i dołączania innych zadań.

Nie scalać osobnej polityki Spotify `7a26f3a` w tej poprawce. Kanoniczny checkout
zachowuje cofniętą próbę integracji i odebrany #26 do czasu odebrania nowej delty.
Nie wykonywano GUI/NVDA, odsłuchu, fizycznego Denona, kont, sieci usług,
instalacji, push ani publikacji. Odrębne zadanie zapisu/wycinania nagrań pozostaje
następne i nie jest przez tę diagnozę rozwiązane.

## Materiał dla użytkownika

Znaleziono przyczynę martwego wznowienia: spóźnione potwierdzenie startu
nadpisuje ochronny stan. Ostatnia poprawka przechodzi test tego przypadku
oraz zwykłego końca, zatrzymania i ruchu kolejki, także w próbie rodzica.
Kontrola kodu ujawniła jednak brak rozróżnienia nowszej ochronnej pauzy podczas
przejścia do kolejnego materiału. Tego połączonego przypadku jeszcze nie
odtworzono testem. Poprawka nie została przyjęta; prace nad nią zatrzymano po
dwóch uzgodnionych cyklach. Nie ma nowej wersji do instalacji ani zmierzonego
samoczynnego dźwięku. Nagrania i przyszły scenariusz lekkiego cięcia są osobno.
