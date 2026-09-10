# Weryfikacja AMC alpha338 — 10 września 2026

## Zakres

Otwieranie wykonawcy/albumu/playlisty TIDAL przy przebudowie listy, stabilność
numeru nawigacji i zachowanie osłon przed spóźnionymi odpowiedziami.
Nie zmieniano poświadczeń, zakresów OAuth ani modułu odtwarzania TIDAL.

## Wykonane sprawdzenia

- Release, `dotnet build AccessibleMediaController.sln --configuration Release
  --no-restore --disable-build-servers`: kod 0, bez błędów i ostrzeżeń.
- `--tidal-interaction-smoke`: kod 0. Test na rzeczywistej kontrolce ListBox WPF
  odtwarza starą sekwencję: wymiana obiektów, utrata i odtworzenie zaznaczenia
  tego samego ID, odrzucenie kontekstu. Następnie ta sama sekwencja objęta
  ListSelectionRefresh zachowuje kontekst.
- Dodatkowo: zagnieżdżone odświeżenie, wielokrotne zaznaczenie, ręczne A–B–A,
  zniknięcie wybranego elementu, pusta lista, wyjątek i późniejsza nawigacja.
- `build.ps1 -Publish`: kod 0. Pełne zestawy Core i Windows zakończone
  powodzeniem, kompilacja bez błędów i ostrzeżeń, utworzony pakiet win-x64.
- Przegląd integracji: ApplyFilter osłania wyłącznie synchroniczną wymianę
  ItemsSource i wybór wiersza; synchronizacja TIDAL obejmuje też odtwarzanie
  wielu zaznaczeń. Zmiana tekstu filtra jawnie unieważnia oczekujące wejście.
- Nie osłabiono TidalInteractionContext.CanPresent. Przechwytywane są również
  zmiana sesji/widoku, Escape i niedostępność okna.
- ZIP: 24 wpisy, EXE zgodny przez SHA-256 z rozpakowaną wersją. ProductVersion:
  0.1.0-alpha.338. Spis obejmuje program, biblioteki, licencje, mostek SDK i
  dwie instrukcje wydania; nie obejmuje stanu użytkownika, logów ani nagrań.
- Wielkość ZIP: 71005439 bajtów.
- SHA-256 ZIP: C87D1896DC992F4174ACDCA553CA93436EC822EB19F145CF1CE491C81DBFE5E8.

## Granice i odbiór

Nie przeprowadzono odsłuchu mowy rzeczywistego NVDA w AMC 338 ani ponownego
wejścia do konta TIDAL. Nie jest to dowód usunięcia każdej możliwej przyczyny
wcześniejszego przypadku Stevie Wondera. Dodano logi startu, czasu i powodu
pominięcia wyniku, aby kolejny test był jednoznaczny.

Nie zamknięto uruchomionej 337. Build pozostawił jej cały pakiet, usunął
starszy rozpakowany 336; ZIP 336 pozostał jako możliwość odtworzenia wydania.
Nie uruchomiono 338. Pełne odtwarzanie pozostaje nierozwiązane.

`gh auth status` nie wykazał zalogowanego hosta: lokalny ZIP nie został
opublikowany w GitHub Releases i nie może zostać usunięty jako wysłany.
Wysłanie źródeł przez Git jest osobnym krokiem publikacji.
