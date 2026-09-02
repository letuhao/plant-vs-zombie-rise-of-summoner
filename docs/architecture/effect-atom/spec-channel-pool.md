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

| # | Test | Proves |
|---|---|---|
| 1 | A pooled atom resolves to **different concrete channels across two roll seeds** | The pool is live, not decorative |
| 2 | The **same seed replays byte-identically**, asserted over `ContentFingerprint()` | Law 1's reproducibility contract holds through L2 |
| 3 | `count: 6` over a 6-member pool with `allowRepeat: false` draws **each member exactly once** | Draw-without-replacement is real |
| 4 | `price(pooled)` equals the weighted mean × count, computed in a hand-worked fixture | The pricing rule is arithmetic, not a vibe |
| 5 | **Planted violation:** a pool member naming `crit.rat` where `crit.rate` was meant is **refused at load** | The check that `stat.derived` never had. **A test that cannot fail is not a guardrail** |
| 6 | **Planted violation:** `count: 7` on a 6-member pool with `allowRepeat: false` is refused, not clamped | Silent clamping is the defect class this repo removes |
| 7 | An atom with a **concrete** channel is unchanged — same id, same price, same hash | The concrete form is not deprecated and does not move |
| 8 | Overflow: a pool whose weighted mean would exceed `long` **throws**, never wraps | `AGENTS.md` numeric rule |

**Tests never call a model** — this module has no model stage at all.

---

## 6. Acceptance criteria

1. `data/seed/channel-pools/pools.v1.json` loads through `AtomSeedFile`, and `SeedScanner.OwnedFolders`
   sweeps its folder.
2. `params.channel` accepts a concrete string **or** a pool reference; both validate, both price, both
   compile to the same opcode.
3. Every one of the five refusals in §3.3 fires on a planted case and names the offending id.
4. A pooled atom prices as the weighted mean × count, integer-exact, with overflow throwing.
5. Rerunning a roll over unchanged inputs is **byte-identical**, proven by hash.
6. **No concrete atom's id, price or content hash changes.** This module is additive.
7. `ContentValidation` gains `pool-spread` and reports it without failing a build by itself.
8. **The 98 authored families in `data/seed/items/affix-families/` are reconciled** — every
   element-typed family resolves to a declared pool, and one that resolves to none is **reported by
   id**. Map §12 assigns this to E30 and it had no criterion until 2026-09-03.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **E42** `units-correction` | ⛔ **Hard prerequisite**, as map §12 and §14 both state. This module
prices `combat.power.*` / `combat.defense.*` / `combat.shield.*`, whose units row in `definitions.md`
is known-wrong. Filing it as a hazard rather than a dependency was an error corrected 2026-09-03 |
| **effect-pipeline module 2** | ⛔ **Dependency**, per the §4 correction — it owns the resolve this
module's tests 1–3 assert |
| **E28** `param-parity` | Pools are only useful across the params E28 unblocks — notably `resource.delta` over all six resources |
| **E29** `kind-value-guard` | §3.3's member check is the same machinery; E29 establishes the per-kind pattern E30 extends to members |
| **effect-pipeline modules 1+2** | Own the slot declaration and the resolver. **E30 must not implement one** |
| **`definitions.md` §2 units** | Still carries the row the item program corrected 2026-08-22 (`combat.power.*` are **flat game units**, not resolver points). `DESIGN-GATE.md` makes that file win over any spec, and **this module prices those channels** — correct it before authoring magnitudes |
| **Stale instances** | A `catalog_revision` bump makes every rolled `effect_instance` unbindable (`StaleInstance`). Pre-existing for any content change; state it in the rollout note |
