# Lokalny Sonos w AMC Test — instrukcja i lista zmian (szkic)

Status: szkic do korekty użytkownika po próbie z prawdziwym głośnikiem.
Nie publikować jako instrukcji wersji Public przed odbiorem sprzętowym i NVDA.

## Szkic instrukcji użytkownika

### Co jest potrzebne

- komputer z AMC Test i głośnik Sonos muszą być połączone z tą samą siecią
  lokalną;
- sieć Windows powinna zezwalać na wykrywanie urządzeń;
- lokalny dostęp do Sonosa przez UPnP nie może być wyłączony.

Konto Sonos, logowanie w przeglądarce i broker internetowy nie są potrzebne.
AMC nie prosi o hasło do Sonosa.

### Pierwsze użycie

1. Przejdź do sesji Sonos skrótem `Ctrl+6`.
2. AMC automatycznie wyszuka głośniki i grupy w sieci lokalnej.
3. Strzałkami wybierz grupę. NVDA powinno odczytać nazwy głośników, ich liczbę
   i stan odtwarzania.
4. Naciśnij Enter, aby otworzyć sterowanie wybraną grupą.

Jeżeli lista się zmieniła albo głośnik został włączony później, naciśnij
`Ctrl+F5`, aby ponowić wyszukiwanie.

### Biblioteka i odtwarzanie

- `Ctrl+L` otwiera Bibliotekę Sonosa, w tym `Moje stacje` zapisane w AMC.
- `Ctrl+U` otwiera Ulubione Sonos.
- `Ctrl+P` otwiera Playlisty Sonos.
- Enter uruchamia wybraną stację, ulubiony element albo playlistę na aktywnej
  grupie.
- W odtwarzaczu działają wspólne polecenia AMC do pauzy i odtwarzania,
  poprzedniego i następnego materiału, głośności, wyciszenia oraz skoku w
  czasie, o ile bieżący materiał Sonosa obsługuje daną operację.

### Gdy AMC nie znajduje Sonosa

1. Sprawdź, czy komputer i Sonos są w tej samej sieci oraz tej samej podsieci.
2. Ustaw profil bieżącej sieci Windows jako prywatny i zezwól na wykrywanie
   urządzeń.
3. Sprawdź w ustawieniach Sonosa, czy lokalne sterowanie UPnP jest dozwolone.
4. Wróć do AMC i naciśnij `Ctrl+F5`.

Automatyczne wykrywanie nie obejmuje obecnie ręcznego adresu IP, innej podsieci
ani sterowania przez VPN. Sterowanie spoza domu pozostaje osobnym, przyszłym
wariantem chmurowym.

## Szkic changelogu

### Dodano

- lokalne wykrywanie grup Sonos przez SSDP/UPnP bez konta i OAuth;
- bezpośrednie sterowanie odtwarzaniem, głośnością i wyciszeniem;
- lokalny odczyt Ulubionych Sonos i Playlist Sonos;
- uruchamianie `Moich stacji` AMC na wybranej grupie;
- użytkowe komunikaty dla braku urządzeń i utraty grupy;
- przypięty i weryfikowany po sumach zestaw SoCo 0.32.2.

### Zmieniono

- `Ctrl+F5` wyszukuje lokalne grupy zamiast rozpoczynać logowanie;
- konfiguracja Sonosa w Menedżerze sesji otwiera wybór lokalnych głośników;
- stan niezainicjowanej sesji brzmi `Wymaga wyszukania głośników`;
- ekran logowania i broker pozostają wyłącznie zapasową ścieżką w kodzie.

### Dostępność

- etykiety grup składają się wyłącznie z nazw nadanych głośnikom;
- identyfikatory techniczne, adresy IP i reprezentacje obiektów nie są
  przekazywane jako tekst kontrolek ani pozycji listy;
- użyto istniejących poleceń i skrótów, więc menu, pomoc i paleta pozostają
  zgodne ze wspólnym modelem możliwości AMC.

### Zakres wydania

Korekta użytkownika z 14.11.2026: lokalny Sonos ma być dostępny również w AMC Public. Zastępuje to poniższe wcześniejsze ograniczenie do Test; historyczny opis pozostaje zapisem poprzedniego kandydata. Szczegóły bieżącego wydania: `RELEASE-20261114-public-sonos-playlisty.md`.

- funkcja jest włączona wyłącznie w AMC Test;
- AMC Public nie zawiera modułu ani zależności lokalnego Sonosa;
- kod, kompilacja, paczka i testy automatyczne są gotowe;
- instalacja, próba z głośnikiem, ręczny odbiór NVDA i publikacja nie zostały
  jeszcze wykonane.

Kandydat roboczy: `ab8f6843`, oparty na commicie
`19d08fd60da97736daa933a6cfc0d2ae901827a0`, ze stanem `sourceDirty=true`.
