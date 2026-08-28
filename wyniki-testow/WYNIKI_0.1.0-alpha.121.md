# AMC 0.1.0-alpha.121 — testy

Wystarczy opisać zauważony problem. Nie trzeba wpisywać przed każdym punktem wariantu „OK” albo „błąd”.

1. Wyszukaj i uruchom **Polskie Radio — Chopin (Radio Chopin) (AAC+)**. AMC powinien użyć zgodnego MP3 i rozpocząć odtwarzanie. Pozostaw stację na co najmniej pół minuty.

2. Uruchom **Polskie Radio Program 3**. Jeżeli oba wejścia nadawcy nadal zwracają niedostępność, AMC powinien po ograniczonym oczekiwaniu podać zwykły komunikat o nieudanym odtworzeniu, bez zawieszenia i bez fałszywego komunikatu, że stacja zaczęła grać. Jeżeli serwer 8904 wróci, Trójka powinna rozpocząć odtwarzanie przez MP3.

3. Ponownie uruchom Jedynkę. Komunikat o rozpoczęciu powinien pojawić się dopiero wraz z rzeczywistym dźwiękiem, bez dodatkowej próby użytkownika.

4. Szybko przełącz: Jedynka, Trójka, Chopin, PR24. Ostatecznie ma grać ostatnio wybrana osiągalna stacja. Nieudana Trójka nie może później zatrzymać Chopina ani zgłosić spóźnionego błędu.

5. Wyrywkowo sprawdź Program 2, Czwórkę i Radio Emaus, aby wykluczyć regresję MP3 oraz OGG/Vorbis.
