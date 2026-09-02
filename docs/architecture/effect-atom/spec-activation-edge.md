# Spec: activation-edge (E33)

**Program:** effect-atom · **Map:** [../effect-atom-map.md](../effect-atom-map.md) §13 ·
**Ideal:** [../effect-atom-ideal.md](../effect-atom-ideal.md) §W8.2.1, §W8.3 ·
**Definitions (win over this spec):** [definitions.md](definitions.md) ·
**Kind registry:** [spec-atom-kind-registry.md](spec-atom-kind-registry.md)

**Status: specced, unbuilt (2026-09-03).** Wave 8, independent — the only Wave 8 module the action
corpus is blocked on, and the map says it should run early.

E33 owns the **activation edge on the lawn**: the seam that turns *"this actor decided to act"* into an
`OnActivate` atom event, so a bound container's `OnActivate` atoms fire outside Battle. It owns the
contract constant, the capture kind, the adapter arm, the owner-key arm and the fast gate. It does
**not** own a game-facing producer — that is `A9 movement-actions` — and it does not build a scheduler,
which `decisions.md` row 97 (amended 2026-09-03) explicitly keeps off the lawn.

---

## 1. What exists today

**Built** — nothing here is re-built by this module.

| Fact | Evidence |
|---|---|
| `OnActivate` is a real, authorable trigger | `src/FusionRpg.Core/Effects/Atoms/AtomKind.cs:71`, in `All` at `:74`, in its own `Actions` category at `:93` |
| `TriggerCount = 8`, guard-tested against `AtomTriggers.All.Length` | `AtomKindRegistry.cs:20`; `tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:80` |
| Three kinds may carry it — `resource.delta`, `status.apply`, `shield.grant` | `AtomKindRegistry.cs:27-29` (`AllTriggers`), used at `:193`, `:230`, `:275` |
| Battle raises it, once per resolved intent, at the post-redirect target | `src/FusionRpg.Core/Actions/BasicAttack.cs:88-95` |
| The bag matches triggers by **case-insensitive string**, not by an enum | `src/FusionRpg.Core/Effects/EffectBag.cs:387` — so no bag change is needed for a new trigger to execute |
| `match`, `player:` and `entity:` owner keys match trigger-agnostically | `src/FusionRpg.Core/Effects/EffectProcAndOwner.cs:11-12`, `:46-59` |

**Wiring gap** — a shipped path that is inert, not an architectural wall.

| Gap | Evidence |
|---|---|
| `EffectTriggers` declares **7** and omits `OnActivate`, so the runtime contract and the atom vocabulary disagree | `src/FusionRpg.Contracts/EffectDtos.cs:11-20` |
| `/effects/contract` publishes the same stale 7 and advertises `frozen = true` | `src/FusionRpg.Server/DebugEndpoints.cs:378-388` |
| No fast gate — the injector has four `HasOn*Grant()` helpers and no `OnActivate` one, so a producer would have to fire unconditionally | `src/FusionRpg.Injector/Effects/EffectRuntime.cs:145-159` |
| `OnActivate` can already be fired **by hand** on a live lawn: `debug.effect.fire-synthetic` takes a free-string trigger and calls `FireSynthetic` | `CheatCommandRunner.cs:364-368`, `:2052`; `EffectRuntime.cs:330-338`. A debug-only entry point — evidence the plumbing works, not a producer |

**Real gap** — code that must be written.

| Gap | Evidence |
|---|---|
| Nothing in the injector raises it | `grep -rn "OnActivate" src/FusionRpg.Injector/ --include=*.cs` returns **0** |
| A `plant:{typeId}` grant can **never** see an `OnActivate` event: the branch names four triggers and then `return false` | `EffectProcAndOwner.cs:14-28` — the four are `OnSpawn`/`OnDeath`/`OnDamageTaken` at `:17-19` and `OnDamageDealt` at `:25`, then the bare `return false` at `:27` |
| A `zombie:{typeId}` grant **already sees `OnActivate` today** — that branch names **no triggers at all** | `EffectProcAndOwner.cs:30-44`. See the correction in §2.3: this row previously claimed it was narrowed the same way, and it is not |
| `EffectEventAdapterCore.TryMap` has no capture kind that produces it, so nothing on the wire can | `EffectEventAdapterCore.cs:14-44` |

## 2. The contract

### 2.1 Contract parity — `EffectTriggers` gains the eighth

```csharp
// src/FusionRpg.Contracts/EffectDtos.cs
public static class EffectTriggers
{
    // ... the existing seven, unchanged ...

    /// <summary>An actor's own decision to act. Mirrors AtomTriggers.OnActivate (A18b) — the two
    /// constants must be byte-identical, because EffectBag matches by string.</summary>
    public const string OnActivate = "OnActivate";
}
```

`/effects/contract`'s `triggers` array (`DebugEndpoints.cs:382-387`) grows by one in the same change,
from the seven it publishes today. A published list that lies is the defect this module exists to
close; leaving it at seven repeats it.

**And E34 edits the same guardrail immediately afterwards — so E33 must not pin it as a constant.**

> **⛔ CORRECTED 2026-09-03 — E33 asserted 8 and E34 asserts 13, and only E34 said so.** E34
> (`spec-trigger-vocabulary.md` §2.1) adds five more triggers and takes the same array to thirteen; its
> §6 says *"land E33 first or merge the two contract edits in one change"*. **E33 said nothing
> reciprocal**, and it is E33's criterion 2 that pins the count — a guardrail one module owns and
> another module is scheduled to rewrite is the shape that makes the second edit look like a
> regression.
>
> **The merge order, stated from this side too:** E33 lands first and publishes **8**; E34 lands next
> and takes it to **13**. Either module may instead merge both contract edits into one change — that
> is legal and E34 says so. What is not legal is E34 editing E33's count-asserted test without the
> commit message naming E33's criterion 2, or E33 shipping a test whose failure message reads as a
> regression when E34 does exactly what it is supposed to.
>
> **How the test is written so both are satisfiable:** the count assertion reads
> `EffectTriggers`' own declared constants — *"`/effects/contract` publishes every constant in
> `EffectTriggers`, and no others"* — rather than a literal `8`. It goes red when the array and the
> class disagree, which is the defect, and stays green when both grow together, which is E34.

### 2.1a The `actions` array lies in the same way, and E33 owns fixing it

> **⛔ ADDED 2026-09-03 — E33 and E34 both make *"a published list that lies"* their principle, and
> both fixed `triggers` only.**

`/effects/contract` publishes **ten** action opcodes (`DebugEndpoints.cs:388-394`) under
`frozen = true` (`:381`). `EffectActions` declares **twelve** (`EffectDtos.cs:22-44`).
**`GrantShield` and `ModifyDerivedStat` are missing** — `GrantShield` has a live executor, and
`ModifyDerivedStat` is declarative-by-design (its own doc comment at `EffectDtos.cs:36-43` explains
that nothing executes it) but is no less part of the published vocabulary for that.

E33 adds both, and the array's assertion is written the same way as the trigger one: *"publishes every
constant in `EffectActions`, and no others."* That is the form that survives Wave 8, because
**E35 (`ModifyMatch`), E36 (`WaveControl`) and E37 (`BulletModify`) each add an opcode** and each of
those specs now says it must appear here. A literal count would make three later modules edit a test
they did not break.

E33 was already the right owner: it is the module the map says should run early, it is already opening
this endpoint, and repairing two existing holes costs nothing extra once the file is open.

**No `FoundationContractVersion` bump.** `Current = 2` documents itself as *"bump when EffectEvent /
IntentPlan / Grant DTO shapes break"* (`EffectDtos.cs:5`) — a new constant breaks no shape and adds no
field.

### 2.2 The capture kind — `actor.activate`

The edge reaches the atom layer the way every other lawn event does: `GameHooks.Emit` →
`EffectRuntime.OnCapture` (`GameHooks.cs:85`) → `EffectEventAdapter.TryMap` (`EffectRuntime.cs:284`).
That keeps `SimEffectHost` in parity for free, since it shares the same mapper (`SimEffectHost.cs:170`).

| JSON key | Type | Required | Maps to |
|---|---|---|---|
| `actorPtr` | string (hex) | **yes** | `EffectEventDto.ActorPtr` |
| `targetPtr` | string (hex) | no | `EffectEventDto.TargetPtr` |
| `side` | `"plant"` \| `"zombie"` | **yes** | `EffectEventDto.Side` |
| `typeId` | int | no | `EffectEventDto.TypeId` — the **actor's** type |
| `targetTypeId` | int | no | `EffectEventDto.TargetTypeId` |
| `actionId` | string | no | **not mapped** — telemetry only. The atom layer has no action vocabulary and gains none here |

A payload with no `actorPtr` maps to `null` (no event), never to a board-wide fan-out. That is the
inverse of G5's `FindObjectsOfType<Zombie>()` hole (`InjectorEffectActionSink.cs:251-256`), and it is
why `actorPtr` is required rather than defaulted.

### 2.3 The owner-key arm — and the two branches are **not** symmetric

> **⛔ CORRECTED 2026-09-03 — this spec's claim about the zombie branch was false, and the fix it
> described is a behaviour change, not a wiring fix.**
>
> §1 said *"`zombie:{typeId}` is narrowed the same way"*. It is not. Read `EffectProcAndOwner.cs`:
>
> - **The plant branch (`:14-28`) is narrowed.** It names four triggers — `OnSpawn`, `OnDeath`,
>   `OnDamageTaken` (`:17-19`) and `OnDamageDealt` (`:25`) — and then `return false` at `:27`. An
>   `OnActivate` event falls off the end and is refused. **That half of the original claim is right.**
> - **The zombie branch (`:30-44`) names no trigger in its match decision.** Its only gate is a side
>   check (`:32-40`, which returns `false` only when `ev.Side` is non-null and not `"zombie"`), and it
>   then returns `(ev.TypeId ?? ev.TargetTypeId) == tid` at `:41-43`. **Any trigger matches**,
>   `OnActivate` included.
>
> And Battle already raises `OnActivate`: `BasicAttack.cs:87-94` fires one per resolved intent at the
> post-redirect target. **So the zombie branch is live code that `OnActivate` events already flow
> through, and editing it changes a shipped runtime** — it is not the inert path this module is
> otherwise made of, and it must not be described as one.
>
> **Why it does not misfire *yet*, and why that is luck rather than design:** Battle's emit sets only
> `Trigger`, `ActorPtr`, `TargetPtr`, `Tick` and `HitCount` (`BasicAttack.cs:87-94`). `Side`, `TypeId`
> and `TargetTypeId` are all `int?`/`string?` and stay null (`EffectDtos.cs:66-83`), so `:41`'s
> `(ev.TypeId ?? ev.TargetTypeId) == tid` compares `null` to an `int` and is false. **E33's own
> `actor.activate` capture kind changes that**: §2.2 maps `side` and `typeId` onto the event, which is
> exactly the shape `:41-43` matches on. The unnarrowed return does not misfire today because nothing
> hands it a typed `OnActivate` event; this module is the thing that starts handing it one.

**What E33 does, stated as two different changes:**

1. **Plant branch — a wiring fix.** `EffectOwnerKey.MatchesEvent` gains an `OnActivate` clause beside
   the `OnDamageDealt` one at `EffectProcAndOwner.cs:25-26`: for `plant:{tid}`, `OnActivate` matches
   when `ev.Side == "plant"` **and** `ev.TypeId == tid` — the actor's own type, never the target's.
   Nothing matched before; something matches now. No shipped behaviour changes, because nothing raises
   `OnActivate` on the plant side yet.

2. **Zombie branch — a behaviour change on a shipped runtime, and it is owned here.** The branch gains
   the same explicit clause: for `zombie:{tid}`, `OnActivate` matches when `ev.Side == "zombie"`
   **and** `ev.TypeId == tid`. **This replaces an implicit match with a narrower explicit one**, and
   the difference is not cosmetic:

   - the unnarrowed `:41-43` return also matches on **`TargetTypeId`** when `TypeId` is null, so an
     activation *aimed at* zombie type 7 would fire a grant owned by zombie type 7 — the target's type
     standing in for the actor's, which is the exact thing this section's rule forbids;
   - it also matches when `ev.Side` is **null**, because `:37-38` only refuses a side that is present
     and wrong.

   Both paths close. Battle's own `OnActivate` emit carries neither field today, so no Battle
   behaviour observable right now flips — but the code being edited is on Battle's live path, and E33
   is simultaneously introducing the typed events that would have travelled the open path. **Say this
   in the commit message and in the rollout note**, in those terms: *a narrowing change to a shipped
   owner-key branch, no shipped content affected*. "No content uses it" is a fact to state, not a
   reason to leave it unsaid.

`match`, `player:` and `entity:` need no change (`:46-59`).

**E34 arms the same branch for its own triggers.** `spec-trigger-vocabulary.md` §2.4 adds the refusal
for match-scoped and board-economy events, which leak through the same unnarrowed `:41-43` return.
Whichever lands second reads the branch as the other left it.

### 2.4 The gate

```csharp
// src/FusionRpg.Injector/Effects/EffectRuntime.cs — alongside the four at :145-159
public static bool HasOnActivateGrant() => Bag.HasGrantWithTrigger(EffectTriggers.OnActivate);
```

A producer calls this before building a payload dictionary. The 2026-08 perf audit's finding was
per-hit allocation and uncached resolves on the Unity main thread; an ungated activation emit is the
same shape.

## 3. What it must NOT do

- **No scheduler, no queue, no per-actor turn machine on the lawn.** `decisions.md` row 97, as amended
  2026-09-03, permits an *activation edge* and nothing more. If a design needs ordering between two
  lawn activations, that is out of scope and needs its own decisions row.
- **No new `EffectEventDto` field.** `actionId` stays in the capture payload. Adding a field to a DTO
  the contract calls frozen is a separate, reviewed change.
- **No widening of which kinds may carry `OnActivate`.** A18b already chose the three
  (`AtomKindRegistry.cs:27-29`); `stat.modify`'s widen was A18e's call. E33 changes no cell.
- **No position write.** `decisions.md` row 105 (Lawn position write) is DRAFTED, not built, and is
  `action-corpus`'s. A movement action's reposition is not this module's.
- **No magnitude is chosen here** — this module carries no numbers at all. Where the repo's numeric
  rules would apply they still bind: a magnitude is `long`, never `float`; widen before multiplying;
  divide by 1000 last, exactly once; overflow throws rather than wraps; and any number a balance pass
  would turn lives in `data/tuning/<domain>.v{n}.json`, never in code.

## 4. Testing strategy

`FusionRpg.Core.Tests` covers the contract, the mapper and the owner key. **The injector is not built
by CI** — `.github/workflows/ci.yml` restores and tests ten managed projects and never compiles
`FusionRpg.Injector`, which needs the game's interop assemblies. Everything injector-side is proven by
a LIVE falsifier, not by a green pipeline.

| Case | Expect |
|---|---|
| `EffectTriggers.OnActivate == AtomTriggers.OnActivate` | equal, ordinal. Guards the exact string the bag matches on |
| `/effects/contract` trigger list | contains every constant declared in `EffectTriggers` **and no others** — so a constant added without publishing it, or published without declaring it, fails. E34 takes both to 13 and this test stays green (§2.1) |
| `/effects/contract` action list | contains every constant declared in `EffectActions` **and no others** — red today, because `GrantShield` and `ModifyDerivedStat` are declared and not published (§2.1a) |
| `TryMap("actor.activate", {actorPtr, side})` | `Trigger == OnActivate`, `ActorPtr` set |
| `TryMap("actor.activate", {})` — no `actorPtr` | returns `null`. **Never** an event with an empty actor |
| `MatchesEvent(plant:7 grant, OnActivate ev {side=plant, typeId=7})` | `true` |
| `MatchesEvent(plant:7 grant, OnActivate ev {side=zombie, typeId=7})` | `false` |
| `HasOnActivateGrant()` with no such grant | `false` |
| `MatchesEvent(zombie:7 grant, OnActivate ev {side=zombie, typeId=7})` | `true` |
| `MatchesEvent(zombie:7 grant, OnActivate ev {side=null, typeId=7})` | `false` — the explicit clause requires the side. Today the unnarrowed branch returns **true** here (`EffectProcAndOwner.cs:37-43`), so this test is the one that pins the behaviour change §2.3 describes |
| `MatchesEvent(zombie:7 grant, OnActivate ev {side=zombie, typeId=null, targetTypeId=7})` | `false` — the actor's own type, never the target's. Today the unnarrowed branch returns **true** |
| `MatchesEvent(zombie:7 grant, Battle's own OnActivate shape — no side, no typeId)` | `false`, before and after. Pins that this module moves no Battle behaviour anyone can observe today |
| **PLANTED VIOLATION** — revert the `OnActivate` clause in the **plant** branch (restore the bare `return false` at `EffectProcAndOwner.cs:27`) | the `plant:7` match test **fails**. A guardrail that cannot fail is not one, and this is the exact regression that leaves type-keyed containers inert |
| **PLANTED VIOLATION** — revert the `OnActivate` clause in the **zombie** branch (fall back through to the unnarrowed `:41-43` return) | the two `false` rows above **fail**, because the branch starts matching on a null side and on the target's type. The plant-branch violation alone never touched this half, which is how §2.3's asymmetry survived the first draft |
| **PLANTED VIOLATION** — set `EffectTriggers.OnActivate = "onActivate"` | the parity test fails, naming both constants. The bag's `OrdinalIgnoreCase` compare would hide this at runtime, which is why it is asserted ordinally |

**LIVE proof (owner-run, not CI):** grant a container holding one `OnActivate` `resource.delta` atom to
a selected lawn plant, fire `debug.effect.fire-synthetic` with `trigger: "OnActivate"`, and read the
target's HP before and after. The falsifier is the withdrawn state — the same fire must change nothing.

## 5. Acceptance criteria

1. `EffectTriggers.OnActivate` exists and is ordinally equal to `AtomTriggers.OnActivate`.
2. `/effects/contract` publishes **every** constant declared in `EffectTriggers` and no others —
   eight after this module, thirteen after E34, with the same test green for both (§2.1).
3. `/effects/contract` publishes **every** constant declared in `EffectActions` and no others.
   `GrantShield` and `ModifyDerivedStat` are added; the assertion is written so E35/E36/E37's
   opcodes do not require editing it again (§2.1a).
4. `EffectEventAdapterCore.TryMap` maps `actor.activate` → `OnActivate`, and maps a payload with no
   `actorPtr` to `null`.
5. `EffectOwnerKey.MatchesEvent` matches an `OnActivate` event for `plant:{tid}` / `zombie:{tid}` on
   the actor's own side and type, and refuses the wrong side, a **missing** side, and a match that
   came from `TargetTypeId`. The zombie half is a **narrowing behaviour change** on a branch Battle's
   live path flows through, recorded as such in the commit message and the rollout note (§2.3).
6. `EffectRuntime.HasOnActivateGrant()` exists, and every producer added later calls it first.
7. All four planted violations in §4 fail their tests, including the zombie-branch one.
8. A LIVE run shows a bound `OnActivate` atom firing on the lawn and **not** firing once withdrawn.
9. No `EffectEventDto` field and no `AtomKindRegistry` cell changes here. `decisions.md` row 97 was
   already amended (2026-09-03, see the header); no further row moves.

## 6. Dependencies and cross-program hazards

**Depends on:** nothing. E33 may run at any point, and the map says it should run early.

**Shares the contract endpoint with E34.** E33 lands first and publishes eight triggers; E34 takes the
same array to thirteen. Merging both contract edits into one change is legal and E34 says so. §2.1
states the merge order from this side and writes the assertion so both are satisfiable.

**Unblocks:** `A9 movement-actions` ([../action-map.md](../action-map.md)), which supplies the
production producer.

| Hazard | Detail |
|---|---|
| **D6 recurrence** | This module ships a seam with **no production caller of its own**. D6's lesson (`definitions.md` §13; `AtomKindRegistry.cs:140-143`) is that a path with no consumer is accepted and then does nothing forever. Mitigation, and it is hard: E33 is not "done" on a green suite — criterion 7's LIVE run is required, and if A9 does not follow in the same window the map row must say **inert**, in that word |
| **Injector not built by CI** | `.github/workflows/ci.yml` — this is `effect-atom-map.md` §6's H1 hazard recurring, and it is why §4 splits managed tests from the LIVE proof |
| **battle-timeline B25/B26** | B26 freezes shield + DoT behaviour while this module edits `EffectProcAndOwner`, which every trigger flows through. Sequence them; never straddle (map §16) |
| **A guardrail E34 is scheduled to rewrite** | Criterion 2 pins the published trigger list, and E34's whole job is to grow it. §2.1's *"every constant in `EffectTriggers`, and no others"* form is what keeps that from reading as a regression. If either module changes to a literal count, the other's commit looks like a broken test |
| **A narrowing change on Battle's live path** | §2.3 point 2. `EffectProcAndOwner` is not lawn-only; Battle's `OnActivate` emit (`BasicAttack.cs:87-94`) flows through the branch E33 edits. Nothing observable moves today, and that is a measured claim (Battle's emit carries no `Side` and no `TypeId`), not an assumption |
| **Stale instances** | E33 touches no content table, so there is no `catalog_revision` bump and no `StaleInstance` fallout |
