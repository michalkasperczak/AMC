# AMC-20261108 — wspólne wydanie Test i Public

Stan 8.11.2026: oba wydania 0.1.0-alpha.486 opublikowane na GitHubie; Public zainstalowany, sprawdzony i uruchomiony lokalnie. Test lokalny nadal w alpha.482, bez podmiany podczas pracy i nagrań.

## Decyzje i zachowanie

8.11.2026 użytkownik potwierdził odbiór playlist w Public alpha.485: dodawanie i usuwanie działają. Zlecił wydanie na GitHubie obu profili po sprawdzeniu oraz następnie zwykłą aktualizację własnych instalacji. W nowym Public jedyną różnicą funkcjonalną względem Test jest brak sesji Sonos. Poprzednie wykluczenie Spotify, Radia Archiwisty i odczytu konta YouTube z Public (opisane 6–7.11.2026 w `AUTH-PUBLIC-20261106-logowanie-tidal-spotify.md`) zostaje zastąpione tą decyzją. To nie scala profili kont, poświadczeń ani katalogów danych: izolacja instalacji i logowania pozostaje.

Spotify: profil Public udostępnia sesję, playlisty oraz przypiętego hosta Librespot z kompletem licencji. Zachowanie usuwania powtórzonych URI jest takie samo jak w Test: host odmawia niejednoznacznego usunięcia. Zewnętrzny dostęp do Spotify zależy również od warunków aplikacji deweloperskiej i konta; test lokalny nie potwierdza dostępu każdego odbiorcy. Użytkownik zgłosił działanie Spotify na innej instalacji Test bez własnoręcznego dodania odbiorcy do listy, co stanowi obserwację, a nie zweryfikowaną gwarancję dla Public.

TIDAL Test: użytkownik zgłosił komunikat „Ten element nie ma dostępnego albumu ani wykonawcy nadrzędnego” po prawej strzałce na albumie Roda Stewarta, mimo że w Public wcześniej przejście do wykonawcy działało. Gdy bezpośrednia lista albumów wykonawcy zwraca album bez jawnego pola `artists`, rdzeń przypisuje jako relację otwartego wykonawcę; nie nadpisuje istniejącej relacji. Dotyczy albumów w sekcji wykonawcy, bez zmiany innych list ani nazwy mówionej przez NVDA. Nie stwierdzono jeszcze, czy odpowiedź TIDAL z rzeczywistego konta była przyczyną tego konkretnego zdarzenia.

Krótsze etykiety miejsc presetów z zadania `PRESET-20261106-krotszy-odczyt-miejsc.md` wchodzą do zbiorczej aktualizacji. `Ctrl+Alt+P` i `Ctrl+Alt+Shift+P` czytają numer miejsca, nazwę albo „pusty” i skrót; model przypisania pozostaje oddzielny. Odbiór NVDA po instalacji nadal wymagany.

## Komendy, pomoc i tryby

Lista sesji, menu Widok, Ctrl+F1, pełna pomoc i paleta Ctrl+Shift+K korzystają z możliwości wybranego profilu. Public wyświetla Spotify i Radio Archiwistę, ale nie Sonos. Test zachowuje wszystkie trzy. Playlisty: Ctrl+Shift+P wybiera wskazane utwory; Delete usuwa członkostwo zgodnie z kontekstem, po potwierdzeniu dla usług zdalnych. Opisy nowych zachowań są w `PLAYLIST-20261107-delete-i-zaznaczenie.md`.

## Weryfikacja dotychczasowa

- Testy Python profilu, fizycznego pakowania, importu GUI i okna playlist TIDAL w świeżych procesach: 51/51 zaliczonych. Test pełnej paczki Public sprawdził obecność Librespot i brak Sonos.
- Celowane testy playlist, presetów, nawigacji TIDAL/Spotify oraz menu: 170/172, dwa niepowodzenia wynikały ze starych oczekiwanych opisów pomocy. Po ich uaktualnieniu testy palety: 19/19 zaliczonych. Nie zmieniano działania poleceń dla tych dwóch przypadków.
- Testy C# Core Smoke: zaliczone, w tym nowy przypadek albumów artysty bez pola `artists` w odpowiedzi. Testy launchera/aktualizatora: 39/39 zaliczonych, w tym przejście ze starszego zakresu Public.
- Test na rzeczywistym koncie TIDAL po nowej poprawce, logowanie Spotify Public i odsłuch NVDA nowego wydania: oczekują na użytkownika. Paczki, instalatory i publikacja są opisane poniżej jako wykonane.

## Materiał do przewodnika i dziennika zmian — szkic

AMC Public otrzymuje Spotify, Radio Archiwistę oraz funkcje konta YouTube obecne w Test. Sonos pozostaje tylko w Test. W albumach wykonawcy TIDAL prawa strzałka może ponownie prowadzić do otwartego wykonawcy również wtedy, gdy odpowiedź katalogu pomija relację przy albumie. Miejsca presetów mają krótsze etykiety. Poprawki playlist przyjęte przez użytkownika w Public alpha.485 są częścią obu aktualizacji. Tekst wymaga korekty użytkownika przed publikacją jako przewodnik; techniczne informacje wydania mogą być użyte w notatkach GitHub po weryfikacji pakietów.

## Ograniczenia i bramki

Źródło, test, zbudowany pakiet, instalacja i opublikowane wydanie to osobne stany. Przed podmianą Test sprawdzić nagrywanie i harmonogram; nie zamykać procesu podczas aktywnego nagrania. Przed publikacją sprawdzić commit, integralność, profil/wersję instalatorów oraz pozycje kanału aktualizacji. Nie kopiować kont ani identyfikatorów aplikacji z profilu użytkownika do instalatora. Po publikacji zweryfikować zdalny commit, nazwy, rozmiary i sumy plików.

## Wersja, commit i wynik końcowy

Wersja obu profili: `0.1.0-alpha.486`. Commit źródła `ecd2f4439ff19561dc1351baaf1a4735ac099b0c` na `codex/konfigurowalne-skroty-20261026`, potwierdzony przez zdalny odczyt GitHub. Obie paczki zbudowano z tego samego czystego commitu w istniejącym zarządzanym worktree. Test ma 4197 plików i Sonos; Public ma 4196 plików bez `sonos_source.py`. Oba mają `spotify_source.py` i przypiętego hosta Librespot. Pełna integralność, kompletność manifestu i kontrolowany start GUI/hosta przeszły osobno dla obu profili.

Instalator Test: `D:\Projekty Codex\AMC - integracja wxPython\publish\test-alpha486-20261108\AMC-Test-2026-11-08-0823.exe`, 143847345 bajtów, SHA-256 `0411aa66fc0ac45a60ba958c7240c34e33ad1ebf389913c9254cfa833c4d2c65`, tag `test-2026.11.08.0823`. Instalator Public: `D:\Projekty Codex\AMC - integracja wxPython\publish\public-alpha486-20261108\AMC-Public-2026-11-08-0826.exe`, 143831082 bajty, SHA-256 `763f11052dc489bb2a494fae30b20b70f651a729d303985726ef26a915c1b9c7`, tag `public-2026.11.08.0826`. Obie sumy są zgodne z plikami `.integrity.json` i manifestami aktualizacji, oba skany sekretów `passed`. Użytkownik zatwierdził krótkie opisy i publikację 8.11.2026. Opisy wydania są opublikowane; osobne szkice przewodników pozostają lokalne do korekty użytkownika.

Diagnoza spowolnienia Test po restarcie 8.11: przy działającym Test alpha.482 log potwierdził równoległe nagrania rozpoczęte o 08:12 i 08:19 oraz cykliczne przygotowanie podpisów rozpoznawania dźwięku. Około 08:27 pierwsze nagranie się zakończyło; ręczne nagranie z 08:19 pozostawało aktywne przy ostatnim odczycie. Wolne RAM około 12 GB z 31 GB; chwilowe obciążenie hosta 5–9% jednego rdzenia i Python 3% jednego rdzenia, ogólne obciążenie CPU około 27%. Wartość pamięci roboczej hosta rosła, prywatna około 430 MB; nie przesądza to o trwałym wycieku. Test nie został zamknięty ani zaktualizowany.

W tym samym logu raz wystąpił konflikt nazwy przy zakończeniu nagrania. Istniejący plik miał 56017199 bajtów (zapis 00:11), a bezpieczny plik roboczy drugiego nagrania 87390767 bajtów (08:22). Utworzono tylko dodatkową kopię tego drugiego pliku w `D:\Projekty Codex\AMC - kopie prywatne\Nagranie odzyskane 2026-11-08\Uspokojenie wieczorne Poznań - druga kopia 2026-11-08 08-22.mp3`; SHA-256 kopii i źródła są zgodne, `ffprobe` rozpoznaje 5461,896 s MP3. Oryginalny plik roboczy na C: i wcześniejszy gotowy plik pozostawiono. Nie przypisywano tego pojedynczego konfliktu do zgłoszonego spowolnienia jako dowiedzionej przyczyny.

## Publikacja i lokalna instalacja — 8.11.2026

`Publish-AMCRelease.ps1 -Publish` opublikował oba wydania. Niezależny odczyt GitHub potwierdził dla `test-2026.11.08.0823` wydanie wstępne i dla `public-2026.11.08.0826` zwykłe wydanie oznaczone jako Latest. W każdym z nich są trzy zasoby: instalator, `.exe.integrity.json` i `amc-update-<profil>.json`, wszystkie w stanie `uploaded`; zdalne rozmiary i SHA-256 instalatorów są identyczne z lokalnymi. Oba wydania wskazują commit `ecd2f4439ff19561dc1351baaf1a4735ac099b0c`. `release-ready-*.json` mają `published=true`.

Przed aktualizacją Public był zamknięty. Kopia ośmiu plików profilu i UI została zaszyfrowana DPAPI w `D:\Projekty Codex\AMC - kopie prywatne\PUBLIC-20261108-przed-alpha486\profil-przed-alpha486.dpapi`; odszyfrowanie, liczba pozycji i suma kontrolna przeszły. Instalator Public uruchomiony cicho z dokładnego lokalnego pliku opublikowanego na GitHubie zakończył się kodem 0. Log: `C:\Users\micha\Documents\Codex\2026-11-07\amc-playlisty-delete-20261107\work\install-public-alpha486.log`. Pełna kontrola integralności zainstalowanych 4196 plików zwróciła kod 0. Manifest: `public`, źródło `ecd2f443`, `sourceDirty=false`, Spotify i Librespot obecne, Sonos nieobecny; ProductVersion `0.1.0-alpha.486`. Wszystkie osiem plików profilu po instalacji jest bajtowo zgodnych z zaszyfrowaną kopią. Uruchomiono dokładne `C:\Users\micha\AppData\Local\Programs\AMC Public\AMC.exe`; launcher, GUI i host działają, okno GUI ma tytuł „AMC Public — 08.11.2026, 08:26”.

W Public sekcja Spotify istnieje, lecz Client ID nie jest skonfigurowany. W Test Client ID jest skonfigurowany; jego wartości ani poświadczeń nie przenoszono. Użytkownik ma wykonać własne logowanie w Public przez okno konta Spotify `Ctrl+F5`, zapisując tylko Client ID i wpisując hasło wyłącznie na stronie Spotify. Odbiór tego przepływu, playlist i odtwarzania na żywym koncie pozostaje otwarty. Lokalny Test nadal pracuje w alpha.482; aktualizacja Test nie była częścią tej instalacji, żeby nie przerywać jego pracy i nagrań.

Osobny commit dokumentacyjny po zbudowaniu pakietów zawierał prywatne szczegóły nagrania i lokalne ścieżki. Automatyczna kontrola odrzuciła jego push na GitHub z powodu ryzyka ujawnienia tych danych. Pozostaje lokalny; opublikowany commit źródła i oba wydania nie zawierają tego commitu.
## Korekta 8 listopada wieczorem — następne Test

Użytkownik zlecił następną aktualizację AMC Test z internetowym Sygnalistą, lokalnym Sonosem i bez LibreSpot, przyjmując próbę Sonosa na rzeczywistym głośniku dopiero za kilka dni. Zastępuje to wcześniejsze planowanie kolejnego Test z hostem LibreSpot. Opublikowane rano Test i Public `alpha.486` pozostają historycznymi, poprawnie zweryfikowanymi wydaniami; Public nie jest teraz podmieniany. Następne Test musi mieć wyższy numer techniczny, a ograniczenia sprzętowego odbioru Sonosa należy podać w opisie wydania. Czysty kod jest na osobnej gałęzi GitHub `codex/amc-test-sygnalista-sonos-20261108` przy commicie `066a61b3`. Nowa kompletna paczka, instalator, GitHub Release i instalacja nie są jeszcze wykonane. Użytkownik odłożył instalację na jutro z powodu nagrywania dziś wieczorem; nie zamykać działającego Test podczas nagrania.

## Kolejna korekta 9 listopada 2026 — zbiorczy kandydat

Użytkownik potwierdził słyszalne odtwarzanie Spotify w Public po własnym logowaniu. To zastępuje wcześniejsze w tym rekordzie oczekiwanie na próbę odsłuchową; nie stanowi odbioru każdej funkcji Spotify ani Sonosa, którego próbę na głośniku użytkownik planuje na 12.11. Nowe źródła obejmują stany kont Spotify/TIDAL, opcjonalny tytuł odtwarzanego materiału w tytule okna, status nagrań i wznowienie TIDAL. Potwierdzone testy celowane tej części: 169 zaliczonych, 1 pominięty, 3 wyłączone; brak instalacji i ręcznej próby NVDA. Dodatkowo zgłoszenie Huberta #14 o kodzie 5 i dostępność Sygnalisty mają osobny rekord `UPDATE-20261109-hubert-kod5-i-sygnalista.md`. Następna wspólna aktualizacja Test/Public musi zebrać i zweryfikować te zmiany przed publikacją; wcześniejsze alpha.486 nie zawierało nowych źródeł.
