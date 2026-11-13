[CmdletBinding()]
param([Parameter(Mandatory)] [string]$Executable)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$path = [IO.Path]::GetFullPath($Executable)
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    throw "Brak procesu Apple Music w paczce."
}
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $path
$start.WorkingDirectory = Split-Path -Parent $path
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
[void]$start.ArgumentList.Add("--pipe-probe")
$process = [Diagnostics.Process]::Start($start)
if ($null -eq $process) {
    throw "Nie udało się uruchomić procesu Apple Music."
}
try {
    $process.StandardInput.WriteLine("ping")
    $process.StandardInput.Close()
    if (-not $process.WaitForExit(15000)) {
        $process.Kill()
        throw "Proces Apple Music nie odpowiedział na próbę potoku."
    }
    $reply = $process.StandardOutput.ReadToEnd().Trim()
    if ($process.ExitCode -ne 0 -or $reply -ne "pong") {
        throw "Proces Apple Music nie obsługuje przekierowanego potoku."
    }
    Write-Output "Potok procesu Apple Music: OK"
}
finally {
    $process.Dispose()
}
