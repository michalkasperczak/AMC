# AUDIO-BITRATE-20261105 — bitrate w odtwarzaczu i radiu

Status: kod i testy celowane gotowe 5 listopada 2026; instalacja i odbiór NVDA oczekują na zakończenie nagrania.

## Korekta użytkownika

5 listopada 2026 użytkownik usłyszał w odtwarzaczu TIDAL „bez ciebie, 48 kHz” bez przepływności i zgłosił podobny brak w radiu. Ten raport zastępuje wcześniejsze założenie, że sam odczyt częstotliwości dowodzi pełnego opisu parametrów audio. Wartość 48 kHz oznacza częstotliwość próbkowania; nie wolno jej podawać jako bitrate.

## Zachowanie i miejsca

- TIDAL w AMC: po uruchomieniu utworu status odtwarzania używany przez NVDA+End podaje tytuł, dostępny bitrate w kb/s i częstotliwość w kHz. Dla zwykłego pliku FLAC proxy oblicza średni bitrate z całkowitej liczby bajtów i czasu utworu przy pierwszym normalnym żądaniu dźwięku. Nie wykonuje dodatkowego żądania przed startem. Dla Max/DASH bierze deklarowaną przepływność reprezentacji z manifestu, jeśli istnieje.
- Radio: gdy manifest HLS podaje kodek, ale nie podaje przepływności, pomiar nie kończy się na samym kodeku. Program może oszacować bitrate z rozmiaru i czasu pierwszego segmentu audio. Status radia i Alt+D używają tego samego formatu. Brak wiarygodnych danych nadal pozostaje brakiem danych, bez zmyślonej liczby.
- Odtwarzacz TIDAL zachowuje kompletny status hosta po odświeżeniu widoku; ponowne narysowanie nie zastępuje go samym tytułem.
- Nie ma nowych skrótów ani poleceń. Pomoc, menu i paleta zachowują istniejące nazwy odczytu bieżącego odtwarzania.

## Dlaczego

Wewnętrzny tor TIDAL znał częstotliwość z dekodera, lecz nie przekazywał do hosta rozmiaru skompresowanego źródła. Dodatkowo widok TIDAL mógł zastąpić gotowy status samym tytułem. W radiu kod przerywał pomiar HLS po znalezieniu samego kodeka. Poprawki dotyczą danych wejściowych i zachowania gotowego tekstu, a nie odgadywania kb/s z 48 kHz.

## Weryfikacja i ograniczenia

- Python: 59 testów celowanych i 6 podtestów: proxy BTS, Max/DASH, adapter, status NVDA i radio.
- C#: test protokołu TIDAL 1/1; test radia HLS z lokalnym serwerem przeszedł; sam host skompilowany bez ostrzeżeń i błędów.
- Rzeczywiste konto TIDAL, stacje użytkownika i NVDA wymagają odsłuchu po instalacji. Przy strumieniu bez całkowitego rozmiaru, deklaracji przepływności ani mierzalnego segmentu liczba nie zostanie wypowiedziana. Wartości z manifestu i rozmiaru są średnie lub deklarowane, nie chwilową przepływnością każdej sekundy.

## Wersja i commit

Wspólny kandydat AMC Test 0.1.0-alpha.480. Commit kodu i testów: 9a24c83c. Instalacja: niewykonana. Publikacja: niewykonana.
