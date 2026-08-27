# Gate: Phase 9 readiness, asserted not judged (class-system-todo.md P9.0)
# Usage (repo root): .\scripts\gate-class-system-phase9.ps1
#
# class-system-plan.md §0.1: "A human cannot help... every number in this system is decided by
# measurement." Phase 9 (tuning on real data) cannot start until every mechanism the aptitude
# distribution funds actually exists (map §5) -- until then, some fraction of the distribution points
# at channels nothing reads, and fitting over those specific channels would freeze noise rather than
# find balance. Nobody gets to decide "we are ready" by eye. This script is the mechanical assertion:
# it wraps scripts/audit-reader-census.py (P8.4) and reports READY only when that census -- not a
# person -- says every aptitude-fed family has a reader, and _meta.measurable's own prose still agrees
# with a fresh run (not stale).
#
# Correct, expected behavior TODAY is exit 1 -- this is a readiness report, not a code-defect guard,
# so a "NOT READY" verdict is not a failure of this program's own work; it is this script doing its job.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    # Test seam: when set, skip invoking python entirely and evaluate readiness against this JSON file
    # (the same shape scripts/audit-reader-census.py --json emits) instead. Lets Guard.Tests prove both
    # the READY and NOT-READY branches of the readiness arithmetic without needing a real, fully-built
    # game to exist, and without touching audit-reader-census.py's own hardcoded, already-shipped,
    # already-tested paths (that script has no -Root override; extending it is out of this task's scope
    # -- see class-system-todo.md P9.0 evidence).
    [string]$CensusJsonPath = $null
)

$ErrorActionPreference = "Stop"
$failures = @()

if ($CensusJsonPath) {
    if (-not (Test-Path $CensusJsonPath)) { throw "CensusJsonPath not found: $CensusJsonPath" }
    $census = Get-Content -LiteralPath $CensusJsonPath -Raw | ConvertFrom-Json
} else {
    $censusScript = Join-Path $Root "scripts\audit-reader-census.py"
    if (-not (Test-Path $censusScript)) { throw "audit-reader-census.py missing: $censusScript" }

    # _meta.measurable is prose (not a structured per-coefficient flag) -- --check is the mechanical
    # proxy for "true for every coefficient the fit will touch": it fails if the prose's own claimed
    # numbers no longer match a fresh census, i.e. if the file is lying about what it thinks is measurable.
    $checkOutput = & python $censusScript --check 2>&1
    if ($LASTEXITCODE -ne 0) {
        $failures += "P9.0: _meta.measurable is STALE relative to a fresh reader census -- fix the prose before readiness can even be evaluated:`n$($checkOutput -join "`n")"
    }

    $jsonOutput = & python $censusScript --json 2>&1
    if ($LASTEXITCODE -ne 0) { throw "audit-reader-census.py --json failed:`n$($jsonOutput -join [Environment]::NewLine)" }
    $census = ($jsonOutput -join [Environment]::NewLine) | ConvertFrom-Json
}

if ($census.families_without_reader -gt 0) {
    $failures += ("P9.0: {0} of {1} aptitude-fed families still have no reader ({2} of {3} edges, {4}%) -- not ready: {5}" -f `
        $census.families_without_reader, $census.families_total, $census.edges_reserved, $census.edges_total, `
        $census.edges_reserved_pct, ($census.reader_less_families -join ", "))
}

if ($failures.Count -gt 0) {
    Write-Host "PHASE 9 READINESS GATE: NOT READY" -ForegroundColor Yellow
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "PHASE 9 READINESS GATE: READY -- every aptitude-fed family has a reader, _meta.measurable agrees with a fresh census"
exit 0
