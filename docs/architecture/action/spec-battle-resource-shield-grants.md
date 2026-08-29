# Spec: battle-resource-shield-grants (A18c)

Module **A18c** in the [action map](../action-map.md) §12.1. Depends on A18a
(`spec-action-container-binding.md`) and A18b (`spec-on-activate-trigger.md`).

> **Read both dependency specs and `action-map.md` §12/§12.1 first.** A18a defines what a bound grant
> is; A18b adds the `OnActivate` trigger. This module fires the OTHER trigger these two kinds mostly
> care about (`OnDamageDealt`) and proves both kinds actually execute once fired.

## Objective

Make `resource.delta` (FA10) and `shield.grant` (the unnumbered eleventh opcode) actually execute in
battle when a bound grant fires — closing exactly the gap `AtomKindRegistry.cs` names for both:
*"Full again when battle grows a grant path"* (`resource.delta`) and *"battle also needs a grant
path"* (`shield.grant`, T14 already fixed its other half — `Bag.ShieldGate`).

**What "done" looks like:** a real skill container (A18a-bound) with a `resource.delta` atom heals or
damages the right actor when its trigger fires; one with a `shield.grant` atom grants a real, later
`ShieldRuntime`-tracked shield; one carrying `resource.delta`'s DoT/contagion payload
(`statusId`/`periodMs`/`durationMs`) applies a real, ticking `StatusRuntime` instance — all through the
exact mechanism `EffectBag` already runs for Lawn, unmodified.

**What this module does NOT do:**
- Wire `stat.modify` (A18e) or `status.apply`'s own FA2 opcode (A18d) — this module's two kinds are
  `resource.delta` and `shield.grant` only, per the map's own split rationale (§12.1: these two were
  already `RuntimeState.Full`-except-for-the-grant-path; the other two are markedly further from live).
- Build a new DoT/contagion mechanism. **Finding, verified against code, correcting an assumption I
  nearly carried into this spec:** the payload is not unbuilt — `EffectBag.cs:439` already calls
  `StatusEffectBridge.TryApplyFromGrant` inside the `ApplyResourceDelta` branch of `FireGrant`,
  platform-agnostic, shared by every host. It is gated on two settable, currently-unset properties
  (`EffectBag.Status` and `EffectBag.StatusRng`, `EffectBag.cs:169-170`) — the **exact same shape**
  T14 already found and fixed for `ShieldGate`. This module's job is that one-line wiring, twice, not
  a new mechanism.

## Design

### 1. Two wiring lines, T14's own pattern

`BattleRunState`'s constructor, immediately after the existing `Host.Bag.ShieldGate = ShieldGate;`
(T14, `BattleRunState.cs:136`):

```csharp
Host.Bag.Status = Status;
Host.Bag.StatusRng = StatusRng;
```

`Status` and `StatusRng` are `BattleRunState`'s own already-constructed fields — the same
`StatusRuntime` and `BattleStatusRng` (wrapping the `"status"` RNG stream) the round loop already
uses for `Status.Tick(...)`. **Not a new stream, not a new instance** — a grant-applied status now
rolls against the identical stream a scripted/DoT-pulse status already does, which is what keeps this
byte-identical for any battle where no bound grant ever fires (every battle that exists today).

Leaving `Bag.StatusRng` at its default (`FixedStatusRng(0.0)`, `EffectBag.cs:170`) would have been a
real bug if missed: every grant-applied status's `GrantChance` roll would silently read as `0.0` (always
succeeds) rather than drawing from battle's real stream — deterministic, but not what "battle mode
consumes FA10 only... the SAME `Funnel` merge/mailbox rules" (`decisions.md` row 37) actually promises.
Caught here, not in a later module that would have inherited it silently.

### 2. The `OnDamageDealt` firing site

`resource.delta`'s existing shipped content (`fx.poison_on_hit`, `fx.freeze_on_hit`, `fx.cold_on_hit`,
`fx.butter_on_hit`) is `OnDamageDealt`-triggered, not `OnActivate` — a skill's on-hit rider should fire
when the hit actually lands, mirroring existing content exactly. `BattleEngine` never calls
`Bag.OnEvent` with this trigger today (confirmed: zero hits for `OnEvent(` under `src/FusionRpg.Core/Battle/`
before this reopening). New call site in `RunBasicAttackStep`, right after `breakdown.Hit` is
confirmed true (before the `Continue`/miss return, so a miss never fires it — matching hit-gated
content's own name):

```csharp
if (!breakdown.Hit) return new AttackStep(AttackStepOutcome.Continue, null, 0);

// --- NEW, this module ---
state.Host.Bag.OnEvent(new EffectEventDto
{
    Trigger = AtomTriggers.OnDamageDealt,
    ActorPtr = attacker.Setup.Key,
    TargetPtr = target.Setup.Key,
    Damage = -signedDelta,   // magnitude, matching EffectEventDto.Damage's own sign convention
    Tick = nowTick,
    HitCount = 1,
});
state.Host.Flush();
// --- end new ---

state.Cooldowns.Start(attacker.Setup.Key, intent.Envelope, nowTick);   // EXISTING (T38) — shown for placement, not touched by this module
return new AttackStep(AttackStepOutcome.Proceed, target, signedDelta);
```

Placed **before** `DispatchHit` runs (which happens in the caller, `BattleEngine.cs`'s round loop,
against the `Proceed` outcome) — a landed hit's own `resource.delta`/`shield.grant` riders fire
alongside the calculator-resolved damage, not nested inside the trait tail `DispatchHit` owns (A5's
own boundary: the trait tail is `EngineBehavior`, not the declared action).

**Composes with A18b's own firing site, does not replace it.** By the time this module lands,
`RunBasicAttackStep`'s body fires `OnActivate` once (A18b, right after the `loyal` redirect, before
`calculator.Compute`) and `OnDamageDealt` once (this module, only on a landed hit) — two independent
`Bag.OnEvent` calls at two different points in the same method, not two competing designs for the
same call site. Neither spec shows the other's insertion in context; an implementer building both
should read this note rather than assume one subsumes the other.

### 3. What proves each kind works

**Correction, from building this module's own tests (T46), against my own first-draft claim below:**
plain `resource.delta` does **not** reach `BattleEffectSink.Execute` at all — traced precisely:
`FireGrant`'s `ApplyResourceDelta` branch (`EffectBag.cs:419-489`) builds a `DamagePacket`
(`DamagePacketBuilder.FromOverlay`) and calls `CombatDamageDispatcher.DispatchInstant(...)` directly,
`continue`-ing before ever reaching the generic `_sink.Execute(ctx, item)` call at the bottom of the
loop (line 510) that every OTHER action (`ModifyStat`, `ApplyStatus`, ...) goes through.
`DispatchInstant` itself queues onto `Funnel`, and `Bag.OnEvent`'s own `Funnel?.Flush()` (right after
the grants loop) is what ultimately calls `_sink.Execute` — so `BattleEffectSink.Execute`'s FA10
branch **is** reached, just Funnel-mediated, never directly from `FireGrant`. The outcome my first
draft predicted was right; the mechanism named was wrong. **Proven working end to end regardless**,
empirically, via `EffectBag`'s own sensible defaults for combat math it needs
(`CombatPolicy.Default`, `PassThroughCombatMath.Instance`, a fixed-seed `SeededCombatRng` —
`EffectBag.cs:163-165`) — no extra wiring needed beyond a live grant and a firing event, matching the
original claim's own bottom line.

- **`resource.delta`, plain amount:** proven via a real shipped def, `fx.overlay_damage`
  (`EffectAtomCatalog.Generated.cs:204-222`) — ships with empty `Params` (`{channel: "hp"}` only),
  every magnitude overlay-driven (D7); `amount`/`targetPtr` supplied on the **grant's** `Overlay`.
- **`resource.delta`, DoT/contagion payload:** once `Bag.Status`/`Bag.StatusRng` are wired (§1),
  `EffectBag.cs:439`'s existing branch calls `StatusEffectBridge.TryApplyFromGrant` against battle's
  real `StatusRuntime` — a granted DoT applies as a real `StatusInstance`, ticking on the same
  event-driven schedule B16 already gave every other status. **No shipped content exercises this at
  all** (verified: zero `statusId` hits in `EffectAtomCatalog.Generated.cs` — `fx.poison_on_hit` uses
  the separate `ApplyStatus`/FA2 action, A18d's own kind, not this FA10 piggyback), so this half is
  proven with a synthetic def, the only way to reach it today. **Load-bearing detail, found only by
  running the test, not by reading the code first:** `TryApplyFromGrant` reads the DoT payload
  (`statusId`/`periodMs`/`durationMs`/`amount`) from **`grant.Overlay` directly**
  (`EffectBag.cs:439-441` passes it, not the def-Params/grant-Overlay merged dictionary `FireGrant`
  builds for the instant packet) — the payload belongs on the grant, never the def's own `Actions[0].Params`,
  even though the plain-amount case above tolerates either.
- **`shield.grant`:** bag-side (`ExecGrantShield`, not `IEffectActionSink.Execute` at all — the
  eleventh, unnumbered opcode per `AtomKindRegistry.cs`'s own `shield.grant` comment). `Bag.ShieldGate`
  is already wired (T14) to the exact same `ShieldGate`/`ShieldRuntime` ordinary attacks absorb
  through — a granted shield and a swing-dealt hit share one stack, not two, already proven by T14's
  own tests. This module needs no new shield-side code, only a live grant and a firing event.
  **`GrantShield`'s own overlay allowlist has no flat `targetPtr` key** (`EffectProcAndOwner.cs:167-171`
  — only `amount`/`element`/`priority`/`sourceClass`/`durationTicks`/`refillOnMerge`/`target`), unlike
  `ApplyResourceDelta`'s allowlist, which has both — an author supplying `targetPtr` on a shield grant
  gets a loud `"unknown overlay key 'targetPtr'"` rejection, not a silent ignore; use the nested
  `target: {mode, ptr}` shape instead.

**A second real finding, shared with A18b's own §3:** the owner-matching dual-check
(`EffectOwnerKey.MatchesEvent`, `ActorPtr` OR `TargetPtr`) means a `resource.delta`/`shield.grant`
grant owned by one actor fires on **that actor's own** `OnDamageDealt` *and* on the *other* side's
`OnDamageDealt` against them — proven directly in `BattleResourceShieldGrantsTests`, not merely
inferred from A18b's finding. Unlike `OnActivate` (fires unconditionally), `OnDamageDealt` only fires
on a **landed hit** (`RunBasicAttackStep`'s own `if (!breakdown.Hit) return Continue` gates it before
this module's own call site) — so an exact "2× per round" prediction over-counts by however many
rounds included a miss; only a directional ("strictly more damage/absorption with the grant bound
than without") claim is robust without also tracking landed-hit counts per side.

## Tunables

None new. This module wires existing mechanism against new callers; no balance number is authored.

## Numeric types

None new. `EffectEventDto.Damage` is `long?`, matching `signedDelta`'s existing `long` type.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BattleResourceShieldGrants"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1
```

## Project structure

```
src/FusionRpg.Core/Battle/BattleRunState.cs   (Bag.Status / Bag.StatusRng wiring, next to Bag.ShieldGate)
src/FusionRpg.Core/Actions/BasicAttack.cs     (the OnDamageDealt OnEvent call site)
tests/FusionRpg.Core.Tests/Battle/Adoption/BattleResourceShieldGrantsTests.cs
```

## Testing strategy

- **Plain `resource.delta` heals/damages the right actor** — a bound grant (A18a) with a real
  `EffectAtomCatalog`-shipped `resource.delta` def, fired via a landed hit; assert the target's Hp
  moved by the def's authored amount, through the same clamp `BattleEffectSink` already applies.
- **DoT/contagion payload applies a real, ticking status** — a `resource.delta` grant carrying
  `statusId`/`periodMs`/`durationMs`; assert `state.Status.ForHost(targetKey)` holds a real instance
  after the hit, and that it delivers pulses on schedule (reusing B16's own event-driven pulse
  machinery — no new status delivery path).
- **`GrantChance` rolls against the real stream, not the `FixedStatusRng` default** — two identical
  setups differing only in seed produce different apply outcomes for a `GrantChance < 1.0` status,
  proving `Bag.StatusRng` is genuinely wired to `state.StatusRng` and not silently defaulted.
- **`shield.grant` grants a real, absorbing shield** — a bound grant fires; assert a subsequent hit is
  partially absorbed, through the same `ShieldGate`/`ShieldRuntime` T14's own tests already exercise.
- **`OnDamageDealt` fires once per landed hit, never on a miss** — mirrors A18b's own `OnActivate`
  hit/miss-independence test, but the opposite assertion.
- **Golden-neutral by construction, proven not assumed** — no content shipped today binds a container
  (A18a's own scope), so `Bag.OnEvent(OnDamageDealt)` finds zero matching grants on every existing
  golden/trait/expedition battle — full suite green, zero hashes moved, zero new RNG draws on the
  pre-adoption trace fixtures.

## Boundaries

- **Always:** reuse `state.Status`/`state.StatusRng` — never construct a second `StatusRuntime` or a
  second RNG stream for the grant-applied path.
- **Ask first:** widening `BattleEffectSink.Execute` itself beyond `ApplyResourceDelta` — that is
  A18d's kind (`status.apply`, FA2), a different opcode through a different branch.
- **Never:** let a grant-applied status bypass `StatusRuntime`'s own immunity/resist-floor evaluator —
  `StatusEffectBridge.TryApplyFromGrant` already routes through it; this module must not add a second,
  shorter path that skips it.

## Success criteria

1. A real `resource.delta` atom, bound and fired, changes the right actor's Hp by the right amount.
2. A real `resource.delta` atom carrying a DoT/contagion payload applies a real `StatusInstance`
   through `StatusRuntime`, rolling `GrantChance` against battle's own `"status"` stream.
3. A real `shield.grant` atom, bound and fired, grants a shield that a later hit measurably absorbs.
4. Zero goldens moved — proven, since no content shipping today binds a grant at all.

## Propagation once approved

Per Design Gate evidence rule 6, two `AtomKindRegistry.cs` cells flip once this module lands:
`resource.delta`'s `RuntimeSupportMatrix(Full, None, PlanOnly)` → Battle `None → Full`
(`AtomKindRegistry.cs:146`, whose own comment already names the trigger: *"Full again when battle
grows a grant path"*), and `shield.grant`'s equivalent cell (`AtomKindRegistry.cs:221`) does the same
— both comments should be updated in the same change, not left describing a now-superseded state.
