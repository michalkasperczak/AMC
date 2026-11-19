# Wyjścia audio — rodzicielska weryfikacja odbioru

## Werdykt

**BLOKADA wspólnego kandydata; próbna integracja cofnięta.**
Poprawiona aparatura `3948aec` przeszła w Debug u wykonawcy i rodzica, lecz
świeży wspólny build Release ponownie upadł na tej samej asercji wznowienia.
Późniejsza diagnoza `6c70141` potwierdziła spóźnione PlaybackStarted, które
nadpisuje ochronną pauzę hosta. Rodzic niezależnie odtworzył kontrolowany RED.
Minimalny `7b0a60b` przechodzi ten sam późny start również u rodzica.
Trwa review `deleg_9a50915b` granic nowej flagi hosta; bez akceptacji integracji.
[Wynik i aktualny etap](AUDIO-RECOVERY-20261120-late-start-odbior.md).

## Nowe polecenie Michała — dalsze badanie

Po powyższej blokadzie Michał wyraźnie zlecił dalsze badanie przełączania audio.
Etap `deleg_12ac386e` zakończył się dowodem przyczyny. Nowy `deleg_de2324a4`
prowadzi minimalny RED/GREEN. Nie unieważnia to zapisanych FAIL ani revertu.
[Aktualny zakres, wykonawca i kryteria](AUDIO-RECOVERY-20261119-diagnoza2.md).
Spotify `7a26f3a` ma osobny potwierdzony Release offline, bez integracji
[raport rodzica](AUDIO-RECOVERY-20261119-spotify-parent-check.md).

## Końcowy cykl i wspólna kompilacja — 19.11 po 23:03

1. Codex `deleg_96958dcb` dostarczył `3948aecc0aac374da79120ba205829d791799222`,
   zmieniając wyłącznie test hosta. Bramy fake outputów i event `audio.outputFallback`
   zamiast samej liczby outputów; produkcji nie zmieniał. Debug u wykonawcy PASS.
2. Rodzic przeczytał pełną deltę i dowód. Porównał 747 plików źródłowych/projektów
   stagingu z `3948aec`, bez różnic; SHA Program.cs
   `c85c6e74000864bf9143236adefeae256d40ca30802f634686ff3ab7e5e044aa`.
   Wykonał tam Windows `amc_lite_host_handler_tests.dll` w Debug: exit 0,
   komunikat `LiteHost automatic refresh dispatcher and protected output transport: OK`.
3. Próbne scalenie do bieżącej bazy z #26 utworzyło `aef4626a0034e3dde098c642794edba314e31650`.
   Źródła tego commita wyeksportowano do nowego, pustego katalogu NTFS
   `amc-output-integrated-aef4626`; manifest sprawdził obecność wszystkich
   3069 śledzonych plików (liczbę skorygowano z manifestu). Restore exit 0; Release build hosta i HandlerTests
   exit 0, zero ostrzeżeń i błędów.
4. Faktyczne uruchomienie tej nowo zbudowanej DLL: **exit 1**, po 4,30 s,
   `transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku`,
   `Program.cs:117`. Pipeline przerwano; planowany kolejny Windows smoke nie
   został zbudowany ani uruchomiony w tym wspólnym przebiegu.
5. Nie zmieniano asercji, nie wykonywano retry ani trzeciego cyklu produktu.
   Nie ustalono, czy różnicę powoduje konfiguracja Debug/Release, skład wspólnego
   commita czy wciąż niepełny kontrakt synchronizacji. Zielony Debug nie rozstrzyga
   tej przyczyny; końcowy werdykt wykonawcy nie utrzymał się dla integracji.
6. Próbne scalenie cofnięto przez `8d1da7124bcaadcff881f6c20929ce6a9407c9e0`.
   `git diff 8dd7278 -- src tests` po revert jest pusty: poprzednie źródła
   i odebrany #26 zostały zachowane. Commit kandydata i dowody nadal istnieją.
   Uwaga: merge i revert są w historii; ponowny zwykły merge tego samego SHA
   nie przywróci usuniętej treści. Nie wykonywać takiej operacji w ciemno.

Dowody rodzica, wyłącznie lokalnie:
`/home/michal/.hermes/profiles/amc/cache/scratch/amc-output-boundary-parent-20261119/`.
`source-verification.json`, `handler-parent.json` (Debug PASS),
`integrated-source.json`, `integrated-results.json` (świeży Release FAIL),
surowe stdout/stderr i skrypty rzeczywistego wykonania. Nie jest to odbiór
GUI, odsłuchu, aktywnego Spotify, instalatora ani wydania.

## Tożsamość kandydata i dowodu

- Odebrany wspólny kandydat i korekta: `4a791c22980157a3a449b730a16ee813dd45e1eb`.
- Obejmuje wspólny przyrost `e758ae409689bb81f7aed94379bd1f03557ddcff` oraz
  potwierdzoną naprawę ciszy przy wyłączonej ochronie po utracie urządzenia.
- Osobna polityka Spotify Chrome: `06bb4b0dbd8c5711105e914a5b79c856609d465f`.
- Kanoniczny checkout podczas kontroli: `/home/michal/projekty/AMC`,
  gałąź `codex/amc-test-public-20261109`, HEAD `ea1c0e9`.
- Staging wykonawcy: `C:\Users\Michal\AppData\Local\AMC-BuildTools\staging\amc-output-acceptance-review-20261119`.

Rodzic programowo wyliczył pliki `src` i `tests` kończące się na `.cs`,
`.csproj`, `.props` i `.targets` z Git `4a791c2`, po czym porównał ich surowe
bajty ze stagingiem: **747 sprawdzonych, zero braków i różnic**.
Jest to kontrola źródeł stagingu, nie dowód deterministycznego pochodzenia DLL.

Odczytano zapisane logi wykonawcy: testy ochronnego recovery, LiteHost HandlerTests
oraz SoloistAdapterTests zakończyły się u niego sukcesem; buildy wykazały zero
ostrzeżeń i błędów. Zachowano te wyniki jako fakty wcześniejszego przebiegu,
nie zmieniono ich na wynik późniejszej próby rodzica.

## Rzeczywisty pomiar rodzica

Z katalogu stagingu uruchomiono bez GUI, kont i fizycznego audio:

```text
C:\Users\Michal\AppData\Local\AMC-BuildTools\dotnet\dotnet.exe tests/AccessibleMediaController.LiteHost.HandlerTests/bin/Debug/net8.0-windows10.0.19041.0/amc_lite_host_handler_tests.dll
```

Wynik: **exit 1**, czas 1,73 s.

```text
System.InvalidOperationException: transport.pauseResume nie zwrócił wznowionego stanu po ochronnym fallbacku
at Program.ProtectedFallbackResumesThroughProductionTransport(...):line 83
at Program.Main():line 27
```

Przechowano stdout i stderr jako surowe bajty oraz JSON z poleceniem, czasem,
wyjściem i kodem zakończenia. Wyjście natywne dekodowano CP852, dopiero po
zachowaniu kodu. Pliki:
`/home/michal/.hermes/profiles/amc/cache/scratch/amc-output-parent-20261119/handler-parent.json`,
`handler-parent.stdout.raw` i `handler-parent.stderr.raw`.

Nie ponawiano polecenia aż do uzyskania zielonego wyniku. Nie nazwano
niepowodzenia „flaky” ani błędem wcześniejszym od zmian.

## Hipoteza wymagająca rozstrzygnięcia

Test czeka na `outputs.Count == 2`. Fabryka dopisuje nowy output jeszcze przed
`Init`, podmianą lease i zgłoszeniem `OutputDeviceFallback` do hosta.
Może zatem wywoływać `transport.pauseResume` przed ustaleniem ochronnej pauzy.
Sam ten odczyt kodu nie dowodzi, czy winna jest aparatura czy produkt.
Potrzebna jest kontrolowana brama przeplotu i rzeczywisty sygnał gotowości,
bez `Sleep` oraz bez osłabienia asercji wznowienia tej samej pozycji.

`deleg_96958dcb` / `sa-0-b1ffc2a9` rozstrzyga tylko tę granicę w worktree
`amc-output-host-boundary-20261119` od `4a791c2`: jeden końcowy cykl korekty
z zachowaniem dowodu RED/GREEN. Kanonicznych źródeł nie edytuje.

## Osobna bramka Spotify Chrome

Przyrost `06bb4b0` zbudowano i przetestowano offline przez wykonawcę, lecz
rodzic nie przyjął go jeszcze do integracji. Odczyt wykazał:

- `FollowDefaultAudioOutputAsync` przy ochronie wywołuje `Pause` także bez
  ręcznej preferencji; brak testu zwykłej zmiany domyślnego przy włączonej ochronie.
- Watcher obsługuje zmianę systemowego domyślnego, lecz ignoruje
  `OnDeviceStateChanged`/`OnDeviceRemoved` i nie zgłasza identycznego domyślnego ID.
  Brakuje dowodu dostarczenia utraty ręcznego, niedomyślnego endpointu do Chrome.

Wpływ funkcjonalny tych dwóch granic odtwarza `deleg_fd64ca45` /
`sa-0-b07db464` w worktree `amc-spotify-policy-acceptance-20261119` od `06bb4b0`.
Maksymalnie dwa celowane cykle; bez fikcyjnych komend Soloist/Spotify Connect.

## Uzasadnienie, ograniczenia i następny krok

Nie łączyć kandydatów tylko na podstawie wcześniejszych zielonych raportów.
Odebrać wyniki obu konkretnych korekt, następnie zintegrować zgodne źródła i
potwierdzić zmienioną wspólną granicę. #26 i #28 pozostają osobnymi zadaniami;
PR #29 dotyka tego samego `WindowsMediaOutput` i wymaga własnego odbioru.

Nie wykonano GUI, NVDA, Chrome, fizycznego odłączenia Denona, zmiany wyjść
Windows, logowania, instalacji, publikacji ani zamykania zgłoszeń.

## Materiał do dokumentacji użytkownika

Kontrakt pozostaje bez zmian: zwykła zmiana domyślnego wyjścia ma przenieść
lokalny dźwięk bez ponownego uruchamiania materiału. Utrata ręcznie wybranego
urządzenia przy włączonej ochronie ma pozostawić ciszę; dopiero świadoma
Spacja lub Enter pozwala wznowić tę samą pozycję na dostępnym wyjściu.
Ten dokument zapisuje nieudany pomiar i plan jego rozstrzygnięcia, nie odbiór funkcji.
