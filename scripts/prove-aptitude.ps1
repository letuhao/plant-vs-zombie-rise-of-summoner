# PROVE: aptitude resolve agrees between the overlay composer (DerivedComposer) and the battle
# composer (BattleStatComposer) for the same allocation/Theta — class-system-todo.md P2.6, V3.
#
# Unlike prove-overlay-combat.ps1, both engines being compared are pure FusionRpg.Core types (no
# live game, no injector, no server) — this drives tools/ProveAptitude, a small console tool, not a
# REST probe against a running instance.
#
# Checkpoint 2 is scoped to the one vertical slice Phase 2 actually built: Might -> combat.power.omni.
# The wider comparison (every channel an allocation touches) is available via -Channels "" but is NOT
# this script's default: it surfaces a real, pre-existing, out-of-scope-for-Phase-2 gap —
# BattleStatComposer's ChannelMods loop applies no cap at all (confirmed: zero `Cap(` calls in that
# file), so a SumIncreased-kind capped channel (status.resist.* etc.) can never agree once either side's
# contribution clears the cap. Not fixable here (spec-aptitude-resolve.md §8 forbids touching
# BattleStatComposer's compose logic) — it is P3.1's inheritance ("all twelve, all live channels...
# zero deltas"), not P2.6's. Documented, not hidden: run with -Channels "" to see it for yourself.
#
# Usage (repo root):
#   .\scripts\prove-aptitude.ps1                              # Checkpoint 2's scope: Might -> combat.power.omni
#   .\scripts\prove-aptitude.ps1 -Source Fortitude -Theta 500  # a different funded aptitude
#   .\scripts\prove-aptitude.ps1 -Channels ""                  # unfiltered — every touched channel (P3.1 preview)
param(
    [int]$Theta = 1000,
    [string]$Source = "Might",
    [long]$Points = 100,
    [string]$Channels = "combat.power.omni",
    [string]$OutJson = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $OutJson) {
    $OutJson = Join-Path $repoRoot "docs\research\class-system\_prove-aptitude.json"
}

$toolArgs = @("--theta", $Theta, "--source", $Source, "--points", $Points, "--out", $OutJson)
if ($Channels) { $toolArgs += @("--channels", $Channels) }

Push-Location (Join-Path $repoRoot "tools\ProveAptitude")
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
