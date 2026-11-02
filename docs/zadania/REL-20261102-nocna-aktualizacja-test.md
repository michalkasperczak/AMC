# REL-20261102 — wspólna aktualizacja AMC Test po 02:00

Status: **W trakcie — uruchomione 02.11.2026 o 02:05 Europe/Warsaw**. Harmonogram wstrzymany na początku przebiegu.

## Zgoda i zakres użytkownika — 01.11.2026

Użytkownik zlecił publikację na GitHubie i instalację aktualizacji po drugiej w nocy, obejmującą wszystkie najnowsze zadania, zwłaszcza Spotify i poprawę Shift+A. Zgoda obejmuje przygotowanie i sprawdzenie wspólnej paczki AMC Test, zapis sprawdzonych źródeł, publikację prerelease w `michalkasperczak/AMC` i aktualizację lokalnej instalacji. Nie wymaga ponownego pytania o te czynności. Publikacja i instalacja nie mogą nastąpić przed 02.11.2026 o 02:00 czasu polskiego.

Zakres najnowszego przyrostu:

- `CAST-001`: Shift+C i podstawowy Cast dla Plików, Radia i Podcastów. Użytkownik zaliczył odbiór listy z NVDA. Shift+A pozostaje osobnym wyborem toru sesji. Mirroring nie jest wdrażany.
- `SPOTIFY-20261101-album-kolejka-komunikaty.md`: Ctrl+Enter dla całego albumu, prywatna kolejka AMC pod Ctrl+Q / Ctrl+Shift+Q, komunikaty po potwierdzonym starcie i uczciwa dostępność tempa. Trwałe tempo odcinków Spotify pozostaje otwartą potrzebą.
- `RARCH-001`: poprawki Spacji i Shift+A z 01.11, w tym kierowanie transportu według sesji, urządzenia WiiM/Sonos/Frontier Smart i obsługa Spacji na listach.
- Uwzględnić wcześniejsze gotowe, jeszcze niewydane poprawki obecnej linii (m.in. pobieranie i wycinki Radio Archiwisty, pasek stanu, tytuł, gotowe poprawki Spotify). Przejrzeć kartę każdej dołączanej zmiany; nie rozpoczynać pozostałych pomysłów z rejestru. Nie oznaczać brakującego ręcznego odbioru jako zaliczonego.

## Uzasadnienie i decyzje

Jedna spójna paczka ma zastąpić instalację użytkową. Nie instalować starego kandydata alpha.461 z harmonogramu poprzedniej nocy: nie zawiera najnowszego zakresu. Kanoniczny katalog to `D:\Projekty Codex\AMC - integracja wxPython`, bieżąca gałąź `codex/konfigurowalne-skroty-20261026`. Zachować istniejące zmiany robocze, dane i profile. Przed pakowaniem ustalić dokładny przyrost, testy i commit; nie wykonywać bezrefleksyjnego `git add .`. Nie publikować danych kont, poświadczeń, prywatnych profili, nagrań ani dzienników.

## Bezpieczna instalacja

Przed zatrzymaniem lub zastąpieniem AMC sprawdzić faktyczne procesy, aktywne nagrywanie, harmonogramy i inne istotne operacje. Jeżeli bezpieczeństwa nie da się potwierdzić, zachować działającą aplikację, odroczyć instalację i podać przyczynę. Nie kończyć zbiorczo procesów Python/.NET/Chrome. Nie restartować NVDA bez potrzeby. Aktualizować profil Test; nie zmieniać Public.

Po budowie zweryfikować wersję, commit, manifest, integralność i skan sekretów. Po publikacji sprawdzić zdalny commit, tag prerelease, nazwę, rozmiar i SHA-256 instalatora. Po instalacji sprawdzić manifest i integralność rzeczywistej instalacji, uruchomić właściwy stabilny launcher oraz potwierdzić ścieżkę i jedną instancję. Źródła, testy, pakiet, GitHub, instalację i start raportować oddzielnie.

## Limit

Odczyt 01.11.2026: zwykłe użycie dozwolone, 75% wykorzystane, 25% pozostało; okno 10080 minut, reset 04.11.2026 o 07:45 Europe/Warsaw (1793774737). Brak płatnych kredytów, jeden dostępny bezpłatny reset niewykorzystany i nieautoryzowany do zużycia. Odczytać limit ponownie przed dłuższą budową i podczas istotnych etapów. Przy 20% nie rozpoczynać opcjonalnych prac; poniżej 15% zatrzymać się w bezpiecznym punkcie, zapisać wynik i powiadomić użytkownika. Brak danych o limicie wymaga ostrożnej decyzji.

## Testy i wynik

Końcowa bramka integracji: **597 zaliczonych testów Python w 33 modułach, bez niepowodzeń**, każdy moduł uruchomiony w osobnym procesie pytest 9.0.2, Python 3.14 / wxPython 4.3.1 z `AMC - zależności\python-test-envs\wxpython-4.3.1-cp314`. Polecenia i logi: `C:\Users\micha\Documents\Codex\2026-10-29\a-3\work\run_nightly_checks.py` i `nightly-20261102`. Obejmują Cast, hosta, audio, transport, Spotify, menu/paletę/skróty, archiwum, historię pobrań, presety, aktualizacje, trzy sesje głośników oraz tłumaczenia. Poprawiono brak Spotify w widoczności menu kolejki, uzupełniono atrapę skrótów przypisywania presetów i brakujące tłumaczenia nowych komunikatów. Ponowiono wyłącznie dotknięte zestawy, a podana liczba obejmuje końcowy wynik każdego modułu raz. **5 zestawów protokołu zaliczonych**: audio, kierowanie transportu, katalog Spotify, historia odtwarzania i zakładki. Pierwsze uruchomienie .NET wymagało odtworzenia brakującego `project.assets.json`; po standardowym restore testy przeszły. Izolowany host Release: 0 ostrzeżeń i 0 błędów. Nie otwierano widocznych okien ani nie odtwarzano na fizycznych urządzeniach.

Zachowano nowsze funkcje istniejącej instalacji: historia pobrań i schowek, niemodalne konta, presety archiwum, wirtualne listy oraz bramki nagrywania aktualizatora. [Szkic wspólnej instrukcji i listy zmian](REL-20261102-instrukcja-i-lista-zmian-szkic.md) wymaga korekty użytkownika.

02.11.2026: zastano instalację z commitu `13dde8b3e14f61cf331843d5bbbc23ae9a92b102`, nowszą niż główna gałąź robocza. Aby zachować historię pobrań, poprawki okien, presety archiwum, parametry Radia i ochronę aktualizacji podczas nagrywania, scalanie wykonuje się w istniejącym czystym worktree `D:\Projekty Codex\AMC build worktrees\amc-test-title-20261031`, baza `77ebbc92a817b0d0e48f247bc5538e5101dfa755`. Zapis roboczych zmian `a3561e6ce8e937e89e7f677188cc013643d909fd` sporządzono przez osobny indeks, bez zmiany gałęzi, indeksu i plików głównego repozytorium. Rozwiązano konflikty z zachowaniem nowszych funkcji. Trwają testy integracji; nie ma jeszcze nowego pakietu ani publikacji.

Limit przy starcie: 76% wykorzystane, 24% pozostało, zwykłe użycie dozwolone; reset bez zmiany. Dostępne są już dwa bezpłatne resety, żadnego nie użyto. W procesach AMC nie było potomnego FFmpeg, a ostatnie pobliskie okno harmonogramu zakończyło się o 02:00. Przed instalacją nadal wymagana świeża kontrola stanu.

Na etapie planowania: brak nowej kompilacji, paczki, instalacji i publikacji. Dowody dotychczasowych testów znajdują się w kartach wymienionych zadań. Zastosować testy celowane do wspólnego przyrostu, bez rutynowej pełnej regresji GUI i bez odsłuchów na urządzeniach w nocy. Przyszły ręczny odbiór nowych elementów Spotify i Shift+A pozostaje osobnym krokiem.

## Materiał do dokumentacji użytkownika

Przygotować roboczą listę zmian wspólnej aktualizacji z dokładnymi skrótami, różnicami sesji i ograniczeniami. Oddzielić rzeczywiście dołączone funkcje od otwartych pomysłów. Podać pełną lokalną ścieżkę instalatora i opublikowany adres wydania (link opisowy oraz osobno pełny URL). Nie oznaczać szkicu instrukcji jako zatwierdzonego.

## Wersja i commit

Nowy kandydat: `0.1.0-alpha.465`. Dokładny commit, manifest i datę paczki zapisać po budowie. Nie przypisywać starej wersji alpha.461 nowemu zakresowi.

01.11.2026 zaktualizowano istniejący harmonogram `amc-test-alpha-461-nocna-instalacja-i-publikacja`, bez tworzenia duplikatu. Nazwa: „AMC Test — najnowsze poprawki, GitHub i instalacja po 02:00”. Stan ACTIVE, jednorazowo o 02:05, przypisany do bieżącej rozmowy `01a18547-613d-7232-bc1c-a13d4222464b`. Odczyt zapisanej konfiguracji potwierdził termin, stan i przypisanie. Harmonogram ma datowaną bramkę 02.11.2026, okno 02:00–06:00 i obowiązek wstrzymania powtórzeń. W tej chwili zadanie jest zaplanowane; nie wykonano publikacji ani instalacji.
