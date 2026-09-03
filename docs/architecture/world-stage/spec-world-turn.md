# Spec: world-turn

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-turn` in the
[world-stage capability map](../world-stage-map.md). **Level 4**, depends on `world-hud` (which owns
the bottom-right anchor this cluster occupies).

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.6, §8c.1, §8d.1, §8e.3.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §H.1–H.2 (§H.3–H.6 are
`world-notify`'s).

---

## Objective

Build the turn control as a **cluster, not a button**: End Turn in each of its four states, a live
count of legions with movement remaining and no orders, a cycle-to-next-unresolved control, the
force-end escape hatch, and file-orders.

The pattern is unanimous — Endless Legend's bottom-right wheel holds eight functions, ES2 trims it to
three, and Total War, HoMM3 and Civ VI all cluster the same way. ES2's third function is the one
worth taking outright: *"This icon indicates the number of ships or fleets that have remaining
movement points but no orders to move"* — the unresolved-work signal sitting adjacent to the control
it would block. It is the cheapest idea in the corpus and the best one.

**Success is that a player never ends a turn without knowing what they left undone, and never loses a
turn to a control that thought it knew better than they did.**

### Why this is worth building now, and why it was not last month

§8c.1 found the design calibrated for a game that does not exist yet: the player commands one legion
and cannot gain another, so the count's maximum was 1 and cycling walked a set of size 1. §8d.1
answered it by building recruitment first, and §8e.3 fixed the number: **6–10 legions, and it is
tunable**, which puts it in `data/tuning/` per §0.12 rather than a `const`. That is a real set to
walk and a real count to read, and it is `sector-development`'s tuning row to author — this module
consumes the target, it does not produce it.

## Design

### 1. End Turn, in four states

The engine is a **`WaitForAll` barrier with no deadline** — `WaitForAllCommitted`
(`src/FusionRpg.Core/World/Turn/TurnBarrier.cs:17-33`), whose own summary at `:16` reads
*"Turn-based: every commander must have committed. No deadline, ever."* Every state below is shaped
by that fact.

| State | Reads | Behaviour |
|---|---|---|
| **Ready** | `0` and the words *legions waiting on you* | Ends the turn. **The 0 is stated in words, not left as a bare digit** — a lone `0` is indistinguishable from a feed that failed to load, which is the same defect as a stale number |
| **Nag** | `2 legions with moves left and no orders`, and the button relabels to *End turn anyway* | Appears on attempt; **does not stop you**. One extra keypress, never a modal, never a lost turn |
| **Hard-blocked** | The blocker's own sentence beside the disabled button, with *Take me there* | Clicking the button **navigates to the blocker** rather than doing nothing — Civ VI's rule, and the reason its end-turn icon becomes the blocker itself. GG-55 |
| **Committed — waiting** | *Waiting on 2 commanders · no deadline · the map stays live* | Names who it waits for and how many. It can last indefinitely and **must never read as a hang** |

**The committed state is where GG-15 bites.** Acknowledge instantly, paint authority never: the turn
has not advanced until the server says it did. The commit endpoint requires the client to name the
turn it thinks it is ending (`WorldEndpoints.cs:122-123` refuses `turn.missing`, with the comment
*"a commit that does not name its turn is a commit that can be retried into the next one once the AI
releases the barrier automatically, which costs the player a turn they never played"*). The commit
itself is `:125`, and only the commit that actually stepped the world reports `Advanced` (`:129`,
`:135`). So the cluster reads `Advanced` to leave the waiting state — never a local timer, never an
optimistic advance.

### 2. Two blocking classes, declared — and the hard-block list defaults to **empty**

ES2's two-tier rule, verbatim from its manual: *"Some notifications will appear when you attempt to
end your turn… Others will prevent you from completing your turn until you have resolved them. For
example, a battle notification will prevent you from completing the turn until the battle is
resolved."*

**And that exact blocker is the one Amplitude later patched out**: *"Battle Result Notifications no
longer block the turn."* They shipped a blocker into the hard class, players called it a feature not a
bug, and it came back out.

So:

> **The hard-block list ships EMPTY. Every addition to it is argued in writing, in this spec, and
> reviewed.** It is a declared list — the GG-53 analogue for band 3, where D6 already establishes that
> exactly one class may take a blocking layer unprompted — not a config row and not a per-feature
> decision made by whoever adds the next event kind.

A blocker is a promise that resolving it is worth more than the player's next click, and that promise
is usually false.

**The blocker's own correctness is a first-class testable surface, not an incidental.** Humankind's
blocking end-turn produced a soft-lock family its own bug forum describes as *"not a single bug, but
multiple different bugs that have the same symptom"*, alongside a filed defect: *"Turn Button Shows
End Turn When Moves Are Still Available."* If the button's state can disagree with the world's, the
player can trust neither — so §5's property tests target exactly that disagreement, and the force-end
hatch is the insurance that a disagreement can never cost a session.

### 3. The live unresolved count, and cycling

**The count.** Legions of yours with movement remaining and no order filed this turn. It is
**client-derived** — `WorldEntityDto.MovementRemaining > 0` (`src/FusionRpg.Contracts/WorldDtos.cs:183`)
intersected with the pending-order queue. There is no server field for it and none is asked for: the
same derivation drives the outliner's per-row unresolved flag, so it is written **once** and consumed
twice. One derivation, two surfaces — two derivations is how the count and the flag come to disagree
in front of the player.

`MovementRemaining` is **per-mille and its name says nothing about its unit at all**. A full march is
`MovementPolicy.PointsPerTurn = 1000` (`src/FusionRpg.Core/World/Movement/LaneCost.cs:23`), a scout
gets `ScoutPointsPerTurn` = 500 (`:26`), hold gets 0 (`:40`). It goes through `world-numbers` with
the per-mille family declared, like every other magnitude on the stage.

**The cycle control lives on the count**, so reading the problem and acting on it are the same
gesture. Once cycling starts, the row names the current subject and its movement — *"Ash Column —
500‰ movement left"* — rather than only counting.

**Cycling is player-initiated, always.** Civ VI's auto-cycle is the failure to design against: it
selects units *late*, after the player has already picked a different one, so the wrong unit moves;
and units can become un-cycleable blockers with no way forward. This cluster never takes a selection
from the player between actions.

**Our cycle key is `W`, and that is our choice, not a borrowed fact.** Civ VI's *mechanism* is what we
are citing — the end-turn icon becomes the next blocker, and clicking it navigates to that blocker.
Its cycle **key** is not: published descriptions point at the space bar, and Shift+Enter's
reliability in Civ VI is contested. We bind `W` because it is free and adjacent to the map's arrow
pan; no source is claimed for it.

### 4. The keymap trap — verified, and it can throw on mount

**`registerGlobalVerb` throws on a duplicate key.** `web/fusion-rpg-web/src/shell/keymap.ts:45-50`:
if a key is already in the registry it raises *"already registered by … — every global verb has
exactly one owner."* That is correct design (GG-20: one key, one meaning) and it is a **runtime
throw inside a mount effect**, so a collision does not degrade — it takes the stage down.

**And the collision is reachable by a player, not only by a programmer.** The eight rail bindings in
`layers/system/keybindings.ts:22-31` (`c k r f p e a h`) are **player-rebindable**, and the rail
registers one global verb per unlocked layer from that live table
(`stages/sanctum/SanctumStage.tsx:165-176`). `conflictFor` (`keybindings.ts:87-95`) checks a candidate
key **only against the other seven bindable actions** — it has no knowledge of a stage's own
registered verbs. So a player who rebinds Chronicle to `w`, or Almanac to `1`, passes the rebind UI
cleanly and then makes the world stage throw the next time it mounts.

**Three obligations follow, and none of them is optional:**

1. **This module registers its verbs through one owner** — a `worldVerbs.ts` module that registers
   the stage's whole set in a single effect and unregisters it as a unit, so ordering is deterministic
   rather than dependent on which component mounted first.
2. **Registration never throws into the tree.** The stage's registration wraps each call, and a
   collision surfaces as a *named, player-readable* condition — *"`W` is bound to Chronicle; the turn
   cycle has no key until you change one"* — with a link into the Controls screen. A rebind must not
   be able to break a stage.
3. **`conflictFor` needs to see stage verbs too.** That is a `keymap.ts`/`keybindings.ts` change and
   therefore **ask-first** — every stage binds through it. This module states the requirement and does
   not make the change unilaterally.

**A second, harder finding, and it changes what the force-end hatch can be bound to.** The single
listener passes only `event.key` to the registry — `useGlobalKeys.ts:25` is
`dispatchGlobalVerb(event.key)`, with **no modifier state carried at all**. So `Shift+Enter` and
`Enter` arrive as the same registry key `"Enter"`, and the registry cannot tell them apart. **The
plate's `⇧⏎` force-end binding is not expressible in the shipped keymap.** Two honest resolutions,
and the choice is the owner's because the first one is a shell change:

- **Teach the keymap modifiers** — a canonical key form (`"Shift+Enter"`) produced at the listener and
  consumed by the registry. Correct, small, and it touches every stage's keymap, so it is ask-first.
- **Bind the hatch to an unmodified key of its own**, and keep `⏎` for the ordinary end. Ships without
  a shell change; costs the gesture's family resemblance to Civ VI's.

Until one is chosen, the hatch is reachable by **pointer** — the *end anyway* control beside the
blocker's sentence — which is the shipping-critical half. The keyboard binding is the part that is
blocked, and it is blocked on a fact, not a preference.

### 5. File orders

The fifth cluster member and the least dramatic: it commits the pending queue as one batch. It shares
the queue with `world-targeting` (`worldSelection.ts`'s `PendingOrder` list) and adds nothing to it.
Its only rule is GG-15's: the button acknowledges immediately, and the orders are not shown as filed
until the server has accepted them.

## What stays out

- **Notifications.** §H.3–H.6 — the two classes, the passive rail, flush-on-End-Turn and the
  per-notification channel control — are `world-notify`'s. This module owns only the **blocking
  classification**, which `world-notify` reads.
- **The turn report.** `world-playback` owns the keyframe rail, its transport, and the translation of
  every engine token the turn produced.
- **The calendar and the turn number.** `world-hud`'s top strip.
- **Recruitment.** `sector-development` produces the legions this cluster counts. §8e.3's 6–10 target
  is consumed here and authored there.
- **Changing the barrier.** `WaitForAllCommitted` is not touched. The server has no notion of a
  blocked commit and this module does not give it one — a hard block is a *client* refusal to send.

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run — includes keymapGuard and the blocker property tests
npm run build            # tsc --noEmit && vite build
npm run lint
```

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/turn/
    TurnCluster.tsx          → the bottom-right cluster; the four End Turn states
    UnresolvedCount.tsx      → the count, its words, and the cycle control on it
    unresolvedLegions.ts     → THE derivation — MovementRemaining > 0 ∧ no order filed
    unresolvedLegions.test.ts
    blockingClasses.ts       → the declared list; hard-block ships as an empty array
    blockingClasses.test.ts
    forceEnd.ts              → the escape hatch, pointer-reachable today
    worldVerbs.ts            → the stage's single verb-registration owner
    *.test.tsx
```

`unresolvedLegions.ts` is imported by `world-outliner` as well. It lives here because the count is
this cluster's reason to exist; the outliner consumes it rather than re-deriving it.

## Code style

The declared list is data in code, with its emptiness stated rather than implied:

```ts
/**
 * Events that HARD BLOCK the turn. Ships empty, and stays empty until an addition is argued in
 * spec-world-turn.md §2 and reviewed. ES2 shipped a battle notification into this class and
 * patched it back out; the default is the lesson.
 */
export const HARD_BLOCKING_EVENTS: readonly BlockingEventKind[] = [];

/** Events that NAG on attempt — they appear when you try to end, and never stop you. */
export const NAGGING_EVENTS: readonly BlockingEventKind[] = ["legion.idle", "loam.will-release"];
```

The count states its unit and never renders a bare digit:

```tsx
// Right — the word is part of the fact.
<Count value={0} noun="legions waiting on you" />

// Wrong — a lone 0 and a failed fetch look identical.
<span>{unresolved.length}</span>
```

## Testing strategy

Vitest, colocated. Five groups; group 3 is the one Humankind's bug forum wrote for us.

1. **Four render states.** Ready, nag, hard-blocked, committed — each asserted by its visible words,
   not by a class. The ready state asserts the noun phrase is present, so a bare `0` cannot pass.
2. **The derivation, once.** `unresolvedLegions` over a fixture of 10 legions across march / scout /
   hold and with and without filed orders. Assert the per-mille boundaries explicitly: 1000 and 500
   count as unresolved when no order is filed; 0 never does.
3. **Blocker correctness, as properties.** Over generated worlds: *the button's state always agrees
   with the world's* — if any legion satisfies the unresolved predicate the button is never in the
   Ready state, and if none does it is never in the nag or blocked state. This is the direct test of
   *"Turn Button Shows End Turn When Moves Are Still Available"*, and it is a property because the
   defect family is *"multiple different bugs with the same symptom"* rather than one case.
4. **The hard-block list is empty**, asserted by a test that names the rule. Adding an entry fails a
   test whose message points at §2 — which is how "argue every addition" becomes mechanical.
5. **Keymap safety.** With `w` rebound to a rail action, mounting the world stage does **not** throw
   and the cluster reports the collision in player words. Plus the negative fact this module must not
   regress: `Enter` and `Shift+Enter` are indistinguishable to `dispatchGlobalVerb`, so no test may
   assert a `⇧⏎` binding until the keymap carries modifiers.

Sizing: every fixture above runs at the §8e.3 target — 6 and 10 legions — not at 1. A cluster tested
at one legion tests nothing about the count, the cycle, or the list it escapes.

## Boundaries

- **Always:** derive the unresolved set in one module; state the count in words; let the player end
  the turn; read `Advanced` from the server before leaving the committed state; declare
  `MovementRemaining`'s per-mille family at every render.
- **Ask first:** any addition to `HARD_BLOCKING_EVENTS` — each one is argued in §2 and reviewed. Any
  change to `keymap.ts` or `keybindings.ts`, including the modifier support §4 needs and the
  `conflictFor` widening — every stage binds through those. Any change to the barrier or to the commit
  contract.
- **Never:** auto-cycle, or take a selection from the player between actions. Never predict the turn
  advancing (GG-15). Never render a bare digit as a fact. Never claim `W` is Civ VI's cycle key —
  cite its mechanism, not its binding. Never let a rebind throw a stage down.

## Success criteria

1. All four End Turn states render with their own words, and the committed state never reads as a
   hang at any duration.
2. The unresolved count is live, correct at 6–10 legions, and derived in exactly one module that the
   outliner also imports.
3. Cycling walks the real set, is player-initiated only, and names its current subject and that
   subject's movement in per-mille.
4. `HARD_BLOCKING_EVENTS` is empty, and a test enforces that any addition arrives with an argument.
5. The property test proves the button's state cannot disagree with the world's.
6. The force-end hatch is reachable by pointer, and the keyboard half is recorded as blocked on the
   keymap's missing modifier support rather than silently dropped.
7. A player rebind cannot make the stage throw on mount.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**One, and it is a decision rather than an unknown.** §4's second finding — `dispatchGlobalVerb`
carries no modifier state, so `⇧⏎` cannot be bound — has two specified resolutions: teach the keymap a
canonical modified-key form (a shell change affecting every stage, hence ask-first), or bind the hatch
to an unmodified key. Both are costed above; the pointer path ships either way, so this blocks the
hatch's keyboard binding and nothing else.
