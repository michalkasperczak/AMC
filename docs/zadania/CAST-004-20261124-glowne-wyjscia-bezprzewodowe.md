# CAST-004 — główne wyjścia bezprzewodowe Cast i AirPlay

**Korekta statusu 25.11.2026:** źródła CAST-004 zostały wydane w alpha.526 i zainstalowane wraz z alpha.527. Wcześniejsze stwierdzenia o braku instalacji są historyczne; odsłuch prawdziwego AirPlay i odbiór NVDA nadal nie są potwierdzone. [Dowody wydania i instalacji](RELEASE-ALPHA527-20261125.md).

**Decyzja Michała, 24.11.2026. Stan 24.11.2026:** kontrakt zatwierdzony i wdrożony w źródłach gałęzi następnego AMC Test; testy kodu i odizolowanego hosta zaliczone. Nie ma instalacji ani wydania GitHub tej zmiany. Odsłuch na prawdziwym AirPlay i odbiór NVDA są otwarte; zainstalowana alpha.525 nie zawiera tego przyrostu.

## Decyzja zastępująca wcześniejsze warianty

- `Shift+A`: wybór wyjścia audio Windows bieżącej sesji.
- `Shift+C`: wybór jednego **głównego wyjścia bezprzewodowego** bieżącej sesji: „Ten komputer”, Google Cast albo AirPlay. Wybrany cel ma być oznaczony słowem „wybrane”. AirPlay na tej liście ma przenieść słyszalne odtwarzanie na jeden odbiornik; nie może być tylko drugim, równoległym strumieniem.
- `Shift+M`: Mirror, czyli jawny dodatkowy odsłuch na drugim wyjściu. Wybór AirPlay jako drugiego celu może pozostać tutaj, ale jego stan musi być oddzielony od głównej trasy AirPlay.
- `Ctrl+Shift+[` i `Ctrl+Shift+]`: poprzednie i następne dostępne **główne wyjście** z tej samej listy co `Shift+C`, z zawinięciem i potwierdzonym komunikatem „Wyjście bieżącej sesji: {nazwa}”. Nazwa obejmuje także typ celu, jeśli dwa protokoły zgłaszają tę samą nazwę urządzenia. Po nieudanym przełączeniu AMC ma powiedzieć o błędzie i pozostawić poprzedni cel.
- `Ctrl+Alt+Shift+[` i `Ctrl+Alt+Shift+]`: brak decyzji i przypisania. Nie zmieniać nimi Mirror bez osobnego ustalenia.

To rozstrzygnięcie zastępuje sugestię `Shift+P`, rozważanie umieszczenia AirPlay pod `Shift+A` oraz wcześniejszy zapis CAST-003, że miejsce głównego AirPlay pozostaje otwarte. Nie zastępuje znaczenia `Shift+M`: Mirror nadal oznacza dwa jednoczesne wyjścia.

## Zachowanie w źródłach i miejsca poleceń

`Shift+C` oraz `Ctrl+Shift+[` / `Ctrl+Shift+]` używają jednej typowanej migawki celu: komputer, dostępne Google Cast i dostępne AirPlay. Etykieta zawiera protokół, np. „AirPlay: Gabinet, wybrane”, bez prywatnego identyfikatora. Wybrany odbiornik, który zniknął z wykrywania, pozostaje widoczny jako „niedostępne, wybrane”; skróty go pomijają. Lista jest otwierana poza wątkiem wx, ignoruje spóźnione wyniki po zmianie sesji i przywraca fokus po zamknięciu. `Shift+M` zachowuje oddzielną listę **dodatkowego** wyjścia i nie pozwala użyć tego samego odbiornika AirPlay w obu trasach.

Nowy `PrimaryAirPlayRouter` zachowuje kolejkę, metadane i pozycję w natywnym silniku AMC, wyciszając jego lokalne wyjście na czas transmisji RAOP przez FFmpeg. Przed pierwszym strumieniem ustawia głośność AirPlay najwyżej na 30%; późniejsze jawne polecenia głośności sterują odbiornikiem. Obsługuje pauzę/wznowienie, Stop, seek, następny materiał wykryty przez zmianę źródła w statusie, wyciszenie, powrót do komputera i awarię odbiornika. Po awarii oddaje dźwięk komputerowi zgodnie z ostatnim stanem wyciszenia użytkownika. Przełączenie Cast–AirPlay przechodzi przez lokalny silnik z wyciszeniem; po niepowodzeniu próbuje odtworzyć poprzedni cel. Dla natywnego TIDAL poprawiono status i zmianę wyciszenia po Stop, gdy materiał nie ma już właściciela dekodera. Gdy inna sesja zaczyna używać współdzielonego dekodera, stara trasa AirPlay zostaje wyłączona przed jej startem.

W sesji TIDAL format DASH nie pojawia się wśród wyborów Cast, ale główne AirPlay pozostaje możliwe przez lokalny adres proxy. Dla źródeł, których Cast nie może odtworzyć bezpośrednio, lista zachowuje AirPlay bez reklamowania nieobsługiwanego odbiornika Cast. Warunki dostępności dla natywnego TIDAL i aktywnej trasy są używane również przez menu, paletę i pomoc.

Wspólny `CommandPresentation` nadaje nowe nazwy i opisy `Shift+C` oraz skrótom nawiasów. Korzystają z niego menu Dźwięk, paleta `Ctrl+Shift+K`, pomoc `Ctrl+F1` i pełna pomoc. Po potwierdzeniu wyboru komunikat mówi „Wyjście bieżącej sesji: AirPlay: {nazwa}” albo nazwę Cast/komputera. Odczyt bieżącego materiału dołącza faktyczny cel AirPlay tylko przy aktywnej trasie z tytułem. Błędy i utrata RAOP mają osobne komunikaty polskie i angielskie; nie wypowiadają identyfikatorów urządzeń.

## Weryfikacja i ograniczenia

- Celowane testy tras, klienta hosta, skrótów, menu i palety: **223 zaliczone, 1 pominięty**; pominięty test wymaga osobno wskazanego hosta Cast. Testy sprawdzają m.in. bezpieczny początkowy poziom AirPlay, rollback, sesje współdzielące dekoder, zmianę źródła, TIDAL DASH, wybrany odbiornik offline oraz brak ujawnienia ID.
- Osobny proces z prawdziwym ukrytym wx: **40 zaliczonych**, w tym ponowne otwarcie listy z wybranym AirPlay i czytelne etykiety. To nie jest test mowy NVDA.
- Celowane testy katalogów PL/EN, szablonu języka oraz parytetu menu/palety/pomocy: **4 zaliczone**. Pełne `test_i18n.py` dało 160 zaliczonych, 1 pominięty i 2 niezaliczone szerokie testy inwentarza istniejących tłumaczeń; ich pełny zakres nie był naprawiany w CAST-004.
- `py_compile` zmienionych modułów Python: zaliczone. Host .NET Release w tym worktree: kompilacja zaliczona, **0 ostrzeżeń i 0 błędów**. Odizolowany host z pustymi danymi i bez odtwarzania potwierdził `transport.toggleMute` oraz status wyciszenia zatrzymanego TIDAL: **1 zaliczony**. Pierwsze trzy próby uruchomienia tego testu wykryły wymagania magazynu kolejki (nie wolno wskazywać dwóch magazynów i profil wymaga własnej bazy); dopiero konfiguracja z jednym tymczasowym magazynem przeszła. Nie zmieniono zainstalowanego AMC.
- Nie wykonano odtwarzania na prawdziwym odbiorniku AirPlay, parowania RAOP ani ręcznego odbioru NVDA. Historyczny timeout AirPlay na WiiM pozostaje niepotwierdzony sprzętowo. Strumień AirPlay może mieć opóźnienie, a `Shift+M` nie obiecuje synchronizacji dwóch wyjść. Wydanie GitHub, instalacja i numer nowej wersji pozostają otwarte do wspólnej aktualizacji.

Zmiana nie wymaga migracji ustawień: wybór głównego AirPlay żyje tylko w bieżącej sesji aplikacji. Historyczny workflow budujący alpha.525 przy każdym pushu tej gałęzi przełączono na ręczne uruchomienie, aby nie tworzył nowej paczki pod starym numerem. Kod jest na gałęzi `codex/heos-next-test-20261123`, commit `ae7c456c3d2e2d978326925e322cb81d83ea8470`. Zainstalowana i wydana wersja pozostaje alpha.525.

## Szkic do instrukcji po wdrożeniu

„`Shift+C` wybiera główne wyjście bezprzewodowe bieżącej sesji: komputer, Google Cast lub AirPlay. `Ctrl+Shift+[` i `Ctrl+Shift+]` przechodzą do poprzedniego lub następnego dostępnego celu i wypowiadają jego nazwę po potwierdzeniu. `Shift+A` wybiera urządzenie Windows. `Shift+M` włącza dodatkowy odsłuch na drugim wyjściu.” To jest tekst przyszłej instrukcji do korekty Michała, nie opis bieżącej wersji.
