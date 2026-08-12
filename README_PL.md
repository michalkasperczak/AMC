# Dostępny kontroler multimedialny — prototyp dla Windows

To pierwszy demonstracyjny prototyp aplikacji sterowanej globalnym prefiksem. Sprawdza architekturę klawiatury, sesji, list, komunikatów dostępności, profili oraz importu i eksportu. Nie łączy się jeszcze z prawdziwymi kontami TIDAL, Apple Music ani WiiM.

## Najprostsze uruchomienie — bez wpisywania poleceń

1. Rozpakuj całą paczkę do zwykłego folderu.
2. W Eksploratorze plików wybierz `ZBUDUJ_I_URUCHOM.cmd` i naciśnij Enter albo kliknij go dwukrotnie.
3. Zaczekaj na komunikat. Po udanym zbudowaniu aplikacja otworzy się automatycznie.

Plik sam sprawdza .NET 8 SDK. Jeśli go nie ma, próbuje zainstalować oficjalny pakiet Microsoft za pomocą Menedżera pakietów Windows (`winget`), a następnie przywraca składniki, kompiluje projekt, uruchamia testy i tworzy samowystarczalną wersję w `publish\win-x64`. Pierwsze uruchomienie może potrwać kilka minut, wymaga połączenia z Internetem i może wyświetlić systemową prośbę o zgodę na instalację.

W razie błędu skrypt automatycznie otwiera w Notatniku plik `build-log.txt` ze szczegółami. Nie trzeba wpisywać żadnych poleceń w terminalu.

## Wymagania do zbudowania

- Windows 10 lub Windows 11;
- .NET 8 SDK — plik `ZBUDUJ_I_URUCHOM.cmd` może go doinstalować automatycznie;
- PowerShell 5.1 lub nowszy;
- do testów dostępności: NVDA, JAWS albo Narrator.

Środowisko, w którym przygotowano źródła, nie zawiera zestawu .NET SDK. Projekt został sprawdzony statycznie, ale przed pierwszym użyciem trzeba go zbudować i uruchomić na Windows. Najprościej zrobić to powyższym plikiem uruchamianym z Eksploratora.

## Budowanie i uruchamianie

Poniższe polecenia są przeznaczone tylko dla osób, które wolą budowanie ręczne. Do zwykłego użycia wystarczy `ZBUDUJ_I_URUCHOM.cmd`.

W PowerShellu, w katalogu projektu:

```powershell
dotnet build AccessibleMediaController.sln
dotnet run --project src/AccessibleMediaController.Windows
```

Testy rdzenia bez dodatkowych bibliotek:

```powershell
dotnet run --project tests/AccessibleMediaController.Core.SmokeTests
```

Po każdej większej zmianie wykonaj także testy ręczne opisane w
[`TESTY_ZADANIA_PL.md`](TESTY_ZADANIA_PL.md). Ten stały plik jest aktualizowany dla bieżącej wersji i zawiera ponumerowane zadania z tytułami. Wyniki wpisuj do nowego, niepowtarzalnego pliku w folderze `wyniki-testow`; jego nazwa zawiera datę, godzinę oraz wersję programu.

Samowystarczalna wersja dla Windows x64, zawierająca środowisko .NET:

```powershell
.\build.ps1 -Publish
```

Wynik znajdzie się w `publish\win-x64`.

## Domyślne działanie prototypu

Domyślny globalny prefiks to `Ctrl+Alt+Windows+F12`. Zastąpił wcześniejsze kombinacje kolidujące z NVDA albo systemowym skrótem Narratora. Warstwa globalna pozostaje eksperymentalna i w etapie `alpha.12` rozwój koncentruje się na aktywnym oknie programu. Po prefiksie:

- `Ctrl+1` — TIDAL;
- `Ctrl+2` — Apple Music;
- `Ctrl+3` — WiiM;
- `Ctrl+0` — lista sesji;
- `Page Up` i `Page Down` — poprzednia i następna sesja;
- strzałki w lewo i w prawo — 10 sekund wstecz lub naprzód;
- strzałki w górę i w dół — głośność o 5%;
- `Ctrl+E`, `Ctrl+R`, `Ctrl+T` — odpowiednio czas upłynięty, pozostały i całkowity;
- `F` i `Shift+F` — Ulubione i zmiana stanu Ulubionych;
- `P` i `Shift+P` — Playlisty i zmiana przynależności.

Gdy okno AMC jest aktywne, `Ctrl+1–9` przełącza sesję bez globalnego prefiksu, `Ctrl+0` otwiera listę sesji, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję. Lokalne skróty widoków to: `Ctrl+P` — Playlisty, `Ctrl+L` — Biblioteka i `Ctrl+Q` — Kolejka. Działają również wtedy, gdy fokus przypadkowo albo celowo znajduje się w filtrze. `Ctrl+Z` cofa kolejno zmiany przynależności do Ulubionych, Biblioteki i Kolejki oraz stan „Odtwórz jako następne”; przywrócony element jest ponownie zaznaczany, jeśli znajduje się w bieżącym widoku. W polu filtra `Ctrl+Z` zachowuje standardowe znaczenie cofania edycji tekstu. Polecenie jest także dostępne w menu **Edycja**. Po wyczerpaniu historii cofania fokus pozostaje na liście. `Ctrl+N` i `Ctrl+A` są zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko; widoki Teraz odtwarzane i Albumy pozostają dostępne w menu.

Pusta lista zatrzymuje klawisze strzałek i podaje komunikat „lista jest pusta”, zamiast przenosić fokus do przycisków lub menu. Demonstracyjna Biblioteka zawiera po uruchomieniu dwa utwory.

Prefiks, czas oczekiwania i wszystkie polecenia można zmienić w ustawieniach. Można też wybrać, czy po uruchomieniu program ma otwierać listę multimediów, czy listę sesji. Profil wbudowany jest chroniony; próba zmiany skrótu automatycznie tworzy jego edytowalną kopię.

## Kolejność odczytu list

Na karcie **Listy i odczyt** można ustawić kolejność informacji odczytywanych dla elementów listy: tytułu, wykonawcy, czasu trwania i typu elementu. `Alt+Strzałka w górę/dół` przenosi zaznaczone pole, pozostawia fokus na liście i ogłasza jego nowe położenie. Podgląd aktualnej kolejności znajduje się przed przyciskami zmiany. Domyślna kolejność to tytuł, wykonawca, czas trwania, typ. Pola bez wartości są pomijane. Ustawienie obejmuje listy oraz komunikaty o bieżącym elemencie.

Polecenia **Dodaj do kolejki** oraz **Odtwórz jako następne** działają w prototypie jak przełączniki. Ponowne wykonanie usuwa element z odpowiedniego miejsca, a nazwa w menu kontekstowym odzwierciedla bieżący stan. Domyślne komunikaty zawierają nazwę zmienianego elementu, a własne szablony użytkownika nie są zastępowane podczas aktualizacji. Przed usunięciem fokus jest kotwiczony na kontrolce listy, a po zakończeniu układu WPF przywracany na najbliższy element; jeśli lista stała się pusta, pozostaje na pustej liście. W głównym oknie aktywny filtr jest po `Escape` czyszczony, a fokus wraca do ostatnio zaznaczonego elementu listy. Bez aktywnego filtra `Escape` również wraca z pola lub głównego przycisku do listy. Menu zachowuje standardowe działanie: każde naciśnięcie `Escape` wychodzi o jeden poziom.

## Import i eksport

Program rozróżnia trzy rodzaje plików:

- `*.amckeys.json` — pojedyncza mapa klawiszy;
- `*.amcsettings.json` — konfiguracja bez map klawiatury;
- `*.amcbackup.json` — pełna kopia: ustawienia, profile klawiatury, sesje i szablony komunikatów.

Żaden eksport nie zawiera haseł, tokenów ani danych logowania.

Robocza konfiguracja programu jest przechowywana w `%AppData%\AccessibleMediaController\state.json`.

## Aktualizacje

Projekt ma oddzielony interfejs systemu aktualizacji oraz ustawienia kanału, pobierania w tle i instalacji przy zamknięciu. Serwer aktualizacji nie jest jeszcze skonfigurowany.

Docelowy mechanizm powinien:

- publikować samowystarczalny program niewymagający ręcznego instalowania .NET;
- działać dla bieżącego użytkownika bez uprawnień administratora;
- aktualizować aplikację i adaptery usług;
- weryfikować podpis oraz SHA-256 każdego pakietu;
- instalować atomowo i umożliwiać powrót do poprzedniej wersji;
- nie zmieniać profili, konfiguracji ani danych logowania;
- nie kraść fokusu i nie przerywać odtwarzania komunikatami.

## Zakres i ograniczenia

- Wszystkie trzy usługi są obecnie sesjami demonstracyjnymi.
- Otwieranie oficjalnych aplikacji jest tylko komunikatem demonstracyjnym.
- Nie ma pobierania muzyki ani obsługi DRM.
- Aktualizator nie pobiera jeszcze pakietów.
- Pierwszym celem jest Windows. macOS, VoiceOver i Siri pozostają etapem późniejszym.
- `Ctrl+Alt+Windows+F12` jest prefiksem prototypu; został pomyślnie zarejestrowany na komputerze testowym, ale kombinacje z Windows należy sprawdzać na każdym docelowym komputerze.

## Struktura

- `src/AccessibleMediaController.Core` — polecenia, profile, konfiguracja, sesje i interfejs aktualizacji;
- `src/AccessibleMediaController.Windows` — WPF, UI Automation, globalny prefiks i dostępne okna;
- `tests/AccessibleMediaController.Core.SmokeTests` — proste testy logiki bez zewnętrznych pakietów;
- `MEDIA_CONTROLLER_PL.md` i `MEDIA_CONTROLLER_EN.md` — pełna specyfikacja koncepcji.
