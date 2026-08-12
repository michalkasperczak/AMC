# Dostępny kontroler multimedialny — koncepcja projektu

Wersja dokumentu: 0.3, szkic do dyskusji

Data: 7 sierpnia 2026 r.

## 1. Cel projektu

Projekt zakłada stworzenie dostępnej aplikacji działającej przede wszystkim w tle. Jej głównym sposobem obsługi będzie konfigurowalny prefiks klawiaturowy, po którym użytkownik wybiera sesję albo wykonuje polecenie w bieżącej sesji.

Pierwsze planowane integracje:

- TIDAL;
- Apple Music;
- WiiM.

Aplikacja nie będzie w pierwszej wersji zastępować Foobara2000 ani Free Radia. Możliwość dołączenia kolejnych modułów ma pozostać otwarta.

Program nie będzie wtyczką NVDA. Pierwsza wersja powstaje wyłącznie dla Windows i ma działać z NVDA, JAWS-em oraz Narratorem. Architektura nie powinna niepotrzebnie blokować późniejszego wydania dla macOS, ale skróty macOS, VoiceOver, Siri i pozostałe elementy platformy Apple zostaną zaprojektowane dopiero po dopracowaniu wersji windowsowej. Bezpośrednia integracja z konkretnym czytnikiem ekranu może być kiedyś dodatkiem, ale nie podstawą działania.

## 2. Model hybrydowy

Aplikacja będzie połączeniem dwóch sposobów obsługi:

1. **Warstwa prefiksowa** do szybkich operacji wykonywanych w tle.
2. **Proste dostępne okna** do przeglądania wyników wyszukiwania, albumów, playlist, biblioteki i ustawień.

Nie planujemy na początku rozbudowanego, stale otwartego interfejsu. Złożone dane muszą jednak być prezentowane w normalnym oknie, ponieważ nie da się wygodnie przejrzeć kilkudziesięciu albumów lub playlist samymi komunikatami głosowymi.

Każda operacja aplikacji będzie wewnętrznym poleceniem, które można wywołać:

- w warstwie prefiksowej;
- lokalnym skrótem w oknie;
- z menu kontekstowego;
- w przyszłości przez opcjonalną wtyczkę lub zewnętrzny interfejs sterowania.

## 3. Prefiks aplikacji

### 3.1. Zasada działania

1. Użytkownik naciska globalny prefiks.
2. Aplikacja przechodzi na krótko do warstwy poleceń.
3. Następny klawisz lub kombinacja zostaje zinterpretowana przez aplikację.
4. `Escape` anuluje warstwę.
5. Warstwa wygasa po konfigurowalnym czasie, proponowane domyślnie 3 sekundy.

Ponowne naciśnięcie samego prefiksu może informować o bieżącej sesji, np. „TIDAL”. To zachowanie również powinno być konfigurowalne.

### 3.2. Wybór prefiksu

Prefiks pozostaje konfigurowalny. Od prototypu `0.1.0-alpha.5` wartością domyślną dla Windows jest `Ctrl+Alt+Windows+F12`, pomyślnie zarejestrowany na komputerze testowym. Zastąpił kombinację z klawiszem Enter, która nakładała się na systemowy skrót Narratora. Rozważane możliwości:

| Kandydat | Zalety | Ryzyko |
| --- | --- | --- |
| `Ctrl+Alt+Windows+F12` | rozpoznawalny i bez konfliktu na komputerze testowym | cztery klawisze; wymaga testu rejestracji |
| `Ctrl+Alt+Spacja` | stosunkowo krótki | możliwy konflikt z innymi aplikacjami lub metodami wprowadzania |
| `Ctrl+Shift+Windows+Spacja` | wyraźnie odróżnia aplikację od Free Radia | długi; kombinacje z Windows mogą być zarezerwowane przez system |
| `Ctrl+Windows+\` | krótki i charakterystyczny | kombinacje z Windows wymagają testu rejestracji |
| `Ctrl+Shift+Windows+P` | łatwe skojarzenie z prefiksem | cztery klawisze |
| `F13–F24` | bardzo małe ryzyko konfliktu | wymaga klawiatury programowalnej lub mapowania dodatkowego klawisza |

Ustawienia powinny zawierać funkcję „Sprawdź prefiks”. Program zapisze kombinację dopiero po udanej rejestracji i ostrzeże, jeżeli skrót jest już zajęty.

Microsoft zastrzega, że skróty zawierające klawisz Windows są przeznaczone dla systemu operacyjnego, dlatego nie wolno zakładać, że każda taka kombinacja będzie dostępna. Program musi sprawdzać ją na konkretnym komputerze: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey>.

## 4. Sesje

### 4.1. Wybieranie sesji

Po prefiksie kombinacje `Ctrl+cyfra` będą bezpośrednio przełączać usługę. Kolejność usług jest konfigurowalna. Cyfry nie zależą od języka i nie zajmują liter potrzebnych do poleceń.

Proponowane ustawienia domyślne:

| Polecenie po prefiksie | Sesja lub działanie |
| --- | --- |
| `Ctrl+1` | TIDAL |
| `Ctrl+2` | Apple Music |
| `Ctrl+3` | WiiM |
| `Ctrl+4–9` | kolejne usługi lub urządzenia |
| `Ctrl+0` | lista wszystkich sesji |
| `Page Up` | poprzednia dostępna sesja |
| `Page Down` | następna dostępna sesja |

Po zmianie aplikacja przekazuje krótki komunikat, np. „3, WiiM”. Jeśli miejsce nie zostało przypisane, mówi „Sesja 4 nieprzypisana”. Bezpośrednie skróty `Ctrl+litera`, np. `Ctrl+W`, mogą zostać ustawione przez użytkownika jako dodatkowe aliasy, ale nie są potrzebne w profilu domyślnym.

### 4.2. Zapamiętywanie sesji

- Wybrana sesja pozostaje aktywna dla następnych poleceń.
- Nie trzeba wskazywać usługi za każdym razem.
- Po przełączeniu sesji warstwa może pozostać aktywna jeszcze przez około 2 sekundy, aby można było od razu wykonać funkcję.

Przykłady:

- prefiks, `Ctrl+1` — zmień sesję na TIDAL;
- prefiks, `S` — wyszukaj w aktualnej sesji, czyli w TIDAL-u;
- prefiks, `Ctrl+3`, `Spacja` — zmień sesję na WiiM i włącz albo zatrzymaj odtwarzanie;
- prefiks, `Ctrl+2`, `P` — zmień sesję na Apple Music i otwórz playlisty.

Do rozstrzygnięcia pozostaje, czy ostatnia sesja ma być pamiętana po ponownym uruchomieniu programu. Bezpieczniejszy wariant to przywrócenie sesji i ogłoszenie jej przy pierwszym użyciu prefiksu.

## 5. Polecenia literowe

### 5.1. Ogólna reguła

- `litera` otwiera widok, listę albo kategorię;
- `Shift+litera` wykonuje powiązaną czynność dotyczącą aktualnego lub zaznaczonego elementu;
- nie każda litera musi od razu mieć wersję z Shiftem;
- wszystkie przypisania można zmienić w ustawieniach.

Wersja z Shiftem nie powinna wykonywać nieodwracalnej operacji bez potwierdzenia.

Program może zmieniać język interfejsu i komunikatów, ale nie powinien automatycznie zmieniać przypisań klawiszy. Domyślny zestaw będzie wspólny dla wszystkich języków, np. `F` pozostaje skrótem Ulubionych również w wersji polskiej. Chroni to pamięć mięśniową i ułatwia korzystanie z dokumentacji w różnych językach. Użytkownik nadal może zbudować własny profil.

### 5.2. Wstępna mapa

| Klawisz po prefiksie | Funkcja | `Shift+klawisz` | Funkcja powiązana |
| --- | --- | --- | --- |
| `F` | Ulubione / Favorites | `Shift+F` | przełącz stan: dodaj do ulubionych albo usuń z ulubionych |
| `P` | Playlisty | `Shift+P` | otwórz wybór playlist i zmień przynależność elementu |
| `S` | Wyszukiwanie w bieżącej sesji | `Shift+S` | wyszukiwanie we wszystkich obsługiwanych usługach, przyszłościowo |
| `L` | Biblioteka | `Shift+L` | dodaj element do biblioteki albo go z niej usuń |
| `Q` | Kolejka | `Shift+Q` | dodaj element do kolejki |
| `A` | Albumy | `Shift+A` | dodaj wskazany album do biblioteki, jeśli usługa to umożliwia |
| `R` | Radio utworu, wykonawcy lub rekomendacje | `Shift+R` | uruchom radio na podstawie wskazanego elementu |
| `M` | Miksy i rekomendacje | `Shift+M` | funkcja do ustalenia; na razie nieprzypisana |
| `H` | Historia | `Shift+H` | na razie nieprzypisana |
| `N` | Teraz odtwarzane | `Shift+N` | otwórz aktualny element w oficjalnej aplikacji usługi |
| `I` | Informacje o elemencie | `Shift+I` | rozszerzone informacje, np. wykonawcy i autorzy |
| `O` | Wyjścia i urządzenia | `Shift+O` | otwórz wybór wyjścia dla bieżącej sesji |
| `D` | Pobrane / offline | `Shift+D` | pobierz wewnątrz usługi, tylko jeśli istnieje oficjalne wsparcie |
| `?` lub `F1` | Pomoc bieżącej warstwy | — | — |

`Shift+F` działa jako przełącznik, jeśli adapter usługi potrafi pewnie odczytać aktualny stan. Program mówi odpowiednio „Dodano do ulubionych” albo „Usunięto z ulubionych”. Jeśli stan jest nieznany, aplikacja nie może zgadywać i powinna otworzyć menu z jednoznacznymi czynnościami.

Funkcje pobierania nie należą do podstawowej wersji. `D` i powiązane kombinacje pozostają rezerwacją projektu do czasu sprawdzenia możliwości i zasad konkretnej usługi.

### 5.3. Informacje o czasie

Informacje o czasie pozostają trzema osobnymi poleceniami, żeby użytkownik nie musiał słuchać niepotrzebnych danych:

| Polecenie po prefiksie | Informacja domyślna |
| --- | --- |
| `Ctrl+E` | czas, który upłynął, np. „1:23” |
| `Ctrl+R` | czas pozostały, np. „2:57” |
| `Ctrl+T` | całkowity czas elementu, np. „4:20” |

Są to polecenia informacyjne, dlatego używają Ctrl i nie kolidują z literą `R` uruchamiającą widok radia. Domyślnie wypowiadana jest tylko wartość. Każde ponowne wywołanie podaje wartość aktualną; program nie powinien co sekundę przerywać mową. Monitor brajlowski i dostępne pole stanu mogą pokazywać aktualizowany czas bez automatycznej wypowiedzi.

## 6. Polecenia transportowe i strzałki

Propozycja dla warstwy prefiksowej:

| Polecenie po prefiksie | Działanie |
| --- | --- |
| `Spacja` | odtwórz / pauza |
| `Strzałka w lewo` | cofnij o 10 sekund |
| `Strzałka w prawo` | przewiń o 10 sekund |
| `Strzałka w górę` | zwiększ głośność, domyślnie o 5% |
| `Strzałka w dół` | zmniejsz głośność, domyślnie o 5% |
| `Ctrl+Strzałka w lewo` | poprzedni utwór lub element |
| `Ctrl+Strzałka w prawo` | następny utwór lub element |
| `Shift+Strzałka w lewo` | cofnij o 60 sekund |
| `Shift+Strzałka w prawo` | przewiń o 60 sekund |
| `Shift+Strzałka w górę` | zwiększ głośność o 1% |
| `Shift+Strzałka w dół` | zmniejsz głośność o 1% |
| `Ctrl+Home` | początek utworu |
| `Ctrl+End` | koniec utworu, jeśli usługa pozwala; w przeciwnym razie brak działania |
| `Page Up` | poprzednia sesja |
| `Page Down` | następna sesja |

Polecenia nieobsługiwane przez daną sesję nie mogą być po cichu ignorowane. Program powinien powiedzieć np. „Przewijanie niedostępne dla WiiM”.

Strzałki po prefiksie służą wyłącznie do sterowania globalnego. W aktywnym oknie listy zwykłe strzałki nawigują po elementach bez używania prefiksu.

## 7. Okno przeglądania

### 7.1. Lista zamiast drzewa

Podstawowym widokiem będzie zwykła dostępna lista lub tabela, nie drzewo rozwijane strzałkami.

Powody:

- Enter jest jednoznaczny i przewidywalny;
- czytnik ekranu nie musi ogłaszać wielu poziomów rozwinięcia;
- łatwiej zachować pozycję po przeładowaniu;
- ten sam komponent może pokazać wykonawców, albumy, utwory, playlisty i urządzenia.

Kolejność informacji w dostępnej etykiecie elementu jest konfigurowalna. Użytkownik może ustawić na przykład tytuł, wykonawcę, czas trwania i typ elementu albo wykonawcę przed tytułem. Pola bez wartości są pomijane. Ta sama kolejność obowiązuje na listach i w komunikatach o bieżącym elemencie; pozostaje niezależna od profilu klawiatury.

### 7.2. Nawigacja

| Klawisz w oknie | Działanie |
| --- | --- |
| `Strzałki w górę/dół` | poprzedni / następny element |
| `Home`, `End` | pierwszy / ostatni element |
| `Page Up`, `Page Down` | przewijanie listy stronami |
| wpisywanie liter | przejście do elementu zaczynającego się od podanego ciągu; powtarzanie litery przechodzi między dopasowaniami |
| `Enter` | otwórz wykonawcę, album lub playlistę; na utworze wykonaj domyślną czynność |
| `Ctrl+Enter` | odtwórz zaznaczenie teraz |
| `Shift+Enter` | dodaj zaznaczenie do kolejki |
| `Ctrl+Shift+Enter` | odtwórz jako następne |
| `Backspace` lub `Delete` | usuń element z bieżącej playlisty, kolejki, ulubionych albo biblioteki, jeśli działanie jest jednoznaczne |
| `Alt+Strzałka w lewo` | poprzedni widok |
| `Alt+Strzałka w prawo` | następny widok, jeśli istnieje |
| `Alt+Enter` | informacje o elemencie |
| `Ctrl+Shift+O` | otwórz element w oficjalnej aplikacji usługi |
| `Klawisz aplikacji` lub `Shift+F10` | menu kontekstowe |

Enter wykonuje działanie podstawowe zależne od rodzaju elementu: odtwarza utwór, stację lub preset, natomiast na albumie, playliście albo wykonawcy otwiera zawartość. `Ctrl+Enter` odtwarza natychmiast również cały album lub playlistę. `Alt+Enter`, zgodnie z typowym zachowaniem menedżerów plików, pozostaje informacją lub właściwościami elementu; nie służy do otwierania zewnętrznej aplikacji.

### 7.3. Przeładowywanie

- Stara lista pozostaje widoczna do czasu otrzymania nowych danych.
- Program ogłasza krótko „Ładowanie”, ale nie powtarza komunikatu dla każdej części danych.
- Po zakończeniu informuje np. „24 utwory”.
- Po otwarciu albumu lub playlisty podaje zwięzłe podsumowanie, np. „Album: Abbey Road, The Beatles. 17 utworów, 47 minut 23 sekundy”.
- Po powrocie do wcześniejszego widoku przywraca poprzednio zaznaczony element.
- Odświeżenie nie powinno bez potrzeby przenosić fokusu na początek listy.

## 8. Wybór playlisty

`Shift+P` w warstwie albo lokalne polecenie zarządzania playlistami otwiera niewielkie modalne okno:

- pole filtrowania;
- lista wszystkich playlist bieżącej usługi z informacją „zawiera” albo „nie zawiera”;
- ostatnio używane playlisty na początku, opcjonalnie;
- Spacja przełącza przynależność elementu do wskazanej playlisty;
- Enter zatwierdza wszystkie zmiany;
- `Ctrl+N` tworzy nową playlistę;
- Escape anuluje;
- po wykonaniu operacji fokus wraca dokładnie do wcześniejszego elementu.

W ustawieniach można później wskazać playlistę domyślną, np. „Do odsłuchu”. Osobne polecenie może dodawać do niej bez otwierania okna. Zwykłe `Shift+P` powinno jednak domyślnie dawać wybór.

## 9. Skróty lokalne w oknie

Skróty lokalne są niezależne od warstwy prefiksowej, ale wywołują te same wewnętrzne polecenia.

Wstępna propozycja:

| Skrót lokalny | Działanie |
| --- | --- |
| `Ctrl+F` | znajdź lub filtruj bieżącą listę |
| `Ctrl+C` | kopiuj nazwę wybranego elementu |
| `Ctrl+Shift+C` | kopiuj łącze do elementu w usłudze |
| `Ctrl+Shift+F` | dodaj do ulubionych albo usuń z ulubionych |
| `Ctrl+Shift+P` | otwórz wybór playlist i zmień przynależność |
| `Ctrl+Shift+Q` | dodaj do kolejki |
| `Ctrl+Shift+L` | dodaj do biblioteki |
| `Ctrl+D` | pobierz offline wewnątrz usługi, jeśli obsługiwane |
| `Ctrl+Shift+D` | pobierz do pliku lokalnego; funkcja eksperymentalna, domyślnie wyłączona |
| `Backspace` lub `Delete` | usuń z bieżącej playlisty, kolejki, ulubionych lub biblioteki; z potwierdzeniem albo możliwością cofnięcia |
| `Ctrl+Shift+O` | otwórz element w oficjalnej aplikacji usługi |
| `F2` | zmień nazwę playlisty, jeśli obsługiwane |
| `Ctrl+A` | zaznacz wszystkie elementy, jeśli widok pozwala |

Każdy skrót lokalny jest zmienny. Polecenia pobierania nie powinny być aktywne, dopóki odpowiedni moduł nie zostanie świadomie włączony.

## 10. Menu kontekstowe

Menu kontekstowe jest obowiązkowe. Powinno pokazywać tylko funkcje dostępne dla rodzaju elementu i bieżącej usługi, ale zachowywać stałą, przewidywalną kolejność:

1. Odtwórz teraz.
2. Odtwórz jako następne.
3. Dodaj do kolejki.
4. Pokaż zawartość albumu, playlisty albo wykonawcy — jeśli dotyczy.
5. Dodaj do ulubionych albo usuń z ulubionych.
6. Dodaj do playlisty.
7. Dodaj do biblioteki.
8. Udostępnij lub kopiuj łącze.
9. Pobierz — tylko jeśli dostępne i włączone.
10. Usuń z bieżącego widoku — jeśli dotyczy.
11. Informacje.
12. Otwórz w oficjalnej aplikacji usługi.

Przy każdej pozycji menu powinien być wyświetlony jej aktualny skrót. Czynności destrukcyjne należy oddzielić separatorem.

## 11. Ustawienia klawiatury

Ustawienia muszą umożliwiać zmianę wszystkiego:

- globalnego prefiksu;
- czasu aktywności warstwy;
- skrótów wyboru sesji;
- liter i par `litera` / `Shift+litera`;
- poleceń transportowych;
- skrótów lokalnych w oknie;
- poleceń bez przypisanego skrótu;
- zachowania klawisza Enter dla każdego typu elementu;
- domyślnej playlisty;
- pamiętania ostatniej sesji.

Język interfejsu jest ustawieniem niezależnym od profilu skrótów. Zmiana języka nie może samodzielnie przemeblować klawiatury. W pierwszej wersji powstaje wyłącznie profil Windows; profil macOS zostanie zaprojektowany razem z późniejszym wydaniem dla tej platformy.

Program może przechowywać wiele profili klawiatury. Użytkownik wybiera profil aktywny, tworzy edytowalną kopię profilu domyślnego, nadaje jej nazwę i przełącza profile w ustawieniach. Profil wbudowany pozostaje chroniony, aby zawsze istniał pewny punkt powrotu.

Wymagane funkcje edytora skrótów:

- przechwycenie nowej kombinacji;
- wykrywanie duplikatów wewnątrz programu;
- próba wykrycia konfliktu globalnego;
- możliwość całkowitego wyłączenia polecenia;
- przywracanie ustawień domyślnych dla pojedynczego polecenia lub całej sekcji;
- osobny import i eksport pojedynczej mapy klawiszy;
- osobny import i eksport konfiguracji bez map klawiatury;
- import i eksport pełnej kopii zawierającej ustawienia, wszystkie profile, kolejność sesji i szablony komunikatów.

Pełna kopia ani pozostałe eksporty nie mogą zawierać haseł, tokenów i danych logowania. Proponowane rozszerzenia to `.amckeys.json`, `.amcsettings.json` oraz `.amcbackup.json`.

Okno ustawień powinno zawierać kategorie: Ogólne, Język, Prefiks i polecenia, Sesje i usługi, Odtwarzanie, Listy, Komunikaty i dostępność oraz Zaawansowane. `Ctrl+F` wyszukuje ustawienie po nazwie.

## 12. Komunikaty, mowa i brajl

Każdy komunikat powinien być jednocześnie:

- zapisany w widocznym, dostępnym polu stanu;
- wysłany jako systemowe powiadomienie dostępności;
- dostępny dla mowy i monitora brajlowskiego za pośrednictwem czytnika ekranu.

Podstawą pierwszej wersji jest UI Automation w Windows. Program nie może wymagać NVDA do działania. Accessibility API w macOS zostanie opracowane dopiero przy rozpoczęciu prac nad wydaniem dla Maca.

Domyślne komunikaty mają być krótkie. W pierwszej wersji nie wprowadzamy osobnych profili „krótki”, „normalny” i „szczegółowy”. Zamiast nich użytkownik może edytować szablon każdego komunikatu, wyłączyć go albo przywrócić ustawienie domyślne.

Ustawienia komunikatów:

- osobne włączanie komunikatów o sesji, odtwarzaniu, głośności, ładowaniu i błędach;
- edycja tekstu z polami takimi jak `{service}`, `{title}`, `{elapsed}`, `{remaining}` i `{total}`;
- przycisk odsłuchania przykładu i przywrócenia krótkiego szablonu domyślnego;
- opcjonalne dźwięki zamiast niektórych wypowiedzi;
- ograniczanie powtarzających się komunikatów;
- możliwość ponownego odczytania ostatniego komunikatu;
- opcja używania bezpośredniego modułu NVDA w przyszłości, ale tylko jako dodatku.

Przykłady komunikatów krótkich:

- „TIDAL”.
- „Dodano do ulubionych”.
- „Już w ulubionych”.
- „Dodano do: Do odsłuchu”.
- „Głośność WiiM: 35%”.
- „Ładowanie albumu”.
- „24 utwory”.
- „Polecenie niedostępne dla tej sesji”.
- dla skrótów czasu domyślnie tylko „1:23”, „2:57” albo „4:20”.

Przy operacjach globalnych warto podawać usługę, jeżeli istnieje ryzyko pomyłki, np. „Apple Music: dodano do playlisty”.

## 13. Architektura funkcjonalna

Program powinien rozdzielać:

- **silnik poleceń** — jednolite identyfikatory funkcji;
- **silnik prefiksu** — przechwytywanie sekwencji i timeout;
- **menedżer sesji** — TIDAL, Apple Music, WiiM;
- **adaptery usług** — tłumaczenie wspólnych poleceń na API konkretnej usługi;
- **okno przeglądania** — wspólna lista dla wszystkich usług;
- **system komunikatów dostępności**;
- **ustawienia i profile skrótów**;
- **system aktualizacji aplikacji i adapterów usług**.

Adapter usługi deklaruje swoje możliwości. Dzięki temu aplikacja wie, że np. WiiM obsługuje głośność i presety, ale nie obsługuje dodawania albumu do biblioteki.

Docelowa dystrybucja powinna być samowystarczalna i zawierać wymagane środowisko uruchomieniowe. Aktualizator działa dla bieżącego użytkownika bez uprawnień administratora, sprawdza aktualizacje w tle, pobiera wyłącznie podpisane pakiety, weryfikuje ich sumy SHA-256, instaluje atomowo i pozwala wrócić do poprzedniej wersji. Aktualizacja nie może nadpisywać profili, konfiguracji ani danych logowania, kraść fokusu czy przerywać odtwarzania. Użytkownik wybiera kanał stabilny albo beta oraz może wyłączyć automatyczne sprawdzanie, pobieranie lub instalację.

## 14. Zakres pierwszej wersji

Pierwszy prototyp powinien zawierać:

1. Rejestrowany i zmienny prefiks.
2. Warstwę poleceń z timeoutem i anulowaniem.
3. Trzy przykładowe sesje, początkowo nawet jako moduły demonstracyjne.
4. Przełączanie sesji przez `Ctrl+1–9`, lista sesji pod `Ctrl+0` oraz zmiana kolejna przez `Page Up` i `Page Down`.
5. Konfigurowalne mapowanie kilku poleceń literowych.
6. Komunikaty przez systemową dostępność i widoczne pole stanu.
7. Jedno wspólne okno listy z Enterem, powrotem i menu kontekstowym.
8. Edytor skrótów lub przynajmniej plik konfiguracyjny, zanim powstanie pełne Settings.
9. Krótkie, edytowalne szablony komunikatów, w tym osobne polecenia czasu upłyniętego, pozostałego i całkowitego.
10. Zmienne profile klawiatury z chronionym profilem domyślnym.
11. Trzy rodzaje importu i eksportu: mapa klawiszy, konfiguracja oraz pełna kopia.
12. Interfejs systemu automatycznych aktualizacji, początkowo bez serwera dystrybucyjnego.

Pierwszy prototyp i pierwsze działające wydanie dotyczą wyłącznie Windows. Wersja dla macOS, VoiceOver i ewentualna obsługa Siri są etapem późniejszym.

Następna kolejność integracji:

1. WiiM jako prosty test komend, głośności i presetów.
2. TIDAL: logowanie, wyszukiwanie, albumy, ulubione i playlisty.
3. Apple Music: przygotowany adapter i dokumentacja przekazania osobie utrzymującej konto Apple Developer.

## 15. Otwarte decyzje

1. Ostateczny prefiks domyślny.
2. Czas wygaśnięcia warstwy.
3. Czy aplikacja pamięta sesję po ponownym uruchomieniu.
4. Czy istnieje od początku playlista „Do odsłuchu”.
5. Które komunikaty mają być mówione, a które sygnalizowane dźwiękiem.
6. Czy pobieranie lokalne w ogóle należy do projektu.
7. Nazwa aplikacji.

## 16. Zasada dalszej pracy

Dokument jest projektem, a nie zamkniętą specyfikacją. Każda zatwierdzona zmiana powinna być równolegle naniesiona do wersji polskiej i angielskiej. Kod, ustawienia i dokumentacja mają używać stabilnych identyfikatorów poleceń niezależnych od wyświetlanego języka i wybranych skrótów.
