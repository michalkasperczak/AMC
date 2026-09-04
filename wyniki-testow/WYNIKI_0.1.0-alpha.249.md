# Wyniki testów AMC 0.1.0-alpha.249

Wpisz zauważone zachowanie pod odpowiednim zadaniem. Nie trzeba dodawać przed
każdym zadaniem osobnego wariantu „OK” albo „błąd”. Po dwukropku wpisuj spację.

## AMC-249-01 — pierwsza migracja Podcastów

Uruchom wersję tylko raz i poczekaj na Bibliotekę. Sprawdź, czy program nie
wyświetla błędu konfiguracji, zachował podcasty, odcinki, stan odsłuchania,
pozycje, Ulubione, Kolejkę i Playlisty. Pierwszy start może jednorazowo potrwać
kilka sekund dłużej; następny nie powinien ponawiać migracji.

Wynik: 

## AMC-249-02 — ponowne uruchomienie

Zamknij AMC prawidłowo, uruchom ponownie i sprawdź powrót do zapamiętanego
podcastu oraz odcinka. Nawigacja i `Ctrl+1`, `Ctrl+2`, `Ctrl+I` nie powinny się
blokować.

Wynik: 

## AMC-249-03 — strona 250 odcinków

Otwórz podcast mający ponad 250 odcinków. Na końcu pierwszej porcji powinna być
pozycja **Załaduj więcej odcinków, pozostało N**. Nie powinna mówić „odcinek”
ani przyjmować menu działań multimedialnych.

Wynik: 

## AMC-249-04 — doładowanie i fokus

Na pozycji **Załaduj więcej odcinków** naciśnij Enter. Fokus powinien przejść
na pierwszy nowo dołączony odcinek, a komunikat podać liczbę doładowanych
odcinków. Powtórz co najmniej dwa razy i sprawdź nawigację literami.

Wynik: 

## AMC-249-05 — filtr całego podcastu

W dużym podcaście użyj `Ctrl+K` i wpisz fragment tytułu albo opisu odcinka,
który nie należy do pierwszych 250 pozycji. Wynik powinien się pojawić bez
ręcznego doładowywania wcześniejszych stron. Escape ma wyczyścić filtr i
wrócić na listę.

Wynik: 

## AMC-249-06 — stan odcinka z dalszej strony

Po doładowaniu odtwórz odcinek, dodaj go do Ulubionych i Kolejki, zmień
prędkość, wróć Escape. Następnie przejdź do innego widoku i wróć. Fokus i stan
odcinka powinny zostać zachowane.

Wynik: 

## AMC-249-07 — wspólny adres audio

Jeżeli dwa różne odcinki kanału korzystają z tego samego adresu audio, oba
powinny pozostać osobnymi pozycjami i otwierać się według własnego tytułu.

Wynik: 

## AMC-249-08 — regresja NVDA i odtwarzania

Przejdź szybko między Biblioteką, Nowymi odcinkami, W trakcie słuchania,
Pobranymi, Kolejką i odtwarzaczem. Sprawdź Escape, Page Up, Page Down,
strzałki, cyfry procentów, `Alt+D`, `Alt+Enter`, `Ctrl+C` i `Ctrl+Shift+C`.
Fokus nie powinien przechodzić do filtra, menu ani paska stanu.

Wynik: 

## AMC-249-09 — pełna kopia zapasowa

Wyeksportuj pełną kopię AMC. Jeżeli później będzie wykonywany test importu,
użyj wyłącznie kopii danych testowych. Eksport powinien nadal obejmować całe
archiwum Podcastów mimo osobnej bazy `podcasts.db`.

Wynik: 
