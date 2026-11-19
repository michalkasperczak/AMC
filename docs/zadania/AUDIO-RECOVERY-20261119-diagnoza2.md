# Przełączanie audio — dalsza diagnoza po nieudanej integracji

## Aktualne zlecenie i status

**Etap diagnostyczny zakończony: przyczyną jest spóźnione PlaybackStarted
nadpisujące ochronną pauzę hosta. Trwa osobna minimalna poprawka produktu.**
Codex `6c70141` dostarczył kontrolowany przeplot; rodzic sprawdził 749 źródeł
stagingu i własnym uruchomieniem potwierdził późny start jako RED exit 1.
Macierz źródeł/konfiguracji nie potwierdziła stałej winy Release ani #26.
[Aktualny odbiór diagnozy i etap RED/GREEN](AUDIO-RECOVERY-20261120-late-start-odbior.md):
`deleg_de2324a4`, osobne worktree od `6c70141`, maksymalnie dwa celowane cykle.
Review `deleg_9a50915b` zakończony **NIE** dla `7b0a60b`: brak granic flagi
przy końcu/Stop kolejki i późny reset next/previous. Nowych RED jeszcze brak;
`deleg_c4e52a18` prowadzi drugi i ostatni uzgodniony cykl od testów hosta.
Nie ma integracji ani nowego wydania.
To nowsza decyzja niż zatrzymanie po poprzednim limicie cykli. Nie oznacza
nieograniczonych prób zmian, zgody na API, GUI, fizyczne audio ani instalację.
Zadanie zapisu/wycinania nagrań pozostaje osobnym następnym zadaniem.

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
