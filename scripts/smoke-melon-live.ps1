# Post-boot Melon LIVE smoke: health gate + debug session + p1-baseline + spawn/damage events.
# Run AFTER deploy-play -LoaderHost MelonLoader and a level lawn is open.
# Usage:
#   .\scripts\smoke-melon-live.ps1
#   .\scripts\smoke-melon-live.ps1 -BaseUrl http://127.0.0.1:5089 -WaitSeconds 3
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [double]$WaitSeconds = 2
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

function Write-Step([string]$name, [bool]$ok, [string]$detail) {
    $mark = if ($ok) { "PASS" } else { "FAIL" }
    Write-Host ("[{0}] {1}: {2}" -f $mark, $name, $detail)
    return $ok
}

$failed = $false

Write-Host "==> Melon LIVE smoke against $BaseUrl"

# 1) Health gate
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
$detail = "injectorConnected=$connected simEnabled=$($health.simEnabled) source=$source"
if (-not (Write-Step "health" $okHealth $detail)) { $failed = $true }

if (-not $okHealth) {
    Write-Host "Aborting: fix Melon injector connection (no SIM, no FUSIONRPG_MELON_SKIP_HARMONY) before scenarios."
    exit 1
}

# 2) Session start
try {
    $session = Invoke-RestMethod -Uri "$BaseUrl/api/debug/session/start" -Method POST -ContentType "application/json" -Body "{}"
    $okSession = [bool]$session.ok
    Write-Step "session/start" $okSession ("ok=$($session.ok) scenarioId=$($session.scenarioId)") | Out-Null
    if (-not $okSession) { $failed = $true }
}
catch {
    Write-Step "session/start" $false $_.Exception.Message | Out-Null
    exit 1
}

# 3) Capture afterId BEFORE scenario (ListEvents is ascending from afterId; afterId=0 = oldest)
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

$afterId = 0L
try {
    $afterId = Get-MaxEventId $BaseUrl
    Write-Host ("afterId baseline: {0}" -f $afterId)
}
catch {
    Write-Host ("WARN: could not capture afterId: {0}" -f $_.Exception.Message)
}

# 4) p1-baseline
try {
    $scenario = Invoke-RestMethod -Uri "$BaseUrl/api/debug/scenario/p1-baseline" -Method POST -ContentType "application/json" -Body "{}"
    $okScenario = [bool]$scenario.ok
    Write-Step "scenario/p1-baseline" $okScenario ("ok=$($scenario.ok) steps=$($scenario.steps)") | Out-Null
    if (-not $okScenario) { $failed = $true }
}
catch {
    Write-Step "scenario/p1-baseline" $false $_.Exception.Message | Out-Null
    $failed = $true
}

# 5) Wait + events (must use afterId or kinds filter sees oldest page only)
Start-Sleep -Seconds $WaitSeconds
try {
    $kinds = "zombie.damage,debug.spawn.plant,debug.spawn.zombie"
    $events = Invoke-RestMethod -Uri "$BaseUrl/api/debug/events?kinds=$kinds&limit=200&afterId=$afterId" -Method GET
    $items = @($events.items)
    $kindsSeen = @($items | ForEach-Object { $_.kind } | Select-Object -Unique)
    $hasPlant = $kindsSeen -contains "debug.spawn.plant"
    $hasZombie = $kindsSeen -contains "debug.spawn.zombie"
    $hasDamage = $kindsSeen -contains "zombie.damage"
    # Spawn is enough for smoke; damage may need pea shots / longer wait
    $okEvents = $hasPlant -or $hasZombie -or $hasDamage
    $detail = "afterId=$afterId count=$($items.Count) kinds=[$($kindsSeen -join ', ')] plant=$hasPlant zombie=$hasZombie damage=$hasDamage"
    if (-not (Write-Step "debug/events" $okEvents $detail)) { $failed = $true }
}
catch {
    Write-Step "debug/events" $false $_.Exception.Message | Out-Null
    $failed = $true
}

Write-Host ""
if ($failed) {
    Write-Host "Melon LIVE smoke: FAILED (see rows above). Continue with docs/runbook/melon-live-checklist.md"
    exit 1
}

Write-Host "Melon LIVE smoke: PASSED health + session + p1-baseline + spawn/damage events."
Write-Host "Next: fill Priority A–C in docs/runbook/melon-live-checklist.md"
exit 0
