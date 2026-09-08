[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:5088",
    [ValidateSet("quick", "button")]
    [string]$Method = "quick",
    [int]$TimeoutSec = 15
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

$health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5
if (-not $health.ok) { throw "server health.ok=false" }
if (-not $health.injectorConnected) { throw "injector is not connected" }

$body = @{ method = $Method; timeoutSec = $TimeoutSec } | ConvertTo-Json -Compress
$result = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/debug/setup/skip" `
    -ContentType "application/json" -Body $body -TimeoutSec ($TimeoutSec + 5)

if (-not $result.ok) {
    throw "setup skip failed: $($result.error)"
}

Write-Host ("LIVE setup skip succeeded: method={0} acknowledgement={1}" -f $result.method, ($result.acknowledgement | ConvertTo-Json -Compress))
Write-Host "Verify the plant-selection panel advanced and the game is progressing before continuing the probe."
