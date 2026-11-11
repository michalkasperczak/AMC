# RADIO-TRACK-001 — zmiany sygnału wokół metadanych ICY

Status: **Pomiar wykonany 11.11.2026; słyszalna granica niepotwierdzona odsłuchem**.

## Cel i metoda

Po pomiarach długości próbek sprawdzono, czy zmiana tytułu ICY pokrywa się ze zmianą w samym dźwięku. Rozszerzony, bezgłośny `RadioLiveTimingProbe` korzysta z bezpiecznego `SaveClip` w aktualnym `RadioMediaOutput`. Na każdą kolejną niepustą zmianę tytułu zapisuje ciągły WAV od 12 s przed końcem odebranego PCM przy zdarzeniu do 12 s po nim. W obu fragmentach znacznik ICY wypada więc dokładnie w 12. sekundzie pliku. Próbnik nie uruchamia Shazama, nie steruje zainstalowanym AMC Test i nie publikuje audio.

Badano przez 360 s oficjalny strumień SomaFM Indie Pop Rocks! z wcześniejszego raportu [pomiary ICY/HLS](RADIO-TRACK-001-pomiary-ICY-HLS-20261111.md). Surowy log: `work/measurements/radio-boundary-20261111-somafm.jsonl`; dwa WAV, spektrogramy i pomocnicza analiza są w sąsiednim katalogu `.jsonl.clips` oraz `work/measurements/analyze_transition.py`. `work/` jest ignorowany przez Git. Są to lokalne dane diagnostyczne, nie pliki wydania.

## Wyniki

Próbnik zakończył się z kodem 0. Wszystkie 72 odczyty dały PCM; wystąpiły dwie zmiany tytułu z kotwicą bufora. Oba fragmenty mają dokładnie 24,000 s i po 8 467 258 bajtów. Nie było błędu zapisu, utraty zakresu ani ponownego żądania rozpoznania.

| Przejście | Zdarzenie ICY, czas próbnika | Koniec odebranego PCM | Kandydat zmiany widma w WAV | Odstęp do ICY |
| --- | ---: | ---: | ---: | ---: |
| 1 | 117,043 s | 132,10 s | około 8,5–9,5 s | około 2,5–3,5 s |
| 2 | 291,533 s | 306,35 s | około 10,2–10,8 s | około 1,2–1,8 s |

Spektrogram pierwszego fragmentu pokazuje wyraźną zmianę tekstury około 8,5 s. Prosta analiza odległości pięciu pasm widma z uśrednieniem po 1,5 s z obu stron wskazała największą zmianę przy 9,5 s (wynik 0,322), podczas gdy w 12. sekundzie było 0,088. W drugim fragmencie zmiana widma jest silniejsza: maksimum przy 10,2 s (1,137), wobec 0,499 przy 12 s. Wyniki są kandydatami do odsłuchu; analiza może wskazać zmianę aranżacji wewnątrz utworu, zapowiedź lub przenikanie dwóch nagrań. Nie nazywamy ich pewnym początkiem piosenki.

## Uzasadnienie i decyzje

Oba kandydaty wypadają przed tytułem ICY, lecz w różnej odległości. Metadane nie stanowią więc dokładnej granicy audio ani podstawy do jednego stałego przesunięcia. Przy niepewnym początku i końcu utrzymano decyzję o ręcznym wskazaniu i odsłuchaniu fragmentu przed zapisem. Pomiar wzmacnia tę decyzję, lecz nie stanowi testu jakości automatycznego wycinania całej piosenki.

## Materiał do dokumentacji użytkownika

W historii rozpoznanych utworów można sprawdzić dostępny fragment z bufora i samodzielnie ustawić jego początek oraz koniec. Tytuł podany przez stację może pojawić się później niż zmiana w dźwięku, dlatego przed zapisem warto odsłuchać oba wybrane końce. Program nie obiecuje, że automatycznie znalazł cały utwór.

## Testy i wynik

- `dotnet build tests/AccessibleMediaController.Windows.SmokeTests/AccessibleMediaController.Windows.SmokeTests.csproj -c Release --no-restore --nologo -v:q`: sukces, 0 ostrzeżeń i błędów.
- Sześciominutowy próbnik na żywym ICY: kod 0, 72/72 próbek, dwa eksporty WAV, 0 błędów.
- `ffprobe`: oba WAV mają po 24,000 s. Spektrogramy utworzono przez FFmpeg; pomocniczy analizator korzysta wyłącznie ze standardowej biblioteki Python.
- Nie przeprowadzono odsłuchu przez człowieka, testu NVDA nowego okna, instalacji ani publikacji.

## Znane ograniczenia

Badano dwa przejścia jednej stacji. Dostępne narzędzia nie przekazują asystentowi dźwięku do odsłuchu, więc wskazanie słyszalnej granicy wymaga późniejszej próby odsłuchowej. Spektrogram i liczba opisują zmianę sygnału, nie tożsamość nagrania. Krótkie fragmenty mogą nie zawierać całej piosenki, a metadane innych stacji mogą mieć inne opóźnienie lub nie występować wcale.

## Wersja i commit

Zmiany dotyczą tylko próbnika w projekcie SmokeTests i niniejszego rekordu. Baza kodu: `0166930b71738d09244a327cd35fa5f55cd8b609`; commit tego pomiaru: jeszcze brak. Zbudowano projekt testowy, nie pakiet AMC. Zainstalowana aplikacja nie została zmieniona.
