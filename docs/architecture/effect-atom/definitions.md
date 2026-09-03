# Definitions — the values the specs left undefined

**Status:** Written 2026-08-22 after a four-way adversarial audit of the module specs (~147 findings, ~37 S1). Roughly a third of those findings were one shape: *a thing referenced everywhere and defined nowhere.* This document pins them. Where a spec and this document disagree, **this document wins** until the spec is rewritten.

Not a spec. No module owns it. It is the shared vocabulary every module spec assumes.

---

## 0. The model, corrected

**Items have no behaviour. Actors do.**

An actor attacks. The attack raises an event. An atom **on that actor's effect list** responds — adding fire damage, applying a status, spawning a body. An item, trait, skill, or species passive is a **source** that put the atom on the list. None of them participates at runtime.

```text
actor ──does──► action (attack / take damage / die / spawn / timer)
                   │
                   └─raises─► event
                                │
      actor's effect list ──────┴──► atoms whose `when` matches respond
                ▲
                └── bindings put atoms here (from items, traits, skills, passives)
```

Three consequences the specs got wrong:

1. **The unit of compile/run classification is the atom, not the binding.** An "item whose atoms split across both paths" is a non-problem — the item was never a runtime unit.
2. **`seq` on a container is authoring order**, not an execution guarantee. Reading a container is stable; execution order is a property of the actor's list.
3. **A binding is bookkeeping** — how an atom arrived and how to withdraw it. It is not an execution unit.

---

## 1. Identity and grammar

| Id | Grammar | Notes |
|---|---|---|
| `kind_id` | `^[a-z]+\.[a-z_]+$` | closed set of 12; not content |
| `family_id` | `^atom\.[a-z0-9]+(-[a-z0-9]+)*$` | one affix concept. **Kebab-case, `atom.` prefix, no underscores** — the family library writes display shorthand (`elemental_power`); the id is `atom.elemental-power` |
| `variant` | `^[a-z0-9]+(-[a-z0-9]+)*$` or `''` | the discriminator within a family — element id, channel, side. Empty string, never NULL |
| `atom_id` | `^atom\.[a-z0-9-]+(\.[a-z0-9-]+)?\.t[1-9][0-9]*$` | **derived, not authored:** `{family_id}[.{variant}].t{tier}` |
| `container_id` | `^(item\|trait\|skill\|species-passive\|patron\|world-buff)\.[a-z0-9-]+$` | prefix must match `container_kind` |
| `curve_id` | `^curve\.[a-z0-9-]+(\.[a-z0-9-]+)*$` | |
| `instance_id` | 32 lowercase hex | generated; see §5 |
| `binding_id` | 32 lowercase hex | generated |
| `owner_key` | see §6 | |

**`atom_id` is computed from its columns and validated against them.** A row whose id does not equal `{family_id}[.{variant}].t{tier}` is rejected (`IdMismatch`). This kills the whole class of findings where an id and its columns disagree, and it makes E14's tier-gap lint trustworthy.

### `effect_atom` key — corrected

**`UNIQUE (family_id, tier, variant)`.** The previous `(family_id, tier)` forbade the generation rule outright: `elemental_power` × 7 element slots (6 elements + `omni`) × 5 tiers is 35 rows over 5 tiers, and the constraint rejected 30 of them.

`variant` is `''` for families with one member (`vitality`, `might`). It carries the element for generated families, and the channel for families like `plating` that write two channels.

---

## 2. Values, rolls, and curves

### Units — non-negotiable

> **⛔ CORRECTED 2026-09-03 (E42 `units-correction`).** This table used to give one row —
> *"Derived-channel magnitudes — resolver points, `AccuracyScale = CritRateScale = 100.0`"* — to every
> derived channel. That was wrong for `combat.power.*` / `combat.defense.*` / `combat.shield.*`, and the
> item program proved it and handed the fix over on 2026-08-22
> ([`item/atom-layer-handoff.md`](../item/atom-layer-handoff.md) §1). It sat uncorrected for eleven days
> because `DESIGN-GATE.md` makes this file win over every spec, so no downstream document could fix it
> by being right — only editing this file closes it. **The decisive negative evidence:**
> `CombatProbabilityPolicy` declares `AccuracyScale`, `CritRateScale`, `CritDamageScale` and `Steepness`
> — **there is no `PowerScale` and no `DefenseScale`**
> (`src/FusionRpg.Core/Stats/Derived/CombatPolicies.cs:10-13`), and `OverlayCombatCalculator` sums
> `(power − defense)` straight into `weightedDelta` with no sigmoid anywhere in the call path
> (`src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs:84-89,104`). So `+10 fire power` is **+10
> damage** — the peer of `+10 hp`, not a tenth of it under a sigmoid.

| Kind of value | Unit |
|---|---|
| Primary-channel magnitudes | game units (hit points, attack points) |
| `combat.power.*` · `combat.defense.*` · `combat.shield.*` | **game units** — additive damage / hit points, summed directly (`OverlayCombatCalculator.cs:84-89`). **Not resolver points.** |
| `combat.accuracy.*` · `dodge` · `crit.rate` · `crit.resist` · `crit.damage` · `crit.resist.damage` | **resolver points** — sigmoid scale, `AccuracyScale = CritRateScale = CritDamageScale = 100.0` (`CombatPolicies.cs:10-12`) |
| `chance`, curve multipliers, ratios | **integer per-mille** |
| Durations | integer ms |

For calibration: `critical-hunter` grants **+150** crit-rate points (crit ~7.6% → ~26.9%); the patron aura divides ‰ by ten, so its 150‰ clamp is **+15 points**. `+10 hp` and `+10 fire power` differ by roughly an order of magnitude in effect. **Tier bands are authored per channel family and never copied across.**

### Value spec validation

| Rule | Reason code |
|---|---|
| `Fixed` ⇒ `Min == Max` | `BadValueSpec` |
| `Min ≤ Max` | `BadValueSpec` |
| `chance` ∈ `[0, 1000]` | `BadParamValue` |
| `icd_ms ≥ 0` | `BadParamValue` |
| `icd_key` matches `^[a-z0-9]+(-[a-z0-9]+)*$` when present | `BadParamValue` |
| `tier ≥ 1` | `BadParamValue` |
| `weight ≥ 0` | `BadParamValue` |
| Magnitude fits `int` after curve scaling | `MagnitudeOverflow` |

**Sign carries meaning and is per-kind:** `resource.delta` negative = damage, positive = heal. `stat.modify` on a `LowerIsBetter` channel (E16) negative = improvement. Every kind's schema states its sign convention; there is no global rule.

### Curves

`points_json` is an array of `[x, multiplierMilli]` pairs.

| Rule | Behaviour |
|---|---|
| Zero points | **row rejected at load** (`BadCurve`) — never a hot-path divide-by-zero |
| One point | constant multiplier, no interpolation |
| Duplicate `x` | **rejected** |
| Unsorted `x` | **rejected** — "ordered" is validated, not assumed |
| `x` below first / above last | clamp to the end point; never extrapolate |
| Interpolation | linear in ‰, rounded **half away from zero, exactly once**, at the end |

**Curve application order:** the curve scales `Min` and `Max` **before** the roll. Scaling after the roll can land outside the scaled range on a rounding boundary, which would break the inclusive-bounds guarantee.

**Curve `input` sources:**

| `input` | Reads | When absent |
|---|---|---|
| `level` | the **owning actor's** level | a `match`/`sector`/`slot`-scoped binding has no actor → **`ScopeUnsupported` at bind** |
| `rarity` | the container's rarity **ordinal** (§4) | container has no rarity → rejected at bind |
| `tier` | the atom's own `tier` column | always available |

---

## 3. Predicates

### JSON encoding — one canonical form

```json
{ "op": "and", "children": [
  { "leaf": "sideIs", "subject": "target", "value": "zombie" },
  { "leaf": "hpBelowMilli", "subject": "target", "value": 500 } ] }
```

Internal nodes carry `op` + `children`. Leaves carry `leaf`, `subject`, `value`. No other shape parses.

### `subject` is required on **every** leaf

Not just side and type leaves. The `OnDamageDealt` inversion is a property of **the event**, not of two leaf types — `hasStatus` on `OnDamageDealt` is exactly as ambiguous as `sideIs`. Omitting it is `AmbiguousSubject`.

E11's migration therefore writes `subject: target` onto **every** migrated `OnDamageDealt` leaf, not only the side/type ones.

### Depth and node counting

- A bare leaf is **depth 1**. `And(leaf, leaf)` is depth 2. Max depth **4**.
- **Node count** counts internal nodes and leaves. Leaf `value` args do not count. Max **16**.
- `And` / `Or` with zero children: **rejected** (`EmptyNode`). `Not` with anything but exactly one child: rejected.
- An **absent** predicate is legal and means "always". That is different from a present, empty node.

---

## 4. Rarity — a real mechanism

Rarity governs two things, and now has columns for both.

`effect_container` gains **`min_tier`** and **`max_tier`** (nullable ints).

| Rule | |
|---|---|
| `pool_rolls` | how many atoms are drawn — unchanged |
| `min_tier` / `max_tier` | the tier window the pool may offer |
| Validation | every pool row's atom must have `min_tier ≤ tier ≤ max_tier`; violation is `TierOutOfWindow` |
| `rarity` | a label, and the **key budgets are looked up by** (§7) |

Rarity ordinals are **append-only and explicit**, in a `rarity` table with an `ordinal` column — never inferred from declaration order. That is the same rule E18 enforces for elements, and for the same reason.

### Pool grouping

**`group` defaults to `(family_id, variant)`, not `family_id`.** A container may therefore roll *fire* power and *ice* power — two variants of one family — which is normal ARPG itemisation, while still never rolling two tiers of the same variant.

An explicit `group` value overrides the default. **`pool_rolls ≤ count(distinct group HAVING max(weight) > 0)`**, validated with the default applied, not with NULLs. Counting groups whose every row is `weight = 0` passes validation and then under-fills the instance — groups A(10), B(0), C(0) with `pool_rolls = 3` draws one atom.

**All-zero-weight pools:** if every pool row has `weight = 0`, the container is **rejected** (`UnsatisfiablePool`). Silently under-filling an instance is the failure mode this program exists to remove.

**An atom in both the fixed core and the pool** is rejected (`DuplicateAtomInContainer`).

---

## 4a. Slot, affix bundle, resolution order, and RNG streams — normative (added 2026-09-01, `seed-to-concrete` T0.5)

`effect-pipeline-ideal.md` §5 designed these against a real defect (`+15% all resistances` costing six
of a rung-100 item's pool rolls because the element channel had no selection layer). This section
promotes that design from ideal doc to **normative definition** — it wins over any spec, the same
standing this document already has for everything above.

### Slot

A **slot** on a container pool row is a parameterised atom reference: it names a **domain** (e.g.
`element`) and a **pick count**, rather than one concrete atom.

```text
slot E1 : domain = element, pick = 1
atom ref: atom.elemental-power.$E1
```

The atom catalog, `atom_id` derivation, and its unique key are **unchanged** — only the container's
*reference* becomes parameterised. A patterned ref must resolve for **every member of its domain** at
load, so a missing element row is a load-time rejection, never a roll-time surprise.

**This is a different `slot` from §6's `owner_key` value `slot` (a world-map construction slot).**
Two different concepts share the word; §6 already warns not to share a type between them, and this
section is the other half of that warning — a container pool's `slot` is a channel-selection
parameter, never an `owner_key`.

### Affix bundle

The pool's roll unit is an **affix** — a named bundle of atom refs (which may include slots) that
share the container's resolved slots and are drawn **together as one roll**. `effect_container_pool`
rows reference affixes, not bare atoms.

An affix is what makes *"master of fire and ice"* (four atoms, two families, two elements, the
element correlated across both families) expressible as a single draw: today's one-atom-per-row pool
cannot correlate two independent draws, and an affix bundle is the unit that carries the correlation.

Validation follows the same law as every other entity in this document (§10): a bad affix — an
unresolvable ref, a slot whose domain has no eligible member, a duplicate atom within the bundle — is
rejected whole, with its id and reason, and does not enter the catalog.

### Resolution order — normative, not the order the layers were designed in

```text
1. slots      pick concrete variants for every slot in the container/affix
2. affixes    draw which affixes appear (the container's pool draw)
3. atoms      expand each drawn affix's refs against the resolved slots
4. tiers      pick tier within the container's min_tier/max_tier window
5. values     roll each magnitude in its range (§2)
```

A slot must resolve before the concrete atom it names can be looked up, and a tier must resolve before
the value range that atom's tier implies can be read — the dependency direction fixes the order. This
is what makes a roll reproducible; leaving it implicit is how two runtimes disagree on the same seed.

### RNG streams — one per layer, named

Each layer of the resolution order draws from its **own** named stream, following the shipped pattern
`SeededRng.DeriveStream(seed, "system:purpose")` (`FusionRoller.cs:27`, `fusion:traits` — already in
production, not a new mechanism):

| Layer | Stream name |
|---|---|
| slots | `affix.slot` |
| affixes | `affix.draw` |
| tiers | `affix.tier` |
| values | `atom.value` |

**Never one shared stream across layers.** If every layer drew from one stream, inserting a new layer
later would shift every historical roll's consumption — `catalog_revision` (§5) detects a *content*
change, not a change in how many numbers the resolver consumed, so it cannot protect against this.
Separate streams make each layer's consumption independent of every other layer, which is what lets
this program add a layer later without silently re-resolving every already-owned instance on replay —
the reproducibility law two paragraphs above this one (§5's `roll_seed` contract) depends on it.

---

## 5. Instances, ordering, determinism

### Instance identity and reproduction

`instance_id` and `created_utc` are **excluded** from the byte-identity comparison. The reproduction contract is:

> Same `(container_id, catalog_revision, roll_seed)` ⇒ identical `effect_instance_atom` rows: same atom set, same `values_json`, same `power_json`.

`pool_seed_scope` is **deleted**. It was undefined and it contradicted `roll_seed` being an input.

### `seq` for drawn atoms

Fixed-core atoms keep their authored `seq` (0-based). Drawn atoms are appended **after** the core, in draw order, continuing the numbering. The deterministic part comes first, which matches the intuition that a trait always contains what it says.

### The actor effect list — the one execution order

Atoms on an actor's effect list are iterated in **`(priority DESC, container_id ASC, seq ASC)`**, compared **ordinal**.

An earlier draft used `binding_id` as the tiebreak. That is wrong: `binding_id` is *generated* (§1), so two runs of the same container against the same `roll_seed` produce different ids, sort differently, and consume the `atom.apply` stream in a different order — different trace bytes from identical inputs. The tiebreak must be **content-derived**.

It is also **not** a reuse of `InMemoryEffectGrantStore.Sorted()`: that sort is `.ThenBy(g => g.GrantId)` with no comparer, which resolves to `Comparer<string>.Default` — **culture-sensitive**. Grant ids are not pure hex (`fx.butter_on_hit`, `entity:0xabc`), and ICU gives `.`, `-`, `_` variable weight, so execution order would depend on the host's globalization mode. Whatever reuses this must pass `StringComparer.Ordinal`.

That order is what makes multi-atom `OnApply` draws reproducible: two atoms rolling on one hit consume the stream in list order, and list order does not depend on how or when a binding arrived.

### Catalog revision

**One monotonic integer per catalog**, stored in a `content_meta` row, bumped by the importer **once per import transaction** — not per row. Per-row `revision` columns are for cache invalidation of that row only and are never a bake key.

`catalog_revision` is what E6 reproduces against and what E7 keys its bake cache on. Two authors editing different rows in one import share one bump; concurrent imports are serialised by the transaction.

---

## 6. Owner keys

| Scope | `owner_kind` | `owner_key` | Validation |
|---|---|---|---|
| match | `match` | `''` | empty string, never NULL |
| plant type | `plant` | decimal typeId | `≥ 0` |
| zombie type | `zombie` | decimal typeId | `≥ 0` |
| entity | `entity` | **lowercase hex, no `0x`** | `^[0-9a-f]+$` |
| player | `player` | decimal id | `> 0` |
| sector | `sector` | sector id | must exist |
| slot | `slot` | slot id | must exist |

The canonical string form is `{owner_kind}:{owner_key}`, and `match` renders as `match` with no colon. `entity:0xABC` and `entity:abc` were both in circulation; **only the second parses**. Anything else is `BadOwnerKey`.

**`slot:` here is a world-map construction slot.** It is unrelated to an item's `slot` column. Two different concepts, one word — do not share a type.

### G8, corrected

The `TakeDamage` prefix reads **one side-wide cached value**, so `stat.modify` on `defense` does nothing for *any* per-entity or per-type binding — not just `entity:`. The rule is therefore:

> `stat.modify` on `defense` is legal **only** at `match` scope. `plant:N`, `zombie:N`, and `entity:` all reject with `ScopeUnsupported`.

Per-actor mitigation is `stat.derived` on `combat.defense.*`. Per-entity primary defense waits for perf **O5**.

### Stale owners

| Case | Behaviour |
|---|---|
| `entity:` whose pointer is freed | binding is dropped at match end; **`entity:` bindings are session-scoped and never durable** |
| `plant:N` for a typeId that does not exist | accepted — type catalogs are game data we do not own |
| Instance deleted with live bindings | FK `ON DELETE CASCADE`; bindings go with it |
| Atom disabled (`enabled = 0`) beneath a live instance | the instance keeps its frozen values; **new binds reject** with `StaleInstance` |
| Atom row deleted | forbidden — content is disabled, never deleted (`enabled = 0`) |

---

## 7. Power

### The scalar — geometric mean over non-zero categories

A true geometric mean annihilates on any zero, and most atoms touch one or two of five categories. So:

> **`scalar = geomean(vᵢ + 1) − 1`** over **all five** categories, and `0` when every category is zero.

*(Revised 2026-08-22, closing **D1**. The previous rule — geomean over non-zero categories only — ranked a strictly better vector **lower**: `{100,0,0,0,0}` scored 100 and `{100,1,0,0,0}` scored 10, so adding a 1-point rider divided displayed power by ten. The `+1` shift makes the mean monotone in every component and removes the zero-annihilation that motivated the non-zero filter in the first place.)*

**Stated limitation, not hidden:** the scalar is a **display and sort** number. It compresses five dimensions into one, so two very different builds can land on the same value; it is monotone, which means it never ranks a strictly better thing lower, but it is not a balance instrument. Anything needing a real comparison uses the vector or the marginal read.

Worked, so the property is checkable: `{100,0,0,0,0}` → `101^0.2 − 1 = 1.52`; `{100,1,0,0,0}` → `202^0.2 − 1 = 1.89` (**up**, correctly); `{20,20,20,20,20}` → `21 − 1 = 20`, which beats the glass cannon, so E10's "balanced reads higher" row passes.

### Vector shape and units

`power_json` is `{"offense":n,"survivability":n,"control":n,"utility":n,"economy":n}`, integers, all five keys always present, in that order. The unit is **budget points**, defined as: *one budget point equals the cost of one game unit of `maxHp` at the reference level.* Every coefficient is expressed against that anchor.

### Conditionality's three missing factors

```text
conditionality = (chance/1000) × triggerFrequency × icdFactor × targetCountFactor
```

| Factor | Definition | Source |
|---|---|---|
| **no trigger at all** | **`conditionality = 1`, short-circuit** — permanent modifiers (§14.2) do not fire per minute, so the product below never runs. Without this the 26 passive families price at zero |
| `triggerFrequency` | expected fires per battle-minute for that trigger | `power_trigger_frequency` table — **data**, sweep-fittable, hashed |
| `icdFactor` | `min(1, triggerFrequency⁻¹ / (icd_ms/60000))`; `1` when `icd_ms = 0` **or `triggerFrequency = 0`** (else it divides by zero, and §7 mandates integer arithmetic, which throws) | computed |
| `targetCountFactor` | `min(maxTargets, expectedTargets)` from the target spec; `1` when single-target | computed — **but `expectedTargets` is a balance number in no table and no covered-hash list.** Changing it re-prices every AoE atom under an unchanged `contentHash`, which is the exact outcome `triggerFrequency` was tabled to avoid. Table it, or derive it only from the target spec's own content fields and name them |

Putting `triggerFrequency` in a hashed table rather than in code is deliberate: it is a balance number, the sweep must be able to propose against it, and a code constant would move every golden with **no content-hash change** — the one outcome E8 exists to prevent.

### Drift tolerance

**±25%, per category, on the absolute value**, floor 1 point.

Not 5%: E9 documents that the formula is *known* wrong by 12.5% on multiplicative pairs, so a tight tolerance fails every crit and element atom on day one. Not 50%: that cannot detect a real mistake. 25% catches order-of-magnitude errors — which is the class the units trap produces — while tolerating the interaction error the marginal read exists to handle.

### Actor power

```text
actorPower(actor) = price( Σ over atoms on the actor's effect list, grouped by channel )

  not  Σ over atoms of price(atom)
```

**Base stats contribute nothing.** That is what makes E10's "marginal on an empty actor ≈ stored power" true, and it keeps actor power a measure of *what was granted*, not of the level curve. If a future spec wants level in the number, it adds a progression atom rather than changing this function.

**Revised 2026-08-22, closing D2.** The previous definition summed **per-atom prices**. A sum has no
cross terms, so E10's marginal read — `vector(with) − vector(without)` — collapsed to exactly the atom's
own context-free price on every actor, for every actor. The read that was supposed to capture
multiplicative pairs returned the number it was meant to improve on.

Aggregating **channel totals first** and pricing the composed result fixes it by construction: crit rate
and crit damage land on different channels, compose the way combat composes them, and the price of the
composition is not the sum of the two prices. The marginal read then genuinely differs by context — a
crit-rate atom on a high-crit-damage actor prices above its stored value, which is what E10's acceptance
rows always claimed and could not previously distinguish.

**Stored `atom.power` does not go away.** It stays the context-free number for budgets, rarity windows,
sorting, and display — computed with the atom alone on an empty actor. What changes is that actor power
is no longer its sum.

Memoized per `(actor, catalog_revision, binding set hash, truncateSpawns)`. **The cache lives in E9**, not E10, because E9's spawn recursion needs it to terminate.

`truncateSpawns` is in the key on purpose. Without it, an actor evaluated first as a *spawned body* caches its truncated price, and a later top-level read of the same actor returns that truncated number — so the same catalog yields two answers depending on traversal order, and E10 stamps whichever happened.

Spawn recursion: **depth 1**, then truncate. A spawned actor's own spawn atoms price as 0.

**The spawned body is priced (closes D3).** "Base stats contribute nothing" applies to the *owning*
actor's level curve — it does not apply to a body an atom conjures, or a `spawn.entity{hp: 5000}` would
price at zero and clear every budget. A spawn's value is its body, valued from `hp`/`maxHp`/`atk` against
the maxHp anchor above, **plus** whatever atoms the spawned actor carries. `spawn.entity` gains a `count`
param — **`min: 1`, default 1**, because without a floor an omitted `count` is 0 and re-opens this very
defect. Declaring it belongs to **D7**, which re-derives every param schema against its executor.

---

## 8. Content hash

**Algorithm: sort-then-concatenate. XOR-fold is banned.**

XOR cancels duplicates — two identical rows fold to zero, so a non-idempotent import that doubled every row would produce an *unchanged* hash, and E14's "import twice, hash unchanged" test would pass while the database doubled. The cheaper option is the broken one.

```text
rowDigest  = SHA256(canonical(row))
tableDigest = SHA256(concat(sort(rowDigests)))          // sorted, not folded
contentHash = SHA256(concat(tableDigest for each covered table in registry order))
```

### Canonical row form

Columns in declared order, each **length-prefixed** as `{byteLen}:{bytes}`. A bare `\x1f` separator is **not injective**: `(name="a\x1fb", note="c")` and `(name="a", note="b\x1fc")` canonicalise identically, and `name`/`power_note` are free text. Length-prefixing removes the boundary ambiguity.

**NULL is a sentinel, not a payload — corrected 2026-08-22 while building E8.** This section previously encoded NULL as a literal `\x00` byte and argued the length prefix kept it distinct from a string containing one. It does not: a column holding exactly `"\0"` is *also* one byte of `0x00` under prefix `1:`, so the two produce the same digest and a NULL column is forgeable. The form is now `N:` with no payload — `N` is not a digit, so no `{byteLen}` can ever spell it. Pinned by `ContentHashTests.Null_and_a_single_nul_character_string_do_not_collide`, which failed against the original rule.

Within a JSON column: keys sorted ordinal, no whitespace, **numbers emitted as integers when integral** (`100.0` → `100`, `1.50` → `1.5`), strings NFC-normalised. Array order is preserved — curve points are ordered, and sorting them would hide a real edit.

### Covered tables — a registry, not a list

Modules **register** into an ordered set, and the set is versioned as `contentHashSchemaVersion`. Adding a table is an explicit bump, not silent breakage of every stamp made before it.

Covered: `effect_atom` · `effect_container` · `effect_container_atom` · `effect_container_pool` · `effect_curve` · `effect_element` + both matrices · `power_coefficient` · `power_trigger_frequency` · `rarity`.
Not covered: instances · bindings · runtime state · `power_coefficient_proposal`.

Disabled rows **are** hashed. An empty covered table digests as `SHA256("")` — an empty catalog produces a *specific, recognisable* hash, not a stable-looking accident.

---

## 9. Runtime support — four states, not three

A three-flag bitfield cannot express the matrix it must hold. Per runtime:

| State | Meaning | Bind |
|---|---|---|
| `Full` | executes end to end | accept |
| `Partial` | executes only through a named side path (e.g. `status.apply` in battle: setup only) | accept **with a warning**; the named path is recorded |
| `PlanOnly` | produces a plan, applies nothing (sim) | accept only when the host declares itself a planner |
| `None` | no consumer | **reject** `RuntimeUnsupported` |

Collapsing `PlanOnly` into `Full` would make sim silently accept bindings it cannot execute — the exact silent no-op the layer exists to prevent.

---

## 10. Rejection codes — the closed list

`UnknownKind` · `UnknownParam` · `MissingParam` · `ParamNotHonoured` · `ParamNotImplemented` · `BadParamValue` · `BadValueSpec` · `BadCurve` · `MagnitudeOverflow` · `IdMismatch` · `DuplicateKey` · `UnknownLeaf` · `AmbiguousSubject` · `DepthExceeded` · `NodeCountExceeded` · `EmptyNode` · `UnknownAtom` · `UnknownContainer` · `DuplicateSeq` · `DuplicateAtomInContainer` · `UnsatisfiablePool` · `PoolRollsExceedGroups` · `TierOutOfWindow` · `OverrideChangesKind` · `MissingPowerNote` · `BadOwnerKey` · `ScopeUnsupported` · `RuntimeUnsupported` · `AmbiguousTarget` · `StaleInstance` · `UnknownTrigger` · `TriggerNotAllowed` · `LevelTooLow`

**Thirty-three codes.** Adding one is a reviewed change; they are the operator-facing error surface.

*(`UnknownTrigger` and `TriggerNotAllowed` ship in E1 today — a trigger that does not exist and a trigger a kind may not carry are different author mistakes. `LevelTooLow` is E6's `level_req` gate.)*

### Two phases, both rejecting

| Phase | Policy |
|---|---|
| **Import** (E14) | **all-or-nothing.** One bad row and nothing is imported. A partial import produces a hash for a state nobody authored |
| **Load** (E4/E5) | per-row rejection, logged. Defence in depth against a database edited outside the importer |

These are different phases, not a contradiction — the earlier specs stated them as competing policies for the same event.

---

## 11. Measurement methods

| Claim | Method |
|---|---|
| "zero allocation" | `GC.GetAllocatedBytesForCurrentThread()` delta, after 1 000 warmup iterations, over 10⁵ measured iterations, workstation GC, single thread |
| ns/atom budget | median of **9** runs; fails at **> 1.5×** the budget; raw numbers always printed |
| Budget target | **≤ 50 ns/atom on the CI reference machine**, recorded with CPU and runtime. Not "the owner's machine" — the guard runs in CI |
| Weight distribution | fixed seed ⇒ **exact expected counts**, not a tolerance. A tolerance on a seeded test is an invitation to widen it |
| Fuzz corpora | **seeded and recorded**; a failing seed is persisted as a regression case |

---

## 12. What this document deliberately does not decide

- The 12-kind granularity rule (owner-approved separately; belongs in E1).
- Whether battle grows consumers — that is the combat-action program's.
- The damage consumer/applier, the AI layer, world triggers.
- Coefficient **values** — E9 authors those; this pins only their table and units.

## 13. Defect log — four adversarial passes (2026-08-22)

Verified against code and arithmetic, not asserted.

> **None of these block the spec.** D1–D4 are one accepted limitation the ideal already scoped
> ([effect-atom-ideal.md:362](../effect-atom-ideal.md): *"This does not block the spec. It blocks
> trusting the number for balance."*). D7 is build work inside E1. D5, D9, D10 are decided below.
> Each entry says which build position owns it, so nothing here gates a module before that point.

### D1–D4 — the power model — **ACCEPTED LIMITATION, owned by E9 (build position 15)**

> **This was always the shape of the problem, and the ideal said so before any spec existed:**
> *"This does not block the spec. It blocks trusting the number for balance."* Pricing multiplicative
> effects has no closed-form answer — every linear cost function makes the marginal read inert, and a
> nonlinear one has to be **fitted by a simulation sweep**, which is exactly what E9's coefficients
> were always scheduled for. Treating it as a design gate was a mistake; it is a research task with a
> known home.
>
> **What ships meanwhile, and what it is good for:**
>
> | Use | Works today? |
> |---|---|
> | Authoring budget — stop an author shipping an overpowered item | **Yes.** `Σ atom.power` bounds an item, which is all a ceiling needs |
> | Display and sorting | **Yes**, with the monotone scalar — imprecise, never wrong-ordered under domination |
> | AI reads (vector + matchup) | **Yes.** Neither depends on the scalar or the marginal read |
> | Trusting the number for **balance** | **No** — and this was never claimed. It waits for the sweep |
>
> The four findings below are kept in full because they are the specification of what the sweep must
> fix. Nothing before position 15 reads any of it.

#### D1 — the display scalar

> Monotonicity is **fixed and verified** — `geomean(vᵢ+1)−1` survives the domination attack cleanly.
> But the scalar is **underspecified** (no type, no rounding point, and `pow(x, 0.2)` is not
> bit-reproducible across runtimes, while E10 *stamps* power into hashed reports), and it **inverts
> sort order at real magnitudes**: `{4,4,4,4,4}` (20 budget points) reads 4.00 and outranks
> `{1000,0,0,0,0}` (1000 points) at 2.98. A 100× budget span renders as 3.5×. Deferred to E9.

> Fixed in §7: `scalar = geomean(vᵢ + 1) − 1` over all five categories. The defect as found is kept below.

`scalar = geomean(categories where value > 0)` (§7) ranks a **strictly better** vector **lower**:

| Vector | Non-zero set | Scalar |
|---|---|---|
| A `{off:100, 0, 0, 0, 0}` | {100} | **100** |
| B `{off:100, surv:1, 0, 0, 0}` | {100, 1} | **10** |

B dominates A in every component and scores 10× lower, so adding a 1-point rider to a 100-offense atom
divides its displayed power by ten — a direct incentive to author narrower atoms, the opposite of what
the family library is for. `{100,0,0,0,0}` and `{100,100,100,100,100}` also both score 100, so budget
scale is invisible. And it falsifies E10's own acceptance row *"balanced reads higher than glass cannon"*:
balanced `{20×5}` scores **20**, glass cannon `{100,0,0,0,0}` scores **100**.

The stated limitation in §7 covers *incomparability across category counts*. This is a different defect:
**wrong ordering under domination.**

**Proposed:** `scalar = geomean(vᵢ + 1) − 1` over **all five** categories. Monotone in every component,
never annihilates, still root-shaped, still exactly 0 when the vector is 0. A → 1.52, B → 1.89 (up,
correctly), balanced → 20 > glass cannon 1.52, so E10's row starts passing. **This overturns an owner
pick and needs the owner's word.**

#### D2 — the marginal read is identically equal to stored power

> The "aggregate channel totals, then price" fix is **inert**. `normalize(magnitude, referenceScale)`
> is a division — **linear** — so `price(Σm) = Σprice(m)` and grouping changes nothing. Worse, crit
> rate and crit damage are on **different** channels, so each total *is* a single atom's magnitude and
> there is no composition at all: marginal still returns stored power exactly. A genuinely nonlinear
> cross-channel `price()` is asserted in prose and defined nowhere. The mechanism does bite within one
> nonlinear channel (the element sigmoid) — that is all it buys. Deferred to E9.

> Fixed in §7: `actorPower` aggregates channel totals and prices the composed result. The defect as found is kept below.

`actorPower = Σ atom.power` (§7) is additive, so
`marginal = Σ_{A∪{x}} − Σ_A = p(x)` for **every** actor A. A sum has no cross terms.

So *"the difference captures whatever multiplies, by construction"* is false by construction, and E10's
three acceptance rows (marginal above / at / approximately stored power) cannot be told apart by any test.
This matters beyond the tests: the map lists multiplicative pairs as the program's **one remaining open
problem** and says E10's marginal read closes it. It does not.

**Proposed:** `actorPower` aggregates **channel totals through the resolver** — sum each atom's
contribution per channel, then price the composed result the way combat folds it (crit rate × crit
damage, element ring, shield layers). Memo key unchanged. Until that exists, multiplicative pricing is
**open**, not solved.

#### D3 — a summon prices at exactly zero

> Body pricing is right, but `count` was added with **no default and no floor**, so
> `spawn.entity{hp:5000}` authored without it gives `count = 0` and prices at zero again — the same
> defect, re-entered through the param added to fix it. Needs `min: 1`. Same for
> `targetCountFactor`, which needs `max(1, …)`. Deferred to E9.

> Fixed in §7: the spawned body is priced from `hp`/`maxHp`/`atk` against the maxHp anchor, plus its own atoms; `count` becomes a real param.

*"Base stats contribute nothing"* plus `actorPower = Σ atom.power` means a spawned actor carrying no
atoms is worth **0**. So E9's own motivating example — `5% on death, spawn 2 zombies with 500 hp /
100 atk` → `0.05 × 2 × power(actor)` — evaluates to **0**, and its acceptance row passes vacuously as
`0 = 0`. A `spawn.entity{hp: 5000}` at `chance: 1000` clears every rarity budget and displays a scalar
of 0. Separately, the formula multiplies by **`count`**, which is not a param of `spawn.entity` in the
shipped schema.

**Proposed:** carve an explicit exception — price the spawned **body** from `hp`/`maxHp`/`atk` against
the maxHp anchor in §7, *plus* its atoms — and add `count` to the schema or drop it from the formula.

#### D4 — conditionality is 0 for proc atoms

> The passive half is genuinely closed by §14.2, and there is no divide-by-zero. But the **integer
> `chance/1000`** zero is live and unaddressed: under §2's mandated integer per-mille, `chance = 999`
> gives `999/1000 = 0`, so **every proc atom below 1000‰ still prices at zero**. The resolution note
> claimed §7 tracks the fixed-point scale; §7 does not. Also unspecified: which conditionality applies
> to a composed channel total when two atoms differ — the naive readings mis-price by ±33%, outside
> E14b's ±25% drift tolerance. Deferred to E9.

> Dissolved by §14.2: `stat.modify` / `stat.derived` declare no trigger at all, so `conditionality = 1` is the normal case for all 26 affected families, not an exception. The integer fixed-point scale still needs pinning — tracked in §7.

`conditionality = (chance/1000) × triggerFrequency × icdFactor × targetCountFactor`. `stat.modify` and
`stat.derived` carry only `OnGranted`/`OnRemoved` — they do not fire per minute, so `triggerFrequency`
is absent, the product is **0**, and **26 of the 71 authored families** price at zero. Every item made
of passive stats is free under the budget.

Two more in the same formula: the product is written as fractions while §2 mandates integer per-mille,
and in integer arithmetic `chance/1000` is **0** for every chance below 1000 — which zeroes proc atoms too.

**Proposed:** `conditionality = 1` when `when.trigger` is absent; the `icdFactor` guard is already
recorded in §7; and pin the fixed-point scale plus the single rounding point for the product.

### D5 — the replay path was not byte-reproducible — ✅ **PARTLY FIXED 2026-08-22**

> `SeededEffectRandom` now wraps the owned xoshiro256** stream. **Zero goldens moved** — verified, not
> assumed: all 7 chance-gated fixtures use `chance: 1.0` and `EffectProcAndOwner.cs:293` short-circuits
> the draw at `chance >= 1.0`, so the RNG was never consulted on that path. 1897 Core / 235 Data /
> 47 Guard green. The injector seed and the ordinal comparer remain open (below).

Three separate breaks, all verified in code:

- `SeededEffectRandom` wraps **`System.Random`** (`EffectModels.cs:97`), and it backs `SimEffectHost`
  and `FoundationHarness` — the two hosts the 49-fixture corpus runs on. The repo's own
  `SeededRng.cs` header says *"Never use System.Random for anything replayable: its seeded sequence is
  not guaranteed stable across .NET versions."* Battle obeys it; the sim/fixture path does not. A .NET
  upgrade moves every chance-gated golden with **no content change and no `contentHash` change**, so
  E8's cross-hash refusal cannot catch it.
- The injector's proc RNG is seeded from **`Environment.TickCount`** (`EffectRuntime.cs:57`) — wall
  clock, no per-match seed, no stream derivation. E15's *"fixed seed → exact expected count"* rows
  cannot hold on that path.
- The reference sort is **culture-sensitive** (see §5).

**Done:** `SeededEffectRandom` now derives from `SeededRng`. No golden moved, so no `RngAlgoVersion`
bump and no sign-off was needed — the earlier "this needs the owner" was an **assumed** constraint,
not a tested one, which is exactly the failure [DESIGN-GATE.md](../../DESIGN-GATE.md) §3.4 exists to stop.

**Still open:** the injector's per-match seed (E19 pushes it) and the ordinal comparer (§5). Both are
testable the same way — run the suite before claiming either one moves anything.

**Correction to the reasoning, 2026-08-22.** An earlier draft argued the roll must be injector-local
because a server round trip cannot complete inside a frame. **That premise is false**, and it was
written without reading the pipeline SSOT. The pipeline is **record-then-drain**: hooks record a typed
struct and return, effects are decided in a later budgeted drain, and records carry to the next frame
when the budget runs out. [event-pipeline-v2-ssot.md](../event-pipeline-v2-ssot.md) **G5** makes
*delayed effects* the designed worst case, explicitly in preference to frame drops.

The RPG and the game are **two async systems**. The RPG works from past events and contributes a signed
delta later; it never reads or guesses current game state. So **where the roll happens is a free
choice**, not a latency constraint, and determinism comes from the server owning the **seed** either
way. The real tradeoffs are chattiness (1,250–10,000+ events/s at the hook, before coalescing),
pointer lifetime (a longer delay means more targets already dead, so more silently skipped procs), and
offline resilience — none of which is "it must finish this frame".

### D6 — `stat.derived` is a kind with no executor, shipped as `Full / Full / Full` — ✅ **FIXED 2026-08-22**

> Applied in shipped code: `stat.derived` is quarantined to `None/None/None`; `resource.delta` battle
> and `shield.grant` battle/sim are `None`. Three tests that asserted the old cells were rewritten —
> including one named `Battle_support_is_narrow_and_honest`, which asserted three unreachable `Full`
> cells. Propagated to `atom-catalog-ssot.md` §2. 1902 Core tests green, four guards pass.

No opcode, no `EffectBag` branch, no sink arm (the sink dispatches ten actions and throws on unknown),
and battle reads derived mods only from `TraitBattleCatalog` — never from a grant. Bind
`atom.elemental-power.fire.t3` in any runtime: the gate accepts it, the compiler has no opcode, the
runner has nothing to dispatch to. It does nothing, forever, silently — which is `status.expose.*`
again, the exact failure E1 exists to prevent, and it violates E1's own *"never add a kind without an
executor."*

Three more cells fail the same audit: `resource.delta` Battle=Full and `shield.grant` Battle=Full
(battle never grants and never raises an event; `Bag.ShieldGate` is never set on the battle host), and
`shield.grant` Sim=Full (`SimEffectHost` sets `Bag.Status` and `Bag.UtcNow`, never `Bag.ShieldGate`,
so `ExecGrantShield` skips with `shield-runtime-missing`).

**Proposed:** those cells become `None` until a consumer ships. E1 already commits to treating the
matrix as audited fact and re-verifying against code — this is that re-verification, and it edits
shipped code plus its tests.

### D7 — E1's param schemas do not match the opcodes they claim — ✅ **FIXED 2026-08-22**

> Re-derived from the executors. `box.set.boxType` → `Int` (read with `GetInt`); `status.apply` →
> FA2's real names and units (`status`, `duration` in **seconds**, `level`) and **no `target`**, because
> FA2 has none; the DoT/contagion payload (`statusId`, `periodMs`, `durationMs`, `tickBudget`,
> `spread`) moved to **`resource.delta`**, the FA10 opcode that actually carries it;
> `shield.grant.sourceClass` declared, restoring aura priority and the `refillOnMerge` default;
> `spawn.entity.count` declared. **`G5` is reclassified as a runtime hole** — no load-time param check
> can close it. 10 new tests pin each schema to its executor.

> The rule is already specified: a schema declares only keys its executor honours. Five schemas do not
> match theirs. Closing it is **reading five executors and correcting five declarations** — mechanical
> work with a known method and no decision in it. Belongs to E1's task list; blocks E7, which is where
> a wrong schema would first produce a wrong grant.

Five verified mismatches: `box.set.boxType` is declared `String` and read as `int`; `status.apply`
declares `statusId`/`durationMs` while FA2 reads `status`/`duration` as **float seconds** and its
allowlist contains neither; `status.apply.target` is declared **required** but FA2 has no `target`
param at all (the target comes from the event, so a load-time check cannot close G5's runtime hole);
`status.apply`'s DoT/contagion payload (`periodMs`, `amount`, `tickBudget`, `spread`) lives on **FA10**,
not FA2, so most shipped status content compiles to a different opcode than the SSOT claims; and
`shield.grant` omits `sourceClass`, which the executor honours — so every atom-granted shield is
`PrioritySkill` with `refillOnMerge = true` and the `warded` family loses a shipped capability.

**Proposed:** re-derive each `ParamSchema` from the executor's actual reads, with types, and make the
two-opcode split explicit where a kind spans both.

### D8 — two of the 16 defs carry trigger **lists**; one atom holds one trigger — ✅ **RESOLVED 2026-08-22**

> Fixed in §14: `icd_key` gives the cooldown its own identity (Unreal GAS's model), and `OnGranted`/`OnRemoved` stop being authorable. `fx.shield_grant` → 3 atoms sharing one clock; `fx.passive_atk_flat` → **one** atom. Both stay byte-identical.

`fx.shield_grant` has `{OnDamageDealt, OnTimer, OnSpawn}` and `fx.passive_atk_flat` has
`{OnGranted, OnRemoved}`. Splitting each into N atoms yields N grants and therefore **N independent
ICD clocks**, where today one grant shares one clock — so an actor hit then spawning 100 ms later
fires `fx.shield_grant` twice on the atom path and once today. That is a behaviour change inside E11,
whose acceptance is byte-identical plans and whose stated rule is *"there are no deliberate ones in
this module."* Worse, `fx.passive_atk_flat`'s `OnRemoved` half is not content at all — `EffectBag`
injects `remove = true` — so as two atoms the apply/un-apply coupling is unenforceable and an author
can ship only the `OnGranted` half and leak a permanent buff.

**Proposed:** `when_json.trigger` becomes a **list** (the field it migrates from already is one), one
grant per atom.

### D9 — the compile/run split leaks on curves — ✅ **DECIDED 2026-08-22**

> **`input: level` is forbidden on an `OnApply` value spec** (`BadValueSpec` at E4 load). E7 bakes the
> **pre-multiplied `(Min, Max)`** into each runner entry and re-pushes on level change, so no curve row
> ever travels and E19's "the injector holds no content rows" holds literally rather than
> approximately. `OnInstantiate` keeps `input: level` — it resolves server-side at drop, where the
> curve table is present.

E19 forbids curve rows from travelling. But an `OnApply` range with `scale: curve.atk.level` is
classified as **runner** work, and rolling it locally needs `points_json` and the actor's level — a
content lookup on the injector. **Proposed:** forbid `input: level` on `OnApply` value specs and have
E7 bake the pre-multiplied `(Min, Max)` into each runner entry, re-pushed on level change. *(Related
and unnamed anywhere: if a spawned actor carries atoms, something must bind them at spawn time on the
injector unless E7 pre-compiles every spawnable archetype.)*

### D10 — `shield.grant` requires `amount`; the def it must migrate has empty params — ✅ **DECIDED 2026-08-22**

> **`amount` becomes optional on `shield.grant`, with a bind-time check that the magnitude arrives from
> *somewhere*** — the atom's params or the grant overlay — rejecting `MissingParam` if neither has it.
> The alternative (E11 authors a magnitude into the row) would change the emitted grant and break
> Checkpoint D's byte-identity, so it loses. Deferring the check to bind rather than load is the whole
> point: the overlay is not visible at load.

`fx.shield_grant`'s `Params` is an empty dictionary — every magnitude arrives from the grant overlay.
Migrating it yields `MissingParam`, the row is rejected, and Checkpoint D cannot close. **Proposed:**
make `amount` optional with a bind-time presence check against the overlay, or accept that E11 authors
a magnitude into the row and that the emitted grant is no longer byte-identical — and say which.


---

## 14. The ICD key, and why lifecycle is not a trigger — resolved 2026-08-22 (closes D8)

`when_json.trigger` stays a **single trigger string**. Two additions make that safe.

### 14.1 `icd_key` — a **compile-time grouping key**

> **Corrected 2026-08-22, second pass.** The first draft said atoms sharing an `icd_key` share one
> runtime clock. That does not work: the ICD clock is keyed `grantId + "|" + grantEffectId`
> (`EffectProcAndOwner.cs:307`), which a column on `effect_atom` cannot reach; `ClearGrant` tears
> clocks down by **prefix match on `grantId|`** (`:279`), so re-keying would leak a withdrawn grant's
> cooldown and break `effect-withdraw-on-die`; and the same key backs `max_stacks`, the chance roll,
> `RecordCounterHit`, and `status_icd_ms` — so "share the clock" would silently re-scope four other
> things. The corrected design below touches no runtime key at all.

**Foundation already holds a trigger list.** `EffectDef.Triggers` is a `List<string>`
(`FoundationHarness.cs:335` ships `{OnDamageDealt, OnTimer, OnSpawn}` on one def today), and the ICD
key deliberately does **not** include the trigger — so one grant with three triggers is already one
clock. Nothing in the runtime needs to change.

So `icd_key` is a nullable column on `effect_atom` that **E7 groups on at compile time**: atoms sharing
an `icd_key` compile into **one** `EffectGrantDto` whose `Triggers` is the union of theirs. The atom
schema keeps one trigger per row (simple, indexable); the grant keeps the list Foundation already
understands.

That makes byte-identity trivial rather than argued: `fx.shield_grant` compiles back to exactly the
grant shape it has today — same `grantId`, same trigger list, same single clock, same stacks, same
chance roll, same counter accumulator. **Atom→grant cardinality is therefore many-to-one, keyed on
`COALESCE(icd_key, atom_id)`,** which is the thing the first draft left unstated and which every
objection to it turned on.

This is not invented. It is how the most mature system in this space models it:

| System | How it handles it |
|---|---|
| **Unreal GAS** | A cooldown is **not** a property of an ability. It is a separate effect granting a **Gameplay Tag**; anything holding that tag is on cooldown, and abilities share a cooldown by sharing the tag |
| **Path of Exile** | Giving one skill **multiple trigger conditions disables it** — the case is refused outright. But *identical* skills share a cooldown, so "what fires" and "what the clock is keyed on" are already separate concepts |
| **WoW** | The internal cooldown belongs to the **item or aura**, regulating proc rate — not to the event that triggered it |

None of the three puts a trigger list on the effect. All three give the cooldown a separate identity.

**What it buys us.** `fx.shield_grant` migrates to three atoms — `OnDamageDealt`, `OnTimer`, `OnSpawn` —
all carrying `icd_key: shield-grant`. One clock, so the actor hit at t=0 and spawning at t=100 ms fires
**once**, exactly as today. E11's byte-identity holds.

It also buys a superset of what a trigger list could express: two *different* atoms can share a clock
(PoE's Guard-skill category), which a per-atom list cannot say at all.

**What it costs.** One nullable column. `when_json.trigger` stays scalar, so E4's trigger index stays a
plain indexed column rather than a JSON-array index or a junction table.

### 14.2 `OnGranted` / `OnRemoved` are lifecycle, not content triggers

The second half of D8 was never a cooldown problem. `fx.passive_atk_flat` carries
`{OnGranted, OnRemoved}` because a permanent modifier must be un-applied when it leaves — and that
un-apply is **not content**: `EffectBag` injects `remove = true` itself when the trigger is `OnRemoved`
and the action is `ModifyStat`.

Treating those two as authorable triggers is what created the hazard, because it let an author ship the
`OnGranted` half alone and leak a permanent buff.

**So they stop being authorable.** `stat.modify` and `stat.derived` are **permanent modifiers**: they
declare **no trigger at all**, and apply/revert is a lifecycle mechanic the runtime owns. Authoring a
trigger on either is `TriggerNotAllowed`.

**E7 must compile them as `EffectType = Passive`.** `EffectDef.EffectType` defaults to `Triggered`
(`EffectModels.cs:10`), and `EffectBag` fires the lifecycle pair only when the def is `Passive` **or**
its trigger list contains `OnGranted` (`EffectBag.cs:195`, `:220`). A triggerless atom compiled with
the default type satisfies neither and would **never apply at all**. This is a compiler rule, not an
optional one.

The migration is genuinely behaviour-neutral: `fx.passive_atk_flat` is **already**
`EffectType = Passive` (`FoundationHarness.cs:225`), so its `{OnGranted, OnRemoved}` list is already
dead weight — the `Passive` clause alone fires it today. Dropping both triggers changes nothing at
runtime. `fx.patron_aura` is the shipped precedent for a passive with an empty trigger list.

Consequences, all of them improvements:

- `fx.passive_atk_flat` migrates to **one** atom, not two. The coupling becomes unenforceable-by-construction rather than unenforced.
- The trigger vocabulary splits cleanly: **4 event triggers** (`OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`) plus `OnTimer` are authorable; `OnGranted`/`OnRemoved` remain in the enum as runtime lifecycle states no atom may name. The closed count stays **7** — what changes is which of them content may write.
- **D4 dissolves.** Conditionality was 0 for passive atoms because they have no `triggerFrequency`. A permanent modifier now has no trigger *by definition*, so §7's `conditionality = 1 when no trigger` rule covers all 26 affected families as the normal case rather than an exception.
