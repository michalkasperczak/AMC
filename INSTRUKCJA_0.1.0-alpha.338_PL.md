# AMC 338 — otwieranie wykonawcy i bezpieczne odświeżanie listy

Ta wersja poprawia błąd AMC, który mógł wymuszać ponawianie wejścia do
wykonawcy, albumu lub playlisty. Nie odblokowuje pełnego odtwarzania TIDAL
i nie zmienia logowania. Raport próbki z 337 został już przeanalizowany.

## Uruchomienie

Po zakończeniu ewentualnych nagrań zamknij poprzednią wersję i uruchom:

`D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.338\AccessibleMediaController-0.1.0-alpha.338.exe`

Sprawdź numer 338 w tytule okna. Nie trzeba rozpakowywać ZIP-a ani ponownie
się logować. Nie zamykano działającego AMC podczas przygotowania wydania.

## Krótki test

Możesz opisać wyniki swobodnie. Wystarczy uwaga pod całością, bez osobnych
pól OK/błąd. Pełne testy odsłuchu NVDA pozostają po stronie odbioru wydania.

1. Otwórz TIDAL i na utworze lub albumie Stevie Wondera naciśnij prawą
   strzałkę. Wybierz „Przejdź do wykonawcy” jeden raz. Poczekaj na wynik:
   po ewentualnym „Wczytywanie…” mają pojawić się albumy wykonawcy,
   bez drugiego uruchamiania tej samej funkcji. Sprawdź też innego wykonawcę.
2. Powtórz przejście po zmianie kolekcji, np. dodaniu albumu. Samo
   odświeżenie etykiet i ponowne zaznaczenie tego samego wiersza nie powinno
   anulować wejścia. Sprawdź raz także po zaznaczeniu kilku elementów.
3. Podczas wolnego otwierania naciśnij Escape, zmień sesję albo przejdź na
   inny element. Spóźniony wynik nie może cofnąć tej decyzji. To samo
   dotyczy wpisania nowego filtra, nawet gdy na liście pozostanie ten sam plik.
4. Sprawdź powrót Escape do poprzedniego widoku i zwykłe strzałki.
   Nie powinien zmienić się sposób czytania ani miejsce fokusa.

Jeśli wejście nadal wymaga ponowienia, wystarczy podać element i przybliżoną
godzinę. Dodano log czasu żądania i powodu pominięcia jego wyniku; nie trzeba
przesyłać logowania, ustawień konta ani całego pliku logów.

Uwagi: …

## Co wiadomo o próbkach

W 337 potwierdzono przygotowanie poświadczenia użytkownika, odczyt przez SDK
i odtwarzanie próbki około 30 sekund. TIDAL podał
`FULL_REQUIRES_SUBSCRIPTION`. Nie jest to dowód braku opłaconego abonamentu
użytkownika ani dowód konkretnego poziomu dostępu aplikacji.
Pełne odtwarzanie pozostaje niewyjaśnione. Ponowne identyczne logowanie nie
jest zalecanym kolejnym testem. Nie obiecujemy, że ta poprawka je odblokuje.
