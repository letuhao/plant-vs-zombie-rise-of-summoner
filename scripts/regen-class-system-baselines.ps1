# class-system-todo.md V4 — regenerate the three checked-in baselines every later phase diffs
# against, rather than describing a result in prose.
#   _baseline-residual.json    <- CombatSim `predict --json` (closed form vs simulator, per arrow)
#   _baseline-dominance.json   <- CombatSim `trinity --json` (12x12 dominance matrix + coverage)
#   _baseline-goldens.json     <- the four BattleGoldenTests.cs hash consts, extracted not retyped
# Usage (repo root): .\scripts\regen-class-system-baselines.ps1
# Fixed seeds throughout (predict: 8888, trinity: 20260826 — CombatSim's own defaults) so re-running
# this script reproduces byte-identical `arrows`/`dominanceMatrix` content; only `_meta.measuredAt`
# differs between runs, by design (Guard/prove tests strip it before the determinism comparison).
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$OutDir = Join-Path $Root "docs\research\class-system"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# PSCustomObject + Add-Member rather than `ConvertFrom-Json -AsHashtable`: the -AsHashtable parameter
# does not exist on Windows PowerShell 5.1's ConvertFrom-Json (only pwsh 6+), and this script is
# invoked as plain `powershell` by the Guard.Tests determinism proof — the exact PS-5.1-vs-7 host
# divergence guard-stat-pairs.ps1's own P4 comment already warns about, one level up the stack.
function Add-Meta([string]$path, [string]$conditions) {
    $doc = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $meta = [pscustomobject]@{
        measuredAt = (Get-Date).ToUniversalTime().ToString("o")
        conditions = $conditions
    }
    if ($doc.PSObject.Properties.Match("_meta").Count -gt 0) {
        $doc._meta = $meta
    } else {
        $doc | Add-Member -NotePropertyName "_meta" -NotePropertyValue $meta
    }
    ($doc | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $path -Encoding UTF8
}

Write-Host "==> _baseline-residual.json (CombatSim predict --json, live v2 config, elements live)"
$residualPath = Join-Path $OutDir "_baseline-residual.json"
$liveAptitudesPath = Join-Path $Root "data\tuning\aptitudes.v2.json"
if (-not (Test-Path $liveAptitudesPath)) { throw "missing $liveAptitudesPath" }

# class-system-todo.md P8.1/Checkpoint 8, resolved 2026-08-27: the tuning-sync concern that blocked
# this (a second session's own uncommitted tools/CombatSim work) turned out not to exist -- that diff
# was this program's own earlier, static WIP, and this tool's resolver was blind to the AptitudeMitigation
# dial regardless of whose changes they were. Both are fixed now: --models below points at the LIVE
# shipped config directly (never tools/CombatSim's own internal v1 POC copy), and AptitudeTuning.ToModel
# (AptitudeTuning.cs) mirrors Core's AptitudeResolver.EffectiveKMilli's recovery/mitigation scaling
# exactly (tests/FusionRpg.Core.Tests/Balance/ResolverMatchesSimulatorTests.cs, P3.4, proves the two
# resolvers agree within measured floating-point/discretization noise).
#
# Elements-live needs archetypes with DIFFERENT elements, which the tracked builds/*.json (all "fire")
# do not carry -- scratch copies only, matching P8.1's own already-validated ad-hoc methodology exactly
# (FORCE=fire, FINESSE=air, BASTION=earth, tools/CombatSim/archetypes/'s own existing assignment),
# never overwriting the tracked files (P8.1's own "Build.Load's File.Exists-before-default-directory
# check" trick).
$elementScratchDir = Join-Path $OutDir "_scratch-elements"
New-Item -ItemType Directory -Force -Path $elementScratchDir | Out-Null
$elementByArchetype = @{ force = "fire"; finesse = "air"; bastion = "earth" }
$scratchPaths = @()
foreach ($name in @("force", "finesse", "bastion")) {
    $build = Get-Content -LiteralPath (Join-Path $Root "tools\CombatSim\builds\$name.json") -Raw | ConvertFrom-Json
    $build.element = $elementByArchetype[$name]
    $scratchPath = Join-Path $elementScratchDir "$name.json"
    ($build | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $scratchPath -Encoding UTF8
    $scratchPaths += $scratchPath
}

Push-Location $Root
try {
    dotnet run --project tools\CombatSim -- predict --json --out "$residualPath" `
        --models "$liveAptitudesPath" --archetypes ($scratchPaths -join ",") --theta 100 --seed 8888 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "CombatSim predict failed (exit $LASTEXITCODE)" }
} finally { Pop-Location }
Remove-Item -LiteralPath $elementScratchDir -Recurse -Force
Add-Meta $residualPath "data/tuning/aptitudes.v2.json (live shipped config -- P8.2 stamina bind + P8.3 mitigation dial, and this tool's own resolver was ported 2026-08-27 to apply that dial the same way Core's AptitudeResolver.EffectiveKMilli does). FORCE=fire/FINESSE=air/BASTION=earth (scratch copies of tools/CombatSim/builds/*.json with only element changed, never overwriting the tracked fire/fire/fire files). Theta=100, seed 8888, 3000 trials/arrow. Elements genuinely live: closed-form and simulated win shares both move with the matchup (P8.1's own headline finding, e.g. FORCE v FINESSE closed-form ~98% vs simulated ~68%), not the ~100%/~0% same-element result the tracked builds alone would have produced."

Write-Host "==> _baseline-dominance.json (CombatSim trinity --json, live v2 config)"
$dominancePath = Join-Path $OutDir "_baseline-dominance.json"
Push-Location $Root
try {
    dotnet run --project tools\CombatSim -- trinity --json --out "$dominancePath" --models "$liveAptitudesPath" --seed 20260826 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "CombatSim trinity failed (exit $LASTEXITCODE)" }
} finally { Pop-Location }
# class-system-todo.md P8.5/Checkpoint 8, resolved 2026-08-27: --models above now points trinity at the
# LIVE data/tuning/aptitudes.v2.json directly (not tools/CombatSim's own internal v1 POC copy), and
# that tool's resolver now applies the AptitudeMitigation dial the same way Core's own
# AptitudeResolver.EffectiveKMilli does (ResolverMatchesSimulatorTests.cs, P3.4) -- so `chains` below
# (a best-response CHASE, BestResponse.Chase, the one field DominanceGuard has no equivalent search for
# and so cannot reproduce the way dominanceMatrix/dominantCorners are overlaid below) is fresh against
# the real shipped config, not stale.
$dominanceDoc = Get-Content -LiteralPath $dominancePath -Raw | ConvertFrom-Json

# class-system-todo.md Checkpoint 8: dominanceMatrix/dominantCorners are ALSO closed-form-only
# (BestResponse.DominanceMatrix calls Analytic.Predict directly, confirmed P8.1 -- no RNG/simulator
# involved), so tools/DominanceBaseline reproduces them via FusionRpg.Core.DominanceGuard/
# TerminationGuard directly -- the SAME production resolver, not a second implementation that could
# drift from it. `chains` (a best-response CHASE, a different and more complex search DominanceGuard
# has no equivalent for) is NOT reproducible this way and stays trinity's own (see --models above).
Write-Host "==> overlaying dominanceMatrix/dominantCorners (FusionRpg.Core.DominanceGuard, live v2 config)"
$coreDominancePath = Join-Path $OutDir "_dominance-core-scratch.json"
Push-Location $Root
try {
    dotnet run --project tools\DominanceBaseline -- --theta 100 --out "$coreDominancePath" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "DominanceBaseline failed (exit $LASTEXITCODE)" }
} finally { Pop-Location }
$coreDominance = Get-Content -LiteralPath $coreDominancePath -Raw | ConvertFrom-Json
Remove-Item -LiteralPath $coreDominancePath -Force
$dominanceDoc.dominanceMatrix = $coreDominance.dominanceMatrix
$dominanceDoc.dominantCorners = $coreDominance.dominantCorners

$syncNote = "dominanceMatrix/dominantCorners updated 2026-08-27 (Checkpoint 8) -- measured via " +
    "tools/DominanceBaseline (FusionRpg.Core.DominanceGuard/TerminationGuard, the SAME production " +
    "resolver TerminationGuard.Assert uses), which reads the LIVE data/tuning/aptitudes.v2.json " +
    "config automatically, no internal copy. Independently corroborated by " +
    "DominanceGuardTests.cs's own Measure_theRealTwelveCornerShape_matchesTheCheckedInBaselinesEmptyDominantCorners " +
    "and docs/research/class-residual-2026-08-27.md's P8.5 section (same headline finding: no absolute " +
    "dominant corner, Retribution near-dominant at 10 of 11, loses only to Pierce). " +
    "`chains` above is ALSO fresh as of 2026-08-27, closing the one remaining gap: trinity now runs " +
    "with --models pointed at this same live v2 file (never tools/CombatSim's own internal v1 POC " +
    "copy), and that tool's own resolver (AptitudeTuning.ToModel) was ported the same day to apply " +
    "the AptitudeMitigation dial the way Core's AptitudeResolver.EffectiveKMilli does " +
    "(ResolverMatchesSimulatorTests.cs, P3.4) -- so this is a best-response CHASE (BestResponse.Chase, " +
    "a search DominanceGuard has no equivalent for and so cannot reproduce the way dominanceMatrix/ " +
    "dominantCorners are overlaid above) against the real shipped config, not a stale internal copy. " +
    "coverage.elementAxis stays `"neutral`" deliberately, not as a residual gap: dominanceMatrix/" +
    "dominantCorners/chains all spike individual APTITUDES, and aptitudes feed combat.power.omni only " +
    "by design (class-system-ideal.md 4.1 rule 2 -- `"an aptitude reaches a MECHANISM, never a " +
    "FLAVOUR`"; elements are a flavour axis, so any aptitude-to-element mapping would be arbitrary, " +
    "which is exactly the rule this file's OWN elements-live measurement lives in _baseline-residual." +
    "json instead, at the ARCHETYPE/posture level, where FORCE/FINESSE/BASTION do carry a real element)."
$dominanceDoc.coverage | Add-Member -NotePropertyName "tuningSync" -NotePropertyValue $syncNote -Force
($dominanceDoc | ConvertTo-Json -Depth 10) | Set-Content -LiteralPath $dominancePath -Encoding UTF8
Add-Meta $dominancePath "dominanceMatrix/dominantCorners: data/tuning/aptitudes.v2.json (live, via FusionRpg.Core.DominanceGuard -- P8.2/P8.3's stamina bind + mitigation dial). chains: ALSO data/tuning/aptitudes.v2.json now (live, via tools/CombatSim trinity --models, that tool's own resolver ported 2026-08-27 to apply the mitigation dial too). All twelve spiked corners, Theta=100, seed 20260826 — elementAxis neutral BY DESIGN (aptitudes are element-blind, class-system-ideal.md 4.1 rule 2 -- not a gap; see coverage.tuningSync), action economy off."

Write-Host "==> _baseline-goldens.json (BattleGoldenTests.cs hash consts, extracted not retyped)"
$goldensTestPath = Join-Path $Root "tests\FusionRpg.Core.Tests\Battle\BattleGoldenTests.cs"
if (-not (Test-Path $goldensTestPath)) { throw "missing $goldensTestPath" }
$goldensText = Get-Content -LiteralPath $goldensTestPath -Raw

function Get-HashConst([string]$text, [string]$name) {
    $m = [regex]::Match($text, "const string $name\s*=\s*""([0-9A-Fa-f]+)""")
    if (-not $m.Success) { throw "could not find const string $name in BattleGoldenTests.cs" }
    return $m.Groups[1].Value
}
# The LIVE RulesetVersion, from the const itself -- not a comment in BattleGoldenTests.cs, which
# accumulates one prose paragraph per historical re-bless and would match the wrong (earliest) one.
$rulesetSourcePath = Join-Path $Root "src\FusionRpg.Core\Battle\BattleModels.cs"
if (-not (Test-Path $rulesetSourcePath)) { throw "missing $rulesetSourcePath" }
$rulesetMatch = [regex]::Match((Get-Content -LiteralPath $rulesetSourcePath -Raw), "public const int RulesetVersion\s*=\s*(\d+)")
if (-not $rulesetMatch.Success) { throw "could not find BattleRuleset.RulesetVersion in $rulesetSourcePath" }
$goldens = [ordered]@{
    rulesetVersion = [int]$rulesetMatch.Groups[1].Value
    stompHash      = Get-HashConst $goldensText "StompHash"
    closeHash      = Get-HashConst $goldensText "CloseHash"
    wipeHash       = Get-HashConst $goldensText "WipeHash"
    seedSweepHash  = Get-HashConst $goldensText "SeedSweepHash"
}
$goldensPath = Join-Path $OutDir "_baseline-goldens.json"
($goldens | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath $goldensPath -Encoding UTF8
Add-Meta $goldensPath "Extracted from tests/FusionRpg.Core.Tests/Battle/BattleGoldenTests.cs's four const string Hash declarations — never hand-retyped, so this file cannot itself drift from the test."

Write-Host ""
Write-Host "Wrote:"
Write-Host "  $residualPath"
Write-Host "  $dominancePath"
Write-Host "  $goldensPath"
