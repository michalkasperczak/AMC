# Pomiar: co dokladnie mowi yt-dlp przy zakonczonej transmisji, prywatnym
# nagraniu i nieistniejacym adresie. Potrzebne, zeby AMC mowilo PRAWDE zamiast
# jednego komunikatu "brak publicznego strumienia audio" na wszystko.
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# yt-dlp (spakowany Python) przeglada KAZDY katalog z PATH przy starcie i
# przewraca sie na WinError 448 "niezaufany punkt instalacji", gdy natrafi na
# sciezke wskazujaca do WSL. Dlatego na czas pomiaru zostawiamy w PATH tylko
# katalogi systemowe Windows.
$env:PATH = "$env:SystemRoot\system32;$env:SystemRoot"
Set-Location $env:TEMP

$yt = (Get-ChildItem "$env:LOCALAPPDATA\AccessibleMediaController\components" -Recurse -Filter yt-dlp.exe -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
if (-not $yt) { Write-Output "BRAK yt-dlp"; exit 1 }
Write-Output "yt-dlp: $yt"
Write-Output ""

$przypadki = @(
    @{ nazwa = "Hermanice (transmisja zakonczona)"; url = "https://www.youtube.com/watch?v=txtC7cxR6m8" },
    @{ nazwa = "Rzeszow Dominikanie"; url = "https://www.youtube.com/watch?v=xU4o5CDb4kU" },
    @{ nazwa = "Nieistniejacy film"; url = "https://www.youtube.com/watch?v=aaaaaaaaaaa" },
    @{ nazwa = "TVP Info (nadaje)"; url = "https://www.youtube.com/watch?v=TxaOAxfWLTM" }
)

foreach ($p in $przypadki) {
    Write-Output "=== $($p.nazwa)"
    $out = & $yt --no-warnings --no-playlist --no-progress `
        --extractor-args "youtube:lang=pl;player_client=default,android" `
        --dump-single-json -- $p.url 2>&1
    Write-Output "kod wyjscia: $LASTEXITCODE"
    $tekst = ($out | Out-String)
    if ($LASTEXITCODE -ne 0) {
        # Interesuje nas TRESC bledu - to ona rozroznia przypadki.
        foreach ($linia in ($tekst -split "`n")) {
            if ($linia -match "ERROR|error") { Write-Output ("BLAD: " + $linia.Trim()) }
        }
    } else {
        try {
            $j = $tekst | ConvertFrom-Json
            Write-Output ("tytul: " + $j.title)
            Write-Output ("na zywo: " + $j.is_live)
            Write-Output ("bylo na zywo: " + $j.was_live)
            Write-Output ("czas trwania: " + $j.duration)
            Write-Output ("status nadawania: " + $j.live_status)
        } catch { Write-Output "JSON sie nie sparsowal" }
    }
    Write-Output ""
}
