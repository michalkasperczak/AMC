# DOC-20261118 — uporządkowanie dostępu do plików AMC na głównym PC

Status: wykonano uporządkowanie dojścia do notatek i zapis podsumowania. Nie wykonano masowego przenoszenia ani kasowania dawnych katalogów AMC.

## Polecenie i zakres

Michał polecił zapisać podsumowanie jego zgłoszeń, uporządkować materiały AMC na dysku D głównego komputera i odnaleźć poniedziałkowy plik. Zakres ograniczono do AMC; pozostałe projekty w Hermes i Nowe bez zmian. Nie instalować, zamykać ani restartować AMC na głównym komputerze.

## Rzeczywisty odczyt

Połączenie SSH `michal-glowny`, potwierdzony komputer `MICHAL`, użytkownik `micha`, 18.11.2026. Przejrzano katalogi AMC bezpośrednio w `D:\Projekty Codex`, foldery `Hermes`, `Hermes\Nowe`, `Hermes\AMC`, `AMC - notatki`, znaczniki katalogów, manifesty dwóch instalacji, skróty AMC pulpitu i menu Start, procesy aplikacji i listę Git worktree. Nie wykonywano pełnego audytu integralności ani zawartości wszystkich dawnych katalogów.

Znaleziono oryginał `D:\Projekty Codex\AMC - notatki\Hubert zgłoszenia 2026-11-16.md`, zawierający również dopiski Michała. Pozostawiono jego nazwę i bajty bez zmiany. SHA-256 przed i po: `0A7E30E2EFC73F2FEFFD6BEBD2A727C840C46FD8D71C97ABE930EF5475D2B833`.

## Dostarczone i odczytane ponownie materiały

- `D:\Projekty Codex\AMC - notatki\00 - Zacznij tutaj.md` — mapa materiałów, wskazanie poniedziałkowego oryginału, właściwe aplikacje, stan kodu oraz role zachowanych kopii i budów.
- `D:\Projekty Codex\AMC - notatki\AMC - podsumowanie moich zgłoszeń 2026-11-18.md` — podsumowanie własnych zgłoszeń Michała z poprzedniej odpowiedzi, z ograniczeniami odbioru. Nie przypisuje mu późniejszych zgłoszeń Huberta #19/#20.
- `D:\Projekty Codex\Hermes\AMC\Czytaj najpierw.md` — krótka instrukcja dotarcia do notatek i oryginału.

Dodane wyłącznie nowe skróty `.lnk`:

- `D:\Projekty Codex\AMC - zadania i notatki.lnk` → `D:\Projekty Codex\AMC - notatki`.
- `D:\Projekty Codex\Hermes\AMC\Notatki i podsumowania.lnk` → ten sam folder notatek.
- `D:\Projekty Codex\Hermes\AMC\Zadania z poniedziałku.lnk` → oryginał z 16 listopada.

Świeży odczyt potwierdził pełne bajty i SHA-256 wszystkich trzech dokumentów, dokładne cele wszystkich trzech skrótów i istnienie tych celów. Nie klikano ani nie uruchamiano skrótów. Nie powielono oryginału w folderze Hermes. Nowe notatki AMC mają trafiać do istniejącego `AMC - notatki`.

## Stan aplikacji i kodu, niezmieniony porządkami

- Test działał z `D:\Projekty Codex\AMC Python\AMC.exe`; skróty pulpitu i menu Start prowadzą do tego pliku. Manifest: profil `test`, `18.11.2026, 03:37`, source `1723786c4557ef68bb3922189a028099ce96db31`.
- Public: skrót menu Start prowadzi do `D:\Projekty Codex\AMC Public\AMC.exe`; manifest nadal `14.11.2026, 16:45`, source `aafa2bc3a6cbacaf892df96b1414f7ef3d50b589`. Nie jest to nowe opublikowane wydanie Public. Bez aktualizacji na głównym PC.
- PID-y Test `28144` (launcher), `36472` (Python GUI), `42020` (host) nie zmieniły się w odczycie po dostawie. Nie potwierdzano interaktywnie mowy, odtwarzania i nagrywania; nie były potrzebne do zapisania nowych dokumentów.
- Lokalny kod `AMC - integracja wxPython` pozostaje na `26950509629a2092b0cc4e7987e5a370d740db31`, starszy niż źródła wydania. Zachowano dokładnie dwa istniejące zmodyfikowane MD: `AMC_PYTHON_STAN_2026-10-10_PL.md`, `PROJEKT_SONOS_PL.md`. Nie wykonywano resetu, pull, synchronizacji ani czyszczenia Git. Rozwój bieżącego źródła odbywa się na Hermesie.

## Dlaczego nie kasowano dawnych katalogów

Część dawnych budów jest nadal zarejestrowana jako Git worktree. Kopie prywatne mogą zawierać ustawienia i bazy; odrębne repozytoria mogą zawierać niescalone lub niewysłane zmiany. Nieużywanie ich przez proces AMC nie dowodzi zbędności. Nie potwierdzono kompletnego zastąpienia tych danych, więc nie przenoszono ich i nie kasowano. Stare nazwy opisano w mapie jako katalogi techniczne do osobnej oceny, nie jako bieżącą aplikację lub miejsce zgłoszeń.

Usunięto wyłącznie własny nowy plik transportowy JSON z TEMP po zweryfikowaniu dostawy; odczyt potwierdził jego brak. Nie odzyskiwano miejsca kosztem danych użytkownika. Nie deklarować pełnego sprzątnięcia dysku.

## Dowody

Na stanowisku roboczym, trwały zapis: `/home/michal/.hermes/profiles/amc/state/amc-mainpc-order-20261118/` — `inspect.json`, `preflight.json`, `delivery.json`, `delivered.json`, `verified.json`, `cleanup.json` i oryginalne bajty dokumentu poniedziałkowego w polu Base64 inspekcji. Zachowano również trzy dostarczone dokumenty; dziewięć plików dowodowych porównano po SHA-256 z roboczymi odpowiednikami. To lokalne dowody techniczne; dostarczone dokumenty są na komputerze MICHAL, nie tylko w Telegramie lub WSL.
