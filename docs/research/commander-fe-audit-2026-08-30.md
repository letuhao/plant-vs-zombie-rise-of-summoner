# Commander FE audit — command list, deploy defaults, genre comparison

**Status:** Research reference. **Audit only — no build authorized.** Verified against code and binding
docs 2026-08-30.

**Governed by:** [../architecture/game-gui-principles.md](../architecture/game-gui-principles.md) ·
[../design/information-architecture.md](../design/information-architecture.md) ·
[../architecture/aura-skill/spec-aura-surface.md](../architecture/aura-skill/spec-aura-surface.md) ·
[../architecture/aura-skill-ideal.md](../architecture/aura-skill-ideal.md) ·
[../architecture/commander-surface-ideal.md](../architecture/commander-surface-ideal.md) (layout + async default — post-audit)

**Why this file exists.** The aura-skill program shipped backend + partial FE (T18c) while commander
concerns stayed scattered across layers. This audit answers: what menus exist today, whether default
command-for-deploy is possible, how genre peers handle commander/hero UI, and whether a centralized
commander hub fits the layer-over-stage architecture.

**Correction (2026-08-30):** Detail for both commanders and creatures is the **same Actor sheet**
(`ActorPanel`, GG-9). The audit gaps reduce to **two**: (1) **commander list menu**, (2) **Actor sheet
extensions** per role (commander-only vs creature-only tabs, footer actions, data wiring).

---

## 0. One sheet, two lists, role extensions

```text
                    ┌─────────────────────────────────────┐
  Commander list    │  Actor sheet (ActorPanel)           │  ← same detail component
  (MISSING)    ──►  │  tabs/footer vary by actor role     │
                    └─────────────────────────────────────┘
  Creatures list    │
  (partial)    ──►  │
```

| Piece | Status | Notes |
|---|---|---|
| **Actor sheet** — detail for any actor | **Building** | `ActorPanel` + six tabs; demo route + actor-sheet program. Commander and creature both drill in here. |
| **Creatures list** | **Partial** | `CreaturesLayer` — `ActorRow` list → select → `ActorCard` (panel not wired in production). |
| **Commander list** | **Missing** | No layer, no API list, no pick Dave/Zomboss / active-for-run. |
| **Role extensions on Actor sheet** | **Partial** | Shared shell exists; commander-only (loadout, aura enable, commands) and creature-only (Deploy, Release, phase, lawn deploy) not fully wired per role. |

**Not the same as Actor sheet:** `ActorMenuScopePicker` — WHO a buff reaches (scope primitive); composes into authoring, not list or detail.

**List pattern (both roles):** `ActorRow` list → select → **`ActorPanel`**. Creatures layer proves the list half; commanders need the same list half with `CommanderId[]` instead of `useUniqueActors`.

---

## Executive summary

**Actor sheet = detail for commanders and creatures — same `ActorPanel`.** Only two FE gaps matter for
this audit:

| Gap | Status |
|---|---|
| **1. Commander list menu** | **Missing** — no list layer, no list API, no Set default persistence |
| **2. Actor sheet role extensions** | **Partial** — shared six-tab shell building; commander-only and creature-only behaviour not fully wired |

Everything else in this doc is evidence for those two gaps (backend truth, genre comparison, deploy
defaults). Aptitudes layer (`◎`) is a separate account-wide stat shortcut, not the commander list.

There is **no commander list**, **no persisted default field**, and **no pre-run setup** — Sanctum
goes straight to `#/lawn`. See [commander-surface-ideal.md](../architecture/commander-surface-ideal.md)
for the target async-default model.

---

## 1. What exists in FE today

### 1.1 Commander list menu — **missing** (this is the gap you asked about)

Backend knows exactly two commanders today:

```csharp
// CommanderId.cs — CommanderIds.All = [Dave, Zomboss]
ToStableId() → "commander:dave" | "commander:zomboss"
```

| Need | Backend | FE |
|---|---|---|
| List commanders for the player | `CommanderIds.All` (Core only — **no REST list endpoint**) | **Nothing** — no layer, no row list, no picker |
| Pick active commander for lawn run | No persisted "selected commander" field found | Sanctum skips straight to `/lawn` |
| Pick default command / loadout for deploy | `/api/loadout` is Dave-only; no default seed | Nothing |
| Open commander detail after pick | Commander is an actor (T9); detail = `ActorPanel` | No list → no drill-in path in production |

**Verdict:** The **commander list menu** does not exist. Genre peers show this as hero roster / CO
select / formation leader slot before battle — we have no equivalent.

**Closest shipped pattern:** [CreaturesLayer.tsx](web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx)
— virtualized `ActorRow` list, select one, detail card, deploy action. A commander list menu would
reuse the **same ladder** (`ActorRow` → `ActorPanel`) with a different data source (`CommanderId[]`,
not `useUniqueActors`).

### 1.2 Actor sheet — shared detail (commander + creature)

One **`ActorPanel`** (GG-9, [actor-sheet-map.md](../architecture/actor-sheet-map.md)) — not two detail
systems. List → select → same sheet; **role** drives which tabs, footer, and APIs bind.

| Concern | Commander role | Creature role |
|---|---|---|
| **Footer** | Set default · Defend the lawn (mirrors) — not Deploy/Release | `Release` / `Deploy to lawn` (`ActorPanel.tsx`) |
| **Progression tab** | Aptitude allocation per commander scope (`ProgressionTab` — commander wired) | Level/XP readout; other scopes deferred |
| **Actions tab** | Aura loadout + enable (`AuraSlot`, T18c); future command list | Locked until action system ships |
| **Gear tab** | Commander banner (`commanderOnly` item role — backend only) | Specimen equip (`equipSlots` pending) |
| **Overview** | Identity without plant/zombie phase semantics | Side, phase, standing placeholders |
| **Data wiring today** | `/api/loadout`, `/api/aura-runtime` keyed to player/Dave only | `useUniqueActors`, deploy API |

**Shipped partial:** `#/actor-ladder-demo` proves the sheet; Creatures layer has **list + card** but
does not open full panel in production. Commander actors are not in any list yet.

**Still to build (gap 2):** explicit role on `ActorView` (or equivalent) so one panel hides creature
Deploy on commanders, shows aura loadout assign on commanders only, etc. — extensions on the shared
sheet, not a forked panel.

### 1.3 Creatures list (proves list → sheet pattern)

- **Creatures layer** — `ActorRow` list → `ActorCard` + deploy; detail = same `ActorPanel` when wired.
- Commander list should copy this list half only.

### 1.4 Aptitudes layer (shortcut, not commander list)

- Rail `◎` / "Primary Stats" — account-wide commander-scope budget; parallel to Progression tab, not a
  commander roster.

### 1.5 Actor-menu scope picker (orthogonal)

- `ActorMenuScopePicker` — WHO a buff reaches; demo only.

### 1.6 Other "command" surfaces (out of scope)

- World-map legion orders (`WorldPage`) — different domain.
- Patron picker (Pacts layer) — separate cross-mode aura.
- Locked action placeholders — commander role on Actions tab when action system ships.

### 1.7 Lawn run + deploy

```text
Sanctum "Defend the lawn"  →  #/lawn
Creatures layer            →  /lawn?deploy={instanceId}  →  POST deploy
```

No commander list step; no default commander. Pre-run squad excluded (game-gui T21).

---

## 2. Default command for deploy — backend truth

| Question | Answer |
|---|---|
| Default selected commander for lawn run? | **No persisted field** — implicit Dave; target is `defaultLawnCommanderId` (ideal §4) |
| List commanders in FE? | **No** — `CommanderIds.All` exists in Core only |
| Default equipped aura loadout (Dave)? | **No** — `GET /api/loadout/{playerId}` returns null until POST |
| Default enabled aura on match start? | **No** — `AuraRuntime` starts empty; nothing auto-enables |
| Default aptitude allocation? | **Empty** until player allocates |
| Auto-equip on deploy? | **Demons only** (`AutoEquip`); not Dave/commander |
| Default on world map idle? | **Yes** — `stand-fast` (`src/FusionRpg.Core/World/Turn/WorldCommand.cs`) |
| Aura reaches live lawn today? | **Partial** — aptitudes wired (T5); aura enable is server-RAM only, **no injector consumer** (`spec-aura-delivery-path.md` R4 gap) |

So even if FE added a "default command" picker tomorrow, **enabling an aura would not affect the lawn**
until delivery-path work ships.

---

## 3. Genre comparison — what peers do vs what we have

Primary prior art is already surveyed in [aura-skill-ideal.md §3](../architecture/aura-skill-ideal.md)
(HoMM3 off-field commander is the north star).

| Pattern | Typical UI | Rise of Summoner |
|---|---|---|
| **Centralized pre-battle hub** | HoMM3 army screen, gacha Formation, TFT lobby | **No** — IA deliberately splits across layers; Sanctum is home, not commander HQ |
| **Commander identity** | CO/hero select before fight | **Persisted default** — no pre-run gate; **FE list menu: missing** |
| **Skill/aura loadout** | 3–10 saved presets, leader skill slot | Commander role on shared **Actor sheet**; need list to reach it |
| **Pre-run squad confirm** | Pick 1–N units + review buffs | **Designed (plate 07 §A), excluded T21** — no API |
| **In-battle command UI** | Hero ability bar, 1 spell/turn | **None on lawn** — observe + deploy intent only |
| **Default / "just play"** | Last army / team 1 | **Zero-config** — aptitudes + patron passive; creatures deployed ad hoc |

**Expeditions** is the closest shipped preset pattern: tier-gated squad slots with availability
reasons — the pattern T21 wanted for lawn runs but lacks backend.

### Peer lessons mapped to this architecture

| Peer lesson | RoS-aligned interpretation |
|---|---|
| HoMM3: review army + hero before fight | Optional prep in **Commanders layer**; future **T21** dialog may show creature berths + aura summary — **not** a mandatory commander gate |
| Gacha: saved team presets | Extend **`/api/loadout`** with named presets once action layer ships; surface in Aptitudes or a band-3 dialog, not a new stage |
| TD: hero bar during fight | Lawn HUD chip shows **this match's** leader + aura — **read-only** (commander-surface-ideal §3); optional tap opens sheet with "next run" labeling |
| Gacha leader slot | **Set default** in Commanders list; detail is Actor sheet — Dave implicit today |

### 3.1 Post-ideal corrections (2026-08-30)

After [commander-surface-ideal.md](../architecture/commander-surface-ideal.md) and IA sync, these audit
rows are **superseded** for commander (creature/T21 concerns unchanged):

| Prior audit wording | Corrected frame |
|---|---|
| "Commander identity picker" / "pick for run" | **Persisted `defaultLawnCommanderId`** — Set default anytime; never required before play |
| HoMM3 pre-battle hub via T21 for commander | T21 **creature** dialog (07 §A) when squad API exists — **not** a commander gate |
| Lawn HUD chip → opens commander list | **Read-only this match** by default; optional non-blocking sheet with next-run labeling |
| "active for this run" / "default deploy selection" | **Set default** (persisted) + **match snapshot at board.start** |

Gap 1 (Commanders list layer + list API) and Gap 2 (Actor sheet role extensions) **remain valid**;
wording shifts from picker to async default + optional prep.

---

---

## 4. What to build (audit conclusion — not authorized)

### Gap 1 — Commander list menu

Copy **Creatures list** shape: `ActorRow` × N → select `CommanderId` → open shared **`ActorPanel`**.
Needs list data (`GET /api/commanders` filtered to player empire) and **Set default** persistence
(`defaultLawnCommanderId`). Reusable from Sanctum, optional lawn HUD drill-in, world legion setup.

### Gap 2 — Actor sheet role extensions

Same **`ActorPanel`**; branch on actor role (commander vs creature):

- Commander: loadout assign UI, aura enable (T18c widgets), command list when action layer ships;
  commander-scoped `/api/loadout` / `/api/aura-runtime` (today Dave-only).
- Creature: Deploy / Release, phase, lawn deploy path — already specced on footer; wire in Creatures flow.

Do **not** fork a second detail panel. Do **not** treat creature detail and commander detail as different
programs — they are the same sheet with different extensions.

### Also blocked (not FE list/sheet work alone)

- Pre-run squad ceremony (game-gui T21) — backend gap for batch deploy.
- Lawn aura delivery — injector R4; enabling on sheet still won't move the board until then.

---

## 5. Answers to the audit questions

| Question | Answer |
|---|---|
| Commander list menu? | **No.** |
| Creature / commander detail? | **Same Actor sheet** — partial (demo + actor-sheet program). |
| What's missing? | **(1) Commander list** **(2) Role extensions** on the shared sheet. |
| Default lawn commander? | **No persisted field** — Dave implicit; needs `defaultLawnCommanderId` + Commanders layer |

---

## 6. Key file index (for follow-up)

| Area | Files |
|---|---|
| **Gap 1 — Commander list** | Pattern: `CreaturesLayer.tsx`; ids: `CommanderId.cs` |
| **Gap 2 — Actor sheet (both roles)** | `ActorPanel.tsx`, `ActionsTab.tsx`, `AuraSlot.tsx`, `ProgressionTab.tsx` |
| Creatures list (reference) | `CreaturesLayer.tsx` |
| Backend | No list endpoint; `LoadoutEndpoints.cs` (Dave only) |
| Programs | [actor-sheet-map.md](../architecture/actor-sheet-map.md), aura-skill T18c |

---

## DESIGN-GATE checklist

- Read authoritative docs for UI + aura-skill: **yes**
- Verified against code, not comments: **yes**
- Proposed changes: **none** (audit-only)
- Constraint tested (two gaps: list + role extensions on one Actor sheet): **yes**
