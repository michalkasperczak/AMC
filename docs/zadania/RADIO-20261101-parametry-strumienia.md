# RADIO-20261101 — parametry bieżącej stacji radiowej

Stan: źródła, testy, pakiet i wydanie GitHub gotowe; instalacja oczekuje na koniec nagrywania. Wersja: AMC Test 0.1.0-alpha.463.

## Zgłoszenie i decyzja

1.11.2026 użytkownik zgłosił, że NVDA+End dla Radia Poznań mówi 48 kHz i nazwę stacji, ale pomija bitrate i kodek, na przykład 128 AAC lub 192 kb/s. Odczyt ma podawać rzeczywistą wartość, kiedy silnik potrafi ją ustalić. Ta decyzja uzupełnia wcześniejszy projekt paska stanu wydania alpha.461; dotychczasowy sam odczyt częstotliwości nie był wystarczający.

## Materiał do dokumentacji użytkownika

Podczas odtwarzania stacji w sesji Radio naciśnij NVDA+End, aby odczytać pasek stanu. Odczyt zawiera nazwę stacji, wykryty bitrate w kb/s, kodek, częstotliwość próbkowania i podaną przez stację nazwę bieżącej audycji lub utworu. Alt+D podaje ten sam zestaw. Parametry mogą pojawić się kilka sekund po rozpoczęciu odtwarzania. Gdy strumień nie przekazuje wiarygodnego bitrate'u lub kodeka, AMC pomija brakującą wartość; nie podaje liczby na podstawie samej częstotliwości. Wartość wywnioskowana z adresu strumienia ma przedrostek „około”. Pozostałe sesje i oba profile wydania zachowują dotychczasowe skróty.

## Uzasadnienie i decyzje

Host pobierał parametry z dekodera przy starcie, lecz część strumieni ujawnia tylko częstotliwość. Co ważne, zapisana stacja „Poznań” ma już w profilu 128 kb/s, AAC i 48 kHz, ale polecenie odtwarzania przekazywało tylko nazwę i adres. Odtwarzanie przekazuje teraz zapisane parametry tej samej stacji, po sprawdzeniu identyfikatora i adresu. Gdy brakuje pól, host uzupełnia je ze znanego sondowania strumienia, bez blokowania interfejsu. Wynik spóźniony po zmianie stacji jest odrzucany. Tekst Radia zawiera także kodek, a wartość szacowana jest jawnie oznaczona.

## Testy i wynik

Kompilacja hosta z 1.11.2026: zaliczona, zero błędów i ostrzeżeń. Test formatu dla 128 AAC przy 48 kHz, szacowanego 192 MP3 oraz samej częstotliwości: zaliczony w zestawie Core. Pełny zestaw uruchomiony w sandboxie zgłosił błędy dostępu do katalogów tymczasowych; powtórka z właściwymi uprawnieniami zakończyła się kodem 0. Po uzupełnieniu przekazywania zapisanych parametrów stacji: 155 testów odczytu Radia, klienta hosta, skrótów, menu i palety poleceń zaliczonych; osobny zestaw cache informacji: 55 zaliczonych, 2 pominięte warunkowo. Zbudowano pakiet i instalator; pełna integralność oraz kontrolowany start przeszły. Prerelease na GitHubie zdalnie potwierdzony. Rzeczywisty odczyt NVDA dla Radia Poznań wymaga bezpiecznego uruchomienia nowej instalacji i ręcznego odsłuchu.

## Znane ograniczenia

Nie każdy serwer udostępnia wiarygodny bitrate lub kodek. AMC nie wylicza bitrate'u z 48 kHz. Sondowanie brakujących danych może zająć kilkanaście sekund. Podczas nagrania nie należy zastępować działającej instalacji.

## Wersja i commit

Wydanie pochodzi z commitu `ae8ae822c0a1c734c382aebe270bbb17459769e1`. Instalator `AMC-Test-2026-11-01-1913.exe`, 133658858 bajtów, SHA-256 `d0c4b65e86c926bd50e39c3d63ead377bbac61b6d4b355a83689f4bb0835762d`. GitHub prerelease `test-2026.11.01.1913`: commit, nazwa, rozmiar i digest zasobu potwierdzone. Próba lokalnej aktualizacji 1.11 zakończyła się kodem 3: o 19:13 rozpoczęło się nagrywanie „Katedra”, którego wcześniejsza kontrola harmonogramu nie wykazała. Aktualizator odmówił zamknięcia AMC; plików instalacji nie zastąpiono. Lokalnie pozostaje alpha.462. Dokładny raport: `D:\Projekty Codex\AMC aktualizacje\AMC Test 0.1.0-alpha.463 Radio bitrate 2026-11-01\stan-aktualizacji.md`.
