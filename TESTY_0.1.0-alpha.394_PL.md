# AMC 0.1.0-alpha.394 — Spotify i Spotify — Librespot

## Dwie sesje w jednym programie

Dotychczasowa sesja Spotify nadal używa oficjalnego Web Playback SDK. Nowa sesja Spotify — Librespot jest dopisana na końcu listy sesji. Nie zastępuje starej i nie zmienia jej skrótów.

Obie sesje korzystają ze wspólnego katalogu i biblioteki Spotify, ale mają oddzielne kolejki, bieżący utwór, pamięć pozycji i ustawienia. Odtwarzanie Librespot wymaga osobnego, jednorazowego parowania tego samego konta Premium.

## Co sprawdzić w nowej sesji

1. Otwórz listę sesji i wybierz Spotify — Librespot. Zapamiętana biblioteka Spotify powinna być dostępna również tutaj.
2. Naciśnij Ctrl+F5. Otwiera się konto Spotify — Librespot. Czytnik ma najpierw odczytać stan i instrukcję. Jeżeli parowanie jest już zapisane, nie trzeba go powtarzać.
3. Przy pierwszym parowaniu wybierz Rozpocznij parowanie, następnie Otwórz stronę Spotify. Kod i adres są również w polach tylko do odczytu, z kursorem umożliwiającym ich odczytanie i skopiowanie. Przeglądarka nie otwiera się samoczynnie. Zatwierdzenie na stronie kończy oczekiwanie w AMC; Escape anuluje oczekiwanie.
4. Przycisk Konto katalogu i biblioteka otwiera dotychczasowe ustawienia Spotify. Po zamknięciu wraca okno Librespot. Parowanie odtwarzania nie zastępuje logowania do katalogu.
5. Odtwórz rzeczywisty utwór. Sprawdź pauzę, wznowienie i przewijanie również podczas pauzy.
6. Naciśnij Shift+A. Wybierz dostępne urządzenie i zatwierdź Enterem. Zmiana powinna przenieść dźwięk i wznowić utwór od bieżącego miejsca; może wystąpić krótka przerwa. Nie zmienia ustawień miksera Windows.
7. Zamknij i uruchom program ponownie. W Shift+A ma pozostać wybrane urządzenie. Escape z okna ma przywracać fokus na listę.
8. Dodaj różne pozycje do kolejek obu sesji. Kolejki nie powinny się wzajemnie zmieniać. Zmiana Biblioteki lub Ulubionych dotyczy natomiast wspólnego konta.
9. Odłącz tylko Librespot w jego oknie konta. Stare logowanie Spotify i zapamiętana biblioteka mają pozostać. Zwykłe zamknięcie programu nie usuwa parowania.

## Wspólna obsługa Spotify

- Ctrl+F przeszukuje katalog Spotify. Przy błędzie sieci pozostawia dostępne wyniki lokalne i komunikat.
- Ctrl+Shift+U i Ctrl+Shift+L zapisują zmiany na koncie. Stare logowanie katalogu może wymagać ponownej zgody na zmianę biblioteki. Odmowa nie może wyglądać jak sukces.
- Usunięcie z Ulubionych nie zatrzymuje odtwarzania. Dodanie z wyszukiwania nie powinno tworzyć duplikatu wiersza.
- Strzałka w prawo na wykonawcy prowadzi do albumów. Na albumie pokazuje również przejście do wykonawcy, jeżeli katalog zawiera tę relację. Starszy zapis biblioteki może wymagać odświeżenia danych.
- Alt+Shift+Enter otwiera rzeczywiste opcje pamiętania pozycji. Wybór powinien przetrwać ponowne uruchomienie.
- Ctrl+Shift+E, Ctrl+Shift+R i Ctrl+Shift+T odczytują czas od początku, czas pozostały i długość.
- Alt+Enter otwiera właściwości z natywnym kursorem tekstowym. Strzałki i NVDA+góra czytają tekst. Przełącznik zachowuje widok dokumentu HTML.

## Granice tego etapu

Librespot nie obsługuje Spotify Lossless. Nowa sesja daje oddzielny wybór wyjścia, ale nie dźwięk bezstratny ani zmianę tempa.

Shift+A wybiera wyjście w sesji Spotify — Librespot. Nie dodaje tej możliwości do dotychczasowego Web Playback SDK.

Tworzenie i edycja playlist na koncie Spotify oraz zewnętrzny wariant Lossless pozostają osobnymi etapami. Lokalna kolejka AMC nie jest playlistą zapisywaną na koncie.

## Zakres przeprowadzonej weryfikacji

Testy wykonano na Windows. Żywy NVDA sprawdził prawdziwe okno AMC na odrębnych, próbnych danych: konto, wybór urządzenia, zapis i odtworzenie wyboru po ponownym uruchomieniu oraz powrót fokusu.

Osobna próba na koncie potwierdziła rzeczywiste odtwarzanie i przełączenie z Realteka na Denona PMA-1700NE, z pomiarem sygnału przypisanym do procesu odtwarzacza. Pauza, przewijanie na pauzie i wznowienie również przeszły. Próba nie restartowała działającego AMC ani nie zmieniała miksera Windows.

Próby zapisu Biblioteki i Ulubionych opierają się na kontrolowanych odpowiedziach HTTP; nie zmieniały rzeczywistej kolekcji użytkownika.
