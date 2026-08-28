# AMC 0.1.0-alpha.119 — testy

Wystarczy opisać zauważony problem. Nie trzeba wpisywać przed każdym punktem wariantu „OK” albo „błąd”.

1. W sesji **Radio internetowe** wyszukaj „Radio Emaus” i uruchom stację. Powinna rozpocząć odtwarzanie bez komunikatu o błędzie.

2. Wyszukaj i kolejno sprawdź: **Polskie Radio Program 2**, **Polskie Radio — Czwórka**, **Polskie Radio 24** oraz **Eska Gdańsk**. Wpis AAC albo HLS, dla którego AMC zna zgodny wariant, powinien odtworzyć dźwięk bez zawieszania okna.

3. Podczas szybkiego przechodzenia po kilku stacjach uruchamiaj je kolejno. Stara próba połączenia nie powinna później przejąć odtwarzacza ani fokusu.

4. W wynikach wyszukiwania radia zaznacz trzy sąsiednie stacje przez `Shift+strzałka w dół`, a następnie zmniejsz zakres przez `Shift+strzałka w górę`. NVDA powinien pozostać na ruchomym końcu zaznaczenia.

5. Na zaznaczonych stacjach naciśnij `Ctrl+C` i wklej do edytora. Powinny znaleźć się wszystkie nazwy, każda w osobnym wierszu.

6. Na tych samych stacjach naciśnij `Ctrl+Shift+C` i wklej do edytora. Dla każdej stacji powinny wystąpić dwa wiersze: nazwa, a pod nią adres HTTP lub HTTPS.

7. Powtórz wielokrotne zaznaczanie oraz oba sposoby kopiowania w zwykłym wyszukiwaniu Plików lokalnych. `Ctrl+C` kopiuje wszystkie nazwy; `Ctrl+Shift+C` zachowuje wszystkie pełne ścieżki i pozwala wkleić fizyczne pliki do Eksploratora lub Total Commandera.

8. Jeżeli jakaś stacja nadal nie działa, podaj jej dokładną nazwę z listy. Log zapisze typ awarii, ale nie zapisze kopiowanej treści ani danych ze schowka.
