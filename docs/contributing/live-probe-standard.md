# Live-probe standard

**Status: binding for every "prove this feature works live" session.** A live probe proves the real
RPG server pipeline works for a real player object, through the same centralized backend the normal
game/web frontend uses. It never proves that a debug tool can make an API *respond as if* that were
true. This is the standard [`DESIGN-GATE.md`](../DESIGN-GATE.md) points debug-API and live-verification
work at, the same way [`testing-standard.md`](testing-standard.md) governs `tests/**` substrate and
[`validation-ssot.md`](../architecture/validation-ssot.md) governs what a guardrail may assert.

**Why this exists.** 2026-09-13, `actor-hub-and-combat-power-solid-fixing` T14 (`bound-loadout-hub`):
a live probe deployed a WallNut with a debug loadout JSON (`{"absolutes":{"hp":2000,"maxHp":5000,
"atk":500}}`) directly through the Game Injector's debug bind path, then read the result back through
the SAME injector's debug telemetry (`debug.board-stats`). `hp` matched (2000); `maxHp` (4000) and
`attack` (1) were untouched vanilla baseline — the feature was actually broken. The probe's *shape* was
sound engineering practice (real numbers, not a screenshot), but its *scope* was wrong on both ends:
the actor was never a real player-owned `UniqueActor` created through the real acquire/level/build
flow, and "prove it worked" meant "the injector says the field changed," not "the RPG server's own
domain logic and persisted state say so." A debug tool that can fabricate the precondition can equally
fabricate a false pass — the T12 half of the SAME probe (aptitude allocation) happened to be real
Server-persisted state reached through a real endpoint (`POST /api/aptitudes/unique/allocate`), which
is exactly why it caught the T14 defect instead of hiding it too.

---

## 1. Two debug scopes — name which one you are in

| | **Game Injector Debug** | **RPG Server Debug** |
|---|---|---|
| Lives in | `FusionRpg.Injector`'s `debug.*` commands (`CheatCommandRunner.cs`), relayed by `DebugEndpoints.cs`'s thin `MapPost(g, path, "debug.xyz")` wrappers | `FusionRpg.Server`'s own application/domain/persistence code — `RpgStore`, the real `/api/*` endpoints the web FE calls, `EffectGrantSession`, `AptitudeEndpoints`, etc. |
| Purpose | Simulate/inject a game-side situation, inspect client behavior, reproduce a hard-to-trigger case, read live Unity/board telemetry | Invoke a **real** server operation, create/mutate **real** persisted records, exercise the **same** domain/application logic the normal FE depends on |
| May fabricate | Board/entity state inside the game engine (`debug.spawn-plant`, `debug.set-mods`, a raw loadout JSON bound straight to a ptr) | Nothing. It may only call the real endpoint/service with real inputs |
| Already-correct examples | Most of `DebugEndpoints.cs`'s `MapPost(g, ..., "debug.*")` lines | `POST /api/debug/reforge-world` (DAL-only, no injector round trip, re-derives a *real* player's roster) · `POST /api/debug/derived-audit-actor` ("real UniqueActor → Hub → /sheet (never a synthetic 269 paint)" — its own comment already states this rule) · `POST /api/aptitudes/unique/allocate` (real, used correctly by T12's half of the 2026-09-13 probe) |
| May prove | "the game engine reflects X" | "the RPG server's domain logic and persistence produce X for a real record" |
| May NOT prove | Server-side correctness — the injector has no domain logic, no persistence, no validation | Game-engine wiring — a real DB record proves nothing about whether the Injector applies it |

**An agent must be able to say which scope a given debug call belongs to before using it as evidence.**
If you cannot say, stop and find out — do not treat a response from either scope as proof of the other.

---

## 2. The rule

**A debug API may trigger a real operation. It must never fabricate the state that operation is
supposed to produce.**

Concretely, for any "does feature X work" probe:

- The subject (an actor, an item, an allocation, a deployment — whatever the feature is about) must be
  a record a **real** player-facing flow could have created: real summon/fusion/acquire, real level-up,
  real stat allocation via the real endpoint, real deploy. `uniqueActorId`/`instanceId` etc. passed to a
  debug call must resolve to a row that already exists in `RpgStore`, not one the debug call invents.
- A debug call may **skip tedium** (clicking through a UI, waiting for a timer) but must still route
  through the real application/domain/persistence path — same service, same validation, same DB write.
- Fabricating stats, level, definition, inventory, or a deployment *result* directly — bypassing the
  service/domain layer that would normally compute or gate it — is the exact defect this standard bans.

---

## 3. Proof requires persistence and read-back, never a response alone

An HTTP 200 or a plausible JSON body is not evidence. At minimum, a live probe for feature X must show:

1. The subject record existed (or was created through a real flow) **before** the operation under test.
2. The operation was invoked through the real endpoint/service — not a hand-rolled shortcut into the
   same final state.
3. `RpgStore` (or the relevant persistence) actually holds the new/changed row afterward — read it back
   through the **same query path** the normal frontend uses, not a debug-only accessor built to make the
   test convenient.
4. Where the feature also has a live-engine half (the Injector reflecting server state onto a Unity
   entity), that half is checked **separately**, and a pass on one half is never reported as covering
   the other — see §1's "may prove / may not prove" rows. The 2026-09-13 incident's actual lesson is
   that these are two independent chains that happen to look like one pipeline.

---

## 4. Anti-cheat checklist — each of these is an outright fail

- **Fake actor/subject.** The id under test does not resolve to a record real gameplay could have
  produced.
- **Fake stats/state supplied by the debug call** instead of loaded from persisted backend state.
- **Injector-only proof of a server claim**, or **server-only proof of an injector claim** (§1).
- **Database bypass.** The operation "succeeds" without the expected persisted row existing or changing.
- **Mocked pipeline.** A stub/mock stands in for the real application/domain/persistence path.
- **Unwired implementation.** Code exists but nothing in the real API/application pipeline can reach
  it — an implementation that can only be exercised by a test-only shortcut is incomplete, not proven.
- **Response-only proof.** No read-back through the normal query path.

---

## 5. Applying this to Actor Hub (and every future feature)

The intended chain for any Actor-Hub-adjacent live probe:

```
real game/web flow → acquire/create real UniqueActor → RPG backend persists it
  → real level/stat-allocation calls persist real changes → real deploy operation
  → RPG backend persists deployment state → Actor Hub / Injector reads that real state
```

Building or extending a **dedicated RPG Server Debug surface** (endpoints that chain the real
acquire/allocate/deploy calls the web FE already uses, without needing a human to click through them)
is in scope and encouraged — that is what turns a slow manual proof into a fast one **without**
fabricating anything. What is never in scope is a debug endpoint that hands the "already computed"
result to the reader instead of making the real pipeline compute it.

**This document does not itself create that dedicated surface.** `DebugEndpoints.cs` today mixes both
scopes in one file (mostly Game Injector Debug relays, with `reforge-world`/`derived-audit-actor` as
the two RPG-Server-Debug-shaped exceptions). Splitting it, and building out a fuller RPG Server Debug
API/skill, is real design work and belongs behind this repo's own `/idea` → `/spec` pipeline
([DESIGN-GATE.md](../DESIGN-GATE.md) §0), not a drive-by refactor — named here as the open follow-up,
not claimed as done.

---

## 6. Definition of done for a live probe

Not:

> "The API returned `ok: true`."

But:

> "A real record, created through a real flow, was changed by a real operation, and I read the changed
> state back through the same path the normal frontend uses — and, separately, confirmed or denied
> whether the live game engine reflects it."

State explicitly, per probe:

- Which scope (§1) each debug call belonged to.
- What was read back, from where, and whether that read used the normal query path.
- If either half (server-persisted vs. live-engine) was not checked, say so — do not let a pass on one
  stand in for the other.

---

## 7. Enforcement

- [`DESIGN-GATE.md`](../DESIGN-GATE.md) §1 topic index points here for any debug-API or live-verification
  work.
- `AGENTS.md`/`CLAUDE.md` point here rather than restating the rule (same convention as
  [`testing-standard.md`](testing-standard.md) §4).
- No automated guard enforces this today — it is a review discipline, the same way `DESIGN-GATE.md`
  itself is. A future guard (e.g. flagging a debug endpoint that both accepts fabricated stats AND
  never reads `RpgStore`) is a reasonable follow-up, not yet built.
