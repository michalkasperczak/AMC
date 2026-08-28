# AMC 0.1.0-alpha.122 — testy

Wystarczy opisać zauważony problem. Nie trzeba wpisywać przed każdym punktem wariantu „OK” albo „błąd”.

1. Wyszukaj i uruchom **Polskie Radio — Chopin (Radio Chopin) (AAC+)**. AMC powinien użyć działającego wariantu MP3 192 kb/s. Pozostaw stację na co najmniej pół minuty.

2. Uruchom **Polskie Radio Program 3**. Jeżeli serwery 8954 i 8904 nadal odpowiadają niedostępnością, program powinien zakończyć próbę zwykłym komunikatem, bez zawieszenia i bez fałszywej informacji o rozpoczęciu. Po powrocie portu 8904 AMC użyje MP3 automatycznie.

3. Uruchom Jedynkę. Komunikat o rozpoczęciu powinien pojawić się wraz z rzeczywistym dźwiękiem, bez potrzeby ponawiania próby.

4. Szybko przełącz kolejno: Jedynka, Trójka, Chopin, PR24, Czwórka. Ostatecznie ma grać ostatnio wybrana osiągalna stacja. Nieudana lub anulowana próba nie może później zatrzymać nowej stacji ani wywołać spóźnionego komunikatu.

5. Zamknij AMC podczas działania radia, uruchom ponownie i sprawdź inną stację. Zamykanie poprzedniego toru nie może utrudnić następnego uruchomienia.

6. Wyrywkowo sprawdź Radio Emaus oraz jedną regionalną stację Polskiego Radia, aby wykluczyć regresję OGG/Vorbis i dekodera systemowego.
