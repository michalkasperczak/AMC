# RADIO-TRACK-001 — zapis rozpoznanego utworu z radia

Status: **Oczekuje: badanie wykonalności**. Decyzja użytkownika: 10.11.2026. Plan rozpoczęcia: 11.11.2026.

## Ustalenie użytkownika

Sprawdzić, czy pomysł BlindRadio polegający na zapisaniu rozpoznanego utworu jako osobnego pliku da się sensownie zastosować w AMC. Nie zakładać, że samo rozpoznanie ustala dokładny początek i koniec nagrania.

## Stan i zakres pracy

- Istniejące `ECOSYSTEM-001` opisuje w BlindRadio znaczniki granic utworów w buforze przesunięcia czasu oraz zapis segmentu. AMC ma już rozpoznawanie przez Shazam, historię i radiowy bufor przesunięcia czasu; sprawdzić ich aktualne zachowanie w bieżącym profilu AMC Test.
- Ustalić, jakie znaczniki początku i końca daje ICY/HLS i kiedy rozpoznanie przychodzi względem faktycznego początku utworu. Porównać je z osią czasu bufora oraz opóźnieniem transmisji.
- Zaprojektować bezpieczny przebieg: wskazanie rozpoznanego utworu, podgląd zakresu, ręczna korekta granic, nazwa pliku i zapis do wybranego folderu. Gdy granice są niepewne lub fragment wypadł z bufora, powiedzieć to jasno i nie tworzyć pozornie dokładnego pliku.
- Rozstrzygnąć zapis w formacie źródła albo konwersję, zależności od kodeka, limit bufora, przerwanie zapisu, konflikt nazwy oraz wpływ na równoległe nagrywanie. Ustalić zgodność z istniejącymi poleceniami nagrywania i zapisu fragmentu.
- Dopiero po badaniu wykonalności przygotować decyzję o implementacji. Nowe polecenie, jeśli powstanie, musi trafić do wspólnego modelu, menu, palety, pomocy i dokumentacji.

## Uzasadnienie i decyzje

Źródło: [BlindRadio — kod i opis](https://github.com/serrebidev/Blindradio), wcześniej ocenione w `ECOSYSTEM-001` z 4.11.2026. Tamten przegląd stwierdził, że rozpoznanie bywa spóźnione względem granicy utworu. Jest to inspiracja do własnego projektu AMC, nie zgoda na kopiowanie kodu lub deklarowanie gotowej funkcji.

## Materiał do dokumentacji użytkownika

Na tym etapie brak nowej funkcji do instrukcji. Jeżeli badanie potwierdzi wykonalność, przyszły opis ma wskazać, jak sprawdzić i skorygować granice utworu oraz kiedy AMC nie może go już zapisać z bufora.

## Testy i wynik

Nie uruchamiano testów w ramach tego zadania. Najpierw pomiary metadanych i bufora na kontrolowanych przykładach, potem ewentualnie testy zapisu, niepewnych granic, anulowania, konfliktów nazw, NVDA i równoległego nagrywania. Źródła, build, instalacja i publikacja: bez zmian.

## Znane ograniczenia

Nie każdy strumień podaje metadane. Rozpoznanie może nastąpić po początku utworu; bufor może nie zawierać całego materiału. Nazwa rozpoznania nie dowodzi prawidłowych granic pliku.

## Wersja i commit

Nie dotyczy przed rozpoczęciem implementacji. Zadanie dokumentacyjne, bez zmiany programu.
