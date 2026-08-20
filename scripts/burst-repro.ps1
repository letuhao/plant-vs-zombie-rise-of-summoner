# Server burst-stability repro — perf-v3-spec.md module server-burst (B1).
# Launches a SCRATCH server (own port + data dir), floods /api/events with a synthetic
# spawn/death burst mimicking the 1000-zombie stress fill, and reports whether the server
# survives. Never points at a live dev server unless -BaseUrl is passed explicitly.
param(
    [string]$BaseUrl = "",
    [int]$Events = 6000,
    [int]$Batch = 256,
    [int]$Port = 5177,
    [string]$OutLog = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutLog) { $OutLog = Join-Path $root "docs\research\perf\_burst-repro-last.json" }

$proc = $null
if (-not $BaseUrl) {
    $BaseUrl = "http://127.0.0.1:$Port"
    $exe = Join-Path $root "dist\FusionRpg.Server\FusionRpg.Server.exe"
    if (-not (Test-Path $exe)) { Write-Error "publish the server first (deploy-play.ps1)"; exit 1 }
    $dataDir = Join-Path $env:TEMP "fusionrpg-burst-data-$([guid]::NewGuid().ToString('N').Substring(0,8))"
    New-Item -ItemType Directory -Force $dataDir | Out-Null
    $env:FUSIONRPG_URLS = $BaseUrl
    $env:FUSIONRPG_DATA = $dataDir
    $stdout = Join-Path $dataDir "server-out.log"
    $stderr = Join-Path $dataDir "server-err.log"
    $proc = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $env:FUSIONRPG_URLS = $null
    $env:FUSIONRPG_DATA = $null
    $up = $false
    foreach ($i in 1..20) { Start-Sleep 1; try { Invoke-RestMethod "$BaseUrl/health" -TimeoutSec 2 | Out-Null; $up = $true; break } catch {} }
    if (-not $up) { Write-Error "scratch server failed to start"; exit 1 }
    Write-Host "[burst] scratch server up on $BaseUrl (data: $dataDir, pid $($proc.Id))"
}

function New-Batch([int]$startIdx, [int]$count, [string]$matchKey) {
    # Mimics the real stress fill: per entity a zombie.place + zombie.spawn (fat ~30-field
    # dump) + stat.applied, then a zombie.die — the full projection fan-out per zombie.
    $items = New-Object System.Collections.Generic.List[object]
    $ts = (Get-Date).ToUniversalTime().ToString("o")
    for ($i = 0; $i -lt $count; $i++) {
        $n = $startIdx + $i
        $ptr = "B{0:X}" -f (0xA000 + $n)
        switch ($n % 4) {
            0 { $items.Add(@{ t = $ts; kind = "zombie.place"; matchKey = $matchKey
                              payload = @{ ptr = $ptr; type = ($n % 30); typeName = "Zed$($n % 30)"; row = ($n % 5); theX = 7.5; mindControlled = $false; withEffect = $false } }) }
            1 { $items.Add(@{ t = $ts; kind = "zombie.spawn"; matchKey = $matchKey
                              payload = @{ ptr = $ptr; typeId = ($n % 30); type = ($n % 30); typeName = "Zed$($n % 30)"; side = "zombie"; row = ($n % 5); col = 8; x = 7.5; y = 1.1
                                           hp = 500; maxHp = 500; attack = 20; armor = 100; armorMax = 100; theSecondArmorHealth = 0; theSecondArmorMaxHealth = 0
                                           theSpeed = 1.2; source = "debug.spawn"; displayName = "Burst Zombie $n"; f1 = 1; f2 = 2; f3 = 3; f4 = 4; f5 = 5; f6 = 6; f7 = 7; f8 = 8; f9 = 9; f10 = 10 } }) }
            2 { $items.Add(@{ t = $ts; kind = "stat.applied"; matchKey = $matchKey
                              payload = @{ ptr = $ptr; side = "zombie"; typeId = ($n % 30); hpBefore = 500; hpAfter = 25000; maxBefore = 500; maxAfter = 25000; atkBefore = 20; atkAfter = 200; source = "debug.spawn" } }) }
            3 { $items.Add(@{ t = $ts; kind = "zombie.die"; matchKey = $matchKey
                              payload = @{ ptr = ("B{0:X}" -f (0xA000 + $n - 2)); type = (($n - 2) % 30); typeName = "Zed$(($n - 2) % 30)"; reason = 1 } }) }
        }
    }
    return ,$items
}

$matchKey = "burst-" + [guid]::NewGuid().ToString("N").Substring(0, 8)
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$result = [ordered]@{ events = $Events; batch = $Batch; matchKey = $matchKey; healthFailures = 0; sendFailures = 0 }

# board.start first — server needs the run row for projections (audit §4c.1).
$start = @{ events = @(@{ t = (Get-Date).ToUniversalTime().ToString("o"); kind = "board.start"; matchKey = $matchKey; payload = @{ levelName = "burst"; matchKey = $matchKey } }) } | ConvertTo-Json -Depth 6
Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/events" -ContentType "application/json" -Body $start | Out-Null

$sent = 0
while ($sent -lt $Events) {
    $n = [math]::Min($Batch, $Events - $sent)
    $body = @{ events = (New-Batch $sent $n $matchKey) } | ConvertTo-Json -Depth 6
    try {
        Invoke-RestMethod -Method POST -Uri "$BaseUrl/api/events" -ContentType "application/json" -Body $body -TimeoutSec 10 | Out-Null
    } catch {
        $result.sendFailures++
        Write-Warning "send failed at ${sent}: $($_.Exception.Message)"
        Start-Sleep -Milliseconds 200
    }
    $sent += $n
    if ($sent % (4 * $Batch) -eq 0) {
        try { Invoke-RestMethod "$BaseUrl/health" -TimeoutSec 3 | Out-Null }
        catch { $result.healthFailures++; Write-Warning "health failed at $sent events" }
    }
}
$sw.Stop()

Start-Sleep 3
$alive = $true
try { $h = Invoke-RestMethod "$BaseUrl/health" -TimeoutSec 5; } catch { $alive = $false }
$procAlive = if ($proc) { -not $proc.HasExited } else { $null }

$result.elapsedMs = $sw.ElapsedMilliseconds
$result.serverRespondingAfter = $alive
$result.processAlive = $procAlive
$result.eventsPerSec = [math]::Round($Events / [math]::Max(0.001, $sw.Elapsed.TotalSeconds), 0)
$result | ConvertTo-Json | Set-Content $OutLog -Encoding UTF8

Write-Host ""
Write-Host "=== burst repro result ==="
Write-Host ("sent {0} events in {1}ms ({2}/s), sendFailures={3}, healthFailures={4}" -f $Events, $sw.ElapsedMilliseconds, $result.eventsPerSec, $result.sendFailures, $result.healthFailures)
Write-Host ("server responding after: {0}   process alive: {1}" -f $alive, $procAlive)
if ($proc -and $proc.HasExited) { Write-Host ("process EXIT CODE: {0}" -f $proc.ExitCode) }
if ($proc) { Write-Host "server logs in the scratch data dir (server-out.log / server-err.log)" }

if ($proc -and -not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
