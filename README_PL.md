# Dostępny kontroler multimedialny — prototyp dla Windows

To pierwszy demonstracyjny prototyp aplikacji sterowanej globalnym prefiksem. Sprawdza architekturę klawiatury, sesji, list, komunikatów dostępności, profili oraz importu i eksportu. Nie łączy się jeszcze z prawdziwymi kontami TIDAL, Apple Music ani WiiM.

Ten README opisuje zachowanie bieżącego prototypu. Wspólny numer wersji jest zapisany w `Directory.Build.props`, dzięki czemu rdzeń, okno i publikowany program zawsze otrzymują ten sam numer. Zatwierdzony kierunek dalszego rozwoju, docelowa architektura oraz pełna mapa skrótów znajdują się w [`MEDIA_CONTROLLER_PL.md`](MEDIA_CONTROLLER_PL.md).

Aktywne repozytorium robocze powinno znajdować się na zwykłym lokalnym woluminie NTFS, poza iCloud Drive, Google Drive, OneDrive i innymi katalogami synchronizowanymi. GitHub przechowuje historię kodu, natomiast atomowe kopie danych użytkownika mogą być eksportowane do chmury. Na głównym komputerze testowym stałą ścieżką projektu jest `D:\Projekty Codex\Accessible Multimedia Controller`.

## Najprostsze uruchomienie gotowej wersji

1. Otwórz folder `publish`.
2. Otwórz folder `AccessibleMediaController-<wersja>` z najwyższym numerem wersji.
3. Uruchom znajdujący się w nim plik `AccessibleMediaController-<wersja>.exe`.

Publikowany program jest samowystarczalny i zawiera wymagane środowisko .NET. Dwóch bibliotek SoundTouch znajdujących się obok EXE nie należy przenosić ani usuwać: pozostają osobnymi, wymiennymi składnikami zgodnie z ich licencją. Użytkownik testujący gotową wersję nie musi instalować SDK ani budować projektu.

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

Wynik znajdzie się w folderze `publish\AccessibleMediaController-<wersja>`. Folder zawiera EXE, dwie wymienne biblioteki SoundTouch oraz informacje licencyjne.

## Domyślne działanie prototypu

Domyślny globalny prefiks to `Ctrl+Alt+Windows+F12`. Zastąpił wcześniejsze kombinacje kolidujące z NVDA albo systemowym skrótem Narratora. Warstwa globalna pozostaje eksperymentalna, a w `alpha.13` otrzymała uzgodnioną mapę. Po prefiksie:

- `1` — Pliki lokalne, gdy sesja jest dostępna;
- `2` — WiiM;
- `3` — TIDAL;
- `4` — Apple Music;
- `5–9` — następne przypisane sesje, jeśli istnieją;
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
- `K` — filtr bieżącej listy, `Shift+K` — paleta poleceń;
- `F` — wyszukiwanie w bieżącej usłudze, `Shift+F` — wyszukiwanie globalne;
- `D` — pobieranie wewnątrz usługi, `Shift+D` — eksperymentalne pobieranie na dysk.

Gdy okno AMC jest aktywne, `Ctrl+1–9` przełącza sesję bez globalnego prefiksu, `Ctrl+0` otwiera listę sesji, a `Ctrl+Page Up` i `Ctrl+Page Down` wybierają poprzednią lub następną sesję. Lokalne skróty widoków to `Ctrl+U` — Ulubione, `Ctrl+P` — Playlisty, `Ctrl+L` — Biblioteka, `Ctrl+Q` — Kolejka oraz `Ctrl+Shift+A` — Albumy. Wszystkie strzałki na zwykłej liście zachowują działanie właściwe dla listy. Enter na utworze lub stacji uruchamia element i przechodzi do odtwarzacza; `Ctrl+Enter` przełącza odtwarzanie zaznaczenia bez opuszczania listy. `F6`, polecenie „Teraz odtwarzane” albo `N` po prefiksie otwiera odtwarzacz bez zmiany zaznaczenia.

W odtwarzaczu lewo/prawo przewija o 10 sekund, Shift+lewo/prawo o 30 sekund, Ctrl+lewo/prawo o minutę, góra/dół zmienia głośność o 5%, a Shift+góra/dół o 1%. `Shift+,` zmniejsza prędkość, `Shift+.` ją zwiększa, a `Ctrl+.` przywraca 1,00×. Dostępne wartości to 0,50–2,00× co 0,25; tempo zmienia się bez zmiany wysokości dźwięku. Home przechodzi na początek, End — 10 sekund przed końcem, a cyfry `0–9` przechodzą do `0–90%` czasu trwania co 10%. `Ctrl+J` otwiera „Skocz do czasu”: sama liczba oznacza minuty, `minuty:sekundy` podaje dokładniejszą pozycję, a trzy części oznaczają `godziny:minuty:sekundy`. `Ctrl+Shift+J` otwiera osobne „Skocz do procentu” i przyjmuje `0–100`. Oba skróty oraz cyfry działają wyłącznie w odtwarzaczu. `Ctrl+Shift+E/R/T` podaje czas, a Escape wraca dokładnie do wcześniejszej listy i pozycji. Odtwarzany element ma na liście początek „Odtwarzany”, a zatrzymany w połowie — „Wstrzymany”. Skróty widoków działają również wtedy, gdy fokus znajduje się w filtrze. `Ctrl+K` przechodzi do filtra już załadowanej listy. `Ctrl+F` otwiera okno nazwane krótko „Szukaj w TIDAL” lub odpowiednio dla bieżącej usługi, a `Ctrl+Shift+F` — „Szukaj we wszystkich usługach”. W polu wyszukiwania Enter wykonuje zapytanie, a kolejny Enter otwiera wybrany wynik bez automatycznego uruchamiania jedynego dopasowania. Na wyniku działają także `Ctrl+Enter` — odtwórz lub wstrzymaj zaznaczenie, `Shift+Enter` — kolejka, `Ctrl+Shift+Enter` — odtwórz jako następne, `Ctrl+Shift+U` — Ulubione i `Alt+Enter` — informacje. Te działania bezpośrednie pozostawiają okno wyników otwarte i fokus na wyniku; ich komunikat zawsze kończy się nazwą usługi. Po naciśnięciu zwykłego Enter wynik zostaje otwarty, a fokus przechodzi do głównej listy. Dostępnościowa nazwa tego jednego elementu zaczyna się chwilowo od nazwy usługi, dlatego NVDA czyta usługę i element w jednej nieprzerwanej wypowiedzi. To samo dzieje się po zamknięciu wyszukiwania globalnego Escape, jeśli wcześniej wykonano działanie bezpośrednie — również wtedy, gdy wybrana usługa była już aktywna. Po przejściu na inny element dodatkowy początek znika. Lista sama odczytuje wynik oraz jego pozycję, bez prefiksu „Wyniki wyszukiwania” i bez dodatkowego komunikatu na żywo o liczbie wyników. Escape zamyka okno. `Ctrl+Shift+K` otwiera dostępną paletę wszystkich poleceń AMC. Wpisywanie od razu filtruje listę, strzałka w dół przechodzi do wyników, Enter wykonuje zaznaczone polecenie, a Escape zamyka paletę. Nazwy można wpisywać bez polskich znaków, a wiersz podaje skrót działający w oknie oraz aktywny skrót działający po prefiksie.

`Ctrl+O` albo menu **Plik → Otwórz pliki audio** otwiera jeden lub wiele lokalnych plików. `Ctrl+Shift+O` albo **Plik → Otwórz folder z plikami audio** rejestruje trwałe źródło, wczytuje rozpoznane pliki również z podfolderów i otwiera standardowy widok **Foldery**. Enter wchodzi do zaznaczonego folderu albo otwiera plik, Backspace wraca o poziom wyżej, wpisywanie liter działa w bieżącym poziomie, `Ctrl+K` go filtruje, a `Ctrl+F` nadal przeszukuje całą lokalną sesję. Płaska **Biblioteka** pozostaje dostępna równolegle. Żadne z tych poleceń nie uruchamia dźwięku automatycznie, a ponowne wczytanie tej samej ścieżki nie tworzy duplikatu.

Od `alpha.77` kolejność sesji można zmieniać w **Ustawienia → Ogólne** przyciskami albo `Alt+strzałka w górę/w dół`. Pozycja określa jednocześnie `Ctrl+1–9`, listę sesji oraz kolejność `Ctrl+Page Up/Page Down`. Domyślnie jest to: Pliki lokalne, WiiM, TIDAL, Apple Music. Dawna karta **Listy i odczyt** została połączona z kartą **Komunikaty** jako sekcja **Odczytywanie elementów list**; wszystkie ustawienia i wejścia z palety poleceń pozostały dostępne.

Od `alpha.78` sesja **Pliki lokalne** istnieje od uruchomienia także wtedy, gdy biblioteka jest pusta, dlatego `Ctrl+1` nigdy nie prowadzi już do „nieprzypisanej” sesji. Po wybraniu katalogu przez `Ctrl+Shift+O` AMC natychmiast przechodzi do lokalnego widoku Foldery i pozostaje w nim podczas skanowania, zamiast pokazywać demonstracyjną listę innej usługi.

Od `alpha.79` zarejestrowany folder jest jednoznacznie **źródłem Biblioteki**. `Ctrl+Shift+O` włącza do płaskiej Biblioteki wszystkie rozpoznane pliki z wybranego folderu i podfolderów, także rekordy wcześniej z niej usunięte; widok **Foldery** jest tylko hierarchicznym sposobem oglądania tych samych rekordów. `Delete` na pliku w Folderach usuwa jego przynależność do Biblioteki, lecz pozostawia plik na dysku i w widoku jego rzeczywistego folderu. `Delete` na wierszu folderu niczego nie usuwa, a fizyczne przeniesienie pliku do Kosza nadal wymaga `Shift+Delete` i potwierdzenia.

Od `alpha.80` źródła są synchronizowane przy starcie, po zmianach zauważonych w systemie plików oraz ręcznie przez `F5`. Nowe pliki są dopisywane, brakujące stają się niedostępne bez utraty historii, zakładek i pozycji, a plik przywrócony pod tą samą ścieżką wraca do aktywnej Biblioteki. `Delete` tworzy trwałe wykluczenie, dlatego zwykłe ponowne skanowanie ani restart nie dodają pliku z powrotem; natychmiastowe `Ctrl+Z` cofa wykluczenie. `Alt+1` otwiera **Foldery Biblioteki**, `Alt+2` — płaskie **Wszystkie pliki**, a `Ctrl+L` wraca do ostatnio używanego układu Biblioteki. `Shift+cyfry` pozostaje dostępne dla nawigacji po nazwach zaczynających się od znaków specjalnych.

Od `alpha.81` skaner rozróżnia prawdziwe dowiązania katalogów od plików i folderów-placeholderów Cloud Files. Dzięki temu źródła iCloud są indeksowane bez otwierania i pobierania treści każdego nagrania, a dowiązania oraz junctiony nadal nie mogą utworzyć pętli. Jednorazowa migracja naprawia źródło, którego wszystkie starsze rekordy zostały omyłkowo wykluczone przez alfę 80. Przy przejściu z `Alt+2` do `Alt+1` AMC otwiera teraz rzeczywisty folder zaznaczonego pliku i zachowuje jego zaznaczenie; dla pliku dodanego pojedynczo, poza źródłami, wybierany jest główny poziom Folderów.

Od `alpha.82` awaryjne rozpoznawanie znaczników Cloud Files obejmuje także OneDrive, gdy dostawca odmawia odczytu celu punktu ponownej analizy. Google Drive w trybie lustrzanym działa jak zwykły folder, a w trybie strumieniowanym jak źródło na wirtualnym dysku: AMC indeksuje nazwy bez otwierania treści, nie wymusza masowego pobierania i zachowuje rekordy, gdy aplikacja Google Drive albo jej dysk są chwilowo niedostępne. Obserwator zmian jest dodatkiem; na systemie plików, który go nie obsługuje, pozostają skan przy starcie i `F5`.

Od `alpha.83` polecenie **Plik → Zarządzaj źródłami Biblioteki** oraz paleta poleceń otwierają dostępny menedżer źródeł. Lista podaje stan folderu, liczbę aktywnych, niedostępnych i wykluczonych plików oraz pełną ścieżkę. Można dodać folder, odświeżyć jedno albo wszystkie źródła, bezpiecznie odłączyć źródło i wyeksportować pełną kopię AMC. Odłączenie wyłącza tylko dalszą automatyczną synchronizację: nie usuwa plików z dysku ani rekordów, Ulubionych, kolejki, historii, zakładek i pozycji. Nowe źródło nie może powtarzać ani obejmować innego źródła i nie może być jego podfolderem. Pełna kopia `.amcbackup.json` obejmuje teraz jawnie opisany katalog Biblioteki, źródła, wykluczenia i wszystkie wymienione dane użytkownika; nadal nie zawiera haseł ani tokenów.

Zwykły Enter wykonuje działanie podstawowe: na utworze lub stacji zapewnia odtwarzanie zaznaczenia i otwiera odtwarzacz, a na albumie, playliście lub wykonawcy otwiera zawartość. Jeżeli bieżący element już gra, Enter tylko pokazuje odtwarzacz i go nie wstrzymuje. `Ctrl+Enter` wykonuje polecenie **Odtwórz lub wstrzymaj** bez otwierania elementu; nowy element zaczyna grać, bieżący zostaje wstrzymany, a wstrzymany — wznowiony. W wynikach wyszukiwania okno pozostaje otwarte. `Spacja` steruje wyłącznie tym, co faktycznie jest odtwarzane, niezależnie od bieżącego zaznaczenia. Sesja ma jeden tor odtwarzania, więc nowy utwór zastępuje poprzedni; dźwięki nie nakładają się. Tytuł głównego okna zaczyna się od aktualnie odtwarzanego lub wstrzymanego elementu, potem podaje moduł, sesję oraz pełny numer wersji AMC, np. „Audycja — Kolejka — Pliki lokalne — AMC…”. Na domyślnej liście zbędny moduł „Multimedia” jest pomijany. Nie jest to nazwa elementu tylko zaznaczonego kursorem. Samo otwarcie wyniku wyszukiwania bez uruchamiania odtwarzania nie zmienia tytułu bieżącego utworu. Tytuł okna wyszukiwania opisuje jego zakres, np. „Szukaj w TIDAL — AMC”. Widoki jednorodne nie powtarzają rodzaju zasobu: Albumy nie mówią „album”, a Playlisty — „playlista”. Biblioteka i Ulubione zachowują rodzaj, ponieważ mogą mieszać zasoby. Ulubione należą do bieżącej usługi lub lokalnej biblioteki, a Kolejka do aktywnej sesji odtwarzania; dlatego ich zwykłe wiersze nie powtarzają nazwy usługi. Od `alpha.26` przejście do widoku nie uruchamia osobnego komunikatu obszaru „Stan programu”. Pierwszy element otrzymuje jednorazowy prefiks widoku, np. „Albumy, Dziwne”, a pusta lista — nazwę „Ulubione, lista pusta”. Dzięki temu NVDA ma do odczytania jedno zdarzenie fokusu zamiast konkurujących komunikatów podsumowania i listy.

Od `alpha.27` ta sama zasada obejmuje zmianę sesji wykonywaną z listy przez `Ctrl+1–9` oraz `Ctrl+Page Up/Page Down`. Numer sesji i usługa są jednorazowym początkiem nazwy zaznaczonego elementu, np. „2, Apple Music, Zielony horyzont”, zamiast osobnego komunikatu „Stan programu”. Czas występujący w tej etykiecie jest czasem zaznaczonego utworu, albumu albo playlisty. Nie jest podsumowaniem łącznego czasu bieżącej listy.

Od `alpha.28` wykonane zapytania trafiają do trwałej historii: osobnej dla każdej usługi i osobnej dla wyszukiwania globalnego. Każdy zakres przechowuje do 20 unikatowych pozycji, z najnowszą na początku; ponowne użycie zapytania przenosi je na początek zamiast tworzyć duplikat. Przy pustym polu wyszukiwania strzałka w dół wybiera najnowszy wpis, kolejne naciśnięcia przechodzą do starszych, a strzałka w górę wraca do nowszych i następnie do pustego pola. Zapytanie zostaje zapisane także wtedy, gdy nie daje wyników.

Od `alpha.29` paleta poleceń pod `Ctrl+Shift+K` jest działającym interfejsem, a nie tylko zarezerwowanym skrótem. Obejmuje również polecenia bez własnego skrótu, filtruje po wielu fragmentach nazwy i skrótu oraz ignoruje wielkość liter i polskie znaki diakrytyczne.

Od `alpha.30` polecenie „Odtwórz lub wstrzymaj” ma tę samą przełączającą logikę pod `Ctrl+Enter`, na przycisku i w menu kontekstowym. `Spacja` niezależnie steruje aktualnym odtwarzaniem. Paleta pokazuje zarówno skróty okna, jak i skróty po prefiksie, a błędne dopisanie znaków nie pozostawia użytkownika w pustej liście bez możliwości szybkiego rozpoczęcia nowego wyszukiwania.

Od `alpha.31` paleta obejmuje wszystkie działające miejsca i operacje okna Ustawienia. Wpisy dotyczące profilu, prefiksu, kolejności list, importu, eksportu, szablonów i planowanych aktualizacji tylko otwierają właściwą kartę oraz ustawiają fokus na odpowiedniej kontrolce. Bezpośrednio przełączać można wyłącznie dwie bezpieczne opcje: komunikaty dostępności i szczegółowe podpowiedzi klawiatury. Ich nazwy zawsze podają aktualny stan oraz działanie Entera, a krótki komunikat potwierdza zmianę nawet wtedy, gdy właśnie wyłączono zwykłe komunikaty.

Od `alpha.32` lista zdarzeń na karcie **Komunikaty** podaje wyłącznie zrozumiałe nazwy, np. „Zmiana sesji”. Techniczne znaczniki, takie jak `{slot}` i `{service}`, pozostają dostępne dopiero w osobnym polu edycji szablonu, gdzie są potrzebne do jego modyfikowania.

Od `alpha.33` lokalna sesja korzysta z rzeczywistego wyjścia dźwięku Windows. Enter i `Ctrl+Enter` uruchamiają lub wstrzymują wybrany plik, a Spacja, przewijanie, głośność i polecenia czasu sterują tym samym torem. `Alpha.34` wprowadziła lokalne przypisania czasu, lecz test NVDA wykazał, że nie docierały niezawodnie do aplikacji. W `alpha.35` `Ctrl+E`, `Ctrl+R` i `Ctrl+T` zostały przechwycone na granicy komunikatów okna. W `alpha.36` eksperymentalny transport strzałkami przeniesiono z głównej listy do odtwarzacza, aby wszystkie strzałki na zwykłych listach zachowały naturalne działanie. Implementacja używa kodeków dostępnych w Windows i wyjścia współdzielonego, nie instaluje globalnych pakietów kodeków i nie wycisza czytnika ekranu. Lista lokalna jest na razie przechowywana tylko do zamknięcia programu.

W `alpha.37` Escape z odtwarzacza nadal wraca do miejsca ostatnio przeglądanego, nie do utworu bieżącego. Stan bieżącego utworu jest widoczny i dostępny bez zmieniania zaznaczenia. Skróty czasu w aktywnym oknie przeniesiono na `Ctrl+Shift+E/R/T`; przypisania `Ctrl+E/R/T` po globalnym prefiksie pozostają bez zmian.

W `alpha.38` karta **Komunikaty** zawiera opcję **Oznajmiaj pozycję po przewijaniu**. Jej wyłączenie ucisza automatyczny odczyt czasu po strzałkach oraz Home i End, ale nie wyłącza celowych pytań `Ctrl+Shift+E/R/T`. `Ctrl+Shift+G` przełącza tę opcję w każdej chwili, a paleta poleceń pokazuje jej aktualny stan i skutek Entera. Ustawienie jest zapisywane po zamknięciu programu.

W `alpha.39` cyfry `0–9` w odtwarzaczu przechodzą odpowiednio do `0%, 10%, …, 90%` długości utworu. Działają także klawisze bloku numerycznego przy włączonym Num Lock. Na zwykłej liście cyfry zachowują nawigację po elementach, a `Ctrl+cyfra` nadal wybiera sesję. Skoki procentowe respektują ustawienie odczytu pozycji; przy nieznanym czasie trwania program zgłasza niedostępność zamiast zgadywać.

W `alpha.40` skok cyfrą domyślnie oznajmia tylko procent, np. „50%”; dokładny czas pozostaje dostępny przez `Ctrl+Shift+E`. W Ustawieniach na karcie **Komunikaty** można wybrać: **Tylko procent**, **Tylko czas** albo **Procent i czas**. Dotychczasowy przełącznik i `Ctrl+Shift+G` obejmują teraz automatyczne komunikaty czasu oraz głośności. Ich wyłączenie nie ucisza odtwarzania, pauzy, błędów ani jawnych poleceń czasu.

W `alpha.41` `Ctrl+Shift+G` jest nadrzędnym, odwracalnym wyciszeniem automatycznych komunikatów odtwarzacza. Nie niszczy indywidualnego wyboru czterech kategorii: skoków cyframi, przewijania strzałkami, głośności oraz odtwarzania/pauzy. Można więc pozostawić same procenty, a wyłączyć pozostałe trzy grupy. Błędy i jawne pytania o czas pozostają słyszalne. Prawdziwy pasek stanu na dole okna udostępnia przez `NVDA+End` usługę, stan, tytuł, pozycję i czas całkowity, głośność oraz przepływność. Dla lokalnego pliku przepływność jest przybliżeniem z rozmiaru i czasu; dla źródła bez danych pojawia się „brak danych”. Pasek aktualizuje się bez automatycznego przerywania mowy.

W `alpha.42` pasek został przeniesiony na rzeczywistą dolną krawędź okna, ponieważ `NVDA+End` lokalizuje pasek stanu właśnie w tym miejscu. Dynamiczny tekst znajduje się w bezpośrednim elemencie paska UI Automation. Wersja dodaje również `Ctrl+G` — „Skocz do czasu” — oraz osobne „Skocz do procentu”. Oba polecenia są dostępne z menu Odtwarzanie, przycisków odtwarzacza i palety poleceń.

W `alpha.43` pasek WPF, którego NVDA nadal nie odnajdywał w ręcznym teście, został zastąpiony natywną kontrolką paska stanu Windows. Dokładny skok do czasu przez `Ctrl+G` oraz skok do procentu z menu lub palety działają w całym głównym oknie i dotyczą aktualnie odtwarzanego elementu. Cyfry `0–9` pozostają wyłącznie skrótem odtwarzacza, aby na zwykłej liście nie przejmować nawigacji po numerach. Błędna wartość czasu lub procentu jest teraz zgłaszana aktywnym zdarzeniem dostępności, a fokus pozostaje w zaznaczonym polu.

W `alpha.44` dodano punkt zgodności z algorytmem `NVDA+End`. NVDA nie przeszukuje całego drzewa kontrolek, lecz sprawdza obiekt w skrajnym lewym dolnym punkcie okna. WPF pozostawiał tam ramkę zamiast paska. Warstwa Windows utrzymuje teraz w tym miejscu prawie niewidoczny natywny obiekt klasy `msctls_statusbar32`, aktualizowany tą samą treścią co widoczny pasek. Punkt nie przyjmuje fokusu, nie trafia do Tab ani Alt+Tab i podąża za przesunięciem, zmianą rozmiaru oraz maksymalizacją okna.

W `alpha.45` `Ctrl+J` oznacza „Skocz do czasu”, a `Ctrl+Shift+J` — „Skocz do procentu”. Oba skróty i cyfry `0–9` działają wyłącznie w odtwarzaczu. `F6` otwiera wspólny widok odtwarzacza w każdej sesji: lokalnej, streamingowej, radiowej i urządzenia. Źródło bez znanej długości nadal może korzystać z odtwarzania i innych dostępnych funkcji, lecz nie z przewijania do czasu lub procentu.

W `alpha.46` wycofano niemal niewidoczny pomocniczy pasek z `alpha.44–45`. Ręczny test wykazał, że jako osobne okno potrafił przejąć kontekst NVDA przy uruchomieniu, zablokować odczyt i skróty, a zniknąć dopiero po `Alt+F4`. W bieżącej wersji nie istnieje drugie okno paska ani obiekt mogący otrzymać fokus. Główne okno udostępnia automatyzacji granice własnego obszaru klienta, aby próba `NVDA+End` trafiała do rzeczywistego paska osadzonego na dole. Niezależnie od wyniku tego eksperymentu stan można zawsze bezpiecznie odczytać poleceniem **Odtwarzanie → Odczytaj stan odtwarzania** lub z palety poleceń; nie zmienia ono fokusu.

W `alpha.47` pasek udostępnia pełny komunikat tylko na swojej wewnętrznej etykiecie. Sam kontener nie powtarza już tej samej nazwy, dlatego `NVDA+End` powinien odczytać treść jeden raz. Kolejność została skrócona pod kątem szybkiego odsłuchu: przepływność, stan, pozycja i czas całkowity, głośność, tytuł, usługa.

W `alpha.48` pasek nie podaje głośności; jego kolejność to przepływność, stan, pozycja i czas całkowity, tytuł oraz usługa. Głośność pozostaje w celowo wywołanym poleceniu „Odczytaj stan odtwarzania”. Pomocnicze komunikaty wysyłają jedno zdarzenie UI Automation z właściwą treścią. Nie wysyłają równolegle zdarzenia regionu aktywnego, które w niektórych przejściach powodowało sporadyczny odczyt technicznej nazwy „Stan programu”.

W `alpha.49` lokalne odtwarzanie korzysta z NAudio, współdzielonego WASAPI i SoundTouch. `Shift+,` oraz `Shift+.` zmieniają tempo bez zmiany wysokości dźwięku, a `Ctrl+.` przywraca normalną prędkość. Wartość należy do lokalnej sesji i pozostaje aktywna przy zmianie utworu. Usługi demonstracyjne bez własnego toru audio jednoznacznie zgłaszają brak obsługi. Wyjście pozostaje współdzielone, aby nie odbierać dźwięku NVDA. SoundTouch jest publikowany jako dwie wymienne biblioteki obok EXE; informacje oraz pełne teksty licencji znajdują się w tym samym folderze.

W `alpha.50` pasek stanu rozpoczyna się bezpośrednio od parametrów, np. „około 192 kb/s, 48 kHz”, bez zbędnego słowa „przepływność”. `Ctrl+I` otwiera informacje o elemencie, a `Ctrl+Shift+I` odczytuje pełny stan odtwarzania. Po prefiksie odpowiadają im `I` oraz `Shift+I`. Rozszerzone informacje techniczne pozostają w menu i palecie bez stałego skrótu.

W `alpha.51` bieżący pasek nie mówi „około” i pomija parametry, których źródło nie podało. Stare polecenia `Ctrl+I`, `Ctrl+Shift+I`, prefiksowe `I` i `Shift+I` zostały usunięte. Jedno `Alt+Enter` otwiera dostępne okno **Właściwości i informacje**; jego tekst można zaznaczać i kopiować. Każda sesja pamięta osobno widok, filtr, zaznaczenie i aktywny odtwarzacz. Filtr i wyszukiwanie nie otwierają się na powierzchni odtwarzacza, natomiast skróty Ulubionych, Biblioteki, kolejki i playlist działają na faktycznie odtwarzanym elemencie. Escape wraca do listy, z której ostatnio otwarto odtwarzacz przez F6. Plan importu i eksportu radia obejmuje M3U/M3U8, PLS, XSPF oraz format Ulubionych VRadio; prywatne pliki źródłowe nie trafiają do repozytorium.

W `alpha.52` widok Kolejki jednoznacznie traktuje zarówno zwykły wpis kolejki, jak i „Odtwórz jako następne”: ponowne `Shift+Enter`, usunięcie albo odpowiednie menu usuwa oba znaczniki zamiast przypadkowo dodawać element ponownie. `Shift+strzałka w górę/dół` rozszerza zaznaczenie listy, a działania Ulubionych, Biblioteki, Kolejki, „Odtwórz jako następne”, playlist i Delete obejmują cały wybór; jedno `Ctrl+Z` cofa całą operację zbiorową. Po naturalnym końcu pliku odtwarzanie wybiera najpierw „Odtwórz jako następne”, następnie Kolejkę, a potem kolejny element załadowanej listy, bez zapętlania ostatniego. Głośność jest regulowana programowo wyłącznie w strumieniu AMC i nie powinna zmieniać głośności NVDA ani systemu. Po zmianie sesji komunikat fokusu zawiera także przywrócony widok, np. „1, TIDAL, Biblioteka…”.

W `alpha.53` okno `Alt+Enter` używa zwykłej dostępnej listy zamiast pola tylko do odczytu. Strzałki przechodzą po osobnych wierszach, pierwszy wiersz zaczyna się bezpośrednio od „Tytuł”, `Ctrl+C` kopiuje zaznaczone wiersze, `Ctrl+A` zaznacza wszystkie, a przycisk nadal kopiuje całość z podziałem na sekcje. `Ctrl+Shift+C` kopiuje pełną ścieżkę pliku lokalnego; w adapterze usługi kopiuje wyłącznie kanoniczne publiczne łącze, nigdy prywatny lub tymczasowo podpisany adres odtwarzania. `Alt+strzałka w lewo/prawo` działa w całym głównym widoku, nie tylko przy fokusie na liście, i oznajmia kierunek oraz docelowy widok. Historia pozostaje na razie funkcją eksperymentalną do ponownego testu.

W `alpha.54` okno `Alt+Enter` jest natywnym tekstem Windows tylko do odczytu. Zwykłe strzałki przesuwają kursor po znakach i wierszach, `Ctrl+strzałki` po słowach, a warianty z `Shift` zaznaczają dowolny fragment; działają standardowe `Ctrl+A` i `Ctrl+C`. „Kopiuj wszystko” oznajmia powodzenie i pozostawia dialog otwarty, a Escape lub przycisk „Zamknij” kończy pracę. Pełna lokalna ścieżka znajduje się bezpośrednio po nazwie usługi. Menu kontekstowe listy i odtwarzacza udostępniają te same adekwatne działania na elemencie: kolejkę, „Odtwórz jako następne”, Ulubione, Bibliotekę, playlisty, informacje oraz kopiowanie nazwy i ścieżki lub publicznego łącza. Nazwy działań przełączanych pokazują bieżący stan.

W `alpha.55` właściwości mają trzy stałe części: identyfikację elementu i źródła, stan **W aplikacji**, a następnie dane **Techniczne**. Czas został przeniesiony do danych technicznych obok formatu, rozmiaru, bitrate i częstotliwości. W menu kontekstowych nazwa dostępnościowa każdej pozycji zawiera skrót, dlatego NVDA powinien podawać go zarówno podczas poruszania się po menu, jak i przy ponownym odczycie aktualnego fokusu, bez zdublowania. Historia `Alt+lewo/prawo` jest świadomie prowadzona osobno w każdej sesji i w komunikacie podaje nazwę bieżącej usługi; nie służy do zmiany sesji.

W `alpha.56` komunikat historii ma kolejność: kierunek, docelowy widok, sesja — na przykład „Wstecz, Kolejka, Lokalne multimedia”. W Ustawieniach, na karcie Komunikaty, można wyłączyć samo oznajmianie kierunku historii bez wyłączania `Alt+lewo/prawo`; docelowy widok i sesja nadal poprzedzają element, np. „Kolejka, Lokalne multimedia, Audycja”. Opcja jest również dostępna z palety poleceń. Nadrzędne wyłączenie wszystkich komunikatów dostępności usuwa także ten kontekst. Ustawienie komunikatów odtwarzacza pozostaje niezależne, ponieważ dotyczy czasu, przewijania, głośności i odtwarzania, a nie nawigacji po widokach. Pełny odczyt obiektu odtwarzacza przez NVDA nadal zawiera tytuł, rodzaj, usługę, stan, prędkość, przycisk oraz krótką instrukcję — jest to zamierzony przegląd powierzchni odtwarzacza.

W `alpha.57` lokalna biblioteka staje się trwała. AMC zapisuje w `%AppData%\AccessibleMediaController\state.json` listę plików, ich stany Ulubionych, Biblioteki i Kolejki, bieżący plik, głośność, prędkość oraz osobną pozycję wznowienia każdego pliku. Pozycja jest utrwalana co najwyżej raz na 15 sekund i przy prawidłowym zamknięciu; wznowienie nie uruchamia dźwięku samoczynnie. Rozmiar i data modyfikacji chronią przed zastosowaniem starej pozycji do podmienionego pliku. Pakiet programu pozostaje na razie przenośny, ale aktualizacja lub podmiana jego folderu nie usuwa danych użytkownika z AppData. Pierwsze uruchomienie `alpha.57` wymaga jednorazowego ponownego otwarcia plików załadowanych w starszej wersji, która ich jeszcze nie zapisywała.

W `alpha.58` AMC działa jako jedna instancja: ponowne uruchomienie przywołuje aktualnie otwarte okno albo najgłębszy dialog zamiast tworzyć drugi proces zapisujący ten sam stan. Okna wyszukiwania, ustawień, właściwości i palety pozostają modalnymi dialogami bez osobnych przycisków na pasku zadań, ponieważ zapewniają przewidywalną granicę fokusu NVDA; odtwarzacz jest widokiem głównego okna, a nie kolejnym oknem. Zasobnik systemowy jest planowany wyłącznie jako opcja domyślnie wyłączona: minimalizacja może wtedy ukrywać okno, lecz `Alt+F4` nadal zakończy aplikację. Tytuł okna zaczyna się od aktualnego elementu, a następnie podaje moduł i sesję. `Page Up` oraz `Page Down` w odtwarzaczu uruchamiają poprzedni lub następny element bez zapętlania krańców listy. OGG/Vorbis korzysta z dekodera NAudio.Vorbis/NVorbis, a lokalna pozycja jest dodatkowo zapisywana przy pauzie, zmianie utworu, wpisanym skoku i utracie aktywności okna.

W `alpha.59` sesja nazywa się zwięźle **Pliki lokalne**, a ogólny moduł „Multimedia” jest pomijany w tytule i dodatkowym kontekście, gdy nie wnosi informacji. Na głównej lokalnej liście `Strzałka w lewo` podaje krótki zestaw już zapisanych danych technicznych bez otwierania okna, a `Strzałka w prawo` otwiera menu działań zawierające także „Otwórz w domyślnej aplikacji” i systemowe „Otwórz w…”. `Ctrl+Shift+C` umieszcza w schowku jednocześnie tekst pełnej ścieżki oraz standardową listę plików Windows; dzięki temu ten sam skrót wkleja ścieżkę do edytora albo fizyczny plik do Eksploratora i Total Commandera. Zaznaczenie wielu plików tworzy wieloelementową listę. `Delete` na głównej lokalnej liście usuwa wpisy wyłącznie z AMC, nigdy z dysku, a `Ctrl+Z` je przywraca. Usunięcie całej zawartości bezpiecznie odłącza pustą sesję; dodanie nowych plików albo cofnięcie tworzy ją ponownie.

W `alpha.60` lewa i prawa strzałka udostępniają szybkie informacje oraz menu działań we wszystkich widokach **Plików lokalnych**, nie tylko w domyślnym katalogu. `Ctrl+Shift+E/R/T` działa zarówno na liście, jak i w odtwarzaczu. Każda sesja ma trwałą Historię odtwarzania dostępną przez `Ctrl+H`; lista jest uporządkowana od najnowszego wpisu, nie zawiera duplikatów i oznacza ostatni lub wstrzymany element bez samoczynnej zmiany fokusu po starcie. W odtwarzaczu `Alt+strzałka w dół` wybiera starszy odtwarzany element, a `Alt+strzałka w górę` nowszy, przywracając zapamiętaną pozycję. `Page Up/Down` nadal przechodzi po liście źródłowej, zaś `Alt+lewo/prawo` pozostaje odrębną, nietrwałą historią widoków. `Shift+Delete` po jednoznacznym potwierdzeniu przenosi zaznaczone pliki lokalne do systemowego Kosza i usuwa je z AMC; tej operacji nie cofa `Ctrl+Z`. Zwykły `Delete` nadal nie dotyka dysku. `Ctrl+C` kopiuje nazwy wszystkich zaznaczonych elementów w osobnych wierszach, a `Ctrl+Shift+C` zachowuje ścieżki i dane `FileDrop`. Po zmianie sesji kontekst ma kolejność **numer i sesja → przywrócony widok → element**.

W `alpha.61` prawa strzałka na lokalnej liście nie otwiera już menu kontekstowego AMC. Wywołuje bezpośrednio systemowe **Otwórz w…**, aby jednorazowo wybrać np. foobar2000 albo zmienić aplikację domyślną, jeśli pozwala na to bieżąca wersja Windows. `Shift+Delete` działa również w otwartym odtwarzaczu: po potwierdzeniu zatrzymuje i zwalnia bieżący plik przed przekazaniem go do Kosza. Usunięte identyfikatory są równocześnie usuwane z trwałej historii oraz z jej aktywnej migawki. Zapis nie zachowuje już osieroconego identyfikatora ostatniego pliku po usunięciu całej lokalnej sesji, a normalizacja czyści starsze takie wpisy. Samo przeniesienie do Kosza pozostaje synchroniczną operacją powłoki Windows i może chwilę trwać na dysku iCloud.

W `alpha.62` lewa strzałka zawsze próbuje podać średni bitrate lokalnego pliku. Jeśli dane nie zostały jeszcze zapisane podczas odtwarzania, AMC otwiera tylko zaznaczony plik w trybie odczytu metadanych, ustala czas i częstotliwość próbkowania, wylicza `kb/s` z rozmiaru i czasu, a wynik zachowuje na przyszłość. Nie skanuje całej biblioteki. Dla niedostępnego albo nierozpoznawalnego pliku pozostałe informacje nadal są odczytywane. `Alt+F4` ma jednoznaczne znaczenie systemowe: w głównym oknie zamyka całą aplikację także wtedy, gdy widoczny jest odtwarzacz. `Escape` i `Shift+F6` pozostają poleceniami powrotu do listy.

W `alpha.63` przywrócona pozycja jest widoczna i używana jeszcze przed pierwszym otwarciem pliku przez silnik audio. Prawa strzałka uruchamia bezpośrednio systemowy wybór aplikacji także dla rozszerzenia bez poprawnego skojarzenia. `Shift+Delete` fizycznie przenosi do Kosza tylko pliki zaznaczone na liście. W lokalnym odtwarzaczu `Delete` usuwa bieżący wpis z AMC, pozostawia plik na dysku, oznajmia następny element i daje się cofnąć przez `Ctrl+Z`.

W `alpha.64` wycofano systemowe „Otwórz w…” i przypisaną do niego prawą strzałkę, ponieważ ręczny test NVDA wykazał utratę czytelnego fokusu. Prawa strzałka znów zachowuje standardowe działanie listy. Dostępne pozostaje „Otwórz w domyślnej aplikacji” dla poprawnie skojarzonych plików. Pozostałe poprawki `alpha.63`, w tym zapamiętywanie pozycji oraz bezpieczne `Delete` w odtwarzaczu, nie zmieniają się.

W `alpha.65` „Otwórz w…” wraca do testów po poprawieniu kolejności zdarzeń fokusu. AMC kończy obsługę prawej strzałki albo menu, a dopiero potem otwiera systemowy wybór aplikacji. Oddzielny test prawej strzałki i menu pozwoli zachować przynajmniej działające wejście, jeżeli problem okaże się związany tylko ze skrótem.

W `alpha.66`, po negatywnym teście obu wejść `alpha.65`, „Otwórz w…” jest uruchamiane przez osobny proces powłoki Windows. Ma to pozwolić systemowi ustanowić zwykły fokus pierwszoplanowy poza wątkiem WPF. Jest to ostatni wariant testowy; jeśli NVDA nadal nie odczyta okna, funkcja zostanie usunięta. W planie lokalnym Foldery stają się głównym widokiem, Zakładki poprzedzają prosty niedestrukcyjny montaż A–B, a późniejszy pilot NVDA pozostaje cienką warstwą nad wspólnym rdzeniem.

W `alpha.67` „Otwórz w…” zostaje definitywnie usunięte po negatywnych testach wszystkich trzech wariantów. Prawa strzałka zachowuje zwykłe działanie listy, a menu lokalnego pliku nadal zawiera stabilne „Otwórz w domyślnej aplikacji”. Zmianą skojarzeń zarządza Windows poza AMC. Pozostałe funkcje i plan rozwoju `alpha.66` pozostają bez zmian.

W `alpha.68` działają trwałe Zakładki. W otwartym odtwarzaczu `B` zapisuje bieżące miejsce, `Shift+Page Up` i `Shift+Page Down` przechodzą po zakładkach tego samego materiału, a `Ctrl+B` otwiera wspólną listę zakładek ze wszystkich sesji. Każdy wiersz podaje tytuł, czas i usługę; Enter wybiera właściwą sesję, rozpoczyna materiał i ustawia zapisaną pozycję. `Delete` usuwa wyłącznie zakładkę, a `Shift+Delete` jest w tym widoku blokowany, aby nie skasować pliku. Zakładki są zapisywane w `state.json` i w pełnej kopii `*.amcbackup.json`. Na zwykłych listach pojedyncze `B` nadal służy nawigacji literowej.

W `alpha.69` szybkie, kolejne naciśnięcia `Shift+Page Up` i `Shift+Page Down` poruszają się sekwencyjnie względem ostatnio osiągniętej zakładki, nawet gdy odtwarzanie zdążyło ruszyć dalej. Kategoria „Oznajmiaj nawigację po zakładkach” w Ustawieniach → Komunikaty pozwala wyciszyć automatyczną wypowiedź czasu bez wyłączania skrótów. Informacja o braku dalszej zakładki pozostaje słyszalna jako potrzebny komunikat graniczny. Nawigacja celowo nie przechodzi do innego pliku.

W `alpha.70` `Ctrl+Shift+B` otwiera w odtwarzaczu dostępne pole nazwy i zapisuje nazwaną zakładkę. Jeśli szybka zakładka już istnieje w tej samej sekundzie, otrzymuje podaną nazwę zamiast tworzenia duplikatu. Na liście `Ctrl+B` nazwa jest czytana jako pierwsza, przed tytułem materiału, czasem i usługą; działa również w filtrze i nawigacji literowej. Nazwa jest trwała i wchodzi do pełnej kopii danych.

W `alpha.71` globalna lista pokazuje najpierw zakładki bieżącego materiału w kolejności czasu od początku do końca. Pozostałe wpisy są grupowane według sesji i tytułu materiału, a wewnątrz każdego materiału również według czasu. Enter na zakładce ustawia fokus bezpośrednio na głównym przycisku odtwarzacza; Escape wraca na ten sam rekord listy. Ustawienie fokusu odbywa się z priorytetem załadowanego widoku, aby nie zatrzymywało się na ukrytej liście ani nagłówku.

W `alpha.72` każdy rekord globalnej listy Zakładek zaczyna się od tytułu pliku lub materiału, następnie podaje lokalną datę utworzenia zakładki i pozycję w materiale. Opcjonalna nazwa zakładki pozostaje częścią rekordu. Nawigacja `Shift+Page Up/Down` wewnątrz otwartego materiału jest krótsza: podaje sam czas, a dla nazwanej zakładki nazwę i czas. `Ctrl+C` na liście Zakładek kopiuje widoczne opisy zaznaczonych zakładek zamiast samego tytułu pliku źródłowego.

Ta sama wersja ujednolica schowek w obu oknach wyszukiwania: `Ctrl+C` kopiuje nazwę, a `Ctrl+Shift+C` kopiuje prawdziwy plik i jego pełną ścieżkę albo łącze do wyniku usługowego. `Ctrl+X` na zwykłych listach lokalnych oraz na lokalnym wyniku wyszukiwania przekazuje prawdziwe pliki do systemowego schowka z operacją przeniesienia. Sam skrót nie usuwa danych; przeniesienie wykonuje dopiero `Ctrl+V` w folderze docelowym. Po powrocie AMC usuwa wpisy, których stare ścieżki rzeczywiście przestały istnieć. Wycinanie nie działa na liście Zakładek ani dla elementów strumieniowych.

W `alpha.73` lista wyników wyszukiwania używa zaznaczania rozszerzonego. `Shift+strzałka` może zaznaczyć kilka wyników, `Ctrl+C` kopiuje ich nazwy w oddzielnych wierszach, a `Ctrl+Shift+C` przekazuje lokalne wyniki jako prawdziwe pliki oraz pełne ścieżki; dla wyników usługowych kopiuje łącza. Historia wpisanych zapytań pozostaje zwykłym tekstem. `Ctrl+X` i `Ctrl+V` są blokowane na wynikach wyszukiwania.

`Ctrl+V` na listach lokalnych pełni rolę dostępnego odpowiednika przeciągania i upuszczania. W widokach Multimedia i Biblioteka importuje obsługiwane pliki audio do katalogu AMC, w Kolejce dodatkowo dodaje je do kolejki, a w Ulubionych oznacza jako ulubione. Pliki pozostają w dotychczasowych folderach — AMC zapisuje ich rzeczywiste ścieżki i nie tworzy ukrytej kopii. Wklejanie jest niedostępne w Historii odtwarzania, Zakładkach, odtwarzaczu i sesjach streamingowych. Wewnętrzne wklejenie po `Ctrl+X` anuluje systemowy zamiar przeniesienia, aby późniejsze przypadkowe wklejenie poza AMC nie przesunęło pliku.

W `alpha.74` Zakładki są przejściowym, otwieranym jawnie widokiem. `Ctrl+B` zapamiętuje sesję, poprzedni widok i zaznaczony element, a Escape wraca dokładnie do tego miejsca. Po otwarciu zakładki Enterem pierwszy Escape wraca do tego samego rekordu Zakładek, a drugi — do listy, z której wywołano `Ctrl+B`. Jeśli filtr Zakładek zawiera tekst, pierwszy Escape czyści filtr i pozostawia widok otwarty. Program nie przywraca już po uruchomieniu samej listy Zakładek bez kontekstu; wcześniejszy taki stan jest normalizowany do podstawowej listy sesji. `Shift+Page Up/Down` zachowuje dwa jednoznaczne znaczenia: w odtwarzaczu przechodzi po zakładkach bieżącego materiału, natomiast na każdej zwykłej liście wykonuje standardowe rozszerzone zaznaczanie stronami i nigdy nie otwiera widoku Zakładek.

W `alpha.75` strzałka w lewo podaje krótkie informacje o elemencie zarówno na zwykłej liście, jak i na liście wyników wyszukiwania. Dla pliku lokalnego obejmują dostępne wartości: format, wykonawcę, czas, bitrate, częstotliwość próbkowania i rozmiar. W sesji streamingowej używany jest ten sam układ, ale AMC odczytuje wyłącznie metadane faktycznie zwrócone przez adapter usługi i nie wymyśla ani nie szacuje parametrów strumienia. Fokus pozostaje na wybranym rekordzie.

W `alpha.76` `Shift+Delete` używa nowoczesnego interfejsu `IFileOperation` powłoki Windows zamiast starszego mechanizmu kasowania. Jest to istotne dla plików-placeholderów zarządzanych przez iCloud Drive i innych dostawców chmurowych. Po potwierdzeniu operacja nadal przenosi wyłącznie do systemowego Kosza; nie wykonuje trwałego usunięcia. Błąd pojedynczego pliku nie zamyka AMC, nie usuwa jego rekordu z katalogu i jest oznajmiany wraz z kodem systemowym pomocnym w diagnozie. Pozostałe poprawnie przeniesione pozycje są bezpiecznie usuwane z katalogu AMC.

W `alpha.84` domyślne wyjście `Escape`, `Shift+F6`, przyciskiem **Wróć do listy** albo bezpośrednio do innego widoku wstrzymuje dźwięk. Można wyłączyć tę zasadę w Ustawieniach ogólnych. Jest ona niezależna od pamiętania pozycji: domyślnie lokalne pliki zachowują miejsce, natomiast każde zarejestrowane źródło folderowe może wymusić **Pamiętaj pozycję**, **Zawsze od początku** albo odziedziczyć ustawienie ogólne. Pliki dodane pojedynczo przez `Ctrl+O` używają zasady ogólnej. Zwykła lista nadal zachowuje standardowe działanie strzałek; przewijanie i regulacja głośności pozostają w odtwarzaczu.

W `alpha.85` lista w menedżerze źródeł udostępnia NVDA wyłącznie czytelną etykietę źródła. Techniczny zapis rekordu z identyfikatorem i nazwami pól nie jest już przekazywany przez UI Automation.

W `alpha.86` `Ctrl+F5` otwiera Menedżera Biblioteki, a `F5` pozostaje ręcznym odświeżeniem źródeł. Na lokalnej liście `F2` zmienia wyłącznie trwałą nazwę wyświetlaną w AMC, bez dotykania pliku. `Shift+F2` zmienia rzeczywistą nazwę pliku na dysku, zachowuje rozszerzenie, stabilny identyfikator, Ulubione, Kolejkę, Historię, Zakładki i pozycję wznowienia. Istniejący cel, nazwy niedozwolone i nazwy zarezerwowane Windows są odrzucane bez nadpisania. Jeśli zmieniany plik był załadowany, AMC zatrzymuje go i zwalnia przed operacją, zachowując miejsce do późniejszego wznowienia.

W `alpha.87` lokalna Biblioteka ma trzy jawne układy: `Alt+1` — **Foldery Biblioteki**, `Alt+2` — **Wszystkie pliki alfabetycznie** i `Alt+3` — **Kolejność własna**. Tylko w Kolejności własnej `Alt+strzałka w górę/w dół` przesuwa jeden plik albo ciągły blok zaznaczony Shiftem. Porządek jest trwały, nowe pliki trafiają na koniec, a operacja nie zmienia folderów, nazw ani położenia plików na dysku. Aktywny filtr blokuje przesuwanie. `Ctrl+K` filtruje tylko bieżącą listę; Escape czyści filtr i wraca do listy, natomiast przejście do innego widoku, folderu albo sesji również automatycznie usuwa filtr. Filtr nie jest przywracany po restarcie programu.

Obecny katalog demonstracyjny może pokazywać wspólne wyniki testowe. Prawdziwy adapter TIDAL będzie modułem izolowanym: `Ctrl+Shift+F` może uruchomić jego zapytanie, ale treści TIDAL nie zostaną wymieszane na jednej liście z treściami podobnych usług. AMC otworzy osobny, oznaczony widok wyników TIDAL i zachowa działanie wszystkich wspólnych skrótów.

Planowany moduł YouTube zacznie od publicznego wyszukiwania i oficjalnego, widocznego odtwarzacza bez synchronizacji konta. Jego lokalna Biblioteka obejmie wyłącznie materiały świadomie dodane do Ulubionych, własne playlisty AMC i lokalną historię odtwarzania; nie kopiujemy całego konta ani pełnego interfejsu YouTube. Logowanie OAuth pozostaje nieobowiązkowym późniejszym rozszerzeniem, jeżeli pojawi się realna potrzeba subskrypcji, playlist lub polubień z konta. Oficjalny adapter nie będzie pobierać, wyodrębniać dźwięku ani nagrywać materiałów odtwarzanych z YouTube. Ewentualne eksperymentalne narzędzia zapisu pozostaną osobnym, izolowanym i niezależnie aktualizowanym modułem dla źródeł, które na zapis pozwalają; nie staną się częścią rdzenia ani warunkiem działania YouTube. Interfejs może być podobny do podcastów, lecz źródłem pozostaje oficjalny odtwarzacz YouTube.

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
- `*.amcbackup.json` — pełna kopia: ustawienia, profile klawiatury, sesje, zakładki i szablony komunikatów.

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

- TIDAL, Apple Music i WiiM są obecnie sesjami demonstracyjnymi. Sesja Pliki lokalne odtwarza prawdziwe pliki i trwale zapisuje swój katalog oraz stan w AppData.
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
