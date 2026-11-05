# TIDAL Max, komunikaty list i kontrola odtwarzacza — 5 listopada 2026

## Zakres i decyzje użytkownika

Po wydaniu alpha.472 użytkownik potwierdził, że listy reagują wyraźnie szybciej. Zlecił rzeczywistą najwyższą jakość TIDAL, usunięcie zbędnego słowa „album” przy czytaniu albumu, sprawdzenie skoków, zakładek, historii i kolejki, audyt jakości Spotify oraz wrażenia ucięcia początku po Page Up/Down. Obecna decyzja o jakości zastępuje stan z 5 listopada opisany w końcu `TID-001-pelne-odtwarzanie-w-AMC-Test.md`: alpha.472 żądała High; kandydat alpha.473 żąda Max, obsługuje jego manifest DASH, a przy niezgodnym wariancie wraca do High.

## Wynik analizy i zmiany źródeł

1. **TIDAL Max.** AMC Test żąda `HI_RES_LOSSLESS`. Jeżeli TIDAL zwróci pełny, niezaszyfrowany MPEG-DASH FLAC, walidowany manifest jest przepisywany na losową lokalną zdolność HTTP; podpisane adresy segmentów pozostają tylko w pamięci. FFmpeg dostaje jawny format DASH. Odpowiedź rzeczywistego konta dla dwóch publicznych utworów podała 24 bit/192 kHz i 70 segmentów dla pierwszego. Dekoder odczytał jedną sekundę PCM od początku i od 30. sekundy. Dla odpowiedzi BTS działa dotychczasowy tor. Przy nieobsługiwanym DASH adapter żąda High ponownie. Po potwierdzonym starcie NVDA słyszy rzeczywistą jakość, format, głębię i próbkowanie, gdy usługa je podała. Max zależy od konkretnego utworu i konta.
2. **Lista TIDAL.** Dla albumu kolumna rodzaju jest pusta. Tytuł, wykonawca i czas zostają w zwykłych kolumnach, więc odczyt brzmi np. „Gemini, Kasia Kowalska, 45:21”. Lewa strzałka nadal podaje krótką informację. Potwierdzenie użytkownika: nawigacja po listach po alpha.472 jest znacznie szybsza; nie ma dowodu, że wymaga dalszych odczytów sieciowych na każdą strzałkę.
3. **Skoki i zakładki.** W Plikach, Spotify i wewnętrznym TIDAL `J` otwiera wpisanie czasu, `Shift+J` procentu; `B` dodaje zakładkę, `Ctrl+Shift+B` nazwaną. Spotify ma własną oś czasu i zapis zakładek hosta. Radio na żywo nie ma stałego czasu utworu, więc `J` jest wyłączone. Polecenia pozostają w menu, pomocy i palecie przez wspólny model; opis polecenia TIDAL uzupełniono o najwyższą dostępną jakość do Max. Nie wprowadzono nowego skrótu.
4. **Historia i kolejka.** `Ctrl+H` w Spotify zapisuje dopiero potwierdzony start, a lista zawiera własne wpisy. W TIDAL `Ctrl+H` istniało, ale start wewnętrzny nie zapisywał wpisu; naprawiono to. Enter na wpisie TIDAL nadal prowadzi do oryginalnego TIDAL zgodnie z dotychczasową semantyką, a polecenie kontekstowe może odtworzyć utwór wewnątrz AMC. Kolejka prywatna TIDAL i Page Up/Down działają; start z widoku kolejki ustawia ją jako źródło natywnego odtwarzania.
5. **Początek utworu po Page Up/Down.** Znaleziono czterosekundowe narastanie głośności w ogólnym torze lokalnym. Dla tymczasowego strumienia TIDAL wyłączono to narastanie i wygaszanie poprzedniego strumienia, aby pierwsze próbki nowego utworu były słyszalne. Wrażenie użytkownika wymaga jeszcze odsłuchu w nowej instalacji; Spotify używa osobnego przeglądarkowego SDK i tej zmiany nie dziedziczy.
6. **Spotify Lossless.** AMC używa oficjalnego Web Playback SDK w izolowanym Chrome, a nie toru PCM/FFmpeg TIDAL. Według Spotify web player Premium używa AAC 256 kb/s i nie udostępnia przełącznika jakości. Oficjalny Spotify Soloist dla Windows potrafi Lossless, ale sam wysyła audio do WASAPI, udostępnia sterowanie przez WebSocket, wymaga osobnego klucza użytkownika i nie może być redystrybuowany w instalatorze AMC. Zatem nie można po prostu skierować jego Lossless do bieżącego wewnętrznego toru AMC. Integracja sterowania Soloist byłaby osobnym projektem i wymagałaby jego instalacji oraz autoryzacji przez użytkownika.

## Testy i stan

Źródła zmienione, lecz w chwili utworzenia rekordu budowa alpha.473, instalacja, wydanie GitHub i ręczny odsłuch/NVDA są jeszcze przed weryfikacją. Szersza regresja Python: 120 testów i 6 podtestów zaliczonych. Dwa zestawy protokołu hosta `--tidal-native --transport-routing`: zaliczone. Kompilacja LiteHost Release: bez ostrzeżeń i błędów. Cichy test prawdziwego hosta z syntetycznym FLAC (start, przewijanie, pauza, wybór wyjścia): zaliczony. Realny strumień Max: FLAC 24 bit/192 kHz, dekodowanie jednej sekundy PCM od początku i od pozycji 30 s zaliczone. Nie zapisywano podpisanych adresów ani tokenów w raportach.

## Wersja i commit

Planowana wersja `0.1.0-alpha.473`. Commit, budowę, instalację i publikację uzupełnić po faktycznym wyniku każdego kroku. Nie utożsamiać zaliczonych testów ze sprawdzonym odsłuchem NVDA.


## Aktualizacja stanu — 5 listopada 2026, po publikacji

Źródła alpha.473 z commitu `578d39fd75bc803b1052d4b350cb0402885fa664` zbudowano i opublikowano jako wstępne wydanie `test-2026.11.05.1414`. Zdalny załącznik instalatora został odczytowo zweryfikowany. Instalacji alpha.473 nie wykonywano, ponieważ użytkownik rozszerzył zakres o zbiorczą konfigurację odczytu list. Ostatnią zainstalowaną wersją pozostaje alpha.472. Korekta użytkownika z późniejszej godziny tego samego dnia zastępuje zdanie w punkcie 2 o komunikacie TIDAL o niedostępnym formacie: w alpha.474 brakujące parametry mają być pomijane. Szczegóły nowego zadania: `LIST-20261105-konfiguracja-odczytu.md`.
