# Owner-only: live Actor HUD program E2E (injector + server required).
# See .claude/skills/live-lawn-quick-start/SKILL.md for cold-start details.
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [switch]$SkipServerCheck,
    [switch]$SkipDeploy
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

if (-not $SkipServerCheck) {
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET -TimeoutSec 5
        if (-not $health.injectorConnected) {
            Write-Host "Health OK but injectorConnected=false."
            Write-Host "Start server: Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe"
            Write-Host "Deploy injector: .\scripts\deploy-play.ps1 -NoServer"
            exit 1
        }
    }
    catch {
        Write-Host "Server not reachable at $BaseUrl — start FusionRpg.Server first."
        exit 1
    }
}

if (-not $SkipDeploy) {
    Write-Host "Tip: run .\scripts\deploy-play.ps1 -NoServer if injector DLLs are stale."
}

Set-Location (Join-Path $repoRoot "web\fusion-rpg-web")
$env:ACTOR_HUD_LIVE_E2E = "1"
$env:FUSIONRPG_API_BASE = $BaseUrl

Write-Host "Running live Playwright (vite dev :5173 → API $BaseUrl)..."
npm run test:e2e:live
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Live Actor HUD E2E passed."
Write-Host "Unity LIVE eyeball still required — see tasks/actor-hud-todo.md P6 Unity manual."
