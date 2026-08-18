# Stat system architecture

Forward-only combat stats: immutable game baseline **Y0**, source-tagged modifiers **Xi** in a bag, composed **Y**. Features register plugins; they never reverse `Y → Xi` or edit Unity fields.

See also: [decisions.md](decisions.md), [overview.md](overview.md), [actor-hub-ssot.md](actor-hub-ssot.md) (derived layer — design locked), [ARPG effects → FusionRpg mapping](../research/arpg-effects/06-fusionrpg-mapping.md) (inspiration only).

## Invariant

```text
Y0  = immutable game baseline (captured once per entity instance)
Xi  = source-tagged modifiers in ModifierBag
Y   = Compose(Y0, bag)   // forward only

Never: Xi = f(Y)
Never: persist final Y as RPG SSOT
Save inputs (Y0 + bag / feature state), not computed totals.
```

## Derived layer (Actor Hub — design locked)

**StatSystem** composes **primary** channels only. **Actor Hub** adds a second pass — **DerivedComposer** → `ActorDerivedSnapshot` — for catalog channels such as `progression.power`, `status.power.*`, `status.resist.*`, and future `progression.bonus.*`.

```text
ActorHub.Resolve(entityKey):
  RuntimePrimary = StatSystem.Resolve(entityKey)     // unchanged ownership
  Derived        = DerivedComposer.Compose(...)      // catalog channels
  AppliedCombat  = RuntimePrimary + progression.bonus.*   // Writer input
```

Status Apply (L2b) reads **Derived** for attacker + defender — not primary `hp`/`atk`. Spec: [actor-hub-ssot.md](actor-hub-ssot.md). **Implementation deferred**; StatusRuntime blocked until Actor Hub code lands.

PvzStats and cheats may contribute modifiers on **catalog** channels when validated — same plugin path, extended channel allowlist.

## Single Unity writer (locked)

```text
Features / plugins / FE / F8
  → Upsert modifiers or Invalidate only
  → NEVER assign Plant/Zombie combat fields

EntityApply.Run* → StatSystem.Resolve → EntityStatWriter
  = the only legal Unity combat mutation path

Session effect mods carry `ApplyOwnerKey` (`match` / `plant:N` / `zombie:N` / `entity:HEX`).
`StatSystem.Resolve` copies only matching session mods into the compose bag (see [unique-entity-effects.md](unique-entity-effects.md)).
```

Tab A PushScales and Tab B Absolute Apply both call `EntityApply.Run*` (absolute = Override map via `cheat.absolute`). Kill/reinforce helpers use `EntityStatWriter.Force*` / `Scale*`.

CI/local guard: `scripts/guard-single-writer.ps1` fails if combat field assigns appear outside `EntityStatWriter.cs`.

### Proof + LimHealth policy

With `SYS-EMIT-PROOF` (default on):

- `stat.writer` — every Writer apply (before→after, source)
- `stat.limhealth` — observe-only when `Plant.LimHealth` changes HP/max vs Writer registry

`SYS-LIMHEALTH-GATE` (default **off**): Writer-owned Prefix skips vanilla `LimHealth` for registered entities so finals stick. Enable only after observe shows revert (`revertedVsWriter`).

**W11-B:** documented **Bend** — this wave did not run LIVE LimHealth prove. Gate stays off until an operator session records `stat.limhealth` `revertedVsWriter=true`.

## Patterns

| Pattern | Type | Role |
|---|---|---|
| Plugin | `IStatModifierPlugin` | Feature → Upsert/Withdraw modifiers |
| Registry | `ModifierPluginRegistry` | Register/unregister; ordered refresh |
| Factory | `StatContextFactory` | Build resolve context + attach Y0 |
| Factory | `StatModifierFactory` | Typed Flat / Increased / More / Override |
| Strategy | `IComposeStrategy` | Phased math (swappable) |
| Facade | `StatSystem` | Capture baseline, session Upsert, resolve, invalidate |
| Apply | `EntityApply` | Injector entry: Resolve then Writer |
| Writer | `EntityStatWriter` | Sole Unity combat field mutator |

## Data plane vs plugins

| Path | API | Use when |
|---|---|---|
| Plugin | `Contribute` → bag each Resolve | Feature owns state (class, items, cheats) |
| Session bag | `StatSystem.Upsert` / `WithdrawSource` | Ad-hoc mods without a plugin |

`WithdrawSource` only clears the **session** bag. It does **not** stop a plugin from re-emitting the same `SourceId` on the next `Contribute`. To remove plugin-backed mods, change feature state (or Unregister the plugin).

## Compose phases (locked)

Per channel (`hp`, `maxHp`, `atk`, armor…):

```text
base     = Y0
afterFlat = base + Σ Flat
afterInc  = afterFlat * (1 + Σ Increased)   // +0.25 = +25% increased
afterMore = afterInc  * Π (1 + More)        // +0.5  = 50% more
final     = Override (highest Priority) if any else afterMore
final     = round / clamp (HP/ATK min 1)
```

Legacy cheat `StatMod.*Percent` where `1` = identity, `2` = double maps to **More `(p - 1)`**. Flat maps to **Flat**. Absolute Tab B/C writers use **Override**.

When `ApplyStats` / compose `applyStats` is **false**, Flat/Increased/More are ignored, but **Override** mods still compose (so Tab B absolute works with A-APPLY off).

**Defense** (for `ScaleIncoming`): Increased/More/Override compose a percent multiplier from baseline `1` via the same strategy; Flat sums to `DefenseFlat`. An Override replaces the whole defense view (`DefensePercent = value`, `DefenseFlat = 0`).

## Invalidate → living re-resolve

1. Feature or session change → `StatSystem.Invalidate` (sets dirty; raises `Invalidated`).
2. Injector `RpgLoop` calls `ConsumeDirty` once per frame → `ReapplyLivingFromStats` when cheat doc / PvzStats revision / Tab A scales need write (re-Resolve from stored **Y0**; empty PvzStats bag restores baseline after clear).
3. Explicit Tab A `PushScalesNow` / A-PUSH-NOW still available; PvzStats-only dirty proceeds without requiring Tab A non-identity scales.

## Plugin registration (feature checklist)

1. Implement `IStatModifierPlugin.Contribute(StatContext, IModifierBagEditor)`.
2. Build mods with `StatModifierFactory` (`Flat` / `Increased` / `More` / `Override`).
3. `StatSystem.Plugins.Register(plugin)` at boot.
4. On feature state change → update store → `Invalidate` → injector re-`Resolve` living entities from **Y0**.

Do **not** edit `StatComposer`, `PhasedComposeStrategy`, or scatter Unity field writes. Extend via plugins + `EntityApply` / `EntityStatWriter` only.

Stub plugin ids (Order bands): `rpg.class` 100, `rpg.progression` 100 (derived stub — [actor-hub-ssot.md](actor-hub-ssot.md)), `rpg.achievement` 200, `pvz.stats` 250, `rpg.item` 300, `rpg.buff` 400, `cheat.scale` 900, `cheat.absolute` 950.

## Module paths

```text
FusionRpg.Core/Stats/          engine + plugins
FusionRpg.Injector/Stats/      EntityApply + EntityStatWriter only
```

## Anti-patterns

- Editing living Unity HP outside `EntityStatWriter`
- `Apply*Absolute` bypass writers (removed — Tab B uses EntityApply)
- Reverse-engineering class/items/Tab A from `spawn_stats` dumps
- `if (feature == Items)` inside `GameHooks` or `StatComposer`
- Storing only final Y in SQLite as progression SSOT
- Mixing plugin `Order` with math phases
- Non-idempotent `Contribute` (double Upsert without Withdraw)
- Expecting `WithdrawSource` to cancel plugin emissions
- Enabling `SYS-LIMHEALTH-GATE` without `stat.limhealth` proof
