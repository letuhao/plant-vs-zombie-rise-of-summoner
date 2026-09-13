# Spec: Tier propagation contract (`tier-propagation-contract`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `tier-propagation-contract`
**Owning programs:** `power` (the `thetaOffset` rule) + `creature-seed` (the ladder declarations)
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md) § The shape 1 (T-1…T-5), DO #5

---

## Objective

**Make "a tier ladder" a thing the repo can add without a 22-file change, and make the ways a tier
may reach a magnitude a guarded contract rather than a convention.**

⚠ **An earlier draft opened with a "6 sites vs ≥22 files" measurement presented as taken this
session. It named no ladder, no file list and no command, and it is not reproducible from this
document — so it is struck rather than repeated.** The argument does not need it: the restatement
count below is directly countable, and it is the real cost driver.

Nothing downstream in this initiative is expensive *because of tiers*. It becomes expensive when a
fifth ladder is added the way the previous four were. This module is the prerequisite that stops that
compounding, and it ships no new content and no player-visible behaviour.

⚠ **Sizing, corrected.** An earlier draft said *"a modest refactor of ~6–8 sites plus four guards."*
`tier-system-ideal.md:178-183` had already corrected that to **~30 sites across ~24 files**, and this
spec rebuilt on the pre-correction figure. **Treat the size as ~30 sites / ~24 files** until a counted
list exists; the seven restatements below are the *declaration* sites, not the consumers.

**Who the user is:** the next developer to add or widen a ladder. Success looks like a widening that
touches the tuning file and nothing else, with a red test if they miss a consumer.

### The five rules, as the contract they become

| Rule | Statement | Enforcement |
|---|---|---|
| **T-1** | A new tier ladder is declared **once**, in `data/tuning/`, and every consumer reads it | Review + T-2's guard |
| **T-2** | **No code may restate a ladder's ids** | A guard test that greps declarations |
| **T-3** | A derived count is **derived**, never mirrored | `CreatureRarityLadder.RungCount` becomes `All.Count`; a test asserts the identity |
| **T-4** | Coverage is **guarded**, not remembered — every rarity-keyed tuning map carries every rung | Generalise `RarityTuningCoverageTests` per ladder |
| **T-5a** | A tier reaches an **actor combat / derived magnitude** only as an additive per-rung `thetaOffset` into `Θ`, from a table, with `P(Θ)` applied once downstream | Review gate + a `ssot-power-scale.md` §10 row |
| **T-5b** | A tier **may** key an **economy cost** coefficient. `materials.v1.json` `operations.*.variable: "rung"` is the shipped precedent. Every such **cost ladder owes its own §10 row** | Review gate + the §10 row |

⚠ **T-5 was originally written as a single absolute — *"a tier reaches a magnitude ONLY as an additive
`thetaOffset`"* — and that is false of shipped code.** `data/tuning/materials.v1.json` prices **six**
verbs on `rung` as a **coefficient**, not an offset: `forge-gem` (souls ×30, essence ×3), `bore`
(×50/×3), `imbue` (×50/×3/×2), `elevate` (souls ×60, substrate ×2, shard ×1), `reroll-one` (×80/×2),
`reroll-all` (×200). Three sibling modules in this very initiative do the same
(`species-cost-shaping`, `rarity-promotion`, `item-upgrade-tree`).

**Shipping the single-clause version would have written a rule into the power SSOT that fails against
`materials.v1.json` on day one, and would have made three of this initiative's own modules illegal.**
Split into T-5a / T-5b above.

---

## What exists today — verified against code this session, not comments

### Built

- **`CreatureRarityLadder.All`** already derives from the enum:
  `Enum.GetValues<CreatureRarity>().OrderBy(r => (int)r).ToArray()`
  (`src/FusionRpg.Core/Creatures/CreatureRarityLadder.cs`, last member). This is T-3 done correctly,
  in the same file as the defect below.
- **`RarityTuningCoverageTests.cs:29-43`** asserts rung coverage over rarity-keyed tuning maps. It is
  the shape T-4 generalises, and it guards a **closed vocabulary**, which `validation-ssot.md` §1
  explicitly permits. It must never be extended to assert a corpus size.
  ⚠ **But it is not the blanket guard an earlier draft claimed.** `tier-system-ideal.md:290-292`:
  *"It covers **5 tuning files, not 11**, and names **two explicit exceptions** (`RecipeCost`,
  `InheritCostByRarity`, seven rungs each)."* **T-4 as originally worded — "every rarity-keyed tuning
  map carries every rung" — would fail on those two seven-rung maps.** T-4 must carry the exception
  list explicitly, or re-derive why those two are legitimately partial. See Open question 3.
- **`droptablegen/tuning.py:138-142 load_rarity_ids`** is the in-repo pattern T-1 describes: a ladder
  read from tuning at load, not restated.
- **`data/tuning/sockets.v1.json` `rarityGrant`** carries all ten rungs, and its own note records that
  *"the parser refuses a non-overlapping, non-monotonic or ceiling-breaking table at LOAD"* — the
  fail-closed posture T-4 wants everywhere.

### Wiring gap

- ⛔ **`CreatureRarityLadder.RungCount` is `public const int RungCount = 10`** while `All` in the same
  class derives from `Enum.GetValues`. **They disagree by construction** the moment the enum widens.
  `OneRungAbove` throws `"already the top rung (Almanac)"` off the const — so after a widening it
  would throw a message that **lies about the cause**, on a real rung that is not the top. No test
  guards the identity. This is DO #5 and the single highest-value line in the module.

### Real gap — the restated ladders

⚠ **Seven restatements, not four.** An earlier draft of this spec counted four; an independent
re-count found seven. The rarity ids are restated **five** times and the threat rung ids **twice**.

| # | Site | What it restates | Note |
|---|---|---|---|
| 1 | `src/FusionRpg.Core/Dungeon/Tuning/EncounterTuning.cs:40-43` `ThreatRungIds` | The ten `creature-threat.v1.json` threat rung ids (`nuisance`…`calamity`) | Its own comment concedes the file is the source (*"creature-threat.v1.json's own ten rung ids (:3-14)"*) |
| 2 | `tools/seedsmith/.../creatures/anchor/schema.py:44-47` | A Python `RARITY` tuple | |
| 3 | `tools/seedsmith/.../structures/anchor/schema.py:48-51` | A second Python `RARITY` tuple | |
| 5 | ⭐ `tools/seedsmith/.../actions/characteristic_pool/ladders.py:23-26` `RARITY_LADDER` | The ten rarity ids | **Found on re-count; not in the ideal** |
| 6 | ⭐ `tools/seedsmith/.../items/materialgen/vocab.py:51-54` `RARITY_RUNGS` | The ten rarity ids | **Found on re-count.** Same file carries the `len(ISSUABLE) == 27` pins `species-materials` must replace |
| 7 | ⭐ `tools/seedsmith/.../creatures/anchor/schema.py:40-43` `THREAT_BAND` | The ten **threat** rung ids | **Found on re-count** — a second threat restatement beside site 1, in the same file as site 2 |
| 4 | ⭐ **`src/FusionRpg.Core/Items/RarityLadder.cs` `RungIds`** | The ten item rarity ids, as a C# array | **Found this session; not in the ideal.** Its own class comment says the rows are *"seeded from `data/seed/rarity/ladder.v1.json` through the existing `AtomSeedFile.ReadRarity` → `RpgStore` import pipeline… this class does not duplicate that data"* — but `RungIds` does exactly that for the id list. The comment and the code disagree, and **code beats comments** |

⚠ **Site 4 changes the module's cost estimate honestly.** `RarityLadder.RungIds` is `public static
readonly` and is likely read from several places; converting it to a tuning read is larger than
deleting a private tuple. **It is specced as a named finding with a decision, not silently absorbed**
— see Open question 1.

---

## Tech stack

C# (.NET 8, `FusionRpg.Core` — Unity-free), Python 3 (seedsmith, `tools/seedsmith`), xUnit.
No new dependency. No FE surface.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RarityTuningCoverage"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~LadderDeclaration"
dotnet test tests/FusionRpg.Guard.Tests
$env:PYTHONPATH = "tools/seedsmith"; python -m pytest tools/seedsmith/tests -q
python scripts/audit-magic-numbers.py --summary
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Core/Creatures/CreatureRarityLadder.cs` | T-3's fix |
| `src/FusionRpg.Core/Items/RarityLadder.cs` | T-2 site 4 |
| `src/FusionRpg.Core/Dungeon/Tuning/EncounterTuning.cs` | T-2 site 1 |
| `tools/seedsmith/**/anchor/schema.py` | T-2 sites 2–3 |
| `tests/FusionRpg.Core.Tests/.../RarityTuningCoverageTests.cs` | T-4's template |
| `tests/FusionRpg.Guard.Tests/` | The new declaration guard |
| `docs/architecture/power/ssot-power-scale.md` §10 | Where a `thetaOffset` table registers |

## Code style

T-3's fix is one line and a comment that says *why* it is derived, since the const is what a future
reader would otherwise re-add:

```csharp
/// <summary>DERIVED, never mirrored (tier-propagation-contract T-3). A const here and
/// <see cref="All"/>'s Enum.GetValues disagree by construction the moment the enum widens, and
/// OneRungAbove would then throw a message that lies about the cause.</summary>
public static int RungCount => All.Count;
```

The T-4 generalisation asserts a **closed vocabulary**, and says so, because the neighbouring rule
bans the mirror-image mistake:

```csharp
// A ladder's rung set is a CLOSED VOCABULARY the code owns — pinning it is correct
// (validation-ssot.md §1). This must never be extended to assert a corpus SIZE: species,
// membership and recipe counts are READINGS and grow when content ships.
[Fact]
public void Every_rarity_keyed_tuning_map_carries_every_rung() { ... }
```

---

## Tunables

This module **introduces no balance number.** It relocates declarations; it does not author values.

| Number | Meaning | Owner |
|---|---|---|
| Per-rung `thetaOffset` for any new tier ladder | T-5's one sanctioned tier→magnitude path. Additive into `Θ`; `P(Θ)` applied once downstream | `data/tuning/<ladder>.v{n}.json` + a `ssot-power-scale.md` §10 row |

**Structural (stays `const`, with a comment saying why):** nothing new here. Note that
`sockets.v1.json:structuralCeiling` and `SocketCircuitSize = 4` are already correctly marked
structural and are **not** this module's to touch.

⚠ **A revision is the `version` field, not a new filename.** The shipped pattern is one file carrying
`schemaVersion` + `version` (`sockets.v1.json` is at `version: 2` today, having been re-owned by
module 16). The ideals' phrasing *"a new `sockets.v{n}.json` revision"* would have created a second
file. **Corrected here: bump `version` inside the existing file.**

## Numeric types

No magnitude is produced or consumed. `thetaOffset` values are small signed integers added into `Θ`
**before** `P(Θ)` — the widening rules bind at the `P(Θ)` site, which this module does not own.
Stated so the next module does not assume this one settled it: **every magnitude downstream of a
`thetaOffset` is `long`**, `P(Θ)` is quadratic, and a `float` magnitude stops being integer-exact at
`Θ` = 232, inside normal play (`CLAUDE.md` "Numeric overflow").

## ActorHub gate

**N/A and checked.** This module produces and consumes no actor combat / derived / AppliedCombat
number. T-5 constrains how a *later* module may route a tier into `Θ`; the composition of the
resulting magnitude stays where it already is, in `ActorHub`. **No private fold is introduced, and
none is permitted by this contract** — T-5 exists partly to prevent one.

## Testing strategy

xUnit, in `FusionRpg.Core.Tests` and `FusionRpg.Guard.Tests`; pytest for the seedsmith half.

| Level | What it asserts |
|---|---|
| Guard | **No C# or Python file outside the declaring site restates a ladder's ids.** Allowlist the declaring site by path, so adding a duplicate is a red test, not a review miss |
| Unit | `RungCount == All.Count` for every ladder — the T-3 identity |
| Unit | `OneRungAbove` throws only at the genuine top rung, proven by widening the enum in a test double rather than by asserting the literal 10 |
| Unit (T-4) | Every rarity-keyed tuning map carries every rung, per ladder, plus the TS union and the Python tuples |
| pytest | The seedsmith ladders load from tuning and no module-level tuple shadows them |

⛔ **No test in this module may assert a population count.** Rung counts are a closed vocabulary and
are pinned deliberately; species, membership, recipe and corpus sizes are **readings** and are
printed, never asserted (`validation-ssot.md`; `AGENTS.md` Hard boundaries).

## Boundaries

**Always**
- Delete a duplicate declaration rather than syncing it.
- State, in a comment at each converted site, that the ladder is read and why.
- Run `FusionRpg.Guard.Tests` and the seedsmith pytest suite before committing.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- Converting `RarityLadder.RungIds` to a tuning read (Open question 1) — it has real consumers.
- Any change to `ssot-power-scale.md` §10, which is a registry, not a doc.
- Widening any ladder enum. This module makes widening *safe*; it does not authorise one.

**Never**
- Introduce a new `f(level)` curve. Contests read `Θ`; magnitudes read `P(Θ)`.
- Let a tier become a **multiplier** on a magnitude — `ssot-rarity.md` §3.6 bans `CurveInput.Rarity`
  on item containers, and the reason is measured: a multiplier on the rung makes rarity dominant and
  destroys the overlap the ladder depends on.
- Collapse the four ladders into one enum. The owner ruled it lazy design 2026-09-12 and the evidence
  agrees — they already mean different things.
- Assert a corpus size in a guard.
- Hand-edit generated seed data to make a guard pass.

## Success criteria

1. `CreatureRarityLadder.RungCount` is derived, and a test fails if a mirror is reintroduced.
2. `OneRungAbove`'s throw is proven correct at a *widened* enum width, not at width 10.
3. A guard test fails on a newly added restatement of any ladder's ids, and passes on HEAD after the
   duplicates are resolved.
4. Sites 1–3 no longer restate ids; site 4 has a recorded decision (convert or allowlist with a
   stated reason).
5. T-4 coverage assertions exist for every ladder, and each names the closed vocabulary it pins.
6. `ssot-power-scale.md` §10 carries the rule that a tier contributes an additive per-rung
   `thetaOffset` and never a curve.
7. Guards green: `guard-actor-hub.ps1`, `guard-dal.ps1`, `guard-test-substrate.ps1`.
8. No tuning value changed — this refactor is behaviour-preserving, and a golden re-bless would be
   evidence it is not.

## Open questions

1. **`RarityLadder.RungIds` — convert to a tuning read, or allowlist it as the declaring site?**
   It is `public static readonly` with real consumers, and its own class comment already claims the
   seed file is the authority. Converting is correct by T-1; allowlisting is honest if the array *is*
   the boot-time source the seed import validates against. **Recommendation: allowlist it as the
   declaring site and add the assertion that it matches `data/seed/rarity/ladder.v1.json` value for
   value** — that gets T-2's guarantee without a load-order change. One owner call.
2. **Does the guard scan Python as well as C#?** Recommendation: yes — two of the four duplicates are
   Python, and they are the pair with nothing checking they agree.
