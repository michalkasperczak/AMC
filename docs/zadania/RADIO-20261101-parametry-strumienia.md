# RADIO-20261101 — parametry bieżącej stacji radiowej

Stan: w trakcie. Wersja docelowa: AMC Test 0.1.0-alpha.463.

## Zgłoszenie i decyzja

1.11.2026 użytkownik zgłosił, że NVDA+End dla Radia Poznań mówi 48 kHz i nazwę stacji, ale pomija bitrate i kodek, na przykład 128 AAC lub 192 kb/s. Odczyt ma podawać rzeczywistą wartość, kiedy silnik potrafi ją ustalić. Ta decyzja uzupełnia wcześniejszy projekt paska stanu wydania alpha.461; dotychczasowy sam odczyt częstotliwości nie był wystarczający.

## Materiał do dokumentacji użytkownika

Podczas odtwarzania stacji w sesji Radio naciśnij NVDA+End, aby odczytać pasek stanu. Odczyt zawiera nazwę stacji, wykryty bitrate w kb/s, kodek, częstotliwość próbkowania i podaną przez stację nazwę bieżącej audycji lub utworu. Alt+D podaje ten sam zestaw. Parametry mogą pojawić się kilka sekund po rozpoczęciu odtwarzania. Gdy strumień nie przekazuje wiarygodnego bitrate'u lub kodeka, AMC pomija brakującą wartość; nie podaje liczby na podstawie samej częstotliwości. Wartość wywnioskowana z adresu strumienia ma przedrostek „około”. Pozostałe sesje i oba profile wydania zachowują dotychczasowe skróty.

## Uzasadnienie i decyzje

Host pobierał parametry z dekodera przy starcie, lecz część strumieni ujawnia tylko częstotliwość. Osobne sondowanie metadanych działało w informacji o pozycji, ale nie aktualizowało bieżącej stacji. Po rozpoczęciu odtwarzania host uzupełnia wyłącznie brakujące pola z tego samego sondowania, bez blokowania interfejsu. Wynik spóźniony po zmianie stacji jest odrzucany. Tekst Radia zawiera także kodek, a wartość szacowana jest jawnie oznaczona.

## Testy i wynik

Kompilacja hosta z 1.11.2026: zaliczona, zero błędów i ostrzeżeń. Test formatu dla 128 AAC przy 48 kHz, szacowanego 192 MP3 oraz samej częstotliwości: zaliczony w zestawie Core. Pełny zestaw uruchomiony w sandboxie zgłosił błędy dostępu do katalogów tymczasowych; powtórka z właściwymi uprawnieniami zakończyła się kodem 0. Testy skrótów, menu i palety poleceń: 102 zaliczone. Rzeczywisty odczyt NVDA dla Radia Poznań wymaga bezpiecznego uruchomienia nowej instalacji i ręcznego odsłuchu.

## Znane ograniczenia

Nie każdy serwer udostępnia wiarygodny bitrate lub kodek. AMC nie wylicza bitrate'u z 48 kHz. Sondowanie może zająć do sześciu sekund. Podczas nagrania nie należy zastępować działającej instalacji.

## Wersja i commit

Źródła w przygotowaniu dla 0.1.0-alpha.463. Commit, pakiet, instalacja i wydanie GitHub zostaną zapisane osobno po rzeczywistej weryfikacji.
