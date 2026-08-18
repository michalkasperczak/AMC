# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-051`
- Tytuł zestawu: Odtwarzacz jako osobny widok, informacje i pamięć sesji
- Wersja programu: `0.1.0-alpha.51`
- Utworzono: 2026-08-18 16:18, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-18_1618_0.1.0-alpha.51.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-051-01 — Krótki pasek stanu

1. Otwórz lokalny plik, rozpocznij odtwarzanie i naciśnij `NVDA+End`.
2. Wstrzymaj, zmień prędkość na 1,50 razy i ponownie odczytaj pasek.
3. Przejdź do demonstracyjnej sesji bez parametrów audio i odczytaj pasek.

Oczekiwane:

- pasek lokalnego pliku ma kolejność: wartości audio, stan, czas, tytuł, usługa;
- mówi np. „322 kb/s, 48 kHz”, bez słów „przepływność” i „około”;
- głośność nie występuje, a zmieniona prędkość występuje po stanie;
- przy braku parametrów komunikat zaczyna się od stanu, bez „brak danych audio”;
- pasek nie przejmuje fokusu i nie wywołuje automatycznej wypowiedzi co sekundę.

## AMC-051-02 — Jedno okno Właściwości i informacje

1. Na liście zaznacz element inny niż odtwarzany i naciśnij `Alt+Enter`.
2. Czytaj tekst strzałkami oraz Home i End, użyj `Ctrl+A` i `Ctrl+C`.
3. Zamknij okno Enterem, otwórz ponownie i zamknij Escape.
4. W odtwarzaczu użyj `Alt+Enter`.

Oczekiwane:

- na liście dane dotyczą zaznaczenia, a w odtwarzaczu faktycznie bieżącego elementu;
- okno zawiera zrozumiałe sekcje Element, Odtwarzanie, Techniczne i Źródło, jeśli są dla nich dane;
- dla pliku lokalnego są ścieżka, format i rozmiar; nie ma technicznych tokenów ani adresów podpisanych;
- tekst jest dostępny do czytania, zaznaczania i kopiowania;
- Enter i Escape zamykają okno, a fokus wraca dokładnie do listy albo odtwarzacza.

## AMC-051-03 — Usunięcie starych poleceń informacji

1. Na liście naciśnij `Ctrl+I`, a potem `Ctrl+Shift+I`.
2. W odtwarzaczu naciśnij te same skróty oraz zwykłe `I`.
3. Otwórz paletę `Ctrl+Shift+K` i wyszukaj „właściwości”, „informacje” oraz „stan odtwarzania”.
4. Sprawdź menu Odtwarzanie i menu kontekstowe listy.

Oczekiwane:

- stare skróty nie uruchamiają dawnych okien ani dodatkowego odczytu stanu;
- zwykłe `I` nie zmienia głośności i nie naciska przycisku;
- paleta i menu zawierają jedno polecenie „Właściwości i informacje, Alt+Enter”;
- nie ma osobnych poleceń „Informacje”, „Rozszerzone informacje” ani „Odczytaj stan odtwarzania”.

## AMC-051-04 — Działania na bieżącym utworze w odtwarzaczu

1. Uruchom element, wejdź do odtwarzacza i użyj `Shift+Enter`.
2. Użyj kolejno `Ctrl+Shift+U`, `Ctrl+Shift+L`, `Ctrl+Shift+P` i `Ctrl+Shift+Enter`.
3. Wróć Escape i sprawdź Kolejkę, Ulubione oraz Bibliotekę.
4. Powtórz wybrane przełączniki, aby usunąć element.

Oczekiwane:

- wszystkie działania dotyczą utworu widocznego w odtwarzaczu, a nie ukrytego zaznaczenia listy;
- `Shift+Enter` rzeczywiście dodaje lub usuwa z kolejki;
- skróty Ulubionych, Biblioteki, playlist i „odtwarzaj jako następne” działają i podają krótkie potwierdzenie;
- ponowne wykonanie przełącznika odwraca stan bez utraty fokusu.

## AMC-051-05 — Granica odtwarzacza i wyszukiwania

1. Będąc w odtwarzaczu, naciśnij kolejno `Ctrl+K`, `Ctrl+F` i `Ctrl+Shift+F`.
2. Następnie użyj `Ctrl+U`, `Ctrl+P`, `Ctrl+L`, `Ctrl+Q` i `Ctrl+Shift+A`, za każdym razem wracając do odtwarzacza przez `F6`.

Oczekiwane:

- filtr i oba wyszukiwania nie otwierają okna ani pola nad odtwarzaczem;
- każde z nich krótko mówi „Wyszukiwanie jest dostępne na listach”;
- skróty widoków świadomie opuszczają odtwarzacz i pokazują żądaną listę;
- F6 zawsze wraca do odtwarzacza bieżącej sesji.

## AMC-051-06 — Escape wraca do ostatniego miejsca

1. Uruchom utwór z Multimedia i wróć Escape.
2. Wejdź do Ulubionych, ustaw fokus na wybranym elemencie i naciśnij `F6`.
3. Naciśnij Escape.
4. Powtórz z Biblioteką albo Kolejką oraz z aktywnym filtrem.

Oczekiwane:

- Escape po F6 wraca do listy i elementu, z których F6 użyto ostatnio, nie do pierwotnego miejsca uruchomienia utworu;
- wraca także właściwy filtr danego widoku;
- dźwięk trwa, a zaznaczenie nie jest przestawiane na bieżący utwór.

## AMC-051-07 — Osobna pamięć każdej sesji

1. W TIDAL ustaw widok Ulubione, filtr i konkretny element, a następnie otwórz odtwarzacz przez F6.
2. Przejdź do Apple Music, ustaw inny widok i element, pozostawiając zwykłą listę.
3. Kilka razy przełączaj `Ctrl+1` i `Ctrl+2`.
4. Zamknij AMC przez `Alt+F4`, uruchom ponownie i ponów przełączanie.

Oczekiwane:

- TIDAL wraca do odtwarzacza, a Apple Music do własnej listy;
- po Escape w TIDAL wracają jego Ulubione, filtr i element;
- Apple Music zachowuje własny widok, filtr i element, bez kopiowania stanu TIDAL;
- zapis przetrwa prawidłowe zamknięcie i ponowne uruchomienie programu.

## AMC-051-08 — Regresja najważniejszych funkcji

1. Sprawdź otwieranie pliku i folderu, Enter, `Ctrl+Enter`, Spację, przewijanie, cyfry, skoki do czasu i procentu oraz prędkość.
2. Sprawdź filtr na liście, wyszukiwanie bieżące i globalne oraz bezpośrednie dodanie wyniku do kolejki.
3. Sprawdź paletę poleceń, ustawienia, Ctrl+Z, menu kontekstowe i zamknięcie `Alt+F4`.

Oczekiwane:

- wcześniejsze funkcje działają bez regresji;
- wyszukiwanie globalne nadal może znaleźć plik lokalny i wykonać na nim działanie;
- nie pojawiają się techniczne identyfikatory, „Stan programu”, podwójne komunikaty ani utrata fokusu;
- główne okno zamyka aplikację, a nie zachowuje się jak Escape.
