# AMC alpha 342 i dodatek NVDA 0.2.1

Użytkownik po testach zaakceptował wersję do publikacji 11 września 2026 r.
Podczas przygotowywania i publikowania pakietu nie jest wymagany restart AMC
ani NVDA; aktualizacji nie należy instalować podczas nagrywania.

## Pliki

- AMC: `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.342\AccessibleMediaController-0.1.0-alpha.342.exe`
- Dodatek: `D:\Projekty Codex\Accessible Multimedia Controller\AMC-NVDA-0.2.1.nvda-addon`
- Pełna mapa: `D:\Projekty Codex\Accessible Multimedia Controller\INSTRUKCJA_NVDA_PL.md`

Gdy nic nie nagrywasz, zamknij wcześniejszy AMC i uruchom alpha 342. Otwórz pakiet
dodatku w Windows, zatwierdź aktualizację w NVDA i postępuj według jego instrukcji.
Nie musisz instalować pośredniej wersji 0.2.0. Własne gesty NVDA pozostają
zachowane; mogą wymagać korekty w Ustawienia > Zdarzenia wejścia > AMC.

## Najważniejsze zmiany

Do poniższych klawiszy dodaj **Ctrl+Windows**:

- A — urządzenie audio bieżącej sesji.
- Tab / Shift+Tab — następna / poprzednia sesja.
- Przecinek / kropka — wolniej / szybciej; Shift+kropka — normalne 1,00 razy.
- Alt+1–9, 0, minus, równa się — presety 1–12 bieżącej sesji.
- Shift+P — lista playlist; Alt+P — lista presetów.

Na liście presetów jest przycisk **Utwórz nowy preset**. Przypisuje element, dla
którego otwarto listę, podany w jej opisie. Następny dialog wybiera miejsce,
potwierdza ewentualne zastąpienie, a Escape anuluje. Po zapisie/anulowaniu
wracasz do presetów. Enter lub Spacja na samej liście niczego nie zapisuje.
W WiiM przycisk **Przypisz skrót AMC do wybranego presetu** nie zmienia urządzenia.

Widok Playlisty ma **Utwórz nową playlistę**. Dotyczy również TIDAL poprzez
istniejące API tworzenia playlist; zależy od autoryzacji konta. W oknie dodawania
elementów do playlist przycisk Nowa pozostaje dostępny.

## Krótkie testy do wykonania

1. NVDA+1: sprawdź nową mapę bez wykonywania funkcji, potem wyłącz pomoc.
2. W innym programie użyj sesji Tab/Shift+Tab, regulacji prędkości i A.
   Otwieranie wyboru urządzenia ma przywołać AMC; zwykłe sterowanie nie.
3. Sprawdź presety 1, 9, 0, minus i równa się, także puste miejsce.
   Pusty preset ma być oznajmiony, nigdy automatycznie zapisany.
4. Otwórz presety. Tabem znajdź przycisk Utwórz, użyj Entera i Spacji.
   Sprawdź Save/Cancel, początkowy fokus, strzałki i ponowne otwarcie.
   Enter lub Spacja na Zamknij nie może odtwarzać presetu.
5. Powtórz dla WiiM: przypisanie zmienia tylko skrót AMC.
6. Na liście playlist utwórz nową, sprawdź anulowanie i powrót na listę.
   Osobno sprawdź TIDAL, jeżeli jesteś zalogowany.
7. Globalny preset stacji/pliku uruchamia ją w tle; preset folderu/albumu/playlisty
   jawnie otwiera zawartość w AMC. Sprawdź także zminimalizowane AMC.

Testy automatyczne nie potwierdzają odbioru wszystkich klawiszy przez rzeczywiste
NVDA, mowy/brajla ani zachowania fizycznego urządzenia. Nie zmieniano ustawień
Windows lub innych dodatków. Ctrl+Windows+Shift+cyfry pozostają Windows.
