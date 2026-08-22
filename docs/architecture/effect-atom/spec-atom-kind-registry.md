# Spec: atom-kind-registry (E1)

Module **E1** in the [atom effect map](../effect-atom-map.md). No dependencies; nothing in the game changes. Vocabulary source: [atom-catalog-ssot.md](atom-catalog-ssot.md).

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Declare the **closed vocabulary** in code: 5 attach points, 12 kinds, their param schemas, the runtime support matrix, and the **power categories** each kind touches. *(`CostHook` was removed — see below; E9 owns pricing.)* This module owns *what an atom may say* — never what any atom says. No magnitudes, no content ids, no I/O.

It also owns the rule that keeps the vocabulary closed, because that rule is the reason the module exists.

## Design (locked on approval)

### The code-or-data rule — this module's constitution

> A thing may be **data** if adding a row changes behaviour **without new code**. If a new row needs a new consumer, it must be **code**.

The repo supplies its own counter-example: `status.expose.*` is a legal, registered, fully-valid derived channel with **zero readers** — adding it changed nothing. The eight declared-only statuses are the same failure. **A row that no code consumes is not content.**

Consequences, locked:

- The **12 derived channel families stay code** (each has a named reader: `CombatDerivedReader.Power` → resolver, `ShieldCapacity` → shield runtime, `CritRate` → sigmoid). A thirteenth family proposed as a row is rejected at review, not at runtime.
- The **element roster is data** (E18) — channels are generated and readers match by *pattern*, so a new element needs no consumer.
- **Kinds, triggers, and predicate leaves are code.** Each needs an executor.

### Attach points — 5

`Stat` · `Resource` · `Status` · `Shield` · `Board`. This list is guarded by ADR. It is the seam list, and it is meant to stay short.

### Kinds — 12

`stat.modify` · `stat.derived` · `resource.delta` · `resource.economy` · `status.apply` · `status.clear` · `shield.grant` · `spawn.entity` · `board.action` · `grid.spawn` · `grid.clear` · `box.set`

Eleven map to a shipped opcode. `stat.derived` is the one addition and it earns its place: patron auras, star merges, expedition injuries, and contract ranks already write derived channels with **no opcode at all**.

**`shield.grant` is the eleventh opcode, and it is irregular** — `GrantShield` ships in `EffectDtos.cs:34`, is absent from the FA1–FA10 doc table, and is **not in `InjectorEffectActionSink`** (its dispatch default arm throws). It executes bag-side in Core. The registry records that irregularity rather than papering over it; normalising the execution path is not this module's job.

### `AtomKind`

```csharp
sealed record AtomKind(
    string KindId,               // "stat.modify"
    AttachPoint Attach,
    ParamSchema Params,          // typed, closed key set
    RuntimeSupportMatrix Support,     // four states per runtime - see below
    IReadOnlyList<string> Triggers);  // which of the 7 this kind may carry
```

`ParamSchema` declares each key's type, whether it is required, and — critically — **whether the executor actually honours it** (see G1). A key the executor drops is not a valid key.

**`CostHook` was removed.** Its parameter and return types (`conditionality`, category contributions) belong to E9, and a singular `magnitude` cannot carry a kind holding several value specs — `spawn.entity` uses `count` as a multiplier and `hp`/`atk` as the spawned body. E9 owns a `kind → cost` side table instead.

**E1 owns the 7-trigger vocabulary** (`OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`, `OnGranted`, `OnRemoved`, `OnTimer`) with a count guard — E4 validates against it at load and nothing else owned it.

### Rejection over silence — the eight gaps this module closes

The layer below fails silently in eight documented places. Each becomes a **load-time or bind-time rejection**. This is the module's acceptance surface.

| # | Today | Here |
|---|---|---|
| G1 | Overlay accepts `atk` on spawn; the sink **drops it**. Plant spawn drops `hp`/`maxHp`/`atk`/`x`/`mindControlled`; bullet drops five more | honouring is conditional on a **discriminator param** (`kind=zombie,bullet`), never on an ambient "side" — an atom has no side, and its owner's side is a bind-time fact. An unhonoured key is a validation error |
| G2 | `box.set` accepts `cells[]`; executor handles one cell | `cells` rejected until implemented |
| G3 | FA5–FA9 always return `true` | kinds declare whether their executor reports failure; sequence-stop is only claimed where true |
| G4 | `capPerMatch` in the allowlist, **no implementation anywhere** | declared as runner-owned (E15); rejected if E15 is absent |
| G5 | `status.apply` with empty target hits **every zombie on the board** | empty target is a rejection; "all" must be explicit |
| G6 | Unknown **primary** channel silently inert — **verified 2026-08-22**: `ModifierBag.Upsert` checks only that `Channel` is non-empty, and `StatComposer` matches exactly, so a typo composes into nothing forever (derived throws) | both reject |
| G7 | `ExecModifyStat` defaults a missing channel to `atk` | missing channel is a rejection |
| G8 | Primary `defense` reaches the lawn via a **side-wide** cached prefix value | `stat.modify` on `defense` is **match-scope only**; entity binding rejects. Per-actor mitigation is `stat.derived` on `combat.defense.*`. Per-entity primary defense waits for perf **O5** — resolving per-target in the `TakeDamage` prefix is exactly the uncached-per-hit-resolve pattern the 2026-08 perf audit blamed for combat lag |

### Runtime support — four states, not three

A three-flag bitfield cannot hold the matrix. Per runtime: `Full` · `Partial` (executes only through a named side path) · `PlanOnly` (produces a plan, applies nothing) · `None` (**reject**). Collapsing `PlanOnly` into `Full` makes sim silently accept bindings it cannot execute — the very no-op this module exists to prevent. Definitions §9.

### Runtime support is a living audited table, not an assertion

Battle consumes **one** opcode today (`BattleEffectSink`: *"battle mode consumes FA10 only; other actions are inert here"*) and never calls `OnEvent`. So the matrix ships lopsided and honest, and families are marked `battle: pending`, never `battle: never` — [action-map.md](../action-map.md) is building those consumers.

A container bound in a runtime that cannot execute one of its atoms is a **bind-time rejection**. Loud beats a silent no-op.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Kind"
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/AtomKind.cs            (new — record, AttachPoint, RuntimeSupportMatrix, PowerCategory)
src/FusionRpg.Core/Effects/Atoms/ParamSchema.cs         (new — typed closed key sets, honoured flags)
src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs    (new — the 12, lookup, Validate)
src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs       (new — reason codes; never a bool)
tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Every kind validates its own canonical sample params | pass |
| Unknown `kindId` | rejection with reason `UnknownKind`, never a skip |
| Unknown param key for a kind | rejection `UnknownParam` |
| `spawn.entity` plant with `atk` | rejection `ParamNotImplemented` (G1) — the sink drops `atk` for **every** spawn kind, so it is unimplemented, not conditionally unhonoured |
| `box.set` with `cells[]` | rejection `ParamNotImplemented` (G2) |
| `stat.modify` with no channel | rejection `MissingParam` (G7) — never a default to `atk` |
| `status.apply` with no `status` | rejection `MissingParam` — FA2 really does read it |
| `status.apply` declaring `target` | **rejection `UnknownParam`.** FA2 has no `target` param: the target comes from `ResolveStatusTargetPtr(ctx)`, i.e. from the **event**. **G5 cannot be closed here** — declaring a required `target` would validate a key the executor never reads and leave the board-wide `FindObjectsOfType<Zombie>()` loop exactly as open. It belongs to whoever guards that loop |
| Trigger count | exactly 7 — guard test. **"No trigger" is not a name**: it is an empty allowed-trigger list on the kind and an omitted `when_json.trigger` key, so it never counts toward the 7 |
| `stat.modify` / `stat.derived` with **any** trigger | rejection `TriggerNotAllowed` — they are **permanent modifiers**; apply/revert is a runtime lifecycle mechanic, not content ([definitions.md](definitions.md) §14.2). Letting an author write `OnGranted` alone was how a permanent buff could leak |
| `RuntimeSupportMatrix` | round-trips all four states; `PlanOnly` never reads as `Full` |
| Attach-point count | exactly 5 — a guard test, so growth requires editing the test and noticing |
| Kind count | exactly 12, same reason |
| Reason-code count | exactly **33** — the closed list in [definitions.md](definitions.md) §10, guarded so a silent addition fails |
| Unknown **channel value** on `stat.modify` | rejection `BadParamValue` (G6) — the registry must check the value against `PrimaryChannels`, which it declares today and never reads |

*(Owner-scope and bind-time checks — G8's `ScopeUnsupported` and `RuntimeUnsupported` — are **E6's** tests. E1 owns the reason codes and the matrix; E6 owns the checks.)*

## Boundaries

**Always:** return a typed rejection reason; keep the registry pure (no I/O, no clock, no RNG); treat the runtime matrix as audited fact and re-verify it against code before changing a cell.

**Ask first:** adding a kind, an attach point, or a param key; changing a runtime cell from ✖ to ✅.

**Never:** put a magnitude or a content id in this module (the closed **channel name list** is vocabulary and belongs here — a channel *magnitude* does not); add a kind without an executor; let an unknown anything pass silently.
