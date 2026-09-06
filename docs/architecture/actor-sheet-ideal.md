# ActorSheet — the ideal

**Status:** idea phase, 2026-09-07. Not a spec. No build authorized.

**Visual draft:** [13-actor-sheet.html](../design/13-actor-sheet.html) (owner: the new design is
good enough to graduate). Lawn landing: [spec-lawn-interactive.md](../design/spec-lawn-interactive.md)
§3. Sibling HUD (Band B tokens, not this sheet): [actor-hud-ideal.md](actor-hud-ideal.md) — **shipped**.

**This document supersedes, as intent, the 2026-08-29 six-tab map**
([actor-sheet-map.md](actor-sheet-map.md)): “Overview unchanged”, “derived tab is a doorway”,
“Actions/Passives are locked previews”. That map jumped from plate 08 to module specs without an
ideal. `/spec` rewrites the map. The five files under [actor-sheet/](actor-sheet/) stay as a
reasoning trail until replaced; they are not what to implement.

---

## Load-bearing (restated, not linked)

Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is. Dodge, shields,
elements, aptitudes, and derived channels already exist as `rpg.*`. The lawn may **observe** past
events and accept **signed deltas**. It does not need a Unity field for a sheet row to be legal.

Two async systems. Record-then-drain. Delay is the designed degradation, not a frame-budget argument
for hiding a number.

One power ladder: contests read `Θ` (linear, difference); magnitudes read `P(Θ)`. This sheet
**displays** those numbers. It does not invent a third curve.

The balance surface is `data/tuning/`. Player English is **not** a tunable. A balance pass does not
retitle Crit chance.

No hard progression ceilings. A spark that paints `CAP` on an uncapped `GameUnits` channel is a
silent wall (PS-8). Real registry caps (`status.resist.dot/cc/contagion` at the tuned 0.95) **do**
show `CAP`.

Gameless-first is capability: the sheet must work with Fusion closed (roster / Sanctum). The
injector may enrich a fielded unique (live HP, live shield stacks). It must not permanently gate
opening the sheet.

The player has **no class**. Twelve aptitudes are sources, never registered channels. Points go
anywhere at one price.

A game is a **stage with layers**, not a document with pages. ActorSheet is one canonical band-2
panel (`GG-9`), opened over whichever stage the player is already on. It is never a `#/actor/:id`
route.

---

## Which loop this extends

From [the-loops.md](../guide/the-loops.md):

| Loop | How the sheet serves it |
|---|---|
| **A. Level up and power** (spine) | Aptitude leftover + Confirm; derived families; Standing; specimen XP. This is the primary loop. |
| **C. Item collection** (spine) | Kit paper-doll (15 roles) and loadout. Empty until gear is known — honest empty, not a fake doll. |
| **1. Lawn** (place) | Same sheet, `lawn-bound` chrome, over the live canvas. Glance numbers that must be acted on under a wave obey **GG-60** (HUD), not this panel. |
| Combat depth (hangs on places) | Derived / Shield / Status / Elements tabs are the combat language, read at leisure. |

It does not invent a parallel “character menu” loop. It does not make the lawn the whole game: the
same sheet opens from Creatures and Commanders with Fusion closed.

---

## What this is

The one surface where you **look at a unique demon (or the commander) and spend power**.

You open it from the lawn dock, the roster, or the commander chip. Identity stays in the header.
Tabs partition a **no-scroll floor**. Selecting a tile or a stat fills a **right inspector** — it
does not push a dialog. Aptitude `+`/`−` drafts against a leftover; **Confirm** is the decision.
Derived rows show a player name, a number, a spark, and a `?`. Shield is three coloured wells, not
a paragraph. Status is glyphs. Empty leftover is legal.

Filename stays `ActorPanel`; export alias `ActorSheet`. `?sel=` is `instanceId` only. Commander
never a lawn tile. Wave generals are look-only.

---

## What already exists

Sorted **built / wiring gap / real gap**. A default-off, a pending adapter, a missing argument, or
an unread seed is a **wiring gap**, never a wall.

### Built

| Finding | Evidence |
|---|---|
| Twelve aptitudes, postures Force/Finesse/Bastion as a **read** | `AptitudeCatalog.Count = 3×4` ([Aptitude.cs:30-36](../../src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs)); `Reading` already authored on the row (`"Hit harder."`) |
| Commander allocate GET/POST, refuse overspend (never clamp) | [AptitudeEndpoints.cs:24-57](../../src/FusionRpg.Server/AptitudeEndpoints.cs); 409 `aptitudes.overbudget` |
| Same draft+save on the sheet Progression tab **and** the Aptitudes layer | [ProgressionTab.tsx:65-69](../../web/fusion-rpg-web/src/ui/actor/ProgressionTab.tsx); [useAllocationDraft.ts:61-67](../../web/fusion-rpg-web/src/hooks/useAllocationDraft.ts) |
| `ActorHub.Resolve` + live `GET /api/actors/{id}/derived` with contributions | [ActorHub.cs:39-48](../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs); [AuraDerivedEndpoints.cs:36-95](../../src/FusionRpg.Server/AuraDerivedEndpoints.cs) |
| DerivedStatsTab already consumes that feed (raw `channelId`) | [DerivedStatsTab.tsx:22-60](../../web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx) |
| Six-tab `ActorPanel` shell already ships | [ActorPanel.tsx:16-26](../../web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx) |
| UniqueActor identity + DemonProfile (two concrete elements, nickname) | [UniqueActorDtos.cs:15-29](../../src/FusionRpg.Contracts/UniqueActorDtos.cs); [DemonDtos.cs:6-21](../../src/FusionRpg.Contracts/DemonDtos.cs) |
| Six resource **ids** registered | `hp stamina hunger spirit qi poise` ([DerivedStatChannels.cs:521](../../src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs)) |
| 24 locked statuses | [status-ssot.md](status-ssot.md) §9 |
| Shield stack: max 3, drain priority, `GetShields` / `Totals` | [ShieldRuntime.cs:220-243](../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs); [ShieldPolicy.cs:17](../../src/FusionRpg.Core/Combat/Shield/ShieldPolicy.cs) |
| Band B HUD already shows shield + status strip (not the sheet) | actor-hud program, implemented 2026-08-31 |
| `status.resist.omni` uncapped; `.dot/.cc/.contagion` cap = tunable 0.95 | [DerivedStatRegistry.cs:98-111](../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs); [derived-stats.v2.json](../../data/tuning/derived-stats.v2.json) `categoryResistCap` |
| Thirteen `UnitClass` ledger; six sheet render states | [spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3; [spec-derived-stat-sheet.md](../design/spec-derived-stat-sheet.md) §3 |
| Equipment GET exists | [UniqueActorEndpoints.cs:79-82](../../src/FusionRpg.Server/UniqueActorEndpoints.cs) |
| Aura enable/disable on Actions tab (commander auras) | [ActionsTab.tsx:38-42](../../web/fusion-rpg-web/src/ui/actor/ActionsTab.tsx) |
| Passives tab is a real tree surface, not a locked grid | [PassivesTab.tsx:45-52](../../web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx) |
| Lexicon seed authored | [lexicon.v1.json](../../data/seed/derived-stats/lexicon.v1.json) |
| Plate 13 InspectSplit + leftover + radials (HTML only) | `docs/design/13-actor-sheet.html` |
| GG-62 / GG-63 / GG-64 + D11–D14 | [game-gui-principles.md](game-gui-principles.md) §16, §20.5 |

`progression.bonus.*` is composed into `AppliedCombat` ([ActorHub.cs:89-112](../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs)).
`EntityStatWriter` not writing a Unity defense field is **not** a sheet blocker. Overlay combat
already reads the derived channel.

### Wiring gap

| Finding | The inert line |
|---|---|
| `channelSummary` / `xpToNext` / `shieldStack` / `elementTyping` / `displayName` / `equipSlots` always pending | [adapt.ts:176-185](../../web/fusion-rpg-web/src/contract/adapt.ts) — `pendingWithReason(...)` regardless of input |
| `xpToNext` math exists; DTO has no field | [RpgProgression.cs](../../src/FusionRpg.Core/Progression/RpgProgression.cs) `XpToNext`; UniqueActorDto stops at `xp` |
| `channelLabel` still `idWords` the dotted id | [adapt.ts:898-899](../../web/fusion-rpg-web/src/contract/adapt.ts) `"Named gap"` / `return idWords(channelId)` — lexicon unread by `src/`, `web/`, `tests/` |
| Live derived list prints `c.channelId` | [DerivedStatsTab.tsx:55](../../web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx) |
| “Open full derived-stat sheet” is a disabled button | [DerivedStatsTab.tsx:65-71](../../web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx) |
| `/derived` merge is Commander + DemonType only — UniqueDemon baseline omitted | [AuraDerivedEndpoints.cs](../../src/FusionRpg.Server/AuraDerivedEndpoints.cs) (SpeciesAllocationSource; AptitudeEndpoints comment: commander only) |
| UniqueDemon allocate: store + `UniqueDemonAllocation.Baseline` exist; no player POST | [AptitudeEndpoints.cs:12-18](../../src/FusionRpg.Server/AptitudeEndpoints.cs) |
| Species GET exists; sheet does not use it | [AptitudeEndpoints.cs:67-72](../../src/FusionRpg.Server/AptitudeEndpoints.cs) |
| Shield layers live in Core; no player `GET /api/actors/{id}/shields` | debug/sim only |
| Six pools in Core; web `ResourceId` omits `poise` | [types.ts:664](../../web/fusion-rpg-web/src/contract/types.ts) five ids |
| `ResourceView` typed, no component binds it | [types.ts:666-672](../../web/fusion-rpg-web/src/contract/types.ts) |
| GearTab empty-states while RelicsLayer already fetches equipment | [GearTab.tsx:22-26](../../web/fusion-rpg-web/src/ui/actor/GearTab.tsx) |
| Release/Deploy footer only closes the panel | [ActorPanel.tsx:97-102](../../web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx) — deploy API exists elsewhere |
| Lawn HUD fold has meters unused by Inspector/Phaser | [foldActorHud.ts:66-78](../../web/fusion-rpg-web/src/features/lawn/foldActorHud.ts) |
| `unitClass` / `cap` / `composeSentence` have no server producer on `ActorChannelDetail` | DerivedStatsTab comment `:16-18` |
| `channelSummary` richer Pending unused; live path is a second DTO | [types.ts:586-597](../../web/fusion-rpg-web/src/contract/types.ts) |

### Real gap

| Finding | What would have to be built |
|---|---|
| React `StatRow`, `InspectSplit`, leftover meter, shield radials, status glyphs as **shared kit** | New components. Kit CSS on the plate is not React. |
| `ActorView` has no `resources`, `statuses`, or `actions` | Type + adapter + a projection. Not a Unity rewrite. |
| StatusDef has no `DisplayName` | Join lexicon at the adapter (preferred) or add a field. Catalog stays code-first ids. |
| Regular action slots | Action corpus + `ActionSlot` bind. Plate 13 Kit loadout is the **slot chrome**; [action-ideal.md](action-ideal.md) is sealed and this program does not reopen it. Placeholder Strike/Firebolt stay forbidden (GG-23). |
| Aspect-scope aptitude | **Reverted, not authorized.** Do not start. |
| Promote | Owner: ignore. No placeholder. |
| Unified ActorSheet DTO (identity + pools + live statuses + shield layers + derived + lex names) | One server projection or a documented fan-in of existing GETs. Today the player hits five pending notes plus a raw-id list. |

**Not a real gap:** “the lawn cannot show defense / dodge.” Overlay combat already consumes those
families. An inert FE row is a **wiring gap**.

---

## Prior art

Layout only. Genre pitch is already locked (RPG + empire; lawn first core). Sources below; unverified
wiki numbers are marked.

| Steal | From | Failure mode if copied wrong |
|---|---|---|
| Left-docked sheet over live play | Path of Exile 2 character panel ([Mobalytics](https://mobalytics.gg/poe-2/guides/character-sheet); [wiki](https://www.poe2wiki.net/wiki/Character_Panel)) | Fine as a **layer**. PoE2 then hid **all offensive stats** off `C` onto the Skills panel (`G`) — players could not find accuracy/crit ([official forum](https://www.pathofexile.com/forum/view-thread/3599334)). **Do not split Derived off this sheet.** |
| Pool tokens first | PoE2 Life / ES / Mana | Condition tab, not the HUD under a wave (GG-60). |
| One-open collapse | Diablo IV stats | Many-open wiki lists as first paint. |
| Inspector, not a nested dialog | BG3 Examine, Genshin Details | Dialog-in-dialog burns GG-10’s last push (GG-63). |
| Unspent + plus | Grim Dawn / Titan Quest attribute/skill plus | Grim Dawn **commits on click** and refunds at an NPC ([Steam thread](https://steamcommunity.com/app/219990/discussions/0/601896014188668803/)). We already have a **draft + Confirm** (`useAllocationDraft`) because overspend must **refuse**, not clamp. Steal the leftover *readout*, not instant commit. |
| Shield as colour on HP | Slay the Spire block, Hades armour, Last Epoch ward | Six fake-cap radials on uncapped magnitudes. |
| Icon grid for loadout | HoMM3 spellbook | Full-screen book covering the board. |

**Do not steal**

- Diablo IV **Paragon hard cap of 220** ([GamesRadar](https://www.gamesradar.com/diablo-4-paragon-board-points-glyphs-gates-explained/)) — a progression ceiling. Aptitude leftover empty is **legal**; the budget is not fill-or-lose.
- D4 **auto-path / auto-grant** as if it were leftover. Manual `+` is the product (free build).
- Last Epoch **+ on attributes with no leftover pool** — our PointBudget is real.
- PoE2 / Last Epoch **page-length derived lists** as first paint. Completeness lives behind Show unchanged + collapse (spec-derived-stat-sheet §5.2).
- Painting `CAP` on uncapped GameUnits. `status.resist.omni` is the worked example of uncapped sitting next to capped siblings.

Screenshot URLs used for plate 13: [actor-sheet-ui-refs-2026-09-07.md](../research/actor-sheet-ui-refs-2026-09-07.md).

---

## The shape

**Eight tabs, InspectSplit, leftover footer, lexicon names.** That is plate 13. Alternatives
rejected in this phase (not re-opened at `/spec` unless the owner reverses):

| Rejected | Why |
|---|---|
| Six-tab map (Overview / Progression / Derived doorway / locked Actions / locked Passives / Gear) | Already shipped as a stub bar. Condition/Shield/Status/Elements have nowhere to live. Derived *is* the sheet, not a door. Passives and auras are no longer locked. |
| Nested aptitude DialogShell | GG-10: cell → dock → sheet is two pushes. A paragraph is not a third. Confirm stays a decision control (GG-22 / GG-63). |
| Channel-id tables as first paint | GG-62 / GG-64. Completeness in the inventory below the mock, not the glance. |
| Third channel classification | Six render states + thirteen `UnitClass` already exist. 2026-08-24 incident. |
| Display copy in `data/tuning/derived-stats.v2.json` | T1: that file is `categoryResistCap` and `turnDefaultSpeed`. |
| `idWords` on a player band | GG-62. Developer tree only. |
| Sun **bank** meter on the sheet | Match-scoped `pvz.*`. Actor `hunger` (plant label **Sun**) is the pool. |
| Separate `LawnActorSheet` vs `RosterActorSheet` programs | GG-9. One sheet, a role prop. |
| `#/actor/:id` | GG-1. `?panel=…&sel=<instanceId>` over the current stage. |

### Tabs (player words)

| Tab | First paint | Inspector / footnote |
|---|---|---|
| **Condition** | HP radial with shield overlay · six coloured meters · five-axis Standing · live status glyphs | Glance. No dialog. |
| **Aptitudes** | 3×4 tiles, `+`/`−`, leftover meter, Reset / Confirm | Right: aptitude `Reading` + what the share feeds (lexicon family names). Commander-scope copy until UniqueDemon allocate wires. |
| **Derived** | Category segs, one collapse open, StatRow, Show unchanged | Right: unit sentence, compose sentence, cap or “more still counts”, sources (GG-49). Six states from spec-derived-stat-sheet §3. |
| **Shield** | Three instance radials (empty well dashed) | Omni shield StatRows. Full 28×7 matrix stays on Derived → Shield category. Noun is **Shield**, never Ward. |
| **Status** | Glyphs (live / catalog / mastery segs) | Lexicon reading. Mastery is player-lifetime, not a live stack. |
| **Elements** | Six coloured radials + mastery StatRows | Two concrete types from profile. Omni is a baseline, not a seventh chip. |
| **Kit** | ActionSlot row (same chrome as the lawn combat book) + 15-role doll | Auras already live above regular actions. Regular slots stay locked-with-reason until the action corpus ships. Gear binds equipment GET. |
| **Paths** | Species bloodline vs shared corpus — node track, not a paragraph | Tree surface may push once (`spec-tree-surface.md`). That is GG-10’s last push. |

Header is always on: portrait, name, species, side, level, two elements, Fielded/Wave, Esc.

### Shared widgets (one implementation)

`AptitudeTile` · `StatRow` · `InspectSplit` · `LeftoverBar` · `ShieldLayer` (radial) · `StatusGlyph`.
The inspector is **not** a stack push. Esc pops the **sheet**.

### Bindings that stay true

- Aptitude is a **source share**, never a `DerivedStatCatalog` id.
- Allocation is the **sum of scopes**; `share` is on the sum. Aspect scope is **reverted**.
- Combat families are a **28 × (omni+6)** matrix, same shape as the generator.
- `omni` column is visually first and separated.
- Spark fill-to-100% only for pools, bounded ratios, and registry caps (D14 / GG-64).

---

## Tunables

This program introduces **no new balance numbers**.

| Number the sheet shows | Home | Notes |
|---|---|---|
| `categoryResistCap` (0.95) | `data/tuning/derived-stats.v2.json` | Already the only compose-time clamp. UI paints `CAP` from the **registry cap**, not a CSS guess. |
| Aptitude PointBudget | existing class-system / `point-economy` | Leftover = budget − spent. Empty leftover is legal. Overspend is 409, not a silent min(). |
| Shield max 3, drain priorities | `ShieldPolicy` (structural / already tuned) | Not retuned here. |
| Six pool ids | structural | Plant label Sun for `hunger` is **content**, not a channel id. |

Lexicon `displayName` / `reading` / `gauge` live in **seed**, not tuning. Aptitude one-liners
already live on `AptitudeRow.Reading` — do not fork a second English table for the twelve. Derived
families, statuses, and resources read the lexicon.

---

## What this deliberately does not decide

- UniqueDemon (and DemonType) allocate **on this sheet in v1**, vs commander-only leftover until a
  specimen picker exists. Core can persist UniqueDemon today; the player cannot reach it.
- Whether `AptitudesLayer` (rail) **retires** once the sheet leftover ships, or stays a shortcut.
  GG-9 prefers one home; a shortcut that opens the **same** tab is not a second surface.
- Action corpus content, costs, targeting — sealed in [action-ideal.md](action-ideal.md). Kit shows
  slots.
- Passive-tree lattice beyond the Paths glance — [passive-tree-ideal.md](passive-tree-ideal.md).
- Aspect-scope (reverted).
- Promote.
- HUD sliver / numeric Band B readout — actor-hud already decided **off** for v1 (GG-60).
- A new `f(level)` or a thirteenth aptitude.

---

## Open questions — owner only

Answerable. Not a template dump.

1. **Graduate plate 13 as the visual SSOT for `/spec`?** Recommendation: **yes.** The six-tab map is
   already false against shipped code (Passives live, auras live, derived live-but-ugly).
2. **v1 leftover: commander-only (today’s POST) on every sheet, with UniqueDemon as a later role
   chrome — or block Confirm on unique-scope until that POST exists?** Recommendation: **commander
   leftover in v1**, same as ProgressionTab, with an honest scope chip. Do not pretend a specimen
   spend that the server will ignore.
3. **Commander HUD chip tap opens this same sheet in v1?** Recommendation: **yes, with a “this
   match” banner** — GG-9. Optional delay was listed in spec-lawn-interactive; it is not a second
   product.

Once those three are answered, `/spec` rewrites `actor-sheet-map.md` and replaces the five stale
module specs. The first wiring patch that does not need a new module is: **`channelLabel` reads
lexicon `displayName`**.
