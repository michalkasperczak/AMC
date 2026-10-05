# Buduje i uruchamia testy protokolu LiteHost na SDK Windows (SDK nie ma w WSL).
# Pakiety z istniejacego RestorePackagesPath -- nie sciagamy cache od nowa.
$ErrorActionPreference = 'Stop'
$dotnet = 'C:\Users\Michal\amc-build-tools\dotnet-8.0.425\dotnet.exe'
$env:NUGET_PACKAGES = 'C:\Users\Michal\amc-build-tools\nuget-packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Set-Location $PSScriptRoot
$proj = 'tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj'

& $dotnet build $proj --nologo -v minimal
if ($LASTEXITCODE -ne 0) { Write-Output "BUILD-EXIT=$LASTEXITCODE"; exit $LASTEXITCODE }

& $dotnet run --project $proj --no-build -- @args
Write-Output "TESTS-EXIT=$LASTEXITCODE"
exit $LASTEXITCODE
