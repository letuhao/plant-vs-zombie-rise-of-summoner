# Design drafts — how we get from domain to screen

**What this folder is.** Static HTML design plates. No framework, no build, no imports from `web/`.
Open a `.html` file in a browser and it renders. They are **specification artifacts**: the reference
the React implementation is checked against, and the place a design argument is settled *before*
anyone writes a component.

**What this folder is not.** Not a component library, not shipped code, not a place to prototype
behaviour. Plates are static. Interaction lives in the app.

**Binding rules.** Every plate obeys [architecture/game-gui-principles.md](../architecture/game-gui-principles.md)
(GG-1 … GG-61). A plate that violates a GG rule is wrong even if it looks good.

---

## 1. How to view

```powershell
start docs\design\00-foundation.html
```

No server needed. Plates link `_kit/tokens.css` and `_kit/kit.css` relatively.

---

## 2. The methodology — Entity–Representation Matrix (ERM)

The question was: *how do you extract the shared components instead of discovering them one screen at
a time?* This is the answer, and it is the reason the foundation can be built before the screens.

### 2.1 The premise

**Screens are compositions. Components are representations of domain entities.**

The usual failure — the one this repo already lived through — is to design screen by screen and
extract "shared" components from whatever two screens happened to overlap. That yields a kit shaped
like the *order the screens were built in*, which is why the current FE has a `DataTable` and a
`KpiStat` but no way to render an atom, a status, an element, or an actor. Those are the things the
game is actually made of.

Invert it. The repo already has a **closed, versioned domain vocabulary** — five attach points,
twelve kinds, seven triggers, twenty-one statuses, seven elements, a channel registry, a resource
registry, a rarity table. That vocabulary is the component inventory. Enumerate it first, give each
entity a fixed ladder of representations, and screens become assemblies with nothing left to invent.

> The atom catalog already says this in its own words: *"Twelve kinds and five attach points is the
> whole machine. Everything a player will ever see is families and tiers on top of it."*

### 2.2 The six steps

**Step 1 — Enumerate entities from the SSOTs, never from screens.**
Walk the architecture docs and list every noun the player can encounter. Cite where each came from.
An entity that no SSOT defines is not an entity yet — it is a screen idea, and it waits.

**Step 2 — Give every entity the same representation ladder.**
Six densities, always in this order. Declare which rungs an entity needs; most need four.

| Rung | Size | Job | Typical home |
|---|---|---|---|
| **Token** | ~16–24px | Identity only — icon, colour, one glyph | Inline in text, meters, grids |
| **Chip** | one line | Identity + one value | Lists, trays, filter bars, tooltips |
| **Row** | one line, full width | Identity + 3–5 columns, selectable | Tables, ledgers, search results |
| **Card** | block | The whole entity at a glance | Grids, collections, pickers |
| **Panel** | band-2 layer | Everything, with tabs and actions | Opened over a stage (GG-1) |
| **Editor** | band-2/3 | Mutating form | Rare — most entities are read-only to the player |

The ladder is fixed on purpose. When every entity has the same rungs, a screen author picks a
*density*, not a *design*, and two screens showing the same entity cannot diverge (GG-9).

**Step 3 — Extract the primitives the ladder demands.**
Only now do you list buttons, meters, fields, frames, tabs. They are derived from what the rungs
need, so the kit cannot grow a component nobody consumes.

**Step 4 — Extract the layer shells from the band model.**
GG-5 defines six bands. Each band gets exactly one shell — one panel shell, one dialog shell, one
toast, one system sheet. Feature code never builds its own (a per-feature modal is a rejected
pattern).

**Step 5 — Extract control clusters.**
Repeating groups of controls that travel together: stage transport, map controls, filter bar,
selection tray, action bar. These are what the request called *"lawn control menu, world map menu"* —
they are clusters, not screens, and they belong in the foundation.

**Step 6 — Compose screens. Invent nothing.**
A screen is bands + shells + ladders + clusters. **If a screen needs something the foundation lacks,
that is a defect in step 1 or 2 — go back and fix the matrix, do not add a one-off to the screen.**
This rule is the whole discipline. Without it the method degrades into the screen-first approach it
replaced.

### 2.3 The rules that keep it honest

| Rule | Why |
|---|---|
| **Bind to the registry shape, not to the enumerated values.** A resource meter takes `(id, label, value, max, polarity)` — it does not know the resource list | The lists are still moving. Components bound to a *shape* survive the list changing; components with a `switch` over resource names do not |
| **No component without a declared consumer.** Either it is a rung of an entity in the matrix, or a shell of a band, or it does not exist | Kits rot by accretion |
| **One entity, one ladder, no forks.** No "roster card" and "shop card" for the same actor — one Actor card with props | GG-9 |
| **Every rung ships its four states.** Loading, empty, error, locked — designed, not defaulted | GG-17 |
| **Domain vocabulary in code, player vocabulary on screen.** The component may be named `AtomChip`; it must never render the word "atom" to a player | GG-23 |
| **The plate shows every state, including ugly ones.** Longest name, biggest number, negative value, missing icon, rejected row | States you did not draw are states the implementation will guess at |

### 2.4 Why draft HTML rather than going straight to React

- **Speed of disagreement.** A plate is edited and re-judged in seconds. A component is not.
- **No behaviour to hide behind.** A static plate cannot mask a weak layout with interactivity.
- **Every state visible simultaneously.** In the app, four states of one component require four
  scenarios. On a plate they sit side by side and can be compared.
- **It becomes the acceptance reference.** "Does the implementation match plate §D.2" is a checkable
  question; "does it look right" is not.
- **The CSS is not thrown away.** `_kit/tokens.css` is the source the app's `theme/tokens.css`
  is regenerated from, so the palette, scale and band values transfer as data, not as re-typing.

---

## 3. Folder layout

```text
docs/design/
  README.md                    this file — methodology, index, status
  information-architecture.md  the whole GUI: stages, layers, keymap, reachability, motion, route migration
  tech-stack.md                stack decisions, i18n, the measured bundle plan, and the gap register
  _kit/tokens.css              the token layer: colour, type, space, radius, elevation, motion, bands
  _kit/kit.css                 foundation component styles
  _kit/screens.css             stage layouts and screen-level structures
  00-foundation.html … 10-actor-hud.html · 11-world-stage.html   the twelve plates
```

Plates are numbered by the order they are *designed*, not by navigation order.

---

## 4. Plate index

**The design is complete for player stages and layers; plate 10 adds the per-unit lawn HUD ideal.**
Every player-facing surface in the game is drawn across these eleven plates, and
[information-architecture.md](information-architecture.md) is the map that connects them.

| Plate | Covers | Status |
|---|---|---|
| [00-foundation.html](00-foundation.html) | Tokens · primitives · domain tokens · entity ladders (atom, container, actor, status, sector, contract) · comparison · band shells · control clusters | **Draft — for review** |
| [01-shell-home.html](01-shell-home.html) | Title · save select · **the Sanctum** · HUD · the rail and its states · the authored first run | **Draft — for review** |
| [02-collection.html](02-collection.html) | Creatures · Relics with in-situ comparison · Fusion with its loss column · volume at 10/100/1000 | **Draft — for review** |
| [03-world.html](03-world.html) | The world map stage · sector inspector · commit-a-legion · Expeditions · Pacts | **Draft — for review** |
| [04-run-stages.html](04-run-stages.html) | Lawn stage · a panel over a live board · Battle stage · action states · truth under latency | **Draft — for review** |
| [05-chronicle-almanac.html](05-chronicle-almanac.html) | The Almanac as a book · element and affliction reference · Chronicle · the attribution ledger | **Draft — for review** |
| [06-system-dev.html](06-system-dev.html) | Run result · level-up · confirms · toasts · Settings · rebinding · Display and Sound · the keymap · the developer tree | **Draft — for review** |
| [07-flows.html](07-flows.html) | Loadout · deploy targeting · the pact offer · the four first-session beats · focus order · the last ladder rungs | **Draft — for review** |
| [08-actor-sheet.html](08-actor-sheet.html) | One Actor panel · six tabs · specimen and commander role extensions | **Draft — for review** |
| [09-commander-list.html](09-commander-list.html) | Player-empire commander list · persisted default (Dave) · Set default / Defend the lawn · location &amp; legion map stubs · list → Actor sheet | **Draft — for review** |
| [10-actor-hud.html](10-actor-hud.html) | Per-unit lawn HUD — identity / resource / status rows · dual render (Unity + Phaser) · legend · overflow · §H player scenarios (strengthened 2026-08-30) · ideal: [actor-hud-ideal.md](../architecture/actor-hud-ideal.md) · audit: [actor-hud-audit-2026-08-30.md](../research/actor-hud-audit-2026-08-30.md) | **Draft — for review** |
| [11-world-stage.html](11-world-stage.html) | **The world map component catalog** — sector node in every state · lanes · the four fog states · legions and supply · orders and targeting on the map · lenses · the anchored HUD · turn cluster and notifications · outliner · the bounded sector inspector · confirms · turn playback translating all 21 engine event prefixes · unit families and the modifier ledger. **Supersedes plate 03's world sections**, which predate the loam economy. Ideal: [world-stage-ideal.md](../architecture/world-stage-ideal.md) | **Draft — for review** |

### Coverage

| | Count | Where |
|---|---|---|
| Stages | 4 | Sanctum, World, Lawn, Battle — plates 01, 03, 04 |
| Player layers | 9 | Creatures, **Commanders**, Relics, Fusion, Pacts, Expeditions, Almanac, Chronicle, Sector inspector — plates 02, 03, 05, **09** |
| Band-3 dialogs | 6 | Run result, level-up, destructive confirm, commit, loadout, pact offer — plates 03, 06, 07 |
| Shell surfaces | 3 | Title, save select, unrecoverable — plates 01, 06 |
| Developer surfaces | 13 | One tree — plate 06 |
| Flows | 4 | Run start, deploy targeting, the offer, the first session — plate 07 |
| Entity ladder | 11 entities × 6 rungs | Every cell closed or refused with a reason — plate 00 §G |

---

## 5. What is deliberately not drawn

"Complete" is only meaningful if the exclusions are stated. These are refusals with reasons, not
omissions — if you disagree with one, that is a design argument, which is the point.

| Not drawn | Why |
|---|---|
| **Boot / splash** | A progress bar behind a logo. It has no decisions in it and no states worth arguing about |
| **Tab contents that are a list of drawn rungs** — Almanac → Fusions, Chronicle → Runs and Growth, Relics → Storage | Step 6 of the method says a screen is bands + shells + ladders + clusters. A tab that is a filtered list of recipe rows or run cards invents nothing, so drawing it would only prove the ladder works twice |
| **Illustration and final art** | The layouts are art-ready: the Sanctum composes around a backdrop, frames accept portraits, the map accepts terrain. Whether that art exists is a cost decision (§20.2 of the principles), not a structural one |
| **Animation as motion** | The vocabulary is specified as a contract in [information-architecture.md §10](information-architecture.md) — nine transitions with durations, directions and easings. Plates are static by design; animating them would make the spec harder to diff, not easier |
| **Copy at final polish** | Every string on a plate is written in the player's voice and is a real candidate, but wording is cheap to change and should be argued against a built screen |
| **Developer surface interiors** | One representative tree is drawn. The other twelve are tables of engine data governed by GG-41, where density beats design |

Everything else — every stage, every layer, every dialog, every band, every entity rung **in §6's
inventory** — is drawn.

> **Correction, 2026-08-22.** That inventory is short by 29 entities: step 1 never swept
> [`architecture/item/`](../architecture/item/) (31 documents) or
> [`architecture/action/`](../architecture/action/) (11 specs), and
> the item program's own presentation contract
> ([ssot-presentation.md](../architecture/item/ssot-presentation.md)) is the FE's contract for every
> number a player reads. The full register is [gap-audit-2026-08-22.md](gap-audit-2026-08-22.md). This
> §5 list is therefore *refusals I made deliberately*, not the whole of what is undrawn.

---

## 6. Entity inventory (step 1 output)

Extracted from the SSOTs, with the source that defines each. This is the matrix the foundation plate
implements; anything missing from here is missing from the kit by definition.

> **⚠ This table is incomplete and the sentence above is why it matters.** The extraction swept eight
> documents and missed [`architecture/item/`](../architecture/item/),
> [`architecture/action/`](../architecture/action/), the shield spec, and 29 specs under
> `battle/ combat/ standalone/ world/ demons/`. **29 entities are absent**, including the entire item
> presentation contract. Because "missing from here is missing from the kit by definition" is true, the
> omission propagated through the kit, the plates, the 14 modules and the sealed contract with nothing
> able to catch it. Register: [gap-audit-2026-08-22.md](gap-audit-2026-08-22.md). **Re-running step 1
> as a complete sweep is the fix**; until it lands, treat this table as a partial list, not an
> inventory.

| Entity | Defined by | Ladder rungs needed |
|---|---|---|
| **Atom effect** | [effect-atom/atom-catalog-ssot.md](../architecture/effect-atom/atom-catalog-ssot.md) §2, [definitions.md](../architecture/effect-atom/definitions.md) §1–§2 | Token · Chip · Row · Card |
| **Container** (item / trait / skill / species-passive / patron / world-buff) | definitions.md §1 grammar, §4 rarity | Chip · Row · Card · Panel |
| **Actor / specimen** | [unique-actor-runtime.md](../architecture/unique-actor-runtime.md), [actor-hub-ssot.md](../architecture/actor-hub-ssot.md) | Token · Chip · Row · Card · Panel |
| **Status** | atom-catalog-ssot.md §5 (21 declared, 13 functional) | Token · Chip · Row |
| **Element** | [element-hub-ssot.md](../architecture/element-hub-ssot.md) — 6 concrete + `omni` | Token · Chip |
| **Channel** | atom-catalog-ssot.md §4 — 8 primary (→11), 99 derived | Token · Chip · Row |
| **Resource** | [resource-hub-ssot.md](../architecture/resource-hub-ssot.md) — six locked ids (incl. `poise`), registry shape (§5) | Token · Meter · Row |
| **Power vector** | definitions.md §7 — 5 categories + scalar | Token · Chip · Card |
| **Sector / lane / legion** | [world-map-program.md](../architecture/world-map-program.md) | Token · Chip · Card · Panel |
| **Demon + contract** | [demon-system-map.md](../architecture/demon-system-map.md) | Chip · Row · Card · Panel |
| **Run / wave** | [match-runtime.md](../architecture/match-runtime.md) | Row · Card |

### ~~Known conflict~~ — resolved 2026-08-22

This section recorded a conflict between `decisions.md` and `resource-hub-ideal.md` over the resource
list. **There was no conflict.** The ideal doc's own §10.2a already carried the locked model matching
`decisions.md`; the stale material was *inside* that document, and it is now superseded outright by
[resource-hub-ssot.md](../architecture/resource-hub-ssot.md), which states at `:9` that the ideal's §2,
its header bullet and its §10.2 are **not authoritative**.

**The locked model:** six ids — `hp` · `stamina` · `hunger` · `spirit` · `qi` · `poise` — **one shared set, both
factions, no branch anywhere.** The only faction difference is a display label (§3): `hunger` reads
"Sun" on a plant and "Hunger" on a zombie; `qi` reads "Yang" and "Yin". Labels are content, never a key.

Two consequences the foundation plate has **not** yet absorbed (Class-B defect B8 in
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md)):

1. **All six resources are `asset`** (SSOT §6) — full is good, including `hunger`, which is an ordinary
   fed/starving gauge. The plate's `burden` visual branch ("burdens fill red and full is bad") has **no
   member** in the locked set. The field is retained for a future resource, not for a current one.
2. **Two different things are called "Sun"** (SSOT §4) — the match-scoped `pvz.*` lawn bank and the
   actor-scoped `rpg.*` `hunger` pool. A surface showing both must distinguish them **by scope**: the
   bank belongs to the stage HUD, the pool to the actor's meters. The plate distinguishes neither.

The registry binding is unchanged and still correct: `(id, label, value, max, polarity)`, with `label`
resolved from the actor's faction at the display layer.
