# AMC Test i Public alpha.496 — szkic instrukcji i listy zmian

Szkic do korekty użytkownika; nie jest opublikowaną instrukcją.

## Instrukcja

AMC Test i AMC Public są zainstalowane osobno. Włącz właściwe okno, a dodatek AMC do NVDA 0.4.2 skieruje skrót do tego profilu. Po przejściu do innego programu dodatek pamięta ostatnio wybrany profil.

- `Ctrl+Windows+P`: odtwarzaj lub wstrzymaj.
- `Ctrl+Windows+Alt+P`: otwórz presety.
- `Ctrl+Windows+Shift+P`: otwórz playlisty.
- W samym AMC `Ctrl+Alt+P` otwiera presety, a `Ctrl+Alt+Shift+P` przypisuje bieżący element do presetu.

Na liście presetów NVDA powinno odczytać numer, nazwę stacji i przypisany skrót, np. „8, nazwa stacji, Control Shift 8”.

W AMC Test lista rozpoznanych utworów jest dostępna pod `Ctrl+Alt+S`. Na dokładnym wyniku `Ctrl+Shift+L` zmienia zapis albumu w Bibliotece, a `Ctrl+Shift+U` zapis utworu w Ulubionych. Program wypowiada wynik po odpowiedzi usługi. Ręczny zapis fragmentu radia wymaga odsłuchu i wybrania początku oraz końca; program nie obiecuje całej piosenki.

## Lista zmian

- Cichy instalator AMC aktualizuje starszy dodatek AMC do NVDA i uruchamia NVDA ponownie. Wersji aktualnej lub nowszej nie zastępuje.
- Naprawiono zgłoszone „Brak połączenia z AMC” wynikające ze starego dodatku 0.4.1, który nie rozpoznawał oddzielnych kanałów Test/Public.
- Instalator zwraca błąd, jeśli kontrola nowej instalacji albo aktualizacja dodatku nie powiedzie się.
- Skrócono etykiety presetów czytane przez NVDA.
- W profilu Test poprawiono historię TIDAL, działania na rozpoznanych utworach i ręczny zapis fragmentu radia.

Kontrola kodu, paczek, instalatorów i połączenia obu profili przeszła. Rzeczywista wymowa skrótów NVDA oraz operacje na koncie usługi wymagają odbioru użytkownika.
