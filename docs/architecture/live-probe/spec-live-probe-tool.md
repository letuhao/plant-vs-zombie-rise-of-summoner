# Spec: `live-probe-tool`

**Program:** `live-probe` · **Map:** [../live-probe-map.md](../live-probe-map.md)
**Ideal:** [../live-probe-ideal.md](../live-probe-ideal.md) "The shape" §2 (six-step recipe) ·
**Standard:** [../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)

---

## Objective

An operator tool that runs the real 6-step live-probe recipe against a running Server + live game
(Injector), end to end, and reports pass/fail per step with concrete numbers — never a fabricated
actor, never a response-only "ok:true" as proof. Replaces "hand-type curl calls and eyeball the game
screen" with one repeatable, scriptable run any agent or the owner can invoke.

**This is genuinely new engineering, not an extension of an existing prove tool** — corrected in the
ideal doc's audit: `tools/ProveHubCombat` drives `RpgStore.InMemory()` in-process with zero
`HttpClient` usage; it proves offline invariants, not a live server+game. This tool is the first one
in the repo that does real HTTP against a running `FusionRpg.Server` plus an async ack/poll wait for
the Injector's own telemetry.

Who reads this: any agent asked to prove a Bound-`UniqueActor`/combat-stat feature works live (the
exact class of claim T12/T14 needed and got wrong the fast way).

## The six steps (from the ideal doc, restated)

1. **Acquire** — `POST /api/creatures/summon` (real gacha) or `POST /api/debug/spawn-unique-actor`
   (identity-only shortcut: `PlayerId, Side, GameTypeId` — legitimate, no stats to fabricate).
2. **Allocate** — `POST /api/aptitudes/unique/allocate` for the new `instanceId`.
3. **Equip** — `POST /api/items/equip` (prefer over the `PUT .../equipment/{slot}` stub-allowlist
   route — see the ideal doc's Equip row).
4. **Deploy** — `POST /api/unique/actors/{id}/deploy` — **`loadoutJson` omitted, always.**
5. **Persisted-state read-back** — `GET /api/unique/actors/{id}` + `.../equipment`. Proves inputs
   were persisted correctly. **Not yet a live proof.**
6. **Live-engine read, kept explicitly separate** — `debug.board-stats` for the deployed ptr
   (`CheatCommandRunner.cs:325` → `DebugRuntime.BoardEntityStats()`, relayed at
   `DebugEndpoints.cs:314`), asserted against step 5's persisted values. Labeled in the tool's own
   output as the distinct "does the live game reflect it" claim, never merged into step 5's verdict.

## Tech stack

- **C# console app**, `net8.0`, matching `tools/ProveHubCombat`/`tools/ProveAptitude`'s project
  shape (a `Program.cs` + `.csproj`) — but using **real `HttpClient`** against a running Server base
  URL, not `RpgStore.InMemory()`. This is the deliberate divergence from those tools' internals; their
  *project structure* convention (a `tools/<Name>` console app wrapped by a thin `scripts/<name>.ps1`)
  is still worth matching for discoverability.
- `scripts/prove-live-probe.ps1` — thin wrapper (`Push-Location tools/ProveLiveProbe; dotnet run --
  ...`), mirroring `scripts/prove-hub-combat.ps1`'s own wrapper shape exactly.
- Async ack/poll for the Injector-relayed step (6): the same pattern `DebugEndpoints.cs` already uses
  server-side (`PollForKind`, e.g. lines 85/373/382) — poll `GET /api/debug/events?kinds=debug.board-
  stats` after sending the command, with a bounded timeout, not a fixed sleep.

## Commands

```powershell
.\scripts\prove-live-probe.ps1 -PlayerId 1 -Side plant -TypeId <peashooter-id> `
    -AptitudeId Might -AptitudePoints 30 -ItemInstanceId <owned-item-id> -TimeoutSec 30
# exits 0 on full pass, non-zero + a labeled failure line naming which of the 2 halves (persisted vs live) failed
```

## Project structure

| Path | Duty |
|---|---|
| `tools/ProveLiveProbe/Program.cs` | The 6-step HTTP client + assertions |
| `tools/ProveLiveProbe/ProveLiveProbe.csproj` | net8.0 console app, references `FusionRpg.Contracts` for typed request/response DTOs (no ad-hoc JSON shape guessing) |
| `scripts/prove-live-probe.ps1` | Thin wrapper, same shape as `scripts/prove-hub-combat.ps1` |
| `docs/runbook/local-dev.md` | Gets a short "live-probe-tool" section pointing here, alongside the existing `live-lawn-quick-start` skill reference |

## Code style

One real snippet showing the two-halves separation the whole tool exists to enforce:

```csharp
var persisted = await GetPersistedState(http, instanceId);   // step 5 — Server GETs only
var liveEngine = await GetLiveBoardStats(http, ptr, timeout); // step 6 — debug.board-stats, separate

Report.Section("Persisted state (RPG Server Debug)", persisted);
Report.Section("Live engine (Game Injector Debug read)", liveEngine);
if (persisted.MaxHp != liveEngine.MaxHp || persisted.Atk != liveEngine.Atk)
    Report.Fail("live-engine half: game does not reflect persisted state", persisted, liveEngine);
```

Never a single merged `bool Passed` across both halves without saying which one failed — matching
`live-probe-standard.md` §6's own "Definition of done" requirement to state each half explicitly.

## Testing strategy

| Level | Cases |
|---|---|
| Unit (Core/Data, offline) | Request/response DTO (de)serialization only — no live server needed for this slice |
| Live (owner or agent, real game+server up) | Full 6-step run against a real deployed specimen; assert both halves pass; assert the tool correctly FAILS and names the right half when `loadoutJson` is deliberately populated in a throwaway test invocation (proves the tool itself would have caught the 2026-09-13 incident) |
| Guard | None new — this tool is itself a verification instrument, not a source-shape guard (see `debug-scope-guard`) |

## Boundaries

- **Always:** `loadoutJson` stays empty/omitted at step 4 — the tool must refuse to run (or warn
  loudly) if invoked with a non-empty override, since that is the exact fabrication vector this whole
  program exists to close.
- **Always:** report the two halves (persisted vs. live-engine) separately, never as one merged
  boolean.
- **Ask first:** adding a 7th step or a new endpoint call not already named in the 6-step recipe —
  scope creep back toward "the tool grows its own domain logic" is the SOLID concern the ideal doc's
  audit named for Decision 2.
- **Never:** call any `/api/debug/*` route that fabricates actor/stat state (the Game-Injector-Debug
  relays this program's own standard bans as evidence) — the ONLY debug-prefixed route this tool may
  call is the identity-only `spawn-unique-actor` shortcut (step 1) and the read-only `board-stats`
  (step 6).

## Success criteria

- [ ] `tools/ProveLiveProbe` builds and runs against a live Server + live game.
- [ ] Exits 0 only when both halves (persisted + live-engine) match; exits non-zero with a labeled
      failure otherwise.
- [ ] Never populates `loadoutJson`; refuses/warns if a caller tries to pass one through.
- [ ] Proven to catch the 2026-09-13 incident shape: run against a specimen with a deliberately
      broken Hub-bonus grant (or the pre-fix `bound-loadout-hub` state via git if still reachable) and
      confirm it reports a live-engine-half FAIL rather than a pass.

## Open questions

None — tech stack and shape were resolved in the ideal-phase audit (net-new C# console tool,
real HTTP, matching project-structure convention only).
