# Install git hooksPath + copy local (gitignored) agent MCP/hook configs from templates.
#
# Usage (repo root):
#   powershell -File scripts/commit-tool/install_hooks.ps1
#   powershell -File scripts/commit-tool/install_hooks.ps1 -InstallShim
#   powershell -File scripts/commit-tool/install_hooks.ps1 -SkipPip
#
# Does NOT commit .cursor / .claude / .kilo / .agents / .mcp.json — those stay gitignored.
# Tracked SSOT is scripts/commit-tool/templates/ + scripts/commit-tool/*.py

param(
    [switch]$InstallShim,
    [switch]$SkipPip
)

$ErrorActionPreference = "Stop"
$Root = git rev-parse --show-toplevel
if (-not $Root) { throw "Not inside a git repo" }
Set-Location $Root

$Tpl = Join-Path $Root "scripts\commit-tool\templates"
if (-not (Test-Path $Tpl)) { throw "Missing templates at $Tpl" }

$hooksPath = Join-Path $Root ".githooks"
if (-not (Test-Path (Join-Path $hooksPath "commit-msg"))) {
    throw "Missing .githooks/commit-msg"
}

git config core.hooksPath ".githooks"
Write-Host "Set core.hooksPath=.githooks"

if (-not $SkipPip) {
    Write-Host "Installing Python mcp package..."
    python -m pip install -r (Join-Path $Root "scripts\commit-tool\requirements.txt")
    if ($LASTEXITCODE -ne 0) { throw "pip install failed" }
}

function Ensure-Dir([string]$Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

# --- Claude Code (gitignored .claude/) ---
Ensure-Dir (Join-Path $Root ".claude\hooks")
Copy-Item -Force (Join-Path $Tpl "claude.settings.json") (Join-Path $Root ".claude\settings.json")
Copy-Item -Force (Join-Path $Tpl "claude.block-git-write.sh") (Join-Path $Root ".claude\hooks\block-git-write.sh")
Copy-Item -Force (Join-Path $Tpl "mcp.json") (Join-Path $Root ".mcp.json")
Write-Host "Installed Claude settings + .mcp.json (local, gitignored)"

# --- Cursor (gitignored .cursor/) ---
Ensure-Dir (Join-Path $Root ".cursor\hooks")
Ensure-Dir (Join-Path $Root ".cursor\rules")
Copy-Item -Force (Join-Path $Root "scripts\commit-tool\cursor\gate_git_commit.py") (Join-Path $Root ".cursor\hooks\gate_git_commit.py")
Copy-Item -Force (Join-Path $Tpl "cursor.hooks.json") (Join-Path $Root ".cursor\hooks.json")
Copy-Item -Force (Join-Path $Tpl "cursor.git-mcp-commit.mdc") (Join-Path $Root ".cursor\rules\git-mcp-commit.mdc")
# Prefer absolute path so Cursor Settings always resolves the server
$serverPy = (Join-Path $Root "scripts\commit-tool\mcp_server.py") -replace '\\','/'
@"
{
  "mcpServers": {
    "repo-git": {
      "command": "python",
      "args": [
        "$serverPy"
      ],
      "cwd": "$(($Root -replace '\\','/'))"
    }
  }
}
"@ | Set-Content -Encoding utf8 (Join-Path $Root ".cursor\mcp.json")
Write-Host "Installed Cursor MCP + hooks + rule (local, gitignored)"
Write-Host "  -> .cursor/mcp.json points at $serverPy"

# --- .agents rules (gitignored) ---
Ensure-Dir (Join-Path $Root ".agents\rules")
Copy-Item -Force (Join-Path $Tpl "agents.git-mcp-commit.md") (Join-Path $Root ".agents\rules\git-mcp-commit.md")
Write-Host "Installed .agents/rules/git-mcp-commit.md (local, gitignored)"

# --- Kilo (gitignored .kilo/) ---
Ensure-Dir (Join-Path $Root ".kilo\plugin")
Copy-Item -Force (Join-Path $Tpl "kilo.block-git-write.ts") (Join-Path $Root ".kilo\plugin\block-git-write.ts")
$kiloJson = Join-Path $Root ".kilo\kilo.json"
if (Test-Path $kiloJson) {
    python -c @"
import json
from pathlib import Path
p = Path(r'$kiloJson')
data = json.loads(p.read_text(encoding='utf-8'))
mcp = data.setdefault('mcp', {})
mcp['repo-git'] = {
    'command': 'python',
    'args': ['scripts/commit-tool/mcp_server.py'],
}
plugins = data.get('plugin')
if plugins is None:
    data['plugin'] = ['./plugin/block-git-write.ts']
elif isinstance(plugins, list):
    if './plugin/block-git-write.ts' not in plugins:
        plugins.append('./plugin/block-git-write.ts')
elif isinstance(plugins, str):
    data['plugin'] = [plugins, './plugin/block-git-write.ts']
p.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
print('merged kilo.json mcp + plugin')
"@
} else {
    @"
{
  "`$schema": "https://app.kilo.ai/config.json",
  "snapshot": false,
  "plugin": ["./plugin/block-git-write.ts"],
  "mcp": {
    "repo-git": {
      "command": "python",
      "args": ["scripts/commit-tool/mcp_server.py"]
    }
  }
}
"@ | Set-Content -Encoding utf8 $kiloJson
    Write-Host "Created .kilo/kilo.json (local, gitignored)"
}

# Patch kilo agent prompts that still say no-git if present
$solid = Join-Path $Root ".kilo\agent\solid-runner.md"
if (Test-Path $solid) {
    $txt = Get-Content -Raw $solid
    if ($txt -match 'no `git commit`') {
        # leave as-is if already patched; best-effort note only
        Write-Host "Note: review .kilo/agent/solid-runner.md for MCP-only commit wording"
    }
}

if ($InstallShim) {
    $shimDir = Join-Path $Root "scripts\commit-tool\git-shim"
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    if ($userPath -notlike "*$shimDir*") {
        [Environment]::SetEnvironmentVariable("Path", "$shimDir;$userPath", "User")
        Write-Host "Prepended git-shim to User PATH (new shells): $shimDir"
    } else {
        Write-Host "git-shim already on User PATH"
    }
}

Write-Host ""
Write-Host "=== Checklist (local only — not committed) ==="
Write-Host "1. Cursor: Settings > MCP — confirm repo-git; reload hooks"
Write-Host "2. Claude Code: trust workspace, /mcp approve repo-git"
Write-Host "3. Hygiene: turn off IDE Attribution (does not replace hooks)"
Write-Host "4. Agent commit: MCP repo-git.commit only (never git commit / git push)"
Write-Host ""
Write-Host "Tracked SSOT: scripts/commit-tool/ (+ templates/)"
Write-Host "Smoke: python scripts/commit-tool/smoke_test.py"
