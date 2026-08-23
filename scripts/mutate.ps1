<#
.SYNOPSIS
  Breaks the code on purpose and reports which tests failed to notice.

.DESCRIPTION
  Coverage says what the tests *touched*; mutation says what they would *notice*. A line covered by
  a test that asserts nothing is 100% covered and worth nothing, and that has happened twice in this
  repo already — see docs/research/world/mutation-pass-2026-08-22.md.

  Each mutant is one deliberate defect: a swapped constant, a dropped guard, a wrong lens. The suite
  runs against it and the mutant is either **caught** (some test failed, good) or it **SURVIVED**
  (every test passed while the code was wrong, which is a hole).

  Mutants live in scripts/mutants/*.json so adding one is a data edit:

      [ { "file":  "src/FusionRpg.Core/World/Ai/Hops.cs",
          "name":  "every reachable sector is one hop away",
          "find":  "distance[neighbour] = distance[current] + 1;",
          "with":  "distance[neighbour] = 1;" } ]

  A surviving mutant is not automatically a bug. Some code is unreachable through shipped content
  and some detail is deliberately not load-bearing — but each survivor has to be *explained*, and
  the explanation belongs in a comment next to the code so the next person does not re-find it.

.NOTES
  Restoring a mutated file gives it an older timestamp than the compiled output, so MSBuild keeps
  the **mutated** assembly and the next ordinary run fails against clean source. This script touches
  every file it restores. If it is killed mid-run, `git status` will show the mutation — the .bak
  beside it is the original.

  A mutant targeting a `.py` file runs under `python -m pytest tools\seedsmith\tests\ -q` instead
  of `dotnet test` — inferred from the file extension, never a schema field, so every existing
  `.cs` mutant set keeps running exactly as before. Python has no MSBuild timestamp trap, so the
  post-restore touch is skipped for it.

.EXAMPLE
  .\scripts\mutate.ps1 -Set world-ai
  .\scripts\mutate.ps1 -Set world-ai -Filter "FullyQualifiedName~World.Ai"
  .\scripts\mutate.ps1 -Set seedsmith
#>
[CmdletBinding()]
param(
    # Which scripts/mutants/<set>.json to run. Omit for every set.
    [string]$Set = "",

    [string]$Project = "tests\FusionRpg.Core.Tests",

    # Narrowing the suite makes the run quick and the verdict weaker: a mutant "caught" by a filtered
    # run is caught by *those* tests. Leave empty when the answer matters. Applies to the dotnet
    # runner only — pytest has no equivalent here, seedsmith's suite runs in full every time.
    [string]$Filter = ""
)

function Test-PythonMutant($mutant) {
    return $mutant.file -like "*.py"
}

function Invoke-DotnetSuite {
    $testArgs = @("test", $Project, "--nologo", "-v", "q")
    if ($Filter) { $testArgs += @("--filter", $Filter) }
    & dotnet @testArgs *> $null   # xunit reports failures on stderr; a mutant run is all noise
    return $LASTEXITCODE
}

function Invoke-PythonSuite {
    & python -m pytest "tools\seedsmith\tests" -q *> $null
    return $LASTEXITCODE
}

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $sets = Get-ChildItem (Join-Path $PSScriptRoot "mutants") -Filter "*.json" |
        Where-Object { -not $Set -or $_.BaseName -eq $Set }
    if (-not $sets) { throw "no mutant set matched '$Set'" }

    $allMutants = $sets | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json }
    $needsDotnet = @($allMutants | Where-Object { -not (Test-PythonMutant $_) }).Count -gt 0
    $needsPython = @($allMutants | Where-Object { Test-PythonMutant $_ }).Count -gt 0

    # A red baseline makes the whole run meaningless: every mutant would report "caught" because the
    # suite already fails, and a compile error in somebody else's file looks exactly like a test
    # noticing the defect. Check once per runner actually needed, up front, rather than reading a
    # page of false green.
    if ($needsDotnet) {
        Write-Host "checking the dotnet baseline is green..." -ForegroundColor DarkGray
        if ((Invoke-DotnetSuite) -ne 0) {
            throw "the dotnet suite is red before any mutant was applied - fix that first, or every mutant will look caught"
        }
    }
    if ($needsPython) {
        Write-Host "checking the seedsmith (pytest) baseline is green..." -ForegroundColor DarkGray
        if ((Invoke-PythonSuite) -ne 0) {
            throw "the seedsmith suite is red before any mutant was applied - fix that first, or every mutant will look caught"
        }
    }

    $survivors = @()
    $stale = @()

    foreach ($file in $sets) {
        Write-Host "`n$($file.BaseName)" -ForegroundColor Cyan

        foreach ($mutant in (Get-Content $file.FullName -Raw | ConvertFrom-Json)) {
            $target = Join-Path $repo $mutant.file
            $isPython = Test-PythonMutant $mutant

            # Normalised for matching: a multi-line anchor written with LF endings never matches a
            # CRLF file, and the mutant then reports STALE forever while looking like it ran.
            $source = (Get-Content $target -Raw) -replace "`r`n", "`n"
            $find = $mutant.find -replace "`r`n", "`n"

            if (-not $source.Contains($find)) {
                Write-Host ("  {0,-10} {1}" -f "STALE", $mutant.name) -ForegroundColor Yellow
                Write-Host "             anchor no longer in $($mutant.file) - the mutant needs rewriting"
                $stale += "$($file.BaseName): $($mutant.name)"
                continue
            }

            Copy-Item $target "$target.bak"
            try {
                $index = $source.IndexOf($find)
                $mutated = $source.Remove($index, $find.Length).Insert($index, ($mutant.with -replace "`r`n", "`n"))
                Set-Content $target $mutated -NoNewline

                $exitCode = if ($isPython) { Invoke-PythonSuite } else { Invoke-DotnetSuite }

                if ($exitCode -eq 0) {
                    Write-Host ("  {0,-10} {1}" -f "SURVIVED", $mutant.name) -ForegroundColor Red
                    $survivors += "$($file.BaseName): $($mutant.name)"
                }
                else {
                    Write-Host ("  {0,-10} {1}" -f "caught", $mutant.name) -ForegroundColor DarkGray
                }
            }
            finally {
                # Restore, then touch: an older timestamp leaves MSBuild holding the mutated build.
                # Python has no build step to fool, so the touch is skipped for it.
                Move-Item "$target.bak" $target -Force
                if (-not $isPython) {
                    (Get-Item $target).LastWriteTime = Get-Date
                }
            }
        }
    }

    Write-Host ""
    if ($stale) {
        # Louder than a warning, because a stale mutant is an *untested claim* wearing the colours of
        # a passing one. The first version of this script printed "every mutant was caught" while
        # three of them had never run.
        Write-Host "$($stale.Count) never ran - their anchors no longer match the code:" -ForegroundColor Yellow
        $stale | ForEach-Object { Write-Host "  $_" }
    }

    if ($survivors) {
        Write-Host "$($survivors.Count) survived - every test passed while the code was wrong:" -ForegroundColor Red
        $survivors | ForEach-Object { Write-Host "  $_" }
        exit 1
    }

    if ($stale) {
        Write-Host "every mutant that ran was caught, but $($stale.Count) did not run" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "every mutant was caught" -ForegroundColor Green
    # Explicit, not implicit: with no `exit` here, PowerShell's own exit code falls through to
    # whatever $LASTEXITCODE the last external process (dotnet/pytest, from the final mutant's
    # "caught" run — itself a NON-zero, test-failed exit, correctly) happened to leave behind.
    # A fully green mutation run reporting failure to a CI step reading the exit code is exactly
    # the silent-wrong-signal class this whole script exists to catch, just one layer up.
    exit 0
}
finally { Pop-Location }
