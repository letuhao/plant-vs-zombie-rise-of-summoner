# Setup a reusable mid-match lab lawn (freeze + clear + fixtures).
# Requires: game running, injector connected, operator already in a normal day lawn
# (Adventure / Challenge). Explore / travel boards are a bad lab surface.
# lab-overlay / lab-empty zero plant vanilla ATK (attackPercent=0, pea atk=0,
# debug.combat.silence-vanilla) so peas do not add noise during overlay prove.
# Usage:
#   .\scripts\setup-lab-run.ps1
#   .\scripts\setup-lab-run.ps1 -Scenario lab-empty
#   .\scripts\setup-lab-run.ps1 -ThenProve
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [ValidateSet("lab-overlay", "lab-empty")]
    [string]$Scenario = "lab-overlay",
    [switch]$ThenProve,
    [int]$TimeoutSec = 60
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

function Get-MaxEventId {
    function Has-After([long]$id) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$id&limit=1" -Method GET
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

function Get-LatestBoardStart {
    # Prefer kinds filter (avoids missing board.start in a combat.hit flood).
    try {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?kinds=board.start&limit=5" -Method GET
        $hits = @($page.items | Where-Object { $_.kind -eq "board.start" })
        if ($hits.Count -gt 0) { return $hits[-1] }
    } catch { }

    $max = Get-MaxEventId
    if ($max -le 0) { return $null }
    $after = [math]::Max(0L, $max - 5000L)
    $cursor = $after
    $last = $null
    for ($i = 0; $i -lt 30; $i++) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$cursor&limit=500" -Method GET
        $items = @($page.items)
        if ($items.Count -eq 0) { break }
        $hits = @($items | Where-Object { $_.kind -eq "board.start" })
        if ($hits.Count -gt 0) { $last = $hits[-1] }
        $cursor = [long]$items[-1].id
        if ($items.Count -lt 500) { break }
    }
    return $last
}

function Test-BoardStillLive([object]$boardStartEv) {
    if (-not $boardStartEv) { return $false }
    $startId = [long]$boardStartEv.id
    try {
        $ends = Invoke-RestMethod -Uri "$BaseUrl/api/events?kinds=board.end&limit=5" -Method GET
        $endHits = @($ends.items | Where-Object { $_.kind -eq "board.end" -and ([long]$_.id) -gt $startId })
        if ($endHits.Count -gt 0) { return $false }
    } catch { }
    return $true
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

function Wait-RunStepsDone([long]$afterId, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$afterId&limit=200" -Method GET
        $items = @($page.items)
        $done = @($items | Where-Object { $_.kind -eq "debug.run-steps.done" })
        if ($done.Count -gt 0) { return $done[-1] }
        $err = @($items | Where-Object { $_.kind -eq "cheat.error" }) | Select-Object -Last 1
        if ($err) {
            $ep = Get-Payload $err
            Write-Host ("cheat.error: {0}" -f ($(if ($ep.message) { $ep.message } elseif ($ep.error) { $ep.error } else { $err.payload })))
        }
        if ($items.Count -gt 0) { $afterId = [long]$items[-1].id }
        Start-Sleep -Milliseconds 300
    }
    throw "timeout waiting for debug.run-steps.done (scenario=$Scenario afterId was advanced to $afterId)"
}

function Wait-BoardSnapshot([long]$afterId, [int]$timeoutSec = 15) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        Invoke-RestMethod -Method POST "$BaseUrl/api/debug/effect/board-snapshot" `
            -ContentType "application/json" -Body '{}' -TimeoutSec 8 | Out-Null
        Start-Sleep -Milliseconds 400
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$afterId&limit=100" -Method GET
        $items = @($page.items)
        $snapEv = @($items | Where-Object { $_.kind -eq "debug.effect.board-snapshot" }) | Select-Object -Last 1
        if ($snapEv) { return (Get-Payload $snapEv) }
        if ($items.Count -gt 0) { $afterId = [long]$items[-1].id }
    }
    return $null
}

Write-Host "Health check..."
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5
if (-not $health.ok) { throw "server health.ok=false" }
if (-not $health.injectorConnected) {
    throw "injector not connected — start game with FusionRpg injector loaded"
}

$bs = Get-LatestBoardStart
if (-not $bs) {
    throw "No board.start in events — enter a normal Adventure day lawn first (main menu → Adventure → any day), then re-run."
}
if (-not (Test-BoardStillLive $bs)) {
    throw "Last board already ended (board.end after board.start). Enter a lawn again, leave it running, then re-run."
}
$bsp = Get-Payload $bs
$levelType = [string]$bsp.levelType
$boardLevel = $bsp.boardLevel
$levelName = [string]$bsp.levelName
Write-Host ("Board live: levelType={0} boardLevel={1} levelName={2}" -f $levelType, $boardLevel, $levelName)

$badTypes = @("Explore", "TravelAdvanture", "Travel", "IZ")
if ($badTypes -contains $levelType) {
    throw @"
Refusing lab on levelType=$levelType boardLevel=$boardLevel (levelName=$levelName).
That mode often looks like 'run never starts' (zero zombie speed / empty Explore).
Return to main menu and open Adventure (or Challenge) day lawn, then re-run.
"@
}

Write-Host "Scenario=$Scenario (freeze + reset + fixtures)..."
$after = Get-MaxEventId
Write-Host "event cursor afterId=$after"
$queued = Invoke-RestMethod -Method POST "$BaseUrl/api/debug/scenario/$Scenario" `
    -ContentType "application/json" -Body '{}' -TimeoutSec 15
Write-Host ("queued steps={0}" -f $queued.steps)

$done = Wait-RunStepsDone -afterId $after -timeoutSec $TimeoutSec
Write-Host "run-steps.done id=$($done.id)"

$snap2 = Wait-BoardSnapshot -afterId ([long]$done.id) -timeoutSec 15
if (-not $snap2) {
    throw "no debug.effect.board-snapshot after lab — injector may not have Board"
}
$plants = @($snap2.entities | Where-Object { $_.side -eq "plant" -and $_.living })
$zombies = @($snap2.entities | Where-Object { $_.side -eq "zombie" -and $_.living })

Write-Host ("living plants={0} zombies={1}" -f $plants.Count, $zombies.Count)
if ($plants.Count -gt 0) {
    Write-Host ("PlantPtr={0}" -f $plants[0].ptr)
}
$targetPtr = ""
if ($zombies.Count -gt 0) {
    $targetPtr = [string]$zombies[0].ptr
    Write-Host ("ZombiePtr={0} (use as -TargetPtr)" -f $targetPtr)
}

if ($Scenario -eq "lab-overlay" -and $zombies.Count -eq 0) {
    throw "lab-overlay finished but no living zombie — check spawn Admit / Board state"
}

$next = ".\scripts\prove-overlay-combat.ps1"
if ($targetPtr) { $next += " -TargetPtr $targetPtr" }
Write-Host "Lab ready. Next: $next"

if ($ThenProve) {
    if ($targetPtr) {
        & (Join-Path $PSScriptRoot "prove-overlay-combat.ps1") -BaseUrl $BaseUrl -TargetPtr $targetPtr
    } else {
        & (Join-Path $PSScriptRoot "prove-overlay-combat.ps1") -BaseUrl $BaseUrl
    }
    exit $LASTEXITCODE
}
