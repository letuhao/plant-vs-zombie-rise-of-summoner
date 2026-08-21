# Server-side shield probe — no game required (combat-unification, sim-adoption U16).
# Prereq: FusionRpg.Server running (default http://127.0.0.1:5088).
# Spawns a sim plant, grants a 50-HP shield, deals 80 damage, prints the absorb + state.
param([string]$BaseUrl = "http://127.0.0.1:5088")

$ErrorActionPreference = "Stop"
function Post($path, $body) {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl$path" -ContentType "application/json" `
        -Body ($body | ConvertTo-Json -Depth 6)
}

Post "/api/sim/board/start" @{ levelName = "shield-probe" } | Out-Null
Post "/api/sim/plant/spawn" @{ ptr = "P1"; row = 2; col = 3; hp = 300; maxHp = 300 } | Out-Null

Write-Host "== grant 50 shield to P1 =="
(Post "/api/sim/shield/grant" @{ ptr = "P1"; amount = 50 }).events |
    Where-Object kind -eq "shield.granted" | Select-Object -Expand payload | Format-List

Write-Host "== deal 80 damage (expect shieldAbsorbed 50, hp 300 -> 270) =="
(Post "/api/sim/plant/damage" @{ ptr = "P1"; damage = 80 }).events |
    Where-Object kind -eq "plant.damage" | Select-Object -Expand payload | Format-List

Write-Host "== state =="
$state = Invoke-RestMethod -Uri "$BaseUrl/api/sim/state"
$state.plants | Select-Object ptr, hp, maxHp | Format-Table
$state.shields | Format-Table
