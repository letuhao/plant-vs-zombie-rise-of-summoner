# Spec: loam-ai-survival (wave 2)

**Status:** **Sealed 2026-08-23** — owner-approved.
 Module id `loam-ai-survival` in the
[loam capability map](../loam-map.md). Depends on `loam-turn`.
**Why it exists:** map §7 finding **S5** — without it, the gate playtest shows a collapsing opponent
and the owner is asked to judge the mechanic through that.

## Objective

Stop Zomboss dissolving. **One rule and one lever**, deliberately not a loam strategy.

Success looks like: across a hundred turns on the gate map, Zomboss's territory does not shrink to
nothing through arithmetic alone, and any handicap he is given is stated in the report rather than
hidden in the numbers.

## Design

### Why this bends the ordering rule, and why that is fine

The capability map says **maps before AI**, learned when W37 predicted `first-light` would
under-exercise the AI and it did. That rule is about **not tuning** a policy against a map that cannot
exercise it. A survival reflex is not tuning — it is the difference between an opponent and a
demonstration.

The failure it prevents has already happened once here: *"Zomboss had a faction, a fortress and no
army."* Every suite passed, the AI worked, and there was simply nobody to be. An opponent whose empire
evaporates while the owner is trying to judge whether holding ground is interesting is the same defect
wearing a new costume.

### The one rule: `Abandon`

Added to `FrontierRulesPolicy`'s existing ordered chain, high — above `Expand`, below `Defend`:

> **Do not keep what you cannot sustain.** If a component's balance is negative and its stock will not
> outlast a stated horizon, release the weakest contributor deliberately instead of letting it fade.

Releasing early rather than fading is better on three counts: the component stops paying upkeep on it
immediately, the ground goes to `Lost` cleanly instead of decaying, and the turn report carries a
*reason* — the AI's audit trail is what separates a mistake from a bug, and W38 already requires every
AI order to say why.

**It reads belief, never truth.** `WorldDeterminismGuardTests` fails any file under `World/Ai/` that
mentions `WorldState` — a guard that has been seen to fail, so the belief-side overloads
`loam-calc` ships from day one are not a nicety here, they are the only way this compiles.

### The lever: `UpkeepHandicapMilli`

Owner, 2026-08-23: *"add some cheat/balance for him if building the AI is so hard for now."*

**It is a handicap, never a cheat**, and the naming is not pedantry:

- `FusionRpg.CheatCore` already owns "cheat" for debug tooling. A second meaning poisons every search.
- A hidden fudge **cannot survive replay**. Anything that changes the numbers must be in the hashed
  state or the command log, or `(seed, template, command log)` stops reproducing the game.

So it is `WorldFaction.UpkeepHandicapMilli` (`loam-model`): hashed, replayed, 1000 = normal, applied
inside `LoamUpkeep`, and **named in the turn report whenever it is not 1000**.

> A visible handicap is a balance lever. A silent one is a bug that explains itself away — and on a map
> where we are trying to find out whether the economy works, that is the single most expensive thing
> that could be sitting in the code.

**Upkeep discount, not a production bonus.** A discount makes Zomboss *resilient to bad decisions*,
which is exactly the gap a one-rule policy leaves. A production bonus would make him richer and would
mask whether the economy works at all.

### What this module is not

No loam value axes, no `Sever`, no march gate, no tuning. Those are `loam-ai`, after the gate, when
there is a played map to tune against. **If this module grows a second rule, it has become `loam-ai`
early** — and it will be doing it against a map nobody has judged yet.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Ai
dotnet test tests\FusionRpg.Guard.Tests
dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~World
```

## Project structure

```
src/FusionRpg.Core/World/Ai/FrontierRulesPolicy.cs   → the Abandon rule, one place in the chain
src/FusionRpg.Core/World/Loam/LoamPolicy.cs          → the abandonment horizon constant
tests/FusionRpg.Core.Tests/World/Ai/AbandonRuleTests.cs
scripts/mutants/world-ai.json                        → extended for the new rule
```

## Code style

Belief-side only under `World/Ai/`. Integer math. The rule returns a `PolicyOrder` carrying a reason
string like every other rule in the chain.

## Testing strategy

**The ordered-chain trap applies here and it has bitten before.** W35 found five vacuous tests of
exactly this shape: *to test rule N you must build a world where rules 1..N−1 all decline.* Five
mutants survived because Take's guard was never exercised — Finish always answered first. So the
`Abandon` tests must each construct a world where `Defend` and `Finish` both decline, and that
construction is stated in the test rather than assumed.

Named cases:

- **It fires** — a hopeless component releases its weakest sector, with a reason.
- **It does not fire** — a component in surplus keeps everything, including its worst sector.
- **It picks the right one** — the released sector is the worst net contributor, not the first by id.
- **Defend wins** — a threatened sector is defended rather than abandoned, proving position in the
  chain.
- **The handicap is announced** — a non-default handicap appears in the report exactly once.
- **A hundred turns** — Zomboss's territory does not reach zero on the gate map. The property this
  module exists for, asserted rather than eyeballed.

**Mutation:** the existing `world-ai` set is extended, and it must run on a **verified-green
baseline** — an earlier *"all 22 caught"* was false because a concurrent stream had `Core`
uncompilable and `dotnet test` exits non-zero for build failures too.

## Boundaries

- **Always:** belief-side only; one rule; a reason on every order; the handicap named in the report.
- **Ask first:** a second rule (that is `loam-ai`); any handicap other than upkeep; changing where
  `Abandon` sits in the chain.
- **Never:** `WorldState` under `World/Ai/`; a silent handicap; tuning constants against the gate map
  before the gate has been played.

## Success criteria

1. Zomboss's territory survives a hundred turns on `two-hearths` without a handicap, or the handicap
   needed is recorded as a finding rather than quietly applied.
2. Every named case passes, each failing if the rule is removed.
3. The extended mutant set is fully caught on a verified-green baseline.
4. No golden moves — this adds a policy, and `first-light`'s scenarios commit explicitly for Zomboss,
   which suppresses the AI fill. **Verify this by dumping the command log both ways** rather than
   assuming it, exactly as W37 did when that assumption turned out to hide a real bug.

## Decided (2026-08-23)

- **The player gets the same reading as advice, never as an action** — `loam-fe` marks what the engine
  will release next turn and why. Showing the player less than the AI acts on is a fairness problem;
  acting for them is a different game.

## Still open

- The abandonment horizon (how many turns of shortfall before releasing). A constant in `LoamPolicy`,
  found by the harness rather than chosen here.
