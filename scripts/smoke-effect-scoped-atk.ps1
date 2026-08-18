# Melon LIVE prove: scoped FA1 ModifyStat (match / plant:N / entity / mid-spawn / withdraw).
# Requires: lawn open, Melon injector connected, SIM off.
# Usage:
#   .\scripts\smoke-effect-scoped-atk.ps1
#   .\scripts\smoke-effect-scoped-atk.ps1 -BaseUrl http://127.0.0.1:5088 -WaitSeconds 1.5
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [double]$WaitSeconds = 1.5,
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
if (-not $OutJson) {
    $OutJson = Join-Path $PSScriptRoot "..\docs\research\effect-runtime\_prove-melon39-scoped-atk.json"
}

function Write-Step([string]$name, [bool]$ok, [string]$detail) {
    $mark = if ($ok) { "PASS" } else { "FAIL" }
    Write-Host ("[{0}] {1}: {2}" -f $mark, $name, $detail)
    return $ok
}

function Get-MaxEventId([string]$url) {
    function Has-After([long]$id) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$id&limit=1" -Method GET
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
    while (($hi - $lo) -gt 1L) {
        $mid = $lo + (($hi - $lo) / 2L)
        if (Has-After $mid) { $lo = $mid } else { $hi = $mid }
    }
    $tail = Invoke-RestMethod -Uri "$url/api/events?afterId=$lo&limit=500" -Method GET
    $batch = @($tail.items)
    if ($batch.Count -eq 0) { return $lo }
    return [long]$batch[-1].id
}

function Get-Payload($ev) {
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) {
        try { return $p | ConvertFrom-Json } catch { return $null }
    }
    return $p
}

function Get-BoardStatsAfter([string]$url, [long]$afterId, [string]$tag = "", [double]$timeoutSec = 8) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $cursor = $afterId
    while ((Get-Date) -lt $deadline) {
        # Page through events: combat flood can bury board-stats past the first page.
        $events = Invoke-RestMethod -Uri "$url/api/events?afterId=$cursor&limit=500" -Method GET
        $items = @($events.items)
        if ($items.Count -eq 0) {
            Start-Sleep -Milliseconds 400
            continue
        }
        $cursor = [long]$items[-1].id
        $matches = @($items | Where-Object { $_.kind -eq "debug.board-stats" })
        if ($tag) {
            $matches = @($matches | Where-Object {
                $pl = Get-Payload $_
                $pl -and ("$($pl.tag)" -eq $tag)
            })
        }
        if ($matches.Count -gt 0) {
            return Get-Payload $matches[-1]
        }
        Start-Sleep -Milliseconds 300
    }
    return $null
}

function Get-PlantAt($board, [int]$col) {
    if ($null -eq $board -or $null -eq $board.plants) { return $null }
    foreach ($pl in @($board.plants)) {
        if ([int]$pl.col -eq $col) { return $pl }
    }
    return $null
}

function Get-PlantByType($board, [int]$typeId) {
    if ($null -eq $board -or $null -eq $board.plants) { return $null }
    foreach ($pl in @($board.plants)) {
        if ([int]$pl.typeId -eq $typeId) { return $pl }
    }
    return $null
}

function Test-Scenario([string]$id, [string]$url, [double]$wait) {
    $afterId = Get-MaxEventId $url
    try {
        $queued = Invoke-RestMethod -Uri "$url/api/debug/scenario/$id" -Method POST -ContentType "application/json" -Body "{}"
        if (-not [bool]$queued.ok) {
            return @{ id = $id; pass = $false; note = "scenario queue failed" }
        }
    }
    catch {
        return @{ id = $id; pass = $false; note = $_.Exception.Message }
    }

    Start-Sleep -Seconds $wait

    switch ($id) {
        "effect-entity-atk" {
            $board = Get-BoardStatsAfter $url $afterId
            if ($null -eq $board) { return @{ id = $id; pass = $false; note = "no debug.board-stats" } }
            $p1 = Get-PlantAt $board 1
            $p3 = Get-PlantAt $board 3
            if ($null -eq $p1 -or $null -eq $p3) {
                return @{ id = $id; pass = $false; note = "need peas at col 1 and 3" }
            }
            $ok = [double]$p1.attack -gt [double]$p3.attack
            return @{ id = $id; pass = $ok; note = "col1=$($p1.attack) col3=$($p3.attack)" }
        }
        "effect-plant-type-atk" {
            $board = Get-BoardStatsAfter $url $afterId
            if ($null -eq $board) { return @{ id = $id; pass = $false; note = "no debug.board-stats" } }
            $pea = Get-PlantByType $board 0
            $wall = Get-PlantByType $board 3
            if ($null -eq $pea -or $null -eq $wall) {
                return @{ id = $id; pass = $false; note = "need pea(type0) and wallnut(type3)" }
            }
            $ok = [double]$pea.attack -gt [double]$wall.attack
            return @{ id = $id; pass = $ok; note = "pea=$($pea.attack) wall=$($wall.attack)" }
        }
        "effect-match-midspawn" {
            $board = Get-BoardStatsAfter $url $afterId
            if ($null -eq $board) { return @{ id = $id; pass = $false; note = "no debug.board-stats" } }
            $p1 = Get-PlantAt $board 1
            $p3 = Get-PlantAt $board 3
            if ($null -eq $p1 -or $null -eq $p3) {
                return @{ id = $id; pass = $false; note = "need peas at col 1 and 3" }
            }
            $ok = [Math]::Abs([double]$p1.attack - [double]$p3.attack) -le 0.5
            return @{ id = $id; pass = $ok; note = "col1=$($p1.attack) col3=$($p3.attack)" }
        }
        "effect-spawn-then-grant" {
            $board = Get-BoardStatsAfter $url $afterId
            if ($null -eq $board) { return @{ id = $id; pass = $false; note = "no debug.board-stats" } }
            $p1 = Get-PlantAt $board 1
            $p3 = Get-PlantAt $board 3
            if ($null -eq $p1 -or $null -eq $p3) {
                return @{ id = $id; pass = $false; note = "need peas at col 1 and 3" }
            }
            $ok = [double]$p1.attack -gt [double]$p3.attack
            return @{ id = $id; pass = $ok; note = "col1=$($p1.attack) col3=$($p3.attack)" }
        }
        "effect-entity-midspawn" {
            $boardGrant = Get-BoardStatsAfter $url $afterId "after-grant"
            $boardWithdraw = Get-BoardStatsAfter $url $afterId "after-withdraw"
            if ($null -eq $boardGrant -or $null -eq $boardWithdraw) {
                return @{ id = $id; pass = $false; note = "missing after-grant/after-withdraw board-stats" }
            }
            $g1 = Get-PlantAt $boardGrant 1
            $g3 = Get-PlantAt $boardGrant 3
            $w1 = Get-PlantAt $boardWithdraw 1
            $w3 = Get-PlantAt $boardWithdraw 3
            if ($null -eq $g1 -or $null -eq $g3 -or $null -eq $w1 -or $null -eq $w3) {
                return @{ id = $id; pass = $false; note = "missing plants in tagged board-stats" }
            }
            $okGrant = [double]$g1.attack -gt [double]$g3.attack
            $okRestore = [double]$w1.attack -lt [double]$g1.attack
            $okSib = [Math]::Abs([double]$w1.attack - [double]$w3.attack) -le 0.5
            $ok = $okGrant -and $okRestore -and $okSib
            return @{
                id = $id
                pass = $ok
                note = "g1=$($g1.attack) g3=$($g3.attack) w1=$($w1.attack) w3=$($w3.attack)"
            }
        }
        default {
            return @{ id = $id; pass = $false; note = "unknown scenario assert" }
        }
    }
}

$results = @()
$failed = $false

Write-Host "==> Melon scoped-ATK prove against $BaseUrl"

try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET
}
catch {
    Write-Step "health" $false $_.Exception.Message | Out-Null
    exit 1
}
$connected = [bool]$health.injectorConnected
$simOff = -not [bool]$health.simEnabled
$source = [string]$health.source
$okHealth = $connected -and $simOff -and ($source -eq "injector")
if (-not (Write-Step "health" $okHealth "injectorConnected=$connected source=$source")) {
    exit 1
}

Invoke-RestMethod -Uri "$BaseUrl/api/debug/session/start" -Method POST -ContentType "application/json" -Body "{}" | Out-Null

foreach ($id in @(
        "effect-entity-atk",
        "effect-plant-type-atk",
        "effect-match-midspawn",
        "effect-spawn-then-grant",
        "effect-entity-midspawn"
    )) {
    $r = Test-Scenario $id $BaseUrl $WaitSeconds
    Write-Step $r.id ([bool]$r.pass) $r.note | Out-Null
    $results += $r
    if (-not $r.pass) { $failed = $true }
}

$payload = [ordered]@{
    at       = (Get-Date).ToString("o")
    game     = "pvzrh-3.9"
    baseUrl  = $BaseUrl
    passed   = @($results | Where-Object { $_.pass }).Count
    total    = $results.Count
    results  = $results
    status   = if ($failed) { "PENDING_LIVE" } else { "PASS" }
    note     = "Run with Melon lawn open after deploy-play -LoaderHost MelonLoader"
}
$dir = Split-Path -Parent $OutJson
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
($payload | ConvertTo-Json -Depth 8) | Set-Content -LiteralPath $OutJson -Encoding UTF8
Write-Host ("Wrote {0}" -f $OutJson)

if ($failed) { exit 1 }
exit 0
