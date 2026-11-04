# TSH-001 — folder bufora Time Shift

Stan: kod, testy celowane i kompilacja hosta gotowe; wspólna aktualizacja z TIDAL oraz odbiór NVDA w przygotowaniu.

## Materiał do dokumentacji użytkownika

W AMC Test otwórz **Ustawienia programu** (`Ctrl+,`) i przejdź na kartę **Radio i nagrywanie**. Obok długości bufora znajduje się **Folder bufora Time Shift**. Przycisk **Wybierz folder…** pozwala wskazać istniejący folder, np. `D:\AMC Time Shift`. Wskazana ścieżka jest odczytywana z pola; **Użyj folderu tymczasowego Windows** przywraca dotychczasowe działanie.

Po zapisaniu zmiany NVDA otrzymuje komunikat, że nowy folder zadziała po ponownym uruchomieniu AMC. Bieżący odsłuch i nagrania pozostają w starym miejscu do bezpiecznego restartu programu. Długość bufora oraz wskazany folder są ustawieniami Radia, niezależnymi od iCloud Drive i innych sesji. AMC sam tworzy w wybranym folderze plik bufora i usuwa go po zwolnieniu bufora. Gdy folder zniknie lub nie da się w nim zapisać, radio nadal działa z krótszym buforem w pamięci; program nie przenosi bufora po cichu na C:.

## Uzasadnienie i decyzje

- 2026-11-04: użytkownik wskazał, że sześciogodzinny Time Shift AMC zajmuje około 7,1 GiB na C: i poprosił o osobne ustawienie folderu oraz wspólną aktualizację z zadaniem TIDAL. To zastępuje wcześniejszą sugestię zmiany długości bufora jako jedynego sposobu odzyskania miejsca.
- Domyślne zachowanie pozostaje folderem tymczasowym Windows dla wszystkich istniejących instalacji. Użytkownik wybiera folder, a nie konkretny plik. AMC tworzy plik o unikatowej nazwie i używa mechanizmu `DeleteOnClose` oraz dodatkowego usunięcia przy zwolnieniu bufora.
- Wybrany folder jest sprawdzany przy zapisie ustawień przez próbę utworzenia i zamknięcia pliku tymczasowego. Host nie wraca automatycznie na C:, jeśli wybrany folder przestanie być dostępny.
- Zmiana lokalizacji wymaga ponownego uruchomienia hosta, więc nie restartuje działającego radia ani nagrywania przy zwykłym zapisie ustawień.

## Testy i wynik

- Nowe testy zapisu ustawienia i argumentu startowego: **3/3 zaliczone**.
- Istniejące testy klienta hosta, magazynu ustawień i struktury dialogu: **114/114 zaliczone** przy ustawieniu izolowanego katalogu `TMPDIR`. Pierwsza próba bez niego zatrzymała się na niedostępnym katalogu tymczasowym piaskownicy i została przerwana; nie była wynikiem regresji AMC.
- `dotnet build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj -c Release --no-restore -v minimal`: **sukces, zero ostrzeżeń i błędów**.
- Testy ustawienia, opisu komendy, palety, pomocy klawiatury, skrótów i struktury dialogu po zmianie pomocy: **80/80 zaliczone**. Paleta udostępnia nazwę i skrót; opis należy do wspólnej pomocy komendy.
- Próba funkcjonalna skompilowanego magazynu: utworzył plik 65 MiB w wskazanym folderze testowym, a `Dispose` usunęło go. Żadna stacja, konto ani aktywna instalacja nie były użyte.
- `git diff --check`: bez błędów formatowania także po dokumentacji.
- Test nawigacji NVDA po nowym polu i przyciskach, rzeczywista instalacja, ustawienie folderu na D:, publikacja źródeł i wydania: **jeszcze nie wykonane**.
- Utworzono pusty folder docelowy `D:\AMC Time Shift` (D: miało 865,04 GiB wolnego); zostanie wybrany w AMC dopiero po bezpiecznej aktualizacji programu.

## Znane ograniczenia

- Jeśli wybrany folder stanie się niedostępny po zapisaniu ustawień, host zapisze ostrzeżenie diagnostyczne i użyje około 64 MiB bufora w pamięci. Brakuje jeszcze bezpośredniego komunikatu NVDA o tym zdarzeniu.
- Aktywny, poprzedni bufor na C: nie jest przenoszony w trakcie odtwarzania. Zostanie zwolniony przy bezpiecznym zamknięciu starego hosta; nowy plik na D: powstanie po restarcie i rozpoczęciu nowej transmisji.

## Wersja i commit

- Gałąź: `codex/konfigurowalne-skroty-20261026`.
- Planowana wspólna wersja AMC Test z TIDAL: `alpha.469`; finalny numer i commit do zapisania po scaleniu.
- Zmienione źródła: `state_store.py`, `host_client.py`, `gui.py`, `shortcuts.py`, `i18n.py`, `Program.cs`, `LiteEngineHandlers.cs`, `RadioMediaOutput.cs`; dodany `test_timeshift_buffer_folder.py`; zaktualizowana `wxlite/JAK_TESTOWAC.md`.
