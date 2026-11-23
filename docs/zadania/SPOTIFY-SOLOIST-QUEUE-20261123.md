# Spotify Soloist — przedwczesne przejście utworu

Data: 23.11.2026. Stan: kod i celowany test adaptera gotowe; wydanie Public w przygotowaniu.

## Zgłoszenie i zakres

`spotify/soloist#40` jest otwartym zgłoszeniem autora `eduPHP` o samoczynnym pomijaniu utworów po błędzie `playback_error_drm_denied` po pojedynczym `next`. Próba dotyczy Soloist 1.3.10.83 na Linux. Komentarz autora wskazuje także niepowodzenie uruchomienia niektórych URI bezpośrednio. Źródło: https://github.com/spotify/soloist/issues/40

AMC na Windows używa `soloist ctl play <uri>` i własnej kolejki. Nie wysyła `soloist ctl next` ani nie synchronizuje kolejki Spotify Connect. Ta poprawka chroni kolejkę AMC; nie zmienia oficjalnego Soloist.

## Materiał do dokumentacji użytkownika

W sesji Spotify, gdy AMC korzysta z opcjonalnego Soloist, przedwczesne samoczynne przejście do innego utworu zgłasza komunikatem „Soloist zmienił utwór przed jego końcem. Sprawdź odtwarzanie w Spotify.” Nie ogłasza wtedy naturalnego końca i nie uruchamia kolejnego utworu z własnej kolejki. Przy rzeczywistym zakończeniu utworu kolejka AMC działa jak dotąd. Konfiguracja Soloist jest w oknie konta Spotify otwieranym przez `Ctrl+F5`. AMC nie potwierdza faktycznego kodeka Soloist i nie naprawia błędu DRM w oficjalnym programie.

## Uzasadnienie i decyzje

Adapter wcześniej uznawał dwie kolejne migawki Soloist bez żądanego URI za naturalny koniec niezależnie od pozycji. Teraz zapamiętuje ostatnią pozycję i czas trwania żądanego utworu. Koniec ogłasza tylko wtedy, gdy ostatnia pozycja była najwyżej trzy sekundy od końca. Wcześniejszą zmianę zgłasza jako niepowodzenie. Brak znanego czasu trwania także nie uprawnia do automatycznego przesunięcia kolejki.

## Testy i wynik

`dotnet run --project tests/AccessibleMediaController.SoloistAdapterTests/AccessibleMediaController.SoloistAdapterTests.csproj --configuration Release --no-restore`: 7 sprawdzeń zaliczonych na Windows, w tym naturalny koniec przy końcu utworu i wczesna zmiana URI bez przesunięcia kolejki. Test na atrapie CLI, bez konta Spotify i dźwięku. Kompilacja paczki, kontrola integralności, instalacja, próba NVDA i publikacja: do zapisania osobno po wykonaniu.

## Znane ograniczenia

Heurystyka trzech sekund opiera się na migawkach `soloist ctl now --json`, odpytywanych co 750 ms. Nie rozpoznaje dokładnego kodu DRM ani przyczyny zewnętrznej zmiany utworu. Nie potwierdzono zachowania na żywym koncie ani na Windows z bieżącym Soloist.

## Wersja i commit

Źródło poprzedniego Public: `abc11a9d`; poprawka: `7c4326d4`. Kandydat techniczny: `0.1.0-alpha.516`; końcowy commit, manifest i tag do uzupełnienia po wydaniu.
