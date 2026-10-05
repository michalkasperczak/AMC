# POMIAR RED: te same testy na KODZIE BAZOWYM (bb4c917), zeby pokazac, ze luki
# byly prawdziwe. Zrodla Protocol/ bierzemy z git show HEAD bazowego, a testy z
# katalogu roboczego. Osobny staging, zeby nie tknac wynikow GREEN.
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Users\Michal\amc-build-tools\dotnet-8.0.425\dotnet.exe'
$env:NUGET_PACKAGES = 'C:\Users\Michal\amc-build-tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$stage = 'C:\Users\Michal\AppData\Local\Temp\amc-wx-queue-transport-final-RED'
$src = Join-Path $stage 'src-tests'
if (Test-Path $src) { Remove-Item $src -Recurse -Force }
New-Item -ItemType Directory -Path $src -Force | Out-Null

$repo = $PSScriptRoot
Copy-Item (Join-Path $repo 'Directory.Build.props') $src -Force
foreach ($part in @(
    'src\AccessibleMediaController.Core',
    'src\AccessibleMediaController.LiteHost\Protocol',
    'tests\AccessibleMediaController.LiteHost.ProtocolTests')) {
    $dest = Join-Path $src $part
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item (Join-Path $repo "$part\*") $dest -Recurse -Force
}
Get-ChildItem $src -Recurse -Directory | Where-Object { $_.Name -in 'bin', 'obj' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# PODMIANA na wersje BAZOWA koordynatora (wgrana wczesniej przez WSL) oraz
# usuniecie testow, ktore na bazie sie nie kompiluja (uzywaja nowego API).
$baseline = Join-Path $stage 'LiteQueueCoordinator.base.cs'
if (-not (Test-Path $baseline)) { Write-Output 'GUARD=brak pliku bazowego'; exit 3 }
Copy-Item $baseline (Join-Path $src 'src\AccessibleMediaController.LiteHost\Protocol\LiteQueueCoordinator.cs') -Force
Remove-Item (Join-Path $src 'tests\AccessibleMediaController.LiteHost.ProtocolTests\QueueTransportTests.cs') -Force
$prog = Join-Path $src 'tests\AccessibleMediaController.LiteHost.ProtocolTests\Program.cs'
(Get-Content $prog -Raw).Replace(
    '("--transport", QueueTransportTests.Run),',
    '("--red", QueueTransportRedWitness.Run),') | Set-Content $prog -NoNewline

$proj = Join-Path $src 'tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj'
Set-Location $src
& $dotnet build $proj --nologo -v minimal
Write-Output "BUILD-EXIT=$LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $dotnet run --project $proj --no-build -- --red
Write-Output "RED-EXIT=$LASTEXITCODE"
exit 0
