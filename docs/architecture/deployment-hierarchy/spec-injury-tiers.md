# Spec: `injury-tiers`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this
session. Module id `injury-tiers`, row 2 of the [deployment-hierarchy map](../deployment-hierarchy-map.md)
(wave 1, depends on `deploy-carry` (module 1, built — [spec-deploy-carry.md](spec-deploy-carry.md))
and decisions.md row **P1 — Status SSOT amended 2026-09-13** (`wound.*` joins the closed status
vocabulary)). Ideal: [deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md) §"Death, injury
tiers, and who dies for real"; decisions.md rows **Status SSOT** (amended 2026-09-13) and
**Deployment hierarchy SSOT (2026-09-13)**.

## Objective

Today a lawn Bound death is free: `TryRecoverActiveByPtr` (`RpgStore.UniqueActors.cs:733-790`) awards
XP and flips `ActiveBound→Roster` in one write — no penalty, no memory of the fight. A delve
non-permadeath death is one clock for everyone: `ExtractionSettlement.Decide` (`ExtractionSettlement.cs:34-64`)
only ever asks `downedOnce`/`permadeathApplies` and returns `Retire`/`Recover`/`Roster` — no grading
of *how bad* the death was. This module makes death cost something graded, not binary:

1. Grade a `wound.*` tier from **%HP lost past a relative threshold** (Battle Brothers/XCOM shape) at
   the point a specimen would otherwise walk away clean — a delve `Recover`/`Roster` settlement, or a
   lawn die. The tier is a status **spec** that rides `deploy-carry`'s `Statuses` carry path into the
   specimen's *next* deployment exactly like any other status (`spec-deploy-carry.md`'s own Interface
   row) — no new carry mechanism.
2. Persist the graded tier and a durable **worsening counter** per specimen (new table, sibling of
   `rpg_unique_actor_recovery`), ticking on the specimen's own settlement clock — never wall time.
   Left uncured, it can advance the tier and, at the top, Retire the specimen for real — the *other*
   producer of "real death" alongside hardcore-delve `Retired`, feeding the same `corpse-cache` trigger
   (module 3).
3. Ship `wound.*`'s own anti-spiral constructor check — `StatusRuntime.Apply`/`StatusCatalog.Register`
   enforce nothing on their own (verified this session: neither method reads or writes a resource-regen
   channel), so this module's own policy class must, mirroring `ExhaustionPolicy.cs:54-66` /
   `NervePolicy.cs:63-74`.
4. Wire the two recovery clocks this wave has real evidence for — delve-counted `Recovering`
   (existing) and a priced ritual (existing `SoulSinkPolicy`) — and **not** wire "world-turn legion
   rest": its target (a wound on a unique attached to a world-map legion) does not exist yet, and the
   code the ideal doc gestures at for it, `LegionSupply.cs`, is confirmed this session to be a pure
   loam capacity/burn/top-up mechanism with zero HP/wound logic (§What already exists, Real gap).
5. Leave the tier-severity **curve** OWED. `ssot-power-scale.md` §10 has no row for it today (confirmed
   by a full-document search this session) — this spec ships the *structure* (a closed per-mille
   threshold ladder, mirroring the one relative-HP-threshold check already shipped,
   `PhaseGrant`/`BattleModels.cs:210-215`) and explicitly does not author the `base + %lost × scale`
   binding to a real potency/power read. That binding and its §10 row are a named follow-on, not
   invented here (map's own "Open items carried from the ideal").

Success looks like: a specimen graded into a wound tier fights measurably weaker on its **next**
deployment (not the current one — G2 in the map), proven through `deploy-carry`'s own two-room
round-trip harness; an untreated tier advances on its own settlement clock and can Retire the specimen
without ever reading a wall clock; the anti-spiral constructor check refuses a tier whose stat block
touches its own recovery-relevant regen channel; `ssot-power-scale.md` §10 is unchanged (no row added
without review) and `guard-power.ps1`-shaped discipline holds.

## Locked anchors

- **Statuses ride the existing carry path, no new mechanism** (`spec-deploy-carry.md` Interface row,
  verified this session): a carried `wound.*` status is a `BattleStatusSpec(StatusId, MagnitudePerPulse,
  DurationMs, PeriodMs, GrantChanceMilli)` (`BattleModels.cs:247-248`) in `DelveMemberState.Statuses`
  (`DelveMemberState.cs:18-25`), applied through `StatusRuntime.Apply` at the child's first tick with
  full resistance — never pre-applied, never a live `StatusInstance` reference.
- **Power-neutral apply; the potency read happens at grading time, not inside `Apply`** (F5-power
  correction, verified this session): `ResistanceEvaluator.cs:294-302` already excludes attacker-less
  applies from the power contest by construction (`attackerLess ? 0 : defender.TierPower × ...`, line
  302) — `wound.*` MUST apply `AttackerLess: true`, exactly like `ExhaustionPolicy.Sync`
  (`ExhaustionPolicy.cs:120-133`) and `NervePolicy.Sync` (`NervePolicy.cs:111-126`). The %HP-lost read
  that decides *which* tier to grade happens **before** `Apply` is ever called, as ordinary `long`
  arithmetic over already-resolved pool values — never inside the resist/potency math.
- **Anti-spiral is per-policy, not runtime** (F6-spiral correction, verified this session):
  `StatusRuntime.Apply` (`StatusRuntime.cs:218-298`) and `StatusCatalog.Register` enforce no
  self-referential-channel check of any kind — the guard exists only inside `ExhaustionPolicy`'s own
  constructor (`ExhaustionPolicy.cs:54-66`, rejects a mod touching the resource's own
  `resource.regen.{id}`) and `NervePolicy`'s own constructor (`NervePolicy.cs:63-74`, rejects a mod
  touching `resource.regen.spirit`). This module's own `WoundPolicy` ships the identical shape of
  check or ships unprotected — there is no third place it could live.
- **The worsening counter is durable per-specimen state, never on `StatusInstance`**
  (`StatusInstance`, `StatusRuntime.cs:8-62`, has no count field of any kind — confirmed by reading the
  whole class). Home: a new table sibling to `rpg_unique_actor_recovery`
  (`RpgStore.cs:547-554`) — mid-delve it rides party state exactly like `DelveMemberState.NerveStacks`
  (`DelveMemberState.cs:18-25`).
- **`wound.*` joins the closed status vocabulary the `nerve.*` way, never a runtime-authored kind**
  (map assumption #2, decisions.md Status SSOT row, verified against `StatusCatalogBootstrap.cs:62-68`'s
  static `9.5 Nerve` block): ids are registered in `StatusCatalogBootstrap.cs` at compile time; a
  policy class only *reads* them (`catalog.GetRequired(...)`, `NervePolicy.cs:76`) — it never
  registers them dynamically the way `ExhaustionPolicy`'s constructor does for `exhaustion.*`
  (`ExhaustionPolicy.cs:70-77`). The two existing precedents differ on this exact point; `wound.*`
  follows nerve's.
- **Troop representation, world-map, and siege are out of scope** (deploy-carry's own Locked anchors;
  ideal doc's wave-scope note, `deployment-hierarchy-ideal.md:219`): this module wires lawn and delve
  only. `WorldEntityMember.Hp/Wounds` is untouched.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `StatusRuntime` full lifecycle (Apply/Tick/ClearGrant/Clear), SSOT per `entity:{ptr}` | `src/FusionRpg.Core/Status/StatusRuntime.cs:138` (SSOT comment), `:218-298` (`Apply`), `:370-439` (`Tick`), `:443-470` (`ClearGrant`), `:497-501` (`Clear`) |
| Attacker-less applies excluded from the power contest by construction | `src/FusionRpg.Core/Status/ResistanceEvaluator.cs:294-302` — `var totalResist = (attackerLess ? 0 : defender.TierPower * StatusPolicy.ResistFromPowerRatio) + ...` |
| `nerve.*` — the exact precedent this module follows: static catalog registration, a pure stage resolver, a policy `Sync` with its own anti-spiral ctor check, a stack counter riding party state | `src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs:62-68` (`9.5 Nerve` block, `nerve.unsettled`/`.shaken`/`.afflicted`); `src/FusionRpg.Core/Delve/Attrition/NerveLadder.cs:12-23` (`StageFor`); `src/FusionRpg.Core/Delve/Attrition/NervePolicy.cs:54-81` (ctor + spiral check at `:63-74`), `:92-127` (`Sync`); `DelveMemberState.cs:23` (`NerveStacks`) |
| `exhaustion.*` — the sibling anti-spiral precedent (dynamic registration, not the one this module follows) | `src/FusionRpg.Core/Actions/Cost/ExhaustionPolicy.cs:50-81` (ctor, spiral check `:54-66`, live `catalog.Register` `:70-77`), `:88` (`IsExhausted`), `:99-136` (`Sync`), `:127` (`BaseDuration: 0` → persists until `ClearGrant`) |
| Durable per-specimen state family already exists for exactly this shape (a phase + a countdown row) | `src/FusionRpg.Data/Sqlite/RpgStore.cs:547-554` — `rpg_unique_actor_pools` / `rpg_unique_actor_recovery` (`instance_id TEXT PRIMARY KEY, player_id, recovery_delves_left, wounded_delve_id, theta_run`) |
| `Recovering` phase set, countdown, and priced-ritual clear — the exact recovery-clock shape this module reuses unmodified | `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs:415-435` (phase→Recovering + recovery row upsert), `:446-488` (`DecrementAllRecoveringForPlayerUnlocked`, "R6 — virtual time, no clock", every `CloseDelve` decrements every Recovering row this player owns), `:521-523` (`TryPerformRecoveryRitual` signature) |
| Priced ritual: `SoulSinkPolicy.Price` + `risk.recoveryRitualSouls.{rung}` tuning key | `src/FusionRpg.Core/Creatures/SoulSinkPolicy.cs:40-41` — `Price(long basePriceSouls, int thetaContent, PowerTuning tuning)`; loader `src/FusionRpg.Core/Dungeon/Tuning/DungeonTuning.cs:273-274`; shipped values `data/tuning/dungeon.v1.json:297-307` (`very-easy:200` … `abyss:25600`) |
| `Retire`/`Recover`/`Roster` already decided per-member at delve extraction, and `Retire` already calls the one, idempotent, phase-only-flip retire function | `src/FusionRpg.Core/Delve/Attrition/ExtractionSettlement.cs:34-64` (`Decide`, pure); dispatch `src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs:830-856`; retire fn `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs:358-385` (`RetireUniqueActorUnlocked` — flips `phase` to `Retired` + clears `match_key`/`last_ptr`/`deploy_correlation_id`; idempotent on an already-`Retired` row, line `:362-363`; **touches no gear/assignment table** — confirmed by reading the whole function) |
| Permadeath rung gate, read-only, unchanged by this module | `src/FusionRpg.Core/Delve/Difficulty/PermadeathGate.cs:12` (class), `:14-17` (`Applies`) |
| A shipped, relative-HP-per-mille threshold check — the exact shape this module's tier check mirrors | `src/FusionRpg.Core/Battle/BattleModels.cs:210-215` — `PhaseGrant(long HpThresholdMilli, ...)`, doc comment: `"hp * 1000 < HpThresholdMilli * maxHp"`, `long`, widen-before-multiply |
| `MaxHp`, `CurrentHp`, `CarryInPools` — all `long`, all the fields this module's grading input reads | `src/FusionRpg.Core/Battle/BattleModels.cs:42` (`MaxHp`), `:159` (`CurrentHp`), `:192` (`CarryInPools`); `BattleActorResult.HpRemaining`/`.Survived`/`.Retreated` `:509-512` |
| The registered seam for reading an actor's max HP from a derived snapshot — never a private read | `src/FusionRpg.Core/Actions/Cost/ActorResourcePools.cs:25` — `ResourceChannelReader.Max(derived, Ids[i])`; resolved via `ActorHub.ResolveDerived` (`src/FusionRpg.Injector/Effects/InjectorStatusBridge.cs:23`) |
| `deploy-carry` (module 1) makes `Statuses` a real, tested, two-room carry path for delve | `spec-deploy-carry.md` (this session's read) — Objective, §Design 1/3, Interface row: *"a carried `wound.*` status rides `Statuses` exactly like any other status spec, no new carry mechanism needed"* |
| Fixed, non-rolled tier-container precedent (JSON shape + parser) this module's `wound.v1.json` copies verbatim | `data/seed/dungeon/_containers/nerve.v1.json` (3 entries, `{id, stage, stat: {channel: {increased: n}}}`); loader `NerveContainer.Load`, `src/FusionRpg.Core/Delve/Attrition/NervePolicy.cs:150-175`, reusing the shipped `StatusStatPayload.TryParse` |
| `status-ssot.md` §9.6's nerve entry is the exact documentation precedent this module's own §9.7 entry copies | `docs/architecture/status-ssot.md:312-325` |

### Wiring gap

The machinery this module needs exists on one side and is silent on the other:

| Gap | The inert line |
|---|---|
| The lawn-die ingest path carries zero HP/damage signal — there is nothing to grade a tier from at the one place a lawn death is recorded | `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs:733-790` (`TryRecoverActiveByPtr`) reads only `ptr`/`matchKey`/`occurrenceId`/`killerPtr`/`activeMatchMs`; the `plant.die`/`zombie.die` capture payload it is fed from (`src/FusionRpg.Injector/GameHooks.cs:657-677`) carries only `type`/`typeName`/`ptr`/`reason`/`reasonName`/`lifecycleOccurrence` plus the fatal-killer fold — no HP, no max HP, confirmed by reading the whole `Postfix` |
| Delve non-permadeath settlement already computes a stage from `NerveLadder.StageFor` but has no equivalent wound-tier read, and `DelveMemberState` has no field to carry a graded tier forward | `RpgStore.Delve.cs:830-856` computes `stage`/`afflicted` (line 834-835) and calls `ExtractionSettlement.Decide` but never reads `member.Pools["hp"]` against a max for a wound grade; `DelveMemberState.cs:18-25` has `NerveStacks` but no sibling wound-tier field |
| `rpg_unique_actor_recovery` carries a delve countdown but no tier id and no worsening count — the shape for the NEW table this module adds does not exist yet | `RpgStore.cs:551-554` — columns are `recovery_delves_left, wounded_delve_id, theta_run` only |
| `RetireUniqueActorUnlocked` is reusable from an open connection/transaction (its own doc comment says so) but today has exactly one caller — this module's second caller is new wiring, not a new function | `RpgStore.UniqueActors.cs:354-357` (doc comment: *"reusable from a caller that already holds an open connection... exactly like `AwardUniqueActorXpUnlocked`"*); sole existing caller `RpgStore.Delve.cs:849` |

### Real gap

| Gap | What would have to be built |
|---|---|
| No `wound.*` status ids exist anywhere | `StatusCatalogBootstrap.cs` has no `9.7 Wound`-shaped block (confirmed by reading the whole file); `data/seed/dungeon/_containers/` holds only `nerve.v1.json` (confirmed by glob) |
| No durable worsening-counter table exists | `rpg_unique_actor_recovery` counts a delve clock **down** to a clean Roster return — nothing counts a wound clock **up** toward a worse tier or a Retire |
| No untreated-worsening mechanism exists in `src/` at all | Confirmed by a repo-wide search this session — zero hits for a worsening/injury-tier concept outside this session's own new docs |
| **World-turn legion rest for a wound — real gap, and the ideal doc's own cited precedent for it is a mis-citation.** `LegionSupply.cs` is a loam capacity/burn/top-up mechanism only | Read in full this session (`src/FusionRpg.Core/World/Loam/LegionSupply.cs:1-220`): `BearerCount`, `Capacity`, `Burn`, `LeashTurns`, `TurnsUntilExhausted`, `Resolve` (tops up `WorldEntity.CarriedLoam` toward `Capacity` in supply, burns and can destroy a legion outside it) — **zero** reference to `Hp`, `Wounds`, injury, or recovery anywhere in the file. The map's row-2 description names "world-turn legion rest" as a per-deployment-kind recovery clock for this module; there is no code precedent for it. This module does not build it — the target (a wound on a unique attached to a world-map legion, `WorldEntityMember.InstanceId`) does not exist either (`WorldState.cs:278`, written by nothing today, ideal doc's own Wiring-gap table, unchanged this session) — deferred to the world-map program per the ideal doc's wave-scope note (`deployment-hierarchy-ideal.md:219`) |
| `ssot-power-scale.md` §10 has no injury-tier row | Confirmed by a full-document search this session (`injury`/`wound` — zero hits outside the ideal/map docs); the `base + %lost × scale` binding to a real potency/power read is OWED, not decided here |

## Design

### 1. Tier vocabulary — a `9.7 Wound` block, the `nerve.*` way

Add to `StatusCatalogBootstrap.cs`, immediately after the `9.5 Nerve` block (`:62-68`), following its
exact `Register(catalog, id, kind, family, category, stacking, payloadKind)` call shape: `N` ids
`wound.{tier}` (starting shape 3 — `wound.minor`/`wound.serious`/`wound.critical`, matching nerve's
3-stage precedent and Battle Brothers' minor/major/permanent-adjacent shape), `StatusKind.Debuff`,
family `"wound"`, primary category `StatusL2bCategory.Dot` (matching nerve's own precedent — *"despite
none being literal damage over time"*, `status-ssot.md:302`), `StatusStacking.Replace` (one wound tier
live at a time, exactly like nerve — a tier change is a different `statusId`, never a re-`Refresh` of
the same one), payload `StatusPayloadKind.ModifyStat`. Catalog id count grows from today's 24 by the
tier count; DESIGN-GATE's status row and `status-ssot.md` §9 move in the same change (decisions.md
Status SSOT row, already amended to say so).

Each tier's stat block is a fixed, non-rolled container — `data/seed/dungeon/_containers/wound.v1.json`,
copying `nerve.v1.json`'s shape verbatim (`{schemaVersion, kind: "container", entries: [{id, stage,
stat: {channel: {increased: n}}}]}`), loaded by the same `StatusStatPayload.TryParse` parser
`NerveContainer.Load` already calls — no new parser, no new file format.

### 2. `WoundPolicy` — the live projection + the anti-spiral constructor check

New `src/FusionRpg.Core/Delve/Attrition/WoundPolicy.cs`, modeled field-for-field on `NervePolicy.cs`
(co-located because delve is where the live projection is consumed first, exactly like `NervePolicy`
itself):

```csharp
public sealed class WoundPolicy
{
    // ctor(StatusCatalog catalog, IReadOnlyList<string> tiers,
    //      IReadOnlyDictionary<string, IReadOnlyList<StatusStatMod>> modsByTier)
    // — validates every tier's stat block against DerivedStatChannels.ResourceRegen("hp")
    //   (DerivedStatChannels.cs:512) the identical shape NervePolicy.cs:68-74 checks against
    //   spirit's own regen channel: a wound tier that touched hp's own regen would be the one
    //   true spiral this correction exists to name (F6). No tier authored today reads or needs
    //   to read that channel — the check exists so a FUTURE authored tier cannot silently create
    //   the spiral, not because today's containers are suspected of it.
    // — confirms every tier id is already registered: catalog.GetRequired(WoundStatusIds.For(tier))
    //   (NervePolicy.cs:76's own pattern) — this ctor never calls catalog.Register itself.

    // Sync(StatusRuntime runtime, string hostPtr, int tier, DateTimeOffset now)
    // — identical shape to NervePolicy.Sync (NervePolicy.cs:92-127): on a tier CHANGE, ClearGrant
    //   the old grant, Apply the new tier AttackerLess: true with FixedStatusRng(0) and
    //   BaseDuration: 0 (persists until the next Sync/cure, never a timed decay — the exact
    //   comment at ExhaustionPolicy.cs:127). -1 = no wound at all, matching Nerve's sentinel.
}
```

This is a direct copy of a shipped, tested shape — the only new content is which channel the ctor
guards (`resource.regen.hp` instead of `resource.regen.spirit`) and which container it reads from.

### 3. Grading — a pure function, separated from where the tier is applied

`WoundGrading.TierFor(long hpLost, long maxHp, IReadOnlyList<long> thresholdsMilli)` — new,
`src/FusionRpg.Core/Delve/Attrition/WoundGrading.cs`, pure, no store, no RNG. Shape: mirrors
`NerveLadder.StageFor`'s highest-threshold-met scan (`NerveLadder.cs:15-22`) but reads a per-mille
%-lost ratio instead of a stack count, reusing the exact `long`, widen-before-multiply, per-mille
arithmetic already shipped for `PhaseGrant` (`BattleModels.cs:211`'s own `hp * 1000 < HpThresholdMilli
* maxHp` — this module's check is `checked(hpLost * 1000 / maxHp)` compared against `thresholdsMilli`,
same shape, same widen-first-divide-last discipline).

**OWED, not decided here.** The exact conversion from a graded tier into "how much worse the tier's
stat block is" — the ideal doc's `base + %lost × scale` — is a private curve as written until it is
bound to an existing potency/power read and given a reviewed `ssot-power-scale.md` §10 row (map's own
"Open items"; confirmed this session — §10 has no such row). This module ships the *structure* (a
closed tier ladder read off a per-mille %-lost threshold table) and explicitly does not ship that
binding. Until the §10 row lands, `wound.v1.json`'s tier stat blocks are flat, authored-by-hand
containers with no `Θ` term — power-neutral not only at apply time (already locked by F5) but in the
whole tier ladder, exactly like `nerve.v1.json` today.

Two call sites feed `TierFor`, both at settlement, never inside `StatusRuntime.Apply`:

**a) Delve non-permadeath settlement** (`RpgStore.Delve.cs:830-856`). Alongside the existing
`stage`/`afflicted` read (line 834-835), add a sibling read: `hpLost = maxHp - member.Pools["hp"]`
(`member.Pools` already carries `hp`, asserted present by `PartyPoolsCarry`), `maxHp` resolved through
the same registered seam every other pool-max read already uses (`ResourceChannelReader.Max(derived,
"hp")`, `ActorResourcePools.cs:25` — never a private read), fed to `WoundGrading.TierFor`. This is
**additive** — `ExtractionSettlement.Decide`'s own signature and its three-way `Roster`/`Recover`/
`Retire` branch (`ExtractionSettlement.cs:34-64`) are **unchanged**; the wound grade is written
independently of `settlement.Outcome`, in the same loop, on the same connection. `DelveMemberState`
gains a new field, `WoundTier` (`int`, -1 default), directly beside `NerveStacks` — a genuinely new
addition to that record, not present today (`DelveMemberState.cs:18-25`).

**b) Lawn die** (`RpgStore.UniqueActors.cs:733-790`, `TryRecoverActiveByPtr`) — **wiring gap, named
not solved by this spec.** The `plant.die`/`zombie.die` capture payload carries no HP signal today
(§What already exists, Wiring gap). This module's first implementation task is a new field on that
**existing** outbound event — the injector's die hook already has Unity-side access to the specimen's
last-known HP and its `MaxHp` through the existing lawn Unity write surface
(`EntityStatWriter`/`UniqueBoundLoadout.cs:21-71`, read-only here) — never a new write path, only two
new fields (`hp`, `maxHp`) on an event that is already emitted. This spec does not pretend that field
already exists.

### 4. Worsening counter — durable, per-specimen, ticks on settlement

New table `rpg_unique_actor_wound`, sibling of `rpg_unique_actor_recovery`
(same file, `RpgStore.cs`, next to `:547-554`):

```sql
CREATE TABLE IF NOT EXISTS rpg_unique_actor_wound (
  instance_id TEXT PRIMARY KEY,
  tier INTEGER NOT NULL,
  untreated_settles INTEGER NOT NULL DEFAULT 0
);
```

`tier` is the currently-graded `wound.*` index; row absence means "no wound." `untreated_settles` is
the counted clock — **advances by exactly one on every settlement event this specimen itself goes
through** (lawn die/board-end recovery, delve room clear, delve extraction) where the wound has not
been cured since the last settle. This mirrors `DecrementAllRecoveringForPlayerUnlocked`'s own
"R6 — virtual time, no clock" discipline (`RpgStore.UniqueActors.cs:441-445`'s own doc comment: *"every
`CloseDelve` decrements EVERY `Recovering` row this player owns... in the same transaction"*) — except
counting **up**, per-specimen, on that specimen's own settle, not down per-player on every `CloseDelve`
regardless of whose row it is.

At a tunable `wound.worsenAtSettles.{tier}` threshold, the tier advances one step (re-`Sync`s the live
projection through `WoundPolicy.Sync`) or, at the top tier, calls `RetireUniqueActorUnlocked` — the
**same** function hardcore permadeath already calls (`RpgStore.Delve.cs:849`), reused exactly the way
its own doc comment invites (`RpgStore.UniqueActors.cs:354-357`) — no second Retire path, no new
"is dead" field.

**Named honestly, not solved here:** today only a delve's `CloseDelve` reaches a single dispatch point
that could tick this counter for a delve-recovering specimen. A lawn-only wounded specimen (back in
`Roster`, never re-entering a delve) has no equivalent recurring event — its next settlement is
whenever it next deploys and returns, which may be a long real-time gap or never. This is the
deliberate consequence of "never wall time," not an oversight: a specimen kept in Roster indefinitely
never worsens. `wound.worsenAtSettles` values are a play-feel question for the owner once this module
is built, not an architecture question this spec resolves.

### 5. Recovery clocks wired — and the one explicitly not

- **Delve-counted `Recovering`** — reused unmodified. A graded `Recover` outcome already writes
  `rpg_unique_actor_recovery` (`RpgStore.UniqueActors.cs:415-435`); this module's own
  `rpg_unique_actor_wound` row is written in the **same transaction**, decremented/advanced alongside
  `DecrementAllRecoveringForPlayerUnlocked`'s existing pass.
- **Priced ritual** — reused unmodified. `TryPerformRecoveryRitual` (`RpgStore.UniqueActors.cs:521-523`)
  already prices via `SoulSinkPolicy.Price(risk.recoveryRitualSouls.{rung}, theta_run, tuning)`. This
  module's ritual clears **both** `rpg_unique_actor_recovery` and `rpg_unique_actor_wound` in the same
  transaction — never a cure that clears one and leaves the other.
- **World-turn legion rest — not wired by this module.** §What already exists, Real gap.

### 6. What does not change

`ActorHub` compose is untouched — this module only changes which `Statuses` a `BattleActorSetup` is
seeded with, through the same already-registered seam `deploy-carry` already proved (its own "What
does not change" section). No new Unity **write** — the injector reads existing HP fields to populate
two new fields on an event it already emits. `WorldEntityMember`/troop `Hp-Wounds` arithmetic is
untouched. `ExtractionSettlement.Decide`'s existing three-way branch and its shipped tests are
untouched — this module adds a parallel read at the same call site, never a change to that function's
signature or behavior.

## Tunables

Every number this introduces, and which `data/tuning/` file owns it. No `const` balance numbers.

| Number | Owner | Notes |
|---|---|---|
| `wound.*` tier count / ids | `data/tuning/status-catalog.v{n}.json` (injected catalog) + `StatusCatalogBootstrap.cs` (migration shim) | Starting shape 3, mirrors `nerve.*`'s 3 |
| `wound.{tier}` stat containers | `data/seed/dungeon/_containers/wound.v1.json` | Mirrors `nerve.v1.json` verbatim; flat until the §10 binding lands (§3) |
| Tier thresholds (%HP-lost per-mille cutoffs) | `data/tuning/deployment-hierarchy.v1.json` (new domain file, already named by the ideal doc's own Tunables table) | **OWED exact values** — bound to the §10 row, not authored here |
| `wound.worsenAtSettles.{tier}` (untreated-worsening clock) | same new domain file | A play-feel number, owner-set once this module is built |
| `risk.recoveryRitualSouls.{rung}` | **existing, reused unmodified** | `DungeonTuning.cs:273-274`, `dungeon.v1.json:297-307` |
| `risk.downedRecoveryDelves` | **existing, reused unmodified** | `dungeon.v1.json:298` |

## Numeric types

`hp`, `maxHp`, `hpLost` are `long` (existing `ActorResourcePools`/`BattleModels` contract — no new
magnitude type). Threshold and worsen-at-settles values are per-mille `long`, widened before
multiplying, divided last (`checked(hpLost * 1000 / maxHp)`, matching `PhaseGrant`'s own shape,
`BattleModels.cs:211`). Tier index is `int`, bounded by a small closed list (mirrors `NerveLadder`'s
`int stage`) — never itself a magnitude. `untreated_settles` is `INTEGER` in SQLite / `int` in C#: a
**count of settlement events**, structurally bounded by tier count × threshold, never scaled by `Θ` —
exempt from the no-hard-ceilings rule as a structural counter, not a magnitude, and this comment is
the required exemption note (CLAUDE.md "Caps"). No `float`, no `double`, no `System.Random` — matches
`ExhaustionPolicy`/`NervePolicy`'s own `FixedStatusRng(0)` discipline.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Attrition.Wound"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Status.Catalog"     # registry-equality test
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve"              # full delve suite, no regressions
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~UniqueActor"
.\scripts\guard-actor-hub.ps1        # no new composer introduced
.\scripts\guard-dal.ps1              # every new SQL string stays in RpgStore.*.cs
.\scripts\guard-funnel-delta.ps1     # unaffected — this module writes no HP delta
python scripts\audit-magic-numbers.py --targets M1   # wound tuning stays out of code
python scripts\audit-overflow.py
```

## Structure

```
src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs      EDIT — add "9.7 Wound" block after "9.5 Nerve" (:62-68)
src/FusionRpg.Core/Delve/Attrition/WoundPolicy.cs         NEW — mirrors NervePolicy.cs field-for-field
src/FusionRpg.Core/Delve/Attrition/WoundGrading.cs        NEW — pure TierFor(hpLost, maxHp, thresholdsMilli)
src/FusionRpg.Core/Delve/Attrition/DelveMemberState.cs    EDIT — add WoundTier (int, -1 default) beside NerveStacks
data/seed/dungeon/_containers/wound.v1.json               NEW — mirrors nerve.v1.json shape
data/tuning/deployment-hierarchy.v1.json                  NEW — thresholds, worsenAtSettles (shared with later modules)
src/FusionRpg.Data/Sqlite/RpgStore.cs                      EDIT — add rpg_unique_actor_wound table beside :547-554
src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs         EDIT — wound row read/write beside :415-521; second
                                                            RetireUniqueActorUnlocked call site (worsened-to-death)
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs                EDIT — sibling wound grading read at :830-856
src/FusionRpg.Injector/GameHooks.cs                        EDIT — add hp/maxHp fields to plant.die/zombie.die
                                                            payload (:657-677) — read-only Unity access, no new write
tests/FusionRpg.Core.Tests/Delve/Attrition/                gains WoundPolicyTests, WoundGradingTests
tests/FusionRpg.Data.Tests/UniqueActors/                   gains wound-row + worsening-counter tests
UNTOUCHED: StatusRuntime.cs, ResistanceEvaluator.cs, ExhaustionPolicy.cs, NervePolicy.cs (read-only
  precedent), ExtractionSettlement.cs (signature/behavior unchanged), ActorHub.cs, WorldEntityMember,
  LegionSupply.cs
```

## Code style

A direct structural copy of `NervePolicy`'s constructor check, re-targeted at `hp`'s own regen channel:

```csharp
// The anti-spiral guard is opt-in per policy (F6) -- StatusRuntime/StatusCatalog enforce nothing.
// Mirrors NervePolicy.cs:68-74's own check against spirit's regen channel, exactly.
var hpRegenChannel = DerivedStatChannels.ResourceRegen("hp");   // DerivedStatChannels.cs:512
foreach (var tier in tiers)
{
    foreach (var mod in modsByTier[tier])
    {
        if (mod.ChannelId == hpRegenChannel)
            throw new ArgumentException(
                $"self-regen cycle: wound tier '{tier}' must not touch hp's own regen channel '{hpRegenChannel}'",
                nameof(modsByTier));
    }
    catalog.GetRequired(WoundStatusIds.For(tier));   // ids are pre-registered by StatusCatalogBootstrap, never here
}
```

## Testing strategy

- **`WoundPolicy` ctor rejects the spiral:** a tier stat block touching `resource.regen.hp` throws at
  construction — the exact regression shape `ExhaustionPolicy`/`NervePolicy`'s own tests already prove
  for their channels.
- **`WoundGrading.TierFor` boundary tests:** exact-at-threshold and one-per-mille-below, mirroring
  `PhaseGrant`'s own per-mille discipline; a threshold table round-trip mirrors `NerveLadder`'s tests.
- **Attacker-less regression:** a `WoundPolicy.Sync` call never varies with a fake "attacker" derived
  snapshot — pinned against `ResistanceEvaluator`'s own `attackerLess` branch (`ResistanceEvaluator.cs:302`),
  the same shape the existing `RedTest_...` suite already proves for nerve/exhaustion.
- **Cross-module proof (the concrete link to `deploy-carry`):** a member carrying a `wound.*` status
  through `deploy-carry`'s own two-room round trip (that module's Success criterion 5) fights measurably
  weaker in room 2 — proven once, read by both specs.
- **Worsening counter:** N settles without a cure advances the tier exactly once at the configured
  threshold, never partially or twice; a cure (ritual or a `Recovering` countdown reaching zero) clears
  both `rpg_unique_actor_recovery` and `rpg_unique_actor_wound` in one transaction — no half-cured row
  survives a crash-mid-write simulation.
- **Real-death trigger, shared with `corpse-cache`:** a worsened-to-death Retire and a hardcore-permadeath
  Retire both leave `rpg_unique_actors.phase = Retired` and nothing else distinguishes them — asserted
  directly, since module 3 depends on this being the single trigger (§Interface).
- **Catalog registry-equality test:** `9.7 Wound`'s ids match the injected `status-catalog.v{n}.json` —
  mirrors `9.5 Nerve`'s own such test (`status-ssot.md` §9's stated discipline).
- **Determinism:** `FixedStatusRng(0)` throughout — no `System.Random` anywhere in this module, matching
  `ExhaustionPolicy`/`NervePolicy`'s `ScriptedRng`.
- **`ExtractionSettlement.Decide` unchanged:** the existing delve golden/fixture suite for `Retire`/
  `Recover`/`Roster` stays byte-identical — this module's wound read is additive, asserted by running
  that suite unmodified.

## Boundaries

- **Always:** `AttackerLess: true` + `BaseDuration: 0` persistence pattern on every `wound.*` apply; a
  closed tier vocabulary registered in `StatusCatalogBootstrap.cs`, never at runtime; the ctor spiral
  check; reuse `RetireUniqueActorUnlocked` for a worsened-to-death Retire, never a second retire path.
- **Ask first:** touching the lawn die Unity write surface beyond adding two read-only fields to the
  outbound event; changing `NerveLadder`/`NervePolicy`/`DelveMemberState`'s existing fields (append
  only — `WoundTier` is additive); authoring tier threshold **numbers** before the §10 row lands
  (structure ships now, numbers wait); a world-turn recovery clock (real gap, out of this module).
- **Never:** a private `f(Θ)` tier-severity curve; a second status-count field on `StatusInstance`; a
  change to `ExtractionSettlement.Decide`'s signature or branch logic; touching `WorldEntityMember` or
  troop `Hp/Wounds` arithmetic; a second ActorHub composer or private derived fold.

## Success criteria

1. A specimen graded into a wound tier fights measurably weaker on its **next** deployment (map's G2),
   proven through `deploy-carry`'s own carry path — not just the current fight.
2. An untreated serious wound advances on its own settlement clock and can Retire the specimen with no
   wall-clock read anywhere in the path.
3. `WoundPolicy`'s constructor refuses a tier whose stat block touches `resource.regen.hp` — the
   anti-spiral guarantee is provably per-policy, not assumed from the runtime.
4. `ExtractionSettlement.Decide`'s existing tests and the full delve golden suite are unchanged.
5. `ssot-power-scale.md` §10 unchanged — no tier-severity row added without a reviewed follow-on.
6. `guard-actor-hub.ps1`/`guard-dal.ps1`/`guard-funnel-delta.ps1` green — no new composer, no SQL
   outside `RpgStore.*.cs`, no HP delta written by this module.
7. A worsened-to-death Retire and a hardcore-permadeath Retire are indistinguishable to any reader of
   `rpg_unique_actors.phase` — the single trigger `corpse-cache` depends on (§Interface).

## Interface exposed to dependents

**The exact trigger `corpse-cache` (module 3) must read for "a wound tier worsened to death."** This
module introduces no new "is dead" signal. It adds a **second caller** of the existing, idempotent
`RetireUniqueActorUnlocked` (`RpgStore.UniqueActors.cs:358-385`) — the same function hardcore-delve
permadeath already calls at `RpgStore.Delve.cs:849` — from the worsening-counter tick in §Design 4,
when `untreated_settles` crosses `wound.worsenAtSettles` at the top tier. `RetireUniqueActorUnlocked`
records no reason (verified by reading the whole function: it flips `phase` and clears
`match_key`/`last_ptr`/`deploy_correlation_id`, nothing else) and is idempotent on an already-`Retired`
row.

| Member | Consumer |
|---|---|
| `rpg_unique_actors.phase == UniqueActorPhases.Retired` (the phase transition itself — no new field, no reason column, no second signal) | `corpse-cache` (module 3) — this is the ENTIRE real-death trigger. A hardcore-permadeath Retire (`RpgStore.Delve.cs:849`) and this module's worsened-to-death Retire are the same write to the same column; `corpse-cache` never needs to distinguish their cause, matching the ideal doc's own framing that both are named as one trigger (`deployment-hierarchy-ideal.md` Drop §1: *"hardcore-delve `Retired`, or a wound tier that worsened all the way"*) |
| `wound.*` status riding `DelveMemberState.Statuses` / `BattleActorSetup.InitialStatuses` (deploy-carry's existing path, unmodified) | Any future consumer that wants to know a specimen is currently wounded reads the live `StatusRuntime` instance or `DelveMemberState.WoundTier` — never a new query surface |
| `rpg_unique_actor_wound(instance_id, tier, untreated_settles)` | `corpse-cache`/UI surfaces that need the CURRENT tier or the worsening progress — read-only for every consumer outside this module; the row is deleted on cure (ritual or Recovering-zero), same transaction discipline as `rpg_unique_actor_recovery` |
| `RetireUniqueActorUnlocked`'s own reuse precedent (`RpgStore.UniqueActors.cs:354-357`) | `corpse-cache` — if it ever needs a third caller (e.g. a future world-map death path), the same reuse shape applies: same open connection/transaction, no new function |

## Design-gate checklist

```
[x] Subsystems: Status (Core), delve attrition (Core), unique-actor store (Data), status catalog
    (Core) — no ActorHub, Item, or World subsystem touched by this module.
[x] Read this session: deployment-hierarchy-ideal.md §"Death, injury tiers, and who dies for real"
    (including its F5-power/F6-spiral Corrections), Prior Art (Battle Brothers, XCOM); deployment-
    hierarchy-map.md row 2, Prerequisite P1, Wave scope, "Open items"; decisions.md "Status SSOT"
    (amended 2026-09-13) and "Deployment hierarchy SSOT (2026-09-13)" rows, read in full, not
    summarized; spec-deploy-carry.md in full (this module's own dependency's Interface row);
    status-ssot.md in full (§3, §9, §9.5, §9.6, §11); ssot-power-scale.md §10/§11 (searched for an
    existing injury/wound row — none found).
[x] Code cited by file:line, opened this session: StatusRuntime.cs (:138, :218-298, :370-439,
    :443-470, :497-501), ResistanceEvaluator.cs (:270-309), StatusCatalogBootstrap.cs (:62-68),
    ExhaustionPolicy.cs (:1-147, spiral check :54-66, :88, :99-136, :127), NervePolicy.cs (:1-176,
    spiral check :63-74, :92-127, :150-175), DelveMemberState.cs (:1-25), NerveLadder.cs (:1-23),
    RpgStore.cs (:535-564), RpgStore.UniqueActors.cs (:354-385, :400-521, :700-790), RpgStore.Delve.cs
    (:820-874), ExtractionSettlement.cs (:1-98), PermadeathGate.cs (:1-30), SoulSinkPolicy.cs (:1-43),
    DungeonTuning.cs (:270-277), dungeon.v1.json (:297-307), BattleModels.cs (:42, :120-215, :247-248,
    :495-512), ActorResourcePools.cs (:1-55), DerivedStatChannels.cs (:512), GameHooks.cs (:650-677),
    LegionSupply.cs (full file, :1-220), nerve.v1.json (full file).
[x] Drift reported: the ideal doc's own design section names "world-turn legion rest" as a recovery
    clock for this module (map row 2); `LegionSupply.cs`, the file the design gestures toward, is a
    loam-only mechanism with zero HP/wound logic — no code precedent for a wound recovering over world
    turns exists today. This module does not build one; the gap is named, not silently assumed solved.
    `PermadeathGate.cs` lives at `Delve/Difficulty/`, not `Delve/Attrition/` as a casual reading of the
    ideal doc's own citation style might suggest — verified by locating the file directly.
[ ] The exact hp/maxHp fields to add to the plant.die/zombie.die payload, and which existing Unity
    read supplies them without a new write, was not pinpointed to a specific `EntityStatWriter` call
    this session — named as this module's own first implementation task (§Design 3b), not asserted as
    already solved.
[ ] Tier threshold numbers, tier count beyond the starting shape of 3, and worsenAtSettles values are
    explicitly NOT authored by this spec — owed to the ssot-power-scale.md §10 follow-on and an owner
    balance pass, per the map's own "Open items."
[x] No §2 invariant contradicted: no cap on a magnitude (untreated_settles is a structural count,
    commented as exempt), no private f(Θ) (the tier curve is explicitly deferred, not invented), SQL
    only in `RpgStore.*.cs`, no second ActorHub composer, Foundation/PvZ untouched (one new read-only
    field pair on an existing outbound event, no new Unity write).
[x] ActorHub gate: this module neither contributes nor consumes a new Hub channel — `maxHp` grading
    input reads the same already-registered `ResourceChannelReader.Max` seam every other pool-max read
    already uses.
```
