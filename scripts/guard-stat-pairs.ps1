# Guard: the counterbalance rule stays executable (spec-stat-taxonomy.md SS6.2, T0.3)
# Usage (repo root): .\scripts\guard-stat-pairs.ps1
# Reads data/seed/derived-stats/catalog.json rather than the C# registry directly — that file is the
# documented machine-readable mirror for tooling that cannot reference FusionRpg.Core, and this guard
# is exactly such tooling: it must run standalone, pre-build, like guard-power.ps1.
# Live in deploy-play.ps1 and Guard.Tests.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$CatalogPath = Join-Path $Root "data\seed\derived-stats\catalog.json"

if (-not (Test-Path $CatalogPath)) { throw "catalog.json missing: $CatalogPath" }

$catalog = Get-Content -LiteralPath $CatalogPath -Raw | ConvertFrom-Json

# ---- Flatten `entries` and `prefixFamilies` into one row shape. The two arrays are checked for
# internal symmetry SEPARATELY (a `entries` row never names a `prefixFamilies` row as its counterpart,
# and vice versa) — they describe different things (fixed categories vs sparse per-id overrides) and
# were never meant to cross-reference.
function Get-Rows($catalogEntries, [string]$nameField, [string]$group) {
    $rows = @()
    foreach ($e in $catalogEntries) {
        $rows += [pscustomobject]@{
            Name        = $e.$nameField
            StatClass   = [string]$e.statClass
            Counterpart = [string]$e.counterpart
            Cap         = $e.cap
            UnitClass   = [string]$e.unitClass
            Group       = $group
        }
    }
    return $rows
}

$rows = @()
$rows += Get-Rows $catalog.entries "family" "entries"
$rows += Get-Rows $catalog.prefixFamilies "prefix" "prefixFamilies"

$failures = @()

# ---- P1: every Contest family names a counterpart -------------------------------------------------
foreach ($r in $rows) {
    if ($r.StatClass -eq "Contest" -and [string]::IsNullOrWhiteSpace($r.Counterpart)) {
        $failures += "P1 $($r.Name): Contest class with no counterpart"
    }
}

# ---- P2: every declared counterpart resolves, and the pair is symmetric ---------------------------
foreach ($group in ($rows | Group-Object Group)) {
    $byName = @{}
    foreach ($r in $group.Group) { $byName[$r.Name] = $r }
    foreach ($r in $group.Group) {
        if ([string]::IsNullOrWhiteSpace($r.Counterpart)) { continue }
        $other = $byName[$r.Counterpart]
        if ($null -eq $other) {
            $failures += "P2 $($r.Name): counterpart '$($r.Counterpart)' does not resolve in $($group.Name)"
            continue
        }
        if ($other.Counterpart -ne $r.Name) {
            $failures += "P2 $($r.Name) <-> $($r.Counterpart): asymmetric pair (back-reference is '$($other.Counterpart)')"
        }
    }
}

# ---- P3: Race class never declares a counterpart — the opponent's own value is the counter --------
foreach ($r in $rows) {
    if ($r.StatClass -eq "Race" -and -not [string]::IsNullOrWhiteSpace($r.Counterpart)) {
        $failures += "P3 $($r.Name): Race class must not declare a counterpart"
    }
}

# ---- P4: a Contest magnitude (GameUnits / GameUnitsPerSecond) is never capped ----------------------
# Scoped to the two true "magnitude" unit classes, not every UnitClass — SigmoidPoints/
# SigmoidMultiplierPoints are deliberately uncapped INPUTS to a bounded output (spec-stat-taxonomy.md
# SS2.5) and StatusPotencyPoints' shipped 0.95 resist cap is a pre-existing, documented, bounded-ratio-
# shaped magnitude cap (SS11.6 of ssot-power-scale.md) that this guard does not relitigate.
$magnitudeUnits = @("GameUnits", "GameUnitsPerSecond")
foreach ($r in $rows) {
    if ($r.StatClass -ne "Contest") { continue }
    if ($magnitudeUnits -notcontains $r.UnitClass) { continue }
    # A JSON number deserializes as Int32/Int64/Double under pwsh 7 but as Decimal under Windows
    # PowerShell 5.1 (ConvertFrom-Json differs by host version) -- [ValueType] catches every numeric
    # CLR type in one check instead of enumerating them and missing one, the way an earlier draft did.
    # JSON null -> $null (not a ValueType) and a documentary string like "MaxNetFactor" -> [string]
    # (a reference type), so both are correctly excluded without checking for them explicitly.
    $isNumericCap = ($null -ne $r.Cap) -and ($r.Cap -is [ValueType]) -and ($r.Cap -isnot [bool])
    if ($isNumericCap) {
        $failures += "P4 $($r.Name): Contest magnitude ($($r.UnitClass)) carries a numeric cap $($r.Cap) — a capped defender half is a progression ceiling (PS-8)"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "STAT-PAIRS GUARD FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "STAT-PAIRS GUARD OK — every Contest paired, every pair symmetric, no Race paired, no capped Contest magnitude"
exit 0
