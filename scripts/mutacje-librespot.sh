#!/usr/bin/env bash
# Sonda mutacyjna klienta/adaptera Librespot.
#
# Mierzy JEDNO pytanie: czy testy Librespota lapia zepsucie konkretnej funkcji.
# Wymogi na sonde (skill verifying-fixes-with-valid-measurements,
# references/celowanie-mutacji-w-wlasciwa-funkcje.md):
#  - kotwica FUNKCJI, nie samego wygladu linii (igla wystepuje w pliku wiele razy),
#  - twardy blad, gdy mutacja NIE weszla do pliku,
#  - wypisanie numeru zmutowanej linii do przeczytania,
#  - przywrocenie plikow przez trap EXIT, nie na koncu happy-path,
#  - bez set -e: testy CELOWO zwracaja kod 1,
#  - na koniec zielony przebieg i porownanie sum kontrolnych.
#
# Uzycie: scripts/mutacje-librespot.sh [nazwa-mutacji ...]  (bez argumentow: wszystkie)

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="${DOTNET:-/home/michal/dotnet/dotnet}"
CORE_PROJ="$REPO/tests/AccessibleMediaController.Core.SmokeTests/AccessibleMediaController.Core.SmokeTests.csproj"
CORE_DLL="$REPO/tests/AccessibleMediaController.Core.SmokeTests/bin/Debug/net8.0/AccessibleMediaController.Core.SmokeTests.dll"
CORE_TEST="Transport osobnego procesu hosta Librespot"
KLIENT="$REPO/src/AccessibleMediaController.Core/Spotify/LibrespotHostClient.cs"
ADAPTER="$REPO/src/AccessibleMediaController.Windows/Services/SpotifyLibrespotMediaOutput.cs"

RUCHOME=("$KLIENT" "$ADAPTER")
for plik in "${RUCHOME[@]}"; do cp "$plik" "$plik.przed-mutacja"; done
# Przywrocenie MUSI odswiezyc czas modyfikacji. mv/cp -p zachowuje stary czas,
# a wtedy MSBuild uznaje kompilacje za aktualna i test mierzy ZMUTOWANY binarny
# plik po przywroconym zrodle - czyli falszywy wynik.
przywroc() {
    for plik in "${RUCHOME[@]}"; do
        if [ -f "$plik.przed-mutacja" ]; then
            cat "$plik.przed-mutacja" > "$plik"
            rm -f "$plik.przed-mutacja"
            touch "$plik"
        fi
    done
}
trap przywroc EXIT

# mutuj <plik> <kotwica-funkcji> <igla> <zamiennik>
# Podmienia PIERWSZE wystapienie igly ZA kotwica funkcji. Zwraca 1, gdy nie weszlo.
mutuj() {
    python3 - "$1" "$2" "$3" "$4" <<'PY'
import sys
plik, kotwica, igla, zamiennik = sys.argv[1:5]
src = open(plik, encoding="utf-8").read()
if kotwica not in src:
    print(f"  SONDA ZLA: brak kotwicy funkcji {kotwica!r}"); sys.exit(2)
i = src.index(kotwica)
ogon = src[i:]
if igla not in ogon:
    print(f"  SONDA ZLA: igla nie wystepuje za kotwica"); sys.exit(2)
mut = src[:i] + ogon.replace(igla, zamiennik, 1)
if mut == src:
    print("  SONDA ZLA: mutacja nic nie zmienila"); sys.exit(2)
linia = mut[:src[:i].__len__() + ogon.index(igla)].count("\n") + 1
print(f"  zmutowana linia: {linia} (kotwica w linii {src[:i].count(chr(10)) + 1})")
open(plik, "w", encoding="utf-8").write(mut)
PY
}

wynik_core() {
    "$DOTNET" build "$CORE_PROJ" -v q --nologo >/tmp/mut-build.log 2>&1
    if [ $? -ne 0 ]; then echo "BUILD-PADL"; return; fi
    "$DOTNET" "$CORE_DLL" --only "$CORE_TEST" >/tmp/mut-core.log 2>&1
    if [ $? -eq 0 ]; then echo "ZIELONY"; else echo "CZERWONY"; fi
}

# ---------- definicje mutacji ----------
# Kazda mutacja: nazwa | plik | kotwica funkcji | igla | zamiennik | warstwa
mutacja() {
    local nazwa="$1" plik="$2" kotwica="$3" igla="$4" zam="$5" gdzie="$6"
    echo "=== $nazwa ($gdzie) ==="
    mutuj "$plik" "$kotwica" "$igla" "$zam" || { echo "  POMINIETO"; return; }
    local w; w="$(wynik_core)"
    echo "  Core test: $w"
    [ "$w" = "CZERWONY" ] && echo "  WERDYKT: mutacja ZLAPANA" || echo "  WERDYKT: mutant UCIEKL"
    cp -f "$plik.przed-mutacja" "$plik"
    touch "$plik"
}

CHCE=("$@")
chce() { [ ${#CHCE[@]} -eq 0 ] && return 0; for n in "${CHCE[@]}"; do [ "$n" = "$1" ] && return 0; done; return 1; }

chce klient-guard-playid && mutacja "klient-guard-playid" "$KLIENT" \
    "private bool IsCurrentPlay(long playId, string uri)" \
    "if (disposed || playId <= 0 || playId != currentPlayId) return false;" \
    "if (disposed || playId <= 0) return false;" \
    "KLIENT: guard playId w IsCurrentPlay"

chce klient-clamp-volume && mutacja "klient-clamp-volume" "$KLIENT" \
    "public async Task SetVolumeAsync(int volume" \
    "var clamped = Math.Clamp(volume, 0, 100);" \
    "var clamped = volume;" \
    "KLIENT: clamp glosnosci"

chce klient-clamp-seek && mutacja "klient-clamp-seek" "$KLIENT" \
    "public async Task SeekAsync(TimeSpan position" \
    "var positionMs = (long)Math.Max(0, position.TotalMilliseconds);" \
    "var positionMs = (long)position.TotalMilliseconds;" \
    "KLIENT: clamp ujemnego przewijania"

chce klient-pauza-w-przygotowaniu && mutacja "klient-pauza-w-przygotowaniu" "$KLIENT" \
    "public async Task PauseAsync(CancellationToken" \
    "            if (isPreparing) pauseRequestedDuringPreparation = true;
" \
    "" \
    "KLIENT: zapamietanie pauzy w trakcie przygotowania"

chce klient-stop-w-przygotowaniu && mutacja "klient-stop-w-przygotowaniu" "$KLIENT" \
    "public async Task StopAsync(CancellationToken" \
    "            if (isPreparing) stopRequestedDuringPreparation = true;
            else
            {" \
    "            {" \
    "KLIENT: zapamietanie stopu w trakcie przygotowania"

chce klient-ponowny-stop && mutacja "klient-ponowny-stop" "$KLIENT" \
    "        if (supersededByStop)" \
    "            await SendAsync(LibrespotHostContract.CommandStop, null, CancellationToken.None)
                .ConfigureAwait(false);
" \
    "" \
    "KLIENT: PONOWNE wyslanie stopu po potwierdzeniu grania"

chce klient-ponowna-pauza && mutacja "klient-ponowna-pauza" "$KLIENT" \
    "        if (supersededByPause)" \
    "            await SendAsync(LibrespotHostContract.CommandPause, null, CancellationToken.None)
                .ConfigureAwait(false);
" \
    "" \
    "KLIENT: PONOWNE wyslanie pauzy po potwierdzeniu grania"

chce adapter-guard-playid && mutacja "adapter-guard-playid" "$ADAPTER" \
    "private void OnStateChanged(object? sender" \
    "if (disposed || e.PlayId != currentPlayId) return;" \
    "if (disposed) return;" \
    "ADAPTER: guard playId w OnStateChanged"

chce adapter-clamp-volume && mutacja "adapter-clamp-volume" "$ADAPTER" \
    "public void SetVolume(int volume)" \
    "Math.Clamp(volume, 0, 100)" \
    "volume" \
    "ADAPTER: clamp glosnosci"

chce adapter-clamp-seek && mutacja "adapter-clamp-seek" "$ADAPTER" \
    "public void Seek(TimeSpan position)" \
    "var normalized = position < TimeSpan.Zero ? TimeSpan.Zero : position;" \
    "var normalized = position;" \
    "ADAPTER: clamp ujemnego przewijania"

chce adapter-pauza-w-przygotowaniu && mutacja "adapter-pauza-w-przygotowaniu" "$ADAPTER" \
    "    public void Pause()" \
    "            if (isPreparing) isPreparing = false;
" \
    "" \
    "ADAPTER: koniec przygotowania po pauzie"

# ---------- kontrola koncowa ----------
przywroc
trap - EXIT
echo "=== kontrola koncowa ==="
for plik in "$KLIENT" "$ADAPTER"; do
    if [ -f "$plik.przed-mutacja" ]; then echo "UWAGA: zostala kopia $plik.przed-mutacja"; fi
done
git -C "$REPO" diff --stat -- "$KLIENT" "$ADAPTER"
echo "src bez zmian: $(git -C "$REPO" diff --quiet -- "$KLIENT" "$ADAPTER" && echo TAK || echo NIE)"
echo "Core test po przywroceniu: $(wynik_core)"
