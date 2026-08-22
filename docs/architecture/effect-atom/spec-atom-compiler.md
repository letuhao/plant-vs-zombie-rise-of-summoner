# Spec: atom-compiler (E7)

Module **E7** in the [atom effect map](../effect-atom-map.md). Depends on **E6**, **E13**, **E1** (its follow-up re-derives the five param schemas that do not match their executors — §13 D7; a wrong schema first produces a wrong grant here). First module where the layer does work.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Turn bindings into something the shipped machine already runs. **Compile what Foundation can express; hand the rest to the runner (E15).** The atom layer is a compiler, never an applier — it emits the same `EffectGrantDto` shapes the Funnel and `EffectBag` already accept, so the sealed layer is untouched.

## Design (locked on approval)

### The unit is the **atom**, not the binding

**Items have no behaviour; actors do** (definitions §0). An item is a *source* that puts atoms on an actor's effect list. So there is no binding-level coherence to preserve, and the earlier question — "what happens to an item whose atoms split across both paths" — was a non-question. Each atom on the list is classified independently.

### The compile/run split — the one architectural idea this program adds

| An atom whose `when` is… | Goes to | Runtime cost |
|---|---|---|
| an FT* trigger plus **simple filters** (side, typeId, chance, ICD) | **compiled** → an ordinary Foundation grant | **none** — `EffectBag` already does this work, LIVE-proven |
| a **predicate tree**, or dependent on per-binding state (cooldown, counter, charges, `capPerMatch`) | **runner** (E15) | one compiled predicate evaluation per candidate event |

This is not a micro-optimisation. It keeps the runner small and keeps the majority of content on a path that has been proven live through L1–L14. **Nothing is ever dropped**: a binding the compiler cannot express falls through to the runner, and a binding neither can handle is a **bind-time rejection**, never a silent no-op.

```csharp
Compile(atoms) → CompiledCatalog(Defs, Compiled, CompiledAtomIds, Runtime, Rejected)
// E7 owns the RunnerEntry contract; E15 consumes it.
```

**Corrected while building (2026-08-22):** an earlier draft said this emits `EffectGrantDto` alone. It
cannot — a grant carries only an overlay and an `effectId`, while **triggers, `EffectType` and actions
live on `EffectDefDto`**. A compiled ICD group therefore emits **one def plus one grant**. Without the
def there is nowhere to put the trigger union, and `EffectType = Passive` is unreachable, so every
triggerless permanent modifier would compile to something that never fires.

### The classification rule, stated precisely

An **atom** is compilable when all of these hold:

1. Its `when.predicate` is absent, **or** reduces exactly to the filters a Foundation grant overlay already supports — `side`, `typeId`, `actorIsKiller` — with no `Or`, no `Not`, and the subject matching what the legacy filter means.
2. Its kind maps 1:1 to an FA opcode the target runtime's sink implements.
3. No value on it is `OnApply` **with a range** — a per-hit roll needs a runner. (`OnApply` where `Min == Max` is just `Fixed` and stays compilable.)
4. It needs no per-binding state. **`icd_ms` alone does not count** — `EffectBag` already enforces grant ICD on the compiled path, so an ICD-only atom stays compilable. The runner owns ICD only for atoms it already owns for another reason.

Anything else is runner work. The classifier is a pure function, tested at E7 against a **synthetic matrix** (one atom per kind × predicate shape) — the migrated catalog does not exist until E11, so the whole-catalog golden is an **E11** acceptance row, not this module's.

**The `subject` trap (E3):** on `OnDamageDealt`, legacy `filters.side`/`typeId` mean the **damaged** entity. Rule 1 therefore only fires for `subject: target` on that trigger; `subject: actor` is runner work even though it looks identical. Getting this backwards silently inverts a filter, so it is a golden, not a comment.

### Where the compiler runs

**Server-side.** It compiles bindings and pushes compiled output; the injector **never holds content rows**. **Delivery is E19** (`compiled-push`), which extends today's `effects.grants.apply`-on-Hello path rather than inventing a transport. E7 compiles; E19 ships. Per-hit rolls still happen locally, because they are per-hit.

Consequences: content edits need a push to reach a running game, and the injector's memory stays flat regardless of catalog size.

### Baking

Compiled output is materialised into the form **E13** chose — no dictionaries, no strings in the per-hit path. `curveId` and element ids resolve to **int indices** at bake time, never string lookups at resolve time. Baking happens once per catalog revision, not per bind.

### What it must not do

The compiler emits grant shapes. It does not apply, order, merge, or mitigate anything. It does not call Unity, the Writer, `StatusExecutor`, or `EffectBag.Grant` — Secondary law is unchanged, and `guard-funnel-delta.ps1` / `guard-secondary-no-unity.ps1` keep passing untouched.


**Write it as "the Writer", never the type name.** `guard-funnel-delta.ps1` regex-matches the literal Writer type name against every `.cs` under `src/FusionRpg.Core` — **comments included** — and fails the build. Same trap for `AddPlantHp` / `AddZombieHp` / `targetPtrs`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Compiler"
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs        (new — classify + emit)
src/FusionRpg.Core/Effects/Atoms/Compilability.cs       (new — the pure classifier)
src/FusionRpg.Core/Effects/Atoms/RunnerEntry.cs          (new — the contract E15 runs and E19 ships)
src/FusionRpg.Core/Effects/Atoms/CompiledCatalog.cs     (new — baked form, int-indexed)
tests/FusionRpg.Core.Tests/Atoms/AtomCompilerTests.cs
tests/FusionRpg.Core.Tests/Atoms/CompilabilityGoldenTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Binding with no predicate, kind → FA opcode | compiled; emitted `EffectGrantDto` is **byte-identical** to today's equivalent grant |
| Predicate `side AND typeId`, `subject: target`, on `OnDamageDealt` | compiled — legacy filter equivalence |
| Same predicate with `subject: actor` | **runner**, not compiled |
| Any `Or` / `Not` | runner |
| `OnApply` range | runner |
| `OnApply` with `Min == Max` | compiled |
| Binding needing `capPerMatch` | runner |
| Kind unsupported in the target runtime | rejection, not a silent drop |
| Synthetic matrix, one atom per kind × predicate shape | every atom in exactly one bucket; **golden** records the split |
| Union of both buckets | equals the input atom set — an id-level completeness check. *Semantic* equivalence is E11's fixture parity, which is the only oracle that can catch an inverted filter |
| Bake determinism | same `catalog_revision` → identical baked bytes |
| Output shape | emits the `(EffectGrantDto[], RunnerEntry[])` pair **E19** consumes and **E15** runs |
| Guards | both pass unchanged |

## Boundaries

**Always:** classify with a pure function; keep the split golden'd; emit only shapes the Funnel already accepts; bake once per revision.

**Ask first:** widening the compilable set; moving the compiler off the server.

**Never:** drop a binding that fits neither bucket — reject it; apply, merge, or mitigate anything; put content rows on the injector; hold a string or dictionary in the baked form.
