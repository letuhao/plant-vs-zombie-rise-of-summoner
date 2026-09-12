<#
.SYNOPSIS
  Enforce the generated-seed hard rule: generated data is regenerated, never hand-edited.

.DESCRIPTION
  A tree whose entries carry generator provenance (_meta.model / promptVersion / batch) is the
  OUTPUT of seedsmith or a tools/*Gen program, not authored source (AGENTS.md / CLAUDE.md hard rule;
  docs/architecture/validation-ssot.md).

  This guard fails when a change set modifies a PROVENANCE-CARRYING file under a generated tree
  WITHOUT also touching that tree's generator, tuning, or registry. Editing the emitted row forks
  the corpus from its generator: the next run reverts it and the ledger stops describing the file.

  Sanctioned path it points you at: change the generator/tuning/registry, then regenerate.

  A file is only inspected when BOTH hold:
    - its path is under a generated tree (see $GeneratedTrees), and
    - its JSON carries generator provenance (so authored registries/tuning under the same root,
      e.g. data/seed/items/_registry/**, are correctly ignored).

  Usage (repo root):
    .\scripts\guard-generated-seed.ps1                     # working tree + staged vs HEAD
    .\scripts\guard-generated-seed.ps1 -BaseRef origin/main   # CI: branch diff vs a base
    .\scripts\guard-generated-seed.ps1 -Range a..b          # CI: an explicit commit range

  Exit 0 = clean, 1 = a generated file changed with no generator change.
#>
[CmdletBinding()]
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$BaseRef = "HEAD",
    [string]$Range
)

$ErrorActionPreference = "Stop"

# ---- generated tree -> the paths whose change legitimately re-emits it --------------------------
# `Generated` is the emitted output; `Sources` are the generator code plus the tuning/registry
# inputs the generator reads. Touching any Source is the sanctioned way to change Generated.
$Trees = @(
    @{ Generated = '^data/seed/items/';
       Sources   = @('^tools/seedsmith/seedsmith/adapters/items/',
                     '^tools/seedsmith/seedsmith/pipeline/',
                     '^tools/seedsmith/seedsmith/report/',
                     '^data/seed/items/_registry/',
                     '^data/seed/items/_tuning/',
                     '^data/seed/items/_seed/',
                     '^tools/ItemSeedValidator/') }
    @{ Generated = '^data/seed/actions/';
       Sources   = @('^tools/seedsmith/seedsmith/adapters/actions/',
                     '^tools/seedsmith/seedsmith/adapters/items/',
                     '^data/seed/actions/_registry/',
                     '^data/seed/actions/_generated/') }
    @{ Generated = '^data/seed/atoms/generated/';
       Sources   = @('^tools/FamilyExpandGen/',
                     '^src/FusionRpg.Core/Effects/Atoms/Generation/',
                     '^data/seed/channel-pools/',
                     '^data/seed/items/_registry/') }
    @{ Generated = '^data/generated/';
       Sources   = @('^tools/',
                     '^data/seed/',
                     '^data/tuning/') }
    @{ Generated = '^data/seed/passive-tree/';
       Sources   = @('^tools/seedsmith/seedsmith/adapters/trees/',
                     '^data/seed/passive-tree/_registry/') }
    @{ Generated = '^data/seed/creatures/';
       Sources   = @('^tools/seedsmith/seedsmith/adapters/creatures/') }
    @{ Generated = '^data/seed/dungeon/';
       Sources   = @('^tools/seedsmith/seedsmith/adapters/dungeon/') }
    @{ Generated = '^data/seed/structures/';
       Sources   = @('^tools/seedsmith/seedsmith/adapters/structures/') }
)

# Generator bookkeeping and provenance side-cars are never corpus content.
$IgnoredNamePatterns = @('\.ledger\.json$', '^data/seed/items/_runs/', '^data/seed/actions/_runs/',
                         '_meta\.json$', '/_index\.json$')

function Get-ChangedFiles {
    $files = New-Object System.Collections.Generic.List[string]
    if ($Range) {
        git -C $Root diff --name-only --diff-filter=ACMR $Range 2>$null |
            ForEach-Object { if ($_) { $files.Add(($_.Trim() -replace '\\', '/')) } }
    }
    else {
        git -C $Root diff --name-only --diff-filter=ACMR $BaseRef 2>$null |
            ForEach-Object { if ($_) { $files.Add(($_.Trim() -replace '\\', '/')) } }
        git -C $Root diff --cached --name-only --diff-filter=ACMR 2>$null |
            ForEach-Object { if ($_) { $files.Add(($_.Trim() -replace '\\', '/')) } }
        # untracked, non-ignored additions
        git -C $Root ls-files --others --exclude-standard 2>$null |
            ForEach-Object { if ($_) { $files.Add(($_.Trim() -replace '\\', '/')) } }
    }
    return $files | Sort-Object -Unique
}

function Test-HasGeneratorProvenance {
    param([string]$RelPath)
    $full = Join-Path $Root $RelPath
    if (-not (Test-Path -LiteralPath $full)) { return $false }
    try { $doc = Get-Content -LiteralPath $full -Raw | ConvertFrom-Json } catch { return $false }
    $meta = $doc._meta
    if ($null -eq $meta) { return $false }
    foreach ($k in @('model', 'promptVersion', 'batch')) {
        if ($meta.PSObject.Properties.Name -contains $k -and $meta.$k) { return $true }
    }
    return $false
}

$changed = @(Get-ChangedFiles)
if ($changed.Count -eq 0) {
    $scope = if ($Range) { $Range } else { $BaseRef }
    Write-Host "[guard-generated-seed] no changes vs $scope" -ForegroundColor Green
    exit 0
}

$violations = New-Object System.Collections.Generic.List[string]

foreach ($tree in $Trees) {
    $touchedGenerated = @($changed | Where-Object {
        $rel = $_
        if ($rel -notmatch $tree.Generated) { return $false }
        foreach ($ig in $IgnoredNamePatterns) { if ($rel -match $ig) { return $false } }
        return $true
    })

    if ($touchedGenerated.Count -eq 0) { continue }

    $sourceTouched = $false
    foreach ($s in $tree.Sources) {
        if ($changed | Where-Object { $_ -match $s }) { $sourceTouched = $true; break }
    }
    if ($sourceTouched) { continue }

    foreach ($f in $touchedGenerated) {
        if (Test-HasGeneratorProvenance -RelPath $f) {
            $violations.Add($f)
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "[guard-generated-seed] BLOCKED: generated seed edited without its generator" -ForegroundColor Red
    Write-Host ""
    foreach ($v in $violations) { Write-Host "  ! $v" -ForegroundColor Red }
    Write-Host ""
    Write-Host "These files carry generator provenance (_meta.model / promptVersion / batch), so they" -ForegroundColor Yellow
    Write-Host "are OUTPUT, not authored source. Hand-editing them forks the corpus from its generator:" -ForegroundColor Yellow
    Write-Host "the next run reverts the edit and the run ledger stops describing the file." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Sanctioned path: change the generator / tuning / registry, then regenerate and commit" -ForegroundColor Yellow
    Write-Host "the re-emitted output. See AGENTS.md and CLAUDE.md, and the seedsmith skill." -ForegroundColor Yellow
    exit 1
}

Write-Host "[guard-generated-seed] clean ($($changed.Count) changed file(s) inspected)" -ForegroundColor Green
exit 0
