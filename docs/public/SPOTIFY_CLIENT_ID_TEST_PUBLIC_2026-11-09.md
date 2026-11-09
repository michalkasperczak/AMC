# Spotify w AMC Test i Public: Client ID i logowanie

**Szkic instrukcji do następnego wydania, 9 listopada 2026 r.** Spotify odtwarza już w AMC Test i Public w odsłuchu użytkownika. Oba profile przechowują ustawienia i logowanie osobno.

## Gdy masz już Client ID

Ten sam identyfikator własnej aplikacji Spotify można wpisać do AMC Test i AMC Public. Oba profile używają adresu powrotu `http://127.0.0.1:43822/spotify/callback/`, lecz w każdym trzeba osobno wybrać logowanie. Kopiuj tylko **Client ID**; nie przenoś Client Secret, tokenów ani całego profilu AMC. Jeśli z aplikacji deweloperskiej korzysta inne konto Spotify, właściciel musi dopisać je do listy dozwolonych użytkowników.

Po następnej aktualizacji otwórz Spotify w AMC i naciśnij `Ctrl+F5`. Gdy identyfikator jest zapisany, okno pokaże go w polu oraz przycisk „Kopiuj identyfikator aplikacji”. Po zalogowaniu przycisk „Zaloguj w przeglądarce” ustąpi miejsca „Wyloguj ze Spotify w AMC”. Wylogowanie usuwa zapisane logowanie tylko z bieżącego profilu AMC; zachowuje Client ID.

## Gdy tworzysz własną aplikację Spotify

1. W AMC otwórz Spotify i naciśnij `Ctrl+F5`. Skopiuj adres z pola „Adres powrotu”. Obecnie jest to `http://127.0.0.1:43822/spotify/callback/`.
2. W zwykłej przeglądarce otwórz [panel aplikacji Spotify](https://developer.spotify.com/dashboard), zaloguj się i wybierz „Create app”.

   https://developer.spotify.com/dashboard

3. Podaj nazwę i krótki opis. W polu **Redirect URI** wklej adres powrotu z AMC znak w znak, łącznie z końcowym ukośnikiem. Jeśli panel pyta o używane API, zaznacz **Web API** oraz **Web Playback SDK**. Zapisz aplikację.
4. W ustawieniach nowej aplikacji skopiuj **Client ID**, czyli publiczny identyfikator. Nie kopiuj **Client Secret**. W trybie deweloperskim właściciel aplikacji musi mieć Spotify Premium. Do jednej aplikacji może dołączyć najwyżej pięciu wskazanych użytkowników.
5. Wróć do AMC: `Ctrl+F5`, wklej Client ID, wybierz „Zapisz identyfikator aplikacji”, a potem „Zaloguj w przeglądarce”. Hasło podaj wyłącznie na oficjalnej stronie Spotify. Po zatwierdzeniu wróć do AMC i otwórz Bibliotekę lub album.
6. Przy błędzie `403` sprawdź w panelu **Settings → Users Management**, czy konto użyte do logowania jest na liście. Przy błędzie adresu powrotu porównaj go z polem AMC, również końcowy ukośnik. `localhost` nie zastępuje `127.0.0.1`.

Client ID służy obecnie **zarówno katalogowi, jak i odtwarzaniu w ukrytym Chrome** przez Spotify Web Playback SDK. Nie trzeba drugiego kodu odtwarzacza. Soloist nie jest obecnie silnikiem AMC; jego ewentualne dodanie wymagałoby osobnego parowania i nie zastąpiłoby Client ID używanego przez katalog.

## Jakość dźwięku i dalsze możliwości

Obecny tor AMC korzysta z Web Playback SDK w Chrome. Spotify opisuje swój web player jako AAC 128 kb/s dla kont Free i AAC 256 kb/s dla Premium, bez przełącznika jakości. Dokumentacja Web Playback SDK nie daje AMC odczytu rzeczywistego kodeka i przepływności konkretnego utworu. Dlatego **nie deklarujemy lossless ani dokładnych 256 kb/s dla konkretnego odsłuchu w AMC**. Dźwięk Spotify w obecnym torze traktujemy jako stratny.

Oficjalny Spotify Soloist deklaruje lossless do 24 bitów/44,1 kHz na koncie Premium oraz lokalne sterowanie i odczyt kolejki. To możliwy osobny kierunek badań dla lepszej jakości i kolejki, lecz niepotrzebny do działania obecnego Spotify w AMC. Wcześniejsze zgłoszenia o kolejce, albumach, komunikatach NVDA i prędkości podcastów pozostają osobnymi punktami odbioru; samo udane logowanie ich nie rozstrzyga.

## Oficjalne źródła

- [Tworzenie aplikacji Spotify](https://developer.spotify.com/documentation/web-api/concepts/apps)

  https://developer.spotify.com/documentation/web-api/concepts/apps

- [Początek pracy z Web Playback SDK](https://developer.spotify.com/documentation/web-playback-sdk/tutorials/getting-started)

  https://developer.spotify.com/documentation/web-playback-sdk/tutorials/getting-started

- [Adresy powrotu](https://developer.spotify.com/documentation/web-api/concepts/redirect_uri)

  https://developer.spotify.com/documentation/web-api/concepts/redirect_uri

- [Tryb deweloperski i limity](https://developer.spotify.com/documentation/web-api/concepts/quota-modes)

  https://developer.spotify.com/documentation/web-api/concepts/quota-modes

- [Jakość dźwięku Spotify](https://support.spotify.com/ca-en/article/audio-quality/)

  https://support.spotify.com/ca-en/article/audio-quality/

- [Ogłoszenie Spotify Soloist](https://developer.spotify.com/blog/2026-08-13-introducing-spotify-soloist)

  https://developer.spotify.com/blog/2026-08-13-introducing-spotify-soloist
