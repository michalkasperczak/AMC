# QUEUE-002 — kolejki AMC Test i Public

Stan: 14.11.2026. Użytkownik zlecił przegląd aktywnego AMC Test wxPython,
z którego powstaje AMC Public. To zastępuje planistyczną decyzję z 30.10.2026
o odłożeniu przeglądu. Historyczny kod WPF nie należy do tego zadania.

## Zachowanie i decyzje

- `Ctrl+Q` otwiera żywą kolejkę plików, jeśli host ją zna; przed jej
  inicjalizacją otwiera kolejkę zapisaną. Komunikat otwarcia podaje nazwę,
  liczbę pozycji i łączny czas z biblioteki. Częściowo znany czas jest
  zapowiadany jako częściowy, a brak danych jako nieznany, bez fałszywego zera.
- Czas przy otwarciu podają także zapisane kolejki plików i podcastów,
  prywatne kolejki Spotify, TIDAL i Radio Archiwisty oraz zawartość lokalnej
  playlisty. Lewa strzałka na lokalnej playliście czyta jej czas, jeśli jest
  znany. Gdy usługa nie zwraca długości, program mówi o braku danych.
- W otwartej kolejce `Alt+strzałka w górę/w dół` przenosi jeden zaznaczony
  wpis. Zmiana jest trwała. W kolejce hosta nie przestawia grającego wpisu
  ani nie przenosi zwykłego wpisu przez grupę „Odtwórz jako następne”.
  Kolejki Spotify, TIDAL i Radio Archiwisty zapisują własny porządek;
  zapisane kolejki plików i podcastów zapisują porządek profilu.
- `Delete` usuwa wybrane wpisy z otwartej kolejki, także żywej. W żywej
  kolejce nie zatrzymuje aktualnego dźwięku. Następny i naturalny koniec
  uwzględniają aktualny porządek również po usunięciu bieżącego wpisu.
- Menus, opisy skrótów i dostępność komend uwzględniają kolejki w ich
  sesjach. Nie dodano kolejki do sesji, które nie mają takiej funkcji.

## Sprawdzenia

- Celowane testy Python: 170 istniejących zaliczonych; dodano 4 testy
  komunikatów czasu, odczytu długości, przenoszenia/Delete i skrótów.
- Test silnika `--queue`: zaliczony po dodaniu przypadków przestawienia,
  Delete następnego oraz Delete grającego wpisu i dalszego przestawienia.
- Kompilacja LiteHost: zaliczona. Testy pełne, paczki, instalatory,
  instalacja, publikacja i ręczny odsłuch NVDA: odnotować po wykonaniu.

## Szkic instrukcji i listy zmian do korekty użytkownika

Otwórz kolejkę przez `Ctrl+Q`. AMC poda liczbę pozycji i jej czas trwania,
jeśli dane są dostępne. Zaznacz jedną pozycję i użyj `Alt+strzałka w górę` lub
`Alt+strzałka w dół`, aby zmienić kolejność. `Delete` usuwa zaznaczone pozycje
z kolejki. Podczas odtwarzania bieżąca pozycja nie zmienia miejsca, a usunięcie
jej z kolejki nie przerywa dźwięku. Otwarta playlista podaje czas trwania;
na lokalnej playliście lewa strzałka czyta czas samej pozycji.

Lista zmian: poprawiono usuwanie i ręczną kolejność kolejek w Test/Public.
`Ctrl+Q` oraz widoki playlist podają czas trwania z uczciwą informacją o
brakujących długościach. Poprawiono zgodność widoku z rzeczywistym następnym
utworem po Delete.

## Wersja i ograniczenia

Planowana wersja: `0.1.0-alpha.503`. Commit i wynik wydania do uzupełnienia.
Ręczne potwierdzenie NVDA oraz rzeczywiste konta Spotify/TIDAL/Radio
Archiwisty wymagają odbioru na zainstalowanej aplikacji.
