# AMC Public z Sonosem — korekta zakresu wydania 14.11.2026

Użytkownik 14.11.2026 polecił przygotować Public z działającym lokalnym Sonosem oraz uwzględnić zapisane zmiany YouTube i podcastów w aktualizacji AMC. Decyzja zastępuje wcześniejsze założenie w `AMC10-lokalny-Sonos-instrukcja-i-zmiany-szkic.md`, że Sonos jest wyłącznie w Test. Nie oznacza włączenia niezakończonej usługi logowania Sonos przez broker; chodzi o dostępne lokalne wyszukiwanie i sterowanie.

## Zachowanie i kontrakt

- Sesja Sonos pojawia się także w AMC Public jako istniejąca pozycja Menedżera sesji, menu Widok, pełnej pomocy, pomocy kontekstowej i palety. Dostępność wynika ze wspólnego modelu możliwości.
- Public zawiera `sonos_source.py` i przypięte zależności SoCo. Instalator Public nie usuwa już tego modułu przy aktualizacji. Apple Music pozostaje poza Public.
- Stan Test i Public, katalogi ustawień, przestrzenie poświadczeń, identyfikatory instalacji oraz kanały aktualizacji nadal są odrębne. Migracja starszego Public bez Sonosa jest akceptowana przez aktualizator.
- Wydanie `0.1.0-alpha.507` ma zawierać poprawkę dodawania wielu pozycji do playlist, potwierdzenie odpowiedzi TIDAL, zmiany kolejek i wcześniej zapisane prace nad YouTube oraz podcastami. Dokładny commit ustalić po zakończeniu równoległych zmian źródeł. Numer 507 jest wyższy od zainstalowanego w projektowym Test alpha.506.

## Weryfikacja bieżąca

- Python: `test_release_profile.py`, `test_bundle_layout.py`, `test_installer_profiles.py`: **82 zaliczone**.
- .NET: `AccessibleMediaController.PythonLauncher.Tests`: **46 zaliczonych**, w tym migracja Public bez Sonosa.
- Po zapisie równoległej zmiany Apple Music: menu i klawisze dostępu **44 zaliczone** po poprawieniu dwóch pozycji usuwania z kolejki; playlisty na atrapach wx **34 zaliczone**; Sonos i Frontier Smart w rzeczywistym wx **61 zaliczonych**. Pierwsza próba połączenia testów prawdziwego wx i dialogów na atrapach w jednym procesie dała błąd konstrukcji atrapy; osobny proces przeszedł. Wspólne menu wykryło realny brak `&` w dwóch pozycjach kolejki, już poprawiony.
- Szkic instalatora Test z wcześniejszego czystego commitu przeszedł kontrolę paczki; nowy wspólny zestaw źródeł będzie wymagał osobnych paczek i instalatorów. Nie przypisywać starego wyniku do nowej wersji.

## Ryzyko instalacji

Starsze AMC Test nadal działa z `C:\Users\micha\AppData\Local\Programs\AMC Test`, podczas gdy bieżąca instalacja projektowa Test leży w `D:\Projekty Codex\AMC Python`. W starszej instancji jest niedokończony formularz dodawania stacji YouTube. Nie zamykać jej ani nie nadpisywać katalogu C przed zabezpieczeniem wpisu. Poprzednie nieudane aktualizacje zgłoszone przez użytkownika nie mają jeszcze ustalonej przyczyny; zaliczona kompilacja instalatora nie zastępuje sprawdzenia rzeczywistej instalacji i uruchomienia.

## Szkic instrukcji i listy zmian

**Instrukcja:** W Menedżerze sesji AMC Public włącz Sonos. Wybierz lokalny głośnik, a następnie steruj odtwarzaniem i przeglądaj dostępne źródła. `Ctrl+Shift+P` dodaje kilka zaznaczonych pozycji do playlist zgodnie z bieżącą sesją; w Radiu playlista stacji nie podaje łącznego czasu.

**Lista zmian:** Dodano lokalnego Sonosa do Public. Zachowano osobne profile Test/Public. Sprawdzono migrację starszego Public. Do wspólnego wydania wchodzą potwierdzone poprawki playlist i zapisane zmiany YouTube oraz podcastów. Ten tekst jest szkicem do korekty użytkownika, bez automatycznej publikacji.

## Korekta 14.11.2026 — pełne wyłączenie Apple Music w Public

Użytkownik polecił aktualizację obu profili: Test ze wszystkimi ostatnimi
zmianami, Public z Sonosem, YouTube, kolejkami i podcastami, bez Apple Music.
Podczas kontroli pierwszej paczki Public wykryto, że brak sesji nie usuwał
publicznego wyszukiwania linku Apple Music z historii rozpoznawania utworów.
Ta korekta doprecyzowuje wcześniejsze zdanie „Apple Music pozostaje poza
Public”: Public nie pokazuje także działań Apple Music w szybkiej liście ani
menu kontekstowym rozpoznanego utworu, a moduł wyszukiwania linku nie trafia
do paczki. Test zachowuje te działania. Spotify, TIDAL i pozostałe linki
historii rozpoznawania pozostają dostępne w obu profilach.

Wspólny model działań historii rozpoznawania przyjmuje możliwość z aktywnego
profilu. Import modułu linków Apple Music następuje tylko w Test; próba
rozwiązania takiego linku w Public kończy się bez uruchomienia usługi.
Zmieniono `recognition_actions.py`, `gui.py` i `build_bundle.py` oraz testy
`test_recognition_actions.py` i `test_bundle_layout.py`.

Testy przed tą korektą: **208 zaliczonych i 3 podtesty** dla profili, Apple
Music, podcastów, kolejek i menu; **123 zaliczone i 2 podtesty** dla Sonosa,
YouTube, podcastów i kolejek; **46 zaliczonych** samodzielnych testów .NET
launchera i aktualizatora. Po korekcie: testy rozpoznawania, profilu, pakietu
i menu **125 zaliczonych**. Pierwsze paczki z commitu `6e708c11` przeszły
kontrolę integralności i cichy start, ale są nieaktualne wobec tej poprawki
i nie mogą być użyte do instalacji ani publikacji. Nowe buildy, instalatory,
instalacja i NVDA wymagają osobnego potwierdzenia.

Materiał do dokumentacji użytkownika — projekt: AMC Public pokazuje Sonosa,
zmiany YouTube, kolejek i podcastów. W historii rozpoznawania utworów można
korzystać z działań Spotify i TIDAL; działania Apple Music są dostępne tylko
w AMC Test. Opis jest projektem do korekty po odbiorze, nie opublikowaną
instrukcją.

## Wynik wydania 14.11.2026 — commit `aafa2bc3`

Po powyższej korekcie obie paczki zbudowano ponownie z jednego czystego commitu
`aafa2bc3a6cbacaf892df96b1414f7ef3d50b589`, wersja techniczna
`0.1.0-alpha.507`. Test: etykieta `14.11.2026, 16:42`, identyfikator paczki
`b05f3f66`, 4730 plików. Public: `14.11.2026, 16:45`, identyfikator `17e5c5c5`,
3459 plików. Obie paczki przeszły pełną kontrolę i cichy start interfejsu oraz
hosta. Manifest Public nie zawiera żadnego pliku Apple Music; zawiera Sonosa.

Instalator Test: `AMC-Test-2026-11-14-1642.exe`, 218 473 592 bajty,
SHA-256 `61e17c890631055fea84840eceb6c68f9019d8d48c499e472fd48b1b9e63642c`.
Instalator Public: `AMC-Public-2026-11-14-1645.exe`, 136 533 154 bajty,
SHA-256 `d262c0073dfc060a58e272c96ba2a277783851b4b79200c6a811df618c7cd8c0`.
Oba pliki integralności potwierdzają brak sekretów. Wydania GitHub:
`test-2026.11.14.1642` jako prerelease i `public-2026.11.14.1645` jako
stable. Załączniki instalatorów, manifesty aktualizacji i pliki integralności
zostały odczytowo potwierdzone na GitHubie z właściwymi nazwami i rozmiarami.

Public zainstalowano w `D:\Projekty Codex\AMC Public`: kod instalatora 0,
manifest `aafa2bc3`, pełna kontrola integralności 0 i cichy start zaliczony.
W katalogu instalacji nie pozostały moduł linków Apple Music, klient biblioteki,
zależności `applemusic` ani `playwright`, ani host WebView. Test opublikowano,
lecz lokalna instalacja w `D:\Projekty Codex\AMC Python` jest jeszcze
odłożona: starsze AMC Test z C: działało, a narzędzie wykryło aktywność
użytkownika w jego oknie podczas sprawdzania. Widok nagrań pokazał zero
pozycji; nie zamknięto procesu bez pewności, że użytkownik skończył pracę.
Nie wykonano jeszcze ręcznego odbioru NVDA ani prób na żywych kontach.

### Korekta stanu instalacji 14.11.2026, po 17:55

Użytkownik potwierdził, że nagrywanie się zakończyło, niedokończony formularz
można zamknąć i AMC jest wolne. W starszej instancji z C: widok nagrań pokazał
„zero elementów”; wcześniejszy pasek stanu podał zakończenie nagrywania
„Dominikanie Poznań — YouTube na żywo”. Starsze okno zamknięto przez Alt+F4;
launcher, Python i host z tej instalacji zniknęły przed podmianą. To zastępuje
powyższy stan „lokalna instalacja Test odłożona”.

Instalator Test zakończył się kodem 0 w `D:\Projekty Codex\AMC Python`.
Zainstalowany manifest ma profil `test`, etykietę `14.11.2026, 16:42`, commit
`aafa2bc3a6cbacaf892df96b1414f7ef3d50b589`, identyfikator `b05f3f66`
i `sourceDirty=false`. Pełna kontrola integralności zwróciła 0, cichy start
interfejsu i hosta przeszedł. Uruchomiono dokładnie
`D:\Projekty Codex\AMC Python\AMC.exe`; procesy launchera, Pythona i hosta
pochodzą z tego katalogu, bez starej instancji C. Start Menu i skrót pulpitu
`AMC Test` wskazują ten launcher. Okno ma tytuł
„AMC Test — 14.11.2026, 16:42”, otworzyło sesję Radio i podało
„Nic nie jest odtwarzane”. Przed zamknięciem starsza aplikacja pokazywała
inną nazwę stacji niż wcześniej wskazany przez użytkownika Poznań; nie
uruchomiono automatycznie niepewnego źródła. Wznowienie dźwięku i wybranego
źródła po aktualizacji pozostaje osobnym zadaniem. Odbiór NVDA, Apple Music,
Sonosa oraz nowych ustawień podcastów na żywych danych nadal jest otwarty.
