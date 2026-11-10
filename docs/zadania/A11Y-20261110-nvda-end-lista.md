# A11Y-20261110 — NVDA+End odczytuje „Lista” zamiast parametrów

Data: 10 listopada 2026. Status: poprawka źródłowa, testy i paczka dodatku NVDA 0.4.1 gotowe; instalacja i odbiór żywym NVDA pozostają do wykonania.

## Zachowanie widoczne dla użytkownika

Użytkownik zgłosił, że NVDA+End mówi „Lista” zamiast bitrate'u, parametrów nagrania i bieżącego utworu. Potwierdził TIDAL i Spotify, a Podcasty wskazał jako prawdopodobnie dotknięte. Spotify długo wczytywał; nie ustalono, czy ma to związek z połączeniem sieciowym albo Soloist. Doprecyzowanie użytkownika z 10.11.2026: pytanie o poprawne działanie na listach TIDAL i Spotify dotyczyło zakresu przygotowanej wersji 0.4.1; nie było potwierdzeniem, że zainstalowany dodatek 0.4.0 już działa poprawnie.

Dodatek NVDA 0.4.1 wskazuje czytnikowi natywny pasek stanu okna AMC Test lub AMC Public. Nie przejmuje skrótów NVDA. Odczyt na liście i w odtwarzaczu używa tego samego paska i istniejącego tekstu sesji. W innych programach korzystających z python.exe NVDA działa jak dotąd.

## Uzasadnienie i decyzje

Korekta z 10.11.2026 zastępuje założenie z A11Y-20261030, że obecność natywnego paska i poprawny tekst MSAA wystarczają do odbioru NVDA+End. W działającym AMC Test alpha.490, przy fokusie na liście Radia, drzewo dostępności zawierało pasek stanu z tekstem „Poznań, 128 kb/s, AAC, 48 kHz, LIST DO M/DZEM”. Później inny utwór został prawidłowo wpisany w ten sam pasek. Użytkownik nadal słyszał „Lista” w innych sesjach. To wskazuje na problem odnajdywania kontrolki przez czytnik, ale ręczny test nowej wersji jest potrzebny do potwierdzenia przyczyny.

Kod NVDA `api.getStatusBar()` próbuje znaleźć pasek po punkcie przy lewym dolnym brzegu okna. Moduł aplikacji może podać kontrolkę bez tego wyszukiwania. GUI AMC działa jako python.exe; nowy `appModules/python.py` rozpoznaje tylko tytuły „AMC Test — ” i „AMC Public — ”, zwraca bezpośrednie dziecko o roli STATUSBAR, a poza AMC lub bez paska oddaje sterowanie standardowej metodzie NVDA. Przegląd `C:\Program Files\NVDA\library.zip` nie wykazał wbudowanego modułu `appModules/python` w NVDA 2026.2.

Nie zmieniono tekstu statusu, kolejności danych, poleceń ani profilu możliwości AMC. Bitrate pojawia się tylko wtedy, gdy host rzeczywiście go zna; Spotify Web opisuje jakość usługi, a Soloist nie podaje pomiaru konkretnego strumienia.

## Pliki i polecenia

- `nvda-addon/addon/appModules/python.py`: wskazanie natywnego paska.
- `nvda-addon/addon/manifest.ini`, `nvda-addon/addon/doc/pl/readme.html`: wersja i opis 0.4.1.
- `nvda-addon/tests/test_status_bar_app_module.py`, `nvda-addon/tests/test_gesture_scope.py`: kontrakt obu profili, innych aplikacji i brak przejęcia gestów.
- `wxlite/tests/test_current_radio_broadcast.py`: aktualny kontekst sesji w atrapach i jawne pokrycie Podcastów.
- `AMC-NVDA-0.4.1.nvda-addon`: lokalna paczka do instalacji.

## Testy i wynik

- Python 3.14: `python -B -m unittest discover -s nvda-addon/tests -p test_*.py -v` — 15/15 zaliczonych, bez GUI.
- Python 3.14: `python -B -m pytest -q -p no:cacheprovider` dla `test_native_status_bar_for_nvda_end.py`, `test_current_radio_broadcast.py` i `test_nvda_bridge_gui.py` — początkowo 33/35; dwie stare atrapy nie miały aktywnej sesji po wcześniejszym wprowadzeniu ochrony przed obcym statusem. Po poprawieniu atrap 35/35. Po dodaniu Podcastów do testu statusu: `test_current_radio_broadcast.py` 14/14.
- `nvda-addon/build.ps1` zbudował paczkę 0.4.1, 13 003 B, SHA-256 `5E9A8B78B0792932404AEAC208715BDDD58690725BD5A5F1B0037237926BBD68`. Kontrola ZIP potwierdziła `manifest.ini` w wersji 0.4.1 oraz `appModules/python.py`.
- W zainstalowanym AMC Test otwarto widok „Nagrywane”: zero elementów. Wrócono Escape do poprzedniej listy stacji. To był odczyt stanu przed ewentualną instalacją, nie próba nagrywania ani odsłuch poprawionego dodatku.

## Znane ograniczenia

- Nowy dodatek nie jest jeszcze zainstalowany; NVDA nie został zrestartowany. Testy atrap i kontrola ZIP nie dowodzą wypowiedzi żywego czytnika.
- Przed instalacją należy ponownie sprawdzić trwające nagrania. Po instalacji sprawdzić NVDA+End na liście i w odtwarzaczu TIDAL, Spotify, Podcastów, Radia oraz plików, wraz z powrotem fokusu i ponownym odczytem.
- Opóźnienie ładowania Spotify i możliwy związek z Soloist pozostają odrębną, niepotwierdzoną obserwacją.

## Materiał do dokumentacji użytkownika

W oknie AMC naciśnij NVDA+End w układzie desktopowym albo NVDA+Shift+End w laptopowym, aby usłyszeć bieżący materiał i dostępne parametry dźwięku. Skrót działa także wtedy, gdy fokus jest na liście. Jeśli źródło nie udostępnia bitrate'u, AMC nie podaje zgadywanej wartości. Dodatek AMC nie zmienia przypisania standardowego skrótu NVDA.

Roboczy opis zmian do korekty użytkownika: poprawiono odnajdywanie paska stanu AMC przez NVDA z fokusu listy; odczyt ma być spójny między sesjami Test i Public. Nie jest to jeszcze potwierdzenie z odsłuchu na zainstalowanym dodatku.

## Wersja i commit

Gałąź `codex/amc-test-public-20261109`. Bieżąca instalacja AMC Test alpha.490 z commitu `ac606f04d5a1f12ef297b10c7dc225aa94f5c7d6`. Commit źródeł, testów i zapisu zadania: `5b29321c7eabf1f0105aee803d9151ae85f27a6c`. Pakiet dodatku 0.4.1 jest lokalnym kandydatem; brak instalacji i publikacji.


## Etap wydania 10.11.2026

Użytkownik poprosił o dwie aktualizacje na GitHubie oraz aktualizację AMC Test po 12:30, kiedy nie będzie nagrywać. Pytanie, czy chodzi o wydania Test i Public czy o dodatek NVDA i Test, pozostaje otwarte. Mimo zapowiedzi o 12:33 w widoku „Nagrywane” był jeden aktywny plik `3 - 2026-11-10 12-30`; nie zamykano AMC ani NVDA. O 12:35 interfejs podał „Zakończono zaplanowane nagrywanie 3”, a późniejszy odczyt nie wykazał aktywnej pozycji. Stan trzeba ponownie sprawdzić tuż przed instalacją.

Sam nowy instalator programu nie dostarczał poprawki 0.4.1, bo dotychczas dołączał jedynie Quick App Switcher. Dlatego w kodzie instalatora alpha.491 dodano możliwość sprawdzenia paczki AMC NVDA i zaproponowania jej przez standardową instalację NVDA po aktualizacji Test lub Public. Opcja nie uruchamia się po cichej instalacji, nie wymusza restartu czytnika i jest pokazywana tylko wtedy, gdy zainstalowana wersja dodatku jest starsza. Poprawiono porównanie równych wersji trzyczęściowych, które wcześniej traktowało brak czwartego składnika jako błąd. Zachowano odrębny etap Quick App Switcher dla Test.

Zmiany źródeł: `Directory.Build.props`, `installer/AMC_Wx_Setup.iss`, `wxlite/tools/Build-AMCInstaller.ps1` oraz test `wxlite/tests/test_installer_profiles.py`. Testy pakowania i modułu statusu: 28 zaliczonych, dwa dodatkowe podtesty. Pierwszy przebieg ujawnił stare oczekiwania dwóch nazw kontroli instalatora po zmianie z alpha.490; zaktualizowano test do aktualnych nazw i ponowiono z wynikiem pozytywnym. To jest sprawdzenie źródeł, bez nowego bundle, instalatora, instalacji lub GitHub Release.

### Materiał do dokumentacji użytkownika

Po instalacji AMC Test lub AMC Public kreator może zaproponować uruchomienie instalacji dodatku AMC do NVDA 0.4.1. Ta propozycja pojawia się tylko przy starszej wersji dodatku. W celu sprawdzenia poprawki NVDA+End trzeba dokończyć instalację dodatku i restart czytnika, a następnie odczytać status na liście oraz w odtwarzaczu. Sama aktualizacja programu bez aktualizacji dodatku nie zmienia tego skrótu.
