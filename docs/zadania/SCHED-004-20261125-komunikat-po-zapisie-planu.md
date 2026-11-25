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

## Kontrola uzupełniająca, 25.11.2026

Po wydaniu alpha.527 użytkownik poprosił o sprawdzenie innych komunikatów, które może zagłuszać powrót fokusu lub odczyt tytułu okna. W bezpośredniej drodze `Shift+R` (nowy plan dla wskazanej stacji) wynik zapisu, również odmowny, był wypowiadany przed powrotem fokusu do głównego okna. Ta droga nie korzystała z naprawionej wcześniej metody `RadioSchedulesDialog._commit`. Zmieniono ją: wynik czeka na przywrócenie fokusu w kolejce zdarzeń wx, a potem na osobne zdarzenie wypowiedzi. Teksty komunikatów i działanie zapisu pozostały takie same. Test drogi bezpośredniej sprawdza teraz kolejność fokus → wypowiedź dla sukcesu i odmowy; wszystkie 18 testów w `test_radio_gui_wiring.py` przeszło. Zmiana źródłowa ma commit `b69f1d802b9d17a1aa4210c41fd587dd613e0f8a` **po alpha.527**; nie ma jej jeszcze w opublikowanym instalatorze. Rzeczywistej kolejności mowy NVDA na Windows nie sprawdzono.

Przegląd statyczny pokazał też inne możliwe kolizje wypowiedzi z natychmiastowym powrotem fokusu, m.in. potwierdzenia eksportu stacji WiiM i Sonos, zapis wyboru kanałów YouTube na żywo oraz zapis opcji odtwarzania podcastów w `gui.py`. Są to kandydaci do osobnej weryfikacji NVDA; sam porządek wywołań nie dowodzi, że każda z tych wypowiedzi jest przerywana. Nie zmieniono ich w tej poprawce.

Potwierdzono w modelu poleceń i menu: `Ctrl+Shift+I` otwiera transmisje wybranych kanałów YouTube, a `Shift+F6` otwiera listę aktywnego odtwarzania. Skróty nie kolidują. F6 w odtwarzaczu pozostaje przejściem zgodnym z jego dotychczasowym kontekstem.

Testy parytetu polecenia `Shift+F6`: 23 zaliczone. Pełny test okna aktywnego odtwarzania w tym środowisku nie jest rozstrzygający, ponieważ Python użyty do testów nie ma zainstalowanego prawdziwego `wx`; osobne uruchomienie 6 testów GUI zakończyło się błędem importu `wx`, a 1 test pominięto. Nie jest to wynik testu NVDA ani regresja potwierdzona w instalatorze.
