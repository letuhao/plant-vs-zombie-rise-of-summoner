# Spec: unlock-discard-endpoint (A28)

Module **A28** in the [action map](../action-map.md) §17. Reads
[action-playability-ideal.md](../action-playability-ideal.md) gap #3. Depends on **A26** (a real tuning)
and **A27** (same endpoint family, same review batch).

## Objective

`UnlockDiscardService` (T20, built, 12 tests, action-ideal.md §3.3) has **zero Server callers**. A player
cannot discard a held unlock today even once A26/A27 ship. This module gives it a real REST surface.

## Design

1. `POST /api/actors/{instanceId}/unlock/discard { "unlockId": "..." }`.
2. **Owner scope: `OwnerKind.UniqueActor`, not `Entity`.** Per A27's audit finding
   (`WebMatchService.cs:702-716`), unlock-ladder grants and earn history are the **durable** half —
   fixed 2026-09-07 specifically because `Entity` is session-scoped and would let a session boundary
   silently erase real progress. `GetUnlockState`/`SaveUnlockState` (`RpgStore.ActionUnlocks.cs:47,88`)
   must be called with `new OwnerScope(OwnerKind.UniqueActor, instanceId)`. This is the **opposite**
   scope from A27's loadout *slot assignment* (`Entity`) — the two modules are adjacent but read/write
   different scopes for different reasons; do not copy one endpoint's scope into the other.
3. **A specimen with no unlock state row yet** (fresh, zero earns): `GetUnlockState` returns an
   effectively-empty state (`earnCount = 0`, no held unlocks — confirmed by reading
   `GetUnlockStateUnlocked`'s own SQL, `RpgStore.ActionUnlocks.cs:56-67`: a missing row resolves to
   `earnCount = 0`, never throws). `TryDiscard` against an empty held set naturally refuses `NotHeld` —
   **no special-case code needed**, this is already handled correctly by construction.
4. Call `UnlockDiscardService.TryDiscard(state, unlockId, theta, tuning)`
   (`UnlockDiscardService.cs:29`), where:
   - `tuning` = `UnlockTuningPolicy.Tuning` (non-null once A26 ships; `500` if somehow null — should not
     happen post-A26, treat as a server error, not a silent no-op, since this endpoint has no reason to
     run before A26 does).
   - `trySpendSoul` = a **real** call into `RpgStore.Souls.TrySpendSouls(...)` (`RpgStore.Souls.cs:189`)
     — unlike `isMidRun` in A27, there is no honest-gap reason to stub this; the real soul ledger already
     exists and is exactly what `DiscardPolicy`/T20 was designed to spend from.
   - `theta` = **open integration point, confirm before implementing** (see Boundaries). `CostLedger`
     (T17) already established the precedent for this exact situation — an optional
     `thetaScaleMilliOf` seam with a documented "the real anchor formula is a follow-up, not decided
     here" caveat. This module should use whatever the repo's existing per-actor Θ reader is (the same
     one any other per-specimen `P(Θ)`-priced number already reads), not invent a second one.
5. On success, `RpgStore.SaveUnlockState(scope, state)` (`RpgStore.ActionUnlocks.cs:88`) persists the
   freed slot; the spent souls are already persisted by `TrySpendSouls` itself.
6. **Response echoes the new soul balance** — `TrySpendSouls` already returns a `SoulBalanceDto`
   (`RpgStore.Souls.cs:189`); include it in the `200` response so the FE (A30) can update its display
   without a second round trip, the same pattern any other spend-then-display flow in this codebase
   already follows.
7. **Idempotency / double-submit safety**: a retried discard of the same `unlockId` after a successful
   first call naturally refuses with `NotHeld` (the slot is already gone) rather than double-charging —
   this falls out of `UnlockState.TryDiscard`'s existing shape (§ above) and needs no extra guard code,
   but **assert it explicitly** as an acceptance line, since a REST endpoint retried by a flaky client is
   a real, common failure mode this program hasn't had to consider until now (T20's own tests only ever
   called the service directly, once per test).

## Tunables

None new. `discardTaxCoeffMilli` (`action-unlock.v1.json`, owned by A26) already prices this; this
module only makes the priced action reachable.

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~UnlockDiscard"
```

## Project Structure

```
src/FusionRpg.Server/UnlockDiscardEndpoints.cs      (new)
tests/FusionRpg.Server.Tests/Actions/UnlockDiscardEndpointTests.cs   (new)
```

## Code Style

Same request/response shape convention as `LoadoutEndpoints.cs`/A27's endpoint —
`Results.Ok`/`Results.Conflict`(typed reason)/`Results.NotFound`.

## Testing Strategy

| Case | Expect |
|---|---|
| Discard a real held unlock with sufficient soul | slot freed, soul balance decreases by exactly `DiscardPolicy.PriceOf`'s quoted amount, `EarnCount` unchanged (T20's anti-farm property, now provable end to end via a real balance read) |
| Insufficient soul | `409` typed reason, soul balance and `UnlockState` both unchanged |
| Discarding an unlock not held | typed refusal, no state change |
| Mid-run (once a real `isMidRun` oracle exists — until then, matches A27's placeholder) | refused, matching `UnlockState.TryDiscard`'s existing rule |
| Two discards in a row, both affordable, different `unlockId`s | both succeed independently (matches T20's existing test) |
| The **same** discard request retried after success (double-submit) | second call refuses `NotHeld`, no second soul charge — the idempotency property, asserted explicitly |
| A fresh specimen with no unlock-state row at all | refuses `NotHeld` cleanly, no exception (already correct by construction, per Design §3) |
| Successful discard response body | includes the post-spend `SoulBalanceDto`, not just a bare `200` |

## Boundaries

**Always:** spend real souls via `TrySpendSouls` — never a stubbed/no-op spend for a real currency the
game already tracks (this is not the same category of gap as `isMidRun`, which has no real oracle to
call yet; soul spending does).

**Ask first:** **the real Θ source for a specimen at this call site.** This spec does not invent one —
confirm with whatever the class-system/power program already exposes for a per-actor Θ read before
implementing, rather than deriving a new formula here (PS-4/PS-14: one power ladder, no private
`f(actor)`).

**Never:** build a second discard-pricing mechanism — `DiscardPolicy`/`UnlockDiscardService` already do
this, tested. This module is a caller, not a reimplementation.

## Success Criteria

1. A real player can discard a real held unlock over HTTP.
2. Real souls are spent, real state is persisted, `EarnCount` is provably untouched.
3. The Θ source question is resolved (not deferred again) before this module is marked done.
