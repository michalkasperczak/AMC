# Audyt AMC — wersja 0.1.0-alpha.342

Data: 11 września 2026. Wykonał: Hermes (agent). Zakres: analiza kodu, testy automatyczne, dostępność, przyczyna 30-sekundowych próbek TIDAL.

Dokument jest częścią podręcznika projektu. Opisuje metodę, dowody i wnioski tak, aby każdy wniosek dał się sprawdzić samodzielnie.

## 1. Co zostało zmierzone, a co pozostaje hipotezą

Rzetelność wymaga rozdzielenia tych dwóch rzeczy.

Zmierzone i potwierdzone uruchomieniem:

- Kompilacja całego rozwiązania: 0 błędów, 0 ostrzeżeń. Projekt ma `TreatWarningsAsErrors`, więc to mocny wynik.
- Testy Core: 93 z 93 zaliczone.
- Testy Windows: 29 komunikatów zaliczenia, jedna awaria (opisana w punkcie 4).
- Zestaw testów Windows jest niestabilny: awaria wystąpiła w 2 z 3 uruchomień tego samego, niezmienionego kodu.
- Etykiety dostępności: żadna kontrolka w żadnym oknie nie jest pozbawiona nazwy dostępnej.

Hipoteza, jeszcze niepotwierdzona odtworzeniem pełnego utworu:

- Przyczyna 30-sekundowych próbek TIDAL (punkt 2). Oparta na dokumentacji i kodzie TIDAL, nie na udanym odtworzeniu. Rozstrzygnie ją ponowne zalogowanie.

## 2. TIDAL: dlaczego odtwarza tylko 30 sekund

### 2.1 Objaw

Konto ma aktywny abonament. Logowanie, wyszukiwanie i kolekcja działają. Odtwarzanie kończy się po około 30 sekundach, a SDK raportuje `PREVIEW` z powodem `FULL_REQUIRES_SUBSCRIPTION`.

### 2.2 Dlaczego komunikat TIDAL jest mylący

`FULL_REQUIRES_SUBSCRIPTION` nie znaczy „użytkownik nie ma abonamentu". Znaczy: „ten token nie ma uprawnienia do pełnego odtwarzania". Serwer nie odpowiada na pytanie o abonament, tylko o zakres uprawnień tokenu. Ta różnica jest źródłem pomyłki i skierowała wcześniejsze poszukiwania na kwestie abonamentu.

### 2.3 Rozpoznana przyczyna

AMC żądało wyłącznie uprawnień nowego interfejsu TIDAL:

    user.read collection.read collection.write playlists.read playlists.write

Natomiast manifest pełnego utworu pobierany jest ze starszego adresu:

    api.tidal.com/v1/tracks/{id}/playbackinfo

Ten starszy interfejs autoryzuje się wyłącznie starymi uprawnieniami `r_usr` i `w_usr`. Bez nich zwraca próbkę około 30 sekund, niezależnie od abonamentu.

Dowody:

- Oficjalne przykłady TIDAL dla wszystkich trzech platform (web, iOS, Android) żądają `r_usr` i `w_usr`.
- Osadzony player AMC wysyła już żądanie `assetpresentation=FULL`, czyli prosi o pełny utwór. Nie on jest przyczyną.
- W zgłoszeniu numer 251 na forum TIDAL inny programista opisał identyczny objaw.
- Zgłoszenie numer 384, założone przez autora AMC, pozostaje bez odpowiedzi.

Wniosek: wina nie leży w kodzie playera ani w abonamencie, lecz w jednym wierszu z listą żądanych uprawnień.

### 2.4 Wprowadzone zmiany

Gałąź: `hermes/tidal-playback-scope`. Zatwierdzenie: `9ec6300`.

Zmiana pierwsza, uprawnienia. Dopisano `r_usr` i `w_usr`, zachowując nowe uprawnienia, bo na nich działa kolekcja i playlisty w nowym interfejsie.

Zmiana druga, naprawa błędu wprowadzonego przez zmianę pierwszą. Odświeżanie tokenu żądało zawsze pełnej listy uprawnień. Po rozszerzeniu listy serwer odrzucałby żądanie szersze od przyznanego i wylogowywał użytkownika po aktualizacji. Odświeżanie wysyła teraz to, co konto faktycznie przyznało.

Zmiana trzecia, diagnostyka. AMC zapisuje w dzienniku, czy TIDAL przyznał `r_usr`. Zamiast wnioskować z długości utworu, dostajemy odpowiedź wprost.

Zmiana czwarta, test. Nowa asercja pilnuje, że odświeżanie nie żąda uprawnień szerszych niż przyznane. Test wykonał się i został zaliczony.

### 2.5 Jak sprawdzić, czy to pomogło

Warunek konieczny: trzeba wylogować się z TIDAL w AMC i zalogować od nowa. Istniejący token nie zyska nowych uprawnień przez odświeżenie — z założenia, potwierdzonego zmianą drugą.

Po ponownym zalogowaniu dziennik AMC zawiera jedną z dwóch informacji:

- że konto przyznało `r_usr` — wtedy pełne odtwarzanie jest możliwe;
- że nie przyznało — wtedy mamy twardy, konkretny argument w rozmowie z pomocą TIDAL, zamiast ogólnego „nie działa".

Możliwy wynik negatywny: TIDAL może odmawiać `r_usr` aplikacjom spoza własnej listy zatwierdzonych. Wtedy problem jest po stronie zasad TIDAL, nie kodu AMC, a dziennik to udokumentuje.

## 3. Dostępność

### 3.1 Etykiety kontrolek

Sprawdzono wszystkie okna. Kontrolek bez nazwy dostępnej: zero. Nazwy pochodzą z trzech mechanizmów: bezpośredniej właściwości, powiązanej etykiety oraz przypisania z kodu.

Uwaga metodyczna, istotna dla przyszłych audytów. Pierwszy pomiar wskazał 184 braki i był błędny. Nie uwzględniał, że przycisk z tekstem ma nazwę z treści, a lista może być opisana etykietą powiązaną lub z kodu. Wynik odrzucono i pomiar powtórzono. Prosty licznik dopasowań w plikach interfejsu daje w tej dziedzinie wyniki bezwartościowe.

### 3.2 Ukryty player TIDAL a uciekający fokus

Zgłaszany objaw uciekającego fokusa nie ma potwierdzenia w tym miejscu. Ukryty element playera ma wyłączone zatrzymywanie tabulatorem, wyłączoną klikalność, rozmiar jednego piksela i pustą nazwę, czyli nie wchodzi do kolejności tabulacji ani nie zaśmieca drzewa czytnika.

To nie jest dowód, że problem nie istnieje — jest dowodem, że nie pochodzi z playera. Do rozstrzygnięcia potrzebny jest scenariusz odtworzenia: która lista, jaka akcja, gdzie wraca fokus.

### 3.3 Kolejność czytania i powiadomienia

Kod korzysta z powiadomień dostępności do zgłaszania zmian stanu odtwarzania, więc czytnik dowiaduje się o zmianach bez przenoszenia fokusa. To rozwiązanie właściwe.

## 4. Znaleziony błąd: niestabilny test mostka NVDA

### 4.1 Fakty

Plik `tests/AccessibleMediaController.Windows.SmokeTests/NvdaBridgeSmokeTests.cs`, metoda `TestDispatcherDeadline`, wiersz 132. Awaria: `Expired request cannot run after the UI recovers`.

Występuje również na nietkniętej gałęzi `main`, więc nie wywołały jej zmiany z tego audytu. Sprawdzono osobnym uruchomieniem.

Powtarzalność: awaria w 2 z 3 uruchomień identycznego kodu. To test zależny od czasu, nie od zawartości zmiany.

### 4.2 Dlaczego to ma znaczenie

Konsekwencja bezpośrednia: awaria przerywa cały zestaw testów Windows, więc testy następujące po niej nie wykonują się wcale. Zestaw daje wtedy fałszywe poczucie pokrycia. Przy każdej przyszłej zmianie połowa testów może milczeć.

Konsekwencja możliwa, wymagająca sprawdzenia: test opisuje regułę, że polecenie NVDA, którego czas minął, nie może wykonać się po odzyskaniu responsywności okna. Kod produkcyjny w `MainWindow.Nvda.cs`, wiersz 25, używa dokładnie tego samego wzorca kolejkowania z anulowaniem. Jeśli reguła zawodzi, spóźnione polecenie z NVDA mogłoby wykonać się po czasie — na przykład przy zajętym oknie w trakcie ładowania dużej listy. Nie zostało to potwierdzone na działającej aplikacji i pozostaje hipotezą.

### 4.3 Zalecenie

Rozdzielić dwie sprawy. Po pierwsze, uodpornić test na wyścig czasowy, bo dziś jego wynik zależy od obciążenia maszyny. Po drugie, niezależnie od testu, ustalić obserwacją na działającej aplikacji, czy spóźnione polecenia NVDA są odrzucane.

Dodatkowo zaleca się, aby zestaw testów nie przerywał się na pierwszej awarii, lecz wykonywał wszystkie przypadki i podsumowywał je na końcu. Dziś jedna awaria ukrywa stan pozostałych.

## 5. Co sprawdzono i co jest w porządku

Warto to zapisać, aby nie szukać problemów tam, gdzie ich nie ma.

Logowanie jest zaimplementowane bezpiecznie: PKCE, weryfikacja parametru stanu w sposób odporny na atak czasowy, brak tokenów w dzienniku.

Nie znaleziono wzorców grożących zawieszeniem okna. Wszystkie miejsca oczekujące na wynik zadania robią to albo po jego zakończeniu, albo poza wątkiem okna. Sprawdzono ręcznie każde wystąpienie — proste wyszukiwanie takich wzorców daje dużo fałszywych alarmów.

## 6. Stan repozytorium, do uporządkowania

Sprawdzono: wersja 342 jest obecna zarówno lokalnie, jak i na GitHub (zatwierdzenie `7f29acd`). Wcześniejsze przypuszczenie o rozbieżności było błędne i wynikało z nieaktualnej kopii repozytorium na maszynie pomocniczej. Lekcja metodyczna: stan zdalny należy sprawdzać u źródła, a nie we własnym klonie.

W katalogu roboczym znajduje się plik z treścią prywatnej korespondencji z pomocą TIDAL, oznaczony jako nieprzeznaczony do publikacji. Nie został objęty zatwierdzeniem i nie należy go umieszczać w repozytorium.

## 7. Zalecana kolejność działań

Najpierw ponowne zalogowanie do TIDAL i sprawdzenie w dzienniku, czy przyznano `r_usr`. To rozstrzyga główny problem projektu jednym testem.

Następnie naprawa niestabilnego testu NVDA oraz zmiana zestawu testów tak, by nie przerywał się na pierwszej awarii. Bez tego kolejne zmiany będą testowane pozornie.

Następnie wypchnięcie wersji 342 na GitHub, aby kod lokalny i zdalny opisywały ten sam stan.

Na końcu, jeśli TIDAL odmówi uprawnienia `r_usr`, przygotowanie zapytania opartego na zapisie z dziennika — z konkretną nazwą odmówionego uprawnienia zamiast opisu objawu.

## 8. Metoda, do powtórzenia w przyszłości

Budowanie i testy wykonano na komputerze z systemem Windows i .NET 8, ponieważ interfejs aplikacji wymaga systemu Windows. Próba instalacji .NET na maszynie pomocniczej nie powiodła się z powodu zerwanych połączeń sieciowych i zablokowanego menedżera pakietów; nie ma to wpływu na wyniki, bo pomiary wykonano tam, gdzie aplikacja faktycznie działa.

Dwie lekcje warte zapamiętania.

Pierwsza: wniosków o dostępności nie wolno opierać na liczeniu dopasowań w plikach interfejsu. Trzeba uwzględnić wszystkie mechanizmy nadawania nazwy, inaczej powstaje raport z setkami nieistniejących błędów.

Druga: przy każdej znalezionej awarii należy sprawdzić, czy występuje również bez wprowadzonych zmian, i czy jest powtarzalna. Bez tego przypisuje się winę niewłaściwej zmianie i naprawia się nie ten problem.
