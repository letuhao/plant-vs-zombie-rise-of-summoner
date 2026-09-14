# Capability map: `live-probe`

**Status:** approved 2026-09-13. Ideal doc: [live-probe-ideal.md](live-probe-ideal.md). Standard:
[../contributing/live-probe-standard.md](../contributing/live-probe-standard.md). Plan/tasks:
[../../tasks/live-probe-plan.md](../../tasks/live-probe-plan.md) /
[../../tasks/live-probe-todo.md](../../tasks/live-probe-todo.md).

| Module id | Responsibility | Depends on |
|---|---|---|
| `debug-scope-guard` | `scripts/guard-debug-scope.ps1` — mechanically detects Game-Injector-Debug vs RPG-Server-Debug handler shape from handler bodies (no file split); scope-banner comments in `DebugEndpoints.cs` | — |
| `live-probe-tool` | The 6-step operator tool: summon/`spawn-unique-actor` → aptitude allocate → equip → deploy (`loadoutJson` always empty) → persisted-state read-back → separate live-engine read (`debug.board-stats`), asserting both halves match | — |
| `actor-hub-live-proof` | Run the real T12/T14 proof end-to-end using `live-probe-tool`; tick the remaining live-probe checkboxes in `actor-hub-and-combat-power-solid-fixing-todo.md` | `live-probe-tool` |
| `lawn-screenshot` | On-demand Unity frame capture (`debug.screenshot`) + server store/serve + minimal control-room viewer, so live probes stop depending on eyeballing the game (ideal addendum + spec on this branch; plan/tasks `tasks/live-probe-screenshot-*`) | — |

**Build order:** `debug-scope-guard`, `live-probe-tool` in parallel → `actor-hub-live-proof`; `lawn-screenshot` independent (own worktree/branch, no shared files with the first three).

**Module specs:** `docs/architecture/live-probe/spec-debug-scope-guard.md` ·
`spec-live-probe-tool.md` · `spec-actor-hub-live-proof.md` · `spec-lawn-screenshot.md`
(idea addendum: `ideal-lawn-screenshot.md`).

**Dropped, not a module here — handed off instead (2026-09-13):** the `/talk`/`/cage`
`CreatureMintSpec` trust surface turned out to be `party-dungeon`'s own `wild-room` module (D4.8's
own "Still not built" note names the exact same gap — `TalkTree`'s missing `Step` orchestrator — as
an already-known, already-scoped-out boundary from 2026-09-07, not something this survey discovered
fresh). Building it here would fork ownership of an already-approved module. Cross-linked into
`tasks/party-dungeon-todo.md` D4.8 instead of built or re-specified in this initiative.

**Out of scope for this initiative** (named in the ideal doc, not re-opened here): splitting
`DebugEndpoints.cs` into two files, removing `DeployAsync`'s `loadoutJson` parameter, replacing
`UniqueEquipmentCatalog`'s stub allowlist, building a Server-side ActorHub-equivalent compose
endpoint.
