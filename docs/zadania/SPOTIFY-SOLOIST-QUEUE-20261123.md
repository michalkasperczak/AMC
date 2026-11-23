# Spotify Soloist — przedwczesne przejście utworu

Data: 23.11.2026. Stan: Test alpha.517 opublikowany na GitHubie; instalacja i odbiór NVDA pozostają do wykonania przez użytkownika.

## Zgłoszenia i zakres

- `spotify/soloist#40`: otwarte zgłoszenie autora `eduPHP` o samoczynnym pomijaniu kolejnych utworów po błędzie `playback_error_drm_denied` po pojedynczym `next`. Próba dotyczy Soloist 1.3.10.83 na Linux; późniejszy komentarz autora wskazuje, że niektóre utwory z playlisty zawodzą także po bezpośrednim uruchomieniu URI. Źródło: https://github.com/spotify/soloist/issues/40
- `spotify/soloist#3`: osobny nowy komentarz dotyczy jakości 160 kb/s na koncie Premium, nie kolejki. Źródło: https://github.com/spotify/soloist/issues/3
- AMC na Windows używa `soloist ctl play <uri>` i własnej kolejki. Nie wysyła `soloist ctl next` ani nie synchronizuje kolejki Spotify Connect. Nie zmieniono oficjalnego Soloist.

## Materiał do dokumentacji użytkownika

W sesji Spotify, gdy AMC korzysta z opcjonalnego Soloist, przedwczesne samoczynne przejście do innego utworu zgłasza komunikatem „Soloist zmienił utwór przed jego końcem. Sprawdź odtwarzanie w Spotify.” Nie ogłasza wtedy naturalnego końca i nie uruchamia kolejnego utworu z własnej kolejki. Przy rzeczywistym zakończeniu utworu kolejka AMC działa jak dotąd. Konfiguracja Soloist nadal jest w oknie konta Spotify otwieranym przez `Ctrl+F5`. Zmiana dotyczy profili Test i Public; katalog, tor Chrome i skróty pozostają takie same. AMC nie potwierdza faktycznego kodeka Soloist i nie naprawia błędu DRM w oficjalnym programie.

## Uzasadnienie i decyzje

Adapter wcześniej uznawał dwie kolejne migawki Soloist bez żądanego URI za naturalny koniec niezależnie od pozycji. Zgłoszenie #40 pokazało przypadek, w którym Soloist sam przeskakuje po błędzie. Adapter zapamiętuje ostatnią pozycję i czas trwania żądanego utworu. Koniec ogłasza tylko wtedy, gdy ostatnia pozycja była najwyżej trzy sekundy od końca; wcześniejszą zmianę zgłasza jako niepowodzenie. Jest to ochrona kolejki AMC, nie rozwiązanie przyczyny w Soloist. Brak znanego czasu trwania także nie uprawnia do automatycznego przesunięcia kolejki.

## Testy i wynik

- `dotnet run --project tests/AccessibleMediaController.SoloistAdapterTests/AccessibleMediaController.SoloistAdapterTests.csproj --configuration Release -p:NuGetAudit=false`: 7 sprawdzeń zaliczonych na Windows, w tym naturalny koniec przy końcu utworu i wczesna zmiana URI bez przesunięcia kolejki. Test na atrapie CLI, bez konta Spotify i dźwięku.
- Paczka Test: 3649 plików, kontrola integralności i ukryty start interfejsu/hosta przeszły. Instalator przeszedł kontrolę źródeł, SHA-256 i sekretów. Publikacja GitHub zweryfikowała obecność i rozmiar trzech plików. Nie instalowano programu ani nie wykonywano próby NVDA i żywego Spotify.

## Znane ograniczenia

Heurystyka trzech sekund opiera się na kolejnych migawkach `soloist ctl now --json`, odpytywanych co 750 ms. Nie rozpoznaje dokładnego kodu błędu DRM ani przyczyny zewnętrznej zmiany utworu. Nie potwierdzono zachowania na żywym koncie ani na Windows z bieżącym Soloist.

## Wersja i commit

Poprawka w bieżącej gałęzi D: `cf6861eb`. Baza Test alpha.516: `d998160c`; przeniesienie poprawki: `acfb123a`; commit wydania: `9d5c2011252bf7a751141a168950c1ec2345a85c`. Wersja `0.1.0-alpha.517`, tag `test-2026.11.23.1317`, instalator `AMC-Test-2026-11-23-1317.exe` (214458742 B), SHA-256 `5e9968cfc182e38c2ff62bf6191ed9df66faa88bd4370a0c9ee963c1ef66ac63`. Manifest `amc-update-test.json`. Wydanie: https://github.com/michalkasperczak/AMC/releases/tag/test-2026.11.23.1317. Public alpha.516 wydano osobno; funkcja odsłuchu trwającego nagrania z Test alpha.516 została zachowana.
