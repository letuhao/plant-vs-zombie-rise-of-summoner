# Guard: the power ladder stays the one power ladder (spec-power-guard.md, T4.1)
# Usage (repo root): .\scripts\guard-power.ps1
# Live in deploy-play.ps1 and Guard.Tests — empty allowlists by default, each entry reasoned.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string[]]$G1AllowlistFiles = @(),
    # False-positive survey (T4.1, 2026-08-24, spec-power-guard.md §5): G2's heuristic (a level/lvl/
    # index-named parameter with arithmetic on it) necessarily over-matches. Two real hits, each
    # reasoned, not silently swallowed:
    #  - PatronPolicy.cs: AuraMilli(rarity, star, level) -- `level` is the PATRON DEMON's own level, a
    #    different axis from the actor's Theta; added to ssot-power-scale.md S10.2 row 16 and
    #    inventory.json (so G3 also passes here, on its own separate check).
    #  - RpgProgression.cs: XpToNext/TotalToReach(kind, level) -- the XP COST ladder (SSOT S10.1 row 6,
    #    "kept, unchanged... it is the cost ladder, not a power ladder"), already in inventory.json so
    #    G3 already passes; G2's own list is separate and needs the same file named here too.
    [string[]]$G2AllowlistFiles = @("PatronPolicy.cs", "RpgProgression.cs")
)

$ErrorActionPreference = "Stop"
$Src = Join-Path $Root "src"
$PowerDir = Join-Path $Src "FusionRpg.Core\Power"
$InventoryPath = Join-Path $Root "docs\architecture\power\inventory.json"

if (-not (Test-Path $PowerDir)) { throw "Core/Power missing: $PowerDir" }
if (-not (Test-Path $InventoryPath)) { throw "inventory.json missing: $InventoryPath" }

function Get-CodeLines([string]$path) {
    # Comment-only lines are BLANKED, not removed (matches guard-dal.ps1's convention of skipping
    # comments for the regex check) — blanking, rather than dropping, keeps every returned line's
    # ARRAY INDEX equal to its real 1-based line number in the file, which both call sites below rely
    # on to report an accurate file:line rather than a position in a comment-stripped, re-flowed blob.
    Get-Content -LiteralPath $path | ForEach-Object {
        if ($_.TrimStart() -match '^(//|\*|/\*)') { "" } else { $_ }
    }
}

$failures = @()

# ---- G1: no literal curve — Core/Power's own C/A/B/pin fields must come from PowerTuningLoader, --
# never a bare literal anywhere else in the directory. PowerTuning.cs is exempt too: the three
# FixedC/PinIndex/PinValue anchor consts legitimately live there BY DESIGN (an ask-first ADR, not a
# tuning edit — PowerTuning.cs's own doc comment), and Build()'s belt-and-braces re-derivation is
# structural verification math, not a second curve.
$g1Exempt = @("PowerTuningLoader.cs", "PowerTuning.cs") + $G1AllowlistFiles
$curveFieldPattern = '\b(CMilli|AMilli|BMilli|PinIndex|PinValue)\s*[:=]\s*-?\d'

Get-ChildItem -Path $PowerDir -Filter "*.cs" | ForEach-Object {
    if ($g1Exempt -contains $_.Name) { return }
    $rel = $_.FullName.Substring($Root.Length).TrimStart('\', '/')
    $codeLines = @(Get-CodeLines $_.FullName)
    for ($i = 0; $i -lt $codeLines.Count; $i++) {
        if ($codeLines[$i] -match $curveFieldPattern) {
            $failures += "G1 ${rel}:$($i + 1): literal curve field outside PowerTuningLoader — $($codeLines[$i].Trim())"
        }
    }
}

# ---- G2/G3: no private f(level) — a method taking a level/lvl/index parameter, doing arithmetic on
# it, and returning a numeric type, outside Core/Power. G2 fails on anything not on its own allowlist
# (reasoned false positives). G3 fails separately when the SAME match's file is not represented in
# inventory.json's `location` column — a different, doc-linked source of truth (spec-power-guard.md
# §2.3) from the ad-hoc G2 allowlist.
$methodSigPattern = '(?im)^[ \t]*(public|internal|private|protected|static)[^(=;]*\b(int|long|double|float)\s+\w+\s*\([^)]*\b(level|lvl|index)\b[^)]*\)'
$inventory = Get-Content -LiteralPath $InventoryPath -Raw | ConvertFrom-Json
$inventoryLocations = @()
foreach ($row in $inventory.scales) {
    foreach ($loc in ($row.location -split ',\s*')) { $inventoryLocations += $loc.Trim() }
}

Get-ChildItem -Path $Src -Recurse -Filter "*.cs" | ForEach-Object {
    $full = $_.FullName
    if ($full -match '[\\/](obj|bin)[\\/]') { return }
    if ($full.StartsWith($PowerDir, [StringComparison]::OrdinalIgnoreCase)) { return }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    $relFwd = $rel.Replace('\', '/')

    $text = (Get-CodeLines $full) -join "`n"
    $matches = [regex]::Matches($text, $methodSigPattern)
    foreach ($m in $matches) {
        # Body-arithmetic check: within ~15 lines after the signature, the level/lvl/index parameter
        # name is combined with +, -, or * — the heuristic spec-power-guard.md §2.2 names explicitly,
        # over-matching by design, with the allowlist as the safety valve.
        $paramMatch = [regex]::Match($m.Value, '\b(level|lvl|index)\b')
        $paramName = $paramMatch.Value
        $startIdx = $m.Index
        $bodyWindow = $text.Substring($startIdx, [Math]::Min(600, $text.Length - $startIdx))
        if ($bodyWindow -notmatch [regex]::Escape($paramName) + '\s*[*+\-]' -and
            $bodyWindow -notmatch '[*+\-]\s*' + [regex]::Escape($paramName)) { continue }

        $lineNo = ($text.Substring(0, $startIdx) -split "`n").Count
        if (-not ($G2AllowlistFiles -contains $_.Name)) {
            $failures += "G2 ${rel}:${lineNo}: private f($paramName)-shaped method outside Core/Power"
        }
        if (-not ($inventoryLocations | Where-Object { $relFwd -eq $_ -or $relFwd.StartsWith($_) })) {
            $failures += "G3 ${rel}:${lineNo}: power-shaped method not listed in inventory.json"
        }
    }
}

# ---- G4: pin holds — every data/tuning/power-scale.v*.json must reproduce its own pinValue exactly,
# re-derived independently in PowerShell rather than trusted from the C# loader (this guard runs
# standalone, pre-build). Mirrors PowerTuning.Build's own belt-and-braces check (PowerTuning.cs).
# A repo (or a test fixture) with no data\tuning directory at all has nothing for G4 to check —
# distinct from having the directory but zero matching files, which is also legal and checks nothing.
$tuningDir = Join-Path $Root "data\tuning"
$tuningFiles = if (Test-Path $tuningDir) { Get-ChildItem -Path $tuningDir -Filter "power-scale.v*.json" } else { @() }
$tuningFiles | ForEach-Object {
    $rel = $_.FullName.Substring($Root.Length).TrimStart('\', '/')
    $json = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    $curve = $json.curve
    $cMilli = [long]$curve.cMilli
    $bMilli = [long]$curve.bMilli
    $pinIndex = [long]$curve.pinIndex
    $pinValue = [long]$curve.pinValue

    if ($pinIndex -le 0) { $failures += "G4 ${rel}: pinIndex must be positive, got $pinIndex"; return }

    # Same halved-before-multiplying shape as PowerLadder.TriangularMilli — avoids forming the
    # un-halved product, which is the exact overflow PowerLadder.cs's own comment documents finding.
    if (($pinIndex % 2) -eq 0) { $half = $pinIndex / 2; $other = $pinIndex - 1 }
    else { $half = ($pinIndex - 1) / 2; $other = $pinIndex }
    $triangularMilli = $bMilli * $half * $other

    $numerator = $pinValue * 1000 - $cMilli - $triangularMilli
    if (($numerator % $pinIndex) -ne 0) {
        $failures += "G4 ${rel}: bMilli=$bMilli does not divide the pin exactly at pinIndex=$pinIndex"
        return
    }
    $aMilli = $numerator / $pinIndex
    $pinCheckMilli = $cMilli + $aMilli * $pinIndex + $triangularMilli
    if ($pinCheckMilli -ne ($pinValue * 1000)) {
        $failures += "G4 ${rel}: pin broken — P($pinIndex)*1000 = $pinCheckMilli, expected $($pinValue * 1000)"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "POWER GUARD FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "POWER GUARD OK — one ladder, pin holds, no private f(level)"
exit 0
