# Spec: `actor-hub-live-proof`

**Program:** `live-probe` · **Map:** [../live-probe-map.md](../live-probe-map.md)
**Depends on:** `live-probe-tool` (must be built first — this module has no code of its own beyond a
tool invocation + doc updates)

---

## Objective

Actually run the real T12/T14 proof `actor-hub-and-combat-power-solid-fixing` left as an owner-only
"live probe owed" gate — using `live-probe-tool`, never a fabricated actor — and close the remaining
live-probe checkboxes in that program's own todo with real evidence. This module is the reason
`live-probe` exists: the incident that started this whole program was T14's own broken proof.

Success: T12 (Bound aptitude parity) and T14 (Bound loadout via Hub) each get a real, `live-probe-
tool`-produced pass/fail, for BOTH halves (persisted state, live engine), and
`tasks/actor-hub-and-combat-power-solid-fixing-todo.md`'s remaining `[ ]` lines for these two tasks
either tick with that evidence or stay open with a named, concrete reason.

## Tech stack

No new code — this module is an **operation**, not a build: run `live-probe-tool` against a real
deployed game + server, twice (once per claim), and write down what it says. **Both runs must use
`-Mode B`** — T12/T14 are combat-stat claims about the live Unity entity, which only Mode B checks;
Mode A alone would repeat this program's own founding mistake by proving persisted state only.

## Prerequisites (missing from an earlier draft — added after audit)

This module can only run with a real game and server up. It is **not** a synchronous call an agent
makes cold. Before either command below:

1. **Build + deploy the Injector, start the server correctly** — per `CLAUDE.md`'s "Live deploy" /
   "Server lifetime" section: `Start-Process dist\FusionRpg.Server\FusionRpg.Server.exe` directly
   (an agent-launched server dies when a synchronous tool call's process tree is reaped — this has
   caused real, misread "mid-run crash" incidents before). Never `deploy-play.ps1` with a server
   restart from an agent shell.
2. **Confirm the server is actually up** — `GET /health` (or equivalent) before proceeding.
3. **Cold-start the lawn** per the `live-lawn-quick-start` skill — enter a real level, confirm a live
   board exists. `store.InjectorConnected` gates several routes (`DebugEndpoints.cs:180`,
   `/lawn/quick-start`) and returns 409 without a connected game; `debug.board-stats` (step 6) returns
   nothing useful without a live board either.
4. **Only then** run the commands below, from the owner's own terminal, or a background agent that
   itself followed steps 1-3 correctly (never a plain synchronous `Bash` call expecting the server
   it just started in the same call to still be alive).

## Commands

```powershell
# T12 — aptitude parity (Bound Peashooter)
.\scripts\prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId <banner-id> `
    -AptitudeId Might -AptitudePoints 30

# T14 — loadout via Hub (Bound WallNut; equip a real, owned item first, per live-probe-tool's recipe)
.\scripts\prove-live-probe.ps1 -Mode B -PlayerId 1 -Side plant -BannerId <banner-id> `
    -Role <slot> -ItemInstanceId <owned-item-id>
```

## Project structure

No new files. Touches only:

| Path | Duty |
|---|---|
| `tasks/actor-hub-and-combat-power-solid-fixing-todo.md` | T12/T14's own remaining checkboxes get ticked with real evidence, or stay open with a named reason |
| `docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md` | "Program Done when" row updated once T12/T14's own boxes close |

## Code style

N/A — no code. Evidence entries in the todo file follow that file's own established convention (see
its T12/T14 entries from 2026-09-13 for the shape: what was run, what the numbers were, what passed).

## Testing strategy

| Level | Cases |
|---|---|
| Live (real game + server) | Run `live-probe-tool` for T12 and T14 separately, per the commands above |
| Regression | None new — this module produces no code to regress |

## Boundaries

- **Always:** run through `live-probe-tool` only — never hand-roll a debug bind/loadout shortcut for
  this proof, which is exactly the mistake this whole program exists to close.
- **Always:** report both halves (persisted-state, live-engine) separately per specimen, per
  `live-probe-tool`'s own boundary rule.
- **Ask first:** if `live-probe-tool` itself needs a fix to run this proof (a bug found while using
  it) — fix the tool under `live-probe-tool`'s own module, then re-run here; don't patch around it
  inline.
- **Never:** declare T12/T14 "done" on a persisted-state pass alone if the live-engine half fails or
  wasn't run — matches `live-probe-standard.md` §6's own definition of done.
- **Never:** start the Server via a synchronous agent tool call for this module (see Prerequisites) —
  use `Start-Process` and confirm `/health` first, from the owner's own terminal or a correctly
  detached background agent.
- **Never:** run `-Mode A` and report it as covering T12/T14 — these two tasks are combat-stat claims
  about the live entity; only `-Mode B` checks that.

## Success criteria

- [ ] T12 run: both halves reported, both pass (T12 already passed once manually on 2026-09-13 with
      real numbers — this run should reproduce that through the tool, not just cite the earlier
      manual result).
- [ ] T14 run: both halves reported. **Observed failing** in the 2026-09-13 incident (`maxHp`/`attack`
      stayed at vanilla baseline); a hypothesis for why (the Hub-bonus grant needs a fresh
      `EntityApply` reapply to reach Unity, and nothing forces one right after Bind) was proposed but
      **not confirmed** — if T14 still fails when this module runs, report the observed numbers
      plainly; do not assume the hypothesis is the actual cause without checking, and do not fix
      `bound-loadout-hub` here either way (out of scope for `live-probe`; a fix belongs to that
      program once the real cause is confirmed).
- [ ] `actor-hub-and-combat-power-solid-fixing-todo.md`'s T12/T14 lines updated with the real result
      either way.

## Open questions

None.
