# Spec: `basic-attack-cost`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `resource-subtick`, `basic-attack-grant`, `lawn-action-bridge`, `lawn-hit-entry`

> `lawn-hit-entry` is a real dependency, not a courtesy: cost is charged **once per swing**, and the
> swing-id dedupe lives there. `lawn-action-bridge` supplies the `CostLedger` the injector currently
> cannot reach at all.

---

## Objective

Make the lawn basic attack cost a resource, per the existing design: **the basic attack consumes a
resource; no resource, no trigger** (D2, owner). The rule is already implemented at the right place —
`UsabilityEvaluator.cs:66` → `CostLedger.Check` → `UsabilityReason.CannotAfford`, with the real ledger
passed at `BasicAttack.cs:163-166`, so an unaffordable action is never declared.

It is **vacuous today** for four separate reasons, and all four must land together.

Success: a lawn actor pays `stamina` per swing, regenerates it over time, and contributes no elemental
delta while empty.

## Cost-delivery shape (decided — `lawn-combat-wire-todo.md` Task 2)

`spec-lawn-action-bridge.md` ("Cost is a different problem") names three candidate shapes for getting
an authored cost onto the lawn. **Chosen: construct `CompiledActionCost` locally in Core, reading the
already-shipped cost-template tuning file** — a corrected, smaller version of that spec's first
candidate.

| # | Candidate (as named in `spec-lawn-action-bridge.md`) | Verdict |
|---|---|---|
| 1 | Ship `authored-basics.json` beside the tuning files and read the cost locally | **Chosen, corrected** — see below |
| 2 | Deliver the cost through an existing cold-edge cache (the aptitude-cache shape) | Rejected — reintroduces the §2.16 trigger-set apparatus that spec's own adversarial rewrite tore out for the row itself; a cost row needs none of it |
| 3 | Accept an uncosted lawn basic attack for the first increment | Rejected as the todo's own default *only* because a clearly-better option exists; would contradict D2 from day one |

### Correction to candidate 1, found on inspection

`data/seed/actions/authored-basics.json` carries `act.attack`'s **identity** (name, category,
`kindHint`, `atomFamilies`, `rungBand`) but **no cost fields at all** — read directly, confirmed. The
cost is composed at import time from `data/tuning/action-corpus-cost-templates.v1.json`
(`categories.attack.{resourceId, baseAmountAtRung1, timing}`) by `ActionCorpusComposer.Compose`
(`ActionCorpusComposer.cs:165-168`):

```csharp
var costs = new[] { new ActionCostRow(brief.Id, costTemplateRow.ResourceId,
    ValueSpec.Of(costTemplateRow.BaseAmountAtRung1), costTemplateRow.Timing) };
```

So there is nothing to "ship beside the tuning files" — **the cost template is already there.** All
three injector host `.csproj`s already copy `data\tuning\**\*.json` verbatim
(`FusionRpg.Injector.MelonLoader.39.csproj:51-54` and its BepInEx/MelonLoader siblings), and
`action-corpus-cost-templates.v{n}.json` lives under `data/tuning/`, so it already reaches the plugin
folder with zero build changes.

### Why this beats candidates 2 and 3

- **No compilation problem exists for a cost row.** `spec-lawn-action-bridge.md`'s rewrite killed the
  first draft's cold-edge/DTO/trigger-set machinery because `CompiledAction.Condition` is an
  `ICompiledPredicate` — a behavioural object that cannot cross a wire. A cost has no such shape:
  `CompiledActionCost(string ResourceId, ValueSpec ScaledAmount, ActionCostTiming When)`
  (`CompiledAction.cs:8`) is three data fields, and `ValueSpec` (`ValueSpec.cs`) is itself
  Min/Max/enum data with no interned or behavioural members. Candidate 2's cache/trigger-set apparatus
  solves a problem a cost row does not have.
- **There is already a shipped precedent for exactly this shape.** `ConcreteSpeciesSeedReader` reads
  `data/generated/creatures/*.json` directly, Core-only, no SQL, via `RpgHost.Initialize` — "the same
  way `data/tuning/` already reaches this same folder" (`FusionRpg.Injector.MelonLoader.39.csproj:55-59`,
  its own comment). Reading `action-corpus-cost-templates.v{n}.json` the same way is the established
  pattern, not a new one.
- **It keeps D2 intact.** Candidate 3 (uncosted) is the todo's own named fallback *only if no
  clearly-better option turns up* — one did, so it is not taken. The basic attack costs a resource
  from the first increment, per D2 (`lawn-combat-wire-ideal.md:425`: *"Follow the existing resource
  design ... no resource, no trigger"*).
- **No new machinery, no new project reference.** `ActionCorpusComposer` already lives in
  `FusionRpg.Core.Actions.Corpus` (`ActionCorpusComposer.cs:30`), which the injector already
  references — `spec-lawn-action-bridge.md`'s own correction established "there is no reference to
  add." The cost-row construction quoted above is a pure function of `costTemplateRow`, reusable (or
  trivially mirrored) injector-side without a second implementation of the arithmetic.

### Consequence for this task (T12, `basic-attack-cost`)

Wire 1 ("A `stamina` cost row for `act.attack`") is built as: **construct `CompiledActionCost` locally
in Core**, at the same place `lawn-action-bridge`'s `BasicAttackCompiled` factory builds the rest of
the row, by reading `action-corpus-cost-templates.v{n}.json` (already on disk in the plugin folder —
no new copy rule needed) and applying the kind-aware `Basic → stamina` rule `basic-attack-seed`/D5
establish. **Not**:

- SQLite-delivered cost (`RpgStore.Actions.cs:453 ListCosts`) — the injector has no `FusionRpg.Data`
  reference and no Server round trip on the construction path, per `lawn-action-bridge`'s own
  boundary.
- A hardcoded C# fallback number — that reintroduces the exact defect this program exists to remove
  (a balance number in code, per `tunables-ssot.md`).
- Uncosted — this task charges a real `stamina` amount from the first increment; D2 is honoured, not
  deferred.

The remaining wires (pool-max seeding, regen as a kernel kind, the `CostLedger` seam/assembly) are
unaffected by this decision — it only answers **where the raw cost value comes from**, not how the
ledger is assembled around it.

## Tech stack

`FusionRpg.Core` (cost ledger, pools, regen) + `FusionRpg.Injector` (the lawn's cost gate, the regen
kernel kind). Unity-free Core half.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostLedger|ActorResourcePools"
dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"
```

## Five wires — [audit] the "four wires" framing was wrong

There is a **fifth**, and it is the one that turns the feature from inert into *actively worse than
today*: **an authored regen rate**. `resource-subtick` correctly refuses to author values
(*"leave `BaseResourceRegen` returning 0"*) and this spec's Boundaries said *"ask first"*. Both
refusing means the shipped state is **cost > 0, regen = 0** — every lawn actor swings once and is
permanently inert.

**Resolution: this module owns a documented placeholder**, per the repo's own posture (*"shipping a
guess is fine, calling it balance is not"*), marked `UNMEASURED` in tuning, with the real pass left to
the balance program. The same applies to the `stamina` cost itself and to the rider's
`sharePermille` — a placeholder is a decision; silence is a blocker.

**Also required: the feature kill switch** (see the map's program-wide constraints). Disabling it must
stop **binding the grant and charging the cost together** — with `OVERLAY-COMBAT` off but the grant
bound, actors pay stamina and the delta is dropped at `Finalize`: paying for nothing, worse than
undeployed.

## The wires — any one missing ships the feature silently inert

| # | Wire | Inert line today |
|---|---|---|
| 1 | **A `stamina` cost row for `act.attack`** | `BattleRunState.cs:81` `Costs: Array.Empty<CompiledActionCost>()`. Delivered as data by `basic-attack-seed`; this module consumes it — delivery shape decided above (Task 2) |
| 2 | **Seed `resource.max.*` on the lawn Hub** | `ActorHub.cs:147` `seedResourceBaseline = false`; sole `true` caller is `UniqueActorHubCompose.cs:75` (Server). The injector's Hub (`CheatState.cs:49`) never opts in, so **max is 0 for every lawn actor** |
| 3 | **Regen as a third kernel kind** | `KernelDriveHost.cs:69-70` has `KindDotPulse` and `KindShieldUpkeep`; regen is not among them |
| 4 | **The lawn's missing `CostLedger` call** | `grep CostLedger src/FusionRpg.Injector` → **zero hits**. The lawn has pools but no gate |

**Wire 2 is the dangerous one.** With max 0, an actor can never afford anything, so under D6 it
contributes no delta — which is *exactly indistinguishable from the bug this whole program exists to
fix*. Shipping 1, 3 and 4 without 2 would look like the feature simply doesn't work.

## Exhaustion suppresses the rider, not the shot

We do not own PvZ's sim loop and may not modify vanilla projectile behaviour
(`combat-damage-ssot.md:611`), so "no resource, no trigger" **cannot stop the pea from flying**. Out of
stamina ⇒ the shot lands for its vanilla/`attackDamage` number and **no elemental delta is added**; the
actor waits for regen and triggers again.

Same underlying rule as battle — *the RPG gates only the RPG's own contribution* — applied at a
different level of control over the clock. **Verified there is no coupling that would also drop the
stat bleed:** `progression.bonus.*` reaches Unity through a compose-time `EntityStatWriter` write,
independent of packet creation.

**Explicitly out of scope:** decaying an exhausted actor's stats. That belongs to a resource-exhaustion
feature with real statuses/debuffs (owner, 2026-09-13); doing it ad-hoc here would overlap and confuse
the status system.

## Cost cadence

- **One payment per swing**, not per victim — a piercing pea hitting five zombies is one attack
  (`lawn-hit-entry`'s swing-id dedupe).
- **Regen on the 100 ms kernel**, carry-corrected, with the sub-tick unit from `resource-subtick`. Not
  a frame counter: 60 frames is not a second unless the machine holds 60 fps, which would make regen
  hardware-dependent.
- Cost is authored **against regen**, never against max (`action-ideal.md:223`). With regen on a
  real-time grid this is a rate-vs-rate comparison and needs no round↔second fiction.

## Lawn pool lifecycle — state it, do not inherit it

`resource-hub-ssot.md:284` says pools *"persist across a run and refill **at rest**"*. **The lawn does
not do that**, and the spec must say so rather than silently contradicting the lock:

- `KernelDriveHost` runs only between `BeginBoard`/`EndBoard` (`MatchHost.cs:127,153,189`) and ticks
  with `* Time.timeScale` — so regen does not run paused, and does not exist between matches.
- Pools are created full per ptr and dropped on death (`LawnActorResourcePools.cs:38,50`).

**Declared rule: lawn pools are match-scoped and full at spawn.** If that is wrong, amend
`resource-hub-ssot.md` explicitly — do not leave the two in conflict.

## Code style

One authority. The lawn calls the **same** `CostLedger` battle does; per-context differences are
authored **data**, never a second gate — a parallel cost path would be a §2.15 SOLID failure.

`CostLedger.Check` inspects only `OnCommit`-timed rows; an `OnDeclare` cost would be invisible to the
declare-time gate. Author the basic attack's cost as `onCommit` unless that is deliberately changed.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | An actor with stamina pays once per swing; five victims of one bullet cost one payment |
| Core unit | An actor at zero stamina declares no action → no packet, no delta |
| Core unit | The same actor after regen triggers again |
| Core unit | `progression.bonus.*` still reaches Unity while exhausted — the stat bleed is untouched |
| Core unit | Pool max is non-zero for a lawn actor once wire 2 lands (the regression that catches a silent-inert ship) |
| Core unit | Regen accrues on the 100 ms grid and does not run while paused |
| Regression | Battle behaviour unchanged where no cost row is authored for a mode |
| Live | `lawn-combat-live-proof`'s falsifier |

## Boundaries

- **Always:** one `CostLedger`; per-context cost is data.
- **Always:** land all four wires together.
- **Ask first:** authoring the actual stamina/regen numbers — those are a balance act, and balance is
  a separate program (D1). Ship a documented placeholder, per this repo's own "shipping a guess is
  fine, calling it balance is not" posture.
- **Never:** a lawn-only cost gate; decay stats on exhaustion; charge per victim; suppress the vanilla
  shot.

## Success criteria

- [ ] A lawn actor's `resource.max.stamina` is **non-zero** — the anti-silent-inert check.
- [ ] One swing costs one payment regardless of victim count.
- [ ] At zero stamina: the pea still flies, no elemental delta is added, and the stat bleed is intact.
- [ ] Regen restores the actor and the next swing contributes again.
- [ ] Lawn pool lifecycle is stated in the spec and matches the code, or `resource-hub-ssot.md` is
      amended.
- [ ] No second cost gate exists; `guard-actor-hub` green.
- [ ] **Where `CostLedger.Check` is called on the lawn is specified**, and its position relative to the
      swing-id dedupe is stated — there is no lawn "declare" step for `UsabilityEvaluator.cs:66` to sit
      in, so the seam must be designed, not inherited.
- [ ] **Zombies get pools too.** `LawnActorResourcePools` is ptr-keyed with side unstated, while the
      grant binds to "plant and zombie" — confirm a zombie can hold and spend `stamina`, or state that
      zombies are exempt and why.
- [ ] Placeholder values for cost, regen and `sharePermille` are authored and marked `UNMEASURED`.
- [ ] The kill switch disables grant-binding and cost-charging together; with it off, behaviour is
      byte-identical to today.
