# Spec: battle-status-apply (A18d)

Module **A18d** in the [action map](../action-map.md) §12.1. Depends on A18a
(`spec-action-container-binding.md`) and A18b (`spec-on-activate-trigger.md`).

> **Read both dependency specs and `action-map.md` §12/§12.1 first.**

## Objective

Make `status.apply` (FA2) execute in battle through the real grant/trigger pipeline A18a/b built —
today its `RuntimeSupportMatrix` is `(Full, Partial, PlanOnly)`: Battle's "Partial" is **setup only**
(`BattleRunState`'s own scripted `InitialStatuses` loop, applied once at construction, never through
an atom or a trigger). This module is what makes a real skill's `status.apply` atom apply a real,
timed status when the skill is used — not just at battle start.

**What "done" looks like:** a bound grant (A18a) carrying a `status.apply` atom, fired via
`OnActivate` or `OnDamageDealt` (A18b), applies a real `StatusInstance` through `StatusRuntime` —
resistance/immunity-evaluated, the same evaluator every other application path already goes through.

**What this module does NOT do, and why — verified against code, not assumed:**
- **Scale a magnitude from FA2's `level` param.** Checked `StatusDef` (`ResistanceEvaluator.cs:33-52`,
  the catalog's own definition record): it carries no magnitude, period, or duration field at all —
  every status application anywhere in this codebase supplies those at the CALL site, never reads
  them from the catalog. FA2's own registry doc comment (`AtomKindRegistry.cs:182-184`) already draws
  the line: *"The DoT/contagion payload... lives on FA10 `resource.delta`"* — FA2 is documented as the
  **non-pulsing** counterpart (CC/buff/debuff-flag statuses, `PayloadKinds` of `UnityCc`, never
  `PulseHp`). `BaseMagnitude = 0` for every FA2 application is not a narrowing; it is what this opcode
  has always meant. `level` has no reader anywhere in Core — the injector's own `ExecApplyStatus`
  (`InjectorEffectActionSink.cs:196-235`) does not feed it into `StatusRuntime` either; it calls a
  wholly separate Unity-side path (`DebugActions.ApplyStatusToZombie`, decisions.md's "Unity CC via L4
  StatusExecutor") that has no Core equivalent at all. `level` is accepted and threaded through
  (so authored content round-trips) but **inert in battle**, named exactly like A17 named
  `MinRange`/`MaxRange` inert without a board — a documented gap, not a silent drop.
- Touch `resource.delta`'s own status path (A18c) — that is a different opcode, already-built
  mechanism (`StatusEffectBridge.TryApplyFromGrant`), a different branch of `FireGrant` entirely.

## Design

### 1. `BattleEffectSink` gains `status.apply` support — via settable properties, not constructor
injection, and this is a correction against my own first draft, not a stylistic choice

**A constructor-injection design does not compile against the real code, verified precisely, not
assumed:** `BattleRunState`'s constructor builds `Host = new BattleEffectHost(...)` at
`BattleRunState.cs:115`, **two lines before** `Status = new StatusRuntime(...)` at line 117. Any
`BattleEffectHost` constructor that takes a `StatusRuntime` parameter cannot be called at the point
`Host` is actually built — the value does not exist yet. This is not a preference against constructor
injection; it is a hard ordering fact in the file this module edits.

**The fix, matching the pattern T14 and A18c already established** (`Host.Bag.ShieldGate =`,
`Host.Bag.Status =` — a settable property, assigned *after* the dependency exists): `BattleEffectSink`
(`Battle/BattleEffects.cs:59-88`, currently only holding `Func<string, IBattleHpTarget?> resolveActor`)
gains two new settable properties and a constructor-time `FakeEffectClock` reference:

```csharp
sealed class BattleEffectSink : IEffectActionSink
{
    readonly Func<string, IBattleHpTarget?> _resolve;
    readonly FakeEffectClock _clock;
    public StatusRuntime? Status { get; set; }
    public IStatusRng? StatusRng { get; set; }

    public BattleEffectSink(Func<string, IBattleHpTarget?> resolve, FakeEffectClock clock)
    {
        _resolve = resolve;
        _clock = clock;
    }
    // ...
}
```

`BattleEffectHost`'s own constructor (`BattleEffects.cs:27-37`) currently builds `_sink` **before**
`Clock` (`_sink = new BattleEffectSink(resolveActor); Clock = new FakeEffectClock();`) — these two
lines swap order (`Clock` first, then `_sink = new BattleEffectSink(resolveActor, Clock)`), a purely
internal reordering with no effect on any external caller, since neither is read before both
complete. `BattleEffectHost` itself gains two forwarding properties, its **public constructor
signature unchanged**, so neither of the two existing call sites (`BattleRunState.cs:115`,
`BattleEffectHostTests.cs:19`) needs to change:

```csharp
public StatusRuntime? Status { set => _sink.Status = value; }
public IStatusRng? StatusRng { set => _sink.StatusRng = value; }
```

`BattleRunState`'s constructor sets both **after** `Status`/`StatusRng` exist (near A18c's own
`Host.Bag.Status = Status;` line, not at `Host`'s own construction site):

```csharp
Host.Status = Status;
Host.StatusRng = StatusRng;
```

`Status`/`StatusRng` are `BattleRunState`'s own already-constructed fields — the exact same instances
A18c wires onto `Bag.Status`/`Bag.StatusRng` for the resource.delta path (a **different** object,
`EffectBag`, not `BattleEffectSink` — the two modules touch different classes in different files, so
despite both needing "the same `StatusRuntime`," they do not conflict on the same lines). **One
`StatusRuntime`, one RNG stream, every application path** — never a second instance a grant-applied CC
status would roll against differently than a scripted or DoT-pulse one.

**The clock bug this fix also catches:** using `T0` (battle start, a fixed historical moment used only
for `InitialStatuses`'s own scripted setup) as `StatusRuntime.Apply`'s `now` argument would be wrong
for any status applied after round 1 — every live-fired status would compute its expiry relative to
battle start, not to when it actually landed. `_clock.UtcNow` is the live, round-loop-updated value
(`state.Host.Clock.UtcNow = now;` already runs every round before any event dispatch) — the correct
"now" for a live apply, and the reason `BattleEffectSink` needs its own `_clock` reference at all
rather than reusing `T0`.

`BattleEffectSink.Execute` gains a branch before its existing FA10-only early return:

```csharp
if (string.Equals(item.Action, EffectActions.ApplyStatus, StringComparison.OrdinalIgnoreCase))
{
    if (Status is null || StatusRng is null) return true;   // not wired (e.g. a bare test harness) — refuse quietly, not a NullReferenceException

    var statusId = item.Params.TryGetValue("status", out var s) ? s as string : null;
    if (string.IsNullOrWhiteSpace(statusId)) return true;   // malformed content, refused upstream at bind

    var durationSec = item.Params.TryGetValue("duration", out var d) ? Convert.ToDouble(d) : 4.0;
    var durationMs = (int)Math.Round(durationSec * 1000);
    var targetPtr = item.Params.TryGetValue("targetPtr", out var p) ? p as string : ctx.Event.TargetPtr;
    if (string.IsNullOrWhiteSpace(targetPtr)) return true;

    // BaseDuration and DurationMs are the SAME unit (ms) — found empirically while building T49:
    // StatusRuntime.Apply uses eval.EffectiveDuration (derived FROM BaseDuration) whenever
    // BaseDuration > 0, so an earlier draft passing durationSec (seconds) here produced a 5ms status
    // for an authored 5-SECOND fx.poison_on_hit duration. Verified against the existing scripted-
    // InitialStatuses call (BattleRunState.cs), which already passes the identical ms value to both.
    Status.Apply(new StatusApplyInput(
        StatusId: statusId!,
        HostPtr: targetPtr!,
        AttackerPtr: ctx.Event.ActorPtr,
        GrantId: item.GrantId,
        BaseMagnitude: 0,                          // FA2 never pulses HP — see Objective
        BaseDuration: durationMs,
        DurationMs: durationMs,
        GrantChance: 1.0,                          // the atom's own `when.chance` gate already ran
        EffectId: item.EffectId,
        PluginId: "battle",
        AttackerLess: ctx.Event.ActorPtr is null), StatusRng, _clock.UtcNow);
    return true;
}
```

`Status`/`StatusRng` are nullable and checked, not asserted — `BattleEffectSink` is constructed before
`BattleRunState` finishes building `Status`/`StatusRng` (§ above), so there is a real, if narrow,
window where a bare `BattleEffectHost` (the existing `BattleEffectHostTests.cs` harness, or `SimEffectHost`-
adjacent test code that never calls `Host.Status = ...`) legitimately has neither wired — refusing
quietly there is the same "not every host implements every kind" posture `RuntimeSupportMatrix` already
formalizes, not a new exception to design around.

`GrantChance: 1.0` — the atom's own bind-time `chance` gate (E4's `when_json.chance`) already decided
whether this plan item exists at all; `StatusRuntime.Apply`'s own `GrantChance` roll would be a SECOND,
redundant gate rolling against the wrong semantics (a per-application roll, not a per-atom-fire roll)
if left at any value but certain-apply.

### 2. Duration units, named because getting this wrong is silent

FA2's `duration` param is **seconds** (registry: *"Seconds, not milliseconds. FA2 predates the
integer-ms rule"*) — `StatusApplyInput.DurationMs` is milliseconds. The `× 1000` conversion above is
the one place this module must get right; a test asserts it directly rather than trusting the
multiplication reads correctly on review.

## Tunables

None new. No balance number is authored by this module.

## Numeric types

`DurationMs` stays `int` (matches `StatusApplyInput`'s existing field type) — status durations are
authored in the seconds-to-low-thousands-of-ms range, nowhere near `int`'s overflow threshold for a
duration (not a magnitude — `CLAUDE.md`'s overflow table concerns power-scaled magnitudes, and a
status duration is not one).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BattleStatusApply"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1
```

## Project structure

```
src/FusionRpg.Core/Battle/BattleEffects.cs   (BattleEffectSink gains Status/StatusRng settable properties + a Clock ctor
                                               param; BattleEffectHost gains forwarding Status/StatusRng setters — its
                                               OWN public constructor is UNCHANGED)
src/FusionRpg.Core/Battle/BattleRunState.cs  (Host.Status = Status; Host.StatusRng = StatusRng; — after both exist)
tests/FusionRpg.Core.Tests/Battle/Adoption/BattleStatusApplyTests.cs
```

## Testing strategy

- **A real `status.apply` atom applies a real, timed status** — a bound grant with a shipped
  `fx.*`-style `status.apply` def, fired via `OnActivate`; assert `state.Status.ForHost(targetKey)`
  holds an instance with `ExpiresAt` matching the authored `duration` (seconds → ms, asserted as an
  exact conversion, not "close enough").
- **Resistance/immunity still evaluates** — a target with an immunity tag matching the applied status
  is refused through the same `ResistanceEvaluator` path scripted statuses already use; proves this
  module did not add a second, shorter apply path.
- **`level` round-trips but does nothing observable** — a fixture varying `level` with everything else
  held constant produces byte-identical `StatusInstance` state; the named gap proven, not merely
  documented.
- **One `StatusRuntime`, one RNG stream** — a chance-gated scenario (via the atom's own `when.chance`,
  not `GrantChance`) draws from `state.StatusRng`'s existing `"status"` stream, proven by the same
  determinism check A18c's spec uses.
- **Golden-neutral by construction** — no content shipping today binds a `status.apply` grant; full
  suite green, zero hashes moved.
- **Applies real time, not battle-start time** — a status fired several rounds into a battle has an
  `ExpiresAt` computed from the round it was actually applied at, not from `T0`; the regression this
  module's own clock-reference fix exists to prevent, proven by asserting `ExpiresAt` differs correctly
  between a round-1 fire and a round-5 fire of the identical grant.
- **A `BattleEffectSink` with `Status`/`StatusRng` unset refuses quietly, not with a
  `NullReferenceException`** — directly exercises the existing `BattleEffectHostTests.cs`-style bare
  construction path (no `Host.Status = ...` call), proving this module did not silently require every
  caller to opt in or crash.
- **The existing two `BattleEffectHost` construction call sites are unaffected** — `BattleRunState.cs:115`
  and `BattleEffectHostTests.cs:19` both compile and pass unchanged; this module's own tests are the
  only new caller of the new `Host.Status`/`Host.StatusRng` setters.

## Propagation once approved

Per Design Gate evidence rule 6: `AtomKindRegistry.cs`'s `status.apply` entry
(`RuntimeSupportMatrix(Full, Partial, PlanOnly)`, `AtomKindRegistry.cs:179`) flips its Battle cell
`Partial → Full` once this module lands — its own comment ("battle... setup only") becomes the
finding this module closes, not a description of the shipped state anymore. `BattleEffectSink.Execute`'s
inline comment (`"battle mode consumes FA10 only; other actions are inert here"`,
`BattleEffects.cs:70`) also stops being true the moment this branch lands — update it in the same
change, not left for whichever of A18d/A18e happens to land last to notice.

## Boundaries

- **Always:** route through `StatusRuntime.Apply` and its resistance evaluator — never a shortcut that
  mutates a `StatusInstance` directly.
- **Ask first:** giving `level` a real Core-side scaling formula — that would be inventing status
  balance logic this spec explicitly declined to guess at (no catalog field, no existing Core
  consumer to match).
- **Never:** let FA2 carry a magnitude/period override — that payload belongs to FA10 (A18c), by this
  opcode's own documented split.

## Success criteria

1. A real `status.apply` atom, bound and fired, applies a real `StatusInstance` with the correct
   duration, through the same resistance/immunity path every other status application already uses.
2. `level` is accepted, threaded through, and named inert — not silently dropped, not silently guessed.
3. Zero goldens moved.
