# PLAYER-20261105 — skok do czasu, procentu i zakładki

Status: wydane na GitHubie i zainstalowane w AMC Test alpha.472; ręczny odbiór NVDA otwarty.

## Zgłoszenie i korekta użytkownika — 5.11.2026

Wcześniejszy audyt uznał znaczenie „skocz do terka” i liter G/Shift+G za nieustalone. Użytkownik doprecyzował później: chodzi o czas **bieżącego utworu**; `J` otwiera wpisanie czasu, `Shift+J` wpisanie procentu. Ta decyzja zastępuje wcześniejsze wątpliwości i opis „osobnego etapu”. `G` i `Shift+G` pozostają bez nowego przypisania. `Ctrl+Shift+G` nadal steruje komunikatami skoków.

## Zachowanie

- W widoku odtwarzacza `J` otwiera dostępne z klawiatury okno „Skocz do czasu”. Przyjmuje `mm:ss` albo `hh:mm:ss`; sprawdza zakres i długość bieżącego materiału. `Shift+J` otwiera „Skocz do procentu”, przyjmuje liczbę 0–100 z opcjonalnym `%`. Escape anuluje; po obu zakończeniach fokus wraca do odtwarzacza.
- Polecenia są dostępne w menu Odtwarzanie, palecie, pomocy kontekstowej i pełnej pomocy przez wspólny model `Action`. J obsługuje sesje ze stałą długością: Pliki, Podcasty, Spotify, Radio Archiwista, Sonos i natywny TIDAL AMC Test. Shift+J działa także w Radiu na żywo jako procent bufora Time Shift. Oryginalny TIDAL poza AMC nie otrzymuje tych poleceń.
- W natywnym TIDAL skok do czasu pobiera aktualny stan na wątku roboczym, sprawdza identyfikator granego utworu i jego długość. Nie wykonuje skoku po zmianie utworu ani poza długość utworu.
- `B` i `Ctrl+Shift+B` zapisują szybką lub nazwaną zakładkę bieżącego utworu TIDAL. `Shift+Page Up/Down` oraz `Ctrl+Shift+Page Up/Down` przechodzą po zakładkach lub tylko nazwanych. Host zapisuje je w sesji `tidal`, oddzielnie od lokalnych plików i innych usług.
- Dotychczasowe cyfry 0–9 skaczą co 10%; Home/End działają według możliwości bieżącej sesji.

## Testy i stan

Testy parsowania, skrótów, menu, palety, fokusu okna, identyfikatora utworu, granic czasu i magazynu zakładek przeszły. Zbiorczy zestaw Python: 160 zaliczonych, w tym testy fokusu i rozróżnienia bufora Radia. Host .NET: kompilacja bez ostrzeżeń i błędów, trzy zestawy protokołu zaliczone. Ręczny odbiór dialogów i mowy NVDA w zainstalowanym AMC pozostaje do wykonania; symulacja nie zastępuje rzeczywistego czytnika.

## Materiał do szkicu instrukcji i listy zmian

W odtwarzaczu naciśnij `J`, wpisz czas `mm:ss` lub `hh:mm:ss` i zatwierdź Enterem. `Shift+J` pozwala podać procent od 0 do 100. Zakładkę dodaj `B`, nazwaną `Ctrl+Shift+B`; przechodź między nimi `Shift+Page Up/Down` lub między nazwanymi `Ctrl+Shift+Page Up/Down`. W TIDAL działa to dla utworów odtwarzanych bezpośrednio w AMC Test. Szkic wymaga korekty użytkownika przed uznaniem za ostateczną instrukcję.

## Wersja i commit

Gałąź `codex/konfigurowalne-skroty-20261026`, baza `91511480`. Wydanie `0.1.0-alpha.472`, commit źródeł `5bce69ca`; wynik budowy, instalacji i GitHub opisano poniżej.

## Wynik wydania i instalacji — 5.11.2026, 12:48

Commit źródeł `5bce69ca7e3a050aa324a31cd0e3b5ee3e602cd3` został wysłany do `michalkasperczak/AMC`. Wydanie wstępne kanału test `test-2026.11.05.1248` opublikowano i odczytowo potwierdzono: instalator 143825210 bajtów, SHA-256 `f029b793830695d2f750dc4dfc8f7c0be531ed76fce360cb6c8108b244f3fb28`, plik integralności i manifest aktualizacji mają stan `uploaded`. Paczka Test zawiera 4194 pliki, `sourceDirty=false`, pełna kontrola i próbny start GUI/hosta przeszły. Testy poleceń i list Python: 160 zaliczonych; host: trzy zestawy protokołu zaliczone, kompilacja bez ostrzeżeń.

Instalator: `D:\Projekty Codex\AMC - integracja wxPython\publish\installers\AMC-Test-2026-11-05-1248.exe`. Stare AMC zamknięto łagodnie przez okno; jego własna bramka pobiera świeży stan nagrywania i odmawia zamknięcia podczas nagrania. Instalator zakończył się kodem 0. Zainstalowane `C:\Users\micha\AppData\Local\Programs\AMC Test\AMC.exe` ma ProductVersion `0.1.0-alpha.472`, dokładnie ten commit, `bundleId=225e63ef`; pełna kontrola instalacji zakończyła się kodem 0. Uruchomiono jeden launcher, jeden GUI i jeden host z tej instalacji; tytuł okna „AMC Test — 05.11.2026, 12:48”. Dodatek NVDA dołączono opcjonalnie do instalatora, ale nie instalowano go i nie restartowano NVDA.

Odbiór mowy NVDA, działania nowych okien na żywym utworze TIDAL i pomiar płynności list na wolnym łączu pozostają otwarte. Kompilacja, testy i integralność nie zastępują tych prób. Limit Codex przy zakończeniu: 20% użyte, 80% pozostało, reset 11.11.2026 o 07:45 czasu warszawskiego.
