#!/usr/bin/env bash
# Odtwarzalny przebieg testu trwalego magazynu poswiadczen Sonos (DPAPI
# biezacego uzytkownika Windows). Zarchiwizowana kopia harnessu z przebiegu
# /home/michal/projekty/amc_pomoc/sonos-credentials1/.
#
# 1. buduje minimalny harness net8.0 w WSL (SDK 8.0.425), BEZ projektu WPF i
#    bez ProjectReference, linkujac pliki produktowe wprost z tego worktree,
# 2. kopiuje TYLKO artefakty harnessu do NOWEGO losowego katalogu w Windows TEMP,
# 3. uruchamia je NATYWNIE na Windows (runtime 8.0.31) przez
#    powershell.exe -NoProfile -NonInteractive -File, wiec DPAPI jest PRAWDZIWE,
# 4. sprawdza $LASTEXITCODE, liczy hasze zrodel i binarki, zapisuje wynik.
#
# Test uzywa WYLACZNIE syntetycznych wartosci i wlasnego, swiezego katalogu w
# Windows TEMP; nie czyta i nie rusza zadnego istniejacego pliku danych AMC.
# Kod wyjscia 0 = wszystkie kontrole zaliczone. Dowolna porazka = 1.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKTREE="${AMC_WORKTREE:-$(cd "$HERE/../.." && pwd)}"
DOTNET="${AMC_DOTNET:-/home/michal/dotnet/dotnet}"
WIN_TEMP_WSL="${AMC_WIN_TEMP:-/mnt/c/Users/Michal/AppData/Local/Temp}"
WIN_TEMP_WIN="${AMC_WIN_TEMP_WIN:-C:\\Users\\Michal\\AppData\\Local\\Temp}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

RUN_ID="$(head -c 8 /dev/urandom | od -An -tx1 | tr -d ' \n')"
WIN_DIR_NAME="amc-sonos-credentials-${RUN_ID}"
WIN_DIR_WSL="${WIN_TEMP_WSL}/${WIN_DIR_NAME}"
WIN_DIR_WIN="${WIN_TEMP_WIN}\\${WIN_DIR_NAME}"

# Sprzatamy WYLACZNIE wlasny katalog przebiegu; nic obcego.
cleanup() {
  rm -rf "${WIN_DIR_WSL}" 2>/dev/null || true
}
trap cleanup EXIT

"$DOTNET" build "$HERE/csharp/Harness.csproj" -c Release /m:1 -v m \
  -p:AmcWorktree="$WORKTREE" 2>&1 | tee "$HERE/build.log"

OUT="$HERE/csharp/bin/Release/net8.0"
mkdir -p "${WIN_DIR_WSL}"
cp "$OUT/SonosCredentialStoreHarness.dll" \
   "$OUT/SonosCredentialStoreHarness.deps.json" \
   "$OUT/SonosCredentialStoreHarness.runtimeconfig.json" \
   "${WIN_DIR_WSL}/"

# Skrypt po stronie Windows. Zadnego tokenu w argumentach ani w env.
cat > "${WIN_DIR_WSL}/run.ps1" <<'PS1'
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
& 'C:\Program Files\dotnet\dotnet.exe' (Join-Path $dir 'SonosCredentialStoreHarness.dll')
$code = $LASTEXITCODE
Write-Output ("EXITCODE=" + $code)
exit $code
PS1

set +e
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass \
  -File "${WIN_DIR_WIN}\\run.ps1" 2>&1 | tee "$HERE/run.log"
STATUS="${PIPESTATUS[0]}"
set -e

# Hasze zrodel i binarki: wynik liczony programatycznie, nie przepisany.
{
  sha256sum "$WORKTREE/src/AccessibleMediaController.Core/Sonos/SonosCredentialStoreContract.cs"
  sha256sum "$WORKTREE/src/AccessibleMediaController.Windows/Services/SonosDpapiCredentialStore.cs"
  sha256sum "$HERE/csharp/Program.cs"
  sha256sum "$OUT/SonosCredentialStoreHarness.dll"
} > "$HERE/sha256.txt"

echo "STATUS=${STATUS}" | tee "$HERE/status.txt"
exit "${STATUS}"
