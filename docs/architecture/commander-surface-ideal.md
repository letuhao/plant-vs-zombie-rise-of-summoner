# Commander surface — the ideal

**Status:** Ideal captured 2026-08-30; strengthened same day after layout debate. **Not a spec. No build
authorized.** Layout audit + plate patches preceded this document
([`09-commander-list.html`](../design/09-commander-list.html),
[`01-shell-home.html`](../design/01-shell-home.html),
[`04-run-stages.html`](../design/04-run-stages.html)).
Research evidence: [`commander-fe-audit-2026-08-30.md`](../research/commander-fe-audit-2026-08-30.md).
IA sync: [`information-architecture.md`](../design/information-architecture.md) §3 (Commanders layer `K`).
Aura mechanics stay in the aura-skill program — this ideal is **UI, persistence, and async handoff only**.

When this graduates: capability map `docs/architecture/commander-surface-map.md`, module specs under
`docs/architecture/commander-surface/`, tasks `tasks/commander-surface-plan.md` /
`tasks/commander-surface-todo.md`.

**Read before proposing against this:** [DESIGN-GATE.md](../DESIGN-GATE.md) §1 rows for player GUI,
actor sheet, and aura-skill. Verify against code, not plate comments alone.

---

## 0. Present — decided; do not reopen without owner

| Decision | Source |
|---|---|
| **No pre-run web gate for commander** — persisted default; change optional in Commanders layer | Owner 2026-08-30 |
| **Async handoff** — web saves default + loadout/aura; injector/match reads on start; PvZ menu and web FE are independent clients | Owner 2026-08-30 |
| **Match snapshot at `board.start`** — who led and which aura was active is frozen for this match; lawn HUD is historical identity | Debate 2026-08-30 |
| **Active aura ≠ default commander** — default names *who* leads; loadout/runtime names *which aura* is live for that commander | Plate 09 §D · debate 2026-08-30 |
| **Standard commander verbs** — **Set default** + **Defend the lawn** only; ban *picker*, *Set for lawn run*, *Deploy to lawn* on commander surfaces | Debate 2026-08-30 |
| **Commanders layer** — ninth player layer, hotkey `K`, rail order after Creatures | IA §3 · plate 09 §B |
| Player commander list = **player empire only**; Zomboss not in the menu | Owner 2026-08-30 |
| Detail = **same `ActorPanel`**; role extensions per commander vs creature | GG-9 · [actor-sheet-map.md](actor-sheet-map.md) |
| Default lawn commander = **Crazy Dave** until the player sets another persisted default | Plates + owner |
| **Lawn HUD chip** — read-only this match (GG-60); optional tap-to-sheet only with “next run” labeling | Debate 2026-08-30 |
| **v1 handoff** — poll server state at `board.start` only; push on save deferred | Debate 2026-08-30 |
| Location + legion stubs until world map + legion programs ship | Plate 09 §E |
| Aura magnitude, tick cost, and channel mapping = **aura-skill program only** | Scope boundary · [aura-skill-ideal.md](aura-skill-ideal.md) |
| Penny row on plates = **layout fixture** for N>1 UI; not shipped roster | Plate honesty |

**Incident this §0 prevents (pre-run gate, 2026-08-30):** treating the web control room as a gate the
PvZ game menu must pass through. The overlay cannot enforce a web step before play. Async default matches
aptitudes today — save state, read on match start.

**Incident this §0 prevents (plate drift, 2026-08-30):** plates and audit still said “lawn-run
commander picker” / “Set for lawn run” after the owner refused a pre-run gate. That wording implies a
per-run choice step. The correct frame is one persisted default and optional prep in the Commanders
layer.

---

## 1. What the commander surface is

Four touchpoints, one persistence story:

| Surface | Plate / IA | Job |
|---|---|---|
| **Commanders layer** | 09 · IA §3 `K` | Roster (player empire), set default, open actor sheet, map/legion stubs |
| **Actor sheet — commander role** | 08 §I | Loadout, aura enable, banner, aptitudes shortcut, footer mirrors |
| **Run entry** | 01 Sanctum · game menu | Uses **saved default** — not a picker dialog |
| **Lawn HUD chip** | 04 §A | Read-only identity for **this** match (leader + active aura) |

**Rail order** (identical on every stage): Sanctum → Creatures → **Commanders** → Relics → Fusion →
Pacts → Expeditions → Almanac → Chronicle. See plate 09 §B mock and
[`information-architecture.md`](../design/information-architecture.md) §3.

The commander is **not a lawn tile** (HoMM3 framing in aura-skill-ideal §1). Mid-run identity is a HUD
chip on the lawn stage, not a deploy slot.

**Not the commander surface:** `ActorMenuScopePicker` (WHO a buff reaches in authoring), creature deploy
URLs, creature loadout in plate 07 §A (separate async concern — creature berths are not a commander
gate; T21 when it ships addresses creatures only).

---

## 2. Async player flows

```text
Set once (or never)     Commanders layer → Set default → persisted defaultLawnCommanderId
Play anytime            Defend the lawn (web or game) → match reads default + loadout/aura at board.start
Change later            Commanders layer anytime → affects NEXT board.start only
Mid-run                 Lawn HUD shows who led THIS match; layer/sheet edits do not gate current wave
Future                  World map reverse links: list ↔ sector ↔ legion (stubs today)
```

**Set default** and row select write the same persisted field. **Defend the lawn** on the list footer,
Sanctum CTA, and sheet footer are **convenience mirrors** — same async travel handoff, none block play.

**N=1 today:** hide the commander seg control; show read-only Dave + optional `Change commander` link
(plate 01). When roster grows, seg + row badge `default` replace the read-only line; seg and persisted
field stay in sync.

**◎ Aptitudes** on the sheet is a shortcut to the account-wide aptitude view — not the commander list.

### 2.1 Match snapshot

At **`board.start`**, the injector (or match bootstrap) snapshots:

- `leadingCommanderId` — resolved from persisted `defaultLawnCommanderId` (fallback `commander:dave`)
- Active aura loadout + runtime for **that** commander
- Aptitude allocation for that commander's scope (`player:{id}` for Dave)

The lawn HUD chip and aura delivery for **this** match read the snapshot, not live server state.

Changes to default or loadout in the web FE apply at the **next `board.start`**, not retroactively mid-wave.
Mid-run sheet edits may save optimistically (same UX as loadout elsewhere), but lawn delivery keeps the
snapshot until the match ends — aligned with `LoadoutSet.Validate(isMidRun)` intent in
[`LoadoutEndpoints.cs`](../../src/FusionRpg.Server/LoadoutEndpoints.cs) once a mid-run oracle exists.

### 2.5 What already exists

Sorted into **built** / **wiring gap** / **real gap**. Same honesty standard as
[aura-skill-ideal.md](aura-skill-ideal.md) §2.

#### Built

| Thing | Evidence |
|---|---|
| **Two commanders in Core** | `CommanderIds.All` = Dave, Zomboss · [`CommanderId.cs`](../../src/FusionRpg.Core/Commanders/CommanderId.cs) |
| **Stable ids + allocation scope** | `commander:dave` · `player:{playerId}` for Dave aptitudes |
| **Dave loadout REST** | `/api/loadout` · [`LoadoutEndpoints.cs`](../../src/FusionRpg.Server/LoadoutEndpoints.cs) |
| **Loadout persistence + mid-run tests** | `RpgStore.Loadouts.cs` · `LoadoutStoreTests.MidRunRejectsAndPersistsNothing` |
| **Actor sheet shell** | `ActorPanel` + six tabs · actor-sheet program · demo route |
| **Creatures list pattern** | `CreaturesLayer` — `ActorRow` → select → panel (reference for Commanders layer) |
| **Aura FE widgets (partial)** | T18c — `AuraSlot`, Actions tab grouping |
| **Scope + patron aura precedent** | Side-wide grant at board lifecycle · aura-skill-ideal §2.1 |

#### Wiring gap

| # | Gap | Consequence |
|---|---|---|
| **C1** | No Commanders layer or `K` hotkey in production FE | No roster, no set-default, no drill-in |
| **C2** | No `defaultLawnCommanderId` persisted | Dave implicit; no multi-commander default |
| **C3** | No `GET /api/commanders` (player-empire list) | List must hardcode or skip |
| **C4** | Commander role extensions on `ActorPanel` incomplete | Footer/tabs still creature-shaped in places |
| **C5** | Aptitude allocation inert on lawn (W1) | Commander investment reads zero until aura-skill wiring lands |
| **C6** | `isMidRun` oracle wired to `() => false` on loadout endpoint | Mid-run refusal mechanism exists but is not enforced in production |
| **C7** | Sanctum → lawn skips commander readout in production | Plate 01/09 show intended UX only |

#### Real gap

| Gap | Notes |
|---|---|
| **Multi-commander player roster** | Only Dave in empire today; Penny is a plate fixture |
| **Penny loadout endpoint** | Zomboss uses authored patterns, not player loadout — by design |
| **Legion menu + map reverse links** | Stubs only until world-map / legion programs |
| **Lawn aura delivery (R4)** | Enabling aura on sheet still won't move the board until injector path ships |

---

## 3. Layout decisions

| Topic | Decision |
|---|---|
| Deploy triplication | Strip, footer, and sheet buttons write default or trigger travel — **not gates** |
| Row select | **Set default** (persisted), syncs seg when N>1 — not a per-run picker |
| Standard verbs | **Set default** · **Defend the lawn** — never *Set for lawn run* / *Deploy to lawn* on commanders |
| Pre-run dialog | **Refused** — no commander row in 07 §A; no web confirmation before PvZ menu |
| Sanctum → Lawn (IA §9) | **No loadout confirm for commander** — persisted default; creature berths remain T21 / 07 async |
| Sanctum display | Default summary + optional `Change commander`; `Defend the lawn` does not open a picker |
| Lawn HUD | Commander chip before deployed specimens; **read-only this match** (GG-60). Optional: tap opens sheet with “this match” banner and edits labeled “next run” — not a mid-run gate |
| Empire boundary | List shows Dave (and future player commanders); Zomboss lives in world map / AI data only |
| Detail panel | No second panel — commander drills into the same six-tab shell as creatures |
| Row badge (N>1) | **`default`** on the persisted commander row; not `lawn run` or `leading` |

Genre comparison (plate 09 §F): HoMM3's mandatory pre-battle hero review maps to **optional** prep in
the Commanders layer, not a mandatory web gate.

---

## 4. Data boundary

**Id spaces today (do not collapse):**

| Id | Example | Used for |
|---|---|---|
| `CommanderId` enum | `Dave`, `Zomboss` | Core roster, allocation scope, resource pools |
| Stable id | `commander:dave` | REST, persistence keys |
| Allocation scope | `player:{playerId}` (Dave) | Aptitude allocation — `CommanderIds.AllocationScopeKey` |
| World faction | `"dave"` | World commands, sector ownership — not the player list |

**Player list source:** player-empire commanders only. `CommanderIds.All` includes Zomboss for Core/world
AI; the **FE list filters** to empire members. Zomboss detail remains on the world map program.

**Active aura ≠ default commander:**

| Field | Meaning |
|---|---|
| `defaultLawnCommanderId` | Who leads the **next** lawn run at `board.start` |
| Per-commander loadout + aura runtime | Which aura is equipped/active for **that** commander |

Changing default does **not** migrate another commander's loadout. Switching default to Penny leads with
**Penny's saved** aura state, not Dave's.

**Persisted fields (recommended shape — exact column at spec):**

- `defaultLawnCommanderId` — player-scoped settings surface (same seam as aptitudes/loadout); initial
  `"commander:dave"` on first save
- Loadout + aura runtime — per-commander, today Dave-only via `/api/loadout`

**Read path (v1):** poll server state at **`board.start` only** — default + loadout/aura + allocation
scope. Push-on-save deferred until match-session FSM or owner asks for parity.

**Detail data:** commander actor uses T9 actor hub + commander role extensions; not `unique_actors`.

---

## 5. Refuses

| Refusal | Why |
|---|---|
| Mandatory commander pre-run dialog in web FE | Dual-client; owner 2026-08-30 |
| Enforcing web steps before PvZ game menu actions | Same |
| Per-run commander picker as a gate | Async default; not HoMM3 mandatory review |
| *Set for lawn run* / *Deploy to lawn* on commander surfaces | Implies per-run gate; use Set default + Defend the lawn |
| Sanctum → Lawn confirming commander loadout (IA) | Commander uses persisted default; no band-3 gate |
| Zomboss in player commander list | Empire boundary |
| Separate commander detail panel | GG-9 one sheet |
| Commanders authored as `unique_actors` rows | Wrong id space |
| Aura math or new derived channels in this program | aura-skill owns mechanics |
| Mid-run commander change affecting current wave delivery | Snapshot at board.start |

---

## 6. Open questions for spec

Decided items from the 2026-08-30 debate live in §0–§4. What remains for module specs:

1. **Exact persistence column** for `defaultLawnCommanderId` — player profile vs settings table vs
   loadout bundle extension (recommended: player-scoped settings seam; §4).
2. **Mid-run oracle** — what signal feeds `LoadoutSet.Validate(isMidRun)` for lawn matches (world
   campaign concept vs match session FSM).
3. **Legion menu program** — stub links on list row and sheet Overview until legion program exists.
4. **`GET /api/commanders` response shape** — player-empire filter, default flag, location/legion stub
   fields for FE list.

Creature berths (plate 07 §A) and T21 pre-run squad ceremony are **out of scope** for this program —
separate async concern; they must not become a commander gate.

---

## 7. Downstream

**Doc dependencies satisfied (2026-08-30 strengthen pass):**

- IA §3 — Commanders layer `K`, nine player layers, Sanctum → Lawn without commander loadout confirm
- Plates 08 §I/J, 09 intro/§B/§C — async verb alignment
- Audit §3.1 — post-ideal corrections

After owner review of this ideal:

1. **`commander-surface-map.md`** — capability map, module ids, build order (list layer, persistence,
   Sanctum readout, lawn HUD chip, sheet footer alignment, injector hydrate at board.start).
2. Module specs — one per map row; aura-skill program remains separate for T18c+ aura mechanics.

**Verification against plates (2026-08-30):**

- 09 §D prose matches async model (no 07 commander gate) ✓
- Sanctum → lawn path on 01 + 09 without blocking dialog ✓
- 04 §A commander HUD chip + next-run-only note ✓
- No patch to 07-flows for commander ✓
- IA + ideal agree on nine layers, `K`, no Sanctum loadout confirm for commander ✓

---

## DESIGN-GATE checklist

- Read authoritative docs for GUI + aura-skill + actor sheet + IA: **yes**
- Verified against code (`CommanderId.cs`, `LoadoutEndpoints.cs`, FE layers): **yes** (via audit + §2.5)
- Proposed changes: **ideal capture + doc sync** — no code
- Constraint tested (async default, no pre-run gate, match snapshot): **yes** — §0
