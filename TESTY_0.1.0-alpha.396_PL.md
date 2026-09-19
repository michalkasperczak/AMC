# AMC 0.1.0-alpha.396 — jedna sesja Spotify, podcasty i tempo TimeShift

## Spotify — próba użytkowa

1. Otwórz listę sesji przez Ctrl+0. Spotify powinno występować raz. Numery pozostałych sesji nie powinny się przesunąć.
2. W Ustawieniach jest wybór odtwarzacza Spotify: Librespot albo SDK. Zmiana obowiązuje po ponownym uruchomieniu AMC. Nie trzeba zmieniać działającego Librespot.
3. W Spotify naciśnij Ctrl+Alt+O, aby otworzyć zapisane podcasty. Enter otwiera listę odcinków wybranego podcastu. Na odcinku prawa strzałka pozwala wrócić do jego podcastu, jeżeli Spotify przekazało to powiązanie.
4. Alt+D otwiera opis podcastu lub odcinka. Tekst można czytać strzałkami. Escape zamyka opis i przywraca fokus na liście.
5. W wynikach wyszukiwania odcinka Spotify menu kontekstowe zawiera „Przejdź do podcastu”, gdy dostępne jest powiązanie.
6. Sprawdź kolejkę przed i po ponownym uruchomieniu. Pozycja usunięta z kolejki nie powinna wracać. Zmiana „Odtwórz jako następne” na zwykłą kolejkę powinna pozostać zapisana. Pobranie biblioteki nie powinno usuwać kolejki.
7. NVDA+End powinno czytać pasek stanu bez powtarzania całego tekstu.

Testy automatyczne i próby interfejsu nie zastępują odsłuchu rzeczywistego odcinka na koncie Spotify. W tej wersji usunięto blokadę odcinków w obu adapterach, ale pełnego odtwarzania podcastów na koncie użytkownika jeszcze nie potwierdzono.

## Tempo bufora transmisji — próba użytkowa

1. Włącz stację radiową, poczekaj na zgromadzenie fragmentu transmisji i przejdź do odtwarzacza klawiszem F6. Cofnij się strzałką w lewo, aby słuchać z bufora, a nie na żywo.
2. Shift+kropka przyspiesza, Shift+przecinek zwalnia, Ctrl+kropka przywraca normalne tempo. Porównaj dźwięk, nie tylko wypowiedziany numer. Wysokość głosu nie powinna zmieniać się wraz z tempem.
3. W buforze wstrzymaj odsłuch Spacją, zmień tempo, a następnie wznów. Potwierdzenie ma być poprawne już podczas pauzy.
4. Gdy przyspieszone odtwarzanie zbliży się do transmisji na żywo, AMC wraca do normalnego tempa i jednokrotnie o tym informuje. Nie powinno później samo ponownie przyspieszać.
5. End od razu przywraca transmisję na żywo i normalne tempo. Próba przyspieszenia na żywo powinna wyjaśnić, że regulacja działa w buforze.
6. Po przyspieszeniu zmień stację i zamknij program w zwykły sposób, jeżeli nie nagrywasz. Zwykłe odtwarzanie ma pozostać dostępne.

## Zakres zmiany

Regulacja tempa działa za buforem transmisji. Nie przebudowano pobierania i dekodowania radia ani YouTube.

Spotify ma jedną sesję i wspólną kolejkę; silnik wybiera się w Ustawieniach. Migracja zachowuje archiwum danych dawnej sesji Librespot. Nie usuwa poświadczeń. Binarka hosta Librespot i dodatek NVDA 0.3.2 pozostają niezmienione.

## Wykonane pomiary

Pełna regresja po połączeniu zmian: 141 testów Windows, cały zestaw Core zakończony kodem 0 i 17 testów dodatku NVDA. Sprawdzono rzeczywisty zapis oraz odczyt pliku i baz SQLite, dwa uruchomienia głównego okna, zachowanie ustawień i zmiany kolejki wykonane poleceniami aplikacji.

Testy tempa odróżniają zaakceptowane ustawienie od rzeczywistego zużycia próbek. Obejmują PCM16 i float32, neutralne 1x, zmianę w pauzie, End/Seek, ciągłe uzupełnianie bufora, mały zapas oraz zablokowany odczyt równoczesny ze zmianą tempa i zamknięciem. Osobno mierzone jest 20 s wyjścia po 3 s rozgrzewki.

Przeprowadzono również próby publicznych strumieni AAC i MP3 przez istniejący MediaFoundationReader oraz rzeczywiste wyjście WASAPI. Próbę sprzętowego renderera celowo wyciszono; potwierdza ona pracę wyjścia i tempo konsumpcji, nie subiektywną ocenę brzmienia.

Interfejs i skróty sprawdzono żywym NVDA na rzeczywistych oknach z jawnymi danymi próbnymi. Dla paska stanu i tempa sprawdzono także zapis z podglądu mowy. Próbki interfejsu podcastów nie korzystały z konta użytkownika.
