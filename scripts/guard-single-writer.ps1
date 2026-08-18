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
    'theSecondArmorMaxHealth\s*='
)

$allowed = @(
    "EntityStatWriter.cs",
    "ZombieCombatFields.cs",  # version bridges — only HP width adapters; Writer still owns policy
    "UniqueBoundLoadout.cs"   # W5 ptr-only Bound apply; EntityStatWriter.ForceSet* still follows
)

$failures = @()
Get-ChildItem -Path $Injector -Recurse -Filter "*.cs" | ForEach-Object {
    $name = $_.Name
    if ($allowed -contains $name) { return }
    if ($_.FullName -match '[\\/]Bridges[\\/]') { return }
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
