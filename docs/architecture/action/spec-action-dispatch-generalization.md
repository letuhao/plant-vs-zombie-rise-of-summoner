# Spec: action-dispatch-generalization (A18f)

Module **A18f** in the [action map](../action-map.md) §12.1a. Depends on A17, A18a, A18b, A18e (all
built and closed). **This is the module that makes a second, real action possible to ship at all** —
found by a 2026-09-06 completeness audit, not anticipated when A17/A18 were specced.

> **Read `action-map.md` §12.1a before this spec.** It records why this module exists and why it
> must land before A19, not after.

## Objective — the exact gap, traced to two lines, not a vague "dispatch is missing"

**Selection and activation are already general. Resolution is not.** Tracing
`TimelineDispatch.cs`/`BasicAttack.cs` directly (not from the map's own prose, which undersold how
close this already is):

1. `DeclareBasicAttack` (`BasicAttack.cs:105-143`) — despite its name — already calls the real
   `IIntentSource` (`source.TryDeclare`, line 118), gets back **whichever action the actor's real
   loadout selected**, and **already fires `OnActivate`** (lines 130-138) against that real
   selection. A18b's grant-triggered effects (A18c/d/e: resource grants, shields, statuses, stat
   modifiers) are therefore **already reachable for any real selected action today** — this was a
   genuinely bigger head start than the map's 2026-08-28 text credited.
2. `TimelineDispatch.cs:132` builds the committed intent from that same real envelope
   (`envelope.ActionId`), and `ActionRunner.TryCommit` stores it in the actor's own `ActionRun.Envelope`
   (`ActionRunner.cs:90`) — internal, correctly per-actor, correctly whichever action was picked.
3. **At resolve time, `TimelineDispatch.cs:211-213` throws all of that away**:
   ```csharp
   var step = ApplyBasicAttack(
       actor, target, state.BasicAttackEnvelopeCompiled, state,   // <- hardcoded, ignores step 2
       now, localClock.Now, state.Calculator, state.CritRng);
   ```
   `state.BasicAttackEnvelopeCompiled` is one fixed field. Whatever the actor actually committed —
   a different `CooldownChannel`, a different `skill.effectiveness.{category}` — is discarded, and
   every actor's damage/cooldown-arming math runs as if they'd used the basic attack, regardless of
   what `ActionRun.Envelope` actually holds.

**What "done" looks like:** an actor whose real loadout selects a non-basic-attack action has that
action's own envelope (cooldown category, effectiveness category, action id) actually drive the
resolve-time damage/cooldown math — not the basic attack's. `OnActivate`-triggered effects already
worked; this closes the one remaining seam so the *damage-application* half agrees with them.

**What this module does NOT do** (later modules' jobs, and one genuinely new gap this audit
surfaced — see "⛔ Real, load-bearing gap" below before assuming this module's fix is safe for any
action kind):
- Enforce resource costs or non-trivial cooldowns for the newly-reachable action — A19's job. This
  module only makes the *right* envelope resolve; A19 makes it *cost* something and *cool down* for
  real per-category.
- Add a second damage formula per action kind. `ApplyBasicAttack`'s own math
  (`calculator.Compute` off `attacker.LiveAtk`, scaled by `EffectivenessChannel`) stays the one
  formula for every "attack-shaped" action — that generalization already exists via the
  effectiveness channel (species-skills S3). Skill/defence/movement-shaped actions with genuinely
  different resolution math are out of scope here; this module only stops the wrong envelope from
  being substituted for the right one.
- Touch `state.BasicAttackEnvelopeCompiled` itself, or remove it — an actor with an empty loadout
  still needs it as the real fallback (A17's own contract).

## Design (locked on approval)

### 1. `ActionRunner` gains one accessor, mirroring `CurrentTarget` exactly

```csharp
/// <summary>The envelope this actor actually committed, for the caller applying the resolved hit
/// -- the runner itself never applies damage. Null under the same conditions as
/// <see cref="CurrentTarget"/>.</summary>
public ActionEnvelope? CurrentEnvelope(string actorKey) =>
    _runs.TryGetValue(actorKey, out var run) && run.Active ? run.Envelope : null;
```

`ActionRun.Envelope` (`ActionRunner.cs:90`) already stores exactly this, unconditionally, on every
commit — this is a read-only accessor over data that already exists, the same shape as
`CurrentTarget`, not new state.

### 2. `TimelineDispatch.cs:211` reads it back instead of hardcoding

```csharp
var committedEnvelope = runner.CurrentEnvelope(ev.OwnerKey) ?? state.BasicAttackEnvelopeCompiled;
var step = ApplyBasicAttack(
    actor, target, committedEnvelope, state,
    now, localClock.Now, state.Calculator, state.CritRng);
```

The `?? state.BasicAttackEnvelopeCompiled` fallback is not a hedge — it is the same "no intent /
mid-abandon" defensive shape `CurrentTarget`'s own null case already forces callers to handle
(§ line 178's doc comment: null "under the same conditions"), and it is exactly what already
happens today for an actor whose empty loadout resolved to the basic attack anyway — so the fallback
path is provably unchanged.

### 3. `RunBasicAttackStep` (the non-timeline, `W`-less atomic path) — checked, needs no change

**Verified directly, not left as an assumption.** `RunBasicAttackStep` (`BasicAttack.cs:88-97`) is a
two-line wrapper: `DeclareBasicAttack` → `ApplyBasicAttack(attacker, target!, envelope, ...)`,
passing the real `envelope` its own local variable already holds. It never reads
`state.BasicAttackEnvelopeCompiled` at all — the bug this module exists to fix is unique to
`TimelineDispatch`'s split-phase (commit now, resolve later) shape, where the envelope has to
survive a round-trip through `ActionRunner`'s own state rather than staying in one function's local
scope. **No change needed here.** (Per `decisions.md`'s 2026-09-05 row, `UsesTimelineDispatch=true`
now ships for all three profiles, so this path is likely close to unreachable in production anyway —
but it is correct regardless of that, not correct-by-luck.)

## ⛔ Real, load-bearing gap found by this audit — narrows this module's acceptance bar

**`ApplyBasicAttack` is attack-shaped throughout, not just in name, and nothing gates that today —
not on this path, not on the atomic path either.** Read past where the earlier draft of this spec
stopped:

- `calculator.Compute` always runs a hit/miss roll (`BasicAttack.cs:156-174`) and returns early with
  **zero cooldown arming** on a miss (`if (!breakdown.Hit) return ...`, line 174, before
  `state.Cooldowns.Start` at line 198).
- `CompiledAction` already carries a real `Category` field (`CompiledAction.cs:17-21`, A-M1) — but
  `ActionEnvelope` does not, and `ActionRunner.ActionRun` (`ActionRunner.cs:88-99`) stores only
  `Envelope`/`TargetKey`, never the category or the full `CompiledAction`.

**Consequence: a pure buff/heal/status Skill — one that should always arm its cooldown on activation
and never rolls an attack "hit" at all — would, under a naive application of this module's own §2
fix, sometimes skip arming its cooldown entirely (whenever `calculator.Compute`'s unrelated hit roll
happens to miss) and would run combat damage math it has no business running.** This is not a defect
this module introduces — the identical coupling already exists on the atomic path today, for
whatever `StubIntentSource` declares, for every actor, right now. It has been invisible only because
no real content has ever equipped anything but the basic attack (the same "no real second action
exists yet" fact that makes this module a zero-golden-mover, §"Golden-safety" above).

**This changes what "done" means for this module, precisely:**

> A18f generalizes envelope selection **for attack-shaped actions only** — anything that is meant to
> resolve through `calculator.Compute`'s hit-roll-then-damage shape, cooldown gated on landing.
> Generalizing to a genuinely different-shaped action kind (a Skill whose entire effect is its
> `OnActivate`-triggered atoms, with no attack roll and unconditional cooldown arming) needs a
> **per-category resolve branch that does not exist on either dispatch path today** — real,
> additional, separately-scoped work, not something this module's fix silently makes safe.

**Named as its own follow-up rather than left implicit**: a module (working name
`action-resolution-by-category`, not yet in the map's `A18`/`A19`/`A20` numbering) that (a) threads
`Category` from `CompiledAction` through `ActionRunner.ActionRun` alongside `Envelope` (mirroring
this module's own `CurrentEnvelope` addition exactly — same shape, one more field), and (b) branches
`TimelineDispatch`'s resolve step: attack-category → today's `ApplyBasicAttack` path unchanged;
non-attack category → arm cooldown unconditionally (no hit roll gates it), skip
`calculator.Compute` entirely, rely solely on the already-firing `OnActivate` atoms for effect. **Do
not equip a non-attack-category Skill on any real actor before this follow-up lands** — this module's
own acceptance criteria (below) are scoped to prove attack-shaped generalization only, and must not
be read as proof the system is safe for any other kind.

## Golden-safety — the claim, and how it is actually proven, not assumed

**Claim: zero-golden-mover**, for the identical reason A17 was one: no real, shipped content today
equips a second action (A17's own scope note — no grant-writer, no persisted loadout; every real
actor's loadout is empty or a directly-constructed test fixture). With an empty loadout,
`CurrentEnvelope` returns the actor's own committed basic-attack envelope, which **is**
`state.BasicAttackEnvelopeCompiled` today — same value, read from a different place. `Envelope`
equality is by-value (`ActionEnvelope.Equals`, `ActionEnvelope.cs:165-166` compares `SpeedChannel`/
`CooldownChannel` among other fields) — assert this directly in a test, not by inspection.

**Do not assert this from the design above. Run the full suite and diff the goldens, per this
repo's own repeated lesson** (`action-map.md` §12: *"this paragraph originally predicted the
switch-over 'will move the eight goldens' — verified false, not merely optimistic"*). A prediction
here is not a substitute for the same eight-golden comparison A17 and A18 each actually ran.

## Acceptance criteria

**Scoped to attack-shaped actions only** — see the gap named above. A criterion proving a
non-attack-category action behaves correctly is explicitly NOT part of this module's own bar; that
is `action-resolution-by-category`'s job.

1. `ActionRunner.CurrentEnvelope(actorKey)` returns the exact `ActionEnvelope` the actor's active
   run committed, `null` under the identical conditions `CurrentTarget` returns `null`.
2. `TimelineDispatch`'s resolve branch applies damage/cooldown-arming using the committed envelope,
   not the hardcoded field — proven with a synthetic loadout (mirroring A18a's own "tests construct
   one directly" pattern) whose sole action is **attack-category** and carries a **different**
   `CooldownChannel`/action id than the basic attack, asserting the resolve step used *that*
   channel, not the basic attack's.
3. An actor with an empty loadout is byte-identical to today — the fallback path, proven by hash,
   not by code inspection.
4. All eight existing battle goldens are byte-identical after this change, run for real.
5. **A planted-violation test proves the module does NOT silently mishandle a non-attack-category
   action** — construct a synthetic Skill-category loadout, run it through the fixed dispatch path,
   and assert the CURRENT (still attack-shaped) behavior fires — i.e. that this module makes no
   claim of correctness for it, and a follow-up reviewer sees a failing-if-assumed-fixed test rather
   than silence. This is a deliberate, documented limitation captured as a test, not merely as prose.
6. `RunBasicAttackStep` — verified in this spec's own §3, needs no code change; a regression test
   pins that it still reads its own local `envelope`, never the hardcoded field, so a future edit
   that reintroduces the coupling is caught.

## Testing strategy

- Unit: `CurrentEnvelope` returns the right value / `null` under each of `ActionRunner`'s own
  existing state-machine test fixtures (mirrors `CurrentTarget`'s own test coverage exactly — one
  new accessor, same test shape).
- Integration: a synthetic actor with a one-action loadout whose envelope differs from the basic
  attack's, run through `TimelineDispatch` end to end, asserting the resolve-time damage math read
  the actor's own channel — not `BasicAttackEnvelopeCompiled`'s.
- Regression: full `dotnet test tests/FusionRpg.Core.Tests`, all eight goldens diffed byte-for-byte,
  before and after.

## Boundaries

- **Never a new damage formula.** `ApplyBasicAttack`'s math is reused verbatim; only which envelope
  feeds it changes.
- **Never touches A19's territory.** No cost is spent, no new cooldown-arming behavior beyond
  reading the correct channel for the correct action — arming itself already generalizes once the
  right envelope is in hand (`BasicAttack.cs:198`'s own `envelope.CooldownChannel` read already
  works for any envelope passed to it; it was never attack-specific).
- **Never invents a second `IIntentSource`/selection mechanism.** A17's is reused untouched.
- **Never claims correctness for a non-attack-category action.** See "⛔ Real, load-bearing gap"
  above — that is `action-resolution-by-category`'s job, a named but unbuilt follow-up, not this
  module's silent responsibility.
