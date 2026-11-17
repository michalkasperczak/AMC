[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
# This narrowly authorized operation must never run on Hermes or a user PC.
if ($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_ENVIRONMENT -cne 'github-hosted' -or $env:RUNNER_OS -cne 'Windows' -or $env:GITHUB_REF -cne 'refs/heads/codex/amc-diagnostic-gh-protected-20261118-verified') {
    throw 'Protection setup is restricted to the authorized GitHub-hosted Windows job.'
}
$diagWarnings=@()
function Assert-NoDiagnosticWarnings {
    if ($diagWarnings.Count -gt 0) { throw ('Defender warning: ' + ($diagWarnings -join '; ')) }
}
function Get-ProtectionSnapshot {
    param($Status, $Preference)
    [ordered]@{
        antivirusEnabled=$Status.AntivirusEnabled
        realTimeProtectionEnabled=$Status.RealTimeProtectionEnabled
        behaviorMonitorEnabled=$Status.BehaviorMonitorEnabled
        ioavProtectionEnabled=$Status.IoavProtectionEnabled
        onAccessProtectionEnabled=$Status.OnAccessProtectionEnabled
        mode=$Status.AMRunningMode
        engine=$Status.AMEngineVersion
        signature=$Status.AntivirusSignatureVersion
        signatureUpdated=$Status.AntivirusSignatureLastUpdated
        disableRealtimeMonitoring=$Preference.DisableRealtimeMonitoring
        disableBehaviorMonitoring=$Preference.DisableBehaviorMonitoring
        disableIOAVProtection=$Preference.DisableIOAVProtection
        disableArchiveScanning=$Preference.DisableArchiveScanning
        disableScriptScanning=$Preference.DisableScriptScanning
        exclusionPaths=@($Preference.ExclusionPath | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        exclusionProcessCount=@($Preference.ExclusionProcess | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count
        exclusionExtensionCount=@($Preference.ExclusionExtension | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count
    }
}
New-Item -ItemType Directory -Path $EvidenceDirectory -Force | Out-Null
$report=[ordered]@{stage='authorized-protection-setup'; startedUtc=[DateTime]::UtcNow.ToString('o'); finishedUtc=$null; exitCode=1; before=$null; after=$null; removedDefaultExclusions=@(); error=$null}
try {
    $status=Get-MpComputerStatus -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
    $pref=Get-MpPreference -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
    Assert-NoDiagnosticWarnings
    $report.before=Get-ProtectionSnapshot -Status $status -Preference $pref
    $paths=@($pref.ExclusionPath | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    if (@($paths | Where-Object { $_ -notin @('C:\','D:\') }).Count -gt 0 -or $report.before.exclusionProcessCount -gt 0 -or $report.before.exclusionExtensionCount -gt 0) { throw 'Unexpected exclusions: do not modify an unknown environment.' }
    $existingThreats=@(Get-MpThreat -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings)
    $existingDetections=@(Get-MpThreatDetection -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings)
    Assert-NoDiagnosticWarnings
    if ($existingThreats.Count -gt 0 -or $existingDetections.Count -gt 0) { throw 'Existing threat state blocks protection preparation and building.' }
    # Only strengthen protection. Do not change sample submission, cloud consent,
    # tamper protection, policies, services, quarantine, or historical event logs.
    $settings=@{DisableRealtimeMonitoring=$false; DisableBehaviorMonitoring=$false; DisableIOAVProtection=$false; DisableArchiveScanning=$false; DisableScriptScanning=$false; DisableScanningNetworkFiles=$false; DisableAutoExclusions=$true}
    Set-MpPreference @settings -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
    Assert-NoDiagnosticWarnings
    if ($paths.Count -gt 0) {
        Remove-MpPreference -ExclusionPath $paths -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
        Assert-NoDiagnosticWarnings
        $report.removedDefaultExclusions=$paths
    }
    Update-MpSignature -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
    Assert-NoDiagnosticWarnings
    $deadline=[DateTime]::UtcNow.AddSeconds(60)
    do {
        $status=Get-MpComputerStatus -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
        $pref=Get-MpPreference -ErrorAction Stop -WarningAction Continue -WarningVariable +diagWarnings
        Assert-NoDiagnosticWarnings
        $report.after=Get-ProtectionSnapshot -Status $status -Preference $pref
        $ready=$status.AntivirusEnabled -and $status.RealTimeProtectionEnabled -and $status.BehaviorMonitorEnabled -and $status.IoavProtectionEnabled -and $status.OnAccessProtectionEnabled -and $status.AMRunningMode -eq 'Normal' -and -not $pref.DisableRealtimeMonitoring -and -not $pref.DisableBehaviorMonitoring -and -not $pref.DisableIOAVProtection -and -not $pref.DisableArchiveScanning -and -not $pref.DisableScriptScanning -and $report.after.exclusionPaths.Count -eq 0 -and $report.after.exclusionProcessCount -eq 0 -and $report.after.exclusionExtensionCount -eq 0
        if ($ready) { break }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $ready) { throw 'Active protection was not confirmed: no downloads, build or packaging permitted.' }
    $report.exitCode=0
} catch { $report.error=$_.Exception.Message; throw } finally {
    $report.finishedUtc=[DateTime]::UtcNow.ToString('o')
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'protection-setup.json') -Encoding utf8
}
