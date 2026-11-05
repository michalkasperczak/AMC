# LIST-SEARCH-GLOBAL-20261105 — krótsze listy i wspólne podglądy

Status: kod i 240 testów gotowe 5 listopada 2026; kandydat AMC Test alpha.480 czeka na budowę, instalację i odbiór NVDA po zakończeniu dwóch trwających nagrań.

## Zgłoszenie i korekta użytkownika

5 listopada 2026 użytkownik potwierdził, że w alpha.477 strzałki w odtwarzaczu i lista nagrań działają szybko. Pierwsze wczytanie TIDAL na wolnym łączu trwało około 20 sekund, Spotify krócej, natomiast dalsza nawigacja była szybka. Korekta zastępuje wcześniejsze zgłoszenie o stale wolnych listach. Nowe wymagania: zwykłe listy utworów TIDAL/Spotify nie czytają słowa „utwór”; lewa strzałka na utworze czyta czas i tytuł; wyniki wyszukiwania zachowują rodzaj pozycji; Alt+R, Alt+Shift+R, Ctrl+I i Ctrl+Alt+S działają z każdej sesji oraz Escape wraca do miejsca wywołania.

## Materiał do dokumentacji użytkownika

- Na zwykłej liście utworów TIDAL lub Spotify strzałki góra/dół czytają tytuł i dostępne szczegóły bez słowa „utwór”. W wynikach Ctrl+F i Ctrl+Shift+F kolumna „Rodzaj” rozróżnia utwór, album i wykonawcę. Prawa strzałka na wyniku Spotify/TIDAL prowadzi do dostępnych relacji na podstawie już wczytanych danych; otwarcie relacji może pobierać jej zawartość.
- Lewa strzałka na utworze TIDAL/Spotify mówi najpierw czas, potem tytuł utworu, np. „3:21, Tytuł”. Gdy w ustawieniach odczytu dla lewej strzałki czas jest wyłączony lub niedostępny, obowiązują pozostałe zaznaczone informacje.
- Alt+R otwiera aktualnie nagrywane pliki, Alt+Shift+R listę nagrań, Ctrl+I skrzynkę nowych odcinków i materiałów, a Ctrl+Alt+S listę rozpoznanych utworów. Skróty są dostępne w każdej sesji, także TIDAL, Spotify, WiiM, Sonos i Frontier Smart. Escape z podglądów przywraca dokładne miejsce listy lub odtwarzacza. Okno rozpoznanych utworów zamyka się do miejsca wywołania bez przełączania sesji.
- Ctrl+Shift+F obejmuje zapisane i aktualnie znane programowi dane. W Spotify dodaje aktualnie wczytaną listę i historię odtwarzania bez dodatkowego zapytania do serwisu. Pełny katalog Spotify i TIDAL jest dostępny przez Ctrl+F w danej sesji. Zawartość wyników może być ograniczona do wcześniej wczytanych pozycji.
- W opcjach ogólnych przełącznik „Wspólny podgląd transmisji wybranych kanałów we wszystkich sesjach” dotyczy tylko transmisji; nie blokuje czterech wspólnych list opisanych wyżej. Pomoc Ctrl+F1, pełna pomoc, menu i paleta Ctrl+Shift+K pokazują ich aktualną dostępność.

## Uzasadnienie i decyzje

Zwykłe listy są jednorodne, więc nazwa rodzaju przed każdym utworem wydłuża mowę. Wyniki wyszukiwania mieszają rodzaje, dlatego zachowują osobną kolumnę „Rodzaj”. Dodatkowe Spotify w Ctrl+Shift+F bazuje na migawce listy i historii, aby nie wydłużać globalnego wyszukiwania zewnętrznym zapytaniem. Strzałki po wynikach nie pobierają relacji z sieci. Wspólne listy korzystają z jednej bramki możliwości poleceń.

## Testy i wynik

Testy źródeł TIDAL/Spotify, mowy lewej strzałki, wyszukiwania, menu, palety, pomocy, routingu skrótów i podglądów: 240 zaliczonych. `git diff --check` bez błędów. Ręczny test NVDA nowej wersji nie został jeszcze przeprowadzony. W działającym AMC Test alpha.479 `Alt+R` pokazał 5 listopada około 23:01 dwa aktywne nagrania: TVP Info i Koszalin. Nie zamknięto AMC, nie uruchomiono budowy ani instalatora, by nie zakłócić nagrań.

## Znane ograniczenia

Pierwsze otwarcie listy TIDAL może zależeć od łącza i trwać kilkanaście sekund. Ctrl+Shift+F nie przeszukuje pełnych katalogów kont TIDAL/Spotify. Dostępność relacji wykonawcy lub albumu zależy od metadanych danego wyniku. Odbiór NVDA musi sprawdzić początkowe zaznaczenie, strzałki, Escape, fokus oraz ponowne otwarcie w kilku sesjach.

## Wersja i commit

Planowana wersja: AMC Test 0.1.0-alpha.480. Commit kodu i testów: `d28eb97b`. Budowa: niewykonana. Instalacja: niewykonana. Publikacja: niewykonana; kandydat wymaga ręcznego odbioru.
