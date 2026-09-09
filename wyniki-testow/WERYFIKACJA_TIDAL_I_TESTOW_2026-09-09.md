# Weryfikacja TIDAL i obsługi błędów testowych

Data: 9 września 2026. Baza: cc92e15, alpha.335.
Zmiany dotyczą programu testowego i dokumentacji, nie silnika produkcyjnego.
Nie zmieniono numeru wersji, pakietu ani konfiguracji działającego AMC.

## Wyniki końcowe

- Kompilacja Windows.SmokeTests, Release: kod 0, bez ostrzeżeń i błędów.
- Core.SmokeTests, pełne uruchomienie bez parametrów: kod 0.
- Windows.SmokeTests, pełne uruchomienie bez parametrów: kod 0.
  Obejmuje m.in. kontrolki dostępności, dekodery, lokalne scenariusze nagrywania,
  przejścia TIDAL i nowe testy granicy błędów. To nie test pełnych utworów TIDAL
  na koncie użytkownika ani ręczny odsłuch wszystkich komunikatów NVDA.
- `--smoke-runner-self-test`: kod 0. Każdy z pięciu izolowanych procesów
  kontrolnych main/probe/STA/dispatcher/cleanup zakończył się kodem 1,
  ze wskazaniem właściwego wymuszonego błędu i bez komunikatu sukcesu.
- `--tidal-webview-smoke` poza ograniczonym środowiskiem testowym, po uzyskaniu
  zgody na uruchomienie: kod 0. Bez polityki odtwarzania hosta: NotAllowedError;
  z polityką: playing. Test używa cichego WAV, bez wyciszenia elementu audio,
  prywatnego profilu i komunikatu hosta. Nie korzysta z konta TIDAL.

## Niepowodzenia pośrednie i ograniczenia

Pierwszy test procesów kontrolnych wykrył nieczytelne polskie znaki w
przekierowanym stderr. Wymuszono UTF-8 w procesie testowym; po poprawce
wszystkie pięć przypadków przeszło.

Pierwsze uruchomienie rzeczywistego WebView2 w ograniczonym środowisku
przekroczyło czas oczekiwania na nawigację. Test zwrócił kod 1 i diagnostykę,
zamiast pozostawiać nieobsłużony wyjątek CLR. Nie zaliczono go jako sukcesu.
Ten sam test bez zmian przeszedł przy zatwierdzonym uruchomieniu poza tym
środowiskiem. Różnica wskazuje wpływ środowiska, ale nie identyfikuje dokładnej
przyczyny przekroczenia czasu.

Po udanym teście dwa prywatne profile tymczasowe WebView2 nie mogły zostać
od razu usunięte z powodu blokad plików. Zostały jawnie zgłoszone i pozostawione;
nie zamykano procesów przeglądarki użytkownika. Nie są profilem AMC ani Chrome
i nie zawierają logowania użytkownika z tego testu.

Obsługa wyjątków dotyczy wyłącznie testów zarządzanych. Nie jest gwarancją
odzyskania po awarii natywnego komponentu, przepełnieniu stosu czy błędzie
produkcyjnego odtwarzacza. Nie przeprowadzono w tej zmianie ręcznego testu
NVDA całej aplikacji; nie zmieniano jej kontrolek.

## Odtwarzanie TIDAL

Pełne odtwarzanie w AMC nadal nie jest potwierdzone. Zaliczenie syntetycznego
testu WebView2 nie dowodzi pełnego odtwarzania, uprawnień abonenta ani DRM.
Osobny wynik próby Embed i analiza oficjalnego kodu:
[`TIDAL_WERYFIKACJA_PELNEGO_ODTWARZANIA_2026-09-09.md`](../TIDAL_WERYFIKACJA_PELNEGO_ODTWARZANIA_2026-09-09.md).

Pytanie do dostawcy przygotowano jako szkic, bez wysyłania go w imieniu użytkownika.
