# Install git hooksPath + local Cursor shell gate for clean commits.
#
# Usage (repo root):
#   powershell -File scripts/commit-tool/install_hooks.ps1
#
# What it does:
#   1) Sets this repo's core.hooksPath to .githooks (commit-msg policy)
#   2) Copies Cursor beforeShellExecution gate into .cursor/ (gitignored locally)

$ErrorActionPreference = "Stop"
$Root = git rev-parse --show-toplevel
if (-not $Root) { throw "Not inside a git repo" }
Set-Location $Root

$hooksPath = Join-Path $Root ".githooks"
if (-not (Test-Path (Join-Path $hooksPath "commit-msg"))) {
    throw "Missing .githooks/commit-msg — expected commit-tool install"
}

git config core.hooksPath ".githooks"
Write-Host "Set core.hooksPath=.githooks"

$cursorDir = Join-Path $Root ".cursor"
$cursorHooksDir = Join-Path $cursorDir "hooks"
New-Item -ItemType Directory -Force -Path $cursorHooksDir | Out-Null

$srcGate = Join-Path $Root "scripts\commit-tool\cursor\gate_git_commit.py"
Copy-Item -Force $srcGate (Join-Path $cursorHooksDir "gate_git_commit.py")

# Matcher targets the git `commit` subcommand only (not paths like commit-msg).
# failClosed=false: a broken python path must not freeze the whole Shell tool.
@"
{
  "version": 1,
  "hooks": {
    "beforeShellExecution": [
      {
        "command": "python .cursor/hooks/gate_git_commit.py",
        "matcher": "\\bgit(?:\\.exe)?\\b(?:\\s+-c\\s+\\S+|\\s+-\\S+)*\\s+commit\\b|commit-tool[/\\\\]clean_commit\\.py",
        "failClosed": false
      }
    ]
  }
}
"@ | Set-Content -Encoding utf8 (Join-Path $cursorDir "hooks.json")

Write-Host "Installed Cursor gate -> .cursor/hooks.json"
Write-Host ""
Write-Host "Agent commit command:"
Write-Host '  python scripts/commit-tool/clean_commit.py -m "Your subject here"'
Write-Host ""
Write-Host "Reload Cursor hooks (Hooks settings / restart) if the gate does not fire yet."
