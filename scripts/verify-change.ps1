param(
    [string[]]$Paths = @(),
    [string[]]$DeletedPaths = @(),
    [switch]$PlanOnly,
    [switch]$AllowUnscoped,
    [ValidateSet("text", "json")][string]$Format = "text",
    [string]$Session,
    [string]$Root
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
} else {
    $Root = (Resolve-Path -LiteralPath $Root).Path
}
$integrityScript = Join-Path $Root "scripts\guard-verification-boundaries.ps1"
if (-not (Test-Path -LiteralPath $integrityScript -PathType Leaf)) { throw "verification integrity guard missing: $integrityScript" }
$integrityOutput = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $integrityScript -Root $Root 2>&1)
$integrityExitCode = $LASTEXITCODE
if ($integrityExitCode -ne 0) {
    $integrityOutput | ForEach-Object { Write-Host $_ }
    throw "verification registry integrity guard failed"
}
$registryPath = Join-Path $Root "scripts\verification-boundaries.v1.json"
if (-not (Test-Path -LiteralPath $registryPath)) { throw "verification registry missing: $registryPath" }
$registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
if ($registry.schemaVersion -ne 1) { throw "unsupported verification registry schema" }

function Normalize-Path([string]$path, [bool]$allowMissing) {
    if ([IO.Path]::IsPathRooted($path) -or $path -match '(^|[\\/])\.\.([\\/]|$)') { throw "path must be repository-relative without traversal: $path" }
    $normalized = $path.Replace('\', '/').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($normalized)) { throw "path is empty" }
    $full = Join-Path $Root ($normalized.Replace('/', '\'))
    if (-not $allowMissing -and -not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "path does not exist: $normalized" }
    return $normalized
}
function Matches([string]$path, [string]$pattern) {
    if ($pattern.EndsWith('/**')) { return $path.StartsWith($pattern.Substring(0, $pattern.Length - 2), [StringComparison]::OrdinalIgnoreCase) }
    return $path.Equals($pattern, [StringComparison]::OrdinalIgnoreCase)
}
function Matches-SessionPath([string]$path, [string]$pattern) {
    if ([string]::IsNullOrWhiteSpace($pattern)) { return $false }
    return [System.Management.Automation.WildcardPattern]::new(
        $pattern.Replace('\', '/'),
        [System.Management.Automation.WildcardOptions]::IgnoreCase).IsMatch($path)
}

$normalized = @(
    @(
        $Paths | ForEach-Object { Normalize-Path $_ $false }
        $DeletedPaths | ForEach-Object { Normalize-Path $_ $true }
    ) | Sort-Object -Unique
)
if ($normalized.Count -eq 0) { throw "supply at least one repository-relative -Paths or -DeletedPaths value" }
if (-not $Session -and -not $AllowUnscoped) {
    throw "-Session is required for agent verification. Maintainers may explicitly pass -AllowUnscoped for read-only planning outside a session."
}
if ($Session) {
    if ($Session -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') { throw "session id is invalid: $Session" }
    $sessionPath = Join-Path $Root ("tasks\sessions\{0}.json" -f $Session)
    if (-not (Test-Path -LiteralPath $sessionPath -PathType Leaf)) { throw "session record not found: $Session" }
    try { $sessionRecord = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json }
    catch { throw "session record is invalid JSON: $Session" }
    if ($sessionRecord.status -ne 'active') { throw "session is not active: $Session" }
    $sessionPaths = @($sessionRecord.paths)
    if ($sessionPaths.Count -eq 0) { throw "session has no declared paths: $Session" }
    foreach ($path in $normalized) {
        if (@($sessionPaths | Where-Object { Matches-SessionPath $path $_ }).Count -eq 0) {
            throw "path is outside session scope ($Session): $path"
        }
    }
}
$selected = @()
foreach ($path in $normalized) {
    $hits = @(
        foreach ($boundary in $registry.boundaries | Where-Object { $_.kind -eq 'owner' }) {
            foreach ($pattern in @($boundary.paths | Where-Object { Matches $path $_ })) {
                [pscustomobject]@{ boundary = $boundary; specificity = $pattern.Length }
            }
        }
    )
    if ($hits.Count -eq 0) { throw "VERIFICATION BOUNDARY MISSING: $path. Add an owner mapping; do not run a broad suite as a fallback." }
    $highestSpecificity = ($hits | Measure-Object specificity -Maximum).Maximum
    $owners = @($hits | Where-Object { $_.specificity -eq $highestSpecificity } | ForEach-Object boundary | Sort-Object id -Unique)
    if ($owners.Count -ne 1) { throw "VERIFICATION BOUNDARY AMBIGUOUS: $path matches $($owners.id -join ', ') at specificity $highestSpecificity." }
    $owner = $owners[0]
    $selected += [pscustomobject]@{ path = $path; boundary = $owner.id; project = $owner.project; verificationId = $owner.verificationId; level = $owner.level; guards = @($owner.guards) }
    foreach ($seam in @($registry.boundaries | Where-Object { $_.kind -eq 'seam' -and @($_.paths | Where-Object { Matches $path $_ }).Count -gt 0 } | Sort-Object id)) {
        $selected += [pscustomobject]@{ path = $path; boundary = $seam.id; project = $seam.project; verificationId = $seam.verificationId; level = $seam.level; guards = @($seam.guards) }
    }
}
$selected = @($selected | Sort-Object path, boundary)
$checks = @()
foreach ($entry in $selected) {
    foreach ($guard in $entry.guards) { $checks += [pscustomobject]@{ kind = 'guard'; id = $guard; level = $entry.level; path = $entry.path } }
    $checks += [pscustomobject]@{ kind = 'test'; id = $entry.project; verificationId = $entry.verificationId; level = $entry.level; path = $entry.path }
}
$checks = @($checks | Sort-Object kind, id, verificationId -Unique)
$plan = [pscustomobject]@{ paths = $normalized; selections = $selected; checks = $checks; fullEvidenceOwner = 'CI/nightly/release' }
if ($Format -eq 'json') { $plan | ConvertTo-Json -Depth 8 } else {
    Write-Host "Verification plan:"
    $selected | ForEach-Object { Write-Host "  $($_.path) -> $($_.boundary) ($($_.level))" }
    $checks | ForEach-Object { Write-Host "  $($_.kind): $($_.id) $($_.verificationId)" }
    Write-Host "  full evidence: CI/nightly/release"
}
if ($PlanOnly) { exit 0 }
foreach ($check in $checks) {
    if ($check.kind -eq 'guard') {
        $script = $registry.guards.($check.id)
        if (-not $script) { throw "unknown guard: $($check.id)" }
        & (Join-Path $Root $script)
    } else {
        $project = $registry.projects.($check.id)
        if (-not $project) { throw "unknown project: $($check.id)" }
        $filter = if ($check.verificationId) { "VerificationId=$($check.verificationId)" } else { "Category!=DiskSemantics&Category!=Heavy" }
        dotnet test (Join-Path $Root $project) -c Release --verbosity minimal --filter $filter
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
