AMC 0.1.0-alpha.396 — tempo odtwarzania bufora transmisji

Próba użytkowa

1. Włącz stację radiową, poczekaj na zgromadzenie fragmentu transmisji i przejdź do odtwarzacza klawiszem F6. Cofnij się strzałką w lewo, aby słuchać z bufora, a nie na żywo.
2. Shift+kropka przyspiesza, Shift+przecinek zwalnia, Ctrl+kropka przywraca normalne tempo. Porównaj dźwięk, nie tylko wypowiedziany numer. Wysokość głosu nie powinna zmieniać się wraz z tempem.
3. W buforze wstrzymaj odsłuch Spacją, zmień tempo, a następnie wznów. Potwierdzenie ma być poprawne już podczas pauzy.
4. Gdy przyspieszone odtwarzanie zbliży się do transmisji na żywo, AMC wraca do normalnego tempa i jednokrotnie o tym informuje. Nie powinno później samo ponownie przyspieszać.
5. End od razu przywraca transmisję na żywo i normalne tempo. Próba przyspieszenia na żywo powinna wyjaśnić, że regulacja działa w buforze.
6. Po przyspieszeniu zmień stację i zamknij program w zwykły sposób, jeżeli nie nagrywasz. Zwykłe odtwarzanie ma pozostać dostępne.

Zakres zmiany

Zmiany są za buforem transmisji. Nie przebudowano pobierania i dekodowania radia ani YouTube. Nie zmieniono odtwarzaczy Spotify. Dodatek NVDA pozostaje w wersji 0.3.2.

Pomiar techniczny

Testy odróżniają zaakceptowane ustawienie od rzeczywistego zużycia próbek. Obejmują PCM16 i float32, neutralne1x, zmianę w pauzie, End/Seek, ciągłe uzupełnianie bufora, mały zapas oraz zablokowany odczyt równoczesny ze zmianą tempa i zamknięciem. Osobno mierzone jest20s wyjścia po3s rozgrzewki.

Przeprowadzono również próby publicznych strumieni AAC i MP3 przez istniejący MediaFoundationReader oraz rzeczywiste wyjście WASAPI. Próbę sprzętowego renderera celowo wyciszono; potwierdza ona pracę wyjścia i tempo konsumpcji, nie subiektywną ocenę brzmienia.

Interfejs i skróty sprawdzono żywym NVDA na prawdziwym MainWindow z jawnymi danymi próbnymi. Komunikaty odczytano z podglądu mowy czytnika, nie wyłącznie z elementów interfejsu.
