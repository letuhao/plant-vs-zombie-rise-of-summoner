# Guard: commit-tool smoke + optional history range.
# Usage: .\scripts\guard-commit-policy.ps1
$ErrorActionPreference = "Stop"
$Root = git rev-parse --show-toplevel
Set-Location $Root
python scripts/commit-tool/smoke_test.py
if ($LASTEXITCODE -ne 0) { throw "commit-tool smoke failed" }
python scripts/commit-tool/check_history.py
if ($LASTEXITCODE -ne 0) { throw "commit-tool check_history failed" }
Write-Host "guard-commit-policy: ok"
