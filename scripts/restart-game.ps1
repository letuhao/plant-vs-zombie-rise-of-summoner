<#
.SYNOPSIS
  Close and relaunch the game process, then poll real ground truth until the injector reconnects.

.DESCRIPTION
  The easiest, most reliable recovery for a stuck/defeated board: close PlantsVsZombiesRH.exe and
  start it fresh. Real problem this replaces (2026-09-14): in-place recovery via debug.ui-nav
  (UIMgr.BackToMenu / EnterMainMenu / enter-level) works for menu navigation, but proving a genuinely
  fresh SCENE (not a stale Board/InitBoard reference reused across the old and new "run") turned out
  to need real engine-level verification this tooling doesn't have yet -- deferred, not abandoned
  (see docs/architecture/live-probe/lawn-run-state-machine.md). A full process relaunch sidesteps the
  question entirely: a new process cannot have a stale reference from the old one.

  Does NOT rebuild the injector or server -- this only restarts the GAME. Use deploy-play.ps1 first
  if injector code changed.

  Polls the same real ground truth as wait-for-deploy.ps1 (server /health + the game process actually
  existing) rather than trusting a fixed sleep -- never assume "the game is probably up by now".

.PARAMETER TimeoutSec
  Max seconds to wait for the relaunched game's injector to reconnect. Default 120.

.PARAMETER IntervalSec
  Seconds between polls. Default 3.

.PARAMETER GameDir
  Override the game install dir. Defaults to $env:FUSIONRPG_ML_GAMEDIR or the MelonLoader default
  (H:\Games\PVZ-Fusion-3.9_MelonLoader), matching deploy-play.ps1's own default.

.PARAMETER BaseUrl
  Server base URL to poll for health/injectorConnected. Default http://127.0.0.1:5088.

.EXAMPLE
  .\scripts\restart-game.ps1
#>
param(
    [int]$TimeoutSec = 120,
    [int]$IntervalSec = 3,
    [string]$GameDir,
    [string]$BaseUrl = "http://127.0.0.1:5088"
)

$ErrorActionPreference = "Stop"

if (-not $GameDir) {
    $GameDir = if ($env:FUSIONRPG_ML_GAMEDIR) { $env:FUSIONRPG_ML_GAMEDIR } else { "H:\Games\PVZ-Fusion-3.9_MelonLoader" }
}
$GameDir = (Resolve-Path $GameDir).Path
$GameExe = Join-Path $GameDir "PlantsVsZombiesRH.exe"
if (-not (Test-Path $GameExe)) { throw "game exe not found: $GameExe" }

# Real ground truth for "is this the same injector session as before" -- a fresh process reconnecting
# always mints a new injector.hello, so recording the pre-restart heartbeat lets the poll below
# distinguish a genuinely new connection from the old one just still being up during shutdown.
$before = $null
try { $before = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5 -ErrorAction Stop } catch { }
$beforeHeartbeat = if ($before) { $before.lastHeartbeatUtc } else { $null }

# Write-Host through a redirected/piped stdout can appear fully buffered until the process exits on
# this host (real incident 2026-09-14: the operator saw no output at all and assumed the loop had
# hung, when the relaunch had actually already succeeded) -- write straight to Console and flush
# after every line so progress is visible immediately even when piped.
function Say([string]$msg) {
    [Console]::Out.WriteLine($msg)
    [Console]::Out.Flush()
}

$running = Get-Process -Name "PlantsVsZombiesRH" -ErrorAction SilentlyContinue
if ($running) {
    Say "==> Closing PlantsVsZombiesRH (pid $($running.Id -join ', '))"
    $running | Stop-Process -Force
    Start-Sleep -Seconds 2
} else {
    Say "==> Game not currently running"
}

Say "==> Launching $GameExe"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $GameExe
$psi.WorkingDirectory = $GameDir
$psi.UseShellExecute = $false
$psi.Environment["FUSIONRPG_SERVER_URL"] = $BaseUrl
[System.Diagnostics.Process]::Start($psi) | Out-Null

$start = Get-Date
$deadline = $start.AddSeconds($TimeoutSec)
$attempt = 0
while ((Get-Date) -lt $deadline) {
    $attempt++
    $health = $null
    try { $health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5 -ErrorAction Stop } catch { }
    $gameUp = [bool](Get-Process -Name "PlantsVsZombiesRH" -ErrorAction SilentlyContinue)
    $isFreshHeartbeat = $health -and $health.lastHeartbeatUtc -and ($health.lastHeartbeatUtc -ne $beforeHeartbeat)

    if ($health -and $health.ok -and $health.injectorConnected -and $gameUp -and $isFreshHeartbeat) {
        Say "==> Game relaunched and injector reconnected after $attempt check(s) (~$([math]::Round(((Get-Date) - $start).TotalSeconds))s):"
        Say ($health | Format-List | Out-String)
        exit 0
    }

    $reason = if (-not $gameUp) { "game process not found" }
        elseif (-not $health) { "server not answering" }
        elseif (-not $health.ok) { "health.ok=false" }
        elseif (-not $health.injectorConnected) { "injectorConnected=false" }
        elseif (-not $isFreshHeartbeat) { "heartbeat still matches pre-restart snapshot" }
        else { "not ready" }
    Say "==> [$attempt] not ready yet ($reason) -- retrying in ${IntervalSec}s"
    Start-Sleep -Seconds $IntervalSec
}

Say "==> TIMEOUT after ${TimeoutSec}s -- game did not reach a confirmed fresh-connected state."
exit 1
