# AMC alpha.519 — zgodny Test i Public po wydaniu alpha.518

- Data decyzji: 23.11.2026. Michał zlecił wydanie ukończonych dziś poprawek w obu kanałach: wideo, wyszukiwania, kolejki, lewej strzałki i czasu. Test może zawierać eksperymentalne Apple Music oraz odsłuch nagrania; Public pozostaje bez Apple Music, HEOS, Google Cast i odsłuchu nagrania.
- Korekta nadrzędna wobec wcześniejszego ograniczenia wideo do Test w `VIDEO-002-YouTube-online-ocena-20261123.md`: wideo lokalne, pobrane i YouTube online są dostępne również w Public.
- Stan wejściowy: Test `alpha.516` (`d998160c`) zawierał odsłuch trwającego nagrania. Wydane później Test `20:04` (`ff8eafd9`) oraz Test/Public `alpha.518` (`bb7c5e0a`) powstały z odrębnej linii. Test utracił możliwość `recording.monitor`. Instalator `20:04` zwrócił kod 0 i uruchomił AMC Test, lecz starszy aktualizator odrzucił manifest nowego pakietu komunikatem „profil wydania ma nieprawidłowe pole: lista możliwości”. Tego przebiegu nie zalicza się jako poprawnie zakończonej aktualizacji.

## Materiał do dokumentacji użytkownika

- W sesjach Pliki oraz Podcasty i YouTube zaznacz film. „Odtwarzaj wideo” jest w menu Odtwarzanie, menu kontekstowym, palecie `Ctrl+Shift+K` i pomocy. Obraz pojawia się w dotychczasowej zakładce odtwarzacza. „Odtwarzaj tylko audio” działa dla pliku lokalnego lub pobranego. Na filmie YouTube online użyj „Otwórz w przeglądarce”, jeśli twórca wyłączył osadzanie. W trybie YouTube online sam dźwięk z ukrytego filmu nie jest dostępny.
- Spacja wstrzymuje i wznawia, strzałki przewijają, Page Up/Page Down przechodzą po liście, Escape wraca do listy i pauzuje film. Czas pochodzi z odtwarzacza YouTube. Zakładki, rozdziały i wycinki wymagające hosta audio są wtedy niedostępne. Wywołanie ich skrótu daje komunikat „To polecenie nie jest dostępne podczas oglądania YouTube online”.
- `Ctrl+F` ustawia wykonawców przed albumami i utworami; `Ctrl+C` kopiuje nazwę, `Ctrl+Shift+C` dostępny link. Lewa strzałka na albumie TIDAL podaje zapisany czas lub „Sprawdzam czas albumu”, a po pobraniu pełnej listy czas albo „Czas trwania nieznany”. Prawa strzałka na utworze podaje powiązanie albumu i wykonawcy.
- W Podcastach i YouTube zwykły Enter wznawia od aktualnej zapisanej pozycji z biblioteki. Jawny skok do podanego czasu ma pierwszeństwo, a ustawienie „Zawsze od początku” nadal działa. Poprawka kolejki Spotify Soloist usuwa przedwczesne przejście po szybkim pominięciu utworu; nie dodaje nowego polecenia ani komunikatu.
- W AMC Test `Alt+R` otwiera listę aktywnych nagrań. Enter rozpoczyna prywatny odsłuch nagrywanego materiału, a `F2` zmienia nazwę pliku docelowego po zakończeniu nagrania. Public nie pokazuje tych dwóch poleceń. Test zawiera eksperymentalne logowanie Apple Music; sterowanie odtwarzaniem Apple Music nie zostało potwierdzone.
- `Shift+Spacja` na liście harmonogramów pomija tylko najbliższy termin; kolejne naciśnięcie przed terminem go przywraca. `Spacja` włącza lub wyłącza cały plan. Funkcja działa w obu profilach.

## Uzasadnienie i decyzje

- Odtwarzacz YouTube używa oficjalnego IFrame API w Edge WebView2 i osobnego wirtualnego adresu HTTPS dla Test i Public, powiązanego z ich `AppId`. Nie używa dodatkowego dekodera ani nie rozdziela strumienia audio i wideo.
- W Test zachowano `recording.monitor` z `alpha.516`; w Public możliwość i zależne od niej polecenia są wycięte profilem. Apple Music i HEOS są wycięte z Public, a Google Cast pozostaje zadaniem rozwojowym.
- Poprawka wznawiania podcastu przenosi odczyt punktu wznowienia z nieaktualnego wiersza listy do aktualnego rekordu bazy po zapisie poprzedniego odcinka.

## Testy i wynik

- Testy profili, wideo, nagrywania, TIDAL i Apple Music po integracji: 216 zaliczonych, 55 podtestów.
- Celowane testy wznawiania podcastów i profili: 132 zaliczone, 16 podtestów. LiteHost Release: 0 ostrzeżeń i 0 błędów.
- Kompilacja paczek, instalatory, publikacja i instalacja: do uzupełnienia osobnymi dowodami. Rzeczywisty odbiór NVDA wideo i odsłuchu nagrania w całym AMC nadal otwarty.

## Znane ograniczenia

- Film YouTube może odmówić osadzenia; wtedy należy skorzystać z jawnego polecenia otwarcia w przeglądarce. Wymagane są Edge WebView2 Runtime i połączenie z siecią.
- Public `alpha.518` ma wideo, lecz origin WebView2 dziedziczy identyfikator Test. W `alpha.519` rozdzielono adresy obu profili. Nie stwierdzono transmisji prywatnych danych z powodu starego origin; poprawka zapewnia właściwą tożsamość aplikacji.
- Nie przeprowadzono jeszcze próby NVDA na rzeczywistym filmie w zainstalowanym AMC Public ani na prywatnym odsłuchu nagrania w Test.

## Wersja i commit

- Kandydat: `0.1.0-alpha.519`, gałąź `codex/amc-video-recording-release-20261123`. Bazuje na ukończonym Test `d998160c`; przeniesiono online YouTube, poprawkę kolejki Spotify, logowania Apple Music, Public wideo i czasu TIDAL oraz poprawkę podcastów. Dokładny commit końcowy, instalatory i tagi dopisać po weryfikacji.
- Kod, testy, pakiet, instalator, publikacja i lokalna instalacja są odrębnymi stanami. Wersja Test `20:04` została zapisana w katalogu instalacji i uruchomiona, ale aktualizator zgłosił błąd kontraktu, więc wymaga zastąpienia poprawnym wydaniem.

## Szkic instrukcji i listy zmian do korekty Michała

**Instrukcja:** „W Plikach lub Podcastach i YouTube zaznacz film i wybierz «Odtwarzaj wideo». W przypadku filmu online obraz i dźwięk pochodzą z odtwarzacza YouTube. Spacja, strzałki i Page Up/Page Down działają jak w zwykłym odtwarzaczu AMC. Jeśli film nie pozwala na osadzenie, wróć do listy i wybierz «Otwórz w przeglądarce».”

**Lista zmian:** „AMC Test i Public: wideo lokalne, pobrane i YouTube online; poprawione wyszukiwanie, czas albumów TIDAL, wznawianie podcastów i kolejka Spotify; jednorazowe pomijanie terminu harmonogramu. AMC Test dodatkowo zachowuje odsłuch trwającego nagrania i eksperymentalne logowanie Apple Music.”

Ten szkic nie jest automatycznie publikowaną instrukcją użytkownika.
