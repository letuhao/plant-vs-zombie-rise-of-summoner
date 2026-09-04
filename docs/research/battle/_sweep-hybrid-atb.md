# classic-round → hybrid-atb — staged attribution sweep

**battle-timeline B34.** Measured 2026-09-04, after B37 made `BattleEngine.Resolve` actually read the
profile. Before B37 every stage below would have measured zero for the uninteresting reason that
nothing was read; the zeros here are real findings with verified causes.

## Why five configurations and not two

`classic-round` → `hybrid-atb` moves **four axes at once**: advance policy, `W` 1→4, commitment, and
turn economy. A single before/after sweep produces one number and no way to attribute it, and the
re-bless it feeds is a one-way door. So each axis is added in turn.

## Result

Squad win rate, 240 seeds, `CloseSetup` (the balanced fixture — a stomp or a wipe would floor or
ceiling the measurement and hide any axis).

| Stage | Configuration | Win rate | Delta |
|---|---|---:|---:|
| 0 | `classic-round` | 89.58 % | — |
| 1 | + `FixedIncrement` advance | 89.58 % | **0.00 %** |
| 2 | + `W = 4` | 89.58 % | **0.00 %** |
| 3 | + `EarlyBoundWithFallback` | 89.58 % | **0.00 %** |
| 4 | + `ActionPoints(2)` — full `hybrid-atb` | 87.92 % | **−1.67 %** |
| 5 + `OrdersBySpeed` *(B39)* → **`hybrid-atb`** | 87.92 % | **0.00 %** |
| | **total** | | **−1.67 %** |

**The deltas sum exactly to the total**, which is B34's stated acceptance. They are counted outcomes
over a fixed seed band, not sampled statistics, so the check is exact rather than tolerant.

## The finding: three axes are inert, and the economy owns the whole move

Each zero has a verified cause, not an unexplained one:

- **`AdvancePolicy`** — a batch resolve has no frames to step. Already documented on `Resolve`'s own
  `profile` parameter, and B32 measured the mechanism's cost at 1.2× for the case where it *does*
  matter (the injector's per-frame drive).
- **`W`** — cannot bind without wind-up. `ActionSlots`' own doc: *"W only binds when actions have
  wind-up: under next-event advance with a strict total order and atomic resolution, a battle is
  already serialized regardless of W."* The slot path is exercised on every action; it simply cannot
  refuse.
- **`Commitment`** — deliberately left unwired by B37, which deferred early binding to the migration
  that first selects a profile using it, so its golden delta can be predicted in the same pass.

**That is a good outcome for the migration, not a disappointing one.** A change whose entire effect is
one named axis is a change someone can actually predict and review — which is precisely what B35 needs
to write and B36 needs to re-bless against.

## Reading the −1.67 %

Both sides get two actions per round under `ActionPoints(2)`, so this is not "the squad got stronger".
The squad's win rate falls slightly because compressing the same fight into fewer rounds gives the
numerically larger side more of its extra actions before attrition thins it — the wave acts twice too.

Whether −1.67 % is acceptable is a balance question this sweep surfaces and does not decide. It feeds
the existing bar from `combat-unification-map.md` decision 5 (level-parity P(hit) 0.90 ± 0.02,
P(crit) 0.05–0.10), none of which this touches — no rate moved, only the number of actions.

## Reproducing

The sweep is a test, not a script that was run once:

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~HybridAtbSweep"
```

`TheFourAxesAttributeAndTheDeltasSumToTheTotal` prints the table above and asserts the sum;
`ThreeAxesAreInert_andTheEconomyOwnsTheWholeDelta` pins each zero individually, so an axis that
silently starts binding fails the suite instead of quietly changing the economy's attributed share.


## Stage 5 — added 2026-09-04 (B39)

B39 wired `turn.speed`/`turn.haste` into turn order, adding a fifth axis to `hybrid-atb`. Until this stage was added, the table above ended at stage 4 while still being labelled `hybrid-atb` — **it had stopped describing the profile production runs.** `HybridAtbSweepTests.TheFinalStageIsTheShippedProfile` now pins the final stage to `BattleModeProfileCatalog.HybridAtb` by measured result, so that drift cannot recur silently.

**Its delta is 0.00 %, and the reason is content rather than structure.** Readiness reorders a round only when speeds *differ*; no shipped content authors a `turn.speed`, so every actor clamps to the same `TurnDefaultSpeed`, every comparison ties, and ordering falls through to the same initiative jitter as before. The feature is live and inert.

⚠️ **This is the one zero in the table expected to stop being zero.** The other three are inert by construction; this one is fully wired and simply has nothing to order on yet. The day a content pass authors speed, the assertion goes red, the goldens move, and **that** is when a `RulesetVersion` bump is earned — not before.
