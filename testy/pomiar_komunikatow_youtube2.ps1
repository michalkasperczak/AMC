# Pomiar 2: czy komunikaty bledu ROZROZNIAJA przyczyny, i w jakim jezyku.
# Pomiar 1 pokazal, ze przy --extractor-args lang=pl yt-dlp mowi PO POLSKU
# ("Ten film jest niedostepny") - i to samo zdanie dla zakonczonej transmisji,
# nieistniejacego adresu i kanalu, ktory nadaje. Czyli po polsku NIE DA SIE
# rozroznic przyczyn. Sprawdzam, czy bez lang=pl komunikaty sa dokladniejsze.
$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$env:PATH = "$env:SystemRoot\system32;$env:SystemRoot"
Set-Location $env:TEMP

$yt = (Get-ChildItem "$env:LOCALAPPDATA\AccessibleMediaController\components" -Recurse -Filter yt-dlp.exe -ErrorAction SilentlyContinue | Select-Object -First 1).FullName

$przypadki = @(
    @{ n = "Hermanice (zakonczona)"; u = "https://www.youtube.com/watch?v=txtC7cxR6m8" },
    @{ n = "Nieistniejacy"; u = "https://www.youtube.com/watch?v=aaaaaaaaaaa" },
    @{ n = "TVP Info (nadaje)"; u = "https://www.youtube.com/watch?v=TxaOAxfWLTM" }
)
$warianty = @(
    @{ n = "Z lang=pl (jak w AMC dzisiaj)"; a = "youtube:lang=pl;player_client=default,android" },
    @{ n = "BEZ lang (komunikaty angielskie)"; a = "youtube:player_client=default,android" },
    @{ n = "BEZ extractor-args wcale"; a = $null }
)

foreach ($w in $warianty) {
    Write-Output "########## $($w.n)"
    foreach ($p in $przypadki) {
        $args = @("--no-warnings","--no-playlist","--no-progress","--dump-single-json")
        if ($w.a) { $args += @("--extractor-args", $w.a) }
        $args += @("--", $p.u)
        $out = (& $yt @args 2>&1 | Out-String)
        if ($LASTEXITCODE -eq 0) {
            try {
                $j = $out | ConvertFrom-Json
                Write-Output "$($p.n): OK - live_status=$($j.live_status) is_live=$($j.is_live) was_live=$($j.was_live) tytul=$($j.title)"
            } catch { Write-Output "$($p.n): OK ale JSON sie nie sparsowal" }
        } else {
            # Bierzemy CALA tresc po "ERROR:", bez ozdobnikow PowerShella.
            $linie = $out -split "`n" | Where-Object { $_ -match "ERROR" -and $_ -notmatch "CategoryInfo|FullyQualifiedErrorId" }
            $tresc = ($linie | ForEach-Object { ($_ -replace '^.*ERROR:\s*','').Trim() }) -join " | "
            Write-Output "$($p.n): BLAD -> $tresc"
        }
    }
    Write-Output ""
}
