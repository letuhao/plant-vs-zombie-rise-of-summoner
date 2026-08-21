# Tight shield-bar HUD probe — world VFX bars, not IMGUI.
#
# Prerequisites: Melon/Bep injector connected + Adventure lawn live.
# Optional: already ran setup-shield-bar-lab.ps1 (or pass -Setup).
#
# Pass criteria:
#   dataOwners > 0
#   shaderOk = true
#   worldBars == dataOwners (and lastDraw.early = ok)
#   fillRatio > 0
#
# Usage:
#   .\scripts\probe-live-shield-bar.ps1
#   .\scripts\probe-live-shield-bar.ps1 -Setup
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [switch]$Setup,
    [int]$WaitDrawSec = 8
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

function Get-MaxEventId {
    function Has-After([long]$id) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$id&limit=1"
        return @($page.items).Count -gt 0
    }
    if (-not (Has-After 0)) { return 0L }
    $lo = 0L; $hi = 1L
    while (Has-After $hi) {
        $lo = $hi
        if ($hi -gt [long]::MaxValue / 2) { break }
        $hi = $hi * 2L
    }
    while ($lo + 1L -lt $hi) {
        $mid = [long](($lo + $hi) / 2L)
        if (Has-After $mid) { $lo = $mid } else { $hi = $mid }
    }
    return $lo
}

function Wait-Kind([long]$afterId, [string]$kind, [int]$timeoutSec) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $cursor = $afterId
    while ((Get-Date) -lt $deadline) {
        $page = Invoke-RestMethod -Uri "$BaseUrl/api/events?afterId=$cursor&limit=100"
        $items = @($page.items)
        $hit = @($items | Where-Object { $_.kind -eq $kind }) | Select-Object -Last 1
        if ($hit) { return $hit }
        if ($items.Count -gt 0) { $cursor = [long]$items[-1].id }
        Start-Sleep -Milliseconds 200
    }
    return $null
}

function Get-Payload($ev) {
    if ($null -eq $ev) { return $null }
    $p = $ev.payload
    if ($null -eq $p) { return $null }
    if ($p -is [string]) { try { return $p | ConvertFrom-Json } catch { return $null } }
    return $p
}

Write-Host "== health =="
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5
if (-not $health.ok) { throw "server health.ok=false" }
if (-not $health.injectorConnected) {
    throw "injector not connected — start Melon game on an Adventure lawn"
}
Write-Host ("  injectorConnected={0}" -f $health.injectorConnected)

if ($Setup) {
    Write-Host "== setup lab-shield-bar =="
    & "$PSScriptRoot\setup-shield-bar-lab.ps1" -BaseUrl $BaseUrl
}

Write-Host "== ensure shields (demo-all) =="
$after = Get-MaxEventId
Invoke-RestMethod -Method POST "$BaseUrl/api/debug/shield/demo-all" `
    -ContentType "application/json" -Body '{"amount":100}' -TimeoutSec 15 | Out-Null
$demo = Wait-Kind -afterId $after -kind "debug.shield.demo-all" -timeoutSec 15
if (-not $demo) { throw "no debug.shield.demo-all" }
$demoP = Get-Payload $demo
Write-Host ("  targets={0}" -f $demoP.targetCount)

Write-Host "== wait world VFX bars (poll bar-status) =="
$best = $null
$deadline = (Get-Date).AddSeconds($WaitDrawSec)
while ((Get-Date) -lt $deadline) {
    $after = Get-MaxEventId
    Invoke-RestMethod -Method POST "$BaseUrl/api/debug/shield/bar-status" `
        -ContentType "application/json" -Body '{}' -TimeoutSec 10 | Out-Null
    $ev = Wait-Kind -afterId $after -kind "debug.shield.bar-status" -timeoutSec 3
    $p = Get-Payload $ev
    if ($p) {
        $best = $p
        Write-Host ("  data={0} worldBars={1} shaderOk={2} fillRatio={3} early={4}" -f `
            $p.dataOwners, $p.worldBars, $p.shaderOk, $p.fillRatio, $p.lastDraw.early)
        if ([bool]$p.shaderOk -and [int]$p.worldBars -gt 0 -and [int]$p.worldBars -eq [int]$p.dataOwners) {
            break
        }
    }
    Start-Sleep -Milliseconds 400
}

if (-not $best) { throw "no debug.shield.bar-status — is injector build current?" }

$dataOk = [int]$best.dataOwners -gt 0
$shaderOk = [bool]$best.shaderOk
$barsOk = ([int]$best.worldBars -gt 0) -and ([int]$best.worldBars -eq [int]$best.dataOwners)
$ratioOk = [double]$best.fillRatio -gt 0
$earlyOk = [string]$best.lastDraw.early -eq "ok"

Write-Host ""
Write-Host "== verdict =="
Write-Host ("  [{0}] injector has shields (dataOwners={1})" -f ($(if ($dataOk) { "OK" } else { "FAIL" }), $best.dataOwners))
Write-Host ("  [{0}] OverlayShaderProbe material ok (shaderOk={1})" -f ($(if ($shaderOk) { "OK" } else { "FAIL" }), $best.shaderOk))
Write-Host ("  [{0}] world VFX bars live (worldBars={1})" -f ($(if ($barsOk) { "OK" } else { "FAIL" }), $best.worldBars))
Write-Host ("  [{0}] fill length from capacity (fillRatio={1} early={2})" -f `
    ($(if ($ratioOk -and $earlyOk) { "OK" } else { "FAIL" }), $best.fillRatio, $best.lastDraw.early))

if (-not ($dataOk -and $shaderOk -and $barsOk -and $ratioOk -and $earlyOk)) {
    Write-Host ""
    Write-Host "Look under pea/zombie for shader bars (not top-left GUI)."
    Write-Host "early=no-shader → OverlayShaderProbe failed (no GUI fallback)."
    Write-Host "early=no-body → shields exist but AnchorResolver miss."
    exit 1
}

Write-Host ""
Write-Host "PASS — world shield bars should be under shielded units (length = capacity)."
exit 0
