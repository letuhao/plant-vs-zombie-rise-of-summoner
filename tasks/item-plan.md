# Implementation Plan: item

Specs: [item-map.md](../docs/architecture/item-map.md) → 22 modules under
[docs/architecture/item/](../docs/architecture/item/). Rulings register:
[item-ideal.md](../docs/architecture/item-ideal.md) (**D1–D41**). Tasks: [item-todo.md](item-todo.md).

Named pair per repo convention — the bare `tasks/plan.md` / `tasks/todo.md` are the perf stream's.

## Overview

The item program has a complete design layer (17 lane SSOTs, 22 modules, 8,077 lines), a 133-file seed
corpus, **and zero runtime**. This plan sequences the 22 modules into six phases behind one rule the
map states explicitly:

> **Owner, 2026-09-03:** *"build order in plan phase. resolve dependencies first."*

So **Phase 0 is dependency resolution, not module 1** — and Phase 0 is mostly *other programs' work
plus one regeneration pass of our own*.

## What changed at plan time — D30–D41 (2026-09-04)

**Twelve rulings across two rounds cleared every blocking decision and every open question.** Three of
them **rejected the question rather than the options** — D30 and D34 (a proportion taken from a stale
registry) and D36 (a boundary I should not have been reasoning across at all):

| id | Ruling | Plan consequence |
|---|---|---|
| **D30** | D3 wins over the shipped sources: twelve-role hybrid core at 800‰. Re-author the 18 legacy sets | `core.v1.json` → **v2** in Phase 0 |
| **D31** | Scope `ssot-rarity` §3.8 to *drop* pity — **before** D7 | An **ordering constraint inside module 7**, not a co-delivery |
| **D32** | A content patch retunes owned items, deliberately | Module 1 ships as specced; no `ValuesJson` freeze |
| **D33** | (a) charms bind at `unique-actor:` (b) the missing atom apply scope is filed as a defect | Module 12's charm consumer is **un-gated**; (b) is `buff-debuff-scope`'s and blocks nothing here |
| **D34** | `basis = "name"` is missing data — the pipeline generates it | Two new seedsmith deps in Phase 0: `theme-refresh`, `theme-enrich` |
| **D35** | Unfreeze and re-derive `classes.v1.json` → **v4** | The **same pass** as D30's v2 |
| **D36** | ⛔ `action-corpus` is out of item scope and under another owner's active construction | X3 needs **no decision and no request**. Module 19 ships GA2 standalone and waits. ⛔ We do not read their map to infer their schedule |
| **D37** | The consumable carry limit is a **belt** (`girdle`), not a number | Capacity becomes a **content axis** on a shipped role, not a tunable. §10.1's `N = 2` withdrawn |
| **D38** | Drop rate is a **flat 5 % per kill**, tunable, and it is **two independent rolls** | Removes the slope from the kill path. ⚠ D18's `Θ`-linear volume survives on non-kill sources only |
| **D39** | Add **`Override`** to `stat.modify`'s ops | Overrides *do not add the kind before the consumer* — so the damage applier is **part of the ask** |
| **D40** | Charm carry splits to **module 22** | The program is **22 modules** |
| **D41** | Socket recipes are **unordered** — a multiset match | 102 combinations stay 102; `bind_ordinal` is display-only |

⛔ **Standing rule this round produced.** I derived *"hybrids are 6% of species"* from
`themes.v1.json`'s **84** entries to argue against D3. `data/seed/demons/species/` holds **386**. The
registry is a stale snapshot of a *generated* corpus. **Never derive a design proportion from a
snapshot of a generated corpus** — count it, or don't quote it.

## Architecture decisions (locked by the specs — not re-litigated here)

- **Ownership and binding are two independent reachability roots.** `rpg_item` is the second; the
  orphan sweep tests both. Unequip must never delete gear (D5/R1).
- **Every RPG feature lives in the RPG layer.** Item effects resolve through atoms →
  `DamagePacket`/`ChannelMods`, never by changing what PvZ is. `Sim` stays `None` for `stat.derived`
  until it has a real consumer.
- **One power ladder.** Contests read `Θ`; magnitudes read `P(Θ)`. Drop *volume* reads `Θ` linearly
  (D18); *quality* keeps reading `P(Θ)` through rarity/tier. No private loot curve.
- **`long` for every magnitude**, widen before multiplying, divide by 1000 last, overflow throws.
- **No hard progression ceilings.** `enhance_cap` is a shrinking **soft** cap —
  `gain(n) = enhance_cap(rung) × n/(n+K)` — never a stop (§2g #0c).
- **The balance surface is config.** Every number a balance pass would touch lives in
  `data/tuning/<domain>.v{n}.json`.
- **Item balance is validated by the class-system's existing guards** (termination HARD, dominance
  SOFT), extended to geared corners — not by an item-specific ratio (D29).
- **Item scope is generate → drop → apply** (D26). Pacing, realm gating and encounter difficulty
  belong to world map, battle engine and event generator. We supply the middle arrow only.

## Dependency graph

```text
                 ┌─────────────── PHASE 0 · dependency resolution ────────────────┐
 seedsmith:   theme-refresh ──► theme-enrich ──► X1 frame-classify
 effect-atom: X7 container kinds · X6 E44 power-sweep · bind_ordinal · D28/E43 tags
 eff-pipeline: X4 L0 (modules 11–12, specced/unbuilt)
 action:      X3 — nothing to do (D36): action-corpus owns it, we wait
 OURS:        core.v1.json v2  +  classes.v1.json v4   ← ONE regeneration pass
                 └───────────────────────────┬───────────────────────────────────┘
                                             │
 PHASE 1 · the spine to the payoff           ▼
 1 durable-ownership ─► 2 armoury ─► 3 slot-roles ─► 4 equip-assign ─► ⭐5 equip-runtime
      (standalone: closes two LIVE defects)                    (first geared corner run)
                                             │
 PHASE 2 · content model (no model calls)    ▼
 7 rarity-bands ─┐
                 ├─► 8 affix-legality ─► 9 item-power-reads ─► 10 item-card
 6 base-types  ──┘        ▲                     ▲
  (E1 §3.8 edit lands     │ needs D28/E43       │ needs X6
   BEFORE D7)             │                     │
                                             │
 PHASE 3 · generation + drops                ▼
 11 drop-volume ─► 13 set-charm-gen       12 threshold-grants
  (supplies AND consumes X4)                (un-gated by D33a)
                                             │
 PHASE 4 · economy and depth                 ▼
 14 salvage-craft ─► 15 enhance-reroll ─► 16 sockets ─► 21 strain-splice-gen
                                             │
 PHASE 5 · content breadth + the player      ▼
 17 uniques · 18 consumables · 19 granted-actions · 22 charm-carry ─► ⭐20 item-surfaces
```

⛔ **X4 carries a real two-way edge:** modules 11 and 13 both **consume** L0's pool composition **and
supply channels to it**. The map no longer claims "no cycles". Resolution in this plan: L0 ships with
its channel set declared but empty; 11 and 13 register their channels on load. **Neither waits for the
other to be complete** — they wait for the registration seam.

## Phases and checkpoints

### Phase 0 — dependency resolution + one regeneration pass

**Nothing in phases 1–5 starts on a dependency someone else has not accepted.** Most external items are
unbuilt today; the first task in each is *get an owner to accept or formally decline*, which costs a
message and prevents a phase stalling mid-build.

⛔ **X3 is the exception, and it defines the etiquette for all of them (D36).** `action-corpus` is
under active construction by another owner. We **do not** file a request against their map, propose
amendments to their scope, or read their documents to infer their schedule. We consume what they ship.
Where a dependency's owner is actively building, *accept-or-decline* means **ask them once, plainly** —
not audit their program to argue they should want it.

⭐ **The regeneration pass is ours and it is one pass, not two.** `core.v1.json` v2 (D30's twelve-role
core) and `classes.v1.json` v4 (D35's lifted quarantine, refilled stopgap slates and the new
directional-profile field) are the same generation run that re-authors D30's 18 legacy sets. Splitting
them costs two full runs and leaves the corpus incoherent in between.

> **CHECKPOINT 0** — every external dependency is *accepted, declined, or built*; both registries are
> bumped; `python -m pytest tools/seedsmith` is green. **A declined dependency is a pass** — it moves
> its dependent module to Phase 5 with the decline recorded. Ambiguity is the only failure.
>
> ⚠ **Amended 2026-09-04, against a measured result:** "the seedsmith gating metrics are green" does
> not hold universally, and it was wrong to write it that way. Bumping `core.v1.json` to D30's shape
> makes `Linkage/SetCompletability` (`gates = True`, wired into CI at `ci.yml:220`) report **18**
> `SetRoleNotHybridCore` findings it was previously blind to (measured: `seedsmith check --adapter
> items --gate` goes from exit 0 to exit 1). **This is D30's own accepted cost** — its ruling text says
> "silently leaving the gate blind is the only expensive answer" — not a defect in the bump. It closes
> when module 13 (`set-charm-gen`, Phase 3) regenerates those 18 sets, which D30 already prices at "no
> additional pass". CI's items-check step is red between here and there; that is expected, not a build
> break to chase.

### Phase 1 — the spine to the payoff (modules 1–5)

⭐ **Module 1 ships first and has standalone value**: `ProduceAndBind` runs in production today
(`RpgStore.UniqueActors.cs:756`), so **both defects are live now** — unequip deletes gear (R1), and one
content import silently disables every rolled item (R2). It is worth shipping whether or not the rest
of the program proceeds.

⭐ **Phase 1 needs no external dependency — verified module by module, 2026-09-04.** Modules 1, 2, 4
and 5 declare none. Module 3's tables are all **registry** data (`core.v1.json` v2), not species data,
so X1 gates only the per-actor **species → frame lookup**.

> ⚠ **Two spec errors found while verifying this, both fixed.** `spec-slot-roles.md`'s §X1 heading read
> *"this module cannot start until `frame` exists"* — **wrong, and it would have stopped a builder on
> day one.** And module 4's `EquipGate` **does** read frame, which the plan had glossed: its frame arm
> ships **inert** (no species carries one) while its predicate, level and faction arms are live. A test
> asserts the inertness and **fails when X1 lands** — which is the reminder to populate. ⛔ Never stub a
> default frame; it would silently admit what the gate exists to refuse.

⚠ **X7 also touches module 4**, not just 12/13/16/18/21 — I11 §2.7's *"an equippable container may not
grant a non-equippable binding"* is effect-atom's load-time validation. ✅ **Not a blocker:** with no
charm/gem/set kinds shipped, there are no charms, so the hole cannot be exercised.

> **CHECKPOINT 1 — ⭐ THE PAYOFF.** One hand-made item on one actor measurably changes a number **in
> battle and on the lawn**. `UniqueActor` bindings reach `AtomPushService`. Unequip removes the
> contribution with no residue. All four boundary guards green. **The first geared corner run
> executes, termination stays green, and dominance reports with its coverage line.**
>
> Everything before this is plumbing with no observable effect. **Do not start Phase 2 until it holds.**

### Phase 2 — the content model (modules 6–10)

No model calls in this phase. By the time the first token is spent in module 13, base types, rarity
bands, affix legality and the power model are all inspectable against real data.

⚠ **Module 7 carries an internal ordering constraint (D31):** the `ssot-rarity` §3.8 scope edit (E1) is
a **predecessor** of D7's tier pity, not a co-delivery. D7 is unimplementable on the tier axis until E1
lands, so E1 ships first and alone if need be.

⚠ **Module 6 is not "author base types."** Phase 0's v4 bump did the unfreezing; this module's
authoring runs *against the lifted quarantine*. Authoring against the current allow-list would bake an
expired quarantine into 740 shipped entries.

> **CHECKPOINT 2** — the dominance lint runs in its **real** form (`power_ceiling` seeded, not the weak
> fallback), so D11 stops degrading silently. Every role has a build where each frame's base is
> correct. An item card renders a real item as readable text with units.

### Phase 3 — generation and drops (modules 11–13)

> **CHECKPOINT 3** — a drop table produces an item at a level, its rarity distribution matches the
> published bands, and a set bonus fires at its breakpoint at `unique-actor:` scope. **No atom is
> written at `player:` scope** (D33a).

### Phase 4 — economy and depth (modules 14–16, 21)

> **CHECKPOINT 4** — salvage → craft → enhance → socket is a closed loop on one item, and
> `CraftingHorizonReport` prints. ⚠ **N ≈ 0.19 realms at v1 depth** is a recorded constraint, not a bug
> to engineer away: both ways of raising it are refused (steepening re-inverts the rarity ladder;
> flattening `contentScale` is PS-7's, not ours). **Do not size module 15's risk bands or pity
> threshold as a progression choice.**

### Phase 5 — content breadth and the player surface (17–20)

⭐ **Module 20 is last and it is not optional.** Nothing owned a player-facing surface until it was
added; `docs/web/spec.md` recorded the same seam as unclaimed from its side. D20 promotes the socket
preview and the combination compendium from nicety to requirement at 127 combinations.

> **CHECKPOINT 5** — a player can see, compare, equip, socket and craft an item in the web control room
> without reading a database.

## Verification, every phase

⛔ **The bar is the baseline, not zero.** Measured 2026-09-04, before any item code:
`Guard` **162/162** and `seedsmith` **1489** are clean — zero-tolerance. But `Core` carries **14**
inherited failures and `Data` **2**, all owned by the demon/seedsmith and world-stage streams, which are
building in this tree. **Compare against 14 and 2, never against green** — and do not fix them from
here. Full diagnosis in [item-todo.md](item-todo.md)'s baseline section.

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-single-writer.ps1      # combat writes only via EntityStatWriter
.\scripts\guard-funnel-delta.ps1       # HP deltas only via Funnel -> FA10
.\scripts\guard-dal.ps1                # SQL only inside FusionRpg.Data
.\scripts\guard-secondary-no-unity.ps1
python scripts\audit-overflow.py       # every magnitude is long
python scripts\audit-magic-numbers.py --summary
python -m pytest tools/seedsmith       # corpus + gating metrics
```

## Risks — named, with the response

| Risk | Response |
|---|---|
| **Phase 0 stalls on another program.** Four of seven external deps are unacknowledged | Checkpoint 0 accepts a **formal decline** as a pass. A declined dep moves its module to Phase 5 with the decline recorded — the phase never waits on silence |
| **The regeneration pass churns the corpus twice** | D30 + D35 are explicitly one pass. Any second bump is a plan deviation and needs saying out loud |
| **The geared corner run goes red at Checkpoint 1** | Termination is HARD and unrepairable by later content — which is exactly why the run sits at module 5 of 21 rather than at the end. A red row here is the plan working |
| ⚠ **`ContentValidation.cs:71` skips a null ceiling**, so `Budget` currently evaluates zero containers and reports green | A green Budget is not evidence until this is fixed. Folded into Phase 2 as a precondition of Checkpoint 2 |
| **The two-way edge on X4** stalls 11 and 13 against each other | L0 ships with an empty declared channel set; 11 and 13 register on load |
| **346 authored `nameWords` are dropped by the only code that reads them** (`AffixFamilyFile.cs:13`) | **Module 8** owns naming (`spec-affix-legality.md:374`); the work is *wiring existing words*, not authoring 210 rows. ⚠ 27 of 98 families are not band-keyed, so a position-indexed naming function would be wrong for those |
