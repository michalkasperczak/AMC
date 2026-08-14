[CmdletBinding()]
param(
    [string]$ConfigPath,
    [string]$BaseUrl = "http://127.0.0.1:8765",
    [string]$Token = $env:NVDA_MCP_BRIDGE_TOKEN,
    [switch]$WriteReport
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot "amc.nvda-smoke.json"
}

function Stop-NvdaSmoke {
    param([string]$Message)
    throw "NVDA smoke: $Message"
}

function Invoke-NvdaBridgeAction {
    param(
        [string]$Action,
        [hashtable]$Headers,
        [string]$Endpoint
    )

    $safeActions = @("get_window_title", "get_current_focus", "get_navigator_object")
    if ($safeActions -notcontains $Action) {
        Stop-NvdaSmoke "Odmowa wykonania niedozwolonej akcji '$Action'."
    }

    $body = @{ action = $Action; params = @{} } | ConvertTo-Json -Compress
    $response = Invoke-RestMethod -Method Post -Uri "$Endpoint/action" -Headers $Headers `
        -ContentType "application/json" -Body $body -TimeoutSec 5 -MaximumRedirection 0

    if (-not $response.success) {
        Stop-NvdaSmoke "Akcja '$Action' nie powiodła się: $($response.error)"
    }

    return $response.result
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    Stop-NvdaSmoke "Brak konfiguracji: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))

$uri = $null
if (-not [Uri]::TryCreate($BaseUrl, [UriKind]::Absolute, [ref]$uri)) {
    Stop-NvdaSmoke "Nieprawidłowy adres mostu."
}
if ($uri.Scheme -ne "http" -or $uri.Host -ne "127.0.0.1" -or $uri.UserInfo -or
    $uri.AbsolutePath -ne "/" -or $uri.Query -or $uri.Fragment) {
    Stop-NvdaSmoke "Most musi używać dokładnie lokalnego adresu http://127.0.0.1."
}
$endpoint = $BaseUrl.TrimEnd("/")

$knownPublicTokens = @(
    "nvda-bridge-secret-change-me",
    "nvda-mcp-secret-token-change-me"
)
if ([string]::IsNullOrWhiteSpace($Token)) {
    Stop-NvdaSmoke "Ustaw losowy token w zmiennej NVDA_MCP_BRIDGE_TOKEN."
}
if ($knownPublicTokens -contains $Token) {
    Stop-NvdaSmoke "Publiczny token przykładowy jest zabroniony. Skonfiguruj losowy token."
}
if ($Token.Length -lt 32) {
    Stop-NvdaSmoke "Token musi mieć co najmniej 32 znaki."
}

$headers = @{ "X-NVDA-Bridge-Token" = $Token }
$health = Invoke-RestMethod -Method Get -Uri "$endpoint/health" -TimeoutSec 3 -MaximumRedirection 0
$toolResponse = Invoke-RestMethod -Method Get -Uri "$endpoint/tools" -Headers $headers -TimeoutSec 3 -MaximumRedirection 0
$toolNames = @($toolResponse.tools | ForEach-Object { [string]$_.name })

$readOnlyProfile = @(
    "get_current_focus",
    "get_window_title",
    "get_navigator_object"
)
$unexpectedTools = @($toolNames | Where-Object { $readOnlyProfile -notcontains $_ })
if ($unexpectedTools.Count -gt 0) {
    Stop-NvdaSmoke ("Most wystawia akcje spoza profilu tylko do odczytu: " +
        (($unexpectedTools | Sort-Object) -join ", ") + ". Użyj utwardzonego profilu testowego.")
}
$missingTools = @($readOnlyProfile | Where-Object { $toolNames -notcontains $_ })
if ($missingTools.Count -gt 0) {
    Stop-NvdaSmoke ("Most nie udostępnia wymaganych akcji: " + ($missingTools -join ", "))
}

$window = Invoke-NvdaBridgeAction -Action "get_window_title" -Headers $headers -Endpoint $endpoint
$focus = Invoke-NvdaBridgeAction -Action "get_current_focus" -Headers $headers -Endpoint $endpoint
$navigator = Invoke-NvdaBridgeAction -Action "get_navigator_object" -Headers $headers -Endpoint $endpoint

$windowTitle = [string]$window.title
$expectedTitle = [string]$config.expectedWindowTitleContains
if (-not [string]::IsNullOrWhiteSpace($expectedTitle) -and
    $windowTitle.IndexOf($expectedTitle, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    Stop-NvdaSmoke "Aktywne okno '$windowTitle' nie jest oknem projektu '$expectedTitle'."
}

$focusName = [string]$focus.name
$focusRole = [string]$focus.role
if ([string]::IsNullOrWhiteSpace($focusName)) {
    Stop-NvdaSmoke "Bieżący element fokusu nie ma dostępnej nazwy."
}
if ([string]::IsNullOrWhiteSpace($focusRole)) {
    Stop-NvdaSmoke "Bieżący element fokusu nie ma dostępnej roli."
}

$report = [ordered]@{
    schemaVersion = 1
    project = [string]$config.project
    checkedAtUtc = [DateTime]::UtcNow.ToString("o")
    bridgeHealth = [string]$health.status
    windowTitle = $windowTitle
    focus = [ordered]@{
        name = $focusName
        role = $focusRole
        roleText = [string]$focus.roleText
        states = @($focus.states)
        windowClassName = [string]$focus.windowClassName
        windowControlID = [string]$focus.windowControlID
        hasValue = -not [string]::IsNullOrEmpty([string]$focus.value)
        hasDescription = -not [string]::IsNullOrEmpty([string]$focus.description)
    }
    navigator = [ordered]@{
        name = [string]$navigator.name
        role = [string]$navigator.role
        states = @($navigator.states)
    }
    privacy = [ordered]@{
        valuesPersisted = $false
        descriptionsPersisted = $false
        currentLinePersisted = $false
        nvdaLogPersisted = $false
    }
}

$json = $report | ConvertTo-Json -Depth 8
if ($WriteReport) {
    $reportDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts\accessibility"))
    if (-not $reportDirectory.StartsWith($projectRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-NvdaSmoke "Nieprawidłowy folder raportu."
    }
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $reportPath = Join-Path $reportDirectory "nvda-smoke-$stamp.json"
    [System.IO.File]::WriteAllText($reportPath, $json, [Text.UTF8Encoding]::new($false))
    Write-Host "Raport: $reportPath"
}

$json
