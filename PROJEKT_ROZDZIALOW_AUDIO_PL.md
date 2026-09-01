# Projekt rozdziałów audio opartych na Zakładkach AMC

Status: zatwierdzony kierunek niewielkiej, przyszłej funkcji wspólnego
odtwarzacza AMC. Nie jest to osobna sesja ani duży samodzielny moduł. Dokument
opisuje model i interfejs; nie oznacza jeszcze gotowej implementacji ani
rezerwacji nowych skrótów klawiszowych.

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
metadanych dostawcy, importu CUE/AMC albo nazwanych zakładek użytkownika.
Użytkownik może więc dodać własne rozdziały także do istniejącego podcastu.
Rozdziały dostawcy i użytkownika muszą pozostać rozróżnialne, aby odświeżenie
kanału nie nadpisało lokalnej pracy.

`Ctrl+Shift+B` pozostaje szybkim sposobem zapisania nazwanego punktu podczas
słuchania. Przekształcenie go w rozdział odbywa się bez kopiowania czasu i bez
tworzenia drugiego, prawie identycznego rekordu.

## 7. Kolejność wdrożenia

1. Rozszerzenie modelu Zakładki o oznaczenie początku rozdziału i migracja SQLite.
2. Dostępna lista rozdziałów, tworzenie z nazwanej zakładki, zmiana nazwy i podgląd.
3. Precyzyjna korekta czasu oraz propozycje wykryte na podstawie ciszy.
4. Eksport CUE i wersjonowanego pliku AMC, potem natywne rozdziały MP3/M4A.
5. Eksport osobnych plików i niedestrukcyjna lista montażowa.
6. Udostępnienie odziedziczonej funkcji w sesji Podcasty oraz import rozdziałów
   dostawcy.

Każdy etap wymaga testów NVDA dla początkowego fokusu, nawigacji listy,
edytowania czasu, podglądu, anulowania i powrotu do odtwarzacza. W nazwach
dostępnościowych nie mogą pojawić się identyfikatory techniczne modelu.
