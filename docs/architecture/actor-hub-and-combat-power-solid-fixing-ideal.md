# Actor-hub + combat-power SOLID fixing — stub / placeholder ideal

**Status:** idea phase, owner clearance 2026-09-12 — **B1–B4 + O1–O2 locked**. Stub hygiene accepted. Not a build authorize.  
**Program id:** `actor-hub-and-combat-power-solid-fixing`  
**Sibling product ideal (combat power number / one-compose):** [combat-power-number-ideal.md](combat-power-number-ideal.md) — **Q1 accepted**  
**Map / specs:** [actor-hub-and-combat-power-solid-fixing-map.md](actor-hub-and-combat-power-solid-fixing-map.md)

---

## Which loop this extends

From `the-loops.md` / `the-game.md`: Rise of Summoner is **RPG + empire building**.

| Loop | Why stubs matter |
|---|---|
| **Spine A — Level up and power** | Stub equip and dual compose lie about how strong an actor is |
| **Place — Lawn / Battle / World** | Placeholder world combat (`Hp×Level`) and Bound Writer abs are wrong RPG paths beside ActorHub |
| **Place — Sanctum sheet** | Stub catalog and Standing gaps ship as “ready” when they are scaffolds |

**Load-bearing principles (restated inline):** Every RPG feature lives in the **RPG layer** (ActorHub, atoms, Funnel) — never by changing what PvZ is. Contests read **Θ**; magnitudes read **P(Θ)** — a placeholder `Hp×Level` combat strength is a **second ladder** defect. **SOLID is non-negotiable** — PO/ADR cannot bless a callable stub as intentional SSOT. Deltas, not Writer absolutes, for overlay HP. Balance numbers live in tuning JSON. Endless grind; absolute bounds throw.

---

## What this is

**Stub / placeholder hygiene law (locked):**

1. **No unfinished stubs on live paths.** Wave 5+ **deletes** callable stubs/placeholders that pretend to work.
2. **Cover with SOLID finished code** when this program (or a named follow-on) owns the real feature.
3. **If there is no solid idea yet:** stop. Owner starts a **new `/idea` enrich** for that feature. Do **not** leave scaffold that compiles and returns plausible numbers.
4. **Huge features that cannot finish here:** delete the stub, **track a new program** (name + backlog note). Never ship PlaceholderV2 or “keep the stub until later.”

**Done (Wave 5+ hygiene):** stubs gone from production paths; deferred work is a tracked program id, not leftover code that looks like combat/equip/intel.

---

## What already exists

### Built (works today — including grandfathered debt)

| Piece | Evidence | Note |
|---|---|---|
| ActorHub lawn/sheet compose | `ActorHub.Resolve` / Server `UniqueActorHubCompose` | Sole Hot end-state |
| BattleStatComposer production | `BattleEngine.cs:38` · header debt comment | **Grandfathered dual compose** — Wave 1 fuse retires |
| ProveAptitude dual-engine | `tools/ProveAptitude/Program.cs` | Flip to Hub-only after fuse |
| Server PassiveTreeTuningHub | `Program.cs` Configure | Server **Built**; Injector not |
| Atom path for some equip EffectIds | `UniqueEquipmentCatalog.cs:62-69` | Real atom map beside stub Items — stubs **deleted** in W5+ |
| PlaceholderBattle **tuning knobs** | `WorldTuning.cs` `PlaceholderBattleTuning` | Die with resolver deletion |
| Sheet “stub” state label | `DerivedSheetChannelMeta` StateStub | Honest UI for unhydrated Θ — **not** a combat stub |

### Wiring gap

| Gap | Evidence | Disposition |
|---|---|---|
| Injector PassiveTree never configured | `InjectorLoop` / `GateCounterHost` / `TreeBoundAtoms` | W3 `lawn-tree-hydrate` — finish |
| TurnEngine / DistrictAssault → PlaceholderBattleResolver | `TurnEngine.cs:139` · `DistrictAssaultResolver` early returns | **W5+ delete stub**; world combat → **new program** (not this map’s Hub-fed assault) |
| Intel `Strength(entity)` via placeholder | `IntelRecorder.cs:146` | **B4 drop** with stub delete; real weight → `world-actor-combat` |
| Battle / Delve Θ aliased to Level | `BattleStatComposer` · `ActorThetaSeam` | **W5+ fold** (`unique-theta-wire` / related) — wire real Θ or delete alias fiction |
| EquipAtomSource.None / StubPowerIndexProvider Θ=0 | defaults | Host inject real; no silent “zero is fine” as feature |

### Real gap

| Gap | Disposition |
|---|---|
| `UniqueEquipmentCatalog` stub Items | **W5+ delete** — do not ship equip the player cannot use |
| `PlaceholderBattleResolver` | **W5+ delete** — track **new world-combat program** |
| Bound Writer abs | W3 `bound-loadout-hub` — finish SOLID |
| ChannelMods / battle ignore-op / dual compose | W1 — finish SOLID |
| Sim Partial vs Full | W4 `sim-hub-parity` — finish or delete Partial fiction per that spec |

---

## Prior art

| Source | Lesson |
|---|---|
| Placeholder hygiene / live-path honesty | Scaffold that still runs and looks done is a defect |
| GauntletCI / no-stubs CI | Prefer delete + track over deferred crash or forever TODO |
| This repo’s power-scale incident | Private `f(level)` (Hp×Level) as “temporary combat” becomes a second ladder |

---

## The shape (locked)

### Policy

| Rule | Meaning |
|---|---|
| Delete > unfinished | Wave 5+ removes stubs; does not “polish” them |
| SOLID cover or new idea | If implementable under this program’s ideals → finish correctly; else → owner `/idea` for a **new program** |
| No new stubs | CI / review: no new `stub.*` / Placeholder* combat success paths |
| Fuse-first still stands | W1–4 before hygiene deletes that depend on Hub |

### Wave 5+ in *this* program

| Work | Action |
|---|---|
| Stub equip catalog | **Delete** Items / EffectIds that are non-usable; player path must not offer dead gear |
| `PlaceholderBattleResolver` + tuning surface that only serves it | **Delete**; call sites fail loud or feature-gate **off** (no silent Hp×Level win) |
| World assault / district combat engine | **Out of scope** — track **`world-actor-combat`** |
| Delve / battle Level-as-Θ alias | **Fold into W5+** — wire real Θ or remove the alias path that lies |
| Intel Strength (depends on placeholder) | **B4 drop** — presence-only until `world-actor-combat` |

### Tracked out

- Provisional program id **`world-actor-combat`**: world force/actor state · world/district combat · intel combat weight. **No specs under this program.** Start with `/idea` when ready (rename allowed then).
- World-map UI/runtime stays under existing **world-map-*** streams.

### Rejected

- PlaceholderV2 / tuned Hp×Level as Done
- Keeping stub equip “for demos”
- Leaving world combat stub “until the big program” while it still resolves fights

---

## Tunables

| Number | Owner |
|---|---|
| `PlaceholderBattleTuning` | **Deleted** with resolver |
| Strength bands | Idle until `world-actor-combat` invents honest weight (B4 dropped stub Strength) |
| Equip rolled magnitudes | Item / atom pipeline only after stubs gone |

---

## Owner clearances (2026-09-12)

| Id | Decision |
|---|---|
| **B1** | Delete stubs in Wave 5+; must implement SOLID to cover. If no idea yet → stop and run a **new idea enrich**. |
| **B2** | World assaults need a real combat engine — **huge program**. Defer + track new program. **Delete** stub code; do not keep unfinished combat. |
| **B3** | **Delete** stub equipment; do not ship features players cannot use. |
| **B4** | **Drop** intel force Strength / bands that depend on placeholder weight. Presence-only (or feature off) until a **world actor / combat state** program exists. Not a hotfix. |
| **O1** | No new stubs. Big unfinished feature → delete stub, track new program. |
| **O2** | **Fold** Delve/Level-as-Θ into Wave 5+. |
| **O3** | Clarified: two ideal docs stay linked (product combat-power vs this stub hygiene). Not a merge. |

### Layers note (why B4 drop is not a hotfix)

| Layer | Owns | Does not own |
|---|---|---|
| **ActorHub** | Cold → Hot compose: primary + derived / AppliedCombat for an actor identity | Mid-fight wound ticks, world map position, fog memory |
| **BattleEngine** (post-fuse: Hub seed) | In-battle resolution using Hub-seeded derived + fight-local mutations | Persistent world force identity across turns |
| **World** today | Map entities: sector/lane, stance, `Members` with crude `Hp/Wounds/Level` | Honest RPG combat weight or Hub-fed force power |
| **Missing program** | World-side actor/force state that can feed combat + intel from the same truth as Hub (and mid-campaign persistence) | — |

Placeholder `Hp×Level` was a fake bridge across that missing layer. Deleting it is correct; inventing Strength from Hub without world force state would still be a lie for most world entities (species-only members, no UniqueDemon instance).

### Still open

None for stub hygiene. Deferred: `/idea` for **`world-actor-combat`**.

---

## Handoff

1. Stub hygiene **B1–B4 locked**. Tracked id **`world-actor-combat`** (rename at `/idea`).
2. Map scope-cleared; Wave 5 = delete + track only.
3. Amend **in-scope** specs as gaps appear; do not add world-combat modules here.
4. `/plan` when owner asks — fuse-first.

**No build authorized from this document alone.**
