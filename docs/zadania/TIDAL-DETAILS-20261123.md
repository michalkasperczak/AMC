# TIDAL: relacje i długości po szybkim dodawaniu — 23.11.2026

## Zachowanie użytkowe

- Ulubione TIDAL: prawa strzałka pokazuje `Przejdź do albumu` i `Przejdź do wykonawcy`, jeśli te relacje są znane dla wskazanego utworu. Nie pokazuje technicznych identyfikatorów i nie dopisuje pozycji do biblioteki.
- Albumy TIDAL: lewa strzałka podaje zapisany czas. Jeżeli odświeżona odpowiedź pominie wcześniej znany czas, zachowywana jest ostatnia znana wartość dla tego samego zasobu. Jeżeli czas nie był znany, jawne naciśnięcie lewej strzałki pobiera utwory albumu i podaje sumę tylko przy kompletnych czasach. W przeciwnym razie mówi `Czas trwania nieznany`.
- Ulubiony utwór bez czasu pod lewą strzałką dostaje komunikat `Czas trwania nieznany` zamiast ciszy. Szybkie chodzenie po liście nie powoduje dodatkowych żądań.
- Aktualne członkostwo w Ulubionych/Bibliotece pochodzi ze świeżej odpowiedzi. Zachowywane są wyłącznie brakujące fakty katalogowe wcześniejszej pozycji o tym samym identyfikatorze i rodzaju. Powiązane nazwy są zachowywane tylko przy tym samym identyfikatorze powiązania.

## Ustalenie przyczyny i zakres

Odczyt profilu AMC Test wykazał 674 zapisane albumy TIDAL bez brakującego `durationTicks` oraz 136 zapisanych utworów z identyfikatorami albumu i wykonawcy. Obejmuje to wskazane albumy Grzegorza Turnaua. Świeża odpowiedź katalogu może pominąć niektóre metadane; przed zmianą synchronizacja zastępowała nimi pełniejsze rekordy. Nie potwierdzono przeciążenia ani błędu kolejki wywołań przez szybkie dodawanie. Po stronie hosta operacje kolekcji są serializowane. Poprawka dotyczy adaptera TIDAL; nie zmienia sesji lokalnej, Apple Music ani Spotify, których źródła metadanych są inne.

## Zmiany i położenie

- `src/AccessibleMediaController.Core/Tidal/TidalCollectionMetadata.cs`: zachowanie znanych faktów przy niepełnym odświeżeniu.
- `src/AccessibleMediaController.Windows/Services/TidalIntegrationService.cs`: użycie tej zasady przy synchronizacji każdej rodziny zasobów TIDAL.
- `wxlite/amc_wx_lite/gui.py` i `tidal_source.py`: odczyt lewą strzałką, sprawdzenie czasu albumu na żądanie, odrzucenie spóźnionego wyniku po zmianie zaznaczenia.
- `tests/AccessibleMediaController.Core.SmokeTests/Program.cs`, `wxlite/tests/test_tidal_navigation.py` i `test_tidal_source.py`: regresja metadanych oraz odczytu.
- Skróty, menu relacji, paleta poleceń i pomoc nadal używają istniejących poleceń; nie wprowadzono nowego polecenia ani zmiany dostępności. Opis zachowania uzupełniono w `README_PL.md` i `PROJEKT_TIDAL_PL.md`.

## Weryfikacja i wydanie

- Źródła: zmienione na gałęzi `codex/amc-current-d-20261123` przy bazowej rewizji `cf6861eb`; bez nowego commitu.
- Python: `python wxlite\\run_tests.py tidal_navigation tidal_source` — 42 zaliczone, 0 błędów, 0 pominiętych po korekcie umieszczenia metody.
- Core C#: sprawdzenie `TestTidalIntegrationFoundation` zaliczone w uruchomionym programie smoke. Argument filtra nie ograniczył tego programu, więc szeroki przebieg przerwano po zaliczeniu testu TIDAL; nie traktować tego jako wyniku pełnego zestawu.
- Host: `dotnet build src\\AccessibleMediaController.LiteHost\\AccessibleMediaController.LiteHost.csproj --no-restore -v:q` — kompilacja pomyślna, 0 ostrzeżeń, 0 błędów.
- `git diff --check` — bez błędów białych znaków.
- Nie instalowano, nie uruchamiano ani nie publikowano nowej paczki. Odbiór klawiaturą i NVDA w aplikacji pozostaje do wykonania przez użytkownika po przygotowaniu wydania. Użytkownik 23.11.2026 wskazał własny odbiór NVDA jako właściwy etap i nie chce dodatkowej szerokiej regresji.

Korekta wydania z 23.11.2026: tę ukończoną poprawkę wyodrębniono do połączonego
źródła alpha.518 dla Test i Public. Szczegóły testu, buildu i publikacji są w
`RELEASE-ALPHA518-20261123.md`; powyższy stan odnosi się do wcześniejszej
gałęzi roboczej przed wyodrębnieniem.

## Projekt instrukcji i listy zmian

Instrukcja: w TIDAL otwórz Ulubione przez `Ctrl+U`. Na utworze naciśnij prawą strzałkę, wybierz album albo wykonawcę i zatwierdź Enterem. Na liście albumów naciśnij lewą strzałkę, aby odczytać czas; przy brakującym czasie AMC sprawdzi utwory dopiero wtedy.

Lista zmian: zachowanie relacji i czasów TIDAL po niepełnym odświeżeniu; odczyt długości albumu na żądanie; jasna informacja o nieznanym czasie. Tekst jest szkicem do korekty użytkownika, bez publikacji.
