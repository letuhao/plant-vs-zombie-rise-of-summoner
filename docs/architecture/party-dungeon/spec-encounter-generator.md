# Spec: encounter-generator

Status: **APPROVED by the owner 2026-09-05 (wave 2) — written against shipped code; unbuilt.** Every
`file:line` below was opened this session; drift against the brief that named it is reported in the
design-gate checklist. Every number is a starting shape chosen so the system runs, never a balance
decision (`ssot-power-scale.md` §5.3's caveat applies to all of them).

Module id `encounter-generator` in the [party-dungeon map](../party-dungeon-map.md) (row 6, first of
wave 2). Depends on `difficulty-ladder` (`RoomTheta.Compose`, `RungDef`), `dungeon-seed-contract` (the
encounter anchor, §1.6), `dungeon-registries` (`encounter.v1.json`, `raid.modes.*`, `difficulty.rungs[]`)
and `delve-graph-roll` (`Facts`: `kind`, `archetypeId → encounterRef`). **External:** demon-seed module 7
`threat-audit` (gate: before this module ships — map §External dependencies row 1). **Consumed later,
never gating:** base-defense `siege-board` + `board-render` (A10 — v1 ships 1-D rank, R1) and
`siege-waves` roster-add / `combatant-kind` (summoner and fixture bosses). Ideal: §4.4, §4.8, §11.4, §8 box
decision 3, §11.10 R1/R9. Review: [audit-2026-09-05.md](audit-2026-09-05.md) §1(d), S1-7, S2-5, S2-9, §9.
Format precedent: [standalone/spec-expeditions.md](../standalone/spec-expeditions.md).

## Objective

Turn an encounter anchor plus the room's `Θ` into the enemy half of a `BattleSetup` — `BattleActorSetup`s
in rank order — exactly as `WaveCatalog.Enemies` does for the four authored waves (`WaveCatalog.cs:125-153`),
with one difference that is the whole point: every enemy's level is **`θ_enemy = Θ_room + thetaOffset(species)`**,
the sum nothing computes today (`WaveCatalog.cs:140` sets `Level = theta` from a content constant;
`SpeciesExpander.cs:66-67` adds the offset to a *species base*, never to a room). The player's half is not
this module's: the delve host builds it as `WebMatchService.BuildSquad` does (`WebMatchService.cs:390-410`),
one setup per standing demon across every party at the rendezvous. Success looks like: a `fight` room on a
`rich` domain at `hard` resolves raiders at `θ = 83` from a `pack` anchor, byte-identical on replay; a boss
room at 4 parties fields the domain's tyrant at `θ = 127` with a retinue, a wider `W` and a shield pool, and
lands inside `boss.fightLengthTargetRounds` over a 32-seed sweep; and every battle golden and expedition
hash is untouched, because this module emits setups only for delve content.

## Locked anchors

- **Decision 3 (ideal §8 box, owner 2026-09-05):** *"Raid boss scaling: retinue and `W`, plus a shield
  pool whose capacity per extra party is `P(Θ_room)` through the built shield layer — no new curve, one
  more dial to tune (`raid.modes.*.bossShieldPerPartyMilli`, a share of `P(Θ)`)."* The refused option was
  *"a boss HP multiplier per party"* — *"a new power-shaped scale"*.
- **Review §1(d) target statement, verbatim — this spec's calibration contract:** *"The boss fight at N
  parties lasts within a tunable band of the solo fight's length. `raid.modes.*.bossShieldPerPartyMilli`
  is a share of `P(Θ_room)` per extra party, derived from the `bossW` ratio and the retinue count, never
  flat; starting shape 300‰ (RoR2's 0.3), with D10's 170‰ (flat length under `W` doubling) as the lower
  reference. The read is registered in the §10 mirror with a PS-2 'read once' note so the next sweep does
  not find a private scale."* Monster Hunter's 100/163/200/234 % is **Wilds** only (R5) — a label, not a reference.
- **R1 (§11.10):** *"the Delve ships 1-D rank now and adopts the board when it lands; rank collapses into
  column."* **R9:** *"A `siege-ai`-class policy per un-steered party"* — the intent source is never this module's.
- **`ssot-power-scale.md` §10.3:** *"Both, on separate owners. Enemy *level* is `Θ_content` and belongs
  here; enemy *count* is encounter design and does not."* This module owns count, retinue, `W`, kit tiers
  and the shield share; it owns no curve.
- **Ideal §11.4:** *"A slot is a filter tuple over anchor ordinals, never a new noun"*; *"Boss is a role,
  not a species field"* — `DemonDeployMode.HypnoAlly` (`DemonRarity.cs:41-45`) is a lawn expression, never
  read here; *"Elite = one slot with an affix roll … through `Instantiator.TryInstantiate` — the item roll,
  unchanged."* **S2-13:** *"An encounter is a filter over the species corpus, never a list of species."*
- **Determinism (`BattleModels.cs:261-266`):** *"the pattern is part of the SETUP, resolved before the battle
  runs — never rolled during resolution."* Everything here is drawn at setup; nothing rolls inside `Resolve`.

## Design

### 1. Inputs

`Encounter.Build(anchor, roomTheta, climate, raid, rung, seed, corpus, tuning)`, all read models owned
elsewhere: the **encounter anchor** (seed contract §1.6 — `formation`, `slots[] {posture, reach,
targetPreference, countBand}`, `rankOrder`, `elementSpread`, `threatWindow`, `synergyHint`, `tempo`,
`affixRoll`, `boss{build, phasing, phaseTrigger, signatureAction, retinue{slotRef, countBand}}`);
**`RoomTheta`** (`spec-difficulty-ladder.md:267, :354` — `Θ_room` composed, boss rooms already carrying
`depth.bossBandDelta`); the room's **climate** (`ElementTypeId` or `none` — the boss room is climate-neutral
and passes `none`); the **`RaidMode`** row (`raid.modes.*.{parties,squadSlots,bossW}`); the **`RungDef`**
(`enemyCountDelta.{fight,elite}`, `bossRetinuePerPartyDelta`, `bossWDelta`, `eliteKitTier`, `bossKitTier`,
`doubleBoss`, `eliteSecondActionRow`); the sealed **`ulong` seed** plus `(row, col)`; the **species corpus**
as `ConcreteSpecies` rows (`ConcreteSpecies.cs:13-68` — `Theta`, `RangeCells :34`, `ElementPrimary`,
`TraitPool`, `AttackIntervalMs`) joined to their anchor ordinals (`aptitudePrimary`, `reach`,
`targetPreference`, `threatBand`, `attackTempo` — the fields `aerial-flora.json` carries); the domain's
`bossSpeciesRef`; `EncounterTuning`; `DemonThreatTuning` (`DemonThreatTuning.cs:12-13`).

Output `EncounterHalf { Enemies, int W, Warnings, Cell }` — `Cell = (postureMultiset, elementSpread,
formation)` for §8's metric. `W` is returned, never serialized (a `W` on `BattleSetup` moves all four
expedition hashes, `WaveCatalog.cs:27-30`); the host applies it as `profile with { W = w }` (`:84`).

### 2. Slot filling — filter, count, draw, and the stream per step

Every stream is `SeededRng.DeriveStream(seed, name)` (`SeededRng.cs:26`), one per step so an extra draw in
one never shifts another (`ExpeditionResolver.cs:92, :106`). Weighted picks go through
`WeightedChoice.Pick(options, rollSeed, streamName)` (`Actions/Seeding/WeightedChoice.cs:25`), `rollSeed` the
stream's first `NextULong()`; an empty option list throws (`:36-37`). Root `dungeon:encounter:{r}:{c}` —
the name `delve-graph-roll` reserved and never draws.

1. **Spread set** — `…:spread`. `mono` → `{climate}`; `dual` → `{climate}` + one other element drawn
   uniformly; `rainbow` → all six; under `climate = none` all six with no off-climate weight. Element
   weight per candidate: 1000 on climate, `spread.*.offClimateMilli` off climate inside the set, 0 outside.
2. **Filter** — no draw. Candidates = rows where **`threatBand` is present** and its rung ∈
   `[threatWindow.floorRung, ceilRung]` (`demon-threat.v1.json` rungs 1–10); **posture** = `roster.json`'s
   `posture` for `aptitudePrimary` (`roster.json:11-22`, case-insensitive — anchors write `Bastion`, the
   roster `bastion`; the file's own `posture` is the `_derived` echo and is never trusted); `reach`,
   `targetPreference`, `tempo` (→ `attackTempo`) match unless the slot says `none`; element in the set.
   `HypnoAlly` species are candidates like any other.
3. **Count** — `…:slot:{i}:count`. `n = NextInt(min, max)` over `slot.countBand.*.{min,max}`, then
   `+ enemyCountDelta.fight` (`fight`/`wild` rooms) or `.elite` (`elite` rooms); `n < 1` refuses (§8).
4. **Draw** — `…:slot:{i}:pick:{k}`, weighted by step 1. **XCOM's same-shape guard without a retry loop:**
   once a species holds `⌈n · pack.sameSpeciesMaxMilli / 1000⌉` seats it leaves the option list —
   draw-without-replacement past the cap, never redraw-until-different.
5. **Synergy** — `…:synergy`. If `synergyHint` names a trait pair and two drawn species carry them
   (`TraitPool`), they are emitted adjacent in rank (`loyal` guards index ±1, `BattleEngine.cs:598-609`);
   otherwise nothing — *"the roll may miss it"* (§1.6).
6. **Elite** — `…:elite` picks which actor of the marked slot carries the affix roll (§6); **boss** —
   `…:boss:retinue`, `…:boss:phase:{p}`, `…:boss:affix` (§5).

### 3. Stats — the Θ sum, and the first place it exists

```text
θ_enemy = RoomTheta.Theta + DemonThreatTuning.OffsetFor(anchor.threatBand)     // int + int, checked
MaxHp   = BattleRuleset.BaseHp(θ_enemy)      Atk = BaseAtk(θ_enemy)      Defense = BaseDefense(θ_enemy)
```

`BaseHp/Atk/Defense` (`BattleModels.cs:218-221`) are the only three reads that touch the ladder; the contest
baselines (`:239-242`) read `Level` inside `BattleStatComposer`, so `Level = θ_enemy` is also what makes a
`hard`-rung raider hit the party at the ladder spec's §7 rates. `OffsetFor` is called **only after** step 2
has refused a null `threatBand`; its `inferredDefaultRung` fallback (`DemonThreatTuning.cs:27-29`, rung 4
`raider` +13) is a species-generator convenience this module never reaches. Worked rows (ladder spec §1):
`Θ_room 70` + raider → `θ 83`, `MaxHp 3,616`; boss `Θ_room 100` + tyrant +27 → `θ 127`, `MaxHp 6,608`. Each
setup carries `SpeciesId`, `TypeId`, `ElementPrimary/Secondary`, `TraitIds = TraitPool`, `AttackIntervalMs`
(`BattleModels.cs:9-61`) as `WaveCatalog.Enemies` fills them (`:134-148`); `Key = "wave:{n}"`, `Side =
"wave"`, `SpecimenId = null`, `Kind = Animate` (`:107` — a structure is base-defense's combatant).

### 4. Formation — 1-D rank now, the board consumed later

There is no geometry: `PositionOf(actorKey) => null` with no board (`BattleRunState.cs:459-461, :474`;
`_board` null for every caller until `siege-resolver`, `:80-85`). What exists is **`SideIndex` — "position
within its own side (adjacency)"** (`BattleEngine.cs:50`, set from setup order at `BattleRunState.cs:204-206`).

- **Emit order = `rankOrder`**, slots front → back, draw order within a slot; `SideIndex` *is* rank. This
  fixes the meaning of setup order for delve content only — no shipped setup changes.
- **`RankSpan`** — `int? RankSpan` on `BattleActorSetup`, `[JsonIgnore(WhenWritingDefault)]`, null = 1.
  **Semantics (this module's):** span `k` at rank `i` occupies `[i, i+k−1]`; the next actor is at `i+k`; a
  mask covering *any* occupied rank covers the actor; displacement moves the span. **The field, its golden
  argument and its engine read are `delve-battle-profile`'s** (map row 7). Written only for the `boss` role,
  from `formation.boss.rankSpan`.
- **Reach → contiguous target mask** over enemy ranks (a read model for the intent source, R9; the kernel's
  `SourceOrder` fallback stays): `melee` → rank 0; `short` → 0–1; `long`/`siege` → all.
- **`targetPreference` → default pick within the mask:** `frontline` lowest rank; `backline` highest;
  `swarm` smallest `MaxHp`; `elite` largest; `structure` a `CombatantKind.Structure` if any, else
  `frontline`; `indiscriminate` a uniform draw on the caller's stream. `RangeCells` has zero readers in
  `src/` and stays unread — the anchor's `reach` ordinal is the rank vocabulary.
- **What waits for A10:** lanes, AoE by cell, movement as a turn, a written `SideIndex` for knockback
  (`get`-only today). When `siege-board` lands, rank collapses into column; nothing authored is lost.

### 5. Boss — kit, phases, retinue, `W`, shield pool

**Species.** The `role: boss` slot is filled from `bossSpeciesRef` at the boss room's `Θ_room`; its
`threatBand` must be ≥ `threatWindow.bossFloorRung` or the build refuses. `rung.doubleBoss` (rung 10) fills
the slot twice, `Key`s `wave:boss:0/1`, each with its own phase streams.

**Kit.** `boss.build` names a `ZombossPattern` (nine, `ZombossPatterns.cs:33-77`; `Resolve` throws on an
unknown id, `:89-92`). `ZombossCommanderAllocation.Refresh(scope, θ_boss, tuning)` (`:51-56`) yields an
`AptitudeAllocation` via `PointBudget.PointsFor` (`PointBudget.cs:51`) and `ToAllocation`
(`ZombossPattern.cs:27-40`); it becomes `ChannelMods` through `AptitudeResolver.ResolveForBattle(allocation,
tuning, ladder, θ_boss, registry)` — the Core call `WebMatchService.AptitudeChannelMods` already makes for
the squad (`WebMatchService.cs:486-500`). *"Nothing feeds a wave actor an allocation"* (ideal §11.4): this is
the first feeder — a wiring gap, closed. `signatureAction` is one action id in `EquippedActionIds`
(`BattleModels.cs:88-89`) from round 1; `rung.eliteSecondActionRow` adds a second id from the species'
action rows. Loadouts bind once at `BindContainers` (`BattleRunState.cs:426`) — a phase that *changes the
kit* is the ideal's stated real gap and is not promised: phases change stats, never actions.

**Phases.** `breakpoint` (one threshold) / `escalating` (two), from `phase.*.hpThresholdMilli[]`. A phase
**is** an HP-threshold grant — the `berserker` precedent: a per-hit read of `hp`/`maxHp` against staged
thresholds (`TraitBattleCatalog.cs:46-47`, `BerserkerRampMilli :65-71`, read at `BattleRunState.cs:699-701`).
The grant is one pre-rolled `enemy.` container per phase (§6) from `affix.bossKitTier.{rung.bossKitTier}`,
rolled at setup on `…:boss:phase:{p}`, carried as `BattleActorSetup.PhaseGrants` (nullable,
`WhenWritingDefault`; field and engine read owned by `delve-battle-profile` beside `RankSpan`). When the boss
first satisfies `hp · 1000 < threshold · maxHp` (`long`), the engine grants the phase's effect ids through
the `Host.Bag.Grant` block `BindContainers` uses (`:445`) and calls `RecomposeDerived`
(`BattleRunState.cs:157-161` — *"a real trigger … calls this at the moment it happens"*). `round` and
`ally-down` are the same grant on another predicate; `hp-threshold` is the v1 kind.

**Retinue.** `boss.retinue.{slotRef, countBand}` is one more §2 slot on `…:boss:retinue`:
`n = NextInt(countBand) + rung.bossRetinuePerPartyDelta · (parties − 1)`. **Count only** — stats are §3's at
`Θ_room`; there is no per-party HP anywhere.

**`W`.** `W = raid.modes.{mode}.bossW + rung.bossWDelta`, on `EncounterHalf.W`, applied by the host as a
profile override (`WaveCatalog.cs:80-84`; read at `ActionSlots(profile.W, WScope)`, `BattleEngine.cs:401`).
Outside the boss room `W = formation.{pack,party}.w`.

**Shield pool.** Solo: none. At `parties = N > 1`:

```text
pool = bossShieldPerPartyMilli · P(Θ_room) · (N − 1) / 1000       // long; widen, divide last, once
InnateShield = new BattleInnateShield(pool, Element: null, ShieldPolicy.PriorityInnate, DurationMs: null)
```

`P(Θ_room)` is `PowerLadder.Value` read **once**, here (PS-2). The engine applies it at t0 as an innate grant
(`BattleRunState.cs:294`; `ShieldRuntime.Apply` sets `maxHp = BaseHp + capacity`, `:123`) and drains
it ahead of the sink (`ShieldGate.AbsorbFinalized`, `ShieldGate.cs:51-63`). **The only bound is
`ShieldMath.MaxInput`** — derived from the loaded `ShieldPolicy` coefficients, recomputed per read, and
`AbsorbLayer` **throws** `ShieldInputOverflow` past it (`ShieldMath.cs:50-63, :76`); nothing clamps.
**Registration:** `inventory.json` gains a `location` row for `Core/Delve/Encounter/BossShieldPool.cs`
pointing at §10 rows 20/22 with the note *"PS-2 read-once of P(Θ_room); count on encounter design; not a
curve"*, so `guard-power.ps1`'s G3 finds a reviewed read. No §10 row is added.

**Derivation of the share.** `L_N = BattleReport.Rounds` at `N` parties for a parity squad (`Θ_actor =
Θ_room`); `EHP_N` the enemy side's total effective HP; `D_N` squad damage per round; `ρ_N = D_N / D_1` the
throughput ratio the raid's `W` and bodies produce; `R_N` the retinue count; `κ_x = P(Θ_room + offset_x) /
P(Θ_room)`. `L_N = EHP_N / D_N`; holding `L_N = L_1`:

```text
EHP_1  = P_room · (κ_boss + R_1 · κ_ret)                 EHP_N = ρ_N · EHP_1
pool_N = EHP_N − EHP_1 − (R_N − R_1) · κ_ret · P_room
share‰ = 1000 · [ (ρ_N − 1)(κ_boss + R_1 κ_ret) − (R_N − R_1) κ_ret ] / (N − 1)
```

With the ladder spec's §1 numbers (`Θ_room 100`; `P = 80 + 26.2Θ + 0.2Θ(Θ−1)` refit from its table this
session): `κ_boss(tyrant) = 6608/4680 = 1.41`, `κ_ret(warden) = 5933/4680 = 1.27`, `R_1 = 2`:

| `ρ_2` (pair) | Meaning | `share‰`, `ΔR = 0` | `ΔR = +1` (abyss) |
|---|---|---|---|
| 1.08 | `W` fixed, squad already above `W` | **316** | 0 — retinue alone covers |
| 1.25 | `bossW` 4 → 5 | 987 | 0 |
| 2.00 | `W` doubling | 3,950 | 2,680 |

**300‰ is the right order when `W` barely moves and the retinue grows on the upper rungs; the derivation
asks for far more under a `W` that doubles.** That is why the share is a tunable calibrated against the
engine, not a constant argued on paper: `ρ_N` depends on the intent policy's target spread, on `WScope`
and on how many bodies exceed `W` — none visible to this arithmetic. **Starting shape 300‰ (RoR2's
`1 + 0.3·(n−1)`, `08-endless-scaling-meta-progression.md:66`), lower reference 170‰ (the review's D10
figure), settled by §Testing's 32-seed sweep at 1/2/4 parties against `boss.fightLengthTargetRounds`.** A
sweep outside the band changes the tuning file, never the formula.

### 6. Elite affixes and the seventh `ContainerKind`

`ContainerKind` is closed at six (`ContainerRow.cs:7-15`); **this module adds the seventh, `Enemy`** — one
enum member, one `PrefixOf` arm (`"enemy"`, `:142-151`), one alternative in `ContainerValidator`'s id regex
(`ContainerValidator.cs:23-24`). Id `enemy.{encounterId}-{elite|boss-p{n}}`. The container: `Rarity =
affixRoll`; `MinTier/MaxTier` from `affix.{elite,boss}KitTier.{t}.{floorRung, ceilRung}`; `PrefixRolls +
SuffixRolls = affixCount` split by the rung's bands (`RarityRow`, `ContainerRow.cs:163`); `Pool` = every
affix-library row tagged for enemies. **`exclusiveTags`** need no mechanism: each tag becomes the `Group` of
the pool rows carrying it, and the one-per-group rule (`ContainerRow.cs:33-38`) refuses the pair. The roll is
`Instantiator.TryInstantiate(container, lookupAtom, lookupAffix, rollSeed, thetaContent: Θ_room, tuning,
out instance, …)` (`Instantiator.cs:98-107`) on `…:elite` — **rung → count band + tier window only**, never a
magnitude. The instance's effect ids ride as `BattleActorSetup.GrantedContainerIds` (nullable,
`WhenWritingDefault`; `delve-battle-profile`'s field) and are granted at setup through the `BindContainers`
grant block, the resolver answering from the minted instance (`IContainerEffectResolver.cs:13`).

**The library gap, named as the external dependency it is:** `data/seed/effects/affixes/all.json` holds
**two** entries (`affix-draw-000/-001`, both `suffix` on-hit bundles); D2's monster-mod identity list is
`effect-pipeline`'s affix-authoring work. When the pool has zero drawable rows in the tier window, **the
elite path degrades to "no affix" with a warning row** (`EncounterHalf.Warnings`, the shape of
`BattleReport.Warnings`, `BattleModels.cs:428-429`) — never a fake affix, never a flat stat bump.

### 7. Raid symmetry — a `party` template is what a rival summoner fields

`formation.party` is `formation.party.slots.{min,max}` distinct slots, at most `maxRepeatedPosture` posture
repeated, authored front → back — the same shape the player brings (`raid.modes.*.squadSlots`;
`WebMatchService.cs:343`'s `maxSquad = 6` is the anti-pattern and is not read). A rival summoner's party is a
`party` anchor filled by §2; nothing else is needed. `pack` = one posture, `many`, flat; `boss` = one `role:
boss` + retinue × parties. Room kind fixes the formation (`fight`/`wild` → `pack`, `elite` → `party`, `boss`
→ `boss`, seed contract §1.2) and a mismatch refuses. `PartyIndex` is `delve-battle-profile`'s label on the
player's side; this module never emits it.

### 8. Refusals and preflight

Every refusal is a thrown `EncounterRefusal` naming the encounter id, slot and filter — no flag, no fallback:

1. **Unfillable slot** — zero candidates after §2 step 2. **`reach: siege` is one today:** 0 of 841 anchors
   carry it (counted from disk this session: `short 605 · long 168 · melee 68`).
2. **Species without `threatBand`** in any candidate set — refused, *never* the rung-4 default. Today **657
   of 841** anchors across 503 files lack the key (recounted; S1-7 holds); the middle rungs are nearly
   empty (`pest 3 · marauder 1 · raider 10 · warden 10 · scourge 1`), the top four hold 23.
3. `count < 1` after the rung delta; formation ≠ room kind; `bossSpeciesRef` below `bossFloorRung`; an
   unknown pattern id; a `phaseTrigger` of kind `summon` or a fixture role — **blocked on `siege-waves`
   roster-add and `combatant-kind`'s non-acting combatant**; `summon.capPerBoss` is loaded (T5) and
   unread until then.

**Preflight** — `EncounterPreflight.Run(corpus, domains, tuning)`, model-free, run by the domain importer
and the tests: for every domain, every encounter its `roomPalette` reaches, every slot — count candidates
ignoring element, then under the domain's climate with the spread set. Any zero → **the domain is refused
for shipping** with the row named. It prints the per-rung histogram above — exactly what `threat-audit`
changes; against today's corpus it refuses every domain whose windows sit on rungs 2–6, and that refusal
is the gate the map names, not a defect here.

**Closed-loop metric — cell coverage.** Over a domain at one rung, 32 seeds × its fight/elite/boss rooms:
distinct `(postureMultiset, elementSpread, formation)` cells reached. The pass threshold is a budget row
(`data/seed/dungeon/_plan/budget.v1.json`, seed contract §7 — a quality gate, not a balance number); the
report is a test. **StS sibling rule at graph rows:** two same-kind siblings on one row resolving to the
same cell are a finding here and a filed ask on `delve-graph-roll` (archetypes per row drawn without
replacement where the palette cell has ≥ 2).

### 9. Determinism

Pure over `(anchor, Θ_room, climate, raid, rung, seed, corpus, tuning)`: same inputs ⇒ byte-identical
`Enemies`, `W`, `Warnings`, `Cell`. Corpus rows in ordinal `SpeciesId` order (`WaveCatalog.cs:122-123`'s
`OrderBy(…, Ordinal)`); no dictionary enumeration reaches an output; every draw is a named stream; the elite
instance reproduces over `(container, catalogRevision, rollSeed, Θ_room)` by `Instantiator`'s own contract.
No clock, no store, no I/O, no retry loop.

## Tunables

**Read** from `data/tuning/encounter.v1.json` (owner `dungeon-registries`): `slot.countBand.*.{min,max}`
(lone 1–1 · few 2–3 · several 3–5 · many 4–7 — ideal §11.4's 4–7 pack); `threatWindow.bossFloorRung`
(`tyrant`); `spread.{mono,dual,rainbow}.offClimateMilli` (0 · 400 · 700); `formation.{pack,party}.w` (4 —
`hybrid-atb`'s, `battle.v4.json:36`); `formation.party.slots.{min,max}` (3–5); `formation.party.
maxRepeatedPosture` (1); `boss.fightLengthTargetRounds.{min,max}` (8–14 of `MaxRounds 50`);
`affix.{elite,boss}KitTier.{t1,t2,t3}.{affixCount,floorRung,ceilRung}` (elite 1/2/3 over `sprout–cultivated ·
grafted–fused · cultivated–heirloom`; boss 2/3/3 over `cultivated–chimeric · fused–heirloom ·
heirloom–sunwoven` — PoE 3.20's 1–2 acts / 2–3 maps); `phase.breakpoint.hpThresholdMilli [500]`,
`phase.escalating.hpThresholdMilli [660, 330]`; `summon.capPerBoss` (3, unread until roster-add);
`pack.sameSpeciesMaxMilli` (600). From `dungeon.v1.json`: `raid.modes.*.{parties,squadSlots,bossW}`
(`bossW` 4 · 5 · 6 — proposed here; the registries spec named the key without values);
`difficulty.rungs[].{enemyCountDelta,bossRetinuePerPartyDelta,bossWDelta,eliteKitTier,bossKitTier,
doubleBoss,eliteSecondActionRow}` (ladder spec §2).

**Two keys added, derived and named above, filed on `dungeon-registries` as schema changes through
`publish.py`:** `raid.modes.{pair,quad}.bossShieldPerPartyMilli` (‰ `long`, 300 — decision 3's own name;
absent on `solo` by schema, not by default) and `formation.boss.rankSpan` (int, 2 — DD's Large).
**Not keys:** `threatWindow.defaultRungs` (a default), `rank.spanMax` (derived from `squadSlots`),
`tempo.*.wOverride` (retired — `tempo` is a species filter), `affix.exclusiveTags` (the library's row).

## Numeric types

| Quantity | Type | Why |
|---|---|---|
| `MaxHp`, `Atk`, `Defense`, shield pool, `P(Θ_room)` | `long` | ladder-driven magnitudes; `share * P / 1000` widened before the multiply, divided last, once; `PowerLadder` throws past `MaxIndex`, `ShieldMath` past `MaxInput` |
| `Θ_room`, `θ_enemy`, `thetaOffset` | `int` | `RoomTheta.Theta` is `int`; the sum is `checked` — `SpeciesExpander.cs:67`'s shape |
| counts, ranks, `RankSpan`, `W`, parties, rung and tier ordinals | `int` | bounded by the graph, the raid row or the ladder |
| every `*Milli`, `hpThresholdMilli` | `long` | a ‰ multiplies into a `P(Θ)` magnitude; the threshold test is `hp * 1000 < threshold * maxHp` in `long` |
| `rollSeed` | `ulong` → `long` | `NextULong()` reinterpreted for `Instantiator`'s `long rollSeed` |

No `float`/`double` in this module; `AptitudeResolver`'s doubles stay inside its own boundary.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Encounter"                       # goldens, properties, refusals, sweep
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Expedition"  # hashes untouched
.\scripts\guard-power.ps1                                     # G3 sees the registered read, no new curve
python scripts\audit-magic-numbers.py --domain dungeon        # Delve/Encounter adds zero M1 rows
python -m seedsmith dungeon audit                             # runs EncounterPreflight over the corpus
```

## Structure

```
src/FusionRpg.Core/Delve/Encounter/
  Encounter.cs             Build(anchor, roomTheta, climate, raid, rung, seed, corpus, tuning) → EncounterHalf
  SlotFilter.cs            Candidates(corpus, slot, window, spreadSet) — §2 step 2, posture via roster
  SlotFill.cs              count + weighted draw with the same-species cap; stream names
  RankOrder.cs             rankOrder → emit order; reach mask; targetPreference default pick (read model)
  BossBuild.cs             pattern → allocation → ChannelMods; phases; retinue; W
  BossShieldPool.cs        the one P(Θ_room) read (inventory.json location row)
  EliteAffix.cs            enemy.* container + TryInstantiate + the no-affix warning
  EncounterPreflight.cs    per-domain candidate counts, rung histogram, refusals
  EncounterCoverage.cs     (postureMultiset, spread, formation) cells; sibling findings
  EncounterRefusal.cs
src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs, ContainerValidator.cs  → ContainerKind.Enemy, prefix arm, regex alternative
docs/architecture/power/inventory.json                                  → one location row (a read, not a curve)
tests/FusionRpg.Core.Tests/Delve/Encounter/
```

`RankSpan`, `PhaseGrants`, `GrantedContainerIds` on `BattleActorSetup` and their engine reads live in
`delve-battle-profile`'s files; this module only writes them.

## Code style

Mirrors `WaveCatalog.Enemies`: a static pure builder over ordered pools, `BattleRuleset` for every
magnitude, no logging — the warnings list is the record.

```csharp
static IReadOnlyList<ConcreteAnchor> Candidates(
    IReadOnlyList<ConcreteAnchor> corpus, EncounterSlot slot, ThreatWindow window, ElementSet spread)
{
    var list = new List<ConcreteAnchor>();
    foreach (var a in corpus)                       // ordinal SpeciesId order — never a dictionary
    {
        if (a.ThreatBand is null)
            throw new EncounterRefusal(slot, $"'{a.SpeciesId}' has no threatBand — never the rung-4 default");
        if (!window.Contains(a.ThreatRung)) continue;
        if (PostureOf(a.AptitudePrimary) != slot.Posture) continue;          // roster.json, not the file's echo
        if (slot.Reach is not null && a.Reach != slot.Reach) continue;
        if (slot.TargetPreference is not null && a.TargetPreference != slot.TargetPreference) continue;
        if (!spread.Contains(a.ElementPrimary)) continue;
        list.Add(a);
    }
    if (list.Count == 0) throw new EncounterRefusal(slot, "unfillable — no anchor satisfies the tuple");
    return list;
}

static BattleActorSetup Emit(ConcreteAnchor a, int roomTheta, DemonThreatTuning threat, int n)
{
    var theta = checked(roomTheta + threat.OffsetFor(a.ThreatBand));      // Θ_room + thetaOffset — the sum
    return new()
    {
        Key = $"wave:{n}", Side = "wave", SpeciesId = a.SpeciesId, TypeId = a.DemonTypeId, Level = theta,
        ElementPrimary = a.ElementPrimary, ElementSecondary = a.ElementSecondary, TraitIds = a.TraitPool,
        MaxHp = BattleRuleset.BaseHp(theta), Atk = BattleRuleset.BaseAtk(theta),
        Defense = BattleRuleset.BaseDefense(theta), AttackIntervalMs = a.AttackIntervalMs
    };
}
```

## Testing strategy

- **Goldens:** one encounter per (formation × kit tier) — `pack`, `party`, `boss` × t1/t2/t3 = 9 — hashed
  over the emitted setups in order, blessed once against a fixture corpus with every rung filled.
- **Property tests** (256 seeds): every enemy `Level == Θ_room + OffsetFor(threatBand)`; emit order equals
  `rankOrder` and `SideIndex` equals rank; no species above the same-species cap; `W` equals the raid row
  plus the rung delta; the elite affix instance reproduces over `(container, revision, seed, Θ_room)`.
- **Corpus satisfiability:** *after* `threat-audit`, every shipped encounter's slots are satisfiable over
  the shipped corpus under every climate (preflight green); *before* it, a refusal fixture over today's
  corpus asserts the preflight names the empty rungs and refuses — red now by design.
- **Refusals named:** a `siege` slot; a null `threatBand`; `count < 1`; formation ≠ room kind; boss below
  `bossFloorRung`; a `summon` trigger — each a thrown `EncounterRefusal` with the slot named.
- **Boss shield sweep:** parity squads at 1/2/4 parties, 32 seeds, automated intent source, the `delve`
  profile: `BattleReport.Rounds` within `boss.fightLengthTargetRounds` at every `N`; solo carries no
  `InnateShield`; the pool equals `share · P(Θ_room) · (N−1) / 1000` exactly.
- **Battle goldens and expedition hashes untouched:** the four battle hashes, the 32-seed battle sweep and
  the four expedition tier hashes run in the same command and do not move — setups for delve content only,
  no serialized field on shipped setups touched.
- **Seventh kind:** `enemy.x` validates; `enemy.` under a `Trait` kind is refused; the six old alternatives
  pass; `guard-power.ps1` green with the `inventory.json` row. **No-affix degradation:** an empty pool
  yields an elite with no `GrantedContainerIds` and one warning row.

## Boundaries

- **Always:** `θ = Θ_room + thetaOffset` through `BattleRuleset`; a named stream per step; refuse rather
  than default; `P(Θ_room)` read once for the pool; `exclusiveTags` through pool groups; `W` returned,
  never serialized; setups only for delve content.
- **Ask first:** a `party` anchor whose slots exceed `squadSlots`; a fourth formation; a phase that
  rebinds actions; reading `deployMode` for anything; a boss-side aura (`ActiveCommanderAura`,
  `BattleModels.cs:279` — the pattern's `AuraId` exists, enemy-side delivery is not specified here).
- **Never:** an HP multiplier per party; a species id in an anchor; a fourth role vocabulary; a private
  `f(level)`; an affix invented when the library is empty; drawing from the rung-4 fallback; a retry loop;
  `System.Random`/`DateTime`; a `float` magnitude; a new `ssot-power-scale.md` §10 row; a `W` or profile
  field on `BattleSetup`; `WaveCatalog.ProfileForExpedition` or `ProfileForWave` on the delve path.

## Success criteria

1. Nine goldens blessed; 256-seed property sweep green. 2. Every refusal has a named throwing test; the
pre-`threat-audit` preflight fixture is red and says why. 3. The 32-seed boss sweep lands inside the band
at 1/2/4 parties with the shipped share, or the share is republished and re-run — the formula is untouched
either way. 4. Battle goldens, battle sweep and expedition hashes byte-identical. 5. `ContainerKind.Enemy`
validates end-to-end and an elite's atoms reach the actor. 6. `guard-power.ps1` green, `inventory.json` +1
location row, §10 unchanged. 7. `audit-magic-numbers.py --domain dungeon` adds zero M1 rows under
`Delve/Encounter`. 8. Cell coverage per domain meets the budget row on the six first-ship domains (G4).

## Interface exposed to dependents

| Member | Returns | Consumer |
|---|---|---|
| `Encounter.Build(anchor, roomTheta, climate, raid, rung, seed, corpus, tuning)` | `EncounterHalf { Enemies, W, Warnings, Cell }` — the `BattleSetup.Wave` half plus the profile override | `delve-battle-profile`: composes `BattleSetup` with the party half, applies `W`, passes `profile:` and the intent source to `Resolve` |
| `BattleActorSetup.Level` on every enemy | `θ_enemy` | `dungeon-loot` — `SoulEarnPolicy.KillEarn(θ_enemy)` per kill; `delve-attrition` reads the role (elite/boss presence), not the stat |
| `Enemies[i].{RankSpan, PhaseGrants, GrantedContainerIds}` | written here | `delve-battle-profile` owns the fields, their `WhenWritingDefault` golden argument and the engine reads |
| `EncounterPreflight.Run(corpus, domains, tuning)` | refusals + rung histogram | `domain-catalog` importer; `dungeon-seed-contract`'s `dungeon audit` |
| `EncounterCoverage.Report(domain, rung, seeds)` | distinct cells, sibling findings | G4; the seedsmith budget loop |
| `ContainerKind.Enemy` | seventh kind, `enemy.` prefix | `effect-pipeline` affix authoring (enemy-tagged pool rows) |

## Design-gate checklist

```
[x] Subsystems: battle kernel (setup, SideIndex, W, innate shields), power ladder (Θ sum, P(Θ) read),
    shield system, effect atoms (containers, Instantiator), demon species corpus, party dungeon.
[x] Read this session, in order: party-dungeon-map.md (row 6, external deps, G2/G4); the five approved
    wave-1 specs; ideal §4.4, §4.8, §11.4 in full, §8 box 3, §10, §11.10 R1/R9; audit §1(d), S1-7, S2-5,
    S2-9, §9; 08-endless-scaling §1.2; spec-expeditions.md (format); decisions.md:42 (amended clause present).
[x] Every code claim cites file:line opened this session. DRIFT against the brief: BattleActorSetup is
    :7-120 (Kind :107, GarrisonedBy :119 added since); BaseHp/Atk/Defense are :218-221 (not :172-175); the
    contest baselines are :239-242 (not :193-196 — the ladder spec's cite is stale too); ActiveCommanderAura
    is :273 (not :220); BattleEngine adjacency is :598-609 (not :573-576), the W/slots read :401 and the
    PerSide economy key :386-387 (not :357-358); BattleRunState materialises Actors at :204-206 (not
    :182-184), BindContainers is :426 (grant block :445), PositionOf is :474 with its null-board comment at :459-461 (not
    :428); TraitBattleMath is inside TraitBattleCatalog.cs :62-72 (no separate file); DemonThreatTuning.cs is
    under Demons/Generation/; ZombossPatterns' nine entries are :33-77 (the ideal's :74-117 is stale); the
    species _index.json is a speciesId → file map (840 keys), so anchor counts came from the 503 species
    files (841 anchors).
[x] Verified against CODE, not comments: OffsetFor's fallback body; ShieldMath.MaxInput's derivation and the
    throw at :76; ShieldRuntime.Apply's maxHp = BaseHp + capacity; ContainerValidator's regex;
    ZombossPattern.ToAllocation; AptitudeResolver.ResolveForBattle via WebMatchService; the two affix
    entries; roster.json postures; battle.v4.json hybrid-atb w=4.
[x] Constraints tested where testable without code: corpus counts recomputed from disk (657/841 missing
    threatBand; siege 0; top four rungs 23); P(Θ) refit from the ladder spec's table (κ values). Not run: any
    dotnet suite — the spec changes no code; "hashes untouched" is a build-phase criterion.
[x] No §2 invariant contradicted: no injector, no private curve, no cap, no multiplier, tunables in data.
    Two keys added and named (bossShieldPerPartyMilli — the owner's own name; formation.boss.rankSpan).
[x] Propagations landed 2026-09-05 (verification pass): registries spec carries the two keys; the ideal's
    §11.4 ZombossPatterns/BattleRunState cites corrected; the ladder spec's BattleModels cite corrected;
    delve-graph-roll's "closed six-kind" line names the seventh. `RankSpan`, `PhaseGrants` and
    `GrantedContainerIds` are rows in spec-delve-battle-profile.md §5 with their golden argument.
```
