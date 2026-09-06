# ActorSheet — the ideal

**Status:** idea phase, strengthened 2026-09-07 (catalog-driven surface). Not a spec. No build
authorized.

**Visual draft:** [13-actor-sheet.html](../design/13-actor-sheet.html) (owner: the new design is
good enough to graduate). Lawn landing: [spec-lawn-interactive.md](../design/spec-lawn-interactive.md)
§3. Sibling HUD (Band B tokens, not this sheet): [actor-hud-ideal.md](actor-hud-ideal.md) — **shipped**;
glyphs/names amend with this catalog SSOT.

**Capability map:** [actor-sheet-map.md](actor-sheet-map.md) — catalog-first modules (rewritten
2026-09-07). The five files under [actor-sheet/](actor-sheet/) stay as a 2026-08-29 reasoning trail;
they are not what to implement.

---

## Load-bearing (restated, not linked)

Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is. Dodge, shields,
elements, aptitudes, and derived channels already exist as `rpg.*`. The lawn may **observe** past
events and accept **signed deltas**. It does not need a Unity field for a sheet row to be legal.

Two async systems. Record-then-drain. Delay is the designed degradation, not a frame-budget argument
for hiding a number.

One power ladder: contests read `Θ` (linear, difference); magnitudes read `P(Θ)`. This sheet
**displays** those numbers. It does not invent a third curve.

The balance surface is `data/tuning/`. **Runtime catalogs** (names, readings, icons, roster
membership) also live in `data/tuning/` — see § Runtime catalog SSOT. A balance-number file and a
catalog file stay separate so a rename cannot hide a combat golden (T7).

No hard progression ceilings. A spark that paints `CAP` on an uncapped `GameUnits` channel is a
silent wall (PS-8). Real registry caps (`status.resist.dot/cc/contagion` at the tuned 0.95) **do**
show `CAP`.

Gameless-first is capability: the sheet must work with Fusion closed (roster / Sanctum). The
injector may enrich a fielded unique (live HP, live shield stacks). It must not permanently gate
opening the sheet.

The player has **no class**. Aptitudes are sources, never registered channels. Points go anywhere at
one price. The count of aptitudes is whatever the injected catalog lists (today twelve); DESIGN-GATE
and residual-fit must move with any widen.

A game is a **stage with layers**, not a document with pages. ActorSheet is one canonical band-2
panel (`GG-9`), opened over whichever stage the player is already on. It is never a `#/actor/:id`
route.

**Near-fullscreen shell (owner, 2026-09-07):** the sheet is information-dense and must use most of
the viewport. Today's default `PanelShell` (`min(640px,92vw)` × `min(720px,82vh)`) is too small for
eight catalog tabs + InspectSplit. ActorSheet uses a **dedicated larger bound** (see map / shell
spec) with stage margins still visible — not a full-bleed stage replacement (GG-1) and not an
unbounded grow (GG-61: body scrolls, footer sticky). “Actor HUD modal” in product speech means this
sheet; Band B lawn tokens stay compact (GG-60).

---

## Runtime catalog SSOT

**Owner lock, 2026-09-07:** every identity the player sees on ActorSheet and Actor HUD — derived
families, resources, primary aptitudes, elements, statuses, kit role labels — loads from versioned
files under `data/tuning/`. Hosts inject; FE and HUD iterate. No hardcoded roster unions in the FE.

### Why this amends the old “English is not a tunable”

[tunables-ssot.md](tunables-ssot.md) T1 asked whether a *balance pass* would change a *number*. Copy
was parked in `data/seed/derived-stats/lexicon.v1.json` (D11, 2026-09-07). That split still left the
FE rebuilding to retitle Crit chance or to add a StatRow — the same tax as a magic number. Names,
readings, icons, and roster membership are therefore a fourth class next to Tunable / Structural /
Literal:

> **Runtime catalog** — identity + player English + presentation tokens for a vocabulary the FE/HUD
> iterate. Lives in `data/tuning/<domain>-catalog.v{n}.json`. A copy-only bump must not move combat
> goldens (T7).

**Retracted:** “Player English is not a tunable” and “lexicon lives only under `data/seed/`.” GG-62
still forbids `idWords` on a player band; the file path is now the tuning catalogs.

### What stays locked

| Lock | Meaning here |
|---|---|
| **T7.2** | Core never reads a file. Server and Injector parse JSON and `Configure(...)`. Tests construct objects inline. |
| **T5** | Unknown `StatusKind` / compose kind / `UnitClass` / tab kind / incomplete element matrix / aptitude–channel collision = **load reject naming the key**, never a silent default. |
| **T4** | `publish.py` writes `v{n+1}`. No hand-edit of live files after the first seed publish. |
| **One domain, one file for numbers** | Copy does **not** go into `aptitudes.v7.json` or `derived-stats.v2.json`. |
| **GG-1 / GG-9 / GG-60 / GG-63 / GG-64** | HUD stays Band B tokens; full numbers stay on this sheet. |
| **No third channel classification** | Six render states + thirteen `UnitClass`. |
| **Item program owns role ids** | ActorSheet does not fork `ItemRole` / `core.v1.json` budget weights. |
| **No hot-reload required** | Startup load + restart is enough. |
| **Action corpus sealed** | Kit shows slots the catalog lists; [action-ideal.md](action-ideal.md) is not reopened. |

### File layout

| File | Owns | Does not own |
|---|---|---|
| `data/tuning/aptitude-catalog.v1.json` | Rows: id, posture, ordinal, displayName, role, reading | Edges / pointEconomy (`aptitudes.v7.json`) |
| `data/tuning/derived-stat-catalog.v1.json` | Families, expand axis, compose, unitClass, displayName, reading, icon, gauge, sheet group, cap **ref** | `categoryResistCap`, `turnDefaultSpeed` (`derived-stats.v2.json`) |
| `data/tuning/status-catalog.v1.json` | Ids: kind, categories, stacking, payload kinds, displayName, reading, hudToken, color | Policy numbers (`status.v1.json`) |
| `data/tuning/resource-catalog.v1.json` | Ids, class, exhaustion, actionCost, plant/zombie labels, icon, color, meter kind | Pool math |
| `data/tuning/element-catalog.v1.json` | Concrete roster + omni presentation (name, ordinal, color). Matchup **completeness** checked at load | Shield vs combat matrix *values* |
| `data/tuning/actor-sheet.v1.json` | Tab **order**, labels, which catalog groups bind to which **kind**, default-open group, kit role ids + `(role_id, frame)` display words | New renderer kinds; item budget weights (`core.v1.json`) |
| `data/tuning/actor-hud.v3.json` | Geometry (keep); stop inventing glyphs here — HUD reads `hudToken`/`color` from status/resource catalogs | Gameplay |

Seed `catalog.json`, `lexicon.v1.json`, and `data/seed/*/roster.json` become **generated check
mirrors** (or are deleted once tools read tuning). `SeedCatalogMatchesCode` becomes “injected catalog
matches live registry.”

### Load path

```text
Host reads data/tuning/*-catalog.v{n}.json
  → Parse (pure, string in) → XxxCatalogHub.Configure
Core registries Register() by iterating the injected object
GET /api/catalogs/actor-surface
  (one fan-in DTO: tabs + aptitudes + families + resources + elements + statuses + kit roles)
FE / HUD iterate. Unknown id on a player band → designed placeholder, never idWords.
Injector loads the same catalogs (plugin folder copy).
```

Missing catalog file at host startup is a load reject (T5). Kind/payload/`UnitClass`/compose enums
stay closed in C# — JSON may only name existing ones.

### What FE may hardcode vs must not

**Structural renderer kinds (closed, in code):** Condition, Aptitudes, Derived, Shield, Status,
Elements, Kit, Paths. A catalog row may **order, hide, and retitle** those kinds. A ninth `kind`
with no React renderer is a load reject.

**Must not hardcode:** family list, resource union, status union, aptitude ids, element ids, gear
role labels, StatRow membership, HUD initials/colors, tab **labels**. `ResourceId = five strings` in
`types.ts` is the defect this exists to kill.

Plate 13 stays the **visual** SSOT for how a kind looks. Membership of rows inside a kind comes from
the catalog.

### File-save + restart vs still needs code

| Change | `publish.py` + restart, no FE | Needs code (and DESIGN-GATE count updates) |
|---|---|---|
| Rename Crit chance / Butter; new icon, hudToken, reading | yes | |
| New combat **family** on existing element axis, existing compose kind, **existing consumer** | yes | |
| Sparse `status.power.{id}` for an existing status | yes | |
| New status id whose kind/payload already exist (including overlay `ModifyStat`) | yes | UnityCc with no FA2 case is a def error |
| New aptitude row | catalog can list it | residual-fit, `aptitudes.v{n}` edges, DESIGN-GATE — load-reject if edges omit the new id |
| New resource | expand-by-construction for `resource.*` families | every resource-touching family must cover **all** ids; action costs; DESIGN-GATE resource row |
| New element | roster | complete matchup matrix or reject; overlay combat |
| New kit role | | item `core.v1.json` `registryVersion` + `ItemRole` append-only |
| New `UnitClass`, compose kind, Funnel consumer, tab kind | | C# + a renderer |

A catalog family with no writer is **`no-producer`** (already a sheet state), not a silent combat
effect.

---

## Which loop this extends

From [the-loops.md](../guide/the-loops.md):

| Loop | How the sheet serves it |
|---|---|
| **A. Level up and power** (spine) | Aptitude leftover + Confirm; derived families; Standing; specimen XP. This is the primary loop. |
| **C. Item collection** (spine) | Kit paper-doll (roles from catalog; weights from item program) and loadout. Empty until gear is known — honest empty, not a fake doll. |
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
| Twelve aptitudes, postures Force/Finesse/Bastion as a **read** | `AptitudeCatalog.Count = 3×4` ([Aptitude.cs:30-36](../../src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs)); `Reading` already authored on the row (`"Hit harder."`) — **today still C#-first; target is `aptitude-catalog.v1.json`** |
| Commander allocate GET/POST, refuse overspend (never clamp) | [AptitudeEndpoints.cs:24-57](../../src/FusionRpg.Server/AptitudeEndpoints.cs); 409 `aptitudes.overbudget` |
| Same draft+save on the sheet Progression tab **and** the Aptitudes layer | [ProgressionTab.tsx:65-69](../../web/fusion-rpg-web/src/ui/actor/ProgressionTab.tsx); [useAllocationDraft.ts:61-67](../../web/fusion-rpg-web/src/hooks/useAllocationDraft.ts) |
| `ActorHub.Resolve` + live `GET /api/actors/{id}/derived` with contributions | [ActorHub.cs:39-48](../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs); [AuraDerivedEndpoints.cs:36-95](../../src/FusionRpg.Server/AuraDerivedEndpoints.cs) |
| DerivedStatsTab already consumes that feed (raw `channelId`) | [DerivedStatsTab.tsx:22-60](../../web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx) |
| Six-tab `ActorPanel` shell already ships | [ActorPanel.tsx:16-26](../../web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx) — **hardcoded; target is `actor-sheet.v1.json` kinds** |
| UniqueActor identity + DemonProfile (two concrete elements, nickname) | [UniqueActorDtos.cs:15-29](../../src/FusionRpg.Contracts/UniqueActorDtos.cs); [DemonDtos.cs:6-21](../../src/FusionRpg.Contracts/DemonDtos.cs) |
| Six resource **ids** registered | `hp stamina hunger spirit qi poise` ([DerivedStatChannels.cs:521](../../src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs)) |
| 24 locked statuses | [status-ssot.md](status-ssot.md) §9 — **C#-first today; target is `status-catalog.v1.json`** |
| Shield stack: max 3, drain priority, `GetShields` / `Totals` | [ShieldRuntime.cs:220-243](../../src/FusionRpg.Core/Combat/Shield/ShieldRuntime.cs); [ShieldPolicy.cs:17](../../src/FusionRpg.Core/Combat/Shield/ShieldPolicy.cs) |
| Band B HUD already shows shield + status strip (not the sheet) | actor-hud program, implemented 2026-08-31 |
| `status.resist.omni` uncapped; `.dot/.cc/.contagion` cap = tunable 0.95 | [DerivedStatRegistry.cs:98-111](../../src/FusionRpg.Core/Stats/Derived/DerivedStatRegistry.cs); [derived-stats.v2.json](../../data/tuning/derived-stats.v2.json) `categoryResistCap` |
| Thirteen `UnitClass` ledger; six sheet render states | [spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3; [spec-derived-stat-sheet.md](../design/spec-derived-stat-sheet.md) §3 |
| Equipment GET exists | [UniqueActorEndpoints.cs:79-82](../../src/FusionRpg.Server/UniqueActorEndpoints.cs) |
| Aura enable/disable on Actions tab (commander auras) | [ActionsTab.tsx:38-42](../../web/fusion-rpg-web/src/ui/actor/ActionsTab.tsx) |
| Passives tab is a real tree surface, not a locked grid | [PassivesTab.tsx:45-52](../../web/fusion-rpg-web/src/ui/actor/PassivesTab.tsx) |
| Lexicon seed authored (unread at runtime) | [lexicon.v1.json](../../data/seed/derived-stats/lexicon.v1.json) — **migrate into tuning catalogs** |
| Plate 13 InspectSplit + leftover + radials (HTML only) | `docs/design/13-actor-sheet.html` |
| GG-62 / GG-63 / GG-64 + D11–D14 | [game-gui-principles.md](game-gui-principles.md) §16, §20.5 — **D11 path amended to tuning catalogs** |

`progression.bonus.*` is composed into `AppliedCombat` ([ActorHub.cs:89-112](../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs)).
`EntityStatWriter` not writing a Unity defense field is **not** a sheet blocker. Overlay combat
already reads the derived channel.

### Wiring gap

| Finding | The inert line |
|---|---|
| `channelSummary` / `xpToNext` / `shieldStack` / `elementTyping` / `displayName` / `equipSlots` always pending | [adapt.ts:176-185](../../web/fusion-rpg-web/src/contract/adapt.ts) — `pendingWithReason(...)` regardless of input |
| `xpToNext` math exists; DTO has no field | [RpgProgression.cs](../../src/FusionRpg.Core/Progression/RpgProgression.cs) `XpToNext`; UniqueActorDto stops at `xp` |
| `channelLabel` still `idWords` the dotted id | [adapt.ts:898-899](../../web/fusion-rpg-web/src/contract/adapt.ts) — catalog unread by `src/`, `web/`, `tests/` |
| Live derived list prints `c.channelId` | [DerivedStatsTab.tsx:55](../../web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx) |
| “Open full derived-stat sheet” is a disabled button | [DerivedStatsTab.tsx:65-71](../../web/fusion-rpg-web/src/ui/actor/DerivedStatsTab.tsx) |
| `/derived` merge is Commander + DemonType only — UniqueDemon baseline omitted | [AuraDerivedEndpoints.cs](../../src/FusionRpg.Server/AuraDerivedEndpoints.cs) |
| UniqueDemon allocate: store + `UniqueDemonAllocation.Baseline` exist; no player POST | [AptitudeEndpoints.cs:12-18](../../src/FusionRpg.Server/AptitudeEndpoints.cs) |
| Species GET exists; sheet does not use it | [AptitudeEndpoints.cs:67-72](../../src/FusionRpg.Server/AptitudeEndpoints.cs) |
| Shield layers live in Core; no player `GET /api/actors/{id}/shields` | debug/sim only |
| Six pools in Core; web `ResourceId` omits `poise` | [types.ts:664](../../web/fusion-rpg-web/src/contract/types.ts) five ids — **fixed by iterating resource-catalog, not by hardcoding six** |
| `ResourceView` typed, no component binds it | [types.ts:666-672](../../web/fusion-rpg-web/src/contract/types.ts) |
| GearTab empty-states while RelicsLayer already fetches equipment | [GearTab.tsx:22-26](../../web/fusion-rpg-web/src/ui/actor/GearTab.tsx) |
| Release/Deploy footer only closes the panel | [ActorPanel.tsx:97-102](../../web/fusion-rpg-web/src/ui/actor/ActorPanel.tsx) |
| Lawn HUD fold has meters unused by Inspector/Phaser | [foldActorHud.ts:66-78](../../web/fusion-rpg-web/src/features/lawn/foldActorHud.ts) |
| `unitClass` / `cap` / `composeSentence` have no server producer on `ActorChannelDetail` | DerivedStatsTab comment `:16-18` |
| Rosters still C#-first; seed lexicon unread | AptitudeCatalog / StatusCatalogBootstrap / ElementRoster / DerivedStatChannels |
| No `GET /api/catalogs/actor-surface` | — |

### Real gap

| Finding | What would have to be built |
|---|---|
| Host inject + CatalogHubs for the six catalog files | Same pattern as `AptitudeTuningHub` / `ActorHudTuningHub` |
| `GET /api/catalogs/actor-surface` fan-in DTO | Server projection; FE caches once per session |
| React `StatRow`, `InspectSplit`, leftover meter, shield radials, status glyphs as **shared kit** | New components. Kit CSS on the plate is not React. |
| `ActorView` has no `resources`, `statuses`, or `actions` | Type + adapter + a projection. Not a Unity rewrite. |
| Regular action slots | Action corpus + `ActionSlot` bind. Plate 13 Kit loadout is the **slot chrome**; [action-ideal.md](action-ideal.md) is sealed. Placeholder Strike/Firebolt stay forbidden (GG-23). |
| Aspect-scope aptitude | **Reverted, not authorized.** Do not start. |
| Promote | Owner: ignore. No placeholder. |
| Unified ActorSheet DTO (identity + pools + live statuses + shield layers + derived + catalog names) | One server projection or a documented fan-in of existing GETs + the surface catalog. |

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

**Eight tab kinds, InspectSplit, leftover footer, catalog names.** That is plate 13. Alternatives
rejected in this phase (not re-opened at `/spec` unless the owner reverses):

| Rejected | Why |
|---|---|
| Six-tab map (Overview / Progression / Derived doorway / locked Actions / locked Passives / Gear) | Already shipped as a stub bar. Condition/Shield/Status/Elements have nowhere to live. Derived *is* the sheet, not a door. Passives and auras are no longer locked. |
| Nested aptitude DialogShell | GG-10: cell → dock → sheet is two pushes. A paragraph is not a third. Confirm stays a decision control (GG-22 / GG-63). |
| Channel-id tables as first paint | GG-62 / GG-64. Completeness in the inventory below the mock, not the glance. |
| Third channel classification | Six render states + thirteen `UnitClass` already exist. 2026-08-24 incident. |
| Display copy in `data/tuning/derived-stats.v2.json` | That file is numbers only (`categoryResistCap`, `turnDefaultSpeed`). Copy lives in `*-catalog.v{n}.json`. |
| Hardcoded FE roster unions | Owner lock 2026-09-07. Iterate `GET /api/catalogs/actor-surface`. |
| `idWords` on a player band | GG-62. Developer tree only. |
| Sun **bank** meter on the sheet | Match-scoped `pvz.*`. Actor `hunger` (plant label **Sun** from resource-catalog) is the pool. |
| Separate `LawnActorSheet` vs `RosterActorSheet` programs | GG-9. One sheet, a role prop. |
| `#/actor/:id` | GG-1. `?panel=…&sel=<instanceId>` over the current stage. |

### Tabs (player words — labels and order from `actor-sheet.v1.json`)

| Kind (structural) | First paint | Inspector / footnote |
|---|---|---|
| **Condition** | HP radial with shield overlay · resource meters from catalog · five-axis Standing · live status glyphs | Glance. No dialog. |
| **Aptitudes** | Tiles from aptitude-catalog, `+`/`−`, leftover meter, Reset / Confirm | Right: aptitude `reading` + what the share feeds (family displayNames). Commander-scope copy until UniqueDemon allocate wires. |
| **Derived** | Category segs from derived-stat-catalog, one collapse open, StatRow, Show unchanged | Right: unit sentence, compose sentence, cap or “more still counts”, sources (GG-49). Six states from spec-derived-stat-sheet §3. |
| **Shield** | Three instance radials (empty well dashed) | Omni shield StatRows. Full combat×element matrix stays on Derived → Shield category. Noun is **Shield**, never Ward. |
| **Status** | Glyphs (live / catalog / mastery segs) from status-catalog | Catalog reading. Mastery is player-lifetime, not a live stack. |
| **Elements** | Coloured radials from element-catalog + mastery StatRows | Two concrete types from profile. Omni is a baseline, not a seventh type chip. |
| **Kit** | ActionSlot row + paper-doll roles listed in actor-sheet catalog | Auras already live above regular actions. Regular slots stay locked-with-reason until the action corpus ships. Gear binds equipment GET. |
| **Paths** | Species bloodline vs shared corpus — node track, not a paragraph | Tree surface may push once (`spec-tree-surface.md`). That is GG-10’s last push. |

Header is always on: portrait, name, species, side, level, two elements, Fielded/Wave, Esc.

### Shared widgets (one implementation)

`AptitudeTile` · `StatRow` · `InspectSplit` · `LeftoverBar` · `ShieldLayer` (radial) · `StatusGlyph`.
The inspector is **not** a stack push. Esc pops the **sheet**.

Meters and icons use the locked presentation libs — **`recharts`**, **`react-tiny-sparkline`**,
**`lucide-react`** (+ GG-58 fallback), **`motion`** when needed; Paths tree on **`@xyflow/react`**
([tech-stack.md](../design/tech-stack.md) §3.3; map tech table). Buy before build; fat chunk ⇒ split.

### Bindings that stay true

- Aptitude is a **source share**, never a derived channel id.
- Allocation is the **sum of scopes**; `share` is on the sum. Aspect scope is **reverted**.
- Combat families expand over the injected element axis the same way the generator does.
- `derived-stat-catalog` is a **family** list (~tens of entries). Live registry is **268** channels
  today (`CatalogResolves268`). The sheet joins expand(family) → `/api/actors/{id}/derived`
  `Channels` — it does not require one catalog row per channel, and must not truncate the matrix
  because “200+ is too many for the backend.”
- `omni` column is visually first and separated.
- Spark fill-to-100% only for pools, bounded ratios, and registry caps (D14 / GG-64).

---

## Tunables and catalogs

This program introduces **no new balance numbers**. It **does** introduce runtime catalogs (copy +
roster identity).

| What the sheet shows | Home | Notes |
|---|---|---|
| `categoryResistCap` (0.95) | `data/tuning/derived-stats.v2.json` | Number. UI paints `CAP` from the **registry cap**, not a CSS guess. |
| Aptitude PointBudget | class-system / `point-economy` in `aptitudes.v7.json` | Leftover = budget − spent. Empty leftover is legal. Overspend is 409, not a silent min(). |
| Shield max 3, drain priorities | `ShieldPolicy` / shield tuning | Not retuned here. |
| Aptitude / family / status / resource / element **names and readings** | `*-catalog.v{n}.json` | Runtime catalog SSOT. |
| Tab order and labels | `actor-sheet.v1.json` | Structural kinds stay in code. |
| Plant label Sun for `hunger` | `resource-catalog.v1.json` | Content, not a channel id. |

Do **not** dump display copy into `aptitudes.v7.json` or `derived-stats.v2.json`.

---

## What this deliberately does not decide

- Whether `AptitudesLayer` (rail) **retires** once the sheet leftover ships, or stays a shortcut.
  GG-9 prefers one home; a shortcut that opens the **same** tab is not a second surface.
- Action corpus content, costs, targeting — sealed in [action-ideal.md](action-ideal.md). Kit shows
  slots.
- Passive-tree lattice beyond the Paths glance — [passive-tree-ideal.md](passive-tree-ideal.md)
  (Paths kind wraps PassivesTab; graph on `@xyflow/react`).
- Aspect-scope (reverted).
- Promote.
- HUD sliver / numeric Band B readout — actor-hud already decided **off** for v1 (GG-60).
- Hot-reload of catalogs without process restart.
- Moving `ItemRole` off the C# enum / item `registryVersion` process.

**Locked 2026-09-07:** commander leftover v1 (honest scope chip); plate 13 visual SSOT; near-fullscreen
`min(1800px, 96vw)` × `min(960px, 92vh)`; buy-before-build presentation libs; leftover footer only on
Aptitudes / dirty draft.

---

## Open questions — owner only

Answerable. Not a template dump. **Catalog shape does not wait on these.**

1. **Commander HUD chip tap opens this same sheet in v1?** Recommendation: **yes, with a “this
   match” banner** — GG-9. Optional delay was listed in spec-lawn-interactive; it is not a second
   product.

Plate 13 is visual SSOT; leftover is commander v1 — already locked above. The first wiring patch
that does not need a new UI module is: **`channelLabel` reads catalog `displayName`**.
(and FE stops hardcoding resource/status unions).
