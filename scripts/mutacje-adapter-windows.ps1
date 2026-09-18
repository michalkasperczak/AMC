$ErrorActionPreference = "Stop"
# Sonda mutacyjna ADAPTERA na Windows. Adapter (SpotifyLibrespotMediaOutput) da
# sie zmierzyc tylko testem WPF, wiec ta czesc pomiaru musi biec tutaj.
# Zasady te same co w wersji linuksowej: kotwica funkcji, twardy blad gdy
# mutacja nie weszla, przywrocenie w finally, zielony przebieg na koniec.
$repo = "C:\amc_librespot_client_build"
$adapter = "$repo\src\AccessibleMediaController.Windows\Services\SpotifyLibrespotMediaOutput.cs"
$proj = "$repo\tests\AccessibleMediaController.Windows.SmokeTests\AccessibleMediaController.Windows.SmokeTests.csproj"
$exe = "$repo\tests\AccessibleMediaController.Windows.SmokeTests\bin\Debug\net8.0-windows10.0.19041.0\AccessibleMediaController.Windows.SmokeTests.exe"

function Wynik {
    & dotnet build $proj -v q --nologo *> "$env:TEMP\mut-wpf-build.log"
    if ($LASTEXITCODE -ne 0) { return "BUILD-PADL" }
    $out = & cmd /c "`"$exe`" --librespot-output-smoke 2>&1"
    if ($LASTEXITCODE -eq 0) { return "ZIELONY" } else { return "CZERWONY: " + ($out | Select-Object -Last 1) }
}

# Mutacje: nazwa, kotwica funkcji, igla, zamiennik
$mutacje = @(
    @{ nazwa = "adapter-guard-playid"
       kotwica = "private void OnStateChanged(object? sender"
       igla = "e.PlayId != currentPlayId"
       zam  = "false" },
    @{ nazwa = "adapter-pauza-w-przygotowaniu"
       kotwica = "    public void Pause()"
       igla = "isPreparing = false;"
       zam  = "_ = isPreparing;" }
)

$oryginal = Get-Content -Raw -Encoding UTF8 $adapter
try {
    foreach ($m in $mutacje) {
        Write-Output ("=== " + $m.nazwa + " ===")
        $i = $oryginal.IndexOf($m.kotwica)
        if ($i -lt 0) { Write-Output "  SONDA ZLA: brak kotwicy"; continue }
        $ogon = $oryginal.Substring($i)
        $j = $ogon.IndexOf($m.igla)
        if ($j -lt 0) { Write-Output "  SONDA ZLA: brak igly za kotwica"; continue }
        $mut = $oryginal.Substring(0, $i) + $ogon.Remove($j, $m.igla.Length).Insert($j, $m.zam)
        if ($mut -eq $oryginal) { Write-Output "  SONDA ZLA: nic nie zmieniono"; continue }
        $linia = ($oryginal.Substring(0, $i + $j) -split "`n").Count
        Write-Output ("  zmutowana linia: " + $linia)
        Set-Content -NoNewline -Encoding UTF8 $adapter $mut
        $w = Wynik
        Write-Output ("  WPF: " + $w)
        if ($w -like "CZERWONY*") { Write-Output "  WERDYKT: mutacja ZLAPANA" } else { Write-Output "  WERDYKT: mutant UCIEKL" }
        Set-Content -NoNewline -Encoding UTF8 $adapter $oryginal
    }
}
finally {
    Set-Content -NoNewline -Encoding UTF8 $adapter $oryginal
}
Write-Output "=== kontrola koncowa ==="
$teraz = Get-Content -Raw -Encoding UTF8 $adapter
Write-Output ("adapter przywrocony: " + ($teraz -eq $oryginal))
Write-Output ("WPF po przywroceniu: " + (Wynik))
