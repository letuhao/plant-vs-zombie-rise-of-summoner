<#
.SYNOPSIS
  Validate session boundary records and report drift before a session edits anything.

.DESCRIPTION
  Reads tasks/sessions/*.json (see docs/contributing/session-boundary.md) and checks:
    - the record parses and carries the required fields;
    - `branch` / `worktree` still exist;
    - no two ACTIVE records claim overlapping `paths`;
    - every `worktree-*` branch is claimed by a record (an unclaimed one is abandoned).

  Exit 0 = clean, 1 = drift found. Run at session start; a clean run is the
  precondition for editing. Read-only: this script never writes.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = (git rev-parse --show-toplevel 2>$null)
)

$ErrorActionPreference = 'Stop'
if (-not $RepoRoot) { Write-Error 'not inside a git worktree'; exit 1 }
if (-not (Test-Path (Join-Path $RepoRoot '.git'))) { }

$sessionsDir = Join-Path $RepoRoot 'tasks/sessions'
if (-not (Test-Path $sessionsDir)) {
    Write-Host '[session-boundary] no tasks/sessions/ — nothing to check' -ForegroundColor Yellow
    exit 0
}

$required = @('session','program','problem','mode','branch','worktree','paths','started','status')
$validModes = @('direct','worktree')
$validStatus = @('active','merged','abandoned')

$problems = New-Object System.Collections.Generic.List[string]
$records = New-Object System.Collections.Generic.List[object]

# ---- branches and worktrees that actually exist ----
$branches = @(git -C $RepoRoot branch --format='%(refname:short)' 2>$null)
$worktreeBranches = @(git -C $RepoRoot worktree list --porcelain 2>$null |
    Where-Object { $_ -like 'worktree *' } | ForEach-Object { ($_ -replace '^worktree\s+','').Trim() })
$worktreePaths = @($worktreeBranches)

Get-ChildItem $sessionsDir -Filter *.json -File |
    Where-Object { $_.Name -notlike '_*' } |
    ForEach-Object {
        $file = $_.FullName
        $name = $_.Name
        try { $rec = Get-Content $file -Raw | ConvertFrom-Json }
        catch { $problems.Add("${name}: does not parse as JSON — $($_.Exception.Message)"); return }

        foreach ($f in $required) {
            if ($null -eq $rec.$f -and $f -ne 'worktree') {
                $problems.Add("${name}: missing required field '$f'")
            }
        }
        if ($rec.mode -and $validModes -notcontains $rec.mode) {
            $problems.Add("${name}: mode '$($rec.mode)' is not one of $($validModes -join '/')")
        }
        if ($rec.status -and $validStatus -notcontains $rec.status) {
            $problems.Add("${name}: status '$($rec.status)' is not one of $($validStatus -join '/')")
        }

        if ($rec.status -eq 'active' -and $rec.mode -eq 'worktree') {
            if (-not $rec.worktree) {
                $problems.Add("${name}: mode=worktree but no 'worktree' path recorded")
            } elseif (-not (Test-Path $rec.worktree)) {
                $problems.Add("${name}: worktree path '$($rec.worktree)' no longer exists")
            }
        }
        if ($rec.branch -and $branches -notcontains $rec.branch) {
            $problems.Add("${name}: branch '$($rec.branch)' does not exist")
        }

        $records.Add([pscustomobject]@{ Name = $name; Record = $rec })
    }

# ---- overlapping paths between two ACTIVE records ----
function Test-PathOverlap([string]$a, [string]$b) {
    $na = $a.TrimEnd('/').TrimEnd('*').TrimEnd('/')
    $nb = $b.TrimEnd('/').TrimEnd('*').TrimEnd('/')
    if ([string]::IsNullOrEmpty($na) -or [string]::IsNullOrEmpty($nb)) { return $false }
    return $na.StartsWith($nb, [StringComparison]::OrdinalIgnoreCase) -or
           $nb.StartsWith($na, [StringComparison]::OrdinalIgnoreCase)
}

$active = @($records | Where-Object { $_.Record.status -eq 'active' })
for ($i = 0; $i -lt $active.Count; $i++) {
    for ($j = $i + 1; $j -lt $active.Count; $j++) {
        $ri = $active[$i]; $rj = $active[$j]
        if ($ri.Record.mode -eq 'worktree' -and $rj.Record.mode -eq 'worktree') { continue }
        foreach ($pi in @($ri.Record.paths)) {
            foreach ($pj in @($rj.Record.paths)) {
                if (Test-PathOverlap $pi $pj) {
                    $problems.Add("$($ri.Name) and $($rj.Name) both claim '$pi' / '$pj' while active — " +
                        'narrow the scope or move one to a worktree')
                }
            }
        }
    }
}

# ---- unclaimed worktree-* branches ----
$claimed = @($records | ForEach-Object { $_.Record.branch }) + @($records | ForEach-Object { $_.Record.worktree })
foreach ($b in $branches) {
    if ($b -like 'worktree-*' -and $claimed -notcontains $b) {
        $problems.Add("branch '$b' is not claimed by any session record — abandoned worktree? " +
            'mark the owning record abandoned/merged, or remove the worktree')
    }
}

Write-Host ("[session-boundary] {0} record(s), {1} active" -f $records.Count, $active.Count)
foreach ($r in $records) {
    Write-Host ("  - {0} [{1}] {2} -> {3}" -f $r.Record.session, $r.Record.status, $r.Record.branch, $r.Record.problem)
}

if ($problems.Count -gt 0) {
    Write-Host ''
    Write-Host "[session-boundary] DRIFT ($($problems.Count)):" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "  ! $_" -ForegroundColor Red }
    exit 1
}

Write-Host '[session-boundary] clean' -ForegroundColor Green
exit 0
