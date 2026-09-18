# Spotify Lossless — wariant zewnętrzny i monitoring

## Decyzja Michała (18.09.2026)

Lossless jest ważne, ale NIE kosztem funkcjonalności ani rezygnacji z działającego odtwarzacza wbudowanego. Nie zastępować go pilotem oryginalnej aplikacji. Wariant zewnętrzny rozważyć wyłącznie jako opcję, jeżeli sterowanie będzie wyraźnie pełniejsze i wygodniejsze niż w TIDAL. Teraz badanie i zapis ustaleń, nie wdrożenie.

## Co potwierdza publiczna dokumentacja

Spotify Web API daje oficjalne sterowanie wybranym urządzeniem Connect (także aplikacją komputerową, gdy jest widoczna jako urządzenie i nie ma ograniczeń):

- `PUT /me/player/play?device_id=...` przyjmuje listę konkretnych URI utworów, kontekst albumu/playlisty, offset oraz `position_ms`. Nie wymaga szukania tytułu i klikania w okno jak obecna integracja TIDAL w AMC.
- `PUT /me/player/seek?position_ms=...&device_id=...` przewija do określonego czasu.
- `GET /me/player` podaje bieżący utwór/odcinek, postęp, stan oraz urządzenie.
- `GET /me/player/devices` podaje dostępne urządzenia i `is_restricted`; gdy ta flaga jest true, polecenia Web API są odrzucane. Lista nie obejmuje wszystkich modeli urządzeń.
- Dostępne są pauza/wznawianie, następny/poprzedni, regulacja głośności (jeśli `supports_volume`), transfer i dodanie do kolejki.
- Sterowanie odtwarzaniem wymaga Premium i zakresów `user-modify-playback-state` oraz `user-read-playback-state`; AMC już prosi o oba zakresy.
- Kolejność realizacji różnych poleceń Player API nie jest gwarantowana; potrzebna serializacja i odczyt stanu po operacji. HTTP 204 samo nie dowodzi, że gra żądany utwór.

Zatem projektowo wariant zewnętrzny może być znacznie sprawniejszy niż obecne sterowanie TIDAL-em: adres konkretnego utworu, prawdziwa pozycja i przewijanie zamiast klików oraz przybliżonego zegara. NIE jest to jeszcze pomiar działania na komputerze Michała.

## Jakość — osobny warunek

Publiczny Web Playback SDK nie ma udokumentowanego przełącznika Lossless. Spotify podaje Web Player Premium AAC 256 kb/s. To deklaracja dostawcy, nie zmierzony bitrate utworu.

Oficjalna aplikacja komputerowa Spotify obsługuje Lossless; dokumentacja wymienia od wersji 1.2.67, do 24 bit/44,1 kHz FLAC. Ustawienie Lossless trzeba wybrać w aplikacji/na docelowym urządzeniu. Web API nie udostępnia udokumentowanego ustawienia jakości ani pola potwierdzającego Lossless w `GET /me/player`. Samo wysłanie polecenia z AMC NIE potwierdza jakości. Potwierdzenia trzeba szukać w oryginalnym Spotify (wskaźnik jakości aktualnego odtwarzania).

Lossless nie dotyczy podcastów, audiobooków ani teledysków. Połączenie Bluetooth może wprowadzić ponowną kompresję.

## Zmierzony stan lokalny, bez ingerencji

Na komputerze głównym jest pakiet SpotifyAB.SpotifyMusic w wersji pakietu 1.300.277.0. W czasie sprawdzenia proces Spotify nie działał, AMC alpha.393 działało w sesji 1. Nie uruchamiano Spotify, nie zmieniano jakości i nie przerywano muzyki. Wersja pakietu Microsoft Store nie jest dowodem wersji wewnętrznej aplikacji ani aktywnego Lossless.

## Warunki przyszłej próby

Za osobnym uzgodnieniem momentu, bez zastępowania wbudowanego odtwarzacza: uruchomić oryginalne Spotify; sprawdzić widoczność urządzenia i brak ograniczeń; zagrać DWA różne wskazane URI, potwierdzić tytuły i postęp; przewinąć w przód i tył, potwierdzić wynik; przetestować pauzę, własną kolejkę AMC i powrót do odtwarzacza wbudowanego; potwierdzić wskaźnik Lossless w oryginalnej aplikacji. Bez tego nie ogłaszać wariantu gotowym ani bezstratnym.

## Monitoring

Sprawdzać okresowo zmiany oficjalnego Web Playback SDK (Lossless, wybór wyjścia, raportowanie jakości), Player Web API oraz ograniczeń trybu deweloperskiego. Nie przebudowywać integracji automatycznie. Zmiana w dokumentacji uruchamia ocenę i ewentualną sondę, nie wdrożenie.

## Librespot — sprawdzone u źródła 18.09.2026

Librespot to nieoficjalny otwarty silnik odtwarzania Spotify, a nie pilot oryginalnej aplikacji. Biblioteka może działać bez WebView2, jako odbiornik Connect; obsługuje Windows, wyjście `rodio`, listowanie i wskazanie urządzenia przez `--device`. Dokumentuje jakości 96/160/320 kb/s, nadal stratne, i wymaga Premium. Większa liczba kb/s niż AAC z Web Playera nie dowodzi sama lepszej jakości.

Nie daje Spotify Lossless. Zgłoszenie upstream #1583 ma tytuł „Spotify lossless will not be supported” i jest zablokowane. W komentarzu z 06.11.2025 członek projektu `roderickvd` potwierdził kontakt Spotify i zakaz dalszych prac obchodzących zabezpieczenia techniczne. Włączenie dekodera FLAC do v0.8.0 (PR #1589) nie jest dostępem do bezstratnych strumieni Spotify — to różne warstwy.

Dla AMC: potencjalnie większa kontrola nad urządzeniem audio bez sterowania oryginalnym klientem, ale konieczny osobny silnik i nieoficjalny protokół. Nie zastępujemy działającego Web Playback SDK. Nie uruchamiano librespot ani nie logowano nim konta Michała; wnioski dotyczą opublikowanych możliwości, nie testu na jego koncie.

Źródła:
- https://github.com/librespot-org/librespot
- https://github.com/librespot-org/librespot/wiki/Options
- https://github.com/librespot-org/librespot/issues/1583#issuecomment-3499343020
- https://github.com/librespot-org/librespot/pull/1589

## Źródła

- https://developer.spotify.com/documentation/web-playback-sdk/reference
- https://support.spotify.com/us/article/audio-quality/
- https://support.spotify.com/us/article/lossless-audio-quality/
- https://developer.spotify.com/documentation/web-api/reference/start-a-users-playback
- https://developer.spotify.com/documentation/web-api/reference/seek-to-position-in-currently-playing-track
- https://developer.spotify.com/documentation/web-api/reference/get-information-about-the-users-current-playback
- https://developer.spotify.com/documentation/web-api/reference/get-a-users-available-devices
- https://developer.spotify.com/documentation/web-api/references/changes/february-2026
