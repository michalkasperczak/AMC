# AMC Test i Public alpha.530 — wydanie GitHub

Data: 25.11.2026. Stan: oba wydania opublikowane i zdalnie zweryfikowane; instalacja i odbiór NVDA otwarte.

## Zakres i decyzje

Oba profile powstają z tej samej rewizji gałęzi `codex/heos-next-test-20261123`. Public otrzymuje wspólne poprawki z 24–25 listopada znajdujące się w tej gałęzi: wybór Cast/AirPlay, widok aktywnego odtwarzania, poprawki komunikatów po zapisie planu, poprawki fokusu Biblioteki Podcastów, zachowanie pauzy po aktualizacji oraz zapowiedzi kolejnego utworu z ustawieniem osobno w sesji. Public zachowuje wyłączenie Apple Music i HEOS; Test zawiera obie sesje. Otwarty problem kursora po ukończeniu pobierania podcastu nie ma poprawki w tym wydaniu.

Korekta użytkownika z 25.11.2026 zastępuje wcześniejszy układ klawiszy: `Shift+Enter` ustawia zaznaczony materiał do odtworzenia po bieżącym; `Ctrl+Shift+Enter` jest wycofany. `Ctrl+Shift+Q` nadal przełącza zwykłą kolejkę, również w wynikach wyszukiwania. Zmiana obejmuje główną listę, odtwarzacz, Radio Archiwistę, HEOS w Test, wyniki wyszukiwania, menu, paletę i pomoc z modelu poleceń. Dodanie mówi „Odtwarzaj jako następne: [nazwa]”, usunięcie „Usunięto z następnych: [nazwa]”; dla wielu elementów komunikat podaje liczbę.

## Dowody i ograniczenia

252 testy Python: zaliczone. Sprawdzono skróty, natywny hook listy, wyszukiwanie, menu, paletę i oba profile. `git diff --check`: bez błędów. GitHub Actions zbudował osobne instalatory i pliki integralności dla profili test i public; skrypt publikacyjny sprawdził źródłowy commit, rozmiar i SHA-256. GitHub Actions run `45650365024`: oba zadania zakończone sukcesem. Oba wydania opublikowano etapowo po przejściowych błędach API GitHub podczas częściowej awarii serwisu. Instalacja i odbiór NVDA pozostają niepotwierdzone. Użytkownik instaluje sam. Rzeczywiste Cast, AirPlay, HEOS i komunikaty NVDA wymagają sprawdzenia w użyciu.

Wersja źródeł: `0.1.0-alpha.530`, commit `965bd33846db682dea3b1267e11cdbeb97cd9a22`. Starsze wydania: Test `test-2026.11.25.0226`, Public `public-2026.11.24.0102`.

AMC Test: `test-2026.11.25.1702` (prerelease), instalator `AMC-Test-2026-11-25-1702.exe`, 214674456 bajtów, SHA-256 `0c88449b1aa81f57a556183ee091e869a7fa3e0aa3c8a2a0c4579d838897ef54`.

AMC Public: `public-2026.11.25.1702` (najnowsze stabilne), instalator `AMC-Public-2026-11-25-1702.exe`, 134052169 bajtów, SHA-256 `cfe3df855163d335ba6a675402b09b19548be7d8430dd2263723d1d5e6031c6f`.

Zdalny odczyt GitHub potwierdził oba tagi na tym samym commicie, wszystkie trzy zasoby każdego wydania w stanie uploaded, rozmiary i sumy SHA-256 każdego zasobu zgodne z lokalnymi oraz sumy instalatorów w opisach. GitHub API `releases/latest` wskazuje Public alpha.530. Kopie instalatorów i manifestów zachowano w `D:\Projekty Codex\AMC - integracja wxPython\work\release-alpha530-20261125`; bez instalacji lokalnej.
