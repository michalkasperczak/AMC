# Dostępny kontroler multimedialny — prototyp dla Windows

To pierwszy demonstracyjny prototyp aplikacji sterowanej globalnym prefiksem. Sprawdza architekturę klawiatury, sesji, list, komunikatów dostępności, profili oraz importu i eksportu. Nie łączy się jeszcze z prawdziwymi kontami TIDAL, Apple Music ani WiiM.

Ten README opisuje zachowanie bieżącego prototypu. Wspólny numer wersji jest zapisany w `Directory.Build.props`, dzięki czemu rdzeń, okno i publikowany program zawsze otrzymują ten sam numer. Zatwierdzony kierunek dalszego rozwoju, docelowa architektura oraz pełna mapa skrótów znajdują się w [`MEDIA_CONTROLLER_PL.md`](MEDIA_CONTROLLER_PL.md).

## Najprostsze uruchomienie — bez wpisywania poleceń

1. Rozpakuj całą paczkę do zwykłego folderu.
2. W Eksploratorze plików wybierz `ZBUDUJ_I_URUCHOM.cmd` i naciśnij Enter albo kliknij go dwukrotnie.
3. Zaczekaj na komunikat. Po udanym zbudowaniu aplikacja otworzy się automatycznie.

Plik sam sprawdza .NET 8 SDK. Jeśli go nie ma, próbuje zainstalować oficjalny pakiet Microsoft za pomocą Menedżera pakietów Windows (`winget`), a następnie przywraca składniki, kompiluje projekt, uruchamia testy i tworzy pojedynczy samowystarczalny plik `publish\AccessibleMediaController-<wersja>.exe`. Pierwsze uruchomienie może potrwać kilka minut, wymaga połączenia z Internetem i może wyświetlić systemową prośbę o zgodę na instalację.

W razie błędu skrypt automatycznie otwiera w Notatniku plik `build-log.txt` ze szczegółami. Nie trzeba wpisywać żadnych poleceń w terminalu.

## Wymagania do zbudowania

- Windows 10 lub Windows 11;
- .NET 8 SDK — plik `ZBUDUJ_I_URUCHOM.cmd` może go doinstalować automatycznie;
- PowerShell 5.1 lub nowszy;
- do testów dostępności: NVDA, JAWS albo Narrator.

Projekt jest budowany i sprawdzany automatycznymi testami rdzenia na Windows. Najprościej uruchomić go powyższym plikiem z Eksploratora.

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

Opcjonalny, bezpieczny klient smoke testu NVDA znajduje się w
[`tests/accessibility/nvda`](tests/accessibility/nvda). Nie instaluje dodatku i akceptuje wyłącznie utwardzony, lokalny profil mostu tylko do odczytu. Nie zastępuje testów ręcznych ani UI Automation.

Samowystarczalna wersja dla Windows x64, zawierająca środowisko .NET:

```powershell
.\build.ps1 -Publish
```

Wynik znajdzie się w pojedynczym pliku `publish\AccessibleMediaController-<wersja>.exe`.

## Domyślne działanie prototypu

Domyślny globalny prefiks to `Ctrl+Alt+Windows+F12`. Zastąpił wcześniejsze kombinacje kolidujące z NVDA albo systemowym skrótem Narratora. Warstwa globalna pozostaje eksperymentalna, a w `alpha.13` otrzymała uzgodnioną mapę. Po prefiksie:

- `1` — TIDAL;
- `2` — Apple Music;
- `3` — WiiM;
- `4–9` — następne przypisane sesje, jeśli istnieją;
- `0` — lista sesji;
- `Page Up` i `Page Down` — poprzednia i następna sesja;
- strzałki w lewo i w prawo — 10 sekund wstecz lub naprzód;
- strzałki w górę i w dół — głośność o 5%;
- `Ctrl+E`, `Ctrl+R`, `Ctrl+T` — odpowiednio czas upłynięty, pozostały i całkowity;
- `U` i `Shift+U` — Ulubione i zmiana stanu Ulubionych;
- `L` i `Shift+L` — Biblioteka i zmiana przynależności;
- `P` i `Shift+P` — Playlisty i zmiana przynależności;
- `Q` i `Shift+Q` — Kolejka i dodanie albo usunięcie elementu;
- `A` — Albumy; `Shift+A` pozostaje wolne;
- `K` — filtr bieżącej listy, `Shift+K` — przyszła paleta poleceń;
- `F` — wyszukiwanie w bieżącej usłudze, `Shift+F` — wyszukiwanie globalne;
- `D` — pobieranie wewnątrz usługi, `Shift+D` — eksperymentalne pobieranie na dysk.

Gdy okno AMC jest aktywne, `Ctrl+1–9` przełącza sesję bez globalnego prefiksu, `Ctrl+0` otwiera listę sesji, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję. Lokalne skróty widoków to `Ctrl+U` — Ulubione, `Ctrl+P` — Playlisty, `Ctrl+L` — Biblioteka, `Ctrl+Q` — Kolejka oraz `Ctrl+Shift+A` — Albumy. Skróty widoków działają również wtedy, gdy fokus znajduje się w filtrze. `Ctrl+K` przechodzi do filtra już załadowanej listy. `Ctrl+F` otwiera osobne okno wyszukiwania w bieżącej usłudze, a `Ctrl+Shift+F` — we wszystkich usługach. Zakres jest częścią tytułu i nazwy dostępnościowej pola. W polu wyszukiwania Enter wykonuje zapytanie, a kolejny Enter otwiera wybrany wynik bez automatycznego uruchamiania jedynego dopasowania. Na wyniku działają także `Ctrl+Enter` — odtwórz teraz, `Shift+Enter` — kolejka, `Ctrl+Shift+Enter` — odtwórz jako następne, `Ctrl+Shift+U` — Ulubione i `Alt+Enter` — informacje. Te działania bezpośrednie pozostawiają okno wyników otwarte i fokus na wyniku; zwykły Enter świadomie otwiera wynik i wraca do głównej listy. W wyszukiwaniu globalnym komunikat działania zawiera także usługę, a powrót podaje bieżącą usługę i widok. Lista sama odczytuje wynik oraz jego pozycję, bez dodatkowego komunikatu na żywo o liczbie wyników. Escape zamyka okno. `Ctrl+Shift+K` jest zarezerwowane dla palety poleceń i obecnie podaje jednoznaczny komunikat o jej niedostępności.

Zwykły Enter na utworze lub stacji uruchamia nowo wybrany element. Ponowne naciśnięcia na tym samym elemencie przełączają kolejno pauzę i wznowienie. `Ctrl+Enter` oraz polecenie **Odtwórz teraz** zawsze zapewniają odtwarzanie wybranego elementu i nie pełnią roli pauzy. Sesja ma jeden tor odtwarzania, więc nowy utwór zastępuje poprzedni; dźwięki nie nakładają się.

`Ctrl+Z` cofa kolejno zmiany przynależności do Ulubionych, Biblioteki i Kolejki oraz stan „Odtwórz jako następne”; przywrócony element jest ponownie zaznaczany, jeśli znajduje się w bieżącym widoku. W polu filtra `Ctrl+Z` zachowuje standardowe znaczenie cofania edycji tekstu. Polecenie jest także dostępne w menu **Edycja**. Dodatkowe angielskie „Undo” zostało przypisane funkcji **Clipboard command announcement** dodatku NVDA Global Commands Extension, a nie mechanizmowi AMC. `Ctrl+N` i `Ctrl+A` pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko. Wpisywanie pojedynczych i kolejnych liter bez modyfikatorów na liście przechodzi do głównej nazwy pasującego elementu i nie uruchamia poleceń. Dla utworu jest nią tytuł, dla wykonawcy jego nazwa, dla albumu tytuł albumu, a dla playlisty, stacji lub urządzenia ich nazwa. Kryterium pozostaje niezależne od skonfigurowanej kolejności odczytu pól. Nowa sekwencja zaczyna się po krótkiej przerwie albo od razu wtedy, gdy dotychczasowy ciąg z następną literą nie daje dopasowania; ponawianie jednej litery przechodzi przez kolejne pasujące pozycje.

Pusta lista zatrzymuje klawisze strzałek i podaje komunikat „lista jest pusta”, zamiast przenosić fokus do przycisków lub menu. Widok „Teraz odtwarzane” ma 17 stałych elementów przeznaczonych do testowania nawigacji literami. Demonstracyjna Biblioteka celowo zawiera tylko dwa utwory należące do biblioteki.

Prefiks, czas oczekiwania i wszystkie polecenia można zmienić w ustawieniach. Można też wybrać, czy po uruchomieniu program ma otwierać listę multimediów, czy listę sesji. Chroniony profil wbudowany jest odświeżany wraz z wersją programu; edytowalne profile użytkownika zachowują własne przypisania.

## Kolejność odczytu list

Na karcie **Listy i odczyt** można ustawić kolejność informacji odczytywanych dla elementów listy: tytułu, wykonawcy, czasu trwania i typu elementu. `Alt+Strzałka w górę/dół` przenosi zaznaczone pole, pozostawia fokus na liście i ogłasza jego nowe położenie. Podgląd aktualnej kolejności znajduje się przed przyciskami zmiany. Domyślna kolejność to tytuł, wykonawca, czas trwania, typ. Pola bez wartości są pomijane. Ustawienie obejmuje listy oraz komunikaty o bieżącym elemencie.

Polecenia **Dodaj do kolejki**, **Odtwórz jako następne** i **Ulubione** działają w prototypie jak przełączniki. Ponowne wykonanie usuwa element z odpowiedniego miejsca, a nazwa w menu kontekstowym odzwierciedla bieżący stan. Domyślne komunikaty zawierają nazwę zmienianego elementu, a własne szablony użytkownika nie są zastępowane podczas aktualizacji. Przed usunięciem fokus jest kotwiczony na kontrolce listy, a po zakończeniu układu WPF przywracany na najbliższy element; jeśli lista stała się pusta, pozostaje na pustej liście. W głównym oknie aktywny filtr jest po `Escape` czyszczony, a fokus wraca do ostatnio zaznaczonego elementu listy. Bez aktywnego filtra `Escape` również wraca z pola lub głównego przycisku do listy. W demonstracyjnych widokach wyszukiwania `Escape` wraca do poprzedniego widoku i ponownie ustawia fokus na jego liście. `Alt+F4` zamyka aktywne okno: w wyszukiwaniu wraca do okna głównego, a użyty w oknie głównym kończy aplikację. Samo wejście do wyszukiwania podaje nazwę trybu bez czasu i liczby elementów poprzedniej listy. Menu zachowuje standardowe działanie: każde naciśnięcie `Escape` wychodzi o jeden poziom.

Na karcie **Komunikaty** opcja **Pokazuj szczegółowe podpowiedzi klawiatury przy polach i listach** steruje rozbudowanymi opisami filtra oraz obu rodzajów wyszukiwania. Jest domyślnie wyłączona, dzięki czemu NVDA nie powtarza przy każdym wyniku instrukcji o Enterze i Escape.

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
- Pobieranie muzyki i obsługa DRM nie są jeszcze zaimplementowane; skróty `D` i `Shift+D` tylko podają komunikaty.
- Aktualizator nie pobiera jeszcze pakietów.
- Pierwszym celem jest Windows. macOS, VoiceOver i Siri pozostają etapem późniejszym.
- `Ctrl+Alt+Windows+F12` jest prefiksem prototypu; został pomyślnie zarejestrowany na komputerze testowym, ale kombinacje z Windows należy sprawdzać na każdym docelowym komputerze.

## Struktura

- `src/AccessibleMediaController.Core` — polecenia, profile, konfiguracja, sesje i interfejs aktualizacji;
- `src/AccessibleMediaController.Windows` — WPF, UI Automation, globalny prefiks i dostępne okna;
- `tests/AccessibleMediaController.Core.SmokeTests` — proste testy logiki bez zewnętrznych pakietów;
- `tests/accessibility/nvda` — opcjonalny, chroniony klient smoke testu NVDA;
- `MEDIA_CONTROLLER_PL.md` i `MEDIA_CONTROLLER_EN.md` — pełna specyfikacja koncepcji.
