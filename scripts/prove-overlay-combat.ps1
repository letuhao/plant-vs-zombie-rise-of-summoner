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
