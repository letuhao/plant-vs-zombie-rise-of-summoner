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

/**
 * Cheap pre-filter: the policy script only has something to say about commands that
 * mention git/gh or the owner-only commit helper. Anything else (ls, dotnet test, npm,
 * pytest…) can skip the spawn entirely — which also avoids a Windows console flash per
 * command. Deliberately conservative: any doubt falls through to the real check.
 */
const POLICY_RELEVANT = /(?:^|[\s;|&"'`(])(?:git|gh)(?:\.exe)?\b|commit-tool[/\\]clean_commit\.py/i;

const BlockGitWrite = async (): Promise<Hooks> => ({
  "tool.execute.before": async (input, output) => {
    const tool = (input.tool || "").toLowerCase();
    if (!["bash", "shell", "execute_command", "run_terminal_cmd", "powershell"].includes(tool)) {
      return;
    }
    const cmd = commandFromArgs(output.args || {});
    if (!cmd || !POLICY_RELEVANT.test(cmd)) return;

    const script = path.join(projectRoot(), "scripts", "commit-tool", "block_git_write.py");
    const r = spawnSync("python", [script, "--command", cmd, "--format", "exit"], {
      encoding: "utf8",
      // The extension host has no console. Without windowsHide, Windows allocates a
      // console for this python child on every git/gh tool call, stealing focus with a
      // flashing window. Keep it fully hidden.
      windowsHide: true,
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
