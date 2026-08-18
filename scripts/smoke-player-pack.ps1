# Smoke-test an unpacked player pack (default: dist\FusionRpg).
# Usage:
#   .\scripts\smoke-player-pack.ps1
#   .\scripts\smoke-player-pack.ps1 -PackDir dist\FusionRpg -SkipServerBoot
param(
    [string]$PackDir = "",
    [switch]$SkipServerBoot
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

if (-not $PackDir) { $PackDir = Join-Path $Root "dist\FusionRpg" }
$PackDir = [System.IO.Path]::GetFullPath($PackDir)
if (-not (Test-Path $PackDir)) {
    throw "Pack directory not found: $PackDir (run scripts/publish-player.ps1 first)"
}

$Artifacts = Join-Path $Root "artifacts"
New-Item -ItemType Directory -Force -Path $Artifacts | Out-Null
$SummaryPath = Join-Path $Artifacts "player-pack-smoke.json"
$PackSmokeProj = Join-Path $Root "tools\FusionRpg.PackSmoke\FusionRpg.PackSmoke.csproj"

Write-Host "==> Build FusionRpg.PackSmoke"
& dotnet build $PackSmokeProj -c Release --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "PackSmoke build failed" }

Write-Host "==> PlayerPackProbe on $PackDir"
$probeJson = & dotnet run --project $PackSmokeProj -c Release --no-build -- $PackDir
$probeExit = $LASTEXITCODE
Write-Host $probeJson

$serverStep = [ordered]@{ name = "server_boot"; ok = $true; message = "Skipped (-SkipServerBoot)." }
if (-not $SkipServerBoot) {
    $serverExe = Join-Path $PackDir "Server\FusionRpg.Server.exe"
    if (-not (Test-Path $serverExe)) {
        $serverStep = [ordered]@{ name = "server_boot"; ok = $false; message = "Missing Server\FusionRpg.Server.exe" }
    }
    else {
        $port = Get-Random -Minimum 5200 -Maximum 5800
        $dataDir = Join-Path $env:TEMP ("FusionRpgSmokeData-" + [guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
        $url = "http://127.0.0.1:$port"
        Write-Host "==> Booting server on $url (SIM must stay off)"

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $serverExe
        $psi.WorkingDirectory = Split-Path $serverExe -Parent
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.CreateNoWindow = $true
        $psi.Environment["FUSIONRPG_NO_BROWSER"] = "1"
        $psi.Environment["FUSIONRPG_URLS"] = $url
        $psi.Environment["FUSIONRPG_DATA"] = $dataDir

        $proc = [System.Diagnostics.Process]::Start($psi)
        try {
            $ok = $false
            $deadline = [datetime]::UtcNow.AddSeconds(45)
            while ([datetime]::UtcNow -lt $deadline) {
                try {
                    $resp = Invoke-WebRequest -Uri "$url/health" -UseBasicParsing -TimeoutSec 2
                    if ($resp.StatusCode -eq 200 -and $resp.Content -match '"ok"\s*:\s*true') {
                        $ok = $true
                        break
                    }
                }
                catch {
                    Start-Sleep -Milliseconds 400
                }
            }

            if (-not $ok) {
                $serverStep = [ordered]@{
                    name = "server_boot"
                    ok = $false
                    message = "GET /health did not return ok within timeout."
                }
            }
            else {
                # /api/test/* is only mapped when FUSIONRPG_SIM=1. With SPA fallback,
                # unknown paths return index.html (200) — so assert health.simEnabled
                # and that snapshot is HTML (fallback), not the SIM JSON payload.
                $healthJson = (Invoke-WebRequest -Uri "$url/health" -UseBasicParsing -TimeoutSec 5).Content
                $simOn = $healthJson -match '"simEnabled"\s*:\s*true'
                $snap = Invoke-WebRequest -Uri "$url/api/test/snapshot" -UseBasicParsing -TimeoutSec 5
                $ct = "$($snap.Headers['Content-Type'])"
                $looksLikeSimJson = $snap.Content -match '"eventCount"' -or $snap.Content -match '"simEnabled"'
                if ($simOn) {
                    $serverStep = [ordered]@{
                        name = "server_boot"
                        ok = $false
                        message = "Health reports simEnabled=true (player pack must leave SIM off)."
                    }
                }
                elseif ($looksLikeSimJson -and $ct -notmatch "text/html") {
                    $serverStep = [ordered]@{
                        name = "server_boot"
                        ok = $false
                        message = "/api/test/snapshot returned SIM JSON (expected SPA fallback / SIM off)."
                    }
                }
                else {
                    $serverStep = [ordered]@{
                        name = "server_boot"
                        ok = $true
                        message = "Health ok; simEnabled off; /api/test/snapshot is SPA fallback (not SIM)."
                    }
                }
            }
        }
        finally {
            if ($null -ne $proc -and -not $proc.HasExited) {
                try { $proc.Kill($true) } catch {
                    try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch { }
                }
            }
            if ($null -ne $proc) { $proc.Dispose() }
            try { Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue } catch { }
        }
    }
}

$probeObj = $null
try { $probeObj = $probeJson | ConvertFrom-Json } catch {
    $probeObj = [ordered]@{ ok = ($probeExit -eq 0); steps = @(); packDir = $PackDir }
}

$summaryOk = (($probeExit -eq 0) -and [bool]$serverStep.ok)
$summary = [ordered]@{
    ok = $summaryOk
    packDir = $PackDir
    probeExitCode = $probeExit
    probe = $probeObj
    server = $serverStep
    utc = [datetime]::UtcNow.ToString("o")
}
($summary | ConvertTo-Json -Depth 8) | Set-Content -Path $SummaryPath -Encoding UTF8
Write-Host "==> Wrote $SummaryPath"

if (-not $summaryOk) {
    Write-Host "SMOKE FAILED" -ForegroundColor Red
    exit 1
}
Write-Host "SMOKE PASSED" -ForegroundColor Green
exit 0
