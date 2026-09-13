# lawn-combat-observer (Task 0, lawn-combat-wire "Phase 0 — the ruler") — watches a real running
# FusionRpg.Server (+ live game/Injector, for real hit data) over real HTTP, for -DurationSec seconds,
# and writes a machine-readable run file a gate can diff against another run. Drives
# tools/LawnCombatObserver, a small console tool that opens a real HttpClient against -BaseUrl
# (default http://127.0.0.1:5088).
#
# Server up (any board state) is enough to see the non-perturbation proof and an honest "no data" /
# "zero hits" read; a LIVE board with real combat happening is what is needed to see real vanilla hits
# and (once basic-attack-grant/T10 lands) a real RPG delta.
#
# Usage (repo root):
#   .\scripts\lawn-combat-observer.ps1 -DurationSec 60 -Out docs/research/perf/_lawn-combat-observer.json
#   .\scripts\lawn-combat-observer.ps1 -BaseUrl http://127.0.0.1:5088 -PollIntervalSec 2
#
# Every named flag just forwards through to `dotnet run --` as-is. Exits 0 on a run that collected real
# data (hits may legitimately be zero — see the run file's own `noData` vs `totalHits` fields),
# non-zero when the server could not be reached at all or no /api/perf window ever landed.
#
# Same "build once, never silently retry" shape as prove-live-probe.ps1: this tool's own run has real
# side effects only in the sense that it spends wall-clock time watching a live process — there is
# nothing here to retry blindly on a nonzero exit, since a "no data" result is itself the finding.
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RestArgs
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$toolDir = Join-Path $repoRoot "tools\LawnCombatObserver"
$builtDll = Join-Path $toolDir "bin\Debug\net8.0\LawnCombatObserver.dll"

if (-not (Test-Path $builtDll)) {
    & dotnet build $toolDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Push-Location $toolDir
try {
    & dotnet run --no-build -- @RestArgs
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

exit $exitCode
