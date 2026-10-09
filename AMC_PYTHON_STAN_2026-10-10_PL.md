# AMC Python — stan prac 10 października 2026

## Decyzja architektoniczna

AMC Python nie jest przepisywaniem całego programu od zera. Docelowy układ to:

- natywny, dostępny interfejs w wxPython;
- istniejący rdzeń C# AMC jako jedyny właściciel odtwarzania, nagrywania,
  timeshiftu, harmonogramów i zapisów we wspólnych bazach;
- wąski LiteHost C# łączący interfejs z istniejącymi usługami rdzenia;
- prywatny stan interfejsu wxPython wyłącznie tam, gdzie równoczesny zapis do
  profilu głównego AMC byłby niebezpieczny.

Takie rozdzielenie zachowuje sprawdzony tor multimediów i zmienia przede
wszystkim interfejs. Python nie uruchamia drugiego harmonogramu ani drugiego
silnika nagrywania.

## Stan funkcjonalny

### Radio i multimedia

Interfejs korzysta z istniejącego toru C# dla odtwarzania, nagrywania w tle,
historii nagrań, harmonogramów, timeshiftu oraz edycji nagrań. Tych mechanizmów
nie należy zastępować odpowiednikami napisanymi w Pythonie bez osobnej,
udowodnionej potrzeby. Zmiany mają dotyczyć przede wszystkim dostępności,
kompletności skrótów i zgodności zachowania z głównym AMC.

### Podcasty i YouTube

Działa przeglądanie źródeł i odcinków, odtwarzanie przez wspólny silnik,
zapisywanie postępu, kolejka, historia, pobrane materiały, import i eksport
OPML oraz obsługa źródeł YouTube. Eksport subskrypcji YouTube do przenośnego
formatu zgodnego z innymi aplikacjami pozostaje osobnym zadaniem.

### TIDAL

Odtwarzanie pozostaje zgodne z obecnym, sprawdzonym rozwiązaniem głównego AMC:
AMC przekazuje utwór, album albo widoczną listę do oficjalnej aplikacji TIDAL.
Nie udajemy własnego pełnego odtwarzacza TIDAL ani możliwości, których nie daje
obecna integracja. Pauza i wznowienie zależą od sterowania sesją systemową;
następny i poprzedni utwór mogą zależeć od oficjalnej aplikacji.

Interfejs wxPython obsługuje teraz:

- `Ctrl+Shift+L` — dodanie lub usunięcie albumu, wykonawcy albo playlisty z
  Biblioteki TIDAL;
- `Ctrl+Shift+U` — dodanie lub usunięcie utworu albo materiału wideo z
  Ulubionych TIDAL;
- `Ctrl+Shift+Q` — dodanie lub usunięcie zaznaczonych utworów z prywatnej,
  trwałej Kolejki interfejsu wxPython;
- `Ctrl+Q` — otwarcie tej Kolejki;
- Enter na utworze lub albumie — przekazanie odtwarzania do oficjalnego TIDAL.

Biblioteka i Ulubione są zmieniane przez istniejącą oficjalną integrację C#.
Kolejka TIDAL jest na tym etapie prywatna dla interfejsu wxPython i nie jest
współdzielona z kolejką głównego AMC ani oficjalnej aplikacji TIDAL. Tokeny i
prywatne uchwyty odtwarzania nie przechodzą do Pythona ani do tekstu list.

### WiiM

Sesja WiiM jest podłączona do istniejących usług AMC. Wymaga jeszcze odbioru
na obudzonym urządzeniu: wykrywanie, aktywacja, odtwarzanie, pauza, zmiana
utworu, głośność, seek, powrót fokusu i komunikaty NVDA.

### Sonos

Dodano sesję Sonos korzystającą z tego samego magazynu poświadczeń Windows i
istniejącego klienta C# co główne AMC. Python otrzymuje wyłącznie celowe nazwy
grup i bezpieczny stan odtwarzania. Dostępne są wybór grupy, odtwarzanie,
pauza, następny/poprzedni, seek i głośność. Tokeny Sonos nie opuszczają C#.

Urządzenia były uśpione, dlatego nie ma jeszcze podstaw do stwierdzenia, że
sterowanie zostało odebrane na żywym systemie. Taki odbiór jest następnym
krokiem, a nie częścią obecnego dowodu automatycznego.

### Spotify

Nowa sesja Spotify jest świadomie odłożona. Nie należy jej wdrażać w obecnym
etapie ani zużywać na nią czasu przed odbiorem istniejących sesji.

## Dostępność i NVDA

Listy i dialogi pozostają natywnymi kontrolkami wxPython. Nie dodajemy własnych
nakładek dostępnościowych bez potwierdzonej potrzeby. Każdy wiersz ma celową,
użytkową etykietę; identyfikatory, nazwy klas, rekordy i reprezentacje obiektów
nie mogą być czytane przez NVDA. Po zmianie listy należy sprawdzić pierwszy
element, ruch strzałkami, zaznaczenia Shift i Ctrl+Spacja, powrót fokusu,
ponowne otwarcie widoku oraz mowę po Escape.

## Aktualizacje programu

Moduł aktualizacji pozostaje do wdrożenia na końcu obecnego etapu. Ma działać
na wzór rozwiązania „Sygnalista” używanego w programach Michała Dziwisza,
jednak przed implementacją trzeba odzyskać właściwe repozytorium lub ticket z
Hermesa i sprawdzić licencję oraz rzeczywisty kontrakt biblioteki. Nie wolno
zgadywać nazwy pakietu ani kopiować niesprawdzonego kodu. EdSharp NG może być
punktem odniesienia dla zachowania aktualizacji, ale nie został jeszcze
pobrany do tego repozytorium.

## Dowody automatyczne tego przyrostu

- pełny runner Python: 1191 zaliczonych, 0 błędów, 138 historycznych pominięć;
- testy protokołu C#: 14 zestawów zaliczonych;
- LiteHost zbudowany i opublikowany dla Windows x64 bez błędów i ostrzeżeń;
- pakiet uruchomieniowy przeszedł sprawdzenie środowiska z Pythonem 3.14.7,
  wxPython 4.3.1 i nowym hostem.

Testy protokołu dowodzą granic danych i obsługi poleceń. Nie zastępują odbioru
żywego konta TIDAL, urządzeń WiiM/Sonos ani mowy NVDA.

## Najbliższa kolejność prac

1. Odbiór WiiM i Sonos na obudzonych urządzeniach.
2. Sprawdzenie zaniku wybranego urządzenia audio: automatyczny powrót na
   urządzenie domyślne i dalsze odtwarzanie tej samej stacji bez przełączania.
3. Uzupełnienie wykrytych braków w istniejących sesjach, bez regresji radia,
   nagrywania, harmonogramów i timeshiftu.
4. Odzyskanie źródła lub ticketu „Sygnalisty”, analiza i wdrożenie aktualizacji.
5. Dopiero później decyzja o Spotify.

