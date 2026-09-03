# Spec: world-targeting

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-targeting` in the
[world-stage capability map](../world-stage-map.md). **Level 4**, depends on `world-render` and
`world-commands`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.5, §4.11, §7.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §E (E.1 route preview,
E.2 blocked targets, E.3 range overlays, E.4 the queued order, E.5 the verb vocabulary).

---

## Objective

Move targeting **onto the map**, and make every refusal a sentence.

Today the order flow is *explained in prose*. `WorldPage.tsx:365-369` renders:

```tsx
{legion
  ? `${legion.entityId} is selected — click a sector, then March here.`
  : "No force selected. Click the sector one of yours is standing in, then pick it above."}
```

That is an instruction manual printed beside a raw entity id, under a column of text buttons in a
`300px` sidebar (`WorldPage.tsx:221`). It is exactly the half of the failure §7 names as
*interaction* — and it is the half a rendering fix alone would leave in place.

**Success is that selecting a legion answers "where can I go, at what cost, and what is refused and
why" on the map itself, before any button is pressed.**

## Design

### 1. The pure layer survives; this module is its view

§7 is explicit that `worldSelection.ts` is good and should not be re-derived. It is reused whole:

| Function | Line | What it already does right |
|---|---|---|
| `routeBetween` | `worldSelection.ts:81-125` | BFS over open lanes, skipping `severed`, in **stable lane-id order** — deterministic, so two renders never draw different routes |
| `routeForLegion` | `:137-153` | The awkward case, already handled: a legion caught mid-lane must have its current lane at the **head** of the path, because the engine refuses any path not containing it (`path.not-contiguous`) |
| `worldUiReducer` | `:39-60` | `queue` / `unqueue` / `clear-queue`, with the one-order-per-legion-per-kind replacement rule the engine also applies |
| `toRequests` | `:63-73` | The exact `POST /commands` payload |
| `orderId` | `:155-158` | `t{turn}-{kind}-{entityId}` — the id that makes filing twice idempotent |

Nothing in that list is rewritten. What is added is a view that draws it and a widening of the two
types it carries.

### 2. `PendingOrder.kind` is a closed union of three and must widen to eight

```ts
kind: "move" | "clear" | "claim";        // worldSelection.ts:13 — today
```

Plate 11 §E.5 draws **eight** verbs. The union widens to match, and each new member arrives with the
field the engine needs — which is `world-commands`' work on the wire and this module's work in the
queue:

| Verb | `kind` | Extra field | Status today |
|---|---|---|---|
| March | `move` | `lanePath` | live |
| Take the ground | `claim` | — | live |
| Break the guard | `clear` | `slotIndex` | live |
| Change posture | `stance` | `stance` | live on the C# wire (`WorldDtos.cs:213-214`), **missing from the TS mirror** (`lib/bus/world.ts:23-30`) |
| Do nothing | `stand-fast` | — | live; never a button — it is what "end turn with nothing queued" means |
| Feed the ground | `sustain` | **`amount`** | inert — the resolver runs, the wire carries no amount |
| Raise | `build` | **`structureId`** + `slotIndex` | inert — same shape, no structure named |
| Ward a road | `ward` | a **lane id**, not a sector id | no command kind at all |

**`ward` is the one that breaks the shape**, and it must not be smuggled in as a sector order: a ward
sits on a lane (`WorldLaneDto.WardLevel`, `WorldDtos.cs:160`), so `PendingOrder` gains an optional
`laneId` and the overlay's click target is a **line**. Designing it as a sector action would fight the
model and would collide with binding a warden to a sector (`WardenBindingId`), which is a different
mechanic the engine already separates.

`toRequests` (`:63-73`) widens in lockstep. The rule from `world-commands` applies here too: a field
this module puts in the queue and the wire drops is **lost silently** — that is how `stance` was found
missing — so every new field has a round-trip test, not just a unit test.

### 3. Route preview: this turn, next turn, later — and the cost is projected, never derived

Select a legion and the map answers immediately: **solid bright** is ground reached this turn,
**dashed amber** next turn, **dotted faint** later — and every one of them **also carries its turn in
text** (`T`, `T+1`, `T+2`). Endless Legend uses white for this turn and orange for the ones after; the
idea transfers, the reliance on colour does not (GG-27, GG-30).

Reach is per-mille. A full march is `MovementPolicy.PointsPerTurn = 1000` (`LaneCost.cs:23`), a scout
gets `500` (`:26`), holding gives it up entirely (`:40`) — and `WorldEntityDto.MovementRemaining`
(`WorldDtos.cs:183`) is in those units, which its name does not say. It goes through `world-numbers`
with the per-mille family attached like every other magnitude.

**The lane cost is a gap, and the resolution is a projection, not a TypeScript port.**
`routeBetween`'s own comment states the limitation: *"Wave 1 has no lane costs on the client — this
finds *a* legal route, and the engine is the one that decides how far along it the legion actually
gets this turn."* Pricing a lane needs `LaneCost.For` (`LaneCost.cs:117-131`: `length × type
multiplier × hazard`, with an 800‰ ley discount for a matching banner), plus `LaneTypeCatalog`, plus
the legion's banner element derived from its members' species — **none of which is on the wire**.

Re-implementing that in TypeScript is forbidden by the ideal's own §0.13 (*"a map UI displays numbers
and must never derive a curve to render one"*) and would put a second copy of a hashed engine rule in
the browser. So:

> **The per-lane march cost for the selected legion is projected by the server and consumed here.**
> This module names the need; `world-wire` owns the projection's shape. Until it lands, the preview
> draws the **route and its hop sequence** — which `routeBetween` already gives — and shows the
> turn split as `pending` with a player-readable reason, per `world-contract`'s `Pending<T>` rule.
> It never guesses a cost and never shows a number it did not receive.

One property of the projection must be preserved deliberately rather than by accident: **fog prices
the route too.** `LaneCost.For`'s lookup is a parameter precisely so a faction that has never scouted
a ley lane's endpoints does not know its climate, the discount does not apply, and the march is
**over**-priced (`LaneCost.cs:108-116`). An army plans with what it knows. The preview must not
quietly correct it, and a test asserts it does not.

**And it never paints authority early** (GG-15). The preview is a *plan*: it draws instantly, it is
honest about being a plan, and it never renders a move as having happened. Responsiveness is bought
with acknowledgement and motion, never with prediction.

### 4. Range overlays — three verbs that reach past where you stand, one grammar

| Verb | Range | Measured how | Source |
|---|---|---|---|
| **Raise a waystation** | `3` hops | **Plain road hops**, unweighted — a hard road and an easy one are one hop each; and measured from any holding of yours that is currently **habitable**, not merely owned | `BuildResolver.cs:90-99` calls `WithinWaystationRange`, which walks `Hops.Between` against `LoamPolicy.WaystationRangeHops` and skips any sector failing `Habitability.For` (`:145-159`). The `3` is a tunable: `data/tuning/loam.v1.json:35` |
| **Raise a well** | none | A rootbed is its own source — you raise it where you stand | Same site: the range check is gated on `RequiredSlotKind == SlotKind.Seat` (`BuildResolver.cs:94`) |
| **Ward a road** | adjacent lanes | The target is an **edge** | `WorldLaneDto.WardLevel` |
| **Take the ground** | `0` | Exactly where you stand | — |

**One grammar for all of them.** Reachable ground gets a solid ring **plus its hop number**;
out-of-reach ground gets nothing except, on hover or focus, the sentence saying why. The number is
what makes the overlay teachable — a player who reads `3` on the far sector learns the rule without a
manual and never has to count nodes.

Range zero is **drawn anyway**, as a one-cell overlay. It tells the player *"this verb reaches nowhere
else"* in the same visual language as the three-hop one; silence would make them wonder whether they
had missed a target.

These overlays and the placement overlay are **transient**: no picker slot, no hotkey, alive only
while the verb is (`world-lenses` §2). On Esc or completion they restore the player's chosen lens.

### 5. Every refusal is translated, and it is shown where the decision is made

GG-23 is Tier-1 and this is the second surface it bites on. The engine's vocabulary is large and none
of it may reach a player raw. Verified against `src/FusionRpg.Core/World/` on 2026-09-03 — the map's
count is **37 drop reasons** (33 bare, 4 carrying an argument); the ones this module renders:

| Family | Tokens | Where the sentence lands |
|---|---|---|
| Route | `path.not-contiguous`, `path.empty`, `lane.severed`, `lane.gated`, `lane.one-way`, `lane.unknown`, `lane.no-heading` | On the **road** for a lane refusal; on the **target sector** for a path refusal |
| Taking ground | `claim.contested`, `claim.guarded:<n>`, `claim.elsewhere`, `claim.already-yours:` | On the target sector; a guard refusal on the **guarded slot**, in the inspector |
| Raising | `build.out-of-range:<sector>`, `build.wrong-slot-kind:<a>-needs-<b>`, `build.cannot-afford`, `build.occupied:<what>`, `build.not-yours`, `build.elsewhere`, `structure.unknown` | Out-of-range on **every out-of-reach sector** as the overlay; slot refusals on the slot; affordability on the **confirm, with both numbers** |
| Feeding | `sustain.nothing-carried`, `sustain.not-standing`, `sustain.not-yours`, `amount.invalid` | On the **verb**, before it is offered |
| The legion | `entity.routed`, `entity.not-yours`, `entity.gone`, `entity.missing`, `entity.unknown`, `entity.held` | On the **marker**, and on every verb |
| Posture | `stance.unknown` | On the posture picker |
| Protocol | `command.id-missing`, `command.id-too-long`, `slot.unknown`, `slot.elsewhere` | Should never be reachable — a client bug if it is, and it surfaces as such in development |

**Two rules the table encodes, and both are testable:**

1. **A reason is a sentence with the subject in it.** *"Ashfoot is carrying 180 loam. A waystation
   costs 300."* beats *"cannot afford"* because it names the shortfall the player has to close.
2. **A reason is shown where the decision is made.** A road refusal belongs on the road, a slot
   refusal in the inspector, a legion refusal on the marker. Scattering them into one notification
   string is the current behaviour and it is why the map reads as a flowchart.

**The translation itself is `world-playback`'s table**, not a second one here. This module imports it
and supplies placement. One table, one module — per-prefix handling is exactly how today's 5-of-21
state arose.

**Blocked is drawn, never hidden and never merely dimmed** (GG-55): hatched, crossed and captioned.
Hiding an unavailable action is how a mechanic quietly stops existing; dimming it without a reason is
how a player concludes the game is broken.

**Inert is a third treatment, visually distinct from blocked.** `sustain` and `build` are implemented
end to end in the engine and unreachable because the wire drops one field each. Until `world-commands`
lands them they are drawn hatched with *"the game cannot carry this order yet"* — because hiding them
would hide the fact that they are two fields from working. Refused means *this order is wrong right
now*; inert means *this order cannot be carried at all*.

### 6. The queued order: filed, drawn, and takeable back

Filing an order and ending the turn are two separate acts. Between them the order is **queued**: it
exists, it is drawn, nothing has resolved, and it can be pulled back.

- **On the map, the token never moves.** The legion is drawn where it actually is; the intent is a
  **dashed flag on the destination** and a lit route between them. Nothing about a queued order may
  look like it has happened.
- **In the turn cluster, the queue is a list with a way out of it.** Each row names the order in
  player words and carries *take back*. `unqueue` (`worldSelection.ts:56`) already does the work.
- **The player-facing promise is exact and must hold: nothing you filed this turn is binding until you
  end the turn.** Orders are keyed by `orderId` and filing the same id twice is the same order, so
  take-back removes it from the set the client will submit and re-submits the remainder.
- **After the commit there is no take-back.** The queue becomes a record and the stage switches to
  playing the turn back (`world-playback`).

**A standing order is re-issued whole each turn.** The server keeps no multi-turn queue, so a march
that takes three turns is three filings. The interface may make re-issuing nearly free — remembering
last turn's orders and offering them back — but it **must not pretend the server is remembering
them**. Whether the server stores a standing order is a turn-engine decision with hashing and replay
consequences, and the ideal's §6 explicitly leaves it undecided.

## What stays out

- **The command wire.** `world-commands` owns `Amount`, `StructureId`, the `ward` kind, the `cede`
  order and the `dowse` stance. This module queues them and must not ship ahead of them: a queued
  order whose field the wire drops is worse than a disabled button.
- **The translation table.** `world-playback` owns it; §5 supplies where each sentence lands.
- **The per-lane cost projection.** `world-wire` owns it; §3 states the need and the interim.
- **Lens selection and the lens picker.** `world-lenses` owns them; this module's overlays are
  transient and restore the player's chosen lens.
- **The confirms.** `world-confirms` owns the band-3 dialogs that a stake-bearing order opens.
- **The turn cluster itself.** `world-turn` owns End Turn and the unresolved count; this module
  contributes the queue rows it lists.

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
```

Round-trip coverage for a new order field is a **server** test — the reveal path is where `stance` was
lost — so `dotnet test tests\FusionRpg.Core.Tests` runs too when `world-commands` lands a field this
module queues.

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/
    targeting/
      targetingState.ts      → pure: active verb, its overlay, the restore contract with world-lenses
      targetingState.test.ts
      RoutePreview.tsx       → this-turn / next / later, each with its turn in text
      RangeOverlay.tsx       → one grammar, three verbs; ring + hop number
      BlockedTarget.tsx      → hatched + crossed + captioned, reason from the shared table
      QueuedOrders.tsx       → the filed list with take-back
  features/world/
    worldSelection.ts        → PendingOrder.kind widened to eight; amount / structureId / laneId added
```

`worldSelection.ts` stays where it is — it is pure and has two test files with 46 tests behind it; only the type
widens. Its DTO import moves with `world-contract`'s `lib/bus/world.ts` migration.

## Code style

The state is a reducer, the overlay is a component, and a refusal is a lookup — never a string built
at the call site.

```ts
export type PendingOrder = {
  commandId: string;
  kind: "move" | "clear" | "claim" | "stance" | "stand-fast" | "sustain" | "build" | "ward";
  entityId: string;
  sectorId?: string;
  /** A ward targets an edge, not a sector — see spec §2. */
  laneId?: string;
  slotIndex?: number;
  lanePath?: string[];
  /** Whole loam units. `sustain` only; the wire drops it until world-commands lands. */
  amount?: LoamUnits;
  structureId?: string;
  stance?: "march" | "scout" | "hold";
  label: string;
};
```

## Testing strategy

Vitest, colocated, plus one Playwright path. Five levels:

1. **The pure layer is unchanged** — the 46 existing `worldSelection` / `worldViewModel` tests stay
   green with no edits. A diff that touches them is a re-derivation and is wrong.
2. **The widened union round-trips** — for each new `kind`, `toRequests` emits the field the engine
   reads, and a fixture asserts the field survives the wire shape. The mid-lane case gets its own
   test: `routeForLegion` puts the current lane at the head, because the queue looks correct and the
   order is silently dropped otherwise.
3. **Refusals are sentences, and they are placed** — a table test over every token in §5 asserts (a)
   no raw token appears in rendered output, and (b) the sentence is attached to the right subject
   (road / sector / slot / marker). This is the GG-23 gate for this surface.
4. **The preview is a plan, never authority** — queueing a march does not move the marker; the token
   stays at `atSectorId` and the destination carries a dashed flag. Asserted by test id, not by class.
5. **Fog over-prices and stays over-priced** — with an unscouted ley lane's endpoints, the preview
   shows the undiscounted cost. A test that "fixes" this is fixing the wrong thing.

Playwright covers the one interaction the unit tests cannot: select a legion → the map highlights →
click a reachable sector → the order appears in the queue → take it back → the queue is empty and the
marker never moved.

## Boundaries

- **Always:** reuse `routeBetween` / `routeForLegion` / `worldUiReducer` rather than reimplementing
  them; show a refusal where the decision is made; draw blocked targets with their reason; carry the
  this-turn / later split in **text** as well as in style; keep the marker where the legion is.
- **Ask first:** any change to the one-order-per-legion-per-kind replacement rule in `worldUiReducer`
  (it mirrors an engine rule). Any client-side march-cost computation — §3 rules it out, and reopening
  it is a decision about where a hashed rule lives. Any ninth verb.
- **Never:** print an engine token on a player surface. Never re-implement `LaneCost.For` in
  TypeScript. Never move a token to show a queued order. Never file an order whose field the wire
  drops without saying, on the control, that it cannot be carried yet.

## Success criteria

1. The prose instruction at `WorldPage.tsx:365-369` is gone, and no raw entity id reaches a player
   surface.
2. Selecting a legion draws its reachable set with the route and a **this-turn vs later** distinction
   carried in text as well as in style.
3. Range overlays exist for Raise (3 hops from habitable ground), Ward (edges) and Claim (range 0,
   drawn anyway), all in one grammar with hop numbers.
4. Every refusal in §5 renders as a sentence naming its subject, placed on the road / sector / slot /
   marker it belongs to — proven by a table test over the whole token set.
5. `PendingOrder.kind` carries all eight verbs, with `amount`, `structureId`, `laneId` and `stance`,
   and each survives the wire round-trip when `world-commands` lands it.
6. A queued order is visible, takeable back, and never moves the marker; the commit locks it.
7. The 46 existing pure-layer tests (`worldSelection.test.ts` 19, `worldViewModel.test.ts` 27) are green **without edits**.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §4.5 decided targeting-on-the-map, plate 11 §E drew all four states, and the two things this
module cannot supply itself — the command fields and the per-lane cost — are named dependencies on
`world-commands` and `world-wire` with a stated interim, not questions.
