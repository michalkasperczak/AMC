# RADIO-LIVE-REC-20261122 — nagranie po zmianie adresu stacji

Stan: 22.11.2026. Poprawka źródeł i testy gotowe; **niezintegrowana z wydaniem, niezainstalowana, nieodebrana w NVDA ani na komputerze Hermes**. Baza: AMC Python Test/Public alpha.512, commit `d360cd89f622b90ae351f1ed876904b38856b625`.

## Zgłoszenie Michała i granice diagnozy

22.11.2026 Michał opisał zaplanowane po 16:00 nagranie transmisji „Hermanice”/Dominikanie. Nieaktywny odnośnik wywołał komunikat sugerujący start nagrania. Po podmianie adresu w Ulubionych Enter odtwarzał transmisję, lecz ponowne Ctrl+R szybko zakończyło się błędem zapisu. Użytkownik wymaga, aby ręczne i zaplanowane nagranie korzystało z aktualnego adresu tego samego wpisu stacji, a Ulubione i Biblioteka były spójne.

Komputer Hermes i jego dziennik tego zdarzenia nie są dostępne w tej sesji. Lokalny profil bieżącego komputera służył tylko do odczytowej kontroli modelu; nie jest dowodem tego, jaki adres i błąd wystąpiły na Hermesie. Nie otwierano ani nie zmieniano nagrań użytkownika. Dokładnej przyczyny komunikatu „nie można zapisać pliku” na Hermesie nie da się jeszcze przypisać bez tamtego logu lub próby odbiorczej.

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

Poprawka aktywnego planu została sprawdzona kompilacją, ale nie próbą z prawdziwą transmisją na Hermesie. W odbiorze trzeba wykonać bezpieczną próbę na własnym, krótkim harmonogramie i sprawdzić: niedostępny URL, zmianę URL w trakcie oczekiwania, komunikat rozpoczęcia po rzeczywistym otwarciu pliku, Ctrl+R po edycji w Ulubionych i zgodność widoku Biblioteki. Nie podmieniaj działającej instalacji w trakcie żadnego nagrania.

**Następny krok:** zintegrować tę gałąź z przygotowywaną aktualizacją AMC, sprawdzić stan nagrań przed instalacją, przeprowadzić próbę odbiorczą, a po faktycznym wydaniu wpisać wynik Hubertowi w odpowiednim zgłoszeniu GitHub.

## Wersja i commit

Gałąź `codex/live-radio-recording-20261122`, źródła i testy commit `f21fb18d`. Kod i testy: tak. Kompilacja LiteHost: tak. Instalacja Test/Public: nie. Publikacja GitHub: nie. Odbiór użytkownika i NVDA: nie.
