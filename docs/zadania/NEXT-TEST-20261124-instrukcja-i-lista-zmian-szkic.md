# Następny AMC Test — szkic instrukcji i listy zmian, 24.11.2026

Ten tekst wynika z kart HEOS-001, AM-003 i CAST-002. Jest szkicem do korekty Michała. Nie opisuje zainstalowanej alpha.525 i nie jest opublikowaną listą zmian.

**Korekta 24.11.2026:** zatwierdzony zakres przyszłego `Shift+C` obejmuje także AirPlay jako pojedyncze główne wyjście, a skróty nawiasów mają przechodzić po tej wspólnej liście. Główna trasa AirPlay jest już w źródłach, bez wydania i próby sprzętowej. To zastępuje wcześniejsze sformułowania o nierozstrzygniętym miejscu i niewdrożonej trasie AirPlay. Szczegóły: [CAST-004](CAST-004-20261124-glowne-wyjscia-bezprzewodowe.md).

**Następny stan źródeł, 24.11.2026:** główna trasa AirPlay została dodana w gałęzi następnego Test, lecz nie została jeszcze wydana ani sprawdzona na głośniku. Poniższy szkic instrukcji opisuje planowane wspólne wydanie, nie zainstalowaną alpha.525. [Wyniki i granice CAST-004](CAST-004-20261124-glowne-wyjscia-bezprzewodowe.md).

## Instrukcja użytkownika — szkic

- W sesji HEOS `Ctrl+L` otwiera katalog. Na liście Ulubionych Enter ma uruchamiać wybraną stację na wybranym głośniku; AMC używa do tego funkcji Ulubionych urządzenia. `Ctrl+Alt+Shift+P` przypisuje wybraną stację do presetu AMC, a `Ctrl+Shift+1` do `Ctrl+Shift+=` uruchamia zapisane miejsca 1–12. Po przestawieniu Ulubionych preset ma nadal wskazywać tę samą stację; gdy stacja zniknie, program poda jej brak.
- `Shift+C` otwiera komputer, Google Cast i AirPlay jako główne wyjścia bieżącej sesji. Lista oznacza aktualny cel słowem „wybrane”. `Ctrl+Shift+[` wybiera poprzednie, a `Ctrl+Shift+]` następne dostępne wyjście i wypowiada nazwę po zatwierdzeniu. Odczyt bieżącego materiału podaje tytuł i nazwę odbiornika, jeśli zna je z aktywnej trasy. Nowo wybrany odbiornik AirPlay otrzymuje początkowo najwyżej 30% głośności; później można ją jawnie zmienić.
- `Shift+A` wybiera urządzenie Windows. `Shift+M` wybiera dodatkowy odsłuch na drugim wyjściu, oddzielnie od głównego AirPlay. Skróty `Ctrl+Alt+Shift+[` / `Ctrl+Alt+Shift+]` nadal nie są przypisane.
- `Shift+F6` pokazuje odtwarzane i wstrzymane materiały znanych sesji: tytuł, usługa, urządzenie i stan. Enter otwiera wskazany odtwarzacz; Escape z niego wraca do listy **wybranej** sesji. Escape w samym oknie Shift+F6 wraca bez zmiany sesji. Spacja w tym oknie pauzuje lub wznawia wskazaną pozycję. Szczegóły i granice odczytu: [PLAY-004](PLAY-004-20261124-lista-aktywnego-odtwarzania.md).
- Strzałka w lewo ma podawać dostępne informacje o zaznaczonej stacji lub materiale. Jeśli HEOS nie udostępnia parametrów strumienia, AMC ma to powiedzieć wprost. Przechodzenie strzałkami po listach ma sygnalizować pozycję aktualnie grającą; ten fragment pozostaje do implementacji i odbioru.

## Lista zmian — szkic

- Rozdzielono odmowę polecenia HEOS od braku późniejszego odczytu statusu. Jednorazowa próba na Denon Home 200 potwierdziła odmowę `play_stream` dla stacji „357” z Ulubionych; głośność 0 nie była przyczyną tej odmowy. Źródła kierują teraz Ulubione do `play_preset`, lecz tej nowej komendy nie sprawdzono jeszcze na głośniku.
- Dodano źródłową obsługę 12 presetów AMC dla stacji HEOS z przypisaniem do konkretnego urządzenia i stacji oraz ponownym sprawdzeniem jej bieżącej pozycji przed odtworzeniem.
- Lista głównych wyjść bezprzewodowych oznacza cel, odczyt bieżącego materiału dołącza aktywny odbiornik, a skróty nawiasów przełączają komputer, Cast i AirPlay z komunikatem celu. Oddzielna trasa głównego AirPlay wycisza lokalny silnik podczas transmisji i przywraca jego stan przy powrocie lub błędzie. Wskaźnik aktualnie grającej pozycji na wszystkich listach i skróty Mirror pozostają otwarte.
- Dodano źródłowo listę aktywnego odtwarzania Shift+F6, przejście Enter i sterowanie wskazaną pozycją przez Spację. Dołączenie komputera jako Mirror do Cast odtwarzanego poza AMC jest odrębnym, niewdrożonym wariantem; nie ma przypisanego skrótu.
- Zwykłe Radio nie pokazuje polecenia kolejki; Radio Archiwista zachowuje swoją kolejkę. Opcjonalna wspólna kolejka ma osobną kartę QUEUE-003 i nie została wdrożona.

## Stan weryfikacji

Poprawki HEOS: 191 celowanych testów zaliczonych, kolejne 50 testów profilu i nawigacji zaliczonych. Cast: 39 testów zaliczonych w osobnym procesie z rzeczywistym ukrytym oknem wx; most NVDA: 8 zaliczonych na atrapach; wspólne powierzchnie poleceń i presety: 113 zaliczonych. `py_compile` i `git diff --check` zaliczone. Zestawy częściowo nakładają się; liczb nie sumować. Brak odbioru fizycznego NVDA, odsłuchu nowej komendy, buildu, instalacji i wydania GitHub. Wydana i zainstalowana pozostaje AMC Test alpha.525. Wersja i commit następnego wydania nie są jeszcze nadane.

Kolejny etap CAST-003: 116 celowanych testów skrótów/menu/palety/Mirror oraz 39 testów tras z ukrytym wx, a 4 testy katalogu polskiego i angielskiego zaliczone. Wykryte po drodze kolizje liter dostępu poprawiono. Nie wykonano sprzętowego przełączenia Cast ani odbioru NVDA; dokładny zakres znajduje się w `CAST-003-20261124-szybka-zmiana-wyjscia.md`.

Etap CAST-004: 223 celowane testy zaliczone, 1 pominięty; osobno 40 testów z ukrytym wx, 4 testy parytetu języka i 1 test odizolowanego hosta zaliczone. Host Release skompilował się bez ostrzeżeń, ale nie ma jeszcze instalatora ani wydania. Odsłuch na prawdziwym AirPlay i NVDA pozostają do odbioru. Pełny przebieg `test_i18n.py` miał także 2 niezaliczone szerokie testy inwentarza tłumaczeń, niewchodzące w zakres tej poprawki.
