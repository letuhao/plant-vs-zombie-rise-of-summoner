# Guard: numeric-overflow audit (power-plan.md P0.6) — fails the build on a CRITICAL finding
# (A1/A2/A4: float or int-per-mille on a magnitude, or a cast-after-multiply). A3 (int whole-unit
# magnitude) and A7 (double in the shipped stat architecture) are HIGH/REVIEW and non-blocking —
# they are the triaged, currently-BOUNDED sites (docs/architecture/power/overflow-triage.md), not
# unowned defects; a new BOUNDED site is a review question, not an automatic red build.
# Usage (repo root): .\scripts\guard-overflow.ps1
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
Push-Location $Root
try {
    python scripts/audit-overflow.py
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    Write-Host "OVERFLOW GUARD FAILED — critical finding(s) above (float/int-per-mille magnitude or cast-after-multiply)" -ForegroundColor Red
    exit 1
}

Write-Host "OVERFLOW GUARD OK — no critical findings"
exit 0
