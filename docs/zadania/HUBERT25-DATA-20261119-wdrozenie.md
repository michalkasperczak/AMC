# Hubert #25 — dane, datowanie i trwałość odświeżania (2026-11-19)

## Zakres i tożsamość

- Worktree: `/home/michal/.hermes/profiles/amc/cache/scratch/amc-hubert25-data-20261119`
- Branch: `codex/amc-hubert25-data-20261119`
- Baza: `80c257668ef3abd07607b97c4a1561222d67986d`
- Windowsowy staging testów: `C:\Users\Michal\AppData\Local\AMC-Hermes-Build\hubert25-data`
- SDK: `C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe`, SDK `8.0.423`

To jest wyłącznie etap danych/trwałości #25. Nie zmieniono GUI, harmonogramu,
Spotify/CDP, rozdziałów, `WindowsMediaOutput`, workflow/release ani instalatora.
Nie wykonano migracji masowej ani czyszczenia istniejących flag `IsNew`.

## Zmiana

1. **Flaga „nowe” przy niepokrywających się oknach.**
   `PodcastLibraryUpdater` pamięta czas poprzedniego udanego odświeżenia przed
   jego aktualizacją. Gdy bieżące ograniczone okno nie ma żadnego wspólnego
   identyfikatora z trwałą biblioteką, tylko nieznana pozycja z jawną datą po
   tym sukcesie dostaje `IsNew=true`. Starsza i niedatowana pozycja pozostają
   nie-nowe. Ścieżka z pierwszym znanym identyfikatorem, członkostwo, postęp,
   odczytane pozycje, zakładki i archiwum nie są zerowane.

2. **Kanały YouTube.**
   `MergeChannelDocuments` nie obcina już połączonych, dwustuelementowych
   kandydatów `/videos` i `/streams` przed `CompleteFromChannelFeed`.
   Ostateczne uporządkowanie i limit 100 pozostają w
   `CompleteFromChannelFeed`, już po nałożeniu dokładnych dat RSS. Dzięki temu
   niedatowane filmy z `/videos`, których RSS zna datę, nie są wcześniej
   wypychane przez stare, datowane archiwa transmisji.

3. **Trwałość automatycznego odświeżania.**
   `LitePodcastRefreshCoordinator` zapisuje każdy ukończony kanał przez
   pojedyncze `ApplyRefresh` przed przejściem do następnego. Usunięto
   odroczony `pendingBatch` na końcu pętli. Jest to celowa zmiana starego
   grupowego zapisu: `MarkRefreshAttempt` następuje przed pobraniem, więc
   zamknięcie procesu po sukcesie pierwszego z N kanałów nie może utracić jego
   wyniku ani odroczyć go wyłącznie przez zapisaną próbę. Nie wprowadzono
   ogólnego kosztownego potoku ani zapisu całej biblioteki; zapis nadal jest
   krótką transakcją jednego kanału.

## TDD: rzeczywiste RED → GREEN

### 1. Flagi nowych pozycji

Nowy przypadek: brak wspólnego identyfikatora, jedna pozycja po poprzednim
sukcesie, jedna starsza i jedna bez daty.

- **RED:**
  ```text
  dotnet run --project tests\AccessibleMediaController.Core.SmokeTests\AccessibleMediaController.Core.SmokeTests.csproj --no-restore -- --podcast-library-update
  ```
  kod `1`, `Oczekiwano „False”, otrzymano „True”` dla starego albo
  niedatowanego elementu.
- **GREEN:** ten sam command po zmianie, kod `0`.

Stary test RSS, który zakładał nowość bez poprzedniego sukcesu, został
uszczegółowiony: najpierw wykonuje rzeczywiste udane odświeżenie, aby miał
punkt odniesienia. To odpowiada nowemu bezpiecznemu wymaganiu, a nie zmienia
produkcji przez dopasowanie testu.

### 2. YouTube: ponad 100 starych transmisji i daty RSS

Nowy przypadek łączy 100 niedatowanych filmów `/videos` z 100 starszymi,
datowanymi transmisjami. RSS daje dokładne, nowsze daty pierwszym 20 filmom.

- **RED:**
  ```text
  dotnet run --project tests\AccessibleMediaController.Windows.SmokeTests\AccessibleMediaController.Windows.SmokeTests.csproj --no-restore -- --youtube-collection-normalization
  ```
  kod `1`, `Ponad sto datowanych starych transmisji obcięło niedatowane filmy przed nałożeniem dat RSS.`
- **GREEN:** ten sam command, kod `0`,
  `OK: ograniczone kanały i playlisty publicznego YouTube`.

### 3. Przerwanie po pierwszym z N kanałów

`AccessibleMediaController.LiteHost.ProtocolTests` linkuje teraz rzeczywisty
`LitePodcastRefreshCoordinator` oraz jego rzeczywiste zależności kolekcji
YouTube. Test buduje dwa RSS-y, kończy pierwszy sukcesem, przerywa drugi
nieobsługiwanym błędem, zamyka właściciela bazy i otwiera bazę ponownie.

- **RED:**
  ```text
  dotnet run --project tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj --no-restore -- --podcast-progress
  ```
  kod `1`, `wynik pierwszego kanału musi przetrwać ponowne otwarcie bazy po przerwaniu N-elementowego przebiegu`.
- **GREEN:** ten sam command, kod `0`,
  `Wszystkie zestawy przeszly (1).`

## Końcowa weryfikacja

Każdy Windowsowy command wykonano w lokalnym stagingu NTFS z istniejącym
cache NuGet, po offline restore:

```text
dotnet restore <projekt> --source C:\Users\Michal\.nuget\packages --ignore-failed-sources
```

Wyniki końcowe:

1. `Core.SmokeTests --podcast-library-update`: kod `0`.
2. `Windows.SmokeTests --youtube-collection-normalization`: kod `0`;
   istniejące regresje normalizacji, dat RSS, transmisji, duplikatów i limitu
   kolekcji przeszły razem z nowym przypadkiem.
3. `LiteHost.ProtocolTests --podcast-progress`: kod `0`; obejmuje istniejące
   zapisy postępu, inbox, odczytane/odtworzone, ulubione, zakładki, źródła,
   automatyczną kolejkę oraz nowy ponowny odczyt bazy po przerwaniu.
4. Rzeczywisty konsument z linkowanym koordynatorem:
   ```text
   dotnet build src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj --no-restore --nologo
   ```
   kod `0`, `Ostrzeżenia: 0`, `Liczba błędów: 0`.

Nie testowano żywego YouTube, logowania, GUI, audio, instalatora ani głównego
komputera; nie są one objęte tym etapem. Nie pushowano i nie zmieniano issue
ani wspólnych rejestrów.
