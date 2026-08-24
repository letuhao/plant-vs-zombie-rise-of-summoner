# Lane I6 — enhancement (+X levels), and the instance-mutation model

## 1. Status

**Lane I6 SSOT, drafted 2026-08-22.** Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Two halves. **Part A** is the instance-mutation model — the shared answer to "how does an item change
after its rolls are frozen", which I4, I7 and I9 adopt (contract cut #6, SC5). **Part B** is enhancement
itself. Part A is written first because three other lanes are blocked on it.

Every number below is **illustrative, not balanced**. Recalled facts about shipped games are marked
*(recalled, unverified)* and must be re-checked before any of them reaches a spec.

---

## 2. Scope

**This lane owns**

- The **instance-mutation model**: how an `effect_instance` whose `OnInstantiate` rolls are frozen can
  legally change afterwards, and how the reproduction contract survives it.
- The `effect_instance_op` log, its ordering rules, and the replay law.
- The `op_kind` namespace that I4, I7 and I9 draw from.
- **Enhancement**: what `+X` does to an item, its risk model, its cost curve shape, its caps, its
  transfer and undo rules, its display, and its rejections.

**This lane does NOT own**

| Not mine | Whose |
|---|---|
| Re-drawing affix values or affix identities | **I7** (reroll) — adopts this lane's mutation model |
| Sockets, inserts, socket geometry | **I4** — adopts this lane's mutation model |
| The material vocabulary and what things cost | **I9** — this lane expresses costs in I9's terms |
| The rarity ladder and its ordinals | **I1** — this lane *proposes* an `enhance_cap` per rung; I1 registers it |
| Base types, implicits, item level | **I3** — this lane asks base types to declare an enhancement track |
| The affix pool and tier bands | **I8** |
| Equip roles and slot names | **I2** — transfer is gated on I2's role id |
| Turning a loot event into an instance | **I12** |
| Bags, comparison UI plumbing | **I13** |

---

## 3. The model

### 3.1 Part A — the mutation model, in one page

`effect_instance` freezes its `OnInstantiate` rolls, and the atom layer guarantees

> same `(container_id, catalog_revision, roll_seed)` ⇒ identical `effect_instance_atom` rows
> ([definitions.md](../effect-atom/definitions.md) §5).

Enhancement, reroll and socketing all change the item after that moment. The contract's instruction
(SC5) is that an item's current state must be derivable from its origin seed **plus an ordered, recorded
list of operations** — never a silent re-roll. Concretely:

```text
  origin  =  instantiate(container_id, origin_catalog_revision, roll_seed)     -- pure, contractual
  head    =  replay(origin, ops[1..n])                                          -- pure, transcript
  invariant:  hash(head)  ==  effect_instance.state_hash
```

Three moving parts:

1. **The head is materialised.** `effect_instance_atom.values_json` always holds the item's *current*
   numbers. Every existing reader — the bind gate, the compiler, the runner, the tooltip — reads exactly
   what it reads today. Nothing composes anything.
2. **The origin is recoverable, not stored.** It is a pure function of three columns already on the
   instance (plus `origin_catalog_revision`, which E6 does not store yet — §9.10). We keep the *ability*
   to recompute it, and cache it for display.
3. **Every mutation appends one row** to `effect_instance_op` and bumps `mutation_seq` and `state_hash`.
   The log is append-only. Undo is a new op, never a delete.

The extended reproduction contract, which degenerates to the existing one when `n = 0`:

> same `(container_id, origin_catalog_revision, roll_seed, ops[1..n])` ⇒ byte-identical instance.

### 3.2 Replay is a transcript, not a simulation

This is the load-bearing decision in Part A, and it is easy to get backwards.

Each op records the **materialised result** it produced — the exact per-atom value deltas, the rows it
appended, the rows it suppressed — in `result_json`. Replay applies those recorded deltas. It does
**not** re-run the enhancement formula, and it does **not** re-roll the success check.

The op *also* records `op_seed`, `rules_version` and `catalog_revision`, so an auditor can independently
re-derive what the op *should* have produced and compare. That is a second, optional level:

| Level | What it does | Depends on | When it runs |
|---|---|---|---|
| **Replay** | rebuild the head from origin + recorded deltas | nothing but the log | any load, any test, always exact |
| **Audit** | re-derive the outcome from `op_seed` + the archived rules and check it matches | the archived `item_enhance_rules` rows | anti-cheat, bug forensics, never on a load path |

Why this split matters: the odds table and the enhancement curve are **balance data and will be
rebalanced**. If replay re-simulated, a nerf to the success table would retroactively un-succeed
attempts players already paid for, and a re-tune of the scalar would silently change every item in every
save. A transcript cannot do that. **A rebalance must never reach backwards into an item a player already
owns**, and the only way to guarantee that is to stop replay from consulting the rules at all.

### 3.3 Part B — what enhancement is

Enhancement is a **repeatable, mostly deterministic upgrade track** on one equipped item: `+1`, `+2`, …
up to a cap. It has two components and no third:

1. **The affix scalar** — each level adds **+20‰ increased magnitude** to the item's *rolled* affixes,
   computed **from the origin value, not compounded**. At `+10` a rolled affix reads 1.200× its drop
   value; at `+20`, 1.400×. Implicits are never scaled (they are I3's statement of what the base type
   *is*), and milestone atoms are never scaled (they are fixed).
2. **Milestone atoms** — at `+4 / +8 / +12 / +16 / +20` the base type's authored **enhancement track**
   grants one fixed atom from a reserved family. These are **identical for every copy of that base
   type regardless of rarity**, which is what makes enhancement an overlap engine rather than a rarity
   multiplier (OD4, §7.2).

The budget law that sizes both:

> **At its cap, enhancement is worth roughly one rarity rung** — about **2× the item's own `+0`
> magnitude**. Enough that a maxed lower-rung item overlaps the next rung; never enough to clear it.

Nothing here needs a new atom kind, a new attach point, a new trigger, or a new `container_kind`. The
scalar rewrites frozen values on rows that already exist; the milestones append rows pointing at
ordinary catalog atoms. SC1, SC2 and SC3 are satisfied without an exception.

---

## 4. Options considered, and the recommendation

Four decisions. Each lists the alternatives that were genuinely arguable.

### Decision A — where the mutated value lives

| Option | Read cost at bind | Read cost in an inventory list (N items × M ops) | Cost of being wrong |
|---|---|---|---|
| **A1. Rewrite `values_json` in place, keep an op log** | unchanged — one SELECT | unchanged | the head can drift from the log if a replay bug lands, and nobody notices |
| **A2. Freeze origin, store a delta layer, compose on read** | O(M) composition per instance, per equip | **O(N×M) per screen** — 200 items × 30 ops is 6 000 compositions to draw one inventory page | two sources of truth for one number, which is the exact bug class this program exists to remove |
| **A3. Version the instance — each op mints a new `instance_id`** | unchanged | unchanged | `instance_id` churn. Bindings, favourite flags, socket contents and any thin `item` row above the instance all re-point on every `+1`. Row growth is ~6 atom rows × 20 levels per item |

Two seams matter for read cost, and "hot path" is too vague for either. The first is **bind**, which
happens once per equip — A2 is affordable there. The second is the **web inventory listing**, where the
control room draws a bagful of items with their numbers; A2 is not affordable there, and it is the seam
that actually gets hit hundreds of times per screen.

**Pick: A1 — materialised head plus an append-only op log.** The op log is *cold storage*: nothing on
any read path consults it. Its only readers are the item-history UI, the audit tool, and the replay test.

A1's characteristic failure — silent drift between head and log — is the one thing that must not be
left to hope. It is closed by `state_hash` (§5.1), a `ReplayDivergence` rejection (§6), and a suite test
that replays every mutated instance in a fixture database and compares hashes.

### Decision B — how replay works

| Option | Tradeoff |
|---|---|
| **B1. Re-simulate** — replay re-runs the formula and re-rolls from `op_seed` | the log is small (seed + params only). But every rebalance rewrites history, and a formula refactor silently changes every owned item |
| **B2. Transcript** — replay applies recorded deltas | the log is larger (a `result_json` per op). History is immutable under rebalance. Verification of *legitimacy* becomes a separate, optional audit pass |

**Pick: B2.** The size argument is weak — a few hundred bytes per op, tens of ops per item — and the
correctness argument is decisive. See §3.2.

### Decision C — what `+X` actually does

| Option | Effect on rarity | Effect on roll quality | Verdict |
|---|---|---|---|
| **C1. Scale affix magnitudes by a percentage** | rarity-**proportional**: a 6-affix legendary and a 1-affix magic both gain the same *percentage*, so the absolute gap widens. Produces no overlap | a good roll stays good — the scalar preserves it | half right |
| **C2. Flat increments identical for all copies of a base type** | rarity-**neutral**: worth proportionally far more to a low-rarity item. Produces overlap | flattens roll quality if it dominates | half right |
| **C3. Unlock extra rolled affixes at milestones** | strong, and it *re-opens the freeze problem* — a new roll after instantiate is precisely what SC5 forbids doing silently, and re-drawing affix identity is **I7's** | — | rejected |
| **C4. Mix: proportional scalar + rarity-neutral fixed milestones** | both effects, deliberately weighted | preserved by the scalar half | **picked** |

**Pick: C4.** C1 alone leaves OD4's overlap entirely to the roll distribution; C2 alone makes a lucky
roll irrelevant; C3 belongs to another lane and breaks the freeze. The mix gives OD4 a second overlap
source (the milestone half) while keeping "a better roll stays better" (the scalar half).

C3 is rejected but its *feel* is kept: milestones are dramatic, they just are not random. What the
player does not get at a milestone is a slot machine. What they get is a named, previewable atom they
could read on the base type before spending anything.

### Decision D — the risk shape

| Option | Prior art *(recalled, unverified)* | Verdict |
|---|---|---|
| **D1. Guaranteed success, cost rises** | Diablo 4 masterworking, S4 onward — resettable, guaranteed, and essentially uncontroversial | safe, and slightly inert |
| **D2. Success chance falls; failure costs only materials** | Lost Ark honing without the artisan bar | a gamble with no floor — §8.4 |
| **D3. D2 plus a visible pity floor** | Lost Ark's artisan's energy: failures accumulate toward a guaranteed success | tension with a bounded worst case |
| **D4. Failure can drop a level** | MapleStory starforce below the destruction band | real stakes; recoverable |
| **D5. Failure can destroy the item** | MapleStory starforce above +15; the D4 S4 tempering brick in a different shape | **rejected — §8.1** |

**Pick: D3 as the spine, with a narrow D4 band at the very top, and D5 never.** Three bands:

| Band | Levels | Success | Failure |
|---|---|---|---|
| **Safe** | +1 … +8 | 1000‰ | — |
| **Risk** | +9 … +14 | 950‰ falling to 600‰ | materials spent, level unchanged, pity +80‰ |
| **Peril** | +15 … cap | 500‰ falling to 200‰ | as above, and from **+17** a failure may drop **one** level unless a `ward.enhance` is loaded |

There is **no destruction outcome in the model at all** — not as an enum value, not as a reason code.
A code nothing emits is the "lie in a table" defect this repo already has scar tissue from
([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §8a), and reserving one invites a later
session to wire it up. It is not reserved.

---

## 5. Data shape

### 5.1 Columns on tables that already exist

`effect_instance` ([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)) —
**reused**: `instance_id`, `container_id`, `roll_seed`, `created_utc`, `origin`. **New:**

| Column | Type | Notes |
|---|---|---|
| `origin_catalog_revision` | INT | **the revision the instance rolled under.** Not stored today, which means origin reproduction after any import is currently unverifiable. This is a defect in E6, not a new feature — see §9.10 |
| `enhance_level` | INT NOT NULL DEFAULT 0 | the `+X`. One writer, this lane |
| `enhance_pity_permille` | INT NOT NULL DEFAULT 0 | accrued pity at the *current* level; reset to 0 on success |
| `mutation_seq` | INT NOT NULL DEFAULT 0 | count of applied ops; equals `max(op_seq)`. Hard cap **4096** (§8.5) |
| `state_hash` | TEXT | hash of the head's atom rows — see below |

`effect_instance_atom` — **reused**: `instance_id`, `atom_id`, `seq`, `values_json`, `power_json`.
**New:**

| Column | Type | Notes |
|---|---|---|
| `overrides_json` | TEXT NULL | an **instance-level value-spec override**, same shape and same validation as `effect_container_atom.overrides_json` (E5). **Load-bearing**: without it no `OnApply` affix can ever be enhanced or rerolled, because `values_json` holds only frozen `OnInstantiate` results and an unresolved range has nowhere else to live. Resolution order becomes atom params → container override → **instance override** |
| `suppressed` | INT NOT NULL DEFAULT 0 | a row an op has retired. Never binds. Kept so `seq` is never renumbered |
| `origin_values_json` | TEXT NULL | **a cache, not the authority.** Populated at first mutation, holds what the drop rolled. The authority is `instantiate(container_id, origin_catalog_revision, roll_seed)`; a test asserts cache == recompute |

**`state_hash`** uses the canonical form already specified in [definitions.md](../effect-atom/definitions.md)
§8: `SHA256` over length-prefixed columns, **sort-then-concatenate, XOR-fold banned**, over every
`effect_instance_atom` row of the instance including `suppressed` and `overrides_json`. One algorithm in
the tree, not two.

### 5.2 `effect_instance_op` — new, and shared with I4, I7, I9

| Column | Type | Notes |
|---|---|---|
| `instance_id` | TEXT FK | `ON DELETE CASCADE`, same as bindings |
| `op_seq` | INT | 1-based, **dense, no gaps**. PK `(instance_id, op_seq)` |
| `op_kind` | TEXT | closed enum, §5.3 |
| `op_seed` | INT | the seed this op consumed. Always recorded, even when the op consumed no randomness |
| `params_json` | TEXT | the player's/system's *inputs* — target level, which affix, which insert, whether a ward was loaded |
| `catalog_revision` | INT | the catalog in force when the op ran |
| `rules_version` | INT | the version of the op's own rule table (`item_enhance_rules`) |
| `outcome` | TEXT | `success` \| `fail-nothing` \| `fail-level-lost`. **Recorded, never recomputed** (§3.2). There is no destroy outcome |
| `result_json` | TEXT | the **materialised** effect: per `(atom_id, seq)` the new `values_json` / `overrides_json`, rows appended, rows suppressed. This is what replay applies |
| `cost_json` | TEXT | what was spent, in I9's vocabulary, written in the same transaction that debits it |
| `applied_utc` | TEXT | |

Index: `(instance_id, op_seq)` is the PK and the only access pattern.

### 5.3 The `op_kind` namespace — reserved here, claimed by lane

| `op_kind` | Lane | Meaning |
|---|---|---|
| `enhance` | **I6** | one `+X` attempt, success or failure |
| `enhance-transfer-out` / `enhance-transfer-in` | **I6** | the donor and recipient halves of a transfer (§7.4) |
| `restore` | **I6** | administrative rollback to a recorded `op_seq` |
| `reroll-value` | I7 | re-draw a magnitude, same affix identity |
| `reroll-affix` | I7 | replace an affix identity — suppress + append |
| `socket-add` | I4 | add a hole |
| `socket-insert` / `socket-remove` | I4 | fill or empty a hole |

Adding a value is a reviewed change against this document, the same way adding a `container_kind` is a
reviewed change against E5.

### 5.4 New content tables, each with its consumer named (SC7)

| Table | Columns | Author | Consumer |
|---|---|---|---|
| `item_enhance_track` | `base_type_id`, `at_level`, `atom_id`, `seq` | **I3** (base types) | the `enhance` op |
| `item_enhance_rules` | `rules_version`, `level`, `success_permille`, `pity_permille`, `fail_mode` | balance | the `enhance` op, and the audit pass. **Rows are never deleted** — audit needs the archived version |
| `item_enhance_cost` | `rarity`, `level`, `material_id`, `qty` | **I9** (offered) | the `enhance` op |

No table here is a free string field pretending to be data: each is read by the enhance op and by nothing
else, and adding a row to any of them changes behaviour with no new code.

### 5.5 Reserved atom families for milestones

Milestone atoms must not collide with the item's rolled affixes. The one-mod-per-`(family_id, variant)`
rule ([definitions.md](../effect-atom/definitions.md) §4) exists so a rolled item never reads
`+62 hp / +10 hp / +25 hp`, and a naive milestone track produces exactly that.

**Rule: milestone atoms come from a reserved family space no affix pool may draw from**, illustratively
`atom.enhance-vigor` (`stat.modify` maxHp Flat), `atom.enhance-edge` (`stat.modify` atk Flat),
`atom.enhance-aegis` (`shield.grant`, omni, `OnSpawn`). Five tiers each, ordinary catalog rows, no new
kind.

**Rule: at most one milestone per enhancement family per track.** A later milestone in the same family
**replaces** the earlier one — suppress the old row, append the new one. That is the same suppress-and-
append rule the contract gives I7, dogfooded here so it is proven before another lane depends on it.

The tooltip consequence is good: the player sees one clean **Enhancement** block, not a pile of stacked
lines.

### 5.6 Where the code would live

`effect_instance_op` DDL and IO in `src/FusionRpg.Data/Sqlite/` beside `RpgStore.AtomInstances.cs`
(`guard-dal.ps1` — no SQL outside `FusionRpg.Data`). Replay, the scalar, the odds roll and the caps are
pure and belong in `src/FusionRpg.Core/`. The op's randomness derives from the existing owned PRNG:
`SeededRng.DeriveStream(op_seed, "item.enhance")` then `NextPerMille()`
(`src/FusionRpg.Core/Battle/SeededRng.cs:31`, `:60`) — one named stream per op kind, so adding a roll in
one op never shifts another's sequence. `System.Random` never touches this path, for the same reason it
never touches battle.

### 5.7 The one place the instance stops being a projection of its container

Today an instance's atom set is exactly *fixed core + pool draw*. Milestones, inserts (I4) and affix
replacement (I7) all append rows that are in **no** container. That is schema-legal —
`effect_instance_atom.atom_id` is an FK to `effect_atom`, not to the container's list — but any
validator or migration that assumes `instance_atoms ⊆ container_atoms ∪ pool` **must be relaxed**, and
relaxed to a stronger check, not a weaker one:

> every `effect_instance_atom` row is either produced by `instantiate(...)` or justified by a row in
> `effect_instance_op`. A row justified by neither is a defect.

That check is exactly what the `state_hash` replay test performs.

---

## 6. Validation and reason codes

### 6.1 Reused from the closed list of 33

| Bad input | Code |
|---|---|
| Instance contains a disabled (`enabled = 0`) atom; enhancing would deepen an investment in retiring content | `StaleInstance` |
| The player's progression band does not permit the target level | `LevelTooLow` — the same meaning E6 gives it at the bind gate (owner's level below what the content requires) |
| A scaled magnitude does not fit `int` | `MagnitudeOverflow` |
| `params_json` on an op is malformed | `BadParamValue` |
| A milestone track names an atom that does not exist | `UnknownAtom` |
| A milestone atom shares `(family_id, variant)` with a live row on the instance | `DuplicateAtomInContainer` |
| An instance-level `overrides_json` is not a well-formed value spec | `BadValueSpec` |
| An instance-level override would change the atom's `kind_id` | `OverrideChangesKind` |

### 6.2 Proposed new codes

Seven. Three of them (`OpSequenceGap`, `ReplayDivergence`, `OriginRevisionUnavailable`) belong to the
**mutation model** and therefore serve I4, I7 and I9 as well as this lane — they are not seven codes for
one mechanic.

| New code | Fires when | Why nothing existing fits |
|---|---|---|
| `EnhanceCapReached` | target level exceeds `cap(item)` (§7.3) | `LevelTooLow` is about the *owner*; this is about the *item* |
| `EnhanceNotSupported` | the base type has no enhancement track and the instance has no scalable rolled affix | distinct from a malformed request; the request is well-formed and simply has nothing to act on |
| `OddsNotAcknowledged` | the odds hash the client acknowledged ≠ the odds the server computed (§7.7) | the disclosure gate is a contract, not a UI nicety |
| `OpSequenceGap` | an op arrives with `op_seq ≠ mutation_seq + 1` | concurrency, or a replay defect. Dense ordering is the model's premise |
| `ReplayDivergence` | replaying origin + ops does not reproduce `state_hash` | the characteristic failure of a materialised head. It must be loud |
| `OriginRevisionUnavailable` | reproduction is requested against an archived `catalog_revision` the catalog no longer holds | an honest degradation of the audit path, distinct from a corrupt one |
| `TransferRoleMismatch` | a transfer's target role ≠ the donor's role, or item levels differ by more than the window | |

`InsufficientMaterials` is **not** proposed here — that belongs to I9, and this lane declares that it
uses I9's code rather than minting a second one (§9.3).

### 6.3 Transactional rule

An op row, the material debit, and the head rewrite **commit together or not at all**. A spent cost with
no op is theft; an op with no cost is duplication. There is no partial enhancement, the same way E4/E5
have no partial row.

---

## 7. Worked examples with real numbers

*Illustrative, not balanced.* Units per SC4: primary-channel magnitudes in game units, ratios in integer
per-mille, shield capacity in game units (hp-equivalent absorb).

### 7.1 An item at +0, +10 and +20

**`plate-helm`**, humanoid `head` (role *head-protective*, I2), **item level 64**, **Epic**, 5 rolled
affixes. Scalar 20‰/level, applied to origin, non-compounding, rounded half away from zero.

Enhancement track authored on the base type by I3:

| Milestone | Atom | Effect |
|---|---|---|
| +4 | `atom.enhance-vigor.t1` | +10 hp |
| +8 | `atom.enhance-edge.t1` | +6 atk |
| +12 | `atom.enhance-aegis.t1` | 60 hp shield `OnSpawn` |
| +16 | `atom.enhance-vigor.t3` | +35 hp — **replaces** the t1 |
| +20 | `atom.enhance-edge.t3` (+18 atk) and `atom.enhance-aegis.t3` (180 hp shield) | both replace their t1 |

| Line | +0 | +10 (×1.200) | +20 (×1.400) |
|---|---|---|---|
| implicit `might` t1 — **never scaled** | +8 atk | +8 atk | +8 atk |
| affix A `vitality` t4, rolled 62 | +62 hp | +74 hp | +87 hp |
| affix B `might` t3, rolled 18 | +18 atk | +22 atk | +25 atk |
| affix C `fortitude` t2, rolled 80‰ increased maxHp | +80‰ | +96‰ | +112‰ |
| affix D `searing_strike` t3, `OnApply` 100–200 fire | 100–200 | 120–240 | 140–280 |
| affix E `regeneration` t3, 14 hp / 3000 ms | 14 | 17 | 20 |
| milestone `enhance-vigor` | — | t1: +10 hp | t3: +35 hp |
| milestone `enhance-edge` | — | t1: +6 atk | t3: +18 atk |
| milestone `enhance-aegis` | — | — | t3: 180 hp shield |
| **total flat hp** | **62** | **84** | **122** |
| **total flat atk** | **26** | **36** | **51** |
| **increased maxHp** | **80‰** | **96‰** | **112‰** |
| **shield on spawn** | — | — | **180 hp** |

`+20 / +0` is **1.97× on hp and 1.96× on atk** — the budget law (§3.3) landing where it says it does.

Two things worth reading off this table:

- **Affix D is only enhanceable because of `effect_instance_atom.overrides_json` (§5.1).** Its range is
  `OnApply` — deliberately *not* frozen at instantiate — so there is no `values_json` entry to rewrite.
  Without the new column, on-hit affixes would be permanently enhancement-immune, which is a balance
  distortion nobody chose.
- **Affix E gains only the scalar** (14 → 20, ×1.43) because no milestone touches regeneration. A channel
  the base type's track ignores grows slower than one it favours. That asymmetry is intentional — it is
  what makes an enhancement track part of a base type's identity rather than a flat tax — and it must be
  visible in the track preview (§7.7).

### 7.2 Rarity overlap — the OD4 mechanism this lane contributes

Same base type, three rungs, each at its own cap. Milestone contributions are identical for all three;
only the scalar's *base* differs.

| Rung | affixes | `+0` flat hp | cap | scalar at cap | milestone hp | **hp at cap** |
|---|---|---|---|---|---|---|
| Rare | 4 | 46 | **+12** | ×1.24 → 57 | +10 | **67** |
| Epic | 5 | 62 | **+16** | ×1.32 → 82 | +35 | **117** |
| Legendary | 6 | 90 | **+20** | ×1.40 → 126 | +35 | **161** |

A maxed **Rare (67)** beats an unenhanced **Epic (62)**. A maxed Epic (117) does not reach a maxed
Legendary (161). That is OD4's overlap delivered by a mechanism, not asserted: the milestone half is
worth proportionally far more to a small affix budget than a large one, so **enhancement helps a
low-rarity item more** — a designed catch-up property, and the reason rarity caps rise with the ladder.

I8's roll distribution is the *other* overlap source. This lane assumes it carries part, not all
(§9.1).

### 7.3 Caps — soft, not hard (AGENTS.md "no hard progression ceilings"; reconciled 2026-08-24)

**Pre-build correction.** An earlier draft clamped `ilvl_cap` to a hard `[4, 20]` and stopped the
`rarity_cap` table at Legendary +20 "because that felt like enough." Neither is a real constraint —
the Peril band's falling success rate and from-+17 level-drop risk (§3.1) is *already* the soft cap
this lane needs: pushing further gets steadily less worth it, without a numeric wall forcing the
stop. A hard ceiling on top of that risk curve is a second, redundant, unexplained cap of exactly the
kind PS-8 exists to catch.

```text
cap(item) = min( rarity_cap[rarity], ilvl_cap(item_level), progression_cap(player) )
ilvl_cap(ilvl) = max(4, 4 + ilvl / 4)          -- integer division; floor only, no ceiling
```

Proposed `rarity_cap`, offered to **I1** as an append-only column on the existing `rarity` table
alongside `ordinal`. Rung names are I1's; this is the shape, not the vocabulary. The table is
**open-ended**: a future rarity rung above Legendary/Unique adds a higher row, the same way the
power ladder's own rung list grows — it is not a hard stop at +20.

| Rung (illustrative) | cap |
|---|---|
| Normal | +4 |
| Magic | +8 |
| Rare | +12 |
| Epic | +16 |
| Legendary / Unique | +20, and whatever a future rung above it adds |

`ilvl_cap` scales with item level and does not top out: ilvl 20 → +9, ilvl 40 → +14, ilvl 64 → +20,
ilvl 128 → +36, without an artificial floor on how far the formula can go. `progression_cap` is a
campaign band; this lane defaults it to a high, effectively-non-binding value because progression is
not its to decide (§10.3) — a real progression gate, if one is ever wanted, is a configurable soft
cap owned by the progression system, not a number this lane hardcodes.

**A cap lowered by a later rebalance grandfathers.** An item already above the new cap keeps its level
and cannot go higher. Retroactively stripping levels is the same defect as retroactively un-succeeding an
attempt (§3.2).

### 7.4 Transfer — one-directional and lossy

An `enhance-transfer-out` op on the donor and an `enhance-transfer-in` op on the recipient, applied in
one transaction.

- The recipient gains `floor(donor_level × 700 / 1000)` levels, then clamped to its own cap.
- Gate: recipient `role` == donor `role` (I2's stable role id, not a display name) **and** item levels
  within ±8. Otherwise `TransferRoleMismatch`.
- The donor drops to `+0`. Its milestone rows are suppressed; its scalar is recomputed from origin.
- Cost: a dedicated I9 material.

Worked: a `+16` Epic helm donates to a freshly dropped Legendary helm of the same role, ilvl 64 →
`floor(16 × 0.7) = 11`, under the Legendary cap of 20, so the recipient starts at **+11** and the donor
returns to `+0`.

**Why transfer exists:** without it, enhancement punishes finding better loot — you keep the worse item
because it is the one you paid for. That is the tax failure mode (§8.2) in its purest form.
**Why it is lossy:** a lossless transfer turns `+X` into a portable currency, the item becomes a
disposable carrier, and the decision disappears.

### 7.5 The cost curve, in I9's vocabulary

Today's shipped material vocabulary is `essence.{element}` (6 concrete elements) and `shard.{rarity}`
(common/rare/epic/legendary) in `rpg_demon_materials(player_id, material_id, qty)`
(`src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:15-20`,
`src/FusionRpg.Data/Sqlite/RpgStore.cs:520`), plus the soul ledger
(`rpg_soul_balances` / `rpg_soul_ledger`, `src/FusionRpg.Data/Sqlite/RpgStore.cs:440`). Costs are
expressed in those three today and move to whatever I9 lands.

The table is **authored data**, not a formula — SC4 bans floats in content. These authoring guidelines
generated it: `shard = ceil(L^1.6 / 2)`, `essence = ceil(ilvl × L / 40)`, `souls = 50 × L`.

For an **Epic** item at **ilvl 64**:

| Level | `shard.epic` | `essence.fire` | souls |
|---|---|---|---|
| +1 | 1 | 2 | 50 |
| +4 | 5 | 7 | 200 |
| +8 | 14 | 13 | 400 |
| +12 | 27 | 20 | 600 |
| +16 | 43 | 26 | 800 |
| +20 | 61 | 32 | 1000 |

**Cost is per attempt and is spent whether or not the attempt succeeds.** Two totals matter, and the
second is the honest one:

| To reach | `shard.epic` if every attempt succeeded | **expected**, including failures and pity |
|---|---|---|
| +8 (end of the safe band) | 52 | **52** — the safe band cannot fail |
| +12 | 140 | ~215 |
| +16 | 280 | ~450 |
| +20 | 495 | **~960** |

The shape to notice: **the safe band is 52 of ~960 — about 5% of the ladder's total cost.** Gearing a
roster to `+8` is affordable; taking one item to `+20` costs eighteen times as much as taking it to
`+8`. That split is deliberate and it is the answer to the roster-scale problem in
[item-ideal.md](../item-ideal.md) §8.

**Where the curve stops.** Hard stop: the cap — an attempt above it is `EnhanceCapReached`, not a priced
option. Soft stop, and the more interesting one: marginal power per level is roughly flat (a constant
20‰ plus lumpy milestones) while attempt cost grows superlinearly and expected cost grows again on top of
that. So for a rational player the ladder self-terminates well before the cap, at the point where the
next `+1` costs more than upgrading a *different* item. **The ladder is a breadth-versus-depth
allocation problem across a roster, not a number to max.** That is the intended shape, and it only holds
if I9 gives `shard.{rarity}` something else to buy (§9.4).

### 7.6 Odds, and the pity floor

Success chance before pity, from `item_enhance_rules`:

| Level | ‰ | Level | ‰ | Level | ‰ |
|---|---|---|---|---|---|
| +1…+8 | 1000 | +12 | 780 | +17 | 350 |
| +9 | 950 | +13 | 700 | +18 | 290 |
| +10 | 900 | +14 | 600 | +19 | 240 |
| +11 | 850 | +15 | 500 | +20 | 200 |
| | | +16 | 420 | | |

**Pity: every failure at a level adds +80‰ to that level's chance, persisted on the instance, reset on
success.** At the worst level (200‰) success is guaranteed by the **11th** attempt, and the expected
number of attempts is **3.1**. The in-tree precedent is already shipped, already persisted and already
visible in the UI — `SummonRoller`'s pity counters
(`src/FusionRpg.Core/Demons/SummonRoller.cs:6`).

**Protection.** One item, `ward.enhance` (I9). Consuming one converts a `fail-level-lost` into a
`fail-nothing` for that attempt. It does **not** raise the success chance. Protection that improves odds
is a paywall on the core mechanic; protection that removes only the downside is insurance, and insurance
is a legitimate material sink. It is also strictly optional, because level loss exists only from +17.

### 7.7 Display

The level is a **name prefix**: `+12 Plate Helm of the Ember`. Prefix, because item lists are
left-aligned and the left edge is what a player scans *(D2 and MapleStory both prefix — recalled,
unverified)*.

Each scaled affix line shows **both numbers**: `+87 hp` with `62 rolled · +25 enhanced` beneath. This is
not decoration — the mutation model keeps the origin derivable specifically so the UI can show it, and
showing it is what stops enhancement from erasing the identity of a good roll.

Milestone atoms render in their own **Enhancement** block, each labelled with the level that granted it,
and the next one greyed out with its level — so the track is legible *before* the player commits, and
§7.1's "affix E gains only the scalar" asymmetry is visible rather than discovered.

**The odds panel, and the gate behind it.** No attempt may be committed from a screen that does not show
all six of:

1. current level → target level;
2. base ‰, **pity already accrued ‰**, effective ‰ — three numbers, not one blended number;
3. **guaranteed by attempt N** — the pity floor as a count, not a bar to interpret;
4. the failure consequence in words — *"materials are spent; the item stays at +16"*;
5. this attempt's cost **and** the expected total to reach the target at the current pity;
6. whether a `ward.enhance` is loaded and exactly what it changes.

This is enforced, not requested: the enhance endpoint requires an `acknowledged_odds_hash` matching the
odds the server computes, and a mismatch rejects with `OddsNotAcknowledged`. A stale or hidden odds
display becomes a rejection instead of a surprise.

### 7.8 The contract for I4, I7 and I9

The short numbered list those lanes need, and nothing else.

1. **One mutation table.** `effect_instance_op`. Do not add a second, and do not mutate an instance
   without appending to it.
2. **Ordering is dense and final.** `op_seq` starts at 1, has no gaps, is applied in order, and is never
   reordered. An out-of-order arrival is `OpSequenceGap`.
3. **Record the result, not the recipe.** Write the materialised deltas into `result_json`. Replay never
   re-runs your formula and never re-rolls your dice (§3.2).
4. **Derive your randomness.** `SeededRng.DeriveStream(op_seed, "item.{op_kind}")`
   (`src/FusionRpg.Core/Battle/SeededRng.cs:31`). One named stream per op kind, so adding a roll in your
   op never shifts anyone else's sequence. Record `op_seed` even when you consumed none.
5. **Claim an `op_kind` from §5.3.** Do not invent a synonym for one that exists.
6. **Never renumber `seq`.** You may rewrite `values_json`, write `overrides_json`, append rows with
   `seq` continuing the numbering, and set `suppressed = 1`. You may not delete a row or renumber one.
   Identity changes are suppress-then-append.
7. **Spend atomically.** Your op row and your material debit commit in one transaction, and `cost_json`
   records what was spent in I9's vocabulary (§6.3).
8. **Rehash and bump.** After your op, recompute `state_hash` and set `mutation_seq = op_seq`. A
   mismatch is `ReplayDivergence` — a defect, not a warning.
9. **Undo is an op.** `restore` appends; it never deletes a prior row. The log is append-only forever.
10. **If your op cannot be expressed as value deltas + appends + suppressions, say so.** That is a
    finding against this model, and the contract says a finding is not a failure.

### 7.9 What happens when the catalog changes underneath

| Change | Effect on an enhanced item |
|---|---|
| An atom is **rebalanced** (its authored band changes) | **The head is untouched.** The item is its frozen-then-mutated values. Origin *re-derivation* is checked against `origin_catalog_revision`, not the current one |
| An atom is **disabled** (`enabled = 0`) | Definitions §6 already says the instance keeps its values and **new binds reject** with `StaleInstance`. This lane adds: **enhancing it also rejects** with `StaleInstance`. The item is already unequippable; taking materials for it would be selling an upgrade to a dead item. Existing bindings are untouched |
| An atom is **deleted** | Forbidden — content is disabled, never deleted (definitions §6). If it happens it is a data defect: the instance is quarantined and reported, never silently repaired |
| A new `catalog_revision` (any import) | **Nothing happens to any item.** `catalog_revision` is monotonic and bumped once per import transaction; it is not a live key on an instance. Only origin re-derivation reads it |
| The **enhance rules** change (odds, curve) | Existing levels stand — they are recorded ops. New attempts use the new `rules_version`. Old rules rows are retained for audit |
| A **cap is lowered** below an item's level | The item grandfathers: keeps its level, cannot go higher (§7.3) |
| `origin_catalog_revision` is no longer held by the catalog | Replay still works — it needs only the log. **Audit** degrades and reports `OriginRevisionUnavailable`. This is an honest limitation, not a silent one |

---

## 8. Failure modes

### 8.1 Bricking an item players spent hours on — the 2024 tempering controversy

*(Recalled, unverified.)* **Diablo 4 Season 4, May 2024, "tempering".** An item carried a small,
fixed number of tempering attempts. Each attempt rolled a random affix from a chosen manual's pool. Burn
every attempt without hitting the affix your build needed and the item was **permanently unusable** — not
destroyed, but bricked, which is worse because it stays in your bag. Perfect Greater Affix items
representing dozens of hours died to a bad sequence. Blizzard raised the attempt counts within weeks and
later added a currency reset, making bricking recoverable.

Three separable things went wrong, and it is worth naming them separately because a design can repeat
any one of them alone:

1. **The loss was terminal.** No currency, no downgrade path, no partial recovery.
2. **It landed at the end of the investment chain** — after the drop luck and after masterworking. Loss
   scaled with sunk cost, which is the single worst shape a risk can have.
3. **The odds were undiscoverable before committing.** Players learned the pool size empirically, by
   losing items.

What in this design prevents each:

| Failure | Prevented by |
|---|---|
| Terminal loss | There is **no destroy outcome** — not in the `outcome` enum, not as a reason code (§4 D). The worst case is a one-level slip above +17, insurable with `ward.enhance` |
| Loss scaling with sunk cost | Pity: **every failure permanently improves the next attempt at that level** and the worst level is guaranteed by attempt 11. Sunk cost buys progress, never nothing |
| Undiscoverable odds | The six-item disclosure and the `OddsNotAcknowledged` gate (§7.7) — enforced server-side, not left to the UI |
| Investment locked to one item | Transfer at 70% (§7.4) |

### 8.2 Enhancement becoming mandatory — a tax, not a choice

If every item must be enhanced to be usable, `+X` is homework and the loot has been devalued into a
substrate for it. Lost Ark honing is the canonical example *(recalled, unverified)*: gear rolls became
secondary and the ladder became the game.

Mitigations here: the safe band is short and cheap (**5% of the ladder's cost**, §7.5) so roster gear can
be `+8` without a decision; marginal power is flat while cost is superlinear so the top of the ladder is
a *choice*; transfer means the choice is not locked to one item; and the budget law caps the whole ladder
at roughly one rarity rung.

**Honest admission: this is the failure mode I am least confident about.** Any always-available upgrade
drifts toward mandatory, and none of the mitigations above actually removes the drift — they only slow
it. The mitigation that works is structural and it is not mine: **`shard.{rarity}` must buy something
that competes with enhancement.** If enhancement is the only sink for the ladder's currency, "spend it or
hoard it" is not a choice and the ladder becomes mandatory by default. That is §9.4, and it is a real
dependency, not a courtesy.

### 8.3 `+X` so strong that base rarity stops mattering

Prevented by the budget law (≈2× at cap, §3.3) plus rarity-scaled caps (§7.3): a rung's cap sits below
the next rung's, so maxed rung N overlaps rung N+1 but never clears it — §7.2 shows the arithmetic. The
proportional half of the design (the scalar) also means a better roll stays better at every level:
enhancement multiplies roll quality, it does not replace it.

### 8.4 A gambling loop with no floor

Prevented three ways. **Pity** bounds the worst case at 11 attempts and states it as a number of
attempts, not a bar to interpret. **The safe band has no gamble at all.** And most importantly:
enhancement has **no random reward** — the only random element is whether you advanced. There is no
"what did I get" moment. A gamble whose prize is known in advance is a cost with variance; a gamble
whose prize is unknown is a slot machine, and that is what C3 (rolled milestones) would have built.

### 8.5 The op log grows without bound

240 equipped items × ~30 ops each is 7 200 rows — nothing. But a retry loop or a broken client could
write millions. `mutation_seq` is hard-capped at **4 096** per instance and exceeding it rejects. Cheap
insurance against a class of bug rather than a class of design.

### 8.6 The materialised head drifts from the log

This is the characteristic failure of Decision A1 and the price paid for zero read cost. It is closed by
`state_hash` on every op, `ReplayDivergence` as a loud rejection, and a suite test that replays **every**
mutated instance in a fixture database and compares hashes — not a spot check. The check is cheap because
replay needs nothing but the log.

### 8.7 A rebalance retroactively destroys items

Prevented structurally by transcript replay (§3.2): the recorded `outcome` and `result_json` mean the
rules table is never consulted on a load path, so no nerf can un-succeed a past attempt and no re-tune
can silently rewrite an owned item. This is the failure this design most deliberately engineers out,
because it is invisible until a player notices their item changed and there is no way to explain it.

### 8.8 Enhancement locks a player out of experimenting

If enhancing is expensive and the level cannot move, players stop trying new builds and the loot loop
stalls. Transfer (§7.4) is the release valve; the 70% ratio is the price of the valve.

---

## 9. What this lane needs from other lanes

1. **I1 (rarity)** — register `enhance_cap` as a column on the existing `rarity` table, append-only
   alongside `ordinal`. Proposal in §7.3 (Normal +4 → Legendary +20); the rung names are yours. Also
   tell me whether OD4's overlap is expected from roll distribution alone or whether enhancement carries
   part of it — **I have assumed I carry part**, and §7.2's arithmetic depends on that assumption.
2. **I3 (base types)** — base types must declare (a) an `item_level`, and (b) an **enhancement track**
   in `item_enhance_track`. A base with no track is scalar-only, which is legal but flat. Please also
   confirm the rule that **implicits are never scaled by enhancement**; if you disagree, the budget law
   in §3.3 has to be re-derived.
3. **I9 (materials and costs)** — own `item_enhance_cost` or tell me where costs live. I need three
   material classes: a rarity-keyed sink (`shard.{rarity}` exists), an element-keyed sink
   (`essence.{element}` exists), and one new insurance item, `ward.enhance`. I also need
   `InsufficientMaterials` as **your** reason code, and the atomic spend-and-append transaction in §6.3.
4. **I9 again, and this one is load-bearing** — §8.2's tax failure mode is only structurally solved if
   `shard.{rarity}` buys something *other* than enhancement. If enhancement is the only sink, the ladder
   becomes mandatory no matter how I tune the curve. Please make sure it is not the only sink.
5. **I7 (reroll)** — adopt `effect_instance_op`, claim `reroll-value` and `reroll-affix`, and use
   suppress-then-append for identity changes (§5.5 shows it working). Confirm you can express a reroll's
   result as value deltas + appends + suppressions. If you cannot, that is a finding against my model and
   I want it early, not after three lanes have built on it.
6. **I4 (sockets)** — adopt `effect_instance_op` with `socket-add` / `socket-insert` / `socket-remove`.
   [item-ideal.md](../item-ideal.md) §11 says socketing "probably needs new schema"; I believe it needs
   **one** new table for socket geometry (which holes exist, which are filled) and nothing more, because
   an inserted gem's atoms can land as appended `effect_instance_atom` rows and then nothing on the bind
   path changes. Tell me if that is wrong.
7. **I2 (equip slots)** — transfer is gated on **role** equality. I need the role to be a stable
   comparable id, not a display name, and I need to know whether a hybrid frame's role ids are the same
   ids as the pure frames' (OD3) — if they are not, transfer across a hybrid is undefined.
8. **I12 (loot → instance)** — set `origin_catalog_revision` when you create an instance. See also #10;
   the column does not exist yet.
9. **I13 (inventory)** — if the item entity turns out to be a thin row above the instance
   ([item-ideal.md](../item-ideal.md) §6.3, still open), the enhancement level must **display** from
   `effect_instance.enhance_level` and never be copied into your row. One writer.
10. **E6 (effect-atom program, not an item lane)** — three additive columns, all nullable or defaulted:
    `effect_instance.origin_catalog_revision`, and `effect_instance_atom.overrides_json` +
    `suppressed`. Two notes. The missing revision column is a **defect in E6 today**, not a feature
    request: `effect_instance` stores `container_id`, `roll_seed`, `origin` and `created_utc`
    ([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)) but not the revision it
    rolled under, so the reproduction contract in definitions §5 names an input the schema does not
    persist and origin reproduction after any import is unverifiable. And `overrides_json` is
    load-bearing for *three* lanes: without it no `OnApply` value can ever be enhanced (§7.1) or rerolled
    (I7), because the instance has nowhere to hold a modified value spec.
11. **E6 again** — relax any validator asserting `instance_atoms ⊆ container_atoms ∪ pool` to the
    stronger op-justified form in §5.7. This is the one place the instance stops being a pure projection
    of its container, and it affects I4 and I7 identically.
12. **I8 (affix pool)** — the reserved enhancement families (`atom.enhance-*`, §5.5) must never be
    drawable by an affix pool. The cleanest place for that is a `poolable` flag on the family, which is
    yours, not a new reason code from me.
13. **E9 (power)** — per SC9 this lane ships without power. What it *would* want: `power_json`
    recomputed on the head after every op, so "is a +12 Rare better than a +0 Epic" has a number instead
    of a table of raw lines. Until then the comparison UI shows lines only, and §7.2's overlap claim is
    argued from magnitudes rather than measured.
14. **The shield stream** — §7.1's milestone atoms use `shield.grant`, and I have read its `maxHp` as
    game units (hp-equivalent absorb) from [shield-system-spec.md](../shield-system-spec.md). Confirm,
    so the display numbers are honest. Note the *family* `shield_capacity` is `stat.derived` and
    therefore quarantined (D6) — the milestone deliberately uses `shield.grant`, which is shipped.

---

## 10. Open questions for the owner

1. **Is `+X` in the item's name or only in its tooltip?** I picked a name prefix (`+12 Plate Helm`)
   because it changes every list in the UI and the left edge is what gets scanned. It is a strong
   aesthetic commitment and I made it without asking.
2. **Should the risk band exist at all?** I argued for a small one (§4 D). The defensible alternative is
   zero risk with a rising cost — which is where Diablo 4 masterworking landed after the tempering
   backlash *(recalled, unverified)*, and essentially nobody complains about it. Choosing zero risk
   deletes §7.6, three reason codes, and the whole odds-disclosure gate. That is a real simplification,
   and it costs the only moment in the ladder that has any tension.
3. **Is there a campaign progression gate on the ladder, and who owns it?** I defaulted
   `progression_cap` to a high, effectively-non-binding value (§7.3, reconciled 2026-08-24 — no hard
   progression ceilings, AGENTS.md) because progression is not this lane's.
4. **The transfer ratio (70%) and the ±8 item-level window** are pure feel numbers with no reasoning
   behind them beyond "lossy but not punitive".
5. **Does enhancement apply to charms (I10) and socket inserts (I4), or only to equipment?** I scoped it
   to equipment. Extending it is cheap mechanically and expensive in balance.
6. **Roster scale** — [item-ideal.md](../item-ideal.md) §8's unanswered question decides whether the safe
   band must be *free* rather than merely cheap. If twenty demons × twelve slots each want `+8`, "5% of
   the ladder" is still 240 × 52 shards.
7. **Should players be able to un-enhance?** I shipped administrative `restore` only, and no player-facing
   undo, on the grounds that a reversible decision is not a decision and there is nothing to protect the
   player from once destruction is off the table. If you want a player-facing un-enhance, it is one more
   `op_kind` and a refund rate.
