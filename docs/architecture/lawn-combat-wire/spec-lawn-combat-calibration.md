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

### One of these is a hard blocker, not a deferral

```json
// data/tuning/action-shares.v1.json — keyed by ATOM FAMILY
{ "atom.strike": 1000, "atom.fireball": 1000, "atom.poison-rider": 300 }
```

`atom.fx-overlay-damage` — the family the authored basic attack uses — **is absent**, and
`ActionShareTable` *"rejects rather than defaults"* (`ActionShareTable.cs:13`, per
`spec-action-seeding.md` §2/§7). **The feature throws on first use without this row.** It is not a
balance nicety.

## The four numbers

| # | Number | Home | Status |
|---|---|---|---|
| 1 | `sharePermille` for `atom.fx-overlay-damage` | `data/tuning/action-shares.v1.json` | **missing — runtime rejection** |
| 2 | `stamina` cost per swing for `act.attack` | Kind-aware cost template (`basic-attack-seed`) | missing |
| 3 | Lawn `stamina` regen rate (per-mille/tick) | `data/tuning/battle-resources.v1.json` | missing; blocked until `resource-subtick` makes it expressible |
| 4 | Lawn `resource.max.stamina` | derived, `poolShareMilli` | **already derivable** — `BaseHp(Θ) × poolShareMilli/1000`; only the seeding is missing (`basic-attack-cost` wire 2) |

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

- [ ] `atom.fx-overlay-damage` has an authored `sharePermille`; `ActionShareTable` no longer rejects.
- [ ] The chosen share's arithmetic and resulting TTK-versus-vanilla are recorded in `_meta`.
- [ ] `stamina` cost and lawn regen are authored, and satisfy `cost ≤ regenPerSecond × 1.5 s` at the
      pin.
- [ ] Exhaustion is reachable under burst fire — live-proof 4's falsifier can actually run.
- [ ] Every value marked `UNMEASURED` and traceable to a named anchor.
- [ ] No exact-damage assertion anywhere in the suite.
