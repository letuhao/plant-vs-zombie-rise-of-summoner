# Sockets and sets — combination legibility, one evaluator, no wiki

**Status:** Detail design, 2026-08-23. **Document 3 of 9** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Covers gaps **A4** (socket · insert · resonance ·
word) and **A5** (set threshold ladder). Fills item card blocks 7 and 8, left as placeholders in
[spec-item-card.md §1](spec-item-card.md).

**Sources, all read this session:**
[`ssot-sockets.md`](../architecture/item/ssot-sockets.md) §4.4–4.7, §7 ·
[`ssot-sets.md`](../architecture/item/ssot-sets.md) §3.2–3.6, §6.1 ·
[`item/ssot-presentation.md`](../architecture/item/ssot-presentation.md) §4.3 ·
[spec-magnitude-and-units.md](spec-magnitude-and-units.md).

---

## 0. Two more instances of a defect document 1 already named

Both worked examples this document draws from label `combat.power.fire` **"resolver points"** —
[ssot-sockets.md §7.1](../architecture/item/ssot-sockets.md): *"+30 resolver points ⚠️"*;
[ssot-sets.md §6.1](../architecture/item/ssot-sets.md): *"+30 fire power … 30 resolver points on a
sigmoid where `CritRateScale = 100.0`."*

[spec-magnitude-and-units.md §2](spec-magnitude-and-units.md) verified against
`OverlayCombatCalculator.cs` that `combat.power.*` is **`GameUnits`** — one damage point, no sigmoid,
`CombatDerivedReader.Power` has exactly one call site and it never touches `Sigmoid`. Both SSOTs are
citing the same corrected-away model [ValueSpec.cs:24-26](../../src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs)
carries — the same error, propagated into two more files by two more authors working from the same
stale comment. **Flagged, not edited** — `item/` is a separate program's files, same courtesy extended
to `ValueSpec.cs` in document 1. Rendered correctly below regardless: `+30 fire power`, no "points."

(One adjacent term in the same examples, `combat.crit.damage.ice` as *"+45 resolver points"* — is
**not** wrong, just using the pre-ledger name for what document 1 calls `SigmoidMultiplierPoints`.
Rendered here as `≈ ×1.45 vs neutral`, per that document's §5.6 table.)

---

## 1. Sockets — the ceiling is ~45, and it is generated, not authored

[ssot-sockets.md §4.4](../architecture/item/ssot-sockets.md) picks **B as the floor, A as the ceiling,
nothing in between** — named exact recipes alone chase a wiki; colour-counts alone are flat; tag
thresholds have no fiction; ordered patterns are unguessable.

### The floor — resonance, fully generated

| Shape | Fires when | Rows |
|---|---|---:|
| **Pure** | k inserts share one concrete element, k ∈ {2,3,4} | 6 elements × 3 = **18** |
| **Ring** | ≥1 insert of each of two ring-adjacent elements | **4** |
| **Eclipse** | ≥1 `light` and ≥1 `dark` | **1** |
| **Diversity** | 3 or 4 *distinct* elements present | **2** |

**25 generated**, enumerable in the UI from the roster the same way the atom library generates its
element families — the whole set is inferable from two examples, never memorised from a wiki.

**`omni` counts toward Diversity only** — never Pure, Ring, or Eclipse. It is the deliberate no-combo
option: raw additive power for a player who doesn't want the puzzle. **The card must say so on the
omni insert's own line** ([ssot-presentation.md §4.3](../architecture/item/ssot-presentation.md)) — an
omni sitting in a three-fire fill that isn't firing Pure looks broken unless the socket itself explains
why.

### The ceiling — words, hand-authored, revealed by holding, never by reading

≤ 20 words, ordered, exact ingredients. The wiki problem is solved structurally, not by hiding the
list: **a word reveals in the compendium once the player has held every ingredient at least once**, and
the socket UI previews which combinations the current fill produces *and which are one insert away*.
The recipe is a stated goal, not secret knowledge.

**25 + ≤20 ≈ 45 total** — the number [ssot-presentation.md §4.1](../architecture/item/ssot-presentation.md)
already cites for the catalog size the card's full-list expansion holds.

### The four-state model, and the one evaluator that produces all four

[ssot-presentation.md §4.3](../architecture/item/ssot-presentation.md), closed:

| State | Rendered | Condition |
|---|---|---|
| `active` | full colour, name, atom lines | the evaluator returned it |
| `one-away` | dimmed, name, atom lines, **the exact missing ingredient named** — *"needs 1 more Ember Shard"* | `distance == 1` |
| `known-inactive` | dimmed, name only, atoms hidden | every ingredient held at least once, `distance > 1` |
| `undiscovered` | **not rendered at all** | reveal has not fired, or unreachable on this item |

**Near-miss is computed by the same evaluator that computes the active set, called once with a
distance parameter — never a second function.** Two functions is precisely how *"the tooltip said one
more and it did not fire"* happens.

```text
distance(combination, fill) = minimum insert substitutions that would satisfy it,
                               counting an empty socket as one substitution

Pure-k:  distance = max(0, k − count(e)),  ∞ if the item lacks k sockets  →  undiscovered, never one-away
Word:    distance = number of ordered positions whose required ingredient is absent
```

An item with two sockets can never show `one-away` toward a Pure-4 — it shows nothing, because
promising a shape the item structurally cannot reach is worse than silence.

### Worked, verified against the SSOT's own numbers

**A 3-socket plant chest**, affinities `[earth, earth, fire]`, filled `stone-heart / stone-heart /
ember-shard`:

- **Pure earth fires**: 2 earth inserts, both in earth-affinity sockets → every contributor attuned →
  effective count **3**. Grants `+80‰ maxHp` (Increased — renders `+8% hp`, per
  [spec-magnitude-and-units.md R4](spec-magnitude-and-units.md)) and `+6 hp / 5.0 s` — **a different
  shape from the inserts**, a multiplier and a regen tick against two flat adds, never just more of
  what's already there.
- **Pure fire**: 1 insert, below the k=2 floor. Not rendered — `undiscovered`, not `one-away`, since a
  single fire insert with no second fire slot open is a real structural ceiling, not a near miss.
- The third insert (`combat.power.fire`, +30 GameUnits) still renders on the card — **the runtime-support
  badge is what tells the truth about it**, not the socket layer. This atom is `stat.derived`,
  currently quarantined `None/None/None` — the same three-state badge D.1's third atom card already
  demonstrates.

**A 3-socket weapon**, top rarity, fill `ember-shard(fire) / rime-tear(ice) / ember-shard(fire)`:
**three combinations at once** — the word `Frostfire` (its 3 exact positions matched), Pure fire at
effective count 2 (one fire insert sits in a non-fire-affinity socket, so it isn't "every contributor
attuned" — no +1), and Ring fire-ice (adjacent on the ring, both present). This is the stated ceiling
for a top-rarity 3-socket item — a 4-socket item could stack more, which is exactly why `min_sockets`
and once-per-shape caps exist.

---

## 2. Sets — the whole ladder always renders, because the inactive thresholds are the goal

[ssot-sets.md §3.2](../architecture/item/ssot-sets.md) inverts genre convention on purpose:

> **Every set grants exactly one capability atom, and it sits at the lowest threshold.** Every higher
> threshold grants `stat.modify` / `stat.derived` — plain numbers — only.

A 2-piece splash gets the thing rares cannot roll at all; full commitment gets numbers. **Two half-sets
and one full set are two different, roughly equal answers** — the definition of a build space, not a
checklist. The genre's cautionary case (Diablo 3's 6-piece multipliers, abandoned in Diablo 4) is what
this inversion exists to avoid, and it costs something real: less "chase the last piece" feeling, paid
deliberately.

### The card renders the ladder differently from sockets, and must

Unlike the ~45-item socket catalog, a set has **at most 6 members and a handful of thresholds** — small
enough that hiding any of it removes the goal rather than protecting a surprise. So:

> **The whole ladder always renders. Every threshold, active lit, inactive dimmed but with its atoms
> visible.**

A threshold line **names the piece count it needs, never "next."** `4 pieces:` — not `Next tier:`. If
the player unequips down to two, "next" meant something different an hour ago and a screenshot taken
then is now a lie.

### Worked — Ember Legion, 4 pieces, verified against the SSOT's own budget check

| Threshold | Grants | Kind |
|---|---|---|
| **2** (capability) | `atom.warded.fire.t3` — 120 shield, `OnSpawn` | `shield.grant` |
| **4** (numbers) | `+35 atk` · `+45 hp` · `+30 fire power` | `stat.modify` × 2, `stat.derived` × 1 |

**The `+30 fire power` line is stated honestly, not smoothed over.** It's `stat.derived`, quarantined
until E12 wires `BattleStatComposer` — the set's most thematically obvious grant is the one thing it
cannot deliver yet, and [ssot-sets.md](../architecture/item/ssot-sets.md) says so in its own text
rather than shipping it silently inert. The card's runtime-support badge (D.1) is what carries that
honesty into the UI — the same mechanism, a third time, after the atom card and the socket insert.

**Budget, self-checking:** cap is 4 members × 1.5 AE = 6.0 AE. Shield ≈ 2.0 AE + three t3 stat atoms ×
~1.0 AE = 3.0 AE → **5.0 ≤ 6.0**, passes. A completed set is worth roughly a third of an affix more than
four comparable rares, plus a capability rares cannot roll — not a power spike, a real but modest edge.

### Two partial sets — legal, budgeted, expected

[ssot-sets.md §3.5](../architecture/item/ssot-sets.md)'s anti-jail mechanisms, ranked by how much work
they do: (1) capability at the floor — load-bearing; (2) **no `More`-op on a set tier**, so `Increased`
sums against the whole build and a set's *share* of total power falls as the build grows, self-
diminishing by construction; (3) a hard 1.5-AE-per-member budget; (4) no set owns both weapon roles;
(5) two partials are explicitly the design target. **The card must render two simultaneously-tracked
ladders** when a player is running two partial sets — neither collapses to make room for the other.

---

## 3. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **Near-miss and active state share one evaluator**, called with a distance parameter | a second function computes near-miss and drifts from what actually fires |
| 2 | **`omni` never counts toward Pure/Ring/Eclipse**, and says so on its own line | an omni insert in a non-firing fill reads as broken |
| 3 | **A combination cannot show `one-away` if it is structurally unreachable** | a 2-socket item promises a 4-insert Pure |
| 4 | **A set's whole ladder always renders**, inactive thresholds dimmed but atoms visible | a threshold hides and removes the goal |
| 5 | **A threshold names its piece count, never "next"** | a screenshot becomes wrong the moment the player unequips one piece |
| 6 | **Two partial sets render as two independent ladders** | one collapses when a second is being tracked |
| 7 | **A quarantined atom (`None/None/None`) still renders**, with its runtime badge | a set or socket grant that doesn't work yet is hidden instead of stated |

---

## 4. What this document deliberately does not draw

- **Real content.** Ember Legion and the frostfire word are the SSOTs' own worked examples, cited, not
  invented — no real word list or resonance content exists yet.
- **The mutation/replacement UI** for changing an insert (I7's territory, document 5).
- **Set-eligible minimum ordinal gating** — a display concern of I1's rarity registry, not this
  document's ladder.

---

## 5. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — sockets, sets, item presentation.
[x] I read every doc in the §1 row(s) this session: ssot-sockets.md §4.4-4.7/§7,
    ssot-sets.md §3.2-3.6/§6.1, item/ssot-presentation.md §4.3.
[x] I checked decisions.md for a lock covering this (Game GUI row).
[x] Every factual claim cites file:line or a document section.
[x] I verified claims against CODE where code exists — the "resolver points" mislabel in both
    worked examples was checked against OverlayCombatCalculator.cs (already verified in document 1),
    not re-derived from scratch.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no test suite exists for this
    program (item/README.md: "no code, no schema"). The worked examples in §1 and §2 are the SSOTs'
    own cited numbers, re-verified for internal consistency (the budget check in §2 sums to the
    SSOT's own stated total) but not independently computed against running code, unlike documents
    1/8/9 which had shipped code to check against.
[x] Nothing contradicts a §2 invariant.
[~] Corrections propagated. §0's finding is flagged in this document; the two item-program SSOT
    files are NOT edited, matching the courtesy already extended to ValueSpec.cs in document 1.
```
