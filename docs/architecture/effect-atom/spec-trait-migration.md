# Spec: trait-migration (E12)

Module **E12** in the [atom effect map](../effect-atom-map.md). Depends on **E11**, **E14b**. **Also re-opens `stat.derived` for battle** — D6 quarantined the kind to `None/None/None` because nothing consumes it; this module ships the first consumer (`BattleStatComposer` at squad build) and flips the battle cell. Without that, its own bind is rejected `RuntimeUnsupported`. **Also re-opens `stat.derived` for battle** — D6 quarantined the kind to `None/None/None` because nothing consumes it; this module ships the first consumer (`BattleStatComposer` at squad build) and flips the battle cell. Without that, its own bind is rejected `RuntimeUnsupported`.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

> **⛔ Checkpoint E — goldens move.** `RulesetVersion` bumps and battle goldens re-bless. Needs a written predicted delta and **owner sign-off**, and must not collide with the battle-timeline gate.

## Objective

Migrate the battle traits that the atom vocabulary can actually express, and state plainly which ones it cannot — because that list is shorter than the map assumed.

## Design (locked on approval)

### ⚠️ Scope correction: **1** of 14, not 7 of 13

The map says *"the 7 funnel-routed traits become containers of atoms."* Two things are wrong with that, both found by the 2026-08-22 sweep.

First, there are **14 traits, not 13** — a deliberate 7 FunnelRouted / 7 EngineBehavior split.

Second, and more consequential: **`FunnelRouted` does not mean atom-expressible.** The enum classifies which traits the contracts module layers obedience onto — the catalog says so — not the literal code path. Checking each against the 12 kinds:

| Trait | Mechanic | Migrates? |
|---|---|---|
| `regenerator` | `RegenPerRoundMilli = 20` of MaxHp per round | ⚠️ **blocked** — `OnTimer` is an injector ms scheduler, and battle never calls `OnEvent`; the runner is injector-thread only. Needs a battle consumer |
| `soul-eater` | `OnKillHealMilli = 100` of MaxHp per kill | ⚠️ **blocked** — same reason (`OnDeath` in battle) |
| `critical-hunter` | `ChannelMods` crit rate **+150** points | ✅ `stat.derived` on `combat.crit.rate.omni` |
| `berserker` | `damage × BerserkerRampMilli / 1000` on **resolver output** (`BattleEngine.cs:369–372`) | ❌ **no kind multiplies outgoing damage** |
| `guardian` | two-slice split: target takes `damage − share`, guardian takes `share`, each through its own shield gate (`:389–398`) | ❌ **damage redirect is the applier layer** |
| `swift` | `InitiativeBonusMilli` subtracted from the initiative roll | ❌ **turn kernel** |
| `immortal` | `DeathRefusalCharges` — survive at 1 HP | ❌ **death interception; no kind** |
| the 7 `EngineBehavior` | targeting, retreat, loot and XP multipliers | ❌ **AI and rewards layers** |

**So E12 migrates one trait: `critical-hunter`.**

It is the only one that survives, because `stat.derived` ChannelMods are merged at **compose** time — a path battle already runs — while `regenerator` and `soul-eater` need event dispatch that battle does not have. The earlier claim of three was wrong: it treated `OnTimer`/`OnDeath` as available in battle when this program's own specs say battle never calls `OnEvent` and the runner lives on the injector thread.

The other thirteen are blocked on layers that do not exist yet — and that is the correct outcome, not a shortfall. Inventing `damage.multiply` and `damage.redirect` kinds to force the other four through would break the 12-kind ceiling to serve four content rows, and would put damage-merge semantics in the atom layer where §5.1 of the ideal explicitly refuses them.

### The facet flags are decorative — migration is not "move a number"

`GuardsAdjacentAlly` and `TargetsLowestHp` are declared fields that **nothing reads**. The engine dispatches on hardcoded id strings — `attacker.Has("bloodthirsty")` at `BattleEngine.cs:504`, and `FindAdjacentWithTrait(actors, target, "loyal")` at `:525` (plus `"guardian"` at `:389`).

So for the engine-behaviour traits, adoption is not moving a magnitude into a row; it is replacing an id-string branch with a declared behaviour. That is the AI layer's job, and this spec records it so nobody scopes it as trivial.

### What actually changes in battle

One trait stops reading `TraitBattleCatalog` and starts reading a bound container. Its number is unchanged — `+150` crit-rate points — so the **predicted delta is zero**.

**How battle reads a binding at all** must be specified before this runs: E12's structure previously said "`BattleEngine.cs` (3 read paths swap to bindings)", which is a fourth consumption path bypassing both the compiler and the runner and appearing in no spec. For `critical-hunter` the answer is narrow — `BattleStatComposer` reads bound `stat.derived` atoms at squad build, the same place it reads `ChannelMods` today — and that is the only path this module adds.

That prediction is the sign-off document. If a golden moves by anything other than serialisation shape, the migration is wrong and stops.

### Why the version still bumps

Even at zero behavioural delta, the report's `contentHash` changes (trait magnitudes are now content) and the trait source changes. Per the program's own rule, that is a **visible** re-bless with a written prediction — which is the entire reason the content hash exists.

### Gate collision

The battle-timeline stream also moves goldens. Two re-blesses landing together makes attribution impossible. **E12 must not run in the same window** — sequence it, and state which stream re-blessed which goldens.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|Trait"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Expedition"
```

## Structure

```
data/seed/containers/trait-critical-hunter.json         (1 trait as a container)
src/FusionRpg.Core/Battle/TraitBattleCatalog.cs         (1 entry retired; 13 remain)
src/FusionRpg.Core/Battle/BattleStatComposer.cs         (reads bound stat.derived atoms at squad build)
tests/FusionRpg.Core.Tests/Battle/TraitMigrationParityTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| `critical-hunter` crit rate | still +150 points; sigmoid outcome unchanged |
| `regenerator`, `soul-eater` | **untouched** — still read `TraitBattleCatalog` |
| Fixture reports | now carry `contentHash` (moved here from E11, since a new stamped field is a golden diff) |
| Whole-battle goldens | **zero delta** except the stamp — anything else stops the wave |
| Expedition manifests | unchanged; the 4 tier hashes hold |
| The 13 unmigrated traits | untouched, still read `TraitBattleCatalog` |
| `RulesetVersion` | bumped once, with the predicted delta recorded |
| Timeline-gate overlap | none — asserted by sequencing, stated in the sign-off |

## Boundaries

**Always:** write the predicted delta before re-blessing; migrate only `critical-hunter`; keep the other thirteen reading the catalog.

**Ask first:** anything that moves a golden beyond the stamp; re-blessing in the same window as the timeline stream.

**Never:** invent a kind to force `berserker` or `guardian` through; put damage-merge semantics in the atom layer; treat the `FunnelRouted` flag as proof a trait is atom-expressible; migrate a trait whose trigger battle cannot fire.
