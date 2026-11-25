# AMC Test i Public alpha.530 — wydanie GitHub

Data: 25.11.2026. Stan przed publikacją: źródła i testy gotowe, budowa GitHub w przygotowaniu.

## Zakres i decyzje

Oba profile powstają z tej samej rewizji gałęzi `codex/heos-next-test-20261123`. Public otrzymuje wspólne poprawki z 24–25 listopada znajdujące się w tej gałęzi: wybór Cast/AirPlay, widok aktywnego odtwarzania, poprawki komunikatów po zapisie planu, poprawki fokusu Biblioteki Podcastów, zachowanie pauzy po aktualizacji oraz zapowiedzi kolejnego utworu z ustawieniem osobno w sesji. Public zachowuje wyłączenie Apple Music i HEOS; Test zawiera obie sesje. Otwarty problem kursora po ukończeniu pobierania podcastu nie ma poprawki w tym wydaniu.

Korekta użytkownika z 25.11.2026 zastępuje wcześniejszy układ klawiszy: `Shift+Enter` ustawia zaznaczony materiał do odtworzenia po bieżącym; `Ctrl+Shift+Enter` jest wycofany. `Ctrl+Shift+Q` nadal przełącza zwykłą kolejkę, również w wynikach wyszukiwania. Zmiana obejmuje główną listę, odtwarzacz, Radio Archiwistę, HEOS w Test, wyniki wyszukiwania, menu, paletę i pomoc z modelu poleceń. Dodanie mówi „Odtwarzaj jako następne: [nazwa]”, usunięcie „Usunięto z następnych: [nazwa]”; dla wielu elementów komunikat podaje liczbę.

## Dowody i ograniczenia

252 testy Python: zaliczone. Sprawdzono skróty, natywny hook listy, wyszukiwanie, menu, paletę i oba profile. `git diff --check`: bez błędów. GitHub Actions ma zbudować osobne instalatory oraz pliki integralności dla profili test i public, po czym skrypt publikacyjny sprawdzi źródłowy commit, rozmiar i SHA-256. Budowa, wydanie GitHub, instalacja oraz odbiór NVDA są na tym etapie osobnymi, niepotwierdzonymi krokami. Użytkownik instaluje sam. Rzeczywiste Cast, AirPlay, HEOS i komunikaty NVDA wymagają sprawdzenia w użyciu.

Wersja źródeł: `0.1.0-alpha.530`. Commit źródeł, uruchomienia GitHub Actions, tagi, sumy SHA-256 i status publikacji należy dopisać po ich potwierdzeniu. Starsze wydania: Test `test-2026.11.25.0226`, Public `public-2026.11.24.0102`.
