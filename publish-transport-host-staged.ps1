# Buduje PRAWDZIWY host amc_lite_host (ten, ktory odpala frontend wxPython) w
# OSOBNYM stagingu NTFS i mierzy na nim ZADANIA PROTOKOLU: queue.playAt,
# transport.*, queue.status. Bez GUI i bez NVDA -- host jest bezokienny.
#
# Dlaczego osobny staging, a nie publish-queue-host.ps1: tamten ma zahardcodowane
# build-src\ i host-queue\, ktore czyta rodzic. Nie wolno ich nadpisac.
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Users\Michal\amc-build-tools\dotnet-8.0.425\dotnet.exe'
$env:NUGET_PACKAGES = 'C:\Users\Michal\amc-build-tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$stage = 'C:\Users\Michal\AppData\Local\Temp\amc-wx-queue-transport-final-host'
$free = [math]::Round((Get-PSDrive C).Free / 1GB, 2)
Write-Output "FREE-GB-BEFORE=$free"
if ($free -lt 2) { Write-Output 'GUARD=za malo miejsca'; exit 3 }

$src = Join-Path $stage 'src-host'
if (Test-Path $src) { Remove-Item $src -Recurse -Force }
New-Item -ItemType Directory -Path $src -Force | Out-Null

$repo = $PSScriptRoot
Copy-Item (Join-Path $repo 'Directory.Build.props') $src -Force
foreach ($part in @('src', 'third_party', 'licenses')) {
    Copy-Item (Join-Path $repo $part) $src -Recurse -Force
}
Get-ChildItem $src -Recurse -Directory | Where-Object { $_.Name -in 'bin', 'obj' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Output "STAGED-FILES=$((Get-ChildItem $src -Recurse -File).Count)"

# Tylko host Lite: BEZ projektu WPF, wiec zadne okno nie wchodzi do kompilacji.
$proj = Join-Path $src 'src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj'
$outDir = Join-Path $stage 'host-transport-real'
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

Set-Location $src
& $dotnet publish $proj -c Release -r win-x64 --self-contained false -o $outDir --nologo -v minimal
Write-Output "PUBLISH-EXIT=$LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $outDir 'amc_lite_host.exe'
$dll = Join-Path $outDir 'amc_lite_host.dll'
Write-Output "EXE=$exe"
Write-Output "EXE-EXISTS=$(Test-Path $exe)"
# EXE moze byc tylko launcherem -- liczy sie hasz DLL z kodem.
Write-Output "HOST-DLL-SHA256=$((Get-FileHash $dll -Algorithm SHA256).Hash)"
Write-Output "HOST-EXE-SHA256=$((Get-FileHash $exe -Algorithm SHA256).Hash)"
Write-Output "TEMPO-DLL-SHA256=$((Get-FileHash (Join-Path $outDir 'AmcTempoEngines.dll') -Algorithm SHA256).Hash)"
Write-Output "OUT-DIR=$outDir"
Write-Output "FREE-GB-AFTER=$([math]::Round((Get-PSDrive C).Free / 1GB, 2))"
