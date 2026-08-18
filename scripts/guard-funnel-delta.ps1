# Guard: Secondary IEffectGrantPlugin must not TakeDamage / SetHp / Bag.Grant.
# Combat SSOT: Core must not call EntityStatWriter; injector HP Add stays in FA10 sink.
# Usage (repo root): .\scripts\guard-funnel-delta.ps1
# Does not scan EffectFunnel.cs (rejection strings would false-positive).
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$Src = Join-Path $Root "src"
$PluginDir = Join-Path $Src "FusionRpg.Core\Effects\Plugins"

$patterns = @(
    'TakeDamage',
    'SetHp',
    'thePlantHealth\s*=',
    'theHealth\s*=',
    'Bag\.Grant',
    'ctx\.Bag\.Grant'
)

$failures = @()
$scanned = @{}

function Test-SecondaryFile([string]$full) {
    if ($script:scanned.ContainsKey($full)) { return }
    $script:scanned[$full] = $true
    if ($full -match '[\\/](obj|bin)[\\/]') { return }
    $name = [System.IO.Path]::GetFileName($full)
    if ($name -eq "EffectFunnel.cs") { return }
    $text = Get-Content -LiteralPath $full -Raw
    if ([string]::IsNullOrEmpty($text)) { return }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    foreach ($pat in $patterns) {
        if ([regex]::IsMatch($text, $pat)) {
            $script:failures += "${rel}: matches /$pat/"
        }
    }
}

if (Test-Path $PluginDir) {
    Get-ChildItem -Path $PluginDir -Recurse -Filter "*.cs" | ForEach-Object {
        Test-SecondaryFile $_.FullName
    }
}

if (Test-Path $Src) {
    Get-ChildItem -Path $Src -Recurse -Filter "*.cs" | ForEach-Object {
        $full = $_.FullName
        if ($full -match '[\\/](obj|bin)[\\/]') { return }
        $text = Get-Content -LiteralPath $full -Raw
        if ([string]::IsNullOrEmpty($text)) { return }
        if ($text -match ':\s*.*IEffectGrantPlugin') {
            Test-SecondaryFile $full
        }
    }
}

$coreDir = Join-Path $Src "FusionRpg.Core"
$coreWriterPatterns = @(
    'EntityStatWriter',
    'AddPlantHp',
    'AddZombieHp',
    'targetPtrs'
)
if (Test-Path $coreDir) {
    Get-ChildItem -Path $coreDir -Recurse -Filter "*.cs" | ForEach-Object {
        $full = $_.FullName
        if ($full -match '[\\/](obj|bin)[\\/]') { return }
        $name = [System.IO.Path]::GetFileName($full)
        if ($name -eq "EffectFunnel.cs") { return }
        $text = Get-Content -LiteralPath $full -Raw
        if ([string]::IsNullOrEmpty($text)) { return }
        $rel = $full.Substring($Root.Length).TrimStart('\', '/')
        foreach ($pat in $coreWriterPatterns) {
            if ([regex]::IsMatch($text, $pat)) {
                $script:failures += "${rel}: matches /$pat/ (Core must fan-out via CombatDamageDispatcher + Funnel)"
            }
        }
    }
}

$injectorDir = Join-Path $Src "FusionRpg.Injector"
$writerAllow = @{
    "EntityStatWriter.cs" = $true
    "InjectorEffectActionSink.cs" = $true
}
$injectorWriterPatterns = @(
    'EntityStatWriter\.AddPlantHp',
    'EntityStatWriter\.AddZombieHp',
    'targetPtrs'
)
if (Test-Path $injectorDir) {
    Get-ChildItem -Path $injectorDir -Recurse -Filter "*.cs" | ForEach-Object {
        $full = $_.FullName
        if ($full -match '[\\/](obj|bin)[\\/]') { return }
        $name = [System.IO.Path]::GetFileName($full)
        if ($writerAllow.ContainsKey($name)) { return }
        $text = Get-Content -LiteralPath $full -Raw
        if ([string]::IsNullOrEmpty($text)) { return }
        $rel = $full.Substring($Root.Length).TrimStart('\', '/')
        foreach ($pat in $injectorWriterPatterns) {
            if ([regex]::IsMatch($text, $pat)) {
                $script:failures += "${rel}: matches /$pat/ (HP Add only via FA10 sink / dispatcher fan-out)"
            }
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host "FUNNEL DELTA GUARD FAILED — Secondary TakeDamage/SetHp/Bag.Grant or multi-ptr Writer:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "FUNNEL DELTA GUARD OK — Secondary enqueue via Funnel only"
exit 0
