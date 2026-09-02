# Spec: runner-def-emit (E26)

**Status: DRAFTED 2026-09-03**, from [effect-atom-ideal.md](../effect-atom-ideal.md) §W7.2 defect 1 and
the capability map's [§12](../effect-atom-map.md). Module **E26**, Wave 7. **No dependencies.**

**What it owns: making the runner path deliverable.** Today every atom the compilability classifier
routes to `AtomRunner` throws `unknown effect_id` the moment it is granted. This module emits a def per
`RunnerEntry`, from that entry's own `Params`, so the runner path can execute at all.

---

## 1. The defect, in the code's own words

`AtomRunner.cs:206-209` states it plainly — this is not an inference:

> *"The def for a runner atom is **not emitted by anything yet** — E7 emits defs only for the compiled
> path. Until **E19** ships one per runner entry (from `RunnerEntry.Params`), a dispatch needs a def
> already in the catalog under the atom id."*

The chain, verified end to end:

| Step | Evidence |
|---|---|
| The push payload ships only compiled defs | `AtomPushCodec.cs:170-171` — `payload.Defs.AddRange(catalog.Defs)` then `catalog.Compiled` |
| `AtomRunner.Dispatch` builds a grant with `EffectId = entry.AtomId` | `AtomRunner.cs:216` |
| `EffectBag.Grant` throws on an unknown id | `EffectBag.cs:196-197` — `_catalog.Get(grant.EffectId) ?? throw new InvalidOperationException("unknown effect_id: " + …)` |

**What routes to the runner**, from `Compilability.Classify` — each is a shape a content corpus wants:

- a **per-hit roll range** (`Compilability.cs:99-100`)
- any of `capPerMatch` · `charges` · `everyHits` · `maxStacks` (`Compilability.cs:61,88-90`)
- a **predicate that does not reduce to legacy filters** (`Compilability.cs:104-112`)

**Sorted: wiring gap.** The runner exists, the classifier exists, the dispatch exists. Only the def is
missing — and its absence turns an authoring decision into a runtime crash.

---

## 2. Why this is a Wave 7 prerequisite, not a nice-to-have

**W7-D2, owner-decided:** *fix E19's def emission first, then generate.* The reasoning recorded in the
ideal is that content should be authored against the **full** atom schema rather than a subset a later
module has to widen.

**And E30 sharpens it: a pooled atom is runner-shaped.** A channel drawn at roll time is not a constant
the compiled path can fold, so the pool work lands squarely on the path this module repairs.

---

## 3. The contract

### 3.1 What is emitted

For each `RunnerEntry` reachable from an accepted binding, emit **one `EffectDef`** whose:

- **`EffectId` is the entry's `AtomId`** — exactly what `AtomRunner.Dispatch` puts in
  `EffectGrantDto.EffectId` (`AtomRunner.cs:216`). A different id reintroduces the same throw.
- **`Actions` come from `AtomCompiler`'s existing translation of the entry's `Params`** to the kind's
  opcode (`AtomCompiler.OpcodeOf`, the 12↔12 bijection). **This module adds no opcode and no kind.**
- **Overlay-carried values are left unresolved in the def.** `AtomRunner` already puts rolled values on
  the grant overlay (`AtomRunner.cs:210-214`), and `EffectOverlayMerge.TryValidateOverlayForDef` checks
  the overlay against the def — so the def must declare the keys the overlay will carry, or a correct
  grant is refused mid-flush.

### 3.2 Where it is emitted

`AtomPushCodec.BuildPayload` (`AtomPushCodec.cs:146-178`), beside the two existing lines:

```
payload.Defs.AddRange(catalog.Defs);       // compiled — unchanged
payload.Grants.AddRange(catalog.Compiled); // unchanged
payload.Defs.AddRange(RunnerDefs(bindings)); // NEW — one per RunnerEntry
```

**The payload stays binding-scoped.** `AtomPushService.cs:59-69` builds the catalog from only the atoms
behind accepted bindings, never `ListAtoms()`, so this adds defs for content that is actually bound and
nothing else.

### 3.3 The staleness trap this module must not fall into

`AtomImporter` reports *"nothing changed"* when only compiler **code** changed, because the content hash
covers **seed data** (`tasks/effect-atom-todo.md:587-591`). **E26 is exactly a compiler-code change**, so
it would never trigger a re-push and the fix would look inert on a live host.

**This module owns the fix**: the push must key on something that moves when the emitter changes — a
codec/emitter version stamped into the payload alongside `CatalogRevision`, so
`receiverRevision == catalog.CatalogRevision` (`AtomPushCodec.cs:164-168`) cannot short-circuit past new
code. **State the chosen mechanism in the implementation; do not leave it to the deploy script.**

---

## 4. What this module must NOT do

- **Add a kind or an opcode.** The 12↔12 bijection holds; a runner def is a *translation*, not a new
  capability.
- **Resolve a rolled value at emit time.** The roll is the runner's, per hit, on its own stream. Folding
  it into the def would be a second roll and would break replay.
- **Widen `Compilability.Classify`.** What routes where is not this module's decision.
- **Let a magnitude be `float`, or divide before the last step.** `long` throughout; overflow throws.
- **Silently skip an entry it cannot translate.** An untranslatable `RunnerEntry` is a **refusal with its
  id**, never a missing def that resurfaces as `unknown effect_id` at grant time — that is the exact
  failure this module exists to end.

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | An atom with a **per-hit roll range** is granted and executes end to end | The headline defect is closed |
| 2 | One test per runner trigger — `capPerMatch`, `charges`, `everyHits`, `maxStacks`, non-legacy predicate | Every route into the runner is covered, not just the easy one |
| 3 | The emitted def's `EffectId` **equals `entry.AtomId`** | The id contract that `AtomRunner.Dispatch` depends on |
| 4 | A grant whose overlay carries rolled keys **passes `TryValidateOverlayForDef`** | The def declares what the overlay will bring |
| 5 | **Planted violation:** an untranslatable `RunnerEntry` produces a **named refusal**, not a missing def | Silent omission is refused |
| 6 | **Regression:** a compiled-path atom's def, id and content hash are **unchanged** | The module is additive |
| 7 | Changing the emitter **causes a re-push** on a host at the current `catalog_revision` | §3.3's staleness trap is actually closed |

### ⛔ Two CI facts this module must handle, not dodge

- **`EffectCatalogExecutionParityTests` asserts `Assert.Empty(compiled.Runtime)`**
  (`EffectCatalogExecutionParityTests.cs:49`).
  > **⚠️ CORRECTED 2026-09-03.** This originally read *"E26 exists to violate it."* **It does not, as
  > specced.** That test compiles only shipped `data/seed/atoms/**/fx-*.json`, all 21 of which classify
  > **Compiled** today, and §4 forbids widening `Compilability.Classify`. So `compiled.Runtime` stays
  > empty and the assertion never fires.
  >
  > **This module owes one of two things and must pick:** ship a **runner-shaped fixture atom** so the
  > repaired path is exercised by real content, or **withdraw the claim** and leave the assertion to
  > E43, whose generated output first trips it. **Shipping the fixture is the better answer** — a
  > repaired path with nothing exercising it is D6's exact failure mode: accepted, then nothing
  > forever. **Renaming a file to slip past the gate remains unacceptable either way.**
- **The injector is not built by CI.** Anything landing in `src/FusionRpg.Injector` needs a local build
  and an owner-run live check; say so in the task rather than assuming green CI means shipped.

---

## 6. Acceptance criteria

1. An atom carrying a per-hit roll range is granted **without throwing** and its effect applies.
2. Each of the five runner routes in §5 test 2 has a passing end-to-end test.
3. An untranslatable entry is refused **by id**, and a test plants one.
4. Either a runner-shaped fixture atom ships and `EffectCatalogExecutionParityTests` asserts runner defs
   **exist**, or the claim is withdrawn in favour of E43 — **stated explicitly, never left ambiguous**.
5. A compiler-code change triggers a re-push on a host already at the current revision.
6. No compiled-path atom's def, id or content hash moves.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. E26 may run first in Wave 7 |
| **Unblocks** | **E30** — a pooled atom is runner-shaped; and W7-D2 makes this a prerequisite of all generation |
| **battle-timeline B25/B26** | Both rewrite `EffectRuntime`'s `_dotAccum`/`_shieldAccum` grids while this touches the grant path. `effect-atom-map.md` §6's H1 hazard: a mover overlapping a freezer means neither can attribute a change |
| **Stale instances** | Any `catalog_revision` bump makes rolled `effect_instance` rows unbindable. Pre-existing; note it in the rollout |
