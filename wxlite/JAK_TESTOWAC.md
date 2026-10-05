# AMC Python — uruchamianie i sprawdzanie bieżącego przyrostu

To rozwijany równoległy interfejs pełnegoAMC. Nie jest jeszcze zamiennikiem wszystkich funkcji programu. Katalog i nazwa uruchamiacza `wxlite` są historyczne; nie oznaczają decyzji o ograniczeniu docelowego zakresu do dwóch sesji.

## Co zostało wykonane

Bieżący odbiór mechanizmu wszystkich list: 562 testy, 0 błędów, 0 pominięć. Zwykłe listy Windows, przyrostowe zmiany w obrębie widoku, zachowanie wyboru po ID, puste zakładki i pojedynczy odczyt po zmianie dużego widoku sprawdzono w prywatnym stagingu z żywym NVDA. Końcowe kwity: `native-w02-valid-retest-after422/parent-final/`. To nie jest odbiór wszystkich funkcji pełnego AMC ani innego czytnika.

Odbiór KOŃCOWY żywej kolejki (po scaleniu): **588 testów Pythona, 0 błędów, 0 pominięć** i **6/6 zestawów C#**. Z żywym NVDA zmierzono na nowym hoście (DLL `6FFF6BD0…`): Ctrl+Q na starcie oddaje zapis, Enter uruchamia kolejkę w porządku **B→A→C (nie alfabet)**, a pozostawiona OTWARTA lista sama usuwa zużyty wiersz po naturalnym przejściu i nie kradnie wyboru grającej pozycji. Po ostatnim utworze lista ma 0 pozycji (potwierdzone też kontrolką Win32 `LVM_GETITEMCOUNT=0`), czytnik mówi „Kolejka odtwarzania, pusto", a ponowny Ctrl+Q NIE wraca do starego zapisu. Transport na przebudowanym hoście: 22/22. Kwity: `live-queue-after-native-lists/final-integrated/`. Trwały pisarz profilu i dalszy pełny port pozostają poza tym etapem.

## Cisza podczas wpisywania filtra — korekta po próbie użytkownika

Wpisywanie i usuwanie znaków nadal zawęża listę, ale aplikacja nie mówi
„Wyniki filtrowania” ani liczby po każdym znaku, również przy zerowym wyniku.
Zostaje zwykłe echo klawiszy NVDA. Jawne wejście na pustą listę nadal wyjaśnia
brak wyników; Escape w polu nadal czyści filtr i wraca na listę.

Dwa testy kodujące dawną automatyczną zapowiedź odwrócono: najpierw26PASS/2FAIL,
po zmianie74PASS/0FAIL/0SKIP w filtrze i jego bezpośrednich zależnościach.
Odbiór żywej mowy do wykonania na scalonym kandydacie. Historia fraz, nowe
zachowanie Tab/Down i zmiana pamiętania filtra pozostają propozycjami.

## Filtr listy Ctrl+K: jak to sprawdzić na żywo

Pełny Python po tym odbiorze: **649 testów, 0 błędów, 0 pominięć**
(`search-filter-after-resume/final-recovery/full-python-final.log`).

Staging i narzędzia (nie publikacja, nie instalacja):

1. Skopiuj `wxlite/amc_wx_lite` do prywatnego stagingu i SPRAWDŹ `sha256sum` oraz
   `gui.__file__` z kwitu, zanim cokolwiek orzekniesz — stary staging potrafi mieć
   inny kod niż repo.
2. Uruchom `launch-final.ps1 <nazwa-kwitu>`; kwit ląduje w `kwit-<nazwa>/`
   (`probe.jsonl`, `snapshot.json`, `boot-stderr.txt`).
3. Gesty: `gestures-final.ps1 <pid> <plan>`. Plany: `A` pisanie+Enter, `B` Escape,
   `C` brak wyników, `D` Backspace w polu, `E` Down, `F` Radio, `H` Backspace NA LIŚCIE,
   `I` filtr per widok, `J` dwa niezależne zapytania.
4. Mowę czytaj z Podglądu mowy NVDA (`harness-nvda/read-speech-viewer.ps1`), zachowując
   bufor PRZED gestami i porównując różnicę. `Announcer.say` z sondy NIE jest dowodem mowy.

Czego wymagać od aparatury (sprawdzone tu na własnych błędach):

- Bramka pierwszego planu musi zwracać TYLKO `bool` i logować osobno; `Write-Output`
  przed `return` zwraca tablicę i `if (-not ...)` przepuszcza gest mimo braku fokusu.
- `$pid` jest w PowerShellu tylko do odczytu — użyj innej nazwy (`$fgPid`).
- Owijka sondy MUSI przekazywać `*args/**kwargs`, inaczej zgubi nowy parametr metody
  i zdarzenie w ogóle się nie zaloguje.
- Nie owijaj tej samej metody dwa razy w jednej instalacji sondy — drugie owinięcie
  nadpisuje pierwsze i zdarzenia milkną.

Ograniczenie zmierzone, nie naprawione: przy szybkim wpisywaniu przez `SendKeys`
pierwszy znak serii bywa gubiony przez samą aparaturę (plan J: wysłane `bo`,
dotarło `o`). To artefakt wysyłki gestów, nie filtra — wynik czytaj z `filter.text`
w kwicie, a nie z zamierzonego napisu.

## Domknięcie żywej kolejki

Nadrzędny kwit `live-queue-after-native-lists/parent-speech-full-profile/`: pełna kopia 11200→11203 pliki, 5000 zakładek zachowanych, naturalne B→A→C z C zaznaczonym jeszcze podczas B. Natywne/model 3→2→1→0, stały fokus, ponowne Ctrl+Q nadal puste. Mowa z rzeczywistego Podglądu mowy; `Announcer.say` sam w sobie jej nie dowodzi. Kod wykonawczy 38d931e, host DLL 6fff6bd0…, bez kolejnych zmian produkcyjnych w tym odbiorze.

## Trwałość kolejki: próbny profil i rzeczywisty restart

Aktualny kwit `queue-persistence-after-live/gui-integration-parent/REPORT.md`: pełny Python603/0/0, GUI odmowa zapisu ostatniego utworu z prawdziwą mową NVDA, ponowne uruchomienie niepustej kolejki, udany zapis do zera i kolejny restart z0wierszy. Własny profil `profile-persist-parent-final` zawiera pełne11203 rekordy; nie zastępuj go małą próbką.

Host: `C:\Users\Michal\AppData\Local\Temp\amc-wx-queue-persist-parent\complete-host-8abc180\amc_lite_host.exe`, DLL13f38747…. Przed użyciem sprawdź komplet wymaganych bibliotek z deps.json i manifest41plików w kwicie. Sam SHA własnejDLL NIE wykrywa brakujących NAudio/SoundTouch; poprzedni katalog był niekompletny mimo zgodnego SHA. Starych kwitów/profili nie nadpisuj, użyj następnego backupuSQLite. Zapisy są dozwolone tylko na prywatnej kopii, a późniejszy odbiór czasu opisano poniżej.

## Czas wznowienia — odbiór z rzeczywistym NVDA

Kod70c833d nad984c660/4ad9696. Aktualny host: `C:\Users\Michal\AppData\Local\Temp\amc-wx-resume-parent-final\host-policy-completion\amc_lite_host.exe`, DLL9cedd23b…,41plików,22wymagane DLL z deps.json i0braków. Kwity: `queue-resume-after-persistence/parent-final/REPORT.md`, `accepted-gui-cycles.json`, pełny `python-final-full.log` (608/0/0).

Na NOWEJ pełnej kopii, nie na zachowanych kwitach: Ctrl+Q, wybór niepierwszego utworu, Enter, Right+10s. Sprawdź rzeczywisty `loadedId` i rosnącą pozycję; `transport.status` nie ma pola `playing`. Zamknij własne okno podczas grania. Nowy GUI/host nie może grać sam: ma odtworzyć porządek, wybrać bieżące ID, a świadomy Enter wznowić od zapisanego czasu. Dla czytnika wstrzymaj materiał, poczekaj na aktualizację stanu, wywołaj Ctrl+E i odczytaj NOWY tekst Podglądu mowy przed zamknięciem okna. Sam `Announcer.say` nie dowodzi mowy. Wykonana próba:12,63s zapisane →12,75s start, NVDA „Minelo 12 s”.

Następnie wyłącz pamięć dla pozycji dziedziczącej ustawienie, pozostawiając stary niezerowy checkpoint. Nowy proces: `resumeSeconds=0`, start od początku (zmierzono0,97s, NVDA „Minelo 1 s”), checkpoint po zamknięciu0. Jawny Remember pozycji jest osobnym priorytetem. Dziedziczenie źródeł folderów sprawdzaj zgodnie z oryginałem: najbliższe Inherit prowadzi do sesji, nie do dalszego źródła.

C#7 zielonych zestawów, jedna pominięta próba ścieżki produkcyjnej naWSL. Adapter WPF skompilowany naWindows bez błędów/ostrzeżeń. To nie jest odbiór pełnego portu ani zgoda na współdzielony pisarz z działającym starym WPF.

## Przygotowane stanowisko prywatne

Odebrany układ naHermesie: `C:\Users\Michal\AppData\Local\Temp\amc-wx-425` z podkatalogami `app`, `runtime`, `host` i prywatną kopią `profile`. Jest to staging, nie numer publicznego wydania.

Uruchomienie bez obserwatora, z tym samym prawdziwym punktem wejścia:

```powershell
$stage = 'C:\Users\Michal\AppData\Local\Temp\amc-wx-425'
$env:APPDATA = "$stage\profile"
$env:LOCALAPPDATA = "$stage\profile"
$env:AMC_LITE_HOST = "$stage\host\amc_lite_host.exe"
Remove-Item Env:AMC_WX_FIXTURE -ErrorAction SilentlyContinue
& "$stage\runtime\python.exe" -m amc_wx_lite
```

Prywatny runtime ma `python314._pth` wskazujący `../app`. Embeddable Python może ignorować `PYTHONPATH`: zawsze sprawdzaj faktyczne `gui.__file__` i zgodność kopiowanych modułów. Stary moduł kandydata w innym katalogu nie testuje nowego kodu.

Bez przekierowania `APPDATA`/`LOCALAPPDATA` aplikacja domyślnie czyta profil właściwego użytkownikaWindows. Testy wykonuj na kopii. Zmienna `AMC_WX_FIXTURE` wybiera inny tryb prywatnej piaskownicy, dlatego nie zastępuje automatycznie prób lustrzanego, pełnego profilu.

## Odbiór klawiatury i NVDA

1. Ctrl+1: Biblioteka. Foldery mają pochodzić z bazy, nawet gdy zapisane dyski nie istnieją na stanowisku. Enter i Backspace zachowują logiczny powrót.
2. Alt+2: wszystkie lokalne pliki alfabetycznie. Porównaj liczność ze źródłem, nie z wpisaną na stałe liczbą.
3. Ctrl+U: Ulubione. Zachowaj właściwą kolejność i wybór.
4. Ctrl+P: playlisty; Enter do zawartości; Backspace na tę samą playlistę.
5. Ctrl+C: nazwa, pojedyncze potwierdzenie na każde świadome naciśnięcie. Ctrl+Shift+C: pełna ścieżka jakoTEKST, komunikat „Skopiowano pełną ścieżkę". Sprawdź rzeczywistą zawartość schowka zWindows (`Get-Clipboard`, czytaj bajty i dekoduj sam — `text=True` wywala się na polskich znakach), nie sam komunikat. Komunikat celowo NIE mówi o skopiowaniu pliku: obsługi formatuFileDrop nadal nie ma.
6. Ctrl+O: zwykły dialog folderu, nie Biblioteka. W zmierzonym dialogu pierwszyEnter wybiera wpisany folder, drugi zatwierdza; kontroluj rzeczywisty fokus i zamknięcie okna zamiast wysyłać gesty w ciemno.
7. Na dostępnym pliku:Enter, Spacja, Spacja. Stan przycisku Odtwórz/Wstrzymaj ma zgadzać się z działaniem; cisza czytnika i sam tekst statusu nie dowodzą komunikatuNVDA.
8. Ctrl+2: Radio z `radio.stations` wspólnego profilu. W trybie tylko do odczytu dodawanie/zmiana/usuwanie/import nadal mają odmówić uczciwie. Prywatna lista piaskownicy to odrębny tryb, nie zapis doAMC.

9. Menu (Alt): pasek ma być w całości klawiaturowy — Alt otwiera, strzałki chodzą, litery wybierają, Escape zamyka. Każda pozycja musi wykonać TO SAMO co jej skrót (ten sam `_dispatch`). W Radiu pozycje lokalnych Ulubionych/playlist mają być wyszarzane, nie ukryte. Nie dodawaj pozycji dla funkcji, których nie ma.

10. Widoki aktywności. Ctrl+H historia, Ctrl+Q kolejka, Ctrl+Shift+B zakładki ZAZNACZONEGO pliku. Trzy rzeczy do sprawdzenia poza samą licznością:
    - Etykieta kolejki odpowiada stanowi hosta. Przed pierwszą inicjalizacją jest to „Kolejka (zapisana)”; potem „Kolejka odtwarzania”, także „pusto” po ostatnim utworze. Pozostaw Ctrl+Q otwarte przy B→A→C: wiersze mają ubywać bez ponownego skrótu, a po ponownym wejściu nie mogą wrócić z zapisu. Wybrany nadal istniejący wiersz i widok pozostają; przejście pliku nie zmienia bieżącej stacji w oglądanej sesji Radia.
    - Ctrl+Shift+B, nie Ctrl+B. Ctrl+B to teraz OSOBNY, szerszy widok zbiorczy (`GetForDisplay` / `all_bookmark_rows`); Ctrl+Shift+B zostaje przy węższym `GetForItem` dla jednego pliku. Dwa skróty, dwa widoki — sprawdzaj, że się nie podmieniają.
    - Backspace z zakładek ma wrócić na TEN plik. Sprawdzaj ID zaznaczenia, nie numer wiersza — po powrocie do Wszystkich plików numer jest inny (zmierzono 2309 z 2476).
11. Fizyczny skok zakładki mierz pozycją z ŻYWEGO hosta (`transport.status`), nie oczekiwanym payloadem. `play.file` dostaje `positionSeconds` w jednym wywołaniu — nie wysyłaj seeka obok play, bo pozycja ginie przy starcie nowego pliku. Materiał do próby zrób SYNTETYCZNY: wygenerowany plik plus wstrzyknięty rekord w OSOBNEJ kopii pełnej bazy (nie w małej próbce, nie na pliku użytkownika). Przy wstrzykiwaniu zarejestruj zastępczą kolację `AMC_PL`, inaczej `INSERT` padnie na indeksie. Pozycję daj z częścią ułamkową (próba:83,456s) — kontrakt to dzielenie `/ 10_000_000`, nie `//`.

Przed audio/GUI sprawdź rezerwację pulpitu, obce procesy i ŻYWY stan nagrywania. Nie wyprowadzaj braku nagrywania z pustych pólJSON. Nie restartujNVDA ani nie wysyłaj klawiszy w obce okno. Podgląd mowy musi być otwarty podczas gestów. Zachowaj surowy przyrost i osobno skutek działania, nie tylko informację„niepusta mowa”.

Próba mowy/menu dla tego przyrostu: `amc_pomoc/wx-full-profile-after421/menu-and-speech-after422/proba_koncowa.py` (kwit `proba-koncowa.json`). Mierzy tylko to, co zmienione: mowę po zmianie widoku, oba kopiowania z odczytem schowka i obejście menu. Po próbie zamyka własne okno, sprawdza procesy i przywraca schowek.

Mowa natywnej listy: odebrano pojedynczy odczyt po Foldery → Wszystkie pliki oraz Playlisty → Wszystkie pliki, bez poprzedniej nazwy; strzałki czytają właściwy wiersz. Puste zakładki zgłaszają „pusto” i dostępną listę zamiast „nieznane”, a Backspace wraca po ID. Sama migracja na LC_REPORT tego nie dowodziła — końcowe dowody są w `native-w02-valid-retest-after422/parent-final/`. Pełna podmiana zawartości jest ograniczona do zmiany widoku; nie odtwarza HWND.

Próba widoków aktywności: `amc_pomoc/wx-full-profile-after421/activity-gui-after422/proba_koncowa_activity.py` (etapy A–C) oraz `dopiecie_de.py` (etapy D–F na tym samym żywym oknie). Nazwa gestu Backspace w mostku NVDA to `backspace`; `back` zwraca HTTP 500 i pierwszy przebieg na tym padł.

12. Zbiorczy widok zakładek (Ctrl+B) i powrót z odtwarzacza. Aparatura:
    `amc_pomoc/wx-full-profile-after421/all-bookmarks-gui-after422/`
    (`odbior_zakladek.py` = Ctrl+B / odmowa / pusto, `dopiecie_bcd2.py` =
    start i pauza i skok, `dopiecie_d.py` = powrót menu). Pułapki, na które
    już wpadnięto — nie powtarzaj ich:
    - **Ścieżka hosta w stagingu to `host\amc_lite_host.exe`**, nie
      `AccessibleMediaController.LiteHost.exe`. Przy złej nazwie okno
      pokazuje widok odtwarzacza przy `engine_pid=null` i `HostUnavailable` —
      wygląda jak zaliczony skok, a nic nie grało. Zawsze sprawdź
      `runtime.json` i dziennik wywołań, nie sam widok.
    - **Klasa klienta to `LiteHostClient`**, nie `HostClient`. Podgląd
      wywołań po złej nazwie milczy; podglądu bez pokrycia nie wolno czytać
      jako „zero wywołań".
    - **Pasek menu otwiera gołe `alt`.** `alt+w` wysłane z fokusem na
      przycisku odtwarzacza trafia w przycisk (NVDA zgłasza rolę 9
      „Odtwórz"), nie w menu. Potwierdź rolę 11 po `alt`, dopiero potem
      wysyłaj literę.
    - **Nie dosięgniesz pliku 40 strzałkami w 2476 wierszach.** Materiał
      próbny ma `is_favorite=1` — wejdź przez Ulubione (Ctrl+U, ~10 wierszy).
      Start i tak jest zwykłym Enterem z listy, charakter dowodu bez zmian.
    - **Kontekst bierz z sesji, nie z zaznaczenia**, i sprawdź go RÓWNIEŻ w
      pauzie. `status_text="Wstrzymano"` nie znaczy „nic nie jest bieżące".
    - **Powrót PLAYER→LIST wymaga `session_view` z obserwatora.**
      `library_view` nazywa tylko widok Biblioteki i nie rozróżnia
      odtwarzacza od listy, więc „Lista→Lista" nie jest dowodem powrotu.
    - **Pusty widok rób na POTWIERDZONYM innym pliku.** Najpierw zmień
      wybrany plik, potwierdź jego rzeczywiste ID i 0 zakładek tą samą
      funkcją, której używa widok, dopiero potem Ctrl+Shift+B i wymagaj
      `rows==0`. Nazwanie czegoś „pusto" przy `rows=1` nie jest dowodem.

## Dane i silnik

- SQLite: `mode=ro`, dziennikWAL uwzględniany. Błąd odczytu nie przechodzi automatycznie na `immutable=1`.
- ID są napisami. Dostępność i członkostwo w Bibliotece pochodzą z danychAMC, nie z `Path.exists` na innym komputerze.
- Jeden właścicielC# ma zapisywać wspólne dane; Python nie uruchamia drugiego harmonogramu. Implementacja wszystkich mutacji pozostaje kolejnym etapem.
- Foldery:AMC_PL. Kolekcje alfabetyczne: właściwy osobny tryb tytułu iOrdinalIgnoreCase ścieżki. Zgoda jednego korpusu nie dowodzi równoważności dwóch komparatorów.
- ProtokółJSON-lines, identyfikatorynapisy, limit64KiB po stronie żądania, wsady dzielone po bajtach. Nowe tryby kluczy wymagają nowegoLiteHost; klient odmawia niezgodnego trybu starego hosta.
- Host wykorzystuje istniejące silnikiAMC, nie własny dekoderPython. Sprawdzenie Roslynem lub kluczy w osobnej sondzie nie zastępuje kompilacji i uruchomienia rzeczywistego hostaWindows.

## Testy kodu

```bash
cd /home/michal/projekty/amc-wx-full-profile/wxlite
python3 run_tests.py
```

Wąskie przebiegi wybiera się fragmentem nazwy modułu, np. `library_view_navigation`, `library_views_through_source`, `announcement_reaches_screen_reader`. Testy protokołu z rzeczywistymC# wymagają zbudowanych `AccessibleMediaController.LiteHost.ProtocolTests`; pominięć nie przedstawiaj jako zaliczenia.

Przed publikacją potrzebny jest osobny, pełny odbiór niezmienianej paczki. Ten dokument nie potwierdza publicznego wydania ani instalacji na komputerze Michała.

## Znane ograniczenia

Wymienione wyżej przypadki mowy list są odebrane. Nie rozszerzaj tego na wszystkie możliwe scenariusze i czytniki. Całość nie ma jeszcze wszystkich funkcji zapisu, usług i ustawień oryginału. Kolejka żywego silnika DZIAŁA (Ctrl+Q oddaje `queue.status`, naturalne przejścia zmierzone), ale kolejność po `queue.set` NIE jest jeszcze zapisywana do profilu — trwały pisarz pozostaje poza zakresem. Tempo i wybór silników mają osobne wcześniejsze kwity — odbiór list ich nie powtarza.

Pełne dowody robocze: `amc_pomoc/wx-full-profile-after421/library-gui-after422/REPORT.md` i `parent-acceptance/`. Nie kopiuj prywatnych tytułów/profilu do repo ani paczki.

### Zwykła natywna lista: jak sprawdzać aktualizacje (po migracji z `LC_VIRTUAL`)

Lista jest zwykłą `wx.ListCtrl` (`LC_REPORT`) i trzyma teksty u siebie. Kontrolka
jest TRWAŁA — nie wolno jej niszczyć i odtwarzać przy zmianie danych (ta droga
była mierzona i odrzucona: zabierała mowę wybranego wiersza przy 2476 pozycjach).

Co sprawdzać:

- **Brak zbędnych aktualizacji.** Po samej strzałce, Ctrl+C, zmianie statusu czy
  ogłoszeniu widoku liczba operacji na liście ma być **0**. Mierz licznikiem
  operacji (`observer_native.py` owija `_apply_ops`/`sync_rows`), nie na oko.
- **Ten sam ID z nową treścią MA się odświeżyć.** Zmieniona nazwa, długość albo
  flaga Ulubione przy niezmienionym ID musi zaktualizować swoje pole. ID/kolejność
  i treść/stan porównywane są osobno — testuj oba warunki, nie jeden.
- **Jedna zmiana nie przepisuje listy.** Dodanie, usunięcie albo przeniesienie
  jednego wiersza daje jedną operację, nie czyszczenie całości. Pełna podmiana
  jest dopuszczalna tylko przy rzeczywistej zmianie całego zbioru.
- **Fokus i wybór to OSOBNE własności.** Sprawdzaj oba (`GetFirstSelected` nie
  wystarcza) i zachowanie świadomego ruchu użytkownika.
- **Programowa selekcja nie może zmienić modelu.** W trakcie podmiany wiersze
  wysyłają `EVT_LIST_ITEM_SELECTED`; bramka `updating` pilnuje, żeby przejściowy
  indeks nie wszedł jako nowy wybór. Prawdziwy ruch użytkownika musi nadal dojść.
- **Strzałki natywne.** Góra/dół działają same, bez naszej obsługi.
- **Pusta lista i przejścia mały↔duży** (np. 1 → 2475 → 9) bez degradacji mowy.

Pomiar czasu planu na pełnej skali: `python3 tools/measure_list_sync.py 2476`.
Odbiór 10 widoków i kwity: `amc_pomoc/wx-full-profile-after421/native-lists-after422/`.

Pusta lista jest odebrana: nazwa „Pliki lokalne”, rola NVDA 14 (MSAA LIST ma
inny numer: 33), właściwy komunikat i wyjście Backspace. Dawna para A/B z
`native-lists-after422/diag_pustej.py` była nadpisana i nie dowodziła wersji
źródłowej usterki. Przy ponawianiu prób używaj `observer_w02v.py`, pełnego
przekazywania argumentów wrapperów, PID/runID producenta, zgodności modelu
z `GetItemCount` i czystego stdout; sam licznik modelu nie dowodzi wypełnienia GUI.
