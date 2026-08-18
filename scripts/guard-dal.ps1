# Guard: SQLite / SQL only inside FusionRpg.Data
# Usage (repo root): .\scripts\guard-dal.ps1
# Live in deploy-play.ps1 (Slice E / W11) and Guard.Tests — empty allowlist.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string[]]$AllowlistFiles = @()
)

$ErrorActionPreference = "Stop"
$DataDir = Join-Path $Root "src\FusionRpg.Data"
$Src = Join-Path $Root "src"

if (-not (Test-Path $DataDir)) {
    throw "FusionRpg.Data missing: $DataDir — DAL project required"
}

$codePatterns = @(
    'Microsoft\.Data\.Sqlite',
    'SqliteConnection',
    'SqliteCommand',
    'SqliteTransaction',
    'PRAGMA\s+',
    'CREATE\s+TABLE',
    'INSERT\s+INTO',
    'BEGIN\s+IMMEDIATE'
)

$failures = @()

Get-ChildItem -Path $Src -Recurse -Filter "*.cs" | ForEach-Object {
    $full = $_.FullName
    if ($full.StartsWith($DataDir, [StringComparison]::OrdinalIgnoreCase)) { return }
    # Generated build output (WPF .g.cs etc.) is not product source
    if ($full -match '[\\/](obj|bin)[\\/]') { return }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    $name = $_.Name
    if ($AllowlistFiles -contains $name) { return }
    $text = Get-Content -LiteralPath $full -Raw
    foreach ($pat in $codePatterns) {
        if ([regex]::IsMatch($text, $pat, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
            $failures += "${rel}: matches /$pat/"
        }
    }
}

Get-ChildItem -Path $Src -Recurse -Filter "*.csproj" | ForEach-Object {
    $full = $_.FullName
    if ($full.StartsWith($DataDir, [StringComparison]::OrdinalIgnoreCase)) { return }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    $text = Get-Content -LiteralPath $full -Raw
    if ($text -match 'Microsoft\.Data\.Sqlite') {
        $failures += "${rel}: PackageReference Microsoft.Data.Sqlite outside FusionRpg.Data"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "DAL GUARD FAILED — database access outside FusionRpg.Data:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host "DAL GUARD OK — no SQLite/SQL outside FusionRpg.Data"
exit 0
