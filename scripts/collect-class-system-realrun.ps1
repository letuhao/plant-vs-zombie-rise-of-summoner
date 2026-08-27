# class-system-todo.md P9.1 -- collect real-run metrics into a durable, source-tagged, drop-rate-aware
# store. decisions.md "Class system real-data collection" (2026-08-27): a file-based JSONL log, read
# only from the already-public GET /api/perf/recent -- no change to PerfProbe/PerfReporter/
# PerfWindowBuffer. Sibling to scripts/probe-perf.ps1 (perf program's own one-shot baseline capture);
# this one is class-system-owned, runs continuously across a play session, and is multi-run (one file
# per RunId) rather than one fixed scenario per file.
#
# Usage (start it, then play):
#   .\scripts\collect-class-system-realrun.ps1 -DurationSec 300
#
# Output: docs/research/class-system/real-runs/<RunId>.jsonl (one JSON line per captured window,
# {runId, t, window}) and docs/research/class-system/real-runs/<RunId>.summary.json (windowsCaptured,
# expectedWindows, estimatedDropped, dropRatePct -- the drop-rate metric P9.1's own acceptance line
# requires, computed from gaps in each window's own "t" field against the known emit cadence, never
# silently assumed zero).
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [int]$DurationSec = 300,
    [double]$PollIntervalSec = 4.0,
    [string]$RunId = [guid]::NewGuid().ToString("N"),
    # Structural (tunables-ssot.md T2), not a balance dial -- the injector's own PerfReporter emit
    # cadence (data/tuning/net.v1.json's perfReporter.intervalSeconds), used only to ESTIMATE drops,
    # never to alter what's collected. Overridable for a test harness whose synthetic emitter uses a
    # different cadence than the real injector's.
    [double]$ExpectedIntervalSec = 5.0
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

$outDir = Join-Path $PSScriptRoot "..\docs\research\class-system\real-runs"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Force $outDir | Out-Null }
$jsonlPath = Join-Path $outDir "$RunId.jsonl"
$summaryPath = Join-Path $outDir "$RunId.summary.json"

$seen = @{}
$capturedTimestamps = New-Object System.Collections.Generic.List[datetime]
$startedUtc = (Get-Date).ToUniversalTime()

Write-Host "[collect-realrun] runId=$RunId collecting for ${DurationSec}s (poll every ${PollIntervalSec}s) -> $jsonlPath"

$deadline = (Get-Date).AddSeconds($DurationSec)
while ((Get-Date) -lt $deadline) {
    try {
        $resp = Invoke-RestMethod -Uri "$BaseUrl/api/perf/recent?limit=240" -Method GET -TimeoutSec 10
        foreach ($w in @($resp.items)) {
            $t = [string]$w.t
            if ([string]::IsNullOrEmpty($t) -or $seen.ContainsKey($t)) { continue }
            $seen[$t] = $true
            $capturedTimestamps.Add([datetime]$t)
            $envelope = [ordered]@{ runId = $RunId; t = $t; window = $w }
            ($envelope | ConvertTo-Json -Depth 12 -Compress) | Add-Content -LiteralPath $jsonlPath -Encoding UTF8
        }
    } catch {
        Write-Warning "[collect-realrun] poll failed: $($_.Exception.Message)"
    }
    $remaining = ($deadline - (Get-Date)).TotalSeconds
    if ($remaining -le 0) { break }
    Start-Sleep -Seconds ([math]::Min($PollIntervalSec, [math]::Max(0.1, $remaining)))
}

$endedUtc = (Get-Date).ToUniversalTime()

# Drop-rate estimate: how many ~ExpectedIntervalSec-spaced windows SHOULD have arrived across the
# actual captured span (first-to-last "t"), vs how many actually did -- never silently assumed zero,
# never silently omitted. A run with 0 or 1 windows has no interior span to estimate drops over; report
# that honestly (expectedWindows = windowsCaptured, estimatedDropped = 0) rather than divide-by-zero.
$windowsCaptured = $capturedTimestamps.Count
$expectedWindows = $windowsCaptured
$estimatedDropped = 0
$dropRatePct = 0.0
if ($windowsCaptured -ge 2) {
    $sorted = $capturedTimestamps | Sort-Object
    $spanSec = ($sorted[-1] - $sorted[0]).TotalSeconds
    $expectedWindows = [int][math]::Round($spanSec / $ExpectedIntervalSec) + 1
    $estimatedDropped = [math]::Max(0, $expectedWindows - $windowsCaptured)
    if ($expectedWindows -gt 0) { $dropRatePct = [math]::Round(100.0 * $estimatedDropped / $expectedWindows, 2) }
}

$summary = [ordered]@{
    runId            = $RunId
    baseUrl          = $BaseUrl
    startedUtc       = $startedUtc.ToString("o")
    endedUtc         = $endedUtc.ToString("o")
    durationSec      = $DurationSec
    windowsCaptured  = $windowsCaptured
    expectedWindows  = $expectedWindows
    estimatedDropped = $estimatedDropped
    dropRatePct      = $dropRatePct
}
($summary | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($windowsCaptured -eq 0) {
    Write-Warning "[collect-realrun] no perf windows arrived. Is the game running with the injector connected?"
    exit 1
}

Write-Host "[collect-realrun] wrote $windowsCaptured window(s) -> $jsonlPath"
Write-Host "[collect-realrun] estimated $estimatedDropped dropped of $expectedWindows expected (${dropRatePct}%) -> $summaryPath"
exit 0
