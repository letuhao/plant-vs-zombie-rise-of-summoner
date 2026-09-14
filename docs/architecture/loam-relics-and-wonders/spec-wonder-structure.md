# Spec: `wonder-structure`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `wonder-structure`, row 2 of the
[loam-relics-and-wonders map](../loam-relics-and-wonders-map.md) (wave 1, no dependency — builds in
parallel with `relic-item-kind`). Ideal: [loam-relics-and-wonders-ideal.md](../loam-relics-and-wonders-ideal.md)
§The shape, §Alternatives rejected, §The effect-kind vocabulary. Decisions:
[decisions.md](../decisions.md) "Loam relics and wonders SSOT (2026-09-13)". House style and
overlay-module precedent: [scoped-inventory-hierarchy/spec-sector-storage.md](../scoped-inventory-hierarchy/spec-sector-storage.md),
[scoped-inventory-hierarchy/spec-legion-cargo.md](../scoped-inventory-hierarchy/spec-legion-cargo.md)
(sibling programs, not a dependency).

## Objective

Define what a Wonder **is**, as a `StructureDef` row, and nothing past that. This module ships four
closed vocabularies (`WonderScope`, `WonderRarity`, `WonderEffectKind`, `WonderEffectDef`), the
`StructureDef`/`StructureCorpus` field additions that carry them, the load-time refusals that keep
`World`/`Multiverse` scope and `DefensePower`/`AuraGrant`/`EmpireBuff` effect kinds **named but
inert**, and a tunable existence-cap lookup keyed by scope. It does **not** wire the effect into
`LoamProduction`/`WorldFaction.ScopeModifierMilli` (module 3, `wonder-effect-empire`), does not mint
or spend a relic (module 1 `relic-item-kind`, module 4 `wonder-build-flow`), and does not touch
`scoped-inventory-hierarchy`'s own tables.

Success looks like: a `well.json`-shaped Wonder anchor authors `wonderScope: "Sector"`,
`wonderRarity: "Common"`, `wonderEffects: [{ "kind": "LoamGenerationRate", "scope": "Sector",
"valueMilli": ... }]` alongside the same `yieldMultiplierMilli`/`constructRubbleCost` fields every
other structure already carries, and loads through the existing `StructureCatalog.All` pipeline with
zero changes to any of the 25 shipped rows; a catalog row that authors `wonderScope: "World"` or a
`wonderEffects` entry with `kind: "AuraGrant"` fails `StructureCatalog.Validate` at startup, loud,
naming the reserved member — never silently accepted, never reachable by any runtime path.

## Locked anchors

- **`Sector`/`Empire` scope only this wave; `World`/`Multiverse` named, reserved, unregistered**
  (decisions.md, Loam relics and wonders SSOT). This module's own `Validate` extension is the
  mechanism that keeps the reservation real rather than aspirational — see §Design 5.
- **Rarity is a tunable per-scope existence cap, never a hard-coded `1`** (map row 2; AGENTS.md "no
  hard progression ceilings"). `Common` = uncapped; `Unique` = a `long` read from
  `data/tuning/loam-relics-wonders.v1.json`, mirroring the "dynamic headroom, not a constant" idiom
  `RpgStore.MaxSoulAwardFrom` already uses for soul-award ceilings (decisions.md, Caps
  (project-wide) row).
- **Basic-material cost reuses `ConstructRubbleCost`/`ConstructIronworkCost` — no new field**
  (decisions.md; confirmed fresh this session, `StructureCatalog.cs:87,90`).
- **`WonderEffectKind` ships exactly one live member, `LoamGenerationRate`; `DefensePower`/
  `AuraGrant`/`EmpireBuff` are named-but-unregistered** (map row 2; decisions.md). §Design 5 is the
  enforcement, not just the naming.
- **The `Empire`-scope combination rule (SUM) and the `LoamProduction.For` faction-input plumbing are
  module 3's build, not this module's** — this module ships the `WonderEffectDef` shape module 3
  will read, and nothing past that (map row 3, "Depends on: `wonder-structure`").

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `StructureKind` is closed at exactly **5** values today: `LoamSource, Storage, Yield, Refinery, Obstacle` — **not 6.** `spec-sector-storage.md`'s own `ItemStorage` (the scoped-inventory program's 6th value) is a written, unbuilt spec — `scoped-inventory-hierarchy` is "spec'd, unbuilt" per its own map's dependency line, and a fresh read of the real enum this session confirms it still has 5 members, no `ItemStorage` case anywhere | `StructureCatalog.cs:10-39`, read in full this session; `scoped-inventory-hierarchy-map.md:13-14` ("hard-blocks... spec'd, unbuilt") |
| `StructureDef`'s complete field list has no Wonder-shaped field anywhere — no `WonderScope`, no `Rarity`, no effect list | `StructureCatalog.cs:42-194`, the whole record read this session |
| `Obstacle` — the exact **orthogonal-facet-on-the-same-record** precedent this module's own call reuses: "an obstacle is a `StructureDef` facet, so it inherits HP, destructibility and everything else `structure-state` already built... not a parallel system," default `ObstacleKind.None` so every pre-existing row is unaffected | `StructureCatalog.cs:137-143` |
| `ContainerId` (`string?`) and `WorldSlot.Element` (`ElementTypeId?`) — the existing nullable-optional-facet-field convention this module's own `WonderScope?`/`WonderRarity?` fields follow, rather than a sentinel enum member | `StructureCatalog.cs:193`; `WorldState.cs:102` |
| `YieldMultiplierMilli`/`FlatYieldPerTurn` already exist, already read by `LoamProduction.For`, and either field can express any bonus size the tuning/seed content authors — a Wonder's `Sector`-scope loam effect needs **zero new engine code**, confirmed fresh this session | `StructureCatalog.cs:54-58,69-77`; `LoamProduction.cs:18-62` (both the multiplier loop and the flat-add loop) |
| `LoamProduction.For(WorldSector sector)` has **no faction-level input in its signature at all** — confirmed fresh this session, both overloads read | `LoamProduction.cs:18` (`For(WorldSector sector)`), `:70` (`For(string? ownerFactionId, IEnumerable<string> slotTypeIds)` — belief-side only, still sector-scoped) |
| `WorldFaction.ScopeModifierMilli` — declared, `int`, per-mille, default 1000, doc comment anticipating "whichever future consumer['s] own compute path reads it" | `WorldState.cs:70-94`, specifically `:93` |
| **New finding this session, not in the ideal doc's own citation:** `ScopeModifierMilli` is hashed into `WorldCanonical` for determinism but **is never persisted to SQL at all** — no `scope_modifier_milli` column exists anywhere. `rpg_world_factions`' `CREATE TABLE` has no such column; the `INSERT`/`SELECT` pair `RpgStore.World.cs` uses to seed and read a `WorldFaction` back both omit it; and the turn-commit diff path that re-persists factions every turn (`RpgStore.WorldGraphDiff.cs`'s `DiffFactions`) also omits it from its own `INSERT OR REPLACE`. The field is genuinely real and hashed *within one loaded `WorldState`*, but a save/load round-trip silently resets it to the default 1000 today | `RpgStore.World.cs:37-44` (CREATE, no such column), `:270-278` (seed INSERT, 6 named columns, none is `scope_modifier_milli`), `:462-472` (SELECT, same 5 columns read back); `RpgStore.WorldGraphDiff.cs:97-109` (`DiffFactions`'s own `INSERT OR REPLACE`, same 5-plus-1 column list, still no `scope_modifier_milli`); `WorldCanonical.cs:97-98` (the hash-side read, proving the field is live in memory, not merely declared) |
| `WorldFactionKind` is closed at exactly 5: `Player, Zomboss, Clan, Rival, Wild` — only `Player` ("Dave") is a real, item/relic-owning identity | `FactionKindCatalog.cs:7-30`, read in full this session |
| `WorldSector` already carries the exact `Slots`/`OwnerFactionId`/`DevelopmentLevel`/`RubbleStock`/`IronworkStock`/`LoamStock` shape this module's design reasons about | `WorldState.cs:148-210` (`SectorId:150`, `OwnerFactionId:158`, `DevelopmentLevel:164`, `LoamStock:173`, `RubbleStock:181`, `IronworkStock:187`, `Slots:210`) |
| `WorldSlot.StructureId` (`string?`) and `.ConstructionTurnsRemaining` (`int?`) — the exact fields `LoamProduction.For`/`LoamPhases.EffectiveCapacity` gate on; a Wonder occupies a slot exactly like any other structure, no new slot field needed | `WorldState.cs:96-126` |
| `LoamPhases.EffectiveCapacity` — the additive-sum-over-active-slots shape, gated on `StructureKind.Storage`; **the precedent for "a capacity/effect axis reads one `Kind`, ignores every other," which this module's own `WonderScope?`-on-any-`Kind` design deliberately does NOT need**, since a Wonder is never a capacity axis | `LoamPhases.cs:66-80` |
| `MaxHpOf` — every `StructureDef` with `MaterialTier > 0` already gets its HP from `P(Θ_development)` via `PowerLadder`, the **one power ladder**, no private curve. A Wonder that authors a nonzero `MaterialTier` (optional; 0 = indestructible, the existing default) gets this for free — no new magnitude-from-level code anywhere in this module | `StructureCatalog.cs:121-135` |
| `StructureCorpus`/`StructureMagnitudes` — the real JSON wire shape every structure row (Wonders included) is authored through. `structureKind`/`obstacleKind` are parsed via plain `Enum.Parse<T>(string)` with **no `ignoreCase`**, so JSON enum strings must match the C# member spelling exactly (`"LoamSource"`, not `"loamSource"`/`"loam-source"`) — confirmed against the real shipped `well.json` row | `StructureSeed/StructureCorpus.cs:20-37` (`StructureMagnitudes`), `:110-135` (`ParseRow`'s magnitudes block); `StructureCatalog.cs:263` (`Enum.Parse<StructureKind>(m.StructureKind)`, no `ignoreCase`); `data/seed/structures/extract/well.json:52` (`"structureKind": "LoamSource"`) |
| Every optional/newer `StructureMagnitudes` field (`FlatYieldPerTurn`, `ConstructRubbleCost`/`ConstructIronworkCost`, `ContainerId`) was added **required-with-a-zero/null-default in the JSON shape**, not truly optional at the parse layer — `RequireLong`/`RequireInt` throw if the key is absent. A Wonder-only field must instead be genuinely optional at parse time (`TryGetProperty`, default `null`/empty) so the 25 existing rows load unmodified — see §Design 4 | `StructureSeed/StructureCorpus.cs:113-134` (every `magnitudes` field uses `Require*`, none uses `TryGetProperty` except `containerId` at `:132-134`, which is the one existing precedent for a genuinely-optional field) |
| `RelicCatalog` (combat) is unrelated — a small, hand-seeded, **equip-slot** relic list (`fx.*` effect ids), zero reference to `World`/`Loam`/Wonders. Confirmed fresh this session, not merely cited from the ideal doc | `src/FusionRpg.Core/Match/RelicCatalog.cs:1-30`, read this session |
| No generic `Rarity` enum exists in `FusionRpg.Core` to collide with — only `CreatureRarity` (a distinct, differently-shaped domain axis) | grep across `src/`, this session; `CreatureRarity.cs:16` |
| `LoamPolicy`'s `Configure(tuning)` / static-holder-with-loud-failure shape is the exact tunable-injection convention this module's own `WonderPolicy` mirrors | `LoamPolicy.cs:1-21` |

### Real gap

| Gap | What this module builds |
|---|---|
| No Wonder-shaped vocabulary anywhere (`WonderScope`, `WonderRarity`, `WonderEffectKind`, `WonderEffectDef`) | §Design 1-3 |
| No `StructureDef`/`StructureCorpus` field to carry any of the above | §Design 2, 4 |
| No load-time enforcement that keeps `World`/`Multiverse` and the three reserved effect kinds unconstructible | §Design 5 |
| No existence-cap lookup for `Unique`-rarity Wonders | §Design 6 |
| **Not this module's gap, named for module 3's benefit:** the `ScopeModifierMilli` persistence gap found this session (Built table above) means `wonder-effect-empire` needs a new `scope_modifier_milli` column and INSERT/SELECT/diff wiring, not merely "a first reader" as the ideal doc's own wiring-gap framing said | §Interface exposed to dependents |
| **Not this module's gap:** a relic-denominated cost field on `StructureDef`. Relics are rolled items, not a stock count (ideal doc §The shape, `relic-item-kind`'s own scope) — "spend N relics" is a construction-verb parameter `wonder-build-flow` (module 4) owns, never a `StructureDef` magnitude. This module deliberately adds **no** `RelicCost`-shaped field | §Design 7 |

## Design

### 1. The `StructureKind` decision — an orthogonal facet, not a 6th/7th value

**The evidence-based call the task required, made fresh against the real enum, not the map's own
assumption.** The map's module-2 description frames this as "a 7th value, since `ItemStorage` just
claimed the 6th" — verified false this session: `StructureKind` has exactly 5 members today
(`StructureCatalog.cs:10-39`), and `ItemStorage` is a written, unbuilt spec
(`scoped-inventory-hierarchy-map.md:13-14`, "hard-blocks... spec'd, unbuilt"), not shipped code. If
`wonder-structure` built first, a naive reading of the map's own framing would add a **6th** value,
not a 7th — the map's count was already stale at spec-kickoff time. This is reported as drift, not
silently corrected.

**But the real call is not "6th vs 7th" — it is whether `Kind` is the right axis at all, and the
evidence says no.** `Kind` answers one question consistently across all 5 existing members: *what
economic behavior does this structure have* (`LoamSource`/`Yield`: produces; `Storage`: raises
capacity; `Refinery`: converts; `Obstacle`: blocks). A Wonder's `LoamGenerationRate` effect is **not**
a new economic behavior — it is the *same* behavior an ordinary Well or Extractor already has,
delivered through the *same* two fields (`YieldMultiplierMilli`/`FlatYieldPerTurn`) `LoamProduction.For`
already reads (`LoamProduction.cs:27-35,43-51`). `ItemStorage`'s own spec explicitly justified its new
`StructureKind` value on the opposite finding — "a structurally distinct capability... a different
consumer, a capacity shape this doc is free to define without inheriting loam's exact rules"
(`spec-sector-storage.md` §Design 1) — a test a Wonder's v1 effect fails on purpose: it inherits
loam's exact rules, verbatim.

What a Wonder actually needs is the same shape `Obstacle` already proved works: an **orthogonal
facet field on the same `StructureDef` record**, independent of `Kind` — `StructureCatalog.cs:137-143`'s
own doc comment states the precedent directly: *"an obstacle is a `StructureDef` facet... not a
parallel system."* A Wonder stays `Kind = LoamSource` or `Kind = Yield` (whichever its `RequiredSlotKind`
already implies, exactly like today's Well/Extractor rows) and gains a new, independent
`WonderScope?` facet. This also means **zero new seedsmith role/slot-kind pairing work** — the real
gap `sector-storage` named for its own new `Store`↔`Vault` pairing (`spec-sector-storage.md`'s own
Real-gap table) simply does not arise here, because a Wonder authors the *same* role/slot pairing an
ordinary Yield/LoamSource row already uses.

**Decision: no new `StructureKind` value.** `WonderScope?`/`WonderRarity?`/`WonderEffects` are new,
independent fields on `StructureDef`, mirroring `Obstacle`'s exact precedent — null/empty for every
one of the 25 shipped rows, unaffected.

### 2. `WonderScope`, `WonderRarity`, `WonderEffectKind` — three closed enums

```csharp
namespace FusionRpg.Core.World;

/// <summary>How far a Wonder's effect and existence-cap reach (loam-relics-and-wonders `wonder-structure`).
/// Sector/Empire are live this wave. World/Multiverse are named per the owner's own "reserve vocabulary
/// for extend later" instruction but are refused by <see cref="StructureCatalog.Validate"/> — see §Design 5.
/// Never select/construct a World or Multiverse row from any live code path; the member exists only so
/// a future spec can cite it without re-deriving the ladder.</summary>
public enum WonderScope
{
    Sector,
    Empire,

    /// <summary>Reserved. Needs the same LoamProduction.For faction-input plumbing as Empire, looped
    /// over every faction in the world, plus a named wonder-race mitigation (decisions.md, Loam relics
    /// and wonders SSOT) — not this program's wave. Refused by Validate today.</summary>
    World,

    /// <summary>Reserved. Needs a wholly new player-scoped, world-surviving ledger with no precedent
    /// anywhere in this codebase (loam-relics-and-wonders-ideal.md §The owner's fourth-pass framing).
    /// Refused by Validate today.</summary>
    Multiverse
}

/// <summary>Scarcity axis, orthogonal to WonderScope by construction — a Sector-scope Wonder can be
/// Common or Unique independently of a World/Empire one, exactly the way DemonRarity and a creature's
/// other closed axes stay independent (CLAUDE.md closed-vocabulary table).</summary>
public enum WonderRarity
{
    /// <summary>No existence cap — WonderPolicy.ExistenceCapFor returns long.MaxValue, the same
    /// "dynamic headroom, never a constant" idiom RpgStore.MaxSoulAwardFrom already uses
    /// (decisions.md, Caps (project-wide)). Never a silent hard cap of 1.</summary>
    Common,

    /// <summary>Capped by a tunable count per WonderScope (data/tuning/loam-relics-wonders.v1.json) —
    /// never a hard-coded 1 (AGENTS.md "no hard progression ceilings"). Which scope-unit the cap
    /// counts against (per sector / per faction) is module 4's (`wonder-build-flow`) own enforcement
    /// concern; this module ships only the tunable lookup (§Design 6).</summary>
    Unique
}

/// <summary>What a Wonder's effect boosts. Closed, reviewed-growth, mirroring StatusCatalogBootstrap's
/// "a new member is a reviewed addition, never an open string" discipline (StatusCatalogBootstrap.cs).
/// LoamGenerationRate is the only member Validate accepts this wave — see §Design 5.</summary>
public enum WonderEffectKind
{
    LoamGenerationRate,

    /// <summary>Reserved — boosts a defending unit's combat power in a warded/garrisoned sector. Needs
    /// a real sector-scoped combat-power read at world/siege scope, not audited this session
    /// (loam-relics-and-wonders-ideal.md §The effect-kind vocabulary). ⛔ When eventually designed,
    /// must contribute via ActorHub (IActorStatSubsystem / registered atom reader) or consume Hub
    /// output only — never a private per-sector combat fold (CLAUDE.md "One ActorHub compose / one
    /// read"). Refused by Validate today.</summary>
    DefensePower,

    /// <summary>Reserved — an aura/buff for defending units, not just a flat stat add. Needs a
    /// world-map-to-battle aura delivery path that does not exist today. Same ActorHub warning as
    /// DefensePower applies once designed. Refused by Validate today.</summary>
    AuraGrant,

    /// <summary>Reserved — a buff reaching every sector/legion the faction owns. WorldFaction.ScopeModifierMilli
    /// is the storage; each consumer is its own wiring task. Refused by Validate today.</summary>
    EmpireBuff
}
```

**Naming note, a deliberate deviation from the map's literal three-name list, stated so no downstream
module is surprised.** The map and ideal doc name `WonderEffectKind`, `WonderEffectScope`, and
`WonderEffectDef` as three separate things. This module ships all three *concepts* but **does not**
declare a second, byte-identical `WonderEffectScope` enum — `WonderEffectDef.Scope` (§Design 3) is
typed as the same `WonderScope` enum above. The ideal doc's own design already states the two are
meant to "move together" and that an effect's scope is "set by the Wonder's `WonderScope`, not chosen
freely per instance" (ideal doc §The effect-kind vocabulary) — two enums with an identical,
co-mandated member set and no independent runtime meaning is duplication, not a distinct vocabulary,
and this repo's SOLID/DRY hard rule reads that as a defect to avoid, not a naming contract to honor
literally. `Validate` enforces the "moves together" invariant directly (§Design 5: an effect's
`Scope` must equal its owning row's `WonderScope`). Every downstream consumer that expected a type
named `WonderEffectScope` should read `WonderScope` instead — the vocabulary (which values exist, and
which are reserved) is unchanged.

### 3. `WonderEffectDef`

```csharp
/// <summary>One thing a Wonder does. A row's WonderEffects list holds one or more of these — v1
/// content authors exactly one, Kind = LoamGenerationRate (Validate enforces the Kind restriction,
/// not the count restriction, so a future wave can add a second live Kind to an existing Wonder's
/// list without a schema change).</summary>
public sealed record WonderEffectDef
{
    public WonderEffectKind Kind { get; init; }

    /// <summary>Must equal the owning StructureDef.WonderScope — Validate enforces this (§Design 5).
    /// Carried on the effect itself, not merely inferred from the parent row, so a WonderEffectDef
    /// travels as a self-describing unit into whatever consumer wonder-effect-empire (module 3)
    /// builds, without that consumer needing to also thread the parent StructureDef through.</summary>
    public WonderScope Scope { get; init; }

    /// <summary>Per-mille magnitude, tunable-by-content (seed-authored, see §Tunables).
    /// <para><b>For a Sector-scope LoamGenerationRate effect, this field is DESCRIPTIVE, not the
    /// consumed magnitude</b> — the real number `LoamProduction.For` reads is this same row's own
    /// `YieldMultiplierMilli`/`FlatYieldPerTurn` (already-existing StructureDef fields, unchanged),
    /// because a Sector-scope Wonder sits on one slot in one sector and the existing per-slot fields
    /// already reach exactly that sector, zero new plumbing. `ValueMilli` here should read the same
    /// delta a content author put in those fields, for tooltip/UI consistency — Validate does not
    /// cross-check the two this wave (a future content-QA pass, not an architecture requirement).</para>
    /// <para><b>For an Empire-scope LoamGenerationRate effect, this field IS the consumed magnitude</b>
    /// — there is no existing "reaches every sector this faction owns" field to reuse (`LoamProduction.For`
    /// only ever reads the one sector passed to it, `LoamProduction.cs:18`). `ValueMilli` is what
    /// module 3 (`wonder-effect-empire`) reads off every built Empire-scope Wonder for a faction, sums
    /// (decisions.md's locked SUM rule), and writes into that faction's `WorldFaction.ScopeModifierMilli`
    /// — once that field's own persistence gap (Built table above) is also closed.</para></summary>
    public long ValueMilli { get; init; }
}
```

### 4. `StructureDef` and `StructureCorpus` field additions

`StructureDef` (`StructureCatalog.cs:42-194`) gains three fields, following `ContainerId`'s exact
nullable-optional convention (`:193`):

```csharp
/// <summary>Null for every ordinary structure. Set only on a Wonder-tier row (loam-relics-and-wonders
/// `wonder-structure`) — an orthogonal facet, mirroring <see cref="Obstacle"/>'s own "a StructureDef
/// facet, not a parallel system" shape (§Design 1). Kind is unchanged by this — a Wonder stays
/// Kind = LoamSource/Yield, reusing YieldMultiplierMilli/FlatYieldPerTurn exactly as an ordinary row
/// does. World/Multiverse are named but refused by Validate this wave (§Design 5).</summary>
public WonderScope? WonderScope { get; init; }

/// <summary>Only meaningful when WonderScope is set — Validate enforces the pairing (§Design 5).</summary>
public WonderRarity? WonderRarity { get; init; }

/// <summary>Empty for every ordinary structure; one or more rows on a Wonder. Validate refuses any
/// unregistered WonderEffectKind and any Scope mismatch (§Design 5).</summary>
public IReadOnlyList<WonderEffectDef> WonderEffects { get; init; } = Array.Empty<WonderEffectDef>();
```

`StructureMagnitudes` (`StructureSeed/StructureCorpus.cs:20-37`) needs the wire-format siblings. Every
existing field in this record is parsed with `Require*` (throws if absent) — a Wonder-only field must
instead be genuinely optional, matching `ContainerId`'s own already-proven optional-parse pattern
(`ParseRow:132-134`), so the 25 shipped rows load completely unmodified:

```csharp
public sealed record StructureMagnitudes(
    string StructureKind,
    // ...existing fields, unchanged...
    string? ContainerId,
    string? WonderScope = null,
    string? WonderRarity = null,
    IReadOnlyList<WonderEffectMagnitude>? WonderEffects = null);

public sealed record WonderEffectMagnitude(string Kind, string Scope, long ValueMilli);
```

`ParseRow` (`StructureCorpus.cs:93-138`) gains, inside the existing `magnitudes` block:

```csharp
WonderScope: m.TryGetProperty("wonderScope", out var ws) && ws.ValueKind == JsonValueKind.String
    ? ws.GetString() : null,
WonderRarity: m.TryGetProperty("wonderRarity", out var wr) && wr.ValueKind == JsonValueKind.String
    ? wr.GetString() : null,
WonderEffects: m.TryGetProperty("wonderEffects", out var we) && we.ValueKind == JsonValueKind.Array
    ? we.EnumerateArray().Select(e => new WonderEffectMagnitude(
          e.GetProperty("kind").GetString()!, e.GetProperty("scope").GetString()!,
          e.GetProperty("valueMilli").GetInt64())).ToList()
    : null,
```

`StructureCatalog.ToStructureDef` (`:256-283`) gains, matching the existing `Enum.Parse<T>(string)`
convention exactly (no `ignoreCase`, matching `structureKind`/`obstacleKind`):

```csharp
WonderScope = row.Magnitudes!.WonderScope is { } wsStr ? Enum.Parse<WonderScope>(wsStr) : null,
WonderRarity = row.Magnitudes!.WonderRarity is { } wrStr ? Enum.Parse<WonderRarity>(wrStr) : null,
WonderEffects = row.Magnitudes!.WonderEffects?
    .Select(e => new WonderEffectDef {
        Kind = Enum.Parse<WonderEffectKind>(e.Kind),
        Scope = Enum.Parse<WonderScope>(e.Scope),
        ValueMilli = e.ValueMilli })
    .ToList() ?? Array.Empty<WonderEffectDef>(),
```

### 5. `Validate` — the enforcement that keeps the reservation real

`StructureCatalog.Validate` (`:305-346`) gains, per structure, in the same loud-over-silent style as
every existing check in that method:

```csharp
if ((s.WonderScope is null) != (s.WonderRarity is null))
    throw new InvalidOperationException(
        $"Structure '{s.StructureId}' must set both WonderScope and WonderRarity, or neither.");
if (s.WonderScope is not (null or WonderScope.Sector or WonderScope.Empire))
    throw new InvalidOperationException(
        $"Structure '{s.StructureId}' authors WonderScope={s.WonderScope}, which is reserved and " +
        "not yet registered (loam-relics-and-wonders `wonder-structure` §Design 5).");
if (s.WonderScope is not null && s.WonderEffects.Count == 0)
    throw new InvalidOperationException(
        $"Structure '{s.StructureId}' is a Wonder (WonderScope set) but names no WonderEffectDef.");
if (s.WonderScope is null && s.WonderEffects.Count > 0)
    throw new InvalidOperationException(
        $"Structure '{s.StructureId}' names WonderEffects but has no WonderScope — not a Wonder.");
foreach (var effect in s.WonderEffects)
{
    if (effect.Kind != WonderEffectKind.LoamGenerationRate)
        throw new InvalidOperationException(
            $"Structure '{s.StructureId}' authors WonderEffectKind={effect.Kind}, which is reserved " +
            "and not yet registered (loam-relics-and-wonders `wonder-structure` §Design 5).");
    if (effect.Scope != s.WonderScope)
        throw new InvalidOperationException(
            $"Structure '{s.StructureId}' has a WonderEffectDef whose Scope ({effect.Scope}) does not " +
            "match its own WonderScope ({s.WonderScope}).");
    if (effect.ValueMilli < 0)
        throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative WonderEffectDef.ValueMilli.");
}
if (s.WonderEffects.Select(e => (e.Kind, e.Scope)).Distinct().Count() != s.WonderEffects.Count)
    throw new InvalidOperationException($"Structure '{s.StructureId}' has a duplicate (Kind, Scope) WonderEffectDef pair.");
```

This is the concrete answer to "cannot be accidentally selected/constructed today": a catalog row
naming `World`/`Multiverse` or any of the three reserved effect kinds is a **startup error**, not a
silently-accepted inert value — matching `DropTableValidator.UndesignedSourceKind`'s own precedent for
refusing `pvz-run` outright (cited in the ideal doc) and this repo's own "loud over silent" catalog
discipline used for every other `StructureDef` field.

### 6. `WonderPolicy.ExistenceCapFor` — the tunable existence cap

```csharp
namespace FusionRpg.Core.World;

/// <summary>Every tunable Wonder constant, mirroring LoamPolicy's Configure/static-holder shape
/// exactly (LoamPolicy.cs:1-21). Values live in data/tuning/loam-relics-wonders.v1.json.</summary>
public static class WonderPolicy
{
    static WonderTuning? _tuning;

    public static void Configure(WonderTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static WonderTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "WonderPolicy.Configure(...) has not run. Every Wonder rule reads " +
        "data/tuning/loam-relics-wonders.v1.json — there is no built-in default to fall back to.");

    /// <summary>Common = uncapped (long.MaxValue, the RpgStore.MaxSoulAwardFrom "dynamic headroom"
    /// idiom, decisions.md Caps (project-wide) — never a silent hard cap). Unique = a tunable count
    /// per scope. World/Multiverse throw — Validate already refuses them as a WonderScope on any
    /// catalog row, so this method is never called with either in practice; it still names them
    /// loudly rather than returning a made-up number if a future caller ever tries.</summary>
    public static long ExistenceCapFor(WonderScope scope, WonderRarity rarity)
    {
        if (rarity == WonderRarity.Common) return long.MaxValue;
        return scope switch
        {
            WonderScope.Sector => Tuning.UniqueExistenceCap.Sector,
            WonderScope.Empire => Tuning.UniqueExistenceCap.Empire,
            _ => throw new ArgumentOutOfRangeException(nameof(scope),
                $"WonderScope.{scope} has no registered existence-cap tunable (reserved, unregistered).")
        };
    }
}
```

**Who calls this, and when — named, not built here.** The cap only matters at Wonder *construction*
time (does building this Wonder push the scope-unit's count of already-built `Unique` Wonders past
the cap?), which requires scanning live `WorldState` structures for existing Wonders of the same
scope/rarity/scope-unit (sector or faction) — a query this module does not need and does not build.
`wonder-build-flow` (module 4) is the sole caller (§Interface exposed to dependents).

### 7. Basic-material cost — confirmed reuse, no new field

`ConstructRubbleCost`/`ConstructIronworkCost` (`StructureCatalog.cs:87,90`) already exist, already
default to 0, and are already spent by the exact same siege-construction machinery
(`ConstructionActions.cs`, not modified by this module) every other structure uses. A Wonder authors
nonzero values in these two fields directly — no new field, no new engine code. Re-verified fresh this
session (decisions.md's own text already states this; this module adds no correction).

### 8. Relic cost — deliberately not this module's field

The ideal doc names a relic-denominated cost as "genuinely missing from `StructureDef` today," but
resolving it here would be premature: relics are rolled, individually-identified items, not a stock
count (`relic-item-kind`'s own scope), so "cost" is not a `long RelicCost` magnitude the way
`Cost`/`ConstructRubbleCost` are — it is a **recipe**: "consume one relic instance matching some
kind/rarity" is a verb parameter, not a catalog field. This module adds **no** relic-cost field to
`StructureDef`. `wonder-build-flow` (module 4) designs the recipe shape once `relic-item-kind`'s own
schema question (module 1) is settled.

## Tunables

| Number | Home | Notes |
|---|---|---|
| `UniqueExistenceCap.Sector`, `UniqueExistenceCap.Empire` | `data/tuning/loam-relics-wonders.v1.json` (new domain file — this module's own first write to it) | **A policy constant, correctly a `data/tuning/*.json` value** — one number per scope, not authored per-Wonder-row content. Never `1` by default; the file must name a real, reviewed number. `long`, per §Design 6 |
| `WonderScope`/`WonderRarity`/`WonderEffectDef.ValueMilli` per Wonder row | `data/seed/structures/**` (generated seed content, via the existing `structures` seedsmith adapter) | **Correction to the map's own Tunables table**, mirroring the identical correction `spec-sector-storage.md` already made for `CapacityBonus`/`ItemStorageCapacityBonus`: a per-row `StructureDef` magnitude is `StructureCorpus` seed content (`StructureCatalog.cs:256-283`), never a bare `data/tuning/*.json` entry. The map listed these under `loam-relics-wonders.v1.json`; the real precedent says otherwise |
| `YieldMultiplierMilli`/`FlatYieldPerTurn`/`ConstructRubbleCost`/`ConstructIronworkCost` for a Wonder row | Same seed-content home as every other structure's identical fields — unchanged, no new tunable | Confirms §Design 7 — nothing new to tune, only new rows to author |
| Relic-to-Wonder recipe cost | Not this module's tunable — `wonder-build-flow`'s (module 4) | §Design 8 |
| `Empire`/`World`-scope combination rule (SUM) | Structural constant, not a tunable number (decisions.md) — module 3's file, not this module's | Named here only so a reader does not look for it in this module's own tuning surface |

## Numeric types

`WonderEffectDef.ValueMilli` is `long` — a per-mille magnitude (`CLAUDE.md` "Numeric overflow" rule 1:
`long` for any magnitude). `WonderPolicy.ExistenceCapFor`'s return value is `long`, matching every
other existence-cap-shaped number in this codebase (`RpgStore.MaxSoulAwardFrom`). `WonderScope`/
`WonderRarity`/`WonderEffectKind` are plain enums (structural, not magnitudes — no overflow concern).
No field in this module is level/power-derived: the one place a Wonder's magnitude touches the power
ladder is `StructureDef.MaxHpOf` (`StructureCatalog.cs:129-135`), an existing function every
`MaterialTier > 0` structure already uses unchanged — this module introduces no `f(level)` of its own,
satisfying "One power ladder" by construction rather than by exemption.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WonderCatalog"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~StructureCatalogImportTests"  # byte-identity of the 25 existing rows must still hold
.\scripts\guard-dal.ps1        # no SQL introduced by this module at all — a green run with zero new hits is the proof
```

## Structure

```
src/FusionRpg.Core/World/StructureCatalog.cs           MODIFIED — StructureDef gains WonderScope?/
                                                        WonderRarity?/WonderEffects (§Design 4);
                                                        ToStructureDef parses them (§Design 4);
                                                        Validate gains the reservation checks (§Design 5)
src/FusionRpg.Core/World/StructureSeed/StructureCorpus.cs
                                                        MODIFIED — StructureMagnitudes gains three
                                                        optional fields; ParseRow parses them via
                                                        TryGetProperty, matching ContainerId's own
                                                        optional-parse precedent (§Design 4)
src/FusionRpg.Core/World/WonderCatalog.cs              NEW — WonderScope, WonderRarity, WonderEffectKind,
                                                        WonderEffectDef, WonderPolicy (§Design 2, 3, 6)
src/FusionRpg.Core/World/Loam/WonderTuning.cs           NEW — the tuning DTO WonderPolicy.Configure takes
                                                        (mirrors LoamTuning's own shape)
data/tuning/loam-relics-wonders.v1.json                NEW — UniqueExistenceCap.{Sector,Empire} (§Tunables)
tests/FusionRpg.Core.Tests/World/WonderCatalogTests.cs NEW
UNTOUCHED: LoamProduction.cs, LoamPhases.cs, WorldFaction/WorldState.cs record shapes (no new field —
           this module's vocabulary lives entirely on StructureDef), ClaimResolver.cs,
           ConstructionActions.cs, every one of the 25 existing data/seed/structures/**/*.json rows.
```

## Code style

```csharp
// Mirrors Obstacle's own "a StructureDef facet, not a parallel system" precedent (StructureCatalog.cs:137-143) —
// an orthogonal field on the same record, independent of Kind, null for every ordinary row.
public WonderScope? WonderScope { get; init; }
```

## Testing strategy

- **The 25 shipped structure rows stay byte-identical.** `StructureCatalogImportTests`'s own existing
  identity test (`StructureCatalog.cs:214`'s own citation) must still pass unmodified — every new
  field is optional-with-a-default, so no existing seed file needs a single-byte edit.
- **A `Sector`-scope, `Common`-rarity Wonder loads and is indistinguishable from an ordinary
  `Yield`/`LoamSource` row to `LoamProduction.For`** — proving §Design 1's central claim that no
  engine change was needed for the Sector case.
- **`Validate` refuses `World`/`Multiverse`** with a message naming the reserved scope, not a generic
  parse failure.
- **`Validate` refuses each of `DefensePower`/`AuraGrant`/`EmpireBuff`** individually, same discipline.
- **`Validate` refuses a `WonderScope`/`WonderRarity` pairing mismatch** (one set, the other null).
- **`Validate` refuses a `WonderEffectDef.Scope` that disagrees with its own row's `WonderScope`.**
- **`Validate` refuses a duplicate `(Kind, Scope)` pair** within one row's `WonderEffects`.
- **`WonderPolicy.ExistenceCapFor(scope, Common)` returns `long.MaxValue`** for both live scopes,
  never a finite number, proving "no hard cap for Common" is enforced, not merely documented.
- **`WonderPolicy.ExistenceCapFor(scope, Unique)` reads the tunable file's own number** — changing the
  tuning file's value changes the returned cap with no code change, proving it is data, not a
  constant.
- **`WonderPolicy.ExistenceCapFor` throws before `Configure` runs** — matching `LoamPolicy`'s own
  "no built-in default" discipline.

## Boundaries

- **Always:** `WonderScope?`/`WonderRarity?`/`WonderEffects` stay orthogonal facet fields on
  `StructureDef`, never a new `StructureKind` value; every reserved enum member stays refused at
  `Validate`, never silently accepted; `Unique` existence caps come from `WonderPolicy`, never a
  hard-coded `1` anywhere this module writes.
- **Ask first:** registering `World`/`Multiverse` or any of `DefensePower`/`AuraGrant`/`EmpireBuff`
  as live (removing their `Validate` refusal) without the named prerequisite work landing first
  (§Design 5's own doc comments name each one's blocker).
- **Never:** a private per-sector or per-faction combat-power fold for `DefensePower`/`AuraGrant` if
  a future session designs them — they must contribute via `ActorHub`, per this repo's binding "One
  ActorHub compose / one read" rule (CLAUDE.md); a second, parallel Wonder-content generator program
  (the seedsmith `structures` adapter already exists and already produces this exact corpus shape —
  reuse it, per the ideal doc's own "Alternatives rejected" table).

## Success criteria

1. `StructureKind` is unchanged at 5 members — this module adds none. 2. `WonderScope`, `WonderRarity`,
`WonderEffectKind`, `WonderEffectDef` exist, closed, each reserved member refused by `Validate`.
3. A `Sector`-scope Wonder row loads through the existing `StructureCatalog.All` pipeline and is read
by the existing, unmodified `LoamProduction.For`. 4. `WonderPolicy.ExistenceCapFor` never returns a
hard-coded `1` for `Unique`. 5. `guard-dal.ps1` green (no SQL touched). 6. All 25 existing structure
seed rows remain byte-identical.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `WonderScope`, `WonderRarity` enums; `StructureDef.WonderScope`/`.WonderRarity`/`.WonderEffects` | `wonder-build-flow` (module 4) — reads these off a `StructureDef` to know what it is building and what cap applies |
| `WonderEffectKind`, `WonderEffectDef` (`Kind`, `Scope`, `ValueMilli`) | `wonder-effect-empire` (module 3) — for `Sector`-scope, no consumption needed (already handled by `YieldMultiplierMilli`/`FlatYieldPerTurn`, §Design 3); for `Empire`-scope, module 3 reads `ValueMilli` off every built Empire-scope Wonder for a faction and sums it (locked SUM rule) into `WorldFaction.ScopeModifierMilli` |
| `WonderPolicy.ExistenceCapFor(WonderScope, WonderRarity)` | `wonder-build-flow` (module 4) — the exact call site for its own construction-time existence-cap refusal; this module does not implement the refusal or the "how many already exist" scan itself |
| **The `ScopeModifierMilli` persistence gap** (Built table above: no `scope_modifier_milli` column anywhere, confirmed at `RpgStore.World.cs:37-44,270-278,462-472` and `RpgStore.WorldGraphDiff.cs:97-109`) | `wonder-effect-empire` (module 3) — named here so module 3 does not re-derive it as merely "add a first reader"; it must also add the column and its INSERT/SELECT/diff wiring, or an Empire-scope Wonder's effect silently resets on every world reload |
| The finding that a Wonder needs **no new seedsmith role/slot-kind pairing** (§Design 1) | Any future seedsmith `structures` content pipeline extension authoring real Wonder catalog rows |

## Design-gate checklist

```
[x] Subsystems: world-map structure/economy state (Core) — no Status/ActorHub/Combat subsystem
    touched; the two effect kinds that WOULD touch ActorHub (DefensePower/AuraGrant) are refused by
    Validate this wave, and their own doc comments warn the future session that registers them.
[x] Read this session: loam-relics-and-wonders-ideal.md (full, both pages); loam-relics-and-wonders-map.md
    (full); decisions.md "Loam relics and wonders SSOT (2026-09-13)" (full row, quoted verbatim above);
    scoped-inventory-hierarchy/spec-sector-storage.md (full, house style + the ItemStorage precedent
    this module's own §Design 1 argues against); scoped-inventory-hierarchy/spec-legion-cargo.md
    (full, house style); DESIGN-GATE.md §1 rows: Economy/currencies/yields, World map, Any cap or
    ceiling, Any tunable number, Data/SQL/schema.
[x] Code cited by file:line, opened fresh this session: StructureCatalog.cs (:10-39 StructureKind,
    :42-194 StructureDef, :52-193 individual fields, :121-135 MaxHpOf/power ladder, :137-143 Obstacle
    precedent, :200-347 catalog/Validate); StructureSeed/StructureCorpus.cs (:20-159, full file);
    LoamProduction.cs (:1-84, full file); LoamPhases.cs (:1-90, EffectiveCapacity); WorldState.cs
    (:70-146,148-210 WorldFaction/WorldSlot/WorldSector); WorldCanonical.cs (:97-98); RpgStore.World.cs
    (:37-44,270-278,462-472); RpgStore.WorldGraphDiff.cs (:97-109); FactionKindCatalog.cs (full);
    RelicCatalog.cs (:1-30); LoamPolicy.cs (:1-21); StructurePolicy.cs (grep-confirmed);
    data/seed/structures/extract/well.json (full); data/tuning/structure-seed.v1.json (full, tuning
    file shape precedent); grep confirming no bare `Rarity` enum exists in FusionRpg.Core.
[x] Corrected (strengthen pass, 2026-09-13): §Design 5's `Validate` snippet originally typo'd
    `World.Sector`/`World.Empire` (not valid C# — no such type) where it meant
    `WonderScope.Sector`/`WonderScope.Empire`, the enum this same module declares in §Design 2. Fixed
    in place; the design intent (refuse `World`/`Multiverse`) was never in question, only the literal
    code sample.
[x] Drift reported: the map's own "a 7th `StructureKind` value" framing is stale — the real enum has
    5 members, not 6, because `ItemStorage` is spec'd but unbuilt (§Design 1); the map's own Tunables
    table mis-homes per-row Wonder magnitudes under `data/tuning/loam-relics-wonders.v1.json` when the
    real precedent (already corrected once by `spec-sector-storage.md` for `CapacityBonus`) says seed
    content (§Tunables); a new, previously-uncited real gap found this session — `WorldFaction.ScopeModifierMilli`
    is hashed but never persisted to SQL (Built table, `RpgStore.World.cs`/`RpgStore.WorldGraphDiff.cs`
    citations) — handed to module 3, not silently left for it to rediscover; one deliberate naming
    deviation from the map's literal type list (`WonderEffectScope` merged into `WonderScope`, §Design 2).
[x] No §2 invariant contradicted: no SQL touched by this module at all (guard-dal.ps1 trivially
    green); no cap on a magnitude presented as a progression ceiling (`Unique` existence caps are
    tunable counts via `WonderPolicy`, `Common` is explicitly uncapped); no `f(Θ)` introduced (the
    one power-ladder touch, `MaxHpOf`, is pre-existing and unchanged); no second ActorHub composer
    (DefensePower/AuraGrant are refused, not designed, and carry a standing warning for whoever
    eventually does); no second ownership root (this module owns no item/relic state at all).
[ ] The `Empire`-scope combination rule (SUM) and the `LoamProduction.For` faction-input plumbing are
    module 3's build, not designed further here — correctly deferred, not assumed solved.
[ ] The relic-to-Wonder recipe shape (§Design 8) was not designed this session — correctly deferred to
    `wonder-build-flow` (module 4), which itself waits on `relic-item-kind` (module 1) and the external
    `scoped-inventory-hierarchy` dependency.
```
