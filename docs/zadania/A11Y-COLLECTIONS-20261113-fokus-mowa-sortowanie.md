# A11Y-COLLECTIONS-20261113 — fokus i mowa po zmianach kolekcji

- Status: **kod oraz testy celowane gotowe; pełna regresja, paczka, instalacja
  i publikacja w toku**.
- Utworzono: 13 listopada 2026.
- Obszar: TIDAL, Spotify, lokalne Ulubione, Biblioteka Radia, playlisty,
  kolejki, wyszukiwanie, NVDA.

## Cel

Po dodaniu lub usunięciu elementu AMC ma zachować logiczny fokus, nie
przerywać komunikatu NVDA późnym odczytem pierwszego wiersza i pamiętać tylko
operacje potwierdzone przez usługę. Audyt obejmuje Bibliotekę, Ulubione,
playlisty, kolejki, sortowanie `Alt+1–3`, kategorie `Ctrl+Alt+1–3`, filtr i
wyniki wyszukiwania we wszystkich sesjach.

## Wdrożone zachowanie

- Usunięcie z otwartej kolekcji wybiera następny widoczny element; po usunięciu
  ostatniego wybiera poprzedni. Wiersz „Załaduj więcej” nie może zostać
  wyborem zastępczym.
- TIDAL po `Ctrl+Shift+U` lub `Ctrl+Shift+L` przekazuje sąsiedni element do
  odświeżonego widoku i wypowiada wynik dopiero po zakończeniu natywnej zmiany
  listy. Naprawia to skok fokusu na początek i zagłuszanie ponownego dodania.
- Ten sam wybór sąsiada zastosowano do lokalnych Ulubionych, Biblioteki Radia
  oraz ogólnego `Delete` w Bibliotece, Historii, kolejkach i playlistach.
- Prywatne kolejki TIDAL i Spotify stosują tę samą regułę oraz wypowiadają
  potwierdzenie dopiero po odświeżeniu listy; usunięcie nie przenosi już
  kursora na pierwszy wiersz.
- Spotify zapisuje ostatnio wybrane playlisty wyłącznie dla wyników, których
  pole `failed` wynosi zero. Niepotwierdzony cel częściowej operacji nie wróci
  jako pozornie poprawny wybór.
- Apple Music `Ctrl+F` przeszukuje aktualnie wczytaną, bezpiecznie
  znormalizowaną stronę i po Enterze wraca do tego samego wiersza. Nie udaje
  przeszukiwania całej, niewczytanej biblioteki.

## Uzasadnienie i decyzje

W TIDAL komunikat był wypowiadany przed asynchronicznym odświeżeniem, a po
usunięciu przekazywano brak preferowanego wiersza. Natywna lista wybierała
wtedy pierwszy element i jej późne zdarzenie fokusu przecinało komunikat NVDA.
Spotify miał już częściowo poprawny wzorzec, dlatego wspólną regułę wyboru
sąsiada przeniesiono do jednej funkcji i zastosowano w kolejnych ścieżkach.

Sortowanie Apple Music nie zostało włączone. Adapter pobiera strony po 25
pozycji, więc lokalne `Alt+1–3` sortowałoby tylko fragment i podawało mylący
wynik. Ulubione oraz dwukierunkowy zapis Apple także pozostają wyłączone do
czasu potwierdzenia rzeczywistego API.

## Materiał do dokumentacji użytkownika

Po usunięciu utworu, stacji lub wpisu z otwartej listy fokus pozostaje w tym
samym miejscu listy: przechodzi na następny element, a na końcu listy na
poprzedni. Końcowy komunikat o dodaniu lub usunięciu jest ostatnią wypowiedzią
NVDA. W Apple Music `Ctrl+F` obejmuje obecnie tylko pozycje już wczytane w
bieżącym widoku; `Ctrl+K` nadal filtruje tę listę bez otwierania wyszukiwarki.

## Testy i wynik

- Testy wyboru następnego, poprzedniego i pustej listy oraz pomijania
  „Załaduj więcej”: zaliczone.
- TIDAL: usunięcie środkowego Ulubionego przekazuje następny wiersz i gotowy
  komunikat do odświeżenia: zaliczone.
- Lokalne Ulubione i Biblioteka Radia: testy zapisu, fokusu i komunikatu:
  zaliczone.
- Spotify playlisty: testy tworzenia, potwierdzonych celów i pamięci wyboru:
  zaliczone.
- Kolejki Spotify i TIDAL: następny widoczny wiersz oraz mowa po synchronizacji:
  11/11 testów modułu kolejki zaliczonych.
- Szeroka regresja bez dwóch wcześniej znanych kontroli kompletności języka
  angielskiego: **2724 zaliczone, 117 pominiętych i 34 podtesty zaliczone**.
  Testy GUI Radio Archiwisty, izolowane z powodu konfliktu atrap `wx`: 3/3.
  Dwie kontrole pełnego katalogu angielskiego pozostają czerwone z powodu
  zastanego długu 127 tekstów i pięciu pól Ustawień; nie zostały ukryte ani
  przedstawione jako regresja tej zmiany.

## Znane ograniczenia

- Audyt wykazał dalsze miejsca do ręcznej próby NVDA: wielokrotne nieciągłe
  zaznaczenie z filtrem, szybka zmiana sesji podczas odpowiedzi i kolejki
  wszystkich dostawców.
- Apple Music nie ma jeszcze Ulubionych, zapisu Biblioteki, sortowania pełnej
  kolekcji ani odtwarzacza. Nie są one reklamowane jako gotowe.
- `Ctrl+F` Apple Music nie wykonuje jeszcze zapytania katalogowego i nie
  pobiera automatycznie wszystkich stron.

## Wersja i commit

Wersja docelowa: AMC Test `0.1.0-alpha.498`. Commit, paczka i wydanie zostaną
dopisane po bramce wydania.

