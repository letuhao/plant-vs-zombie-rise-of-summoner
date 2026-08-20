# Architecture stress test — perf runbook / event-pipeline-v2 Task 12.
# With a lawn open: freezes waves, mass-spawns plants + zombies via POST /api/debug/stress-fill,
# waits for the board to settle, then captures a probe window set and prints the verdict.
#
#   .\scripts\stress-test.ps1                          # 40 plants / 150 zombies, 90s
#   .\scripts\stress-test.ps1 -Zombies 400 -DurationSec 120
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [int]$Plants = 40,
    [int]$Zombies = 150,
    [int]$PlantType = 0,
    [int]$ZombieType = 0,
    [int]$DurationSec = 90,
    [string]$Scenario = ""
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
if (-not $Scenario) { $Scenario = "stress-$($Plants)p-$($Zombies)z" }

$h = Invoke-RestMethod "$BaseUrl/health"
if (-not $h.injectorConnected) { Write-Error "injector not connected — is the game running on a lawn?"; exit 1 }

Write-Host "[stress] freezing waves + filling board: $Plants plants ($PlantType), $Zombies zombies ($ZombieType)..."
$body = @{ plants = $Plants; zombies = $Zombies; plantType = $PlantType; zombieType = $ZombieType; freeze = $true } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/debug/stress-fill" -ContentType "application/json" -Body $body | Out-Null

# The fill runs on the game's next command drain; wait for the census to reflect it.
$settled = $false
foreach ($i in 1..30) {
    Start-Sleep 2
    try {
        $r = Invoke-RestMethod "$BaseUrl/api/perf/recent?limit=1" -TimeoutSec 2
        $w = $r.items[-1]
        if ($w -and [int]$w.board.plants -ge [math]::Min($Plants, 20) -and [int]$w.board.zombies -ge [math]::Min($Zombies, 30)) {
            $settled = $true
            "[stress] board live: $($w.board.plants)p / $($w.board.zombies)z"
            break
        }
    } catch {}
}
if (-not $settled) { Write-Warning "board census did not reach targets — check debug.stress.fill ack in /api/debug/events; continuing anyway" }

try {
    & (Join-Path $PSScriptRoot "probe-perf.ps1") -BaseUrl $BaseUrl -Scenario $Scenario -DurationSec $DurationSec
}
finally {
    # C1: restore caps / wave freeze / free-set so the session is playable afterwards.
    try { Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/debug/stress-clear" -ContentType "application/json" -Body '{}' | Out-Null; Write-Host "[stress] session state restored (stress-clear)" } catch { Write-Warning "stress-clear failed: $_" }
}

# Verdict against event-pipeline-v2-spec.md success criteria.
$doc = Get-Content (Join-Path $PSScriptRoot "..\docs\research\perf\_baseline-$Scenario.json") -Raw | ConvertFrom-Json
$windows = @($doc.windows)
function AvgOf($sel) { $v = @($windows | ForEach-Object $sel | Where-Object { $null -ne $_ }); if ($v.Count) { ($v | Measure-Object -Average).Average } else { 0 } }

$fps      = [math]::Round((AvgOf { $_.frames.fpsAvg }), 1)
$gen2     = ($windows | ForEach-Object { $_.gc.gen2 } | Measure-Object -Sum).Sum
$drainMs  = [math]::Round((AvgOf { $_.sections.'drain.tick'.totalMs }), 1)
$ocMs     = [math]::Round((AvgOf { $_.sections.'effect.onCapture'.totalMs }), 1)
$tdMs     = [math]::Round((AvgOf { $_.sections.'takeDamage.prefix'.totalMs }), 1)
$carried  = ($windows | ForEach-Object { $_.drain.carried } | Measure-Object -Sum).Sum
$dropped  = ($windows | ForEach-Object { $_.drain.droppedOverflow } | Measure-Object -Maximum).Maximum
# Sections nest: OnDrained (inside drain.tick) shares the onCapture section, so the pipeline
# share is drain + onCapture-outside-drain + takeDamage — not a raw sum (that double-counts).
$ocOutside = [math]::Max(0, $ocMs - $drainMs)
$pipePct  = [math]::Round(($drainMs + $ocOutside + $tdMs) / 5000 * 100, 2)

Write-Host ""
Write-Host "=== stress verdict ($Scenario) ==="
Write-Host ("fps avg          : {0}  (cap 60)" -f $fps)
Write-Host ("gen2 collections : {0}  (bar: 0)" -f $gen2)
Write-Host ("v2 pipeline share: {0}%  (bar: <=5%)  [drain {1} + onCaptureOutside {2} + takeDamage {3} ms/5s]" -f $pipePct, $drainMs, $ocOutside, $tdMs)
Write-Host ("drain carried    : {0} records total  (occasional carry ok; growth = budget too tight)" -f $carried)
Write-Host ("ring dropped     : {0}  (bar: 0)" -f $dropped)
# vfx-v2 budget (SPEC W2/F8): warning-level — vfx.tick <= 0.5% of wall at the 300z tier.
$vfxMs = [math]::Round((AvgOf { $_.sections.'vfx.tick'.totalMs }), 1)
$vfxPct = [math]::Round($vfxMs / 5000 * 100, 2)
Write-Host ("vfx.tick share   : {0}%  ({1} ms/5s; budget <=0.5% — warning only)" -f $vfxPct, $vfxMs)
if ($vfxPct -gt 0.5) { Write-Warning "vfx.tick over the 0.5% budget — see tasks/vfx-v2-todo.md T2 / SPEC.md F8" }
$pass = ($gen2 -eq 0) -and ($pipePct -le 5) -and (-not $dropped -or $dropped -eq 0)
Write-Host ($pass ? "VERDICT: PASS" : "VERDICT: CHECK FAILURES ABOVE") -ForegroundColor ($pass ? "Green" : "Yellow")
