# AMC alpha.334 — TIDAL: dźwięk i ochrona kolejki

Można testować opisowo. Uwagi wpisz po dwukropku; nie trzeba wybierać OK/Błąd.
Nie zamykaj starszego AMC podczas nagrywania. Nowy pakiet jest osobnym folderem
w publish; uruchom znajdujący się w nim EXE dopiero po zakończeniu nagrywania.

## Co zostało naprawione

Blokada dźwięku przez WebView2 nie jest błędem logowania. Klawisze Enter i
Spacja obsługiwane przez AMC mogą teraz uruchomić dźwięk SDK. Poprawka nie
zmienia ustawień Twojego Chrome ani NVDA.
Powodem niezamierzonego przechodzenia po kolejnych utworach było traktowanie
sygnału SDK zakończonego błędem jako prawidłowego końca utworu.

## Krótki test na koncie

1. Otwórz znany utwór w TIDAL (np. Usta). Czy pojawia się dźwięk i czy pozostaje
   wybrany właściwy utwór? Nie loguj się ponownie, jeśli nie ma błędu autoryzacji.
Uwagi: 

2. Sprawdź, czy to pełny utwór, czy 30-sekundowa próbka. Jeżeli AMC zgłasza
   próbkę/ograniczenie dostępu, zanotuj komunikat — sama poprawka nie przyznaje
   aplikacji wyższego poziomu dostępu w TIDAL.
Uwagi: 

3. Pauza i wznowienie Spacją; następnie kilka świadomych przełączeń Page Up /
   Page Down. Po zatrzymaniu żaden starszy utwór nie powinien sam wrócić.
Uwagi: 

4. Jeżeli wystąpi błąd, sprawdź, czy wskazany utwór pozostaje w Kolejce i nie
   włącza się inna piosenka. Po końcu próbki kolejka również ma zostać zachowana.
Uwagi: 

## Testy automatyczne

- 7 testów JS: pomijanie/błąd kontra naturalny koniec, odmowa autoplay,
  stare błędy, przełączanie, pauza, wznowienie, identyfikacja produktu i próby.
- Test C#: stare/obce/brakujące identyfikatory, błąd i duplikaty, zakończenie
  próbki, prawidłowy koniec, nienaruszona Kolejka po odmowie, redakcja logu.
- Rzeczywisty WebView2: lokalny WAV i PostWebMessage, bez konta/sieci TIDAL;
  bez poprawki NotAllowedError, z poprawką playing.
- Pełny zestaw Core i Windows: sprawdzany przy kompilacji pakietu.
- Odsłuchu TIDAL na koncie ani NVDA w nowej wersji nie potwierdzono automatycznie;
  bieżącej aplikacji nie zamykano ze względu na nagrywanie.
