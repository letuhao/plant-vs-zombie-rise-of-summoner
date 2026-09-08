# Spec: action-resolution-by-category (A22)

Module **A22** in the [action map](../action-map.md) §14. Depends on A18f
(`action-dispatch-generalization`, built). **Named, not discovered here** — A18f's own spec
(`spec-action-dispatch-generalization.md`, "⛔ Real, load-bearing gap") found this exact defect while
narrowing A18f's own acceptance bar to attack-shaped actions only, and `action-plan.md` §5 has carried
it as an unscheduled deferred item since 2026-09-06. This spec schedules it.

## Objective — the exact gap, traced to two lines that both ignore `Category`

`ApplyBasicAttack` (`BasicAttack.cs:171-219`) resolves **every** action identically, regardless of
what kind of action it is:

```csharp
// BasicAttack.cs:175-193
var (signedDelta, breakdown) = calculator.Compute(new OverlayCombatRequest { ... }, critRng);
if (!breakdown.Hit) return new AttackStep(AttackStepOutcome.Continue, null, 0);
```

Every committed action rolls an accuracy/crit-shaped hit check — correct for `Attack`, meaningless
for a pure buff, heal, or status application that was never supposed to "miss."

```csharp
// BasicAttack.cs:212-218
state.Cooldowns.Start(attacker.Setup.Key, envelope, nowTick,
    SkillCooldownReductionPm(attacker, envelope));
```

This line is **unreachable on a miss** — it sits after the `if (!breakdown.Hit) return` above, inside
the same method, so a non-Attack action's cooldown depends on a hit roll it should never have taken
in the first place. **Both defects share one root cause**: `ApplyBasicAttack` takes an
`ActionEnvelope`, which carries no `Category` at all — the method has no way to know it is resolving
anything but an attack.

**Why this is dormant, not broken, today**: no real action has ever reached this method except the
basic attack (`Category` implicitly `Attack`) and this program's own synthetic test fixtures. T55.4's
own planted-violation test (`T55_4_a_non_attack_category_action_still_deals_attack_shaped_damage_today`,
`ActionDispatchGeneralizationTests.cs`) proves this is a **known, named, tested** limitation, not a
silent one — it asserts today's actual (wrong) behavior so a future change is forced to touch this
test rather than shipping an unaudited fix. **Why it stops being dormant**: A21 imports a real corpus
where 10 of 19 accepted rows in `committed-round-1.json` are `support`/`defense`/`movement`/`status`
— the first real import makes this a live, player-visible bug.

## Design (locked on approval)

### 1. Threading `Category` to `ApplyBasicAttack`

`Category` lives on `CompiledAction` (`ActionRow.cs` §"corpus metadata", threaded through
`ActionCompiler`), not on `ActionEnvelope` — confirmed by reading both types directly.
`ApplyBasicAttack`'s two callers (`RunBasicAttackStep`, the atomic path; `TimelineDispatch.cs`'s
resolve branch) each already have `envelope.ActionId` in scope. **Chosen shape**: store the
`ActionCatalog?` `BattleRunState` already receives as a constructor parameter (`BattleRunState.cs:232`)
as a field — it is captured in a closure today (`rungOf`'s lambda, `:493`) but never kept for
later access — then resolve `state.ActionCatalog?.Get(envelope.ActionId)?.Category` inside
`ApplyBasicAttack` itself. **Rejected alternative**: widening `ActionIntent`
(`Timeline/IntentSource.cs:12`) to carry `Category`, threaded from `StubIntentSource.TryDeclare`'s
own already-available `CompiledAction`. Rejected because it touches a public record used across more
call sites for a value `ApplyBasicAttack` can resolve locally with a one-field addition instead — but
named here as the fallback if storing the catalog reference proves awkward at build time (e.g. a
lifetime/capture conflict with the existing `CostLedger` closure, unconfirmed either way until built).

### 2. The branch, stated as a truth table

| `Category` | Hit roll | Cooldown arms |
|---|---|---|
| `Attack` (or `null` — every action authored before A-E1 shipped) | Yes, unchanged | On landed hit only, unchanged |
| `Defense` · `Support` · `Movement` · `Status` | **Skipped** — the action's `OnActivate` atoms (A18a/b, already firing today regardless of category) are the only effect | **Unconditional** — arms the moment the action resolves, exactly once, never gated on an outcome that was never rolled |

`null` mapping to `Attack` is deliberate, not a default-by-omission: every action authored before
A-E1 (`spec-eligibility-axis.md`) shipped has no `Category` at all, and every one of them is the
basic attack or a test fixture built to be attack-shaped — treating `null` as anything else would
silently change the resolution of existing, already-golden-blessed content.

### 3. What does NOT change

`calculator.Compute`'s own call for an `Attack`-category action is byte-identical — this module adds
a branch **before** it, never edits the resolver. The `OnDamageDealt` trigger
(`BasicAttack.cs:195-210`, A18c's on-hit riders) stays gated on a landed hit for `Attack` actions and
never fires at all for a non-Attack one (there is no "hit" to trigger it) — `OnActivate` already
covers the non-Attack case (A18b, firing at declare time regardless of category, unchanged by this
module).

## Golden-safety

Every shipped golden and every existing test fixture equips only `Attack`-category (or uncategorized,
i.e. `null` → `Attack`) actions. **Zero-golden-mover by the same argument A18f/A19/A20 each already
proved**: the new branch is unreachable for any content that exists today, provable by running the
full suite, not assumed from "it's additive."

## Acceptance criteria

1. A real `Support`/`Defense`/`Movement`/`Status`-category action commits and resolves **without**
   ever calling `calculator.Compute` — proven by a `BattleTrace` showing a target declared but no
   corresponding `Applies` entry from that action's own hit (mirroring T56.3's own "declared but never
   landed" trace-based proof technique), not by asserting the code path was skipped from reading it.
2. The same action's cooldown arms on resolve regardless of any hit outcome — proven by a second
   commit attempt being refused by the cooldown gate immediately, the same technique T56.4 already
   uses.
3. `null`/`Attack`-category content is byte-identical to today — the existing T55.4 planted-violation
   test **now asserts the corrected behavior** (this module is what makes that test's own "still
   attack-shaped today" framing obsolete; it is rewritten to assert the fix, not deleted, so the
   history of what changed and why stays in the test file).
4. Full A17-A23 regression sweep plus a full `Core.Tests` run, both green.

## Testing strategy

- Unit/Integration: extend `ActionDispatchGeneralizationTests.cs` (the file T55.4 already lives in)
  with the Support/Defense/Movement/Status cases from the acceptance criteria — same
  `BattleGoldenTests.CloseSetup()`-based fixture pattern every A18f/A19/A20 test already uses, for the
  same reason (a fixture proven to neither stalemate nor resolve in one swing).
- Regression: the standing A17-A23 sweep, plus a full `Core.Tests` run to catch anything outside the
  targeted set.

## Boundaries

- **Never** change `Attack`-category resolution. This module adds a branch, never edits the resolver
  `Attack` already uses.
- **Never** invent a sixth resolution shape. The table in §2 is exhaustive over the closed
  5-member `ActionCategory` vocabulary (`action-ideal.md`'s sealed decisions) plus `null`.
- **Ask first** before treating `null` as anything other than `Attack`. That reading protects every
  pre-A-E1 action; changing it is a content-migration decision, not a code default.
