# AMC 0.1.0-alpha.399 — co sprawdzić

To wydanie obejmuje sprawdzony etap: szybsze przygotowanie zapisu dużej biblioteki, zachowanie Enter na wynikach wyszukiwania, widok wykonawcy i zachowanie priorytetu podczas zapisu kolejki.

Dalsze poprawki presetów, menu i opcji sesji są oddzielnym etapem. Nie należy szukać ich w wersji 399.

## Praca z nagraniem

Otwórz zwykłe nagranie, przejdź do następnego pliku i wróć do listy. Zwróć uwagę na płynność oraz reakcję czytnika.

Na kopii nagrania sprawdź ustawianie początku i końca fragmentu. Upewnij się, że program ogłasza właściwe granice i pamięta je po ponownym otwarciu.

W próbach porównawczych na kopii biblioteki czas obsługi Escape wyniósł około 1,88 sekundy przed poprawką i 0,24 sekundy po niej. Dla polecenia zmiany pliku było to około 1,79 i 0,23 sekundy. Te pomiary dotyczą obsługi w programie na maszynie testowej, nie czasu mowy NVDA ani gwarantowanej szybkości każdego komputera.

## Enter i Biblioteka

Wyszukaj materiał, którego nie masz w Bibliotece. Przy domyślnym ustawieniu naciśnięcie Enter powinno go otworzyć lub odtworzyć, ale samo nie powinno go dodawać do Biblioteki.

W Ustawieniach znajdź „Zachowanie klawisza Enter na wyniku wyszukiwania”. Czytnik powinien odczytać nazwę, bieżący wybór i opis. Możesz wybrać automatyczne dodawanie do Biblioteki albo otwieranie bez dodawania.

Po zapisaniu ustawienia i ponownym uruchomieniu programu wybór powinien pozostać. Anulowanie zmiany nie powinno zapisywać nowej wartości.

Jawne dodanie do Biblioteki nadal powinno działać niezależnie od tego ustawienia. Otwarcie już zapisanego materiału nie powinno usuwać go z Biblioteki.

Sama pauza odtwarzanego wyniku lub dodanie do kolejki nie powinny dopisywać materiału za pośrednictwem opcji przeznaczonej dla Enter.

## Wykonawcy

Sprawdź osobne wejścia do albumów i utworów wybranego wykonawcy oraz powrót na tego wykonawcę. Czytnik powinien rozróżniać nazwy sekcji i wskazywać właściwy element po powrocie.

W Spotify, gdy są dalsze wyniki utworów, użyj pozycji wczytania kolejnych. Nie zakładamy, że wynik wyszukiwania stanowi kompletną dyskografię wykonawcy.

## Kolejka

Jeżeli kolejka zawiera kilka wystąpień tego samego utworu, sprawdź zachowanie wskazania „Następne” po zapisaniu i odtworzeniu kolejki. Ta poprawka nie przebudowuje sterowania zewnętrznym programem TIDAL.

## Zakres weryfikacji wydania

Przed przygotowaniem instalatora przeszły testy Core i wszystkie 148 testów Windows. Na Windows przeszło też 17 testów dodatku NVDA.

Ustawienie Enter i powrót z sekcji wykonawcy sprawdzono w rzeczywistym oknie z żywym NVDA. W 48 właściwych próbach nawigacji sprawdzono prawdziwe odtwarzanie wyciszonych kopii plików, poprawne przejście do następnego nagrania, przewijanie i powrót do listy.

Kontrola danych potwierdziła zachowanie całego zbioru pozycji biblioteki oraz znaczników i zapamiętanych pozycji pozostałych nagrań.

Nie zmieniono dodatku NVDA 0.3.2 ani silnika Librespot. Instalator zawiera wymagane środowisko uruchomieniowe programu.
