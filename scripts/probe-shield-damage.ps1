# Damage RPG shields and show bar fillRatio shrink.
# Preferred: python -m live_test run shield.absorb   (tools/live_test)
# See docs/runbook/live-test-ssot.md
# Prerequisites: Adventure lawn live, injector connected, units with shields
#   (.\scripts\setup-shield-bar-lab.ps1 or demo-all first).
#
# Usage:
#   .\scripts\probe-shield-damage.ps1
#   .\scripts\probe-shield-damage.ps1 -Amount -200 -Setup
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [int]$Amount = -150,
    [switch]$Setup
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
if ($Amount -ge 0) { throw "-Amount must be negative damage (e.g. -150)" }

function Get-MaxEventId {
    function Has-After([long]$id) {
        return @((Invoke-RestMethod "$BaseUrl/api/events?afterId=$id&limit=1").items).Count -gt 0
    }
    if (-not (Has-After 0)) { return 0L }
    $lo = 0L; $hi = 1L
    while (Has-After $hi) {
        $lo = $hi
        if ($hi -gt [long]::MaxValue / 2) { break }
        $hi = $hi * 2L
    }
    while ($lo + 1L -lt $hi) {
        $mid = [long](($lo + $hi) / 2L)
        if (Has-After $mid) { $lo = $mid } else { $hi = $mid }
    }
    return $lo
}

function Wait-Kind([long]$afterId, [string]$kind, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $cursor = $afterId
    while ((Get-Date) -lt $deadline) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$cursor&limit=100"
        $items = @($page.items)
        $hit = @($items | Where-Object { $_.kind -eq $kind }) | Select-Object -Last 1
        if ($hit) { return $hit }
        if ($items.Count -gt 0) { $cursor = [long]$items[-1].id }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

function Get-Payload($ev) {
    if ($null -eq $ev) { return $null }
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) { try { return $p | ConvertFrom-Json } catch { return $null } }
    return $p
}

$health = Invoke-RestMethod "$BaseUrl/health" -TimeoutSec 5
if (-not $health.injectorConnected) { throw "injector not connected" }

if ($Setup) {
    & "$PSScriptRoot\setup-shield-bar-lab.ps1" -BaseUrl $BaseUrl
}

Write-Host "== enable OVERLAY-COMBAT (needed for element probe path; passthrough also absorbs after injector fix) =="
Invoke-RestMethod -Method POST "$BaseUrl/api/cheats/toggle" -ContentType "application/json" `
    -Body '{"id":"OVERLAY-COMBAT","enabled":true}' | Out-Null

Write-Host "== ensure shields =="
$after = Get-MaxEventId
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/shield/demo-all" `
    -ContentType "application/json" -Body '{"amount":100}' | Out-Null
$demo = Get-Payload (Wait-Kind $after "debug.shield.demo-all" 15)
if (-not $demo -or [int]$demo.targetCount -lt 1) { throw "demo-all got 0 targets — enter Adventure lawn (not Idle)" }
$zPtr = @($demo.targets)[-1].targetPtr
Write-Host ("  targets={0} hitPtr={1}" -f $demo.targetCount, $zPtr)

Write-Host "== BEFORE =="
$after = Get-MaxEventId
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/shield/snapshot" -ContentType "application/json" `
    -Body (@{ targetPtr = $zPtr } | ConvertTo-Json) | Out-Null
$before = Get-Payload (Wait-Kind $after "debug.shield.snapshot" 8)
foreach ($o in @($before.owners)) {
    Write-Host ("  hp={0}/{1}" -f $o.hp, $o.maxHp)
}

Write-Host ("== combat.probe amount={0} ==" -f $Amount)
$after = Get-MaxEventId
$body = @{
    amount = $Amount
    targetPtr = $zPtr
    forceHit = $true
    seed = 1
    elementPayload = @(@{ element = "fire"; weightPm = 1000 })
} | ConvertTo-Json -Depth 5
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/combat/probe" -ContentType "application/json" -Body $body | Out-Null
$probe = Get-Payload (Wait-Kind $after "debug.combat.probe" 12)
Write-Host ("  source={0} hit={1} shieldAbsorbed={2} appliedDelta={3}" -f `
    $probe.source, $probe.hit, $probe.shieldAbsorbed, $probe.appliedDelta)

Write-Host "== AFTER =="
$after = Get-MaxEventId
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/shield/snapshot" -ContentType "application/json" `
    -Body (@{ targetPtr = $zPtr } | ConvertTo-Json) | Out-Null
$afterSnap = Get-Payload (Wait-Kind $after "debug.shield.snapshot" 8)
foreach ($o in @($afterSnap.owners)) {
    $trueRatio = if ([double]$o.maxHp -gt 0) { [double]$o.hp / [double]$o.maxHp } else { 0 }
    $display = [Math]::Floor($trueRatio * 10) / 10
    if ($trueRatio -gt 0 -and $display -eq 0) { $display = 0.1 }
    Write-Host ("  hp={0}/{1} true={2:N2} displayFill={3:N1}" -f $o.hp, $o.maxHp, $trueRatio, $display)
    foreach ($s in @($o.stacks)) {
        Write-Host ("    {0} {1}/{2}" -f $s.element, $s.hp, $s.maxHp)
    }
}

Write-Host "In-game: bar fill length uses 10% steps (displayFill), not every HP tick."
Write-Host "Repeat probe to drain further; stacks break outer→inner (fire then ice then earth)."
