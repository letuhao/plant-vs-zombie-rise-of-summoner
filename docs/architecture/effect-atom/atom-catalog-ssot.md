# SSOT — the atom effect catalog

**Status:** Drafted 2026-08-22 from a six-way repo sweep. **This is the closed vocabulary.** It is the content input to **E1** (`atom-kind-registry`) and **E4** (`atom-schema`), and the corpus **E11** migrates and **E14** validates. Map: [../effect-atom-map.md](../effect-atom-map.md). Ideal: [../effect-atom-ideal.md](../effect-atom-ideal.md).

**The rule this document exists to enforce:** richness comes from **families × tiers × containers**, never from growing the kind list. Kinds are a reviewed change. Families and tiers are data. If a proposed effect needs a new kind, that is a design conversation; if it needs a new row, that is Tuesday.

---

## 0. ⛔ The vocabulary is closed and built. The POOL is empty. Do not confuse the two.

**Added 2026-09-02**, because this document reads as an inventory and was being used as one. Full
measurement: [`../../research/atom-effect-pool-audit-2026-09-02.md`](../../research/atom-effect-pool-audit-2026-09-02.md).

| | Built | Authored content |
|---|---|---|
| Kinds | **12** — all 12 have ≥1 atom, so every kind is proven executable end to end | — |
| Primary channels | **11** addressable | **1** addressed (`atom.fx-passive-atk-flat` → `atk`) |
| Derived channels | **267** addressable | **1** addressed (`atom.critical-hunter.t1` → `combat.crit.rate.omni`) |
| Atoms | — | **21**, in 17 families — FX demo rows plus one migrated trait |
| Containers | — | **6** |

**What selects an atom, and whether a model is involved.** Neither is ambiguous, and both get
mis-stated:

- **Selection is `Instantiator.Draw`** — a seeded weighted sample over `container.Pool`, with per-budget
  RNG streams and one-per-group exclusion. **Pure code. No model, ever.**
- **A model authors AFFIXES**, one step earlier: it picks a `name` and which **existing** atom ids to
  bundle. Never a magnitude, never a new atom id, `affix_class` derived not authored, both judgement
  fields 3-way voted.
- **The pool the model picks from is `data/seed/atoms/*.json` itself** — `generate_affixes.load_eligible_atoms`
  walks the shipped seed tree and derives each id the same way `AtomRow.DeriveId` does. **Built and wired**,
  proven by its own `--dry-run` (21 eligible atoms today). `--only` narrows it for a themed run.

**So the pool file exists and is read. What it lacks is rows.** The step that would fill it — emitting the
family library from the registry, deterministically — is model-free and unbuilt; `atom-family-library.md`
§2 already states the rule it should follow: *"do not hand-author what a pure function can generate."*

---

## 1. What the sweep corrected

Six parallel sweeps read the whole repo. Five counts we had been quoting were wrong, and every one of them had already propagated into the ideal, the adoption audit, or the map.

| Claim we made | Reality | Where it came from |
|---|---|---|
| "19 JSON fixtures" | **49** — 19 `effect-*`, 5 `combat-*`, 25 `status-*`, plus 15 golden plans | fixtures sweep |
| "10 FA opcodes" | **11** — `GrantShield` is shipped, unnumbered, absent from the FA1–FA10 doc table, and **not in the injector sink** (it executes bag-side in Core) | Foundation sweep |
| "13 battle traits" | **14** — a deliberate 7 FunnelRouted / 7 EngineBehavior split | battle sweep |
| "21 statuses" | **21 declared, ~13 functional** — see §5 | status sweep |
| "11 effect-shaped sites" | **12** — `ContractPolicy` carries rank bonuses, loyalty rates, and per-personality modifiers | battle sweep |

And one schema-level error that would have shipped into the atom spec:

> **The documented `channel` enum is fiction.** `effect-data.md` lists `hp, maxHp, atk, def, attackInterval, produceInterval, zombieSpeed, bulletDamage, boardPressure`. The real `StatChannels` are `hp, maxHp, atk, defense, arm1, arm1Max, arm2, arm2Max`. Four documented values are cheat-document keys that bypass the modifier bag entirely and **cannot be reached by an effect**; four real armor channels are missing from the doc.

---

## 2. The closed kind list — 12

Twelve kinds cover everything that has a working consumer today. Eleven map to a shipped opcode; `stat.derived` is the one addition, and it exists because four separate magnitude sites (patron, stars, injuries, contracts) already write derived channels with no opcode at all.

| # | Kind | Attach point | Maps to | Runtime support |
|---|---|---|---|---|
| 1 | `stat.modify` | stat | FA1 `ModifyStat` | lawn ✅ · battle ✖ (sink ignores FA1) · sim plan-only |
| 2 | `stat.derived` | stat | *(no opcode — direct channel mods)* | **✖ everywhere — quarantined** (D6): no opcode, no bag branch, no sink arm, and battle reads ChannelMods only from `TraitBattleCatalog`, never from a grant. Re-opens per runtime as consumers ship — **battle re-opens in E12**, which wires `BattleStatComposer` to read bound `stat.derived` atoms at squad build. E12 cannot bind `critical-hunter` until it does, so the re-open is part of that module, not a later favour |
| 3 | `resource.delta` | resource | FA10 `ApplyResourceDelta` | lawn ✅ · battle ✖ (D6) — battle's sink *does* consume FA10, but no **atom** can reach it: `BattleEngine` never grants and never calls `OnEvent` · sim plan-only |
| 4 | `resource.economy` | resource | FA9 `Economy` | lawn ✅ · battle ✖ · sim plan-only |
| 5 | `status.apply` | status | FA2 `ApplyStatus` | lawn ✅ · battle partial *(no FA2 path; setup only)* · sim plan-only |
| 6 | `status.clear` | status | FA3 `ClearStatus` | lawn ✅ · battle ✖ · sim plan-only |
| 7 | `shield.grant` | shield | `GrantShield` *(unnumbered)* | lawn ✅ *(Core bag-side)* · battle ✖ · sim ✖ (D6) — `ExecGrantShield` needs `Bag.ShieldGate`, set only by `FoundationHarness` and the injector's `EffectRuntime`. Sim is one line of wiring away |
| 8 | `spawn.entity` | board | FA4 `SpawnEntity` | lawn ✅ · battle ✖ · sim plan-only |
| 9 | `board.action` | board | FA5 `BoardAction` | lawn ✅ · battle ✖ · sim plan-only |
| 10 | `grid.spawn` | board | FA6 `SpawnGridItem` | lawn ✅ · battle ✖ · sim plan-only |
| 11 | `grid.clear` | board | FA7 `ClearGridItem` | lawn ✅ · battle ✖ · sim plan-only |
| 12 | `box.set` | board | FA8 `SetBoxType` | lawn ✅ · battle ✖ · sim plan-only |

**Five attach points:** stat · resource · status · shield · board. That list is the thing an ADR guards.

**`stat.modify` and `stat.derived` carry no trigger.** They are permanent modifiers; apply and revert are runtime lifecycle, not content — `OnGranted`/`OnRemoved` stay in the 7-trigger enum as runtime states no atom may author ([definitions.md](definitions.md) §14.2).

### Not kinds, on purpose

| Concept | Why not | Owner |
|---|---|---|
| Targeting, retreat, threat, aggro | decisions, not effects | AI layer spec |
| Loot and XP multipliers (`greedy`, `genius`) | reward math | rewards spec |
| Initiative and turn order (`swift`) | scheduling | turn kernel / action |
| Damage merge, order, mitigation | many sources into one hit | consumer/applier spec |
| Contagion spread | **not a kind** — it is overlay data on `status.apply` | this catalog |
| Damage riders | **not a kind** — a trigger plus `resource.delta` with an element payload | this catalog |

---

## 3. Trigger vocabulary — 8

| Trigger | LIVE signal | Note |
|---|---|---|
| `OnSpawn` | `plant`/`zombie`/`bullet` place, spawn, init | |
| `OnDamageDealt` | `combat.hit` via TakeDamage prefix + melee `AttackPlant` | **Gotcha:** overlay `filters.side`/`typeId` refer to the *damaged* entity, not the attacker |
| `OnDamageTaken` | `plant.damage` / `zombie.damage` | |
| `OnDeath` | `plant.die` / `zombie.die` | No `OnKill` — killer arrives as `actorIsKiller` |
| `OnGranted` / `OnRemoved` | grant lifecycle | **Not authorable** — runtime lifecycle only (§14.2). The bag injects the revert itself |
| `OnTimer` | injector hot-loop ms scheduler | **Exists in code and `effect-data.md`, absent from the trigger table in `effect-system.md`** — no FT number ever assigned |
| `OnActivate` | the actor's own decision to act | **Added by A18b** (`spec-on-activate-trigger.md`) — a reviewed cross-program vocabulary change, not a unilateral addition. Neither a board event (nothing has necessarily been damaged, spawned or killed) nor a lifecycle transition (the grant was bound earlier, possibly turns ago) |

`OnWave`, `OnMindControl`, and `OnHitLand` are **probed but not shipped** (§6).

> **Corrected 2026-09-02:** this section said **7** and omitted `OnActivate`. SSOT is `AtomTriggers.All`
> (`src/FusionRpg.Core/Effects/Atoms/AtomKind.cs`), whose length `AtomKindRegistry.TriggerCount = 8`
> mirrors as a structural constant. Note the section header counts **authorable + lifecycle** together:
> `OnGranted`/`OnRemoved` are in `All` but are runtime lifecycle states no kind may carry.

---

## 4. Channel vocabulary

### 4.1 Primary — 11, and only these

`hp` · `maxHp` · `atk` · `defense` · `arm1` · `arm1Max` · `arm2` · `arm2Max` · `attackInterval` · `produceInterval` · `zombieSpeed`

**SSOT is `StatChannels.All`** (`src/FusionRpg.Core/Stats/ModifierOp.cs:26`) — never this list. `AtomKindRegistry.PrimaryChannels` reads it rather than copying it, and rule **G6** refuses any `stat.modify` whose `channel` is not in it.

> **Corrected 2026-09-02.** This section said *"8, and only these"* with the last three marked *"growing to 11 … own spec, after the atom layer lands."* **That growth shipped (E16)** — all eleven are real composed channels today, which is what makes fire rate, sun rate and creep speed authorable at all. Measured in `docs/research/atom-effect-pool-audit-2026-09-02.md` §3.2.

Armor channels are zombie-only — that is a fact about which Unity fields exist, **not** about mitigation: elemental defense and the whole shield stack serve both sides.

`defense` never reaches a Unity *field*, but it does reach lawn damage — through the `TakeDamage` prefix (`StatMath.ScaleIncoming`). The catch is scope: the prefix reads **one side-wide cached value**, so entity-scoped defense atoms are impossible today (gap **G8**, §7). The Writer's direct lawn surface is 3 plant fields and 7 zombie fields.

Ops available to an atom: **`Flat` · `Increased` · `More`**. `Override` exists in the stat system but effects cannot emit it — that is a deliberate constraint, not an oversight.

### 4.2 Derived — 267 registered

**SSOT is `DerivedStatRegistry.CreateDefault().AllRegistered`** — 53 families in `data/seed/derived-stats/catalog.json`, expanded over their declared axis widths (`none`=1, `element`=7, `status-category`=4, `action-category`=5, `resource-id`=6). That enumeration reproduces **exactly 267**, so catalog and registry agree.

| Axis | Families | Channels |
|---|---:|---:|
| `element` (omni + 6) | 28 | **196** |
| `resource-id` (6 resources) | 4 | 24 |
| `status-category` | 6 | 24 |
| `action-category` (5 action categories) | 2 | 10 |
| `none` | 13 | 13 |
| **Total** | **53** | **267** |

Plus the open-ended prefix families (`status.power.{id}`, `status.resist.{id}`, `status.immune.{tag}`, `status.immuneReduction.{tag}`, `status.expose.{category}`).

> **Corrected 2026-09-02, and this is the one that had drifted furthest.** This section said **99**. The real figure is **267** — the gap accumulated across three separate expansions (T2 element widening 99→256, `poise-resource` 256→259, `turn.speed`/`turn.haste` 259→261, `resource.restore` 261→267) with nothing watching.
>
> **Why it went unnoticed, and what now stops it:** `spec-derived-stat-sheet.md` carries the same numbers and *cannot* drift, because `ElementHubDocDriftTests.StatSheetCountsMatchGeneration` pins it to `registry.AllRegistered.Count` plus a planted-drift companion. This file had no such test. It does now — `AtomCatalogSsotDriftTests`.
>
> ⚠️ **Registered ≠ authorable.** All 267 are addressable by the `stat.derived` KIND; **one** is addressed by a shipped atom, and 63 of them have no designed atom family at all. See the audit §3.3 and §4.

Derived ops are a **different set**: `Flat` · `Increased` · `Replace` · `Flag` — **no `More`** — folded by four compose kinds with per-channel caps (resist caps at 0.95).

**Known stubs:** `progression.power` = 1.0 in Core · `progression.realm` = 1.0 everywhere · `progression.bonus.arm1`/`.arm2` registered and read but **no producer** · `status.expose.*` legal with **zero readers**.

---

## 5. Status catalog — 21 declared, 21 functional in at least one runtime

| Provenance | Statuses | Note |
|---|---|---|
| **Working** (11) | `butter` `freeze` `cold` `poison` `hypno` · `wither` · `blight` `rot` `spark` `pact_mark` `spore` | 5 Unity CC + 1 DoT + 5 contagion |
| **Partial** (2) | `leech` (damage half only — the heal half was never built) · `bond` (declares `PulseHp`, but `Counter` is skipped by the pulse loop; its real payload is the nested burst) | |
| **Declared, inert** (8) | `ember` `jala` `kelp` `charm_pulse` — declare `UnityCc` with **no Unity branch**; `rally` `expose` `command` `shatter` — declare `ModifyStat`, and **`StatusPayloadKind.ModifyStat` has zero consumers repo-wide** | **Owner decision 2026-08-22: build the payloads in this program** — wire the **3** real Unity branches (`ember`, `jala`, `kelp`) — `charm_pulse` is a **def error**, not missing wiring: no vanilla method exists, implement a `ModifyStat` consumer, finish `leech`'s heal half. Needs the status stream's agreement (`StatusCatalog` is ADR-locked code-first) |

> ### ✅ CORRECTED 2026-09-02 — "13 functional / 8 declared inert" is stale, and wrong on all 8
>
> Re-measured from code. **All 21 have an executing consumer in at least one shipped runtime.** The
> table above is the 2026-08-22 sweep and is kept for the reasoning trail; where it disagrees with this
> block, this block wins.
>
> | Old claim | Today |
> |---|---|
> | `ember` `jala` `kelp` declare `UnityCc` with **no Unity branch** | **All three have branches** — `DebugActions.cs:886-912` |
> | `rally` `expose` `command` `shatter` declare `ModifyStat`, and **"`StatusPayloadKind.ModifyStat` has zero consumers repo-wide"** | **False.** `EffectRuntime.cs:76-98` upserts and withdraws them |
> | `charm_pulse` is a **def error**, no vanilla method exists | Resolved as a **def correction** (`UnityCc` → `ModifyStat`), not left broken |
> | `leech` — *"the heal half was never built"* | **Built** — `StatusEffectBridge.cs:93` |
> | `poison` CC-locks in battle because the check tests `Kind` | **Fixed** — `BattleEngine.cs:398` tests `IsCrowdControl` (category); poison is `dot` |
> | contagion **"cannot spread at all"** | **Half true.** Spreads on the lawn (`EffectBag.cs:655` passes a real board); cannot in battle (`BattleEngine.cs:275` passes `board: null`) |
>
> **What is still true, and is the more useful number: reach is uneven.**
>
> | | Statuses reachable |
> |---|---|
> | Battle `status.apply` | **21** — no id filter (`BattleEffects.cs:171-182`) |
> | Lawn `status.apply` | **8** — the switch at `DebugActions.cs:869-912`; the other 13 hit no case and do nothing |
> | Lawn `status.clear` | **4** — butter/freeze/cold/poison only (`InjectorEffectActionSink.cs:307-318`) |
> | Authored in a shipped atom | **4** — butter, freeze, cold, poison |
>
> ⛔ **`status.apply` and `status.clear` are asymmetric**, so `ember`/`jala`/`kelp` can be applied and
> never removed — and `ember`/`jala` have no Unity-side expiry either (`DebugActions.cs:893-899`).
> **Permanent once applied.**
>
> ⛔ **Battle has no `StatMods` consumer** (`BattleEffects.cs:154-183` applies the status but never reads
> `inst.StatMods`), so `rally`/`expose`/`command`/`shatter`/`charm_pulse` are inert *there* while working
> on the lawn — the mirror image of the contagion split.

Three further facts the catalog must carry:

- **Only the `elemental` family mutex is implemented.** Every other "family" is a label with no runtime behaviour.
- **`StatusDef.Tags` is unconditionally empty.** Immunity tags arrive per-grant, not from the def — the opposite of what `status-ssot.md` describes.
- **`poison` is incoherent across three subsystems:** category `dot`, family `elemental`, kind `UnityCc`. It resists on the DoT channel, CC-locks in battle (the check tests `Kind`), and never pulses.

Battle reachability is thinner still: once applied, only `wither`, `leech`, and the 5 contagions do anything; `rally`/`expose`/`command`/`shatter`/`bond` are inert; and contagion **cannot spread at all** because the engine passes `board: null`.

---

## 6. Refused, with cause

These must be in the SSOT so nobody re-probes them.

| Capability | Verdict | Cause |
|---|---|---|
| `Board.OnPlantDie` / `OnPlantCreate` hooks | **banned** | trampoline access violation |
| `Update` / `FixedUpdate` / `OnTrigger*` primary hooks | **banned** | unsafe primary hook, update spam |
| `GameAPP.Start`, EventNodes | **banned** | banned Harmony surface |
| Ice trail (`CreateIceRoad`) | **failed probe F51** | spawns a Sledge; no trail effect |
| Scene weather / fog | **level-bound** | tied to level load; crash risk on day lawn |
| `takeDmgMultiplier` | **inconclusive LIVE** | writer exists, no observable damage change |
| `Board.roadType` tile paint | **failed** | 12-element array, not a lawn map |
| `Bullet.HitZombie` / `HitPlant` Harmony | **off** | unsafe; on-hit uses TakeDamage + `AttackPlant` |
| `combat.hitland` | **not shipped** | ~134 overrides, no LIVE events |
| `OnWave`, `OnMindControl`, summon-wave | **probed, no LIVE row** | may be promoted with evidence |

---

## 7. Gaps that must become rejections

The layer we build on fails silently in **eight** documented places. Every one becomes a **load-time or bind-time rejection** in the atom layer.

| # | Today | Must become |
|---|---|---|
| G1 | Overlay accepts `atk` on `spawn.entity`; the sink **drops it**. Plant spawn drops `hp`, `maxHp`, `atk`, `x`, `mindControlled`; bullet drops five more | param set is per-`kind`+per-`side`; an unsupported param is a **validation error** |
| G2 | `box.set` accepts `cells[]`; the executor handles **one cell** | either implement `cells` or reject it |
| G3 | FA5–FA9 **always return true**, never reporting Unity failure | executors report failure; sequence-stop actually works |
| G4 | `capPerMatch` is in the allowlist with **no implementation anywhere** | implement the cap, or reject the key |
| G5 | `status.apply` with an empty target applies to **every zombie on the board** | empty target is a **rejection**; "all" must be explicit |
| G6 | Unknown *primary* channel is silently inert (unknown *derived* channel throws) | both reject |
| G7 | `ExecModifyStat` defaults a missing channel to `atk` | missing channel is a rejection |
| G8 | Primary `defense` reaches the lawn through a **side-wide** cached prefix value, so an entity-scoped `warding` atom silently does nothing | `warding`/`resilience` are **match-scoped families**; binding at **any** non-`match` scope rejects — `plant:N` and `zombie:N` included, because the prefix reads one side-wide value. Per-actor mitigation uses `combat.defense.*`. Per-entity primary defense waits for perf **O5** (per-ptr resolve cache) — resolving per-target in the prefix is exactly the uncached-per-hit-resolve pattern the perf audit blamed for combat lag |

---

## 8. Predicate leaves — proposed closed list

The `when` tree (AND/OR/NOT, depth-limited) over exactly these leaves. Anything not on this list is a reviewed change.

| Leaf | Param | Source |
|---|---|---|
| `sideIs` | plant \| zombie \| bullet | existing `filters.side` |
| `typeIdIs` / `typeIdIn` | int / int[] | existing `filters.typeId` |
| `actorIsKiller` | bool | existing OnDeath filter |
| `hasStatus` | statusId | StatusRuntime |
| `hpBelowMilli` / `hpAboveMilli` | ‰ of MaxHp | berserker/coward shapes |
| `elementIs` | element id | ActorElementTypes |
| `rowIs` / `colIs` | int | existing target filters |
| `isMindControlled` | bool | existing `excludeMindControlled` |

---

## 8a. Code or data — the rule, and where each thing lands

**The test:** a thing can be **data** if adding a row changes behaviour *without new code*. If a new row needs a new consumer, it must be **code**.

The repo already proves what happens when this is ignored. `status.expose.*` is a legal, registered, fully-valid derived channel with **zero readers** — adding it changed nothing. The eight declared-only statuses are the same failure. **A row no code consumes is not content; it is a lie in a table.**

### Decided 2026-08-22

| Thing | Verdict | Reason |
|---|---|---|
| **Element roster** (`ActorElementTypes`, `ElementRoster`) | **→ data** | The 84 combat channels are *generated* from families × roster, and `CombatDerivedReader` reads them **by pattern, not by name**. A seventh element regenerates its 12 channels and every existing consumer picks them up with no new code. Textbook content. |
| **Element matchup matrices** | **→ data** | Pure values (±250‰). **Two tables, not one** — the shield matrix is asymmetric with the combat ring (light↔dark are mutually +1 in shields), so they must not share rows. |
| **Derived channel *families*** (the 12) | **stays code** | Each has a named consumer — `CombatDerivedReader.Power` → resolver, `ShieldCapacity` → shield runtime, `CritRate` → sigmoid. A thirteenth family added as a row would have no reader and be dead on arrival. **This rule goes in the E1 spec** so nobody proposes one. |
| **Power coefficients** | **→ data**, with a sweep-proposal side table | "Hand-authored now, fitted later" only works if the numbers are readable and writable. The sweep writes *proposals*; a test reports the gap; humans decide what ships. |
| **Per-channel policy** (compose kind, default, cap) | **→ data** | Values, not behaviour — `SumIncreased`, cap 0.95. Also removes today's duplication between the constants file and `DerivedStatRegistry`. |
| **Kinds, triggers, predicate leaves** | **stays code** | Each needs an executor. Same rule. |

### The cost of moving elements

Not free, and worth naming: the enum **ordinal is load-bearing**, a test asserts exactly **84** derived channels, and the channel set feeds snapshots and goldens. So the content hash (E8) must cover the element roster, the 84-count test becomes `families × (roster + omni)`, and ordinal stability needs one careful pass — a reordered roster silently changes every generated channel id.

### Tables this adds

`effect_element` · `effect_element_matrix_combat` · `effect_element_matrix_shield` · `power_coefficient` (+ `power_coefficient_proposal`) · `effect_channel_policy` — on top of the eight already in the schema.

**`effect_channel_policy` has no owning module and is in no covered-hash list.** It holds compose kind, default, and **cap** (the 0.95 resist cap) — balance numbers whose change moves every golden with an unchanged `contentHash`. Either give it a module and register it with E8, or strike it.

**`effect_channel_policy` has no owning module and is in no covered-hash list.** It holds compose kind, default, and **cap** (the 0.95 resist cap) — balance numbers whose change moves every golden with an unchanged `contentHash`. Either give it a module and register it with E8, or strike it.

---

## 9. Counts

| Thing | Count | Growth policy |
|---|---|---|
| **Attach points** | **5** | ADR |
| **Kinds** | **12** | reviewed code change |
| **Triggers** | **8** | reviewed code change — `OnActivate` added by A18b (was 7 here until 2026-09-02) |
| **Predicate leaves** | **~8** | reviewed code change |
| Owner-key scopes | **7 total**, including `sector:{id}` and `slot:{id}` | reviewed change |
| Primary channels | **11** | shipped (E16); SSOT `StatChannels.All` |
| Derived channels | **267** | generated from 53 families × axis width; SSOT `DerivedStatRegistry` |
| Elements | 6 | **data** — roster rows (§8a) |
| Channel families | 12 | code — each needs a consumer |
| Statuses | 21 declared / 13 functional | catalog |
| Families | **unbounded** | data |
| Tiers per family | **unbounded** | data |
| Atoms | **unbounded** | data |

Twelve kinds and five attach points is the whole machine. Everything a player will ever see is families and tiers on top of it.
