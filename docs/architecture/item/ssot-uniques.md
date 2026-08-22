# Lane G1 SSOT — uniques as a content class

**Status:** Lane G1 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md). Gap lane **G1** of the
[reconciliation plan](reconciliation-plan.md) §R3 — a mechanism the thirteen-lane round did not assign
to anyone.

Read this session, in the contract's §5 order plus the brief's extras:
[enrichment-contract.md](enrichment-contract.md), [reconciliation-plan.md](reconciliation-plan.md),
[item-ideal.md](../item-ideal.md) (§6.2 is the framing this lane corrects),
[ssot-rarity.md](ssot-rarity.md), [ssot-affixes.md](ssot-affixes.md),
[ssot-generation.md](ssot-generation.md), [ssot-item-categories.md](ssot-item-categories.md),
[ssot-sets.md](ssot-sets.md), plus the sections of [ssot-reroll.md](ssot-reroll.md),
[ssot-enhancement.md](ssot-enhancement.md), [ssot-requirements.md](ssot-requirements.md),
[ssot-inventory.md](ssot-inventory.md) and [ssot-sockets.md](ssot-sockets.md) that name uniques;
[../effect-atom/definitions.md](../effect-atom/definitions.md) §§0–2, 4–6, 10, 14,
[../effect-atom/spec-container-schema.md](../effect-atom/spec-container-schema.md),
[../effect-atom/spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md),
[../effect-atom/atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md),
[../effect-atom/atom-family-library.md](../effect-atom/atom-family-library.md).

Code opened this session: `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs`,
`ContainerRow.cs`, `Instantiator.cs`, `AtomRejection.cs`, `PredicateNode.cs`, `AtomKindRegistry.cs`,
`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs`.

**Honest limits up front.** No test suite was run for this document, so every constraint below is read
from code and specs rather than executed — the design gate's evidence box is unticked and §11 says so.
The parity invariant in §3.7 is **stated and not measured**: I1 built a simulator for its overlap
invariant and I did not re-run it. Recalled prior-art numbers are marked unverified.

---

## 2. Scope

### This lane owns

- **What a unique is** as a content class, and the one rule that separates it from a rare with a good name.
- **What a unique may do that the generator may not** — the rule-breaking budget, ranked by architectural cost, and the line it may not cross.
- **Where uniques sit relative to the rarity ladder** — the flag, not the rung.
- **The authoring shape**: fixed identity atoms, the single variance slot, and the discipline on both.
- **The mutual-relevance mechanism** — the structural device that stops uniques obsoleting rolled loot in either direction.
- **Participation** in sockets, enhancement, reroll and sets — a yes/no with a reason for each.
- **What a unique entry needs from the drop pipeline**, including the deterministic acquisition path I1 left open at its top rung.
- **The v1 content budget** — how many, and what one costs in rows.
- Unique-specific validation and reason codes.
- The **terminology collision** on the word "unique" in this tree (§3.1), which the contract's §1 lock did not cover.

### This lane does NOT own

| Not ours | Owner |
|---|---|
| The rarity ladder, its ordinals, the overlap invariant | **I1** — we register one budget key and read the rung |
| The affix pool, tier bands, the AE unit, the naming grammar | **I8** — we own the *right to bypass the pool*, not the pool |
| Sets, set membership, set-bonus tiers | **I5** — boundary drawn explicitly in §3.8 |
| The drop pipeline, drop tables, correlation and pity | **I12** — we declare what a unique entry needs from it |
| Base types, implicits, base stats, class ladders | **I3** — a unique occupies one of their rows |
| Equip roles and slot names | **I2** |
| Sockets, inserts, socket geometry | **I4** |
| The instance-mutation model and the op log | **I6** |
| The reroll menu and its prices | **I7** — we request one operation |
| Material and cost vocabulary, salvage yield | **I9** |
| Equip gating and requirement expressions | **I11** |
| Bags, stacking, comparison, the item row | **I13** |
| Tooltip text and how a fixed line reads | **G3** (presentation) |
| Item-granted actions | **G4** — and §4.3 says why half the genre's iconic uniques live there, not here |

---

## 3. The model

### 3.1 First, the word

Before anything else, because this tree already spends "unique" three ways and a fourth is arriving:

| Meaning | Where it lives today | Keep? |
|---|---|---|
| A **specimen** — one durable individual demon | `rpg_unique_actors` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:337`), `unique-actor-runtime.md` | Yes, unchanged. Nothing to do with items |
| The **equipment stub** hanging off those specimens | `rpg_unique_equipment` (`RpgStore.cs:356`), `UniqueEquipmentCatalog` (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs`) | **Legacy.** It is called "unique" because it belongs to unique *actors*, not because its three items are uniques. Retired by I2/I13 |
| The SQL `UNIQUE` constraint | everywhere | Yes |
| **This lane's content class** | new | Yes — and every table it adds is prefixed `item_unique_*` so it never reads as actor-unique |

That is a real hazard, not pedantry: a reviewer reading `UpsertUniqueEquipment` will reasonably assume
it is this system, and it is not. **The contract's §1 terminology lock should gain this row at R4.**

### 3.2 The premise, tested

> A unique is not a rarity. It is a content class whose defining property is that it breaks the rules
> the generator obeys.

The premise holds, and the architecture already agrees with it in shipped code. `ContainerValidator`
checks the tier window **only inside the pool loop** (`ContainerValidator.cs:73-96`); the fixed-core
loop (`:44-57`) never looks at `min_tier`/`max_tier` at all. The code even says why, in a comment on the
line above the check: *"The window governs what the POOL may offer; a fixed core says what the thing
is"* (`ContainerValidator.cs:87`). Override validation is the same shape — `ValidateOverrides`
(`:134-176`) rejects an unknown param, a kind rewrite, and a malformed value spec, and it **never
compares a magnitude against a band** (`:167-171`).

So the substrate already draws the line this lane needs. **The generator's rules live on the pool. The
machine's rules live on the atom, the kind, and the bind gate.** A unique is a container that uses the
first half and obeys the second.

The corollary is the definition, and it is a refusal:

> **A hand-authored item that only rolls higher numbers is not a unique.** It is a rare with a name, and
> §6 rejects it at import. A unique must carry at least one atom that the role's rolled pool cannot
> offer at all — a different kind, a predicate, or a family the pool tag excludes.

That is checkable, and §6 checks it.

### 3.3 What the correction is, concretely

[item-ideal.md](../item-ideal.md) §6.2 lists Unique as a **rarity row**, between Rare and Set. Three
lanes inherited the framing and two of them contradicted it:

| Doc | Says | Verdict |
|---|---|---|
| [item-ideal.md](../item-ideal.md) §6.2 | Unique is a rarity: *"hand-authored, named, fixed identity"* alongside Normal / Magic / Rare | **The thing this lane corrects.** The right-hand column of that row is exactly right; the left-hand column is in the wrong table |
| [ssot-rarity.md](ssot-rarity.md) §3.6 | *"Unique should be a rung — No. A unique is a container with a fixed core and `pool_rolls = 0`; it carries a rung like anything else… The flag itself is I3's"* | **Correct.** This lane confirms it from the other side and supplies the mechanism I1 pointed at |
| [ssot-item-categories.md](ssot-item-categories.md):201 | *"`unique` and `set` are rarities (I1)"* | **Wrong in both halves.** I1 says neither is a rarity. Uniques are not a category either — they are `equipment`. Correction for R4 |
| [ssot-generation.md](ssot-generation.md):409 | Illustrative drop ladder with row `R7 \| unique \| 300 (0.3%) \| pool_rolls 1` | **Wrong, and I12's own §3.2 already knows it.** Their pipeline argues *"a unique, a set piece, and a boss-signature drop are all defined on a base type"* and puts base-type selection at step 6 **before** rarity at step 7 (`ssot-generation.md:89-98`). The R7 row is a leftover from the ideal's §6.2 framing and contradicts the pipeline above it |
| [ssot-affixes.md](ssot-affixes.md):147, :783 | Unique as a row in a rarity table, *"the grammar is bypassed entirely"* | The bypass is right; the row placement inherits §6.2. Corrected here |

Nobody actually built anything on "unique is a rung". The correction is a documentation fix plus one
column, not a redesign.

### 3.4 So what is a unique, in the schema

Three sentences.

1. A unique is an ordinary **`effect_container` with `container_kind = 'item'`** — SC3's reserved value,
   no new kind, no new prefix. It occupies one `item_base_type` row like every other equipment template
   (I3's), so the frame filter, the role gate, the requirement gate, the socket capacity and the bind
   path all work on it with **zero special-casing**.
2. Its **identity** is one to three atoms in `effect_container_atom`, authored, possibly out of band,
   possibly of a kind no affix pool offers.
3. Its **variance** is at most one `pool_rolls` from a small authored pool, so two copies differ without
   either stopping being the item.

Everything else in this document is discipline on top of those three facts.

### 3.5 THE RULE-BREAKING LADDER — what a unique may do that a rare may not

Ranked by what each costs the architecture. The line is drawn between rung 5 and rung 6.

| # | The break | Costs | Verdict |
|---|---|---|---|
| **1** | **Magnitudes outside the tier band.** A core atom whose `overrides_json` sets `{Min: 120, Max: 138}` on a family whose t5 tops out at 96 | **Nothing.** `ValidateOverrides` never band-checks (`ContainerValidator.cs:167-171`), and the tier window is pool-only (`:87-96`) | **Allowed**, capped at **1.5× the top of the family's t5 band** and inside the AE budget (§3.7) |
| **2** | **Affix families illegal for the role.** The pool is filtered by `item_base_type.affix_pool_tag` (I3 §5.2); the fixed core has no tag | **Nothing.** The core is not drawn, so no tag applies | **Allowed** — this is what makes a unique a *choice* rather than a better roll |
| **3** | **More lines than the count band allows.** `pool_rolls` bounds the drawn half; the core is unbounded in shipped code | **Nothing** | **Allowed**, capped at **4 core atoms** including the inherited base stat — a readability cap, not a schema one (§3.6) |
| **4** | **Requirement clauses a rolled item may not carry.** I11 confines faction clauses to hand-authored uniques and set pieces (`ssot-requirements.md:124-128`) | **Nothing** — it is `req_json`, opaque to everyone but I11 | **Allowed.** It is a right I11 already granted this class; §9.9 confirms it |
| **5** | **Atoms from kinds no affix pool uses.** All 12 kinds have families ([atom-family-library.md](../effect-atom/atom-family-library.md) §3.5), but the board kinds — `board.action`, `grid.spawn`, `grid.clear`, `box.set`, `spawn.entity` — are terrible **random** rolls and good **authored** ones | **Nothing new.** Every one is a shipped opcode with a live lawn consumer (atom-catalog-ssot §2, rows 8–12) | **Allowed, and this is the point.** See below |
| **6** | **A genuinely new mechanic** — conversion, replacement, "your X becomes Y", a 13th kind, a new predicate leaf, a new trigger | An executor, an SC2 amendment, a re-audit of the runtime matrix, and a power model that can price a verb with no magnitude | **Refused.** §4.3 states the ask properly rather than smuggling it |

**Rung 5 is where the value is, and it costs nothing.** The interesting half of the genre's uniques is
not "bigger numbers" and not "a new verb" — it is **verbs the generator is not allowed to hand out at
random**. This codebase has five of them shipped and LIVE-proven (`grid.spawn`/`grid.clear` F42–F43 and
F48–F49, `box.set` F45–F47 and F50 — atom-family-library §3.5), and no rolled affix will ever use them,
because a random item that paints a tile Lava is a bug report. Authored, it is a build.

**The line, in one sentence:**

> **A unique may break every rule that lives in the generator, and no rule that lives in the machine.**

Generator rules — the pool tag, the tier window, the count band, the prefix/suffix quota, the
one-per-group exclusion, the naming grammar, the frame-filtered family list where the filter is taste.
Machine rules — the 12 kinds, the 5 attach points, the 7 triggers (5 authorable), the closed predicate
leaf list (11 ids in `PredicateNode.cs:7-21`), the bind gate's scope and runtime checks, SC4's units,
SC5's determinism, SC6's reject-never-ignore.

**Two frame-filter carve-outs, because "taste" and "physics" are different.** I8's frame filter stops
`+move speed` rolling on a turnip (item-ideal §5.4). A unique may bypass it where the reason is flavour;
it may **not** bypass it where the reason is that the Unity field does not exist. `plating` and
`carapace` write `arm1`/`arm2`, which are zombie-only fields (atom-family-library §4.1) — a plant unique
carrying either is not daring, it is dead. Rejection: `ParamNotHonoured`, the code that already means
*"the executor drops it for this configuration"* (`AtomRejection.cs:24`).

### 3.6 The authoring shape

| Part | Count | Table | Rolls? |
|---|---|---|---|
| Inherited base stat | 1 | `effect_container_atom` seq 0, family `atom.base-*` (I3 §5.2) | no |
| **Identity atoms** | **1–3** | `effect_container_atom` seq 1..3 | **value only**, `OnInstantiate`, spread ≤ ±15% of midpoint |
| **Variance slot** | **0 or 1** | `effect_container_pool`, 3–6 rows, one authored tier | yes — which atom, and its value |
| Total core | ≤ 4 | | |

**Does a unique roll anything? Yes, twice over, and neither roll may move its identity.**

- **Identity atoms roll their value.** This is D2's model — Windforce rolls 250–350% enhanced damage
  and is Windforce at both ends (*recalled, unverified*). Shipped code already supports it: the core
  loop calls `Freeze(atom, entry.OverridesJson, rollSeed, entry.Seq, …)` exactly as the drawn loop does
  (`Instantiator.cs:87-93` vs `:99-107`), so an `OnInstantiate` spec in `overrides_json` freezes at drop
  like any affix. **±15% of midpoint** is the cap: wide enough that a good copy is worth hunting, narrow
  enough that a bad copy is still the item.
- **The variance slot rolls its identity.** At most one draw, from a pool authored *for this unique* —
  never the generic role pool. PoE's Watcher's Eye and D4's "unique with one random affix" are the
  shape (*recalled, unverified*).

**`pool_rolls` may never exceed 1 on a unique.** Two rolls reintroduce the rare's grind — hunt the item,
then hunt the good copy of the item, twice — on the one item whose promise was that *finding it* was the
event. One roll is a reason to keep a second copy; two is a second job.

**A unique's container sets `min_tier == max_tier`.** The variance pool is authored at exactly one tier,
which makes ilvl narrowing a no-op inside the item and a clean structural refusal outside it: if the
drop site's `tierCeiling` is below the unique's tier, the entry is filtered out of the table and the
unique **cannot drop there at all** (I1 §3.2's structural-refusal argument, applied). No under-fill, no
weakened copy, no special case in `Instantiator`.

### 3.7 THE MUTUAL-RELEVANCE MECHANISM

The two failure modes are symmetric: uniques so strong that rolled loot is filler, or so weak that they
are trophies. Intentions do not hold that line. Four devices do, and three of them are import-time
checks.

**Device 1 — counter-pressure is a validated column, not a design principle.**

Every unique declares `counter_pressure ∈ {drawback, conditional, narrow}`, and the declaration is
checked against the content:

| Value | Means | Import check |
|---|---|---|
| `drawback` | the item costs you something | at least one core atom with a **negative** magnitude on a channel the actor wants positive. Sign carries meaning per kind (definitions §2) |
| `conditional` | the capability only fires in a state | at least one core atom carrying a non-empty `when_json` predicate tree |
| `narrow` | it is deliberately a worse stat stick | summed raw-stat AE ≤ **60%** of the rung's rolled baseline (§7.4) |

A unique satisfying none is refused (`UniqueNoCounterPressure`). This is the same device I5 used to make
the Diablo 3 set failure *literally unauthorable* (`ssot-sets.md`, `SetTierForbiddenAtom`) — a rule the
importer enforces rather than a rule a reviewer remembers.

**Device 2 — the premium is capped, and it buys capability, not magnitude.**

Denominated in **affix-equivalents (AE)** — I5's unit, one rolled affix at the middle of the relevant
tier window, owned by I8:

> A unique's total value ≤ **the rung's rolled baseline + 1.5 AE**.

1.5 AE is deliberately the *same* premium I5 gives a set per member piece (`ssot-sets.md` §3.5). Two
reasons: the two mechanisms are the same device and should cost the same, and a shared number is one
balance dial instead of two. Violation is `UniqueBudgetExceeded`. Per SC9 this is an **authoring rule in
AE today** and converts to an E9 power-vector budget when one exists.

**Device 3 — the parity invariant.**

The budget bounds the top. This bounds both ends:

> For any unique **U** at rung *n*, let `W` be the probability that a randomly rolled rare at rung *n*
> beats U on total magnitude **within one channel family** (I1 §3.5's single-family rule — SC4 forbids
> adding hp to resolver points).
>
> | | Required | Why |
> |---|---|---|
> | `W` | **≥ 25%** | below this the unique is strictly better on the stat sheet and rolled loot in that role is filler |
> | `W` | **≤ 75%** | above this the unique loses to most rares before its capability is even counted, and it is a trophy |

**Stated, not measured.** I1 built a simulator for its own overlap invariant and reports numbers from
it; I did not re-run it and I am not going to claim its output. §9.1 asks I1 for the harness, and §10.3
puts the unmeasured invariant in front of the owner.

The asymmetry is deliberate and it is the whole balance philosophy: **the rare wins the stat sheet, the
unique wins the build.** A player who wants the biggest number keeps farming rares. A player who wants
the thing the number cannot buy equips the unique.

**Device 4 — the anti-convergence rules.** "One correct unique per slot" is the second failure mode and
it needs cross-row checks, not per-item ones:

| Rule | Refusal |
|---|---|
| At most **one** unique per `(role, rung band, power axis)`, where axis ∈ the five power categories (definitions §7) | `UniqueAxisCollision` |
| **No uniques on the two `jewel-minor` roles** in v1 | `UniqueRoleForbidden` — a duplicated role with the smallest budget (item-ideal §5.5) is doubled by construction, which is the fastest path to convergence |
| **At most 8 of the 15 roles** per frame carry a unique in v1 | authoring quota, checked as a count |
| **A unique is never a set member** | `UniqueSetMembership` (§3.8) |

The 8-of-15 quota is I5's device applied to this lane: I5 caps a set at 6 roles *"so at least 9 slots on
a pure frame are always rare or unique territory"* (`ssot-sets.md:167`). Uniques must leave room the same
way, and 8 + 6 > 15 means the two quotas must be read together — which is a real interaction, named in
§9.4.

### 3.8 The boundary with sets — drawn explicitly, because both are hand-authored

A set piece and a unique are the same *authoring technique* and different *content classes*. The cut is
**where the identity lives**:

| | Unique (G1) | Set piece (I5) |
|---|---|---|
| Identity is complete in | **one item** | the group; one piece is a fragment |
| Fixed atoms | 1–3, may be out of band, may be a board kind | 2, at a fixed tier, from a set-specific pool |
| Rolled | ≤ 1 | 2 |
| Where the premium is paid | the item's own capability | the `set`-kind threshold containers |
| Container kind | `item` | `item` for the piece, `set` for the bonus |

**A unique may not be a set member. Hard no**, for three reasons, and I5 supplied two of them:

1. **The premiums would stack.** Both are 1.5 AE for the same reason. A unique set piece is a piece paid
   for twice.
2. **I5's anti-jail rules cannot reach a unique's core.** The `More`-op ban, the AE cap and the
   no-both-weapons rule are enforced on `set`-kind containers (`ssot-sets.md` §3.5). A unique's identity
   atoms are in `effect_container_atom` on an `item` container, where none of those checks run.
3. **I5 already rejected the shape.** Their §3.9 lists *"Fixed like a unique — once you own the set,
   every drop in those roles is dead"* as a rejected alternative for how set pieces roll
   (`ssot-sets.md:287`). A unique set piece is that rejected option, arriving through this lane.

Enforcement: `item_set_member` may not reference a container that has an `item_unique` row.

---

## 4. Options considered, and the recommendation

### 4.1 Where uniques sit relative to the rarity ladder

The brief's question, and the one with a ten-rung ladder already standing behind it.

| Option | Shape | Verdict |
|---|---|---|
| **A — a rung at the top** (ordinal 110) | D2's model; what item-ideal §6.2, I3:201 and I12's R7 row all half-assume | **Rejected on three counts.** (i) I1's ladder is a monotone staircase in exactly two axes — count band and tier window (`ssot-rarity.md` §3.4) — and a unique has neither, so the eleventh step is not a step. (ii) I1's overlap invariant `U(n,k)` is measured over count × tier × magnitude; a fixed-core item has no such distribution, so the invariant is *undefined* at the new top and the ladder's one guarantee stops one rung short of its own summit. (iii) It makes every unique better than every Almanac **by label**, which is the strictly-better-tier failure written into the schema |
| **B — outside the ladder, no rung at all** | `effect_container.rarity` NULL for uniques | **Rejected on plumbing.** The rung is what I13 reads for colour/pips/`display_key`, I9 for salvage yield, I4 for socket count, I12 for drop weight (`ssot-rarity.md` §4.4's registry). A rung-less item special-cases every one of them. And I1 is adding the FK check the column never had (`UnknownRarity`), so NULL needs a carve-out in the check that exists to remove carve-outs |
| **C — an orthogonal flag; the item carries an ordinary rung** ✅ | the `item_unique` row plus whatever rung the author picks | **Recommended.** Costs one table and no ladder change. Confirms I1 §3.6 rather than reopening it |

**Which rungs may a unique carry? Ordinal ≥ 30 (`grafted`).** Not a taste call:

- Ordinal 10 `chaff` is defined as *"husks, clippings… salvage fodder"* with `pool_rolls = 0`
  (`ssot-rarity.md` §3.3). It has the same *shape* as a unique and the opposite *meaning*.
- Ordinal 20 `sprout` is defined as *"it works. That is all it does."*
- Ordinal 30 `grafted` — *"one graft took and held"* — is the first rung that claims a design decision
  was made. A unique is the presence of design; the two rungs below it are rungs whose whole meaning is
  its absence.

No upper floor and no upper ceiling. I1 is explicit that *"a rung-40 unique and a rung-90 unique are
both real content"* (§3.6) and this lane agrees: a low-rung unique is a **weak, weird, early** item, and
that is some of the best content in the genre (D2's Nagelring, *recalled, unverified*). The rung prices
it; it does not rank it.

**Which columns of the rung a unique actually reads.** This matters, because I1 warns that an unread
column is the `status.expose.*` defect (SC7):

| Rung column | Read by a unique? |
|---|---|
| `ordinal`, `color_hex`, `pip_count`, `display_key` | **yes** — display and sort |
| `salvage_yield`, `socket_min`/`socket_max`, `drop_weight_default`, `charm_potency` | **yes** — via I9 / I4 / I12 / I10 |
| `pool_rolls` / `pool_rolls_max` | **no** — the unique's own `pool_rolls` is 0 or 1 |
| `min_tier` / `max_tier` | **no** — the unique authors a single tier |
| `enhance_cap` | **yes**, scoped (§4.4) |
| `promote_from` | **forced to 0** regardless of the rung's value (§4.4) |

This is precisely I1 §4.3's *"one ladder; every category free to use a subset of the rungs"*, so no new
mechanism is needed to express it. It does need one new budget key, `unique_eligible` — §9.1.

### 4.2 Where the flag lives

| Option | Verdict |
|---|---|
| **Derive it** — `pool_rolls ≤ 1 AND core atom count > 1` | **Rejected.** A `chaff` normal is also `pool_rolls = 0`, and a fully-crafted fixed item would masquerade as a unique and inherit its budget exemptions. A content class inferred from a shape is a class anyone can forge |
| **A boolean on `item_base_type`** | **Rejected as insufficient.** It answers "is it one" and nothing else — no home for `counter_pressure`, `acquisition`, `budget_ae`, the display parent, or the flavour key. Widening I3's table for five unique-only columns is the wide-column mistake I1 rejected for `rarity` (§4.4) |
| **A separate `item_unique` table, 1:1 on the container** ✅ | **Recommended.** The container and the `item_base_type` row stay ordinary — so bind, frame filter, role gate, requirement gate and socket capacity need no branch — and every unique-only column lands in one place that is trivially enumerable for the cross-row checks in §3.7 |

**A unique still occupies an `item_base_type` row.** That is the load-bearing choice and it is worth
saying why: it means a unique is **not a special case anywhere except the two places it must be** — the
pool it does not draw from, and the acquisition path. Everything else in the item stack sees an ordinary
equipment container.

### 4.3 The SC2 collision, head on

SC2 closes the vocabulary at 12 kinds, 5 attach points, 7 triggers. The brief's question is how much of
"changes how something WORKS" survives that. The honest answer is **most of the arena and none of the
pipeline**, and the limitation is real.

#### Three that ARE expressible today

Each uses a different attach point, and each changes a rule rather than a number.

1. **State — the item that inverts a losing position.**
   `shield.grant` on `OnDamageTaken` with predicate `hpBelowMilli, subject: self, value: 300`, plus
   `status.clear` on the same trigger sharing an `icd_key` so the pair is one clock (definitions §14.1).
   Being nearly dead stops being a countdown and becomes a reset button you can build around. Nothing
   here is a bigger number; the *shape of a fight* changed. Attach points: shield + status. Predicate
   leaves: `HpBelowMilli` — shipped (`PredicateNode.cs:13`).

2. **Arena — the item that edits the board.**
   `box.set` on `OnDamageDealt`, gated on the damaged entity's HP, painting the tile Lava. No stat can
   do this; no rolled affix ever should. The lawn opcode is shipped and LIVE-proven (FA8, probes
   F45–F47, F50 — atom-family-library §3.5), and the `OnDamageDealt` filter inversion works *in our
   favour* here: on that trigger `filters.side`/`typeId` refer to the **damaged** entity
   (atom-catalog-ssot §3), which is exactly the tile we want. Attach point: board.

3. **Bodies — the item that changes the unit count.**
   `spawn.entity` on `OnDeath`, `count: 1` (`min: 1` per the D3 fix, definitions §7), spawning a
   replacement. Your death becomes a transition instead of a loss. Priced honestly — definitions §7
   prices the spawned body from `hp`/`maxHp`/`atk` plus its own atoms — so it cannot slip the budget the
   way it did before D3 was closed. Attach point: board.

State, arena, bodies. Three different attach points, twelve kinds, zero additions.

#### Two that are NOT, and what they cost

1. **Conversion and replacement.** *"Your fire damage becomes ice." "You cannot be healed; you gain
   shield instead." "Your attacks chain to two extra targets."* The 12 kinds have no **transform** verb.
   `stat.modify`'s ops are `Flat · Increased · More`; `Override` exists in the stat system and *"effects
   cannot emit it — a deliberate constraint, not an oversight"* (atom-catalog-ssot §4.1). `stat.derived`
   has `Replace`, but replacing a derived channel's value is not converting a damage type. And a
   conversion is a rule about the damage pipeline, which atom-catalog-ssot §2 assigns explicitly to the
   *consumer/applier spec* under **"Not kinds, on purpose"**.

   **Cost:** a thirteenth kind, plus an executor inside a damage applier that does not exist, plus a
   spec with no owner, plus a power model extension — E9 prices magnitudes, and a conversion has none.
   **This is a named SC2 request, not an assumption, and nothing in this lane depends on it:**

   | Request | Reason | Blocked on |
   |---|---|---|
   | `damage.convert` (working name) — a 13th kind that re-routes a damage packet's element or channel | It is the single largest class of interesting unique the closed vocabulary cannot express, and it is the reason two of my three "not expressible" examples exist | The damage consumer/applier spec, which has no owner today. **Do not add the kind before the consumer** — that is the `status.expose.*` and `stat.derived` mistake for the third time |

2. **Cost, timing and behaviour changes.** *"Your skill costs no stamina." "This weapon fires twice."
   "Cooldowns do not apply to you." "+1 range."* These are the **action layer's**, and the container
   schema refuses them by design: *"this schema holds what a skill contains, never when it fires"*
   (`spec-container-schema.md:98`). The item-side seam is `grants_action_id`, which is **G4's lane and
   does not exist yet** (`reconciliation-plan.md` §R3).

   **Cost:** not a kind at all — the action layer must land first, and then this is a column or a
   container kind, not a vocabulary change. **Deferred, not refused.** The honest statement is that a
   large share of the genre's most-loved uniques — Headhunter, Mjölner, the whole "your ability behaves
   differently" class (*recalled, unverified*) — are **action-layer items**, and this lane cannot author
   a single one of them until G4 and the action program land.

#### The limitation that bites harder than SC2 in wave 1

Not the kind list — the **D6 quarantine**. `stat.derived` is `None/None/None` (atom-catalog-ssot §2,
row 2), which means every `combat.*` channel — crit rate, crit damage, all elemental power and defence,
the entire shield stat stack, accuracy, dodge — **binds nowhere** until E12 ships
`BattleStatComposer`'s reader. Half the design space a unique would naturally reach for is unauthorable
today, and it will not announce itself: the atom loads, the container validates, the bind gate rejects
with `RuntimeUnsupported`.

I8 already closed the timing hole — for `container_kind = 'item'` the runtime-support check is
**promoted from bind time to import time**, because *"a drop that cannot be equipped is worse than no
drop"* (`ssot-affixes.md:660-665`). This lane adopts that rule verbatim and applies it to the fixed
core as well as the pool.

So the practical wave-1 palette for a unique is: `stat.modify` (primary channels),
`resource.delta`, `resource.economy`, `status.apply`, `status.clear`, `shield.grant`, `spawn.entity`,
`board.action`, `grid.spawn`, `grid.clear`, `box.set` — eleven of twelve kinds, all lawn-live. That is
not a small palette. It is just not the one a Diablo player would reach for first.

### 4.4 Participation — sockets, enhancement, reroll, sets

| Mechanic | Verdict | Reason |
|---|---|---|
| **Sockets (I4)** | **Yes — fixed, never rolled** | Sockets are a *player* choice layered on an *author* choice, which is exactly the pairing that keeps a fixed item alive across a build's whole life. The count comes from `item_base_type.socket_capacity` (I3 §5.2), inherited from the parent base type. It may **not** come from a rolled per-rung count: a rolled socket count is a fourth variance (I1 flags it as moving every number in its §3.5), and a unique has exactly one authored variance by design |
| **Enhancement (I6)** | **Yes — magnitude-only** | Enhancement scales existing values without changing identity, which is compatible by construction. But it may only touch `stat.modify` and `resource.delta` **magnitudes** in the core. It may not touch an atom carrying a board kind or a predicate: "20% more Lava" is not a number, and scaling a `chance` or an `icd_ms` moves a capability's *shape*. Column: `enhance_scope = 'magnitude-only'`. The ceiling is still I6's `enhance_cap` per rung |
| **Reroll — identity (I7)** | **No** | I7 already decided it and named this class in the decision: the fixed core, *"implicits, unique-item identity"*, is **never** rerollable (`ssot-reroll.md:74`), under the rule *"you may only redraw what the pool drew, and you must redraw it from the same pool."* Identity atoms were never drawn. Confirmed, not reopened |
| **Reroll — the variance slot (I7)** | **Yes** | It *was* drawn from a pool, so I7's rule admits it with no amendment. One roll, one group, same tier |
| **Reroll — an identity atom's *value* (I7 + I6)** | **Yes, and it is a new operation** | See below |
| **Sets (I5)** | **No** | §3.8 |
| **Promotion (I1 §3.7 / I6)** | **No** | Promotion *"only adds"* affixes drawn in the new rung's window, which would push a unique past `pool_rolls ≤ 1`. `promote_from` is forced to 0 for a unique regardless of what its rung's budget key says |
| **Salvage (I9)** | **Open — recommended no** | §10.6 |

**The reroll case the brief flags is real, and I7's rule does not reach it.**

I7's rule keys on *where an atom came from*, not on *whether its value rolled*. A unique's identity atom
is a fixed-core atom whose **value** rolled inside an authored `OnInstantiate` band (§3.6). So "my Kiln
Nozzle rolled 121 of a possible 120–138, can I re-roll the number" is a question I7's rule answers
**no** by accident rather than by decision.

Answering `no` is the trophy failure: a unique with a bad roll is dead, and the item whose whole promise
was *finding it is the event* becomes an item you must find four times.

Answering `yes` naively is the destroy-the-identity failure the brief warns about — which is why the
operation must be **value-only**:

> **`unique_value_reroll`** — re-draw the `OnInstantiate` value of **one** identity atom, from **its own
> authored band**, on the **same atom id**. Same family, same variant, same tier, same channel, same
> kind, same predicate, same trigger. Only the number moves, and only inside the band the author wrote.

Under that shape it is structurally impossible for a reroll to produce an item the author did not
author — the same property I7's pool rule buys for rares, arrived at the same way. It is I7's operation
to add and I6's op to log; §9.6 and §9.7 make the request.

### 4.5 Drop and acquisition

I1 left a door open at the top of its ladder: *"Rung 100 must have at least one deterministic source. An
unreachable top rung is a frustration, not a fantasy. If it is not pity-guaranteed it must be quest- or
boss-guaranteed. I12 owns which"* (`ssot-rarity.md` §3.8). Uniques are the natural tenant, and one of
the four channels **already exists in I12's spec**.

| Channel | Verdict | Mechanism |
|---|---|---|
| **Random drop from the general table** | Yes, low weight | Selected at I12's **step 6** (base type), not step 7 (rarity) — which is what their own §3.2 argues for. No new machinery; a drop-table entry naming a unique's container is the same entry shape as one naming a base type |
| **Source-locked** (boss, sector, expedition tier) | **Yes — the primary channel** | Same entry shape, restricted to one source id. This is where identity comes from: an item that is *from* somewhere is a story before it is a stat block |
| **Quest / first clear** | **Yes, and it is already built** | I12 ships *"first clear of any content id grants one fixed, authored item — a container with `pool_rolls = 0`, no rolls at all, so it never disappoints… recorded per `(player_id, source_kind, source_id)` in `item_first_clear`"* (`ssot-generation.md` §3.5). That is a unique, described precisely, by a lane that did not know it was describing one. The seam needs naming, not building |
| **Crafted from a recipe** | **Yes — the deterministic top-rung answer** | A `blueprint` (I3's category) plus materials (I9) mints one specific unique. Deterministic acquisition bounded by a material cost, which is the sink I9 wants and the guarantee I1 asked for |

Two rules:

1. **Every unique at ordinal ≥ 90 must be `source-locked` or `deterministic`, never plain `drop`.**
   Refusal: `UniqueUnreachable`. At the drop weights I12 sketches, a plain-drop top unique is content
   nobody sees, and I1 already ruled that unacceptable.
2. **No unique pity.** I12 decided this and gave the design reason — *"a guaranteed unique means every
   player converges on the same handful in the same week"* (`ssot-generation.md` §3.4). This lane adds
   the structural one: **pity keys on rung** (I1 §3.8, *"pity may key on rung only"*), and a unique's
   rung is ordinary, so a unique pity would need a counter keyed on a **content class** — a new counter
   axis, for a guarantee the deterministic craft path already provides better.

### 4.6 How many, and what one costs

Per-unique row cost. The variable is **how much rule-breaking the item does**, which closes the loop
with §3.5:

| Rows | When |
|---|---|
| 1 `effect_container` | always |
| 1 `item_base_type` (I3) | always |
| 1 `item_unique` | always |
| 2–4 `effect_container_atom` | base stat + 1–3 identity atoms |
| 0–6 `effect_container_pool` | only if it has a variance slot |
| 0–3 **private** `effect_atom` | only for identity atoms outside the shared bands — see below |
| **6–17 rows, zero code** | |

**Why some identity atoms must be private rows.** Content is disabled, never deleted, and *"atom
disabled beneath a live instance → the instance keeps its frozen values; new binds reject with
`StaleInstance`"* (definitions §6). So if a unique's identity is a shared `atom.vitality.t5` row with an
out-of-band override, a later balance pass that retires or re-tiers `vitality.t5` **bricks every dropped
copy of that unique at the next bind**, silently, with a code that blames the instance.

Rule: **an identity atom whose magnitude is outside the shared band must be a private atom row**, family
`atom.unique-{slug}`, owned by this unique alone. In-band identity atoms reuse shared rows and cost
nothing. So the cheapest uniques (§7.2's Understudy's Pot) cost 7 rows and the most rule-breaking
(§7.1's Kiln Nozzle) cost 14 — the row cost *is* the rule-breaking budget, made visible.

**v1 count: 20 uniques.**

| Rung band | Count | Spread |
|---|---|---|
| 30–40 (`grafted`, `cultivated`) | 5 | early, weak, weird — the "you can find this in week one" tier |
| 50–60 (`fused`, `chimeric`) | 5 | |
| 70–80 (`heirloom`, `firstseed`) | 5 | |
| 90–100 (`sunwoven`, `almanac`) | 5 | all `source-locked` or `deterministic` (§4.5) |

Frame split: **8 humanoid, 8 plant, 4 `either`** (I3's frame value for OD3 hybrids). Roles: at most 8 of
15 per frame, none on `jewel-minor` (§3.7). Axis spread enforced by `UniqueAxisCollision`.

At 6–17 rows each that is roughly **200 rows and zero lines of code**. Twenty is chosen against the
prior art the ideal cites: D2 LoD shipped on the order of 350 uniques and PoE well over a thousand
(*both recalled, unverified*) — but those are the totals of a decade, and the number that matters for
v1 is *how many roles have at least one interesting alternative to a rare*, which 20 answers for
sixteen of thirty role-frame pairs.

**The claim to test, stated so it can fail:** *once the first three exist, one unique should cost one
authoring session and no code change.* If unique number four needs a C# edit, this design failed and
§8.3 says why that matters.

---

## 5. Data shape

### 5.1 Reused as-is — no schema change

| Column | Table | What a unique puts in it |
|---|---|---|
| `container_id` | `effect_container` | `item.{slug}` — SC3's reserved prefix, no new `container_kind` |
| `container_kind` | `effect_container` | `item`. **This lane adds no reserved value** |
| `slot` | `effect_container` | the frame-neutral role id (I3 §5.2) |
| `rarity` | `effect_container` | an ordinary rung, ordinal ≥ 30 (§4.1) |
| `min_tier` / `max_tier` | `effect_container` | **equal**, and only for the variance pool (§3.6) |
| `pool_rolls` | `effect_container` | **0 or 1**, never more |
| `level_req` | `effect_container` | enforced at bind, `LevelTooLow` |
| `effect_container_atom.overrides_json` | | the identity atoms' value specs, including out-of-band ones — validated for well-formedness only (`ContainerValidator.cs:167-171`) |
| `effect_container_pool` | | 3–6 rows at one tier for the variance slot |
| `roll_seed`, `catalog_revision` | `effect_instance` | unchanged; SC5 holds with no amendment |
| `item_base_type.*` (I3) | | frame, class, band, socket capacity, pool tag, `req_json` |

**Nothing above is a new column, a new kind, or a new reserved value.** The identity half of a unique is
expressible in the shipped schema today; what this lane adds is the *class*, its budget, and its rules.

### 5.2 New — `item_unique`, keyed 1:1 on the container

| Column | Type | Notes | Consumer (SC7) |
|---|---|---|---|
| `container_id` | TEXT PK, FK → `effect_container` | must have `container_kind = 'item'` and an `item_base_type` row | the validator; every check in §6 |
| `derived_from` | TEXT NOT NULL, FK → `item_base_type` | the parent base type, **for display and inheritance of class/frame flavour** — "Kiln Nozzle — Pea Nozzle" | G3's tooltip; the salvage/compare UI (I13) |
| `counter_pressure` | TEXT NOT NULL | `drawback` \| `conditional` \| `narrow` — checked against content (§3.7) | the import validator |
| `budget_ae` | INTEGER NOT NULL | the author's declared total in **AE × 100** (integer; SC4 forbids floats in content) | the budget check; E9 replaces it with a power read when one exists |
| `power_axis` | TEXT NOT NULL | one of the five power categories (definitions §7) — the axis the item *is about* | `UniqueAxisCollision` |
| `acquisition` | TEXT NOT NULL | `drop` \| `source-locked` \| `deterministic` (§4.5) | I12's table builder; the ≥ 90 check |
| `enhance_scope` | TEXT NOT NULL DEFAULT `'magnitude-only'` | §4.4 | I6 |
| `flavour_key` | TEXT NULL | localisation key for flavour text. **Not a literal** — same rule I1 applied to `display_key` | G3 |
| `enabled`, `revision` | INT | joins the E8 content hash | the importer |

Nine columns, one table, no widening of anyone else's row. `item_unique` **must join the covered-hash
registry**, which is an explicit `contentHashSchemaVersion` bump — the thing definitions §8 says must
never happen silently.

### 5.3 New — one budget key on I1's registry

| Key | Value | Read by | Proposed by |
|---|---|---|---|
| `unique_eligible` | 0/1 — may a unique carry this rung | this lane's validator | **G1**, registered by **I1** |

Set to 0 at ordinals 10 and 20, 1 at 30–100 (§4.1). This mirrors I5's `set_eligible` slot exactly, which
is why it is a key in `rarity_budget` and not a column on `rarity` (I1 §4.4's argument).

### 5.4 What this lane does **not** add

Worth listing, because the temptation was there for each:

- **No `container_kind`.** SC3 reserves four values for I3/I4/I5/I10; this lane needs none of them.
- **No atom kind, trigger, or predicate leaf.** §4.3's request is written as a request and depended on
  by nothing.
- **No instance column.** A unique instance is an ordinary `effect_instance`; the class lives on the
  template, which is correct — two copies of the same unique are the same content class and different
  items.
- **No second effect mechanism** (SC1). Identity atoms, the variance slot, and the item's implicit all
  reach the actor through container → instance → binding → the actor's effect list, unchanged.
- **No rarity rung** (§4.1).

---

## 6. Validation and reason codes

### 6.1 Existing codes a unique hits, unchanged

These need no new machinery — a unique is an ordinary container and the shipped checks already cover it.

| Bad input | Reason code | Where |
|---|---|---|
| Identity atom references an unknown atom id | `UnknownAtom` | `ContainerValidator.cs:50-52` |
| `overrides_json` names a param the kind does not declare | `UnknownParam` | `:160-165` |
| `overrides_json` tries to change `kind_id` | `OverrideChangesKind` | `:156-158` |
| `overrides_json` value spec malformed (`Min > Max`, bad policy) | `BadValueSpec` | `:168-171` → `AtomJson.TryReadValueSpec` |
| Out-of-band magnitude leaves the channel's integer range | `MagnitudeOverflow` | E2 |
| Same atom in the fixed core and the variance pool | `DuplicateAtomInContainer` | `ContainerValidator.cs:83-85` |
| Variance pool row outside `[min_tier, max_tier]` | `TierOutOfWindow` | `:87-93` |
| Every variance pool row at `weight = 0` | `UnsatisfiablePool` | `:100-102` |
| `pool_rolls = 1` with no drawable group | `PoolRollsExceedGroups` | `:104-105` |
| Duplicate `seq` in the core | `DuplicateSeq` | `:46-47` |
| Identity atom of a kind whose target runtime is `None` (any `stat.derived` atom today, D6) | `RuntimeUnsupported` | **promoted to import time** for `container_kind = 'item'`, adopting I8's rule (`ssot-affixes.md:660-665`) |
| `warding` / `resilience` as an identity atom | `ScopeUnsupported` | G8 — `stat.modify` on `defense` is `match`-scope only (definitions §6) |
| A plant unique carrying `plating` / `carapace` | `ParamNotHonoured` | the Unity field does not exist for that side (§3.5) |
| Identity atom authoring `OnGranted` / `OnRemoved` | `TriggerNotAllowed` | definitions §14.2 — lifecycle is not content |
| `status.apply` identity atom with an empty target | `AmbiguousTarget` | G5 — "all" must be explicit |
| An identity atom disabled under a live copy | `StaleInstance` | at bind. §4.6's private-atom rule exists to make this rare |
| `level_req` above the wearer | `LevelTooLow` | the bind gate |

### 6.2 New codes this lane proposes — six

| Bad input | Reason code | Check |
|---|---|---|
| A unique declaring no counter-pressure, or declaring one its content does not satisfy | **`UniqueNoCounterPressure`** | `drawback` ⇒ a negative-magnitude core atom exists; `conditional` ⇒ a core atom has a non-empty predicate; `narrow` ⇒ raw-stat AE ≤ 60% of the rung baseline |
| `budget_ae` above the rung's rolled baseline + 1.5 AE, **or** the summed content disagreeing with the declared `budget_ae` by more than ±25% | **`UniqueBudgetExceeded`** | ±25% is definitions §7's drift tolerance, reused rather than reinvented |
| Two uniques sharing `(role, rung band, power_axis)`; or a unique on a `jewel-minor` role | **`UniqueAxisCollision`** / **`UniqueRoleForbidden`** | cross-row, at import. `UniqueRoleForbidden` also carries the 8-of-15 quota overflow |
| A unique at a rung whose `unique_eligible` is 0, or ordinal < 30 | **`UniqueRungIneligible`** | reads I1's budget key |
| A container with an `item_unique` row referenced by `item_set_member` | **`UniqueSetMembership`** | cross-table (§3.8) |
| `acquisition = 'drop'` at ordinal ≥ 90 | **`UniqueUnreachable`** | §4.5 |
| `pool_rolls > 1`, `min_tier ≠ max_tier`, more than 3 identity atoms, or an `OnInstantiate` spread wider than ±15% of midpoint | **`UniqueShapeInvalid`** | the §3.6 shape rules, one code with a detail string |

That is **seven names across six lines** — and it is too many.

### 6.3 The reason-code inflation problem, stated rather than ignored

definitions §10 fixes the list at **33** and calls adding one *"a reviewed change; they are the
operator-facing error surface."* This lane wants six or seven. I1 wants `UnknownRarity`. I5 wants
`SetThresholdUnreachable`, `SetTierForbiddenAtom`, `SetRoleForbidden`. I3 wants `CategoryHasNoConsumer`,
`UnknownBaseTypeSet`. Across thirteen lanes plus four gap lanes the closed list is on course to double,
and a list that doubles is not closed.

**Two ways out, and the owner should pick one at R4:**

| Option | Shape |
|---|---|
| **A — take the codes** | 33 → ~50. Each is precise and lookupable; the list stops being the small, memorable surface definitions §10 wanted |
| **B — one code plus a rule id** ✅ *(this lane's recommendation)* | A single `ContentRuleViolated` code carrying `rule = "unique.counter-pressure"`, `rule = "unique.budget"`, and so on. The operator surface stays at 34, rule ids are namespaced per lane so no two lanes collide, and the detail string a rejection already carries (`AtomRejection.Detail`) is where the specificity lives. Each lane then owns its own rule namespace instead of a slice of a shared enum |

This lane's checks are written above under option A names so they read clearly; under option B every one
becomes `ContentRuleViolated` with `rule = "unique.<slug>"`. **Either works; what does not work is
thirteen lanes each adding four codes and nobody counting.** §10.2 puts it to the owner.

### 6.4 Where each check runs

| Phase | Checks |
|---|---|
| **Import** (E14, all-or-nothing) | every §6.2 check, plus the runtime-support promotion, plus every cross-row check (`UniqueAxisCollision`, `UniqueSetMembership`, the role quota). Cross-row checks *must* be import-phase: they are properties of the catalog, not of a row |
| **Load** (E4/E5, per-row) | the §6.1 container checks, unchanged |
| **Instantiate** | nothing new. `Instantiator` needs no unique branch |
| **Bind** (E6) | the §6.1 bind-time rows, unchanged |

**`Instantiator` needs no unique branch** is the sentence to hold this design to. If it grows one, the
class stopped being data.

---

## 7. Worked examples with real numbers

Units per SC4: hp and atk in **game units**, chances in **integer per-mille**, durations in **ms**
(except `status.apply.duration`, which FA2 reads as **float seconds** — definitions §13/D7). Tier bands
are I1's illustrative `vitality` ladder (t1 10–12 · t2 20–25 · t3 40–50 · t4 85–100 · t5 170–205 hp),
anchored on the two real numbers in atom-family-library §2a. **Everything numeric here is illustrative,
not balanced.**

### 7.1 Kiln Nozzle — plant, `armament-primary` (`muzzle`), rung 70 `heirloom`, band 3

The maximally rule-breaking example: out-of-band magnitude, a board kind, a drawback, and a variance
slot.

| Part | Row | Detail |
|---|---|---|
| Container | `item.kiln-nozzle` | `container_kind = item`, `slot = armament-primary`, `rarity = heirloom`, `min_tier = max_tier = 3`, `pool_rolls = 1`, `level_req = 22` |
| Base type | `item_base_type` | `frame = plant`, `class_id = nozzle`, `band = 3`, `socket_capacity = 2`, `affix_pool_tag = weapon-nozzle` |
| Unique row | `item_unique` | `derived_from = pea-nozzle`, `counter_pressure = drawback`, `power_axis = offense`, `acquisition = source-locked`, `budget_ae = 393` |
| Core seq 0 | `atom.base-damage.nozzle.t3` | **shared row**, inherited. `atk` Flat, +34 atk |
| Core seq 1 — identity | `atom.unique-kiln-heat.t1` **private** | `stat.modify`, `atk` Flat, `{Min: 120, Max: 138, OnInstantiate}` game units |
| Core seq 2 — identity | `atom.unique-kiln-tile.t1` **private** | `box.set`, `boxType: Lava` (Int per D7), `when.trigger = OnDamageDealt`, predicate `{leaf: hpBelowMilli, subject: target, value: 300}`, `chance: 1000‰`, `icd_ms: 4000` |
| Core seq 3 — drawback | `atom.unique-kiln-brittle.t1` **private** | `stat.modify`, `maxHp` Flat, `{Min: -60, Max: -60, Fixed}` — negative is a reduction; sign is per-kind (definitions §2) |
| Pool ×4 | `atom.searing-strike.{fire,ice,earth,dark}.t3` | **shared rows**, weights 40/20/20/20, `{Min: 100, Max: 200, OnApply}` |

**14 rows** (1 + 1 + 1 + 4 core + 4 pool + 3 private atoms), zero code.

Rule-breaking check against §3.5: rung 1 (120–138 atk against a `might` t5 top of ~96 → **1.44×**, under
the 1.5× cap); rung 3 (four core atoms, at the cap); rung 5 (`box.set` — a kind no rolled affix uses).
Rung 6 untouched. Counter-pressure `drawback` is satisfied by seq 3, structurally.

**Runtime honesty:** `stat.modify` on `atk`, `box.set` and `resource.delta` are all **lawn ✅** and all
**battle ✖** (atom-catalog-ssot §2, rows 1, 3, 12). Kiln Nozzle equips and works on the lawn today and
rejects `RuntimeUnsupported` in battle until E12 and the battle enrichment land. That is not a defect in
the item; it is the shipped matrix, and §9.14 names it.

**One honest gap:** `box.set` on `OnDamageDealt` needs a **cell**, and where that cell comes from on an
event-sourced board atom is not specified anywhere — G2 says the executor handles one cell and G5 says
an empty target must be a rejection. The natural answer is "the damaged entity's cell", which the
`OnDamageDealt` filter inversion already points at (atom-catalog-ssot §3), but it is not written down.
**This is a dependency on the atom program, not a decision this lane may make** — §9.15.

### 7.2 Understudy's Pot — plant, `armament-secondary` (`thorn`), rung 50 `fused`, band 2

The cheap end: no variance slot, no private out-of-band atom except the capability, `narrow`
counter-pressure.

| Part | Row | Detail |
|---|---|---|
| Container | `item.understudys-pot` | `rarity = fused`, `pool_rolls = 0`, `min_tier`/`max_tier` NULL, `level_req = 12` |
| Base type | `item_base_type` | `frame = plant`, `class_id = thornguard`, `band = 2`, `socket_capacity = 1` |
| Unique row | `item_unique` | `derived_from = bramble-guard`, `counter_pressure = narrow`, `power_axis = survivability`, `acquisition = deterministic` |
| Core seq 0 | `atom.base-guard.thornguard.t2` | **shared**, `maxHp` Flat, +55 hp |
| Core seq 1 — identity | `atom.unique-understudy.t1` **private** | `spawn.entity`, family `gardener`, `when.trigger = OnDeath`, `count: 1`, spawned body `hp/maxHp: 240`, `atk: 18`, `chance: 1000‰` |
| Core seq 2 — identity | `atom.regeneration.t2` | **shared row, in band, zero new atoms** — `resource.delta`, `OnTimer` |

**7 rows** (1 + 1 + 1 + 3 core + 1 private atom).

`narrow` is satisfied arithmetically: a rung-50 `fused` rare draws 2–3 affixes in the t2–t4 window
(I1 §3.3), so its baseline is ≈ 2.5 AE. This item's raw stats are +55 hp (≈ 0.6 AE against a t2/t3
midpoint) plus a t2 regeneration (1.0 AE) = **1.6 AE = 64% of baseline** — which **fails** the ≤ 60%
threshold by four points. Two honest ways out: drop the base-guard share to +45 hp (0.5 AE → 60%), or
re-declare `counter_pressure = conditional` and put a predicate on the spawn (`hpBelowMilli` on the
killer, say). **Worth showing rather than quietly fixing:** the check has teeth, it fires on a
plausibly-authored item, and the author's response is a design decision, not a waiver.

The spawn prices honestly: definitions §7 (closing D3) prices a spawned body from `hp`/`maxHp`/`atk`
against the maxHp anchor plus its own atoms, and `count` has `min: 1`, so this cannot be the
`spawn.entity{hp: 5000}` budget hole the D3 defect described.

### 7.3 Brainpan Sigil — humanoid, `jewel-major` (`neck`), rung 90 `sunwoven`, band 4

Reused deliberately from I11, which already authored this row: *"`brainpan-sigil`, role `jewel-major`,
Unique · `humanoid`, `plant` · level 30 · `sap ≥ 28` · faction `zombie`"* (`ssot-requirements.md:609`).
Confirming rather than re-inventing is how two lanes stay consistent.

| Part | Row | Detail |
|---|---|---|
| Container | `item.brainpan-sigil` | `rarity = sunwoven`, `min_tier = max_tier = 4`, `pool_rolls = 1`, `level_req = 30` |
| Base type | `item_base_type` | `frame = either`, `class_id = seal`, `band = 4`, `socket_capacity = 0`, `req_json` = I11's `sap ≥ 28` + faction `zombie` |
| Unique row | `item_unique` | `counter_pressure = conditional`, `power_axis = control`, `acquisition = deterministic` (**forced** — ordinal ≥ 90, §4.5) |
| Core seq 0 | `atom.vitality.t4` | **shared, in band** — `{Min: 85, Max: 100, OnInstantiate}` hp |
| Core seq 1 — identity | `atom.unique-brainpan.t1` **private** | `status.apply`, `status: hypno`, `duration: 4` (**float seconds** — FA2's real unit, D7), `when.trigger = OnDamageDealt`, predicate `{op: and, children: [{leaf: sideIs, subject: target, value: zombie}, {leaf: hpBelowMilli, subject: target, value: 250}]}`, `chance: 250‰`, `icd_ms: 8000` |
| Pool ×3 | `atom.fortitude.t4` / `atom.might.t4` / `atom.mending.t4` | **shared**, weights 34/33/33 |

**10 rows** (1 + 1 + 1 + 2 core + 3 pool + 1 private atom). Depth 2, four nodes — well inside the
predicate limits (max depth 4, max 16 nodes, definitions §3). Both leaves carry `subject`, which is
required on **every** leaf with no default (definitions §3).

The faction clause is the §3.5 rung-4 break, and it is a right I11 already granted this class:
*"A faction clause is legal but content-restricted: allowed only on hand-authored uniques and set
pieces… On a hand-authored unique it is flavour with a known audience, which is what uniques are for"*
(`ssot-requirements.md:124-128`).

**Why `stalwart` is not in the variance pool.** The obvious pool for a control amulet is
`status.resist.*` — family `stalwart`, kind `stat.derived`, **quarantined `None/None/None`** (D6). It
would be rejected at import under §6.1's promoted runtime check. The pool is three `stat.modify`
families instead. This is §4.3's "harder than SC2" limitation biting a real item.

### 7.4 The budget arithmetic, worked

For Kiln Nozzle at rung 70 `heirloom` (count band 3–4, tier window t3–t5 — I1 §3.3):

```
rolled baseline   = 3.5 affixes × 1 AE at the window midpoint (t4)      = 3.50 AE
unique allowance  = baseline + 1.5 AE                                   = 5.00 AE
```

| Line | Reckoning | AE |
|---|---|---|
| `atom.base-damage.nozzle.t3` (+34 atk) | base stat, not an affix — excluded, per I3 §5.2's `seq 0` convention | — |
| Identity `+120–138 atk` | midpoint 129 against a `might` t4 midpoint of ~72.5 atk | **+1.78** |
| Identity `box.set → Lava` on a conditional trigger | a capability with no magnitude — **author-assigned**, and unverifiable until E9 | **+2.00** |
| Drawback `−60 maxHp` | against a `vitality` t4 midpoint of 92.5 hp | **−0.65** |
| Variance slot, `searing_strike` t3 | one rolled affix = 1.0 AE by definition, discounted for sitting below the window midpoint | **+0.80** |
| **Total** | | **3.93 AE ≤ 5.00** ✅ |

`budget_ae = 393` (AE × 100, integer — SC4 forbids floats in content).

**The soft spot, named:** the 2.00 AE for the Lava capability is a number an author wrote down, and
nothing can check it until E9 prices a verb with no magnitude. That is SC9's situation exactly — the
design ships without power, and the budget is an authoring discipline rather than an enforced ceiling
for the capability line specifically. The *magnitude* lines above are checkable today.

---

## 8. Failure modes

### 8.1 The strictly-better tier that obsoletes the generator

**How it goes wrong.** Diablo 3 at launch: set bonuses and legendary powers so far ahead of rolled
affixes that rare items became a currency, not a reward, and the entire generator — the machine the game
is built on — turned into a slot machine that only paid in one denomination (*recalled, unverified*).

**What prevents it here.** Three checks, not one intention: the AE budget caps the total (§3.7 device
2); `counter_pressure` is a validated column so an item with no cost cannot be imported (device 1); and
the parity invariant requires a rolled rare at the same rung to beat the unique on raw stats at least
25% of the time (device 3). The philosophy is one sentence — **the rare wins the stat sheet, the unique
wins the build** — and each of its three halves is a check.

**Residual risk, stated:** the parity invariant is unmeasured, and the capability half of the budget is
author-assigned. Both are §10 questions, not solved problems.

### 8.2 The "one correct unique per slot" convergence

**How it goes wrong.** Enough uniques that every role has one, and each role's best is obvious, so the
"build" is a lookup table and every character converges. The item system's variety becomes a checklist.

**What prevents it here.** `UniqueAxisCollision` forbids two uniques sharing `(role, rung band, power
axis)`, so the second unique in a role is always about a *different thing* rather than a stronger
version of the same thing. `UniqueRoleForbidden` keeps 7 of 15 roles per frame unique-free in v1, so no
build is more than half prescribed. And uniques are barred from `jewel-minor`, the duplicated pair whose
budget the ideal deliberately keeps small (§5.5) — the one place where a single strong unique becomes
two.

**Residual risk:** the axis quota gets looser every time a rung band is added, and it is a v1 number.
Re-derive it when the unique count passes 40.

### 8.3 The unique that needs a code change

**How it goes wrong.** Each unique is special, so each needs an `if`. Content velocity dies at item
number twelve, and the balance pass nobody can afford is the one that requires a rebuild.

**This repo has already shipped that failure once, and it is in the tree right now.**
`UniqueEquipmentCatalog` is three items hardcoded in C#
(`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:20-26`), with a hardcoded three-slot allowlist
(`:12`) whose normaliser throws on anything else (`:50-56`), and one of the three points at *"placeholder
effect id for bag prove"* (`:25`). Adding a fourth item is a code change, a rebuild, and a deploy. Three
items is exactly where that model stopped.

**What prevents it here.** SC7's test applied to this lane: a unique is 6–17 rows and no new consumer,
because every mechanism it uses already has one. The refusal that makes it true is §3.5's rung 6 — a
unique needing a 13th kind, a new trigger, or a new predicate leaf is **refused**, and the refusal is
what keeps the other twenty cheap. The sentence to hold the design to is in §6.4: **`Instantiator` never
grows a unique branch.**

**Residual risk:** the pressure to grant one exception is highest for the best-sounding item. §10.1
gives the owner the conversion-kind decision explicitly so it is made once, in the open, rather than
twenty times in a pull request.

### 8.4 The trophy nobody equips

**How it goes wrong.** The unique is cool, weak, and inventory-locked forever. Players screenshot it and
equip the rare. The class becomes flavour.

**What prevents it here.** The parity invariant's *upper* bound (`W ≤ 75%`) — a unique that loses to
three rares in four is refused, not shipped. The variance slot gives a reason to want a second copy. And
`unique_value_reroll` (§4.4) means a bad roll is a cost, not a death sentence — which is the single
biggest driver of this failure mode in practice: an item you cannot improve is an item you stop caring
about the day you find a better roll you cannot have.

**Residual risk:** `unique_value_reroll` is a *request* to I7 and I6, not a decision this lane can make.
If they refuse it, this failure mode's main mitigation is the variance slot alone, and the ±15% identity
spread should probably narrow to ±10% so a bad copy hurts less. Named as a conditional in §9.7.

### 8.5 The unique that binds nowhere

**How it goes wrong.** The item is authored on `combat.crit.rate.omni` because that is what an
interesting amulet does. `stat.derived` is quarantined `None/None/None` (D6). The container validates,
the item drops, the player clicks equip, and the bind gate returns `RuntimeUnsupported`. The player's
reward is an error dialog.

**What prevents it here.** I8's promotion of the runtime-support check from bind time to import time for
`container_kind = 'item'` (`ssot-affixes.md:660-665`), adopted here for the fixed core as well as the
pool. The item is refused when the *author* runs the importer, not when the *player* clicks equip.

**Residual risk:** it is a refusal, not a capability. Until E12 wires `BattleStatComposer`, the entire
`combat.*` half of the design space is unauthorable and every unique is a lawn item. §9.14.

### 8.6 The balance pass that bricks every copy

**How it goes wrong.** A designer retires or re-tiers a shared atom that a unique's identity was built
on. Content is disabled, never deleted (definitions §6), so live copies keep their frozen values — and
then *"new binds reject with `StaleInstance`"*. Every player who owns that unique finds it unequippable
after a content patch, with an error that blames their instance.

**What prevents it here.** §4.6's rule: an identity atom whose magnitude sits outside the shared band
must be a **private** atom row (`atom.unique-{slug}`), owned by that unique alone, so a balance pass on
`vitality` cannot reach it. In-band identity atoms may share, and accept the coupling knowingly.

**Residual risk:** nothing stops an author sharing a row they should not have. A lint — *"a unique's
identity atom is referenced by more than one container"* — is cheap and belongs in the importer. Not
specified here; named in §9.3.

### 8.7 The word

**How it goes wrong.** Someone reads `UpsertUniqueEquipment` or `rpg_unique_actors`, assumes it is this
system, and either extends the wrong thing or reports the wrong bug. This is not hypothetical — the two
concepts already share a name in shipped code and the contract's §1 lock does not mention it.

**What prevents it here.** §3.1's row, the `item_unique_*` prefix rule, and a request that R4 add the
row to the contract's terminology lock.

---

## 9. What this lane needs from other lanes

1. **I1 (rarity)** — register the `unique_eligible` budget key in `rarity_budget` (§5.3), set 0 at
   ordinals 10–20 and 1 at 30–100. Confirm §3.6's *"unique is not a rung"* now that this lane supplies
   the mechanism behind it. And publish the **rolled baseline in AE per rung**, which §3.7's budget
   check divides by and which does not exist in any document yet.
2. **I1 (rarity)** — the simulation harness behind §3.5's overlap invariant. §3.7's parity invariant is
   the same measurement with a fixed-value item on one side, and it should be run on the same code with
   the same seed rather than a second implementation.
3. **I3 (base types)** — confirm that a unique occupies one ordinary `item_base_type` row (§4.2), and
   add the `derived_from` FK target. Also: correct `ssot-item-categories.md:201` — uniques are not a
   category and, per I1, not a rarity either. And an importer lint: *a unique's private identity atom
   must not be referenced by a second container* (§8.6).
4. **I5 (sets)** — accept the mutual exclusion in §3.8 (`UniqueSetMembership`), and agree that the
   **1.5 AE premium is one shared number**, not two. The two role quotas must also be read together:
   I5 caps a set at 6 of ~15 roles, this lane caps uniques at 8 of 15, and 6 + 8 > 15 means some roles
   carry both a set piece and a unique. That is fine, but it is an interaction neither lane sized.
5. **I4 (sockets)** — a unique's socket count is **fixed**, inherited from
   `item_base_type.socket_capacity`, and **never rolled**. If I4's per-rung `socket_min`/`socket_max` is
   a rolled range, uniques opt out of the roll and take the base type's number.
6. **I6 (mutation)** — `enhance_scope = 'magnitude-only'` on uniques (§4.4); the op log must carry
   `unique_value_reroll` as an op kind; and the promotion gate must read the unique flag and refuse,
   forcing `promote_from = 0` regardless of the rung's budget key.
7. **I7 (reroll)** — add **`unique_value_reroll`** to the operation menu (§4.4), with the value-only
   shape spelled out there. Confirm that identity atoms remain non-rerollable in identity — your §2
   already says so and names this class explicitly (`ssot-reroll.md:74`). **If you refuse the
   operation**, say so, because §8.4's mitigation changes shape and the identity spread should narrow
   to ±10%.
8. **I9 (materials and cost)** — what does salvaging a unique yield? The rung's `salvage_yield` turns
   an authored artifact into three shards, which is wrong in both directions (too little for the player,
   too much for the economy if it is raised). Recommend a `no_salvage` flag or a unique-specific yield;
   §10.6 puts it to the owner.
9. **I11 (requirements)** — confirm the faction clause on uniques (`ssot-requirements.md:124-128`), and
   confirm that `brainpan-sigil` in your §7 is the same item as §7.3 here so the two documents do not
   drift into two Brainpan Sigils.
10. **I12 (generation)** — three things. **(a)** Strike or relabel the `R7 | unique` row at
    `ssot-generation.md:409`; your own §3.2 already puts unique selection at step 6, which is correct.
    **(b)** Name the seam: your first-clear grant (§3.5) — *"a container with `pool_rolls = 0`, no rolls
    at all"* — **is** a unique, and it should be authored as one so it inherits the budget and the
    counter-pressure check. **(c)** Confirm no unique pity (§4.5), and that a drop-table entry may name
    a unique's container directly.
11. **I13 (inventory)** — the reserved `no_reassign` flag (`ssot-inventory.md:208`) is exactly the
    quest-unique case; it should be driven by `item_unique.acquisition = 'deterministic'` rather than a
    second flag. And the comparison UI needs to know which lines on an item are fixed and which rolled.
12. **G3 (presentation)** — a unique's tooltip must distinguish **identity lines** from the **variance
    line**, and must render `flavour_key`. The affix naming grammar is bypassed entirely
    (`ssot-affixes.md:783`): a unique's name is authored, in two frame vocabularies.
13. **G4 (granted actions)** — §4.3's second non-expressible class is yours. When `grants_action_id`
    exists, a large share of the genre's iconic uniques become authorable, and the `item_unique` row is
    where the pointer should hang.
14. **E12 (effect-atom)** — until `BattleStatComposer` reads bound `stat.derived` atoms, no unique may
    use crit, elemental power or defence, accuracy, dodge, or the shield stat stack, and every unique is
    a lawn-only item. This is the largest single constraint on what this lane can author, larger than
    SC2.
15. **E1 / the atom program** — where does an event-sourced `box.set` get its **cell**? G2 says the
    executor handles one cell; G5 says an empty target must be a rejection; the `OnDamageDealt`
    inversion suggests "the damaged entity's cell" but nothing states it. §7.1 depends on the answer and
    this lane may not invent it.
16. **E9 (power)** — the AE budget converts to a power-vector budget when one exists, and the
    author-assigned capability price in §7.4 is the line that most needs replacing with a computed one.
    Per SC9 nothing here waits for it.
17. **R4 (reconciliation)** — add the §3.1 terminology row to the contract's §1 lock, and decide §6.3's
    reason-code inflation question across all lanes rather than per lane.

---

## 10. Open questions for the owner

1. **The conversion kind.** §4.3 requests `damage.convert` as a named SC2 ask and depends on nothing.
   Is it *deferred until the damage applier spec has an owner*, or *refused permanently*? Deferred is
   the honest default; refused permanently is also a legitimate answer and would let the lane stop
   raising it. What is not acceptable is leaving it open and letting it arrive one exception at a time.
2. **Reason-code inflation** (§6.3). Six or seven new codes, or one `ContentRuleViolated` with a
   namespaced rule id? This is a decision across all thirteen lanes plus four gap lanes, not just this
   one, and it should be made once at R4.
3. **The parity invariant is unmeasured** (§3.7). `W ∈ [25%, 75%]` is stated with a method and no
   numbers, because I did not re-run I1's simulator. Do you want it measured before any unique is
   authored, or is the budget plus the counter-pressure column enough for v1?
4. **Twenty uniques for v1** (§4.6), five per rung band, 8 humanoid / 8 plant / 4 either. Too many, too
   few, or the wrong spread? The cost is roughly 200 rows and no code, so the constraint is authoring
   hours, not schema.
5. **`unique_value_reroll`** (§4.4). It is the operation that keeps a badly-rolled unique alive, and it
   is a request to I7 and I6. Do you want it in v1, or is "a bad roll is a bad roll" the intended
   feeling?
6. **Salvage** (§9.8). Should a unique be salvageable at all? Recommend no — an item whose acquisition
   was an event should not have a "convert to three shards" button next to it — but that leaves a dead
   row in a bag when the player has two copies, which I13 will have to solve some other way.
7. **The rung floor at 30** (§4.1). The argument is that ordinals 10 and 20 are defined as the absence
   of design. If you would rather ship a `sprout`-rung joke unique, the floor moves to 20 and the
   `unique_eligible` key carries it.
8. **`counter_pressure` as a hard requirement.** Every unique must declare and satisfy one of three
   (§3.7). Some of the genre's best-loved uniques have no drawback at all and win on flavour — the check
   forbids that. Is the structural guarantee worth the design freedom it costs?

---

## Design-gate checklist

```
[x] Subsystems identified — effect-atom (container / instance / binding / validator), rarity,
    affixes, sets, generation, base types, reroll, enhancement, requirements, inventory.
[x] Read every doc the brief and the contract's §5 name, this session, in order.
[x] Checked decisions.md's locks via the contract's §6 owner decisions (OD1–OD7); none forbid
    this document. SC1–SC9 obeyed: no second effect mechanism, no 13th kind assumed, no new
    container_kind, units stated per value, determinism unchanged, every table names a consumer.
[x] Every repo claim cites file:line — ContainerValidator.cs:44-57/73-96/87/134-176/167-171,
    Instantiator.cs:87-93/99-107, PredicateNode.cs:7-21, AtomRejection.cs:24,
    UniqueEquipmentCatalog.cs:12/20-26/25/50-56, RpgStore.cs:337/356.
[x] Verified against CODE, not comments — the tier-window check's scope was read from the loop
    structure, not from the comment on ContainerValidator.cs:87 that happens to agree with it.
[x] Read the surrounding section of every rule quoted, including I1 §3.6, I5 §3.9, I7 §2,
    I8 §4.9, I11 §2.3 and I12 §3.2, all of which were quoted against their own lanes.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no suite was run.** Every
    constraint above is read from code and specs. Before any of it justifies a build decision,
    run tests\FusionRpg.Core.Tests and tests\FusionRpg.Data.Tests.
[ ] The parity invariant in §3.7 is stated and **not measured** (§10.3).
[x] Nothing contradicts an invariant. Five cross-lane contradictions are reported in §3.3 and
    §9 rather than silently resolved.
[x] Corrections routed to the lanes that own them (§9) rather than applied to their files.
```
