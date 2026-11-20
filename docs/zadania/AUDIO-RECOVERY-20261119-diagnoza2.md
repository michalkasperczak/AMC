# Przełączanie audio — dalsza diagnoza po nieudanej integracji

## Aktualne zlecenie i status

**Diagnoza zakończona; ostatni kandydat `8941e34` NIE do integracji.
Dwa uzgodnione cykle wykorzystane, dalsze poprawki zatrzymane.**
`6c70141` dowiódł pierwotnego wyścigu, `7b0a60b` naprawił direct fallback,
a `8941e34` konkretne luki naturalnego A→B i kolejki Stop. Nowe A→B ma RED/GREEN,
pozostałe kontrakty i pierwotny late-fallback GREEN; rodzic sprawdził 749 źródeł
oraz sam uruchomił Release, exit 0. Review `deleg_edbfee20` wskazał jednak
statyczny przeplot A→B + fallback B przed późnym Started(B): nowsza pauza hosta
może zostać cofnięta przez wyjątek nowej pozycji. Nie wykonano łączonego RED
ani pomiaru dźwięku. Nie uznawać pojedynczych GREEN za odbiór tej granicy.
[Aktualny odbiór i punkt STOP](AUDIO-RECOVERY-20261120-late-start-odbior.md).
Nie ma integracji ani nowego wydania; nie rozpoczynać trzeciego cyklu.
Ewentualne wznowienie wymaga osobnej decyzji. Bez API, GUI, sprzętu i instalacji.
Nagrania mają osobny plan; DirectCut RS jest zapisanym scenariuszem na później.

## Co już wiadomo

- `3948aec` poprawia aparaturę hosta. Debug wykonawcy i rodzica przeszedł.
- Po połączeniu z bieżącą bazą powstał `aef4626`; świeży Release z tego commita
  buduje się bez błędów i ostrzeżeń, ale HandlerTests kończy się exit 1:
  `transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku`.
- Scalenie cofnięto `8d1da71`. Kanoniczne `src/tests` wróciły do przyjętej bazy,
  #26 pozostał zintegrowany. Nie instalowano i nie publikowano tej próby.
- To nie jest dowód, że problem powodują rozdziały YouTube, Release albo
  aparatura. Każde z tych wyjaśnień wymaga pomiaru rozróżniającego.

[Pełny wcześniejszy pomiar](AUDIO-RECOVERY-20261119-parent-check.md).

## Wykonawca i izolacja

Codex: `deleg_12ac386e`, `sa-0-436036e4`.
Worktree `amc-output-investigation2-20261119` w scratch profilu AMC,
gałąź `codex/amc-output-investigation2-20261119`, baza
`aef4626a0034e3dde098c642794edba314e31650`. Baza zawiera odrzuconą próbę
połączenia wyłącznie do odtworzenia błędu; nie jest nowym zaakceptowanym źródłem.
Kanoniczny checkout i wspólne rejestry prowadzi rodzic, nie wykonawca.

## Cel i ograniczony eksperyment

Najpierw ustalić przyczynę, nie zmieniać produkcji. Przeczytać dokładny test,
przepływ stanów `_filesPaused`, callbacki startu/fallbacku i istotną deltę
`3948aec → aef4626`. Następnie jedna główna deterministyczna sonda z kontrolą
lub mała macierz A/B, która rozróżnia:

1. Nadal niepełną gotowość aparatury, np. wcześniejszy callback startu docierający
   po ochronnej pauzie albo oczekiwanie tylko na wejście do fake Play.
2. Błąd produktu: publiczne polecenie i stan hosta nie zgadzają się mimo
   właściwie zakończonego przełączenia.
3. Różnicę konfiguracji Debug/Release lub składu wspólnego commita.

To hipotezy, nie ustalenia. Bez pętli aż zieleń, sleepów zastępujących gotowość,
setek przebiegów, osłabiania asercji jawnego wznowienia tej samej pozycji oraz
szerokich audytów. Ochronna cisza i rozdzielenie user-pause/mute/fallback zostają.

## Wynik wymagany przed kolejną zmianą

Raport PL z faktycznymi poleceniami, identyfikacją źródeł i DLL, raw stdout/stderr,
statusami oraz kontrolowanym przeplotem potwierdzającym przyczynę. Możliwy osobny
commit sondy/testu i raportu, bez push. Jeśli przyczyny nie uda się potwierdzić,
jasno opisać niewiadomą i najmniejszy potrzebny następny pomiar. Nie podawać
hipotezy jako wyjaśnienia i nie ogłaszać naprawy po samym pojedynczym PASS.

Dopiero potwierdzona przyczyna pozwoli wybrać dalszą minimalną poprawkę w ramach
uzgodnionego zadania; nie scalać ponownie odrzuconego SHA w ciemno. Osobny odbiór
Spotify `7a26f3a` nie usuwa tej blokady i nie jest częścią tej sondy.
