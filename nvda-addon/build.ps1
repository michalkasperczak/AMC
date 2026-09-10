param()
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path -Parent $PSScriptRoot
$addonDirectory = Join-Path $PSScriptRoot 'addon'
$versionLine = Get-Content -LiteralPath (Join-Path $addonDirectory 'manifest.ini') | Where-Object { $_ -match '^version\s*=' }
$addonVersion = ($versionLine -split '=', 2)[1].Trim()
if ($addonVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Nieprawidłowa wersja dodatku.' }
$output = Join-Path $projectDirectory "AMC-NVDA-$addonVersion.nvda-addon"
if (Test-Path -LiteralPath $output) { throw "Pakiet już istnieje: $output. Zwiększ wersję zamiast nadpisywać." }
Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::Open($output, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = Get-ChildItem -LiteralPath $addonDirectory -Recurse -File |
        Where-Object { $_.Extension -in '.py', '.ini', '.html', '.txt' -and $_.FullName -notmatch '__pycache__' }
    foreach ($file in $files) {
        $relative = [System.IO.Path]::GetRelativePath($addonDirectory, $file.FullName).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative) | Out-Null
    }
} finally { $archive.Dispose() }
Get-Item -LiteralPath $output | Select-Object FullName, Length
Get-FileHash -LiteralPath $output -Algorithm SHA256
