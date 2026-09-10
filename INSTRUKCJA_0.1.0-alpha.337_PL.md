# AMC 337 — logowanie i test odtwarzania TIDAL

Ta wersja poprawia przekazywanie logowania przy wznowieniu po pauzie i dodaje
czytelny raport ostatniej próby. Nie oznacza odblokowania pełnych utworów.
Nie trzeba ponownie tworzyć aplikacji deweloperskiej ani wpisywać jej ID.
Nie dodano nowego sposobu logowania ani nakładki na pełną stronę TIDAL.

## Uruchomienie

1. Jeżeli nagrywasz radio, dokończ nagranie. Nie zamykaj programu w trakcie.
2. Zamknij dotychczasowy AMC i uruchom:
   `D:\Projekty Codex\Accessible Multimedia Controller\publish\AccessibleMediaController-0.1.0-alpha.337\AccessibleMediaController-0.1.0-alpha.337.exe`
3. Sprawdź w tytule okna numer 337. Nie rozpakowuj ZIP-a bezpośrednio do
   `publish`. Gotowa wersja znajduje się już w osobnym folderze.
4. Przejdź do swojej sesji TIDAL. Zachowaj istniejące ustawienia logowania.

## Krótki test — najważniejsze

1. Otwórz album i odtwórz jeden utwór. Poczekaj na dźwięk albo błąd.
2. Otwórz Ctrl+F5. Wybierz przycisk „Diagnostyka odtwarzania” Tabem albo
   naciśnij Alt+D w tym oknie. Poza tym oknem Alt+D zachowuje dotychczasową rolę.
3. Raport pokazuje, czy przygotowano aktualne logowanie, czy SDK je odczytał,
   czy rozpoczął odtwarzanie oraz czy otrzymał próbkę, pełny materiał lub
   nie przekazał odpowiedzi. Przy próbce podaje rozpoznany powód i jej czas.
4. Użyj „Kopiuj wszystko” i wklej raport w rozmowie. Możesz też opisać go
   swoimi słowami. Przycisk kopiowania nie zamyka raportu.
5. Escape zamyka raport i wraca do przycisku diagnostyki. Drugie Escape
   zamyka ustawienia i powinno wrócić do odtwarzacza, jeżeli stamtąd je
   otwarto, albo do listy, jeżeli ustawienia otwarto z listy.

Raport jest migawką przy jego otwarciu. Aby zobaczyć nowy wynik, zamknij go
i otwórz ponownie. Nie jest zapisywany w ustawieniach ani odczytywany z konta.
Log techniczny zapisuje zdarzenia bez tokenów, haseł i identyfikatora konta.
Nie wysyłaj całych plików ustawień ani poświadczeń.

## Co znaczą wyniki

- „Logowanie użytkownika odczytane przez SDK: tak” oznacza, że silnik dostał
  poświadczenie użytkownika. Nie potwierdza samodzielnie prawa do pełnego audio.
- „Wyższy poziom dostępu aplikacji” pokazuje się tylko po takim rozstrzygnięciu
  TIDAL. Wielokrotne logowanie tego nie naprawi.
- „Subskrypcja” lub „zakup” oznacza inną przyczynę zgłoszoną przez TIDAL.
  To wynik dla tego materiału, nie automatyczna diagnoza całego konta.
- „Brak potwierdzonej odpowiedzi” nie oznacza, że konto jest wylogowane ani
  że udostępniono próbkę. Potrzebna będzie analiza pozostałych komunikatów.
- „Pełny materiał według odpowiedzi TIDAL” wymaga odsłuchu ponad 30 sekund,
  przewinięcia dalej i sprawdzenia następnego utworu. Sam napis nie wystarcza.

## Dodatkowy test pauzy

Podczas odtwarzania naciśnij Spację, odczekaj kilka sekund i naciśnij ją
ponownie. Pozycja oraz głośność powinny zostać zachowane. Otwórz raport:
działanie powinno brzmieć „wznowienie po pauzie”. Nie musisz czekać godziny
na wygaśnięcie logowania — taki przypadek jest objęty testem syntetycznym.
Koniec próbki lub błąd nie może skasować kolejki ani uruchomić serii innych utworów.

## Obserwacje

Możesz pisać swobodnie, bez wypełniania osobnych pól OK/błąd.

Pierwszy utwór i wynik: …

Raport diagnostyczny: …

Pauza i wznowienie: …

Powrót Escape i fokus: …

Inne uwagi: …
