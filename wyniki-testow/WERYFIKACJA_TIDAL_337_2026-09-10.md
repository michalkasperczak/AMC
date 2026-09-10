# AMC 337 — weryfikacja przekazywania logowania

## Potwierdzone w źródłach

- Logowanie jest oficjalnym Authorization Code z PKCE, nie anonimowym Client
  Credentials. Hasło nie przechodzi przez AMC.
- Przy otwarciu przekazywano do SDK Client ID, token użytkownika, ID użytkownika,
  termin ważności w milisekundach oraz faktycznie przyznane zakresy.
- Wznowienie tego samego załadowanego materiału pomijało pobranie aktualnego
  poświadczenia. Naprawiono to bez dodatkowego load/reset, z zachowaniem pozycji.
- Startowy odczyt poświadczeń przez SDK wymaga obsługi braku logowania kodem
  A0001. Mostek wcześniej rzucał ogólny Error; teraz zwraca oczekiwany kod.

Te luki mogły wpływać na inicjalizację i wznowienie po długiej pauzie. Nie
udowodniono, że były przyczyną przyznania 30-sekundowych próbek.

## Testy

- JavaScript: 15 testów mostka, wszystkie zaliczone. W tym niepełne i wygasłe
  poświadczenia, zmiana tokenu przy resume, ochrona pozycji, brak powtórnego
  ładowania, kod startowego braku logowania, powiadomienie dostawcy SDK,
  pominięte zdarzenie metadanych, deduplikacja raportu, błędy i koniec próbki.
- Release: kompilacja bez błędów i ostrzeżeń.
- Pełny zestaw Core i Windows: zaliczony podczas build.ps1 -Publish.
- Testy poświadczeń HTTP używają własnego HttpMessageHandler i sztucznych
  tokenów. Nie wykonują zmian kolekcji ani odtwarzania na prawdziwym koncie.
- Raport rozróżnia brak danych, próbkę i FULL zgłoszone przez serwer; nawet
  FULL nie jest opisane jako potwierdzone odsłuchem. Testy odrzucają dowolny
  tekst zamiast oczekiwanych identyfikatorów stanu i powodu.
- Raport i log potwierdzają jedynie fakt odczytu poświadczenia przez SDK,
  nie kopiują wartości tokenu, identyfikatora użytkownika ani sekretu aplikacji.
- Test kontrolki: nazwa przycisku przez UI Automation to „Diagnostyka
  odtwarzania”; raport jest wielowierszowym polem tylko do odczytu z obsługą
  standardowego zaznaczania tekstu. Okna testowe nie były pokazywane.

## Granice

Nie uruchomiono nowego AMC na koncie użytkownika, nie wykonano ponownego
logowania i nie testowano pełnych utworów ani osadzania pełnej strony TIDAL.
Właściwy odsłuch NVDA, Tab, Alt+D, kopiowanie i powrót Escape wymagają odbioru
według INSTRUKCJA_0.1.0-alpha.337_PL.md. Test kontrolki tekstowej i nazwy
przycisku nie zastępuje testu mowy NVDA.

Podczas publikowania pozostawiono uruchomiony proces i cały folder 336.
Nowy pakiet powstał osobno. Nie usuwano ZIP-ów bez potwierdzonego odpowiednika
w GitHub Releases; publikacja źródeł i paczki są raportowane oddzielnie.
