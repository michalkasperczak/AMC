# Blindradio — pomysły dla rozpoznawania utworów w AMC Test

Status: **Zrobione**. Data przeglądu: 4.11.2026. Źródło: [Blindradio](https://github.com/serrebidev/Blindradio), główna gałąź, opis wersji 0.2.56 w [changelogu](https://github.com/serrebidev/Blindradio/blob/main/CHANGELOG.md). Licencja kodu: [MIT](https://github.com/serrebidev/Blindradio/blob/main/LICENSE). To porównanie jest obserwacją, bez zgody na wdrażanie nowej funkcji lub kopiowanie kodu.

## Ustalenia

- Blindradio rozpoznaje utwory przez AudD z 12 sekund dźwięku, co najmniej 45 sekund między próbami, opcjonalnie tylko przy braku metadanych strumienia ([źródło](https://github.com/serrebidev/Blindradio/blob/main/crates/blindradio-core/src/tuner.rs), [opis](https://github.com/serrebidev/Blindradio/blob/main/doc/readme.md)). AMC Test już pobiera 12 sekund dźwięku, rozpoznaje przez Shazam na podstawie sygnatury i ma osobny cykl prób. AudD wymaga tokenu użytkownika i wysyła próbkę dźwięku. Nie widać podstaw, by teraz zmieniać dostawcę.
- Blindradio oddziela identyfikację tytułu od jego oznajmienia, a historię stacji nadającej własną listę od historii z bieżącej sesji ([historia](https://github.com/serrebidev/Blindradio/blob/main/crates/blindradio-core/src/history.rs), [mowa](https://github.com/serrebidev/Blindradio/blob/main/crates/blindradio/src/speech.rs)). W AMC Test zapis rozpoznania jest już niezależny od włączonego oznajmiania, a `Ctrl+Alt+S` pokazuje trwałą historię. Nie należy zastępować jej chwilową historią sesji.
- Przy powrocie fokusu Blindradio odkłada komunikaty mówione, aby NVDA najpierw odczytał wskazany wiersz ([źródło](https://github.com/serrebidev/Blindradio/blob/main/crates/blindradio/src/speech.rs)). AMC Test ma podobne opóźnienie dla części aktualizacji list. Własny problem AMC z gubionym komunikatem po zamknięciu okna warto badać na jego konkretnej ścieżce; nie jest to dowód, że rozwiązanie Blindradio trzeba przenieść globalnie.
- Blindradio oznacza w buforze przesunięcia czasu granice utworów na podstawie metadanych lub późniejszego rozpoznania i pozwala zapisać segment ([changelog](https://github.com/serrebidev/Blindradio/blob/main/CHANGELOG.md)). To może być osobny pomysł dla nagrań AMC, lecz rozpoznanie po rozpoczęciu utworu nie ustala dokładnej granicy. Wymagałoby własnego projektu i pomiarów.

## Jedyna rekomendacja na teraz

W przyszłym zadaniu rozważyć niewymuszający mowy stan automatycznego rozpoznawania w historii AMC: czas ostatniej próby oraz rozróżnienie „za mało dźwięku”, „brak dopasowania” i „błąd usługi”. Nie dopisywać takich stanów jako utworów. Pomysł odpowiada wcześniejszej trudności użytkownika z odróżnieniem pustej listy od braku prób i nie zależy od dostawcy AudD. Najpierw sprawdzić istniejące komunikaty AMC i wynik próby w hostcie; Blindradio nie jest tu źródłem gotowej implementacji.

## Materiał do dokumentacji użytkownika

Brak zmiany funkcji AMC. Tę notatkę należy zachować jako materiał projektowy, bez dopisywania nowej funkcji do instrukcji ani listy zmian.

## Testy i wynik

Porównano README, changelog, instrukcję, licencję oraz wskazane fragmenty kodu Blindradio z aktualnym torem rozpoznawania AMC Test. Nie uruchamiano testów Blindradio ani AMC; nie budowano i nie instalowano aplikacji.

## Znane ograniczenia

Ocena opiera się na publicznym kodzie głównej gałęzi z dnia przeglądu, bez pomiaru zachowania programu z NVDA. Szczegóły mowy i granic segmentów wymagają własnych prób w AMC, jeśli użytkownik zleci późniejszą funkcję.

Zakres poza tym przeglądem: instalowanie, uruchamianie i testowanie Blindradio; jego pozostałe katalogi, nagrywanie, scrobbling, aktualizator i naprawianie jego błędów. Nie zmieniono kodu AMC ani działającej instalacji.

## Wersja i commit

Porównanie wykonano na lokalnej gałęzi AMC Test przy commicie `6db208e4`; nie zmieniono kodu programu. Commit tej notatki jest osobnym zapisem dokumentacji.
