# Spec: wild-room

Status: **DRAFTED 2026-09-05 (wave 4) — written against the working tree and the twelve approved specs; unbuilt, not yet
approved.** Every `file:line` below was opened this session; drift is named in place and collected in §Drift. Every number
is a starting shape so the system runs, never a balance decision.

Module id `wild-room`, row 13 of the [party-dungeon map](../party-dungeon-map.md) (`:123`; wave 4, `:140`). Depends on
`event-deck` (`talk` rows, `story` on `wild` rooms, the `remembers` outcome row — `spec-event-deck.md:203-209, :424`),
`delve-battle-profile` (`Withdraw`, the second `Retreated` producer, `ThetaActor`, the trace — `:177-191, :193-207`),
`dungeon-loot` (`DelvePrices.PullPrice/OfferFloor`, `SpendUnbanked`, the at-risk ledger — `:178-199, :212-219`). Reads
`dungeon-registries` (`disposition.v1.json`, `wild.*`, `capture.*`, `altar.*` — `:77, :137-140, :146`), `dungeon-seed-contract`
(`dispositionBase` — `:68`), `encounter-generator` (`wild` = `pack` at `Θ_room`, enemy `Level = θ_enemy` — `:96, :251-253,
:446`), `delve-attrition` (`pools["spirit"]` — `:419`), `supplies-and-objects` (`pray`, the cage seam — `:181, :187-188, :211`),
`loot-pack` (no cell — `:19`), `delve-scope` (`parties_json`, `decisions_json` — `:72-73`). **External, gating:** the item-cost
row on actions (A3) and the `consumable` kind (D27) — map `:97`. Gate **G4** (`:160`). Format: [standalone/spec-expeditions.md](../standalone/spec-expeditions.md).

## Objective

The runtime half of recruit, capture and summon inside a delve, built so that no path into the roster is ever cheaper than
the altar. A `wild` room is a `pack` encounter with a **disposition**; before the fight the party may **talk** — a fixed verb
set, one answer per step, every roll on a named stream; a demon that `joins` is minted at `θ = Θ_room + thetaOffset`,
teleports home at once and binds if a slot is free (decision 12). **Capture** is a corpus action on a weakened enemy;
success withdraws the target alive, pays no `KillEarn`, mints the same way. The **altar** is one pull on the shipped
`SummonRoller` from unbanked souls, delivered at extraction. The **cage** is a recruit offer with no fight. **Remembers**
is a read over the delve log.

Success looks like: a `wary` pack at `Θ_room 70` is offered souls at `OfferFloor(70) = PullPrice(70) × 1500 / 1000` from
the unbanked ledger, draws `joins` on `dungeon:wild:{r}:{c}:1`, mints a `θ 83` demon with `Origin = "delve"` bound into a
free slot, and replays byte-identically; a capture at 22 % hp on the battle's `capture` stream withdraws the target, the
report shows `Retreated`, `KillsFrom` pays nothing; an altar pull advances the player's one pity row and is a haul row
until `CloseDelve(Extracted)` mints it, forfeited on a wipe; every battle, expedition, world and summon golden holds.

## Locked anchors

- **R5 (ideal `party-dungeon-ideal.md:1751`), verbatim:** *"The offer floors at the altar pull price at the room's Θ via
  `SoulSinkPolicy`, paid from unbanked souls; spirit, supply and released-contract offers priced as equivalents. The bind
  stays free; teleport-home stands. Altar pulls are **at-risk haul on the delve ledger, delivered at extraction** (decision
  12 named recruits and captures only)."*
- **Decision 12 (§11.9 box, `:1694-1698`):** *"Recruited and captured demons teleport home at once, bound if a contract slot
  is free — not a pack cell, not a party slot. Never at risk in the delve, never usable in it. The §11.6 guard against
  'recruit beats fight' therefore rests entirely on the costs (no `KillEarn`, no XP, a seal or an offer spent) and on the
  encounter roll's rarity shape … the spec must keep both."*
- **Review §1(e) (`audit-2026-09-05.md:107-123`), quoted:** *"talk's EV is ≈5× fight's before the offer is priced, and the
  doc prices the offer as a fraction of room yield — cents … **(1)** the souls offer's floor is the altar pull price at the
  room's Θ through `SoulSinkPolicy`, paid from unbanked souls — the offer *is* the bind price, the bind stays free … `takes
  and leaves` is a real loss and always-talk stops dominating by construction. **(2)** Altar pulls … at-risk haul … **(3)**
  Capture pays no `KillEarn` … and spends a seal … **(4)** … draw from the home roster via a released contract."* (S2-3, `:213`.)
- **Seed → concrete (ideal §10 laws 1–2, `:782-796`):** ordinals never a magnitude; structural rolls reuse
  `SeededRng.DeriveStream` + `WeightedChoice`; *"Never a second roll implementation."* **Decision 13 (`:1699-1708`; seed
  contract `:124`):** *"granted by id … never categorically"* — a cage occupant and an altar pool are drawn by the shipped roller.
- **Ideal §11.6 (`:1381-1425`):** *"a talk, not a coin"*; *"the answer raises a band, never sets the outcome"*; capture is *"an
  action in the corpus, not a verb on the engine"*, *"integer per-mille … No exponent, no float"*, *"a per-target ramp"*,
  *"no cross-delve pity"*; *"pity shared with the Sanctum altar."* **Map row 13:** `Θ_party` is *"the commander's composed
  `Θ_actor` … never a mean"* (audit N9, `:237`).

## Design

### 1. The wild room and disposition

A `wild` archetype carries `dispositionBase` (VALIDATED, voted — `eager · open · wary · hostile`, seed contract `:68`;
vocabulary `disposition.v1.json`, registries `:77`) and an `encounterRef` of formation `pack` (`:69`). **The disposition is
the room's, not the species'**: 0 of 841 species anchors carry a `disposition` or `temperament` field (counted from
`data/seed/demons/species/` this session; the anchor schema has neither).

`DispositionCatalog` gives the band its ordinal `0..3` (`eager` = 0). The **effective band** is the base plus five one-step
shifts in `{−1, 0, +1}` (positive = toward `hostile`): the rung's `wildDispositionShiftRungs`; the Δ band's
`wild.deltaShiftRungs[band]`; the offer's preference (§2); `remembers` (§8); the stance verb (§2) — clamped to `[0, 3]`, an
index into a four-member registry, exempt and commented. The pack is `Encounter.Build(...)` at entry whether or not a fight
follows, so `leave` cannot re-roll it (`spec-encounter-generator.md:60, :284-290`); each enemy's `θ` is its
`BattleActorSetup.Level` (`BattleModels.cs:14`). `HypnoAlly` (`DemonRarity.cs:41-45`) is a lawn expression, never read here.
**Θ_party** is the commander's composed `Θ_actor` via `IPowerIndexProvider.ActorIndex(StatContext)` (`IPowerIndexProvider.cs:15`)
— `dungeon-loot` §1's read, never a mean; `Δ = θ_pack − Θ_party` (`θ_pack` the highest enemy `Level`), banded on
`wild.deltaBands[]` (four signed Θ edges → `far-below · below · even · above · far-above`). The actor-side composition is
the ladder spec's wiring gap (`:198-208`); until it lands `Θ_party` reads the commander's specimen index — stated, not defaulted.

### 2. Talk verbs and eligibility

At most `wild.talk.maxSteps` steps (2): an optional **stance**, then an **offer**, `fight` or `leave`. Every step is one
decision `{seq, kind: "talk", partyIndex, payload: {surface: "wild", roomId, speciesId, verb, outcome?}}`
(`spec-delve-battle-profile.md:141-145`; `surface` discriminates from the deck's `event`, `spec-event-deck.md:207-209`).

| Verb | Eligible when | Band effect | Then |
|---|---|---|---|
| `flatter` | step 1 | coin on `…:{seq}`: `NextPerMille() < wild.talk.flatterMilli` → −1, else +1 | step 2 |
| `threaten` | step 1; **refused** at `far-above` | −1 when Δ band ≤ `even`, else +1 | step 2 |
| `offer:souls` | unbanked ≥ `OfferFloor(Θ_room)` — a host ledger check, not a leaf (`spec-supplies-and-objects.md:181`) | preference (§3) | outcome draw |
| `offer:spirit` | the offering member's `pools["spirit"]` ≥ the equivalent (§3) | preference | outcome draw |
| `offer:supply:{tag}` | `HoldsStock(Self, tag-bearing supply, 1)` over the pack (`PredicateNode.cs:32`; `spec-event-deck.md:227`) | preference | **refuses `delve.price-undesigned` in v1** (§3) |
| `offer:contract` | a releasable bound demon at home whose equivalent ≥ the floor (§3) | preference | outcome draw |
| `fight` · `leave` | always | — | `DelveBattle.Run` on the drawn pack · room consumed, no souls |

**Capture-only species never `join` by talk**: a pack carrying `DemonAcquisition.CaptureOnly` (`DemonRarity.cs:32-38`) offers
no `offer:*` — the expedition coin already excludes them (`ExpeditionResolver.cs:238-243`); capture (§5) is their path. **No
binding slot** refuses every `offer:*` before any soul moves (`wild.no-slot`), read as `CountBoundContractsUnlocked <
ContractPolicy.Capacity(purchasedSlots)` — the mint's own auto-bind test (`RpgStore.Contracts.cs:100-101`; `ContractPolicy.cs:171`).

**The outcome draw.** Row = `wild.outcome.{effectiveBand}.{joins,takesLeaves,flees,attacks}Milli` (sums to 1000,
loader-checked, registries `:137`), drawn with `WeightedChoice.Pick` (`WeightedChoice.cs:25`; `Weight` is `int`, `:6`) on
`dungeon:wild:{r}:{c}:{seq}` (ideal `:1382`), `rollSeed` the stream's first `NextULong()` (`SeededRng.cs:26-29`). `joins` →
debit and §4 mint; `takesLeaves` → debit, no mint (§1(e)'s real loss); `flees` → no debit, one `essence.{element}`
(`ExpeditionResolver.cs:133-136`); `attacks` → the fight, no re-talk. A `craves` offer never guarantees `joins` (`ideal:1395`);
the one guarantee is `offer:supply:{wild.provisionOverrideTag}` (`bait`, registries `:74`) forcing `joins` without a draw —
inert until supply prices exist (§3).

**Autopilot** answers `fight`; under `wild.autopilot.rule = leave-hostile` it answers `leave` when the band after the rung's
shift is `hostile` — the rung speaks through its shift, no rung bool. Closed ids; unknown refuses at load. Autopilot never
offers. **Personality** for the talk is `ContractPolicy.PersonalityFor("dungeon:wild:{r}:{c}")` (`:195-196`). **Drift:** the
ideal wants it *"recorded on the mint"* (`:1384`), but a minted demon's is `PersonalityFor(instanceId)` over a fresh `Guid`
(`RpgStore.Demons.cs:45`) with no column — v1 accepts the mismatch; a mint override is filed on `demon-system-map.md`, ask-first.

### 3. Offer pricing and the floor

Every offer resolves to a **soul equivalent**, accepted only when `≥ OfferFloor(Θ_room)`:

```text
PullPrice(Θ_room)  = SoulSinkPolicy.Price(banners[altar.bannerId].costPerPull, Θ_room, power)   dungeon-loot §6; SoulSinkPolicy.cs:40-41
OfferFloor(Θ_room) = PullPrice(Θ_room) × wild.offer.soulsMilliOfPullPrice / 1000                 ≥ 1000‰, loader-checked (registries :138)
```

`costPerPull` is `BannerTuning.CostPerPull` (`SummoningTuning.cs:5`, parsed `:56`; `summoning.v1.json:10-11` — 100 / 120),
never a soul number in `dungeon.v1.json` (S2-10). Both functions are `dungeon-loot`'s `DelvePrices` (`:394`), called, never re-derived.

| Offer | Equivalent (`long`) | Debited from |
|---|---|---|
| `souls` | `OfferFloor(Θ_room)` — the floor **is** the price | `SpendUnbanked(delveId, price, "wild:{r}:{c}")` (`spec-dungeon-loot.md:214`); no overdraft (P6) |
| `spirit` | `OfferFloor × 1000 / wild.offer.spiritPerSoulMilli` spirit units | `TrySpend` on `pools["spirit"]`, all-or-nothing (`ResourcePoolState.cs:64-78`); a spend, not harm — **no nerve stacks** (attrition §4's table is closed); `ExhaustionPolicy.Sync` after |
| `supply:{tag}` | the supply's DERIVED price at `Θ_room` | `pack.drop{by: use}` (`spec-loot-pack.md:79`) — **wiring gap:** item-side price DERIVED, none built (`spec-dungeon-loot.md:193-195`); refuses `delve.price-undesigned` until it lands |
| `contract` | `ContractPolicy.RitualPrice(rarity, Θ_room, power) × loyalty / LoyaltyMax` (`ContractPolicy.cs:161-166`; `contracts.v1.json:51-62`), widen, divide last | `ReleaseContract` (`RpgStore.Contracts.cs:331-368`, its blockers `:353-360`); the demon stays owned, unbound — a real sink; its freed slot is what the recruit binds into |

**Why the floor sits above the pull.** A recruit mints at `θ_enemy` (§4) while every pull mints at the shipped `level 1`
(`RpgStore.Demons.cs:53`) — price parity alone would still favour the talk. The floor equalises price, the
`takesLeaves`/`flees`/`attacks` rows price the expectation, and `soulsMilliOfPullPrice` starts at **1500‰**, above the
minimum, so a wild room's expected value sits beside the altar's. A knob, settled by §Testing's EV property.

### 4. Recruit minting and teleport-home

On `joins` (or a cage `open` that joins), in the room-close transaction of `RpgStore.Delve.cs`: debit, then
`MintDemonUnlocked(db, playerId, spec, now, out newlyDiscovered)` (`RpgStore.Demons.cs:29-94`) — **the one mint every
acquisition uses** (summons `RpgStore.Summons.cs:104`, expeditions `RpgStore.Expeditions.cs:344`, fusion `RpgStore.Fusion.cs:218`):
the `Roster` row, the profile, the codex upsert and **the free auto-bind** (`:85-88` → `AutoBindNewSpecimenUnlocked`,
`RpgStore.Contracts.cs:85-105`: a free slot binds at `bindLoyalty 300`, `contracts.v1.json:12`; capacity full writes nothing).
Teleport-home is that write — `Roster` at once, never in the pack or a party (decision 12). No cell, no `KillEarn`, no XP
(`spec-loot-pack.md:19`; `spec-dungeon-loot.md:72-77`).

**Drift — no shipped capture path exists.** `Origin` lists `capture` (`spec-demon-core.md:29`) and it is never written (zero
hits under `Core/Demons/` and `RpgStore.Demons.cs`; `demon-capture` is *"later"*, `demon-system-map.md:65`); the map row's
*"path the shipped capture uses"* is `MintDemonUnlocked`, whose `DemonMintSpec` (`DemonDtos.cs:58-70`) has **no `Level`** and
whose INSERT hard-codes `level 1` (`:53`). **Filed on `demon-system-map.md` (`demon-core`), additive:** `long? Level` on the
spec, `$level = spec.Level ?? 1` — null is today's line for every caller. The recruit's spec: identity from the pack's
`ConcreteSpecies` row (`ConcreteSpecies.cs:15-68`), `Rarity = BaseRarity`, `TraitIds = SummonRoller.RollTraits(species, rarity,
rng)` on `dungeon:wild:{r}:{c}:traits` (`SummonRoller.cs:183-198`, *"shared by summons and wild joins"*), `Origin = "delve"`
(`"capture"` for §5), **`Level = θ_enemy = Θ_room + thetaOffset`** — the sum `encounter-generator` §3 writes on the setup
(`SpeciesExpander.cs:66-67` is the same shape over the species base).

### 5. Capture action and chance bands

**The action.** `act.capture` is a corpus `ActionRow` (`ActionRow.cs:15-90`): `Kind = Skill`, `Relation = Enemy`
(`ActionTargetSpec.cs:80`; `RelationKind.cs:15`), `Mode = Single`, `ConditionsJson` compiled through E3 (`ActionCompiler.cs:
74-100`) to `And(HpBelowMilli(Target, capture.usableBelowMilli), HoldsStock(Self, seal.*, 1))` (`PredicateNode.cs:26, :32`),
**a seal as its cost** — `ActionCostRow` is resource ids only (`ActionRow.cs:122-123`), so the item-cost row is external A3,
gating (map `:97`); the action ships behind the `CrossProgramLandedFlags` shape (`spec-supplies-and-objects.md:324`). It is
the **second code-backed action after `act.attack`** (`BattleEngine.cs:551-555`) — `CaptureAction.Resolve` lives in the
action layer; the runner's id → resolver row is a one-line ask on `action-map.md`. **Deviation from the map row, stated:**
its gate `hasStatus` needs a *"has any status"* leaf and `HasStatus` takes one named id (`PredicateNode.cs:64`); status
enters the **chance** as a count band; a named-id gate is ask-first (a leaf is a reviewed reader change, `:4-5`).

```text
hpBand    = index of target hp‰ in bands.hpBand.{low,half,high}.milli                event-deck's registry row (:135)
deltaBand = index of (θ_target − Θ_caster) in wild.deltaBands[]                       θ_target = setup.Level; Θ_caster = setup.ThetaActor ?? setup.Index (battle-profile §7)
deltaBand = clamp(deltaBand + capture.sealTierShiftBands[sealTier] + attempts(target) × capture.failStepBands, 0, 4)   index rail, exempt
chance‰   = capture.chanceMilli[hpBand][deltaBand] + capture.statusBonusMilli[countBand(live statuses)]             long
success   = (long)CaptureRng.NextPerMille() < chance‰
```

`countBand` is `lone · few · several · many` over the target's live `StatusRuntime` instances; `none` → 0. **Bands shift
indices, never a number from a model:** the seal's tier (`t1..t3` from its `powerBand` ordinal) shifts Δ toward `far-below`;
each failed attempt on the same target shifts it toward `far-above` — the ramp against spam (`ideal:1422-1423`), kept in a
per-battle `CaptureAttempts` ledger beside `CooldownLedger` (`BattleRunState.cs:112`). **Drift:** registries spell
`capture.sealTierShiftMilli[]` (‰, `:139`); a seal shifts an index, so the key is `capture.sealTierShiftBands[]` (int) — filed.
**The stream:** `CaptureRng = SeededRng.DeriveStream(seed, "capture")`, one line beside `EssenceRng`/`RidersRng`
(`BattleRunState.cs:194-199`), trace-wrapped as `crit` is (`:191-192`) — the battle's own seed, never a second RNG, never
`atom.proc` (the atom layer's, `AtomRandom.cs:31`). Deriving draws nothing, so no shipped battle moves.

**Success** → `state.Withdraw(target)` — the three lines `CheckRetreats` sets (`BattleRunState.cs:671-673`: `Retreated`,
`Status.WithdrawEntity`, `Shields.RemoveAll`), lifted by battle-profile §6: alive, no `die` event, `Active` false
(`BattleEngine.cs:63, :68`), skipped by `KillsFrom` (`spec-dungeon-loot.md:73-75`). The success is the `act.capture`
decision's result on the trace; at room close the host mints every `(Retreated && captured-in-trace)` enemy as §4 with
`Origin = "capture"` — a coward's retreat has no trace row and mints nothing. **Failure** → seal spent, attempt counted,
turn used; a second attempt on a withdrawn actor is refused by targeting before any roll. No cross-delve pity (`ideal:1424`).

### 6. Altar pulls as at-risk haul

`pray` on an altar (`spec-supplies-and-objects.md:181`) is **one pull on the shipped roller**: `SummonRoller.Roll(banner,
focus, count: 1, pity, rng)` (`SummonRoller.cs:61-81`; `count` must be 1 or 10, `:64` — v1 sells single pulls, a ten-pull is
ask-first). `banner = SummonBannerCatalog.TryGet(altar.bannerId)` (`:44`) — exactly two ids exist, `standard-rift` and
`element-focus` (`:17-18`, `:30-34`; `summoning.v1.json:10-11`), so `altar.bannerId` names one or a third row enters through
`publish.py summoning` plus one `Of(...)` line. Starting shape `element-focus`, `focus = domain.climate` — the domain-focus
banner (`ideal:1428-1429`; `PickWeighted`, `:162-181`). `altar.poolFromDomain` is loaded (T5) and **inert at `false`**:
`RollSpecies` pools the whole summonable catalog (`:152-153`); a domain pool is *"one filter argument, not a new roller"*
(`ideal:1373`) — an optional trailing `Func<DemonSpeciesDef, bool>? poolFilter` on `Roll`, null = today's line, filed.

**Price** `PullPrice(Θ_room)` via `SpendUnbanked` in the same transaction (dungeon-loot §6 *altar pull*). **Pity rides:**
`ReadPityUnlocked`/`WritePityUnlocked` on `rpg_summon_pity` (`RpgStore.Summons.cs:94, :150, :197-215`) are per player and
cross-banner (`spec-demon-summoning.md:24`), so the altar reads and writes the Sanctum's one row. The `rng` is
`dungeon:altar:{r}:{c}:{n}` off the delve seed, `n` the pull ordinal at that altar — replay-safe, never `PullSummon`'s
`Guid`-rolled `rngSeed` (`:29`). **The result is haul, not a demon:** the `SummonRollResult` (`SummonRoller.cs:17-21`) is
written as `parties_json[p].haul[] += {kind: "pull", speciesId, rarity, variant, traitIds, r, c, n}` (`spec-delve-scope.md:72`)
— **no `UniqueActor` and no phase** until `CloseDelve(Extracted)`, where `RpgStore.Delve` calls `MintDemonUnlocked` per row
(`Origin = "delve"`, `Level` null — a pull is a pull), the free auto-bind and discovery souls as the summon path pays them
(`RpgStore.Summons.cs:118-125`). `Wiped` drops the rows with the haul (`spec-loot-pack.md:88`); the pity advance and the
spend stand — the pull happened, only delivery was at risk (R5). No pack cell. No phase ask is needed.

### 7. The cage

A cage is a `building` `RoomObject` in a `wild` room (`spec-supplies-and-objects.md:158`; `open` reserved `:187-188`).
**Which wild rooms hold one is a structural draw**, the discipline `delve-graph-roll` uses for gates and secrets (`:117-127`):
at first entry, `NextPerMille() < wild.cageMilli` on `dungeon:wild:{r}:{c}:cage` makes the room a cage room — no pack is
seated, the object is projected, `rpg_delve_rooms.resolved_kind = 'cage'` records it (event-deck's column,
`spec-delve-scope.md:85`; `cage` filed as a legal value). The occupant is one species drawn on the same stream from the wild
pool (`WildBand`'s filter, `ExpeditionResolver.cs:238-243`: never `CaptureOnly`, never the top rung) at `θ = Θ_room +
thetaOffset`, `dispositionBase` shifted **one band toward `eager`** — a caged demon wants out; a rule, not a knob. `open` is
§2's tree **without `fight` and `threaten`**, same refusals, same mint, same memory. One-shot. Deterministic over
`(seed, r, c, tuning)`; no anchor field, no event row, no model.

### 8. Remembers

`WildMemory.For(decisions, speciesId)` is a pure read over this delve's `decisions_json` talk rows with `surface: wild` —
the log battle-profile §4a owns and event-deck's `remembers` outcome row writes into (`spec-event-deck.md:424`). The **most
recent** row naming the species decides: `joins` → −1 (toward `eager`); `fight`, `attacks`, or `fight` after any stance or
offer (a betrayal) → +1; `leave`, `flees`, `takesLeaves` → 0. **Exactly one band**, whatever the count — a rule with the
exemption comment. No table: the log is the memory and ends with the delve (SMT V's "I remember you", audit `:247`).

### 9. Refusals

Named rule ids (`ConsumableDef.cs:212-219` pattern), before any write, never a fallback: `wild.no-slot` (before any offer
verb is shown); `wild.souls-insufficient`; `wild.spirit-insufficient`; `wild.contract-not-releasable`; `delve.price-undesigned`
(a supply offer); `wild.disposition-unknown` (a band not in `disposition.v1.json` — a corpus refusal at import,
`DispositionCatalog.Get` throws); `wild.verb-not-offered` (`threaten` at `far-above`, `offer:*` on a capture-only pack,
`fight` on a cage); `wild.step-exhausted`; `capture.above-threshold` (the predicate refuses before the roll);
`capture.no-seal`; `capture.target-withdrawn`; `capture.not-landed` (A3 absent); `altar.banner-unknown`; `altar.count`.
Nothing clamps: `Price` throws through `ContentScale.Apply` (`spec-dungeon-loot.md:228`); the two index rails say so.

### 10. Determinism

Talk resolution is pure over `(dispositionBase, verb, offer, party state, delve seed, r, c, seq, rung, tuning, memory)`.
Streams `dungeon:wild:{r}:{c}:{seq}`, `…:traits`, `…:cage`, `dungeon:altar:{r}:{c}:{n}` are `DeriveStream` names off the sealed
delve seed, **filed on `delve-graph-roll`'s reserved list** (`:130-132`). Capture rolls on the battle's `capture` stream and
replays from `(setup_json, seed, decisions_json)`; the pull rolls on the roller's own `rng`, derived here. No `System.Random`,
`DateTime`, `Guid` or store read under `Core/Delve/Wild/`; `PersonalityFor` is the owned PRNG over a string
(`ContractPolicy.cs:196`). The answer is persisted before it is applied (event-deck §10's posture).

## Tunables

All in `data/tuning/dungeon.v1.json` through `dungeon-registries`' T5 loader; new keys enter there and through `publish.py`;
every value a starting shape. **Read:** `wild.outcome.*` (eager 600/200/150/50 · open 400/250/250/100 · wary 200/300/300/200
· hostile 0/250/250/500 as `joins/takesLeaves/flees/attacks`); `wild.deltaBands[]` (`[−15, −5, 5, 15]` Θ);
`wild.deltaShiftRungs[]` (`[−1, 0, 0, +1, +1]`); `wild.offerPreference.{loyal,stoic,proud,calculating,feral}.{souls,spirit,item,
demon}` (`craves · accepts · scorns` → −1/0/+1; keys = `contracts.v1.json:32-38`); `wild.offer.soulsMilliOfPullPrice` (1500);
`wild.provisionOverrideTag` (`bait`); `wild.tide.*` (`false`, loaded, unread — v1 off, `ideal:1387`);
`difficulty.rungs[].wildDispositionShiftRungs` (0 through `very-hard`, +1 from `nightmare`); `capture.usableBelowMilli` (300);
`capture.chanceMilli[hpBand][deltaBand]` (low `700/550/400/250/100` · half `450/350/250/150/50` · high `200/150/100/50/0`);
`capture.statusBonusMilli[countBand]` (`50/100/150/200`); `capture.failStepBands` (1); `altar.bannerId` (`element-focus`);
`altar.poolFromDomain` (`false`).

| New key | Unit | Owner | Starting shape |
|---|---|---|---|
| `wild.talk.maxSteps` · `wild.talk.flatterMilli` | steps int · ‰ long | this module via registries | 2 · 500 |
| `wild.autopilot.rule` | id ∈ {`fight`, `leave-hostile`} | this module | `fight` |
| `wild.offer.spiritPerSoulMilli` | spirit units per soul, ‰ long | **replaces** registries' `wild.offer.spiritMilli` (`:138`), a ‰-of-max with no soul equivalence | 2000 |
| `wild.cageMilli` | ‰ per wild room, long | this module | 150 |
| `capture.sealTierShiftBands[]` | bands int per seal tier | **renames** `capture.sealTierShiftMilli[]` (`:139`) | `[0, −1, −2]` |

**Not keys:** `altar.sharedPity` (a rule, registries `:140`); `altar.pullPriceSouls`; `wild.joinMilli`; a `remembers` size;
`wild.offer.soulsMilliOfRoomYield` (ideal `:1468`, superseded by R5). **Structural, commented:** the two index rails; the one-band memory.

## Numeric types

`PullPrice`, `OfferFloor`, every equivalent, spirit cost, `RitualPrice` and pool: **`long`** — `P(Θ)` magnitudes
(`SoulSinkPolicy.Price` returns `long`, `:40`; `ContentScale.Apply` widens then divides once; `× loyalty / LoyaltyMax` and
`× 1000 / spiritPerSoulMilli` widen first, divide last). Every `*Milli` and `chance‰`: **`long`**, compared against
`NextPerMille()`'s `int` widened; `WeightedOption.Weight` narrows to `int` after a range check (`WeightedChoice.cs:6`).
`Θ_room`, `θ_enemy`, `Θ_party`, `Θ_caster`, Δ, band indices, `seq`, `n`, steps, attempts, `Level`: **`int`**, `checked`
(`Level` widens at the `rpg_unique_actors.level` write). Loyalty and slots `int` (`ContractPolicy.cs:80-83`); seeds `ulong`
(`SeededRng.cs:15`). No `float`/`double`; `FocusWeightMultiplier` (`double`, `SummoningTuning.cs:5`) is the roller's, never read here.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Wild"                                  # goldens, properties, refusals
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Expedition|FullyQualifiedName~Summon"   # hashes and the roller untouched
dotnet test tests\FusionRpg.Data.Tests  --filter "FullyQualifiedName~Delve|FullyQualifiedName~Demon|FullyQualifiedName~Contract"
.\scripts\guard-dal.ps1 ; .\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-power.ps1
python scripts\audit-magic-numbers.py --domain dungeon ; python scripts\audit-overflow.py
```

## Structure

```
src/FusionRpg.Core/Delve/Wild/
  Disposition.cs   ordinal + five shifts, the commented [0,3] rail    TalkTree.cs       verbs, eligibility, Step(...) pure, autopilot
  OfferPricing.cs  four equivalents over DelvePrices (calls only)      WildOutcome.cs    the draw on dungeon:wild:{r}:{c}:{seq}
  RecruitMint.cs   DemonMintSpec builder — Origin, Level θ_enemy       CaptureAction.cs  act.capture resolver; CaptureChance; CaptureAttempts
  AltarPull.cs     one Roll on SummonRoller; PendingPull haul rows     Cage.cs · WildMemory.cs · WildRefusal.cs
src/FusionRpg.Core/Battle/BattleRunState.cs    → CaptureRng, one DeriveStream line beside RidersRng (:199)
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs    → talk/offer transaction; PullAtAltar; pending pulls minted in CloseDelve(Extracted)
src/FusionRpg.Server/DelveWildEndpoints.cs     → POST …/rooms/{id}/talk · …/pray · …/cage
tests/FusionRpg.Core.Tests/Delve/Wild/ · tests/FusionRpg.Data.Tests/Delve/
FILED, NOT EDITED HERE: DemonMintSpec.Level + RpgStore.Demons.cs:53 (demon-core); SummonRoller.Roll poolFilter (demon-summoning, inert);
  the runner's second code-backed action id (action-map); four stream names (delve-graph-roll); two key fixes + five keys
  (dungeon-registries); `cage` as a resolved_kind value (delve-scope); a personality mint override (demon-contracts, ask-first)
UNTOUCHED: SummonRoller's rates and pity (SummonRoller.cs:83-136), SummonBannerCatalog ids, LootPipeline, BattleEngine's round order
```

## Code style

Pure resolvers with seed and tuning as parameters — the `WeightedChoice`/`SummonRoller` voice; the host applies the record in
one `RpgStore.Delve` transaction; rejections name the rule.

```csharp
/// <summary>The soul equivalent of one offer, or a refusal. Every path ends in DelvePrices — never a price literal.</summary>
public static OfferQuote Quote(OfferKind kind, OfferFacts f, WildTuning t, PowerTuning power)
{
    long floor = DelvePrices.OfferFloor(f.ThetaRoom);                     // PullPrice × soulsMilliOfPullPrice / 1000, ≥ PullPrice
    return kind switch
    {
        OfferKind.Souls    => f.SoulsUnbanked >= floor ? OfferQuote.Souls(floor) : OfferQuote.Refuse("wild.souls-insufficient"),
        OfferKind.Spirit   => checked(floor * 1000 / t.SpiritPerSoulMilli) is var spirit && f.Spirit >= spirit
                                ? OfferQuote.Spirit(spirit) : OfferQuote.Refuse("wild.spirit-insufficient"),
        OfferKind.Contract => checked(ContractPolicy.RitualPrice(f.Released.Rarity, f.ThetaRoom, power) * f.Released.Loyalty
                                / ContractPolicy.LoyaltyMax) is var worth && worth >= floor
                                ? OfferQuote.Contract(f.Released.InstanceId, worth) : OfferQuote.Refuse("wild.contract-not-releasable"),
        OfferKind.Supply   => OfferQuote.Refuse("delve.price-undesigned"),   // item-side DERIVED price: none built yet
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>Integer per-mille, one table read. Bands shift indices; the roll is the battle's `capture` stream.</summary>
public static bool CaptureSucceeds(CaptureFacts f, CaptureTuning t, SeededRng captureRng)
{
    int hp = HpBands.IndexOf(f.TargetHpMilli, t.HpBandMilli);
    int delta = DeltaBands.IndexOf(checked(f.ThetaTarget - f.ThetaCaster), t.DeltaBands)
                + t.SealTierShiftBands[f.SealTier] + checked(f.AttemptsOnTarget * t.FailStepBands);
    delta = Math.Clamp(delta, 0, t.DeltaBands.Length);   // index into a five-band rail — structural bound, not a magnitude clamp
    long chance = checked(t.ChanceMilli[hp][delta] + t.StatusBonusMilli[CountBands.IndexOf(f.LiveStatusCount)]);
    return (long)captureRng.NextPerMille() < chance;       // int roll widened, never the other way
}
```

## Testing strategy

- **Goldens per disposition × verb:** 4 bands × 8 verbs at `Θ_room 70`, hashed over effective band, debited equivalent and
  outcome — 32, blessed once; one cage golden; one altar golden over the `SummonRollResult` and pity.
- **Properties (256 seeds):** `OfferFloor(Θ) ≥ PullPrice(Θ)` for Θ in `[1, 500]` and every `soulsMilliOfPullPrice ≥ 1000` (the
  loader refuses 999); **no offer cheaper than the altar** — every accepted equivalent `≥ OfferFloor`; **capture monotone** —
  `chanceMilli` non-increasing along Δ and non-decreasing as the hp band falls, over the table and the seal/ramp shifts;
  **recruit Θ** — every minted `Level == Θ_room + OffsetFor(threatBand)`; **EV** — over 256 talks per band a wild room's
  expected soul value does not exceed the altar's at equal `Θ_room` (settles `soulsMilliOfPullPrice`).
- **Capture:** a withdrawn enemy pays no `KillEarn`, `Retreated == true, Survived == true`; a coward's retreat mints nothing; a
  second attempt on a withdrawn actor is refused before any roll; a failure spends the seal and raises `attempts`; the
  `capture` stream is trace-wrapped and replays; `act.capture` absent from every offered set when no slot is free.
- **No-slot refuses before any soul moves:** a counting fake sees zero `SpendUnbanked`/`TrySpend`/`ReleaseContract` calls at
  full capacity; `flatter`/`threaten`/`fight`/`leave` still offered. **Altar:** pity advances exactly as `PullSummon` does for
  the same `(pity, rng)`; a haul row and no `rpg_unique_actors` row until `Extracted`; **forfeited on wipe** — zero rows, pity
  and spend intact; `Extracted` mints once, a replayed close mints nothing; `count 10` refused.
- **Remembers shifts exactly one band:** three fights against one species → +1; `joins` then `fight` → +1 (latest wins); an
  unrelated species → 0. **Autopilot:** `fight`; `leave` only under `leave-hostile` on a post-rung `hostile`; never an
  `offer:*` row. **Capture-only pack** offers no `offer:*`.
- **Untouched:** the four battle hashes, the 32-seed sweep, the four expedition tier hashes, the world goldens and the summon
  suite run in the same command — no engine branch, the `capture` stream derived and undrawn on every golden,
  `poolFilter`/`Level` null on every existing caller. **No clock, `Guid` or `System.Random`** under `Core/Delve/Wild/`
  (the `spec-turn-engine.md:138` scan shape).

## Boundaries

- **Always:** every price through `DelvePrices` → `SoulSinkPolicy.Price`; every offer at or above the floor; debit only on
  `joins`/`takesLeaves`; the slot check before the offer; one mint path, teleport-home at room close, the bind free; the pull
  on `SummonRoller.Roll` with the player's one pity row; pulls as haul until extraction; every draw on a named stream; every
  answer a `talk` row.
- **Ask first:** a named-status gate on `act.capture`; a ten-pull; a third banner row; `poolFromDomain: true` before a domain
  pool predicate exists; a `personality` mint override; a souls fallback for the seal before A3; a cage that fights; a spirit
  offer that adds nerve stacks; `wild.talk.maxSteps` above 2.
- **Never:** a second roller beside `SummonRoller` or a second pity stock; an offer below the altar floor; a free recruit or a
  bind that bypasses `AutoBindNewSpecimenUnlocked`; a battle-engine special case for capture (no `actionId == "act.capture"`
  under `Core/Battle/`); `HypnoAlly` read for anything; a wall clock; a number from a model; a recruit in a pack cell or a
  party slot; `KillEarn` or XP for a captured or recruited demon; `wild.joinMilli`, `altar.pullPriceSouls` or any soul literal
  in `dungeon.v1.json`; a `float` magnitude; SQL outside `FusionRpg.Data`.

## Success criteria (G4, `party-dungeon-map.md:160`)

1. The 32 talk goldens, the cage and altar goldens hold; the 256-seed sweep is green on the six first-ship domains.
2. `OfferFloor ≥ PullPrice` and *no offer cheaper than the altar* proven; the EV property holds at the shipped ‰. 3. A captured
enemy pays no `KillEarn`, shows `Retreated`, mints at `θ_enemy` with `Origin = "capture"`; a recruit mints at `Θ_room + offset`
with `Origin = "delve"`. 4. No-slot refuses before any soul moves. 5. An altar result is forfeited on a wipe and minted once
on extraction; pity moves as the Sanctum's would. 6. Remembers shifts exactly one band. 7. Battle, expedition, world and
summon goldens byte-identical; guards green; no M1 under `Delve/Wild`; no new overflow critical. 8. G4's 4-party raid sees
per-party talk rows and per-party pull haul with one shared pity.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `TalkTree.Offered(room, party, facts, tuning) → verbs[]` · `TalkTree.Step(...) → TalkStep { EffectiveBand, Quote?, Outcome?, Decision }` | `delve-stage` (verb labels, the band as a **name**, the quote as a soul/spirit label — never Θ or ‰), `RpgStore.Delve` (applies the record) |
| `AltarPull.Pull(room, party, pity, seed, n, tuning) → PullResult { Roll, NewPity, Price }` · `Cage.Resolve/Open` | `supplies-and-objects` (`pray` `:181, :363`; `RoomObjectBuilder` `:362` — the reserved `open` resolves here), `dungeon-loot` (the *altar pull* sink row), `delve-stage` (band-4 reveal) |
| `CaptureAction` (`act.capture` row + resolver) · `CaptureChance.Compute` | the action runner (filed row); `delve-battle-profile` (`Withdraw` caller #1, `:184-185`); `dungeon-loot` (`KillsFrom` reads `Retreated`) |
| `WildMemory.For(decisions, speciesId) → int` · `OfferPricing.Quote` | `event-deck` (its `remembers` row writes the shape this reads); `delve-attrition` (`pools["spirit"]` through `PartyState.Write`); `dungeon-loot` (the *recruit offer floor* sink row) |

## Drift found this session (report, not fixed here)

- **No `Demons/Summoning/` folder** — the files are `Demons/SummonRoller.cs`, `SummoningTuning.cs`, `SummonBannerCatalog.cs`;
  `SummoningTuning.cs:5` and `:56` are as the brief says. **`RpgStore.UniqueActors.cs`:** `TryRetireUniqueActor :189-222`
  matches; the W4 observer is `:224-268`, `RecoverToRosterUnlocked :316-336` (one line off) — the **lawn's** event path, not a
  demon-capture path. **`ApplyContractResults :459`** is `RpgStore.Contracts.cs:459-495`; `ContractPolicy.cs` has no such member.
- **Species anchors carry no disposition** (0 of 841; no schema field) — the owner is the archetype's `dispositionBase`.
  **No shipped capture** (§4). **Research:** `06-summoner-minion-fusion-rpg.md:272-285, :1070-1072` carries SMT's negotiation
  *inputs* and says no datamined formula exists; the ideal's §3.2/§11.6 numbers (`:1451-1460`) are the provenance.
- Registries' `wild.offer.spiritMilli` and `capture.sealTierShiftMilli[]` are the two key-shape corrections in §Tunables;
  `party-dungeon-map.md:123` speaks of the capture path as if shipped.

## Design-gate checklist

```
[x] Subsystems: demon summoning (roller, banners, pity), demon contracts (slots, loyalty, release), demon core (mint, origin),
    actions (rows, relation, predicates, cost rows), battle kernel (streams, Retreated), soul economy (SoulSinkPolicy),
    party dungeon (registries, loot prices, attrition pools, event log), tunables.
[x] Read this session, in order: party-dungeon-map.md (row 13, G4, external deps :97-99); the twelve APPROVED specs in full;
    ideal §0, §3, §4.7-4.9, §8 box, §10, §11.6 in full, §11.9 box, §11.10 R1-R12; audit §1(e), S2-3, S2-9, S2-10, §4 N9/R-series,
    §5 #3, §7; spec-expeditions.md (format); decisions.md :113-116.
[x] Every code claim cites file:line opened this session (SummonRoller, SummoningTuning, SummonBannerCatalog, SoulSinkPolicy,
    WeightedChoice, SeededRng, AtomRandom, PredicateNode, DemonRarity, DemonSpeciesCatalog, ConcreteSpecies, SpeciesExpander,
    ContractPolicy, RpgStore.Contracts/.Demons/.Summons/.Expeditions/.UniqueActors, UniqueActorDtos, DemonDtos, ActionRow,
    ActionTargetSpec, ActionCompiler, RelationKind, BattleRunState, BattleEngine, BattleModels, ExpeditionResolver,
    ExpeditionEndpoints, IPowerIndexProvider, four tuning files, three demon docs, research 06). Drift in its own section.
[x] Verified against CODE, not comments: the roller's Summonable filter and count guard; the mint's level 1 and auto-bind; the
    capacity test; the pity read/write; the three Withdraw lines; the wild pool filter; PersonalityFor's key derivation; the
    zero-hit grep for a written "capture" origin; the anchor count. Surrounding sections read for every quoted rule (R5 with
    S2-3 and §1(e); decision 12 with its box; §11.6 whole; dungeon-loot §6-§7; supplies §4-§7).
[ ] Constraints not tested — nothing was run; this spec changes no code. "Goldens untouched" is argued from a
    derived-but-undrawn stream, null-default additive fields and a delve-only host; the suites are the first build task. The
    EV property's 1500‰ is a shape, not a measured number.
[x] No §2 invariant contradicted. Three readings added and named: status is a chance bonus, not a gate (§5); recruits and
    captures mint at θ_enemy while pulls mint at the mint's default (§3, §6); the cage is a per-room structural draw (§7). Two
    wiring gaps named as gaps: supply prices (item-side) and the seal cost row (A3).
[x] Propagations landed 2026-09-05 (verification pass): registries carries the two key corrections and the four new keys;
    delve-graph-roll reserves the wild and altar streams; delve-scope lists `cage` as a `resolved_kind` value;
    demon-system-map.md and action-map.md carry the filed rows (`DemonMintSpec.Level`, `poolFilter`, personality
    override, `act.capture`).
```
