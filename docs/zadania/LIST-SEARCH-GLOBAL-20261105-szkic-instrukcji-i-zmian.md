# Szkic instrukcji i zmian — kandydat AMC Test alpha.480

Materiał roboczy do korekty użytkownika. Nie jest opublikowanym opisem wydania.

## Instrukcja

Na zwykłych listach TIDAL i Spotify utwory są czytane bez powtarzania słowa „utwór”. Lewa strzałka na utworze podaje czas i tytuł. W wynikach Ctrl+F i Ctrl+Shift+F pozostaje rodzaj pozycji, aby odróżnić utwór od albumu i wykonawcy. Prawa strzałka na wyniku TIDAL lub Spotify pokazuje dostępne przejścia do albumu, wykonawcy lub podcastu.

Alt+R otwiera aktywne nagrania, Alt+Shift+R nagrane pliki, Ctrl+I skrzynkę nowych odcinków, a Ctrl+Alt+S rozpoznane utwory. Można wywołać te listy z każdej sesji. Escape wraca do miejsca, z którego lista została otwarta.

Ctrl+Shift+F korzysta z zapisanych i już wczytanych danych. Wśród wyników Spotify mogą znaleźć się pozycje z obecnej listy i historii odtwarzania. Pełne wyszukiwanie katalogu Spotify lub TIDAL uruchamia się przez Ctrl+F po przejściu do właściwej sesji.

Na albumie otwartym z listy rozpoznanych utworów Ctrl+Shift+L zapisuje album do Biblioteki Spotify lub TIDAL. Backspace wraca do albumów rozpoznanego wykonawcy i wskazuje ten album. Enter na utworze TIDAL uruchamia odtwarzanie w AMC. Przekazanie utworu do oryginalnego TIDALa pozostaje osobną pozycją menu Biblioteka i menu kontekstowego.

W odtwarzaczu TIDAL NVDA+End odczytuje dostępny bitrate w kb/s i częstotliwość w kHz. W radiu bieżący status oraz Alt+D również podają bitrate, gdy da się go wiarygodnie zmierzyć lub odczytać z metadanych strumienia.

W plikach lokalnych, podcastach i Radiu Archiwista odczyt bieżącego materiału podaje średni bitrate, jeżeli znany jest czas i rozmiar pliku. W Spotify Web Player odczyt podaje deklarowaną jakość usługi: AAC 256 kb/s dla muzyki Premium albo około 128 kb/s dla odcinka. To opis jakości usługi, nie pomiar konkretnego utworu. WiiM wypowiada bitrate, gdy urządzenie go udostępnia. Brakująca wartość jest pomijana. Te same pełne opisy są dostępne w odczycie bieżącego materiału i stanu przez dodatek NVDA.

## Lista zmian

- Krótszy odczyt utworów na listach TIDAL i Spotify.
- Czas i tytuł pod lewą strzałką na utworze Spotify i TIDAL.
- Jasny rodzaj w wynikach wyszukiwania bez powtarzania wykonawcy w szczegółach.
- Znane pozycje Spotify w wyszukiwaniu globalnym bez dodatkowego połączenia z katalogiem.
- Cztery wspólne listy dostępne z każdej sesji, ze spójnymi skrótami, menu, paletą i pomocą.
- Kontekstowy powrót z rozpoznanego albumu do albumów jego wykonawcy w Spotify i TIDAL.
- Enter w TIDAL odtwarza w AMC; osobne polecenie przekazuje utwór do oryginalnej aplikacji.
- Bitrate TIDAL trafia do statusu odtwarzacza; radio HLS sprawdza segment audio także wtedy, gdy manifest podał sam kodek.
- Archiwista i zdalne odcinki szacują bitrate po rozpoczęciu odtwarzania z czasu i rozmiaru pliku; WiiM przekazuje wartość podaną przez urządzenie, a Spotify jawnie opisuje deklarowaną jakość Web Playera.
- Komendy NVDA odczytują pełny opis materiału zamiast samego tytułu, gdy opis jest dostępny.

Stan: poprzednie etapy mają odpowiednio 240 i 231 zaliczonych testów. Pierwsza poprawka bitrate miała 59 testów Pythona, 6 podtestów i dwa celowane przebiegi C#. Rozszerzenie z 6 listopada przeszło 142 testy Python oraz trzy zestawy C# w izolowanym checkoutcie. Nagranie zakończone; instalator i ręczny odbiór NVDA oczekują na wykonanie.
