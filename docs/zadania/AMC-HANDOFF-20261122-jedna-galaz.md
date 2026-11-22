# AMC — przekazanie po alpha.513: jedna aktywna linia pracy

## Decyzja Michała z 22.11.2026

Codex i Hermes nie mają pracować równolegle nad tymi samymi zadaniami AMC.
Codex prowadzi bieżącą pracę; Hermes otrzymuje konkretne ścieżki do raportów
lub zadań tylko wtedy, gdy Michał zdecyduje przekazać mu etap. Ta decyzja
zastępuje wcześniejszy pomysł utrzymywania równoległych wykonawców.
Osobne pliki zadań służą porządkowi diagnoz, a nie uruchamianiu drugiej pracy.

## Stan źródeł i wydania

- Jedna aktywna gałąź dalszych zmian: `codex/alpha512-update-candidate-20261122`
  w `michalkasperczak/AMC`, oparta na źródłach opublikowanego alpha.513
  (`7751dee7` przed dzisiejszymi zmianami). Obie diagnozy z 22.11 są już
  w tej gałęzi. Dawne gałęzie `codex/audio-cut-diagnostics-20261122` i
  `codex/apple-account-20261122` są zapisami pośrednimi, nie aktywnymi torami.
- Kanoniczny katalog na D `D:\Projekty Codex\AMC - integracja wxPython`
  zawiera bieżące raporty, rejestr zadań i test Escape. Jego checkout Git
  jest jeszcze na starszej gałęzi `codex/amc-test-public-20261109` z pięcioma
  commitami presetów i zastanymi niezatwierdzonymi plikami dokumentacji.
  Wspólny przodek tej gałęzi i źródeł wydania alpha.513 to `2695050`;
  każda ma własne późniejsze commity. Nie nadpisywać plików, nie resetować
  ani nie robić automatycznego merge w tym checkoutcie. Przed przeniesieniem
  D na aktywną linię trzeba osobno zachować i porównać te zmiany.
- Dzisiejszy kod to źródła i testy. Nie powstała nowa paczka, instalacja
  ani GitHub Release po alpha.513. Michał sam aktualizuje instalację.

## Kolejność

1. Michał może sprawdzić presety w alpha.513. Wynik zapisać przy zadaniu
   presetów. Nie wymaga to pracy Hermesa równolegle.
2. Hubert sprawdza na własnym profilu bibliotekę ponad 50 tys. elementów
   i pobieranie YouTube; jego wynik jest potrzebny do dalszych poprawek.
   Nowy link Hermanic może pojawić się dopiero przy kolejnej transmisji.
3. Codex bada wycinanie: lokalne dwa cięcia WAV/MP3 i routing Escape przeszły,
   ale zniknięcia plików w synchronizowanym folderze nie odtworzono.
   Po porównaniu osi FFmpeg z szacunkiem Windows skorygowany test wszystkich
   formatów przechodzi; dokładność granicy MP3 nadal wymaga oceny audio.
   Szczegóły: `docs/zadania/AUDIO-EDIT-20261122-dwa-ciecia-diagnoza.md`.
4. Codex prowadzi oddzielny zakres Apple Music: wymuszenie widocznego logowania
   po „Tak” i rozróżnienie błędów 401/braku połączenia podczas wyszukiwania są
   poprawione w źródłach (commit `15951987`); działanie na żywym koncie, NVDA
   i pełne odtwarzanie dokładnie wskazanego utworu pozostają do odbioru.
   Szczegóły: `docs/zadania/AM-008-20261122-wylogowanie-i-odtwarzanie.md`.

## Następny konkretny krok

Kontynuować na jednej gałęzi `codex/alpha512-update-candidate-20261122`:
najpierw zbadać granicę MP3 i bezpieczny wyścig zapisu/Escape na
sztucznych plikach. Rozróżnienie Apple Music 401 i braku połączenia ma
celowane testy, ale wymaga odbioru w widocznym interfejsie i NVDA.
Nie używać nagrań ani tokenów Michała. Rzeczywisty odbiór
MusicKit i katalogu synchronizowanego wymaga odrębnych, bezpiecznych prób.

## Materiał do dokumentacji użytkownika

Brak nowej aktualizacji po alpha.513. Test Escape wskazuje, że powrót na listę
nie jest poleceniem anulowania cięcia; nie wyjaśnia jeszcze incydentu Dwójki.
Potwierdzenie „Konto Apple Music” po przyszłej aktualizacji ma uruchomić
widoczne logowanie. Wyszukiwanie ma podpowiedzieć ponowne logowanie przy 401
i ponowienie próby przy błędzie połączenia. Pełne odtwarzanie wskazanego
utworu nie jest jeszcze potwierdzone.
