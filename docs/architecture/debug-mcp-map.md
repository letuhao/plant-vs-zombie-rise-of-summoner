# Debug MCP — capability map

**Status:** spec phase, 2026-09-13. Single-module program (owner chose a single
spec over per-module specs): the map is the index, the spec is the contract.

| Module id | Responsibility | Depends on |
|---|---|---|
| `debug-mcp-server` | All 7 tools, registry, budgets, evidence envelopes, both transports | — |

Spec: [`debug-mcp/spec-debug-mcp.md`](debug-mcp/spec-debug-mcp.md) ·
Idea: [`debug-mcp-ideal.md`](debug-mcp-ideal.md) ·
Plan/todo (phase 2): `tasks/debug-mcp-plan.md`, `tasks/debug-mcp-todo.md`.

Build order inside the module (for plan phase): registry + budget + evidence
→ `debug_call` → read tools (events, match, logs, actor) + `debug_lawn_setup`
→ `debug_verify` + `debug_preflight` → HTTP mode → owner Inspector walkthrough.
