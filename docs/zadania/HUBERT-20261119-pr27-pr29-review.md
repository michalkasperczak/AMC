# Przygotowawczy przegląd poprawek Huberta #27 i #29

Stan 19.11.2026: przegląd statyczny zakończony; najpierw realizowany jest #25.
Nie scalano PR, nie zmieniano ich publicznego stanu, nie uruchamiano GUI,
audio, kont ani testów tych poprawek. Brak checków GitHub nie oznacza zielonego CI.

## Sprawdzone kandydaty

- PR #27: `d46e4c85a380a9997b8a671e1099b00ab40b2462`.
- PR #29: `aedb5f0649226bebef5376677d43bb8731d2ca89`.
- Rodzic obu: `1723786c4557ef68bb3922189a028099ce96db31`.
- Oba nadal OPEN, mergedAt=null. Odczyt przy porównaniu z aktualnym main
  `546f21f4b60973eed22fe5fcb29520dd6da0fcda`.

## Rozdziały #26 / PR #27 — poprawić przed bezpośrednim przyjęciem

Przegląd wskazał, a rodzic niezależnie potwierdził w oryginalnym diffie:

1. `YouTubeSourceResolver.ReadChaptersAsync` zwraca pustą listę przy braku
   narzędzia, błędzie uruchomienia procesu i niepoprawnym JSON. Koordynator
   zapamiętuje pusty wynik w `_emptyPageChecks`. Awaria nie powinna więc być
   utożsamiona z poprawną odpowiedzią „materiał nie ma rozdziałów”, bo następna
   próba w tej sesji zostanie pominięta. Prawidłowe NA/[] może pozostać cache'owane.
2. `ReadToEndAsync` akumuluje stdout i stderr przed sprawdzeniem wielkości.
   Limit parsera nie ogranicza zużycia pamięci podczas czytania. Czytać z limitem
   i właściwym zakończeniem własnego procesu po przekroczeniu; drenaż stderr
   także musi mieć bezpieczny limit.

Wymagane proporcjonalne testy: brak narzędzia i późniejsza skuteczna próba,
exit nonzero, uszkodzony JSON, timeout/cancel bez cache, NA/[] z cache,
przekroczenie limitu przez prawdziwy proces-fixture bez zawieszenia. Zachować
selekcję pojedynczych URL YouTube, brak odczytu całego kanału i istniejący tor
zwykłego podcastu. Zbudować rzeczywisty consumer LiteHost, nie tylko test parsera.

To zastrzeżenia statyczne, nie wykonane jeszcze RED. Naprawę wykonywać pionowo
RED/GREEN, nie dopisywać fikcyjnego zaliczenia.

## Wznowienie #28 / PR #29 — sprawdzić kontrakt tożsamości

PR pomija seek przy Paused, pozycji w tolerancji 250 ms i pauzie sieciowej do
2 minut. Recenzent wskazał, a rodzic odczytał wybór pipeline wyłącznie po Source
oraz init-only `PlaybackPipeline.Item`. Dwa różne itemy z tym samym adresem
mogą zostać uznane za zachowany pipeline, mimo zmiany `_requestedItem`.

**Korekta klasyfikacji rodzica:** samo dopasowanie po Source istniało już PRZED
#29. Nie przedstawiać go jako dowiedzionej nowej regresji tego PR. Przed zmianą
napisać funkcjonalny test A/B i określić wymaganą stabilną tożsamość dla in-place.
Nie rozszerzać naprawy w przebudowę całej fabryki odtwarzania bez reprodukcji.

Wymagane testy: ten sam oraz różny item o tym samym Source; 249/250/251 ms;
Paused=false; granica 2 minut ze sterowanym czasem; Stop/Seek/end oraz zmiana
Source/urządzenia/tempa podczas pauzy. Status musi opisywać prawdziwy pipeline.
Próba odsłuchowa 2x jest oddzielną bramką, nie wynika z helper-testów.

## Dalsze wykonanie

Nie wdrażać tych PR przed ukończeniem etapu #25. Nie scalać całej starej
chronionej gałęzi wydania. Przyjęte poprawki przenieść wąsko do rzeczywistej
bazy integracji z testami i osobnymi commitami. Nie zamykać zgłoszeń przed
właściwym dostarczeniem. Szczegółowy surowy raport recenzenta:
`/home/michal/.hermes/profiles/amc/cache/scratch/hubert27-29-review-20261119.md`.
