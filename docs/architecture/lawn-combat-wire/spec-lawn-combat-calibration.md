# Spec: `lawn-combat-calibration`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `basic-attack-seed` (the row whose share is authored), `resource-subtick` (makes a
regen rate expressible)

> Added 2026-09-13. Every other spec correctly refused to invent balance numbers, and the emergent
> result was a feature with **no magnitude at all**. This module owns the numbers — by **deriving**
> them from shipped anchors, not inventing them.

---

## Objective

Give the lawn basic attack defensible starting values, with the derivation written down so a later
balance pass can redo it rather than reverse-engineer it.

**Calibration is not balance.** D1 keeps balance in a separate program, and that stands: this module
does **not** run win-rate sweeps or tune for feel. It answers the narrower question *"what number is
defensible on day one, and why"*, so the feature is playable and its numbers are traceable.

### ⚠ Correction — the first draft of this spec was wrong about its own headline

**It claimed `sharePermille` was a hard blocker that would throw on first use. It is not.**

`ActionShareTable` has **zero callers** in `src/` — only its own definition file
(`Actions/Seeding/ActionShareTable.cs`). It belongs to the **seeding** path, whose entry point
`ActionSeeder.Generate` also has zero production callers (`ItemGrantedActionRow.cs:111-119` asserts
`ActionCorpusProducerLanded = false`). Neither `BasicAttack.cs` nor `BattleRunState.cs` mentions
`sharePermille` at all.

**The basic attack's base damage does not come from a share.** It comes from the attacker's own atk:

```csharp
BaseOverlayDamage = attacker.LiveAtk(state.Ledger)                         // BasicAttack.cs:369
public long LiveAtk(…) => ledger.Recompose(Setup.Key, "atk", Setup.Atk);   // BattleEngine.cs:101
```

So `anchor(Θ) = sharePermille × P(Θ)/1000` is **not** the formula in play, and the
`atom.poison-rider = 300` precedent is irrelevant to this module. That error came from inferring a
call path from a component's existence instead of verifying it — the same mistake that produced the
first `lawn-action-bridge` draft, made twice in one hour.

## The numbers — three, now that the open question below is resolved

| # | Number | Home | Status |
|---|---|---|---|
| 1 | `stamina` cost per swing for `act.attack` | Kind-aware cost template (`basic-attack-seed`) | **missing — real** |
| 2 | Lawn `stamina` regen rate (per-mille/tick) | `data/tuning/battle-resources.v1.json` | **missing — real**; blocked until `resource-subtick` makes it expressible |
| 3 | Lawn `resource.max.stamina` | derived | **already derivable** — `BaseHp(Θ) × poolShareMilli/1000`; only the seeding is missing (`basic-attack-cost` wire 2) |
| 4 | `atom.fx-overlay-damage` amount | `data/seed/atoms/fx-core.json:33` (or the grant's overlay, or an `eventField` marker — the module's choice) | **missing — real, confirmed 2026-09-13** (see resolved question below): the atom resolves to a hardcoded `0` today, an independent magnitude to author |

### RESOLVED (2026-09-13): (a) — the atom needs an authored amount. Today it resolves to a hardcoded zero.

**Correction to this spec's own prior citation:** the line range `DamagePacketBuilder.cs:17-28` (quoted
below, and in `tasks/lawn-combat-wire-todo.md` Task 1) is **stale** — that range is `FromOverlay`'s
signature and packet construction. The method that actually decides the amount, `ResolveAmount`, is at
`DamagePacketBuilder.cs:63-83` in this checkout. Cite the method, not a drifted line number.

**The trace, file:line by file:line:**

1. `EffectBag.cs:507-514` — for the `ApplyResourceDelta` action, `FireGrant` calls
   `DamagePacketBuilder.FromOverlay(merged, ev, ...)` with **no `amountOverride`** (the parameter is
   left at its `null` default), so the amount is decided entirely by `ResolveAmount`.
2. `merged` comes from `EffectOverlayMerge.TryMerge` (`EffectProcAndOwner.cs:322-323, 331-344`): it
   copies `actionParams` (the atom's compiled row) into `merged`, then folds in `grant.Overlay` **only
   for keys `grant.Overlay` already contains**. It never invents an `"amount"` key.
3. The atom's actually-shipped compiled row — `EffectAtomCatalog.Generated.cs:205-222`
   (`EffectId = "fx.overlay_damage"`, `Action = "ApplyResourceDelta"`) — has
   `Params = { ["channel"] = "hp" }`. **No `amount` key, at the compiled/runtime level, not just the
   seed source** (`data/seed/atoms/fx-core.json:33-40` matches it exactly).
4. `basic-attack-grant`'s standing bind (`spec-basic-attack-grant.md`) never sets `grant.Overlay["amount"]`
   either — that spec's own "what the grant carries" section only bakes `elementPayload` and the
   `entity:{ptr}` owner key. The grant is bound **once at spawn** and never re-created per swing, so
   there is no per-hit call site that could inject a fresh `amount` into it even if one wanted to.
5. So `merged` reaching `ResolveAmount` (`DamagePacketBuilder.cs:63-83`) never contains an `"amount"` key
   at all. `ResolveAmount`'s first branch (`:65-80`, the P0.2 event-linked marker) is gated on
   `overlay.TryGetValue("amount", ...)` succeeding — it does not — so it is skipped entirely. Execution
   falls to the final line, `DamagePacketBuilder.cs:82`:
   `return (long)JsonOverlay.GetDouble(overlay, "amount");` — and `JsonOverlay.GetDouble`
   (`EffectModels.cs:234-238`) returns its `fallback` parameter (default `0`) when the key is absent.
6. Back in `EffectBag.cs:518-521`, the only re-read of `merged["amount"]` is itself gated on
   `merged.ContainsKey("amount")` — also false here — so nothing overwrites the zero.

**Verdict: as authored and as compiled today, `atom.fx-overlay-damage` resolves to a literal, hardcoded
`0` on every hit through the `basic-attack-grant` path.** It does **not** read the vanilla/RPG hit's own
damage automatically. This is answer **(a)** — a real, confirmed gap, not (b).

**One nuance worth recording so the next session doesn't reinvent it:** the mechanism that
answer (b) describes — reading the firing event's own `Damage` field — already exists and is exactly
built for this shape (`ValueSpec.EventField`/`P0.2`, `docs/architecture/effect-atom/spec-value-spec-and-curve.md`
"Event-linked magnitudes", **not** under `lawn-combat-wire/` — that doc-path citation in the todo/map
was also wrong). It is just not invoked by this atom: the seed row authors no
`"amount": {"eventField": "damage", "multiplierMilli": ...}` marker. Whoever authors the missing amount
for `basic-attack-calibration` has two legitimate shapes to choose between — a flat authored number
(closer to the `atom.poison-rider = 300` precedent already used elsewhere in this file) or wiring the
existing `eventField` marker so the rider scales off the vanilla hit's own damage — and that choice is
this module's to make, not something this task decides. What this task closes is only the factual
question of what happens **today**, unauthored: zero, always.

## Shipped anchors — derive from these, do not invent

| Anchor | Value | Source |
|---|---|---|
| Power pin | `BaseHp(Θ=20) = 680` | `power-scale.v2.json` `curve.pinValue` |
| Atk pin | `atk(Θ=20) = 92` | `power-scale.v2.json` `atk.pinValue` |
| Full strike share | `atom.strike = 1000` (100% of anchor) | `action-shares.v1.json` |
| **Rider share** | `atom.poison-rider = 300` (30%) | `action-shares.v1.json` — **the closest precedent; ours is a rider on a vanilla hit, not a replacement for it** |
| Pool share | `500‰` uniform → pool 340 at Θ=20 | `battle-resources.v1.json` |
| Pool sizing precedent | `reaction-lane` poiseSpend 100 ⇒ ~3 counters from a pinned pool | `battle-resources.v1.json` `_meta.sizing` |
| Cost is authored against **regen**, never max | — | `action-ideal.md:223` |
| Vanilla pea | 20 damage, `thePlantAttackInterval` **1.5 s** | measured live 2026-09-13 |
| Vanilla NormalZombie | 270 hp | measured live 2026-09-13 |

**Derived sanity check, stated so the calibration can be argued with:** a vanilla Peashooter needs
`270 / 20 ≈ 14` peas ≈ **20 seconds** to kill one basic zombie. Any rider that changes that to under
~2 seconds has made vanilla damage irrelevant; one that changes it to 19 seconds is decorative. The
starting share should land the combined time-to-kill somewhere a player would notice and a designer
would recognise as "my element matters", and the spec must state the resulting TTK it chose.

## Method

1. **Share (#1).** Start from the `atom.poison-rider = 300` precedent, since our basic attack is a
   rider layered on a vanilla hit. Compute the resulting per-hit rider at the pin (Θ=20) and the
   resulting TTK against a 270 hp zombie. Adjust only to land a defensible TTK, and **record the
   arithmetic in the tuning file's `_meta`**.
2. **Cost (#2) and regen (#3) together.** `action-ideal.md:223` — cost is authored against **regen**.
   A Peashooter swings every **1.5 s**, so on the 100 ms kernel the sustainable cost is
   `regenPerSecond × 1.5`. Pick regen first from the pool size (a pinned pool of 340 should sustain
   continuous fire for a plant that is *meant* to fire continuously), then set cost at or just under
   the sustainable rate, so exhaustion is a **burst-fire** consequence and not a permanent stop.
3. **Prove the exhaustion case is reachable but recoverable** — the falsifier in
   `lawn-combat-live-proof` proof 4 requires an actor that actually runs dry and actually recovers. A
   cost far under sustainable makes that proof unreachable; a cost far over makes every plant inert.
   **The numbers must make that proof runnable.**

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionShareTable"
python tools/tuning/resource_ownership.py --check
```

## Project structure

| Path | Duty |
|---|---|
| `data/tuning/action-shares.v1.json` | Gains the `atom.fx-overlay-damage` row (**or a v2 per the file's own rebalance convention**) |
| `data/tuning/battle-resources.v1.json` | Gains its regen rows, now that `resource-subtick` makes them expressible |
| Kind-aware cost template | Gains the Basic → `stamina` amount |

## Code style

Every number lands in **data**, never a `const`. Follow each file's own rebalance convention: several
say *"Never hand-edit this file"* and require `python tools/tuning/publish.py` writing a `v{n+1}`, with
the old version kept for revert. **Read the target file's `_meta.rebalance` before editing it** —
these conventions differ per file and are not decorative.

Every authored value carries, in `_meta`: the anchor it derived from, the arithmetic, and the word
`UNMEASURED`. This repo's posture, quoted: *"shipping a guess is fine, calling it balance is not."*

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | `ActionShareTable` resolves `atom.fx-overlay-damage` — the rejection path is gone |
| Core unit | The derived rider at Θ=20 matches the arithmetic recorded in `_meta` (a test that fails if someone edits the number without editing the reasoning) |
| Core unit | Sustainable-fire invariant: `cost ≤ regenPerSecond × attackInterval` at the pin, so a continuously-firing plant does not permanently dry out |
| Core unit | Exhaustion is still **reachable** under burst fire — otherwise live-proof 4 cannot run |
| BalanceGuard | Values are present and within the stated envelope; **no test pins an exact damage number** — that is a reading, not a contract |
| Live | `lawn-combat-live-proof` proofs 2 and 4 both become runnable only once these exist |

## Boundaries

- **Always:** derive from a named shipped anchor; record the arithmetic; mark `UNMEASURED`.
- **Always:** follow the target tuning file's own `_meta.rebalance` convention.
- **Ask first:** any value that cannot be derived from an existing anchor — that is a real balance
  decision and belongs to the balance program, not here.
- **Never:** a balance number in code; a test that pins an exact damage output; an immunity-style
  threshold; a second curve. Magnitude stays `anchor(Θ) × q(rung)` with `rungBand [1,1]`.

## Success criteria

- [x] **`ResolveAmount`'s behaviour for `atom.fx-overlay-damage` is read and recorded** — **(a)**,
      resolved 2026-09-13: as authored and as compiled, the atom resolves to a hardcoded `0` on every
      hit (no `amount` key anywhere in its compiled row or its standing grant's overlay reaches
      `ResolveAmount`); it does not read the vanilla hit's own damage. See "RESOLVED" above for the
      full trace. Everything else in this module depends on this, and it is now a fourth number (#4
      above) to calibrate, not a reduction to cost + regen.
- [ ] The authored amount's arithmetic and resulting TTK-versus-vanilla are recorded in `_meta`
      (the module still must pick a shape — flat authored number, or the existing `eventField`
      marker — before this can close).
- [ ] `stamina` cost and lawn regen are authored, and satisfy `cost ≤ regenPerSecond × 1.5 s` at the
      pin.
- [ ] Exhaustion is reachable under burst fire — live-proof 4's falsifier can actually run.
- [ ] Every value marked `UNMEASURED` and traceable to a named anchor.
- [ ] No exact-damage assertion anywhere in the suite.
