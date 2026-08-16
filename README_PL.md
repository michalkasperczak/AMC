# Dostępny kontroler multimedialny — prototyp dla Windows

To pierwszy demonstracyjny prototyp aplikacji sterowanej globalnym prefiksem. Sprawdza architekturę klawiatury, sesji, list, komunikatów dostępności, profili oraz importu i eksportu. Nie łączy się jeszcze z prawdziwymi kontami TIDAL, Apple Music ani WiiM.

Ten README opisuje zachowanie bieżącego prototypu. Wspólny numer wersji jest zapisany w `Directory.Build.props`, dzięki czemu rdzeń, okno i publikowany program zawsze otrzymują ten sam numer. Zatwierdzony kierunek dalszego rozwoju, docelowa architektura oraz pełna mapa skrótów znajdują się w [`MEDIA_CONTROLLER_PL.md`](MEDIA_CONTROLLER_PL.md).

## Najprostsze uruchomienie gotowej wersji

1. Otwórz folder `publish`.
2. Wybierz plik `AccessibleMediaController-<wersja>.exe` z najwyższym numerem wersji.
3. Naciśnij Enter albo kliknij go dwukrotnie.

Publikowany plik jest samowystarczalny i zawiera wymagane środowisko .NET. Użytkownik testujący gotową wersję nie musi instalować SDK ani budować projektu.

## Wymagania do zbudowania

- Windows 10 lub Windows 11;
- .NET 8 SDK zainstalowany przez osobę budującą projekt;
- PowerShell 5.1 lub nowszy;
- do testów dostępności: NVDA, JAWS albo Narrator.

Projekt jest budowany na Windows i sprawdzany automatycznymi testami rdzenia. Do zwykłego testowania służy gotowy plik EXE z folderu `publish`.

## Budowanie i uruchamianie

Poniższe polecenia są przeznaczone dla osób rozwijających projekt lub przygotowujących nową publikację.

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

Gdy okno AMC jest aktywne, `Ctrl+1–9` przełącza sesję bez globalnego prefiksu, `Ctrl+0` otwiera listę sesji, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję. Lokalne skróty widoków to `Ctrl+U` — Ulubione, `Ctrl+P` — Playlisty, `Ctrl+L` — Biblioteka, `Ctrl+Q` — Kolejka oraz `Ctrl+Shift+A` — Albumy. Skróty widoków działają również wtedy, gdy fokus znajduje się w filtrze. `Ctrl+K` przechodzi do filtra już załadowanej listy. `Ctrl+F` otwiera okno nazwane krótko „Szukaj w TIDAL” lub odpowiednio dla bieżącej usługi, a `Ctrl+Shift+F` — „Szukaj we wszystkich usługach”. W polu wyszukiwania Enter wykonuje zapytanie, a kolejny Enter otwiera wybrany wynik bez automatycznego uruchamiania jedynego dopasowania. Na wyniku działają także `Ctrl+Enter` — odtwórz teraz, `Shift+Enter` — kolejka, `Ctrl+Shift+Enter` — odtwórz jako następne, `Ctrl+Shift+U` — Ulubione i `Alt+Enter` — informacje. Te działania bezpośrednie pozostawiają okno wyników otwarte i fokus na wyniku; ich komunikat zawsze kończy się nazwą usługi. Po naciśnięciu zwykłego Enter wynik zostaje otwarty, a fokus przechodzi do głównej listy. Dostępnościowa nazwa tego jednego elementu zaczyna się chwilowo od nazwy usługi, dlatego NVDA czyta usługę i element w jednej nieprzerwanej wypowiedzi. To samo dzieje się po zamknięciu wyszukiwania globalnego Escape, jeśli wcześniej wykonano działanie bezpośrednie — również wtedy, gdy wybrana usługa była już aktywna. Po przejściu na inny element dodatkowy początek znika. Lista sama odczytuje wynik oraz jego pozycję, bez prefiksu „Wyniki wyszukiwania” i bez dodatkowego komunikatu na żywo o liczbie wyników. Escape zamyka okno. `Ctrl+Shift+K` jest zarezerwowane dla palety poleceń i obecnie podaje jednoznaczny komunikat o jej niedostępności.

Zwykły Enter na utworze lub stacji uruchamia nowo wybrany element. Ponowne naciśnięcia na tym samym elemencie przełączają kolejno pauzę i wznowienie. `Ctrl+Enter` oraz polecenie **Odtwórz teraz** zawsze zapewniają odtwarzanie wybranego elementu i nie pełnią roli pauzy. Sesja ma jeden tor odtwarzania, więc nowy utwór zastępuje poprzedni; dźwięki nie nakładają się. Tytuł głównego okna zaczyna się od aktualnie odtwarzanego elementu, następnie podaje usługę i widok. Samo otwarcie wyniku wyszukiwania bez uruchamiania odtwarzania nie zmienia tytułu bieżącego utworu. Tytuł okna wyszukiwania opisuje jego zakres, np. „Szukaj w TIDAL — AMC”. Widoki jednorodne nie powtarzają rodzaju zasobu: Albumy nie mówią „album”, a Playlisty — „playlista”. Biblioteka i Ulubione zachowują rodzaj, ponieważ mogą mieszać zasoby. Ulubione należą do bieżącej usługi lub lokalnej biblioteki, a Kolejka do aktywnej sesji odtwarzania; dlatego ich zwykłe wiersze nie powtarzają nazwy usługi. Od `alpha.26` przejście do widoku nie uruchamia osobnego komunikatu obszaru „Stan programu”. Pierwszy element otrzymuje jednorazowy prefiks widoku, np. „Albumy, Dziwne”, a pusta lista — nazwę „Ulubione, lista pusta”. Dzięki temu NVDA ma do odczytania jedno zdarzenie fokusu zamiast konkurujących komunikatów podsumowania i listy.

Od `alpha.27` ta sama zasada obejmuje zmianę sesji wykonywaną z listy przez `Ctrl+1–9` oraz `Ctrl+Page Up/Page Down`. Numer sesji i usługa są jednorazowym początkiem nazwy zaznaczonego elementu, np. „2, Apple Music, Zielony horyzont”, zamiast osobnego komunikatu „Stan programu”. Czas występujący w tej etykiecie jest czasem zaznaczonego utworu, albumu albo playlisty. Nie jest podsumowaniem łącznego czasu bieżącej listy.

Od `alpha.28` wykonane zapytania trafiają do trwałej historii: osobnej dla każdej usługi i osobnej dla wyszukiwania globalnego. Każdy zakres przechowuje do 20 unikatowych pozycji, z najnowszą na początku; ponowne użycie zapytania przenosi je na początek zamiast tworzyć duplikat. Przy pustym polu wyszukiwania strzałka w dół wybiera najnowszy wpis, kolejne naciśnięcia przechodzą do starszych, a strzałka w górę wraca do nowszych i następnie do pustego pola. Zapytanie zostaje zapisane także wtedy, gdy nie daje wyników.

Obecny katalog demonstracyjny może pokazywać wspólne wyniki testowe. Prawdziwy adapter TIDAL będzie modułem izolowanym: `Ctrl+Shift+F` może uruchomić jego zapytanie, ale treści TIDAL nie zostaną wymieszane na jednej liście z treściami podobnych usług. AMC otworzy osobny, oznaczony widok wyników TIDAL i zachowa działanie wszystkich wspólnych skrótów.

`Ctrl+Z` cofa kolejno zmiany przynależności do Ulubionych, Biblioteki i Kolejki oraz stan „Odtwórz jako następne”; przywrócony element jest ponownie zaznaczany, jeśli znajduje się w bieżącym widoku. W polu filtra `Ctrl+Z` zachowuje standardowe znaczenie cofania edycji tekstu. Polecenie jest także dostępne w menu **Edycja**. Dodatkowe angielskie „Undo” zostało przypisane funkcji **Clipboard command announcement** dodatku NVDA Global Commands Extension, a nie mechanizmowi AMC. `Ctrl+N` i `Ctrl+A` pozostają zarezerwowane dla standardowych działań Nowy oraz Zaznacz wszystko. Wpisywanie pojedynczych i kolejnych liter bez modyfikatorów na liście przechodzi do głównej nazwy pasującego elementu i nie uruchamia poleceń. Dla utworu jest nią tytuł, dla wykonawcy jego nazwa, dla albumu tytuł albumu, a dla playlisty, stacji lub urządzenia ich nazwa. Kryterium pozostaje niezależne od skonfigurowanej kolejności odczytu pól. Nowa sekwencja zaczyna się po krótkiej przerwie albo od razu wtedy, gdy dotychczasowy ciąg z następną literą nie daje dopasowania; ponawianie jednej litery przechodzi przez kolejne pasujące pozycje.

Pusta lista zatrzymuje klawisze strzałek i podaje komunikat „lista jest pusta”, zamiast przenosić fokus do przycisków lub menu. Widok „Teraz odtwarzane” ma 17 stałych elementów przeznaczonych do testowania nawigacji literami. Demonstracyjna Biblioteka celowo zawiera tylko dwa utwory należące do biblioteki.

Opcjonalna szczegółowa pomoc wyniku wyszukiwania jest celowo krótka: wymienia tylko strzałki, Enter i Escape. Pozostałe działania są dostępne w menu kontekstowym i dokumentacji, ale nie wydłużają każdego odczytu wyniku.

Po otwarciu Ustawień fokus zawsze trafia na zaznaczoną kartę „Ogólne”. Strzałki w lewo i w prawo zmieniają kategorię, a Tab przechodzi do kontrolek wybranej karty. Zarówno Zapisz, jak i Anuluj przywracają fokus do zaznaczonego elementu głównej listy; komunikat o zapisaniu jest podawany dopiero po przywróceniu listy.

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
