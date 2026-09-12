# Default test profile — the dev/agent loop. Runs the store test suite MINUS the file-bound
# (`DiskSemantics`) and long-running (`Heavy`) tests, so a routine run writes nothing to the SSD and
# runs no multi-minute case. This is the ONE place that owns the default filter, so the default
# cannot drift between a developer and an agent. Standard: docs/contributing/testing-standard.md.
# Usage (repo root):
#   .\scripts\test-fast.ps1                                             # the 4 store projects
#   .\scripts\test-fast.ps1 -Project tests/FusionRpg.Data.Tests         # a directory
#   .\scripts\test-fast.ps1 -Project tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj
#   .\scripts\test-fast.ps1 -Project tests/FusionRpg.Data.Tests, tests/FusionRpg.Server.Tests
# The `full` profile (CI, nightly, release gate) is the same call with NO --filter:
#   dotnet test <proj> -c Release --blame-hang --blame-hang-timeout 15min
param(
    [string[]]$Project = @(),
    [string]$Configuration = "Release",
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

# The default profile: everything except the two excluded categories. A negative filter includes
# uncategorized tests (verified — docs/architecture/data-test-substrate/spec-test-profiles.md §1), so
# only the excluded tests carry a trait.
$Filter = "Category!=DiskSemantics&Category!=Heavy"

# No -Project: the four store test projects (Data first — it holds the majority of store sites).
$DefaultProjects = @(
    "tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj",
    "tests/FusionRpg.Server.Tests/FusionRpg.Server.Tests.csproj",
    "tests/FusionRpg.E2E.Tests/FusionRpg.E2E.Tests.csproj",
    "tests/FusionRpg.Core.Tests/FusionRpg.Core.Tests.csproj"
)

# Accept a .csproj path OR its directory (the spec's own example passes a directory).
function Resolve-TestProject {
    param([string]$Path)
    $full = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $Root $Path }
    if (Test-Path -LiteralPath $full -PathType Container) {
        $proj = Get-ChildItem -LiteralPath $full -Filter *.csproj -File | Select-Object -First 1
        if (-not $proj) { throw "no .csproj found in $full" }
        return $proj.FullName
    }
    if (-not (Test-Path -LiteralPath $full)) { throw "test project not found: $full" }
    return (Resolve-Path -LiteralPath $full).Path
}

$requested = if ($Project.Count -gt 0) { $Project } else { $DefaultProjects }
$projects = @($requested | ForEach-Object { Resolve-TestProject -Path $_ })

Write-Host "==> Test profile: default (fast)"
Write-Host "    filter: $Filter"
Write-Host "    projects:"
$projects | ForEach-Object { Write-Host "      $_" }

$exitCode = 0
foreach ($proj in $projects) {
    Write-Host ""
    Write-Host "==> dotnet test $proj -c $Configuration --filter `"$Filter`""
    dotnet test $proj -c $Configuration --verbosity minimal --filter $Filter --blame-hang --blame-hang-timeout 15min
    if ($LASTEXITCODE -ne 0) {
        Write-Host "DEFAULT PROFILE TEST FAILED: $proj" -ForegroundColor Red
        if ($exitCode -eq 0) { $exitCode = $LASTEXITCODE }
    }
}

if ($exitCode -ne 0) {
    Write-Host ""
    Write-Host "Default test profile failed (filter: $Filter). Standard: docs/contributing/testing-standard.md" -ForegroundColor Red
    exit $exitCode
}

Write-Host ""
Write-Host "Default test profile OK (filter: $Filter)" -ForegroundColor Green
exit 0
