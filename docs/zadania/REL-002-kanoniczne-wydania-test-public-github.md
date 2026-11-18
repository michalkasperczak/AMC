# REL-002 — kanoniczne wydania AMC Test i AMC Public na GitHubie

## Status

### Najnowsza decyzja: publikacja istniejących nowych instalatorów na GH

18.11.2026, po meldunku o sukcesie obu chronionych budów, braku nowej detekcji,
nierozstrzygniętym historycznym RCX oraz braku instalacji/żywego odbioru NVDA,
Michał polecił: **„To instaluj w sensie na GH”**. Zakres to publikacja nowych
Public/Test z `1723786c4557ef68bb3922189a028099ce96db31`, nie instalowanie
na jego komputerze ani uruchamianie lokalnych kandydatów. To nowa decyzja
publikacyjna po wskazaniu ograniczeń, nie werdykt Microsoftu i nie zaliczenie
odbioru. Nie zmieniać historycznej klasyfikacji RCX na fałszywy alarm.

Publikować wyłącznie istniejący artifact `14395074212` z runu `44229055217`:
bez kolejnego buildu, lokalnych dużych pobrań, instalacji i zmian Defendera.
Przeniesienie i statyczne sprawdzenie plików ma nastąpić na efemerycznym
runnerze Linux. Przed publikacją sprawdzić ZIP digest, dokładne EXE/sidecary,
sumy i rozmiary EXE, źródła oraz manifesty aktualizacji obu osobnych kanałów.
Public pozostaje stable, Test prerelease; tagi wskazują źródła kompilacji,
nie commit narzędzia publikacji. Nie nadpisywać istniejących wydań.
Opisy mają jawnie wskazywać brak próby instalacji/żywego NVDA i nadal otwartą
analizę historycznego alertu, obok pozytywnych wyników nowej budowy.

Przygotowanie zamkniętego jednorazowego publishera z małymi testami zlecono
przez openai-codex (`deleg_df9a76b1`), bez płatnego API i bez prawa workerowi
do pushu/publikacji. Rodzic odbierze wynik i uruchomi publikację po review.
Samo przygotowanie workflow nie oznacza publikacji.

### Chronologia po odbiorze źródeł #21

Michał polecił wydać obie wersje na GitHubie: **Public** oraz **Test z Apple
Music**. Nie tworzyć ani nie wznawiać lokalnego Windows/QEMU/Sandboxu.
Określenie prowadzącego „testowy Windows” było mylące: bieżące odczyty
dotyczyły zwykłego systemu Windows 11 Pro komputera HERMES (Lenovo), nie
nowej instalacji Windows. Nie działa QEMU; dawny katalog prób
`amc_sondy/applemusic-lifecycle` nie istnieje. Nie znaleziono tu obrazu do
usunięcia. Nie usuwać systemu gospodarza ani WSL, nie zatrzymywać procesów
`vmwp`/`vmmem` na podstawie ich nazw; ich obecność nie identyfikuje własnego
testowego Windows. Niczego nie usunięto.

Przygotowanie nowej budowy Public/Test i niezależny przegląd zakończono.
Uruchomiono jednorazowy chroniony workflow na GitHub-hosted runnerze:
**run `44221728197`, source `aca4bdb74348db574a8a9552dcb406196c1ece16`,
gałąź `codex/amc-protected-public-test-20261118-issue21`**. Odczyt API
potwierdził dokładny zdalny SHA. **Przebieg zakończył się `failure`**;
job i cleanup zakończone `2026-11-18T02:29:07Z`, bez instalatora ani publikacji.
Źródła, zamknięte wejścia, podpis i hash Inno oraz dodatek NVDA przeszły swoje
bramki. Składanie **Public** odrzucono przez `validate_profile_files`:
`copy_runtime()` skopiował z przypiętego starszego instalatora Test również
`runtime/Lib/site-packages/applemusic/...`; dla Public to niedopuszczalna
zawartość. Pominięcie dodatkowego kopiowania Apple Music nie usuwa plików
już obecnych w wejściowym runtime. Kontrola zawartości działała prawidłowo;
nie osłabiać jej, nie ukrywać fizycznych plików przez sam filtr manifestu.
Public installer oraz cały etap Test nie rozpoczęły się.
Końcowy rzeczywisty audyt Defendera: aktywna ochrona real-time/behavior/IOAV/
on-access, brak wykluczeń, detekcji i zdarzeń 1116/1117. To błąd przygotowania
zależności, **nie nowa detekcja malware**; nie rozstrzyga dawnych RCX.
Zachowano i odczytano 8 małych JSON z artifact `14392315332`, 3705 B ZIP,
SHA-256 archiwum `9b7348b12812e6f6ace23d3aa4bab5383e102d32180f19d2ff143b47a55886a2`;
lokalna suma jest zgodna z API. Dowody: profil AMC,
`state/github-public-test-44221728197`. Artefaktów binarnych brak; bez lokalnego
pobierania dużych plików, instalacji i zmian AV.
Naprawę z RED/GREEN odebrano niezależnie: profil Public fizycznie pomija
unikalne `applemusic`/`playwright`, Test je zachowuje, wejście nie zmienia się,
a współdzielone zależności oraz integralność pozostają. Bazowy builder
rzeczywiście odtworzył błąd; poprawiony zaliczył tę samą granicę. Worker
`0432e27` oraz parent `1723786` nie zmienili pięciu odebranych blobów #21.
W review poprawiono też pominięty dokładny ref r2 w ochronie (RED/GREEN);
6 kontraktów, parser PS i 5 wyizolowanych kontekstów guard PASS. YAML
naprawdę odczytano przez `ruamel.yaml`, bez instalowania narzędzi.

**Nowy kontrolowany run `44229055217`, source
`1723786c4557ef68bb3922189a028099ce96db31`, gałąź
`codex/amc-protected-public-test-20261118-issue21-r2`, attempt 1 —
`success`, completed.** GitHub API potwierdziło source SHA i attempt 1;
job `134549324896` wraz z cleanup zakończył się `2026-11-18T03:43:37Z`.
Nową gałąź wysłano raz; starego runu nie ponawiano. Readback potwierdził
SHA i trzy zmienione bloby produkcyjne; przejściowy błąd odczytu drzewa
nie spowodował dodatkowego pushu ani buildu.

**Oba kanoniczne instalatory powstały**, wersja `0.1.0-alpha.511`, z #21.
Public: bundle i installer exit 0, ISCC `/DProfil=public`, etykieta pliku
`2026-11-18-0329`. Test: bundle i installer exit 0, ISCC `/DProfil=test`,
etykieta `2026-11-18-0337`. Zachowane pełne argv kompilatora i state `exited`;
żadne argumenty nie pomijają bramek. Są to nowe wyjścia kanonicznych wrapperów,
nie przemianowany dawny diagnostyczny Test. Potwierdzono wszystkie 13 małych
raportów (12 wyników z exit 0 oraz rekord czystych źródeł).
Rzeczywisty końcowy Defender: AV/real-time/behavior/IOAV/on-access aktywne,
0 wykluczeń ścieżek/procesów/rozszerzeń, 0 detekcji i 0 zdarzeń 1116/1117.
To wynik nowego przebiegu, **nie werdykt o dawnych RCX**.

- Evidence artifact `14395482588`, 5995 B ZIP,
  SHA-256 `24f0f8ff72139f328d28e37a70ccd549eae24d865df790b0419c833cedc95682`.
  Lokalną sumę ponownie porównano z GitHub API; dowody w profilu AMC,
  `state/github-public-test-44229055217`.
- Binary artifact `14395074212`, 347586692 B ZIP,
  SHA-256 archiwum z GitHub API:
  `8c6a7d6daa5a621eb9231ea5204b6930006b55e9e535fdda4ec9191eeec1a5f3`.
  Workflow zachowuje oba EXE i ich `.integrity.json`. **Nie pobrano tego ZIP-a
  ani EXE lokalnie**; nie znamy jeszcze niezależnie odczytanych sum samych EXE
  ani pełnej zawartości payloadów. Suma archiwum nie jest sumą instalatora.

Monitor `proc_4ae581d9d30d` zakończony exit 0. Nie powtarzać budowy.
**Wydanie nadal wstrzymane**: historyczny alert RCX nie ma rozstrzygnięcia,
nowe instalatory nie przeszły instalacji ani żywego odbioru Windows/NVDA.
Nie publikowano release, nie uruchamiano kandydatów lokalnie, nie zmieniano
lokalnego AV i nie dotykano głównego PC. Odbiór nowej paczki nie może być
zastąpiony samym zielonym Actions ani dawnymi instalacjami alpha.511.
Po review pięć statycznych kontraktów przeszło; cztery scenariusze natywnego
PowerShell na atrapach potwierdziły odrzucenie wykluczeń, detekcji i błędów
odczytu oraz zachowanie dowodów kompilatora przy błędzie. Nie były to prawdziwe
skany. Bieżący przebieg wymaga rzeczywistej aktywnej ochrony.
Oba profile zawierają niezmienione pięć odebranych blobów #21, potwierdzonych
względem `ca9d1b23b804abdada8ffa833b1dc139705a95f9`, bez promowania starszych
artefaktów. Nowe wykrycie ma zatrzymać przebieg. Nie zmieniać lokalnego AV.
Hipoteza Michała o fałszywym alarmie nie jest potwierdzonym werdyktem;
klasyfikacja historycznych RCX pozostaje osobna od badań nowego kandydata.
Wymogi i rzeczywiste wyniki odbioru/publikacji należy opisać jawnie, bez
uznawania samego przygotowania workflow za gotowe wydanie.

Michał potwierdził użytkowo, że biblioteka Apple Music działa już dobrze:
wyszukiwanie i dodawanie utworów jest możliwe; **odtwarzanie nadal nie działa**.
To potwierdzenie użytkownika, nie nowy pomiar Hermesa. Dalsze poprawki
wyszukiwania zgłosi w osobnych zadaniach — nie rozszerzać tego wydania.

**Powrót do etapu wydania po #21 (18.11.2026):** źródła poprawki Folderów
Biblioteki zintegrowano i wysłano na GitHub (`ca9d1b23b804abdada8ffa833b1dc139705a95f9`).
Odczyt API potwierdził zdalny HEAD oraz pięć zmienionych blobów produkcyjnych.
Po integracji 6 celowanych testów Python przeszło; samodzielny zestaw Core
`--local-folder-library` zakończył się kodem 0. Szerszy, sąsiedni dobór dał
10 passed i 19 skipped z powodu brakującej historycznej kopii bazy.
Nie jest to odbiór NVDA ani finalnego instalatora. Issue #21 pozostaje otwarte.

Sprawdzono wymagania powrotu do publikacji: wcześniejszy hold bezpieczeństwa
pozostaje nierozstrzygnięty; nie ma potwierdzonego werdyktu Microsoftu.
Ponowny odczyt fizycznego C na Hermesie z 18.11.2026 01:19:28Z wskazał
553693184 B wolnego (poprzednie 23 MB nie opisuje już bieżącego stanu).
Nie przypisujemy zmiany wolnego miejsca naszym działaniom; inwentaryzacja
nie usuwała plików. To nadal nie zapewnia uzgodnionego miejsca na kontrolowany
odbiór paczki. Nie rozpoczęto nowej budowy, nie publikowano starego artefaktu
jako zawierającego #21, nie zmieniono AV i nie dotknięto głównego PC.
Kolejność dalszej pracy pozostaje zgodna ze zleceniem: po odblokowaniu
rzeczywistych bramek kontynuować wydanie na GitHubie, nie instalować u Michała.

W trakcie. **Publikacja kandydatów Public/Test `0.1.0-alpha.511` wstrzymana
17.11.2026 z powodu niewyjaśnionego alertu Defendera podczas budowy Test.**
Bundle z czystej rewizji `17e01e44b137b9dd3002444efcb8d17443ba956c` i prywatne
instalacje obu profili na Hermesie przeszły integralność/start. To nie jest pełny
odbiór wydania: Test powstał bezpośrednim wywołaniem ISCC po błędzie wrappera,
a Defender trzykrotnie oznaczył tymczasowe `RCX*.tmp` jako
`Worm:Win32/Mofksys!pz`. Skan końcowych EXE bez nowego wykrycia nie dowodzi
fałszywego alarmu. Nie publikować ani dalej uruchamiać kandydatów do wyjaśnienia
pochodzenia alertu i kanonicznej kompilacji. **Ta blokada nie zatrzymuje
rozwoju kodu, analizy błędów ani niezależnych testów z nieszkodliwymi fixture;
użytkownik 17.11 wymaga kontynuacji pracy przez Codex.** Nie wyłączać ochrony, nie dodawać
wyjątków, nie obchodzić blokady zmianą katalogu. Szczegółowe hashe, odczyty i
kryteria dalszego odbioru: `HUBERT-20261116-zgloszenia-i-wydanie.md`, sekcja
„Bieżąca blokada wydania”. Odczytowa diagnoza zakończona; niezależne podpisy
ECDSA czterech składników Inno są poprawne, ale nie rozstrzygają alertu
w plikach tymczasowych. Użytkownik zgodził się 17.11 na przekazanie próbek
Microsoftowi bez profili i danych logowania. Po potwierdzeniu użytkownika
standardowe RunAs dało uprawnienia administratora, ale kwarantanna jest pusta;
próby eksportu trzech konkretnych RCX nie dały plików. Odczytano hash jednego
RCX z MPLog. Logowanie oraz dodatkową zgodę OAuth zatwierdzono po osobnym
„Tak” użytkownika; portal rozpoznaje konto. Odczytano rekord dokładnie tego
jednego hasha (4427264 B) i jednokrotnie wywołano `Rescan File`. Odczyt karty
po odświeżeniu nie dał werdyktu ani potwierdzenia przyjęcia; historia pokazuje
0 wpisów, brak submission ID. Nie uznawać wywołania za potwierdzoną analizę.
Formularz programisty wymaga oryginalnego pliku; próbek nadal nie wysłano.
Dowód: `logs/microsoft-portal-status-20261117.json` pod rootem alpha.511.
Brak uprawnień ani logowanie nie są już aktualną blokadą. Porównanie dostępnych danych z najnowszymi wydaniami z 15.11
(Test 20:36 i Public 20:40, oba ze źródła
`2b6b061db22523ed9cfb66d26e2808b3d7bf00a7`) zakończono. Oryginalne pliki
pobrano i SHA potwierdzono; rodzina formatu Inno wspólna. Starego payloadu
nie wyodrębniono — oba dostępne czytniki nie obsłużyły formatu. Brak dowodu
przyczyny detekcji lub fałszywego alarmu. Żywy odbiór Windows/NVDA nadal otwarty; główny
komputer pozostaje nietykalny, użytkownik aktualizuje go ręcznie.

## Korekta zakresu 18.11.2026 — instalacja i wydanie w następnym etapie

Michał wyjaśnił: „Pisząc poprawiaj i Sprawdzaj chodziło mi o najnowsze
zadanie które Hubert przysłał do poprawek”, następnie: „Ale to też możesz
później zrobić w następnym etapie”. Poprzednia interpretacja tych słów jako
polecenia odbioru instalatora była błędna i jest zastąpiona tą korektą.
**Teraz zadanie #21: komunikaty Folderów Biblioteki i rozróżnianie identycznych
nazw folderów. Próba instalacji, odbiór binarny i wydanie odłożone.**
Nie zdejmować holda, nie uruchamiać gotowego EXE ani nowej budowy w ramach
naprawy #21. Gotowy artefakt `44197448182` nie zawiera przyszłej poprawki #21.

**Późniejsze doprecyzowanie tej samej rozmowy:** Michał polecił po skończeniu
naprawy wrócić nie do instalowania u niego, lecz do „wydania instalatora na
git Hab”. Kolejność: naprawa i weryfikacja #21 → powrót do pełnego wydania
instalatora na GitHubie, bez ponownego pytania o kontynuację. Poprzednie
„odłożone” określa kolejność, nie anulowanie tego następnego etapu.
Zgoda na wydanie nie znosi holda ani wymogu rzeczywistej próby instalacji
i celowanego odbioru na Hermesie. Nie instalować na głównym komputerze.
Jeżeli wydanie obejmuje #21, musi powstać z nowego zweryfikowanego commita;
nie publikować starszego artefaktu jako zawierającego tę poprawkę.

Przed pobraniem dużego pliku odczytano z GitHub tylko małe metadane ZIP
i sidecar (767 B transferu); EXE nie został jeszcze pobrany. Sidecar deklaruje
214564686 B i SHA-256
`5cfd7337f5fff8ff4033c20c6e8882cd116d2813bc26d8710a981e9e3a2c3d48`.
Te wartości trzeba porównać z faktycznie pobranymi bajtami przed uruchomieniem.
Profil `test`, bundle `ae2cbdf9`, oba sourceRevision wskazują `9739fe3`.

Bieżąca przeszkoda odbioru: fizyczny dysk Windows Hermesa ma 23560192 B
wolnego miejsca, brak D:. AMC/setup/ffmpeg nie działają; NVDA i mostek działają.
Inwentaryzację zakończono po doprecyzowaniu zakresu: wykonawca nie usunął,
nie zarchiwizował ani nie zmodyfikował plików Windows AMC; nie uruchomił AMC
ani instalatora. Rodzic odczytał raport i przeliczył jego 42 pliki AMC z
`C:\Temp`, `C:\tmp`, `C:\amctest`: łącznie 5619731 B, bez reparse points.
Odczyt raportu potwierdza pomiar FreeC 23486464 B z 18.11.2026 00:03:02Z;
nie jest to pomiar po odzyskaniu miejsca — żadnego miejsca nie odzyskano.
Nie znaleziono tu wystarczającego źródła bezpiecznego zwolnienia 3 GB.
Nie ruszać innych projektów, danych, nagrań ani profili i nie zmieniać ochrony.
Niedobór miejsca nie blokuje naprawy źródłowej #21 i nie uzasadnia
zastępowania jej dalszym sprzątaniem.

## Aktualizacja 18.11.2026 — instalator diagnostyczny z chronionej VM gotowy

Michał osobno zatwierdził włączenie ochrony wyłącznie na tymczasowej VM
GitHuba oraz budowę ze skanowaniem. Ta zgoda zastępuje poniższe historyczne
oczekiwanie na uzgodnienie chronionego środowiska; nie obejmuje zmiany
ochrony na Hermesie/głównym PC, instalacji ani publikacji wydania.

- Pierwsza chroniona próba `44195759246` rzeczywiście włączyła ochronę,
  usunęła domyślne wykluczenia i zaliczyła skan źródeł oraz kontrolę wejść.
  Zatrzymała ją nasza błędna lista oczekiwanych wydawców podpisu Inno,
  nie detekcja malware. Oficjalna dokumentacja JRSoftware wymaga
  `Pyrsys B.V.`; plik miał podpis `Valid` tego właśnie wydawcy.
- Poprawiono sprawdzenie na dokładny `SimpleName == 'Pyrsys B.V.'`;
  zachowano wymaganie ważnego podpisu i ten sam przypięty SHA-256.
  Siedem testów kontraktowych i parser dziewięciu bloków PowerShell PASS.
- Osobna jednorazowa gałąź `codex/amc-diagnostic-gh-protected-20261118-verified`,
  commit `9739fe346be503a9c1b755f03aa6983d81c74e7d`, run `44197448182`:
  **SUCCESS**. Build bundle i kanoniczny wrapper instalatora zakończyły się
  kodem 0; zachowano pełne argv ISCC i jego exit 0. Nie omijano integralności
  ani technicznych kontroli uruchomienia; dodane skany poprzedzają wykonanie.
- Potwierdzono real-time, behavior, IOAV, on-access, skanowanie skryptów
  i archiwów, brak wykluczeń oraz skany wymagane przez workflow. Końcowy
  audyt: aktywny Defender, silnik `1.1.26100.4`, sygnatury `1.463.347.0`,
  brak detekcji i zdarzeń 1116/1117 od początku joba.
- Powstał `AMC-Test-2026-11-17-2326.exe` (`0.1.0-alpha.511`). Etykieta
  pliku pochodzi z zegara UTC VM, a nie lokalnej daty w Polsce.
  Zachowany jako Actions artifact `14380677377`, 214074499 B archiwum,
  SHA-256 **archiwum ZIP**, nie samego EXE:
  `8bbdbef9397235b50b5e6652a98604053ea4de1a9c6389eed7e79d9bd8743526`.
- Raporty: artifact `14379838582`, 4355 B, SHA-256 ZIP
  `feba50573120917af5e56b4de453a9630e9fa5c9a53893018575f31087a925e2`.
  Pobrano i odczytano wszystkie 10 małych JSON do profilu AMC,
  `state/github-diagnostic-44197448182`; dużego instalatora nie pobierano
  na Hermesa. Nie było lokalnej kompilacji, instalacji ani zmiany Defendera.
- Job i cleanup zakończone (`2026-11-17T23:31:03Z`); standardowa
  GitHub-hosted VM podlega automatycznej likwidacji po jobie. Odczyt API:
  0 uruchomionych i 0 oczekujących przebiegów. Raportów nie usuwano.
- To nowy wynik diagnostycznej budowy, nie werdykt Microsoftu o dawnych
  RCX. **Historyczny hold alpha.511, brak release i brak odbioru instalacji
  oraz Windows/NVDA pozostają jawne.** Nie zamykano zgłoszeń #14/#17/#18/#19/#20
  na podstawie samego zielonego buildu. Nie ponawiać budowy ani dawnych
  fixtures bez nowej przyczyny. Kolejnym etapem jest rozstrzygnięcie dopuszczenia
  tego konkretnego kandydata do ograniczonego odbioru, nie kolejna kompilacja.

Dowód przebiegu: https://github.com/michalkasperczak/AMC/actions/runs/44197448182

## Diagnostyczna próba na maszynie GitHub — 17.11.2026 (historia)

Użytkownik zatwierdził jedną kontrolowaną budowę na GitHub-hosted Windows,
bez budowy i instalacji na Hermesie oraz bez instalacji na głównym komputerze.
Nie jest to zgoda na publikację ani uznanie wcześniejszych detekcji RCX za
fałszywe. Pozostałe bramki wydania nadal obowiązują.

- W pierwszej próbie uruchomiono workflow: run `44188078273`, attempt `1`,
  źródła `95001e494ac2e0ac3c5f0833a7cd4a63bd40775c`, gałąź
  `codex/amc-diagnostic-gh-20261117`, GitHub-hosted `windows-2025`.
- Przebieg zakończył się niepowodzeniem przed pobraniem runtime, kompilacją,
  pakowaniem i instalowaniem narzędzi. Przyczyną jest błąd naszego skryptu:
  polecenia CIM Defendera nie obsługują `-WarningAction Stop`. Nie odczytano
  stanu ochrony ani wyniku skanu; to NIE jest detekcja zagrożenia i NIE dowód,
  że Defender jest wyłączony lub niedostępny.
- Zachowano i odczytano trzy małe raporty z Actions artifact
  `14374373325`. Archiwum ma 1216 B i digest
  `b6f8fe0d0336b88f5cb98846fa96daeb25d350eb39293a796f563c99bcab9495`.
  Lokalne dowody: profil AMC, `state/github-diagnostic-44188078273`.
- Przygotowano lokalną poprawkę `0dc0ce8`: ostrzeżenia są przechwytywane przez
  `WarningVariable` z obsługiwanym `WarningAction Continue`, a każde rzeczywiste
  ostrzeżenie nadal powoduje błąd. Sześć celowanych kontraktów i parser ośmiu
  bloków PowerShell przeszły. Na tym etapie poprawka nie była jeszcze wykonana
  na VM; jej późniejszy rzeczywisty odbiór opisuje ponowienie poniżej.
- Nie powstał instalator, nie opublikowano wydania, nie uruchomiono AMC ani
  setupu, nie zmieniano ochrony. Na Hermesie wykonano jedynie małe zmiany
  źródeł, testy statyczne i parsowanie; nie było lokalnej kompilacji.
### Zatwierdzone ponowienie i zakończenie zasobów — 17.11.2026

Michał zatwierdził ponowienie słowami „Tak, sprawdzaj, poteuem usuń te maszyny”.
To zastępuje wcześniejsze oczekiwanie na zgodę. Nie rozszerza zakresu o wydanie,
instalację AMC, lokalną kompilację ani zmianę konfiguracji Defendera.

- Druga, osobno zatwierdzona próba: run `44191185213`, attempt `1`, job
  `134420706041`, gałąź `codex/amc-diagnostic-gh-20261117-r2`, dokładne źródła
  `999b31ded210d553604647e099c5b221f687b259`; sześć kontraktów statycznych PASS.
  Starej gałęzi nie kasowano, starego runu nie ponawiano. Oba joby są zakończone.
- Poprawiony odczyt CIM zadziałał. Faktyczna konfiguracja świeżej VM:
  `AntivirusEnabled=true`, `RealTimeProtectionEnabled=false`,
  `DisableRealtimeMonitoring=true`, `DisableBehaviorMonitoring=true`,
  `DisableIOAVProtection=true`, dwa wykluczenia ścieżek. Silnik `1.1.26100.4`,
  sygnatury `1.463.347.0`. To nie jest detekcja wirusa, tylko niespełniony
  warunek aktywnej ochrony. Początkowy skan źródeł nie został wykonany.
- Bramka prawidłowo przerwała pracę przed SDK, runtime, Inno, kompilacją oraz
  pakowaniem. Końcowy odczyt także potwierdził wyłączoną ochronę real-time;
  odczyt detekcji był pusty, co nie zastępuje niewykonanego skanu.
- Zachowano i odczytano trzy raporty; artifact `14376851571`, 1361 B, digest
  `sha256:bd88029c39beb1830da1edf2aa98338c35b525db3587dd4c2ca30d2c1bd25370`.
  Lokalna kopia: profil AMC, `state/github-diagnostic-44191185213`.
- Job zakończony 17.11 o `22:34:15Z`, łącznie 48 s, z zakończonymi etapami
  sprzątania. API potwierdza standardową grupę `GitHub Actions`, etykietę
  `windows-2025`; repozytorium nie ma zarejestrowanych self-hosted runnerów.
  Zgodnie z dokumentacją GitHuba standardowa VM jest automatycznie likwidowana
  po jobie. Nie istnieje należąca do nas trwała VM do ręcznego skasowania;
  nie deklarujemy wykonania operacji DELETE VM. Raporty pozostają zachowane.
- Oficjalny skrypt obrazu `actions/runner-images`,
  `images/windows/scripts/build/Configure-WindowsDefender.ps1`, jawnie ustawia
  wyłączoną ochronę real-time/behavior/IOAV oraz wykluczenia `C:\` i `D:\`.
  Nie należy zakładać, że kolejny standardowy obraz będzie chroniony.
- **Następny krok:** uzgodnić środowisko z aktywną ochroną na czas całej budowy.
  Nie usuwać bramki, nie zmieniać ochrony i nie uruchamiać następnych prób
  automatycznie. Wcześniejsze detekcje RCX i hold wydania nadal nierozstrzygnięte.
  Instalator, publikacja, instalacja i odbiór NVDA nie zostały wykonane.

Źródła zasad cyklu życia i konfiguracji VM:
- https://docs.github.com/actions/using-github-hosted-runners/using-github-hosted-runners
- https://github.com/actions/runner-images/blob/main/images/windows/scripts/build/Configure-WindowsDefender.ps1

Historyczny punkt poniższego planu z 13.11.2026: AMC Test
`0.1.0-alpha.500`, prerelease `test-2026.11.13.1952`, commit `16eb05b5`.
Nie traktować tego numeru jako świeżo odczytanego najnowszego wydania GitHub.

## Nadrzędna korekta użytkownika z 26.10.2026

Ta decyzja zastępuje wcześniejsze opisy, które traktowały kolejne numery
`alpha.4xx`, osobną publiczną linię albo stare repozytorium WPF jako możliwą
bazę dalszych wydań.

- Interesuje nas wyłącznie najnowszy AMC Test w Pythonie. Bazą jest wersja
  złożona 26.10.2026 z czterech najnowszych zadań.
- AMC Public powstaje okresowo z dokładnie tego samego sprawdzonego commitu,
  ale z profilem wyłączającym funkcje niedopuszczone do publicznego wydania.
- Każde faktyczne, zweryfikowane wydanie ma zostać zapisane na GitHubie.
- AMC Test i AMC Public mają mieć niezależne kanały aktualizacji w aplikacji.
- Program może być standardowo zainstalowany na dysku C. Źródła, worktree,
  staging i build pozostają na dysku D w `D:\Projekty Codex`.
- Historyczny AMC WPF, `alpha.422` i przypadkowe późniejsze odnogi nie są bazą
  produktu. Usuwanie nastąpi dopiero po sklasyfikowaniu dokładnych celów i
  potwierdzeniu nowej instalacji Python.

## Stan wejściowy potwierdzony 26.10.2026

### Bieżący AMC Test

- Repozytorium: `D:\Projekty Codex\AMC - integracja wxPython`.
- Worktree bieżącej integracji:
  `D:\Projekty Codex\AMC worktrees\sonos-broker-handoff-20261024`.
- Commit: `bba5e92d687ade1998e79c88656e1a5d545abcf2`.
- Pakiet: `39f0d3f2`.
- Etykieta techniczna:
  `2026.10.26-bba5e92d687ade1998e79c88656e1a5d545abcf2-39f0d3f2`.
- Nazwa okna: `AMC Test — 26.10.2026, 13:32`.
- Instalacja: `D:\Projekty Codex\AMC Python`.
- Raport promocji: testy zaliczone, instalacja atomowa, uruchomienie zaliczone.
- Manifest: czyste źródła, profil `test`, kanał `test`, 1674 pliki.

### Konflikt historycznego AMC

Równolegle do właściwej promocji Python inny proces zbudował i zainstalował
historyczny WPF `0.1.0-alpha.427` z katalogu
`D:\Projekty Codex\Accessible Multimedia Controller`. Ten program nie jest
bieżącym AMC Test. Instalacja pozostaje tymczasowo nienaruszona, aby nie usuwać
jej w trakcie innych prac; zostanie odinstalowana dopiero po weryfikacji
systemowego instalatora AMC Test Python.

### GitHub

- Repozytorium `michalkasperczak/AMC` jest publiczne.
- Zdalny `main` nadal kończy się na historycznym WPF `alpha.422`.
- Zdalne gałęzie w chwili audytu: `main`,
  `integracja/wxpython-sonos-tidal-20261008`, `hermes/librespot-probe`,
  `hermes/sonos-group-account-messages` i `wydanie-379`.
- Najnowsze wydanie GitHub to wstępne WPF `v0.1.0-alpha.422`; repozytorium nie
  zawiera jeszcze wydania obecnego AMC Python.

### Lokalny Git

- 12 lokalnych gałęzi jest przodkami dzisiejszego commitu i po ustanowieniu
  kanonicznej gałęzi można je usunąć bez utraty commitów.
- 16 gałęzi ma własne, rozbieżne commity. Nie wolno usuwać ich zbiorczo;
  wymagają porównania commitów i patch-id z dzisiejszą bazą.
- Worktree mają czyste pliki śledzone. Część zawiera wyłącznie nieśledzone
  `.test-output` albo `.test-tmp`; ich sprzątanie nastąpi po odbiorze i
  potwierdzeniu zdalnego archiwum wydania.

## Docelowy model produktu

### Jedno źródło, dwa profile

AMC Test i AMC Public powstają z jednego repozytorium i jednego commitu. Nie
utrzymujemy osobnej gałęzi rozwojowej Public. Różnica jest jawnie wyliczana z
profilu możliwości.

Samo ukrycie pozycji interfejsu nie wystarcza. Funkcja wyłączona w Public:

- nie występuje w menu, `Ctrl+F1`, pełnej pomocy ani `Ctrl+Shift+K`;
- nie ma aktywnego skrótu ani drogi wykonania;
- nie trafia do logów jako dostępna funkcja;
- nie wnosi prywatnego modułu, hosta, zależności ani zasobu do paczki, jeśli
  można go fizycznie odfiltrować;
- ma test parytetu dostępności i wykonania w profilach `test` i `public`.

Kod wspólny może pozostać w jednym publicznym repozytorium, jeśli funkcja ma
być jedynie niedostępna w publicznym instalatorze. Jeżeli jakaś funkcja lub
integracja ma być poufna, nie może trafić do publicznego repozytorium; wymaga
zewnętrznego brokera albo osobnego prywatnego modułu. Sekret nie jest funkcją
i nigdy nie jest dopuszczalnym przełącznikiem wydania.

### Instalacja Windows

Docelowe katalogi użytkownika:

- `%LOCALAPPDATA%\Programs\AMC Test`;
- `%LOCALAPPDATA%\Programs\AMC Public`.

Oba instalatory mają osobne AppId, InstallId, skróty, App Paths, blokady
uruchomienia, deinstalatory i kanały aktualizacji. Integracja systemowa obejmuje
menu Start, opcjonalny skrót pulpitu, rejestrację obsługiwanych plików i
polecenia Eksploratora wyłącznie dla właściwego profilu. Źródła i artefakty
robocze pozostają na D; C zawiera tylko normalnie zainstalowaną aplikację.

Publiczny katalog nazywa się `AMC Public`, a nie samo `AMC`, aby podczas
migracji nie zderzyć się ze starą instalacją WPF w
`%LOCALAPPDATA%\Programs\AMC`.

### Dane użytkownika

Profile Test i Public pozostają fizycznie oddzielone. Nie wolno kierować dwóch
działających aplikacji do tych samych żywych baz SQLite i `state.json`.

Do sprawdzania Public na rzeczywistych danych służy istniejące polecenie
`Odśwież dane z AMC Test`: wersjonowana migawka przenosi wspólne dane do
osobnego profilu Public, nie przenosi tokenów ani poświadczeń i zatrzymuje się
przy konflikcie. Oba profile mogą wskazywać te same istniejące pliki audio bez
ich kopiowania. To jest bezpieczny model używania raz jednej, raz drugiej
aplikacji. Dwukierunkowa synchronizacja zmian Public → Test nie jest obecnie
obiecana; wymagałaby osobnego projektu konfliktów, blokady pisarza i migracji
schematu.

## Docelowy model GitHub i aktualizacji

### Kanał Test

- Tag: `test-YYYY.MM.DD.HHMM`.
- GitHub Release: wydanie wstępne, nieoznaczane jako `Latest`.
- Instalator: `AMC-Test-YYYY-MM-DD-HHMM.exe`.
- Aktualizator AMC Test sprawdza wyłącznie kanał `test` i nigdy nie przyjmuje
  paczki `public`.

### Kanał Public

- Tag: `public-YYYY.MM.DD.HHMM`.
- GitHub Release: zwykłe stabilne wydanie, oznaczane jako `Latest`.
- Instalator: `AMC-Public-YYYY-MM-DD-HHMM.exe`.
- Aktualizator AMC Public sprawdza wyłącznie kanał `stable` i nigdy nie
  przyjmuje paczki `test`.

Każde wydanie zawiera instalator, manifest, SHA-256 i informację o dokładnym
commicie. Publikacja źródeł, publikacja instalatora, instalacja lokalna i
uruchomienie są raportowane jako cztery osobne fakty. Lokalny kandydat albo
build do ręcznego odbioru nie staje się wydaniem tylko dlatego, że się
skompilował.

## Bramki bezpieczeństwa przed pierwszym wydaniem Python

1. Ręcznie odebrać dzisiejszy AMC Test z NVDA.
2. Usunąć możliwość kopiowania `youtube-oauth-client-secret.txt` do dowolnej
   publikowanej paczki. Lokalna konfiguracja deweloperska musi znajdować się
   poza repozytorium i paczką. Czysta instalacja musi użyć PKCE bez sekretu albo
   bezsekretnego brokera; do tego czasu konto YouTube nie może blokować wydania
   pozostałego programu.
3. Dodać test publikacyjny odrzucający sekrety, tokeny, profile, nagrania,
   logi, nieznane pliki i dane użytkownika.
4. Zmienić instalator na dwa jednoznaczne katalogi na C i przetestować
   instalację obok siebie, aktualizację, anulowanie, rollback i deinstalację.
5. Dodać podpisany lub co najmniej hashowany indeks dwóch kanałów oraz test, że
   pakiet obcego profilu zostaje odrzucony przed zamknięciem programu.
6. Zbudować Test i Public z tego samego commitu, porównać manifesty możliwości
   oraz przeskanować zawartość Public pod kątem wyłączonych modułów.
7. Opublikować najpierw wydanie wstępne Test, odczytać z GitHuba nazwę,
   rozmiar i SHA-256, a następnie wykonać aktualizację z poprzedniej wersji.
8. Dopiero po odbiorze Public opublikować stabilne wydanie Public.

## Wykonany etap 1 — źródła bez publikacji

- Publiczny instalator otrzymał oddzielny katalog
  `%LOCALAPPDATA%\Programs\AMC Public`; nie próbuje już wejść do historycznego
  `%LOCALAPPDATA%\Programs\AMC`.
- `Build-AMCInstaller.ps1` odrzuca paczkę, jeżeli manifest albo rzeczywisty
  katalog zawiera lokalny plik sekretu YouTube lub Sonos. Zaliczone przejście
  zapisuje `secretScan: passed` w pliku integralności instalatora.
- Nowy `Publish-AMCRelease.ps1` obsługuje oba profile. Test dostaje tag
  `test-YYYY.MM.DD.HHMM`, kanał `test` i GitHub prerelease; Public dostaje tag
  `public-YYYY.MM.DD.HHMM`, kanał `stable` i zwykłe wydanie.
- Stary `Publish-AMCPublicRelease.ps1` pozostaje zgodnym punktem wejścia, ale
  deleguje do wspólnej profilowanej bramki.
- Tryb bez `-Publish` tylko sprawdza instalator, SHA-256, rewizję, profil,
  skan sekretów i przygotowuje opis oraz manifest gotowości. W tym etapie
  użyto wyłącznie tego kontraktu testowego; nic nie wysłano na GitHub.
- Nie usunięto starego pliku OAuth z działającej instalacji. Próba dodania
  automatycznego usuwania została zatrzymana, ponieważ mogłaby przerwać
  logowanie YouTube przed gotową migracją.

## Wykonany etap 2 — aktualizacja z poziomu programu

### Zachowanie użytkowe

- W menu `Pomoc` działa nowe polecenie `Sprawdź aktualizację AMC…`. Ta sama
  nazwa i akcja występują w palecie `Ctrl+Shift+K`, pomocy kontekstowej
  `Ctrl+F1` oraz pełnej pomocy klawiatury; polecenie nie ma osobnego skrótu.
- Po sprawdzeniu otwiera się dostępne okno. Początkowy fokus trafia do pola
  tylko do odczytu `Informacje o aktualizacji AMC`, zawierającego wersję i
  krótkie podsumowanie zmian. Dalej znajduje się `Aktualizuj AMC` albo, gdy
  nie ma nowej wersji, tylko `Zamknij okno aktualizacji`.
- Nie ma wyboru kanału. Profil Test przyjmuje wyłącznie prerelease z tagiem
  `test-YYYY.MM.DD.HHMM`, a Public wyłącznie stabilne wydanie z tagiem
  `public-YYYY.MM.DD.HHMM`. Obcy profil, kanał, produkt, InstallId, nazwa
  instalatora, rozmiar albo SHA-256 są odrzucane przed zamknięciem AMC.
- Aktualizacja pobiera ten sam pełny instalator EXE, który służy nowemu
  użytkownikowi. Pomocnik prosi AMC o zwykłe, bezpieczne zamknięcie, odmawia
  podmiany przy nagrywaniu albo żywym składniku instalacji, uruchamia Inno
  Setup bez dodatkowych pytań i po sukcesie ponownie otwiera AMC.
- Cichy przebieg instalatora zachowuje wcześniejszy katalog oraz zadania Inno
  Setup, między innymi decyzję o skrócie pulpitu i integracji Eksploratora.
  Windows dostaje poprawny nowy AppVersion i stan deinstalatora; aktualizacja
  nie omija instalatora przez prostą podmianę plików.

### Kontrakt wydania

- `Publish-AMCRelease.ps1` tworzy `amc-update-test.json` albo
  `amc-update-public.json` z profilem, kanałem, wersją użytkową, commitem,
  krótkim opisem oraz nazwą, rozmiarem i SHA-256 pełnego instalatora.
- Wydanie GitHub zawiera instalator, manifest kanału i plik integralności.
  Oddzielny ZIP aktualizacyjny nie jest potrzebny i nie jest publikowany.
- Budowa instalatora wymaga czystych źródeł oraz zgodności commitu paczki z
  commitem instalatora. Pełna paczka nadal zawiera prywatny runtime Python,
  samowystarczalny LiteHost, launcher i helper aktualizacji; nowy komputer nie
  musi mieć zainstalowanego Pythona ani .NET.

### Sprawdzenie aktualizacji składników

- Droga `Pomoc > Sprawdź i zaktualizuj składniki: FFmpeg i yt-dlp` nadal
  prowadzi przez wspólną akcję do LiteHosta. Testy potwierdzają menu, paletę,
  pomoc, dispatcher, tryb automatyczny oraz bezpieczny komunikat błędu bez
  ujawniania ścieżek.
- Zainstalowane składniki zostały rzeczywiście uruchomione: FFmpeg zgłosił
  `n9.0.2-29-g6dfaba896f-20261020`, a yt-dlp `2026.08.19`; suma pliku yt-dlp
  zgadza się z zapisanym stanem.
- Pełne sieciowe pobranie w piaskownicy doszło do uruchomieniowej kontroli
  nowego yt-dlp, lecz system testowy zablokował wykonanie pobranego EXE.
  Próba zmiany współdzielonych składników poza piaskownicą nie otrzymała zgody
  bezpieczeństwa. Rzeczywista kontrola sieciowa z menu pozostaje więc punktem
  ręcznego odbioru, a nie zaliczonym testem aktualizacji dostawcy.

## Korekta użytkownika z 26.10.2026 — Quick App Switcher w instalatorze

Ta decyzja zastępuje rozpoczęty wariant, w którym klient aktualizacji AMC miał
osobno pobierać dodatek NVDA i pytać o niego po aktualizacji programu.

- Nowy instalator AMC Test musi zawierać zweryfikowaną paczkę dodatku NVDA
  `Quick App Switcher`.
- Na końcowej stronie interaktywnego instalatora pojawia się jednoznaczna,
  domyślnie zaznaczona propozycja `Uruchom instalację dodatku NVDA Quick App
  Switcher <wersja>` tylko wtedy, gdy dodatku nie ma albo zainstalowana wersja
  jest starsza.
- Sam instalator AMC nie modyfikuje katalogu dodatków NVDA i nie restartuje
  czytnika. Po zgodzie uruchamia standardową paczkę `.nvda-addon`; NVDA nadal
  pokazuje własne potwierdzenie instalacji i ewentualnego restartu.
- Cicha aktualizacja AMC nie uruchamia instalatora dodatku. Dzięki temu
  aktualizator programu nie wyświetla nieoczekiwanego okna ani nie restartuje
  NVDA. Dodatek jest proponowany przy ręcznym uruchomieniu nowego instalatora.
- Budowa instalatora AMC Test zatrzymuje się, jeśli nie otrzyma paczki Quick
  App Switchera. Skrypt sprawdza nazwę dodatku i wersję z `manifest.ini`, nazwę
  pliku, rozmiar oraz SHA-256, a metadane zapisuje w pliku integralności
  instalatora.

Pierwszą przewidzianą paczką jest `quickAppSwitcher-0.1.6.nvda-addon`: 17 920
bajtów, SHA-256
`16dc660e6b3fece58af8ccb84ccc43824bcb7744e1f18a37ffa629685349a61f`.
Na bieżącym komputerze wersja 0.1.6 jest już zainstalowana, więc instalator nie
powinien proponować jej ponownie.

## Kolejność porządkowania

1. Zamrozić dzisiejszy commit jako pierwszy kandydat kanoniczny i dokończyć
   ręczny odbiór.
2. Wprowadzić bramki sekretów i kanały aktualizacji.
3. Zbudować i zweryfikować systemowy instalator AMC Test Python na C.
4. Po działającym zastępstwie usunąć starą instalację WPF `alpha.427`, jej
   skróty i wpisy systemowe; repozytorium WPF zachować jako archiwum historii,
   dopóki porównanie zmian nie potwierdzi, że nic potrzebnego nie zginie.
5. Utworzyć zabezpieczający tag lub bundle wszystkich lokalnych końcówek,
   porównać 16 rozbieżnych gałęzi, a następnie usuwać tylko gałęzie w pełni
   zintegrowane albo jednoznacznie odrzucone.
6. Przenieść kanoniczną linię Python na GitHub `main`. Przed zmianą zachować
   stary koniec WPF pod jednoznacznym tagiem archiwalnym. Nie wykonywać ślepego
   force-push ani kasowania bez weryfikacji zdalnego punktu odniesienia.
7. Po potwierdzeniu zdalnych commitów i artefaktów usunąć wyłącznie dokładnie
   wskazane stare worktree, staging, cache, paczki i katalogi aktualizacji.

## Materiał do dokumentacji użytkownika

AMC Test jest bieżącą, pełną wersją programu. Otrzymuje częstsze wydania
wstępne i aktualizuje się wyłącznie z kanału Test. AMC Public jest okresowym,
sprawdzonym wydaniem tego samego kodu z wyłączonymi funkcjami, które nie są
jeszcze gotowe do przekazania innym osobom. Aktualizuje się wyłącznie z kanału
Public.

Obie aplikacje mogą być zainstalowane obok siebie. Mają osobne ustawienia,
konta i logi. Polecenie `Odśwież dane z AMC Test` pozwala sprawdzić AMC Public
na kopii rzeczywistych danych bez współdzielenia żywych baz i bez przenoszenia
haseł lub tokenów. Nie należy uruchamiać obu programów jako dwóch pisarzy do
tych samych plików profilu.

## Uzasadnienie i decyzje

- Jedna baza kodu usuwa ryzyko naprawiania tego samego błędu dwa razy.
- Dwa profile kompilacji pozwalają fizycznie usunąć niedopuszczone moduły z
  paczki Public, zamiast polegać tylko na ukrytym przycisku.
- Osobne kanały GitHub pozwalają aktualizować Test często, bez proponowania
  tych samych buildów użytkownikom Public.
- Standardowa instalacja na C zapewnia normalny deinstalator i integrację z
  Windows. D pozostaje bezpiecznym miejscem źródeł i buildów.
- Stare wydania GitHub są historią, nie bazą rozwoju. Ich masowe usuwanie nie
  jest wymagane do działania aktualizatora; decyzja o ich późniejszym
  ukryciu lub usunięciu nastąpi po migracji `main`.

## Testy i wynik

- Pełna promocja AMC Test z commitu `bba5e92d…`: zaliczona.
- Atomowa instalacja pakietu `39f0d3f2`: zaliczona.
- Kontrolowany start: zaliczony.
- Ręczny odbiór czterech nowych zakresów z NVDA: oczekuje.
- Kod mechanizmu aktualizacji z GitHuba: zaimplementowany; profil, kanał,
  manifest, rozmiar i SHA-256 są sprawdzane przed zamknięciem aplikacji.
- Ukierunkowane testy Pythona mechanizmu, instalatora, publikacji i składników:
  **80 zaliczonych**.
- Pełna regresja wxPython: **2075 zaliczonych, 111 pominiętych i 13 podtestów
  zaliczonych**.
- Testy launchera i aktualizatora .NET: **33 z 33 zaliczone**, w tym przyjęcie
  pełnego instalatora właściwego profilu i odrzucenie obcego profilu oraz
  niezgodnej sumy.
- LiteHost Release: build zaliczony, 0 ostrzeżeń i 0 błędów.
- Pierwsza próba pakowania wykryła wyścig kontrolowanego startu: interfejs
  zapisywał `READY`, lecz natychmiastowe `ExitMainLoop` mogło ominąć oczekujące
  zdarzenie zniszczenia ramki i pozostawić procesy testowe. Wyjście jest teraz
  planowane przez `wx.CallAfter` po `Close`; bramka nadal wymaga naturalnego
  zakończenia wszystkich procesów i niczego nie zabija. Testy ukierunkowane po
  tej korekcie: **38 zaliczonych**.
- Pełna paczka kandydata: **2135 plików**, pełna kontrola integralności i
  kontrolowany start interfejsu oraz hosta zaliczone; po teście nie pozostał
  żaden proces paczki.
- Instalator systemowy AMC Test został zbudowany jako
  `AMC-Test-2026-10-26-1954.exe` (134 236 985 bajtów). Lokalna sucha próba
  wydania potwierdziła zgodność profilu Test, kanału Test, prerelease, nazwy,
  rozmiaru, commitu i SHA-256 między instalatorem, plikiem integralności i
  `amc-update-test.json`. Instalator nie został uruchomiony ani zainstalowany.
- Pierwsze prawdziwe wydanie GitHub kanału Test zostało opublikowane jako
  `test-2026.10.26.1954`. GitHub potwierdził profil prerelease, tag na commicie
  `251e163a`, instalator 134 236 985 bajtów oraz zgodne z lokalnymi plikami
  sumy SHA-256 instalatora, manifestu aktualizacji i pliku integralności.
- Rzeczywista aktualizacja z programu oczekuje na następną wersję wyższą niż
  zainstalowane `0.1.0-alpha.434`; pierwsze wydanie stanowi bazę kanału.
- Kontrakt nowego instalatora z Quick App Switcherem: zaimplementowany w
  źródłach; celowany test strukturalny ma wynik **20 zaliczonych**, a Inno
  Setup 7.1.0 poprawnie skompilował mały instalator kontrolny z prawdziwą
  paczką 0.1.6. Kontrolnego EXE nie uruchomiono i po teście usunięto.
- Kontrakt Quick App Switchera został zapisany po zbudowaniu instalatora
  `AMC-Test-2026-10-26-1954.exe`. To opublikowane pierwsze wydanie nie zawiera
  jeszcze dodatku; następny instalator Test ma powstać z commitu zawierającego
  kontrakt i zapisać dane dodatku w pliku integralności.
- Zweryfikowana gałąź źródłowa została wysłana bez przepisywania historii jako
  `github/integration/sonos-broker-20261024`; GitHub potwierdził jej zdalny
  koniec.
- Po ponownym powiązaniu działającego procesu, manifestu instalacji, instalatora
  i commitu użytkownik polecił opublikować tę linię jako kod bieżącego AMC.
  26.10.2026 zdalny `main` przesunięto zwykłym fast-forwardem z `1f5dccb6` na
  `219062c8`. Domyślną gałęzią repozytorium pozostał `main`; nie wykonano
  force-pusha ani usunięcia historii WPF.

## Korekta 27.10.2026 — konfiguracja OAuth poza wydaniem

- Ta decyzja zastępuje zapis z 23.10.2026 w zadaniu
  `AMC-TEST-2026-10-23-youtube-secret-frontier-choice.md`, według którego
  builder dołączał lokalną parę OAuth do paczki AMC Test.
- Bramka instalatora prawidłowo zatrzymała kandydata `0.1.0-alpha.435`, gdy
  manifest zawierał `youtube-oauth-client-secret.txt`; instalator nie powstał,
  a żaden taki plik nie został wysłany na GitHub.
- `build_bundle.py` usuwa teraz identyfikator i sekret OAuth YouTube z każdej
  publikowanej paczki, także gdy istnieją w lokalnym katalogu deweloperskim.
  Istniejącej, niezarządzanej konfiguracji w zainstalowanym AMC aktualizator
  nie usuwa. Czysta instalacja ukrywa konto YouTube do czasu wdrożenia PKCE bez
  sekretu albo bezsekretnego brokera.
- Testy ochrony paczki i kontraktu instalatora po korekcie: **51 zaliczonych**.
- Źródła, testy i pomyślna budowa są oddzielnymi faktami; w chwili tej korekty
  pełna paczka, instalator i publikacja nowego wydania wymagały ponownego
  wykonania po commicie. Wynik tej promocji zapisano poniżej.

## Drugie wydanie Test 27.10.2026

- Poprawkę OAuth zapisano przed ponowną promocją; końcowy commit źródłowy to
  `4cb57da5a6bda7bb82696ff426c00f2b9f655704`, a drzewo źródłowe było czyste.
- Pełna regresja wxPython: **2094 zaliczone, 113 pominiętych i 13 podtestów**;
  wszystkie 27 zestawów protokołu LiteHosta, 33 testy Launchera i Updatera oraz
  pełne testy dymne Core zakończyły się powodzeniem.
- Końcowa paczka ma identyfikator `73ec48a6`, 2136 plików i nie zawiera żadnego
  pliku identyfikatora ani sekretu OAuth YouTube. Pełna integralność i
  kontrolowany start zostały zaliczone.
- Inno Setup zbudowało `AMC-Test-2026-10-27-0057.exe` o rozmiarze 134290649
  bajtów i SHA-256
  `a09a957923afd708a78445d4bb9574181d222ce9ee557c776118bb5cb86a2329`.
  Plik integralności potwierdza dołączony Quick App Switcher 0.1.6.
- Sucha próba publikacji, właściwa publikacja oraz odczytowa weryfikacja
  GitHuba zostały zaliczone. Zdalne sumy instalatora, pliku integralności i
  manifestu aktualizacji są zgodne z lokalnymi.
- W chwili publikacji instalatora jeszcze nie uruchomiono ani nie zainstalowano.
  Późniejszy wynik rzeczywistej aktualizacji zapisano w następnej sekcji.

## Rzeczywista aktualizacja 27.10.2026

- Przed aktualizacją działał AMC Test `26.10.2026, 19:54` z commitu
  `251e163a` i pakietu `67437118`. Nie działał proces FFmpeg ani nagrywanie;
  trwał wyłącznie wznawialny, odczytowy proces `yt-dlp` dla zapisanych kanałów.
- Pierwsze uruchomienie kopii aktualizatora przez powłokę nie zachowało
  poprawnie ścieżek ze spacjami. Instalacja nie zmieniła się, AMC pozostał
  uruchomiony, a katalog `.aktualizacja-*` nie powstał. Nie uznano tego za
  aktualizację.
- Zweryfikowaną kopię tego samego `AMC-Python-Updater.exe` uruchomiono następnie
  synchronicznie z pełnymi argumentami. Aktualizator sprawdził SHA-256
  instalatora, bezpiecznie zamknął AMC, wykonał pełną instalację i uruchomił
  aplikację ponownie.
- Po aktualizacji manifest wskazuje `27.10.2026, 00:57`, commit
  `4cb57da5a6bda7bb82696ff426c00f2b9f655704`, pakiet `73ec48a6` i
  `sourceDirty=false`. Działa jedna instancja z właściwego katalogu
  `C:\Users\micha\AppData\Local\Programs\AMC Test`.
- Pełna cicha kontrola integralności `--sprawdz-pakiet-pelny-cicho` zakończyła
  się kodem 0.
- Przed i po aktualizacji baza zawierała dokładnie 421 zapisanych kanałów
  YouTube; SHA-256 uporządkowanego zestawu ich identyfikatorów i adresów
  pozostał `b1156375904f699213cc4a8ed6b1fff3869576a9a862f19d5c75ebf7d4c89172`.
- W instalacji oraz manifeście było 0 plików `youtube-oauth-client-id.txt` i
  `youtube-oauth-client-secret.txt`. Zgodnie z decyzją użytkownika nie
  uruchamiano logowania Google; test nowego OAuth nastąpi po wdrożeniu zwykłego
  logowania niezależnego od ręcznej konfiguracji Google Cloud.
- Quick App Switcher 0.1.6 pozostał zainstalowany. Aktualizator używa jednak
  instalatora w trybie cichym, który celowo pomija opcjonalne propozycje po
  instalacji, więc ten przebieg nie jest żywym testem warunku „brak lub starsza
  wersja”. Warunek pozostaje pokryty testami kontraktowymi instalatora.
- Niedostępne sterowanie natywnym oknem nie pozwoliło kliknąć polecenia w menu
  ani odebrać dialogu pobierania z NVDA. Został rzeczywiście sprawdzony etap
  bezpiecznego zastosowania pełnego instalatora, zachowanie profilu i ponowny
  start; ręczny odbiór interfejsu aktualizacji pozostaje.

## Trzecie wydanie Test 27.10.2026

- Końcowy, czysty commit źródłowy to
  `abfee285274810746caedd948aae4d9d7b44aafd`; zdalna gałąź robocza oraz `main`
  zostały przesunięte do tego commitu bez przepisywania historii.
- Wydanie zawiera YT-005: doładowanie skrzynki po 150 pozycji przez kolejne
  `Ctrl+I`, pełny obieg obserwowanych źródeł oraz polskie tytuły YouTube.
- Wydanie zawiera POD-004: SoundCloud jako zwykły podcast RSS z wyszukiwaniem,
  lokalną subskrypcją, odtwarzaniem, kolejką, pobieraniem i zapisem pliku.
  Niespełniający wymagań wariant Mixcloud został wycofany przed wydaniem.
- Pełna regresja SoundCloud: **2101 zaliczonych, 113 pominiętych i 13
  podtestów**; końcowe 42 testy, pełne testy Core i rzeczywiste próby RSS,
  wyszukiwania oraz `audio/mpeg` zaliczone. YT-005 ma 141 zaliczonych testów
  Pythona, dwie regresje C# i odczytowe próby biblioteki oraz yt-dlp.
- Samowystarczalna paczka ma identyfikator `adc818de`, 2136 plików,
  `sourceDirty=false`; pełna integralność i odizolowany start zostały zaliczone.
- Inno Setup zbudowało `AMC-Test-2026-10-27-0233.exe` o rozmiarze 134290393
  bajtów i SHA-256
  `1d4fd2ecabe4d358246ae094ebfefdbc19dde7ceade018e6f13bcb5c61beb36a`.
  Skan sekretów przeszedł, a integralność potwierdza Quick App Switcher 0.1.6.
- Sucha próba i publikacja prerelease `test-2026.10.27.0233` przeszły. GitHub
  potwierdził tag na dokładnym commicie i zgodne sumy trzech załączników.
- Zgodnie z decyzją użytkownika nie uruchomiono instalatora ani aktualizatora.
  Działające AMC Test pozostało na `0.1.0-alpha.435`; ręczna aktualizacja z menu
  zostanie sprawdzona przez użytkownika następnego dnia.

## Ręczny odbiór alpha.436 i korekta startu po aktualizacji 27.10.2026

- Użytkownik uruchomił aktualizację z aplikacji, pobrał ją i zainstalował bez
  pomocy Codexa. Plik zainstalowanego `AMC.exe` wskazuje
  `0.1.0-alpha.436`. Użytkownik potwierdził, że wszystkie etapy się wykonały,
  i uznał mechanizm aktualizacji za działający. Ten wynik zastępuje wcześniejszy
  zapis z sekcji „Trzecie wydanie Test”, według którego ręczna próba pozostawała.
- Ręczny przebieg ujawnił jedno zbędne zachowanie: po ponownym uruchomieniu
  aktualizator wyświetlał modalne potwierdzenie „Aktualizacja AMC Test została
  zainstalowana.” z przyciskiem OK. To okno mogło dodatkowo zasłonić główne
  okno AMC.
- Decyzja użytkownika z 27.10.2026: po powodzeniu nie ma być żadnego dialogu
  potwierdzającego. Komunikaty błędów pozostają jawne. Po starcie z aktualizatora
  główne okno AMC ma otrzymać jednorazową próbę pierwszego planu; zwykły start
  i uruchomienie kontrolne nie mogą przejmować tej ścieżki.
- Commit źródłowy `ae24e9df` usuwa dialog sukcesu. Aktualizator przekazuje
  launcherowi zamknięty przełącznik wewnętrzny i uprawnienie Windows do
  pierwszego planu. Launcher nie przekazuje przełącznika do Pythona, udziela
  tego uprawnienia procesowi interfejsu i przez maksymalnie 15 sekund czeka na
  właściwe okno profilu, po czym korzysta z istniejącej drogi
  przywrócenia, maksymalizacji i aktywacji.
- Testy po zmianie: **34/34** kontrakty Launchera i Updatera oraz **31/31**
  celowanych testów Pythona. Rzeczywiste projekty `AMC.exe` i
  `AMC-Python-Updater.exe` zbudowały się w Release bez błędów i ostrzeżeń.
- Te fakty potwierdzają źródła, testy i build. Nie powstała nowa paczka ani
  instalator, nie zmieniono działającej instalacji `alpha.436` i nie
  opublikowano nowego wydania. Zachowanie fokusu po tej korekcie wymaga odbioru
  przy następnej ręcznej aktualizacji.

## Znane ograniczenia

- Bieżąca instalacja na C nie zawiera pliku identyfikatora ani sekretu OAuth
  YouTube. Zapisane publiczne kanały pozostały dostępne, ale logowanie do konta
  nie jest obecnie odbierane. Nowy przepływ OAuth wymaga osobnego zadania.
- Historyczny WPF `alpha.427` został dziś zainstalowany przez równoległe
  zadanie. Nie jest dowodem aktualności AMC i nie może zastąpić Python.
- Tagi `test-2026.10.26.1954`, `test-2026.10.27.0057` i
  `test-2026.10.27.0233` wskazują dokładny kod wersji odpowiednio
  `0.1.0-alpha.434`, `0.1.0-alpha.435` i `0.1.0-alpha.436`; ruchome `main` może
  zawierać późniejsze commity wyłącznie dokumentacyjne.

## Korekta użytkownika z 28.10.2026 — najwyżej dwie kopie ratunkowe

### Materiał do dokumentacji użytkownika

- Udana aktualizacja bieżącego AMC Test przez pełny instalator nie odkłada
  trwałej pełnej kopii programu. Instalator ma własne transakcyjne cofanie, a
  pobrany plik jest sprzątany po użyciu.
- Katalog `.aktualizacja-…` jest kopią ratunkową zastępowanych plików tylko dla
  lokalnej ścieżki podmiany pakietu. Zwykle znika po sukcesie albo pełnym
  wycofaniu. Jeżeli awaria uniemożliwi pełne cofnięcie, aktualizator zachowuje
  najwyżej dwa takie katalogi: bieżący uszkodzony przebieg i co najwyżej jeden
  wcześniejszy.
- Zmiana nie ma nowej kontrolki, skrótu ani komunikatu NVDA. Nie obejmuje
  Biblioteki, ustawień, kont, nagrań ani plików użytkownika.

### Uzasadnienie i decyzje

- Katalogi `%LOCALAPPDATA%\AccessibleMediaController\update-backups` pochodzą
  z historycznego AMC WPF i nie są tworzone przez bieżący aktualizator AMC
  Python. Nie należy dodawać do nowego programu mechanizmu utrwalającego takie
  pełne kopie po każdej udanej aktualizacji.
- Retencja rozpoznaje wyłącznie bezpośrednie katalogi o ścisłej nazwie
  `.aktualizacja-<GUID>`, pomija dowiązania i obce nazwy. Bieżąca kopia po
  niepełnym wycofaniu ma pierwszeństwo przed starszymi katalogami.

### Testy i wynik

- Celowany zestaw `AccessibleMediaController.PythonLauncher.Tests`: **36/36
  zaliczonych**. Nowe przypadki potwierdzają pozostawienie dwóch najnowszych
  kopii, nienaruszenie obcej nazwy oraz ochronę bieżącej niepełnej transakcji.
- Projekt `AccessibleMediaController.PythonUpdater` zbudował się w konfiguracji
  Release: **0 ostrzeżeń, 0 błędów**.
- Nie uruchamiano interfejsu ani pełnej regresji. Nie zbudowano paczki ani
  instalatora wydania, nie zmieniono działającej instalacji i niczego nie
  opublikowano.
- Z `C:\Users\micha\AppData\Local\Temp` trwale usunięto 18 209 katalogów
  należących do testów AMC. Odzyskano około 370 053 120 bajtów. Pozostało 775
  pustych lub testowych katalogów z listami dostępu, których bieżąca sesja nie
  może przejąć; nie rozszerzano uprawnień na inne cele.
- Zachowano aktywny bufor Time Shiftu
  `C:\Users\micha\AppData\Local\Temp\AMC-timeshift-66df642f539e457d99f654a896b50353.buf`
  o rozmiarze 3 810 240 000 bajtów. To on stanowił większość raportowanych
  około 4,05 GB w Temp; nie jest starym katalogiem testowym.

### Znane ograniczenia

- Retencja sprząta stare katalogi przy następnym bezpiecznie rozpoczętym
  przebiegu aktualizatora. Jeśli Windows blokuje usunięcie, aktualizacja nadal
  może się odbyć, a błąd porządkowy trafia tylko do diagnostyki.
- W bieżącej instalacji `D:\Projekty Codex\AMC Python` istnieje jeszcze siedem
  starych katalogów `.aktualizacja-…` o łącznym rozmiarze logicznym około
  829 353 043 bajtów. Próba pozostawienia dwóch najnowszych została dwukrotnie
  bezpiecznie zatrzymana, ponieważ kontrola poza piaskownicą nadal widzi żywe
  procesy AMC, hosta audio i Pythona. Nie zamykano programu bez potwierdzenia
  stanu nagrywania. Po bezpiecznym zamknięciu można usunąć pięć starszych albo
  pozostawić to następnemu wydaniu zawierającemu nową retencję.
- Usunięte katalogi Temp nie trafiają do Kosza i nie są odzyskiwalne zwykłym
  przywróceniem. Pozostałe 775 katalogów wymagałoby osobnej sesji z prawami
  administratora; ich usuwanie nie jest potrzebne do odzyskania głównej części
  miejsca, ponieważ dominującym plikiem jest zachowany bufor Time Shiftu.

### Wersja i commit

- Zmiana istnieje w źródłach bieżącej gałęzi roboczej. Nie utworzono osobnego
  commitu, wersji, paczki, instalacji ani wydania GitHub.

## Czwarte wydanie Test 28.10.2026

- Wersję `0.1.0-alpha.437` opublikowano jako prerelease
  `test-2026.10.28.1323` z commitu
  `301c7fcf5c54473bfa1307d3b1c6961b25115ef3`.
- Wydanie zawiera zatwierdzone poprawki YouTube i Ustawień, polskie wyniki
  SoundCloud, historię rozpoznanych utworów, korektę edycji klipów oraz
  poprawki aktualizatora. Pakiet językowy celowo pozostał poza tym wydaniem.
- Instalator `AMC-Test-2026-10-28-1323.exe` ma SHA-256
  `794810fbf66fd468af4c687df216370b556af5b5f670b717a64e619b374c39db`.
  Publikacja została odczytowo zweryfikowana; instalatora nie uruchamiano.

## Piąte wydanie Test 28.10.2026

- Wersję `0.1.0-alpha.438` opublikowano jako prerelease
  `test-2026.10.28.2022` z commitu
  `9b2f6246f6c24d4cfc21d453b094e4dc2baddec9`.
- Wydanie zawiera wspólną warstwę językową, polski, angielski, wybór zgodny z
  systemem, osobne szablony komunikatów oraz katalogi i narzędzia dla kolejnych
  języków.
- Końcowa paczka `908e5dd5` ma profil `test`, `sourceDirty=false`, 2139 plików
  manifestu i przeszła pełną integralność oraz kontrolowany ukryty start.
- Instalator `AMC-Test-2026-10-28-2022.exe` ma 134355952 bajty i SHA-256
  `6e16f2b392e5957365fee856c62778096a23d1fef46a6f0f09fe3bac8ef50c96`.
  GitHub potwierdził ten sam rozmiar i digest oraz oba manifesty w stanie
  `uploaded`.
- Instalatora ani aktualizatora nie uruchamiano. Lokalna instalacja pozostała
  bez zmian, aby użytkownik mógł samodzielnie odebrać aktualizację.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.28.2022`.

## Szóste wydanie Test 29.10.2026

- Wersję `0.1.0-alpha.441` opublikowano jako prerelease
  `test-2026.10.29.0006` z commitu
  `19fc8e72ef258266b4b53144097d10bbec839c72`.
- Wydanie dodaje **Ustawienia sesji** w Menedżerze `Ctrl+Shift+S` i sesyjny
  wybór, czy Enter otwiera odtwarzacz, czy pozostawia użytkownika na liście.
  Zachowanie obejmuje obsługiwane sesje lokalne i sieciowe, kolejki, zakładki
  oraz wyniki wyszukiwania. Pliki lokalne i Radio mają też wybór dla presetów.
- Końcowa paczka `7b805a42` ma profil `test`, `sourceDirty=false`, 2139 plików
  manifestu i przeszła pełną integralność oraz kontrolowany ukryty start.
- Instalator `AMC-Test-2026-10-29-0006.exe` ma 134364676 bajtów i SHA-256
  `755242b85eff9f02c8443de66c56480b4d39de2189d3fe5c26aad0bbf8adca71`.
  Skan sekretów przeszedł; integralność potwierdza Quick App Switcher 0.1.6.
- Sucha bramka i publikacja zostały zaliczone. GitHub potwierdził tag na
  dokładnym commicie oraz instalator, plik integralności i manifest
  aktualizacji w stanie `uploaded` z digestami SHA-256.
- Instalatora ani aktualizatora nie uruchamiano. Działająca instalacja AMC Test
  pozostała bez zmian; ciche kontrole użyły jednorazowego profilu.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.29.0006`.

## Siódme wydanie Test 29.10.2026

- Wersję `0.1.0-alpha.442` opublikowano jako prerelease
  `test-2026.10.29.0057` z commitu
  `2b772d066d0e54188354dfb2e024a76833d14033`.
- Wydanie poprawia rozpoznawanie polskiego języka Windows, przełącza język
  otwartego interfejsu od razu po zapisie i usuwa mieszany odczyt nazwy listy
  `Ulubione` w angielskim interfejsie.
- Celowany zestaw języka, Ustawień, dostępności listy, aktualizatora,
  instalatora i bramki publikacji przeszedł w całości: 76/76 testów.
- Końcowa paczka `6b27fa6d` ma profil `test`, `sourceDirty=false`, 2139 plików
  manifestu i przeszła pełną integralność oraz kontrolowany ukryty start.
- Instalator `AMC-Test-2026-10-29-0057.exe` ma 134383471 bajtów i SHA-256
  `f1ebe22f1a69e5c1c8f205ffa8fbeb72d394a3a4b0145363eff6871bb071a6db`.
  Skan sekretów przeszedł; integralność potwierdza Quick App Switcher 0.1.6.
- Sucha bramka i publikacja zostały zaliczone. GitHub potwierdził tag na
  dokładnym commicie oraz instalator, plik integralności i manifest
  aktualizacji w stanie `uploaded` z digestami SHA-256.
- Instalatora ani aktualizatora nie uruchamiano. Działająca instalacja AMC Test
  pozostała na wersji z 28.10.2026, 20:22; odbiór aktualizacji pozostaje ręczny.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.29.0057`.

## Ósme wydanie Test 29.10.2026

- Wersję `0.1.0-alpha.444` opublikowano jako prerelease
  `test-2026.10.29.1243` z commitu
  `3e6e7303e8a6f12daaa9d47b219ea5d12da3ab46`. Wspólny zakres obejmuje
  poprawkę przełączania języka interfejsu oraz próbne, ręcznie odświeżane
  strony Facebooka i pojedyncze filmy Facebooka i Instagrama.
- Przed wydaniem: 39 celowanych testów Pythona zaliczonych, 1 pominięty;
  28 zestawów kontraktów LiteHosta zaliczonych. Anonimowy odczyt strony
  `buremisie` ponownie zwrócił jeden film. Nie testowano rzeczywistego
  pobrania MP3 ani obsługi z NVDA.
- Paczka `5ce8af5d` ma profil `test`, `sourceDirty=false`, 2139 plików;
  pełna integralność i cichy start interfejsu oraz hosta przeszły.
- Instalator `AMC-Test-2026-10-29-1243.exe` ma 134385400 bajtów i SHA-256
  `65036795a6915a4da02090d703de0f4c9913cea7ab1cb99abeed9952467c8a18`.
  Skan sekretów oraz sucha bramka kanału `test` przeszły.
- GitHub potwierdził tag na dokładnym commicie, prerelease i trzy zasoby
  w stanie `uploaded`. Instalatora nie uruchamiano ani nie instalowano.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.29.1243`.

## Dziewiąte wydanie Test 29.10.2026

### Materiał do dokumentacji użytkownika

- AMC Test otrzymał poprawiony import nazw i wielu stacji z playlist M3U,
  tytuły audycji z metadanych ICY, korektę migracji prędkości sesji oraz
  automatyczne sprawdzanie nowej wersji po uruchomieniu.
- Pole automatycznego sprawdzania jest na karcie `Ustawienia > Aktualizacje`.
  Samo sprawdzenie nie pobiera ani nie instaluje pliku; wymaga późniejszego
  wybrania `Aktualizuj AMC`.
- Profil Test nadal zawiera TIDAL, Spotify, Sonos, WiiM i Frontier Smart.
  Sonos pozostaje funkcją eksperymentalną w dotychczasowym, niedziałającym
  stanie i nie jest przedstawiany jako naprawiony.

### Uzasadnienie i decyzje

- Użytkownik 29.10.2026 przekazał, że Hubert potwierdził poprawne działanie
  poprzedzającego wydania Public, w tym prawidłowe zamknięcie aplikacji.
  Na tej samej wspólnej bazie przygotowano następne wydanie Test, zachowując
  pełne możliwości profilu Test zamiast ograniczeń Public.
- Wydanie obejmuje wszystkie zmiany wspólnej bazy po Test `alpha.444`.
  Otrzymało nowy numer `0.1.0-alpha.445` i osobny tag prerelease kanału `test`.

### Testy i wynik

- Celowane, bezokienne testy Pythona: **205 zaliczonych, 1 kontrolowanie
  pominięty**. Zakres obejmował aktualizacje, Ustawienia, start, stan,
  tłumaczenia, profil Test/Public, paczkę, instalator i skrypt wydania.
- Celowane testy importu M3U/PLS/XSPF/VRadio oraz metadanych ICY: **3/3
  zaliczone**. Testy launchera i aktualizatora: **38/38 zaliczonych**.
- Paczka `e6945c5c` ma profil `test`, `sourceDirty=false`, 2139 plików
  manifestu i 443 730 737 bajtów wraz z manifestem. Fizycznie zawiera TIDAL,
  Spotify, Sonos i host Librespot; po kontrolowanym starcie liczba plików spoza
  manifestu wynosi zero. Pełna integralność i ukryty start interfejsu oraz
  hosta przeszły.
- Instalator `AMC-Test-2026-10-29-2032.exe` ma 133 590 131 bajtów i SHA-256
  `da28bcb96bc71262627bbc91d110295aa13d0a9f63cf28f29e860c0c1ef7624a`.
  Skan sekretów przeszedł; integralność potwierdza Quick App Switcher 0.1.6.
- Sucha bramka i publikacja GitHub zostały zaliczone. Tag wskazuje dokładny
  commit, wydanie jest prerelease, a instalator, plik integralności i manifest
  aktualizacji mają zdalny stan `uploaded` oraz zgodne digesty.

### Znane ograniczenia

- Sonos nadal nie działa i pozostaje w wydaniu wyłącznie jako eksperymentalna
  funkcja profilu Test.
- Nie uruchamiano instalatora, rzeczywistej aktualizacji działającej kopii ani
  widocznej regresji GUI z NVDA. Działającej instalacji AMC Test nie zmieniono.

### Wersja i commit

- Wersja techniczna: `0.1.0-alpha.445`; wersja widoczna:
  `AMC Test — 29.10.2026, 20:32`.
- Commit źródłowy:
  `cbf71ad1ba86f2b2d638eb3128b26d5ff73456e1`.
- Lokalny instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.445 wydanie 2026-10-29\instalator\AMC-Test-2026-10-29-2032.exe`.
- Tag: `test-2026.10.29.2032`.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.29.2032`.
- Instalacja lokalna: niewykonana.

## Wydanie Public na bazie alpha.438 — 28.10.2026

### Materiał do dokumentacji użytkownika

- AMC Public otrzymuje te same nowości użytkowe co najnowszy AMC Test, ale
  zachowuje osobny profil danych, poświadczeń, pamięci podręcznej, logów,
  instalacji i kanału aktualizacji `stable`.
- Public nie udostępnia logowania Google do YouTube. W menu, pomocy i palecie
  nie ma polecenia konta, a pakiet nie zawiera identyfikatora, brokera, sekretu
  ani tokenu Google.
- W `Ctrl+Alt+S` działania Spotify i TIDAL nie przechodzą do nieobecnych sesji
  wewnątrz AMC. Prawa strzałka i pełne menu udostępniają publiczne wyszukiwanie
  na stronach usług. Enter i `Ctrl+Enter` korzystają z tej samej zasady, a
  `Ctrl+Shift+C` kopiuje łącze wyszukiwania.

### Uzasadnienie i decyzje

- Korekta użytkownika z 28.10.2026 zastępuje przygotowany wcześniej, lecz
  niewydany wariant przyszłego Public z uśpioną możliwością Google. Brak
  konfiguracji nie jest wystarczający: możliwość została usunięta z jawnego
  profilu, kontraktu Launchera i procesu pakowania.
- Starszy AMC Public może mieć tę możliwość w historycznym manifeście. Nowy
  aktualizator przyjmuje ją wyłącznie jako zgodny stan wejściowy migracji;
  nowy manifest i nowa instalacja już jej nie deklarują.
- Techniczny numer podniesiono do `0.1.0-alpha.439`, aby artefaktu Public nie
  pomylić z opublikowanym Testem `0.1.0-alpha.438`. Kod użytkowy pozostaje jego
  bazą, a zmiany `alpha.439` dotyczą ograniczeń profilu Public i wydania.

### Testy i wynik

- Celowane testy bez widocznego interfejsu: **109/109 zaliczone**. Obejmują
  profile, rozpoznawanie, fizyczną zawartość paczki, brak konfiguracji Google,
  bramkę publikacji, snapshot źródeł i kompletność katalogów językowych.
- Launcher i aktualizator: **37/37 zaliczonych**, w tym aktualizacja ze
  starszego Public z historyczną możliwością Google.
- Pełnej regresji GUI nie powtarzano. Bazą jest pełna regresja i sprawdzony
  pakiet wydania Test `alpha.438`; korekta Public ma celowane testy różnic.
- Czysta paczka Public `e44af269`: **zbudowana** z 1152 plików; pełna cicha
  kontrola integralności i kontrolowany start interfejsu/hosta: **zaliczone**.
- Instalator: **zbudowany**; skan sekretów: **zaliczony**. Sucha bramka
  publikacji kanału `stable`: **zaliczona**.
- GitHub Release `public-2026.10.28.2109`: **opublikowany** jako stabilny i
  odczytowo zweryfikowany. GitHub potwierdził commit, trzy zasoby w stanie
  `uploaded`, rozmiar 124342770 bajtów i digest instalatora zgodny z lokalnym
  SHA-256.
- Nie zbudowano dodatkowej paczki porównawczej Test, ponieważ uruchomiony AMC
  Test i dwa oczekujące aktualizatory nie mogły zostać bezpiecznie przerwane.
  Parytet oraz różnice profili pokrywają testy celowane; instalacja użytkownika
  pozostała bez zmian.

### Znane ograniczenia

- Spotify i TIDAL nie są sesjami profilu Public. Publiczne wyszukiwanie WWW nie
  obiecuje dokładnego dopasowania katalogowego ani sterowania aplikacją
  producenta.
- Instalator nie jest jeszcze podpisany cyfrowo; Windows może pokazać
  ostrzeżenie reputacyjne. Integralność jest weryfikowana przez SHA-256.

### Wersja i commit

- Wersja techniczna: `0.1.0-alpha.439`; wersja widoczna:
  `28.10.2026, 21:09`.
- Bazowe wydanie Test: `0.1.0-alpha.438`, commit `9b2f6246`.
- Commit źródłowy Public:
  `a4bedbd62e15e69701f6e358c421dee7730216c6`.
- Identyfikator paczki: `e44af269`.
- Instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Public 0.1.0-alpha.439 kandydat 2026-10-28\instalator\AMC-Public-2026-10-28-2109.exe`.
- SHA-256 instalatora:
  `af2c5f3ba799a6d5937b98cb773d66d5bbb7c24bd2cfb92747024171063362af`.
- Tag: `public-2026.10.28.2109`; wydanie stabilne, nie prerelease.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/public-2026.10.28.2109`.
- Instalatora ani aktualizatora nie uruchamiano; nie wykonano instalacji ani
  ręcznego odbioru z NVDA.

## Korekta profilu Public po wydaniu alpha.439 — 28.10.2026

### Materiał do dokumentacji użytkownika

- Korekta użytkownika z 28.10.2026 zastępuje profil usług `alpha.439`:
  w AMC Public mają być WiiM i Frontier Smart, a nie ma być Sonosa, Spotify
  ani TIDAL-a.
- Menedżer sesji, menu, pomoc, paleta i paczka mają pokazywać i zawierać
  wyłącznie ten zakres. Aktualizacja z `alpha.439` usuwa moduł Sonosa.

### Uzasadnienie i decyzje

- Nie podmieniamy istniejącego tagu ani pliku `alpha.439`. Materialnie inny
  instalator otrzymuje numer `0.1.0-alpha.440` i nowe stabilne wydanie.
- Aktualizator zachowuje wąską zgodność ze starszym manifestem Public
  zawierającym Sonosa, wyłącznie po to, aby bezpiecznie przejść do nowego
  profilu z Frontier Smart.

### Testy i wynik

- Celowane testy profilu, menu/palety, Frontier Smart, paczki, instalatora i
  wersji: **100/100 zaliczonych**.
- Launcher i aktualizator: **38/38 zaliczonych**, w tym migracja z manifestu
  Public zawierającego Sonosa do profilu z Frontier Smart.
- Czysta paczka `8da5168e`: **zbudowana** z 1152 plików. Manifest i fizyczna
  zawartość potwierdzają Frontier Smart oraz brak Sonosa, Spotify, TIDAL-a i
  zasobów OAuth; cicha integralność i kontrolowany start: **zaliczone**.
- Instalator: **zbudowany**; skan sekretów i sucha bramka kanału `stable`:
  **zaliczone**. Pełnej regresji GUI ani widocznych testów nie uruchamiano.
- GitHub Release `public-2026.10.28.2201`: **opublikowany** jako stabilny i
  odczytowo zweryfikowany. Zdalny commit, trzy zasoby w stanie `uploaded`,
  rozmiar i digest SHA-256 są zgodne z lokalnym wynikiem.
- Działającej instalacji AMC Test ani oczekujących aktualizatorów nie wolno
  przerywać na potrzeby tej korekty.

### Znane ograniczenia

- `alpha.439` pozostaje historycznym, opublikowanym wydaniem o błędnym
  zakresie usług. Zastępuje go `alpha.440`; starego instalatora nie należy
  dalej przekazywać jako najnowszego Public.
- Ręczny odbiór Frontier Smart i nawigacji z NVDA nie jest wykonywany przez
  niewidoczne testy profilu.

### Wersja i commit

- Wersja techniczna: `0.1.0-alpha.440`; wersja widoczna:
  `28.10.2026, 22:01`.
- Commit źródłowy:
  `eff12af289d0eaa81058d19bc17b2f418d415e28`; paczka `8da5168e`.
- Instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Public 0.1.0-alpha.440 korekta 2026-10-28\instalator\AMC-Public-2026-10-28-2201.exe`.
- Rozmiar: 124351672 bajty. SHA-256:
  `e1c111cf4a0eb8432cfb0d2f8682443cd5bc3be481525c37108d0c6ea2485b0f`.
- Tag: `public-2026.10.28.2201`; wydanie stabilne, nie prerelease.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/public-2026.10.28.2201`.
- Instalatora nie uruchamiano ani nie instalowano; działający AMC Test i
  oczekujące aktualizatory pozostały nietknięte.

## Wydanie Public na bazie Test alpha.442 — 29.10.2026

### Materiał do dokumentacji użytkownika

- AMC Public otrzymuje zmiany wspólnego kodu wydane wcześniej w AMC Test
  `alpha.441` i `alpha.442`: osobne Ustawienia sesji oraz poprawioną zmianę
  języka polskiego, angielskiego i zgodnego z systemem bez restartu.
- Zakres usług Public pozostaje bez zmian: dostępne są WiiM i Frontier Smart,
  natomiast Sonos, Spotify i TIDAL nie są pokazywane ani pakowane.
- Profil Public nadal nie zawiera logowania Google do YouTube, prywatnych
  tokenów ani konfiguracji OAuth Google.

### Uzasadnienie i decyzje

- Polecenie użytkownika z 29.10.2026 rozpoczyna nowe stabilne wydanie z
  najnowszej opublikowanej bazy AMC Test `0.1.0-alpha.442`.
- Wydanie otrzymuje nowy numer `0.1.0-alpha.443`; nie nadpisuje poprzedniego
  stabilnego `alpha.440` ani jego tagu i artefaktów.
- Oba profile nadal korzystają z jednego wspólnego kodu. Ograniczenia Public
  wynikają z profilu możliwości i kontroli fizycznej zawartości paczki.

### Testy i wynik

- Celowane testy bez widocznego interfejsu: **231/231 zaliczonych**. Zakres
  obejmuje profil i paczkę Public, Frontier Smart, instalator, publikację,
  języki oraz Ustawienia sesji.
- Launcher i aktualizator: **38/38 zaliczonych**, w tym migracja starszego
  Public z Sonosa na Frontier Smart i kontrola zgodności profilu.
- Czysta paczka Public `89473955`: **zbudowana** z 1085 plików. Manifest i
  fizyczna zawartość potwierdzają WiiM i Frontier Smart oraz brak Sonosa,
  Spotify, TIDAL-a i prywatnych zasobów OAuth. Pełna integralność i
  kontrolowany cichy start interfejsu oraz hosta: **zaliczone**.
- Instalator `AMC-Public-2026-10-29-0136.exe`: **zbudowany**; skan sekretów i
  sucha bramka kanału `stable`: **zaliczone**.
- GitHub Release `public-2026.10.29.0136`: **opublikowany** jako stabilny i
  niezależnie odczytowo zweryfikowany. Tag wskazuje dokładny commit źródłowy,
  trzy zasoby mają stan `uploaded`, a rozmiar i digest instalatora są zgodne z
  plikiem lokalnym. Bezparametrowy odczyt najnowszego stabilnego wydania
  wskazuje właśnie ten tag.
- Nie uruchamiano pełnej regresji GUI, instalatora ani widocznych testów
  przejmujących fokus. Działająca instalacja AMC Test pozostała nietknięta.

### Znane ograniczenia

- Automatyczne testy nie zastępują ręcznego odbioru z NVDA, w szczególności
  zmiany języka, Ustawień sesji i powrotu fokusu.
- Instalator nie jest podpisany cyfrowo; Windows może pokazać ostrzeżenie
  reputacyjne. Integralność potwierdzają zgodne lokalne i zdalne rozmiar oraz
  SHA-256.

### Wersja i commit

- Przygotowywana wersja techniczna: `0.1.0-alpha.443`.
- Bazowe wydanie Test: `0.1.0-alpha.442`, commit `2b772d06` wraz z późniejszym
  commitem dokumentacyjnym `66bdddf8`.
- Commit źródłowy:
  `367c46a1b6c24488789b3a730716f70651e4cbfb`; paczka `89473955`.
- Instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Public 0.1.0-alpha.443 wydanie 2026-10-29\instalator\AMC-Public-2026-10-29-0136.exe`.
- Rozmiar: 124371469 bajtów. SHA-256:
  `90ed3e558ae81a90ce768d7e0645a811fc00559f9273f61d2a8b282383462c33`.
- Tag: `public-2026.10.29.0136`; wydanie stabilne, nie prerelease.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/public-2026.10.29.0136`.
- Bezpośredni adres instalatora:
  `https://github.com/michalkasperczak/AMC/releases/download/public-2026.10.29.0136/AMC-Public-2026-10-29-0136.exe`.
- Instalatora nie uruchamiano ani nie instalowano; działający AMC Test pozostał
  bez zmian.

## Wersja i commit

- Wersja techniczna przygotowywanego kandydata: `0.1.0-alpha.434`.
- Bazowy stan przed zmianą: `73279fbb15a1cd34c6813f24e097e0467889a197`.
- Commit mechanizmu aktualizacji: `66118f651309efc711c2f090c2e0e4c42e37a80c`.
- Dokładna rewizja paczki i instalatora:
  `251e163afd1fe7697c3efe525f1fa284050cfebb`.
- Kandydat instalatora:
  `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.434 kandydat 2026-10-26\instalator\AMC-Test-2026-10-26-1954.exe`.
- SHA-256 instalatora:
  `b8a411799beb086fb17f0f1dfaca795fd37ee211fd12fde352fa309984554b8e`.
- Instalacja: `D:\Projekty Codex\AMC Python`.
- Instalacja kandydata: użytkownik potwierdził odbiór przed publikacją.
- Publikacja GitHub Release: wykonana 26.10.2026; wydanie wstępne
  `test-2026.10.26.1954`, tag i źródła na
  `251e163afd1fe7697c3efe525f1fa284050cfebb`.
- Pierwszy punkt migracji zdalnego `main` na kanoniczną linię Python:
  `219062c81e2526131e5bc76846ecac06cf5ea06d`; poprzedni punkt `main`
  `1f5dccb6` pozostaje w historii oraz w zweryfikowanym archiwum WPF.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.26.1954`.
- Wcześniejszy commit etapu źródłowego: `d87a27f6`
  (`build(release): rozdziel kanały Test i Public`).
- Commit kontraktu Quick App Switchera w instalatorze: `781f8283`
  (`feat(installer): offer Quick App Switcher with AMC Test`).
- Drugie wydanie testowe: wersja techniczna `0.1.0-alpha.435`, wersja widoczna
  dla użytkownika `27.10.2026, 00:57`, tag `test-2026.10.27.0057`.
- Commit źródłowy drugiego wydania:
  `4cb57da5a6bda7bb82696ff426c00f2b9f655704`.
- Kandydat drugiego instalatora:
  `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.435 kandydat 2026-10-27\instalator\AMC-Test-2026-10-27-0057.exe`.
- Adres drugiego wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.27.0057`.
- Rzeczywista aktualizacja przez oficjalny aktualizator, pełna kontrola
  integralności, ponowny start i zachowanie 421 kanałów: wykonane 27.10.2026.
- Kliknięcie polecenia aktualizacji w menu i ręczny odbiór dialogu z NVDA:
  niewykonane z powodu braku sterowania natywnym oknem w sesji automatyzacji.
- Trzecie wydanie testowe: wersja techniczna `0.1.0-alpha.436`, wersja widoczna
  dla użytkownika `27.10.2026, 02:33`, tag `test-2026.10.27.0233`.
- Commit źródłowy trzeciego wydania:
  `abfee285274810746caedd948aae4d9d7b44aafd`.
- Kandydat trzeciego instalatora:
  `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.436 kandydat 2026-10-27\instalator\AMC-Test-2026-10-27-0233.exe`.
- Adres trzeciego wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.10.27.0233`.
- Instalacja i aktualizacja lokalna: wykonane ręcznie przez użytkownika
  27.10.2026; pobranie, instalacja i start `0.1.0-alpha.436` przeszły, a
  mechanizm aktualizacji został odebrany jako działający.
- Następna, nieopublikowana korekta startu po aktualizacji: commit źródłowy
  `ae24e9df`; testy i build zaliczone, bez instalacji i bez wydania.
- Czwarte wydanie testowe: `0.1.0-alpha.437`, wersja widoczna
  `28.10.2026, 13:23`, tag `test-2026.10.28.1323`, commit `301c7fcf`.
- Piąte wydanie testowe: `0.1.0-alpha.438`, wersja widoczna
  `28.10.2026, 20:22`, tag `test-2026.10.28.2022`, commit `9b2f6246`.

## Automatyczne powiadamianie o nowej wersji — 29.10.2026

### Materiał do dokumentacji użytkownika

- AMC domyślnie sprawdza w tle dostępność nowej wersji pięć sekund po
  uruchomieniu. Kanał nadal wynika z instalacji: AMC Test sprawdza wyłącznie
  wydania Test, a AMC Public wyłącznie stabilne wydania Public.
- Gdy bieżąca wersja jest aktualna, sprawdzenie niczego nie mówi i nie zmienia
  fokusu. Gdy istnieje nowsza wersja, otwiera się to samo dostępne okno
  `Aktualizacja AMC`, które pokazuje wersję i opis zmian oraz oferuje
  `Aktualizuj AMC` albo `Zamknij okno aktualizacji`.
- Samo sprawdzenie nigdy nie pobiera ani nie instaluje pliku. Pobranie pełnego,
  zweryfikowanego instalatora rozpoczyna się dopiero po wybraniu
  `Aktualizuj AMC`.
- Ustawienie można wyłączyć na karcie `Aktualizacje` przełącznikiem
  `Automatycznie sprawdzaj dostępność nowej wersji AMC po uruchomieniu programu`.
  Ręczne polecenie `Pomoc > Sprawdź aktualizację AMC…` pozostaje dostępne.

### Uzasadnienie i decyzje

- Automatyczna droga deleguje do istniejącego, profilowanego klienta i tego
  samego dialogu co polecenie ręczne. Nie powstał drugi aktualizator ani drugi
  zestaw reguł bezpieczeństwa.
- Brak sieci podczas automatycznego sprawdzania jest zapisywany do dziennika,
  ale nie przerywa pracy ani mowy NVDA. Ręczne sprawdzenie nadal podaje błąd
  użytkownikowi.
- Jeżeli użytkownik ma otwarte inne okno modalne, automatyczne powiadomienie
  czeka i ponawia próbę po pięciu sekundach. Zapobiega to nakładaniu dialogu na
  wybór sesji lub Ustawienia i chroni przewidywalny fokus.
- Ręczne sprawdzenie zatrzymuje jeszcze nieuruchomione sprawdzenie automatyczne,
  aby nie pokazywać dwa razy tego samego wyniku.

### Testy i wynik

- Celowany zestaw mechanizmu aktualizacji, stanu, startu, tłumaczeń, Ustawień i
  aktualizacji składników: **119 zaliczonych, 1 kontrolowanie pominięty**.
- Sprawdzono osobno: wartość domyślną i trwały zapis ustawienia, ciszę przy
  aktualnej wersji, pokazanie dostępnego dialogu przy wersji nowszej, cichy
  błąd sieci, dokładnie jedno sprawdzenie po starcie oraz odroczenie przy innym
  modalu.
- Testy były bezokienne. Nie uruchamiano instalatora ani widocznego interfejsu.

### Znane ograniczenia

- Ręczny odbiór NVDA nowego pola Ustawień i automatycznie otwartego dialogu
  pozostaje do wykonania na zbudowanym kandydacie.
- Paczka i instalator są lokalnym kandydatem. Nie wykonano instalacji ani
  publikacji wydania.

### Wersja i commit

- Commit źródłowy: `9684e75789512e564f9cdd8066f2068934de88eb`.
- Kandydat `AMC Public — 29.10.2026, 19:24`, paczka `33e68ad6`: 1085
  kontrolowanych plików i manifest, 424 041 851 bajtów oraz zero plików spoza
  manifestu po kontrolowanym starcie.
- Instalator:
  `D:\Projekty Codex\AMC aktualizacje\AMC Public kandydat automatyczne aktualizacje 2026-10-29\instalator\AMC-Public-2026-10-29-1924.exe`.
- Rozmiar instalatora: 123 624 555 bajtów; SHA-256:
  `db26bc2ed863b5c104923e8ed8df143a807dd2d6d322532f1b907afc2ec0241f`.
- Kontrolowany start paczki i ponowny start w bramce instalatora: zaliczone.
  Lokalna bramka wydania `stable` i skan sekretów: zaliczone.
- GitHub Release `public-2026.10.29.1924`: **opublikowany** jako stabilny i
  odczytowo zweryfikowany. Tag wskazuje dokładny commit źródłowy, wszystkie
  trzy zasoby mają stan `uploaded`, a rozmiar i digest SHA-256 instalatora są
  zgodne z plikiem lokalnym. Bezparametrowy odczyt najnowszego stabilnego
  wydania wskazuje właśnie ten tag.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/public-2026.10.29.1924`.
- Bezpośredni adres instalatora:
  `https://github.com/michalkasperczak/AMC/releases/download/public-2026.10.29.1924/AMC-Public-2026-10-29-1924.exe`.
- Instalatora nie uruchamiano ani nie instalowano; kanału AMC Test nie
  zmieniono.

## Wydanie AMC Test 0.1.0-alpha.497 — 13.11.2026

### Materiał do dokumentacji użytkownika

- Wydanie dodaje dziesiątą sesję Apple Music tylko do odczytu, `Ctrl+0`, F11
  do sprawdzania aktualizacji i `Ctrl+F11` do zgłoszeń, poprawki lokalnego
  Sonosa oraz bezpieczniejsze przejście ze starszego układu silnika.
- Aktualizator wybiera host zadeklarowany w zweryfikowanym manifeście i
  transakcyjnie usuwa stare pliki wyłącznie z katalogów silnika. Dane kont,
  ustawienia i lokalne pliki poświadczeń pozostają poza tym sprzątaniem.

### Uzasadnienie i decyzje

Pierwsza instalacja kandydata ujawniła, że stare `host-next` i biblioteki .NET
pozostawione w `host` mogą zmienić wybór lub sposób ładowania silnika. Publikację
zatrzymano. Wydanie powstało dopiero po dwóch poprawkach, testach regresji,
ponownej czystej budowie, instalacji i zaliczonym starcie z katalogu użytkowego.

### Testy i wynik

- klient hosta: 44/44; test transakcyjnego usuwania starych plików: zaliczony;
- mały zestaw launchera: 44/45; jedyny zastany błąd kanału przekazania do
  działającej instancji powtórzył się dwukrotnie i nie dotyczy aktualizacji ani
  hosta;
- pełna integralność oraz kontrolowany start kandydata i instalacji: zaliczone;
- zwykłe uruchomienie instalacji: jeden launcher, runtime i LiteHost, tytuł
  `AMC Test — 13.11.2026, 00:28`;
- sucha bramka wydania, push źródeł, publikacja prerelease i odczytowa
  weryfikacja trzech załączników: zaliczone.

### Znane ograniczenia

Instalator nie był uruchamiany ręcznie po zbudowaniu, ponieważ bieżącą kopię
zainstalował wcześniej ten sam zweryfikowany mechanizm pakietu. Ręczny odbiór
nowych funkcji z NVDA oraz prawdziwym Sonosem pozostaje osobnym etapem.

### Wersja i commit

- wersja techniczna `0.1.0-alpha.497`, użytkowa `13.11.2026, 00:28`;
- commit `f38ea76803a01b095e06892866a31082613eca32`;
- paczka `c992d212`, 3767 plików, `sourceDirty=false`;
- instalator `AMC-Test-2026-11-13-0028.exe`, 73 219 776 bajtów, SHA-256
  `16542007d32c9a89c64127c06ccc91c5c58d44f0d43088fd76b273ad9229846e`;
- tag `test-2026.11.13.0028`, prerelease, trzy załączniki `uploaded`;
- `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.13.0028`.

## Wydanie AMC Test 0.1.0-alpha.498 — 13.11.2026

### Zachowanie użytkowe

- Ujednolicono wybór sąsiedniego wiersza i końcową mowę NVDA po zmianach
  Biblioteki, Ulubionych, playlist oraz kolejek w TIDAL, Spotify, Radiu i
  lokalnych kolekcjach.
- Sonos po zmianie głośności mówi tylko wartość potwierdzoną przez urządzenie;
  Spotify udostępnia działający suwak głośności w odtwarzaczu.
- Apple Music `Ctrl+Shift+C` kopiuje publiczny URL albo nazwany link
  wyszukiwania; `Ctrl+F` działa w obrębie wczytanej strony.
- Pełne pobieranie i wycinki Radio Archiwisty nie przejmują fokusu i pozwalają
  dalej używać AMC.

### Testy i ograniczenia

- Szeroka regresja: 2724 zaliczone, 117 pominiętych i 34 podtesty zaliczone;
  izolowane GUI Radio Archiwisty: 3/3.
- Dwie jawnie pozostawione kontrole kompletności angielskiego katalogu nie
  przeszły z powodu wcześniejszego długu 127 tekstów i pięciu pól Ustawień.
- Odtwarzanie, `Shift+A`, zapis i Ulubione Apple Music nie należą do tego
  wydania; test oryginalnej aplikacji Apple Music odłożono decyzją użytkownika.

### Wydanie i instalacja

- commit źródłowy `6e34a3fedc78642ab86e83ab16202b707a1293b2`;
- paczka `8fce9c2a`, 4256 plików, `sourceDirty=false`, wersja użytkowa
  `13.11.2026, 02:17`;
- instalator `AMC-Test-2026-11-13-0217.exe`, 165 818 571 bajtów, SHA-256
  `3f3c020db76f66bbbcf0c4f0df78032442a10f2bed839a534e63b88967d996ea`;
- GitHub prerelease `test-2026.11.13.0217`: tag wskazuje dokładny commit,
  wszystkie trzy zasoby mają stan `uploaded`, a rozmiar i digest instalatora
  są zgodne z plikiem lokalnym;
- zainstalowano przez transakcyjny aktualizator w
  `D:\Projekty Codex\AMC Python`; kontrolowany start zaliczony, a zwykły start
  uruchomił po jednej instancji launchera, runtime i LiteHosta z tytułem
  `AMC Test — 13.11.2026, 02:17`;
- adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.13.0217`.

## Wydanie AMC Test 0.1.0-alpha.499 — 13.11.2026

### Zachowanie użytkowe

- Apple Music: `Ctrl+Shift+L` dodaje katalogowy utwór lub album do Biblioteki
  i usuwa biblioteczny utwór albo album z bezpośredniej listy Utwory lub
  Albumy. Menu, paleta, skrót i pomoc korzystają ze wspólnego polecenia.
- TIDAL: po zapisie albumu, wykonawcy lub playlisty z wyszukiwania AMC
  unieważnia tylko właściwą kolekcję i po zamknięciu wyszukiwarki odświeża
  widoczną listę bez zbędnej mowy i bez utraty zaznaczenia.
- Bramka instalatora przekazuje ścieżki ze spacjami do Inno Setup jako osobne
  argumenty. Test strukturalny profili instalatora: 27/27 zaliczone.

### Testy, build i instalacja

- Szeroka regresja: 2731 testów i 34 podtesty zaliczone, 117 pominiętych;
  sześć jawnych wyłączeń obejmowało dwa zastane długi językowe i cztery testy
  prawdziwego wxPython wykonane osobno.
- Commit funkcji `8ef22969fed875e34f3759a0b3187bc93e4a38c6`; końcowy commit wydania
  `7ef8396849eff477e618920dce39392371381e03`.
- Paczka `cbf655af`, 3797 plików, `sourceDirty=false`, wersja użytkowa
  `13.11.2026, 14:58`; kontrolowany start paczki i stałej instalacji zaliczony.
- Zainstalowano transakcyjnie i uruchomiono jedną instancję z
  `D:\Projekty Codex\AMC Python`; tytuł okna
  `AMC Test — 13.11.2026, 14:58`.

### GitHub

- Instalator `AMC-Test-2026-11-13-1458.exe`: 73 278 127 bajtów, SHA-256
  `4cd16ddb1ce2273dfdff60c18bc3551bd239118fd3e69a84d2cea783bc56ea83`.
- Prerelease `test-2026.11.13.1458` wskazuje końcowy commit. Instalator, plik
  integralności i manifest kanału aktualizacji mają stan `uploaded`; zdalny
  rozmiar i digest instalatora są zgodne z lokalnymi.
- Adres wydania:
  `https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.13.1458`.

## Wydanie AMC Test 0.1.0-alpha.500 — 13.11.2026

- Użytkownik poprosił o publikację GitHub, a następnie wybrał samodzielną
  aktualizację. Działającej instalacji `D:\Projekty Codex\AMC Python` nie
  zamykano, nie podmieniano i nie uruchamiano ponownie.
- Zakres od poprzednio zainstalowanego `alpha.499`: jawne otwieranie utworu w
  oryginalnym Apple Music, transport jego bieżącej sesji oraz kandydat
  odtwarzania po Enterze w izolowanym WebView2. Funkcja Apple Music jest tylko
  w profilu Test. Rzeczywisty dźwięk, fokus NVDA i jakość strumienia wymagają
  osobnej żywej próby, gdy użytkownik zwolni komputer.
- Celowane testy Apple Music i pakowania: 67/67; kontrakt C#: 1 zestaw.
  `test_i18n.py`: 12 zaliczonych, 1 pominięty, 2 zastane błędy kompletności
  angielskiego katalogu. Kompilacja WebView2: 0 błędów, 1 ostrzeżenie
  `WindowsBase`. Pełna kontrola integralności paczki i ukryty start interfejsu
  oraz hosta: zaliczone. Instalatora ani prawdziwego odtwarzania nie uruchamiano.
- Commit wydania: `16eb05b5976d57b42c48c3be8df11062f52a70ec`;
  paczka `288c4699`, 4729 plików, `sourceDirty=false`, profil Test,
  wersja użytkowa `13.11.2026, 19:52`.
- Instalator `AMC-Test-2026-11-13-1952.exe`: 218 486 032 bajty, SHA-256
  `ff33a276c1161e94b03f56f7437530d08e4885096bdca04eb7300e5fd01fe65f`;
  kontrola sekretów przeszła. Dodatki NVDA pozostały w wersjach Quick App
  Switcher 0.1.6 i AMC NVDA 0.4.2.
- Sucha bramka publikacji i właściwa publikacja przeszły. Prerelease
  `test-2026.11.13.1952` wskazuje dokładny commit; trzy zasoby mają stan
  `uploaded`. Zdalny rozmiar i digest instalatora odpowiadają lokalnemu.
  Adres: https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.13.1952
