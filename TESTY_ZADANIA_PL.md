# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-049`
- Tytuł zestawu: Regulacja prędkości bez zmiany wysokości dźwięku
- Wersja programu: `0.1.0-alpha.49`
- Utworzono: 2026-08-17 23:55, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-17_2355_0.1.0-alpha.49.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-049-01 — Regulacja prędkości i wysokość dźwięku

1. Otwórz lokalny plik z wyraźną mową albo dobrze znaną muzyką i przejdź do odtwarzacza.
2. Naciśnij kilka razy `Shift+.`.
3. Naciśnij kilka razy `Shift+,`.
4. Podczas zmian wstrzymuj i wznawiaj Spacją.

Oczekiwane:

- wartości zmieniają się kolejno co 0,25, np. 1,25, 1,50 i 1,75 razy;
- NVDA podaje jedną, zrozumiałą wartość po każdym poleceniu;
- tempo realnie się zmienia, lecz głos ani muzyka nie stają się wyższe lub niższe;
- nie słychać trzasków, nakładania się dwóch torów ani trwałego zniekształcenia.

## AMC-049-02 — Granice, reset i zmiana utworu

1. Zejdź `Shift+,` do najniższej wartości i spróbuj zwolnić jeszcze raz.
2. Wejdź `Shift+.` do najwyższej wartości i spróbuj przyspieszyć jeszcze raz.
3. Ustaw 1,50 razy, wróć Escape do listy i uruchom inny lokalny plik.
4. W odtwarzaczu naciśnij `Ctrl+.`.

Oczekiwane:

- zakres nie wychodzi poniżej 0,50 ani powyżej 2,00 razy;
- drugi plik zachowuje prędkość lokalnej sesji;
- `Ctrl+.` mówi „Prędkość normalna” i rzeczywiście przywraca 1,00 razy;
- przewijanie, czas i zakończenie pliku nadal odnoszą się do prawidłowej pozycji materiału.

## AMC-049-03 — Menu, przyciski i paleta poleceń

1. Tabulatorem znajdź w odtwarzaczu przyciski Wolniej, Szybciej i Prędkość normalna.
2. Wykonaj te same operacje z menu Odtwarzanie.
3. Otwórz `Ctrl+Shift+K`, wyszukaj słowo „prędkość” i wykonaj każde z trzech poleceń.
4. Wybierz demonstracyjną sesję TIDAL lub Apple Music i spróbuj polecenia prędkości z palety.

Oczekiwane:

- przyciski i pozycje menu mają czytelne nazwy oraz podają skróty;
- paleta pokazuje trzy polecenia i właściwe skróty odtwarzacza;
- wszystkie trzy drogi sterują tą samą wartością;
- sesja bez działającego toru audio mówi, że regulacja prędkości jest niedostępna, zamiast pozornie zmieniać stan.

## AMC-049-04 — Formaty lokalne i współpraca z NVDA

1. Jeśli masz pod ręką odpowiednie pliki, odtwórz co najmniej MP3 i WAV lub FLAC.
2. Opcjonalnie sprawdź M4A/AAC, OGG/Opus albo WMA.
3. W każdym obsługiwanym pliku zmień prędkość, głośność i pozycję.
4. Podczas odtwarzania wykonuj zwykłą nawigację NVDA i pozwól mu mówić dłuższy tekst.

Oczekiwane:

- formaty działające wcześniej nadal się otwierają; ewentualny konkretny wyjątek zapisz z rozszerzeniem pliku;
- NVDA pozostaje słyszalne równocześnie z AMC;
- aplikacja nie przejmuje wyjścia dźwięku na wyłączność;
- brak pliku kodeka lub nieobsługiwany format daje zrozumiały błąd, a nie zawieszenie programu.

## AMC-049-05 — Pasek stanu i regresja odtwarzacza

1. Ustaw prędkość inną niż normalna i naciśnij `NVDA+End`.
2. Wykonaj polecenie „Odczytaj stan odtwarzania” z menu i palety.
3. Sprawdź `Ctrl+J`, `Ctrl+Shift+J`, cyfry, strzałki, głośność, Escape i ponowne `F6`.
4. Przywróć 1,00 razy i ponownie sprawdź pasek.

Oczekiwane:

- przy prędkości innej niż 1,00 pasek i jawny stan podają prędkość po stanie odtwarzania;
- przy normalnej prędkości krótki pasek nie jest niepotrzebnie wydłużony;
- pasek nadal pomija głośność, a jawne polecenie ją podaje;
- nie pojawia się techniczny komunikat „Stan programu”, fokus nie ginie, a dotychczasowe skróty nadal działają.
