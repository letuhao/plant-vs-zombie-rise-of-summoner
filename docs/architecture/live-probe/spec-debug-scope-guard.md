# Spec: `debug-scope-guard`

**Program:** `live-probe` · **Map:** [../live-probe-map.md](../live-probe-map.md)
**Ideal:** [../live-probe-ideal.md](../live-probe-ideal.md) "The shape" §1 · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)

---

## Objective

A mechanical guard that distinguishes **Game Injector Debug** routes (`DebugEndpoints.cs` handlers
that only relay a `"debug.xyz"` command string to the Injector) from **RPG Server Debug** routes
(handlers that call `store.*`/`ua.*`/a service method directly — real domain/persistence logic) —
by handler-body shape, with **no file split and no route-prefix change**. Prevents a repeat of the
2026-09-13 incident class at the *source* level: a future route that looks RPG-Server-Debug-shaped in
a comment but is actually a bare relay (or vice versa) fails CI instead of misleading the next
session that reads it.

Who reads this: any agent or human about to write a new `/api/debug/*` route, or cite an existing one
as evidence in a live probe.

Success: the guard runs green today against the current `DebugEndpoints.cs` (which already contains
both shapes correctly, per the ideal doc's own survey), and turns red the moment a new route mixes
the two shapes inside one handler (calls both `store.*` AND relays a `"debug.xyz"` command in the
same body) without an explicit, named exemption.

## Tech stack

- PowerShell (`.ps1`), same convention as every other `scripts/guard-*.ps1` in this repo
  (`guard-single-writer.ps1`, `guard-actor-hub.ps1`, `guard-funnel-delta.ps1`, etc.)
- Regex/text scan over `src/FusionRpg.Server/DebugEndpoints.cs` (and any future file registered the
  same way) — no build step, no `dotnet` invocation, matching this repo's existing static guards

## Commands

```powershell
.\scripts\guard-debug-scope.ps1                 # run standalone
.\scripts\deploy-play.ps1                       # runs all guards, this one included once wired
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"
```

## Project structure

| Path | Duty |
|---|---|
| `scripts/guard-debug-scope.ps1` | The guard itself |
| `tests/FusionRpg.Guard.Tests/DebugScopeGuardTests.cs` | Regression test invoking the guard against fixture text, mirroring `SingleWriterGuardTests`-style existing guard tests if present, or a new peer |
| `src/FusionRpg.Server/DebugEndpoints.cs` | Gets scope-banner comments (`// Game Injector Debug` / `// RPG Server Debug`) above each route grouping — human-readable signal, not what the guard itself checks |

## Code style

Mirror `guard-single-writer.ps1`'s own shape exactly: a `param($Root = ...)` root resolve, an
`$ErrorActionPreference = "Stop"`, a per-route scan, a `$failures` accumulator, and a final
`if ($failures.Count -gt 0) { ...; exit 1 }` / else `"... GUARD OK"` line. Detection logic per route
handler body (a `g.MapPost("/path", ...)` or `g.MapGet("/path", ...)` lambda):

```powershell
# A route is Game-Injector-Debug-shaped if its ONLY effect is relaying a command string:
#   await Send(hub, inbox, "debug.xyz", ...)  — and it calls no store.*/ua.* domain method.
# A route is RPG-Server-Debug-shaped if it calls a real domain/persistence method
#   (store.*, ua.*, a *Service.* method) directly in its own body.
# A route calling BOTH shapes in the same handler body is a VIOLATION unless the route name
# appears in an explicit $exempt allowlist with a one-line reason (same convention as
# guard-single-writer.ps1's own $allowed list).
```

## Testing strategy

| Level | Cases |
|---|---|
| Guard unit test | A fixture route body with only a `Send(hub, inbox, "debug.xyz", ...)` call → passes as Game Injector Debug. A fixture body with only `store.Foo(...)` → passes as RPG Server Debug. A fixture body with both → fails unless allowlisted |
| Live | Run the guard against the real, current `DebugEndpoints.cs` — must pass green with zero exemptions needed (the file's existing routes are already correctly single-shaped, per the ideal doc's survey) |
| Regression | Guard script itself has a small `tests/FusionRpg.Guard.Tests` case so a future edit to the guard's own regex is caught by CI, not discovered live |

## Boundaries

- **Always:** Guard is text/regex-only — no `dotnet build`, no live server, no game.
- **Ask first:** Adding a new exemption to the guard's allowlist (mirrors `guard-single-writer.ps1`'s
  own review-gated allowlist).
- **Never:** Weaken the guard to pass a specific new route without either fixing that route's shape
  or naming a real, reviewed exemption — the same rule `testing-standard.md` R4 states for its own
  ratchet ("adding a line is a review event").

## Success criteria

- [ ] `scripts/guard-debug-scope.ps1` exists, runs standalone, green against current
      `DebugEndpoints.cs`.
- [ ] Wired into `deploy-play.ps1`'s guard set (alongside the other 5+ existing guards).
- [ ] `DebugEndpoints.cs` carries scope-banner comments above each route grouping.
- [ ] `tests/FusionRpg.Guard.Tests` has a regression case proving the guard catches a mixed-shape
      fixture.
- [ ] `docs/DESIGN-GATE.md`'s live-probe topic row and `docs/contributing/live-probe-standard.md` §7
      (Enforcement) updated to name this guard now that it exists (both currently say "no automated
      guard enforces this today").

## Open questions

None — this module's shape was already settled in the ideal-phase audit (named resolver, no file
split required).
