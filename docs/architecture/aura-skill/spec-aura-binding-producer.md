# Spec: aura-binding-producer

Module id `aura-binding-producer` in the [aura-skill map](../aura-skill-map.md). Delivers
`tasks/backlog-clear-todo.md` **BP1–BP4** (Phase 1).

Reads and does not restate: [effect-atom/definitions.md](../effect-atom/definitions.md) **(wins over
this spec where they disagree)** · [spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)
(E6 — the schema and the bind gate) · [overlay-control-loops.md](../overlay-control-loops.md)
(which loop may do what) · [spec-aura-delivery-path.md](spec-aura-delivery-path.md).

**Status:** written 2026-08-31. ⛔ **Owner review required before BP2** — `backlog-clear-todo.md`
makes Phase 1 spec-first.

---

## Design gate checklist ([../../DESIGN-GATE.md](../../DESIGN-GATE.md) §5)

```
[x] I identified the subsystem(s) this touches.
    — the atom/Secondary layer, effects, injector↔game, data/SQL, aura-skill.
[x] I read every doc in the §1 row(s) for those subsystems, this session.
    — definitions.md's binding/scope vocabulary via spec-instance-and-binding.md (E6, in full),
      overlay-control-loops.md (in full), event-pipeline-v2-ssot.md (in full), DESIGN-GATE §1-3/§5.
[x] I checked decisions.md for a lock covering this.
    — the "Overlay control loops" row locks Cold = grant/loadout push, Hot = per-hit. This module is
      Cold by that row's own definition. No row forbids it; none prescribes it either.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments.
[x] I read the surrounding section of every rule I quoted.
[x] I tested (not assumed) any constraint I am reporting.
    — "zero production callers" was grepped (`\.Bind(` across src/, tests/, tools/): 19 test callers,
      and the 5 src/ hits are BepInEx `config.Bind(...)`, unrelated. "Push happens only at Hello" was
      grepped, not inferred: `PushGrantSnapshotAsync` has exactly one caller, `RpgHub.cs:43`, inside
      `Hello`. "Active auras are not persisted" was grepped in FusionRpg.Data: no ActiveAura/
      active_aura anywhere.
[x] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
    — §4 names the one place this module asks to CHANGE an existing contract (AuraRuntime's
      activation state), with the alternative that avoids it.
[x] Corrections are propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    — the false "this is effect-atom E20–E25" attribution is corrected in
      `tasks/backlog-clear-plan.md`; `aura-skill-todo.md`'s own copy is corrected at BP-close
      (Checkpoint 1), and the map row is added with this spec.
```

---

## 1. Objective

Everything needed to put an aura on the lawn exists **except the one row that starts it.**

The consumer chain is complete and shipped, verified end to end this session:

```
RpgStore.ResolveBindings(owner, ctx)        RpgStore.AtomInstances.cs:286
  → AtomPushService.Build(...)              AtomPushService.cs:54
  → RpgHub.BuildApplyCommand()              RpgHub.cs:105   ← OwnerScope(Player, playerId), RuntimeId.Lawn
  → SignalR "Command"
  → AtomPushReceiver.Install(payload)       AtomPushReceiver.cs:64
  → AtomPushInstaller → Funnel.EnqueueModifier   AtomPushReceiver.cs:34
  → EffectBag                               (and from there the lawn, proven live by aura A5)
```

The producer end is empty: `RpgStore.Bind` (`RpgStore.AtomInstances.cs:205`) has **19 test callers and
zero production callers**. The aura A5 live proof had to hand-write an `effect_instance` +
`effect_binding` row for `player:1` to make the chain run, then delete it.

**Done means:** enabling an aura on a commander writes the rows itself, and the aura reaches a live
lawn plant with nothing hand-made anywhere.

### ⛔ The correction this module rests on

`tasks/aura-skill-todo.md:2019` calls this *"`effect-atom` **E20-E25**, another program's named
deliverable."* **That is false and it was believed for three sessions.**
`tasks/effect-atom-plan.md:11` — *"Wave 6 closed same-day, all six modules: E20–E25 fully built and
proven"* — and per `effect-atom-plan.md:113-118` those six are `content-boot`,
`status-stat-applier`, `channel-policy-reader`, `content-codegen`, `validation-in-ci`,
`compose-channel-cache`. **None of them creates a binding.** This module is new and unowned, which is
why it is specced rather than scheduled.

---

## 2. Two wiring gaps found while tracing, which change the scope

Neither was in the task description. Both are **wiring gaps, not architectural walls** — the
distinction `CLAUDE.md` requires — and each is one line of cause.

### G1 — the atom push fires only on `Hello`

`PushGrantSnapshotAsync` has exactly one caller: `RpgHub.cs:43`, inside `Hello`. So the injector
receives bindings **only when it connects or re-injects.**

Consequence, stated plainly: *even with a perfect producer, enabling an aura would do nothing until
the game was restarted.* A binding written at 14:00 sits in SQLite unseen until the next inject.

This is not a design disagreement with the async rule. `commander-surface`'s "changes apply to the
**next** run" is about `board.start` freezing a snapshot — but the injector does not re-`Hello` at
`board.start`, so "next run" never arrives either. The gap is real for both readings.

**Fix:** the producer triggers a push after a successful binding write. `PushGrantSnapshotAsync`
already builds a **full rehydrate** (`RpgHub.cs:89-92` — *"a reconnect must not leave the injector
holding half of its effects"*), so re-sending it is idempotent by construction. No new transport, no
new payload shape.

### G2 — active auras are RAM-only, so bindings would outlive their cause

`AuraRuntimeEndpoints.cs:31` holds `static readonly ConcurrentDictionary<long, AuraRuntime> Runtimes`,
populated by `Runtimes.GetOrAdd` (`:40`). Grepping `FusionRpg.Data` for `ActiveAura` / `active_aura`
returns **nothing**: what is *equipped* persists (`store.GetLoadout(...)`, `:42`), what is **active**
does not.

Bindings are durable rows. Activation is a process-lifetime dictionary. Left as-is, a server restart
leaves durable bindings for auras the runtime no longer believes are active — a desync with no
symptom until someone reads two sources and gets two answers.

---

## 3. Design

### 3.1 Which loop owns this — settled by the doc, then checked against code

`overlay-control-loops.md` is unambiguous and this module sits squarely in it:

- **Cold** is *"Equip item, level-up mod defs, roster deploy templates"*, and it *"never applies to
  Unity directly — **pushes grants/loadout**; Hot applies later"* (`:79`).
- The worked Cold example is literally this shape: *"On deploy/bind: Server pushes Grant templates
  **through Funnel** (no plugin→Bag)"* (`:118`).
- *"Secondary plugin calls Unity / StatusExecutor / `Bag.Grant`"* is a **hard-law anti-pattern**
  (`:196`).

So the producer is **Server-side, Cold loop**, writing durable rows that the existing push carries.
Verified against code rather than inherited from that sentence: the push already resolves
`OwnerScope(Player, playerId)` on `RuntimeId.Lawn` (`RpgHub.cs:105`), which is the exact scope the A5
hand-made row used.

**This module never touches Unity, never calls `Bag.Grant`, and never runs on the Hot path.**

### 3.2 The seam

`AuraRuntimeEndpoints.cs:74` (`/enable`) and `:94` (`/disable`) already own the moment activation
changes, and both already resolve the runtime and the store. The producer hooks there — not at
loadout save, because equipping is not activating, and not at `board.start`, because the server is not
in that conversation.

```
POST /api/aura-runtime/{playerId}/enable
  → runtime.Enable(auraId)                      (existing — refusals unchanged)
  → AuraBindingProducer.Sync(store, playerId, runtime.ActiveAuraIds)   ← NEW
  → push                                        (fixes G1)
```

`Sync` is **declarative, not incremental**: given the set of active aura ids, make the durable rows
match it. Add what is missing, withdraw what is no longer active, leave the rest untouched. An
incremental add/remove API would need every caller to be correct at every call site; a reconcile needs
one function to be correct once.

### 3.3 G2 — the recommendation, and the alternative

**Recommended: bindings become the source of truth for activation.** `AuraRuntime` seeds its active
set from the durable bindings on first resolve instead of starting empty. Two states collapse into
one, and the desync class stops existing rather than being managed.

This is the same move the kernel's `EventQueue` made when it replaced lazy deletion with an indexed
heap: *"it deleted a whole correctness class"* (`tasks/battle-timeline-todo.md` B2). The pattern is
the repo's own.

**Alternative if the owner prefers a smaller blast radius:** persist active aura ids in their own
table and keep bindings derived from that. It works, but it is a third place the same fact lives, and
`resource-hub-ssot.md`'s standing complaint about duplicated state applies.

⛔ **Ask-first**, because it changes `AuraRuntime`'s contract. BP2 may build the producer against
either shape; the reconcile in §3.2 is identical under both. **This decision does not block BP2** — it
blocks only the seeding line.

### 3.4 What the producer writes

Per E6, and nothing beyond it:

| Field | Value | Why |
|---|---|---|
| instance | `Instantiator` output for the aura's container, `origin = "grant"` | E6 owns instantiation; this module does not roll anything |
| `owner_kind` / `owner_key` | `player:{playerId}` | the scope the push already resolves (`RpgHub.cs:105`) |
| `priority` | `0` | E6: the actor list sorts `priority DESC, container_id ASC, seq ASC`, **never `binding_id`**. Auras have no authored precedence yet; a non-zero default would invent one |
| `source` | `"aura"` | E6: *"plugin or feature id, **for withdraw**"* — this is what makes withdraw targetable without touching other features' rows |
| `slot` | `null` | slots are for items |

**Rolls happen at instantiate, never at bind** — E6 Boundaries, *"Never: roll anything at bind time"*.

### 3.5 What it must not do

- **No `entity:{ptr}` bindings.** E6 Boundaries: *"treat `entity:` bindings as **session-scoped and
  never durable** — a pointer can be recycled, and a durable row aimed at a recycled address silently
  retargets."* A commander aura is `player:` scoped and there is no reason to reach lower.
- **No new durable runtime table.** E6: ICD clocks, stacks, counters stay in RAM.
- **No flow control on rejection.** `Bind` returns `AtomRejection.StaleInstance` for a missing
  instance (`RpgStore.AtomInstances.cs:212-214`); the producer must not *rely* on that as its
  existence check. A rejection is an error to surface, not a branch to take.
- **No SQL outside `FusionRpg.Data`** — `guard-dal.ps1` enforces it.

---

## 4. Numeric types and tunables

This module carries no magnitude — it writes identity and ordering only. `priority` is an `int`
ordinal, not a quantity. Aura magnitudes are `AuraMagnitude`'s and their coefficients are Phase 2's
tunables. **Nothing here is a balance number**, so nothing here belongs in `data/tuning/`.

---

## 5. Structure

```
src/FusionRpg.Core/Effects/Atoms/AuraBindingPlan.cs   NEW — pure: (active ids, existing bindings) → adds/withdraws
src/FusionRpg.Server/AuraRuntimeEndpoints.cs          EDIT — call Sync + push on enable/disable
src/FusionRpg.Server/AuraBindingProducer.cs           NEW — applies the plan via RpgStore; triggers the push
src/FusionRpg.Server/RpgHub.cs                        EDIT — expose a push trigger (G1); Hello path unchanged
tests/FusionRpg.Core.Tests/Atoms/AuraBindingPlanTests.cs      NEW
tests/FusionRpg.Server.Tests/AuraBindingProducerTests.cs      NEW — drives the ENDPOINT, not the store
```

**The decision logic is a pure Core function on purpose.** `AuraBindingPlan` takes the active set and
the existing bindings and returns what to add and withdraw, with no store and no I/O — so the
reconcile's edge cases (already bound, no longer active, aura equipped but inactive, duplicate ids) are
table-testable without a database. `AuraBindingProducer` is the thin part that applies it.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Enable an aura with no existing binding | one instance + one binding at `player:{id}`, `source = "aura"` |
| Enable the same aura twice | **no second binding, no revision bump** — the reconcile is idempotent |
| Disable an active aura | its binding withdrawn; **other auras' bindings untouched** |
| Enable A, then enable B evicting A (`MaxActiveAuras`) | A withdrawn and B bound **in one reconcile**, not two half-states |
| Equipped but never enabled | **no binding** — equipping is not activating |
| Binding write succeeds | a push is triggered (G1) — asserted by observing the push, not by reading the code |
| `Bind` returns a rejection | surfaced as an error; **no partial state left behind** (E6: *"never let a rejected bind degrade into a partial bind"*) |
| Endpoint-level | `Server.Tests` drives `POST /api/aura-runtime/{id}/enable`; a store-level test would pass with the endpoint unwired, which is exactly the defect this module exists to fix |
| `guard-dal.ps1` | green — no SQL escapes `FusionRpg.Data` |

**Falsifier required before BP2 is called done:** break the producer and prove the aura stops reaching
a resolved actor. A test that passes because a fixture already had rows proves nothing — that is
precisely how the aura program's A5 proof had to be redone.

⛔ **BP4 is owner-run:** an aura reaches a live lawn plant with **no hand-made instance or binding
row**. Same measurement shape as A5 — a `combat.power.*` channel on a real plant ptr moving by
`AuraMagnitude.Compute(...)` and back.

---

## 7. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AuraBindingPlan"
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~AuraBindingProducer"
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-dal.ps1
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-secondary-no-unity.ps1
```

---

## 8. Boundaries

- **Always:** Cold loop only; `player:` scope; reconcile declaratively; `source = "aura"` so withdraw
  is targetable; surface a rejection rather than branching on it; SQL only in `FusionRpg.Data`.
- **Ask first:** §3.3 (bindings as activation SSOT); any owner scope other than `player:`; a non-zero
  default `priority`; pushing on any trigger other than a binding change.
- **Never:** `entity:{ptr}` durable bindings; rolling at bind time; a new durable runtime table;
  calling `Bag.Grant` or touching Unity from the server; letting a rejected bind leave partial state.

---

## 9. Success criteria

1. Enabling an aura writes exactly one instance + one binding; enabling it again writes nothing.
2. Disabling withdraws exactly that aura's binding and no other.
3. The injector sees the change **without a game restart** (G1 closed).
4. Activation and bindings cannot disagree after a server restart (G2 closed, either shape).
5. ⛔ An aura reaches a live lawn plant with nothing hand-made — the thing A5 could only fake.
6. Four boundary guards green; `guard-dal.ps1` specifically, since this module writes rows.

---

## 10. Open questions (owner)

1. **§3.3 — do bindings become the SSOT for activation, or does activation get its own table?**
   Recommended: bindings. Blocks only the seeding line, not BP2.
2. **Should `board.start` also reconcile?** Not proposed. With G1 fixed, enable-time push covers the
   live case, and adding a second trigger means two paths that must agree. Named here because
   `commander-surface`'s snapshot rule makes `board.start` a natural-looking hook, and the reason it
   is *not* used should be on the record rather than rediscovered.
