# Polls REAL ground truth (server /health + the game process) with a bounded timeout, instead of
# trusting a background shell tool's own "is this task done yet" status -- that status has been
# observed lagging behind real completion (2026-09-14: a deploy-play.ps1 run had already launched a
# healthy, connected game 4+ minutes before the task runner still reported it "running"). Never poll
# this in an unbounded loop; it always terminates, successfully or with a clear timeout message.
#
# Usage:
#   .\scripts\wait-for-deploy.ps1                # default: 300s timeout, 5s interval, requires
#                                                 # both health and a running game process
#   .\scripts\wait-for-deploy.ps1 -TimeoutSec 120 -IntervalSec 3
#   .\scripts\wait-for-deploy.ps1 -NoGame         # server-only restart (Procedure A) -- don't wait
#                                                 # for a game process to exist
param(
    [int]$TimeoutSec = 300,
    [int]$IntervalSec = 5,
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [switch]$NoGame
)

$start = Get-Date
$deadline = $start.AddSeconds($TimeoutSec)
$attempt = 0
while ((Get-Date) -lt $deadline) {
    $attempt++
    $health = $null
    try { $health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5 -ErrorAction Stop } catch { }

    $gameUp = $NoGame -or [bool](Get-Process -Name PlantsVsZombiesRH -ErrorAction SilentlyContinue)

    if ($health -and $health.ok -and $health.injectorConnected -and $gameUp) {
        Write-Host "==> Deploy confirmed live after $attempt check(s) (~$([math]::Round(((Get-Date) - $start).TotalSeconds))s):"
        $health | Format-List | Out-String | Write-Host
        exit 0
    }

    $reason = if (-not $health) { "server not answering" }
        elseif (-not $health.ok) { "health.ok=false" }
        elseif (-not $health.injectorConnected) { "injectorConnected=false" }
        else { "game process not found" }
    Write-Host "==> [$attempt] not ready yet ($reason) -- retrying in ${IntervalSec}s"
    Start-Sleep -Seconds $IntervalSec
}

Write-Host "==> TIMEOUT after ${TimeoutSec}s -- deploy did not reach a confirmed-live state."
Write-Host "    Check the deploy-play.ps1 output directly rather than assuming it is still running."
exit 1
