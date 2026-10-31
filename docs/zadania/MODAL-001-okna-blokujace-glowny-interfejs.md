# MODAL-001 — okna blokujące główny interfejs AMC

Status: **W trakcie**. Zgłoszenie użytkownika: 31.10.2026.

## Zachowanie widoczne dla użytkownika

- `Ctrl+Alt+S` ma otwierać historię rozpoznanych utworów z fokusem na liście. Pozostawienie jej otwartej nie powinno wyłączać głównego okna AMC. Ponowny skrót przywołuje istniejące okno zamiast tworzyć następne.
- Zamknięcie historii przyciskiem „Zamknij”, `Escape` lub krzyżykiem wraca do miejsca wywołania. Działanie wybranego utworu nadal zamyka historię przed przejściem do Spotify, TIDAL albo strony usługi.
- Pozostałe modalne okna wymagające decyzji mają wracać na wierzch po ponownej aktywacji AMC; automatyczny komunikat nie może otworzyć kolejnego modala nad już otwartym.

## Uzasadnienie i decyzje

- 31.10.2026: podczas testu otwarto historię i nie zamknięto jej po przerwaniu sterowania. Odczyt UI Automation wykazał wyłączone główne okno i jedyny widoczny dialog „Rozpoznane utwory” z filtrem `GDA`. Kliknięcie jego przycisku „Zamknij” odblokowało główne okno bez zatrzymania AMC. To potwierdza przyczynę bieżącego incydentu; nie dowodzi, że aplikacja sama otworzyła listę.
- Wcześniejszy incydent Sonos z 25.10.2026 opisano w `AMC10-logowanie-sonos-i-publiczny-broker.md`: główne okno było wyłączone przez modal konta. Konto Sonos zostało potem zamienione na okno niemodalne. Historia jest podobnie długotrwałym oknem przeglądania, więc także powinna być niemodalna.
- Krótkie dialogi potwierdzeń i ustawień mogą pozostać modalne. Wspólna obsługa aktywacji aplikacji ma przywołać ich bieżący, widoczny dialog, jeżeli wraca się do AMC z innego programu.
- Przegląd statyczny bieżącego `gui.py`: 78 wywołań `ShowModal`, w tym 15 w odpowiedziach asynchronicznych (`done`/`ready`). Nie wszystkie są błędem: wybór lub potwierdzenie wymaga czasem modala. Ryzyko ukrycia dotyczy szczególnie odpowiedzi pojawiających się po zakończeniu operacji, gdy użytkownik zdążył otworzyć inne okno. Dlatego wspólny mechanizm przywołuje najgłębszy widoczny modal po jego inicjalizacji i po aktywacji AMC. Automatyczna kontrola aktualizacji dodatkowo czeka na zamknięcie innych okien, także gdy odpowiedź przyjdzie już po ich otwarciu.
- Zamknięcie głównej ramki sprząta otwartą historię bez przywracania fokusu do niszczonego okna. Odpowiedzi asynchroniczne historii ignorują wynik po jej zamknięciu.

## Materiał do dokumentacji użytkownika

> `Ctrl+Alt+S` pokazuje historię rozpoznanych utworów. Możesz wrócić do głównego AMC, pozostawiając historię otwartą, i przywołać ją ponownie tym samym skrótem. `Escape` lub „Zamknij” zamyka historię. Jeśli otwarte jest okno wymagające decyzji, po powrocie do AMC powinno ono pojawić się na wierzchu.

## Testy i wynik

- Przed zmianą: odczyt okna na żywo potwierdził modalną blokadę i skuteczne odblokowanie przyciskiem „Zamknij”.
- Po zmianie źródeł: 42 testy celowane przeszły w prywatnym środowisku wxPython 4.3.1, Python 3.14, bez uruchamiania widocznej aplikacji. Obejmują ponowne wywołanie istniejącej historii, powrót fokusu do sesji, przywołanie najgłębszego modala oraz opóźnienie okna aktualizacji do zamknięcia historii.
- Build, instalacja i odbiór NVDA po poprawce: do zapisania osobno.

## Znane ograniczenia

- Nie należy kończyć działającego AMC w trakcie nagrania. Zmiana źródłowa może powstać i przejść testy bez zamykania programu; instalacja wymaga świeżej kontroli nagrań.
- Testy bez widocznego interfejsu nie dowodzą, że każda kombinacja natywnych okien Windows i NVDA ustawi fokus prawidłowo. Po bezpiecznej instalacji potrzebny jest odbiór klawiaturą i NVDA: `Ctrl+Alt+S`, `Tab`, `Shift+Tab`, ponowny skrót, `Escape`, przełączenie do głównego okna oraz kontrola dialogu aktualizacji.

## Wersja i commit

- Baza zainstalowanej aplikacji: `d6d661cf380a773cef662ef39a45ba8b9bc81143`, pakiet `a48651b1`. Commit poprawki i identyfikator jej paczki do wpisania po wykonaniu.
