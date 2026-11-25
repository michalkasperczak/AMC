# ACCESS-SESSION — kontrakt dostępności każdej sesji

Stan 25.11.2026: wymaganie i scenariusz kontroli zapisane; pełny audyt interfejsu oraz próby NVDA jeszcze otwarte. To zadanie jest odrębne od ustawień automatycznych zapowiedzi utworów.

## Decyzja użytkownika z 25.11.2026

Każdą sesję sprawdzać osobno. Przy tworzeniu nowej sesji jej ustawienia zapowiedzi, odczytu listy i zachowania odtwarzacza mają dziedziczyć ustawienia globalne, dopóki użytkownik nie wybierze inaczej. Nie wolno zakładać, że wspólny kod listy automatycznie zapewnia wszystkie gesty i komunikaty w nowym źródle.

## Scenariusz dla nowej lub zmienianej sesji

1. Zapisać możliwości źródła i brakujące możliwości. Pokazać w dialogu wyłącznie pola, które mają działającego wykonawcę. Zweryfikować domyślny wariant „Jak ustawienie ogólne”, zapis wariantu własnego, powrót do dziedziczenia i ponowne uruchomienie AMC.
2. Osobno sprawdzić: Enter, Spacja, F6, Escape, Ctrl+U, presety, kolejkę, playlisty, menu kontekstowe, pomoc Ctrl+F1, pełną pomoc, paletę Ctrl+Shift+K i odpowiednią pozycję menu. Każdy gest musi mieć tę samą nazwę, skrót, dostępność i skutek na wszystkich powierzchniach poleceń.
3. Na każdej liście sprawdzić odczyt góra/dół, informację pod lewą strzałką, przenoszenie Alt+góra/dół, wycinanie Ctrl+X, filtrowanie i sortowanie **tam, gdzie źródło pozwala zmieniać listę**. Jeśli źródło nie daje tej możliwości, polecenie ma być wyłączone albo podać konkretny powód; nie może wyglądać na czynne bez skutku. Szczególnie sprawdzić Ulubione HEOS, Ulubione usług, playlisty i kolejki.
4. Na żywym NVDA sprawdzić pierwszy zaznaczony element, każde rozwinięte pole wyboru, strzałki, fokus po Zapisz/Anuluj i ponowne otwarcie. Żadna wartość modelu, identyfikator polecenia ani domyślny opis obiektu nie może trafić do mowy. Zmiana materiału w tle nie może przenosić fokusu lub zaznaczenia do innej sesji.
5. Zapisać w rekordzie zadania dokładne komunikaty, różnice między sesjami, wynik testów kodu, kompilację GitHub, opublikowany release, instalację i odbiór NVDA jako osobne stany.

## Otwarte różnice

- Ulubione HEOS: odczyt lewą strzałką, przenoszenie, filtr, sortowanie, Ctrl+X i presety wymagają pełnego odbioru; działanie źródła na głośniku do sprawdzenia jutro.
- Wspólna kolejka i „Odtwarzaj jako następne” pozostają oddzielnymi zadaniami QUEUE-003/004; radio nie powinno obiecywać kolejki lokalnej.
- Menu kontekstowe, kolejność poleceń oraz cyfry słyszane przez NVDA mają osobny zapis MENU-001.
- Apple Music odłożone na prośbę użytkownika 25.11.2026.

Wersja i commit: rekord kontraktu bez nowego wydania.
