# Pomiar 3: PRAWDZIWE adresy z bazy AMC Michala (state.json), nie wymyslone.
# Sprawdzam, ktore kanaly dzialaja, a ktore nie, i jak brzmi blad po angielsku.
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$env:PATH = "$env:SystemRoot\system32;$env:SystemRoot"
Set-Location $env:TEMP
$yt = (Get-ChildItem "$env:LOCALAPPDATA\AccessibleMediaController\components" -Recurse -Filter yt-dlp.exe -ErrorAction SilentlyContinue | Select-Object -First 1).FullName

# Nazwy bierzemy z tytulu zwroconego przez YouTube, nie z wlasnych domyslow.
$ids = @("HqvIthIqZpo","X29ZS4rDabU","cqCt9gqk-CI","cGzUGxPGisk","3jKb-uThfrg","txtC7cxR6m8")

foreach ($id in $ids) {
    $out = (& $yt --no-warnings --no-playlist --no-progress `
        --extractor-args "youtube:player_client=default,android" `
        --dump-single-json -- "https://www.youtube.com/watch?v=$id" 2>&1 | Out-String)
    if ($LASTEXITCODE -eq 0) {
        try {
            $j = $out | ConvertFrom-Json
            $formaty = ($j.formats | Where-Object { $_.acodec -and $_.acodec -ne "none" }).Count
            Write-Output "$id : GRA - live_status=$($j.live_status) formatow_audio=$formaty - $($j.title)"
        } catch { Write-Output "$id : kod 0 ale JSON sie nie sparsowal" }
    } else {
        $linie = $out -split "`n" | Where-Object { $_ -match "ERROR" -and $_ -notmatch "CategoryInfo|FullyQualified" }
        $tresc = ($linie | ForEach-Object { ($_ -replace '^.*ERROR:\s*','').Trim() }) -join " | "
        Write-Output "$id : BLAD -> $tresc"
    }
}
