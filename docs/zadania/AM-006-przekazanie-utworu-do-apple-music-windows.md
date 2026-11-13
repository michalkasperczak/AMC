# AM-006 — przekazanie utworu do Apple Music dla Windows

- Status: **kod przekazania i transportu w źródłach; dokładne odtwarzanie Enterem nie działa; instalacja i wydanie niewykonane**.
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
- Doprecyzowanie użytkownika z 13 listopada 2026 zastępuje kolejność z commitu `20c1cb36`: na utworze Apple Music polecenie oryginalnej aplikacji jest pierwszą pozycją menu kontekstowego. Najpierw sprawdzamy żywym przekazaniem, czy aplikacja Apple rzeczywiście przyjmuje wskazany utwór.
- Korekta użytkownika z 13 listopada 2026 zastępuje wcześniejszy cel Entera jako zwykłego otwarcia: Enter ma uruchamiać **dokładnie zaznaczony utwór w tle i pozwalać sterować nim z AMC**. Dotychczasowe przekazanie łącza nie spełnia tego celu i nie może być opisane jako odtwarzanie.
- Użytkownik potwierdził, że oficjalny Apple Music sam rozpoczął odtwarzanie podczas prób i słyszał Whitney Houston oraz jeszcze coś, ale kolejność naszych prób nie pozwoliła mu przypisać dźwięku do konkretnej akcji. Windows SMTC pokazał „I Wanna Dance with Somebody (Who Loves Me)” Whitney Houston po próbie przekazania „A Prayer By The River (Instrumental)” Bruce’a Springsteena. To nie potwierdza zgodności wskazanego utworu.

## Testy i wynik

- Testy adaptera, nawigacji, menu, palety, pomocy i atrapy GUI: **159 zaliczonych** w celowanym przebiegu.
- Dodatkowy przebieg `test_i18n.py`: 75 zaliczonych, dwa zastane długi kompletności angielskiego katalogu; nowe etykiety Apple dodano do katalogu.
- `git diff --check`: bez błędów białych znaków.
- Łącze zbudowane dla jednego rzeczywistego katalogowego utworu Biblioteki odpowiedziało HTTP 200; próba wypisała tylko status, bez tytułu i identyfikatora. To potwierdza adres, nie start dźwięku.
- Nie uruchomiono żadnego utworu Apple z tego kodu. Nie potwierdzono, czy `/url` samo zaczyna odtwarzanie; nie ma odsłuchu NVDA ani rzeczywistego pełnego utworu.
- Korekta wyniku żywego z 13 listopada 2026: `/url` otworzył album Bruce’a Springsteena. Odczyty Windows po próbach z oficjalnym linkiem katalogowym, `/play` i `music://` nadal wskazywały utwór Whitney Houston w pauzie; nie potwierdziły startu dokładnie przekazanego utworu. Użytkownik słyszał automatycznie uruchamiane utwory, lecz nie da się na podstawie tej sekwencji określić, która próba je wywołała. `Ctrl+Spacja` przy fokusie na okładce albumu nie zmieniło późniejszego odczytu SMTC.
- Oddzielny test SMTC na oficjalnej aplikacji: `TryTogglePlayPauseAsync` zwrócił `true`, stan zmienił się z `Paused` na `Playing`, a po dwóch sekundach z powrotem na `Paused`. Sesja zgłaszała `canNext=true`, `canPrevious=true`, `canSeek=false`; tego ostatniego nie reklamujemy jako przewijania.
- Źródła nowego transportu: Spacja, Shift+Page Down i Shift+Page Up kierują polecenia tylko do sesji Apple Music. Celowane testy Python: **111 zaliczonych** dla menu, skrótów, palety i GUI; test kontraktu hosta: **1 zestaw zaliczony**; kompilacja hosta Release: **0 błędów**. Są to wyniki źródeł, nie dowód zainstalowanej wersji ani pełnego utworu.

## Materiał do dokumentacji użytkownika

Roboczy opis źródeł AMC Test: w sesji Apple Music wybierz „Utwory” albo playlistę. Pierwsza pozycja menu kontekstowego „Otwórz utwór w oryginalnym Apple Music” przekazuje link do oficjalnej aplikacji. Enter obecnie robi to samo, lecz **nie uruchamia pewnie wskazanego utworu** i musi zostać zmieniony przed uznaniem celu za wykonany. Jeżeli w Apple Music gra już materiał, Spacja w AMC przełącza pauzę i wznowienie, a Shift+Page Down/Up prosi o następny/poprzedni utwór w kolejce oficjalnej aplikacji. AMC odczytuje rzeczywisty tytuł z Windows. Funkcji nie ma w AMC Public. Przewijanie utworu nie jest dostępne przez tę sesję Windows.

Roboczy tekst instrukcji i listy zmian: [AM-006-instrukcja-i-lista-zmian-szkic.md](AM-006-instrukcja-i-lista-zmian-szkic.md). Nie publikować przed odbiorem.

## Znane ograniczenia

- Wymagany jest uporządkowany test dwóch różnych utworów: osobne polecenie, odczyt tytułu z Windows po każdej próbie i potwierdzenie pełnego odtwarzania bez przełączania okna. Obecny link otwiera album; zgodność utworu i kolejki pozostaje niepotwierdzona.
- Dla rekordów bez bezpiecznego łącza i numerycznego identyfikatora katalogowego akcja jest niedostępna.
- Windows SMTC daje stan i podstawowy transport bieżącej sesji Apple Music, ale nie wybiera katalogowego utworu. Przewijanie nie jest zgłaszane przez aplikację.

## Wersja i commit

- Zmiany źródłowe i testy: commit `20c1cb3635d85cc6fa6cd461ace4e96d018ce41a`.
- Build, instalacja i publikacja: niewykonane.
- Transport SMTC, kolejność menu i aktualizacja dokumentacji: commit `7c3b9a26` z 13 listopada 2026. Host zbudowany tylko w katalogu repozytorium, bez instalacji.
