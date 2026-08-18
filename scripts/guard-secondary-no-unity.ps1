# Guard: Secondary IEffectGrantPlugin code must not call Unity / Status / Writer
# Usage (repo root): .\scripts\guard-secondary-no-unity.ps1
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$Src = Join-Path $Root "src"
$PluginDir = Join-Path $Src "FusionRpg.Core\Effects\Plugins"

$patterns = @(
    'UnityEngine',
    'HarmonyLib',
    'StatusExecutor',
    'EntityStatWriter',
    'FindObjectsOfType',
    'CreateZombie'
)

$failures = @()
$scanned = @{}

function Test-SecondaryFile([string]$full) {
    if ($script:scanned.ContainsKey($full)) { return }
    $script:scanned[$full] = $true
    if ($full -match '[\\/](obj|bin)[\\/]') { return }
    $text = Get-Content -LiteralPath $full -Raw
    if ([string]::IsNullOrEmpty($text)) { return }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    foreach ($pat in $patterns) {
        if ([regex]::IsMatch($text, $pat, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
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

$csproj = Join-Path $Src "FusionRpg.Core\FusionRpg.Core.csproj"
if (Test-Path $csproj) {
    $projText = Get-Content -LiteralPath $csproj -Raw
    if ([regex]::IsMatch($projText, 'UnityEngine', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $rel = $csproj.Substring($Root.Length).TrimStart('\', '/')
        $failures += "${rel}: references UnityEngine"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "SECONDARY NO-UNITY GUARD FAILED — Unity/Status/Writer refs in Secondary plugins:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "SECONDARY NO-UNITY GUARD OK — plugins Grant/Withdraw only"
exit 0
