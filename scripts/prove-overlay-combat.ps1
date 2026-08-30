# LIVE prove: overlay combat + Element Hub (C1–C10) — narrow telemetry only.
# Complex damage math SSOT is offline: dotnet test ... --filter FullyQualifiedName~Combat
#
# Requires: lawn open + lab fixtures (preferred):
#   .\scripts\setup-lab-run.ps1
#   .\scripts\prove-overlay-combat.ps1 -TargetPtr <printed ZombiePtr>
#
# Do not invent board state here. Pass -TargetPtr from setup-lab-run when possible.
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [string]$TargetPtr = "",
    [string]$ActorPtr = "",
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
if (-not $OutJson) {
    $OutJson = Join-Path $PSScriptRoot "..\docs\research\effect-runtime\_prove-overlay-combat.json"
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
    while ($lo + 1L -lt $hi) {
        $mid = [long](($lo + $hi) / 2L)
        if (Has-After $mid) { $lo = $mid } else { $hi = $mid }
    }
    return $lo
}

function Get-Payload($ev) {
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) {
        try { return $p | ConvertFrom-Json } catch { return $null }
    }
    return $p
}

function Get-OverlayEvents([string]$url, [long]$afterId) {
    $all = @()
    $cursor = $afterId
    for ($i = 0; $i -lt 40; $i++) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$cursor&limit=200" -Method GET
        $items = @($page.items)
        if ($items.Count -eq 0) { break }
        $all += @($items | Where-Object { $_.kind -eq "debug.combat.overlay" })
        $cursor = [long]$items[-1].id
        if ($items.Count -lt 200) { break }
    }
    return $all
}

function Wait-CombatSnapshot([string]$url, [int]$timeoutSec = 15) {
    $after = Get-MaxEventId $url
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        Invoke-RestMethod -Method POST "$url/api/debug/combat/snapshot" `
            -ContentType "application/json" -Body '{}' -TimeoutSec 8 | Out-Null
        Start-Sleep -Milliseconds 400
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$after&limit=100" -Method GET
        $items = @($page.items)
        $snapEv = @($items | Where-Object { $_.kind -eq "debug.combat.snapshot" }) | Select-Object -Last 1
        if ($snapEv) { return (Get-Payload $snapEv) }
        if ($items.Count -gt 0) { $after = [long]$items[-1].id }
    }
    return $null
}

function Invoke-Probe([string]$url, [hashtable]$body) {
    $json = $body | ConvertTo-Json -Depth 8 -Compress
    Invoke-RestMethod -Method POST "$url/api/debug/combat/probe" `
        -ContentType "application/json" -Body $json | Out-Null
}

# C11-C13 (aura-skill T7): the probe endpoint (Invoke-Probe above) special-cases ANY positive
# `amount` as a raw pass-through (DebugCombatActions.Probe's own `amount > 0` branch), which never
# calls OverlayCombatMath.Finalize/FinalizeHeal at all -- confirmed by direct code read, not assumed.
# C5 above only proves that pass-through shape (no overlay breakdown), which is a real but DIFFERENT
# claim from "a heal actually reads combat.heal.power". The one debug path that DOES route a positive
# amount through the real CombatDamageDispatcher.DispatchInstant -> OverlayCombatMath.Finalize chain
# is `debug/effect/enqueue-delta` when it carries a `target` object (CheatCommandRunner.RunEnqueueDelta's
# own `useCombatDispatch` gate) -- but that command has no channel-pinning of its own. The fix used
# here: `InjectorDerivedOverride`'s pin store is a persistent, ptr-keyed dictionary
# (InjectorCombatBridge.ResolveActor consults it on every resolve, regardless of which debug command
# set it) -- so a zero-amount probe call sets `combat.heal.power` on a ptr, and a LATER enqueue-delta
# call against that SAME ptr reads it for real, through the real math.

function Invoke-PinActorChannels([string]$url, [string]$ptr, [hashtable]$channels) {
    Invoke-Probe $url @{
        targetPtr = $ptr
        actorPtr = $ptr
        amount = 0   # no-op: DebugCombatActions.Probe's passApply==0 short-circuits the funnel write
        pinActorChannels = $channels
    }
}

function Invoke-EnqueueDelta([string]$url, [hashtable]$body) {
    $json = $body | ConvertTo-Json -Depth 8 -Compress
    Invoke-RestMethod -Method POST "$url/api/debug/effect/enqueue-delta" `
        -ContentType "application/json" -Body $json | Out-Null
}

function Get-EntityHp([string]$url, [string]$ptr) {
    Invoke-RestMethod -Method POST "$url/api/debug/board-stats" -ContentType "application/json" -Body '{}' -TimeoutSec 8 | Out-Null
    $before = Get-MaxEventId $url
    $deadline = (Get-Date).AddSeconds(6)
    while ((Get-Date) -lt $deadline) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$before&limit=50" -Method GET
        $items = @($page.items)
        $ev = @($items | Where-Object { $_.kind -eq "debug.board-stats" }) | Select-Object -Last 1
        if ($ev) {
            $payload = Get-Payload $ev
            $entry = @($payload.plants) + @($payload.zombies) | Where-Object { $_.ptr -eq $ptr } | Select-Object -First 1
            if ($entry) { return [double]$entry.hp }
            throw "ptr $ptr not found in debug.board-stats"
        }
        if ($items.Count -gt 0) { $before = [long]$items[-1].id }
        Start-Sleep -Milliseconds 300
    }
    throw "no debug.board-stats event"
}

function Wait-LastOverlay([string]$url, [long]$afterId, [int]$timeoutMs = 4000) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 250
        $ev = Get-OverlayEvents $url $afterId | Select-Object -Last 1
        if ($ev) { return (Get-Payload $ev) }
    }
    # Fallback once: snapshot lastOverlay
    $snapAfter = Get-MaxEventId $url
    Invoke-RestMethod -Method POST "$url/api/debug/combat/snapshot" `
        -ContentType "application/json" -Body '{}' -TimeoutSec 8 | Out-Null
    Start-Sleep -Milliseconds 400
    $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$snapAfter&limit=50" -Method GET
    $snapEv = @($page.items | Where-Object { $_.kind -eq "debug.combat.snapshot" }) | Select-Object -Last 1
    if ($snapEv) {
        $snap = Get-Payload $snapEv
        if ($snap -and $snap.lastOverlay -and $snap.lastOverlay.source) {
            return $snap.lastOverlay
        }
    }
    return $null
}

Write-Host "Health..."
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5
if (-not $health.injectorConnected) {
    throw "injector not connected — start game + FusionRpg injector first"
}

Write-Host "Enabling OVERLAY-COMBAT..."
Invoke-RestMethod -Method POST "$BaseUrl/api/cheats/toggle" -ContentType "application/json" `
    -Body '{"id":"OVERLAY-COMBAT","enabled":true}' | Out-Null

if ([string]::IsNullOrWhiteSpace($TargetPtr) -or [string]::IsNullOrWhiteSpace($ActorPtr)) {
    Write-Host "Resolving board via combat/snapshot (prefer setup-lab-run.ps1)..."
    $snap = Wait-CombatSnapshot $BaseUrl
    if (-not $snap) { throw "no combat snapshot — run .\scripts\setup-lab-run.ps1 first" }
    if ([string]::IsNullOrWhiteSpace($TargetPtr)) {
        $z = @($snap.entities | Where-Object { $_.side -eq "zombie" -and $_.living } | Select-Object -First 1)
        if ($z.Count -eq 0) {
            throw "No living zombie — run .\scripts\setup-lab-run.ps1 then pass -TargetPtr <ZombiePtr>"
        }
        $TargetPtr = [string]$z[0].ptr
    }
    if ([string]::IsNullOrWhiteSpace($ActorPtr)) {
        $p = @($snap.entities | Where-Object { $_.side -eq "plant" -and $_.living } | Select-Object -First 1)
        if ($p.Count -gt 0) { $ActorPtr = [string]$p[0].ptr }
    }
}
Write-Host "Target ptr: $TargetPtr"
if ($ActorPtr) { Write-Host "Actor ptr: $ActorPtr" }

Write-Host "Silence vanilla plant ATK..."
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/combat/silence-vanilla" `
    -ContentType "application/json" -Body '{"plant":true}' | Out-Null

Invoke-RestMethod -Method POST "$BaseUrl/api/debug/session/start" -ContentType "application/json" -Body '{}' | Out-Null

$results = @()

function Run-Case([string]$Name, [scriptblock]$Body) {
    $ok = $false
    $detail = ""
    try {
        $detail = & $Body
        $ok = $true
    } catch {
        $detail = $_.Exception.Message
    }
    Write-Host ("[{0}] {1}: {2}" -f $(if ($ok) { "PASS" } else { "FAIL" }), $Name, $detail)
    $script:results += [ordered]@{ name = $Name; pass = $ok; detail = $detail }
}

function Assert-Matchup([string]$name, [string]$pinEl, $payload, [double]$expectedBonus) {
    Run-Case $name {
        $before = Get-MaxEventId $BaseUrl
        $body = @{
            amount = -100
            targetPtr = $TargetPtr
            seed = 1
            forceHit = $true
            forceCrit = $false
            pinTargetElement = $pinEl
            elementPayload = $payload
        }
        Invoke-Probe $BaseUrl $body
        $p = Wait-LastOverlay $BaseUrl $before
        if (-not $p) { throw "no debug.combat.overlay" }
        $bonus = [double]$p.matchupBonus
        if ([math]::Abs($bonus - $expectedBonus) -gt 0.5) {
            throw "matchupBonus=$bonus expected ~$expectedBonus"
        }
        "matchupBonus=$bonus"
    }
}

Assert-Matchup "C1 overlay-fire-vs-ice" "ice" @(@{ element = "fire"; weight = 1.0 }) 25
Assert-Matchup "C2 overlay-fire-vs-air" "air" @(@{ element = "fire"; weight = 1.0 }) (-25)
Assert-Matchup "C3 overlay-hybrid-vs-ice" "ice" @(
    @{ element = "fire"; weight = 0.7 },
    @{ element = "air"; weight = 0.3 }
) 17.5

Run-Case "C4 overlay-miss" {
    $before = Get-MaxEventId $BaseUrl
    Invoke-Probe $BaseUrl @{
        amount = -100
        targetPtr = $TargetPtr
        seed = 1
        forceMiss = $true
        pinTargetElement = "ice"
        elementPayload = @(@{ element = "fire"; weight = 1.0 })
    }
    $p = Wait-LastOverlay $BaseUrl $before
    if (-not $p) { throw "no debug.combat.overlay" }
    if ($p.hit -ne $false) { throw "hit=$($p.hit) expected false" }
    if ([int]$p.finalSignedDelta -ne 0) { throw "finalSignedDelta=$($p.finalSignedDelta) expected 0" }
    "hit=false finalSignedDelta=0"
}

Run-Case "C5 overlay-heal" {
    $before = Get-MaxEventId $BaseUrl
    Invoke-Probe $BaseUrl @{
        amount = 50
        targetPtr = $TargetPtr
        seed = 1
        elementPayload = @(@{ element = "fire"; weight = 1.0 })
    }
    Start-Sleep -Milliseconds 600
    $overlays = @(Get-OverlayEvents $BaseUrl $before)
    if ($overlays.Count -gt 0) { throw "unexpected debug.combat.overlay on heal" }
    "no overlay breakdown; heal pass-through"
}

Run-Case "C6 overlay-flag-off" {
    Invoke-RestMethod -Method POST "$BaseUrl/api/cheats/toggle" -ContentType "application/json" `
        -Body '{"id":"OVERLAY-COMBAT","enabled":false}' | Out-Null
    Start-Sleep -Milliseconds 300
    $before = Get-MaxEventId $BaseUrl
    Invoke-Probe $BaseUrl @{
        amount = -100
        targetPtr = $TargetPtr
        seed = 1
        forceHit = $true
        pinTargetElement = "ice"
        elementPayload = @(@{ element = "fire"; weight = 1.0 })
    }
    Start-Sleep -Milliseconds 600
    $overlays = @(Get-OverlayEvents $BaseUrl $before)
    if ($overlays.Count -gt 0) { throw "unexpected debug.combat.overlay when flag off" }
    Invoke-RestMethod -Method POST "$BaseUrl/api/cheats/toggle" -ContentType "application/json" `
        -Body '{"id":"OVERLAY-COMBAT","enabled":true}' | Out-Null
    "pass-through -100; no overlay emit"
}

Assert-Matchup "C7 overlay-ice-vs-fire" "fire" @(@{ element = "ice"; weight = 1.0 }) (-25)
Assert-Matchup "C8 overlay-air-vs-earth" "earth" @(@{ element = "air"; weight = 1.0 }) (-25)
Assert-Matchup "C9 overlay-earth-vs-air" "air" @(@{ element = "earth"; weight = 1.0 }) 25

Run-Case "C10 overlay-force-crit" {
    if ([string]::IsNullOrWhiteSpace($ActorPtr)) {
        throw "need living plant ActorPtr for crit channels (lab-overlay)"
    }
    $before = Get-MaxEventId $BaseUrl
    Invoke-Probe $BaseUrl @{
        amount = -100
        targetPtr = $TargetPtr
        actorPtr = $ActorPtr
        seed = 1
        forceHit = $true
        forceCrit = $true
        pinTargetElement = "ice"
        pinActorChannels = @{
            "combat.accuracy.omni" = 500
            "combat.crit.damage.omni" = 500
            "combat.crit.rate.omni" = 500
        }
        elementPayload = @(@{ element = "fire"; weight = 1.0 })
    }
    $p = Wait-LastOverlay $BaseUrl $before
    if (-not $p) { throw "no debug.combat.overlay" }
    if ($p.crit -ne $true) { throw "crit=$($p.crit) expected true" }
    $mult = [double]$p.critMultiplierFinal
    if ($mult -le 1.0) { throw "critMultiplierFinal=$mult expected > 1" }
    "crit=true critMultiplierFinal=$mult"
}

Run-Case "C11 overlay-heal-with-payload-scales-with-heal-power" {
    if ([string]::IsNullOrWhiteSpace($TargetPtr)) { throw "need TargetPtr" }
    Invoke-PinActorChannels $BaseUrl $TargetPtr @{ "combat.heal.power" = 40 }
    $before = Get-EntityHp $BaseUrl $TargetPtr
    Invoke-EnqueueDelta $BaseUrl @{
        targetPtr = $TargetPtr
        amount = 10
        target = @{ mode = "single"; ptr = $TargetPtr }
        elementPayload = @(@{ element = "fire"; weight = 1.0 })
    }
    Start-Sleep -Milliseconds 400
    $after = Get-EntityHp $BaseUrl $TargetPtr
    $healed = $after - $before
    # effectiveHeal = max(0, signedAmount + healPower) = max(0, 10 + 40) = 50 -- FinalizeHeal's own
    # formula (OverlayCombatMath.cs). Capped at maxHp, which setup-lab-run's fixtures leave headroom
    # under -- if this ever fails on a full-HP target, heal a smaller amount first via debug tooling.
    if ([math]::Abs($healed - 50) -gt 0.5) { throw "healed=$healed expected ~50 (10 base + 40 heal.power)" }
    "healed=$healed (expected ~50)"
}

Run-Case "C12 overlay-heal-with-no-payload-still-reads-heal-power" {
    # spec: `if (signedAmount > 0) return FinalizeHeal(...)` runs BEFORE the payload-null check
    # (OverlayCombatMath.Finalize) -- so a heal WITHOUT any elementPayload must still scale by
    # combat.heal.power, not silently fall through to "amount unchanged" the way a PAYLOAD-LESS
    # DAMAGE packet would (Finalize's own `if (packet.ElementPayload == null) return signedAmount`,
    # which only applies below the heal branch, never reached for signedAmount > 0).
    if ([string]::IsNullOrWhiteSpace($TargetPtr)) { throw "need TargetPtr" }
    Invoke-PinActorChannels $BaseUrl $TargetPtr @{ "combat.heal.power" = 40 }
    $before = Get-EntityHp $BaseUrl $TargetPtr
    Invoke-EnqueueDelta $BaseUrl @{
        targetPtr = $TargetPtr
        amount = 10
        target = @{ mode = "single"; ptr = $TargetPtr }   # forces useCombatDispatch; deliberately NO elementPayload
    }
    Start-Sleep -Milliseconds 400
    $after = Get-EntityHp $BaseUrl $TargetPtr
    $healed = $after - $before
    if ([math]::Abs($healed - 50) -gt 0.5) {
        throw "healed=$healed expected ~50 -- a value of exactly 10 here would mean the no-payload heal fell through to raw signedAmount instead of FinalizeHeal"
    }
    "healed=$healed (expected ~50, proving FinalizeHeal ran despite no payload)"
}

Run-Case "C13 overlay-full-mitigation-resolves-to-zero-no-chip-floor" {
    # owner decision 6: the overlay profile's MinChipShareKPm is 0 (CombatProfiles.Overlay = new(0)),
    # unlike every other profile's 50‰ floor -- a fully-mitigated overlay hit must resolve to EXACTLY
    # 0, not a guaranteed minimum chip. Sky-high combat.defense.omni on the defender drives
    # DivisiveMitigation's powerAdjusted to 0 (Math.Max(0, powerAdjusted) floors it), and 0 times any
    # crit/amp multiplier is still 0.
    if ([string]::IsNullOrWhiteSpace($TargetPtr)) { throw "need TargetPtr" }
    Invoke-PinActorChannels $BaseUrl $TargetPtr @{ "combat.defense.omni" = 999999999 }
    $before = Get-MaxEventId $BaseUrl
    Invoke-Probe $BaseUrl @{
        amount = -100
        targetPtr = $TargetPtr
        seed = 1
        forceHit = $true
        elementPayload = @(@{ element = "fire"; weight = 1.0 })
    }
    $p = Wait-LastOverlay $BaseUrl $before
    if (-not $p) { throw "no debug.combat.overlay" }
    if ([int]$p.finalSignedDelta -ne 0) { throw "finalSignedDelta=$($p.finalSignedDelta) expected exactly 0 -- a nonzero value here would mean a chip floor leaked into the overlay profile" }
    "finalSignedDelta=0, no exception -- the game handled full mitigation cleanly"
}

$payload = @{
    at = (Get-Date).ToString("o")
    targetPtr = $TargetPtr
    actorPtr = $ActorPtr
    results = $results
}
$payload | ConvertTo-Json -Depth 6 | Set-Content -Path $OutJson -Encoding UTF8
Write-Host "Wrote $OutJson"

$fail = @($results | Where-Object { -not $_.pass }).Count
if ($fail -gt 0) { exit 1 }
