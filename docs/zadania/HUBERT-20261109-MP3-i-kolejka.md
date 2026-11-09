# Zgłoszenia Huberta z 9 listopada 2026: MP3 i kolejka plików

Status: **W trakcie** od 9.11.2026 na polecenie użytkownika. Poprawki #15 i #16 są w kodzie i przeszły testy celowane, ale bez instalacji, odbioru NVDA, odsłuchu i publikacji. Zgłoszenia dotyczą testowanej przez Huberta wersji Test `alpha.486`; wydana `alpha.487` jeszcze ich nie zawiera.

## Źródła i zakres

- [#15 — przester MP3](https://github.com/michalkasperczak/AMC/issues/15), zgłoszone 9.11.2026 o 20:19 czasu polskiego.
- [#16 — kolejka znika po ponownym uruchomieniu](https://github.com/michalkasperczak/AMC/issues/16), zgłoszone chwilę później.
- Oba zgłoszenia wpłynęły po zbudowaniu paczek Test/Public `alpha.487` z tego dnia. Żadna z tych paczek nie zawiera poprawek opisanych poniżej. Kod jest wspólny dla obu profili; objawy potwierdzono dotąd tylko u Huberta w Test.

## #15 — MP3

Hubert wykazał na próbce MP3 szczyt `+6,37 dBFS` przed dekodowaniem i 447 spłaszczonych próbek po Media Foundation. `WindowsMediaOutput.cs` kieruje `.mp3` do osobnej gałęzi z `AudioFileReader` lub `StreamMediaFoundationReader`; preferowana ścieżka `FfmpegLocalAudioWaveStream` obejmuje obecnie transport stream i pliki częściowe, lecz nie MP3. Dopisanie `.mp3` do listy rozszerzeń nie wystarczy, ponieważ wcześniejsza gałąź przechwytuje te pliki. Samo obniżenie głośności po Media Foundation nie odzyska utraconych szczytów.

`IsDecoderFailure` obejmuje każdy `COMException`. Podany przez Huberta `0x88890004` po odłączeniu słuchawek może więc błędnie uruchamiać odzyskiwanie dekodera zamiast obsługi zmiany urządzenia wyjściowego.

Decyzja po poleceniu użytkownika z 9.11: wdrożono ścieżkę FFmpeg float z ogranicznikiem dla lokalnych MP3 oraz oddzielono błąd urządzenia WASAPI od błędu dekodera. Przed wydaniem pozostaje odsłuch głośnego pliku z #15, zwykłych MP3, prędkości, wznowienia i fizycznego odłączenia słuchawek. Nie deklarować bezstratności ani wierności bitowej bez pomiaru wyjścia.

## #16 — zwykła kolejka plików

Domyślny `resolve_layout()` zwraca `READ_ONLY_MIRROR`. W tym układzie `_queue_persistence_arguments()` nie przekazuje hostowi `--profile-dir` i `--queue-write`, a `_note_queue_persistence()` pomija komunikat, bo zapis nie był żądany. Kolejka plików zostaje tylko w pamięci. Ten stan występuje także w kodzie `alpha.487`.

Decyzja po poleceniu użytkownika z 9.11: wdrożono własny, odrębny magazyn kolejki profilu Test/Public, bez zapisu do dawnej współdzielonej bazy. Automatyczne testy obejmują restart hosta, pustą kolejkę, brakujący plik i uszkodzony zapis. Gdy magazyn jest niedostępny, AMC startuje z kolejką w pamięci i ogłasza to raz przy użyciu kolejki. Przed wydaniem pozostaje próba całej instalacji w obu profilach oraz odbiór NVDA.

Kolejka Spotify przechowywana w `state_store.py` jest osobnym mechanizmem. Zgłoszenie #16 dotyczy kolejki lokalnych plików AMC; nie należy traktować jej zapisu jako dowodu działania drugiej.

## Materiał do dokumentacji użytkownika

Projekt po wdrożeniu: „Kolejka plików jest zapisywana osobno w AMC Test i AMC Public i wraca po ponownym uruchomieniu. Jeśli plik został przeniesiony lub usunięty, AMC informuje o niedostępnym elemencie i pozwala go usunąć z kolejki.” Wymaga korekty po rzeczywistych testach.

Projekt komunikatu NVDA przy niedostępnym zapisie: „Kolejka działa tylko do zamknięcia AMC. Nie można otworzyć miejsca zapisu.” Odczyt jednokrotny przy wejściu do kolejki lub jej zmianie; zwykły udany zapis nie ogłasza się. Kolejka Spotify nie zmienia się w tym zadaniu.

## Uzasadnienie i decyzje

Music Assistant to niezależny projekt, którego implementację preferencji oficjalnego Soloist analizowano przy żądaniu Spotify Lossless. Nie jest częścią AMC ani przyczyną zgłoszeń #15/#16. Te zgłoszenia są od Huberta i dotyczą własnego odtwarzacza oraz kolejki AMC.

## Testy i wynik

9.11.2026: statyczny przegląd kodu `alpha.487` i treści zgłoszeń. Nie wykonano odtwarzania próbek, testu NVDA ani testu na komputerze Huberta. Wnioski o występowaniu objawów w `alpha.487` wynikają ze ścieżek kodu; sam użytkownik testował `alpha.486`.

9.11.2026 po poleceniu naprawy:

- #15: lokalny MP3 w trybie automatycznym kierowany do FFmpeg float z limiterem przed obróbką tempa; HRESULT `0x88890004` rozpoznawany jako utrata urządzenia wyjściowego z jedną próbą ponownego otwarcia, nie jako uszkodzenie dekodera. Celowane kompilacje projektu Windows i SmokeTests: 0 błędów i ostrzeżeń. `dotnet run --project tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj --no-restore -v:q -- --mp3-peaks-only`: kod 0, syntetyczny głośny MP3 stereo, ograniczenie szczytu, przewinięcie i klasyfikacja HRESULT zaliczone. Zmiany: `FfmpegLocalAudioWaveStream.cs`, `WindowsMediaOutput.cs`, `Mp3PeakPlaybackTests.cs`, jego `Program.cs`.
- #16: własny plik `file-queue-v1.json` pod katalogiem ustawień bieżącego profilu Test/Public, atomowy zapis i blokada pisarza; bez zapisu do `library.db`. Odtworzenie kolejki po restarcie, pustej kolejki, brakującego pliku i zachowanie uszkodzonego pliku przeszły w celowanym zestawie .NET `--persistence`. Celowane `test_queue_persistence_wiring.py`: 17/17. Kompilacja LiteHost/ProtocolTests przeszła. Rzeczywisty cichy proces hosta z niedostępnym katalogiem wystartował z kodem 0 i zwrócił `persistent:false` z komunikatem błędu; GUI ma wypowiedzieć go raz. Zmiany: `LiteQueueStore.cs`, `LiteQueueCoordinator.cs`, `LiteHost/Program.cs`, `host_client.py`, fragmenty `gui.py` związane z kolejką oraz dwa zestawy testów.
- Nie uruchamiano widocznego GUI, NVDA, instalatorów ani nie publikowano nowego wydania. To nie jest potwierdzenie rzeczywistego odsłuchu i wygody komunikatów NVDA.

## Znane ograniczenia

Brak nagrania audio z odsłuchu i pomiaru wyjścia po zmianie. Niepotwierdzony przebieg przy fizycznym odłączeniu słuchawek. FFmpeg jest składnikiem opcjonalnym; bez niego automatyczny MP3 wraca do dotychczasowej ścieżki i przester głośnych szczytów może pozostać. Nie przeprowadzono odbioru NVDA ani restartu całej instalacji GUI. Docelowy format magazynu kolejki w tej poprawce: osobny plik JSON wersji 1 dla każdego profilu.

## Wersja i commit

Analizowana wydana wersja: Test/Public `0.1.0-alpha.487`, commit `bf78903399254390ee5432354d45e0c30b5227d8`. Poprawki pozostają w roboczym drzewie kanonicznego repozytorium, bez osobnego commita i bez nowej wersji instalacji. W `gui.py` są też niezależne równoległe zmiany innych zadań; nie włączać ich automatycznie do tego zadania.
