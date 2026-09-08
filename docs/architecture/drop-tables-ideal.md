# Drop-tables — ideal

**Status: idea phase. No spec, no plan, no code changes. This document is where that phase stops.**
Written 2026-09-07, against shipped code and approved-but-unbuilt specs read the same session.

## 0. Architecture principles, restated (not linked)

**Every RPG feature lives in the RPG layer — it is never built by changing what PvZ is.** "Drop
tables," "rarity," and "loot pools" are not concepts PvZ has any notion of; the lawn just runs plants
vs zombies. Every gameplay surface that can drop loot — a lawn battle, a dungeon room, a siege wave, a
world-map sector event, an expedition tick — already resolves through an RPG-layer resolver
(`DistrictAssaultResolver`, `LootPipeline`/`DelveLoot`, `BattleEngine`, `WorldSectorLootSource`) that
decides outcomes entirely inside `FusionRpg.Core`/`Data`/`Server`. So "each gameplay mode gets its own
drop table with its own rate and pool" is purely a question of *which `DropTableRow` a resolver
selects and how richly that table is authored* — never a question of whether PvZ can express rarity
(it can't and doesn't need to). The wrong framing is "can the lawn support a rare-drop mechanic"; the
right framing is "does the RPG layer's drop-table model already support per-gameplay-mode tables and a
tunable minimum floor, and if not, is the gap wiring or real."

**No hard progression ceilings, but a probability floor is not a progression ceiling.**
`ssot-power-scale.md` Rule PS-8: *"A cap on a magnitude is a progression ceiling until proven
otherwise. Structural limits... and bounded ratios (per-mille, 0..1) are exempt by their nature."* A
drop-rate floor bounds a **probability** (0..1), not a magnitude a player accumulates — it is exempt
from the caps register by PS-8's own words, and `tunables-ssot.md` §1 lists "drop odds" by name as an
ordinary tunable. This matters because item's own `spec-drop-volume.md` (D26) separately, and for an
unrelated reason, bans **volume/count caps** — "no per-run, per-period or per-player ceiling" on *how
many* items drop. A rarity-floor tunable is not that: it governs *how rare the rarest thing is*, never
*how many things a player can hold or earn per period*. Conflating the two would wrongly block a
legitimate tunable by citing a rule that was never about it.

**Θ is the one ladder; two separate axes read it.** D18 (item's shipped decision): *how many* items
drop reads `Θ_actor` linearly (a rate, never a magnitude); *how strong* they are reads `P(Θ_content)`
through the rarity/tier path (quadratic, `contentScale`). A per-gameplay-mode drop-table redesign must
not blur these — differentiating tables by "which pool, which rates" is a **third, independent** axis
(composition/rarity-shape), never a private `f(level)`.

## 1. What was asked

> "Each gameplay [mode should] have multiple drop tables with different drop rates and different item
> pools — this makes the mechanism unique [and] avoids people ignoring a gameplay mode because they can
> loot everything [elsewhere]. Also make the lowest drop [rate] cap 0.0001% and tunable."

Two asks, cleanly separable:

1. **Per-gameplay-mode differentiation** — a siege wave, a dungeon room, a world sector, and a lawn
   battle should not draw from the same undifferentiated pool at the same rates.
2. **A tunable minimum floor**, as fine as 0.0001% (1-in-1,000,000), for the rarest tier of an
   individual table or entry.

## 2. Built — what already exists, verified against code this session

| # | What | Where | Verified |
|---|---|---|---|
| B1 | **Ten-rung rarity ladder**, ordinals 10–100 spaced by 10, each with a count band, a prefix/suffix split, a tier window and a per-rung drop weight | `docs/architecture/item/ssot-rarity.md` §3.3; weights in `data/tuning/item-rarity.v1.json` (`dropWeightPer100k`, e.g. `chaff: 40700`, `almanac: 700`) | Read `item-rarity.v1.json` directly; ladder table read from `ssot-rarity.md` |
| B2 | **Weighted, grouped drop-table draw engine** — entries carry an `int Weight`, drawn via a `checked long` running-total cursor (no silent overflow) | `src/FusionRpg.Core/Items/Drops/DropTableModel.cs` (`DropTableEntryRow`, `EffectiveWeight` returns `long`, weighted-draw loop) | Read directly |
| B3 | **Kind-agnostic, shared instantiation engine** — `Instantiator.TryInstantiate(ContainerRow, ...)` rolls ANY container kind into a reproducible `InstanceRow`, already used by loot drops, unique items, and (built today, same session) gems | `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:98` | Read directly; today's own `GemContainerBuild`/`Instantiator` round-trip test is a second, independent proof |
| B4 | **Volume reads `Θ_actor` linearly, quality reads `P(Θ_content)`** — two separate, already-shipped axes; volume has a structural floor and *no* upper bound (D26) | `docs/architecture/item/spec-drop-volume.md` D18; `Items/Drops/DropVolume.cs` | Read the spec in full |
| B5 | **`loot_source(source_kind, source_id, table_id, content_level, first_clear_grant)`** — the schema-level hook that lets *any* gameplay source point at its *own* table id. This is already the multi-table-per-gameplay-mode mechanism, at the schema level | `spec-drop-volume.md` "Data shape"; `LootSourceRow`, `DropTableModel.cs:52-60` | Read directly |
| B6 | **Per-room-kind rarity floor and shift** (approved, cited below as unbuilt for the generator half) — `RarityFloor` raises the tier-window floor; `RarityWeightShift` moves the weight distribution up/down N rungs without touching magnitude. Different room kinds (fight/elite/boss/cache/quest) already have different floor/shift columns in the design | `docs/architecture/party-dungeon/spec-dungeon-loot.md` §4; `DropTableModel.cs:97-98`; `LootPity.cs:74-76, :101-103` | Read the dungeon-loot spec in full |
| B7 | **`affix_channel` ∈ {drop, boss}`** — a per-entry, content-authored fact distinguishing a trash drop's affix pool from a boss drop's | `spec-drop-volume.md` "X4 — this module supplies the drop and boss channels"; `DropTableModel.cs:34-36` | Read directly |
| B8 | **Bad-luck protection** — per-player pity counters on two guarded rungs (`heirloom`=70, `sunwoven`=90), a floor on incidental drops (every Nth incidental drop is at least rung 30), rung 100 deliberately unguarded but given a deterministic source (first-clear grant) | `ssot-rarity.md` §3.8; `LootPity.cs` | Read directly |
| B9 | **The kill-drop roll is independent of the rarity roll** (D38, owner-decided 2026-09-04): a flat, tunable 5% "does anything drop" roll, then a *separate* table decides which rung — never conflated | `spec-drop-volume.md` "D38" | Read directly |
| B10 | **`long`/`checked` arithmetic throughout**, per CLAUDE.md's numeric-overflow rules — no float, no silent wrap, matches the same discipline already audited elsewhere this session | `Instantiator.cs`, `DropTableModel.cs`, `ContentScale.cs` | Read directly |

## 3. Wiring gaps — built but not connected, one call from being real

| # | What | Where | Why it's wiring, not a wall |
|---|---|---|---|
| W1 | **`affix_channel` is authored and inert.** The column exists and is threaded through step 9, but nothing yet reweights composition by channel — that lands with `effect-pipeline` module 12 (X4) | `spec-drop-volume.md`: *"Until X4 lands, `affix_channel` is authored and inert. That is a wiring gap, not a wall."* | The spec itself names this a wiring gap, not an architectural limit; landing X4 is a one-call change, no schema change needed |
| W2 | **`Instantiator.TryInstantiate` used to have zero production callers** — this was true as recently as `seed-to-concrete` T0.8 (2026-09-01) and is explicitly why DESIGN-GATE §1 carries a warning row about it. It is **no longer true** — loot, uniques, and today's gem work all call it in production. Recorded here only so a future session does not re-flag it as unreachable without checking | `DESIGN-GATE.md` row: *"Affix/container authoring, rolled instances..."* | Confirmed built via three independent production call sites, not assumed |
| W3 | **`WorldSectorLootSource.TryResolve` is built, tested, and correct, but has zero production callers.** World-map's own sector-clear loot resolution exists as a pure function today with nothing in the real sector-clear flow calling it | `src/FusionRpg.Core/Items/Drops/WorldSectorLootSource.cs`; only reference outside itself is `tests/FusionRpg.Core.Tests/Items/WorldSectorLootSourceTests.cs` | Confirmed via `grep -rln "WorldSectorLootSource\.TryResolve\|WorldSectorLootSource\.SourceKind"` — one hit, a test file. Wiring, not a wall: the function needs one real call site at whichever code fires a sector-clear event (`World/Turn/SiegePhase.cs`/`BattleSeam.cs` are candidates, unconfirmed which owns the event) |

## 4. Real gaps — genuinely unbuilt or structurally missing

| # | What | Where | Evidence |
|---|---|---|---|
| R1 | **Corrected after a second pass (the first grep scoped only `World/`/`Battle/` and missed a file filed under item's own directory).** The real split is two different gaps, not one: **(a) base-defense has zero loot integration of any kind** — confirmed no `LootSourceRow`/`DropTableRow`/`Instantiator` reference anywhere under `World/Turn` or `Battle/Board`, and `DistrictAssaultResolver.cs` grants no souls or items on its own — this is a genuine real gap, build from scratch. **(b) world-map already has a real, tested, PURE function** — `WorldSectorLootSource.TryResolve(sectorId, dangerBand, tuning, out LootSourceRow?)` (`src/FusionRpg.Core/Items/Drops/WorldSectorLootSource.cs`) resolves a sector-clear event to a `LootSourceRow` against one shipped table (`drop.world.sector-clear`) — **but it has zero production callers**, only a test (`WorldSectorLootSourceTests.cs`). This is a **wiring gap** (§3 W-class), not a real gap: the resolver just needs a real caller wherever a sector-clear event actually fires (`World/Turn/SiegePhase.cs`/`BattleSeam.cs` are the candidate call sites, unconfirmed which one owns the event). It also names its own further limitation: it points at exactly ONE table for every sector regardless of type — no per-sector-type pool differentiation yet, only content-level scaling | Confirmed via `grep -rln "WorldSectorLootSource"` (finds the file) and a second grep for `WorldSectorLootSource\.TryResolve\|WorldSectorLootSource\.SourceKind` (finds only the test file) | Re-splits what "each gameplay mode" needs: base-defense needs a first, real binding built; world-map needs its already-built resolver wired to a real call site, then its single shared table differentiated by sector type |
| R2 | **Party-dungeon's own rich, per-room-kind table design is approved but unbuilt.** `spec-dungeon-loot.md` is dated *"APPROVED by the owner 2026-09-05 (wave 3) — unbuilt."* Its own generator, `DungeonLootTableGen`, exists in code today only as a deliberately minimal "stage-1b" placeholder: **every** (climate, room-kind) pair currently emits the identical two-entry shape (one `plant`-frame slot, one `humanoid`-frame slot, both at the same `EquipmentWeight`, one `Nothing` remainder) — no per-role weighting, no rarity floor/shift wired to a real generator, no distinct pool per room kind yet | `src/FusionRpg.Core/Delve/Loot/DungeonLootTableGen.cs` doc comment: *"One real, minimal, valid table... Deliberately structural, not a balance pass"*; confirmed reading the `Build()` method directly | This is the gap the user's dungeon-drop-table observation was actually pointing at — not a bug in the placeholder, but the placeholder standing in for a whole unbuilt richer generator |
| R3 | **Precision ceiling: both the rarity-ladder weight convention and the drop-table-entry weight convention are explicitly documented as "plain integers out of 100,000, not per-mille."** `bands.v1.json`'s own `dropBand.resolvesTo` field states this in words. The finest weight the shipped convention names today is `exceptional: 7` (out of a ~100,000-scale total) — nowhere near fine enough to express 0.0001% (1-in-1,000,000) as a **named, documented convention**, even though the underlying `long`/`checked` arithmetic in `DropTableModel.cs`/`Instantiator.cs` has no structural ceiling at all (an author could already set `Weight = 1` against a `groupTotal` of 1,000,000 and get exactly that rate — the code permits it; the *convention and tooling* do not name or validate for it) | `data/seed/items/_registry/bands.v1.json`: `"resolvesTo": "a plain positive integer weight — never per-mille... Weights are plain integers out of 100,000"`; `item-rarity.v1.json`'s own `dropWeightPer100k` field name | This is the concrete, checkable form of "make the lowest drop cap 0.0001% and tunable" — the fix is a convention/schema move from a per-100,000 unit to a per-1,000,000-or-finer unit (e.g. `dropWeightPerMillion`), not a rewrite of the draw engine itself |
| R4 | **No named, tunable "minimum floor" constant exists anywhere for an individual ultra-rare drop-table entry**, distinct from the rarity ladder's own rung weights. If a designer wants one specific item (not tied to a rarity rung at all — a "legendary drop," a cosmetic, a title) to have a guaranteed, named, config-driven floor of 0.0001%, there is no existing tunable to reach for; it would need to be authored fresh, following `tunables-ssot.md`'s T6 (carry its unit — e.g. `MinDropRatePerMillion`) | Searched `Items/Drops/`, `item-rarity.v1.json`, `bands.v1.json` — no existing "floor"/"minimum rate" tunable found outside the rarity ladder's own per-rung weight | A real gap, not a wiring gap — nothing partially exists to connect |
| R5 | **Smart loot (frame-weighted composition) is deferred, correctly, pending `X1` (frame-classify, unbuilt).** This is a *different* axis from rarity-floor/table differentiation (it biases which base type drops, not which rung) but is worth naming since it is the other half of "make each mode's pool feel different" | `spec-drop-volume.md` "Smart loot — deferred" | Already correctly deferred with a trigger; not blocking the two asks above |

## 5. Genre prior art — with sources, and honest gaps where nothing solid was found

*(Full sourced report from a dedicated research pass is preserved verbatim in this session's own
record; the load-bearing numbers and citations are restated here so a downstream reader does not need
to chase a link.)*

### 5.1 Per-context drop tables — real, shipped, quantified examples

- **Diablo 2's Treasure Class (TC) system** is the clearest documented "bucket per context" model.
  Every monster has a Treasure Class with a numeric **"TC bonus"**: regular monsters = 0; champion/act
  bosses ≈ 1024 (guarantees at least a magic item and meaningfully raises unique/set/rare odds); quest
  bosses get a further bump on top. The actual reweighting formula, quoted directly from the community
  reverse-engineering reference: `chance = chance − (chance × tcbonus) / 1024`. Item quality resolves
  in a fixed priority order (Unique → Set → Rare → Magic → High-Quality → Normal), with Magic Find
  applied at each stage. Source: [purediablo.com/d2wiki/Treasure_Classes](https://www.purediablo.com/d2wiki/Treasure_Classes),
  [d2mods.info drop-rate thread](https://d2mods.info/forum/viewtopic.php?t=44413).
- **Path of Exile**: normal monsters drop items at the area's monster level; magic monsters at +1;
  rare/unique at +2 — a built-in per-tier item-level bump, independent of rarity weighting. Base "does
  anything drop" chance from a normal monster is 16%. Increased Item Rarity only reweights
  magic/rare/unique odds; Increased Item Quantity only changes count — the same D18-style volume/quality
  split this repo already made independently. Source: [PoE Wiki — Rarity](https://pathofexile.fandom.com/wiki/Rarity).
- **Borderlands**: unique/legendary drops are an explicit **per-enemy loot pool**. Borderlands 2/3: a
  9%–30% "does a legendary drop" chance is **split across every pool entry** an enemy has (two entries
  halve the effective chance each). Borderlands 4 changed this so each legendary rolls independently
  and rates scale with difficulty tier (a 9% base becomes 23% at the hardest tier); "Shiny" ultra-rares
  sit in a wholly separate pool at 0.3%–1.5%. Source: [Mobalytics BL4 drop-rate guide](https://mobalytics.gg/borderlands-4/guides/complete-legendary-loot-drop-rates),
  [Gearbox — Inside the Box: Evolution of Loot](https://www.gearboxsoftware.com/2013/09/inside-the-box-evolution-of-loot/).
- **Warframe**: an explicit 4-tier bucket vocabulary (Common/Uncommon/Rare/Legendary) where the table's
  own weights, not a universal percentage, are authoritative. Void Relics are the cleanest example of a
  *tunable* per-tier table: refining a relic shifts weight from common toward rare —
  Intact 76/22/2 → Radiant 50/40/10 (common/uncommon/rare, in percent). Source:
  [Warframe Wiki — Void Relic/Math](https://wiki.warframe.com/w/Void_Relic/Math).
- **Diablo 4**: a wholly separate table for "Mythic Uniques," layered as a flat ~2% chance on top of
  specific, purpose-built farm-target bosses — i.e. an entirely distinct pool/rate for the
  content type meant to be the endgame chase, not a reweight of the normal table. Source:
  [Icy Veins — Uber Unique farming](https://www.icy-veins.com/d4/guides/how-to-farm-uber-uniques/).

**Takeaway for this repo:** every one of these already matches a piece of the shipped design —
Diablo 2's TC-bonus-by-monster-importance is the same shape as B6's per-room-kind `rarityFloor`/shift;
PoE's IIR/IIQ split is the same shape as B4's `Θ_actor`(volume)/`P(Θ_content)`(quality) split; Diablo
4's fully separate top-tier table is the shape R4 names as missing (a distinct pool for the rarest
tier, not just a lower weight in the same pool).

### 5.2 Ultra-rare / extreme-tail rates — and where 0.0001% actually sits

- Diablo 2's own drop-rate reference documents a worked example with a **base rate of 0.0001 (1-in-
  10,000)** for a specific unique from a specific monster at zero Magic Find, and a separate worked
  rare-item example landing at **1-in-88,865**. Both are computed entirely in integer arithmetic.
  Source: [d2mods.info](https://d2mods.info/forum/viewtopic.php?t=44413).
- Path of Exile's Mirror of Kalandra has **no developer-published rate**; community estimates cluster
  at **roughly 1-in-10,000,000 to 1-in-26,000,000** per monster kill (0.00001%–0.0000038%) — rarer than
  the 0.0001% floor asked for here. **This means 0.0001% (1-in-1,000,000) is not an extreme outlier
  against real industry practice — it sits comfortably above PoE's most famous "rarest of the rare"
  item, in the range of a strong top-tier legendary, not a mythical unicorn rate.** Flagged honestly:
  this is a community estimate, not GGG-published.
- WoW's documented ultra-rare mounts (Galleon, Nalak, Sha of Anger, etc.) all sit around **0.01%–0.03%**
  — an order of magnitude *more common* than 0.0001%. No Blizzard-disclosed drop reaches the 1-in-a-
  million tier with solid sourcing.
- **No developer-disclosed rate at or below 0.0001% was found anywhere** in this research pass across
  Diablo, PoE, WoW, Borderlands, or Warframe. Studios treat their true bottom-tier rates as
  intentionally undisclosed. This is worth stating plainly rather than papered over: **shipping a
  documented, tunable 0.0001% floor would put this game's transparency ahead of every AAA title
  checked**, which is a deliberate design choice worth the owner naming explicitly (a strength, if
  intentional; a support-ticket magnet if the number is ever screenshotted and compared to a
  competitor's opaque one).

### 5.3 Bad-luck protection — one hard formula, several confirmed-but-undisclosed ones

- **Genshin Impact is the one system with fully public numbers**, and it is the cleanest template for
  this repo's own pity mechanism (B8) to compare against: base 5★ rate 0.6%; soft pity begins at pull
  74 (~6.6%, rising ~6% per further pull); hard pity at pull 90 (100%); average pulls-to-5★ ≈ 62.
  Source: [genshintactics.com](https://genshintactics.com/guides/genshin-pity-system-explained-2026/).
- **WoW confirms a streak-based bad-luck-protection mechanic exists** (bonus-roll chance rises after
  each failed roll, persists until awarded) but has never published the formula. **This repo's own
  design already exceeds WoW's public transparency**: `ssot-rarity.md` §3.8 names its two guarded
  rungs, the incidental-drop floor, and the rung-100 deterministic-source rule explicitly, in a
  document anyone can read.
- **PoE's Stacked Deck / Divination Cards** is a *reweighted-pool* form of protection (buy/craft a
  guaranteed draw from a pool biased toward common but reachable to rare) rather than a counter — a
  different mechanism from a pity counter, worth knowing as a second valid pattern if a counter-based
  approach ever feels too deterministic for a specific reward.

### 5.4 Why differentiate pools by content type at all — documented design reasoning

- **Diablo 3's own GDC 2015 postmortem** (Josh Mosqueira, *"Against the Burning Hells"*) is a direct,
  first-party admission that launch-era Diablo 3's loot was not sufficiently targeted/differentiated —
  Reaper of Souls' rework made drops class-appropriate and substantially raised legendary rates as a
  correction. This is the strongest single citation for "undifferentiated loot pools are a real,
  shipped design failure, not a hypothetical one." Source: [GDC Vault](https://www.gdcvault.com/play/1021776/Against-the-Burning-Hells-Diablo).
- No talk found makes the fully general, cross-genre case independent of a specific game's launch
  failure — flagged honestly rather than invented. The practice (differentiated pools per content
  type) is otherwise treated as an industry default in design-practice writeups
  ([gamedeveloper.com — Defining Loot Tables in ARPG Game Design](https://www.gamedeveloper.com/design/defining-loot-tables-in-arpg-game-design)).

### 5.5 Representing extremely small probabilities without float precision loss

- **Diablo 2's engine is documented as integer-only end to end** — treasure-class bonuses resolve
  against a fixed denominator of 1024, and item-quality odds are integer ratios converted to a
  percentage only at final display. This is the real, shipped precedent for R3's proposed fix: keep
  the whole pipeline in integer weight/denominator form (exactly what `DropTableModel.cs` already
  does with `long`/`checked` arithmetic — B2/B10) and simply widen the **named unit** from
  per-100,000 to per-1,000,000 or finer. Source: [d2mods.info](https://d2mods.info/forum/viewtopic.php?t=44413).
- No studio was found to state "we use per-million weights specifically to avoid float precision loss"
  in those exact terms — the practice is strongly evidenced (Diablo 2's integer arithmetic, Warframe's
  documented display-only rounding) but not framed that way by any developer directly. Flagged rather
  than invented.

## 6. What the ideal shape looks like, given all of the above

Not a spec — a description of what "done" would mean, so a downstream `/spec` pass has a target:

1. **Every gameplay mode that can reward a player owns at least one real `loot_source` binding —
   decided in scope 2026-09-07 (D1), not deferred.** Today only item's own hand-authored corpus
   (`d1.json`–`d4.json`) and party-dungeon (designed, unbuilt) qualify. Base-defense and world-map each
   need a genuinely new `source_kind`/`table_id` binding — real, first-time build work per mode, since
   neither has any integration with the pipeline today (R1) — before "each gameplay mode has its own
   table" is true across all five modes.
2. **`DungeonLootTableGen` (and any sibling generator a future gameplay mode needs) grows from the
   flat, single-weight placeholder it is today into the rich generator its own spec already designs**
   — reading a room kind's `rarityFloor`/`rarityShiftRungs`, drawing from `bands.v1.json`'s
   `weightTable` for per-entry weighting, and differentiating `fight`/`elite`/`boss`/`cache`/`quest`
   the way §5 of `spec-dungeon-loot.md` already lays out (R2). This is squarely party-dungeon's own
   module, already approved — it needs building, not redesigning.
3. **The weight-unit convention widens from "per 100,000" to a unit fine enough to name 0.0001%
   without rounding to zero** — e.g. `dropWeightPerMillion`, or a `long` micro-percent field — applied
   first wherever an author actually wants a rate that fine (an ultra-rare table entry), not as a
   blanket rewrite of every existing per-100k weight (R3). The underlying draw math needs no change;
   this is a documented-unit and validation-tooling change.
4. **A new, named, tunable minimum-floor concept ships for individual ultra-rare entries**, independent
   of the rarity ladder — e.g. `MinDropRatePerMillion` in whichever tuning file owns the table it
   applies to, following T6 (carries its unit) and T5 (a missing value is a load rejection, never a
   silent default) from `tunables-ssot.md` (R4).
5. **`affix_channel` differentiation (X4) lands**, so a boss table's affix *composition*, not only its
   rarity floor/shift, differs from a trash table's (W1) — closing the gap Diablo 2's TC-bonus system
   and Diablo 4's separate Mythic-Unique table both already demonstrate is standard practice.
6. **Every new floor/rate stays inside the existing axes, never a new one**: volume still reads
   `Θ_actor` linearly with no upper bound (D26 untouched); quality still reads `P(Θ_content)`; a rarity
   floor moves *which rung*, never a magnitude (`ssot-rarity.md` §3.6 — "a multiplier on the rung makes
   rarity dominant and destroys the overlap," binding, do not violate it when authoring per-mode
   differentiation).

## 7. Owner decisions — 2026-09-07

All three open questions from the first draft of this document are now decided:

- **D1 — Base-defense and world-map ARE in scope, not deferred.** Both gain real `loot_source`
  bindings so they can drop items through this pipeline at all. This widens §4 R1 from "a gap worth
  naming" to "a gap a downstream `/spec` pass must close" — building the binding for two gameplay modes
  that today have zero integration with the pipeline is real, first-time build work for each, not a
  wiring gap. §6 point 1 is updated below to reflect this.
- **D2 — The 0.0001% floor is a per-entry floor, independent of the rarity ladder.** No new rung, no
  ladder review. A tunable minimum weight (e.g. `MinDropRatePerMillion`) applies to any drop-table
  entry a designer marks with it, following `tunables-ssot.md` T5 (a missing value is a load rejection,
  never a silent default) and T6 (carries its unit). The closed ten-rung ladder (`ssot-rarity.md` §3.4)
  is untouched — §6 point 4 already described this shape; it is now the decided one, not one of two
  options.
- **D3 — The floor stays opaque to players**, matching the genre norm every title in §5.2's research
  observed (no AAA title checked discloses its rarest-tier rate). No player-facing surface should
  display the exact per-mode floor value or name it as a distinct mechanic.

## 8. Adversarial audit, 2026-09-07 — one real interpretation gap surfaced, everything else fixed at the spec

Four independent reviews were run against this doc and its three module specs before moving to `/plan`.
Every concrete bug and factual error found was fixed directly in the specs (see each spec's own review
annotations). One open question survived the pass and belongs here, not buried in a module spec:

**D2 ("a per-entry floor, independent of the rarity ladder") was built as a universal REFUSAL gate — an
entry can never be configured rarer than the floor. It is not an authoring TOOL — nothing in
`spec-rate-floor.md` lets a designer actually place a new item at exactly 0.0001%.** The user's original
phrase, "make the lowest drop cap to 0.0001% and tunable," is genuinely ambiguous between these two
readings, and D2 resolved it toward the cheaper one (a safety rail) without that ambiguity being named
at decision time. `spec-rate-floor.md` §4 R3 (the weight-unit-precision gap that WOULD support fine-
grained authoring) was identified in the original idea pass and never turned into its own module — it
is still open. **Whether that's a real gap or an acceptable scope split is the owner's call, named here
so `/plan` does not silently inherit an assumption:** ship `rate-floor` as pure infrastructure now (no
player-facing payoff, confirmed and stated in `spec-rate-floor.md`'s own Success Criteria), and decide
separately whether a fourth module ("author a specific entry at the floor, on purpose") is still wanted.

**Resolved, same day:** a fourth module, `rate-authoring` (`docs/architecture/item/spec-rate-authoring.md`),
closes this gap — reasoned explicitly, not picked as the cheaper default this time. A plain "solve for
the Weight" calculator was considered and demoted to a secondary convenience: a weighted draw's share
is relative to its whole group, so a solved-for weight silently drifts the moment a future content pass
(quests, events — the owner's own named upcoming consumers) adds a new sibling entry to the same table.
The recommended default instead reuses this codebase's own existing pattern for "a rate that must stay
exact no matter what else is in the table" — D38's kill-drop roll (a flat, independent, separately-
streamed check) — as `IndependentRateEntry`/`RateAuthoring.Hit`. Build order revised: `rate-authoring`
moves ahead of `sector-loot-wiring`/`siege-loot` so both gameplay modes can author their first tables
with the real tool from day one.
