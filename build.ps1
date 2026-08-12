param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet restore AccessibleMediaController.sln -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "Nie udało się przywrócić składników projektu." }
dotnet build AccessibleMediaController.sln --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Nie udało się skompilować projektu." }
dotnet run --project tests/AccessibleMediaController.Core.SmokeTests --configuration Release --no-build
if ($LASTEXITCODE -ne 0) { throw "Testy kontrolne nie powiodły się." }

if ($Publish) {
    $output = Join-Path $root "publish/win-x64"
    dotnet publish src/AccessibleMediaController.Windows/AccessibleMediaController.Windows.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $output `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:NuGetAudit=false
    if ($LASTEXITCODE -ne 0) { throw "Nie udało się utworzyć wersji samowystarczalnej." }
    Write-Host "Gotowy program: $output"
}
