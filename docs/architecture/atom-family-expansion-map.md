# Atom family expansion — capability map

Source: `docs/architecture/atom-family-expansion-ideal.md`. Two independently testable modules, no
dependency between them — each closes a distinct subset of the 103 `FamilyExpandGen --check` refusals
measured live this session (`112 families read, 45 row(s) emitted, 103 family(ies) refused`).

## Modules

| Module id | Closes | Depends on | Risk |
|---|---|---|---|
| `tier-bands-coverage` | 98 of 103 refusals (`no authored sharePermille`) | nothing new | Low — pure data-authoring via an existing, already-built CLI (`seedsmith numerics rebalance`); introduces one new formula (powerBand → `channelWeightPermille`), reviewed in the module's own spec |
| `battle-ruleset-curve-extension` | 5 of 103 refusals (`no referenceBaseGameUnits`) | nothing new | Medium — adds 5 new rows to `ssot-power-scale.md`'s closed §10 inventory (a reviewed change per CLAUDE.md's "One power ladder" rule), touches `BattleModels.cs`'s `BattleRuleset` class (there is no separate `BattleRuleset.cs` file), and must handle `attackInterval`/`produceInterval`'s inverted-meaning semantics under an `Increased` op correctly, not just add a `Flat` reference base and call it done |

## Dependency graph

```
tier-bands-coverage        battle-ruleset-curve-extension
  (independent)                  (independent)
```

Neither module reads the other's output. `FamilyExpandGen` is re-run once at the end of whichever
module(s) are built, picking up whatever coverage exists at that point — a partial state (only one
module built) is safe and simply leaves the other module's refusals in place, exactly as they are
today.

## Build order rationale

**`tier-bands-coverage` first.** It closes 98 of 103 refusals (95%) with a small, low-risk, purely
additive change (one CLI invocation plus a documented formula), using tooling that already exists in
full (`seedsmith numerics rebalance --publish`, `FamilyExpandGen`). `battle-ruleset-curve-extension`
closes the remaining 5 but requires a reviewed change to the closed power-scale inventory and a real
design decision about how a magnitude curve backs an inverted-meaning (cooldown-shaped) channel
correctly — worth doing, per the owner's explicit choice, but strictly higher-risk and lower-yield per
unit of work. Building the cheap, high-yield module first means the corpus goes from 9→107 expanded
families before the harder module even starts.

## Not re-specced here

- **Stage-Define family-definition authoring** (`affixfamgen`) — already built, already closes 100% of the named-
  family gap (112/112 needed ids exist). Nothing to spec.
- **`FamilyExpandGen`/`FamilyExpansion.cs` themselves** — already built, already general, verified live
  this session to sweep all 16 group files correctly. Nothing to spec; both modules below only *feed*
  it new coverage, never modify it.
