# Spotify Soloist — przedwczesne przejście utworu

Data: 23.11.2026. Stan: kod i celowany test adaptera gotowe; wydanie Test alpha.517 w przygotowaniu.

## Zgłoszenia i zakres

- `spotify/soloist#40`: otwarte zgłoszenie autora `eduPHP` o samoczynnym pomijaniu kolejnych utworów po błędzie `playback_error_drm_denied` po pojedynczym `next`. Próba dotyczy Soloist 1.3.10.83 na Linux; późniejszy komentarz autora wskazuje, że niektóre utwory z playlisty zawodzą także po bezpośrednim uruchomieniu URI. Źródło: https://github.com/spotify/soloist/issues/40
- `spotify/soloist#3`: osobny nowy komentarz dotyczy jakości 160 kb/s na koncie Premium, nie kolejki. Źródło: https://github.com/spotify/soloist/issues/3
- AMC na Windows używa `soloist ctl play <uri>` i własnej kolejki. Nie wysyła `soloist ctl next` ani nie synchronizuje kolejki Spotify Connect. Nie zmieniono oficjalnego Soloist.

## Materiał do dokumentacji użytkownika

W sesji Spotify, gdy AMC korzysta z opcjonalnego Soloist, przedwczesne samoczynne przejście do innego utworu zgłasza komunikatem „Soloist zmienił utwór przed jego końcem. Sprawdź odtwarzanie w Spotify.” Nie ogłasza wtedy naturalnego końca i nie uruchamia kolejnego utworu z własnej kolejki. Przy rzeczywistym zakończeniu utworu kolejka AMC działa jak dotąd. Konfiguracja Soloist nadal jest w oknie konta Spotify otwieranym przez `Ctrl+F5`. Zmiana dotyczy profili Test i Public; katalog, tor Chrome i skróty pozostają takie same. AMC nie potwierdza faktycznego kodeka Soloist i nie naprawia błędu DRM w oficjalnym programie.

## Uzasadnienie i decyzje

Adapter wcześniej uznawał dwie kolejne migawki Soloist bez żądanego URI za naturalny koniec niezależnie od pozycji. Zgłoszenie #40 pokazało przypadek, w którym Soloist sam przeskakuje po błędzie. Adapter zapamiętuje ostatnią pozycję i czas trwania żądanego utworu. Koniec ogłasza tylko wtedy, gdy ostatnia pozycja była najwyżej trzy sekundy od końca; wcześniejszą zmianę zgłasza jako niepowodzenie. Jest to ochrona kolejki AMC, nie rozwiązanie przyczyny w Soloist. Brak znanego czasu trwania także nie uprawnia do automatycznego przesunięcia kolejki.

## Testy i wynik

- `dotnet run --project tests/AccessibleMediaController.SoloistAdapterTests/AccessibleMediaController.SoloistAdapterTests.csproj --configuration Release --no-restore`: 7 sprawdzeń zaliczonych na Windows, w tym naturalny koniec przy końcu utworu i wczesna zmiana URI bez przesunięcia kolejki. Test na atrapie CLI, bez konta Spotify i dźwięku.
- Kompilacja paczek, kontrola integralności, instalacja, próba NVDA i publikacja: do zapisania osobno po rzeczywistym wykonaniu.

## Znane ograniczenia

Heurystyka trzech sekund opiera się na kolejnych migawkach `soloist ctl now --json`, odpytywanych co 750 ms. Nie rozpoznaje dokładnego kodu błędu DRM ani przyczyny zewnętrznej zmiany utworu. Nie potwierdzono zachowania na żywym koncie ani na Windows z bieżącym Soloist.

## Wersja i commit

Poprawka w bieżącej gałęzi D: `cf6861eb`. W czystych źródłach najnowszego opublikowanego Test alpha.516 (`d998160c`): `acfb123a`. Kandydat Test: `0.1.0-alpha.517`; Public: `0.1.0-alpha.516`. Wersja Test alpha.516 z odsłuchem trwającego nagrania zostaje zachowana. Końcowy commit, manifest i tag do uzupełnienia po wydaniu.
