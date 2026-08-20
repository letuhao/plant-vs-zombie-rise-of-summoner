# LIVE prove: VFX cue → recipe → primitive pipeline (vfx-ssot.md §11, §16.7).
# Plays every catalog cue through /api/debug/fx/play and asserts one debug.fx.shown per play,
# including per-element combat.hit variants, one hybrid (rainbow) payload, and the
# SYS-ELEMENT-FX-off neutral path (asserted by rgb payload = white).
#
# Requires: game + injector running, lawn open (cell anchors need LawnCoords):
#   .\scripts\setup-lab-run.ps1
#   .\scripts\prove-vfx.ps1
# Pass -TargetPtr from setup-lab-run to also prove the unit-anchored (floater) path.
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [string]$TargetPtr = "",
    [int]$Col = 4,
    [int]$Row = 2,
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
if (-not $OutJson) {
    $OutJson = Join-Path $PSScriptRoot "..\docs\research\effect-runtime\_prove-vfx.json"
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

function Get-FxEvents([string]$url, [long]$afterId) {
    $all = @()
    $cursor = $afterId
    for ($i = 0; $i -lt 40; $i++) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=$cursor&limit=200" -Method GET
        $items = @($page.items)
        if ($items.Count -eq 0) { break }
        $all += @($items | Where-Object { $_.kind -in @("debug.fx.shown", "debug.fx.skipped", "debug.fx.list") })
        $cursor = [long]$items[-1].id
        if ($items.Count -lt 200) { break }
    }
    return $all
}

function Post-Fx([string]$path, $body) {
    $json = if ($null -eq $body) { '{}' } else { $body | ConvertTo-Json -Depth 6 }
    Invoke-RestMethod -Method POST "$BaseUrl/api/debug$path" -ContentType "application/json" -Body $json -TimeoutSec 8 | Out-Null
}

function Set-Cheat([string]$id, [bool]$on) {
    # Cheats SSOT: web/server document. Same toggle route the FE uses.
    $body = @{ id = $id; enabled = $on } | ConvertTo-Json
    Invoke-RestMethod -Method POST "$BaseUrl/api/cheats/toggle" -ContentType "application/json" -Body $body -TimeoutSec 8 | Out-Null
}

# ---- test matrix ------------------------------------------------------------
$plays = @(
    @{ name = "probe";        path = "/fx/play"; body = @{ cueId = "debug.probe"; col = $Col; row = $Row } },
    @{ name = "hit-neutral";  path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -55 }; expectRgb = "#FF8014" },
    @{ name = "hit-fire";     path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -60; elements = @(@{ element = "fire"; weight = 1.0 }) };  expectRgb = "#FF5A28" },
    @{ name = "hit-ice";      path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -60; elements = @(@{ element = "ice"; weight = 1.0 }) };   expectRgb = "#6ED2FF" },
    @{ name = "hit-air";      path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -60; elements = @(@{ element = "air"; weight = 1.0 }) };   expectRgb = "#BEFFAA" },
    @{ name = "hit-earth";    path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -60; elements = @(@{ element = "earth"; weight = 1.0 }) }; expectRgb = "#D2A046" },
    @{ name = "hit-hybrid";   path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -80; elements = @(@{ element = "fire"; weight = 0.7 }, @{ element = "ice"; weight = 0.3 }) }; expectHybrid = $true },
    @{ name = "unknown-cue";  path = "/fx/play"; body = @{ cueId = "status.nope.apply"; col = $Col; row = $Row }; expectSkip = "unknown-cue" },
    @{ name = "world-flash-alias"; path = "/fx/world-flash"; body = @{ col = $Col; row = $Row } }
)
if ($TargetPtr) {
    $plays += @{ name = "hit-ptr-crit-fire"; path = "/fx/play"; body = @{ cueId = "combat.hit"; ptr = $TargetPtr; amount = -200; tag = "Crit"; elements = @(@{ element = "fire"; weight = 1.0 }) }; expectRgb = "#FF5A28" }
    $plays += @{ name = "heal-ptr"; path = "/fx/play"; body = @{ cueId = "combat.heal"; ptr = $TargetPtr; amount = 40; tag = "Heal" } }
}

$results = @()
$pass = $true

foreach ($case in $plays) {
    # rate-limit clearance between cases (bursts group per cell at 0.15s)
    Start-Sleep -Milliseconds 400
    $after = Get-MaxEventId $BaseUrl
    Post-Fx $case.path $case.body
    Start-Sleep -Milliseconds 700
    $events = Get-FxEvents $BaseUrl $after
    $shown = @($events | Where-Object { $_.kind -eq "debug.fx.shown" })
    $skipped = @($events | Where-Object { $_.kind -eq "debug.fx.skipped" })

    $ok = $false
    $detail = ""
    if ($case.expectSkip) {
        $hit = @($skipped | Where-Object { (Get-Payload $_).reason -eq $case.expectSkip })
        $ok = $hit.Count -ge 1
        $detail = "skip=" + (@($skipped | ForEach-Object { (Get-Payload $_).reason }) -join ",")
    }
    else {
        $ok = $shown.Count -ge 1
        $p = if ($shown.Count -ge 1) { Get-Payload $shown[0] } else { $null }
        if ($ok -and $case.expectRgb -and $p.rgb -ne $case.expectRgb) { $ok = $false }
        if ($ok -and $case.expectHybrid -and -not $p.hybrid) { $ok = $false }
        $detail = if ($p) { "rgb=$($p.rgb) hybrid=$($p.hybrid) prims=$(@($p.primitives) -join '+')" } else { "no shown event" }
    }

    if (-not $ok) { $pass = $false }
    $results += [pscustomobject]@{ case = $case.name; ok = $ok; detail = $detail }
    Write-Host ("[{0}] {1} — {2}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $case.name, $detail)
}

# ---- element toggle off → neutral path (white → legacy-orange burst) --------
$toggleResult = $null
try {
    Set-Cheat "SYS-ELEMENT-FX" $false
    Start-Sleep -Milliseconds 600
    $after = Get-MaxEventId $BaseUrl
    Post-Fx "/fx/play" @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -60; elements = @(@{ element = "fire"; weight = 1.0 }) }
    Start-Sleep -Milliseconds 700
    $events = Get-FxEvents $BaseUrl $after
    $shown = @($events | Where-Object { $_.kind -eq "debug.fx.shown" })
    $p = if ($shown.Count -ge 1) { Get-Payload $shown[0] } else { $null }
    $ok = $p -and $p.rgb -eq "#FFFFFF" -and -not $p.hybrid
    if (-not $ok) { $pass = $false }
    $toggleResult = [pscustomobject]@{ case = "element-toggle-off"; ok = $ok; detail = "rgb=$($p.rgb)" }
    $results += $toggleResult
    Write-Host ("[{0}] element-toggle-off — rgb={1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $p.rgb)
}
finally {
    Set-Cheat "SYS-ELEMENT-FX" $true
}

# ---- fx.list roundtrip ------------------------------------------------------
$after = Get-MaxEventId $BaseUrl
Post-Fx "/fx/list" @{}
Start-Sleep -Milliseconds 600
$listEv = @(Get-FxEvents $BaseUrl $after | Where-Object { $_.kind -eq "debug.fx.list" }) | Select-Object -Last 1
$listOk = $false
if ($listEv) {
    $lp = Get-Payload $listEv
    $listOk = (@($lp.cues) -contains "combat.hit") -and (@($lp.cues) -contains "debug.probe")
}
if (-not $listOk) { $pass = $false }
$results += [pscustomobject]@{ case = "fx-list"; ok = $listOk; detail = "cues=" + (@($lp.cues) -join ",") }
Write-Host ("[{0}] fx-list" -f ($(if ($listOk) { "PASS" } else { "FAIL" })))

$summary = [pscustomobject]@{
    at = (Get-Date).ToString("o")
    baseUrl = $BaseUrl
    targetPtr = $TargetPtr
    pass = $pass
    results = $results
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $OutJson -Encoding utf8
Write-Host ""
Write-Host ("prove-vfx: {0} ({1} cases) → {2}" -f ($(if ($pass) { "PASS" } else { "FAIL" })), $results.Count, $OutJson)
if (-not $pass) { exit 1 }
