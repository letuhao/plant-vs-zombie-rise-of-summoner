# Spec: channel-pool (E30)

**Status: DRAFTED 2026-09-03**, from [effect-atom-ideal.md](../effect-atom-ideal.md) §W7.9 and the
capability map's [§12](../effect-atom-map.md). Module **E30**, Wave 7. Depends on **E28**, **E29**.

**What it owns: layer 2 — the missing one.** An atom's `params.channel` may today name exactly one
concrete channel. This module lets it name a **pool** instead: a named, authored set of channels with a
count and per-member weights, resolved to a concrete channel **at roll time, per player**. It owns the
pool vocabulary, the schema addition, the validation, and **pricing a pooled atom** — not the resolver,
which is `effect-pipeline` module 2's.

---

## 1. Why this module exists, restated inline

The owner's four-layer model, recorded verbatim in
[`effect-pipeline-ideal.md`](../effect-pipeline-ideal.md) §5:

> *"layer 1 define the shape of container, how many atom effect, chance it appear · **layer 2 define the
> pool, how many derived stats, chance it appear** · layer 3 define the range of value · layer 4 make
> resolve number, resolve derived stats, resolve list of atom in 3 layer above"*

and the worked example that makes the shape concrete:

> *"runtime read the json it shouldn't read `+x hp`, **that wrong**. it should read `+x derived stats in
> this pool` … the seed is `+x element power of Y`, **Y is a pool of [6 type of element]**."*

**L1, L3 and L4 are built. L2 is not.** Without it, *"element power of some element"* is inexpressible:
every element is a different atom, so `element master of X` is not one affix with a hole in it — it is
six unrelated candidates. The same absence shows up as a budget defect: `+15% to all resistances` needs
six atoms in six groups, **consuming six pool rolls for one affix line**.

**This is Law 1 applied to the channel axis** — seedsmith emits a seed, the runtime rolls the concrete
object per player. A pre-multiplied cartesian is a second roll implemented at authoring time, which
Law 1 forbids.

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| A ref may be a **slot** naming a domain, resolved at roll time | **built** | `AffixValidator.cs:14-23`; `RpgStore.Containers.cs:466-467` resolves `DomainMembers("element")` to the six elements |
| *"a slot's pattern names a family/variant, **never a concrete tier** — tier resolves later"* | **built** | `AffixValidator.cs:19-23` |
| The same idea on the **channel** axis | ⛔ **real gap** | no pool type, no schema key, no resolver hook |
| 98 atom families authored, all 12 kinds | **built**, unswept | `data/seed/items/affix-families/*.json` — `SeedScanner.OwnedFolders` does not include `items` |
| `CostFunction.Price` keys on `(kindId, channel)` | **built** | `CoefficientTable.cs:14` — **and it cannot price an atom with no concrete channel** |
| 267 registered derived channels | **built** | `DerivedStatRegistry.CreateDefault().AllRegistered` |

**The slot half proves the pattern ships.** *One row, six outcomes* is not a proposal.

---

## 3. The contract

### 3.1 The pool artifact

`data/seed/channel-pools/pools.v1.json`, in the repo's standard envelope:

```jsonc
{
  "schemaVersion": 1,
  "kind": "channel-pool",
  "entries": [
    {
      "id": "pool.element-power",
      "note": "The six concrete element power channels. omni is NOT a member — it is the untyped case.",
      "members": [
        { "channel": "combat.power.fire",  "weight": 1000 },
        { "channel": "combat.power.ice",   "weight": 1000 },
        { "channel": "combat.power.air",   "weight": 1000 },
        { "channel": "combat.power.earth", "weight": 1000 },
        { "channel": "combat.power.light", "weight": 1000 },
        { "channel": "combat.power.dark",  "weight": 1000 }
      ]
    }
  ]
}
```

- **`weight` is per-mille and structural to the draw**, not a magnitude. It is a balance surface, so the
  file lives under `data/` and is published, never hand-edited (`tunables-ssot.md` T4).
- **A pool is a set of channels, never of kinds.** Crossing kinds would make the atom's opcode ambiguous.
- **Every member must be a registered channel.** Enforced — §3.3.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — `pool.element-power` has **6** members; `omni` is not one

The `variants: { "generate": "elements+omni" }` on every element-typed family is **not** an argument for
a 7th pool member, because the two axes mean different things:

| | What it is | Where it resolves |
|---|---|---|
| `omni` | the **untyped** channel — *"power against everything"* | a **concrete** channel on a **concrete** atom, exactly as today |
| the 6 elements | *"power against **one** element, and which one is rolled"* | the **pool** |

The generated code says the same thing structurally: `CombatChannelEntry` emits the omni channel on its
own line and then loops the roster (`DerivedStatChannels.cs:423-425`), and `ElementRoster.Concrete` is
the six. **A pool containing `omni` would mean *"element power of some element, or possibly of all of
them"*, which is not a draw over a domain — it is two different affixes sharing one row.**

**So a family declaring `elements+omni` expands to exactly two atom rows per tier, not seven and not
one:** the `omni` variant, keeping its concrete `combat.power.omni`, and one pooled row whose
`params.channel` is `{ "pool": "pool.element-power" }`. E43 owns emitting them; E30 owns the pool being
6 members wide so that it can.

**What would overturn it:** a design that wants *"a random element **or** omni"* as a single affix — at
which case the answer is still not a 7th member, it is a second pool (`pool.element-power-or-omni`)
declared beside the first, so the two intents stay separately weighted and separately priced.

### 3.2 How an atom names one

`params.channel` accepts **either** form. The concrete form is unchanged and is not deprecated:

```jsonc
{ "kind": "stat.derived", "params": { "channel": "combat.power.fire", "op": "flat", "amount": 150 } }
{ "kind": "stat.derived", "params": { "channel": { "pool": "pool.element-power", "count": 1 },
                                      "op": "flat", "amount": 150 } }
```

- **`count`** — how many members are drawn, default `1`, **floored at 1**. `count > 1` is how
  *"+15% to all resistances"* becomes one atom.
- **`count` may exceed the member list only if `allowRepeat` is true**, which defaults false. Otherwise
  the draw is without replacement and a `count` above the member count is a **load-time refusal**, not a
  silent clamp.
- **No magnitude appears in the pool reference.** The magnitude stays in `amount`, resolved by L3 as
  today.

### 3.2a ⛔ DECIDED 2026-09-03 — what a resolved `count > 1` atom executes as

**Found, not assumed, before this was answered:** `InjectorEffectActionSink.cs:93` reads `channel` as
ONE string (`JsonOverlay.GetString(p, "channel")`) — checked directly, not inferred. No consumer of a
`ResolvedAtom` anywhere in the repo reads an array-valued channel. `Resolver.RollValues` today does not
special-case a pool-object `channel` value at all — it falls into the generic
`frozen[prop.Name] = prop.Value.Clone()` branch (the value is not `ParamKind.Value`), so a pooled
reference currently freezes **verbatim as the raw pool-object JSON**, unread and unexecuted. This is the
gap that made count > 1 undecided: nothing in the shipped architecture said what "one atom, many
channels" becomes at the moment it has to run on one entity's stat sheet.

**The decision: a pooled `channel` reference expands into `count` separate `ResolvedAtom` entries at
resolve time — never one entry carrying an array.** Every existing consumer (`InjectorEffectActionSink`,
`AtomDerivedSubsystem`, `BattleStatComposer`, anything else that reads a compiled atom) stays completely
unmodified: by the time any of them see a resolved atom, its `channel` is a single concrete string, the
same shape it has always been.

**This is not a new mechanism — it is `Resolver`'s own existing one, recognised rather than invented.**
An affix bundle with two refs already produces two `ResolvedAtom`s from one drawn affix
(`Master_of_fire_and_ice_resolves_as_one_correlated_draw`, `ResolverTests.cs`) — "one authored unit,
several resolved atoms" is the pattern this resolver already implements for slot-bearing bundles. A
pooled channel with `count = N` is the same shape at a different layer: instead of a bundle naming N
refs, one atom names N *channel draws* of itself.

**The magnitude is rolled ONCE, not once per drawn channel.** *"+15% to all resistances"* means every
resistance gets the SAME 15%, not six independently-rolled percentages — the intuitive game-design
reading, and the only one under which the phrase "+15%" is a single number rather than a range
description. Concretely: `RollValues` rolls `amount` exactly as it does today (one `OnInstantiate`/
`Fixed` resolution, one `atom.value` stream draw), then stamps that ONE frozen value onto every expanded
copy. `RunnerBindings`/whatever hash discipline stores it, each of the `count` copies is a complete,
independent `ResolvedAtom` — same `atom_id`, same frozen `amount`, different `channel` — not a shared
reference.

**Where the draw happens: a fifth named stream, `channel.pool`, derived exactly like the other four
(`SeededRng.DeriveStream(rollSeed, "channel.pool." + container.ContainerId)`, mirroring `affix.slot`/
`affix.draw`/`affix.tier`/`atom.value`'s own convention).** It is a stream of the RESOLVER
(`effect-pipeline` module 2), not of E30, per §4's own boundary — E30 owns the reference and its price,
module 2 owns turning it into concrete channels, exactly as it already owns turning a slot reference
into a concrete element. Weighted, without replacement unless `allowRepeat`, using the exact
weighted-pick shape `Resolver.PickOne`-equivalent logic already implements for affix draws (module 2's
own implementation, not copied here) — no new draw algorithm, the same one this program already has.

**Where in the five-step order this runs:** after step 4 (tiers) resolves the atom's identity, as part
of step 5 (values) — a pooled `channel` is a property of the atom being rolled, not of which atom gets
drawn, so it cannot run any earlier than the point where `RollValues` already reads every other param.

**What this changes about `ResolvedDraw`'s own contract:** today, one entry in the tiered atom-id list
produces exactly one `ResolvedAtom`. After this, one entry produces **one-to-`count`** `ResolvedAtom`s —
a widening, not a breaking change, since every existing caller already treats `ResolvedDraw.Atoms` as a
list of unknown length. The reproducibility law is unaffected: `(container_id, catalog_revision,
roll_seed, variant)` still determines every stream deterministically, `channel.pool` included.

**What would overturn it:** a design wanting `count` independently-rolled magnitudes instead of one
shared roll (a "roll N separate charms" reading rather than "one enchant touching N things") — that is
a different game-design intent needing its own field (e.g. an `independentRolls: true` flag), not a
change to this decision, and no authored content or brief has asked for it.

**What this decision does NOT do:** it does not implement `effect-pipeline` module 2's own resolve step
— that is `Resolver.cs`'s code, sequenced with the rest of that module's work, not written here. It only
answers the question that was blocking it: what the resolved output IS, so the implementation has a
target to build toward instead of a guess.

### 3.3 Validation — the rules E29 gives this module a home for

Refusals, all at **load**, all naming the offending id:

1. `pool` names an id not in the pool catalog → **`BadParamValue`**, naming the unknown pool id.
   > **⛔ CORRECTED 2026-09-03.** This originally invented a 34th code, `UnknownPool`. **The list is
   > closed at 33** (`definitions.md` §10: *"Thirty-three codes. Adding one is a reviewed change."*)
   > and `AtomKindRegistryTests.cs:33` asserts `Assert.Equal(34, reasons.Length)` — 33 plus `None` —
   > so the addition would have gone red. `spec-projectile-control.md` §3 states the same rule
   > independently. **No new code.**
2. A pool member is **not a registered derived channel** → `BadParamValue`. This is the `stat.derived`
   check `AtomRowValidator.cs:296` defers to *"G6's job"* — which never runs for it — and here it applies
   to every member of every pool.
3. A member's channel has a compose kind that **does not accept the atom's `op`** → `ParamNotHonoured`.
   The existing single-channel check at `AtomRowValidator.cs:289-305` runs **per member**; a pool whose
   members disagree about which ops they accept is refused rather than partially honoured.
4. `count < 1`, or `count > members.Count` with `allowRepeat: false` → `BadParamValue`.
5. An empty `members` array → `BadParamValue`. A pool that can draw nothing is a defect, not an
   expression of *"no effect"*.

### 3.4 Pricing — the part with no precedent

`CostFunction.Price` takes an `AtomRow` and looks up `(kindId, channel)`. **A pooled atom has no concrete
channel at authoring time**, so this module must define the price of the *reference*:

> **`price(pooled) = count × weighted_mean(price(member) for member in pool)`**, weights being the pool's
> own per-mille weights.

Three properties, each load-bearing:

- **It is exact under the existing integer contract.** Weights are per-mille; widen to `long` before
  multiplying, **divide by 1000 last, exactly once**, and let overflow throw.
- **It is the expected value of the roll**, so a pooled atom and the concrete atoms it can resolve to
  price consistently — an author cannot dodge a budget by pooling.
- **It is stable under a channel rename.** The 267-channel vocabulary moved four times in nine days
  (99 → 256 → 259 → 261 → 267, the last a rename with a shim). A pool reference survives a member
  changing; 196 pinned rows would not.

**Where the coefficients disagree between members, the mean is the honest number and the spread is
reportable.** `ContentValidation.Drift` gains a `pool-spread` finding when a pool's members differ by
more than a stated per-mille — a pool of wildly unequal channels is a content defect, not a pricing one.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — `pool-spread` fires above **250‰**

```csharp
/// A pool whose priciest member is more than 25% away from the pool's own weighted mean. The number
/// is DriftTolerancePercent, deliberately: the pricing error a pool inherits IS the pricing error
/// Drift already tolerates, so one band answers both rather than two bands disagreeing.
public const int PoolSpreadTolerancePerMille = 250;   // == DriftTolerancePercent * 10
```

**Why 250‰ and not a number picked to feel right.** It is `ContentValidation.DriftTolerancePercent`
(`ContentValidation.cs:44` — `25`), restated in the per-mille the pool weights already use. That
constant carries its own written reasoning (`:88-93`): *"Not 5%: the cost function is knowingly wrong
by ~12.5% on multiplicative pairs… Not 50%: that cannot detect a real mistake. 25% catches
order-of-magnitude errors."* **A pool's members are priced by the same cost function with the same
known error**, so a second, differently-chosen band would be a second answer to a question already
answered — and the two would drift.

**It is neutral today, by construction.** All 20 rows in `CoefficientTable.Authored()` are
`CoeffMilli = 1000` (`CoefficientTable.cs:125-147`), and `stat.derived` has a **single channel-less
row** (`:136`), so every derived channel prices identically and **every pool's spread is exactly 0**.
The finding cannot fire on today's data. That is the right property for a threshold shipping ahead of
the coefficients it measures: it costs nothing now and starts working the moment E44 fits them.

**What re-tunes it:** E44's fitted coefficient set. When `stat.derived` gains per-channel rows, run the
finding over every declared pool and read the distribution — if the honest spreads cluster above 250‰
the band is too tight and the number moves; if nothing fires, it is doing nothing and should tighten.
**Either way it is one `const`, and it should move only with a measurement attached.**

---

## 4. What this module must NOT do

- **Implement the resolver.** `effect-pipeline` module 2 `resolution-order` owns *slots → affixes →
  atoms → tiers → values*, with a named RNG stream per layer. **E30 defines the reference and its
  price; module 2 turns it into a concrete channel.** The seam is normative
  ([`effect-atom-ideal.md`](../effect-atom-ideal.md) §W7.11.1).
  > **⚠️ CORRECTED 2026-09-03 — this boundary conflicted with §5's own tests.** Tests 1, 2 and 3
  > assert *resolution* behaviour (different channels across seeds, byte-identical replay, draw
  > without replacement), and §3.4 named an RNG stream. **A spec cannot forbid building the thing its
  > own acceptance depends on.** Resolved: **E30 declares a dependency on effect-pipeline module 2**,
  > and those become integration tests over the two together. Checkpoint I — which the map calls *"the
  > one worth failing the wave over"* — is verified jointly, never by E30 alone.
- **Emit a corpus.** No expansion, no cartesian, no generated rows. That was the error W7.9 corrected.
- **Let a model choose a pool, a weight or a count.** Law 2. These are authored data.
- **Introduce a second roll.** The draw runs on `Instantiator`'s existing `AtomRandom` stream and is
  frozen into the `InstanceRow`, under the same reproducibility contract over
  `(container_id, catalog_revision, roll_seed)`.
- **Use `float` anywhere.** `long` for every magnitude, widen before multiplying, divide by 1000 last,
  overflow throws.
- **Cap a magnitude.** `count`'s floor of 1 is a **structural** bound (a draw of zero members is not an
  effect) and must carry a comment saying so, per `AGENTS.md`.

---

## 5. Testing strategy

**⛔ Tests 1-3 need `effect-pipeline` module 2's own `channel.pool` resolve step (§3.2a, decided
2026-09-03 — not yet implemented), so they live in that module's test suite, not this one's**, the same
split `Master_of_fire_and_ice_resolves_as_one_correlated_draw` already draws between "what a reference
means" (E30) and "what it resolves to" (module 2). Listed here because they are this module's
acceptance bar, proven jointly.

| # | Test | Proves |
|---|---|---|
| 1 | A pooled atom resolves to **different concrete channels across two roll seeds** | The pool is live, not decorative |
| 2 | The **same seed replays byte-identically**, asserted over `ContentFingerprint()` | Law 1's reproducibility contract holds through L2 |
| 3 | `count: 6` over a 6-member pool with `allowRepeat: false` draws **each member exactly once**, as `count` separate `ResolvedAtom`s sharing one rolled magnitude (§3.2a) | Draw-without-replacement is real, and the "one roll, N channels" decision is the shape actually built |
| 4 | `price(pooled)` equals the weighted mean × count, computed in a hand-worked fixture | The pricing rule is arithmetic, not a vibe |
| 5 | **Planted violation:** a pool member naming `crit.rat` where `crit.rate` was meant is **refused at load** | The check that `stat.derived` never had. **A test that cannot fail is not a guardrail** |
| 6 | **Planted violation:** `count: 7` on a 6-member pool with `allowRepeat: false` is refused, not clamped | Silent clamping is the defect class this repo removes |
| 7 | An atom with a **concrete** channel is unchanged — same id, same price, same hash | The concrete form is not deprecated and does not move |
| 8 | Overflow: a pool whose weighted mean would exceed `long` **throws**, never wraps | `AGENTS.md` numeric rule |
| 9 | The declared pool set **equals** the element-expanded stem set derived from the 98 families, minus the unregistered ones | §6.1 — the list is checked against the corpus, never restated by hand |
| 10 | `pool.element-power` has **6** members and `omni` is absent | §3.1's decision, pinned |

**Tests never call a model** — this module has no model stage at all.

---

## 6. Acceptance criteria

1. `data/seed/channel-pools/pools.v1.json` loads through `AtomSeedFile`, and `SeedScanner.OwnedFolders`
   sweeps its folder.
2. `params.channel` accepts a concrete string **or** a pool reference; both validate, both price. "Both
   compile to the same opcode" is proven once module 2's `channel.pool` step exists (§3.2a) — a pooled
   reference resolves into `count` ordinary, concrete-channel `ResolvedAtom`s, so nothing downstream of
   the resolver ever sees anything but the opcode it already knows.
3. Every one of the five refusals in §3.3 fires on a planted case and names the offending id.
4. A pooled atom prices as the weighted mean × count, integer-exact, with overflow throwing.
5. Rerunning a roll over unchanged inputs is **byte-identical**, proven by hash — provable once module
   2's `channel.pool` stream exists; the design that makes it provable is decided (§3.2a).
6. **No concrete atom's id, price or content hash changes.** This module is additive.
7. `ContentValidation` gains `pool-spread` and reports it without failing a build by itself.
8. **The 98 authored families in `data/seed/items/affix-families/` are reconciled** — every
   element-typed family resolves to a declared pool, and one that resolves to none is **reported by
   id**. Map §12 assigns this to E30 and it had no criterion until 2026-09-03. **The pool list that
   makes this satisfiable is §6.1, decided 2026-09-03.**

### 6.1 ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the full pool list is **12**, declared, not scoped away

§3.1 declared **one** pool while acceptance 8 required *"every element-typed family resolves to a
declared pool."* Reading all 98 families, the 33 `stat.derived` entries name **14 distinct
element-expanded channel stems**, so acceptance 8 was unsatisfiable against a one-entry file. **The
decision is to declare the pools, not to scope the acceptance** — the file is data, one entry per stem,
and a stem that has no pool is exactly the *"reported by id"* case acceptance 8 already describes.

**Twelve entries. The ids are mechanical**, so a test can derive the expected set rather than restate it:
strip `combat.`, replace `.` with `-`, prefix `pool.element-`. That rule reproduces §3.1's own
`pool.element-power` unchanged.

| Pool id | Channel family | Named by |
|---|---|---|
| `pool.element-power` | `combat.power` | `atom.elpw-override`, `atom.elpw-attune` |
| `pool.element-defense` | `combat.defense` | `atom.elemental-defense`, `atom.ward-harden` |
| `pool.element-accuracy` | `combat.accuracy` | `atom.precision`, `atom.prec-fixation`, `atom.prec-truesight` |
| `pool.element-dodge` | `combat.dodge` | `atom.evasion`, `atom.evd-flinch`, `atom.evd-brace` |
| `pool.element-crit-rate` | `combat.crit.rate` | `atom.keen-edge`, `atom.prec-verdict` |
| `pool.element-crit-resist` | `combat.crit.resist` | `atom.evd-harden`, `atom.evd-scar`, `atom.stoicism`, `atom.ward-brace` |
| `pool.element-crit-damage` | `combat.crit.damage` | `atom.cruelty`, `atom.prec-reckoning` |
| `pool.element-crit-resist-damage` | `combat.crit.resist.damage` | `atom.evd-seal`, `atom.evd-husk`, `atom.padding` |
| `pool.element-shield-capacity` | `combat.shield.capacity` | `atom.shield-capacity`, `atom.shld-surge` |
| `pool.element-shield-toughness` | `combat.shield.toughness` | `atom.shield-toughness` |
| `pool.element-shield-pen` | `combat.shield.pen` | `atom.shield-pen`, `atom.shld-breach` |
| `pool.element-shield-regen` | `combat.shield.regen` | `atom.shield-regen`, `atom.shld-cycle` |

Every one of the twelve is in `DerivedStatChannels.CombatChannelFamilies`
(`DerivedStatChannels.cs:186-216`), and every one takes the same six members —
`ElementRoster.Concrete`, weights flat at `1000` — for the reason §3.1's `omni` decision gives.

**The other 16 registered combat families get no pool yet**, and that is deliberate: no authored family
names them, and an unreferenced pool is an orphan by `ContentValidation`'s own logic. **The rule for
adding one is stated rather than left to judgement:** a family naming an element-expanded stem with no
pool is a `BadParamValue` refusal naming the missing pool id, and the fix is one entry in the file
following the id rule above. That is a one-line change, which is why declaring 16 unused entries now
would buy nothing.

**The two stems that get no pool and are not a follow-up here:** `combat.power.pierce` and
`combat.power.overflow` are **not registered channel families at all** — the three families naming them
are refused by E29's guard before a pool is ever consulted. They are that module's §5.1 follow-up, not
a missing pool.

**What would overturn the 12:** E43's expansion finding a 13th stem, which means a family was edited
after this count. The test derives the expected pool set from the family corpus, so that shows up as a
failing test with the stem's name in it — the correct way for this to be wrong.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **E42** `units-correction` | ✅ **CLOSED 2026-09-03.** `definitions.md` §2 and `atom-family-library.md`
§2a now correctly state `combat.power.*` / `combat.defense.*` / `combat.shield.*` are flat game units,
not resolver points, with a doc-drift test pinning it. This module's magnitudes may now be authored
against the corrected reference |
| **effect-pipeline module 2** | ⛔ **Dependency**, per the §4 correction — it owns the resolve this
module's tests 1–3 assert |
| **E28** `param-parity` | Pools are only useful across the params E28 unblocks — notably `resource.delta` over all six resources |
| **E29** `kind-value-guard` | §3.3's member check is the same machinery; E29 establishes the per-kind pattern E30 extends to members |
| **effect-pipeline modules 1+2** | Own the slot declaration and the resolver. **E30 must not implement one** |
| **`definitions.md` §2 units** | ✅ **CLOSED 2026-09-03 by E42** — see the row above |
| **Stale instances** | A `catalog_revision` bump makes every rolled `effect_instance` unbindable (`StaleInstance`). Pre-existing for any content change; state it in the rollout note |
