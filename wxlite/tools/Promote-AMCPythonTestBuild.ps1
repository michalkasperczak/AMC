[CmdletBinding()]
param(
    [string]$InstallRoot = "D:\Projekty Codex\AMC Python",
    [string]$TidalSitePackages = "",
    [string]$SonosSitePackages = "",
    [string]$AppleMusicSitePackages = "",
    [switch]$SkipTests,
    [switch]$NoLaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$WxliteRoot = Join-Path $RepositoryRoot "wxlite"
$BuildTool = Join-Path $WxliteRoot "tools\build_bundle.py"
$StartupSmokeTool = Join-Path $WxliteRoot "tools\Test-AMCBundleStartup.ps1"
$RuntimePython = Join-Path $InstallRoot "runtime\python.exe"
$InstalledLauncher = Join-Path $InstallRoot "AMC.exe"
$TestOutputRoot = Join-Path $RepositoryRoot ".test-output"
$TemporaryRoot = Join-Path $RepositoryRoot ".test-tmp"

function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList,
        [Parameter(Mandatory)] [string]$WorkingDirectory,
        [Parameter(Mandatory)] [string]$Stage
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    foreach ($argument in $ArgumentList) {
        [void]$startInfo.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "$Stage nie uruchomił procesu."
    }
    try {
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        if ($exitCode -ne 0) {
            throw "$Stage zakończył się kodem $exitCode."
        }
    }
    finally {
        $process.Dispose()
    }
}

function Read-BundleManifest {
    param([Parameter(Mandatory)] [string]$Root)

    $manifestPath = Join-Path $Root "bundle-manifest.json"
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Brak manifestu paczki: $manifestPath"
    }
    return Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
}

function Get-SourceChanges {
    $changes = & git -C $RepositoryRoot status --porcelain --untracked-files=all -- `
        . ":(exclude).test-output/**" ":(exclude).test-tmp/**"
    if ($LASTEXITCODE -ne 0) {
        throw "Nie udało się sprawdzić stanu repozytorium."
    }
    return @($changes)
}

function Assert-CleanSource {
    $changes = @(Get-SourceChanges)
    if ($changes.Count -gt 0) {
        throw (
            "Odbiorcza paczka nie może powstać z niezatwierdzonych zmian, " +
            "także z nowych plików źródłowych. Najpierw zatwierdź spójny " +
            "przyrost w lokalnym Git.`n" + ($changes -join "`n")
        )
    }
}

function Assert-ReleaseIdentity {
    param([Parameter(Mandatory)] $Manifest)

    $revision = [string]$Manifest.sourceRevision
    $bundleId = [string]$Manifest.bundleId
    $buildLabel = [string]$Manifest.buildLabel
    $dirtyProperty = $Manifest.PSObject.Properties["sourceDirty"]
    if ($null -eq $dirtyProperty -or $dirtyProperty.Value -ne $false) {
        throw "Paczka odbiorcza nie pochodzi z czystego stanu Git."
    }
    if ($revision -notmatch '^[0-9a-fA-F]{40}$') {
        throw "Paczka odbiorcza nie podaje pełnego identyfikatora commita Git."
    }
    if ($bundleId -notmatch '^[0-9a-fA-F]{8}$') {
        throw "Paczka odbiorcza nie podaje identyfikatora swojej zawartości."
    }
    $expected = [Regex]::Escape($revision.ToLowerInvariant() + "-" + $bundleId.ToLowerInvariant())
    if ($buildLabel -notmatch "^\d{4}\.\d{2}\.\d{2}-$expected$") {
        throw "Etykieta kompilacji nie wiąże pełnego commita z zawartością paczki."
    }
}

function Test-BundleIntegrity {
    param(
        [Parameter(Mandatory)] [string]$Root,
        [Parameter(Mandatory)] $Manifest
    )

    $resolvedRoot = [IO.Path]::GetFullPath($Root).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar
    ) + [IO.Path]::DirectorySeparatorChar
    $files = @($Manifest.files)
    $hashProperties = @($Manifest.fileHashes.PSObject.Properties)
    if ($files.Count -eq 0 -or $hashProperties.Count -ne $files.Count) {
        throw "Manifest nie zawiera jednej sumy dla każdego pliku paczki."
    }

    $expectedHashes = [Collections.Generic.Dictionary[string,string]]::new(
        [StringComparer]::Ordinal
    )
    foreach ($property in $hashProperties) {
        $expectedHashes.Add([string]$property.Name, [string]$property.Value)
    }

    foreach ($relativeValue in $files) {
        $relative = [string]$relativeValue
        if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative)) {
            throw "Manifest zawiera nieprawidłową ścieżkę pliku."
        }
        $normalized = $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
        $path = [IO.Path]::GetFullPath((Join-Path $resolvedRoot $normalized))
        if (-not $path.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Manifest próbuje wyjść poza folder paczki."
        }
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Brak pliku paczki: $relative"
        }
        $expected = ""
        if (-not $expectedHashes.TryGetValue($relative, [ref]$expected) -or
            $expected -notmatch '^[0-9a-fA-F]{64}$') {
            throw "Brak prawidłowej sumy SHA-256 dla pliku: $relative"
        }
        $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        if (-not $actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Niezgodna suma SHA-256 pliku paczki: $relative"
        }
    }

    [string[]]$names = @($expectedHashes.Keys)
    [Array]::Sort($names, [StringComparer]::Ordinal)
    $identityInput = [Text.StringBuilder]::new()
    foreach ($name in $names) {
        [void]$identityInput.Append($name.Replace('\', '/'))
        [void]$identityInput.Append([char]0)
        [void]$identityInput.Append($expectedHashes[$name].ToLowerInvariant())
        [void]$identityInput.Append("`n")
    }
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $identityBytes = [Text.Encoding]::UTF8.GetBytes($identityInput.ToString())
        $identityHash = $sha256.ComputeHash($identityBytes)
    }
    finally {
        $sha256.Dispose()
    }
    $actualBundleId = [BitConverter]::ToString($identityHash).Replace("-", "").ToLowerInvariant().Substring(0, 8)
    if ($actualBundleId -ne ([string]$Manifest.bundleId).ToLowerInvariant()) {
        throw "Identyfikator zawartości paczki nie pasuje do jej manifestu."
    }
}

if (-not (Test-Path -LiteralPath $RuntimePython -PathType Leaf)) {
    throw "Brak prywatnego runtime'u bieżącej instalacji: $RuntimePython"
}
if (-not (Test-Path -LiteralPath $InstalledLauncher -PathType Leaf)) {
    throw "Brak stałego pliku uruchamiającego: $InstalledLauncher"
}

Assert-CleanSource

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw "Brak środowiska dotnet potrzebnego do kompilacji składników AMC."
}
$env:DOTNET = $dotnet.Source

if (-not $SkipTests) {
    $uv = Get-Command uv -ErrorAction SilentlyContinue
    if ($null -eq $uv) {
        throw "Brak polecenia uv potrzebnego do uruchomienia testów Pythona."
    }
    Invoke-Checked -FilePath $dotnet.Source -ArgumentList @(
        "run", "--project",
        (Join-Path $RepositoryRoot "tests\AccessibleMediaController.PythonLauncher.Tests\AccessibleMediaController.PythonLauncher.Tests.csproj"),
        "-c", "Release"
    ) -WorkingDirectory $RepositoryRoot -Stage "Testy launchera i aktualizatora"
    Invoke-Checked -FilePath $dotnet.Source -ArgumentList @(
        "build",
        (Join-Path $RepositoryRoot "tests\AccessibleMediaController.LiteHost.ProtocolTests\AccessibleMediaController.LiteHost.ProtocolTests.csproj"),
        "-c", "Release"
    ) -WorkingDirectory $RepositoryRoot -Stage "Budowanie serwera testowego protokołu"
    Invoke-Checked -FilePath $dotnet.Source -ArgumentList @(
        "build",
        (Join-Path $RepositoryRoot "src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj"),
        "-c", "Release"
    ) -WorkingDirectory $RepositoryRoot -Stage "Budowanie hosta do testów integracyjnych"
    Invoke-Checked -FilePath $uv.Source -ArgumentList @(
        "run", "--with", "pytest", "--with", "wxpython",
        "python", "-m", "pytest", "tests", "-q"
    ) -WorkingDirectory $WxliteRoot -Stage "Testy interfejsu"
}

# Testy i równoległe zadania nie mogą zmienić źródeł pomiędzy pierwszą
# kontrolą a właściwym budowaniem paczki odbiorczej.
Assert-CleanSource

$projects = @(
    "src\AccessibleMediaController.PythonLauncher\AccessibleMediaController.PythonLauncher.csproj",
    "src\AccessibleMediaController.PythonUpdater\AccessibleMediaController.PythonUpdater.csproj",
    "src\AccessibleMediaController.LiteHost\AccessibleMediaController.LiteHost.csproj"
)
foreach ($project in $projects) {
    Invoke-Checked -FilePath $dotnet.Source -ArgumentList @(
        "publish", (Join-Path $RepositoryRoot $project),
        "-c", "Release", "-r", "win-x64", "--no-self-contained"
    ) -WorkingDirectory $RepositoryRoot -Stage "Budowanie $project"
}
$webHostOutput = Join-Path $RepositoryRoot (
    "src\AccessibleMediaController.LiteHost\bin\Release\" +
    "net8.0-windows10.0.19041.0\win-x64\publish\apple-music-web"
)
Invoke-Checked -FilePath $dotnet.Source -ArgumentList @(
    "publish",
    (Join-Path $RepositoryRoot "src\AccessibleMediaController.AppleMusicWebHost\AccessibleMediaController.AppleMusicWebHost.csproj"),
    "-c", "Release", "-r", "win-x64", "--no-self-contained",
    "-o", $webHostOutput
) -WorkingDirectory $RepositoryRoot -Stage "Budowanie odtwarzacza Apple Music Test"
& (Join-Path $WxliteRoot "tools\Test-AppleMusicWebPipe.ps1") `
    -Executable (Join-Path $webHostOutput "amc_apple_music_web_host.exe")

New-Item -ItemType Directory -Path $TemporaryRoot -Force | Out-Null
New-Item -ItemType Directory -Path $TestOutputRoot -Force | Out-Null
$stagingRoot = Join-Path $TemporaryRoot ("AMC-Python-candidate-" + [guid]::NewGuid().ToString("N"))

if ([string]::IsNullOrWhiteSpace($TidalSitePackages)) {
    throw "AMC Test wymaga -TidalSitePackages z osobnego venv."
}
$tidalSource = [IO.Path]::GetFullPath($TidalSitePackages)
if (-not (Test-Path -LiteralPath $tidalSource -PathType Container)) {
    throw "Nie istnieje katalog zależności TIDAL: $tidalSource"
}
if ([string]::IsNullOrWhiteSpace($SonosSitePackages)) {
    throw "AMC Test wymaga -SonosSitePackages z osobnego venv."
}
$sonosSource = [IO.Path]::GetFullPath($SonosSitePackages)
if (-not (Test-Path -LiteralPath $sonosSource -PathType Container)) {
    throw "Nie istnieje katalog zależności Sonosa: $sonosSource"
}
if ([string]::IsNullOrWhiteSpace($AppleMusicSitePackages)) {
    throw "AMC Test wymaga -AppleMusicSitePackages z osobnego venv."
}
$appleMusicSource = [IO.Path]::GetFullPath($AppleMusicSitePackages)
if (-not (Test-Path -LiteralPath $appleMusicSource -PathType Container)) {
    throw "Nie istnieje katalog zależności Apple Music: $appleMusicSource"
}
$bundleArguments = @(
    $BuildTool,
    "--out", $stagingRoot,
    "--profile", "test",
    "--runtime", (Join-Path $InstallRoot "runtime"),
    "--tidal-site-packages", $tidalSource,
    "--sonos-site-packages", $sonosSource,
    "--apple-music-site-packages", $appleMusicSource,
    "--clean"
)
Invoke-Checked -FilePath $RuntimePython -ArgumentList $bundleArguments -WorkingDirectory $WxliteRoot -Stage "Składanie paczki"

$candidateManifest = Read-BundleManifest -Root $stagingRoot
Assert-ReleaseIdentity -Manifest $candidateManifest
$expectedBuild = [string]$candidateManifest.buildLabel
$createdText = [string]$candidateManifest.created
if ([string]::IsNullOrWhiteSpace($createdText)) {
    throw "Manifest paczki nie zawiera daty utworzenia."
}
$expectedWindowTitle = "AMC Test — " + (
    [DateTimeOffset]::Parse(
        $createdText,
        [Globalization.CultureInfo]::InvariantCulture
    ).ToLocalTime().ToString(
        "dd.MM.yyyy, HH:mm",
        [Globalization.CultureInfo]::InvariantCulture
    )
)
if ([string]::IsNullOrWhiteSpace($expectedBuild) -or $expectedBuild.EndsWith("-robocza")) {
    throw "Paczka odbiorcza nie ma czystej, jednoznacznej etykiety kompilacji."
}
$candidateRoot = Join-Path $TestOutputRoot ("AMC-Python-next-" + $expectedBuild)
if (Test-Path -LiteralPath $candidateRoot) {
    throw "Paczka o tej tożsamości już istnieje: $candidateRoot"
}
Move-Item -LiteralPath $stagingRoot -Destination $candidateRoot

Test-BundleIntegrity -Root $candidateRoot -Manifest $candidateManifest

$candidateLauncher = Join-Path $candidateRoot "AMC.exe"
Invoke-Checked -FilePath $candidateLauncher -ArgumentList @(
    "--sprawdz-pakiet-cicho"
) -WorkingDirectory $candidateRoot -Stage "Sprawdzenie paczki kandydującej"

# Sama zgodność manifestu nie dowodzi, że połączone wersje Pythona, wx i hosta
# potrafią się uruchomić. Ta bramka używa odizolowanego profilu i musi przejść
# przed pierwszym zapisem do stałego katalogu instalacji.
& $StartupSmokeTool -BundleRoot $candidateRoot

$updater = Join-Path $candidateRoot "AMC-Python-Updater.exe"
Invoke-Checked -FilePath $updater -ArgumentList @(
    "--source", $candidateRoot,
    "--target", $InstallRoot,
    "--silent"
) -WorkingDirectory $candidateRoot -Stage "Instalowanie paczki"

$installedManifest = Read-BundleManifest -Root $InstallRoot
Assert-ReleaseIdentity -Manifest $installedManifest
$installedBuild = [string]$installedManifest.buildLabel
if ($installedBuild -ne $expectedBuild) {
    throw "Instalacja ma kompilację $installedBuild, oczekiwano $expectedBuild. Program nie zostanie uruchomiony."
}
Invoke-Checked -FilePath $InstalledLauncher -ArgumentList @(
    "--sprawdz-pakiet-cicho"
) -WorkingDirectory $InstallRoot -Stage "Sprawdzenie zainstalowanej paczki"
Test-BundleIntegrity -Root $InstallRoot -Manifest $installedManifest
# Druga próba sprawdza dokładnie pliki po atomowej podmianie. Jej błąd nie
# pozwala przejść niżej do zwykłego, widocznego uruchomienia programu.
& $StartupSmokeTool -BundleRoot $InstallRoot

$runningTitle = ""
$runningProcessId = $null
$runningExecutable = ""
if (-not $NoLaunch) {
    Start-Process -FilePath $InstalledLauncher -WorkingDirectory $InstallRoot
    $expectedRuntime = [IO.Path]::GetFullPath((Join-Path $InstallRoot "runtime\python.exe"))
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 400
        $process = Get-Process | Where-Object {
            if ($_.MainWindowTitle -ne $expectedWindowTitle) {
                return $false
            }
            try {
                return [IO.Path]::GetFullPath($_.Path).Equals(
                    $expectedRuntime,
                    [StringComparison]::OrdinalIgnoreCase
                )
            }
            catch {
                return $false
            }
        } | Select-Object -First 1
        if ($null -ne $process) {
            $runningTitle = $process.MainWindowTitle
            $runningProcessId = $process.Id
            $runningExecutable = [IO.Path]::GetFullPath($process.Path)
            break
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    if ([string]::IsNullOrWhiteSpace($runningTitle)) {
        throw "Nie potwierdzono uruchomienia kompilacji $expectedBuild."
    }
}

$receipt = [ordered]@{
    createdLocal = [DateTimeOffset]::Now.ToString("yyyy-MM-ddTHH:mm:sszzz")
    sourceRevision = [string]$candidateManifest.sourceRevision
    bundleId = [string]$candidateManifest.bundleId
    buildLabel = $expectedBuild
    candidateRoot = $candidateRoot
    installRoot = $InstallRoot
    installedBuildLabel = $installedBuild
    launched = -not $NoLaunch
    runningWindowTitle = $runningTitle
    runningProcessId = $runningProcessId
    runningExecutable = $runningExecutable
    tests = if ($SkipTests) { "pominięte" } else { "zaliczone" }
}
$receipt | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (
    Join-Path $TestOutputRoot "LAST_TEST_BUILD.json"
) -Encoding UTF8
$summary = if ($NoLaunch) {
    "Zainstalowano kompilację $expectedBuild. Nie uruchomiono programu."
} else {
    "Uruchomiono kompilację $expectedBuild z D:\Projekty Codex\AMC Python\AMC.exe."
}
$summary | Set-Content -LiteralPath (
    Join-Path $TestOutputRoot "LAST_TEST_BUILD.txt"
) -Encoding UTF8
Write-Output $summary
