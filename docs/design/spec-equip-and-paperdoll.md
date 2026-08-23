# Equip and the paper-doll — fifteen roles, two vocabularies, one unlock ladder

**Status:** Detail design, 2026-08-23. **Document 6 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Covers gaps **A10** (equip slots / paper-doll)
and **A19** (item-granted action). Fills item card block 9, left as a placeholder in
[spec-item-card.md §1](spec-item-card.md).

**Chosen ahead of documents 5 and 7** because it carries the least exposure to the item program's open
owner decisions and the most already-written material — 32 display strings, a fully worked hybrid
budget proof, and a level-gated unlock table are already in the source SSOT, none of them contingent
on the content-cut or primary-attribute decisions.

**Sources, all read this session:**
[`ssot-equip-slots.md`](../architecture/item/ssot-equip-slots.md) §2.1–2.10, §4 ·
[`ssot-granted-actions.md`](../architecture/item/ssot-granted-actions.md) §3.6–3.7 ·
[`item/ssot-presentation.md`](../architecture/item/ssot-presentation.md) §4.1 row 9.

---

## 1. Three keys, and the paper-doll cares about exactly one of them

```text
frame   — humanoid | plant | hybrid        ← this document
role    — armament-primary, core-guard, …  ← this document
faction — plant or zombie allegiance       ← not this lane, not this document
```

**The role id is the data; the display word is a lookup on `(role_id, frame)`.** `head` and `crown` are
the same slot wearing two vocabularies — this is the single load-bearing decision in the source lane,
because if the display word were the stored value, an affix pool authored for helmets would need
authoring twice. The paper-doll must never let its slot art or its label leak into the id it binds to.

**Frame is a declared field, never derived from faction.** `DemonSpeciesDef.Side` conflates capture
side with body — the roster already contains zombie-side entries with plant bodies
(`peashooterzombie`, `cherrynutzombie`), verified at
[`DemonSpeciesCatalog.cs:11`](../../src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs). A paper-doll that
infers frame from side would draw a plant body on a specimen whose faction says zombie, or vice versa.

---

## 2. The fifteen roles, two vocabularies, one budget

| # | `role_id` | Humanoid | Plant | ‰ | Axis |
|---:|---|---|---|---:|---|
| 1 | `armament-primary` | main-hand | muzzle | 160 | what it hits with |
| 2 | `core-guard` | torso | stem | 120 | the HP pool itself |
| 3 | `ward-array` | shoulders | sheath | 90 | shields — spent before HP |
| 4 | `armament-secondary` | off-hand | thorn | 80 | the answering half |
| 5 | `jewel-major` | neck | pollen | 80 | strongest non-weapon affix |
| 6 | `manipulator` | hands | leaves | 70 | rate, crit damage, on-hit |
| 7 | `mantle` | back | canopy | 60 | elemental mitigation |
| 8 | `head-guard` | head | crown | 60 | resistance to being disabled |
| 9 | `girdle` | waist | soil | 60 | the resource role |
| 10 | `sense` | face | bract | 50 | accuracy, crit rate |
| 11 | `footing` | feet | roots | 50 | evasion / stability, frame-split |
| 12 | `infusion` | bandolier | glands | 50 | what your hits inflict |
| 13 | `retinue` | horn | runner | 40 | spawns, board, grid |
| 14 | `jewel-minor-a` | ring-1 | graft-1 | 15 | degree, capped |
| 15 | `jewel-minor-b` | ring-2 | graft-2 | 15 | the identical twin |

Weights are integer per-mille of a fully-geared pure frame's total budget and sum to **1000** —
ratios, not points, so the paper-doll needs nothing from E9's unbuilt power model to show relative
slot weight (a bar length, not a magnitude).

### The plant vocabulary is not humanoid words with a plant skin

Every plant slot answers one test: *can a rooted, handless thing possess this, or would it have to put
it on?* Five categories, none of them clothing:

| Category | Slots | The fiction |
|---|---|---|
| **Growth** | crown, bract, stem, canopy, leaves, sheath, runner | parts the plant produced — `sheath` sheds like a shield; `runner` is the stolon, the exact botanical word for a summon slot |
| **Substrate** | roots, soil | what it stands *in* — `soil` is the resource slot because that is where a plant draws from |
| **Secretion** | pollen, glands | what it emits |
| **Graft** | graft-1, graft-2 | grafted cuttings — the botanically correct way a plant acquires a foreign part |
| **Apparatus** | muzzle, thorn | the aperture it fires through, the spines it answers with |

**Rejected on sight, and why it matters for the art direction too:** `leaf-gloves` (a plant has no
hands under them), `root-boots` (footwear implies walking), `petal-cape` (a cape hangs from a back), the
whole set failing the same test — implying an anatomy the plant doesn't have. The paper-doll's plant
silhouette must not accidentally reintroduce one of these by drawing a boot-shaped icon on `roots`.

### The twin minor jewels — a duplicate, priced on purpose

`jewel-minor-a`/`b` are identical, and that's deliberate: expressing *degree* is worth a slot, priced
by three shipped mechanisms rather than a special rule — 15‰ each (the smallest pair), tier cap 3
against `jewel-major`'s 5, and the six strongest families (`bulwark`, `savagery`, all four shield
families) **absent from both minor-jewel legality lists**. You may double a mid affix; you may not
double a top one. The paper-doll renders them as two visually identical slots — no asymmetric flavour
icon, because differentiating them would undo the only thing that justifies the pair existing.

---

## 3. The unlock ladder — GG-44, with real numbers

[ssot-equip-slots.md §2.10](../architecture/item/ssot-equip-slots.md): roles open on actor **level**,
read from `rpg_unique_actors.level` — the column already exists and already advances. A new specimen
starts with **four** slots, not fifteen, which directly answers the *"twenty demons × twelve slots is a
gearing chore"* worry: a bench specimen needs four items, and only actors you actually level reach all
fifteen.

| Level | Opens | Note |
|---:|---|---|
| **1** | `armament-primary` · `core-guard` · `head-guard` · `jewel-minor-a` | 355‰ of budget live from the start |
| 3 | `armament-secondary` | |
| 5 | `manipulator` | |
| 8 | `footing` | |
| 11 | `girdle` | |
| 14 | `sense` | |
| 17 | `mantle` | |
| 20 | `jewel-major` | the first "chase" unlock |
| 24 | `ward-array` | **hybrid: never** |
| 28 | `infusion` | |
| 32 | `retinue` | hybrid's last unlock |
| 36 | `jewel-minor-b` | **hybrid: never** |

**A self-caught error while building the plate.** The first render of this ladder hand-assigned each
row's open/locked state instead of deriving it from `unlock_level ≤ viewed_level`, and five rows —
`armament-secondary` (unlocks 3), `manipulator` (5), `footing` (8), `girdle` (11), `sense` (14) — were
marked locked while sitting at or below the viewed level of 14, contradicting the plate's own caption.
Fixed by deriving state from the level comparison rather than hand-setting it per row — the same class
of self-consistency bug caught in other people's SSOTs earlier in this program, this time in my own
plate code before it shipped.

**This is precisely the `Pending<T>` / default-state pattern**
[spec-derived-stat-sheet.md §3](spec-derived-stat-sheet.md) already established for a stat that exists
but hasn't been reached: a locked slot is not absent, it is `default` — real, gearable eventually,
rendered dimmed with the unlock level stated on it, never hidden. **A slot the player hasn't reached yet
must say when, not just that it's locked** — `Unlocks at level 20`, not a bare padlock.

---

## 4. Hybrid — thirteen roles, and the price is provably fair

A hybrid drops `ward-array` (90‰ — no coherent outer layer on a body that's half bark, half bone) and
`jewel-minor-b` (15‰ — a second graft onto something already two things doesn't take). **The shield
families aren't lost, only the slot is** — they become legal on `core-guard` at `max_tier = 3`, so a
hybrid trades *shields or hit points* inside one fixed budget rather than losing shields outright.

**Two independent derivations of the same number, cross-checked:**

| | Pure | Hybrid |
|---|---:|---:|
| Roles | 15 | 13 |
| Budget ‰ | 1000 | **895** |
| Roll count (§4.1's bands, summed) | 63 | **56** |

Verified this session: `6+6+5+5+5+4+4+4+4+4+4+3+2 = 56`. `56/63 = 88.9%`, matching the `895/1000 = 89.5%`
budget ratio within 0.6pp — two independently-derived numbers landing on the same answer is the SSOT's
own check, and it holds.

**Why an ~11% cut for double the loot pool is fair, not generous:** for roughly uniform item quality,
the expected best of *N* candidates is *N/(N+1)* of the range — at N=10 that's a **~4.7%** quality lift,
shrinking as N grows. So doubling the pool is worth roughly 5% of power and a large amount of
unmeasured *convenience* (every drop is useful; the bag fills slower). A 10.5% cut over-prices the
power on purpose: a hybrid at 89% of a pure frame is a real choice, a hybrid at 100% is the only one.
*(The 4.7% figure is order-statistics arithmetic on a uniform-quality assumption — not measured, and
flagged for recheck against real drop distributions before it's used to balance anything.)*

**The paper-doll's job:** a hybrid silhouette with 13 filled positions and `ward-array` /
`jewel-minor-b` rendered **absent**, not locked — a different visual state from "not yet unlocked,"
since no level will ever open them for this frame. Absent-by-frame and locked-by-level must not share
a rendering, or a hybrid player spends real time looking for a level that doesn't exist.

---

## 5. The commander's `standard` — one extra slot, and why not two

Commanders get the full fifteen of their chosen frame **plus one**: `standard` (humanoid `banner`,
plant `root-totem`), binding at **`match` scope** so its atoms reach the whole squad. It is exactly one
slot for three stated reasons: it's already the *only* position in the game where `+defense` legally
does anything (`warding`/`resilience` are `ScopeUnsupported` everywhere except `match`); a match-scoped
atom worth X on one actor is worth roughly `X × squad size` — at five actors, its nominal 100‰ is
~500‰ effective, half a body again; and one slot keeps commander itemisation *a choice*, where four
would just be another body. Priced from a **separate** 100‰ commander budget, not drawn from the body's
1000‰. The paper-doll renders it visually distinct from the fifteen — a squad-scoped badge, not a
sixteenth body slot.

---

## 6. Item card block 9 — the granted action, stated honestly

[ssot-granted-actions.md §3.6](../architecture/item/ssot-granted-actions.md), read against the shipped
runtime matrix
([`AtomKindRegistry.cs`](../../src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs)): **eleven of
twelve atom kinds have no battle consumer at all.** A weapon's *numbers* (`stat.modify`) work only on
the lawn; a weapon's *granted action* works only in battle. Neither runtime executes both halves of the
same item. This is deliberately not smoothed over — the picked option (b) is *"battle content, and say
so"*, with the display requirement stated as part of the pick, not a nicety:

> A base type with a grant row carries a **battle-only** presentation tag, and the card must render it.

**Two more states this block must carry, both decided from shipped code:**

- **"Already known."** Two items granting the same `action_id` dedup to one entry —
  `CooldownLedger` keys `(ActorKey, CooldownKey)`, so two "instances" would share one clock regardless.
  The item is not broken; the player must be able to tell this line of its text is doing nothing *for
  this actor*. The same item on a different species is a real upgrade, so the card reports, never
  refuses.
- **Default-attack precedence**, declared not emergent: `armament-primary`'s `default-attack` if any,
  else the species' intrinsic attack. `default-attack` is legal only on `armament-primary`, so a
  two-handed item (which reserves `armament-secondary`) can never conflict with itself.

---

## 7. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **The paper-doll binds to `role_id`, never to a display string** | a helmet's affix pool has to be authored twice for two vocabularies |
| 2 | **A locked slot states its unlock level; an absent (hybrid) slot never does** | a hybrid player looks for a level that will never come |
| 3 | **`jewel-minor-a`/`b` render identically** | a flavour asymmetry undoes the only reason the pair exists |
| 4 | **Plant slot icons never imply an anatomy the plant lacks** — no boots on `roots`, no cape on `canopy` | the fiction test in §2.6 is violated visually even though the id is correct |
| 5 | **A granted action always carries the battle-only tag when it applies** | a player believes a weapon's headline property works on the lawn |
| 6 | **A duplicate granted action reports "already known", never silently vanishes** | the player can't tell the item is doing nothing for this actor |
| 7 | **`standard` renders visually distinct from the fifteen body slots** | a squad-wide slot reads as just another ring |

---

## 8. What this document deliberately does not draw

- **Real base-type content** for each role — none is authored yet; every number above is the SSOT's
  own cited figure.
- **The action layer's own UI** — targeting, cooldown, the action bar (document 7).
- **Two-handed / dual-wield rendering mechanics** beyond stating the rule (§2.7's reservation and
  budget) — a layout question for whichever screen hosts the paper-doll, not this document's ladder.

---

## 9. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — equip slots, frame, granted actions, item presentation.
[x] I read every doc in the §1 row(s) this session: ssot-equip-slots.md §2.1-2.10/§4,
    ssot-granted-actions.md §3.6-3.7, item/ssot-presentation.md §4.1 row 9.
[x] I checked decisions.md for a lock covering this (Game GUI row; decisions.md:90 on actions being
    battle-mode only, cited directly).
[x] Every factual claim cites file:line or a document section.
[x] I verified claims against CODE where code exists — DemonSpeciesCatalog.cs:11 for the frame/faction
    conflation, AtomKindRegistry.cs for the 11-of-12 battle-consumer claim, CooldownLedger.cs:8 for
    the dedup rule; the 56-roll hybrid sum was recomputed independently this session, not copied.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite exists for this
    program. The 56/63 vs 895/1000 cross-check is arithmetic verification of the SSOT's own claim,
    not an execution against running code — there is no equip-slot code shipped yet to run against.
[x] Nothing contradicts a §2 invariant.
[x] Corrections propagated — no correction was needed this pass; the source SSOT's own arithmetic
    held up under an independent recheck.
```
