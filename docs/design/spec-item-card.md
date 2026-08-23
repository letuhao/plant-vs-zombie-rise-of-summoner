# The item card — eleven blocks, ten rungs, and the parts that break rules on purpose

**Status:** Detail design, 2026-08-23. **Document 2 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Covers gaps **A1** (item card), **A6** (affix
line + `sourceKind`), **A7** (rarity ladder), **A8** (enhancement), **A9** (requirement gate), **A16**
(uniques), **A17** (base type / categories / implicit).

**Chosen to go first among the item-program documents** because block order, the disclosure rule and
the line grammar are **presentation-layer decisions** — they do not move under any of the item
program's seven open owner decisions (content cut, primary attributes, the `stat.derived` quarantine,
etc.). Everything here is safe to build against before those land.

**Consumes** [spec-magnitude-and-units.md](spec-magnitude-and-units.md) for every number on the card —
no line here invents a rendering rule that document doesn't already own.

**Sources, all read this session:**
[`item/ssot-presentation.md`](../architecture/item/ssot-presentation.md) §4.1, §4.4 (the committed
design) · [`ssot-rarity.md`](../architecture/item/ssot-rarity.md) §3.3–3.5, §4.5–4.6 ·
[`ssot-affixes.md`](../architecture/item/ssot-affixes.md) §4.1–4.3 ·
[`ssot-enhancement.md`](../architecture/item/ssot-enhancement.md) §4 Decision C, §7.1 ·
[`ssot-requirements.md`](../architecture/item/ssot-requirements.md) §2.6–2.7 ·
[`ssot-uniques.md`](../architecture/item/ssot-uniques.md) §3.5–3.6 ·
[`ssot-item-categories.md`](../architecture/item/ssot-item-categories.md) §5.1–5.2.

---

## 0. What is and is not settled by this document

**The item program's own status: "No build is authorized."** [`item/README.md`](../architecture/item/README.md)
lists seven open owner decisions, including the content cut and whether primary attributes exist at
all. **This document does not resolve any of them.** It designs the *renderer*, which is the one part
of the item program's output that a decision on rarity content, cost bands, or attribute count cannot
touch — the card shows whatever the container ends up holding.

What *is* new here and does need saying plainly: **the kit's rarity system today is five ad-hoc
tokens** (`--rarity-1`…`--rarity-5`, [tokens.css:64-68](_kit/tokens.css)), used across roughly ninety
`data-rarity` attributes on all eight plates. The real ladder is **ten rungs**, named, with a measured
lightness progression. §5 below adds the real ten as new tokens **additively** — the existing five stay
exactly as they are, because remapping ninety usages across eight plates is a real migration with its
own review, not a side effect of one document.

---

## 1. The card — eleven blocks, two zones

[ssot-presentation.md §4.1](../architecture/item/ssot-presentation.md) commits this order. **Identity
never collapses. Detail may.**

| # | Block | Contents | Collapses? | Gap closed |
|---:|---|---|---|---|
| 1 | **Header** | enhancement prefix (`+12`) · name · rarity (pips + text + colour) · base type + class noun · role in frame vocabulary · frame badge · item level | never | A7, A8, A17 |
| 2 | **Requirements** | level + I11's clause, red when unmet, **names which number gates** | never | A9 |
| 3 | **Base stats** | `atom.base-*` at seq 0. Plain numbers, no bars — `Fixed` | never | A17 |
| 4 | **Implicit** | the one implicit at seq 1, italic, no bar | never | A17 |
| 5 | **Affixes** | one line per rolled atom, prefixes then suffixes, roll bar per line | never | A6 |
| 6 | **Enhancement** | one block, never stacked lines | never | A8 |
| 7 | **Sockets** | cells, active resonances, near-misses | catalog collapses | *document 3* |
| 8 | **Set** | name, `N / M`, whole ladder | never | *document 3* |
| 9 | **Granted action** | name, description, battle-only tag | never | *document 6* |
| 10 | **Flavour** | uniques only, italic | may | A16 |
| 11 | **Footer** | mean roll quality, stale/locked flags, salvage yield | may | *document 5* |

Blocks 7, 8, 9, 11 belong to documents 3, 3, 6, 5 respectively and are drawn here only as placeholders
so the card reads whole — this document owns 1–6 and 10.

**The one collision, resolved.** [ssot-enhancement.md](../architecture/item/ssot-enhancement.md) picked
`+12` as a name prefix because *"the left edge is what gets scanned"*; rarity's pips also want the left
edge. **Pips first, then `+12`, then the name** — the pips are the rarity ladder's accessibility channel
and must not be displaced by an optional token (presentation §4.1, flagged to the owner as its own
§10.2).

---

## 2. A17 — base type, categories, the implicit

### The container is not enough for equipment, and D.4 said so honestly

[00-foundation.html §D.4](00-foundation.html) currently renders a generic **Container** with the
caption *"a container is a list of atom chips plus rarity — it needs no rendering of its own, which is
the test that the ladder is right."* That test is correct for a **trait, skill, patron, or world-buff**
— it fails for `container_kind = 'equipment'`, which needs the eleven-block structure above. The
container ladder was never wrong; it was drawn against the wrong member of its own family.

### Ten declared categories, four of them still `do not author`

[ssot-item-categories.md §5.1](../architecture/item/ssot-item-categories.md): every category names its
consumer in `item_category.consumer`, **NOT NULL, non-empty** — *"a row no code consumes is not
content; it is a lie in a table."* Six of ten ship; four (`consumable`, `insert`, `charm`, `blueprint`,
`cache` — five, in fact) name an unbuilt consumer and are marked **do not author**. The card must be
able to render that state without inventing a sixth category of its own — it is the same `no-producer`
state [spec-derived-stat-sheet.md §3](spec-derived-stat-sheet.md) already defined for a stat.

### The base stat and the implicit are both atoms, and they never get a bar

`atom.base-*` at `seq 0` and the one implicit at `seq 1` are both `Fixed` (`Min == Max`,
[ValueSpec.cs:9](../../src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs)). Per
[spec-magnitude-and-units.md §6](spec-magnitude-and-units.md), a `Fixed` line gets **no bar**. Base
stats render plain; the implicit renders in italic to distinguish it from the affix list without
implying it rolled.

---

## 3. A6 — the affix line, and the field that groups the card without re-deriving anything

A `DisplayLine` carries `sourceKind`, twelve values
([spec-magnitude-and-units.md §7](spec-magnitude-and-units.md) references the shape;
[ssot-presentation.md §4.4](../architecture/item/ssot-presentation.md) defines the values):

```text
base · implicit · affix-prefix · affix-suffix · enhancement · socket-insert · resonance ·
word · set-threshold · granted-action · unique-identity · unique-variance
```

**Sort key: prefixes then suffixes, then group order, then tier DESC, then `seq` ASC** — content-derived
and ordinal, the same tiebreak discipline `definitions.md` §5 forced on the actor effect list.

### Where a line's group comes from — fifteen groups, verified against the shipped kinds

[ssot-affixes.md §4.1](../architecture/item/ssot-affixes.md): every one of the (corrected) **70**
authored families lands in exactly one of fifteen groups — nine prefix groups (`stat.modify` /
`stat.derived`, permanent, triggerless) and six suffix groups (everything else, triggered). The card
does not need the group→family table to render a line; it needs the fact that **prefix kind is derived
from `kind_id`, never authored** ([item/README.md](../architecture/item/README.md) — *"Prefix versus
suffix is derived from `kind_id`, not authored"*), so the split the header uses is structural, not
content that could drift.

**Six of the fifteen groups are `⛔ quarantined`** on `stat.derived` — see
[actor-hub-ssot.md §6.1](../architecture/actor-hub-ssot.md). An affix line whose kind is quarantined
still renders — the card shows what the container holds — but the runtime-support badge from
[spec-magnitude-and-units.md's D.1 rung](00-foundation.html) applies to it exactly as it does to any
other atom card.

---

## 4. A7 — the rarity ladder, ten rungs, measured

### The real ladder

[ssot-rarity.md §3.3](../architecture/item/ssot-rarity.md), ordinals spaced by 10 so a future rung
inserts without renumbering:

| Ordinal | id | Display | Colour | Pips |
|---:|---|---|---|---:|
| 10 | `chaff` | Chaff | `#63645d` | 1 |
| 20 | `sprout` | Sprout | `#697a5c` | 2 |
| 30 | `grafted` | Grafted | `#509639` | 3 |
| 40 | `cultivated` | Cultivated | `#37a39c` | 4 |
| 50 | `fused` | Fused | `#63a4ed` | 5 |
| 60 | `chimeric` | Chimeric | `#c994ff` | 6 |
| 70 | `heirloom` | Heirloom | `#ff94d2` | 7 |
| 80 | `firstseed` | Firstseed | `#ffab7a` | 8 |
| 90 | `sunwoven` | Sunwoven | `#f9d464` | 9 |
| 100 | `almanac` | Almanac | `#f3eaa0` | 10 |

**Three redundant channels, never one alone** ([ssot-rarity.md §4.5](../architecture/item/ssot-rarity.md)):
lightness is the ladder (`L*` strictly increasing, min adjacent step 2.9, min two-apart step 7.2,
verified monotone under a deuteranope simulation); pip count is what a colour-blind player actually
reads; the rung name is always in text. **"A comparison UI encoding 'better' in hue alone is forbidden."**

**Measured, not eyeballed.** The first draft painted the rung name in its own rarity colour, reading
this section's "always in text" as "text tinted to match". That is not what the SSOT asks for — colour
lives in the pips and the palette; the name only needs to *exist* as text — and painting it as `--rc`
failed WCAG AA on the two darkest rungs: `chaff` at **2.82**, `sprout` at **3.64** against the panel.
Rendering the label in `--text` instead brings every rung to a uniform **14.08**.

### Why ten and not five

[ssot-rarity.md §3.4](../architecture/item/ssot-rarity.md): five count bands × five tier windows walked
as a one-axis-per-step monotone chain admit at most `5 + 5 − 1 = 9` steps; with the pool-less bottom
rung (`chaff`, salvage fodder, `pool_rolls = 0`) that is exactly ten. **An eleventh was tested, not
assumed** — splitting a count band drove the adjacent-rung upset rate to 37.6–38.8%, above the ceiling
that makes a rung name predictive. Every step reads as one sentence: **odd steps widen the pool, even
steps add an affix.**

### What the kit has today, and what changes

The kit's five tokens ([tokens.css:64-68](_kit/tokens.css)) are a **sparse legacy approximation** —
comparing hexes, `--rarity-3` (`#6fa8d9`) and `--rarity-5` (`#e0b44b`) already sit close to real rungs
50 (`fused`, `#63a4ed`) and 90 (`sunwoven`, `#f9d464`). They were never the wrong idea, just an
incomplete sample. **§5 below adds the real ten as new tokens.** The existing `--rarity-1`…`-5` and
their ~90 `data-rarity="1".."5"` call sites across all eight plates are **left untouched** — remapping
them is a real cross-plate migration and does not belong inside one document's scope. Flagged in §8.

---

## 5. A8 — enhancement, as one block that tells the truth about what changed

[ssot-enhancement.md](../architecture/item/ssot-enhancement.md), Decision C picks a mix: a **proportional
scalar** (so a good roll stays good) plus **rarity-neutral milestone atoms** (so a low-rarity item gets
real overlap headroom too) — never a third roll after instantiate, which SC5 forbids.

**Worked, §7.1 — `plate-helm`, item level 64, Epic, +0 → +10 → +20:**

| Line | +0 | +10 (×1.200) | +20 (×1.400) |
|---|---|---|---|
| implicit `might` — **never scaled** | +8 atk | +8 atk | +8 atk |
| affix `vitality` t4 | +62 hp | +74 hp | +87 hp |
| affix `regeneration` t3 (no milestone touches it) | 14/3s | 17/3s | 20/3s |
| milestone `enhance-vigor` | — | t1: +10 hp | t3: +35 hp |
| milestone `enhance-aegis` | — | — | t3: 180 hp shield `OnSpawn` |

`+20 / +0` lands at **1.97× hp, 1.96× atk** — the budget law landing where it says it does. And the
`regeneration` line growing only **1.43×** while `vitality` grows **1.40×** off the same scalar plus a
milestone is the point, not an inconsistency: **an enhancement track is part of a base type's identity,
not a flat tax**, and that asymmetry must be visible.

**Card consequence:** the enhancement block is not a modifier badge. It is a **preview of the next
milestone**, previewable before spending anything — *"a named, previewable atom, not a slot machine."*
A card at +8 with a milestone at +12 shows what +12 unlocks, dimmed, the same `default`-state discipline
[spec-derived-stat-sheet.md §3](spec-derived-stat-sheet.md) already established for a stat that exists
but hasn't been reached.

---

## 6. A9 — the requirement gate, and the number that gates

[ssot-requirements.md §2.7](../architecture/item/ssot-requirements.md): the gate reads an actor's
**unassisted** attribute value — composed from every source *except* the four equippable container
kinds (`item`, `gem`, `set`, `charm`), so two items cannot cycle-unlock each other.

> *"The displayed sheet shows the full composed value with the equipment contribution broken out, so a
> player sees `Sinew 32 (29 + 3)` and the tooltip says which number gates."*

**The card must show both numbers and name which one gates**, exactly as the SSOT states it —
`Sinew 32 (29 + 3) — 29 gates`, red when the gating number (29) is below the requirement, regardless of
whether the composed number (32) would clear it. Showing only the composed total would let a player
believe a swap is legal when it is not.

**A quieter consequence for the affix line (§3):** `+attribute` affixes are real but **non-enabling** —
they feed every derived channel the attribute feeds, and they cannot unlock gear. The card renders them
identically to any other affix; nothing about the line changes. The distinction lives entirely in the
requirement block, which is the only place it is load-bearing.

---

## 7. A16 — uniques, and the two kinds of line that must never look the same

[ssot-uniques.md §3.5](../architecture/item/ssot-uniques.md): the rule in one sentence —

> **A unique may break every rule that lives in the generator, and no rule that lives in the machine.**

Pool tag, tier window, count band, one-per-group — all generator rules, all breakable. The 12 kinds, 5
attach points, 7 triggers, closed predicate list — machine rules, never breakable. A unique carrying
`board.action` or `grid.spawn` is not a bug; it is *"terrible random rolls and good authored ones"* —
five such verbs are shipped and live-proven, and no rolled affix will ever draw them.

### The card's job: two kinds of line, distinguishable by their `sourceKind`

| Part | Count | `sourceKind` | Rolls? |
|---|---:|---|---|
| Inherited base stat | 1 | `base` | no |
| **Identity atoms** | 1–3 | **`unique-identity`** | value only, `OnInstantiate`, spread ≤ ±15% of midpoint |
| **Variance slot** | 0 or 1 | **`unique-variance`** | which atom **and** its value, from a pool authored *only* for this unique |

**This is the answer to a question G1 asked and this document closes:** *"a unique's identity lines must
be distinguishable from its variance line."* `sourceKind` does it precisely — `Fixed` core atoms on a
unique render `unique-identity`; the single `OnInstantiate` slot is `unique-variance` and **is the only
line on that card with a roll bar.** Every other identity line, even though it technically rolled within
±15%, renders **without** a bar — the item is Windforce at both ends of its range, and a bar would imply
otherwise.

`min_tier == max_tier` on a unique's container, so the variance pool sits at exactly one tier — the
card never needs to render a tier window for it, only the roll bar for the one draw.

---

## 8. What this document deliberately does not draw

- **Content** — no real rarity table, affix pool, or base-type roster exists to draw from; every
  example above is the SSOT's own worked example, cited, not invented.
- **The ~90-usage rarity migration.** Real, necessary, and out of scope here — see §4.
- **Blocks 7–9, 11** — sockets/sets (document 3), granted actions (document 6), footer/salvage
  (document 5). Placeholders only, so the card in §9 reads as a whole card.
- **The seven open item-program decisions.** None of them changes anything in this document.

---

## 9. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **The eleven blocks render in order and Identity blocks (1–6) never collapse** | a layout hides a base stat or the header to save space |
| 2 | **Every rendered line carries a `sourceKind`** from the twelve closed values | a line cannot be grouped without re-deriving where it came from |
| 3 | **`Fixed` lines (base, implicit, unique identity) never render a bar** | a full bar implies a roll that could not have happened |
| 4 | **Rarity renders pips + text + colour, never colour alone** | a colour-blind player cannot read the ladder |
| 5 | **The requirement block shows the composed number, the gating number, and names which gates** | a player believes an equip is legal because the composed total clears it |
| 6 | **A unique's variance slot is the only line with a bar; identity lines never get one** | an identity line's ±15% spread is rendered as though it were a normal roll |
| 7 | **A category marked `do not author` renders `no-producer`, never a silent absence** | an unbuilt category's row disappears instead of explaining itself |

---

## 10. What this changes elsewhere

| File | Change |
|---|---|
| [00-foundation.html §D.4](00-foundation.html) | the Container caption is narrowed to non-equipment kinds; a new **§D.7** carries the full item card |
| [_kit/tokens.css](_kit/tokens.css) | ten new rarity tokens added, additively — §5 |
| [_kit/kit.css](_kit/kit.css) | a rarity-chip component (pips + text) distinct from the existing `.frame[data-rarity]` border system |
| [gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) | A1, A6, A7, A8, A9, A16, A17 close |

---

## 11. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — item presentation, rarity, affixes, enhancement,
    requirements, uniques, base types/categories.
[x] I read every doc in the §1 row(s) this session: item/ssot-presentation.md §4.1/§4.4,
    ssot-rarity.md §3.3-3.5/§4.5-4.6, ssot-affixes.md §4.1-4.3, ssot-enhancement.md Decision C/§7.1,
    ssot-requirements.md §2.6-2.7, ssot-uniques.md §3.5-3.6, ssot-item-categories.md §5.1-5.2,
    item/README.md.
[x] I checked decisions.md for a lock covering this (Game GUI row). The item program itself is
    explicitly "no build authorized" — this document does not treat that as a lock on the RENDERER,
    stated as the reason in §0.
[x] Every factual claim cites file:line or a document section.
[x] I verified claims against CODE where code exists — ValueSpec.cs for Fixed/no-bar, the existing
    kit's --rarity-1..5 hexes read directly from tokens.css and compared numerically against the real
    ladder rather than assumed to be wrong.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite exists for this
    program (item/README.md: "no code, no schema"). Every number in §5 (worked enhancement example)
    is the SSOT's own cited worked example, not independently computed — unlike documents 1/8/9,
    there is no running code to verify against here.
[x] Nothing contradicts a §2 invariant.
[~] Corrections propagated. Plate changes are listed in §10 and land in the same pass. The ~90-usage
    rarity migration is EXPLICITLY NOT done — flagged, not silently deferred.
```
