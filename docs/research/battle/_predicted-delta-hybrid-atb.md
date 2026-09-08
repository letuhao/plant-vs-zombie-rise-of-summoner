# Predicted delta — flipping the four waves to `hybrid-atb`

**battle-timeline B35**, the gate before any re-bless. Written 2026-09-04, after B34 measured the
attribution and B37 made the profile reachable.

The rule this exists for: *a golden that moves unpredicted stops the phase.* So this names what moves,
what does not, and why — in advance.

## The change being predicted

Setting `Profile = hybrid-atb` on the four authored `WaveDef` rows (`rift-skirmish`, `rift-warband`,
`rift-onslaught`, `rift-tyrant`). The resolution path from wave → profile → engine is wired
(`WebMatchService.ProfileForWave`, all three `Resolve` call sites) and is byte-identical today because
every wave still carries `Profile = null`.

## Prediction: **no golden moves.** The change is live-outcome only.

That is not a hope; it follows from which fixtures use which wave ids.

| Golden | Uses | Moves? | Why |
|---|---|---|---|
| `BattleGoldenTests` (3 hashes + 32-seed sweep) | `WaveId` = `"golden-stomp"` / `"golden-close"` / `"golden-wipe"` | **No** | Those ids are **not in `WaveCatalog`**, so `ProfileForWave` returns null and the battle resolves under `classic-round` exactly as today |
| `PreAdoptionTraceTests` | the same three fixtures | **No** | Same reason |
| `ExpeditionResolverTests.Tier_goldens_are_locked` (4 tier hashes) | `ExpeditionResolver.Resolve` | **No** | That hash covers the expedition **plan** — tick outcomes, battle *plans*, rewards — not resolved battle reports. Battles resolve later, at collect |

**So `RulesetVersion` does not need to move either.** The "4 → 5 bump shared with B26" that
`spec-profile-migration.md` budgeted is, on this evidence, **not required by the profile flip**. B26's
scaled clock is a separate mover and keeps its own claim on a bump.

## What does change: live outcomes

Web matches and expedition-collect battles resolve through `WebMatchService`, which reads the wave's
profile. Those battles would move by the amount B34 measured:

**−1.67 % squad win rate** (89.58 % → 87.92 %, 240 seeds, `CloseSetup`), **entirely attributable to
the turn economy.** The other three axes measured exactly 0.00 %, each for a verified reason —
`AdvancePolicy` has no frames in a batch resolve, `W` cannot bind without wind-up, and `Commitment`
is deliberately unwired by B37.

Both sides get two actions per round, so this is not "the squad got stronger". The squad's rate falls
slightly because compressing the fight into fewer rounds lets the numerically larger side spend more
of its extra actions before attrition thins it.

## What would invalidate this prediction

Stated so the prediction is falsifiable rather than reassuring:

1. **A golden fixture starts using a catalog wave id.** Then it would resolve under `hybrid-atb` and
   move. Today none does — `BattleGoldenTests` deliberately uses `"golden-*"` ids.
2. **`Commitment` gets wired.** B37 deferred early binding; wiring it adds the `BloodthirstyView`
   retarget (its lowest-HP read moves from resolve time to schedule time), which is a real behaviour
   change and its own predicted delta.
3. **`W` starts binding.** That needs wind-up, which is a separate module.

## Recommendation for B36

The flip is a **two-line content change** (`Profile = hybrid-atb` on four rows) whose entire predicted
effect is a −1.67 % live win-rate shift with **no golden movement and no version bump**.

Per `decisions.md`'s *Golden ordering across streams* — "freeze first, move last" — B36 was sequenced
to land back-to-back with B26 under one re-bless. **That coupling now looks unnecessary**, because
this mover does not move a golden. Whether to decouple it is an owner call: the ordering rule exists to
stop two movers being confused for each other, and a mover with no golden delta cannot be confused
with anything. Flagged rather than decided.
