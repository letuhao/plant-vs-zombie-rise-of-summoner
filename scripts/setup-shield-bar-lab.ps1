# Setup a frozen lawn with pea + zombie, each with 3 RPG shield stacks (fire/ice/earth).
# Preferred: python -m live_test run shield.lab   (tools/live_test)
# See docs/runbook/live-test-ssot.md
# Use this BEFORE looking for the in-game shield bar — do not use the bare probe alone.
#
# Requires: Melon/Bep injector connected, operator already in an Adventure day lawn
# (same gate as setup-lab-run.ps1 — Explore/Travel boards refuse).
#
# Usage:
#   .\scripts\setup-shield-bar-lab.ps1
#
# What it does:
#   1. wave-freeze + reset board
#   2. spawn pea at col=2 row=2
#   3. spawn zombie at row=2 x=7.5
#   4. debug.shield.demo-all → fire/ice/earth ×100 on EVERY living plant+zombie
#   5. prints PlantPtr / ZombiePtr + shield snapshot
#
# In-game: look under the pea and the zombie for multi-stop bars labeled ~100% ×3.
# F9 toggles bars; F7 overlay settings.
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [int]$TimeoutSec = 60
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

function Get-MaxEventId {
    function Has-After([long]$id) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$id&limit=1"
        return @($page.items).Count -gt 0
    }
    if (-not (Has-After 0)) { return 0L }
    $lo = 0L
    $hi = 1L
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

function Get-Payload($ev) {
    if ($null -eq $ev) { return $null }
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) {
        try { return $p | ConvertFrom-Json } catch { return $null }
    }
    return $p
}

function Get-LatestBoardStart {
    try {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?kinds=board.start&limit=5"
        $hits = @($page.items | Where-Object { $_.kind -eq "board.start" })
        if ($hits.Count -gt 0) { return $hits[-1] }
    } catch { }
    return $null
}

function Test-BoardStillLive([object]$boardStartEv) {
    if (-not $boardStartEv) { return $false }
    $startId = [long]$boardStartEv.id
    try {
        $ends = Invoke-RestMethod -Uri "$BaseUrl/api/events?kinds=board.end&limit=5"
        $endHits = @($ends.items | Where-Object { $_.kind -eq "board.end" -and ([long]$_.id) -gt $startId })
        if ($endHits.Count -gt 0) { return $false }
    } catch { }
    return $true
}

function Wait-Kind([long]$afterId, [string]$kind, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $cursor = $afterId
    while ((Get-Date) -lt $deadline) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$cursor&limit=200"
        $items = @($page.items)
        $hit = @($items | Where-Object { $_.kind -eq $kind }) | Select-Object -Last 1
        if ($hit) { return $hit }
        if ($items.Count -gt 0) { $cursor = [long]$items[-1].id }
        Start-Sleep -Milliseconds 250
    }
    return $null
}

Write-Host "== health =="
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5
if (-not $health.ok) { throw "server health.ok=false" }
if (-not $health.injectorConnected) {
    throw "injector not connected — start Melon/Bep game with FusionRpg loaded, enter a lawn, re-run"
}
Write-Host ("  source={0} injectorConnected={1}" -f $health.source, $health.injectorConnected)

$bs = Get-LatestBoardStart
if (-not $bs) {
    throw "No board.start — open Adventure day lawn first (main menu → Adventure → any day), leave it running, re-run."
}
if (-not (Test-BoardStillLive $bs)) {
    throw "Last board already ended. Enter a lawn again, leave it running, re-run."
}
$bsp = Get-Payload $bs
$levelType = [string]$bsp.levelType
Write-Host ("Board live: levelType={0} boardLevel={1} levelName={2}" -f $levelType, $bsp.boardLevel, $bsp.levelName)
$badTypes = @("Explore", "TravelAdvanture", "Travel", "IZ")
if ($badTypes -contains $levelType) {
    throw "Refusing lab on levelType=$levelType — use Adventure/Challenge day lawn."
}

Write-Host "== scenario lab-shield-bar (freeze + spawn pea/zombie + demo-all 3 stacks) =="
$after = Get-MaxEventId
Write-Host ("  afterId tip={0}" -f $after)
$queued = Invoke-RestMethod -Method POST "$BaseUrl/api/debug/scenario/lab-shield-bar" `
    -ContentType "application/json" -Body '{}' -TimeoutSec 15
Write-Host ("  queued steps={0}" -f $queued.steps)

$done = Wait-Kind -afterId $after -kind "debug.run-steps.done" -timeoutSec $TimeoutSec
if (-not $done) { throw "timeout waiting for debug.run-steps.done" }
Write-Host ("  run-steps.done id={0}" -f $done.id)

$demoAll = Wait-Kind -afterId ([long]$done.id) -kind "debug.shield.demo-all" -timeoutSec 15
if (-not $demoAll) {
    # demo-all may have landed before run-steps.done cursor; scan from scenario afterId
    $demoAll = Wait-Kind -afterId $after -kind "debug.shield.demo-all" -timeoutSec 5
}
if (-not $demoAll) { throw "no debug.shield.demo-all — spawn/demo failed" }
$demoPayload = Get-Payload $demoAll
Write-Host ("  demo-all targets={0} amount={1}" -f $demoPayload.targetCount, $demoPayload.amount)
foreach ($t in @($demoPayload.targets)) {
    Write-Host ("    ptr={0} stacks={1}" -f $t.targetPtr, $t.count)
}

$snapEv = Wait-Kind -afterId $after -kind "debug.shield.snapshot" -timeoutSec 10
$snap = Get-Payload $snapEv
if (-not $snap -or $snap.ownerCount -lt 1) {
    throw "shield snapshot empty — bars will not show (no stacks)"
}

Write-Host ""
Write-Host "== living shielded units =="
foreach ($o in @($snap.owners)) {
    $side = if ($o.ownerKey -match "plant") { "plant?" } else { "unit" }
    Write-Host ("  ptr={0} hp={1}/{2} stacks={3}" -f $o.ptr, $o.hp, $o.maxHp, $o.stackCount)
    foreach ($s in @($o.stacks)) {
        Write-Host ("    - {0} {1}/{2}" -f $s.element, $s.hp, $s.maxHp)
    }
}

# Board snapshot for plant/zombie labels
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/effect/board-snapshot" `
    -ContentType "application/json" -Body '{}' | Out-Null
Start-Sleep -Milliseconds 500
$boardEv = Wait-Kind -afterId (Get-MaxEventId) -kind "debug.effect.board-snapshot" -timeoutSec 8
# re-fetch near tip
$tip = Get-MaxEventId
$page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$([Math]::Max(0,$tip-80))&limit=80"
$boardEv = @($page.items | Where-Object { $_.kind -eq "debug.effect.board-snapshot" }) | Select-Object -Last 1
$board = Get-Payload $boardEv
$plants = @()
$zombies = @()
if ($board -and $board.entities) {
    $plants = @($board.entities | Where-Object { $_.side -eq "plant" -and $_.living })
    $zombies = @($board.entities | Where-Object { $_.side -eq "zombie" -and $_.living })
}

Write-Host ""
Write-Host "== fixtures =="
if ($plants.Count -gt 0) {
    Write-Host ("  PlantPtr={0}  (pea col=2 row=2)" -f $plants[0].ptr)
} else {
    Write-Warning "no living plant in board-snapshot"
}
if ($zombies.Count -gt 0) {
    Write-Host ("  ZombiePtr={0} (basic row=2 x≈7.5)" -f $zombies[0].ptr)
} else {
    Write-Warning "no living zombie in board-snapshot"
}

Write-Host ""
Write-Host "Lab ready. Look in-game under the pea AND the zombie for fire→ice→earth bars (~100% ×3)."
Write-Host "F9 = toggle shield bars. F7 = Overlay Settings."
Write-Host "Re-grant only: POST /api/debug/shield/demo-all  body { `"amount`": 100 }"
