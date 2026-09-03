# Information architecture — the whole game GUI

**Status:** design complete, for review. Governed by
[architecture/game-gui-principles.md](../architecture/game-gui-principles.md) (GG-1 … GG-61);
decisions D1–D8 recorded in its §20.1.

This is the map: every stage, every layer, every key, and what happens to all twenty routes the
current build has. Plates draw these surfaces; this document says what they are and how they connect.

---

## 1. The shape

**Four stages, one at a time. Nine player layers, openable over any of them. One developer tree,
off by default.**

```text
                    ┌─────────── SHELL (band −1) ───────────┐
                    │   boot → title → save select          │
                    └───────────────┬───────────────────────┘
                                    │ enter session
                                    ▼
   ┌────────────────────────── STAGES (band 0) ──────────────────────────┐
   │                                                                     │
   │      SANCTUM ◄──── travel ────► WORLD MAP                           │
   │         │                            │                              │
   │         │ start a run                │ commit a legion              │
   │         ▼                            ▼                              │
   │       LAWN                        BATTLE                            │
   │         └──────── result ───────────┘                               │
   │                      │                                              │
   │                      ▼ always returns to                            │
   │                   SANCTUM                                           │
   └─────────────────────────────────────────────────────────────────────┘
                                    ▲
        LAYERS (band 2) open over whichever stage is current and close back to it:
        Creatures · Commanders · Relics · Fusion · Pacts · Expeditions · Almanac · Chronicle
                          System (band 5) · Developer (band 2, gated)
```

**The rule that produced this shape** is GG-4's test: *going somewhere to act* is a stage; *going
somewhere to look, compare or configure* is a layer. The world map is consulted **and** acted in, so
it is a stage — because the losing case (a place you act in, rendered as a panel that closes) is
worse than the reverse.

---

## 2. Stage catalog

### 2.1 Sanctum — the home stage `#/sanctum`

The summoner's hall. Where a session with no run in progress lives, and where every run returns.

| | |
|---|---|
| **Contains** | Bound creatures on display · the map table (travel) · the pact shelf · the fusion altar · expedition board · a "what next" focus card |
| **HUD** | Summoner level + XP · soul balance · unread results · the layer rail |
| **Time** | Nothing advances. Expeditions and tribute timers are wall-clock and shown as remaining, not ticking pressure |
| **Enter** | Session start; every run result; travel from world map |
| **Leave** | Travel to world map · start a lawn run · enter a battle |
| **Empty state** | The first-run script (GG-43) — one creature, one instruction, everything else absent rather than greyed |

The sanctum is **not** a menu of places to go (GG-3). Its content is the player's stuff, arranged.
Navigation is a consequence of that arrangement, not its purpose.

### 2.2 World map — `#/world`

The strategic stage. Sectors, lanes, legions, fog.

| | |
|---|---|
| **Contains** | Sector graph · legion markers · lane types · fog · frontier |
| **HUD** | Turn or tick readout · selected legion · map controls cluster (zoom / fit / layers / fog) |
| **Time** | Advances on the player's commit, not on wall-clock |
| **Enter** | Travel from sanctum (`M`, or the map table) |
| **Leave** | Travel back · commit a legion into a battle |
| **Selection** | Selecting a sector opens the **sector inspector** — a band-2 layer, because inspecting is looking |

### 2.3 Lawn — `#/lawn/{matchKey}`

The PvZ board projection. Observe-and-intent only; the RPG never owns this simulation.

| | |
|---|---|
| **Contains** | The Phaser canvas — the Dual-Plane Lawn Projector |
| **HUD** | Sun · wave clock · match phase · commander + active aura · deployed specimens · status tray · transport cluster |
| **Time** | Owned by the game. Under a band-2 panel it follows `overlay-spec.md` §Pause while away |
| **Canvas lifetime** | Created on **entering this stage**, destroyed on **leaving it**. Never on opening a panel (GG-11) |
| **Enter** | Start a run from the sanctum, or the injector reporting a live board |
| **Leave** | Board ends → result → sanctum |

### 2.4 Battle — `#/battle/{id}`

The turn-based stage, on the discrete-event kernel.

| | |
|---|---|
| **Contains** | The grid, the initiative track, the acting party, the target set |
| **HUD** | Initiative order · acting actor's resources · action bar · target readout |
| **Time** | **Virtual.** The kernel advances only when acted on, so "does it keep running under a panel" is not a question here — a distinction worth stating, because it is the one stage where GG-13 is trivially satisfied |
| **Enter** | Commit a legion on the world map; an expedition resolving into a fight |
| **Leave** | Resolution → result → sanctum |
| **Forbidden while here** | Stage travel with a committed turn (§6) |

### 2.5 Shell — band −1

Boot, title, save select, fatal error. The only surfaces that replace a stage without being travel.
Save select is where the current HudBar's player picker goes — a save is chosen once, at the door,
not from a dropdown wedged into the top bar of every screen.

---

## 3. Layer catalog — band 2

Every one opens over any stage and closes back to it. Each names the entity ladder rungs it uses, so
none of them invents a rendering.

| Layer | Key | Contains | Ladder rungs | Replaces route |
|---|---|---|---|---|
| **Creatures** | `C` | The bound roster; bind, rename, equip, deploy, release; specimen detail | Actor row / card / **panel** · Atom chip | `/roster` |
| **Commanders** | `K` | Player-empire commanders; set default lawn leader; open commander Actor sheet; map/legion stubs | Actor row / **panel** | *(new — no legacy route)* |
| **Relics** | `R` | Items and containers held; equip and compare; storage tabs | Container card · Atom row · **comparison** | `/storage` |
| **Fusion** | `F` | Two parents → result; recipe browse; preview of what is gained and lost | Actor card · **comparison** · recipe row | `/fusion`, part of `/recipes` |
| **Pacts** | `P` | Demon contracts, loyalty, tribute, terms, renegotiation | Contract card / panel · Actor chip | `/demons` |
| **Expeditions** | `E` | Dispatch, in-progress timers, returns and their rewards | Expedition row · Actor chip | `/expeditions` |
| **Almanac** | `A` | The reference: creature types, elements and the ring, statuses, recipes, effect families | Every token in plate §C · Actor card | `/types`, `/recipes` |
| **Chronicle** | `H` | Run history, progression dossier, XP ledger, the PvZ modifier sheet | Run card / row · charts | `/runs`, `/rpg-progression`, `/pvz-stats` |
| **Sector inspector** | — | Opened by selecting a sector on the world map | Sector card · Legion chip | part of `/world` |
| **System** | `Esc` on empty stack | Settings, keybinds, audio, display, developer toggle, quit | — | — |
| **Developer** | `` ` `` | Status, live log, metrics, activity, cheats, icon and almanac dumps, sim | Own rules (GG-41) | `/status`, `/log`, `/runs` raw, `/pvz-activity`, `/cheats`, `/icon-dump`, `/almanac-dump`, `/sim`, `/stats` |

**Twenty flat routes become four stages, nine player layers and one gated tree.**

**The rail is identical on every stage.** It lists Sanctum, then the six doing-layers, then the two
reading-layers — the same ten entries in the same order whether the player is in the sanctum, on the
map, on the lawn or in a battle. That uniformity *is* GG-7: a rail that changes per stage is a rail
that says some things are unreachable from here.

The first entry is the exception that proves it: **Sanctum is the travel-home affordance**, active on
the sanctum and a stage change everywhere else. It is the only rail entry that moves the player
rather than opening a layer, and it is therefore the only one that can confirm before acting.

---

## 4. Band assignment — every surface in the game

| Band | Surfaces |
|---|---|
| −1 Shell | Boot · title · save select · fatal error |
| 0 Stage | Sanctum · World map · Lawn · Battle |
| 1 HUD | Per-stage HUD clusters · the layer rail · connection status |
| 2 Panel | The ten layers in §3 (nine player + sector inspector) |
| 3 Dialog | Confirm (release, fuse, release-tribute) · **run result** · level-up · contract offer |
| 4 Toast | Mutation results · drops · tribute due · connection warnings · expedition returns |
| 5 System | Settings · quit · unrecoverable connection failure |

---

## 5. Verb table

Declared once. No surface reassigns a global verb (GG-20).

| Key | Verb | Notes |
|---|---|---|
| `Esc` | Pop one layer; **on an empty stack, open System** | The universal game convention, and it matches the overlay window's own Esc |
| `F10` | Toggle the overlay window | **Reserved** — owned by launcher/injector, never handled by the app |
| `C` `K` `R` `F` `P` `E` `A` `H` | Open Creatures / Commanders / Relics / Fusion / Pacts / Expeditions / Almanac / Chronicle | Pressing an open layer's key closes it |
| `M` | Travel to the world map | A stage change, so it confirms if something would be abandoned |
| `Space` | Stage transport pause/resume | Lawn and battle only; inert elsewhere |
| `Tab` | Cycle focus within the top layer | Never escapes it (GG-19) |
| `` ` `` | Developer tree | Only when developer mode is on |
| `1`–`9` | Stage-specific hotbar | Owned by the current stage |
| `?` | Keymap overlay | Reads this table; never diverges from it |

Nothing shadows a browser verb (`Ctrl+*`, `F5`, `F12`) or an overlay verb.

---

## 6. Reachability — GG-7 made checkable

Everything opens from everywhere, with exactly three exceptions. Each names its reason, because an
unexplained exception is indistinguishable from a bug.

| Blocked | Where | Reason |
|---|---|---|
| Stage travel | Battle, with a turn **committed** | Costs are consumed at commit and roll back only on failure; leaving mid-commit has no defined semantics |
| Fuse / release a creature | Any stage, when that creature is **deployed** | The specimen is live in a run; destroying it has no reversal |
| Renegotiate a pact | Any stage, when its tribute is **overdue** | The overdue state is the leverage; renegotiating out of it would make tribute optional |

Everything else — including opening Creatures mid-wave, comparing relics during a battle, and
reading the Almanac while a legion marches — is permitted. That is the point of GG-1.

---

## 7. Unlock ladder — GG-44

Complexity is revealed. Locked layers say what unlocks them (GG-17); they are not invisible, and
they are not present-but-dead.

| Unlocks | On |
|---|---|
| Sanctum · Creatures · Commanders · first run | Session start |
| Chronicle · Almanac | First run completed |
| Relics | First container acquired |
| World map (travel) | First sector contact |
| Fusion | Second creature of one species held |
| Pacts | First contract offered |
| Expeditions | First sector held |

The rail therefore renders from **state**, never from a constant list — the architectural
consequence GG-44 exists to force.

---

## 8. URL grammar — GG-8

The address encodes **stage + open layers**, never a replacement screen.

```text
#/sanctum                                  stage only
#/sanctum?panel=commanders                 layer over the stage
#/sanctum?panel=commanders&sel=commander:dave   layer with selection
#/sanctum?panel=creatures                  layer over the stage
#/sanctum?panel=creatures&sel=spec-42      layer with selection
#/sanctum?panel=relics&tab=equipped&cmp=r-9   layer, tab, comparison target
#/world?sector=ashfall                     stage with its inspector open
#/lawn/7f3a?panel=creatures                panel over a live board
#/battle/118                               stage only
#/dev/log                                  developer tree
```

Following any of these cold restores the **stage first**, then opens the layer over it. Esc returns
to the bare stage URL. Back and Esc do the same thing, always.

---

## 9. Stage transitions

| From → To | Trigger | Costs / confirms |
|---|---|---|
| Sanctum → World | `M`, or the map table | None |
| World → Sanctum | Travel back | None |
| Sanctum → Lawn | Start a run | **No commander gate** — uses persisted default + snapshot at board.start; creature berths (T21 / plate 07) are a separate async concern |
| World → Battle | Commit a legion | **Commits the legion** — confirm dialog names what is staked |
| Lawn / Battle → Sanctum | Resolution | Run result at band 3 (the one interruption class, D6) |
| Any → Shell | Quit | Confirms |

Every transition is a designed animation with a direction: outward (into a run) and inward (home).
Layer motion is a different vocabulary entirely, so the player can always tell which one just
happened (GG-31).

---

## 10. Motion vocabulary

GG-31 requires a consistent, short set of transitions whose **direction tells the player what just
happened**. Left undeclared, every surface invents its own and the stack stops being legible. This is
the whole set; there is no other motion in the game's chrome.

| # | Transition | When | Duration | Motion | Easing |
|---|---|---|---|---|---|
| M1 | **Layer in** | A band-2 panel opens | 180 ms | Up 12px + fade in; scrim fades to 0.72 | `ease-out` |
| M2 | **Layer out** | Esc / close | 120 ms | Down 8px + fade out; scrim fades first | `ease-in-out` |
| M3 | **Dialog in** | Band 3 opens | 140 ms | Scale 0.96 → 1 + fade; no translation | `ease-out` |
| M4 | **Toast in** | Band 4 appears | 120 ms | In from the right 16px + fade | `ease-out` |
| M5 | **Toast out** | Auto-expire | 200 ms | Fade + collapse height | `ease-in-out` |
| M6 | **Travel out** | Leaving a stage | 260 ms | Whole stage scales 1 → 1.04 and fades — going *into* something | `ease-in-out` |
| M7 | **Travel home** | Returning to the sanctum | 260 ms | Stage scales 0.97 → 1 and fades in — coming *back out* | `ease-out` |
| M8 | **Value change** | A number the player caused changes | 300 ms | Count up/down + one flash of `ok`/`bad` | `ease-out` |
| M9 | **Acknowledge** | Any input, immediately | 80 ms | Press depth 1px, or a ring pulse | `ease-out` |

**The three rules that make the set work**

1. **Direction encodes depth.** Layers move on Y, travel scales, dialogs do neither. A player learns
   in three uses which one just happened without being told.
2. **Out is faster than in.** Closing at 120 ms against opening at 180 ms is what makes an interface
   feel responsive rather than sluggish — nobody wants to watch a thing they have dismissed.
3. **M6/M7 are the latency budget.** 260 ms of travel animation is 260 ms a stage load can use for
   free. This is the honest alternative to prediction, which is banned (GG-15).

**Reduced motion** (GG-32) collapses M1–M8 to instant and keeps M9, because an acknowledgement that
does not appear reads as an input that was not received. Nothing in the set carries meaning that the
static end state does not also carry.

---

## 11. Route migration — what happens to the current twenty

| Current route | Becomes | Kind |
|---|---|---|
| `/status` | Developer → Status | dev |
| `/stats` | Developer → Tuning | dev |
| `/pvz-stats` | Chronicle → PvZ sheet | player |
| `/pvz-activity` | Developer → Activity | dev |
| `/rpg-progression` | Chronicle → Dossier | player |
| `/icon-dump` | Developer → Icons | dev |
| `/almanac-dump` | Developer → Text dump | dev |
| `/cheats` | Developer → Cheats | dev |
| `/types` | Almanac → Creatures | player |
| `/recipes` | Almanac → Recipes, and Fusion | player |
| `/log` | Developer → Log | dev |
| `/runs` | Chronicle → Runs | player |
| `/storage` | Relics | player |
| `/lawn` | **Lawn stage** | stage |
| `/world` | **World stage** | stage |
| `/roster` | Creatures | player |
| `/demons` | Pacts | player |
| `/expeditions` | Expeditions | player |
| `/fusion` | Fusion | player |
| `/sim` | Developer → Sim | dev |

Nothing is deleted. Nine surfaces move behind the developer gate, eight become layers, two become
stages, and one (`/recipes`) splits between a reference and a workshop.

---

## 12. Plate index

| Plate | Covers |
|---|---|
| [00-foundation.html](00-foundation.html) | Tokens · primitives · domain tokens · entity ladders · comparison · band shells · control clusters |
| [01-shell-home.html](01-shell-home.html) | Boot / title / save select · the Sanctum stage · HUD · rail · first-run script · unlock states |
| [02-collection.html](02-collection.html) | Creatures · Relics · Fusion — the itemisation surfaces, with comparison in situ |
| [03-world.html](03-world.html) | World map stage · sector inspector · legions · Expeditions · Pacts — **world sections superseded by plate 11**; Expeditions and Pacts stand |
| [04-run-stages.html](04-run-stages.html) | Lawn stage · Battle stage · their HUDs, transport and target surfaces |
| [05-chronicle-almanac.html](05-chronicle-almanac.html) | Chronicle · Almanac · the reference and history surfaces |
| [06-system-dev.html](06-system-dev.html) | Run result · level-up · confirms · toasts · Settings · rebinding · Display and Sound · the keymap · the developer tree |
| [07-flows.html](07-flows.html) | Loadout · deploy targeting · the pact offer · the four first-session beats · focus order and directional input · the last ladder rungs |
| [08-actor-sheet.html](08-actor-sheet.html) | Actor sheet · specimen and commander role extensions |
| [09-commander-list.html](09-commander-list.html) | Commanders layer · persisted default · list → Actor sheet |
| [10-actor-hud.html](10-actor-hud.html) | Per-unit lawn HUD — identity / resource / status rows · dual render |
| [11-world-stage.html](11-world-stage.html) | **The world map component catalog** — every map component in all its states, with the field that drives it. Supersedes plate 03 §A–B |
