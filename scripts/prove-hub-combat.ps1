# PROVE: the program's own operator prove for actor-hub-and-combat-power-solid-fixing (T19) --
# battle Hub and sheet Hub agree on the SAME equip/tree bound atoms for one UniqueActor, AND Standing
# rises via a real Hub combat writer (a shipped skill.cooldown/effectiveness aptitude edge) while Theta
# (level) alone never moves it. No live game, no live server -- drives tools/ProveHubCombat, a small
# console tool over RpgStore.InMemory(), the same shape prove-aptitude.ps1 already established.
#
# Bullet 3 (Bound lawn aptitude input vs Server UniqueCreature compose, lawn-aptitude-parity) is
# deliberately NOT here: that task (T12) found the Injector has zero UniqueCreature aptitude fetch to
# compare against -- aptitude-sheet's own unique-lawn-wire (AS-1.1) is unbuilt. Nothing to prove until
# that lands; see tasks/actor-hub-and-combat-power-solid-fixing-evidence-map.md T12/T19.
#
# Usage (repo root):
#   .\scripts\prove-hub-combat.ps1
#   .\scripts\prove-hub-combat.ps1 -OutJson "path\to\result.json"
param(
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $OutJson) {
    $OutJson = Join-Path $repoRoot "docs\research\actor-hub-and-combat-power\_prove-hub-combat.json"
}

$toolArgs = @("--out", $OutJson)

Push-Location (Join-Path $repoRoot "tools\ProveHubCombat")
try {
    & dotnet run --no-build -- @toolArgs
    if ($LASTEXITCODE -ne 0) {
        # --no-build fails on a clean checkout with no prior build; retry once, built this time.
        & dotnet run -- @toolArgs
    }
    $exitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

exit $exitCode
