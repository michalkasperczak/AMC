# Spotify: polityka lokalnego wyjścia — odbiór rodzica

## Werdykt

**Potwierdzone ograniczone wyniki offline; kandydat zachowany osobno,
bez integracji do kanonicznej produkcji.**

Codex `deleg_fd64ca45` dostarczył warunkowy werdykt dla
`7a26f3a75eb65e67e65f89324cd0b399f609d20b`, poprawiającego `06bb4b0`
względem `e758ae4`. Rodzic przeczytał raport i delty produkcji/testów oraz
zbudował i uruchomił zamrożony commit niezależnie w Release.
To nie odbiera wspólnego WindowsMediaOutput, aktywnego Chrome ani Denona.

## Zakres zmian i dowodu

- Zwykła zmiana systemowego domyślnego wyjścia bez ręcznej preferencji nie
  nakłada ochronnej pauzy. Utrata ręcznie wybranego endpointu nadal ją nakłada.
- Watcher rejestruje zmianę stanu/usunięcie endpointu także przy identycznym
  domyślnym ID; tracker grupuje sygnały. Zwykli lokalni konsumenci zachowują
  swój filtr `ShouldFollow`.
- Koordynator sprawdza generację i referencję pozycji po await; test zatrzymuje
  odczyt dostępności, wykonuje Stop i nowy Play tej samej pozycji, a następnie
  uwalnia callback. Sprawdzono też powtórzenie sygnału po jawnym wznowieniu.
- Soloist pozostaje no-op dla lokalnego sinka. Brak nowych komend Connect.

Test routingowy uruchamia rzeczywisty tracker i koordynator, ale sam łączy
ich wywołania w fixture. Wpięcie `NotificationClient → watcher → callback hosta
→ koordynator` zostało przeczytane w źródle, nie odtworzone natywnym zdarzeniem
Windows. Nie nazywać tego żywym pomiarem całej drogi odłączania urządzenia.
Nie rozszerzać testu opóźnienia dostępności na gwarancję wszystkich możliwych
przeplotów ani opisu powrotu endpointu na osobny wykonany test, jeśli go nie ma.

## Weryfikacja rodzica

Pierwsza kontrola dawnego stagingu wykazała 746/747 plików identycznych i jedną
różnicę w Program.cs testów: pojedynczą końcową spację w deklaracji helpera.
Rodzic nie uruchomił wtedy istniejącej DLL jako dokładnego artefaktu commita.
Nie był to funkcjonalny RED produktu. Nie zmieniano tego stagingu ani jego logów.

Zamiast tego wyeksportowano dokładny `7a26f3a` do nowego, pustego katalogu NTFS
`spotify-7a26f3a-parent` i sprawdzono obecność śledzonych plików.
Po wykonaniu rodzic porównał także 747 plików źródłowych/projektów ze ścisłym
`7a26f3a`: bez różnic bajtowych.
Prywatny .NET SDK 8.0.423:

- `dotnet restore tests/AccessibleMediaController.SoloistAdapterTests/AccessibleMediaController.SoloistAdapterTests.csproj --ignore-failed-sources -v:minimal`: exit 0.
- `dotnet build` tego projektu, `-c Release --no-restore -v:minimal`: exit 0,
  zero ostrzeżeń i błędów. W tym samym buildzie powstał rzeczywisty LiteHost.
- Nowa `amc_soloist_adapter_tests.dll`: exit 0 po 3,08 s,
  `Soloist adapter and offline output-policy checks passed`, stderr pusty.
- SHA DLL: `bfbce8fdcae078d7a2e3b93596ff3cc085b68846091910e4f9f8ba84fef38c97`.
- `AMC_SOLOIST_TEST_EXE` jawnie pusty; nie wykonywano ścieżki rzeczywistej
  binarki ani aktywnego konta. Testy adaptera stosują wstrzyknięty transport.

Nie powtarzano niezmienionych RED wykonawcy ani całego Windows Smoke.
Wykonawca zachował RED zwykłej zmiany default i spóźnionego callbacku.
Jego selektor `--default-output-follow-only` miał exit 1 także na czystym
`e758ae4`: blokuje go wcześniejsza asercja źródłowego okablowania
`WindowsMediaOutput`, nie nowe asercje trackera. To nie zielony cały selektor
ani samodzielny dowód funkcjonalnego błędu WindowsMediaOutput.

## Dowody i następny krok

Raport wykonawcy pozostaje w zamrożonym commicie:
`docs/zadania/AUDIO-RECOVERY-20261119-spotify-policy-codex-review.md`.
Nie przenoszono jego produkcji na kanoniczną gałąź.

Dowody rodzica wyłącznie lokalnie w profilu AMC:
`cache/scratch/amc-spotify-policy-parent-20261119/` — `source-verification.json`
(pierwsza odmowa uruchomienia), `fresh-source.json`, `fresh-results.json`,
raw stdout/stderr i skrypty wykonania.

Najpierw rozstrzygnąć wspólną blokadę audio zgodnie z nowym zleceniem Michała
[ograniczonej diagnozy](AUDIO-RECOVERY-20261119-diagnoza2.md), następnie wybrać
jedną spójną bazę i dopiero przyjąć integrację. Nie scalać odrzuconych merge'ów
w ciemno. GUI/NVDA, Chrome/Spotify, odłączenie Denona, odsłuch, instalacja,
push i publikacja nie były wykonywane. Zadanie zapisu/wycinania nagrań jest
osobne i nie zostało przez ten odbiór zakończone.
