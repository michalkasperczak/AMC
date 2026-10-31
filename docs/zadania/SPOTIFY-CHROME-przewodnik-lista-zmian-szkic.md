# Szkic dla użytkownika: Spotify w AMC Test

Ten tekst jest szkicem do sprawdzenia i korekty użytkownika; nie jest opublikowaną instrukcją.

## Krótki przewodnik

1. W sesji Spotify otwórz konto skrótem `Ctrl+F5`. Wpisz Client ID swojej aplikacji Spotify, skopiuj podany adres powrotu do panelu aplikacji i zapisz identyfikator. Następnie wybierz logowanie. To jedno logowanie służy katalogowi i odtwarzaniu; parowanie odtwarzacza nie jest potrzebne.
2. Wybierz utwór lub odcinek i uruchom odtwarzanie zwykłym poleceniem AMC. Odtwarzacz Spotify działa w ukrytym Google Chrome. Potrzebne jest konto Premium, zainstalowany Chrome i dostęp konta do aplikacji Spotify.
3. Naciśnij `Shift+A`, aby wybrać fizyczne wyjście Windows, na przykład Denon USB. `Ctrl+[` i `Ctrl+]` przełączają sąsiednie wyjścia. Po wyborze AMC podaje nazwę urządzenia. Zmiana podczas utworu nie powinna zaczynać go od nowa.
4. Steruj pauzą, przewijaniem i głośnością tak jak w innych sesjach AMC.

## Lista zmian

- Utwory, które LibreSpot odrzucał z błędem klucza audio, mogą być odtwarzane przez oficjalny Spotify Web Playback SDK.
- Katalog i odtwarzanie korzystają z jednego logowania. Z okna konta znika osobne parowanie LibreSpot.
- `Shift+A` obsługuje wyjścia Windows także przy odtwarzaniu Spotify w Chrome; sprawdzono przełączenie na Denona USB.
- Wcześniejsze poprawki pola Client ID i zapisu kompletnej biblioteki pozostają dostępne.

Jakość Lossless w tym trybie nie jest potwierdzona. AMC Test wymaga systemowego Google Chrome z Widevine. Ręczny odbiór NVDA i odsłuch w zainstalowanej wersji pozostają do wykonania po aktualizacji.
