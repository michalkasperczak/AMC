# AMC 0.1.0-alpha.414 — odtwarzacz i sterowanie Sonosem

## Nowa sesja Sonos

- Osobna lista grup i odtwarzacz wybranej grupy. Wybranie grupy nie uruchamia samoczynnie muzyki, a wyjście z jej odtwarzacza nie zatrzymuje urządzenia.
- Wybór domu oraz odświeżanie grup z menu Plik i palety poleceń. Zmiana konta albo zniknięcie grupy usuwa nieaktualne dane; program nie wybiera po cichu innego celu.
- Bieżący materiał, stan, czas, głośność i wyciszenie pochodzą z odczytu Sonosa, nie z lokalnej sesji demonstracyjnej. Brak danych jest nazywany brakiem informacji, nie zerem.
- Odtwarzanie i pauza, poprzedni i następny utwór, regulacja głośności oraz wyciszenie działają przez aktywną grupę. Polecenia niedostępne dla danego materiału są odmawiane.

## Przewijanie i klawiatura

- Strzałki lewo/prawo przewijają o 10 sekund, z Shiftem o 30 sekund, z Ctrl o 60 sekund, a z Ctrl+Alt o czas ustawiony w programie.
- Cyfry 0–9 wybierają odpowiednio 0–90 procent materiału. Ctrl+J otwiera skok do czasu, a Ctrl+Shift+J skok do procentu.
- PageUp i PageDown wybierają poprzedni lub następny materiał, góra/dół zmieniają głośność o 5 punktów, Shift+góra/dół o 1 punkt, a Ctrl+M przełącza wyciszenie grupy.
- Dialogi oddają fokus właściwej kontrolce. Odpowiedź na porzucone polecenie nie przejmuje nowego widoku i nie odzywa się po przełączeniu sesji.
- Komunikaty odróżniają brak próby wysłania od próby bez potwierdzenia wyniku. Program nie ponawia polecenia automatycznie po błędzie odczytu.

## Instalator

Usunięto zbędne pobieranie i instalowanie osobnego .NET. Paczka jest budowana jako samowystarczalna i zawiera środowisko uruchomieniowe.

## Zakres tego przyrostu

To wydanie odtwarzacza i sterowania grupą. Ulubione oraz presety Sonosa pozostają kolejnym etapem; nie dodano ładowania dowolnych adresów utworów, korektora ani odpowiedników funkcji, których Sonos nie zgłasza.

Odebrany kod przeszedł testy Core i Windows oraz niezależne sprawdzenie klawiaturą i żywym NVDA na syntetycznym zapleczu. Te próby nie są pomiarem dźwięku ani odpowiedzi rzeczywistego głośnika. Wyniki końcowej budowy, instalacji i prób na sprzęcie są odnotowywane oddzielnie po ich wykonaniu.
