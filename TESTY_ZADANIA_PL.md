# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-057`
- Tytuł zestawu: Trwała biblioteka lokalna, wznowienia i kontekst okna
- Wersja programu: `0.1.0-alpha.57`
- Utworzono: 2026-08-19 15:47, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-19_1547_0.1.0-alpha.57.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-057-01 — Tytuł okna: moduł, element, sesja

1. Otwórz Lokalne multimedia i przejdź kolejno do Biblioteki, Ulubionych, Kolejki i odtwarzacza.
2. W każdym miejscu odczytaj tytuł głównego okna.
3. Przesuń fokus na inny plik bez rozpoczynania odtwarzania i ponownie odczytaj tytuł.

Oczekiwane:

- kolejność to „moduł, aktualnie odtwarzany lub wstrzymany element, sesja, AMC i wersja”;
- przykładowo: „Kolejka — nazwa pliku — Lokalne multimedia — AMC 0.1.0-alpha.57”;
- samo przesuwanie fokusu nie zmienia elementu odtwarzania w tytule.

## AMC-057-02 — Historia bez słów Wstecz i Naprzód

1. Odznacz w Ustawieniach „Oznajmiaj kierunek historii widoków”.
2. Przejdź `Alt+lewo/prawo` między Ulubionymi, Kolejką i Biblioteką.
3. Włącz opcję ponownie i powtórz próbę.

Oczekiwane:

- przy wyłączonej opcji nie ma słów Wstecz/Naprzód, ale jest „widok, sesja, element”, np. „Kolejka, Lokalne multimedia, nazwa pliku”;
- przy włączonej opcji wraca „Wstecz/Naprzód, widok, sesja, element”.

## AMC-057-03 — Pierwsze zapisanie lokalnej biblioteki

1. Jednorazowo otwórz folder lub kilka plików przez `Ctrl+O` albo `Ctrl+Shift+O`.
2. Dodaj różne pliki do Ulubionych i Kolejki, ustaw inną głośność oraz prędkość.
3. Zamknij AMC przez `Alt+F4`, uruchom ponownie i wybierz sesję Lokalne multimedia.

Oczekiwane:

- lista plików wraca bez ponownego otwierania folderu;
- zachowane są Ulubione, Kolejka, bieżący plik, głośność i prędkość;
- program nie rozpoczyna sam odtwarzania.

## AMC-057-04 — Osobne pozycje wznowienia

1. Odtwórz dłuższy plik A i przewiń go np. do drugiej minuty.
2. Odtwórz plik B i przewiń go do innego miejsca.
3. Zamknij program, uruchom ponownie i osobno uruchom A oraz B.

Oczekiwane:

- każdy plik rozpoczyna się od własnego zapamiętanego miejsca;
- samo uruchomienie AMC nie zaczyna odtwarzania;
- pozycja zakończonego naturalnie pliku wraca do początku.

## AMC-057-05 — AAC

1. Otwórz wskazany wcześniej plik `.aac`, najlepiej `2 - 2026-08-14 13-00.aac`.
2. Uruchom go Enterem i sprawdź dźwięk, czas, przewijanie oraz zmianę prędkości.
3. Jeśli nie zagra, zapisz dokładny komunikat AMC i nazwę pliku.

Oczekiwane:

- AAC odtwarza się przez ten sam współdzielony tor co MP3;
- NVDA pozostaje słyszalne;
- czas i regulacja prędkości działają.

## AMC-057-06 — Podstawowa regresja

Sprawdź wyrywkowo listy, filtr, wyszukiwanie, paletę, `Alt+Enter`, menu kontekstowe, pasek stanu oraz następny plik po naturalnym końcu.

Oczekiwane: działają jak w `alpha.56`; pełny ręczny odczyt odtwarzacza może pozostać długi.
