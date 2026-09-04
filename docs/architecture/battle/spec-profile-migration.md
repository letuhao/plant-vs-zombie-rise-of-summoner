# Spec: profile-migration

Module id `profile-migration` (T15) in the [battle timeline map](../battle-timeline-map.md). Depends
on T4 `mode-profiles` and T5 `kernel-adoption` (both shipped) and on T14 `timeline-tunables` for the
profile defaults it will publish. **Written 2026-09-04**, from the owner decision recorded in
`decisions.md` **Battle engine open questions (2026-09-04)**.

## Objective

Move expeditions and web matches from `classic-round` to `hybrid-atb`, so `turn.speed` and
`turn.haste` matter in production for the first time. The readiness kernel has been built, tested and
shipped for a week while every battle that actually runs ignores it — `classic-round` pins readiness
to a constant by design. This module is what makes the kernel load-bearing.

**It is the only module in the program that deliberately moves the economy**, and it should be read
with that in mind: everything below is arranged so the movement is *attributable* rather than merely
observed.

## What actually changes — four axes at once

`classic-round` → `hybrid-atb` is not one switch. Read from `BattleModeProfileCatalog.cs:67-100`:

| Axis | `classic-round` | `hybrid-atb` | Why it moves win rates on its own |
|---|---|---|---|
| Advance policy | `NextEvent` | **`FixedIncrement`** | Time steps rather than jumps; sub-tick coincidences resolve differently |
| Concurrency `W` | 1 | **4** | Up to four actors mid-action at once — the biggest structural change here |
| Commitment | `LateBound` | **`EarlyBoundWithFallback`** | Targets lock at *schedule* time, not resolve time. A target that dies in between takes the fallback path instead of being re-picked |
| Economy | `OneActionPerTurn` | **`ActionPoints(2)`** | Two actions per round instead of one |

**All four are independently significant, and a single sweep cannot attribute a delta to any of
them.** That is this module's central design problem, and §2 is the answer to it.

⚠️ **`EarlyBoundWithFallback` interacts with a shipped behaviour.** `decisions.md:43` records that
battle's live targeting is `StubIntentSource.TryDeclare` with a `BloodthirstyView` decorator that
reorders `LiveActorKeys` so the lowest-HP live enemy sorts first. Under late binding that read happens
at resolve; under early binding it happens at schedule. **A bloodthirsty attacker will therefore pick
a different target than it does today** — not a bug, a direct consequence, and it must appear in the
predicted-delta write-up rather than be discovered in a moved golden.

## Design

### 1. The mechanical change is four rows

`WaveDef.Profile` already exists and already resolves — `WaveCatalog.Get(waveId).Profile ?? classic-round`
via `BattleModeProfileCatalog.Resolve`. All four authored waves pass `Profile = null` today
(`WaveCatalog.cs`: `rift-skirmish`, `rift-warband`, `rift-onslaught`, `rift-tyrant`). The migration is
setting that argument.

**⛔ The constraint that governs everything else in this module.** `WaveDef.Profile`'s own doc comment:

> *"**Never reaches `BattleSetup`** — the profile is looked up from the existing `WaveId` at resolve
> time, never serialized (a field on `BattleSetup` would move all four expedition hashes; named a
> 'Never' in both `battle-timeline-map.md` and `spec-mode-profiles.md`)."*

So the profile is free of serialization cost, and **per-wave `W` must be carried the same way** —
see §3. Anything this module puts on `BattleSetup` moves four hashes for no gameplay reason.

### 2. The staged sweep — one axis at a time

The re-bless is a one-way door: once four goldens move together, nobody can say afterwards which axis
moved them. So the sweep runs **five configurations, not two**, using `tools/CombatSim` (`sweep`,
`compare`, `matrix`) — the same tool and the same discipline that produced `decisions.md:99`'s
mitigation-shape findings over 50,000 fights each.

| # | Configuration | Question it answers |
|---|---|---|
| 0 | `classic-round` | The baseline, re-measured now rather than assumed from an old run |
| 1 | baseline + `FixedIncrement` only | Does stepping vs jumping move anything at all? |
| 2 | (1) + `W = 4` | What does concurrency alone cost or buy? |
| 3 | (2) + `EarlyBoundWithFallback` | The bloodthirsty-retarget delta, isolated |
| 4 | full `hybrid-atb` (+ ActionPoints(2)) | The shipped configuration |

**Each stage is a temporary profile built for measurement, not a shipped catalog row** — no new ids
enter `BattleModeProfileCatalog`, which would make the mode vocabulary lie about what the game
supports. Stage 4 is the only configuration that ships.

**Acceptance for the sweep is attribution, not a target number.** The deliverable is a table saying
"axis X moved the level-parity win rate by Y", with 0 → 4 summing to the observed total. If they do
not sum, there is an interaction and it gets named before the re-bless, not after.

Whether the resulting win rates are *good* is a balance question this module surfaces and does not
decide — it feeds the same `P(hit) = 0.90 ± 0.02` / `P(crit) = 0.05–0.10` acceptance
`combat-unification-map.md` decision 5 already established.

### 3. Per-wave `W`

Owner decision: `W` is content-configurable per wave. T14 deliberately publishes only the profile
*default* and leaves the per-wave override here, so `W` has one owner.

- `WaveDef` gains **`int? W = null`** — same optional-with-default shape as `Profile`, so the four
  authored rows need no edit and the change stays additive.
- Resolution is `wave.W ?? profile.W`, applied where the profile is already resolved. **Never on
  `BattleSetup`**, for the reason in §1.
- **Ships with no wave overriding it.** The four rows keep `null` in this module; authoring a
  strictly-serialized boss is content work and belongs to whoever authors that encounter. Shipping
  the mechanism inert is what keeps this module's own delta attributable to the profile switch alone.

### 4. Both surfaces migrate together — this is not optional

`WebMatchService.cs:39,50` calls `WaveCatalog.IsKnown` / `WaveCatalog.Get` on the same roster
expeditions use. **Expeditions and web matches share the wave definitions**, so setting
`WaveDef.Profile` moves both at once. There is no per-surface profile axis today.

The owner chose both surfaces, so this costs nothing here — but it is recorded because the "split:
expeditions `galaxy-sync`, web `hybrid-atb`" option that was offered would have required a new axis
(a profile override at the caller, or a surface field on the wave), not just a different argument.
**If the `FixedIncrement` measurement in §5 comes back expensive, that is the work the fallback
implies** — it is not free.

### 5. Two tasks that run before the migration

Both are gates on this module and neither is discretionary.

**(a) Close the `KernelPurityScan` hole.** The scan matches the `float ` / `double ` declaration
tokens, so `var x = 1.5f;` inside `Timeline/` slips past undetected — planted and verified during
B25, and left at the time as the owner's call because tightening a guard can redden unrelated files.
**Answered 2026-09-04: fix it.** Determinism is the foundation this module is about to lean on
hardest, and three modules are about to be built on the kernel. Expect the tightened scan to find
things; each one is triaged, not blanket-exempted.

**(b) Measure `FixedIncrement` resolve cost.** `hybrid-atb` is the only profile that steps rather
than jumps. A 50-round battle at `roundDurationMs = 1000` is on the order of 50,000 clock steps
against a few hundred event pops — **an estimate, never measured.** It matters because expeditions
resolve four battles server-side and the boot sweep re-resolves *every* unresolved match at server
start (`spec-virtual-time-core.md`).

- Measure: one expedition resolve and one boot sweep, `NextEvent` versus `FixedIncrement`, wall-clock
  and allocation.
- **If the cost is real, `galaxy-sync` for expeditions is the pre-agreed fallback** and needs no new
  decision — but per §4 it needs the per-surface axis, so it is a scope change, not a flag flip.
- If it is not real, say so with the numbers and move on. A measured non-finding is a finding.

## Commands

```powershell
# the staged sweep
dotnet run --project tools\CombatSim -c Release -- sweep --profile <stage> --fights 50000
dotnet run --project tools\CombatSim -c Release -- compare --baseline <stage-n> --candidate <stage-n+1>

dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~TimelinePurityGuard"
dotnet test tests\FusionRpg.Data.Tests
.\scripts\guard-single-writer.ps1 ; .\scripts\guard-funnel-delta.ps1
python scripts\audit-overflow.py ; python scripts\audit-magic-numbers.py --summary
```

## Structure

```
src/FusionRpg.Core/Battle/WaveCatalog.cs                (Profile on 4 rows; new optional W)
src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs (wave-W override in Resolve)
scripts/…KernelPurityScan                                (task (a): tighten the token match)
tests/FusionRpg.Core.Tests/Battle/                       (goldens, sweep fixtures, W resolution)
docs/research/battle/_sweep-hybrid-atb.md                (the attribution table — the real deliverable)
```

## Testing strategy

1. **Baseline first, and against the right number.** The tree carries **14 red Core and 2 red Data**
   tests owned by other streams (species-regeneration id renames; the half-landed `loamUnits`) —
   measured 2026-09-04, `tasks/item-todo.md`. **The bar is 14 and 2, not zero.** Re-measure at each
   checkpoint and compare against those, so a T15 regression stays distinguishable from inherited
   breakage. Guard and seedsmith are clean and therefore zero-tolerance.
2. **A predicted-delta write-up before any re-bless**, naming which goldens move and why — including
   the bloodthirsty-retarget consequence from §0. A golden that moves *unpredicted* stops the module.
3. **`W` resolution proven by contrast**: a wave with `W = 1` on `hybrid-atb` provably serializes
   where the profile default provably overlaps, in one test file — the same shape B12 already uses
   for `W=1` vs `W=2`.
4. **`W = null` changes nothing**: with no wave overriding, every report is identical to the profile
   default. This is what keeps §3's mechanism honest while it ships inert.
5. **The purity guard finds its planted case**: after task (a), `var x = 1.5f;` in `Timeline/` is
   caught. A guard fix asserted by nothing repeats B25's own "17 green, zero tests ran" incident.
6. **One re-bless, shared with B26.** Per `decisions.md` *Golden ordering across streams* —
   "freeze first, move last", movers land back to back under a single bump. `RulesetVersion`
   **4 → 5**, once, covering this module and the scaled injector clock together.

## Boundaries

- **Always:** stage the sweep; write the predicted delta before re-blessing; keep `Profile` and `W`
  out of `BattleSetup`; measure against 14/2.
- **Ask first:** shipping any wave with a non-null `W`; adding a per-surface profile axis (that is
  the §4 scope change); any re-tune of trait or ruleset magnitudes discovered by the sweep — those
  are `combat-unification` decision 5's, not this module's.
- **Never:** a new id in `BattleModeProfileCatalog` for a measurement stage; a second
  `RulesetVersion` bump for the clock; blanket-exempting files the tightened purity scan flags;
  re-blessing a golden whose movement was not predicted.

## Success criteria

1. Expeditions and web matches resolve on `hybrid-atb`, and `turn.speed`/`turn.haste` demonstrably
   change turn order in a production-path test. 2. The attribution table exists and its four stages
   sum to the observed total, or the interaction is named. 3. Per-wave `W` resolves and ships inert,
   proven both ways. 4. The purity hole is closed and the closure is asserted. 5. The
   `FixedIncrement` cost is **measured** and recorded, whichever way it comes out. 6. One
   `RulesetVersion` bump to 5, shared with B26, with every moved golden predicted in advance.
