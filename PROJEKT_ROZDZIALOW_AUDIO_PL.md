# Projekt rozdziałów audio opartych na Zakładkach AMC

Status: działająca implementacja od `alpha.241`, rozszerzona w `alpha.252` o
rozdziały dostawcy i ustabilizowana w `alpha.260`. Dalsze punkty tego dokumentu
wyznaczają opcjonalne etapy edycyjne. Nie jest to osobna sesja ani duży
samodzielny moduł, tylko funkcja wspólnego odtwarzacza AMC.

## Stan wdrożony w alpha.241

- zakładka i rozdział korzystają z jednego trwałego punktu czasu, który może
  pełnić jedną albo obie funkcje;
- usunięcie zakładki zachowuje współdzielony rozdział, a usunięcie własnego
  rozdziału zachowuje współdzieloną zakładkę;
- `Ctrl+Alt+Shift+B` w odtwarzaczu dodaje nazwany rozdział;
- `Ctrl+Alt+B` otwiera dostępną listę chronologiczną, również z listy materiałów;
- `Ctrl+Shift+strzałka w lewo` i `Ctrl+Shift+strzałka w prawo` przechodzą do poprzedniego albo
  następnego początku rozdziału;
- Spacja lub `Ctrl+Spacja` na liście rozdziałów przełącza pojedynczą pozycję,
  Shift ze strzałkami zaznacza zakres, Enter odtwarza tylko zaznaczone
  rozdziały, a Delete usuwa własne oznaczenia;
- przy odtwarzaniu nieprzyległych rozdziałów AMC przeskakuje pominięte odcinki,
  a po ostatnim wybranym rozdziale zatrzymuje się bez przejścia do kolejnego
  materiału;
- przycisk **Zapisz rozdział…** zapisuje jeden rozdział do nowego pliku przez
  sprawdzony mechanizm FFmpeg; oryginał pozostaje nietknięty;
- pobrany odcinek podcastu korzysta także z trwałego zaznaczenia `I`–`O`,
  nawigacji po granicach i `Shift+X`; zdalny odcinek wymaga najpierw `Ctrl+D`;
- schemat SQLite przechowuje zastosowanie punktu, pochodzenie rozdziału oraz
  przyszły identyfikator źródła. Kontrolki mają jawne etykiety dla NVDA.

Od `alpha.252` AMC odczytuje rozdziały dostawcy z Podcasting 2.0 JSON,
Podlove Simple Chapters, uporządkowanych znaczników czasu w opisie oraz ID3 i
MP4 przez zweryfikowany FFprobe. Nie wdrożono jeszcze importu CUE, korekty
granic o 0,1/0,5 sekundy, wykrywania ciszy, eksportu CUE/AMC, natywnego zapisu
metadanych i listy montażowej.

## 1. Jedna oś czasu, dwa zastosowania

Rozdziały nie tworzą drugiego, konkurencyjnego systemu punktów czasu. Ich
podstawą są trwałe Zakładki AMC. Istniejące `Ctrl+Shift+B` nadal dodaje nazwaną
zakładkę. W przyszłym **Edytorze rozdziałów** nazwana zakładka może zostać
oznaczona jako początek rozdziału, a w aktywnym trybie tworzenia rozdziałów to
samo pole ma etykietę „Nazwa rozdziału”.

Nie każda dawna zakładka nazwana staje się automatycznie rozdziałem. Rekord
zachowuje stabilną tożsamość, pozycję, nazwę i materiał, a dodatkowe oznaczenie
określa jego zastosowanie: zwykła zakładka, początek rozdziału albo oba. Dzięki
temu włączenie funkcji nie zmieni znaczenia istniejących danych ani nawigacji
`Shift+Page Up` i `Shift+Page Down`.

## 2. Granice i kolejność

Początkiem rozdziału jest jego oznaczona zakładka. Końcem jest początek
następnego rozdziału albo fizyczny koniec materiału. Rozdziały źródłowego pliku
są zawsze przedstawiane według czasu; ręczne przesuwanie ich w górę i w dół nie
może fałszować osi czasu.

Osobna **lista montażowa** może natomiast układać rozdziały w dowolnej kolejności
przed utworzeniem nowego pliku. Taka kolejność nie zmienia zakładek, materiału
źródłowego ani jego rozdziałów.

## 3. Dostępny Edytor rozdziałów

Podstawową kontrolką będzie zwykła dostępna lista. Każdy wiersz poda kolejno
nazwę, początek i obliczony czas rozdziału. Interfejs ma pozwalać co najmniej na:

- utworzenie rozdziału z bieżącej pozycji albo z istniejącej zakładki nazwanej;
- zmianę nazwy i czasu początku;
- odsłuch kilku sekund przed granicą i po niej;
- precyzyjne przesuwanie granicy o 0,5 sekundy, a w trybie dokładnym o 0,1 sekundy;
- przejście do poprzedniego i następnego rozdziału;
- usunięcie oznaczenia rozdziału bez obowiązkowego usuwania zwykłej zakładki;
- zaznaczenie rozdziałów przeznaczonych do eksportu albo listy montażowej.

Widok nie może wymagać obsługi graficznej fali dźwiękowej. Ewentualna fala jest
tylko dodatkiem wizualnym; pełna obsługa czasu, podglądu i zaznaczenia pozostaje
dostępna z klawiatury i przez UI Automation.

## 4. Wykrywanie ciszy

FFmpeg może analizować materiał i proponować kandydatów granic przez
`silencedetect`. Użytkownik wybiera próg głośności i minimalny czas ciszy albo
korzysta z bezpiecznych wartości domyślnych. Wyniki są wyłącznie propozycjami:
nie tworzą rozdziałów, nie przesuwają zakładek i nie tną pliku bez jawnego
zatwierdzenia.

Po zaakceptowaniu kandydat może zostać skorygowany małymi krokami podczas
odsłuchu. Analiza działa poza wątkiem interfejsu, ma postęp i anulowanie oraz nie
powoduje pobierania pliku chmurowego bez świadomej decyzji.

## 5. Eksport

Funkcja przewiduje trzy oddzielne działania:

1. zapis samych rozdziałów jako metadanych albo pliku towarzyszącego;
2. zapis każdego rozdziału jako osobnego pliku;
3. utworzenie nowego materiału z listy montażowej.

MP3 może otrzymać rozdziały ID3 CHAP/CTOC, a M4A/MP4 własne atomy rozdziałów,
jeżeli zastosowana biblioteka zapisze je bezpiecznie. Dla formatów bez pewnej,
zgodnej obsługi podstawowym wyjściem pozostaje CUE oraz wersjonowany format AMC.
Eksport zakładek i rozdziałów korzysta z tej samej bezpiecznej tożsamości pliku
opisanej w `PROJEKT_IMPORTU_EKSPORTU_ZAKLADEK.md`.

Osobne pliki mogą używać kopiowania oryginalnego strumienia z granicami
dopasowanymi do ramek albo dokładnego zapisu WAV/FLAC. Ponowne kodowanie musi być
jawne. Lista montażowa zawsze tworzy nowy plik przez plik tymczasowy i atomową
publikację; nie podmienia materiału źródłowego.

## 6. Powiązanie z Odtwarzaczem i Podcastami

Rozdziały są funkcją wspólnego odtwarzacza, dlatego mogą działać dla trwałych
plików lokalnych, nagrań oraz pobranych odcinków bez budowania drugiego
odtwarzacza. Sesja Podcasty dziedziczy po nim pozycję, prędkość, Zakładki,
nawigację i obsługę rozdziałów.

W sesji Podcasty rozdziały należą do konkretnego odcinka. Mogą pochodzić z
Podcasting 2.0 JSON Chapters, metadanych ID3/MP4 dostawcy, importu CUE/AMC albo
nazwanych zakładek użytkownika.
Użytkownik może więc dodać własne rozdziały także do istniejącego podcastu.
Rozdziały dostawcy i użytkownika muszą pozostać rozróżnialne, aby odświeżenie
kanału nie nadpisało lokalnej pracy.

`Ctrl+Shift+B` pozostaje szybkim sposobem zapisania nazwanej zakładki podczas
słuchania. `Ctrl+Alt+Shift+B` zapisuje nazwany początek rozdziału. Jeżeli oba
punkty przypadają w tym samym miejscu, są scalane bez kopiowania czasu i bez
tworzenia drugiego, prawie identycznego rekordu.

`Ctrl+Page Up` i `Ctrl+Page Down` pozostają przełączaniem sesji, dlatego nie
mogą równocześnie nawigować po rozdziałach. W odtwarzaczu działają
`Ctrl+Shift+strzałka w lewo` i `Ctrl+Shift+strzałka w prawo`; dawne
`Ctrl+Alt+Page Up/Down` pozostaje zgodnościowym aliasem. Prawa strzałka nadal nie zmienia
znaczenia zależnie od obecności rozdziałów; lista ma stałe polecenie
`Ctrl+Alt+B`, a brak rozdziałów nie zmienia fokusu.

Od `alpha.260` skróty poprzedniego i następnego rozdziału same uruchamiają
ograniczone wykrywanie rozdziałów, jeżeli spisu nie ma jeszcze w trwałym
magazynie. Nie trzeba wcześniej otwierać `Ctrl+Alt+B`. Jeżeli bieżący rozdział
jest odtwarzany dłużej niż 3 sekundy, pierwsze polecenie poprzedniego rozdziału
wraca na jego początek; kolejne szybkie naciśnięcie przechodzi do wcześniejszego
rozdziału. Bezpośrednio po skoku jedno naciśnięcie od razu przechodzi wstecz.
Pozwala to cofać się kolejno przez cały spis bez wielokrotnego zatrzymywania na
tym samym punkcie.

Od `alpha.261` każdy wiersz listy ma dwa jawne warianty nazwy dostępnościowej:
„Zaznaczony” oraz „Niezaznaczony”, podawane przed numerem i nazwą rozdziału.
Stan musi być słyszalny również podczas nawigacji strzałkami i
`Ctrl+strzałkami`, a nie wyłącznie bezpośrednio po użyciu Spacji. Nie wolno
polegać na opcjonalnym odczycie natywnego stanu wyboru przez konkretną wersję
czy konfigurację NVDA.

Wybrany pojedynczy rozdział można zapisać jako osobny plik przyciskiem
**Zapisz rozdział…** na liście rozdziałów. `Ctrl+S` nadal oznacza zapis całego
odcinka. Źródłowa kolejność rozdziałów pozostaje czasowa. Osobna lista
odtwarzania lub montażowa będzie mogła w przyszłości wybrać i przestawić
rozdziały bez modyfikowania podcastu.

## 7. Kolejność wdrożenia

1. Wykonane: rozszerzenie modelu Zakładki o oznaczenie początku rozdziału i migracja SQLite.
2. Częściowo wykonane: dostępna lista rozdziałów, tworzenie nazwanych punktów,
   usuwanie oznaczenia, nawigacja, wybiórcze odtwarzanie i zapis jednego rozdziału.
3. Precyzyjna korekta czasu oraz propozycje wykryte na podstawie ciszy.
4. Eksport CUE i wersjonowanego pliku AMC, potem natywne rozdziały MP3/M4A.
5. Eksport osobnych plików i niedestrukcyjna lista montażowa.
6. Wykonane w `alpha.252`: udostępnienie odziedziczonej funkcji w sesji
   Podcasty oraz import rozdziałów dostawcy. Zewnętrzny JSON przez HTTPS jest pobierany
   dopiero na jawne polecenie `Ctrl+Alt+B`, bez masowego odpytywania serwerów
   podczas `F5`.

Każdy etap wymaga testów NVDA dla początkowego fokusu, nawigacji listy,
edytowania czasu, podglądu, anulowania i powrotu do odtwarzacza. W nazwach
dostępnościowych nie mogą pojawić się identyfikatory techniczne modelu.
