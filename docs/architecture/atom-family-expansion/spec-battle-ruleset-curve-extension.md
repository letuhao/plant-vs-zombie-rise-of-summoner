# Module: `battle-ruleset-curve-extension`

Source: `docs/architecture/atom-family-expansion-ideal.md` §4 point 3, `-map.md`. Owner explicitly
chose to add all 5 curves rather than defer or narrow scope, after being shown the real complications
below — this spec takes that choice as given and addresses each complication directly rather than
re-litigating it.

## 1. Objective

Give `BattleRuleset` a real, calibrated curve for the 5 channels `FamilyExpandGen`'s
`flatReferenceBaseGameUnits` delegate currently returns `null` for — `arm1Max`, `arm2Max`,
`attackInterval`, `produceInterval`, `zombieSpeed` — mirroring the exact existing pattern `BaseHp`/
`BaseAtk`/`BaseDefense` already use, then wire the new curves into `FamilyExpandGen` and register them
in `ssot-power-scale.md`'s closed §10 inventory (a reviewed change, per CLAUDE.md's "One power
ladder" rule). Closes the remaining 5 of 103 `FamilyExpandGen --check` refusals.

## 2. The two complications, resolved

**Complication A — `attackInterval`/`produceInterval` are "lower is better."** Already solved
elsewhere, verified this session: `StatChannels.DirectionOf` (`src/FusionRpg.Core/Stats/ModifierOp.cs:94,98`
— a static method on the `StatChannels` class declared further down the same file as the `ModifierOp`
enum, not a method on `ModifierOp` itself) already declares `AttackInterval`/`ProduceInterval`/
`AttackCountdown`/`ProduceCountdown` as `ChannelDirection.LowerIsBetter`, consumed by
`ChannelPolicyTable.cs:37-39` for pricing/UI direction everywhere else. **This module does not need to
solve direction at all** — it only needs to answer "what game-unit value does this channel have at the
reference level," a magnitude fact, independent of which direction is better. `FamilyExpandGen`'s own
`FlatReferenceBase` delegate is only ever consulted for `Flat`-op families (confirmed: `FamilyExpansion.cs`'s
own doc — "`Increased`/`More` use the identity ratio and never touch a game curve at all"), and all 5
refused families here are authored with `Flat`. Direction correctness for a *future* `Increased`-op
family on these channels is already handled by the existing `ChannelDirection` machinery and is
explicitly not this module's concern.

**Adjacent, stale content found while checking Complication A**: the `quickening`/`flourishing`/
`swiftness`/`plating`/`carapace` family JSON entries each carry an authoring note claiming these
channels are "not bindable today, pending E16's channel-extension promotion." That promotion already
happened — `AtomKindRegistry.PrimaryChannels` (`= StatChannels.All`) already includes all 5. The note is
stale and should be corrected (a one-line content fix, not a code change) so a future reader doesn't
believe there's a second blocker beyond the missing curve this module supplies.

**Complication B — `arm1Max`/`arm2Max` are zombie-only legacy fields, no plant-side equivalent.** This
is real and this module does not paper over it: the new `BaseArm1Max`/`BaseArm2Max` curves are
authored and consumed as **zombie-side-only** baselines, exactly matching the field's own existing
scope (`atom-family-library.md:257`: "legacy Unity armor layers"; `:126-127`'s table rows separately
confirm both fields "zombie only"). A family targeting `arm1Max`/`arm2Max` for a plant-frame container
is refused by the existing `frames` gate on the family definition (`plating`'s own `g-armour.json`
entry already declares which frames it targets) — this module adds no new frame-legality logic; it
only supplies the missing reference-base number for the zombie-side case that already exists.

## 3. Mechanism — mirror the existing 3-channel pattern exactly

`src/FusionRpg.Core/Battle/BattleModels.cs` today — `ChannelLadderFor` at lines 307-315, `BaseAtk`/
`BaseDefense` at lines 322-323 (illustrative excerpt below, real code fully-qualifies
`FusionRpg.Core.Power.*` and has a longer exception message — see the file directly, not this excerpt,
before writing the new lines):

```csharp
static ChannelLadder ChannelLadderFor(string channelId) {
    var tuning = PowerTuningHub.Tuning;
    if (!tuning.ChannelsOrEmpty.TryGetValue(channelId, out var channel))
        throw new InvalidOperationException($"BattleRuleset: no '{channelId}' entry in ...");
    return new ChannelLadder(tuning.Curve.BMilli, PowerTuning.FixedPinValue, channel);
}
public static long BaseAtk(int level) => (_atkLadder ??= ChannelLadderFor("atk")).Value(level);
public static long BaseDefense(int level) => (_defenseLadder ??= ChannelLadderFor("defense")).Value(level);
```

This module adds five more lines of the exact same shape — `BaseArm1Max`, `BaseArm2Max`,
`BaseAttackInterval`, `BaseProduceInterval`, `BaseZombieSpeed` — each backed by a new
`tuning.ChannelsOrEmpty["<id>"]` entry, **no new code path, no new class**. `ChannelLadderFor` already
throws loudly rather than defaulting when a channel entry is missing (T5, tunables-ssot.md) — this
module's whole job is supplying those 5 missing entries, never adding a fallback.

**`data/tuning/power-scale.v2.json`'s `channels` block** gains 5 new rows, same shape as the existing
two (`{"cMilli": ..., "pinValue": ...}`):

```json
"channels": {
  "atk":            { "cMilli": 12000, "pinValue": 92 },
  "defense":        { "cMilli": 2000,  "pinValue": 22 },
  "arm1Max":        { "cMilli": ???,   "pinValue": ??? },
  "arm2Max":        { "cMilli": ???,   "pinValue": ??? },
  "attackInterval": { "cMilli": ???,   "pinValue": ??? },
  "produceInterval":{ "cMilli": ???,   "pinValue": ??? },
  "zombieSpeed":    { "cMilli": ???,   "pinValue": ??? }
}
```

**`tools/FamilyExpandGen/Program.cs:124-130`**'s `FlatReferenceBase` switch gains 5 new arms, each
calling the new `BattleRuleset.Base*` function — mechanical, mirrors the existing 3 lines exactly.

**`docs/architecture/power/ssot-power-scale.md` §10**'s closed inventory gains 5 new rows — the
reviewed part of this change. Each row states the channel, its unit class (`GameUnits` for the two
`arm*Max` channels, `Milliseconds` for the two intervals, `GameUnitsPerSecond` for `zombieSpeed` — all
three unit classes already exist in `ChannelUnits.cs`, none new), and its `ChannelDirection` (citing
`StatChannels.DirectionOf`, not re-declaring it).

## 4. The one real gap this spec does not resolve: calibration data

**The `???` cells above are not filled in here, deliberately** — inventing plausible-looking `cMilli`/
`pinValue` numbers for 5 new channels without a real source would be exactly the "private curve"
CLAUDE.md's power-ladder rule exists to prevent, worse for being done carelessly under a deadline. This
session did not trace how `atk`'s `92`/`12000` or `defense`'s `22`/`2000` were originally derived (no
doc found citing a source), so this spec cannot respond in kind for the new 5 with confidence.

**Named resolver and default, per this repo's own "gates must be answerable" rule** (a gate needs a
resolver and a stated fallback, never an open-ended wait): the implementer of this module, as step 1
before writing any code, pulls the real vanilla PvZ zombie/plant base values for these 5 fields
directly from the game's own shipped data (the same source `EntityStatWriter.WritePlantExtras`/
`WriteZombieExtras` already reads at runtime, per `spec-channel-extension.md`'s own history of these
fields) at a reference specimen (matching whatever baseline `atk`/`defense`'s own `92`/`22` pinValues
represent — a normal early-game zombie/plant, by their magnitude). If that data cannot be found or
reconstructed, the default is to **not** invent numbers: leave these 5 channels' `channels` block
entries absent (this module's own commits fall back to sub-scope — extend `BattleRuleset`/
`FamilyExpandGen`/`ssot-power-scale.md`'s mechanism and registration, all real code with no numbers to
guess, and land the 5 families as still-honestly-refused) rather than ship a fabricated calibration.

## 5. Testing strategy

New/extended test files: `tests/FusionRpg.Core.Tests/Battle/BattleRulesetTests.cs` (or wherever
`BaseAtk`/`BaseDefense` are already tested — extend, don't duplicate), `tests/FamilyExpandGen`-adjacent
coverage if any exists, else a direct `FamilyExpansion` test.

- Each new `Base*` function returns a deterministic, `long`, overflow-checked value for a range of
  levels (mirrors the existing `BaseAtk`/`BaseDefense` test shape exactly).
- `ChannelLadderFor` still throws for a channel absent from `channels` — proven with a channel name
  guaranteed never to be added, so this guard is never silently weakened.
- `FamilyExpandGen -- --check` against the real corpus, run for real after this module lands: zero
  `no referenceBaseGameUnits` refusals remain (assuming §4's calibration data was found and supplied —
  if not, this criterion is explicitly waived per §4's own stated fallback, and the test instead
  asserts the refusal reason for these 5 has NOT changed to something else, e.g. a crash).
- `ssot-power-scale.md` §10's own existing "closed inventory" self-check (if one exists as an automated
  guard) picks up the 5 new rows without manual re-registration elsewhere.

## 6. `RulesetVersion` — a named question this spec originally missed

Found by adversarial audit: `docs/architecture/decisions.md`'s "Power scale"/"Power dial" rows record
that every prior change to `power-scale.v2.json` in this repo's history was paired with an explicit
`RulesetVersion` bump-or-not verdict, entered into that file. Adding 5 new `channels` rows is exactly
this class of change, and this spec did not originally address it.

**Resolution**: before publishing the new `channels` rows, run the existing golden/regression suite
(`guard-power.ps1` plus whatever `Core.Tests` battle goldens exercise `BattleRuleset`) once with the
change staged. Adding rows for channels no *existing* content reads (`arm1Max`/`arm2Max`/
`attackInterval`/`produceInterval`/`zombieSpeed` have zero current `Flat`-op consumers besides the 5
families this module unblocks) should not move any existing golden — a purely additive `channels`
entry doesn't change `BaseAtk`/`BaseDefense`'s own values. If a golden moves anyway, that is new
information contradicting this expectation, and `RulesetVersion` must bump with an explicit decision
recorded in `decisions.md`, per this repo's own established precedent — never silently.

## 7. Boundaries

- **Always**: mirror `BaseAtk`/`BaseDefense`'s exact existing shape for the 5 new functions — no new
  curve TYPE, no new formula shape, only new calibration inputs to the one that already exists.
- **Always**: register new rows in `ssot-power-scale.md` §10 in the same commit as the code change —
  never let the closed inventory drift behind the code, the exact failure mode DESIGN-GATE.md's own
  incident log names repeatedly for this file.
- **Ask first / gate, per §4**: do not fabricate `cMilli`/`pinValue` calibration numbers. Find the real
  source or ship the mechanism without the numbers (families stay refused, honestly) — never guess.
- **Always, per §6**: run the golden/regression suite with the change staged before publishing, and
  record an explicit `RulesetVersion` bump-or-not verdict in `decisions.md` — never silently.
- **Never**: add a `ChannelDirection` declaration here — `attackInterval`/`produceInterval` already
  have one; this module only ever reads it, never redeclares or second-guesses it.
- **Never**: extend `arm1Max`/`arm2Max` to plant frames, or otherwise widen these fields' existing
  zombie-only scope — that would be a real, separate design change to `EntityStatWriter`'s own field
  ownership, well outside this module.
