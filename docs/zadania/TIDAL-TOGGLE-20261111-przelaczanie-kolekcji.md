# TIDAL-TOGGLE — naprzemienne Ulubione i Biblioteka

Status: źródła poprawione; budowa, instalacja i odbiór wersji alpha.497 w toku.

## Zgłoszenie i korekta decyzji

11.11.2026 użytkownik potwierdził, że wydanie alpha.496 działa, lecz w TIDAL po każdym naciśnięciu `Ctrl+Shift+U` albo `Ctrl+Shift+L` słyszy dodanie. Poprosił o kontrolę wszystkich sesji i ekranów. To zastępuje wcześniejszą decyzję, która w katalogu TIDAL wymuszała dodanie, a w zapisanej kolekcji wymuszała usunięcie. Kod interfejsu faktycznie wysyłał `add=True` z katalogu, więc komunikat hosta odzwierciedlał powtarzaną operację, nie błędną etykietę NVDA.

## Zachowanie użytkowe

- W sesji TIDAL `Ctrl+Shift+U` zmienia Ulubione wybranego utworu lub wideo, a `Ctrl+Shift+L` Bibliotekę albumu, wykonawcy albo playlisty. W katalogu, kontenerze i odtwarzaczu kierunek ustala host ze stanu kolekcji po ostatnim potwierdzonym zapisie; dwa naciśnięcia mają dać dodanie, potem usunięcie. W nazwanej liście zapisanej kolekcji polecenie usuwa wybrany wpis i odświeża tę listę. Z odtwarzacza nie przełącza widoku na listę.
- Wyniki wyszukiwania i rozpoznanych utworów korzystają z tego samego trybu przełączania. Komunikat „Dodano…” albo „Usunięto…” pochodzi z wyniku operacji hosta po odpowiedzi usługi.
- Przegląd pozostałych sesji znalazł analogiczne wymuszenie dodania albumu na ekranie kontenera Spotify. Tam także używany jest wynik potwierdzony przez usługę, a widok zapisanej kolekcji nadal usuwa wybrany wpis. Sesje plików, Radia i Podcastów nie używają wymuszonego dodawania przy tych skrótach.
- Menu, paleta poleceń, pomoc kontekstowa i pełna pomoc już opisują te polecenia jako „dodaj lub usuń”; skróty i nazwy nie zmieniają się.

## Uzasadnienie i ograniczenia

Flagi w wierszach katalogu mogą być starsze od ostatniego zapisu. Dlatego interfejs nie wylicza przeciwnego kierunku samodzielnie: przekazuje tryb przełączania (`add=None`) do hosta. Host serializuje operacje i aktualizuje stan kolekcji dopiero po potwierdzeniu TIDAL. Widok zapisanej kolekcji pozostaje jednoznaczną podstawą do jawnego usunięcia.

Nie wykonano automatycznego zapisu i usunięcia pozycji na rzeczywistym koncie użytkownika. Zmiana stanu kolekcji przez inną aplikację między odświeżeniami AMC może wymagać ponownego odświeżenia widoku. Odbiór mowy NVDA i realnego konta pozostaje odrębnym etapem po instalacji.

## Testy i wersja

- Testy ekranów wyszukiwania, TIDAL, Spotify, rozpoznanych utworów, skrótów i menu: 240 zaliczonych.
- Testy hosta i pozostałych sesji, w tym lokalnych Ulubionych, Radia i Podcastów: 102 zaliczone. Wspólny przebieg wymagał uzupełnienia trzech starszych atrap stanu nawigacji w testach; nie była to zmiana zachowania produktu.
- Wspólny przebieg powyższych testów wraz z paletą poleceń: **325 zaliczonych**.
- Wersja docelowa: `0.1.0-alpha.497`; commit, paczka, instalacja i publikacja do uzupełnienia po weryfikacji.
