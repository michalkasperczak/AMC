# AMC Test i Public — wspólna aktualizacja alpha.488

Status: **W trakcie**, 9 listopada 2026. Użytkownik zlecił zbudowanie i publikację obu profili, instalację tylko Public oraz pozostawienie nagrywającego Test do późniejszej aktualizacji przez użytkownika.

## Zakres użytkowy

- Lokalne MP3 z głośnymi szczytami: automatyczna ścieżka FFmpeg float ogranicza szczyty przed wyjściem audio, jeśli składnik FFmpeg jest dostępny. Utrata urządzenia wyjściowego nie uruchamia już odzyskiwania dekodera MP3.
- Zwykła kolejka plików (`Ctrl+Q`): osobny zapis dla AMC Test i Public, odtworzenie po restarcie, zachowanie pustej kolejki i brakującego pliku. Gdy miejsce zapisu jest niedostępne, AMC działa z kolejką tymczasową i informuje o tym jednokrotnie.
- Historia TIDAL i Spotify (`Ctrl+H`): prawa strzałka pokazuje nazwane albumy, wykonawców i podcasty. `Ctrl+C` kopiuje nazwę, `Ctrl+Shift+C` dokładny link, jeśli jest dostępny. Podobne przejście jest dostępne dla historii odcinków Podcastów i YouTube.
- Rozpoznane utwory (`Ctrl+Alt+S`): prawa strzałka podaje nazwany utwór najpierw w usłudze domyślnej, następnie w drugiej oraz pozycję kopiowania Apple Music. Album pojawia się w krótkiej liście tylko przy znanej nazwie.

## Uzasadnienie i decyzje

Test i Public powstają z tego samego commitu i numeru technicznego. Instalacja Test jest odłożona ze względu na aktywne nagrywanie. Spotify Soloist i żądanie Lossless pozostają z wydania alpha.487; ta aktualizacja nie jest potwierdzeniem rzeczywistej jakości strumienia.

## Testy i wynik

- Na wspólnym drzewie źródeł: 153 celowane testy Python dla kolejki, historii, rozpoznawania, Spotify, menu i podcastów zaliczone; 123 dodatkowe testy nawigacji, profili i instalatora zaliczone w izolowanym środowisku wxPython.
- Celowane .NET `QueuePersistenceTests` oraz `--mp3-peaks-only` zaliczone. Dla kolejki sprawdzono także cichy start rzeczywistego hosta przy niedostępnym katalogu.
- Kompilacja paczek, kontrola instalatorów, publikacja, instalacja Public i odbiór NVDA: do wpisania po wykonaniu. Nie używać tego dokumentu jako dowodu tych etapów.

## Znane ograniczenia

FFmpeg pozostaje składnikiem opcjonalnym: bez niego przester problematycznego MP3 może pozostać. Nie sprawdzono odsłuchu na pliku Huberta, fizycznego odłączenia słuchawek ani odczytu nowej listy w NVDA. Test nadal nagrywa; nie zamykać ani nie instalować.

## Wersja i commit

Planowana wersja techniczna obu profili: `0.1.0-alpha.488`. Commit źródeł i tożsamości instalatorów zostaną dopisane po ich weryfikacji.
