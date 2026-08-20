# Perf baseline probe — perf-probe-plan.md Phase B.
# Collects PerfProbe windows from /api/perf/recent over a scenario run and writes
# docs/research/perf/_baseline-<Scenario>.json plus a console summary.
#
# Usage (start it, then play the scenario until it finishes):
#   .\scripts\probe-perf.ps1 -Scenario b2-heavy-normal -DurationSec 60
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [Parameter(Mandatory = $true)][string]$Scenario,
    [int]$DurationSec = 60
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

$outDir = Join-Path $PSScriptRoot "..\docs\research\perf"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }
$outFile = Join-Path $outDir "_baseline-$Scenario.json"

# Mark the probe window start: remember timestamps already present.
$before = Invoke-RestMethod -Uri "$BaseUrl/api/perf/recent?limit=240" -Method GET
$seen = @{}
foreach ($w in @($before.items)) { $seen[[string]$w.t] = $true }

Write-Host "[probe-perf] scenario=$Scenario collecting for ${DurationSec}s — play now..."
Start-Sleep -Seconds $DurationSec

$after = Invoke-RestMethod -Uri "$BaseUrl/api/perf/recent?limit=240" -Method GET
$windows = @($after.items) | Where-Object { -not $seen.ContainsKey([string]$_.t) }

if ($windows.Count -eq 0) {
    Write-Warning "No new perf windows arrived. Is the game running with the injector connected?"
    exit 1
}

$doc = [ordered]@{
    scenario    = $Scenario
    baseUrl     = $BaseUrl
    durationSec = $DurationSec
    capturedUtc = (Get-Date).ToUniversalTime().ToString("o")
    windows     = $windows
}
$doc | ConvertTo-Json -Depth 12 | Set-Content -Path $outFile -Encoding UTF8
Write-Host "[probe-perf] wrote $($windows.Count) windows -> $outFile"

# Summary: averages across windows.
function Avg($values) { if ($values.Count) { [math]::Round(($values | Measure-Object -Average).Average, 2) } else { 0 } }

$fps      = Avg @($windows | ForEach-Object { $_.frames.fpsAvg })
$frameMax = ($windows | ForEach-Object { $_.frames.maxMs } | Measure-Object -Maximum).Maximum
$allocKb  = Avg @($windows | ForEach-Object { $_.gc.allocKb })
$gen2     = ($windows | ForEach-Object { $_.gc.gen2 } | Measure-Object -Sum).Sum

Write-Host ""
Write-Host ("{0,-22} {1,10} {2,10} {3,12} {4,10}" -f "summary", "fpsAvg", "frameMax", "allocKb/5s", "gen2")
Write-Host ("{0,-22} {1,10} {2,10} {3,12} {4,10}" -f $Scenario, $fps, $frameMax, $allocKb, $gen2)
Write-Host ""
Write-Host ("{0,-22} {1,10} {2,12} {3,10} {4,10}" -f "section", "calls/s", "totalMs/5s", "avgUs", "maxMs")

$sectionNames = @("loop.tick", "board.capture", "stats.resolve", "hub.resolveDerived",
    "effect.onCapture", "effect.tickDots", "takeDamage.prefix", "fx.show", "grants.scan",
    "entity.apply", "match.apply", "effect.onEvent", "drain.tick",
    "vfx.tick", "cheat.continuous", "cheat.autocollect", "poll.board", "pump.main",
    "combat.dispatch", "funnel.flush")
foreach ($name in $sectionNames) {
    $rows = @($windows | ForEach-Object { $_.sections.$name } | Where-Object { $_ })
    if (-not $rows.Count) { continue }
    Write-Host ("{0,-22} {1,10} {2,12} {3,10} {4,10}" -f $name,
        (Avg @($rows | ForEach-Object { $_.perSec })),
        (Avg @($rows | ForEach-Object { $_.totalMs })),
        (Avg @($rows | ForEach-Object { $_.avgUs })),
        (($rows | ForEach-Object { $_.maxMs } | Measure-Object -Maximum).Maximum))
}

$emits = @{}
foreach ($w in $windows) {
    if ($null -eq $w.emits) { continue }
    foreach ($p in $w.emits.PSObject.Properties) {
        $emits[$p.Name] = [double]($emits[$p.Name]) + [double]$p.Value
    }
}
if ($emits.Count) {
    Write-Host ""
    Write-Host "emits (total over run): " -NoNewline
    Write-Host (($emits.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "  ")
}
