/**
 * Kilo plugin: block raw git/gh write from bash/execute tools.
 * Defense-in-depth — git hooks + MCP remain mandatory (subagent gaps exist).
 */
import { spawnSync } from "node:child_process";
import path from "node:path";

type Hooks = {
  "tool.execute.before"?: (
    input: { tool: string; sessionID: string; callID: string },
    output: { args: Record<string, unknown> },
  ) => Promise<void>;
};

function projectRoot(): string {
  return process.env.KILO_PROJECT_DIR || process.env.CLAUDE_PROJECT_DIR || process.cwd();
}

function commandFromArgs(args: Record<string, unknown>): string {
  const c = args.command ?? args.cmd ?? args.script;
  return typeof c === "string" ? c : "";
}

const BlockGitWrite = async (): Promise<Hooks> => ({
  "tool.execute.before": async (input, output) => {
    const tool = (input.tool || "").toLowerCase();
    if (!["bash", "shell", "execute_command", "run_terminal_cmd", "powershell"].includes(tool)) {
      return;
    }
    const cmd = commandFromArgs(output.args || {});
    if (!cmd) return;

    const script = path.join(projectRoot(), "scripts", "commit-tool", "block_git_write.py");
    const r = spawnSync("python", [script, "--command", cmd, "--format", "exit"], {
      encoding: "utf8",
    });
    if (r.status === 2) {
      throw new Error(
        (r.stderr || r.stdout || "").trim() ||
          "Blocked git/gh write. Use MCP repo-git.commit; push is owner-only.",
      );
    }
  },
});

export default BlockGitWrite;
