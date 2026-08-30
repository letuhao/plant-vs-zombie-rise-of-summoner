# Shared LIVE helpers: apply StatusRuntime status until sustained VFX starts.
# SSOT for POST /api/debug/status/apply payloads (not /apply-status — Unity CC bypass only).
# Dot-sources LiveLawnSetup.ps1 for board setup + HTTP/event helpers.

. (Join-Path $PSScriptRoot "LiveLawnSetup.ps1")

function Get-LiveTargetPtr {
    param(
        [string]$BaseUrl = "http://127.0.0.1:5088",
        [string]$Scenario = "lab-overlay",
        [int]$LevelNumber = 1,
        [int]$TimeoutSec = 60,
        [switch]$SkipSetup
    )
    $lab = Ensure-LiveLabBoard -BaseUrl $BaseUrl -Scenario $Scenario -LevelNumber $LevelNumber `
        -TimeoutSec $TimeoutSec -SkipSetup:$SkipSetup
    return [string]$lab.TargetPtr
}

function Wait-StatusFxStarted {
    param(
        [string]$BaseUrl,
        [long]$AfterId,
        [string]$StatusId,
        [int]$TimeoutMs = 2500
    )
    $deadline = (Get-Date).AddMilliseconds($TimeoutMs)
    do {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$AfterId&limit=200" -Method GET
        foreach ($ev in @($page.items)) {
            if ($ev.kind -ne "debug.fx.state.started") { continue }
            $p = Get-DebugPayload $ev
            if ($p -and $p.statusId -eq $StatusId) { return $true }
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    return $false
}

function Invoke-StatusApplyUntilStarted {
    param(
        [string]$BaseUrl = "http://127.0.0.1:5088",
        [Parameter(Mandatory = $true)][string]$StatusId,
        [Parameter(Mandatory = $true)][string]$HostPtr,
        [int]$DurationMs = 6000,
        [long]$Amount = 20,
        [int]$MaxTries = 6
    )
    $BaseUrl = $BaseUrl.TrimEnd('/')
    for ($i = 0; $i -lt $MaxTries; $i++) {
        $win = Get-DebugMaxEventId $BaseUrl
        Invoke-DebugPost $BaseUrl "/status/apply" @{
            statusId = $StatusId
            hostPtr = $HostPtr
            amount = $Amount
            durationMs = $DurationMs
        } | Out-Null
        if (Wait-StatusFxStarted $BaseUrl $win $StatusId) { return $true }
    }
    return $false
}

function Clear-StatusTarget {
    param(
        [string]$BaseUrl = "http://127.0.0.1:5088",
        [Parameter(Mandatory = $true)][string]$HostPtr
    )
    Invoke-DebugPost $BaseUrl.TrimEnd('/') "/clear-status" @{ ptr = $HostPtr } | Out-Null
}
