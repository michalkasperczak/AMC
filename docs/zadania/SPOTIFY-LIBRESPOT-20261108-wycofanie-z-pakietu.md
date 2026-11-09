# Wycofanie LibreSpot z paczek AMC — 8 listopada 2026

## Decyzja i uzasadnienie

Po próbach Huberta 31 października tor Spotify w bieżącym AMC Test został zastąpiony Web Playback SDK w Chrome. Potwierdza to `SPOTIFY-CHROME-20261031.md` i kod LiteHost: koordynator odtwarzania tworzy `LiteSpotifyChromeOutput`, nie uruchamia hosta LibreSpot. Pliki hosta i zapis `spotifyEngine: Librespot` w starszych profilach są pozostałościami. Uwaga użytkownika z 8 listopada 2026 koryguje wcześniejsze przedstawienie LibreSpot jako bieżącego silnika.

Usuwamy host i jego licencje z **nowych paczek obu profili**. Instalator przy aktualizacji usuwa dokładne dawne ścieżki w katalogu programu. Zachowujemy obsługę dawnych danych profilu oraz historyczny kod źródłowy, aby migracja i ewentualna analiza starszych wydań pozostały możliwe. Nie kasujemy ustawień, kont, nagrań ani prywatnych plików użytkownika.

## Materiał do dokumentacji użytkownika

- Spotify w AMC Test nadal odtwarza przez ukryty Chrome. Nie wymaga parowania LibreSpot. `Shift+A` wybiera wyjście dźwięku komputera; zapisane dawne nazwy wyjść są rozpoznawane podczas migracji.
- AMC Public zachowuje zakres Spotify określony przez profil danego wydania; samo wycofanie starego hosta nie dodaje logowania Spotify ani dostępu Web API.
- Po zainstalowaniu przyszłej aktualizacji znikną stare pliki LibreSpot z katalogu programu. Dane konta i ustawienia pozostaną.
- Ta zmiana nie daje Spotify Lossless. W torze Chrome Hubert zmierzył AAC 44,1 kHz; szczegóły w `SPOTIFY-CHROME-20261031.md`.

## Zmiany, weryfikacja i status

- `wxlite/tools/build_bundle.py`: nowe pełne paczki nie wymagają, nie kopiują i odrzucają pliki hosta LibreSpot niezależnie od profilu. Historyczne funkcje walidacji źródeł pozostają poza ścieżką budowy.
- `installer/AMC_Wx_Setup.iss`: oba profile usuwają stare foldery hosta i licencji z własnego katalogu instalacji przed kopiowaniem nowego pakietu.
- Testy struktury paczki i instalatora: `py -3.14 -m pytest wxlite/tests/test_bundle_layout.py wxlite/tests/test_installer_profiles.py -q` — 61 zaliczonych, 8 listopada 2026.
- Źródła i testy to obecny etap. Nowej paczki, instalatora, instalacji lokalnej ani wydania GitHub jeszcze nie wykonano. Weryfikacja rzeczywistej aktualizacji oraz zachowania NVDA pozostają do zrobienia przy najbliższym wydaniu.
- Commit: do uzupełnienia po bezpiecznym oddzieleniu tej zmiany od równoległych prac Sonos w kanonicznym drzewie roboczym.

## Szkic listy zmian do zatwierdzenia

- Wycofaliśmy nieużywany host LibreSpot z instalatorów AMC Test i AMC Public.
- Aktualizacja usuwa jego stare pliki programu, zachowując ustawienia i dane konta.
- Odtwarzanie Spotify w AMC Test nadal korzysta z Chrome.
