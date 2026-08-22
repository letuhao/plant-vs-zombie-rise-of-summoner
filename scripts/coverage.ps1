<#
.SYNOPSIS
  Line and branch coverage for one namespace, as a table you can read.

.DESCRIPTION
  Runs a test project with coverlet and reports per-class coverage for classes whose full name
  starts with -Namespace. Sorted worst-first, because the only rows worth reading are the low ones.

  Coverage is a *floor*, not a score. A line can be covered by a test that asserts nothing, which is
  why this ships alongside scripts/mutate.ps1 — coverage says what the tests touched, mutation says
  what they would notice. Chase mutation survivors; use coverage to find the code no test reaches at
  all, which is the cheaper of the two problems to find.

.EXAMPLE
  .\scripts\coverage.ps1 -Namespace FusionRpg.Core.World
  .\scripts\coverage.ps1 -Namespace FusionRpg.Core.World.Ai -Filter "FullyQualifiedName~World.Ai"
  .\scripts\coverage.ps1 -Project tests\FusionRpg.Data.Tests -Namespace FusionRpg.Data -Threshold 60
#>
[CmdletBinding()]
param(
    [string]$Project = "tests\FusionRpg.Core.Tests",
    [string]$Namespace = "FusionRpg.Core.World",

    # Narrow the test run itself. Leave empty to run every test in the project, which is the honest
    # number: a filtered run credits only what those tests reach.
    [string]$Filter = "",

    # Timing assertions cannot survive instrumentation: coverlet rewrites every sequence point, so a
    # test asserting nanoseconds-per-atom fails under coverage and passes without it. Excluded by
    # default rather than left to look like a regression. Pass -IncludeTimingTests to see them fail.
    [switch]$IncludeTimingTests,

    # Exit non-zero when any class falls below this line coverage. 0 disables the gate.
    [int]$Threshold = 0
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $results = Join-Path $repo "$Project\TestResults"
    if (Test-Path $results) { Remove-Item $results -Recurse -Force }

    $clauses = @()
    if ($Filter) { $clauses += $Filter }
    if (-not $IncludeTimingTests) { $clauses += "FullyQualifiedName!~Bench" }

    $testArgs = @("test", $Project, "--collect:XPlat Code Coverage", "--nologo", "-v", "q")
    if ($clauses) { $testArgs += @("--filter", ($clauses -join "&")) }

    Write-Host "running $Project$(if ($Filter) { " (filter: $Filter)" })..." -ForegroundColor DarkGray
    & dotnet @testArgs | Where-Object { $_ -match "^(Passed!|Failed!)" }
    if ($LASTEXITCODE -ne 0) { throw "tests failed - coverage of a red suite means nothing" }

    $report = Get-ChildItem $results -Recurse -Filter "coverage.cobertura.xml" |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $report) { throw "no coverage report produced" }

    [xml]$xml = Get-Content $report.FullName

    $rows = foreach ($class in $xml.coverage.packages.package.classes.class) {
        if ($class.name -notlike "$Namespace*") { continue }

        # A class with no branches reports branch-rate 0, which reads as a failure rather than as
        # "there was nothing to branch on". Report it as n/a instead.
        $branches = @($class.lines.line | Where-Object { $_.branch -eq "True" }).Count

        [pscustomobject]@{
            Class  = $class.name -replace "^$([regex]::Escape($Namespace))\.?", ""
            Line   = [math]::Round([double]$class.'line-rate' * 100)
            Branch = if ($branches -gt 0) { [math]::Round([double]$class.'branch-rate' * 100) } else { $null }
            Lines  = @($class.lines.line).Count
        }
    }

    if (-not $rows) { throw "no classes matched namespace '$Namespace'" }

    $rows = $rows | Sort-Object Line, Class
    $rows | Format-Table @{ N = "Class"; E = { $_.Class }; Width = 46 },
                         @{ N = "Line%"; E = { $_.Line }; Align = "right" },
                         @{ N = "Branch%"; E = { if ($null -eq $_.Branch) { "n/a" } else { $_.Branch } }; Align = "right" },
                         @{ N = "Lines"; E = { $_.Lines }; Align = "right" }

    $total = ($rows | Measure-Object Lines -Sum).Sum
    $covered = ($rows | ForEach-Object { $_.Lines * $_.Line / 100 } | Measure-Object -Sum).Sum
    Write-Host ("{0}: {1}% of {2} lines across {3} classes" -f
        $Namespace, [math]::Round($covered / $total * 100), $total, $rows.Count) -ForegroundColor Cyan

    if ($Threshold -gt 0) {
        $under = $rows | Where-Object { $_.Line -lt $Threshold }
        if ($under) {
            Write-Host "below the $Threshold% floor:" -ForegroundColor Yellow
            $under | ForEach-Object { Write-Host ("  {0} ({1}%)" -f $_.Class, $_.Line) }
            exit 1
        }
    }
}
finally { Pop-Location }
