# AMC — katalog wszystkich plików zadań dla Hermesa

Stan spisu: 16.11.2026. To nawigacja po plikach `docs/zadania`, a nie zgoda
na równoczesne uruchomienie wszystkich prac. Szczegółowy status, korekty
użytkownika, wyniki testów i następny krok są w każdym pliku. Krótki stan
przy pozycji jest odczytany z początku pliku i może być historyczny. Nowsze
korekty w tym samym pliku mają pierwszeństwo przed wcześniejszymi akapitami.
Bieżące priorytety i stan wydania są w
[pełnym przekazaniu](../../HERMES_AMC_PRZEKAZANIE_2026-11-16_PL.md).
Kanoniczny rejestr to [README.md](README.md).

**Uzupełnienie 19.11.2026, poza historycznymi licznikami spisu z 16.11:**
[Wyjścia audio: domyślne Windows, TIDAL/Spotify i jawne wznowienie po utracie](AUDIO-RECOVERY-20261119-kontrakt.md)
— nowe zlecenie wdrożenia #30 oraz scenariusza zapamiętanego Denona; pauza
ochronna zostaje, ponowna Spacja/Enter bez zmiany stacji ma odzyskać wyjście.
Niezależny `4a791c2` naprawił potwierdzoną ciszę przy wyłączonej ochronie.
Rodzic zweryfikował 747 źródeł, ale test hosta zwrócił exit 1; integracja
wstrzymana do rozstrzygnięcia przez `deleg_96958dcb`. Politykę Chrome
`06bb4b0` celowanie odbiera `deleg_fd64ca45`: zwykła zmiana a utrata wyjścia
oraz sygnał utraty ręcznego endpointu. Bez odbioru sprzętu, GUI i instalatora.

[Radio Archiwista: Ctrl+F i zwykły wybór po nazwie](RARCH-HOURS-20261119-diagnoza.md)
— Ctrl+F w źródłach, 10 testów PASS; po korekcie Michała zwykła lista plików,
bez specjalnego aliasu 7 → 07. Test 18.11, 03:37 nadal najnowszy opublikowany.
[Pełny kontrakt odtwarzacza: historia, zakładki, procenty](RARCH-PLAYER-20261119-kontrakt.md)
— ograniczony odbiór offline zakończony; nie wykryto luki produkcji, poprawiono
niepełne atrapy i dodano regresje handlerów. Integracja `8ff468c`; rodzic po
integracji 41 Python i 3 zestawy C# PASS. Bez GUI/instalacji/publikacji.

[Zgłoszenia Huberta: #11, #25, #26, #28 — ocena i rekomendowana kolejność](HUBERT-20261119-triage-zgloszen.md).
Analiza zakończona; Michał następnie zatwierdził realizację po kolei.
#25 dane/trwałość odebrano i zintegrowano w `adf6e51`: cztery celowane trasy
Windows, build i niezależny przegląd zaliczone. Harmonogram #25 również odebrano
i zintegrowano w `d113fae`; zgodność no-arg, kontrolowany timer anulowania,
rzeczywisty dispatcher i niezależny review zaliczone. #26 odebrano i zintegrowano
w `36ba1fe` z poprawką `516e5b3` (dziecko utrzymujące pipe'y po wyjściu rodzica).
Rodzic: dziewięć źródeł finalnego stagingu zgodnych, po integracji celowany
Windows smoke exit 0. Dalej #28, z uwzględnieniem odbioru wspólnego audio.
[Stan rozdziałów i dowody](HUBERT26-CHAPTERS-20261119-plan.md).
Bez instalacji i publikacji. [Odbiór harmonogramu](HUBERT25-SCHEDULE-20261119-akceptacja.md).
[Odbiór P0 i dokładne granice](HUBERT25-P0-20261119-akceptacja.md).
Starych gałęzi PR #27/#29 nie scalano; rozwiązanie #26 jest już w bieżącej bazie,
a #28 pozostaje odrębnym zadaniem.

Łącznie: 179 plików w 70 grupach: 173 zastanych i 6 jawnie oznaczonych rekonstrukcji.

## Spis grup

- [A11Y](#a11y) — 4
- [AM](#am) — 8
- [AMC](#amc) — 17
- [AMC02](#amc02) — 1
- [AMC04](#amc04) — 1
- [AMC05](#amc05) — 1
- [AMC08](#amc08) — 1
- [AMC09](#amc09) — 1
- [AMC10](#amc10) — 3
- [AMC12](#amc12) — 1
- [AMC13](#amc13) — 1
- [AMC14](#amc14) — 1
- [ARC](#arc) — 1
- [AUDIO](#audio) — 1
- [AUTH](#auth) — 1
- [BACKUP](#backup) — 1
- [BLUOS](#bluos) — 1
- [CAST](#cast) — 2
- [DOWNLOADS](#downloads) — 3
- [ECOSYSTEM](#ecosystem) — 1
- [EDIT](#edit) — 1
- [FRONTIER](#frontier) — 1
- [HIST](#hist) — 1
- [HUBERT](#hubert) — 1
- [I18N](#i18n) — 1
- [INDEKS](#indeks) — 1
- [KEY](#key) — 1
- [LIC](#lic) — 1
- [LIST](#list) — 4
- [MAC](#mac) — 1
- [MENU](#menu) — 1
- [MIRROR](#mirror) — 1
- [MODAL](#modal) — 1
- [NAME](#name) — 1
- [NAV](#nav) — 1
- [NVDA](#nvda) — 4
- [PERF](#perf) — 2
- [PLAY](#play) — 2
- [PLAYER](#player) — 2
- [PLAYLIST](#playlist) — 2
- [POD](#pod) — 9
- [PODCAST](#podcast) — 1
- [PODRAD](#podrad) — 1
- [PRESET](#preset) — 1
- [PUBLIC](#public) — 2
- [QUEUE](#queue) — 1
- [RADIO](#radio) — 12
- [RADIOBROWSER](#radiobrowser) — 2
- [RARCH](#rarch) — 4
- [REC](#rec) — 2
- [RECOG](#recog) — 4
- [REL](#rel) — 13
- [RELEASE](#release) — 5
- [SCHED](#sched) — 1
- [SEARCH](#search) — 2
- [SESSION](#session) — 3
- [SET](#set) — 1
- [SOCIAL](#social) — 1
- [SPOT](#spot) — 1
- [SPOTIFY](#spotify) — 8
- [STAB](#stab) — 1
- [STR](#str) — 1
- [TEST](#test) — 3
- [TID](#tid) — 5
- [TIDAL](#tidal) — 2
- [TITLE](#title) — 1
- [TSH](#tsh) — 1
- [UNDO](#undo) — 1
- [UPDATE](#update) — 3
- [YT](#yt) — 6

## Pliki według grup

### A11Y

- [A11Y-20261030-nvda-end-pasek-stanu.md](A11Y-20261030-nvda-end-pasek-stanu.md) — A11Y-20261030 — odczyt paska stanu przez NVDA+End. Stan przy nagłówku: W trakcie — poprawki źródeł, testy i kompilacja; bez instalacji i ręcznego odbioru NVDA..
- [A11Y-20261110-nvda-end-lista.md](A11Y-20261110-nvda-end-lista.md) — A11Y-20261110 — NVDA+End odczytuje „Lista” zamiast parametrów.
- [A11Y-20261110-powiazania-status-spotify.md](A11Y-20261110-powiazania-status-spotify.md) — AMC Test i Public — nawigacja po powiązaniach i status Spotify.
- [A11Y-COLLECTIONS-20261113-fokus-mowa-sortowanie.md](A11Y-COLLECTIONS-20261113-fokus-mowa-sortowanie.md) — A11Y-COLLECTIONS-20261113 — fokus i mowa po zmianach kolekcji. Stan przy nagłówku: wydane, zainstalowane i uruchomione w AMC Test alpha.498..

### AM

- [AM-001-nieoficjalne-konto-i-biblioteka.md](AM-001-nieoficjalne-konto-i-biblioteka.md) — AM-001 — nieoficjalne konto i Biblioteka Apple Music. Stan przy nagłówku: Kod GUI AMC Test gotowy i przetestowany — żywe logowanie, DPAPI, stronicowany odczyt Biblioteki, cztery kategorie i playlisty są podłączone; odtwarzanie pozostaje w AM-002.
- [AM-002-osadzony-odtwarzacz-music-apple-com.md](AM-002-osadzony-odtwarzacz-music-apple-com.md) — AM-002 — izolowany odtwarzacz music.apple.com. Stan przy nagłówku: 14.11.2026 — wycofanie osadzonego odtwarzania opublikowane w AMC Test alpha.507, zainstalowane i uruchomione; bez odbioru Apple Music z NVDA.
- [AM-003-sesja-i-dostepny-interfejs.md](AM-003-sesja-i-dostepny-interfejs.md) — AM-003 — sesja Apple Music i dostępny interfejs AMC. Stan przy nagłówku: Sesja i pierwszy zapis Biblioteki gotowe w źródłach; odtwarzanie AM-002 nadal oczekuje.
- [AM-004-oficjalny-musickit-i-konto-developer.md](AM-004-oficjalny-musickit-i-konto-developer.md) — AM-004 — oficjalny MusicKit i konto Apple Developer. Stan przy nagłówku: Oczekuje — nie kupować członkostwa przed wynikiem prywatnego pilota.
- [AM-005-czlonkostwo-biblioteki-apple-music.md](AM-005-czlonkostwo-biblioteki-apple-music.md) — AM-005 — członkostwo utworów i albumów w Bibliotece Apple Music. Stan przy nagłówku: kod i testy celowane gotowe; bez kompilacji, instalacji, publikacji.
- [AM-006-instrukcja-i-lista-zmian-szkic.md](AM-006-instrukcja-i-lista-zmian-szkic.md) — AM-002 i AM-006 — projekt instrukcji i listy zmian.
- [AM-006-przekazanie-utworu-do-apple-music-windows.md](AM-006-przekazanie-utworu-do-apple-music-windows.md) — AM-006 — przekazanie utworu do Apple Music dla Windows. Stan przy nagłówku: jawne przekazanie do oryginalnej aplikacji i nowy Enter w AM-002 opublikowano w AMC Test alpha.500; użytkownik aktualizuje ręcznie, odbiór odłożony..
- [AM-007-wyszukiwanie-i-listy-150.md](AM-007-wyszukiwanie-i-listy-150.md) — AM-007 — wyszukiwanie i nawigacja Apple Music. Stan przy nagłówku: źródła, testy celowane, pakiet, instalator i wydanie wstępne GitHub.

### AMC

- [AMC-20261108-wspolne-wydanie.md](AMC-20261108-wspolne-wydanie.md) — AMC-20261108 — wspólne wydanie Test i Public.
- [AMC-20261109-Soloist-Lossless-i-wydanie.md](AMC-20261109-Soloist-Lossless-i-wydanie.md) — AMC Test i Public — Soloist, Lossless i aktualizacja. Stan przy nagłówku: 9 listopada 2026. Użytkownik zlecił włączenie Soloist z jakością Spotify Lossless oraz wydanie zbiorczych aktualizacji Test i Public. Zastępuje to decyzję z 9 listopada w AMC-Test-.
- [AMC-INSTALLER-UPDATE-GATE-20261110.md](AMC-INSTALLER-UPDATE-GATE-20261110.md) — AMC — fałszywy błąd integralności podczas automatycznej aktualizacji.
- [AMC-TEST-2026-10-23-1124-instalacja-zbiorcza.md](AMC-TEST-2026-10-23-1124-instalacja-zbiorcza.md) — AMC Test — instalacja zbiorcza 23.10.2026, 11:24.
- [AMC-TEST-2026-10-23-1256-korekta-spotify-youtube-frontier.md](AMC-TEST-2026-10-23-1256-korekta-spotify-youtube-frontier.md) — AMC Test — korekta Spotify, YouTube i Frontier 23.10.2026, 12:56.
- [AMC-TEST-2026-10-23-1514-youtube-oauth.md](AMC-TEST-2026-10-23-1514-youtube-oauth.md) — AMC Test 23.10.2026, 15:14 — klient OAuth YouTube. Stan przy nagłówku: Zbudowane, zainstalowane i uruchomione, lecz odrzucone w ręcznym odbiorze: polecenie konta nie było widoczne w palecie ani w menu..
- [AMC-TEST-2026-10-23-1542-youtube-command-wire-fix.md](AMC-TEST-2026-10-23-1542-youtube-command-wire-fix.md) — AMC Test 23.10.2026, 15:42 — widoczne konto YouTube. Stan przy nagłówku: Zbudowane, atomowo zainstalowane i uruchomione; automatyczna.
- [AMC-TEST-2026-10-23-1630-spotify-output-youtube-oauth.md](AMC-TEST-2026-10-23-1630-spotify-output-youtube-oauth.md) — AMC Test 23.10.2026, 16:30 — wyjście Spotify i rzetelny wynik OAuth YouTube. Stan przy nagłówku: Zbudowane, atomowo zainstalowane i uruchomione; automatyczny.
- [AMC-TEST-2026-10-23-2220-spotify-tempo-frontier-pin.md](AMC-TEST-2026-10-23-2220-spotify-tempo-frontier-pin.md) — AMC Test 23.10.2026, 22:20 — tempo Spotify i PIN Frontier Smart. Stan przy nagłówku: Zainstalowane i uruchomione do ręcznego odbioru. Nieopublikowane..
- [AMC-TEST-2026-10-23-2241-frontier-pin-spotify-start.md](AMC-TEST-2026-10-23-2241-frontier-pin-spotify-start.md) — AMC Test 23.10.2026, 22:41 — bezpieczne usuwanie PIN-u i informacja Spotify. Stan przy nagłówku: zbudowane, atomowo zainstalowane i uruchomione; automatyczne.
- [AMC-TEST-2026-10-23-youtube-secret-frontier-choice.md](AMC-TEST-2026-10-23-youtube-secret-frontier-choice.md) — AMC Test — diagnoza sekretu YouTube i wyboru Frontier. Stan przy nagłówku: Paczka zbudowana, zainstalowana i uruchomiona; oczekuje na ręczny odbiór YouTube oraz Frontier z NVDA..
- [AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md](AMC-TEST-2026-10-24-0021-youtube-refresh-shortcuts.md) — AMC Test 24.10.2026, 00:21 — trwałe usunięcia i kolejka YouTube. Stan przy nagłówku: zbudowane, atomowo zainstalowane i uruchomione; automatyczne.
- [AMC-TEST-2026-10-24-ctrl-shift-i-frontier-stabilnosc.md](AMC-TEST-2026-10-24-ctrl-shift-i-frontier-stabilnosc.md) — AMC Test — zbiorcze transmisje YouTube i stabilność Frontier Smart. Stan przy nagłówku: zbudowane, atomowo zainstalowane i uruchomione; automatyczne bramki.
- [AMC-TEST-2026-10-24-ZAMKNIECIE-SESJI-23-24.md](AMC-TEST-2026-10-24-ZAMKNIECIE-SESJI-23-24.md) — AMC Test — zamknięcie sesji 23–24.10.2026. Stan przy nagłówku: bezpieczny punkt wznowienia; bieżąca wersja AMC Test jest.
- [AMC-Test-alpha487-Soloist-20261109.md](AMC-Test-alpha487-Soloist-20261109.md) — AMC Test alpha.487 — Soloist, Sygnalista i lokalny Sonos.
- [AMC-Test-alpha487-szkic-wydania.md](AMC-Test-alpha487-szkic-wydania.md) — AMC Test — szkic następnej aktualizacji.
- [AMC-TEST-ZAWIESZENIE-20261110.md](AMC-TEST-ZAWIESZENIE-20261110.md) — AMC Test — zawieszenie interfejsu po długiej pracy (10.11.2026).

### AMC02

- [AMC02-listy-nagrywania-i-historii.md](AMC02-listy-nagrywania-i-historii.md) — AMC02 — krótkie listy nagrywania i historia plików. Stan przy nagłówku: Uogólniona korekta pojedynczego wiersza i zapisu Ctrl+H jest.

### AMC04

- [AMC04-nawigacja-zakladki-rozdzialy-historia.md](AMC04-nawigacja-zakladki-rozdzialy-historia.md) — AMC04 — zakładki, rozdziały i historia nawigacji. Stan przy nagłówku: Rozdziały podcastów oraz zakładki i rozdziały Spotify są.

### AMC05

- [AMC05-odtwarzany-element-na-listach.md](AMC05-odtwarzany-element-na-listach.md) — AMC05 — odtwarzany element na listach. Stan przy nagłówku: Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór NVDA.

### AMC08

- [AMC08-playlisty-spotify.md](AMC08-playlisty-spotify.md) — AMC08 — natywne playlisty Spotify. Stan przy nagłówku: Użytkownik potwierdził prawdziwe OAuth i skuteczne dodawanie do.

### AMC09

- [AMC09-menedzer-sesji.md](AMC09-menedzer-sesji.md) — AMC09 — Menedżer sesji i zwarta numeracja. Stan przy nagłówku: Menedżer, ujednolicone Ctrl+F5, Foldery Biblioteki oraz pełne.

### AMC10

- [AMC10-HERMES-AUDYT-SONOS-2026-10-20.md](AMC10-HERMES-AUDYT-SONOS-2026-10-20.md) — AMC10 — odczytowy audyt dawnego brokera Sonos.
- [AMC10-logowanie-sonos-i-publiczny-broker.md](AMC10-logowanie-sonos-i-publiczny-broker.md) — AMC10 — proste logowanie Sonos i publiczny broker.
- [AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md](AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md) — Lokalny Sonos w AMC Test — instrukcja i lista zmian (szkic). Stan przy nagłówku: szkic do korekty użytkownika po próbie z prawdziwym głośnikiem..

### AMC12

- [AMC12-profile-wydania-publicznego-i-testowego.md](AMC12-profile-wydania-publicznego-i-testowego.md) — AMC12 — profile wydania publicznego i testowego.

### AMC13

- [AMC13-timeshift-end-na-zywo.md](AMC13-timeshift-end-na-zywo.md) — AMC13 — End w Time Shifcie Radia wraca na żywo. Stan przy nagłówku: Rozszerzenie skoków procentowych zainstalowane w AMC Test;.

### AMC14

- [AMC14-foldery-i-integracja-powloki.md](AMC14-foldery-i-integracja-powloki.md) — AMC14 — foldery i integracja z powłoką Windows. Stan przy nagłówku: W trakcie — warstwa aplikacyjna zainstalowana w AMC Test;.

### ARC

- [ARC-001-wspolny-rdzen-polecen-cli-ai.md](ARC-001-wspolny-rdzen-polecen-cli-ai.md) — ARC-001 — wspólny rdzeń poleceń, CLI i lokalne sterowanie AI. Stan przy nagłówku: Oczekuje.

### AUDIO

- [AUDIO-BITRATE-20261105.md](AUDIO-BITRATE-20261105.md) — AUDIO-BITRATE-20261105 — bitrate w odtwarzaczu i radiu. Stan przy nagłówku: kod, testy, paczka i instalacja AMC Test alpha.480 gotowe 6 listopada 2026; odbiór NVDA na rzeczywistych źródłach nadal potrzebny..

### AUTH

- [AUTH-PUBLIC-20261106-logowanie-tidal-spotify.md](AUTH-PUBLIC-20261106-logowanie-tidal-spotify.md) — AUTH-PUBLIC-20261106 — logowanie TIDAL i Spotify dla odbiorcy Public.

### BACKUP

- [BACKUP-001-eksport-import-danych.md](BACKUP-001-eksport-import-danych.md) — BACKUP-001 — eksport i import danych AMC. Stan przy nagłówku: Wdrożone do AMC Test i automatycznie zweryfikowane; czeka na ręczny.

### BLUOS

- [BLUOS-001-sesja-bluos.md](BLUOS-001-sesja-bluos.md) — BLUOS-001 — osobna sesja BluOS / Bluesound. Stan przy nagłówku: Oczekuje — opracowanie źródłowe jest gotowe; przed implementacją potrzebny jest pomiar na sprzęcie użytkownika.

### CAST

- [CAST-001-google-cast.md](CAST-001-google-cast.md) — CAST-001 — Google Cast jako wyjście odtwarzania. Stan przy nagłówku: Zrobione w podstawowym zakresie — AMC Test alpha.465 wydany i zainstalowany 02.11.2026. Ograniczenia oraz dodatkowy odbiór w głównym AMC opisano poniżej..
- [CAST-001-instrukcja-i-lista-zmian-szkic.md](CAST-001-instrukcja-i-lista-zmian-szkic.md) — Google Cast w AMC — instrukcja i lista zmian do odbioru.

### DOWNLOADS

- [DOWNLOADS-001-historia-pobierania.md](DOWNLOADS-001-historia-pobierania.md) — DOWNLOADS-001 — wspólna historia pobierania. Stan przy nagłówku: W trakcie ręcznego odbioru NVDA; kod, pełne testy, paczka,.
- [DOWNLOADS-002-schowek-historii-pobran.md](DOWNLOADS-002-schowek-historii-pobran.md) — DOWNLOADS-002 — schowek Historii pobrań. Stan przy nagłówku: kod, testy, pakiet, instalator, wydanie GitHub i lokalna instalacja.
- [DOWNLOADS-003-folder-radioarchiwisty-i-naprawa-historii.md](DOWNLOADS-003-folder-radioarchiwisty-i-naprawa-historii.md) — DOWNLOADS-003 — folder Radio Archiwisty i naprawa Historii pobrań. Stan przy nagłówku: źródła, testy celowane, obie paczki, instalatory i wydania GitHub.

### ECOSYSTEM

- [ECOSYSTEM-001-Blindradio-rozpoznawanie.md](ECOSYSTEM-001-Blindradio-rozpoznawanie.md) — Blindradio — pomysły dla rozpoznawania utworów w AMC Test. Stan przy nagłówku: Zrobione. Data przeglądu: 4.11.2026. Źródło: [Blindradio](https://github.com/serrebidev/Blindradio), główna gałąź, opis wersji 0.2.56 w [changelogu](https://github.com/serrebidev/B.

### EDIT

- [EDIT-001-zwalnianie-pliku-przed-wycinaniem.md](EDIT-001-zwalnianie-pliku-przed-wycinaniem.md) — EDIT-001 — zwalnianie pliku przed wycinaniem. Stan przy nagłówku: Zrobione.

### FRONTIER

- [FRONTIER-001-frontier-smart-fsapi.md](FRONTIER-001-frontier-smart-fsapi.md) — FRONTIER-001 — Frontier Smart przez FSAPI. Stan przy nagłówku: W trakcie — rzeczywisty Teufel RADIO 3SIXTY łączy się i pokazuje.

### HIST

- [HIST-REL-20261109-nazwy-i-linki.md](HIST-REL-20261109-nazwy-i-linki.md) — HIST-REL-20261109 — nazwy i kopiowanie celów z historii. Stan przy nagłówku: kod i testy celowane gotowe; bez budowy, instalacji i odbioru NVDA. Zgłoszenie i korekta użytkownika: 9.11.2026..

### HUBERT

- [HUBERT-20261109-MP3-i-kolejka.md](HUBERT-20261109-MP3-i-kolejka.md) — Zgłoszenia Huberta z 9 listopada 2026: MP3 i kolejka plików. Stan przy nagłówku: W trakcie od 9.11.2026 na polecenie użytkownika. Poprawki #15 i #16 są w kodzie i przeszły testy celowane, ale bez instalacji, odbioru NVDA, odsłuchu i publikacji. Zgłoszenia dotyc.

### I18N

- [I18N-001-wielojezyczny-interfejs-wxpython.md](I18N-001-wielojezyczny-interfejs-wxpython.md) — I18N-001 — wielojęzyczny interfejs wxPython. Stan przy nagłówku: Poprawka po odbiorze wydana w AMC Test 0.1.0-alpha.444 — wspólna.

### INDEKS

- [INDEKS-MATERIALU-INSTRUKCJA-I-ZMIANY-2026-10-19.md](INDEKS-MATERIALU-INSTRUKCJA-I-ZMIANY-2026-10-19.md) — Roboczy indeks materiału do instrukcji i opisu zmian — stan na 25 października 2026.

### KEY

- [KEY-001-konfigurowalne-skroty-klawiaturowe.md](KEY-001-konfigurowalne-skroty-klawiaturowe.md) — KEY-001 — konfigurowalne skróty klawiaturowe. Stan przy nagłówku: Zrobione — kod, pełna regresja, czysta paczka, instalator, wydanie.

### LIC

- [LIC-001-radio-browser-bass-darowizny.md](LIC-001-radio-browser-bass-darowizny.md) — LIC-001 — Radio Browser, BASS i dobrowolne wpłaty. Stan przy nagłówku: Oczekuje.

### LIST

- [LIST-20261105-czas-tidal-i-strzalki.md](LIST-20261105-czas-tidal-i-strzalki.md) — LIST-20261105 — czas TIDAL i koszt strzałek na listach. Stan przy nagłówku: wydane na GitHubie i zainstalowane w AMC Test alpha.472; pomiar z NVDA na wolniejszym łączu i odbiór użytkownika otwarte..
- [LIST-20261105-konfiguracja-odczytu.md](LIST-20261105-konfiguracja-odczytu.md) — LIST-20261105 — konfigurowalny odczyt list. Stan przy nagłówku: W trakcie. Data: 5 listopada 2026. Użytkownik potwierdził, że po alpha.472 listy działają wyraźnie szybciej. Nowe ustawienia nie mogą przywrócić sieci ani bazy do obsługi każdego r.
- [LIST-SEARCH-GLOBAL-20261105-szkic-instrukcji-i-zmian.md](LIST-SEARCH-GLOBAL-20261105-szkic-instrukcji-i-zmian.md) — Szkic instrukcji i zmian — kandydat AMC Test alpha.480. Stan przy nagłówku: poprzednie etapy mają odpowiednio 240 i 231 zaliczonych testów. Pierwsza poprawka bitrate miała 59 testów Pythona, 6 podtestów i dwa celowane przebiegi C#. Rozszerzenie z 6 listopa.
- [LIST-SEARCH-GLOBAL-20261105.md](LIST-SEARCH-GLOBAL-20261105.md) — LIST-SEARCH-GLOBAL-20261105 — krótsze listy i wspólne podglądy. Stan przy nagłówku: kod i 240 testów gotowe 5 listopada 2026; kandydat AMC Test alpha.480 czeka na budowę, instalację i odbiór NVDA po zakończeniu dwóch trwających nagrań..

### MAC

- [MAC-001-port-macos-wxpython-litehost.md](MAC-001-port-macos-wxpython-litehost.md) — MAC-001 — ocena portu AMC Python na macOS. Stan przy nagłówku: Oczekuje.

### MENU

- [MENU-001-menu-kontekstowe-i-skroty.md](MENU-001-menu-kontekstowe-i-skroty.md) — MENU-001 — menu kontekstowe i skróty zależne od sesji. Stan przy nagłówku: W trakcie; korekta natywnej granicy Ctrl+Shift+0 jest przetestowana.

### MIRROR

- [MIRROR-001-dublowanie-dzwieku-cast-airplay.md](MIRROR-001-dublowanie-dzwieku-cast-airplay.md) — MIRROR-001 — dublowanie dźwięku AMC przez Google Cast.

### MODAL

- [MODAL-001-okna-blokujace-glowny-interfejs.md](MODAL-001-okna-blokujace-glowny-interfejs.md) — MODAL-001 — okna blokujące główny interfejs AMC. Stan przy nagłówku: W trakcie. Zgłoszenie użytkownika: 31.10.2026..

### NAME

- [NAME-001-amc-test.md](NAME-001-amc-test.md) — NAME-001 — widoczne nazwy AMC Test i AMC Public.

### NAV

- [NAV-20261106-pageup-pagedown-na-listach.md](NAV-20261106-pageup-pagedown-na-listach.md) — NAV-20261106 — Page Up i Page Down na listach.

### NVDA

- [NVDA-001-opoznienie-ctrl-c.md](NVDA-001-opoznienie-ctrl-c.md) — NVDA-001 — opóźnienie Ctrl+C z NVDAExtensionGlobalPlugin. Stan przy nagłówku: W trakcie — użytkownik potwierdził bardzo szybką reakcję Ctrl+C w AMC; pozostaje kontrola zwykłej aplikacji.
- [NVDA-DUAL-20261110-dwa-profile-jeden-dodatek.md](NVDA-DUAL-20261110-dwa-profile-jeden-dodatek.md) — NVDA-DUAL-20261110 — AMC Test i AMC Public z jednym dodatkiem.
- [NVDA-DUAL-20261110-instrukcja-szkic.md](NVDA-DUAL-20261110-instrukcja-szkic.md) — AMC Test i AMC Public z jednym dodatkiem NVDA — szkic instrukcji.
- [NVDA-DUAL-20261110-zmiany-szkic.md](NVDA-DUAL-20261110-zmiany-szkic.md) — AMC 0.1.0-alpha.492 i dodatek NVDA 0.4.2 — szkic listy zmian.

### PERF

- [PERF-001-duze-listy-hubert.md](PERF-001-duze-listy-hubert.md) — PERF-001 — duże listy i odtwarzanie według zgłoszeń Huberta.
- [PERF-002-duze-biblioteki-plikow.md](PERF-002-duze-biblioteki-plikow.md) — PERF-002 — duże biblioteki Plików lokalnych.

### PLAY

- [PLAY-001-odtwarzanie-losowe-r.md](PLAY-001-odtwarzanie-losowe-r.md) — PLAY-001 — odtwarzanie losowe klawiszem R.
- [PLAY-20261105-kontynuacja-albumu.md](PLAY-20261105-kontynuacja-albumu.md) — PLAY-20261105 — kontynuacja albumu po wybraniu utworu. Stan przy nagłówku: AMC Test alpha.479 zbudowano, zainstalowano i uruchomiono 5 listopada 2026; kontynuacja albumu czeka na ręczny odbiór NVDA..

### PLAYER

- [PLAYER-20261105-skoki-i-zakladki.md](PLAYER-20261105-skoki-i-zakladki.md) — PLAYER-20261105 — skok do czasu, procentu i zakładki. Stan przy nagłówku: wydane na GitHubie i zainstalowane w AMC Test alpha.472; ręczny odbiór NVDA otwarty..
- [PLAYER-20261113-sonos-spotify-apple-link.md](PLAYER-20261113-sonos-spotify-apple-link.md) — PLAYER-20261113 — Sonos, Spotify i łącze Apple Music. Stan przy nagłówku: wydane, zainstalowane i uruchomione w AMC Test alpha.498..

### PLAYLIST

- [PLAYLIST-20261107-delete-i-zaznaczenie.md](PLAYLIST-20261107-delete-i-zaznaczenie.md) — PLAYLIST-20261107 — Delete i zakres zaznaczenia. Stan przy nagłówku: Public alpha.485 zainstalowany i uruchomiony 7.11.2026; użytkownik 8.11.2026 potwierdził, że playlisty poprawnie się dodają i usuwają oraz działają. Pełna aktualizacja obu profili .
- [PLAYLIST-BULK-20261114-potwierdzenie-i-radio.md](PLAYLIST-BULK-20261114-potwierdzenie-i-radio.md) — PLAYLIST-BULK — wielokrotne zaznaczenie i potwierdzenie TIDAL.

### POD

- [POD-001-sortowanie-biblioteki-zrodel.md](POD-001-sortowanie-biblioteki-zrodel.md) — POD-001 — sortowanie Biblioteki podcastów i kanałów.
- [POD-002-import-youtube-ctrl-n.md](POD-002-import-youtube-ctrl-n.md) — POD-002 — jednoetapowy import linków YouTube pod Ctrl+N.
- [POD-003-niezawodnosc-odswiezania-i-duze-kanaly.md](POD-003-niezawodnosc-odswiezania-i-duze-kanaly.md) — POD-003 — niezawodność odświeżania i duże kanały podcastów.
- [POD-004-changelog-draft.md](POD-004-changelog-draft.md) — Changelog POD-004 — projekt.
- [POD-004-instrukcja-uzytkownika-draft.md](POD-004-instrukcja-uzytkownika-draft.md) — SoundCloud w Podcastach — instrukcja użytkownika, projekt.
- [POD-004-soundcloud-rss-wyszukiwanie-i-subskrypcje.md](POD-004-soundcloud-rss-wyszukiwanie-i-subskrypcje.md) — POD-004 — SoundCloud jako podcast RSS: wyszukiwanie i subskrypcje. Stan przy nagłówku: Wydanie alpha.436 istnieje; poprawa Ctrl+F i parytet globalnego Ctrl+Shift+F zapisane w commitach 11ba6271 oraz a8263cc0, bez nowej instalacji i publikacji.
- [POD-20261105-wydajnosc-skrzynki-i-nawigacji.md](POD-20261105-wydajnosc-skrzynki-i-nawigacji.md) — POD-20261105 — lekka skrzynka podcastów i spójny powrót. Stan przy nagłówku: lokalny kandydat alpha.478 zainstalowany, oczekuje na odbiór NVDA użytkownika, 5 listopada 2026. To nowe zgłoszenie użytkownika następuje po lokalnej instalacji alpha.477. Nie zast.
- [POD-20261106-wybor-zrodel-skrzynki.md](POD-20261106-wybor-zrodel-skrzynki.md) — POD-20261106 — wybór źródeł skrzynki podcastów. Stan przy nagłówku: W trakcie — kod, testy bez okien i test hosta gotowe; bez instalacji oraz odbioru NVDA..
- [POD-PERF-20261114-odswiezanie-duzej-biblioteki.md](POD-PERF-20261114-odswiezanie-duzej-biblioteki.md) — POD-PERF-20261114 — obciążenie odświeżania dużej biblioteki podcastów. Stan przy nagłówku: W trakcie odbioru. Wcześniejsze instalatory alpha.505 i źródła alpha.506 opisano poniżej historycznie. Wspólna alpha.507 z wyborem taktu lub trybu tylko na żądanie jest opublikowan.

### PODCAST

- [PODCAST-YOUTUBE-AUTOREFRESH-20261111.md](PODCAST-YOUTUBE-AUTOREFRESH-20261111.md) — Podcasty i YouTube — zbędny komunikat automatycznego odświeżania.

### PODRAD

- [PODRAD-001-osobne-foldery-stacji-i-podcastow.md](PODRAD-001-osobne-foldery-stacji-i-podcastow.md) — PODRAD-001 — osobne foldery stacji i podcastów. Stan przy nagłówku: W trakcie. Kod i testy celowane ukończone 30.10.2026. Instalacja i próba z NVDA pozostają do wykonania..

### PRESET

- [PRESET-20261106-krotszy-odczyt-miejsc.md](PRESET-20261106-krotszy-odczyt-miejsc.md) — PRESET-20261106 — krótszy odczyt miejsc presetów. Stan przy nagłówku: W trakcie — poprawka źródła i test celowany zaliczone; użytkownik odłożył budowę, instalację i odbiór NVDA do zbiorczej aktualizacji z równoległymi zadaniami..

### PUBLIC

- [PUBLIC-20261030-uzupelnienie-poprawek-huberta.md](PUBLIC-20261030-uzupelnienie-poprawek-huberta.md) — PUBLIC-20261030 — uzupełnienie poprawek Huberta. Stan przy nagłówku: W trakcie. Data korekty: 30.10.2026..
- [PUBLIC-20261030-wydanie-alpha448.md](PUBLIC-20261030-wydanie-alpha448.md) — PUBLIC-20261030 — historyczne wydanie AMC Public alpha.448.

### QUEUE

- [QUEUE-002-przeglad-kolejek-sesji-internetowych.md](QUEUE-002-przeglad-kolejek-sesji-internetowych.md) — QUEUE-002 — kolejki AMC Test i Public. Stan przy nagłówku: 14.11.2026. Test i Public alpha.504 wydane oraz zainstalowane;.

### RADIO

- [RADIO-001-import-m3u-icy-i-tytuly.md](RADIO-001-import-m3u-icy-i-tytuly.md) — RADIO-001 — import M3U z nazwami ICY i tytuły bieżącej audycji.
- [RADIO-20261101-parametry-strumienia.md](RADIO-20261101-parametry-strumienia.md) — RADIO-20261101 — parametry bieżącej stacji radiowej. Stan przy nagłówku: źródła, testy, pakiet i wydanie GitHub gotowe; instalacja oczekuje na koniec nagrywania. Wersja: AMC Test 0.1.0-alpha.463..
- [RADIO-FAST-001-pageup-pagedown-w-odtwarzaczu.md](RADIO-FAST-001-pageup-pagedown-w-odtwarzaczu.md) — RADIO-FAST-001 — zmiana stacji w otwartym odtwarzaczu. Stan przy nagłówku: Zrobione — wycofane decyzją użytkownika 10.11.2026. Nie jest planowane dalsze przyspieszanie w ramach tego zadania..
- [RADIO-REC-20261109-status-i-kolizje.md](RADIO-REC-20261109-status-i-kolizje.md) — RADIO-REC-20261109 — status nagrań i kolizje nazw.
- [RADIO-TRACK-001-granice-sygnalu-20261111.md](RADIO-TRACK-001-granice-sygnalu-20261111.md) — RADIO-TRACK-001 — zmiany sygnału wokół metadanych ICY. Stan przy nagłówku: Pomiar wykonany 11.11.2026; słyszalna granica niepotwierdzona odsłuchem..
- [RADIO-TRACK-001-implementacja-20261111.md](RADIO-TRACK-001-implementacja-20261111.md) — RADIO-TRACK-001 — implementacja ręcznego zapisu fragmentu.
- [RADIO-TRACK-001-instrukcja-i-zmiany-SZKIC.md](RADIO-TRACK-001-instrukcja-i-zmiany-SZKIC.md) — Szkic instrukcji i listy zmian: fragment rozpoznanego utworu. Stan przy nagłówku: źródła i testy automatyczne gotowe. Test NVDA, instalacja i wydanie jeszcze nie wykonane. Nie obiecywać automatycznego zapisu całego utworu..
- [RADIO-TRACK-001-pomiar-timeshift-prywatny-20261111.md](RADIO-TRACK-001-pomiar-timeshift-prywatny-20261111.md) — RADIO-TRACK-001 — pomiar Time Shift i prywatnego nagrania.
- [RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md](RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md) — RADIO-TRACK-001 — pomiar ICY i HLS z 11.11.2026.
- [RADIO-TRACK-001-zapis-rozpoznanego-utworu.md](RADIO-TRACK-001-zapis-rozpoznanego-utworu.md) — RADIO-TRACK-001 — zapis rozpoznanego utworu z radia. Stan przy nagłówku: Ręczny eksport fragmentu gotowy w źródłach; zmiany sygnału wokół dwóch przejść zmierzone; test NVDA, instalacja i słyszalne granice utworu nadal otwarte. Decyzja użytkownika: 10.11.
- [RADIO-VIANNEY-20261107-poprawa-ulubionego.md](RADIO-VIANNEY-20261107-poprawa-ulubionego.md) — RADIO-VIANNEY-20261107 — poprawa adresu ulubionego.
- [RADIO-YOUTUBE-NAJBLIZSZA-TRANSMISJA-20261111.md](RADIO-YOUTUBE-NAJBLIZSZA-TRANSMISJA-20261111.md) — Radio YouTube — aktualizacja do najbliższej transmisji.

### RADIOBROWSER

- [RADIOBROWSER-001-przegladanie-katalogu.md](RADIOBROWSER-001-przegladanie-katalogu.md) — RADIOBROWSER-001 — przeglądanie katalogu w sesji Radio.
- [RADIOBROWSER-001-szkic-instrukcji-i-zmian.md](RADIOBROWSER-001-szkic-instrukcji-i-zmian.md) — Radio Browser w AMC Test — szkic do korekty użytkownika.

### RARCH

- [RARCH-001-integracja-radioarchiwista.md](RARCH-001-integracja-radioarchiwista.md) — RARCH-001 — sesja Radio Archiwista. Stan przy nagłówku: Kod przejrzany, pakiet i instalator alpha.449 zbudowane; instalacja wstrzymana do wspólnego wydania z poprawką dużych list, odbiór NVDA oczekuje..
- [RARCH-001-przewodnik-i-lista-zmian-szkic.md](RARCH-001-przewodnik-i-lista-zmian-szkic.md) — Radio Archiwista — szkic przewodnika i listy zmian.
- [RARCH-002-pobieranie-fragmentow.md](RARCH-002-pobieranie-fragmentow.md) — RARCH-002 — pobieranie fragmentu Radio Archiwisty. Stan przy nagłówku: kod, testy, pakiet, wydanie GitHub i lokalna instalacja AMC Test.
- [RARCH-003-presety-katalogow.md](RARCH-003-presety-katalogow.md) — RARCH-003 — presety katalogów Radio Archiwisty. Stan przy nagłówku: źródło i testy zaliczone; budowa, instalacja i publikacja wymagają osobnego potwierdzenia wyników. Wersja docelowa: AMC Test 0.1.0-alpha.462..

### REC

- [REC-001-harmonogram-alt-r-zamykanie.md](REC-001-harmonogram-alt-r-zamykanie.md) — REC-001 — harmonogram, Alt+R i bezpieczne zamykanie. Stan przy nagłówku: Zrobione; korekta zachowania pliku po zerwaniu strumienia jest.
- [REC-002-prawa-strzalka-nagrywane-pliki.md](REC-002-prawa-strzalka-nagrywane-pliki.md) — REC-002 — prawa strzałka w nagrywanych plikach. Stan przy nagłówku: Zrobione; kod, testy, build i instalacja AMC Test są zaliczone..

### RECOG

- [RECOG-001-interakcje-z-rozpoznanymi-utworami.md](RECOG-001-interakcje-z-rozpoznanymi-utworami.md) — RECOG-001 — interakcje z rozpoznanymi utworami.
- [RECOG-002-album-z-historii-do-uslug.md](RECOG-002-album-z-historii-do-uslug.md) — RECOG-002 — album z historii rozpoznawania w Spotify i TIDAL. Stan przy nagłówku: W trakcie; kod, testy celowane i kompilacja hosta gotowe, bez instalacji. Zgłoszenie: 3.11.2026..
- [RECOG-HIST-20261111-tidal-bitrate-kolekcje.md](RECOG-HIST-20261111-tidal-bitrate-kolekcje.md) — RECOG-HIST-20261111 — Historia TIDAL i działania na rozpoznanym utworze. Stan przy nagłówku: W trakcie — kod, testy celowane i kompilacja hosta zaliczone; bez instalacji i odbioru NVDA..
- [RECOG-TIDAL-20261105.md](RECOG-TIDAL-20261105.md) — RECOG-TIDAL-20261105 — kontekst albumu i domyślne Enter TIDAL. Stan przy nagłówku: kod i testy celowane gotowe 5 listopada 2026; budowa, instalacja i ręczny test NVDA oczekują na koniec nagrań..

### REL

- [REL-001-github-aktualizacje-sygnalista.md](REL-001-github-aktualizacje-sygnalista.md) — REL-001 — GitHub Releases, aktualizacje i Sygnalista. Stan przy nagłówku: W trakcie — osobny Worker AMC Test wdrożony bez sekretu; aktywacja czeka na ograniczony token GitHuba i odbiór.
- [REL-001-opis-pierwszego-wydania-publicznego-roboczy.md](REL-001-opis-pierwszego-wydania-publicznego-roboczy.md) — AMC 0.1.0-alpha.425 — roboczy opis pierwszego wydania publicznego.
- [REL-002-instrukcja-aktualizacji-robocza.md](REL-002-instrukcja-aktualizacji-robocza.md) — AMC — aktualizowanie programu i składników.
- [REL-002-kanoniczne-wydania-test-public-github.md](REL-002-kanoniczne-wydania-test-public-github.md) — REL-002 — kanoniczne wydania AMC Test i AMC Public na GitHubie.
- [REL-003-usuniecie-wpf-i-stan-youtube.md](REL-003-usuniecie-wpf-i-stan-youtube.md) — REL-003 — usunięcie starego WPF i stan synchronizacji YouTube. Stan przy nagłówku: stara instalacja i artefakty WPF usunięte; źródła historyczne zachowane do archiwizacji.
- [REL-20261102-alpha467-AirPlay-mirroring.md](REL-20261102-alpha467-AirPlay-mirroring.md) — AMC Test alpha.467 — AirPlay i drugie wyjście lokalne.
- [REL-20261102-alpha467-notes.md](REL-20261102-alpha467-notes.md) — AMC Test 0.1.0-alpha.467.
- [REL-20261102-alpha467-przewodnik-szkic.md](REL-20261102-alpha467-przewodnik-szkic.md) — Szkic instrukcji i listy zmian AMC Test alpha.467.
- [REL-20261102-instrukcja-i-lista-zmian-szkic.md](REL-20261102-instrukcja-i-lista-zmian-szkic.md) — AMC Test — wspólna aktualizacja 02.11.2026.
- [REL-20261102-mirror-wiim-notes.md](REL-20261102-mirror-wiim-notes.md) — AMC Test — dublowanie przez Google Cast i poprawka WiiM.
- [REL-20261102-mirror-wiim-przewodnik-szkic.md](REL-20261102-mirror-wiim-przewodnik-szkic.md) — Szkic instrukcji i listy zmian — AMC Test alpha.466.
- [REL-20261102-nocna-aktualizacja-test.md](REL-20261102-nocna-aktualizacja-test.md) — REL-20261102 — wspólna aktualizacja AMC Test po 02:00. Stan przy nagłówku: Zrobione — wydano, zainstalowano i uruchomiono 02.11.2026; ręczny odbiór nowych zachowań pozostaje osobnym etapem..
- [REL-20261106-alpha481-github-instalacja.md](REL-20261106-alpha481-github-instalacja.md) — AMC Test alpha.481 — publikacja i instalacja.

### RELEASE

- [RELEASE-20261031-alpha461.md](RELEASE-20261031-alpha461.md) — RELEASE-20261031 — AMC Test alpha.461. Stan przy nagłówku: kandydat nocnej aktualizacji. Instalacja i GitHub wymagają osobnej.
- [RELEASE-20261109-alpha488.md](RELEASE-20261109-alpha488.md) — AMC Test i Public — wspólna aktualizacja alpha.488. Stan przy nagłówku: Wydane i Public zainstalowany, 9 listopada 2026. AMC Test opublikowany, lecz nieinstalowany: użytkownik kończy nagrywanie w starej wersji..
- [RELEASE-20261111-instrukcja-lista-zmian-szkic.md](RELEASE-20261111-instrukcja-lista-zmian-szkic.md) — AMC Test i Public alpha.496 — szkic instrukcji i listy zmian.
- [RELEASE-20261111-zbiorcza-aktualizacja.md](RELEASE-20261111-zbiorcza-aktualizacja.md) — RELEASE-20261111 — zbiorcza aktualizacja AMC. Stan przy nagłówku: Wydanie Test/Public alpha.496 opublikowane i zainstalowane 11.11.2026; ręczny odsłuch gestów NVDA pozostaje do odbioru..
- [RELEASE-20261114-public-sonos-playlisty.md](RELEASE-20261114-public-sonos-playlisty.md) — AMC Public z Sonosem — korekta zakresu wydania 14.11.2026.

### SCHED

- [SCHED-001-edytor-i-klawisze-harmonogramow.md](SCHED-001-edytor-i-klawisze-harmonogramow.md) — SCHED-001 — edytor i klawisze harmonogramów.

### SEARCH

- [SEARCH-001-youtube-biblioteka-komunikaty.md](SEARCH-001-youtube-biblioteka-komunikaty.md) — SEARCH-001 — YouTube, Biblioteka i komunikaty wyszukiwania. Stan przy nagłówku: Wdrożone i zainstalowane w AMC Test; czeka na ręczny odbiór NVDA.
- [SEARCH-MEMBERSHIP-20261109-wyniki-ulubione-biblioteka.md](SEARCH-MEMBERSHIP-20261109-wyniki-ulubione-biblioteka.md) — SEARCH-MEMBERSHIP-20261109 — Biblioteka i Ulubione w wynikach wyszukiwania. Stan przy nagłówku: W trakcie — kod i testy celowane gotowe; bez instalacji i odbioru NVDA.

### SESSION

- [SESSION-001-opcje-sesji-i-predkosc.md](SESSION-001-opcje-sesji-i-predkosc.md) — SESSION-001 — pełne opcje sesji i domyślna prędkość.
- [SESSION-002-ustawienia-sesji-i-zachowanie-odtwarzacza.md](SESSION-002-ustawienia-sesji-i-zachowanie-odtwarzacza.md) — SESSION-002 — Ustawienia sesji i zachowanie odtwarzacza.
- [SESSION-003-wiim-enter-i-presety.md](SESSION-003-wiim-enter-i-presety.md) — SESSION-003 — Enter i natywne presety WiiM.

### SET

- [SET-001-audyt-ustawien-dostepnosc-integracja-windows.md](SET-001-audyt-ustawien-dostepnosc-integracja-windows.md) — SET-001 — audyt Ustawień, NVDA i integracja z Windows. Stan przy nagłówku: W trakcie — kod priorytetów 1–3, pełna regresja, czysta paczka,.

### SOCIAL

- [SOCIAL-001-facebook-instagram-wideo.md](SOCIAL-001-facebook-instagram-wideo.md) — SOCIAL-001 — publiczne filmy Facebooka i Instagrama.

### SPOT

- [SPOT-001-predkosc-odtwarzania.md](SPOT-001-predkosc-odtwarzania.md) — SPOT-001 — prędkość odtwarzania Spotify. Stan przy nagłówku: Zrobione w zakresie prędkości — użytkownik potwierdził działanie.

### SPOTIFY

- [SPOTIFY-20261101-album-kolejka-komunikaty.md](SPOTIFY-20261101-album-kolejka-komunikaty.md) — SPOTIFY-20261101 — album, kolejka i komunikaty NVDA. Stan przy nagłówku: W trakcie — zmiany źródłowe i testy bez instalacji oraz ręcznego odbioru NVDA..
- [SPOTIFY-ACCESS-001-zapytanie-o-publiczne-web-api.md](SPOTIFY-ACCESS-001-zapytanie-o-publiczne-web-api.md) — SPOTIFY-ACCESS-001 — zapytanie o publiczne Spotify Web API.
- [SPOTIFY-CHROME-20261031.md](SPOTIFY-CHROME-20261031.md) — Spotify w AMC Test: oficjalny odtwarzacz w ukrytym Chrome.
- [SPOTIFY-CHROME-przewodnik-lista-zmian-szkic.md](SPOTIFY-CHROME-przewodnik-lista-zmian-szkic.md) — Szkic dla użytkownika: Spotify w AMC Test.
- [SPOTIFY-ISSUES-009-011-20261030.md](SPOTIFY-ISSUES-009-011-20261030.md) — SPOTIFY-ISSUES-009-011 — zgłoszenia Huberta dotyczące AMC Test.
- [SPOTIFY-LIBRESPOT-20261108-wycofanie-z-pakietu.md](SPOTIFY-LIBRESPOT-20261108-wycofanie-z-pakietu.md) — Wycofanie LibreSpot z paczek AMC — 8 listopada 2026.
- [SPOTIFY-OUTPUT-001-wyjscie-oficjalnego-odtwarzacza.md](SPOTIFY-OUTPUT-001-wyjscie-oficjalnego-odtwarzacza.md) — SPOTIFY-OUTPUT-001 — fizyczne wyjście oficjalnego Spotify.
- [SPOTIFY-SOLOIST-20261108-proste-logowanie.md](SPOTIFY-SOLOIST-20261108-proste-logowanie.md) — SPOTIFY-SOLOIST-20261108 — prostsza konfiguracja Spotify.

### STAB

- [STAB-001-stabilnosc-okna-po-operacjach-sieciowych.md](STAB-001-stabilnosc-okna-po-operacjach-sieciowych.md) — STAB-001 — stabilność okna po operacjach sieciowych.

### STR

- [STR-001-tidal-apple-music-laboratorium.md](STR-001-tidal-apple-music-laboratorium.md) — STR-001 — laboratorium TIDAL i Apple Music. Stan przy nagłówku: W trakcie — 12.11.2026 użytkownik uruchomił prywatny pilot AMC Test.

### TEST

- [TEST-20261030-amc-test-alpha448.md](TEST-20261030-amc-test-alpha448.md) — TEST-20261030 — historyczne wydanie AMC Test alpha.448.
- [TEST-20261030-instrukcja-i-lista-zmian-szkic.md](TEST-20261030-instrukcja-i-lista-zmian-szkic.md) — TEST-20261030 — szkic instrukcji i listy zmian alpha.448. Stan przy nagłówku: historyczny brakujący załącznik. Rekord odtworzony 16.11.2026.
- [TEST-20261031-spotify-alpha458.md](TEST-20261031-spotify-alpha458.md) — TEST-20261031-SPOTIFY — zbiorcza aktualizacja AMC Test. Stan przy nagłówku: kod i regresja gotowe; pakiet, wydanie GitHub, instalacja i odbiór.

### TID

- [TID-001-pelne-odtwarzanie-w-AMC-Test.md](TID-001-pelne-odtwarzanie-w-AMC-Test.md) — TID-001 — Pełne odtwarzanie TIDAL w odtwarzaczu AMC Test.
- [TID-LIST-PLAYER-20261105-alpha474-szkic-instrukcji-i-zmian.md](TID-LIST-PLAYER-20261105-alpha474-szkic-instrukcji-i-zmian.md) — Szkic instrukcji i zmian AMC Test alpha.474.
- [TID-LIST-PLAYER-20261105-alpha477-szkic-instrukcji-i-zmian.md](TID-LIST-PLAYER-20261105-alpha477-szkic-instrukcji-i-zmian.md) — AMC Test alpha.477 — szkic instrukcji i opisu zmian. Stan przy nagłówku: roboczy tekst do korekty użytkownika. Lokalny kandydat z 5 listopada 2026, godz. 21:20. Nie jest wydaniem GitHub..
- [TID-LIST-PLAYER-20261105-followup.md](TID-LIST-PLAYER-20261105-followup.md) — TIDAL Max, komunikaty list i kontrola odtwarzacza — 5 listopada 2026.
- [TID-LIST-PLAYER-20261105-szkic-instrukcji-i-zmian.md](TID-LIST-PLAYER-20261105-szkic-instrukcji-i-zmian.md) — Szkic instrukcji i listy zmian AMC Test alpha.473.

### TIDAL

- [TIDAL-20261113-odswiezenie-po-zapisie-z-wyszukiwania.md](TIDAL-20261113-odswiezenie-po-zapisie-z-wyszukiwania.md) — TIDAL-20261113 — odświeżenie kolekcji po zapisie z wyszukiwania. Stan przy nagłówku: kod i testy gotowe; jeszcze bez nowej kompilacji, instalacji i.
- [TIDAL-TOGGLE-20261111-przelaczanie-kolekcji.md](TIDAL-TOGGLE-20261111-przelaczanie-kolekcji.md) — TIDAL-TOGGLE — naprzemienne Ulubione i Biblioteka. Stan przy nagłówku: źródła poprawione; budowa, instalacja i odbiór wersji alpha.497 w toku..

### TITLE

- [TITLE-001-data-w-tytule-AMC-Test.md](TITLE-001-data-w-tytule-AMC-Test.md) — TITLE-001 — data i godzina kompilacji w tytule AMC Test. Stan przy nagłówku: W trakcie; zmieniono źródła i wykonano testy, bez kompilacji i instalacji..

### TSH

- [TSH-001-folder-bufora-timeshift.md](TSH-001-folder-bufora-timeshift.md) — TSH-001 — folder bufora Time Shift. Stan przy nagłówku: kod, testy celowane i kompilacja hosta gotowe; wspólna aktualizacja z TIDAL oraz odbiór NVDA w przygotowaniu..

### UNDO

- [UNDO-001-przywracanie-pozycji-kolekcji.md](UNDO-001-przywracanie-pozycji-kolekcji.md) — UNDO-001 — przywracanie pozycji kolekcji. Stan przy nagłówku: Oczekuje na doprecyzowanie i pracę. Rekord odtworzony 16.11.2026.

### UPDATE

- [UPDATE-20261101-ponowna-aktualizacja.md](UPDATE-20261101-ponowna-aktualizacja.md) — UPDATE-20261101 — wynik i ponowne sprawdzenie aktualizacji AMC. Stan przy nagłówku: W trakcie; alpha.464 opublikowana i zainstalowana, potrzebny ręczny odsłuch NVDA. Data: 1.11.2026. Dotyczy bieżącej aplikacji wxPython AMC Test..
- [UPDATE-20261109-hubert-kod5-i-sygnalista.md](UPDATE-20261109-hubert-kod5-i-sygnalista.md) — UPDATE-20261109 — kod 5 instalatora Huberta i Sygnalista.
- [UPDATE-20261113-widoczne-okna.md](UPDATE-20261113-widoczne-okna.md) — UPDATE-20261113 — widoczny wynik aktualizacji AMC.

### YT

- [YT-001-konto-youtube-i-synchronizacja.md](YT-001-konto-youtube-i-synchronizacja.md) — YT-001 — konto YouTube i synchronizacja. Stan przy nagłówku: Wdrożone do ręcznego odbioru — logowanie, kanały i opisy zostały.
- [YT-002-transmisje-na-zywo-i-dynamiczne-radio.md](YT-002-transmisje-na-zywo-i-dynamiczne-radio.md) — YT-002 — transmisje YouTube na żywo i dynamiczne radio. Stan przy nagłówku: Zrobione w AMC Test — import i parsowanie kanałów, Ctrl+I,.
- [YT-003-opisy-youtube-alt-d.md](YT-003-opisy-youtube-alt-d.md) — YT-003 — opisy YouTube pod Alt+D. Stan przy nagłówku: Podstawowy zakres opisów jest zainstalowany i wcześniej odebrany;.
- [YT-004-pobieranie-wideo-youtube.md](YT-004-pobieranie-wideo-youtube.md) — YT-004 — pobieranie wideo z YouTube. Stan przy nagłówku: W trakcie; kod, testy, build i instalacja AMC Test są gotowe,.
- [YT-005-skrzynka-ctrl-i-i-tytuly-youtube.md](YT-005-skrzynka-ctrl-i-i-tytuly-youtube.md) — YT-005 — skrzynka Ctrl+I i polskie tytuły YouTube.
- [YT-006-dokladny-wybor-wariantu-pobrania.md](YT-006-dokladny-wybor-wariantu-pobrania.md) — YT-006 — wybór konkretnego wariantu pobrania. Stan przy nagłówku: Kod i testy celowane gotowe; instalacja i test NVDA odłożone do aktualizacji aplikacji. Decyzja użytkownika: 10.11.2026. Projekt i implementacja: 11.11.2026. Ten wynik zastępuje pr.

## Kontrola kompletności odsyłaczy w rejestrze

- Odsyłacze do brakujących plików: 0.
- Rekordy odtworzone 16.11.2026 z krótkich wpisów rejestru: 6.
  Ich oryginalnych pełnych treści nie odnaleziono; nie są dowodem
  wykonania nieopisanych testów ani dawnych decyzji.
  - [PUBLIC-20261030-wydanie-alpha448.md](PUBLIC-20261030-wydanie-alpha448.md) — Historyczne wydanie AMC Public alpha.448 opublikowano na GitHubie; bez lokalnej instalacji i ręcznego odbioru NVDA według rejestru.
  - [SPOTIFY-ACCESS-001-zapytanie-o-publiczne-web-api.md](SPOTIFY-ACCESS-001-zapytanie-o-publiczne-web-api.md) — Pakiet wstępnego zapytania o publiczny dostęp Spotify Web API był gotowy do recenzji, bez wysyłki. Aktualny kanał kontaktu i kwota Client ID wymagały potwierdzenia.
  - [SPOTIFY-OUTPUT-001-wyjscie-oficjalnego-odtwarzacza.md](SPOTIFY-OUTPUT-001-wyjscie-oficjalnego-odtwarzacza.md) — Historyczne badanie fizycznego wyjścia oficjalnej aplikacji Spotify. Opis Librespot w rejestrze jest starszy niż decyzja o Web Playback SDK w ukrytym Chrome.
  - [TEST-20261030-amc-test-alpha448.md](TEST-20261030-amc-test-alpha448.md) — Historyczne AMC Test alpha.448 było wydane i zainstalowane; ręczny odbiór NVDA oczekiwał według rejestru.
  - [TEST-20261030-instrukcja-i-lista-zmian-szkic.md](TEST-20261030-instrukcja-i-lista-zmian-szkic.md) — Szkic instrukcji i listy zmian do historycznego Test alpha.448; sam plik nie zachował się pod tym odsyłaczem.
  - [UNDO-001-przywracanie-pozycji-kolekcji.md](UNDO-001-przywracanie-pozycji-kolekcji.md) — Plan przeglądu Ctrl+Z, trwałych porządków Ulubionych i kolejek oraz kontraktu cofania; szczegółowy rekord nie zachował się pod tym odsyłaczem.
- Pliki bez odsyłacza w `README.md`: 52. Mogą być materiałem
  pomocniczym lub zadaniem pominiętym w rejestrze; przed pracą sprawdź ich treść.
  - [A11Y-20261110-powiazania-status-spotify.md](A11Y-20261110-powiazania-status-spotify.md)
  - [AM-005-czlonkostwo-biblioteki-apple-music.md](AM-005-czlonkostwo-biblioteki-apple-music.md)
  - [AM-006-instrukcja-i-lista-zmian-szkic.md](AM-006-instrukcja-i-lista-zmian-szkic.md)
  - [AMC-20261108-wspolne-wydanie.md](AMC-20261108-wspolne-wydanie.md)
  - [AMC-INSTALLER-UPDATE-GATE-20261110.md](AMC-INSTALLER-UPDATE-GATE-20261110.md)
  - [AMC-TEST-2026-10-23-1124-instalacja-zbiorcza.md](AMC-TEST-2026-10-23-1124-instalacja-zbiorcza.md)
  - [AMC-TEST-2026-10-23-1256-korekta-spotify-youtube-frontier.md](AMC-TEST-2026-10-23-1256-korekta-spotify-youtube-frontier.md)
  - [AMC-TEST-2026-10-23-1514-youtube-oauth.md](AMC-TEST-2026-10-23-1514-youtube-oauth.md)
  - [AMC-TEST-2026-10-23-1542-youtube-command-wire-fix.md](AMC-TEST-2026-10-23-1542-youtube-command-wire-fix.md)
  - [AMC-TEST-2026-10-23-1630-spotify-output-youtube-oauth.md](AMC-TEST-2026-10-23-1630-spotify-output-youtube-oauth.md)
  - [AMC-TEST-2026-10-23-2220-spotify-tempo-frontier-pin.md](AMC-TEST-2026-10-23-2220-spotify-tempo-frontier-pin.md)
  - [AMC-TEST-2026-10-23-youtube-secret-frontier-choice.md](AMC-TEST-2026-10-23-youtube-secret-frontier-choice.md)
  - [AMC-TEST-2026-10-24-ZAMKNIECIE-SESJI-23-24.md](AMC-TEST-2026-10-24-ZAMKNIECIE-SESJI-23-24.md)
  - [AMC-TEST-2026-10-24-ctrl-shift-i-frontier-stabilnosc.md](AMC-TEST-2026-10-24-ctrl-shift-i-frontier-stabilnosc.md)
  - [AMC-TEST-ZAWIESZENIE-20261110.md](AMC-TEST-ZAWIESZENIE-20261110.md)
  - [AMC-Test-alpha487-szkic-wydania.md](AMC-Test-alpha487-szkic-wydania.md)
  - [AMC08-playlisty-spotify.md](AMC08-playlisty-spotify.md)
  - [AMC10-HERMES-AUDYT-SONOS-2026-10-20.md](AMC10-HERMES-AUDYT-SONOS-2026-10-20.md)
  - [AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md](AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md)
  - [CAST-001-instrukcja-i-lista-zmian-szkic.md](CAST-001-instrukcja-i-lista-zmian-szkic.md)
  - [LIST-SEARCH-GLOBAL-20261105-szkic-instrukcji-i-zmian.md](LIST-SEARCH-GLOBAL-20261105-szkic-instrukcji-i-zmian.md)
  - [NAV-20261106-pageup-pagedown-na-listach.md](NAV-20261106-pageup-pagedown-na-listach.md)
  - [NVDA-DUAL-20261110-instrukcja-szkic.md](NVDA-DUAL-20261110-instrukcja-szkic.md)
  - [NVDA-DUAL-20261110-zmiany-szkic.md](NVDA-DUAL-20261110-zmiany-szkic.md)
  - [POD-004-changelog-draft.md](POD-004-changelog-draft.md)
  - [POD-004-instrukcja-uzytkownika-draft.md](POD-004-instrukcja-uzytkownika-draft.md)
  - [PODCAST-YOUTUBE-AUTOREFRESH-20261111.md](PODCAST-YOUTUBE-AUTOREFRESH-20261111.md)
  - [RADIO-TRACK-001-instrukcja-i-zmiany-SZKIC.md](RADIO-TRACK-001-instrukcja-i-zmiany-SZKIC.md)
  - [RADIO-TRACK-001-pomiar-timeshift-prywatny-20261111.md](RADIO-TRACK-001-pomiar-timeshift-prywatny-20261111.md)
  - [RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md](RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md)
  - [RADIO-YOUTUBE-NAJBLIZSZA-TRANSMISJA-20261111.md](RADIO-YOUTUBE-NAJBLIZSZA-TRANSMISJA-20261111.md)
  - [RADIOBROWSER-001-szkic-instrukcji-i-zmian.md](RADIOBROWSER-001-szkic-instrukcji-i-zmian.md)
  - [RARCH-001-przewodnik-i-lista-zmian-szkic.md](RARCH-001-przewodnik-i-lista-zmian-szkic.md)
  - [REL-001-opis-pierwszego-wydania-publicznego-roboczy.md](REL-001-opis-pierwszego-wydania-publicznego-roboczy.md)
  - [REL-002-instrukcja-aktualizacji-robocza.md](REL-002-instrukcja-aktualizacji-robocza.md)
  - [REL-20261102-alpha467-AirPlay-mirroring.md](REL-20261102-alpha467-AirPlay-mirroring.md)
  - [REL-20261102-alpha467-notes.md](REL-20261102-alpha467-notes.md)
  - [REL-20261102-alpha467-przewodnik-szkic.md](REL-20261102-alpha467-przewodnik-szkic.md)
  - [REL-20261102-instrukcja-i-lista-zmian-szkic.md](REL-20261102-instrukcja-i-lista-zmian-szkic.md)
  - [REL-20261102-mirror-wiim-notes.md](REL-20261102-mirror-wiim-notes.md)
  - [REL-20261102-mirror-wiim-przewodnik-szkic.md](REL-20261102-mirror-wiim-przewodnik-szkic.md)
  - [REL-20261106-alpha481-github-instalacja.md](REL-20261106-alpha481-github-instalacja.md)
  - [RELEASE-20261111-instrukcja-lista-zmian-szkic.md](RELEASE-20261111-instrukcja-lista-zmian-szkic.md)
  - [SPOTIFY-CHROME-20261031.md](SPOTIFY-CHROME-20261031.md)
  - [SPOTIFY-CHROME-przewodnik-lista-zmian-szkic.md](SPOTIFY-CHROME-przewodnik-lista-zmian-szkic.md)
  - [SPOTIFY-SOLOIST-20261108-proste-logowanie.md](SPOTIFY-SOLOIST-20261108-proste-logowanie.md)
  - [TID-LIST-PLAYER-20261105-alpha474-szkic-instrukcji-i-zmian.md](TID-LIST-PLAYER-20261105-alpha474-szkic-instrukcji-i-zmian.md)
  - [TID-LIST-PLAYER-20261105-alpha477-szkic-instrukcji-i-zmian.md](TID-LIST-PLAYER-20261105-alpha477-szkic-instrukcji-i-zmian.md)
  - [TID-LIST-PLAYER-20261105-szkic-instrukcji-i-zmian.md](TID-LIST-PLAYER-20261105-szkic-instrukcji-i-zmian.md)
  - [TIDAL-20261113-odswiezenie-po-zapisie-z-wyszukiwania.md](TIDAL-20261113-odswiezenie-po-zapisie-z-wyszukiwania.md)
  - [TIDAL-TOGGLE-20261111-przelaczanie-kolekcji.md](TIDAL-TOGGLE-20261111-przelaczanie-kolekcji.md)
  - [UPDATE-20261113-widoczne-okna.md](UPDATE-20261113-widoczne-okna.md)
- Pliki bez nagłówka głównego: 0.
- Pliki bez jawnej linii Status/Stan w pierwszych 45 wierszach: 77.
