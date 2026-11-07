# TIDAL w AMC — logowanie i dodawanie do playlist

Szkic instrukcji z 7 listopada 2026, do odbioru poprawki alpha.483. Obowiązuje po aktualizacji do paczki zawierającej tę poprawkę. Dotychczasowa instalacja alpha.482 ma wcześniejszy interfejs. Public i Test korzystają ze wspólnego kodu, lecz zachowują osobne logowania i dane.

## Dlaczego przy pierwszym użyciu są dwa zatwierdzenia

Obecna integracja AMC ma dwa osobne połączenia TIDAL. Pierwsze udostępnia katalog, własną bibliotekę i playlisty. Drugie pozwala wewnętrznemu odtwarzaczowi odtwarzać pełne utwory. Logowanie przez Google w pierwszym etapie nie zastępuje zatwierdzenia kodu pełnego odtwarzacza. Oba połączenia mają wskazywać to samo konto TIDAL.

To opis obecnej implementacji AMC. Nie oznacza, że każdy program korzystający z TIDAL musi mieć identyczny interfejs. Pełny odtwarzacz AMC korzysta z nieoficjalnej integracji. Zapisane połączenia są używane ponownie do czasu wygaśnięcia albo odłączenia; nie należy oczekiwać dwóch zatwierdzeń przy każdym utworze.

## Pierwsze połączenie

1. Przez Ctrl+Shift+S wybierz TIDAL. Ctrl+F5 otwiera konto i synchronizację.
2. Wybierz „Zaloguj w przeglądarce”. Zaloguj się na stronie TIDAL przez Google lub inną dostępną metodę. Hasło wpisujesz wyłącznie na stronie dostawcy.
3. Po potwierdzeniu wróć do AMC, zamknij okno konta przez Escape i otwórz Bibliotekę przez Ctrl+L.
4. Otwórz album i naciśnij Enter na utworze. Jeśli pełny odtwarzacz nie jest jeszcze połączony, AMC pokaże osobne okno.
5. Wybierz „Otwórz przeglądarkę z kodem”. Przycisk otwiera zwykłą domyślną przeglądarkę na stronie TIDAL z kodem zawartym w adresie. Potwierdź na tym samym koncie. AMC nie otwiera tej strony automatycznie przy pokazaniu okna.
6. Przyciski „Kopiuj adres” i „Kopiuj kod” pozostają dostępne, gdy chcesz samodzielnie przejść do przeglądarki. Jeżeli uruchomienie przeglądarki się nie powiedzie, AMC poda komunikat i pozostawi możliwość skopiowania adresu.
7. Okno kodu nie blokuje głównego okna AMC. Po otwarciu strony możesz wrócić do AMC; przycisk pozwala ponownie otworzyć tę samą stronę podczas oczekiwania. Po udanym zatwierdzeniu kodu odtwarzanie rozpocznie się w AMC. „Anuluj logowanie” kończy oczekiwanie.

W próbie użytkownika alpha.482 nowe połączenie przez Google, biblioteka i pełny utwór zadziałały. Nie musisz teraz usuwać zapisanej sesji, aby sprawdzić poprawki playlist, języka i powrotu Enter. Nowy przycisk można odebrać przy następnym rzeczywistym wymaganiu zatwierdzenia odtwarzacza.

## Client ID i zakres publicznego wydania

Gdy AMC ma skonfigurowany identyfikator swojej aplikacji katalogu, użytkownik loguje się własnym kontem. W obecnej próbie identyfikator był już skonfigurowany w profilu Public. Nie dodano poświadczeń ani identyfikatora z tego profilu do instalatora. Ta próba na własnym koncie nie dowodzi jeszcze gotowości zupełnie pustej instalacji dla innej osoby. Produkcyjna konfiguracja aplikacji AMC i osobny użytkownik pozostają do potwierdzenia przed szerszym wydaniem. Nie trzeba ponownie przenosić działającego toru audio z Public do Test.

## Dodawanie do playlist — Ctrl+Shift+P

Na utworze, zaznaczeniu albo otwartym albumie naciśnij Ctrl+Shift+P. Lista zawiera nazwy playlist i stan zaznaczenia, np. „Ulubione, niezaznaczona”. W TIDAL przed każdą nazwą nie ma już powtarzanego „Playlista TIDAL”.

- Strzałki wybierają wiersz, a litery służą do przejścia do nazwy, np. U do „Ulubione”.
- Enter na liście dodaje wybrane materiały do wskazanej playlisty. Nie musisz najpierw zaznaczać jej Spacją ani przechodzić Tabem do przycisku.
- Jeśli chcesz dodać do kilku playlist, zaznacz je Spacją, a następnie potwierdź Enterem lub przyciskiem „Dodaj”. Po użyciu Spacji Enter zatwierdza cały przygotowany wybór.
- Jeżeli odznaczysz wszystkie cele, Enter nie wykona dodawania i poprosi o wskazanie playlisty.
- Escape anuluje okno. Anulowanie nie zmienia zapamiętanego, potwierdzonego wyboru.
- Po udanym dodaniu AMC zapamiętuje potwierdzone playlisty. Ponowne otwarcie ustawia kursor na pierwszym zapamiętanym celu. Przy pojedynczym celu wrócisz na ostatnio używaną playlistę.

Utworzona i potwierdzona playlista trafia do pamięci listy. Ponowne otwarcie korzysta z migawki z tej sesji aplikacji; zmiany z innych programów mogą wymagać odświeżenia lub ponownego uruchomienia. Serwer sprawdza uprawnienia ponownie przy dodawaniu. Częściowy błąd nie jest przedstawiany jako powodzenie dla wszystkich celów. Wspólne okno Spotify ma takie same zasady Enter i Spacji.

## Powrót do odtwarzacza i język

Escape wraca z odtwarzacza do listy. Enter na tym samym odtwarzanym utworze ponownie pokazuje odtwarzacz. Jeżeli utwór dopiero się przygotowuje, ponowny Enter nie uruchamia drugiego żądania; AMC informuje o trwającym przygotowaniu.

Ustawienie języka „Zgodny z systemem” korzysta z języka interfejsu Windows. Na polskim Windows okno powinno być po polsku również po uruchomieniu wxPython. Jawnie wybrany angielski nadal pozostaje angielski.

## Jakość i nieznany czas playlisty

NVDA+End podaje źródło i osobno PCM w silniku. Użytkownik potwierdził AAC/PCM 44,1 kHz dla „Sara” oraz Max FLAC 24 bit/PCM 96 kHz dla „Cała naprzód” Misi Furtak. W Public zaobserwowano też Max/PCM 192 kHz. Format urządzenia Windows może być inny od formatu silnika.

AMC pokazuje czas playlisty, gdy otrzyma dodatnią długość w danych katalogu. Gdy odpowiedź jej nie zawiera albo ma nieobsługiwany format, obecny kod pozostawia czas nieznany. To może wyjaśniać brak czasu przy My Mix, lecz w tym etapie nie zbadano odpowiedzi konkretnych miksów użytkownika. Nie podajemy zmyślonego zera ani sumy częściowo pobranej listy. Brak czasu całej playlisty sam w sobie nie dowodzi błędu odtwarzania utworów.
