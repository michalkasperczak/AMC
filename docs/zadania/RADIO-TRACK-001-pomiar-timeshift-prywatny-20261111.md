# RADIO-TRACK-001 — pomiar Time Shift i prywatnego nagrania

Data: 11.11.2026. Kod sond: commit `f1a07e14`. Wyniki liczbowe są także w [rekordzie zadania](RADIO-TRACK-001-zapis-rozpoznanego-utworu.md).

## Cel i decyzja

Sondy sprawdziły, czy próbka rozpoznawania jest powiązana z właściwą osią PCM podczas przesunięcia czasu i prywatnego nagrania. Nie wyznaczały słyszalnego początku ani końca piosenki. Po odpowiedzi Shazam 429 kolejne zapytania sondy wyłączono; zwykłe rozpoznawanie w AMC nie zmieniło się.

## Metoda i wynik

`RadioLiveTimingProbe` pobierał próbki z ICY, zapisywał pozycje odbioru i odsłuchu, cofnął kursor o 20 sekund i skoczył do transmisji na żywo. Różnica osi wyniosła 20 sekund po cofnięciu i około 0,25 sekundy po powrocie. `RadioPrivateRecordingProbe` nagrał krótki WAV w odrębnym, bezgłośnym torze i potwierdził, że `RadioRecordingControl` zwraca tę samą kotwicę `Capture` co bezpośredni odczyt tego toru. Raport i audio pozostają lokalnie w `work/measurements/private-recording-20261111/` jako dane diagnostyczne.

SmokeTests Release zbudowano dwukrotnie bez błędów i ostrzeżeń. `--radio-sample-anchors-only` oraz `--radio-stream-title-only` zakończyły się kodem 0. Nie uruchamiano testu NVDA ani nie instalowano aplikacji.

## Ograniczenia

Przejścia nie odsłuchano w samym dźwięku. Zdarzenia metadanych są tylko kandydatami na granice, a rozpoznanie obejmujące przejście może wskazać nie ten utwór, który użytkownik chce zapisać. Nie zmierzono kosztu eksportu z pełnego bufora ani działania nowego okna na rzeczywistym urządzeniu audio. Wersja zainstalowana i opublikowane wydanie pozostały bez zmian.
