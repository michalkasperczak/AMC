# A11Y-20261128 — TIDAL: bitrate w NVDA+End i komunikat Spacji

Zgłoszenie 28.11.2026. Użytkownik słyszy bitrate w pasku NVDA+End nieregularnie; po Spacji słyszał pełny opis źródła AAC, przepływności, PCM i tytułu. Ta obserwacja zastępuje wcześniejsze założenie z A11Y-20261030 i A11Y-20261110, że samo istnienie natywnego paska oraz jednorazowy prawidłowy odczyt dowodzą niezawodności.

## Przyczyna i poprawka źródeł

Proxy TIDAL wylicza przeciętny bitrate z rzeczywistego rozmiaru strumienia przy pierwszym żądaniu audio i wywołuje callback tylko raz. Przed poprawką adapter wysyłał `tidal.nativeBitrate` od razu z osobnego wątku. Takie żądanie mogło dotrzeć do hosta, zanim `tidal.nativePlay` zarejestrowało ten utwór; host odpowiadał `applied=false`, a pomiar nie był ponawiany. To jest deterministyczna przyczyna utraty bitrate w tym porządku zdarzeń.

Adapter przechowuje pomiar dla konkretnej generacji i utworu. Wysyła go po potwierdzeniu `nativePlay`, o ile utwór nadal jest bieżący. Przy odrzuconym starcie, Stop i nowym utworze stary pomiar nie przechodzi do następnego. Źródło nadal podaje tylko wiarygodny bitrate z nagłówków strumienia; przy braku rozmiaru nie zgaduje wartości.

Spacja natywnego TIDAL w źródłach wywołuje tylko polecenie pauza/wznów i komunikat „TIDAL w AMC: pauza” albo „TIDAL w AMC: wznowiono”. Nie składa opisu jakości z tytułem. Oddzielny natywny pasek stanu przechowuje tekst audio dla NVDA+End bez wyzwalania mowy. Test utrwala ten kontrakt także wtedy, gdy odpowiedź transportu zawiera opis audio. To nie rozstrzyga jeszcze, czy sam NVDA wypowiada status przy zmianie fokusu lub późnej aktualizacji; wymaga prób w działającej aplikacji.

## Testy i granice

`test_tidal_native_playback.py` oraz `test_native_status_bar_for_nvda_end.py`: 32 zaliczone. Obejmują bitrate przed i po potwierdzeniu hosta, nieudany start, stary callback po zmianie utworu, osobną mowę Spacji i zachowanie paska. `test_tidal_native_host_transport.py` oraz `test_current_radio_broadcast.py`: 14 zaliczonych, 2 pominięte z powodu opcjonalnego hosta. `py_compile` i `git diff --check` przeszły. Przestarzała atrapa w istniejącym teście Spotify otrzymała brakującą metodę playback YouTube, żeby grupa statusu wykonała się w całości.

Nie było kompilacji, instalacji, publikacji ani odsłuchu NVDA. Sprawdzić z prawdziwym TIDAL: NVDA+End natychmiast po Enter oraz po sekundzie, na liście i w odtwarzaczu, AAC i FLAC, pauza/wznowienie, przejście do następnego utworu, zmiana sesji, brak Content-Length. Potwierdzić, czy Spacja mówi tylko pauzę/wznowienie; obserwacja użytkownika o pełnym statusie może pochodzić z odczytu paska w innym momencie.

Commit źródłowy: 74e4a51633e6a8012428da18d90f7ac3139bb4a6. Wersja wydania: nieprzypisana; źródła opierają się na Test alpha.533.

## Draft user guide / Szkic instrukcji

PL: W sesji TIDAL naciśnij NVDA+End, aby odczytać bieżący utwór i dostępne parametry audio. Jeśli strumień podał rozmiar, AMC może obliczyć i pokazać bitrate. Spacja pauzuje albo wznawia odtwarzanie i wypowiada tylko wynik tej czynności.

EN: In TIDAL, press NVDA+End to read the current track and available audio details. When the stream provides its size, AMC can calculate and show its bitrate. Space pauses or resumes playback and announces only that action.
