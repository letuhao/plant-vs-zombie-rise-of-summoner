# Spec: action-selection (A7)

> **Reconciled 2026-08-27.** Checked against the sealed [action-ideal.md](../action-ideal.md) and
> found **substantively intact**; only the notes below change.
> — The stub AI must now respect **gate 0** (`A4` §1a): an actor mid-stance refuses every candidate, and the
> check is **hoisted out of both loops** because it is per-actor.
> — Preference key stays the stub's own — **not `priority_band`**, which is a scheduling concept.


Module **A7** in the [action map](../action-map.md). Depends on **A2**, **A4**, **A6**.

> **This is the game's first AI layer.** The effect-atom ideal disclaims AI entirely — *"targeting, retreat, and decision-making need an AI layer spec, and this game has no AI layer yet."* That hole is this module. The owner scoped it deliberately small: **build a stub first.**

## Objective

Decide what an actor does on its turn: **pursue the nearest target, and use an action to kill it.**

It implements `IIntentSource`, which is the same seam a player's input will use — so the interactive modes plug into the socket the stub is proving.

## Design (locked on approval)

### 1. Deliberately stupid, and honest about it

The stub does not evaluate threat, does not retreat, does not kite, does not plan, and does not read the atom program's power vector. It picks a target and something to hit it with.

Naming this is the point. A stub presented as an AI invites patching until it becomes an accidental architecture; a stub named as a stub gets replaced by a real one with its own map and its own spec.

### 2. The decision, in order

```
1. can I act at all?          → A4 gate 1, hoisted: the actions this actor holds
2. who?                       → nearest live enemy, ties broken deterministically
3. with what?                 → first usable action, in priority order, against that target
4. can I reach?               → if not, and I hold a movement action, move toward them
5. nothing works?             → pass
```

Step 5 is not a fallback, it is a **requirement**. An intent source with no defined "no legal action" answer is a deadlock: under next-event advance an actor holding a slot with nothing scheduled drains the queue and stops the clock. The kernel already returns `SeatOutcome.Passed` for exactly this; the stub must reach it rather than hang.

### 3. Ordering is deterministic or replay breaks

- **Nearest** ties break on **ordinal ptr** — the same tiebreak `TargetResolver` and `ActionSlots.SortContenders` already use. Three places, one rule.
- **Action choice** is the stub's **own preference key** — tag preference (`offensive` before `utility`), then `action_id` ordinal. Never catalog or dictionary order, which is precisely the leak this codebase has already been bitten by once.

  **Not `priority_band`** (audit I2). On the envelope that field is a **scheduling** override, part of the event queue's sort key and documented as impossible to retrofit for that reason. Using it as an AI preference gives one column two meanings, and they disagree the first time an always-first effect is not the AI's first choice.
- The stub draws **no random numbers**. If it ever needs one it takes a named seeded stream, never an ambient RNG.

### 4. The AI reads the board through a view, not the raw state

> **`IBattleView` from day one, even while it returns everything.**

Fog of war is deferred, not refused. Three of its four consequences cost nothing to postpone; the fourth is expensive to retrofit and free to prepare. Under fog, "nearest target" becomes "nearest *known* target" — and that is a change to **every read the AI makes**.

With the seam, fog is later an implementation swap behind one interface. Without it, it is an AI rewrite. The interface costs one indirection now.

### 5. Cost discipline — this is the hot loop

The stub evaluates usability across actions × targets, every turn, for every actor. `A4`'s gate order exists for this module:

| Gate | Hoist to |
|---|---|
| bound | per actor — outside both loops |
| cooldown, affordability | per action — outside the target loop |
| range, condition | per target — the only ones that belong inside |

Evaluating all five per pair does the same work an order of magnitude more often. **Zero allocation per decision**, asserted — `FactReader` is a struct so that this is achievable, and a stub that allocates per candidate would be the first thing to show up in a stress probe.

**The budget is the sweep, not a frame** (audit R2-4). Actions run server-side, so there is no frame deadline — which makes it easy to conclude there is no ceiling. There is one: the **win-rate sweep** resolves thousands of battles back to back with no player, and this module runs actions × targets on every turn of every one of them.

> A sweep that takes minutes is a tool. One that takes hours is abandoned — and the balance discipline it supports goes with it.

So the perf contract here is stated in **battles per second**, not milliseconds, and gate hoisting is the lever. The `FactReader.Reads` test below is the instrument; it needs a target number, set from a sweep measurement rather than guessed.

### 6. Golden impact — and why it is zero *today*

Replacing `SelectTarget` with "nearest by distance" changes who gets hit, which moves goldens. But:

> **There is no board yet, so there is no distance.** With coordinates absent, "nearest" is undefined and the stub falls back to `SourceOrder` — which is exactly what `SelectTarget` does today.

So `A7` is **golden-neutral until `A10` lands**, and its targeting change joins the movers bucket (T9 + E12 + grid + fog, one combined re-bless) rather than threatening the freeze. This is a property to **assert**, not to assume — the moment a board exists, this module starts moving hashes.

Two engine behaviours stay where they are until this layer is real: `bloodthirsty` (lowest-HP targeting) and `coward` (threshold retreat) are `EngineBehavior` traits, and `E12` leaves them in place. The stub does not reimplement them.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionSelection"
```

## Structure

```
src/FusionRpg.Core/Actions/IBattleView.cs        (the read seam fog will swap)
src/FusionRpg.Core/Actions/StubIntentSource.cs   (IIntentSource — pursue and kill)
tests/FusionRpg.Core.Tests/Actions/
```

## Testing strategy

- **With no legal action, the battle terminates** rather than hanging. The sharpest test in the module: every actor unable to declare, and the round must end by cap. A hang here is a stopped clock, not a slow test.
- **Ties break identically across two runs with the same seed** — and across two runs with the *actors list shuffled*, which is what catches a hidden dependence on insertion order.
- **Zero allocation per decision**, in bytes, across a 200-actor board.
- **Gate hoisting is proven by count, not by reading the code** — `FactReader.Reads` must scale with targets, not with actions × targets. A correct-but-unhoisted implementation passes every behavioural test and fails this one.
- **The AI reads only through `IBattleView`** — an architecture test failing if `StubIntentSource` references the battle state directly. Without it the seam erodes on the first convenient shortcut, and fog stops being a swap.
- **With no board, selection matches `SelectTarget`** on all eight golden fixtures — the assertion that keeps §6 true.
- **Out of range and holding a movement action produces a move**, not a pass. Inert until `A10`, so it is written against a synthetic board.

## Boundaries

- **Always:** read through `IBattleView`; break ties on ordinal ptr; return a pass rather than nothing; keep decisions allocation-free.
- **Ask first:** anything resembling real AI — threat, retreat, kiting, ability scoring, or reading the power vector. Those belong to the AI program, and the moment the stub grows one, it should stop being a stub and get a map.
- **Never:** an ambient RNG; catalog or dictionary iteration order in a decision; a direct read of battle state; reimplementing `bloodthirsty` or `coward` here.

## Success criteria

1. An actor pursues, attacks, and kills, with no hand-written per-encounter scripting.
2. `SelectTarget` has a replacement that both auto-battle and interactive play enter through.
3. Replay is byte-identical across runs and across list order.
4. Fog of war, when it arrives, changes **one** implementation behind `IBattleView`.
5. The stub is still recognisably a stub, and its replacement is a program rather than a patch.
