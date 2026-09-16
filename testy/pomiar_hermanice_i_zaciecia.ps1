# pomiar_hermanice_i_zaciecia.ps1
# Dwa zgloszenia Michala z 16.09.2026:
#   1) "Znowu urywa Hermanice" - txtC7cxR6m8
#   2) "TVP Info, dominikanie Rzeszow ... dalej strumien prywatny"
#
# Mierzy TYM SAMYM yt-dlp i ffmpeg, ktorego uzywa AMC, i TYMI SAMYMI
# argumentami co YouTubeSourceResolver / FfmpegLocalAudioWaveStream.
# PowerShell 5.1: zadnej ArgumentList, zadnej zmiennej $args.

$ErrorActionPreference = 'Continue'
$baza = Join-Path $env:LOCALAPPDATA 'AccessibleMediaController\components'

# ZMIERZONE 16.09.2026: skladniki NIE leza wprost w components, tylko w
# podkatalogach z numerem wydania (components\yt-dlp\releases\<hash>\yt-dlp.exe).
# Pierwsza wersja tego skryptu szukala wprost w components i konczyla sie
# "BRAK yt-dlp" — szukamy rekurencyjnie i bierzemy najnowszy.
function ZnajdzSkladnik($nazwa) {
    $x = Get-ChildItem -Path $baza -Recurse -Filter $nazwa -ErrorAction SilentlyContinue |
         Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($x) { return $x.FullName }
    return $null
}
$ytdlp  = ZnajdzSkladnik 'yt-dlp.exe'
$ffmpeg = ZnajdzSkladnik 'ffmpeg.exe'

if (-not $ytdlp)  { Write-Output "BRAK yt-dlp pod $baza";  exit 3 }
if (-not $ffmpeg) { Write-Output "BRAK ffmpeg pod $baza"; exit 3 }
Write-Output ("yt-dlp:  " + $ytdlp)
Write-Output ("ffmpeg: " + $ffmpeg)

# AMC czysci PATH (ExternalToolProcess.ApplySafeEnvironment) - bez tego yt-dlp
# wywala sie [WinError 448] na katalogu cua-driver z PATH uzytkownika.
$env:PATH = "$env:SystemRoot\system32;$env:SystemRoot;" + (Split-Path $ytdlp) + ';' + (Split-Path $ffmpeg)

function Uruchom($plik, $argi, $timeoutSek) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $plik
    $psi.Arguments = $argi
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $wy = $p.StandardOutput.ReadToEndAsync()
    $bl = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit($timeoutSek * 1000)) { try { $p.Kill() } catch {}; return @{ Kod = -1; Out = ''; Err = 'TIMEOUT' } }
    return @{ Kod = $p.ExitCode; Out = $wy.Result; Err = $bl.Result }
}

$kanaly = @(
    @{ Nazwa = 'Hermanice';          Id = 'txtC7cxR6m8' },
    @{ Nazwa = 'Rzeszow Dominikanie'; Id = 'NRIOcMdE4DM' },
    @{ Nazwa = 'TVP Info';            Id = '3jKb-uThfrg' }
)

foreach ($k in $kanaly) {
    Write-Output ''
    Write-Output ('=' * 62)
    Write-Output ("KANAL: " + $k.Nazwa + "  (" + $k.Id + ")")
    Write-Output ('=' * 62)

    # KROK 1 - dokladnie to, co robi YouTubeSourceResolver.
    $argi = '--no-warnings --dump-single-json --no-playlist ' +
            '--extractor-args "youtube:lang=pl;player_client=default,android" ' +
            'https://www.youtube.com/watch?v=' + $k.Id
    $r = Uruchom $ytdlp $argi 90
    if ($r.Kod -ne 0) {
        Write-Output ("KROK1 yt-dlp NIEUDANY, kod " + $r.Kod)
        $pierwszy = ($r.Err -split "`n" | Where-Object { $_ -match 'ERROR|error' } | Select-Object -First 2) -join ' | '
        Write-Output ("  blad: " + $pierwszy)
        continue
    }

    $json = $r.Out | ConvertFrom-Json
    Write-Output ("na zywo (is_live): " + $json.is_live)
    Write-Output ("tytul: " + $json.title)

    # KROK 2 - wybor formatu wg logiki SelectAudio: najpierw tylko-audio,
    # potem obraz+dzwiek (fallback muxed dodany dla Hermanic w v375).
    $wybrany = $null
    $skad = ''
    foreach ($tylkoAudio in @($true, $false)) {
        foreach ($poleNazwa in @('requested_downloads','requested_formats')) {
            if ($wybrany) { break }
            $poj = $json.$poleNazwa
            if ($poj) {
                foreach ($c in $poj) {
                    if (-not $c.url) { continue }
                    if (-not $c.acodec -or $c.acodec -eq 'none') { continue }
                    if ($tylkoAudio -and $c.vcodec -and $c.vcodec -ne 'none') { continue }
                    $wybrany = $c; $skad = $poleNazwa + ' audioOnly=' + $tylkoAudio; break
                }
            }
        }
        if (-not $wybrany -and $json.formats) {
            foreach ($c in $json.formats) {
                if (-not $c.url) { continue }
                if (-not $c.acodec -or $c.acodec -eq 'none') { continue }
                if ($tylkoAudio -and $c.vcodec -and $c.vcodec -ne 'none') { continue }
                $wybrany = $c; $skad = 'formats audioOnly=' + $tylkoAudio; break
            }
        }
        if ($wybrany) { break }
    }

    if (-not $wybrany) {
        Write-Output 'WYNIK: SelectAudio NIC BY NIE WYBRAL -> program powie "brak publicznego strumienia audio"'
        $ile = 0; if ($json.formats) { $ile = $json.formats.Count }
        Write-Output ("  dostepnych formatow ogolem: " + $ile)
        if ($json.formats) {
            $json.formats | Select-Object -First 12 | ForEach-Object {
                Write-Output ("   id=" + $_.format_id + " acodec=" + $_.acodec + " vcodec=" + $_.vcodec + " proto=" + $_.protocol)
            }
        }
        continue
    }

    Write-Output ("WYBRANY FORMAT: id=" + $wybrany.format_id + " acodec=" + $wybrany.acodec +
                  " vcodec=" + $wybrany.vcodec + " proto=" + $wybrany.protocol + "  [" + $skad + "]")

    # KROK 3 - 90 s czytania przez ffmpeg tak, jak czyta FfmpegLocalAudioWaveStream:
    # porcjami 19200 B = 50 ms dzwieku 16-bit 48 kHz stereo.
    $ffArgi = '-hide_banner -loglevel error -live_start_index -1 -readrate 1 ' +
              '-rw_timeout 20000000 -reconnect 1 -reconnect_streamed 1 -reconnect_delay_max 2 ' +
              '-i "' + $wybrany.url + '" -vn -f s16le -ar 48000 -ac 2 -'
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $ffmpeg
    $psi.Arguments = $ffArgi
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $pf = [System.Diagnostics.Process]::Start($psi)
    $errTask = $pf.StandardError.ReadToEndAsync()
    $we = $pf.StandardOutput.BaseStream

    $porcja = New-Object byte[] 19200
    $zegar = [System.Diagnostics.Stopwatch]::StartNew()
    $bajty = 0L
    $przestoje = New-Object System.Collections.ArrayList
    $ostatni = 0.0
    $koniecPrzedCzasem = $false

    while ($zegar.Elapsed.TotalSeconds -lt 90) {
        $przed = $zegar.Elapsed.TotalMilliseconds
        $ile = $we.Read($porcja, 0, $porcja.Length)
        $po = $zegar.Elapsed.TotalMilliseconds
        if ($ile -le 0) { $koniecPrzedCzasem = $true; break }
        $bajty += $ile
        $czekanie = $po - $przed
        # 50 ms dzwieku; czekanie ponad 150 ms = karta dzwiekowa nie ma czego grac.
        if ($czekanie -gt 150) { [void]$przestoje.Add([pscustomobject]@{ Ms = [int]$po; Czekanie = [int]$czekanie }) }
        $ostatni = $po
    }
    $zegar.Stop()
    try { if (-not $pf.HasExited) { $pf.Kill() } } catch {}

    $sekundDzwieku = $bajty / 192000.0
    $pokrycie = 0.0
    if ($zegar.Elapsed.TotalSeconds -gt 0) { $pokrycie = 100.0 * $sekundDzwieku / $zegar.Elapsed.TotalSeconds }

    Write-Output ("CZYTANIE: " + [math]::Round($zegar.Elapsed.TotalSeconds,1) + " s zegara, " +
                  [math]::Round($sekundDzwieku,1) + " s dzwieku, pokrycie " + [math]::Round($pokrycie,1) + "%")
    if ($koniecPrzedCzasem) {
        Write-Output ("URWALO SIE PRZED CZASEM na " + [math]::Round($ostatni/1000.0,1) + " s  <<< to jest objaw 'urywa'")
        $bl = $errTask.Result
        if ($bl) { Write-Output ("  ffmpeg: " + (($bl -split "`n" | Where-Object { $_.Trim() } | Select-Object -First 3) -join ' | ')) }
    }
    Write-Output ("PRZESTOJE ponad 150 ms: " + $przestoje.Count)
    if ($przestoje.Count -gt 0) {
        $lista = ($przestoje | Select-Object -First 16 | ForEach-Object { "$($_.Ms)ms/$($_.Czekanie)" }) -join ', '
        Write-Output ("  (czas/dlugosc): " + $lista)
        $sredni = [int](($przestoje | Measure-Object -Property Czekanie -Average).Average)
        $najdl  = [int](($przestoje | Measure-Object -Property Czekanie -Maximum).Maximum)
        Write-Output ("  sredni " + $sredni + " ms, najdluzszy " + $najdl + " ms")
    }
}

Write-Output ''
Write-Output 'KONIEC POMIARU'
