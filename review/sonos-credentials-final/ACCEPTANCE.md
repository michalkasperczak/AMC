# Magazyn poświadczeń Sonos — lokalny odbiór zakończony

Przegląd jakości nie wykazał blokad funkcjonalnych. Rodzic sprawdził jego kod i ponowił pomiary, korygując poniższe nadinterpretacje.

## Wyniki rodzica

- Własny odczyt diffu i całej implementacji magazynu.
- Niezmieniona sonda Core recenzenta: faktycznie 56 kontroli, nie 58. Zaliczone 53; trzy pozostałe obalają dawne twierdzenia komentarza o enkoderze, a nie zachowanie zapisu. Jej kod wyjścia 1 nie jest opisany jako pełny zielony przebieg.
- Natywna sonda Windows z poprawionymi kontrolami entropii i zakresu: 42 kontrole, exit 0. Poprawne dane, ta sama treść w każdej próbie; własny losowy TEMP, cleanup potwierdzony. Nie przejęto deklaracji 44 kontroli.
- Po korekcie komentarza i nazwy prywatnej stałej: świeża budowa i 139 kontroli natywnego harnessu, niezależna sonda kontraktu 9/9, własna próba odrzucenia zapisu obcego brokera — wszystkie exit 0.
- Ponowny build Core.SmokeTests z --no-incremental: 0 ostrzeżeń, 0 błędów; logowanie 36/36, odnawianie 58/58.
- SHA źródeł przed i po pomiarach zgodne, brak nowych pozostawionych katalogów tymczasowych.

## Korekta komentarza bez zmiany zachowania

UnsafeRelaxedJsonEscaping nie ogranicza się do cudzysłowu i odwrotnego ukośnika: zmierzono m.in. U+00A0, czyli 2 bajty UTF-8 zapisane jako 6 bajtów JSON. Usunięto fałszywe uzasadnienie i zmieniono nazwę prywatnej stałej na BudgetSizingFactor. Wartość, wzór i wszystkie limity pozostały identyczne.

Porównanie kodu po pominięciu komentarzy i uwzględnieniu tej jednej zmiany nazwy potwierdziło brak pozostałych zmian. Nowe przebiegi wykonano już po korekcie.

Nie przyjęto twierdzenia recenzenta o uniwersalnej granicy „3 × odpowiedź + 512”: zapis zawiera też metadane, w tym skonfigurowany adres brokera. Dowodami są rzeczywiste próby oraz sprawdzanie całego zserializowanego rozmiaru, nie sam ten uproszczony rachunek.

## Korekta dowodu DPAPI

Pierwotne W9/W9b/W9c szyfrowały JSON niezgodny z formatem magazynu. Wynik Invalid nie rozstrzygał, czy zawiodło odszyfrowanie, czy walidacja treści.

Rodzic zaszyfrował własnym P/Invoke ten sam POPRAWNY rekord: prawidłowa entropia i CurrentUser → Success; obca entropia → Invalid bez zmiany pliku; prawidłowa entropia i LocalMachine → Success. Zatem nieprawdziwe było twierdzenie, że sam odczyt odrzuca zakres LocalMachine.

Produkcyjny zapis nadal wywołuje DPAPI z UI_FORBIDDEN i bez flagi LocalMachine, czyli zapisuje w zakresie bieżącego użytkownika. Nie obiecuje ochrony przed procesem tego samego użytkownika, który może sam stworzyć inny poprawny blob. Nie badano przenoszenia między kontami Windows.

W12 recenzenta badał nieudany zapis do innej ścieżki, a nie zachowanie zastępowanego pliku. Zachowanie poprzedniego rekordu przy prawdziwej blokadzie tego samego celu ma odrębny dowód w natywnym harnessie.

## Granice

Bez GUI, kont, domyślnego magazynu, produkcji, publikacji ani instalacji. Brak koordynatora, automatycznego odświeżania i dostępnego okna — to następne etapy. Nie badano wielu procesów zapisujących równocześnie.

Lokalne dowody rodzica: amc_pomoc/sonos-credentials-final-parent/results.json oraz after-comment/{results.json,core-results.json}. Historycznych raportów i dowodów błędów nie nadpisano.
