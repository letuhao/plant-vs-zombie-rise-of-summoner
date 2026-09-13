# Spec: `cache-retrieval-mission`

**Status: written against shipped code 2026-09-13** — every `file:line` below was opened this session.
Module id `cache-retrieval-mission`, row 6 of the [deployment-hierarchy map](../deployment-hierarchy-map.md)
(wave 1, depends on `corpse-cache` (module 3, spec written, **not yet coded** — `spec-corpse-cache.md`)
and `cache-decay-void` (module 4, spec written, **not yet coded** — `spec-cache-decay-void.md`), plus an
external ask on the expedition program named in the map's §External dependencies table, row 6). Sibling
`cache-field-access` (module 5) had **no spec file on disk** at the time this session read the directory
(`docs/architecture/deployment-hierarchy/` listing: `spec-deploy-carry.md`, `spec-injury-tiers.md`,
`spec-corpse-cache.md`, `spec-cache-decay-void.md`, `spec-item-durability-repair.md` — no
`spec-cache-field-access.md`) — this spec does not wait on it and defends its own claim mechanism
accordingly (§Design 7). Ideal: [deployment-hierarchy-ideal.md](../deployment-hierarchy-ideal.md)
§"Corpse-drop, decay, and retrieval" items 3/4/5/6/7, §"Baggage, revisit loot, and the void" (map-less
missions paragraph, V2/V5/V6), §"Resolved 2026-09-13 (third clearing round)" items 2/4 (bounty identity),
and decisions.md row **Deployment hierarchy SSOT (2026-09-13)**, P2.

## Objective

Give a player a way back into a cache that no live party can reach: a new expedition-kind mission whose
target is a cache id, not a board — no squad-on-a-board, no interactive fight, no `WaveCatalog` wave. The
mission prices a souls fee scaled to the cache's own origin depth, runs on the expedition wall-clock (the
one legal wall clock in this whole program), rolls a single pass/fail contest for whether the mission
reaches the cache at all, and — on reach — hands back **everything currently sitting in the cache**
(certain-on-reach, no second per-item roll; module 4 already owns that guarantee and this module never
re-implements it). A "bounty hunt" is the same mechanism under a different label, run by the player's own
empire units, never an NPC faction (identity closed 2026-09-13, ideal doc §Resolved third clearing round
item 4).

Success looks like: a player whose delve wiped three sessions ago dispatches a retrieval mission against
that wipe's now-void cache, pays a souls fee priced off the delve's own depth, waits out a tier's wall
clock, and collects a manifest that includes whatever the cache's decay clock has not yet destroyed — not
a re-roll of what dropped, a **read** of what is still there; a mission that arrives after the cache has
already been fully claimed (by an earlier expedition, or by a field party the sibling module built) is an
honest empty haul, souls already spent, never a crash and never a second grant from an already-emptied
cache; two retrieval missions racing the same cache — or a retrieval mission racing whatever `cache-field-
access` turns out to build — produce exactly one winner, by construction of one idempotent claim write,
not by hoping the two modules agree.

## Locked anchors

- **Checked 2026-09-13 (later session, cargo-fate ask) — no edit needed here.** `spec-corpse-cache.md`
  widened `place_kind` to `world_sector`/`world_lane` and `source_kind` to include `legion_death`
  (§1a there); `spec-cache-decay-void.md` amended V5 so `world_sector`/`world_lane` caches never flip
  `in_void` (they start `in_void = 0` and stay there under this ask, unlike lawn/siege). This module's
  own targeting rule is unconditionally `in_void = 1` (below) — since the two new kinds never reach
  `in_void = 1`, they are structurally outside this module's scope today, exactly like lawn/siege caches
  are not (those DO reach `in_void = 1`, immediately). And this module's own boundary already forbids
  branching on `place_kind`/`source_kind` beyond the `in_void` filter (§Boundaries, "Never" — inherited
  from modules 3/4), so the new `legion_death` source_kind and the two new place_kind values need no new
  carve-out here either. If a future ask ever adds a void-transition trigger for `world_sector`/
  `world_lane` (named as open, not built, in `spec-cache-decay-void.md` §Boundaries), this module would
  then need re-verification — not assumed to already cover that case.
- **`in_void = 1` only — verified against module 4's own interface, not assumed from the ideal doc's
  looser prose.** The ideal doc's §4 wording ("when the place is gone or unreachable... the cache
  surfaces as a retrieval expedition") reads like a soft heuristic; `spec-cache-decay-void.md`'s own
  Interface table is the hard contract: *"`cache-retrieval-mission` (module 6) targets only `in_void = 1`
  rows (V2 — a bounty is a map-less mission against a void-resident cache, never a second fate)"*
  (`spec-cache-decay-void.md:379`). Combined with V5/V6 (a lawn/siege cache is `in_void=1` from the
  instant it is created; a delve cache flips to `in_void=1` in the same transaction as `CloseDelve`,
  `spec-cache-decay-void.md` §Design 2/Testing strategy), this resolves the alignment the task brief
  asked me to verify rather than assume: **every** lawn-death cache is retrieval-only from the moment it
  exists (no durable place-row ever lets `cache-field-access` reach one), and every delve cache becomes
  retrieval-only the instant the run that produced it closes. There is no `place_kind`/`in_void`
  combination this module and `cache-field-access` can legally race on **today**, by construction of the
  void flag alone — the racing scenario the task brief names is a defensive requirement for what module 5
  might build next, not a gap this session found already open.
- **Certain-on-reach is a claim-time property, not a manifest-roll property.** `spec-cache-decay-void.md`
  §Locked anchors, verbatim: *"reaching a live cache (baggage, revisit, or a **successful retrieval
  mission**) always recovers whatever decay has not yet taken — this module never rolls a second time at
  read/retrieval; modules 5/6 read surviving rows as-is."* Read together with the ideal doc's second-
  clearing-round item 3 (*"the challenge is the journey plus the decay clock, not a second dice roll at
  the cache"*), the conclusion this spec draws — stated explicitly because the task brief asked for a
  reasoned answer, not a restated ambiguity — is a **two-stage gate, not a guaranteed hit**:
  1. A mission-level pass/fail contest (retriever `Θ` vs the cache's own origin `Θ`) that **can fail** —
     this is "the journey," and it is where the Tarkov-insurance "priced" shape bites: a failed mission
     still spent the souls fee.
  2. **Only if the contest passes**, delivery is unconditional and complete: every `rpg_corpse_cache_item`
     row still present at claim time transfers, no per-item roll, no partial-on-success split. "Partial"
     in the Tarkov precedent is explained entirely by decay having already taken some items before the
     mission ever claims the cache — never by a second roll bolted onto a successful claim.
- **Souls price reads the cache's own origin depth, not the retrieving party's.** Direct precedent,
  read this session: `DelvePrices.RecoveryRitual(long recoveryRitualSouls, int woundingDelveThetaRun,
  PowerTuning tuning)` — *"Θ read = `theta_run` of the WOUNDING delve (attrition §7 — never the current
  one)"* (`src/FusionRpg.Core/Delve/Loot/DelvePrices.cs:85-92`). This module's price reads the **cache's**
  origin `Θ` the identical way — never the retriever's, never the current delve/match, matching the
  established "a sink reads the SAME Θ its faucet reads, and the faucet's Θ is the event's own depth, not
  whoever is paying" discipline (`SoulSinkPolicy.cs:23-25`, read this session).
- **No NPC hunter faction, ever.** Owner-locked verbatim, ideal doc §Resolved third clearing round item 4:
  *"Bounty hunts are run by the player's own empire forces as expedition-kind missions, surfaced through a
  bounty quest... No NPC hunter faction."* This module never creates an opposing side, never rolls against
  an NPC's stats — the "cache depth/threat" half of the contest is a property of the **place**, not an
  enemy actor.
- **A retrieval mission is not a battle.** `spec-expeditions.md`'s own Boundaries line — *"Always: ...
  battles through `WebMatchService` only"* (`spec-expeditions.md:98`) — is a real boundary this module's
  own `kind` deliberately does not honor, because a retrieval mission creates **zero** `WaveCatalog`
  fights, zero `BattleActorSetup` combat resolution, zero `RunPlannedMatchAsync` calls. Named here as an
  explicit, intentional exception this ask must carry — not a silent divergence discovered later by
  whoever reads `ExpeditionResolver.Resolve`'s existing squad-battle path and assumes every `kind` walks it.

## What already exists

### Built

| Finding | Evidence |
|---|---|
| `rpg_expeditions` schema — `id, player_id, correlation_id, state, tier_id, squad_json, seed, dispatched_utc, due_utc, collected_utc`; **no target/cache column of any kind** | `src/FusionRpg.Data/Sqlite/RpgStore.cs:698-710` — confirmed this session by reading the live `CREATE TABLE`, not the spec's own prose citation alone |
| `rpg_expedition_members` — `expedition_id, instance_id, active`, `PRIMARY KEY (expedition_id, instance_id)`; the soft-lock membership shape this module's own squad reuses unmodified | `RpgStore.cs:711-718` |
| `ExpeditionResolver.Resolve(tierId, squad, seed, elapsedTicks)` → `ExpeditionResolution{Ticks, Battles, Rewards}`; `ExpeditionRewards` has **Souls/Materials/WildJoins/SpecimenXpPerBattleWon — no gear-return field** | `src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs:17-38` (records), `:61-165` (`Resolve` body) — confirmed this session, matching `spec-expeditions.md:38`'s own citation of a "COMPLETE rewards manifest" with the identical four channels |
| Four fixed tiers, slots 2/3/4/5, durations 30m/4h/8h/20h, all tunable via `ExpeditionTuningHub` | `src/FusionRpg.Core/Expeditions/ExpeditionTierCatalog.cs:17-35` (`scout-30m`/`forage-4h`/`hunt-8h`/`warpath-20h`, `HasBossWave` on the last only) |
| Dispatch phase-gate precedent this module's own dispatch mirrors exactly | `src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs:61-64` — `Roster`-phase check, active-expedition-membership check, both already read this session (also cited by `spec-corpse-cache.md`'s own anti-fraud ask) |
| Squad composition is a shared, already-Hub-backed builder — **not a private composer** | `_webMatch.BuildSquad(playerId, squadIds)`, called at collect (`src/FusionRpg.Server/ExpeditionEndpoints.cs:106`), producing `IReadOnlyList<BattleActorSetup>` |
| A per-actor `Θ` is already carried on every `BattleActorSetup` — `Index` is a **read-only alias of `Level`**, the same field every other contest in this codebase reads as `Θ_actor` | `src/FusionRpg.Core/Battle/BattleModels.cs:15-24` (`Index => Level`, doc comment cites `spec-content-authoring.md §2.2`, `decisions.md:42`) |
| A **per-player** `Θ` read already lives inside this exact service, injected and used today for an unrelated purpose (Zomboss boss-battle pattern selection) — the same call shape this module's own retriever-strength read reuses | `ExpeditionEndpoints.cs:123` — `var theta = (long)_powerIndex.ActorIndex(new FusionRpg.Core.Stats.StatContext { PlayerId = playerId });` (`IPowerIndexProvider` injected `ExpeditionEndpoints.cs:23,26`) |
| Idempotent-claim-by-insert is an established pattern in this exact program, not invented here | Three separate precedents read this session: `CommandExistsUnlocked`/`InsertCommandUnlocked` (`RpgStore.WorldTurns.cs:139-153`, per `spec-cache-decay-void.md:67`); `rpg_delve_pack_lock UNIQUE(instance_id)` (`spec-loot-pack.md` §4, §Structure); `rpg_corpse_cache_decay_log PRIMARY KEY (cache_id, tick)` (`spec-cache-decay-void.md` §Design 1) |
| Souls-price-by-origin-depth is an established pattern, not invented here | `DelvePrices.RecoveryRitual` (`DelvePrices.cs:85-92`), `SoulSinkPolicy.Price(basePriceSouls, thetaContent, tuning) : long` (`src/FusionRpg.Core/Creatures/SoulSinkPolicy.cs:40-41`) |
| `(place_kind, place_ref)` cache addressing, `in_void`, `decay_started_turn`, `rpg_corpse_cache_item.{kind, instance_id, container_id, qty}` — the exact rows this module reads | `spec-corpse-cache.md` §Design 1 (schema), `spec-cache-decay-void.md` §Design 1 (additive columns), both modules' own §Interface tables (already cite `cache-retrieval-mission` as a named consumer) |

### Wiring gap

None found. Every piece this module needs either does not exist yet in shipped code (real gap, below) or
exists and is already reused unmodified by the exact dispatch/collect path this module extends
(`BuildSquad`, `IPowerIndexProvider`, the phase-gate, the tier catalog) — there is no inert toggle or null
delegate sitting between this module and its dependencies the way `CarryInPools` sat null at every battle
call site before `deploy-carry`.

### Real gap

| Gap | What would have to be built |
|---|---|
| `rpg_expeditions` has no target column, no `kind` discriminator, and `ExpeditionRewards`/`CollectResult` have no gear-return field | This module's own §Design 3/4 — pending the expedition program's sign-off (map §External dependencies row 6, `deployment-hierarchy-map.md:92`) |
| No cache-claim mechanism exists anywhere — `grep -rn "rpg_corpse_cache_claim" src/` returns nothing (confirmed this session) | §Design 7, proposed here defensively, explicitly flagged as pending reconciliation with `cache-field-access` (module 5), which has no spec file yet |
| No "cache origin `Θ`" signal exists on `rpg_corpse_cache` — neither `corpse-cache` (module 3) nor `cache-decay-void` (module 4) declared one; module 4 added `owner_player_id`/`decay_started_turn` post-hoc via the same additive pattern this module now needs to extend a second time | §Design 2 |
| **The "bounty quest" / Place 7 wrapper has no mechanism to hook into.** `docs/guide/the-loops.md`'s own Place-7 row (§7 "Quests and events") is **Vision**-status for everything except "Expedition event ticks... Shipped (thin)" (`the-loops.md:139`); the only *built* "Quest" system in `src/` is `Core/Delve/Quests/*` (`QuestCatalog`, `QuestRow`, `QuestSeedFile`) — an **in-delve** objective system keyed to a single `rpg_delves.QuestsJson` row (`RpgStore.Delve.cs:513-624`), unrelated in scope to a standalone, non-delve retrieval mission. There is no player-facing quest log, no "name a cache as a quest target" entity, no bounty-board table (`grep -rin "bounty" src/` — confirmed zero hits, this session). See §Design 6 for what this means for the wrapper's actual shape. |
| No existing mechanism validates "does this expedition kind skip `WaveCatalog`/`WebMatchService` entirely" | §Design 4 — `ExpeditionResolver.Resolve` today unconditionally builds `battles` from `WaveChain`/`BattleTickIndices` (`ExpeditionResolver.cs:69-104`); a retrieval `kind` needs its own resolve path, not a parameter threaded through the existing one |

## Design

### 1. Dependency interface consumed (module 3 + module 4, verbatim)

This module reads, never writes, three things modules 3/4 already declared as consumer-facing:

- `rpg_corpse_cache.{cache_id, place_kind, place_ref, in_void, owner_player_id}` — target resolution and
  the `in_void=1` filter (`spec-corpse-cache.md` §Design 1, `spec-cache-decay-void.md` §Design 1).
- `rpg_corpse_cache.decay_started_turn` + a plain read of `rpg_worlds.current_turn` for `owner_player_id`
  — elapsed-ticks display only, exactly as module 4's own Interface table already scopes this reader
  (`spec-cache-decay-void.md:380`): *"modules 5/6 compute elapsed ticks... neither module derives its own
  clock or re-implements this arithmetic."*
- `rpg_corpse_cache_item` row presence — this module only ever **removes** rows from it (on a successful
  claim), matching module 4's own Interface line verbatim (`spec-cache-decay-void.md:381`).

This module never reads or writes `decay_started_utc`, never re-rolls survival, never branches on
`source_kind` (module 3's own boundary, inherited transitively through module 4 — neither module reads it
either).

### 2. New column this module needs on `rpg_corpse_cache`: `origin_theta`

Neither module 3 nor module 4 gave the cache row a depth/threat signal — module 4 already established the
precedent for adding one more additive column to an earlier module's not-yet-coded table (`decay_started_
turn`, `owner_player_id`, both via `EnsureColumn`, `spec-cache-decay-void.md` §Design 1, called there a
"cheap, pre-code correction" because `corpse-cache` has zero `src/` hits). This module extends the same
pattern a second time, for the same reason:

```sql
-- Additive migration on corpse-cache's own header (EnsureColumn, matching cache-decay-void's own
-- precedent for a later module needing one more column on an earlier module's table).
-- The cache's own depth signal, priced the way DelvePrices.RecoveryRitual prices off "the WOUNDING
-- delve's Θ, never the current one" (DelvePrices.cs:85-92) -- this is that same Θ, stored once at
-- cache-creation time so price/contest reads never re-derive it from a place that may no longer exist
-- (a sealed delve's own rpg_delves row is not guaranteed reachable by the time retrieval runs).
EnsureColumn(db, "rpg_corpse_cache", "origin_theta", "INTEGER");
```

Written once, at cache-creation time, by whichever of module 3's two callers creates the row:
- **Delve-sourced cache:** `rpg_delves.ThetaRun` (`RpgStore.Delve.cs:64`, confirmed this session) of the
  delve that produced the death/wipe — read at the exact moment `corpse-cache`'s own
  `ResolveOrCreateCacheUnlocked` fires (already inside `CloseDelve`'s transaction, per
  `spec-cache-decay-void.md` §Design 2's own "one hook, not two" argument — the delve's `ThetaRun` is
  already in scope there, no second read).
- **Lawn/siege/world-sourced cache:** no per-death depth signal exists yet anywhere in this codebase
  (confirmed by the same reasoning `SoulSinkPolicy.cs:15-21` already states for vanilla-PvZ kills: *"the
  capture pipeline carries no per-kill depth signal yet"*). This module reuses `SoulSinkPolicy.
  VanillaPvzTheta` (`= 20`, `SoulSinkPolicy.cs:34`) **explicitly**, the same documented placeholder every
  other vanilla-PvZ soul flow already reads — never a silent zero, never a fabricated new constant.

This is a one-line write on an already-open transaction, not a new subsystem. Like module 4's own two
columns, it never enters `WorldState`'s hashed graph.

### 3. The ask this files on the expedition program — shape proposed, sign-off pending

Per the map's own row (`deployment-hierarchy-map.md:92`): *"`rpg_expeditions` today has no target column
and the manifest has no gear seat"* — this module is not authorized to add either unilaterally. The shape
proposed here, for the expedition program to accept, amend, or reject:

```sql
-- Additive on rpg_expeditions (RpgStore.cs:698-710) -- NOT built by this module, filed as an ask.
ALTER TABLE rpg_expeditions ADD COLUMN kind TEXT NOT NULL DEFAULT 'squad';   -- closed two: 'squad' | 'retrieval'
ALTER TABLE rpg_expeditions ADD COLUMN cache_id TEXT;                        -- NULL for kind='squad'; required for 'retrieval'
```

```csharp
// Additive on ExpeditionRewards (ExpeditionResolver.cs:28-32) and CollectResult (ExpeditionEndpoints.cs:70-77)
// -- NOT built by this module, filed as an ask alongside the schema change above.
public sealed record CacheRecoveryItem(string Kind, string? InstanceId, string? ContainerId, long Qty);
// ExpeditionRewards gains: IReadOnlyList<CacheRecoveryItem> RecoveredGear  -- empty for every kind='squad' row, unchanged goldens
// CollectResult gains the same field, populated at collect from live rpg_corpse_cache_item rows (§Design 5)
```

`CacheRecoveryItem`'s shape mirrors `rpg_corpse_cache_item.{kind, instance_id, container_id, qty}` exactly
— module 3's own Interface table already commits to that row shape as the one other modules read
(`spec-corpse-cache.md:277`: *"an `instance` row transfers via the existing `rpg_item`/assignment
machinery; a `stack` row via `AdjustStock`-shaped writes"*) — so this module invents no second item-return
shape, it only exposes the one that already exists as a manifest field.

**Named exception carried into the ask, not smuggled** (§Locked anchors): a `kind='retrieval'` row skips
`ExpeditionResolver`'s existing `WaveChain`/`BattleTickIndices` battle-tick machinery entirely (§Design 4)
— the ask must accept that a retrieval mission produces an `ExpeditionResolution` with an **empty**
`Battles` list, a deliberate divergence from `spec-expeditions.md:98`'s "battles through `WebMatchService`
only" boundary for every other kind.

### 4. Resolve path for `kind='retrieval'` — no battles, one contest tick

`ExpeditionResolver` gains a second entry point (or an internal branch keyed on `kind` — the exact split
is the expedition program's call, not this module's to force) that, for a retrieval mission, produces:

```
tickOutcomes:  a single tick outcome at the tier's own due tick — kind "cache-retrieval", carrying the
               contest's pass/fail and nothing else (no battle plan, no wave chain)
battles:       [] (empty — always, for every retrieval-kind row)
rewards:       EventSouls=0, Materials=[], WildJoins=[], SpecimenXpPerBattleWon=0, RecoveredGear=[...]
               (populated at COLLECT, not at this pure-resolve step -- see §Design 5, the one place this
               module's design deliberately does NOT preserve ExpeditionResolver's "outcome derivable
               from seed alone" purity, and says so)
```

**Named, not papered over: this is the one place a retrieval mission cannot be a pure function of
`(tier, squad, seed, elapsedTicks)` the way every existing expedition kind is.** `spec-expeditions.md`'s
own stated invariant — *"lazy resolution at collect is provably identical to eager resolution at
dispatch... the server stores no outcome, only `(tier, squad_json, seed)`"* (`spec-expeditions.md:40`) —
holds for the **contest** (pass/fail is derivable from seed + the two `Θ` values, both fixed at dispatch:
the cache's `origin_theta` never changes, per module 4's own "no re-stamp on reuse" rule, and the
retriever's `Θ` is read once at dispatch, not re-read at collect — see §Design 5 for why). It does **not**
hold for `RecoveredGear`, because `rpg_corpse_cache_item`'s row set is genuinely mutable external state
(module 4's own decay ticks can remove rows on every world turn the expedition's wall-clock window spans).
`RecoveredGear` is therefore a **live read at collect**, populated by the server transaction after the
resolver's pure contest result comes back — the same split `PackSettlement.Decide` (pure, returns a write
list) / `CloseDelve` (applies it against live state) already uses (`spec-loot-pack.md` §7), not a new
pattern. This is named here as an explicit, reasoned design decision for the expedition program to accept
or push back on — not an oversight discovered after the fact.

### 5. Retriever strength — reuse, not a new read

Two existing signals were found this session; either satisfies "composed `Θ` (+ rung/band), full stop"
(the ideal doc's own struck-`standing` correction, Tunables table). This spec proposes the second as the
starting shape, names the first as the alternative, and leaves the final choice to the expedition program:

1. **Per-squad-member**, via the same `BuildSquad` call `CollectAsync` already makes (`ExpeditionEndpoints.
   cs:106`), reading each `BattleActorSetup.Index` (`BattleModels.cs:15-24`) — an aggregate (max, sum, or
   mean; not decided here) over however many specimens the player commits.
2. **Per-player** (proposed starting shape): `_powerIndex.ActorIndex(new StatContext{PlayerId})`
   (`ExpeditionEndpoints.cs:123`) — the exact call this exact service already makes for the Zomboss
   pattern, reused unmodified. A mission-level pass/fail contest (not a multi-actor fight) reads more
   naturally against one scalar retriever-strength number than a squad roster, and this avoids inventing
   an aggregation rule (max? sum? mean?) the ideal doc never specified.

Read **once**, at dispatch, stored nowhere new (the expedition's own `seed` plus this `Θ` value are
sufficient to reproduce the contest deterministically at collect — the same "no outcome stored" discipline
`spec-expeditions.md:40` already states, extended to one extra scalar input the way `squad_json`/`tier_id`
already are). The contest itself: a per-mille roll in the `SeededRng`/`NextPerMille()` regime every other
Data-side deterministic roll in this program uses (`ExpeditionResolver.cs:106-107`'s own `tick:{t}`
stream; `spec-cache-decay-void.md` §Design 3's `corpse-decay:{cacheId}:{tick}:{seq}` stream) — **never**
`CombatProbability`'s `ICombatRng`/sigmoid regime, which is live-lawn-combat-scoped and non-deterministic
by design (`CombatProbability.cs:11-16` takes a live `ICombatRng`, not a seeded stream).

**The exact curve (`successMilli` as a function of `retrieverTheta − originTheta`) is not decided here.**
Per the "no private `f(Θ)`" rule and the precedent module 2 (`injury-tiers`) already set for its own
undecided binding (`spec-cache-decay-void.md`'s own citation of that module's open `ssot-power-scale.md`
§10 row), this spec states the **shape** — a bounded per-mille ratio, floor/cap, PS-8-exempt as a bounded
ratio not a magnitude — and leaves the exact slope as a tunable pending review, not a number asserted as
already balanced.

### 6. The "bounty quest" wrapper — what it actually is, given the real gap above

Since no quest-log or bounty-board mechanism exists to hook into (§Real gap), **this module does not build
one.** The ideal doc's own wording is read literally and narrowly: *"the quest **names** the cache, the
expedition **resolves** it"* (ideal doc §Corpse-drop item 4). The resolving half is entirely this module's
`kind='retrieval'` expedition row — already fully specified above. The naming half is **presentation**,
not a new backend entity: a `kind='retrieval'` expedition dispatched by the player is, by construction,
already "a request the player posted, run by the player's own empire units" (bounty identity, closed) —
there is nothing left for a quest wrapper to *do* mechanically that the expedition row does not already
do. This matches the map's own explicit scope line (`deployment-hierarchy-map.md:44-46`): *"Bounty hunts
in this program are player-posted, run by the player's own empire units as expedition-kind missions... Not
automatic bounties"* — and the ideal doc's own deferred-FE line (*"FE presentation... menu composition
belongs to `/idea-ui`"*). **Conclusion, stated plainly because the task asked for one:** "bounty quest" is
a **label a future quest-log UI puts on this exact expedition row** (per-cache framing text, maybe a
`docs/guide/the-loops.md` Place-7 catalog entry once that Vision item ships) — it is not a table, not a
state machine, not a dependency this module is blocked on. If a real quest log ever ships, it reads
`rpg_expeditions WHERE kind='retrieval'` the same way any other quest-log source reads its own backing
rows; nothing here changes when that happens.

### 7. The claim — proposed here, defensively, pending reconciliation with module 5

No claim mechanism exists in shipped code today (`grep -rn "rpg_corpse_cache_claim" src/` — zero hits,
confirmed this session) and `cache-field-access` (module 5) has no spec to read. Per §Locked anchors, the
two modules cannot race on a cache **today** (the `in_void` split already partitions their targets), but
the task's own brief requires a defensive design regardless — so this module proposes shared infrastructure
rather than a private one, explicitly flagged as needing module 5's sign-off once it exists:

```sql
-- Proposed NEW table, owned by whichever module lands first in this codebase (this session proposes it
-- here); NOT to be duplicated by cache-field-access once its own spec exists -- reconcile, don't fork.
CREATE TABLE IF NOT EXISTS rpg_corpse_cache_claim (
  cache_id        TEXT NOT NULL PRIMARY KEY,   -- one claim per cache, ever -- first successful INSERT wins
  claimed_by_kind TEXT NOT NULL,                -- closed two today: 'field' | 'expedition' -- a third
                                                 -- kind is a reviewed add, exactly like place_kind/source_kind
  claimed_by_ref  TEXT NOT NULL,                -- party id (field) | expedition id (this module)
  claimed_turn    INTEGER,                      -- world-stage turn at claim -- audit only, never read by
                                                 -- arithmetic, same discipline as decay_started_utc's
                                                 -- sibling column (spec-cache-decay-void.md §Real gap)
  claimed_utc     TEXT NOT NULL,                -- audit only
  FOREIGN KEY (cache_id) REFERENCES rpg_corpse_cache(cache_id) ON DELETE CASCADE
);
```

Claim and transfer happen **in one transaction**, at collect, only after the pass/fail contest (§Design 5)
succeeds:

```
TryClaimAndTransferCacheUnlocked(db, tx, cacheId, "expedition", expeditionId, destinationPlayerId)
    inserted = INSERT INTO rpg_corpse_cache_claim(...) -- fails silently (0 rows) on a PK collision,
                                                        -- the identical shape CommandExistsUnlocked/
                                                        -- InsertCommandUnlocked already uses
    if not inserted: return []   // already claimed by someone -- field access, or a beaten-to-it
                                  // expedition -- an honest empty RecoveredGear, not an error
    items = SELECT * FROM rpg_corpse_cache_item WHERE cache_id = cacheId   -- whatever decay left
    for each item: move it back (instance -> rpg_item_assignment/armoury visibility restored by
        DELETEing its rpg_corpse_cache_item row, per corpse-cache's own "absence from the cache table
        is presence in circulation" design, spec-corpse-cache.md §Design 1; stack -> AdjustStock-shaped
        write, same split corpse-cache's own Interface table already names for modules 5/6)
    return items as CacheRecoveryItem rows
```

This mirrors — and does not duplicate — three separate idempotent-claim precedents already read this
session (§Built): `CommandExistsUnlocked`'s insert-guard shape, `rpg_delve_pack_lock`'s `UNIQUE` lock, and
`rpg_corpse_cache_decay_log`'s own `PRIMARY KEY (cache_id, tick)`. **Explicit cross-module contract, named
per the task's own instruction:** if `cache-field-access`'s own eventual spec proposes a different claim
shape (a different table, a claim column on `rpg_corpse_cache` itself, a different `claimed_by_kind`
vocabulary), **the two specs must reconcile before either module builds its claim path** — this module
does not assume module 5 will adopt this table, only that *some* single, shared, atomically-inserted claim
record must exist so the two mechanisms cannot both empty the same cache. Whichever module's build lands
first should own the table; the other consumes it.

### 8. What this module does not do

Never rolls per-item survival (module 4's job, done before this module ever runs); never creates a cache
(module 3's job); never touches `WaveCatalog`, `BattleEngine`, or `WebMatchService` for a retrieval-kind
row; never grants gear back for a `kind='squad'` row (the field stays empty, existing goldens untouched);
never invents a bounty-board table or a quest-log entity (§Design 6); never reads or writes `rpg_corpse_
cache.decay_started_turn`/`in_void` beyond the one filter read at dispatch-time target validation.

## Tunables

`data/tuning/deployment-hierarchy.v1.json` (the shared new domain file `corpse-cache`/`cache-decay-void`
already name) for this module's own numbers; `data/tuning/expeditions.v1.json` (`ExpeditionTuningHub`) for
anything that extends the existing tier ladder, per the ideal doc's own "extend, not fork" instruction
(Tunables table row "Retrieval expedition tiers / bounty reward bands").

| Number | Owner | Notes |
|---|---|---|
| Retrieval price by tier: `basePriceSouls` per tier, scaled by `SoulSinkPolicy.Price(base, originTheta, tuning)` | `deployment-hierarchy.v1.json` (new keys) | Mirrors `DelvePrices.RecoveryRitual`'s shape exactly (§Locked anchors) — a `long` base per tier, never a literal in code |
| Expedition wall-clock window length | **Starting shape: the existing four tiers, unmodified** (`ExpeditionTierCatalog.All`, `scout-30m`/`forage-4h`/`hunt-8h`/`warpath-20h`) | Open point for the ask: reuse verbatim (this module's proposal, keeps the tunable surface from growing) vs. a dedicated retrieval-tier ladder (more control over "how far" a cache can be, but a second duration axis the expedition program may not want) — **not decided here**, named as a question the ask must answer, not assumed |
| Retrieval success: `successMilli.base`, `successMilli.thetaDeltaStepMilli`, `successMilli.floor`, `successMilli.cap` (bounded 0..1000, PS-8 exemption comment) | `deployment-hierarchy.v1.json` (new keys) | Shape only — the exact slope is pending an `ssot-power-scale.md` §10 review, mirroring `injury-tiers`' own deferred binding (module 2) |
| Cache origin `Θ` fallback for lawn/siege/world caches | **Not a new tunable — reuses `SoulSinkPolicy.VanillaPvzTheta = 20`** (`SoulSinkPolicy.cs:34`) | Explicit placeholder, not a silent default; becomes live the day a real per-death depth signal lands, exactly as that constant's own doc comment already promises for every other vanilla-PvZ soul flow |
| Retrieval expedition `kind`/`place_kind`/`claimed_by_kind` vocabularies | structural, code `const` classes (mirroring `ExpeditionStates`' own shape) | Closed enums, not tunables — a third `kind` value is a reviewed code change, per the "guardrail validates the contract, not a population" rule |

## Numeric types

Souls prices are `long` — `SoulSinkPolicy.Price` already returns `long` (`SoulSinkPolicy.cs:40-41`), and
every price this module computes flows through it unmodified, per `CLAUDE.md` "Numeric overflow" (a magnitude
is `long`, never assumed small). `origin_theta`, retriever `Θ`, `successMilli.*` bounds, and `claimed_turn`
are `int` — matching every existing `Θ`/turn field already read this session (`ThetaRun`/`ThetaActor` are
`int`; `decay_started_turn`/`newTurn` are `int`, `spec-cache-decay-void.md` §Numeric types). `Qty` on
`CacheRecoveryItem` is `long`, inherited unchanged from `rpg_corpse_cache_item.qty`'s own type (`spec-
corpse-cache.md` §Numeric types: *"a haul stack can, in principle, exceed `int` range"*). The success roll
uses `SeededRng.NextPerMille()` (an `int` per-mille draw against a `long`-typed bounded-ratio tunable, the
identical shape `cache-decay-void`'s own per-item roll already uses) — never `double`, never `System.
Random`, never `ICombatRng`'s sigmoid regime (§Design 5).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Expeditions"       # resolver goldens, kind='squad' unmoved
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CacheRetrieval"    # NEW
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~CorpseCache"       # claim-table interaction regression, once module 3 lands
.\scripts\guard-dal.ps1                                  # every new SQL string lives in FusionRpg.Data
.\scripts\guard-actor-hub.ps1                             # unaffected -- retriever Θ reads an existing provider, no new composer
python scripts\audit-magic-numbers.py --targets M1        # deployment-hierarchy.v1.json new keys, not bare literals
```

## Structure

```
src/FusionRpg.Core/Expeditions/CacheRetrieval.cs      NEW (pending the ask) -- CacheRecoveryItem, the
                                                       retrieval-kind resolve branch, the success-roll shape
src/FusionRpg.Data/Sqlite/RpgStore.CacheRetrieval.cs  NEW -- EnsureColumn(origin_theta) on rpg_corpse_cache
                                                       (§Design 2, buildable independent of the ask);
                                                       rpg_corpse_cache_claim schema + TryClaimAndTransfer-
                                                       CacheUnlocked (§Design 7, shared with module 5 --
                                                       BLOCKED on reconciling with its own spec once it
                                                       exists, do not build the claim table twice)
src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs     gains DispatchRetrieval (kind='retrieval' branch) --
                                                       BLOCKED on the expedition-program ask (§Design 3)
src/FusionRpg.Server/ExpeditionEndpoints.cs           CollectAsync gains the live-read-at-collect branch
                                                       for RecoveredGear (§Design 4/5) -- BLOCKED on the ask
tests/FusionRpg.Data.Tests/CacheRetrieval/            NEW
UNTOUCHED: ExpeditionResolver's existing squad-battle path, ExpeditionTierCatalog's four tier defs,
           WaveCatalog, BattleEngine, WebMatchService's existing RunPlannedMatchAsync path
```

## Code style

```csharp
// The one new column this module can add today without waiting on any ask (§Design 2).
EnsureColumn(db, "rpg_corpse_cache", "origin_theta", "INTEGER");

// The claim -- one INSERT as the lock, exactly the CommandExistsUnlocked/InsertCommandUnlocked shape
// (RpgStore.WorldTurns.cs:139-153) already establishes for a (subject, event) idempotency guard.
bool TryClaimCacheUnlocked(SqliteConnection db, SqliteTransaction tx, string cacheId, string claimedByKind, string claimedByRef)
{
    using var cmd = db.CreateCommand();
    cmd.Transaction = tx;
    cmd.CommandText = """
        INSERT OR IGNORE INTO rpg_corpse_cache_claim(cache_id, claimed_by_kind, claimed_by_ref, claimed_utc)
        VALUES ($id, $kind, $ref, $now);
        """;
    cmd.Parameters.AddWithValue("$id", cacheId);
    cmd.Parameters.AddWithValue("$kind", claimedByKind);
    cmd.Parameters.AddWithValue("$ref", claimedByRef);
    cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));   // audit only, never read by arithmetic
    return cmd.ExecuteNonQuery() == 1;   // 0 rows == another claim already won -- never an exception
}
```

## Testing strategy

- **`in_void` gate:** dispatching a `kind='retrieval'` expedition against a cache with `in_void=0` refuses
  (`cache.not-void`), naming the cache id — this module never touches a still-open delve room's cache.
- **Price reads origin, not retriever:** two players with different power dispatch retrieval missions
  against caches that share the same `origin_theta` and pay identical souls fees; the same player's own
  live power at dispatch time never changes the price.
- **Contest determinism:** same `(cache origin_theta, retriever Θ, seed)` twice ⇒ identical pass/fail,
  matching the existing `ExpeditionResolver` golden-test discipline (`spec-expeditions.md` §Testing
  strategy, "Determinism").
- **Certain-on-reach, proven against a live decay clock:** a cache with N items, some already decayed away
  by collect time, returns exactly the N-minus-decayed remaining rows on a successful claim — never a
  partial subset of the survivors, never a second roll (a source scan for a second `NextPerMille()` call
  on the post-claim item set, asserting zero).
- **Claim exclusivity:** two `TryClaimCacheUnlocked` calls against the same `cache_id` — the second always
  returns `false`/no rows, regardless of call order, regardless of which `claimed_by_kind`.
- **Empty-on-beaten-to-it:** a claim attempt against an already-claimed cache returns an empty
  `RecoveredGear` list, never an error, never a partial souls refund (the fee was already spent at
  dispatch — a genuinely sunk cost, matching Tarkov's own "priced" shape).
- **`kind='squad'` unmoved:** every existing `ExpeditionResolverTests` golden still passes unmodified —
  the new `kind` branch and the new `RecoveredGear` field never touch the existing path's hash.
- **No battle for a retrieval kind:** a `kind='retrieval'` expedition's resolution has an empty `Battles`
  list and never calls `RunPlannedMatchAsync`/`WebMatchService`, asserted by a call-count of zero, not by
  absence of an exception.

## Boundaries

- **Always:** claim before transfer, both in one transaction; price off the cache's own `origin_theta`,
  never the retriever's; target only `in_void=1` caches; leave a beaten-to-it claim's souls fee spent
  (never refunded); read `RecoveredGear` live at collect, never store it at dispatch.
- **Ask first:** the `rpg_expeditions` schema change and the `kind='retrieval'` resolve/collect path
  (expedition program, §Design 3/4) — this module's own build is **blocked** on that sign-off, not merely
  polite about asking; the `rpg_corpse_cache_claim` table's final shape, once `cache-field-access` (module
  5) has a spec to reconcile against (§Design 7); a dedicated retrieval-tier duration ladder, if the
  expedition program prefers one over reusing the existing four tiers.
- **Never:** a second `WaveCatalog`/`BattleEngine` path for a retrieval mission; an NPC hunter faction or
  actor to roll against (§Locked anchors); a per-item second roll on top of a successful claim (certain-
  on-reach is module 4's guarantee, not this module's to re-implement); a quest-log table or bounty-board
  entity (§Design 6 — none exists to extend, and none is this module's to invent); branching on `source_
  kind`/`place_kind` beyond the `in_void` target filter (modules 3/4's own boundary, inherited).

## Success criteria

1. A void cache's remaining gear returns through a priced, timed, `kind='retrieval'` expedition mission,
   provably reading live `rpg_corpse_cache_item` rows at collect, not a sealed-at-dispatch manifest.
2. Two claim attempts against one cache — by any combination of `claimed_by_kind` — never both succeed.
3. Existing `kind='squad'` expedition goldens are untouched. 4. `guard-dal.ps1`/`guard-actor-hub.ps1`
   green. 5. The expedition-program ask (§Design 3) and the module-5 claim reconciliation (§Design 7) are
   named as explicit blockers in `docs/architecture/decisions.md`-style language, not silently assumed
   granted.

## Interface exposed to dependents

None of the other six modules in this map depend on `cache-retrieval-mission` — it is a leaf in the build
order (`deployment-hierarchy-map.md` Build order: `{cache-field-access ∥ cache-retrieval-mission}`, both
terminal). What this module exposes is entirely **outward, to the expedition program**, as the ask:

| Member | Consumer |
|---|---|
| `rpg_expeditions.{kind, cache_id}` (proposed, §Design 3) | the expedition program's own dispatch/collect endpoints, once accepted |
| `CacheRecoveryItem` (proposed, §Design 3) | `ExpeditionRewards`/`CollectResult`, once accepted — the FE's `#/expeditions` collect reveal (`spec-expeditions.md` §FE) gains a gear shelf alongside the existing materials shelf |
| `rpg_corpse_cache_claim` (proposed, §Design 7) | `cache-field-access` (module 5), once its own spec exists — reconcile, do not fork |
| `origin_theta` on `rpg_corpse_cache` (§Design 2) | no other module reads it today; available for `cache-field-access`'s own success-contest pricing if it turns out to need the identical signal, rather than inventing a second one |

## Design-gate checklist

```
[x] Subsystems: expedition dispatch/collect (Data + Server), item assignment/ownership (Data), corpse-
    cache/decay schema (Data, read-only here) -- no Status/ActorHub/World-Step subsystem touched; the
    retriever-Θ read reuses an already-injected IPowerIndexProvider, no new composer.
[x] Read this session: deployment-hierarchy-ideal.md §Corpse-drop items 3-7, §Baggage/void map-less-
    missions paragraph, §Resolved third clearing round items 2/4; deployment-hierarchy-map.md in full
    (row 6, §External dependencies, §Assumptions); spec-corpse-cache.md in full; spec-cache-decay-void.md
    in full; spec-loot-pack.md in full (house style + PackSettlement pure/apply split precedent);
    spec-expeditions.md in full; docs/guide/the-loops.md §7 "Quests and events".
[x] Code cited by file:line, opened this session: RpgStore.cs (:698-718), RpgStore.Expeditions.cs
    (:53-95, :170, :198, :380), ExpeditionResolver.cs (whole file), ExpeditionTierCatalog.cs (whole file),
    ExpeditionEndpoints.cs (:1-135), BattleModels.cs (:1-60, :165-194), DelvePrices.cs (whole file),
    SoulSinkPolicy.cs (whole file), CombatProbability.cs (whole file), ClampedContest.cs (whole file,
    ruled out as the wrong regime), RpgStore.Delve.cs (:513-624 Quests, :64 ThetaRun), Core/Delve/Quests/
    (QuestCatalog.cs, QuestSeedFile.cs, QuestCoverage.cs — confirmed in-delve-scoped, unrelated).
[x] Drift reported: the ideal doc's §4 prose ("when the place is gone or unreachable") reads softer than
    module 4's own hard `in_void=1`-only interface line — resolved in favor of the latter, cited exactly
    (§Locked anchors, first bullet). spec-expeditions.md's own "battles through WebMatchService only"
    boundary is a real, load-bearing line this module's own `kind` must deliberately violate — named, not
    silently carried forward as if it still applied universally.
[ ] The exact retriever-Θ aggregation rule (single-scalar per-player vs. squad-member aggregate, §Design 5)
    is presented as two options with a recommendation, not resolved — the expedition program's ask response
    is what closes it, not this session's own preference.
[ ] The exact success-roll curve (successMilli as a function of Θ-delta) is shape-only, pending an
    ssot-power-scale.md §10 review — same posture module 2 (injury-tiers) already took for its own curve,
    named here rather than asserted as already bound.
[x] No §2 invariant contradicted: SQL only in FusionRpg.Data; souls prices are long; no f(Θ) asserted as
    final (both open curves named, not invented); no second ActorHub composer (retriever Θ reuses the
    existing IPowerIndexProvider injection); no second WaveCatalog/BattleEngine path; no NPC faction.
[x] Every build task this module cannot do alone is named BLOCKED in §Structure and §Boundaries, not
    silently assumed buildable: the rpg_expeditions/manifest ask (expedition program), the claim-table
    reconciliation (module 5, no spec yet to reconcile against).
[x] Checked 2026-09-13 (later session, cargo-fate ask): re-read `spec-corpse-cache.md` §1a and
    `spec-cache-decay-void.md`'s V5 amendment fresh — confirmed this module needs no edit, because
    `world_sector`/`world_lane` caches never reach `in_void = 1` under that amendment, so they never
    enter this module's own `in_void = 1`-only target set (§Locked anchors, new bullet).
```
