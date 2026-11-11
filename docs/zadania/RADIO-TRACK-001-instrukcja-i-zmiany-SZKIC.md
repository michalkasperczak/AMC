# Szkic instrukcji i listy zmian: fragment rozpoznanego utworu

Data: 11.11.2026. Materiał do korekty użytkownika, bez publikacji.

## Instrukcja użytkownika

1. Odtwórz stację i rozpoznaj utwór klawiszem `S` w otwartym odtwarzaczu. Możesz też włączyć automatyczne rozpoznawanie klawiszem `Shift+S`.
2. Otwórz historię rozpoznań klawiszem `Ctrl+Alt+S` i wybierz utwór. Wybierz przycisk **Sprawdź fragment…** albo tę samą funkcję w menu kontekstowym.
3. AMC poda zakres dźwięku, który wciąż jest w buforze. Początek i koniec ustaw w sekundach względem początku próbki użytej do rozpoznania. Liczba ujemna oznacza wcześniejszy dźwięk. Strzałki zmieniają wartość o sekundę.
4. Przyciskami **Odtwórz początek** i **Odtwórz koniec** sprawdź około pięciu sekund przy wybranych granicach. Podgląd korzysta z domyślnego urządzenia Windows; podczas odsłuchu może być słychać również grającą stację.
5. Wybierz **Zapisz fragment…**, wskaż folder i nazwę pliku WAV. AMC nie zastąpi istniejącego pliku. Zapis nie przełącza stacji, nie zmienia pozycji odsłuchu i nie kończy nagrania.

AMC rozpoznaje tytuł i wykonawcę, lecz usługa rozpoznawania nie wskazuje dokładnego początku ani końca piosenki. Dlatego funkcja zapisuje wybrany fragment dźwięku. Gdy próbka zostanie nadpisana, tor zmieni się albo aplikacja zostanie ponownie uruchomiona, rozpoznaj utwór ponownie, jeśli wciąż jest odtwarzany. Bufor nie odzyskuje dźwięku, którego nie zawiera.

## Szkic listy zmian

- Dodano wybór, odsłuch i bezstratny zapis dostępnego fragmentu radia po rozpoznaniu utworu.
- Rozpoznanie jest powiązane z konkretnym buforem i segmentem odbioru. Eksport odrzuca stary, nadpisany albo przecinający przerwę zakres.
- Podgląd i zapis działają również dla próbki z prywatnego toru aktywnego nagrania, bez zatrzymywania nagrania.
- Historia nie utrwala technicznych żetonów dźwięku. Po ponownym uruchomieniu dostępne są dane rozpoznania, ale eksport wymaga nowej próbki.

Stan: źródła i testy automatyczne gotowe. Test NVDA, instalacja i wydanie jeszcze nie wykonane. Nie obiecywać automatycznego zapisu całego utworu.
