# Spotify: ocena Luny i ewentualnego usunięcia fallbacku Chrome

Stan 19.11.2026: analiza kodu zakończona. **Rekomendacja: na razie zachować fallback Chrome; usunięcie technicznie możliwe, ale wymaga jawnej decyzji o ograniczeniach Soloist.** Konsultacja z Hubertem zakończona: używa w AMC Chrome, bo Soloist nie jest skonfigurowany. Priorytetem są tempo podcastów i dostępne parowanie Soloist; szczegóły poniżej. Nie ma zgody na usunięcie kodu, instalację, logowanie ani publikację w tym zadaniu.

## Zakres i korekty Michała

- Ocenić wzorce Spotify z **diamondStar35/luna_player v0.8.0** względem obecnego AMC, nie innego programu o tej samej nazwie.
- Crossfade nie jest potrzebny. Ryzyka regulaminowe oddzielać od technicznej wykonalności.
- AMC ma już odtwarzanie bez Chrome przez **Soloist**. Nie przedstawiać tego jako brakującej nowości do przejęcia z Luny.
- Sprawdzić możliwość usunięcia tylko zapasowego toru Spotify Chrome i zapytać Huberta. To nie jest polecenie wykonania takiego usunięcia.
- Nie zmieniać działającej instalacji, odtwarzania ani profili użytkownika.

## Zweryfikowana architektura

Podstawa kodu AMC: `90d766a3c66e9cf95f8290d3fe7db6562583698d`, gałąź `codex/amc-test-public-20261109`. Drzewo przed analizą czyste. Nie scalano starszej gałęzi z origin.

1. Katalog/biblioteka i logowanie OAuth PKCE są oddzielone od wyjścia audio. Przeglądarka do logowania nie jest silnikiem audio.
2. `wxlite/amc_wx_lite/spotify_soloist.py` uruchamia osobny natywny `soloist.exe`, z danymi i kluczem osobnymi dla Test/Public. Soloist **nie działa wewnątrz Chrome**.
3. `LiteSpotifyPlaybackCoordinator.EnsureOutputLocked` i `RefreshOutputLocked` wybierają Soloist po poprawnej konfiguracji i potwierdzeniu sesji przez `ctl now --json`. W przeciwnym razie mogą utworzyć `LiteSpotifyChromeOutput`.
4. Ten drugi adapter uruchamia **systemowy Google Chrome**, bez widocznego okna, dla Web Playback SDK. Nie jest dużą dołączoną binarką przeglądarki do usunięcia z instalatora. Historyczne WPF używało WebView2; nie przenosić tego opisu na obecny LiteHost.
5. Status rozróżnia Soloist od Spotify Web tekstem; zdarzenie `playback.started` podaje jedynie sesję `spotify`, nie nazwę adaptera. Wybór fallbacku nie emituje osobnej przyczyny zmiany.
6. Adapter Soloist jest także w sprawdzonych tagach wydanych źródeł z 14–15 listopada. Nie jest wyłącznie dzisiejszym eksperymentem. Nie sprawdzano, który silnik działa teraz w instalacji Michała lub Huberta.

## Konsekwencje usunięcia fallbacku

- Bez konfiguracji, pliku EXE, gotowej sesji lub po wygaśnięciu Soloist Spotify nie zagra. Obecny fallback pokrywa te przypadki, jeśli katalog i Chrome są gotowe. Nie jest gwarancją ratowania każdej awarii w środku utworu.
- Soloist w obecnym adapterze udostępnia wyłącznie domyślne wyjście systemowe. Wariant Chrome ma obsługę nazwanych wyjść `Shift+A`. Rezygnacja z tej alternatywy wymaga świadomego zaakceptowania ograniczenia lub osobnego rozwiązania wyjść.
- Soloist jawnie odrzuca tempo inne niż 1.0. Chrome ma kod ustawiający `HTMLMediaElement.playbackRate` przez CDP; to **nie metoda oficjalnego SDK** i nie nowy dowód działania tempa na obecnym koncie. Nie obiecywać utraty/powrotu funkcji bez próby.
- Własna kolejka AMC pozostaje, ale usunięcie Chrome samo nie poprawia przejść. Adapter Soloist odpytuje CLI co 750 ms i wnioskuje o końcu po kolejnych migawkach. Nie utożsamiać tego z jednoznacznym EOS ani z kolejką Connect. Przejście na WebSocket wymaga osobnej oceny transferu urządzenia, zewnętrznego skip, pauzy i autoplay.
- Usunięcie adaptera Spotify nie oznacza usunięcia przeglądarki OAuth ani niezależnych hostów WebView2 TIDAL/Apple Music. Nie znaleziono ich bezpośredniej zależności od `LiteSpotifyChromeOutput`.

## Wnioski z Luny

Nie rekomendujemy podmiany całego silnika. Luna ma własny, nieoficjalny stos Spotify oraz niestandardowy mpv, a nie prostszą wersję obecnego adaptera Soloist. Jej własny kod ma GPL-3.0-or-later; MIT przy komponentach librespot nie obejmuje automatycznie całości. Nie kopiowano kodu.

Kandydaci do osobnych, niewdrożonych usprawnień AMC:

- Czytelny komunikat o `429`, z uwzględnieniem `Retry-After` i odróżnieniem kwoty aplikacji, jeśli odpowiedź rzeczywiście to potwierdza. AMC obecnie ma ogólne `RateLimited`; Luna ma bardziej szczegółowy kontrakt.
- Jednolite anulowanie i odrzucanie spóźnionych odpowiedzi katalogu przez token/generację. AMC ma już część ochron i stronicowanie; nie zakładać usterki bez reprodukcji. Najpierw mały test konkretnej granicy.

## Zalecany następny krok — bez automatycznego wdrożenia

1. Zachować Chrome: Hubert potwierdził rzeczywiste używanie tej ścieżki w AMC; Soloist w AMC nie jest u niego skonfigurowany. Osobno przeanalizować #11 i wykonalność trwałego tempa podcastów przez CDP. Samo ustawienie właściwości elementu nie dowodzi zmiany rzeczywistego dźwięku.
2. Jeśli wybiorą Soloist-only: najpierw jawna przyczyna niedostępności Soloist, informacja o rzeczywistym silniku oraz czytelne ograniczenia wyjść/tempa. Brak Soloist nie może kończyć się ogólnym błędem ani fałszywym startem.
3. Odbiór braku konfiguracji, EXE, klucza, sesji, wygaśnięcia i poprawnego Soloist na czystym Test/Public. Dopiero potem odwracalne wyłączenie fallbacku i ewentualne usunięcie adaptera. Oddzielić źródła, build, odsłuch i NVDA.

## Weryfikacja

Rodzic niezależnie przeczytał wybór silnika, start procesu, status, wyjścia, tempo i monitor Soloist. Prawdziwy pytest uruchomiony offline przez istniejący cache uv, bez GUI i konta:

- `test_spotify_soloist.py` + `test_spotify_source.py`: **24 passed in 1.04s**, exit 0.
- `test_spotify_navigation.py`: **22 passed in 0.31s**, exit 0.

Polecenie: `PYTHONPATH=wxlite:wxlite/tests uv run --offline --with pytest python -m pytest <pliki> -q --basetemp <profilowy scratch> -p no:cacheprovider`.

Nie uruchomiono ponownie testów C# adaptera ani pełnego buildu. Wcześniejsze próby wykonawcy z exit1 bez tracebacku są nierozstrzygające; nie uznawać ich za PASS ani zdiagnozowaną awarię produktu. Testy Python nie dowodzą prawdziwego audio, jakości, skuteczności zmiany urządzenia, logowania ani żywego NVDA. Nie instalowano i nie uruchamiano Luny.

## Kontakt z Hubertem

Pytanie dotyczyło tego, czy pozostawienie samego Soloist byłoby dla niego problemem, oraz z którego toru korzysta. Przypięta tożsamość Telegram zweryfikowana w ograniczonej skrzynce. Wysłana wiadomość `272`; odczyt tego dokładnego komunikatu przez `forwardMessage` potwierdził treść, kopia do Michała `273`. Bez sekretów w wiadomości.

Hubert w wiadomości `275` poprosił poczekać; nowsza pełna odpowiedź `279`, powtórzona i doręczona jako `282`, zastępuje ten stan. Potwierdził używanie Chrome w AMC Test z kontem Premium; Soloist w AMC nieskonfigurowany. Nie wyraził bezwarunkowej zgody na usunięcie Chrome. Wolałby go zachować, przynajmniej dla podcastów, jeśli da się skutecznie utrzymywać playbackRate przez CDP. Soloist-only byłby dla niego do przyjęcia pod warunkiem prowadzenia przez parowanie także w sieci publicznej.

Relacja Huberta, **nie nowy pomiar rodzica**: 30–31.10 Soloist poza AMC odtwarzał FLAC i zmieniał wyjście audio. Publiczne Wi-Fi i wirtualne adaptery WSL utrudniały wykrywanie mDNS; pomogło ręczne ogłoszenie urządzenia. Przycisk Soloist w obcej aplikacji Spotify był bez nazwy dla NVDA. Wskazał wygaśnięcie binarki po 90 dniach, klucz z panelu deweloperskiego i brak komendy prędkości w Web API oraz WebSocket API Soloist 1.3.10. Nie należy utożsamiać możliwości silnika wewnętrznego z komendą dostępną adapterowi AMC.

Michał zlecił analizę tych informacji oraz wskazanych przez Huberta zgłoszeń #11, #25, #26 i #28. Odczytowa ocena kodu i pełnych wątków zakończona; nie jest to zgoda na wdrożenie całej listy. [Pełny podział i kolejność prac](HUBERT-20261119-triage-zgloszen.md). Żadnych załączników ani linków z samej skrzynki nie pobierano.

Oddzielny błąd kanału kontaktu: wiadomości `263` i `279` zapisano, ale miały timeout powiadomienia. Obie treści dotarły w późniejszych powtórzeniach. Wtyczka skrzynki w profilu AMC, nie produkt AMC, została poprawiona do 0.2.1: nagłówek „Hubert:”, globalny budżet transportu uwzględnia przełączanie tras Telegrama. 31 testów PASS, aktywacja bez restartu, rzeczywista wysyłka i odczyt dokładnej wiadomości potwierdzone. Brak nowego inbound testu z konta Huberta i automatycznej kolejki retry po całkowitej awarii.

## Źródła publiczne

- https://github.com/diamondStar35/luna_player/releases/tag/v0.8.0
- https://github.com/diamondStar35/luna_player/blob/v0.8.0/NOTICE
- https://developer.spotify.com/documentation/soloist/concepts/authentication
- https://developer.spotify.com/documentation/soloist/reference/downloads-and-updates
- https://developer.spotify.com/documentation/soloist/reference/websocket-api
- https://developer.spotify.com/policy

Pełny przegląd wykonawcy wraz z korektami i odrębnymi wynikami rodzica zachowany w profilowym cache `luna-correct-research/amc-comparison-review.md`.
