# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-050`
- Tytuł zestawu: Zbiorczy test odtwarzacza, parametrów audio i informacji
- Wersja programu: `0.1.0-alpha.50`
- Utworzono: 2026-08-18 00:15, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-18_0015_0.1.0-alpha.50.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-050-01 — Regulacja prędkości i wysokość dźwięku

1. Otwórz lokalny plik z wyraźną mową albo dobrze znaną muzyką i przejdź do odtwarzacza.
2. Naciśnij kilka razy `Shift+.`.
3. Naciśnij kilka razy `Shift+,`.
4. Podczas zmian wstrzymuj i wznawiaj Spacją.

Oczekiwane:

- wartości zmieniają się kolejno co 0,25, np. 1,25, 1,50 i 1,75 razy;
- NVDA podaje jedną, zrozumiałą wartość po każdym poleceniu;
- tempo realnie się zmienia, lecz głos ani muzyka nie stają się wyższe lub niższe;
- nie słychać trzasków, nakładania się dwóch torów ani trwałego zniekształcenia.

## AMC-050-02 — Granice, reset i zmiana utworu

1. Zejdź `Shift+,` do najniższej wartości i spróbuj zwolnić jeszcze raz.
2. Wejdź `Shift+.` do najwyższej wartości i spróbuj przyspieszyć jeszcze raz.
3. Ustaw 1,50 razy, wróć Escape do listy i uruchom inny lokalny plik.
4. W odtwarzaczu naciśnij `Ctrl+.`.

Oczekiwane:

- zakres nie wychodzi poniżej 0,50 ani powyżej 2,00 razy;
- drugi plik zachowuje prędkość lokalnej sesji;
- `Ctrl+.` mówi „Prędkość normalna” i rzeczywiście przywraca 1,00 razy;
- przewijanie, czas i zakończenie pliku nadal odnoszą się do prawidłowej pozycji materiału.

## AMC-050-03 — Menu, przyciski i paleta poleceń

1. Tabulatorem znajdź w odtwarzaczu przyciski Wolniej, Szybciej i Prędkość normalna.
2. Wykonaj te same operacje z menu Odtwarzanie.
3. Otwórz `Ctrl+Shift+K`, wyszukaj słowo „prędkość” i wykonaj każde z trzech poleceń.
4. Wybierz demonstracyjną sesję TIDAL lub Apple Music i spróbuj polecenia prędkości z palety.

Oczekiwane:

- przyciski i pozycje menu mają czytelne nazwy oraz podają skróty;
- paleta pokazuje trzy polecenia i właściwe skróty odtwarzacza;
- wszystkie trzy drogi sterują tą samą wartością;
- sesja bez działającego toru audio mówi, że regulacja prędkości jest niedostępna, zamiast pozornie zmieniać stan.

## AMC-050-04 — Formaty lokalne i współpraca z NVDA

1. Jeśli masz pod ręką odpowiednie pliki, odtwórz co najmniej MP3 i WAV lub FLAC.
2. Opcjonalnie sprawdź M4A/AAC, OGG/Opus albo WMA.
3. W każdym obsługiwanym pliku zmień prędkość, głośność i pozycję.
4. Podczas odtwarzania wykonuj zwykłą nawigację NVDA i pozwól mu mówić dłuższy tekst.

Oczekiwane:

- formaty działające wcześniej nadal się otwierają; ewentualny konkretny wyjątek zapisz z rozszerzeniem pliku;
- NVDA pozostaje słyszalne równocześnie z AMC;
- aplikacja nie przejmuje wyjścia dźwięku na wyłączność;
- brak pliku kodeka lub nieobsługiwany format daje zrozumiały błąd, a nie zawieszenie programu.

## AMC-050-05 — Pasek stanu i regresja odtwarzacza

1. Ustaw prędkość inną niż normalna i naciśnij `NVDA+End`.
2. Wykonaj polecenie „Odczytaj stan odtwarzania” z menu i palety.
3. Sprawdź `Ctrl+J`, `Ctrl+Shift+J`, cyfry, strzałki, głośność, Escape i ponowne `F6`.
4. Przywróć 1,00 razy i ponownie sprawdź pasek.

Oczekiwane:

- przy prędkości innej niż 1,00 pasek i jawny stan podają prędkość po stanie odtwarzania;
- przy normalnej prędkości krótki pasek nie jest niepotrzebnie wydłużony;
- pasek nadal pomija głośność, a jawne polecenie ją podaje;
- nie pojawia się techniczny komunikat „Stan programu”, fokus nie ginie, a dotychczasowe skróty nadal działają.

## AMC-050-06 — Bitrate i częstotliwość próbkowania

1. Otwórz lokalny plik i rozpocznij odtwarzanie.
2. Naciśnij `NVDA+End`.
3. Jeśli masz pliki 44,1 kHz i 48 kHz, uruchom kolejno oba i ponownie odczytaj pasek.
4. Wstrzymaj i wznów odtwarzanie, po czym jeszcze raz użyj `NVDA+End`.

Oczekiwane:

- komunikat zaczyna się bezpośrednio od wartości, np. „około 192 kb/s, 48 kHz”;
- nie występuje słowo „przepływność”;
- 44 100 Hz jest czytane jako „44,1 kHz”, a 48 000 Hz jako „48 kHz”;
- po parametrach są stan, czas, tytuł i usługa, bez głośności i bez podwójnego odczytu;
- pasek nie przejmuje fokusu ani klawiatury.

## AMC-050-07 — Dwa poziomy informacji

1. Na głównej liście zaznacz inny element niż aktualnie odtwarzany i naciśnij `Ctrl+I`.
2. Zamknij okno informacji, otwórz odtwarzacz `F6` i ponownie naciśnij `Ctrl+I`.
3. Podczas odtwarzania naciśnij `Ctrl+Shift+I` na liście oraz w odtwarzaczu.
4. Ustaw prędkość inną niż 1,00, wstrzymaj odtwarzanie i powtórz `Ctrl+Shift+I`.

Oczekiwane:

- `Ctrl+I` podaje dane zaznaczonego albo bieżącego elementu i otwiera czytelne okno informacji;
- `Ctrl+Shift+I` mówi parametry audio, stan, zmienioną prędkość, czas, głośność, tytuł i usługę;
- jawny stan działa także przy wyłączonych automatycznych komunikatach;
- menu pokazuje nowe skróty, fokus się nie przesuwa i nie pojawia się „Stan programu”.

## AMC-050-08 — Informacje po prefiksie i krótka regresja

1. Przy aktywnym wbudowanym profilu „Domyślny” naciśnij prefiks, potem `I`.
2. Naciśnij prefiks, potem `Shift+I`.
3. W palecie `Ctrl+Shift+K` wyszukaj „informacje” i „stan odtwarzania”.
4. Sprawdź filtr `Ctrl+K`, oba wyszukiwania, Escape, `F6` i zakończenie głównego okna przez `Alt+F4`.

Oczekiwane:

- prefiks `I` otwiera informacje o elemencie, a prefiks `Shift+I` odczytuje pełny stan;
- paleta pokazuje pary `Ctrl+I` / prefiks `I` oraz `Ctrl+Shift+I` / prefiks `Shift+I`;
- „Rozszerzone informacje o elemencie” pozostają w palecie bez stałego skrótu;
- nowe skróty nie powodują regresji fokusu, list, wyszukiwania ani zamykania okien.
