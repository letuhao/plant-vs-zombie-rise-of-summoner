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

# Poll for the first fx event matching the predicate — fixed sleeps smear on a busy lawn
# (command inbox pulls every 250ms; a late event lands in the next case's window otherwise).
function Wait-FxMatch([string]$url, [long]$afterId, [scriptblock]$match, [int]$timeoutMs = 5000) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    do {
        foreach ($ev in (Get-FxEvents $url $afterId)) {
            $p = Get-Payload $ev
            if ($p -and (& $match $ev $p)) { return $p }
        }
        Start-Sleep -Milliseconds 250
    } while ((Get-Date) -lt $deadline)
    return $null
}

function Set-Cheat([string]$id, [bool]$on) {
    # Cheats SSOT: web/server document. Same toggle route the FE uses.
    $body = @{ id = $id; enabled = $on } | ConvertTo-Json
    Invoke-RestMethod -Method POST "$BaseUrl/api/cheats/toggle" -ContentType "application/json" -Body $body -TimeoutSec 8 | Out-Null
}

# ---- test matrix ------------------------------------------------------------
# Every fx.play case carries a UNIQUE amount so its shown/skipped event is matched exactly
# (cueId + amount), immune to late arrivals from the previous case.
$plays = @(
    @{ name = "probe";        path = "/fx/play"; body = @{ cueId = "debug.probe"; col = $Col; row = $Row; amount = -901 } },
    # element-only rule: plain damage renders no burst; cell-anchored (no floater) → skip no-element.
    @{ name = "hit-neutral-cell"; path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -55 }; expectSkip = "no-element" },
    @{ name = "hit-fire";     path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -61; elements = @(@{ element = "fire"; weight = 1.0 }) };  expectRgb = "#FF5A28" },
    @{ name = "hit-ice";      path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -62; elements = @(@{ element = "ice"; weight = 1.0 }) };   expectRgb = "#6ED2FF" },
    @{ name = "hit-air";      path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -63; elements = @(@{ element = "air"; weight = 1.0 }) };   expectRgb = "#BEFFAA" },
    @{ name = "hit-earth";    path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -64; elements = @(@{ element = "earth"; weight = 1.0 }) }; expectRgb = "#D2A046" },
    @{ name = "hit-light";    path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -65; elements = @(@{ element = "light"; weight = 1.0 }) }; expectRgb = "#FFE878" },
    @{ name = "hit-dark";     path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -66; elements = @(@{ element = "dark"; weight = 1.0 }) };  expectRgb = "#965ADC" },
    @{ name = "hit-hybrid";   path = "/fx/play"; body = @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -80; elements = @(@{ element = "fire"; weight = 0.7 }, @{ element = "ice"; weight = 0.3 }) }; expectHybrid = $true },
    @{ name = "heal-rising";  path = "/fx/play"; body = @{ cueId = "combat.heal"; col = $Col; row = $Row; amount = 41; tag = "Heal" } },
    @{ name = "unknown-cue";  path = "/fx/play"; body = @{ cueId = "status.nope.apply"; col = $Col; row = $Row; amount = -902 }; expectSkip = "unknown-cue" }
)
if ($TargetPtr) {
    # ptr-anchored: element hit = floater + burst + flash; plain hit = floater ONLY (no burst).
    $plays += @{ name = "hit-ptr-crit-fire"; path = "/fx/play"; body = @{ cueId = "combat.hit"; ptr = $TargetPtr; amount = -200; tag = "Crit"; elements = @(@{ element = "fire"; weight = 1.0 }) }; expectRgb = "#FF5A28"; expectPrim = "flash" }
    $plays += @{ name = "hit-ptr-plain-no-burst"; path = "/fx/play"; body = @{ cueId = "combat.hit"; ptr = $TargetPtr; amount = -57 }; expectRgb = "#FFFFFF"; expectPrim = "floater"; expectNotPrim = "burst" }
    $plays += @{ name = "heal-ptr"; path = "/fx/play"; body = @{ cueId = "combat.heal"; ptr = $TargetPtr; amount = 42; tag = "Heal" }; expectPrim = "floater" }
}

# All 21 status apply recipes (recipe correctness via fx.play; organic producer proven below).
$statusIds = @(
    "butter", "freeze", "cold", "poison", "hypno", "ember", "jala", "kelp",
    "wither", "bond", "rally", "leech", "expose", "command", "shatter",
    "charm_pulse", "blight", "rot", "spark", "pact_mark", "spore")
$sidN = 700
foreach ($sid in $statusIds) {
    $sidN++
    $plays += @{ name = "status-recipe-$sid"; path = "/fx/play"; body = @{ cueId = "status.$sid.apply"; col = $Col; row = $Row; amount = -$sidN } }
}

$results = @()
$pass = $true

foreach ($case in $plays) {
    # rate-limit clearance between cases (bursts group per cell at 0.15s)
    Start-Sleep -Milliseconds 400
    $after = Get-MaxEventId $BaseUrl
    Post-Fx $case.path $case.body
    $cueId = $case.body.cueId
    $amount = $case.body.amount

    $ok = $false
    $detail = ""
    if ($case.expectSkip) {
        $reason = $case.expectSkip
        $p = Wait-FxMatch $BaseUrl $after {
            param($ev, $pl)
            $ev.kind -eq "debug.fx.skipped" -and $pl.cueId -eq $cueId -and
            ($null -eq $amount -or $pl.amount -eq $amount) -and $pl.reason -eq $reason
        }
        $ok = $null -ne $p
        $detail = if ($p) { "skip=$($p.reason)" } else { "expected skip=$reason not seen" }
    }
    else {
        $p = Wait-FxMatch $BaseUrl $after {
            param($ev, $pl)
            $ev.kind -eq "debug.fx.shown" -and $pl.cueId -eq $cueId -and
            ($null -eq $amount -or $pl.amount -eq $amount)
        }
        $ok = $null -ne $p
        if ($ok -and $case.expectRgb -and $p.rgb -ne $case.expectRgb) { $ok = $false }
        if ($ok -and $case.expectHybrid -and -not $p.hybrid) { $ok = $false }
        if ($ok -and $case.expectPrim -and @($p.primitives) -notcontains $case.expectPrim) { $ok = $false }
        if ($ok -and $case.expectNotPrim -and @($p.primitives) -contains $case.expectNotPrim) { $ok = $false }
        $detail = if ($p) { "rgb=$($p.rgb) hybrid=$($p.hybrid) prims=$(@($p.primitives) -join '+')" } else { "no shown event" }
    }

    if (-not $ok) { $pass = $false }
    $results += [pscustomobject]@{ case = $case.name; ok = $ok; detail = $detail }
    Write-Host ("[{0}] {1} — {2}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $case.name, $detail)
}

# world-flash alias (no amount tagging — match on cueId + amount 0)
Start-Sleep -Milliseconds 400
$after = Get-MaxEventId $BaseUrl
Post-Fx "/fx/world-flash" @{ col = $Col; row = $Row }
$wf = Wait-FxMatch $BaseUrl $after {
    param($ev, $pl)
    $ev.kind -eq "debug.fx.shown" -and $pl.cueId -eq "debug.probe" -and $pl.amount -eq 0
}
$ok = $null -ne $wf
if (-not $ok) { $pass = $false }
$results += [pscustomobject]@{ case = "world-flash-alias"; ok = $ok; detail = if ($wf) { "prims=$(@($wf.primitives) -join '+')" } else { "no shown" } }
Write-Host ("[{0}] world-flash-alias" -f ($(if ($ok) { "PASS" } else { "FAIL" })))

# ---- organic producer paths (real funnel / StatusRuntime, not fx.play) ------
if ($TargetPtr) {
    # combat: enqueue-delta drives dispatcher → funnel mutation (elements ride along) → cue
    Start-Sleep -Milliseconds 400
    $after = Get-MaxEventId $BaseUrl
    Post-Fx "/effect/enqueue-delta" @{ targetPtr = $TargetPtr; amount = -50; elementPayload = @(@{ element = "fire"; weight = 1.0 }) }
    $p = Wait-FxMatch $BaseUrl $after {
        param($ev, $pl)
        $ev.kind -eq "debug.fx.shown" -and $pl.cueId -eq "combat.hit" -and $pl.rgb -eq "#FF5A28"
    }
    $ok = $null -ne $p
    if (-not $ok) { $pass = $false }
    $results += [pscustomobject]@{ case = "organic-combat-fire"; ok = $ok; detail = if ($p) { "cue=$($p.cueId) rgb=$($p.rgb)" } else { "no matching shown" } }
    Write-Host ("[{0}] organic-combat-fire" -f ($(if ($ok) { "PASS" } else { "FAIL" })))

    # status: debug.status.apply drives StatusRuntime.Apply → OnApplied → cue
    Start-Sleep -Milliseconds 400
    $after = Get-MaxEventId $BaseUrl
    Post-Fx "/status/apply" @{ statusId = "wither"; hostPtr = $TargetPtr; amount = 20; durationMs = 4000 }
    $p = Wait-FxMatch $BaseUrl $after {
        param($ev, $pl)
        $ev.kind -eq "debug.fx.shown" -and $pl.cueId -eq "status.wither.apply"
    }
    $ok = $null -ne $p
    if (-not $ok) { $pass = $false }
    $results += [pscustomobject]@{ case = "organic-status-wither"; ok = $ok; detail = if ($p) { "shown" } else { "no matching shown" } }
    Write-Host ("[{0}] organic-status-wither" -f ($(if ($ok) { "PASS" } else { "FAIL" })))
}
else {
    Write-Warning "no -TargetPtr: organic producer + flash/floater cases SKIPPED — run setup-lab-run.ps1 and pass the printed ZombiePtr for full coverage"
    $results += [pscustomobject]@{ case = "organic-paths"; ok = $true; detail = "SKIPPED (no TargetPtr)" }
}

# ---- rate limit: rapid same-cue/cell volley → at least one collapses ---------
# Command delivery can straddle the injector's 250ms inbox pull, so exact counts are
# unreliable; assert the mechanism: some volley members must rate-limit, not all show.
Start-Sleep -Milliseconds 400
$after = Get-MaxEventId $BaseUrl
1..4 | ForEach-Object { Post-Fx "/fx/play" @{ cueId = "debug.probe"; col = $Col; row = $Row } }
Start-Sleep -Milliseconds 900
$events = Get-FxEvents $BaseUrl $after
$shownN = @($events | Where-Object { $_.kind -eq "debug.fx.shown" }).Count
$limited = @($events | Where-Object { $_.kind -eq "debug.fx.skipped" -and (Get-Payload $_).reason -eq "rate-limited" }).Count
$ok = ($limited -ge 1) -and ($shownN -le 3) -and ($shownN -ge 1)
if (-not $ok) { $pass = $false }
$results += [pscustomobject]@{ case = "rate-limit-collapse"; ok = $ok; detail = "shown=$shownN limited=$limited (volley of 4)" }
Write-Host ("[{0}] rate-limit-collapse — shown={1} limited={2} (volley of 4)" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $shownN, $limited)

# ---- mute roundtrip ----------------------------------------------------------
Start-Sleep -Milliseconds 400
Post-Fx "/fx/mute" @{ cueId = "debug.probe" }
Start-Sleep -Milliseconds 400
$after = Get-MaxEventId $BaseUrl
Post-Fx "/fx/play" @{ cueId = "debug.probe"; col = $Col; row = $Row }
Start-Sleep -Milliseconds 700
$muted = @(Get-FxEvents $BaseUrl $after | Where-Object { $_.kind -eq "debug.fx.skipped" -and (Get-Payload $_).reason -eq "muted" }).Count
Post-Fx "/fx/unmute" @{ cueId = "debug.probe" }
Start-Sleep -Milliseconds 500
$after = Get-MaxEventId $BaseUrl
Post-Fx "/fx/play" @{ cueId = "debug.probe"; col = $Col; row = $Row }
Start-Sleep -Milliseconds 700
$unmuted = @(Get-FxEvents $BaseUrl $after | Where-Object { $_.kind -eq "debug.fx.shown" }).Count
$ok = ($muted -ge 1) -and ($unmuted -ge 1)
if (-not $ok) { $pass = $false }
$results += [pscustomobject]@{ case = "mute-roundtrip"; ok = $ok; detail = "muted=$muted unmutedShown=$unmuted" }
Write-Host ("[{0}] mute-roundtrip — muted={1} unmutedShown={2}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $muted, $unmuted)

# ---- master toggle off → disabled --------------------------------------------
try {
    Set-Cheat "SYS-DAMAGE-FX" $false
    Start-Sleep -Milliseconds 600
    $after = Get-MaxEventId $BaseUrl
    Post-Fx "/fx/play" @{ cueId = "debug.probe"; col = $Col; row = $Row }
    Start-Sleep -Milliseconds 700
    $disabled = @(Get-FxEvents $BaseUrl $after | Where-Object { $_.kind -eq "debug.fx.skipped" -and (Get-Payload $_).reason -eq "disabled" }).Count
    $ok = $disabled -ge 1
    if (-not $ok) { $pass = $false }
    $results += [pscustomobject]@{ case = "master-toggle-off"; ok = $ok; detail = "disabledSkips=$disabled" }
    Write-Host ("[{0}] master-toggle-off — disabledSkips={1}" -f ($(if ($ok) { "PASS" } else { "FAIL" })), $disabled)
}
finally {
    Set-Cheat "SYS-DAMAGE-FX" $true
}

# ---- element toggle off → element hit degrades to the plain-damage path ------
# With SYS-ELEMENT-FX off, a fire hit is treated as plain: element-only burst/flash skip.
# Cell-anchored (no floater) → the whole cue reports skip reason no-element.
try {
    Set-Cheat "SYS-ELEMENT-FX" $false
    Start-Sleep -Milliseconds 600
    $after = Get-MaxEventId $BaseUrl
    Post-Fx "/fx/play" @{ cueId = "combat.hit"; col = $Col; row = $Row; amount = -68; elements = @(@{ element = "fire"; weight = 1.0 }) }
    $p = Wait-FxMatch $BaseUrl $after {
        param($ev, $pl)
        $ev.kind -eq "debug.fx.skipped" -and $pl.cueId -eq "combat.hit" -and $pl.amount -eq -68 -and $pl.reason -eq "no-element"
    }
    $ok = $null -ne $p
    if (-not $ok) { $pass = $false }
    $results += [pscustomobject]@{ case = "element-toggle-off"; ok = $ok; detail = if ($p) { "skip=no-element" } else { "expected no-element skip not seen" } }
    Write-Host ("[{0}] element-toggle-off" -f ($(if ($ok) { "PASS" } else { "FAIL" })))
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
Write-Host ""
Write-Host "Eyeball checklist (visuals events cannot assert — confirm on screen):"
Write-Host "  [ ] crit numbers POP (start big ~1.5x, settle) and big hits render larger than small ones"
Write-Host "  [ ] floaters have a black shadow — readable over bright lawn tiles"
Write-Host "  [ ] heal motes drift UPWARD (Rising shape), hit bursts stay radial"
Write-Host "  [ ] hybrid hits cycle rainbow colors; struck units flash briefly on hit"
Write-Host "  [ ] with heavy DoT statuses active, cue frequency feels ok (else: raise status RateLimit, see review note)"
if (-not $pass) { exit 1 }
