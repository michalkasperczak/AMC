# Odsłuch nagrania — korekta 25.11.2026

Stan: źródła zmienione w gałęzi `codex/heos-next-test-20261123`.
Poprzednia decyzja z 23.11 o starcie od początku bufora i oknie z osobnymi
przyciskami jest zastąpiona korektą użytkownika z 25.11. Zmiana nazwy
aktywnego pliku F2 działa według odbioru użytkownika.

**Doprecyzowanie użytkownika 25.11.2026:** w Ustawieniach, karta Radio,
wybiera się początek odsłuchu: „Na żywo” albo „Od początku dostępnego
fragmentu”. Domyślnie „Na żywo”. To zastępuje bezwarunkowy start na
żywo opisany niżej; pozostałe klawisze i niezależność nagrania zostają.
Wybór jest zapisywany w prywatnym profilu AMC wxPython. Host dostaje
go jako `startPosition` tylko przy uruchamianiu odsłuchu.

## Zachowanie

- Radio, Alt+R, Enter na aktywnym nagraniu: osobny odsłuch od bieżącego
  momentu. Dźwięk trafia na domyślne lokalne wyjście Windows; tor radia
  na Denonie lub innym urządzeniu i zapis nagrania działają niezależnie.
- Oś czasu pokazuje bieżącą pozycję i dostępne granice bufora. Home cofa do
  pierwszej dostępnej próbki, End wraca na żywo. Strzałki lewo/prawo przesuwają
  o 10 sekund, z Shift o 30 sekund, z Ctrl o minutę.
- Góra/dół reguluje głośność o 5 punktów, Shift+góra/dół o 1 punkt.
  Shift+przecinek/kropka zmienia tempo o stopień, Ctrl+kropka przywraca
  normalną prędkość. Na żywo tempo może pozostać normalne; przyspieszenie
  ma sens po cofnięciu odsłuchu. Nazwy wyboru tempa wyjaśniają 1,00 razy
  jako normalną prędkość.
- Ctrl+Shift+E czyta czas od początku dostępnego fragmentu, Ctrl+Shift+R
  czas do końca bufora, Ctrl+Shift+T długość dostępnego fragmentu. Enter
  na osi czasu mówi „Odsłuch nagrania”. Spacja wstrzymuje tylko odsłuch.
  F1 w oknie wymienia klawisze; Escape zamyka odsłuch i wraca na listę.
- Ctrl+R w oknie zamyka odsłuch i wysyła żądanie zatrzymania nagrania
  wskazanego jego identyfikatorem. Nie zatrzymuje innych nagrań.

Funkcja nadal jest wspólna dla Test i Public; Apple Music i HEOS pozostają
wyłącznie w Test. Menu, paleta i pomoc na liście Nagrywane pokazują wejście
Enter; pełna pomoc na tej liście wylicza również klawisze okna. Lokalne F1
i wykonanie tych klawiszy korzystają z tej samej tablicy poleceń.

## Technika i ograniczenia

Host `radio.recordingMonitor` obsługuje głośność i tempo oraz odsyła faktyczne
wartości i granice bufora. `StartRecordingMonitor` przeskakuje do końca bufora
przed podłączeniem lokalnego wyjścia. Zamknięcie okna odłącza tylko wyjście.
W formacie oryginalnego strumienia źródło dekodowanego odsłuchu może być
osobnym połączeniem z radiem, więc jego treść może się nieznacznie różnić
od zapisywanych bajtów. Początek bufora może się przesuwać, gdy jego
pojemność zostanie osiągnięta. Dźwięk z laptopa wymaga wybrania jego
głośników jako domyślnego wyjścia Windows.

## Weryfikacja

- 25.11.2026: 62/62 skupionych testów Pythona (nagrywanie i podcasty).
- 25.11.2026: kompilacja LiteHost `--no-restore`, 0 ostrzeżeń, 0 błędów.
- Test C# `--recording-monitor-only` przeszedł: po cofnięciu powrót
  na żywo mieści się w bezpiecznym opóźnieniu 0,25 s.
- Dodatkowo 51 testów profili, HEOS, Apple Music, Radio Archiwisty
  i skrótu Shift+Enter przeszło; poprawiono dwie stare asercje
  wycofanego Ctrl+Shift+Enter i atrapę listy HEOS.
- Brak testu rzeczywistego dźwięku, NVDA, instalacji i publikacji w chwili
  sporządzenia tego rekordu. Te etapy raportować oddzielnie po wykonaniu.

## Szkic instrukcji i listy zmian

Tekst dla użytkownika do korekty: „Podczas nagrywania naciśnij Alt+R i Enter,
aby usłyszeć bieżący moment nagrania na lokalnym wyjściu audio. Możesz
cofnąć odsłuch suwakiem lub strzałkami. End wraca na żywo, Escape zamyka
odsłuch bez zatrzymywania nagrania, a Ctrl+R zatrzymuje wskazane nagranie.”

Lista zmian: odsłuch nagrania otwiera się na żywo, ma zwykłą oś czasu,
klawisze transportu, głośności, tempa i informacji o buforze. Ctrl+R
zatrzymuje tylko odsłuchiwane nagranie. Nie publikować szkicu instrukcji
automatycznie bez odbioru użytkownika.

Wersja i commit: do uzupełnienia po commicie i ewentualnym wydaniu.
