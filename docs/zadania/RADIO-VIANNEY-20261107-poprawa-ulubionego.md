# RADIO-VIANNEY-20261107 — poprawa adresu ulubionego

## Stan

Zakończono zmianę danych użytkownika w AMC Test 7.11.2026. Odsłuch w oknie AMC i NVDA pozostaje do wykonania przez użytkownika; nie uruchamiano widocznego interfejsu.

## Materiał do dokumentacji użytkownika

W Radiu, w Ulubionych, stacja „Vianneya” zachowała nazwę i pozycję. Enter na stacji uruchamia transmisję z oficjalnego kanału parafii św. Jana Vianneya w Poznaniu. Adres prowadzi do bieżącej transmisji kanału, więc nie trzeba klikać odtwarzacza na stronie. Dźwięk zależy od tego, co nadaje parafia. AMC wymaga dostępnych składników yt-dlp i FFmpeg. Plan nagrywania tej stacji pozostał wyłączony.

## Uzasadnienie i decyzje

Poprzedni adres `http://85.221.248.82:8081/hls/live.stream.m3u8` nie przyjmował połączenia TCP na porcie 8081 i przekraczał czas łączenia w historii AMC. Oficjalna strona parafii kierowała przyciskiem „Transmisja” na `https://www.youtube.com/channel/UCwHcqK-ZyR0Uvzklq4AhpeA/live`. Zmieniono adres istniejącej stacji przez wąską operację `radio.editStation` hosta AMC, zachowując jej identyfikator, nazwę, flagę ulubionej oraz obecność w Bibliotece. Host zaktualizował także adres w wyłączonym planie nagrywania i utworzył kopię poprzedniego profilu.

Korekta użytkownika z 7.11.2026 zastępuje wcześniejszą ocenę, że brak odczytu filmu oznacza brak trwającej transmisji. Publiczna lista kanału oznaczała transmisję jako `is_live`. Wstępna, uproszczona próba yt-dlp zwróciła błąd, lecz ponowna próba z dokładnymi argumentami odtwarzacza AMC zwróciła bieżący film oraz format audio AAC w HLS.

## Testy i wynik

- Odczyt oficjalnej strony: przycisk „Transmisja” prowadzi do kanału YouTube.
- Połączenie TCP ze starym serwerem: nieudane; historia zapisanych nagrań zawiera błędy czasu łączenia.
- yt-dlp z argumentami AMC: powodzenie, stan `is_live`, wybrany format 96, ścieżka `mp4a.40.2`, protokół `m3u8_native`.
- Po zapisie odczyt profilu potwierdził nowy adres, `isFavorite=true`, `isInLibrary=true` oraz ten sam adres w wyłączonym planie nagrywania.
- Nie uruchamiano aplikacji, odsłuchu NVDA ani nagrywania, nie budowano i nie instalowano pakietu.

## Znane ograniczenia

Nie potwierdzono, czy w momencie próby nadawany był słyszalny dźwięk. Nie przeprowadzono odsłuchu w AMC. Dynamiczny adres YouTube zależy od aktualnej dostępności kanału i składników yt-dlp oraz FFmpeg.

## Wersja i commit

Zmiana dotyczyła danych profilu AMC Test, bez zmiany kodu źródłowego. Nie powstał commit, build, instalacja ani publikacja.
