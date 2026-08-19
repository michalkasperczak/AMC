# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-056`
- Tytuł zestawu: Kolejność historii i wyłączanie jej komunikatów
- Wersja programu: `0.1.0-alpha.56`
- Utworzono: 2026-08-19 14:40, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-19_1440_0.1.0-alpha.56.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-056-01 — Kierunek, widok, sesja

1. W Lokalnych multimediach przejdź kolejno do Biblioteki, Ulubionych i Kolejki.
2. Użyj `Alt+strzałka w lewo`, a potem `Alt+strzałka w prawo`.
3. Powtórz w TIDAL lub Apple Music.

Oczekiwane:

- komunikat ma kolejność „Wstecz, nazwa widoku, nazwa sesji” albo „Naprzód, nazwa widoku, nazwa sesji”;
- przykładowe brzmienie to „Wstecz, Kolejka, Lokalne multimedia”, a nie „Wstecz, Lokalne multimedia, Kolejka”;
- historie sesji nadal nie mieszają się ze sobą.

## AMC-056-02 — Osobne wyłączenie komunikatów historii

1. Otwórz Ustawienia, kartę Komunikaty.
2. Odznacz „Oznajmiaj kierunek historii widoków”, zapisz i ponownie użyj historii.
3. Sprawdź także próbę przejścia w kierunku, w którym nie ma wcześniejszego widoku.
4. Zamknij i uruchom AMC ponownie; sprawdź, czy ustawienie zostało zapamiętane.
5. Włącz opcję ponownie.

Oczekiwane:

- `Alt+lewo/prawo` nadal zmienia widok, lecz nie dodaje słów „Wstecz” ani „Naprzód” i nie mówi komunikatu o braku historii;
- NVDA odczytuje zwykły element docelowej listy;
- ustawienie przetrwa ponowne uruchomienie;
- ponowne zaznaczenie przywraca pełny komunikat historii.

## AMC-056-03 — Ustawienie w palecie i przełącznik nadrzędny

1. Otwórz paletę poleceń `Ctrl+Shift+K` i wyszukaj „historia widoków”.
2. Sprawdź, czy pozycja podaje bieżący stan włączone/wyłączone i otwiera właściwy checkbox w Ustawieniach.
3. Wyłącz nadrzędne „Włącz komunikaty dostępności” i sprawdź historię.
4. Włącz komunikaty dostępności ponownie.

Oczekiwane:

- paleta pokazuje „Komunikaty historii widoków” wraz z rzeczywistym stanem;
- Enter przechodzi bezpośrednio do odpowiedniej opcji;
- wyłączenie wszystkich komunikatów ucisza także kierunek historii;
- ustawienie „Automatyczne komunikaty odtwarzacza” pozostaje niezależne od historii.

## AMC-056-04 — Pełny odczyt odtwarzacza bez regresji

1. Otwórz odtwarzacz i użyj polecenia NVDA odczytującego bieżący obiekt lub wiersz.
2. Sprawdź zwykłą nawigację po przyciskach i menu kontekstowe.

Oczekiwane:

- pełny odczyt może nadal zawierać tytuł, rodzaj, usługę, stan, prędkość, bieżący przycisk i instrukcję;
- tekst nie jest automatycznie powtarzany przy każdej zwykłej zmianie przycisku;
- sterowanie, właściwości i menu odtwarzacza działają jak w `alpha.55`.
