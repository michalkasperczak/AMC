# AM-005 — członkostwo utworów i albumów w Bibliotece Apple Music

- Status: **kod i testy celowane gotowe; bez kompilacji, instalacji, publikacji
  i próby zapisu na żywym koncie**.
- Utworzono: 13 listopada 2026.
- Obszar: Apple Music, `Ctrl+Shift+L`, menu, paleta, pomoc, fokus i NVDA.

## Zachowanie użytkowe

- `Ctrl+Shift+L` oraz równoważna pozycja menu wykonują jedną wspólną akcję
  „Dodaj lub usuń z Biblioteki Apple Music”.
- Katalogowy utwór albo album może zostać dodany do Biblioteki. Biblioteczny
  utwór albo album może zostać usunięty wyłącznie z bezpośredniej listy
  „Utwory” albo „Albumy”.
- Po potwierdzonym usunięciu lista jest odczytywana ponownie. Fokus przechodzi
  na następny widoczny element, a po usunięciu ostatniego — na poprzedni.
- Końcowa wypowiedź NVDA następuje po odświeżeniu, np. „Usunięto utwór z
  Biblioteki Apple Music”. Jeśli zapis się udał, ale odświeżenie nie, AMC mówi
  o obu faktach. Podczas trwającej operacji drugie wywołanie nie uruchamia
  równoległego zapisu.
- Identyfikatory Apple, wyjątki biblioteki Python i reprezentacje obiektów nie
  trafiają do kontrolki ani komunikatów czytnika ekranu.

## Decyzje i ograniczenia

- Zakres jest celowo ograniczony do utworów i albumów, ponieważ używana
  biblioteka ma dla nich potwierdzone oba kierunki: `library_add` oraz osobne
  usuwanie. Wykonawcy, playlisty i nowe Ulubione Apple nie są reklamowane.
- Usuwanie z wnętrza playlisty jest wyłączone. Ten sam biblioteczny utwór może
  tam oznaczać zarówno członkostwo Biblioteki, jak i członkostwo playlisty;
  AMC nie wykonuje niejednoznacznej operacji.
- Ścieżka dodawania jest gotowa dla rekordów katalogowych, lecz obecny interfejs
  Apple Music nie ma jeszcze pełnego wyszukiwania katalogu. W bieżących listach
  częściej dostępne będzie usuwanie niż ponowne dodanie.
- `Ctrl+U` i `Ctrl+Shift+U` pozostają niedostępne. Nie wolno utożsamiać nowych
  Ulubionych Apple z samym członkostwem Biblioteki.
- Odtwarzanie Enterem, `Shift+A`, kolejka i transport są poza tym zadaniem.
  Zgodnie z decyzją użytkownika próba oryginalnej aplikacji Apple Music jest
  odłożona do osobnego etapu.

## Powierzchnie polecenia

Jedna akcja `appleMusic.library.toggle` zasila skrót `Ctrl+Shift+L`, menu
Biblioteka, menu kontekstowe, paletę `Ctrl+Shift+K`, pomoc kontekstową i pełną
pomoc klawiatury. Menu jest aktywne tylko dla rekordu o bezpiecznie ustalonym
kierunku operacji; warunek usuwania uwzględnia także bieżący widok.

## Testy i rzeczywisty wynik

- Adapter: dodanie katalogowego albumu, usunięcie bibliotecznego utworu,
  odrzucenie wykonawcy i ukrycie wyjątku dostawcy — zaliczone na atrapie.
- GUI: odświeżenie tego samego widoku, wybór sąsiada, końcowa mowa i
  zwolnienie blokady operacji — zaliczone.
- Menu i klawiatura: zakres tylko sesji Apple Music oraz trzy warianty
  dostępności (dodanie, bezpieczne usunięcie, odmowa w playliście) — zaliczone.
- Parytet menu, skrótów, pomocy, palety i angielskich nazw poleceń — zaliczony.
- Łączny celowany przebieg na tym etapie: **112 testów i 3 podtesty zaliczone**.
- Końcowa szeroka regresja obu przyrostów: **2731 testów i 34 podtesty
  zaliczone, 117 pominiętych**. Sześć jawnie wyłączonych kontroli obejmowało
  dwa znane długi kompletności angielskiego katalogu oraz cztery testy
  prawdziwego wxPython uruchomione osobno; trzy testy GUI Radio Archiwisty i
  sonda modeless Sonosa przeszły w prywatnym runtime AMC.
- Podpisy rzeczywistych metod zainstalowanej biblioteki `applemusic` zostały
  sprawdzone w prywatnym runtime AMC. Nie wykonano jeszcze zapisu na koncie,
  dlatego testy kodu nie są przedstawiane jako dowód działania usługi.

## Stan dostarczenia

- Zmiany źródłowe: wykonane, jeszcze niezatwierdzone.
- Testy celowane: zaliczone.
- Pełna regresja: wykonana z wyżej opisanymi, jawnymi wyłączeniami środowiska.
- Build: niewykonany.
- Instalacja lokalna: nadal alpha.498 z 13.11.2026, 02:17; ta funkcja nie jest
  jeszcze obecna w zainstalowanym programie.
- GitHub i wydanie: niewykonane.
- Wersja i commit nowego przyrostu: do nadania po regresji i zatwierdzeniu.
