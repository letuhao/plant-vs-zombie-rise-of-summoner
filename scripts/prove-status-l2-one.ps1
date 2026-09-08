# LIVE prove: one StatusRuntime L2 scenario (organic atom path).
# All-in-one: Ensure-LiveLabBoard runs first unless -SkipSetup.
# Usage:
#   .\scripts\prove-status-l2-one.ps1 -Scenario status-l2-rot
# See .claude/skills/live-lawn-quick-start/SKILL.md
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [string]$Scenario = "status-l2-wither",
    [int]$TimeoutSec = 90,
    [switch]$SkipSetup
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
. (Join-Path $PSScriptRoot "lib\LiveLawnSetup.ps1")

if (-not $SkipSetup) {
    Ensure-LiveLabBoard -BaseUrl $BaseUrl | Out-Null
}

Write-Host "Running scenario $Scenario..."
$tip = Get-DebugMaxEventId $BaseUrl
Invoke-DebugPost $BaseUrl "/scenario/$Scenario" @{} | Out-Null

$deadline = (Get-Date).AddSeconds($TimeoutSec)
$done = $false
do {
    $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$tip&limit=100" -Method GET
    foreach ($ev in @($page.items)) {
        if ($ev.kind -eq "debug.run-steps.done") { $done = $true; break }
    }
    if ($done) { break }
    Start-Sleep -Milliseconds 400
} while ((Get-Date) -lt $deadline)

if (-not $done) { throw "scenario '$Scenario' did not complete within ${TimeoutSec}s" }

$page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$tip&limit=300" -Method GET
$started = @($page.items | Where-Object { $_.kind -eq "debug.fx.state.started" }).Count
$applied = @($page.items | Where-Object { $_.kind -eq "debug.status.apply" }).Count
$statusEv = @($page.items | Where-Object { $_.kind -eq "debug.status" }).Count

Write-Host "  fx.state.started=$started debug.status.apply=$applied debug.status=$statusEv"
if ($started -lt 1 -and $applied -lt 1) {
    throw "no organic status apply evidence in events"
}
Write-Host "PASS $Scenario"
