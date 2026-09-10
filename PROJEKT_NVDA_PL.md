# Dodatek NVDA do AMC — etap 1, alpha 339

## Decyzja i zakres

Drugi, niezależny sposób sterowania obok zachowanego prefiksu. NVDA jest cienkim klientem poleceń, a AMC nadal przechowuje sesje, kolejki, odtwarzanie, poziomy głośności i nagrywanie. Nie kopiujemy silnika Free Radio. Jego źródła sprawdzono w celu rozpoznania przyzwyczajeń i konfliktów, nie skopiowano implementacji.

Dodatek 0.1.0 udostępnia 14 poleceń w NVDA > Ustawienia > Zdarzenia wejścia > AMC. Początkowo bez przypisań: Free Radio ma już Ctrl+Windows+strzałki (głośność i poprzednia/następna stacja), P (pauza), I (informacja) i inne kombinacje. Wybór docelowego profilu pozostaje do uzgodnienia. Instalacja nie wyłącza Free Radio, nie nadpisuje gestures.ini, nie rejestruje klawisza prefiksu ani nie zmienia skrótów AMC.

Proponowana mapa po świadomym rozdzieleniu skrótów Free Radio i Windows:

| Kombinacja | Działanie |
| --- | --- |
| Ctrl+Windows+góra/dół | Głośniej/ciszej |
| Ctrl+Windows+lewo/prawo | Poprzedni/następny element kontekstu odtwarzania |
| Ctrl+Windows+P | Odtwórz/wstrzymaj |
| Ctrl+Windows+M | Wycisz sesję |
| Ctrl+Windows+I | Co jest odtwarzane |
| Ctrl+Windows+J/K | Cofnij/przewiń o 10 sekund |
| Ctrl+Windows+E/R/T | Czas od początku/pozostały/całkowity |
| Ctrl+Windows+PageUp/PageDown | Poprzednia/następna sesja |

To propozycja, nie działający domyślny zestaw. Ctrl+Windows+lewo/prawo jest też używane przez pulpity Windows. Ctrl+Windows+Enter nie przejmujemy (Narrator). Nie zmieniamy też skrótów cyfr paska zadań. Tymczasowy wariant Ctrl+Windows+Alt wymaga sprawdzenia kolizji z pozostałymi dodatkami.

## Granice bezpieczeństwa i wydajności

- Named pipe w obrębie sesji Windows, CurrentUserOnly; klient otwiera wyłącznie lokalną ścieżkę pipe, bez portu TCP/HTTP. Nie jest to zdalne sterowanie przez Internet.
- Protokół wersjonowany JSON, allow-list 14 komend. Bez ścieżek, sekretów, dowolnych ID poleceń, uruchamiania procesów, zapisu plików czy operacji usuwania. Wiadomość maksymalnie 512 bajtów; odpowiedź ograniczona do 2000 znaków.
- Serwer obsługuje jedno polecenie na połączenie. Limit 2 sekund obejmuje czytanie i oczekiwanie na Dispatcher; anulowanie kolejki UI zapobiega późniejszemu wykonaniu niepodjętego polecenia. Operacji już rozpoczętej nie można cofnąć — brak odpowiedzi nie dowodzi, że nie została wykonana.
- Klient: najwyżej 0,4 sekundy na zestawienie połączenia i 2,5 sekundy na wymianę. Wszystko w pojedynczym wątku pomocniczym, nigdy w wątku obsługi klawiatury NVDA. Anulowanie overlapped I/O przed zwolnieniem buforów.
- Nie ponawiamy wysłanej komendy. Ponawiane jest wyłącznie otwarcie połączenia przed wysłaniem jakichkolwiek danych. Limit kolejki 4, ważność oczekującej komendy 1,5 sekundy.
- Nie wykonujemy nic na ekranie blokady ani w secure mode. Nie podnosimy uprawnień AMC, nie uruchamiamy NVDA i nie instalujemy dodatku automatycznie.
- Globalny transport używa ExecuteCommand i istniejących CommandIds. Dotyczy aktualnego odtwarzania aktywnej sesji AMC, nie elementu zaznaczonego w obcym oknie. Sesja jest ustalana w momencie obsługi polecenia w AMC.
- Natychmiastowe komunikaty przechwytujemy dla jednej odpowiedzi NVDA (mowa i brajl). Błędy asynchronicznego odtwarzania zachowują dotychczasową drogę komunikatów AMC. Przyjęcie komendy nie jest potwierdzeniem zakończenia połączenia z usługą.
- Jawna informacja na żądanie może być wypowiadana poza AMC; automatyczne Shazam/rozpoznawanie nie zmienia zasad. Dodatek nie wycisza czytnika.
- Zdalne przejście do innej sesji nie przywołuje okna. Mechanizmy przywracania fokusa dla bieżącej komendy nie przenoszą go do AMC z tła. Do sprawdzenia z rzeczywistym NVDA także po opóźnionej odpowiedzi dostawcy.

## Dalsze etapy

Po testach podstaw: profile klawiszy, presety, zakładki/rozdziały, bezpieczne zarządzanie nagrywaniem, jawne otwieranie dostępnych list. Bez przejmowania skrótów istniejących dodatków. Zmiany kolekcji muszą jednoznacznie wskazywać element; nie wykonujemy usuwania ani edycji przez przypadkowy fokus.

Rozdzielenie strony wykonawcy TIDAL na kategorie pozostaje odrębnym zagadnieniem; ten etap go nie implementuje ani nie zmienia uprawnień odtwarzania TIDAL.

## Źródła techniczne

- NV Access: https://download.nvaccess.org/documentation/developerGuide.html — GlobalPlugin, dekorator script, paczka i manifest dodatku.
- NV Access: https://github.com/nvaccess/nvda/blob/master/source/utils/security.py — integracja z winAPI.sessionTracking, rozróżnienie blokady i secure desktop.
- Microsoft: https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream — named pipes i CurrentUserOnly.

## Testy przed uznaniem dodatku za gotowy

Testy transportu nie zastępują NVDA. Ręcznie: instalacja, widoczność opisów komend w Zdarzeniach wejścia, pomoc klawiszy NVDA, przypisanie i zmiana akordu, brajl, brak podwójnej mowy, powrót fokusa. Następnie AMC na pierwszym planie/w tle/zminimalizowany, edytor podczas sterowania, zamknięty AMC, menu i modalny dialog, uśpienie, blokada Windows, Free Radio włączone, nieudany strumień, wyłączone urządzenie, nagrywanie w tle, lokalne/radio/podcasty/WiiM/TIDAL. Nie wolno deklarować tych prób jako zaliczonych na podstawie samych testów jednostkowych.
