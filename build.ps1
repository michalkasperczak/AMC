param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

dotnet restore AccessibleMediaController.sln
dotnet build AccessibleMediaController.sln --configuration Release --no-restore
dotnet run --project tests/AccessibleMediaController.Core.SmokeTests --configuration Release --no-build

if ($Publish) {
    $output = Join-Path $root "publish/win-x64"
    dotnet publish src/AccessibleMediaController.Windows/AccessibleMediaController.Windows.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $output `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
    Write-Host "Gotowy program: $output"
}
