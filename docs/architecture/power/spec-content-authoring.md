# Spec: content-authoring

Module **`content-authoring`**, wave 2 in the [power map](../power-map.md). Depends on **`power-index`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Owner approved 2026-08-24 — build authorized.

---

## 1. Objective

Re-author the game's authored content levels as **`Θ_content`**, so difficulty is expressed in the
same unit everything else reads.

## 2. Design

### 2.1 What actually exists — verified, and smaller than the docs claim

`ssot-generation.md` §4.1 lists four `contentLevel` sources. **Only one exists in code.**

| Doc claim | Reality |
|---|---|
| `web battle` → `WaveDef.RecommendedLevel`, 1/3/6/10 | **Exists.** `WaveCatalog.cs:32-35` |
| `expedition tick` → tier base level, scout 2 / forage 5 / hunt 9 / warpath 14 | **Does not exist.** `ExpeditionTierDef` is `(TierId, Name, DurationMinutes, TickCount, BattleCount, SquadSlots, HasBossWave)` — there is no level field |
| `expedition boss` → tier base `+3` | **Does not exist** — no field to add 3 to |
| `world sector` → `sectorLevel(danger_band)` | **Now specified**: `5 · DangerBand` (SSOT §5.3), derived from the shipped catalog |

**Expeditions already carry a content level — they inherit it through the wave chain.**
`ExpeditionResolver.cs:82-87` selects a `waveId` and reads `WaveCatalog.Get(waveId).Enemies`, whose
level came from `Enemies(level: N, …)`.

**This shrinks the module.** One authored level source, not four, and **no new expedition field is
needed**. The doc describes a design that a simpler one superseded and nobody updated.

### 2.2 The change

```text
WaveDef.RecommendedLevel  ->  WaveDef.ContentIndex     # same values, unit renamed
```

A rename plus a statement of intent, not a value change. The four waves stay at `1 / 3 / 6 / 10`;
they are now *indices*, which is what they always were. `BattleSetup.Level` likewise becomes the
actor's `Θ` — the thing `battle-magnitude` and `battle-rates` already consume.

### 2.3 Why a rename earns a module

The *unit* is the product. Once a wave declares `Θ_content`, `content-scale` can scale its drops
without asking anything else, and the world program has a named thing to produce. Leaving it called
`RecommendedLevel` preserves exactly the ambiguity that let three curves ship.

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Wave|FullyQualifiedName~Expedition"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Server.Tests
git status --short tests\
```

## 4. Structure

```
src/FusionRpg.Core/Battle/WaveCatalog.cs      (edit — RecommendedLevel -> ContentIndex)
src/FusionRpg.Core/Battle/BattleModels.cs     (edit — BattleSetup.Level -> Index, or a documented alias)
docs/architecture/item/ssot-generation.md     (edit — §4.1 correction, see §8)
tests/FusionRpg.Core.Tests/Power/ContentIndexTests.cs
```

## 5. Testing strategy

| Case | Expect |
|---|---|
| Values unchanged | the four waves read `1 / 3 / 6 / 10` after the rename |
| Expedition inheritance | each tier's resolved battles carry the wave chain's index — asserted, because it is load-bearing and currently undocumented |
| Boss wave | `warpath-20h`'s boss tick resolves `rift-tyrant` at index 10 |
| **Serialization safety** | `BattleSetup` hashes byte-identically before and after. If a renamed field is serialized, the wire name stays and the rename is internal only — a field rename on `BattleSetup` moves all four expedition hashes (`decisions.md:42`) |
| No golden moved | Core + Server green, `git status tests/` clean |

## 6. Boundaries

**Always** — change names and units, never values · assert expedition inheritance rather than
assuming it.

**The `BattleSetup` rename is internal only** (audit F7). `decisions.md:42` is explicit that a field
there *"moves all four expedition hashes."* The serialized name stays; the alias is the rule, not a
fallback. Renaming a wire field for vocabulary is not worth a golden re-bless in the one wave whose
entire purpose is that nothing moves.

**Ask first** — adding a level field to `ExpeditionTierDef`; the docs imply one, the code does not
need one.

**Never** — re-bless a golden · invent a sector level; that is the world program's.

## 7. Success criteria

1. Wave values unchanged; unit renamed.
2. Expedition inheritance asserted by test.
3. No golden re-blessed, no hash moved.
4. `ssot-generation.md` §4.1 corrected to match code.

## 8. Open

**None.** The documentation defect below is scoped work, not a question — it needs no decision.

### The defect

`ssot-generation.md` §4.1
describes three `contentLevel` sources that do not exist. The expedition rows are not "not yet
built": expeditions work today by inheriting from waves. Correcting the doc is in scope here.
