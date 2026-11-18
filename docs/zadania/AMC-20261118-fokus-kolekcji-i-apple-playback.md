# AMC-20261118 — fokus kolekcji, następnie odtwarzanie Apple Music

Status: W trakcie — poprawka fokusu zintegrowana i odebrana headless;
rekonesans Apple zakończony, rzeczywista próba odtwarzania oczekuje
uzgodnionych warunków interaktywnych i zalogowanego konta.
Zlecenie Michała z 18.11.2026.

## Źródło i zakres

Oryginał, odczytany ponownie na komputerze `MICHAL` przez SSH:
`D:\Projekty Codex\AMC - notatki\AMc. CTRL-Shift-l dalsze prace nad odtwarzaniem Apple Music.md`.
SHA-256: `C136E1CCB171833FD77DE524EA2364AA9FC9F0DECEB821F39094B455EAB7F501`.
Oryginał pozostaje niezmieniony. Niniejszy rekord nie zastępuje materiału Michała.

Dwie kolejno realizowane części:

1. Po usunięciu z Biblioteki przez `Ctrl+Shift+L` lista Podcastów/YouTube
   przeskakuje na początek. Sprawdzić także dodawanie/usuwanie w Ulubionych,
   kolejkach i playlistach w odrębnych ścieżkach sesji i okien. Naprawiać
   wyłącznie potwierdzone błędy. Michał: „Nic nie ruszaj bez potrzeby, bo
   wszystko działa dobrze”. Nie przebudowywać dojrzałego interfejsu.
2. Następnie wrócić do wcześniejszej próby Apple Music: odnaleźć bibliotekę
   GitHub oraz ślady wycofanego odtwarzacza i błędnych identyfikatorów.
   Rozstrzygnąć, czy można odtwarzać dokładnie wybrany utwór przez oryginalny
   Apple Music dla Windows lub legalny odtwarzacz webowy. Nie utożsamiać
   widocznej Biblioteki, otwarcia linku i sterowania bieżącym utworem z
   potwierdzonym odtwarzaniem wskazanego elementu.

## Uzasadnienie i decyzje

- Najpierw poprawka fokusu; niezależne czytanie historii Apple może odbywać
  się równolegle, bez zmian odtwarzacza i działającej instalacji.
- Bazą jest bieżący AMC wxPython, nie historyczny interfejs WPF.
- Zachować działające wyszukiwanie i zapis do Biblioteki Apple Music.
- Wzorzec TIDAL dotyczy kontraktu użytkowego, a nie dowodu, że Apple ma ten sam
  protokół, SDK, sposób logowania lub dostęp do dźwięku.
- Deklarowana możliwość zakupu konta Apple nie jest poleceniem zakupu.
  Nie kupowano członkostwa ani abonamentu. Bez omijania DRM i kopiowania sesji.
- Bez instalowania, zamykania lub restartowania AMC na głównym komputerze.
- Bez nowego lokalnego buildu Windows, instalatorów, ISCC, dużych pobrań EXE,
  zmian ochrony i ponawiania historycznego reskanu RCX. Rozwój źródeł i
  bezokienne testy niezależne od kandydatów instalacyjnych pozostają dozwolone.
- Zadanie samo w sobie nie uruchamia nowego wydania ani publikacji instalatora.

## Kryteria odbioru fokusu

- Zmiana samego znacznika członkostwa zachowuje tożsamość wybranego wiersza.
- Po rzeczywistym usunięciu wiersza wybrany jest logiczny sąsiad, także dla
  ostatniego i jedynego elementu; brak bezwarunkowego skoku na początek.
- Odmowa/błąd nie zmienia listy ani zaznaczenia i daje jeden właściwy komunikat.
- Spóźniony wynik operacji nie przywraca porzuconego widoku lub zaznaczenia.
- Sprawdzić wspólne i odrębne handlery Biblioteki, Ulubionych, kolejki i
  playlist, głównej listy oraz wyszukiwania; zapisać pokrycie i ograniczenia.
- Rzeczywisty test RED musi wykazać błąd przed minimalną poprawką, a celowane
  testy GREEN potwierdzić zachowanie produkcyjnej ścieżki. Bez tworzenia
  pełnej kartezjańskiej regresji wszystkich sesji.
- Headless nie dowodzi natywnego fokusu Windows ani mowy żywego NVDA.
  Te etapy raportować osobno; nie uruchamiać samowolnie uciążliwych prób GUI.

## Kryteria badania Apple Music

- Podać nazwę/repozytorium i rolę wcześniejszej biblioteki, dokładne moduły,
  ślady prób i przyczynę wycofania na podstawie plików, historii i pomiarów.
- Rozdzielić identyfikator Biblioteki, identyfikator katalogu, adres WWW i cel
  transportu. Nie zgadywać utworu po zbliżonym tytule i nie zamieniać ID.
- Oddzielnie ocenić dostępne API/SDK, właściwy runtime/DRM, warunki użycia i
  wymagania konta. Brak jednego wyniku nie dowodzi ogólnej niemożliwości.
- Zaproponować i, jeśli da się bezpiecznie wykonać, przeprowadzić najmniejszą
  miarodajną próbę. Odtworzenie próbki, samo uruchomienie aplikacji ani mock
  nie są dowodem pełnego odtwarzania dokładnie wybranego utworu.
- Jeśli potrzebne jest konto lub interaktywny etap, podać konkretną blokadę
  i warunki próby; nie logować ani nie ujawniać tokenów, cookies i sekretów.

## Stan zastany, wersja i commit

- Checkout: `/home/michal/projekty/AMC`, gałąź `codex/amc-test-public-20261109`.
- Baza rozpoczęcia: `a1fac36cb11c88e062dc0c768d2d3d8805a9b132`.
- Zastane lokalne materiały porządkowe `DOC-20261118` i modyfikacja rejestru
  są zachowane; nie resetowano ani nie czyszczono worktree.
- Opublikowane wcześniej Public/Test z 18.11 wskazują `1723786`; nie zawierają
  jeszcze poprawek z tego nowego zadania. Nie zmieniać ich opisów na wyrost.
- Izolowana gałąź diagnozy fokusu: `codex/amc-membership-focus-20261118`.
- Badanie Apple jest odczytowe, poza repo. Wyników wykonawców nie traktować
  jako odbioru: wymagane sprawdzenie konkretnego diffu i dowodów.

## Testy i wynik

Diagnoza wykazała brak przekazania sąsiada do odświeżenia Biblioteki
Podcastów/YouTube po usunięciu źródła. Izolowany commit kandydata:
`b31e455a3f992da4287951b9fe48691c3ba80baa` — tylko
`wxlite/amc_wx_lite/gui.py` i `wxlite/tests/test_podcast_library_queue.py`.
Nie zmieniano działających ścieżek innych sesji ani Apple Music.

- RED wykonawcy: produkcyjny handler i rzeczywiste `Navigator`/`ListModel`
  wybierały `podcast-1` zamiast `podcast-3`; 1 test niezaliczony, exit 1.
- GREEN wykonawcy: zapisane logi pokazują 263 zaliczone i 11 pominiętych;
  oddzielny zestaw granic pokazuje 23 zaliczone. Nie sumować bez sprawdzenia
  nakładania się selekcji. Zbiorczy log nie zawiera pełnej komendy wyboru.
- Własna próba graniczna rodzica: 5 przypadków produkcyjnego handlera i
  produkcyjnego odświeżenia, wszystkie zaliczone: usunięcie środka, ostatniego,
  jedynego, dwóch zaznaczonych źródeł oraz odwrócona kolejność widocznej listy.
  Komenda z worktree kandydata: `PYTHONPATH=wxlite:wxlite/tests uv run --python
  3.11 --with pytest python -m pytest -q
  /home/michal/.hermes/profiles/amc/cache/scratch/amc-membership-review-20261118/test_source_removal_focus.py`.
  Wynik: `5 passed in 0.43s`. Pierwsza próba aparatu nie zebrała testów wskutek
  błędnego importu własnego stubu; po użyciu istniejącego `test_gui_logic`
  właściwy test wykonano. To nie był RED produktu.
- Pierwsze niezależne review potwierdziło zwykłe granice i brak regresji,
  ale wskazało istniejący wcześniej błąd dla aktywnego filtra: wybór sąsiada
  z pełnego modelu wskazywał ukryty wiersz, a kontrolka wracała na pierwszy
  widoczny. Uwagi nie pominięto jako „starej”: dotyczy bezpośrednio zlecenia.
- Dodatkowy RED rodzica: dla filtra `match` wybrany `podcast-hidden` zamiast
  `podcast-3`; 1 niezaliczony, 1 zaliczony, 6 poza selekcją. Minimalnie
  zastosowano istniejące `list_filter.filter_rows` przed wyborem sąsiada,
  bez zmiany wspólnego helpera i innych sesji. Commit uzupełnienia:
  `7bff7b6c91b2645a9ca9222b0155cea041aa43c5`.
- Końcowy celowany przebieg: cały `test_podcast_library_queue.py` (8
  przypadków) i własne 5 granic: `13 passed in 0.72s`. To bieżąca akceptacja
  headless, nie dodawanie jej liczby do dawnych 263/23. Drugie, ograniczone
  review zaakceptowało poprawkę filtra bez uwag blokujących i potwierdziło
  produkcyjny związek `filter_box` z listą Podcastów. Po sprawdzeniu braku
  nakładania plików wykonano fast-forward głównego checkoutu do `7bff7b6`;
  oba pliki produkcyjnej zmiany są bajtowo identyczne z odebranym kandydatem.
  Po integracji sprawdzono tylko krytyczną granicę (oba warianty filtra):
  `2 passed, 6 deselected in 0.71s`; `git diff --check` zaliczone.
- Brak natywnego Windows/wx, żywego NVDA, buildu, instalacji i nowego wydania.

## Wynik odczytowego badania Apple Music

Odnaleziono `LunaParker/apple-music-py`, pakiet `appmusic` przypięty w AMC do
`1d698359155d9b70d640234a1489165a06e6e01b`. Świeży odczyt GitHub potwierdził
repozytorium aktywne, licencję MIT i status Alpha w README. To klient
nieoficjalnego API Biblioteki/katalogu, nie silnik odtwarzania. Licencja kodu
nie jest zgodą Apple na używanie jego usług w dowolny sposób. Nie odczytano
prywatnych cookies ani tokenów. Dane i metadane nie zostały wysłane do obcych
skanerów ani usług analitycznych.

W aktualnym modelu istnieją dwa odrębne ID: `resource_id` z Biblioteki oraz
`catalog_id` z `attributes.playParams.catalogId`. Obecna translacja potrafi
je prawidłowo rozdzielić: własne fixture'y wykonawcy sprawdziły rekord
biblioteczny z katalogowym ID, rekord bez katalogowego ID i rekord katalogu.
Brak katalogowego ID blokuje obecną ścieżkę katalogową/WWW; nie dowodzi
niemożliwości odtworzenia zasobu jakąkolwiek inną metodą w aplikacji Apple.
Nie dowiedziono, że dawną awarię powodowało wyłącznie pomieszanie ID.

AM-002 dokumentuje również odrębny, konkretny błąd wycofanego hosta: po
rozbieżności wskazanego i bieżącego utworu host mógł zwrócić błąd, ale nie
zatrzymać dźwięku. GUI po błędzie nie uznawało go za aktywny tor, a Spacja
sterowała oryginalną aplikacją. Wycofanie powiązania hosta z Enterem było
uzasadnione; nie oznacza dowodu ogólnej niemożliwości WebView/MusicKit.

Obecne jawne `AppleMusic.exe /url` otwiera link. Historyczne próby SMTC
potwierdziły pauzę/wznowienie bieżącego utworu, lecz nie wybór dokładnie
przekazanego utworu. To pilot transportu, nie udowodniona selekcja katalogowa.
Istnieje też historyczny trop interfejsu WinRT `IPlayer` z
`StartOrQueueLibraryPlaybackAsync` (pełny wpis pamięci `1789981152`); same
metadane interfejsu nie dowodzą dostępności z obcego procesu i skutku Play.

Świeża strona Apple `https://developer.apple.com/musickit/` potwierdza
MusicKit on the Web jako drogę odtwarzania w przeglądarce; nie potwierdza
zgodności konkretnego WebView, konta, jakości audio ani praw licencyjnych
naszej implementacji. Jeden odsyłacz do szczegółów `setQueue` był nieaktualny
(strona nieznaleziona); nie użyto go jako dowodu zachowania.

Najmniejsza miarodajna następna próba: dwa znane różne utwory z poprawnymi
katalogowymi ID, jawne przekazanie do oryginalnej aplikacji i porównanie
bieżącego utworu SMTC z oboma celami. Dopiero potem osobno transport.
Alternatywa webowa musi najpierw potwierdzić właściwy utwór, zatrzymanie po
błędzie i jeden rozłączny tor sterowania. Potrzebne są uzgodnione warunki
widocznej próby i zalogowana aplikacja; nie uruchomiono jej samowolnie.
Raport techniczny: `state`/scratch rekonesansu Apple, `RAPORT-READONLY.md`.

## Znane ograniczenia i następny krok

Następny krok: uzgodnić krótką widoczną próbę wyboru dwóch utworów w
zalogowanym Apple Music, bez instalacji AMC i automatyzacji cudzego GUI.
Do tego czasu nie uruchamiać dźwięku ani logowania. Kod poprawki fokusu jest
zintegrowany i odebrany headless, nie odebrany przez żywy NVDA i nie wydany.
Nie traktować rekonesansu jako działającego nowego odtwarzacza.

## Dostawa raportu

Zapisano nowy raport na `MICHAL`:
`D:\Projekty Codex\AMC - notatki\AMC - wynik fokusu i analiza Apple Music 2026-11-18.md`.
Po zapisie odczytano cały plik, potwierdzając 3649 bajtów i SHA-256
`77359dcd15ff64b866c67b70ad18de0ab890985e30ee17bd54aa5ed318e96aaa`.
Nie nadpisano istniejących innych materiałów ani oryginału zlecenia.
Do map `00 - Zacznij tutaj.md` i `Hermes\AMC\Czytaj najpierw.md` dopisano
wyłącznie odsyłacz do nowego wyniku. Pełny odczyt obu celów potwierdził,
że wszystkie wcześniejsze bajty zostały zachowane jako niezmieniony prefiks.
Źródła sprzed dopisku przechowano tylko na Hermesie, bez plików kopii na PC.
Dowody (w tym oryginalny RED, logi wykonawcy, fixture'y i raport Apple)
przeniesiono z cache do trwałego `state/amc-focus-apple-20261118` profilu AMC.
Nie są zależne od 24-godzinnego czasu życia scratch.

## Materiał do dokumentacji użytkownika

Oczekiwany efekt poprawki: dodawanie i usuwanie elementów kolekcji nie gubi
miejsca na liście. Skróty i dotychczasowy układ interfejsu pozostają bez zmian.
To na razie kryterium odbioru, nie informacja o wdrożeniu na komputerze Michała.

Apple Music w bieżącym Test nadal umożliwia wyszukiwanie i zapis do Biblioteki.
Pełne odtwarzanie wskazanego utworu w nowym torze nie jest jeszcze potwierdzone.
