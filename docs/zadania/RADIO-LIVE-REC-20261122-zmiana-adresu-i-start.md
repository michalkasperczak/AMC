# RADIO-LIVE-REC-20261122 — nagranie po zmianie adresu stacji

Stan: 22.11.2026. Poprawka źródeł i testy gotowe; **niezintegrowana z wydaniem, niezainstalowana, nieodebrana w NVDA ani na komputerze Hermes**. Baza: AMC Python Test/Public alpha.512, commit `d360cd89f622b90ae351f1ed876904b38856b625`.

## Zgłoszenie Michała i granice diagnozy

22.11.2026 Michał opisał zaplanowane po 16:00 nagranie transmisji „Hermanice”/Dominikanie. Nieaktywny odnośnik wywołał komunikat sugerujący start nagrania. Po podmianie adresu w Ulubionych Enter odtwarzał transmisję, lecz ponowne Ctrl+R szybko zakończyło się błędem zapisu. Użytkownik wymaga, aby ręczne i zaplanowane nagranie korzystało z aktualnego adresu tego samego wpisu stacji, a Ulubione i Biblioteka były spójne.

**Korekta Michała z 22.11.2026:** incydent nagrania wystąpił na głównym komputerze, na którym działa ta sesja, a nie na Hermesie. Zastępuje to wcześniejsze błędne przypisanie komputera. Odczytano lokalny dziennik `C:\Users\micha\AppData\Roaming\AMC-wx-Lite\logs\frontend.log` oraz historię z lokalnego profilu, bez zmiany profilu lub nagrań. O 16:15:12 operacja `youtube.radioBroadcasts` zwróciła „Nie udało się ustalić kanału YouTube tej stacji” (ten sam błąd wystąpił o 14:25). Historia zawiera cztery ręczne próby nagrania stacji Hermanice zakończone o 16:14:08, 16:14:20, 16:14:34 i 16:15:26: wszystkie mają wynik `failed`, zero zapisanych plików i przyczynę „Nie udało się odtworzyć tej transmisji YouTube. Sprawdź, czy transmisja trwa i jest publiczna”. Historia ta nie potwierdza ani nie wyjaśnia zgłoszonego pięciosekundowego pliku. Nie przeglądano Kosza i nie można przypisać dokładnego starego URL każdej z prób tylko na podstawie tego dziennika.

W bieżącym zapisie profilu stacja z tym samym identyfikatorem ma adres `https://www.youtube.com/watch?v=mD6gpBK7r6E`; trzy prywatne plany dla tego identyfikatora również wskazują ten adres. To jest stan po zdarzeniu, a nie dowód adresu w chwili każdej próby. Bieżące flagi tej stacji wskazują, że nie należy obecnie do Biblioteki ani Ulubionych; nie potwierdza to jej członkostwa w czasie incydentu. Dziennik hosta z godzin 16:46–16:48 zawiera wpisy późniejszych testów Codexa, dlatego nie służy jako dowód incydentu.

## Ustalenia w kodzie

1. `radio.recordingToggle` oddawał `starting` zaraz po zleceniu zadania. GUI mówiło wtedy „Rozpoczynam nagrywanie”, zanim połączenie z YouTube dało pierwsze próbki dźwięku i zanim otwarto plik. Harmonogram analogicznie ogłaszał start w chwili nadejścia terminu. Wykonawca w rzeczywistości czeka na pierwszy dekodowany dźwięk przed `StartRecording`; przy niedostępnym adresie nie zapisuje poprawnego nagrania. Zaplanowany wykonawca może ponawiać połączenie do końca okna planu, ponieważ transmisja może zacząć się później.
2. Po odświeżeniu Ulubionych widoczny wiersz zawierał nowy URL, lecz `_selected_station()` najpierw zwracał obiekt z wcześniejszej `self.stations`. Enter odtwarzał adres wiersza, a ręczne Ctrl+R mogło przekazać do nagrania poprzedni adres tej samej stacji.
3. Edycja stacji aktualizowała zapisane plany profilu i prywatne nadpisania, lecz przy braku prywatnego nadpisania GUI nie synchronizował ponownie działającego harmonogramu hosta. Przy nadpisaniu synchronizacja była zlecana, lecz aktywne nagranie planu nadal używało migawki starego adresu, bo host zatrzymywał je tylko przy zmianie terminu.
4. Biblioteka i Ulubione Radia są widokami tego samego `radio.stations`, po identyfikatorze stacji. Edycja tego identyfikatora w profilu zmienia jeden rekord. Flagi członkostwa są osobne; nowy wpis dodany do Ulubionych w hostcie jest także oznaczany jako należący do Biblioteki. To nie usuwa możliwych historycznych niespójności danych ani planu skierowanego na inny identyfikator stacji.

## Zmiana źródeł

- `wxlite/amc_wx_lite/gui.py`: wybór stacji do Ctrl+R i edycji bierze aktualny URL z wybranego wiersza. Po zmianie stacji z aktywnymi planami ponownie czyta profil i synchronizuje harmonogram hosta, również bez prywatnego nadpisania. Zlecenie nagrania mówi o sprawdzaniu połączenia; rzeczywisty start jest ogłaszany dopiero po zdarzeniu otwarcia pliku.
- `src/AccessibleMediaController.LiteHost/LiteRadioRecordingCoordinator.Schedules.cs`: zmiana URL w synchronizowanym planie zatrzymuje jego starą aktywną próbę. Zgodnie z istniejącym cyklem wykonawcy stary segment jest zamykany, a plan w pozostałym oknie podejmuje próbę z nowym adresem. Inne stacje i plany pozostają bez zmian.
- `wxlite/tests/test_radio_recording.py`, `wxlite/tests/test_radio_schedule_management.py`: regresje dla świeżego wiersza Ulubionych, synchronizacji profilu bez nadpisania i rozróżnienia „łączenie”/„nagrywanie rozpoczęte”.

## Materiał do dokumentacji użytkownika

W sesji Radio wybranie stacji i naciśnięcie **Ctrl+R** zleca nagranie w tle. Komunikat „Sprawdzam połączenie dla nagrania w tle: [nazwa]” oznacza, że AMC dopiero łączy się ze strumieniem. „Nagrywanie rozpoczęte: [nazwa]” oznacza, że po odebraniu dźwięku rozpoczęto zapis pliku. Przy terminie z harmonogramu analogiczne komunikaty zawierają „zaplanowane nagranie”. Jeżeli strumień nie działa, AMC podaje przyczynę nieudanego nagrania; sam termin planu nie jest potwierdzeniem powstania pliku. Zmiana adresu stacji w Bibliotece albo Ulubionych działa na ten sam wpis Radia i aktualizuje plany z tym samym identyfikatorem. Już trwający plan po zmianie adresu ponawia nagrywanie w pozostałym czasie planu. Przy próbie ręcznej Ctrl+R używa adresu widocznego na wybranym wierszu. Czas oczekiwania na transmisję i ponawianie prób zależy od okna planu.

## Uzasadnienie i decyzje

Nie blokujemy planu tylko dlatego, że adres przed terminem był nieaktywny: transmisja na żywo może ruszyć później. Rozróżniamy zlecenie, połączenie i faktyczny start zapisu. Plany wiążemy przez identyfikator stacji, więc podmiana adresu tej stacji dotyczy następnych prób. Nie modyfikujemy automatycznie planów wskazujących inny identyfikator, nawet przy podobnej nazwie.

Korekta z 22.11.2026 zastępuje wcześniejszy status „następne RARCH-004”: ten zgłoszony incydent nagrania ma pierwszeństwo przed ulubionymi stacji Radio Archiwisty. Inny, jednorazowy incydent **wycinania nagrań Dwójki** pozostaje odłożony zgodnie z wcześniejszą decyzją Michała.

Ustalenie Michała z 22.11.2026: po **faktycznej aktualizacji** zawierającej poprawkę należy od razu przekazać Hubertowi wynik przez odpowiednie zgłoszenie GitHub lub inny dostępny uzgodniony kanał. W tej sesji dostępny jest GitHub; wiadomości do Huberta jeszcze nie wysłano, bo kod nie stał się wydaniem.

## Testy i wynik

- `PYTHONPATH=wxlite python -m pytest wxlite/tests/test_radio_recording.py wxlite/tests/test_radio_schedule_management.py -q` — **64 passed**.
- Powiązany zestaw siedmiu plików testowych nagrywania, planów, skrótów i widoków Radia — **136 passed**.
- `dotnet restore src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj --ignore-failed-sources --nologo` — powodzenie z lokalnych pakietów.
- `dotnet build src/AccessibleMediaController.LiteHost/AccessibleMediaController.LiteHost.csproj -c Release --nologo --no-restore` — powodzenie, **0 ostrzeżeń, 0 błędów**. Pierwsza próba `--no-restore` zgłosiła jedynie brak `project.assets.json` w nowym worktree; po restore kompilacja przeszła.
- Nie wykonano próby żywego YouTube, pliku nagrania, NVDA, instalacji ani publikacji.

## Znane ograniczenia i następny krok

Poprawka aktywnego planu została sprawdzona kompilacją, ale nie próbą z prawdziwą transmisją na głównym komputerze ani na Hermesie. W odbiorze trzeba wykonać bezpieczną próbę na własnym, krótkim harmonogramie i sprawdzić: niedostępny URL, zmianę URL w trakcie oczekiwania, komunikat rozpoczęcia po rzeczywistym otwarciu pliku, Ctrl+R po edycji w Ulubionych i zgodność widoku Biblioteki. Nie podmieniaj działającej instalacji w trakcie żadnego nagrania.

**Następny krok:** zintegrować tę gałąź z przygotowywaną aktualizacją AMC, sprawdzić stan nagrań przed instalacją, przeprowadzić próbę odbiorczą, a po faktycznym wydaniu wpisać wynik Hubertowi w odpowiednim zgłoszeniu GitHub.

## Wersja i commit

Gałąź `codex/live-radio-recording-20261122`, źródła i testy commit `f21fb18d`. Kod i testy: tak. Kompilacja LiteHost: tak. Instalacja Test/Public: nie. Publikacja GitHub: nie. Odbiór użytkownika i NVDA: nie.

## Dalszy stan 22.11.2026

Ten przyrost został połączony z kandydatem `codex/alpha512-update-candidate-20261122`. Późniejszy commit `cadc8ead` dodaje trwały adres kanału przy odświeżaniu stacji YouTube, jednoznaczny komunikat Ctrl+R podczas planu oraz lokalną korektę diagnozy. Szczegóły i testy są w `YOUTUBE-RADIO-HERMANICE-20261122.md`, a pełny stan kandydata w `ALPHA512-INTEGRATION-20261122.md`. Wcześniejsze polecenie „zintegrować tę gałąź” zostało wykonane w kodzie; nadal nie wykonano instalacji, odbioru NVDA ani publikacji.
