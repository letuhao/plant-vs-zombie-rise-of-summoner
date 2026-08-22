# Spec: channel-extension (E16)

Module **E16** in the [atom effect map](../effect-atom-map.md). Depends on **E11**, **E9** (direction-aware pricing), **E14b** (the lower-is-better lint). Off the critical path — it touches the sealed stat path.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Promote `attackInterval`, `produceInterval`, and `zombieSpeed` from cheat-document keys to **real composed channels**, so fire rate, sun rate, and creep speed become authorable content. Primary channels go **8 → 11**.

## Design (locked on approval)

### The problem

These three are written directly by `EntityStatWriter.WritePlantExtras` / `WriteZombieExtras` from cheat keys (`P-ATK-INT`, `P-PROD-INT`, `Z-SPD-U`, …), **bypassing the modifier bag entirely**. No effect can reach them.

For a tower-defense game that is the genre's single most wanted affix — "shoots faster" — and it is currently impossible to author. The documented `channel` enum even *lists* them, which is how the gap survived: the docs promised a capability the code never had.

### The change

| Channel | Unity field | Side | Direction |
|---|---|---|---|
| `attackInterval` | `thePlantAttackInterval` | plant | **lower is better** |
| `produceInterval` | `thePlantProduceInterval` | plant | **lower is better** |
| `zombieSpeed` | `uniqueSpeed` | zombie | higher is faster |

Each gets: a `StatChannels` constant, a `StatComposer` case, an `EntityFinal` field, and an `EntityStatWriter` case. Nothing else in the compose path changes.

### ⚠️ Lower-is-better inverts the whole grammar

`Increased`/`More` on an interval make the plant **slower**. That is a trap for authors, for the power cost function, and for UI copy alike.

**Locked:** channels declare a **direction**, and everything downstream reads it.

- The cost function (E9) flips the sign for `LowerIsBetter` channels — otherwise `quickening` prices as a *penalty*.
- Content lint (E14b) warns on a positive `Increased` on a lower-is-better channel, which is almost always an author meaning "faster".
- A floor is enforced at compose: intervals clamp to a minimum above zero. A zero interval is a divide-by-zero or an infinite fire rate, depending on the call site — neither is shippable.

### The extras path must stop writing them behind compose's back

Once these are channels, `WritePlantExtras` / `WriteZombieExtras` writing the same fields from cheat keys would fight the composer, last-write-wins, nondeterministically.

**Locked:** the cheat keys become **`Override` modifiers** through `CheatAbsoluteStatPlugin`, exactly as `P-HP` / `P-ATK` already do — so the operator surface is unchanged, the single-writer law holds, and there is one path to the field. A guard test asserts the extras path no longer writes these three.

### Scope discipline

Only these three. The extras surface has ~18 cheat keys, and the rest (`P-SHIELD`, `P-LEVEL`, `Z-ARMOR-F`, `Z-TAKEMULT`, …) stay where they are. `Z-TAKEMULT` in particular is **LIVE-inconclusive** and must not be promoted on the way past.

**This does not fix G8.** Per-entity primary `defense` still waits for perf **O5** — the `TakeDamage` prefix reads a side-wide cached value, and making it resolve per-target is the uncached-per-hit-resolve pattern the perf audit blamed for combat lag.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Stat"
.\scripts\guard-single-writer.ps1
```

## Structure

```
src/FusionRpg.Core/Stats/ModifierOp.cs                (3 StatChannels constants + direction)
src/FusionRpg.Core/Stats/StatComposer.cs              (3 compose cases, interval floor)
src/FusionRpg.Core/Stats/EntityBaseline.cs            (3 EntityFinal fields)
src/FusionRpg.Injector/Stats/EntityStatWriter.cs      (3 write cases; extras path stops)
src/FusionRpg.Injector/CheatState.cs                  (cheat keys → Override map)
tests/FusionRpg.Core.Tests/Stats/ChannelExtensionTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| `Flat -0.2` on `attackInterval` | interval drops, plant fires faster |
| `Increased +0.5` on `attackInterval` | interval **rises** — and lint warned |
| Interval driven toward 0 | clamps at the floor, never 0 or negative |
| Cost function on `quickening` | prices as a **benefit**, not a penalty |
| `P-ATK-INT` cheat set | still works, now via `Override` |
| Extras path | no longer writes the three fields — guard test |
| `guard-single-writer.ps1` | passes |
| Existing 8 channels | unchanged; goldens unmoved |
| `Z-TAKEMULT` and friends | untouched |

## Boundaries

**Always:** declare channel direction; clamp intervals above zero; route cheat keys through `Override`; keep the single-writer law.

**Ask first:** promoting any other extras key; changing the interval floor.

**Never:** two write paths to one field; promote `Z-TAKEMULT`; let a lower-is-better channel price as a penalty; touch the `TakeDamage` prefix's defense caching here.
