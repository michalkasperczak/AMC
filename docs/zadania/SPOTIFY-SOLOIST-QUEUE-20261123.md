# Spotify Soloist — przedwczesne przejście utworu

Data: 23.11.2026. Stan: Public alpha.516 opublikowany na GitHubie; instalacja i odbiór NVDA pozostają do wykonania przez użytkownika.

## Zgłoszenie i zakres

`spotify/soloist#40` jest otwartym zgłoszeniem autora `eduPHP` o samoczynnym pomijaniu utworów po błędzie `playback_error_drm_denied` po pojedynczym `next`. Próba dotyczy Soloist 1.3.10.83 na Linux. Komentarz autora wskazuje także niepowodzenie uruchomienia niektórych URI bezpośrednio. Źródło: https://github.com/spotify/soloist/issues/40

AMC na Windows używa `soloist ctl play <uri>` i własnej kolejki. Nie wysyła `soloist ctl next` ani nie synchronizuje kolejki Spotify Connect. Ta poprawka chroni kolejkę AMC; nie zmienia oficjalnego Soloist.

## Materiał do dokumentacji użytkownika

W sesji Spotify, gdy AMC korzysta z opcjonalnego Soloist, przedwczesne samoczynne przejście do innego utworu zgłasza komunikatem „Soloist zmienił utwór przed jego końcem. Sprawdź odtwarzanie w Spotify.” Nie ogłasza wtedy naturalnego końca i nie uruchamia kolejnego utworu z własnej kolejki. Przy rzeczywistym zakończeniu utworu kolejka AMC działa jak dotąd. Konfiguracja Soloist jest w oknie konta Spotify otwieranym przez `Ctrl+F5`. AMC nie potwierdza faktycznego kodeka Soloist i nie naprawia błędu DRM w oficjalnym programie.

## Uzasadnienie i decyzje

Adapter wcześniej uznawał dwie kolejne migawki Soloist bez żądanego URI za naturalny koniec niezależnie od pozycji. Teraz zapamiętuje ostatnią pozycję i czas trwania żądanego utworu. Koniec ogłasza tylko wtedy, gdy ostatnia pozycja była najwyżej trzy sekundy od końca. Wcześniejszą zmianę zgłasza jako niepowodzenie. Brak znanego czasu trwania także nie uprawnia do automatycznego przesunięcia kolejki.

## Testy i wynik

`dotnet run --project tests/AccessibleMediaController.SoloistAdapterTests/AccessibleMediaController.SoloistAdapterTests.csproj --configuration Release -p:NuGetAudit=false`: 7 sprawdzeń zaliczonych na Windows, w tym naturalny koniec przy końcu utworu i wczesna zmiana URI bez przesunięcia kolejki. Test na atrapie CLI, bez konta Spotify i dźwięku. Paczka Public: 2919 plików, kontrola integralności i ukryty start interfejsu/hosta przeszły. Instalator przeszedł kontrolę źródeł, SHA-256 i sekretów. Publikacja GitHub zweryfikowała obecność i rozmiar trzech plików. Nie instalowano programu ani nie wykonywano próby NVDA i żywego Spotify.

## Znane ograniczenia

Heurystyka trzech sekund opiera się na migawkach `soloist ctl now --json`, odpytywanych co 750 ms. Nie rozpoznaje dokładnego kodu DRM ani przyczyny zewnętrznej zmiany utworu. Nie potwierdzono zachowania na żywym koncie ani na Windows z bieżącym Soloist.

## Wersja i commit

Źródło poprzedniego Public: `abc11a9d`; poprawka: `7c4326d4`; commit wydania: `460d1dcff669f27c1a8fdb743e868aba2c12665d`. Wersja `0.1.0-alpha.516`, tag `public-2026.11.23.1309`, instalator `AMC-Public-2026-11-23-1309.exe` (133965472 B), SHA-256 `64dd1dd7795763bc3d57ba7dfff5db51e06a69d40033843345e5076ac6f5fbb8`. Manifest `amc-update-public.json`. Wydanie: https://github.com/michalkasperczak/AMC/releases/tag/public-2026.11.23.1309
