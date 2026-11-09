# Spotify Public — izolacja odtwarzacza i logowania

Data: 9.11.2026. Stan: Public alpha.487 zainstalowany; odsłuch Spotify i odbiór NVDA wymagają próby użytkownika.

## Uzasadnienie i decyzje

Użytkownik zalogował Spotify Public i odświeżył katalog, lecz utwór był niemy, mimo pozornego postępu. W logu Public dwukrotnie wystąpiło przekroczenie 45 sekund przy uruchamianiu ukrytego Chrome. Audyt wykazał, że Public i Test wskazywały ten sam profil Chrome `AMC Test` oraz tę samą nazwę tokenu Spotify w Menedżerze poświadczeń. Public mógł zderzyć się z działającym Test, a logowanie jednej wersji mogło nadpisać token drugiej.

Od tego kandydata Public ma własny katalog Chrome i własną nazwę poświadczenia; Test zachowuje dotychczasowe nazwy. Nie kopiujemy tokenu między profilami, ponieważ odświeżenie jednego egzemplarza mogłoby unieważnić drugi. Po instalacji kandydat Public wymaga ponownego jawnego logowania przez `Ctrl+F5`; Client ID zapisany w profilu Public pozostaje.

## Materiał do dokumentacji użytkownika

W AMC Public otwórz Spotify i naciśnij `Ctrl+F5`, aby po aktualizacji ponownie wybrać „Zaloguj w przeglądarce”. Własny Client ID pozostaje w ustawieniach; logowanie jest oddzielne od AMC Test. Utwór uruchom Enterem z listy. Gdy Spotify jeszcze uruchamia odtwarzacz, przycisk transportu pokazuje „Anuluj”, a próba skoku procentowego wyjaśnia, że trzeba zaczekać na rozpoczęcie. Po potwierdzonym starcie dostępne są czas, skoki procentowe cyframi `0–9` i `Shift+J`, przewijanie, pauza, głośność oraz `Shift+A` do wyboru wyjścia. Gdy Chrome przekroczy czas startu, NVDA poda tę przyczynę zamiast samej ciszy. Po rozpoczęciu dźwięku trzeba sprawdzić te czynności na rzeczywistym koncie.

## Testy i wynik

- Testy kontraktu profilów i katalogu Spotify w czystym checkoutcie: 2/2 zestawy zaliczone.
- Testy Python etykiet odtwarzacza, klienta i transportu: 16/16 zaliczone.
- Kompilacja hosta w głównym checkoutcie: 0 błędów, 0 ostrzeżeń; nie jest jeszcze kompilacją kandydata wydania.
- Odsłuch i nawigacja NVDA: oczekują na użytkownika; budowę i instalację potwierdza osobna sekcja poniżej.

## Znane ograniczenia

Sama izolacja usuwa wykrytą kolizję, ale nie potwierdza jeszcze dźwięku na koncie użytkownika. Skok procentowy potrzebuje czasu trwania zgłoszonego przez Spotify po rzeczywistym starcie. Prędkość utworów Spotify nie jest trwała; osobne rozwiązanie dla podcastów nie wchodzi do tej poprawki. Kolejka AMC jest lokalna i nie synchronizuje się z kolejką aplikacji Spotify.

## Wersja i commit

Kandydat `0.1.0-alpha.487`; commit i wynik instalacji podano poniżej. Wydanie stable oczekuje na odsłuch.

## Szkic opisu zmian do korekty użytkownika

AMC Public oddziela od AMC Test przeglądarkę odtwarzającą Spotify i zapisane logowanie. Odtwarzacz pokazuje rzeczywisty stan przygotowania i wyjaśnia brak skoku procentowego przed startem. To szkic, nie publikować go jako zatwierdzonego przewodnika.

## Korekta pakietu LibreSpot — 9.11.2026

Pierwsza zbudowana paczka techniczna miała jeszcze historyczny host LibreSpot i została odrzucona. Następny kandydat usuwa go z pakietu Public; instalator ma usunąć pozostałe pliki z istniejącej instalacji. Żadne dane konta ani profile nie są usuwane. Sam kod katalogu historycznego pozostaje w repozytorium przejściowo.

## Budowa i instalacja kandydata — 9.11.2026

Po odrzuceniu pierwszej paczki zawierającej LibreSpot zbudowano Public z czystego commitu `d2b5d2bb6e4f79201a0ef97e667ef46110c8e5d1`. Testy kontraktu Spotify: 2/2 zestawy; testy etykiet i transportu Python: 16/16; testy pakietu i instalatora bez LibreSpot: 58/58. Pełna paczka ma 3146 plików, `sourceDirty=false`, profil `public`, bez wpisu i plików LibreSpot. Dwie pełne kontrole integralności i kontrolowany start interfejsu/hosta zakończyły się powodzeniem.

Instalator `AMC-Public-2026-11-09-1806.exe` ma 133881473 bajty i SHA-256 `b9ad15687f6f7d3556cba75e261ab608fcfa35480993456edd35411891bb90ec`; kontrola zgodności z `.integrity.json` i skan sekretów przeszły. Został uruchomiony cicho po potwierdzeniu braku procesu Public i utworzeniu zweryfikowanej, zaszyfrowanej kopii ustawień poza repozytorium. Instalacja zakończyła się kodem 0. Zainstalowany `AMC.exe` ma wersję `0.1.0-alpha.487`, manifest dokładnego commitu, 3146 plików i brak dawnego katalogu LibreSpot; pełna kontrola integralności zwróciła kod 0. Uruchomiono jeden egzemplarz Public. Działający AMC Test pozostawiono nienaruszony.

Gałąź kandydata `codex/spotify-public-profile-20261109` została wypchnięta do autoryzowanego repozytorium GitHub `michalkasperczak/AMC`; odczyt GitHub potwierdził commit. Nie utworzono wydania stable ani nie wysłano instalatora jako zasobu Release przed użytkowym odsłuchem. Potwierdzenie dźwięku, skoku procentowego i wyjścia audio w zainstalowanym Public nadal oczekuje na użytkownika po ponownym logowaniu. Źródła, testy, paczka, instalacja i publikacja Release pozostają rozdzielonymi etapami.

## Audyt funkcji Spotify po instalacji — 9.11.2026

Testy źródła albumu, lokalnej kolejki, nawigacji i klienta hosta: 55/55. Testy GUI Spotify na runtime wxPython Public: 36/36 po uaktualnieniu wyłącznie przestarzałej nazwy callbacku w atrapie testu. Testy playlist, usuwania i palety poleceń: 27/27. Razem te próby potwierdzają działanie kontraktów kodu, lecz nie są odsłuchem ani próbą NVDA na koncie użytkownika. Testowe poprawienie atrapy wykonano po budowie instalatora; nie zmienia jego zawartości.
