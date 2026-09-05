# Spec: delve-attrition

Status: **APPROVED by the owner 2026-09-05 (wave 2) — unbuilt.** Depends on `decisions.md`
row **"Status SSOT + Resource model — nerve (2026-09-05)"** (`decisions.md:115`, the map's P3). Written
against shipped code the same day; every `file:line` was opened this session, and where the map's row
drifted from the code the drift is named in place.

Module id `delve-attrition`, row 8 of the [party-dungeon map](../party-dungeon-map.md) (`:118`; wave 2,
last of the wave, `:138`). Depends on `delve-battle-profile` (the `delve` profile row, the explicit
`Resolve(profile:, intentSource:)` call, its golden-safe additive `BattleActorSetup` fields — `:117`),
`difficulty-ladder` (`RungTable.Get`, `PermadeathGate.Applies` — `spec-difficulty-ladder.md:355-357`),
`delve-scope` (`parties_json`, `Warband` entities, `CloseDelve` — `spec-delve-scope.md:72, :94-96, :272`)
and `dungeon-registries` (`attrition.*`, `risk.*`, `rest.*`, `bands.hazardBand.*`, `difficulty.rungs[]` —
`spec-dungeon-registries.md:125, :131-132, :139, :142`). Gate: **G3** (`party-dungeon-map.md:159`).

## Objective

Everything a demon carries between rooms, and what happens when it runs out. A room fight today starts
every actor at `Hp = setup.MaxHp` (`BattleEngine.cs:29`) with no pools (`ActorState`, `:23-82`);
`resource-hub-ssot.md` §11 says pools *"persist across a run and refill at rest"* and no run has needed
that until this one. This module constructs the six pools per party member from persisted state, hands
them into each room's battle and reads them back; charges hunger per room and lets it bind between rests
and across delves; turns spirit drain into the staged `nerve` status; makes the `rest` room the only
refill; wires the timeline FSM's `Downed` into a delve fight; and settles the consequences —
`downedOnce`, Retired on a permadeath rung, Recovering **in delves** below it, the priced ritual, the
wipe, loyalty — at **one assessment point: extraction**.

Success looks like: a solo delve enters row 3 with hunger at 61 % and two `nerve.unsettled` demons,
rests on row `N−1`, extracts, and comes home with hunger at 74 % (not refilled), one demon `Recovering`
for two delves, loyalty applied once — and every battle golden byte-identical, because nothing here runs
unless the profile row says so.

## Locked anchors

- **Decision 1 (ideal §8 box, `party-dungeon-ideal.md:690-697`):** *"priced time plus souls by default,
  AND permadeath on rungs `very-hard` and above, per domain, tunable … A downed demon on a permadeath rung
  is `Retired` at extraction; below it, it is `Recovering` for a tunable count of **delves**
  (`risk.downedRecoveryDelves`) … with the `−10` loss and the soul ritual as the priced escape."*
- **Decision 4 (`:700-706`):** *"`spirit` is the nerve meter, realised as a stackable, staged status …
  spirit drain applies stacks of a `nerve` status whose stages are the Darkest Dungeon affliction ladder
  (unsettled → shaken → afflicted, names tunable), each stage a container of atoms."*
- **R3 (`:1750`), verbatim:** *"`downedOnce`: on a permadeath rung a demon downed at any point is
  `Retired` at extraction even if revived — the revive lets it finish the run, not escape the rule. A wipe
  Retires the whole party and drops the haul; the named mitigation is cheap replacement at the pull price."*
- **R6 (`:1753`), verbatim:** *"Virtual time only — a downed demon sits out a tunable number of delves
  (`risk.downedRecoveryDelves`). The real-time clock is removed. Owner: 'this game is not a paywall game;
  we don't limit players by a stamina system like some cheap mobile game.'"*
- **`decisions.md:115` (P3), quoted:** *"**`spirit` pays for nerve.** Spirit drain from horror curios,
  elite auras and boss presence applies stacks of a **staged `nerve` status** (stage names tunable;
  unsettled → shaken → afflicted as the starting shape), each stage a container of atoms, resolved through
  `StatusRuntime`. The shipped `StatusStacking` (`Refresh/Replace/Coexist`, `ResistanceEvaluator.cs:18-23`)
  is a re-apply policy, so a **stack counter with stage thresholds is a small new build** beside the
  `Counter` kind. `StatusCatalog` gains the `nerve.*` ids (the ADR-locked list grows by the stage count).
  `resource-hub-ssot.md` §2's normative 'pays for' column gains '`spirit` — essence cost; **and nerve**:
  drained by harm in a delve, restored only by rest and supplies, never by regen'."*
- **`resource-hub-ssot.md` §11 (`:266-268`):** *"Pools **persist across a run and refill at rest.** They
  are not per-encounter."* §10 (`:253-262`): exhaustion is a status, a container of atoms, never touching
  its own regen; *"`hp` is exempt: depletion is death, owned by the turn FSM's `Downed` state."* §8
  six-coverage (`:188-197`): *"`ResourceIds` is `{ hp, stamina, hunger, spirit, qi, poise }` and it is the
  only list."*
- **`decisions.md:42`:** *"`Downed` (HP ≤ 0 is veto-capable, never a terminal edge)"*; a mode is data, so
  every delve-only behaviour below hangs on a **profile field**, never a profile id.
- **Review** §1(f) (`audit-2026-09-05.md:125-138`): one assessment point; `Retired`'s second producer is
  *"an FSM semantics change the spec states"*. S1-1 (`:195`): victory souls once per delve — the shape
  loyalty mirrors. S2-6 (`:216`): rations `useContext: rest`. S2-7 (`:217`): *"hunger persists across
  delves … a delve is a run and home is not a rest."* S2-8 (`:218`): rituals priced on content Θ.

## Design

### 1. Party state — the per-demon record

`rpg_delves.parties_json` is one element per `PartyIndex` (`spec-delve-scope.md:72`); each gains
`members[]`, one record per demon, read and written only through `RpgStore.Delve.cs`:

| Field | Type | Written by | Read by |
|---|---|---|---|
| `instanceId` | string | entry | everything |
| `pools` | six `long` keyed by `ResourceIds` — `hp` **is** the battle's `Hp` (it rides `CurrentHp` into a fight, the other five ride `CarryInPools`) | entry, carry-out after every room/rest/curio | §2 carry-in, `delve-stage` meters |
| `statuses[]` · `shield` | `BattleStatusSpec` (`BattleModels.cs:152-153`) · `BattleInnateShield` (`:142`) | carry-out | carry-in via `InitialStatuses` (`:67`) / `InnateShield` (`:71`) |
| `nerveStacks` | `int` | §4 events, rest | §4 stage resolver |
| `downed` · `downedOnce` | bool · bool (set once, never cleared in-run) | §6 | room entry · §7 |

Two durable per-demon rows live **outside** the delve because they outlive it — and neither has a
`*_utc` column, because recovery is counted, never timed (§7):

```sql
CREATE TABLE IF NOT EXISTS rpg_unique_actor_pools (      -- FromStored's "persisted run-pool row"
  instance_id TEXT NOT NULL, resource_id TEXT NOT NULL, stored INTEGER NOT NULL,   -- long
  PRIMARY KEY (instance_id, resource_id));
CREATE TABLE IF NOT EXISTS rpg_unique_actor_recovery (
  instance_id TEXT PRIMARY KEY, player_id INTEGER NOT NULL,
  recovery_delves_left INTEGER NOT NULL, wounded_delve_id INTEGER NOT NULL, theta_run INTEGER NOT NULL);
```

### 2. Pools in a delve fight — construction, carry-in/out, exhaustion

**What exists.** `ActorResourcePools` holds all six pools array-indexed by `ResourceIds`
(`ActorResourcePools.cs:13-17`; `DerivedStatChannels.cs:521`). `CreateFull` (`:21-27`) seeds from
`resource.max.*` (`ResourceChannelReader.cs:16-17`); **`FromStored` (`:32-42`) seeds from a caller's map,
throws on any missing id, and has zero callers in `src/` — this module is the first.** `Resolve` is
`clamp(stored + rate × elapsed, 0, max)` (`ResourcePoolState.cs:14-25`); `TrySpend` (`:64-78`) is
all-or-nothing; `Add` (`:93-106`) is the signed delta `resource.delta` compiles to (`AtomCompiler.cs:283`);
`SettleAll` (`:113-123`) is the persistence shape, *"no clock attached."* `CostLedger` (`:51-65`) and
`PoiseLedger` (`PoiseLedger.cs:28-88`) are ledgers **over** these pools — the precedent for spending a pool
without owning one.

**Drift, reported.** `BattleRunState.cs:122` already declares a `LawnActorResourcePools ResourcePools`
(E28; `Combat/LawnActorResourcePools.cs:26-45`) keyed by **combat ptr**, `CreateFull` on first touch — and
nothing under `Battle/`, `Effects/` or `Combat/` reads it. The map's *"battle lacks the reference"* is exact
for `ActorState` and generous about the run state, which holds an unread, wrong-keyed registry. Not reused:
a delve member is keyed by `BattleActorSetup.Key` and must be seeded by `FromStored`.

**Carry-in.** `delve-battle-profile` owns the additive setup fields and lists this module's
`IReadOnlyDictionary<string, long>? CarryInPools` beside its `CurrentHp` (`spec-delve-battle-profile.md` §5:
`WhenWritingDefault`, null on every existing caller, the same goldens argument). **`CurrentHp` is the one hp
seat; `CarryInPools` carries the five non-hp ids.** When non-null, `BattleRunState` builds
`FromStored(carryIn + {hp: CurrentHp ?? MaxHp}, atTick: 0)` into `PartyPools : Dictionary<string,
ActorResourcePools>` keyed by actor key — the six-id contract holds and hp still has one owner.

**Carry-out.** After `Resolve`, `SettleAll(finalTick, derived)` per member → `members[].pools`;
`BattleActorResult.HpRemaining` (`BattleModels.cs:311-313`) is asserted equal to `pools["hp"]`. Between
rooms no tick advances, so the next room's `Resolve` at tick 0 returns `stored` exactly; regen exists only
inside a fight (`RunRegeneratorPulses`, `BattleEngine.cs:331`), and under P3 `resource.regen.spirit` is
contributed as 0 on the hub for delve actors — a channel contribution, not a branch.

**Exhaustion — built, wired here.** `ExhaustionPolicy` registers `exhaustion.{resourceId}`
(`ExhaustionPolicy.cs:7-14`, `:68-77`: `Debuff`, family `exhaustion`, `Refresh`, `ModifyStat`), refuses `hp`
(`:56-57`) and a self-regen cycle (`:62-65`); `Sync` (`:99-136`) writes on the transition only. The host
constructs one policy and calls `Sync` for **every id in `ResourceIds` except `hp`** — a `foreach` over the
list — at room entry, after every carry-out and after every rest. `IsExhausted` (`:88`) is the only gate.

### 3. Hunger — the supply meter

Charged **on room entry**, per member, from the archetype's hazard band, `long` throughout, widened
before the multiply, divided once:

```text
cost = maxHunger × bands.hazardBand.{archetype.hazardBand}.hungerPerMille × rung.hungerMilli / 1_000_000
```

`rest` and `boss` archetypes carry `hazardBand: none` (0 ‰); `rung.hungerMilli` is the economy column at
`spec-difficulty-ladder.md:236`. `pools.Add("hunger", −cost)`, then `Sync`.

**Hunger persists across delves (S2-7).** At `CloseDelve` (either state) each member's pools for the ids
in `attrition.persistAcrossDelves[]` are written to `rpg_unique_actor_pools`; the next entry's `FromStored`
reads them back and every other id starts at `resource.max`. The list's starting shape is `["hunger"]` — a
**tuning-declared, registry-validated subset**, never a code list; the loop still visits all six. This is
the shallow-farm throttle without a clock: two row-1 rooms then extract still spent the hunger.

**Rations are `useContext: rest` (S2-6).** `UseContext` today is `Menu · Dispatch · Battle · Lawn`
(`ConsumableDef.cs:43-58`); `supplies-and-objects` adds `Rest` and `Curio` (its §2). Hunger therefore **binds
between rests**: provisioning decides how many rests you can skip, not whether hunger matters.

### 4. Nerve — the staged status build (row P3)

**Why it is a build.** `StatusInstance` has no count (`StatusRuntime.cs:8-27`); `UpsertInstance`
(`:269-298`) implements `Refresh` (replace same id+grant), `Replace` (remove same id), `Coexist` (append) —
**a re-apply policy**. The `Counter` kind (`ResistanceEvaluator.cs:9`; `bond`, `StatusCatalogBootstrap.cs:27`)
is the closest shipped shape and still carries no count. So:

- **The counter lives in party state** (`nerveStacks`) and the status is its **projection**: at most one
  live `nerve.*` instance per demon; the count is never a status field.
- **Resolver** (pure): `NerveLadder.StageFor(stacks, spiritResolved, thresholds)` — the highest stage
  whose threshold `≤ stacks`, **or the top stage when `ExhaustionPolicy.IsExhausted(spirit)`**. Spirit is
  the pool the stacks read; exhausted spirit is `afflicted` whatever the count.
- **Sync** (`NervePolicy.Sync`, the `ExhaustionPolicy.Sync` shape): on stage change `ClearGrant` the old
  grant (`nerve:{hostPtr}`) and `Apply` the new stage attacker-less, `FixedStatusRng(0)`, `BaseDuration 0`;
  on no change, no write.
- **Catalog:** `nerve.unsettled`, `nerve.shaken`, `nerve.afflicted` in a new `// 9.5 Nerve (P3)` block of
  `StatusCatalogBootstrap` — `Debuff`, family `nerve`, `Replace`, `ModifyStat`; **21 → 24** (`status-ssot.md`
  §9, ADR row landed). Ids are `nerve.{stage}` per member of the `nerveStage` registry; a test asserts equality.
- **Each stage is a container of atoms:** `stat.derived` atoms compiled to `StatusStatMod(ChannelId, Op,
  Value)` lists (`StatusStatPayload.cs:14`) from `data/seed/dungeon/_containers/nerve.v1.json`; the self-regen
  check (`ExhaustionPolicy.cs:62-65`) runs against `resource.regen.spirit`.

**Drain events** — each adds stacks **and** drains spirit (`maxSpirit × ‰ × rung.spiritDrainMilli / 10⁶`):

| Event | Spirit ‰ | Stacks |
|---|---|---|
| entering an `elite` room | `attrition.spirit.perEliteMilli` | `attrition.nerve.stackPerElite` |
| entering the `boss` room | `attrition.spirit.bossPresenceMilli` | `stackPerBoss` |
| retreat (`Retreated`, the profile's second producer) | `attrition.spirit.retreatMilli` | `stackPerRetreat` |
| a horror curio outcome (`event-deck`, `resource.delta` on `spirit`) | the outcome's amount | `stackPerCurio` |

**Relief:** a rest removes `attrition.nerve.restRelief` stacks and restores spirit (§5); a shrine or
supply restores spirit through `resource.delta` and the ladder re-resolves. Nothing else — *"never by regen"*.

### 5. Rest — activations, heal, ambush

A `rest` room (the guaranteed row `N−1`, `spec-delve-graph-roll.md:106-107`; a hold point, `:183-185`)
grants each member `rest.activations` action uses. Camp actions are **corpus actions with `useContext:
rest`**, paid from the six pools through `CostLedger`, occupying the five equipped slots (`LoadoutSet.cs:40`
`MaxSize = 5`) — ideal `:1333-1338`; this module builds the activation counter and the heal, no camp system.

```text
heal(pool) = max(pool) × attrition.restHealMilli × rung.restHealMilli / 1_000_000   for pool ∈ rest.healsPools[]
```

`rest.healsPools[]` is validated against `ResourceIds` and the loop visits all six. `restHealMilli 750` on
`very-hard`+ (`spec-difficulty-ladder.md:105-106`) means **rest never refills to max what the rung
reduced** — the amount comes from the reduced ‰; only the pool's own `[0, max]` rail (a bounded quantity,
exempt and commented at `ActorResourcePools.cs:89-91`) stops it. Stacks drop by `restRelief`, then §4 re-syncs.

**Ambush** is an `event-deck` row drawn at `rest.ambushMilli` on the reserved stream `dungeon:event:{r}:{c}`
(`spec-delve-graph-roll.md:130-132`); the seam this module exposes is `RestOutcome.Ambushed`, the deck owns
the draw, and a `watch` action's status is read by its eligibility through `HasStatus`.

### 6. Downed and revive — the FSM, wired

**Drift, reported.** The ideal's §2.3 row (`party-dungeon-ideal.md:173`) says *"`Downed`/revive exists in the
timeline FSM, not in `Resolve`"* and counted `ActorTurnMachine` as unconstructed in the engine. `Downed` is a member of `TurnState` (`TurnState.cs:21-22`: *"HP ≤ 0 but still
present, targetable, and revivable. Death is a decision, not an edge"*); `ActorTurnMachine` (`:24`) is the
holder (`IsPresent` `:39`, `CanAct` `:47-48`); and **B38 already constructs one machine per actor**
(`BattleRunState.cs:125-141`). The gap is narrower: the engine transitions only `Ready/Committed/Resolving/
Recovering/Charging` (`BattleEngine.cs:402-470`, nine sites) and **never `Downed` or `Dead`**; death is still
`Alive => Hp > 0` (`:65`). The legal table already admits every state → `Downed` (`TurnState.cs:53-57`),
`Downed → Charging` (`:60`), `→ Dead` (`:61`), `→ Withdrawn` (`:69-71`).

**The wiring**, behind a new profile field `DownedOnDeplete: bool` (true on the `delve` row only; the
`RequiresLiveInput` shape, never `profile.Id == "delve"`; the row is `delve-battle-profile` §1's table):

- At death cleanup, an actor with `Hp ≤ 0` whose setup carries `PartyIndex` transitions to `Downed`
  instead of dying; `TriggerPhase.Fire`'s return is checked before any `Downed → Dead` (`TriggerPhase.cs:36-37`)
  and **a party member is never driven to `Dead` inside a room**. Wave actors keep today's path.
- `Active` for such an actor reads `machine.CanAct` (false while Downed): no turn, and `AnyActive("squad")`
  ends the fight when every party member is down.
- A downed member **stays downed for the rest of the room's fight and the delve** — `downed: true`, seated
  at the next room with `CanAct == false` — **unless revived**: a `revive`-class supply or corpus action
  fires `resource.delta hp + attrition.revive.hpMilli` of max and the machine takes `Downed → Charging`.
- The first `Downed` transition sets `downedOnce`.

### 7. downedOnce — permadeath, recovery in delves, the ritual

At `CloseDelve(Extracted)`, one transaction, per member with `downedOnce`:

- **`PermadeathGate.Applies(domain, rung, oath)`** (`spec-difficulty-ladder.md:145-154`) →
  `TryRetireUniqueActor` (`RpgStore.UniqueActors.cs:189-220`). **A second producer of `Retired`** — `:216`
  is the only write today, the release path. FSM change #1: `Roster → Retired` also fires on *died in a
  delve on a permadeath rung*. The `unique-actor-runtime.md` (`:91-125`) row owed is drafted in §Interface.
- **Otherwise** → `Recovering`, `recovery_delves_left = risk.downedRecoveryDelves`. FSM change #2: W4 writes
  `ActiveBound → Roster` in one step and *"`Recovering` is not an observable intermediate row"*
  (`unique-actor-runtime.md:121`); for a delve wound it becomes **durable**. Delve dispatch refuses it (the
  expedition soft-lock, `spec-expeditions.md:47`); `TryRetireUniqueActor` already refuses it (`:200-202`),
  so a wounded demon cannot be released until recovered — kept, named in Ask-first.
- **Recovery is virtual time (R6).** The counter decrements **inside the same `CloseDelve` transaction**
  of every later delve the player closes (Extracted **or** Wiped), for every recovery row of that player; 0
  flips the demon to `Roster` in the same write. **No `DateTime`, no `ElapsedDays`
  (`ContractPolicy.cs:187-191` is contracts' own wall-clock seam, never called here), no due stamp.** The
  audit's §5 #6(a) "settle on read through `ElapsedDays`" is the option the owner **refused** (`:286`, R6).
- **The ritual — the priced escape.** `POST …/recovery-ritual` spends `SoulSinkPolicy.Price(
  risk.recoveryRitualSouls.{rung}, theta_run, tuning)` (`SoulSinkPolicy.cs:40-41`; key at
  `spec-dungeon-registries.md:132`), `theta_run` being the wounding delve's deepest cleared room stored on
  the row (the S1-1 read); clears the row, writes `Roster`. `ContractPolicy.RitualPrice`'s shape (`:161-166`).

### 8. Wipe

A party **stands** while one member has `Hp > 0` and is not `downed`. When **every** party of the raid has
no standing member: `CloseDelve(Wiped)` — the haul is lost (`loot-pack`'s ledger), victory souls forfeited
(S1-1, `dungeon-loot`), and on a permadeath rung **every `downedOnce` member — on a wipe, all of them — is
Retired** in the same transaction; below the gate every member enters `Recovering` per §7. The named
mitigation is cheap replacement at the pull price at the pin — the Sanctum's price, not this module's.

### 9. Loyalty — once, at extraction

`ApplyContractResults(playerId, instanceIds, won)` (`RpgStore.Contracts.cs:459-495`) is called **once per
delve at `CloseDelve`** — the expedition precedent (`ExpeditionEndpoints.cs:184-190`, *"for the trip as a
whole, not per battle"*). Per-room credit would be S1-1 in a different currency.

**Victory, stated:** a member's delve is `won` when the raid extracted **and** (the boss was killed **or**
the party cleared at least half the rooms on its route) **and** the member is not `afflicted` at
extraction. Everyone else — wipe, shallow bail, top nerve stage — takes `ApplyLoss` (`ContractPolicy.cs:154`,
`lossPenalty 10`, `contracts.v1.json:17`). Ideal §4.6's *"spirit at zero at extraction = a second −10"* is
folded into this single call, so no demon is touched twice. `WinGain 15` (`:16`) rides `ApplyGain`'s daily
window (`ContractPolicy.cs:143-150`); that `day` (`RpgStore.Contracts.cs:464`) is contracts' own clock.

### 10. Determinism

Every drain and heal is `max × ‰ × ‰ / 10⁶` in `long`; every counter an `int`; every event keyed to a
room the graph fixed. No `System.Random`, no `DateTime.UtcNow`/`Now`; `StatusRuntime` takes the **battle's
virtual `now`** (the `DateTimeOffset` `now` the engine passes to `Status.Tick`, `BattleEngine.cs:320`), never the host clock.
Recovery is a counter. Same `(party state, room, rung, tuning)` ⇒ byte-identical new party state.

## Tunables

All in `data/tuning/dungeon.v1.json`; schema and T5 loader are `dungeon-registries`' (`:164-172`), so new
keys enter through that spec and `publish.py`. Every value is a starting shape.

| Key | Unit | Owner | Starting shape |
|---|---|---|---|
| `bands.hazardBand.{none,light,heavy}.hungerPerMille` · `attrition.spirit.{perEliteMilli,bossPresenceMilli,retreatMilli}` · `attrition.restHealMilli` | ‰ of max, long | registries `:125, :131` (read) | 0/40/90 · 100/200/150 · 500 |
| `difficulty.rungs[].{hunger,restHeal,spiritDrain}Milli` · `domain.permadeathFromRung` | ‰ long · rung id | ladder `:236-240` (read) | 1000 identity · `very-hard` |
| `risk.downedRecoveryDelves` · `risk.recoveryRitualSouls.{rung}` · `rest.activations` · `rest.ambushMilli` | delves int · souls long · uses int · ‰ long | registries `:132, :139` (read) | 2 · per rung · 3 · 330 |
| **new** `attrition.nerve.stageThresholds[]` | stacks int; length = `nerveStage` count, strictly increasing | this module | `[1, 3, 5]` |
| **new** `attrition.nerve.stackPer{Elite,Boss,Retreat,Curio}` · `attrition.nerve.restRelief` | stacks int | this module | 1/2/1/1 · 2 |
| **new** `attrition.revive.hpMilli` | ‰ of max hp, long | this module | 250 |
| **new** `attrition.persistAcrossDelves[]` · `rest.healsPools[]` | resource ids ⊆ `ResourceIds`, loader-checked | this module | `["hunger"]` · `["hp","hunger","spirit"]` |
| **new registry** `bands.v1.json` → `nerveStage` | enum | `dungeon-registries` | `unsettled · shaken · afflicted` |

No structural constants: every number above is a feel number; the pool rail is `ActorResourcePools`' own.

## Numeric types

Pools, drains, heals, ritual prices: **`long`** (`ResourcePoolState.Stored`, `:9`; `ResourceChannelReader`
rounds once at the boundary, `:16-20`); two ‰ factors multiplied then divided by 10⁶ once, in `checked`.
Stacks, thresholds, activations, the recovery counter, `PartyIndex`: `int`. `theta_run`: `int`. No `float`
or `double` here — `StatusStatMod.Value` (`StatusStatPayload.cs:14`) is the status layer's, read, never computed.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Attrition|FullyQualifiedName~Battle"  # + all battle goldens
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve|FullyQualifiedName~UniqueActor"
.\scripts\guard-dal.ps1 ; .\scripts\guard-funnel-delta.ps1
python scripts\audit-magic-numbers.py --domain dungeon ; python scripts\audit-overflow.py
```

## Structure

```
src/FusionRpg.Core/Delve/Attrition/
  DelveMemberState.cs      §1 record; PartyState read model        HungerCharge.cs        §3 (pure, long)
  PartyPoolsCarry.cs       FromStored in, SettleAll out, hp == pools["hp"] assertion
  NerveLadder.cs           StageFor (pure)                          NervePolicy.cs         Sync; container loader
  RestResolver.cs          activations, heal, restRelief, RestOutcome seam
  ExtractionSettlement.cs  §7-§9 decisions (pure): Retire | Recover(n) | Roster, won per member
src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs       9.5 nerve.* block (21 → 24)
src/FusionRpg.Core/Battle/BattleRunState.cs               PartyPools by actor key; Downed step under DownedOnDeplete
src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs   DownedOnDeplete (false on every shipped row)
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs               members[] writer; CloseDelve settlement + loyalty call
src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs        the two tables; recovery ritual
src/FusionRpg.Server/DelveEndpoints.cs                    POST …/recovery-ritual
data/seed/dungeon/_containers/nerve.v1.json · _registry/bands.v1.json (nerveStage)
tests/FusionRpg.Core.Tests/Delve/Attrition/ · tests/FusionRpg.Data.Tests/Delve/
```

`CarryInPools` on `BattleActorSetup` is filed on `delve-battle-profile`'s additive-field row.

## Code style

Pure resolvers with tick and tuning as parameters; ledgers over the shipped pools, never a second pool
type; rejections name the key; the `ExhaustionPolicy` voice.

```csharp
/// <summary>One demon between rooms. `Pools` carries all six — `hp` included — so the battle's Hp and the
/// pool have one owner. `NerveStacks` is the counter StatusRuntime has no field for (P3).</summary>
public sealed record DelveMemberState(
    string InstanceId,
    IReadOnlyDictionary<string, long> Pools,        // keys == DerivedStatChannels.ResourceIds, checked on write
    IReadOnlyList<BattleStatusSpec> Statuses, BattleInnateShield? Shield,
    int NerveStacks, bool Downed, bool DownedOnce);

public static class NerveLadder
{
    /// <summary>Highest stage whose threshold ≤ stacks; exhausted spirit is the top stage regardless. -1 = none.</summary>
    public static int StageFor(int stacks, long spiritResolved, IReadOnlyList<int> thresholds)
    {
        if (ExhaustionPolicy.IsExhausted(spiritResolved)) return thresholds.Count - 1;
        var stage = -1;
        for (var i = 0; i < thresholds.Count; i++) if (stacks >= thresholds[i]) stage = i;
        return stage;
    }
}
```

## Testing strategy

- **Six-coverage loop:** a carry-in map missing one id throws (`FromStored`, `:38`); the exhaustion, persist
  and heal loops each visit `ResourceIds.Count` ids — asserted against the list, never a literal 6.
- **Hunger:** charged once per room entry at band × rung; `rest`/`boss` charge 0; across two delves the
  second starts at the first's closing hunger with stamina at max.
- **Exhaustion:** hunger to 0 → `exhaustion.hunger` live, one apply (counted); above 0 at a rest → withdrawn.
- **Nerve:** stacks 0/1/3/5 → stage −1/0/1/2; spirit exhausted at 0 stacks → 2; a rest removes `restRelief`
  and the live id changes exactly once; catalog block equals the `nerveStage` registry; a stage container
  touching `resource.regen.spirit` is rejected at load.
- **Downed:** with `DownedOnDeplete: true` a party member at `Hp ≤ 0` is `Downed`, `CanAct == false`, still
  `IsPresent`; the fight continues while another stands; revive takes `Downed → Charging` at `revive.hpMilli`;
  a wave actor still dies. **Goldens:** all four battle hashes, the 32-seed sweep and the expedition tier
  hashes byte-identical with the field false on every shipped row.
- **Extraction:** `downedOnce` on rung ≥ gate → `Retired` (and the release path's idempotent return, `:198`);
  below → `Recovering` with the counter at `risk.downedRecoveryDelves`; two later `CloseDelve`s (one Wiped)
  → `Roster`; revived-then-extracted on a permadeath rung is still Retired (R3).
- **Ritual:** spends exactly `SoulSinkPolicy.Price(base, theta_run, tuning)` from the row, never the current
  delve; writes `Roster`; a second call refuses.
- **Wipe:** four parties down → `Wiped`; permadeath rung Retires all; below it all Recovering.
- **Loyalty once:** a counting fake store sees one `ApplyContractResults` per delve — `won: true` for standing
  members, `false` for an `afflicted` one; a wipe: one call, `false`; per-room calls: zero.
- **No clock — guard test:** no `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `.Now`, `Environment.TickCount`,
  `ElapsedDays` or `System.Random` under `Core/Delve/Attrition/` or in the `CloseDelve` settlement (the
  `spec-turn-engine.md:138` scan shape); `rpg_unique_actor_recovery` has no `*_utc` column.

## Boundaries

- **Always:** pools through `ActorResourcePools` (`FromStored` in, `SettleAll` out); exhaustion through
  `ExhaustionPolicy.Sync` over the whole `ResourceIds` loop; nerve through `StatusRuntime` with the counter
  in party state; every delve-only engine behaviour behind a profile **field**; settlement in one `CloseDelve`
  transaction; loyalty once per demon per delve; `long` and `checked` for every amount.
- **Ask first:** persisting more than `hunger` across delves; a fourth nerve stage; letting
  `TryRetireUniqueActor` release a `Recovering` demon; loyalty for a partial clear below half the route; a
  revive that clears `downedOnce` (that re-opens R3).
- **Never:** a wall clock, `ElapsedDays`, a due stamp or a scheduler for recovery; a hand-listed resource
  subset in code; a `nerve` pool or a seventh `ResourceIds` entry (spirit is the pool); per-room loyalty; `hp`
  exhaustion (depletion is `Downed`, `ExhaustionPolicy.cs:56-57`); a silent cap on `resource.max` or a
  silently floored cost; `Dead` for a party member inside a room; `profile.Id == "delve"` anywhere in
  `Battle/`; SQL outside `FusionRpg.Data`; an HP write that bypasses the funnel.

## Success criteria (G3, `party-dungeon-map.md:159`)

1. *"hunger binds between rests"* — the two-delve and rest-only-refill tests hold. 2. *"a downed demon sits
out N delves"* — the counter test holds with no timestamp anywhere. 3. *"a permadeath rung Retires a
`downedOnce` demon at extraction"* — including revived-then-extracted. 4. Battle goldens, the 32-seed sweep
and the expedition tier hashes byte-identical. 5. `guard-dal`, `guard-funnel-delta`, the no-clock guard and
`audit-magic-numbers --domain dungeon` green. 6. The `unique-actor-runtime.md` row is appended and
`status-ssot.md` §9 reads 24 ids.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `PartyState.Read/Write(delveId)` — `members[]` per §1 | `event-deck` (spirit/hunger `resource.delta` outcomes; `PartyDownedCount` leaf), `wild-room` (spirit offers debit `pools["spirit"]`), `delve-stage` (six pool meters, nerve stage id, downed) |
| `NerveLadder.StageFor` · `NervePolicy.Sync` · `nerveStage` registry | `delve-stage` (a stage *name*, never a number), `event-deck` (`HasStatus nerve.*` eligibility) |
| `HungerCharge.ForRoom(archetype, rung, maxHunger)` | `delve-stage` (preview on a `Glimpse`d room) |
| `RestResolver.Resolve(members, rung, tuning)` → `RestOutcome { Healed, Relieved, Ambushed? }` | `event-deck` (draws the ambush row when the seam says so) |
| `ExtractionSettlement.Decide(raid, rung, gate)` → per member `Retire | Recover(n) | Roster` + `won` | `RpgStore.Delve.CloseDelve` (the only writer), `delve-stage` (band-3 summary's wipe/permadeath notice) |
| `RecoveryLedger` — recovery rows; ritual price at `theta_run` | `dungeon-loot` (a `SoulSinkPolicy` sink), the Sanctum roster (a `Recovering` badge with delves left) |
| **`unique-actor-runtime.md` row owed** (drafted; that program appends it): *"party-dungeon `delve-attrition` (2026-09-05): `Retired` gains a second producer — extraction from a delve on a permadeath rung (`PermadeathGate.Applies`) retires a `downedOnce` demon; `Recovering` becomes a **durable** row for a delve wound, counted down in delves at `CloseDelve` (never timed), left by the counter reaching 0 or the recovery ritual. The W4 one-write note stands for lawn deaths. Spec: `party-dungeon/spec-delve-attrition.md` §7."* | — |

## Design-gate checklist

```
[x] Subsystems: resources/actor pools, status effects, battle/turns, unique-actor lifecycle, demon contracts,
    tunables, DAL boundary, party dungeon.
[x] Read this session: party-dungeon-map.md (row 8, P3, G3); decisions.md:42, :113-116; the five approved
    wave-1 specs; ideal §2.3-2.4, §4.5, §4.6, §8 box, §11.5, §11.10; audit §1(f), S1-1, S1-9, S2-4, S2-6/7/8,
    D15, §5 #6; resource-hub-ssot §2/§8/§10/§11; spec-action-costs §7; status-ssot §9;
    unique-actor-runtime :91-125; spec-expeditions (format); DESIGN-GATE §5.
[x] decisions.md checked: :115 enables this module; :42 locks Downed as veto-capable and "mode is data" —
    both honoured (profile field; TriggerPhase.Fire checked).
[x] Every claim cites file:line against CODE. Drift reported: Downed is TurnState.cs:22 (not
    ActorTurnMachine.cs); B38 constructs the machines (BattleRunState.cs:136-141) — the gap is that
    Downed/Dead are never entered (:402-470); HpRemaining is BattleModels.cs:311-313 (not :252-255) and
    BattleStatusSpec :152-153 (not :106-107); BattleRunState.cs:122 holds an unread ptr-keyed
    LawnActorResourcePools; ActorResourcePools.FromStored has zero callers in src/.
[x] Surrounding sections read for every quoted rule (hub §2 whole with its 2026-08-30 correction; §1(f)
    whole; R3/R6 in their table; ExhaustionPolicy whole file).
[ ] Constraints not tested — nothing was run; this spec changes no code. "Goldens do not move" is argued
    from the profile field being false on every shipped row and CarryInPools being WhenWritingDefault; the
    battle suite is the proof and the first build task. "CostLedger has zero production constructors" is
    the ideal's claim (:183), not re-grepped here.
[x] No §2 invariant contradicted. One reading added and named: spirit-zero at extraction counts as the
    member's loss inside the single loyalty call (§9), so no demon is touched twice.
[x] Corrections propagated 2026-09-05 (verification pass): the ideal's :173 row corrected; CarryInPools and
    DownedOnDeplete are rows in spec-delve-battle-profile.md §5/§1; the unique-actor-runtime.md §11 row and
    the status-ssot.md §9 note are appended; the new tuning keys are rows in spec-dungeon-registries.md.
```
