# LIST-20261105 — czas TIDAL i koszt strzałek na listach

Status: poprawka źródeł i testy gotowe; pomiar z NVDA na wolniejszym łączu i odbiór użytkownika otwarte.

## Zgłoszenie z 5.11.2026

Na albumie lub utworze TIDAL użytkownik chce słyszeć samą długość, np. „45:21”, bez „Czas”. Przy wolniejszym łączu strzałki góra/dół w TIDAL, Spotify i Radiu Archiwiście reagują z opóźnieniem. Oczekuje płynnego poruszania się po 150–200 załadowanych pozycjach.

## Wynik analizy i zmiana

- `MediaListCtrl.OnGetItemText` zwraca gotowy tekst z pamięci. `FOCUSED` i `SELECTED` wybierają ID w modelu oraz przeliczają stan menu; nie pobierają danych z sieci ani bazy. TIDAL odsłania lokalną stronę 150 wierszy z migawki, Spotify ładuje kolekcję przy wejściu lub dalszej stronie, Radio Archiwista przy wejściu na poziom lub odświeżeniu.
- Windows może wywołać `FOCUSED` i `SELECTED` dla jednej strzałki. Jeśli `FOCUSED` ustawił już ten sam wiersz, `SELECTED` pomija drugie przeliczenie menu w TIDAL, Spotify i Radiu Archiwiście. Podcasty zachowują obsługę wielokrotnego zaznaczenia.
- Krótki odczyt lewej strzałki w TIDAL zwraca sam czas, np. „45:21”. Informacja o formacie pozostaje osobnym komunikatem.
- Przegląd kodu nie dowodzi przyczyny całej zauważalnej zwłoki. Wcześniejsze TID-001 wykazało okresowe obciążenie hosta audio, ale bez potwierdzonego związku ze strzałkami. Potrzebny pomiar na tym samym wczytanym widoku z NVDA przy aktywnym i zatrzymanym odtwarzaniu. Nie przypisujemy opóźnienia wolnemu łączu bez pomiaru.

## Testy i stan

Zbiorczy zestaw Python obejmujący listy, TIDAL, skróty, menu i paletę: 160 zaliczonych. Test zdarzeń potwierdza jedno przeliczenie menu dla pary `FOCUSED`/`SELECTED` w każdej z trzech sesji; test odczytu potwierdza „45:21” bez prefiksu. Wcześniejszy stary moduł `test_gui_logic.py` ma testy wymagające nieaktualnego sposobu tworzenia obiektu wx i nie jest dowodem błędu tej poprawki. Żywego pomiaru opóźnienia i odbioru NVDA jeszcze nie wykonano.

## Szkic instrukcji i listy zmian

Po lewej strzałce na utworze lub albumie TIDAL aplikacja podaje długość bez słowa „Czas”. Strzałki pionowe używają już załadowanych pozycji; dalszą stronę odsłania „Załaduj więcej”. W Spotify i Radiu Archiwiście przejście na inny poziom lub odświeżenie może wymagać sieci. Szkic wymaga potwierdzenia z NVDA przez użytkownika.

## Wersja i commit

Gałąź `codex/konfigurowalne-skroty-20261026`, baza `91511480`. Kandydat `0.1.0-alpha.472`; commit, build, instalacja i publikacja do uzupełnienia po wykonaniu.
