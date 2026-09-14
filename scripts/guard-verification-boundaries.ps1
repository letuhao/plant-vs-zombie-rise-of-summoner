param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path)
$ErrorActionPreference = "Stop"

function Has-OnlyFields($object, [string[]]$allowed, [string]$label) {
    foreach ($name in @($object.PSObject.Properties.Name)) {
        if ($allowed -notcontains $name) { $script:failures += "$label has unknown field: $name" }
    }
}
function Is-RelativeRegistryPath([string]$path) {
    return -not [string]::IsNullOrWhiteSpace($path) -and
        -not [IO.Path]::IsPathRooted($path) -and
        $path -notmatch '(^|[\\/])\.\.([\\/]|$)' -and
        $path -notmatch '\\' -and
        ($path -notmatch '\*' -or $path.EndsWith('/**'))
}
function Matches([string]$path, [string]$pattern) {
    if ($pattern.EndsWith('/**')) { return $path.StartsWith($pattern.Substring(0, $pattern.Length - 2), [StringComparison]::OrdinalIgnoreCase) }
    return $path.Equals($pattern, [StringComparison]::OrdinalIgnoreCase)
}

$Root = (Resolve-Path -LiteralPath $Root).Path
$path = Join-Path $Root "scripts\verification-boundaries.v1.json"
try { $doc = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json }
catch { Write-Host "VERIFICATION BOUNDARY GUARD FAILED: invalid registry"; exit 1 }

$failures = @()
Has-OnlyFields $doc @('schemaVersion', 'projects', 'guards', 'boundaries') 'registry'
if ($doc.schemaVersion -ne 1) { $failures += 'unsupported schemaVersion' }
if ($null -eq $doc.projects -or $null -eq $doc.guards -or $null -eq $doc.boundaries) { $failures += 'registry requires projects, guards, and boundaries' }

foreach ($property in @($doc.projects.PSObject.Properties)) {
    if ($property.Name -notmatch '^[a-z][a-z0-9-]*$') { $failures += "invalid project id: $($property.Name)"; continue }
    if (-not (Is-RelativeRegistryPath ([string]$property.Value))) { $failures += "invalid project path: $($property.Name)"; continue }
    if (-not (Test-Path -LiteralPath (Join-Path $Root $property.Value) -PathType Leaf)) { $failures += "project file missing: $($property.Name)" }
}
foreach ($property in @($doc.guards.PSObject.Properties)) {
    if ($property.Name -notmatch '^[a-z][a-z0-9-]*$') { $failures += "invalid guard id: $($property.Name)"; continue }
    if (-not (Is-RelativeRegistryPath ([string]$property.Value))) { $failures += "invalid guard path: $($property.Name)"; continue }
    if (-not (Test-Path -LiteralPath (Join-Path $Root $property.Value) -PathType Leaf)) { $failures += "guard file missing: $($property.Name)" }
}

$ids = @{}
$ownerPatterns = @{}
foreach ($boundary in @($doc.boundaries)) {
    Has-OnlyFields $boundary @('id', 'kind', 'paths', 'project', 'verificationId', 'guards', 'level') "boundary '$($boundary.id)'"
    if ($boundary.id -notmatch '^[a-z][a-z0-9-]*$') { $failures += "invalid boundary id: $($boundary.id)" }
    if ($ids.ContainsKey($boundary.id)) { $failures += "duplicate boundary id: $($boundary.id)" }
    $ids[$boundary.id] = $true
    if (@('owner', 'seam') -notcontains $boundary.kind) { $failures += "unsupported boundary kind: $($boundary.id)" }
    if (@($boundary.paths).Count -eq 0) { $failures += "boundary has no paths: $($boundary.id)" }
    foreach ($pattern in @($boundary.paths)) {
        if (-not (Is-RelativeRegistryPath ([string]$pattern))) { $failures += "invalid boundary path: $($boundary.id): $pattern" }
        if ($boundary.kind -eq 'owner') {
            $key = ([string]$pattern).ToLowerInvariant()
            if ($ownerPatterns.ContainsKey($key)) { $failures += "ambiguous owner pattern: $pattern ($($ownerPatterns[$key]), $($boundary.id))" }
            $ownerPatterns[$key] = $boundary.id
        }
    }
    if (-not $doc.projects.($boundary.project)) { $failures += "unknown project: $($boundary.project)" }
    if (@('focused', 'module', 'seam') -notcontains $boundary.level) { $failures += "invalid evidence level: $($boundary.id)" }
    foreach ($guard in @($boundary.guards)) { if (-not $doc.guards.($guard)) { $failures += "unknown guard: $guard" } }
    if ($boundary.verificationId) {
        if ($boundary.verificationId -notmatch '^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$') { $failures += "invalid VerificationId: $($boundary.verificationId)"; continue }
        $projectPath = $doc.projects.($boundary.project)
        if ($projectPath) {
            $projectDirectory = Split-Path -Parent (Join-Path $Root $projectPath)
            $escaped = [regex]::Escape([string]$boundary.verificationId)
            $trait = '\[Trait\s*\(\s*"VerificationId"\s*,\s*"' + $escaped + '"\s*\)\]'
            if (@(Get-ChildItem -LiteralPath $projectDirectory -Recurse -Filter '*.cs' -File | Select-String -Pattern $trait).Count -eq 0) {
                $failures += "VerificationId has no matching test trait: $($boundary.verificationId)"
            }
        }
    }
}

Get-ChildItem (Join-Path $Root 'src') -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | ForEach-Object {
    $rel = $_.FullName.Substring($Root.Length).TrimStart('\','/').Replace('\','/')
    $matches = @($doc.boundaries | Where-Object { $_.kind -eq 'owner' -and @($_.paths | Where-Object { Matches $rel $_ }).Count -gt 0 })
    if ($matches.Count -eq 0) { $failures += "unmapped source: $rel" }
}

if ($failures.Count) { Write-Host 'VERIFICATION BOUNDARY GUARD FAILED'; $failures | Select-Object -Unique | ForEach-Object { Write-Host "  $_" }; exit 1 }
Write-Host 'VERIFICATION BOUNDARY GUARD OK'
