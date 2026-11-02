# SET-001 — audyt Ustawień, NVDA i integracja z Windows

Status: **W trakcie — kod priorytetów 1–3, pełna regresja, czysta paczka,
instalator, wydanie GitHub i rzeczywista aktualizacja instalacji zweryfikowane
27.10.2026; ręczny odbiór NVDA pozostaje**

## Zgłoszenie użytkownika

26 października 2026 r. użytkownik wskazał dwie luki w oknie **Ustawienia**:

- na kartach dotyczących Podcastów oraz nagrywania Tab przechodzi od razu do
  przycisku **Wybierz**, chociaż wcześniej NVDA powinien odczytać nazwę pola i
  bieżącą ścieżkę domyślnego folderu;
- ustawienia aplikacji nie pokazują integracji dodanej w instalatorze, w tym
  poleceń Eksploratora Windows, wpisu **Otwórz za pomocą** i wyboru typów
  plików.

Ta decyzja rozszerza wcześniejszy zakres AMC14. Integracja nie ma być
jednorazowym, niewidocznym wyborem instalatora: bieżący stan powinien być
czytelny i odwracalny również z Ustawień AMC.

### Korekta zakresu użytkownika — 27.10.2026

Użytkownik zaakceptował dodanie rekomendowanej karty **Aktualizacje** oraz
zmianę nazwy karty **Komunikaty** na **Dostępność i komunikaty**. Ta decyzja
zastępuje wcześniejsze ustalenie z pierwszego etapu realizacji, według którego
priorytet 3 miał pozostać jedynie rekomendacją do osobnego zadania. Publikacja
na GitHubie i instalator mają powstać dopiero po zakończeniu równoległej pracy.

### Korekta układu kart i sekcji — 27.10.2026

Użytkownik ustalił następującą kolejność kart: **Ogólne**, **Radio i
nagrywanie**, **Podcasty i YouTube**, **Dostępność i komunikaty**,
**Klawiatura**, **Aktualizacje**, **Integracja z Windows**. Integracja pozostaje
na końcu jako ustawienie systemowe wykonywane rzadziej niż ustawienia pracy z
mediami.

Karty Radio oraz Podcasty mają zaczynać się od ustawień zapisu. Na karcie
Radio najpierw wybiera się położenie nagrań, odczytuje ścieżkę osobnego
folderu i ustawia format; dopiero potem występują opcje bufora, rozpoznawania i
harmonogramu. Na karcie Podcasty najpierw występują folder i format wideo, a
potem automatyczne odświeżanie. Ta korekta zastępuje wcześniejszy techniczny
układ kontrolek i nie zmienia wartości modelu ani dotychczasowych ustawień
użytkownika.

### Korekta edytora komunikatów i kierunek wielojęzyczności — 27.10.2026

Pole **Oznajmiaj automatycznie rozpoznane utwory** ma występować razem ze
zwykłymi opcjami oznajmiania, przed listą edytowanych tekstów. Edytor własnych
tekstów pozostaje bezpośrednio na karcie, ale trafia na jej koniec pod
nagłówkiem **Własne teksty komunikatów**. Lista zdarzeń i powiązane pole tekstu
są prostsze w obsłudze NVDA niż edytowalna tabela; osobny przycisk i dialog
ukrywałyby funkcję oraz wymagały dodatkowego kontraktu powrotu fokusu.

Przyszłego języka interfejsu nie należy łączyć z profilem komunikatów. Zalecany
model ma dwa niezależne wybory:

- **Język interfejsu** na karcie Ogólne: zgodny z systemem, polski, angielski
  i później kolejne kompletne katalogi tłumaczeń;
- **Profil komunikatów** na karcie Dostępność i komunikaty: standardowy,
  ograniczony, szczegółowy albo własny.

W modelu dane powinny nadal używać stabilnych kluczy zdarzeń i profili, a NVDA
ma otrzymywać wyłącznie przetłumaczone nazwy użytkowe. Domyślne teksty należy
przechowywać osobno dla każdego języka. Własne nadpisania powinny być związane
z językiem, aby polski tekst użytkownika nie zastąpił angielskiego domyślnego
komunikatu po zmianie języka. Obecne polskie nadpisania trzeba przy przyszłej
migracji zachować jako profil własny dla języka polskiego. Ten model jest na
razie decyzją projektową, a nie zaimplementowaną funkcją.

## Wynik audytu

### 1. Potwierdzona przyczyna pomijania ścieżek przez Tab

Oba pola ścieżek są natywnymi `wx.TextCtrl` utworzonymi ze stylem
`wx.TE_READONLY`:

- `recording_folder` — **Domyślny folder nagrywania radia**;
- `podcast_folder` — **Domyślny folder pobierania podcastów**.

Pola mają jawne, użytkowe nazwy dostępności i pokazują wartość ścieżki. Test
na runtime bieżącej instalacji (`wxPython 4.3.1`, `wxWidgets 3.3.3`) wykazał
jednak:

- `AcceptsFocus=True`;
- `CanAcceptFocus=True`;
- `AcceptsFocusFromKeyboard=False`.

Wyjaśnia to całe zgłoszone zachowanie: pole może dostać fokus myszą albo przez
`SetFocus()` po wyborze folderu, lecz natywna nawigacja Tab je pomija. Obecny
test `test_general_settings_dialog.py` sprawdza jedynie istnienie nazw w
źródle. Nie uruchamia prawdziwej nawigacji i nie wykrywa tej regresji.

### 2. Nazwy i wartości pozostałych kontrolek

Statyczny przegląd bieżącego `GeneralPlaybackOptionsDialog` wykazał 51
przechowywanych kontrolek interaktywnych, w tym notebook. Każda z nich ma
jawne `SetName(...)`. Pola wyboru otrzymują użytkowe napisy oddzielone od
wewnętrznych wartości modelu, więc nie znaleziono wycieku reprezentacji
obiektu, nazwy enuma ani identyfikatora komendy.

Pozostały trzy problemy jakościowe:

1. Standardowy przycisk **Anuluj** polega na etykiecie tworzonej przez
   wxWidgets i nie ma własnej, stabilnej nazwy **Anuluj ustawienia**.
2. Opcje algorytmu tempa mówią **Mowa – Speedy**, **Muzyka – Signalsmith** i
   **Dotychczasowy – SoundTouch**. `Speedy`, `Signalsmith` oraz `SoundTouch`
   są nazwami implementacji. W zwykłej mowie powinny pozostać etykiety skutku,
   na przykład **Najlepsza czytelność mowy**, **Najlepsza jakość muzyki** i
   **Tryb zgodności**; nazwy techniczne mogą trafić do szczegółów.
3. Nazwa **Domyślny folder pobierania podcastów** jest za wąska, ponieważ ten
   sam folder obsługuje również pobierany dźwięk materiałów YouTube. Zalecana
   nazwa: **Domyślny folder zapisu pobranych podcastów i dźwięku YouTube**.

### 3. Integracja z Windows istnieje, ale nie jest widoczna w Ustawieniach

Źródło instalatora ma dwie dobrowolne opcje:

- polecenia **Otwórz w AMC Test** i **Dodaj do kolejki AMC Test** w menu
  kontekstowym obsługiwanych plików;
- obecność AMC Test na liście **Otwórz za pomocą** i w ustawieniach aplikacji
  domyślnych Windows.

Odczyt bieżącego rejestru potwierdził aktywną integrację AMC Test dla 48
rozszerzeń w obu miejscach. Profil publiczny nie ma obecnie takiej rejestracji.
Instalator stosuje jednak wybór do całej listy naraz. Nie udostępnia wyboru
grup ani pojedynczych rozszerzeń, a okno Ustawień nie pokazuje nawet stanu
dwóch głównych opcji.

W Core istnieje już pięć użytkowo nazwanych grup, które powinny stać się
wspólnym modelem instalatora i Ustawień:

- **Muzyka i nagrania**;
- **Rzadsze formaty dźwiękowe**;
- **Filmy i pojemniki wideo**;
- **Playlisty**;
- **Niedokończone nagrania AMC**.

## Zalecany zakres wdrożenia

### Priorytet 1 — naprawa dwóch pól folderów

- oba pola ścieżki muszą być osobnymi przystankami Tab przed przyciskiem
  **Wybierz**;
- NVDA ma podać pełną nazwę pola, bieżącą ścieżkę i stan tylko do odczytu;
- po **Wybierz**, anulowaniu systemowego dialogu, użyciu folderu domyślnego,
  Zapisz i Anuluj fokus ma wracać do przewidywalnego miejsca;
- test ma używać rzeczywistej kontrolki wx, a nie wyszukiwać napis w źródle.

Oczekiwana kolejność na karcie Podcasty i YouTube:

1. **Domyślny folder zapisu pobranych podcastów i dźwięku YouTube**, ścieżka;
2. **Wybierz domyślny folder zapisu**;
3. **Użyj domyślnego folderu AMC**.

Analogiczna kolejność obowiązuje na karcie Radio i nagrywanie.

### Priorytet 2 — karta Integracja z Windows

Nowa karta **Integracja z Windows** powinna zawierać:

- **Pokaż polecenia Otwórz w AMC Test i Dodaj do kolejki AMC Test w menu
  kontekstowym**;
- **Pokaż AMC Test na liście Otwórz za pomocą i w ustawieniach aplikacji
  domyślnych Windows**;
- pięć pól wyboru grup typów plików z listą rozszerzeń w pomocy dostępności;
- **Otwórz ustawienia aplikacji domyślnych Windows**;
- nazwany region stanu informujący, czy zapis zastosowano i ilu typów plików
  dotyczy.

Etykieta grup nie może mówić **Odtwarzaj te rozszerzenia**, ponieważ wyłączenie
integracji nie odbiera AMC możliwości ręcznego otwarcia pliku. Powinna mówić
**Udostępniaj AMC dla następujących typów plików**.

Ustawienia i instalator muszą korzystać z jednego modelu wyboru. Aktualizacja
instalatora nie może przywracać starego zestawu po zmianie wykonanej w
aplikacji. AMC Test i AMC Public zachowują osobne nazwy oraz wpisy rejestru.

### Priorytet 3 — karta Aktualizacje

Bieżący host respektuje odziedziczone ustawienia automatycznego sprawdzania i
pobierania składników FFmpeg oraz yt-dlp, lecz AMC Python nie pozwala ich
odczytać ani zmienić. Warto dodać kartę **Aktualizacje** z:

- automatycznym sprawdzaniem składników;
- automatycznym pobieraniem zweryfikowanych składników;
- przyciskiem ręcznego sprawdzenia;
- czytelnym stanem FFmpeg i yt-dlp.

Kanał `test` albo `stable` wynika z profilu wydania i powinien być pokazany
informacyjnie, a nie jako pozornie edytowalny wybór.

## Realizacja 27.10.2026

Zrealizowano priorytety 1–3 oraz korekty nazw znalezione podczas audytu:

- dodano wspólną kontrolkę `FocusableReadOnlyTextCtrl`, która pozostaje tylko
  do odczytu, ale jest prawdziwym przystankiem Tab;
- pola Radia oraz Podcastów i YouTube pokazują rzeczywiste ścieżki domyślne
  zwracane przez host, a nie ogólny tekst zastępczy;
- etykieta pola oraz nazwy obu przycisków Podcastów mówią konsekwentnie o
  podcastach i dźwięku YouTube;
- przycisk anulowania ma stabilną nazwę **Anuluj ustawienia**, a trzy sposoby
  zmiany tempa mają nazwy opisujące skutek zamiast nazw bibliotek;
- dodano kartę **Integracja z Windows** z dwoma niezależnymi przełącznikami,
  pięcioma grupami typów plików, dostępną pomocą z rozszerzeniami, stanem oraz
  przyciskiem otwierającym ustawienia aplikacji domyślnych Windows;
- AMC Test i AMC Public używają oddzielnych nazw klas, poleceń, możliwości i
  trwałych ustawień w rejestrze bieżącego użytkownika;
- zapis z aplikacji przyjmuje tylko pięć znanych identyfikatorów grup; klient
  nie może przekazać dowolnego rozszerzenia, klucza rejestru ani polecenia;
- instalator zachowuje wybór zapisany później przez aplikację i podczas
  aktualizacji nie przywraca automatycznie wszystkich 48 rozszerzeń;
- współbieżnie rozwijana karta **Klawiatura** z zadania KEY-001 używa tej samej
  dostępnej kontrolki tylko do odczytu dla pola nowego skrótu.
- dodano kartę **Aktualizacje** z informacyjnym kanałem wydania, dwoma
  przełącznikami automatyki, osobnymi polami stanu FFmpeg i yt-dlp oraz
  przyciskiem ręcznego sprawdzenia;
- karta nie tworzy drugiej implementacji aktualizacji: przycisk wywołuje to
  samo polecenie **Sprawdź, pobierz i zweryfikuj składniki FFmpeg i yt-dlp**,
  które jest w menu Narzędzia, palecie poleceń i pomocy;
- oba wybory automatyki są prywatnymi ustawieniami AMC Python. Do hosta trafiają
  jako wąskie opcjonalne wartości; brak wyboru nadal dziedziczy dotychczasową
  politykę profilu, a stary wspólny `state.json` nie jest zapisywany;
- karta **Komunikaty** otrzymała nazwę **Dostępność i komunikaty** bez zmiany
  działania istniejących przełączników i szablonów.
- karty ustawiono w kolejności zgodnej z codziennym przebiegiem pracy:
  **Ogólne**, **Radio**, **Podcasty**, **Dostępność i komunikaty**,
  **Klawiatura**, **Aktualizacje**, **Integracja z Windows**;
- karta Radio rozpoczyna się od sekcji **Zapisywanie nagrań**, a karta
  Podcasty od sekcji **Zapisywanie pobranych materiałów**; ustawienia bufora,
  rozpoznawania, harmonogramu i automatycznego odświeżania są w dalszych,
  nazwanych sekcjach;
- kolejność utworzenia kontrolek odpowiada kolejności wizualnej, dlatego Tab
  przechodzi przez ścieżkę przed przyciskiem **Wybierz**, bez osobnej,
  niewidocznej logiki nawigacji.

Ponowny audyt statyczny wykazał 65 przechowywanych kontrolek interaktywnych
tego okna. Wszystkie 65 mają jawne `SetName(...)`; dynamiczne pola grup
integracji również dostają nazwę użytkową i pomoc. Nie znaleziono nazwy enuma,
identyfikatora komendy, reprezentacji obiektu ani nazwy klasy wystawionej jako
zwykła etykieta kontrolki.

Wcześniejszy zapis, że priorytet 3 pozostaje rekomendacją, został zastąpiony
decyzją użytkownika z 27.10.2026. Karta ma pełny model stanu i testy oraz weszła
do instalatora `AMC-Test-2026-10-27-0057.exe` i zaktualizowanej instalacji.

### Pliki zmienione w zakresie SET-001

- `wxlite/amc_wx_lite/accessible_controls.py` — kontrolka tylko do odczytu,
  osiągalna klawiaturą;
- `wxlite/amc_wx_lite/gui.py` — pola folderów, nazwy, karta integracji i
  obsługa zapisu; ten plik zawiera też równoległą pracę KEY-001, której nie
  należy przypisywać wyłącznie temu zadaniu;
- `wxlite/amc_wx_lite/host_client.py` — wąskie operacje integracji, stanu
  składników i uruchamiania wspólnego aktualizatora;
- `wxlite/amc_wx_lite/state_store.py` — prywatne, opcjonalne wybory automatyki
  aktualizacji składników;
- `src/AccessibleMediaController.LiteHost/LiteComponentUpdateCoordinator.cs`
  i `Protocol/LiteComponentUpdatePolicy.cs` — stan użytkowy oraz bezpieczne
  nadpisania polityki dla bieżącego wywołania;
- `tests/AccessibleMediaController.LiteHost.ProtocolTests/ComponentUpdatePolicyTests.cs`
  i `wxlite/tests/test_component_updates.py` — dziedziczenie, nadpisania,
  klient protokołu, wspólne wykonanie i callback karty;
- `src/AccessibleMediaController.Core/ShellIntegration/ShellIntegrationRules.cs`
  — stabilne identyfikatory grup i tożsamości profili Test/Public;
- `src/AccessibleMediaController.LiteHost/LiteGeneralSettingsCoordinator.cs`
  oraz `LiteShellIntegrationCoordinator.cs` — odczyt folderów i integracja
  bieżącego profilu;
- `src/AccessibleMediaController.LiteHost/Program.cs` — rejestracja operacji;
- `installer/AMC_Wx_Setup.iss` — zachowanie wyboru zarządzanego przez
  aplikację;
- `wxlite/tests/test_general_settings_dialog.py`,
  `test_focusable_readonly_text_ctrl_native.py`,
  `test_shell_integration_host_client.py` oraz
  `tests/AccessibleMediaController.Core.SmokeTests/InstallerOfflineContractTests.cs`
  — testy kontraktowe i natywny test fokusu.

Niezwiązane z tą kartą fragmenty `shortcuts.py`, `menu_model.py`,
`state_store.py` i innych plików KEY-001 widoczne równocześnie w drzewie
roboczym należą do osobnego zadania i zostały zachowane bez cofania.

## Materiał do dokumentacji użytkownika

Projekt tekstu po instalacji i ręcznym odbiorze:

> Karty Ustawień są ułożone w kolejności: **Ogólne**, **Radio i nagrywanie**,
> **Podcasty i YouTube**, **Dostępność i komunikaty**, **Klawiatura**,
> **Aktualizacje**, **Integracja z Windows**. Na kartach Radio oraz Podcasty
> ustawienia miejsca i formatu zapisu znajdują się przed ustawieniami
> odtwarzania, rozpoznawania, odświeżania i harmonogramu.
>
> Na kartach **Radio i nagrywanie** oraz **Podcasty i YouTube** Tab najpierw
> przechodzi do pola z bieżącą ścieżką, a dopiero potem do przycisku
> **Wybierz**. Pole jest tylko do odczytu i nie pozwala przypadkowo zmienić
> ścieżki podczas pisania.
>
> Karta **Integracja z Windows** pokazuje, czy AMC jest dostępny w menu
> kontekstowym i na liście **Otwórz za pomocą**. Można osobno wybrać grupy
> plików audio, wideo, playlist i niedokończonych nagrań. Nie ustawia to AMC
> jako programu domyślnego bez decyzji użytkownika.
>
> Karta **Aktualizacje** podaje kanał bieżącego wydania, ustawienia
> automatycznego sprawdzania i pobierania oraz stan FFmpeg i yt-dlp. Pola
> kanału i stanu są tylko do odczytu, ale Tab zatrzymuje się na nich, aby NVDA
> podał nazwę i wartość. Przycisk ręcznego sprawdzenia wykonuje to samo
> polecenie co menu Narzędzia i paleta poleceń.
>
> Dotychczasowa karta **Komunikaty** nazywa się teraz **Dostępność i
> komunikaty**, aby jej nazwa obejmowała także szczegółowe podpowiedzi
> klawiatury i pozostałe opcje dla czytnika ekranu.
>
> Opcja **Oznajmiaj automatycznie rozpoznane utwory** znajduje się przed
> edytorem komunikatów. Na końcu karty sekcja **Własne teksty komunikatów**
> pozwala wybrać zdarzenie strzałkami, nacisnąć Tab i zmienić jego tekst.
> Pozostawienie tekstu pustego wyłącza tylko wybrany komunikat.

## Uzasadnienie i decyzje

- Pole ścieżki pozostaje tylko do odczytu; poprawiamy możliwość osiągnięcia go
  klawiaturą, a nie zmieniamy go w edytowalne pole podatne na przypadkowe
  nadpisanie.
- Pięć grup jest dostępniejsze niż 48 kolejnych pól wyboru. Dokładne
  rozszerzenia pozostają w pomocy każdej grupy. Ewentualny wybór pojedynczych
  rozszerzeń może być osobnym przyciskiem **Dostosuj rozszerzenia**.
- Stan rzeczywisty należy odczytywać z wpisów danego profilu w Windows, a
  trwały zamiar przechowywać we wspólnym modelu używanym przez instalator i
  aplikację.
- Konta, urządzenia i Foldery Biblioteki pozostają pod kontekstowym
  **Konfiguruj**, czyli `Ctrl+F5`; nie należy dublować wieloetapowych menedżerów
  w Ustawieniach ogólnych.
- Kanał aktualizacji programu wynika z wariantu instalacji. Jest polem
  informacyjnym **Testowy — wydania wstępne AMC Test** albo **Stabilny —
  publiczne wydania AMC**, a nie pozornie edytowalnym wyborem.
- Oba przełączniki automatyki pozostają dostępne w kolejności Tab również wtedy,
  gdy automatyczne sprawdzanie jest wyłączone. Drugi wybór może dzięki temu
  zostać przygotowany wcześniej; jego pomoc wyjaśnia, że działa dopiero przy
  automatycznym sprawdzaniu.

## Testy i wynik

Audyt początkowy 26.10.2026:

- porównano ówczesne źródło `gui.py` z plikiem zainstalowanym w
  `D:\Projekty Codex\AMC Python`; SHA-256 był identyczny;
- runtime: Python 3.14, wxPython 4.3.1, wxWidgets 3.3.3;
- zwykła kontrolka `wx.TextCtrl(..., style=wx.TE_READONLY)` zwróciła
  `AcceptsFocusFromKeyboard=False`, co potwierdziło przyczynę;
- bezpośredni odczyt rejestru wykazał wtedy 48 rozszerzeń AMC Test w
  **Otwórz za pomocą** i 48 w menu kontekstowym.

Weryfikacja wdrożenia 27.10.2026:

- rzeczywisty test wxPython potwierdził kolejność: przycisk przed polem,
  pole ścieżki tylko do odczytu, przycisk **Wybierz**; pole zwraca
  `IsEditable=False` i `AcceptsFocusFromKeyboard=True`;
- kontrola składni trzech zmienionych modułów Pythona: zaliczona;
- 58 celowanych testów ustawień, klienta hosta, natywnej kontrolki wx oraz
  skrótów klawiaturowych: **58 zaliczonych, 0 pominiętych, 0 błędów**;
- kompilacja `AccessibleMediaController.LiteHost` Debug: **0 ostrzeżeń,
  0 błędów**;
- test `--installer-offline`: zaliczony; potwierdza opcjonalną i odwracalną
  integrację w skrypcie, ale nie jest dowodem instalacji;
- samowystarczalna paczka Test v2 została zbudowana w
  `D:\Projekty Codex\AMC - integracja wxPython\.test-output\SET-001-settings-accessibility-20261027-v2`;
- manifest v2: `installId=AMC.Test.win-x64`, 2136 wpisów plików,
  `sourceRevision=73279fbb15a1cd34c6813f24e097e0467889a197`,
  `sourceDirty=true`;
- pełna kontrola integralności paczki: zaliczona;
- kontrolowany start interfejsu i hosta z paczki: zaliczony;
- host paczki zwrócił rzeczywiste foldery
  `C:\Users\micha\Music\AMC — Nagrania radia` i
  `C:\Users\micha\Music\AMC — Pobrane podcasty`, dostępność profilu
  **AMC Test** oraz wszystkie pięć grup użytkowych;
- podczas tej weryfikacji wywołano tylko operacje odczytowe stanu. Nie
  zastosowano integracji, nie otwarto Ustawień Windows i nie zmieniono
  rejestru.

Weryfikacja rozszerzenia o karty 27.10.2026:

- rzeczywisty test wxPython otworzył całe okno Ustawień i potwierdził kolejność
  Tab na karcie Aktualizacje: **Kanał aktualizacji programu**, automatyczne
  sprawdzanie, automatyczne pobieranie, **Stan składnika FFmpeg**;
- oba pola stanu oraz kanał używają `FocusableReadOnlyTextCtrl`, zachowują
  wartość tylko do odczytu i mają jawne nazwy dostępności;
- cztery bezpośrednio związane moduły testowe: **77 zaliczonych, 0 błędów**;
- szersza regresja ustawień, startu, opcji sesji, pomocy/transportu i klienta
  hosta: **172 zaliczone, 0 błędów**;
- zestaw C# `--component-updates`: zaliczony;
- kompilacja `AccessibleMediaController.LiteHost` Debug: **0 ostrzeżeń,
  0 błędów**;
- `git diff --check` dla zmienionych plików nie wykazał błędów białych znaków;
- nie zbudowano nowej paczki ani instalatora i nie uruchomiono publikacji,
  zgodnie z decyzją o oczekiwaniu na zakończenie równoległego zadania.

Wspólna weryfikacja i publikacja SET-001 oraz KEY-001 27.10.2026:

- pełna regresja wxPython: **2094 zaliczone, 113 kontrolowanie pominiętych i 13
  zaliczonych podtestów** w 199,80 s;
- wszystkie **27** zestawów testów protokołu LiteHosta zaliczone;
- testy Launchera i Updatera: **33 z 33 zaliczone**; pełne testy dymne Core
  zakończone kodem 0;
- czysta paczka z commitu `4cb57da5a6bda7bb82696ff426c00f2b9f655704`
  zawiera 2136 plików, przeszła pełną kontrolę integralności i kontrolowany
  start;
- instalator `AMC-Test-2026-10-27-0057.exe` przeszedł skan danych poufnych i
  zawiera Quick App Switcher 0.1.6;
- wydanie testowe `test-2026.10.27.0057` opublikowano i odczytowo
  zweryfikowano na GitHubie wraz z sumami plików.

Weryfikacja roboczych korekt układu kart i edytora komunikatów 27.10.2026:

- kontrola składni `wxlite/amc_wx_lite/gui.py`: zaliczona;
- 26 celowanych testów struktury Ustawień i Menedżera skrótów: **26
  zaliczonych, 0 błędów**;
- nowy kontrakt sprawdza dokładną kolejność siedmiu kart oraz kolejność
  tworzenia pól folderu, formatu, bufora i odświeżania wpływającą na Tab;
- kontrakt sprawdza również, że opcja rozpoznanych utworów i ustawienia stanu
  radia poprzedzają końcowy edytor własnych tekstów, a lista i pole tekstu mają
  jawne podpowiedzi klawiatury;
- nie otwierano okna GUI, nie budowano paczki ani instalatora, nie zmieniano
  instalacji i nie publikowano wydania; korekta ma wejść do następnego
  wspólnego wydania po zakończeniu równoległych prac.

Rzeczywista aktualizacja 27.10.2026:

- oficjalny `AMC-Python-Updater.exe` bezpiecznie zamknął poprzednią instancję,
  zainstalował pełny instalator i uruchomił AMC Test ponownie;
- manifest instalacji zmienił wersję z `26.10.2026, 19:54`, commit
  `251e163a`, na `27.10.2026, 00:57`, commit `4cb57da5`, pakiet `73ec48a6`;
- pełna kontrola integralności zainstalowanego pakietu zakończyła się kodem 0,
  a po starcie działała jedna instancja AMC Test;
- instalacja i manifest nie zawierają plików OAuth YouTube; nie uruchamiano
  logowania Google;
- 421 zapisanych kanałów YouTube zachowało identyczny zestaw identyfikatorów i
  adresów przed oraz po aktualizacji;
- interfejs natywny sterowania oknem nie był dostępny w sesji automatyzacji,
  dlatego karty Ustawień nadal wymagają ręcznego odbioru z NVDA.

Najważniejsze użyte polecenia i wejścia kontrolne:

- `uv run --no-project --python 3.14 --with pytest --with wxpython python -B
  -m pytest ... -q -p no:cacheprovider` dla 77 testów bezpośrednich i 172
  testów szerszej regresji;
- `dotnet run --project
  tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj
  --configuration Debug --no-restore -- --component-updates`;
- `dotnet build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj --no-restore --configuration Debug`;
- bezpośredni runner czterech modułów testowych z prywatnego runtime Python,
  ponieważ `pytest` nie jest w nim zainstalowany;
- `dotnet run --project tests\AccessibleMediaController.Core.SmokeTests\AccessibleMediaController.Core.SmokeTests.csproj --configuration Debug --no-restore -- --installer-offline`;
- `wxlite\tools\Build-AMCBundle.ps1 -Profile test` z prywatnym runtime
  bieżącej instalacji oraz wyjściem do katalogu paczki v2;
- operacje protokołu hosta `settings.defaults` i
  `shellIntegration.status` z paczki v2.

## Znane ograniczenia

- Nie wykonano instalacji ani ręcznej próby całego poprawionego okna z NVDA.
  Odbiór musi objąć Tab, Shift+Tab, zmianę kart, strzałki w polach wyboru,
  Zapisz, Anuluj, powrót fokusu oraz ponowne otwarcie.
- Natywny test wx dowodzi rzeczywistego fokusu klawiatury, ale nie zastępuje
  odsłuchu dokładnej wypowiedzi NVDA na komputerze użytkownika.
- Nie zastosowano wpisów integracji z paczki roboczej, aby audyt nie zmieniał
  rejestru. Ścieżka zapisu jest skompilowana i objęta testami kontraktowymi,
  lecz wymaga odbioru po bezpiecznej instalacji AMC Test.
- Inno Setup 7 było dostępne podczas końcowej promocji i instalator został
  zbudowany oraz zweryfikowany. Sam build nie dowodzi jednak poprawnej
  instalacji ani wypowiedzi NVDA na komputerze użytkownika.
- Prywatny runtime bieżącej instalacji nie zawiera `pytest`; w pierwszym
  etapie uruchomiono więc 58 testów bezpośrednio. Rozszerzenie kart sprawdzono
  później w przypiętym środowisku `uv` z pytest i wxPython. Przed publikacją
  wykonano też pełną regresję całego projektu.
- Samo zgłoszenie rozszerzenia w Windows nie dowodzi, że każdy z 48 formatów
  faktycznie dekoduje się na danej instalacji.
- Wcześniejsza paczka testowa SET-001 v2 nie zawierała końcowego etapu. Zastąpił
  ją wspólny instalator `AMC-Test-2026-10-27-0057.exe`, który zawiera kartę
  **Aktualizacje**, nazwę **Dostępność i komunikaty** i kartę **Klawiatura**.
- Nowa kolejność kart i sekcji jest obecnie zmianą źródłową z testami
  kontraktowymi. Nie jest jeszcze dowodem działania z NVDA w zainstalowanej
  aplikacji; ręczny odbiór powinien sprawdzić przełączanie kart, Tab i
  Shift+Tab na kartach Radio oraz Podcasty.

## Wersja i commit

- commit funkcjonalny SET-001 i KEY-001: `d43d24b566a5c31fd1f862fa92ae65b7adbc8c84`;
- commit źródłowy wydania: `4cb57da5a6bda7bb82696ff426c00f2b9f655704`;
- wersja techniczna: `0.1.0-alpha.435`; wersja widoczna dla użytkownika:
  `27.10.2026, 00:57`;
- czysty manifest paczki: identyfikator `73ec48a6`, profil `test`, 2136 plików,
  `sourceDirty=false`;
- instalator: `AMC-Test-2026-10-27-0057.exe`, 134290649 bajtów, SHA-256
  `a09a957923afd708a78445d4bb9574181d222ce9ee557c776118bb5cb86a2329`;
- wydanie GitHub: `test-2026.10.27.0057`, opublikowane i odczytowo
  zweryfikowane;
- lokalna aktualizacja przez oficjalny aktualizator: wykonana i zweryfikowana;
  ręczny test kart Ustawień z NVDA pozostaje.
- korekta układu kart z 27.10.2026: zmiana robocza po powyższym wydaniu,
  obecnie bez osobnego commitu, builda, instalatora, instalacji i publikacji;
  zostanie objęta następnym wspólnym wydaniem.

## Korekta karty Aktualizacje — 29.10.2026

### Materiał do dokumentacji użytkownika

Karta `Aktualizacje` ma teraz osobny, domyślnie włączony przełącznik
`Automatycznie sprawdzaj dostępność nowej wersji AMC po uruchomieniu programu`.
Jego pomoc wyjaśnia, że AMC jedynie pokaże opis i zaproponuje pobranie; bez
zgody niczego nie pobiera ani nie instaluje. Ustawienia automatyki całego AMC
pozostają oddzielone od dotychczasowych ustawień FFmpeg i yt-dlp.

### Uzasadnienie i decyzje

Pole ma jawną nazwę dostępności i jest zwykłym przełącznikiem w kolejności Tab.
Wartość modelu jest logiczna i nie może zostać odczytana jako nazwa klasy,
identyfikator ani techniczna wartość. Nie dodano pozornego wyboru kanału.

### Testy i wynik

Celowany zestaw obejmujący Ustawienia, zapis stanu, start, i18n i aktualizator:
**119 zaliczonych, 1 kontrolowanie pominięty**. Test ręczny NVDA pozostaje.

### Znane ograniczenia

Kod został objęty lokalnym kandydatem AMC Public. Początkowy fokus, Tab,
Shift+Tab, zapis, anulowanie i ponowne otwarcie nadal wymagają odbioru NVDA.
Kandydata nie zainstalowano ani nie opublikowano.

### Wersja i commit

Commit bieżącej korekty: `9684e757`. Kandydat użytkowy:
`AMC Public — 29.10.2026, 19:24`, paczka `33e68ad6`. Kontrolowany start i
instalator są zaliczone. Instalacji ani publikacji nie wykonano.


## Korekta użytkownika — 02.11.2026: wybór programu domyślnego

### Materiał do dokumentacji użytkownika

W Ustawieniach (`Ctrl+,`) na karcie **Integracja z Windows** pola grup
udostępniają AMC jako możliwy program do otwierania zaznaczonych formatów.
Nie zmieniają programu domyślnego. Przycisk **Wybierz domyślne typy plików
dla AMC w Windows** prowadzi, jeśli system to obsługuje, bezpośrednio do
strony AMC w systemowych ustawieniach aplikacji domyślnych. Tam użytkownik
zatwierdza potrzebne formaty. Grupa wideo nosi nazwę **Pliki wideo**.

### Uzasadnienie i decyzje

Użytkownik poprosił o pola wyboru w AMC, które trwale przypisują AMC do
rozszerzeń, jak w starszych wersjach foobar2000. Niniejsza korekta zastępuje
wcześniejszy opis sugerujący, że pola grup mogą ustawić domyślną aplikację.
Windows 10/11 wymaga zmiany domyślności przez interfejs systemowy. Rejestracja
formatów i bezpośredni odsyłacz do strony AMC są obsługiwanym zakresem.
Nie używamy nieobsługiwanej modyfikacji danych wyboru użytkownika w rejestrze.
Źródła: Microsoft Learn, `windows-integration/default-apps-platform` oraz
`launch/launch-default-apps-settings`.

### Testy i wynik

Zmiana dotyczy nazw, opisu dostępności i tłumaczeń. Składnia Python: zaliczona.
Celowane testy karty Ustawienia, klienta integracji i profili instalatora:
40 zaliczonych (`python -B -m pytest -q -p no:cacheprovider` z `PYTHONPATH=wxlite`).
Ręczny test z NVDA i faktyczne otwarcie strony AMC w Windows pozostają do odbioru.

### Znane ograniczenia

Pola grup w AMC nie mogą samodzielnie ustawić programu domyślnego. Na starszym
Windows przycisk może otworzyć ogólną stronę aplikacji domyślnych.

### Wersja i commit

Źródła i celowane testy: commit `9266fd4f` na bieżącej gałęzi AMC Test.
Nie wykonano nowej kompilacji, instalacji ani publikacji. Wcześniejsze
wydanie `0.1.0-alpha.466` nie zawiera tej korekty.
