# Spec: on-activate-trigger (A18b)

Module **A18b** in the [action map](../action-map.md) §12.1. Depends on A18a (spec written, not yet
built — `spec-action-container-binding.md`).

> **Read [action-map.md](../action-map.md) §12/§12.1 and [spec-action-container-binding.md](spec-action-container-binding.md)
> before this spec.** A18a defines what a bound grant is; this module is the first thing that fires
> against one.

## Objective

Add a new trigger, `OnActivate`, to the atom layer's closed vocabulary — **8 triggers, not 7** — and
wire the one place in `BattleEngine` that fires it: the moment an actor's declared `ActionIntent`
resolves to something real, independent of whether the subsequent attack roll hits or misses.

**Why this needs its own module, not a line inside A18c/d/e:** `effect-atom-map.md`'s H4 hazard and
its own DESIGN-GATE.md row are explicit — *"the vocabulary is closed... adding one is a reviewed
change, not a convenience."* This spec **is** that reviewed change. A18c/d/e all need `OnActivate` to
exist before they have anything to fire against A18a's grants; none of them should each unilaterally
half-decide what it means.

**What "done" looks like:** a bound grant declaring `OnActivate` fires exactly once per turn a
matching actor's action resolves to a real intent — proven by a synthetic grant with a counting sink,
not by any real skill (A18c/d/e own making a *specific* kind actually do something when it fires).

**What this module does NOT do:**
- Decide what `resource.delta`/`shield.grant`/`status.apply`/`stat.modify` DO when `OnActivate` fires
  — only that they CAN be authored with it (§2 below widens exactly the kinds that already declare
  `AllTriggers`; `stat.modify`/`stat.derived` stay untouched, A18e's own call per §12.1's split).
- Price the new trigger. `docs/architecture/effect-atom/definitions.md` §7's `triggerFrequency` table
  (`power_trigger_frequency`) is the mechanism that WOULD price an `OnActivate`-triggered atom's
  expected-fires-per-minute — verified not built: no `power_trigger_frequency` file exists under
  `data/tuning/`, and no code references a per-trigger frequency table (`grep -rn triggerFrequency
  src/` finds only `CostFunction.cs`'s filename match, no symbol). E9's own map status ("both E9's
  coefficients and its function remain open") already carries this gap — `OnActivate` inherits it
  rather than creating a new one. A skill atom triggered `OnActivate` prices the same way every
  other event-triggered atom prices today: correctly for `chance`/`icd`, silently uncounted for
  trigger frequency, same as `OnSpawn`/`OnDamageDealt` already are.

## Design

### 1. The trigger, added to the closed vocabulary

`AtomKind.cs`:

```csharp
public static class AtomTriggers
{
    // ... existing seven ...
    public const string OnActivate = "OnActivate";

    public static readonly string[] All =
        { OnSpawn, OnDamageDealt, OnDamageTaken, OnDeath, OnGranted, OnRemoved, OnTimer, OnActivate };

    /// <summary>The four that fire from a board event.</summary>
    public static readonly string[] Events = { OnSpawn, OnDamageDealt, OnDamageTaken, OnDeath };

    /// <summary>Grant attach / detach — lifecycle, not authorable (§14.2 unchanged).</summary>
    public static readonly string[] Lifecycle = { OnGranted, OnRemoved };

    /// <summary>An actor's own decision to act, independent of any board event or grant lifecycle —
    /// the third category `OnActivate` starts (A18b, spec-on-activate-trigger.md). Not a board event
    /// (no target has necessarily been damaged, spawned, or killed) and not a lifecycle transition
    /// (the grant that owns this atom was already bound, possibly turns ago, at loadout compile —
    /// A18a).</summary>
    public static readonly string[] Actions = { OnActivate };
    // AtomTriggers.None (permanent modifiers) unchanged.
}
```

`AtomKindRegistry.TriggerCount` (a structural cardinality per `tunables-ssot.md` T2, `AtomKindRegistry.cs:20`)
moves **7 → 8** — a guard-tested count, not a balance number, so this is a code change, never a
tuning file.

**Kind eligibility — one line, not a per-kind review.** `AtomKindRegistry.Build()`'s local `AllTriggers`
array (`AtomKindRegistry.cs:23-25`) already feeds exactly `resource.delta`, `status.apply`, and
`shield.grant` — the three kinds A18c/d already own. Appending `AtomTriggers.OnActivate` to that one
array is the whole change; `stat.modify`/`stat.derived` (`AtomTriggers.None`) and the Board-attach kinds
(`spawn.entity`/`board.action`/`grid.spawn`/`grid.clear`/`box.set`, all `AtomTriggers.Events`) are
**deliberately untouched** — the Board kinds are `RuntimeState.None` in battle regardless of trigger
(H3), so widening their trigger list would authorize content nothing in battle can execute, the exact
promise H3 says wave 1 must not make.

### 2. The firing site

One call, in `RunBasicAttackStep` (`Actions/BasicAttack.cs`), placed **after** the intent resolves and
the `loyal` bodyguard redirect runs, but **before** `calculator.Compute` — so `OnActivate` fires
independent of hit/miss (a cast succeeds even if the attack roll misses — the RPG convention this
repo's own content already implies by keeping "landed" effects on the separate `OnDamageDealt`
trigger) and its target is whoever the attack is actually about to resolve against (post-redirect, so
an `OnActivate`-triggered debuff lands on the bodyguard taking the hit, not the original, bypassed
target):

```csharp
// after: var target = state.ByKey[intent.TargetKey!]; ... loyal redirect ...
state.Host.Bag.OnEvent(new EffectEventDto
{
    Trigger = AtomTriggers.OnActivate,
    ActorPtr = attacker.Setup.Key,
    TargetPtr = target.Setup.Key,
    Tick = nowTick,
    HitCount = 1,
});
state.Host.Flush();
```

`Bag.OnEvent` (`Effects/EffectBag.cs:311`) is self-contained — it finds matching grants
(`_grants.Matching(ev)`), executes their action plan through the sink, and flushes the funnel
internally; the caller's `Host.Flush()` afterward matches the existing convention every other
mutation site in `BattleRunState`/`BattleEngine` already follows (`DispatchHit`, `ReviveImmortals`),
for `AcknowledgeWindow()`'s bookkeeping, not to re-flush what `OnEvent` already flushed.

An intent that resolves to `ActionIntent.None` (Break, hazard 3) fires nothing — no action was used.
The basic attack (no bound grant, A18a's own scope) makes this call a genuine no-op today: `_grants.Matching(ev)`
finds nothing owned by that actor declaring `OnActivate`, so **zero grants means zero RNG draws and
zero sink calls** — proven in this module's own tests, not assumed, the same discipline this session
already applied to A17.

### 3. A real content hazard, found empirically while building this module's own tests

**A grant fires on `OnActivate` both when its own owner acts, and when its owner is merely the
*target* of someone else's activation.** Not a bug in this module — a direct, provable consequence of
the owner-matching this whole system already uses (`EffectOwnerKey.MatchesEvent`,
`EffectProcAndOwner.cs:47-57`): an `entity:{ptr}`-owned grant matches any event whose `ActorPtr` *or*
`TargetPtr` names that entity, the same dual-check `OnDamageDealt`/`OnDamageTaken` content already
relies on to let both an attacker's and a defender's grants see the same hit. `OnActivate` inherits it
for free, with a consequence worth naming loudly for whoever authors the first real `OnActivate`
content: a **self-buff-on-activate** grant (e.g. "gain +Atk when I act") will *also* fire the moment
its owner is merely the target of someone else's attack — a defender's grant sees the attacker's
`OnActivate` too, because the attacker's event carries the defender as `TargetPtr`.

Verified directly, not just reasoned about: `OnActivateTriggerTests.Fires_once_per_resolved_intent_regardless_of_hit_or_miss`
's first attempt predicted `1 × Rounds` self-damage applications and measured **`2 × Rounds`** — the
bound actor's own grant fired once for its own activation and once more every round the *other* side
activated against it. The test now asserts the correct `2×` — no test file is wrong here; the finding
changed the prediction, not the mechanism.

**No fix belongs in this module.** The dual-match is the SAME mechanism every other trigger already
depends on; "fixing" it for `OnActivate` alone would special-case one trigger out of a shared,
load-bearing behavior. A content author who wants "only when I am the actor, not merely a target"
needs an explicit filter — `PassesOverlayFilters` (`EffectProcAndOwner.cs:66`) has no such filter
today (only `side`/`typeId`/`actorIsKiller`), so this is named as a real, open content-authoring gap
for whichever module first ships real `OnActivate` self-buff content — not solved here, not hidden
either.

## Tunables

None new. `power_trigger_frequency` (when it exists) will carry an `OnActivate` row, but that table
does not exist in code or data today (see Objective) — nothing for this module to author.

## Numeric types

None new. `EffectEventDto.Tick` reuses the existing `long` tick convention (`SimulationClock.Now`,
already the type `nowTick` carries through `RunBasicAttackStep` since T37).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~OnActivateTrigger"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1
```

## Project structure

```
src/FusionRpg.Core/Effects/Atoms/AtomKind.cs       (OnActivate const + Actions grouping + All)
src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs (TriggerCount 7->8; AllTriggers local array +1 line)
src/FusionRpg.Core/Actions/BasicAttack.cs           (the OnEvent call site, in RunBasicAttackStep)
tests/FusionRpg.Core.Tests/Battle/Adoption/OnActivateTriggerTests.cs
```

## Testing strategy

- **Vocabulary count** — `AtomTriggers.All.Length == 8`, `AtomKindRegistry.TriggerCount == 8`; an
  existing architecture test presumably already asserts the old count (`AtomKindRegistryTests`-shaped)
  — find and update it, per Design Gate evidence rule 6 (propagate, don't leave stale).
- **A bound `OnActivate` grant fires exactly once per resolved intent** — a synthetic container (A18a's
  resolver) bound to an actor, declaring `OnActivate`, wired to a counting/recording sink or a real
  `resource.delta` (reusing A18c once it lands, or a minimal recording stub if built first) — assert
  it fires once per attack the actor actually declares, zero times on a `Break` round.
- **Fires independent of hit/miss** — force a guaranteed-miss combat setup (e.g. an extreme
  hit-chance-suppressing derived snapshot) and confirm the `OnActivate` grant still fired even though
  no `AttackStepOutcome.Proceed` followed.
- **Fires with the post-redirect target** — a `loyal` bodyguard fixture where the `OnActivate` grant's
  recorded `TargetPtr` is the bodyguard, not the originally-selected ward.
- **Golden-neutral by construction, proven not assumed** — every existing golden/trait/expedition test
  has no actor with a bound `OnActivate` grant (A18a's own scope: nothing binds one without a real
  `ContainerId`, and nothing today supplies one) — full suite green, zero hashes moved, zero new RNG
  draws recorded on the existing pre-adoption trace fixtures.
- **Kind eligibility, not behavior** — `AtomKindRegistry.Get("resource.delta").AllowsTrigger("OnActivate")`
  true; same for `shield.grant`/`status.apply`; `AtomKindRegistry.Get("stat.modify").AllowsTrigger("OnActivate")`
  **false** — proving the one-line `AllTriggers` change reached exactly the three intended kinds and no
  others.

## Boundaries

- **Always:** fire `OnActivate` through `Bag.OnEvent`, the same seam every other trigger already uses
  — never a bespoke "run these atoms now" path that bypasses the grant/trigger model A18a established.
- **Ask first:** widening `stat.modify`/`stat.derived`'s trigger list, or any Board-attach kind's — both
  are named, deliberate exclusions above, not oversights.
- **Never:** fire `OnActivate` for a `Break`ing intent; fire it twice for one resolved intent (once at
  declare, once at redirect) — it is one event per attacker per round, matching `OnDamageDealt`'s own
  once-per-landed-hit cardinality.

## Success criteria

1. `OnActivate` exists in the closed vocabulary at count 8, with a named, documented reason it is a
   third category (`Actions`) rather than folded into `Events` or `Lifecycle`.
2. Exactly `resource.delta`, `shield.grant`, `status.apply` may carry it; `stat.modify`/`stat.derived`
   and every Board kind may not.
3. It fires exactly once per attacker whose intent resolves to a real (non-`Break`) attempt, regardless
   of hit/miss, targeting whoever the attack is actually about to resolve against.
4. Zero goldens moved — proven, since no bound grant declaring it exists in any content that ships
   today.

## Propagation once approved

Per Design Gate evidence rule 6, three doc surfaces name "7 triggers" today and all three need the
same edit in the same change this module lands in, not a stray one:
`docs/DESIGN-GATE.md`'s atom-layer row, `docs/architecture/effect-atom-map.md`'s own count claims,
and `AtomKind.cs`'s own doc comment ("The 7 triggers an atom's `when` may name").
