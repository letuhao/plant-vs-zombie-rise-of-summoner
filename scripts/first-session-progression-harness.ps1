param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

Write-Host 'First-session progression focused harness'
Write-Host 'Fresh SQLite stores are created by the test fixtures; no direct SQL is issued here.'

$dotnetArgs = @('test', 'tests/FusionRpg.Data.Tests', '--filter', 'FullyQualifiedName~Onboarding')
if ($NoBuild) { $dotnetArgs += '--no-build' }
& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { throw "onboarding Data harness failed ($LASTEXITCODE)" }

$serverArgs = @('test', 'tests/FusionRpg.Server.Tests', '--filter', 'FullyQualifiedName~OnboardingEndpointsTests')
if ($NoBuild) { $serverArgs += '--no-build' }
& dotnet @serverArgs
if ($LASTEXITCODE -ne 0) { throw "onboarding HTTP harness failed ($LASTEXITCODE)" }

Write-Host 'Evidence covered: fresh Player 1, settled victory, species reveal, deterministic Dave item,
replay/idempotency, GET projection, and acknowledgement-only claim.'
