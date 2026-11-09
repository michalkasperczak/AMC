# Spotify Public — izolacja odtwarzacza i logowania

Data: 9.11.2026. Stan: poprawka źródłowa w kandydacie Public; odsłuch i odbiór NVDA wymagają próby użytkownika po instalacji.

## Uzasadnienie i decyzje

Użytkownik zalogował Spotify Public i odświeżył katalog, lecz utwór był niemy, mimo pozornego postępu. W logu Public dwukrotnie wystąpiło przekroczenie 45 sekund przy uruchamianiu ukrytego Chrome. Audyt wykazał, że Public i Test wskazywały ten sam profil Chrome `AMC Test` oraz tę samą nazwę tokenu Spotify w Menedżerze poświadczeń. Public mógł zderzyć się z działającym Test, a logowanie jednej wersji mogło nadpisać token drugiej.

Od tego kandydata Public ma własny katalog Chrome i własną nazwę poświadczenia; Test zachowuje dotychczasowe nazwy. Nie kopiujemy tokenu między profilami, ponieważ odświeżenie jednego egzemplarza mogłoby unieważnić drugi. Po instalacji kandydat Public wymaga ponownego jawnego logowania przez `Ctrl+F5`; Client ID zapisany w profilu Public pozostaje.

## Materiał do dokumentacji użytkownika

W AMC Public otwórz Spotify i naciśnij `Ctrl+F5`, aby po aktualizacji ponownie wybrać „Zaloguj w przeglądarce”. Własny Client ID pozostaje w ustawieniach; logowanie jest oddzielne od AMC Test. Utwór uruchom Enterem z listy. Gdy Spotify jeszcze uruchamia odtwarzacz, przycisk transportu pokazuje „Anuluj”, a próba skoku procentowego wyjaśnia, że trzeba zaczekać na rozpoczęcie. Po potwierdzonym starcie dostępne są czas, skoki procentowe cyframi `0–9` i `Shift+J`, przewijanie, pauza, głośność oraz `Shift+A` do wyboru wyjścia. Gdy Chrome przekroczy czas startu, NVDA poda tę przyczynę zamiast samej ciszy. Po rozpoczęciu dźwięku trzeba sprawdzić te czynności na rzeczywistym koncie.

## Testy i wynik

- Testy kontraktu profilów i katalogu Spotify w czystym checkoutcie: 2/2 zestawy zaliczone.
- Testy Python etykiet odtwarzacza, klienta i transportu: 16/16 zaliczone.
- Kompilacja hosta w głównym checkoutcie: 0 błędów, 0 ostrzeżeń; nie jest jeszcze kompilacją kandydata wydania.
- Odsłuch, nawigacja NVDA, paczka i instalacja: do wykonania osobno.

## Znane ograniczenia

Sama izolacja usuwa wykrytą kolizję, ale nie potwierdza jeszcze dźwięku na koncie użytkownika. Skok procentowy potrzebuje czasu trwania zgłoszonego przez Spotify po rzeczywistym starcie. Prędkość utworów Spotify nie jest trwała; osobne rozwiązanie dla podcastów nie wchodzi do tej poprawki. Kolejka AMC jest lokalna i nie synchronizuje się z kolejką aplikacji Spotify.

## Wersja i commit

Kandydat `0.1.0-alpha.487`, commit, paczka, instalacja i publikacja: do uzupełnienia po wykonaniu każdego etapu.

## Szkic opisu zmian do korekty użytkownika

AMC Public oddziela od AMC Test przeglądarkę odtwarzającą Spotify i zapisane logowanie. Odtwarzacz pokazuje rzeczywisty stan przygotowania i wyjaśnia brak skoku procentowego przed startem. To szkic, nie publikować go jako zatwierdzonego przewodnika.
