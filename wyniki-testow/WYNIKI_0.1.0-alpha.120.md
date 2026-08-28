# AMC 0.1.0-alpha.120 — testy

Wystarczy opisać zauważony problem. Nie trzeba wpisywać przed każdym punktem wariantu „OK” albo „błąd”.

1. W sesji **Radio internetowe** uruchom **Polskie Radio Jedynka** i pozostaw je włączone co najmniej przez minutę. Stacja nie może zakończyć się tuż po komunikacie o rozpoczęciu odbioru.

2. Kolejno uruchom **Polskie Radio Program 2**, **Polskie Radio — Czwórka** i **Polskie Radio 24**. Każdej stacji posłuchaj kilkanaście sekund. Zmiana stacji powinna być płynna i nie może powodować komunikatu o przerwanym połączeniu po ułamku sekundy.

3. Przełącz kilka razy Jedynkę i PR24 w odstępie około jednej sekundy. Ostatecznie ma grać ostatnio wybrana stacja; wcześniejsza próba nie może powrócić ani zgłosić spóźnionego błędu.

4. Podczas działania jednej z tych stacji sprawdź pauzę, powrót na żywo klawiszem `End` oraz regulację głośności. Tor timeshiftu i fokus odtwarzacza powinny zachowywać się jak wcześniej.

5. Wyrywkowo uruchom jedną stację regionalną działającą przez dekoder systemowy, na przykład Polskie Radio Gdańsk, oraz Radio Emaus. Poprawka MP3 nie może pogorszyć zwykłych strumieni ani OGG/Vorbis.

6. Jeżeli problem nadal wystąpi, podaj dokładną nazwę stacji i przybliżoną godzinę. Log rozróżni prawdziwe zerwanie połączenia od błędu dekodowania ramki.
