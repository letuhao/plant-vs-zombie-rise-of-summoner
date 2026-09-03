# Spec: world-confirms

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-confirms` in the
[world-stage capability map](../world-stage-map.md). **Level 5**, depends on `world-inspector` and
`world-commands`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.6, §8c.2, §8d.2.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §K (K.1 commit a legion,
K.2/K.3 bind a warden, K.4 ground you are about to lose).

---

## Objective

Three band-3 dialogs, each of which **names the exact thing being lost** rather than asking *"are you
sure"* — and one of which names something that can never be taken back.

GG-22 is the rule: *"anything that deletes, retires, consumes, or overwrites confirms first, names the
exact thing being lost, and prefers undo over confirmation where the domain allows it."* Two of these
three have no undo, so the naming carries the whole weight.

GG-53 and D6 are the ceiling: **only run-ending results may take a blocking layer unprompted**, and
everything else reports at band 4 and queues. **Every dialog in this module is opened by the player**
— by choosing a verb, or by pressing the action on a warning. None of them appears on its own.

**Success is that a player who binds a warden knows, before they do it, that they can never undo it.**

## Design

### 1. Commit a legion — a travel act, and the stake is no longer just the creatures

Plate 03 §B already drew a version of this confirm: *"three creatures march… a loss costs their
condition."* True, and silent about the economy. Since loam shipped, marching also removes a garrison
from ground that may be fading, takes the supply with it, and starts a burn clock.

**Plate 11 §K.1 counts four stakes where plate 03 counted one. The confirm draws all of them, plus
the two facts a player needs to judge them — the runway and what is waiting:**

| Row | What it names | Field |
|---|---|---|
| The garrison leaving | *"Four bound creatures leave your ground"* | `WorldEntityDto.Members` (`WorldDtos.cs:185`) |
| The supply going with them | *"They carry their supply with them — 180 loam"* | carried loam — a `world-wire` projection |
| The burn clock starting | *"They burn on the march, every night — −40 / night"* | `burnPerMember` × members, `data/tuning/loam.v1.json` |
| The runway | *"What they carry runs out on night 11"* | the turn `legion.runway:` names |
| The fade the departure causes | *"Frost Mire loses its garrison and fades faster: −10 → −16"* | the sector's net, before and after |
| What is waiting | *"Waiting at Ashfall: a host"* | `WorldForceDto` — a **band** unless surveyed, never an exact figure |

Two rules the last two rows encode. The fade row must show **both numbers**, because *"fades faster"*
without them is a mood, not a fact. And the waiting-force row must never render a band as a count:
`WorldForceDto.Exact` is false unless the viewer stood on the ground with it (`WorldDtos.cs:47-61`, the flag at `:52`),
and `world-contract`'s `ForceView` makes rendering a band as an exact figure impossible by type.

**It closes with the truth about timing:** *"A fight is likely. Nothing resolves until you end the
turn."* The order is queued, not executed — the same promise `world-targeting` §6 makes — so this
confirm is about **filing** a stake, and it is takeable back until the commit.

### 2. Bind a warden — permanent, and the refusal downstream is real

This is the one act on the stage that the rest of the game will not undo, and the engine is
unambiguous about it. `ReleaseContract` checks the warden flag **before every other release blocker**
and refuses unconditionally:

```csharp
// Permanent, for the life of the world (spec-loam-texture.md) — unconditional, checked
// before every other release blocker below, none of which can override it.
if (row.Warden) return (false, "contract.warden-permanent", null);
```
— `RpgStore.Contracts.cs:351-353`

So the confirm states the loss in full, in player words, with no hedging: **Ashkell will never leave
your roster, never take another contract, and can never be released — not for souls, not by retiring
it, not ever.** *You keep the ground. You do not keep the demon.*

**What it costs, read from `BindAsWarden` (`RpgStore.Contracts.cs:283-326`) rather than from the
plate:**

| Row | Value | Source |
|---|---|---|
| The binding becomes permanent | — | `BindContractRowUnlocked(…, warden: true)` (`:323`) |
| One binding slot, spent for good | `7 / 8 used` | `CountBoundContractsUnlocked >= ContractPolicy.Capacity(purchasedSlots)`, refusing `capacity.full` (`:309-311`) |
| A soul fee, taken now | one day's upkeep | `fee = ContractPolicy.UpkeepPerDay(rarity, personality)`, charged at bind (`:316`), with the balance checked against it at `:317-318` |
| Its daily upkeep never stops | the same rate | `SettleContractsUnlocked` charges it every day thereafter |
| The ground stops fading | permanently exempt | `LoamForecast.Weakest` skips any sector with a `WardenBindingId` (`LoamForecast.cs:24`) |

**One correction the plate invites and this spec makes explicit:** the fee taken now and the daily
upkeep are **the same number**, because binding charges day one. Drawing them as two different figures
would be a lie the player catches on their second day. If the copy shows two rows — and it should,
because they are two different obligations — it shows the same rate twice and says so.

**The engine's four refusals get sentences before the dialog opens**, not after (GG-55):
`capacity.full` → *"Every binding slot is taken."*; `souls.insufficient` → *"You cannot pay the fee."*;
`contract.already-bound` → *"Ashkell is already under an ordinary contract."*; `specimen.missing` →
should never be reachable.

**The verb is "Bind a warden here", not "Ward".** The engine separates the two mechanics —
`WardLevel` sits on a lane, `WardenBindingId` on a sector — and an earlier draft of plate 11 called
both "Ward", so choosing the irreversible one got you the road overlay. Repaired 2026-09-03 (defect
class 6); the naming is now load-bearing and this module must not undo it.

### 3. Two steps, and the second appears only on a low balance

Owner decision: a **second confirmation step when the soul balance is low.**

- **Step 1 — what it costs.** The five rows above, the permanence warning, and `Continue ›`.
- **Step 2 — say it back.** Appears **only** when the balance cannot cover the fee plus the next day's
  upkeep. It states the arithmetic — *"You have 520 souls. The fee is 400 and the upkeep is 400 a day.
  After tonight you cannot pay Ashkell, and an unpaid warden is still bound — you would be carrying a
  debt you cannot release your way out of."* — and requires typing `bind`.

**With souls to spare, step 1 is the whole confirm.** A permanent act deserves one deliberate gesture,
not two on every occasion; a second step charged on every bind would be trained away within a week and
would then be worthless on the one occasion it mattered.

The threshold is `balance < fee + upkeepPerDay` — computed from the same values the engine charges,
never a magic number, and read from the balance the client already has
(`/api/souls/{playerId}`, `lib/bus/demons.ts:135-136`).

Typing `bind` is recall, and GG-24 forbids recall in the general case (*"the player chooses from what
is shown"*). This is the deliberate exception and the reason is stated on the dialog: the friction
**is** the safeguard, and it applies only where an unpayable permanent debt is being taken on.

### 4. Abandon / release ground — drawn **before** the turn, not reported after

The engine already computes this a full turn early with the **same selection** it will use to apply
the fade — `LoamForecast.Weakest` (`LoamForecast.cs:19-31`) is the function `LoamPhases.Pressure`
calls at the moment of the act (`LoamPhases.cs:138`), and the comment there names the property:
*"Same selection the forecast makes a turn early"*. So **the warning and the event cannot disagree**,
and that is exactly what licenses stating the warning this bluntly.

What is missing is only that nothing surfaces it. A player who first learns about it from
`loam.lost:frost-mire` in the turn report **has been told after the decision was taken for them.**

The dialog names, in order: the reach and its arithmetic (*"Five sectors pool their loam here.
Together they earn 210 and cost 248. They are 38 short, and the stores are empty."*), the sector that
goes and why it was chosen, **what goes with it** (the well, the half-built waystation and its lost
nights), and whether losing it splits the territory — which lens 4 already draws and this dialog
names.

Then: **what would stop it.** Pour in the shortfall (with what a legion is actually carrying, so the
option is checkable rather than aspirational), or bind a warden (with its reason if every slot is
taken).

**And the copy constraint that governs this whole dialog until `world-commands` ships the cede
order:**

> **No surface may say *"choose what to release."*** Today `LoamPhases.Pressure` picks the victim
> itself, every turn, via `LoamForecast.Weakest` — and there is **no `abandon` / `cede` / `release`
> command kind** — `WorldCommandKinds` declares exactly seven: `stand-fast`, `move`, `clear`,
> `claim`, `stance`, `sustain`, `build` (`WorldCommand.cs:7-34`). Shipped as plate 11 §K.4 and §H.1 currently draw it, that copy
> is a lie the player catches on their first shortfall. Until the cede order lands the dialog says
> *"here is what will be released, and here is what would stop it"*, which is truthful, ships now, and
> keeps the tension as a forecast.

When §8d.2's cede order does land, the third option — *"Give up Hollowmoor instead"* — is added to the
same dialog, and `Weakest` becomes **a default the player may override**. The property in the first
paragraph must survive that: the player's choice becomes an **input** to the shared function, never a
second code path that computes the answer differently. That is `world-commands`' obligation; this
module's is to not draw the option before it exists.

### 5. None of these opens itself

GG-53 gives exactly one class of event the right to take a blocking layer unprompted, and D6 declares
it **run-ending results only** — level-ups, drops and contract offers report at band 4 and queue.

| Dialog | Opened by |
|---|---|
| Commit a legion | The player choosing March on a stake-bearing route |
| Bind a warden | The player choosing "Bind a warden here" in the inspector |
| Ground about to be lost | The player pressing **Show me** on the band-4 toast, or the fade-risk lens |

The fade warning is the one that would be tempting to open on its own — it is the most important thing
that can happen in a turn. It still does not. It arrives as a toast (`world-notify` §4's top tier),
and *"ground goes tonight"* remains a **nag on attempt** at End Turn rather than a hard block, with
Amplitude's shipped-then-retracted battle blocker as the precedent.

Every dialog uses `DialogShell` (`shell/DialogShell.tsx`), which pushes onto the layer stack and pops
on close (`:30-37`), so Esc pops one layer and the stage behind it never unmounts (GG-6, GG-11).

## What stays out

- **The cede command.** `world-commands` owns it; §4 states the copy constraint that holds until it
  lands.
- **The `BindAsWarden` endpoint.** There is **no production caller** for `RpgStore.Contracts.cs:283`
  today — it is reachable only from the store. `world-commands` owns the first one; this module owns
  the dialog in front of it.
- **The projections.** Carried loam, burn, runway, warden presence and the component arithmetic are
  `world-wire`'s. A row whose field is `pending` renders its reason, never a zero.
- **The turn cluster's blocking classes.** `world-turn` owns the declared hard-block list, which
  defaults to empty.
- **The notification that leads here.** `world-notify` owns the toast and its action button.
- **The battle stage.** What committing a legion into a fight *looks like* beyond this confirm is
  outside the program (ideal §6).

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
```

```powershell
# The warden path crosses into the store, so the C# refusals are covered there.
dotnet test tests\FusionRpg.Data.Tests
```

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/confirms/
    CommitLegionDialog.tsx       → §1, the four stakes
    CommitLegionDialog.test.tsx
    BindWardenDialog.tsx         → §2 + §3, two steps, second one conditional
    BindWardenDialog.test.tsx
    ReleaseGroundDialog.tsx      → §4, before the turn
    ReleaseGroundDialog.test.tsx
    wardenGate.ts                → pure: does this balance need step 2, and why
    wardenGate.test.ts
```

Each is a `DialogShell` consumer. No new shell, no new band, no `z-index` in feature code (GG-5,
enforced by `shell/bandGuard.test.ts`).

## Code style

The stake list is data, so a missing row is a visible diff rather than a forgotten paragraph.

```ts
/** One line of what is being staked. Every row names a subject and a number with its family. */
export type StakeRow = {
  glyph: string;
  /** Player words. The engine token, if any, belongs in the dev tree — never here. */
  says: string;
  value: Rendered;          // world-numbers; carries its unit family
  tone: "loss" | "cost" | "clock" | "risk";
};

/** Step 2 is a function of the balance, not a flag someone remembers to set. */
export function needsSayItBack(balance: Souls, fee: Souls, upkeepPerDay: Souls): boolean {
  return balance < fee + upkeepPerDay;   // see spec §3 — the same values the engine charges
}
```

## Testing strategy

Vitest, colocated. Five levels, and two of them exist because the failure would be silent.

1. **Every stake is named** — for each dialog, a test asserts the specific rows in §1/§2/§4 are
   present by accessible text. A confirm that lost a row still renders and still works; only a test
   notices.
2. **The permanence copy is exact** — the bind dialog contains the words *"can never be released"* and
   *"You do not keep the demon."* This is a copy test on purpose: it is the sentence GG-22 requires and
   it is the one a later refactor would soften.
3. **Step 2 is conditional, both ways** — with a comfortable balance the dialog completes in one step;
   with `balance < fee + upkeepPerDay` step 2 appears and **the confirm button stays disabled until
   `bind` is typed**, with its reason attached (GG-55).
4. **No dialog opens itself** — a test renders the stage, drives a turn with a fade warning in it, and
   asserts **no band-3 layer is on the stack**. This is D6's gate for this module and it is the test
   that would catch the tempting mistake in §5.
5. **The forbidden copy** — a scan asserts *"choose what to release"* and any synonym offering a
   choice of victim appears **nowhere**, until `WorldCommandKinds` gains the cede kind. The test reads
   the command vocabulary rather than a flag, so it turns itself off when the verb exists.

Plus: the band never rounds a **band** into a count. `ForceView` with `exact: false` renders the band
name and ceiling; a test asserts the exact strength never appears.

## Boundaries

- **Always:** name the exact thing being lost; show both numbers when a value changes; render a
  refusal as a sentence before the dialog opens; open only when the player asked; use `DialogShell`.
- **Ask first:** adding a **fourth** confirm — three is the set §8c/§K settled, and a fourth is an
  interruption-budget decision under GG-53. Changing the step-2 threshold away from
  `fee + upkeepPerDay`. Any confirm that would open unprompted, which is a D6 amendment and not a
  feature choice.
- **Never:** say *"choose what to release"* while `WorldCommand.cs` has no cede kind. Never soften the
  permanence copy. Never render an inexact force strength as a figure. Never open a band-3 layer from
  a notification (that is `world-notify`'s boundary and this is the other side of it). Never declare a
  `z-index`.

## Success criteria

1. Three dialogs exist, all band 3 via `DialogShell`, all player-opened, and a test proves none opens
   itself.
2. The commit confirm names garrison, carried supply, burn rate, runway turn, the fade it causes **with
   both numbers**, and what waits — as a band when the force is not exact.
3. The bind confirm states permanence in the words GG-22 requires, lists the slot, the fee, the
   never-ending upkeep and the exemption gained, and says that the fee and the daily rate are the same
   number because day one is charged at bind.
4. Step 2 appears **only** when `balance < fee + upkeepPerDay`, requires typing `bind`, and keeps the
   confirm disabled with a reason until it is.
5. The release warning is drawn **before** the turn, names the sector, what goes with it and whether it
   splits the territory, and offers only options that exist today.
6. *"Choose what to release"* appears nowhere, and the test enforcing that reads the command vocabulary
   so it retires itself when the cede order lands.
7. Every engine refusal on the warden path renders as a sentence before the act, not after.
8. `npm test`, `npm run build`, `npm run lint` and `dotnet test tests\FusionRpg.Data.Tests` are green.

## Open questions

**None.** §8d.2 decided the cede order and this module's copy constraint follows from it; the owner
decided the two-step bind with the conditional second step; GG-53 and D6 already fix who may open a
blocking layer.
