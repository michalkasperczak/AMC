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


## Korekta z 11.11.2026: harmonogram i Dominikanie Lublin

Ta korekta zastępuje zdanie z 7.11.2026, że plan Vianneya pozostał wyłączony. Użytkownik polecił włączyć go codziennie 18:40–19:00. Obowiązujący prywatny plan AMC Test (`radio_schedule_overrides`, ID `0ebabbd653f245d6b69b57f49888ef30`) jest teraz włączony, ma regułę `Daily`, 20 minut, najbliższy start 11.11.2026 o 18:40 czasu Warszawy i adres `https://www.youtube.com/channel/UCwHcqK-ZyR0Uvzklq4AhpeA/live`. Wśród 52 planów zmieniono tylko ten wpis. Zachowano kopie stanu sprzed naprawy URL i sprzed włączenia planu w katalogu `C:\Users\micha\AppData\Roaming\AMC\ui`.

Przyczyna rozbieżności: F2 zmienił adres stacji w Bibliotece i w profilu hosta, ale prywatna kopia harmonogramów interfejsu wxPython nadal zawierała poprzedni adres HLS. Ulubiona stacja i wpis Biblioteki miały ten sam identyfikator `radio:imported:c6285eff08c1417696d0704492c9dd0e`. Funkcja `effective_schedules` wybiera prywatną kopię, więc plan mógł nadal próbować starego serwera. Zmieniono prywatną kopię danych oraz kod F2, aby przyszła edycja nazwy lub adresu stacji aktualizowała wszystkie powiązane prywatne plany i synchronizowała włączone plany z hostem. Komunikat NVDA informuje o częściowej awarii zapisu planu lub potrzebie ponownego uruchomienia AMC po awarii synchronizacji.

Zaktualizowano opis F2 w menu, pomocy skrótów oraz README_PL.md. Nie dodano nowego polecenia ani skrótu. Gdy AMC nie jest uruchomione, zapis prywatny zostanie odczytany po następnym starcie.

Oficjalna strona Dominikanów Lublin prowadzi do `https://www.youtube.com/@DominikanieLublinYT/streams` i podaje transmisje od poniedziałku do soboty o 18:00 oraz w niedzielę o 12:00. Kanału nie było w bazie subskrypcji AMC. Dodano go do Biblioteki jako `youtube-channel:UCeUwjPt4VxhNI0ULHLZ54BQ`, pobrano 100 pozycji i dodano do wyboru kanałów zbiorczej listy transmisji na żywo. Nie jest to dowód widoczności transmisji o 18:00; wymaga sprawdzenia podczas emisji.

Testy: 101 testów ukierunkowanych przeszło (`test_radio_schedule_management.py`, `test_menu_exposes_existing_actions.py`, `test_shortcuts.py`). Ponowny odczyt przez `effective_schedules` potwierdził jeden włączony plan Vianneya, adres YouTube, 18:40, 20 minut, `Daily`. Nie uruchamiano GUI, NVDA ani próbnego nagrywania. Zmieniono dane użytkownika i kod źródłowy, lecz nie budowano, nie instalowano i nie publikowano nowej wersji. Poprawkę kodu F2 zapisano w commicie `0d1ec78f`; w repozytorium były też zastane, niezwiązane zmiany GUI i rozpoznawania utworów, które pozostawiono.


## Doprecyzowanie z 11.11.2026: niedziela i powiązanie planu

Użytkownik zgłosił także problem z niedzieli 8.11.2026 i potwierdził, że wcześniej zmienił adres stacji klawiszem F2, a w harmonogramie tylko ją włączył. Kontrola wykazała jeden plan Vianneya (ID `0ebabbd653f245d6b69b57f49888ef30`) w profilu hosta i jego prywatną kopię w stanie wxPython. To dwa zapisy jednego planu, nie dwa oddzielne harmonogramy. Przed naprawą prywatna kopia miała stary adres HLS. Historia profilu zawiera nieudane próby Vianneya z 18 i 19.09.2026 o 18:42; dostępna historia profilu i interfejsu nie zawiera zdarzenia z 8.11.2026 około 18:00. Stary adres jest wiarygodnym wyjaśnieniem, ale przyczyny konkretnej niedzielnej próby nie można potwierdzić z zachowanych wpisów.

Powiązanie planu ze stacją opiera się na trwałym `stationId`, a nazwa jest etykietą dla użytkownika. Plan przechowuje własną kopię `streamUrl`. Sama jednakowa nazwa dwóch stacji nie gwarantuje ich tożsamości ani aktualizacji planu. Host przy F2 aktualizował plany w swoim profilu po `stationId`, lecz prywatna kopia planu w wxPython pozostała stara. Commit `0d1ec78f` poprawia tę drugą ścieżkę w źródłach, ale nie jest jeszcze zainstalowany. Do aktualizacji programu ponowna zmiana F2 może znów pozostawić dawny URL w prywatnym planie; obecnie plan Vianneya został skorygowany bezpośrednio w danych.


## Korekta użytkownika z 11.11.2026: niedzielny plik

Użytkownik wyjaśnił, że niedzielny plik nagrania istniał: odsłuchał go, a następnie przypadkowo usunął; nie ma go też w historii. To zastępuje wcześniejszą sugestię, że brak wpisu z 8.11.2026 może świadczyć o niewykonaniu planu. Sam brak pliku lub wpisu nie rozstrzyga, czy inne niedzielne próby miały błąd starego adresu.
