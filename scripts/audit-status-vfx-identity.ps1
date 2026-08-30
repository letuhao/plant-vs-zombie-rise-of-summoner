# Status VFX identity audit harness — static matrix export + optional LIVE stress/event checks.
# Plan: docs/research/vfx/status-identity-audit-2026-08-30.md
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [string]$TargetPtr = "",
    [switch]$Live,
    [switch]$Stress,
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutJson) {
    $OutJson = Join-Path $repoRoot "docs\research\vfx\_status-identity-audit.json"
}

$customIds = @(
    "wither", "blight", "rot", "spark", "spore", "pact_mark", "leech",
    "expose", "shatter", "bond", "rally", "command", "charm_pulse")

function Get-StatusFxRow($id) {
    $catalog = Join-Path $repoRoot "src\FusionRpg.Core\Vfx\VfxCatalog.cs"
    $text = Get-Content $catalog -Raw
    # Apply RGB from StatusFx array — parse via dotnet test output instead for accuracy
    return $null
}

# Static matrix via dotnet test (pure C# SSOT)
Write-Host "Running static identity + aura math tests..."
$testOut = & dotnet test (Join-Path $repoRoot "tests\FusionRpg.Core.Tests\FusionRpg.Core.Tests.csproj") `
    --filter "FullyQualifiedName~StatusVfxIdentity|FullyQualifiedName~VfxAuraMath" `
    --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $testOut
    throw "Static identity tests failed"
}
Write-Host "Static tests: PASS"

# Emit signature matrix by invoking a tiny inline eval through dotnet run is heavy;
# instead parse from a one-off script target. Use PowerShell mirror of VfxCatalog rows (audit snapshot).
$statusFx = @{
    wither = @(140, 110, 90); blight = @(130, 160, 60); rot = @(120, 90, 50)
    spark = @(255, 240, 120); spore = @(150, 200, 90); pact_mark = @(170, 90, 220)
    leech = @(180, 60, 60); expose = @(250, 250, 140); shatter = @(200, 230, 255)
    bond = @(255, 170, 200); rally = @(255, 200, 90); command = @(120, 140, 255)
    charm_pulse = @(240, 120, 240)
}
$sustain = @{
    wither = @{ aura = "WispOut"; tint = 0.25; marker = $null }
    blight = @{ aura = "BubbleRise"; tint = 0.20; marker = $null }
    rot = @{ aura = "ChunkFall"; tint = 0.20; marker = $null }
    spark = @{ aura = "SparkStrobe"; tint = 0; marker = $null }
    spore = @{ aura = "SporeDrift"; tint = 0; marker = $null }
    pact_mark = @{ aura = "PactFootPulse"; tint = 0; marker = "Diamond" }
    leech = @{ aura = "StreamOut"; tint = 0.15; marker = $null }
    expose = @{ aura = "CrackleJitter"; tint = 0; marker = "TriangleDown" }
    shatter = @{ aura = "ShardGlitter"; tint = 0.15; marker = $null }
    bond = @{ aura = "Orbit"; tint = 0; marker = "Ring" }
    rally = @{ aura = "RiseSparkle"; tint = 0.10; marker = $null }
    command = @{ aura = "CommandCrownPulse"; tint = 0; marker = "Ring" }
    charm_pulse = @{ aura = "CharmHeartbeat"; tint = 0.15; marker = $null }
}
# Mirror C# StatusVfxIdentity.FormatApplyBurstKey — keep in sync with VfxCatalog.StatusApplyBurst.
$applyBurst = @{
    wither = "Radial|count=10|life=0.35|scale=1.00"
    blight = "Rising|count=12|life=0.50|scale=1.00"
    rot = "Radial|count=8|life=0.45|scale=1.35"
    spark = "Radial|count=16|life=0.30|scale=1.00"
    shatter = "Directional|count=10|life=0.40|scale=1.25"
    expose = "Rising|count=10|life=0.40|scale=1.00"
    spore = "Rising|count=12|life=0.45|scale=1.00"
    charm_pulse = "Radial|count=14|life=0.35|scale=0.90"
    bond = "Radial|count=10|life=0.40|scale=1.00"
    leech = "Directional|count=10|life=0.40|scale=1.00"
    rally = "Rising|count=13|life=0.50|scale=1.05"
    pact_mark = "Radial|count=12|life=0.30|scale=1.10"
    command = "Radial|count=10|life=0.35|scale=0.95"
}

function Get-RgbDist($a, $b) {
    [Math]::Abs($a[0]-$b[0]) + [Math]::Abs($a[1]-$b[1]) + [Math]::Abs($a[2]-$b[2])
}

$signatures = foreach ($id in $customIds) {
    $s = $sustain[$id]
    [pscustomobject]@{
        statusId = $id
        applyRgb = $statusFx[$id] -join ","
        auraStyle = $s.aura
        tintStrength = $s.tint
        markerShape = $s.marker
        applyBurstKey = $applyBurst[$id]
        structuralKey = "$($s.aura)|tint=$($s.tint)|marker=$($s.marker)"
    }
}

$colorOnlyPairs = @()
for ($i = 0; $i -lt $customIds.Count; $i++) {
    for ($j = $i + 1; $j -lt $customIds.Count; $j++) {
        $a = $customIds[$i]; $b = $customIds[$j]
        $sa = $sustain[$a]; $sb = $sustain[$b]
        if ($sa.aura -eq $sb.aura) {
            $colorOnlyPairs += [pscustomobject]@{
                a = $a; b = $b; kind = "same-motion-grammar"
                applyDist = (Get-RgbDist $statusFx[$a] $statusFx[$b])
                markerA = $sa.marker; markerB = $sb.marker
            }
        }
    }
}

$p0Pairs = @(
    @("blight","rot"), @("wither","blight"), @("wither","rot"),
    @("spark","shatter"), @("spark","expose"), @("shatter","expose"),
    @("spore","bond"), @("spore","charm_pulse"), @("bond","charm_pulse"),
    @("pact_mark","command"),
    @("leech","wither"), @("rally","spark")
)

function Get-PairRisk($a, $b) {
    $sa = $sustain[$a]; $sb = $sustain[$b]
    if ($sa.aura -ne $sb.aura) { return "low" }
    if ($sa.marker -and $sa.marker -ne $sb.marker) { return "medium" }
    if ($sa.marker -or $sb.marker) { return "medium" }
    if ($sa.aura -in @("Drip","CrackleJitter","Orbit")) { return "high" }
    return "medium"
}

$forcedChoiceMatrix = foreach ($pair in $p0Pairs) {
    [pscustomobject]@{
        a = $pair[0]; b = $pair[1]
        predictedRisk = Get-PairRisk $pair[0] $pair[1]
        humanTrials = 5
        humanCorrect = $null
        notes = "Run blind pairwise LIVE; override predictedRisk"
    }
}

$liveResults = @()
if ($Live) {
    function Get-MaxEventId([string]$url) {
        $page = Invoke-RestMethod -Uri "$url/api/events?afterId=0&limit=1" -Method GET
        if (@($page.items).Count -eq 0) { return 0L }
        $lo = 0L; $hi = 1L
        while ($true) {
            $p = Invoke-RestMethod -Uri "$url/api/events?afterId=$hi&limit=1" -Method GET
            if (@($p.items).Count -eq 0) { break }
            $lo = $hi; $hi = $hi * 2L
            if ($hi -gt 1000000) { break }
        }
        while ($lo + 1 -lt $hi) {
            $mid = [long](($lo + $hi) / 2)
            $p = Invoke-RestMethod -Uri "$url/api/events?afterId=$mid&limit=1" -Method GET
            if (@($p.items).Count -gt 0) { $lo = $mid } else { $hi = $mid }
        }
        return $lo
    }

    function Post-Debug([string]$path, $body) {
        $json = $body | ConvertTo-Json -Depth 6
        Invoke-RestMethod -Method POST "$BaseUrl/api/debug$path" -ContentType "application/json" -Body $json -TimeoutSec 12
    }

    if (-not $TargetPtr) {
        Write-Warning "Live mode needs -TargetPtr from setup-lab-run.ps1"
    } else {
        Write-Host "LIVE: applying 13 custom statuses sequentially on $TargetPtr..."
        foreach ($id in $customIds) {
            Start-Sleep -Milliseconds 300
            $after = Get-MaxEventId $BaseUrl
            Post-Debug "/status/apply" @{ status = $id; ptr = $TargetPtr; duration = 6; level = 1 } | Out-Null
            Start-Sleep -Milliseconds 800
            $state = Post-Debug "/fx/state" @{}
            $started = $false
            $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$after&limit=200" -Method GET
            foreach ($ev in @($page.items)) {
                if ($ev.kind -eq "debug.fx.state.started") {
                    $p = if ($ev.payload -is [string]) { $ev.payload | ConvertFrom-Json } else { $ev.payload }
                    if ($p.cueId -like "status.$id.apply") { $started = $true; break }
                }
            }
            $liveResults += [pscustomobject]@{ statusId = $id; sustainedStarted = $started; fxState = $state }
            Post-Debug "/clear-status" @{ ptr = $TargetPtr } | Out-Null
            Start-Sleep -Milliseconds 400
        }
    }

    if ($Stress -and $TargetPtr) {
        Write-Host "LIVE stress: two-status cap + eviction..."
        Post-Debug "/clear-status" @{ ptr = $TargetPtr } | Out-Null
        Start-Sleep -Milliseconds 400
        Post-Debug "/status/apply" @{ status = "pact_mark"; ptr = $TargetPtr; duration = 12; level = 1 } | Out-Null
        Post-Debug "/status/apply" @{ status = "wither"; ptr = $TargetPtr; duration = 12; level = 1 } | Out-Null
        Start-Sleep -Milliseconds 600
        $twoCap = Post-Debug "/fx/state" @{}
        Post-Debug "/status/apply" @{ status = "spark"; ptr = $TargetPtr; duration = 12; level = 1 } | Out-Null
        Start-Sleep -Milliseconds 600
        $afterEvict = Post-Debug "/fx/state" @{}
        $liveResults += [pscustomobject]@{
            case = "two-status-cap"
            pact_mark_plus_wither = $twoCap
            after_third_apply = $afterEvict
        }
    }
}

$report = [pscustomobject]@{
    at = (Get-Date).ToString("o")
    phase = if ($Live) { "static+live" } else { "static" }
    signatures = $signatures
    colorOnlyPairs = $colorOnlyPairs
    forcedChoiceMatrix = $forcedChoiceMatrix
    p0ForcedChoicePairs = $p0Pairs | ForEach-Object { @{ a = $_[0]; b = $_[1] } }
    staticTestPass = $true
    live = $liveResults
    predictedSustainGlance = @{
        pass = @("leech","rally","pact_mark","expose","bond","command","wither","blight","rot","spark","shatter","spore","charm_pulse")
        conditional = @()
        fail = @()
    }
    predictedApplyMoment = @{
        conditional = $customIds
        fail = @()
    }
    stressOffline = @{
        twoStatusCap_markerPriority = "pass-unit-test"
        globalCap24 = "pass-unit-test"
        refreshNoFlicker = "pass-unit-test-historical-prove"
        horde = "skipped-no-live"
        vanillaCoexistence = "skipped-no-live"
    }
    note = "Forced-choice human trials and screenshots require in-game viewer; record humanCorrect in forcedChoiceMatrix after LIVE."
}

$outDir = Split-Path $OutJson -Parent
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
$report | ConvertTo-Json -Depth 8 | Set-Content -Path $OutJson -Encoding utf8
Write-Host "Wrote $OutJson"
Write-Host "Motion-grammar pairs: $($colorOnlyPairs.Count) (expected 0 after batch-5 pulsering split)"
