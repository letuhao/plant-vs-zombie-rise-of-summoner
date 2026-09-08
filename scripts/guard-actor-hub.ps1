# Guard: player combat/derived compose must go through ActorHub — sole Hot gate (ADR 2026-09-07).
# Usage (repo root): .\scripts\guard-actor-hub.ps1
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"
$Src = Join-Path $Root "src"

$failures = @()

function Get-CodeLines([string]$text) {
    $out = New-Object System.Collections.Generic.List[string]
    foreach ($line in ($text -split "`r?`n")) {
        $t = $line.TrimStart()
        if ($t.StartsWith('//') -or $t.StartsWith('*') -or $t.StartsWith('/*')) { continue }
        $out.Add($line)
    }
    return ($out -join "`n")
}

# Server player GETs for derived/sheet must call ActorHub.Resolve*
$serverTargets = @(
    (Join-Path $Src "FusionRpg.Server\AuraDerivedEndpoints.cs"),
    (Join-Path $Src "FusionRpg.Server\UniqueActorHubCompose.cs")
)
foreach ($full in $serverTargets) {
    if (-not (Test-Path $full)) {
        $failures += "missing required file: $($full.Substring($Root.Length).TrimStart('\','/'))"
        continue
    }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    $code = Get-CodeLines (Get-Content -LiteralPath $full -Raw)
    if ($rel -match 'AuraDerivedEndpoints') {
        if ($code -notmatch 'UniqueActorHubCompose') {
            $failures += "${rel}: player /derived|/sheet must route through UniqueActorHubCompose (ActorHub)"
        }
        if ($code -notmatch 'composeKind') {
            $failures += "${rel}: /derived must ship composeKind (FlatReplace honesty)"
        }
    }
    if ($rel -match 'UniqueActorHubCompose') {
        if ($code -notmatch 'ActorHubBootstrap\.CreateDefault' -and $code -notmatch 'new ActorHub') {
            $failures += "${rel}: must construct ActorHub as sole compose gate"
        }
        if ($code -notmatch 'ResolveDerivedWithContributions') {
            $failures += "${rel}: sheet/derived must call ResolveDerivedWithContributions (GG-49)"
        }
        if ($code -notmatch 'EquippedBoundAtoms') {
            $failures += "${rel}: must fan in equip via EquippedBoundAtoms (shared with battle)"
        }
    }
}

# Ban new private *DerivedComposer* classes outside Core/Stats/Derived and Battle exception
$allowComposer = @(
    '[\\/]FusionRpg\.Core[\\/]Stats[\\/]Derived[\\/]',
    '[\\/]FusionRpg\.Core[\\/]Battle[\\/]BattleStatComposer\.cs',
    '[\\/]FusionRpg\.Core[\\/]Stats[\\/]PvzStatsSheetComposer\.cs',
    '[\\/]obj[\\/]',
    '[\\/]bin[\\/]'
)

if (Test-Path $Src) {
    Get-ChildItem -Path $Src -Recurse -Filter "*Composer*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
        $full = $_.FullName
        $rel = $full.Substring($Root.Length).TrimStart('\', '/')
        $allowed = $false
        foreach ($pat in $allowComposer) {
            if ($full -match $pat) { $allowed = $true; break }
        }
        if ($allowed) { return }
        $code = Get-CodeLines (Get-Content -LiteralPath $full -Raw)
        if ($code -match 'DerivedModifier|ActorDerivedSnapshot|ContributeDerived') {
            $failures += "${rel}: private derived composer outside ActorHub / BattleStatComposer allowlist"
        }
    }
}

# EntityApply / GameHooks / SimEngine must use ActorHub.Resolve
$hubRequired = @(
    @{ Path = "FusionRpg.Injector\Stats\EntityApply.cs"; Why = "Writer path must call ActorHub.Resolve" },
    @{ Path = "FusionRpg.Injector\GameHooks.cs"; Why = "damage-scale cache must call ActorHub.Resolve (not Stats.Resolve)" },
    @{ Path = "FusionRpg.Core\SimEngine.cs"; Why = "sim apply must call ActorHub.Resolve (not Stats.Resolve)" }
)
foreach ($req in $hubRequired) {
    $full = Join-Path $Src $req.Path
    if (-not (Test-Path $full)) {
        $failures += "missing required file: $($req.Path)"
        continue
    }
    $rel = $full.Substring($Root.Length).TrimStart('\', '/')
    $code = Get-CodeLines (Get-Content -LiteralPath $full -Raw)
    if ($code -notmatch 'ActorHub\.Resolve') {
        $failures += "${rel}: $($req.Why)"
    }
}

# Ban bare Stats.Resolve in Injector combat paths (allowlist: none today — contexts stay on factory)
$injectorDir = Join-Path $Src "FusionRpg.Injector"
$allowStatsResolve = @(
    '[\\/]obj[\\/]',
    '[\\/]bin[\\/]'
)
if (Test-Path $injectorDir) {
    Get-ChildItem -Path $injectorDir -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue | ForEach-Object {
        $full = $_.FullName
        foreach ($pat in $allowStatsResolve) {
            if ($full -match $pat) { return }
        }
        $rel = $full.Substring($Root.Length).TrimStart('\', '/')
        $code = Get-CodeLines (Get-Content -LiteralPath $full -Raw)
        if ($code -match '(?<![A-Za-z])Stats\.Resolve\s*\(') {
            $failures += "${rel}: Stats.Resolve bypasses ActorHub — use ActorHub.Resolve / AppliedCombat"
        }
    }
}

# SimEngine must not regress to Stats.Resolve for the four apply sites
$simEngine = Join-Path $Src "FusionRpg.Core\SimEngine.cs"
if (Test-Path $simEngine) {
    $rel = $simEngine.Substring($Root.Length).TrimStart('\', '/')
    $code = Get-CodeLines (Get-Content -LiteralPath $simEngine -Raw)
    if ($code -match '(?<![A-Za-z])Stats\.Resolve\s*\(') {
        $failures += "${rel}: Stats.Resolve bypasses ActorHub — use ActorHub.Resolve"
    }
}

# Program.cs battle equip must use EquippedBoundAtoms (role-tagged SourceIds)
$program = Join-Path $Src "FusionRpg.Server\Program.cs"
if (Test-Path $program) {
    $rel = $program.Substring($Root.Length).TrimStart('\', '/')
    $code = Get-CodeLines (Get-Content -LiteralPath $program -Raw)
    if ($code -match 'UseEquipment' -and $code -notmatch 'EquippedBoundAtoms') {
        $failures += "${rel}: BattleStatComposer.UseEquipment must use EquippedBoundAtoms.SourceFromStore"
    }
}

# StatusDerivedSubsystem must refuse empty SourceIds (GG-49 parity with AtomDerivedSubsystem)
$statusDerived = Join-Path $Src "FusionRpg.Core\Stats\Derived\Subsystems\StatusDerivedSubsystem.cs"
if (Test-Path $statusDerived) {
    $rel = $statusDerived.Substring($Root.Length).TrimStart('\', '/')
    $code = Get-CodeLines (Get-Content -LiteralPath $statusDerived -Raw)
    if ($code -notmatch 'IsNullOrWhiteSpace\(mod\.SourceId\)') {
        $failures += "${rel}: must skip empty SourceId (actor-hub-ssot §8.1)"
    }
}

# Debug actor-derived emit must ship contributions (inspect honesty)
$cheatRunner = Join-Path $Src "FusionRpg.Injector\CheatCommandRunner.cs"
if (Test-Path $cheatRunner) {
    $rel = $cheatRunner.Substring($Root.Length).TrimStart('\', '/')
    $code = Get-CodeLines (Get-Content -LiteralPath $cheatRunner -Raw)
    if ($code -match 'EmitActorDerived' -and $code -notmatch 'ResolveDerivedWithContributions') {
        $failures += "${rel}: EmitActorDerived must call ResolveDerivedWithContributions"
    }
}

if ($failures.Count -gt 0) {
    Write-Host "ACTOR-HUB GUARD FAILED" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "ACTOR-HUB GUARD OK" -ForegroundColor Green
exit 0
