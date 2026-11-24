# SCHED-004 — komunikat po zapisaniu harmonogramu

**Zgłoszenie Michała, 25.11.2026.** W AMC Test alpha.525 po Enter zapisującym edytowany plan Radia NVDA potrafi odczytać tytuł głównego okna, np. „THE GROUND BENEATH HER FEET/U2 — AMC Test —”, zamiast wyraźnego wyniku zapisu. Przy uruchomieniu AMC odczytuje podobnie „POWTËRKA_SUP_AGNES_3/ — AMC Test — 24.11.2026, 16:18”. Ta korekta zastępuje wcześniejsze potraktowanie obu odczytów jako jednego błędu.

## Zachowanie i przyczyna

- Główne okno działającej alpha.525 miało dokładnie tytuł „POWTËRKA_SUP_AGNES_3/ — AMC Test — 24.11.2026, 16:18”; sesja Radio odtwarzała materiał. W zapisanych ustawieniach `window_title_playback_position` było `before`. Tytuł odczytywany przy starcie jest zgodny z wybraną preferencją, a nie komunikatem o zapisie planu.
- W **Ustawienia programu → Ogólne → Tytuł odtwarzanego utworu w tytule okna** istnieją już trzy wybory: po nazwie i dacie AMC, przed nazwą AMC, bez utworu. Użytkownik poprosił o możliwość wyboru. Zachowujemy jego aktualną preferencję i nie migrujemy prywatnych ustawień.
- W oknie harmonogramów kod wysyłał zdarzenie regionu aktywnego „Zapisano plan…” przed zaplanowanym przywróceniem fokusu na listę. Następny odczyt fokusu mógł przerwać komunikat i podać tytuł okna głównego. Ten sam porządek dotyczył odmowy zapisu.

## Kontrakt dostępności po poprawce

Po zapisaniu lub odmowie zapisu planu fokus ma wrócić na listę harmonogramów, a następnie NVDA ma otrzymać pojedynczy, użytkowy komunikat wyniku: „Zapisano plan …” albo „Nie udało się zapisać harmonogramu. Niczego nie zmieniono”. Zaznaczony plan pozostaje wybrany. Enter, Spacja, Escape, menu, paleta i pomoc zachowują dotychczasowe przypisania. Tytuł głównego okna nadal stosuje wybrany wariant; przy starcie NVDA może go normalnie wypowiedzieć.

## Wdrożenie i wynik

`RadioSchedulesDialog._commit` przekazuje wynik do dwóch kolejnych zdarzeń wx: najpierw przywraca fokus listy, potem ogłasza wynik, o ile dialog nadal istnieje. Dotyczy udanego i nieudanego zapisu. Nie zmieniono danych harmonogramów ani okien czasowych nagrań.

Celowane testy harmonogramów, dostępnego regionu, ustawień, aktualizatora i tytułu okna: **142 zaliczone** przed wydaniem. Test sprawdza, że komunikat jest wysyłany dopiero po przywróceniu fokusu. Ręczny odbiór w NVDA, po rzeczywistym Enter w zainstalowanej wersji, pozostaje do wykonania; testy kodu nie dowodzą kolejności fizycznej mowy.

## Wersja i etapy

Poprawkę zawiera źródłowy commit `853504bcdcbbbbc9c31b1d93879900235f6f0263` w AMC Test `0.1.0-alpha.527`. Kompilacja GitHub, instalator, wydanie i lokalna aktualizacja zostały potwierdzone osobno w [zapisie wydania alpha.527](RELEASE-ALPHA527-20261125.md). Alpha.526 była opublikowana na GitHub, lecz nie została lokalnie zainstalowana i nie zawiera tej poprawki.
