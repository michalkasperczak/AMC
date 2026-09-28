# Sonos — wytyczne, stan prac i otwarte sprawy

Stan na 28 września 2026 r. To dokument rozwoju, nie deklaracja, że wszystkie opisane funkcje są już dostępne w instalatorze.

## Co jest wydane

[AMC 0.1.0-alpha.412](https://github.com/michalkasperczak/AMC/releases/tag/v0.1.0-alpha.412) zawiera obsługę konta Sonos: logowanie w oficjalnej przeglądarce, sprawdzenie wyniku, zapis dostępu dla bieżącego użytkownika Windows, odnawianie dostępu, ponowienie zapisu i wylogowanie z potwierdzeniem.

Pierwsze rzeczywiste logowanie zostało potwierdzone po publikacji tego wydania. Nie oznacza to jeszcze potwierdzenia sterowania głośnikami ani wszystkich przebiegów odnawiania dostępu na rzeczywistym koncie.

**Alfa412 nie zawiera listy głośników, osobnej sesji Sonos ani sterowania jego odtwarzaniem.** Zakres wydania opisuje również [lista zmian alfa412](ZMIANY_0.1.0-alpha.412_PL.md).

## Ustalony kierunek: sposób obsługi możliwie jak w WiiM

Wzorcem jest istniejąca sesja WiiM, nie osobny, niepowiązany sposób obsługi Sonosa:

- wybór aktywnego celu; w Sonosie trzeba uwzględnić domy i grupy głośników;
- lista oraz widok odtwarzacza, podobna nawigacja i powrót;
- spójne skróty, ustawienia i komunikaty o bieżącym stanie;
- zachowanie wyboru i miejsca klawiatury po odświeżeniu;
- wyjście z kontrolera nie zatrzymuje samoczynnie muzyki na autonomicznym urządzeniu;
- brak zmian numerów i skrótów dotychczasowych sesji AMC.

Podobieństwo obsługi nie oznacza kopiowania protokołu WiiM. Nie przenosimy w ciemno dodawania urządzeń po IP, komend sieci lokalnej ani ograniczeń usług muzycznych właściwych dla WiiM. Nie należy pokazywać martwych odpowiedników funkcji WiiM.

Oficjalna dokumentacja Sonosa potwierdza sterowanie odtwarzaniem grupy, przewijanie, odczyt metadanych oraz regulację głośności i wyciszenia. Istnieją także odczyt ulubionych i obsługa wejścia liniowego na odpowiednim sprzęcie. Dostępności pełnego korektora nie ustalono. [Szczegóły, źródła i ograniczenia](SONOS_CONTROL_API_PL.md) zapisano oddzielnie — potwierdzenie dokumentacji nie oznacza jeszcze wdrożenia ani pomiaru na rzeczywistym urządzeniu.

Okno **Konto Sonos → Głośniki i grupy** jest przyrostem pomocniczym. Samo jego przygotowanie nie kończy całej integracji.

## Prace po alfa412 — jeszcze niewydane

Kod tych przyrostów jest obecnie w lokalnych gałęziach roboczych, a nie w opublikowanym instalatorze:

- klient odczytu domów, grup i głośników z oficjalnego Control API;
- odczyt przez wspólnego właściciela konta, bez przekazywania tokenów do modeli okna;
- lista urządzeń, wybór domu, odświeżanie, obsługa pustej i częściowej odpowiedzi;
- poprawki ochrony przed wynikiem starej operacji po zmianie konta;
- poprawki komunikatów wczytywania, fokusu i zachowania wybranego domu.

Te elementy przeszły testy automatyczne. Ponowna próba klawiaturą i żywym NVDA na poprawce `f1801bb` potwierdziła zachowanie wybranego domu, komunikaty w prawdziwym Podglądzie mowy, zamknięcie podczas odczytu i powrót do okna konta. Nadal wykazała utratę fokusu po odświeżeniu uruchomionym z rzeczywistego wiersza obu list — S-02 pozostaje blokadą wydania. Próba używała rzeczywistych okien i syntetycznych odpowiedzi; nie jest odczytem urządzeń użytkownika.

## Rejestr spraw tego przyrostu

### S-01. Stary odczyt przechodził na nowe konto podczas odnawiania

Fakt: w niewydanym kandydacie opóźniona odmowa dostępu albo odnowienie wygasłego dostępu mogły doprowadzić do użycia nowego konta przez dawną operację. Zostało to odtworzone na danych próbnych przez rzeczywistą drogę logowania, a nie przez ręczne podstawienie wewnętrznych pól.

Stan: poprawka i testy automatyczne potwierdzają odrzucenie starej operacji oraz zachowanie nowego konta. Nie jest to zgłoszenie utraty rzeczywistego konta użytkownika ani błąd przypisany wydaniu alfa412.

### S-02. Odświeżanie gubiło miejsce klawiatury

Fakt: żywy NVDA po wolnym odświeżeniu wskazywał całe okno zamiast kontrolki.

Stan: poprawka `f1801bb` działa przy odświeżaniu z przycisku oraz z samego kontenera listy. Ponowny pomiar NVDA wykazał, że **Alt+O z rzeczywistego wiersza grupy lub głośnika nadal gubi fokus**, w trakcie i po odczycie. Czytnik powtarza wtedy treść całego dialogu. Przyczyną jest pominięcie elementu `ListBoxItem` w ochronie fokusu; sam test kontenera listy nie wykrywa tego przypadku. Kolejny lokalny kandydat `a067dd8` przeszedł już żywy NVDA dla zwykłego odświeżenia obu list oraz ostatnich wierszy list 20-elementowych. Osobna próba wykazała jeszcze brak powrotu na ten sam element, gdy zmiana kolejności przesuwa go poza wcześniej widoczną część listy: fokus pozostaje na instrukcji. Trwa punktowa korekta utworzenia właściwego wiersza poza tym obszarem; po niej trzeba powtórzyć identyczny przypadek. Nadal nie jest to funkcja wydana w instalatorze.

### S-03. Odświeżanie zmieniało wybrany dom

Fakt: odświeżenie drugiego domu wybierało pierwszy mimo dalszej obecności wybranego domu.

Stan: poprawka `f1801bb` zachowuje wybór po identyfikatorze, a nie pozycji na liście. Testy automatyczne obejmują zmianę kolejności. Ponowna próba z żywym NVDA potwierdziła zachowanie drugiego domu po wolnym odświeżeniu z przycisku, wraz z powrotem na przycisk. Nie zamyka to odrębnego problemu wierszy S-02.

### S-04. Brakowało informacji o trwającym odczycie

Fakt: podczas oczekiwania pozostawał poprzedni wynik, bez komunikatu o wczytywaniu.

Stan: na poprawce `f1801bb` potwierdzono wypowiedzi rozpoczęcia odczytu domów, odczytu grup oraz wyniku w rzeczywistym Podglądzie mowy NVDA. Pomiar dotyczy pierwszego otwarcia i wolnego odświeżania z przycisku. Ponowny odczyt całego dialogu przy utracie fokusu z wiersza jest nadal otwartym problemem S-02.

## Warunki przed publikacją kolejnego przyrostu

- Zakończony odbiór klawiaturą i NVDA: pierwsze wczytanie, odświeżanie, wybór domu, Escape w czasie oczekiwania, powrót do okna konta i ponowne otwarcie.
- Potwierdzenie prawdziwej drogi wywołania, nie wyłącznie ręczne wywołanie pomocniczej metody w teście.
- Odczyt rzeczywistych urządzeń dopiero po odebraniu bezpieczeństwa połączenia; wyniki syntetyczne nie są przedstawiane jako odpowiedź konta Sonos.
- Sterowanie odtwarzaniem, metadane i głośność jako kolejne, jawnie sprawdzane możliwości; bez pozornego odtwarzacza z niezmierzonym czasem.
- Opis konkretnej wersji, testy, paczki i weryfikacja publikacji. Wydanie, instalacja oraz uruchomienie u użytkownika są osobnymi stanami.

## Gdzie utrwalamy ustalenia

Ten plik jest publicznym punktem odniesienia dla kierunku i bieżących spraw Sonosa. Ogólne zasady projektu są w [AGENTS.md](AGENTS.md), scenariusze w [TESTY_ZADANIA_PL.md](TESTY_ZADANIA_PL.md), a szersze niejednoznaczności w [rejestrze ryzyk](REJESTR_RYZYK_I_NIEJEDNOZNACZNOSCI_PL.md).

Każde zamknięcie sprawy powinno podawać wersję lub commit oraz zakres rzeczywistej weryfikacji. Opisy kolejnych wydań są w [GitHub Releases](https://github.com/michalkasperczak/AMC/releases) i plikach zmian wersji. Nie publikujemy surowych logów użytkownika, poświadczeń, prywatnych kolekcji ani nagrań.
