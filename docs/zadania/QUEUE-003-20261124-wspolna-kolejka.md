# QUEUE-003 — opcjonalna wspólna kolejka materiałów

Stan: wymaganie zapisane 24.11.2026; nie zaimplementowano, nie budowano i nie wydano.

## Cel i zakres

Michał chce w ustawieniach możliwość wyboru, czy lokalne pliki, podcasty i ewentualnie inne odtwarzalne materiały mają trafiać do jednej wspólnej kolejki, czy pozostać w kolejkach sesji. Wspólna kolejka ma być przewidywalnym kontenerem, którego lista i polecenia pokazują tylko rzeczywiście obsługiwane pozycje. Zwykłe Radio na żywo nie ma kolejki; jest to osobna poprawka w `QUEUE-002-przeglad-kolejek-sesji-internetowych.md`. Kolejki sterowane przez zewnętrzne urządzenia lub konta nie mogą udawać, że należą do lokalnego hosta.

## Uzasadnienie i decyzje

- Ustawienie powinno jawnie wybierać tryb „kolejki osobne” albo „wspólna kolejka”, z domyślnym zachowaniem dotychczasowym dla istniejących profili. Zmiana ustawienia nie może bez pytania usuwać ani scalać zapisanych pozycji.
- Każdy wpis wspólnej kolejki przechowuje stabilną tożsamość, rodzaj materiału, sesję źródłową i wskazanie sposobu odtworzenia. Zwykły strumień Radia nie jest pozycją kolejki; odcinek podcastu i lokalny plik mogą być, jeśli istnieje działający adapter odtwarzania.
- Przejście między różnymi silnikami odtwarzania musi jawnie przełączyć kontekst, zachować fokus i ogłosić materiał. Wpis, którego źródło znikło, ma dać czytelny błąd i możliwość pominięcia lub usunięcia, bez zatrzymania całej kolejki. Nie wolno przenosić technicznych identyfikatorów do etykiet NVDA.
- Filtrowanie jest tylko widokiem. Przenoszenie, `Ctrl+X`, wklejanie, Delete, następny/poprzedni i zapis działają na stabilnych wpisach, a po operacji fokus zostaje na najbliższym widocznym wierszu. Sortowanie nie może po cichu zmienić trwałego ręcznego porządku.
- Menu, paleta, `Ctrl+F1`, pełna pomoc i skróty muszą czerpać dostępność z tego samego modelu możliwości: na żywym Radiu kolejka pozostaje ukryta, a nieobsługiwane źródło nie dostaje martwego „Dodaj do wspólnej kolejki”.

## Testy i wynik

Na razie brak kodu i testów tej opcji. Rozpoznano istniejące osobne kolejki Plików, Podcastów, TIDAL, Spotify, Radio Archiwisty i HEOS. Poprawka wyłączenia kolejki w zwykłym Radiu ma 118 zaliczonych testów celowanych we wspólnej gałęzi, lecz nie jest implementacją QUEUE-003.

## Materiał do dokumentacji użytkownika — szkic

Ustawienie „Sposób kolejki” wybierze kolejki osobne lub jedną wspólną kolejkę dla obsługiwanych plików i odcinków. Zwykłe stacje Radia nadal grają na żywo i nie są dopisywane. Pozycje z różnych źródeł będą miały czytelną nazwę i źródło, a AMC poinformuje, gdy któregoś materiału nie da się już odtworzyć. To jest szkic planowanej funkcji, nie opis obecnej wersji.

## Znane ograniczenia, migracja, wersja i commit

Nie ustalono jeszcze technicznego połączenia lokalnego hosta z odtwarzaniem usług zewnętrznych. W pierwszym etapie trzeba rozpoznać zdolność każdego adaptera i zaprojektować zapis bez utraty starych kolejek. Nie ma wersji ani commitu implementacyjnego. Następny krok: tabela możliwości źródeł i scenariusze migracji, po czym testy parytetu poleceń oraz przepływu mieszanego lokalny plik → podcast → niedostępny materiał.
