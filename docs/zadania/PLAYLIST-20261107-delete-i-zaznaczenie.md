# PLAYLIST-20261107 — Delete i zakres zaznaczenia

Status: Public alpha.485 zainstalowany i uruchomiony 7.11.2026; kontrola techniczna przeszła. Odbiór Delete/Ctrl+Shift+P z NVDA i późniejsza pełna aktualizacja Test pozostają do wykonania.

## Zgłoszenie i kryteria akceptacji

1. Delete nie usuwa elementów playlist; możliwe analogiczne braki dla strumieni. Przejrzeć całą bieżącą aplikację wxPython i zapewnić wspólne polecenie we wszystkich kontekstach, w których usuwanie jest obsługiwane. Objąć TIDAL, Spotify, playlisty lokalne i pozostałe odpowiednie listy/sesje, nie tylko aktualny przypadek.
2. W otwartym albumie zaznaczenie pojedynczego utworu i Ctrl+Shift+P dodaje cały album. Przykład użytkownika: Rod Stewart, Every Beat of My Heart, około 10 utworów zamiast jednego. Sprawdzić także zaznaczenie wielu utworów, playlisty i pozostałe sesje.
3. Wybrane utwory mają pierwszeństwo przed otwartym kontenerem. Cały album dodawać po jawnym wyborze albumu lub odpowiedniej jawnej komendzie. Otwarcie albumu nie oznacza wybrania całej zawartości.
4. Użytkownik potwierdza, że samo okno playlist w Public alpha.484 działa bardzo dobrze. Zachować działający interfejs, Enter/Spację, wyszukiwanie literą, pamięć ostatniego celu i powrót fokusu.

## Uzasadnienie i decyzje

Korekta użytkownika z 7.11.2026 zastępuje wcześniejsze założenie, że w otwartym albumie Ctrl+Shift+P ma automatycznie obejmować cały album mimo wskazania utworu.

Potwierdzony odczytem źródła punkt błędu: wxlite/amc_wx_lite/gui.py, LiteFrame._manage_tidal_playlist_membership, około linii 29708–29717 na dd8e6c03. Po _selected_action_rows() kod bezwarunkowo ustawia rows=[album], jeśli _open_service_album_row zwróci album. To zastępuje wybrane utwory. Sprawdzić analogiczne ścieżki Spotify/lokalne, nie zmieniać automatycznie innych działań na albumie.

Test test_tidal_album_playlist_creation_and_cached_reopening w wxlite/tests/test_tidal_playlist_gui.py utrwala wcześniejszą semantykę całego otwartego albumu. Zmienić/splitować go na poprawny kontrakt: pojedynczy utwór, wiele utworów, jawny album i brak zaznaczenia.

Delete ma oznaczać właściwą czynność kontekstową: usunięcie pozycji z playlisty, wpisu z biblioteki albo całej playlisty to różne zakresy. Nie zastępować usunięcia członkostwa skasowaniem pliku. Respektować playlisty cudze/tylko do odczytu i możliwości dostawcy. W polach edycji zachować zwykłe kasowanie tekstu.

Ujednolicić wykonanie, menu, Ctrl+F1, pełną pomoc klawiatury, Ctrl+Shift+K i dokumentację przez wspólny model poleceń/możliwości; testować parytet dostępności i wykonania w Test/Public.

## Stan przejęcia

Repozytorium: D:\Projekty Codex\AMC - integracja wxPython
Gałąź: codex/konfigurowalne-skroty-20261026
HEAD: dd8e6c031b62912664b9813ea9a45e796195f278
Nie przełączać gałęzi, nie tworzyć alternatywnej kopii historycznego WPF.

Zainstalowany Public: 0.1.0-alpha.484, okno AMC Public — 07.11.2026, 19:54. Pełna integralność 3144 plików: kod 0. Zachowane oba połączenia TIDAL i biblioteka. Lokalny kandydat, bez publikacji.
Zainstalowany Test: 0.1.0-alpha.482, okno AMC Test — 07.11.2026, 13:54, źródła ed9d61acff8c71e517b5e25b5c454012b314397a. Pozostał uruchomiony; aktualny stan nagrywania nieznany. Nie zamykać go bez aktualnej kontroli.

Po poprawieniu i odbiorze Public pozostaje wcześniejsze polecenie przygotowania pełnego Test ze wspólnych źródeł: wszystkie poprawki TIDAL oraz pełne możliwości Test (Spotify, Sonos, Radioarchiwista itd.). Nie kopiować ograniczonych binariów Public na Test. Uwzględnić osobno uzgodnione presety, zachować inne zmiany robocze. Szczegóły: AUTH-PUBLIC-20261106-logowanie-tidal-spotify.md.

Niezależne zmiany robocze: profile_presets.py, test_profile_presets.py i zapis PRESET; osobny RADIO-VIANNEY. test_tidal_picker_profile.py ma niezatwierdzone rozszerzenie obu świeżych profili (4/4 zaliczone). Nie resetować, nie nadpisywać, nie obejmować cudzych zmian przypadkowym commitem.

## Materiał do dokumentacji użytkownika — projekt docelowego zachowania

Na liście utworów wybierz jeden albo kilka utworów i naciśnij Ctrl+Shift+P: wybór celów dotyczy wyłącznie tych utworów. Wybranie samego albumu pozwala dodać album. Delete usuwa zaznaczone pozycje zgodnie z rodzajem bieżącej listy i uprawnieniami. W komunikatach i potwierdzeniach trzeba jasno podać zakres. Konkretne nowe komunikaty NVDA i różnice sesji dopisać po wdrożeniu; nie przedstawiać tego szkicu jako działania obecnej instalacji.

## Wykonana zmiana i zakres poleceń

- TIDAL: Ctrl+Shift+P bierze zaznaczony utwór, wideo lub kilka pozycji. Jawnie zaznaczony album nadal oznacza album. W otwartym albumie sam kontekst nie zastępuje wskazanego utworu. Dotychczasowe okno wyboru playlist pozostaje bez zmiany.
- Delete w otwartej playliście TIDAL usuwa wskazane wystąpienia po ponownym potwierdzeniu konta, playlisty i identyfikatorów wpisów przez host. Dialog wymienia playlistę i zakres. Po sukcesie lista jest ponownie pobierana, a fokus wraca do sąsiedniej pozycji.
- Delete w otwartej playliście Spotify usuwa zaznaczone utwory i odcinki przez oficjalne API, tylko z playlist dostępnych do edycji na bieżącym koncie i przy odpowiednim zakresie zgody. Host sprawdza aktualną zawartość przed usunięciem. Nowy i dawny kształt odpowiedzi playlisty (`item` i `track`) są odczytywane.
- Delete w lokalnej playliście plików lub Radia usuwa wyłącznie członkostwo w tej playliście, z kontrolą sesji. Plik ani stacja źródłowa nie są kasowane. Historie usług i zapisane widoki zachowują własny zakres. W Radiu Delete w otwartej playliście kieruje do wspólnego usuwania pozycji, nie do usunięcia stacji z całej biblioteki.
- Wspólny model polecenia Delete opisuje usuwanie z bieżącej listy. Menu ma pozycje kontekstowe dla playlist TIDAL, Radia, Spotify i historii; Ctrl+F1, pełna pomoc oraz paleta używają tego samego opisu i skrótu. W polach edycji Delete pozostaje kasowaniem tekstu.

Komunikaty NVDA: „Wybierz pozycje otwartej playlisty TIDAL do usunięcia”, „Wybierz utwory lub odcinki otwartej playlisty Spotify”, „Usunięto N pozycji z playlisty TIDAL/Spotify”, „Lista pozostała bez zmian” przy zerowym wyniku lokalnym oraz odpowiednie komunikaty odmowy/anulowania. Dokładne brzmienie odsłuchu NVDA trzeba jeszcze sprawdzić na zainstalowanej wersji.

## Testy i wynik

- 83 testy Pythona dotyczące playlist, klienta hosta, menu i dialogów: zaliczone. Obejmują pojedynczy i wielokrotny wybór, jawny album, brak wyboru, Delete, anulowanie, fokus, opis polecenia i kontrakt hosta.
- Izolowany test `test_profile_edits_go_through_the_real_host`: zaliczony (1), sprawdza SQLite lokalnej playlisty, playlisty Radia i odmowę obcego zakresu. Dane testowe są tymczasowe.
- Testy protokołu hosta `--tidal-catalog --spotify-catalog`: 2 zestawy zaliczone. Test klienta Spotify `--spotify-playlists`: zaliczony, w tym ochrona duplikatu i żądanie DELETE tylko dla wskazanych URI.
- Budowa `AccessibleMediaController.LiteHost` Release bez restore: 0 błędów, 0 ostrzeżeń. `git diff --check`: bez błędów formatowania.
- Szeroki przebieg `test_profile_mutations.py` bez podwyższonego dostępu zawiesił się na uruchomieniu hosta; przerwany. Ten sam właściwy test uruchomiony osobno z dostępem do D: przeszedł. Nie jest to wynik negatywny logiki.
- W testach nie wykonano zdalnych operacji na rzeczywistych kontach ani widocznego testu klawiaturą/NVDA. Wynik instalacji Public alpha.485 opisano poniżej. Test alpha.482 nie został zastąpiony.

Interpreter pytest: C:\Users\micha\AppData\Local\Programs\Python\Python314\python.exe. Zależności wx są w runtime AMC Test; atrapy GUI używają test_gui_logic.install_wx_stub przed importem GUI. Dla odfiltrowanych profili świeży proces i profil przed importem. Nie ograniczać weryfikacji Public do zmiany środowiska po imporcie Test.

## Znane ograniczenia

Spotify usuwa przez URI, a nowe API nie wskazuje jednego wystąpienia utworu. Gdy URI występuje w playliście więcej niż raz, host odmawia usunięcia i prosi o odświeżenie, by nie usunąć wszystkich kopii. Jedno żądanie obejmuje najwyżej 100 pozycji. Obce lub niedostępne do edycji playlisty pozostają bez zmian. TIDAL potwierdza własność i aktualne identyfikatory wpisów przed zapisem. Brak testu na żywym koncie i odsłuchu NVDA; źródła, testy, budowa, instalacja, odbiór i publikacja są osobnymi bramkami.

## Wersja i commit

Poprawka źródłowa na bazie dd8e6c03: commit `99f37b2e` na `codex/konfigurowalne-skroty-20261026`; zapis wyników `d2ece565`; numer alpha.485 w `c3007d5f`. Limit po testach: 52% wykorzystane, 48% pozostało; reset 11.11.2026, 07:45 Europe/Warsaw.

## Instalacja Public alpha.485 — 7.11.2026

Użytkownik polecił „To instaluj”. Z czystego zarządzanego worktree commita `c3007d5f` zbudowano pełną paczkę profilu Public. Pierwsza próba z runtime zainstalowanego Public zatrzymała się na ochronie przed podwójnym dołączeniem TIDAL. Utworzono wyłącznie tymczasową kopię runtime w `.test-tmp/public-runtime-base-alpha485`, usunięto z tej kopii 12 przypiętych składników TIDAL i ponowiono build, dołączając je ze źródła w zainstalowanym Public. Zainstalowane biblioteki i profil nie były zmieniane przez tę korektę. Pierwszy kandydat miał jeszcze numer skompilowanego launchera alpha.484; skrypt instalatora go odrzucił. Commit `c3007d5f` podniósł wersję, po czym paczkę zbudowano ponownie.

Paczka `.test-output/amc-public-playlist-alpha485-20261107-v2`: profil Public, `sourceDirty=false`, źródła `c3007d5fc7448adb342cbd72aa7347a5a19ee20e`, launcher `0.1.0-alpha.485`, 3144 plików, bez modułów Spotify. Pełna kontrola integralności oraz kontrolowany start interfejsu i hosta przeszły. Instalator `D:\Projekty Codex\AMC - integracja wxPython\publish\public-playlist-alpha485-20261107\AMC-Public-2026-11-07-2302.exe`: 133863804 bajty, SHA-256 `8d7147a4172a9391a6fd0ebe70588d84c503f4fa502d3ab4ed9328a7935b1b32`; suma zgadza się z `.integrity.json`, profil/wersja/commit są zgodne, `secretScan=passed`. Bez publikacji w kanale aktualizacji ani w GitHub Releases.

Przed instalacją Public był zamknięty. Zaszyfrowana kopia `D:\Projekty Codex\AMC - kopie prywatne\TIDAL-20261107\profil-przed-alpha485.dpapi` przeszła odszyfrowanie i weryfikację sum: 46581846 bajtów, 9 pozycji. AMC Test pozostał uruchomiony; jego plików nie podmieniano. Cicha instalacja do `C:\Users\micha\AppData\Local\Programs\AMC Public` zakończyła się kodem 0 i logiem sukcesu o 23:11, bez restartu Windows. Log: `C:\Users\micha\Documents\Codex\2026-11-07\amc-playlisty-delete-20261107\work\install-alpha485.log`.

Pełna kontrola 3144 plików zainstalowanej kopii: kod 0. Manifest: profil Public, commit `c3007d5f`, `sourceDirty=false`; ProductVersion alpha.485. Porównanie z zaszyfrowaną kopią wykazało `publicTidalCredentialsUnchanged=true`, `publicTidalConnectionsPresent=2`, `publicStateUnchanged=true`. Uruchomiono dokładne `C:\Users\micha\AppData\Local\Programs\AMC Public\AMC.exe`. Jedna instancja Public (launcher PID 24780, Python 9860, host 22712) pokazuje okno „AMC Public — 07.11.2026, 23:02”. Osobna instancja Test nadal działa w alpha.482, z launcherem PID 39680. Odbiór klawiaturą i NVDA nowego Delete oraz pojedynczego utworu Ctrl+Shift+P pozostaje do wykonania przez użytkownika; nie wykonywano zapisu na żywych playlistach w jego imieniu.

## Projekt instrukcji użytkownika i dziennika zmian

Wybierz utwór albo kilka utworów i naciśnij Ctrl+Shift+P. W oknie playlist wybierz cele i zatwierdź. AMC doda wybrane pozycje. Aby dodać cały album TIDAL, zaznacz sam album na liście albumów. W otwartej playliście Delete usuwa wskazane pozycje z tej playlisty po potwierdzeniu; nie usuwa nagrań z katalogu ani plików z dysku. W Spotify pojedyncze wystąpienie utworu powtórzonego na playliście wymaga osobnej obsługi w przyszłości.

Dziennik zmian, szkic: poprawiono zakres Ctrl+Shift+P w otwartym albumie TIDAL; dodano Delete do playlist TIDAL, Spotify, lokalnych plików i Radia; skorygowano dostępność polecenia w menu i pomocy; zabezpieczono Spotify przed przypadkowym usunięciem duplikatów. Projekt wymaga korekty i akceptacji użytkownika przed publikacją.

## Osobna rozmowa

Utworzona 7.11.2026: AMC: Delete na listach i dodawanie wybranych utworów.
Id rozmowy: 01a1b7b6-19ab-7243-8745-ce0884230f3c, host local. Przekazano polecenie rozpoczęcia diagnozy i naprawy; sam fakt utworzenia rozmowy nie dowodzi wykonania poprawek.
