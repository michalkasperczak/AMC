#!/usr/bin/env bash
# Pomiar kontrolek okna konta Sonos BEZ pokazywania GUI.
#
# Buduje z WSL (SDK 8.0.425), uruchamia na Windows (runtime 8.0.31), bo WPF
# potrzebuje prawdziwego Windows. Tryb domyslny NIE pokazuje okna, nie mowi do
# czytnika i nie otwiera przegladarki.
#
# Uzycie:
#   ./run.sh              # pomiar bez GUI (kod wyjscia 0/1)
#   ./run.sh --show-fixture [--with-account] [--seconds N]
#                         # POKAZUJE okno probne - tylko za zgoda wlasciciela
#                         # pulpitu, bo zabiera fokus i ruszy czytnik ekranu.
set -euo pipefail

WORKTREE="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT="$WORKTREE/tests/SonosAccountWindowHarness/csharp/Harness.csproj"
DOTNET_WSL="${DOTNET_WSL:-/home/michal/dotnet/dotnet}"
DOTNET_WIN="${DOTNET_WIN:-/mnt/c/Program Files/dotnet/dotnet.exe}"
OUTPUT_DIR="$WORKTREE/tests/SonosAccountWindowHarness/csharp/bin/Release/net8.0-windows"

echo "== budowanie (WSL) =="
"$DOTNET_WSL" build "$PROJECT" -p:AmcWorktree="$WORKTREE" -c Release -v q --nologo

if [[ ! -x "$DOTNET_WIN" ]]; then
    echo "BLOKADA: brak runtime .NET na Windows ($DOTNET_WIN). Pomiaru WPF nie da sie uruchomic z samego WSL." >&2
    exit 2
fi

echo "== pomiar (Windows) =="
WIN_DIR="$(wslpath -w "$OUTPUT_DIR")"
"$DOTNET_WIN" "$WIN_DIR\\SonosAccountWindowHarness.dll" "$@"
