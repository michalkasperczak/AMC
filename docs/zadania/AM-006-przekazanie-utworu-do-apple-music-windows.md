# AM-006 — przekazanie utworu do Apple Music dla Windows

- Status: **kod i testy celowane gotowe; żywe odtwarzanie, build, instalacja i wydanie niepotwierdzone**.
- Utworzono: 13 listopada 2026.
- Obszar: AMC Test, sesja Apple Music, Enter, menu, dostępność.
- Zależność: odczyt Biblioteki z AM-001/AM-003. Pełny transport w AMC pozostaje zadaniem AM-002.

## Zachowanie użytkowe w źródłach

- Enter na utworze oraz polecenie „Otwórz utwór w oryginalnym Apple Music” w menu Biblioteka i menu kontekstowym prowadzą do tej samej akcji.
- AMC używa oficjalnego aliasu `AppleMusic.exe` i przełącznika `/url`, które zainstalowana aplikacja Apple deklaruje w manifeście. Nie używa domyślnej przeglądarki.
- Pierwsza strona rzeczywistej Biblioteki zwróciła 25 rekordów: 21 z identyfikatorem katalogowym i zero z publicznym łączem. Dla numerycznego identyfikatora AMC tworzy łącze `music.apple.com/pl/song/...`; jeżeli Apple podało własne bezpieczne łącze, zachowuje je.
- Polecenie jest dostępne tylko w AMC Test na utworze z poprawnym celem i przy zainstalowanej aplikacji Apple Music. Album, wykonawca, playlista i rekord bez celu nie są reklamowane jako odtwarzalne.
- Po przekazaniu AMC mówi: „Otworzono utwór w oryginalnym Apple Music. Sprawdź odtwarzanie w aplikacji Apple Music.” Identyfikatory i obiekty dostawcy nie trafiają do mowy NVDA. Menu kontekstowe nie odbiera ponownie fokusu aplikacji Apple po udanym przekazaniu.

## Uzasadnienie i decyzje

- Apple nie dokumentuje interfejsu Windows, który każe oficjalnej aplikacji rozpocząć wskazany utwór z listy obcego programu. Manifest potwierdza obsługę łącza, ale nie automatyczny start dźwięku. Dlatego etykieta i komunikat mówią „otwórz”, a nie „odtworzono”.
- MusicKit on the Web oficjalnie oferuje odtwarzanie, ale wymaga oddzielnej autoryzacji i rozwiązania runtime/DRM z AM-002. Sam odczyt Biblioteki przez nieoficjalny adapter nie daje strumienia audio.
- Korekta użytkownika z 13 listopada 2026 zastępuje wcześniejsze odłożenie prób Apple Music zapisane w `PLAYER-20261113`: Enter oraz polecenie oryginalnej aplikacji mają być wdrażane teraz. Niniejszy etap dostarcza bezpieczne przekazanie; nie zamyka AM-002.

## Testy i wynik

- Testy adaptera, nawigacji, menu, palety, pomocy i atrapy GUI: **159 zaliczonych** w celowanym przebiegu.
- Dodatkowy przebieg `test_i18n.py`: 75 zaliczonych, dwa zastane długi kompletności angielskiego katalogu; nowe etykiety Apple dodano do katalogu.
- `git diff --check`: bez błędów białych znaków.
- Łącze zbudowane dla jednego rzeczywistego katalogowego utworu Biblioteki odpowiedziało HTTP 200; próba wypisała tylko status, bez tytułu i identyfikatora. To potwierdza adres, nie start dźwięku.
- Nie uruchomiono żadnego utworu Apple z tego kodu. Nie potwierdzono, czy `/url` samo zaczyna odtwarzanie; nie ma odsłuchu NVDA ani rzeczywistego pełnego utworu.

## Materiał do dokumentacji użytkownika

W AMC Test przejdź do sesji Apple Music i otwórz „Utwory” albo playlistę. Na utworze Enter lub polecenie „Otwórz utwór w oryginalnym Apple Music” przekazuje go do zainstalowanej aplikacji Apple. Gdy utwór nie ma celu katalogowego, AMC powie o braku możliwości przekazania. Po przekazaniu sprawdź odtwarzanie w aplikacji Apple; AMC nie twierdzi jeszcze, że dźwięk sam się rozpoczął. Sesja AMC Public nie pokazuje tego polecenia. Pauza, seek, kolejka, wybór wyjścia i pełne odtwarzanie sterowane z AMC pozostają poza tym etapem.

## Znane ograniczenia

- Wymagany jest ręczny test otwarcia konkretnego utworu, pełnego odtwarzania i komunikatów NVDA w oficjalnej aplikacji.
- Dla rekordów bez bezpiecznego łącza i numerycznego identyfikatora katalogowego akcja jest niedostępna.
- Przekazanie do zewnętrznej aplikacji nie daje AMC odczytu czasu ani własnego transportu.

## Wersja i commit

- Zmiany źródłowe i testy: commit `20c1cb3635d85cc6fa6cd461ace4e96d018ce41a`.
- Build, instalacja i publikacja: niewykonane.
