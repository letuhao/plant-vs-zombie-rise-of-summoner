# Spec: `wonder-effect-empire`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session, independently of (and cross-checked against) the citations already made by the sibling
`wonder-structure` spec. Module id `wonder-effect-empire`, row 3 of the
[loam-relics-and-wonders map](../loam-relics-and-wonders-map.md) (wave 2, depends on
`wonder-structure`). Ideal: [loam-relics-and-wonders-ideal.md](../loam-relics-and-wonders-ideal.md)
§The shape ("Four scopes, gated by `WonderScope`"), §The effect-kind vocabulary, Real-gap table rows
on `LoamProduction.For`'s missing faction input and the `ScopeModifierMilli` combination rule.
Decisions: [decisions.md](../decisions.md) "Loam relics and wonders SSOT (2026-09-13)" (quoted in
full below). Direct dependency: [spec-wonder-structure.md](spec-wonder-structure.md) (module 2, the
vocabulary this module reads — not re-derived, only consumed). House style and overlay-module
precedent: [scoped-inventory-hierarchy/spec-legion-cargo.md](../scoped-inventory-hierarchy/spec-legion-cargo.md)
(sibling program, not a dependency).

**Decision quoted verbatim (decisions.md, "Loam relics and wonders SSOT"):** *"Multiple simultaneous
`Empire`-scope Wonders on one faction combine by **SUM**, matching `LoamProduction`'s existing
additive discipline for structure yields (adopted as the working design, reviewable at build time —
a structural constant, not a tunable number)."*

## Objective

Wire a built `Empire`-scope Wonder's `WonderEffectDef` (Kind = `LoamGenerationRate`, shipped by
`wonder-structure`) into the real loam economy, and close the `WorldFaction.ScopeModifierMilli`
persistence gap `wonder-structure` found and handed to this module by name. This is the **only** real
new work `loam-relics-and-wonders` needs for the economy side — `Sector`-scope Wonders already work
with zero engine changes (re-confirmed fresh, §Design 0), and `World`/`Multiverse` scope stay
reserved/refused by `StructureCatalog.Validate` (module 2's own enforcement, untouched here).

Success looks like: a faction with two built `Empire`-scope Wonders authoring `valueMilli: 200` and
`valueMilli: 300` sees `WorldFaction.ScopeModifierMilli == 1500` (`1000 + 200 + 300`) recomputed every
Production phase; every one of that faction's sectors' loam yield is multiplied by that value before
the existing capacity/overflow logic runs; saving and reloading the world (or a normal turn-commit
diff) preserves that number exactly, where today it silently resets to `1000`; a faction with no
`Empire`-scope Wonder is byte-identical to today, on every existing golden.

## Locked anchors

- **SUM is a structural constant, not a tunable** (decisions.md, quoted above). This module ships no
  new `data/tuning/*.json` row for it — confirmed fresh against the map's own Tunables table row 4
  (§Tunables below).
- **`Sector`-scope needs no plumbing from this module** — `wonder-structure`'s own finding, re-verified
  independently this session by re-reading `LoamProduction.cs` in full (§Design 0). This module's
  entire scope is the `Empire`-scope path.
- **The `ScopeModifierMilli` persistence gap is this module's to close, not merely "add a first
  reader"** — `wonder-structure`'s own "Interface exposed to dependents" table names this explicitly:
  a new `scope_modifier_milli` SQL column plus INSERT/SELECT/diff wiring, or an Empire-scope Wonder's
  effect silently resets on every world reload (§Design 5).
- **`TurnEngine.Step` stays pure — no live DB read mid-step.** Every number this module reads comes
  from the already-loaded, in-memory `WorldState` (`Sectors[].Slots[].StructureId` resolved through
  the process-wide, in-memory `StructureCatalog`) — never a `FusionRpg.Data` call from inside a turn
  phase. Confirmed against `LoamPhases.cs`'s own header comment (§Design 1).
- **No new closed vocabulary.** This module consumes `WonderScope`/`WonderEffectKind`/
  `WonderEffectDef` exactly as `wonder-structure` ships them (module 2's own enum/record, not
  re-declared or widened here) and adds none of its own.
- **`WorldFaction.ScopeModifierMilli`'s pre-existing `int` type is inherited debt, not this module's
  to fix.** Widening it to `long` is a `WorldState.cs`/`WorldCanonical.cs`/SQL-column-type change with
  its own golden-moving blast radius — out of scope. This module mitigates with `checked` arithmetic
  so an overflow throws rather than silently wraps (§Numeric types).

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `TurnEngine`'s `Production` wrapper already forwards the **full** `WorldState` into `LoamPhases.Production` — no `TurnEngine.cs` signature change is needed anywhere for this module | `TurnEngine.cs:264-276` (the `Production` method), specifically `:267`: `var next = LoamPhases.Production(world, report, Phases.Production);` |
| `LoamPhases.Production(WorldState world, TurnReport report, string phase)` already receives `world` in full, so `world.Factions` is directly reachable inside it today — the real gap is entirely inside this function and `LoamProduction.For`, never at the `TurnEngine` call site | `LoamPhases.cs:18` |
| `LoamProduction.For(WorldSector sector)` has **no faction-level input in its signature at all** — re-confirmed fresh, independently of `wonder-structure`'s own citation | `LoamProduction.cs:18` |
| `LoamProduction.For(string? ownerFactionId, IEnumerable<string> slotTypeIds)` — the belief-side overload, used only by `FrontierRulesPolicy.cs:188` for AI scouting decisions, not a real-yield consumer. Out of this module's scope: belief-side production is a coarse estimate that already ignores `YieldMultiplierMilli`/`FlatYieldPerTurn`/`DevelopmentYield` entirely (it counts Rootbed slots only), so it has never tried to track a Wonder's effect and this module does not start now | `LoamProduction.cs:70-83`; `FrontierRulesPolicy.cs:188` |
| **New finding this session, not named by either the ideal doc or `wonder-structure`'s own citations:** `LoamProduction.For(WorldSector sector)` has **four** real call sites today, not one. Three of them are not `LoamPhases.Production` and would silently keep reporting stale (too-low) numbers for any faction with a built `Empire`-scope Wonder unless each is also updated | `LoamPhases.cs:30` (the writer, `Production` phase); `LoamForecast.cs:55` (`ProjectedStock`, the player-facing "stock next turn" projection); `LoamBalance.cs:13` (`PerSector(WorldState, WorldSector)`, the truth-side balance harness `LoamForecast.Weakest`/`.PerFaction` both call transitively); `WorldEndpoints.cs:631` (a REST debug/loam-state view) — confirmed by grepping every call site in `src/` this session, not merely re-citing the one the ideal doc already knew about |
| All three non-`Production` call sites already have `WorldState`/`world` in scope at the point they call `LoamProduction.For` — resolving the owning faction's stored `ScopeModifierMilli` there costs a `FirstOrDefault` lookup, no new parameter threading through unrelated call chains | `LoamForecast.cs:49-61` (`world` is already a parameter); `LoamBalance.cs:12-13` (`world` is already a parameter); `WorldEndpoints.cs:615-644` (`world`/`sector` already resolved in the surrounding loop) |
| `WorldFaction.ScopeModifierMilli` — `int`, default `1000`, doc comment: *"a standing world-map buff/debuff on this faction, per-mille, 1000 = no modifier... applied by whichever future consumer['s] own compute path reads it"* | `WorldState.cs:85-93` |
| `ScopeModifierMilli` is hashed into `WorldCanonical.Write`, **conditionally** — a row is emitted only when the value differs from the default `1000`, specifically so every pre-existing save/golden that never touches this field emits exactly the bytes it always did | `WorldCanonical.cs:90-99` (`if (f.ScopeModifierMilli != 1000) Row(sb, "faction-scope", f.FactionId, f.ScopeModifierMilli);`), re-verified fresh this session — matches `wonder-structure`'s own citation exactly |
| **The SQL persistence gap, re-verified independently this session, not merely re-cited:** `rpg_world_factions`'s `CREATE TABLE` has exactly 5 columns (`world_id, faction_id, kind, name, policy_id`) — no `scope_modifier_milli`. `WriteWorldGraphUnlocked`'s faction INSERT and `LoadWorldGraphUnlocked`'s faction SELECT both use the identical 6-column list (`world_id, faction_id, kind, name, policy_id, upkeep_handicap_milli`) — still no `scope_modifier_milli`. `RpgStore.WorldGraphDiff.cs`'s `DiffFactions` has the identical omission in its own `INSERT OR REPLACE`. A `WorldFaction` loaded from disk always gets the record's compiled-in default (`1000`) for this field, never whatever was actually in memory when the world was last saved | `RpgStore.World.cs:37-44` (CREATE), `:269-278` (`WriteWorldGraphUnlocked` INSERT), `:461-473` (`LoadWorldGraphUnlocked` SELECT, `UpkeepHandicapMilli = r.GetInt32(4)` is the last field read — no sixth); `RpgStore.WorldGraphDiff.cs:97-117` (`DiffFactions`, same 6-column `INSERT OR REPLACE`) |
| **The exact migration idiom this module needs already exists in the same file, for the sibling field this record already carries** — `EnsureColumn`, not a `CREATE TABLE` edit, because an existing database never re-runs `CREATE TABLE` | `RpgStore.World.cs:134-136` (the file's own comment: *"Additive columns go through EnsureColumn, not the CREATE above"*), `:144` (`EnsureColumn(db, "rpg_world_factions", "upkeep_handicap_milli", "INTEGER NOT NULL DEFAULT 1000")`) |
| `DiffFactions`'s own skip check compares the **full** `WorldFaction` record by structural equality (`was == f`) before writing anything — once `ScopeModifierMilli` round-trips correctly, a turn that changes it is written and a turn that doesn't is silently skipped, with zero additional logic needed in this function beyond adding the column/parameter | `RpgStore.WorldGraphDiff.cs:111-116`, specifically `:113` |
| `LoamPhases.Production`'s existing "decrement-then-read-same-pass" ordering: a structure's `ConstructionTurnsRemaining` is decremented **first**, and the very same pass's yield check reads the **post-decrement** value — so a structure finishing construction this turn is already active this turn, not next | `LoamPhases.cs:24-27` (own doc comment), `:28-30` (`DecrementConstruction` called, then `LoamProduction.For(decremented)` reads the result) |
| **`Sector`-scope needs zero new plumbing — re-confirmed independently this session, not merely re-cited from `wonder-structure`.** The Rootbed loop already reads any active slot structure's `YieldMultiplierMilli`; the flat loop already reads any active structure's `FlatYieldPerTurn` regardless of slot kind, both already summed into one sector's `total` before this module's own multiply step runs | `LoamProduction.cs:22-36` (Rootbed loop), `:43-51` (flat loop), `:59` (`DevelopmentYield.For` added to the same `total`) |
| The locked turn-phase order places `Production` (where this module's write happens) before `Snapshot` (where `WorldCanonical.Write` hashes the state) — a freshly recomputed `ScopeModifierMilli` is hashed the same turn it changes, matching the L25/L27 precedent that a field's value changing across a turn is a real, hashed behaviour change | decisions.md, "World turn phase order" row: `Reveal → Movement → Sieges → Production → Growth → Pressure → Events → Snapshot → Intel` |
| `WonderScope`, `WonderEffectKind`, `WonderEffectDef` (`Kind`, `Scope`, `ValueMilli`) and `StructureDef.WonderScope?`/`.WonderEffects` — the closed vocabulary this module reads, shipped by module 2. `StructureCatalog.Validate` already enforces `effect.Scope == structure.WonderScope` and refuses any `WonderEffectKind` other than `LoamGenerationRate`, so this module's own scan does not need to re-validate either pairing — only read it | [spec-wonder-structure.md](spec-wonder-structure.md) §Design 2, 3, 5 (not yet built code — cited as the module-2 contract this module 3 spec is written against, per the map's own dependency order) |

### Real gap

| Gap | What this module builds |
|---|---|
| No faction-level input on `LoamProduction.For(WorldSector sector)` | §Design 2 — a new `int scopeModifierMilli = 1000` parameter, applied once at the end of the existing sum |
| No function anywhere computes "the sum of every built `Empire`-scope Wonder's `LoamGenerationRate` `ValueMilli` for one faction" | §Design 1, 3 — `WonderEmpireEffects.ComputeScopeModifierMilli`, this module's own centerpiece |
| The `scope_modifier_milli` SQL column and its INSERT/SELECT/diff wiring | §Design 5 |
| **Found this session, not previously named by the ideal doc or `wonder-structure`:** three real, non-`Production` call sites of `LoamProduction.For` (`LoamForecast.ProjectedStock`, `LoamBalance.PerSector`, `WorldEndpoints.cs:631`) would silently under-report for any faction with a built `Empire`-scope Wonder unless each resolves and passes the stored modifier too | §Design 4 |

## Design

### 0. Confirming `Sector`-scope needs nothing from this module

Re-read `LoamProduction.cs` in full this session, independently of `wonder-structure`'s own claim.
The Rootbed loop (`:22-36`) already multiplies `LoamPolicy.SeepPerTurn` by any active structure's
`YieldMultiplierMilli`; the flat loop (`:43-51`) already adds any active structure's
`FlatYieldPerTurn` regardless of slot kind. A `WonderScope.Sector` row authoring either field is,
to this code, indistinguishable from an ordinary Well/Extractor row — no branch anywhere reads
`WonderScope` for the Sector case, and none needs to. **Confirmed, not merely repeated**: this
module's entire scope is the `Empire` path below.

### 1. Where the per-faction sum lives, and when it's computed — the real call

**The question the task poses ("does `TurnEngine` compute a per-faction modifier once, then pass it
into each sector's call?" vs. "does `LoamProduction.For` read `ScopeModifierMilli` directly given a
sector's `OwnerFactionId`?") has a definite answer once the call graph is actually traced**, not
guessed: `TurnEngine.Production` (`TurnEngine.cs:264-276`) already forwards the entire `world` into
`LoamPhases.Production` (`:267`) — `world.Factions` is already reachable there today. So **no
`TurnEngine.cs` change is needed at all.** The real work is entirely inside `LoamPhases.Production`,
which:

1. Computes each faction's `ScopeModifierMilli` **once per faction** (not once per sector) via
   `WonderEmpireEffects.ComputeScopeModifierMilli` (§Design 3).
2. Passes the resolved value into each of that faction's sectors' `LoamProduction.For` calls
   (§Design 2).

This answers the task's second framing directly: `LoamProduction.For` does **not** read
`ScopeModifierMilli` itself given a sector's `OwnerFactionId` — it stays a pure function of
`(WorldSector, int)` with no `WorldState`/`WorldFaction` dependency at all, preserving its existing
signature shape (a sector-scoped calculator) rather than growing a second, wider responsibility. The
faction-level lookup happens exactly once, in the caller that already has both pieces of information
(`LoamPhases.Production` already iterates `world.Sectors` and already has `world.Factions` in scope).

**When: every Production phase, recomputed from scratch — not an incremental update on
construction/destruction.** This matches the established idiom every sibling function in this exact
module already uses: `EffectiveCapacity` (`LoamPhases.cs:66-80`), `LoamUpkeep.For`, and
`TerritoryComponents.For` all recompute fresh from the live slot/structure list every phase — **none
of them cache a running total on `WorldState`.** The rejected alternative (a `WonderBuilt`/`WonderLost`
event bumping a stored running total) would be the first incrementally-maintained aggregate anywhere
in the Loam module, a second bookkeeping discipline alongside the fresh-every-turn one every sibling
function already uses — and it would need its own reconciliation story the moment a Wonder is lost:
`LoamPhases.Pressure`'s own sector-release branch already unconditionally clears every slot's
`StructureId` on a lost sector (`LoamPhases.cs:190-211`, specifically `:202-204`) with **no** awareness
that one of those structures might have been a Wonder feeding a running total elsewhere — an
incremental design would need an explicit decrement wired into that exact branch (a second place the
same fact must be kept correct); recompute-fresh gets this correctly, for free, because a lost
sector's Wonder simply stops appearing in the next Production phase's scan. **Recompute-fresh is the
design**, matching this module's own governing precedent, not merely a default choice.

**Ordering, a real correctness detail — not cosmetic.** `LoamPhases.Production`'s own existing
discipline decrements `ConstructionTurnsRemaining` and reads the **post-decrement** value the same
pass (`LoamPhases.cs:24-27`): a structure finishing construction this turn is already active this
turn. If the per-faction Wonder scan ran against `world.Sectors` (pre-decrement) instead of the
decremented list, an `Empire`-scope Wonder finishing construction this exact turn would be excluded
from this turn's sum while an equivalent `Sector`-scope Wonder finishing the same turn (read directly
off the same post-decrement slot by the existing per-sector loop) would already be active — two
Wonders completing the same turn behaving inconsistently by scope alone, a real, silent bug. `Production`
is restructured into an explicit two-pass shape:

1. Decrement every sector's construction counters first (`DecrementConstruction`, unchanged logic,
   just hoisted out of the per-sector loop into its own pass), producing the full post-decrement
   sector list.
2. Compute each faction's `ScopeModifierMilli` from that post-decrement list.
3. Compute each sector's yield using its owning faction's resolved modifier, applying the existing
   cap/overflow logic exactly as today's single-pass loop already does.

No sector is read from two different snapshots at any point.

**Purity, confirmed.** The whole computation reads only already-loaded `WorldState` fields
(`Sectors[].Slots[].StructureId`) resolved through `StructureCatalog.Get`/`.IsKnown` — a process-wide,
in-memory catalog loaded once at host startup, the exact same resolution pattern every existing line
in `LoamProduction.cs`/`LoamPhases.cs` already uses. No SQL call, no wall clock, anywhere in this
module's own code — satisfying `LoamPhases.cs`'s own header comment verbatim: *"Pure in (state, seed)
like every other phase — no wall clock, no unowned RNG."* This is **not** the live-DB-read-mid-step
violation the task asks to watch for.

### 2. `LoamProduction.For` — the multiply-by-modifier step

```
public static long For(WorldSector sector, int scopeModifierMilli = 1000)
```

Applied once, at the very end, after the Rootbed loop, the flat loop, and `DevelopmentYield.For` are
already summed into `total` (`LoamProduction.cs:22-59`) — never per-loop. An `Empire`-scope Wonder
boosts the sector's **whole** output, not one production term selectively, matching
`ScopeModifierMilli`'s own declared shape (*"a standing world-map buff/debuff on this faction"* —
not a term-scoped one, `WorldState.cs:85-93`):

```
return checked(total * scopeModifierMilli) / 1000;
```

`total` is already `long` — widen-before-multiply is satisfied without a cast. Divide-by-1000 happens
exactly once, last, per `CLAUDE.md` rule 4. The default value `1000` makes the multiply an identity
(`total * 1000 / 1000 == total`), so any isolated unit test that constructs a bare `WorldSector` with
no faction context and calls `LoamProduction.For(sector)` with one argument is unaffected — but every
real call site in this codebase is updated to pass a resolved value explicitly (§Design 4), never
left relying on the default.

### 3. `WonderEmpireEffects.ComputeScopeModifierMilli` — the SUM

New file, same namespace as its siblings (`FusionRpg.Core.World.Loam`), same in-memory
`StructureCatalog` resolution pattern:

```
public static int ComputeScopeModifierMilli(IReadOnlyList<WorldSector> sectors, string factionId)
```

For every sector owned by `factionId`, for every slot whose `StructureId` is known and active
(`ConstructionTurnsRemaining is null or <= 0`, matching every existing "is this structure live"
check in this module) and whose `StructureCatalog.Get(id).WonderScope == WonderScope.Empire`, for
every `WonderEffectDef` in that structure's `WonderEffects` where `Kind ==
WonderEffectKind.LoamGenerationRate` (`Scope` is already guaranteed `== WonderScope.Empire` by
`StructureCatalog.Validate` — this function trusts that invariant rather than re-checking it, the
same way nothing in `LoamProduction.cs` re-validates `YieldMultiplierMilli`'s own non-negativity):

```
long sum = 0;
foreach (matching effect)
    sum = checked(sum + effect.ValueMilli);

return checked((int)(1000 + sum));
```

A faction with no built `Empire`-scope Wonder gets `sum == 0` and the function returns exactly
`1000` — the field's own existing default, so every shipped scenario/golden that authors no Wonder
content is byte-identical, matching this file's own "zero content, zero behaviour change" discipline
(the same proof shape `DevelopmentYield.For`'s own citation already uses: *"ships real... the moment
[the input] is nonzero... moves no golden — proven by running them, not merely argued"*,
`LoamProduction.cs:53-58`).

This is the concrete, evidence-based answer to "does `LoamProduction.For` read `ScopeModifierMilli`
directly:" no — this function is the one and only place that *computes* the value; `LoamProduction.For`
only ever *consumes* an already-resolved `int` (§Design 2), keeping the two concerns (per-faction
aggregation vs. per-sector arithmetic) separate, matching the codebase's own existing separation
between `LoamPhases` (orchestration) and `LoamProduction` (pure per-sector math).

### 4. The three secondary call sites — read the stored field, never rescan

`LoamForecast.ProjectedStock` (`LoamForecast.cs:49-61`), `LoamBalance.PerSector(WorldState,
WorldSector)` (`LoamBalance.cs:12-13`), and `WorldEndpoints.cs:631`'s debug loam view each resolve:

```
var scopeModifierMilli = world.Factions
    .FirstOrDefault(f => f.FactionId == sector.OwnerFactionId)?.ScopeModifierMilli ?? 1000;
```

and pass it into the same two-argument `LoamProduction.For(sector, scopeModifierMilli)` — reading
the value `LoamPhases.Production` already computed and stored on `WorldState` the last time it ran,
**never** re-running the Wonder scan themselves. This is the same "one rule, so the engine and the
player-facing forecast cannot silently disagree" discipline `LoamForecast.cs`'s own header comment
already states as this file's governing principle (`:4-7`, citing `LoamForecast.Weakest`'s identical
shape), extended to the new modifier. A forecast computed between turns reflects Wonders built as of
the last committed turn — the same one-turn staleness every other `LoamForecast` projection already
carries for any structure finishing construction on some future turn, not a new limitation this
module introduces.

**Without this step, three real, already-shipped surfaces would silently under-report** for any
faction with a built `Empire`-scope Wonder — a genuine regression risk this session's own grep found,
not previously named by either the ideal doc or `wonder-structure`'s citations (both name only the
`LoamPhases.cs:30` writer).

### 5. Closing the SQL persistence gap

`RpgStore.World.cs`:

- `EnsureColumn(db, "rpg_world_factions", "scope_modifier_milli", "INTEGER NOT NULL DEFAULT 1000")`,
  added beside the existing `upkeep_handicap_milli` line (`:144`) — identical idiom, identical
  default, identical "an existing saved world reads back at its shipped default" migration story the
  surrounding comment already states for every other post-hoc column in this file (`:134-136`).
- `WriteWorldGraphUnlocked`'s faction INSERT (`:269-278`) gains the column and an
  `f.ScopeModifierMilli` parameter.
- `LoadWorldGraphUnlocked`'s faction SELECT (`:461-473`) gains the column and
  `ScopeModifierMilli = r.GetInt32(5)` in the constructed `WorldFaction`.

`RpgStore.WorldGraphDiff.cs`:

- `DiffFactions`'s `INSERT OR REPLACE` (`:105-116`) gains the column and parameter identically. No
  other change is needed here — `DiffFactions`'s own skip check (`:113`, `was == f`) already compares
  the **full** `WorldFaction` record by structural equality, so once the field round-trips correctly
  it participates in that comparison for free; a turn that leaves every faction's `ScopeModifierMilli`
  unchanged still emits nothing, matching the file's existing "no line for what didn't change"
  discipline.

All three edits stay inside `FusionRpg.Data` — `guard-dal.ps1` scope, satisfying the SQL-only-in-Data
hard rule. No schema change touches any table this module does not already name.

## Numeric types

- `WonderEffectDef.ValueMilli` — `long`, already declared by `wonder-structure` (module 2), unchanged
  here.
- The running sum inside `ComputeScopeModifierMilli` — `long`, `checked` addition throughout (CLAUDE.md
  rule 5: overflow throws, never wraps).
- `WorldFaction.ScopeModifierMilli` — **`int`, pre-existing, declared by buff-debuff-scope T12, not
  owned or widened by this module.** Per CLAUDE.md's own overflow table, an `int` is wrong for any
  per-mille magnitude in principle (it saturates 1000× closer to its ceiling than a whole-unit `int`
  would) — but this field predates this module, is already hashed into `WorldCanonical` at this width,
  and widening it to `long` is a `WorldState.cs`/`WorldCanonical.cs`/SQL-column-type change with its
  own golden-moving blast radius, out of this module's scope. **Mitigation, not a fix**: the narrowing
  conversion from the `long` sum back to this pre-existing `int` field is wrapped in `checked` —
  `checked((int)(1000 + sum))` — so an out-of-range result throws an `OverflowException` immediately,
  never silently wraps, satisfying "overflow throws, never wraps" even though the container itself
  stays undersized long-term. This finding is named here for whoever eventually specs `World`/
  `Multiverse` scope (which reuse the same field per faction, or a sibling of it) to inherit knowingly,
  not rediscover.
- **Why `checked` is warranted here even though a realistic sum is nowhere near either ceiling,
  reasoned explicitly per the task's own instruction:** `WonderRarity.Common` Empire-scope Wonders are
  deliberately uncapped by count (`WonderPolicy.ExistenceCapFor` returns `long.MaxValue` for `Common`,
  per `wonder-structure` §Design 6 — a direct consequence of this repo's "no hard progression
  ceilings" rule). But the number of Wonders a faction can physically hold is still **structurally**
  bounded — one per `WorldSlot`, and a `WorldState`'s sector/slot count is fixed at map-generation
  time, not something that grows via play — so realistically this sum runs over, at most, a few dozen
  to a few hundred slots per world, each contributing a content-authored `ValueMilli` in the low
  hundreds-to-thousands range (never level/power-derived — see below). That keeps a real sum many
  orders of magnitude below either `long.MaxValue` or `int.MaxValue` (`2,147,483,647`). `checked` costs
  nothing at this scale and is the correct discipline regardless of how unlikely an overflow is in
  practice, per CLAUDE.md rule 5 ("overflow throws, never wraps... no silent `unchecked` on a
  magnitude path") — the ceiling is not asserted safe by estimate, it is enforced by the runtime.
- `LoamProduction.For`'s new multiply step (`total * scopeModifierMilli / 1000`) — `total` is already
  `long`; the multiply is wrapped in `checked`; the divide-by-1000 happens exactly once, last (CLAUDE.md
  rule 4).
- **Nothing in this module is level/power-derived.** `ValueMilli` is a content-authored, tunable
  magnitude (`data/seed/structures/**`, module 2's own seed content), never `f(Θ)` — confirmed by
  re-reading `wonder-structure`'s own "Numeric types" section, which states the same for `ValueMilli`
  generally, and by this module introducing no new formula that reads a level or `Θ` anywhere. One
  power ladder, satisfied by construction, not by exemption.

## Tunables

| Number | Home | Notes |
|---|---|---|
| Empire-scope combination rule (SUM) | Not a tunable — a structural constant (decisions.md, quoted above) | **Re-confirmed fresh against the map's own Tunables table row 4**, which already states this correctly: *"a structural constant, commented as such — the combination function, not a tunable number."* This module ships no new `data/tuning/*.json` file or row. |
| `WonderEffectDef.ValueMilli` per row | `data/seed/structures/**` (module 2's own seed content, unchanged by this module) | This module reads the field; it does not author it or change its home. |

No tunable is introduced by this module. This is a deliberate, verified finding, not an omission —
every number this module's own code touches is either a structural constant (SUM) or content already
owned by module 2.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~WonderEmpireEffects"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~LoamPhasesTests"     # Production byte-identity when no Wonder exists
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~LoamProductionTests" # the new multiply step, in isolation
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~WorldGraph"          # ScopeModifierMilli round-trips save/load and turn-commit diff
.\scripts\guard-dal.ps1        # every new SQL string stays inside FusionRpg.Data
```

## Structure

```
src/FusionRpg.Core/World/Loam/LoamProduction.cs        MODIFIED — For(WorldSector, int scopeModifierMilli = 1000);
                                                        final multiply-by-modifier step (§Design 2)
src/FusionRpg.Core/World/Loam/LoamPhases.cs            MODIFIED — Production restructured into
                                                        decrement-all -> compute-per-faction-sum ->
                                                        compute-yield-per-sector (§Design 1); Factions
                                                        list rewritten with each faction's freshly
                                                        computed ScopeModifierMilli
src/FusionRpg.Core/World/Loam/WonderEffectEmpire.cs    NEW — WonderEmpireEffects.ComputeScopeModifierMilli
                                                        (§Design 3)
src/FusionRpg.Core/World/Loam/LoamForecast.cs          MODIFIED — ProjectedStock resolves and passes the
                                                        owning faction's stored ScopeModifierMilli (§Design 4)
src/FusionRpg.Core/World/Loam/LoamBalance.cs           MODIFIED — PerSector(WorldState, WorldSector)
                                                        truth-side overload does the same (§Design 4)
src/FusionRpg.Server/WorldEndpoints.cs                 MODIFIED — the debug/loam-state view (:631) does
                                                        the same (§Design 4)
src/FusionRpg.Data/Sqlite/RpgStore.World.cs            MODIFIED — EnsureColumn "scope_modifier_milli";
                                                        WriteWorldGraphUnlocked INSERT + LoadWorldGraphUnlocked
                                                        SELECT both gain the column (§Design 5)
src/FusionRpg.Data/Sqlite/RpgStore.WorldGraphDiff.cs   MODIFIED — DiffFactions' INSERT OR REPLACE gains
                                                        the column (§Design 5)
tests/FusionRpg.Core.Tests/World/Loam/WonderEffectEmpireTests.cs   NEW
tests/FusionRpg.Data.Tests/World/WorldGraphScopeModifierTests.cs   NEW — the actual persistence-gap
                                                        regression test (save/load AND turn-commit diff)
UNTOUCHED: TurnEngine.cs (no signature change needed anywhere, §Design 1); StructureCatalog.cs,
           WonderCatalog.cs, StructureCatalog.Validate (module 2's own enforcement, consumed not
           extended); WorldState.cs record shapes (no new field on WorldFaction/WorldSector — this
           module writes into the existing ScopeModifierMilli field only); scoped-inventory-hierarchy's
           own tables; every one of the 25 pre-Wonder shipped structure rows.
```

## Code style

```csharp
// LoamPhases.Production, restructured: decrement first, so an Empire-scope Wonder finishing
// construction this exact pass is already counted in this same pass's faction sum — preserving the
// "activates the same pass it finishes" rule Sector-scope structures already have (LoamPhases.cs:24-27).
var decrementedSectors = world.Sectors.Select(DecrementConstruction).ToList();

var scopeModifierByFaction = world.Factions.ToDictionary(
    f => f.FactionId,
    f => WonderEmpireEffects.ComputeScopeModifierMilli(decrementedSectors, f.FactionId),
    StringComparer.Ordinal);
```

## Testing strategy

- **Zero-Wonder byte-identity.** Every existing `LoamPhases`/`LoamForecast`/`LoamBalance`/world-graph
  golden that authors no `Empire`-scope Wonder produces identical output before and after this
  module — `ComputeScopeModifierMilli` returns exactly `1000` and the multiply step is an identity.
- **SUM, not max or replace.** Two built `Empire`-scope Wonders on one faction, `valueMilli: 200` and
  `valueMilli: 300`, produce `ScopeModifierMilli == 1500`, never `1300` (max) or `1000`/`1300` alone
  (replace).
- **Same-pass activation.** An `Empire`-scope Wonder whose `ConstructionTurnsRemaining` reaches `0`
  this exact Production phase already contributes to this same phase's sum — not the phase after.
- **Lost sector drops out with no residue.** A faction that loses (via `Pressure`'s own release path)
  the sector holding its only `Empire`-scope Wonder sees `ScopeModifierMilli` return to `1000` the
  very next Production phase, with no leftover contribution and no explicit "remove" step anywhere in
  this module's own code — proving the recompute-fresh design, not merely asserting it.
- **Persistence round-trip, the actual regression this module exists to close.** A world saved with a
  faction at `ScopeModifierMilli == 1500`, then reloaded via `LoadWorldGraphUnlocked`, reads back
  `1500`, not the record default `1000`. The same value survives a turn-commit `DiffFactions` pass
  unchanged when nothing about that faction's Wonders changed that turn.
- **`checked` overflow proof.** A synthetic scenario with a sum large enough to overflow `int` throws
  `OverflowException` at the narrowing cast, never wraps to a negative or wrapped-around modifier.
- **Three secondary call sites agree with the engine.** `LoamForecast.ProjectedStock`,
  `LoamBalance.PerSector`, and the debug endpoint all report the same yield for a sector as
  `LoamPhases.Production` would compute for it this turn, given the same `WorldState`.

## Boundaries

- **Always:** compute `ScopeModifierMilli` fresh every Production phase from the live, post-decrement
  sector list; apply the resolved modifier as the last step of `LoamProduction.For`, after every
  existing sum; `checked` arithmetic on every step of the sum and the final narrowing cast; every SQL
  change stays inside `FusionRpg.Data`.
- **Ask first:** widening `WorldFaction.ScopeModifierMilli` from `int` to `long` (a golden-moving,
  cross-file structural change named but deliberately deferred here, §Numeric types); reusing this
  module's own per-faction Wonder scan as the existence-cap query `wonder-build-flow` (module 4) will
  need for `Unique`-rarity enforcement — plausible reuse, not designed or locked here, since module 4
  needs every scope/rarity combination, not only `Empire`/`LoamGenerationRate`.
- **Never:** an incremental, event-driven running total for `ScopeModifierMilli` (rejected, §Design
  1); a second, competing writer of `ScopeModifierMilli` inside this module beyond the one Production-
  phase recompute; a live SQL read from inside `LoamPhases.Production` or any other turn-phase
  function; a private per-sector or per-faction fold of anything ActorHub-shaped (this module never
  touches actor combat state at all — see Design-gate checklist).

## Success criteria

1. `LoamProduction.For` gains exactly one new parameter, defaulted so no existing 1-argument call
   site not covered by this module's own edits changes behaviour. 2. `ScopeModifierMilli` is
   recomputed fresh every Production phase as `1000 + Σ(ValueMilli)` over that faction's built
   `Empire`-scope `LoamGenerationRate` effects, `checked` throughout. 3. `rpg_world_factions` gains a
   `scope_modifier_milli` column via `EnsureColumn`, wired into both the seed/graph INSERT/SELECT pair
   and the turn-commit diff's `INSERT OR REPLACE` — a save/load round-trip and a turn-commit diff both
   preserve a nonzero-delta value. 4. All three secondary call sites of `LoamProduction.For`
   (`LoamForecast.ProjectedStock`, `LoamBalance.PerSector`, `WorldEndpoints.cs:631`) resolve and pass
   the stored modifier, never re-scanning. 5. Every existing Loam/world-graph golden that authors no
   `Empire`-scope Wonder is byte-identical. 6. `guard-dal.ps1` green. 7. No `TurnEngine.cs` signature
   change anywhere.

## Interface exposed to dependents

This module has **no direct module dependent inside this map** — `wonder-build-flow` (module 4)
depends on `relic-item-kind` and `wonder-structure` only, per the map's own dependency table, not on
this module. This module's consumer is the live turn engine and its own player-facing read surfaces
(`LoamForecast`, the debug endpoint), not a future spec. Named for completeness, matching house style,
rather than left as an empty section:

| Member | Consumer |
|---|---|
| `WorldFaction.ScopeModifierMilli` (now real, persisted, recomputed every turn) | Any future consumer of the buff-debuff-scope T12 storage this module is the first real reader/writer of — e.g. a future non-Wonder empire-wide effect. **Not designed here**: if a second, non-Wonder writer of this same field is ever built, it must design its own combination rule against this module's own SUM rather than silently overwriting it (named as a real, inherited concern in §Numeric types and Boundaries, not solved) |
| `WonderEmpireEffects.ComputeScopeModifierMilli`'s internal "built `Empire`-scope structures owned by faction X" scan shape | Plausible, non-binding reuse for `wonder-build-flow`'s own `Unique`-rarity existence-cap enforcement (module 4) — that module needs every scope/rarity combination, not only `Empire`/`LoamGenerationRate`, so this is not exposed as a locked API, only named so module 4 does not reinvent the traversal pattern from nothing |

## What this module does not touch

- **`relic-item-kind`'s own scope** (module 1) — minting, drop tables, the `unique` `KindSpec`
  question. Nothing here mints, spends, or references a relic item.
- **`wonder-build-flow`'s construction verb** (module 4) — spending a relic plus
  `RubbleStock`/`IronworkStock` to build a Wonder, and `WonderPolicy.ExistenceCapFor`'s own
  enforcement. This module assumes a Wonder is already built; it does not build one.
- **`scoped-inventory-hierarchy`'s own tables** — never read or written here.
- **`World`/`Multiverse` scope** — still named-but-refused by `StructureCatalog.Validate` (module 2's
  own enforcement, untouched). This module's `ComputeScopeModifierMilli` only ever matches
  `WonderScope.Empire` by construction; it has no branch for either reserved scope and needs none,
  since `Validate` already makes them unreachable.
- **`DefensePower`/`AuraGrant`/`EmpireBuff`** — still named-but-refused by `StructureCatalog.Validate`.
  This module's scan filters on `WonderEffectKind.LoamGenerationRate` specifically and ignores every
  other kind by construction.
- **`StructureCatalog.cs`, `WonderCatalog.cs`, `StructureCatalog.Validate`** — module 2's own files,
  consumed as a fixed contract, not extended or re-validated here.
- **Any `ActorHub`/actor-combat subsystem** — this module is world-map economy state (`FusionRpg.Core.World`)
  exclusively; no Status, ActorHub, or Combat code path is touched, matching `wonder-structure`'s own
  Design-gate finding for the identical reason (`DefensePower`/`AuraGrant` are the only Wonder effect
  kinds that would ever touch ActorHub, and both are refused today).
- **The 25 pre-Wonder shipped structure rows** — every one of them authors no `WonderScope`, so
  `ComputeScopeModifierMilli` never matches them and their yield is multiplied by the identity `1000`.

## Design-gate checklist

```
[x] Subsystems: world-map structure/economy state (Core), turn-engine orchestration (Core), SQL
    persistence (Data) — no Status/ActorHub/Combat subsystem touched.
[x] Read this session: loam-relics-and-wonders-ideal.md (full, both pages, re-read this session);
    loam-relics-and-wonders-map.md (full); decisions.md "Loam relics and wonders SSOT (2026-09-13)"
    (full row, quoted verbatim above) and "World turn phase order" row (full, quoted); DESIGN-GATE.md
    §1 rows: Economy/currencies/yields, World map, Data/SQL/schema, Any tunable number, Any cap or
    ceiling, Any numeric magnitude, Stats (to confirm ActorHub is out of scope);
    spec-wonder-structure.md (full, the direct dependency); scoped-inventory-hierarchy/spec-legion-cargo.md
    (full, house style template).
[x] Code cited by file:line, opened fresh this session (not trusted from any sibling doc's own
    citation without re-opening): TurnEngine.cs (:264-276); LoamPhases.cs (full file); LoamProduction.cs
    (full file); LoamForecast.cs (full file); LoamBalance.cs (full file); WorldState.cs (:60-146,
    WorldFaction/WorldSlot/WorldSector); WorldCanonical.cs (:85-104); RpgStore.World.cs (:37-44,
    :133-152, :260-284, :455-480); RpgStore.WorldGraphDiff.cs (:97-117); WorldEndpoints.cs (:615-644);
    FrontierRulesPolicy.cs (:188, grep-confirmed as the belief-side overload's only caller); grep of
    every `LoamProduction.For` call site in `src/` (10 hits, 4 real call sites, 6 comments/citations).
[x] Drift reported: the ideal doc and `wonder-structure` both name only `LoamPhases.cs:30` as
    `LoamProduction.For`'s call site when discussing this exact gap; grepping the real call graph this
    session found three more real call sites (`LoamForecast.cs:55`, `LoamBalance.cs:13`,
    `WorldEndpoints.cs:631`) that would silently under-report without the same fix — reported as a
    genuinely new finding, not previously named, and designed for (§Design 4), not silently left.
[x] No §2 invariant contradicted: SQL confined to `FusionRpg.Data` (guard-dal.ps1 covers the three
    modified files); no cap on a magnitude presented as a progression ceiling (SUM is unbounded by
    construction, matching `Common` rarity's own "no existence cap" design; the `int` narrowing is an
    inherited-debt overflow guard, not a progression ceiling); no `f(Θ)` introduced anywhere; no second
    ActorHub composer (this module never touches actor combat state); no second ownership root (this
    module owns no item/relic state); the balance surface introduces no new magic number (§Tunables:
    zero new tunables, SUM is a structural constant per decisions.md).
[x] Numeric overflow reasoned explicitly, not asserted safe by estimate: `checked` arithmetic on every
    addition and the final narrowing cast; the structural (not progression) bound on Wonder count
    argued from slot-count finiteness, not from an assumed small number; the pre-existing `int` typing
    on `ScopeModifierMilli` named as inherited debt with a stated future fix, not silently worked
    around.
[ ] The exact reuse (or non-reuse) of this module's own Wonder-scan shape for `wonder-build-flow`'s
    `Unique`-rarity existence-cap check (module 4) was not designed this session — correctly deferred,
    named only as a plausible non-binding option (§Interface exposed to dependents).
[ ] A second, non-Wonder future writer of `WorldFaction.ScopeModifierMilli` and its combination rule
    against this module's own SUM was not designed this session — correctly deferred, named as a real,
    inherited concern (§Interface exposed to dependents, §Boundaries), not solved or dismissed.
```
