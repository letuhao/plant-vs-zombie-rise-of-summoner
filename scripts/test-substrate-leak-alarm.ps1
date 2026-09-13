# Alarm: a test run must not survive a temp dir, and must not leave an rpg-*.sqlite behind.
# Usage (repo root):
#   .\scripts\test-substrate-leak-alarm.ps1 -Run { dotnet test tests/FusionRpg.Data.Tests -c Release }
#   .\scripts\test-substrate-leak-alarm.ps1 -Run { dotnet test tests/FusionRpg.Data.Tests -c Release --filter "FullyQualifiedName~AffixStoreTests" }
# Companion to the static gate scripts/guard-test-substrate.ps1 (module disk-write-probe, T19b):
# that one refuses a bad source pattern at authoring time; this one refuses a run that actually
# leaked — a temp dir or an rpg-*.sqlite the source pattern would not catch.
#
# Snapshot is a SET DIFF, never a count: a pre-existing dir is not read as a new leak, and an equal
# count does not pass while the membership differs. Assert the relationship (delta = 0), never a
# pinned total. Standard: docs/contributing/testing-standard.md; validation-ssot.md.
#
# SHARED-MACHINE FALSE POSITIVE (fixed 2026-09-12): on the OS temp root, a *concurrent* process on the
# same box (another agent, another worktree's test run) creates and removes its own `fusionrpg-*` dirs
# inside this alarm's before/after window, so they appear as "leaked" even though the wrapped run was
# innocent. Measured: a Server run of 26 tests reported ~108 survivors that all belonged to a
# concurrent Data.Tests run. Pass `-IsolateTemp` to give the wrapped run its own private temp root
# (TEMP/TMP redirected), which removes the cross-talk entirely; the alarm defaults to it OFF so the
# behavior is explicit and a CI (single-runner, ephemeral disk) invocation is unaffected.
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$TempRoot = [System.IO.Path]::GetTempPath(),
    [scriptblock]$Run,
    [switch]$IsolateTemp
)

$ErrorActionPreference = "Stop"
$TestsDir = Join-Path $Root "tests"

if (-not (Test-Path -LiteralPath $TestsDir)) {
    throw "tests/ missing: $TestsDir"
}

# Own the wrapped run's temp root when asked, so no other process can pollute the snapshot.
$ownedTemp = $null
$savedTemp = $null
$savedTmp = $null
if ($IsolateTemp) {
    $ownedTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("test-substrate-alarm-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $ownedTemp -Force | Out-Null
    $savedTemp = $env:TEMP
    $savedTmp = $env:TMP
    $env:TEMP = $ownedTemp
    $env:TMP = $ownedTemp
    $TempRoot = $ownedTemp
}

# The test temp root: directories whose name starts with fusionrpg- (the store/helper prefix).
# Returned as a name SET so before/after membership can be diffed. A pre-existing dir is never a leak.
function Get-TempDirSnapshot {
    $set = New-Object System.Collections.Generic.HashSet[string]
    Get-ChildItem -LiteralPath $TempRoot -Directory -Filter 'fusionrpg-*' -ErrorAction SilentlyContinue |
        ForEach-Object { [void]$set.Add($_.Name) }
    return ,$set
}

# rpg-*.sqlite under the test OUTPUT roots (each test project's bin/ + TestResults/, recursive) —
# this is where AppContext.BaseDirectory writes land for the file-bound classes. The source tree has
# no such file; a survivor here is a test that failed to clean up its store.
function Get-SqliteSnapshot {
    $set = New-Object System.Collections.Generic.HashSet[string]
    foreach ($proj in (Get-ChildItem -LiteralPath $TestsDir -Directory -ErrorAction SilentlyContinue)) {
        foreach ($name in @('bin', 'TestResults')) {
            $outRoot = Join-Path $proj.FullName $name
            if (-not (Test-Path -LiteralPath $outRoot)) { continue }
            Get-ChildItem -LiteralPath $outRoot -Recurse -File -Filter 'rpg-*.sqlite' -ErrorAction SilentlyContinue |
                ForEach-Object { [void]$set.Add($_.FullName) }
        }
    }
    return ,$set
}

function Get-NewMembers {
    param($Before, $After)
    $new = New-Object System.Collections.Generic.List[string]
    foreach ($x in $After) { if (-not $Before.Contains($x)) { $new.Add($x) } }
    $new.Sort()
    return ,$new
}

$beforeDirs = Get-TempDirSnapshot
$beforeSqlite = Get-SqliteSnapshot

$runExit = 0
if ($Run) {
    & $Run
    if ($null -ne $LASTEXITCODE) { $runExit = $LASTEXITCODE }
}

$afterDirs = Get-TempDirSnapshot
$afterSqlite = Get-SqliteSnapshot

# Restore the ambient temp env and remove this alarm's private root once it is empty (a survivor in
# it is a leak and is reported above, so only delete the root itself when nothing is left).
if ($IsolateTemp) {
    $env:TEMP = $savedTemp
    $env:TMP = $savedTmp
    if ($null -ne $ownedTemp -and (Test-Path -LiteralPath $ownedTemp)) {
        $left = Get-ChildItem -LiteralPath $ownedTemp -Force -ErrorAction SilentlyContinue
        if (-not $left) { Remove-Item -LiteralPath $ownedTemp -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

$leakedDirs = Get-NewMembers -Before $beforeDirs -After $afterDirs
$newSqlite = Get-NewMembers -Before $beforeSqlite -After $afterSqlite

if ($leakedDirs.Count -gt 0 -or $newSqlite.Count -gt 0 -or $runExit -ne 0) {
    Write-Host "TEST SUBSTRATE LEAK ALARM FAILED — the run did not clean up after itself:" -ForegroundColor Red
    foreach ($d in $leakedDirs) { Write-Host "  temp dir survived: $d" }
    foreach ($s in $newSqlite) { Write-Host "  rpg sqlite left:   $s" }
    if ($runExit -ne 0) { Write-Host "  wrapped command exited non-zero: $runExit" }
    Write-Host ""
    Write-Host "Standard: docs/contributing/testing-standard.md" -ForegroundColor Yellow
    exit 1
}

Write-Host "TEST SUBSTRATE LEAK ALARM OK — no fusionrpg-* temp dir or rpg-*.sqlite survived the run"
exit 0
