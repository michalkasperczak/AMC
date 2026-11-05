# LIST-20261105 — czas TIDAL i koszt strzałek na listach

Status: wydane na GitHubie i zainstalowane w AMC Test alpha.472; pomiar z NVDA na wolniejszym łączu i odbiór użytkownika otwarte.

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

Gałąź `codex/konfigurowalne-skroty-20261026`, baza `91511480`. Wydanie `0.1.0-alpha.472`, commit źródeł `5bce69ca`; wynik budowy, instalacji i GitHub opisano poniżej.

## Wynik wydania i instalacji — 5.11.2026, 12:48

Commit źródeł `5bce69ca7e3a050aa324a31cd0e3b5ee3e602cd3` został wysłany do `michalkasperczak/AMC`. Wydanie wstępne kanału test `test-2026.11.05.1248` opublikowano i odczytowo potwierdzono: instalator 143825210 bajtów, SHA-256 `f029b793830695d2f750dc4dfc8f7c0be531ed76fce360cb6c8108b244f3fb28`, plik integralności i manifest aktualizacji mają stan `uploaded`. Paczka Test zawiera 4194 pliki, `sourceDirty=false`, pełna kontrola i próbny start GUI/hosta przeszły. Testy poleceń i list Python: 160 zaliczonych; host: trzy zestawy protokołu zaliczone, kompilacja bez ostrzeżeń.

Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\AMC-Test-2026-11-05-1248.exe`. Stare AMC zamknięto łagodnie przez okno; jego własna bramka pobiera świeży stan nagrywania i odmawia zamknięcia podczas nagrania. Instalator zakończył się kodem 0. Zainstalowane `C:\Users\micha\AppData\Local\Programs\AMC Test\AMC.exe` ma ProductVersion `0.1.0-alpha.472`, dokładnie ten commit, `bundleId=225e63ef`; pełna kontrola instalacji zakończyła się kodem 0. Uruchomiono jeden launcher, jeden GUI i jeden host z tej instalacji; tytuł okna „AMC Test — 05.11.2026, 12:48”. Dodatek NVDA dołączono opcjonalnie do instalatora, ale nie instalowano go i nie restartowano NVDA.

Odbiór mowy NVDA, działania nowych okien na żywym utworze TIDAL i pomiar płynności list na wolnym łączu pozostają otwarte. Kompilacja, testy i integralność nie zastępują tych prób. Limit Codex przy zakończeniu: 20% użyte, 80% pozostało, reset 11.11.2026 o 07:45 czasu warszawskiego.
