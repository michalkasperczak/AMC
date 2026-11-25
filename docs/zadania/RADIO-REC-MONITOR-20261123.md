# RADIO-REC-MONITOR — odsłuch trwającego nagrania i zmiana nazwy

**Korekta użytkownika 24.11.2026:** gotowy odsłuch trwającego nagrania (Alt+R, Enter) i zmiana nazwy F2 są wspólne w Test/Public alpha.522. Zastępuje to starsze ograniczenie tylko do Test. Apple Music pozostaje tylko w Test. [Wydanie i rzeczywista instalacja](RELEASE-ALPHA522-20261124-Cast-instalacja.md).

**Korekta użytkownika 25.11.2026:** Enter na liście „Nagrywane” ma
rozpoczynać odsłuch na żywo przez domyślne lokalne wyjście audio. Starszy opis
startu od najstarszej próbki i osobnych przycisków czasu zastępuje normalny
profil odtwarzacza: suwak, Home/End, strzałki, głośność, tempo, odczyt czasu
bufora oraz Ctrl+R zatrzymujące wybrane nagranie. Zmiana nazwy F2 została
potwierdzona przez użytkownika w praktyce. Szczegóły i wyniki:
`RADIO-REC-MONITOR-20261125.md`. Tekst z 23.11 poniżej pozostaje historią
poprzedniej decyzji.

Stan na 23.11.2026: implementacja źródłowa i testy celowane. Bez instalacji,
wydania i ręcznego odbioru NVDA. Bieżąca gałąź:
`codex/amc-current-d-20261123`, punkt bazowy `faf67433`. Katalog:
`D:\Projekty Codex\AMC - integracja wxPython`.

## Ustalenie z użytkownikiem

Użytkownik wskazał wcześniejszą rozmowę z niedzieli około połowy października;
dokładnej daty ani zapisu nie potwierdzono. Zgłoszenie z 23.11.2026: Enter po
`Alt+R` ma otwierać materiał, który właśnie się nagrywa, z możliwością cofania,
a nie rozpoczynać odbiór stacji na żywo. Korekta tego samego dnia: drugą ważną
funkcją jest zmiana nazwy pliku w trakcie nagrywania. Niedokończone pytanie
„czy…” użytkownik wycofał; nie stanowi wymagania. Wycinanie fragmentów zostało
wspomniane jako możliwość, bez ustalonego zakresu i polecenia; nie jest częścią
tego przyrostu.

Jeżeli później dodamy wycinanie w czasie nagrywania, powinno ono zaznaczać
początek i koniec wyłącznie w dostępnym buforze odsłuchu oraz zapisywać fragment
jako nowy plik. Nie może przycinać otwartego pliku nagrania ani przesuwać
bieżącej pozycji kodera. Po nadpisaniu początku bufora aplikacja powinna jawnie
odmówić zapisu niedostępnego zakresu. Osobny projekt wymaga decyzji o formacie
wyniku i miejscu zapisu; ten przyrost nie dodaje pozornego przycisku cięcia.

## Zachowanie i polecenia

1. W Radiu `Alt+R` otwiera listę „Nagrywane”. Enter na rozpoczętym nagraniu
   otwiera okno „Odsłuch nagrania” i osobny tor słuchania. Nie uruchamia drugiej
   transmisji na zwykłym odtwarzaczu i nie zmienia strumienia, kodera ani
   nagrywanego pliku. Odsłuch zaczyna się od najstarszego fragmentu nadal
   dostępnego w buforze. Zamknięcie okna odłącza wyłącznie odsłuch.
2. W oknie są osobne przyciski „Wstrzymaj/Wznów odsłuch”, „Cofnij o 30 sekund”,
   „Przejdź 30 sekund naprzód”, „Początek dostępnego nagrania”, „Koniec
   nagrania, na żywo”, pole czasu w formacie `MM:SS` albo `HH:MM:SS` oraz
   „Zamknij odsłuch”. Stan podaje pozycję i dostępny zakres. Po zamknięciu
   fokus wraca na listę.
3. `F2` na tym samym wierszu otwiera dialog zmiany nazwy bez rozszerzenia.
   Folder i format pozostają te same. Gdy nazwa jest wolna, AMC zmienia cel
   publikacji pliku i aktualizuje ścieżki zakładek oraz bieżący wiersz.
   Nagrywanie trwa nadal. Istniejąca nazwa albo nazwa innego aktywnego
   nagrania jest odrzucana. Gotowy plik pojawia się pod nową nazwą po
   zakończeniu zapisu. Zwykłe `F2` poza widokiem „Nagrywane” nadal edytuje
   stację lub właściwy element bieżącej sesji.
4. Funkcje są w menu Radia, kontekstowym `Shift+F10`, pomocy klawiatury,
   kontekstowej `Ctrl+F1` i palecie `Ctrl+Shift+K`. Etykiety nie ujawniają
   identyfikatora nagrania ani nazw wewnętrznych klas.
5. Przyrost należy tylko do profilu AMC Test. Public zachowuje dotychczasowy
   Enter na liście „Nagrywane”; nowe polecenia nie są rejestrowane przez jego
   hosta, pokazywane w menu, pomocy ani palecie. Test profilu porównuje
   kontrakt Pythona z zamkniętym kontraktem launchera. Public zachowuje
   dotychczasowy jednominutowy bufor prywatnego nagrywania; nie rezerwuje
   dużego bufora odsłuchu.

## Rozwiązanie techniczne i granice

Prywatny tor nagrywania otrzymał ograniczony bufor dekodowanego dźwięku
zgodnie z ustawioną długością i folderem Time Shift. W buforze odsłuchu
pomijane są próbki sprzed rozpoczęcia zapisu oraz okres pauzy kodera. Po
przekroczeniu pojemności najstarsze próbki są nadpisywane; przycisk początku
prowadzi wtedy do najstarszej dostępnej pozycji, nie do pierwotnej sekundy zero.
W formacie „oryginalny strumień” koder i tor dekodowanego odsłuchu korzystają
z oddzielnych połączeń z radiem. Przy niestabilnym lub zmiennym źródle ich
zawartość może nie być dokładnie identyczna. W formatach kodowanych z PCM
odsłuch korzysta z tego samego przechwytywania co zapis.

Windows może pozwolić na zmianę nazwy otwartego pliku tylko przy zgodnym
udostępnieniu uchwytu (`FILE_SHARE_DELETE`). Bieżący plik tymczasowy AMC jest
otwierany z `FileShare.Read`, więc nie zmieniamy jego nazwy w systemie podczas
zapisu. Zmieniamy wyłącznie docelową nazwę, pod którą ukończony i zamknięty
plik zostaje opublikowany. Dzięki temu rozwiązanie nie wymaga obchodzenia
blokady Windows ani przerywania nagrania.

## Weryfikacja

- Python: 208 celowanych testów (`test_radio_recording`, skróty, paleta, menu,
  nawigacja i widoki Radia) przeszło 23.11.2026. Po wydzieleniu funkcji do
  profilu Test przeszło 131 testów profilu, nagrywania, menu i palety.
- C#: `dotnet build` LiteHost i Windows SmokeTests w konfiguracji Release,
  bez przywracania pakietów, zakończyły się bez ostrzeżeń i błędów.
- C#: `--recording-monitor-only` przeszedł: bufor nie przejął audio sprzed
  startu ani z pauzy, odczyt nie zmienił kodera, nadpisanie bufora ogranicza
  cofanie, a MP3 po zmianie celu zapisu dał się odkodować.
- C#: test handlerów LiteHost przeszedł po zbudowaniu z lokalnego cache;
  sprawdził obecność nowych operacji w Test i brak ich rejestracji w Public.
- Nie wykonano ręcznego testu NVDA, testu prawdziwej stacji, budowy paczki,
  instalacji, uruchomienia nowej wersji ani publikacji. Działająca instalacja
  na D: pozostała bez zmian. Odbiór NVDA powinien objąć Enter, fokus początkowy,
  Tab, przyciski, zamknięcie i powrót, ponowne otwarcie, F2, istniejącą nazwę,
  pauzę nagrywania i dojście odsłuchu do końca bufora.

## Wersja i migracja

Zmiany znajdują się w niezatwierdzonym drzewie źródeł na wskazanej gałęzi;
numer wydania i commit przyrostu nie zostały nadane. Nie ma migracji danych.
Nowa nazwa jest przechowywana jako bieżąca ścieżka aktywnego nagrania i
przenoszona do historii przy zakończeniu zapisu.
