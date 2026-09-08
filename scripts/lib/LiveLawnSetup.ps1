# All-in-one LIVE lawn setup — enter level 1 (if needed), lab scenario, living zombie ptr.
# SSOT for scripts that need a board without manual Adventure navigation.
# See .claude/skills/live-lawn-quick-start/SKILL.md

function Get-DebugMaxEventId([string]$BaseUrl) {
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

function Get-DebugPayload($ev) {
    if ($null -eq $ev) { return $null }
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) {
        try { return $p | ConvertFrom-Json } catch { return $null }
    }
    return $p
}

function Invoke-DebugPost([string]$BaseUrl, [string]$path, $body) {
    $json = if ($null -eq $body) { '{}' } else { $body | ConvertTo-Json -Depth 8 }
    Invoke-RestMethod -Method POST "$BaseUrl/api/debug$path" -ContentType "application/json" -Body $json -TimeoutSec 15
}

function Wait-LiveBoardSnapshot {
    param(
        [string]$BaseUrl,
        [long]$AfterId,
        [int]$TimeoutSec = 15
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        Invoke-DebugPost $BaseUrl "/effect/board-snapshot" @{} | Out-Null
        Start-Sleep -Milliseconds 400
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$AfterId&limit=100" -Method GET
        $items = @($page.items)
        $snapEv = @($items | Where-Object { $_.kind -eq "debug.effect.board-snapshot" }) | Select-Object -Last 1
        if ($snapEv) { return (Get-DebugPayload $snapEv) }
        if ($items.Count -gt 0) { $AfterId = [long]$items[-1].id }
    }
    return $null
}

function Get-LiveBoardEntities($snapshot) {
    if (-not $snapshot) { return @(), @() }
    $entities = @($snapshot.entities)
    $plants = @($entities | Where-Object { $_.side -eq "plant" -and $_.living })
    $zombies = @($entities | Where-Object { $_.side -eq "zombie" -and $_.living })
    return $plants, $zombies
}

function Get-RecentLiveDebugErrors {
    param(
        [string]$BaseUrl,
        [long]$AfterId = 0
    )
    $lines = @()
    $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$AfterId&limit=200" -Method GET
    foreach ($ev in @($page.items)) {
        if ($ev.kind -notin @("cheat.error", "debug.effect.error")) { continue }
        $p = Get-DebugPayload $ev
        $msg = $null
        if ($p) {
            if ($p.error) { $msg = [string]$p.error }
            elseif ($p.message) { $msg = [string]$p.message }
        }
        if (-not $msg) { $msg = [string]$ev.payload }
        if ($msg) { $lines += "$($ev.kind): $msg" }
    }
    return $lines
}

function Ensure-LiveLabBoard {
    param(
        [string]$BaseUrl = "http://127.0.0.1:5088",
        [ValidateSet("lab-overlay", "lab-empty")]
        [string]$Scenario = "lab-overlay",
        [int]$LevelNumber = 1,
        [int]$TimeoutSec = 60,
        [switch]$SkipSetup
    )
    $BaseUrl = $BaseUrl.TrimEnd('/')

    Write-Host "Ensure-LiveLabBoard: preflight..."
    $health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5
    if (-not $health.ok) { throw "server health.ok=false" }
    if (-not $health.injectorConnected) {
        throw "injector not connected — start game with FusionRpg injector loaded (see live-lawn-quick-start skill)"
    }
    if ($health.simEnabled) {
        Write-Warning "simEnabled=true — LIVE prefers SIM off"
    }

    $entered = $false
    $levelType = ""
    $targetPtr = ""
    $plantPtr = ""
    $cursor = Get-DebugMaxEventId $BaseUrl

    if (-not $SkipSetup) {
        Write-Host "Ensure-LiveLabBoard: POST /lawn/quick-start scenario=$Scenario level=$LevelNumber..."
        try {
            $resp = Invoke-DebugPost $BaseUrl "/lawn/quick-start" @{
                scenario = $Scenario
                levelNumber = $LevelNumber
                timeoutSec = $TimeoutSec
            }
            $entered = [bool]$resp.entered
            $levelType = [string]$resp.levelType
            if ($resp.targetPtr) { $targetPtr = [string]$resp.targetPtr }
            if ($resp.plantPtr) { $plantPtr = [string]$resp.plantPtr }
            if ($resp.note) { Write-Host "  quick-start note: $($resp.note)" }
            Write-Host ("  entered={0} levelType={1} targetPtr={2}" -f $entered, $levelType, $(if ($targetPtr) { $targetPtr } else { "(pending)" }))
        }
        catch {
            $errBody = $_.Exception.Message
            if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $errBody = $_.ErrorDetails.Message }
            throw "lawn/quick-start failed: $errBody"
        }
        $cursor = Get-DebugMaxEventId $BaseUrl
    }
    else {
        Write-Host "Ensure-LiveLabBoard: -SkipSetup — polling board snapshot only"
    }

    if (-not $targetPtr) {
        $snap = Wait-LiveBoardSnapshot -BaseUrl $BaseUrl -AfterId $cursor -TimeoutSec 15
        if ($snap) {
            $plants, $zombies = Get-LiveBoardEntities $snap
            if ($plants.Count -gt 0) { $plantPtr = [string]$plants[0].ptr }
            if ($zombies.Count -gt 0) { $targetPtr = [string]$zombies[0].ptr }
            Write-Host ("  snapshot: living plants={0} zombies={1}" -f $plants.Count, $zombies.Count)
        }
    }

    if ($Scenario -eq "lab-overlay" -and -not $targetPtr) {
        $errs = Get-RecentLiveDebugErrors -BaseUrl $BaseUrl -AfterId $cursor
        $detail = if ($errs.Count -gt 0) { "`nRecent errors:`n  " + ($errs -join "`n  ") } else { "" }
        $skipHint = if ($SkipSetup) {
            "`n-SkipSetup skips /lawn/quick-start — the game must already be on a lab board with living zombies.`nRemove -SkipSetup to auto-enter level 1 + lab-overlay (injector must be connected; game exe must already be running)."
        } else { "" }
        throw "lab board has no living zombie ptr — setup failed.$skipHint$detail"
    }

    if (-not $targetPtr) {
        throw "Ensure-LiveLabBoard: no TargetPtr after setup (scenario=$Scenario SkipSetup=$SkipSetup)"
    }

    Write-Host ("ZombiePtr={0} (TargetPtr)" -f $targetPtr)
    if ($plantPtr) { Write-Host ("PlantPtr={0}" -f $plantPtr) }

    return [pscustomobject]@{
        TargetPtr = $targetPtr
        PlantPtr = $plantPtr
        LevelType = $levelType
        Entered = $entered
        Scenario = $Scenario
    }
}
