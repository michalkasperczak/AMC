# RADIO-REC-20261109 — status nagrań i kolizje nazw

**Status:** W trakcie. Kod i testy celowane gotowe; brak instalacji i odbioru NVDA.

## Zgłoszenie i korekta z 09.11.2026

Użytkownik zauważył w AMC Test wpisy „Przerwane”, chociaż nagrania mogły być
ukończone. Po przeniesieniu albo usunięciu pliku historia ma mówić „Brak pliku
nagrania”. „Nieudane” ma oznaczać próbę, w której rzeczywiście nic nie nagrano.
To zastępuje wcześniejsze założenie, że brakujące pliki należy po restarcie
ukryć w historii.

## Ustalenia z danych i dziennika

- W prywatnym stanie AMC Test są trzy wpisy `interrupted` z 09.11.2026,
  około 02:00, 18:27 i 18:28 czasu lokalnego. Każdy ma jeden zapisany wcześniej
  plik. Dwa z tych wcześniejszych plików nadal istnieją; jednego nie znaleziono.
- Dziennik silnika wskazuje dla wszystkich trzech tę samą przyczynę:
  plik o planowanej nazwie już istniał w folderze docelowym. Nie ma w tych
  wpisach dowodu na brak Internetu. Program nie nadpisał istniejącego pliku.
- Trzy kopie bezpieczeństwa w lokalnym folderze roboczym istnieją. Ich
  rozmiary to odpowiednio 228 977 633, 13 346 735 i 14 143 535 bajtów.
  Nie były przenoszone, usuwane ani modyfikowane. Zawartości audio nie
  przesłuchano, więc nie potwierdzono ich długości ani kompletności.

## Materiał do dokumentacji użytkownika

- Otwórz historię nagrań skrótem `Alt+Shift+R`.
- Zwykły zapisany plik pojawia się pod nazwą nagrania. Gdy plik został później
  przeniesiony lub usunięty, wpis z ostatnich 30 dni mówi „Brak pliku nagrania”.
  `Enter` wyjaśnia, że pliku nie ma już na dysku.
- Gdy nagranie zostało zachowane jako kopia bezpieczeństwa, lista mówi
  „Kopia nagrania do odzyskania”. `Enter` przy próbie bez innego gotowego pliku
  podaje lokalizację kopii. Gdy istnieje wcześniejsza zapisana część, `Enter`
  może ją odtworzyć; kopia wymaga osobnego odzyskania.
- Przy ponownej próbie zapisu pod zajętą nazwą nowe nagranie otrzyma nazwę
  z kolejnym numerem, na przykład `nagranie (2).mp3`.

## Uzasadnienie i decyzje

- Kolizja nazwy powstaje między wyborem ścieżki na początku nagrania a
  publikacją gotowego pliku na końcu. Rozwiązanie wybiera wolną nazwę przy
  publikacji i korzysta z atomowego przeniesienia bez nadpisania. Gdy inny
  proces zajmie tę nazwę w ostatniej chwili, próba powtarza wybór.
- Zmiana wyboru nowej nazwy dotyczy tylko nagrań. Pobieranie podcastu zachowuje
  dotychczasowe zasady publikacji.
- Etykieta kopii bezpieczeństwa zależy od faktycznej obecności tej kopii.
  Sama wartość `failed` lub `interrupted` w dawnym wpisie nie jest dowodem,
  że nagranie nie powstało.
- Brakujące pliki pozostają w historii przez 30 dni, z limitem 100 widocznych
  wpisów. Starsze martwe wpisy pozostają poza krótką historią.

## Zmiany w źródłach

- `src/AccessibleMediaController.Windows/Services/RadioRecordingStagingStore.cs`
- `src/AccessibleMediaController.Windows/Services/RadioMp3Recorder.cs`
- `src/AccessibleMediaController.Windows/Services/RadioOriginalStreamRecorder.cs`
- `src/AccessibleMediaController.Windows/Services/RadioRecordingControl.cs`
- `wxlite/amc_wx_lite/radio_recording.py`
- testy: `wxlite/tests/test_radio_recording.py` i
  `tests/AccessibleMediaController.Windows.SmokeTests/Program.cs`

## Testy i wynik

- 09.11.2026: `PYTHONPATH=wxlite python -m pytest
  wxlite/tests/test_radio_recording.py -q` — **47 zaliczonych**.
- 09.11.2026: `dotnet run --project
  tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj
  -c Debug -- --radio-publish-only` — **zaliczony**, także po zawężeniu
  zmiany do nagrań. Sztuczne pliki tymczasowe usunięto po teście.
- Obejmują kolizję nazwy bez nadpisania, etykietę brakującego pliku po
  restarcie oraz etykietę kopii bezpieczeństwa, również dla pierwszej części.

## Znane ograniczenia

- Nie zainstalowano nowej wersji AMC Test. Bieżąca uruchomiona aplikacja nie
  zawiera tej poprawki. Nie wykonano odbioru głosowego NVDA.
- Nie odzyskano trzech dawnych kopii bezpieczeństwa i nie przypisano ich
  automatycznie do wpisów historii. Wymagają osobnej, bezpiecznej operacji
  z weryfikacją zawartości i docelowych nazw.
- Zapis źródła i test kompilacyjny nie potwierdzają jeszcze pracy z prawdziwą
  stacją i konkretnym folderem chmurowym.

## Wersja i commit

Zmiany robocze w kanonicznym repozytorium AMC Test na gałęzi
`codex/konfigurowalne-skroty-20261026`. Brak osobnego commitu, instalatora,
instalacji i wydania dla tego zadania. Wersja uruchomionej aplikacji pozostaje
do potwierdzenia przy kolejnej aktualizacji.

## Szkic listy zmian do korekty użytkownika

- Historia nagrań rozróżnia brak pliku i zachowaną kopię bezpieczeństwa.
- Równoczesne nagrania o tej samej nazwie zapisują osobne pliki.
