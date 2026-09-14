# Spec: `debug-scope-guard`

**Program:** `live-probe` · **Map:** [../live-probe-map.md](../live-probe-map.md)
**Ideal:** [../live-probe-ideal.md](../live-probe-ideal.md) "The shape" §1 · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)

---

## Objective

A mechanical guard that distinguishes **Game Injector Debug** routes (`DebugEndpoints.cs` handlers
whose correctness depends on relaying a command to the Injector) from **RPG Server Debug** routes
(handlers whose correctness depends only on real, persisted domain logic, with no injector relay at
all) — by handler-body shape, with **no file split and no route-prefix change**. Prevents a repeat of
the 2026-09-13 incident class at the *source* level: a future route that looks RPG-Server-Debug-shaped
in a comment but actually depends on the live game (or vice versa) fails CI instead of misleading the
next session that cites it as evidence.

Who reads this: any agent or human about to write a new `/api/debug/*` route, or cite an existing one
as evidence in a live probe.

**The classification rule, corrected after an audit found the original binary rule wrong against the
real file:** the first draft ("RPG-Server-Debug if it calls `store.*`; Game-Injector-Debug if it ONLY
relays; both in one body = violation") does not survive contact with the real
`DebugEndpoints.cs` — the audit found several routes (`/lawn/quick-start`, `/scenario/{id}`,
`/effect/grant`, `/effect/withdraw`, `/effect/clear`, `/effects/reload`, the shared
`AcceptDebugSpawnExtra` helper used by `/spawn-extra` and `/fire-spawn-extra`) that genuinely call
both a real method (`store.MergeCheatField`, `store.RecordExtraSpawnIntent`, `EffectGrantSession`
mutations) **and** relay a command in the same handler — none of these are defects, they are
legitimate orchestration (e.g. entering a level *requires* telling the Injector to do it). The
relayed command string also isn't always literally `"debug.xyz"` — `"cheat.toggle"`,
`"effects.reload"`, and `"pvz.spawn.extra"` all appear too.

**The rule that actually holds:** *any relay to the Injector, anywhere in the handler body, makes the
route Game-Injector-Debug-shaped* — full stop, regardless of what other bookkeeping (session-state
flags, an in-memory `EffectGrantSession` mutation, a real `RpgStore` write alongside it) the same
handler also does. The true dividing question `live-probe-standard.md` §1 asks is *"does this route's
correctness depend on the live game responding,"* not *"does this handler call exactly one kind of
thing."* A route is **only** RPG-Server-Debug-shaped when it contains **no** injector relay call at
all (`reforge-world`, `derived-audit-actor` — the two clean examples the ideal doc already found).
Under this corrected rule, every route in the current file classifies cleanly with **zero new
exemptions** — the mixed routes above are all Game-Injector-Debug-shaped (they relay), not
violations.

Success: the guard runs green today against the current `DebugEndpoints.cs` under this corrected
rule, and turns red only for a genuinely new shape: a route that calls a real persisted-write method
**and legitimately should not** also depend on the live game, but does, or is miscategorized by a
stale banner comment.

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

**Corrected precedent:** mirror `guard-test-substrate.ps1`'s `Find-SwallowedDelete` shape, not
`guard-single-writer.ps1`'s flat whole-file regex — the audit found a flat regex cannot isolate one
handler's true extent (a route's body can span many lines with validation logic before ever reaching
its relay or store call). `guard-test-substrate.ps1:99-132` already does brace-depth matching (a
`Strip-Comments` pass, then a char-by-char `$depth` counter that finds where a block actually ends)
— reuse that technique here rather than reinventing it.

Two route shapes to recognize, not one flat lambda pattern:

```powershell
# Shape A — the shared helper, single-line, no braces to match at all:
#   MapPost(g, "/path", "cmd.name");
# Every call matching this IS Game-Injector-Debug-shaped by construction (verified once against
# the helper's own definition at the bottom of the file) — no body scan needed.

# Shape B — an inline lambda:
#   g.MapPost("/path", async (...) => { ...body... });
# Brace-match from the opening `{` to its matching `}` (Find-SwallowedDelete's own technique) to
# get the body text, then classify:
#   - body contains ANY `Send(hub, inbox, "<anything>", ...)` call, regardless of the string's own
#     prefix (not just "debug.*" — "cheat.toggle"/"effects.reload"/"pvz.spawn.extra" all count) ->
#     Game-Injector-Debug-shaped, regardless of what else the body does.
#   - body contains NO Send(...) call, and calls a real persisted method (store.*, ua.*, a
#     *Service.* method) -> RPG-Server-Debug-shaped.
#   - neither (e.g. a route that only reads a static in-memory catalog) -> flagged for manual
#     review, not auto-classified either way.
```

## Testing strategy

| Level | Cases |
|---|---|
| Guard unit test | A fixture body with only a `Send(hub, inbox, "cmd", ...)` call → Game Injector Debug. A fixture body with only `store.Foo(...)`, no relay → RPG Server Debug. A fixture body with BOTH a `store.Foo(...)` call and a `Send(...)` call → still Game Injector Debug (the corrected "any relay wins" rule) — this is the actual regression case the original wrong rule would have flagged, so it must be asserted explicitly. A `MapPost(g, "/x", "cmd")` shared-helper call → Game Injector Debug without a body scan |
| Live | Run the guard against the real, current `DebugEndpoints.cs` — must pass green with **zero exemptions**, including the mixed-shape routes the audit found (`/lawn/quick-start`, `/scenario/{id}`, `/effect/grant`, `/effect/withdraw`, `/effect/clear`, `/effects/reload`, `AcceptDebugSpawnExtra`'s two callers) — under the corrected rule these are simply Game-Injector-Debug-shaped, not violations |
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
