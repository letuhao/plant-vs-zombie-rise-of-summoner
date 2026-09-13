# Capability map: empire-development (idea-phase index)

**Status: BOTH SUB-PROGRAMS FULLY SPEC'D, 2026-09-13.** Owner-named umbrella (*"this program should
named empire-development and should have multiple sub program, that we can easier to manage and extend
later"*), created after two independent idea-phase docs (`loam-relics-and-wonders-ideal.md`,
`scoped-inventory-hierarchy-ideal.md`) grew large enough in one session that leaving them as flat,
unrelated files would have made them hard to find and easy to duplicate. **Originally an idea-phase-only
index (no build order, no waves, no gates locked here); superseded in that respect the same session by
`/spec` proceeding on both sub-programs** — `scoped-inventory-hierarchy-map.md` (4/4 modules spec'd) and
`loam-relics-and-wonders-map.md` (4/4 modules spec'd) are both approved and complete. This doc's own job
is unchanged: name the sub-programs with stable ids, show how they depend on each other and on what
already exists, and be the one place a future session looks first. See each sub-program's own map for
its build order/waves/gates — those are no longer absent, just not duplicated here.

## What this program is

The strategic layer above a single sector: Wonders and relics (a rare-material economy that funds
special buildings across a Sector→Empire→World→Multiverse scope ladder), and the scoped-inventory
hierarchy that lets a legion physically carry those relics from wherever they drop to the sector where
a Wonder gets built. Both sub-programs below extend **Place 3 — "Farming, hunting, and defending the
empire"** (its "buildings and wardens" WIP line, `the-loops.md:85`) and **Place 5 — "World stage
empire building"** (its own WIP line, `the-loops.md:117`) — **corrected (strengthen pass 2026-09-13,
finding F19)**: previously cited as one broad `the-loops.md:83-121` range spanning three different
"Places" for two separate claims; verified against the file and tightened to the precise line for
each claim. Both sub-programs are corrections/extensions to the **sealed** `loam-map.md` capability
map, not greenfield features — read that map's own §6d ("the G-F reward hole") before proposing
anything new under this umbrella.

## Sub-programs

Stable kebab-case ids. **Both fully spec'd 2026-09-13** — capability maps approved, all module specs
written.

| Program id | Doc | Scope | Status |
|---|---|---|---|
| `loam-relics-wonders` | [loam-relics-and-wonders-ideal.md](loam-relics-and-wonders-ideal.md) → [loam-relics-and-wonders-map.md](loam-relics-and-wonders-map.md) | Relics (rolled, never-equipped unique items — shipped as a new sibling `DropEntryKind.Relic`/`ContainerKind.Relic`, not a `unique` `KindSpec` variant) fund Wonders (an orthogonal `StructureDef` facet, not a new `StructureKind` value) across `Sector`/`Empire` scope this wave (`World`/`Multiverse` named-but-refused). **4/4 modules spec'd**: [relic-item-kind](loam-relics-and-wonders/spec-relic-item-kind.md), [wonder-structure](loam-relics-and-wonders/spec-wonder-structure.md) (wave 1); [wonder-effect-empire](loam-relics-and-wonders/spec-wonder-effect-empire.md), [wonder-build-flow](loam-relics-and-wonders/spec-wonder-build-flow.md) (wave 2). `wonder-build-flow`'s Data-side half is specced but cannot run end-to-end until `scoped-inventory` ships. | **Spec'd, unbuilt.** Two filed cross-program asks still open: a `unique`-`KindSpec`-relaxation-or-sibling-kind ask (resolved internally as a new sibling kind, no cross-program change needed after all — see `spec-relic-item-kind.md`); `cargo-fate`'s own `place_kind` widening ask against `deployment-hierarchy`'s `corpse-cache` (unrelated program, still open). |
| `scoped-inventory` | [scoped-inventory-hierarchy-ideal.md](scoped-inventory-hierarchy-ideal.md) → [scoped-inventory-hierarchy-map.md](scoped-inventory-hierarchy-map.md) | Five inventory scopes — empire (built), unique-actor (built), delve-party (specced, `loot-pack`), legion (new slot+weight cargo overlay, every member regardless of role), sector (new slot-bound `StructureKind.ItemStorage`, no owner column — capture-transfer is a no-op by construction). Cross-faction transfer stays a named real gap, deferred. **4/4 modules spec'd**: [legion-cargo](scoped-inventory-hierarchy/spec-legion-cargo.md), [sector-storage](scoped-inventory-hierarchy/spec-sector-storage.md) (wave 1); [cargo-transfer](scoped-inventory-hierarchy/spec-cargo-transfer.md), [cargo-fate](scoped-inventory-hierarchy/spec-cargo-fate.md) (wave 2). | **Spec'd, unbuilt** — the hard external gate `loam-relics-wonders`' own `wonder-build-flow` module is blocked on. |

## Dependency direction — one real edge, no cycles

```
scoped-inventory  →  loam-relics-wonders
```

**`loam-relics-wonders` is hard-blocked on `scoped-inventory`.** A legion cannot deposit a carried
relic at a sector — the mint → carry → deposit → spend flow `loam-relics-wonders` itself depends on —
until sector-scoped item storage exists (confirmed this session: `rpg_item`/`rpg_item_stock` are
player-scoped only, `RpgStore.Items.cs:85-107`, no sector or legion column anywhere). This edge is
already named in both docs independently; this map just makes it visible at a glance. Neither
sub-program depends on anything else new — both consume the **already-sealed** `loam-map.md` program
(`StructureCatalog`, `LoamProduction`) and the **already-built** World/`WorldFaction` persistence model
(`RpgStore.World.cs`) as read-only foundations, not as things this map tracks as in-progress work.

## What this program is not

- **Not a rewrite of `sector-development` or the sealed loam program.** Both sub-programs reuse
  `StructureCatalog`/`LoamProduction`/`StructureDef` wholesale — extending the seed corpus and adding
  fields, never forking a parallel structure system.
- **Not multiplayer, and not the multi-empire program itself.** Cross-faction trade in `scoped-inventory`
  is designed so the *schema* doesn't close the door on a third empire (keys on the already-generic,
  already-iterable `WorldFaction`/`FactionId` string model), but building an actual third empire is its
  own "serious program for multiple empire gameplay mechanism" (owner's own words) — tracked as an
  external dependency, not a sub-program of this map.
- **Not the world-map/siege death paths, notification system, or anything from the sibling
  `deployment-hierarchy`/`notification-ssot` programs.** Those are separate umbrellas from the same
  session; cross-referenced where a real dependency exists, never folded in here. **Added (strengthen
  pass 2026-09-13, finding F23):** `deployment-hierarchy` and this program do touch the same
  `rpg_item`/`rpg_item_assignment` ownership surface, from opposite directions — this program moves
  items *into* legions/sectors (carry, deposit), `deployment-hierarchy` moves them *out* of a specimen
  on death (corpse-drop). Named here as a **related, not a dependency** — no formal dependency edge or
  sub-program relationship is created by this note.

## The unlock-gate question — answered at this level, not per sub-program

`scoped-inventory`'s own open question #4 ("does sector/legion cargo wait for the armoury's Dave-level
unlock chapter?") was explicitly pushed up to this map by the owner: *"we will unlock in this huge
programe... [empire-development]... that we can easier to manage and extend later."* Read plainly: the
unlock gate is **`empire-development`'s own progression milestone, not the existing armoury chapter's**
— but naming the *exact* milestone (a turn count, a sector-development tier, a quest) is not yet
answerable, because neither sub-program has enough shape yet to know what "empire-development is
underway" should mean mechanically. This is recorded here as **settled in direction, open in specifics**
— revisit once `loam-relics-wonders`' `Sector`/`Empire` scope work is closer to `/spec`, since that is
the sub-program most likely to define the milestone in practice (e.g. "unlocks the turn a player's
first `Sector`-scope Wonder becomes buildable").

## Open items carried from the sub-programs

Not repeated in full — each doc owns its own list. Flagged here only so a future session does not have
to open both files to know whether anything is still blocking:

- `loam-relics-wonders`: **now fully spec'd (4/4 modules, 2026-09-13)** — most items below this bullet
  originally were resolved during `/spec`; kept here with their resolution noted rather than deleted,
  so a reader trusting only this umbrella doesn't need to re-derive what changed. Which exact
  loop/table gets each relic row and at what rate — **still genuinely open**, `relic-item-kind`'s own
  spec-time content call. The `unique` `KindSpec` schema question — **resolved**: a new sibling
  `relic` kind, `unique` untouched (`spec-relic-item-kind.md`); genuinely open follow-on, not
  previously named — does this need the item program's own sign-off before it ships (strengthen-pass
  finding, 2026-09-13)? Whether `World`-scope uniqueness needs a wonder-race mitigation — **resolved**,
  yes, Zomboss can compete (ideal doc Q3). Whether `World` scope ships in the same wave as
  `Sector`/`Empire` — **resolved**, no: `Sector`/`Empire` only this wave, `World` deferred to its own
  future map. **Still open, its own self-declared biggest real gap, Open question #7 — the
  `Multiverse`-scope player ledger design** (a new player-scoped table surviving a world ending, named
  as a category and a seam only, not designed).
- `scoped-inventory`: **now fully spec'd (4/4 modules, 2026-09-13).** Which loop/milestone unlocks
  sector and legion cargo (see above — direction set, specifics pending); the legion-cargo per-unit
  `carryWeight`/`backslot` stat and its seedsmith extension are named as real, deferred future work,
  not blocking this program's first build; **Open question #6 — cross-faction trade's consent/offer
  flow** (spawned when Q3 was resolved/corrected: the schema keys on `FactionId` rather than the closed
  `CommanderId` enum, but a buildable transfer mechanism and its consent/offer flow both remain open).
- **New, both sub-programs (strengthen pass on the finished specs, 2026-09-13): no notification hook is
  named anywhere.** All 8 module specs are silent on `notification-ssot-ideal.md` despite each
  introducing a clearly delayed-discovery or state-change event a player could easily miss: a legion's
  cargo silently becoming a decaying, revisit-lootable cache on death (`cargo-fate`); a sector's stored
  items changing hands the instant it's captured (`sector-storage`); a Wonder finishing construction or
  a `build` order refusing on `wonder.cap-reached` (`wonder-build-flow`). Named-but-not-built callouts
  were added to `spec-sector-storage.md` and `spec-wonder-build-flow.md` this session; `spec-cargo-fate.md`
  is getting the same treatment as part of resolving its own `place_kind` ask (in progress). Not
  blocking — every event already fires a real `TurnReportEntry`/report line a future notification
  consumer can read, nothing needs to be re-plumbed, only picked up.

## Next step

**Both sub-programs are now fully spec'd — this is a build-order question, not an ideation one.**
`scoped-inventory` has no internal build dependency on `loam-relics-wonders` and should build first:
its own module specs are self-contained, and `loam-relics-wonders`' `wonder-build-flow` module cannot
run end-to-end until `scoped-inventory`'s `rpg_world_entity_cargo`/`rpg_world_sector_storage` tables are
real. `loam-relics-wonders`' other three modules (`relic-item-kind`, `wonder-structure`,
`wonder-effect-empire`) have no such block and can build in parallel with `scoped-inventory`. A real,
previously-unflagged wiring gap was found and closed in `wonder-build-flow`'s own spec along the way:
`BuildResolver` (the peacetime `build`-order resolver) never read or spent `ConstructRubbleCost`/
`ConstructIronworkCost` for any structure, Wonder or not — only the siege-only `ConstructionActions.cs`
path did. Worth a build-time regression check independent of Wonders.

## Corrections log — strengthen pass 2026-09-13

Four independent lenses (mechanism soundness · cross-program/economy · hard-rule compliance ·
spec-quality-vs-bar) ran this session. See the four lens reports this session for the full
checked-and-held list; refuted probes are not repeated here since they were not handed to this pass.

| # | Lens | Finding | Disposition |
|---|---|---|---|
| F1 | cross-program/economy | `scoped-inventory` sub-program summary overclaimed cross-faction transfer as "in scope" without qualifying that the mechanism itself is a real gap | Row corrected: schema choice resolved, transfer mechanism downgraded to real gap, consent/offer deferred regardless |
| F13 | spec-quality-vs-bar | "Open items carried from the sub-programs" omitted `scoped-inventory`'s Q6 (cross-faction consent/offer, spawned by Q3's resolution) and `loam-relics-wonders`' Q7 (the Multiverse-scope ledger, that doc's own self-declared biggest real gap) | Both added to the list |
| F14 (ripple) | spec-quality-vs-bar | `scoped-inventory` row said "one remains genuinely open," undercounting after F1's correction | Corrected to "two remain genuinely open" |
| F19 | spec-quality-vs-bar | Cited a broad `the-loops.md:83-121` range spanning three "Places" for two separate claims | Tightened to the precise line for each claim (Place 3's WIP line `:85`, Place 5's own WIP line `:117`), verified against the file |
| F23 | cross-program/economy | "What this program is not" disclaimed `deployment-hierarchy` as a separate umbrella but did not acknowledge both programs touch the same `rpg_item`/`rpg_item_assignment` ownership surface from opposite directions | Added a one-sentence "related, not a dependency" note — no formal dependency edge created |
