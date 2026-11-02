# SPOTIFY-20261101 — album, kolejka i komunikaty NVDA

- Status: **W trakcie** — zmiany źródłowe i testy bez instalacji oraz ręcznego odbioru NVDA.
- Zgłoszenie: 01.11.2026. Dotyczy bieżącej sesji Spotify i okna rozpoznanych utworów Radia.

## Zachowanie widoczne dla użytkownika

- Enter na utworze Spotify nie wypowiada już „Przygotowywanie odtwarzania Spotify”. Komunikat o wyniku pozostaje związany z potwierdzonym startem albo błędem.
- `Ctrl+Enter` na albumie Spotify pobiera cały dostępny album, zaczyna od pierwszego utworu i przechodzi do kolejnych w porządku albumu. Lista i zaznaczenie pozostają na miejscu. Gdy katalog zwraca tylko część albumu, AMC nie przedstawia jej jako całości.
- `Ctrl+Shift+Q` dodaje zaznaczony utwór lub odcinek do prywatnej kolejki AMC albo usuwa go, jeśli już tam jest. Kolejka jest zapisywana w profilu AMC; nie jest kolejką konta Spotify ani listą na innym urządzeniu.
- `Ctrl+Q` otwiera tę kolejkę. Enter odtwarza zaznaczony element, a po jego końcu AMC przechodzi do następnego bez włączania losowania. Skróty, menu Biblioteka, `Ctrl+F1`, pełna pomoc i paleta poleceń korzystają z tych samych akcji.
- `Shift+Enter` nie dodaje do kolejki; zgodnie z wcześniejszą decyzją nie przywracano tego gestu.
- Pasek stanu jest odświeżany po potwierdzonym `playback.started`; w odtwarzaczu przed pierwszą odpowiedzią hosta pokazuje znany tytuł. Szczegółowy opis dostępnych parametrów w `NVDA+End` nadal wymaga rzeczywistego odbioru NVDA. Zobacz `A11Y-20261030`.

## Decyzje i uzasadnienie

- 01.11.2026: zgłoszenie użytkownika zastępuje wcześniejszy brak skrótów kolejki Spotify. Wybrano kolejkę prywatną w AMC, bo sterowanie odtwarzaniem przez LibreSpot ma własną sekwencję; interfejs nie obiecuje zmiany kolejki konta ani urządzenia Spotify Connect.
- Album jest ładowany przed rozpoczęciem odtwarzania, aby `Ctrl+Enter` oznaczał cały album. Limit zabezpieczający to 2000 pozycji; przekroczenie lub niepełna odpowiedź kończy się komunikatem bez startu części albumu.
- Rutynowa informacja o przygotowaniu została usunięta z kanału mowy. Odpowiedź o sukcesie pozostaje po potwierdzeniu przez host.

## Testy i rzeczywisty wynik

- `test_spotify_gui_wiring.py`: 35 zaliczonych, Python 3.14.2 i wx 4.3.1; obejmuje pobranie wszystkich stron albumu, start i zachowanie listy.
- Testy źródła Spotify, nawigacji, menu, skrótów, palety i paska stanu: 160 zaliczonych; jeden błąd środowiskowy tworzenia katalogu tymczasowego pytest. Celowany test zapisu kolejki z jawnym katalogiem tymczasowym: 1 zaliczony.
- Testy są bez widocznego okna. Nie przeprowadzono instalacji, odsłuchu NVDA ani testu na realnym koncie Spotify/urządzeniu.

## Dalszy odbiór i znane ograniczenia

- Sprawdzić NVDA na albumie, utworze i kolejce: pierwszy element, strzałki, Enter, `Ctrl+Enter`, `Ctrl+Q`, `Ctrl+Shift+Q`, powrót fokusu i ponowne otwarcie.
- Zweryfikować `NVDA+End` w sesjach Pliki, Radio, Podcasty, Spotify (również przy przesyłaniu), Radio Archiwista i sesjach urządzeń. Raport „Gotowe” może dotyczyć starej zainstalowanej wersji; sam kod i testy nie dowodzą zachowania instalacji.
- W historii rozpoznanych utworów `Ctrl+Alt+S` sprawdzić liczbę wypowiedzi NVDA po prawej strzałce. Otwiera ona osobne okno działań; bez odsłuchu nie ustalono, który komunikat jest zbędny.
- Kolejka Spotify pozostaje lokalna w AMC. Jej edycja podczas bieżącego odtwarzania nie zmienia już rozpoczętej migawki sekwencji; nowe odtworzenie z kolejki pobiera aktualny porządek.

## Materiał do dokumentacji użytkownika — szkic

> W Spotify zaznacz album i naciśnij `Ctrl+Enter`, aby odtworzyć go od początku. Utwór lub odcinek dodaj do prywatnej kolejki AMC przez `Ctrl+Shift+Q`; ten sam skrót usuwa go z kolejki. `Ctrl+Q` pokazuje kolejkę. Enter uruchamia zaznaczony element. Kolejka AMC nie zmienia kolejki na koncie Spotify ani na innym urządzeniu.

## Wersja i commit

- Gałąź `codex/konfigurowalne-skroty-20261026`; roboczy przyrost po `833568c99b9f18cb02ab06802a352246a720f944`. Bez osobnego commitu, instalacji i wydania.

## Korekta 01.11.2026 — kolejka konta i tempo Spotify

- Użytkownik zapytał, czy kolejkę AMC można zsynchronizować z oryginalnym Spotify, i zgłosił, że przyspieszenie w odtwarzaczu Spotify działa tylko chwilę. Priorytetem jest tempo odcinków; muzyka może pozostać przy normalnej prędkości.
- Wcześniejsza decyzja o lokalnej kolejce pozostaje opisem obecnej implementacji, ale nie oznacza braku jakiejkolwiek integracji: oficjalne Web API Spotify udostępnia odczyt kolejki (`GET /me/player/queue`) i dodanie utworu lub odcinka do kolejki aktywnego urządzenia (`POST /me/player/queue`, Premium). Nie udostępnia w tym interfejsie usuwania elementu ani zmiany kolejności. Pełna dwukierunkowa synchronizacja prywatnej kolejki AMC nie jest więc obecnie potwierdzona.
- Poprzednie założenie w `session_options.py`, że adapter Spotify trwale obsługuje tempo 0,5–2,0x, zostało zastąpione. Kod zmieniał prędkość wewnętrznego elementu odtwarzacza; według zgłoszenia użytkownika efekt znika. Oficjalny Web Playback SDK i Web API nie udostępniają trwałego polecenia ustawienia tempa. Nie wzmacniamy nietrwałego obejścia.
- Sesja Spotify uruchamia materiał z normalnym tempem 1,00x. Skróty, menu, paleta, `Ctrl+F1` i ustawienia sesji nie reklamują regulacji prędkości w Spotify; przy bezpośredniej próbie wywołania kod podaje uczciwy komunikat. Sesja Podcasty i YouTube zachowuje własną regulację prędkości.
- Otwarta potrzeba: trwała regulacja tempa odcinków Spotify, jeśli dostawca udostępni wspierane polecenie. Do tego czasu nie obiecywać jej ani dla muzyki, ani dla odcinków w sesji Spotify.

Źródła pierwotne: https://developer.spotify.com/documentation/web-api/reference/get-queue ; https://developer.spotify.com/documentation/web-api/reference/add-to-queue ; https://developer.spotify.com/documentation/web-playback-sdk/reference

### Zmiany i testy tej korekty

- Kod: `wxlite/amc_wx_lite/session_options.py`, `shortcuts.py`, `menu_model.py`, `gui.py`; testy: `test_session_options.py`, `test_spotify_gui_wiring.py`, `test_menu_exposes_existing_actions.py`, `test_command_palette.py`.
- Po zmianie: 35 testów połączeń GUI Spotify oraz 126 testów opcji sesji, menu, skrótów i palety zaliczonych; test parytetu tempa Spotify/Podcasty zaliczony. Nie budowano i nie instalowano aplikacji; ręczny odsłuch NVDA nadal pozostaje otwarty.

### Materiał do dokumentacji użytkownika — aktualizacja szkicu

> Kolejka Spotify pod `Ctrl+Q` jest prywatną kolejką AMC. Spotify pozwala odczytać kolejkę konta i dodać do niej element, lecz AMC obecnie jej nie synchronizuje. W sesji Spotify regulacja tempa jest niedostępna, ponieważ wcześniejsza zmiana trwała tylko chwilę. Odcinki z sesji Podcasty i YouTube nadal mają regulację prędkości.

## Doprecyzowanie 01.11.2026 — priorytet tempa odcinków Spotify

- Użytkownik potwierdził sens prywatnej kolejki Spotify w AMC. Nie zastępuje ona kolejki konta Spotify ani nie musi jej wiernie odwzorowywać.
- Wcześniejsze wyłączenie mylącej regulacji tempa w całej sesji Spotify jest stanem przejściowym. Ostateczny cel: stabilna regulacja prędkości **odcinków Spotify** w AMC. Muzyka Spotify może pozostać przy normalnej prędkości. Nie włączać ponownie chwilowego obejścia z elementem HTML jako pozornego rozwiązania.
- Sprawdzone obecnie oficjalne interfejsy Spotify Web Playback SDK, Web API i Soloist nie dokumentują polecenia ustawienia prędkości odtwarzania. Mobilne App Remote SDK nie jest interfejsem dla AMC na Windows. Potrzebny wspierany tor albo jawny tryb odtwarzania dostępnej wersji RSS w sesji Podcasty, jeśli dany odcinek ma takie źródło. Dopasowania RSS nie wolno zakładać na podstawie samego tytułu.
- Do czasu rozwiązania nie reklamować tempa odcinków Spotify w menu, skrótach, pomocy ani palecie. Zachować temat jako otwartą funkcję, nie zamykać zadania jako ukończonego.

Źródła sprawdzone w tej korekcie: https://developer.spotify.com/documentation/web-playback-sdk/reference ; https://developer.spotify.com/documentation/web-api/concepts/scopes ; https://developer.spotify.com/documentation/soloist/reference/websocket-api
