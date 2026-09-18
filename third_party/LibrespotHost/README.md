# Natywny host Librespot (drugi silnik Spotify)

`win-x64/amc_spotify_librespot_host.exe` to przypięta, zweryfikowana kopia
natywnego hosta AMC opartego na bibliotece [librespot](https://github.com/librespot-org/librespot).
Host jest **drugim** silnikiem Spotify i istnieje **obok** dotychczasowego
mostka WebView2 z oficjalnym SDK Spotify — nie zastępuje go. Paczka AMC musi
zawierać oba, bo użytkownik wybiera silnik w programie.

Plik trzymamy w repozytorium tak samo jak `third_party/BASS/win-x64/bass.dll`:
to jedyny sposób, w jaki natywne binaria trafiają do tej paczki. Budowanie nie
pobiera nic z sieci.

## Proweniencja (zweryfikowana, nie deklarowana)

| Pole | Wartość |
| --- | --- |
| Nazwa pliku | `amc_spotify_librespot_host.exe` |
| Rozmiar | 6 541 312 B |
| SHA-256 | `f5b614f01d8943b5872d9a2bb86658761198b763bb25c1647aa79268a2473c8e` |
| Cel kompilacji | `x86_64-pc-windows-msvc`, statyczny CRT |
| librespot | 0.8.0, rewizja `d36f9f1907e8cc9d68a93f8ebc6b627b1bf7267d` |
| Rewizja AMC | `791c8dbf2b3279f1bb8fee9220bf95d714168c96` |
| Kompilacja | GitHub Actions, przebieg [35381167099](https://github.com/michalkasperczak/AMC/actions/runs/35381167099) |
| `rustc` | 1.98.1 (48a229cea 2026-09-01) |

Pełny odczyt z kompilacji leży obok binarki w `win-x64/provenance-amc-host.json`
i jedzie razem z nią do paczki, więc odbiorca wydania może sam sprawdzić sumę.

## Licencja

librespot jest udostępniany na licencji MIT, Copyright (c) 2015 Paul Lietar.
Pełny tekst: `../../licenses/librespot-MIT.txt` (skopiowany z drzewa źródeł
librespot 0.8.0). Licencja trafia do paczki jako `licenses/librespot-MIT.txt`.

## Zależności natywne

Zmierzone tablicą importów PE (`dumpbin /imports`) skompilowanej binarki —
wyłącznie biblioteki systemowe Windows, żadnej dokładanej biblioteki obcej:

`kernel32.dll`, `ntdll.dll`, `advapi32.dll`, `ole32.dll`, `oleaut32.dll`,
`crypt32.dll`, `secur32.dll`, `ws2_32.dll`, `bcryptprimitives.dll`,
`api-ms-win-core-synch-l1-2-0.dll`.

CRT jest wlinkowany statycznie, więc paczka nie potrzebuje redystrybucji
Visual C++. Warstwa TLS to `native-tls`, czyli Schannel z systemu — nie
dołączamy OpenSSL, więc jego licencja tu nie występuje. Zależności crate'ów
Rust są wlinkowane w binarkę; ich licencje odpowiadają drzewu librespot 0.8.0
(`Cargo.lock` z przebiegu kompilacji jest zachowany w artefaktach sondy).

Materiały licencyjne i inwentarz wszystkich paczek z przypiętego Cargo.lock
znajdują się w `../../licenses/librespot-dependencies/INDEX.txt`. Oryginalne
archiwa 365 paczek rejestru zweryfikowano sumami z Cargo.lock. Zachowano
licencje, noty i metadane; gdy dostawca nie zamieścił osobnego pliku licencji,
do paczki dołączono jego całe archiwum źródłowe. Wykaz jest szerszy od zależności
wlinkowanych do Windows: obejmuje także narzędzia i pozostałe platformy.

Wydanie zawiera dodatkowy `AMC-Librespot-Sources-0.1.0-alpha.394.zip` ze źródłami
wszystkich paczek rejestru, pełną rewizją librespota oraz wrapperem AMC. Umożliwia
to również otrzymanie źródeł składników na MPL-2.0. W przypadku dualnego
licencjonowania priority-queue wybrano MPL-2.0. Paczek rejestru nie modyfikowano.

## Straż zgodności

`AccessibleMediaController.Windows.csproj` (cel `VerifyPinnedLibrespotHostBinary`)
liczy SHA-256 tego pliku zadaniem MSBuild `GetFileHash` przy każdym budowaniu
i publikacji. Brak pliku albo inna suma kończy budowanie błędem, więc paczka nie
wyjdzie z podmienioną lub nieobecną binarką.

Test pakowania mierzący realny katalog publikacji:

```
scripts/test-pakowanie-librespot.sh <katalog-publikacji>
```

## Aktualizacja binarki

1. Zbuduj hosta w CI (nie ręcznie) i zapisz przebieg oraz rewizje.
2. Podmień plik, `provenance-amc-host.json` i tabelę wyżej.
3. Zaktualizuj `LibrespotHostExpectedSha256` w `csproj` oraz `EXPECTED_SHA`
   i `EXPECTED_SIZE` w skrypcie testu — inaczej budowanie odmówi.
