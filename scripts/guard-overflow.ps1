# Guard: integer-range overflow audit (power-plan.md P0.6) — fails the build on a CRITICAL finding
# (A2/A4: int-per-mille on a magnitude, or a cast-after-multiply). A3 (int whole-unit magnitude) is
# HIGH and non-blocking — the triaged, currently-BOUNDED sites (docs/architecture/power/overflow-triage.md),
# not unowned defects; a new BOUNDED site is a review question, not an automatic red build.
# Floating point is not audited (owner ruling 2026-09-15 removed the floating-point ban; the former
# A1 float-magnitude and A7 double-magnitude rules are gone).
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
    Write-Host "OVERFLOW GUARD FAILED — critical finding(s) above (int-per-mille magnitude or cast-after-multiply)" -ForegroundColor Red
    exit 1
}

Write-Host "OVERFLOW GUARD OK — no critical findings"
exit 0
