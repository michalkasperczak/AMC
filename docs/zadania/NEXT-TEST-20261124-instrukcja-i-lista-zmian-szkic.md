# Następny AMC Test — szkic instrukcji i listy zmian, 24.11.2026

Ten tekst wynika z kart HEOS-001, AM-003 i CAST-002. Jest szkicem do korekty Michała. Nie opisuje zainstalowanej alpha.525 i nie jest opublikowaną listą zmian.

## Instrukcja użytkownika — szkic

- W sesji HEOS `Ctrl+L` otwiera katalog. Na liście Ulubionych Enter ma uruchamiać wybraną stację na wybranym głośniku; AMC używa do tego funkcji Ulubionych urządzenia. `Ctrl+Alt+Shift+P` przypisuje wybraną stację do presetu AMC, a `Ctrl+Shift+1` do `Ctrl+Shift+=` uruchamia zapisane miejsca 1–12. Po przestawieniu Ulubionych preset ma nadal wskazywać tę samą stację; gdy stacja zniknie, program poda jej brak.
- `Shift+C` otwiera odbiorniki Google Cast bieżącej sesji. Lista oznacza aktualne wyjście słowem „wybrane”, również gdy jest nim „Ten komputer”. Dostępny odczyt bieżącego materiału ma podać tytuł oraz nazwę aktywnego odbiornika, jeżeli AMC zna oba z bieżącej trasy Cast.
- Strzałka w lewo ma podawać dostępne informacje o zaznaczonej stacji lub materiale. Jeśli HEOS nie udostępnia parametrów strumienia, AMC ma to powiedzieć wprost. Przechodzenie strzałkami po listach ma sygnalizować pozycję aktualnie grającą; ten fragment pozostaje do implementacji i odbioru.

## Lista zmian — szkic

- Rozdzielono odmowę polecenia HEOS od braku późniejszego odczytu statusu. Jednorazowa próba na Denon Home 200 potwierdziła odmowę `play_stream` dla stacji „357” z Ulubionych; głośność 0 nie była przyczyną tej odmowy. Źródła kierują teraz Ulubione do `play_preset`, lecz tej nowej komendy nie sprawdzono jeszcze na głośniku.
- Dodano źródłową obsługę 12 presetów AMC dla stacji HEOS z przypisaniem do konkretnego urządzenia i stacji oraz ponownym sprawdzeniem jej bieżącej pozycji przed odtworzeniem.
- Lista Cast oznacza wybrane wyjście, a odczyt bieżącego materiału przez most NVDA dołącza aktywny odbiornik. Wskaźnik aktualnie grającej pozycji na wszystkich listach, szybkie przełączanie Cast/Mirror i docelowe miejsce AirPlay pozostają otwarte.
- Zwykłe Radio nie pokazuje polecenia kolejki; Radio Archiwista zachowuje swoją kolejkę. Opcjonalna wspólna kolejka ma osobną kartę QUEUE-003 i nie została wdrożona.

## Stan weryfikacji

Poprawki HEOS: 191 celowanych testów zaliczonych, kolejne 50 testów profilu i nawigacji zaliczonych. Cast: 39 testów zaliczonych w osobnym procesie z rzeczywistym ukrytym oknem wx; most NVDA: 8 zaliczonych na atrapach; wspólne powierzchnie poleceń i presety: 113 zaliczonych. `py_compile` i `git diff --check` zaliczone. Zestawy częściowo nakładają się; liczb nie sumować. Brak odbioru fizycznego NVDA, odsłuchu nowej komendy, buildu, instalacji i wydania GitHub. Wydana i zainstalowana pozostaje AMC Test alpha.525. Wersja i commit następnego wydania nie są jeszcze nadane.
