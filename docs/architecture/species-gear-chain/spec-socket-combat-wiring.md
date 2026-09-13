# Spec: A socketed insert reaches combat (`socket-combat-wiring`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `socket-combat-wiring`
**Owning program:** `item` module 16 (`sockets`)
**Depends on:** [`gem-tier`](spec-gem-tier.md) — an insert whose tier is hardcoded to 1 resolves to the
wrong atom rung (`atom_id = {family}[.{variant}].t{tier}`), so wiring it first would ship a
contribution that is *present but wrong*, which is worse than absent. **Interacts with**
[`socket-allowance-by-kind`](spec-socket-allowance-by-kind.md) — boundary stated in §Design 6; the two
do not overlap and neither reads the other's tuning.
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md) `:570` (with its own `:322`
correction) — ⛔ **`:570`'s reader count is wrong; `:322`'s is right. Re-measured below.**

---

## Objective

**Make a socketed insert actually change a number an actor fights with — through `ActorHub`, by
contributing to the binding projection that already feeds it, and by adding no second composer.**

⭐ **Established by reading code, not inferred: a socketed insert contributes NOTHING to actor stats
today, by any path.** This is the module that closes that, and the answer to *"which path does it use"*
is **the one equipment already uses** — `ResolveBindings` → `EquipAtomSource.DerivedAtomsFor` →
`AtomDerivedSubsystem` (`IActorStatSubsystem`) → `DerivedComposer` → `ActorHub`.

**The proof of absence, counted this session:**

| Question | Answer | Evidence |
|---|---|---|
| Does anything in `src/FusionRpg.Core/Battle/` read a socket? | **No** | grep for `Socket` across `Battle/` returns nothing |
| Does anything in `src/FusionRpg.Injector/` read a socket? | **No** — the only hits are `SocketsHttpHandler` | `src/FusionRpg.Injector/RpgClient.cs:53`, `:76`, `:81` |
| Does the equip / binding path read `item_socket`? | **No** | `InsertContainerId` / `insert_container_id` appear in exactly 8 files, all socket-ops, card, surface or workbench |
| Is `item_socket.insert_instance_id` ever populated? | **No — always `""`** | `ItemWorkbench.cs:360` passes `""` as `insertInstanceId` into `SocketOperations.TryInsert` (`SocketOperations.cs:53-54`, written at `:100-104`) |
| Is a combination ever *granted*? | **No** — `CombinationEvaluator.Evaluate` has exactly **one** production caller, and it is a display surface | `src/FusionRpg.Core/Items/Surfaces/CombinationDistance.cs:115` |

The code's own comments already say this, in three places — but ⚠ **one of them says the opposite and
is wrong**, so they are listed here as claims, not evidence:

- ✅ `ItemWorkbench.cs:333-336`: *"the instantiated atom still needs a real `effect_binding` row to
  reach combat — the same shape `equip-runtime`'s own multi-round wiring effort needed for equipped
  items."*
- ✅ `RpgStore.Sockets.cs:18-20`: *"Nothing here writes the host's atom rows. Socketing composes at the
  binding layer."*
- ⛔ `RpgStore.Sockets.cs:32-33`, inside the `CREATE TABLE` string: *"Consumer: **the equip path (reads
  it to build bindings)** and the socket UI."* **This is false.** No equip-path site reads
  `item_socket`. Recorded so a later session does not cite it (DESIGN-GATE §3 rule 2 — a comment is not
  evidence), and **correcting it is part of this module's diff.**

---

## What exists today — verified against code

### Built

| Fact | Evidence |
|---|---|
| **`item_socket` is the SSOT for socket state**, with the insert's container id and a column for the insert's own instance | `src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs:34-45` (`insert_container_id`, `insert_instance_id`); D2 §6 refuses the "materialized view" reading at `:11-16` |
| **`ContainerKind.Gem` exists** and `GemContainerBuild.TryBuildOne` mints a real, `Instantiator`-ready `ContainerRow` from a gem seed — one fixed atom, `PrefixRolls: 0`, `SuffixRolls: 0`, empty pool | `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:34`; `src/FusionRpg.Core/Items/Gems/GemContainerBuild.cs:43-74` |
| **The whole equip→Hub chain is shipped and is the single seam this module joins** | `RpgStore.MaterializeRolledEquipRuntime` (`RpgStore.Items.cs:838`) → `ApplyEquipProjection` (`:789`) writes `BindingRow`s at `OwnerKind.UniqueActor` → `EquippedBoundAtoms.InputsFromStore` (`src/FusionRpg.Server/EquippedBoundAtoms.cs:17-42`) calls `store.ResolveBindings` → `EquipAtomSource.DerivedAtomsFor` (`src/FusionRpg.Core/Battle/EquipAtomSource.cs:104-113`) → `AtomDerivedSubsystem` |
| **`AtomDerivedSubsystem` IS an `IActorStatSubsystem`** and is the registered Hub contribution point for bound `stat.derived` atoms | `src/FusionRpg.Core/Stats/Derived/Subsystems/AtomDerivedSubsystem.cs:33` |
| **GG-49 is already enforced on that path** — an empty SourceId is skipped, never minted | `AtomDerivedSubsystem.cs:59-62`; the rule at [actor-hub-ssot.md](../actor-hub-ssot.md) §8.1 |
| **`ContributionSourceIds` is the minting grammar**, with `Equip(role, itemRefId)` → `equip:{role}:{item}` and a matching `FictionLabel` arm | `src/FusionRpg.Core/Stats/Derived/ContributionSourceIds.cs:16-21`, `:40-50` |
| **`ProduceAndBind` / `SaveInstanceAndBind` / `Bind` all exist** as the instance→binding writers | `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:331`, `:255`, `:377` |
| **`store.GetSockets` has 7 production call sites** — `RpgStore.ItemCard.cs:351`, `ItemSurfaceEndpoints.cs:105`, `:181`, `ItemWorkbench.cs:309`, `:359`, `:389`, `:612` | counted this session |

### Wiring gap

Every item here is an inert path with a named call site. **None is an architectural wall.**

| Gap | Evidence |
|---|---|
| ⛔ **`SocketInsert` never mints an instance.** It looks the insert up in a flat display catalog and writes the container id only; `insertInstanceId` is `""` | `src/FusionRpg.Server/ItemWorkbench.cs:356-360` |
| ⛔ **`GemContainerBuild` has no production caller.** It can build the container; nothing calls it on the socket path — the method's own neighbour says so: *"THIS method does not call it yet"* | `ItemWorkbench.cs:330-332` |
| ⛔ **No `effect_binding` row is ever written for an insert**, so `ResolveBindings` returns nothing for it and the Hub never sees it | `EquippedBoundAtoms.cs:23-26` reads only what `ApplyEquipProjection` wrote |
| ⛔ **`ApplyEquipProjection` builds `desired` from rolled *assignments* only** — sockets are not in scope of the projection at all | `RpgStore.Items.cs:793-795` |
| ⛔ **A combination is evaluated for display and never granted.** `CombinationEvaluator.Evaluate`'s single production caller is `CombinationDistance`, a surface | `CombinationDistance.cs:115` |
| ⛔ **`GG-49` has no `insert` arm.** Minting an insert's contribution as `equip:{role}:{hostItem}` would make it indistinguishable from the host's own affixes on the sheet | `ContributionSourceIds.cs:16-21`; §8.1's table |

### Real gap

Exactly two, and both are small and named:

1. ⛔ **A GG-49 SourceId kind for an insert does not exist.** §8.1's producer table is a **locked
   grammar** (*"locked 2026-09-07"*), so adding a row is a **reviewed amendment against `actor-hub`**,
   filed the way [`species-gear-chain-map.md`](../species-gear-chain-map.md) §Cross-program asks
   already files its others. **This is ask-first and the spec does not assume it.**
2. ⛔ **Most gems cannot resolve an atom yet.** `GemContainerBuild`'s own measurement: *"of the 60
   shipped gem entries, 27 resolve as of 2026-09-07 — the rest name a family the affix-family corpus
   does not carry yet, or a family/element combination it never authored"* (`GemContainerBuild.cs:26-30`).
   ⚠ **That `60` is stale — the corpus is 104 today** (counted; the resolve rate was not re-measured
   this session and is **a reading, never a target**). This is a genuine **content** gap in
   `data/seed/items/affix-families/**`, identical in shape to `unique-corpus-atom-family-gap`, and
   `TryBuildOne` already refuses each case by name rather than guessing a substitute. **It does not
   block this module** — an insert that cannot resolve simply contributes nothing, loudly.

---

## Design

### 1. ⭐ The seam is the binding projection, and there is exactly one reason it must be

`ApplyEquipProjection` **reaps** every `UniqueActor`-scoped binding that is not in the projection's own
`desired` set:

```csharp
// RpgStore.Items.cs:797-801
var existing = ListBindings(scope);
foreach (var binding in existing)
    if (!desired.ContainsKey(binding.InstanceId))
        Withdraw(binding.BindingId);
```

⛔ **So a socket binding written by any *parallel* path is withdrawn on the next squad build**, and the
symptom is a gem that works once and then silently stops. **The insert rows must be produced by the
same projection**, which is also exactly what SOLID-O asks: extend the existing gate, do not fork a
second one.

**The contract, stated as the pipeline:**

```
ListAssignments(specimen)            ← rolled equipped items (unchanged)
  + GetSockets(each assignment)      ← NEW: the filled sockets on each equipped item
  → EquipProjector.Project           ← NEW: emits an insert binding per filled socket
  → ApplyEquipProjection             ← one reconcile, one reaper, unchanged shape
  → ResolveBindings(UniqueActor)     ← unchanged
  → EquippedBoundAtoms.InputsFromStore
  → EquipAtomSource.DerivedAtomsFor  ← honours `op` via AtomDerivedSubsystem.TryParseOp
  → AtomDerivedSubsystem (IActorStatSubsystem)
  → DerivedComposer → ActorHub.ResolveDerived → AppliedCombat
```

**Nothing in that chain is new below `EquipProjector`.** The module adds rows to a set; it adds no
compose, no fold, no reader.

### 2. The insert needs a real instance, and the column for it already exists

`item_socket.insert_instance_id` has been in the schema since the table shipped
(`RpgStore.Sockets.cs:42`) and is documented as *"the insert's OWN instance, bound beside the host."*
It has never been written. `SocketInsert` gains three steps, in the transaction it already opens:

1. `GemContainerBuild.TryBuildOne(seed, lookups, out reason)` — refuse by name if the atom does not
   resolve, **exactly as it already does**.
2. `TryInstantiate` on that container → a real `InstanceRow`.
3. Persist the instance id into `insert_instance_id` through the existing wholesale `SetSocketsUnlocked`
   write, which already commits with the material debit (`ItemWorkbench.cs:416-417`).

⚠ **A refusal here refuses the socket-insert**, with the reason `GemContainerBuild` already produces.
It does **not** insert a gem that quietly contributes nothing — that is the wiring gap this module
exists to close, and reproducing it under a new name would be the same defect.

### 3. ⛔ One ActorHub. No second composer, and no `BattleChannelMod` producer.

The contribution is a `BoundDerivedAtom` on an already-registered subsystem. Concretely, this module:

- adds **no** `*Composer*` type (`guard-actor-hub.ps1:74-87` would fail it),
- adds **no** `BattleStatComposer.Compose` call site (`:95-108`),
- constructs **no** `new BattleChannelMod(` outside the debt allowlist (`:122-136`),
- adds **no** `Stats.Resolve` in the injector (`:163-175`).

**Battle gets the contribution for free and by construction.** `EquippedBoundAtoms.InputsFromStore` is
the *shared* input to both seams — battle via `EquipAtomSource.ModsFor`, Hub via `DerivedAtomsFor` —
and the class's own comment says why that sharing is the point: *"The parse is shared (one
`EquippedDerived` walk), so the two sides cannot drift on which atoms count… **Do not invent a third
projection**"* (`EquipAtomSource.cs:93-96`). This module adds inputs to that one walk and writes no
third projection.

⚠ **One known divergence is inherited, not created.** `BattleStatComposer` folds every equip mod
additively, so an atom declaring `op: "increased"` applies as if `flat` on the battle side, while the
Hub side honours the op (`EquipAtomSource.cs:69-77`). **That is `FUSE-battle-hub`'s debt to fix, not
this module's** — and this module must not widen it by routing inserts around either side.

### 4. GG-49 — an insert is not an equip, and the sheet must be able to say so

`ContributionSourceIds.Equip(role, itemRef)` keys on the **host item**. An insert bound at the host's
slot would mint `equip:{role}:{hostItem}` and be **indistinguishable from the host's own affixes** on
the sheet, which defeats the contribution list's whole purpose.

**Asked, not assumed — a §8.1 amendment against `actor-hub`:**

| Producer | SourceId | Fiction label |
|---|---|---|
| **Insert (socketed gem)** | `insert:{role}:{hostItemRef}#{socketIndex}` | `Insert · armament-primary (item… socket 0)` |
| **Combination (arm 2)** | `combo:{role}:{hostItemRef}:{comboId}` | `Combo · armament-primary (Kinetic Deflection)` |

The socket index is in the id because two identical gems in two sockets are two contributions, and a
list that collapses them is lying about where the number came from.

⛔ **If the amendment is refused, the module does not ship an approximation.** Minting
`equip:{role}:{host}` for an insert would be a *wrong-but-plausible* attribution, which is the same
class of defect as `UnauthoredInsertTier` — invisible, and it trains the next reader to trust it.

### 5. Arm 2 — combinations, and why they are in scope rather than deferred

A combination is already a container: D27 fixes `combo_id` **as** the container id, prefixed `combo.`
(`RpgStore.Sockets.cs:47-49`). So a fired combination binds through the **identical** mechanism as an
insert — one more row in the same projection, at the same scope, with its own SourceId. It needs no new
machinery, and the alternative (evaluate at display, grant nowhere) is precisely the shape this module
exists to remove.

⚠ **`CombinationEvaluator.Evaluate` must be called once, at projection time, and its result must not be
re-derived anywhere else.** `CombinationDistance.cs:71-83` already records that it calls `Evaluate`
*"exactly once"* and deliberately does not reimplement the multiset match — that discipline extends
here.

### 6. ⭐ The boundary against `socket-allowance-by-kind`, stated so neither module drifts into the other

| | `socket-allowance-by-kind` | `socket-combat-wiring` (this module) |
|---|---|---|
| Answers | **How many sockets does this item have?** | **Does a filled socket change a combat number?** |
| Owns | `socketAllowanceByKind` in `sockets.v1.json`, and the composition order into the `rarityGrant` window | The binding projection rows, the insert instance, the SourceId grammar ask |
| Runs at | **drop / craft** — the count is rolled once and clamped | **deploy / squad build** — a full rebuild every `MaterializeRolledEquipRuntime` call |
| Reads the other's tuning | **No** | **No** |
| Its own ActorHub gate says | *"Socket count is a capacity, not a combat number… **No private fold is introduced**"* | Contributes through `AtomDerivedSubsystem`; no private fold either |

**They compose without coordination.** Allowance decides that a set piece has two sockets instead of
three; this module decides that whatever is in those two sockets reaches the Hub. Allowance's spec
already anticipates this exact split — *"What a socketed insert contributes composes through the
existing atom path into `ActorHub`, unchanged by this module"* — and that sentence is the contract
this module implements. **Neither module may absorb the other's number.**

### 7. Removal, unequip and pointer reuse — the three ways this can leak

- **Socket removal** must withdraw the insert's binding in the same transaction as the socket write.
  `SocketOperations.TryRemove` already clears `InsertContainerId`/`InsertInstanceId`
  (`SocketOperations.cs:125`); the withdraw rides with it.
- **Unequip** is already handled by the reaper (§Design 1) — because the insert rows come from the
  same projection, unequipping the host removes them in the same reconcile. **That is the second
  reason the seam must be the projection.**
- **Match teardown** is unchanged: these are `UniqueActor`-scoped bindings, not `entity:{ptr}` grants,
  so the IL2CPP pointer-reuse rule ([match-runtime.md](../match-runtime.md)) is not engaged. Stated
  because it is the question a reviewer will ask.

---

## Tech stack

C# .NET 8 — `FusionRpg.Core/Items` (`EquipProjector`), `FusionRpg.Core/Items/Gems`,
`FusionRpg.Core/Items/Sockets`, `FusionRpg.Data` (projection + socket write), `FusionRpg.Server`
(`ItemWorkbench`), xUnit. No new dependency. No FE surface — the sheet's *display* of an insert
contribution belongs to `item-surfaces` module 20 / `gui-lego`. **SQL only inside `FusionRpg.Data`.**

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Socket"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Gem"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorHub"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemSocket"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Binding"
dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~Workbench"
dotnet test tests/FusionRpg.E2E.Tests
.\scripts\guard-actor-hub.ps1
.\scripts\guard-dal.ps1
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-test-substrate.ps1
python scripts/audit-overflow.py
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Items/EquipProjector.cs` | Emits an insert (and arm-2 combination) binding per filled socket — **the one place the set grows** |
| `src/FusionRpg.Data/Sqlite/RpgStore.Items.cs` | `MaterializeRolledEquipRuntime` (`:838`) feeds sockets into the projection; `ApplyEquipProjection` (`:789`) reconciles them with the same reaper |
| `src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs` | ⛔ **the false `CREATE TABLE` comment at `:32-33` is corrected in this diff** |
| `src/FusionRpg.Server/ItemWorkbench.cs` | `SocketInsert` (`:338`) mints the insert instance via `GemContainerBuild` + `TryInstantiate`, writes `insert_instance_id`; socket-remove withdraws |
| `src/FusionRpg.Core/Items/Gems/GemContainerBuild.cs` | **Gains its first production caller.** Unchanged otherwise |
| `src/FusionRpg.Core/Stats/Derived/ContributionSourceIds.cs` | ⚠ **Two new arms + two `FictionLabel` arms — gated on the §8.1 amendment (ask-first)** |
| `docs/architecture/actor-hub-ssot.md` §8.1 | The reviewed amendment adding the `insert:` and `combo:` producer rows |
| `src/FusionRpg.Server/EquippedBoundAtoms.cs` | **Unchanged.** It already fans in whatever `ResolveBindings` returns |
| `src/FusionRpg.Core/Battle/EquipAtomSource.cs` | **Unchanged.** One shared walk; no third projection |
| `data/tuning/sockets.v1.json` | ⚠ **Untouched unless §Tunables row 1 is adopted** — it carries `"version": 2`, so a revision is a **bump inside the file**, never a new `v{n}.json` |
| `data/seed/items/affix-families/**` | ⛔ **generated output** (`_meta.model`). The §Real-gap-2 resolve rate improves by regenerating the family corpus, never by hand-editing a gem or a family row |

## Code style

Emit the insert rows from the **same** projection, and say why the seam is not negotiable:

```csharp
// The insert's binding MUST come out of this projection, not a parallel Bind() call beside it.
// ApplyEquipProjection (RpgStore.Items.cs:797-801) withdraws every UniqueActor-scoped binding whose
// InstanceId is absent from `desired` -- so a socket binding written anywhere else is reaped on the
// next squad build, and the symptom is a gem that works once and then silently stops contributing.
// Extending the existing gate is also what SOLID-O asks: contribute to the SSOT, never fork it.
//
// GG-49: an insert is NOT the host item. ContributionSourceIds.Equip keys on the host, so reusing it
// would make the gem's number indistinguishable from the host's own affixes in the contribution list
// -- a wrong-but-plausible attribution, the same class of defect as a silently under-reported tier.
// The socket index is in the id because two identical gems in two sockets are TWO contributions.
foreach (var slot in sockets.Where(s => !s.IsEmpty))
{
    if (slot.InsertInstanceId is not { Length: > 0 } insertInstance)
        continue;   // pre-gem-tier rows carry no instance; skipping is honest, inventing one is not
    yield return new ProjectedBinding(
        RefId:   insertInstance,
        RefKind: EquipRefKinds.Rolled,
        Role:    hostRole,
        SourceId: ContributionSourceIds.Insert(hostRole, hostItemRef, slot.Index));
}
```

---

## Tunables

| Number | Meaning | Owning `data/tuning` file |
|---|---|---|
| *(optional, §Open question 3)* `insertContributionMultiplierMilli` | A per-mille scalar on what an insert contributes relative to the same atom on an affix — the dial a balance pass needs if gems land too strong or too weak | `data/tuning/sockets.v1.json` — ⚠ it already carries `"version": 2`, so this is a **`version` bump inside the existing file (2 → 3)**, **not** a new `sockets.v2.json` |
| `insertTiers.count`, `insertTiers.upcycleInputPerOutput` | Owned by [`gem-tier`](spec-gem-tier.md). **Read by neither this module nor its tests** | `data/tuning/sockets.v1.json` |
| `resonance.attunedTierBonus` (**1**), `resonance.pureThresholds`, `diversityThresholds` | Already shipped and already read by `CombinationEvaluator`. **Confirm, do not re-author** | `data/tuning/sockets.v1.json` |

⛔ **This module's default is to add NO tunable.** An insert contributes exactly what its atom says,
through the same path an affix's atom already takes — and a needless dial is *"a literal with extra
ceremony"* ([tunables-ssot.md](../tunables-ssot.md) §6). The row above exists only if Open question 3
is answered yes.

⛔ **No hard progression ceiling.** Nothing here caps what an insert may contribute. A per-mille
multiplier, if adopted, is a **bounded ratio** (PS-8 exempt by nature) and must say so in the file's
own note.

**Structural (stays `const`, with a comment saying why):**
- The **one-projection rule** — that insert bindings come from `EquipProjector` and nowhere else. It is
  a contract, not a number, and a tunable that could relax it would be a second compose gate.
- `ContributionSourceIds`' grammar strings (`insert:`, `combo:`) — a **closed vocabulary** parsed by
  `FictionLabel`; changing one silently breaks every sheet label. Not a balance surface.
- The gem container's `PrefixRolls: 0` / `SuffixRolls: 0` (`GemContainerBuild.cs:70-71`) — structural:
  an insert is a *fixed* container by `ContainerRow`'s X7 contract, and a rolling insert is a
  different feature.

## Numeric types

- **An insert's contribution is a magnitude, so it is `long` wherever this module carries one.** Widen
  before multiplying (`(long)a * b`, never `(long)(a * b)`); divide by 1000 **last, exactly once**;
  **never `float`** — integer-exactness fails at `Θ` = 232, inside normal play, and `float` is
  non-deterministic across runtimes, which is disqualifying on a hashed or persisted path; **overflow
  throws, never wraps.**
- ⭐ **The existing seam already got this right, and the reason is on the record.**
  `EquipAtomSource.EquippedDerived` reads `amount` as `long`, not `int`, because *"The first cut of
  this class used `TryGetInt32`, which does not throw on a larger magnitude — it returns false, so the
  atom was silently DROPPED"* (`EquipAtomSource.cs:118-124`). **Inserts ride that same reader, so they
  inherit the fix.** Do not add a second parse.
- ⚠ **`BoundDerivedAtom.Amount` is `double`** (`AtomDerivedSubsystem.cs:91-92`), and that is
  **correct, not a defect**: `ssot-power-scale.md` §10.7 decided *"`double` stands in stat
  composition… the `long` rule applies to the values composition **produces**, not to the arithmetic
  that composes ratios."* What this module owes is §10.7's item 2 — **materialize as `long` at the
  boundary where the value leaves composition** (`EntityStatWriter`, a `DamagePacket`, `BattleRuleset`),
  which the existing path already does. **No new `double` magnitude is introduced outside that path**;
  one would be an A1 finding, not an A7.
- The socket index is a small identity `int`, an ordinal, never a multiplier.

⛔ **No new `ssot-power-scale.md` §10 row is owed.** Nothing here derives a number from a level: the
insert's magnitude is the atom's, which already rides `contentScale` through the atom ladder. Stated
explicitly rather than left for a reviewer to check — §10 is closed and a not-owed row is worth saying.

## ActorHub gate

⭐ **This is the module's load-bearing section.**

**Contributes — through a registered `IActorStatSubsystem`, and through no other path.**

| Requirement | How this module meets it |
|---|---|
| Contribute via `IActorStatSubsystem` / a registered atom reader | `AtomDerivedSubsystem` (`AtomDerivedSubsystem.cs:33`), fed by `EquipAtomSource.DerivedAtomsFor` — **an already-registered reader whose input set this module widens** |
| Read Hub output only | Nothing in this module reads a composed combat number at all |
| Non-empty GG-49 SourceId | `ContributionSourceIds.Insert(...)` / `.Combo(...)` — **a §8.1 amendment, ask-first, named in §Real gap 1.** `AtomDerivedSubsystem.cs:59-62` already skips an empty id, so a mis-mint is inert rather than unattributed |
| No second composer | **None added.** `guard-actor-hub.ps1:74-87` scans `*Composer*.cs` for `DerivedModifier|ActorDerivedSnapshot|ContributeDerived|AppliedCombat|BattleChannelMod` — this module creates no such file |
| No new `BattleStatComposer.Compose` call site | **None.** `guard-actor-hub.ps1:95-108` |
| No new `BattleChannelMod` producer | **None.** `guard-actor-hub.ps1:122-136`. Battle receives the contribution through `EquipAtomSource.ModsFor`, an **allowlisted existing** producer (`:112`), whose input set grows — no new construction site |
| Does not deepen the dual-compose debt | Inserts enter through the **shared** `EquippedBoundAtoms` walk that both seams already read (`EquipAtomSource.cs:91-96`). **Both sides gain the contribution from one parse**, so fusion has one fewer divergence to reconcile, not one more |

⛔ **`BattleStatComposer` is grandfathered debt, never a template.** This module cites the dual-compose
exception as permission for nothing. ([actor-hub-ssot.md](../actor-hub-ssot.md) §8.3; DESIGN-GATE §2.15.)

⛔ **The lawn is not an exception.** `EntityApply` and `GameHooks` already call `ActorHub.Resolve`
(`guard-actor-hub.ps1:139-155`), so an insert contribution reaches the lawn through the Writer's
`AppliedCombat` with **no injector change** — which is the RPG-layer rule working as designed, not a
gap. The question *"does the lawn support gems"* is the wrong one; the right one is whether the Hub
path is wired, and after this module it is.

## Testing strategy

**Store tests run in memory**; disk only when the disk is the thing under test; a failed temp-delete is
a **failure**, never `catch { }` ([testing-standard.md](../../contributing/testing-standard.md)).

| Level | What it asserts |
|---|---|
| Unit | ⭐ **A filled socket produces exactly one projected binding per filled socket**, at the host's role, with a non-empty GG-49 SourceId |
| Unit | An **empty** socket, and a socket whose insert has no instance, produce **zero** bindings — skipping is honest; no placeholder instance is invented |
| Unit | Two identical gems in two sockets produce **two distinct** SourceIds differing by socket index |
| Unit | An insert whose atom does not resolve **refuses the socket-insert by name**, and no partial state is written |
| Unit | ⛔ **The op is honoured** on the Hub side (`AtomDerivedSubsystem.TryParseOp`) — an `increased` insert is not silently folded as `flat` there |
| Contract | ⭐ **Round trip through the real Hub:** socket a gem → `MaterializeRolledEquipRuntime` → `ActorHub.ResolveDerivedWithContributions` shows the channel changed **and** names the insert in the contribution list |
| Contract | ⭐ **The reaper does not eat it.** Calling `MaterializeRolledEquipRuntime` **twice** leaves the insert binding intact — the regression the parallel-`Bind` design would have shipped |
| Contract | **Unequipping the host withdraws the insert binding** in the same reconcile, and the contribution disappears from Hub output |
| Contract | **Socket removal withdraws the binding** in the same transaction as the socket write |
| Contract | The SourceId grammar round-trips through `FictionLabel` — `insert:`/`combo:` render as labels, never as raw ids |
| Regression | ⭐ **An actor with no sockets, and an actor whose sockets are empty, resolve byte-identically to today** — the contribution is additive and reaches no one who has not socketed |
| Regression | Battle numbers for an un-socketed squad are unchanged — the shared walk gained inputs, not behaviour |
| Guard | `guard-actor-hub.ps1`, `guard-dal.ps1`, `guard-single-writer.ps1`, `guard-funnel-delta.ps1`, `guard-test-substrate.ps1` all green |

⛔ **No test asserts a population count.** Not the **104** shipped gems, not the **76** combinations,
not the **27-of-60** (now stale) container-build resolve rate, not how many gems resolve today, not how
many sockets any shipped item has. Every one is a **reading that moves when content ships**, and
`GemContainerBuild`'s own resolve rate is expected to climb as the affix-family corpus grows — a test
pinning it would go red on success. Assert instead the **relationships**: *bindings produced ==
filled sockets with a resolvable insert*; *contributions == bindings*; *every SourceId non-empty and
grammar-valid*; *withdraw count == removed sockets*; *twice-materialized == once-materialized*. **Print
the scale; never assert it** ([validation-ssot.md](../validation-ssot.md) §4).

⛔ **No test asserts a gem's generated `name`, `nameKey` or flavour text.** Authored output is the
model's to change.

## Boundaries

**Always**
- Emit insert bindings from `EquipProjector`, inside the one projection `ApplyEquipProjection`
  reconciles.
- Mint a real instance through `GemContainerBuild` + `TryInstantiate`, and write `insert_instance_id`.
- Refuse by name when an atom does not resolve — never socket a gem that contributes nothing silently.
- Mint a non-empty GG-49 SourceId that distinguishes the insert from its host item.
- Withdraw on removal and on unequip, in the same transaction / the same reconcile.
- Correct `RpgStore.Sockets.cs:32-33`'s false comment in this diff.
- Run store tests in memory.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ **The `actor-hub-ssot.md` §8.1 SourceId amendment** (`insert:` and `combo:` producer rows). The
  grammar is **locked** and its producer table is a **closed vocabulary** — extending it is a reviewed
  change, filed against `actor-hub` the way the map's other cross-program asks are. **Without it, this
  module does not ship an approximation.**
- Whether arm 2 (combination grants) lands with arm 1 or as a follow-up (Open question 2).
- Any `insertContributionMultiplierMilli` dial (Open question 3).
- Widening `EquipRefKinds` if an insert warrants its own ref kind rather than reusing `Rolled`.

**Never**
- ⛔ Write a second composer, a private `ChannelMods` combat writer, or a new
  `BattleStatComposer.Compose` call site. `guard-actor-hub.ps1` must stay green.
- ⛔ Cite the overturned dual-compose ADR exception as permission for a parallel path (DESIGN-GATE §2.15).
- ⛔ Bind an insert outside `EquipProjector`. It will be reaped, and the bug has no symptom until the
  second deploy.
- ⛔ Mint `equip:{role}:{hostItem}` for an insert. A wrong-but-plausible attribution is worse than none.
- ⛔ Invent a third projection of equipped atoms. `EquipAtomSource.cs:93-96` names that ban explicitly.
- ⛔ Have the injector read a socket, or `Stats.Resolve` anything. The lawn receives this through
  `AppliedCombat`, unchanged.
- ⛔ Read or write `socketAllowanceByKind`. Socket **count** is the other module's, entirely.
- ⛔ Hand-edit `data/seed/items/affix-families/**` or `gems/**` to make a gem resolve. Both carry
  `_meta.model`; the fix is the generator and a regeneration.
- Assert a population count.

## Success criteria

1. ⭐ **A socketed gem measurably changes a combat number**, proven end to end: socket → deploy →
   `ActorHub.ResolveDerivedWithContributions` → the channel moved. **This has never been true.**
2. ⭐ The contribution is **attributed** — the Hub's contribution list names the insert, its host item,
   its role and its socket index, under the amended §8.1 grammar.
3. ⭐ **The contribution survives a second `MaterializeRolledEquipRuntime`** — it comes from the
   projection, so the reaper does not eat it.
4. `item_socket.insert_instance_id` holds a real instance for every filled socket; `""` no longer
   appears on that column in production.
5. Unequipping the host, and removing the insert, each withdraw the binding and the contribution
   disappears — no orphan.
6. An unresolvable insert **refuses the socket-insert by name** and writes no partial state.
7. `guard-actor-hub.ps1` is green, and the diff contains **no** new `*Composer*`, no new
   `BattleStatComposer.Compose` call site, and no new `BattleChannelMod` construction.
8. An actor with no sockets resolves byte-identically to today, on both the Hub and battle paths.
9. `RpgStore.Sockets.cs:32-33`'s false "the equip path reads it to build bindings" comment is true
   after this change — or is deleted.
10. Core, Data, Server and E2E suites green; `guard-dal.ps1`, `guard-single-writer.ps1`,
    `guard-funnel-delta.ps1`, `guard-test-substrate.ps1` green; `audit-overflow.py` clean.

## Open questions

1. **Does an insert bind at the host's role, or does it get a slot id of its own
   (`{role}#socket{n}`)?**
   **Recommendation: bind at the host's role, and carry the socket index in the SourceId only.**
   `EquippedBoundAtoms.InputsFromStore` maps `binding.Slot` → role → `itemRef` through
   `roleToItem` (`EquippedBoundAtoms.cs:19-36`); a slot id the role map does not contain falls through
   to `itemRef = binding.InstanceId`, which silently degrades attribution. The role is also the honest
   answer to *"where on the body is this number coming from"*. The index belongs in the SourceId, where
   it distinguishes two identical gems without touching the slot vocabulary. *(Irreversible? No — the
   slot is recomputed on every projection rebuild.)*

2. **Does arm 2 (combination grants) ship with arm 1, or immediately after?**
   **Recommendation: ship arm 1 alone, arm 2 next.** Arm 1 is the claim that has never been true and
   it is provable on its own; arm 2 adds a second SourceId kind and a first production caller for
   `CombinationEvaluator`, and bundling them makes one acceptance test cover two independent failure
   modes. The §8.1 amendment should still be filed for **both** rows at once, because the grammar is
   one reviewed change and splitting it means asking twice. *(Irreversible? No.)*

3. **Does an insert contribute the same magnitude as the identical atom on an affix, or a scaled one?**
   **Recommendation: the same, with no dial, until play says otherwise.** An insert is already
   constrained by the socket count, by `upcycleInputPerOutput`'s drain, and by the gem having to be
   found — three separate costs an affix does not pay. Adding a fourth, invisible one now is balancing
   against a loop nobody has played. If a dial is later needed it is one per-mille row in
   `sockets.v1.json` at a `version` bump, which is cheap. *(Irreversible? No — a multiplier can be
   added at any time, and its default of `1000‰` is exactly today's behaviour.)*

4. **Should the ~content gap in `affix-families` (§Real gap 2) gate this module?**
   **Recommendation: no — ship the wiring, report the rate.** `GemContainerBuild` already refuses
   unresolvable gems by name and its report shape (`BuildReport(Built, Refused)`,
   `GemContainerBuild.cs:77`) exists precisely to make the gap visible. Gating on content would hold a
   proven wiring fix behind a corpus run, and the resolve rate is a **reading** that improves on its own
   as `affix-families` grows. Emit it as a boot report, never as a test assertion. *(Irreversible? No.)*
