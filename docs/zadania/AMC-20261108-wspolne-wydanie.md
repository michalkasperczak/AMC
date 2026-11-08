# AMC-20261108 — wspólne wydanie Test i Public

Stan: przygotowanie źródeł 0.1.0-alpha.486; pakiety, instalacja i publikacja jeszcze niepotwierdzone.

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
- Test na rzeczywistym koncie TIDAL, test logowania Spotify Public, odsłuch NVDA obu instalacji, kompletne paczki, instalatory oraz publikacja: oczekują.

## Materiał do przewodnika i dziennika zmian — szkic

AMC Public otrzymuje Spotify, Radio Archiwistę oraz funkcje konta YouTube obecne w Test. Sonos pozostaje tylko w Test. W albumach wykonawcy TIDAL prawa strzałka może ponownie prowadzić do otwartego wykonawcy również wtedy, gdy odpowiedź katalogu pomija relację przy albumie. Miejsca presetów mają krótsze etykiety. Poprawki playlist przyjęte przez użytkownika w Public alpha.485 są częścią obu aktualizacji. Tekst wymaga korekty użytkownika przed publikacją jako przewodnik; techniczne informacje wydania mogą być użyte w notatkach GitHub po weryfikacji pakietów.

## Ograniczenia i bramki

Źródło, test, zbudowany pakiet, instalacja i opublikowane wydanie to osobne stany. Przed podmianą Test sprawdzić nagrywanie i harmonogram; nie zamykać procesu podczas aktywnego nagrania. Przed publikacją sprawdzić commit, integralność, profil/wersję instalatorów oraz pozycje kanału aktualizacji. Nie kopiować kont ani identyfikatorów aplikacji z profilu użytkownika do instalatora. Po publikacji zweryfikować zdalny commit, nazwy, rozmiary i sumy plików.

## Wersja, commit i wynik końcowy

Planowana wersja obu profili: `0.1.0-alpha.486`. Commit, wyniki budowy, lokalizacje instalatorów, wyniki instalacji, publikacji i odbioru dopisać po faktycznym wykonaniu.
