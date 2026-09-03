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

**⛔ EVERY RESIDUAL BELOW CARRIES A DISPOSITION — DECIDED 2026-09-03 (owner removed themselves as a
gate):** *claimed* by a named module, a *named follow-up*, or *accepted as-is with the reason*.

- **[FOLLOW-UP — `E49 battle-status-stat`] Battle has no status stat applier.** The mechanism is
  Core-pure and would work there; nothing subscribes. A status declaring a `stat` block is silently
  inert in the battle runtime today, which is the same shape of gap A1 described for the lawn.
  ⛔ **DECIDED 2026-09-03.** *Scope, one line:* subscribe battle's status runtime to `OnApplied` /
  `OnEnded` with the same `StatusStatPayload.ToModifiers` / `SourceIdOf` pair E21 wired on the lawn,
  through battle's own modifier bag and recompute.
  **Why a follow-up and not accepted:** re-verified 2026-09-03 — `StatusRuntime.OnApplied`
  (`StatusRuntime.cs:118`) has exactly **two** subscribers in `src/`, both in the injector
  (`EffectRuntime.cs:69` and `Hud/ActorHudInvalidator.cs:25`), so `rally`, `expose`, `command` and
  `shatter` are inert in battle in exactly the way E21 existed to stop them being inert on the lawn.
  The mechanism is already proven Core-side by `StatusStatApplierSeamTests` through the real
  `StatSystem` chain, so what is missing is **two subscriptions**, not a design.
  **What would overturn it:** battle deciding statuses should not touch composed stats at all — a
  product decision, and one that would have to be written down rather than left as an absent
  subscription.

- **[CONVERTED TO A CRITERIA-STATED TASK — `T-live-E21`] The injector half is text-guarded only** (see
  above), so **the executing half of this module has never been proven by a test.**
  ⛔ **DECIDED 2026-09-03.** This is not decidable from the repo: `EffectRuntime` cannot be
  instantiated outside the game process, which is why the four guard tests read it as **text**
  (`StatusStatApplierGuardTests.cs:27-67`). No offline test can close it, so it converts to a task
  needing physical access, and **it blocks nothing** — not this module, not E49, not any Wave 7
  module.
  **What to check:** on a live lawn (`live-lawn-quick-start`, MelonLoader default game), apply to a
  targeted actor a status carrying a non-empty `stat` block — `rally` is the shipped example.
  **What a pass looks like:** the composed channel on that actor rises while the instance is live and
  returns to baseline when it expires, observed through the actor sheet or HUD; the reapply fires for
  the `entity:`-prefixed owner key; and `CheatState.Error` carries nothing for that window.
  **What a fail looks like:** no change on apply (the reapply or the owner-key grammar is wrong), or an
  error line (the swallow below caught something). Either outcome is recorded here, not re-litigated
  offline.

- **[ACCEPTED AS-IS] The try/catch swallows to `CheatState.Error`.** A malformed payload degrades to a
  log line rather than a refusal; the refusal lives upstream at import and at `StatusEffectBridge` (C2's
  `status-stat-overlay-without-ModifyStat` check), not here.
  ⛔ **DECIDED 2026-09-03 — this does not need fixing, and the reason is the shape of the boundary.**
  A stat apply runs inside the status runtime during a frame-budgeted match; letting it throw would
  take the **status runtime** down for every status on the board because one status declared a bad
  block, which converts a content defect into a match-ending one. A swallow at the outermost
  per-status boundary is the correct shape, and it is not silent — it reports through `CheatState.Error`.
  The refusal that actually prevents the bad content is upstream, where it can reject rather than
  degrade, and that is where it lives.
  **What would overturn it:** `CheatState.Error` ceasing to surface anywhere a human reads. The swallow
  is acceptable **because** it reports; if the report goes away, the disposition flips and the fix is
  the report, not the throw.
