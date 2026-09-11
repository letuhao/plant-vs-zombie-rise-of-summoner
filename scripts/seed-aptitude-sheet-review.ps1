#Requires -Version 5.1
<#
.SYNOPSIS
  Seed UniqueDemon + commander allocation + one Even preset for aptitude-sheet UI review.

.DESCRIPTION
  Uses existing debug/product APIs (no new endpoints):
    POST /api/debug/derived-audit-actor   — UniqueActor "derived-audit" @ L80 + broad aptitudes
    GET  /api/players                    — current playerId
    POST /api/aptitude-presets/          — Even ‰=1000 library row
    GET  /api/aptitudes/unique/{id}      — print budget/spent for smoke

  Open after seed (hard-refresh):
    Mode A  http://127.0.0.1:5088/#/actor-ladder-demo?sel=derived-audit
            → Open panel → Aptitudes → Build presets…
    Mode C  Sanctum → Commanders → open sheet → Aptitudes
    Mode B  Sanctum → Pacts → View build (species)

.PARAMETER BaseUrl
  Server base (default http://127.0.0.1:5088).
#>
param(
    [string]$BaseUrl = "http://127.0.0.1:5088"
)

$ErrorActionPreference = "Stop"

function Invoke-Json {
    param([string]$Method, [string]$Path, [object]$Body = $null)
    $uri = "$BaseUrl$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $uri
    }
    $json = $Body | ConvertTo-Json -Depth 12 -Compress
    return Invoke-RestMethod -Method $Method -Uri $uri -ContentType "application/json" -Body $json
}

Write-Host "=== aptitude-sheet review seed ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl"

$health = Invoke-Json GET "/health"
Write-Host ("health ok={0} playerId={1} injector={2}" -f $health.ok, $health.currentPlayerId, $health.injectorConnected)

$seed = Invoke-Json POST "/api/debug/derived-audit-actor" @{ playerId = $health.currentPlayerId }
$instanceId = if ($seed.instanceId) { $seed.instanceId } else { "derived-audit" }
Write-Host ("seeded UniqueActor instanceId={0}" -f $instanceId)

$players = Invoke-Json GET "/api/players"
$playerId = $players.currentPlayerId
Write-Host ("currentPlayerId={0}" -f $playerId)

$ids = @(
    "Might", "Fortitude", "Vigor", "Onslaught",
    "Agility", "Composure", "Pierce", "Focus",
    "Bulwark", "Retribution", "Precision", "Ferocity"
)
$rows = @()
for ($i = 0; $i -lt $ids.Count; $i++) {
    $pm = 83 + $(if ($i -lt 4) { 1 } else { 0 })
    $rows += @{ aptitudeId = $ids[$i]; targetPermille = $pm }
}

$preset = Invoke-Json POST "/api/aptitude-presets" @{
    playerId = $playerId
    name     = "Review Even"
    kind     = "player"
    rows     = $rows
}
Write-Host ("preset saved presetId={0} name={1}" -f $preset.presetId, $preset.name)

$unique = Invoke-Json GET "/api/aptitudes/unique/$instanceId"
Write-Host ("unique budget={0} spent={1} leftover={2}" -f $unique.budget, $unique.spent, $unique.leftover)

$commander = Invoke-Json GET "/api/aptitudes/$playerId"
Write-Host ("commander budget={0} spent={1}" -f $commander.budget, $commander.spent)

Write-Host ""
Write-Host "Review URLs (hard-refresh after FE deploy):" -ForegroundColor Green
Write-Host "  Mode A  $BaseUrl/#/actor-ladder-demo?sel=$instanceId"
Write-Host "          Open panel → Aptitudes → Build presets…"
Write-Host "  Mode C  $BaseUrl/  → Sanctum → Commanders → sheet → Aptitudes"
Write-Host "  Mode B  $BaseUrl/  → Sanctum → Pacts → View build"
Write-Host ""
Write-Host "Obs ring in DevTools:" -ForegroundColor Green
Write-Host '  window.__fusionRpgAptitudeObs'
Write-Host ""
Write-Host "FE rebuild into wwwroot if UI looks stale:" -ForegroundColor Yellow
Write-Host "  cd web\fusion-rpg-web; npm run build"
Write-Host "  # or: .\scripts\deploy-play.ps1 -NoServer -NoGame"
