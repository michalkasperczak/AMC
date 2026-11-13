[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet("public", "test")] [string]$Profile,
    [Parameter(Mandatory)] [string]$BundleRoot,
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')] [string]$Version,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$NvdaAddonPath = "",
    [string]$AmcNvdaAddonPath = "",
    [string]$IsccPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$bundle = [IO.Path]::GetFullPath($BundleRoot)
$output = [IO.Path]::GetFullPath($OutputDirectory)
$installerScript = Join-Path $repositoryRoot "installer\AMC_Wx_Setup.iss"
$amcNvdaUpdateHelper = Join-Path $repositoryRoot "installer\update_amc_nvda_addon.py"
$startupSmokeTool = Join-Path $repositoryRoot "wxlite\tools\Test-AMCBundleStartup.ps1"
$manifestPath = Join-Path $bundle "bundle-manifest.json"
$launcher = Join-Path $bundle "AMC.exe"
$nvdaAddon = $null
$amcNvdaAddon = $null

Push-Location $repositoryRoot
try {
    $installerSourceRevision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installerSourceRevision)) {
        throw "Nie można odczytać rewizji źródeł instalatora."
    }
    $trackedChanges = @(& git status --porcelain --untracked-files=no)
    if ($LASTEXITCODE -ne 0) {
        throw "Nie można sprawdzić stanu źródeł instalatora."
    }
    if ($trackedChanges.Count -gt 0) {
        throw "Instalator nie może powstać z niezapisanych zmian w śledzonych źródłach."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Brak bundle-manifest.json w folderze paczki: $bundle"
}
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "Brak AMC.exe w folderze paczki: $bundle"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ([string]$manifest.releaseProfile -ne $Profile) {
    throw "Profil manifestu nie zgadza się z wybranym profilem instalatora."
}
if ([bool]$manifest.sourceDirty) {
    throw "Instalator nie może powstać z niezapisanych źródeł roboczych."
}
if ([string]$manifest.sourceRevision -cne $installerSourceRevision) {
    throw "Paczka i instalator muszą pochodzić z tego samego commitu."
}

# Najbliższy instalator AMC Test ma zawierać sprawdzoną paczkę Quick App
# Switchera. Inno Setup jedynie proponuje uruchomienie standardowego
# instalatora NVDA; bez zgody użytkownika nie instaluje dodatku ani nie
# restartuje czytnika ekranu.
if ($Profile -eq "test" -and [string]::IsNullOrWhiteSpace($NvdaAddonPath)) {
    throw "Instalator AMC Test wymaga paczki Quick App Switcher przekazanej przez -NvdaAddonPath."
}
if (-not [string]::IsNullOrWhiteSpace($NvdaAddonPath)) {
    $resolvedAddon = [IO.Path]::GetFullPath($NvdaAddonPath)
    if (-not (Test-Path -LiteralPath $resolvedAddon -PathType Leaf) -or
        [IO.Path]::GetExtension($resolvedAddon) -cne ".nvda-addon") {
        throw "Nie znaleziono prawidłowej paczki .nvda-addon."
    }
    $addonFile = Get-Item -LiteralPath $resolvedAddon
    if ($addonFile.Length -le 0 -or $addonFile.Length -gt 64MB) {
        throw "Paczka dodatku NVDA ma nieprawidłowy rozmiar."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($resolvedAddon)
    try {
        $addonManifestEntry = @($archive.Entries | Where-Object {
            $_.FullName.Replace("\", "/") -ceq "manifest.ini"
        })
        if ($addonManifestEntry.Count -ne 1 -or
            $addonManifestEntry[0].Length -le 0 -or
            $addonManifestEntry[0].Length -gt 256KB) {
            throw "Paczka dodatku NVDA nie zawiera jednoznacznego manifest.ini."
        }
        $reader = [IO.StreamReader]::new(
            $addonManifestEntry[0].Open(),
            [Text.UTF8Encoding]::new($false, $true),
            $true)
        try {
            $addonManifest = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
    $addonNameMatch = [Regex]::Match(
        $addonManifest,
        '(?m)^name\s*=\s*(?<value>[^\r\n]+)\s*$')
    $addonVersionMatch = [Regex]::Match(
        $addonManifest,
        '(?m)^version\s*=\s*(?<value>\d+(?:\.\d+){1,3})\s*$')
    if (-not $addonNameMatch.Success -or
        $addonNameMatch.Groups["value"].Value.Trim() -cne "quickAppSwitcher" -or
        -not $addonVersionMatch.Success) {
        throw "Paczka NVDA nie jest dodatkiem Quick App Switcher z prawidłową wersją."
    }
    $addonVersion = $addonVersionMatch.Groups["value"].Value.Trim()
    $expectedAddonFileName = "quickAppSwitcher-$addonVersion.nvda-addon"
    if ($addonFile.Name -cne $expectedAddonFileName) {
        throw "Nazwa paczki dodatku NVDA nie zgadza się z wersją w manifest.ini."
    }
    $nvdaAddon = [ordered]@{
        displayName = "Quick App Switcher"
        version = $addonVersion
        fileName = $addonFile.Name
        bytes = $addonFile.Length
        sha256 = (Get-FileHash -LiteralPath $resolvedAddon -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if (-not [string]::IsNullOrWhiteSpace($AmcNvdaAddonPath)) {
    $resolvedAmcAddon = [IO.Path]::GetFullPath($AmcNvdaAddonPath)
    if (-not (Test-Path -LiteralPath $resolvedAmcAddon -PathType Leaf) -or
        [IO.Path]::GetExtension($resolvedAmcAddon) -cne ".nvda-addon") {
        throw "Nie znaleziono paczki dodatku AMC do NVDA."
    }
    $amcAddonFile = Get-Item -LiteralPath $resolvedAmcAddon
    if ($amcAddonFile.Length -le 0 -or $amcAddonFile.Length -gt 64MB) {
        throw "Paczka dodatku AMC do NVDA ma nieprawidłowy rozmiar."
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($resolvedAmcAddon)
    try {
        $entries = @($archive.Entries | Where-Object {
            $_.FullName.Replace("\", "/") -ceq "manifest.ini"
        })
        if ($entries.Count -ne 1 -or $entries[0].Length -le 0 -or
            $entries[0].Length -gt 256KB) {
            throw "Paczka dodatku AMC nie zawiera jednoznacznego manifest.ini."
        }
        $reader = [IO.StreamReader]::new(
            $entries[0].Open(), [Text.UTF8Encoding]::new($false, $true), $true)
        try {
            $amcManifest = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
    $amcNameMatch = [Regex]::Match(
        $amcManifest, '(?m)^name\s*=\s*(?<value>[^\r\n]+)\s*$')
    $amcVersionMatch = [Regex]::Match(
        $amcManifest, '(?m)^version\s*=\s*(?<value>\d+(?:\.\d+){1,3})\s*$')
    if (-not $amcNameMatch.Success -or
        $amcNameMatch.Groups["value"].Value.Trim() -cne "amcController" -or
        -not $amcVersionMatch.Success) {
        throw "Paczka NVDA nie jest dodatkiem AMC z prawidłową wersją."
    }
    $amcAddonVersion = $amcVersionMatch.Groups["value"].Value.Trim()
    if ($amcAddonFile.Name -cne "AMC-NVDA-$amcAddonVersion.nvda-addon") {
        throw "Nazwa paczki dodatku AMC nie zgadza się z wersją w manifest.ini."
    }
    $amcNvdaAddon = [ordered]@{
        displayName = "AMC — sterowanie odtwarzaczem"
        version = $amcAddonVersion
        fileName = $amcAddonFile.Name
        bytes = $amcAddonFile.Length
        sha256 = (Get-FileHash -LiteralPath $resolvedAmcAddon -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

# Lokalny plik używany podczas rozwoju nie może stać się artefaktem GitHub
# ani instalatora. Sprawdzamy zarówno deklarację manifestu, jak i rzeczywisty
# katalog, aby dodatkowy niezarządzany plik nie ominął kontroli sum pakietu.
$forbiddenBundleFiles = @(
    "app/amc_wx_lite/youtube-oauth-client-secret.txt",
    "app/amc_wx_lite/sonos_client_secret.txt",
    "deploy/sonos-broker/secrets/sonos_client_secret.txt"
)
$manifestFiles = @($manifest.files | ForEach-Object {
    ([string]$_).Replace("\", "/").ToLowerInvariant()
})
$manifestFileSet = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
foreach ($relative in $manifestFiles) {
    [void]$manifestFileSet.Add($relative)
}
$unexpectedBundleFiles = @(
    Get-ChildItem -LiteralPath $bundle -File -Recurse |
        ForEach-Object {
            $relative = $_.FullName.Substring($bundle.Length + 1).Replace("\", "/")
            if ($relative -ne "bundle-manifest.json" -and
                -not $manifestFileSet.Contains($relative)) {
                $relative
            }
        }
)
if ($unexpectedBundleFiles.Count -gt 0) {
    $sample = ($unexpectedBundleFiles | Select-Object -First 5) -join ", "
    throw "Instalator nie może zawierać plików nieobjętych manifestem: $sample"
}
foreach ($relative in $forbiddenBundleFiles) {
    $normalized = $relative.ToLowerInvariant()
    $actualPath = Join-Path $bundle ($relative.Replace("/", [IO.Path]::DirectorySeparatorChar))
    if ($manifestFiles -contains $normalized -or (Test-Path -LiteralPath $actualPath)) {
        throw "Instalator nie może zawierać sekretu ani lokalnego pliku poświadczeń: $relative"
    }
}

$displayVersion = [string]$manifest.displayVersion
$displayVersionMatch = [Regex]::Match(
    $displayVersion,
    '^(?<day>\d{2})\.(?<month>\d{2})\.(?<year>\d{4}), (?<hour>\d{2}):(?<minute>\d{2})$'
)
if (-not $displayVersionMatch.Success) {
    throw "Manifest nie zawiera czytelnej daty i godziny wersji użytkowej."
}
$fileTimestamp = (
    $displayVersionMatch.Groups["year"].Value + "-" +
    $displayVersionMatch.Groups["month"].Value + "-" +
    $displayVersionMatch.Groups["day"].Value + "-" +
    $displayVersionMatch.Groups["hour"].Value +
    $displayVersionMatch.Groups["minute"].Value
)

$launcherVersion = [string](Get-Item -LiteralPath $launcher).VersionInfo.ProductVersion
$launcherVersion = ($launcherVersion -split '\+', 2)[0]
if ($launcherVersion -ne $Version) {
    throw (
        "Numer instalatora $Version nie zgadza się z numerem skompilowanego " +
        "programu $launcherVersion. Najpierw ustaw i zbuduj jedną wersję źródeł."
    )
}

$check = Start-Process -FilePath $launcher `
    -ArgumentList @("--sprawdz-pakiet-pelny-cicho") `
    -WorkingDirectory $bundle `
    -Wait -PassThru -WindowStyle Hidden
try {
    if ($check.ExitCode -ne 0) {
        throw "Pełna kontrola paczki przed instalatorem zakończyła się kodem $($check.ExitCode)."
    }
}
finally {
    $check.Dispose()
}

& $startupSmokeTool -BundleRoot $bundle

if ([string]::IsNullOrWhiteSpace($IsccPath)) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $IsccPath = $command.Source
    }
    else {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
            (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
            (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
        )
        foreach ($candidate in $candidates) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                $IsccPath = $candidate
                break
            }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($IsccPath) -or
    -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw "Nie znaleziono kompilatora Inno Setup ISCC.exe."
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
$compileArguments = @(
    "/DProfil=$Profile",
    "/DWersja=$Version",
    "/DEtykietaUzytkowa=$displayVersion",
    "/DEtykietaPliku=$fileTimestamp",
    "/DZrodlo=$bundle",
    "/DWyjscie=$output"
)
if ($null -ne $nvdaAddon) {
    $compileArguments += @(
        "/DNvdaAddonPath=$resolvedAddon",
        "/DNvdaAddonFileName=$($nvdaAddon.fileName)",
        "/DNvdaAddonVersion=$($nvdaAddon.version)"
    )
}
if ($null -ne $amcNvdaAddon) {
    if (-not (Test-Path -LiteralPath $amcNvdaUpdateHelper -PathType Leaf)) {
        throw "Brak pomocnika cichej aktualizacji dodatku AMC do NVDA."
    }
    $compileArguments += @(
        "/DAmcNvdaAddonPath=$resolvedAmcAddon",
        "/DAmcNvdaAddonFileName=$($amcNvdaAddon.fileName)",
        "/DAmcNvdaAddonVersion=$($amcNvdaAddon.version)",
        "/DAmcNvdaUpdateHelperPath=$amcNvdaUpdateHelper"
    )
}
$compileArguments += $installerScript
$compileStart = [Diagnostics.ProcessStartInfo]::new()
$compileStart.FileName = $IsccPath
$compileStart.WorkingDirectory = $repositoryRoot
$compileStart.UseShellExecute = $false
$compileStart.CreateNoWindow = $true
foreach ($argument in $compileArguments) {
    [void]$compileStart.ArgumentList.Add($argument)
}
$compile = [Diagnostics.Process]::Start($compileStart)
if ($null -eq $compile) {
    throw "Nie udało się uruchomić kompilatora Inno Setup."
}
try {
    $compile.WaitForExit()
    if ($compile.ExitCode -ne 0) {
        throw "Kompilacja instalatora zakończyła się kodem $($compile.ExitCode)."
    }
}
finally {
    $compile.Dispose()
}

$baseName = if ($Profile -eq "public") {
    "AMC-Public-$fileTimestamp.exe"
} else {
    "AMC-Test-$fileTimestamp.exe"
}
$installer = Join-Path $output $baseName
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
    throw "Kompilator nie utworzył oczekiwanego instalatora: $installer"
}

$integrity = [ordered]@{
    schemaVersion = 1
    profile = $Profile
    version = $Version
    displayVersion = $displayVersion
    fileName = $baseName
    bytes = (Get-Item -LiteralPath $installer).Length
    sha256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
    bundleId = [string]$manifest.bundleId
    sourceRevision = [string]$manifest.sourceRevision
    installerSourceRevision = $installerSourceRevision
    secretScan = "passed"
    nvdaAddon = $nvdaAddon
    amcNvdaAddon = $amcNvdaAddon
}
$integrityPath = Join-Path $output ($baseName + ".integrity.json")
$integrity | ConvertTo-Json | Set-Content -LiteralPath $integrityPath -Encoding utf8

Write-Output "Instalator gotowy: $installer"
Write-Output "Integralność: $integrityPath"
