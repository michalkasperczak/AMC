# AMC — dodatek NVDA 0.1.1

Paczka: `D:\Projekty Codex\Accessible Multimedia Controller\AMC-NVDA-0.1.1.nvda-addon`

Wymaga AMC alpha 339 lub nowszego i NVDA 2026.1 lub nowszego. Nie trzeba przebudowywać ani aktualizować AMC 339. Zainstaluj aktualizację dodatku, wykonując wskazówki NVDA. Jeśli Free Radio jest nadal włączone jako dodatek, wyłącz je albo zmień jego gesty — samo nieużywanie odtwarzania nie zwalnia klawiszy. Nie zmieniono prefiksu AMC ani gestów zapisanych samodzielnie przez użytkownika.

## Skróty

Przytrzymaj **Ctrl+Windows**, następnie naciśnij:

- **Góra/dół** — głośniej/ciszej o 5 procent.
- **Lewo/prawo** — poprzedni/następny element kontekstu odtwarzania.
- **P** — odtwórz lub wstrzymaj.
- **M** — wycisz lub przywróć dźwięk bieżącej sesji.
- **I** — bieżące nagranie, sesja i stan.
- **J/K** — cofnij/przewiń o 10 sekund.
- **E/R/T** — czas od początku/pozostały/całkowity.
- **Page Up/Page Down** — poprzednia/następna sesja.

Nie trzeba najpierw naciskać prefiksu. Gesty można zmieniać w **NVDA > Ustawienia > Zdarzenia wejścia > AMC**. Wcześniejsze własne przypisania mogą mieć pierwszeństwo; dodatek ich nie nadpisuje. Pomoc klawiszy NVDA (NVDA+1) powinna podawać opisy komend bez ich wykonywania.

Ctrl+Windows+lewo/prawo podczas działania dodatku służą AMC zamiast przełączaniu pulpitów Windows. Ctrl+Windows+Enter pozostaje nieprzypisane w dodatku, podobnie jak cyfry. Inne globalne programy/dodatki nadal mogą powodować konflikty.

## Próba

Otwórz nagranie w AMC. Przejdź do edytora i naciśnij Ctrl+Windows+I, następnie P, ponownie P i strzałki góra/dół, cały czas z Ctrl+Windows. Fokus powinien pozostać w edytorze. Sprawdź też zmianę sesji i powrót do niej. Pauza odsłuchu nie pauzuje nagrywania. Następny/poprzedni używa kontekstu AMC; dla WiiM to transport urządzenia, nie przeskakiwanie po presetach.

Na końcu sprawdź zachowanie przy zamkniętym AMC: ma być krótki komunikat, bez zawieszania NVDA. Niczego nie nagrywaj specjalnie do tego testu. Po braku odpowiedzi użyj informacji o stanie, zanim ponowisz przełącznik pauzy.

Nie instalowano tej paczki w czytniku użytkownika ani nie wykonano ręcznego odsłuchu. Testy automatyczne obejmują kompletność i jednoznaczność mapy gestów oraz transport 14 poleceń.
