# The atom family library — enriching the effect list

**Status:** Drafted 2026-08-22. Sits on top of [atom-catalog-ssot.md](atom-catalog-ssot.md), which fixes the **closed** vocabulary: 5 attach points, 12 kinds, 7 triggers. **Nothing here adds a kind.** This document is the content layer — the affix families that make the catalog feel rich while the machine stays small.

---

## 1. Sizing, from the prior art

| Game | Affix rows | Real family count | Ratio |
|---|---|---|---|
| **Diablo 2 Resurrected** | **490** prefixes + suffixes | ~50 — "Sturdy / Strong / Glorious" is one family at three tiers | ~10 tiers/family |
| **Path of Exile** | thousands | mod **families/groups**; an item takes at most one mod per family, 3 prefixes + 3 suffixes | many tiers/family |
| **Diablo 4** | ~500 | categories: Offensive · Defensive · Utility · Resource · Mobility | banded by item power |

**The lesson:** 490 affixes is not 490 designs. It is ~50 families × ~10 tiers, and PoE's prefix/suffix + one-per-family rule is what keeps a rolled item coherent. Our schema already encodes both — `(family_id, tier, variant)` on the atom, and `group` on `effect_container_pool`.

**Target for us: ~71 authored families × 5 tiers ≈ 355 atoms.** D2-scale content from 12 kinds.

---

## 2. The generation rule — do not hand-author 196 channel families

> **⚠️ CLARIFIED 2026-09-03 — this section describes TWO stages, and one sentence merged them.**
> The phrase *"the same rule that turns 28 families into ~980 atom rows"* was read by two programs as
> one module, and both claimed it. They are consecutive:
>
> | Stage | Input → output | Owner | Shipped? |
> |---|---|---|---|
> | **families → atoms** — expand family × axis × tier | family definitions → `AtomRow`s | **effect-atom `E30`** | **no** — this is the unimplemented rule |
> | **atoms → affixes** — wrap 1:1 | `AtomRow`s → `AffixRow`s | **effect-pipeline module 3** | **yes** — `AffixLibraryGenerator`, written and tested, but with **zero production callers** |
>
> `E30` must not emit affixes; module 3 must not expand families. Full reasoning:
> [`../effect-atom-ideal.md`](../effect-atom-ideal.md) §W7.7.9.

The derived catalog is 28 combat families × 7 element slots = 196 channels (F6, reconcile pass,
2026-08-25 — was 12 × 7 = 84 when this doc was first drafted; the combat chain's T5.1–T5.4 modules
added 16 more families: `combat.{penetration,absorption,amplification,reduction}`,
`combat.reflect.{rate,resist.rate,damage,resist.damage}`,
`combat.parry.{break,rate,shred,strength}`, `combat.block.{break,rate,shred,strength}` — see
`src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs`'s `CombatChannelFamilies`, the canonical
count). Authoring 196 families would be the exact mistake this program exists to prevent.

**Instead: one family per *combat family*, with the element slot as the `variant` column.** *(The `variant`, not merely a param: `atom_id` derives as `{family_id}[.{variant}].t{tier}` and the unique key is `(family_id, tier, variant)`, so seven t1 element rows would collide if the element lived only inside `params_json`. It may appear in the channel param as well.)* `elemental_power` is one family; its atoms carry `element: fire|ice|air|earth|light|dark|omni`. Seven slots × 5 tiers = 35 rows **generated** from one family definition.

That turns the entire derived layer into **28 authored families** producing ~980 generated rows (28 ×
7 × 5), and it means adding a seventh element later regenerates rather than re-authors.

---

> **Naming note:** family names below are display shorthand. The actual `family_id` is kebab-case with an `atom.` prefix — `elemental_power` here is `atom.elemental-power` in the table, and `atom_id` is derived as `{family_id}[.{variant}].t{tier}` (definitions §1).

> **Naming note:** family names below are display shorthand. The actual `family_id` is kebab-case with an `atom.` prefix — `elemental_power` here is `atom.elemental-power` in the table, and `atom_id` is derived as `{family_id}[.{variant}].t{tier}` (definitions §1).

## 2a. Worked example — kind, family, atom

Three levels, and it is worth seeing them lined up before reading the tables.

**Kind** is the mechanism. **Family** is the affix identity that tiers hang off. **Atom** is the concrete row a player reads on an item.

| Player sees | Kind | Family | Tier | Params | Value spec |
|---|---|---|---|---|---|
| `+10 hp` | `stat.modify` | `vitality` | 1 | `channel: maxHp, op: flat` | `{10, 10, fixed}` |
| `+40–50 hp` | `stat.modify` | `vitality` | 3 | same | `{40, 50, onInstantiate}` |
| `+10 atk` | `stat.modify` | `might` | 1 | `channel: atk, op: flat` | `{10, 10, fixed}` |
| `+10 fire power` | `stat.derived` | `elemental_power` | 1 | `channel: combat.power.fire` | `{10, 10, fixed}` |
| `+10 fire resist` | `stat.derived` | `elemental_defense` | 1 | `channel: combat.defense.fire` | `{10, 10, fixed}` |
| `100–200 fire on hit` | `resource.delta` | `searing_strike` | 3 | `element: fire`, `when: OnDamageDealt` | `{100, 200, **onApply**}` |

Element power and resistance are the **same shape** as `+10 hp` — same mechanism, same value spec, same tier column. Only the channel namespace differs.

### Why `stat.modify` and `stat.derived` are two kinds

They look identical and should not be merged, because the channel layers genuinely differ:

| | `stat.modify` | `stat.derived` |
|---|---|---|
| Channels | 8 primary (→ 11) | 99 derived |
| Ops | `Flat` · `Increased` · `More` | `Flat` · `Increased` · `Replace` · `Flag` — **no `More`** |
| Unknown channel | silently inert today (→ rejection, G6) | throws |
| Caps | HP/ATK min 1 | per-channel; resist caps at 0.95 |
| Consumer | compose → Writer → Unity field | overlay resolver · status evaluator · shield runtime |

### ⚠️ They are not the same units

**`+10 hp` is ten hit points.** **`+10 fire power` is ten *resolver points*** — sigmoid scale, where `AccuracyScale` and `CritRateScale` are `100.0`, so ten points is 0.1 sigmoid units. For calibration: `critical-hunter` grants **+150** crit-rate points, moving crit from ~7.6% to ~26.9%; the patron aura converts per-mille to points by dividing by ten, so its 150‰ clamp is **+15 points**, not +15%.

Two consequences that must not be forgotten:

1. **Tier bands are per channel family, never copied across.** A tier-3 `vitality` might be +45 hp while a tier-3 `elemental_power` is +30 points.
2. **This is exactly what `normalize(magnitude, referenceScale)` in the power cost function is for.** A naive coefficient table that prices `+10 hp` and `+10 fire power` alike is wrong by an order of magnitude.

---

## 3. The library

Power category shown as **O**ffense · **S**urvivability · **C**ontrol · **U**tility · **E**conomy. Towers and creeps share almost the entire stat surface — including the whole elemental-defense and shield stack (§4.1). The only true split is the two vanilla armor layers.

### 3.1 `stat.modify` — the primary channels (14 families)

| Family | Channel · op | Cat | Side | Note |
|---|---|---|---|---|
| `vitality` | `maxHp` Flat | S | both | |
| `fortitude` | `maxHp` Increased | S | both | |
| `bulwark` | `maxHp` More | S | both | rare tier band only |
| `might` | `atk` Flat | O | both | |
| `ferocity` | `atk` Increased | O | both | |
| `savagery` | `atk` More | O | both | rare band |
| `warding` | `defense` Flat | S | both | **side-wide only** — see §4.1a |
| `resilience` | `defense` Increased | S | both | same |
| `plating` | `arm1` / `arm1Max` | S | **zombie only** | vanilla armor layer — the *field* is zombie-only; plant mitigation lives in `elemental_defense` + shields (§4.1) |
| `carapace` | `arm2` / `arm2Max` | S | **zombie only** | same |
| `mending` | `hp` Flat | S | both | current-HP top-up on grant |
| `quickening` | `attackInterval` | O | plant | **new channel** — pending the channel-extension spec (§5) |
| `flourishing` | `produceInterval` | E | plant | **new channel** — faster sun |
| `swiftness` | `zombieSpeed` | O | zombie | **new channel** — the pressure lever |

### 3.2 `stat.derived` — 28 generated families (~980 rows)

> ### ✅ The D6 quarantine is OVER — corrected 2026-09-02
>
> **This banner used to say `stat.derived` is quarantined `None/None/None` and that every family below
> is *"pending in all three runtimes"*, so *"authoring these rows before then produces content nothing
> can bind."* That is no longer true, and it was blocking the exact work it was written to protect.**
>
> Today's shipped matrix — `AtomKindRegistry.cs:160`,
> `new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.None)`:
>
> | Runtime | State | Consumer |
> |---|---|---|
> | **Lawn** | ✅ **Full** | `AtomDerivedSubsystem` — an `IActorStatSubsystem` on the injector's `ActorHub` at the reserved order-350 `foundation.effect` slot. Re-opened 2026-08-30, owner-approved via `decisions.md` *"Derived-write lawn executor"* |
> | **Battle** | ✅ **Full** | `BattleStatComposer` reads bound `stat.derived` atoms at squad build, via `TraitAtomSource`. Re-opened 2026-08-23 by **E12** |
> | **Sim** | ⛔ None | `SimEffectHost` still has no consumer — and it stays `None` deliberately, because *"flipping it on the strength of the other two would re-create the quarantine's cause"* |
>
> **So the families below are bindable content on lawn and in battle right now.** The only runtime that
> cannot execute them is Sim. Read the registry, not this paragraph — the matrix is the SSOT and it
> carries the reasoning for each cell inline.

| Family | Channel family | Cat | Flavour names by element |
|---|---|---|---|
| `elemental_power` | `combat.power.*` | O | Ember / Frost / Gale / Stone / Radiant / Umbral **Power** |
| `elemental_defense` | `combat.defense.*` | S | …**Ward** |
| `precision` | `combat.accuracy.*` | O | |
| `evasion` | `combat.dodge.*` | S | |
| `keen_edge` | `combat.crit.rate.*` | O | |
| `cruelty` | `combat.crit.damage.*` | O | |
| `stoicism` | `combat.crit.resist.*` | S | |
| `padding` | `combat.crit.resist.damage.*` | S | |
| `shield_capacity` | `combat.shield.capacity.*` | S | |
| `shield_toughness` | `combat.shield.toughness.*` | S | |
| `shield_pen` | `combat.shield.pen.*` | O | |
| `shield_regen` | `combat.shield.regen.*` | S | |
| *(owed to seedsmith)* | `combat.penetration.*` | O | — |
| *(owed to seedsmith)* | `combat.absorption.*` | S | — |
| *(owed to seedsmith)* | `combat.amplification.*` | O | — |
| *(owed to seedsmith)* | `combat.reduction.*` | S | — |
| *(owed to seedsmith)* | `combat.reflect.resist.rate.*` | O | — |
| *(owed to seedsmith)* | `combat.reflect.rate.*` | S | — |
| *(owed to seedsmith)* | `combat.reflect.resist.damage.*` | O | — |
| *(owed to seedsmith)* | `combat.reflect.damage.*` | S | — |
| *(owed to seedsmith)* | `combat.parry.break.*` | O | — |
| *(owed to seedsmith)* | `combat.parry.rate.*` | S | — |
| *(owed to seedsmith)* | `combat.parry.shred.*` | O | — |
| *(owed to seedsmith)* | `combat.parry.strength.*` | S | — |
| *(owed to seedsmith)* | `combat.block.break.*` | O | — |
| *(owed to seedsmith)* | `combat.block.rate.*` | S | — |
| *(owed to seedsmith)* | `combat.block.shred.*` | O | — |
| *(owed to seedsmith)* | `combat.block.strength.*` | S | — |

**F6 handoff (reconcile pass, 2026-08-25):** the 16 rows above are structural only — channel family and
`Cat` (O/S) are pulled directly from each channel's `role` field in
`data/seed/derived-stats/catalog.json` (`attacker`→O, `defender`→S, same convention the 12 original rows
already use), not invented here. The `Family` id and any flavour names are **explicitly handed to the
item corpus — [seedsmith](../seedsmith-map.md)** ([tasks/seedsmith-todo.md](../../../tasks/seedsmith-todo.md)) —
this reconcile module authors no atom rows (§3's own ban) and does not choose names for content it will
never bind (E12 gates that, see the quarantine note above). Cat is O for every attacker-owned half of a
role-inverted pair (`penetration`, `amplification`, the `.resist.` reflect channels, `break`, `shred`)
and S for every defender-owned half (`absorption`, `reduction`, the plain reflect channels, `rate`,
`strength`) — mechanically derived, not a creative choice, so it is filled in here rather than deferred.

Plus **4 status-channel families** (not element-expanded): `affliction` (`status.power.*` by category), `stalwart` (`status.resist.*`), `immunity` (`status.immune.{tag}`), `susceptibility` (`status.expose.*` — **declared with zero readers today**, so **not authored at all** until it has a consumer; `tier` validates as ≥ 1, so there is no tier-0 parking spot).

### 3.3 `resource.delta` — 6 families

| Family | Trigger | Cat | Sketch |
|---|---|---|---|
| `searing_strike` | OnDamageDealt | O | bonus element damage on hit — *the `100–200 fire` example* |
| `lifesteal` | OnDamageDealt | S | heal a share of damage dealt |
| `retribution` | OnDamageTaken | O | reflect a share back at the attacker |
| `deathblast` | OnDeath | O | damage burst on the corpse's cell |
| `regeneration` | OnTimer | S | periodic self-heal |
| `martyrdom` | OnDeath | S | heal allies when this dies |

### 3.4 `status.apply` — 21 families (all functional statuses after payload completion)

**Owner decision 2026-08-22: build the missing payloads in this program.** The **three** real Unity CC branches (`ember`, `jala`, `kelp`) get wired — `charm_pulse` has no vanilla method and is a def error to correct, not a branch to write, and a `StatusPayloadKind.ModifyStat` consumer gets implemented, so all 21 catalog statuses become authorable rather than 13.

| Family | Status | Cat | State |
|---|---|---|---|
| `buttering` · `freezing` · `chilling` · `venomous` · `mesmerizing` | butter · freeze · cold · poison · hypno | C | shipped |
| `withering` | wither | O | shipped — the OverTime default |
| `bloodletting` | leech | O | shipped, **damage half only**; the heal half is part of the payload work |
| `blighting` · `rotting` · `sparking` · `marking` · `sporing` | blight · rot · spark · pact_mark · spore | C | shipped; spread geometry is overlay data |
| `embering` · `scalding` · `entangling` | ember · jala · kelp | C | **needs a Unity branch** — `ApplyStatusToZombie` has no case for them today |
| `rallying` · `exposing` · `commanding` | rally · expose · command | U | **needs a `ModifyStat` payload consumer** |
| `shattering` | shatter | C | same |
| `bonding` | bond | O | shipped via the nested burst packet |

**Scope note, stated plainly:** completing these payloads is status-system and injector work inside an effect-layer program. It touches `DebugActions.ApplyStatusToZombie`, needs a real consumer for a payload kind that has none, and `StatusCatalog` is ADR-locked code-first — so it wants the status stream's agreement, not just ours. It is worth doing; it is not free, and it is not our layer alone.

### 3.5 Board and economy — 14 families

| Family | Kind | Cat | Sketch |
|---|---|---|---|
| `cleansing` | `status.clear` | U | strip statuses on trigger |
| `warded` | `shield.grant` | S | element shield on hit or spawn |
| `summoner` | `spawn.entity` | O | on-death spawn zombie |
| `gardener` | `spawn.entity` | U | on-death spawn plant |
| `volley` | `spawn.entity` | O | on-hit extra bullet |
| `cherry_bloom` · `dooming` · `firelining` · `flash_freeze` | `board.action` | O/C | the 4 shipped board ops |
| `gravemaking` · `gravedigging` | `grid.spawn` / `grid.clear` | U | LIVE-proven F42–F43, F48–F49 |
| `terraforming` | `box.set` | U | Water / Grass / Lava / Dirt — LIVE F45–F47, F50 |
| `sunbloom` · `midas` | `resource.economy` | E | on-kill sun / money — `capPerMatch` is **implemented in the runner** (decision 2026-08-22), where per-binding state already lives |

---

## 4. Domains — who can carry which family

This game is not one ARPG character sheet. It has towers, creeps, demons in battle, and a world of sectors with constructible slots. A family declares where it can live.

### 4.1 Mitigation is side-agnostic — only the vanilla armor fields are not

Worth stating plainly, because it is easy to get backwards:

| Mitigation layer | Plants | Zombies | State |
|---|---|---|---|
| **Elemental defense** (`combat.defense.*`, 7 slots) | ✅ | ✅ | shipped |
| **`DefensePercent` / `DefenseFlat`** | ✅ (`A-P-DEF%` / `A-P-DEF+`) | ✅ (`A-Z-DEF*`) | shipped |
| **Shields** — capacity · toughness · pen · regen, 3 stacks/actor, element matrix, drain order | ✅ | ✅ | shipped; `debug.shield.demo-all` grants to every living plant **and** zombie |
| **Vanilla armor layers** (`arm1`, `arm1Max`, `arm2`, `arm2Max`) | ✖ — no such Unity field | ✅ | shipped |

So the **whole resistance and shield mechanism belongs to both sides**. Only `plating` and `carapace` — the two legacy Unity armor layers — are zombie-restricted, and that is a fact about the game's fields, not about our design.

#### 4.1a Two mitigation paths with different scopes — do not confuse them

| Path | Reaches the lawn via | Scope | Family |
|---|---|---|---|
| **Primary `defense`** | the `TakeDamage` Harmony prefix → `StatMath.ScaleIncoming` (`GameHooks.cs:578` plant, `:683` zombie) | **side-wide** — the prefix reads one cached `_plantDefPct`/`_plantDefFlat` resolved with a dummy key, never per-entity | `warding`, `resilience` |
| **Derived `combat.defense.*`** | the overlay resolver, reading attacker and defender snapshots | **per-actor** | `elemental_defense` |

Consequence: a `warding` atom bound to a single plant **silently does nothing for that plant** — it would need the whole side. Per-entity mitigation must use `elemental_defense`. **Decided:** `warding`/`resilience` are match-scoped families only; per-entity primary defense waits for perf **O5**. This is gap **G8**.

### 4.2 Domains

| Domain | Carries | Note |
|---|---|---|
| **Plant (tower)** | stat, mitigation, economy, board control, spawn | `warding`, `sunbloom`, `terraforming`, `gardener` |
| **Zombie (creep)** | stat, mitigation, elite scaling, pressure, the two armor families | `plating`, `carapace`, `savagery`, `summoner` |
| **Demon (battle)** | derived-channel, HP-delta, shield families **today** | expands as the action/skill program lands — see §4.3 |
| **World (sector / slot / lane)** | a domain this library does not yet serve | see §4.4 |

### 4.3 The battle constraint is a snapshot, not a law

Battle consumes one opcode *today*. That is a statement about what has been built, not a permanent shape:

- [action-map.md](../action-map.md) is designing the action layer — skills, targeting, resource pools — and it consumes the container contract at the atom map's **Checkpoint B**.
- Battle enrichment (on-hit status riders, species skills, hybrid payloads) is specified and unstarted.
- The atom map's own runtime-support matrix is written as a **living audited table** precisely so it grows when a runtime grows a consumer.

So demon-facing families are narrow **in wave 1** and widen on someone else's schedule. The library should mark families `battle: pending` rather than `battle: never`.

### 4.4 The world domain is missing from this library

The world map is real and being built — `rpg_worlds`, `rpg_world_sectors`, `rpg_world_slots`, `rpg_world_lanes`, `rpg_world_entities`, `rpg_world_factions`, `rpg_world_commands` all exist, with `SectorTypeCatalog`, `SlotTypeCatalog`, `LaneTypeCatalog`, and `MarchResolver` in Core. Sectors hold constructible slots; lanes carry movement cost; the ideal describes base-building, exploration, and battle areas.

**None of the families above serve any of it.** A world layer wants effect-shaped content this library has no vocabulary for:

| World concept | Effect shape it wants | Nearest existing kind |
|---|---|---|
| A building that produces resources | periodic economy grant scoped to a slot | `resource.economy` + `OnTimer`, but the owner key is a **slot**, not an entity |
| A building that fortifies a sector | stat/mitigation for defenders in that sector | `stat.derived`, scoped to a sector |
| Lane or march modifiers | movement cost multiplier | **no kind** — `LaneCost` is its own math |
| Sector environment (element bias, danger) | ambient modifier on everyone present | `stat.derived` with a place-scoped owner |
| Exploration or discovery rewards | one-shot grant on a world event | `resource.economy` on a world trigger |

Two of those five need only a **new owner-key scope** (`sector:{id}`, `slot:{id}`) — the kinds already fit. The other three need triggers the catalog does not have (`OnWorldTick`, `OnSectorEnter`, `OnBuildComplete`) or math that is not ours (`LaneCost`).

**Decided 2026-08-22 — add the scopes now, leave the triggers to the world spec.**

- **In this program:** the owner-key vocabulary grows by two — `sector:{id}` and `slot:{id}` — alongside today's `match` / `plant:N` / `zombie:N` / `entity:HEX` / `player`. The kinds already fit, so this unblocks *building fortifies a sector* and *sector environment* with a contained change to binding scope.
- **Not in this program:** `OnWorldTick`, `OnSectorEnter`, `OnBuildComplete`, and anything touching `LaneCost`. Those need the world clock and lifecycle to be settled, and the world stream owns that. The trigger list stays at 7 until a world spec asks, and then it is a reviewed change.

What this library owes the world stream is the same contract we gave AI and the damage applier: a written seam, and an honest note about what still has to grow.

## 5. Channel extension — decided

**"Faster fire rate" could not be authored.** `attackInterval`, `produceInterval`, and `zombieSpeed` are cheat-document keys written directly by `EntityStatWriter.WritePlantExtras` / `WriteZombieExtras`, bypassing compose entirely — so no effect could reach the genre's central affix.

**Owner decision 2026-08-22: promote all three to real channels.** They join `StatChannels`, compose through the normal Flat→Increased→More path, and are written by the Writer like every other channel. That turns `quickening`, `flourishing`, and `swiftness` from impossible into data, and it keeps a single write path instead of adding a second one behind a 13th kind.

This is its own small spec after the atom layer lands: three channels, a composer case each, a Writer case each, and a guard test that the extras path no longer writes them behind compose's back.

### 5.1 G8 — per-entity defense does **not** come with it

Primary `defense` reaches the lawn through the `TakeDamage` prefix, which reads one side-wide cached value (§4.1a). The obvious fix — resolve per-target inside the prefix — is precisely the pattern the 2026-08 perf audit identified as the cause of combat lag: **uncached per-hit resolves on the Unity main thread**.

So G8 resolves as:

- **Now:** `warding` and `resilience` are **match-scoped families only**. Binding them at **any** non-`match` scope — `plant:N` and `zombie:N` as well as `entity:` — is a bind-time rejection. The prefix reads one side-wide value, so per-type bindings are exactly as dead as per-entity ones.
- **Per-actor mitigation** uses `elemental_defense` (`combat.defense.*`), which already resolves per-actor through the overlay resolver.
- **Later:** per-entity primary defense becomes possible only after the perf backlog's **O5** — a per-ptr resolve cache invalidated by revision — lands. Until then, do not re-open it.

## 6. Counts

| Layer | Count |
|---|---|
| Domains served | plant · zombie · demon(pending) · **world (scopes only)** (§4.4) |
| Attach points | 5 |
| Kinds | 12 |
| **Authored families** | **71** (§3.1–3.5 tables, counted) |
| Generated element rows (28 `stat.derived` families × 7 slots, F6 2026-08-25 — was 12×7) | ~196 templates |
| Tiers per family | 5 |
| **Total atoms at 5 tiers** | **~355 authored + ~980 generated** |
| Held back pending payloads | 8 status families |

Diablo 2 shipped 490 affix rows on roughly 50 families. We reach comparable content depth from **12 kinds and ~71 authored families**, because the element expansion is generated and the tiers are pure data.

---

## 7. Prior art

[D2R affix database (490)](https://lootcube.net/en/affixes) · [D2 prefixes](https://diablo2.diablowiki.net/Prefix) · [PoE modifier families](https://www.pathofexile.com/forum/view-thread/3587391) · [PoE crafting basics](https://www.craftofexile.com/basics) · [D4 affix categories](https://gamerblurb.com/articles/diablo-4-affix-categories-what-every-stat-means) · [D4 affix list](https://www.wowhead.com/diablo-4/guide/gear-items/affixes)
