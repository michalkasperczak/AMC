# RADIO-TRACK-001 — pomiar ICY i HLS z 11.11.2026

## Cel i metoda

Zmierzono zdarzenia tytułu, pozycje próbek PCM i czas odpowiedzi rozpoznawania w aktualnym torze `RadioMediaOutput` AMC. Osobny proces testowy działał bezgłośnie; zainstalowane AMC Test pozostało uruchomione. Sonda co 5 sekund pobierała maksymalnie 12 sekund PCM i zapisywała wyłącznie znaczniki, liczby bajtów, tytuły otrzymane od stacji i wynik rozpoznania. Dźwięku nie zapisano. Surowe pliki JSONL pozostają lokalnie w ignorowanym przez Git katalogu `work/measurements/`.

Badano oficjalne strumienie SomaFM Groove Salad (90 s), PopTron (300 s), Indie Pop Rocks! (240 s) oraz HLS KUTX (120 s). Źródła: [SomaFM PopTron](https://somafm.com/poptron/directstreamlinks.html), [SomaFM Indie Pop Rocks!](https://somafm.com/indiepop/directstreamlinks.html), [KUTX](https://kutx.org/streams/).

https://somafm.com/poptron/directstreamlinks.html

https://somafm.com/indiepop/directstreamlinks.html

https://kutx.org/streams/

## Wyniki

| Strumień | Czas | Próbki z PCM | Zmiany tytułu po starcie | Wynik |
| --- | ---: | ---: | ---: | --- |
| Groove Salad, ICY | 90 s | 18/18 | 0 | Tytuł początkowy; Shazam: HTTP 429. |
| PopTron, ICY | 300 s | 60/60 | 1 | Zmiana na „Clovis – Mundo” o 14:26:39,302; koniec otrzymanej próbki w bajcie 108 080 280, czyli 306,35 s osi PCM. |
| Indie Pop Rocks!, ICY | 240 s | 48/48 | 1 | Zmiana na „Apophis – On Prendra De L'avance Plus Ta” o 14:26:58,415; koniec próbki w bajcie 27 730 080, czyli 78,60 s osi PCM. |
| KUTX, HLS | 120 s | 22/24 | 0 | Odbiór PCM zaczął się po 10,997 s; dwa pierwsze odczyty były jeszcze puste. Nie dotarł żaden niepusty tytuł. Shazam: HTTP 429. |

PopTron i Indie Pop Rocks! dostarczyły metadane około **15,13 s** i **15,45 s** dalej na osi PCM niż upływ czasu od otwarcia połączenia. To efekt początkowego wyprzedzenia odbioru/buforowania w tych próbach, nie czas trwania piosenki. Nie wolno przeliczać zegara ściennego na pozycję utworu przez stałe odejmowanie 15 sekund.

Publiczne strony historii SomaFM podały początek nowych pozycji jako `05:26:34` (PopTron) i `05:26:54` (Indie Pop Rocks!). Przy założeniu, że są to godziny lokalne stacji w Kalifornii, zdarzenia ICY w AMC nastąpiły **około 5,3 s i 4,4 s później**. Strefa czasowa nie jest opisana na tych stronach; porównanie jest przybliżeniem. Nie przeprowadzono odsłuchu samego przejścia, więc nie jest to pomiar opóźnienia względem słyszalnej granicy. Strony: [historia PopTron](https://somafm.com/poptron/played), [historia Indie Pop Rocks!](https://somafm.com/indiepop/played).

https://somafm.com/poptron/played

https://somafm.com/indiepop/played

Jedna odpowiedź Shazam dla PopTron trwała **1096 ms**, lecz wskazała inny utwór niż metadane stacji. Bez niezależnego odsłuchu nie można rozstrzygnąć przyczyny tej rozbieżności. Trzy pozostałe żądania otrzymały HTTP 429 po 0,9–1,5 s. Dalszych żądań rozpoznawania nie należy wykonywać w krótkich seriach; pomiary PCM mogą trwać lokalnie bez nich.

Próby 1LIVE MP3 i HLS nie dostarczyły zakotwiczonego PCM w sondzie AMC. MP3 zgłosił limit przygotowania po 25 s; HLS przerwano w pierwotnej sondzie po 35 s bez startu. Niezależny FFmpeg zdekodował po 5 s obu źródeł, więc ta próba nie dowodzi ich niedostępności. Sonda została następnie poprawiona tak, by HLS czekał do 95 s. Surowe próby nieudane są zachowane, lecz nie weszły do powyższej tabeli wyników PCM.

## Uzasadnienie i decyzje

Obie zmiany ICY miały konkretną pozycję w PCM, ale tytuł nie był dokładnym znacznikiem słyszalnego początku. HLS KUTX dostarczał próbki bez nazwy utworu. Na tej podstawie automatyczne wycięcie całej piosenki pozostaje nieuzasadnione. Następny etap to jednoczesne wyznaczenie słyszalnej granicy i pozycji PCM na kontrolowanym przejściu, także w timeshift i prywatnym torze nagrania. Potrzebny jest również test nadpisania bufora zanim zacznie się eksport.

Pomiar ujawnił błąd parsera ICY: apostrof wewnątrz tytułu „L'avance” ucinał tekst do „L”. Parser rozpoznaje teraz apostrof zamykający pole przed średnikiem i zachowuje apostrofy w nazwie. Zmiana nie dotyczy interfejsu poleceń.

## Testy i wynik

- Bezgłośna sonda: cztery zakończone sesje jak w tabeli; bez plików audio. Kod sondy i poprawka parsera: commit `0af00681`.
- Test regresji `--radio-stream-title-only`: zaliczony. Build SmokeTests Release i LiteHost Release: 0 ostrzeżeń, 0 błędów.
- Nie wykonano odsłuchu granicy, testu NVDA, eksportu fragmentu, instalacji ani publikacji.

## Materiał do dokumentacji użytkownika

W obecnie zainstalowanym AMC nie ma polecenia zapisu całego rozpoznanego utworu. Pomiary potwierdziły, że stacje różnią się dostępnością tytułów i że tytuł może docierać z opóźnieniem. Do czasu ręcznego sprawdzenia granic nie należy przedstawiać metadanych jako pewnego początku lub końca nagrania. Po przyszłej aktualizacji tytuły ICY z apostrofem powinny być odczytywane w całości.

## Wersja i lokalizacja danych

Zmiany dotyczą źródła AMC Test; instalacja użytkownika nie była zmieniana. Pliki pomiarowe: `work/measurements/radio-timing-20261111-{soma,poptron,indiepop,kut-hls,icy,hls,wdr-hls96}.jsonl` w kanonicznym repozytorium. Zawierają publiczne adresy stacji oraz znaczniki diagnostyczne, dlatego pozostają lokalnie i nie wchodzą do commitu ani wydania.
