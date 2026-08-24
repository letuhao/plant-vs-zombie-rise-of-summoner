# Guard: magic-number audit (power-todo.md M.6) — fails the build on an M1/M2 finding (a bare
# literal in a balance-surface file, or a const with balance vocabulary not in config). M3/M4 are
# MEDIUM/LOW and non-blocking — style gaps (missing comment, missing unit suffix), not the T1
# "balance surface must be config" rule this guard exists to enforce.
# Usage (repo root): .\scripts\guard-magic-numbers.ps1
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
Push-Location $Root
try {
    python scripts/audit-magic-numbers.py
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    Write-Host "MAGIC-NUMBER GUARD FAILED — M1/M2 finding(s) above (bare literal or unconfigured balance const)" -ForegroundColor Red
    exit 1
}

Write-Host "MAGIC-NUMBER GUARD OK — no M1/M2 findings"
exit 0
