# RADIO-20261101 — parametry bieżącej stacji radiowej

Stan: źródła i skupione testy gotowe; końcowa kompilacja, pakiet, wydanie i instalacja oczekują na koniec nagrań. Wersja docelowa: AMC Test 0.1.0-alpha.463.

## Zgłoszenie i decyzja

1.11.2026 użytkownik zgłosił, że NVDA+End dla Radia Poznań mówi 48 kHz i nazwę stacji, ale pomija bitrate i kodek, na przykład 128 AAC lub 192 kb/s. Odczyt ma podawać rzeczywistą wartość, kiedy silnik potrafi ją ustalić. Ta decyzja uzupełnia wcześniejszy projekt paska stanu wydania alpha.461; dotychczasowy sam odczyt częstotliwości nie był wystarczający.

## Materiał do dokumentacji użytkownika

Podczas odtwarzania stacji w sesji Radio naciśnij NVDA+End, aby odczytać pasek stanu. Odczyt zawiera nazwę stacji, wykryty bitrate w kb/s, kodek, częstotliwość próbkowania i podaną przez stację nazwę bieżącej audycji lub utworu. Alt+D podaje ten sam zestaw. Parametry mogą pojawić się kilka sekund po rozpoczęciu odtwarzania. Gdy strumień nie przekazuje wiarygodnego bitrate'u lub kodeka, AMC pomija brakującą wartość; nie podaje liczby na podstawie samej częstotliwości. Wartość wywnioskowana z adresu strumienia ma przedrostek „około”. Pozostałe sesje i oba profile wydania zachowują dotychczasowe skróty.

## Uzasadnienie i decyzje

Host pobierał parametry z dekodera przy starcie, lecz część strumieni ujawnia tylko częstotliwość. Co ważne, zapisana stacja „Poznań” ma już w profilu 128 kb/s, AAC i 48 kHz, ale polecenie odtwarzania przekazywało tylko nazwę i adres. Odtwarzanie przekazuje teraz zapisane parametry tej samej stacji, po sprawdzeniu identyfikatora i adresu. Gdy brakuje pól, host uzupełnia je ze znanego sondowania strumienia, bez blokowania interfejsu. Wynik spóźniony po zmianie stacji jest odrzucany. Tekst Radia zawiera także kodek, a wartość szacowana jest jawnie oznaczona.

## Testy i wynik

Kompilacja hosta z 1.11.2026 dla pierwszego etapu: zaliczona, zero błędów i ostrzeżeń. Test formatu dla 128 AAC przy 48 kHz, szacowanego 192 MP3 oraz samej częstotliwości: zaliczony w zestawie Core. Pełny zestaw uruchomiony w sandboxie zgłosił błędy dostępu do katalogów tymczasowych; powtórka z właściwymi uprawnieniami zakończyła się kodem 0. Po uzupełnieniu przekazywania zapisanych parametrów stacji: 155 testów odczytu Radia, klienta hosta, skrótów, menu i palety poleceń zaliczonych; osobny zestaw cache informacji: 55 zaliczonych, 2 pominięte warunkowo. Ponowna kompilacja hosta jest odłożona do końca zaplanowanych nagrań. Rzeczywisty odczyt NVDA dla Radia Poznań wymaga bezpiecznego uruchomienia nowej instalacji i ręcznego odsłuchu.

## Znane ograniczenia

Nie każdy serwer udostępnia wiarygodny bitrate lub kodek. AMC nie wylicza bitrate'u z 48 kHz. Sondowanie brakujących danych może zająć kilkanaście sekund. Podczas nagrania nie należy zastępować działającej instalacji.

## Wersja i commit

Zmiana formattera i sondowania: `cc7e3fd91588575b284c5549c92db7ad81947a65`. Przekazanie zapisanych danych stacji do hosta: `d482285311f9f4b47e4847ed4f4622a8761a7ccb`. Oba commity są zweryfikowane na gałęzi `codex/amc-test-title-20261031` repozytorium `michalkasperczak/AMC`. Pakiet, instalacja i wydanie GitHub nie zostały jeszcze wykonane; push źródeł nie jest wydaniem.
