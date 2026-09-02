# Guard: class-system program invariants stay executable (class-system-todo.md V2)
# Usage (repo root): .\scripts\guard-class-system.ps1
# Reads JSON directly (aptitude roster, derived-stats catalog, shipped aptitude tuning) rather than
# the C# registry — same reasoning as guard-stat-pairs.ps1: standalone, pre-build tooling that cannot
# reference FusionRpg.Core. Two rules (G2/G3) key off `data/tuning/aptitudes.v*.json`, the SHIPPED
# config. That file did not exist when this guard was written, so the note here used to say "the config
# P2.1 lands... the real tree does not exercise G2/G3 yet" — stale since P2.1 landed, and five versions
# have shipped since (v1..v5, corrected 2026-09-02). The absence branch below is kept anyway: it is what
# makes a fresh checkout say "nothing to check" instead of failing on a missing file.
# Planted-violation fixtures (Guard.Tests) point -Root at a synthetic tree carrying that file, so every
# rule stays provable independently of what the real tree holds.
#
# ⛔ The real tree exits 1, BY DESIGN: G3 reports Might/Ferocity feeding both `combat.power.*` and
# `progression.bonus.atk`. That is class-system-plan.md decision 12 — a deliberate, permanent
# forward-looking safeguard for battle-adoption's transition, present in v1 through v5 alike and never
# silenced by editing the shipped tuning. deploy-play.ps1 tolerates exactly this named exception and
# hard-fails on any other finding; ClassSystemGuardTests pins it. Do not "fix" it here.
# Live in deploy-play.ps1 and Guard.Tests.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$failures = @()

# ---- Sources -----------------------------------------------------------------------------------
$AptitudeRosterPath = Join-Path $Root "data\seed\aptitudes\roster.json"
$CatalogPath        = Join-Path $Root "data\seed\derived-stats\catalog.json"
$ShippedTuningDir   = Join-Path $Root "data\tuning"
$SrcDir             = Join-Path $Root "src"

if (-not (Test-Path $AptitudeRosterPath)) { throw "aptitudes roster.json missing: $AptitudeRosterPath" }
if (-not (Test-Path $CatalogPath)) { throw "catalog.json missing: $CatalogPath" }

$aptitudes = @((Get-Content -LiteralPath $AptitudeRosterPath -Raw | ConvertFrom-Json).entries)
$catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json
$catalogFamilies = @($catalog.entries | ForEach-Object { [string]$_.family })

# The shipped aptitude tuning config, once P2.1 lands. Highest version wins — never hand-edited,
# always published as v{n+1} (tunables-ssot.md T4), so the newest file is the live one.
# Sorted NUMERICALLY on the version, not by name: a lexical sort puts v9 above v10, which would make
# this guard silently check a superseded config the first time the tenth version ships.
$shippedTuningFile = $null
if (Test-Path $ShippedTuningDir) {
    $shippedTuningFile = Get-ChildItem -Path $ShippedTuningDir -Filter "aptitudes.v*.json" -ErrorAction SilentlyContinue |
        Sort-Object { [int]([regex]::Match($_.Name, "aptitudes\.v(\d+)\.json").Groups[1].Value) } -Descending |
        Select-Object -First 1
}

# ---- G1: every aptitude id is collision-free ------------------------------------------------------
$ids = @($aptitudes | ForEach-Object { [string]$_.id })
foreach ($d in ($ids | Group-Object | Where-Object { $_.Count -gt 1 })) {
    $failures += "G1 $($d.Name): duplicate aptitude id"
}
foreach ($id in $ids) {
    foreach ($family in $catalogFamilies) {
        if ($id -eq $family) {
            $failures += "G1 ${id}: collides with a registered channel family '$family'"
        }
    }
}

# ---- G2: every edge channel is registered ----------------------------------------------------------
# ---- G3: no aptitude reaches atk twice (combat.power.* AND progression.bonus.atk from one source) --
if ($null -ne $shippedTuningFile) {
    $tuning = Get-Content -LiteralPath $shippedTuningFile.FullName -Raw | ConvertFrom-Json
    $edges = @($tuning.edges | Where-Object { $_.channel -and ([string]$_.channel).Trim() -ne "" })

    foreach ($e in $edges) {
        $chan = [string]$e.channel
        $registered = $catalogFamilies -contains $chan
        if (-not $registered) {
            $lastDot = $chan.LastIndexOf(".")
            if ($lastDot -gt 0) {
                $stripped = $chan.Substring(0, $lastDot)
                $registered = $catalogFamilies -contains $stripped
            }
        }
        if (-not $registered) {
            $failures += "G2 $($e.source) -> ${chan}: channel not found in derived-stats catalog (exact or family prefix)"
        }
    }

    $atkSources = @($edges | Where-Object { $_.channel -eq "progression.bonus.atk" } | ForEach-Object { [string]$_.source })
    $powerSources = @($edges | Where-Object { $_.channel -like "combat.power.*" } | ForEach-Object { [string]$_.source })
    foreach ($s in ($atkSources | Select-Object -Unique)) {
        if ($powerSources -contains $s) {
            $failures += "G3 ${s}: feeds both combat.power.* and progression.bonus.atk — double-counted atk"
        }
    }
} else {
    Write-Host "  (G2/G3 skipped — no shipped data\tuning\aptitudes.v*.json yet; nothing to check)"
}

# ---- G4: every unitClass: null carries a note -------------------------------------------------------
foreach ($e in $catalog.entries) {
    $hasUnitClass = -not [string]::IsNullOrWhiteSpace([string]$e.unitClass)
    if ($hasUnitClass) { continue }
    $note = [string]$e.unitClassNote
    if ([string]::IsNullOrWhiteSpace($note)) {
        $failures += "G4 $($e.family): unitClass is null with no unitClassNote"
    }
}

# ---- G5: AptitudeReadFunctions has AT MOST one implementation (class-system-map.md SS2d) ------------
if (Test-Path $SrcDir) {
    $hits = @()
    Get-ChildItem -Path $SrcDir -Recurse -Filter "*.cs" | ForEach-Object {
        $text = Get-Content -LiteralPath $_.FullName -Raw
        if ($text -match "class\s+AptitudeReadFunctions\b") { $hits += $_.FullName }
    }
    if ($hits.Count -gt 1) {
        $failures += "G5: AptitudeReadFunctions has $($hits.Count) implementations: $($hits -join ', ')"
    }
}

# ---- G6: DominantPosture is never called from a resolve/subsystem path (P1.3 — a read, never wired) -
if (Test-Path $SrcDir) {
    Get-ChildItem -Path $SrcDir -Recurse -Filter "*.cs" | ForEach-Object {
        if ($_.Name -eq "DominantPosture.cs") { return }
        if ($_.Name -notmatch "Resolve|Subsystem|Composer") { return }
        $text = Get-Content -LiteralPath $_.FullName -Raw
        if ($text -match "DominantPosture\s*\.\s*Of\s*\(") {
            $failures += "G6 $($_.FullName): resolve-shaped file calls DominantPosture.Of — it is a display read, never a resolve input"
        }
    }
}

# ---- G7: the closed form calls shipped combat symbols, never re-derives them (P4.1, a symbol-
# reference check — spec-deterministic-core.md §2/§7: "Never: re-implement a combat formula") --------
# Positive check only: files whose OWN job is per-swing DAMAGE (StrikeMixture, PhaseModel) must
# reference at least one shipped combat symbol from the allowlist. Pure-statistics files (FirstPassage,
# Race — generic mean/variance/Phi/rho over numbers StrikeMixture already produced) are NOT checked
# here; they legitimately call no combat symbol at all, and a blanket rule would be a false positive.
$AnalyticDir = Join-Path $SrcDir "FusionRpg.Core\Balance\Analytic"
$DamageComputingFiles = @("StrikeMixture.cs", "PhaseModel.cs")
$ShippedCombatSymbols = @(
    "CombatProbability\.", "ClampedContest\.", "OverlayCombatCalculator\.", "CombatDerivedReader\.",
    "ShieldMath\.", "ShieldRuntime\.", "ResistanceEvaluator\.", "OverlayCombatMath\.", "CombatDamageDispatcher\."
)
if (Test-Path $AnalyticDir) {
    Get-ChildItem -Path $AnalyticDir -Filter "*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -notin $DamageComputingFiles) { return }
        $text = Get-Content -LiteralPath $_.FullName -Raw
        $hit = $ShippedCombatSymbols | Where-Object { $text -match $_ }
        if (-not $hit) {
            $failures += "G7 $($_.FullName): no reference to a shipped combat symbol found — a per-swing damage file in Balance/Analytic must call the shipped resolver, never re-derive its formulas"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "CLASS-SYSTEM GUARD FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "CLASS-SYSTEM GUARD OK — aptitude ids collision-free, edges registered, no atk double-count, every null unitClass noted, at most one AptitudeReadFunctions, DominantPosture unwired, closed form calls shipped combat symbols"
exit 0
