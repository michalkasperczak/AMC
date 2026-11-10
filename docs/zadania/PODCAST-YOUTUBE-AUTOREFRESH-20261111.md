# Podcasty i YouTube — zbędny komunikat automatycznego odświeżania

## Stan i zachowanie dla użytkownika

11.11.2026 poprawiono kod źródłowy AMC Test. Automatyczna kontrola źródeł co minutę nie otwiera ponownie listy i nie powtarza nazwy kanału oraz liczby pozycji, jeżeli nie przybyło odcinków. Gdy przybyły odcinki i użytkownik przegląda listę, lista odświeża się bez dodatkowego komunikatu NVDA. W odtwarzaczu automatyczne odświeżenie nie przełącza widoku. Przy rzeczywiście nowych odcinkach pasek stanu nadal podaje ich liczbę. Ręczne F5 i Ctrl+F5 zachowują komunikaty wyniku.

## Przyczyna i zakres

`LiteFrame._refresh_due_podcasts_automatically` wywoływało `_refresh_profile_view` po każdym zapytaniu, także gdy `addedEpisodes=0`. Dla otwartego kanału prowadziło to przez `_open_podcast_view` do `apply_podcast_episodes`, które ogłaszało nazwę kanału i liczbę załadowanych odcinków (np. 150). Dodano warunek nowych pozycji i tryb cichego odświeżenia list podcastów i YouTube. Zmieniono `wxlite/amc_wx_lite/gui.py` i testy `wxlite/tests/test_podcast_refresh.py`; opis dopisano w `README_PL.md`. Nie zmieniono skrótów, nazw poleceń, menu ani dostępności funkcji.

## Dominikanie Lublin i zainstalowana wersja

Kanał `youtube-channel:UCeUwjPt4VxhNI0ULHLZ54BQ` dodano do Biblioteki i wyboru kanałów zbiorczej listy transmisji. Silnik instalacji, do której prowadzi skrót „AMC Test” (`C:\Users\micha\AppData\Local\Programs\AMC Test\host\amc_lite_host.exe`), zwraca jednak „Nieznana operacja youtube.channelBroadcasts”. Z tego powodu w zainstalowanej wersji nie można jeszcze potwierdzić ani pokazać transmisji tego kanału przez tę funkcję. Kod źródłowy bieżącej gałęzi zawiera tę operację; potrzebna jest późniejsza aktualizacja aplikacji i próba podczas transmisji. Manifest zainstalowanego pakietu wskazuje rewizję źródeł `4c89848245887397c2ef2c99055eb490b67783bf`.

## Testy, ograniczenia i wersja

Ukierunkowany zestaw `test_podcast_refresh.py`, `test_radio_schedule_management.py`, `test_menu_exposes_existing_actions.py`, `test_shortcuts.py`: 112 testów przeszło. Nie uruchamiano GUI ani NVDA, nie budowano i nie instalowano nowego pakietu. Nie wykonano próby transmisji Dominikanów o 18:00. Zmiany źródeł nie są jeszcze obecne w instalacji. Zastane, niezwiązane zmiany GUI i rozpoznawania utworów pozostawiono.
