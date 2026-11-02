# A11Y-20261030 — odczyt paska stanu przez NVDA+End

Status: W trakcie — poprawki źródeł, testy i kompilacja; bez instalacji i ręcznego odbioru NVDA.

## Zachowanie widoczne dla użytkownika

- `NVDA+End` ma odczytywać natywny pasek stanu zarówno na liście, jak i w odtwarzaczu.
- W Radiu pasek podaje stację, dostępne parametry audio i nazwę audycji. W plikach, Podcastach i Radio Archiwiście podaje nazwę materiału oraz parametry audio, które rzeczywiście zna host. Bitrate lokalnego pliku może być oszacowany z jego rozmiaru i czasu trwania; bitrate strumienia sieciowego nie jest zgadywany.
- W sesjach urządzeń WiiM, Sonos i Frontier Smart pasek otrzymuje użytkową etykietę bieżącego odtwarzania z migawki urządzenia. W TIDAL-u pokazuje znany tytuł z zewnętrznego odtwarzacza.
- Gest NVDA przechodzi przez listę, odtwarzacz i pole filtra bez wykonania zwykłego `End` przez AMC.

## Uzasadnienie i decyzje

- 30.10.2026: zgłoszenie użytkownika zastępuje wcześniejsze założenie, że samo utworzenie natywnego paska stanu wystarcza. Host wysyłał gotowy tekst tylko dla Radia, więc inne sesje zostawiały stary komunikat. Wspólny status hosta ma teraz osobne pole z użytkowym opisem bieżącego materiału.
- Pasek przechowuje tekst do odczytu na żądanie i nie emituje co sekundę zdarzenia mowy.

## Testy i wynik

- Python 3.14, celowane testy bez GUI: 46 zaliczonych (`test_current_radio_broadcast.py`, `test_radioarchiwista_followup.py`, `test_file_menu_keys_parity.py`).
- Powiązany przegląd Radio Archiwisty: 100 zaliczonych, bez widocznego interfejsu.
- LiteHost Debug zbudowany przez `dotnet build --no-restore`: 0 błędów, 0 ostrzeżeń. Wynik kompilacji nie jest instalacją.
- Pierwsza próba pytest napotkała brak dostępu do systemowego katalogu tymczasowego; po ustawieniu katalogu roboczego testy przeszły. W szerszym zestawie pozostaje niezwiązana rozbieżność opisu `Ctrl+N` Podcastów w równolegle zmienianym kodzie.

## Znane ograniczenia

- Potrzebny ręczny odbiór NVDA na rzeczywistym oknie: lista i odtwarzacz każdego używanego źródła, także po zmianie sesji. Nie uruchamiano widocznych testów ani nie instalowano nowej wersji.
- Źródło sieciowe może nie podawać bitrate'u. Pasek wypowiada tylko parametry potwierdzone przez host lub urządzenie.

## Materiał do dokumentacji użytkownika

> Naciśnij `NVDA+End`, aby ponownie usłyszeć pasek stanu. W odtwarzaczu i na liście usłyszysz bieżący materiał oraz dostępne parametry dźwięku. Jeśli serwer nie udostępnia bitrate'u, AMC go nie dopowiada.

## Wersja i commit

- Kanoniczna gałąź `codex/konfigurowalne-skroty-20261026`, stan roboczy po commicie `89a0dcbf178f16f5c1624a1476134c8404dfeb95`. Brak osobnego commitu, instalacji i publikacji tego przyrostu.

## Aktualizacja 01.11.2026

Użytkownik zgłosił, że `NVDA+End` w odtwarzaczu ponownie wypowiada „Gotowe”, także przy przesyłaniu Spotify. Ta obserwacja zastępuje wcześniejsze założenie, że obecne źródła rozwiązują odbiór użytkownika. Po potwierdzonym starcie wywoływane jest odświeżenie statusu; przy wejściu do odtwarzacza przed pierwszą migawką hosta pasek dostaje znany tytuł. Testy źródeł nie stanowią ręcznego potwierdzenia w zainstalowanej wersji. Odbiór NVDA i instalacja pozostają otwarte; powiązane zadanie `SPOTIFY-20261101`.
