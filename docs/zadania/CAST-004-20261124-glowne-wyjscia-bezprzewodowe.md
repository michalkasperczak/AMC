# CAST-004 — główne wyjścia bezprzewodowe Cast i AirPlay

**Decyzja Michała, 24.11.2026. Status:** kontrakt zatwierdzony; implementacja głównej trasy AirPlay, testy i wydanie otwarte. Ten zapis nie opisuje działania obecnej alpha.525 ani nie twierdzi, że AirPlay już przeniesiono.

## Decyzja zastępująca wcześniejsze warianty

- `Shift+A`: wybór wyjścia audio Windows bieżącej sesji.
- `Shift+C`: wybór jednego **głównego wyjścia bezprzewodowego** bieżącej sesji: „Ten komputer”, Google Cast albo AirPlay. Wybrany cel ma być oznaczony słowem „wybrane”. AirPlay na tej liście ma przenieść słyszalne odtwarzanie na jeden odbiornik; nie może być tylko drugim, równoległym strumieniem.
- `Shift+M`: Mirror, czyli jawny dodatkowy odsłuch na drugim wyjściu. Wybór AirPlay jako drugiego celu może pozostać tutaj, ale jego stan musi być oddzielony od głównej trasy AirPlay.
- `Ctrl+Shift+[` i `Ctrl+Shift+]`: poprzednie i następne dostępne **główne wyjście** z tej samej listy co `Shift+C`, z zawinięciem i potwierdzonym komunikatem „Wyjście bieżącej sesji: {nazwa}”. Nazwa obejmuje także typ celu, jeśli dwa protokoły zgłaszają tę samą nazwę urządzenia. Po nieudanym przełączeniu AMC ma powiedzieć o błędzie i pozostawić poprzedni cel.
- `Ctrl+Alt+Shift+[` i `Ctrl+Alt+Shift+]`: brak decyzji i przypisania. Nie zmieniać nimi Mirror bez osobnego ustalenia.

To rozstrzygnięcie zastępuje sugestię `Shift+P`, rozważanie umieszczenia AirPlay pod `Shift+A` oraz wcześniejszy zapis CAST-003, że miejsce głównego AirPlay pozostaje otwarte. Nie zastępuje znaczenia `Shift+M`: Mirror nadal oznacza dwa jednoczesne wyjścia.

## Stan kodu i warunek wdrożenia

W gałęzi `codex/heos-next-test-20261123` `Shift+C` i skróty nawiasów obsługują obecnie „Ten komputer” oraz Google Cast. Istniejący `MirrorAirPlayRouter` pod `Shift+M` uruchamia RAOP z FFmpeg jako **dodatkowy** strumień, pozostawiając lokalny odtwarzacz włączony. Przeniesienie go bez zmiany sterowania spowodowałoby dublowanie, sprzeczne z powyższą decyzją.

Główna trasa AirPlay musi posiadać osobny stan sesji i obsłużyć co najmniej: bezpieczne przejęcie odtwarzania lokalnego, zachowanie lub odtworzenie stanu po błędzie, odtwarzanie/pauzę/stop, następny materiał, seek, status i pozycję, zmianę głośności i wyciszenie odbiornika, powrót na komputer, przejście między AirPlay i Cast, utratę odbiornika oraz kolizje z Mirror. Odbiornik RAOP może wymagać sparowania; aktualny kod nie zapewnia dla głównego AirPlay tych gwarancji. Nie wolno oznaczyć celu jako „wybrane” przed potwierdzeniem przełączenia.

Wspólny model poleceń musi jednocześnie zmienić nazwę/opis `Shift+C` i pary nawiasów w menu Dźwięk, palecie `Ctrl+Shift+K`, pomocy `Ctrl+F1`, pełnej pomocy i dokumentacji. Lista musi używać etykiet zamierzonych dla NVDA, a skróty tej samej migawki urządzeń i tego samego stanu sesji. `Shift+M` musi pokazywać tylko stan dodatkowego wyjścia. Wskazanie aktualnie odtwarzanego tytułu ma podawać rzeczywistą trasę AirPlay tak samo jak Cast.

## Weryfikacja i ograniczenia

Na etapie tego zapisu sprawdzono kod `host_client.py`, `cast_session.py`, `mirror_airplay.py`, `gui.py` i kontrakty CAST-002/003. Nie zmieniono kodu, nie uruchomiono testów dla nowej trasy, nie wykonano odsłuchu AirPlay ani testu NVDA. Nie było kompilacji, instalacji ani wydania GitHub. Wydana alpha.525 i nieopublikowana poprawka Cast z CAST-003 pozostają odrębnymi stanami. Późniejsza implementacja musi otrzymać własne wyniki testów oraz odbiór na sprzęcie przed opisaniem AirPlay jako działającego wyjścia głównego.

## Szkic do instrukcji po wdrożeniu

„`Shift+C` wybiera główne wyjście bezprzewodowe bieżącej sesji: komputer, Google Cast lub AirPlay. `Ctrl+Shift+[` i `Ctrl+Shift+]` przechodzą do poprzedniego lub następnego dostępnego celu i wypowiadają jego nazwę po potwierdzeniu. `Shift+A` wybiera urządzenie Windows. `Shift+M` włącza dodatkowy odsłuch na drugim wyjściu.” To jest tekst przyszłej instrukcji do korekty Michała, nie opis bieżącej wersji.
