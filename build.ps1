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
    [xml]$buildProperties = Get-Content -LiteralPath (Join-Path $root "Directory.Build.props")
    $version = [string]$buildProperties.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) { throw "Nie znaleziono wspólnego numeru wersji." }

    $publishRoot = Join-Path $root "publish"
    $staging = Join-Path $publishRoot ".staging-win-x64"
    $program = Join-Path $publishRoot "AccessibleMediaController-$version.exe"
    $resolvedRoot = [IO.Path]::GetFullPath($root)
    $resolvedStaging = [IO.Path]::GetFullPath($staging)
    if (-not $resolvedStaging.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Nieprawidłowy katalog tymczasowy publikacji."
    }

    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    try {
        dotnet publish src/AccessibleMediaController.Windows/AccessibleMediaController.Windows.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            --output $staging `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw "Nie udało się utworzyć wersji samowystarczalnej." }
        Copy-Item -LiteralPath (Join-Path $staging "AccessibleMediaController.exe") -Destination $program -Force
    }
    finally {
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    }
    Write-Host "Gotowy program: $program"
}
