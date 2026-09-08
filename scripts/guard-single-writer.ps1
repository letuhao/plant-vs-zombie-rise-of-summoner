# Guard: combat HP/ATK field assignments must live only in EntityStatWriter.cs
# Usage (repo root): .\scripts\guard-single-writer.ps1
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$Injector = Join-Path $Root "src\FusionRpg.Injector"
if (-not (Test-Path $Injector)) { throw "Injector path missing: $Injector" }

$patterns = @(
    'thePlantHealth\s*=',
    'thePlantMaxHealth\s*=',
    'theHealth\s*=',
    'theMaxHealth\s*=',
    '\.attackDamage\s*=',
    'theAttackDamage\s*=',
    'theFirstArmorHealth\s*=',
    'theFirstArmorMaxHealth\s*=',
    'theSecondArmorHealth\s*=',
    'theSecondArmorMaxHealth\s*=',
    # A-M2 lawn-reposition (spec-lawn-reposition.md §2) — the fifth Unity write path, position
    # instead of combat fields. Same single-writer shape, EntityPositionWriter.cs is its file.
    # (?!=) excludes `==` comparisons — LawnCoords.cs:118's `z.theZombieRow == row` is a read, not
    # a write, and the other three field/property reads (thePlantRow/thePlantColumn/.position) get
    # compared the same way elsewhere in the tree.
    'thePlantRow\s*=(?!=)',
    'thePlantColumn\s*=(?!=)',
    'theZombieRow\s*=(?!=)',
    'transform\.position\s*=(?!=)',
    '\.localPosition\s*=(?!=)'
)

$allowed = @(
    "EntityStatWriter.cs",
    "ZombieCombatFields.cs",  # version bridges — only HP width adapters; Writer still owns policy
    "UniqueBoundLoadout.cs",  # W5 ptr-only Bound apply; EntityStatWriter.ForceSet* still follows
    "EntityPositionWriter.cs" # A-M2 lawn-reposition — sole Plant/Zombie transform/cell-field writer
)

$failures = @()
Get-ChildItem -Path $Injector -Recurse -Filter "*.cs" | ForEach-Object {
    $name = $_.Name
    if ($allowed -contains $name) { return }
    if ($_.FullName -match '[\\/]Bridges[\\/]') { return }
    # Fx/ — VFX GameObjects, not actors: AuraPool.cs:80,117 and BurstPool.cs:57 move particle-system
    # leases (transform.position), never a Plant/Zombie (spec-lawn-reposition.md §2).
    if ($_.FullName -match '[\\/]Fx[\\/]') { return }
    # Hud/ — HUD objects, not actors: ActorHudPool.cs:170,225,243 position the floating HUD root and
    # its row quads (transform.position / .localPosition), never a Plant/Zombie
    # (spec-lawn-reposition.md §2 — the ADR's original enumeration missed this one; see decisions.md:105).
    if ($_.FullName -match '[\\/]Hud[\\/]') { return }
    $text = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($pat in $patterns) {
        if ([regex]::IsMatch($text, $pat)) {
            $rel = $_.FullName.Substring($Root.Length).TrimStart('\', '/')
            $failures += "${rel}: matches /$pat/"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "SINGLE-WRITER GUARD FAILED — combat field writes outside EntityStatWriter:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "SINGLE-WRITER GUARD OK — no combat field writes outside EntityStatWriter.cs"
exit 0
