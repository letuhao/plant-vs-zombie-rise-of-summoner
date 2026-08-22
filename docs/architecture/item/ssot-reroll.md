# Lane I7 SSOT — reroll and re-option

**Status:** Lane I7 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Reroll is the operation that re-draws what an already-dropped item's affixes are, or how strong they
are. It is the second of the three sinks the ideal names (§8) and the one that decides how long an
endgame lasts.

Throughout, **`T`** means the number of affix slots an operation targets, and **`K = pool_rolls − T`**
means the number left anchored.

**Section map against the contract's required ten**, because three of this lane's brief questions —
window escape, cost, and determinism — do not fold cleanly into any one required slot and are kept as
top-level sections. The ten are all present and in the contract's relative order:

| Contract §3 slot | Here |
|---|---|
| 1 Status header | above |
| 2 Scope | §1 |
| 3 The model | §2 (with §4 *what a reroll may never escape* and §8 *what can never be rerolled* as its boundary halves) |
| 4 Options considered, and the recommendation | §3 (with §5 *cost and escalation* as its fifth pick) |
| 5 Data shape | §7 (with §6 *determinism* as the requirement it hands to I6) |
| 6 Validation and reason codes | §9 |
| 7 Worked examples | §10 |
| 8 Failure modes | §11 |
| 9 What this lane needs from other lanes | §12 |
| 10 Open questions for the owner | §13 |

---

## 1. Scope

### This lane owns

- Every operation that **re-draws** an existing item's rolled affixes after the drop: their values,
  their identities, their tiers.
- The **risk shape**: whether an outcome can be worse, and what a player may do about it.
- **Anchoring** — protecting affixes you like while re-drawing the rest — and its price curve.
- The **deterministic alternative** to gambling, and where its ceiling sits.
- The rule that guarantees **a rerolled item is always an item the generator could have dropped**.
- Reroll's **validation surface** and reason codes.

### This lane does NOT own

| Thing | Lane |
|---|---|
| The instance-mutation model, the operation log, and replay | **I6** — I adopt it; §6 states what I need from it |
| The affix pool, its groups, its tier bands, its weights | **I8** — I draw from their pool, unchanged |
| Materials, currencies, and what a thing costs | **I9** — §5 expresses costs in their vocabulary, with illustrative numbers |
| The rarity ladder and its ordinals | **I1** — I read rarity, I never change it (§8) |
| Sockets and inserts | **I4** — a reroll never touches them |
| Base types, implicits, base stats | **I3** — never rerollable (§8) |
| Set membership | **I5** — a container tag, not a drawn affix |
| Turning a loot event into an instance | **I12** — reroll must reuse their draw, not fork it |
| Bags, storage, item identity rows | **I13** |

**Honest boundary note:** this lane is thin on new machinery, on purpose. Almost everything it needs is
already shipped code — `Instantiator.Draw`, the one-per-group rule, the tier window, the
`roll_seed`/`catalog_revision` pair. What I7 contributes is **a menu, a risk shape, a price curve, and
one hard invariant**. The single genuinely new mechanism is a per-operation seed, and that belongs to
I6.

---

## 2. The model

An item that dropped is one `effect_instance` plus one `effect_instance_atom` row per atom
(`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:56-73`). Its atoms come from two places, and the
split is the whole design:

| Where an atom came from | Table | Rerollable? |
|---|---|---|
| The container's **fixed core** — implicits, unique-item identity | `effect_container_atom` | **Never** |
| The container's **weighted pool** — the drawn affixes | `effect_container_pool` | **Yes, and only these** |

**The one rule this lane exists to enforce:**

> **You may only redraw what the pool drew, and you must redraw it from the same pool.**

Every affix a reroll touches was, by construction, a random choice the generator already made.
Everything it refuses to touch was a deliberate authored choice. That single rule is what makes
"rerolls produce an item the generator could never have dropped" *structurally* impossible rather than
*carefully avoided* — a reroll calls the same weighted draw the drop called, against the same container
row, with the same `[min_tier, max_tier]` window and the same one-per-group exclusion
(`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:125-161`).

A reroll is therefore always the same five steps: pick a subset of the instance's drawn `seq` values,
throw them away, re-run the draw for exactly that many slots with the surviving groups excluded, freeze
the new values, replace the rows. The count never changes. The container never changes. The tier window
never changes.

Three operations. Everything not targeted is **anchored**, and anchoring is what costs money.

---

## 3. Options considered, and the recommendation

### 3.1 The operation menu

**Candidates considered:** value-only reroll · single-affix identity reroll · full reroll · rarity
reroll · tier-only reroll · add-an-affix · remove-an-affix.

Two get cut immediately on schema grounds, not taste grounds:

- **Rarity reroll is not expressible as a reroll.** Rarity lives on the *container*, not the instance
  (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:51`), and so do `pool_rolls`, `min_tier`, and
  `max_tier` (`ContainerRow.cs:57-64`). "Change this item's rarity" means repointing `container_id` —
  and `container_id` is the first term of the reproduction contract (`Instantiator.cs:67`,
  definitions §5). That is a **re-instantiation against a different template**, an upgrade path, not a
  re-draw. Handed to **I1** (does the ladder allow it) and **I12** (who owns instantiation). Not mine.
- **Add / remove an affix** changes affix *count*, and count is `pool_rolls`, a rarity-selected
  container column. Same argument. Not mine.

**Tier-only reroll** stays, but not as its own operation. Tier is baked into the atom id
(`{family_id}[.{variant}].t{tier}`, definitions §1), so "reroll the tier" and "reroll the identity" are
the same schema edit — swap `atom_id`. It falls out of an identity reroll *restricted to the target's
own group*, which is a flag, not a fourth button.

**Recommended menu — three operations:**

| Operation | Re-draws | Targets | What it feels like |
|---|---|---|---|
| **Temper** | the **value** of one affix, inside that atom's own authored range | exactly one drawn `seq` | Cheap, low variance, the on-ramp. Your `+32 atk` becomes `+29` or `+38`. It is still `atk`. |
| **Reforge** | the **identity, tier, and value** of a chosen subset of drawn affixes | `T` = 1 … `pool_rolls` drawn `seq` values | The power tool. Everything you did not target is anchored, and anchoring is the price. |
| **Imprint** | nothing — **places** a chosen affix group at the window's floor | one drawn `seq` | The deterministic escape hatch. Guaranteed, and deliberately the worst legal version. |

**Why "reroll one affix" is not a fourth operation.** It is Reforge with `T = 1` and `K = pool_rolls − 1`
anchors, and the anchor multiplier prices it correctly with no extra formula. One price function covers
both "scramble the whole thing cheaply" and "surgically replace slot 3 at great expense", and there is
nothing for the two to disagree about. A separate operation would need its own cost table and would then
drift from this one.

**Rejected alternative — the wide menu.** Diablo 2 shipped a dozen cube recipes and Path of Exile ships
thirty-plus currency items *(both recalled from general knowledge, unverified)*. That works when the item
system is the whole game. Here it would be twelve UI surfaces, twelve price tables, and twelve places for
the §4.1 invariant to be forgotten.

### 3.2 Better or worse — the risk shape

The owner wants outcomes that can be worse. Three shapes were on the table:

| Shape | What it does to grind | Verdict |
|---|---|---|
| **Blind accept** — the new roll replaces the old, seen only afterwards | Highest variance, cheapest per attempt, most attempts | **Picked as the default** |
| **Pick from N candidates** — roll N, keep one | Removes "worse" outright: the expected outcome becomes monotonically upward. Multiplies rolls per attempt by N at no risk | **Rejected** — it is the owner's stated want, inverted |
| **See-then-decline** — pay to roll, decline for free | Also removes "worse". Becomes reroll-until-perfect at only the roll price | **Rejected as the default**, kept as a priced exception |

**Recommendation: blind by default, with one priced take-back called Recall.**

- **Blind** is the base cost. The result lands, it is recorded, it may be worse, and it is yours.
- **Recall** is an opt-in flag on a Temper or Reforge. It costs the base price **plus one recall
  token** — a scarce material (I9 names it) that is not craftable in bulk. After a Recalled operation,
  and only until the next operation on that item, the player may append a **revert**. The token is
  consumed either way.

Recall exists so a player who finally rolls the item they wanted is not one mis-click from losing it. It
is not a laundering loophole, because it is capped at one revert, only against the immediately preceding
operation, and gated on a material the economy controls (§11.5).

**A revert is an appended compensating operation, never a deleted one.** The log is append-only. That
matters for I6 (§6) and it is what stops "undo" becoming a hole in the audit trail.

### 3.3 Locking

The single biggest determinant of endgame length. Three known positions:

| Position | Consequence |
|---|---|
| **No locking** — Reforge is all-or-nothing | Very long endgame, very high frustration; the good affix you already had is destroyed every attempt |
| **Free unlimited locking** | Endgame collapses. Lock five of six, spam the sixth, converge on perfect. Rarity and drops stop mattering |
| **Priced locking with a hard structural cap** | The middle, and the only one worth building |

**Recommendation: anchoring is implicit, superlinear, and can never cover everything.**

- **Implicit.** There is no anchor list. Everything not in the operation's `targets` is anchored. One
  input, so two lists can never disagree.
- **Superlinear.** `ANCHOR_MULT = 2^K`. Anchoring four of six affixes costs **16×** the base Reforge.
  Precision is the expensive thing, and it should be.
- **Structurally capped.** `T ≥ 1` is a validation rule, not a guideline. You may never anchor every
  drawn slot, because that operation is a no-op that charges money.
- **Nothing accumulates.** Anchors are declared per operation and are not item state. There is no anchor
  to build up, buy, or bank between attempts.

The fixed core is *implicitly and permanently* anchored — it was never in the pool, so it is never a
legal target. That needs no rule; it falls out of §2.

### 3.4 The deterministic alternative — Imprint

Most modern ARPGs ship both a gamble and a guarantee, and the guarantee is what stops a player hitting a
wall they cannot pay past. Path of Exile's crafting bench is the model worth copying *(recalled,
unverified)*: **deterministic outcomes exist, and they are deliberately mediocre.**

**Recommendation: yes, and it lands at the floor.**

**Imprint** replaces one drawn affix with a chosen **group**, at the container's `min_tier`, with the
value spec resolved at its **minimum**. No roll. It costs roughly ten Reforges plus a catalyst material.
What it guarantees is *presence*, never *strength* — the player then Tempers and focus-Reforges upward
from a known floor.

That one mechanism is also the bad-luck protection. A pity counter was considered and rejected: it needs
durable per-`(instance, group)` state, which SC7 would rightly demand a consumer for, and the
deterministic floor gives the player the same outcome — *"I will definitely be able to get fire power on
this"* — with no new table and no hidden counter.

---

## 4. Does a reroll respect rarity, the tier window, and item level?

**Yes, and not by a rule — by reusing the same code path.** There is no escape hatch, and there is no
place to put one.

| Existing pool rule | Where it lives | How a reroll interacts with it |
|---|---|---|
| **One atom per group**, defaulting to `(family_id, variant)` | `Instantiator.cs:162-163`; spec-container-schema §"group" | A partial redraw must seed the exclusion set with the groups of the **retained** affixes before drawing, or a Reforge could hand you `+fire power t3` beside `+fire power t5`. This is the one behavioural change I need from I6 (§6.3) |
| **Tier window** `[min_tier, max_tier]` | `ContainerRow.cs:57-58`; violation is `TierOutOfWindow` | Every candidate is already inside the window because it is a row of the same container's pool. A drift check still runs post-op (§4.1) |
| **Weighted draw**, `weight = 0` never drawn | `Instantiator.cs:134-135` | Unchanged. A reroll uses the same weights as the drop, so the affix distribution of a rerolled item equals that of a fresh one |
| **`pool_rolls`** — how many affixes exist | `ContainerRow.cs:64` | Invariant. A reroll always ends with exactly as many drawn atoms as it started with |
| **Rarity** | `ContainerRow.cs:51` | Read-only for this lane. Rarity selects `pool_rolls` and the window; a reroll changes neither |
| **Item level** | expressed today *as* the tier window, not as a column | Same answer: the window is the item level, and it does not move |

### 4.1 The post-operation invariant — the guard, stated as a test

Before an operation commits, the mutated atom set must validate **as if it had just been freshly
instantiated from its own container**:

```text
count(drawn atoms)            == container.PoolRolls
distinct groups(drawn atoms)  == count(drawn atoms)      // one-per-group holds
every drawn atom              ∈ container.Pool
every drawn atom's tier       ∈ [container.MinTier, container.MaxTier]
```

**A failure rolls the whole operation back, spends nothing, and returns a reason code.** This is what
makes "an item the generator could never have dropped" impossible rather than merely unlikely. It also
catches the realistic case: content was re-authored between the drop and the reroll, the pool row that
produced an anchored affix is gone, and the item is no longer reproducible from its own template.

### 4.2 Two code-verified hazards that bite here

1. **The pool draw does not exclude disabled atoms.** `Instantiator.Draw` filters only on `Weight > 0`
   (`Instantiator.cs:134-135`), and `ContainerValidator` never reads `AtomRow.Enabled`
   (`src/FusionRpg.Core/Effects/Atoms/AtomRow.cs:58`). So a disabled atom is drawable — and `BindGate`
   then refuses the resulting instance with `StaleInstance`
   (`src/FusionRpg.Core/Effects/Atoms/BindGate.cs:53-58`). Today that is a latent drop bug; under reroll
   it is a player paying real currency for an unequippable item. **Reroll must exclude disabled atoms
   from the candidate set**, and if that drops the drawable-group count below `T`, reject with
   `PoolRollsExceedGroups` *before* charging. Reported to **I8** and **I12** as a defect in the shared
   draw, not patched locally.
2. **Two sources of truth for `pool_rolls`.** `ContainerRow.PoolRolls` (`ContainerRow.cs:64`) and
   `RarityRow.PoolRolls` (`ContainerRow.cs:93`) both exist. The invariant above needs one authoritative
   answer. **I1** must rule which wins; I assume the container's until told otherwise.

---

## 5. Cost and escalation

**Vocabulary is I9's.** Two spend paths exist in the tree today, and I express costs against them:

| Resource | What exists | Where |
|---|---|---|
| **Souls** — the soft currency | `rpg_soul_balances` / `rpg_soul_ledger`, with an atomic idempotent spend keyed on `(player, reason, correlationId)` | `src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs:178-215` |
| **Materials** — the gating resource | `rpg_demon_materials(player_id, material_id, qty)`, seeded with `essence.{element}` and `shard.{rarity}` | `src/FusionRpg.Data/Sqlite/RpgStore.cs:520`; `src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:17,19` |

`TrySpendSouls` refuses without writing, and a replayed correlation returns the original success without
double-spending (`RpgStore.Souls.cs:191-201`). That is exactly the semantics a recorded reroll operation
needs, and I ask I9 to keep it (§12.4).

### 5.1 The price function

```text
souls    = BASE[op] × ANCHOR_MULT × FOCUS × ESCALATION‰ / 1000

K            = pool_rolls − T                       anchored slot count
ANCHOR_MULT  = 2 ^ K                                Temper: 1 (single target, no anchors)
FOCUS        = 3 when a Reforge target is restricted to its own group; else 1
ESCALATION‰  = min(4000, 1000 + 250 × priorOps(instance, op_kind))
```

**Units:** `souls` are ledger units. `ESCALATION‰` is integer per-mille, per SC4. `ANCHOR_MULT` and
`FOCUS` are plain integer multipliers. No floats reach content.

### 5.2 Escalation — rising, but to a ceiling

| Shape | Why not |
|---|---|
| Flat cost per attempt | Infinite cheap rerolls. Rarity and drops stop mattering — the first named failure mode |
| Rising forever | The item bricks. Nobody uses the system past attempt twenty — the third named failure mode |
| **Rising to a cap** | **Picked** |

`min(4000, 1000 + 250 × n)`: attempt 0 costs 1.00×, attempt 4 costs 2.00×, attempt 12 costs 4.00× and
never more. The ramp means casual play never meets a wall; the cap means the system stays usable forever
on a beloved item.

**The counter is per `(instance, op_kind)` and never resets.** Temper attempts do not inflate Reforge and
vice versa — they are different levers, and cross-inflation is opaque to the player.

**It needs no column.** `priorOps` is `count(*)` over the item's operation log filtered by kind. The
history needed for auditing already answers the pricing question. That is deliberate SC7 hygiene: no
second source of truth for how many times an item has been rerolled.

### 5.3 Illustrative base costs

Numbers are illustrative and unbalanced; I9 owns the real ones.

| Operation | Souls base | Materials |
|---|---|---|
| Temper | 400 | 1 × `shard.{rarity}` |
| Reforge | 1 500 | `T` × `shard.{rarity}`, plus 1 × `essence.{element}` per focused target |
| Imprint | 15 000 (≈ 10 × Reforge) | 1 × `catalyst.{rarity}` — a material that does not exist yet, named for I9 |
| Recall (flag) | +0 souls | 1 × `recall-token`, consumed whether or not the revert is used |

**The real brake is the material, not the souls.** Souls cap at 4×; the shard cost does not cap in
*count* — one shard per attempt, forever. If reroll is to stay a sink rather than a treadmill, the shard
faucet is the dial, and that is an I9 conversation (§11.1, §12.4).

---

## 6. Determinism — what I hand to I6

**I6 owns the mutation model. This section is my requirement list, not a competing design.**

### 6.1 The problem, stated exactly

SC5 gives the atom layer's contract: same `(container_id, catalog_revision, roll_seed)` ⇒ byte-identical
instance. A reroll breaks it, because the same three inputs must now produce a *different* item than they
did before the operation. The contract must become:

> `(container_id, roll_seed, catalog_revision₀)` **plus** an ordered list of operations, each carrying its
> **own** `op_seed` and its **own** `catalog_revision`, reproduces the instance's atom rows.

### 6.2 What every operation must record

| Field | Why it cannot be omitted |
|---|---|
| `instance_id` | which item |
| `op_seq` | 0-based, monotone per instance. **The replay order.** Two operations on one item do not commute |
| `op_kind` | `temper` · `reforge` · `imprint` · `revert` |
| `target_seq_json` | the `effect_instance_atom.seq` values this operation re-drew. `seq` is the instance's stable affix address — it is half the primary key (`RpgStore.AtomInstances.cs:72`) |
| `focus_json` | which targets were group-restricted (Reforge), or the chosen group (Imprint) |
| `op_seed` | **INTEGER, this operation's own seed.** Not derived from `roll_seed` — the same item rerolled twice must differ, and it cannot if the seed is a function of the instance alone |
| `catalog_revision` | the revision *this operation* resolved against. Not the instance's. Content is re-authored between operations, and replaying an old draw against today's pool would silently change an item nobody touched |
| `correlation_id` | idempotent replay of the *request*, matching `TrySpendSouls` (`RpgStore.Souls.cs:191`) |
| `recall_token_spent` | whether a revert is still legal against this operation |
| `applied_utc` | provenance |

### 6.3 The one code change I need

`Instantiator.Draw` (`Instantiator.cs:125`) draws `container.PoolRolls` atoms with an empty exclusion set.
Reroll needs the same function with two parameters:

```csharp
static List<string> Draw(ContainerRow container, Func<string, AtomRow?> lookupAtom,
                         long seed, int count, IReadOnlySet<string> excludeGroups)
```

`count` is `T`; `excludeGroups` holds the group of every **retained** atom. The existing call site passes
`PoolRolls` and an empty set, so instantiation is unchanged. It must be the *same* function — if I12's
generator and I7's reroll ever fork it, §4.1's invariant becomes a claim rather than a consequence.

### 6.4 Seed streams — already correct, and worth stating

The two streams reroll needs both exist and both already do the right thing:

| Operation | Stream | Note |
|---|---|---|
| Reforge draw | `atom.pool.{container_id}`, seeded from `op_seed` | `Instantiator.cs:132` |
| Value freeze (Temper, and every newly drawn atom) | `atom.pool.freeze.{atom_id}.{seq}`, seeded from `op_seed` | `Instantiator.cs:182-183` |

Because the freeze stream is keyed on `(atom_id, seq)` and the seed comes from the operation, **Temper is
a one-argument change to the existing `Freeze`**: pass `op_seed` where `rollSeed` goes. Nothing else in
the value path moves.

**A consequence worth naming:** `Freeze` copies `spec.Min` verbatim for a `Fixed` value spec
(`Instantiator.cs:204`). Tempering an affix whose value spec is `Fixed` therefore cannot change anything,
and must be refused at validation rather than sold (§9.1, `NotRerollable`).

### 6.5 The honest limit — two guarantees, one achievable

This is the finding I most want I6 to rule on.

| Guarantee | Achievable today? |
|---|---|
| **Auditable + idempotent** — the log explains every value on the item, and re-running the same request does not double-apply | **Yes.** Needs only the table in §6.2 plus the existing dedupe semantics |
| **Byte-reproducible after mutation** — replay the log and get the same atoms | **No, not in general.** `catalog_revision` is one monotonic integer in a `content_meta` row (definitions §5). The *catalog at that revision* is archived nowhere, so an operation resolved against revision 41 cannot be replayed once the catalog reaches 42 |

**My position:** I7 requires **auditable + idempotent** now, and asks I6 to state plainly that
byte-reproducibility after mutation holds **only while `catalog_revision` has not moved past the
operation's recorded value**. Past that point the stored `effect_instance_atom` rows are the SSOT and the
log is provenance. Pretending otherwise would put a promise in a spec that no code can keep.

Funding a catalog archive to make full historical replay real is a legitimate alternative and a real
cost. That is an owner call (§13.7), not mine.

---

## 7. Data shape

### 7.1 What I reuse unchanged

| Existing | Used for |
|---|---|
| `effect_instance_atom(instance_id, seq, atom_id, values_json, power_json)` | The rows a reroll rewrites. `seq` is the target address |
| `effect_container_pool(container_id, atom_id, weight, group)` | The candidate set. Never modified by a reroll |
| `effect_container.pool_rolls / min_tier / max_tier / rarity` | The invariant a reroll must preserve |
| `effect_instance.roll_seed / catalog_revision / origin` | Origin state. **`origin` is not rewritten** — an item that dropped stays `drop`; how it changed afterwards is the log's job |
| `rpg_soul_ledger` idempotent spend | Payment, in one transaction with the log append |
| `rpg_demon_materials` | Material spend |

### 7.2 What is new

**One table, owned by I6, shaped by §6.2.** Working name `effect_instance_op`; I6 names it.

Its consumers, per SC7: the reroll service reads it for `priorOps` pricing (§5.2) and for revert
legality; the item detail view reads it for provenance; support reads it to answer *"what happened to my
item"*. Three readers before it ships. No column in it is decorative.

**No table for anchors.** `K = pool_rolls − T`, derived from a field already in the log.

**No table for escalation counters.** Derived from the log.

### 7.3 Two new reason codes, and what they cost

The rejection list is closed at 33 (definitions §10,
`src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs:9-116`) and a guard test asserts the enum has exactly
34 values including `None` (`tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:33`). Adding two
moves that assertion 34 → 36. That is a small, real cost and it is a reviewed change, not an assumption.

| Proposed code | Fires when |
|---|---|
| `NotRerollable` | The target cannot be re-drawn at all: it is a fixed-core `seq`, or its value spec is `Fixed` (Temper), or every drawn slot was anchored, or a revert was requested with no recall token recorded |
| `RerollLocked` | The instance has a live session-scoped (`entity:`) binding, or a match is in progress |

Both were checked against the existing 33 first. `ScopeUnsupported` is about a *kind* versus an *owner
scope*, and reusing it for "the item is in play" would make the operator-facing surface lie.

---

## 8. What can never be rerolled

**The line: drawn versus authored.**

| Never rerollable | Owner | Why the line is here |
|---|---|---|
| **Base type** | I3 | It is `container_id`, the first term of the reproduction contract (`Instantiator.cs:112`). Changing it does not modify an item; it makes a different item |
| **Implicits and base stats** | I3 | They are `effect_container_atom` rows — the fixed core, never in the pool. The ideal is explicit that implicits are *"the reason two items in the same slot feel different"* (item-ideal §6.1). Make them rerollable and every base type collapses into "the one with the best pool" |
| **Affix count** | I1 | `pool_rolls` is a rarity-selected container column. Changing it is changing rarity |
| **Rarity** | I1 | Lives on the container (`ContainerRow.cs:51`). See §3.1 |
| **Set membership** | I5 | A container tag. Same argument as rarity — authored identity, not a draw |
| **Sockets and their inserts** | I4 | A socket is not a pool draw and an insert is a separate container. A reroll of an item's affixes must leave every insert in place, untouched, and must never be a way to reset socket count |
| **Item identity row** — name, bind-on-pickup, favourite flag | I13 | Not effects at all |

**The defence, in one sentence:** everything on this list was an authored decision, and everything a
reroll touches was a random one. That is not an arbitrary line — it is the line that makes §4.1's
invariant true, and it is the reason the "impossible item" failure mode cannot occur.

**The tempting exception, and why it is refused.** *"Let a very expensive operation reroll an implicit."*
It would sell well and it would destroy base types as a design axis in one patch. If a base type's
implicit is not good enough to be worth choosing, the fix is a better implicit, not a currency that
erases it.

---

## 9. Validation and reason codes

Every row rejects **before** any currency is spent. A refusal writes nothing — the same law
`TrySpendSouls` already follows (`RpgStore.Souls.cs:207-211`).

### 9.1 Request shape

| Bad input | Reason code |
|---|---|
| `target_seq` not present in `effect_instance_atom` for this instance | `BadParamValue` |
| `target_seq` names a fixed-core atom | **`NotRerollable`** |
| Temper on an affix whose value spec is `Fixed` — `Instantiator.cs:204` copies `Min`, so the operation is a paid no-op | **`NotRerollable`** |
| Temper with more than one target | `BadParamValue` |
| Temper on an affix whose value spec is `OnApply` — the value belongs to the hit, not the item, and was never frozen (`Instantiator.cs:206`) | **`NotRerollable`** |
| Reforge with `T = 0` — every drawn slot anchored | **`NotRerollable`** |
| Reforge with `T > pool_rolls` | `BadParamValue` |
| Duplicate `seq` inside `targets` | `DuplicateSeq` |
| `focus` naming a target not in `targets` | `BadParamValue` |
| Imprint naming a group with no row in this container's pool | `UnknownAtom` |
| Imprint targeting a `seq` whose group would then duplicate a retained affix's group | `DuplicateAtomInContainer` |
| Revert naming an operation that is not the immediately preceding one | `BadParamValue` |
| Revert with no recall token recorded on the target operation | **`NotRerollable`** |

### 9.2 Instance and content state

| Bad input | Reason code |
|---|---|
| `container_id` no longer resolves — the base type was withdrawn | `UnknownContainer` |
| A **retained** atom is disabled or withdrawn — `BindGate.cs:53-58` would refuse the equip afterwards | `StaleInstance`, refuse the operation. The alternative is charging a player to improve an item that still cannot be worn |
| A **candidate** atom is disabled | Excluded from the draw, then re-checked: if drawable groups now fall below `T`, `PoolRollsExceedGroups`. **See §4.2 — the shared draw does not do this today** |
| Every remaining candidate row has `weight = 0` | `UnsatisfiablePool` |
| A drawn atom's tier falls outside `[min_tier, max_tier]` after content drift | `TierOutOfWindow`, whole operation rolled back |
| Post-operation invariant (§4.1) fails for any reason | The specific code above; **never a partial commit** |
| `catalog_revision` has moved since the instance was created | **Not a rejection.** The operation proceeds and stamps the current revision. The item is simply no longer byte-reproducible from origin (§6.5) |

### 9.3 The equipped item

An `effect_binding` points at `instance_id`, and the atoms live in `effect_instance_atom` beneath it
(`RpgStore.AtomInstances.cs:66-88`). Rewriting those rows under a live binding changes an actor's effect
list underneath a running system.

| Case | Behaviour |
|---|---|
| Instance has an `entity:` binding | **`RerollLocked`.** `entity:` bindings are session-scoped and never durable (spec-instance-and-binding, Boundaries). A reroll mid-match mutates a live combatant |
| A match is in progress for this player | **`RerollLocked`** |
| Instance has only durable bindings (`player:`, `plant:N`, `zombie:N`) and no match is live | **Allowed.** Withdraw the bindings, rewrite the atoms, re-bind — **all in one transaction** — and re-run the bind gate on the way back in. If the re-bind now fails (a newly drawn atom's kind is unsupported in that runtime, or `LevelTooLow`), the whole transaction rolls back and the operation is refused with the bind gate's own code |
| Item is unequipped and sitting in a bag | Allowed — **but see the defect below** |

**Defect found, and it blocks the whole bench.** `CollectOrphanInstancesUnlocked`
(`RpgStore.AtomInstances.cs:461-471`) deletes every `effect_instance` with no `effect_binding` row, and
it runs after every withdraw. An unequipped item has no binding. **Today, unequipping an item deletes
it**, so the natural workbench flow — take it off, improve it, put it back on — destroys the item. This
is correct behaviour for the `entity:`-scoped match grants the sweep was written for, and catastrophic
for player-owned gear. It is I13's and I6's to fix (either a bag row counts as a reference, or
player-owned instances are exempt from the sweep), and no reroll operation can ship before it is.

### 9.4 Payment

Not atom rejections — these belong to I9's surface. Named here so the two error vocabularies do not
collide.

| Case | Behaviour |
|---|---|
| Insufficient souls | `souls.insufficient`, the string `TrySpendSouls` already returns (`RpgStore.Souls.cs:210`) |
| Insufficient materials | I9's equivalent |
| Same `correlation_id`, same request | **Not an error.** Return the original result, spend nothing, append nothing (`RpgStore.Souls.cs:195-201`) |
| Same `correlation_id`, different request | `correlation.mismatch` (`RpgStore.Souls.cs:202`) |
| Payment succeeds, operation then fails validation | Cannot happen — validation runs first, and the spend and the log append share one transaction |

---

## 10. Worked examples

**Numbers are illustrative, not balanced.** Units are stated on every value. The examples lean on
`stat.modify` and `resource.delta` families, because `stat.derived` is quarantined `None/None/None` (D6)
and an item made of `+fire power` binds nowhere until E12 ships.

### 10.1 Temper — a rare plate-helm, and a worse outcome

`item.plate-helm-rare` · humanoid · `head` · rarity `rare` · `pool_rolls = 4` · window `t2–t4`

| `seq` | Source | Atom | Value | Unit |
|---|---|---|---|---|
| 0 | fixed core (implicit) | `atom.vitality.t2` | +25 | hp, game units |
| 1 | drawn | `atom.might.t3` | +32 | atk, game units — authored range 28–40 |
| 2 | drawn | `atom.fortitude.t2` | +80 | ‰ maxHp increased |
| 3 | drawn | `atom.regeneration.t3` | +14 hp per 5 000 | hp per ms |
| 4 | drawn | `atom.lifesteal.t2` | 35 | ‰ of damage dealt |

**Operation 0 — Temper `seq 1`, blind.**
`T = 1`; Temper has no anchors so `ANCHOR_MULT = 1`, `FOCUS = 1`, and `priorOps(temper) = 0` gives
`ESCALATION‰ = 1000`.
Cost: **400 souls + 1 × `shard.rare`**. Freeze stream `atom.pool.freeze.atom.might.t3.1`, seeded from
`op_seed`.

Result: **+32 atk → +29 atk.** Worse. Blind mode: it is recorded and it stands. `seq 1` still holds
`atom.might.t3` — Temper never changes what an affix is, only where in its range it sits.

**Operation 1 — Temper `seq 1` again.**
`priorOps(temper) = 1` gives `ESCALATION‰ = 1250`. Cost: **500 souls + 1 × `shard.rare`**.
Result: **+29 → +38 atk.**

Two operations, 900 souls, 2 shards, net +6 atk. The on-ramp working as intended: cheap, legible, and it
can go backwards.

### 10.2 Reforge, focused — an epic plant nozzle, tier-hunting one affix

`item.pea-nozzle-epic` · plant · `muzzle` · rarity `epic` · `pool_rolls = 5` · window `t3–t5` ·
11 drawable groups in the pool

| `seq` | Atom | Value | Unit |
|---|---|---|---|
| 1 | `atom.ferocity.t4` | +140 | ‰ atk increased |
| 2 | `atom.searing-strike.fire.t4` | 180–260 on hit | hp damage, `OnApply` — **not frozen at instantiate**, so Temper cannot touch it (§9.1) |
| 3 | `atom.flourishing.t3` | −900 | ms `produceInterval` |
| 4 | `atom.quickening.t3` | −700 | ms `attackInterval` |
| 5 | `atom.warded.fire.t3` | 320 | shield capacity, game units |

The player wants a higher tier of `flourishing` and nothing else to move.

**Operation 0 — Reforge, `targets = [3]`, focused on group `(atom.flourishing, '')`.**
`T = 1`, `K = 4` ⇒ `ANCHOR_MULT = 2⁴ = 16`. `FOCUS = 3`. `ESCALATION‰ = 1000`.
Cost: **1 500 × 16 × 3 × 1.000 = 72 000 souls**, plus 1 × `shard.epic` and 1 × `essence.earth`.

The focused draw sees only that group's rows inside the window — `t3` (weight 60), `t4` (30), `t5` (10)
— and the four retained groups are in the exclusion set. It draws **`atom.flourishing.t4`, −1 200 ms
`produceInterval`**. Better.

Post-op invariant: 5 drawn atoms, 5 distinct groups, every tier in `[3, 5]`. Passes.

Note what a *focused* Reforge is doing here: it is a tier reroll, and it needed no fourth operation and
no new column — only that **I8 authors more than one tier of a group into the pool** (§12.6). If the pool
holds one row per group, focus has nothing to draw and this operation is dead content.

### 10.3 Reforge with anchors, escalation, and an Imprint rescue

`item.warplate-legendary` · humanoid · `torso` · rarity `legendary` · `pool_rolls = 6` · window `t3–t5` ·
12 drawable groups

Anchored: `seq 1, 2, 4, 6`. Targeted: `seq 3, 5`. The item has already been Reforged 7 times.

`T = 2`, `K = 4` ⇒ `ANCHOR_MULT = 2⁴ = 16`. `FOCUS = 1`.
`ESCALATION‰ = min(4000, 1000 + 250 × 7) = 2750`.
Cost: **1 500 × 16 × 1 × 2.750 = 66 000 souls**, plus 2 × `shard.legendary`.

The two redrawn slots exclude the four anchored groups, leaving **8 drawable groups** for 2 draws.

| `seq` | Before | After | Verdict |
|---|---|---|---|
| 3 | `atom.stoicism.omni.t3` — `stat.derived`, quarantined, binds nowhere (D6) | `atom.bulwark.t4`, +180 ‰ maxHp More | Better, and it actually binds |
| 5 | `atom.retribution.t5`, 220 ‰ of damage taken reflected | `atom.mending.t3`, +60 hp on grant | **Worse** for this build |

Blind mode: it stands. The player wanted `atom.savagery` (atk More) in `seq 5` and did not get it.

**The grind, made legible.** With 8 drawable groups and 2 draws, the chance a specific group appears in
one Reforge is roughly `2 / 8 = 25%` (ignoring weights). Expected attempts: **4**. At 66 000 souls each
that is **~264 000 souls and 8 legendary shards** for one targeted affix — and the escalation multiplier
is already at its 4.000 cap by attempt 12, so it does not run away further.

**The Imprint rescue.** Rather than gamble a fifth time, the player Imprints `seq 5` to the
`(atom.savagery, '')` group: **15 000 souls + 1 × `catalyst.legendary`**, no roll. They receive
`atom.savagery.t3` — the window's floor — at the minimum of its value range, **+60 ‰ atk More**, against
the `t5` roll of **+150 ‰** they were chasing. Guaranteed presence, deliberately poor strength. From
there, focused Reforges on that single group climb the tier ladder at `2⁵ = 32×` base apiece.

That is the intended shape of the whole lane in one paragraph: gambling is cheap and can go backwards,
precision is expensive, and the guarantee exists but starts at the bottom.

---

## 11. Failure modes

### 11.1 Infinite cheap rerolls make rarity and drops meaningless

**Prevented by three independent things**, because one would not be enough:

1. A reroll **cannot change rarity or affix count** (§8). A rerolled magic item is still a magic item
   with two affixes. The drop still decides the ceiling; the reroll only decides where inside it you
   land.
2. The tier window does not move, so a low-ilvl base can never be rerolled into a high-tier one.
3. Cost escalates per item, per operation kind, to a 4× cap.

**Honest limit:** because the souls cost caps, with unbounded time souls stop being a brake. The brake
that does not cap is the **material count** — one `shard.{rarity}` per attempt, forever. If I9 makes
shards abundant, this failure mode returns and no amount of soul pricing will stop it. Named as a
dependency (§12.4), not papered over.

### 11.2 Reroll-until-perfect turns the endgame into a spreadsheet

**Mitigated, not solved, and I will not claim otherwise.** Every anchoring system in every shipped ARPG
is a spreadsheet at the top end — Path of Exile crafting has third-party expected-value calculators that
serious players treat as mandatory *(recalled, unverified)*. What a design buys here is the **slope**,
not the elimination.

What this design buys:

- `2^K` anchoring makes the fully targeted operation on a 6-affix item **32×** the untargeted one.
  Optimisation is possible and expensive, which is the correct relationship.
- Blind-by-default means the spreadsheet computes an *expected* outcome, never a *chosen* one.
- Anchors do not accumulate. There is no state to optimise between attempts, only a decision per attempt.
- Imprint caps the value of extreme optimisation: the deterministic floor is always reachable and always
  mediocre, so the gap between "I ground for a week" and "I paid the guaranteed price" is tier strength,
  not affix presence.

### 11.3 A system so punishing nobody uses it

**Prevented by the on-ramp and the ceiling.**

- **Temper is cheap** (400 souls base, one shard) and **low variance** — it moves a value inside a range
  it already occupies. A player who never touches Reforge still uses the system.
- **Escalation caps at 4×.** No item ever becomes unimprovable.
- **Imprint means no wall is permanent.** The classic quit-the-game moment in reroll systems is *"I have
  needed this one affix for two hundred attempts."* Imprint removes that state from the game, at a price.
- The failure that usually produces this outcome is a **non-refundable operation that can brick an
  item** — destroying it, or downgrading its rarity. Nothing here can do that. The worst possible outcome
  of any operation is a worse roll on the same item, in the same slot, at the same rarity, with the same
  number of affixes.

### 11.4 Rerolls producing an item the generator could never have dropped

**Structurally prevented, and asserted.** §2's rule plus §4.1's post-operation invariant. Every candidate
is a row of the item's own `effect_container_pool`; the count, the groups, and the tier window all
re-validate before commit; a failure rolls the whole operation back and spends nothing.

The one way it could still happen is if **I12's generator and I7's reroll ever use two different draw
functions.** Named as a hard dependency (§12.9), and the reason §6.3 asks for a *parameter* on the
existing `Draw` rather than a new function beside it.

### 11.5 Recall as a laundering loophole

If the take-back is cheap, blind mode dies. Every outcome silently becomes best-of-two, the owner's "can
be worse" requirement evaporates, and the expected result of every operation shifts upward with no price
reflecting it. Prevented by: one revert only, only against the immediately preceding operation, gated on
a material the economy controls, and the token is consumed whether or not the revert is used.

### 11.6 The workbench deletes the item

Not a design failure mode — a live defect in shipped code, found while writing this. §9.3: unequipping an
item makes its instance an orphan, and `CollectOrphanInstancesUnlocked`
(`RpgStore.AtomInstances.cs:461-471`) deletes orphans after every withdraw. It blocks every operation in
this lane.

### 11.7 The value reroll that does nothing

Diablo-line games ship value rerolls that silently no-op on fixed-value affixes, and players learn the
rule by wasting currency. Here, two of the three roll policies produce a Temper that cannot change
anything: `Fixed` copies `Min` (`Instantiator.cs:204`), and `OnApply` was never frozen at all
(`Instantiator.cs:206`). Both are validation rejections (§9.1), not silent no-ops — the operation refuses
before charging, which is SC6 applied to a player-facing action rather than to a content row.

---

## 12. What this lane needs from other lanes

1. **I6 — the operation log and its replay contract.** The table shape in §6.2, the append-only revert in
   §3.2, and a ruling on the reproducible-versus-auditable split in §6.5. I adopt whatever they name it;
   I need every field in that list to exist.
2. **I6 — the `Draw` overload** in §6.3: `count` and `excludeGroups`. This is the only new code the lane
   needs, and it must be a parameter on the existing function, not a second function.
3. **I6 / I13 — the orphan-instance sweep.** §9.3 and §11.6. An unequipped, player-owned item must be
   reachable. Either a bag row counts as a reference for the sweep, or player-owned instances are exempt.
   **Nothing in this lane can ship until this is resolved.**
4. **I9 — the cost vocabulary and the faucet.** Five spend shapes: a soft currency (souls exist), a
   rarity-gated material (`shard.{rarity}` exists), a focus material (`essence.{element}` exists), a
   **catalyst** for Imprint (does not exist), and a **recall token** (does not exist). Also: the spend and
   the log append must share one transaction, and the idempotent `(reason, correlationId)` semantics of
   `TrySpendSouls` must survive. And per §11.1 — the shard faucet rate is the real brake, not the soul
   price.
5. **I1 — two rulings.** (a) Which `pool_rolls` is authoritative when `ContainerRow.PoolRolls` and
   `RarityRow.PoolRolls` disagree (§4.2). (b) Whether a rarity-change operation exists at all; §3.1 argues
   it is not a reroll, and if it exists it belongs to I1 and I12.
6. **I8 — the pool must be authored for this.** A focused Reforge (§10.2) draws inside one group, so
   **each group needs more than one tier row inside the window** or the operation has nothing to draw. I
   also need the expected count of drawable groups per rarity — my cost examples assume roughly
   `2 × pool_rolls`, and if it is closer to `pool_rolls` the anchor multiplier is mispriced.
7. **I8 / I12 — the disabled-atom defect** in §4.2: the shared draw filters on `weight > 0` but never on
   `AtomRow.Enabled`, so a disabled atom is drawable and the resulting instance is refused at bind. The
   fix belongs in the shared draw.
8. **I3 — confirm implicits are the fixed core.** §8 depends on implicits being `effect_container_atom`
   rows and never `effect_container_pool` rows. If any implicit is authored into the pool, it becomes
   rerollable and I3's design axis quietly disappears.
9. **I12 — one draw function, shared.** §11.4. If the generator forks it, the impossible-item guarantee
   stops being structural.
10. **I4 — confirm sockets are untouched.** A reroll must not change socket count and must not clear
    inserts. If socket count is ever a pool draw rather than a base-type or rarity property, tell me and
    I will exclude that group explicitly.
11. **I5 — confirm set membership is a container tag**, not an affix. If it is ever a drawn atom it lands
    in my pool and becomes rerollable, which would let a player reroll their way into a set.
12. **I11 — confirm no gate interaction.** A reroll does not change `level_req` (a container column), so
    equip gating should be unaffected. I need that confirmed rather than assumed, because §9.3 re-runs the
    bind gate after a durable-binding reroll and a surprise there rolls the transaction back.
13. **Effect-atom program (E-stream) — two reason codes.** `NotRerollable` and `RerollLocked` (§7.3). The
    closed list moves 33 → 35 and `AtomKindRegistryTests.cs:33` moves 34 → 36.
14. **E9 / power — a nice-to-have, not a dependency.** A before/after power delta on the reroll preview
    would make *"is this better?"* answerable without a spreadsheet. `power_json` is nullable (SC9) and
    this lane ships without it.

---

## 13. Open questions for the owner

1. **The anchor curve.** I picked `2^K`, so anchoring 5 of 6 costs 32× base. `1.6^K` (≈10× at K=5) is the
   gentler alternative; a flat 4× regardless of anchor count is the flattest. This one number decides how
   long the endgame is.
2. **Does the soul cost cap?** I picked a 4× ceiling so no item ever becomes unimprovable. Uncapped
   escalation is the stronger brake on reroll-until-perfect and the stronger cause of "nobody uses it".
3. **Should Recall exist at all?** It is the one mechanism in this document that softens "outcomes can be
   worse", which the owner explicitly asked for. I priced it scarce rather than cutting it; cutting it is
   defensible and simpler.
4. **Should Imprint exist?** A guaranteed path removes the worst player experience and also removes the
   best story. If it stays, is the floor right at `min_tier` and minimum value, or one step above?
5. **Where does the bench live?** SC8 says every mechanic must work with the PvZ game closed, which this
   does. But may a player reroll *during* a lawn session? §9.3 says no while a match is live. That is my
   call and it is reversible.
6. **Roster-scale economy.** The ideal's §8 question lands hard here: twenty demons × twelve slots is 240
   items. At these prices, rerolling is a thing you do to two or three items, ever. If the intent is that
   most equipped gear gets optimised, every number in §5 is an order of magnitude too high. **This should
   be answered before I9 fixes costs.**
7. **Byte-reproducibility after mutation** (§6.5). Accept "auditable, and reproducible only until the
   catalog moves", or fund a catalog archive so historical replay is real? The first is free; the second
   is a real cost with a real payoff for support and for goldens.

---

## 14. Design-gate checklist

```
[x] I identified the subsystem this touches — effect-atom container/instance/binding, the
    souls ledger, the materials table.
[x] I read the required reading in enrichment-contract §5, this session: item-ideal.md,
    the contract, definitions.md (§1 §2 §4 §5 §6 §10), spec-container-schema.md,
    spec-instance-and-binding.md, atom-family-library.md.
[x] Every factual claim about the repo cites file:line.
[x] I verified claims against CODE, not comments — Instantiator.Draw, Instantiator.Freeze,
    ContainerValidator, AtomRejection, BindGate, RpgStore.AtomInstances DDL and the orphan
    sweep, RpgStore.Souls.TrySpendSouls, DemonMaterialCatalog were all opened.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no test suite was run.** The
    reason-code count (34 today) is read from AtomKindRegistryTests.cs:33, not executed. The
    orphan-sweep defect in §9.3/§11.6 is read from the SQL, not reproduced against a database.
    Both should be run before either is used to justify a build decision.
[x] Nothing contradicts an enrichment-contract §2 rule. SC1 holds (every reroll outcome is
    atoms in a container). SC2 holds (no new kind). SC4 holds (units stated, per-mille
    integers). SC5 is addressed head-on in §6. SC6 holds (§9). SC7 holds (§7.2 names three
    consumers). SC8 holds (§13.5). SC9 holds (§12.14 wants power, does not depend on it).
[ ] Corrections propagated to map, plan, and tasks. **Gap: no item map, plan, or task list
    exists yet** — reconciliation into the ideal is a single pass after all lanes land.
```
