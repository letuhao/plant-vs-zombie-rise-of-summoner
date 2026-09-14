# Spec: `lawn-action-bridge`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `basic-attack-seed` (cost authoring), and see "Cost is a different problem" below

> **Rewritten 2026-09-13 after an adversarial pass demolished the first draft.** That draft was built
> on a misread grep and proposed transport machinery this module does not need. The correction is
> recorded here rather than quietly swapped, because the wrong version was briefly in the map.

---

## What the first draft got wrong

**Claim:** *"the injector has no reference to the action stack"*. **False.**

```
src/FusionRpg.Injector.MelonLoader.39/…csproj:45   <ProjectReference ..\FusionRpg.Core\FusionRpg.Core.csproj />
src/FusionRpg.Injector.BepInEx/…csproj:39          <ProjectReference ..\FusionRpg.Core\FusionRpg.Core.csproj />
```

`grep FusionRpg.Core.Actions src/FusionRpg.Injector → 0` measured **usage, not reachability**.
`FusionRpg.Injector.csproj` is a shim over the host projects, all of which reference `FusionRpg.Core`,
and the injector already `using`s ~25 Core namespaces including `Core.Battle` and
`Core.Battle.Timeline`. **`using FusionRpg.Core.Actions;` compiles today.** There is no reference to
add and no "broad reference" to gate.

**Claim:** hydrate a compiled row over the existing cold edges. **Impossible.**
`CompiledAction.Condition` is an `ICompiledPredicate` (`CompiledAction.cs:44`), an interface with
`bool Evaluate(ref FactReader)` — a behavioural object with interned indices, not data. It cannot
cross a wire, and re-compiling on arrival is banned by the same draft's own boundary. The whole
cache / DTO / §2.16 trigger-set apparatus was answering a question this module does not have.

## Objective

`act.attack` **is not a database row.** It is hand-built in Core at `BattleRunState.cs:64-81`, and
the comment at `:45-49` says why: *"the basic attack has no rung, no container, no atoms — forcing it
through the real-content compiler would mean inventing fake rung/container rows."* `TargetSpecCompiler.Compile`
and `PredicateCompiler.Always` run **locally**.

So the real work is small and local:

1. **Promote** `BasicAttackCompiled` out of that private nested field into a public factory in
   `FusionRpg.Core.Actions`, so the injector and `BattleRunState` share one construction and no second
   copy can drift.
2. **Configure `ActionTimingPolicy` injector-side** — see below; without it the first touch throws.

No transport. No DTO. No cache. No trigger set. No cold edge. No HTTP.

Success: the injector can construct the compiled basic-attack row locally, on the lawn, with no Server
round trip — because there is nothing to fetch.

## The prerequisite the first draft missed — it throws, it does not fail closed

```csharp
// ActionTimingPolicy.cs:15-17
public static ActionTimingTuning Tuning => _tuning ?? throw new InvalidOperationException(
    "ActionTimingPolicy.Configure(...) has not run. …there is no built-in default to fall back to.");
```

`BasicAttackCompiled`'s construction calls `ActionTimingDerivation.DeriveBasicAttack(…,
ActionTimingPolicy.Tuning)` (`BattleRunState.cs:74`). **The only `Configure` caller in `src/` is
`FusionRpg.Server/Program.cs:223`** — nothing in the injector. So the first injector touch of this row
**throws on the hit path**.

`WebMatchService.cs:335-337` already classes this as a process-global precondition.

**Required: the injector host calls `ActionTimingPolicy.Configure` at `RpgHost.Initialize`**, from
`data/tuning/action-timing.v1.json` — already copied to the plugin folder, so it is cheap. It must be
configured **before** any lawn actor is granted, and ordered against host startup rather than raced
(`BattleRunState.cs:58` documents exactly that race).

## Cost is a different problem, and this module does not solve it

The first draft claimed it would deliver "the compiled row **and** its cost row". It cannot:

- `BattleRunState.cs:81` — `Costs: Array.Empty<CompiledActionCost>()`. The hand-built row has none, by
  construction. `authored-basics.json`'s own `_meta` says this is what "leaves the action uncostable".
- Authored costs reach the store through the Server importer and are read back via SQLite
  (`RpgStore.Actions.cs:453` `ListCosts`) — and the injector's csproj copies `data/tuning/**` but
  **not** `data/seed/actions/**`, so the authored cost is not even on disk beside the plugin.
- `CostLedger` cannot simply be "called": its constructor (`Cost/CostLedger.cs:51-58`) needs
  `costsByActionId`, `poolsFor`, `derivedFor`, `rungOf` and `nowTick`. `BattleRunState.cs:558-571`
  assembles all five per battle. **The lawn has none of that assembly**, and lawn actor pools are
  `basic-attack-cost`'s wire 2 / `resource-subtick`'s territory.

**So the cost path is an owner decision, not a wiring detail.** Three shapes, to be chosen in
`basic-attack-cost` rather than assumed here:

| Option | Consequence |
|---|---|
| Ship `authored-basics.json` beside the tuning files and read the cost locally | Smallest; makes the injector read one more data file it already has a copy path for |
| Deliver the cost through an existing cold-edge cache (like the aptitude caches) | Data only — a cost row *is* serialisable, unlike a compiled predicate. Needs the §2.16 trigger set the first draft wrongly applied to the row |
| Accept an **uncosted** lawn basic attack for the first increment | Cuts `basic-attack-cost`'s dependency entirely; contradicts D2 until a later slice restores it |

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Actions/` | New public basic-attack factory; `BattleRunState` consumes it instead of its own field |
| `src/FusionRpg.Injector/…/RpgHost` initialize | `ActionTimingPolicy.Configure` from the shipped tuning |

## Code style

**One construction, two callers.** Extracting the factory must leave `BattleRunState` calling it —
if the injector gets its own copy, the two drift and that is the dual-compose defect this repo has
already overturned once.

Constructing locally is not a SOLID violation: it is the *same* factory, not a parallel path. The rule
being honoured is one SSOT for the row's shape, not "everything must come from the Server".

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | The factory produces a row identical to today's `BasicAttackCompiled` — a golden on its fields, so the extraction is provably behaviour-preserving |
| Core unit | `BattleRunState` uses the factory; no second construction site exists (source scan) |
| Injector | With `ActionTimingPolicy` configured, constructing the row succeeds |
| Injector | **Without** it configured, the failure is a **loud one-shot diagnostic**, not a silent skip and not a per-hit throw |
| Negative | No HTTP/SignalR/SQLite call on the construction path — assert by source scan |

## Boundaries

- **Always:** one factory, shared with `BattleRunState`.
- **Always:** configure `ActionTimingPolicy` before the first grant, ordered against host startup.
- **Ask first:** the cost delivery shape (above) — it is an owner decision.
- **Never:** a second construction of the basic-attack row; compiling real-content actions
  injector-side; awaiting the Server on the hit path.

## Success criteria

- [ ] A single public factory builds the basic-attack row; `BattleRunState` and the injector both use
      it; a source scan proves there is no second construction site.
- [ ] A field-level golden proves the extracted row is identical to today's.
- [ ] `ActionTimingPolicy.Configure` runs in the injector host before any lawn grant is bound.
- [ ] An unconfigured/failed construction produces a **loud, one-shot diagnostic** — **not** the first
      draft's "no contribution and no exception". *(That posture was the defect this whole program
      exists to fix: silently contributing nothing is indistinguishable from working.)*
- [ ] No HTTP, SignalR or SQLite on the construction path.
- [ ] **Guards: `guard-single-writer.ps1` and `guard-funnel-delta.ps1`** — the ones that can actually
      fire on new lawn combat code. *(The first draft named `guard-secondary-no-unity.ps1` and
      `guard-dal.ps1`; neither can fire here — the first scans only `Core/Effects/Plugins`, the second
      scans for SQL — so both were vacuous green.)*
