# Spec: `siege-loot`

**Module id:** `siege-loot` · **Program:** [base-defense](../base-defense-map.md) · **Filed via
[drop-tables-map.md](../drop-tables-map.md)** (cross-program initiative) · **Build order:** 4 of 4 in
the drop-tables map
**Depends on:** `rate-floor` (item, soft) · `rate-authoring` (item, soft) · the shipped
`LootPipeline`/`Instantiator`/`DropTableModel`
(item, consumed as-is, no changes needed there) · party-dungeon's `DelveLoot`/`spec-dungeon-loot.md` as
the template for hosting `LootPipeline` in a new gameplay mode for the first time
**Source:** `docs/architecture/drop-tables-ideal.md` §4 R1(a), decided D1 (2026-09-07)

## Objective

Base-defense currently grants **nothing** — no souls, no items — through any pipeline when a siege
resolves. Confirmed by direct search: zero references to `LootSourceRow`, `DropTableRow`, or
`Instantiator` anywhere under `World/Turn` or `Battle/Board`, and `DistrictAssaultResolver.cs` itself
contains no reward-granting code of any kind — it resolves combat mechanics only
(`BattleOutcome`/`SiegeOutcomeKind`), never a payout. This module is base-defense's **first** production
host of the item loot pipeline — the same "first production host" framing `spec-dungeon-loot.md` used
for party-dungeon, followed here as the established template rather than re-derived.

## ⚠ Two things this session confirmed exist, and one it did not confirm

**Confirmed:** `DistrictAssaultResolver.Resolve` returns a `BattleOutcome` carrying `SiegeOutcomeKind`
— `CoreTaken` (attacker/besieger won) or `AssaultBroken` (defender won) — the real win/loss signal a
loot grant should key on, the same way party-dungeon's own victory-soul term is bound to
`delve-attrition`'s own `won` predicate rather than firing unconditionally
(`spec-dungeon-loot.md` §2: *"the term pays only when the delve is won... the predicate loyalty already
reads, so faucet and loyalty cannot disagree"*).

**Confirmed, and a real caution for implementation:** `git status` at spec-writing time showed
`src/FusionRpg.Core/World/Turn/DistrictAssaultResolver.cs` with BOTH staged and unstaged changes
(`MM`), and `DistrictLayout.cs`/`BattleSeam.cs`/`DistrictAssaultPhase.cs` also modified — another
session's active, in-progress work on exactly these files, observed twice in one day this session.
**Do not begin implementing against these files without re-checking `git status` fresh** — the
concurrent work may have already changed the exact shape this spec cites, and this program has a
established, repeated pattern of that concurrent work being real, in-progress feature work, not noise.

**Not confirmed — the real call site, and it is now MORE uncertain than the first draft of this spec
said, not less.** `DistrictAssaultPhase.cs`, `TurnEngine.cs`, and `SiegeEngagement.cs` are all
plausible, but a sibling module's own review (`sector-loot-wiring`) traced the analogous world-map
question and found the real trigger for a *comparable* event (a sector's ownership actually flipping)
is `World/Movement/ClaimResolver.cs:79` — gated on an explicit player `Claim` command whose own
precondition requires "every slot's guard already `Cleared`" (`ClaimResolver.cs:66-71`). **"Slot" there
plausibly refers to a district's own guard/defense state** — meaning a won `DistrictAssaultResolver`
result may only *contribute to* a slot becoming `Cleared`, with the actual sector-level consequence
(and, by extension, where a reward naturally belongs) deferred to a *later*, separate `ClaimResolver`
pass, exactly like world-map's own decoupling. **This relationship was not confirmed this session** —
it is a real, named, uninvestigated risk, not a settled fact. Task 1 of this module's build must
resolve: (a) does a district assault win grant loot immediately at resolution, independent of any
later sector claim, or (b) does it only make loot *possible*, with the real grant sitting at whatever
`ClaimResolver`-adjacent code consumes a district's `Cleared` state — and only then wire against the
answer, not the plausible-looking guess.

**The same design-gate conflict `sector-loot-wiring` found also applies here, and is the reason (b)
above should be treated as the leading hypothesis, not (a).** `docs/DESIGN-GATE.md`'s "Battle / turns"
row: *"Battle consumes FA10 only; it never grants and never calls `OnEvent`."* `DistrictAssaultPhase.cs`
and `SiegeEngagement.cs` sit in the same Battle-adjacent layer `sector-loot-wiring`'s own corrected
call site (`ClaimResolver.cs`, under `World/Movement/`, outside Battle) was chosen specifically to
avoid. If district clears do feed a `ClaimResolver`-owned slot state, the grant almost certainly
belongs there too, for the same architectural reason — read `decisions.md` and the DESIGN-GATE "World
map"/"Battle / turns" rows in full before wiring, not after.

## Design

### The pattern to follow — `DelveLoot.RollRoom`/`AtExtraction`, adapted

```text
[real call site — TBD, see above; leading hypothesis is ClaimResolver-adjacent, NOT
 DistrictAssaultResolver/DistrictAssaultPhase directly] a siege resolves favorably
    -> source = new LootSourceRow("siege-assault", $"{districtId}", tableId, ContentLevel: theta_district)
    -> seed   = SeededRng.DeriveStream(turnSeed, $"siege:loot:{districtId}").NextULong()
    -> request = new LootRequest(playerId, "siege-assault", source.SourceId, seed, ThetaActor: theta_commander, ...)
    -> LootPipeline.Resolve(request, view, drops, pity, out manifest)
    -> Instantiator.TryInstantiate(...) at theta_district, exactly as every other host does it —
       no second roll, no private curve (matching D18's "one arrow" framing and party-dungeon's own
       "the roll" boundary rule verbatim)
```

**Two reads, same split D18 and `spec-dungeon-loot.md` both already establish and this module must not
re-litigate:** *how many* items drop reads the commander's own `Θ_actor` (linear, uncapped, D26);
*how strong* they are reads the district's own content level (`Θ_content`, via `contentScale`) — never
the player's level. This module's own job is deciding **what `Θ_content` a district siege resolves to**
(a new content-level source, analogous to `WorldSectorLootSource.MapLevel` or `RoomTheta` in party-
dungeon) — that mapping does not exist yet anywhere in base-defense and is this module's own new,
small function, not a private curve (no `f(level)` — read whatever district-strength signal
`DistrictLayout`/`SiegeOutcomeKind` already expose, the same "read, don't invent" discipline
`WorldSectorLootSource.cs` demonstrates for `mapLevel`).

### A losing defense grants nothing; a losing siege (attacker) grants nothing either — named explicitly

Matching party-dungeon's own S1-1 anchor (*"a wipe forfeits [rewards] with the haul"*): `AssaultBroken`
(attacker lost) triggers no loot grant at all — a siege is not a participation-trophy activity. This
module explicitly does not invent a consolation drop for a broken assault; if one is wanted later, it
is a new, separate, ask-first decision (see Boundaries), not a default this module falls into.

### First-time source kinds

`LootCorrelation.Derive` and `DropTableValidator.KnownSourceKinds` (the same two gates
`spec-dungeon-loot.md` §3 named for its own three new kinds) gain **one** new source kind:
`siege-assault`, correlation `loot:siege:{districtId}:{turnId}` — turn-qualified so a district besieged
more than once (won, lost, retaken, won again) never replays an old manifest for a new siege.

## Data shape

| Table | Change |
|---|---|
| `loot_source` | new `source_kind = "siege-assault"` rows, one per resolved, won siege |
| `data/seed/loot/drop.siege-assault.{districtTier}.json` | **new** — content authored per district tier/type, not one flat table (matching this whole initiative's own point) |
| `LootCorrelation.Derive` | +1 arm for `siege-assault` |
| `DropTableValidator.KnownSourceKinds` | +1 kind |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Loot|FullyQualifiedName~Items.Drops"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~World.Turn|FullyQualifiedName~SiegeConstruction"   # siege goldens, must stay byte-identical for a run that changes nothing else
.\scripts\guard-power.ps1 ; .\scripts\guard-dal.ps1
```

## Project structure

```text
src/FusionRpg.Core/World/Turn/SiegeLoot.cs           new — the RollSiege function, mirrors DelveLoot.RollRoom's shape
src/FusionRpg.Core/Items/Drops/LootCorrelation.cs    EDIT — +1 arm, item program's file
src/FusionRpg.Core/Items/Drops/DropTableValidator.cs EDIT — +1 known source kind, item program's file
data/seed/loot/drop.siege-assault.*.json             new
tests/FusionRpg.Core.Tests/World/Turn/SiegeLootTests.cs   new
```

## Code style

Pure over inputs, tuning injected, no I/O (`tunables-ssot.md` §7.2) — matches `DelveLoot.RollRoom`'s
own signature shape exactly: takes the resolved `BattleOutcome`, a `LootContentView`, drop-volume
tuning, and a mint delegate; returns a rejection or a result. No parameter named `level`/`lvl`/`index`
on a numeric method (the same `guard-power.ps1` G2 rule party-dungeon's own code style cites).

## Testing strategy

| Test | Asserts |
|---|---|
| `a_won_siege_grants_loot_through_the_real_pipeline` | `SiegeOutcomeKind.CoreTaken` → a real `LootManifest`, minted via `Instantiator.TryInstantiate`, not a bespoke roll |
| `a_broken_assault_grants_nothing` | `AssaultBroken` → no `LootSourceRow` constructed, no stream advanced, no manifest |
| `drop_count_reads_the_commanders_theta_actor_never_the_district` | doubling `Θ_actor` with district content level fixed changes count only, matching D18's own test shape |
| `item_level_reads_district_content_never_the_player` | the district's own strength signal, never `Θ_actor` |
| `a_retaken_district_does_not_replay_the_first_sieges_manifest` | the turn-qualified correlation id — two sieges of the same district are two distinct loot events |
| `every_pre_existing_siege_golden_stays_byte_identical` | wiring a new grant must not move any existing base-defense/battle golden for a run that never triggers it |
| `each_district_tier_or_type_resolves_a_distinct_table` | matching `sector-loot-wiring`'s own per-type split — no single flat `drop.siege-assault` table shared by every district |

## Boundaries

**Always:** read `Θ_actor` through the same `IPowerIndexProvider` seam every other loot host uses; key
the correlation id on `(districtId, turnId)`; grant nothing on a broken assault; follow
`DelveLoot.RollRoom`'s own established shape rather than inventing a fourth one.

**Ask first:** whether a broken (lost) assault should grant a smaller consolation reward — this module
ships with **no** consolation grant by default, matching party-dungeon's own wipe-forfeits-everything
precedent; adding one later is a real, separate design decision. Also ask-first: the exact district-
content-level mapping function, since none exists in base-defense today and it is this module's own new
surface (the same class of decision `WorldSectorLootSource.MapLevel` was for world-map).

**Never:** invent a private `f(level)` for district strength — read whatever `DistrictLayout` already
tracks. Never grant loot on `AssaultBroken`. Never let item level read the player's own `Θ_actor`.
Never touch `DistrictAssaultResolver.cs`'s own combat-resolution logic — this module is a caller of its
*result*, never a change to how a siege battle itself resolves.

## Success criteria

- [ ] The real call site is confirmed fresh against `git status` (not another session's in-flight work)
      and cited by `file:line` before implementation begins.
- [ ] A won siege grants a real, `Instantiator`-minted loot manifest; a broken assault grants nothing.
- [ ] Drop count reads `Θ_actor`; item level reads the district's own content level — the same
      two-read property every other loot host already proves.
- [ ] Every pre-existing base-defense/battle golden stays byte-identical for a run that does not
      trigger the new grant.
- [ ] District tiers/types resolve to distinct tables, not one shared flat table. **Distinct ids alone
      do not satisfy this** — the same finding `sector-loot-wiring` names applies here: at least one
      substantive axis (pool, weights, `affix_channel`) must differ between at least two real district
      tiers, verified by a content-diffing test, not an id-uniqueness check alone.
