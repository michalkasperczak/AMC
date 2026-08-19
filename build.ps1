param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Remove-GeneratedNuGetSyncDuplicates {
    foreach ($searchRoot in @((Join-Path $root "src"), (Join-Path $root "tests"))) {
        if (-not (Test-Path -LiteralPath $searchRoot)) { continue }
        $objectDirectories = Get-ChildItem -LiteralPath $searchRoot -Directory -Filter "obj" -Recurse
        foreach ($objectDirectory in $objectDirectories) {
            Get-ChildItem -LiteralPath $objectDirectory.FullName -File -Recurse |
                Where-Object {
                    $_.Name -match '(\.nuget\.(g|dgspec)|project\.(assets|packagespec|nuget)) \d+\.(props|targets|json|cache)$'
                } |
                Remove-Item -Force
        }
    }
}

Remove-GeneratedNuGetSyncDuplicates
dotnet restore AccessibleMediaController.sln --runtime win-x64 -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw "Nie udało się przywrócić składników projektu." }
Remove-GeneratedNuGetSyncDuplicates
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
    $packageDirectory = Join-Path $publishRoot "AccessibleMediaController-$version"
    $program = Join-Path $packageDirectory "AccessibleMediaController-$version.exe"
    $resolvedRoot = [IO.Path]::GetFullPath($root)
    $resolvedStaging = [IO.Path]::GetFullPath($staging)
    $resolvedPackageDirectory = [IO.Path]::GetFullPath($packageDirectory)
    if (-not $resolvedStaging.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Nieprawidłowy katalog tymczasowy publikacji."
    }
    if (-not $resolvedPackageDirectory.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Nieprawidłowy katalog gotowego programu."
    }

    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
    if (Test-Path -LiteralPath $packageDirectory) { Remove-Item -LiteralPath $packageDirectory -Recurse -Force }
    try {
        dotnet publish src/AccessibleMediaController.Windows/AccessibleMediaController.Windows.csproj `
            --configuration Release `
            --runtime win-x64 `
            --self-contained true `
            --no-restore `
            --output $staging `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw "Nie udało się utworzyć wersji samowystarczalnej." }
        New-Item -ItemType Directory -Path $packageDirectory | Out-Null
        Copy-Item -LiteralPath (Join-Path $staging "AccessibleMediaController.exe") -Destination $program -Force
        foreach ($fileName in @("SoundTouch.Net.dll", "SoundTouch.Net.NAudioSupport.dll", "THIRD_PARTY_NOTICES.md")) {
            $sourceFile = Join-Path $staging $fileName
            if (-not (Test-Path -LiteralPath $sourceFile)) { throw "Brak składnika publikacji: $fileName" }
            Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $packageDirectory $fileName) -Force
        }
        Copy-Item -LiteralPath (Join-Path $staging "licenses") -Destination (Join-Path $packageDirectory "licenses") -Recurse -Force
    }
    finally {
        if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
        Remove-GeneratedNuGetSyncDuplicates
    }
    Write-Host "Gotowy pakiet: $packageDirectory"
    Write-Host "Program: $program"
}
