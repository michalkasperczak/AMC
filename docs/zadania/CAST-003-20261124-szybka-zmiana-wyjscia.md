# CAST-003 — szybka zmiana głównego wyjścia Google Cast

**Status 24.11.2026:** poprawka źródłowa na gałęzi następnego AMC Test; bez nowego buildu, instalacji i wydania GitHub. Odbiór fizycznej klawiatury i NVDA pozostaje otwarty.

## Zlecenie i korekta użytkownika

Michał zlecił dalsze poprawki `Shift+C` i proponowanych skrótów nawiasów. Doprecyzował, że Mirror oznacza jednoczesny odsłuch na dwóch wyjściach, podczas gdy podstawowy wybór AirPlay mógłby przełączać dźwięk na jedno inne urządzenie. **Ta korekta z 24.11.2026 zastępuje przejściowy wniosek wykonawcy, że AirPlay powinien docelowo pozostać w Mirror.** Obecny kod AirPlay w AMC jest wyłącznie dodatkowym wyjściem; decyzja o osobnym głównym wyjściu AirPlay i jego miejscu w interfejsie nie zapadła. `Ctrl+Alt+Shift+[` i `Ctrl+Alt+Shift+]` pozostają propozycją bez przypisania.

Michał potwierdził zakres pierwszego skrótu: `Ctrl+Shift+[` ma przełączać główne wyjście i wyraźnie mówić, na co przełączył. Para `Ctrl+Shift+[` / `Ctrl+Shift+]` została użyta do poprzedniego/następnego wyjścia Google Cast zgodnie z listą `Shift+C`.

## Zachowanie i dokładne miejsca

- `Shift+C` otwiera listę: „Ten komputer” oraz odbiorniki Google Cast. Aktualnie wybrane wyjście ma etykietę „{nazwa}, wybrane”. Ten fragment pochodzi z poprzedniego etapu CAST-002.
- `Ctrl+Shift+[` wybiera poprzednie dostępne wyjście z tej samej listy, `Ctrl+Shift+]` następne. Przechodzenie zawija się między komputerem i odbiornikami; niedostępne urządzenia są pomijane. Gdy nie ma innego celu, AMC mówi „Brak innego dostępnego wyjścia Google Cast” i nic nie przełącza.
- Po potwierdzonym przełączeniu komunikat brzmi „Wyjście bieżącej sesji: {nazwa}”, np. „Wyjście bieżącej sesji: WiiM Sound” albo „Wyjście bieżącej sesji: Ten komputer”. Po odmowie źródła lub odbiornika AMC podaje błąd i nie ogłasza nowego wyjścia. Ponowne naciśnięcie podczas trwającej zmiany mówi „Trwa przełączanie wyjścia”.
- Polecenia są dostępne z listy i odtwarzacza w Plikach, Radiu, Podcastach, Radiu Archiwiście i przy obsługiwanym natywnym strumieniu TIDAL. Dla zewnętrznego TIDAL i formatu DASH Cast jest niedostępny. Sesje głośników WiiM, Sonos, HEOS i Frontier Smart oraz Spotify i Apple Music nie reklamują tych poleceń.
- Menu Dźwięk, paleta `Ctrl+Shift+K`, pełna pomoc i pomoc `Ctrl+F1` korzystają ze wspólnych nazw, opisów, skrótów i warunków dostępności. Litery dostępu nowych pozycji nie kolidują z pozostałymi pozycjami w sesjach.
- Lista `Shift+M` oznacza wybrany dodatkowy cel słowem „wybrane”. Obecne AirPlay w tej liście nadal działa jako dodatkowy odsłuch, a nie jako główne wyjście AirPlay. Nazwa polecenia pokazuje „Dublowanie dźwięku (Mirror)”. Nie jest to decyzja o przyszłym układzie AirPlay.

## Uzasadnienie i granice

Szybkie skróty korzystają z tego samego stanu sesji i tej samej operacji wyboru co `Shift+C`; nie utrzymują drugiej listy wyboru. Odczyt urządzeń biegnie poza wątkiem wx. Spóźniona odpowiedź po zmianie sesji nie wysyła polecenia przełączenia. Kody OEM nawiasów Windows są odtwarzane z fizycznego klawisza, gdy `Shift` zwróci nawias klamrowy. Nie przechwytujemy skrótu z Alt, dopóki znaczenie głównego AirPlay i Mirror nie zostanie ustalone. Układ klawiatury użytkownika i rzeczywista mowa NVDA wymagają odbioru.

Wstępna rekomendacja do przyszłego projektu: jeśli AirPlay stanie się pojedynczym **głównym** wyjściem, umieścić go wraz z Cast na uogólnionej liście wyjść sieciowych i zmienić nazwę `Shift+C` zgodnie z zakresem. `Shift+A` dotyczy dziś urządzeń audio Windows; samo przeniesienie odkrytych przez sieć odbiorników AirPlay do tej listy byłoby mylące. `Shift+M` powinno zachować znaczenie równoczesnego odsłuchu. To propozycja, nie zatwierdzony ani wdrożony kontrakt.

## Testy i rzeczywiste wyniki

- `test_cast_output_cycle.py` i celowane testy skrótów, menu, Mirror oraz palety: **116 passed**. Obejmują cykl i zawijanie, pominięcie urządzenia niedostępnego, komunikaty, błąd wyboru, spóźnioną listę, rozstrzygnięcie skrótów w obu widokach i zgodność powierzchni poleceń.
- `test_network_audio_sessions.py` w osobnym procesie z rzeczywistym ukrytym wx: **39 passed**. Wybrany cel Cast i Mirror jest czytelny po ponownym otwarciu; to nie jest odbiór NVDA.
- Cztery celowane testy `test_i18n.py` polskiego/angielskiego katalogu, menu/palety/pomocy i szablonu: **4 passed**. `py_compile` zmienionych modułów: zaliczone.
- Pierwszy przebieg poleceń wykrył kolizję liter dostępu menu, a drugi kolejną kolizję w TIDAL/Podcastach. Litery poprawiono i test kolizji przeszedł we wszystkich sesjach. Nie ukrywamy tych etapów; wynikiem końcowym jest 116 zaliczonych testów.
- Zestawy częściowo się nakładają. Nie wykonywano fizycznego przełączenia na odbiornik Cast ani odsłuchu z NVDA; nie zbudowano ani nie wydano instalatora. Wersja użytkowa pozostaje alpha.525. Commit źródeł: `cba4802893038a2e1265ef4d00028a1f9cf5ceae` na gałęzi `codex/heos-next-test-20261123`.

## Materiał do instrukcji i listy zmian — szkic

W sesji z obsługą Google Cast naciśnij `Shift+C`, aby zobaczyć wyjście oznaczone jako „wybrane”. `Ctrl+Shift+[` przechodzi do poprzedniego wyjścia, a `Ctrl+Shift+]` do następnego; po zatwierdzeniu AMC mówi nazwę. „Ten komputer” przywraca odtwarzanie lokalne. `Shift+M` wybiera dodatkowy odsłuch i pokazuje wybrany cel; miejsce głównego AirPlay oraz skróty Mirror są nadal przedmiotem projektu. Ten opis jest szkicem następnego wydania Test, a nie instrukcją zainstalowanej alpha.525.
