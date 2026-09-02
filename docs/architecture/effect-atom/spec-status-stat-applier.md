# Spec: status-stat-applier (E21)

**Status: BUILT 2026-08-23, retrospective spec written 2026-09-03.** Module **E21** in the
[effect-atom map](../effect-atom-map.md) §3, Wave 6, Checkpoint F. This document records what shipped;
it is not a plan. Acceptance evidence: [tasks/effect-atom-todo.md](../../../tasks/effect-atom-todo.md)
(search `E21: status-stat-applier`). Scoped from [completeness-audit.md](completeness-audit.md)
finding A1.

> Reads [definitions.md](definitions.md), which wins where it and this document disagree.

## What it owns

The two calls that turn a live status instance's declared stat block into a real composed stat change,
and the owner-key grammar those calls depend on. On apply, the injector's status runtime upserts
`StatusStatPayload.ToModifiers(inst)` into the session modifier bag and forces a recompute; on end it
withdraws by `StatusStatPayload.SourceIdOf(inst)` and forces the recompute again. That is the whole
module — **no new Core type, no new plugin.**

## What it closed

E17 wired the status payload's *parser*: a status could declare `stat` mods and they were validated,
ordered and turned into `StatModifier`s. Nothing called that producer. `StatusStatPayload.ToModifiers`
and `SourceIdOf` had zero production callers, so `rally`, `expose`, `command` and `shatter` created live
instances, played their VFX, ticked down and **changed no stat**.

It also closed a real bug that inspection had missed and only a seam test could find. `ToModifiers` set
`ApplyOwnerKey = instance.HostPtr` — a bare pointer. `StatApplyScope.Matches` recognises only the
`entity:`-prefixed grammar and falls through to `return false` for anything else, so the contribution
composed nothing even once a caller existed. Every other owner key in the codebase already arrived
pre-formatted; this was the one place building one raw. The unit test's own assertion
(`Assert.Equal("Z1", mod.ApplyOwnerKey)`) had encoded the bug.

## The contract as shipped

**The Core half** — `src/FusionRpg.Core/Status/StatusStatPayload.cs:148-180`:

- `ToModifiers(StatusInstance)` returns one `StatModifier` per declared mod, with
  `SourceKind = "status"`, `SourceId = "status:" + instance.InstanceId`, `PluginId = instance.PluginId ?? "status"`,
  the op mapped from `"increased"`/`"more"` with everything else falling to `Flat`, and
  `ApplyOwnerKey = "entity:" + instance.HostPtr` (`:173` — the fix, with the reason in the comment
  above it).
- `SourceIdOf(StatusInstance)` returns the same `"status:" + InstanceId` string, so the withdraw
  matches the upsert by construction (`:180`).
- **The source id is the instance id, not the status id.** Two stacks of one status are two
  contributions and one expiring must not withdraw the other's.
- Empty `StatMods` returns `Array.Empty<StatModifier>()` (`:150`).

**The injector half** — `src/FusionRpg.Injector/Effects/EffectRuntime.cs:69-99`, inside `Ensure()`:

- `_status.OnApplied` (`:69-86`): plays the VFX cue, then — gated on `inst.StatMods.Count > 0` —
  calls `CheatState.Stats.Upsert(StatusStatPayload.ToModifiers(inst))` followed by
  `CheatActions.ReapplyLivingForOwner("entity:" + inst.HostPtr)`.
- `_status.OnEnded` (`:87-99`): the expire cue, then `CheatState.Stats.WithdrawSource("status",
  StatusStatPayload.SourceIdOf(inst))` and the same reapply.
- **The reapply is load-bearing.** `Upsert`/`WithdrawSource` touch the session bag only; without
  `ReapplyLivingForOwner` nothing re-composes and nothing is written to the entity.
- **The `StatMods.Count > 0` gate is deliberate**, so the majority of statuses (pure CC and VFX) do not
  pay for a needless recompute on every apply.
- Both halves are wrapped in `try`/`catch` reporting through `CheatState.Error` — a throwing stat apply
  cannot take down the status runtime.

This is the same session-bag pattern `ExecModifyStat` already used for effect-granted mods
(`Upsert` on apply, `WithdrawSource("effect", "effect:" + grantId)` on remove). E21 mirrored it rather
than inventing a second mechanism.

## What it does NOT do

- **It does not touch the battle runtime.** `StatusRuntime.OnApplied` (`src/FusionRpg.Core/Status/StatusRuntime.cs:118`)
  has exactly two subscribers in `src/`, both in the injector (`EffectRuntime.cs:69` and
  `Hud/ActorHudInvalidator.cs:25`, which chains the previous handler). Battle's status path subscribes
  nothing, so a `rally`-shaped status still changes no stat there.
- **It adds no plugin.** `StatSystem.Resolve`'s per-call `IStatModifierPlugin.Contribute` pass is
  untouched; this module uses the persistent session bag only.
- **It does not change what a status may declare.** The payload schema, its channel check and its
  ordering are E17's.
- **It does not write a Unity field directly.** Everything goes through the bag and the existing
  reapply path, so the single-writer boundary is unchanged.

## How it is verified today

- **Seam** — `tests/FusionRpg.Core.Tests/Status/StatusStatApplierSeamTests.cs`, 4 tests through the real
  `StatSystem` → `Upsert` → `Resolve` → `WithdrawSource` → `Resolve` chain, no fakes: a live rally
  instance raises the composed channel; withdrawing returns it to baseline; two stacks are two
  withdrawable contributions; a status on a different host does not leak into this one's resolve. This
  suite went RED on its first run and is what caught the `ApplyOwnerKey` bug.
- **Unit** — `tests/FusionRpg.Core.Tests/Status/StatusStatPayloadTests.cs` (15 test methods,
  pre-existing from E17, one assertion corrected to `"entity:Z1"`).
- **Guard** — `tests/FusionRpg.Guard.Tests/StatusStatApplierGuardTests.cs`, 4 tests.

**Coverage of the injector half is thin, and by construction.** `EffectRuntime` cannot be instantiated
outside the game process, so the four guard tests read `EffectRuntime.cs` as **text** and assert the
call names, the `entity:`-prefixed reapply on both halves, and the `StatMods.Count > 0` gate
(`StatusStatApplierGuardTests.cs:27-67`). They prove the wiring is written, not that it executes. The
executing half of this module has never been proven by a test — only by the live-lawn work that came
after it.

## Known residuals

- **Battle has no status stat applier.** The mechanism is Core-pure and would work there; nothing
  subscribes. A status declaring a `stat` block is silently inert in the battle runtime today, which is
  the same shape of gap A1 described for the lawn.
- **The injector half is text-guarded only** (see above).
- **The try/catch swallows to `CheatState.Error`.** A malformed payload degrades to a log line rather
  than a refusal; the refusal lives upstream at import and at `StatusEffectBridge` (C2's
  `status-stat-overlay-without-ModifyStat` check), not here.
