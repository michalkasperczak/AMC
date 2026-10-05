# Buduje i uruchamia testy protokolu LiteHost w OSOBNYM stagingu NTFS.
#
# Dlaczego osobno, a nie przez run-protocol-tests.ps1 / publish-queue-host.ps1:
# tamte uzywaja katalogow build-src\ i host-queue\, ktore rownolegle czyta
# proces nadrzedny. Nadpisanie ich w trakcie jego pracy podmienilo by mu wynik
# pod rekami. Dlatego wlasny katalog i wlasna nazwa.
#
# Budowanie wprost z \\wsl.localhost pada na CS0006 (plik ref/ nie powstaje na
# UNC), wiec kopiujemy zrodla na dysk Windows. Pakiety NuGet REUZYWAMY z
# istniejacego cache -- nie sciagamy niczego od nowa.
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Users\Michal\amc-build-tools\dotnet-8.0.425\dotnet.exe'
$env:NUGET_PACKAGES = 'C:\Users\Michal\amc-build-tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$stage = 'C:\Users\Michal\AppData\Local\Temp\amc-wx-queue-transport-final'
$src = Join-Path $stage 'src-tests'
$out = Join-Path $stage 'host-transport'

# Wolne miejsce na C: jest na granicy. Mierzymy PRZED kopiowaniem i odmawiamy
# jawnie, zamiast zostawiac po sobie polowe stagingu.
$freeGb = [math]::Round((Get-PSDrive C).Free / 1GB, 2)
Write-Output "FREE-GB-BEFORE=$freeGb"
if ($freeGb -lt 1.2) {
    Write-Output 'GUARD=za malo miejsca na C: (potrzeba >=1,2 GB)'
    exit 3
}

if (Test-Path $src) { Remove-Item $src -Recurse -Force }
New-Item -ItemType Directory -Path $src -Force | Out-Null

$repo = $PSScriptRoot
# Directory.Build.props wnosi ImplicitUsings/LangVersion -- bez niego Core nie
# kompiluje sie wcale (CS0246 na Task/CancellationToken).
Copy-Item (Join-Path $repo 'Directory.Build.props') $src -Force
# Testy protokolu celuja w net8.0 BEZ -windows i referuja tylko Core: nie
# kopiujemy Windows/LiteHost/WPF ani natywnych bibliotek. Protokol linkujemy
# plikami zrodlowymi, dokladnie jak csproj testow.
foreach ($part in @(
    'src\AccessibleMediaController.Core',
    'src\AccessibleMediaController.LiteHost\Protocol',
    'tests\AccessibleMediaController.LiteHost.ProtocolTests')) {
    $dest = Join-Path $src $part
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item (Join-Path $repo "$part\*") $dest -Recurse -Force
}
# Cudze bin/obj z repo nie maja prawa wplywac na nasz wynik.
Get-ChildItem $src -Recurse -Directory | Where-Object { $_.Name -in 'bin', 'obj' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Output ('STAGED-FILES=' + (Get-ChildItem $src -Recurse -File).Count)

$proj = Join-Path $src 'tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj'
Set-Location $src

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
& $dotnet publish $proj -c Release -o $out --nologo -v minimal
Write-Output "PUBLISH-EXIT=$LASTEXITCODE"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $out 'amc_lite_protocol_tests.exe'
$dll = Join-Path $out 'amc_lite_protocol_tests.dll'
Write-Output "EXE=$exe"
Write-Output ("EXE-EXISTS=" + (Test-Path $exe))
# EXE moze byc samym launcherem; dowodem tozsamosci kodu jest hasz DLL.
if (Test-Path $dll) { Write-Output ("DLL-SHA256=" + (Get-FileHash $dll -Algorithm SHA256).Hash) }
if (Test-Path $exe) { Write-Output ("EXE-SHA256=" + (Get-FileHash $exe -Algorithm SHA256).Hash) }

# URUCHOMIENIE Z GOTOWEGO ARTEFAKTU, bez ponownego budowania: mierzymy to, co
# zostalo opublikowane i zahaszowane powyzej.
Write-Output "RUN-ARGS=$($args -join ' ')"
& $exe @args
Write-Output "TESTS-EXIT=$LASTEXITCODE"
Write-Output ("FREE-GB-AFTER=" + [math]::Round((Get-PSDrive C).Free / 1GB, 2))
exit $LASTEXITCODE
