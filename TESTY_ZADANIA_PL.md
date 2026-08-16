# Zadania testowe AMC

- Numer zestawu: `AMC-TEST-031`
- Tytuł zestawu: Ustawienia w palecie poleceń
- Wersja programu: `0.1.0-alpha.31`
- Utworzono: 2026-08-16 23:38, Europe/Warsaw
- Plik wyników: `wyniki-testow/WYNIKI_2026-08-16_2338_0.1.0-alpha.31.md`

W pliku wyników po dwukropku wpisz krótko, co się wydarzyło. Jeżeli wszystko działa, wystarczy `OK`. Nie trzeba przed każdym zadaniem wybierać wariantu odpowiedzi.

## AMC-031-01 — Widoczność ustawień

1. Otwórz paletę przez `Ctrl+Shift+K`.
2. Wpisz kolejno fragmenty `ustawienia`, `prefiks`, `szablony komunikatow` i `pelna kopie`.
3. Przejdź strzałką w dół po wynikach.

Oczekiwane:

- paleta znajduje ustawienia bez wpisywania polskich znaków;
- każdy wiersz ma zwykłą, zrozumiałą nazwę;
- nie pojawiają się identyfikatory techniczne ani zapis obiektu.

## AMC-031-02 — Przełączenie komunikatów

1. W palecie znajdź `Komunikaty dostępności` i sprawdź podany stan.
2. Wykonaj polecenie Enterem, ponownie otwórz paletę i odszukaj ten sam wpis.
3. Wykonaj je jeszcze raz, aby na końcu komunikaty były włączone.

Oczekiwane:

- wiersz jednoznacznie mówi `włączone. Enter: wyłącz` albo `wyłączone. Enter: włącz`;
- po każdej zmianie słychać krótkie potwierdzenie, również podczas wyłączania komunikatów;
- ponowne otwarcie palety pokazuje nowy stan.

## AMC-031-03 — Przełączenie szczegółowych podpowiedzi

1. W palecie znajdź `Szczegółowe podpowiedzi klawiatury`.
2. Przełącz opcję i sprawdź jej nowy stan po ponownym otwarciu palety.
3. Otwórz wyszukiwanie przez `Ctrl+F` i sprawdź opis pola; następnie przywróć preferowany stan opcji.

Oczekiwane:

- zmiana jest natychmiast zapisywana i potwierdzana;
- etykieta w palecie odpowiada aktualnemu stanowi;
- opis pola wyszukiwania zmienia szczegółowość zgodnie z ustawieniem.

## AMC-031-04 — Dokładne miejsce w ustawieniach

1. W palecie wybierz `Ustawienia: globalny prefiks`.
2. Zamknij ustawienia bez zapisywania.
3. Powtórz próbę dla `Ustawienia: kolejność informacji na listach` oraz `Ustawienia: szablony komunikatów`.

Oczekiwane:

- otwiera się właściwa karta ustawień;
- fokus trafia odpowiednio do pola prefiksu, listy kolejności i listy zdarzeń komunikatów;
- samo wybranie wpisu z palety niczego nie zmienia.

## AMC-031-05 — Bezpieczne operacje importu i eksportu

1. W palecie wybierz `Ustawienia: importuj konfigurację`.
2. Zamknij ustawienia i powtórz próbę dla eksportu pełnej kopii.

Oczekiwane:

- paleta tylko otwiera właściwą kartę i ustawia fokus na odpowiednim przycisku;
- okno wyboru pliku nie otwiera się samoczynnie;
- import ani eksport nie zaczyna się bez kolejnego świadomego Entera na przycisku.

## AMC-031-06 — Ustawienia planowane i regresja palety

1. W palecie odszukaj `aktualizacje` i wykonaj znalezione polecenie.
2. Zamknij ustawienia i wywołaj z palety dowolne zwykłe polecenia, na przykład Ulubione, Kolejka i Informacje.

Oczekiwane:

- otwiera się karta `Aktualizacje (planowane)`, a jej nieaktywne opcje nie są przełączane;
- dotychczasowe polecenia palety nadal działają;
- po zamknięciu okna pomocniczego fokus wraca do właściwego elementu głównej listy.
