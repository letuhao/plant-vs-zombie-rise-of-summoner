# Spec: `tempo-content`

Module `tempo-content` in the [battle-tempo map](../battle-tempo-map.md). A **parallel root** with
`action-timing` — it depends on nothing, though its effect is far more visible once actions take time.

**Read before editing:** [battle-turn-ideal.md](../battle-turn-ideal.md) §4 and §10a ·
[resource-hub-ssot.md](../resource-hub-ssot.md) · [tunables-ssot.md](../tunables-ssot.md) ·
[demon-seed-map.md](../demon-seed-map.md).

---

## 1. Objective

**Make `turn.speed` differ between actors, so readiness ordering means something.**

`B39` wired `BattleEngine` to order turns by readiness. It is provably correct and provably inert:
**no content authors `turn.speed`**, so every actor clamps to `DerivedStatPolicy.TurnDefaultSpeed`,
every comparison ties, and ordering falls through to the initiative jitter it always used.

### 1.1 ⭐ The species half is already authored — it is not new content

The obvious plan was "add a speed field to the demon corpus". **That work is already done under another
name.**

| Fact | Evidence |
|---|---|
| Every species has an attack tempo | `attackTempo` on all 831 anchors — a closed five-value vocabulary (`ponderous`, `slow`, `steady`, `quick`, `flurry`) |
| It is already a **number**, not a label | `demon-shape.v1.json` → `attackTempoIntervalMs`: ponderous **3000**, slow **2400**, steady **1500**, quick **900**, flurry **500** — a **6× spread** |
| It is already **computed and persisted** | `ConcreteSpecies.AttackIntervalMs` (a `long`), stored and round-tripped by `RpgStore.Species.cs` |
| ⛔ **Battle ignores it entirely** | The only consumers of `AttackIntervalMs` are the store's own persist / read / compare. No battle path reads it. |

So the species half of this module is **a projection, not an authoring pass**: `turn.speed` derives
from an interval the corpus already carries. **No corpus change, no classifier run, no new column** —
which also removes the cross-program dependency the map originally flagged, and with it the risk of
authoring against ids the demon stream is still reconciling.

### ⛔ D12 — "battle ignores it entirely" was true, but not for the reason this section assumed

**Build-time finding, 2026-09-05.** The table above is accurate about `ConcreteSpecies` — battle never
read it — but that framing hid a second gap the review round never caught: `WaveCatalog.Enemies`
builds every `BattleActorSetup` from **`DemonSpeciesDef`**, the Core-side, DB-free compiled roster, not
`ConcreteSpecies`. `DemonSpeciesDef` never carried an interval field **at all** — so "no battle path
reads it" was not a missing *read*, it was a missing *value to read*, one layer further back than this
section assumed.

The gap turned out to be exactly one line: `RpgStore.BuildDemonSpeciesSnapshot()` already reads a
`ConcreteSpecies` row (`AttackIntervalMs` included) to build each `DemonSpeciesDef`, and simply never
copied that one field across. **This does not change §1.1's conclusion — no corpus change, no
classifier run — it changes what "the wiring" means:** `DemonSpeciesDef.AttackIntervalMs` (new field,
default `0`, additive) → `BuildDemonSpeciesSnapshot` copies it → `WaveCatalog.Enemies` carries it onto
a new `BattleActorSetup.AttackIntervalMs` → `BattleStatComposer.Compose` projects it.

⚠️ **This is a second, more specific golden-movement cost, not a new one.** `BattleActorSetup` is what
`ExpeditionResolverTests.Tier_goldens_are_locked` hashes; a wave enemy carrying a non-zero interval
moves that hash the moment content authors one — on top of, not instead of, the battle-resolution
goldens §9 already warns about. `MEAS` must size this too.

### 1.2 The two halves (owner decision 1: "both")

| Half | Source | Shape |
|---|---|---|
| **Base** | `ConcreteSpecies.AttackIntervalMs`, already populated | A projection: `speed ∝ 1 / interval` |
| **Modifier** | `TraitBattleCatalog` channel mods on `turn.speed` / `turn.haste` | Battle-owned; `swift` already touches initiative, so the precedent exists |

---

## 2. Design

### 2.1 The projection, and why this direction

`turn.speed` is *"higher acts more often"*; `AttackIntervalMs` is *"lower acts more often"*. They are
reciprocals, so the projection is a division against a **reference tempo**, not a table:

```
turn.speed = TurnDefaultSpeed × referenceIntervalMs / attackIntervalMs
```

With `steady` (1500 ms) as the reference and the shipped `TurnDefaultSpeed` of 100, the five tempos land
at roughly **ponderous 50 · slow 62 · steady 100 · quick 166 · flurry 300** — the corpus's own 6× spread,
carried straight through.

⭐ **A formula, not a lookup table, and that is deliberate.** A per-tempo speed table would be a
**second curve** over the same five labels the interval table already defines — the "one ladder"
violation `ssot-power-scale.md` exists to end. The reference interval is the only new number, and it is
a tunable.

⚠️ **Both numbers are tunables, not constants.** `referenceIntervalMs` lives in
`data/tuning/battle.v{n}.json` beside the other timeline numbers. `TurnDefaultSpeed` is already in
`derived-stats.v{n}.json` and is **read, never re-declared**.

### 2.1a ⛔ D4 — this delivers ORDER, not FREQUENCY. Say so, or it reads as a bug

**Review finding, 2026-09-04.** A player who sees a speed stat reasonably expects a fast actor to act
**more often**. This module does not deliver that, and the first draft never said so.

In the batch resolver, **every active actor is offered readiness on every pass** — `B39`'s ordering uses
`ReadyTicks` purely as a **sort key**. So speed decides *who goes first*, never *how many times*.

- ✅ **In scope:** a faster actor acts earlier in the round, which matters — it lands damage before a
  slower rival, and first-strike is a real advantage when a kill removes a turn.
- ⛔ **Not in scope:** acting more often. That needs true readiness *scheduling* — the kernel supports it
  (`TurnReadiness` computes real arrival ticks), but `BattleEngine`'s round loop does not consume it
  that way, and changing that is a resolver rewrite, not a content module.

**State this in any player-facing copy too.** A speed stat that silently means "acts first" while the
player believes it means "acts more" is the kind of mismatch that reads as a bug report.

### 2.2 Clamping is required, not defensive

`TurnReadiness.EffectiveRate` **throws** on a non-positive speed or haste — the readiness spec's own
"speed clamped before division" rule. The projection must therefore floor at a positive value, and that
floor is a **structural limit, PS-8 exempt, and must say so in a comment**
(`ssot-power-scale.md` §11.4 registers the divisor hazard: for a denominator the overflow risk inverts
to *small* values).

### 2.3 The trait half

`TraitBattleCatalog` gains `turn.speed` / `turn.haste` entries in the `ChannelMods` it already carries.
Nothing structural changes — `BattleStatComposer` already accepts both channels (verified: an unknown
channel **throws**, and a `turn.speed` mod is accepted and composed today).

⛔ **`swift` is not re-pointed.** It touches the *initiative jitter*, which survives as the tie-break
when speeds are equal. Making `swift` a speed mod as well would double-count it.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~TempoContent|FullyQualifiedName~TurnReadiness|FullyQualifiedName~ProductionProfilePath"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden|FullyQualifiedName~Expedition"
python scripts\audit-magic-numbers.py --summary
```

---

## 4. Project structure

```
src/FusionRpg.Core/Battle/SpeciesTempoProjection.cs    NEW — interval -> turn.speed, pure
src/FusionRpg.Core/Battle/BattleStatComposer.cs        seeds turn.speed from the projection
src/FusionRpg.Core/Battle/TraitBattleCatalog.cs        turn.speed / turn.haste trait mods (mechanism only -- no content authored)
src/FusionRpg.Core/Battle/BattleTuning.cs              SpeciesTempoReferenceIntervalMs + loader
data/tuning/battle.v{n}.json                           referenceIntervalMs (published, never hand-edited)
tests/FusionRpg.Core.Tests/Battle/SpeciesTempoTests.cs NEW

# D12's own additions -- the wiring gap the spec's §1.1 did not anticipate:
src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs       DemonSpeciesDef.AttackIntervalMs, NEW field
src/FusionRpg.Data/Sqlite/RpgStore.Species.cs          BuildDemonSpeciesSnapshot copies it across
src/FusionRpg.Core/Battle/BattleModels.cs              BattleActorSetup.AttackIntervalMs, NEW field
src/FusionRpg.Core/Battle/WaveCatalog.cs               Enemies() carries species.AttackIntervalMs onto the setup
```

⚠️ **Config is versioned and published, never hand-edited** — `python tools/tuning/publish.py`.
`speciesTempo.referenceIntervalMs` could not go through `publish.py` (it refuses to invent a key not
already present) — a new `battle.v3.json` was authored directly, the same way `v2` introduced
`timeline.profiles` (its own `_meta.noteV2` is the precedent).

---

## 5. Code style

```csharp
/// <summary>
/// `turn.speed` from the species' own attack interval. A PROJECTION of a number the corpus already
/// carries (`attackTempo` -> `attackTempoIntervalMs`), never a second table over the same labels.
/// </summary>
public static long SpeedFor(long attackIntervalMs, long referenceIntervalMs, long defaultSpeed)
{
    // Structural floor, PS-8 exempt (ssot-power-scale.md §11.4): `EffectiveRate` DIVIDES by speed and
    // throws on <= 0, so this bounds a denominator — it is a termination guard, not a progression cap.
    if (attackIntervalMs <= 0) return defaultSpeed;
    return Math.Max(1, checked(defaultSpeed * referenceIntervalMs) / attackIntervalMs);
}
```

---

## 6. Testing strategy

1. **The five shipped tempos project to five distinct speeds**, ordered — `ponderous < slow < steady <
   quick < flurry`. Read from the **real** `demon-shape.v1.json`, not a fixture.
2. ⭐ **A faster species acts first on the production path** — the assertion `B39` could only make with
   a synthetic channel mod. Proven **by contrast in both directions** (swap which species is fast), so
   an initiative roll cannot pass it by luck.
3. **The floor holds:** a zero or negative interval yields the default speed and never throws.
4. **`swift` is not double-counted** — it moves the jitter, not the speed.
5. **Equal tempos reproduce today's ordering exactly** — the containment property.
6. **No literal:** `referenceIntervalMs` is read from tuning; `M1` stays 0.

---

## 7. Boundaries

- **Always:** derive speed from the existing interval; floor the denominator and say it is structural;
  read `TurnDefaultSpeed` from `derived-stats`, never re-declare it.
- **Ask first:** changing the reference tempo away from `steady`; giving traits a haste mod large enough
  to invert the species ordering.
- **Never:** add a per-tempo speed table (a second curve); write a speed number into the demon corpus;
  re-point `swift`; hand-edit a published tuning file.

---

## 8. Success criteria

1. The five tempos yield five ordered, distinct speeds from the corpus's own data.
2. A faster species demonstrably acts before a slower one in a real resolved battle, both directions.
3. No new content authored and no corpus column added.
4. `M1 = 0`; the divisor floor is commented as structural.

---

## 9. Golden movement

**Moves goldens** — turn order changes wherever species tempos differ.

⭐ **D5 (owner, 2026-09-04): this lands TOGETHER with `action-timing` as a single mover** — one
`RulesetVersion` bump, one re-bless, one sweep, one sign-off. The earlier draft of this section claimed
this module could be "byte-identical on top of" `action-timing`, which was **wrong**: reordering turns
cannot be byte-identical by definition.

⚠️ **Measure the two axes separately before landing them jointly** (the `B34` staged-sweep shape), so
the attribution exists even though the re-bless does not separate it.

⚠️ Golden fixtures build actors from `BattleGoldenTests`' own builders rather than the species catalog,
so the movement may be **smaller than expected** — measure before predicting, exactly as `B35` did.
