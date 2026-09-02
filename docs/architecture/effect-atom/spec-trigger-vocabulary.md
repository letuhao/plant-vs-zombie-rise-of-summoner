# Spec: trigger-vocabulary (E34)

**Program:** effect-atom · **Map:** [../effect-atom-map.md](../effect-atom-map.md) §13 ·
**Ideal:** [../effect-atom-ideal.md](../effect-atom-ideal.md) §W8.2 ·
**Definitions (win over this spec):** [definitions.md](definitions.md) ·
**Kind registry:** [spec-atom-kind-registry.md](spec-atom-kind-registry.md)

**Status: specced, unbuilt (2026-09-03).** Wave 8. E35 and E36 both depend on it.

E34 owns the **input half** of the atom vocabulary: which host events become atom triggers. It adds
five — `OnWave`, `OnMatchStart`, `OnMatchEnd`, `OnSunCollect`, `OnGridPlace` — to the closed trigger
list, to the runtime contract, and to `EffectEventAdapterCore.TryMap`, and it decides which kinds may
carry them. It adds no kind, no attach point and no executor: *what happens* when a wave starts is
E35's and E36's.

---

## 1. What exists today

**Built** — the host side is largely done, which is why this module is small.

| Fact | Evidence |
|---|---|
| `TryMap` maps exactly **five** host event families | `src/FusionRpg.Core/Effects/EffectEventAdapterCore.cs:14-42`; everything else returns `null` at `:44` |
| Every capture kind already reaches the mapper | `GameHooks.Emit` → `EffectRuntime.OnCapture` (`GameHooks.cs:85`) → `EffectEventAdapter.TryMap` (`EffectRuntime.cs:284`) |
| `wave.change` is emitted, with `wave` and `maxWave` on the payload | `GameHooks.cs:333-341` (`PollBoard`, on `board.theWave` change) |
| `wave.spawn` and `wave.huge` are emitted, each carrying a wave number | `GameCaptureHooks.cs:214`, `:221` |
| `board.start` is emitted with `matchKey`, `levelName`, `modifiers` | `GameHooks.cs:457-467` |
| `board.end`, `match.win`, `match.lose` are emitted | `GameHooks.cs:507`, `:545`, `:536` |
| `sun.gain` is emitted with `count` and `save` | `GameCaptureHooks.cs:176` |
| `grid.place` is emitted | `GameCaptureHooks.cs:698` |
| Prior research already sorted the host feasibility | `docs/research/effect-runtime/07-effect-opportunities.md:28` — `onWave` is **PROBE** (wired, needs a LIVE row); `:29` `onMindControl` **PROBE**; `:30` `onHitLand` **NOT SHIPPED** (~134 overrides, no LIVE events) |
| The bag matches triggers by case-insensitive string, so a new trigger executes with no bag change | `EffectBag.cs:387` |

**Wiring gap.**

| Gap | Evidence |
|---|---|
| All eight kinds above are emitted, reach `TryMap`, and fall through to `return null` | `EffectEventAdapterCore.cs:44` |
| `EffectTriggers` and `/effects/contract` publish 7 (E33 makes it 8) | `EffectDtos.cs:11-20`; `DebugEndpoints.cs:382-387` |
| `EffectEventDto` has no field for a wave number | `EffectDtos.cs:66-83` |

**Real gap.**

| Gap | Evidence |
|---|---|
| No trigger vocabulary exists for a match-scoped event, so no kind can declare one | `AtomKind.cs:62-99`; `AtomKindRegistry.cs:27-29` |
| `onHitLand` cannot be added — the host does not ship the events | research `:30`, and `combat.hitland` is emitted but not from base overrides. **Out of scope, named** |
| `onMindControl` and `onCardPlay` / `onMowerTrigger` stay PROBE / unmeasured. **Out of scope, named** | research `:29`; the ideal §W8.2's absent list |

## 2. The contract

### 2.1 Five new triggers

```csharp
// src/FusionRpg.Core/Effects/Atoms/AtomKind.cs
public static class AtomTriggers
{
    // ... the existing eight ...
    public const string OnWave       = "OnWave";
    public const string OnMatchStart = "OnMatchStart";
    public const string OnMatchEnd   = "OnMatchEnd";
    public const string OnSunCollect = "OnSunCollect";
    public const string OnGridPlace  = "OnGridPlace";

    /// <summary>Match-scoped: no actor, no target. A kind carrying one of these must be able to
    /// act with no entity in hand.</summary>
    public static readonly string[] MatchEvents = { OnWave, OnMatchStart, OnMatchEnd };

    /// <summary>Board-economy events. They carry a ptr sometimes and must never require one.</summary>
    public static readonly string[] BoardEconomyEvents = { OnSunCollect, OnGridPlace };
}
```

`AtomKindRegistry.TriggerCount` gains **+5** — `8` today at `AtomKindRegistry.cs:20`, so `13` after this
module (structural, `tunables-ssot.md` T2 — a guard-tested cardinality, never a tuning row). The same
five constants are mirrored into `FusionRpg.Contracts.EffectTriggers` and into `/effects/contract`'s
array, ordinally identical, for the reason E33 states: a published list that lies is the defect.

**E33 publishes eight into the same array first; E34 takes it to thirteen.** `spec-activation-edge.md`
§2.1 now states that merge order from its own side and writes the assertion as *"publishes every
constant declared in `EffectTriggers`, and no others"* rather than a literal count — which is what lets
E34 grow the list without E33's test reading as a regression. Do not replace it with a literal here.

**E34 adds no opcode, so it does not touch the `actions` array.** That array publishes ten of
`EffectActions`' twelve constants (`DebugEndpoints.cs:388-394`; constants at `EffectDtos.cs:22-44`) and
**E33 owns repairing it** (`spec-activation-edge.md` §2.1a). E34's principle — a published list that
lies is the defect — applies to both arrays; it is stated here so the `actions` half is not left
un-owned by the two modules that named the principle.

### 2.2 Host event → trigger, in `EffectEventAdapterCore.TryMap`

| Host kind(s) | Trigger | Fields set |
|---|---|---|
| `wave.change` · `wave.spawn` · `wave.huge` | `OnWave` | `Wave` (below), `MatchKey`, `Tick`. No `ActorPtr`, no `Side` |
| `board.start` | `OnMatchStart` | `MatchKey`, `Tick` |
| `board.end` · `match.win` · `match.lose` | `OnMatchEnd` | `MatchKey`, `Tick` |
| `sun.gain` | `OnSunCollect` | `MatchKey`, `Tick`, `Damage` **not** used — the count is a resource amount, see below |
| `grid.place` | `OnGridPlace` | `MatchKey`, `Tick`, `TypeId` (grid item type), `ActorPtr` when the payload carries `ptr` |

**The one DTO change.** `EffectEventDto` gains a single nullable field:

```csharp
[JsonPropertyName("wave")] public int? Wave { get; set; }
```

An additive nullable field breaks no shape, so `FoundationContractVersion.Current` stays 2 per its own
rule (`EffectDtos.cs:5`). `sun.gain`'s `count` deliberately gets **no** field — a predicate over
collected sun is `effect_atom` predicate work (E3's closed leaf list) and is out of scope here; the
trigger alone is what E34 owes.

**`wave.change` versus `wave.spawn`.** Both fire per wave and both are already emitted, so mapping both
double-fires an `OnWave` atom. The mapper therefore maps `wave.change` (the polled `board.theWave`
transition, `GameHooks.cs:333-341`) as the canonical edge, and maps `wave.spawn`/`wave.huge` **only**
when the payload's wave number differs from the last one mapped — the same one-edge-per-wave discipline
`PollBoard` already applies. This is stated because it is exactly the shape of the `combat.hit` versus
`*.damage` double-count that `CombatHitEmitPolicy` already exists to suppress (`EffectRuntime.cs:280`).

### 2.3 Kind eligibility — deliberately narrow

The five new triggers are added to exactly the kinds that can act **with no entity in hand**:

| Kind | Gains | Why |
|---|---|---|
| `spawn.entity`, `board.action`, `grid.spawn`, `grid.clear`, `box.set` | `MatchEvents` + `BoardEconomyEvents` | Board-attach, already on `AtomTriggers.Events` (`AtomKindRegistry.cs:300`, `:310`, `:321`, `:329`, `:343`); each takes explicit `row`/`col` params and needs no event target |
| `resource.economy` | `MatchEvents` + `BoardEconomyEvents` | FA9 writes the match bank; it has no target either (`:208`) |
| `match.modify` (E35), `wave.control` (E36) | `MatchEvents` | These kinds exist for exactly this |

**`resource.delta`, `status.apply` and `shield.grant` gain nothing.** They resolve their target from
the event (`ResolveStatusTargetPtr`, `InjectorEffectActionSink.cs:200-207`), and a match-scoped event
has no ptr — so an empty resolve drops `status.apply` into the unguarded board-wide
`FindObjectsOfType<Zombie>()` loop at `:251-256`. That is G5, and widening these three would make an
already-open hole authorable. `stat.modify` / `stat.derived` are permanent modifiers and stay
triggerless (`definitions.md` §14.2).

### 2.4 Owner keys — and the zombie branch is an open hole, not an accidental refusal

`EffectOwnerKey.MatchesEvent` gains an explicit arm: for the three `MatchEvents`, a `plant:{tid}` or
`zombie:{tid}` grant **refuses**, naming the reason. `match`, `player:` and `entity:` are unchanged
(`:46-59`).

> **⛔ CORRECTED 2026-09-03 — this said both branches "happen to return `false`". Only the plant one
> does, and the two board-economy triggers leak through the other.**
>
> - **The plant branch (`EffectProcAndOwner.cs:14-28`) is narrowed by trigger.** It names four —
>   `OnSpawn`/`OnDeath`/`OnDamageTaken` at `:17-19`, `OnDamageDealt` at `:25` — then `return false`
>   at `:27`. Anything else genuinely falls off the end. The original description fits this branch.
> - **The zombie branch (`:30-44`) names no trigger at all.** Its only gate is a side check at
>   `:32-40`, which refuses only when `ev.Side` is **present and not `"zombie"`**; it then returns
>   `(ev.TypeId ?? ev.TargetTypeId) == tid` at `:41-43`. A match-scoped event has no side, so `:37-38`
>   waves it through, and whether it matches comes down to whether it carries a `TypeId`.
>
> **The concrete leak this module creates.** §2.2 maps `grid.place` → `OnGridPlace` with
> **`TypeId` = the grid item type**. So the moment E34 lands, **a `zombie:7` grant fires on every
> placement of grid item type 7** — a zombie-type-keyed container reacting to a gravestone. Nothing
> declares that; it is the unnarrowed return matching a field E34 newly populates. `OnSunCollect` sits
> on the same path and is safe only because §2.2 gives `sun.gain` no `TypeId` — a safety that lasts
> exactly as long as nobody adds one, which is not a guarantee.
>
> **So E34 arms the zombie branch too.** The explicit arm covers **`MatchEvents` *and*
> `BoardEconomyEvents`** — all five new triggers — in **both** type-keyed branches: a `plant:{tid}` or
> `zombie:{tid}` grant refuses every one of the five, naming the reason. A type-keyed owner means
> *"this entity type"*; none of the five is about an entity type, and `OnGridPlace`'s `TypeId` is a
> **grid item** type that happens to share a field with a zombie type. Two vocabularies in one `int?`
> is what makes the refusal explicit rather than incidental.
>
> **E33 edits the same branch** (`spec-activation-edge.md` §2.3, for `OnActivate`) and its correction
> records the same asymmetry. Whichever lands second reads the branch as the other left it; neither
> re-derives it.

## 3. What it must NOT do

- **No new kind, no new attach point, no executor.** E34 is input only.
- **No `onHitLand`, `onMindControl`, `onCardPlay`, `onMowerTrigger`.** The first is NOT SHIPPED
  host-side and the rest are PROBE or unmeasured (research `:28-30`). Adding a trigger whose host event
  does not fire is the `status.expose.*` defect (`spec-atom-kind-registry.md`, the code-or-data rule).
- **No predicate leaf.** *"When the wave is above 10"* is a predicate over `wave`, and E3 owns the
  closed leaf list. E34 supplies the trigger, not the condition.
- **No widening of `resource.delta` / `status.apply` / `shield.grant`.** §2.3 gives the reason and the
  line number.
- **No magnitudes.** This module carries no numbers. The repo's numeric rules still bind wherever one
  appears downstream: `long` for a magnitude, never `float`; widen before multiplying; divide by 1000
  last, exactly once; overflow throws; and a number a balance pass would turn lives in
  `data/tuning/<domain>.v{n}.json`, not in code.

## 4. Testing strategy

Core tests cover the vocabulary, the mapper and the owner key. **The injector is not built by CI** —
`.github/workflows/ci.yml` tests ten managed projects and never compiles `FusionRpg.Injector`. The
emit sites in §1 are therefore asserted only in the LIVE run below.

| Case | Expect |
|---|---|
| `AtomTriggers.All.Length == AtomKindRegistry.TriggerCount`, both five higher than before this module | pass — the guard at `AtomKindRegistryTests.cs:80-81` is a self-consistency check, so it asserts the **delta**, never a literal |
| Every `AtomTriggers` constant has an ordinally equal `EffectTriggers` one | pass, asserted pairwise over `All` |
| `/effects/contract` triggers | contains every constant declared in `EffectTriggers` and no others — the same assertion E33 writes, green at 8 and at 13 (§2.1) |
| `TryMap("board.start", …)` | `OnMatchStart`, `MatchKey` set, `ActorPtr` null |
| `TryMap("wave.change", {wave: 4})` | `OnWave`, `Wave == 4` |
| `TryMap("wave.spawn", {wave: 4})` immediately after the above | returns `null` — one edge per wave |
| `TryMap("sun.gain", …)` | `OnSunCollect`; **no** field carries `count` |
| `AtomKindRegistry.ValidateTrigger("status.apply", "OnWave")` | `TriggerNotAllowed` |
| `AtomKindRegistry.ValidateTrigger("board.action", "OnWave")` | `Ok` |
| `MatchesEvent(plant:7 grant, OnMatchStart ev)` | `false` |
| `MatchesEvent(zombie:7 grant, OnMatchStart ev)` | `false`, by the explicit arm — not by falling off the end |
| `MatchesEvent(zombie:7 grant, OnGridPlace ev {typeId: 7})` | `false`. **Without §2.4's arm this returns `true`** through `EffectProcAndOwner.cs:41-43`, because the event has no side and carries a grid item type in the same field a zombie type uses |
| `MatchesEvent(zombie:7 grant, OnSunCollect ev)` | `false`, and it stays `false` if `sun.gain` ever gains a `TypeId` |
| **PLANTED VIOLATION** — drop the **zombie** half of §2.4's arm, keeping the plant half | the `OnGridPlace` row above **fails**: a `zombie:7` container fires on every placement of grid item type 7. The plant half alone never covered this, which is how the hole survived the first draft |
| **PLANTED VIOLATION** — add `OnWave` to `AllTriggers` so `status.apply` accepts it | the `TriggerNotAllowed` test fails. This is the guardrail on G5: without it, a wave-triggered status is authorable and silently statuses every zombie on the board |
| **PLANTED VIOLATION** — drop the wave-number de-dupe so `wave.spawn` maps unconditionally | the one-edge-per-wave test fails, and it fails loudly rather than as a doubled magnitude nobody notices |

**LIVE proof (owner-run):** bind a container holding one `OnWave` `resource.economy` atom, play through
two waves, and read the sun bank. Two waves must produce exactly two grants, not three or four.

## 5. Acceptance criteria

1. `AtomTriggers` declares five more triggers and `TriggerCount` matches `AtomTriggers.All.Length`,
   guard-tested as a self-consistency check rather than against a literal.
2. Every atom trigger has an ordinally equal `EffectTriggers` constant, asserted pairwise.
3. `/effects/contract` publishes every constant declared in `EffectTriggers` and no others — the
   assertion E33 writes, which stays green through this module's five (§2.1). E34 adds no opcode,
   so the `actions` array is untouched here; E33 owns its repair.
4. `TryMap` maps all eight new host kinds to the five new triggers, with the fields in §2.2.
5. `wave.spawn` / `wave.huge` do not produce a second `OnWave` for a wave already mapped.
6. `EffectEventDto.Wave` is the only DTO addition, and `FoundationContractVersion.Current` stays 2.
7. The five triggers are allowed on exactly the kinds in §2.3 and refused on the other five.
8. `MatchesEvent` refuses a type-keyed grant — `plant:{tid}` **and** `zombie:{tid}` — for **all five**
   new triggers (`MatchEvents` and `BoardEconomyEvents`), by an explicit arm in each branch. The
   zombie branch is not narrowed today (`EffectProcAndOwner.cs:30-44`), so this is a real refusal
   being added, not a refusal being documented (§2.4).
9. All three planted violations in §4 fail their tests, including the zombie-branch one.
10. The LIVE proof shows exactly one fire per wave.

## 6. Dependencies and cross-program hazards

**Depends on:** nothing hard. It shares `EffectTriggers` with E33, so land E33 first or merge the two
contract edits in one change.

**Blocks:** E35 `match-modify`, E36 `wave-control`.

| Hazard | Detail |
|---|---|
| **Injector not built by CI** | `.github/workflows/ci.yml`; the emit sites in §1 are only proven live. `effect-atom-map.md` §6 H1 recurring |
| **battle-timeline B25/B26 vs the drain chain** | B26 freezes shield + DoT behaviour while E34 edits the mapper every trigger flows through. Map §16 — sequence, do not straddle |
| **Double-fire is a real balance bug, not a tidiness one** | An `OnWave` economy atom that fires twice per wave doubles a payout, and the goldens will not catch it because no shipped content uses the trigger yet. §2.2's de-dupe and its planted test are the mitigation |
| **Predicate pressure** | The moment `OnWave` exists, *"only after wave 10"* will be wanted. That is E3's leaf list and a separate reviewed change; say so rather than adding a leaf under this module |
| **Stale instances** | E34 touches no content table — no `catalog_revision` bump, no `StaleInstance` fallout |
