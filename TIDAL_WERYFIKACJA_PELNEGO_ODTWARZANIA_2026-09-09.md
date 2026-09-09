# TIDAL — wynik weryfikacji pełnego odtwarzania

Data: 9 września 2026. Stan źródeł AMC przed tą analizą: cc92e15,
alpha.335. Test przeglądarkowy nie jest testem silnika AMC.

## Wynik

Nie potwierdzono pełnego odtwarzania w AMC ani w zwykłym Embed.
Potwierdzono odtwarzanie przekraczające 30 sekund na stronie TIDAL po
zalogowaniu. Nie ma podstaw do zalecania kolejnego identycznego logowania.

## Zaobserwowane zachowanie

Test porównawczy dotyczył tego samego publicznego utworu:
[Tańcz — Daria ze Śląska](https://tidal.com/track/518279266).

- Embed przed odtwarzaniem pokazywał katalogowy czas 3:35. Po rozpoczęciu
  czas zmienił się na 0:30; po zakończeniu pojawiły się linki rejestracji
  i logowania. Sam katalogowy czas nie potwierdza pełnego odsłuchu.
- Link logowania otworzył oficjalną stronę. Użytkownik sam się zalogował.
  Nie odczytywano ani nie kopiowano poświadczeń przeglądarki.
- Odświeżony Embed nadal odtwarzał próbkę i ponownie proponował logowanie.
- Na głównej stronie TIDAL odtwarzanie postępowało przez 0:57, a następnie
  zostało wstrzymane przy 1:09 z 3:35. Nie odtwarzano całego albumu ani nie
  wykonywano zmian w kolekcji. Nie testowano ponownego startu przeglądarki.
- Ustawienie jakości na stronie: Max, streaming adaptacyjny włączony.
  Bieżący odtwarzacz podawał 16 bit / 44,1 kHz. To odczyt parametrów UI,
  nie pomiar bit-perfect na wyjściu Windows ani dowód jakości silnika AMC.

## Publiczny kod oficjalnego Embed

Sprawdzono commit `0f4fb0fe60981189e5150632a8df6168f3247dc6` repozytorium
`tidal-music/embed-player`. Nie uruchamiano zmodyfikowanej kopii Embed ani
nie używano identyfikatorów lub tokenów należących do innych aplikacji.

- [Inicjalizacja](https://github.com/tidal-music/embed-player/blob/0f4fb0fe60981189e5150632a8df6168f3247dc6/src/client/js/playback/init.js)
  ustawia domyślnego dostawcę poświadczeń.
- [Dostawcy poświadczeń](https://github.com/tidal-music/embed-player/blob/0f4fb0fe60981189e5150632a8df6168f3247dc6/src/client/js/playback/auth-provider.js):
  domyślny zwraca identyfikator klienta i puste żądane zakresy, bez tokenu
  i identyfikatora użytkownika. Osobny dostawca Nostr obsługuje poświadczenie
  użytkownika i zakres playback.
- [Uruchomienie Nostr](https://github.com/tidal-music/embed-player/blob/0f4fb0fe60981189e5150632a8df6168f3247dc6/src/client/js/nostr.js)
  zależy od obecności interfejsu Nostr w przeglądarce.
- [Szablon okna po próbce](https://github.com/tidal-music/embed-player/blob/0f4fb0fe60981189e5150632a8df6168f3247dc6/src/server/render.js)
  zawiera zwykły link do tidal.com/login?autoredirect=true w nowej karcie.
- [Mostek komunikatów](https://github.com/tidal-music/embed-player/blob/0f4fb0fe60981189e5150632a8df6168f3247dc6/src/client/js/message-bridge.js)
  obsługuje polecenia play/pause. Sam ten plik nie potwierdza kompletnego,
  wspieranego API przewijania, jakości ani trwałego logowania dla AMC.

Wniosek: brak przejęcia zwykłego logowania przez Embed jest zgodny z tą
implementacją. To nie dowodzi, że produkcja używa identycznego commita.
Dokumentacja ogólnie przewiduje pełny odsłuch przez Embed dla abonentów,
ale nie znaleziono w sprawdzonym kodzie zwykłego przepływu logowania, który
rozwiązuje nasz przypadek. Potrzebne jest wyjaśnienie dostawcy. Nie instalować
dodatku Nostr, nie tworzyć kluczy ani nie łączyć nowej tożsamości bez osobnej
decyzji użytkownika i sprawdzenia wspieranej ścieżki.

## Jakość w AMC — granice obecnej implementacji

AMC żąda HI_RES_LOSSLESS z adaptacyjnym doborem jakości. Żądanie nie jest
potwierdzeniem otrzymania HiRes. SDK 0.20.1 zawiera w kontekście rzeczywistą
jakość, głębię bitową, częstotliwość, kodek i przepustowość. Mostek JS
przekazuje je przy zmianie materiału, ale obecna część C# nie wykorzystuje
pól jakości i głębi bitowej. Brakuje również obsługi zmiany jakości w trakcie
odtwarzania. Nie deklarować maksymalnej jakości na podstawie samego ustawienia.

Do przyszłego wdrożenia: faktyczna jakość i głębia bitowa, zmiany adaptacyjne,
czyszczenie starych parametrów przy zmianie utworu oraz jawne „brak danych”.
Najpierw wymagane jest potwierdzone pełne odtwarzanie.

## Warunek wznowienia integracji pełnych utworów

1. Dostawca potwierdza wspierany przepływ logowania i dostęp dla aplikacji
   desktopowej albo zwykły Embed rzeczywiście udostępnia pełne audio.
2. Izolowany test potwierdza naturalny postęp poza 30 sekund, przewinięcie
   poza próbkę, przejście do następnego materiału i zachowanie sesji po
   zamknięciu oraz ponownym otwarciu.
3. Osobny test NVDA: fokus, Escape, pauza, brak samoczynnego przechodzenia
   Kolejki po błędzie, wyciszenie i rzeczywiste parametry.
4. Dopiero wtedy zmiana silnika w wydaniu AMC. Odtwarzanie na stronie TIDAL
   nie jest ukończoną integracją w natywnym interfejsie AMC.

Nie zmieniono uruchomionego odtwarzacza ani konfiguracji konta. Nie ma nowego
wydania aplikacji z pełnym TIDAL. Po osobnej zgodzie użytkownika pytanie
opublikowano 9 września 2026 r. na oficjalnym forum:
[dyskusja TIDAL nr 384](https://github.com/orgs/tidal-music/discussions/384).
Potwierdzono stronę opublikowanego pytania, nie odpowiedź ani przyznanie dostępu.

## Testy zabezpieczenia programu testowego

Kompilacja, pełne zestawy Core i Windows oraz pięć izolowanych przypadków
błędów testowych przeszły. Syntetyczny test WebView2 przeszedł poza ograniczonym
środowiskiem; wewnątrz niego wcześniej zgłosił przekroczenie czasu. Dokładne
wyniki i ograniczenia: [raport testów](wyniki-testow/WERYFIKACJA_TIDAL_I_TESTOW_2026-09-09.md).
Nie jest to potwierdzenie pełnego odtwarzania TIDAL ani nowa wersja AMC.

## Dokumentacja dostawcy

- [TIDAL Developer Terms](https://developer.tidal.com/documentation/guidelines/guidelines-developer-terms)
- [TIDAL Embeds — overview](https://developer.tidal.com/documentation/embeds/embeds-overview)
- [Publiczny SDK — quick start z próbką](https://developer.tidal.com/documentation/api-sdk/api-sdk-quick-start)
