# Spec: `aura-action-shape`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Status:** specced 2026-08-30, not built. Foundation module, independent of the others.

---

## 1. Objective

The **mechanics** of an aura as an action: enable, disable, the active set, eviction, and upkeep.
No magnitudes, no channels, no content — those are `aura-magnitude` and `aura-content`.

**Owner decision (Q8, 2026-08-30), verbatim:** *"aura is cost 1 slot in 5 action slot, cost resource to
enable, only 1 aura can enable at one time, new aura enable will off other aura, add it tunable so we
can extend 2 or more aura enable at same time, if exceed limit, the eldest active aura will be off."*

| Property | Rule |
|---|---|
| Kind | `ActionKind.Skill` — action kinds **close at three** (`action-ideal.md:63`, decision 25) |
| Equipped | 1 of `LoadoutSet.MaxSize` = **5** (`LoadoutSet.cs:40`) |
| Active at once | **1** by default; `maxActiveAuras` **tunable** |
| Overflow | **oldest active aura switches off** (FIFO) |
| Cost | resource to enable, `perTick` upkeep while held |
| Exclusivity | **concurrent** — an aura must NOT block other actions |

**Equipped and active are independent scarcities.** A commander may carry five auras and run one.

---

## 2. The model to follow, and the one thing to change

`StanceRuntime` (`Actions/Defence/StanceRuntime.cs`) is the shipped precedent for a toggled, continuous,
resource-draining action, and this module copies its shape closely:

- *"No new FSM state, no runtime of its own"* (`:19-25`). "Held" is a **plain per-actor dictionary
  entry**; the visible effect is an ordinary self-status with **`BaseDuration: 0`** → never expires on
  its own, cleared explicitly on release.
- Registers its status into `StatusCatalog` **additively** in the constructor (`:34-47`), leaving the
  21 locked ids untouched.
- `GrantIdFor(actorKey) => "stance:" + actorKey` — a deterministic, per-actor grant id.

**The one deliberate divergence: exclusivity.** `StanceRuntime.Check` refuses *every* other action while
held (`:97-103`, `UsabilityReason.StanceHeld`). That is right for Guard and wrong for an aura — a
commander who can do nothing else while their aura runs is not a commander. **`AuraRuntime` must not
implement `IStanceCheck` and must not participate in gate 0.**

It must, however, inherit the slot discipline: *"at `W = 1` an indefinite hold freezes the entire
board… **Guard consumes a slot to RAISE, then releases it. The status persists, not the slot**"*
(`spec-defence-actions.md:85-93`). **An aura holds loadout capacity, never the kernel's concurrency
width `W`** — `spec-action-model.md:53-55` warns that "slot" means both things in this repo.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Aura
dotnet test tests\FusionRpg.Core.Tests
python scripts\audit-magic-numbers.py --targets M1
```

---

## 4. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Actions/Aura/AuraRuntime.cs` | **new** — the active set, enable/disable, eviction |
| `src/FusionRpg.Core/Actions/Aura/AuraActiveSet.cs` | **new** — ordered active auras, FIFO eviction |
| `src/FusionRpg.Core/Actions/Aura/AuraEvictionOutcome.cs` | **new** — the typed result |
| `src/FusionRpg.Core/Actions/UsabilityResult.cs` | edit — add `NotEquipped` / `AlreadyActive` to `UsabilityReason` (a closed enum with neither today). ⚠️ **Not `ActionRejection.cs`** — an earlier draft named it, but `ActionRejectionReason` is *load-time authoring* validation (`UnknownContainer`, `StructureExceedsBudget`), a different closed list |
| `data/tuning/aura.v1.json` | **new** — `maxActiveAuras` and nothing else in this module |
| `tests/FusionRpg.Core.Tests/Actions/Aura/…` | **new** |

---

## 5. Design

### 5.1 The active set

```csharp
public sealed record AuraEnableResult(
    bool Enabled,
    string? EvictedAuraId,        // non-null when FIFO eviction fired
    UsabilityReason? Refusal,     // non-null when it did not enable at all
    string? RefusalDetail);
```

**Enable is one of three outcomes, never a silent no-op:**

1. **Enabled** — under the cap.
2. **Enabled, with eviction** — at the cap; the **oldest** active aura is disabled and named in
   `EvictedAuraId`.
3. **Refused** — a typed `UsabilityReason` (cannot afford, not equipped, already active).

Order is **insertion order of activation**, not loadout order — "oldest active", not "first equipped".
Re-enabling an already-active aura is a **no-op that reports it**, never a refresh that resets its age.

### 5.2 Eviction must be visible

⚠️ **The single most important rule in this module.** GG-55 requires *never disable without saying
why*, and the action layer already refuses with typed reasons (`UsabilityReason.CannotAfford(resourceId)`,
`OnCooldown`, `NotBound`). **"Enabling Might switched off Fortitude" is the same class of information**
and must reach the player through the same channel — not as a silent state change.

This is also a deliberate divergence from the one shipped precedent: **Guild Wars 1 auto-drops the
*most recently* maintained enchantment** on energy exhaustion; the owner chose to drop the **oldest**.
GW1's rule protects an established setup from an over-commit; ours preserves the player's latest
intent, which reads better for a deliberate toggle than for an accidental over-reservation. Recorded so
a later reader does not "fix" it back.

⚠️ **The decision was made on the case where it has no effect, and there is a genuinely bad case at
`N > 1`.** At `maxActiveAuras = 1` oldest-first and newest-first are **identical** — every enable
evicts the only other aura either way. The policies only diverge at `N ≥ 2`, and there oldest-first
means **the aura you have run longest — necessarily your foundation — is the first casualty of every
subsequent toggle**, with no pin, no priority, and no way to express "keep this one." Combined with
§5.1's rule that re-enabling does not reset age, a foundation aura can never protect itself.

Two mitigations, either acceptable, **owner call before `maxActiveAuras` is ever raised above 1**:

- **A pin flag** — one active aura may be marked un-evictable.
- **Refuse instead of auto-evict** — at the cap, enabling refuses with a typed reason naming what would
  have to be turned off, and the player disables it explicitly. This is arguably the more honest reading
  of GG-55: *"never disable without saying why"* is better served by **asking** than by **announcing**,
  and the action layer already refuses with typed reasons rather than acting unilaterally.

Not blocking at `N = 1`, which is what ships. Recorded so it is decided before the tunable is raised
rather than discovered afterwards.

### 5.3 Upkeep

`ActionCostTiming.PerTick` already exists (`ActionEnums.cs:67`) and `CostLedger.TryPay` already pays per
tick with validate-all-then-consume-all semantics (`CostLedger.cs:106-138`). **This module authors no
new cost mechanism** — it calls the existing one.

- **Failing to pay disables the aura** through the existing interrupt path — *"a `perTick` cost that
  cannot be paid ends the action"* (`spec-action-costs.md:70-79`). Same typed, visible outcome as
  eviction.
- **Legal pools are `stamina`, `qi`, `poise` only** — `hp`, `hunger`, `spirit` are never action costs
  (`concrete-action-roster.md:407`). An aura is a skill, so **`qi`** is the default.
- ⚠️ **A `perTick` row spends the `consumption` structure axis**, and an action whose rung does not
  budget it is **rejected at load** with `StructureExceedsBudget` (`StructureBudgetGuard.cs:67`). Aura
  rungs must budget `consumption` or they will not load.

### 5.4 Cost-free is not an option

*"Two actors both guarding forever deal and take nothing — `netAttrition ≤ 0` on both sides, which is
the **termination invariant**, and `decisions.md` makes it **blocking**"* (`spec-action-costs.md` §4.1).
The owner's *"nothing free"* is required by an existing blocking invariant, not a preference.

### 5.5 Lifetime

- **Disabled on match end**, like every other grant.
- **Loadouts freeze at run start** (`LoadoutRejectionReason.MidRun`, `LoadoutSet.cs:7-9`) — so *which*
  auras are equipped cannot change mid-run, but **toggling an equipped aura is not a loadout change**
  and is explicitly allowed. This distinction must be tested, because it is easy to over-apply the
  freeze.

---

## 6. Code style

Match `StanceRuntime`: a plain dictionary, no new FSM state, injected seams rather than statics, XML doc
naming the spec section. `maxActiveAuras` is a **tunable** in `data/tuning/aura.v1.json`, never a
`const` — it is exactly the number a balance pass would change.

---

## 7. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | Enable one aura under the cap | active; nothing evicted |
| 2 | Enable a second at cap 1 | first is **evicted and named**; second active |
| 3 | Cap raised to 2 | two coexist; a third evicts the **oldest**, not the newest |
| 4 | Re-enable an active aura | no-op, reported; **its age does not reset** |
| 5 | Enable while unaffordable | typed refusal naming the pool; **nothing evicted** |
| 6 | `perTick` payment fails mid-run | aura disables through the interrupt path, typed and visible |
| 7 | Aura active + another action attempted | **allowed** — the anti-`StanceHeld` regression test |
| 8 | Toggle mid-run | allowed; equipping mid-run still refuses `MidRun` |
| 9 | Match end | all auras disabled, no leaked grants |
| 10 | `maxActiveAuras` from tuning | changing the config changes behaviour with no code edit |
| 11 | Rung without `consumption` budget | **rejected at load** |

**Test 7 is the regression guard** for the deliberate divergence from `StanceRuntime` — the most likely
mistake here is copying the exclusivity along with the shape.

---

## 8. Boundaries

**Always**
- Report eviction as a typed, visible outcome.
- Use the existing `CostLedger` for upkeep.
- Keep `maxActiveAuras` in tuning.

**Ask first**
- Any aura upkeep pool other than `qi`.
- Changing the eviction policy from oldest-first.

**Never**
- Refuse other actions while an aura is held (that is Guard, not this).
- Hold the kernel's `W` slot.
- Ship a cost-free aura.
- Add a fourth `ActionKind`.

---

## 9. Success criteria

- [ ] One aura active by default; `maxActiveAuras` raises it with a config change only.
- [ ] Overflow evicts the **oldest** and names it in the result.
- [ ] Every non-enable outcome is a typed reason, never a silent no-op.
- [ ] An active aura never blocks another action.
- [ ] Unpayable upkeep disables the aura through the interrupt path.
- [ ] No magic numbers on the balance surface; `audit-magic-numbers.py --targets M1` clean.

## 10. Open questions

1. **Is the enable cost separate from the per-tick upkeep, or is the first tick the cost?** The owner
   said *"cost resource to enable"* **and** *"cost resource when enable"* — `onCommit` + `perTick` is
   the literal reading and both already exist. Leaning: both, with the `onCommit` half authorable as
   zero.
2. **Does eviction refund anything?** No — *"committing is what costs, not landing"* (`CostLedger.cs:28-30`)
   is the shipped rule and this module should not carve an exception. Recorded rather than assumed.
