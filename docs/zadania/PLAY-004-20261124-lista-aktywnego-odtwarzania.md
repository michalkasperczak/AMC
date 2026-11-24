# PLAY-004 — lista aktywnego odtwarzania

**Zgłoszenie Michała, 24.11.2026; doprecyzowanie, 25.11.2026. Stan:** kod w gałęzi następnego AMC Test; bez nowego wydania i instalacji. Decyzja z 24.11 zastępuje dawny alias `Shift+F6` do odtwarzacza na liście i powrót z odtwarzacza. Doprecyzowanie z 25.11 dodaje Spację na wskazanym wierszu; pomysł przełączania dodatkowego odsłuchu komputerowego pozostaje do projektu. Zwykły `F6` nadal przechodzi między listą a odtwarzaczem bieżącej sesji; Escape w odtwarzaczu wraca na listę tej sesji.

## Materiał do dokumentacji użytkownika

`Shift+F6` otwiera listę aktualnie odtwarzanych lub wstrzymanych materiałów znanych AMC. Każdy wiersz mówi tytuł, sesję lub usługę, urządzenie i stan, np. „Radio 357, Radio internetowe, WiiM Sound, odtwarzanie”. Tytuł i nazwa urządzenia pochodzą z odczytanego stanu, nie z technicznego ID. Lista rozpoczyna na pozycji bieżącej sesji, jeśli taka gra. Strzałki przeglądają wiersze. Enter otwiera odtwarzacz wybranej sesji. Escape zamyka listę i przywraca poprzedni fokus bez zmiany sesji. Po Enter Escape z odtwarzacza wraca już do listy **wybranej** sesji. Spacja w oknie Shift+F6 pauzuje lub wznawia wskazaną pozycję bez przełączania bieżącej sesji; po potwierdzeniu wiersz i komunikat podają nowy stan. Gdy urządzenie Sonos nie obsługuje pauzy danego źródła, jego istniejący adapter może zatrzymać strumień; wtedy pozycja znika z listy i AMC mówi „zatrzymano”.

Lista odczytuje w tle stany lokalnego hosta, YouTube online, zewnętrznych aplikacji Apple Music i TIDAL oraz **wybranych w AMC** urządzeń WiiM, Sonos, Frontier Smart i HEOS. Nie skanuje wszystkich urządzeń sieciowych ani nie przechwytuje dowolnego odtwarzania rozpoczętego poza AMC na niezaznaczonym odbiorniku. Puste i zatrzymane urządzenie nie jest podawane jako grające. Jeżeli odczyt części usług nie powiedzie się, wynik nie jest zgadywany, a komunikat wymienia usługi, których nie sprawdzono. Stan może zmienić się po otwarciu okna; wtedy należy otworzyć listę ponownie.

Polecenie „Aktywne odtwarzanie…” jest również w menu Widok, palecie `Ctrl+Shift+K`, pomocy kontekstowej `Ctrl+F1` i pełnej pomocy. Ten sam opis obejmuje Enter, Spację i Escape. Akcja jest dostępna w profilach Test i Public; lista pokazuje tylko włączone sesje danego profilu. Działanie nie wymaga zmiany ustawień.

## Uzasadnienie i decyzje

Lista służy do szybkiego znalezienia, gdzie coś gra. Wyboru Enter nie traktujemy jako chwilowego podglądu: zmienia bieżącą sesję, więc późniejszy Escape z odtwarzacza wraca do jej listy. Spacja steruje tylko właścicielem zaznaczonego wiersza. Identyfikatory sesji i urządzeń pozostają w modelu, a nie w mowie NVDA. Odczyty sieciowe i hosta są poza wątkiem wx, aby nie blokować nawigacji.

Michał zaproponował również klawisz w tym oknie, który dołącza komputer jako drugi odsłuch do materiału grającego na Google Cast lub innym urządzeniu, a po ponownym naciśnięciu go usuwa. **To osobna część PLAY-004, jeszcze bez skrótu i bez implementacji.** Obecny `Shift+M` tworzy Mirror tylko dla źródła, które AMC sam odtwarza na głównym komputerze; główna trasa Cast zatrzymuje lokalny silnik. AMC nie otrzymuje strumienia audio z odbiornika Cast uruchomionego przez inną aplikację. Najpierw trzeba zaprojektować drugi lokalny tor dla źródeł, którymi AMC faktycznie dysponuje, określić zachowanie DRM i synchronizacji, a następnie sprawdzić sprzętowo. Nie pokazujemy czynności Mirror dla dowolnego wiersza, gdy nie może zadziałać.

## Testy i wynik

- Testy modelu odrzucają obcy status sesji, stan bez odtwarzania i puste urządzenie; etykieta nie zawiera ID. Test sprawdza, że Spacja kieruje polecenie do sesji zaznaczonego wiersza, a nie do sesji aktywnej w oknie głównym.
- Testy parytetu porównują `Shift+F6`, nazwę, opis i wykonanie z menu, paletą i pomocą we wszystkich sesjach oraz w obu widokach. Osobny test sprawdza język angielski. Test natywnej listy wx sprawdza początkowy wybór, zmianę wiersza, Spację i ponowne otwarcie.
- **142 testy celowane zaliczone** w izolowanym interpreterze Python z prawdziwym wx; obejmują model, sesje, skróty, menu, paletę, 4 kontrole językowe, początkowy wybór i ponowne otwarcie listy, Enter/Anuluj i Spację kierowaną do Radia lub WiiM. `py_compile` i `git diff --check` zaliczone. Próby używały atrap i nie uruchamiały dźwięku ani poleceń na głośniku. Nie wykonano ręcznej próby NVDA.
- Pełne `test_i18n.py`: **14 zaliczonych, 1 pominięty, 2 niezaliczone** testy szerokiego inwentarza istniejących tłumaczeń. Parytet nowego polecenia w językach polskim i angielskim przeszedł.

## Znane ograniczenia

- Obce urządzenie Cast grające materiał z innej aplikacji nie jest sterowalne z tej listy, dopóki nie jest wybranym celem AMC.
- Pozycja może się zmienić między odczytem a Enter. Lokalna Spacja ponownie sprawdza identyfikator materiału przed poleceniem; sprzętowy odbiór stanów zdalnych pozostaje do testu.
- Wspólny odczyt wielu usług może potrwać, gdy urządzenie sieciowe nie odpowiada. Okno wx pozostaje wtedy responsywne.
- Szeroki inwentarz tłumaczeń projektu ma dwa wcześniejsze niezaliczone testy; nie dotyczy to parytetu nowego polecenia.

## Wersja i commit

Gałąź `codex/heos-next-test-20261123`. Commit kodu i testów: `9cef782a`. Kod i testy nie oznaczają wydania. Wydana i zainstalowana wersja użytkownika pozostaje alpha.525; nowy instalator, instalacja i wydanie GitHub tej zmiany nie powstały. Następny krok: połączyć z innymi poprawkami, zbudować i opublikować wspólne wydanie Test na GitHub, a potem odebrać NVDA i urządzenia na żywo.
