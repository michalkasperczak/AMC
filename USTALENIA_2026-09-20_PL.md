# AMC — ustalenia i zasady odbioru, 20 września 2026

Ten dokument zapisuje decyzje dotyczące przygotowywanej wspólnej paczki po alfie 398. **Nie jest listą zmian już opublikowanego wydania.** Odbiór pojedynczej poprawki nie oznacza odbioru całej paczki.

## 1. Presety: skróty pozostają bez zmian

- `Ctrl+cyfra` — wybór sesji.
- `Ctrl+Shift+cyfra` — uruchomienie presetu bieżącej sesji.
- `Ctrl+Alt+P` — lista presetów.
- `Ctrl+Alt+Shift+P` — okno przypisywania presetów.

Nie przenosimy uruchamiania presetów na `Ctrl+Alt+cyfra` i nie oddajemy `Ctrl+Shift+cyfra` rzadko wykonywanemu numerowaniu sesji. Bezpośrednie przypisywanie przez `Ctrl+Alt+Shift+cyfra` pozostaje pomysłem, a nie zatwierdzoną funkcją.

Przy zwykłym uruchomieniu nie powtarzamy niepotrzebnie słowa „Preset” i jego numeru. Numery pozostają potrzebne w oknie przypisywania, liście oraz komunikatach o pustym lub błędnym przypisaniu.

Preset folderu lokalnego otwiera folder. Preset albumu Spotify ma uruchomić odtwarzanie, nie tylko zaznaczyć album w Bibliotece; nie może przy tym przestawiać przeglądanej listy ani fokusu. Działające uruchamianie presetów podcastów i przejście do odtwarzanego elementu należy zachować.

## 2. Ponowne wywołanie tego samego presetu

Zatwierdzona zasada: krótkie potwierdzenie zamiast ciszy, bez niepotrzebnego ponawiania operacji.

- Jeżeli przypisany materiał już gra, program mówi samą nazwę. Nie przerywa, nie cofa czasu i nie zmienia kolejki.
- Jeżeli z albumu gra już dalszy utwór, powtórzenie presetu nie wraca do pierwszego utworu.
- Jeżeli ten sam materiał jest w pauzie, preset wznawia od bieżącej pozycji i mówi nazwę.
- Jeżeli przypisany folder jest już otwarty, program mówi nazwę bez przebudowy listy i bez zmiany zaznaczenia lub fokusu.
- Jeżeli cel jest inny, preset wykonuje zwykłe uruchomienie albo otwarcie.

Należy porównywać rzeczywisty materiał, sesję i kontekst, nie tylko ostatnio naciśniętą cyfrę. Zmiana przypisania lub przejście do innego materiału nie może zostać pomylone z powtórzeniem.

## 3. TIDAL: naprawa dwóch konkretnych błędów

Preset utworu nie może omijać drogi do oryginalnego TIDAL-a i uruchamiać odtwarzacza próbek SDK. Powinien korzystać ze wspólnej reguły z uruchamianiem utworu z listy, zachowując kontekst i zasady działania w tle.

W żywej próbie potwierdzono również, że wiersz już załadowanego utworu w TIDAL-u może zawierać ikonę głośnika zamiast przycisku odtwarzania — także podczas pauzy. Brak tego przycisku nie jest dowodem odmowy odtwarzania przez usługę.

Bezpieczna droga zastępcza wymaga zgodności identyfikatora docelowego wiersza z utworem w stopce odtwarzacza. Przy pauzie można użyć przycisku Play w stopce; jeżeli utwór już gra, nie wolno kliknąć Pause. Inny utwór w stopce albo brak potwierdzenia tożsamości blokuje tę drogę.

Rozpoznawanie sukcesu i rozpoznawanie „to już jest bieżący utwór” musi używać zgodnego sposobu odczytu odpowiedzi. Potwierdzone ponowienie nie może nadpisywać kolejki.

Pozostajemy przy naprawie potwierdzonych usterek. Ogólna przebudowa sterowania oryginalnym TIDAL-em jest osobnym, późniejszym tematem.

## 4. Wyniki wyszukiwania, wykonawcy i kolejki

Enter na wyniku wyszukiwania domyślnie otwiera lub odtwarza, **bez automatycznego dodawania do Biblioteki**. Jedno globalne ustawienie umożliwia otwieranie z automatycznym dodaniem. Jawne dodawanie pozostaje niezależną czynnością.

Tymczasowe przechowanie elementu potrzebnego do odtwarzania nie oznacza członkostwa w Bibliotece. Ponowne otwarcie już zapisanego elementu nie może go usuwać; odświeżenie lub restart nie może samoczynnie dodawać elementu tymczasowego.

Przegląd wykonawców Spotify i TIDAL ma być spójny, z rozróżnieniem albumów i utworów oraz dostępem do kolejnych stron. Nie wystawiamy pustych kategorii ani nie obiecujemy danych, których usługa nie udostępnia. Błędu lub nieprzeprowadzonej próby API nie opisujemy jako pustego katalogu.

Przy kolejce odróżniamy niezamierzone duplikaty od celowych powtórzeń. Odbiór obejmuje także zmianę priorytetu, usunięcie i ponowny start, nie tylko odtworzenie zapisanej listy.

## 5. Menu i ustawienia sesji

Bieżące, ograniczone porządki:

- „Otwórz strumień” należy do Radia internetowego — ta operacja i tak kieruje do sesji radia, nie do odtwarzacza Spotify.
- „Zapisane podcasty Spotify” są widokiem kolekcji, dlatego docelowo znajdują się w menu **Widok**, z zachowaniem `Ctrl+Alt+O`.
- Główne Ustawienia mają umożliwiać wybranie sesji i zmianę jej opcji odtwarzania, dostępnych obecnie bezpośrednio przez `Ctrl+Alt+Enter`.

Obie drogi edycji opcji powinny korzystać z tej samej logiki. Edytowanie innej sesji nie może przełączać aktywnej sesji ani rozpoczynać odtwarzania.

Zmiany w podoknie otwartym z Ustawień należą do roboczej kopii ustawień: zewnętrzne Anuluj nie zapisuje ich w rzeczywistym stanie. Zapisz, ponowne otwarcie i ponowny start muszą zachować zatwierdzone wartości.

Pokazujemy wyłącznie opcje wykonywane przez konkretną sesję i jej silnik. Ukrycie niedostępnego pola nie może wymazywać zachowanej wartości. Ustawienie bez odbiorcy w odtwarzaniu nie jest ukończoną funkcją.

Szersze uporządkowanie całego interfejsu i ustawień pozostaje osobnym etapem.

## 6. Apple Music — następny etap, dwa osobne zadania

1. Katalog i biblioteka: autoryzacja, pobranie danych, nawigacja i trwałość kolekcji.
2. Odtwarzanie: autoryzacja użytkownika, rzeczywisty pełny utwór i sterowanie.

Sprawny katalog nie dowodzi sprawnego odtwarzania. Ogólny test DRM w WebView2 nie dowodzi, że Apple wyda licencję na konkretny materiał. Prywatnego klucza deweloperskiego nie umieszczamy w publicznie rozprowadzanym programie ani w repozytorium; korzystamy z prawidłowo uzyskanych danych MusicKit, nie cudzych tokenów.

## 7. Jak uznajemy zmianę za sprawdzoną

- Test najpierw musi wykazać stary błąd, a następnie przejść na poprawionej wersji.
- Test interfejsu wywołuje rzeczywiste okno i rzeczywistą obsługę komendy. Dopuszczalna atrapa sieci lub dźwięku musi być jawna i nie zastępuje pomiaru tych usług.
- Do pomiaru nazwy kontrolki potrzebny jest rzeczywisty obiekt WPF/UIA. Sam formatter nie dowodzi etykiety, a etykieta nie dowodzi wypowiedzianej mowy.
- Odczyt NVDA potwierdzamy żywym czytnikiem; przy komunikatach także zapisem z podglądu mowy. Sprawdzamy powtórzenia skrótu, anulowanie i powrót fokusu.
- Sam komunikat „przekazano polecenie” nie dowodzi odtwarzania. Odczytujemy odpowiedni stan i przyrost czasu; wynik nie jest automatycznie odsłuchem jakości dźwięku.
- Przy wydajności porównujemy tę samą operację na tych samych danych. Koszt przygotowania zapisu nie jest pomiarem całej reakcji klawisza ani czytnika.
- Po połączeniu poprawek ponawiamy testy całego kandydata. Zielone testy osobnych gałęzi nie zastępują regresji wspólnej wersji.
- Próby zaczynamy na izolowanych danych i profilach, bez kont oraz plików użytkownika, gdy nie są niezbędne.

## 8. Wydanie i instalacja

Poprawki zbieramy do wspólnej paczki, zamiast instalować kolejne próby osobno. Instalacja wymaga gotowego, sprawdzonego kandydata i weryfikacji właściwego instalatora.

Bezpośrednio przed zamknięciem aplikacji sprawdzamy rzeczywisty stan wszystkich nagrań. Aktywne nagrywanie lub nieudany odczyt blokuje operację; brak nowych plików w katalogu nie dowodzi braku nagrywania.

Instalacja kończy się uruchomieniem właściwej nowej wersji w sesji użytkownika i potwierdzeniem jej działania. Zachowujemy bieżący materiał, pozycję, pauzę, widok i głośność; pauzy nie zamieniamy w odtwarzanie. Nie ponawiamy instalatora w ciemno po przekroczeniu czasu sondy.
