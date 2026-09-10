# Rejestr ryzyk i niejednoznaczności AMC

Ten plik przechowuje obserwacje, których nie należy jeszcze nazywać ani
potwierdzonym błędem, ani prawidłowym zachowaniem. Ma umożliwić przekazanie
projektu innemu modelowi, testerowi albo narzędziu automatycznemu bez
odtwarzania całej historii rozmów.

Rejestr i wynikająca z niego końcowa macierz testów obejmują **całą aplikację
AMC**: wspólny rdzeń, wszystkie sesje, listy i widoki, odtwarzanie,
nagrywanie, wyszukiwanie, ustawienia, dostępność, zapis danych oraz integracje
systemowe. Wpis dodany podczas pracy nad jednym modułem nie ogranicza testów
wyłącznie do tego modułu, jeżeli ten sam mechanizm jest współdzielony.

## Zasady prowadzenia rejestru

### TIDAL alpha.335 — próbki, opóźnienia i urządzenia (otwarte)

- Fakt: użytkownik potwierdził dźwięk i przewijanie próbek około 30 sekund
  w alpha.334. Pełnego odtwarzania nie uzyskano. Log nie zachowywał powodu
  próbki; 335 dodaje ścisłą listę rozpoznawanych powodów SDK.
- Fakt: AMC resetował SDK osobno, a SDK load robił reset ponownie.
  335 usuwa ten zbędny etap. To nie dowodzi, że całe opóźnienie usunięto.
- Do pomiaru na koncie: czas przygotowania AMC, kolejki mostka, load/play
  SDK i potwierdzenia startu; 10 kolejnych zmian i seria szybkich zmian,
  ponowne otwarcie tego samego utworu, utrata sieci, błąd i koniec próbki.
- Regresje obowiązkowe: brak samoczynnego skakania po błędzie/próbce,
  zachowanie Kolejki, brak logowania w pętli, fokus w odtwarzaczu, brak
  przypadkowego uruchamiania poprzedniego utworu po anulowaniu.
- TIDAL Connect: oficjalna dokumentacja ogranicza integrację do partnerów
  sprzętowych. Do uzyskania: potwierdzone API i uprawnienia kontrolera.
  Wybór głośników Windows, DLNA lub podstawowe sterowanie WiiM nie zamykają
  tego zadania. Nie deklarować Connect jako działającego po wykryciu urządzenia.
- Po przyszłym podłączeniu: osobno testować wybór materiału i celu, zmianę
  urządzenia, wybudzenie, utratę urządzenia, wyciszenia i odtwarzanie przez
  inne aplikacje. Lokalne próbki nie mogą zmieniać stanu kolejki na urządzeniu.

- Każdy wpis rozdziela **fakt zaobserwowany** od hipotezy i interpretacji.
- Brak drugiego odtworzenia nie zamyka sprawy; wpis otrzymuje stan
  **do ponownego testu**.
- Po potwierdzeniu błędu powstaje minimalny scenariusz w
  `TESTY_ZADANIA_PL.md`, a jeśli to możliwe także test automatyczny.
- Po naprawie wpis pozostaje w rejestrze ze stanem **zamknięte**, numerem
  wersji, commitem i wskazaniem testu regresji.
- Dane wrażliwe, podpisane adresy strumieni, tokeny i pełna zawartość schowka
  nie mogą trafiać do tego pliku ani do logów.
- Przed wydaniem publicznym wszystkie wpisy otwarte i „do ponownego testu”
  należy przejrzeć osobno, także z NVDA i co najmniej jednym niezależnym
  przebiegiem testów automatycznych.

## Szablon wpisu

### AMC-RYZYKO-NNN — krótka nazwa

- Stan: otwarte / do ponownego testu / potwierdzone / zamknięte.
- Dotyczy: wersja, moduł i widok.
- Fakt zaobserwowany: dokładnie to, co rzeczywiście wystąpiło.
- Warunki: plik, format, źródło, aktywny widok, klawisze i istotne ustawienia.
- Częstość: jeden raz / kilka razy / zawsze / nieustalona.
- Hipotezy: możliwe przyczyny, wyraźnie oddzielone od faktów.
- Dowody: zakres czasu logu, komunikat, test, zrzut albo commit — bez sekretów.
- Następny test: najmniejsza próba, która może potwierdzić albo obalić problem.
- Rozstrzygnięcie: wersja, commit i test regresji po zamknięciu.

## Aktualne wpisy

### TIDAL-EMBED-20260909 — logowanie strony nie odblokowało Embed

- Stan: potwierdzone zachowanie w teście, przyczyna produkcyjna do wyjaśnienia.
- Embed: próbka 30 sekund także po zwykłym logowaniu i odświeżeniu.
  Główna strona TIDAL: ten sam utwór przekroczył 30 sekund.
- Publiczny kod Embed inicjalizuje zwykłe odtwarzanie bez tokenu użytkownika;
  osobna ścieżka użytkownika dotyczy Nostr. Nie jest to dowód, że produkcja
  używa identycznego kodu ani potwierdzenie dostępności Nostr dla AMC.
- Nie ponawiać identycznego logowania, nie ogłaszać pełnego odtwarzania ani
  maksymalnej jakości w AMC. Bez nowych tożsamości, dodatków i kont bez zgody.
- Dowody i następny krok:
  `TIDAL_WERYFIKACJA_PELNEGO_ODTWARZANIA_2026-09-09.md`.

### TEST-RUNNER-20260909 — okno błędu programu testowego

- Windows zarejestrował nieobsłużony wyjątek procesu Windows.SmokeTests
  podczas wcześniejszego testu WebView2. Powiązanie później zgłoszonego przez
  użytkownika okna z tym wpisem jest prawdopodobne, nie całkowicie pewne.
- W źródłach 335 była już osłona dedykowanej komendy testowej. Dodatkowo
  obejmujemy granicą błędów cały program testowy, w tym sprzątanie, oraz
  inicjalizację i nieobsłużone callbacki dispatchera testowego STA.
- Wymagany wynik testu negatywnego: kod wyjścia 1 i czytelny błąd w stderr,
  bez udawania sukcesu i bez pozostawiania procesu oczekującego na dialog.
  Testy nie zmieniają obsługi wyjątków działającego AMC.
- Test regresji: `--smoke-runner-self-test`; osobne procesy dla wyjątków
  main/probe/STA/dispatcher/cleanup. To nie dowodzi odporności na awarie
  natywne, przepełnienie stosu ani wszystkie możliwe awarie WebView2.
- Wynik: pięć przypadków oraz pełne zestawy Core i Windows zaliczone.
  Wyniki pośrednie i ograniczenia: `wyniki-testow/WERYFIKACJA_TIDAL_I_TESTOW_2026-09-09.md`.

### AMC-RYZYKO-001 — chwilowa kolizja równoległego zapisu stanu

- Stan: do ponownego testu.
- Dotyczy: `alpha.229`, automatyczny test rdzenia.
- Fakt zaobserwowany: podczas pierwszego łącznego przebiegu test
  „Serializacja równoległych zapisów stanu” jeden raz zakończył się
  komunikatem „Nie można usunąć pliku, który ma zostać zamieniony”.
- Częstość: jeden raz. Natychmiastowe powtórzenie samego zestawu rdzenia oraz
  pełny przebieg publikacyjny zakończyły się poprawnie.
- Hipotezy: krótkotrwała blokada testowego pliku przez system Windows albo
  wyścig w ścieżce atomowej zamiany. Nie ma dowodu, że zmiana pobierania
  Podcastów była przyczyną.
- Dowody: przebieg przygotowania commita `86500b3`; końcowe testy rdzenia i
  Windows przeszły.
- Następny test: uruchomić test równoległego zapisu wielokrotnie w pętli,
  osobno na NTFS i w folderze synchronizowanym, oraz sprawdzić pozostawione
  pliki tymczasowe po ewentualnym niepowodzeniu.

### AMC-RYZYKO-002 — rzeczywiste przerwanie pobierania odcinka

- Stan: do testu ręcznego.
- Dotyczy: `alpha.229`, `Ctrl+D`, Podcasty.
- Fakt zaobserwowany: test automatyczny potwierdza odrzucenie odpowiedzi
  krótszej niż zadeklarowana i brak urwanego pliku pod nazwą końcową.
  Nie przeprowadzono jeszcze ręcznej próby odłączenia sieci ani zamknięcia
  AMC podczas pobierania dużego, prawdziwego odcinka.
- Hipotezy: lokalny plik roboczy powinien zostać usunięty przy przerwaniu
  transferu. Kompletny plik może świadomie pozostać w katalogu ratunkowym,
  jeżeli zawiedzie dopiero publikacja do folderu docelowego.
- Dowody: automatyczny test „odporne pobieranie odcinków podcastów”.
- Następny test: zadanie `AMC-229-05` z pliku `TESTY_ZADANIA_PL.md`, osobno dla
  zwykłego folderu i folderu synchronizowanego.

### AMC-RYZYKO-003 — komunikat ukończenia przy otwartym oknie wyszukiwania

- Stan: do testu NVDA.
- Dotyczy: `alpha.229`, wyniki `Ctrl+F` i `Ctrl+Shift+F`.
- Fakt zaobserwowany: pobieranie można rozpocząć bez zamknięcia wyników, a
  okno zachowuje zaznaczenie. Kod wysyła komunikat rozpoczęcia z okna wyników,
  natomiast końcowy komunikat pochodzi z głównego okna AMC.
- Hipotezy: zależnie od aktywnego okna i ustawień NVDA końcowy komunikat może
  być usłyszany prawidłowo albo zostać pominięty. Nie ma jeszcze wyniku testu
  ręcznego.
- Następny test: pobrać jeden oraz kilka odcinków z otwartych wyników,
  pozostając w tym oknie do końca, i sprawdzić mowę, brajl oraz fokus.

### AMC-RYZYKO-004 — nazwa i rozszerzenie pliku ustalane z metadanych kanału

- Stan: świadome ograniczenie, do testów zgodności.
- Dotyczy: `alpha.229`, `Ctrl+D` i `Ctrl+S`.
- Fakt zaobserwowany: AMC wybiera rozszerzenie z adresu audio albo typu MIME;
  gdy oba są niejednoznaczne, używa `.mp3`.
- Hipotezy: wadliwy kanał może deklarować typ niezgodny z rzeczywistym
  kontenerem. Sam transfer pozostanie kompletny, ale nazwa może mieć mylące
  rozszerzenie.
- Następny test: przygotować odpowiedzi MP3, AAC/M4A, OGG, OPUS i nietypowy
  adres bez rozszerzenia z poprawnym oraz błędnym `Content-Type`. W przyszłości
  rozważyć ograniczone rozpoznanie sygnatury po pobraniu, bez dekodowania całego
  pliku.

### AMC-RYZYKO-005 — plik pobrany wcześniej, a później przeniesiony poza AMC

- Stan: świadome zachowanie wymagające oceny użyteczności.
- Dotyczy: widok **Pobrane** Podcastów.
- Fakt zaobserwowany: `Ctrl+D` pomija ponowne pobranie tylko wtedy, gdy zapisany
  `DownloadPath` nadal istnieje. Po przeniesieniu albo usunięciu pliku polecenie
  może pobrać odcinek ponownie do folderu domyślnego.
- Hipotezy: jest to bezpieczniejsze niż pozostawienie martwego wpisu, ale
  przyszłe skanowanie folderów lub ręczne wskazanie przeniesionego pliku może
  być wygodniejsze.
- Następny test: pobrać odcinek, przenieść go poza AMC, odświeżyć widok
  **Pobrane** i ponowić `Ctrl+D`; ocenić komunikat i oczekiwany stan wpisu.

### AMC-RYZYKO-006 — techniczny agregat Radia po wyszukiwaniu

- Stan: zamknięte w `alpha.230`, pozostaje test regresji NVDA.
- Dotyczy: `alpha.229`, Radio internetowe, `Ctrl+F`, Ulubione i przejście
  zwykłym Enterem.
- Fakt zaobserwowany: po wyszukaniu Radia Kolor, dodaniu go do Ulubionych i
  naciśnięciu Enter główna lista odsłoniła ponad sto nieznanych stacji zamiast
  właściwego widoku użytkownika.
- Częstość: potwierdzone raz przez użytkownika i jednoznacznie przez log.
- Przyczyna: wynik był kierowany do technicznego widoku `Multimedia`, czyli
  całego wewnętrznego indeksu Radia. Log z `alpha.229` o 21:20 wskazuje 161
  widocznych wierszy przy działaniu na jednym wyniku.
- Rozstrzygnięcie: `alpha.230` kieruje ulubiony wynik do Ulubionych, pozostały
  do Biblioteki i normalizuje każde techniczne `Multimedia` Radia do
  Biblioteki. Test automatyczny sprawdza wszystkie trzy warianty.
- Następny test: wykonać `AMC-230-01` i `AMC-230-02`, a potem powtórzyć
  analogiczny przepływ w Podcastach oraz przyszłej usłudze streamingowej.

### AMC-RYZYKO-007 — niepełne Ctrl+Z po usunięciu podcastu

- Stan: zamknięte w `alpha.240`, pozostaje test regresji NVDA i restartu.
- Dotyczy: usunięcia całego podcastu z Biblioteki i natychmiastowego `Ctrl+Z`.
- Fakt zaobserwowany: wiersz kanału wracał na listę, lecz Enter nie otwierał
  już jego odcinków; następne `Ctrl+Shift+L` ponownie oznajmiało usunięcie.
- Przyczyna: historia przywracała flagę w żywym elemencie listy, ale gałąź
  cofania nie zapisywała odtworzonej przynależności do trwałego rekordu
  Podcastów. Po przebudowie listy możliwa była dodatkowo nieaktualna referencja
  do wcześniejszej instancji elementu.
- Rozstrzygnięcie: cofnięcie rozwiązuje aktualny element po stabilnym
  identyfikatorze, synchronizuje rekord subskrypcji i zleca trwały zapis.
  Operacja nie pobiera kanału z sieci ani nie uruchamia odtwarzania.
- Następny test: wykonać `AMC-240-01`–`AMC-240-03`, w tym przebudowę listy i
  ponowne uruchomienie programu.

### AMC-RYZYKO-008 — dokładność granicy zapisanego rozdziału

- Stan: świadome ograniczenie pierwszego etapu, do testów wielu formatów.
- Dotyczy: `alpha.241`, zapis pojedynczego rozdziału do nowego pliku.
- Fakt: eksport korzysta ze wspólnego bezpiecznego mechanizmu FFmpeg. Dla
  formatów umożliwiających kopiowanie strumienia granica może zostać dopasowana
  do najbliższej ramki; dokładny wariant może wymagać jawnego ponownego kodowania.
- Zabezpieczenie: źródło nigdy nie jest zmieniane, wynik najpierw powstaje jako
  osobny plik tymczasowy, a zdalny podcast wymaga jawnego pobrania.
- Następny test: wykonać `AMC-241-04` dla MP3, AAC/M4A, OGG/Opus, FLAC i WAV,
  porównując początek, koniec, czas i możliwość ponownego otwarcia pliku.

### AMC-RYZYKO-009 — rozdziały dostawcy i materiały bez znanego czasu

- Stan: podstawowa funkcja wdrożona w `alpha.252`, wymaga testów na prawdziwych
  kanałach i plikach różnych dostawców.
- Dotyczy: Podcasting 2.0 JSON Chapters, ID3 CHAP/CTOC, rozdziały MP4 i CUE.
- Fakt: AMC odczytuje Podcasting 2.0 JSON, Podlove Simple Chapters, znaczniki
  czasu z opisu oraz ID3/MP4 przez FFprobe. Zewnętrzny JSON jest pobierany tylko
  po `Ctrl+Alt+B`. Dane dostawcy są wymieniane według źródła, a rozdziały własne
  pozostają nienaruszone. CUE pozostaje etapem późniejszym. Transmisja na żywo i
  materiał bez znanego końca nadal nie mogą tworzyć poprawnie ograniczonych
  segmentów.
- Następny test: sprawdzić legalne przykłady JSON, ID3 i M4A/MP4, w tym TyfloPodcast,
  zmianę spisu po odświeżeniu, brak sieci oraz plik o nieznanym czasie.

### AMC-RYZYKO-010 — wielokrotny wybór rozdziałów a ręczne sterowanie

- Stan: zaimplementowane zabezpieczenie, wymaga testu interakcyjnego NVDA.
- Dotyczy: odtwarzania kilku zaznaczonych, także nieprzyległych rozdziałów.
- Fakt: AMC automatycznie przeskakuje pominięte fragmenty i zatrzymuje się po
  ostatnim wyborze. Ręczne przewinięcie, zmiana materiału albo nawigacja po
  zakładkach i rozdziałach wyłącza ten ograniczony plan, aby zegar nie cofnął
  później użytkownika bez ostrzeżenia.
- Następny test: wykonać `AMC-241-03`, w tym ręczne przewinięcie tuż przed
  automatycznym przejściem i wybór ostatniego rozdziału kończącego się razem z
  całym odcinkiem.

### AMC-RYZYKO-011 — rozmiar archiwum Podcastów i migracja SQLite

- Stan: pierwszy etap zamknięty w `alpha.249`; pełne repozytorium zapytań
  pozostaje do wdrożenia.
- Fakt zaobserwowany: rzeczywisty `state.json` osiągnął około 67 MB przy 227
  podcastach i 32 134 odcinkach, a wcześniejsze uruchomienia potrafiły działać
  ociężale lub sprawiać wrażenie zawieszonych.
- Rozstrzygnięcie etapu pierwszego: archiwum jest migrowane do osobnego
  `podcasts.db`; liczby rekordów i integralność są weryfikowane, a widoki
  tworzą najwyżej 150 wierszy naraz. Próba na kopii rzeczywistych danych
  zachowała wszystkie rekordy, a pierwsza migracja trwała około 4,9 sekundy.
- Ważne rozróżnienie: zrzut pamięci wskazywał również kilka dużych buforów
  audio Radia, dlatego całego użycia pamięci nie wolno przypisywać Podcastom.
- Pozostałe ryzyko: bieżący etap nadal odczytuje archiwum metadanych do modelu
  procesu. Docelowe repozytorium ma stronicować zapytania bez pełnego odczytu,
  a aktualizator kanałów ma scalać transakcyjnie tylko jeden podcast.
- Następny test: migracja, ponowny start, wejście do audycji z ponad 150
  odcinkami, wielokrotne **Załaduj więcej**, `Ctrl+K`, powrót fokusa, pełny
  eksport i odtworzenie kopii zapasowej.

### AMC-RYZYKO-012 — prawdziwa synchronizacja TIDAL

- Stan: odczyt, pięć kolekcji, tworzenie i edycja zawartości playlist, rozdzielone
  widoki `Ctrl+U` i `Ctrl+L`, trwała Kolejka oraz awaryjny katalog kolekcji
  wdrożone do `alpha.330`; nadal wymaga testów z
  aplikacją deweloperską i prawdziwym kontem.
- Granica: TIDAL jest źródłem prawdy dla pięciu zdalnych kolekcji, a AMC dla
  kolejki, historii, zakładek, presetów i ustawień. Dane demonstracyjne nigdy
  nie mogą zostać wysłane na konto.
- Zabezpieczenie: tokeny są w Menedżerze poświadczeń Windows, synchronizacja
  jest sekwencyjna i stronicowana, `429` respektuje `Retry-After`, a błąd jednej
  kategorii nie czyści jej ostatniego poprawnego stanu. Od `alpha.329` pusty
  katalog startowy ani synchronizacja częściowa nie mogą przycinać trwałego
  porządku kolekcji. Pełna synchronizacja odbudowuje tryb **według dodania** z
  udokumentowanego pola `meta.addedAt` każdego związku kolekcji TIDAL, zamiast
  utrwalać techniczną kolejność obiektów `included`.
- Ryzyka: poziom dostępu aplikacji, cofnięty zakres, wygaśnięcie tokenu,
  regionalna niedostępność, zamienniki wydań, bardzo duże kolekcje, opóźniona
  spójność, prawo zapisu tylko do części playlist, duplikaty tego samego utworu
  na playliście, wymiana obiektów cache podczas synchronizacji oraz fokus po
  powrocie z przeglądarki i okien wyboru. Kolejka i **Odtwórz jako następne**
  muszą być przywracane po stabilnym identyfikatorze usługi i nigdy nie mogą
  być zerowane tylko dlatego, że katalog jest jeszcze pusty albo nowy obiekt
  katalogowy nie ma lokalnej flagi. `alpha.328` przechowuje lokalną kopię
  Kolejki i usuwa stare rekordy demonstracyjne. Dopiero `alpha.330` zapisuje
  bezpieczną kopię całej ostatniej kolekcji. Ta wersja uzupełnia też żądanie
  odświeżenia tokenu o `client_id` i zakres wymagany przez oficjalny moduł
  TIDAL; pierwsze logowanie po aktualizacji może być potrzebne do zastąpienia
  tokenu wystawionego dla wcześniejszego, wadliwego przepływu.
- Odtwarzanie: wyłącznie oficjalny moduł Player TIDAL. Bez wydobywania adresów,
  pobierania chronionych utworów i bez udawania TIDAL Connect.
- Następny test: wykonać logowanie PKCE, pełne i częściowe odświeżenie, restart,
  odświeżenie tokenu, wyszukiwanie, `429`, odłączenie konta, trwałość Kolejki
  po synchronizacji i restarcie oraz kontrolę logów i eksportów pod kątem
  danych uwierzytelniających. Te same testy trwałości są obowiązkowe dla
  przyszłych adapterów Spotify i Apple Music.

## TIDAL, 10 września 2026 — pierwsze wejście do wykonawcy

- Zgłoszenie: wejście do Stevie Wondera udało się dopiero za drugim razem;
  wcześniej był komunikat wczytywania. Stary log nie wskazuje, czy wynik
  został pominięty, czy użytkownik ponowił jeszcze trwającą operację.
- Potwierdzony testem WPF błąd mechanizmu: przebudowa ItemsSource i
  przywrócenie tego samego logicznego elementu unieważniały żądanie.
  Poprawka 338 rozdziela techniczne odświeżenie od rzeczywistej nawigacji.
- Nie utożsamiać wykrytego błędu z udowodnioną przyczyną konkretnej próby.
  Następny test: jedno wejście z menu powiązań przy wolnej odpowiedzi,
  także podczas aktualizacji kolekcji. Sprawdzić nowe wpisy tidal-navigation:
  start, czas, prezentacja lub pominięcie i zgodność kontekstu.
- Regresja: ręczne A–B–A, zmiana filtra, sesji, Escape, aktywne inne okno,
  kilka zaznaczonych wierszy, błąd przebudowy i pusta lista. Żadne spóźnione
  żądanie nie może przejąć nowszego miejsca użytkownika.
- Osobno: próba odtwarzania w 337 potwierdziła odczyt poświadczeń przez SDK,
  PREVIEW około 30 s i FULL_REQUIRES_SUBSCRIPTION. Nie potwierdziła braku
  abonamentu ani wyższego poziomu dostępu aplikacji; pełny odsłuch pozostaje
  nierozwiązany. Nie ponawiać identycznego logowania bez nowej przesłanki.

## Stałe, globalne obszary regresji przed publikacją

Poniższe obszary dotyczą całego AMC, a nie wyłącznie modułu rozwijanego w
danej wersji. Miały w historii projektu problemy zależne od czasu, urządzenia
albo programu zewnętrznego. Nie oznacza to, że obecnie są zepsute:

- fokus NVDA po szybkim przełączaniu widoków, Escape, oknach modalnych i
  asynchronicznym otwieraniu źródła;
- odłączenie i ponowne podłączenie zapamiętanego urządzenia audio;
- bardzo duże, uszkodzone lub częściowo dostępne pliki oraz placeholdery
  iCloud, OneDrive i Dysku Google;
- HLS, przekierowania i starsze strumienie ICY podczas odtwarzania oraz
  równoległego nagrywania;
- schowek Windows przy szybkim `Ctrl+C`, `Ctrl+Shift+C`, menedżerach schowka i
  czytniku ekranu;
- globalny prefiks oraz kombinacje przejmowane przez NVDA, Narratora albo
  system Windows;
- każda nowa lista, pole kombi i menu: pierwszy element, ruch strzałkami,
  ponowne otwarcie, powrót fokusa i brak technicznych reprezentacji obiektów.

Ten zestaw powinien zasilać końcowy plan testów macierzowych, a nie zastępować
konkretne zgłoszenia i wyniki kolejnych wersji.
