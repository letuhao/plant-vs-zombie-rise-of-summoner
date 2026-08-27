# Spec: `aptitude-allocation-surface` — the first player-reachable way to spend aptitude points

**Module id:** `aptitude-allocation-surface` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-27 — owner directive ("you should complete the plan") after reviewing the
audit that found `point-economy`'s own persistence (P6.1-P6.4) has zero production callers.**

**Depends on:** `point-economy` (`PointBudget`/`RespecPolicy`/`AllocationStore`, all built and tested) ·
**Blocks:** P9.2-P9.4 (real aptitude-signal-carrying battle outcomes need this to exist first).

---

## 1. Objective and scope decision

`point-economy` (Phase 6) built the budget math and the persistence — nobody can reach either.
`WebMatchService.AptitudeChannelMods` hardcodes `AptitudeAllocation.Empty`. This module is the
narrowest slice that changes that: **commander scope only**, reachable from an already-shipped,
already-player-reachable flow (Expeditions), not a new "fight now" feature.

**Why commander scope only, not all four:** `DemonType`/`UniqueDemon` scopes need a specimen-selection
UI (nested inside `CreaturesLayer`, per the map-review agent's own finding — no doc decides this yet)
and `Aspect` is externally blocked on the demon program's `element_mastery` (decision 10). Commander
scope needs neither: its source is `Θ_player` (already computed server-side via the already-registered
`IPowerIndexProvider`/`ServerPowerIndexProvider`), its key is just the player id, and it already applies
to "every demon you field" — the widest-impact, simplest-to-reach scope, matching this whole program's
own "ship what's unblocked, light up the rest later" pattern (decision 10's own 3-of-4 shape for
`point-economy` itself).

**Why this closes real ground, not a demo:** `ExpeditionService.DispatchAsync`/`CollectAsync`
(`src/FusionRpg.Server/ExpeditionEndpoints.cs`) already call `WebMatchService.BuildSquad` →
`WebMatchService.ResolveAndIngest` → `BattleReportEmitter.Emit` → `RpgStore.InsertWebMatchEvents` for
every expedition battle a real player already runs, through an already-shipped UI
(`ExpeditionsLayer.tsx`). Once `AptitudeChannelMods` reads a real commander-scope allocation instead of
`Empty`, **every expedition battle from that point on carries real aptitude signal** — no new battle
trigger needed.

---

## 2. Backend — `src/FusionRpg.Server/AptitudeEndpoints.cs`

New file, `MapAptitudes(this WebApplication app)`, registered in `Program.cs` alongside the other
`MapX()` calls. Matches `PatronEndpoints.cs`'s own shape (`MapGroup("/api/aptitudes")`, `{ reason }`
error convention, `playerId ?? store.GetCurrentPlayerId()` + `PlayerExists` 404 guard, best-effort
SignalR broadcast via `IHubContext<RpgHub>` on the write path).

```
GET  /api/aptitudes/{playerId}
POST /api/aptitudes/allocate   { playerId?, shares: { <aptitudeId>: <long> } }
```

**GET response**: `{ theta, budget, spent, withinBudget, shares: { <aptitudeId>: <long>, ... } }` — all
twelve aptitude ids present (zero if unset, never omitted, matching `AptitudeAllocation`'s own "empty
means all-zero, never `1/12`" contract).

**POST**: builds an `AptitudeAllocation` from the body's `shares` via repeated
`AptitudeAllocation.Single(Commander, id, points)` summed with `+`; rejects an unknown aptitude id or a
negative point value the same way `AptitudeAllocation.Single` itself does (`ArgumentException`/
`ArgumentOutOfRangeException` caught → `400 { reason }`); checks `PointBudget.CheckScope` and returns
`409 { reason = "aptitudes.overbudget" }` if `!WithinBudget` — **never silently clamps** (PS-8: a budget
is not a cap, and neither is this check — it refuses the write, it does not truncate it). On success,
`RpgStore.SaveAllocation(Commander, $"player:{playerId}", allocation)`, then returns the same shape as
GET.

**`Θ_player`**: `sp.GetRequiredService<IPowerIndexProvider>().ActorIndex(new StatContext { PlayerId = playerId })`
— reuses the already-registered `ServerPowerIndexProvider` (`Program.cs:105-106`), no new Θ computation.

**Not built here, deliberately**: respec. `RespecPolicy.PriceOf` exists and is tested, but spending the
priced resource (`hunger`, per `RespecPolicy.cs`'s own `RespecResource` placeholder) needs a real
resource-spend seam this endpoint doesn't otherwise touch. A player can already change their allocation
freely by POSTing a different `shares` body — that IS "respec, always available, never on a cooldown"
(§3's own three-NOTs), just not yet priced. Named as an explicit, narrow follow-up, not silently
dropped.

---

## 3. The wiring change — `WebMatchService.AptitudeChannelMods`

`AptitudeChannelMods(int level)` → `AptitudeChannelMods(int level, long playerId)`. Reads
`_store.LoadAllocation(AllocationScope.Commander, $"player:{playerId}")` instead of hardcoding
`AptitudeAllocation.Empty`. The one caller (`BuildSquad`'s own `ChannelMods` concat, already threading
`playerId` through) passes it. **Zero behavior change for a player who has never allocated** —
`LoadAllocation` on an unset key returns `AptitudeAllocation.Empty` by the store's own already-tested
contract (`AllocationStoreTests.cs`: "load never saved returns empty"), so this is exactly as inert as
today until a real allocation exists, matching every other "wired but starts empty" seam this whole
program has shipped (P2.4's `AptitudeSubsystem`, P2.5's own original `AptitudeChannelMods`).

---

## 4. Frontend — a new layer, not a route

Per `web/spec.md`'s own hard rule ("never a route for something that is a layer"): a new
`src/layers/aptitudes/AptitudesLayer.tsx`, wrapped in `PanelShell`, registered in
`SanctumStage.tsx` (lazy import + `mountedLayers` gating) and `railState.ts` (`RailLayerId`, label,
keybind, unlock condition — always unlocked, matching a "primary stats" concept's own low gate).

**Data**: `useAptitudeAllocation(playerId)` (GET) and `useSaveAptitudeAllocation()` (POST mutation) in
`src/lib/bus/queries.ts`/`mutations.ts`, matching every other feature's `useX()`-only convention —
`AptitudesLayer.tsx` never imports the raw REST DTO directly (the `CreaturesLayer.tsx`-established
rule).

**UI**: twelve `NumberInput`s (one per aptitude, `src/ui/NumberInput.tsx`), a budget bar
(`StatBar`/`KpiStat`, matching the existing kit) showing spent/budget, a Save action disabled when
`!withinBudget` with a `title` explaining why — mirrors `PactsLayer.tsx`'s own affordability-gating
pattern exactly (`disabled` + explanatory `title`, not a silently-vanished button).

---

## 5. Testing strategy

- **Backend logic**: `tests/FusionRpg.Server.Tests/AptitudeEndpointsTests.cs` — same in-process
  `WebApplication` pattern already proven this session in `RealRunCollectorTests.cs` (a real, live host,
  not a mock), covering: GET on an unset player returns all-zero shares; POST within budget round-trips
  through GET; POST over budget returns 409 and does not save (re-GET still shows the old state); POST
  with an unknown aptitude id returns 400; the wired `AptitudeChannelMods(level, playerId)` reflects a
  saved allocation (was `Empty`, now carries real shares) — the actual end-to-end proof this task exists
  for.
- **Frontend**: a vitest test for the new layer/hooks, matching this repo's own existing web test
  conventions (loading/error/empty states, the affordability-gated Save button).

## 6. Boundaries

**Always** — reuse `PointBudget`/`AptitudeAllocation`/`AllocationStore` as-is, no re-derivation; keep
SQL inside `FusionRpg.Data` (already true — this module only calls existing `RpgStore` methods);
`shares` all-twelve-present in every response.

**Never** — clamp an over-budget POST (refuse it, PS-8); add a second Θ computation (reuse
`IPowerIndexProvider`); build a squad-picker or a new battle-trigger (Expeditions already is one).

**Ask first** — pricing respec for real (needs a resource-spend seam this module doesn't otherwise
need); `DemonType`/`UniqueDemon`/`Aspect` scope UI (a specimen-selection design fork nothing has decided
yet).
