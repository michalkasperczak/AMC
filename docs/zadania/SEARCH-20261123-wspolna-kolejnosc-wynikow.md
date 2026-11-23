# SEARCH-20261123 — wspólna kolejność wyników Ctrl+F

- Data zgłoszenia i korekty użytkownika: 23.11.2026.
- Status: zmiana źródeł i celowane testy zakończone; brak buildu, instalacji,
  publikacji i ręcznego odbioru NVDA.
- Baza źródeł: `0f58310daca70f3969b891ed5160bc9aa880748c`, gałąź
  `codex/amc-test-public-20261109`. Nowe zmiany pozostają bez commitu.
- Profil: wspólna wyszukiwarka AMC Test i AMC Public; katalog Apple Music jest
  dostępny tylko tam, gdzie profil udostępnia tę usługę.

## Decyzja i zachowanie użytkowe

Korekta użytkownika z 23.11.2026 zastępuje wcześniejszą decyzję o zachowaniu
surowej kolejności źródła na liście wyników. W `Ctrl+F` i `Ctrl+Shift+F`
wyniki są grupowane według usługi w bieżącej kolejności sesji. W każdej usłudze
wykonawcy są przed albumami, albumy przed utworami, a playlisty po utworach.
Podcasty i kanały są przed odcinkami, foldery przed plikami lub nagraniami.
W obrębie jednego rodzaju dokładny tytuł poprzedza początek tytułu, potem
pozostałe dopasowania; przy równym wyniku zachowana jest kolejność źródła.

Reguła jest jedna w `wxlite/amc_wx_lite/search_catalog.py` i działa na wspólnym
`SearchEntry` oraz `Row.kind`. Nowa sesja podłączona do wspólnej wyszukiwarki
otrzymuje ją automatycznie, jeśli adapter używa standardowych rodzajów wierszy.
Nie ma osobnego sortowania dla Apple Music, TIDAL ani Spotify. W Apple Music
lista otwarta z wyników zachowuje tę samą kolejność. Wyszukiwanie nadal
zachowuje pierwotny wiersz, identyfikator nawigacji i adres, więc zmiana pozycji
nie zmienia celu Entera ani działań na wyniku.

Na liście wyników `Ctrl+C` kopiuje nazwę, `Ctrl+Shift+C` dostępny adres
publiczny. Jeśli adresu nie ma, AMC mówi „Wybrane wyniki nie mają adresu”.
Lewa strzałka czyta informacje o wyniku, dla Apple Music także znaną długość
i warianty audio. Prawa strzałka otwiera dostępne powiązania; lewa strzałka lub
Escape na liście powiązań wraca do wyniku. Same powiązania i ich cele pozostają
odpowiedzialnością adaptera usługi, ponieważ nie da się ich wyprowadzić z
samej nazwy lub rodzaju wiersza. Podpowiedź w oknie wyszukiwania wymienia
kopiowanie i strzałki. Nie zmieniono skrótów ani poleceń głównego okna.

## Lokalizacje i weryfikacja

- `wxlite/amc_wx_lite/search_catalog.py`: wspólna kolejność rodzaju i trafności.
- `wxlite/amc_wx_lite/gui.py`: zastosowanie kolejności po zebraniu źródeł,
  zgodna lista Apple Music po otwarciu wyniku, podpowiedzi klawiszowe.
- `wxlite/tests/test_search_catalog.py`: każda obecna i przyszła wartość
  `SessionId` przechodzi ten sam test hierarchii rodzajów.
- `wxlite/tests/test_search_gui_navigation.py`: kolejność Apple Music w obu
  listach oraz `Ctrl+C` i `Ctrl+Shift+C` na wyniku.

Uruchomiono z katalogu `wxlite` przez
`D:\Projekty Codex\AMC - zależności\python-test-envs\wxpython-4.3.1-cp314\Scripts\python.exe`:

1. `-m pytest -q -p no:cacheprovider tests\test_search_catalog.py tests\test_search_gui_navigation.py tests\test_apple_music_navigation.py tests\test_tidal_navigation.py tests\test_spotify_navigation.py` — **176 passed**.
2. `-m pytest -q -p no:cacheprovider tests\test_apple_music_library.py tests\test_apple_music_source.py tests\test_remote_search.py` — **42 passed, 3 subtests passed**.
3. `-B -c` z `ast.parse` dla czterech zmienionych plików Python — **4 pliki
   poprawne składniowo**, bez zapisu plików wynikowych.
4. Po ostatniej drobnej poprawce: ponowne uruchomienie dwóch testów
   wyszukiwania z punktu 1 — **121 passed**.

Pierwsza próba uruchomiona z katalogu głównego repozytorium zakończyła się
błędem importu `amc_wx_lite`; poprawny przebieg z `wxlite` zaliczył testy.
Osobne `py_compile` nie mogło zapisać pliku cache w ograniczonym środowisku;
kontrola `ast.parse` zakończyła się poprawnie, a pytest zaimportował moduły.
Nie uruchamiano okna, kont usług ani schowka systemowego. Ręczny test NVDA
pozostaje do wykonania: pierwszy fokus i odczyt rodzaju wyniku, strzałki,
powiązania i powrót, oba skróty kopiowania, Enter, Escape i ponowne otwarcie
w Apple Music, TIDAL, Spotify oraz Bibliotece lokalnej. Testy źródłowe nie są
dowodem działania z kontem ani zgodności mowy NVDA.
Weryfikacja na żywym interfejsie wymaga wcześniejszego potwierdzenia, że AMC
nie nagrywa. Odczyt stanu procesów przez CIM i `tasklist` był w tym środowisku
zablokowany. Zgodnie z `wxlite/JAK_TESTOWAC.md` przed widoczną próbą GUI/NVDA
potrzebna jest zgoda użytkownika; pytanie wysłano 23.11.2026 i pozostaje bez
odpowiedzi. Obowiązuje też decyzja z 23.11.2026 o wstrzymaniu instalatorów i
publikacji alpha.514.

## Szkic instrukcji użytkownika do zatwierdzenia

W sesji naciśnij `Ctrl+F`, wpisz nazwę i naciśnij Enter. Wyniki tej usługi
zaczynają się od wykonawców, następnie pokazują albumy i utwory. Wyszukiwanie
we wszystkich usługach (`Ctrl+Shift+F`) grupuje wyniki według usług. Na wyniku
naciśnij prawą strzałkę, aby sprawdzić powiązania, lub lewą, aby odczytać
informacje. `Ctrl+C` kopiuje nazwę, a `Ctrl+Shift+C` link, jeśli usługa go
udostępnia. Enter otwiera wybrany wynik.

## Szkic wpisu do listy zmian do zatwierdzenia

Ujednolicono kolejność wyników wyszukiwania w usługach: wykonawcy są przed
albumami i utworami, a dokładne dopasowania są pierwsze w swoim rodzaju.
Otwarta lista wyników Apple Music zachowuje tę samą kolejność. Podpowiedzi
wyszukiwarki opisują kopiowanie nazwy i linku oraz nawigację strzałkami.
