#!/usr/bin/env sh
REPO_ROOT="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"
exec python "$REPO_ROOT/scripts/commit-tool/block_git_write.py" --stdin-json --format claude
