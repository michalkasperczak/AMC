# Odbiór okna Sonos — PASS lokalnego przyrostu

Odczytano pełny niezależny raport `sonos-account-window-review-candidate1-verdict/odbior-B1-B2-B3.md` wraz z aneksem obejmującym B1-busy. Jest to odbiór statyczny; nie przypisujemy recenzentowi własnego uruchomienia testów.

Produkt odebrany: SonosAccountWindow.xaml.cs SHA-256 `6b96878edf9d7dd6e9404724d8ec1dddb91a9c8b18ee560acacbc5885294ada7`; XAML zgodny z pierwotnym kandydatem. Parent porównał pełny zbiór plików i hasze z końcowego testu — to samo wejście. Po porównaniu poprawiono tylko historyczne brzmienie dwóch wpisów MAPA_KODU_PL.md; bez zmian kodu/testów.

Runtime parenta: domyślny harness116/116, niezmieniona sonda16/16, niezmieniona sonda jakości51asercji/0realFAIL, busy9/9, build produkcyjnego projektuWindows0warnings/0errors. ŻywyNVDA finalnegoSHA:7przypadków prawdziwymi klawiszami, pojedyncze komunikaty, fokus zachowany także podczas await, brak odebrania fokusu drugiemu własnemu oknu. Szczegóły i ograniczenia w `sonos-account-window-lifetime-parent1/REPORT_FINAL.md`.

Dwie uwagi recenzenta nie są nowymi wymaganiami: pośredni fokus zmienia się synchronicznie przed końcem przebudowy, ale parent nie zmierzył podwojenia mowy; ObjectDisposedException z delegata potwierdzenia dostałoby ten sam komunikat niedostępności. Nie rozszerzano odbioru o porządki.

Nie przejęto jako pomiaru twierdzenia recenzenta o konkretnym pośrednim celu Refresh: target zależy od chwilowej dostępności. Rozstrzygający jest końcowy odczyt NVDA. Wywołanie koordynatora w Disconnect jest po potwierdzeniu, nie pierwsze w całym handlerze; wniosek o braku częściowej nowej migawki po ODE pozostaje prawidłowy.

To odbiór samego okna, nie prawdziwego logowania ani pełnej aplikacji. Następny przyrost: właściciel koordynatora i dojście z interfejsu AMC, domyślna konfiguracja brokera bez pól deweloperskich, prawdziwe dostępne potwierdzenie wylogowania, izolowane testy tej drogi. Bez Sonos LIVE, produkcyjnego wdrożenia i publikacji w tym commicie.
